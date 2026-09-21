using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    [System.Serializable]
    public class VisualEntry
    {
        public VisualKey Key;
        public Sprite Sprite;

        [Tooltip("勾选：按游戏内的碰撞/判定尺寸缩放（推荐）。取消：保持图片原始尺寸。")]
        public bool FitToSize = true;
    }

    /// <summary>
    /// 全局贴图表。**留空的项目自动使用运行时生成的占位色块**，
    /// 所以这个资产全空时游戏照样能跑。
    ///
    /// 换成正式美术只做两件事：
    ///   1. 把图放进 `Assets/_Project/Art/`，文件名 = VisualKey 名；
    ///   2. 执行菜单「Klein/从 Art 文件夹自动填充贴图」（或手动拖进 Inspector）。
    ///
    /// 一旦某个 key 配了图，该处**不再被占位色染色**（只保留 alpha，
    /// 因为透明度仍用来表达状态，例如地形剩余次数）。
    /// </summary>
    [CreateAssetMenu(menuName = "Klein/Sprite Set", fileName = "SpriteSet")]
    public class SpriteSet : ScriptableObject
    {
        public List<VisualEntry> Entries = new List<VisualEntry>();

        [Header("整体选项")]
        [Tooltip("墙体用 Tiled 平铺绘制。要求墙贴图的 Mesh Type = Full Rect，否则会报警告。")]
        public bool TileWalls = false;

        [Tooltip("地形按剩余反应次数调整透明度（配了真图也保留这个反馈）")]
        public bool TerrainAlphaByCharges = true;

        private Dictionary<VisualKey, VisualEntry> _map;

        private void BuildMap()
        {
            _map = new Dictionary<VisualKey, VisualEntry>();
            if (Entries == null) return;
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e == null || e.Key == VisualKey.None) continue;
                _map[e.Key] = e;
            }
        }

        private void OnEnable() => _map = null;
        private void OnValidate() => _map = null;

        public VisualEntry Entry(VisualKey key)
        {
            if (_map == null) BuildMap();
            return _map.TryGetValue(key, out var e) ? e : null;
        }

        public Sprite Get(VisualKey key)
        {
            var e = Entry(key);
            return e != null ? e.Sprite : null;
        }

        public bool Has(VisualKey key) => Get(key) != null;

        /// <summary>该 key 是否按目标尺寸缩放（没配图时恒为 true）。</summary>
        public bool Fits(VisualKey key)
        {
            var e = Entry(key);
            return e == null || e.FitToSize;
        }

        // ------------------------------------------------------------ 编辑用

        public void Set(VisualKey key, Sprite sprite, bool fitToSize = true)
        {
            if (key == VisualKey.None) return;
            var e = Entry(key);
            if (e == null)
            {
                e = new VisualEntry { Key = key };
                Entries.Add(e);
                _map = null;
            }
            e.Sprite = sprite;
            e.FitToSize = fitToSize;
        }

        public void Clear(VisualKey key)
        {
            var e = Entry(key);
            if (e == null) return;
            e.Sprite = null;
        }

        /// <summary>补齐所有 key 的空条目，方便在 Inspector 里一眼看全。</summary>
        public void EnsureAllKeys()
        {
            foreach (VisualKey k in System.Enum.GetValues(typeof(VisualKey)))
            {
                if (k == VisualKey.None) continue;
                if (Entry(k) == null)
                    Entries.Add(new VisualEntry { Key = k });
            }
            _map = null;
        }
    }
}
