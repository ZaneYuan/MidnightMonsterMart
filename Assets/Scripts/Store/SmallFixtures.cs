using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;
using MonsterMart.Player;

// 这几个设施逻辑都很短，放在同一个文件里便于对照阅读。
// 工程中所有组件都由代码 AddComponent 创建，不依赖「文件名 == 类名」。
namespace MonsterMart.Store
{
    /// <summary>
    /// 史莱姆污渍 — 设计文档 §4.4。
    /// 降低整洁度、拖慢玩家、拉低其他顾客耐心；用万能清洁剂清理。
    /// </summary>
    public class Stain : Interactable
    {
        public Vector2Int cell;
        SpriteRenderer _renderer;

        public void Configure(Vector2Int atCell, Color color, int seed)
        {
            cell = atCell;
            transform.position = StoreGrid.CellToWorld(atCell);

            var go = new GameObject("Decal");
            go.transform.SetParent(transform, false);
            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = SpriteFactory.Stain(SpriteFactory.WithAlpha(color, 0.75f), seed);
            _renderer.sortingOrder = SortingLayers.FloorDecal;
            _renderer.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
        }

        public override bool IsAvailable(PlayerController player)
            => player != null && player.Carry.HasCleaningTool;

        public override string GetPrompt(PlayerController player) => "[E] 清理污渍";

        public override InteractionKind Kind => InteractionKind.Hold;

        public override float HoldSeconds(PlayerController player) => GameConfig.CleanStainSeconds;

        public override void OnInteract(PlayerController player)
        {
            // 清理消耗一份清洁剂
            player.Carry.Remove(1);
            Game.Audio?.PlayClean();
            Game.Store.RemoveStain(this);
        }
    }

    /// <summary>
    /// 装饰镜 — 设计文档 §4.1「吸血鬼靠近镜子时会持续降低耐心」。
    /// 营业前可以：保留 / 遮住 / 移走，形成一个明确的取舍。
    /// </summary>
    public class Mirror : Interactable
    {
        public enum MirrorState { Exposed, Covered, Removed }

        public MirrorState State { get; private set; } = MirrorState.Exposed;
        public RectInt cells;

        SpriteRenderer _renderer;

        /// <summary>只有裸露状态才会惹恼吸血鬼。</summary>
        public bool AnnoysVampires => State == MirrorState.Exposed;

        /// <summary>保留镜子时，普通顾客获得满意度加成。</summary>
        public bool GivesDecorBonus => State == MirrorState.Exposed;

        public void Configure(RectInt rect)
        {
            cells = rect;
            transform.position = rect.CenterWorld;

            var go = new GameObject("Glass");
            go.transform.SetParent(transform, false);
            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = SortingLayers.Fixture;
            ApplyVisual();
        }

        public override Vector2 InteractAnchor => cells.CenterWorld;

        void ApplyVisual()
        {
            switch (State)
            {
                case MirrorState.Exposed:
                    _renderer.enabled = true;
                    _renderer.sprite = SpriteFactory.Panel(
                        new Color(0.72f, 0.84f, 0.92f), new Color(0.85f, 0.75f, 0.45f),
                        cells.WidthCells, cells.HeightCells);
                    break;
                case MirrorState.Covered:
                    _renderer.enabled = true;
                    _renderer.sprite = SpriteFactory.Panel(
                        new Color(0.28f, 0.24f, 0.30f), new Color(0.18f, 0.15f, 0.20f),
                        cells.WidthCells, cells.HeightCells);
                    break;
                default:
                    _renderer.enabled = false;
                    break;
            }
        }

        /// <summary>只在营业前可调整（文档：「玩家可以在营业前」）。</summary>
        public override bool IsAvailable(PlayerController player)
            => Game.Manager != null && Game.Manager.State == GameState.Preparation;

        public override string GetPrompt(PlayerController player)
        {
            switch (State)
            {
                case MirrorState.Exposed: return "[E] 用布遮住镜子（吸血鬼友好，失去装饰加成）";
                case MirrorState.Covered: return "[E] 把镜子搬走";
                default: return "[E] 把镜子搬回来（普通顾客 +满意度）";
            }
        }

        public override void OnInteract(PlayerController player)
        {
            State = State == MirrorState.Exposed ? MirrorState.Covered
                  : State == MirrorState.Covered ? MirrorState.Removed
                  : MirrorState.Exposed;
            ApplyVisual();
            Game.Audio?.PlayUiClick();
            Game.UI?.Hud?.Flash(State == MirrorState.Exposed ? "镜子已复原"
                              : State == MirrorState.Covered ? "镜子已遮住"
                              : "镜子已移走");
        }
    }

