using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 一整块元素地形。
    ///
    /// 形状可以是不规则的，但**反应时视作一个整体**：
    ///   · 块内所有格子**共享同一份反应次数**（初始 3）
    ///   · 一次法术对同一块地形**最多产生一次反应**（由 Projectile 记录已反应过的 blob）
    ///   · 每 <see cref="RechargeInterval"/> 秒自行 +1 次，上限 <see cref="MaxCharges"/>
    ///   · 反应冷却（默认 60 帧）也按【块】计，而不是按格
    /// </summary>
    public class TerrainBlob
    {
        public int Id;
        public ElementType Element;

        public int Charges;
        public int MaxCharges = 5;

        /// <summary>自动回充间隔（秒）。默认 5 秒一次。</summary>
        public float RechargeInterval = 5f;
        public float RechargeTimer;

        /// <summary>上一次发生反应的帧号，用于"同一地形每 N 帧最多反应一次"。</summary>
        public int LastReactFrame = -99999;

        /// <summary>块内全部格子。</summary>
        public readonly List<Vector2Int> Cells = new List<Vector2Int>();

        /// <summary>每格的显示对象（形状不规则，所以逐格渲染）。</summary>
        public readonly Dictionary<Vector2Int, SpriteRenderer> Views =
            new Dictionary<Vector2Int, SpriteRenderer>();

        /// <summary>质心，以及离质心最近的块内格子（区域效果以此为中心，保证落在地形上）。</summary>
        public Vector2 Centroid;
        public Vector2Int CenterCell;

        // ---- 显示 ----
        /// <summary>该地块对应的贴图槽位（按元素）。</summary>
        public VisualKey Key = VisualKey.None;
        /// <summary>创建时解析出的贴图与归一化缩放（耗尽换图时会临时覆盖）。</summary>
        public Sprite BaseSprite;
        public Vector3 BaseScale = Vector3.one;

        public bool Depleted => Charges <= 0;
        public int CellCount => Cells.Count;

        public bool Contains(Vector2Int c) => Views.ContainsKey(c);
    }
}
