using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 怪物 AI。
    /// 未激怒：随机游走（速度 ×0.6）。
    /// 已激怒：朝向玩家移动；远程单位保持 PreferRange 并开火；近战单位贴身造成接触伤害。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyBrain : MonoBehaviour
    {
        public EnemyDefinition Def { get; private set; }
        public bool Aggro { get; private set; }
        public bool Dead { get; private set; }

        /// <summary>当前减速倍率（1 = 未被减速）。供测试与 UI 观察。</summary>
        public float SlowFactor => _slowFactor;

        private Rigidbody2D _rb;
        private Transform _target;
        private Vector2 _wanderDir;
        private float _wanderTimer;
        private float _contactTimer;
        private float _fireTimer;
        private float _slowFactor = 1f;
        private float _slowTimer;
        private float _intermittentTimer;
        private bool _intermittentMoving = true;
        private float _knockbackTimer;
        private Vector2 _knockbackVel;

        private const float WanderSpeedScale = 0.6f;

        public void Init(EnemyDefinition def, Transform target)
        {
            Def = def;
            _target = target;
            Aggro = false;
            Dead = false;
            _slowFactor = 1f;
            _slowTimer = 0f;
            _contactTimer = 0f;
            _fireTimer = 0f;
            _knockbackTimer = 0f;
            _wanderTimer = 0f;
            _intermittentTimer = 0f;
            _intermittentMoving = true;
            _wanderDir = Random.insideUnitCircle.normalized;

            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
        }

        public void SetAggro(bool v)
        {
            if (v) Aggro = true;
        }

        public void OnDeath()
        {
            Dead = true;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
        }

        public void ApplySlow(float factor, float duration)
        {
            if (factor >= _slowFactor || Mathf.Approximately(factor, _slowFactor))
                _slowFactor = Mathf.Min(_slowFactor, factor);
            else
                _slowFactor = Mathf.Min(_slowFactor, factor);
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        public void ApplyKnockback(Vector2 fromOrigin, float power)
        {
            var d = ((Vector2)transform.position - fromOrigin);
            if (d.sqrMagnitude < 1e-6f) d = Random.insideUnitCircle.normalized;
            _knockbackVel = d.normalized * power * 6f;
            _knockbackTimer = 0.12f;
        }

        private void FixedUpdate()
        {
            if (Dead || Def == null) return;
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) return;

            float dt = Time.fixedDeltaTime;

            if (_slowTimer > 0f)
            {
                _slowTimer -= dt;
                if (_slowTimer <= 0f) _slowFactor = 1f;
            }

            if (_knockbackTimer > 0f)
            {
                _knockbackTimer -= dt;
                _rb.linearVelocity = _knockbackVel;
                _knockbackVel = Vector2.Lerp(_knockbackVel, Vector2.zero, dt * 8f);
                return;
            }

            float speed = Def.MoveSpeed * _slowFactor;

            if (!Aggro)
            {
                Wander(dt, speed * WanderSpeedScale);
                return;
            }

            if (_target == null) { Wander(dt, speed * WanderSpeedScale); return; }

            Vector2 pos = transform.position;
            Vector2 toTarget = (Vector2)_target.position - pos;
            float dist = toTarget.magnitude;
            Vector2 dir = dist > 0.001f ? toTarget / dist : Vector2.zero;

            if (Def.FireRate > 0f)
            {
                // 远程：保持距离 + 开火
                Vector2 move = Vector2.zero;
                if (dist > Def.PreferRange + 0.5f) move = dir;
                else if (dist < Def.PreferRange - 1.5f) move = -dir;
                Move(move, speed, dt);
                HandleFire(dir, dist, dt);
            }
            else
            {
                // 近战：贴身 + 接触伤害
                Move(dir, speed, dt);
                HandleContact(dist, dt);
            }
        }

        private void HandleFire(Vector2 dir, float dist, float dt)
        {
            _fireTimer -= dt;
            if (_fireTimer > 0f) return;
            if (Def.FireRate <= 0f) return;
            if (dist > Def.PreferRange + 4f) return;

            _fireTimer = 1f / Mathf.Max(0.01f, Def.FireRate);

            var spec = new ProjectileSpec
            {
                Mask = ElementMask.None,
                Innate = ElementType.None,
                PlayerElement = ElementType.None,
                Damage = Def.Damage,
                Speed = Def.BulletSpeed,
                Range = Mathf.Max(dist + 4f, 8f),
                Radius = 0.18f,
                Knockback = 0f,
                FromPlayer = false,
                Pierce = 0,
            };
            Projectile.Spawn(transform.position, dir, spec, ProjectileTeam.Enemy);
        }

        private void HandleContact(float dist, float dt)
        {
            _contactTimer -= dt;
            if (_contactTimer > 0f) return;
            if (dist > Def.ContactRange) return;

            var player = GameRuntime.I != null ? GameRuntime.I.Player : null;
            if (player == null || player.Health == null) return;

            _contactTimer = Def.ContactCooldown;
            player.Health.TakeDamage(new DamageInfo
            {
                Amount = Def.Damage,
                Element = ElementType.None,
                Source = DamageSource.Contact,
                Origin = transform.position,
                Knockback = 0f,
                FromPlayer = false,
            });
        }

        private void Move(Vector2 dir, float speed, float dt)
        {
            if (Def.MoveMode == MoveMode.Stationary) { _rb.linearVelocity = Vector2.zero; return; }

            if (Def.MoveMode == MoveMode.Intermittent)
            {
                _intermittentTimer -= dt;
                if (_intermittentTimer <= 0f)
                {
                    _intermittentMoving = !_intermittentMoving;
                    _intermittentTimer = _intermittentMoving ? Def.IntermittentPattern.x : Def.IntermittentPattern.y;
                }
                if (!_intermittentMoving) { _rb.linearVelocity = Vector2.zero; return; }
            }

            if (dir.sqrMagnitude < 1e-6f) { _rb.linearVelocity = Vector2.zero; return; }
            _rb.linearVelocity = AvoidWalls(dir.normalized) * speed;
        }

        private void Wander(float dt, float speed)
        {
            if (Def.MoveMode == MoveMode.Stationary) { _rb.linearVelocity = Vector2.zero; return; }

            _wanderTimer -= dt;
            if (_wanderTimer <= 0f)
            {
                _wanderTimer = Def.WanderInterval;
                _wanderDir = Random.insideUnitCircle.normalized;
            }
            _rb.linearVelocity = AvoidWalls(_wanderDir) * speed;
        }

        /// <summary>前方 2 格不可走就换方向，避免贴墙卡死。</summary>
        private Vector2 AvoidWalls(Vector2 dir)
        {
            var grid = GameRuntime.I != null && GameRuntime.I.Floor != null
                ? GameRuntime.I.Floor.Layout.Grid : null;
            if (grid == null) return dir;

            var ahead = LevelGrid.WorldToCell((Vector2)transform.position + dir * 1.0f);
            if (!grid.IsPassable(ahead))
            {
                if (!Aggro) { _wanderDir = -dir; }
                return Vector2.zero;
            }
            return dir;
        }
    }
}
