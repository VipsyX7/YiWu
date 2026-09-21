using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 法术房里的法术卡。策划案 v2：「法术房中随机生成一个已解锁的法术」。
    /// 拾取后进入法术背包，按 B 装配到槽位。
    /// </summary>
    public class SpellCardPickup : MonoBehaviour
    {
        private SpellCard _card;
        private float _bob;
        private SpriteRenderer _sr;

        public static SpellCardPickup Create(Transform parent, Vector2 pos, SpellCard card)
        {
            var go = new GameObject("SpellPickup");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var s = go.AddComponent<SpellCardPickup>();
            s._card = card;

            var color = card != null && card.Spell != null ? card.Spell.Color : new Color(0.7f, 0.5f, 1f);
            Make.Visual("Glow", go.transform, Vector3.zero, VisualKey.Spell_CardGlow,
                        SpriteFactory.Solid(Color.white), new Color(color.r, color.g, color.b, 0.35f),
                        new Vector2(1.4f, 1.4f), 1);

            var cardSprite = card != null && card.Spell != null ? card.Spell.ProjectileSprite : null;
            s._sr = cardSprite != null
                ? Make.Preview("Card", go.transform, cardSprite, new Vector2(0.45f, 0.6f), 2)
                : Make.Visual("Card", go.transform, Vector3.zero, VisualKey.Spell_Card,
                              SpriteFactory.Solid(Color.white), color, new Vector2(0.45f, 0.6f), 2);

            WorldLabel.Attach(go, "法术：" + (card != null ? card.Label() : "?"), new Color(0.8f, 0.7f, 1f));
            return s;
        }

        private void Update()
        {
            _bob += Time.deltaTime * 2.5f;
            if (_sr != null)
                _sr.transform.localPosition = new Vector3(0f, Mathf.Sin(_bob) * 0.1f, 0f);

            var rt = GameRuntime.I;
            var player = rt != null ? rt.Player : null;
            if (player == null) return;
            if (((Vector2)transform.position - (Vector2)player.transform.position).sqrMagnitude > 0.9f * 0.9f) return;

            if (player.Caster != null) player.Caster.AddToInventory(_card);
            if (rt != null) rt.ReportSpellPicked(_card);
            Destroy(gameObject);
        }
    }
}