    /// <summary>垃圾桶 — 丢弃手上拿错的商品（按进货价的一半折损）。</summary>
    public class TrashBin : Interactable
    {
        public RectInt cells;

        public void Configure(RectInt rect)
        {
            cells = rect;
            transform.position = rect.CenterWorld;

            var go = new GameObject("Bin");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Panel(new Color(0.30f, 0.36f, 0.30f),
                                            new Color(0.18f, 0.22f, 0.18f),
                                            rect.WidthCells, rect.HeightCells);
            sr.sortingOrder = SortingLayers.Fixture;
        }

        public override Vector2 InteractAnchor => cells.CenterWorld;

        public override bool IsAvailable(PlayerController player)
            => player != null && !player.Carry.IsEmpty;

        public override string GetPrompt(PlayerController player)
            => $"[E] 丢弃 {player.Carry.Product.displayName} ×{player.Carry.Count}";

        public override void OnInteract(PlayerController player)
        {
            int loss = player.Carry.Count * player.Carry.Product.purchasePrice / 2;
            Game.Economy?.RecordSpoilage(loss);
            player.Carry.Clear();
            Game.UI?.Hud?.Flash($"丢弃商品，损耗 {loss} 金币");
        }
    }

    /// <summary>
    /// 灵界包装台 — 设计文档 §4.3。
    /// 幽灵拿不到实体商品，玩家必须替它取货 → 放到这里处理 → 再交给它。
    /// </summary>
    public class SpiritPackingStation : Interactable
    {
        public RectInt cells;

        /// <summary>已处理完、等待交付的商品。</summary>
        public ProductData PackedProduct { get; private set; }

        SpriteRenderer _glow;

        public void Configure(RectInt rect)
        {
            cells = rect;
            transform.position = rect.CenterWorld;

            var body = new GameObject("Table");
            body.transform.SetParent(transform, false);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Panel(new Color(0.30f, 0.26f, 0.46f),
                                            new Color(0.55f, 0.45f, 0.85f),
                                            rect.WidthCells, rect.HeightCells);
            sr.sortingOrder = SortingLayers.Fixture;

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(transform, false);
            _glow = glowGo.AddComponent<SpriteRenderer>();
            _glow.sprite = SpriteFactory.Circle(new Color(0.65f, 0.55f, 1f, 0.55f), 28);
            _glow.sortingOrder = SortingLayers.FixtureOverlay;
            _glow.enabled = false;
        }

        public override Vector2 InteractAnchor => cells.CenterWorld;

        public bool HasPacked => PackedProduct != null;

        public ProductData TakePacked()
        {
            var p = PackedProduct;
            PackedProduct = null;
            if (_glow != null) _glow.enabled = false;
            return p;
        }

        public override bool IsAvailable(PlayerController player)
        {
            if (player == null) return false;

            // 台面上已经有处理好的商品 → 可以拿走
            if (HasPacked) return player.Carry.IsEmpty || player.Carry.Product == PackedProduct;

            if (player.Carry.IsEmpty) return false;
            if (player.Carry.Packed) return false;      // 已经处理过了，别重复放
            return Game.Store != null && Game.Store.AnyGhostWaitingFor(player.Carry.Product);
        }

        public override string GetPrompt(PlayerController player)
        {
            if (HasPacked) return $"[E] 取走已处理的 {PackedProduct.displayName}";
            return $"[E] 灵界处理 · {player.Carry.Product.displayName}";
        }

        public override InteractionKind Kind =>
            HasPacked ? InteractionKind.Instant : InteractionKind.Hold;

        public override float HoldSeconds(PlayerController player) => 1.2f;

        public override void OnInteract(PlayerController player)
        {
            if (HasPacked)
            {
                var packed = TakePacked();
                player.Carry.Clear();
                player.Carry.Take(packed, 1, true);
                Game.Audio?.PlayPickup();
                Game.UI?.Hud?.Flash($"拿起已处理的 {packed.displayName}，去交给幽灵");
                return;
            }

            PackedProduct = player.Carry.Product;
            player.Carry.Remove(1);
            if (_glow != null) _glow.enabled = true;
            Game.Audio?.PlaySpirit();
            Game.UI?.Hud?.Flash($"{PackedProduct.displayName} 已完成灵界处理");
        }
    }
}
