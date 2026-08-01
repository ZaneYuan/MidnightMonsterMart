using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.Customers
{
    /// <summary>
    /// 顾客头顶显示 — 设计文档 §10.1：
    /// 当前需求商品图标、耐心条、情绪表情、特殊状态图标。
    /// 用 SpriteRenderer 实现，不占用 UI Canvas，也不需要字体。
    /// </summary>
    public class CustomerBubble : MonoBehaviour
    {
        SpriteRenderer _panel;
        SpriteRenderer _wantIcon;
        SpriteRenderer _barBack;
        SpriteRenderer _barFill;
        SpriteRenderer _mood;
        SpriteRenderer _statusIcon;

        const float BarWidth = 1.1f;

        public void Build(Transform parent)
        {
            transform.SetParent(parent, false);
            transform.localPosition = new Vector3(0f, 1.55f, 0f);

            _panel = MakeRenderer("Panel", SpriteFactory.Solid(new Color(0.08f, 0.07f, 0.13f, 0.78f)),
                                  SortingLayers.Bubble, new Vector3(1.35f, 0.86f, 1f), Vector3.zero);

            _wantIcon = MakeRenderer("Want", null, SortingLayers.Bubble + 1,
                                     new Vector3(0.62f, 0.62f, 1f), new Vector3(-0.31f, 0.13f, 0f));

            _mood = MakeRenderer("Mood", SpriteFactory.Circle(Color.white, 18), SortingLayers.Bubble + 1,
                                 new Vector3(0.6f, 0.6f, 1f), new Vector3(0.34f, 0.13f, 0f));

            _barBack = MakeRenderer("BarBack", SpriteFactory.Solid(new Color(0.05f, 0.05f, 0.08f, 0.95f)),
                                    SortingLayers.Bubble + 1, new Vector3(BarWidth, 0.16f, 1f),
                                    new Vector3(0f, -0.26f, 0f));

            _barFill = MakeRenderer("BarFill", SpriteFactory.Solid(Color.white),
                                    SortingLayers.Bubble + 2, new Vector3(BarWidth, 0.11f, 1f),
                                    new Vector3(0f, -0.26f, 0f));

            _statusIcon = MakeRenderer("Status", SpriteFactory.Circle(Color.white, 16),
                                       SortingLayers.Bubble + 2, new Vector3(0.5f, 0.5f, 1f),
                                       new Vector3(0.62f, 0.42f, 0f));
            _statusIcon.enabled = false;
        }

        SpriteRenderer MakeRenderer(string name, Sprite sprite, int order, Vector3 scale, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        public void SetWant(ProductData product)
        {
            if (_wantIcon == null) return;

            if (product == null)
            {
                _wantIcon.enabled = false;
                return;
            }

            _wantIcon.enabled = true;
            _wantIcon.sprite = SpriteFactory.ProductIcon(product);
        }

        public void SetPatience(float normalized, PatienceTier tier)
        {
            if (_barFill == null) return;

            float t = Mathf.Clamp01(normalized);
            _barFill.transform.localScale = new Vector3(BarWidth * t, 0.11f, 1f);
            _barFill.transform.localPosition = new Vector3(-BarWidth * 0.5f * (1f - t), -0.26f, 0f);
            _barFill.color = TierColor(tier);

            if (_mood != null) _mood.color = TierColor(tier);
        }

        static Color TierColor(PatienceTier tier)
        {
            switch (tier)
            {
                case PatienceTier.Calm: return new Color(0.45f, 0.85f, 0.45f);
                case PatienceTier.Impatient: return new Color(0.95f, 0.80f, 0.30f);
                case PatienceTier.Complaining: return new Color(0.95f, 0.42f, 0.25f);
                default: return new Color(0.85f, 0.20f, 0.20f);
            }
        }

        /// <summary>特殊状态：等待灵界处理、情绪警告、想问问题等。</summary>
        public void SetStatus(Color color, bool visible)
        {
            if (_statusIcon == null) return;
            _statusIcon.enabled = visible;
            _statusIcon.color = color;
        }

        public void SetVisible(bool visible)
        {
            if (_panel != null) _panel.enabled = visible;
            if (_barBack != null) _barBack.enabled = visible;
            if (_barFill != null) _barFill.enabled = visible;
            if (_mood != null) _mood.enabled = visible;
            if (_wantIcon != null && !visible) _wantIcon.enabled = false;
        }
    }
}
