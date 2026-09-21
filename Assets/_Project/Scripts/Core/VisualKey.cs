namespace Klein
{
    /// <summary>
    /// 所有可视化槽位。每一项都可以在 `SpriteSet` 资产里换成正式美术图。
    ///
    /// 命名规则：`文件名 = 枚举名`。把 `Player_Body.png` 丢进 `Assets/_Project/Art/`，
    /// 再执行菜单「Klein/从 Art 文件夹自动填充贴图」就会自动挂上去。
    ///
    /// 分工：
    ///  · 全局通用的图（玩家、墙、门、地形、区域、掉落物、交互物）走 SpriteSet；
    ///  · 逐单位不一样的图走各自的数据资产
    ///    （`EnemyDefinition.BodySprite` / `ItemDefinition.IconSprite` /
    ///     `SpellDefinition.ProjectileSprite`）。
    /// </summary>
    public enum VisualKey
    {
        None = 0,

        // ---- 玩家 ----
        Player_Body,
        Player_Aim,

        // ---- 通用单位 ----
        Enemy_Body,            // 全局兜底（通常用 EnemyDefinition.BodySprite）
        Projectile_Player,
        Projectile_Enemy,
        Barrier,

        // ---- 地形（按元素）----
        Terrain_Water,
        Terrain_Fire,
        Terrain_Wood,
        Terrain_Depleted,      // 可选：次数耗尽时的替换图

        // ---- 场景 ----
        Wall,
        Door_Locked,
        Door_Open,

        // ---- 区域效果 ----
        Area_Steam,
        Area_Rot,
        Area_Heal,
        Area_Explosion,
        Area_Ring,

        // ---- 掉落物 ----
        Pickup_Element,
        Pickup_Health,
        Pickup_Money,

        // ---- 交互物 ----
        Pedestal_Base,
        Item_Default,          // 道具默认图（ItemDefinition.IconSprite 优先）
        Chest_Body,
        Chest_Lid,
        Spell_Card,
        Spell_CardGlow,

        // ---- 传送门 ----
        Portal_Core,
        Portal_Ring,
    }
}
