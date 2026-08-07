using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.Expeditions
{
    /// <summary>
    /// 地上的战利品 — 设计文档 §3.4「战斗房：清理敌人后获得普通商品」。
    /// 队长走近即自动拾取，进战利品袋，远征结束后统一入库。
    /// </summary>
    public class LootPickup : MonoBehaviour
    {
        public ProductData Product { get; private set; }
        public int Count { get; private set; }

        Vector2 _position;
        float _spawnTime;

        public Vector2 Position => _position;

        public void Initialize(ProductData product, int count, Vector2 localPosition)
        {
            Product = product;
            Count = Mathf.Max(1, count);
            _position = localPosition;
            _spawnTime = Time.time;

            transform.localPosition = _position;

            var glow = new GameObject("Glow");
            glow.transform.SetParent(transform, false);
            glow.transform.localScale = Vector3.one * 0.8f;
            var glowSr = glow.AddComponent<SpriteRenderer>();
            glowSr.sprite = SpriteFactory.Circle(new Color(1f, 0.92f, 0.55f, 0.45f), 34);
            glowSr.sortingOrder = SortingLayers.Floor + 2;

            var icon = new GameObject("Icon");
            icon.transform.SetParent(transform, false);
            var iconSr = icon.AddComponent<SpriteRenderer>();
            iconSr.sprite = SpriteFactory.ProductIcon(product);
            iconSr.sortingOrder = SortingLayers.CarryItem;
        }

        void Update()
        {
            if (Game.Manager == null || Game.Manager.State != GameState.Expedition) return;

            // 轻微上下浮动，比静止的图标更容易被看见
            float bob = Mathf.Sin((Time.time - _spawnTime) * 3.4f) * 0.08f;
            transform.localPosition = new Vector3(_position.x, _position.y + bob, 0f);
        }
    }
}
