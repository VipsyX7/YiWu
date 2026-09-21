using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 运行时生成占位贴图。白盒阶段不引入任何外部美术资源。
    /// 1 张 16x16 贴图 + pixelsPerUnit=16 → 世界尺寸 1x1 格。
    /// </summary>
    public static class SpriteFactory
    {
        private const int PPU = 16;
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private static Material _mat;

        public static Material DefaultMaterial
        {
            get
            {
                if (_mat == null)
                {
                    var sh = Shader.Find("Sprites/Default");
                    if (sh == null) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    _mat = new Material(sh);
                    _mat.hideFlags = HideFlags.DontSave;
                }
                return _mat;
            }
        }

        public static Sprite Solid(Color color)
        {
            string key = "solid:" + ColorUtility.ToHtmlStringRGBA(color);
            if (_cache.TryGetValue(key, out var s)) return s;

            var tex = MakeTexture(PPU, PPU);
            var px = new Color[PPU * PPU];
            for (int i = 0; i < px.Length; i++) px[i] = color;
            tex.SetPixels(px);
            tex.Apply();
            return Cache(key, tex);
        }

        public static Sprite Circle(Color color)
        {
            string key = "circle:" + ColorUtility.ToHtmlStringRGBA(color);
            if (_cache.TryGetValue(key, out var s)) return s;

            const int N = 32;
            var tex = MakeTexture(N, N);
            var px = new Color[N * N];
            float r = N * 0.5f - 1f;
            float c = N * 0.5f - 0.5f;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(r - d);       // 1 像素羽化
                px[y * N + x] = new Color(color.r, color.g, color.b, color.a * a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return Cache(key, tex);
        }

        /// <summary>圆环，用于区域效果的范围指示。</summary>
        public static Sprite Ring(Color color)
        {
            string key = "ring:" + ColorUtility.ToHtmlStringRGBA(color);
            if (_cache.TryGetValue(key, out var s)) return s;

            const int N = 64;
            var tex = MakeTexture(N, N);
            var px = new Color[N * N];
            float outer = N * 0.5f - 1f;
            float inner = outer - 2.5f;
            float c = N * 0.5f - 0.5f;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                px[y * N + x] = new Color(color.r, color.g, color.b, color.a * a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return Cache(key, tex);
        }

        /// <summary>空心方块描边，用作墙体。</summary>
        public static Sprite Frame(Color fill, Color edge)
        {
            string key = "frame:" + ColorUtility.ToHtmlStringRGBA(fill) + ":" + ColorUtility.ToHtmlStringRGBA(edge);
            if (_cache.TryGetValue(key, out var s)) return s;

            var tex = MakeTexture(PPU, PPU);
            var px = new Color[PPU * PPU];
            for (int y = 0; y < PPU; y++)
            for (int x = 0; x < PPU; x++)
            {
                bool border = x == 0 || y == 0 || x == PPU - 1 || y == PPU - 1;
                px[y * PPU + x] = border ? edge : fill;
            }
            tex.SetPixels(px);
            tex.Apply();
            return Cache(key, tex);
        }

        private static Texture2D MakeTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.hideFlags = HideFlags.DontSave;
            return tex;
        }

        private static Sprite Cache(string key, Texture2D tex)
        {
            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                   new Vector2(0.5f, 0.5f), PPU);
            sp.hideFlags = HideFlags.DontSave;
            _cache[key] = sp;
            return sp;
        }
    }

    /// <summary>创建 SpriteRenderer 对象的统一入口。</summary>
    public static class Make
    {
        public static SpriteRenderer Sprite(string name, Transform parent, Vector3 localPos,
                                            Sprite sprite, Color color, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sharedMaterial = SpriteFactory.DefaultMaterial;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        /// <summary>纯色方块，恰好占满 size（世界单位）。</summary>
        public static SpriteRenderer Square(string name, Transform parent, Vector3 localPos,
                                            Color color, Vector2 size, int sortingOrder = 0)
        {
            var sp = SpriteFactory.Solid(Color.white);
            var sr = Sprite(name, parent, localPos, sp, color, sortingOrder);
            sr.transform.localScale = Visuals.FitScale(sp, size);
            return sr;
        }

        /// <summary>纯色圆，直径 = diameter（世界单位）。</summary>
        public static SpriteRenderer Disc(string name, Transform parent, Vector3 localPos,
                                          Color color, float diameter, int sortingOrder = 0)
        {
            var sp = SpriteFactory.Circle(Color.white);
            var sr = Sprite(name, parent, localPos, sp, color, sortingOrder);
            sr.transform.localScale = Visuals.FitScale(sp, new Vector2(diameter, diameter));
            return sr;
        }

        /// <summary>圆环，外径 = diameter（世界单位）。</summary>
        public static SpriteRenderer Ring(string name, Transform parent, Vector3 localPos,
                                          Color color, float diameter, int sortingOrder = 0)
        {
            var sp = SpriteFactory.Ring(Color.white);
            var sr = Sprite(name, parent, localPos, sp, color, sortingOrder);
            sr.transform.localScale = Visuals.FitScale(sp, new Vector2(diameter, diameter));
            return sr;
        }

        /// <summary>
        /// 支持贴图替换的创建入口：有正式美术用美术图，没有就用 placeholder，
        /// 并按 desiredSize 归一化尺寸。
        /// </summary>
        public static SpriteRenderer Visual(string name, Transform parent, Vector3 localPos,
                                            VisualKey key, Sprite placeholder, Color placeholderTint,
                                            Vector2 desiredSize, int sortingOrder = 0)
            => Visuals.Create(name, parent, localPos, key, placeholder, placeholderTint, desiredSize, sortingOrder);

        /// <summary>直接用一张指定的图（逐单位贴图：怪物/道具/法术），并归一化到 size。</summary>
        public static SpriteRenderer Preview(string name, Transform parent, Sprite sprite,
                                             Vector2 size, int sortingOrder = 0)
        {
            var sr = Sprite(name, parent, Vector3.zero, sprite, Color.white, sortingOrder);
            sr.transform.localScale = Visuals.FitScale(sprite, size);
            return sr;
        }
    }
}
