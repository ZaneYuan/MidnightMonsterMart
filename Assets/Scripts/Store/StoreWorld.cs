using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Customers;
using MonsterMart.Data;

namespace MonsterMart.Store
{
    /// <summary>
    /// 便利店的运行时世界：网格、寻路器、所有设施、污渍、仓库库存。
    /// 场景完全由 Build() 在运行时生成 —— 工程里没有需要手动拖拽的预制体。
    /// 布局对应设计文档 §9.1。
    /// </summary>
    public class StoreWorld : MonoBehaviour
    {
        public StoreGrid Grid { get; private set; }
        public Pathfinder Pathfinder { get; private set; }

        public readonly List<Shelf> Shelves = new List<Shelf>();
        public readonly List<Stain> Stains = new List<Stain>();

        public Checkout Checkout { get; private set; }
        public StockRoom StockRoom { get; private set; }
        public SpiritPackingStation SpiritStation { get; private set; }
        public Mirror Mirror { get; private set; }
        public TrashBin TrashBin { get; private set; }

        /// <summary>顾客进出店的格子（门口）。</summary>
        public Vector2Int DoorCell { get; private set; }

        /// <summary>玩家出生点。</summary>
        public Vector2Int PlayerStartCell { get; private set; }

        /// <summary>仓库库存：营业前进货买到的商品都放在这里。</summary>
        public readonly Dictionary<ProductData, int> Warehouse = new Dictionary<ProductData, int>();

        Transform _floorRoot;
        Transform _fixtureRoot;
        Transform _stainRoot;
        int _stainSeed;

        // ------------------------------------------------------------------
        // 构建
        // ------------------------------------------------------------------
        public void Build()
        {
            Grid = new StoreGrid(GameConfig.GridWidth, GameConfig.GridHeight);
            Pathfinder = new Pathfinder(Grid);

            BuildWalls();
            BuildFloorVisual();
            BuildFixtures();

            foreach (var product in GameDatabase.Products)
                if (!Warehouse.ContainsKey(product)) Warehouse[product] = 0;
        }

        void BuildWalls()
        {
            // 四周 2 格厚的墙，留出 20×12 的可行走区域（设计文档 §9.2）
            for (int x = 0; x < Grid.Width; x++)
            {
                for (int y = 0; y < Grid.Height; y++)
                {
                    bool wall =
                        x < GameConfig.InteriorMinX || x > GameConfig.InteriorMaxX ||
                        y < GameConfig.InteriorMinY || y > GameConfig.InteriorMaxY;
                    Grid.SetBlocked(x, y, wall);
                }
            }

            // 在下墙开一个门洞，顾客从这里进出
            DoorCell = new Vector2Int(11, 0);
            for (int x = 11; x <= 12; x++)
                for (int y = 0; y < GameConfig.InteriorMinY; y++)
                    Grid.SetBlocked(x, y, false);

            PlayerStartCell = new Vector2Int(6, 6);
        }

        void BuildFloorVisual()
        {
            var floorGo = new GameObject("Floor");
            floorGo.transform.SetParent(transform, false);
            _floorRoot = floorGo.transform;

            var tileA = new Color(0.16f, 0.15f, 0.22f);
            var tileB = new Color(0.19f, 0.18f, 0.26f);
            var grout = new Color(0.12f, 0.11f, 0.17f);
            var wallColor = new Color(0.10f, 0.09f, 0.15f);
            var wallEdge = new Color(0.30f, 0.24f, 0.42f);

            for (int x = 0; x < Grid.Width; x++)
            {
                for (int y = 0; y < Grid.Height; y++)
                {
                    bool walkable = Grid.IsWalkable(x, y);
                    var go = new GameObject($"T{x}_{y}");
                    go.transform.SetParent(_floorRoot, false);
                    go.transform.position = StoreGrid.CellToWorld(x, y);

                    var sr = go.AddComponent<SpriteRenderer>();
                    if (walkable)
                    {
                        sr.sprite = SpriteFactory.FloorTile((x + y) % 2 == 0 ? tileA : tileB, grout);
                        sr.sortingOrder = SortingLayers.Floor;
                    }
                    else
                    {
                        bool border = IsWallEdge(x, y);
                        sr.sprite = SpriteFactory.FloorTile(border ? wallEdge : wallColor, grout);
                        sr.sortingOrder = SortingLayers.Wall;
                    }
                }
            }
        }

        bool IsWallEdge(int x, int y)
        {
            // 紧贴可行走区域的那一圈墙，画得亮一点，视觉上像踢脚线
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (Grid.IsWalkable(x + dx, y + dy)) return true;
            return false;
        }

