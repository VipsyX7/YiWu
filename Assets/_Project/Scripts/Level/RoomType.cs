using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 房间类型。v2 策划案删除了 v1 的「事件房」，此处与 v2 保持一致。
    /// </summary>
    public enum RoomType
    {
        Trash = 0,   // 小怪房
        Shop = 1,    // 商店房
        Spell = 2,   // 法术房
        Chest = 3,   // 宝箱房
        Boss = 4,    // BOSS 房
        Start = 5,   // 起点（白盒：等同小怪房，但不刷怪）
    }

    public enum DoorSide { Up = 0, Right = 1, Down = 2, Left = 3 }

    public static class RoomTypeUtil
    {
        public static bool IsSpecial(this RoomType t)
        {
            return t == RoomType.Shop || t == RoomType.Spell || t == RoomType.Chest || t == RoomType.Boss;
        }

        public static Color ToColor(this RoomType t)
        {
            switch (t)
            {
                case RoomType.Shop: return new Color(0.95f, 0.80f, 0.25f);
                case RoomType.Spell: return new Color(0.55f, 0.40f, 0.95f);
                case RoomType.Chest: return new Color(0.80f, 0.55f, 0.20f);
                case RoomType.Boss: return new Color(0.85f, 0.20f, 0.25f);
                case RoomType.Start: return new Color(0.35f, 0.85f, 0.55f);
                default: return new Color(0.42f, 0.45f, 0.52f);
            }
        }

        public static string ToCn(this RoomType t)
        {
            switch (t)
            {
                case RoomType.Shop: return "商店房";
                case RoomType.Spell: return "法术房";
                case RoomType.Chest: return "宝箱房";
                case RoomType.Boss: return "BOSS房";
                case RoomType.Start: return "起点";
                default: return "小怪房";
            }
        }
    }

    /// <summary>楼层布局中的一个房间节点（纯数据，不含 GameObject）。</summary>
    public class RoomNode
    {
        public Vector2Int GridIndex;
        public RoomType Type = RoomType.Trash;
        public int Depth;                       // 距起点的 BFS 距离
        public bool Visited;
        public bool Cleared;

        /// <summary>可玩区（世界坐标，格对齐）。</summary>
        public RectInt InnerBounds;

        /// <summary>是否已生成过内容（白盒按需生成，节省开销）。</summary>
        public bool Populated;

        /// <summary>运行时控制器。</summary>
        public RoomController RuntimeRoom;

        /// <summary>BOSS 房是否已生成传送门。</summary>
        public bool PortalSpawned;

        public Vector2 Center => new Vector2(InnerBounds.xMin + InnerBounds.width * 0.5f,
                                             InnerBounds.yMin + InnerBounds.height * 0.5f);

        public bool Contains(Vector2 world)
        {
            return world.x >= InnerBounds.xMin && world.x <= InnerBounds.xMax &&
                   world.y >= InnerBounds.yMin && world.y <= InnerBounds.yMax;
        }
    }

    /// <summary>两个相邻房间之间的门。</summary>
    public class DoorLink
    {
        public RoomNode A;
        public RoomNode B;
        public DoorSide SideFromA;
        /// <summary>门占用的格子（通常 2 格）。</summary>
        public Vector2Int[] Cells;
        public DoorController Runtime;
    }

    /// <summary>一整层的布局结果。</summary>
    public class FloorLayout
    {
        public int FloorIndex;
        public LevelGrid Grid;
        public readonly System.Collections.Generic.List<RoomNode> Rooms = new System.Collections.Generic.List<RoomNode>();
        public readonly System.Collections.Generic.List<DoorLink> Doors = new System.Collections.Generic.List<DoorLink>();
        public RoomNode StartRoom;

        public RoomNode RoomAtGrid(Vector2Int gi)
        {
            for (int i = 0; i < Rooms.Count; i++)
                if (Rooms[i].GridIndex == gi) return Rooms[i];
            return null;
        }
    }
}
