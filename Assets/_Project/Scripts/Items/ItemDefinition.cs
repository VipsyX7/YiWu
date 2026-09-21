using UnityEngine;

namespace Klein
{
    public enum ItemKind { Active = 0, Passive = 1 }

    public enum ItemEffect
    {
        None = 0,
        MaxHealthUp = 1,
        MoveSpeedUp = 2,
        AttackUp = 3,
        CooldownUp = 4,
        LuckUp = 5,
        RangeUp = 6,
        TerrainChargeUp = 7,
        TripleShot = 8,
        ExplosiveRounds = 9,
        BloodPact = 10,
        HealPotion = 11,
        FlameSpit = 12,
    }

    /// <summary>道具池。策划案：「不同房间拥有不同道具池」。</summary>
    public enum ItemPool { Shop = 0, Chest = 1, Boss = 2, Any = 3 }

    /// <summary>
    /// 道具数据。主动 / 被动两类（策划案「道具系统」）。
    /// </summary>
    [CreateAssetMenu(menuName = "Klein/Item Definition", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string DisplayName = "道具";
        public ItemKind Kind = ItemKind.Passive;
        public ItemEffect Effect = ItemEffect.None;
        public ItemPool Pool = ItemPool.Chest;
        public float Magnitude = 10f;
        public Color Color = new Color(0.9f, 0.8f, 0.4f);
        [Tooltip("逐道具贴图。留空则用 SpriteSet 的 Item_Default")]
        public Sprite IconSprite;
        public string Description = "";

        [Header("主动道具")]
        public float ManaCost = 0f;
        public float Cooldown = 1f;
        public bool SingleUse = false;

        public bool IsActive => Kind == ItemKind.Active;
    }
}
