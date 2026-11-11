using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The long middle of stage 1-1, between the mountain and the brook before Snap (the user asked for a longer
    /// stage, many climbs, many challenges and interesting things on the way, and "add everything"):
    /// <list type="bullet">
    /// <item>The vine ravine (196-240): vines to swing across, bamboo bars hung over the water, a giant springy leaf
    /// up to a tiger token.</item>
    /// <item>The temple of traps (240-292): thorns cut with the staff, a hidden pit, a dart statue, swinging logs, a
    /// lever's gate, a stone block pushed onto a plate for the second gate, a snapping plant.</item>
    /// <item>The gong gorge (292-336): a gong that lays a bridge, a beehive over two raiders, a cage to break open.</item>
    /// <item>The raft river (336-400): a mud bank, rafts drifting down a long river, a rock island with a snapping
    /// plant, a spinning log that rolls him back.</item>
    /// <item>The canopy and the waterfall (400-470): a vine-grown pillar, an updraft, ledges and bars in the treetops
    /// with a key, a zipline down, a seesaw and a stone, a lift on a rope, a waterfall with a cave behind it (a
    /// token), rolling boulders, a chest the key opens (a token).</item>
    /// <item>The ridge (470-515): a shaft to wall-jump up, a cage.</item>
    /// </list>
    /// Stone lanterns light up as he passes: falling, he comes back at the last one (CbGame).
    /// Everything is built in code from simple shapes and the jungle's models until drawings are approved.
    /// </summary>
    public partial class CbLevel
    {
        /// <summary>The temple's hidden pits (x from, to): leaves over a pit of spikes.</summary>
        public static readonly (float x0, float x1)[] Pits = { (254f, 256.5f) };
        // a pit's hole: from just behind the ground's front face back to PitBack, PitDepth deep
        public const float PitDepth = 2.8f, PitFront = -1.68f, PitBack = 1.2f;

        /// <summary>Where the ground is cut, in order: the streams and the pits.</summary>
        public static IEnumerable<(float x0, float x1)> Gaps
        {
            get
            {
                var all = new List<(float, float)>(Waters);
                all.AddRange(Pits);
                all.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                return all;
            }
        }

        // ---------- the things ----------

        /// <summary>A vine hanging from a branch at (ax, ay), len long, swinging by angle (radians, 0 straight down).</summary>
        public class Vine { public float ax, ay, len, angle, speed, cool; public Transform t; }
        /// <summary>A bamboo bar at height y from x0 to x1, hung over the water: he hangs from it and moves along.</summary>
        public class Bar { public float x0, x1, y; }
        /// <summary>A rope slid down from (x0, y0) to (x1, y1).</summary>
        public class Zip { public float x0, y0, x1, y1; public float At(float x) => Mathf.Lerp(y0, y1, Mathf.InverseLerp(x0, x1, x)); }
        class Leaf { public float x, half, top, squash; public Transform t; }
        class Draft { public float x0, x1, y0, y1; }
        class Raft { public float x, from, to, speed, sink, baseY; public Transform t; }
        class Lift { public float x, half, top, lo, hi; public Transform deck, weight; }
        class Lantern { public float x; public bool lit; public Material light; public Renderer window; }
        class Gate { public Block b; public float open; public bool wantOpen, sinks; public Vector3 home; }
        class Thorns { public Block b; public int hp = 2; public float cool, clang; }
        class Pendulum { public float x, y, len, period, phase; public Transform t; }
        class Snapper { public float x, y, timer, period = 2.2f, cool; public int hp = 3; public bool dead; public Transform t, jaw, model; }
        class Pit { public float x0, x1, stood; public bool open; public Transform cover; }
        class Dart { public float x, y, vx, gone; public Transform t; }
        class Cage { public float x; public int hp = 3; public bool freed; public Transform t, animal, model; public float hop, cool; }
        class Boulder { public float x, vx, cool; public bool rolling; public Transform t; }

        const float LeafBounce = 19.5f, RaftTop = 0.1f, LogTop = 0.15f, LogRollBack = 1.4f, SeesawTop = 0.35f;
        const float StatueX = 262.5f, PlateX = 281.5f, LeverX = 273f, GongX = 296.5f, BridgeFrom = 300f, BridgeTo = 312f;
        const float HiveX = 320f, HiveY = 4.4f, ChestX = 467.5f, SeesawX = 430f, MudFrom = 338f, MudTo = 344f;
        const float LogFrom = 370f, LogTo = 378f;
        // the darts fly from the guardian's mouth: over a ducking Raja, into a standing one
        const float DartY = 1.35f;

        public readonly List<Vine> vines = new List<Vine>();
        public readonly List<Bar> bars = new List<Bar>();
        public readonly List<Zip> zips = new List<Zip>();
        readonly List<Leaf> leaves = new List<Leaf>();
        readonly List<Draft> drafts = new List<Draft>();
        readonly List<Raft> rafts = new List<Raft>();
        readonly List<Lift> lifts = new List<Lift>();
        readonly List<Lantern> lanterns = new List<Lantern>();
        readonly List<Gate> gates = new List<Gate>();
        readonly List<Thorns> thorns = new List<Thorns>();
        readonly List<Pendulum> pendulums = new List<Pendulum>();
        readonly List<Snapper> snappers = new List<Snapper>();
        readonly List<Pit> pits = new List<Pit>();
        readonly List<Dart> darts = new List<Dart>();
        readonly List<Cage> cages = new List<Cage>();
        readonly List<Boulder> boulders = new List<Boulder>();
        Block pushBlock;
        float pushHome, dartT, bridge, hiveFall = -1, hiveY, bees, beeTick, logSpin, seesawT = -1, boulderT;
        bool lever, gong, chestOpen, leverModel;
        Transform leverHandle, gongDisc, hive, chestLid, chestModel, logT, plank, stone, bridgeT;

        /// <summary>The stone lanterns where he comes back after falling.</summary>
        public IEnumerable<float> Checkpoints { get { foreach (var l in lanterns) yield return l.x; } }
        public bool GongRung => gong;
        public bool BridgeBuilt => bridge >= 1;
        public bool LeverPulled => lever;
        public bool GateOpen(int i) => gates[i].b.broken;
        public bool ChestOpen => chestOpen;
        public float PushBlockX => (pushBlock.x0 + pushBlock.x1) / 2;
        public bool PitOpen(int i) => pits[i].open;
        public int CagesFreed => cages.FindAll(c => c.freed).Count;
        public bool ThornsCut(int i) => thorns[i].b.broken;

        static Material M(string hex) => CbArt.Toon(hex, CbArt.PropOutline);

        const string WaysDir = "Cloudborne/Jungle/Ways/";

        /// <summary>
        /// One of the long stage's modelled things (drawn in Canva, approved, built by Meshy), its foot at parent's
        /// origin, turned yaw (Meshy's front is +Z, away from the camera: 180 faces it), sized to its own proportions;
        /// null if it is not in the project (then the stand-in built in code shows).
        /// </summary>
        static Transform Model(string name, Transform parent, Vector3 size, float yaw)
        {
            var turn = CbArt.Joint(parent, 0, 0, 0, "model");
            turn.localRotation = Quaternion.Euler(0, yaw, 0);
            var fit = Fit(WaysDir + name + "/" + name, turn, new Vector3(-size.x / 2, 0, -size.z / 2), new Vector3(size.x / 2, size.y, size.z / 2));
            if (fit == null) { Object.Destroy(turn.gameObject); return null; }
            return turn;
        }
        static Material Wood => M("#8a5a32");
        static Material Bamboo => M("#c9a45a");
        static Material VineGreen => M("#3f7a3a");
        static Material StoneGrey => M("#8f9a8a");
        static Material leafMat;
        /// <summary>Leaves: drawn from both sides, without outlines (an outline round a thin leaf turns it black).</summary>
        static Material LeafMat
        {
            get
            {
                if (leafMat == null) { leafMat = CbArt.NewToon(CbArt.Hex("#5aa548"), 0f); leafMat.SetFloat("_Cull", 0f); leafMat.SetFloat("_RimStrength", 0.15f); }
                return leafMat;
            }
        }

        // ---------- building ----------

        void BuildWays()
        {
            // stone lanterns: where he comes back (the start counts as one)
            foreach (var x in new[] { 108f, 196f, 242f, 293f, 337f, 401f, 472f, 536f }) AddLantern(x);
            // the ends of the stage: closed gates where he can go no further
            AddEndGate(CbWorld.LevelMin - HalfWidth - 1.75f);
            AddEndGate(CbWorld.LevelMax + HalfWidth + 1.75f);

            // ---- the vine ravine
            AddVine(204f, 9f, 5f);
            AddVine(211.5f, 9f, 5f);
            AddBar(215.5f, 223f, 4.8f);
            AddVine(226.8f, 9.4f, 5.4f);
            AddLeaf(235.5f, 0.45f);
            AddLedge(238f, 1.6f, 6.2f, false);

            // ---- the temple of traps
            AddThorns(246f);
            foreach (var (x0, x1) in Pits) AddPit(x0, x1);
            BuildStatue();
            AddPendulum(266f, 6.2f, 4.6f, 2.4f, 0f);
            AddPendulum(269.5f, 6.2f, 4.6f, 2.4f, 1.2f);
            BuildLever();
            AddGate(275f);
            BuildPushBlock(277.5f);
            BuildPlate();
            AddGate(284f);
            AddSnapper(289f, 0f);

            // ---- the gong gorge
            BuildGong();
            BuildHive();
            AddCage(327f);
            AddSnapper(331f, 0f);

            // ---- the raft river
            BuildMud();
            foreach (var (from, to, offset) in new[] { (346f, 365.5f, 0f), (346f, 365.5f, 10f), (378.5f, 394f, 0f), (378.5f, 394f, 8f) })
                AddRaft(from, to, offset);
            AddRock(367.8f, 3.6f, 0.6f);
            AddSnapper(367.8f, 0.6f);
            BuildRollingLog();

            // ---- the canopy and the waterfall
            AddCage(404f);
            AddTerrace(408f, 410f, 8f, 1.6f);
            blocks[blocks.Count - 1].vines = -1;
            DressVines(blocks[blocks.Count - 1], 408f);
            AddLedge(411.6f, 1.6f, 8.4f, false);
            AddDraft(413.4f, 414.6f, 0f, 10f);
            AddLedge(415.6f, 1.4f, 9.2f, false);
            AddBar(417f, 422f, 10.8f);
            AddLedge(423.6f, 1.6f, 9.6f, false);
            AddZip(424.6f, 11f, 437.2f, 3.4f);
            BuildSeesaw();
            AddLedge(434.5f, 1.4f, 6.2f, false);
            AddLift(436.6f, 0.35f, 7.6f);
            AddTerrace(440f, 446f, 9.4f, 3f);
            BuildWaterfall(446f, 449.5f, 9.4f);
            AddLedge(447f, 1.2f, 2.6f, false);
            AddLedge(448.4f, 1.2f, 4.8f, false);
            AddLedge(447.2f, 1.4f, 7f, false);
            boulders.Add(new Boulder { x = 466f });
            BuildChest();

            // ---- the ridge
            AddCage(492f);
            AddTerrace(504f, 505.4f, 7f, 2f);
            AddTerrace(507.2f, 508.6f, 7f, 2f);
            AddLedge(506.3f, 1.6f, 8.4f, false);
        }

        void AddLantern(float x)
        {
            var t = CbArt.Joint(root, x, 0, 1.2f, "lantern");
            var light = new Material(CbArt.Emissive(new Color(0.15f, 0.12f, 0.1f)));
            Renderer window = null;
            if (Model("Lantern", t, new Vector3(0.86f, 2.1f, 0.88f), 180f) != null)
            {
                // its window glows once he has passed (unlit, the painting's own window shows)
                window = CbArt.Box(light, t, new Vector3(0, 1.42f, -0.45f), new Vector3(0.36f, 0.26f, 0.02f)).GetComponent<Renderer>();
                window.enabled = false;
            }
            else
            {
                CbArt.Box(StoneGrey, t, new Vector3(0, 0.15f, 0), new Vector3(0.7f, 0.3f, 0.7f));
                CbArt.Box(StoneGrey, t, new Vector3(0, 0.75f, 0), new Vector3(0.3f, 0.9f, 0.3f));
                CbArt.Box(light, t, new Vector3(0, 1.4f, 0), new Vector3(0.5f, 0.4f, 0.5f));
                CbArt.Cone(StoneGrey, t, new Vector3(0, 1.85f, 0), 0.5f, 0.5f, null, 4);
            }
            lanterns.Add(new Lantern { x = x, light = light, window = window });
        }

        /// <summary>
        /// A liana hanging from a thick branch: a twisted dark-green rope with a few small leaves, and at its end a
        /// knot with a soft glow, so it reads as a thing to take hold of and swing on.
        /// </summary>
        void AddVine(float ax, float ay, float len)
        {
            var branch = CbArt.Joint(root, ax, ay, 0.3f, "branch");
            CbArt.Cylinder(Wood, branch, new Vector3(0, 0.25f, 0.2f), 0.26f, 4f, Quaternion.Euler(0, 0, 90));
            for (int k = -1; k <= 1; k++)
                CbArt.Part(CbMesh.Leaf(), LeafMat, branch, new Vector3(k * 1.3f, 0.55f, 0.1f), Vector3.one * 0.5f, Quaternion.Euler(-30, k * 40f, k * 25f), shadows: false);
            var t = CbArt.Joint(root, ax, ay, 0.05f, "vine");
            var rope = M("#4d6b2c");
            CbArt.Cylinder(rope, t, new Vector3(0, -len / 2, 0), 0.055f, len);
            CbArt.Cylinder(M("#3d5822"), t, new Vector3(0.05f, -len / 2, -0.03f), 0.03f, len, Quaternion.Euler(0, 0, 1.2f));
            for (float d = 0.7f; d < len - 0.4f; d += 0.75f)
                CbArt.Part(CbMesh.Leaf(), LeafMat, t, new Vector3((d % 1.5f < 0.75f ? 1 : -1) * 0.1f, -d, -0.04f), Vector3.one * 0.16f,
                    Quaternion.Euler(0, 0, (d % 1.5f < 0.75f ? -1 : 1) * 60f), shadows: false);
            CbArt.Part(CbMesh.Sphere, M("#3d5822"), t, new Vector3(0, -len, 0), Vector3.one * 0.2f);
            CbArt.Part(CbMesh.Quad, CbArt.Glow(new Color(1f, 0.9f, 0.5f) * 0.5f, CbArt.Soft), t, new Vector3(0, -len, -0.2f), Vector3.one * 0.9f, shadows: false).name = "grip";
            vines.Add(new Vine { ax = ax, ay = ay, len = len, t = t });
        }

        void AddBar(float x0, float x1, float y)
        {
            var t = CbArt.Joint(root, (x0 + x1) / 2, y, 0.05f, "bars");
            // the model: two-metre poles of bamboo lashed end to end (one stretched along the whole bar looked drawn out)
            float len = x1 - x0 + 0.4f;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 2f));
            bool modelled = false;
            for (int k = 0; k < n; k++)
            {
                var seg = CbArt.Joint(t, -len / 2 + (k + 0.5f) * len / n, -0.08f, 0, "pole");
                modelled |= Model("BambooBar", seg, new Vector3(len / n, 0.36f, 0.16f), 180f) != null;
            }
            if (!modelled) CbArt.Cylinder(Bamboo, t, Vector3.zero, 0.07f, len, Quaternion.Euler(0, 0, 90));
            // hung from the trees by ropes at each end
            foreach (float e in new[] { x0, x1 })
                CbArt.Cylinder(VineGreen, t, new Vector3(e - (x0 + x1) / 2, 2f, 0), 0.04f, 4f);
            bars.Add(new Bar { x0 = x0, x1 = x1, y = y });
        }

        void AddZip(float x0, float y0, float x1, float y1)
        {
            var a = new Vector3(x0, y0, 0.05f); var b = new Vector3(x1, y1, 0.05f);
            var rope = CbArt.Cylinder(M("#6b5a3a"), root, (a + b) / 2, 0.04f, Vector3.Distance(a, b),
                Quaternion.FromToRotation(Vector3.up, b - a));
            rope.name = "zipline";
            foreach (var p in new[] { a, b })
            {
                // a carved post with its pulley where the line is low enough for one; a tall trunk where it starts high
                if (p.y < 5f)
                {
                    var post = CbArt.Joint(root, p.x, 0, 0.6f, "zipPost");
                    if (Model("ZipPost", post, new Vector3(1.5f, p.y + 0.9f, 0.95f), 180f) != null) continue;
                    Object.Destroy(post.gameObject);
                }
                CbArt.Cylinder(Wood, root, new Vector3(p.x, p.y / 2 + 0.3f, 0.6f), 0.22f, p.y + 0.8f);
            }
            zips.Add(new Zip { x0 = x0, y0 = y0, x1 = x1, y1 = y1 });
        }

        void AddLeaf(float x, float top)
        {
            // the model: a great leaf on a curved stem, its pad 1.25 m up (jumped onto, it throws him high)
            var stem = CbArt.Joint(root, x, 0, 0f, "springLeaf");
            if (Model("Leaf", stem, new Vector3(1.38f, 1.36f, 1.45f), 180f) != null)
            {
                leaves.Add(new Leaf { x = x, half = 0.65f, top = 1.25f, t = stem });
                return;
            }
            var t = CbArt.Joint(root, x, top, 0f, "springLeaf");
            CbArt.Part(CbMesh.Leaf(), M("#5fb04a"), t, Vector3.zero, new Vector3(2.4f, 0.2f, 1.6f), Quaternion.Euler(0, 90, 0), shadows: false);
            CbArt.Cylinder(VineGreen, t, new Vector3(0, -top / 2, 0.2f), 0.08f, top);
            leaves.Add(new Leaf { x = x, half = 0.9f, top = top, t = t });
        }

        void AddDraft(float x0, float x1, float y0, float y1) => drafts.Add(new Draft { x0 = x0, x1 = x1, y0 = y0, y1 = y1 });

        void AddThorns(float x)
        {
            var t = CbArt.Joint(root, x, 0, 0f, "thorns");
            if (Model("Thorns", t, new Vector3(1.45f, 2.6f, 1.05f), 180f) == null)
            for (int k = 0; k < 5; k++)
            {
                float s = Random.Range(0.45f, 0.7f);
                CbArt.Part(CbMesh.Blob(12, 8), M("#2f5a2a"), t, new Vector3(Random.Range(-0.5f, 0.5f), 0.35f + k * 0.42f, Random.Range(-0.3f, 0.3f)),
                    new Vector3(s, s * 0.8f, s), Random.rotation);
                for (int j = 0; j < 3; j++)
                    CbArt.Cone(M("#7a3a2a"), t, new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 2.3f), -0.35f), 0.05f, 0.3f,
                        Quaternion.Euler(Random.Range(-60, 60), 0, Random.Range(-90, 90)), 4);
            }
            // taller than a jump: cut with the staff (or a double jump)
            var b = new Block { x0 = x - 0.6f, x1 = x + 0.6f, top = 2.4f, t = t, smooth = true };
            blocks.Add(b);
            thorns.Add(new Thorns { b = b });
        }

        /// <summary>
        /// A pit cut in the path: walls of the ground's own soil, a dark floor bristling with stone spikes, open to the
        /// camera (the user saw a black block where it was); leaves over it until he stands on it.
        /// </summary>
        void AddPit(float x0, float x1)
        {
            var t = CbArt.Joint(root, (x0 + x1) / 2, 0, 0, "pit");
            float w = x1 - x0, cx = (x0 + x1) / 2;
            const float Depth = PitDepth, Front = PitFront, Back = PitBack;
            // dark soil walls: the ground's front face runs on across the pit (CbWorld), so the trap can't be seen
            // from the side; opened, it shows from above
            var soil = M("#4a3326");
            CbArt.Part(CbMesh.Quad, soil, t, new Vector3(0, -Depth / 2, Back), new Vector3(w, Depth, 1), shadows: false).name = "pitBack";
            CbArt.Part(CbMesh.Quad, soil, t, new Vector3(-w / 2, -Depth / 2, (Front + Back) / 2), new Vector3(Back - Front, Depth, 1), Quaternion.Euler(0, -90, 0), shadows: false);
            CbArt.Part(CbMesh.Quad, soil, t, new Vector3(w / 2, -Depth / 2, (Front + Back) / 2), new Vector3(Back - Front, Depth, 1), Quaternion.Euler(0, 90, 0), shadows: false);
            CbArt.Box(M("#3a2c22"), t, new Vector3(0, -Depth - 0.1f, (Front + Back) / 2), new Vector3(w, 0.2f, Back - Front));
            for (float x = x0 + 0.25f; x < x1; x += 0.42f)
                for (float z = Front + 0.3f; z < Back; z += 0.55f)
                    CbArt.Cone(StoneGrey, t, new Vector3(x - cx, -Depth + 0.3f, z), 0.13f, 0.6f, null, 6);
            // the cover: a mat of leaves level with the path
            var cover = CbArt.Joint(t, 0, 0, 0, "cover");
            CbArt.Box(M("#6a8a3a"), cover, new Vector3(0, -0.03f, 0.1f), new Vector3(x1 - x0 + 0.1f, 0.06f, 2.3f));
            for (float x = x0; x < x1; x += 0.45f)
                CbArt.Part(CbMesh.Leaf(), LeafMat, cover, new Vector3(x - (x0 + x1) / 2 + 0.22f, 0.02f, Random.Range(-0.8f, 0.8f)),
                    Vector3.one * 0.25f, Quaternion.Euler(90, Random.Range(0, 360), 0), shadows: false);
            pits.Add(new Pit { x0 = x0, x1 = x1, cover = cover });
        }

        void BuildStatue()
        {
            var t = CbArt.Joint(root, StatueX, 0, 0.3f, "dartStatue");
            // the model: a guardian's head on a pillar, its open mouth turned towards the way Raja comes
            if (Model("Statue", t, new Vector3(0.79f, 1.9f, 0.81f), -110f) != null)
            {
                blocks.Add(new Block { x0 = StatueX - 0.42f, x1 = StatueX + 0.42f, top = 1.9f, t = t, smooth = true });
                return;
            }
            CbArt.Box(StoneGrey, t, new Vector3(0, 0.6f, 0), new Vector3(0.9f, 1.2f, 0.9f));
            CbArt.Box(M("#9aa594"), t, new Vector3(0, 1.45f, 0), new Vector3(0.8f, 0.7f, 0.8f));
            CbArt.Box(M("#2a2420"), t, new Vector3(-0.41f, 1.15f, 0), new Vector3(0.05f, 0.14f, 0.3f));
            CbArt.Box(M("#c9302c"), t, new Vector3(-0.41f, 1.55f, -0.18f), new Vector3(0.05f, 0.1f, 0.1f));
            CbArt.Box(M("#c9302c"), t, new Vector3(-0.41f, 1.55f, 0.18f), new Vector3(0.05f, 0.1f, 0.1f));
            blocks.Add(new Block { x0 = StatueX - 0.45f, x1 = StatueX + 0.45f, top = 1.8f, t = t, smooth = true });
        }

        void AddPendulum(float x, float y, float len, float period, float phase)
        {
            var t = CbArt.Joint(root, x, y, 0f, "pendulum");
            // the log ram (its ringed end towards the camera) on two ropes
            var ram = CbArt.Joint(t, 0, -len - 0.35f, 0, "ram");
            bool modelled = Model("LogRam", ram, new Vector3(1.75f, 1.56f, 0.7f), 90f) != null;
            var rope = modelled ? M("#9a7a48") : VineGreen;
            float rz = modelled ? 0.62f : 0.5f;
            CbArt.Cylinder(rope, t, new Vector3(0, -len / 2, -rz), 0.035f, len);
            CbArt.Cylinder(rope, t, new Vector3(0, -len / 2, rz), 0.035f, len);
            if (!modelled) CbArt.Cylinder(Wood, t, new Vector3(0, -len, 0), 0.32f, 1.7f, Quaternion.Euler(90, 0, 0));
            CbArt.Cylinder(Wood, root, new Vector3(x, y + 0.2f, 0), 0.25f, 3f, Quaternion.Euler(90, 0, 0));
            pendulums.Add(new Pendulum { x = x, y = y, len = len, period = period, phase = phase, t = t });
        }

        void BuildLever()
        {
            var t = CbArt.Joint(root, LeverX, 0, 0.2f, "lever");
            // the model: pulled, it is mirrored, so its handle leans the other way
            leverHandle = Model("Lever", t, new Vector3(0.92f, 1.2f, 0.73f), 180f);
            if (leverHandle != null) { leverModel = true; return; }
            CbArt.Box(StoneGrey, t, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.6f, 0.5f));
            leverHandle = CbArt.Joint(t, 0, 0.6f, 0, "handle");
            CbArt.Cylinder(Wood, leverHandle, new Vector3(0, 0.45f, 0), 0.06f, 0.9f);
            CbArt.Part(CbMesh.Sphere, M("#c9302c"), leverHandle, new Vector3(0, 0.95f, 0), Vector3.one * 0.22f);
            leverHandle.localRotation = Quaternion.Euler(0, 0, 35);
        }

        /// <summary>
        /// A closed bamboo gate where the stage ends at either side, the way on barred: he used to stop at a wall no one
        /// could see (the user). It stands just past where he stops, a little behind the path.
        /// </summary>
        void AddEndGate(float x)
        {
            var t = CbArt.Joint(root, x, 0, 0f, "endGate");
            var model = Model("Gate", t, new Vector3(3.45f, 3.6f, 1.24f), 180f);
            if (model != null) { model.localPosition = new Vector3(0, 0, 0.3f); return; }
            for (float bx = -1.4f; bx <= 1.41f; bx += 0.28f)
                CbArt.Cylinder(Bamboo, t, new Vector3(bx, 1.8f, 0.3f), 0.08f, 3.6f);
            CbArt.Box(Wood, t, new Vector3(0, 3.3f, 0.3f), new Vector3(3.2f, 0.2f, 0.2f));
            CbArt.Box(Wood, t, new Vector3(0, 0.8f, 0.3f), new Vector3(3.2f, 0.2f, 0.2f));
        }

        void AddGate(float x)
        {
            var t = CbArt.Joint(root, x, 0, 0f, "gate");
            // the model: stone posts and bamboo bars in one, standing just behind the path; opened, it sinks into the
            // ground
            var model = Model("Gate", t, new Vector3(3.45f, 3.6f, 1.24f), 180f);
            if (model != null)
            {
                model.localPosition = new Vector3(0, 0, 0.6f);
                var mb = new Block { x0 = x - 0.4f, x1 = x + 0.4f, top = 3.6f, t = t, smooth = true };
                blocks.Add(mb);
                gates.Add(new Gate { b = mb, home = t.localPosition, sinks = true });
                return;
            }
            for (float z = -0.6f; z <= 0.61f; z += 0.3f)
                CbArt.Cylinder(Bamboo, t, new Vector3(0, 1.8f, z), 0.08f, 3.6f);
            CbArt.Box(Wood, t, new Vector3(0, 3.5f, 0), new Vector3(0.25f, 0.25f, 1.6f));
            CbArt.Box(Wood, t, new Vector3(0, 0.5f, 0), new Vector3(0.22f, 0.2f, 1.5f));
            // posts either side stay
            CbArt.Box(StoneGrey, root, new Vector3(x, 2f, -0.95f), new Vector3(0.6f, 4f, 0.3f));
            CbArt.Box(StoneGrey, root, new Vector3(x, 2f, 0.95f), new Vector3(0.6f, 4f, 0.3f));
            var b = new Block { x0 = x - 0.4f, x1 = x + 0.4f, top = 3.6f, t = t, smooth = true };
            blocks.Add(b);
            gates.Add(new Gate { b = b, home = t.localPosition });
        }

        void BuildPushBlock(float x)
        {
            var t = CbArt.Joint(root, x, 0, 0f, "pushBlock");
            if (Model("PushBlock", t, new Vector3(1f, 1f, 1f), 180f) == null)
                CbArt.Box(RockMat(1, 1, 0, 0), t, new Vector3(0, 0.5f, 0), new Vector3(1f, 1f, 1f));
            pushBlock = new Block { x0 = x - 0.5f, x1 = x + 0.5f, top = 1f, t = t };
            pushHome = x;
            blocks.Add(pushBlock);
        }

        void BuildPlate()
        {
            CbArt.Box(M("#b89a5a"), root, new Vector3(PlateX, 0.03f, 0.1f), new Vector3(0.9f, 0.06f, 0.9f)).name = "plate";
        }

        void AddSnapper(float x, float y)
        {
            var t = CbArt.Joint(root, x, y, 0.1f, "snapper");
            // the model, its open mouth turned towards the way Raja comes (and a little to the camera); it snaps by
            // squashing its head down
            var model = Model("Snapper", t, new Vector3(1.19f, 1.7f, 1.19f), -120f);
            if (model != null) { snappers.Add(new Snapper { x = x, y = y, t = t, jaw = model, model = model, timer = Random.Range(0f, 1.5f) }); return; }
            CbArt.Cylinder(VineGreen, t, new Vector3(0, 0.45f, 0), 0.08f, 0.9f);
            var head = CbArt.Joint(t, 0, 0.95f, 0, "head");
            CbArt.Part(CbMesh.Dome(0.5f, 10), M("#b8323a"), head, Vector3.zero, new Vector3(0.7f, 0.35f, 0.6f), Quaternion.Euler(180, 0, 0));
            var jaw = CbArt.Joint(head, -0.3f, 0, 0, "jaw");
            CbArt.Part(CbMesh.Dome(0.5f, 10), M("#c9483f"), jaw, new Vector3(0.3f, 0, 0), new Vector3(0.7f, 0.35f, 0.6f));
            for (int k = 0; k < 4; k++)
                CbArt.Cone(M("#f4eedc"), jaw, new Vector3(0.1f + k * 0.14f, 0.05f, -0.2f), 0.04f, 0.12f, null, 4);
            snappers.Add(new Snapper { x = x, y = y, t = t, jaw = jaw, timer = Random.Range(0f, 1.5f) });
        }

        void BuildGong()
        {
            var t = CbArt.Joint(root, GongX, 0, 0.4f, "gong");
            bridgeT = CbArt.Joint(root, BridgeFrom, -0.08f, 0.1f, "gongBridge");
            for (float x = 0.3f; x < BridgeTo - BridgeFrom; x += 0.62f)
                CbArt.Box(Wood, bridgeT, new Vector3(x, 0, 0), new Vector3(0.55f, 0.14f, 1.4f)).gameObject.SetActive(false);
            // the model: the golden gong in its wooden frame; struck, it shakes
            gongDisc = Model("Gong", t, new Vector3(2.55f, 2.6f, 0.86f), 180f);
            if (gongDisc != null) return;
            CbArt.Cylinder(Wood, t, new Vector3(-0.8f, 1.3f, 0), 0.1f, 2.6f);
            CbArt.Cylinder(Wood, t, new Vector3(0.8f, 1.3f, 0), 0.1f, 2.6f);
            CbArt.Cylinder(Wood, t, new Vector3(0, 2.6f, 0), 0.1f, 1.8f, Quaternion.Euler(0, 0, 90));
            gongDisc = CbArt.Joint(t, 0, 1.6f, 0, "disc");
            CbArt.Cylinder(new Material(CbArt.Emissive(new Color(0.9f, 0.62f, 0.15f))), gongDisc, Vector3.zero, 0.7f, 0.08f, Quaternion.Euler(90, 0, 0));
        }

        void BuildHive()
        {
            hive = CbArt.Joint(root, HiveX, HiveY, 0f, "beehive");
            hiveY = HiveY;
            // the model: the hive hanging from its own branch
            if (Model("Hive", hive, new Vector3(0.73f, 1.4f, 1.07f), 180f) != null) return;
            CbArt.Cylinder(Wood, root, new Vector3(HiveX, HiveY + 1.6f, 0.2f), 0.2f, 3f, Quaternion.Euler(0, 0, 90));
            CbArt.Part(CbMesh.Blob(8, 6), M("#d9a640"), hive, Vector3.zero, new Vector3(0.6f, 0.8f, 0.6f));
            for (int k = -1; k <= 1; k++)
                CbArt.Part(CbMesh.Torus(0.12f), M("#7a5220"), hive, new Vector3(0, k * 0.22f, 0), new Vector3(0.32f, 0.3f, 0.32f));
            CbArt.Cylinder(VineGreen, hive, new Vector3(0, 1f, 0), 0.03f, 1.2f);
            hiveY = HiveY;
        }

        void AddCage(float x)
        {
            var t = CbArt.Joint(root, x, 0, 0.3f, "cage");
            var model = Model("Cage", t, new Vector3(1.5f, 1.39f, 1.49f), 180f);
            for (int i = 0; i < (model != null ? 0 : 6); i++)
            {
                float a = i / 6f * Mathf.PI * 2;
                CbArt.Cylinder(Bamboo, t, new Vector3(Mathf.Cos(a) * 0.55f, 0.7f, Mathf.Sin(a) * 0.55f), 0.05f, 1.4f);
            }
            if (model == null)
            {
                CbArt.Cylinder(Wood, t, new Vector3(0, 1.45f, 0), 0.65f, 0.12f);
                CbArt.Cylinder(Wood, t, new Vector3(0, 0.05f, 0), 0.65f, 0.1f);
            }
            // a little monkey, a friend of Raja's, locked in by the raiders
            var animal = CbArt.Joint(t, 0, 0.1f, 0, "monkey");
            var fur = M("#7a5232"); var face = M("#e9c9a0");
            CbArt.Part(CbMesh.Sphere, fur, animal, new Vector3(0, 0.35f, 0), new Vector3(0.4f, 0.45f, 0.35f));
            CbArt.Part(CbMesh.Sphere, fur, animal, new Vector3(0, 0.75f, 0), Vector3.one * 0.34f);
            CbArt.Part(CbMesh.Sphere, face, animal, new Vector3(0, 0.73f, -0.13f), new Vector3(0.24f, 0.2f, 0.12f));
            foreach (float s in new[] { -1f, 1f })
                CbArt.Part(CbMesh.Sphere, face, animal, new Vector3(s * 0.19f, 0.8f, 0), Vector3.one * 0.12f);
            // with the model, its own monkey sits inside; this one hops out when the cage breaks
            if (model != null) animal.gameObject.SetActive(false);
            cages.Add(new Cage { x = x, t = t, animal = animal, model = model });
        }

        void BuildMud()
        {
            var t = CbArt.Joint(root, (MudFrom + MudTo) / 2, 0, 0, "mud");
            CbArt.Box(M("#5a4128"), t, new Vector3(0, 0.01f, 0.3f), new Vector3(MudTo - MudFrom, 0.04f, 2.6f));
            for (int k = 0; k < 10; k++)
                CbArt.Part(CbMesh.Dome(0.5f, 8), M("#4a3420"), t, new Vector3(Random.Range(-2.8f, 2.8f), 0.02f, Random.Range(-0.8f, 1.4f)),
                    new Vector3(0.3f, 0.12f, 0.3f));
        }

        void AddRaft(float from, float to, float offset)
        {
            var t = CbArt.Joint(root, from, RaftTop - 0.2f, 0.05f, "raft");
            // the model's top is its deck; the stand-in's logs are centred on the joint
            float baseY = RaftTop - 0.45f;
            if (Model("Raft", t, new Vector3(2.2f, 0.45f, 1.79f), 180f) == null)
            {
                baseY = RaftTop - 0.2f;
                for (int k = 0; k < 4; k++)
                    CbArt.Cylinder(Wood, t, new Vector3(0, 0, -0.6f + k * 0.4f), 0.19f, 2.2f, Quaternion.Euler(0, 0, 90));
                CbArt.Box(VineGreen, t, new Vector3(0.7f, 0.12f, 0), new Vector3(0.08f, 0.06f, 1.5f));
                CbArt.Box(VineGreen, t, new Vector3(-0.7f, 0.12f, 0), new Vector3(0.08f, 0.06f, 1.5f));
            }
            var r = new Raft { from = from, to = to, speed = 1.5f, t = t, baseY = baseY };
            r.x = Mathf.Repeat(offset, to - from) + from;
            rafts.Add(r);
        }

        void BuildRollingLog()
        {
            logT = CbArt.Joint(root, (LogFrom + LogTo) / 2, LogTop - 0.35f, 0.05f, "rollingLog");
            CbArt.Cylinder(Wood, logT, Vector3.zero, 0.35f, LogTo - LogFrom, Quaternion.Euler(0, 0, 90));
            for (float x = -3.5f; x <= 3.6f; x += 1.4f)
                CbArt.Box(M("#5c3a20"), logT, new Vector3(x, 0, -0.3f), new Vector3(0.12f, 0.5f, 0.12f));
        }

        void DressVines(Block b, float x)
        {
            for (float y = 0.5f; y < b.top; y += 0.7f)
                CbArt.Part(CbMesh.Leaf(), LeafMat, root, new Vector3(x - 0.04f, y, -0.6f),
                    Vector3.one * 0.22f, Quaternion.Euler(0, 90, Random.Range(-40f, 40f)), shadows: false);
            CbArt.Cylinder(VineGreen, root, new Vector3(x - 0.05f, b.top / 2, -0.5f), 0.05f, b.top);
            CbArt.Cylinder(VineGreen, root, new Vector3(x - 0.05f, b.top / 2, 0.2f), 0.05f, b.top);
        }

        void BuildSeesaw()
        {
            var t = CbArt.Joint(root, SeesawX, 0, 0.1f, "seesaw");
            plank = CbArt.Joint(t, 0, SeesawTop, 0, "plank");
            // the model: its lashed plank on a carved stone, the plank's top level with SeesawTop (it tips as one)
            var model = CbArt.Joint(plank, 0, -SeesawTop - 0.2f, 0, "seesawModel");
            if (Model("Seesaw", model, new Vector3(4f, SeesawTop + 0.25f, 0.9f), 180f) == null)
            {
                Object.Destroy(model.gameObject);
                CbArt.Cone(StoneGrey, t, new Vector3(0, 0.2f, 0), 0.35f, 0.4f, null, 4);
                CbArt.Box(Wood, plank, new Vector3(0, -0.06f, 0), new Vector3(4f, 0.12f, 0.9f));
            }
            CbArt.Cylinder(Wood, root, new Vector3(SeesawX + 2.6f, 3.4f, 0.4f), 0.15f, 6.8f);
            CbArt.Cylinder(Wood, root, new Vector3(SeesawX + 2.1f, 6.8f, 0.4f), 0.1f, 1.2f, Quaternion.Euler(0, 0, 90));
            stone = CbArt.Joint(root, SeesawX + 1.6f, 6.3f, 0.1f, "stone");
            if (CbArt.Prop("Cloudborne/Jungle/Boulder/Boulder", stone, CbArt.PropOutline) is Transform b) b.localScale *= 0.5f;
            else CbArt.Part(CbMesh.Blob(6, 4), StoneGrey, stone, Vector3.zero, Vector3.one * 0.9f);
            CbArt.Cylinder(VineGreen, stone, new Vector3(0, 0.6f, 0), 0.03f, 0.6f);
        }

        void AddLift(float x, float lo, float hi)
        {
            var deck = CbArt.Joint(root, x, lo, 0.05f, "lift");
            // the model: a deck of lashed planks, ropes from its corners to a ring above
            var lift = CbArt.Joint(deck, 0, -0.25f, 0, "liftModel");
            if (Model("Lift", lift, new Vector3(1.7f, 2f, 1.5f), 180f) == null)
            {
                Object.Destroy(lift.gameObject);
                CbArt.Box(Wood, deck, new Vector3(0, -0.1f, 0), new Vector3(1.6f, 0.2f, 1.2f));
            }
            CbArt.Cylinder(Wood, root, new Vector3(x, hi + 1.6f, 0.3f), 0.35f, 0.2f, Quaternion.Euler(90, 0, 0));
            var weight = CbArt.Joint(root, x + 1.2f, hi, 0.3f, "counterweight");
            CbArt.Part(CbMesh.Blob(6, 4), StoneGrey, weight, Vector3.zero, Vector3.one * 0.7f);
            lifts.Add(new Lift { x = x, half = 0.8f, top = lo, lo = lo, hi = hi, deck = deck, weight = weight });
        }

        void BuildWaterfall(float x0, float x1, float top)
        {
            // the cave behind: dark rock; the falling water in front, light and see-through, hiding the ledges a little
            var cave = RockMat(x1 - x0, top, x0, 0, new Color(0.42f, 0.42f, 0.48f));
            CbArt.Part(CbMesh.Quad, cave, root, new Vector3((x0 + x1) / 2, top / 2, 1.2f), new Vector3(x1 - x0, top, 1), shadows: false).name = "cave";
            var water = CbArt.Glow(new Color(0.55f, 0.8f, 1f) * 0.55f, Resources.Load<Texture2D>("Cloudborne/Jungle/Water/Water"));
            var sheet = CbArt.Part(CbMesh.Quad, water, root, new Vector3((x0 + x1) / 2, top / 2, -1.3f), new Vector3(x1 - x0, top, 1), shadows: false);
            sheet.name = "waterfall";
            waterfallMat = water;
            waterfall = new Rect(x0, 0, x1 - x0, top);
        }
        Material waterfallMat;
        Rect waterfall;

        void BuildChest()
        {
            var t = CbArt.Joint(root, ChestX, 0, 0.2f, "chest");
            // the model: opened, it glows and bursts with what was inside
            chestModel = Model("Chest", t, new Vector3(1f, 0.81f, 0.88f), 180f);
            if (chestModel != null)
            {
                blocks.Add(new Block { x0 = ChestX - 0.5f, x1 = ChestX + 0.5f, top = 0.81f, t = t });
                return;
            }
            CbArt.Box(Wood, t, new Vector3(0, 0.3f, 0), new Vector3(1f, 0.6f, 0.7f));
            CbArt.Box(M("#d9a640"), t, new Vector3(0, 0.3f, -0.36f), new Vector3(0.18f, 0.25f, 0.04f));
            chestLid = CbArt.Joint(t, 0, 0.6f, 0.35f, "lid");
            CbArt.Box(M("#9a6a3a"), chestLid, new Vector3(0, 0.1f, -0.35f), new Vector3(1.04f, 0.2f, 0.74f));
            blocks.Add(new Block { x0 = ChestX - 0.5f, x1 = ChestX + 0.5f, top = 0.6f, t = t });
        }

        // ---------- what the hero's physics asks ----------

        void WaysSupport(float x, float fromY, ref float best)
        {
            // an open pit: nothing under him
            foreach (var p in pits)
                if (p.open && x > p.x0 && x < p.x1 && best <= 0) best = Deep;
            // the gong's bridge, laid plank by plank from the near bank
            if (bridge > 0 && x > BridgeFrom - HalfWidth * 0.5f && x < BridgeFrom + (BridgeTo - BridgeFrom) * bridge + HalfWidth * 0.5f && 0 <= fromY + StepUp && best < 0) best = 0;
            foreach (var r in rafts)
                if (r.sink <= 0 && Mathf.Abs(x - r.x) < 1.1f + HalfWidth * 0.5f && RaftTop <= fromY + StepUp && RaftTop > best) best = RaftTop;
            if (x > LogFrom - HalfWidth * 0.5f && x < LogTo + HalfWidth * 0.5f && LogTop <= fromY + StepUp && LogTop > best) best = LogTop;
            foreach (var l in leaves)
                if (Mathf.Abs(x - l.x) < l.half && l.top <= fromY + 0.05f && l.top > best) best = l.top;
            foreach (var l in lifts)
                if (Mathf.Abs(x - l.x) < l.half + HalfWidth * 0.5f && l.top <= fromY + 0.05f && l.top > best) best = l.top;
            if (Mathf.Abs(x - SeesawX) < 2f && SeesawTop <= fromY + StepUp && SeesawTop > best) best = SeesawTop;
        }

        /// <summary>Not a place to come back to: a pit's cover, a raft, the spinning log.</summary>
        bool WaysSafe(float x, float y)
        {
            foreach (var p in pits) if (x > p.x0 - HalfWidth && x < p.x1 + HalfWidth) return false;
            if (x > LogFrom - 0.5f && x < LogTo + 0.5f) return false;
            foreach (var r in rafts) if (Mathf.Abs(x - r.x) < 1.6f && y < 1f && InWater(x)) return false;
            return true;
        }

        /// <summary>
        /// The stretch of open path around x a soldier may roam: to the water, a pit or something solid on either
        /// side (as far as reach), so he stops where the path stops, not at a line no one can see.
        /// </summary>
        public void FreeStretch(float x, float reach, out float lo, out float hi)
        {
            lo = x - reach; hi = x + reach;
            foreach (var (x0, x1) in Gaps)
            {
                if (x1 <= x) lo = Mathf.Max(lo, x1 + 0.6f);
                if (x0 >= x) hi = Mathf.Min(hi, x0 - 0.6f);
            }
            foreach (var b in blocks)
            {
                if (b.broken || InWater((b.x0 + b.x1) / 2)) continue;
                if (b.x1 <= x) lo = Mathf.Max(lo, b.x1 + 0.6f);
                if (b.x0 >= x) hi = Mathf.Min(hi, b.x0 - 0.6f);
            }
        }

        /// <summary>The vine whose end is within reach of his hands at (hx, hy), not one he just let go of.</summary>
        public Vine VineNear(float hx, float hy)
        {
            foreach (var v in vines)
            {
                if (v.cool > 0) continue;
                float ex = v.ax + Mathf.Sin(v.angle) * v.len, ey = v.ay - Mathf.Cos(v.angle) * v.len;
                // anywhere along its lower half
                for (float k = 0.5f; k <= 1.001f; k += 0.25f)
                {
                    float px = Mathf.Lerp(v.ax, ex, k), py = Mathf.Lerp(v.ay, ey, k);
                    if (Mathf.Abs(hx - px) < 0.75f && Mathf.Abs(hy - py) < 0.9f) return v;
                }
            }
            return null;
        }

        /// <summary>The bar his hands reach at (x, hy).</summary>
        public Bar BarNear(float x, float hy)
        {
            foreach (var b in bars) if (x > b.x0 && x < b.x1 && Mathf.Abs(hy - b.y) < 0.5f) return b;
            return null;
        }

        /// <summary>The zipline his hands reach at (x, hy), not at its very end.</summary>
        public Zip ZipNear(float x, float hy)
        {
            foreach (var z in zips)
            {
                float lo = Mathf.Min(z.x0, z.x1), hi = Mathf.Max(z.x0, z.x1);
                if (x > lo + 0.3f && x < hi - 1f && Mathf.Abs(hy - z.At(x)) < 0.55f) return z;
            }
            return null;
        }

        /// <summary>Whether, pressing towards dir at (x, y), he is at a vine-grown side to climb (and below its top).</summary>
        public bool VineWallAt(float x, float y, int dir, out float edgeX, out float top)
        {
            edgeX = top = 0;
            foreach (var b in blocks)
            {
                if (b.broken || b.vines == 0 || b.vines != -dir) continue;
                float side = dir > 0 ? b.x0 - HalfWidth : b.x1 + HalfWidth;
                if (Mathf.Abs(x - side) < 0.12f && y < b.top - 0.4f) { edgeX = dir > 0 ? b.x0 : b.x1; top = b.top; return true; }
            }
            return false;
        }

        /// <summary>An updraft's lift at (x, y): it carries him up in the air.</summary>
        public float LiftAt(float x, float y)
        {
            foreach (var d in drafts) if (x > d.x0 && x < d.x1 && y > d.y0 - 0.5f && y < d.y1) return 44f;
            return 0;
        }

        /// <summary>Landing on a springy leaf at (x, y): how hard it throws him up (0 if not on one).</summary>
        public float BounceAt(float x, float y)
        {
            foreach (var l in leaves)
                if (Mathf.Abs(x - l.x) < l.half && Mathf.Abs(y - l.top) < 0.05f) { l.squash = 1; return LeafBounce; }
            return 0;
        }

        public static bool MudAt(float x, float y) => y < 0.05f && x > MudFrom && x < MudTo;

        /// <summary>On the spinning log at (x, y): it rolls him back (and he walks it as a tightrope).</summary>
        public static bool OnRollingLog(float x, float y) => x > LogFrom && x < LogTo && Mathf.Abs(y - LogTop) < 0.05f;

        /// <summary>
        /// Raja on the ground at (x, y) pressing towards dir against the stone block: it slides ahead of him, as far as
        /// the gate. True while it moves.
        /// </summary>
        public bool Push(float x, float y, int dir, float dt)
        {
            var b = pushBlock;
            if (b == null || y > 0.05f) return false;
            float side = dir > 0 ? b.x0 - HalfWidth : b.x1 + HalfWidth;
            if (Mathf.Abs(x - side) > 0.08f) return false;
            float step = dir * 1.6f * dt;
            float nx0 = b.x0 + step, nx1 = b.x1 + step;
            if (nx0 < pushHome - 1.6f || nx1 > 283.5f) return false;
            b.x0 = nx0; b.x1 = nx1;
            b.t.localPosition = new Vector3((b.x0 + b.x1) / 2, 0, 0);
            if (Random.value < 0.2f) CbFx.Spark(new Vector3(dir > 0 ? b.x0 : b.x1, 0.1f, -0.3f), CbFx.Dust, 1, 1.5f, 0.3f, 0.35f, -1);
            return true;
        }

        // ---------- blows ----------

        bool InReach(float tx, float x, int dir, float range) { float d = (tx - x) * dir; return d > -0.5f && d < range + 0.6f; }

        bool StrikeWays(float x, float y, int dir, float range, bool staff)
        {
            bool any = false;
            foreach (var th in thorns)
            {
                if (th.b.broken || !InReach(dir > 0 ? th.b.x0 : th.b.x1, x, dir, range) || y > th.b.top) continue;
                any = true;
                // paws clang off them (a hint); a blow lands every frame of its hit window: count each once
                if (!staff)
                {
                    if (th.clang <= 0) { th.clang = 0.25f; g.hud.SetTip("Thorns! Cut them with the staff (<color=#ffb03a>HEAVY</color>)"); CbFx.Sfx("block", 0.6f); }
                    continue;
                }
                if (th.cool > 0) continue;
                th.cool = 0.25f;
                th.hp--;
                CbFx.Spark(new Vector3((th.b.x0 + th.b.x1) / 2, 0.8f, -0.4f), new Color(0.3f, 0.6f, 0.25f) * 1.5f, 14, 5, 0.35f, 0.5f, 6);
                CbFx.Sfx("hit", 0.7f);
                if (th.hp <= 0) { th.b.broken = true; th.b.t.gameObject.SetActive(false); CbFx.Puff(new Vector3((th.b.x0 + th.b.x1) / 2, 0.6f, -0.3f), new Color(0.5f, 0.7f, 0.4f), 6, 1.6f, 0.8f, 0.5f); }
            }
            if (!lever && InReach(LeverX, x, dir, range) && y < 1.5f)
            {
                lever = true; any = true;
                if (leverModel) leverHandle.localScale = new Vector3(-1, 1, 1);
                else leverHandle.localRotation = Quaternion.Euler(0, 0, -35);
                gates[0].wantOpen = true;
                CbFx.Sfx("block", 0.8f);
                g.hud.SetTip("The gate opens!");
            }
            if (!gong && InReach(GongX, x, dir, range) && y < 2f)
            {
                gong = true; any = true;
                CbVfx.Ring(new Vector3(GongX, 1.6f, -0.5f), CbFx.Flame * 0.6f, 2.6f, 0.5f);
                CbFx.Sfx("heavy");
                g.shake = Mathf.Max(g.shake, 0.3f);
                g.hud.SetTip("BONG! A bridge lays itself across the gorge");
            }
            if (hiveFall < 0 && InReach(HiveX, x, dir, range) && Mathf.Abs(y + 1.2f - HiveY) < 1.6f)
            {
                hiveFall = 0; any = true;
                CbFx.Sfx("hit");
                g.hud.SetTip("The bees are angry - at the raiders!");
            }
            foreach (var c in cages)
            {
                if (c.freed || !InReach(c.x, x, dir, range) || y > 1.4f) continue;
                any = true;
                if (c.cool > 0) continue;
                c.cool = 0.25f;
                c.hp--;
                CbFx.Spark(new Vector3(c.x, 0.8f, -0.4f), CbFx.Dust, 8, 4, 0.35f, 0.4f, 6);
                CbFx.Sfx("hit", 0.6f);
                c.t.localRotation = Quaternion.Euler(0, 0, dir * 4f);
                if (c.hp <= 0) FreeCage(c);
            }
            foreach (var s in snappers)
            {
                if (s.dead || !InReach(s.x, x, dir, range) || Mathf.Abs(y - s.y) > 1.5f) continue;
                any = true;
                if (s.cool > 0) continue;
                s.cool = 0.25f;
                s.hp--;
                CbFx.Spark(new Vector3(s.x, s.y + 1f, -0.4f), new Color(0.8f, 0.3f, 0.3f) * 1.5f, 10, 4, 0.3f, 0.4f, 6);
                CbFx.Sfx("hit", 0.6f);
                if (s.hp <= 0) { s.dead = true; s.t.gameObject.SetActive(false); CbFx.Puff(new Vector3(s.x, s.y + 0.8f, -0.3f), new Color(0.6f, 0.5f, 0.4f), 6, 1.6f, 0.7f, 0.5f); }
            }
            // the seesaw's rope post: the stone drops onto the plank's far end
            if (seesawT < 0 && InReach(SeesawX + 2.6f, x, dir, range) && y < 2f)
            {
                seesawT = 0; any = true;
                CbFx.Sfx("block", 0.6f);
            }
            foreach (var b in boulders)
            {
                if (!b.rolling || !staff || !InReach(b.x, x, dir, range) || y > 1.5f) continue;
                any = true;
                SmashBoulder(b);
            }
            return any;
        }

        void FreeCage(Cage c)
        {
            c.freed = true;
            for (int i = 0; i < c.t.childCount; i++) c.t.GetChild(i).gameObject.SetActive(c.t.GetChild(i) == c.animal);
            CbFx.Puff(new Vector3(c.x, 0.8f, -0.3f), new Color(0.85f, 0.76f, 0.6f), 7, 1.6f, 0.7f, 0.45f);
            CbFx.Sfx("pickup");
            g.score += 300;
            g.hud.Popup(new Vector3(c.x, 2f, 0), "FRIEND FREED!", CbHud.PopStyle.Normal);
            g.hud.SetTip($"A friend freed! ({CagesFreed}/{cages.Count})");
            c.hop = 0.01f;
        }

        void SmashBoulder(Boulder b)
        {
            b.rolling = false; b.cool = 3f;
            if (b.t != null) b.t.gameObject.SetActive(false);
            CbFx.Puff(new Vector3(b.x, 0.6f, -0.4f), new Color(0.78f, 0.74f, 0.66f), 8, 2f, 1f, 0.6f);
            CbFx.Spark(new Vector3(b.x, 0.6f, -0.4f), CbFx.Grey, 10, 6, 0.3f, 0.4f, 12);
            CbFx.Sfx("heavy", 0.8f);
        }

        // ---------- every frame ----------

        void UpdateWays(float dt, CbHero hero)
        {
            // lanterns light as he passes: the place he comes back to
            foreach (var l in lanterns)
                if (!l.lit && hero.x > l.x - 0.5f && hero.x < l.x + 6f)
                {
                    l.lit = true;
                    CbArt.SetGlow(l.light, new Color(1f, 0.7f, 0.3f) * 3f);
                    if (l.window != null) l.window.enabled = true;
                    CbFx.Spark(new Vector3(l.x, 1.4f, 0.8f), CbFx.Flame, 12, 3, 0.3f, 0.5f, -3);
                    g.Checkpoint(l.x);
                }

            // vines sway; the one he swings on follows him (CbHero sets its angle)
            foreach (var v in vines)
            {
                v.cool = Mathf.Max(0, v.cool - dt);
                if (hero.SwingingOn != v) { v.angle = Mathf.Sin(Time.time * 0.9f + v.ax) * 0.12f; v.speed = 0; }
                v.t.localRotation = Quaternion.Euler(0, 0, v.angle * Mathf.Rad2Deg);
            }
            foreach (var l in leaves)
            {
                l.squash = Mathf.MoveTowards(l.squash, 0, dt * 4f);
                l.t.localScale = new Vector3(1 + l.squash * 0.15f, 1 - l.squash * 0.4f, 1);
            }
            foreach (var d in drafts)
                if (Mathf.Abs(hero.x - (d.x0 + d.x1) / 2) < 14f && Random.value < 0.5f)
                    CbFx.Spark(new Vector3(Random.Range(d.x0, d.x1), Random.Range(d.y0, d.y0 + 3f), -0.3f), new Color(0.85f, 0.95f, 1f) * 1.4f, 1, 0, 0.25f, 1.6f, -6);

            foreach (var th in thorns) { th.cool = Mathf.Max(0, th.cool - dt); th.clang = Mathf.Max(0, th.clang - dt); }
            foreach (var c in cages) c.cool = Mathf.Max(0, c.cool - dt);
            foreach (var sn in snappers) sn.cool = Mathf.Max(0, sn.cool - dt);
            UpdateTraps(dt, hero);
            UpdateMovers(dt, hero);

            // the gong's bridge, plank by plank
            if (gong && bridge < 1)
            {
                bridge = Mathf.MoveTowards(bridge, 1, dt / 1.6f);
                int shown = Mathf.CeilToInt(bridge * bridgeT.childCount);
                for (int i = 0; i < bridgeT.childCount; i++) bridgeT.GetChild(i).gameObject.SetActive(i < shown);
                if (gongDisc != null) gongDisc.localRotation = Quaternion.Euler(0, 180, Mathf.Sin(Time.time * 30) * 2.5f * (1 - bridge));
            }
            // gates slide up when opened; the second opens while its plate is pressed
            bool pressed = (Mathf.Abs(hero.x - PlateX) < 0.6f && hero.y < 0.1f) || (pushBlock.x0 < PlateX && pushBlock.x1 > PlateX);
            if (gates.Count > 1) gates[1].wantOpen = pressed || (gates[1].open > 0 && Mathf.Abs(hero.x - (gates[1].b.x0 + gates[1].b.x1) / 2) < 0.8f);
            foreach (var gt in gates)
            {
                float was = gt.open;
                gt.open = Mathf.MoveTowards(gt.open, gt.wantOpen ? 1 : 0, dt * 1.5f);
                gt.b.t.localPosition = gt.home + Vector3.up * gt.open * (gt.sinks ? -3.7f : 3.4f);
                if (gt.sinks && gt.open != was && Random.value < 0.5f)
                    CbFx.Puff(gt.home + new Vector3(Random.Range(-1.4f, 1.4f), 0.2f, -0.2f), new Color(0.8f, 0.75f, 0.65f), 1, 1f, 0.8f, 0.6f);
                gt.b.broken = gt.open > 0.8f;
            }
            // the beehive: it falls, and the bees go for the raiders near it
            if (hiveFall >= 0)
            {
                hiveFall += dt;
                if (hiveY > 0.4f) { hiveY = Mathf.Max(0.4f, hiveY - dt * (2 + hiveFall * 20)); hive.localPosition = new Vector3(HiveX, hiveY, 0); }
                else if (bees < 7f)
                {
                    bees += dt;
                    beeTick -= dt;
                    if (Random.value < 0.8f) CbFx.Spark(new Vector3(HiveX + Random.Range(-2.5f, 2.5f), Random.Range(0.6f, 2.2f), -0.4f), new Color(1f, 0.85f, 0.2f) * 2f, 1, 3, 0.15f, 0.4f, 0);
                    if (beeTick <= 0)
                    {
                        beeTick = 0.4f;
                        foreach (var e in g.enemies.ToArray())
                            if (!e.dead && Mathf.Abs(e.x - HiveX) < 3.8f) g.HurtEnemy(e, 3, e.x >= HiveX ? 1 : -1, 0.5f, 0, false, false, false, false);
                    }
                }
            }
            // a freed friend hops off into the jungle
            foreach (var c in cages)
                if (c.hop > 0 && c.hop < 2f)
                {
                    c.hop += dt;
                    c.animal.localPosition = new Vector3(c.hop * 2f, 0.1f + Mathf.Abs(Mathf.Sin(c.hop * 9)) * 0.6f, c.hop * 2.5f);
                    if (c.hop >= 2f) c.animal.gameObject.SetActive(false);
                }
            // the chest: the key opens it, a tiger token inside
            if (!chestOpen && Mathf.Abs(hero.x - ChestX) < 1.3f && hero.y < 1f)
            {
                if (g.HasKey)
                {
                    chestOpen = true;
                    if (chestLid != null) chestLid.localRotation = Quaternion.Euler(-70, 0, 0);
                    if (chestModel != null) { chestModel.localScale = Vector3.one * 1.06f; CbVfx.Ring(new Vector3(ChestX, 0.6f, -0.5f), CbFx.Flame * 0.6f, 1.6f, 0.4f); }
                    g.items.Spawn(CbItems.Kind.Token, new Vector3(ChestX, 1.2f, 0), resting: true);
                    for (int k = 0; k < 3; k++) g.items.Spawn(CbItems.Kind.Coin, new Vector3(ChestX, 0.8f, 0));
                    CbFx.Spark(new Vector3(ChestX, 0.8f, -0.4f), CbFx.Flame, 16, 4, 0.35f, 0.5f, -3);
                    CbFx.Sfx("pickup");
                }
                else if (!chestHinted) { chestHinted = true; g.hud.SetTip("A locked chest. Its key is up in the treetops..."); }
            }
            if (waterfallMat != null) waterfallMat.mainTextureOffset = new Vector2(0, Time.time * 1.2f);
            if (Mathf.Abs(hero.x - waterfall.center.x) < 12f && Random.value < 0.6f)
                CbFx.Spark(new Vector3(Random.Range(waterfall.xMin, waterfall.xMax), 0.3f, -1.4f), CbFx.Water, 1, 2, 0.3f, 0.5f, 4);
        }
        bool chestHinted;

        void UpdateTraps(float dt, CbHero hero)
        {
            // thorns prick him if he walks into them
            foreach (var th in thorns)
                if (!th.b.broken && hero.y < th.b.top && hero.x > th.b.x0 - HalfWidth - 0.05f && hero.x < th.b.x1 + HalfWidth + 0.05f)
                    hero.TakeHit(6, hero.x < (th.b.x0 + th.b.x1) / 2 ? -1 : 1);
            // pits: stood on, the leaves give way; falling in, spikes
            foreach (var p in pits)
            {
                if (!p.open)
                {
                    bool on = hero.x > p.x0 && hero.x < p.x1 && hero.y < 0.05f;
                    p.stood = on ? p.stood + dt : 0;
                    if (p.stood > 0.22f)
                    {
                        p.open = true;
                        p.cover.gameObject.SetActive(false);
                        CbFx.Puff(new Vector3((p.x0 + p.x1) / 2, 0.2f, -0.3f), new Color(0.5f, 0.7f, 0.4f), 6, 1.4f, 0.8f, 0.5f);
                        CbFx.Sfx("hit", 0.6f);
                    }
                }
                else if (hero.x > p.x0 - 0.2f && hero.x < p.x1 + 0.2f && hero.y < -1.6f) hero.FellInPit();
            }
            // the dart statue: a dart every 2.4 s along the path at chest height (duck under, jump over)
            dartT -= dt;
            if (dartT <= 0 && Mathf.Abs(hero.x - StatueX) < 16f)
            {
                dartT = 2.4f;
                var t = CbArt.Cylinder(M("#3a2a1a"), root, new Vector3(StatueX - 0.5f, DartY, 0), 0.03f, 0.5f, Quaternion.Euler(0, 0, 90));
                darts.Add(new Dart { x = StatueX - 0.5f, y = DartY, vx = -9f, t = t });
                CbFx.Sfx("dash", 0.4f);
            }
            for (int i = darts.Count - 1; i >= 0; i--)
            {
                var d = darts[i];
                d.x += d.vx * dt; d.gone += Mathf.Abs(d.vx) * dt;
                d.t.localPosition = new Vector3(d.x, d.y, 0);
                float topOfHim = hero.Ducking ? 0.95f : 1.75f;
                bool hit = Mathf.Abs(hero.x - d.x) < 0.35f && d.y > hero.y + 0.15f && d.y < hero.y + topOfHim;
                if (hit) hero.TakeHit(6, d.vx > 0 ? 1 : -1);
                if (hit || d.gone > 14f) { Object.Destroy(d.t.gameObject); darts.RemoveAt(i); }
            }
            // swinging logs
            foreach (var p in pendulums)
            {
                float a = Mathf.Sin(Time.time * Mathf.PI * 2 / p.period + p.phase) * 0.75f;
                p.t.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
                float lx = p.x + Mathf.Sin(a) * p.len, ly = p.y - Mathf.Cos(a) * p.len;
                if (Mathf.Abs(hero.x - lx) < 0.6f && hero.y < ly + 0.35f && hero.y + 1.7f > ly - 0.35f)
                    hero.TakeHit(8, Mathf.Cos(Time.time * Mathf.PI * 2 / p.period + p.phase) >= 0 ? 1 : -1, knock: 4f);
            }
            // snapping plants: open, then a snap
            foreach (var s in snappers)
            {
                if (s.dead) continue;
                float before = s.timer;
                s.timer = Mathf.Repeat(s.timer + dt, s.period);
                bool snapping = s.timer > s.period - 0.35f;
                if (s.model != null) s.model.localScale = snapping ? new Vector3(1.15f, 0.82f, 1.15f) : Vector3.one * (1 + Mathf.Sin(Time.time * 4 + s.x) * 0.02f);
                else s.jaw.localRotation = Quaternion.Euler(0, 0, snapping ? 0 : -50f * Mathf.Clamp01(s.timer * 3));
                if (before < s.period - 0.35f && snapping && Mathf.Abs(hero.x - s.x) < 1.05f && hero.y < s.y + 1.3f && hero.y > s.y - 0.5f)
                    hero.TakeHit(10, hero.x >= s.x ? 1 : -1);
            }
            // rolling boulders down the path past the waterfall
            boulderT -= dt;
            foreach (var b in boulders)
            {
                b.cool = Mathf.Max(0, b.cool - dt);
                if (!b.rolling && b.cool <= 0 && hero.x > 451f && hero.x < b.x - 2f && boulderT <= 0)
                {
                    b.rolling = true; b.vx = -6f; boulderT = 3.2f;
                    if (b.t == null)
                    {
                        b.t = CbArt.Joint(root, b.x, 0.6f, 0f, "rollingBoulder");
                        if (CbArt.Prop("Cloudborne/Jungle/Boulder/Boulder", b.t, CbArt.PropOutline) is Transform m) m.localScale *= 0.75f;
                        else CbArt.Part(CbMesh.Blob(6, 4), StoneGrey, b.t, Vector3.zero, Vector3.one * 1.2f);
                    }
                    b.t.gameObject.SetActive(true);
                    b.t.localPosition = new Vector3(466f, 0.6f, 0);
                    b.x = 466f;
                }
                if (!b.rolling) continue;
                b.x += b.vx * dt;
                b.t.localPosition = new Vector3(b.x, 0.6f, 0);
                b.t.localRotation *= Quaternion.Euler(0, 0, -b.vx * dt * 95f);
                if (Mathf.Abs(hero.x - b.x) < 0.9f && hero.y < 1.1f) { hero.TakeHit(10, -1, knock: 5f); SmashBoulder(b); }
                else if (b.x < 450f) { b.rolling = false; b.cool = 0.5f; b.t.gameObject.SetActive(false); b.x = 466f; }
            }
        }

        void UpdateMovers(float dt, CbHero hero)
        {
            // rafts drift down the river; at its end they sink and come back at the start
            foreach (var r in rafts)
            {
                bool on = Mathf.Abs(hero.x - r.x) < 1.1f + HalfWidth * 0.5f && Mathf.Abs(hero.y - RaftTop) < 0.05f && r.sink <= 0;
                if (r.sink > 0)
                {
                    r.sink += dt;
                    if (r.sink > 1.2f) { r.sink = 0; r.x = r.from; }
                }
                else
                {
                    r.x += r.speed * dt;
                    if (on) hero.x += r.speed * dt;
                    if (r.x > r.to) r.sink = 0.01f;
                }
                r.t.localPosition = new Vector3(r.x, r.baseY - r.sink * 1.2f + Mathf.Sin(Time.time * 2 + r.from) * 0.03f, 0.05f);
            }
            // the spinning log rolls him back
            logSpin += dt * 160f;
            if (logT != null) logT.localRotation = Quaternion.Euler(logSpin, 0, 0);
            hero.onLog = OnRollingLog(hero.x, hero.y);
            if (hero.onLog && hero.state != CbHero.State.Dash) hero.x -= LogRollBack * dt;
            // lifts rise while he stands on them, and sink back
            foreach (var l in lifts)
            {
                bool on = Mathf.Abs(hero.x - l.x) < l.half + HalfWidth * 0.5f && Mathf.Abs(hero.y - l.top) < 0.05f;
                float was = l.top;
                l.top = Mathf.MoveTowards(l.top, on ? l.hi : l.lo, dt * 1.8f);
                if (on) hero.y = l.top;
                l.deck.localPosition = new Vector3(l.x, l.top, 0.05f);
                l.weight.localPosition = new Vector3(l.x + 1.2f, l.hi + l.lo - l.top, 0.3f);
                if (on && l.top != was && Random.value < 0.1f) CbFx.Sfx("talk", 0.3f);
            }
            // the seesaw: the stone drops on its far end, and whoever stands on the near end flies
            if (seesawT >= 0)
            {
                seesawT += dt;
                float y = Mathf.Max(SeesawTop + 0.5f, 6.3f - seesawT * seesawT * 15f);
                stone.localPosition = new Vector3(SeesawX + 1.6f, y, 0.1f);
                bool landed = y <= SeesawTop + 0.5f;
                plank.localRotation = Quaternion.Euler(0, 0, landed ? -14f : 0);
                if (landed && !seesawLaunched)
                {
                    seesawLaunched = true;
                    g.shake = Mathf.Max(g.shake, 0.3f);
                    CbFx.Sfx("heavy");
                    if (hero.x > SeesawX - 2f && hero.x < SeesawX - 0.4f && Mathf.Abs(hero.y - SeesawTop) < 0.1f) hero.Launch(7.5f, 19.5f);
                }
                if (seesawT > 3f) { seesawT = -1; seesawLaunched = false; plank.localRotation = Quaternion.identity; stone.localPosition = new Vector3(SeesawX + 1.6f, 6.3f, 0.1f); }
            }
        }
        bool seesawLaunched;

        void ResetWays()
        {
            foreach (var l in lanterns) { l.lit = false; CbArt.SetGlow(l.light, new Color(0.15f, 0.12f, 0.1f)); if (l.window != null) l.window.enabled = false; }
            foreach (var th in thorns) { th.hp = 2; th.b.broken = false; th.b.t.gameObject.SetActive(true); }
            foreach (var p in pits) { p.open = false; p.stood = 0; p.cover.gameObject.SetActive(true); }
            foreach (var d in darts) Object.Destroy(d.t.gameObject);
            darts.Clear();
            foreach (var s in snappers) { s.dead = false; s.hp = 3; s.t.gameObject.SetActive(true); }
            foreach (var c in cages)
            {
                c.freed = false; c.hp = 3; c.hop = 0;
                for (int i = 0; i < c.t.childCount; i++) c.t.GetChild(i).gameObject.SetActive(c.model == null || c.t.GetChild(i) != c.animal);
                c.animal.localPosition = new Vector3(0, 0.1f, 0);
                c.t.localRotation = Quaternion.identity;
            }
            foreach (var b in boulders) { b.rolling = false; b.cool = 0; b.x = 466f; if (b.t != null) b.t.gameObject.SetActive(false); }
            lever = gong = chestOpen = chestHinted = seesawLaunched = false;
            if (leverModel) leverHandle.localScale = Vector3.one;
            else leverHandle.localRotation = Quaternion.Euler(0, 0, 35);
            bridge = 0;
            for (int i = 0; i < bridgeT.childCount; i++) bridgeT.GetChild(i).gameObject.SetActive(false);
            foreach (var gt in gates) { gt.open = 0; gt.wantOpen = false; gt.b.broken = false; gt.b.t.localPosition = gt.home; }
            pushBlock.x0 = pushHome - 0.5f; pushBlock.x1 = pushHome + 0.5f; pushBlock.t.localPosition = new Vector3(pushHome, 0, 0);
            hiveFall = -1; hiveY = HiveY; bees = 0; hive.localPosition = new Vector3(HiveX, HiveY, 0);
            if (chestLid != null) chestLid.localRotation = Quaternion.identity;
            if (chestModel != null) chestModel.localScale = Vector3.one;
            seesawT = -1;
            foreach (var l in lifts) l.top = l.lo;
            foreach (var r in rafts) r.sink = 0;
        }
    }
}
