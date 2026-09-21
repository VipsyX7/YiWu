using UnityEngine;

namespace Klein
{
    /// <summary>
    /// OnGUI 辅助。
    ///
    /// **不使用 `GUI.Button`**：本工程把 Active Input Handling 切到了新 Input System，
    /// IMGUI 的鼠标事件在这种情况下不可靠。这里统一用 Input System 读到的指针位置
    /// 自己做命中测试，保证菜单 / 背包 / 结算里的按钮一定能点。
    /// </summary>
    public static class KleinUi
    {
        private static readonly Vector2 Offscreen = new Vector2(-99999f, -99999f);

        public static readonly Color BtnNormal = new Color(0.16f, 0.18f, 0.24f, 1f);
        public static readonly Color BtnHover = new Color(0.26f, 0.32f, 0.44f, 1f);
        public static readonly Color BtnPressed = new Color(0.40f, 0.55f, 0.80f, 1f);

        /// <summary>
        /// 屏幕坐标 → GUI 坐标。Input System 的鼠标位置原点在**左下**，
        /// 而 IMGUI 的原点在**左上**，所以 y 必须翻转，否则所有按钮的点击都会上下镜像。
        /// </summary>
        public static Vector2 ScreenToGui(Vector2 screenPoint, float screenHeight)
            => new Vector2(screenPoint.x, screenHeight - screenPoint.y);

        /// <summary>指针在 GUI 坐标系（左上原点）里的位置。</summary>
        public static Vector2 GuiPointer()
        {
            var input = KleinInput.I;
            if (input == null) return Offscreen;
            return ScreenToGui(input.PointerScreen, Screen.height);
        }

        public static bool Hover(Rect r) => r.Contains(GuiPointer());

        public static bool SubmitHeld() => KleinInput.I != null && KleinInput.I.SubmitHeld();

        public static bool SubmitPressed() => KleinInput.I != null && KleinInput.I.SubmitPressed();

        /// <summary>命中测试的纯逻辑部分（可单独测）。</summary>
        public static bool Hit(Rect r, Vector2 guiPointer, bool submitPressed)
            => submitPressed && r.Contains(guiPointer);

        /// <summary>画一个按钮，返回"本帧是否被点击"。</summary>
        public static bool Button(Rect r, string label, GUIStyle style,
                                  Color normal, Color hover, Color pressed)
        {
            var pointer = GuiPointer();
            bool over = r.Contains(pointer);
            bool down = over && SubmitHeld();

            var prev = GUI.color;
            GUI.color = !over ? normal : (down ? pressed : hover);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(r.x + 8, r.y + (r.height - 22f) * 0.5f, r.width - 16, 22), label, style);

            return Hit(r, pointer, SubmitPressed());
        }

        /// <summary>画一个纯展示面板（不可点）。</summary>
        public static void Panel(Rect r, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
