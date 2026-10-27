using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Core
{
    /// <summary>
    /// Kart-racer look: loads the custom shaders (Assets/Resources/Shaders), builds toon materials
    /// and procedural textures. Everything falls back to Unity's default material if a shader is missing,
    /// so the game still runs (just less pretty).
    /// </summary>
    public static class ToonStyle
    {
        public const float ModelOutline = 1.1f;    // outline width for vehicles
        public const float PropOutline = 0.7f;     // outline width for scenery models

        static Shader toon, sky, boostPad, itemBox, sparks, skid, smoke;
        static bool loaded;
        static Material kenneyMaterial, kenneyMaterialNoOutline, skyMaterial, boostPadMaterial, itemBoxMaterial, sparksMaterial, skidMaterial,
            smokeMaterial, chevronMaterial;
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            toon = Supported(Resources.Load<Shader>("Shaders/Toon"));
            sky = Supported(Resources.Load<Shader>("Shaders/SkyGradient"));
            boostPad = Supported(Resources.Load<Shader>("Shaders/BoostPad"));
            itemBox = Supported(Resources.Load<Shader>("Shaders/ItemBox"));
            sparks = Supported(Resources.Load<Shader>("Shaders/Sparks"));
            skid = Supported(Resources.Load<Shader>("Shaders/Skid"));
            smoke = Supported(Resources.Load<Shader>("Shaders/Smoke"));
        }

        static Shader Supported(Shader s) => s != null && s.isSupported ? s : null;

        public static bool Enabled { get { Load(); return toon != null; } }

        /// <summary>A toon material with a flat colour (or texture).</summary>
        public static Material CreateToon(Color color, float outline = 0f, Texture texture = null)
        {
            Load();
            if (toon == null) return null;
            var m = new Material(toon) { color = color };
            if (texture != null) m.mainTexture = texture;
            m.SetFloat("_OutlineWidth", outline);
            return m;
        }

        /// <summary>Replaces every material on a Kenney model with the shared toon material (colormap texture).</summary>
        public static void Apply(GameObject model, float outline)
        {
            Load();
            if (toon == null || model == null) return;
            if (kenneyMaterial == null)
            {
                var atlas = Resources.Load<Texture2D>("Models/Textures/colormap");
                kenneyMaterial = CreateToon(Color.white, ModelOutline, atlas);
                kenneyMaterialNoOutline = CreateToon(Color.white, PropOutline, atlas);
                kenneyMaterialNoOutline.name = "KenneyToonProps";
                kenneyMaterial.name = "KenneyToon";
            }
            var mat = outline >= ModelOutline ? kenneyMaterial : kenneyMaterialNoOutline;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        // ---------- Special materials ----------

        public static Material Sky(Color top, Color horizon, Color bottom)
        {
            Load();
            if (sky == null) return null;
            if (skyMaterial == null) skyMaterial = new Material(sky) { name = "CartoonSky" };
            skyMaterial.SetColor("_TopColor", top);
            skyMaterial.SetColor("_HorizonColor", horizon);
            skyMaterial.SetColor("_BottomColor", bottom);
            return skyMaterial;
        }

        public static Material BoostPad()
        {
            Load();
            if (boostPad == null) return null;
            if (boostPadMaterial == null) boostPadMaterial = new Material(boostPad) { name = "BoostPad" };
            return boostPadMaterial;
        }

        public static Material ItemBox()
        {
            Load();
            if (itemBox == null) return null;
            if (itemBoxMaterial == null) itemBoxMaterial = new Material(itemBox) { name = "ItemBox" };
            return itemBoxMaterial;
        }

        public static Material Sparks()
        {
            Load();
            if (sparks == null) return null;
            if (sparksMaterial == null) sparksMaterial = new Material(sparks) { name = "Sparks" };
            return sparksMaterial;
        }

        public static Material Skid()
        {
            Load();
            if (skid == null) return null;
            if (skidMaterial == null) skidMaterial = new Material(skid) { name = "SkidMarks" };
            return skidMaterial;
        }

        /// <summary>Soft, alpha-blended puffs (damaged-kart smoke, dust).</summary>
        public static Material Smoke()
        {
            Load();
            if (smoke == null) return null;
            if (smokeMaterial == null) smokeMaterial = new Material(smoke) { name = "Smoke" };
            return smokeMaterial;
        }

        /// <summary>Yellow board with black chevrons pointing along +U (corner warning signs).</summary>
        public static Material Chevron()
        {
            Load();
            if (toon == null) return null;
            if (chevronMaterial == null)
            {
                chevronMaterial = CreateToon(Color.white, 0f, ChevronTexture());
                chevronMaterial.name = "Chevron";
                chevronMaterial.SetFloat("_SpecStrength", 0f);
            }
            return chevronMaterial;
        }

        // ---------- Procedural textures ----------

        /// <summary>
        /// Mowed-lawn checkerboard (8 m squares; the texture covers 32 m) with flowers and grass tufts,
        /// or pebbles and sparkles for non-grass themes. Drawn into the texture, so it costs nothing per frame.
        /// </summary>
        public static Texture2D Lawn(Color ground, bool flowers, Color sparkle, float sparkleChance)
        {
            string key = "lawn" + ground + flowers + sparkle + sparkleChance;
            if (textures.TryGetValue(key, out var t) && t != null) return t;
            const int size = 512;             // 16 px per metre over 32 m
            const float pxPerM = size / 32f;
            float luminance = 0.2126f * ground.r + 0.7152f * ground.g + 0.0722f * ground.b;
            Color a = ground, b = ground * (luminance > 0.6f ? 0.97f : 0.92f);
            a.a = b.a = 1f;
            var px = new Color32[size * size];
            int cell = Mathf.RoundToInt(8f * pxPerM);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.05f - 0.025f;
                Color c = ((x / cell + y / cell) % 2 == 0 ? a : b) * (1f + n);
                c.a = 1f;
                px[y * size + x] = c;
            }

            var rng = new System.Random(77);
            Color[] petals =
            {
                Color.white, new Color(1f, 0.88f, 0.3f), new Color(1f, 0.44f, 0.66f), new Color(1f, 0.35f, 0.3f), new Color(0.73f, 0.55f, 1f),
            };
            if (flowers)
            {
                Color tuft = ground * 0.8f;
                tuft.a = 1f;
                for (int k = 0; k < 40; k++)
                    Disc(px, size, rng.Next(size), rng.Next(size), 9, 6, tuft);
                for (int k = 0; k < 36; k++)
                {
                    int cx = rng.Next(size), cy = rng.Next(size);
                    var petal = petals[rng.Next(petals.Length)];
                    for (int p = 0; p < 4; p++)
                    {
                        float ang = p * Mathf.PI * 0.5f + 0.4f;
                        Disc(px, size, cx + Mathf.RoundToInt(Mathf.Cos(ang) * 5f), cy + Mathf.RoundToInt(Mathf.Sin(ang) * 5f), 5, 5, petal);
                    }
                    Disc(px, size, cx, cy, 3, 3, new Color(1f, 0.82f, 0.12f));
                }
            }
            else
            {
                Color pebble = ground * 0.84f;
                pebble.a = 1f;
                for (int k = 0; k < 60; k++)
                {
                    bool shiny = rng.NextDouble() < sparkleChance;
                    int r = shiny ? 3 : 4 + rng.Next(6);
                    Disc(px, size, rng.Next(size), rng.Next(size), r, Mathf.Max(2, r * 3 / 4), shiny ? Color.Lerp(sparkle, Color.white, 0.3f) : pebble);
                }
            }

            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true);
            textures[key] = t;
            return t;
        }

        static void Disc(Color32[] px, int size, int cx, int cy, int rx, int ry, Color color)
        {
            Color32 c = color;
            for (int y = -ry; y <= ry; y++)
            for (int x = -rx; x <= rx; x++)
            {
                if ((x * x) / (float)(rx * rx) + (y * y) / (float)(ry * ry) > 1f) continue;
                int ix = (cx + x + size) % size, iy = (cy + y + size) % size;   // wraps, so the tile repeats seamlessly
                px[iy * size + ix] = c;
            }
        }

        static Texture2D ChevronTexture()
        {
            const int w = 256, h = 64;
            var px = new Color32[w * h];
            Color32 yellow = new Color(1f, 0.82f, 0.12f), black = new Color(0.1f, 0.1f, 0.14f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                // Three ">" shapes: the middle row is further along U than the edges, so they point toward +U.
                float shape = Mathf.Repeat(u * 3f + Mathf.Abs(v - 0.5f) * 0.9f, 1f);
                bool arrow = shape > 0.32f && shape < 0.62f && v > 0.12f && v < 0.88f;
                bool border = v < 0.06f || v > 0.94f;
                px[y * w + x] = arrow || border ? black : yellow;
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Mowed-lawn stripes (two shades of the ground colour).</summary>
        public static Texture2D Stripes(Color baseColor, float contrast = 0.08f)
        {
            string key = "stripes" + baseColor + contrast;
            if (textures.TryGetValue(key, out var t) && t != null) return t;
            const int size = 64;
            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            Color a = baseColor * (1f + contrast), b = baseColor * (1f - contrast);
            a.a = b.a = 1f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.06f - 0.03f;
                Color c = (x < size / 2 ? a : b) * (1f + n);
                c.a = 1f;
                px[y * size + x] = c;
            }
            t.SetPixels(px);
            t.Apply(true);
            textures[key] = t;
            return t;
        }

        /// <summary>Subtle speckled asphalt.</summary>
        public static Texture2D Asphalt(Color baseColor)
        {
            string key = "asphalt" + baseColor;
            if (textures.TryGetValue(key, out var t) && t != null) return t;
            const int size = 128;
            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var rng = new System.Random(7);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.08f + (float)rng.NextDouble() * 0.06f - 0.07f;
                Color c = baseColor * (1f + n);
                c.a = 1f;
                px[y * size + x] = c;
            }
            t.SetPixels(px);
            t.Apply(true);
            textures[key] = t;
            return t;
        }
    }
}
