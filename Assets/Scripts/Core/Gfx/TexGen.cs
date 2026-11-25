using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>值噪声（线程安全，纯数学）。</summary>
    public static class Noise
    {
        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177 + seed * 951274213);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float S(float t) => t * t * (3 - 2 * t);

        public static float Value2(float x, float y, int seed = 0, int period = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = S(x - x0), fy = S(y - y0);
            int x1 = x0 + 1, y1 = y0 + 1;
            if (period > 0) { x0 = Mod(x0, period); x1 = Mod(x1, period); y0 = Mod(y0, period); y1 = Mod(y1, period); }
            float a = Hash(x0, y0, 0, seed), b = Hash(x1, y0, 0, seed), c = Hash(x0, y1, 0, seed), d = Hash(x1, y1, 0, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Value3(Vector3 p, int seed = 0)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = S(p.x - x0), fy = S(p.y - y0), fz = S(p.z - z0);
            float c000 = Hash(x0, y0, z0, seed), c100 = Hash(x0 + 1, y0, z0, seed), c010 = Hash(x0, y0 + 1, z0, seed), c110 = Hash(x0 + 1, y0 + 1, z0, seed);
            float c001 = Hash(x0, y0, z0 + 1, seed), c101 = Hash(x0 + 1, y0, z0 + 1, seed), c011 = Hash(x0, y0 + 1, z0 + 1, seed), c111 = Hash(x0 + 1, y0 + 1, z0 + 1, seed);
            float x00 = Mathf.Lerp(c000, c100, fx), x10 = Mathf.Lerp(c010, c110, fx), x01 = Mathf.Lerp(c001, c101, fx), x11 = Mathf.Lerp(c011, c111, fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        public static float Fbm2(float x, float y, int oct = 4, int seed = 0, int period = 0)
        {
            float s = 0, a = 0.5f, tot = 0;
            for (int i = 0; i < oct; i++)
            {
                s += Value2(x, y, seed + i * 17, period) * a;
                tot += a; a *= 0.5f; x *= 2; y *= 2; if (period > 0) period *= 2;
            }
            return s / tot;
        }

        public static float Fbm3(Vector3 p, int oct = 4, int seed = 0)
        {
            float s = 0, a = 0.5f, tot = 0;
            for (int i = 0; i < oct; i++) { s += Value3(p, seed + i * 31) * a; tot += a; a *= 0.5f; p *= 2; }
            return s / tot;
        }

        /// <summary>Voronoi 细胞距离（可平铺）。</summary>
        public static float Cells(float x, float y, int seed, int period, out float id)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float best = 9, second = 9; id = 0;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int cx = xi + i, cy = yi + j;
                int hx = period > 0 ? Mod(cx, period) : cx, hy = period > 0 ? Mod(cy, period) : cy;
                float px = cx + Hash(hx, hy, 1, seed), py = cy + Hash(hx, hy, 2, seed);
                float d = (px - x) * (px - x) + (py - y) * (py - y);
                if (d < best) { second = best; best = d; id = Hash(hx, hy, 3, seed); }
                else if (d < second) second = d;
            }
            return Mathf.Sqrt(second) - Mathf.Sqrt(best);
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;
    }

    /// <summary>程序化贴图（反照率 + 法线），结果缓存。</summary>
    public static class TexGen
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string key, Func<Texture2D> make)
        {
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            t = make();
            t.name = key;
            Cache[key] = t;
            return t;
        }

        public static Texture2D Make(int w, int h, Func<float, float, Color> f, bool linear = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, linear) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>由高度函数生成法线贴图（可平铺：高度函数需在 0~1 周期内连续）。</summary>
        public static Texture2D Normal(int w, int h, Func<float, float, float> height, float strength)
        {
            var hm = new float[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                hm[y * w + x] = height((x + 0.5f) / w, (y + 0.5f) / h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float l = hm[y * w + (x + w - 1) % w], r = hm[y * w + (x + 1) % w];
                float d = hm[((y + h - 1) % h) * w + x], u = hm[((y + 1) % h) * w + x];
                Vector3 n = new Vector3((l - r) * strength, (d - u) * strength, 1).normalized;
                px[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, 1f, 1f);
            }
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }

        static Color G(float v) => new Color(v, v, v, 1);

        // ---------------------------------------------------------------- 材质库
        public static Texture2D Cardboard => Get("cardboard", () => Make(256, 256, (u, v) =>
        {
            float f = Noise.Fbm2(u * 24, v * 24, 4, 1, 24);
            float fiber = Noise.Fbm2(u * 120, v * 6, 2, 2, 0) * 0.1f;
            float c = 0.86f + f * 0.14f + fiber;
            return G(Mathf.Clamp01(c));
        }));

        public static Texture2D Kraft => Get("kraft", () => Make(256, 256, (u, v) =>
        {
            float f = Noise.Fbm2(u * 32, v * 32, 4, 3, 32);
            float fib = Mathf.Pow(Noise.Value2(u * 200, v * 14, 5), 6) * 0.08f;
            return G(Mathf.Clamp01(0.9f + f * 0.08f - fib));
        }));

        public static Texture2D KraftN => Get("kraftN", () => Normal(256, 256, (u, v) => Noise.Fbm2(u * 32, v * 32, 4, 3, 32) * 0.5f + Mathf.Pow(Noise.Value2(u * 64, v * 4, 9, 64), 3) * 0.3f, 1.2f));

        public static Texture2D BrushedSteel => Get("steel", () => Make(256, 256, (u, v) =>
            G(0.82f + Noise.Fbm2(u * 2, v * 256, 3, 11, 0) * 0.16f)));

        public static Texture2D BrushedSteelN => Get("steelN", () => Normal(256, 256, (u, v) => Noise.Value2(u * 4, v * 256, 12, 256) * 0.3f, 2f));

        public static Texture2D Rubber => Get("rubber", () => Make(128, 128, (u, v) =>
        {
            float rib = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 16)) < 0.12f ? 0.75f : 1f;
            return G((0.9f + Noise.Fbm2(u * 32, v * 32, 3, 4, 32) * 0.1f) * rib);
        }));

        public static Texture2D RubberN => Get("rubberN", () => Normal(128, 128, (u, v) =>
            Mathf.SmoothStep(0, 1, Mathf.Abs(Mathf.Sin(u * Mathf.PI * 16)) * 6) + Noise.Value2(u * 64, v * 64, 6, 64) * 0.1f, 2.5f));

        public static Texture2D FloorTile => Get("floor", () => Make(512, 512, (u, v) =>
        {
            float gx = Mathf.Min(Frac(u * 2), 1 - Frac(u * 2)), gy = Mathf.Min(Frac(v * 2), 1 - Frac(v * 2));
            bool grout = gx < 0.008f || gy < 0.008f;
            float chip = Noise.Cells(u * 180, v * 180, 7, 180, out float id);
            float speck = id > 0.9f ? -0.1f : id < 0.06f ? 0.05f : 0;
            float tone = ((Mathf.FloorToInt(u * 2) + Mathf.FloorToInt(v * 2)) % 2 == 0) ? 0.94f : 0.88f;
            float c = tone + Noise.Fbm2(u * 8, v * 8, 3, 9, 8) * 0.05f + speck * (chip < 0.08f ? 0 : 1) * 0.5f;
            if (grout) c = 0.62f;
            return new Color(c, c * 0.995f, c * 0.97f, 1);
        }));

        public static Texture2D FloorTileN => Get("floorN", () => Normal(512, 512, (u, v) =>
        {
            float gx = Mathf.Min(Frac(u * 2), 1 - Frac(u * 2)), gy = Mathf.Min(Frac(v * 2), 1 - Frac(v * 2));
            float g = Mathf.Clamp01(Mathf.Min(gx, gy) / 0.012f);
            return g + Noise.Fbm2(u * 40, v * 40, 2, 2, 40) * 0.03f;
        }, 4f));

        /// <summary>毛发细节（灰度，乘材质颜色使用）。</summary>
        public static Texture2D Fur => Get("fur", () => Make(256, 256, (u, v) =>
        {
            float strands = Noise.Fbm2(u * 64, v * 10, 3, 21, 64);
            return G(0.82f + strands * 0.22f);
        }));

        public static Texture2D FurN => Get("furN", () => Normal(256, 256, (u, v) => Noise.Fbm2(u * 96, v * 12, 3, 21, 96), 2.2f));

        public static Texture2D Pulp => Get("pulp", () => Make(256, 256, (u, v) =>
        {
            float f = Noise.Fbm2(u * 40, v * 40, 4, 31, 40);
            float fleck = Noise.Value2(u * 160, v * 160, 32, 160) > 0.93f ? -0.12f : 0;
            return G(0.84f + f * 0.18f + fleck);
        }));

        public static Texture2D PulpN => Get("pulpN", () => Normal(256, 256, (u, v) => Noise.Fbm2(u * 40, v * 40, 4, 31, 40), 4f));

        public static Texture2D PlasticWrinkleN => Get("wrinkleN", () => Normal(256, 256, (u, v) =>
        {
            float a = Noise.Fbm2(u * 6, v * 3, 4, 41, 6);
            float b = Mathf.Abs(Noise.Value2(u * 10, v * 18, 42, 10) - 0.5f) * 2;
            return a * 0.8f + (1 - b) * 0.4f;
        }, 3f));

        public static Texture2D Speckle => Get("speckle", () => Make(256, 256, (u, v) =>
        {
            float s = Noise.Value2(u * 90, v * 90, 51, 90) > 0.86f ? 0.82f : 1f;
            return G(s * (0.92f + Noise.Fbm2(u * 8, v * 8, 3, 52, 8) * 0.12f));
        }));

        public static Texture2D BumpyN => Get("bumpyN", () => Normal(256, 256, (u, v) =>
        {
            float c = Noise.Cells(u * 28, v * 28, 61, 28, out _);
            return Mathf.Clamp01(c * 3);
        }, 3f));

        public static Texture2D PitsN => Get("pitsN", () => Normal(256, 256, (u, v) => 1 - Mathf.Pow(Noise.Value2(u * 70, v * 70, 71, 70), 4), 2f));

        public static Texture2D Sponge => Get("sponge", () => Make(128, 128, (u, v) =>
            G(Noise.Cells(u * 18, v * 18, 81, 18, out _) < 0.08f ? 0.7f : 0.98f)));

        public static Texture2D SpongeN => Get("spongeN", () => Normal(128, 128, (u, v) => Mathf.Clamp01(Noise.Cells(u * 18, v * 18, 81, 18, out _) * 5), 4f));

        public static Texture2D Wood => Get("wood", () => Make(128, 256, (u, v) =>
        {
            float ring = Frac((u * 8 + Noise.Fbm2(u * 4, v * 2, 3, 91, 0) * 2));
            float c = 0.78f + Mathf.Pow(ring, 3) * 0.18f;
            return new Color(c, c * 0.8f, c * 0.55f, 1);
        }));

        public static Texture2D Net => Get("net", () =>
        {
            var t = Make(128, 128, (u, v) =>
            {
                float a = Frac(u * 8 + v * 8), b = Frac(u * 8 - v * 8);
                bool line = Mathf.Min(a, 1 - a) < 0.09f || Mathf.Min(b, 1 - b) < 0.09f;
                return new Color(1, 1, 1, line ? 1 : 0);
            });
            return t;
        });

        public static Texture2D Bread => Get("bread", () => Make(256, 256, (u, v) =>
        {
            float f = Noise.Fbm2(u * 12, v * 12, 5, 101, 12);
            float pores = Noise.Cells(u * 40, v * 40, 102, 40, out _) < 0.06f ? -0.12f : 0;
            float c = 0.75f + f * 0.3f + pores;
            return new Color(c * 0.92f, c * 0.62f, c * 0.32f, 1);
        }));

        public static Texture2D BreadN => Get("breadN", () => Normal(256, 256, (u, v) => Noise.Fbm2(u * 12, v * 12, 5, 101, 12) + Mathf.Clamp01(Noise.Cells(u * 40, v * 40, 102, 40, out _) * 6) * 0.3f, 3f));

        public static Texture2D Meat => Get("meat", () => Make(256, 256, (u, v) =>
        {
            float fat = Mathf.Pow(Noise.Fbm2(u * 10, v * 10, 5, 111, 10), 3) * 2.2f;
            float fib = Noise.Value2(u * 80, v * 8, 112, 80) * 0.08f;
            Color red = new Color(0.72f, 0.12f + fib, 0.14f), white = new Color(0.98f, 0.86f, 0.84f);
            return Color.Lerp(red, white, Mathf.Clamp01(fat - 0.25f));
        }));

        public static Texture2D Salmon => Get("salmon", () => Make(256, 256, (u, v) =>
        {
            float stripe = Mathf.Pow(Mathf.Abs(Mathf.Sin((u * 14 + Noise.Fbm2(u * 3, v * 3, 2, 121, 3) * 2) * Mathf.PI)), 18);
            Color o = new Color(1f, 0.52f, 0.36f), w = new Color(1f, 0.86f, 0.78f);
            return Color.Lerp(o, w, stripe * 0.85f);
        }));

        public static Texture2D AppleSkin => Get("apple", () => Make(256, 256, (u, v) =>
        {
            float streak = Noise.Fbm2(u * 40, v * 3, 4, 131, 40);
            float dot = Noise.Value2(u * 120, v * 60, 132, 120) > 0.9f ? 1 : 0;
            Color red = new Color(0.72f, 0.06f, 0.08f), yellow = new Color(0.95f, 0.68f, 0.2f);
            Color c = Color.Lerp(red, yellow, Mathf.Clamp01((streak - 0.45f) * 2.2f + (v < 0.15f ? 0.5f : 0)));
            return Color.Lerp(c, new Color(1, 0.9f, 0.6f), dot * 0.5f);
        }));

        public static Texture2D BananaSkin => Get("banana", () => Make(64, 256, (u, v) =>
        {
            float tip = Mathf.Clamp01(Mathf.Max(0.06f - v, v - 0.94f) * 20);
            float spot = Noise.Value2(u * 20, v * 60, 141, 20) > 0.93f ? 1 : 0;
            float ridge = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 5)) < 0.08f ? 0.88f : 1f;
            Color y = new Color(0.98f, 0.84f, 0.22f) * ridge, g = new Color(0.55f, 0.6f, 0.15f), b = new Color(0.25f, 0.17f, 0.08f);
            Color c = Color.Lerp(y, g, Mathf.Clamp01(1 - v * 6) * 0.4f);
            c = Color.Lerp(c, b, Mathf.Max(tip, spot * 0.7f));
            c.a = 1;
            return c;
        }));

        public static Texture2D Watermelon => Get("watermelon", () => Make(256, 128, (u, v) =>
        {
            float w = Noise.Fbm2(u * 6, v * 6, 3, 151, 6) * 0.25f;
            float s = Mathf.Abs(Mathf.Sin((u * 16 + w * 2 + Mathf.Sin(v * 20) * 0.08f) * Mathf.PI));
            Color dark = new Color(0.05f, 0.2f, 0.07f), light = new Color(0.5f, 0.72f, 0.38f);
            return Color.Lerp(dark, light, Mathf.SmoothStep(0.55f, 0.75f, s));
        }));

        public static Texture2D Pineapple => Get("pineapple", () => Make(256, 256, (u, v) =>
        {
            float a = Frac(u * 10 + v * 6), b = Frac(u * 10 - v * 6);
            float d = Mathf.Min(Mathf.Min(a, 1 - a), Mathf.Min(b, 1 - b));
            Color edge = new Color(0.35f, 0.24f, 0.08f), center = new Color(0.86f, 0.6f, 0.2f), tip = new Color(0.45f, 0.55f, 0.2f);
            Color c = Color.Lerp(edge, center, Mathf.Clamp01(d * 9));
            if (d > 0.2f) c = Color.Lerp(c, tip, 0.5f);
            return c;
        }));

        public static Texture2D PineappleN => Get("pineappleN", () => Normal(256, 256, (u, v) =>
        {
            float a = Frac(u * 10 + v * 6), b = Frac(u * 10 - v * 6);
            return Mathf.Min(Mathf.Min(a, 1 - a), Mathf.Min(b, 1 - b)) * 4;
        }, 3f));

        public static Texture2D Leaf => Get("leaf", () => Make(256, 256, (u, v) =>
        {
            float vein = Mathf.Pow(1 - Mathf.Abs(Mathf.Sin((u * 10 + Noise.Fbm2(u * 4, v * 4, 2, 161, 4)) * Mathf.PI)), 12);
            float f = Noise.Fbm2(u * 6, v * 6, 4, 162, 6);
            return G(0.8f + f * 0.25f + vein * 0.25f);
        }));

        public static Texture2D Onion => Get("onion", () => Make(128, 256, (u, v) =>
        {
            float streak = Noise.Fbm2(u * 30, v * 2, 3, 171, 30);
            return new Color(0.78f + streak * 0.2f, 0.52f + streak * 0.2f, 0.25f + streak * 0.1f, 1);
        }));

        public static Texture2D Potato => Get("potato", () => Make(128, 128, (u, v) =>
        {
            float f = Noise.Fbm2(u * 10, v * 10, 4, 181, 10);
            float eye = Noise.Value2(u * 30, v * 30, 182, 30) > 0.95f ? -0.25f : 0;
            float c = 0.72f + f * 0.25f + eye;
            return new Color(c * 0.85f, c * 0.68f, c * 0.45f, 1);
        }));

        public static Texture2D StrawberrySkin => Get("strawberry", () => Make(128, 128, (u, v) =>
        {
            float a = Frac(u * 12 + v * 4), b = Frac(v * 12);
            bool seed = (a - 0.5f) * (a - 0.5f) + (b - 0.5f) * (b - 0.5f) < 0.02f;
            return seed ? new Color(0.95f, 0.85f, 0.3f) : new Color(0.82f, 0.05f, 0.1f);
        }));

        public static Texture2D Foam => Get("foam", () => Make(128, 128, (u, v) => G(0.93f + Noise.Value2(u * 50, v * 50, 191, 50) * 0.07f)));

        public static Texture2D CeilingTile => Get("ceiling", () => Make(256, 256, (u, v) =>
        {
            bool grid = Frac(u) < 0.02f || Frac(v) < 0.02f;
            float pits = Noise.Value2(u * 100, v * 100, 201, 100) > 0.9f ? 0.9f : 1f;
            return grid ? new Color(0.75f, 0.75f, 0.76f) : G(0.95f * pits);
        }));

        public static Texture2D Gradient(Color top, Color bottom) => Get("grad" + top + bottom, () => Make(4, 64, (u, v) => Color.Lerp(bottom, top, v)));

        static float Frac(float x) => x - Mathf.Floor(x);
    }
}
