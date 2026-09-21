using System;
using UnityEngine;

namespace Klein
{
    public enum DamageSource
    {
        Projectile,
        AreaEffect,
        Burn,
        Explosion,
        Contact,
    }

    public struct DamageInfo
    {
        public float Amount;
        /// <summary>造成此次伤害的元素（可为 None）。</summary>
        public ElementType Element;
        public DamageSource Source;
        /// <summary>伤害来源世界坐标，用于击退方向。</summary>
        public Vector2 Origin;
        public float Knockback;
        /// <summary>施加者。玩家造成的伤害会触发「首伤转追踪」。</summary>
        public GameObject Instigator;
        /// <summary>是否由玩家一方造成（决定是否触发怪物仇恨）。</summary>
        public bool FromPlayer;

        public static DamageInfo Player(float amount, ElementType element, Vector2 origin,
                                        DamageSource source = DamageSource.Projectile, float knockback = 0f)
        {
            return new DamageInfo
            {
                Amount = amount, Element = element, Source = source,
                Origin = origin, Knockback = knockback, FromPlayer = true,
            };
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        Transform Transform { get; }
        void TakeDamage(in DamageInfo info);
    }

    /// <summary>全局战斗事件。用于「玩家在本房间首次造成伤害后怪物转为追踪」。</summary>
    public static class CombatEvents
    {
        /// <summary>玩家对任意单位造成伤害时触发，参数为受击者世界坐标。</summary>
        public static event Action<Vector2> PlayerDealtDamage;

        public static void RaisePlayerDealtDamage(Vector2 victimPosition)
        {
            PlayerDealtDamage?.Invoke(victimPosition);
        }
    }
}
