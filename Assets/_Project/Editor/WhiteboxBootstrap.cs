#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Klein.EditorTools
{
    /// <summary>
    /// 工程自举工具（编辑器菜单 Klein/*）。
    /// 运行时本来就能用代码默认值跑起来，这个工具负责两件事：
    ///   1. 把默认数值落盘成 ScriptableObject 资产，方便在 Inspector 里调；
    ///   2. 生成三个场景（开始 / 选人 / 游戏）并写进 Build Settings。
    /// </summary>
    public static class WhiteboxBootstrap
    {
        private const string Root = "Assets/_Project";
        private const string ResRoot = Root + "/Resources/Klein";
        private const string SceneRoot = Root + "/Scenes";
        private const string ArtRoot = Root + "/Art";
        private const string SpriteSetPath = ResRoot + "/SpriteSet.asset";

        // ------------------------------------------------------------ 数据资产

        [MenuItem("Klein/生成数据资产", priority = 0)]
        public static void GenerateDataAssets()
        {
            EnsureFolder("Assets/_Project");
            EnsureFolder(Root + "/Resources");
            EnsureFolder(ResRoot);
            EnsureFolder(ResRoot + "/Enemies");
            EnsureFolder(ResRoot + "/Spells");
            EnsureFolder(ResRoot + "/SpellModifiers");
            EnsureFolder(ResRoot + "/Items");
            EnsureFolder(SceneRoot);

            WriteAsset(KleinDefaults.CreateConfig(), ResRoot + "/GameConfig.asset");
            WriteAsset(KleinDefaults.CreateReactionTable(), ResRoot + "/ReactionTable.asset");

            foreach (var e in KleinDefaults.CreateEnemies())
                WriteAsset(e, ResRoot + "/Enemies/" + Sanitize(e.name) + ".asset");

            var boss = KleinDefaults.CreateBoss();
            WriteAsset(boss, ResRoot + "/Enemies/" + Sanitize(boss.name) + ".asset");

            foreach (var s in KleinDefaults.CreateSpells())
                WriteAsset(s, ResRoot + "/Spells/" + Sanitize(s.name) + ".asset");

            foreach (var m in KleinDefaults.CreateModifiers())
                WriteAsset(m, ResRoot + "/SpellModifiers/" + Sanitize(m.name) + ".asset");

            foreach (var i in KleinDefaults.CreateItems())
                WriteAsset(i, ResRoot + "/Items/" + Sanitize(i.name) + ".asset");

            // 贴图表：保留已有的图，只补齐缺失的 key 条目
            EnsureFolder(ArtRoot);
            var set = LoadOrCreateSpriteSet();
            set.EnsureAllKeys();
            EditorUtility.SetDirty(set);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Klein] 数据资产生成完成 → " + ResRoot);
        }

        // ------------------------------------------------------------ 贴图表

        public static SpriteSet LoadOrCreateSpriteSet()
        {
            var set = AssetDatabase.LoadAssetAtPath<SpriteSet>(SpriteSetPath);
            if (set != null) return set;

            EnsureFolder(ResRoot);
            set = ScriptableObject.CreateInstance<SpriteSet>();
            set.EnsureAllKeys();
            AssetDatabase.CreateAsset(set, SpriteSetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Klein] 新建贴图表 → " + SpriteSetPath);
            return set;
        }

        /// <summary>
        /// 扫描 `Assets/_Project/Art/` 下所有 png，文件名与 VisualKey 同名的自动挂到贴图表上。
        /// 图片若没被导入成 Sprite，会自动改 TextureImporter 设置后重新导入。
        /// 这样"把图丢进 Art 文件夹"就能直接替换占位图。
        /// </summary>
        [MenuItem("Klein/从 Art 文件夹自动填充贴图", priority = 2)]
        public static void AutoFillSpritesFromArt()
        {
            EnsureFolder(ArtRoot);
            var set = LoadOrCreateSpriteSet();
            set.EnsureAllKeys();

            var keys = new Dictionary<string, VisualKey>();
            foreach (VisualKey k in System.Enum.GetValues(typeof(VisualKey)))
                keys[k.ToString()] = k;

            int filled = 0;
            var files = Directory.GetFiles(ArtRoot, "*.png", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!keys.TryGetValue(name, out var key)) continue;

                var path = file.Replace('\\', '/');
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                if (sprite == null)
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.SaveAndReimport();
                        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    }
                }

                if (sprite == null)
                {
                    Debug.LogWarning($"[Klein] {name}.png 无法作为 Sprite 读取，跳过");
                    continue;
                }

                set.Set(key, sprite, true);
                filled++;
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Klein] 自动填充贴图 {filled} 张（扫描 {ArtRoot}，文件名需等于 VisualKey 名）");
        }

        private static void WriteAsset(Object obj, string path)
        {
            var dir = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(dir);

            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);

            obj.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(obj, path);
        }

        private static string Sanitize(string n)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, leaf);
        }

        // ------------------------------------------------------------ 场景

        /// <summary>
        /// 生成三个场景：
        ///   Title     开始界面（点击「开始游戏」）
        ///   Select    选人界面（水 / 火 / 木 三选一）
        ///   Whitebox  游戏场景（GameBootstrap 在运行时生成全部内容）
        /// 并按顺序写进 Build Settings。
        /// </summary>
        [MenuItem("Klein/生成全部场景（开始/选人/游戏）", priority = 1)]
        public static void GenerateAllScenes()
        {
            EnsureFolder(Root);
            EnsureFolder(SceneRoot);
            EnsureFolder(ArtRoot);

            CreateScene(SceneNames.Title, "TitleScreen", typeof(TitleScreen));
            CreateScene(SceneNames.Select, "SelectScreen", typeof(ElementSelectScreen));
            CreateScene(SceneNames.Game, "Game", typeof(GameBootstrap), true);

            SetupBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Klein] 三个场景已生成 → " + SceneRoot);
        }

        private static void CreateScene(string sceneName, string goName, System.Type componentType,
                                        bool followCamera = false)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.06f, 0.075f);
            cam.orthographicSize = 7f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            if (followCamera) camGo.AddComponent<CameraFollow>();

            var go = new GameObject(goName);
            go.AddComponent(componentType);

            EditorSceneManager.SaveScene(scene, SceneRoot + "/" + sceneName + ".unity");
        }

        private static void SetupBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(SceneRoot + "/" + SceneNames.Title + ".unity", true),
                new EditorBuildSettingsScene(SceneRoot + "/" + SceneNames.Select + ".unity", true),
                new EditorBuildSettingsScene(SceneRoot + "/" + SceneNames.Game + ".unity", true),
            };
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------ 批处理入口

        /// <summary>
        /// 供 Unity 批处理调用：
        /// Unity.exe -batchmode -nographics -quit -projectPath &lt;proj&gt;
        ///   -executeMethod Klein.EditorTools.WhiteboxBootstrap.RunBatch
        /// </summary>
        public static void RunBatch()
        {
            Debug.Log("[Klein] RunBatch 开始");
            GenerateDataAssets();
            GenerateAllScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("KLEIN_BOOTSTRAP_OK");
        }
    }
}
#endif
