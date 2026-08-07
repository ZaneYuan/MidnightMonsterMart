using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.Expeditions
{
    /// <summary>
    /// 远征地图的灰盒房间 — 设计文档 §3.4「地图房间结构」。
    /// 六类房间共用这一张矩形地形，差异来自 RoomData（敌人、采集点、事件、Boss 机制）。
    ///
    /// 复用便利店那套 StoreGrid：它本身没有任何店铺逻辑，只是「哪格能走」的表，
    /// 碰撞同样不依赖 Collider2D。
    ///
    /// 整个房间挂在一个带偏移的根节点下，坐标仍按格子来算，
    /// 这样便利店可以原地不动地留在世界原点，两边不会打架。
    /// </summary>
    public class ExpeditionWorld : MonoBehaviour
    {
        /// <summary>房间在世界坐标里的落点，避开便利店所在的区域。</summary>
        public static readonly Vector2 WorldOffset = new Vector2(0f, 64f);

        public const int RoomWidth = 24;
        public const int RoomHeight = 16;
        public const int WallThickness = 2;

        public StoreGrid Grid { get; private set; }

        /// <summary>本房间的进入点，也是小队的落脚处。</summary>
        public Vector2Int CampCell { get; private set; }

        /// <summary>通往下一个房间的传送点 — 设计文档 §3.4。</summary>
        public Vector2Int ExitCell { get; private set; }

        /// <summary>房间正中 —— Boss 站这里，孢子喷口围着它摆一圈。</summary>
        public Vector2Int CenterCell => new Vector2Int(RoomWidth / 2, RoomHeight / 2);

        public RoomData Room { get; private set; }

        public Vector2 BoundsMin => WorldOffset;
        public Vector2 BoundsMax => WorldOffset + new Vector2(RoomWidth, RoomHeight);

        SpriteRenderer _exitSprite;

        public void Build(RoomData room)
        {
            Room = room;
            transform.localPosition = Vector3.zero;

            Grid = new StoreGrid(RoomWidth, RoomHeight);
            CampCell = new Vector2Int(WallThickness + 2, RoomHeight / 2);
            ExitCell = new Vector2Int(RoomWidth - WallThickness - 2, RoomHeight / 2);

            BuildWalls();
            BuildFloor();
            BuildExit();
        }

        void BuildExit()
        {
            var go = new GameObject("ExitPortal");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = StoreGrid.CellToWorld(ExitCell);

            _exitSprite = go.AddComponent<SpriteRenderer>();
            _exitSprite.sprite = SpriteFactory.Circle(new Color(0.55f, 0.90f, 1f, 0.55f), 44);
            _exitSprite.sortingOrder = SortingLayers.Floor + 2;

            SetExitOpen(false);
        }

        /// <summary>清场之前传送点是暗的，踩上去也没用。</summary>
        public void SetExitOpen(bool open)
        {
            if (_exitSprite == null) return;

            _exitSprite.color = open
                ? new Color(0.55f, 0.90f, 1f, 0.75f)
                : new Color(0.40f, 0.45f, 0.52f, 0.28f);
        }

        void BuildWalls()
        {
            for (int x = 0; x < RoomWidth; x++)
            {
                for (int y = 0; y < RoomHeight; y++)
                {
                    bool wall =
                        x < WallThickness || x >= RoomWidth - WallThickness ||
                        y < WallThickness || y >= RoomHeight - WallThickness;
                    Grid.SetBlocked(x, y, wall);
                }
            }
        }

        void BuildFloor()
        {
            var root = new GameObject("Ground").transform;
            root.SetParent(transform, false);

            // 白天的异世界用明亮奇幻色（§16.1），和夜店的深蓝紫拉开
            var tileA = new Color(0.20f, 0.30f, 0.22f);
            var tileB = new Color(0.24f, 0.35f, 0.26f);
            var grout = new Color(0.14f, 0.22f, 0.17f);
            var wall = new Color(0.10f, 0.16f, 0.13f);
            var wallEdge = new Color(0.28f, 0.42f, 0.30f);

            for (int x = 0; x < RoomWidth; x++)
            {
                for (int y = 0; y < RoomHeight; y++)
                {
                    var go = new GameObject($"T{x}_{y}");
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = StoreGrid.CellToWorld(x, y);

                    var sr = go.AddComponent<SpriteRenderer>();
                    bool walkable = Grid.IsWalkable(x, y);

                    sr.sprite = walkable
                        ? SpriteFactory.FloorTile((x + y) % 2 == 0 ? tileA : tileB, grout)
                        : SpriteFactory.FloorTile(IsWallEdge(x, y) ? wallEdge : wall, grout);
                    sr.sortingOrder = walkable ? SortingLayers.Floor : SortingLayers.Wall;
                }
            }

            // 进入点标记
            var camp = new GameObject("Entrance");
            camp.transform.SetParent(transform, false);
            camp.transform.localPosition = StoreGrid.CellToWorld(CampCell);
            var campSr = camp.AddComponent<SpriteRenderer>();
            campSr.sprite = SpriteFactory.Circle(new Color(0.95f, 0.80f, 0.35f, 0.35f), 40);
            campSr.sortingOrder = SortingLayers.Floor + 1;
        }

        bool IsWallEdge(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (Grid.IsWalkable(x + dx, y + dy)) return true;
            return false;
        }

        /// <summary>格子坐标 → 世界坐标（带上房间偏移）。</summary>
        public Vector2 CellToWorld(Vector2Int cell) => WorldOffset + StoreGrid.CellToWorld(cell);

        /// <summary>随便找一个离营地有点距离的可行走格，用来放敌人。</summary>
        public Vector2Int RandomWalkableCell(int minDistanceFromCamp = 6)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                int x = Random.Range(WallThickness, RoomWidth - WallThickness);
                int y = Random.Range(WallThickness, RoomHeight - WallThickness);
                var c = new Vector2Int(x, y);

                if (!Grid.IsWalkable(c)) continue;
                if ((c - CampCell).sqrMagnitude < minDistanceFromCamp * minDistanceFromCamp) continue;
                // 别把敌人正好压在传送点上，否则清场前玩家看不清出口
                if ((c - ExitCell).sqrMagnitude < 4) continue;
                return c;
            }
            return new Vector2Int(RoomWidth - WallThickness - 1, RoomHeight / 2);
        }
    }
}
