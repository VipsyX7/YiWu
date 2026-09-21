using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 玩家控制。
    ///
    /// 移动（右键点地）：
    ///   · 目标格可走 **且** 中间没有障碍物 → **径直朝目标点直线移动**
    ///   · 有障碍物（含关着的门）→ 退回网格 A* 自动寻路绕行到最近可走格
    ///   · A* 也到不了（门锁着 / 目标在地图外）→ 不接受指令，保持不动
    /// 注意：**WASD 已不再控制移动**，走位完全由右键驱动；
    /// QWEASD 现在只作为六个法术槽的施法键。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour, ISlowable
    {
        private enum MoveMode { Idle, Straight, Path }

        public PlayerStats Stats { get; private set; }
        public PlayerHealth Health { get; private set; }
        public SpellCaster Caster { get; private set; }

        private Rigidbody2D _rb;
        private Transform _aim;
        private SpriteRenderer _body;
        private PlayerItems _items;

        private MoveMode _mode = MoveMode.Idle;
        private Vector2 _straightTarget;
        private readonly List<Vector2Int> _path = new List<Vector2Int>();
        private int _pathIdx;

        private Vector2 _dir;
        private Vector2 _facing = Vector2.right;
        private float _slowFactor = 1f;
        private float _slowTimer;
        private Vector2 _knockbackVel;
        private float _knockbackTimer;

        private float _stuckTimer;
        private Vector2 _stuckSamplePos;
        private const float StuckCheckInterval = 0.25f;
        private const float StuckMoveEpsilon = 0.12f;
        private const float ArriveEpsilon = 0.15f;

        // ---------------------------------------------------------- 对外状态

        public Vector2 Facing => _facing;
        /// <summary>当前减速倍率（1 = 未被减速）。腐烂区域不应对玩家生效。</summary>
        public float SlowFactor => _slowFactor;
        public bool IsIdle => _mode == MoveMode.Idle;
        public bool IsStraightMoving => _mode == MoveMode.Straight;
        public bool IsPathFollowing => _mode == MoveMode.Path;
        public bool HasPath => _mode == MoveMode.Path && _pathIdx < _path.Count;

        public Vector2 Destination
        {
            get
            {
                if (_mode == MoveMode.Path && _path.Count > 0)
                    return LevelGrid.CellCenter2(_path[_path.Count - 1]);
                return _straightTarget;
            }
        }

        // ---------------------------------------------------------- 初始化

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            Stats = GetComponent<PlayerStats>();
            Health = GetComponent<PlayerHealth>();
            Caster = GetComponent<SpellCaster>();
            _items = GetComponent<PlayerItems>();
            BuildVisual();
        }

        private void BuildVisual()
        {
            float r = GameRuntime.I != null ? GameRuntime.I.Cfg.PlayerRadius : 0.4f;
            _body = Make.Visual("Body", transform, Vector3.zero,
                                VisualKey.Player_Body, SpriteFactory.Circle(Color.white),
                                new Color(0.95f, 0.95f, 1f), new Vector2(r * 2f, r * 2f), 2);
            var aim = Make.Visual("Aim", transform, Vector3.zero,
                                  VisualKey.Player_Aim, SpriteFactory.Solid(Color.white),
                                  new Color(0.6f, 0.9f, 1f, 0.9f), new Vector2(0.45f, 0.12f), 2);
            _aim = aim.transform;

            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.linearDamping = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = GetComponent<CircleCollider2D>();
            if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = r;
        }

        public void Bind(PlayerStats stats, PlayerHealth health, SpellCaster caster)
        {
            Stats = stats; Health = health; Caster = caster;
            health.Bind(stats, this, _body);
        }

        // ---------------------------------------------------------- 每帧

        private void Update()
        {
            var input = KleinInput.I;
            if (input == null) return;

            // 朝向始终指向鼠标（施法方向）
            Vector2 toMouse = (Vector2)input.PointerWorld - (Vector2)transform.position;
            if (toMouse.sqrMagnitude > 0.01f)
            {
                _facing = toMouse.normalized;
                if (_aim != null)
                {
                    _aim.localPosition = new Vector3(_facing.x, _facing.y, 0f) * 0.55f;
                    _aim.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_facing.y, _facing.x) * Mathf.Rad2Deg);
                }
            }

            if (KleinInput.UiCaptures)
            {
                _dir = Vector2.zero;
                Caster?.Tick(Time.deltaTime);
                return;
            }

            // 右键点地 → 设定目标
            if (input.MoveCommandPressed()) SetDestination(input.PointerWorld);

            // 只由右键驱动走位；WASD 不再参与移动
            switch (_mode)
            {
                case MoveMode.Straight: _dir = StepStraight(); break;
                case MoveMode.Path: _dir = StepPath(); break;
                default: _dir = Vector2.zero; break;
            }

            CheckStuck();

            // 施法（QWEASD 六个槽）
            if (Caster != null)
            {
                for (int i = 0; i < SpellCaster.SlotCount; i++)
                {
                    if (input.CastPressed(i) && Caster.TryCast(i, input.PointerWorld) && GameRuntime.I != null)
                        GameRuntime.I.ReportCast(i, Caster.Slots[i]);
                }
                Caster.Tick(Time.deltaTime);
            }

            // 主动道具
            if (_items == null) _items = GetComponent<PlayerItems>();
            if (_items != null)
            {
                if (input.UseItemPressed()) _items.UseActive();
                if (input.SwitchItemPressed()) _items.SwitchActive();
            }
        }

        // ---------------------------------------------------------- 移动指令

        /// <summary>
        /// 设定移动目标：无遮挡直行，有遮挡绕行。
        /// </summary>
        public void SetDestination(Vector2 world)
        {
            var rt = GameRuntime.I;
            var grid = rt != null && rt.Floor != null ? rt.Floor.Layout.Grid : null;
            if (grid == null) return;

            Vector2 pos = transform.position;
            var targetCell = LevelGrid.WorldToCell(world);
            bool targetPassable = grid.IsPassable(targetCell);
            float radius = rt.Cfg.PlayerRadius;

            // ---- 1. 目标可走 且 直线无遮挡 → 径直走过去 ----
            if (targetPassable && NavUtil.HasLineOfSight(grid, pos, world, radius))
            {
                GoStraight(world, pos);
                return;
            }

            // ---- 2. 有障碍 → A* 绕行（目标不可达时也会退到"最近的可走格"） ----
            if (TryPathTo(world)) return;

            // ---- 3. 实在到不了（门锁着 / 目标在地图外）→ 不接受这个指令 ----
            Stop();
        }

        private void GoStraight(Vector2 world, Vector2 fromPos)
        {
            _mode = MoveMode.Straight;
            _straightTarget = world;
            _path.Clear();
            _pathIdx = 0;
            ResetStuck(fromPos);
        }

        private bool TryPathTo(Vector2 world)
        {
            var rt = GameRuntime.I;
            var grid = rt != null && rt.Floor != null ? rt.Floor.Layout.Grid : null;
            if (grid == null) return false;

            var start = LevelGrid.WorldToCell(transform.position);
            var goal = GridPathfinder.NearestPassable(grid, LevelGrid.WorldToCell(world));
            if (goal == start) return false;

            var p = GridPathfinder.Find(grid, start, goal);
            if (p.Count == 0) return false;

            _path.Clear();
            _path.AddRange(p);
            _pathIdx = 0;
            _mode = MoveMode.Path;
            ResetStuck(transform.position);
            return true;
        }

        public void Stop()
        {
            _mode = MoveMode.Idle;
            _dir = Vector2.zero;
            _path.Clear();
            _pathIdx = 0;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
        }

        private Vector2 StepStraight()
        {
            Vector2 pos = transform.position;
            Vector2 d = _straightTarget - pos;
            float dist = d.magnitude;
            if (dist <= ArriveEpsilon)
            {
                Stop();
                return Vector2.zero;
            }
            return d / dist;
        }

        private Vector2 StepPath()
        {
            if (_pathIdx >= _path.Count) { Stop(); return Vector2.zero; }

            Vector2 wp = LevelGrid.CellCenter2(_path[_pathIdx]);
            Vector2 pos = transform.position;
            Vector2 d = wp - pos;

            if (d.magnitude < 0.22f)
            {
                _pathIdx++;
                if (_pathIdx >= _path.Count) { Stop(); return Vector2.zero; }
                wp = LevelGrid.CellCenter2(_path[_pathIdx]);
                d = wp - pos;
            }
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector2.zero;
        }

        // ---------------------------------------------------------- 卡住自愈

        private void ResetStuck(Vector2 pos)
        {
            _stuckTimer = 0f;
            _stuckSamplePos = pos;
        }

        /// <summary>
        /// 直行时被墙角/怪物卡住 → 先试着改用 A* 绕过去，再不行就停下。
        /// 寻路模式不做这个判断（物理推挤造成的短暂停顿不该中断指令）。
        /// </summary>
        private void CheckStuck()
        {
            if (_mode != MoveMode.Straight)
            {
                _stuckTimer = 0f;
                _stuckSamplePos = transform.position;
                return;
            }

            _stuckTimer += Time.deltaTime;
            if (_stuckTimer < StuckCheckInterval) return;

            float moved = ((Vector2)transform.position - _stuckSamplePos).magnitude;
            _stuckTimer = 0f;
            _stuckSamplePos = transform.position;
            if (moved >= StuckMoveEpsilon) return;

            if (TryPathTo(_straightTarget)) return;
            Stop();
        }

        // ---------------------------------------------------------- 物理

        private void FixedUpdate()
        {
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
                _knockbackVel = Vector2.Lerp(_knockbackVel, Vector2.zero, dt * 10f);
                return;
            }

            float speed = Stats != null ? Stats.MoveSpeed : 5f;
            _rb.linearVelocity = _dir * speed * _slowFactor;
        }

        public void ApplySlow(float factor, float duration)
        {
            _slowFactor = Mathf.Min(_slowFactor, factor);
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        public void ApplyKnockback(Vector2 fromOrigin, float power)
        {
            var d = ((Vector2)transform.position - fromOrigin);
            if (d.sqrMagnitude < 1e-6f) d = UnityEngine.Random.insideUnitCircle.normalized;
            _knockbackVel = d.normalized * power * 6f;
            _knockbackTimer = 0.15f;
        }

        public void Teleport(Vector2 pos)
        {
            transform.position = new Vector3(pos.x, pos.y, 0f);
            Stop();
            ResetStuck(pos);
        }

        private void OnDrawGizmos()
        {
            if (_mode == MoveMode.Straight)
            {
                Gizmos.color = Color.green;
                var t = new Vector3(_straightTarget.x, _straightTarget.y, 0f);
                Gizmos.DrawLine(transform.position, t);
                Gizmos.DrawWireSphere(t, 0.25f);
                return;
            }

            if (_path == null || _path.Count == 0) return;
            Gizmos.color = Color.cyan;
            for (int i = Mathf.Max(0, _pathIdx); i < _path.Count - 1; i++)
                Gizmos.DrawLine(LevelGrid.CellCenter2(_path[i]), LevelGrid.CellCenter2(_path[i + 1]));
        }
    }
}
