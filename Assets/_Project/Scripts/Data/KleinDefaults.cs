using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 白盒占位数据工厂。所有数值来源：Docs/策划案v2_评审与数值基准.md。
    /// 运行时若 Resources 下存在同名资产则优先用资产，否则用这里生成的数据，
    /// 因此空工程也能直接跑起来。
    /// </summary>
    public static class KleinDefaults
    {
        private static T New<T>(string name) where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            so.name = name;
            so.hideFlags = HideFlags.DontSave;
            return so;
        }

        // ------------------------------------------------------------ 配置

        public static GameConfig CreateConfig() => New<GameConfig>("GameConfig");

        // ------------------------------------------------------------ 反应表

        public static ReactionTable CreateReactionTable()
        {
            var t = New<ReactionTable>("ReactionTable");
            t.Entries = new List<ReactionEntry>
            {
                // 水法术 × 火地形 → 穿过地形，伤害翻倍（策划案：类似火炬树桩）
                new ReactionEntry
                {
                    SpellElement = ElementType.Water, TerrainElement = ElementType.Fire,
                    Kind = ReactionKind.Pierce, DamageMultiplier = 2f,
                },
                // 水法术 × 木地形 → 腐烂区域，减速
                new ReactionEntry
                {
                    SpellElement = ElementType.Water, TerrainElement = ElementType.Wood,
                    Kind = ReactionKind.RotArea, Radius = 2.5f, Duration = 5f, SlowFactor = 0.5f,
                },
                // 火法术 × 水地形 → 蒸汽区域，持续伤害
                new ReactionEntry
                {
                    SpellElement = ElementType.Fire, TerrainElement = ElementType.Water,
                    Kind = ReactionKind.SteamArea, Radius = 3f, Duration = 6f, TickDamage = 8f,
                },
                // 火法术 × 木地形 → 爆炸
                new ReactionEntry
                {
                    SpellElement = ElementType.Fire, TerrainElement = ElementType.Wood,
                    Kind = ReactionKind.Explosion, Radius = 2.5f, BurstDamage = 40f, BurstKnockback = 1.5f,
                },
                // 木法术 × 水地形 → 回血区域
                new ReactionEntry
                {
                    SpellElement = ElementType.Wood, TerrainElement = ElementType.Water,
                    Kind = ReactionKind.HealArea, Radius = 2.5f, Duration = 6f, TickHeal = 5f,
                },
                // 木法术 × 火地形 → 穿过地形 + 灼伤 5%/s（BOSS 1%）
                new ReactionEntry
                {
                    SpellElement = ElementType.Wood, TerrainElement = ElementType.Fire,
                    Kind = ReactionKind.PierceBurn, DamageMultiplier = 1f,
                    BurnPercentPerSecond = 5f, BurnPercentBoss = 1f, BurnDuration = 4f,
                },
            };
            return t;
        }

        // ------------------------------------------------------------ 怪物

        private static EnemyDefinition Enemy(string name, float hp, float spd, float fireRate,
                                             float bulletSpeed, float dmg, int maxInRoom,
                                             MoveMode mode, float cost, Color col, EnemyShape shape,
                                             int[] drops, float[] weights, int dMin, int dMax,
                                             float bodyRadius = 0.42f)
        {
            var e = New<EnemyDefinition>("Enemy_" + name);
            e.DisplayName = name;
            e.Health = hp;
            e.MoveSpeed = spd;
            e.FireRate = fireRate;
            e.BulletSpeed = bulletSpeed;
            e.Damage = dmg;
            e.MaxInRoom = maxInRoom;
            e.MoveMode = mode;
            e.CostWeight = cost;
            e.BodyColor = col;
            e.BodyRadius = bodyRadius;
            e.Shape = shape;
            e.DropCount = new Vector2Int(dMin, dMax);
            e.DropTypes = new List<PickupKind>();
            e.DropWeights = new List<float>();
            for (int i = 0; i < drops.Length; i++)
            {
                e.DropTypes.Add((PickupKind)drops[i]);
                e.DropWeights.Add(weights[i]);
            }
            return e;
        }

        public static List<EnemyDefinition> CreateEnemies()
        {
            var list = new List<EnemyDefinition>
            {
                // 近战 · 慢速高血量单体
                Enemy("重甲兵", 60f, 2.0f, 0f, 0f, 12f, 3, MoveMode.Continuous, 2f,
                      new Color(0.80f, 0.30f, 0.28f), EnemyShape.Square,
                      new[] { 0, 1, 2 }, new[] { 30f, 20f, 50f }, 1, 2),

                // 近战 · 慢速低血量群体
                Enemy("虫群", 18f, 2.5f, 0f, 0f, 5f, 8, MoveMode.Continuous, 1f,
                      new Color(0.85f, 0.55f, 0.25f), EnemyShape.Triangle,
                      new[] { 0, 1, 2 }, new[] { 50f, 35f, 15f }, 1, 1, 0.3f),

                // 远程 · 不移动中等血量低弹速高射速
                Enemy("术士", 35f, 0f, 1.5f, 6f, 8f, 4, MoveMode.Stationary, 2f,
                      new Color(0.55f, 0.40f, 0.85f), EnemyShape.Circle,
                      new[] { 0, 1, 2 }, new[] { 35f, 30f, 35f }, 1, 1),

                // 远程 · 低血量高弹速低射速
                Enemy("游猎", 25f, 1.5f, 0.4f, 14f, 14f, 4, MoveMode.Intermittent, 2f,
                      new Color(0.35f, 0.70f, 0.65f), EnemyShape.Circle,
                      new[] { 0, 1, 2 }, new[] { 35f, 30f, 35f }, 1, 1),
            };
            return list;
        }

        public static EnemyDefinition CreateBoss()
        {
            var b = Enemy("元素之主", 600f, 2.0f, 1.2f, 7f, 12f, 1, MoveMode.Continuous, 0f,
                          new Color(0.90f, 0.20f, 0.35f), EnemyShape.Circle,
                          new[] { 2 }, new[] { 100f }, 5, 8, 0.9f);
            b.IsBoss = true;
            b.PreferRange = 5f;
            return b;
        }

        // ------------------------------------------------------------ 法术

        public static List<SpellDefinition> CreateSpells()
        {
            var bullet = New<SpellDefinition>("Spell_魔弹");
            bullet.DisplayName = "魔弹";
            bullet.Shape = SpellShape.Projectile;
            bullet.InnateElement = ElementType.None;
            bullet.Damage = 12f;
            bullet.Cooldown = 0.5f;
            bullet.Range = 8f;
            bullet.Speed = 12f;
            bullet.Radius = 0.25f;
            bullet.Color = new Color(1f, 0.88f, 0.45f);

            var barrier = New<SpellDefinition>("Spell_防护罩");
            barrier.DisplayName = "防护罩";
            barrier.Shape = SpellShape.Barrier;
            barrier.Damage = 0f;
            barrier.Cooldown = 8f;
            barrier.BarrierHealth = 30f;
            barrier.BarrierDuration = 3f;
            barrier.Color = new Color(0.5f, 0.8f, 1f);

            var slash = New<SpellDefinition>("Spell_斩击");
            slash.DisplayName = "斩击";
            slash.Shape = SpellShape.Melee;
            slash.Damage = 18f;
            slash.Cooldown = 0.6f;
            slash.Range = 1.5f;
            slash.Speed = 30f;
            slash.Radius = 0.5f;
            slash.Color = new Color(1f, 0.95f, 0.8f);

            var ray = New<SpellDefinition>("Spell_射线");
            ray.DisplayName = "射线";
            ray.Shape = SpellShape.Beam;
            ray.Damage = 20f;
            ray.Cooldown = 0.8f;
            ray.Range = 8f;
            ray.Speed = 40f;
            ray.Radius = 0.2f;
            ray.Color = new Color(0.95f, 0.5f, 1f);

            var wave = New<SpellDefinition>("Spell_冲击波");
            wave.DisplayName = "冲击波";
            wave.Shape = SpellShape.Burst;
            wave.Damage = 15f;
            wave.Cooldown = 1.2f;
            wave.Radius = 3f;
            wave.Knockback = 2f;
            wave.Color = new Color(0.6f, 0.9f, 1f);

            return new List<SpellDefinition> { bullet, barrier, slash, ray, wave };
        }

        public static List<SpellModifierDefinition> CreateModifiers()
        {
            var refract = New<SpellModifierDefinition>("Mod_折射镜");
            refract.DisplayName = "折射镜";
            refract.Kind = ModifierKind.Refract;
            refract.Magnitude = 1f;

            var amplify = New<SpellModifierDefinition>("Mod_法术增幅");
            amplify.DisplayName = "法术增幅";
            amplify.Kind = ModifierKind.Amplify;
            amplify.DamageScale = 2f;
            amplify.CooldownScale = 1.5f;

            var enchant = New<SpellModifierDefinition>("Mod_法术附魔");
            enchant.DisplayName = "法术附魔";
            enchant.Kind = ModifierKind.Enchant;

            var origin = New<SpellModifierDefinition>("Mod_原点");
            origin.DisplayName = "原点";
            origin.Kind = ModifierKind.Origin;
            origin.CooldownScale = 1.2f;

            var terrainGen = New<SpellModifierDefinition>("Mod_地形生成");
            terrainGen.DisplayName = "地形生成";
            terrainGen.Kind = ModifierKind.TerrainGen;
            terrainGen.Magnitude = 2f;

            return new List<SpellModifierDefinition> { refract, amplify, enchant, origin, terrainGen };
        }

        // ------------------------------------------------------------ 道具

        private static ItemDefinition Item(string name, ItemKind kind, ItemEffect effect,
                                           ItemPool pool, float mag, Color col, string desc,
                                           float manaCost = 0f, float cd = 1f, bool singleUse = false)
        {
            var i = New<ItemDefinition>("Item_" + name);
            i.DisplayName = name;
            i.Kind = kind;
            i.Effect = effect;
            i.Pool = pool;
            i.Magnitude = mag;
            i.Color = col;
            i.Description = desc;
            i.ManaCost = manaCost;
            i.Cooldown = cd;
            i.SingleUse = singleUse;
            return i;
        }

        public static List<ItemDefinition> CreateItems()
        {
            return new List<ItemDefinition>
            {
                Item("铁护符", ItemKind.Passive, ItemEffect.MaxHealthUp, ItemPool.Shop, 20f,
                     new Color(0.75f, 0.75f, 0.8f), "血量上限 +20"),
                Item("疾行之靴", ItemKind.Passive, ItemEffect.MoveSpeedUp, ItemPool.Chest, 1.0f,
                     new Color(0.6f, 0.85f, 0.6f), "移速 +1.0"),
                Item("锋锐符文", ItemKind.Passive, ItemEffect.AttackUp, ItemPool.Chest, 20f,
                     new Color(0.9f, 0.5f, 0.4f), "攻击力 +20%"),
                Item("元素亲和", ItemKind.Passive, ItemEffect.TerrainChargeUp, ItemPool.Chest, 3f,
                     new Color(0.5f, 0.9f, 0.9f), "地形反应次数上限 +3"),
                Item("血契", ItemKind.Passive, ItemEffect.BloodPact, ItemPool.Boss, 15f,
                     new Color(0.8f, 0.2f, 0.3f), "可消耗生命值代替法力"),
                Item("治疗药水", ItemKind.Active, ItemEffect.HealPotion, ItemPool.Shop, 40f,
                     new Color(1f, 0.4f, 0.5f), "立即回复 40 血（一次性）",
                     0f, 1f, true),
                Item("火焰喷吐", ItemKind.Active, ItemEffect.FlameSpit, ItemPool.Shop, 30f,
                     new Color(1f, 0.5f, 0.2f), "消耗 25 法力，扇形喷射", 25f, 2f, false),
            };
        }

        // ------------------------------------------------------------ 汇总

        public static void BuildAll(out GameConfig cfg, out ReactionTable reactions,
                                    out List<EnemyDefinition> enemies, out EnemyDefinition boss,
                                    out List<SpellDefinition> spells,
                                    out List<SpellModifierDefinition> mods,
                                    out List<ItemDefinition> items)
        {
            cfg = CreateConfig();
            reactions = CreateReactionTable();
            enemies = CreateEnemies();
            boss = CreateBoss();
            spells = CreateSpells();
            mods = CreateModifiers();
            items = CreateItems();
        }
    }
}
