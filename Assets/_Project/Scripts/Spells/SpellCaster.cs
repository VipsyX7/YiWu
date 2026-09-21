using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 法术施放。6 个法术槽对应 Q W E A S D（策划案 v2 已明确为六个键）。
    /// 修正器结算：按队列顺序作用于末尾的法术。
    /// 元素：spell.InnateElement（固有）+ stats.Element（玩家当前）→ 双元素叠加（决策 Q1）。
    /// </summary>
    public class SpellCaster : MonoBehaviour
    {
        public const int SlotCount = 6;

        public SpellCard[] Slots = new SpellCard[SlotCount];
        public readonly List<SpellCard> Inventory = new List<SpellCard>();

        private readonly float[] _cd = new float[SlotCount];
        private PlayerStats _stats;
        private PlayerHealth _health;

        public void Bind(PlayerStats stats, PlayerHealth health)
        {
            _stats = stats;
            _health = health;
            for (int i = 0; i < SlotCount; i++)
                if (Slots[i] == null) Slots[i] = new SpellCard();
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < SlotCount; i++)
                if (_cd[i] > 0f) _cd[i] = Mathf.Max(0f, _cd[i] - dt);
        }

        public bool CanCast(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return false;
            var c = Slots[slot];
            if (c == null || c.IsEmpty) return false;
            if (_cd[slot] > 0f) return false;
            if (c.Spell.ManaCost > 0f && _stats != null && _stats.Mana < c.Spell.ManaCost) return false;
            return true;
        }

        public float CooldownRemaining(int slot) => (slot >= 0 && slot < SlotCount) ? _cd[slot] : 0f;
        public float Cooldown01(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return 0f;
            var c = Slots[slot];
            if (c == null || c.IsEmpty || c.Spell.Cooldown <= 0f) return 0f;
            return Mathf.Clamp01(_cd[slot] / EffectiveCooldown(c));
        }

        private float EffectiveCooldown(SpellCard c)
        {
            float cdScale = 1f;
            for (int i = 0; i < c.Modifiers.Count; i++)
                cdScale *= Mathf.Max(0.05f, c.Modifiers[i].CooldownScale);
            float stat = _stats != null ? Mathf.Max(10f, _stats.SpellCooldown) / 100f : 1f;
            return c.Spell.Cooldown * cdScale / stat;
        }

        public bool TryCast(int slot, Vector2 aimWorld)
        {
            if (!CanCast(slot)) return false;
            var card = Slots[slot];
            var spell = card.Spell;

            // ---- 修正器结算 ----
            float dmgScale = 1f;
            float cdScale = 1f;
            int refract = 0;
            float terrainRadius = 0f;
            bool originOverride = false;
            ElementType enchantElement = ElementType.None;

            for (int i = 0; i < card.Modifiers.Count; i++)
            {
                var m = card.Modifiers[i];
                if (m == null) continue;
                dmgScale *= Mathf.Max(0f, m.DamageScale <= 0f ? 1f : m.DamageScale);
                cdScale *= Mathf.Max(0.05f, m.CooldownScale <= 0f ? 1f : m.CooldownScale);
                switch (m.Kind)
                {
                    case ModifierKind.Refract:
                        refract += Mathf.Max(1, Mathf.RoundToInt(m.Magnitude));
                        break;
                    case ModifierKind.Amplify:
                        dmgScale *= 2f;
                        cdScale *= 1.5f;
                        break;
                    case ModifierKind.Origin:
                        originOverride = true;
                        break;
                    case ModifierKind.Enchant:
                        enchantElement = _stats != null ? _stats.Element : ElementType.None;
                        break;
                    case ModifierKind.TerrainGen:
                        terrainRadius = Mathf.Max(1f, m.Magnitude);
                        break;
                }
            }

            Vector2 origin = originOverride ? aimWorld : (Vector2)transform.position;
            Vector2 dir = aimWorld - origin;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.right;
            dir.Normalize();

            float atk = _stats != null ? _stats.AttackPower / 100f : 1f;
            float damage = spell.Damage * dmgScale * atk;

            ElementType innate = spell.InnateElement;
            ElementType playerEl = enchantElement != ElementType.None
                ? enchantElement
                : (_stats != null ? _stats.Element : ElementType.None);

            _cd[slot] = EffectiveCooldown(card);
            if (spell.ManaCost > 0f && _stats != null) _stats.SpendMana(spell.ManaCost);

            switch (spell.Shape)
            {
                case SpellShape.Barrier:
                    Barrier.Create(transform, origin, spell, innate != ElementType.None ? innate : playerEl,
                                   Mathf.RoundToInt(spell.BarrierHealth));
                    break;

                case SpellShape.Projectile:
                default:
                {
                    var mask = innate.ToMask() | (GameRuntime.AttachPlayerElement ? playerEl.ToMask() : ElementMask.None);
                    var spec = new ProjectileSpec
                    {
                        Mask = mask,
                        Innate = innate,
                        PlayerElement = playerEl,
                        Damage = damage,
                        Speed = spell.Speed > 0f ? spell.Speed : (_stats != null ? _stats.SpellSpeed : 12f),
                        Range = spell.Range > 0f ? spell.Range : (_stats != null ? _stats.CastRange : 8f),
                        Radius = spell.Radius,
                        Knockback = spell.Knockback,
                        FromPlayer = true,
                        Pierce = spell.Pierce + refract,
                        Color = TintColor(spell, innate, playerEl),
                        Sprite = spell.ProjectileSprite,
                    };
                    Projectile.Spawn(origin, dir, spec, ProjectileTeam.Player);
                    break;
                }
            }

            // 地形生成修正器：在鼠标处生成一块地形
            if (terrainRadius > 0f) SpawnTerrain(aimWorld, terrainRadius, innate != ElementType.None ? innate : playerEl);

            return true;
        }

        private static Color TintColor(SpellDefinition spell, ElementType innate, ElementType playerEl)
        {
            var e = innate != ElementType.None ? innate : playerEl;
            return e != ElementType.None ? e.ToColor() : spell.Color;
        }

        private void SpawnTerrain(Vector2 center, float radius, ElementType element)
        {
            if (element == ElementType.None) return;
            var eg = GameRuntime.I != null ? GameRuntime.I.Elements : null;
            var floor = GameRuntime.I != null ? GameRuntime.I.Floor : null;
            if (eg == null || floor == null) return;

            var c0 = LevelGrid.WorldToCell(center);
            int r = Mathf.RoundToInt(radius);
            var cells = new List<Vector2Int>();

            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > r * r) continue;
                var c = c0 + new Vector2Int(dx, dy);
                if (!floor.Layout.Grid.IsWalkable(c)) continue;
                if (eg.HasTerrain(c)) continue;
                cells.Add(c);
            }

            // 生成出来的地形同样是一整块，共享一份反应次数
            if (cells.Count > 0) eg.CreateBlob(cells, element, GameRuntime.I.TerrainRoot);
        }

        // ---------------------------------------------------------- 背包

        public void AddToInventory(SpellCard card)
        {
            if (card == null || card.IsEmpty) return;
            Inventory.Add(card);
        }

        public bool AssignSlot(int slot, int inventoryIndex)
        {
            if (slot < 0 || slot >= SlotCount) return false;
            if (inventoryIndex < 0 || inventoryIndex >= Inventory.Count) return false;
            var card = Inventory[inventoryIndex];
            Inventory.RemoveAt(inventoryIndex);
            if (Slots[slot] != null && !Slots[slot].IsEmpty) Inventory.Add(Slots[slot]);
            Slots[slot] = card;
            return true;
        }

        public void ClearSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;
            if (Slots[slot] != null && !Slots[slot].IsEmpty) Inventory.Add(Slots[slot]);
            Slots[slot] = new SpellCard();
        }

        public void Grant(SpellDefinition spell, params SpellModifierDefinition[] mods)
        {
            var card = new SpellCard { Spell = spell };
            if (mods != null) card.Modifiers.AddRange(mods);
            AddToInventory(card);
        }

        /// <summary>无参构造函数之外，给初始槽位用。</summary>
        public void SetSlot(int slot, SpellDefinition spell)
        {
            if (slot < 0 || slot >= SlotCount) return;
            Slots[slot] = new SpellCard { Spell = spell };
        }
    }
}
