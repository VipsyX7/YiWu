using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 移动相关的几何工具。
    /// 用来判断"玩家与鼠标点的目标之间有没有障碍物"，以决定是径直走过去还是绕路寻路。
    /// </summary>
    public static class NavUtil
    {
        /// <summary>视线采样步长（格）。越小越不容易漏掉细墙。</summary>
        public const float SampleStep = 0.15f;

        /// <summary>
        /// 从 from 到 to 之间是否存在无遮挡的直线通路。
        /// 除了中心线，还会向两侧各偏移一个"身体半径"再采一遍，
        /// 否则贴墙的擦边路径会被误判成通畅，结果人卡在墙角。
        /// 判定用的是 IsPassable —— 关着的门同样算障碍。
        /// </summary>
        public static bool HasLineOfSight(LevelGrid grid, Vector2 from, Vector2 to, float bodyRadius)
        {
            if (grid == null) return false;

            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 1e-4f) return grid.IsPassable(LevelGrid.WorldToCell(from));

            Vector2 dir = d / len;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / SampleStep));

            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                Vector2 p = from + d * t;

                if (!grid.IsPassable(LevelGrid.WorldToCell(p))) return false;

                if (bodyRadius > 0.01f)
                {
                    if (!grid.IsPassable(LevelGrid.WorldToCell(p + perp * bodyRadius))) return false;
                    if (!grid.IsPassable(LevelGrid.WorldToCell(p - perp * bodyRadius))) return false;
                }
            }
            return true;
        }
    }
}
