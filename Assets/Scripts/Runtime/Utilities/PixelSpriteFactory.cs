using UnityEngine;

namespace CreatureClimb
{
    public static class PixelSpriteFactory
    {
        public static Sprite CreateSolid(int width, int height, Color color, int pixelsPerUnit = 16)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        static Sprite cachedCoinSprite;

        /// <summary>
        /// Coin art from CurrencySettings, then Resources/Coin, then procedural 16×16.
        /// </summary>
        public static Sprite CreateCoinSprite()
        {
            var settings = CurrencySettings.Load();
            if (settings != null && settings.CoinSprite != null)
                return settings.CoinSprite;

            if (cachedCoinSprite != null)
                return cachedCoinSprite;

            // Multiple-mode sheet → Coin_0; Single-mode → Coin.
            var fromSheet = Resources.LoadAll<Sprite>("Coin");
            if (fromSheet != null && fromSheet.Length > 0)
            {
                cachedCoinSprite = fromSheet[0];
                return cachedCoinSprite;
            }

            var single = Resources.Load<Sprite>("Coin");
            if (single != null)
            {
                cachedCoinSprite = single;
                return cachedCoinSprite;
            }

            // Procedural fallback if no currency asset / Coin.png.
            const int size = 16;
            var texture = NewTexture(size, size);
            var pixels = ClearPixels(size, size);
            var gold = new Color(1f, 0.84f, 0.18f, 1f);
            var mid = new Color(0.95f, 0.7f, 0.12f, 1f);
            var rim = new Color(0.72f, 0.42f, 0.05f, 1f);
            var shine = new Color(1f, 0.97f, 0.7f, 1f);
            var cx = 7.5f;
            var cy = 7.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 7.4f) continue;

                    Color c;
                    if (d > 6.3f)
                        c = rim;
                    else if (d < 2.4f && dx <= 0f && dy >= 0f)
                        c = shine;
                    else if (d > 4.5f)
                        c = mid;
                    else
                        c = gold;

                    // Soft vertical ridge so it reads as a coin, not a ball.
                    if (Mathf.Abs(dx) <= 0.6f && d < 5.5f && d > 2.2f)
                        c = Color.Lerp(c, rim, 0.35f);

