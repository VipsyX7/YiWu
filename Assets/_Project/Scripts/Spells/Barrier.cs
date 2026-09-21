using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 防护罩 / 屏障。策划案中的驻留法术，同时也是「法术间互相影响」的演示载体
    /// （v1 举例：屏障折射射线）。
    /// 白盒实现：阻挡敌方弹体、可被击破、超时消失。
    /// </summary>
    public class Barrier : MonoBehaviour
    {
        private float _health;
        private float _maxHealth;
        private float _life;
        private float _duration;
        private SpriteRenderer _sr;
        private BoxCollider2D _col;

        public static Barrier Create(Transform parent, Vector2 pos, SpellDefinition spell,
                                     ElementType element, int health)
        {
            var root = GameRuntime.I != null ? GameRuntime.I.EffectsRoot : parent;
            var go = new GameObject("Barrier");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var b = go.AddComponent<Barrier>();
            b._maxHealth = Mathf.Max(1f, health);
            b._health = b._maxHealth;
            b._duration = spell.BarrierDuration > 0f ? spell.BarrierDuration : 3f;

            var col = element != ElementType.None ? element.ToColor() : spell.Color;
            var c = new Color(col.r, col.g, col.b, 0.35f);
            var size = new Vector2(0.6f, 3.2f);
            b._sr = Make.Visual("Visual", go.transform, Vector3.zero, VisualKey.Barrier,
                                SpriteFactory.Solid(Color.white), c, size, 3);

            b._col = go.AddComponent<BoxCollider2D>();
            b._col.size = size;

            return b;
        }

        /// <summary>吸收一次伤害。返回 true 表示屏障仍在。</summary>
        public bool Absorb(float damage)
        {
            _health -= damage;
            if (_sr != null)
            {
                _sr.color = new Color(_sr.color.r, _sr.color.g, _sr.color.b,
                                      Mathf.Lerp(0.12f, 0.5f, _health / _maxHealth));
            }
            if (_health <= 0f) { Destroy(gameObject); return false; }
            return true;
        }

        private void Update()
        {
            _life += Time.deltaTime;
            if (_life >= _duration) Destroy(gameObject);
        }
    }
}
