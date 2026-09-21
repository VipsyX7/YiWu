using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 一层楼的世界构建与运行：墙体、门、元素地形、房间控制器。
    /// 白盒不做房间切换，整层一次性铺在世界里，摄像机跟随玩家。
    /// </summary>
    public class FloorRuntime : MonoBehaviour
    {
        public FloorLayout Layout { get; private set; }
        public int FloorIndex => Layout != null ? Layout.FloorIndex : 0;

        private GameConfig _cfg;
        private Transform _walls, _doors, _rooms, _terrain;
        private readonly Dictionary<Vector2Int, DoorController> _doorByCell = new Dictionary<Vector2Int, DoorController>();
        private readonly List<RoomController> _roomCtrls = new List<RoomController>();

        public RoomController CurrentRoom { get; private set; }

        public static FloorRuntime Build(int floorIndex, GameConfig cfg, int seed, Transform parent)
        {
            var go = new GameObject("Floor_" + floorIndex);
            go.transform.SetParent(parent, false);
            var fr = go.AddComponent<FloorRuntime>();
            fr._cfg = cfg;

            var layout = FloorGenerator.Generate(floorIndex, cfg, seed);
            fr.Layout = layout;
            fr.Construct();
            return fr;
        }

        private void Construct()
        {
            _walls = NewChild("Walls");
            _doors = NewChild("Doors");
            _rooms = NewChild("Rooms");
            _terrain = NewChild("Terrain");

            BuildWalls();
            BuildDoors();

            foreach (var node in Layout.Rooms)
            {
                var rc = RoomController.Create(_rooms, node, this);
                _roomCtrls.Add(rc);
                node.RuntimeRoom = rc;
            }

            TerrainPainter.Paint(this, _terrain);
        }

        private Transform NewChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        // ------------------------------------------------------------ 墙体

        private void BuildWalls()
        {
            var grid = Layout.Grid;
            var wallColor = new Color(0.20f, 0.21f, 0.26f);
            var placeholder = SpriteFactory.Frame(Color.white, Color.white);
            var sprite = Visuals.Get(VisualKey.Wall, placeholder);
            var tint = Visuals.Tint(VisualKey.Wall, wallColor);
            bool tile = Visuals.Set != null && Visuals.Set.TileWalls;
            int runs = 0;

            // 只给「暴露在可玩区旁」的墙生成对象，按行合并成一条，控制对象数量。
            for (int y = 0; y < grid.Height; y++)
            {
                int x = 0;
                while (x < grid.Width)
                {
                    if (!grid.IsExposedWall(new Vector2Int(x, y))) { x++; continue; }

                    int start = x;
                    while (x < grid.Width && grid.IsExposedWall(new Vector2Int(x, y))) x++;
                    int len = x - start;

                    var go = new GameObject($"Wall_{y}_{start}_{len}");
                    go.transform.SetParent(_walls, false);
                    go.transform.position = new Vector3(start + len * 0.5f, y + 0.5f, 0f);

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sharedMaterial = SpriteFactory.DefaultMaterial;
                    sr.color = tint;
                    sr.sortingOrder = -5;

                    if (tile)
                    {
                        // 平铺（要求墙贴图 Mesh Type = Full Rect）
                        sr.drawMode = SpriteDrawMode.Tiled;
                        sr.size = new Vector2(len, 1f);
                    }
                    else
                    {
                        go.transform.localScale = Visuals.ScaleFor(VisualKey.Wall, sprite, new Vector2(len, 1f));
                    }

                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = Vector2.one;
                    col.offset = Vector2.zero;

                    runs++;
                }
            }
            _wallRunCount = runs;
        }

        private int _wallRunCount;

        // ------------------------------------------------------------ 门

        private void BuildDoors()
        {
            foreach (var link in Layout.Doors)
            {
                var d = DoorController.Create(_doors, link, this);
                link.Runtime = d;
                foreach (var c in link.Cells) _doorByCell[c] = d;
            }
        }

        /// <summary>玩家所在格是否被关闭的门挡住（供出生点校验等使用）。</summary>
        public DoorController DoorAt(Vector2Int cell)
        {
            return _doorByCell.TryGetValue(cell, out var d) ? d : null;
        }

        // ------------------------------------------------------------ 每帧

        private void Update()
        {
            var player = GameRuntime.I != null ? GameRuntime.I.Player : null;
            if (player == null) return;

            var pos = (Vector2)player.transform.position;
            var room = Layout.Grid.RoomAtWorld(pos);
            var roomCtrl = room != null ? room.RuntimeRoom : null;

            if (roomCtrl != CurrentRoom)
            {
                if (CurrentRoom != null)
                {
                    var prev = CurrentRoom;
                    CurrentRoom.OnPlayerExited();
                    GameRuntime.I.OnRoomExited(prev);
                }
                CurrentRoom = roomCtrl;
                if (CurrentRoom != null)
                {
                    CurrentRoom.OnPlayerEntered();
                    GameRuntime.I.OnRoomEntered(CurrentRoom);
                }
            }

            RefreshDoors(pos);
        }

        private void RefreshDoors(Vector2 playerPos)
        {
            for (int i = 0; i < Layout.Doors.Count; i++)
            {
                var link = Layout.Doors[i];
                if (link.Runtime == null) continue;

                bool inA = link.A.Contains(playerPos);
                bool inB = link.B.Contains(playerPos);
                bool open;
                if (inA) open = link.A.Cleared;
                else if (inB) open = link.B.Cleared;
                else open = link.A.Cleared || link.B.Cleared;

                link.Runtime.Refresh(open);
            }
        }

        public void OnRoomStateChanged()
        {
            // 门状态在 Update 里统一刷新；这里只处理 BOSS 房传送门。
            foreach (var rc in _roomCtrls)
            {
                if (rc.Node.Type == RoomType.Boss && rc.Node.Cleared && !rc.Node.PortalSpawned)
                {
                    rc.Node.PortalSpawned = true;
                    rc.SpawnPortal();
                }
            }
        }

        public List<RoomController> RoomControllers => _roomCtrls;
        public int WallRunCount => _wallRunCount;

        public void Dispose()
        {
            Destroy(gameObject);
        }
    }
}
