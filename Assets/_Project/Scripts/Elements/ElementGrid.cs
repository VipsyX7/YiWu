using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 全局元素地形表。
    ///
    /// 地形的单位是【块】（<see cref="TerrainBlob"/>）而不是格：
    /// 一块形状不规则的地形共享一份反应次数，反应冷却、耗尽判定、区域效果中心
    /// 全部以块为单位。查询仍然是 O(1) 的按格索引。
    /// </summary>
    public class ElementGrid
    {
        private readonly Dictionary<Vector2Int, TerrainBlob> _cellToBlob =
            new Dictionary<Vector2Int, TerrainBlob>();
        private readonly List<TerrainBlob> _blobs = new List<TerrainBlob>();
        private int _nextId = 1;

        /// <summary>新建地块时的初始次数。</summary>
        public int InitialCharges = 3;
        /// <summary>新建地块时的次数上限。</summary>
        public int MaxCharges = 5;
        /// <summary>同一块地形每 N 帧最多产生一次反应。</summary>
        public int ReactCooldownFrames = 60;
        /// <summary>新建地块时的自动回充间隔（秒）。</summary>
        public float RechargeInterval = 5f;

        private static readonly Color DepletedColor = new Color(0.35f, 0.35f, 0.38f, 0.16f);

        public int BlobCount => _blobs.Count;
        public int CellCount => _cellToBlob.Count;
        public IReadOnlyList<TerrainBlob> AllBlobs => _blobs;

        // ------------------------------------------------------------ 查询

        public TerrainBlob GetBlob(Vector2Int c)
            => _cellToBlob.TryGetValue(c, out var b) ? b : null;

        public TerrainBlob GetBlobAt(Vector2 world)
            => GetBlob(LevelGrid.WorldToCell(world));

        public bool HasTerrain(Vector2Int c) => _cellToBlob.ContainsKey(c);

        // ------------------------------------------------------------ 创建 / 删除

        /// <summary>用一组格子创建一块地形。返回 null 表示没有可用的格子。</summary>
        public TerrainBlob CreateBlob(IEnumerable<Vector2Int> cells, ElementType element, Transform root)
        {
            if (element == ElementType.None) return null;

            var blob = new TerrainBlob
            {
                Id = _nextId++,
                Element = element,
                Charges = InitialCharges,
                MaxCharges = MaxCharges,
                RechargeInterval = RechargeInterval,
            };

            foreach (var c in cells)
            {
                if (_cellToBlob.ContainsKey(c)) continue;
                blob.Cells.Add(c);
            }
            if (blob.Cells.Count < 1) return null;

            var key = Visuals.TerrainKey(element);
            var placeholder = SpriteFactory.Solid(Color.white);
            var baseSprite = Visuals.Get(key, placeholder);
            blob.Key = key;
            blob.BaseSprite = baseSprite;
            blob.BaseScale = Visuals.ScaleFor(key, baseSprite, Vector2.one);

            foreach (var c in blob.Cells)
            {
                var view = Make.Sprite("Terrain_" + c.x + "_" + c.y, root,
                                       LevelGrid.CellCenter(c), baseSprite, Color.white, -3);
                view.transform.localScale = blob.BaseScale;
                blob.Views[c] = view;
                _cellToBlob[c] = blob;
            }

            ComputeCenter(blob);
            _blobs.Add(blob);
            RefreshViews(blob);
            return blob;
        }

        /// <summary>单格地块（供「地形生成」修正器与测试使用）。</summary>
        public TerrainBlob Add(Vector2Int c, ElementType element, Transform root)
            => CreateBlob(new[] { c }, element, root);

        public void RemoveBlob(TerrainBlob blob)
        {
            if (blob == null) return;
            foreach (var c in blob.Cells) _cellToBlob.Remove(c);
            foreach (var kv in blob.Views)
                if (kv.Value != null) Object.Destroy(kv.Value.gameObject);
            blob.Views.Clear();
            blob.Cells.Clear();
            _blobs.Remove(blob);
        }

        /// <summary>移除某一格；地块空了就整块移除。</summary>
        public void Remove(Vector2Int c)
        {
            var blob = GetBlob(c);
            if (blob == null) return;
            _cellToBlob.Remove(c);
            if (blob.Views.TryGetValue(c, out var v) && v != null) Object.Destroy(v.gameObject);
            blob.Views.Remove(c);
            blob.Cells.Remove(c);
            if (blob.Cells.Count == 0) _blobs.Remove(blob);
        }

        private static void ComputeCenter(TerrainBlob blob)
        {
            Vector2 sum = Vector2.zero;
            foreach (var c in blob.Cells) sum += new Vector2(c.x + 0.5f, c.y + 0.5f);
            blob.Centroid = sum / blob.Cells.Count;

            // 取离质心最近的块内格子，保证区域效果中心一定落在这块地形上
            float best = float.MaxValue;
            var bestCell = blob.Cells[0];
            foreach (var c in blob.Cells)
            {
                float d = (LevelGrid.CellCenter2(c) - blob.Centroid).sqrMagnitude;
                if (d < best) { best = d; bestCell = c; }
            }
            blob.CenterCell = bestCell;
        }

        // ------------------------------------------------------------ 次数

        public void Consume(TerrainBlob blob)
        {
            if (blob == null) return;
            blob.Charges = Mathf.Max(0, blob.Charges - 1);
            RefreshViews(blob);
        }

        public void Recharge(TerrainBlob blob)
        {
            if (blob == null) return;
            blob.Charges = Mathf.Min(blob.MaxCharges, blob.Charges + 1);
            RefreshViews(blob);
        }

        public bool OnCooldown(TerrainBlob blob)
            => blob != null && Time.frameCount - blob.LastReactFrame < ReactCooldownFrames;

        public void MarkReacted(TerrainBlob blob)
        {
            if (blob != null) blob.LastReactFrame = Time.frameCount;
        }

        /// <summary>
        /// 自动回充：每块地形独立计时，每 RechargeInterval 秒 +1 次，封顶 MaxCharges。
        /// 由 GameRuntime 每帧驱动。
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < _blobs.Count; i++)
            {
                var blob = _blobs[i];
                if (blob.Charges >= blob.MaxCharges) { blob.RechargeTimer = 0f; continue; }

                blob.RechargeTimer += dt;
                if (blob.RechargeTimer < blob.RechargeInterval) continue;

                blob.RechargeTimer -= blob.RechargeInterval;
                if (blob.RechargeTimer < 0f) blob.RechargeTimer = 0f;
                Recharge(blob);
            }
        }

        // ------------------------------------------------------------ 显示

        public void RefreshViews(TerrainBlob blob)
        {
            if (blob == null) return;

            bool depleted = blob.Depleted;
            bool realArt = Visuals.Has(blob.Key);

            // 配了 Terrain_Depleted 就在耗尽时换成那张图
            var sprite = blob.BaseSprite;
            var scale = blob.BaseScale;
            if (depleted)
            {
                var depletedSprite = Visuals.Get(VisualKey.Terrain_Depleted, null);
                if (depletedSprite != null)
                {
                    sprite = depletedSprite;
                    scale = Visuals.FitScale(depletedSprite, Vector2.one);
                }
            }

            bool alphaByCharges = Visuals.Set == null || Visuals.Set.TerrainAlphaByCharges;
            float alpha;
            if (!alphaByCharges) alpha = 1f;
            else if (depleted) alpha = DepletedColor.a;
            else
            {
                float t = blob.MaxCharges > 1 ? (blob.Charges - 1f) / (blob.MaxCharges - 1f) : 1f;
                alpha = Mathf.Lerp(0.16f, 0.52f, Mathf.Clamp01(t));
            }

            Color col;
            if (realArt) col = new Color(1f, 1f, 1f, alpha);   // 有美术不染色，只保留透明度反馈
            else col = depleted ? DepletedColor : blob.Element.ToColor();
            col.a = alpha;

            foreach (var kv in blob.Views)
            {
                var v = kv.Value;
                if (v == null) continue;
                v.sprite = sprite;
                v.color = col;
                v.transform.localScale = scale;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _blobs.Count; i++)
            {
                foreach (var kv in _blobs[i].Views)
                    if (kv.Value != null) Object.Destroy(kv.Value.gameObject);
                _blobs[i].Views.Clear();
                _blobs[i].Cells.Clear();
            }
            _blobs.Clear();
            _cellToBlob.Clear();
        }
    }
}
