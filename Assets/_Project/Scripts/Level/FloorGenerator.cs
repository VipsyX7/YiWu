using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 以撒式楼层生成器。
    ///
    /// 规则来源（策划案 v2）：
    ///  · 类似《以撒的结合》的网格 + 门
    ///  · 除小怪房外的所有房间一层只能生成一个，并且与小怪房相连
    ///  · 每层随机生成 小怪房 / 商店房 / 法术房 / 宝箱房 / BOSS房
    ///  · BOSS 房击败后生成传送门进入下一层（v2 删除了 v1 的「路尽头」约束）
    ///
    /// 白盒解释（记入 Docs）：
    ///  · 网格中没有「连续」概念，故「不能连续同类房间」实现为
    ///    「两个特殊房不得相邻」（见 Gap A9）。
    ///  · BOSS 房取距起点 BFS 最远的房间，近似 v1 的「道路尽头」语义。
    /// </summary>
    public static class FloorGenerator
    {
        public static FloorLayout Generate(int floorIndex, GameConfig cfg, int seed)
        {
            var rng = new System.Random(seed);
            int gs = Mathf.Max(3, cfg.GridSize);
            int strideX = cfg.RoomWidth + cfg.WallThickness;
            int strideY = cfg.RoomHeight + cfg.WallThickness;
            int w = gs * strideX + cfg.WallThickness;
            int h = gs * strideY + cfg.WallThickness;

            var layout = new FloorLayout
            {
                FloorIndex = floorIndex,
                Grid = new LevelGrid(w, h, strideX, strideY, cfg.WallThickness, cfg.RoomWidth, cfg.RoomHeight),
            };

            // ---- 1. 随机游走铺房间，保证连通 ----
            var center = new Vector2Int(gs / 2, gs / 2);
            var dirs = LevelGrid.Dirs;
            HashSet<Vector2Int> occupied = null;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int target = rng.Next(cfg.MinRoomsPerFloor, cfg.MaxRoomsPerFloor + 1);
                var set = GrowRooms(gs, center, target, dirs, rng);
                if (occupied == null || set.Count > occupied.Count) occupied = set;
                if (occupied.Count >= cfg.MinRoomsPerFloor) break;
            }

            // ---- 2. 建节点 ----
            foreach (var gi in occupied)
            {
                int ox = gi.x * strideX + cfg.WallThickness;
                int oy = gi.y * strideY + cfg.WallThickness;
                var node = new RoomNode
                {
                    GridIndex = gi,
                    InnerBounds = new RectInt(ox, oy, cfg.RoomWidth, cfg.RoomHeight),
                };
                layout.Rooms.Add(node);
            }

            layout.StartRoom = layout.RoomAtGrid(center);

            // ---- 3. 网格标记：可玩区可走，其余默认墙 ----
            foreach (var r in layout.Rooms)
                layout.Grid.FillWalkable(r.InnerBounds, true, r);

            // ---- 4. 邻接与门 ----
            BuildDoors(layout, cfg);

            // ---- 5. 深度（BFS） ----
            ComputeDepth(layout);

            // ---- 6. 房间类型分配 ----
            AssignTypes(layout, rng);

            return layout;
        }

        /// <summary>
        /// 从中心向外长房间。每一步只在「还有空邻居」的房间上生长，
        /// 因此只要还能长就一定能继续，不会因为随机方向连撞墙而提前卡死。
        /// </summary>
        private static HashSet<Vector2Int> GrowRooms(int gs, Vector2Int center, int target,
                                                     Vector2Int[] dirs, System.Random rng)
        {
            var occupied = new HashSet<Vector2Int> { center };
            var cand = new List<Vector2Int>();
            var free = new List<Vector2Int>();

            while (occupied.Count < target)
            {
                cand.Clear();
                foreach (var c in occupied)
                {
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        var n = c + dirs[i];
                        if (n.x < 0 || n.y < 0 || n.x >= gs || n.y >= gs) continue;
                        if (occupied.Contains(n)) continue;
                        cand.Add(c);
                        break;
                    }
                }
                if (cand.Count == 0) break;

                var from = cand[rng.Next(cand.Count)];
                free.Clear();
                for (int i = 0; i < dirs.Length; i++)
                {
                    var n = from + dirs[i];
                    if (n.x < 0 || n.y < 0 || n.x >= gs || n.y >= gs) continue;
                    if (!occupied.Contains(n)) free.Add(n);
                }
                if (free.Count == 0) continue;

                occupied.Add(free[rng.Next(free.Count)]);
            }
            return occupied;
        }

        private static void BuildDoors(FloorLayout layout, GameConfig cfg)
        {
            var dirs = new[] { Vector2Int.right, Vector2Int.down };
            var sides = new[] { DoorSide.Right, DoorSide.Down };

            foreach (var room in layout.Rooms)
            {
                for (int i = 0; i < dirs.Length; i++)
                {
                    var nb = layout.RoomAtGrid(room.GridIndex + dirs[i]);
                    if (nb == null) continue;

                    // 共享墙位于两房间可玩区之间
                    var cells = new List<Vector2Int>();
                    if (sides[i] == DoorSide.Right)
                    {
                        // 墙列 = 本房右边界（xMax == xMin + RoomWidth）
                        int wx = room.InnerBounds.xMax;
                        int cy = room.InnerBounds.yMin + cfg.RoomHeight / 2;
                        cells.Add(new Vector2Int(wx, cy - 1));
                        cells.Add(new Vector2Int(wx, cy));
                    }
                    else
                    {
                        // 墙行 = 本房下边界往外一格（不是 yMax！yMax 是「上方」那堵墙）
                        int wy = room.InnerBounds.yMin - cfg.WallThickness;
                        int cx = room.InnerBounds.xMin + cfg.RoomWidth / 2;
                        cells.Add(new Vector2Int(cx - 1, wy));
                        cells.Add(new Vector2Int(cx, wy));
                    }

                    foreach (var c in cells)
                    {
                        layout.Grid.SetWalkable(c, true);
                        layout.Grid.SetRoom(c, room);   // 门洞归属 A 房，跨房判定用 DoorController 兜底
                    }

                    layout.Doors.Add(new DoorLink
                    {
                        A = room, B = nb, SideFromA = sides[i],
                        Cells = cells.ToArray(),
                    });
                }
            }
        }

        private static void ComputeDepth(FloorLayout layout)
        {
            var q = new Queue<RoomNode>();
            foreach (var r in layout.Rooms) r.Depth = -1;
            if (layout.StartRoom == null) return;
            layout.StartRoom.Depth = 0;
            q.Enqueue(layout.StartRoom);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var d in LevelGrid.Dirs)
                {
                    var nb = layout.RoomAtGrid(cur.GridIndex + d);
                    if (nb == null || nb.Depth >= 0) continue;
                    nb.Depth = cur.Depth + 1;
                    q.Enqueue(nb);
                }
            }
        }

        private static void AssignTypes(FloorLayout layout, System.Random rng)
        {
            if (layout.StartRoom == null) return;
            layout.StartRoom.Type = RoomType.Start;

            // 候选：非起点
            var pool = new List<RoomNode>();
            foreach (var r in layout.Rooms)
                if (r != layout.StartRoom) pool.Add(r);

            // BOSS = 距起点最远者
            pool.Sort((a, b) => b.Depth.CompareTo(a.Depth));
            RoomNode boss = pool.Count > 0 ? pool[0] : null;
            if (boss != null) boss.Type = RoomType.Boss;

            // 其余特殊房：深度 >= 2，且不与任何特殊房相邻
            var specials = new[] { RoomType.Shop, RoomType.Spell, RoomType.Chest };
            var cand = new List<RoomNode>();
            foreach (var r in pool)
                if (r != boss && r.Depth >= 2) cand.Add(r);
            Shuffle(cand, rng);

            int want = specials.Length;
            var placed = new List<RoomNode>();
            if (boss != null) placed.Add(boss);

            foreach (var t in specials)
            {
                RoomNode pick = null;
                foreach (var c in cand)
                {
                    if (c.Type != RoomType.Trash) continue;
                    if (IsAdjacentToAny(c, layout, placed)) continue;
                    pick = c; break;
                }
                if (pick == null)
                {
                    // 放宽：只要求不与特殊房相邻
                    foreach (var c in cand)
                    {
                        if (c.Type != RoomType.Trash) continue;
                        pick = c; break;
                    }
                }
                if (pick == null) continue;
                pick.Type = t;
                placed.Add(pick);
            }
            _ = want;
        }

        private static bool IsAdjacentToAny(RoomNode node, FloorLayout layout, List<RoomNode> others)
        {
            foreach (var d in LevelGrid.Dirs)
            {
                var nb = layout.RoomAtGrid(node.GridIndex + d);
                if (nb == null) continue;
                if (others.Contains(nb)) return true;
            }
            return false;
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
