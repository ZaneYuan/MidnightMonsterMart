using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.Expeditions
{
    /// <summary>
    /// 采集点 — 设计文档 §3.2「交互键：采集、开箱、救援」与
    /// §3.4 资源房「低压力采集，强调路线和携带容量」。
    ///
    /// 走近按 E 一次收完；背包装不下时只收得下的那部分，
    /// 剩下的留在原地 —— 容量压力就是资源房的玩法本体。
    /// </summary>
    public class HarvestNode : MonoBehaviour
    {
        public ProductData Product { get; private set; }
        public int Remaining { get; private set; }

        public bool IsEmpty => Remaining <= 0;

        /// <summary>队长要站多近才能采。</summary>
        public const float HarvestRadius = 1.2f;

        Vector2 _position;
        SpriteRenderer _glow;
        SpriteRenderer _icon;

        public Vector2 Position => _position;

        public void Initialize(ProductData product, int amount, Vector2 localPosition)
        {
            Product = product;
            Remaining = Mathf.Max(0, amount);
            _position = localPosition;

            transform.localPosition = _position;

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(transform, false);
            glowGo.transform.localScale = Vector3.one * 1.15f;
            _glow = glowGo.AddComponent<SpriteRenderer>();
            _glow.sprite = SpriteFactory.Circle(new Color(0.55f, 0.95f, 0.60f, 0.40f), 40);
            _glow.sortingOrder = SortingLayers.Floor + 2;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(transform, false);
            _icon = iconGo.AddComponent<SpriteRenderer>();
            _icon.sprite = SpriteFactory.ProductIcon(product);
            _icon.sortingOrder = SortingLayers.CarryItem;
        }

        /// <summary>
        /// 采集。<paramref name="maxAmount"/> 是背包还装得下的数量。
        /// 返回实际采到多少。
        /// </summary>
        public int Harvest(int maxAmount)
        {
            if (IsEmpty || maxAmount <= 0) return 0;

            int taken = Mathf.Min(Remaining, maxAmount);
            Remaining -= taken;
            RefreshVisual();
            return taken;
        }

        void RefreshVisual()
        {
            if (_glow != null)
                _glow.color = IsEmpty
                    ? new Color(0.45f, 0.48f, 0.45f, 0.18f)
                    : new Color(0.55f, 0.95f, 0.60f, 0.40f);

            if (_icon != null) _icon.enabled = !IsEmpty;
        }

        public bool InRange(Vector2 from) => (from - _position).magnitude <= HarvestRadius;

        void Update()
        {
            if (Game.Manager == null || Game.Manager.State != GameState.Expedition) return;
            if (IsEmpty || _icon == null) return;

            float bob = Mathf.Sin(Time.time * 2.6f) * 0.06f;
            _icon.transform.localPosition = new Vector3(0f, bob, 0f);
        }
    }
}
