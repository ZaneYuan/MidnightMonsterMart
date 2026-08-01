using UnityEngine;
using MonsterMart.Core;
using MonsterMart.Player;

namespace MonsterMart.Store
{
    public enum InteractionKind
    {
        None,
        Instant,
        Hold
    }

    /// <summary>
    /// 所有可交互对象的基类 — 设计文档 §3.1「玩家可以交互的对象」。
    /// 交互不使用物理，靠 InteractableRegistry 的距离查询完成。
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        /// <summary>玩家判定距离时使用的锚点（默认为自身位置）。</summary>
        public virtual Vector2 InteractAnchor => transform.position;

        /// <summary>
        /// 玩家到本对象的判定距离。默认取到锚点的距离；
        /// 占据多个格子的设施会重写成「到矩形边缘的距离」，
        /// 这样贴着任意一边都能交互，而不是只有正对中心那一格。
        /// </summary>
        public virtual float DistanceTo(Vector2 point)
            => Vector2.Distance(point, InteractAnchor);

        /// <summary>当前是否可交互；返回 false 时完全不显示提示。</summary>
        public abstract bool IsAvailable(PlayerController player);

        /// <summary>提示文案，例如「[E] 补充货架」。</summary>
        public abstract string GetPrompt(PlayerController player);

        public virtual InteractionKind Kind => InteractionKind.Instant;

        /// <summary>Hold 型交互需要按住多久。</summary>
        public virtual float HoldSeconds(PlayerController player) => 0f;

        /// <summary>瞬发交互，或长按完成时触发。</summary>
        public abstract void OnInteract(PlayerController player);

        /// <summary>长按过程中每帧回调，t ∈ [0,1]。</summary>
        public virtual void OnHoldProgress(PlayerController player, float t) { }

        /// <summary>长按被中断。</summary>
        public virtual void OnHoldCancelled(PlayerController player) { }

        protected virtual void OnEnable() => InteractableRegistry.Register(this);
        protected virtual void OnDisable() => InteractableRegistry.Unregister(this);
    }

    /// <summary>
    /// 占据若干格子的固定设施（货架、收银台、仓库门、灵界包装台、镜子、垃圾桶）。
    /// 交互距离按矩形边缘算，所以贴着上下左右任意一边都能按 E。
    /// </summary>
    public abstract class FixtureInteractable : Interactable
    {
        public CellRect cells;

        public override Vector2 InteractAnchor => cells.CenterWorld;

        public override float DistanceTo(Vector2 point) => cells.DistanceToWorld(point);
    }
}
