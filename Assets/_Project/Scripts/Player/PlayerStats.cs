using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 玩家属性。字段对照策划案「玩家属性系统」：
    /// 血量 / 法力 / 移速 / 攻击力 / 法术冷却 / 幸运值 / 施法距离 / 法术飞行速度。
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("基础属性（由 GameConfig 初始化）")]
        public float MaxHealth = 100f;
        public float MaxMana = 100f;
        public float MoveSpeed = 5f;
        [Tooltip("百分比乘区")]
        public float AttackPower = 100f;
        [Tooltip("百分比除区：越高冷却越短")]
        public float SpellCooldown = 100f;
        public float Luck = 0f;
        public float CastRange = 8f;
        public float SpellSpeed = 12f;

        [Header("当前元素（祭祀场所选，决策 Q1：会附加到法术上）")]
        public ElementType Element = ElementType.Water;

        [Header("运行时资源")]
        public float Mana = 100f;
        public int Money = 0;

        public readonly List<ItemDefinition> Items = new List<ItemDefinition>();

        private readonly List<ItemDefinition> _passives = new List<ItemDefinition>();

        public void ApplyConfig(GameConfig cfg)
        {
            MaxHealth = cfg.MaxHealth;
            MaxMana = cfg.MaxMana;
            MoveSpeed = cfg.MoveSpeed;
            AttackPower = cfg.AttackPower;
            SpellCooldown = cfg.SpellCooldown;
            Luck = cfg.Luck;
            CastRange = cfg.CastRange;
            SpellSpeed = cfg.SpellSpeed;
            Mana = cfg.MaxMana;
            Money = Mathf.RoundToInt(cfg.StartMoney);
        }

        public void SpendMana(float amount) => Mana = Mathf.Max(0f, Mana - amount);

        public void AddMana(float amount) => Mana = Mathf.Min(MaxMana, Mana + amount);

        public void AddHealth(float amount)
        {
            var h = GetComponent<PlayerHealth>();
            if (h != null) h.Heal(amount);
        }

        public void AddMoney(int amount) => Money = Mathf.Max(0, Money + amount);

        public bool TrySpend(int amount)
        {
            if (Money < amount) return false;
            Money -= amount;
            return true;
        }

        public void ApplyItem(ItemDefinition item)
        {
            if (item == null) return;
            Items.Add(item);
            if (item.Kind == ItemKind.Passive) _passives.Add(item);

            switch (item.Effect)
            {
                case ItemEffect.MaxHealthUp: MaxHealth += item.Magnitude; break;
                case ItemEffect.MoveSpeedUp: MoveSpeed += item.Magnitude; break;
                case ItemEffect.AttackUp: AttackPower += item.Magnitude; break;
                case ItemEffect.CooldownUp: SpellCooldown += item.Magnitude; break;
                case ItemEffect.LuckUp: Luck += item.Magnitude; break;
                case ItemEffect.RangeUp: CastRange += item.Magnitude; break;
                case ItemEffect.TerrainChargeUp:
                {
                    var eg = GameRuntime.I != null ? GameRuntime.I.Elements : null;
                    if (eg != null)
                    {
                        int add = Mathf.RoundToInt(item.Magnitude);
                        eg.MaxCharges += add;
                        foreach (var b in eg.AllBlobs) b.MaxCharges += add;
                    }
                    break;
                }
            }
        }

        public bool HasEffect(ItemEffect e)
        {
            for (int i = 0; i < _passives.Count; i++)
                if (_passives[i] != null && _passives[i].Effect == e) return true;
            return false;
        }
    }
}
