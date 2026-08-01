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
}
