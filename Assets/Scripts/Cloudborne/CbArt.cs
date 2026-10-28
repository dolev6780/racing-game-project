using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cloudborne
{
    /// <summary>
    /// Look of the placeholder art: toon materials (with outline and glow), additive glow materials,
    /// procedural textures (tiger stripes, grass, path, light shafts) and helpers to build models from parts.
    /// </summary>
    public static class CbArt
    {
        public const float CharacterOutline = 1.15f;
        public const float PropOutline = 0.8f;

        static Shader toon, glow, sparks;
        static readonly Dictionary<(Color, float), Material> shared = new Dictionary<(Color, float), Material>();
        static readonly Dictionary<Color, Material> emissive = new Dictionary<Color, Material>();
        static Material fallback;

        static void Load()
        {
            if (toon != null) return;
            toon = Resources.Load<Shader>("Shaders/Toon");
            glow = Resources.Load<Shader>("Shaders/Glow");
            sparks = Resources.Load<Shader>("Shaders/Sparks");
        }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        // ---------- materials ----------

        /// <summary>Shared toon material of a colour (scenery).</summary>
        public static Material Toon(Color c, float outline = 0f)
        {
            if (shared.TryGetValue((c, outline), out var m) && m != null) return m;
            m = NewToon(c, outline);
            m.SetFloat("_SpecStrength", 0.05f);
            return shared[(c, outline)] = m;
        }

        public static Material Toon(string hex, float outline = 0f) => Toon(Hex(hex), outline);

        /// <summary>A material of its own (characters flash and glow, so they never share).</summary>
        public static Material NewToon(Color c, float outline, Texture texture = null)
        {
            Load();
            Material m;
            if (toon != null && toon.isSupported)
            {
                m = new Material(toon) { color = c };
                m.SetFloat("_OutlineWidth", outline);
                m.SetColor("_ShadowColor", new Color(0.5f, 0.6f, 0.78f));
                m.SetFloat("_SpecStrength", 0.15f);
                m.SetFloat("_RimStrength", 0.32f);
                m.SetColor("_EmissionColor", Color.black);
            }
            else
            {
                if (fallback == null)
                {
                    var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fallback = temp.GetComponent<Renderer>().sharedMaterial;
                    temp.SetActive(false);
                    Object.Destroy(temp);
                }
                m = new Material(fallback) { color = c };
            }
            if (texture != null) m.mainTexture = texture;
            return m;
        }

        /// <summary>Unlit-looking colour (eyes, glowing cores): black albedo plus emission. HDR colours bloom.</summary>
        public static Material Emissive(Color c)
        {
            if (emissive.TryGetValue(c, out var m) && m != null) return m;
            m = NewToon(Color.black, 0f);
            m.SetFloat("_SpecStrength", 0f);
            m.SetFloat("_RimStrength", 0f);
            m.SetColor("_EmissionColor", c);
            return emissive[c] = m;
        }

        /// <summary>Sets a character material's glow (multiplied by its emission mask).</summary>
        public static void SetGlow(Material m, Color c)
        {
            if (m != null && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
        }

        /// <summary>Additive glow with a texture (light shafts, fire cones, auras).</summary>
        public static Material Glow(Color c, Texture texture)
        {
            Load();
            var m = new Material(glow != null && glow.isSupported ? glow : sparks) { renderQueue = (int)RenderQueue.Transparent };
            m.SetColor("_Color", c);
            if (texture != null) m.mainTexture = texture;
            return m;
        }

        static Material sparksMat;
        public static Material Sparks
        {
            get
            {
                if (sparksMat != null) return sparksMat;
                Load();
                sparksMat = new Material(sparks) { name = "CbSparks" };
                sparksMat.SetFloat("_Intensity", 1.6f);
                return sparksMat;
            }
        }

        // ---------- textures ----------

        static Texture2D furTex, furMask, grassTex, pathTex, shaftTex, softTex, flameTex, mistTex;

        /// <summary>Orange fur with black stripes, and the same stripes as a white-on-black glow mask.</summary>
        public static Texture2D Fur { get { if (furTex == null) MakeFur(); return furTex; } }
        public static Texture2D FurMask { get { if (furMask == null) MakeFur(); return furMask; } }

        static void MakeFur()
        {
            const int W = 256, H = 256;
            var rng = new System.Random(11);
            var stripes = new List<float[]>();
            for (int i = 0; i < 13; i++)
                stripes.Add(new[] { (i + 0.5f) * H / 13f + (float)(rng.NextDouble() - 0.5) * 8f, (float)rng.NextDouble() * W, 80 + (float)rng.NextDouble() * 110, 4 + (float)rng.NextDouble() * 6, (float)rng.NextDouble() * 6 });
            Color orange = Hex("#f08a24"), dark = Hex("#1d1310");
            var col = new Color[W * H];
            var mask = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool on = false;
                    foreach (var s in stripes)
                    {
                        float u = ((x - s[1]) % W + W) % W / s[2];
                        if (u > 1f) continue;
                        float yc = s[0] + Mathf.Sin(u * 5 + s[4]) * 5f;
                        if (Mathf.Abs(y - yc) < Mathf.Sin(Mathf.PI * u) * s[3]) { on = true; break; }
                    }
                    col[y * W + x] = on ? dark : orange;
                    mask[y * W + x] = on ? Color.white : Color.black;
                }
            furTex = NewTex(W, H, col, TextureWrapMode.Repeat);
            furMask = NewTex(W, H, mask, TextureWrapMode.Repeat);
        }

        public static Texture2D Grass => grassTex != null ? grassTex : grassTex = Speckle(128, Hex("#3f8a35"),
            new[] { new Color(0.08f, 0.27f, 0.12f, 0.4f), new Color(0.6f, 0.86f, 0.43f, 0.3f) }, 600, 1, 3);

        public static Texture2D Path => pathTex != null ? pathTex : pathTex = Speckle(128, Hex("#8f5f3d"),
            new[] { new Color(0.27f, 0.16f, 0.1f, 0.4f), new Color(0.86f, 0.67f, 0.47f, 0.3f), new Color(0.31f, 0.55f, 0.24f, 0.5f) }, 420, 1, 3);

        /// <summary>Vertical fade (bright at the top) with soft sides: light shafts and fire cones.</summary>
        public static Texture2D Shaft
        {
            get
            {
                if (shaftTex != null) return shaftTex;
                const int W = 32, H = 128;
                var c = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float side = 1f - Mathf.Abs(x / (W - 1f) * 2f - 1f);
                        c[y * W + x] = new Color(1, 1, 1, Mathf.SmoothStep(0, 1, side) * (y / (H - 1f)));
                    }
                return shaftTex = NewTex(W, H, c, TextureWrapMode.Clamp);
            }
        }

        /// <summary>Round soft glow.</summary>
        public static Texture2D Soft
        {
            get
            {
                if (softTex != null) return softTex;
                const int S = 64;
                var c = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float d = new Vector2(x - S / 2f + 0.5f, y - S / 2f + 0.5f).magnitude / (S / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        c[y * S + x] = new Color(1, 1, 1, a * a);
                    }
                return softTex = NewTex(S, S, c, TextureWrapMode.Clamp);
            }
        }

        /// <summary>Flickering flame tongues for the fire aura (scrolled upwards at runtime).</summary>
        public static Texture2D Flame
        {
            get
            {
                if (flameTex != null) return flameTex;
                const int W = 64, H = 64;
                var c = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float u = x / (float)W, v = y / (float)H;
                        float tongue = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 7f + Mathf.Sin(v * Mathf.PI * 2f) * 2f);
                        c[y * W + x] = new Color(1, 1, 1, tongue);
                    }
                return flameTex = NewTex(W, H, c, TextureWrapMode.Repeat);
            }
        }

        /// <summary>Soft cloud of overlapping blobs (ground mist).</summary>
        public static Texture2D Mist
        {
            get
            {
                if (mistTex != null) return mistTex;
                const int W = 128, H = 64;
                var c = new Color[W * H];
                var rng = new System.Random(4);
                for (int i = 0; i < 22; i++)
                {
                    float cx = 20 + (float)rng.NextDouble() * (W - 40), cy = H * (0.4f + 0.3f * (float)rng.NextDouble()), r = 9 + (float)rng.NextDouble() * 16;
                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            float d = new Vector2(x - cx, y - cy).magnitude / r;
                            if (d < 1) c[y * W + x].a = Mathf.Max(c[y * W + x].a, (1 - d) * 0.9f);
                        }
                }
                for (int i = 0; i < c.Length; i++) { c[i].r = c[i].g = c[i].b = 1; }
                return mistTex = NewTex(W, H, c, TextureWrapMode.Clamp);
            }
        }

        static Texture2D Speckle(int size, Color baseColor, Color[] dots, int count, int minR, int maxR)
        {
            var c = new Color[size * size];
            for (int i = 0; i < c.Length; i++) c[i] = baseColor;
            var rng = new System.Random(size + count);
            for (int i = 0; i < count; i++)
            {
                var d = dots[rng.Next(dots.Length)];
                int cx = rng.Next(size), cy = rng.Next(size), r = rng.Next(minR, maxR + 1);
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                    {
                        if (x * x + y * y > r * r) continue;
                        int px = (cx + x + size) % size, py = (cy + y * 2 + size * 2) % size;
                        c[py * size + px] = Color.Lerp(c[py * size + px], new Color(d.r, d.g, d.b, 1), d.a);
                    }
            }
            return NewTex(size, size, c, TextureWrapMode.Repeat);
        }

        static Texture2D NewTex(int w, int h, Color[] pixels, TextureWrapMode wrap)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = wrap, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
            t.SetPixels(pixels);
            t.Apply(true, true);
            return t;
        }

        // ---------- model helpers ----------

        public static Transform Joint(Transform parent, float x = 0, float y = 0, float z = 0, string name = "joint")
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = new Vector3(x, y, z);
            return t;
        }

        /// <summary>Rotation from radians (x, y, z), applied in Unity's Z-X-Y order.</summary>
        public static Quaternion Rot(float x, float y = 0, float z = 0) => Quaternion.Euler(x * Mathf.Rad2Deg, y * Mathf.Rad2Deg, z * Mathf.Rad2Deg);

        public static Transform Part(Mesh mesh, Material mat, Transform parent, Vector3 pos, Vector3 scale, Quaternion? rot = null, bool shadows = true)
        {
            var go = new GameObject(mesh != null ? mesh.name : "part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            return go.transform;
        }

        // Sizes follow three.js conventions so the demo's numbers carry over:
        public static Transform Sphere(Material m, Transform p, Vector3 pos, float r, Vector3? stretch = null) =>
            Part(CbMesh.Sphere, m, p, pos, (stretch ?? Vector3.one) * (2f * r));

        /// <summary>Capsule of radius r whose straight part is <paramref name="length"/> long (total length + 2r).</summary>
        public static Transform Capsule(Material m, Transform p, Vector3 pos, float r, float length, Quaternion? rot = null) =>
            Part(CbMesh.Capsule, m, p, pos, new Vector3(2f * r, (length + 2f * r) / 2f, 2f * r), rot);

        public static Transform Cylinder(Material m, Transform p, Vector3 pos, float r, float h, Quaternion? rot = null) =>
            Part(CbMesh.Tube(), m, p, pos, new Vector3(r, h, r), rot);

        public static Transform Box(Material m, Transform p, Vector3 pos, Vector3 size, Quaternion? rot = null) =>
            Part(CbMesh.Box(), m, p, pos, size, rot);

        /// <summary>Cone centred like three.js ConeGeometry: base at -h/2, apex at +h/2.</summary>
        public static Transform Cone(Material m, Transform p, Vector3 pos, float r, float h, Quaternion? rot = null, int segs = 10)
        {
            var holder = Joint(p, pos.x, pos.y, pos.z, "cone");
            holder.localRotation = rot ?? Quaternion.identity;
            Part(CbMesh.Cone(segs), m, holder, new Vector3(0, -h / 2f, 0), new Vector3(r, h, r));
            return holder;
        }

        /// <summary>Ellipsoid offset helper: a sphere stretched by a scale vector.</summary>
        public static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    }
}