        void BuildFixtures()
        {
            var root = new GameObject("Fixtures");
            root.transform.SetParent(transform, false);
            _fixtureRoot = root.transform;

            var stainRootGo = new GameObject("Stains");
            stainRootGo.transform.SetParent(transform, false);
            _stainRoot = stainRootGo.transform;

            // ---- 货架（设计文档 §9.1）----
            // 左侧普通货架
            AddShelf("black_garlic_bread", FixtureKind.Shelf, new CellRect(4, 7, 6, 7), "左侧货架 · 食品");
            AddShelf("silver_chocolate", FixtureKind.Shelf, new CellRect(4, 10, 6, 10), "左侧货架 · 零食");
            // 中间零食货架
            AddShelf("soul_mint", FixtureKind.Shelf, new CellRect(10, 7, 12, 7), "中间货架 · 零食");
            AddShelf("glow_jelly", FixtureKind.Shelf, new CellRect(10, 10, 12, 10), "中间货架 · 零食");
            // 右侧饮料冰柜（两个格位）
            AddShelf("blood_orange_soda", FixtureKind.Cooler, new CellRect(19, 6, 20, 7), "饮料冰柜 · 上层");
            AddShelf("moonlight_milk", FixtureKind.Cooler, new CellRect(19, 10, 20, 11), "饮料冰柜 · 下层");
            // 右后方工具架（销售）+ 清洁用品架（玩家自用，不是卖的）
            AddShelf("warding_salt", FixtureKind.ToolRack, new CellRect(15, 13, 16, 13), "工具架 · 驱灵盐");
            AddShelf("all_purpose_cleaner", FixtureKind.ToolRack, new CellRect(17, 13, 18, 13),
                     "清洁用品架", supplyRack: true);

            // ---- 收银台 + 3 个排队点（§9.3）----
            var checkoutRect = new CellRect(5, 4, 7, 4);
            var queuePoints = new List<Vector2Int>
            {
                new Vector2Int(6, 3),
                new Vector2Int(7, 3),
                new Vector2Int(8, 3),
            };
            Checkout = CreateFixture<Checkout>("Checkout", checkoutRect);
            Checkout.Configure(checkoutRect, queuePoints);
            BlockRect(checkoutRect);

            // ---- 后方仓库门 ----
            var stockRect = new CellRect(10, 13, 11, 13);
            StockRoom = CreateFixture<StockRoom>("StockRoom", stockRect);
            StockRoom.Configure(stockRect);
            BlockRect(stockRect);

            // ---- 左后方灵界包装台 ----
            var spiritRect = new CellRect(3, 13, 4, 13);
            SpiritStation = CreateFixture<SpiritPackingStation>("SpiritPackingStation", spiritRect);
            SpiritStation.Configure(spiritRect);
            BlockRect(spiritRect);

            // ---- 墙面镜子（不阻挡通行，只是装饰）----
            var mirrorRect = new CellRect(21, 5, 21, 6);
            Mirror = CreateFixture<Mirror>("Mirror", mirrorRect);
            Mirror.Configure(mirrorRect);

            // ---- 垃圾桶 ----
            var trashRect = new CellRect(2, 2, 2, 2);
            TrashBin = CreateFixture<TrashBin>("TrashBin", trashRect);
            TrashBin.Configure(trashRect);
            BlockRect(trashRect);
        }

        void AddShelf(string productId, FixtureKind kind, CellRect rect, string label,
                      bool supplyRack = false)
        {
            var product = GameDatabase.GetProduct(productId);
            if (product == null)
            {
                Debug.LogError($"[StoreWorld] 找不到商品 {productId}");
                return;
            }

            var shelf = CreateFixture<Shelf>("Shelf_" + productId, rect);
            shelf.Configure(product, kind, rect, label, supplyRack);
            BlockRect(rect);
            Shelves.Add(shelf);
        }

        T CreateFixture<T>(string name, CellRect rect) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(_fixtureRoot, false);
            go.transform.position = rect.CenterWorld;
            return go.AddComponent<T>();
        }

        void BlockRect(CellRect rect) => Grid.SetBlockedRect(rect, true, true);

        // ------------------------------------------------------------------
        // 查询
        // ------------------------------------------------------------------
        public Shelf FindShelf(ProductData product)
        {
            for (int i = 0; i < Shelves.Count; i++)
                if (Shelves[i].product == product) return Shelves[i];
            return null;
        }

        /// <summary>某商品在货架上还有货吗。</summary>
        public bool IsOnShelf(ProductData product)
        {
            var shelf = FindShelf(product);
            return shelf != null && shelf.Usable;
        }

        /// <summary>只统计真正对外销售的货架（清洁用品架是玩家自用的，不算）。</summary>
        public int SalesShelfCount()
        {
            int n = 0;
            for (int i = 0; i < Shelves.Count; i++)
                if (!Shelves[i].isSupplyRack) n++;
            return n;
        }

        /// <summary>店里有多少个空的销售货架 —— 检查员会看这个。</summary>
        public int EmptyShelfCount()
        {
            int n = 0;
            for (int i = 0; i < Shelves.Count; i++)
                if (!Shelves[i].isSupplyRack && Shelves[i].IsEmpty) n++;
            return n;
        }

