using System.Collections.Generic;
using UnityEngine;

namespace MonsterMart.Core
{
    /// <summary>
    /// 网格 A*（八方向，禁止切角）。地图只有 24×16 = 384 格，
    /// 开放集用线性扫描足够快，且完全无 GC 压力（缓冲区复用）。
    /// </summary>
    public class Pathfinder
    {
        static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        const float StraightCost = 1f;
        const float DiagonalCost = 1.41421356f;

        readonly StoreGrid _grid;
        readonly float[,] _gScore;
        readonly float[,] _fScore;
        readonly Vector2Int[,] _cameFrom;
        readonly bool[,] _closed;
        readonly bool[,] _opened;
        readonly List<Vector2Int> _open = new List<Vector2Int>(128);

        public Pathfinder(StoreGrid grid)
        {
            _grid = grid;
            _gScore = new float[grid.Width, grid.Height];
            _fScore = new float[grid.Width, grid.Height];
            _cameFrom = new Vector2Int[grid.Width, grid.Height];
            _closed = new bool[grid.Width, grid.Height];
            _opened = new bool[grid.Width, grid.Height];
        }

        /// <summary>
        /// 求路径。成功时把结果写入 result（含终点，不含起点）并返回 true。
        /// 起点或终点不可行走时会自动吸附到最近的可行走格。
        /// </summary>
        public bool TryFindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> result)
        {
            result.Clear();

            start = _grid.NearestWalkable(start);
            goal = _grid.NearestWalkable(goal);

            if (!_grid.InBounds(start) || !_grid.InBounds(goal)) return false;
            if (start == goal) return true;
            if (!_grid.IsWalkable(goal)) return false;

            ResetBuffers();

            _gScore[start.x, start.y] = 0f;
            _fScore[start.x, start.y] = Heuristic(start, goal);
            _open.Add(start);
            _opened[start.x, start.y] = true;

            while (_open.Count > 0)
            {
                int bestIndex = 0;
                float bestF = _fScore[_open[0].x, _open[0].y];
                for (int i = 1; i < _open.Count; i++)
                {
                    float f = _fScore[_open[i].x, _open[i].y];
                    if (f < bestF)
                    {
                        bestF = f;
                        bestIndex = i;
                    }
                }

                Vector2Int current = _open[bestIndex];
                _open.RemoveAt(bestIndex);
                _opened[current.x, current.y] = false;

                if (current == goal)
                {
                    Reconstruct(start, goal, result);
                    return true;
                }

                _closed[current.x, current.y] = true;

                for (int n = 0; n < Neighbours.Length; n++)
                {
                    Vector2Int step = Neighbours[n];
                    var next = new Vector2Int(current.x + step.x, current.y + step.y);

                    if (!_grid.IsWalkable(next)) continue;
                    if (_closed[next.x, next.y]) continue;

                    bool diagonal = step.x != 0 && step.y != 0;
                    if (diagonal)
                    {
                        // 禁止贴着墙角斜穿
                        if (!_grid.IsWalkable(current.x + step.x, current.y)) continue;
                        if (!_grid.IsWalkable(current.x, current.y + step.y)) continue;
                    }

                    float tentative = _gScore[current.x, current.y] +
                                      (diagonal ? DiagonalCost : StraightCost);

                    if (_opened[next.x, next.y] && tentative >= _gScore[next.x, next.y]) continue;

                    _cameFrom[next.x, next.y] = current;
                    _gScore[next.x, next.y] = tentative;
                    _fScore[next.x, next.y] = tentative + Heuristic(next, goal);

                    if (!_opened[next.x, next.y])
                    {
                        _open.Add(next);
                        _opened[next.x, next.y] = true;
                    }
                }
            }

            return false;
        }

        void Reconstruct(Vector2Int start, Vector2Int goal, List<Vector2Int> result)
        {
            var node = goal;
            while (node != start)
            {
                result.Add(node);
                node = _cameFrom[node.x, node.y];
            }
            result.Reverse();
        }

        void ResetBuffers()
        {
            _open.Clear();
            for (int x = 0; x < _grid.Width; x++)
            {
                for (int y = 0; y < _grid.Height; y++)
                {
                    _gScore[x, y] = float.MaxValue;
                    _fScore[x, y] = float.MaxValue;
                    _closed[x, y] = false;
                    _opened[x, y] = false;
                }
            }
        }

        static float Heuristic(Vector2Int a, Vector2Int b)
        {
            // 八方向的标准 octile 距离
            float dx = Mathf.Abs(a.x - b.x);
            float dy = Mathf.Abs(a.y - b.y);
            return StraightCost * (dx + dy) + (DiagonalCost - 2f * StraightCost) * Mathf.Min(dx, dy);
        }
    }
}
