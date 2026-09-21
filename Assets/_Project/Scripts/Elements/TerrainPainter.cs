using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 撒元素地形。
    ///
    /// 每个房间生成 1–3 块**不规则**地形：用「随机化 BFS」从一个种子格往外长，
    /// 每步以一定概率不长出去，因此边界天然是凹凸不平的有机形状，
    /// 而不是规则的圆盘/方块。
    ///
    /// 不论长了多少格，**整块共享一份反应次数**（初始 3，每 5 秒 +1，上限 5）。
    /// </summary>
    public static class TerrainPainter
    {
        private const int MinBlobCells = 6;

        public static int Paint(FloorRuntime floor, Transform root)
        {
            var eg = GameRuntime.I != null ? GameRuntime.I.Elements : null;
            if (eg == null) return 0;

            var grid = floor.Layout.Grid;
            var rng = new System.Random(1000 + floor.FloorIndex * 977);
            int painted = 0;

            foreach (var room in floor.Layout.Rooms)
            {
                int patches = rng.Next(1, 4);
                var b = room.InnerBounds;

                for (int p = 0; p < patches; p++)
                {
                    var element = (ElementType)rng.Next(1, 4);   // Water/Fire/Wood
                    int target = rng.Next(14, 62);
                    var seed = new Vector2Int(
                        rng.Next(b.xMin + 2, Mathf.Max(b.xMin + 3, b.xMax - 2)),
                        rng.Next(b.yMin + 2, Mathf.Max(b.yMin + 3, b.yMax - 2)));

                    var cells = GrowBlob(grid, room, seed, target, rng, eg);
                    if (cells.Count < MinBlobCells) continue;

                    var blob = eg.CreateBlob(cells, element, root);
                    if (blob != null) painted += blob.CellCount;
                }
            }
            return painted;
        }

        /// <summary>随机化 BFS 长出不规则形状。</summary>
        private static List<Vector2Int> GrowBlob(LevelGrid grid, RoomNode room, Vector2Int seed,
                                                 int target, System.Random rng, ElementGrid eg)
        {
            var cells = new List<Vector2Int>();
            if (!CanUse(grid, room, seed, eg)) return cells;

            var inSet = new HashSet<Vector2Int> { seed };
            var queue = new Queue<Vector2Int>();
            var dirs = LevelGrid.Dirs;

            queue.Enqueue(seed);
            cells.Add(seed);

            while (queue.Count > 0 && cells.Count < target)
            {
                var cur = queue.Dequeue();
                int offset = rng.Next(dirs.Length);

                for (int k = 0; k < dirs.Length; k++)
                {
                    if (cells.Count >= target) break;

                    var n = cur + dirs[(offset + k) % dirs.Length];
                    if (inSet.Contains(n)) continue;
                    if (!CanUse(grid, room, n, eg)) continue;

                    // 以一定概率跳过，形成不规则边界
                    if (rng.NextDouble() > 0.82) continue;

                    inSet.Add(n);
                    cells.Add(n);
                    queue.Enqueue(n);
                }
            }
            return cells;
        }

        private static bool CanUse(LevelGrid grid, RoomNode room, Vector2Int c, ElementGrid eg)
        {
            if (!grid.IsWalkable(c)) return false;
            if (grid.DoorCells.Contains(c)) return false;
            if (grid.RoomAt(c) != room) return false;
            if (eg.HasTerrain(c)) return false;
            return true;
        }
    }
}
