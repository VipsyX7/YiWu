using UnityEngine;

namespace Klein
{
    /// <summary>商店房里的道具基座。走近并付得起钱即可购买。</summary>
    public class ItemPedestal : MonoBehaviour
    {
        private ItemDefinition _item;
        private float _price;
        private SpriteRenderer _sr;
        private bool _sold;
        private float _bob;

        public static ItemPedestal Create(Transform parent, Vector2 pos, ItemDefinition item, float price)
        {
            var go = new GameObject("Pedestal_" + (item != null ? item.DisplayName : "?"));
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var p = go.AddComponent<ItemPedestal>();
            p._item = item;
            p._price = price;

            var color = item != null ? item.Color : Color.gray;
            Make.Visual("Base", go.transform, new Vector3(0f, -0.4f, 0f), VisualKey.Pedestal_Base,
                        SpriteFactory.Solid(Color.white), new Color(0.35f, 0.32f, 0.30f),
                        new Vector2(0.9f, 0.25f), 1);

            // 逐道具贴图 > 全局 Item_Default
            if (item != null && item.IconSprite != null)
                p._sr = Make.Preview("Item", go.transform, item.IconSprite, new Vector2(0.6f, 0.6f), 2);
            else
                p._sr = Make.Visual("Item", go.transform, Vector3.zero, VisualKey.Item_Default,
                                    SpriteFactory.Circle(Color.white), color,
                                    new Vector2(0.6f, 0.6f), 2);
            WorldLabel.Attach(go, (item != null ? item.DisplayName : "?") + "  " + Mathf.RoundToInt(price) + "G",
                              new Color(1f, 0.9f, 0.5f));
            return p;
        }

        private void Update()
        {
            if (_sold) return;
            _bob += Time.deltaTime * 2f;
            if (_sr != null)
                _sr.transform.localPosition = new Vector3(0f, Mathf.Sin(_bob) * 0.08f, 0f);

            var rt = GameRuntime.I;
            var player = rt != null ? rt.Player : null;
            if (player == null) return;
            if (((Vector2)transform.position - (Vector2)player.transform.position).sqrMagnitude > 0.9f * 0.9f) return;

            var stats = player.Stats;
            if (stats == null) return;
            if (!stats.TrySpend(Mathf.RoundToInt(_price))) return;

            var items = player.GetComponent<PlayerItems>();
            if (items != null) items.Acquire(_item);
            else stats.ApplyItem(_item);

            _sold = true;
            if (rt != null) rt.ReportPurchase(_item, Mathf.RoundToInt(_price));
            Destroy(gameObject);
        }
    }
}
