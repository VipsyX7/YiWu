using UnityEngine;
using UnityEngine.SceneManagement;

namespace Klein
{
    /// <summary>
    /// 游戏开始场景。标题 + 「开始游戏」按钮，点击进入选人场景。
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        private GUIStyle _title, _sub, _btn, _hint;
        private bool _styled;
        private float _time;
        private bool _leaving;

        private void Awake()
        {
            MenuSetup.Ensure();
        }

        private void Update()
        {
            _time += Time.deltaTime;
        }

        private void EnsureStyle()
        {
            if (_styled) return;
            _styled = true;

            var font = UiFont.Get();
            _title = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _sub = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            _btn = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            if (font != null)
            {
                _title.font = font; _sub.font = font; _btn.font = font; _hint.font = font;
            }
            _title.normal.textColor = new Color(0.85f, 0.90f, 1f);
            _sub.normal.textColor = new Color(0.60f, 0.66f, 0.78f);
            _btn.normal.textColor = Color.white;
            _hint.normal.textColor = new Color(0.55f, 0.58f, 0.66f);
        }

        private void OnGUI()
        {
            EnsureStyle();

            float w = Screen.width, h = Screen.height;

            var prev = GUI.color;
            GUI.color = new Color(0.10f, 0.14f, 0.24f, 0.55f + Mathf.Sin(_time * 1.2f) * 0.06f);
            GUI.DrawTexture(new Rect(w * 0.5f - 260f, h * 0.5f - 210f, 520f, 420f), Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(0, h * 0.5f - 190f, w, 80), "KLEIN", _title);
            GUI.Label(new Rect(0, h * 0.5f - 100f, w, 30), "元素 Roguelike · 白盒原型", _sub);

            var btn = new Rect(w * 0.5f - 130f, h * 0.5f + 10f, 260f, 56f);
            if (KleinUi.Button(btn, "开 始 游 戏", _btn, KleinUi.BtnNormal, KleinUi.BtnHover, KleinUi.BtnPressed))
                BeginGame();

            GUI.Label(new Rect(0, h * 0.5f + 96f, w, 24),
                      GameSession.HasSelection
                          ? "上次选择：" + GameSession.SelectedElement.ToCn() + "元素"
                          : "尚未选择元素", _hint);

            GUI.Label(new Rect(0, h - 34f, w, 24),
                      "进入游戏后：右键点地移动 · QWEASD 施法 · F1 切换元素 · R 重建本层", _hint);
        }

        /// <summary>进入选人场景。UI 点击与测试都走这里。</summary>
        public void BeginGame()
        {
            if (_leaving) return;
            _leaving = true;
            SceneManager.LoadScene(SceneNames.Select);
        }
    }
}
