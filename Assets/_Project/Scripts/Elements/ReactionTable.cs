using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 反应类型。对应策划案 v2 的 6 条定向反应 + 同元素回充。
    /// </summary>
    public enum ReactionKind
    {
        None = 0,
        Recharge = 1,        // 同元素 → 地形反应次数 +1
        Pierce = 2,          // 法术穿过地形，伤害翻倍（水→火）
        PierceBurn = 3,      // 法术穿过地形，命中者灼伤（木→火）
        Explosion = 4,       // 以地形为中心爆炸（火→木）
        SteamArea = 5,       // 蒸汽区域，持续伤害（火→水）
        RotArea = 6,         // 腐烂区域，减速（水→木）
        HealArea = 7,        // 回血区域（木→水）
    }

    /// <summary>区域效果细分类。</summary>
    public enum AreaKind { None = 0, Steam = 1, Rot = 2, Heal = 3, Explosion = 4 }

    [System.Serializable]
    public class ReactionEntry
    {
        public ElementType SpellElement;
        public ElementType TerrainElement;
        public ReactionKind Kind;

        [Tooltip("穿透类反应的伤害倍率")]
        public float DamageMultiplier = 1f;

        [Header("区域 / 爆炸参数")]
        public float Radius = 2.5f;
        public float Duration = 6f;
        [Tooltip("每 tick 的伤害（Steam）")]
        public float TickDamage = 8f;
        [Tooltip("每 tick 的治疗（Heal）")]
        public float TickHeal = 5f;
        [Tooltip("减速倍率（Rot），1 = 不减速")]
        public float SlowFactor = 0.5f;
        [Tooltip("爆炸一次性伤害")]
        public float BurstDamage = 40f;
        public float BurstKnockback = 1.5f;

        [Header("灼伤（木→火）")]
        [Tooltip("每秒扣除最大生命值的百分比")]
        public float BurnPercentPerSecond = 5f;
        [Tooltip("对 BOSS 的灼伤百分比（策划案：BOSS 为 1%）")]
        public float BurnPercentBoss = 1f;
        public float BurnDuration = 4f;

        public string Describe()
        {
            switch (Kind)
            {
                case ReactionKind.Recharge: return "地形次数 +1";
                case ReactionKind.Pierce: return $"穿透，伤害 ×{DamageMultiplier:0.##}";
                case ReactionKind.PierceBurn: return $"穿透 + 灼伤 {BurnPercentPerSecond:0.#}%/s（{BurnDuration:0.#}s）";
                case ReactionKind.Explosion: return $"爆炸 {BurstDamage:0} 伤害，半径 {Radius:0.#}";
                case ReactionKind.SteamArea: return $"蒸汽区域 {TickDamage:0}/0.5s，半径 {Radius:0.#}，{Duration:0.#}s";
                case ReactionKind.RotArea: return $"腐烂区域 减速 ×{SlowFactor:0.##}，半径 {Radius:0.#}，{Duration:0.#}s";
                case ReactionKind.HealArea: return $"回血区域 {TickHeal:0}/0.5s，半径 {Radius:0.#}，{Duration:0.#}s";
                default: return "无";
            }
        }
    }

    /// <summary>
    /// 3×3 反应表。检索键为（法术元素, 地形元素）。
    /// 同元素不占表项，由 ElementReactionResolver 直接判为 Recharge。
    /// </summary>
    [CreateAssetMenu(menuName = "Klein/Reaction Table", fileName = "ReactionTable")]
    public class ReactionTable : ScriptableObject
    {
        public List<ReactionEntry> Entries = new List<ReactionEntry>();

        public ReactionEntry Find(ElementType spell, ElementType terrain)
        {
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].SpellElement == spell && Entries[i].TerrainElement == terrain)
                    return Entries[i];
            return null;
        }
    }
}
