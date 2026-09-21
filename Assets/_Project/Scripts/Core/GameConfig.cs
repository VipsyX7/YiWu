using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 全局占位数值。来源：Docs/策划案v2_评审与数值基准.md 第二部分。
    /// 白盒阶段唯一目的＝让机制跑起来，不追求平衡。
    /// </summary>
    [CreateAssetMenu(menuName = "Klein/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("玩家属性（策划案「玩家属性系统」）")]
        public float MaxHealth = 100f;
        public float MaxMana = 100f;
        public float MoveSpeed = 5f;
        [Tooltip("百分比乘区：最终伤害 = 法术基础伤害 × 攻击力%")]
        public float AttackPower = 100f;
        [Tooltip("百分比除区：实际CD = 法术基础CD ÷ (法术冷却% / 100)")]
        public float SpellCooldown = 100f;
        public float Luck = 0f;
        [Tooltip("施法距离（弹体最大飞行距离）")]
        public float CastRange = 8f;
        [Tooltip("法术飞行速度")]
        public float SpellSpeed = 12f;

        [Header("玩家其他参数（案外补充，白盒必需）")]
        public float PlayerRadius = 0.4f;
        public float PlayerInvincibleTime = 0.5f;
        public float PickupRadius = 0.8f;
        public float PickupFlySpeed = 8f;
        public float StartMoney = 0f;
        public float PlayerKnockback = 2f;
        public float PlayerKnockbackTime = 0.15f;

        [Header("地形元素反应（策划案 v2「元素系统」）")]
        [Tooltip("每个地形初始可发生的元素反应次数")]
        public int TerrainInitialCharges = 3;
        [Tooltip("同元素法术撞同元素地形时的次数上限")]
        public int TerrainMaxCharges = 5;
        [Tooltip("地形反应次数的自动回充间隔（秒）：每块地形每 5 秒自行 +1 次，封顶上限")]
        public float TerrainRechargeInterval = 5f;
        [Tooltip("单次法术与同一地形每 N 帧最多产生一次反应")]
        public int ReactionCooldownFrames = 60;
        [Tooltip("区域效果统一 tick 间隔")]
        public float AreaTickInterval = 0.5f;

        [Header("掉落物")]
        public float PickupMana = 10f;
        public float PickupHealth = 15f;
        public float PickupMoney = 5f;

        [Header("关卡（以撒式网格，全部为占位）")]
        public int FloorCount = 5;
        public int MinRoomsPerFloor = 10;
        public int MaxRoomsPerFloor = 14;
        [Tooltip("房间可玩区宽度（格）")]
        public int RoomWidth = 15;
        [Tooltip("房间可玩区高度（格）")]
        public int RoomHeight = 9;
        [Tooltip("墙体厚度（格）")]
        public int WallThickness = 1;
        [Tooltip("网格生成器使用的最大尺寸")]
        public int GridSize = 5;
        [Tooltip("单房间清怪超时（秒），超时追加刷一波")]
        public float RoomClearTimeout = 90f;
        [Tooltip("单房间怪物总数上限（含加刷）")]
        public int RoomSpawnCap = 24;

        [Header("摄像机")]
        public float CameraSize = 7f;
        public float CameraSmooth = 0.12f;

        [Header("经济与商店")]
        public int ShopPriceMin = 15;
        public int ShopPriceMax = 25;
    }
}
