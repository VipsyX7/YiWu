using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 网格 A*。Unity 没有内置 2D 寻路（NavMesh 是 3D 的），故自写一个
    /// （见可行性分析风险 R4）。地图规模约 81×51，路径短，线性取最小的开放表足够。
    /// </summary>
    public static class GridPathfinder
    {
        public static List<Vector2Int> Find(LevelGrid grid, Vector2Int start, Vector2Int goal,
                                            int maxExpansions = 8000)
        {
            var path = new List<Vector2Int>();
            if (grid == null) return path;
            if (!grid.IsPassable(goal)) goal = NearestPassable(grid, goal);
            if (!grid.IsPassable(start) || !grid.IsPassable(goal)) return path;
            if (start == goal) return path;

            var open = new List<Vector2Int>();
            var gScore = new Dictionary<Vector2Int, int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var closed = new HashSet<Vector2Int>();

            open.Add(start);
            gScore[start] = 0;
            int expansions = 0;

            while (open.Count > 0 && expansions++ < maxExpansions)
            {
                int bestIdx = 0, bestF = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    var c = open[i];
                    int f = gScore[c] + Heuristic(c, goal);
                    if (f < bestF) { bestF = f; bestIdx = i; }
                }

                var cur = open[bestIdx];
                open.RemoveAt(bestIdx);

                if (cur == goal) return Reconstruct(cameFrom, start, goal);

                closed.Add(cur);
                var g = gScore[cur];

                for (int i = 0; i < LevelGrid.Dirs.Length; i++)
                {
                    var nb = cur + LevelGrid.Dirs[i];
                    if (closed.Contains(nb)) continue;
                    if (!grid.IsPassable(nb)) continue;

                    int tentative = g + 1;
                    if (gScore.TryGetValue(nb, out int old) && tentative >= old) continue;

                    gScore[nb] = tentative;
                    cameFrom[nb] = cur;
                    if (!open.Contains(nb)) open.Add(nb);
                }
            }
            return path;
        }

        private static int Heuristic(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> cameFrom,
                                                    Vector2Int start, Vector2Int goal)
        {
            var path = new List<Vector2Int>();
            var cur = goal;
            while (cur != start)
            {
                path.Add(cur);
                if (!cameFrom.TryGetValue(cur, out cur)) break;
            }
            path.Reverse();
            return path;
        }

        /// <summary>目标格不可走时，向外螺旋找最近的可走格（例如玩家点了墙）。</summary>
        public static Vector2Int NearestPassable(LevelGrid grid, Vector2Int goal, int maxRadius = 6)
        {
            if (grid.IsPassable(goal)) return goal;
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    var c = goal + new Vector2Int(dx, dy);
                    if (grid.IsPassable(c)) return c;
                }
            }
            return goal;
        }
    }
}
