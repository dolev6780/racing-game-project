using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cloudborne
{
    /// <summary>
    /// The jungle of stage 1-1: path and grass, ferns, jungle trees and palms, big leaves, mossy rocks,
    /// karst pillars with waterfalls, shrine ruins, a painted far backdrop, light shafts, ground mist, fireflies
    /// and dark foreground leaves. Gameplay happens on the plane z = 0; the background is at +z, the camera at -z.
    /// The scenery is drawn in Canva and built in Meshy (Resources/Cloudborne/Jungle); whatever is missing there
    /// is built from code instead. Static scenery is merged into one mesh per material and stretch of the level,
    /// so the parts out of view are skipped.
    /// </summary>
    public class CbWorld
    {
        public const float LevelMin = -8f, LevelMax = 108f;
        // the blue-teal haze of the painted backdrop: distant scenery fades into it, not into white
        public static readonly Color Horizon = CbArt.Hex("#7fc0cc");

        public Transform root;
        readonly List<(Transform t, Material m, float ph, float baseA)> shafts = new List<(Transform, Material, float, float)>();
        readonly List<Transform> mists = new List<Transform>();
        Transform flies;
        static readonly string[] Greens = { "#2e7a3c", "#3c8f46", "#256a36", "#4a9a4c", "#1f5e33" };
        // the scenery models, made by Tools/Blender/meshy_prop.py with their foot at the origin, sized in metres
        const string TreeModel = "Tree", PalmModel = "Palm", FernModel = "Fern", LeavesModel = "Leaves",
            BoulderModel = "Boulder", PillarModel = "Pillar", GateModel = "Gate", KarstModel = "Karst";
        // The backdrop is a painting on a wall far behind everything. It follows the camera along the level and its
        // picture slides only a little, so it reads as very far away. Its top is above the view; the mist under its
        // karst pillars, about 62% of the way down, sits near eye level, and the ground hides the rest.
        const string BackdropPath = "Cloudborne/Jungle/Backdrop/JungleBackdrop";
        const float BackdropZ = 150f, BackdropTop = 48f, BackdropHeight = 73f, BackdropWidth = 260f, BackdropStretch = 1.3f,
            BackdropSlide = 0.15f;
        Transform backdrop;
        Material backdropMat;
        static readonly Color ForegroundShade = new Color(0.16f, 0.24f, 0.2f);
        // The painted textures are bright, and the toon light doubles them in the sun: plain, they glow lime green.
        // Toned down a little towards blue they sit behind the characters, like the painting.
        static readonly Color SceneryTone = new Color(0.66f, 0.64f, 0.7f);
        // the meadow between the path and the trees glowed neon in the sun in the same way
        static readonly Color GroundTone = new Color(0.72f, 0.7f, 0.74f);
        readonly Dictionary<(string, float, Color), (GameObject prefab, Material mat)> models =
            new Dictionary<(string, float, Color), (GameObject, Material)>();

        public CbWorld(Transform parent)
        {
            root = new GameObject("Jungle").transform;
            root.SetParent(parent, false);
            Random.InitState(7);
            // near casts shadows; far (the distance and the low ground cover) doesn't
            var near = new GameObject("near").transform; near.SetParent(root, false);
            var far = new GameObject("far").transform; far.SetParent(root, false);
            BuildGround(far);
            BuildPlants(near, far);
            BuildRocksAndRuins(near);
            BuildBackground(far);
            BuildForeground(far);
            Bake(near, true);
            Bake(far, false);
            BuildLightAndAir();
        }

        // ---------- ground ----------

        void BuildGround(Transform far)
        {
            var grass = CbArt.NewToon(GroundTone, 0f, CbArt.Grass);
            grass.mainTextureScale = new Vector2(70, 4);
            grass.SetFloat("_SpecStrength", 0f);
            CbArt.Box(grass, root, new Vector3(50, -3, 0), new Vector3(260, 6, 20));
            var path = CbArt.NewToon(Color.white, 0f, CbArt.Path);
            path.mainTextureScale = new Vector2(60, 1);
            path.SetFloat("_SpecStrength", 0f);
            CbArt.Part(CbMesh.Quad, path, root, new Vector3(50, 0.01f, 0), new Vector3(260, 3.2f, 1), CbArt.Rot(Mathf.PI / 2), shadows: false);
            CbArt.Part(CbMesh.Quad, CbArt.Toon("#3a7a33"), root, new Vector3(50, -0.3f, 68), new Vector3(520, 120, 1), CbArt.Rot(Mathf.PI / 2), shadows: false);

            // grass blades and fern fronds, merged straight into a few meshes
            var blade = CbMesh.Cone(3);
            var frond = CbMesh.Cone(4);
            var batches = new Dictionary<Material, List<CombineInstance>>();
            void Add(Mesh mesh, Material m, Matrix4x4 mat)
            {
                if (!batches.TryGetValue(m, out var list)) batches[m] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = mat });
            }
            var grassMats = new[] { CbArt.Toon("#357a33"), CbArt.Toon("#468a3c"), CbArt.Toon("#2a6a30"), CbArt.Toon("#559443") };
            for (int i = 0; i < 1800; i++)
            {
                bool front = Random.value < 0.35f;
                var p = new Vector3(Random.Range(-40f, 140f), 0, front ? -Random.Range(1.8f, 6.5f) : Random.Range(1.8f, 9.5f));
                var q = Quaternion.Euler(Random.Range(-17f, 17f), Random.Range(0f, 360f), Random.Range(-17f, 17f));
                float k = Random.Range(0.6f, 1.6f);
                Add(blade, grassMats[i % 4], Matrix4x4.TRS(p, q, new Vector3(0.05f * k, 0.42f * k, 0.05f * k)));
            }
            if (Has(FernModel))
            {
                for (int c = 0; c < 120; c++)
                {
                    bool front = Random.value < 0.3f;
                    var at = new Vector3(Random.Range(-40f, 140f), 0, front ? -Random.Range(4.6f, 6.8f) : Random.Range(2.2f, 9.5f));
                    Place(FernModel, far, at, front ? Random.Range(0.6f, 0.85f) : Random.Range(0.8f, 1.3f));
                }
            }
            var fernMats = new[] { CbArt.Toon("#2c6e30"), CbArt.Toon("#3a8a3c"), CbArt.Toon("#245c2a") };
            for (int c = 0; c < (Has(FernModel) ? 0 : 300); c++)
            {
                bool front = Random.value < 0.3f;
                var center = new Vector3(Random.Range(-40f, 140f), 0, front ? -Random.Range(4.6f, 6.8f) : Random.Range(2.2f, 9.5f));
                float ck = front ? Random.Range(0.55f, 0.85f) : Random.Range(0.8f, 1.4f);
                for (int f = 0; f < 7; f++)
                {
                    float yaw = f / 7f * 360f + Random.Range(-12f, 12f);
                    var q = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(Random.Range(40f, 66f), 0, 0);
                    Add(frond, fernMats[(c + f) % 3], Matrix4x4.TRS(center, q, new Vector3(0.14f * ck, 1.1f * ck * Random.Range(0.9f, 1.3f), 0.14f * ck * 0.25f)));
                }
            }
            foreach (var kv in batches) MakeBatch(kv.Key, kv.Value, root, false);
        }

        // ---------- plants ----------

        void BuildPlants(Transform near, Transform far)
        {
            bool trees = Has(TreeModel), palms = Has(PalmModel);
            for (float x = -30; x < 150; x += Random.Range(6f, 10f))
            {
                // The camera looks a little down and sees about 8 m up at the tree line: the modelled trees (9.5 m)
                // and palms (7.5 m) are made smaller and set further back so their crowns show, not just trunks.
                float z = Random.Range(7.5f, 11f);
                if (Random.value < 0.35f)
                {
                    if (palms) Place(PalmModel, near, new Vector3(x, 0, z + 1.5f), Random.Range(0.75f, 0.95f));
                    else Palm(near, x, z, Random.Range(1f, 1.3f));
                }
                else if (trees) Place(TreeModel, near, new Vector3(x, 0, z + 1.5f), Random.Range(0.6f, 0.75f), CbArt.PropOutline);
                else JungleTree(near, x, z, Random.Range(0.85f, 1.1f), CbArt.PropOutline);
            }
            for (float x = -30; x < 150; x += Random.Range(7f, 11f))
            {
                float z = Random.Range(17f, 28f), s = Random.Range(1.3f, 1.9f);
                if (trees) Place(TreeModel, far, new Vector3(x, 0, z), s * 0.62f);
                else JungleTree(far, x, z, s, 0f);
            }
            // clumps of big leaves among the rocks between the path and the trees
            if (Has(LeavesModel))
                for (float x = -25; x < 145; x += Random.Range(5f, 9f))
                    Place(LeavesModel, near, new Vector3(x + Random.Range(-1f, 1f), 0, Random.Range(2.8f, 6f)), Random.Range(0.75f, 1.15f));
        }

        // ---------- modelled scenery ----------

        static string ModelPath(string name) => $"Cloudborne/Jungle/{name}/{name}";

        static bool Has(string name) => Resources.Load<GameObject>(ModelPath(name)) != null;

        /// <summary>
        /// Models of thin leaves, seen from both sides. They get no outline: the outline is drawn from the back faces,
        /// so a leaf showing its back would turn black.
        /// </summary>
        static bool Leafy(string name) => name == LeavesModel || name == FernModel || name == PalmModel;

        /// <summary>
        /// Places a copy of a scenery model standing at pos, its drawn front turned towards the camera give or take a
        /// little. Copies with the same look share one material, so the bake merges them.
        /// </summary>
        Transform Place(string name, Transform parent, Vector3 pos, float scale, float outline = 0f, Color? tint = null, float turn = 35f)
        {
            if (Leafy(name)) outline = 0f;
            var key = (name, outline, tint ?? SceneryTone);
            if (!models.TryGetValue(key, out var model))
            {
                var path = ModelPath(name);
                var mat = CbArt.AnimeToon(Resources.Load<Texture2D>(path + "_Albedo"), outline);
                mat.color = key.Item3;
                // scenery takes a softer rim light than the characters, so they stand out in front of it
                mat.SetFloat("_RimStrength", 0.25f);
                if (Leafy(name)) mat.SetFloat("_Cull", (float)CullMode.Off);
                models[key] = model = (Resources.Load<GameObject>(path), mat);
            }
            // Meshy's front is Unity's +Z after the export; the camera looks along +Z from in front. The turn and the
            // size go on top of the import's own rotation and scale (it stands Blender's Z-up model upright).
            var t = Object.Instantiate(model.prefab, parent, false).transform;
            var prefab = model.prefab.transform;
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0, 180 + Random.Range(-turn, turn), 0) * prefab.localRotation;
            t.localScale = prefab.localScale * scale;
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = model.mat;
            return t;
        }

        static void JungleTree(Transform parent, float x, float z, float s, float outline)
        {
            var g = CbArt.Joint(parent, x, 0, z, "tree");
            g.localScale = Vector3.one * s;
            CbArt.Part(CbMesh.Frustum(0.32f / 0.55f, 8), CbArt.Toon("#6e5c4a", outline), g, Vector3.zero, new Vector3(0.55f, 7.5f, 0.55f));
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI / 2 + Random.Range(-0.3f, 0.3f);
                // buttress roots lean outwards from the trunk
                CbArt.Cone(CbArt.Toon("#5f4e3e"), g, new Vector3(Mathf.Cos(a) * 0.55f, 0.65f, Mathf.Sin(a) * 0.55f), 0.26f, 1.7f,
                    Quaternion.FromToRotation(Vector3.up, new Vector3(Mathf.Cos(a) * 0.55f, 1f, Mathf.Sin(a) * 0.55f)), 5);
            }
            for (int k = 0; k < 6; k++)
                CbArt.Part(CbMesh.Blob(), CbArt.Toon(Greens[k % 5], outline), g,
                    new Vector3(Random.Range(-2.2f, 2.2f), 7.2f + Random.Range(-0.6f, 1.4f), Random.Range(-1.2f, 1.2f)),
                    new Vector3(1, 0.72f, 1) * Random.Range(1.4f, 2.1f));
            for (int k = 0; k < 3; k++)
            {
                float len = Random.Range(3f, 5.5f);
                CbArt.Cylinder(CbArt.Toon("#2f5a2a"), g, new Vector3(Random.Range(-1.8f, 1.8f), 6.8f - len / 2, -Random.Range(-0.2f, 1f)), 0.035f, len,
                    CbArt.Rot(0, 0, Random.Range(-0.08f, 0.08f)));
            }
        }

        static void Palm(Transform parent, float x, float z, float s)
        {
            var g = CbArt.Joint(parent, x, 0, z, "palm");
            g.localScale = Vector3.one * s;
            float px = 0, py = 0, lean = Random.Range(-0.35f, 0.35f);
            for (int k = 0; k < 7; k++)
            {
                const float h = 0.85f;
                float ang = lean * (k / 7f) * 1.6f;
                CbArt.Part(CbMesh.Frustum((0.15f - k * 0.008f) / (0.19f - k * 0.008f), 7), CbArt.Toon(k % 2 == 1 ? "#8a6e4e" : "#76603f", 0.9f), g,
                    new Vector3(px, py, 0), new Vector3(0.19f - k * 0.008f, h, 0.19f - k * 0.008f), CbArt.Rot(0, 0, -ang));
                px += Mathf.Sin(ang) * h; py += Mathf.Cos(ang) * h;
            }
            for (int k = 0; k < 8; k++)
            {
                var leaf = CbArt.Joint(g, px, py, 0, "frond");
                leaf.localRotation = Quaternion.Euler(0, k / 8f * 360f + Random.Range(-12f, 12f), 0) * Quaternion.Euler(Random.Range(100f, 120f), 0, 0);
                CbArt.Part(CbMesh.Cone(4), CbArt.Toon(k % 2 == 1 ? "#3c9a4a" : "#2f8540", 0.9f), leaf, Vector3.zero, new Vector3(0.34f, 3f, 0.34f * 0.22f));
            }
        }

        // ---------- rocks and ruins ----------

        void BuildRocksAndRuins(Transform near)
        {
            bool boulders = Has(BoulderModel);
            for (float x = -20; x < 140; x += Random.Range(4f, 8f))
            {
                float s = Random.Range(0.25f, 0.6f), z = Random.Range(2.4f, 4.5f);
                if (boulders)
                {
                    Place(BoulderModel, near, new Vector3(x, -0.08f * s, z), s * 1.5f, CbArt.PropOutline, turn: 180f);
                    continue;
                }
                CbArt.Part(CbMesh.Blob(6, 4), CbArt.Toon("#7f8a78", CbArt.PropOutline), near, new Vector3(x, s * 0.35f, z),
                    new Vector3(s, s * 0.7f, s), Random.rotation);
                CbArt.Part(CbMesh.Dome(0.5f, 8), CbArt.Toon("#5f9a50"), near, new Vector3(x, s * 0.45f, z), new Vector3(s * 0.8f, s * 0.36f, s * 0.8f));
            }
            if (Has(GateModel) && Has(PillarModel))
            {
                // the shrine: its gate, lone pillars of different heights and a few fallen stones
                Place(GateModel, near, new Vector3(74, 0, 4.2f), 1f, CbArt.PropOutline, turn: 8f);
                Place(PillarModel, near, new Vector3(66, 0, 5f), 0.5f, CbArt.PropOutline, turn: 20f);
                Place(PillarModel, near, new Vector3(86, 0, 4.8f), 0.65f, CbArt.PropOutline, turn: 20f);
                Place(PillarModel, near, new Vector3(91, 0, 4f), 0.85f, CbArt.PropOutline, turn: 20f);
                foreach (var p in new[] { new Vector2(80, 3.2f), new Vector2(83, 3.6f), new Vector2(88, 3.1f) })
                    Place(boulders ? BoulderModel : PillarModel, near, new Vector3(p.x, -0.05f, p.y), boulders ? 0.4f : 0.15f, CbArt.PropOutline, turn: 180f);
                return;
            }
            var stone = CbArt.Toon("#8f9a8a", CbArt.PropOutline);
            var moss = CbArt.Toon("#5f9a50");
            void Pillar(float x, float z, float h, bool broken)
            {
                CbArt.Box(stone, near, new Vector3(x, h / 2, z), new Vector3(0.8f, h, 0.8f));
                CbArt.Box(stone, near, new Vector3(x, 0.15f, z), new Vector3(1.05f, 0.3f, 1.05f));
                if (!broken) CbArt.Box(stone, near, new Vector3(x, h + 0.15f, z), new Vector3(1.05f, 0.3f, 1.05f));
                CbArt.Part(CbMesh.Dome(0.5f, 8), moss, near, new Vector3(x, h + (broken ? 0 : 0.3f), z), new Vector3(0.5f, 0.2f, 0.5f));
            }
            Pillar(72, 4.2f, 3.6f, false); Pillar(76, 4.2f, 3.6f, false);
            CbArt.Box(stone, near, new Vector3(74, 3.9f, 4.2f), new Vector3(5.2f, 0.6f, 1.1f));
            Pillar(86, 4.8f, 2.2f, true); Pillar(91, 4f, 3f, false); Pillar(66, 5f, 1.4f, true);
            foreach (var p in new[] { new Vector2(80, 3.2f), new Vector2(83, 3.6f), new Vector2(88, 3.1f) })
                CbArt.Box(stone, near, new Vector3(p.x, 0.22f, p.y), new Vector3(0.7f, 0.45f, 0.6f), CbArt.Rot(0, Random.Range(-0.6f, 0.6f)));
        }

        // ---------- background ----------

        void BuildBackground(Transform far)
        {
            var fall = CbArt.Glow(new Color(0.9f, 0.97f, 1f, 0.55f), CbArt.Shaft);
            bool karst = Has(KarstModel);
            for (float x = -20; x < 160; x += Random.Range(15f, 24f))
            {
                float z = Random.Range(36f, 50f), h = Random.Range(16f, 28f), r = Random.Range(2.6f, 4.2f);
                if (karst)
                {
                    // the model is 26 m tall and about 11 m across; r becomes the distance to its face, where the waterfall runs
                    float s = h / 26f;
                    Place(KarstModel, far, new Vector3(x, -0.5f, z), s, turn: 30f);
                    r = 6f * s;
                }
                else
                {
                    CbArt.Part(CbMesh.Frustum(0.55f, 7), CbArt.Toon("#8a9488"), far, new Vector3(x, -0.5f, z), new Vector3(r, h, r), CbArt.Rot(0, Random.Range(0f, 6f)));
                    CbArt.Part(CbMesh.Dome(0.5f, 10), CbArt.Toon("#4f9a48"), far, new Vector3(x, h - 0.7f, z), new Vector3(r * 0.75f, r * 0.41f, r * 0.75f));
                    for (int k = 0; k < 4; k++)
                    {
                        float len = Random.Range(4f, 9f);
                        CbArt.Cylinder(CbArt.Toon("#2f5a2a"), far, new Vector3(x + Random.Range(-r * 0.5f, r * 0.5f), h - 1 - len / 2, z - r * 0.6f), 0.08f, len);
                    }
                }
                if (Random.value < 0.5f)
                {
                    // waterfall: a glowing strip down the pillar's front, with mist at its foot
                    var w = CbArt.Part(CbMesh.Quad, fall, root, new Vector3(x + Random.Range(-0.6f, 0.6f), h * 0.45f, z - r * 0.75f), new Vector3(1.4f, h * 0.85f, 1), shadows: false);
                    w.name = "waterfall";
                    var m = CbArt.Part(CbMesh.Quad, CbArt.Glow(new Color(1, 1, 1, 0.25f), CbArt.Mist), root, new Vector3(w.localPosition.x, 0.8f, z - r - 1), new Vector3(10, 3.5f, 1), shadows: false);
                    m.name = "fallMist";
                }
            }
            if (BuildBackdrop()) return;
            Canopy(far, 70, 7, 4, "#2d5a3a", 1);
            Canopy(far, 110, 13, 6, "#3f6e4a", 3);
            Canopy(far, 160, 22, 9, "#5c8a62", 5);
        }

        /// <summary>The painted backdrop in place of the canopy layers; false when the painting or its shader is missing.</summary>
        bool BuildBackdrop()
        {
            var tex = Resources.Load<Texture2D>(BackdropPath);
            var shader = Resources.Load<Shader>("Shaders/Backdrop");
            if (tex == null || shader == null || !shader.isSupported) return false;
            // mirrored repeats join without a seam
            tex.wrapModeU = TextureWrapMode.Mirror;
            tex.wrapModeV = TextureWrapMode.Clamp;
            backdropMat = new Material(shader) { mainTexture = tex };
            float tile = BackdropHeight * tex.width / tex.height * BackdropStretch;
            backdropMat.mainTextureScale = new Vector2(BackdropWidth / tile, 1);
            backdrop = CbArt.Part(CbMesh.Quad, backdropMat, root, new Vector3(0, BackdropTop - BackdropHeight / 2, BackdropZ),
                new Vector3(BackdropWidth, BackdropHeight, 1), shadows: false);
            backdrop.name = "backdrop";
            backdrop.GetComponent<MeshRenderer>().receiveShadows = false;
            return true;
        }

        static void Canopy(Transform far, float z, float baseY, float amp, string hex, float seed)
        {
            var v = new List<Vector3>(); var tri = new List<int>();
            for (float x = -200; x <= 320; x += 2.5f)
            {
                float y = baseY + Mathf.Sqrt(Mathf.Abs(Mathf.Sin(x * 0.19f + seed))) * amp + Mathf.Sin(x * 0.04f + seed * 2) * amp * 0.6f;
                v.Add(new Vector3(x, -10, 0)); v.Add(new Vector3(x, y, 0));
                int i = v.Count - 4;
                if (i >= 0) { tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i + 3); }
            }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "canopy" };
            mesh.SetVertices(v); mesh.SetTriangles(tri, 0);
            var n = new Vector3[v.Count];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.back;
            mesh.normals = n;
            mesh.uv = new Vector2[v.Count];
            mesh.RecalculateBounds();
            CbArt.Part(mesh, CbArt.Emissive(CbArt.Hex(hex) * 0.9f), far, new Vector3(0, 0, z), Vector3.one, shadows: false);
        }

        void BuildForeground(Transform far)
        {
            var dark = CbArt.Emissive(CbArt.Hex("#13221a"));
            bool leaves = Has(LeavesModel);
            for (float x = -20; x < 150; x += Random.Range(14f, 22f))
            {
                if (leaves)
                {
                    // big leaves in shade right in front of the camera frame the bottom of the view
                    Place(LeavesModel, far, new Vector3(x, -1.2f, -Random.Range(6.8f, 7.8f)), Random.Range(1f, 1.35f), 0f, ForegroundShade);
                    continue;
                }
                int n = Random.Range(3, 5);
                float z = -Random.Range(6.8f, 7.8f);
                for (int k = 0; k < n; k++)
                    CbArt.Part(CbMesh.Leaf(), dark, far, new Vector3(x + Random.Range(-0.8f, 0.8f), -1.3f, z), Vector3.one * Random.Range(0.6f, 1f),
                        CbArt.Rot(0, 0, (k / (float)(n - 1) - 0.5f) * 1.8f + Random.Range(-0.15f, 0.15f)), shadows: false);
            }
            for (float x = -10; x < 150; x += Random.Range(18f, 28f))
            {
                float len = Random.Range(3f, 5f);
                CbArt.Cylinder(dark, far, new Vector3(x, 11 - len / 2, -5.5f), 0.06f, len);
                for (int k = 0; k < 3; k++)
                    CbArt.Part(CbMesh.Leaf(), dark, far, new Vector3(x, 11 - len * (0.3f + k * 0.3f), -5.5f), Vector3.one * 0.35f,
                        CbArt.Rot(0, 0, Mathf.PI + (k % 2 == 1 ? 0.7f : -0.7f)), shadows: false);
            }
        }

        // ---------- light shafts, mist, fireflies ----------

        void BuildLightAndAir()
        {
            for (float x = -20; x < 150; x += Random.Range(7f, 12f))
            {
                var holder = CbArt.Joint(root, x, 15, Random.Range(4f, 16f), "shaft");
                holder.localRotation = CbArt.Rot(0, 0, Random.Range(0.42f, 0.55f));
                var m = CbArt.Glow(new Color(1f, 0.96f, 0.78f, 0.12f), CbArt.Shaft);
                CbArt.Part(CbMesh.Quad, m, holder, new Vector3(0, -14, 0), new Vector3(Random.Range(1.6f, 3.2f), 28, 1), shadows: false);
                shafts.Add((holder, m, Random.Range(0f, 6f), Random.Range(0.06f, 0.12f)));
            }
            for (int i = 0; i < 16; i++)
            {
                float s = Random.Range(24f, 46f);
                var m = CbArt.Part(CbMesh.Quad, CbArt.Glow(new Color(0.95f, 0.97f, 0.9f, Random.Range(0.07f, 0.14f)), CbArt.Mist), root,
                    new Vector3(Random.Range(-60f, 180f), Random.Range(0.8f, 4f), Random.Range(12f, 45f)), new Vector3(s, s * 0.28f, 1), shadows: false);
                mists.Add(m);
            }
            // No sun disc: its glow sat just above the view and blew up into a big yellow flash through bloom
            // whenever a jump or a shake tilted the camera up. The light shafts carry the sunlight.

            // fireflies drift around the camera
            var go = new GameObject("fireflies");
            go.transform.SetParent(root, false);
            flies = go.transform;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.startLifetime = 6f; main.startSpeed = 0.15f; main.startSize = 0.14f; main.maxParticles = 300;
            main.startColor = new Color(0.85f, 1f, 0.48f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 40f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(44, 6, 16);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = CbArt.Sparks;
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
        }

        public void Update(float t, float camX)
        {
            foreach (var s in shafts)
            {
                var c = s.m.GetColor("_Color");
                c.a = s.baseA * (0.75f + Mathf.Sin(t * 0.7f + s.ph) * 0.25f);
                s.m.SetColor("_Color", c);
            }
            foreach (var m in mists)
            {
                var p = m.localPosition;
                p.x += Time.deltaTime * 0.8f;
                if (p.x > camX + 120) p.x = camX - 120;
                m.localPosition = p;
            }
            flies.localPosition = new Vector3(camX, 3.2f, 3.5f);
            if (backdrop != null)
            {
                var p = backdrop.localPosition;
                p.x = camX;
                backdrop.localPosition = p;
                backdropMat.mainTextureOffset = new Vector2(camX * BackdropSlide / (BackdropWidth / backdropMat.mainTextureScale.x), 0);
            }
        }

        // ---------- merging ----------

        const float ChunkWidth = 24f;

        /// <summary>
        /// Merges every mesh under <paramref name="group"/> into one mesh per material and stretch of the level
        /// (ChunkWidth wide), then removes the parts. The camera skips the stretches out of view.
        /// </summary>
        static void Bake(Transform group, bool shadows)
        {
            var batches = new Dictionary<(Material, int), List<CombineInstance>>();
            var toLocal = group.worldToLocalMatrix;
            foreach (var mf in group.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                var key = (mr.sharedMaterial, Mathf.FloorToInt(mf.transform.position.x / ChunkWidth));
                if (!batches.TryGetValue(key, out var list)) batches[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
            }
            for (int i = group.childCount - 1; i >= 0; i--) Object.Destroy(group.GetChild(i).gameObject);
            foreach (var kv in batches) MakeBatch(kv.Key.Item1, kv.Value, group, shadows);
        }

        static void MakeBatch(Material m, List<CombineInstance> list, Transform parent, bool shadows)
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "batch_" + m.name };
            mesh.CombineMeshes(list.ToArray(), true, true);
            mesh.RecalculateBounds();
            var t = CbArt.Part(mesh, m, parent, Vector3.zero, Vector3.one, shadows: shadows);
            t.name = "batch";
        }
    }
}
