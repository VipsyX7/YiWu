using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 场景唯一入口。所有内容（占位贴图、数据、玩家、关卡、HUD）都在运行时生成，
    /// 因此场景里只需要挂这一个组件。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("勾选则忽略 Resources 里的数据资产，直接用代码默认值")]
        public bool ForceCodeDefaults = false;

        [Tooltip("开局时随机种子")]
        public bool RandomizeSeed = true;

        public static GameBootstrap Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            var db = GameDatabase.I ?? GameDatabase.LoadOrCreate(ForceCodeDefaults);
            Debug.Log($"[Klein] 数据加载完成 · 资产模式={db.LoadedFromAssets} · "
                      + $"怪物{db.Enemies.Count} 法术{db.Spells.Count} 修正器{db.Modifiers.Count} 道具{db.Items.Count}");

            EnsureCamera();
            EnsureComponent<KleinInput>("KleinInput");
            EnsureComponent<HudController>("KleinHud");

            if (GameRuntime.I == null)
                gameObject.AddComponent<GameRuntime>();
        }

        private void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.06f, 0.075f);
            cam.orthographicSize = GameDatabase.I != null ? GameDatabase.I.Config.CameraSize : 7f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;

            if (cam.GetComponent<CameraFollow>() == null)
                cam.gameObject.AddComponent<CameraFollow>();
        }

        private T EnsureComponent<T>(string goName) where T : Component
        {
            var existing = Object.FindFirstObjectByType<T>();
            if (existing != null) return existing;

            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }
    }
}
