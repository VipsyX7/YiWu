using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    public enum MoveMode
    {
        Continuous = 0,   // 持续移动
        Intermittent = 1, // 间歇移动
        Stationary = 2,   // 不移动
    }

    public enum EnemyShape { Square = 0, Circle = 1, Triangle = 2 }

    /// <summary>
    /// 怪物数据。字段严格对照策划案 v2「怪物系统」中要求的怪物编辑器：
    ///   血量 / 移速 / 射速 / 子弹速度 / 伤害 / 同一房间此单位最大存在数
    ///   / 掉落物种类 / 掉落物数量 / 掉落物权重 / 移动方式
    /// 其余为白盒补充字段（已标注）。
    /// </summary>
    [CreateAssetMenu(menuName = "Klein/Enemy Definition", fileName = "Enemy")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("★ 策划案怪物编辑器字段")]
        public float Health = 60f;
        public float MoveSpeed = 2f;
        [Tooltip("每秒发射次数。0 表示近战单位")]
        public float FireRate = 0f;
        public float BulletSpeed = 6f;
        public float Damage = 12f;
        [Tooltip("同一房间此单位最大存在数")]
        public int MaxInRoom = 3;

        [Tooltip("掉落物种类（与 DropWeights 一一对应）")]
        public List<PickupKind> DropTypes = new List<PickupKind>();
        [Tooltip("掉落物数量区间 [min, max]")]
        public Vector2Int DropCount = new Vector2Int(1, 1);
        [Tooltip("掉落物权重（与 DropTypes 一一对应，总和 100）")]
        public List<float> DropWeights = new List<float>();
        public MoveMode MoveMode = MoveMode.Continuous;

        [Header("○ 白盒补充字段")]
        public string DisplayName = "怪物";
        [Tooltip("逐怪物贴图。留空则用 SpriteSet 的 Enemy_Body，再留空就用下面的形状色块")]
        public Sprite BodySprite;
        public float BodyRadius = 0.4f;
        public bool IsBoss = false;
        [Tooltip("关卡生成预算里此单位消耗的权重")]
        public float CostWeight = 1f;
        [Tooltip("远程单位保持的理想距离")]
        public float PreferRange = 6f;
        public Color BodyColor = new Color(0.85f, 0.35f, 0.35f);
        public EnemyShape Shape = EnemyShape.Square;
        [Tooltip("近战接触伤害冷却")]
        public float ContactCooldown = 1f;
        [Tooltip("接触伤害的判定距离")]
        public float ContactRange = 0.9f;
        [Tooltip("未激怒时改变游走方向的时间间隔")]
        public float WanderInterval = 1.5f;
        [Tooltip("间歇移动的移动/停顿时长")]
        public Vector2 IntermittentPattern = new Vector2(0.8f, 0.8f);

        public float TotalDropWeight
        {
            get
            {
                float s = 0f;
                for (int i = 0; i < DropWeights.Count; i++) s += DropWeights[i];
                return s;
            }
        }
    }
}
