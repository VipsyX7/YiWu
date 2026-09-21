using UnityEngine;
using UnityEngine.InputSystem;

namespace Klein
{
    /// <summary>
    /// 输入层。全部 InputAction 在代码里构建，不依赖 .inputactions 资产，
    /// 这样批处理/首次运行都不会因为缺资产而失败。
    /// 对应策划案「控制系统」：右键点地移动、空格用主动道具、Shift 切换、B 开背包、
    /// QWEASD 六个法术槽。**注意：WASD 不再控制移动。**
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class KleinInput : MonoBehaviour
    {
        public static KleinInput I { get; private set; }

        public const int SlotCount = 6;
        /// <summary>六个法术槽的按键名（QWEASD）。对外只暴露字符串，避免泄漏 InputSystem 类型。</summary>
        public static readonly string[] SlotKeyNames = { "Q", "W", "E", "A", "S", "D" };
        private static readonly Key[] SlotKeys = { Key.Q, Key.W, Key.E, Key.A, Key.S, Key.D };

        private InputAction[] _cast = new InputAction[SlotCount];
        private InputAction _useItem;
        private InputAction _switchItem;
        private InputAction _spellbook;
        private InputAction _pointer;
        private InputAction _moveCommand;
        private InputAction _cancel;
        private InputAction _debugKey;
        private InputAction _reload;
        private InputAction _submit;

        /// <summary>已按需求移除 WASD 移动轴：角色走位完全由鼠标右键点地驱动。</summary>
        public Vector2 PointerScreen { get; private set; }
        public Vector3 PointerWorld { get; private set; }

        private Camera _cam;

        private void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            BuildActions();
        }

        private void BuildActions()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _cast[i] = new InputAction("Cast" + i, InputActionType.Button);
                _cast[i].AddBinding("<Keyboard>/" + SlotKeyNames[i].ToLowerInvariant());
            }

            _useItem = new InputAction("UseItem", InputActionType.Button, "<Keyboard>/space");
            _switchItem = new InputAction("SwitchItem", InputActionType.Button, "<Keyboard>/leftShift");
            _spellbook = new InputAction("Spellbook", InputActionType.Button, "<Keyboard>/b");
            _cancel = new InputAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            _pointer = new InputAction("Pointer", InputActionType.Value, "<Mouse>/position");
            _moveCommand = new InputAction("MoveCommand", InputActionType.Button, "<Mouse>/rightButton");
            _debugKey = new InputAction("Debug", InputActionType.Button, "<Keyboard>/f1");
            _reload = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");

            // 菜单 / 背包 / 结算的"确认"。左键点击 + 回车。
            _submit = new InputAction("Submit", InputActionType.Button);
            _submit.AddBinding("<Mouse>/leftButton");
            _submit.AddBinding("<Keyboard>/enter");
            _submit.AddBinding("<Keyboard>/numpadEnter");

            foreach (var a in _cast) a.Enable();
            _useItem.Enable(); _switchItem.Enable(); _spellbook.Enable();
            _cancel.Enable(); _pointer.Enable(); _moveCommand.Enable();
            _debugKey.Enable(); _reload.Enable(); _submit.Enable();
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;

            PointerScreen = _pointer.ReadValue<Vector2>();
            if (_cam != null)
            {
                var w = _cam.ScreenToWorldPoint(new Vector3(PointerScreen.x, PointerScreen.y, -_cam.transform.position.z));
                PointerWorld = new Vector3(w.x, w.y, 0f);
            }
        }

        private void OnDestroy()
        {
            if (I != this) return;
            foreach (var a in _cast) a?.Dispose();
            _useItem?.Dispose(); _switchItem?.Dispose(); _spellbook?.Dispose();
            _cancel?.Dispose(); _pointer?.Dispose(); _moveCommand?.Dispose();
            _debugKey?.Dispose(); _reload?.Dispose(); _submit?.Dispose();
            I = null;
        }

        public bool CastPressed(int slot) => slot >= 0 && slot < SlotCount && _cast[slot].WasPressedThisFrame();
        public bool UseItemPressed() => _useItem.WasPressedThisFrame();
        public bool SwitchItemPressed() => _switchItem.WasPressedThisFrame();
        public bool SpellbookPressed() => _spellbook.WasPressedThisFrame();
        public bool CancelPressed() => _cancel.WasPressedThisFrame();
        public bool MoveCommandPressed() => _moveCommand.WasPressedThisFrame();
        public bool DebugPressed() => _debugKey.WasPressedThisFrame();
        public bool ReloadPressed() => _reload.WasPressedThisFrame();

        /// <summary>菜单/背包里的"确认"本帧是否按下。</summary>
        public bool SubmitPressed() => _submit.WasPressedThisFrame();
        /// <summary>"确认"是否处于按住状态（用于按钮按下态）。</summary>
        public bool SubmitHeld() => _submit.IsPressed();

        /// <summary>绘制调试/游戏内 UI 时用于让 UI 吃掉输入。</summary>
        public static bool UiCaptures { get; set; }
    }
}
