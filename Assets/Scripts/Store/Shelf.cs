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
    public class Shelf : FixtureInteractable
    {
        public ProductData product;
        public int count;
        public int capacity = GameConfig.ShelfCapacity;
        public FixtureKind kind = FixtureKind.Shelf;
        public string displayName;

        /// <summary>
        /// 清洁用品架：放的是玩家自己用的道具（万能清洁剂），不是卖给顾客的商品。
        /// 不计入「空货架」统计，检查员也不会因为它扣分；玩家可以直接从上面取用。
        /// </summary>
        public bool isSupplyRack;

        /// <summary>被狼人撞倒 — 设计文档 §7 事件二。</summary>
        public bool knockedOver;

        SpriteRenderer _body;
        Transform _iconRoot;
        readonly List<SpriteRenderer> _icons = new List<SpriteRenderer>();
        SpriteRenderer _emptyBadge;
        SpriteRenderer _emptyBadgeIcon;
        SpriteRenderer _highlight;
        Transform _badgeRoot;

        public bool IsEmpty => count <= 0;
        public bool IsFull => count >= capacity;
        public bool Usable => !knockedOver && !IsEmpty;

        public void Configure(ProductData assignedProduct, FixtureKind fixtureKind, CellRect rect,
                              string label, bool supplyRack = false)
        {
            product = assignedProduct;
            kind = fixtureKind;
            cells = rect;
            displayName = label;
            isSupplyRack = supplyRack;
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

            // 手上拿着这个货架要的商品时，货架会闪黄框指路
            var highlightGo = new GameObject("Highlight");
            highlightGo.transform.SetParent(transform, false);
            _highlight = highlightGo.AddComponent<SpriteRenderer>();
            _highlight.sprite = SpriteFactory.Panel(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.95f, 0.6f),
                                                    cells.WidthCells, cells.HeightCells, 3);
            _highlight.sortingOrder = SortingLayers.Fixture - 1;
            _highlight.transform.localScale = new Vector3(1.12f, 1.22f, 1f);
            _highlight.enabled = false;

            // 空货架提示：贴着货架顶部，并显示「缺哪件商品」而不是一个光秃秃的红点
            var badgeGo = new GameObject("EmptyBadge");
            badgeGo.transform.SetParent(transform, false);
            badgeGo.transform.localPosition = new Vector3(0f, cells.HeightCells * 0.5f + 0.18f, 0f);
            _badgeRoot = badgeGo.transform;

            _emptyBadge = badgeGo.AddComponent<SpriteRenderer>();
            // 清洁用品架空了不是事故，用蓝色提示而不是刺眼的红色
            _emptyBadge.sprite = SpriteFactory.Circle(
                isSupplyRack ? new Color(0.24f, 0.52f, 0.82f, 0.90f)
                             : new Color(0.86f, 0.20f, 0.22f, 0.92f), 30);
            _emptyBadge.sortingOrder = SortingLayers.FixtureOverlay;
            _emptyBadge.enabled = false;

            var badgeIconGo = new GameObject("WantIcon");
            badgeIconGo.transform.SetParent(badgeGo.transform, false);
            badgeIconGo.transform.localScale = Vector3.one * 0.60f;
            _emptyBadgeIcon = badgeIconGo.AddComponent<SpriteRenderer>();
            _emptyBadgeIcon.sprite = SpriteFactory.ProductIcon(product);
            _emptyBadgeIcon.sortingOrder = SortingLayers.FixtureOverlay + 1;
            _emptyBadgeIcon.enabled = false;
        }

        void Update()
        {
            var player = Game.Player;

            bool wanted =
                player != null &&
                !knockedOver &&
                !IsFull &&
                player.Carry.Has(product);

            if (_highlight != null)
            {
                _highlight.enabled = wanted;
                if (wanted)
                {
                    float pulse = 0.45f + 0.35f * Mathf.Sin(Time.time * 6f);
                    _highlight.color = new Color(1f, 1f, 1f, pulse);
                }
            }

            // 空货架红标轻微上下浮动，比静止的圆点更容易被注意到
            if (_badgeRoot != null && _emptyBadge != null && _emptyBadge.enabled)
            {
                float bob = Mathf.Sin(Time.time * 3.2f) * 0.06f;
                _badgeRoot.localPosition =
                    new Vector3(0f, cells.HeightCells * 0.5f + 0.18f + bob, 0f);
            }
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

            bool showBadge = IsEmpty && !knockedOver;
            if (_emptyBadge != null) _emptyBadge.enabled = showBadge;
            if (_emptyBadgeIcon != null) _emptyBadgeIcon.enabled = showBadge;

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

        /// <summary>清洁用品架：空手走过去就能取用，省得跑一趟仓库。</summary>
        bool CanTakeSupply(PlayerController player)
            => isSupplyRack && player != null && player.Carry.IsEmpty && count > 0 && !knockedOver;

        public override bool IsAvailable(PlayerController player)
        {
            if (knockedOver) return true;
            if (player == null) return false;
            if (CanTakeSupply(player)) return true;
            if (CanPickForGhost(player)) return true;

            // 货架满了但玩家手上正好拿着这个商品时也要算「可交互」——按 E 给个
            // 明确提示，而不是安静地毫无反应（之前被当成「卡 bug 了」反馈过：
            // 货架满了没法卸货，人也没法腾出手接下一件）。
            return player.Carry.Has(product);
        }

        public override string GetPrompt(PlayerController player)
        {
            if (knockedOver) return "[E] 扶起货架";
            if (CanTakeSupply(player))
                return $"[E] 取用 {product.displayName}（架上 {count}）";
            if (CanPickForGhost(player)) return $"[E] 取下 {product.displayName}（幽灵在等）";
            if (IsFull) return $"这个货架满了（{count}/{capacity}）—— 去别的货架，或者回仓库放回";

            int amount = Mathf.Min(player.Carry.Count, capacity - count);
            string verb = isSupplyRack ? "补充清洁用品" : "补货";
            return $"[E] {verb} · {product.displayName} ×{amount}（{count}/{capacity}）";
        }

        public override InteractionKind Kind => InteractionKind.Hold;

        public override float HoldSeconds(PlayerController player)
        {
            if (knockedOver) return GameConfig.LiftShelfSeconds;
            if (CanTakeSupply(player)) return 0.3f;
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

            if (CanTakeSupply(player))
            {
                int taken = Mathf.Min(count, GameConfig.PlayerCarryCapacity);
                count -= taken;
                player.Carry.Take(product, taken);
                Refresh();
                Game.Audio?.PlayPickup();
                Game.UI?.Hud?.Flash($"取用 {product.displayName} ×{taken}");
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

            if (IsFull)
            {
                Game.UI?.Hud?.Flash($"「{displayName}」已经满了，去别的货架，或者回仓库放回");
                Game.Audio?.PlayError();
                return;
            }

            int amount = Mathf.Min(player.Carry.Count, capacity - count);
            int placed = AddStock(amount);
            player.Carry.Remove(placed);
            Game.Audio?.PlayRestock();
        }
    }
}
