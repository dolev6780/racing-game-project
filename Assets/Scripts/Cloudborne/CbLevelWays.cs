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
    /// <item>The temple of traps (240-292): thorns cut with the staff, a dart statue, a hidden pit, swinging logs, a
    /// lever's gate, a stone block pushed onto a plate for the second gate.</item>
    /// <item>After the temple (292-316): two raiders, a cage to break open.</item>
    /// <item>The raft river (316-372): rafts drifting down a long river, a rock island in the middle.</item>
    /// (The snapping plants are gone: the user saw no point in them.)
    /// <item>The canopy and the waterfall (372-444): a vine-grown pillar, an updraft, ledges and bars in the treetops
    /// with a key, a zipline down, the waterfall's vine-grown cliff with a cave behind the water (a token),
    /// a chest the key opens (a token). (The rolling boulders are gone: they added nothing, the user.)</item>
    /// <item>The ridge (444-490): a shaft to wall-jump up, a cage.</item>
    /// (The gong, the mud, the spinning log, the seesaw and the lift are gone: a muddle of gadgets, the user.)
    /// </list>
    /// Stone lanterns light up as he passes: falling, he comes back at the last one (CbGame).
    /// Everything is built in code from simple shapes and the jungle's models until drawings are approved.
    /// </summary>
    public partial class CbLevel
    {
        /// <summary>The temple's hidden pits (x from, to): leaves over a pit of spikes.</summary>
        // (the temple's hidden pit is gone: the user saw no point in it)
        public static readonly (float x0, float x1)[] Pits = { };
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
        class Lantern { public float x; public bool lit; public Material light; public Renderer window; }
        class Gate { public Block b; public float open; public bool wantOpen, sinks; public Vector3 home; }
        class Thorns { public Block b; public int hp = 2; public float cool, clang; }
        class Pendulum { public float x, y, len, period, phase; public Transform ram, ropeA, ropeB; }
        class Pit { public float x0, x1, stood; public bool open; public Transform cover; }
        class Dart { public float x, y, vx, gone; public Transform t; }
        // the caged monkey turned to the camera and a little toward the way Raja comes; how long it hops for joy when freed
        const float MonkeyFacing = 205f, MonkeyCheer = 1.2f;
        class Cage { public float x; public int hp = 3; public bool freed; public Transform t, animal, model; public float hop, cool; public CbModel monkey; }

        const float LeafBounce = 19.5f, RaftTop = 0.1f;
        public const float TrunkX = 24.5f;
        public static readonly float[] LeafXs = { 28.6f, 176f, 380.2f, 475.5f };
        const string ClimbVinePath = "Cloudborne/Jungle/ClimbVine/ClimbVine";
        const float StatueX = 251.5f, PlateX = 284.5f;
        // the race in the temple (its traps were too easy: the user): the lever opens the gate past the swinging logs
        // and the low log for LeverOpenTime seconds only, then it shuts and the lever springs back
        public const float LeverX = 259.5f, RaceGateX = 274.5f, RaceLogX = 271.4f, LeverOpenTime = 6f;
        const float ChestX = 441.5f;
        // the darts fly from the guardian's mouth: over a ducking Raja, into a standing one
        const float DartY = 1.35f;

        public readonly List<Vine> vines = new List<Vine>();
        public readonly List<Bar> bars = new List<Bar>();
        public readonly List<Zip> zips = new List<Zip>();
        readonly List<Leaf> leaves = new List<Leaf>();
        readonly List<Draft> drafts = new List<Draft>();
        readonly List<Raft> rafts = new List<Raft>();
        readonly List<Lantern> lanterns = new List<Lantern>();
        readonly List<Gate> gates = new List<Gate>();
        readonly List<Thorns> thorns = new List<Thorns>();
        readonly List<Pendulum> pendulums = new List<Pendulum>();
        readonly List<Pit> pits = new List<Pit>();
        readonly List<Dart> darts = new List<Dart>();
        readonly List<Cage> cages = new List<Cage>();
        Block pushBlock;
        // the stone block once pushed onto its plate: sunk into it a little, it stays there (the user)
        bool blockSet;
        const float BlockSink = 0.2f;
        float pushHome, dartT;
        bool lever, chestOpen, leverModel;
        // how long the lever's gate has left open
        float leverT;
        Transform leverHandle, chestLid, chestModel;
        // the chest model's lid (its own part, its origin on the hinge): its turn shut, the hinge's axis, how far it
        // opens (signed so it swings up and back), and how far it has
        Transform chestLidPart;
        Quaternion chestLidRest;
        Vector3 chestHinge;
        float chestLidAngle, chestOpenT;
        // the lever's handle (its own part of the model, turning about where it meets the base): how far it is thrown
        // (0 at rest, 1 across), its turn at rest and the turn that throws it across; the block on the plate settling
        // into it, and the plate
        Transform leverArm, plate;
        Quaternion leverArmRest;
        float leverThrow, leverAcross, blockSetT = 1f, blockFromX;

        /// <summary>The stone lanterns where he comes back after falling.</summary>
        public IEnumerable<float> Checkpoints { get { foreach (var l in lanterns) yield return l.x; } }
        public bool LeverPulled => lever;
        public bool GateOpen(int i) => gates[i].b.broken;
        int goalGate = -1;

        /// <summary>The shrine gate at the stage's end opens (Snap beaten), or shuts again.</summary>
        public void OpenGoalGate(bool open)
        {
            if (goalGate >= 0) gates[goalGate].wantOpen = open;
        }
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
            // (one just before the gorge: caught there for good, he tries again from its foot, not from the shrine)
            foreach (var x in new[] { 108f, 133f, 196f, 242f, 293f, 318f, 375f, 446f, 520f }) AddLantern(x);
            // the ends of the stage: closed gates where he can go no further
            AddEndGate(CbWorld.LevelMin - HalfWidth - 0.15f - EndGateHalf, 1);
            AddEndGate(CbWorld.LevelMax + HalfWidth + 0.15f + EndGateHalf, -1);

            // ---- after the first fight: a fallen trunk across the path, thorns on it - slide or crawl under it
            AddOverhang(TrunkX, 0.7f, 0.95f, 1.65f, true);

            // ---- the vine ravine
            AddVine(204f, 9f, 5f);
            AddVine(211.5f, 9f, 5f);
            AddBar(215.5f, 223f, 4.8f);
            AddVine(226.8f, 9.4f, 5.4f);
            AddLeaf(235.5f, 0.45f);
            // more springy leaves along the stage (the user liked the one in the ravine): before the rope bridge, past
            // the gorge, up to the canopy's vine pillar, up onto the ridge's shaft
            foreach (var x in LeafXs) AddLeaf(x, 0.45f);
            AddLedge(238f, 1.6f, 6.2f, false);

            // ---- the temple of traps (the user found its traps crowded together: they are spread out, the dart statue
            // moved from beside the logs to the stretch before the pit)
            AddThorns(246f);
            BuildStatue();
            foreach (var (x0, x1) in Pits) AddPit(x0, x1);
            AddPendulum(262.5f, 6.2f, 4.6f, 2.4f, 0f);
            AddPendulum(269f, 6.2f, 4.6f, 2.4f, 1.2f);
            BuildLever();
            // a log fallen across the way between the swinging logs and the gate: slide under it, still racing
            AddOverhang(RaceLogX, 0.7f, 0.95f, 1.65f, false);
            AddGate(RaceGateX);
            BuildPushBlock(280.5f);
            BuildPlate();
            AddGate(287f);

            // ---- after the temple: two raiders and a caged monkey (the gong, its bridge and the gorge are gone: the
            // way was a muddle of gadgets - the gong, the mud, the spinning log, the seesaw, the lift: the user)
            AddCage(308f);

            // ---- the raft river: rafts drift down it, a rock island in the middle
            foreach (var (from, to, offset) in new[] { (320f, 339.5f, 0f), (320f, 339.5f, 10f), (344.8f, 368f, 0f), (344.8f, 368f, 12f) })
                AddRaft(from, to, offset);
            AddRock(341.8f, 3.6f, 0.6f);

            // ---- the canopy and the waterfall
            AddCage(378f);
            AddTerrace(382f, 384f, 8f, 1.6f);
            blocks[blocks.Count - 1].vines = -1;
            DressVines(blocks[blocks.Count - 1], 382f);
            AddLedge(385.6f, 1.6f, 8.4f, false);
            AddDraft(387.4f, 388.6f, 0f, 10f);
            AddLedge(389.6f, 1.4f, 9.2f, false);
            AddBar(391f, 396f, 10.8f);
            AddLedge(397.6f, 1.6f, 9.6f, false);
            AddZip(398.6f, 11f, 411.2f, 3.4f);
            // down the zipline, and up the waterfall's cliff by the vines grown over it
            AddTerrace(414f, 420f, 9.4f, 3f);
            blocks[blocks.Count - 1].vines = -1;
            DressVines(blocks[blocks.Count - 1], 414f);
            BuildWaterfall(420f, 423.5f, 9.4f);
            AddLedge(421f, 1.2f, 2.6f, false);
            AddLedge(422.4f, 1.2f, 4.8f, false);
            AddLedge(421.2f, 1.4f, 7f, false);
            BuildChest();

            // ---- the ridge
            AddCage(466f);
            AddTerrace(478f, 479.4f, 7f, 2f);
            AddTerrace(481.2f, 482.6f, 7f, 2f);
            AddLedge(480.3f, 1.6f, 8.4f, false);

            // ---- the shrine gate: across the path, shut until Sergeant Snap is beaten, then it sinks and he walks through
            AddGate(CbWorld.GoalX + 1.2f);
            goalGate = gates.Count - 1;
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
        /// A liana hanging from a thick branch: two woody strands twisted round each other, swaying in a gentle S,
        /// thinner toward the end, leaves along it, hanging on a little past where he takes hold (a knot and a soft
        /// glow there, so it reads as a thing to swing on). It was a straight green stick (the user).
        /// </summary>
        void AddVine(float ax, float ay, float len)
        {
            var branch = CbArt.Joint(root, ax, ay, 0.3f, "branch");
            CbArt.Cylinder(Wood, branch, new Vector3(0, 0.25f, 0.2f), 0.26f, 4f, Quaternion.Euler(0, 0, 90));
            for (int k = -1; k <= 1; k++)
                CbArt.Part(CbMesh.Leaf(), LeafMat, branch, new Vector3(k * 1.3f, 0.55f, 0.1f), Vector3.one * 0.5f, Quaternion.Euler(-30, k * 40f, k * 25f), shadows: false);
            var t = CbArt.Joint(root, ax, ay, 0.05f, "vine");
            float total = len + VineTail;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int strand = 0; strand < 2; strand++)
            {
                var pts = new List<Vector3>();
                var radii = new List<float>();
                for (float d = -0.2f; d <= total; d += 0.06f)
                {
                    // the S it hangs in, the strands' twist round it (a turn every metre or so)
                    float sway = 0.07f * Mathf.Sin(d * 1.4f) * Mathf.Clamp01(d / 1.5f) * Mathf.Clamp01((len - d) / 0.6f + 0.2f);
                    float turn = d * 5.6f + strand * Mathf.PI;
                    pts.Add(new Vector3(sway + Mathf.Cos(turn) * 0.032f, -d, Mathf.Sin(turn) * 0.032f));
                    radii.Add(Mathf.Lerp(0.042f, 0.026f, Mathf.Clamp01(d / total)));
                }
                TaperedRope(verts, tris, pts, radii);
            }
            var mesh = new Mesh { name = "liana" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("liana");
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = LianaMat;
            // leaves along it, turned this way and that, none where his hands go
            for (float d = 0.55f; d < total - 0.15f; d += 0.62f)
            {
                if (Mathf.Abs(d - len) < 0.35f) continue;
                int side = Mathf.RoundToInt(d / 0.62f) % 2 == 0 ? 1 : -1;
                CbArt.Part(CbMesh.Leaf(), LeafMat, t, new Vector3(side * 0.07f, -d, -0.05f), Vector3.one * 0.2f,
                    Quaternion.Euler(-20, side * 25f, side * -65f), shadows: false);
            }
            CbArt.Part(CbMesh.Sphere, LianaMat, t, new Vector3(0, -total, 0), new Vector3(0.12f, 0.15f, 0.12f));
            CbArt.Part(CbMesh.Quad, CbArt.Glow(new Color(1f, 0.9f, 0.5f) * 0.5f, CbArt.Soft), t, new Vector3(0, -len, -0.2f), Vector3.one * 0.9f, shadows: false).name = "grip";
            vines.Add(new Vine { ax = ax, ay = ay, len = len, t = t });
        }
        /// <summary>How far a liana hangs on past where he takes hold of it.</summary>
        const float VineTail = 0.75f;
        static Material lianaMat;
        static Material LianaMat
        {
            get
            {
                if (lianaMat == null) { lianaMat = CbArt.NewToon(CbArt.Hex("#6b5a2e"), 0.7f); lianaMat.SetFloat("_Cull", 0f); lianaMat.SetFloat("_SpecStrength", 0f); }
                return lianaMat;
            }
        }

        /// <summary>A rope through the points whose thickness changes along it (radii, one per point).</summary>
        static void TaperedRope(List<Vector3> verts, List<int> tris, List<Vector3> pts, List<float> radii, int sides = 6)
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
                    verts.Add(pts[i] + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radii[i]);
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

        /// <summary>
        /// A swinging log: a battering ram slung level on two ropes from a beam overhead, swinging along the path (its
        /// ringed end faced the camera, which looked wrong: the user). The ropes stay parallel, so the log stays level.
        /// </summary>
        void AddPendulum(float x, float y, float len, float period, float phase)
        {
            var t = CbArt.Joint(root, x, y, 0f, "pendulum");
            var ram = CbArt.Joint(t, 0, -len, 0, "ram");
            var log = CbArt.Joint(ram, 0, -0.35f, 0, "log");
            bool modelled = Model("LogRam", log, new Vector3(1.75f, 1.56f, 0.7f), 0f) != null;
            if (!modelled) CbArt.Cylinder(Wood, ram, Vector3.zero, 0.32f, 1.75f, Quaternion.Euler(0, 0, 90));
            var rope = modelled ? M("#9a7a48") : VineGreen;
            Transform Rope(float dx)
            {
                var r = CbArt.Joint(t, dx, 0, 0, "rope");
                CbArt.Cylinder(rope, r, new Vector3(0, -len / 2, 0), 0.035f, len);
                return r;
            }
            var a = Rope(-RamRopes);
            var b = Rope(RamRopes);
            // the bar they hang from, on a beam over the path from a post at the back
            CbArt.Cylinder(Wood, t, new Vector3(0, 0.12f, 0), 0.11f, 2f, Quaternion.Euler(0, 0, 90));
            CbArt.Cylinder(Wood, t, new Vector3(0, 0.3f, 0.4f), 0.2f, 3.2f, Quaternion.Euler(90, 0, 0));
            CbArt.Cylinder(Wood, t, new Vector3(0, (0.3f - y) / 2, 2f), 0.18f, y + 0.3f);
            pendulums.Add(new Pendulum { x = x, y = y, len = len, period = period, phase = phase, ram = ram, ropeA = a, ropeB = b });
        }
        // the ram's ropes, either side of its middle (where the model's rope loops are)
        const float RamRopes = 0.62f;

        void BuildLever()
        {
            var t = CbArt.Joint(root, LeverX, 0, 0.2f, "lever");
            // the model: pulled, it is mirrored, so its handle leans the other way
            leverHandle = Model("Lever", t, new Vector3(0.92f, 1.2f, 0.73f), 180f);
            if (leverHandle != null)
            {
                leverModel = true;
                // the handle (split from the base, its origin where it turns: Tools/Blender/split_part.py) is thrown across
                // to the other side, swinging over (it jumped there: the user); the turn that mirrors its lean
                foreach (var part in leverHandle.GetComponentsInChildren<Transform>())
                    if (part.name == "LeverHandle") leverArm = part;
                if (leverArm != null && leverArm.GetComponentInChildren<Renderer>() is Renderer r)
                {
                    leverArmRest = leverArm.rotation;
                    var lean = r.bounds.center - leverArm.position;
                    float phi = Mathf.Atan2(lean.y, lean.x) * Mathf.Rad2Deg;
                    leverAcross = Mathf.DeltaAngle(0, 180f - 2 * phi);
                }
                return;
            }
            CbArt.Box(StoneGrey, t, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.6f, 0.5f));
            leverHandle = CbArt.Joint(t, 0, 0.6f, 0, "handle");
            CbArt.Cylinder(Wood, leverHandle, new Vector3(0, 0.45f, 0), 0.06f, 0.9f);
            CbArt.Part(CbMesh.Sphere, M("#c9302c"), leverHandle, new Vector3(0, 0.95f, 0), Vector3.one * 0.22f);
            leverHandle.localRotation = Quaternion.Euler(0, 0, 35);
        }

        /// <summary>
        /// A closed bamboo gate where the stage ends at either side, the way on barred: he used to stop at a wall no one
        /// could see (the user). It stood square to the camera like a picture and looked wrong (the user): now it
        /// stands across the dirt path, turned EndGateTurn degrees, from near its front edge to near its back edge, its
        /// face toward the stage and the camera (side +1: the stage lies to its right). Turned all the way (90) the
        /// camera saw only its near post; a little bigger than the gates in the temple, so it reaches across.
        /// He stops just short of it; its near post is the end away from him, so it never stands in front of him.
        /// </summary>
        void AddEndGate(float x, int side)
        {
            var t = CbArt.Joint(root, x, 0, 0f, "endGate");
            t.localRotation = Quaternion.Euler(0, side > 0 ? 180f - EndGateTurn : 180f + EndGateTurn, 0);
            const float k = EndGateScale;
            if (Model("Gate", t, new Vector3(3.45f * k, 3.6f * k, 1.24f * k), 0f) != null) return;
            for (float bx = -1.4f; bx <= 1.41f; bx += 0.28f)
                CbArt.Cylinder(Bamboo, t, new Vector3(bx * k, 1.8f * k, 0), 0.08f * k, 3.6f * k);
            CbArt.Box(Wood, t, new Vector3(0, 3.3f * k, 0), new Vector3(3.2f * k, 0.2f, 0.2f));
            CbArt.Box(Wood, t, new Vector3(0, 0.8f * k, 0), new Vector3(3.2f * k, 0.2f, 0.2f));
        }
        const float EndGateTurn = 45f, EndGateScale = 1.25f;
        /// <summary>How far along the path the end gate reaches from its middle, where it crosses his line (the half of its thickness, slanted).</summary>
        static float EndGateHalf => 1.24f * EndGateScale / 2 / Mathf.Sin(EndGateTurn * Mathf.Deg2Rad);

        void AddGate(float x)
        {
            var t = CbArt.Joint(root, x, 0, 0f, "gate");
            // the model: stone posts and bamboo bars in one, standing across the path like the stage's end gates (it
            // stood square to the camera: the user), its near post the end away from Raja as he comes; opened, it
            // sinks into the ground. Where it crosses his line its bars are thin, inside the block that stops him.
            t.localRotation = Quaternion.Euler(0, 180f + EndGateTurn, 0);
            var model = Model("Gate", t, new Vector3(3.45f, 3.6f, 1.24f), 0f);
            if (model != null)
            {
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
            plate = CbArt.Box(M("#b89a5a"), root, new Vector3(PlateX, 0.03f, 0.1f), new Vector3(0.9f, 0.06f, 0.9f));
            plate.name = "plate";
        }

        /// <summary>The lever's handle where leverThrow has it (eased): at rest, thrown across, or on its way.</summary>
        void ShowLever()
        {
            float k = leverThrow * leverThrow * (3 - 2 * leverThrow);
            if (leverArm != null) leverArm.rotation = Quaternion.AngleAxis(leverAcross * k, Vector3.forward) * leverArmRest;
            else if (!leverModel && leverHandle != null) leverHandle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(35f, -35f, k));
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
            // a little monkey, a friend of Raja's, locked in by the raiders: drawn in Canva and animated in Meshy (it
            // was a doll of balls, and the cage's model had a monkey of its own that never moved: the user), waving
            // for help through the bars, turned to the camera and the way Raja comes
            var animal = CbArt.Joint(t, 0, 0.1f, 0, "monkey");
            animal.localRotation = Quaternion.Euler(0, MonkeyFacing, 0);
            var monkey = CbModel.Load("Cloudborne/Monkey/Monkey", 0.85f, animal);
            if (monkey == null)
            {
                var fur = M("#7a5232"); var face = M("#e9c9a0");
                CbArt.Part(CbMesh.Sphere, fur, animal, new Vector3(0, 0.35f, 0), new Vector3(0.4f, 0.45f, 0.35f));
                CbArt.Part(CbMesh.Sphere, fur, animal, new Vector3(0, 0.75f, 0), Vector3.one * 0.34f);
                CbArt.Part(CbMesh.Sphere, face, animal, new Vector3(0, 0.73f, 0.13f), new Vector3(0.24f, 0.2f, 0.12f));
                foreach (float s in new[] { -1f, 1f })
                    CbArt.Part(CbMesh.Sphere, face, animal, new Vector3(s * 0.19f, 0.8f, 0), Vector3.one * 0.12f);
            }
            cages.Add(new Cage { x = x, t = t, animal = animal, model = model, monkey = monkey });
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

        /// <summary>
        /// A rock face grown over with vines to climb (the side of b at x): a curtain of leafy green vines spilling over
        /// its top, woven into a net with knots where they cross, flowers in it. It was two green poles that didn't look
        /// like anything to climb (the user).
        /// </summary>
        void DressVines(Block b, float x)
        {
            // (against the rock where it bulges out past its side)
            var t = CbArt.Joint(root, x - TerraceBulge * 0.5f, 0, 0, "climbingVines");
            // the vine drawn in Canva and built in Meshy (the net of green tubes looked painted on, not part of the
            // game: the user): two columns of it down the rock's side, facing the camera, one a little behind the
            // other, each a stack of vines overlapping, every other one mirrored, from just over the top to the ground
            if (Resources.Load<GameObject>(ClimbVinePath) != null)
            {
                // (in the stage's pictures they came out pale and thin against the rock: wider, three columns, a
                // deeper green)
                const float Piece = 2.9f, Step = 2.5f, Wide = 0.6f;
                int rows = Mathf.CeilToInt((b.top + 0.4f) / Step);
                int k = 0;
                Material tint = null;
                foreach (float z in new[] { -1.0f, -0.55f, -0.1f })
                    for (int r = 0; r < rows; r++, k++)
                    {
                        float hi = b.top + 0.4f - r * Step - (k % 3) * 0.3f, lo = hi - Piece;
                        var piece = CbArt.Joint(t, (k % 3 - 1) * 0.1f, 0, 0, "vine");
                        Fit(ClimbVinePath, piece, new Vector3(-Wide, lo, z - 0.2f), new Vector3(Wide, hi, z + 0.2f));
                        if (k % 2 == 1) piece.localScale = new Vector3(-1, 1, 1);
                        foreach (var rend in piece.GetComponentsInChildren<Renderer>())
                        {
                            tint ??= new Material(rend.sharedMaterial) { color = new Color(0.62f, 0.9f, 0.5f) };
                            rend.sharedMaterial = tint;
                        }
                    }
                return;
            }
            const float zFront = -0.8f, zBack = 0.62f;
            int strands = 5;
            float top = b.top + 0.12f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var lines = new List<List<Vector3>>();
            for (int i = 0; i < strands; i++)
            {
                float z0 = Mathf.Lerp(zFront, zBack, i / (strands - 1f));
                var pts = new List<Vector3>();
                var radii = new List<float>();
                // over the top edge first, then down the face, swaying
                pts.Add(new Vector3(0.25f, top + 0.02f, z0)); radii.Add(0.05f);
                for (float y = top; y > 0.25f; y -= 0.14f)
                {
                    pts.Add(new Vector3(-0.05f - 0.025f * (1 + Mathf.Sin(y * 3.1f + i * 1.7f)), y, z0 + 0.09f * Mathf.Sin(y * 1.9f + i * 2.3f)));
                    radii.Add(Mathf.Lerp(0.032f, 0.05f, y / top));
                }
                TaperedRope(verts, tris, pts, radii);
                lines.Add(pts);
            }
            // woven across: a strand to its neighbour, down a little, now one way now the other
            for (int i = 0; i < strands - 1; i++)
                for (float y = top - 0.6f - (i % 2) * 0.45f; y > 0.6f; y -= 0.9f)
                {
                    var a = lines[i][Mathf.Clamp(Mathf.RoundToInt((top - y) / 0.14f) + 1, 1, lines[i].Count - 1)];
                    var c = lines[i + 1][Mathf.Clamp(Mathf.RoundToInt((top - y + 0.45f) / 0.14f) + 1, 1, lines[i + 1].Count - 1)];
                    var mid = (a + c) / 2 + new Vector3(-0.04f, -0.06f, 0);
                    TaperedRope(verts, tris, new List<Vector3> { a, mid, c }, new List<float> { 0.026f, 0.024f, 0.026f });
                    // the knot where they cross
                    CbArt.Part(CbMesh.Sphere, VineMat, t, a + new Vector3(-0.01f, 0, 0), new Vector3(0.11f, 0.09f, 0.11f), shadows: false);
                }
            var mesh = new Mesh { name = "climbingVines" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("vineNet");
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = VineMat;
            // leaves all along, and flowers here and there, facing out of the rock and toward the camera
            int n = 0;
            foreach (var pts in lines)
                for (int k = 2; k < pts.Count; k += 2)
                {
                    n++;
                    var p = pts[k];
                    float side = n % 2 == 0 ? 1 : -1;
                    CbArt.Part(CbMesh.Leaf(), LeafMat, t, p + new Vector3(-0.06f, 0, side * 0.08f), Vector3.one * Random.Range(0.18f, 0.27f),
                        Quaternion.Euler(Random.Range(-30f, 30f), -90f + side * 35f, side * 55f + Random.Range(-20f, 20f)), shadows: false);
                    if (n % 5 == 0 && CbWorld.FlowerCard(n / 5) is (Material fm, float aspect))
                        CbArt.Part(CbMesh.Quad, fm, t, p + new Vector3(-0.14f, 0.05f, -0.05f), new Vector3(0.34f * aspect, 0.34f, 1), shadows: false).name = "flower";
                }
        }
        static Material vineMat;
        static Material VineMat
        {
            get
            {
                if (vineMat == null) { vineMat = CbArt.NewToon(CbArt.Hex("#4f8a32"), 0.6f); vineMat.SetFloat("_Cull", 0f); vineMat.SetFloat("_SpecStrength", 0f); }
                return vineMat;
            }
        }

        void BuildWaterfall(float x0, float x1, float top)
        {
            // the cave behind: dark rock, the cliff's own (it was a flat painting of rock); the falling water in front,
            // streaming, foaming at its lip and its foot, light and see-through, hiding the ledges a little (it was a
            // flat panel of the river's texture: the user), starting just over the cliff's side so no gap shows
            var cave = CbArt.Joint(root, 0, 0, 0, "cave");
            if (!RockWall(cave, x0 - 0.2f, x1 + 0.4f, -0.3f, top + 0.4f, 1.0f, 2.6f, Mathf.RoundToInt(x0 * 10), 0.2f, 0.45f, 2.2f))
            {
                var flat = RockMat(x1 - x0, top, x0, 0, new Color(0.42f, 0.42f, 0.48f));
                CbArt.Part(CbMesh.Quad, flat, cave, new Vector3((x0 + x1) / 2, top / 2, 1.2f), new Vector3(x1 - x0, top, 1), shadows: false);
            }
            float w0 = x0 - TerraceBulge - 0.15f, w1 = x1;
            var shader = Resources.Load<Shader>("Shaders/Waterfall");
            Material water;
            if (shader != null && shader.isSupported)
            {
                water = new Material(shader) { name = "waterfall", renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent };
                water.SetFloat("_X0", w0);
                water.SetFloat("_X1", w1);
                water.SetFloat("_Top", top + 0.1f);
                water.SetFloat("_Bottom", 0f);
            }
            else
            {
                water = CbArt.Glow(new Color(0.55f, 0.8f, 1f) * 0.55f, Resources.Load<Texture2D>("Cloudborne/Jungle/Water/Water"));
                waterfallMat = water;
            }
            var sheet = CbArt.Part(CbMesh.Quad, water, root, new Vector3((w0 + w1) / 2, (top + 0.3f) / 2, -1.3f), new Vector3(w1 - w0, top + 0.3f, 1), shadows: false);
            sheet.name = "waterfall";
            waterfall = new Rect(w0, 0, w1 - w0, top);
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
                // its lid, split from the box (Tools/Blender/split_part.py), swings open on its hinge (the model only
                // glowed and grew a little: the user saw a chest that stayed shut)
                foreach (var part in chestModel.GetComponentsInChildren<Transform>())
                    if (part.name == "ChestLid") chestLidPart = part;
                if (chestLidPart != null && chestLidPart.GetComponentInChildren<Renderer>() is Renderer lid)
                {
                    chestLidRest = chestLidPart.rotation;
                    chestHinge = chestModel.right;
                    var c = lid.bounds.center - chestLidPart.position;
                    chestLidAngle = (Quaternion.AngleAxis(105f, chestHinge) * c).y >= (Quaternion.AngleAxis(-105f, chestHinge) * c).y ? 105f : -105f;
                }
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
            // the top of a low passage's trunk is stood on (landed on from above)
            foreach (var o in overhangs)
                if (x > o.x0 - HalfWidth * 0.5f && x < o.x1 + HalfWidth * 0.5f && o.top <= fromY + 0.05f && o.top > best) best = o.top;
            // an open pit: nothing under him
            foreach (var p in pits)
                if (p.open && x > p.x0 && x < p.x1 && best <= 0) best = Deep;
            foreach (var r in rafts)
                if (r.sink <= 0 && Mathf.Abs(x - r.x) < 1.1f + HalfWidth * 0.5f && RaftTop <= fromY + StepUp && RaftTop > best) best = RaftTop;
            foreach (var l in leaves)
                if (Mathf.Abs(x - l.x) < l.half && l.top <= fromY + 0.05f && l.top > best) best = l.top;
        }

        /// <summary>Not a place to come back to: a pit's cover, a raft.</summary>
        bool WaysSafe(float x, float y)
        {
            foreach (var p in pits) if (x > p.x0 - HalfWidth && x < p.x1 + HalfWidth) return false;
            foreach (var r in rafts) if (Mathf.Abs(x - r.x) < 1.6f && y < 1f && InWater(x)) return false;
            return true;
        }

        /// <summary>
        /// The stretch of open path around x a soldier may roam: to the water, a pit or something solid on either
        /// side (as far as reach), so he stops where the path stops, not at a line no one can see.
        /// </summary>
        public void FreeStretch(float x, float reach, out float lo, out float hi)
        {
            float l = x - reach, h = x + reach;
            Stretch(x, ref l, ref h);
            lo = l; hi = h;
        }

        void Stretch(float x, ref float lo, ref float hi)
        {
            // on a rock in the river: that rock is all he has
            foreach (var b in blocks)
                if (!b.broken && InWater((b.x0 + b.x1) / 2) && x >= b.x0 && x <= b.x1)
                {
                    lo = Mathf.Max(lo, b.x0 + 0.3f);
                    hi = Mathf.Min(hi, b.x1 - 0.3f);
                    return;
                }
            foreach (var (gx0, gx1) in Gaps)
            {
                // a stream with a bridge over it is open path: he follows Raja across (he stopped at its foot), up to
                // where its planks are missing
                var bridge = BridgeOver(gx0, gx1);
                if (bridge == null) Bound(x, gx0, gx1, ref lo, ref hi);
                else if (bridge.holes != null)
                    foreach (var (h0, h1) in bridge.holes) Bound(x, h0, h1, ref lo, ref hi);
            }
            foreach (var b in blocks)
            {
                if (b.broken || b.bottom > 1f || InWater((b.x0 + b.x1) / 2)) continue;
                if (b.x1 <= x) lo = Mathf.Max(lo, b.x1 + 0.6f);
                if (b.x0 >= x) hi = Mathf.Min(hi, b.x0 - 0.6f);
            }
            // a soldier can't duck under a low passage
            foreach (var o in overhangs) Bound(x, o.x0, o.x1, ref lo, ref hi);
        }

        // ---------- low passages ----------

        /// <summary>
        /// Something across the path at head height - a fallen trunk on two rocks, thorns over it: standing he can't get
        /// past it; low (sliding, crawling, ducking) he goes under. Its top can be stood on, but thorns there hurt.
        /// (The user: obstacles to slide or crawl under.)
        /// </summary>
        public class Overhang { public float x0, x1, bottom, top; public bool thorny; public Transform t; }
        readonly List<Overhang> overhangs = new List<Overhang>();
        /// <summary>How tall Raja stands, and how tall low (ducking, crawling, sliding).</summary>
        public const float StandHeight = 1.55f, LowHeight = 0.8f;

        /// <summary>The bottom of the lowest low passage or jutting rock over (x, y); infinity if there is none.</summary>
        public float CeilingAt(float x, float y)
        {
            float c = float.PositiveInfinity;
            foreach (var o in overhangs)
                if (x > o.x0 - HalfWidth && x < o.x1 + HalfWidth && o.bottom >= y - 0.05f && o.bottom < c) c = o.bottom;
            foreach (var b in blocks)
                if (!b.broken && b.bottom > 0 && x > b.x0 - HalfWidth && x < b.x1 + HalfWidth && b.bottom >= y - 0.05f && b.bottom < c) c = b.bottom;
            return c;
        }

        /// <summary>A low passage at x: a trunk across the path from bottom to top, on rocks front and back; thorny: thorns on it.</summary>
        void AddOverhang(float x, float width, float bottom, float top, bool thorny)
        {
            var t = CbArt.Joint(root, x, 0, 0, "lowPassage");
            // the jungle's log, turned to lie across the path, as thick as the passage's roof
            var turn = CbArt.Joint(t, 0, bottom, 0, "trunk");
            turn.localRotation = Quaternion.Euler(0, 90, 0);
            var log = CbArt.Prop("Cloudborne/Jungle/Log/Log", turn, CbArt.PropOutline);
            if (log != null) log.localScale = new Vector3(1.8f, (top - bottom) / 0.695f, width / 0.64f);
            else CbArt.Cylinder(Wood, t, new Vector3(0, (bottom + top) / 2, 0), (top - bottom) / 2, 3.6f, Quaternion.Euler(90, 0, 0));
            // the rocks it rests on, at the path's front and back edges
            foreach (float z in new[] { -1.55f, 1.7f })
            {
                var r = CbArt.Joint(t, 0, 0, z, "rest");
                if (Fit(RiverStonePath, r, new Vector3(-0.55f, 0, -0.32f), new Vector3(0.55f, bottom + 0.05f, 0.32f)) == null)
                    CbArt.Box(StoneGrey, r, new Vector3(0, bottom / 2, 0), new Vector3(0.9f, bottom, 0.6f));
            }
            if (thorny)
                for (float z = -1.2f; z <= 1.21f; z += 0.8f)
                {
                    var th = CbArt.Joint(t, 0, top - 0.05f, z, "thorns");
                    if (Model("Thorns", th, new Vector3(width * 1.1f, 0.75f, 0.7f), 180f) == null)
                        CbArt.Cone(M("#7a3a2a"), th, new Vector3(0, 0.2f, 0), 0.15f, 0.4f, null, 5);
                }
            overhangs.Add(new Overhang { x0 = x - width / 2, x1 = x + width / 2, bottom = bottom, top = top, thorny = thorny, t = t });
        }

        /// <summary>Narrows lo..hi around x to the near side of a stretch from x0 to x1 he can't cross.</summary>
        static void Bound(float x, float x0, float x1, ref float lo, ref float hi)
        {
            if (x1 <= x) lo = Mathf.Max(lo, x1 + 0.6f);
            if (x0 >= x) hi = Mathf.Min(hi, x0 - 0.6f);
        }

        /// <summary>The bridge he can walk onto from the bank that spans the stretch of water from x0 to x1, if one does.</summary>
        Crossing BridgeOver(float x0, float x1)
        {
            foreach (var c in crossings)
                if (c.deck && c.X0 <= x0 + 0.7f && c.X1 >= x1 - 0.7f) return c;
            return null;
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

        /// <summary>
        /// Raja on the ground at (x, y) pressing towards dir against the stone block: it slides ahead of him, as far as
        /// the gate. True while it moves.
        /// </summary>
        public bool Push(float x, float y, int dir, float dt)
        {
            var b = pushBlock;
            if (b == null || blockSet || y > 0.05f) return false;
            float side = dir > 0 ? b.x0 - HalfWidth : b.x1 + HalfWidth;
            if (Mathf.Abs(x - side) > 0.08f) return false;
            float step = dir * 1.6f * dt;
            float nx0 = b.x0 + step, nx1 = b.x1 + step;
            if (nx0 < pushHome - 1.6f || nx1 > PlateX + 2f) return false;
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
                leverT = LeverOpenTime;
                gates[0].wantOpen = true;
                CbFx.Sfx("block", 0.8f);
                g.hud.SetTip("The gate opens - <color=#ffb03a>run!</color> It won't stay open long");
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
            return any;
        }

        void FreeCage(Cage c)
        {
            c.freed = true;
            for (int i = 0; i < c.t.childCount; i++) c.t.GetChild(i).gameObject.SetActive(c.t.GetChild(i) == c.animal);
            // it bursts apart: its bamboo bars and lashings fly (it vanished into thin air: the user)
            int side = g.hero.x <= c.x ? 1 : -1;
            for (int k = 0; k < 10; k++)
            {
                float len = Random.Range(0.5f, 1.3f);
                var bar = CbArt.Cylinder(k < 7 ? Bamboo : Wood, g.transform, new Vector3(c.x + Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 1.3f), Random.Range(-0.3f, 0.6f)),
                    k < 7 ? 0.05f : 0.08f, len, Random.rotation);
                g.AddDebris(bar, new Vector3(side * Random.Range(1.5f, 5f) + Random.Range(-1.5f, 1.5f), Random.Range(3f, 6.5f), Random.Range(-1f, 1f)), Random.Range(-500f, 500f));
            }
            CbFx.Puff(new Vector3(c.x, 0.8f, -0.3f), new Color(0.85f, 0.76f, 0.6f), 9, 1.8f, 0.8f, 0.5f);
            CbFx.Sfx("hit", 0.7f);
            CbFx.Sfx("pickup");
            g.score += 300;
            g.hud.Popup(new Vector3(c.x, 2f, 0), "FRIEND FREED!", CbHud.PopStyle.Normal);
            g.hud.SetTip($"A friend freed! ({CagesFreed}/{cages.Count})");
            c.hop = 0.01f;
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
            UpdateTraps(dt, hero);
            UpdateMovers(dt, hero);

            // gates slide up when opened; the second opens while its plate is pressed
            // the block pushed onto the plate settles into it with a thud and stays
            if (!blockSet && Mathf.Abs(PushBlockX - PlateX) < 0.25f)
            {
                blockSet = true;
                blockSetT = 0;
                blockFromX = PushBlockX;
                pushBlock.x0 = PlateX - 0.5f; pushBlock.x1 = PlateX + 0.5f; pushBlock.top = 1f - BlockSink;
            }
            // (it sank in at once: the user wanted to see it settle) sliding onto the plate's middle and sinking into it,
            // then a thud
            if (blockSetT < 1f)
            {
                blockSetT = Mathf.Min(1f, blockSetT + dt / 0.4f);
                float k = blockSetT * blockSetT * (3 - 2 * blockSetT);
                pushBlock.t.localPosition = new Vector3(Mathf.Lerp(blockFromX, PlateX, k), -BlockSink * k, 0);
                if (blockSetT >= 1f)
                {
                    CbFx.Puff(new Vector3(PlateX, 0.15f, -0.4f), new Color(0.8f, 0.75f, 0.65f), 6, 1.4f, 0.7f, 0.5f);
                    CbFx.Sfx("heavy", 0.6f);
                    g.shake = Mathf.Max(g.shake, 0.15f);
                }
            }
            // the lever's handle: thrown across fast, then creeping back as the gate's time runs out (a clock you can
            // see), and springing home when it shuts
            float throwTo = lever ? Mathf.Clamp01(leverT / LeverOpenTime * 1.15f) : 0f;
            leverThrow = Mathf.MoveTowards(leverThrow, throwTo, dt * (throwTo > leverThrow ? 7f : 3f));
            ShowLever();
            // the lever's gate: open while the time lasts (ticking, faster at the end), held while he is in it;
            // then it shuts and the lever springs back, to be pulled again
            if (lever && gates.Count > 0)
            {
                float before = leverT;
                leverT -= dt;
                float tick = leverT < 2f ? 0.25f : 0.5f;
                if (leverT > 0 && Mathf.Floor(before / tick) != Mathf.Floor(leverT / tick)) CbFx.Sfx("block", leverT < 2f ? 0.35f : 0.2f);
                bool inGate = gates[0].open > 0 && Mathf.Abs(hero.x - (gates[0].b.x0 + gates[0].b.x1) / 2) < 0.8f;
                gates[0].wantOpen = leverT > 0 || inGate;
                if (!gates[0].wantOpen)
                {
                    lever = false;
                    if (hero.x < RaceGateX) g.hud.SetTip("The gate shut! Strike the lever again - and don't stop for the logs");
                }
            }
            bool pressed = (Mathf.Abs(hero.x - PlateX) < 0.6f && hero.y < 0.1f) || blockSet || (pushBlock.x0 < PlateX && pushBlock.x1 > PlateX);
            // the plate goes down under what presses it
            if (plate != null)
            {
                var pp = plate.localPosition;
                plate.localPosition = new Vector3(pp.x, Mathf.MoveTowards(pp.y, pressed ? -0.015f : 0.03f, dt * 0.3f), pp.z);
            }
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
            // a caged friend waves for help; freed, it hops for joy, then turns and runs off into the jungle, behind the
            // bushes (it bounced away like a doll: the user)
            foreach (var c in cages)
            {
                if (!c.animal.gameObject.activeSelf) continue;
                if (c.hop <= 0)
                {
                    c.monkey?.LoopSeconds("Wave_for_Help", Time.time + c.x);
                    continue;
                }
                c.hop += dt;
                if (c.hop < MonkeyCheer)
                {
                    if (c.monkey != null) c.monkey.Window("Hop_with_Arms_Raised", 0f, 1f, c.hop / MonkeyCheer);
                    else c.animal.localPosition = new Vector3(0, 0.1f + Mathf.Abs(Mathf.Sin(c.hop * 9)) * 0.4f, 0);
                    continue;
                }
                float run = c.hop - MonkeyCheer;
                // turning to the jungle behind, a little toward the way on, and running
                float turn = Mathf.Clamp01(run / 0.25f);
                c.animal.localRotation = Quaternion.Euler(0, Mathf.LerpAngle(MonkeyFacing, 20f, turn), 0);
                c.animal.localPosition = new Vector3(run * 0.9f, 0.1f, run * 3.6f);
                if (c.monkey != null) c.monkey.LoopSeconds("RunFast", run);
                else c.animal.localPosition += Vector3.up * Mathf.Abs(Mathf.Sin(run * 9)) * 0.4f;
                if (run > 2.2f) c.animal.gameObject.SetActive(false);
            }
            // the chest: the key opens it, a tiger token inside
            if (!chestOpen && Mathf.Abs(hero.x - ChestX) < 1.3f && hero.y < 1f)
            {
                if (g.HasKey)
                {
                    chestOpen = true;
                    if (chestLid != null) chestLid.localRotation = Quaternion.Euler(-70, 0, 0);
                    if (chestModel != null) CbVfx.Ring(new Vector3(ChestX, 0.6f, -0.5f), CbFx.Flame * 0.6f, 1.6f, 0.4f);
                    g.items.Spawn(CbItems.Kind.Token, new Vector3(ChestX, 1.2f, 0), resting: true);
                    for (int k = 0; k < 3; k++) g.items.Spawn(CbItems.Kind.Coin, new Vector3(ChestX, 0.8f, 0));
                    CbFx.Spark(new Vector3(ChestX, 0.8f, -0.4f), CbFx.Flame, 16, 4, 0.35f, 0.5f, -3);
                    CbFx.Sfx("pickup");
                }
                else if (!chestHinted) { chestHinted = true; g.hud.SetTip("A locked chest. Its key is up in the treetops..."); }
            }
            // the lid swinging open, and staying open
            if (chestOpen && chestLidPart != null && chestOpenT < 1f)
            {
                chestOpenT = Mathf.MoveTowards(chestOpenT, 1f, dt / 0.5f);
                float k = 1 - (1 - chestOpenT) * (1 - chestOpenT);
                chestLidPart.rotation = Quaternion.AngleAxis(chestLidAngle * k, chestHinge) * chestLidRest;
            }
            if (waterfallMat != null) waterfallMat.mainTextureOffset = new Vector2(0, Time.time * 1.2f);
            // spray and mist where it lands
            if (Mathf.Abs(hero.x - waterfall.center.x) < 12f && Random.value < 0.08f)
                CbFx.Puff(new Vector3(Random.Range(waterfall.xMin, waterfall.xMax), 0.3f, -1.5f), new Color(0.9f, 0.97f, 1f), 1, 0.8f, 0.9f, 0.9f);
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
            // the dart statue: a dart every 2.4 s along the path at chest height (duck under, jump over), at Raja coming
            // towards it
            dartT -= dt;
            if (dartT <= 0 && hero.x < StatueX && StatueX - hero.x < 12f)
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
                // the thorns (until cut) and anything else solid stop it
                bool stopped = false;
                foreach (var b in blocks)
                    if (!b.broken && d.y < b.top && d.x > b.x0 && d.x < b.x1) { stopped = true; break; }
                if (hit || stopped || d.gone > 12f) { Object.Destroy(d.t.gameObject); darts.RemoveAt(i); }
            }
            // thorns on a low passage's trunk: landing on them hurts and throws him off
            foreach (var o in overhangs)
                if (o.thorny && hero.x > o.x0 - HalfWidth * 0.5f && hero.x < o.x1 + HalfWidth * 0.5f && Mathf.Abs(hero.y - o.top) < 0.06f)
                    hero.TakeHit(6, hero.x < (o.x0 + o.x1) / 2 ? -1 : 1, knock: 5f);
            // swinging logs
            foreach (var p in pendulums)
            {
                float a = Mathf.Sin(Time.time * Mathf.PI * 2 / p.period + p.phase) * 0.75f;
                var turn = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
                p.ropeA.localRotation = p.ropeB.localRotation = turn;
                p.ram.localPosition = new Vector3(Mathf.Sin(a) * p.len, -Mathf.Cos(a) * p.len, 0);
                float lx = p.x + Mathf.Sin(a) * p.len, ly = p.y - Mathf.Cos(a) * p.len;
                // the log is level and long: it hits along its length
                // (as tall as he is: low - crawling, sliding, ducking - he passes under it; it hit him anyway: the user)
                if (Mathf.Abs(hero.x - lx) < 0.95f && hero.y < ly + 0.35f && hero.y + hero.Height + 0.15f > ly - 0.35f)
                    hero.TakeHit(8, Mathf.Cos(Time.time * Mathf.PI * 2 / p.period + p.phase) >= 0 ? 1 : -1, knock: 4f);
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
        }

        void ResetWays()
        {
            foreach (var l in lanterns) { l.lit = false; CbArt.SetGlow(l.light, new Color(0.15f, 0.12f, 0.1f)); if (l.window != null) l.window.enabled = false; }
            foreach (var th in thorns) { th.hp = 2; th.b.broken = false; th.b.t.gameObject.SetActive(true); }
            foreach (var p in pits) { p.open = false; p.stood = 0; p.cover.gameObject.SetActive(true); }
            foreach (var d in darts) Object.Destroy(d.t.gameObject);
            darts.Clear();
            foreach (var c in cages)
            {
                c.freed = false; c.hp = 3; c.hop = 0;
                for (int i = 0; i < c.t.childCount; i++) c.t.GetChild(i).gameObject.SetActive(true);
                c.animal.localPosition = new Vector3(0, 0.1f, 0);
                c.animal.localRotation = Quaternion.Euler(0, MonkeyFacing, 0);
                c.t.localRotation = Quaternion.identity;
            }
            lever = chestOpen = chestHinted = false;
            leverT = 0;
            leverThrow = 0;
            ShowLever();
            blockSetT = 1f;
            if (plate != null) plate.localPosition = new Vector3(PlateX, 0.03f, 0.1f);
            foreach (var gt in gates) { gt.open = 0; gt.wantOpen = false; gt.b.broken = false; gt.b.t.localPosition = gt.home; }
            pushBlock.x0 = pushHome - 0.5f; pushBlock.x1 = pushHome + 0.5f; pushBlock.t.localPosition = new Vector3(pushHome, 0, 0);
            pushBlock.top = 1f; blockSet = false;

            if (chestLid != null) chestLid.localRotation = Quaternion.identity;
            if (chestModel != null) chestModel.localScale = Vector3.one;
            chestOpenT = 0;
            if (chestLidPart != null) chestLidPart.rotation = chestLidRest;
            foreach (var r in rafts) r.sink = 0;
        }
    }
}
