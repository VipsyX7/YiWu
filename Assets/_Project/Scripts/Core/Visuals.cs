using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 贴图解析：**优先用 SpriteSet 里的正式美术，没有就回退到运行时生成的占位图**。
    ///
    /// 还负责一件容易被忽略的事：**尺寸归一化**。
    /// 占位图内部尺寸并不统一（方块 16px、圆形 32px、圆环 64px，PPU 都是 16，
    /// 即分别是 1×1 / 2×2 / 4×4 单位），而正式美术的像素尺寸更是千奇百怪。
    /// 所以这里统一按 `sprite.bounds` 反算 localScale，让图恰好占满目标尺寸。
    /// </summary>
    public static class Visuals
    {
        public const string ResourcePath = "Klein/SpriteSet";

        public static SpriteSet Set { get; private set; }
        public static bool Loaded { get; private set; }

        /// <summary>从 Resources 加载贴图表。没有资产时 Set 为 null，全部走占位图。</summary>
        public static void Load(bool force = false)
        {
            if (Loaded && !force) return;
            Loaded = true;
            Set = Resources.Load<SpriteSet>(ResourcePath);
        }

        /// <summary>测试注入用。</summary>
        public static void OverrideForTest(SpriteSet set)
        {
            Set = set;
            Loaded = true;
        }

        // ------------------------------------------------------------ 取图

        public static Sprite Get(VisualKey key, Sprite placeholder)
        {
            if (key == VisualKey.None) return placeholder;
            Load();
            var s = Set != null ? Set.Get(key) : null;
            return s != null ? s : placeholder;
        }

        public static bool Has(VisualKey key)
        {
            if (key == VisualKey.None) return false;
            Load();
            return Set != null && Set.Get(key) != null;
        }

        /// <summary>
        /// 占位色 → 实际颜色。
        /// 有正式美术时返回白色（保留 alpha，因为透明度还承担状态表达，例如地形剩余次数），
        /// 没有美术时原样返回占位色。
        /// </summary>
        public static Color Tint(VisualKey key, Color placeholderTint)
        {
            if (!Has(key)) return placeholderTint;
            return new Color(1f, 1f, 1f, placeholderTint.a);
        }

        // ------------------------------------------------------------ 尺寸

        /// <summary>让 sprite 恰好占据 desiredSize（世界单位）所需的 localScale。</summary>
        public static Vector3 FitScale(Sprite sprite, Vector2 desiredSize)
        {
            if (sprite == null) return new Vector3(desiredSize.x, desiredSize.y, 1f);

            var b = sprite.bounds.size;
            float sx = b.x > 1e-4f ? desiredSize.x / b.x : desiredSize.x;
            float sy = b.y > 1e-4f ? desiredSize.y / b.y : desiredSize.y;
            return new Vector3(sx, sy, 1f);
        }

        /// <summary>考虑 SpriteSet 里 FitToSize 开关的缩放计算。</summary>
        public static Vector3 ScaleFor(VisualKey key, Sprite sprite, Vector2 desiredSize)
        {
            if (key != VisualKey.None)
            {
                Load();
                if (Set != null && Set.Has(key) && !Set.Fits(key)) return Vector3.one;
            }
            return FitScale(sprite, desiredSize);
        }

        // ------------------------------------------------------------ 便利方法

        /// <summary>创建带贴图替换能力的 SpriteRenderer。</summary>
        public static SpriteRenderer Create(string name, Transform parent, Vector3 localPos,
                                            VisualKey key, Sprite placeholder, Color placeholderTint,
                                            Vector2 desiredSize, int sortingOrder = 0)
        {
            var sprite = Get(key, placeholder);
            var tint = Tint(key, placeholderTint);
            var sr = Make.Sprite(name, parent, localPos, sprite, tint, sortingOrder);
            sr.transform.localScale = ScaleFor(key, sprite, desiredSize);
            return sr;
        }

        /// <summary>就地换图并重新归一化尺寸（用于状态切换，例如门开 / 关、地形耗尽）。</summary>
        public static void Apply(SpriteRenderer sr, VisualKey key, Sprite placeholder,
                                 Color placeholderTint, Vector2 desiredSize)
        {
            if (sr == null) return;
            var sprite = Get(key, placeholder);
            sr.sprite = sprite;
            sr.color = Tint(key, placeholderTint);
            sr.transform.localScale = ScaleFor(key, sprite, desiredSize);
        }

        // ------------------------------------------------------------ key 映射

        public static VisualKey TerrainKey(ElementType e)
        {
            switch (e)
            {
                case ElementType.Water: return VisualKey.Terrain_Water;
                case ElementType.Fire: return VisualKey.Terrain_Fire;
                case ElementType.Wood: return VisualKey.Terrain_Wood;
                default: return VisualKey.None;
            }
        }

        public static VisualKey PickupKey(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.Element: return VisualKey.Pickup_Element;
                case PickupKind.Health: return VisualKey.Pickup_Health;
                default: return VisualKey.Pickup_Money;
            }
        }

        public static VisualKey AreaKey(AreaKind kind)
        {
            switch (kind)
            {
                case AreaKind.Steam: return VisualKey.Area_Steam;
                case AreaKind.Rot: return VisualKey.Area_Rot;
                case AreaKind.Heal: return VisualKey.Area_Heal;
                case AreaKind.Explosion: return VisualKey.Area_Explosion;
                default: return VisualKey.None;
            }
        }
    }
}
