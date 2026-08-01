using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Player;

namespace MonsterMart.Store
{
    /// <summary>
    /// 可交互对象登记表。玩家用它做「最近的可交互对象」查询，
    /// 替代 Physics2D.OverlapCircle —— 工程不引入任何物理组件。
    /// </summary>
    public static class InteractableRegistry
    {
        static readonly List<Interactable> _all = new List<Interactable>(64);

        public static IReadOnlyList<Interactable> All => _all;

        public static void Register(Interactable i)
        {
            if (i != null && !_all.Contains(i)) _all.Add(i);
        }

        public static void Unregister(Interactable i)
        {
            _all.Remove(i);
        }

        public static void Clear() => _all.Clear();

        /// <summary>范围内距离最近且当前可用的交互对象。</summary>
        public static Interactable FindNearest(PlayerController player, Vector2 origin, float range)
        {
            Interactable best = null;
            float bestSqr = range * range;

            for (int i = 0; i < _all.Count; i++)
            {
                var candidate = _all[i];
                if (candidate == null || !candidate.isActiveAndEnabled) continue;
                if (!candidate.IsAvailable(player)) continue;

                Vector2 anchor = candidate.InteractAnchor;
                float dx = anchor.x - origin.x;
                float dy = anchor.y - origin.y;
                float sqr = dx * dx + dy * dy;

                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
