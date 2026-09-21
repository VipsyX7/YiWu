using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 白盒 HUD。全部走 OnGUI，不依赖 Canvas / TextMeshPro / EventSystem，
    /// 这样空工程也能直接显示。包含：
    ///  · 玩家状态（血量/法力/金钱/元素）
    ///  · 六个法术槽与冷却
    ///  · 房间/层信息
    ///  · 元素反应验收面板（白盒核心验收依据）
    ///  · 法术背包（B 键）
    ///  · 世界空间标签
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class HudController : MonoBehaviour
    {
        private bool _spellbook;
        private int _selectedInventory = -1;
        private GUIStyle _text, _small, _bold, _title, _btn;
        private bool _styled;
        private Texture2D _white;

        private static readonly Color Panel = new Color(0.05f, 0.06f, 0.09f, 0.78f);
        private static readonly Color Accent = new Color(0.55f, 0.80f, 1f, 1f);
        private static readonly Color Warn = new Color(1f, 0.45f, 0.45f, 1f);
        private static readonly Color Good = new Color(0.45f, 1f, 0.6f, 1f);

        private void Update()
        {
            var input = KleinInput.I;
            if (input == null) return;

            if (input.SpellbookPressed()) _spellbook = !_spellbook;
            if (input.CancelPressed()) { _spellbook = false; _selectedInventory = -1; }

            // 背包打开、或者已经结算时，吃掉游戏内输入
            var rt = GameRuntime.I;
            KleinInput.UiCaptures = _spellbook || (rt != null && rt.GameOver);
        }

        private void EnsureStyle()
        {
            if (_styled) return;
            _styled = true;
            _white = Texture2D.whiteTexture;

            var font = UiFont.Get();
            _text = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = false };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            _bold = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _btn = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            if (font != null)
            {
                _text.font = font; _small.font = font; _bold.font = font;
                _title.font = font; _btn.font = font;
            }
            _text.normal.textColor = Color.white;
            _small.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
            _bold.normal.textColor = Color.white;
            _title.normal.textColor = Accent;
            _btn.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            EnsureStyle();
            var rt = GameRuntime.I;
            if (rt == null) return;

            GUI.color = Panel;
            GUI.DrawTexture(new Rect(0, 0, 300, 216), _white);
            GUI.color = Color.white;

            var player = rt.Player;
            if (player != null && player.Stats != null)
            {
                var s = player.Stats;
                var h = player.Health;

                GUI.Label(new Rect(10, 6, 280, 22), "Klein 白盒  ·  " + s.Element.ToCn() + "属性", _title);

                float hp = h != null ? h.Normalized : 0f;
                Bar(new Rect(10, 34, 280, 14), hp, new Color(0.9f, 0.25f, 0.3f));
                GUI.Label(new Rect(14, 33, 280, 16),
                          "HP  " + Mathf.CeilToInt(h != null ? h.Health : 0) + " / " + Mathf.RoundToInt(s.MaxHealth), _small);

                Bar(new Rect(10, 52, 280, 14), s.MaxMana > 0f ? s.Mana / s.MaxMana : 0f,
                    new Color(0.25f, 0.5f, 0.95f));
                GUI.Label(new Rect(14, 51, 280, 16),
                          "MP  " + Mathf.FloorToInt(s.Mana) + " / " + Mathf.RoundToInt(s.MaxMana), _small);

                GUI.Label(new Rect(10, 70, 280, 18),
                          "金钱 " + s.Money + "    移速 " + s.MoveSpeed.ToString("0.0")
                          + "    攻击 " + s.AttackPower.ToString("0") + "%", _text);
                GUI.Label(new Rect(10, 88, 280, 18),
                          "冷却 " + s.SpellCooldown.ToString("0") + "%    施法距离 " + s.CastRange.ToString("0.0")
                          + "    飞速 " + s.SpellSpeed.ToString("0.0"), _text);
            }

            GUI.Label(new Rect(10, 108, 280, 18),
                      "第 " + rt.FloorIndex + " / " + rt.Cfg.FloorCount + " 层    "
                      + (rt.Floor != null ? rt.Floor.WallRunCount + " 墙段" : ""), _text);

            var room = rt.CurrentRoom;
            GUI.Label(new Rect(10, 126, 280, 18),
                      "当前房间：" + (room != null && room.Node != null
                          ? room.Node.Type.ToCn() + (room.IsCleared ? " (已清)" : " (剩 " + room.AliveCount + ")")
                          : "走廊"), _text);

            GUI.Label(new Rect(10, 144, 280, 18),
                      "地形 " + rt.Elements.BlobCount + " 块 / " + rt.Elements.CellCount + " 格"
                      + "    反应 " + rt.ReactionCount + "    击杀 " + rt.KillCount, _text);

            // 脚下这块地形的剩余反应次数（整块共享）
            var under = player != null ? rt.Elements.GetBlobAt(player.transform.position) : null;
            GUI.Label(new Rect(10, 162, 280, 18),
                      under != null
                          ? "脚下地形：" + under.Element.ToCn() + " "
                            + under.Charges + "/" + under.MaxCharges + " 次（每 "
                            + under.RechargeInterval.ToString("0.#") + "s +1）"
                          : "脚下地形：无", under != null ? _text : _small);

            GUI.Label(new Rect(10, 182, 290, 16),
                      "右键移动（无障碍直行 / 有障碍寻路）· QWEASD 施法 · 空格 道具 · Shift 切换",
                      _small);
            GUI.Label(new Rect(10, 198, 290, 16),
                      "B 法术背包 · F1 切换元素 · R 重建本层",
                      _small);

            DrawSlots(rt);
            DrawReactionPanel(rt);
            DrawLog(rt);
            DrawWorldLabels();

            if (rt.GameOver) DrawGameOver(rt);
            if (_spellbook) DrawSpellbook(rt);
        }

        // ------------------------------------------------------------ 组件

        private void Bar(Rect r, float t, Color fill)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(r, _white);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), _white);
            GUI.color = Color.white;
        }

        private void DrawSlots(GameRuntime rt)
        {
            var player = rt.Player;
            if (player == null || player.Caster == null) return;

            const float w = 96f, h = 46f, gap = 6f;
            float total = SpellCaster.SlotCount * w + (SpellCaster.SlotCount - 1) * gap;
            float x0 = (Screen.width - total) * 0.5f;
            float y = Screen.height - h - 14f;

            for (int i = 0; i < SpellCaster.SlotCount; i++)
            {
                var card = player.Caster.Slots[i];
                var r = new Rect(x0 + i * (w + gap), y, w, h);

                GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.82f);
                GUI.DrawTexture(r, _white);
                GUI.color = Color.white;

                float cd = player.Caster.Cooldown01(i);
                if (cd > 0f)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * Mathf.Clamp01(cd)), _white);
                    GUI.color = Color.white;
                }

                string key = KleinInput.SlotKeyNames[i];
                string label = card != null && !card.IsEmpty ? card.Label() : "空";
                GUI.Label(new Rect(r.x + 6, r.y + 4, w - 12, 18), key + "  " + label, _bold);
                GUI.Label(new Rect(r.x + 6, r.y + 24, w - 12, 16),
                          card != null && !card.IsEmpty && card.Spell != null
                              ? card.Spell.Damage.ToString("0") + " dmg" : "", _small);
            }
        }

        private void DrawReactionPanel(GameRuntime rt)
        {
            const float w = 300f;
            float x = Screen.width - w - 12f;
            float y = 12f;
            float h = 214f;

            GUI.color = Panel;
            GUI.DrawTexture(new Rect(x, y, w, h), _white);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 10, y + 6, w - 20, 22), "元素反应验收（v2 矩阵）", _bold);
            GUI.Label(new Rect(x + 10, y + 26, w - 20, 16),
                      "地形格反应次数 3（上限 5）· 同格每 60 帧 1 次", _small);

            string[] names =
            {
                "水×火 穿透×2", "水×木 腐烂减速", "火×水 蒸汽区",
                "火×木 爆炸", "木×水 回血区", "木×火 穿透+灼伤",
                "同元素 次数+1",
            };
            ReactionKind[] kinds =
            {
                ReactionKind.Pierce, ReactionKind.RotArea, ReactionKind.SteamArea,
                ReactionKind.Explosion, ReactionKind.HealArea, ReactionKind.PierceBurn,
                ReactionKind.Recharge,
            };

            for (int i = 0; i < names.Length; i++)
            {
                int n = rt.ReactionTally.TryGetValue(kinds[i], out var v) ? v : 0;
                GUI.Label(new Rect(x + 10, y + 46 + i * 17, w - 20, 16),
                          (n > 0 ? "✔ " : "· ") + names[i] + "   ×" + n,
                          n > 0 ? _text : _small);
            }

            GUI.Label(new Rect(x + 10, y + 46 + names.Length * 17 + 2, w - 20, 18),
                      "折射镜 " + rt.RefractCount + "  ·  施法 " + rt.CastCount
                      + "  ·  购买 " + rt.PurchaseCount, _small);
        }

        private void DrawLog(GameRuntime rt)
        {
            const float w = 340f;
            float x = Screen.width - w - 12f;
            float h = 20 + rt.EventLog.Count * 16f;
            float y = Screen.height - h - 66f;

            GUI.color = Panel;
            GUI.DrawTexture(new Rect(x, y, w, h), _white);
            GUI.color = Color.white;

            for (int i = 0; i < rt.EventLog.Count; i++)
                GUI.Label(new Rect(x + 8, y + 2 + i * 16, w - 16, 16), rt.EventLog[i], _small);
        }

        private void DrawWorldLabels()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var labels = WorldLabel.All;
            for (int i = 0; i < labels.Count; i++)
            {
                var l = labels[i];
                if (l == null) continue;
                var sp = cam.WorldToScreenPoint(l.transform.position);
                if (sp.z < 0f) continue;
                var pos = new Vector2(sp.x, Screen.height - sp.y - 22f);
                var style = _small;
                var size = style.CalcSize(new GUIContent(l.Text));
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(pos.x - size.x * 0.5f - 4, pos.y - 2, size.x + 8, size.y + 4), _white);
                GUI.color = l.Color;
                GUI.Label(new Rect(pos.x - size.x * 0.5f, pos.y, size.x, size.y), l.Text, style);
                GUI.color = Color.white;
            }
        }

        private void DrawGameOver(GameRuntime rt)
        {
            float w = 440f, h = 186f;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            KleinUi.Panel(r, new Color(0f, 0f, 0f, 0.88f));

            GUI.Label(new Rect(r.x + 20, r.y + 16, w - 40, 30),
                      rt.Victory ? "通关！" : "单局失败", _title);
            GUI.Label(new Rect(r.x + 20, r.y + 54, w - 40, 20),
                      "推进层数 " + rt.FloorsCleared + "  ·  击杀 " + rt.KillCount
                      + "  ·  反应 " + rt.ReactionCount, _text);
            GUI.Label(new Rect(r.x + 20, r.y + 78, w - 40, 20),
                      "白盒结算占位：法术点 = 通过层数 × 10 = " + (rt.FloorsCleared * 10), _text);

            var again = new Rect(r.x + 20, r.y + 118, 180, 40);
            if (KleinUi.Button(again, "再来一局 (R)", _btn, KleinUi.BtnNormal, KleinUi.BtnHover, KleinUi.BtnPressed))
                rt.RequestRestart();

            var back = new Rect(r.x + 220, r.y + 118, 200, 40);
            if (KleinUi.Button(back, "返回开始界面", _btn, KleinUi.BtnNormal, KleinUi.BtnHover, KleinUi.BtnPressed))
                rt.ReturnToTitle();
        }

        private void DrawSpellbook(GameRuntime rt)
        {
            var player = rt.Player;
            if (player == null || player.Caster == null) return;
            var caster = player.Caster;

            float w = 640f, h = 360f;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.94f);
            GUI.DrawTexture(panel, _white);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + 16, panel.y + 10, w - 32, 26),
                      "法术背包  (B 关闭)", _title);
            GUI.Label(new Rect(panel.x + 16, panel.y + 36, w - 32, 18),
                      "先点右侧背包里的法术，再点左侧槽位装配；未选中时点槽位＝清空。", _small);

            // 左：6 个槽
            GUI.Label(new Rect(panel.x + 16, panel.y + 62, 200, 20), "槽位", _bold);
            for (int i = 0; i < SpellCaster.SlotCount; i++)
            {
                var card = caster.Slots[i];
                var r = new Rect(panel.x + 16, panel.y + 86 + i * 42, 280, 36);
                string label = KleinInput.SlotKeyNames[i] + "  " + (card != null ? card.Label() : "空");

                if (KleinUi.Button(r, label, _btn, KleinUi.BtnNormal, KleinUi.BtnHover, KleinUi.BtnPressed))
                {
                    if (_selectedInventory >= 0)
                    {
                        caster.AssignSlot(i, _selectedInventory);
                        _selectedInventory = -1;
                    }
                    else
                    {
                        caster.ClearSlot(i);
                    }
                }
            }

            // 右：背包
            GUI.Label(new Rect(panel.x + 320, panel.y + 62, 300, 20),
                      "背包（" + caster.Inventory.Count + "）", _bold);

            for (int i = 0; i < caster.Inventory.Count && i < 7; i++)
            {
                var r = new Rect(panel.x + 320, panel.y + 86 + i * 34, 300, 30);
                var normal = i == _selectedInventory
                    ? new Color(0.25f, 0.65f, 0.40f, 1f)
                    : KleinUi.BtnNormal;
                if (KleinUi.Button(r, caster.Inventory[i].Label(), _btn, normal, KleinUi.BtnHover, KleinUi.BtnPressed))
                {
                    _selectedInventory = i == _selectedInventory ? -1 : i;
                }
            }

            if (caster.Inventory.Count == 0)
                GUI.Label(new Rect(panel.x + 320, panel.y + 86, 300, 20), "（去法术房捡法术）", _small);
        }
    }
}
