using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 元素反应的区域/爆炸产物。
    /// 蒸汽（持续伤害）· 腐烂（减速）· 回血（治疗）· 爆炸（一次性）。
    /// 蒸汽 / 回血 / 爆炸对「区域内所有单位」生效（含玩家，见 Gap A8）；
    /// **腐烂区域例外：只减速敌方单位。**
    /// </summary>
    public class AreaEffect : MonoBehaviour
    {
        private AreaKind _kind;
        private float _radius;
        private float _duration;
        private float _tickDamage;
        private float _tickHeal;
        private float _slowFactor;
        private float _burstDamage;
        private float _burstKnockback;
        private ElementType _element = ElementType.None;

        private float _age;
        private float _acc;
        private float _tick = 0.5f;
        private bool _burstDone;
        private SpriteRenderer _disc;
        private SpriteRenderer _ring;

        private static readonly HashSet<IDamageable> _hitBuffer = new HashSet<IDamageable>();

        public static AreaEffect Create(Transform parent, Vector2 pos, ReactionEntry e, AreaKind kind)
        {
            var go = new GameObject("Area_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var a = go.AddComponent<AreaEffect>();
            a.Init(e, kind);
            return a;
        }

        private void Init(ReactionEntry e, AreaKind kind)
        {
            _kind = kind;
            _radius = Mathf.Max(0.5f, e.Radius);
            _duration = kind == AreaKind.Steam && e.Duration <= 0f ? 6f : e.Duration;
            _tickDamage = e.TickDamage;
            _tickHeal = e.TickHeal;
            _slowFactor = e.SlowFactor;
            _burstDamage = e.BurstDamage;
            _burstKnockback = e.BurstKnockback;

            if (GameRuntime.I != null) _tick = GameRuntime.I.Cfg.AreaTickInterval;

            Color c;
            switch (kind)
            {
                case AreaKind.Steam: c = new Color(0.85f, 0.88f, 0.95f, 0.30f); break;
                case AreaKind.Rot: c = new Color(0.45f, 0.35f, 0.20f, 0.35f); break;
                case AreaKind.Heal: c = new Color(0.35f, 0.90f, 0.55f, 0.30f); break;
                case AreaKind.Explosion: c = new Color(1.00f, 0.60f, 0.15f, 0.45f); break;
                default: c = new Color(1f, 1f, 1f, 0.3f); break;
            }

            var areaSize = new Vector2(_radius * 2f, _radius * 2f);
            _disc = Make.Visual("Disc", transform, Vector3.zero, Visuals.AreaKey(kind),
                                SpriteFactory.Circle(Color.white), c, areaSize, 4);
            _ring = Make.Visual("Ring", transform, Vector3.zero, VisualKey.Area_Ring,
                                SpriteFactory.Ring(Color.white), new Color(c.r, c.g, c.b, 0.85f),
                                areaSize, 4);

            if (kind == AreaKind.Rot) ApplySlowToEnemies();
            if (kind == AreaKind.Explosion) _tick = 0f;   // 立即结算
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;

            if (_kind == AreaKind.Explosion)
            {
                if (!_burstDone)
                {
                    _burstDone = true;
                    ApplyBurst();
                }
                Fade(1f - Mathf.Clamp01(_age / 0.35f));
                if (_age >= 0.35f) Destroy(gameObject);
                return;
            }

            if (_tick > 0f)
            {
                _acc += dt;
                while (_acc >= _tick)
                {
                    _acc -= _tick;
                    ApplyTick();
                }
            }

            if (_duration > 0f)
            {
                float fade = _age > _duration - 0.5f ? Mathf.Clamp01((_duration - _age) / 0.5f) : 1f;
                Fade(fade);
            }
            if (_duration > 0f && _age >= _duration) Destroy(gameObject);
        }

        private void Fade(float t)
        {
            if (_disc != null) { var c = _disc.color; c.a = 0.30f * t + 0.05f; _disc.color = c; }
            if (_ring != null) { var c = _ring.color; c.a = 0.85f * t; _ring.color = c; }
        }

        private void ApplyBurst()
        {
            _hitBuffer.Clear();
            var hits = Physics2D.OverlapCircleAll(transform.position, _radius);
            foreach (var h in hits)
            {
                var d = h.GetComponentInParent<IDamageable>();
                if (d == null || !d.IsAlive) continue;
                if (!_hitBuffer.Add(d)) continue;
                var info = DamageInfo.Player(_burstDamage, _element, transform.position,
                                             DamageSource.Explosion, _burstKnockback);
                d.TakeDamage(info);
            }
        }

        private void ApplyTick()
        {
            _hitBuffer.Clear();
            var hits = Physics2D.OverlapCircleAll(transform.position, _radius);
            foreach (var h in hits)
            {
                if (_kind == AreaKind.Steam)
                {
                    var d = h.GetComponentInParent<IDamageable>();
                    if (d == null || !d.IsAlive) continue;
                    if (!_hitBuffer.Add(d)) continue;
                    // 区域伤害不带元素，避免触发二次反应（Gap A5 白盒假设）
                    d.TakeDamage(DamageInfo.Player(_tickDamage, ElementType.None, transform.position,
                                                   DamageSource.AreaEffect));
                }
                else if (_kind == AreaKind.Heal)
                {
                    var heal = h.GetComponentInParent<IHealable>();
                    if (heal != null) heal.Heal(_tickHeal);
                }
            }
            if (_kind == AreaKind.Rot) ApplySlowToEnemies();
        }

        private void ApplySlowToEnemies()
        {
            // 腐烂区域【只减速敌方单位】——玩家不受影响。
            var hits = Physics2D.OverlapCircleAll(transform.position, _radius);
            foreach (var h in hits)
            {
                var enemy = h.GetComponentInParent<EnemyHealth>();
                if (enemy == null || !enemy.IsAlive) continue;

                var s = h.GetComponentInParent<ISlowable>();
                if (s != null) s.ApplySlow(_slowFactor, Mathf.Max(0.4f, _tick * 2f));
            }
        }
    }
}
