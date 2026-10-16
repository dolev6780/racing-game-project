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

        static Shader toon, sky, boostPad, itemBox, sparks, skid;
        static bool loaded;
        static Material kenneyMaterial, kenneyMaterialNoOutline, skyMaterial, boostPadMaterial, itemBoxMaterial, sparksMaterial, skidMaterial;
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

        // ---------- Procedural textures ----------

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
