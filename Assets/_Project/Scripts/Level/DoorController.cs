using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 房间之间的门。锁上时用 BoxCollider2D 阻挡玩家；房间清空后开启。
    /// 开启判定：玩家所在的那一侧房间已清空（以撒规则：本房未清空不能离开）。
    /// </summary>
    public class DoorController : MonoBehaviour
    {
        public DoorLink Link { get; private set; }
        public bool IsOpen { get; private set; }

        private BoxCollider2D _col;
        private SpriteRenderer _sr;
        private FloorRuntime _floor;
        private Vector2 _size;

        private static readonly Color LockedColor = new Color(0.75f, 0.62f, 0.25f);
        private static readonly Color OpenColor = new Color(0.30f, 0.85f, 0.45f, 0.55f);

        public static DoorController Create(Transform parent, DoorLink link, FloorRuntime floor)
        {
            var go = new GameObject("Door_" + link.A.GridIndex + "_" + link.B.GridIndex);
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DoorController>();
            d.Build(link, floor);
            return d;
        }

        private void Build(DoorLink link, FloorRuntime floor)
        {
            Link = link;
            _floor = floor;

            Vector2 min = LevelGrid.CellCenter2(link.Cells[0]);
            Vector2 max = LevelGrid.CellCenter2(link.Cells[link.Cells.Length - 1]);
            Vector2 center = (min + max) * 0.5f;
            Vector2 size = new Vector2(Mathf.Abs(max.x - min.x) + 1f, Mathf.Abs(max.y - min.y) + 1f);
            if (link.SideFromA == DoorSide.Right || link.SideFromA == DoorSide.Left)
                size = new Vector2(1f, 2f);
            else
                size = new Vector2(2f, 1f);

            _size = size;
            transform.position = new Vector3(center.x, center.y, 0f);

            _sr = Make.Visual("Visual", transform, Vector3.zero, VisualKey.Door_Locked,
                              SpriteFactory.Solid(Color.white), LockedColor, size, 5);

            _col = gameObject.AddComponent<BoxCollider2D>();
            _col.size = size;
            _col.offset = Vector2.zero;

            foreach (var c in link.Cells)
                floor.Layout.Grid.DoorCells.Add(c);

            IsOpen = false;
            SyncGrid();
        }

        /// <summary>按当前房间状态刷新门。由 FloorRuntime 每帧调用。</summary>
        public void Refresh(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            if (_col != null) _col.enabled = !open;
            if (_sr != null)
            {
                var key = open ? VisualKey.Door_Open : VisualKey.Door_Locked;
                var tint = open ? OpenColor : LockedColor;
                Visuals.Apply(_sr, key, SpriteFactory.Solid(Color.white), tint, _size);
            }
            SyncGrid();
        }

        private void SyncGrid()
        {
            if (_floor == null) return;
            foreach (var c in Link.Cells)
            {
                if (IsOpen) _floor.Layout.Grid.OpenDoorCells.Add(c);
                else _floor.Layout.Grid.OpenDoorCells.Remove(c);
            }
        }
    }
}
