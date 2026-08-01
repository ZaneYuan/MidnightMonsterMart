using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Data;

namespace MonsterMart.Art
{
    /// <summary>
    /// 程序化生成所有占位美术 —— 工程里没有任何图片资源。
    /// 对应设计文档 §21「最早的版本可以全部使用方块和临时图标」。
    /// 换成正式美术时，只需把这里的调用替换成资源加载即可。
    /// </summary>
    public static class SpriteFactory
    {
        const int PPU = GameConfig.PixelsPerUnit;

        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static void ClearCache() => _cache.Clear();

        // ------------------------------------------------------------------
        // 基础形状
        // ------------------------------------------------------------------

        /// <summary>纯色矩形，1×1 世界单位。</summary>
        public static Sprite Solid(Color color)
        {
            string key = "solid_" + ColorKey(color);
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var tex = NewTexture(PPU, PPU);
            Fill(tex, color);
            tex.Apply();

            var sprite = Make(tex, 0.5f, 0.5f);
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>带描边的矩形块，用于货架、收银台等设施。</summary>
        public static Sprite Panel(Color fill, Color border, int widthCells, int heightCells, int borderPx = 2)
        {
            string key = $"panel_{ColorKey(fill)}_{ColorKey(border)}_{widthCells}x{heightCells}_{borderPx}";
            if (_cache.TryGetValue(key, out var cached)) return cached;

            int w = widthCells * PPU;
            int h = heightCells * PPU;
            var tex = NewTexture(w, h);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool edge = x < borderPx || y < borderPx || x >= w - borderPx || y >= h - borderPx;
                    tex.SetPixel(x, y, edge ? border : fill);
                }
            }

            // 顶部一道高光，让方块看起来有体积
            var highlight = Lighten(fill, 0.18f);
            for (int x = borderPx; x < w - borderPx; x++)
                for (int y = h - borderPx - 3; y < h - borderPx; y++)
                    tex.SetPixel(x, y, highlight);

            tex.Apply();
            var sprite = Make(tex, 0.5f, 0.5f);
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>圆形（污渍、气泡背景等）。</summary>
        public static Sprite Circle(Color color, int diameterPx = PPU)
        {
            string key = $"circle_{ColorKey(color)}_{diameterPx}";
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var tex = NewTexture(diameterPx, diameterPx);
            Fill(tex, Color.clear);

            float r = diameterPx * 0.5f;
            for (int y = 0; y < diameterPx; y++)
            {
                for (int x = 0; x < diameterPx; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    if (dx * dx + dy * dy <= r * r) tex.SetPixel(x, y, color);
                }
            }

            tex.Apply();
            var sprite = Make(tex, 0.5f, 0.5f);
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>不规则水渍形状 —— 史莱姆污渍。</summary>
        public static Sprite Stain(Color color, int seed)
        {
            string key = $"stain_{ColorKey(color)}_{seed}";
            if (_cache.TryGetValue(key, out var cached)) return cached;

            const int size = PPU;
            var tex = NewTexture(size, size);
            Fill(tex, Color.clear);

            var rng = new System.Random(seed);
            // 用 4 个随机圆叠成一团水渍
            for (int blob = 0; blob < 4; blob++)
            {
                float cx = size * 0.5f + (float)(rng.NextDouble() - 0.5) * size * 0.35f;
                float cy = size * 0.5f + (float)(rng.NextDouble() - 0.5) * size * 0.35f;
                float rad = size * (0.18f + (float)rng.NextDouble() * 0.16f);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - cx;
                        float dy = y + 0.5f - cy;
                        if (dx * dx + dy * dy <= rad * rad) tex.SetPixel(x, y, color);
                    }
                }
            }

            tex.Apply();
            var sprite = Make(tex, 0.5f, 0.5f);
            _cache[key] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------
        // 商品图标 — 设计文档 §14.2「商品图标：32×32」
        // ------------------------------------------------------------------
        public static Sprite ProductIcon(ProductData product)
        {
            if (product.runtimeIcon != null) return product.runtimeIcon;

            const int size = PPU;
            var tex = NewTexture(size, size);
            Fill(tex, Color.clear);

            Color body = product.tintColor;
            Color dark = Darken(body, 0.35f);
            Color light = Lighten(body, 0.3f);

            switch (product.iconShape)
            {
                case 0: DrawBottle(tex, size, body, dark, light); break;
                case 1: DrawBar(tex, size, body, dark, light); break;
                case 2: DrawJar(tex, size, body, dark, light); break;
                default: DrawBox(tex, size, body, dark, light); break;
            }

            tex.Apply();
            product.runtimeIcon = Make(tex, 0.5f, 0.5f);
            return product.runtimeIcon;
        }

