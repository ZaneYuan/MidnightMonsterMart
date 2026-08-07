using UnityEngine;
using MonsterMart.Core;
using MonsterMart.Data;
using MonsterMart.Expeditions;

namespace MonsterMart.Combat
{
    /// <summary>
    /// 远征敌人 — 设计文档 §3.5「暮光森林」。普通敌人、精英和区域 Boss 共用这一个控制器，
    /// 差异全部来自 <see cref="EnemyData"/>。
    ///
    /// §3.3 明确要求「敌人攻击必须有清晰前摇」，所以攻击分两步：
    /// 先站定亮起前摇、再结算伤害。玩家在前摇窗口里走开就能躲掉。
    ///
    /// 受击一律走 <see cref="TakeDamage"/>，不要直接调 Health.Damage ——
    /// 精英护甲和 Boss 的喷口护盾都挂在那一层。
    /// </summary>
    public class EnemyController : ExpeditionActor
    {
        public EnemyData Data { get; private set; }

        public EnemyTier Tier => Data != null ? Data.tier : EnemyTier.Normal;
        public bool IsElite => Tier == EnemyTier.Elite;
        public bool IsBoss => Tier == EnemyTier.Boss;

        /// <summary>
        /// 还有孢子喷口开着吗 — §3.3 的「关闭装置」环节。
        /// 只有配了喷口的敌人（孢子巨兽）才会被护盾影响。
        /// </summary>
        public bool IsShielded =>
            Data != null && Data.UsesSporeVents &&
            Game.Expedition != null && Game.Expedition.OpenVentCount > 0;

        protected override float Radius => Data != null ? 0.34f * Data.bodyScale : 0.34f;

        enum Phase { Idle, Chase, Telegraph, Recover }

        Phase _phase = Phase.Idle;
        float _phaseTimer;
        ExpeditionActor _target;
        SpriteRenderer _telegraph;
        SpriteRenderer _marker;
        SpriteRenderer _aura;

        public void Initialize(EnemyData data, Vector2Int startCell)
        {
            Data = data;

            BuildBody(MonsterMart.Art.SpriteFactory.Character(data), data.maxHealth);
            PlaceAtCell(startCell);
            BuildTelegraph();
            BuildMarker();
            ApplyTierLook();

            Health.OnDied += HandleDeath;
        }

        /// <summary>灰盒阶段靠体型和护盾光环把普通 / 精英 / Boss 一眼区分开。</summary>
        void ApplyTierLook()
        {
            // 只放大身体本身。整个节点一起缩放会把血条、前摇圈和光环一并放大，
            // 前摇圈的大小是攻击范围的可视化，不能跟着体型走。
            if (_sprite != null)
                _sprite.transform.localScale = Vector3.one * Data.bodyScale;

            if (_barRoot != null)
                _barRoot.localPosition = new Vector3(0f, BarHeight * Data.bodyScale, 0f);

            if (Data.tier == EnemyTier.Normal) return;

            var go = new GameObject("TierAura");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * Data.bodyScale * 1.7f;

            _aura = go.AddComponent<SpriteRenderer>();
            _aura.sprite = MonsterMart.Art.SpriteFactory.Circle(
                Data.tier == EnemyTier.Boss
                    ? new Color(0.85f, 0.45f, 0.95f, 0.30f)
                    : new Color(0.98f, 0.78f, 0.35f, 0.26f), 32);
            _aura.sortingOrder = SortingLayers.Floor + 2;
        }

        // ------------------------------------------------------------------
        // 受击 — 精英护甲（§3.4）与 Boss 喷口护盾（§3.3）
        // ------------------------------------------------------------------
        /// <summary>
        /// 这一击最终会被打几折。
        ///   · 精英护甲只吃普通攻击，技能和环境伤害打满。
        ///   · 喷口还开着时 Boss 再叠一层护盾，不关装置基本打不动。
        /// </summary>
        public float DamageMultiplier(DamageKind kind)
        {
            if (Data == null) return 1f;

            float multiplier = 1f;

            if (kind == DamageKind.Basic)
                multiplier *= 1f - Mathf.Clamp01(Data.basicAttackResist);

            if (IsShielded)
                multiplier *= Mathf.Clamp01(Data.shieldedDamageMultiplier);

            return multiplier;
        }

        /// <summary>
        /// 所有对敌人的伤害都应该走这里。返回实际打进去的数值。
        /// </summary>
        public float TakeDamage(float amount, DamageKind kind)
        {
            if (amount <= 0f || !IsAlive) return 0f;

            float final = amount * DamageMultiplier(kind);
            if (final <= 0f) return 0f;

            Health.Damage(final);
            return final;
        }

        void BuildMarker()
        {
            var go = new GameObject("TargetMarker");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 1.45f;

            _marker = go.AddComponent<SpriteRenderer>();
            _marker.sprite = MonsterMart.Art.SpriteFactory.Circle(
                new Color(1f, 0.85f, 0.30f, 0.55f), 32);
            _marker.sortingOrder = SortingLayers.Floor + 4;
            _marker.enabled = false;
        }

