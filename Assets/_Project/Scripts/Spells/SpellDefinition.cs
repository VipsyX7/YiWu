using UnityEngine;

namespace Klein
{
    public enum SpellShape
    {
        Projectile = 0,   // 弹体（魔弹）
        Barrier = 1,      // 驻留屏障（防护罩）
        Melee = 2,        // 近战扇形（斩击）
        Beam = 3,         // 持续束（射线）
        Burst = 4,        // 范围爆发（冲击波）
    }

    /// <summary>
    /// 单个法术的数据。对应策划案 v2「法术系统」。
    /// 数值来源：Docs/策划案v2_评审与数值基准.md §2.3。
    /// </summary>
    [CreateAssetMenu(menuName = "Klein/Spell Definition", fileName = "Spell")]
    public class SpellDefinition : ScriptableObject
    {
        public string DisplayName = "法术";
        public SpellShape Shape = SpellShape.Projectile;

        [Tooltip("法术固有元素。None = 无固有元素，只吃玩家附加元素（决策 Q1）")]
        public ElementType InnateElement = ElementType.None;

        public float Damage = 12f;
        public float Cooldown = 0.5f;
        [Tooltip("弹体最大飞行距离")]
        public float Range = 8f;
        public float Speed = 12f;
        public float Radius = 0.25f;
        [Tooltip("可穿透的敌人数")]
        public int Pierce = 0;
        public float Knockback = 0f;
        public float ManaCost = 0f;

        [Header("驻留类（Barrier）")]
        public float BarrierHealth = 30f;
        public float BarrierDuration = 3f;

        public Color Color = new Color(1f, 0.85f, 0.4f);

        [Tooltip("弹体贴图。留空则用 SpriteSet 的 Projectile_Player / Projectile_Enemy")]
        public Sprite ProjectileSprite;

        public bool IsSupport => Shape == SpellShape.Barrier;
    }
}
