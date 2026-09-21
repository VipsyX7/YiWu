using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 整层的格子地图。世界坐标与格子一一对应：
    /// 格 (x,y) 覆盖世界范围 [x, x+1) × [y, y+1)，中心为 (x+0.5, y+0.5)。
    /// 供 A* 寻路、弹体撞墙判定、元素地形查询共用。
    /// </summary>
    public class LevelGrid
    {
        public readonly int Width;
        public readonly int Height;

        private readonly bool[] _walkable;
        private readonly RoomNode[] _roomOf;

        public readonly int StrideX;
        public readonly int StrideY;
        public readonly int WallThickness;
        public readonly int RoomWidth;
        public readonly int RoomHeight;

        public LevelGrid(int width, int height, int strideX, int strideY,
                         int wallThickness, int roomWidth, int roomHeight)
        {
            Width = width;
            Height = height;
            StrideX = strideX;
            StrideY = strideY;
            WallThickness = wallThickness;
            RoomWidth = roomWidth;
            RoomHeight = roomHeight;
            _walkable = new bool[width * height];
            _roomOf = new RoomNode[width * height];
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        public bool IsWalkable(Vector2Int c) => InBounds(c) && _walkable[c.y * Width + c.x];

        public void SetWalkable(Vector2Int c, bool v)
        {
            if (!InBounds(c)) return;
            _walkable[c.y * Width + c.x] = v;
        }

        public RoomNode RoomAt(Vector2Int c)
        {
            if (!InBounds(c)) return null;
            return _roomOf[c.y * Width + c.x];
        }

        public void SetRoom(Vector2Int c, RoomNode r)
        {
            if (!InBounds(c)) return;
            _roomOf[c.y * Width + c.x] = r;
        }

        public RoomNode RoomAtWorld(Vector2 w) => RoomAt(WorldToCell(w));

        public static Vector2Int WorldToCell(Vector2 w)
        {
            return new Vector2Int(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y));
        }

        public static Vector3 CellCenter(Vector2Int c) => new Vector3(c.x + 0.5f, c.y + 0.5f, 0f);

        public static Vector2 CellCenter2(Vector2Int c) => new Vector2(c.x + 0.5f, c.y + 0.5f);

        /// <summary>把一段世界坐标矩形覆盖的格子全部标记为可走。</summary>
        public void FillWalkable(RectInt rect, bool value, RoomNode room)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                var c = new Vector2Int(x, y);
                SetWalkable(c, value);
                if (room != null) SetRoom(c, room);
            }
        }

        /// <summary>四个正方向的邻居（不走斜角，与以撒式房间一致）。</summary>
        public static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };

        /// <summary>所有门洞格子（门锁上时不可通行）。</summary>
        public readonly HashSet<Vector2Int> DoorCells = new HashSet<Vector2Int>();
        /// <summary>当前处于开启状态的门洞格子。</summary>
        public readonly HashSet<Vector2Int> OpenDoorCells = new HashSet<Vector2Int>();

        /// <summary>寻路/移动用：可走，且不被关闭的门挡住。</summary>
        public bool IsPassable(Vector2Int c)
        {
            if (!IsWalkable(c)) return false;
            if (DoorCells.Contains(c) && !OpenDoorCells.Contains(c)) return false;
            return true;
        }

        /// <summary>该格是否暴露在可玩区旁边（用于只给可见墙生成美术/碰撞体）。</summary>
        public bool IsExposedWall(Vector2Int c)
        {
            if (IsWalkable(c)) return false;
            for (int i = 0; i < Dirs.Length; i++)
                if (IsWalkable(c + Dirs[i])) return true;
            return false;
        }

        public IEnumerable<Vector2Int> AllCells()
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return new Vector2Int(x, y);
        }
    }
}
