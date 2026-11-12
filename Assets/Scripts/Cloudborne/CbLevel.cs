using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The shape of the stage beyond its flat path: things to climb, things in the way, and water.
    /// <list type="bullet">
    /// <item>Blocks: solid ground Raja can't walk through but can stand on: the cracked wall, the mountain's
    /// terraces, a rock in the river, the stone arch bridge.</item>
    /// <item>Ledges: floating slabs he jumps up through and lands on; a crumbling one gives way under him.</item>
    /// <item>Water: streams cut into the ground across the path (CbWorld carves their channels). Falling in puts Raja
    /// back on the last safe footing he stood on and costs health. What crosses them: a rope bridge (a deck walked onto
    /// from the bank, sagging in the middle), stepping stones (some sink when stood on), floating logs (some drift
    /// to and fro, carrying him), a rock, and a stone arch bridge.</item>
    /// <item>Walls: the sides of the blocks. Pressing into one in the air he slides down it slowly and can jump off
    /// it; a terrace's top corner or a ledge's end within reach of his raised hands he catches and climbs.</item>
    /// </list>
    /// Stage 1-1, about 265 m: the village; the first fight; a stream under a rope bridge; the second fight; the
    /// cracked wall (only the staff breaks it); a long river of stones and logs; the ambush at the shrine; a
    /// gorge climbed with water rising behind him (CbLevelGorge), the golden fruit off its way and a secret path from its
    /// summit to the scroll above; the fight on the ridge; a brook under a stone arch bridge;
    /// Sergeant Snap at the shrine gate.
    /// </summary>
    public partial class CbLevel
    {
        class Block
        {
            public float x0, x1, top;
            public bool breakable, broken;
            public int hp;
            public Transform t;
            // smooth: not caught or jumped off (the temple's gates, thorns); vines: its side (-1 left, 1 right) is
            // grown over with vines he climbs
            public bool smooth;
            // a rock jutting from the gorge's back wall stands clear of the ground: nothing under its bottom
            public float bottom;
            public int vines;
        }

        class Ledge
        {
            public float x0, x1, top;
            public bool crumbles, gone;
            public float stood, respawn;
            public Transform t;
            public Vector3 home;
        }

        /// <summary>Something over the water to stand on: a bridge's deck, a stepping stone or a log.</summary>
        class Crossing
        {
            public float cx, half, top, sag, dip, bob, stood, dx, sunk, shake;
            // stretches of a deck with no planks (x0..x1 in the world): nothing to stand on
            public List<(float x0, float x1)> holes;
            public bool HoleAt(float x)
            {
                if (holes != null)
                    foreach (var (h0, h1) in holes)
                        if (x > h0 + HalfWidth * 0.4f && x < h1 - HalfWidth * 0.4f) return true;
                return false;
            }
            // drifting logs move to and fro by drift metres every period seconds
            public float drift, period;
            // a deck is stepped onto from the bank; the rest only landed on from above
            public bool deck, sinks, bobs;
            // a sinking stone: 0 resting, 1 going down, 2 under (it stays there until the stage starts over)
            public int phase;
            public Transform t;
            public Vector3 home;
            public float X0 => cx + dx - half;
            public float X1 => cx + dx + half;
            public bool Safe => deck || (!sinks && !bobs && drift == 0);
            public float Top(float x)
            {
                float u = Mathf.Clamp01((x - X0) / Mathf.Max(0.01f, 2 * half));
                return top - sag * Mathf.Sin(u * Mathf.PI) + bob - dip;
            }
        }

        public const float HalfWidth = 0.35f;
        // the cracked wall: taller than a double jump, two staff blows break it
        public const float WallX = 76f, WallHeight = 4.6f;
        const float CrumbleAfter = 0.5f, CrumbleBack = 3f;

        /// <summary>The streams across stage 1-1 (x from, to): the rope bridge's, the long river, the brook.</summary>
        public static readonly (float x0, float x1)[] Waters = { (31.4f, 55.4f), (79f, 103f), (200f, 232f), (319f, 369f), (494.4f, 500.4f) };
        /// <summary>The water's surface, below the banks: its channel is cut into the ground.</summary>
        public const float WaterLevel = -0.45f;
        /// <summary>How far back from the path the streams run before they come out of the ground.</summary>
        public const float WaterBack = 6.5f;
        // below the path where the water is: deep enough that falling in is falling in
        const float Deep = -50f, StepUp = 0.3f;
        // the long rope bridge over the first stream (a screen and a half: the user), sagging RopeSag in the middle,
        // strung from the kit Tools/Blender/bridge_kit.py cuts from the bridge model: an end with its bamboo posts at
        // each side and RopeSections sections of planks and rails between; RopeHoles have no planks (he falls through)
        public const float RopeBridgeX = 43.4f, RopeDeckHalf = 12.3f;
        // (it sagged 0.55 m and its middle touched the water: the user)
        const float RopeSag = 0.25f, RopeEndIn = 0.6f;
        public const int RopeSections = 11;
        static readonly int[] RopeHoles = { 2, 5, 8 };

        /// <summary>Where the rope bridge's section k runs (x0 to x1), and whether it has no planks.</summary>
        public static (float x0, float x1, bool hole) RopeSection(int k)
        {
            float a = RopeBridgeX - RopeDeckHalf + RopeEndIn, b = RopeBridgeX + RopeDeckHalf - RopeEndIn, w = (b - a) / RopeSections;
            return (a + k * w, a + (k + 1) * w, System.Array.IndexOf(RopeHoles, k) >= 0);
        }
        public const float StoneBridgeX = 497.4f;
        // each arch (the model, 4.8 m long, scaled), their spacing, the bridge's half length; the deck between the
        // parapets is about 1.76 of the model's 2.29 m height (the parapets' tops 2.18)
        const float StoneArchLength = 4.1f, StoneArchStep = 3.96f;
        public const float StoneBridgeHalf = (StoneArchStep + StoneArchLength) / 2, StoneDeck = 1.32f;
        // the mountain: where it stands, its summit's height
        public const float MountainFrom = 137f, MountainTo = 168f, SummitTop = 11.4f;
        // an edge he can catch: its top between these heights above his feet (his raised hands hang at
        // CbHero.HangReach), and its corner just ahead of him (he presses against a terrace's face at HalfWidth).
        // Wide, so a jump at an edge is caught (the user found he did not always catch it).
        public const float GrabLow = 0.95f, GrabHigh = 1.95f;
        const float GrabNear = HalfWidth - 0.2f, GrabFar = HalfWidth + 0.55f;

        readonly CbGame g;
        readonly Transform root;
        bool climbTold;
        readonly List<Block> blocks = new List<Block>();
        readonly List<Ledge> ledges = new List<Ledge>();
        readonly List<Crossing> crossings = new List<Crossing>();
        Material earth, grass, crackedEarth;

        public bool WallStanding => blocks.Exists(b => b.breakable && !b.broken);

        public CbLevel(CbGame game, Transform parent)
        {
            g = game;
            root = new GameObject("Level").transform;
            root.SetParent(parent, false);
            Build();
            BuildWays();
        }

        /// <summary>Whether x is over one of the streams (margin widens them, for keeping scenery off the water).</summary>
        public static bool InWater(float x, float margin = 0f)
        {
            foreach (var (x0, x1) in Waters)
                if (x > x0 - margin && x < x1 + margin) return true;
            return false;
        }

        void Build()
        {
            // the cracked wall across the path
            AddBlock(WallX, 1.3f, WallHeight, breakable: true);
            BuildCrossings();
            BuildMountain();
            // the end of the stage: a shrine gate on the path to walk through
            var goal = CbArt.Joint(root, CbWorld.GoalX, 0, 0.8f, "goalGate");
            if (CbArt.Prop("Cloudborne/Jungle/Gate/Gate", goal, CbArt.PropOutline) is Transform gate) gate.localRotation = Quaternion.Euler(0, 180, 0) * gate.localRotation;
        }

        // ---------- the water ----------

        void BuildCrossings()
        {
            // the rope bridge over the first stream: its planks just above the banks at the ends, sagging in the middle,
            // a few stretches without planks to fall through
            var bridge = new Crossing { cx = RopeBridgeX, half = RopeDeckHalf, top = 0.145f, sag = RopeSag, deck = true };
            bridge.t = StringRopeBridge(bridge);
            bridge.home = bridge.t != null ? bridge.t.localPosition : Vector3.zero;
            crossings.Add(bridge);

            // the long river past the cracked wall: stones, some sinking under him; logs, some drifting; a rock
            AddStone(80.6f, false);
            AddLog(83.6f, 1.0f, 4.2f);
            AddStone(86.6f, true);
            // (wide enough for a fight: a soldier guards it - knocked off it, he's in the river)
            AddRock(89f, 2.2f, 1.3f);
            AddLog(92.6f, 1.2f, 5.2f);
            AddStone(95.2f, true);
            AddStone(97.4f, false);
            AddLog(99.8f, 0, 0);

            // the brook by the ridge, under a stone bridge of two arches, half a screen long (it was one short arch, and he
            // walked along the top of its parapet: the user). He jumps up onto it at either end; its deck lies between
            // the parapets, low enough that the near one hides his feet. The arches overlap a little, their end posts
            // making one pier in the middle.
            var stone = CbArt.Joint(root, StoneBridgeX, 0, 0.15f, "stoneBridge");
            bool modelled = false;
            for (int k = 0; k < 2; k++)
            {
                var arch = CbArt.Joint(stone, (k - 0.5f) * StoneArchStep, 0, 0, "arch");
                if (CbArt.Prop("Cloudborne/Jungle/StoneBridge/StoneBridge", arch, CbArt.PropOutline) == null) continue;
                arch.localScale = new Vector3(StoneArchLength / 4.8f, 0.75f, 0.75f);
                modelled = true;
            }
            if (!modelled) CbArt.Box(CbArt.Toon("#8f9a8a", CbArt.PropOutline), stone, new Vector3(0, StoneDeck / 2, 0), new Vector3(StoneBridgeHalf * 2, StoneDeck, 1.6f));
            blocks.Add(new Block { x0 = StoneBridgeX - StoneBridgeHalf, x1 = StoneBridgeX + StoneBridgeHalf, top = StoneDeck, t = stone });
        }

        /// <summary>A stepping stone, its top level with the banks; a sinking one shakes and goes under if he stays.</summary>
        void AddStone(float x, bool sinks)
        {
            // a round river stone, mossy on top, reaching down into the water (drawn in Canva, built in Meshy: the old
            // stone, stood up twice as tall, looked stretched: the user), turned this way or that
            var t = CbArt.Joint(root, x, 0, 0.05f, "stone");
            var turn = CbArt.Joint(t, 0, 0, 0, "turn");
            turn.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            if (Fit(RiverStonePath, turn, new Vector3(-0.62f, WaterLevel - 0.12f, -0.62f), new Vector3(0.62f, 0.05f, 0.62f)) == null)
            {
                Object.Destroy(t.gameObject);
                t = PlaceProp("Cloudborne/Jungle/SteppingStone/SteppingStone", new Vector3(x, -0.56f, 0.05f), new Vector3(1f, 2f, 1f), Random.Range(0f, 360f));
            }
            if (t == null)
            {
                t = CbArt.Joint(root, x, 0, 0.05f, "stone");
                CbArt.Box(CbArt.Toon(sinks ? "#9a8f7a" : "#8f9a8a", CbArt.PropOutline), t, new Vector3(0, -0.3f, 0), new Vector3(1.1f, 0.6f, 0.9f));
            }
            // a sinking stone is a little paler, with a crack: a hint
            if (sinks)
                foreach (var r in t.GetComponentsInChildren<Renderer>()) r.material.color *= new Color(1.1f, 1.05f, 0.95f);
            crossings.Add(new Crossing { cx = x, half = 0.55f, top = 0f, sinks = sinks, t = t, home = t.localPosition });
        }

        /// <summary>A floating log, half under water, bobbing; with drift it floats to and fro, carrying him.</summary>
        void AddLog(float x, float drift, float period)
        {
            var t = PlaceProp("Cloudborne/Jungle/Log/Log", new Vector3(x, -0.73f, 0.05f), Vector3.one, 0);
            if (t == null)
            {
                t = CbArt.Joint(root, x, -0.07f, 0.05f, "log");
                CbArt.Cylinder(CbArt.Toon("#8a5a32", CbArt.PropOutline), t, new Vector3(0, -0.33f, 0), 0.33f, 1.9f, CbArt.Rot(0, 0, Mathf.PI / 2));
            }
            crossings.Add(new Crossing { cx = x, half = 0.95f, top = -0.07f, bobs = true, drift = drift, period = Mathf.Max(0.1f, period), t = t, home = t.localPosition });
        }

        /// <summary>A rock standing up out of the river: solid, a safe place to land.</summary>
        void AddRock(float x, float width, float top)
        {
            // one solid natural rock (the old one looked like blocks stacked one on another: the user): the tall river
            // rock for a tall one, the round river stone for a broad, low one
            var t = CbArt.Joint(root, x, 0, 0, "riverRock");
            bool tall = top - (WaterLevel - 0.6f) > width;
            if (FitTop(tall ? RiverRockPath : RiverStonePath, t, new Vector3(-width / 2, WaterLevel - 0.6f, -0.8f), new Vector3(width / 2, top, 1f)) == null
                && Fit(CliffDir + "Pillar/Pillar", t, new Vector3(-width / 2, WaterLevel - 0.6f, -0.8f), new Vector3(width / 2, top, 1f)) == null)
                EarthSlab(t, -width / 2, width / 2, WaterLevel - 0.6f, top, -0.8f, 1f, earth ??= EarthMat(), grass ??= GrassMat());
            blocks.Add(new Block { x0 = x - width / 2, x1 = x + width / 2, top = top, t = t });
        }

        // ---------- the cliff kit ----------

        // Mossy stone drawn in Canva and built in Meshy (Resources/Cloudborne/Jungle/Cliff): a terrace block, a pillar,
        // a floating ledge and a cracked one. Each is fitted to the box it stands for; big terraces are built of
        // several blocks so the stone keeps its proportions.
        const string CliffDir = "Cloudborne/Jungle/Cliff/";

        // ---------- the mountain's rock ----------

        // The mountain's terraces are rock, not blocks piled up: a painted cliff texture (Cliff/RockFace, drawn in Canva;
        // without it, rock drawn in code) repeated across every face, lined up with the world; bare rock, no grass.
        const float RockTile = 4f;
        static Texture2D rockTex;
        static bool rockPainted;

        /// <summary>The rock's tint: the painting takes the tone of the rest of the painted scenery (the path's earth face).</summary>
        static Color RockTone()
        {
            RockTexture();
            return rockPainted ? CbWorld.SceneryTone : new Color(0.8f, 0.78f, 0.82f);
        }

        static Texture2D RockTexture()
        {
            if (rockTex != null) return rockTex;
            rockTex = Resources.Load<Texture2D>(CliffDir + "RockFace");
            rockPainted = rockTex != null;
            if (rockTex == null)
            {
                // layered grey-brown rock: wavy strata, darker cracks between them, patches of moss
                const int S = 128;
                rockTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "rock" };
                Color a = CbArt.Hex("#8a8072"), b = CbArt.Hex("#a69a86"), crack = CbArt.Hex("#4e463e"), moss = CbArt.Hex("#6f8f45");
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float u = x / (float)S * Mathf.PI * 2, v = y / (float)S * Mathf.PI * 2;
                        float strata = Mathf.Sin(v * 4 + Mathf.Sin(u * 2) * 0.6f + Mathf.Sin(u * 5 + v) * 0.15f);
                        var c = Color.Lerp(a, b, 0.5f + 0.5f * Mathf.Sin(u * 3 + v * 2) * 0.6f);
                        if (strata > 0.86f) c = Color.Lerp(c, crack, (strata - 0.86f) / 0.14f);
                        float m = Mathf.Sin(u * 2 + 1) * Mathf.Sin(v * 3 + Mathf.Sin(u)) ;
                        if (m > 0.55f) c = Color.Lerp(c, moss, Mathf.Clamp01((m - 0.55f) / 0.2f) * 0.8f);
                        px[y * S + x] = c;
                    }
                rockTex.SetPixels(px);
                rockTex.Apply();
            }
            rockTex.wrapMode = TextureWrapMode.Repeat;
            return rockTex;
        }

        /// <summary>The rock for a face w by h whose lower left corner is at world (wx, wy): the painting lined up with the world.</summary>
        static Material RockMat(float w, float h, float wx, float wy, Color? tint = null)
        {
            var m = CbArt.NewToon(tint ?? RockTone(), 0f, RockTexture());
            m.SetFloat("_SpecStrength", 0f);
            m.SetFloat("_RimStrength", 0f);
            m.mainTextureScale = new Vector2(w / RockTile, h / RockTile);
            m.mainTextureOffset = new Vector2(wx / RockTile, wy / RockTile);
            return m;
        }

        /// <summary>
        /// A terrace's rock filling x0..x1, y0..y1 (relative to parent) from zFront back to zBack: not a box but a
        /// block of natural rock (the user asked for the rocks and walls to look better). Its outline as the camera
        /// sees it has rounded top corners and sides that bulge and dip a little, its top kept flat where Raja
        /// stands; that outline is pushed back into a solid. The face is the painted rock, lined up with the world;
        /// the sides are darker, turned from the light; the top is lighter; a dark line inks the face's edge, as the
        /// characters are outlined. Where he can stand and climb stays the block's box.
        /// </summary>
        void RockFormation(Transform parent, float x0, float x1, float y0, float y1, float zFront, float zBack, int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            const float Corner = 0.3f, Ink = 0.05f;
            float h = y1 - y0, w = x1 - x0;
            // the outline, clockwise as the camera sees it: up the left side, along the top, down the right side
            var pts = new List<Vector2> { new Vector2(x0 + R(-0.02f, 0.04f), y0) };
            int n = Mathf.Max(1, Mathf.RoundToInt((h - Corner) / 0.8f));
            // the sides dip in more than they bulge out: the face stands in front of Raja, so a bulge would hide him
            for (int i = 1; i < n; i++) pts.Add(new Vector2(x0 + R(-0.05f, 0.18f), y0 + (h - Corner) * i / n));
            pts.Add(new Vector2(x0 + R(-0.02f, 0.03f), y1 - Corner));
            pts.Add(new Vector2(x0 + Corner * 0.3f, y1 - Corner * 0.3f));
            pts.Add(new Vector2(x0 + Corner, y1));
            int m = Mathf.Max(1, Mathf.RoundToInt((w - 2 * Corner) / 0.9f));
            for (int i = 1; i < m; i++) pts.Add(new Vector2(x0 + Corner + (w - 2 * Corner) * i / m, y1 - R(0f, 0.04f)));
            pts.Add(new Vector2(x1 - Corner, y1));
            pts.Add(new Vector2(x1 - Corner * 0.3f, y1 - Corner * 0.3f));
            pts.Add(new Vector2(x1 + R(-0.03f, 0.02f), y1 - Corner));
            for (int i = n - 1; i >= 1; i--) pts.Add(new Vector2(x1 + R(-0.18f, 0.05f), y0 + (h - Corner) * i / n));
            pts.Add(new Vector2(x1 + R(-0.04f, 0.02f), y0));

            float wx = parent.position.x;
            var v = new List<Vector3>(); var uv = new List<Vector2>();
            var face = new List<int>(); var sides = new List<int>(); var tops = new List<int>(); var ink = new List<int>();
            // a triangle facing out along n, whatever order its corners came in
            void Tri(List<int> into, int a, int b, int c, Vector3 n)
            {
                if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), n) < 0) (b, c) = (c, b);
                into.Add(a); into.Add(b); into.Add(c);
            }
            // the face: a fan from the middle of the outline
            var mid = Vector2.zero;
            foreach (var p in pts) mid += p;
            mid /= pts.Count;
            int c0 = v.Count;
            v.Add(new Vector3(mid.x, mid.y, zFront)); uv.Add(new Vector2((wx + mid.x) / RockTile, mid.y / RockTile));
            foreach (var p in pts) { v.Add(new Vector3(p.x, p.y, zFront)); uv.Add(new Vector2((wx + p.x) / RockTile, p.y / RockTile)); }
            for (int i = 0; i < pts.Count - 1; i++) Tri(face, c0, c0 + 1 + i, c0 + 2 + i, Vector3.back);
            Tri(face, c0, c0 + pts.Count, c0 + 1, Vector3.back);
            // the sides and the top: each edge of the outline (but the buried bottom one) pushed back
            float run = 0;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1], d = b - a;
                var outward = new Vector3(-d.y, d.x, 0).normalized;
                bool top = outward.y > 0.7f;
                int k = v.Count;
                float len = d.magnitude;
                foreach (var (p, z, u) in new[] { (a, zFront, run), (b, zFront, run + len), (b, zBack, run + len), (a, zBack, run) })
                {
                    v.Add(new Vector3(p.x, p.y, z));
                    uv.Add(top ? new Vector2((wx + p.x) / RockTile, z / RockTile) : new Vector2(u / RockTile, z / RockTile));
                }
                run += len;
                var into = top ? tops : sides;
                Tri(into, k, k + 1, k + 2, outward);
                Tri(into, k, k + 2, k + 3, outward);
                // the ink: a thin dark band over the face's edge
                var off = (Vector3)(new Vector2(outward.x, outward.y) * (Ink / 2));
                int q = v.Count;
                foreach (var p in new[] { (Vector3)a - off, (Vector3)b - off, (Vector3)b + off, (Vector3)a + off })
                {
                    v.Add(new Vector3(p.x, p.y, zFront - 0.01f)); uv.Add(Vector2.zero);
                }
                Tri(ink, q, q + 1, q + 2, Vector3.back);
                Tri(ink, q, q + 2, q + 3, Vector3.back);
            }
            var mesh = new Mesh { name = "rock", subMeshCount = 4 };
            mesh.SetVertices(v); mesh.SetUVs(0, uv);
            mesh.SetTriangles(face, 0); mesh.SetTriangles(sides, 1); mesh.SetTriangles(tops, 2); mesh.SetTriangles(ink, 3);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();

            var tone = RockTone();
            Material Rock(Color tint)
            {
                var mat = RockMat(1, 1, 0, 0, tint);
                mat.mainTextureScale = Vector2.one; mat.mainTextureOffset = Vector2.zero;
                return mat;
            }
            var go = CbArt.Part(mesh, Rock(tone), parent, Vector3.zero, Vector3.one, shadows: false);
            go.name = "rock";
            go.GetComponent<MeshRenderer>().sharedMaterials = new[]
            {
                go.GetComponent<MeshRenderer>().sharedMaterial,
                Rock(new Color(tone.r * 0.74f, tone.g * 0.76f, tone.b * 0.84f)),
                Rock(new Color(Mathf.Min(1, tone.r * 1.15f), Mathf.Min(1, tone.g * 1.15f), Mathf.Min(1, tone.b * 1.12f))),
                CbArt.Toon("#3b3029"),
            };
        }

        /// <summary>A model placed and stretched so its bounds fill the box lo..hi of parent's space; null if missing.</summary>
        static Transform Fit(string path, Transform parent, Vector3 lo, Vector3 hi)
        {
            var holder = CbArt.Joint(parent, 0, 0, 0, "fit");
            var model = CbArt.Prop(path, holder, CbArt.PropOutline);
            if (model == null) { Object.Destroy(holder.gameObject); return null; }
            // the natural rocks' pale stone washed out to white in the game's light (the stage's pictures): toned down
            if (path == RiverStonePath || path == RiverRockPath || path == CrackedRockPath || path == CliffRockPath)
                foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial.color = NaturalRockTone;
            var b = LocalBounds(model, holder);
            var size = hi - lo;
            var s = new Vector3(size.x / Mathf.Max(0.01f, b.size.x), size.y / Mathf.Max(0.01f, b.size.y), size.z / Mathf.Max(0.01f, b.size.z));
            holder.localScale = s;
            holder.localPosition = lo - Vector3.Scale(b.min, s);
            return holder;
        }

        /// <summary>
        /// Like Fit, for a rock he stands on: drawn up so the top of its outline, seen from the front, runs on average at
        /// hi.y, where he stands. A rough rock's highest point is well above most of its top, and fitted by its highest
        /// point he walked in the air over it (the user).
        /// </summary>
        static Transform FitTop(string path, Transform parent, Vector3 lo, Vector3 hi)
        {
            var holder = Fit(path, parent, lo, hi);
            if (holder == null) return null;
            float k = TopShare(path, holder);
            if (k >= 0.999f) return holder;
            // stretched upwards from lo.y: the outline's average top (k of its height) lands at hi.y
            var s = holder.localScale;
            var p = holder.localPosition;
            float bottom = (lo.y - p.y) / s.y;
            s.y /= k;
            holder.localScale = s;
            holder.localPosition = new Vector3(p.x, lo.y - bottom * s.y, p.z);
            return holder;
        }

        static readonly Dictionary<string, float> TopShares = new Dictionary<string, float>();

        /// <summary>
        /// How far up its height a model's outline seen from the front runs on average (0..1): the highest point of
        /// its meshes in each of a row of narrow slices across it, averaged over the middle ones. 1 if its meshes can't
        /// be read.
        /// </summary>
        static float TopShare(string path, Transform holder)
        {
            if (TopShares.TryGetValue(path, out float k)) return k;
            k = 1f;
            const int Slices = 16;
            var pts = new List<Vector3>();
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var mf in holder.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                foreach (var v in mesh.vertices)
                {
                    var q = holder.InverseTransformPoint(mf.transform.TransformPoint(v));
                    pts.Add(q);
                    minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
                    minY = Mathf.Min(minY, q.y); maxY = Mathf.Max(maxY, q.y);
                }
            }
            if (pts.Count > 0 && maxX > minX && maxY > minY)
            {
                var top = new float[Slices];
                for (int i = 0; i < Slices; i++) top[i] = float.MinValue;
                foreach (var q in pts)
                {
                    int i = Mathf.Clamp((int)((q.x - minX) / (maxX - minX) * Slices), 0, Slices - 1);
                    top[i] = Mathf.Max(top[i], q.y);
                }
                // the middle slices (at its ends a rock falls away to its sides)
                float sum = 0;
                int n = 0;
                for (int i = 2; i < Slices - 2; i++)
                    if (top[i] > float.MinValue) { sum += top[i]; n++; }
                if (n > 0) k = Mathf.Clamp((sum / n - minY) / (maxY - minY), 0.6f, 1f);
            }
            TopShares[path] = k;
            return k;
        }

        /// <summary>The bounds of a model's meshes in the space of another transform (from the meshes, not the renderers).</summary>
        static Bounds LocalBounds(Transform model, Transform space)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var toSpace = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var mb = mesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = toSpace.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return any ? b : new Bounds(Vector3.zero, Vector3.one);
        }

        // ---------- the mountain ----------

        // natural rocks drawn in Canva and built in Meshy: a round river stone, a tall river rock, a cracked boulder and
        // a block of cliff
        static readonly Color NaturalRockTone = new Color(0.68f, 0.65f, 0.62f);
        const string RiverStonePath = "Cloudborne/Jungle/RiverStone/RiverStone", RiverRockPath = "Cloudborne/Jungle/RiverRock/RiverRock",
            CrackedRockPath = "Cloudborne/Jungle/CrackedRock/CrackedRock", CliffRockPath = "Cloudborne/Jungle/CliffRock/CliffRock";

        /// <summary>
        /// A terrace's rock (the painted boxes didn't look like real rock: the user): the block of cliff for a broad
        /// one, the tall river rock for a spire, side by side (a little overlapped, mirrored this way and that) when it
        /// is wide, each fitted to its share of the terrace's box. False if the models aren't in the project.
        /// </summary>
        bool RockPieces(Transform parent, float x0, float x1, float y0, float y1, float zFront, float zBack, int seed)
        {
            // as wide as tall: the cliff block about 0.85, the river rock 0.48
            const float CliffAspect = 0.85f, SpireAspect = 0.48f, Overlap = 0.15f;
            float w = x1 - x0, h = y1 - y0;
            int n = Mathf.Max(1, Mathf.RoundToInt(w / (h * CliffAspect)));
            float aspect = w / n / h;
            string model = Mathf.Abs(Mathf.Log(aspect / CliffAspect)) <= Mathf.Abs(Mathf.Log(aspect / SpireAspect)) ? CliffRockPath : RiverRockPath;
            var rock = CbArt.Joint(parent, 0, 0, 0, "rock");
            var rng = new System.Random(seed);
            for (int i = 0; i < n; i++)
            {
                float a = x0 + w * i / n - (i > 0 ? Overlap : 0), b = x0 + w * (i + 1) / n + (i < n - 1 ? Overlap : 0);
                var piece = CbArt.Joint(rock, 0, 0, 0, "piece");
                if (FitTop(model, piece, new Vector3(a, y0, zFront), new Vector3(b, y1, zBack)) == null)
                {
                    Object.Destroy(rock.gameObject);
                    return false;
                }
                // mirrored about its middle, so neighbours don't look alike
                if (rng.NextDouble() < 0.5)
                {
                    piece.localScale = new Vector3(-1, 1, 1);
                    piece.localPosition = new Vector3(a + b, 0, 0);
                }
            }
            return true;
        }

        /// <summary>A terrace of the mountain: solid stone (or earth) from the ground up to top, depth metres deep.</summary>
        void AddTerrace(float x0, float x1, float top, float depth)
        {
            var t = CbArt.Joint(root, (x0 + x1) / 2, 0, 0, "terrace");
            float cx = (x0 + x1) / 2;
            if (!RockPieces(t, x0 - cx, x1 - cx, -0.3f, top, -0.9f, -0.9f + depth, Mathf.RoundToInt(x0 * 10)))
                RockFormation(t, x0 - cx, x1 - cx, -0.3f, top, -0.9f, depth, Mathf.RoundToInt(x0 * 10));
            blocks.Add(new Block { x0 = x0, x1 = x1, top = top, t = t });
        }

        /// <summary>
        /// Earth like the stage's own ground: the painted cross-section of soil on its front and sides, grass on top,
        /// the painting lined up with the world so neighbouring pieces match.
        /// </summary>
        static void EarthSlab(Transform parent, float x0, float x1, float y0, float y1, float zFront, float zBack, Material face, Material top)
        {
            float w = x1 - x0, h = y1 - y0, d = zBack - zFront, cx = (x0 + x1) / 2;
            float wx = parent.position.x + cx;
            var front = CbArt.Part(CbMesh.Quad, FaceMat(face, w, h, wx - w / 2), parent, new Vector3(cx, (y0 + y1) / 2, zFront), new Vector3(w, h, 1), shadows: false);
            front.name = "face";
            var side = FaceMat(face, d, h, 0);
            CbArt.Part(CbMesh.Quad, side, parent, new Vector3(x0, (y0 + y1) / 2, zFront + d / 2), new Vector3(d, h, 1), Quaternion.Euler(0, 90, 0), shadows: false);
            CbArt.Part(CbMesh.Quad, side, parent, new Vector3(x1, (y0 + y1) / 2, zFront + d / 2), new Vector3(d, h, 1), Quaternion.Euler(0, -90, 0), shadows: false);
            var lid = new Material(top) { mainTextureScale = new Vector2(w / CbWorld.GrassTile, d / CbWorld.GrassTile), mainTextureOffset = new Vector2(wx / CbWorld.GrassTile, 0) };
            CbArt.Part(CbMesh.Quad, lid, parent, new Vector3(cx, y1, zFront + d / 2), new Vector3(w, d, 1), CbArt.Rot(Mathf.PI / 2), shadows: false);
        }

        /// <summary>The soil material for a face w by h: the painting's grassy top row at the top edge, then earth.</summary>
        static Material FaceMat(Material face, float w, float h, float worldX)
        {
            var m = new Material(face);
            float tileW = CbWorld.FaceTileWidth(face.mainTexture);
            float rows = h / CbWorld.FaceHeight;
            m.mainTextureScale = new Vector2(w / tileW, rows);
            m.mainTextureOffset = new Vector2(worldX / tileW, 1 - rows);
            return m;
        }

        static Material EarthMat() => CbWorld.FaceMaterial();
        static Material GrassMat() => CbWorld.GrassMaterial();

        // ---------- blocks and ledges ----------

        /// <summary>Puts everything back: the wall whole, the ledges in place, the stones up.</summary>
        public void Reset()
        {
            climbTold = false;
            ResetWays();
            ResetGorge();
            foreach (var b in blocks)
            {
                if (!b.breakable) continue;
                b.broken = false;
                b.hp = 2;
                b.t.gameObject.SetActive(true);
                b.t.localRotation = Quaternion.identity;
            }
            foreach (var c in crossings) { c.dip = c.bob = c.dx = c.sunk = c.shake = c.stood = 0; c.phase = 0; }
            foreach (var l in ledges)
            {
                l.gone = false;
                l.stood = l.respawn = 0;
                l.t.localPosition = l.home;
                l.t.gameObject.SetActive(true);
            }
        }

        void AddBlock(float x, float width, float height, bool breakable)
        {
            // the cracked wall: a great cracked boulder (it was a shrine pillar with black stripes, and looked fragile:
            // the user), a little wider than where it stops him at its widest; else a shrine pillar stretched to the size
            var t = CbArt.Joint(root, x, 0, 0.1f, breakable ? "crackedWall" : "pillar");
            if (breakable && Fit(CrackedRockPath, t, new Vector3(-width / 2 - 0.1f, 0, -0.75f), new Vector3(width / 2 + 0.1f, height, 0.75f)) != null)
            {
                blocks.Add(new Block { x0 = x - width / 2, x1 = x + width / 2, top = height, breakable = true, hp = 2, t = t });
                return;
            }
            var holder = CbArt.Joint(t, 0, 0, 0, "shape");
            holder.localScale = new Vector3(width / 1.48f, height / 3.6f, 0.9f);
            if (CbArt.Prop("Cloudborne/Jungle/Pillar/Pillar", holder, CbArt.PropOutline) == null)
                CbArt.Box(CbArt.Toon("#8f9a8a", CbArt.PropOutline), holder, new Vector3(0, 1.8f, 0), new Vector3(1.48f, 3.6f, 1.5f));
            if (breakable)
            {
                // dark cracks down its face: a hint that something heavy will break it
                var crack = CbArt.Toon("#2b2622");
                for (int k = 0; k < 5; k++)
                    CbArt.Box(crack, t, new Vector3(Mathf.Sin(k * 2.1f) * 0.25f, 0.6f + k * 0.8f, -0.62f), new Vector3(0.06f, 0.7f, 0.03f), CbArt.Rot(0, 0, (k % 2 == 0 ? 0.5f : -0.5f)));
            }
            blocks.Add(new Block { x0 = x - width / 2, x1 = x + width / 2, top = height, breakable = breakable, hp = 2, t = t });
        }

        void AddLedge(float x, float width, float top, bool crumbles)
        {
            // a floating slab of mossy stone (the kit's ledge, its underside rough, vines hanging) - a crumbling one
            // cracked; else a slab of earth with grass on top
            var t = CbArt.Joint(root, x, 0, 0.1f, crumbles ? "crumblingLedge" : "ledge");
            earth ??= EarthMat();
            grass ??= GrassMat();
            var kit = Fit(CliffDir + (crumbles ? "CrumblingLedge/CrumblingLedge" : "Ledge/Ledge"), t,
                new Vector3(-width / 2 - 0.08f, top - Mathf.Max(0.34f, width * 0.27f), -0.7f), new Vector3(width / 2 + 0.08f, top + 0.04f, 0.9f));
            if (kit == null) EarthSlab(t, -width / 2, width / 2, top - 0.55f, top, -0.75f, 0.9f, crumbles ? (crackedEarth ?? earth) : earth, grass);
            if (crumbles && kit == null)
            {
                var crack = CbArt.Toon("#2b2622");
                for (int k = 0; k < 3; k++)
                    CbArt.Box(crack, t, new Vector3((k - 1) * width * 0.3f, top - 0.28f, -0.77f), new Vector3(0.05f, 0.4f, 0.02f), CbArt.Rot(0, 0, k % 2 == 0 ? 0.4f : -0.4f));
            }
            ledges.Add(new Ledge { x0 = x - width / 2, x1 = x + width / 2, top = top, crumbles = crumbles, t = t, home = t.localPosition });
        }

        /// <summary>A model in a joint of its own (so its import rotation stays), sized, turned about Y, on the level.</summary>
        static Material bridgeMat;

        /// <summary>One piece of the rope bridge kit, with the bridge's own texture; null if it isn't in the project.</summary>
        static Transform BridgePart(string name, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Cloudborne/Jungle/RopeBridge/" + name);
            if (prefab == null) return null;
            var t = Object.Instantiate(prefab, parent, false).transform;
            t.name = name;
            bridgeMat ??= CbArt.AnimeToon(Resources.Load<Texture2D>("Cloudborne/Jungle/RopeBridge/RopeBridge_Albedo"), CbArt.PropOutline);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = bridgeMat;
                r.sharedMaterials = mats;
            }
            return t;
        }

        /// <summary>
        /// Strings the rope bridge across c from the kit: an end with its posts at each side, then sections of planks
        /// along the deck's sag, each tilted to it; the sections in RopeHoles get no planks, and become c's holes. The
        /// ropes are one smooth curve each over the whole bridge (StringRopes). Without the kit, the one-piece bridge
        /// model, stretched.
        /// </summary>
        Transform StringRopeBridge(Crossing c)
        {
            var t = CbArt.Joint(root, 0, 0, 0.15f, "ropeBridge");
            var probeDeck = BridgePart("BridgeDeck", t);
            if (probeDeck == null)
            {
                Object.Destroy(t.gameObject);
                var whole = PlaceProp("Cloudborne/Jungle/RopeBridge/RopeBridge", new Vector3(c.cx, -0.25f, 0.15f), new Vector3(2 * c.half / 6f, 1, 1), 0);
                return whole;
            }
            // the kit's sizes, measured: a section's length and which way it runs, and its deck's top
            var db = LocalBounds(probeDeck, t);
            float secLen = db.size.x, deckTop = db.max.y;
            bool runsLeft = db.center.x < 0;
            Object.Destroy(probeDeck.gameObject);
            float a = c.X0 + RopeEndIn, b = c.X1 - RopeEndIn, w = (b - a) / RopeSections;
            c.holes = new List<(float, float)>();
            for (int k = 0; k < RopeSections; k++)
            {
                float x0 = a + k * w, x1 = x0 + w;
                float y0 = c.Top(x0) - deckTop, y1 = c.Top(x1) - deckTop;
                var sec = CbArt.Joint(t, x0, y0, 0, "section");
                sec.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(y1 - y0, w) * Mathf.Rad2Deg);
                // the section's start at the joint, along +x, stretched a little to fit
                var fit = CbArt.Joint(sec, 0, 0, 0, "fit");
                fit.localScale = new Vector3((runsLeft ? -1 : 1) * w / secLen, 1, 1);
                fit.localPosition = new Vector3(runsLeft ? 0 : -(db.min.x) * w / secLen, 0, 0);
                if (System.Array.IndexOf(RopeHoles, k) >= 0) c.holes.Add((x0, x1));
                else BridgePart("BridgeDeck", fit);
            }
            // the ends: their posts out on the banks, their planks meeting the first and last sections
            foreach (var (edge, outward) in new[] { (a, -1f), (b, 1f) })
            {
                var end = CbArt.Joint(t, edge, 0, 0, "end");
                var part = BridgePart("BridgeEnd", end);
                if (part == null) continue;
                var eb = LocalBounds(part, end);
                // turned so it reaches outward from the edge
                bool reachesOut = (eb.center.x - 0) * outward > 0;
                if (!reachesOut) end.localScale = new Vector3(-1, 1, 1);
                eb = LocalBounds(part, t);
                float inner = outward < 0 ? eb.max.x : eb.min.x;
                end.localPosition = new Vector3(edge - (inner - edge), c.Top(edge) - EndDeckTop, 0);
            }
            StringRopes(c, t, a, b);
            return t;
        }

        // the end piece's planks where it meets the sections, above its foot
        const float EndDeckTop = 0.31f;
        // the ropes: the handrail as high over the planks as the end piece's rope where it is cut (0.87 over its foot),
        // either side of the planks, a tie between the handrail and the planks' edge rope every TieEvery metres
        const float RailOver = 0.87f - EndDeckTop, RopeSide = 0.6f, TieEvery = 0.6f;

        /// <summary>
        /// The bridge's ropes from end to end, each one smooth curve following the planks' sag: a handrail and a rope
        /// along the planks' edge either side, and ties between them, on over the stretches without planks. The kit's
        /// rail pieces, chained, kinked and stepped at every joint (the user saw broken ropes).
        /// </summary>
        void StringRopes(Crossing c, Transform t, float a, float b)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int n = Mathf.CeilToInt((b - a) / 0.25f);
            foreach (float z in new[] { -RopeSide, RopeSide })
            {
                var rail = new List<Vector3>();
                var edge = new List<Vector3>();
                for (int i = 0; i <= n; i++)
                {
                    float x = Mathf.Lerp(a, b, i / (float)n);
                    rail.Add(new Vector3(x, c.Top(x) + RailOver, z));
                    edge.Add(new Vector3(x, c.Top(x) - 0.03f, z));
                }
                Rope(verts, tris, rail, 0.028f);
                Rope(verts, tris, edge, 0.022f);
                for (float x = a + TieEvery / 2; x < b; x += TieEvery)
                    Rope(verts, tris, new[] { new Vector3(x, c.Top(x) - 0.03f, z), new Vector3(x, c.Top(x) + RailOver, z) }, 0.013f);
            }
            var mesh = new Mesh { name = "bridgeRopes" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("ropes");
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var rope = CbArt.NewToon(CbArt.Hex("#e6cfa0"), 0.6f);
            rope.SetFloat("_Cull", 0f);
            rope.SetFloat("_SpecStrength", 0f);
            go.AddComponent<MeshRenderer>().sharedMaterial = rope;
        }

        /// <summary>A rope (a thin tube of a few sides) through the points, added to a mesh being built.</summary>
        static void Rope(List<Vector3> verts, List<int> tris, IList<Vector3> pts, float radius, int sides = 5)
        {
            int start = verts.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                var dir = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                var side = Vector3.Cross(dir, Vector3.forward);
                if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(dir, Vector3.right);
                side.Normalize();
                var up = Vector3.Cross(side, dir);
                for (int k = 0; k < sides; k++)
                {
                    float ang = k * Mathf.PI * 2 / sides;
                    verts.Add(pts[i] + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radius);
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a0 = start + i * sides + k, a1 = start + i * sides + (k + 1) % sides;
                    tris.Add(a0); tris.Add(a0 + sides); tris.Add(a1);
                    tris.Add(a1); tris.Add(a0 + sides); tris.Add(a1 + sides);
                }
        }

        Transform PlaceProp(string path, Vector3 at, Vector3 scale, float yaw)
        {
            var t = CbArt.Joint(root, at.x, at.y, at.z, "prop");
            if (CbArt.Prop(path, t, CbArt.PropOutline) == null) { Object.Destroy(t.gameObject); return null; }
            t.localScale = scale;
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            return t;
        }

        // ---------- what the hero's physics asks ----------

        /// <summary>
        /// The highest surface under x that a body at height fromY can stand on: the path (0, nothing over the water),
        /// a block's top, a ledge's top (only one it was already above: ledges are jumped up through from below), a
        /// bridge's deck (stepped onto from the bank) or a stone or log (landed on from above).
        /// </summary>
        public float SupportAt(float x, float fromY)
        {
            float best = InWater(x) ? Deep : 0;
            foreach (var c in crossings)
            {
                if (x <= c.X0 - HalfWidth * 0.5f || x >= c.X1 + HalfWidth * 0.5f || c.HoleAt(x)) continue;
                float top = c.Top(x);
                if (top <= fromY + (c.deck ? StepUp : 0.05f) && top > best) best = top;
            }
            foreach (var b in blocks)
                if (!b.broken && x > b.x0 - HalfWidth * 0.5f && x < b.x1 + HalfWidth * 0.5f && b.top <= fromY + 0.05f && b.top > best) best = b.top;
            foreach (var l in ledges)
                if (!l.gone && x > l.x0 - HalfWidth * 0.5f && x < l.x1 + HalfWidth * 0.5f && l.top <= fromY + 0.05f && l.top > best) best = l.top;
            WaysSupport(x, fromY, ref best);
            return best;
        }

        /// <summary>
        /// Whether standing at (x, y) is a safe place to come back to after falling into the water: anywhere away from
        /// the water, a bridge, a steady stepping stone, the rock or the stone bridge - not a sinking stone or a log.
        /// </summary>
        public bool SafeAt(float x, float y)
        {
            if (!WaysSafe(x, y)) return false;
            if (!InWater(x)) return true;
            foreach (var b in blocks)
                if (!b.broken && x > b.x0 - HalfWidth * 0.5f && x < b.x1 + HalfWidth * 0.5f && Mathf.Abs(y - b.top) < 0.05f) return true;
            foreach (var c in crossings)
                if (x > c.X0 - HalfWidth * 0.5f && x < c.X1 + HalfWidth * 0.5f && Mathf.Abs(y - c.Top(x)) < 0.05f) return c.Safe;
            return false;
        }

        /// <summary>Where the middle of the stone, log or bridge nearest x is now (a drifting log moves).</summary>
        public float CrossingAt(float x)
        {
            float best = x, dist = float.MaxValue;
            foreach (var c in crossings)
                if (Mathf.Abs(c.cx - x) < dist) { dist = Mathf.Abs(c.cx - x); best = c.cx + c.dx; }
            return best;
        }

        /// <summary>
        /// An edge Raja can catch, at x and y going towards dir: the top corner of a terrace or rock, or the end of a
        /// ledge, within reach of his raised hands. Not the cracked wall (it is there to be broken), nothing standing in
        /// the water (the river is crossed on its stones) and no corner up against something higher.
        /// </summary>
        public bool FindEdge(float x, float y, int dir, out float edgeX, out float top)
        {
            float bx = 0, bt = 0, best = float.MaxValue;
            void Try(float ex, float tp)
            {
                float ahead = (ex - x) * dir, above = tp - y;
                if (ahead < GrabNear || ahead > GrabFar || above < GrabLow || above > GrabHigh) return;
                if (InWater(ex, 0.4f) || Covered(ex + dir * 0.25f, tp)) return;
                float d = Mathf.Abs(above - CbHero.HangReach);
                if (d < best) { best = d; bx = ex; bt = tp; }
            }
            foreach (var b in blocks)
                if (!b.broken && !b.breakable && !b.smooth) Try(dir > 0 ? b.x0 : b.x1, b.top);
            foreach (var l in ledges)
                if (!l.gone) Try(dir > 0 ? l.x0 : l.x1, l.top);
            edgeX = bx; top = bt;
            return best < float.MaxValue;
        }

        /// <summary>Whether something solid stands over (x, above top): a higher block there.</summary>
        bool Covered(float x, float top)
        {
            foreach (var b in blocks)
                if (!b.broken && x > b.x0 && x < b.x1 && b.top > top + 0.1f && b.bottom < top + CbLevel.StandHeight) return true;
            return false;
        }

        /// <summary>
        /// Whether Raja at (x, y) is up against a wall on his dir side that he can slide down and jump off: a block's
        /// side, with more of it above his feet than a step. Not the cracked wall (it is there to be broken).
        /// </summary>
        public bool WallAt(float x, float y, int dir)
        {
            foreach (var b in blocks)
            {
                if (b.broken || b.breakable || b.smooth || y > b.top - 0.6f || y + 1f < b.bottom) continue;
                float side = dir > 0 ? b.x0 - HalfWidth : b.x1 + HalfWidth;
                if (Mathf.Abs(x - side) < 0.06f) return true;
            }
            return false;
        }

        /// <summary>Whether the edge he hangs from at (edgeX, top) is still there: a crumbling ledge may have dropped.</summary>
        public bool EdgeHolds(float edgeX, float top)
        {
            foreach (var b in blocks)
                if (!b.broken && Mathf.Abs(b.top - top) < 0.02f && edgeX >= b.x0 - 0.01f && edgeX <= b.x1 + 0.01f) return true;
            foreach (var l in ledges)
                if (!l.gone && Mathf.Abs(l.top - top) < 0.02f && edgeX >= l.x0 - 0.01f && edgeX <= l.x1 + 0.01f) return true;
            return false;
        }

        /// <summary>
        /// Moving from x0 to x1 at height y: solid blocks stop him at their side, and a low passage does when he is
        /// taller (height) than the gap under it.
        /// </summary>
        public float BlockX(float x0, float x1, float y, float height = StandHeight)
        {
            foreach (var o in overhangs)
            {
                if (y + height <= o.bottom || y >= o.top - 0.05f) continue;
                if (x0 <= o.x0 - HalfWidth + 0.01f && x1 > o.x0 - HalfWidth) x1 = o.x0 - HalfWidth;
                else if (x0 >= o.x1 + HalfWidth - 0.01f && x1 < o.x1 + HalfWidth) x1 = o.x1 + HalfWidth;
            }
            foreach (var b in blocks)
            {
                if (b.broken || y >= b.top - 0.05f || y + height <= b.bottom) continue;
                if (x0 <= b.x0 - HalfWidth + 0.01f && x1 > b.x0 - HalfWidth) x1 = b.x0 - HalfWidth;
                else if (x0 >= b.x1 + HalfWidth - 0.01f && x1 < b.x1 + HalfWidth) x1 = b.x1 + HalfWidth;
            }
            return x1;
        }

        /// <summary>
        /// A blow from Raja at x towards dir. The cracked wall only gives way to the staff; paws and kicks clang
        /// off it with a hint. True if a block was struck.
        /// </summary>
        public bool Strike(float x, float y, int dir, float range, bool staff)
        {
            if (StrikeWays(x, y, dir, range, staff)) return true;
            foreach (var b in blocks)
            {
                if (!b.breakable || b.broken) continue;
                float near = dir > 0 ? b.x0 - x : x - b.x1;
                if (near < -0.6f || near > range || y > b.top) continue;
                var at = new Vector3(dir > 0 ? b.x0 : b.x1, y + 1.1f, -0.6f);
                if (!staff)
                {
                    CbFx.Spark(at, CbFx.Grey, 6, 4, 0.3f, 0.25f, 4);
                    CbFx.Sfx("block", 0.7f);
                    g.hud.SetTip("Cracked stone: break it with the staff (<color=#ffb03a>HEAVY</color>)");
                    return true;
                }
                b.hp--;
                CbFx.Spark(at, CbFx.Dust, 14, 5, 0.5f, 0.5f, 6);
                g.shake = Mathf.Max(g.shake, 0.3f);
                CbFx.Sfx("heavy", 0.8f);
                if (b.hp > 0) { b.t.localRotation = Quaternion.Euler(0, 0, dir * 2f); return true; }
                // it bursts into tumbling stones
                b.broken = true;
                b.t.gameObject.SetActive(false);
                // it bursts into tumbling rocks and a cloud of dust: the jungle's mossy boulder, a few big chunks that
                // fall heavily and more small ones thrown further (grey blobs flew off it like foam: the user)
                Material stone = null;
                for (int k = 0; k < 11; k++)
                {
                    bool big = k < 4;
                    float s = big ? Random.Range(0.36f, 0.52f) : Random.Range(0.14f, 0.28f);
                    var chunk = CbArt.Joint(g.transform, (b.x0 + b.x1) / 2 + Random.Range(-0.4f, 0.4f), Random.Range(0.4f, b.top - 0.3f), Random.Range(-0.3f, 0.3f), "rubble");
                    var rock = CbArt.Prop("Cloudborne/Jungle/Boulder/Boulder", chunk, CbArt.PropOutline);
                    // the boulder stands on its base; its middle at the chunk's, so it tumbles about it
                    if (rock != null) rock.localPosition = new Vector3(0, -0.6f, 0);
                    else CbArt.Part(CbMesh.Blob(6, 4), stone ??= CbArt.Toon("#8f9a8a", CbArt.PropOutline), chunk, Vector3.zero, Vector3.one * 1.2f, shadows: false);
                    chunk.localScale = new Vector3(s, s * Random.Range(0.7f, 1f), s);
                    chunk.rotation = Random.rotation;
                    var v = big ? new Vector3(dir * Random.Range(1.2f, 3.2f), Random.Range(1.5f, 3.5f), Random.Range(-0.6f, 0.6f))
                        : new Vector3(dir * Random.Range(3f, 7f), Random.Range(3f, 6.5f), Random.Range(-1f, 1f));
                    g.AddDebris(chunk, v, Random.Range(-300f, 300f) * (big ? 0.5f : 1f));
                }
                for (int k = 0; k < 4; k++)
                    CbFx.Puff(new Vector3((b.x0 + b.x1) / 2, b.top * (0.15f + k * 0.25f), -0.4f), new Color(0.78f, 0.74f, 0.66f), 7, 2.2f, 1.3f, 0.8f);
                CbVfx.Burst(at, CbFx.Hit, 3f, 0.25f);
                g.hud.ImpactFlash(0.3f);
                g.hud.SetTip("The way is open!");
                return true;
            }
            return false;
        }

        // ---------- moving parts ----------

        /// <summary>
        /// Crumbling ledges drop when stood on and come back; logs bob, dip
        /// under him and drift, carrying him; sinking stones go under; the rope bridge bounces.
        /// </summary>
        public void Update(float dt, CbHero hero)
        {
            UpdateWays(dt, hero);
            // at the foot of the mountain, how to climb it
            if (!climbTold && hero.x > MountainFrom - 5f && hero.x < MountainFrom)
            {
                climbTold = true;
                g.hud.SetTip("A low gap under the cliff: run and press <color=#ffb03a>↓</color> to slide through, or hold <color=#ffb03a>↓</color> to crawl");
            }
            UpdateCrossings(dt, hero);
            UpdateGorge(dt, hero);
            foreach (var l in ledges)
            {
                if (!l.crumbles) continue;
                if (l.gone)
                {
                    l.respawn -= dt;
                    var p = l.t.localPosition;
                    p.y -= dt * 9;
                    l.t.localPosition = p;
                    if (p.y < -3) l.t.gameObject.SetActive(false);
                    if (l.respawn <= 0)
                    {
                        l.gone = false;
                        l.stood = 0;
                        l.t.localPosition = l.home;
                        l.t.gameObject.SetActive(true);
                    }
                    continue;
                }
                // standing on it, or hanging from its end, his weight is on it
                bool on = Mathf.Abs(hero.y - l.top) < 0.02f && hero.x > l.x0 - HalfWidth * 0.5f && hero.x < l.x1 + HalfWidth * 0.5f
                    || hero.Hanging && Mathf.Abs(hero.GrabTop - l.top) < 0.02f && hero.GrabX >= l.x0 - 0.01f && hero.GrabX <= l.x1 + 0.01f;
                l.stood = on ? l.stood + dt : Mathf.Max(0, l.stood - dt);
                // shaking more and more, then it drops
                float shake = l.stood > 0 ? Mathf.Sin(Time.time * 60) * 0.04f * (l.stood / CrumbleAfter) : 0;
                l.t.localPosition = l.home + new Vector3(shake, 0, 0);
                if (l.stood >= CrumbleAfter)
                {
                    l.gone = true;
                    l.respawn = CrumbleBack;
                    CbFx.Spark(new Vector3((l.x0 + l.x1) / 2, l.top, -0.4f), CbFx.Dust, 12, 3, 0.5f, 0.5f, 6);
                    CbFx.Sfx("hit", 0.5f);
                }
            }
        }

        void UpdateCrossings(float dt, CbHero hero)
        {
            foreach (var c in crossings)
            {
                bool on = hero.x > c.X0 - HalfWidth * 0.5f && hero.x < c.X1 + HalfWidth * 0.5f && Mathf.Abs(hero.y - c.Top(hero.x)) < 0.04f;
                if (c.deck)
                {
                    // the rope bridge bounces a touch under his steps
                    c.bob = on ? Mathf.Sin(Time.time * 9f) * 0.015f : 0;
                    if (c.t != null) c.t.localPosition = c.home + new Vector3(0, c.bob, 0);
                    continue;
                }
                if (c.drift > 0)
                {
                    // to and fro on the current, carrying him with it
                    float to = c.drift * Mathf.Sin(Time.time * Mathf.PI * 2 / c.period + c.cx);
                    if (on) hero.x += to - c.dx;
                    c.dx = to;
                }
                if (c.bobs)
                {
                    c.bob = Mathf.Sin(Time.time * 1.8f + c.cx) * 0.035f;
                    c.dip = Mathf.MoveTowards(c.dip, on ? 0.1f : 0f, dt * 0.5f);
                }
                if (c.sinks) Sink(c, dt, on);
                if (c.t != null)
                {
                    c.t.localPosition = c.home + new Vector3(c.dx + c.shake, c.bob - c.dip, 0);
                    if (c.bobs) c.t.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + c.cx) * 4f, 0, 0);
                }
            }
        }

        /// <summary>A sinking stone: stood on for a moment it shakes, goes under, and comes back up a while later.</summary>
        void Sink(Crossing c, float dt, bool on)
        {
            const float Warn = 0.7f;
            c.shake = 0;
            switch (c.phase)
            {
                case 0:
                    c.stood = on ? c.stood + dt : Mathf.Max(0, c.stood - dt * 0.5f);
                    c.shake = c.stood > 0 ? Mathf.Sin(Time.time * 55) * 0.035f * (c.stood / Warn) : 0;
                    if (c.stood >= Warn) { c.phase = 1; CbFx.Sfx("splash", 0.4f); }
                    break;
                case 1:
                    c.sunk = Mathf.MoveTowards(c.sunk, 1, dt * 2.5f);
                    if (Random.value < 0.5f) CbFx.Spark(new Vector3(c.cx + Random.Range(-0.5f, 0.5f), WaterLevel + 0.05f, -0.3f), CbFx.Water, 1, 2, 0.3f, 0.4f, 6);
                    if (c.sunk >= 1) c.phase = 2;
                    break;
                default:
                    // once under it stays under (it came back up: the user); the river is crossed without it
                    break;
            }
            c.dip = c.sunk * 0.95f;
        }

    }
}