        /// <summary>摆在销售货架上的禁忌商品数量 —— 检查员会看这个。</summary>
        public int StockedTabooCount()
        {
            int n = 0;
            for (int i = 0; i < Shelves.Count; i++)
            {
                var shelf = Shelves[i];
                if (shelf.isSupplyRack) continue;
                if (shelf.product != null && shelf.product.isTaboo && !shelf.IsEmpty) n++;
            }
            return n;
        }

        /// <summary>随便找一个可行走的格子（用于污渍、游荡）。</summary>
        public Vector2Int RandomWalkableCell()
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                int x = Random.Range(GameConfig.InteriorMinX, GameConfig.InteriorMaxX + 1);
                int y = Random.Range(GameConfig.InteriorMinY, GameConfig.InteriorMaxY + 1);
                if (Grid.IsWalkable(x, y)) return new Vector2Int(x, y);
            }
            return PlayerStartCell;
        }

        /// <summary>设施旁边、离参考点最近的可站立格。</summary>
        public Vector2Int AccessCellNear(CellRect rect, Vector2 from)
        {
            var options = Grid.AccessCells(rect);
            if (options.Count == 0) return Grid.NearestWalkable(new Vector2Int(rect.xMin, rect.yMin));

            var best = options[0];
            float bestSqr = float.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                Vector2 world = StoreGrid.CellToWorld(options[i]);
                float sqr = (world - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = options[i];
                }
            }
            return best;
        }

        // ------------------------------------------------------------------
        // 污渍
        // ------------------------------------------------------------------
        public Stain AddStain(Vector2Int cell, Color color)
        {
            if (!Grid.IsWalkable(cell)) return null;

            // 同一格不重复叠加
            for (int i = 0; i < Stains.Count; i++)
                if (Stains[i].cell == cell) return Stains[i];

            var go = new GameObject("Stain");
            go.transform.SetParent(_stainRoot, false);
            var stain = go.AddComponent<Stain>();
            stain.Configure(cell, color, _stainSeed++);
            Stains.Add(stain);

            Game.Cleanliness?.Add(-GameConfig.StainCleanlinessCost);
            return stain;
        }

        public void RemoveStain(Stain stain)
        {
            if (stain == null) return;
            Stains.Remove(stain);
            Game.Cleanliness?.Add(GameConfig.StainCleanlinessCost * 0.8f);
            Destroy(stain.gameObject);
        }

        public bool HasStainAt(Vector2Int cell)
        {
            for (int i = 0; i < Stains.Count; i++)
                if (Stains[i].cell == cell) return true;
            return false;
        }

        public void ClearAllStains()
        {
            for (int i = Stains.Count - 1; i >= 0; i--)
                if (Stains[i] != null) Destroy(Stains[i].gameObject);
            Stains.Clear();
        }

        // ------------------------------------------------------------------
        // 仓库
        // ------------------------------------------------------------------
        public int WarehouseCount(ProductData product)
            => product != null && Warehouse.TryGetValue(product, out int n) ? n : 0;

        public void AddToWarehouse(ProductData product, int amount)
        {
            if (product == null || amount <= 0) return;
            Warehouse[product] = WarehouseCount(product) + amount;
        }

        /// <summary>
        /// 一键把仓库里的货铺到对应货架上，返回上架件数。
        /// 只在营业前准备阶段提供 —— 那时本来就没有时间压力，
        /// 手动来回搬运只是重复劳动；营业中的补货压力才是玩法本体。
        /// </summary>
        public int AutoRestockAll()
        {
            int placed = 0;

            for (int i = 0; i < Shelves.Count; i++)
            {
                var shelf = Shelves[i];
                if (shelf == null || shelf.product == null || shelf.knockedOver) continue;

                int room = shelf.capacity - shelf.count;
                if (room <= 0) continue;

                int taken = TakeFromWarehouse(shelf.product, room);
                if (taken <= 0) continue;

                placed += shelf.AddStock(taken);
            }

            return placed;
        }

        public int TakeFromWarehouse(ProductData product, int amount)
        {
            int available = WarehouseCount(product);
            int taken = Mathf.Min(available, amount);
            if (taken > 0) Warehouse[product] = available - taken;
            return taken;
        }

        // ------------------------------------------------------------------
        // 幽灵专用
        // ------------------------------------------------------------------
        /// <summary>有没有幽灵正在等这件商品做灵界处理。</summary>
        public bool AnyGhostWaitingFor(ProductData product)
        {
            if (product == null) return false;
            var customers = CustomerRegistry.All;
            for (int i = 0; i < customers.Count; i++)
            {
                var c = customers[i];
                if (c == null) continue;
                if (c.Data.monsterType != MonsterType.Ghost) continue;
                if (c.NeedsSpiritPacking(product)) return true;
            }
            return false;
        }

        /// <summary>把营业日之间需要重置的东西清掉。</summary>
        public void ResetForNewDay()
        {
            ClearAllStains();
            for (int i = 0; i < Shelves.Count; i++)
                if (Shelves[i].knockedOver) Shelves[i].Lift();
        }
    }
}
