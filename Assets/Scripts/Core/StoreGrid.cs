using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Data;

namespace MonsterMart.Core
{
    /// <summary>
    /// 便利店的逻辑网格 — 设计文档 §9.2：24×16 格，单格 32×32 像素。
    /// 由于 PixelsPerUnit 也是 32，一格恰好等于一个世界单位，
    /// 因此格 (x,y) 的中心就是世界坐标 (x+0.5, y+0.5)。
    ///
    /// 所有碰撞都走这张表 —— 工程里没有 Collider2D / Rigidbody2D，
    /// 移动和阻挡完全由代码判定，玩家不可能被物理挤进墙里。
    /// </summary>
    public class StoreGrid
    {
        public readonly int Width;
        public readonly int Height;

        readonly bool[,] _blocked;
        readonly bool[,] _blockedByFixture;

        public StoreGrid(int width, int height)
        {
            Width = width;
            Height = height;
            _blocked = new bool[width, height];
            _blockedByFixture = new bool[width, height];
        }

        public static Vector2 CellToWorld(int x, int y) => new Vector2(x + 0.5f, y + 0.5f);
        public static Vector2 CellToWorld(Vector2Int c) => new Vector2(c.x + 0.5f, c.y + 0.5f);

        public static Vector2Int WorldToCell(Vector2 world)
            => new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(Vector2Int c) => InBounds(c.x, c.y);

        public bool IsWalkable(int x, int y) => InBounds(x, y) && !_blocked[x, y];
        public bool IsWalkable(Vector2Int c) => IsWalkable(c.x, c.y);

        public bool IsBlockedByFixture(Vector2Int c)
            => InBounds(c) && _blockedByFixture[c.x, c.y];

        public void SetBlocked(int x, int y, bool blocked, bool isFixture = false)
        {
            if (!InBounds(x, y)) return;
            _blocked[x, y] = blocked;
            if (isFixture) _blockedByFixture[x, y] = blocked;
        }

        public void SetBlockedRect(CellRect rect, bool blocked, bool isFixture = false)
        {
            for (int x = rect.xMin; x <= rect.xMax; x++)
                for (int y = rect.yMin; y <= rect.yMax; y++)
                    SetBlocked(x, y, blocked, isFixture);
        }

        /// <summary>
        /// 一个半径为 radius 的圆放在 world 位置时，是否与任何阻挡格重叠。
        /// 用于玩家移动的连续碰撞判定。
        /// </summary>
        public bool CircleOverlapsBlocked(Vector2 world, float radius)
        {
            int minX = Mathf.FloorToInt(world.x - radius);
            int maxX = Mathf.FloorToInt(world.x + radius);
            int minY = Mathf.FloorToInt(world.y - radius);
            int maxY = Mathf.FloorToInt(world.y + radius);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!InBounds(x, y)) return true;      // 出界视为墙
                    if (!_blocked[x, y]) continue;

                    // 圆 vs 轴对齐格子的最近点判定
                    float nearestX = Mathf.Clamp(world.x, x, x + 1f);
                    float nearestY = Mathf.Clamp(world.y, y, y + 1f);
                    float dx = world.x - nearestX;
                    float dy = world.y - nearestY;
                    if (dx * dx + dy * dy < radius * radius) return true;
                }
            }
            return false;
        }

        /// <summary>找到离 origin 最近的可行走格（含 origin 自身）。找不到返回 origin。</summary>
        public Vector2Int NearestWalkable(Vector2Int origin, int maxRadius = 8)
        {
            if (IsWalkable(origin)) return origin;

            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        var c = new Vector2Int(origin.x + dx, origin.y + dy);
                        if (IsWalkable(c)) return c;
                    }
                }
            }
            return origin;
        }

        /// <summary>某个设施矩形周围所有可站立的格子。</summary>
        public List<Vector2Int> AccessCells(CellRect rect)
        {
            var result = new List<Vector2Int>();
            for (int x = rect.xMin - 1; x <= rect.xMax + 1; x++)
            {
                for (int y = rect.yMin - 1; y <= rect.yMax + 1; y++)
                {
                    bool inside = x >= rect.xMin && x <= rect.xMax && y >= rect.yMin && y <= rect.yMax;
                    if (inside) continue;

                    // 只取四邻接（不取斜角），保证玩家真的贴着设施站
                    bool orthogonal =
                        (x >= rect.xMin && x <= rect.xMax) ||
                        (y >= rect.yMin && y <= rect.yMax);
                    if (!orthogonal) continue;

                    var c = new Vector2Int(x, y);
                    if (IsWalkable(c)) result.Add(c);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// 格子矩形（含边界）。刻意不叫 RectInt —— UnityEngine 里已有同名类型，
    /// 同时 using 两个命名空间会产生 CS0104 歧义。
    /// </summary>
    [System.Serializable]
    public struct CellRect
    {
        public int xMin, yMin, xMax, yMax;

        public CellRect(int xMin, int yMin, int xMax, int yMax)
        {
            this.xMin = xMin;
            this.yMin = yMin;
            this.xMax = xMax;
            this.yMax = yMax;
        }

        public static CellRect Single(int x, int y) => new CellRect(x, y, x, y);

        public int WidthCells => xMax - xMin + 1;
        public int HeightCells => yMax - yMin + 1;

        public Vector2 CenterWorld => new Vector2(
            (xMin + xMax + 1) * 0.5f,
            (yMin + yMax + 1) * 0.5f);

        public Vector2 SizeWorld => new Vector2(WidthCells, HeightCells);

        public bool Contains(Vector2Int c)
            => c.x >= xMin && c.x <= xMax && c.y >= yMin && c.y <= yMax;

        public IEnumerable<Vector2Int> Cells()
        {
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                    yield return new Vector2Int(x, y);
        }
    }
}
