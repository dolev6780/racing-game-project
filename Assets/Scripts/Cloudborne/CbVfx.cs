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
            public float age, life, size0, size1, spin, flip = 1f, rise;
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
            clouds.Clear();
            ghosts.Clear();
        }

        // The painted effects (the user approved them: drawn in Canva on black, cut into Resources/Cloudborne/Effects) -
        // a star burst, a slash arc, sparkles and a ring that add light, a cloud of smoke that doesn't. Where one isn't in
        // the project, the effect drawn in code stands in.

        /// <summary>A star-shaped flash where a blow lands.</summary>
        public static void Burst(Vector3 pos, Color c, float size, float life = 0.14f)
            => Spawn(pos, c, Painted("Burst") ?? Star, size * 0.55f, size, life, true, Random.Range(0f, 360f));

        /// <summary>A slash arc across where a staff blow lands, sweeping the way it goes (dir), tinted c.</summary>
        public static void Slash(Vector3 pos, int dir, Color c, float size, float life = 0.16f)
        {
            var tex = Painted("Slash");
            if (tex == null) return;
            var fx = Spawn(pos, c, tex, size * 0.75f, size, life, true, Random.Range(-25f, 25f));
            if (fx != null) fx.flip = dir < 0 ? 1f : -1f;
        }

        /// <summary>Gold sparkles: a pickup, a perfect guard.</summary>
        public static void Sparkle(Vector3 pos, Color c, float size, float life = 0.35f)
        {
            var tex = Painted("Sparkle");
            if (tex == null) { CbFx.Spark(pos, c, 6, 2f, 0.3f, life, -2); return; }
            var fx = Spawn(pos, c, tex, size * 0.6f, size, life, true, Random.Range(-15f, 15f));
            if (fx != null) fx.rise = 0.8f;
        }

        /// <summary>A cloud of dust or smoke billowing up (a crash, a heavy landing): drawn over the scene, not glowing.</summary>
        public static void Smoke(Vector3 pos, Color c, float size, float life = 0.55f)
        {
            var tex = Painted("Smoke");
            if (tex == null) { CbFx.Puff(pos, c, 6, 2f, size * 0.5f, life); return; }
            var fx = Spawn(pos, c, tex, size * 0.45f, size, life, true, Random.Range(-10f, 10f), clouds: true);
            if (fx != null) fx.rise = 0.6f;
        }

        /// <summary>
        /// A fiery explosion: the painted one (Resources/Cloudborne/Effects/Explosion, drawn in Canva on black, so
        /// it adds light) if it is in the project, else a hot star.
        /// </summary>
        public static void Explosion(Vector3 pos, float size, float life = 0.22f)
        {
            var tex = Painted("Explosion");
            if (tex == null) { Burst(pos, CbFx.Flame, size, life); return; }
            Spawn(pos, new Color(1, 1, 1, 1) * 1.6f, tex, size * 0.5f, size, life, true, Random.Range(0f, 360f));
        }

        /// <summary>
        /// A stroke of ink (the user's ideas list: brush strokes on big blows, as a painted anime game's): a dark crescent
        /// swept the way the blow goes (dir), tapering at both ends, ragged at its edges, a few drops flung off it -
        /// drawn over the scene, not glowing.
        /// </summary>
        public static void Ink(Vector3 pos, int dir, float size, float life = 0.32f)
        {
            var fx = Spawn(pos, new Color(0.07f, 0.05f, 0.09f, 0.92f), InkTexture, size * 0.8f, size, life, true, Random.Range(-30f, 20f), clouds: true);
            if (fx != null) fx.flip = dir < 0 ? -1f : 1f;
        }

        static Texture2D inkTex;
        /// <summary>The ink stroke, made once: a brushed crescent with ragged edges and drops (white; tinted when used).</summary>
        static Texture2D InkTexture
        {
            get
            {
                if (inkTex != null) return inkTex;
                const int W = 256, H = 256;
                inkTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "InkStroke", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[W * H];
                var rng = new System.Random(7);
                // the crescent: an arc round a point below the middle, thick in the middle, thin at the ends
                float cx = W * 0.5f, cy = H * 0.15f, r0 = H * 0.55f;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                        // along the arc from -1 (one end) to 1 (the other)
                        float u = (a - Mathf.PI / 2) / (Mathf.PI * 0.42f);
                        float half = Mathf.Max(0, 1 - u * u) * H * 0.09f;
                        // the brush's bristles: the edge wavers with the angle
                        float edge = half * (0.75f + 0.25f * Mathf.PerlinNoise(a * 9f, r * 0.08f));
                        float d = Mathf.Abs(r - r0);
                        float alpha = Mathf.Clamp01((edge - d) / 2.5f);
                        // streaks along it where the brush ran dry
                        alpha *= 0.65f + 0.35f * Mathf.PerlinNoise(a * 40f, 3.1f);
                        px[y * W + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                    }
                // drops flung off its outer edge
                for (int k = 0; k < 14; k++)
                {
                    float a = Mathf.PI / 2 + ((float)rng.NextDouble() - 0.5f) * Mathf.PI * 0.8f;
                    float r = r0 + H * (0.1f + (float)rng.NextDouble() * 0.2f);
                    float rad = 2f + (float)rng.NextDouble() * 5f;
                    int ox = Mathf.RoundToInt(cx + Mathf.Cos(a) * r), oy = Mathf.RoundToInt(cy + Mathf.Sin(a) * r);
                    for (int y = oy - 8; y <= oy + 8; y++)
                        for (int x = ox - 8; x <= ox + 8; x++)
                        {
                            if (x < 0 || y < 0 || x >= W || y >= H) continue;
                            float dd = Mathf.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy));
                            byte al = (byte)(Mathf.Clamp01((rad - dd) / 1.5f) * 255);
                            if (al > px[y * W + x].a) px[y * W + x] = new Color32(255, 255, 255, al);
                        }
                }
                inkTex.SetPixels32(px);
                inkTex.Apply();
                return inkTex;
            }
        }

        static readonly Dictionary<string, Texture2D> painted = new Dictionary<string, Texture2D>();

        /// <summary>A painted effect from Resources/Cloudborne/Effects, or null; looked up once.</summary>
        public static Texture2D Painted(string name)
        {
            if (!painted.TryGetValue(name, out var t))
            {
                t = Resources.Load<Texture2D>("Cloudborne/Effects/" + name);
                if (t != null) t.wrapMode = name == "Beam" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                painted[name] = t;
            }
            return t;
        }

        /// <summary>An expanding ring: across the view for heavy blows, flat on the ground for slams and the roar.</summary>
        public static void Ring(Vector3 pos, Color c, float radius, float life = 0.3f, bool ground = false)
        {
            // on the ground the painted ring (drawn as seen from a little above: it stands facing the camera); across the
            // view, a round band
            var painted = ground ? Painted("Ring") : null;
            if (painted != null) { Spawn(pos + new Vector3(0, 0.15f, 0), c, painted, radius * 0.4f, radius * 2.2f, life, true, 0); return; }
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

        static readonly List<Fx> clouds = new List<Fx>();
        static Shader paintedFx;

        static Fx Spawn(Vector3 pos, Color c, Texture tex, float size0, float size1, float life, bool billboard, float spin, bool clouds = false)
        {
            if (holder == null) return null;
            var pool = clouds ? CbVfx.clouds : quads;
            var fx = pool.Find(q => !q.active);
            if (fx == null)
            {
                Material m;
                if (clouds)
                {
                    if (paintedFx == null) paintedFx = Resources.Load<Shader>("Shaders/PaintedFx");
                    m = paintedFx != null && paintedFx.isSupported
                        ? new Material(paintedFx) { mainTexture = tex, renderQueue = (int)RenderQueue.Transparent }
                        : CbArt.Glow(c, tex);
                    m.SetColor("_Color", c);
                }
                else m = CbArt.Glow(c, tex);
                fx = new Fx { t = CbArt.Part(CbMesh.Quad, m, holder, Vector3.zero, Vector3.one, shadows: false), m = m };
                pool.Add(fx);
            }
            fx.flip = 1f;
            fx.rise = 0f;
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
            UpdateQuads(quads, cam, dt);
            UpdateQuads(clouds, cam, dt);
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

        static void UpdateQuads(List<Fx> list, Camera cam, float dt)
        {
            foreach (var fx in list)
            {
                if (!fx.active) continue;
                fx.age += dt;
                float k = fx.age / fx.life;
                if (k >= 1) { fx.active = false; fx.t.gameObject.SetActive(false); continue; }
                float grow = 1 - (1 - k) * (1 - k);            // fast out, then settling
                float s = Mathf.Lerp(fx.size0, fx.size1, grow);
                fx.t.localScale = new Vector3(s * fx.flip, s, s);
                if (fx.rise != 0) fx.t.position += Vector3.up * fx.rise * dt;
                if (fx.billboard && cam != null) fx.t.rotation = cam.transform.rotation * Quaternion.Euler(0, 0, fx.spin);
                var c = fx.c;
                c.a *= 1 - k * k;
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

        static Texture2D tailTex, streakTex, coreTex;

        /// <summary>A comet tail along U: bright and full at u = 1 (the head), fading and thinning to u = 0.</summary>
        public static Texture2D Tail => tailTex != null ? tailTex : tailTex = Make(128, 32, TextureWrapMode.Clamp, (u, v) =>
        {
            float across = 1 - Mathf.Abs(v - 0.5f) * 2;
            return Mathf.Pow(u, 1.6f) * Mathf.Clamp01(across * (0.6f + u) * 1.4f) * across;
        });

        /// <summary>Streaks of fire along U (repeating), thickest in the middle row: the body of the flame cannon.</summary>
        public static Texture2D BeamStreaks => streakTex != null ? streakTex : streakTex = Make(128, 64, TextureWrapMode.Repeat, (u, v) =>
        {
            float across = 1 - Mathf.Abs(v - 0.5f) * 2;
            // a few streaks per row, each row with its own phase; whole cycles across U so it repeats seamlessly
            float row = v * 9;
            float a = Mathf.Sin(u * Mathf.PI * 2 * 3 + Mathf.Floor(row) * 2.1f), b = Mathf.Sin(u * Mathf.PI * 2 * 5 + Mathf.Floor(row) * 4.7f);
            float streak = 0.55f + 0.25f * a + 0.2f * b;
            return Mathf.Clamp01(Mathf.Pow(across, 0.7f) * streak * 1.3f);
        });

        /// <summary>A soft white-hot line along U: the beam's core.</summary>
        public static Texture2D BeamCore => coreTex != null ? coreTex : coreTex = Make(16, 32, TextureWrapMode.Repeat, (u, v) =>
            Mathf.Pow(1 - Mathf.Abs(v - 0.5f) * 2, 2f));

        static Texture2D Make(int w, int h, TextureWrapMode wrap, System.Func<float, float, float> alpha)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color(1, 1, 1, alpha((x + 0.5f) / w, (y + 0.5f) / h));
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = wrap, filterMode = FilterMode.Bilinear };
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
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
