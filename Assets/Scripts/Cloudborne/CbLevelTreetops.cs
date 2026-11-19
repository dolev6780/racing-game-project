using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Stage 1-2, The Treetops (the story doc: the chase among the branches to the loggers' camp, about 350 m; vines,
    /// thorn walls the staff cuts, swinging logs, saw drones; the staff-throw scroll in a hidden nest; Tusk at the
    /// camp). He runs along great boughs high in the canopy; between them the gaps drop away (TreetopsDrops: falling
    /// in, he loses health and is back on the last bough he stood on). Built from the stage's own parts and 1-1's:
    /// <list type="bullet">
    /// <item>Into the treetops (0-60): a short rope bridge, thorns.</item>
    /// <item>The vine gap (60-90): swinging vines across a wide gap.</item>
    /// <item>The saw drones (90-118): a drone flying to and fro at head height - slide under, jump over or smash it.</item>
    /// <item>Crumbling branches (118-148): ledges across a gap, some cracked, a drone rising and falling between them.</item>
    /// <item>The swinging logs (148-180).</item>
    /// <item>Hand over hand (180-212): bars over a gap; an updraft up to a hidden nest with the staff-throw scroll.</item>
    /// <item>The big bough (212-250): an ambush.</item>
    /// <item>The long bridge (250-288): a long rope bridge, drones flying over it.</item>
    /// <item>Thorny boughs (288-320): thorns, a springy leaf up to a ledge.</item>
    /// <item>The loggers' camp (320-345): the last soldiers.</item>
    /// <item>Tusk's clearing (345-372): the armoured boar, between two great trunks (CbEnemyTusk.cs).</item>
    /// <item>Koda's cage (374.5): reached after Tusk, the scene of the empty cage (CbStory.EmptyCage).</item>
    /// </list>
    /// </summary>
    public partial class CbLevel
    {
        public const float TreetopsMax = 380f, TreetopsGoal = 372f;
        /// <summary>The middle of Tusk's arena; the trunks he rams stand this far either side of it.</summary>
        public const float TuskX = 358f, TuskTreeOff = CbEnemy.TuskCrash + 2.4f;
        /// <summary>Koda's cage at the end of the camp (empty: the end of stage 1-2).</summary>
        public const float KodaCageX = 374.5f;
        static readonly (float x0, float x1)[] TreetopsDrops = { (24f, 31f), (62f, 84f), (122f, 146f), (182f, 210f), (252f, 286f) };
        /// <summary>The hidden nest high over the gap with the bars: its ledge's middle and top (the staff-throw scroll).</summary>
        public const float NestX = 202.5f, NestTop = 9.5f;
        /// <summary>The ambush on the big bough, and the long rope bridge's middle and half length.</summary>
        public const float BoughAmbushX = 230f, LongBridgeX = 269f, LongBridgeHalf = 17.5f;

        void BuildTreetops()
        {
            foreach (var x in new[] { 60f, 118f, 178f, 248f, 318f, 341f }) AddLantern(x);
            AddEndGate(CbWorld.LevelMin - HalfWidth - 0.15f - EndGateHalf, 1);
            AddEndGate(CbWorld.LevelMax + HalfWidth + 0.15f + EndGateHalf, -1);

            // ---- into the treetops: a short rope bridge over the first gap, thorns on the bough after it
            AddRopeBridge(27.5f, 4.5f);
            AddThorns(46f);

            // ---- the vine gap
            AddVine(66f, 9f, 5f);
            AddVine(72.5f, 9f, 5f);
            AddVine(79f, 9f, 5f);

            // ---- the saw drones: one flying to and fro at head height along the bough
            AddSaw(new Vector2(96f, 1.35f), new Vector2(110f, 1.35f), 5f, 0f);

            // ---- crumbling branches across a gap, a drone rising and falling between them
            AddLedge(124.6f, 1.6f, 1.2f, false);
            AddLedge(128.6f, 1.4f, 2.2f, true);
            AddLedge(132.6f, 1.6f, 2.8f, false);
            AddLedge(136.6f, 1.4f, 2.2f, true);
            AddLedge(140.6f, 1.6f, 1.6f, false);
            AddLedge(144.2f, 1.2f, 0.8f, false);
            AddSaw(new Vector2(134.6f, 2.2f), new Vector2(134.6f, 5.6f), 3.2f, 0.25f);
            // over them, a high ledge with a tiger token (a double jump up from the ledge below)
            AddLedge(132.2f, 1.2f, 5.4f, false);

            // ---- the swinging logs
            AddPendulum(158f, 6.2f, 4.6f, 2.4f, 0f);
            AddPendulum(166f, 6.2f, 4.6f, 2.4f, 1.2f);

            // ---- hand over hand: bars over the gap, a ledge after them, and from it an updraft up to the hidden nest
            AddBar(184f, 194f, 4.4f);
            AddLedge(196f, 1.6f, 2.2f, false);
            AddDraft(199.2f, 200.6f, 0f, NestTop + 1f);
            AddLedge(NestX, 1.8f, NestTop, false);
            AddLedge(204.4f, 1.4f, 2.4f, false);
            AddLedge(207.8f, 1.4f, 1.2f, false);

            // ---- the big bough (the ambush: CbGame), thorns after it
            AddThorns(244f);

            // ---- the long rope bridge, drones flying over it
            AddRopeBridge(LongBridgeX, LongBridgeHalf);
            AddSaw(new Vector2(257f, 1.4f), new Vector2(268f, 1.4f), 4.2f, 0f);
            AddSaw(new Vector2(270f, 1.6f), new Vector2(281f, 1.6f), 4.2f, 0.5f);

            // ---- thorny boughs: thorns, a springy leaf up to a ledge
            AddThorns(296f);
            AddLeaf(304f, 0.45f);
            AddLedge(306.6f, 1.6f, 6.2f, false);

            // ---- the big bough's firefly hollow; the golden fruit out of reach over the thorny boughs
            AddFireflyHollow();
            AddHighFruitVine();

            // ---- Tusk's clearing: a great trunk at either end of his arena
            AddTuskTrees();

            // ---- the end of the camp: Koda's cage
            AddKodaCage();
        }

        // ---------- Koda's cage ----------

        // Koda's cage (the story doc: after Tusk, Raja breaks the cage, but it is empty; on the floor lies a scroll with
        // the prophecy and the seal of the Iron Crown): the friends' cage from stage 1-1, bigger, under a tarp lashed
        // down with rope (so it is only seen to be empty when it breaks), padlocked, the scroll on its floor.
        // CbStory.EmptyCage breaks it and lifts the scroll up to him.
        Transform kodaCage, scroll, scrollPaper;
        readonly Transform[] scrollRods = new Transform[2];
        bool kodaCageBroken;
        static readonly Vector3 ScrollRest = new Vector3(KodaCageX + 0.15f, 0.07f, 0.25f);
        /// <summary>Koda's cage is broken open (test hook), and the prophecy scroll found in it.</summary>
        public bool KodaCageBroken => kodaCageBroken;
        public Transform ProphecyScroll => scroll;

        void AddKodaCage()
        {
            kodaCage = CbArt.Joint(root, KodaCageX, 0, 0.3f, "kodaCage");
            if (Model("Cage", kodaCage, new Vector3(1.9f, 1.75f, 1.85f), 180f) == null)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * Mathf.PI * 2;
                    CbArt.Cylinder(Bamboo, kodaCage, new Vector3(Mathf.Cos(a) * 0.7f, 0.88f, Mathf.Sin(a) * 0.7f), 0.06f, 1.75f);
                }
                CbArt.Cylinder(Wood, kodaCage, new Vector3(0, 1.8f, 0), 0.82f, 0.14f);
                CbArt.Cylinder(Wood, kodaCage, new Vector3(0, 0.05f, 0), 0.82f, 0.1f);
            }
            // the tarp thrown over it: a rounded top, the cloth hanging down and flaring at its hem a little above the
            // ground (the bars' feet showing under it), roped down; the Iron Crown's red crown painted on it, an iron
            // padlock on the bars below (one block read as a cardboard box, and folds drawn on it as a crate's slats)
            var cloth = TarpCloth;
            CbArt.Box(cloth, kodaCage, new Vector3(0, 1.12f, 0), new Vector3(1.98f, 1.46f, 1.93f));
            CbArt.Cylinder(cloth, kodaCage, new Vector3(0, 1.84f, 0), 0.96f, 0.2f);
            CbArt.Part(CbMesh.Sphere, cloth, kodaCage, new Vector3(0, 1.9f, 0), new Vector3(1.96f, 0.42f, 1.9f));
            CbArt.Box(cloth, kodaCage, new Vector3(0, 0.43f, 0), new Vector3(2.1f, 0.18f, 2.05f));
            var rope = M("#6e5430");
            foreach (var rx in new[] { -0.6f, 0.6f })
                CbArt.Box(rope, kodaCage, new Vector3(rx, 1.15f, 0), new Vector3(0.07f, 1.75f, 2.02f));
            CbArt.Box(rope, kodaCage, new Vector3(0, 0.62f, 0), new Vector3(2.04f, 0.07f, 2.0f));
            var crown = M("#b3261e");
            CbArt.Box(crown, kodaCage, new Vector3(0, 1.22f, -1.0f), new Vector3(0.42f, 0.1f, 0.02f));
            for (int i = -1; i <= 1; i++)
                CbArt.Cone(crown, kodaCage, new Vector3(i * 0.16f, 1.27f, -1.0f), 0.07f, i == 0 ? 0.26f : 0.19f, Quaternion.Euler(0, 0, 0), 4);
            var iron = M("#5d646e");
            CbArt.Box(iron, kodaCage, new Vector3(-0.25f, 0.2f, -0.98f), new Vector3(0.22f, 0.2f, 0.08f));
            CbArt.Cylinder(iron, kodaCage, new Vector3(-0.25f, 0.34f, -0.98f), 0.07f, 0.04f, Quaternion.Euler(90, 0, 0));

            // the scroll: rolled parchment tied with a red ribbon, the Iron Crown's seal in red wax on it
            scroll = CbArt.Joint(root, ScrollRest.x, ScrollRest.y, ScrollRest.z, "prophecyScroll");
            scroll.localRotation = Quaternion.Euler(0, 25f, 0);
            var paper = CbArt.Toon("#c9ad78", CbArt.PropOutline);
            var ink = CbArt.Toon("#4a3524", 0f);
            var knob = CbArt.Toon("#5a3a22", 0f);
            var wax = CbArt.Toon("#b3261e", 0f);
            scrollPaper = CbArt.Joint(scroll, 0, 0, 0, "paper");
            CbArt.Box(paper, scrollPaper, new Vector3(0, 0, -0.01f), new Vector3(0.5f, 0.34f, 0.01f));
            // lines of writing either side of the seal
            for (int i = 0; i < 4; i++)
            {
                float ly = 0.11f - i * 0.07f;
                CbArt.Box(ink, scrollPaper, new Vector3(-0.13f, ly, -0.017f), new Vector3(0.14f, 0.012f, 0.004f));
                CbArt.Box(ink, scrollPaper, new Vector3(0.13f, ly, -0.017f), new Vector3(i == 3 ? 0.08f : 0.14f, 0.012f, 0.004f));
            }
            for (int i = 0; i < 2; i++)
            {
                scrollRods[i] = CbArt.Cylinder(paper, scroll, new Vector3(0, i == 0 ? -0.18f : 0.18f, 0), 0.045f, 0.56f, Quaternion.Euler(0, 0, 90));
                foreach (var e in new[] { -0.3f, 0.3f })
                    CbArt.Cylinder(knob, scrollRods[i], new Vector3(0, e / 0.56f, 0), 1.25f, 0.07f / 0.56f);
            }
            CbArt.Cylinder(wax, scroll, new Vector3(0, 0, -0.03f), 0.075f, 0.02f, Quaternion.Euler(90, 0, 0));
            for (int i = -1; i <= 1; i++)
                CbArt.Cone(iron, scroll, new Vector3(i * 0.028f, 0.012f + (i == 0 ? 0.008f : 0), -0.045f), 0.014f, 0.04f, null, 4);
            // rolled up: the paper hidden, the two rods together
            Unroll(0f);
        }

        /// <summary>The scroll from rolled (0) to open (1): the rods part, the paper between them.</summary>
        public void Unroll(float k)
        {
            if (scroll == null) return;
            scrollPaper.localScale = new Vector3(1, Mathf.Max(0.02f, k), 1);
            for (int i = 0; i < 2; i++)
                scrollRods[i].localPosition = new Vector3(0, (i == 0 ? -1 : 1) * Mathf.Lerp(0.05f, 0.18f, k), 0);
        }

        /// <summary>Raja's staff smashes the cage: its bars fly, and it is empty but for the scroll.</summary>
        public void BreakKodaCage()
        {
            if (kodaCage == null || kodaCageBroken) return;
            kodaCageBroken = true;
            kodaCage.gameObject.SetActive(false);
            int side = g.hero.x <= KodaCageX ? 1 : -1;
            // the tarp torn in pieces, the bars
            for (int k = 0; k < 5; k++)
            {
                var rag = CbArt.Box(TarpCloth, g.transform, new Vector3(KodaCageX + Random.Range(-0.7f, 0.7f), Random.Range(0.8f, 1.8f), Random.Range(-0.5f, 0.5f)),
                    new Vector3(Random.Range(0.5f, 0.9f), 0.03f, Random.Range(0.4f, 0.8f)), Random.rotation);
                g.AddDebris(rag, new Vector3(side * Random.Range(1f, 4f), Random.Range(4f, 7f), Random.Range(-1f, 1f)), Random.Range(-300f, 300f));
            }
            for (int k = 0; k < 14; k++)
            {
                float len = Random.Range(0.6f, 1.5f);
                var bar = CbArt.Cylinder(k < 10 ? Bamboo : Wood, g.transform,
                    new Vector3(KodaCageX + Random.Range(-0.8f, 0.8f), Random.Range(0.3f, 1.6f), Random.Range(-0.3f, 0.8f)), k < 10 ? 0.06f : 0.09f, len, Random.rotation);
                g.AddDebris(bar, new Vector3(side * Random.Range(1.5f, 5.5f) + Random.Range(-1.5f, 1.5f), Random.Range(3f, 7f), Random.Range(-1f, 1f)), Random.Range(-500f, 500f));
            }
            CbFx.Puff(new Vector3(KodaCageX, 0.9f, -0.3f), new Color(0.85f, 0.76f, 0.6f), 12, 2.2f, 0.9f, 0.55f);
            CbFx.Sfx("heavy", 0.8f);
            CbFx.Sfx("hit", 0.7f);
            g.shake = Mathf.Max(g.shake, 0.35f);
        }

        static Material TarpCloth => M("#5c5a3c");

        void ResetKodaCage()
        {
            if (kodaCage == null) return;
            kodaCageBroken = false;
            kodaCage.gameObject.SetActive(true);
            scroll.gameObject.SetActive(true);
            scroll.localPosition = ScrollRest;
            scroll.localRotation = Quaternion.Euler(0, 25f, 0);
            Unroll(0f);
        }

        // ---------- Tusk's trees ----------

        // The trunks Tusk rams at the ends of his charges: great bark columns rising through the canopy from far
        // below, a little back from the path (Raja passes in front of them; the boar's tusks reach the bark), shaking
        // when he hits them, a few leaves coming down. Shaded softly as the scenery is, without outlines (outlined and
        // in one hard light step they read as planks; the jungle's kapok model, tried instead, was the pale faceted
        // tree the user had taken out of stage 1-1).
        const float TuskTreeZ = 1.3f, TuskTreeR = 0.95f;
        readonly List<(Transform t, float x)> tuskTrees = new List<(Transform, float)>();
        readonly List<float> treeShake = new List<float>();

        const float BarkTile = 1.6f;
        static Texture2D barkTex;

        /// <summary>
        /// Bark made here: long furrows running up, wandering a little, dark in the grooves and lighter on the ridges,
        /// with a few knots; it wraps round and repeats up the trunk without a seam.
        /// </summary>
        static Texture2D BarkTexture()
        {
            if (barkTex != null) return barkTex;
            const int W = 128, H = 256;
            barkTex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "bark" };
            Color deep = CbArt.Hex("#3b2a1f"), mid = CbArt.Hex("#6b4e38"), ridge = CbArt.Hex("#9a7a58");
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = y / (float)H, u = x / (float)W;
                    // furrows: ridges round the trunk (a whole number of them, so it wraps), each wandering with height
                    float wander = Mathf.Sin(v * Mathf.PI * 2f * 2f + u * 9f) * 0.035f + Mathf.Sin(v * Mathf.PI * 2f * 5f + u * 23f) * 0.012f;
                    float f = Mathf.Sin((u + wander) * Mathf.PI * 2f * 7f);
                    f = Mathf.Sign(f) * Mathf.Pow(Mathf.Abs(f), 0.6f);
                    // plates: a little grain along them
                    float grain = Mathf.PerlinNoise(u * 40f, v * 6f) * 0.35f;
                    float k = Mathf.Clamp01(0.5f + 0.5f * f - 0.15f + grain * 0.6f);
                    var c = k < 0.5f ? Color.Lerp(deep, mid, k * 2f) : Color.Lerp(mid, ridge, (k - 0.5f) * 2f);
                    // a knot or two
                    float kx = Mathf.Min(Mathf.Abs(u - 0.3f), 1 - Mathf.Abs(u - 0.3f)), ky = Mathf.Min(Mathf.Abs(v - 0.62f), 1 - Mathf.Abs(v - 0.62f));
                    float knot = Mathf.Sqrt(kx * kx * 9f + ky * ky * 25f);
                    if (knot < 0.12f) c = Color.Lerp(deep, mid, knot / 0.12f);
                    px[y * W + x] = c;
                }
            barkTex.SetPixels(px);
            barkTex.Apply(true);
            return barkTex;
        }

        static Material SoftBark(string hex)
        {
            var m = CbArt.NewToon(CbArt.Hex(hex), 0f);
            m.SetFloat("_SpecStrength", 0f);
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0.2f);
            if (m.HasProperty("_BandSoftness")) m.SetFloat("_BandSoftness", 0.4f);
            return m;
        }

        /// <summary>
        /// A great bark column rising through the canopy at x, z back from the path, r across: furrows and ridges,
        /// buttress roots behind, vines hanging, a bough going up out of sight (to the side the side says).
        /// </summary>
        Transform BarkTrunk(float x, float z, float r, int side, string name)
        {
            // the column itself in grooved bark (one flat colour read as a plank), about BarkTile metres a repeat
            var column = SoftBark("#ffffff");
            column.mainTexture = BarkTexture();
            column.mainTextureScale = new Vector2(Mathf.Max(1, Mathf.Round(2 * Mathf.PI * r / BarkTile)), 28f / (BarkTile * 2f));
            var bark = SoftBark("#6b4e38");
            var dark = SoftBark("#4a3527");
            var light = SoftBark("#8a6a4c");
            var vine = SoftBark("#3f7a3a");
            var t = CbArt.Joint(root, x, 0f, z, name);
            CbArt.Cylinder(column, t, new Vector3(0, 2f, 0), r, 28f);
            // deep furrows and lighter ridges of bark, wandering a little as they go up
            for (int i = 0; i < 12; i++)
            {
                float a = (i / 12f + 0.03f * side) * Mathf.PI * 2f;
                var m = i % 3 == 0 ? light : dark;
                float w = i % 3 == 0 ? 0.16f : 0.07f;
                CbArt.Box(m, t, new Vector3(Mathf.Sin(a) * r * 0.98f, 2f, -Mathf.Cos(a) * r * 0.98f),
                    new Vector3(w, 28f, 0.06f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, (i % 2 == 0 ? 1 : -1) * 0.6f));
            }
            // its foot flaring out into the bough, buttress roots spreading from it and down into the bark (flat boards
            // for roots read as brown paper triangles stuck to its sides: the survey's pictures), vines hanging down
            // (moss drawn as patches on it read as green stickers)
            var flare = new Material(column) { mainTextureScale = new Vector2(column.mainTextureScale.x, FlareHeight / (BarkTile * 2f)) };
            CbArt.Part(CbMesh.Frustum(1f / FlareOut, 16, false), flare, t, new Vector3(0, -0.15f, 0), new Vector3(r * FlareOut, FlareHeight, r * FlareOut));
            var outward = Quaternion.FromToRotation(Vector3.up, new Vector3(0, -0.38f, 1f));
            foreach (var ra in new[] { -75f, -25f, 25f, 75f })
            {
                var root1 = CbArt.Joint(t, 0, 0, 0, "root");
                root1.localRotation = Quaternion.Euler(0, side < 0 ? ra : -ra, 0);
                CbArt.Part(CbMesh.Cone(10), bark, root1, new Vector3(0, 0.45f, r * 1.05f), new Vector3(0.34f, 1.5f, 0.34f), outward);
            }
            for (int i = 0; i < 3; i++)
                CbArt.Cylinder(vine, t, new Vector3((i - 1) * 0.35f - side * 0.2f, 5.6f - i * 0.5f, -r - 0.06f), 0.03f, 3.2f + i * 0.6f);
            CbArt.Cylinder(bark, t, new Vector3(side * (r + 0.75f), 9.5f, 0.2f), r * 0.4f, 6f, Quaternion.Euler(0, 0, -side * 40f));
            return t;
        }

        // how much wider a great trunk is at its foot, and how high its flare goes
        const float FlareOut = 1.45f, FlareHeight = 1.7f;

        void AddTuskTrees()
        {
            foreach (var side in new[] { -1, 1 })
            {
                float x = TuskX + side * TuskTreeOff;
                tuskTrees.Add((BarkTrunk(x, TuskTreeZ, TuskTreeR, side, "tuskTree"), x));
                treeShake.Add(0f);
            }
        }

        // ---------- the firefly hollow and the golden fruit up high ----------

        // The story doc's other secrets of 1-2: a cave of fireflies that only opens with the water whip of chapter 2
        // (a hollow in a great trunk on the big bough, fireflies glowing in it, a fire of the loggers' across its mouth
        // - for now only seen, a tip saying water could put it out), and fruit up high that the staff throw brings
        // down (a golden fruit hanging out of reach off the ledge the springy leaf throws him up to).
        /// <summary>The firefly hollow's trunk; the golden fruit hanging out of reach, and the ledge it is thrown for from.</summary>
        public const float HollowX = 221f, HighFruitX = 313.6f, HighFruitY = 7.45f, FruitLedgeX = 306.6f, FruitLedgeTop = 6.2f;
        const float HollowZ = 2.4f, HollowR = 1.5f;
        bool hollowTold, fruitTold;

        void AddFireflyHollow()
        {
            var t = BarkTrunk(HollowX, HollowZ, HollowR, 1, "hollowTree");
            // the hollow: a dark mouth in the bark, a lip of lighter wood round it
            CbArt.Part(CbMesh.Sphere, SoftBark("#8a6a4c"), t, new Vector3(0, 1.25f, -HollowR + 0.05f), new Vector3(1.75f, 2.55f, 0.3f));
            CbArt.Part(CbMesh.Sphere, SoftBark("#120c08"), t, new Vector3(0, 1.2f, -HollowR - 0.02f), new Vector3(1.45f, 2.3f, 0.3f));
            // the loggers' fire across it: logs crossed, stones round them (the flames and the fireflies: UpdateHollow)
            var log = SoftBark("#3a2a1e");
            CbArt.Cylinder(log, t, new Vector3(-0.15f, 0.2f, -HollowR - 0.3f), 0.15f, 1.5f, Quaternion.Euler(0, 20f, 90f));
            CbArt.Cylinder(log, t, new Vector3(0.2f, 0.26f, -HollowR - 0.35f), 0.14f, 1.4f, Quaternion.Euler(0, -25f, 90f));
            var stone = SoftBark("#6d6a64");
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                CbArt.Part(CbMesh.Sphere, stone, t, new Vector3(Mathf.Cos(a) * 0.75f, 0.08f, -HollowR - 0.3f + Mathf.Sin(a) * 0.3f), new Vector3(0.28f, 0.2f, 0.24f));
            }
        }

        void AddHighFruitVine()
        {
            // the vine the golden fruit hangs from, down out of the canopy
            var vine = SoftBark("#3f7a3a");
            CbArt.Cylinder(vine, root, new Vector3(HighFruitX, HighFruitY + 3.2f, 0.05f), 0.035f, 5.6f);
            CbArt.Part(CbMesh.Sphere, SoftBark("#4f8a2e"), root, new Vector3(HighFruitX + 0.12f, HighFruitY + 0.75f, 0.05f), new Vector3(0.35f, 0.12f, 0.22f), Quaternion.Euler(0, 0, -20f));
        }

        /// <summary>The fire at the hollow's mouth and the fireflies inside; the tips at the hollow and the fruit's ledge.</summary>
        void UpdateHollow(float dt, CbHero hero)
        {
            if (Mathf.Abs(hero.x - HollowX) < 22f)
            {
                var fire = new Vector3(HollowX + Random.Range(-0.45f, 0.45f), 0.35f, HollowZ - HollowR - 0.32f);
                if (Random.value < 0.75f)
                    CbFx.Spark(fire, Random.value < 0.5f ? CbFx.Fire : CbFx.Flame, 1, 1.2f, Random.Range(0.25f, 0.45f), 0.5f, -5f);
                if (Random.value < 0.08f)
                    CbFx.Spark(new Vector3(HollowX + Random.Range(-0.5f, 0.5f), Random.Range(0.6f, 2f), HollowZ - HollowR + 0.05f),
                        new Color(0.85f, 1f, 0.45f) * 2.4f, 1, 0.25f, 0.12f, 1.4f, -0.3f);
            }
            if (!hollowTold && Mathf.Abs(hero.x - HollowX) < 3f)
            {
                hollowTold = true;
                g.hud.SetTip("Fireflies glow in a hollow behind the flames... water could put them out");
            }
            if (!fruitTold && hero.y > FruitLedgeTop - 0.5f && Mathf.Abs(hero.x - FruitLedgeX) < 1.5f)
            {
                fruitTold = true;
                g.hud.SetTip(CbHero.StaffThrowLearned
                    ? "A golden fruit hangs out of reach: hold <color=#ffb03a>KI</color> to throw the staff for it"
                    : "A golden fruit hangs out of reach... if only something could fly out and fetch it");
            }
        }

        /// <summary>Tusk's tusks hit the trunk nearest x: it shakes (by amount), and leaves come down.</summary>
        public void ShakeTree(float x, float amount)
        {
            int best = -1;
            for (int i = 0; i < tuskTrees.Count; i++)
                if (best < 0 || Mathf.Abs(tuskTrees[i].x - x) < Mathf.Abs(tuskTrees[best].x - x)) best = i;
            if (best < 0) return;
            treeShake[best] = Mathf.Max(treeShake[best], amount);
            float tx = tuskTrees[best].x;
            for (int i = 0; i < Mathf.CeilToInt(8 * amount); i++)
                CbFx.Spark(new Vector3(tx + Random.Range(-2.5f, 2.5f), Random.Range(5f, 8f), Random.Range(-0.8f, 0.4f)),
                    Color.Lerp(CbArt.Hex("#4f8a2e"), CbArt.Hex("#9cc04a"), Random.value), 1, 1.2f, 0.3f, 1.8f, 1.6f);
        }

        /// <summary>How hard each of Tusk's trees is shaking (test hook).</summary>
        public float TreeShake(int i) => treeShake[i];
        public int TuskTrees => tuskTrees.Count;

        /// <summary>A rope bridge over a gap, from cx - half to cx + half.</summary>
        void AddRopeBridge(float cx, float half)
        {
            var bridge = new Crossing { cx = cx, half = half, top = 0.145f, sag = RopeSag, deck = true };
            bridge.t = StringRopeBridge(bridge);
            bridge.home = bridge.t != null ? bridge.t.localPosition : Vector3.zero;
            crossings.Add(bridge);
        }

        // ---------- saw drones ----------

        // A saw drone (drawn in Canva, built in Meshy: its blade and propeller split to spin): it flies to and fro
        // between two points every period seconds, its blade spinning; touching the blade hurts him (sliding passes
        // under one at head height, a jump over it); a paw only clangs off it, two staff blows smash it.
        const string SawDronePath = "Cloudborne/Treetops/SawDrone/SawDrone";
        // the blade's middle under the drone's, its reach, the hurt it does, the score for smashing one
        const float SawBladeDrop = 0.305f, SawReach = 0.42f, SawDamage = 12f;
        const int SawScore = 300;

        class Saw
        {
            public Vector2 a, b;
            public float period, phase, flash;
            public int hp;
            public bool smashed;
            public Transform t, blade, prop;
        }

        readonly List<Saw> saws = new List<Saw>();

        /// <summary>How many saw drones the stage has, and how many are still flying (test hooks).</summary>
        public int SawCount => saws.Count;
        public int SawsFlying => saws.FindAll(s => !s.smashed).Count;
        /// <summary>Where saw drone i's blade is now (test hook).</summary>
        public Vector2 SawBlade(int i) { var p = saws[i].t.position; return new Vector2(p.x, p.y - SawBladeDrop); }

        void AddSaw(Vector2 a, Vector2 b, float period, float phase)
        {
            var t = CbArt.Joint(root, a.x, a.y, 0f, "sawDrone");
            var s = new Saw { a = a, b = b, period = period, phase = phase, hp = 2, t = t };
            var model = CbArt.Prop(SawDronePath, t, CbArt.PropOutline);
            if (model != null)
            {
                foreach (var c in model.GetComponentsInChildren<Transform>())
                {
                    if (c.name == "Saw") s.blade = c;
                    else if (c.name == "Propeller") s.prop = c;
                }
            }
            else
            {
                // stand-in: a rusty barrel, a steel disc under it
                CbArt.Box(CbArt.Toon("#8a5232", CbArt.PropOutline), t, Vector3.zero, new Vector3(0.6f, 0.4f, 0.4f));
                s.blade = CbArt.Part(CbMesh.Cylinder, CbArt.Toon("#b8c0c4", CbArt.PropOutline), t, new Vector3(0, -SawBladeDrop, 0),
                    new Vector3(0.38f, 0.01f, 0.38f), Quaternion.Euler(90, 0, 0));
            }
            saws.Add(s);
        }

        void UpdateTreetops(float dt, CbHero hero)
        {
            float time = g.clock;
            UpdateHollow(dt, hero);
            for (int i = 0; i < tuskTrees.Count; i++)
            {
                treeShake[i] = Mathf.MoveTowards(treeShake[i], 0, dt * 1.3f);
                tuskTrees[i].t.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 27f + i) * treeShake[i] * 1.4f);
            }
            foreach (var s in saws)
            {
                if (s.smashed) continue;
                // to and fro, easing at each end, bobbing a little
                float u = 0.5f - 0.5f * Mathf.Cos((time / s.period + s.phase) * Mathf.PI * 2f);
                var p = Vector2.Lerp(s.a, s.b, u);
                s.t.localPosition = new Vector3(p.x, p.y + Mathf.Sin(time * 3.1f + s.phase * 5f) * 0.06f, 0f);
                if (s.blade != null) s.blade.Rotate(Vector3.forward, -900f * dt, Space.World);
                if (s.prop != null) s.prop.Rotate(Vector3.up, 1400f * dt, Space.World);
                s.flash = Mathf.Max(0, s.flash - dt);
                // the blade cuts him if it meets him (low in a slide, he passes under one at head height)
                float bx = p.x, by = p.y - SawBladeDrop;
                if (Mathf.Abs(hero.x - bx) < SawReach + HalfWidth * 0.5f && hero.y < by + SawReach && hero.y + hero.Height > by - SawReach)
                {
                    hero.TakeHit(SawDamage, hero.x < bx ? -1 : 1);
                    if (Random.value < 0.5f) CbFx.Spark(new Vector3(bx, by, -0.4f), CbFx.Grey, 4, 4, 0.2f, 0.2f, 4);
                }
            }
        }

        bool StrikeSaws(float x, float y, int dir, float range, bool staff)
        {
            foreach (var s in saws)
            {
                if (s.smashed) continue;
                var p = s.t.position;
                float ahead = (p.x - x) * dir;
                if (ahead < -0.4f || ahead > range + 0.5f || p.y - SawBladeDrop - 0.5f > y + 2.2f || p.y < y - 0.2f) continue;
                var at = new Vector3(p.x, p.y - SawBladeDrop * 0.5f, -0.4f);
                if (!staff)
                {
                    // a paw clangs off the steel
                    CbFx.Spark(at, CbFx.Grey, 8, 5, 0.3f, 0.25f, 4);
                    CbFx.Sfx("block", 0.7f);
                    g.hud.SetTip("A saw drone! Smash it with the staff (hold <color=#ffb03a>ATTACK</color>), or slide under it (<color=#ffb03a>stick ↓</color>)");
                    return true;
                }
                s.hp--;
                s.flash = 0.15f;
                CbFx.Spark(at, CbFx.Hit, 12, 6, 0.35f, 0.35f, 6);
                CbFx.Sfx("heavy", 0.8f);
                g.shake = Mathf.Max(g.shake, 0.25f);
                if (s.hp > 0) return true;
                // smashed: it falls apart in sparks and purple oil, and drops away
                s.smashed = true;
                s.t.gameObject.SetActive(false);
                CbFx.Spark(at, CbFx.Purple, 18, 7, 0.4f, 0.6f, 9);
                CbFx.Puff(at, new Color(0.35f, 0.3f, 0.3f), 8, 1.8f, 0.8f, 0.5f);
                g.score += SawScore;
                g.hud.Popup(new Vector3(p.x, p.y + 0.8f, 0), "SMASHED! +" + SawScore, CbHud.PopStyle.Big);
                return true;
            }
            return false;
        }

        void ResetTreetops()
        {
            foreach (var s in saws) { s.smashed = false; s.hp = 2; s.flash = 0; s.t.gameObject.SetActive(true); }
            for (int i = 0; i < treeShake.Count; i++) treeShake[i] = 0;
            hollowTold = fruitTold = false;
            ResetKodaCage();
        }
    }
}
