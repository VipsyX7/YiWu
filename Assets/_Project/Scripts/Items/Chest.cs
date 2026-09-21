using UnityEngine;

namespace Klein
{
    /// <summary>宝箱房里的宝箱。免费开出一个当前道具池的道具。</summary>
    public class Chest : MonoBehaviour
    {
        private ItemDefinition _item;
        private bool _opened;
        private SpriteRenderer _sr;
        private float _pulse;

        public static Chest Create(Transform parent, Vector2 pos, ItemDefinition item)
        {
            var go = new GameObject("Chest");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var c = go.AddComponent<Chest>();
            c._item = item;
            c._sr = Make.Visual("Body", go.transform, Vector3.zero, VisualKey.Chest_Body,
                                SpriteFactory.Solid(Color.white), new Color(0.70f, 0.50f, 0.18f),
                                new Vector2(0.9f, 0.7f), 2);
            Make.Visual("Lid", go.transform, new Vector3(0f, 0.42f, 0f), VisualKey.Chest_Lid,
                        SpriteFactory.Solid(Color.white), new Color(0.85f, 0.65f, 0.25f),
                        new Vector2(0.95f, 0.16f), 2);
            WorldLabel.Attach(go, "宝箱", new Color(0.95f, 0.8f, 0.4f));
            return c;
        }

        private void Update()
        {
            if (_opened) return;
            _pulse += Time.deltaTime * 3f;
            if (_sr != null)
            {
                if (Visuals.Has(VisualKey.Chest_Body))
                {
                    // 有正式美术时不染色，只做轻微呼吸
                    _sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.78f, 1f, (Mathf.Sin(_pulse) + 1f) * 0.5f));
                }
                else
                {
                    _sr.color = Color.Lerp(new Color(0.70f, 0.50f, 0.18f),
                                           new Color(1f, 0.8f, 0.4f),
                                           (Mathf.Sin(_pulse) + 1f) * 0.5f);
                }
            }

            var rt = GameRuntime.I;
            var player = rt != null ? rt.Player : null;
            if (player == null) return;
            if (((Vector2)transform.position - (Vector2)player.transform.position).sqrMagnitude > 0.9f * 0.9f) return;

            _opened = true;
            var items = player.GetComponent<PlayerItems>();
            if (items != null) items.Acquire(_item);
            else if (player.Stats != null) player.Stats.ApplyItem(_item);

            if (rt != null) rt.ReportChestOpened(_item);
            Destroy(gameObject);
        }
    }
}
