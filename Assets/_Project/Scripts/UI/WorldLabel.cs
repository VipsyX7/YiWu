using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 世界空间文字标签。白盒不引入 TextMeshPro/Canvas，
    /// 统一在 HUD 的 OnGUI 里投影绘制（见 HudController）。
    /// </summary>
    public class WorldLabel : MonoBehaviour
    {
        public string Text = "";
        public Color Color = Color.white;
        public float Size = 0.28f;

        private static readonly List<WorldLabel> _all = new List<WorldLabel>();
        public static IReadOnlyList<WorldLabel> All => _all;

        private void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        private void OnDisable() { _all.Remove(this); }

        public static WorldLabel Attach(GameObject go, string text, Color color, float size = 0.28f)
        {
            var l = go.GetComponent<WorldLabel>();
            if (l == null) l = go.AddComponent<WorldLabel>();
            l.Text = text;
            l.Color = color;
            l.Size = size;
            return l;
        }
    }
}
