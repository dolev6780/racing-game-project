using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cloudborne
{
    /// <summary>
    /// Anime fight effects in the world: star-shaped flashes where blows land, expanding shock rings, and afterimages
    /// of a dashing character. Pooled, and animated in unscaled time so they keep playing through hit-stop.
    /// </summary>
    public static class CbVfx
    {
        class Fx
        {
            public Transform t;
            public Material m;
            public MeshFilter mf;
            public Color c;
            public float age, life, size0, size1, spin;
            public bool billboard, active;
        }

        static readonly List<Fx> quads = new List<Fx>();
        static readonly List<Fx> ghosts = new List<Fx>();
        static Transform holder;
        static Texture2D starTex, ringTex;

        public static void Init(Transform parent)
        {
            holder = new GameObject("VFX").transform;
            holder.SetParent(parent, false);
            quads.Clear();
            ghosts.Clear();
        }

        /// <summary>A star-shaped flash where a blow lands.</summary>
        public static void Burst(Vector3 pos, Color c, float size, float life = 0.14f)
            => Spawn(pos, c, Star, size * 0.55f, size, life, true, Random.Range(0f, 360f));

        /// <summary>An expanding ring: across the view for heavy blows, flat on the ground for slams and the roar.</summary>
        public static void Ring(Vector3 pos, Color c, float radius, float life = 0.3f, bool ground = false)
        {
            var fx = Spawn(pos, c, RingTexture, radius * 0.3f, radius * 2f, life, !ground, 0);
            if (fx != null && ground) fx.t.rotation = Quaternion.Euler(90, 0, 0);
        }

        /// <summary>A glowing copy of a skinned character in its current pose, fading where it stood.</summary>
        public static void Ghost(SkinnedMeshRenderer skin, Color c, float life = 0.25f)
        {
            if (holder == null || skin == null) return;
            var fx = ghosts.Find(g => !g.active);
            if (fx == null)
            {
                var go = new GameObject("afterimage");
                go.transform.SetParent(holder, false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = new Mesh { name = "afterimage" };
                var mr = go.AddComponent<MeshRenderer>();
                var m = CbArt.Glow(c, null);
                mr.sharedMaterial = m;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                fx = new Fx { t = go.transform, m = m, mf = mf };
                ghosts.Add(fx);
            }
            skin.BakeMesh(fx.mf.sharedMesh, true);
            var st = skin.transform;
            fx.t.SetPositionAndRotation(st.position, st.rotation);
            fx.t.localScale = Vector3.one * BakedScale(skin, fx.mf.sharedMesh);
            fx.c = c;
            fx.age = 0;
            fx.life = life;
            fx.active = true;
            fx.m.SetColor("_Color", c);
            fx.t.gameObject.SetActive(true);
        }

        /// <summary>
        /// The scale that shows a baked copy at the character's size. The baked vertices may come out in the renderer's
        /// own units (divided by its world scale) or already at world size, so compare them with the renderer's own
        /// local bounds and pick: a copy left at the wrong one is several times too big and floods the view with glow.
        /// </summary>
        static float BakedScale(SkinnedMeshRenderer skin, Mesh baked)
        {
            float world = skin.transform.lossyScale.x;
            float bakedSize = baked.bounds.size.magnitude, localSize = skin.localBounds.size.magnitude;
            if (bakedSize < 1e-6f || localSize < 1e-6f || world < 1e-6f) return 1f;
            // in local units the two sizes match; at world size the baked one is `world` times larger
            float k = bakedSize / localSize;
            return Mathf.Abs(Mathf.Log(k)) < Mathf.Abs(Mathf.Log(k / world)) ? world : 1f;
        }

        static Fx Spawn(Vector3 pos, Color c, Texture tex, float size0, float size1, float life, bool billboard, float spin)
        {
            if (holder == null) return null;
            var fx = quads.Find(q => !q.active);
            if (fx == null)
            {
                var m = CbArt.Glow(c, tex);
                fx = new Fx { t = CbArt.Part(CbMesh.Quad, m, holder, Vector3.zero, Vector3.one, shadows: false), m = m };
                quads.Add(fx);
            }
            fx.m.mainTexture = tex;
            fx.c = c;
            fx.age = 0;
            fx.life = life;
            fx.size0 = size0;
            fx.size1 = size1;
            fx.billboard = billboard;
            fx.spin = spin;
            fx.active = true;
            fx.t.position = pos;
            fx.t.localScale = Vector3.one * size0;
            fx.m.SetColor("_Color", c);
            fx.t.gameObject.SetActive(true);
            return fx;
        }

        public static void Update(Camera cam, float dt)
        {
            foreach (var fx in quads)
            {
                if (!fx.active) continue;
                fx.age += dt;
                float k = fx.age / fx.life;
                if (k >= 1) { fx.active = false; fx.t.gameObject.SetActive(false); continue; }
                float grow = 1 - (1 - k) * (1 - k);            // fast out, then settling
                fx.t.localScale = Vector3.one * Mathf.Lerp(fx.size0, fx.size1, grow);
                if (fx.billboard && cam != null) fx.t.rotation = cam.transform.rotation * Quaternion.Euler(0, 0, fx.spin);
                var c = fx.c;
                c.a *= 1 - k * k;
                fx.m.SetColor("_Color", c);
            }
            foreach (var fx in ghosts)
            {
                if (!fx.active) continue;
                fx.age += dt;
                float k = fx.age / fx.life;
                if (k >= 1) { fx.active = false; fx.t.gameObject.SetActive(false); continue; }
                var c = fx.c;
                c.a *= 1 - k;
                fx.m.SetColor("_Color", c);
            }
        }

        // ---------- textures ----------

        /// <summary>Eight sharp rays, long and short in turn, around a hot core.</summary>
        static Texture2D Star
        {
            get
            {
                if (starTex != null) return starTex;
                const int S = 128;
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        var d = new Vector2(x - S / 2f + 0.5f, y - S / 2f + 0.5f) / (S / 2f);
                        float r = d.magnitude, a = Mathf.Atan2(d.y, d.x) + Mathf.PI;
                        // the nearest of 8 rays, long and short in turn, each a spike tapering to its tip
                        int n = Mathf.RoundToInt(a / (Mathf.PI / 4)), k = n % 8;
                        float off = Mathf.Abs(a - n * Mathf.PI / 4);
                        float len = k % 2 == 0 ? 1f : 0.62f;
                        float half = 0.13f * (1 - r / len);
                        float ray = r < len ? Mathf.Clamp01((half - r * Mathf.Sin(off)) / 0.025f) : 0;
                        float v = ray + Mathf.Clamp01(1 - r / 0.32f) * 1.2f;
                        px[y * S + x] = new Color(1, 1, 1, Mathf.Clamp01(v));
                    }
                return starTex = Tex(S, px);
            }
        }

        /// <summary>A thin bright band, empty inside (a filled one reads as a bubble).</summary>
        static Texture2D RingTexture
        {
            get
            {
                if (ringTex != null) return ringTex;
                const int S = 128;
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float r = new Vector2(x - S / 2f + 0.5f, y - S / 2f + 0.5f).magnitude / (S / 2f);
                        float band = Mathf.Clamp01(1 - Mathf.Abs(r - 0.86f) / 0.05f);
                        px[y * S + x] = new Color(1, 1, 1, band * band);
                    }
                return ringTex = Tex(S, px);
            }
        }

        /// <summary>Radial streaks with a clear middle: the anime speed-line overlay (used by the HUD).</summary>
        public static Texture2D SpeedLines(int size)
        {
            var px = new Color[size * size];
            const int lines = 110;
            var rng = new System.Random(5);
            var inner = new float[lines];
            var width = new float[lines];
            for (int i = 0; i < lines; i++)
            {
                inner[i] = 0.55f + (float)rng.NextDouble() * 0.35f;
                width[i] = 0.25f + (float)rng.NextDouble() * 0.45f;
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var d = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f) / (size / 2f);
                    float r = d.magnitude;
                    float u = (Mathf.Atan2(d.y, d.x) / (2 * Mathf.PI) + 0.5f) * lines;
                    int i = Mathf.Min((int)u, lines - 1);
                    float across = Mathf.Abs(u - i - 0.5f) * 2;                  // 0 in the middle of a line's slot
                    float body = Mathf.Clamp01((width[i] * Mathf.Clamp01((r - inner[i]) / 0.5f) - across) * 6);
                    float a = body * Mathf.Clamp01((r - inner[i]) * 5);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            return Tex(size, px);
        }

        static Texture2D Tex(int s, Color[] px)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }
    }
}
