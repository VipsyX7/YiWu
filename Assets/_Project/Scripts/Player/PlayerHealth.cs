using UnityEngine;

namespace Klein
{
    /// <summary>玩家生命。血量归零 → 单局失败 → 结算（策划案 v2 已明确）。</summary>
    public class PlayerHealth : MonoBehaviour, IDamageable, IHealable
    {
        private PlayerStats _stats;
        private PlayerController _controller;
        private SpriteRenderer _sr;
        private Color _baseColor = Color.white;

        private float _hp;
        private float _invuln;
        private float _flash;

        public bool IsAlive => _hp > 0f;
        public float Health => _hp;
        public float MaxHealth => _stats != null ? _stats.MaxHealth : 100f;
        public Transform Transform => transform;

        public System.Action Died;

        public void Bind(PlayerStats stats, PlayerController controller, SpriteRenderer body)
        {
            _stats = stats;
            _controller = controller;
            _sr = body;
            if (_sr != null) _baseColor = _sr.color;
            _hp = stats.MaxHealth;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;
            if (_invuln > 0f) return;

            _hp = Mathf.Max(0f, _hp - info.Amount);
            _invuln = GameRuntime.I != null ? GameRuntime.I.Cfg.PlayerInvincibleTime : 0.5f;
            _flash = 0.12f;

            if (info.Knockback > 0f && _controller != null)
                _controller.ApplyKnockback(info.Origin, info.Knockback);

            if (_hp <= 0f)
            {
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            _hp = Mathf.Clamp(_hp + amount, 0f, MaxHealth);
        }

        private void Update()
        {
            if (_invuln > 0f) _invuln -= Time.deltaTime;

            if (_flash > 0f)
            {
                _flash -= Time.deltaTime;
                if (_sr != null) _sr.color = _flash > 0f ? new Color(1f, 0.35f, 0.35f) : _baseColor;
            }
        }

        public float Normalized => MaxHealth > 0f ? _hp / MaxHealth : 0f;
    }
}
