using UnityEngine;

namespace Klein
{
    /// <summary>掉落物种类（策划案 v2「掉落物系统」）。</summary>
    public enum PickupKind
    {
        Element = 0,   // 元素 → 恢复法力值
        Health = 1,    // 血量 → 恢复生命值
        Money = 2,     // 金钱 → 于商人处购买物品
    }

    /// <summary>
    /// 掉落物。进入吸附半径后飞向玩家，接触即生效。
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        public PickupKind Kind { get; private set; }
        public float Value { get; private set; }

        private SpriteRenderer _sr;
        private float _bob;
        private bool _magnet;
        private static SimplePool _pool;
        private static Transform _root;

        private static Transform Root
        {
            get
            {
                if (_root == null) _root = new GameObject("PickupPool").transform;
                return _root;
            }
        }

        public static void ResetPool()
        {
            _pool = null;
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
            }
        }

        public static Pickup Spawn(PickupKind kind, Vector3 pos, float value)
        {
            if (_pool == null) _pool = new SimplePool(BuildTemplate(), Root);
            var go = _pool.Spawn(pos, Quaternion.identity);
            var p = go.GetComponent<Pickup>();
            p.Init(kind, value, _pool);
            return p;
        }

        private static GameObject BuildTemplate()
        {
            var go = new GameObject("PickupTemplate");
            go.transform.SetParent(Root, false);
            go.SetActive(false);
            Make.Square("Visual", go.transform, Vector3.zero, Color.white, new Vector2(0.35f, 0.35f), 2);
            go.AddComponent<Pickup>();
            go.AddComponent<PooledObject>();
            return go;
        }

        private void Init(PickupKind kind, float value, SimplePool pool)
        {
            Kind = kind;
            Value = value;
            _bob = Random.value * 6.28f;
            _magnet = false;

            var pooled = GetComponent<PooledObject>();
            if (pooled == null) pooled = gameObject.AddComponent<PooledObject>();
            pooled.Owner = pool;

            _sr = GetComponentInChildren<SpriteRenderer>();
            if (_sr != null)
            {
                Color tint;
                switch (kind)
                {
                    case PickupKind.Element: tint = new Color(0.35f, 0.70f, 1f); break;
                    case PickupKind.Health: tint = new Color(1f, 0.35f, 0.45f); break;
                    default: tint = new Color(1f, 0.85f, 0.25f); break;
                }
                Visuals.Apply(_sr, Visuals.PickupKey(kind), SpriteFactory.Solid(Color.white),
                              tint, new Vector2(0.35f, 0.35f));
            }
        }

        private void Update()
        {
            var rt = GameRuntime.I;
            var player = rt != null ? rt.Player : null;
            if (player == null) return;
            if (rt.GameOver) return;

            Vector2 pos = transform.position;
            Vector2 dst = (Vector2)player.transform.position - pos;
            float dist = dst.magnitude;

            if (!_magnet && dist <= rt.Cfg.PickupRadius) _magnet = true;

            if (_magnet)
            {
                pos += dst.normalized * rt.Cfg.PickupFlySpeed * Time.deltaTime;
                transform.position = new Vector3(pos.x, pos.y, 0f);
            }
            else
            {
                _bob += Time.deltaTime * 3f;
                if (_sr != null)
                    _sr.transform.localPosition = new Vector3(0f, Mathf.Sin(_bob) * 0.06f, 0f);
            }

            if (dist <= 0.45f)
            {
                Apply(player);
                Release();
            }
        }

        private void Apply(PlayerController player)
        {
            var stats = player.Stats;
            if (stats == null) return;
            switch (Kind)
            {
                case PickupKind.Element:
                    stats.AddMana(Value);
                    break;
                case PickupKind.Health:
                    stats.AddHealth(Value);
                    break;
                case PickupKind.Money:
                    stats.AddMoney(Mathf.RoundToInt(Value));
                    break;
            }
            if (GameRuntime.I != null) GameRuntime.I.ReportPickup(Kind, Value);
        }

        private void Release()
        {
            var pooled = GetComponent<PooledObject>();
            if (pooled != null) pooled.Release();
            else gameObject.SetActive(false);
        }
    }
}