        /// <summary>被玩家标记为优先攻击目标 — 设计文档 §3.2。</summary>
        public void SetMarked(bool marked)
        {
            if (_marker != null) _marker.enabled = marked;
        }

        void BuildTelegraph()
        {
            var go = new GameObject("Telegraph");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (Data.attackRange * 1.6f);

            _telegraph = go.AddComponent<SpriteRenderer>();
            _telegraph.sprite = MonsterMart.Art.SpriteFactory.Circle(
                new Color(0.95f, 0.35f, 0.30f, 0.35f), 32);
            _telegraph.sortingOrder = SortingLayers.Floor + 3;
            _telegraph.enabled = false;
        }

        void Update()
        {
            if (Game.Manager == null || Game.Manager.State != GameState.Expedition) return;
            if (!IsAlive) return;

            float dt = Time.deltaTime;
            _phaseTimer -= dt;

            RefreshAura();

            switch (_phase)
            {
                case Phase.Idle: TickIdle(); break;
                case Phase.Chase: TickChase(dt); break;
                case Phase.Telegraph: TickTelegraph(); break;
                case Phase.Recover: if (_phaseTimer <= 0f) Enter(Phase.Chase, 0f); break;
            }
        }

        void TickIdle()
        {
            _target = FindTarget();
            if (_target != null) Enter(Phase.Chase, 0f);
        }

        void TickChase(float dt)
        {
            if (_target == null || !_target.IsAlive)
            {
                _target = FindTarget();
                if (_target == null) { Enter(Phase.Idle, 0f); return; }
            }

            float distance = DistanceTo(_target);

            if (distance > Data.aggroRadius) { _target = null; Enter(Phase.Idle, 0f); return; }

            if (distance <= Data.attackRange)
            {
                Enter(Phase.Telegraph, Data.telegraphSeconds);
                if (_telegraph != null) _telegraph.enabled = true;
                return;
            }

            StepToward(_target.Position, Data.moveSpeed, dt);
        }

        void TickTelegraph()
        {
            if (_phaseTimer > 0f) return;

            if (_telegraph != null) _telegraph.enabled = false;

            // 前摇结束才结算；这段时间里走出攻击范围就算躲开了
            if (_target != null && _target.IsAlive && DistanceTo(_target) <= Data.attackRange)
            {
                _target.Health.Damage(Data.attackDamage);
                SpawnHitEffect(_target.Position);
                Game.Audio?.PlayAngry();
            }

            Enter(Phase.Recover, Data.attackInterval);
        }

        void Enter(Phase phase, float duration)
        {
            _phase = phase;
            _phaseTimer = duration;
        }

        ExpeditionActor FindTarget()
        {
            var expedition = Game.Expedition;
            if (expedition == null) return null;

            ExpeditionActor best = null;
            float bestDistance = Data.aggroRadius;

            var candidates = expedition.Allies;
            for (int i = 0; i < candidates.Count; i++)
            {
                var actor = candidates[i];
                if (actor == null || !actor.IsAlive) continue;

                float d = DistanceTo(actor);
                if (d >= bestDistance) continue;

                bestDistance = d;
                best = actor;
            }
            return best;
        }

        /// <summary>护盾还在时 Boss 的光环更亮 —— 玩家得看得出「现在打它没用」。</summary>
        void RefreshAura()
        {
            if (_aura == null) return;

            if (!Data.UsesSporeVents)
            {
                _aura.color = new Color(0.98f, 0.78f, 0.35f, 0.26f);
                return;
            }

            _aura.color = IsShielded
                ? new Color(0.60f, 0.95f, 0.45f, 0.45f)   // 护盾中：孢子绿
                : new Color(0.95f, 0.40f, 0.35f, 0.40f);  // 破防：危险红
        }

        /// <summary>敌人打中我方时的打击特效，和员工普攻的命中特效用不同色调区分「谁挨打了」。</summary>
        void SpawnHitEffect(Vector2 worldPos)
        {
            var go = new GameObject("HitEffect");
            go.transform.SetParent(transform.parent, false);
            go.transform.localPosition = worldPos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MonsterMart.Art.SpriteFactory.HitSpark(new Color(0.95f, 0.35f, 0.32f));
            sr.sortingOrder = SortingLayers.FixtureOverlay + 2;

            Lifetime.Destroy(go, 0.16f);
        }

        void HandleDeath()
        {
            if (_telegraph != null) _telegraph.enabled = false;
            if (_aura != null) _aura.enabled = false;
            if (_sprite != null) _sprite.color = new Color(1f, 1f, 1f, 0.25f);
            if (_barRoot != null) _barRoot.gameObject.SetActive(false);

            Game.Expedition?.OnEnemyDefeated(this);
            Lifetime.Destroy(gameObject, 0.35f);
        }
    }
}
