using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Combat;
using MonsterMart.Core;
using MonsterMart.Data;
using MonsterMart.Expeditions;

namespace MonsterMart.Staff
{
    /// <summary>
    /// 远征中的怪物员工 — 设计文档 §3.3：
    /// 「所有成员自动普通攻击，玩家负责走位、躲避预警区和主动技能时机」。
    ///
    /// 也就是说：跟随和普攻全自动，玩家唯一的主动输入是技能键。
    /// </summary>
    public class StaffFollower : ExpeditionActor
    {
        public StaffData Data { get; private set; }

        /// <summary>队内序号，决定技能热键（1~3）和站位角度。</summary>
        public int SquadIndex { get; private set; }

        /// <summary>名册里的这份持久状态 —— 打怪升级的等级/经验就记在这上面。</summary>
        StaffRoster.Entry _rosterEntry;

        /// <summary>当前战斗等级，给远征界面显示用。</summary>
        public int Level => _rosterEntry != null ? _rosterEntry.level : 1;

        public float SkillCooldownRemaining { get; private set; }
        public bool SkillReady => IsAlive && SkillCooldownRemaining <= 0f;

        /// <summary>本次远征的技能冷却倍率（§3.6 加班狂热）。</summary>
        public float SkillCooldownScale =>
            Game.Expedition != null ? Game.Expedition.SkillCooldownMultiplier : 1f;

        /// <summary>算上强化之后的实际冷却时长 —— 界面和用例都读它。</summary>
        public float EffectiveSkillCooldown => Data.skillCooldown * SkillCooldownScale;

        /// <summary>
        /// 把技能冷却包装成「法力值」的样子给信息面板看：冷却好 = MP 满，可以放技能；
        /// 冷却中 = MP 还没攒够。技能本身还是纯冷却驱动，不消耗资源，这里只是换一种
        /// 更符合直觉的呈现方式（用户反馈要看到 HP/MP）。
        /// </summary>
        public float ManaPercent => EffectiveSkillCooldown <= 0f
            ? 100f
            : Mathf.Clamp01(1f - SkillCooldownRemaining / EffectiveSkillCooldown) * 100f;

        protected override float Radius => 0.28f;

        /// <summary>连续这么久几乎没挪动就判定卡住，直接归队。</summary>
        const float StuckSeconds = 1.6f;
        const float StuckEpsilonSqr = 0.0004f;

        // ------------------------------------------------------------------
        // 普通攻击 —— 出手（Windup）→ 命中判定 → 收招（Recover）。
        // 用户反馈里明确要求「看得到攻击动作、动作完成了才扣血」，
        // 所以这里不能再是冷却好瞬间就结算伤害。
        // ------------------------------------------------------------------
        enum AttackPhase { Ready, Windup, Recover }

        AttackPhase _attackPhase = AttackPhase.Ready;
        float _phaseTimer;
        EnemyController _attackTarget;
        Vector2 _windupDir;

        /// <summary>出手动作时长 —— 命中判定卡在这段时间结束的那一刻，不是冷却好的瞬间。</summary>
        const float WindupSeconds = 0.22f;

        /// <summary>命中之后收招回位的时长。</summary>
        const float RecoverSnapSeconds = 0.15f;

        /// <summary>出手时朝目标探身的距离，纯视觉表现，不影响网格碰撞判定。</summary>
        const float LungeDistance = 0.22f;

        /// <summary>正在出手或收招 —— 这段时间里站定不挪窝，动作才看得清楚。</summary>
        public bool IsAttacking => _attackPhase != AttackPhase.Ready;

        float _stuckTimer;
        Vector2 _lastPosition;

        public void Initialize(StaffData data, int squadIndex, Vector2Int startCell)
        {
            Data = data;
            SquadIndex = squadIndex;
            _rosterEntry = StaffRoster.Get(data.staffId);

            // 打怪升级：等级越高，带出来的血量也越厚（伤害加成在 OutgoingDamage 里）。
            float maxHealth = data.maxHealth * StaffRoster.HealthMultiplier(_rosterEntry);

            BuildBody(SpriteFactory.Character(data), maxHealth);
            PlaceAtCell(startCell);
        }

        void Update()
        {
            if (Game.Manager == null || Game.Manager.State != GameState.Expedition) return;
            if (!IsAlive) return;

            float dt = Time.deltaTime;

            if (SkillCooldownRemaining > 0f) SkillCooldownRemaining -= dt;

            TickAttack(dt);
            if (IsAttacking) return;   // 出手/收招过程中站定不挪窝，动作才看得清楚

            var enemy = SelectTarget();

            // 敌人在攻击范围内就地开打，否则回到队长身边保持队形
            if (enemy != null && DistanceTo(enemy) <= Data.attackRange)
                BeginAttack(enemy);
            else if (enemy != null && DistanceTo(enemy) <= Data.attackRange * 4f)
                StepToward(enemy.Position, Data.moveSpeed, dt);
            else
                FollowCaptain(dt);
        }

