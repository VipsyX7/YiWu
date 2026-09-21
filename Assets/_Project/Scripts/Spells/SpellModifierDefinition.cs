using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    public enum ModifierKind
    {
        Refract = 0,      // 折射镜
        Amplify = 1,      // 法术增幅
        Enchant = 2,      // 法术附魔
        Origin = 3,       // 原点
        TerrainGen = 4,   // 地形生成
    }

    /// <summary>
    /// 法术修正器。对应策划案 v2 列出的 5 个进阶法术。
    /// 白盒只实装「折射镜」用于验证修正器架构（见可行性分析 R1）。
    /// </summary>
    [CreateAssetMenu(menuName = "Klein/Spell Modifier", fileName = "Modifier")]
    public class SpellModifierDefinition : ScriptableObject
    {
        public string DisplayName = "修正器";
        public ModifierKind Kind = ModifierKind.Refract;
        [Tooltip("折射次数 / 增幅倍率 / 生成地形半径等，含义随 Kind 变化")]
        public float Magnitude = 1f;
        public float DamageScale = 1f;
        public float CooldownScale = 1f;
        public Color Color = new Color(0.7f, 0.6f, 1f);
    }

    /// <summary>
    /// 一个法术槽的内容：一串修正器 + 末尾 1 个法术。
    /// 白盒假设 A12：槽位是有序队列，修正器作用于其后第一个法术（《noita》式）。
    /// </summary>
    [System.Serializable]
    public class SpellCard
    {
        public SpellDefinition Spell;
        public List<SpellModifierDefinition> Modifiers = new List<SpellModifierDefinition>();

        public bool IsEmpty => Spell == null;

        public string Label()
        {
            if (IsEmpty) return "—";
            if (Modifiers.Count == 0) return Spell.DisplayName;
            return Modifiers[0].DisplayName + (Modifiers.Count > 1 ? $"+{Modifiers.Count - 1}" : "")
                   + "→" + Spell.DisplayName;
        }
    }
}
