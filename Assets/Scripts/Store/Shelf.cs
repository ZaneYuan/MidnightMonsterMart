using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;
using MonsterMart.Player;

namespace MonsterMart.Store
{
    /// <summary>
    /// 货架 / 冰柜 / 工具架 — 设计文档 §3.3。
    /// 每个货架绑定一种商品（文档「货架拥有：商品类型」）。
    /// </summary>
    public class Shelf : Interactable
    {
        public ProductData product;
        public int count;
        public int capacity = GameConfig.ShelfCapacity;
        public FixtureKind kind = FixtureKind.Shelf;
        public CellRect cells;
        public string displayName;

        /// <summary>被狼人撞倒 — 设计文档 §7 事件二。</summary>
        public bool knockedOver;

        SpriteRenderer _body;
        Transform _iconRoot;
        readonly List<SpriteRenderer> _icons = new List<SpriteRenderer>();
        SpriteRenderer _emptyBadge;

        public bool IsEmpty => count <= 0;
        public bool IsFull => count >= capacity;
        public bool Usable => !knockedOver && !IsEmpty;

        public override Vector2 InteractAnchor => cells.CenterWorld;

        public void Configure(ProductData assignedProduct, FixtureKind fixtureKind, CellRect rect, string label)
        {
            product = assignedProduct;
            kind = fixtureKind;
            cells = rect;
            displayName = label;
            count = 0;
            knockedOver = false;
            BuildVisuals();
            Refresh();
        }

        void BuildVisuals()
        {
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            _body = bodyGo.AddComponent<SpriteRenderer>();
            _body.sprite = SpriteFactory.Panel(BaseColor(), SpriteFactory.Darken(BaseColor(), 0.28f),
                                               cells.WidthCells, cells.HeightCells);
            _body.sortingOrder = SortingLayers.Fixture;

            var iconRootGo = new GameObject("Icons");
            iconRootGo.transform.SetParent(transform, false);
            _iconRoot = iconRootGo.transform;

            var badgeGo = new GameObject("EmptyBadge");
            badgeGo.transform.SetParent(transform, false);
            _emptyBadge = badgeGo.AddComponent<SpriteRenderer>();
            _emptyBadge.sprite = SpriteFactory.Circle(new Color(0.9f, 0.2f, 0.2f, 0.9f), 20);
            _emptyBadge.sortingOrder = SortingLayers.FixtureOverlay;
            _emptyBadge.transform.localPosition = new Vector3(0f, cells.HeightCells * 0.5f + 0.35f, 0f);
            _emptyBadge.enabled = false;
        }

        Color BaseColor()
        {
            switch (kind)
            {
                case FixtureKind.Cooler: return new Color(0.24f, 0.40f, 0.52f);
                case FixtureKind.ToolRack: return new Color(0.40f, 0.34f, 0.26f);
                default: return new Color(0.34f, 0.29f, 0.38f);
            }
        }

        /// <summary>重建货架上的商品小图标。</summary>
        public void Refresh()
        {
            if (_body != null)
            {
                _body.transform.localRotation = knockedOver
                    ? Quaternion.Euler(0f, 0f, 18f)
                    : Quaternion.identity;
                _body.color = knockedOver ? new Color(0.7f, 0.6f, 0.6f) : Color.white;
            }

            if (_emptyBadge != null)
                _emptyBadge.enabled = IsEmpty && !knockedOver;

            if (_iconRoot == null || product == null) return;

            EnsureIconPool();

            float usableWidth = cells.WidthCells - 0.35f;
            int perRow = Mathf.Max(1, Mathf.Min(capacity, Mathf.FloorToInt(usableWidth / 0.42f)));
            float step = usableWidth / perRow;

            for (int i = 0; i < _icons.Count; i++)
            {
                bool visible = i < count && !knockedOver;
                _icons[i].enabled = visible;
                if (!visible) continue;

                int row = i / perRow;
                int col = i % perRow;
                float x = -usableWidth * 0.5f + step * (col + 0.5f);
                float y = cells.HeightCells * 0.5f - 0.28f - row * 0.34f;
                _icons[i].transform.localPosition = new Vector3(x, y, 0f);
            }
        }

        void EnsureIconPool()
        {
            while (_icons.Count < capacity)
            {
                var go = new GameObject("Item" + _icons.Count);
                go.transform.SetParent(_iconRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.ProductIcon(product);
                sr.sortingOrder = SortingLayers.FixtureOverlay;
                sr.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                sr.enabled = false;
                _icons.Add(sr);
            }
        }

        // ------------------------------------------------------------------
        // 库存操作
        // ------------------------------------------------------------------

        /// <summary>补货，返回实际放上去的数量。</summary>
        public int AddStock(int amount)
        {
            int accepted = Mathf.Min(amount, capacity - count);
            if (accepted <= 0) return 0;
            count += accepted;
            Refresh();
            return accepted;
        }

        /// <summary>顾客取走一件。</summary>
        public bool TakeOne()
        {
            if (!Usable) return false;
            count--;
            Refresh();
            return true;
        }

        /// <summary>被撞倒：商品散落到地上（原型直接损耗掉一半）。</summary>
        public int KnockOver()
        {
            if (knockedOver) return 0;
            knockedOver = true;
            int spilled = count / 2;
            count -= spilled;
            Refresh();
            return spilled;
        }

        public void Lift()
        {
            knockedOver = false;
            Refresh();
        }

        // ------------------------------------------------------------------
        // 交互
        // ------------------------------------------------------------------
        /// <summary>
        /// 有幽灵在等这件商品时，玩家可以直接从货架上取下来送去灵界包装台
        /// （否则玩家只能绕去仓库拿，货架库存也不会被消耗）。
        /// </summary>
        bool CanPickForGhost(PlayerController player)
        {
            if (player == null || !Usable) return false;
            if (!player.Carry.IsEmpty) return false;
            return Game.Store != null && Game.Store.AnyGhostWaitingFor(product);
        }

        public override bool IsAvailable(PlayerController player)
        {
            if (knockedOver) return true;
            if (player == null) return false;
            if (CanPickForGhost(player)) return true;
            if (IsFull) return false;
            return player.Carry.Has(product);
        }

        public override string GetPrompt(PlayerController player)
        {
            if (knockedOver) return "[E] 扶起货架";
            if (CanPickForGhost(player)) return $"[E] 取下 {product.displayName}（幽灵在等）";

            int amount = Mathf.Min(player.Carry.Count, capacity - count);
            return $"[E] 补货 · {product.displayName} ×{amount}（{count}/{capacity}）";
        }

        public override InteractionKind Kind => InteractionKind.Hold;

        public override float HoldSeconds(PlayerController player)
        {
            if (knockedOver) return GameConfig.LiftShelfSeconds;
            if (CanPickForGhost(player)) return 0.35f;

            int amount = Mathf.Min(player.Carry.Count, capacity - count);
            return Mathf.Max(0.2f, amount * GameConfig.RestockSecondsPerItem);
        }

        public override void OnInteract(PlayerController player)
        {
            if (knockedOver)
            {
                Lift();
                Game.Audio?.PlayRestock();
                return;
            }

            if (CanPickForGhost(player))
            {
                if (TakeOne())
                {
                    player.Carry.Take(product, 1);
                    Game.Audio?.PlayPickup();
                    Game.UI?.Hud?.Flash($"拿起 {product.displayName}，送去灵界包装台");
                }
                return;
            }

            int amount = Mathf.Min(player.Carry.Count, capacity - count);
            int placed = AddStock(amount);
            player.Carry.Remove(placed);
            Game.Audio?.PlayRestock();
        }
    }
}
