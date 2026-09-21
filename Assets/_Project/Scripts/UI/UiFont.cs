using UnityEngine;

namespace Klein
{
    /// <summary>
    /// OnGUI 用的中文字体。Unity 内置字体不含 CJK 字形，
    /// 若不换字体，所有中文标签都会变成豆腐块。
    /// </summary>
    public static class UiFont
    {
        private static Font _font;
        private static bool _tried;

        private static readonly string[] Prefer =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
            "SimHei", "黑体", "SimSun", "宋体",
            "Noto Sans CJK SC", "Source Han Sans SC", "PingFang SC",
        };

        public static Font Get()
        {
            if (_tried) return _font;
            _tried = true;

            string[] installed = null;
            try { installed = Font.GetOSInstalledFontNames(); }
            catch { installed = null; }

            if (installed != null)
            {
                foreach (var want in Prefer)
                {
                    foreach (var has in installed)
                    {
                        if (string.Equals(has, want, System.StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                _font = Font.CreateDynamicFontFromOSFont(has, 16);
                                if (_font != null) return _font;
                            }
                            catch { }
                        }
                    }
                }
            }

            // 兜底：把整份偏好列表丢给 Unity 自己挑
            try { _font = Font.CreateDynamicFontFromOSFont(Prefer, 16); }
            catch { _font = null; }
            return _font;
        }

        /// <summary>是否成功换到了（可能）含 CJK 的字体。</summary>
        public static bool HasCjkFont => Get() != null;
    }
}
