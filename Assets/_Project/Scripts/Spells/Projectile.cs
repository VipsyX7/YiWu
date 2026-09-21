using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    public enum ProjectileTeam { Player = 0, Enemy = 1 }

    public struct ProjectileSpec
    {
        public ElementMask Mask;
        public ElementType Innate;
        public ElementType PlayerElement;
        public float Damage;
        public float Speed;
        public float Range;
        public float Radius;
        public float Knockback;
        public bool FromPlayer;
        public int Pierce;
        public Color Color;
        /// <summary>逐法术贴图（SpellDefinition.ProjectileSprite）。为空则用全局 key。</summary>
        public Sprite Sprite;
    }

    /// <summary>
    /// 弹体。玩家与敌方共用一套实现。
    /// 撞墙用 LevelGrid 判定（不依赖物理层），命中用 OverlapCircleAll + 组件筛选。
    /// 元素地形反应在此结算（策划案 v2：法术 × 地形）。
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private ProjectileSpec _spec;
        private ProjectileTeam _team;
        private Vector2 _dir;
        private float _traveled;
        private float _damageMultiplier = 1f;
        private ReactionEntry _burnEntry;
        private ReactionEntry _lastHitEntry;
        private int _refractRemaining;
        private float _life;
        private SpriteRenderer _sr;
        private readonly HashSet<Collider2D> _frameHits = new HashSet<Collider2D>();
        /// <summary>
        /// 本弹体已经与之反应过的【地形块】。
        /// 地形现在是"整块共享次数"的单位，所以这里记录的是 blob.Id 而不是格坐标：
        /// 一个弹体横穿一整块不规则地形时，只应产生一次反应、只消耗 1 次次数。
        /// 只靠「每 60 帧一次」的冷却是不够的——帧率越高，弹体在同一格里停留的帧数越多，
        /// 冷却一到就会对同一块地形重复触发。冷却退化为跨弹体的限流。
        /// </summary>
        private readonly HashSet<int> _reactedBlobs = new HashSet<int>();

        private static SimplePool _playerPool;
        private static SimplePool _enemyPool;
        private static Transform _root;

        private const float MaxSubStep = 0.2f;

        public ReactionEntry LastReaction => _lastHitEntry;

        private static Transform Root
        {
            get
            {
                if (_root == null) _root = new GameObject("ProjectilePool").transform;
                return _root;
            }
        }

        public static void ResetPools()
        {
            _playerPool = null;
            _enemyPool = null;
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
            }
        }

        private static SimplePool GetPool(ProjectileTeam team)
        {
            if (team == ProjectileTeam.Player)
            {
                if (_playerPool == null)
                    _playerPool = new SimplePool(BuildTemplate("PlayerProjectile", VisualKey.Projectile_Player), Root);
                return _playerPool;
            }
            if (_enemyPool == null)
                _enemyPool = new SimplePool(BuildTemplate("EnemyProjectile", VisualKey.Projectile_Enemy), Root);
            return _enemyPool;
        }

        private static GameObject BuildTemplate(string name, VisualKey key)
        {
            var go = new GameObject(name + "Template");
            go.transform.SetParent(Root, false);
            go.SetActive(false);
            Make.Visual("Visual", go.transform, Vector3.zero, key,
                        SpriteFactory.Circle(Color.white), Color.white, new Vector2(0.5f, 0.5f), 3);
            go.AddComponent<Projectile>();
            go.AddComponent<PooledObject>();
            return go;
        }

        public static Projectile Spawn(Vector2 pos, Vector2 dir, in ProjectileSpec spec, ProjectileTeam team)
        {
            var pool = GetPool(team);
            var go = pool.Spawn(new Vector3(pos.x, pos.y, 0f), Quaternion.identity);
            var p = go.GetComponent<Projectile>();
            p.Init(pos, dir, spec, team, pool);
            return p;
        }

        private void Init(Vector2 pos, Vector2 dir, ProjectileSpec spec, ProjectileTeam team, SimplePool pool)
        {
            _spec = spec;
            _team = team;
            _dir = dir.sqrMagnitude < 1e-6f ? Vector2.right : dir.normalized;
            _traveled = 0f;
            _damageMultiplier = 1f;
            _burnEntry = null;
            _lastHitEntry = null;
            _refractRemaining = spec.Pierce;
            _life = 0f;
            _reactedBlobs.Clear();

            var pooled = GetComponent<PooledObject>();
            if (pooled == null) pooled = gameObject.AddComponent<PooledObject>();
            pooled.Owner = pool;

            _sr = GetComponentInChildren<SpriteRenderer>();
            if (_sr != null)
            {
                var size = new Vector2(spec.Radius * 2f, spec.Radius * 2f);

                if (spec.Sprite != null)
                {
                    // 逐法术贴图
                    _sr.sprite = spec.Sprite;
                    _sr.color = Color.white;
                    _sr.transform.localScale = Visuals.FitScale(spec.Sprite, size);
                }
                else
                {
                    var key = _team == ProjectileTeam.Player
                        ? VisualKey.Projectile_Player : VisualKey.Projectile_Enemy;
                    var tint = spec.Color.a > 0f ? spec.Color : Color.white;
                    Visuals.Apply(_sr, key, SpriteFactory.Circle(Color.white), tint, size);
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _life += dt;

            float step = _spec.Speed * dt;
            int steps = Mathf.Max(1, Mathf.CeilToInt(step / MaxSubStep));
            float sub = step / steps;

            for (int i = 0; i < steps; i++)
            {
                if (!Advance(sub)) return;
            }
        }

        /// <returns>false 表示弹体已消失，调用方应立即返回</returns>
        private bool Advance(float dist)
        {
            Vector2 next = (Vector2)transform.position + _dir * dist;
            transform.position = new Vector3(next.x, next.y, 0f);
            _traveled += dist;

            var grid = GameRuntime.I != null && GameRuntime.I.Floor != null
                ? GameRuntime.I.Floor.Layout.Grid : null;

            // ---- 1. 撞墙 ----
            if (grid != null)
            {
                var c = LevelGrid.WorldToCell(next);
                if (!grid.IsWalkable(c))
                {
                    Release();
                    return false;
                }
                // 关闭的门同样挡弹
                if (grid.DoorCells.Contains(c) && !grid.OpenDoorCells.Contains(c))
                {
                    Release();
                    return false;
                }
            }

            // ---- 2. 元素地形反应（策划案 v2 核心） ----
            if (TryTerrainReaction(next))
            {
                return false;
            }

            // ---- 3. 命中单位 ----
            if (TryHitUnits(next))
            {
                return false;
            }

            // ---- 4. 射程 ----
            if (_traveled >= _spec.Range)
            {
                Release();
                return false;
            }
            return true;
        }

        private bool TryTerrainReaction(Vector2 pos)
        {
            var eg = GameRuntime.I != null ? GameRuntime.I.Elements : null;
            if (eg == null || _spec.Mask == ElementMask.None) return false;

            var cell = LevelGrid.WorldToCell(pos);
            var blob = eg.GetBlob(cell);
            if (blob == null) return false;
            if (_reactedBlobs.Contains(blob.Id)) return false;

            var res = ElementReactionResolver.Resolve(eg, blob, _spec.Innate, _spec.PlayerElement);
            if (!res.Triggered) return false;

            _reactedBlobs.Add(blob.Id);
            _lastHitEntry = res.Entry;
            if (GameRuntime.I != null) GameRuntime.I.ReportReaction(cell, res.Entry, _team);

            // 区域 / 爆炸以【地形块】的中心生成（"以地形为中心"）
            var center = LevelGrid.CellCenter2(blob.CenterCell);

            switch (res.Entry.Kind)
            {
                case ReactionKind.Recharge:
                    return false;   // 回充，弹体继续飞

                case ReactionKind.Pierce:
                    _damageMultiplier *= Mathf.Max(1f, res.Entry.DamageMultiplier);
                    return false;   // 穿透，继续飞

                case ReactionKind.PierceBurn:
                    _burnEntry = res.Entry;
                    return false;   // 穿透，继续飞

                case ReactionKind.Explosion:
                    AreaEffect.Create(EffectRoot, center, res.Entry, AreaKind.Explosion);
                    Release();
                    return true;

                case ReactionKind.SteamArea:
                    AreaEffect.Create(EffectRoot, center, res.Entry, AreaKind.Steam);
                    Release();
                    return true;

                case ReactionKind.RotArea:
                    AreaEffect.Create(EffectRoot, center, res.Entry, AreaKind.Rot);
                    Release();
                    return true;

                case ReactionKind.HealArea:
                    AreaEffect.Create(EffectRoot, center, res.Entry, AreaKind.Heal);
                    Release();
                    return true;
            }
            return false;
        }

        private Transform EffectRoot =>
            GameRuntime.I != null ? GameRuntime.I.EffectsRoot : transform.parent;

        private bool TryHitUnits(Vector2 pos)
        {
            _frameHits.Clear();
            var hits = Physics2D.OverlapCircleAll(pos, Mathf.Max(0.1f, _spec.Radius));
            foreach (var h in hits)
            {
                if (h == null || !_frameHits.Add(h)) continue;

                if (_team == ProjectileTeam.Player)
                {
                    var eh = h.GetComponentInParent<EnemyHealth>();
                    if (eh == null || !eh.IsAlive) continue;
                    DealDamageToEnemy(eh, pos);
                }
                else
                {
                    // 先看是不是屏障
                    var barrier = h.GetComponentInParent<Barrier>();
                    if (barrier != null)
                    {
                        barrier.Absorb(_spec.Damage);
                        Release();
                        return true;
                    }

                    var ph = h.GetComponentInParent<PlayerHealth>();
                    if (ph == null || !ph.IsAlive) continue;
                    ph.TakeDamage(new DamageInfo
                    {
                        Amount = _spec.Damage,
                        Element = _spec.Innate,
                        Source = DamageSource.Projectile,
                        Origin = pos,
                        Knockback = 0f,
                        FromPlayer = false,
                    });
                    Release();
                    return true;
                }
            }
            return false;
        }

        private void DealDamageToEnemy(EnemyHealth eh, Vector2 pos)
        {
            float dmg = _spec.Damage * _damageMultiplier;
            eh.TakeDamage(DamageInfo.Player(dmg, _spec.Innate, pos,
                                            DamageSource.Projectile, _spec.Knockback));

            // 木→火 穿透后的灼伤
            if (_burnEntry != null)
            {
                float pct = eh.IsBoss ? _burnEntry.BurnPercentBoss : _burnEntry.BurnPercentPerSecond;
                BurnStatus.Apply(eh.gameObject, eh.MaxHealth, pct, _burnEntry.BurnDuration);
            }

            // 折射镜修正器
            if (_refractRemaining > 0)
            {
                var next = FindNearestEnemy(pos, 12f);
                if (next != null)
                {
                    _refractRemaining--;
                    _damageMultiplier *= 0.7f;
                    var nd = ((Vector2)next.transform.position - pos).normalized;
                    _dir = nd;
                    _traveled = 0f;
                    if (GameRuntime.I != null) GameRuntime.I.ReportRefract();
                    return;
                }
            }

            Release();
        }

        private EnemyHealth FindNearestEnemy(Vector2 from, float maxDist)
        {
            EnemyHealth best = null;
            float bestD = maxDist * maxDist;
            var all = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
            foreach (var e in all)
            {
                if (e == null || !e.IsAlive) continue;
                float d = ((Vector2)e.transform.position - from).sqrMagnitude;
                if (d < bestD && d > 0.01f) { bestD = d; best = e; }
            }
            return best;
        }

        private void Release()
        {
            var pooled = GetComponent<PooledObject>();
            if (pooled != null) pooled.Release();
            else gameObject.SetActive(false);
        }
    }
}