                    pixels[y * size + x] = c;
                }
            }

            cachedCoinSprite = FinishSprite(texture, pixels, size, size, new Vector2(0.5f, 0.5f), 16);
            return cachedCoinSprite;
        }

        public static Sprite CreateLockSprite()
        {
            const int size = 16;
            var texture = NewTexture(size, size);
            var pixels = ClearPixels(size, size);
            var metal = new Color(0.75f, 0.78f, 0.85f);
            var dark = new Color(0.25f, 0.28f, 0.35f);
            // Shackle
            for (var x = 4; x <= 11; x++)
            {
                pixels[12 * size + x] = metal;
                pixels[5 * size + x] = metal;
            }
            for (var y = 5; y <= 12; y++)
            {
                pixels[y * size + 4] = metal;
                pixels[y * size + 11] = metal;
            }
            // Body
            for (var y = 1; y <= 7; y++)
            for (var x = 3; x <= 12; x++)
                pixels[y * size + x] = dark;
            // Keyhole
            pixels[4 * size + 7] = metal;
            pixels[4 * size + 8] = metal;
            pixels[3 * size + 7] = metal;
            pixels[3 * size + 8] = metal;
            pixels[2 * size + 7] = metal;
            pixels[2 * size + 8] = metal;
            return FinishSprite(texture, pixels, size, size, new Vector2(0.5f, 0.5f));
        }

        public static Sprite CreateLeafSprite()
        {
            const int size = 16;
            var texture = NewTexture(size, size);
            var pixels = ClearPixels(size, size);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs(x - 7.5f) / 7.5f;
                    var dy = Mathf.Abs(y - 7f) / 8f;
                    var inside = dx + dy * 0.8f <= 1f;
                    pixels[y * size + x] = inside ? new Color(0.25f, 0.72f, 0.28f) : Color.clear;
                }
            }

            return FinishSprite(texture, pixels, size, size, new Vector2(0.5f, 0.5f));
        }

        public static Sprite CreateCreatureSprite(ProceduralCreatureSprite kind)
        {
            return kind switch
            {
                ProceduralCreatureSprite.Frog => CreateFrogSprite(),
                ProceduralCreatureSprite.Gecko => CreateGeckoSprite(),
                ProceduralCreatureSprite.Bird => CreateBirdSprite(),
                ProceduralCreatureSprite.Beetle => CreateBeetleSprite(),
                ProceduralCreatureSprite.Cat => CreateCatSprite(),
                ProceduralCreatureSprite.Squirrel => CreateSquirrelSprite(),
                ProceduralCreatureSprite.Snake => CreateSnakeSprite(),
                ProceduralCreatureSprite.Bunny => CreateBunnySprite(),
                ProceduralCreatureSprite.Slime => CreateSlimeSprite(),
                ProceduralCreatureSprite.Mushroom => CreateMushroomSprite(),
                _ => CreateFrogSprite()
            };
        }

        public static Sprite CreateFrogSprite()
        {
            const int w = 12;
            const int h = 12;
            var texture = NewTexture(w, h);
            var pixels = ClearPixels(w, h);
            var body = new Color(0.3f, 0.78f, 0.28f);
            var belly = new Color(0.55f, 0.9f, 0.45f);

            void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[y * w + x] = c;
            }

            for (var y = 3; y <= 9; y++)
                for (var x = 2; x <= 9; x++)
                    Set(x, y, body);

            for (var y = 4; y <= 7; y++)
                for (var x = 4; x <= 7; x++)
                    Set(x, y, belly);

            Set(3, 9, body);
            Set(8, 9, body);
            Set(2, 3, body);
            Set(9, 3, body);
            Set(4, 8, Color.white);
            Set(7, 8, Color.white);
            Set(4, 8, new Color(0.1f, 0.1f, 0.1f));
            Set(7, 8, new Color(0.1f, 0.1f, 0.1f));

            return FinishSprite(texture, pixels, w, h, new Vector2(0.5f, 0.2f));
        }

        public static Sprite CreateGeckoSprite()
        {
            const int w = 14;
            const int h = 12;
            var texture = NewTexture(w, h);
            var pixels = ClearPixels(w, h);
            var body = new Color(0.4f, 0.82f, 0.32f);
            var spots = new Color(0.25f, 0.55f, 0.2f);

            void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[y * w + x] = c;
            }

            // Body + long tail
            for (var y = 3; y <= 8; y++)
                for (var x = 3; x <= 9; x++)
                    Set(x, y, body);

            for (var x = 10; x <= 13; x++)
                Set(x, 5, body);
            Set(12, 4, body);
            Set(13, 3, body);

            // Feet
            Set(3, 2, body);
            Set(5, 2, body);
            Set(7, 2, body);
            Set(9, 2, body);

            // Eyes + spots
            Set(4, 7, Color.white);
            Set(6, 7, Color.white);
            Set(4, 7, Color.black);
            Set(6, 7, Color.black);
            Set(5, 5, spots);
            Set(8, 6, spots);

            return FinishSprite(texture, pixels, w, h, new Vector2(0.4f, 0.2f));
        }

        public static Sprite CreateBirdSprite()
        {
            const int w = 12;
            const int h = 12;
            var texture = NewTexture(w, h);
            var pixels = ClearPixels(w, h);
            var body = new Color(1f, 0.72f, 0.25f);
            var wing = new Color(0.95f, 0.55f, 0.15f);
            var beak = new Color(0.95f, 0.35f, 0.15f);

            void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[y * w + x] = c;
            }

            for (var y = 3; y <= 8; y++)
                for (var x = 3; x <= 8; x++)
                    Set(x, y, body);

            // Wing
            for (var y = 4; y <= 7; y++)
                for (var x = 1; x <= 3; x++)
                    Set(x, y, wing);

            // Beak + eye + feet
            Set(9, 6, beak);
            Set(10, 6, beak);
            Set(6, 7, Color.black);
            Set(5, 2, body);
            Set(7, 2, body);
            Set(4, 9, body); // crest

            return FinishSprite(texture, pixels, w, h, new Vector2(0.5f, 0.2f));
        }

        public static Sprite CreateBeetleSprite()
        {
            const int w = 12;
            const int h = 10;
            var texture = NewTexture(w, h);
            var pixels = ClearPixels(w, h);
            var shell = new Color(0.25f, 0.35f, 0.8f);
            var shellDark = new Color(0.15f, 0.2f, 0.5f);
            var leg = new Color(0.15f, 0.15f, 0.2f);

            void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[y * w + x] = c;
            }

            for (var y = 2; y <= 7; y++)
                for (var x = 2; x <= 9; x++)
                    Set(x, y, shell);

            for (var y = 3; y <= 6; y++)
                Set(5, y, shellDark);
            Set(6, 5, shellDark);

            // Legs
            Set(1, 3, leg);
            Set(1, 5, leg);
            Set(1, 7, leg);
            Set(10, 3, leg);
            Set(10, 5, leg);
            Set(10, 7, leg);

            // Eyes
            Set(3, 6, Color.white);
            Set(4, 6, Color.black);
            Set(7, 6, Color.white);
            Set(8, 6, Color.black);

            return FinishSprite(texture, pixels, w, h, new Vector2(0.5f, 0.25f));
        }

        public static Sprite CreateCatSprite()
        {
            const int w = 12;
            const int h = 12;
            var texture = NewTexture(w, h);
            var pixels = ClearPixels(w, h);
            var fur = new Color(1f, 0.65f, 0.3f);
            var ear = new Color(0.95f, 0.5f, 0.25f);

            void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[y * w + x] = c;
            }

            for (var y = 2; y <= 8; y++)
                for (var x = 2; x <= 9; x++)
                    Set(x, y, fur);

            // Ears
            Set(3, 10, ear);
            Set(3, 9, ear);
            Set(8, 10, ear);
            Set(8, 9, ear);

            // Tail
            Set(10, 4, fur);
            Set(11, 5, fur);
            Set(11, 6, fur);

            // Face
            Set(4, 6, Color.black);
            Set(7, 6, Color.black);
            Set(5, 4, new Color(0.9f, 0.4f, 0.5f));
            Set(6, 4, new Color(0.9f, 0.4f, 0.5f));

            return FinishSprite(texture, pixels, w, h, new Vector2(0.5f, 0.2f));
        }

        public static Sprite CreateSquirrelSprite()
        {
            const int w = 12;
            const int h = 12;
            var texture = NewTexture(w, h);
            var pixels = ClearPixels(w, h);
            var fur = new Color(0.72f, 0.42f, 0.2f);
            var belly = new Color(0.9f, 0.75f, 0.55f);

            void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[y * w + x] = c;
            }

            for (var y = 2; y <= 8; y++)
                for (var x = 3; x <= 8; x++)
                    Set(x, y, fur);

            for (var y = 3; y <= 6; y++)
                for (var x = 4; x <= 6; x++)
                    Set(x, y, belly);

            // Big tail
            Set(9, 4, fur);
            Set(10, 5, fur);
            Set(10, 6, fur);
            Set(10, 7, fur);
            Set(9, 8, fur);
            Set(8, 9, fur);

            // Ears + eyes
            Set(4, 9, fur);
            Set(7, 9, fur);
            Set(4, 6, Color.black);
            Set(7, 6, Color.black);

            return FinishSprite(texture, pixels, w, h, new Vector2(0.45f, 0.2f));
        }

        public static Sprite CreateSnakeSprite()
        {
            const int w = 14;
            const int h = 10;
            var texture = NewTexture(w, h);
            var pixels = ClearPixels(w, h);
            var body = new Color(0.35f, 0.7f, 0.3f);
            var stripe = new Color(0.25f, 0.5f, 0.2f);

            void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[y * w + x] = c;
            }

            // S-curve body
            for (var x = 1; x <= 5; x++)
                Set(x, 2, body);
            for (var y = 2; y <= 6; y++)
                Set(5, y, body);
            for (var x = 5; x <= 10; x++)
                Set(x, 6, body);
            for (var y = 4; y <= 6; y++)
                Set(10, y, body);
            for (var x = 10; x <= 12; x++)
                Set(x, 4, body);

            Set(3, 2, stripe);
            Set(7, 6, stripe);
            Set(11, 4, stripe);

            // Head + tongue
            Set(12, 5, body);
            Set(13, 5, body);
            Set(13, 6, Color.red);
            Set(12, 5, Color.black); // eye

            return FinishSprite(texture, pixels, w, h, new Vector2(0.35f, 0.25f));
        }

        public static Sprite CreateBunnySprite()
        {
            const int w = 12;
            const int h = 14;
            var texture = NewTexture(w, h);
            var pixels = ClearPixels(w, h);
            var fur = new Color(0.95f, 0.92f, 0.92f);
            var pink = new Color(1f, 0.7f, 0.75f);

            void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[y * w + x] = c;
            }

            for (var y = 2; y <= 8; y++)
                for (var x = 3; x <= 8; x++)
                    Set(x, y, fur);

            // Tall ears
            for (var y = 9; y <= 13; y++)
            {
                Set(4, y, fur);
                Set(7, y, fur);
            }

            Set(4, 12, pink);
            Set(7, 12, pink);

            // Face + feet
            Set(4, 6, Color.black);
            Set(7, 6, Color.black);
            Set(5, 4, pink);
            Set(6, 4, pink);
            Set(3, 1, fur);
            Set(8, 1, fur);

            return FinishSprite(texture, pixels, w, h, new Vector2(0.5f, 0.15f));
        }

        public static Sprite CreateSlimeSprite()
        {
            const int w = 12;
            const int h = 10;
            var texture = NewTexture(w, h);
            var pixels = ClearPixels(w, h);
            var body = new Color(0.45f, 0.9f, 1f);
            var shine = new Color(0.8f, 1f, 1f);

            void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[y * w + x] = c;
            }

            for (var y = 1; y <= 7; y++)
                for (var x = 2; x <= 9; x++)
                    Set(x, y, body);

            // Blob top
            for (var x = 3; x <= 8; x++)
                Set(x, 8, body);

            Set(4, 6, shine);
            Set(5, 7, shine);
            Set(4, 5, Color.black);
            Set(7, 5, Color.black);
            Set(5, 3, new Color(0.3f, 0.6f, 0.7f));
            Set(6, 3, new Color(0.3f, 0.6f, 0.7f));

            return FinishSprite(texture, pixels, w, h, new Vector2(0.5f, 0.15f));
        }

        public static Sprite CreateMushroomSprite()
        {
            const int w = 12;
            const int h = 12;
            var texture = NewTexture(w, h);
            var pixels = ClearPixels(w, h);
            var cap = new Color(0.95f, 0.3f, 0.3f);
            var spot = Color.white;
            var stem = new Color(0.95f, 0.9f, 0.75f);

            void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[y * w + x] = c;
            }

            // Stem
            for (var y = 1; y <= 5; y++)
                for (var x = 4; x <= 7; x++)
                    Set(x, y, stem);

            // Cap
            for (var y = 6; y <= 10; y++)
                for (var x = 1; x <= 10; x++)
                    Set(x, y, cap);

            Set(3, 9, spot);
            Set(6, 10, spot);
            Set(8, 8, spot);

            // Face on stem
            Set(5, 4, Color.black);
            Set(6, 4, Color.black);
            Set(5, 2, new Color(0.8f, 0.4f, 0.4f));
            Set(6, 2, new Color(0.8f, 0.4f, 0.4f));

            return FinishSprite(texture, pixels, w, h, new Vector2(0.5f, 0.15f));
        }

        static Sprite cachedBackChevron;

        /// <summary>White left-pointing back chevron on a transparent square. UI icon, bilinear.</summary>
        public static Sprite CreateBackChevronSprite()
        {
            if (cachedBackChevron != null)
                return cachedBackChevron;

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = ClearPixels(size, size);

            var tip = new Vector2(size * 0.28f, size * 0.50f);
            var top = new Vector2(size * 0.74f, size * 0.84f);
            var bot = new Vector2(size * 0.74f, size * 0.16f);
            var half = size * 0.072f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var d = Mathf.Min(DistToSegment(p, tip, top), DistToSegment(p, tip, bot));
                    var a = 1f - Mathf.Clamp01((d - (half - 1.2f)) / 1.6f);
                    if (a <= 0f) continue;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            cachedBackChevron = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return cachedBackChevron;
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var denom = ab.sqrMagnitude;
            if (denom < 0.0001f)
                return Vector2.Distance(p, a);
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom);
            return Vector2.Distance(p, a + ab * t);
        }

        static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        static Color[] ClearPixels(int width, int height)
        {
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;
            return pixels;
        }

        static Sprite FinishSprite(Texture2D texture, Color[] pixels, int width, int height, Vector2 pivot, int ppu = 16)
        {
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, ppu);
        }
    }
}
