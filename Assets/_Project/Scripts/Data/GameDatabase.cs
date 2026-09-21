using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 数据总入口。优先从 Resources/Klein 读取 ScriptableObject 资产；
    /// 没有资产时退回 KleinDefaults 的代码默认值，保证空工程可直接运行。
    /// 用菜单 Klein/生成数据资产 可把默认值落成 .asset 以便在 Inspector 里调。
    /// </summary>
    public class GameDatabase
    {
        public static GameDatabase I { get; private set; }

        public const string ResourceRoot = "Klein";

        public GameConfig Config;
        public ReactionTable Reactions;
        public List<EnemyDefinition> Enemies = new List<EnemyDefinition>();
        public EnemyDefinition BossEnemy;
        public List<SpellDefinition> Spells = new List<SpellDefinition>();
        public List<SpellModifierDefinition> Modifiers = new List<SpellModifierDefinition>();
        public List<ItemDefinition> Items = new List<ItemDefinition>();

        public bool LoadedFromAssets { get; private set; }

        public static GameDatabase LoadOrCreate(bool forceCodeDefaults = false)
        {
            var db = new GameDatabase();

            // 贴图表：可空。全空时所有视觉回退到运行时生成的占位图。
            Visuals.Load(true);

            if (!forceCodeDefaults)
            {
                db.Config = Resources.Load<GameConfig>(ResourceRoot + "/GameConfig");
                db.Reactions = Resources.Load<ReactionTable>(ResourceRoot + "/ReactionTable");
                var enemies = Resources.LoadAll<EnemyDefinition>(ResourceRoot + "/Enemies");
                var spells = Resources.LoadAll<SpellDefinition>(ResourceRoot + "/Spells");
                var mods = Resources.LoadAll<SpellModifierDefinition>(ResourceRoot + "/SpellModifiers");
                var items = Resources.LoadAll<ItemDefinition>(ResourceRoot + "/Items");
                db.LoadedFromAssets = db.Config != null && db.Reactions != null
                                      && enemies.Length > 0 && spells.Length > 0;

                if (enemies.Length > 0) db.Enemies.AddRange(enemies);
                if (spells.Length > 0) db.Spells.AddRange(spells);
                if (mods.Length > 0) db.Modifiers.AddRange(mods);
                if (items.Length > 0) db.Items.AddRange(items);
            }

            if (db.Config == null) db.Config = KleinDefaults.CreateConfig();
            if (db.Reactions == null) db.Reactions = KleinDefaults.CreateReactionTable();

            if (db.Enemies.Count == 0) db.Enemies.AddRange(KleinDefaults.CreateEnemies());
            db.BossEnemy = null;
            foreach (var e in db.Enemies)
                if (e.IsBoss) { db.BossEnemy = e; break; }
            if (db.BossEnemy == null)
            {
                db.BossEnemy = KleinDefaults.CreateBoss();
                db.Enemies.Add(db.BossEnemy);
            }

            if (db.Spells.Count == 0) db.Spells.AddRange(KleinDefaults.CreateSpells());
            if (db.Modifiers.Count == 0) db.Modifiers.AddRange(KleinDefaults.CreateModifiers());
            if (db.Items.Count == 0) db.Items.AddRange(KleinDefaults.CreateItems());

            I = db;
            return db;
        }

        // ------------------------------------------------------------ 抽取

        public EnemyDefinition PickTrashEnemy()
        {
            var pool = new List<EnemyDefinition>();
            foreach (var e in Enemies)
                if (!e.IsBoss) pool.Add(e);
            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        public SpellCard PickSpellCard()
        {
            if (Spells.Count == 0) return null;

            var card = new SpellCard();
            // 基础法术（魔弹/防护罩/斩击）为主
            card.Spell = Spells[Random.Range(0, Spells.Count)];

            // 20% 概率带一个修正器（白盒主要用来验证折射镜架构）
            if (Modifiers.Count > 0 && Random.value < 0.2f)
            {
                var refract = Modifiers.Find(m => m.Kind == ModifierKind.Refract);
                if (refract != null) card.Modifiers.Add(refract);
            }
            return card;
        }

        public List<ItemDefinition> PickShopItems(int count)
        {
            var pool = new List<ItemDefinition>();
            foreach (var i in Items)
                if (i.Pool == ItemPool.Shop || i.Pool == ItemPool.Any) pool.Add(i);
            if (pool.Count == 0) pool.AddRange(Items);
            return TakeRandom(pool, count);
        }

        public ItemDefinition PickChestItem()
        {
            var pool = new List<ItemDefinition>();
            foreach (var i in Items)
                if (!i.IsActive) pool.Add(i);
            if (pool.Count == 0) pool.AddRange(Items);
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        public ItemDefinition PickBossItem()
        {
            var pool = new List<ItemDefinition>();
            foreach (var i in Items)
                if (i.Pool == ItemPool.Boss || i.Pool == ItemPool.Chest) pool.Add(i);
            if (pool.Count == 0) pool.AddRange(Items);
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        private static List<ItemDefinition> TakeRandom(List<ItemDefinition> src, int count)
        {
            var copy = new List<ItemDefinition>(src);
            var result = new List<ItemDefinition>();
            for (int i = 0; i < count && copy.Count > 0; i++)
            {
                int idx = Random.Range(0, copy.Count);
                result.Add(copy[idx]);
                copy.RemoveAt(idx);
            }
            return result;
        }
    }
}
