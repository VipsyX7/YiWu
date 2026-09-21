using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 怪物生命与受击。
    /// 「玩家在房间内首次造成伤害后，怪物变为朝向玩家移动」：
    /// 由 EnemyHealth 转交 RoomController.AggroAll() 实现房间级仇恨。
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable, IHealable, ISlowable
    {
        public EnemyDefinition Def { get; private set; }
        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public bool IsBoss => Def != null && Def.IsBoss;
        public bool IsAlive => Health > 0f;
        public Transform Transform => transform;

        private RoomController _room;
        private EnemyBrain _brain;
        private PooledObject _pooled;
        private SpriteRenderer _sr;
        private Color _baseColor;
        private float _flash;

        public void Init(EnemyDefinition def, RoomController room)
        {
            Def = def;
            _room = room;
            MaxHealth = def.Health;
            Health = def.Health;
            _pooled = GetComponent<PooledObject>();
            _brain = GetComponent<EnemyBrain>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _baseColor = def.BodyColor;
            if (_sr != null) _sr.color = _baseColor;
            _flash = 0f;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;

            Health -= info.Amount;
            _flash = 0.08f;

            if (info.FromPlayer)
            {
                // 首伤 → 房间内全体转追踪
                if (_room != null) _room.AggroAll();
                CombatEvents.RaisePlayerDealtDamage(transform.position);
            }

            if (info.Knockback > 0f && _brain != null)
                _brain.ApplyKnockback(info.Origin, info.Knockback);

            if (Health <= 0f) Die(info);
        }

        private void Die(in DamageInfo info)
        {
            Health = 0f;
            if (LootDropper != null) LootDropper(Def, transform.position);
            if (_room != null) _room.OnEnemyKilled(this);
            if (_brain != null) _brain.OnDeath();
            if (_pooled != null) _pooled.Release();
            else gameObject.SetActive(false);
        }

        /// <summary>掉落回调，由 GameRuntime 注入，避免敌人模块依赖道具模块。</summary>
        public static System.Action<EnemyDefinition, Vector3> LootDropper;

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
        }

        public void ApplySlow(float factor, float duration)
        {
            if (_brain != null) _brain.ApplySlow(factor, duration);
        }

        /// <summary>房间级仇恨：玩家首次造成伤害后整房转追踪。</summary>
        public void SetAggro(bool v)
        {
            if (_brain != null) _brain.SetAggro(v);
        }

        private void Update()
        {
            if (_flash > 0f)
            {
                _flash -= Time.deltaTime;
                if (_sr != null)
                    _sr.color = _flash > 0f ? Color.white : _baseColor;
            }
        }
    }
}
