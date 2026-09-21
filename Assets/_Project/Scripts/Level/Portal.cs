using UnityEngine;

namespace Klein
{
    /// <summary>
    /// BOSS 房传送门。策划案 v2：「在 BOSS 房打败 BOSS 后，生成一个传送门，
    /// 进入传送门来到下一层」。
    /// </summary>
    public class Portal : MonoBehaviour
    {
        private int _nextFloor;
        private float _spin;
        private SpriteRenderer _ring;

        public static Portal Create(Transform parent, Vector2 pos, int nextFloor)
        {
            var go = new GameObject("Portal");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var p = go.AddComponent<Portal>();
            p._nextFloor = nextFloor;

            Make.Visual("Core", go.transform, Vector3.zero, VisualKey.Portal_Core,
                        SpriteFactory.Circle(Color.white), new Color(0.6f, 0.3f, 0.9f, 0.75f),
                        new Vector2(1.2f, 1.2f), 3);
            p._ring = Make.Visual("Ring", go.transform, Vector3.zero, VisualKey.Portal_Ring,
                                  SpriteFactory.Ring(Color.white), new Color(0.85f, 0.6f, 1f, 0.9f),
                                  new Vector2(1.7f, 1.7f), 4);

            WorldLabel.Attach(go, "→ 第 " + nextFloor + " 层", new Color(0.85f, 0.6f, 1f));
            return p;
        }

        private void Update()
        {
            _spin += Time.deltaTime;
            if (_ring != null)
            {
                float s = 1.7f + Mathf.Sin(_spin * 2f) * 0.12f;
                _ring.transform.localScale = Visuals.ScaleFor(VisualKey.Portal_Ring, _ring.sprite,
                                                              new Vector2(s, s));
                _ring.transform.localRotation = Quaternion.Euler(0f, 0f, _spin * 40f);
            }

            var rt = GameRuntime.I;
            var player = rt != null ? rt.Player : null;
            if (player == null) return;
            if (((Vector2)transform.position - (Vector2)player.transform.position).sqrMagnitude > 0.7f * 0.7f) return;

            rt.NextFloor();
        }
    }
}
