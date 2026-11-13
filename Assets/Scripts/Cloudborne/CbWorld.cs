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
        // the stage runs from LevelMin to LevelMax; it is cleared at the shrine gate at GoalX, once every fight is won
        public const float LevelMin = -8f, LevelMax = 554f, GoalX = 548f;
        // the scenery runs on a little past the end of the stage
        const float End = 574f;
        // the blue-teal haze of the painted backdrop: distant scenery fades into it, not into white
        public static readonly Color Horizon = CbArt.Hex("#7fc0cc");

        public Transform root;
        // The clean painted look (the user's reference: a bright storybook platformer): a soft misty backdrop, a row
        // of big painted trees, a hedge of bushes and flowers right behind the path, a thick grass lip on the ground's
        // front edge. On when its paintings are in the project. (Leaves hung into the top of the view too, held in front
        // of the camera: they jumped with every jump, and the user had them taken out.)
        const string LipPath = "Cloudborne/Jungle/Ground/GroundLip",
            WaterSidePath = "Cloudborne/Jungle/Water/WaterSide", WaterSurfacePath = "Cloudborne/Jungle/Water/WaterSurface", ClumpPath = "Cloudborne/Jungle/Backdrop/BushClump";
        // the painted river cross-section: SideDepth tall (all of the painting), its foam crests SideCrest over the
        // water's surface, repeating every SideTile metres
        const float SideDepth = 3.4f, SideCrest = 0.23f, SideTile = SideDepth * 1563f / 846f;
        // the water-front shader's quad reaches this far over the still surface (its highest wave is about 0.19)
        const float FrontAbove = 0.25f;
        // a rounded clump of bushes where the hedge meets a river, ClumpHeight tall
        const float ClumpHeight = 2.1f;
        static bool NewLook => Resources.Load<Texture2D>(LipPath) != null;
        // the painted ground drawn as painted (unlit), a touch darker so the characters stand out from it
        static readonly Color PaintTint = new Color(0.93f, 0.92f, 0.95f);
        readonly List<(Transform t, Material m, float ph, float baseA)> shafts = new List<(Transform, Material, float, float)>();
        readonly List<Transform> mists = new List<Transform>();
        Transform flies;
        static readonly string[] Greens = { "#2e7a3c", "#3c8f46", "#256a36", "#4a9a4c", "#1f5e33" };
        // the scenery models, made by Tools/Blender/meshy_prop.py with their foot at the origin, sized in metres
        const string TreeModel = "Tree", PalmModel = "Palm", FernModel = "Fern", LeavesModel = "Leaves",
            BoulderModel = "Boulder", PillarModel = "Pillar", GateModel = "Gate", KarstModel = "Karst";
        // The backdrop is a painting on a wall far behind everything. It follows the camera along the level and its
        // picture slides only a little, so it reads as very far away. The painting (ImageHeight tall, its top at
        // ImageTop) is the soft, misty forest of tall pale trunks and sunbeams, its clearing at about eye level and
        // its top above the view; the wall goes on up to BackdropTop with the painting's top row, so no screen shape
        // ever sees past it.
        const string BackdropPath = "Cloudborne/Jungle/Backdrop/JungleBackdrop";
        // The stage changes scenery on the way: the jungle, then a waterfall gorge from GorgeAt, then temple ruins at
        // sunset from TempleAt, each painting cross-fading into the next over BackdropFade metres. A part whose
        // painting isn't in the project keeps the one before.
        static readonly (string path, float from)[] BackdropParts =
        {
            ("Cloudborne/Jungle/Backdrop/JungleBackdrop", float.MinValue),
            ("Cloudborne/Jungle/Backdrop/GorgeBackdrop", 76f),
            ("Cloudborne/Jungle/Backdrop/TempleBackdrop", 110f),
        };
        const float BackdropFade = 12f;
        readonly List<(Texture2D tex, float from)> paintings = new List<(Texture2D, float)>();
        int shownPart = -1;
        const float BackdropZ = 150f, ImageTop = 44f, ImageHeight = 46f, BackdropTop = 80f, BackdropWidth = 300f,
            BackdropStretch = 1.5f, BackdropSlide = 0.15f;
        Transform backdrop;
        Material backdropMat;
        static readonly Color ForegroundShade = new Color(0.16f, 0.24f, 0.2f);
        // The painted textures are bright, and the toon light doubles them in the sun: plain, they glow lime green.
        // Toned down a little towards blue they sit behind the characters, like the painting.
        public static readonly Color SceneryTone = new Color(0.66f, 0.64f, 0.7f);
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

        // The ground is a platform, as in 2.5D side-scrollers: its grassy top at y = 0 with the path along its front
        // edge, and a front face of earth from that edge down out of view.
        const float GroundFront = -1.7f, GroundBack = 10f, GroundDepth = 10f;
        // painted ground textures drawn in Canva; until they are in the project the speckled ones are used
        const string GroundDir = "Cloudborne/Jungle/Ground/";
        const string GroundFacePath = GroundDir + "GroundFace";
        // the painted grass repeats every GrassTile metres, the path every PathWidth (it is square)
        public const float GrassTile = 4f;
        const float PathWidth = 3.2f;
        // the painted path is a strong orange; toned like the rest of the ground
        static readonly Color PathTone = new Color(0.86f, 0.8f, 0.78f);
        // the painted face shows its full height over FaceHeight metres, then repeats its bottom row (dark earth)
        public const float FaceHeight = 3.5f;
        // the ground runs from GroundFrom to GroundTo, cut where the streams cross it
        const float GroundFrom = -80f, GroundTo = End + 50f;
        readonly List<Material> waterMats = new List<Material>();
        // the painted cross-sections at the streams' front edges, their foam crests rolling along and rising and falling
        // (they stood still like a picture: the user)
        readonly List<(Material m, Transform t, float x, float y)> waterSides = new List<(Material, Transform, float, float)>();

        /// <summary>How wide one copy of the painted soil is (it keeps its proportions, FaceHeight tall).</summary>
        public static float FaceTileWidth(Texture tex) => tex != null && tex.height > 0 ? FaceHeight * tex.width / (float)tex.height : 4f;

        /// <summary>The soil of the ground's front face (the painting if it's in the project), for any earth face.</summary>
        public static Material FaceMaterial()
        {
            var painted = Resources.Load<Texture2D>(GroundFacePath);
            Material face;
            if (painted != null)
            {
                painted.wrapModeU = TextureWrapMode.Repeat;
                painted.wrapModeV = TextureWrapMode.Clamp;
                face = CbArt.NewToon(SceneryTone, 0f, painted);
            }
            else face = CbArt.NewToon(GroundTone, 0f, CbArt.Soil);
            face.SetFloat("_SpecStrength", 0f);
            face.SetFloat("_RimStrength", 0f);
            return face;
        }

        /// <summary>The ground's grass (the painting if it's in the project), for the top of any earth.</summary>
        public static Material GrassMaterial()
        {
            var painted = Resources.Load<Texture2D>(GroundDir + "Grass");
            var grass = CbArt.NewToon(GroundTone, 0f, painted != null ? painted : CbArt.Grass);
            grass.SetFloat("_SpecStrength", 0f);
            return grass;
        }

        // ---------- painted cards and layers ----------

        // Painted in Canva and cut out of their white background (Resources/Cloudborne/Jungle/Cards and Backdrop):
        // drawn in place of the code-built grass and the far rows of 3D trees when they are in the project.
        const string CardDir = "Cloudborne/Jungle/Cards/";
        static readonly string[] GrassCards = { "Grass1", "Grass2", "Grass3", "Grass4", "Grass5" };
        static readonly string[] FlowerCards = { "Flower1", "Flower2", "Flower3", "Flower4", "Flower5" };
        static Shader cutout;
        // how far the tops of the grass and flower cards sway in the breeze, in metres
        const float CardSway = 0.05f;

        /// <summary>An unlit cut-out material for a painting with a clear background; null if it can't be drawn.</summary>
        /// <summary>
        /// The streams' painted surface (the new look) for a stretch w metres wide and depth deep, its ripples drawn
        /// out in depth; null without the painting.
        /// </summary>
        public static Material WaterSurface(float w, float depth)
        {
            var tex = NewLook ? Resources.Load<Texture2D>(WaterSurfacePath) : null;
            if (tex == null) return null;
            tex.wrapMode = TextureWrapMode.Repeat;
            var m = Cutout(tex, 0f, PaintTint);
            m.SetFloat("_Cutoff", 0f);
            m.mainTextureScale = new Vector2(w / 5f, depth / 10f);
            return m;
        }

        /// <summary>A painting cut out of its background (unlit), hazed by haze toward the horizon; null if it can't be drawn.</summary>
        public static Material Painted(Texture2D tex, float haze = 0f) => Cutout(tex, haze);

        static Material Cutout(Texture2D tex, float haze = 0f, Color? tint = null)
        {
            if (cutout == null) cutout = Resources.Load<Shader>("Shaders/PaintedCutout");
            if (tex == null || cutout == null || !cutout.isSupported) return null;
            var m = new Material(cutout) { mainTexture = tex, name = tex.name };
            m.SetColor("_Color", tint ?? Color.white);
            m.SetColor("_HazeColor", Horizon);
            m.SetFloat("_Haze", haze);
            m.SetFloat("_Cutoff", 0.5f);
            return m;
        }

        /// <summary>One of the painted flower cards (cut out, unlit) and its width over height; null if it isn't in the project.</summary>
        public static (Material m, float aspect)? FlowerCard(int i)
        {
            var tex = Resources.Load<Texture2D>(CardDir + FlowerCards[((i % FlowerCards.Length) + FlowerCards.Length) % FlowerCards.Length]);
            var m = Cutout(tex, 0f, new Color(0.9f, 0.9f, 0.9f));
            return m != null ? (m, tex.width / (float)tex.height) : ((Material, float)?)null;
        }

        /// <summary>The grass and flower cards in the project: their material, width over height, and whether a flower.</summary>
        static List<(Material m, float aspect, bool flower)> LoadCards()
        {
            var list = new List<(Material, float, bool)>();
            foreach (var (names, flower) in new[] { (GrassCards, false), (FlowerCards, true) })
                foreach (var n in names)
                {
                    var tex = Resources.Load<Texture2D>(CardDir + n);
                    // a little darker and bluer, like the rest of the painted ground under the toon light
                    var m = Cutout(tex, 0f, new Color(0.86f, 0.88f, 0.86f));
                    if (m == null) continue;
                    // their tops sway in the breeze (the jungle stood still)
                    m.SetFloat("_Sway", CardSway);
                    list.Add((m, tex.width / (float)tex.height, flower));
                }
            return list;
        }

        /// <summary>
        /// A painted layer of jungle behind the path (cut out above the treetops), a strip along the whole stage at depth
        /// z, height metres tall from bottom, repeated along it; the perspective camera gives it its parallax, and the
        /// farther it is the more it fades into the haze. False if its painting isn't in the project.
        /// </summary>
        bool BuildLayer(string name, float z, float bottom, float height, float haze, List<(float a, float b)> spans = null, Color? tint = null)
        {
            var tex = Resources.Load<Texture2D>("Cloudborne/Jungle/Backdrop/" + name);
            var m = Cutout(tex, haze, tint);
            if (m == null) return false;
            tex.wrapModeU = TextureWrapMode.Repeat;
            tex.wrapModeV = TextureWrapMode.Clamp;
            float tileW = height * tex.width / tex.height;
            spans ??= new List<(float, float)> { (-120f, End + 120f) };
            foreach (var (from, to) in spans)
            {
                // each piece lined up with the world, so the painting runs on unbroken where pieces meet
                float w = to - from;
                var piece = new Material(m) { mainTextureScale = new Vector2(w / tileW, 1), mainTextureOffset = new Vector2(from / tileW, 0) };
                var t = CbArt.Part(CbMesh.Quad, piece, root, new Vector3((from + to) / 2, bottom + height / 2, z), new Vector3(w, height, 1), shadows: false);
                t.name = name;
            }
            return true;
        }

        /// <summary>The ground from <paramref name="from"/> to <paramref name="to"/> cut only by the streams and rivers
        /// (not the pits), each stretch pulled back from the banks by <paramref name="margin"/>.</summary>
        static List<(float a, float b)> BankToBank(float from, float to, float margin)
        {
            var list = new List<(float, float)>();
            float a = from;
            foreach (var (x0, x1) in CbLevel.Waters) { list.Add((a, x0 - margin)); a = x1 + margin; }
            list.Add((a, to));
            return list;
        }

        /// <summary>The ground behind the path where the bushes stand: all of it but the streams and rivers.</summary>
        static List<(float a, float b)> BushGround() => BankToBank(-120f, End + 120f, 0.3f);

        bool layers;

        /// <summary>Whether the painted jungle layers are in the project (then the far rows of 3D trees aren't needed).</summary>
        static bool PaintedLayers => Resources.Load<Texture2D>("Cloudborne/Jungle/Backdrop/JungleNear") != null
            || Resources.Load<Texture2D>("Cloudborne/Jungle/Backdrop/JungleMid") != null;

        /// <summary>The stretches of dry ground between the streams.</summary>
        static List<(float a, float b)> DryGround()
        {
            var list = new List<(float, float)>();
            float a = GroundFrom;
            // the streams, and the pits hidden in the temple (their spikes below)
            foreach (var (x0, x1) in CbLevel.Gaps) { list.Add((a, x0)); a = x1; }
            list.Add((a, GroundTo));
            return list;
        }

        void BuildGround(Transform far)
        {
            // the ground is laid in stretches between the streams, each painting lined up with the world
            var grass = GrassMaterial();
            var paintedPath = Resources.Load<Texture2D>(GroundDir + "Path");
            var path = CbArt.NewToon(paintedPath != null ? PathTone : Color.white, 0f, paintedPath != null ? paintedPath : CbArt.Path);
            path.SetFloat("_SpecStrength", 0f);
            var face = FaceMaterial();
            // the new look: the front face drawn as painted, and its grass lip of leaves and flowers over the edge
            var lipTex = NewLook ? Resources.Load<Texture2D>(LipPath) : null;
            var lip = lipTex != null ? Cutout(lipTex, 0f, PaintTint) : null;
            if (lip != null)
            {
                var flat = Cutout(face.mainTexture as Texture2D, 0f, PaintTint);
                if (flat != null) { flat.SetFloat("_Cutoff", 0f); face = flat; }
                lipTex.wrapModeU = TextureWrapMode.Repeat;
                lipTex.wrapModeV = TextureWrapMode.Clamp;
            }
            foreach (var (a, b) in DryGround())
            {
                float w = b - a, c = (a + b) / 2;
                var top = new Material(grass) { mainTextureScale = new Vector2(w / GrassTile, (GroundBack - GroundFront) / GrassTile), mainTextureOffset = new Vector2(a / GrassTile, 0) };
                CbArt.Box(top, root, new Vector3(c, -GroundDepth / 2, (GroundFront + GroundBack) / 2), new Vector3(w, GroundDepth, GroundBack - GroundFront));
                var strip = new Material(path) { mainTextureScale = new Vector2(paintedPath != null ? w / PathWidth : w / 4.3f, 1), mainTextureOffset = new Vector2(a / PathWidth, 0) };
                CbArt.Part(CbMesh.Quad, strip, root, new Vector3(c, 0.01f, 0), new Vector3(w, 3.2f, 1), CbArt.Rot(Mathf.PI / 2), shadows: false);
            }
            // the front face and its lip run on across the hidden pits (the trap showed from the side, the user saw
            // a box in the ground); behind each pit's hole the grass goes on
            foreach (var (a, b) in BankToBank(GroundFrom, GroundTo, 0f))
            {
                FaceQuad(face, a, b, 0f, -GroundDepth);
                if (lip != null) LipQuad(lip, a, b);
            }
            foreach (var (x0, x1) in CbLevel.Pits)
            {
                var behind = new Material(grass) { mainTextureScale = new Vector2((x1 - x0) / GrassTile, (GroundBack - CbLevel.PitBack) / GrassTile), mainTextureOffset = new Vector2(x0 / GrassTile, 0) };
                CbArt.Box(behind, root, new Vector3((x0 + x1) / 2, -GroundDepth / 2, (CbLevel.PitBack + GroundBack) / 2), new Vector3(x1 - x0, GroundDepth, GroundBack - CbLevel.PitBack));
            }
            BuildStreams(grass, face, far);
            CbArt.Part(CbMesh.Quad, CbArt.Toon("#3a7a33"), root, new Vector3(130, -0.3f, 68), new Vector3(700, 120, 1), CbArt.Rot(Mathf.PI / 2), shadows: false);

            // grass blades and fern fronds, merged straight into a few meshes
            var blade = CbMesh.Cone(3);
            var frond = CbMesh.Cone(4);
            var batches = new Dictionary<Material, List<CombineInstance>>();
            void Add(Mesh mesh, Material m, Matrix4x4 mat)
            {
                if (!batches.TryGetValue(m, out var list)) batches[m] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = mat });
            }
            var cards = LoadCards();
            if (cards.Count > 0)
            {
                // painted tufts of grass and little flowering plants (Canva, cut out) behind the path and leaning over
                // its front edge, turned a little this way and that, some mirrored
                for (int i = 0; i < 1500; i++)
                {
                    bool front = Random.value < 0.18f;
                    var p = new Vector3(Random.Range(-40f, End), 0, front ? Random.Range(GroundFront + 0.05f, GroundFront + 0.35f) : Random.Range(1.7f, 9.5f));
                    if (Wet(p.x, p.z)) continue;
                    // grass mostly, a flower now and then
                    bool wantFlower = Random.value < 0.2f;
                    var pick = cards.FindAll(c => c.flower == wantFlower);
                    if (pick.Count == 0) pick = cards;
                    var (m, aspect, flower) = pick[Random.Range(0, pick.Count)];
                    float h = (flower ? Random.Range(0.3f, 0.5f) : Random.Range(0.4f, 0.8f)) * (front ? 0.8f : 1f);
                    var at = p + new Vector3(0, h * 0.48f, 0);
                    Add(CbMesh.Quad, m, Matrix4x4.TRS(at, Quaternion.Euler(0, Random.Range(-25f, 25f), 0), new Vector3(h * aspect * (Random.value < 0.5f ? -1 : 1), h, 1)));
                }
            }
            var grassMats = new[] { CbArt.Toon("#357a33"), CbArt.Toon("#468a3c"), CbArt.Toon("#2a6a30"), CbArt.Toon("#559443") };
            for (int i = 0; i < (cards.Count > 0 ? 0 : 3600); i++)
            {
                var p = new Vector3(Random.Range(-40f, End), 0, Random.Range(1.8f, 9.5f));
                if (Wet(p.x, p.z)) continue;
                var q = Quaternion.Euler(Random.Range(-17f, 17f), Random.Range(0f, 360f), Random.Range(-17f, 17f));
                float k = Random.Range(0.6f, 1.6f);
                Add(blade, grassMats[i % 4], Matrix4x4.TRS(p, q, new Vector3(0.05f * k, 0.42f * k, 0.05f * k)));
            }
            if (Has(FernModel))
            {
                // a few small ferns lean over the front edge; the rest grow behind the path
                for (int c = 0; c < 240; c++)
                {
                    bool front = Random.value < 0.25f;
                    var at = new Vector3(Random.Range(-40f, End), 0, front ? Random.Range(GroundFront + 0.1f, GroundFront + 0.4f) : Random.Range(2.2f, 9.5f));
                    if (Wet(at.x, at.z)) continue;
                    Place(FernModel, far, at, front ? Random.Range(0.45f, 0.6f) : Random.Range(0.8f, 1.3f));
                }
            }
            var fernMats = new[] { CbArt.Toon("#2c6e30"), CbArt.Toon("#3a8a3c"), CbArt.Toon("#245c2a") };
            for (int c = 0; c < (Has(FernModel) ? 0 : 600); c++)
            {
                bool front = Random.value < 0.3f;
                var center = new Vector3(Random.Range(-40f, End), 0, front ? Random.Range(GroundFront + 0.1f, GroundFront + 0.4f) : Random.Range(2.2f, 9.5f));
                if (Wet(center.x, center.z)) continue;
                float ck = front ? Random.Range(0.4f, 0.55f) : Random.Range(0.8f, 1.4f);
                for (int f = 0; f < 7; f++)
                {
                    float yaw = f / 7f * 360f + Random.Range(-12f, 12f);
                    var q = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(Random.Range(40f, 66f), 0, 0);
                    Add(frond, fernMats[(c + f) % 3], Matrix4x4.TRS(center, q, new Vector3(0.14f * ck, 1.1f * ck * Random.Range(0.9f, 1.3f), 0.14f * ck * 0.25f)));
                }
            }
            foreach (var kv in batches) MakeBatch(kv.Key, kv.Value, root, false);
        }

        /// <summary>
        /// A stretch of the ground's front face from x a to b, between heights yTop and yBottom: the painted
        /// cross-section of earth (its grassy top row at y = 0, lined up with the world), else speckled soil.
        /// </summary>
        void FaceQuad(Material face, float a, float b, float yTop, float yBottom)
        {
            float w = b - a, h = yTop - yBottom;
            float tileW = FaceTileWidth(face.mainTexture);
            // as if the face ran from y = 0 down to -GroundDepth, so the earth lines up whatever piece of it this is
            float rows = GroundDepth / FaceHeight;
            var m = new Material(face)
            {
                mainTextureScale = new Vector2(w / tileW, rows * h / GroundDepth),
                mainTextureOffset = new Vector2(a / tileW, 1 - rows),
            };
            var t = CbArt.Part(CbMesh.Quad, m, root, new Vector3((a + b) / 2, (yTop + yBottom) / 2, GroundFront - 0.01f), new Vector3(w, h, 1), shadows: false);
            t.name = "groundFace";
        }

        // the lip: drawn a little smaller than the face, so the leaves rise only a hand's breadth over the path's edge
        const float LipScale = 0.7f;

        /// <summary>The leaves and flowers of the grass lip along the front edge from a to b, over the face's top.</summary>
        void LipQuad(Material lip, float a, float b)
        {
            var tex = lip.mainTexture;
            var faceTex = Resources.Load<Texture2D>(GroundFacePath);
            // metres per pixel of the face, as FaceQuad draws it
            float mpp = faceTex != null ? FaceHeight / faceTex.height : 0.0055f;
            float h = tex.height * mpp * LipScale, tileW = tex.width * mpp * LipScale;
            // the painting's last 40 rows are the face's solid grass: they overlap the face's top
            float below = 40 * mpp * LipScale;
            var m = new Material(lip) { mainTextureScale = new Vector2((b - a) / tileW, 1), mainTextureOffset = new Vector2(a / tileW, 0) };
            var t = CbArt.Part(CbMesh.Quad, m, root, new Vector3((a + b) / 2, h / 2 - below, GroundFront - 0.03f), new Vector3(b - a, h, 1), shadows: false);
            t.name = "groundLip";
        }

        /// <summary>
        /// The streams cut across the ground: a channel with earth banks, the water's surface below them flowing towards
        /// the camera, the river seen in cross-section at the front edge, and behind, where it comes out of the ground,
        /// a little cascade framed by leaves.
        /// </summary>
        void BuildStreams(Material grass, Material face, Transform far)
        {
            var paintedWater = Resources.Load<Texture2D>("Cloudborne/Jungle/Water/Water");
            var waterTex = paintedWater != null ? paintedWater : WaterTexture();
            waterTex.wrapMode = TextureWrapMode.Repeat;
            float level = CbLevel.WaterLevel, back = CbLevel.WaterBack, bed = level - 0.55f;
            var surfaceTex = NewLook ? Resources.Load<Texture2D>(WaterSurfacePath) : null;
            var paintedSurface = surfaceTex != null ? Cutout(surfaceTex, 0f, PaintTint) : null;
            if (paintedSurface != null)
            {
                surfaceTex.wrapMode = TextureWrapMode.Repeat;
                paintedSurface.SetFloat("_Cutoff", 0f);
            }
            // the streams' front: see-through water with rolling waves, drawn by a shader of its own (the painted
            // cross-section stood still like a picture, and then looked wrong moving: the user)
            var frontShader = Resources.Load<Shader>("Shaders/WaterFront");
            Material front = null;
            if (frontShader != null && frontShader.isSupported)
            {
                front = new Material(frontShader) { name = "waterFront", renderQueue = (int)RenderQueue.Transparent };
                front.SetFloat("_Level", level);
            }
            var sideTex = NewLook && front == null ? Resources.Load<Texture2D>(WaterSidePath) : null;
            var paintedSide = sideTex != null ? Cutout(sideTex, 0f, PaintTint) : null;
            if (paintedSide != null)
            {
                sideTex.wrapModeU = TextureWrapMode.Repeat;
                sideTex.wrapModeV = TextureWrapMode.Clamp;
            }
            foreach (var (x0, x1) in CbLevel.Waters)
            {
                float w = x1 - x0, c = (x0 + x1) / 2;
                // the ground behind the channel's end, and under it
                var behind = new Material(grass) { mainTextureScale = new Vector2(w / GrassTile, (GroundBack - back) / GrassTile), mainTextureOffset = new Vector2(x0 / GrassTile, 0) };
                CbArt.Box(behind, root, new Vector3(c, -GroundDepth / 2, (back + GroundBack) / 2), new Vector3(w, GroundDepth, GroundBack - back));
                CbArt.Box(CbArt.Toon("#2d4a3a"), root, new Vector3(c, bed - 1f, (GroundFront + back) / 2), new Vector3(w, 2f, back - GroundFront));
                // the banks: earth walls from the grass down into the water
                var bank = new Material(face);
                float tileW = FaceTileWidth(face.mainTexture), rows = 1.2f / FaceHeight;
                bank.mainTextureScale = new Vector2((back - GroundFront) / tileW, rows);
                bank.mainTextureOffset = new Vector2(0, 1 - rows);
                // (a hair inside the channel, so they don't flicker against the ground's own sides)
                CbArt.Part(CbMesh.Quad, bank, root, new Vector3(x0 + 0.01f, -0.6f, (GroundFront + back) / 2), new Vector3(back - GroundFront, 1.2f, 1), Quaternion.Euler(0, -90, 0), shadows: false);
                CbArt.Part(CbMesh.Quad, bank, root, new Vector3(x1 - 0.01f, -0.6f, (GroundFront + back) / 2), new Vector3(back - GroundFront, 1.2f, 1), Quaternion.Euler(0, 90, 0), shadows: false);
                // the water: its surface, flowing towards the camera
                Material surface;
                if (paintedSurface != null)
                {
                    // the new look: drawn as painted, in the cross-section's turquoise, its ripples drawn out in depth
                    // (seen this low, a finer repeat blurred into a flat stripe)
                    surface = new Material(paintedSurface) { mainTextureScale = new Vector2(w / 5f, (back - GroundFront) / 10f) };
                }
                else
                {
                    surface = CbArt.NewToon(new Color(0.9f, 0.97f, 1f), 0f, waterTex);
                    surface.SetFloat("_SpecStrength", 0.35f);
                    surface.mainTextureScale = new Vector2(w / 3f, (back - GroundFront) / 3f);
                }
                waterMats.Add(surface);
                CbArt.Part(CbMesh.Quad, surface, root, new Vector3(c, level, (GroundFront + back) / 2), new Vector3(w, back - GroundFront, 1), CbArt.Rot(Mathf.PI / 2), shadows: false).name = "water";
                // its cross-section at the front edge: the new look's painting (foamy crests, light rays, pebbles on
                // sand, as deep as the soil it replaces), or deeper and darker towards the bed with a light line on top
                float under = bed;
                if (front != null)
                {
                    // from above the highest wave down to as deep as the painted cross-section went
                    float top = level + FrontAbove, bottom = level - SideDepth;
                    CbArt.Part(CbMesh.Quad, front, root, new Vector3(c, (top + bottom) / 2, GroundFront - 0.012f), new Vector3(w, top - bottom, 1), shadows: false).name = "waterSide";
                    under = bottom;
                }
                else if (paintedSide != null)
                {
                    float top = level + SideCrest, depth = SideDepth;
                    var side = new Material(paintedSide) { mainTextureScale = new Vector2(w / SideTile, 1), mainTextureOffset = new Vector2(x0 / SideTile, 0) };
                    var sideQuad = CbArt.Part(CbMesh.Quad, side, root, new Vector3(c, top - depth / 2, GroundFront - 0.012f), new Vector3(w, depth, 1), shadows: false);
                    sideQuad.name = "waterSide";
                    waterSides.Add((side, sideQuad, x0 / SideTile, top - depth / 2));
                    under = top - depth;
                }
                else CbArt.Part(CbMesh.Quad, WaterSide(), root, new Vector3(c, (level + bed) / 2, GroundFront - 0.012f), new Vector3(w, level - bed, 1), shadows: false).name = "waterSide";
                // and the soil below
                FaceQuad(face, x0, x1, under, -GroundDepth);
                // where it comes out of the ground: a little cascade between leaves
                Material cascade;
                if (paintedSurface != null) cascade = new Material(paintedSurface) { mainTextureScale = new Vector2(w / 3f, 0.4f) };
                else
                {
                    cascade = CbArt.NewToon(new Color(0.95f, 1f, 1f), 0f, waterTex);
                    cascade.SetFloat("_SpecStrength", 0f);
                    cascade.mainTextureScale = new Vector2(w / 3f, 0.4f);
                }
                waterMats.Add(cascade);
                CbArt.Part(CbMesh.Quad, cascade, root, new Vector3(c, level / 2, back - 0.01f), new Vector3(w, -level, 1), shadows: false).name = "cascade";
                if (Has(LeavesModel) && !NewLook)
                    for (float x = x0 - 0.4f; x <= x1 + 0.5f; x += Mathf.Max(1.4f, w / 6f))
                        Place(LeavesModel, far, new Vector3(x + Random.Range(-0.3f, 0.3f), 0, back + Random.Range(0.1f, 0.6f)), Random.Range(0.7f, 1f));
            }
        }

        /// <summary>A water texture drawn in code (until the painted one is approved): soft light ripples on teal.</summary>
        static Texture2D WaterTexture()
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "water" };
            Color deep = CbArt.Hex("#1d7f8f"), mid = CbArt.Hex("#2f9fa8"), light = CbArt.Hex("#bfece6");
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S * Mathf.PI * 2, v = y / (float)S * Mathf.PI * 2;
                    // overlapping soft swells; whole periods across the tile so it repeats seamlessly
                    float swell = Mathf.Sin(v * 2 + Mathf.Sin(u) * 0.8f) * 0.5f + Mathf.Sin(u * 3 + v + Mathf.Sin(v * 2) * 0.6f) * 0.3f;
                    float glint = Mathf.Sin(v * 5 + Mathf.Sin(u * 2 + v) * 1.2f) * Mathf.Sin(u * 4 - v * 2);
                    var col = Color.Lerp(deep, mid, 0.5f + 0.5f * swell);
                    if (glint > 0.72f) col = Color.Lerp(col, light, Mathf.Clamp01((glint - 0.72f) / 0.28f) * 0.8f);
                    px[y * S + x] = col;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static Material waterSide;

        /// <summary>The river's cross-section: a light line at the surface, then teal deepening to dark at the bed.</summary>
        static Material WaterSide()
        {
            if (waterSide != null) return waterSide;
            const int H = 32;
            var tex = new Texture2D(2, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "waterSide" };
            for (int y = 0; y < H; y++)
            {
                float k = y / (H - 1f);   // 0 bottom, 1 top
                var c = Color.Lerp(CbArt.Hex("#0e3f4a"), CbArt.Hex("#2a95a3"), k * k);
                if (k > 0.92f) c = CbArt.Hex("#c9f1ec");
                tex.SetPixel(0, y, c); tex.SetPixel(1, y, c);
            }
            tex.Apply();
            waterSide = CbArt.NewToon(Color.white, 0f, tex);
            waterSide.SetFloat("_SpecStrength", 0f);
            waterSide.SetFloat("_RimStrength", 0f);
            return waterSide;
        }

        // trees of a few kinds and shades, so the forest doesn't look copied and pasted
        static readonly string[] MoreTrees = { "Banyan", "Kapok", "Bamboo" };
        static readonly Color[] TreeTints = { new Color(0.66f, 0.64f, 0.7f), new Color(0.58f, 0.64f, 0.6f), new Color(0.72f, 0.66f, 0.62f) };

        void BuildPlants(Transform near, Transform far)
        {
            // the new look's painted trees and hedge stand in for the modelled palms and leaf clumps
            if (NewLook) return;
            bool trees = Has(TreeModel), palms = Has(PalmModel);
            // With the painted jungle layers behind the path no modelled trees stand in front of them: their faceted
            // crowns and tall pale trunks looked out of place against the painting, worst up the mountain where the
            // camera climbs among them (the user). Only palms, a little more often.
            bool painted = PaintedLayers;
            // the jungle tree and the banyan and kapok about as often as each other, the bamboo now and then
            var kinds = new List<string> { TreeModel, TreeModel };
            foreach (var k in MoreTrees)
                if (Has(k)) { kinds.Add(k); if (k != "Bamboo") kinds.Add(k); }
            for (float x = -30; x < End; x += Random.Range(6f, 10f))
            {
                // The level camera sees about 7 to 8 m up at the tree line: the modelled trees (9.5 m, crown from
                // about half way) and palms (7.5 m) are sized to show whole. Leafy models cast no shadows (far): a
                // thin leaf shadowing itself breaks up into dark specks.
                float z = Random.Range(7.5f, 11f);
                if (Wet(x, z, 1.5f)) continue;
                if (Random.value < (painted ? 0.5f : 0.35f))
                {
                    if (palms) Place(PalmModel, far, new Vector3(x, 0, z + 1f), Random.Range(0.65f, 0.8f));
                    else Palm(near, x, z, Random.Range(1f, 1.3f));
                }
                else if (painted) continue;
                else if (trees) Place(kinds[Random.Range(0, kinds.Count)], near, new Vector3(x, 0, z + 1f), Random.Range(0.62f, 0.78f), CbArt.PropOutline,
                    TreeTints[Random.Range(0, TreeTints.Length)]);
                else JungleTree(near, x, z, Random.Range(0.85f, 1.1f), CbArt.PropOutline);
            }
            if (trees && !painted)
            {
                // two rows of forest behind the first, so the ground between the trees and the horizon is not an
                // empty lawn; they fade into the haze with distance
                foreach (var (z0, z1, s0, s1, step) in new[] { (15f, 24f, 0.6f, 0.8f, 4.5f), (26f, 42f, 0.7f, 0.95f, 5f) })
                    for (float x = -40; x < End + 10; x += Random.Range(step, step * 1.6f))
                    {
                        var at = new Vector3(x, 0, Random.Range(z0, z1));
                        if (palms && Random.value < 0.25f) Place(PalmModel, far, at, Random.Range(s0, s1) * 1.1f);
                        else Place(TreeModel, far, at, Random.Range(s0, s1));
                    }
            }
            else if (!painted)
                for (float x = -30; x < End; x += Random.Range(7f, 11f))
                    JungleTree(far, x, Random.Range(17f, 28f), Random.Range(1.3f, 1.9f), 0f);
            // clumps of big leaves among the rocks between the path and the trees
            if (Has(LeavesModel))
                for (float x = -25; x < End; x += Random.Range(5f, 9f))
                {
                    float lx = x + Random.Range(-1f, 1f);
                    bool mountain = lx > CbLevel.MountainFrom - 1 && lx < CbLevel.MountainTo + 1;
                    if (!Wet(lx, 4f, 1f) && !mountain) Place(LeavesModel, far, new Vector3(lx, 0, Random.Range(2.8f, 6f)), Random.Range(0.75f, 1.15f));
                }
        }

        // ---------- modelled scenery ----------

        static string ModelPath(string name) => $"Cloudborne/Jungle/{name}/{name}";

        static bool Has(string name) => Resources.Load<GameObject>(ModelPath(name)) != null;

        /// <summary>Whether a spot near the path is in one of the streams (they run from the front edge back to z 10).</summary>
        static bool Wet(float x, float z, float margin = 0.5f) => z < CbLevel.WaterBack + 1f && CbLevel.InWater(x, margin);

        /// <summary>
        /// Models with thin leaves, seen from both sides (the boulders have sprigs of moss and leaves). They get no
        /// outline: the outline is drawn from the back faces, so a leaf showing its back would turn black.
        /// </summary>
        static bool Leafy(string name) => name == LeavesModel || name == FernModel || name == PalmModel || name == BoulderModel || name == TreeModel
            || System.Array.IndexOf(MoreTrees, name) >= 0;

        /// <summary>
        /// Places a copy of a scenery model standing at pos, its drawn front turned towards the camera give or take a
        /// little. Copies with the same look share one material, so the bake merges them.
        /// </summary>
        Transform Place(string name, Transform parent, Vector3 pos, float scale, float outline = 0f, Color? tint = null, float turn = 35f)
        {
            // Scenery has no outlines: they broke up into black patches through its leaves and vines, and painted
            // backgrounds read better without them (the characters keep theirs and stand out)
            outline = 0f;
            var key = (name, outline, tint ?? SceneryTone);
            if (!models.TryGetValue(key, out var model))
            {
                var path = ModelPath(name);
                var mat = CbArt.AnimeToon(Resources.Load<Texture2D>(path + "_Albedo"), outline);
                mat.color = key.Item3;
                // scenery takes a softer rim light than the characters, so they stand out in front of it
                mat.SetFloat("_RimStrength", 0.25f);
                // smooth shading for the scenery: light rolls gently into shade instead of one hard cel step
                mat.SetFloat("_BandSoftness", 0.35f);
                mat.SetColor("_ShadowColor", new Color(0.7f, 0.72f, 0.86f));
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
            for (float x = -20; x < End; x += Random.Range(4f, 8f))
            {
                float s = Random.Range(0.25f, 0.6f), z = Random.Range(2.4f, 4.5f);
                // the mountain stands here: its own rocks
                if (x > CbLevel.MountainFrom - 2 && x < CbLevel.MountainTo + 2) continue;
                if (Wet(x, z, 0.8f)) continue;
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
                Place(GateModel, near, new Vector3(121, 0, 4.2f), 1f, CbArt.PropOutline, turn: 8f);
                Place(PillarModel, near, new Vector3(110f, 0, 5f), 0.5f, CbArt.PropOutline, turn: 20f);
                Place(PillarModel, near, new Vector3(127, 0, 4.8f), 0.65f, CbArt.PropOutline, turn: 20f);
                Place(PillarModel, near, new Vector3(132, 0, 4f), 0.85f, CbArt.PropOutline, turn: 20f);
                foreach (var p in new[] { new Vector2(114, 3.2f), new Vector2(124, 3.6f), new Vector2(129, 3.1f) })
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
            // the painted jungle in layers: a near one of trunks and big leaves, a far one of hazy treetops
            // (the near one low enough that the far one and the sky still show over it)
            bool near, mid;
            if (NewLook)
            {
                // the new look: a row of big painted trees in a light haze, and right behind the path a hedge of
                // bushes and flowers (it hides the flat lawn that ran back to the trees), broken by the rivers
                mid = BuildLayer("JungleMid", 18f, -0.6f, 10.5f, 0.3f);
                var hedge = BushGround();
                near = BuildLayer("JungleNear", 3f, -0.15f, 1.9f, 0f, hedge, PaintTint);
                // and behind each river, where its channel ends: it seems to flow out of the bushes (a flat lawn and
                // the old leaf models showed there)
                var behind = new List<(float a, float b)>();
                foreach (var (x0, x1) in CbLevel.Waters) behind.Add((x0 - 1f, x1 + 1f));
                BuildLayer("JungleNear", CbLevel.WaterBack + 0.7f, -0.15f, 1.9f, 0f, behind, PaintTint);
                // where it meets a river, a rounded clump closes it (it ended in a straight cut)
                var clumpTex = Resources.Load<Texture2D>(ClumpPath);
                var clump = clumpTex != null ? Cutout(clumpTex, 0f, PaintTint) : null;
                if (near && clump != null)
                {
                    clumpTex.wrapMode = TextureWrapMode.Clamp;
                    float cw = ClumpHeight * clumpTex.width / clumpTex.height;
                    foreach (var (a, b) in hedge)
                        foreach (var (end, inward) in new[] { (a, 1f), (b, -1f) })
                        {
                            if (end < -100f || end > End + 100f) continue;
                            var t = CbArt.Part(CbMesh.Quad, clump, root, new Vector3(end + inward * (cw / 2 - 0.8f), ClumpHeight / 2 - 0.2f, 2.9f),
                                new Vector3(cw * (inward > 0 ? -1 : 1), ClumpHeight, 1), shadows: false);
                            t.name = "bushClump";
                        }
                }
            }
            else
            {
                near = BuildLayer("JungleNear", 17f, -0.6f, 8.5f, 0.08f);
                mid = BuildLayer("JungleMid", 34f, -1f, 16f, 0.32f);
            }
            layers = near || mid;
            if (NewLook)
            {
                if (BuildBackdrop()) return;
            }
            var fall = CbArt.Glow(new Color(0.9f, 0.97f, 1f, 0.55f), CbArt.Shaft);
            bool karst = Has(KarstModel);
            for (float x = -20; x < End + 20; x += Random.Range(15f, 24f))
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

        /// <summary>Which part of the stage the camera is in: that part's painting, fading into the next near its start.</summary>
        void ShowPart(float camX)
        {
            if (paintings.Count == 0) return;
            int i = 0;
            while (i + 1 < paintings.Count && camX >= paintings[i + 1].from - BackdropFade) i++;
            // i: the last part whose fade has begun; blend from the one before it
            float blend = i == 0 ? 1 : Mathf.Clamp01((camX - (paintings[i].from - BackdropFade)) / BackdropFade);
            var a = i == 0 ? paintings[0].tex : paintings[i - 1].tex;
            if (shownPart != i)
            {
                shownPart = i;
                backdropMat.SetTexture("_MainTex", a);
                backdropMat.SetTexture("_NextTex", paintings[i].tex);
            }
            backdropMat.SetFloat("_Blend", blend);
        }

        /// <summary>The painted backdrop in place of the canopy layers; false when the painting or its shader is missing.</summary>
        bool BuildBackdrop()
        {
            var tex = Resources.Load<Texture2D>(BackdropPath);
            var shader = Resources.Load<Shader>("Shaders/Backdrop");
            if (tex == null || shader == null || !shader.isSupported) return false;
            foreach (var (path, from) in BackdropParts)
            {
                var t = Resources.Load<Texture2D>(path);
                if (t == null) continue;
                // mirrored repeats join without a seam
                t.wrapModeU = TextureWrapMode.Mirror;
                t.wrapModeV = TextureWrapMode.Clamp;
                paintings.Add((t, from));
            }
            backdropMat = new Material(shader) { mainTexture = tex };
            float tile = ImageHeight * tex.width / tex.height * BackdropStretch;
            float height = BackdropTop - (ImageTop - ImageHeight);
            // the quad's bottom is the painting's bottom; above the painting's top the clamped texture repeats its sky
            backdropMat.mainTextureScale = new Vector2(BackdropWidth / tile, height / ImageHeight);
            backdrop = CbArt.Part(CbMesh.Quad, backdropMat, root, new Vector3(0, BackdropTop - height / 2, BackdropZ),
                new Vector3(BackdropWidth, height, 1), shadows: false);
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
            // the new look frames the view with its painted canopy, not dark leaves and vines in front of the camera
            if (NewLook) return;
            var dark = CbArt.Emissive(CbArt.Hex("#13221a"));
            bool leaves = Has(LeavesModel);
            for (float x = -20; x < End; x += Random.Range(14f, 22f))
            {
                if (leaves)
                {
                    // big leaves in shade right in front of the camera rise over the bottom of the view
                    Place(LeavesModel, far, new Vector3(x, -2.4f, -Random.Range(6.8f, 7.8f)), Random.Range(1f, 1.35f), 0f, ForegroundShade);
                    continue;
                }
                int n = Random.Range(3, 5);
                float z = -Random.Range(6.8f, 7.8f);
                for (int k = 0; k < n; k++)
                    CbArt.Part(CbMesh.Leaf(), dark, far, new Vector3(x + Random.Range(-0.8f, 0.8f), -1.3f, z), Vector3.one * Random.Range(0.6f, 1f),
                        CbArt.Rot(0, 0, (k / (float)(n - 1) - 0.5f) * 1.8f + Random.Range(-0.15f, 0.15f)), shadows: false);
            }
            for (float x = -10; x < End; x += Random.Range(18f, 28f))
            {
                // vines hang in from the top of the view (the level camera sees about 5 m up this close)
                float len = Random.Range(1.5f, 2.5f);
                CbArt.Cylinder(dark, far, new Vector3(x, 7 - len / 2, -5.5f), 0.06f, len);
                for (int k = 0; k < 3; k++)
                    CbArt.Part(CbMesh.Leaf(), dark, far, new Vector3(x, 7 - len * (0.3f + k * 0.3f), -5.5f), Vector3.one * 0.35f,
                        CbArt.Rot(0, 0, Mathf.PI + (k % 2 == 1 ? 0.7f : -0.7f)), shadows: false);
            }
        }

        // ---------- light shafts, mist, fireflies ----------

        void BuildLightAndAir()
        {
            for (float x = -20; x < End; x += Random.Range(7f, 12f))
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
                    new Vector3(Random.Range(-60f, End + 40f), Random.Range(0.8f, 4f), Random.Range(12f, 45f)), new Vector3(s, s * 0.28f, 1), shadows: false);
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
            // the streams flow towards the camera; their cascades pour down
            for (int i = 0; i < waterMats.Count; i++)
                waterMats[i].mainTextureOffset = new Vector2(0, (i % 2 == 0 ? -0.12f : 0.6f) * t);
            // the crests roll along (about 0.35 m a second) and swell and settle, each stream in its own time
            for (int i = 0; i < waterSides.Count; i++)
            {
                var (m, q, x, y) = waterSides[i];
                m.mainTextureOffset = new Vector2(x + t * 0.35f / SideTile, 0);
                var p = q.localPosition;
                p.y = y + Mathf.Sin(t * 1.7f + i * 1.3f) * 0.035f + Mathf.Sin(t * 2.9f + i) * 0.015f;
                q.localPosition = p;
            }
            if (backdrop != null)
            {
                var p = backdrop.localPosition;
                p.x = camX;
                backdrop.localPosition = p;
                backdropMat.mainTextureOffset = new Vector2(camX * BackdropSlide / (BackdropWidth / backdropMat.mainTextureScale.x), 0);
                ShowPart(camX);
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
