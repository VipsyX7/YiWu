using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 从 EnemyDefinition 生成怪物实例，并按 definition 维护对象池。
    /// 策划案要求「每层共享一个怪物的对象池」，此处按定义分桶共享（跨层复用）。
    /// </summary>
    public static class EnemyFactory
    {
        private static readonly Dictionary<EnemyDefinition, SimplePool> _pools =
            new Dictionary<EnemyDefinition, SimplePool>();

        private static Transform _root;

        public static void Reset()
        {
            _pools.Clear();
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
            }
        }

        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("EnemyPool");
                    _root = go.transform;
                }
                return _root;
            }
        }

        public static EnemyHealth Spawn(EnemyDefinition def, Vector2 pos, RoomController room)
        {
            if (def == null) return null;

            if (!_pools.TryGetValue(def, out var pool))
            {
                var template = BuildTemplate(def);
                pool = new SimplePool(template, Root);
                _pools[def] = pool;
            }

            var go = pool.Spawn(new Vector3(pos.x, pos.y, 0f), Quaternion.identity);
            var pooled = go.GetComponent<PooledObject>();
            if (pooled == null) pooled = go.AddComponent<PooledObject>();
            pooled.Owner = pool;

            var hp = go.GetComponent<EnemyHealth>();
            hp.Init(def, room);
            var brain = go.GetComponent<EnemyBrain>();
            brain.Init(def, GameRuntime.I != null && GameRuntime.I.Player != null
                                ? GameRuntime.I.Player.transform : null);

            // 首次进入即已激怒的房间（例如玩家先打了一下）保持原状；
            // 新刷出的怪物一律未激怒（符合案中「首次造成伤害后」）。
            return hp;
        }

        private static GameObject BuildTemplate(EnemyDefinition def)
        {
            var go = new GameObject("EnemyTemplate_" + def.name);
            go.transform.SetParent(Root, false);
            go.SetActive(false);

            Sprite placeholder;
            switch (def.Shape)
            {
                case EnemyShape.Circle: placeholder = SpriteFactory.Circle(Color.white); break;
                default: placeholder = SpriteFactory.Solid(Color.white); break;
            }

            float d = def.BodyRadius * 2f;
            var bodySize = new Vector2(d, d);

            // 逐怪物贴图 > 全局 Enemy_Body > 占位形状色块
            if (def.BodySprite != null)
                Make.Preview("Body", go.transform, def.BodySprite, bodySize, 1);
            else
                Make.Visual("Body", go.transform, Vector3.zero, VisualKey.Enemy_Body,
                            placeholder, def.BodyColor, bodySize, 1);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 6f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = def.BodyRadius;
            col.isTrigger = false;

            go.AddComponent<EnemyHealth>();
            go.AddComponent<EnemyBrain>();
            go.AddComponent<PooledObject>();

            return go;
        }
    }
}
