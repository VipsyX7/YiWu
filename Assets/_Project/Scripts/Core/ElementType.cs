using System;
using UnityEngine;

namespace Klein
{
    /// <summary>木 / 水 / 火 三元素。对应策划案 v2「元素系统」。</summary>
    public enum ElementType
    {
        None = 0,
        Water = 1,
        Fire = 2,
        Wood = 3,
    }

    /// <summary>
    /// 弹体携带的元素集合。
    /// 决策 Q1（用户已拍板）：法术固有元素与玩家当前元素【两者叠加】，
    /// 因此一个弹体可以同时带有两种元素（例如「固有火 + 玩家水」）。
    /// </summary>
    [Flags]
    public enum ElementMask
    {
        None = 0,
        Water = 1 << 0,
        Fire = 1 << 1,
        Wood = 1 << 2,
    }

    public static class ElementUtil
    {
        public static ElementMask ToMask(this ElementType e)
        {
            switch (e)
            {
                case ElementType.Water: return ElementMask.Water;
                case ElementType.Fire: return ElementMask.Fire;
                case ElementType.Wood: return ElementMask.Wood;
                default: return ElementMask.None;
            }
        }

        public static bool Has(this ElementMask m, ElementType e)
        {
            return e != ElementType.None && (m & e.ToMask()) != 0;
        }

        /// <summary>把 mask 拆成按「固有元素优先」排序的元素列表，用于反应仲裁。</summary>
        public static int Count(this ElementMask m)
        {
            int n = 0;
            if ((m & ElementMask.Water) != 0) n++;
            if ((m & ElementMask.Fire) != 0) n++;
            if ((m & ElementMask.Wood) != 0) n++;
            return n;
        }

        public static Color ToColor(this ElementType e)
        {
            switch (e)
            {
                case ElementType.Water: return new Color(0.20f, 0.45f, 0.90f);
                case ElementType.Fire: return new Color(0.92f, 0.36f, 0.14f);
                case ElementType.Wood: return new Color(0.24f, 0.66f, 0.30f);
                default: return new Color(0.55f, 0.55f, 0.62f);
            }
        }

        public static string ToCn(this ElementType e)
        {
            switch (e)
            {
                case ElementType.Water: return "水";
                case ElementType.Fire: return "火";
                case ElementType.Wood: return "木";
                default: return "无";
            }
        }
    }
}