        /// <summary>
        /// 队形站位：按队内序号把整圈均分，三个人互不重叠（§3.3「自动保持队形」）。
        /// 落点撞墙时退到最近的可行走格，否则队友会一直贴着墙推。
        /// </summary>
        public Vector2 FormationSlot(ExpeditionActor captain, int squadCount)
        {
            if (captain == null) return Position;

            float step = Mathf.PI * 2f / Mathf.Max(1, squadCount);
            float angle = Mathf.PI * 0.5f + SquadIndex * step;

            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Data.followDistance;
            var slot = captain.Position + offset;

            var grid = Grid;
            if (grid == null) return slot;

            var cell = StoreGrid.WorldToCell(slot);
            if (grid.IsWalkable(cell)) return slot;

            return StoreGrid.CellToWorld(grid.NearestWalkable(cell));
        }

        void FollowCaptain(float dt)
        {
            var expedition = Game.Expedition;
            var captain = expedition != null ? expedition.Captain : null;
            if (captain == null) return;

            int squadCount = expedition.Squad.Count;
            var slot = FormationSlot(captain, squadCount);
            float distance = (slot - Position).magnitude;

            if (distance <= 0.2f)
            {
                _stuckTimer = 0f;
                _lastPosition = Position;
                return;
            }

            StepToward(slot, Data.moveSpeed, dt);

            // §18 第二阶段的完成标准里明确写了「队友不会长期卡住」：
            // 一直在往队形位走却几乎没位移，就直接归队，不让玩家等。
            if ((Position - _lastPosition).sqrMagnitude < StuckEpsilonSqr) _stuckTimer += dt;
            else _stuckTimer = 0f;

            _lastPosition = Position;

            if (_stuckTimer >= StuckSeconds) Unstick(captain);
        }

        /// <summary>卡住了就瞬移回队长身边最近的可行走格。</summary>
        public void Unstick(ExpeditionActor captain)
        {
            _stuckTimer = 0f;
            if (captain == null) return;

            var grid = Grid;
            if (grid == null) return;

            PlaceAtCell(grid.NearestWalkable(captain.Cell));
            _lastPosition = Position;
        }

        /// <summary>
        /// 进入出手动作，命中判定要等 <see cref="TickAttack"/> 把 Windup 走完才结算。
        /// 公开出来是为了让无头用例能直接触发一次攻击，不用真的等 Update 帧循环
        /// （编辑器非播放模式下 Update 根本不会被引擎调用，见「可测试性抽取模式」）。
        /// </summary>
        public void BeginAttack(EnemyController enemy)
        {
            _attackTarget = enemy;
            _attackPhase = AttackPhase.Windup;
            _phaseTimer = WindupSeconds;

            var dir = enemy.Position - Position;
            _windupDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
        }

        /// <summary>
        /// 推进出手/收招动作。从 Update 拆出来是为了让无头用例能直接驱动状态机，
        /// 不用真的等好几帧（见「可测试性抽取模式」）。
        /// </summary>
        public void TickAttack(float dt)
        {
            if (_attackPhase == AttackPhase.Ready || dt <= 0f) return;

            _phaseTimer -= dt;
            UpdateAttackVisual();

            if (_phaseTimer > 0f) return;

            if (_attackPhase == AttackPhase.Windup)
            {
                ResolveAttack();
                _attackPhase = AttackPhase.Recover;
                _phaseTimer = Mathf.Max(0.05f, Data.attackInterval - WindupSeconds);
            }
            else
            {
                ResetAttackVisual();
                _attackPhase = AttackPhase.Ready;
                _attackTarget = null;
            }
        }

        /// <summary>出手动作走完，命中判定在这一刻结算 —— 不是按键/冷却好的那一瞬间。</summary>
        void ResolveAttack()
        {
            var target = _attackTarget;
            if (target == null || !target.IsAlive || DistanceTo(target) > Data.attackRange * 1.3f)
                return;   // 出手过程中目标死了或跑远了，这一下打空：不扣血也不弹特效

            float dealt = target.TakeDamage(OutgoingDamage(Data.attackDamage, target), DamageKind.Basic);
            if (dealt > 0f)
            {
                SpawnHitEffect(target.Position);
                Game.Audio?.PlayPickup();
            }
        }

