using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Combat;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.Expeditions
{
    /// <summary>
    /// 远征队长 —— 玩家直接操作的角色（设计文档 §3.3
    /// 「玩家控制队长移动，其他成员自动保持队形」）。
    ///
    /// 刻意和便利店的 PlayerController 分开：那个类绑死了店铺网格、
    /// 携带商品和设施交互，灰盒阶段没必要为了复用去动它。
    ///
    /// 队长本来只能走路、采集、拾取——用户反馈「队长站在那里不动」，
    /// 要求队长也能打。给的是手动技能而不是自动普攻（自动普攻是员工的活，
    /// 队长的定位是「玩家亲自出手的大招」）：不吃资源、纯冷却驱动，
    /// 伤害高到直接秒杀范围内除 Boss 外的一切——Boss 战还是得靠关喷口，
    /// 这个技能不该绕过那套机制。
    /// </summary>
    public class ExpeditionCaptain : ExpeditionActor
    {
        public const float MaxHealth = 120f;
        public const float WalkSpeed = 4.2f;
        public const float SprintSpeed = 6.6f;

        /// <summary>掉落物的自动拾取半径基线 —— §3.6「史莱姆快递：扩大拾取范围」在此之上加倍。</summary>
        public const float PickupRadius = 1.1f;

        // ------------------------------------------------------------------
        // 队长技能 · 拼死一击 —— 手动触发、纯冷却、不吃 MP。
        // ------------------------------------------------------------------
        public const float SkillCooldown = 16f;
        public const float SkillRadius = 3f;

        /// <summary>足够打死原型里任何非 Boss 敌人的数值，不用逐个抄血量上限。</summary>
        const float SkillDamage = 99999f;

        public float SkillCooldownRemaining { get; private set; }
        public bool SkillReady => IsAlive && SkillCooldownRemaining <= 0f;

        /// <summary>本次远征的移动速度倍率（§3.6 易碎品保险的代价）。</summary>
        public float SpeedScale =>
            Game.Expedition != null ? Game.Expedition.CaptainSpeedMultiplier : 1f;

        /// <summary>算上强化之后的实际步行 / 冲刺速度 —— 用例读它。</summary>
        public float EffectiveWalkSpeed => WalkSpeed * SpeedScale;
        public float EffectiveSprintSpeed => SprintSpeed * SpeedScale;

        protected override float Radius => 0.32f;

        public void Initialize(Vector2Int startCell)
        {
            // 队长就是玩家本人 —— 用和便利店里同一张贴图，别再是个色块圆点。
            BuildBody(MonsterMart.Art.SpriteFactory.PlayerSprite(), MaxHealth);
            PlaceAtCell(startCell);
            SkillCooldownRemaining = 0f;
        }

        void Update()
        {
            if (Game.Manager == null || Game.Manager.State != GameState.Expedition) return;
            if (!IsAlive) return;

            if (SkillCooldownRemaining > 0f) SkillCooldownRemaining -= Time.deltaTime;

            if (Game.UI != null && Game.UI.BlocksWorldInput) return;

            InputReader.Tick();

            var axis = InputReader.MoveAxis;
            if (axis.sqrMagnitude > 0.0001f)
            {
                // §3.6 易碎品保险的代价：背着保险箱走得慢
                float speed = (InputReader.Sprint ? SprintSpeed : WalkSpeed) * SpeedScale;
                MoveBy(axis * speed * Time.deltaTime);
            }

            // §3.2「交互键：采集、开箱、救援、进入传送点」
            // 一个键分流：Boss 战里是关孢子喷口，其余时候是采集。
            if (InputReader.InteractPressed) Game.Expedition?.Interact();

            if (InputReader.CaptainSkillPressed) TryUseSkill();

            Game.Expedition?.TryPickupNear(this);
        }

        /// <summary>
        /// 拼死一击：范围内除 Boss 外的敌人直接秒杀。返回是否真的放出去了
        /// （冷却没到时不算）。公开出来是为了让无头用例能直接触发，不用等
        /// InputReader 走一遍真实按键。
        /// </summary>
        public bool TryUseSkill()
        {
            if (!SkillReady) return false;

            SkillCooldownRemaining = SkillCooldown;
            SpawnSkillBurst();

            var enemies = Game.Expedition != null ? Game.Expedition.Enemies : null;
            int hit = 0;

            if (enemies != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy == null || !enemy.IsAlive || enemy.IsBoss) continue;
                    if (DistanceTo(enemy) > SkillRadius) continue;

                    enemy.TakeDamage(SkillDamage, DamageKind.Skill);
                    hit++;
                }
            }

            Game.Audio?.PlaySpirit();
            Game.UI?.Hud?.Flash(hit > 0
                ? $"拼死一击命中 {hit} 个敌人"
                : "拼死一击扑了个空");
            return true;
        }

        void SpawnSkillBurst()
        {
            var go = new GameObject("CaptainSkillBurst");
            go.transform.SetParent(transform.parent, false);
            go.transform.localPosition = Position;
            go.transform.localScale = Vector3.one * SkillRadius * 2f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle(new Color(1f, 0.55f, 0.35f, 0.55f), 32);
            sr.sortingOrder = SortingLayers.FixtureOverlay;

            Lifetime.Destroy(go, 0.25f);
        }
    }
}
