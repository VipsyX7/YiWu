using UnityEngine;

namespace Klein
{
    /// <summary>摄像机平滑跟随玩家（白盒不做房间切换/镜头锁定）。</summary>
    [DefaultExecutionOrder(100)]
    public class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float Smooth = 0.12f;
        private Vector3 _vel;

        private void LateUpdate()
        {
            if (Target == null)
            {
                var rt = GameRuntime.I;
                if (rt != null && rt.Player != null) Target = rt.Player.transform;
            }
            if (Target == null) return;

            var tp = Target.position;
            var want = new Vector3(tp.x, tp.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, want, ref _vel,
                                                    Mathf.Max(0.01f, Smooth));
        }
    }
}
