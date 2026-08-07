using UnityEngine;
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
    /// </summary>
    public class ExpeditionCaptain : ExpeditionActor
    {
        public const float MaxHealth = 120f;
        public const float WalkSpeed = 4.2f;
        public const float SprintSpeed = 6.6f;

        /// <summary>掉落物的自动拾取半径基线 —— §3.6「史莱姆快递：扩大拾取范围」在此之上加倍。</summary>
        public const float PickupRadius = 1.1f;

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
        }

        void Update()
        {
            if (Game.Manager == null || Game.Manager.State != GameState.Expedition) return;
            if (!IsAlive) return;
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

            Game.Expedition?.TryPickupNear(this);
        }
    }
}
