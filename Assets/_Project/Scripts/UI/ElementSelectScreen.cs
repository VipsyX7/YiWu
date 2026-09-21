using UnityEngine;
using UnityEngine.SceneManagement;

namespace Klein
{
    /// <summary>
    /// 选人场景：从水 / 火 / 木三选一本命元素。
    /// 选中后写入 <see cref="GameSession"/> 并切到游戏场景。
    /// </summary>
    public class ElementSelectScreen : MonoBehaviour
    {
        private struct Card
        {
            public ElementType Element;
            public string Title;
            public string[] Lines;
        }

        private static readonly Card[] Cards =
        {
            new Card
            {
                Element = ElementType.Water, Title = "水",
                Lines = new[]
                {
                    "法术附带水元素",
                    "水 × 火地形 → 穿透，伤害翻倍",
                    "水 × 木地形 → 腐烂区域，减速敌人",
                },
            },
            new Card
            {
                Element = ElementType.Fire, Title = "火",
                Lines = new[]
                {
                    "法术附带火元素",
                    "火 × 水地形 → 蒸汽区域，持续伤害",
                    "火 × 木地形 → 爆炸，一次性高伤",
                },
            },
            new Card
            {
                Element = ElementType.Wood, Title = "木",
                Lines = new[]
                {
                    "法术附带木元素",
                    "木 × 水地形 → 回血区域",
                    "木 × 火地形 → 穿透 + 灼伤 5%/s",
                },
            },
        };

        private GUIStyle _title, _cardTitle, _line, _btn, _hint;
        private bool _styled;
        private float _time;

        private bool _chosen;
        private float _loadTimer = -1f;
        private ElementType _chosenElement;
        private int _hoverIndex = -1;

        private const float LoadDelay = 0.4f;

        private void Awake()
        {
            MenuSetup.Ensure();
        }

        private void Update()
        {
            _time += Time.deltaTime;

            if (_loadTimer >= 0f)
            {
                _loadTimer -= Time.deltaTime;
                if (_loadTimer <= 0f)
                {
                    _loadTimer = -1f;
                    SceneManager.LoadScene(SceneNames.Game);
                }
            }
        }

        private void EnsureStyle()
        {
            if (_styled) return;
            _styled = true;

            var font = UiFont.Get();
            _title = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _cardTitle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _line = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true };
            _btn = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            if (font != null)
            {
                _title.font = font; _cardTitle.font = font; _line.font = font;
                _btn.font = font; _hint.font = font;
            }
            _title.normal.textColor = new Color(0.85f, 0.90f, 1f);
            _cardTitle.normal.textColor = Color.white;
            _line.normal.textColor = new Color(0.80f, 0.84f, 0.90f);
            _btn.normal.textColor = Color.white;
            _hint.normal.textColor = new Color(0.55f, 0.58f, 0.66f);
        }

        private void OnGUI()
        {
            EnsureStyle();

            float w = Screen.width, h = Screen.height;
            GUI.Label(new Rect(0, 40f, w, 44f), "选择你的本命元素", _title);
            GUI.Label(new Rect(0, 86f, w, 24f),
                      "在祭祀场触碰水晶球之前，先决定你要成为哪一种魔法使。", _hint);

            const float cardW = 280f, cardH = 300f, gap = 28f;
            float totalW = Cards.Length * cardW + (Cards.Length - 1) * gap;
            float x0 = (w - totalW) * 0.5f;
            float y = h * 0.5f - cardH * 0.5f;

            _hoverIndex = -1;
            for (int i = 0; i < Cards.Length; i++)
            {
                var rect = new Rect(x0 + i * (cardW + gap), y, cardW, cardH);
                if (DrawCard(i, rect)) Choose(Cards[i].Element);
            }

            var back = new Rect(w * 0.5f - 90f, h - 84f, 180f, 44f);
            if (KleinUi.Button(back, "返回", _btn, KleinUi.BtnNormal, KleinUi.BtnHover, KleinUi.BtnPressed)
                && !_chosen)
            {
                SceneManager.LoadScene(SceneNames.Title);
            }

            if (_chosen)
            {
                GUI.Label(new Rect(0, h - 116f, w, 24f),
                          "已选择：" + _chosenElement.ToCn() + "元素 — 进入异空间…", _hint);
            }
        }

        /// <summary>画一张元素卡，返回是否被点击。</summary>
        private bool DrawCard(int index, Rect rect)
        {
            var card = Cards[index];
            var color = card.Element.ToColor();

            bool over = KleinUi.Hover(rect);
            if (over) _hoverIndex = index;

            bool selected = _chosen && _chosenElement == card.Element;
            float boost = over ? 8f : 0f;
            var r = new Rect(rect.x - boost * 0.5f, rect.y - boost * 0.5f, rect.width + boost, rect.height + boost);

            var bg = selected ? new Color(color.r, color.g, color.b, 0.55f)
                     : over ? new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 0.95f)
                            : new Color(0.11f, 0.13f, 0.17f, 0.92f);
            KleinUi.Panel(r, bg);

            // 元素圆点
            var dot = new Rect(r.x + r.width * 0.5f - 42f, r.y + 26f, 84f, 84f);
            KleinUi.Panel(dot, new Color(color.r, color.g, color.b, selected ? 1f : 0.85f));

            GUI.Label(new Rect(r.x, r.y + 118f, r.width, 48f), card.Title, _cardTitle);

            for (int i = 0; i < card.Lines.Length; i++)
            {
                GUI.Label(new Rect(r.x + 22f, r.y + 176f + i * 38f, r.width - 44f, 36f),
                          "· " + card.Lines[i], _line);
            }

            if (over && !_chosen)
                KleinUi.Panel(new Rect(r.x, r.y + r.height - 26f, r.width, 26f),
                              new Color(color.r, color.g, color.b, 0.75f));

            return over && KleinUi.SubmitPressed() && !_chosen;
        }

        /// <summary>选择元素并进入游戏场景。UI 点击与测试都走这里。</summary>
        public void Choose(ElementType element)
        {
            if (_chosen) return;
            _chosen = true;
            _chosenElement = element;
            GameSession.Select(element);
            _loadTimer = LoadDelay;   // 稍等一下，让选择高亮能被看到
        }

        /// <summary>是否已经完成选择（用于测试观察）。</summary>
        public bool HasChosen => _chosen;
    }
}