        void UpdateAttackVisual()
        {
            if (_sprite == null) return;

            if (_attackPhase == AttackPhase.Windup)
            {
                float t = Mathf.Clamp01(1f - _phaseTimer / WindupSeconds);
                float lunge = Mathf.Sin(t * Mathf.PI * 0.5f) * LungeDistance;   // 命中瞬间正好冲到最远
                _sprite.transform.localPosition = _windupDir * lunge;
            }
            else
            {
                float recoverDuration = Mathf.Max(0.01f, Data.attackInterval - WindupSeconds);
                float elapsed = recoverDuration - _phaseTimer;
                float snapT = Mathf.Clamp01(elapsed / RecoverSnapSeconds);
                _sprite.transform.localPosition = _windupDir * Mathf.Lerp(LungeDistance, 0f, snapT);
            }
        }

        void ResetAttackVisual()
        {
            if (_sprite != null) _sprite.transform.localPosition = Vector3.zero;
        }

        void SpawnHitEffect(Vector2 worldPos)
        {
            var go = new GameObject("HitEffect");
            go.transform.SetParent(transform.parent, false);
            go.transform.localPosition = worldPos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.HitSpark(new Color(1f, 0.92f, 0.45f));
            sr.sortingOrder = SortingLayers.FixtureOverlay + 2;

            Lifetime.Destroy(go, 0.16f);
        }

        /// <summary>
        /// 打出去之前的加成与折扣：
        ///   · §4.2 吸血鬼·维拉「对精英怪额外伤害」。
        ///   · §3.6 史莱姆快递的代价「史莱姆携带货物时攻击力下降」。
        /// 减伤在敌人那一侧结算（精英护甲 / Boss 喷口护盾），这里不碰。
        /// </summary>
        public float OutgoingDamage(float baseDamage, EnemyController enemy)
        {
            // 打怪升级：等级带来的伤害加成叠在最前面，其余加成/折扣照旧乘上去。
            float damage = baseDamage * StaffRoster.DamageMultiplier(_rosterEntry);

            if (enemy != null && enemy.Tier != EnemyTier.Normal)
                damage *= Mathf.Max(1f, Data.eliteDamageMultiplier);

            if (Data.monsterType == MonsterType.Slime && Game.Expedition != null)
                damage *= Game.Expedition.SlimeAttackMultiplier;

            return damage;
        }

        /// <summary>玩家按下技能键 — 设计文档 §3.2「技能按钮 1～3」。</summary>
        public bool TryUseSkill()
        {
            if (!SkillReady) return false;

            // §3.6 加班狂热：冷却缩短，代价是每次施法都透支自己
            SkillCooldownRemaining = Data.skillCooldown * SkillCooldownScale;
            SpawnSkillBurst();

            var expedition = Game.Expedition;
            if (expedition != null && expedition.SkillSelfDamage > 0f)
                Health.Damage(expedition.SkillSelfDamage);

            var enemies = expedition != null ? expedition.Enemies : null;
            if (enemies == null) return true;

            int hit = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                if (DistanceTo(enemy) > Data.skillRadius) continue;

                // 技能不吃精英护甲 —— 这正是精英房逼玩家管技能时机的地方（§3.3）
                enemy.TakeDamage(OutgoingDamage(Data.skillDamage, enemy), DamageKind.Skill);
                hit++;
            }

            Game.Audio?.PlaySpirit();
            Game.UI?.Hud?.Flash(hit > 0
                ? $"{Data.displayName} 使用了「{Data.skillName}」，命中 {hit} 个敌人"
                : $"{Data.displayName} 使用了「{Data.skillName}」，但没打到人");
            return true;
        }

        void SpawnSkillBurst()
        {
            var go = new GameObject("SkillBurst");
            go.transform.SetParent(transform.parent, false);
            go.transform.localPosition = _position;
            go.transform.localScale = Vector3.one * Data.skillRadius * 2f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle(new Color(0.55f, 0.92f, 0.85f, 0.5f), 32);
            sr.sortingOrder = SortingLayers.FixtureOverlay;

            Destroy(go, 0.25f);
        }

        /// <summary>
        /// 选打谁。玩家标记过目标就优先打标记的那个
        /// （§3.2「目标标记：优先攻击指定敌人」），否则打最近的。
        /// </summary>
        public EnemyController SelectTarget()
        {
            var expedition = Game.Expedition;
            if (expedition == null) return null;

            var marked = expedition.MarkedTarget;
            if (marked != null && marked.IsAlive) return marked;

            var enemies = expedition.Enemies;
            if (enemies == null) return null;

            EnemyController best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;

                float d = DistanceTo(enemy);
                if (d >= bestDistance) continue;

                bestDistance = d;
                best = enemy;
            }
            return best;
        }
    }
}