        static void DrawBottle(Texture2D tex, int s, Color body, Color dark, Color light)
        {
            int bodyLeft = s / 4, bodyRight = s - s / 4;
            for (int y = 3; y < s - 10; y++)
                for (int x = bodyLeft; x < bodyRight; x++)
                    tex.SetPixel(x, y, x < bodyLeft + 3 ? light : body);

            int neckLeft = s / 2 - 3, neckRight = s / 2 + 3;
            for (int y = s - 10; y < s - 4; y++)
                for (int x = neckLeft; x < neckRight; x++)
                    tex.SetPixel(x, y, body);

            for (int y = s - 4; y < s - 1; y++)
                for (int x = neckLeft - 1; x < neckRight + 1; x++)
                    tex.SetPixel(x, y, dark);

            Outline(tex, s, dark);
        }

        static void DrawBar(Texture2D tex, int s, Color body, Color dark, Color light)
        {
            for (int y = s / 4; y < s - s / 4; y++)
                for (int x = 2; x < s - 2; x++)
                    tex.SetPixel(x, y, body);

            // 分格压痕
            for (int i = 1; i < 4; i++)
            {
                int x = 2 + i * (s - 4) / 4;
                for (int y = s / 4; y < s - s / 4; y++) tex.SetPixel(x, y, dark);
            }

            for (int x = 2; x < s - 2; x++) tex.SetPixel(x, s - s / 4 - 1, light);
            Outline(tex, s, dark);
        }

