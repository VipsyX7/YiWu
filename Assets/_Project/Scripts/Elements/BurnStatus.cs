using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 灼伤（策划案 v2：木元素法术穿过火元素地形后，命中敌人使其每秒受到
    /// 5% 最大生命值伤害，BOSS 为 1%）。
    /// 同一目标只刷新持续时间，不叠层（Gap A7 白盒假设）。
    /// </summary>
    public class BurnStatus : MonoBehaviour
    {
        private IDamageable _target;
        private float _maxHp;
        private float _pctPerSecond;
        private float _remaining;
        private float _acc;

        private const float TickInterval = 0.25f;

        public static void Apply(GameObject victim, float maxHp, float pctPerSecond, float duration)
        {
            if (victim == null || pctPerSecond <= 0f || duration <= 0f) return;
            var b = victim.GetComponent<BurnStatus>();
            if (b == null) b = victim.AddComponent<BurnStatus>();
            b._target = victim.GetComponent<IDamageable>();
            b._maxHp = maxHp;
            b._pctPerSecond = b._pctPerSecond > 0f ? Mathf.Max(b._pctPerSecond, pctPerSecond) : pctPerSecond;
            b._remaining = Mathf.Max(b._remaining, duration);
            b._acc = 0f;
        }

        private void Update()
        {
            if (_target == null || !_target.IsAlive) { Destroy(this); return; }

            float dt = Time.deltaTime;
            _remaining -= dt;
            _acc += dt;

            while (_acc >= TickInterval)
            {
                _acc -= TickInterval;
                float dmg = _maxHp * (_pctPerSecond / 100f) * TickInterval;
                _target.TakeDamage(new DamageInfo
                {
                    Amount = dmg,
                    Element = ElementType.None,
                    Source = DamageSource.Burn,
                    Origin = transform.position,
                    FromPlayer = true,
                });
            }

            if (_remaining <= 0f) Destroy(this);
        }
    }
}
