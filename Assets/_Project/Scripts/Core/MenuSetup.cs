using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 菜单场景（开始 / 选人）的最小启动：一个正交摄像机 + 输入层。
    /// 菜单里不需要建局，所以不创建 GameRuntime。
    /// </summary>
    public static class MenuSetup
    {
        public static void Ensure()
        {
            EnsureCamera();
            EnsureInput();
        }

        private static void EnsureCamera()
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
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
        }

        private static void EnsureInput()
        {
            if (KleinInput.I != null) return;
            var go = new GameObject("KleinInput");
            go.AddComponent<KleinInput>();
        }
    }
}