        static void DrawJar(Texture2D tex, int s, Color body, Color dark, Color light)
        {
            int r = s / 2 - 3;
            float cx = s * 0.5f, cy = s * 0.45f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r)
                        tex.SetPixel(x, y, dx < -r * 0.35f ? light : body);
                }
            }
            for (int y = s - 8; y < s - 3; y++)
                for (int x = s / 2 - 5; x < s / 2 + 5; x++)
                    tex.SetPixel(x, y, dark);
            Outline(tex, s, dark);
        }

        static void DrawBox(Texture2D tex, int s, Color body, Color dark, Color light)
        {
            for (int y = 4; y < s - 4; y++)
                for (int x = 3; x < s - 3; x++)
                    tex.SetPixel(x, y, body);

            for (int x = 3; x < s - 3; x++) tex.SetPixel(x, s - 5, light);
            for (int y = 4; y < s - 4; y++) tex.SetPixel(s / 2, y, dark);   // 封箱胶带
            Outline(tex, s, dark);
        }

        static void Outline(Texture2D tex, int s, Color color)
        {
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    if (tex.GetPixel(x, y).a > 0.01f) continue;
                    if (HasOpaqueNeighbour(tex, s, x, y)) tex.SetPixel(x, y, color);
                }
            }
        }

        static bool HasOpaqueNeighbour(Texture2D tex, int s, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= s || ny >= s) continue;
                    if (tex.GetPixel(nx, ny).a > 0.9f) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 角色 — 玩家与怪物
        // ------------------------------------------------------------------
        const int CharW = 32;
        const int CharH = 48;

        public static Sprite Character(CustomerData data)
        {
            if (data.runtimeSprite != null) return data.runtimeSprite;
            data.runtimeSprite = BuildCharacter(data.bodyColor, data.accentColor, data.silhouette);
            return data.runtimeSprite;
        }

        public static Sprite PlayerSprite()
        {
            const string key = "player";
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var s = BuildCharacter(new Color(0.25f, 0.42f, 0.62f), new Color(0.95f, 0.85f, 0.55f), 5);
            _cache[key] = s;
            return s;
        }

        static Sprite BuildCharacter(Color body, Color accent, int silhouette)
        {
            var tex = NewTexture(CharW, CharH);
            Fill(tex, Color.clear);

            Color dark = Darken(body, 0.4f);

            switch (silhouette)
            {
                case 2: BuildGhostShape(tex, body, accent); break;
                case 3: BuildSlimeShape(tex, body, accent); break;
                default: BuildHumanoidShape(tex, body, accent, dark, silhouette); break;
            }

            tex.Apply();
            // 轴心放在脚底稍上方，让角色"站"在格子中心
            return Make(tex, 0.5f, 0.18f);
        }

        static void BuildHumanoidShape(Texture2D tex, Color body, Color accent, Color dark, int silhouette)
        {
            bool bulky = silhouette == 1;      // 狼人体型更大
            bool coat = silhouette == 4;       // 检查员穿风衣

            int halfW = bulky ? 11 : 8;
            int torsoTop = 32;
            int torsoBottom = 12;

            // 躯干
            for (int y = torsoBottom; y < torsoTop; y++)
                for (int x = CharW / 2 - halfW; x < CharW / 2 + halfW; x++)
                    tex.SetPixel(x, y, coat && y < torsoTop - 6 ? Darken(body, 0.15f) : body);

            // 腿
            for (int y = 3; y < torsoBottom; y++)
            {
                for (int x = CharW / 2 - halfW + 2; x < CharW / 2 - 1; x++) tex.SetPixel(x, y, dark);
                for (int x = CharW / 2 + 2; x < CharW / 2 + halfW - 2; x++) tex.SetPixel(x, y, dark);
            }

            // 头
            int headR = bulky ? 9 : 7;
            float hcx = CharW * 0.5f, hcy = torsoTop + headR - 1;
            for (int y = 0; y < CharH; y++)
            {
                for (int x = 0; x < CharW; x++)
                {
                    float dx = x + 0.5f - hcx, dy = y + 0.5f - hcy;
                    if (dx * dx + dy * dy <= headR * headR) tex.SetPixel(x, y, Lighten(body, 0.25f));
                }
            }

            // 眼睛
            tex.SetPixel((int)hcx - 3, (int)hcy + 1, accent);
            tex.SetPixel((int)hcx - 2, (int)hcy + 1, accent);
            tex.SetPixel((int)hcx + 2, (int)hcy + 1, accent);
            tex.SetPixel((int)hcx + 3, (int)hcy + 1, accent);

            // 领口 / 装饰色
            for (int x = CharW / 2 - halfW; x < CharW / 2 + halfW; x++)
                for (int y = torsoTop - 4; y < torsoTop; y++)
                    tex.SetPixel(x, y, accent);

            OutlineChar(tex, dark);
        }

        static void BuildGhostShape(Texture2D tex, Color body, Color accent)
        {
            var translucent = new Color(body.r, body.g, body.b, 0.72f);
            int cx = CharW / 2;
            int topY = 42;
            int r = 11;

            // 圆顶
            for (int y = 0; y < CharH; y++)
            {
                for (int x = 0; x < CharW; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - (topY - r);
                    if (dy > 0 && dx * dx + dy * dy <= r * r) tex.SetPixel(x, y, translucent);
                }
            }

            // 身体
            for (int y = 12; y <= topY - r; y++)
                for (int x = cx - r; x < cx + r; x++)
                    tex.SetPixel(x, y, translucent);

            // 波浪下摆
            for (int x = cx - r; x < cx + r; x++)
            {
                int wave = 12 - (int)(3f * Mathf.Abs(Mathf.Sin((x - cx) * 0.9f)));
                for (int y = wave; y < 12; y++) tex.SetPixel(x, y, translucent);
            }

            for (int i = -1; i <= 0; i++)
            {
                tex.SetPixel(cx - 4 + i, topY - r + 4, accent);
                tex.SetPixel(cx + 4 + i, topY - r + 4, accent);
            }
        }

        static void BuildSlimeShape(Texture2D tex, Color body, Color accent)
        {
            var gel = new Color(body.r, body.g, body.b, 0.85f);
            int cx = CharW / 2;
            int baseY = 4;
            int height = 26;
            int halfW = 13;

            for (int y = baseY; y < baseY + height; y++)
            {
                float t = (y - baseY) / (float)height;
                int w = (int)(halfW * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)));
                for (int x = cx - w; x <= cx + w; x++) tex.SetPixel(x, y, gel);
            }

            // 高光
            for (int y = baseY + height / 2; y < baseY + height - 4; y++)
                for (int x = cx - 7; x < cx - 3; x++)
                    tex.SetPixel(x, y, Lighten(gel, 0.45f));

            tex.SetPixel(cx - 4, baseY + 14, accent);
            tex.SetPixel(cx - 3, baseY + 14, accent);
            tex.SetPixel(cx + 3, baseY + 14, accent);
            tex.SetPixel(cx + 4, baseY + 14, accent);
        }

        static void OutlineChar(Texture2D tex, Color color)
        {
            for (int y = 0; y < CharH; y++)
            {
                for (int x = 0; x < CharW; x++)
                {
                    if (tex.GetPixel(x, y).a > 0.01f) continue;

                    bool neighbour = false;
                    for (int dx = -1; dx <= 1 && !neighbour; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= CharW || ny >= CharH) continue;
                            if (tex.GetPixel(nx, ny).a > 0.9f) { neighbour = true; break; }
                        }
                    }
                    if (neighbour) tex.SetPixel(x, y, color);
                }
            }
        }

        // ------------------------------------------------------------------
        // 地板 / 墙
        // ------------------------------------------------------------------
        public static Sprite FloorTile(Color a, Color b)
        {
            string key = $"floor_{ColorKey(a)}_{ColorKey(b)}";
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var tex = NewTexture(PPU, PPU);
            for (int y = 0; y < PPU; y++)
                for (int x = 0; x < PPU; x++)
                    tex.SetPixel(x, y, (x == 0 || y == 0) ? b : a);
            tex.Apply();

            var sprite = Make(tex, 0.5f, 0.5f);
            _cache[key] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------
        // 工具
        // ------------------------------------------------------------------
        static Texture2D NewTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        static void Fill(Texture2D tex, Color c)
        {
            var pixels = new Color[tex.width * tex.height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
            tex.SetPixels(pixels);
        }

        static Sprite Make(Texture2D tex, float pivotX, float pivotY)
        {
            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(pivotX, pivotY),
                PPU);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public static Color Lighten(Color c, float amount)
            => new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);

        public static Color Darken(Color c, float amount)
            => new Color(
                Mathf.Clamp01(c.r - amount),
                Mathf.Clamp01(c.g - amount),
                Mathf.Clamp01(c.b - amount),
                c.a);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        static string ColorKey(Color c)
            => $"{(int)(c.r * 255)}-{(int)(c.g * 255)}-{(int)(c.b * 255)}-{(int)(c.a * 255)}";
    }
}
