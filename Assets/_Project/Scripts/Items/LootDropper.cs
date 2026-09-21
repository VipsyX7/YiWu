using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 掉落生成。策划案 v2：「掉落物权重由怪物种类决定」，
    /// 权重表直接挂在 EnemyDefinition 上（DropTypes / DropWeights）。
    /// </summary>
    public static class LootDropper
    {
        public static void Drop(EnemyDefinition def, Vector3 position)
        {
            if (def == null || def.DropTypes == null || def.DropTypes.Count == 0) return;
            var cfg = GameRuntime.I != null ? GameRuntime.I.Cfg : null;
            if (cfg == null) return;

            int count = Random.Range(def.DropCount.x, def.DropCount.y + 1);
            for (int i = 0; i < count; i++)
            {
                var kind = Roll(def);
                float value;
                switch (kind)
                {
                    case PickupKind.Element: value = cfg.PickupMana; break;
                    case PickupKind.Health: value = cfg.PickupHealth; break;
                    default: value = cfg.PickupMoney; break;
                }

                Vector2 jitter = Random.insideUnitCircle * 0.45f;
                Pickup.Spawn(kind, position + new Vector3(jitter.x, jitter.y, 0f), value);
            }
        }

        public static PickupKind Roll(EnemyDefinition def)
        {
            float total = def.TotalDropWeight;
            if (total <= 0f) return def.DropTypes[0];

            float r = Random.Range(0f, total);
            for (int i = 0; i < def.DropTypes.Count; i++)
            {
                float w = i < def.DropWeights.Count ? def.DropWeights[i] : 0f;
                if (r < w) return def.DropTypes[i];
                r -= w;
            }
            return def.DropTypes[def.DropTypes.Count - 1];
        }
    }
}
