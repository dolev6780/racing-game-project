using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cloudborne
{
    /// <summary>
    /// Live 3D showcase and transparent-background icon renderer for Baba's Workshop: places Baba inside a cozy
    /// timber grove hut (wooden planks, dojo rug, sunlit bamboo window overlooking Baba's grove, clay tea set,
    /// ancient scroll, chest, and glowing lantern) beside an ornate shrine pedestal displaying a rotating 3D relic
    /// (Golden Fruit + dual halo, Fire Crystal wrapped in animated flame, forged Tiger Claw gauntlet, or Staffs),
    /// faceted orbiting level crystals, ambient sunbeam dust motes, and expressive Baba animations.
    /// Safe in headless tests (no graphics device): gracefully skips RenderTexture rendering while keeping menu logic intact.
    /// </summary>
    public class CbWorkshop3D
    {
        enum BabaReact { None, Select, Upgrade, Staff, Deny }

        static readonly Vector3 StageOrigin = new Vector3(-300f, -120f, 0f);
        static readonly Vector3 PedestalPos = new Vector3(0.48f, 0f, -0.05f);
        static readonly Vector3 BabaPos = new Vector3(-0.58f, 0f, 0.22f);
        const float BabaYaw = 148f;

        static readonly Color[] ElementCols =
        {
            CbArt.Hex("#ff5a52"),
            CbArt.Hex("#ff9c2e"),
            CbArt.Hex("#78d4ff"),
            CbArt.Hex("#ffc94a"),
        };
        static readonly string[] StaffModels = { "Staff", "StaffFire", "StaffWind", "StaffVine" };
        static readonly Color[] StaffCols =
        {
            CbArt.Hex("#d8b07a"),
            CbArt.Hex("#ff6a3a"),
            CbArt.Hex("#78d8ff"),
            CbArt.Hex("#6fe08a"),
        };

        readonly Transform stageRoot, roomRoot, babaRoot, pedestal, itemPivot, orbRing, runeRim, moteRoot, staffAuraParent;
        readonly Camera cam;
        readonly RenderTexture liveRT;
        readonly CbModel babaModel;
        readonly Rig babaRig;
        readonly Material runeMat, rimGlowMat, shaftMat, lanternGlowMat;
        readonly Transform[] itemSlots = new Transform[4];
        readonly Transform[] staffSlots = new Transform[4];
        readonly Transform[] staffHalos = new Transform[4];
        readonly Transform[] orbs = new Transform[3];
        readonly Material[] orbMats = new Material[3];
        readonly Texture2D[] thumbs = new Texture2D[4];
        int thumbStaffKind = -1;

        readonly List<(Transform t, Material m, Vector3 v, float age, float life)> sparks =
            new List<(Transform, Material, Vector3, float, float)>();
        readonly List<(Transform t, Material m, Vector3 home, float phase, float amp)> motes =
            new List<(Transform, Material, Vector3, float, float)>();

        int selected, level, staffKind;
        float clock, reactT, reactDur, spin;
        BabaReact reactKind;
        bool active;

        public Texture LiveTexture => liveRT;

        public CbWorkshop3D(Transform parent)
        {
            stageRoot = new GameObject("Workshop3D").transform;
            stageRoot.SetParent(parent, false);
            stageRoot.position = StageOrigin;

            roomRoot = new GameObject("room").transform;
            roomRoot.SetParent(stageRoot, false);
            lanternGlowMat = BuildWorkshopRoom(roomRoot);

            babaRoot = CbArt.Joint(stageRoot, BabaPos.x, BabaPos.y, BabaPos.z, "baba");
            babaRoot.localRotation = Quaternion.Euler(0f, BabaYaw, 0f);
            babaModel = CbModel.Load("Cloudborne/Baba/Baba", 1.78f, babaRoot);
            if (babaModel != null)
                babaModel.LoopSeconds(babaModel.HasClip("Idle_02") ? "Idle_02" : "Idle", 0f);
            else
            {
                babaRig = RigBuilder.Build(CbStory.BabaStyle, "Baba");
                babaRig.root.SetParent(babaRoot, false);
            }

            // ornate stone, oak, and hammered-gold shrine pedestal on the right of the diorama
            pedestal = CbArt.Joint(stageRoot, PedestalPos.x, PedestalPos.y, PedestalPos.z, "pedestal");
            var stoneBase = CbArt.Toon("#3a322d", CbArt.PropOutline);
            var woodDark = CbArt.Toon("#4a2d18", CbArt.PropOutline);
            var woodMid = CbArt.Toon("#6d4526", CbArt.PropOutline);
            var goldTrim = CbArt.Toon("#e8b84a", CbArt.PropOutline);
            goldTrim.SetFloat("_SpecStrength", 0.55f);
            CbArt.Part(CbMesh.Cylinder, stoneBase, pedestal, new Vector3(0, 0.045f, 0), new Vector3(0.41f, 0.09f, 0.41f));
            CbArt.Part(CbMesh.Cylinder, woodDark, pedestal, new Vector3(0, 0.12f, 0), new Vector3(0.35f, 0.07f, 0.35f));
            CbArt.Part(CbMesh.Cylinder, woodMid, pedestal, new Vector3(0, 0.34f, 0), new Vector3(0.20f, 0.38f, 0.20f));
            CbArt.Part(CbMesh.Cylinder, goldTrim, pedestal, new Vector3(0, 0.34f, 0), new Vector3(0.23f, 0.03f, 0.23f));
            CbArt.Part(CbMesh.Frustum(1.42f, 16), woodDark, pedestal, new Vector3(0, 0.53f, 0), new Vector3(0.24f, 0.11f, 0.24f));
            CbArt.Part(CbMesh.Cylinder, goldTrim, pedestal, new Vector3(0, 0.655f, 0), new Vector3(0.35f, 0.028f, 0.35f));

            rimGlowMat = CbArt.Emissive(ElementCols[0] * 1.15f);
            runeRim = CbArt.Part(CbMesh.Torus(0.038f, 28, 8), rimGlowMat, pedestal,
                new Vector3(0, 0.682f, 0), Vector3.one * 0.34f, shadows: false);
            runeRim.name = "runeRim";

            runeMat = CbArt.Glow(ElementCols[0] * 0.72f, CbArt.Soft);
            CbArt.Part(CbMesh.Quad, runeMat, pedestal, new Vector3(0, 0.678f, 0), new Vector3(0.92f, 0.92f, 1f),
                Quaternion.Euler(90f, 0f, 0f), shadows: false).name = "runeGlow";

            shaftMat = CbArt.Glow(new Color(ElementCols[0].r, ElementCols[0].g, ElementCols[0].b, 0.16f), CbArt.Shaft);
            CbArt.Part(CbMesh.Quad, shaftMat, pedestal, new Vector3(0, 1.15f, 0.06f), new Vector3(0.68f, 0.96f, 1f),
                shadows: false).name = "pedestalBeam";

            itemPivot = CbArt.Joint(pedestal, 0, 1.07f, 0, "itemPivot");
            staffAuraParent = itemPivot;
            orbRing = CbArt.Joint(pedestal, 0, 1.07f, 0, "orbRing");
            var pipMesh = CbMesh.Crystal(6);
            for (int i = 0; i < 3; i++)
            {
                orbMats[i] = CbArt.Toon("#3a3a3e", CbArt.PropOutline);
                orbs[i] = CbArt.Part(pipMesh, orbMats[i], orbRing, Vector3.zero, new Vector3(0.055f, 0.09f, 0.055f));
                orbs[i].name = "orb" + i;
            }

            BuildElements();

            // gentle sunlit workshop dust motes drifting across Baba's room
            moteRoot = new GameObject("motes").transform;
            moteRoot.SetParent(stageRoot, false);
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector3(-1.05f + i * 0.23f, 0.42f + (i % 4) * 0.38f, -0.15f + (i % 3) * 0.42f);
                var m = CbArt.Glow(new Color(1f, 0.88f, 0.56f, 0.35f), CbArt.Soft);
                var mt = CbArt.Part(CbMesh.Quad, m, moteRoot, p, Vector3.one * (0.045f + (i % 3) * 0.015f), shadows: false);
                mt.name = "wMote" + i;
                motes.Add((mt, m, p, i * 0.85f, 0.18f + (i % 3) * 0.07f));
            }

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                liveRT = new RenderTexture(608, 500, 16, RenderTextureFormat.ARGB32) { name = "workshopLive", antiAliasing = 2 };
                var cgo = new GameObject("workshopCam");
                cgo.transform.SetParent(stageRoot, false);
                cam = cgo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.14f, 0.10f, 1f);
                cam.fieldOfView = 27f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 30f;
                cam.allowHDR = true;
                cam.depth = -5;
                cam.targetTexture = liveRT;
                cam.transform.localPosition = new Vector3(0.02f, 1.12f, -4.65f);
                cam.transform.LookAt(StageOrigin + new Vector3(0.02f, 0.90f, 0f));
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = false;
                cam.enabled = false;
            }

            SetActive(false);
        }

        Material BuildWorkshopRoom(Transform room)
        {
            var woodFloor = CbArt.Toon("#3b2313");
            var woodPlank = CbArt.Toon("#51321c", CbArt.PropOutline * 0.4f);
            var woodTrim = CbArt.Toon("#6b4326", CbArt.PropOutline * 0.5f);
            var woodBeam = CbArt.Toon("#2c190d", CbArt.PropOutline * 0.6f);
            var rugRed = CbArt.Toon("#88281c", CbArt.PropOutline * 0.4f);
            var rugGold = CbArt.Toon("#e0ab3c", CbArt.PropOutline * 0.5f);

            // timber plank floor & bevel strips
            CbArt.Part(CbMesh.Box(), woodFloor, room, new Vector3(0f, -0.04f, 0.6f), new Vector3(5.2f, 0.08f, 4.0f));
            for (int i = -2; i <= 4; i++)
                CbArt.Part(CbMesh.Box(), woodPlank, room, new Vector3(0f, 0.002f, i * 0.46f), new Vector3(5.0f, 0.006f, 0.42f));

            // circular red-and-gold woven mat linking Baba and the shrine pedestal
            CbArt.Part(CbMesh.Cylinder, rugGold, room, new Vector3(-0.04f, 0.006f, 0.08f), new Vector3(1.16f, 0.008f, 1.16f));
            CbArt.Part(CbMesh.Cylinder, rugRed, room, new Vector3(-0.04f, 0.009f, 0.08f), new Vector3(1.06f, 0.010f, 1.06f));

            // lower cabin timber wainscot, oak window sill, left/right pillars, and overhead rafters (open window in the middle)
            CbArt.Part(CbMesh.Box(), woodBeam, room, new Vector3(0f, 0.42f, 1.62f), new Vector3(4.8f, 0.84f, 0.14f));
            CbArt.Part(CbMesh.Box(), woodTrim, room, new Vector3(0f, 0.86f, 1.56f), new Vector3(3.2f, 0.08f, 0.24f));
            CbArt.Part(CbMesh.Box(), woodBeam, room, new Vector3(-1.38f, 1.15f, 1.54f), new Vector3(0.24f, 2.4f, 0.22f));
            CbArt.Part(CbMesh.Box(), woodBeam, room, new Vector3(1.38f, 1.15f, 1.54f), new Vector3(0.24f, 2.4f, 0.22f));
            CbArt.Part(CbMesh.Box(), woodBeam, room, new Vector3(0f, 2.08f, 1.52f), new Vector3(3.4f, 0.18f, 0.24f));

            // sunlit painted vista of Baba's grove framed through the back window
            var groveTex = Resources.Load<Texture2D>("Cloudborne/Grove/Backdrop/GroveBackdrop");
            var cutout = Resources.Load<Shader>("Shaders/PaintedCutout");
            Material vistaMat;
            if (groveTex != null && cutout != null && cutout.isSupported)
            {
                vistaMat = new Material(cutout) { mainTexture = groveTex };
                vistaMat.SetColor("_Color", new Color(1f, 0.96f, 0.88f, 1f));
                vistaMat.SetFloat("_Cutoff", 0f);
                vistaMat.SetColor("_HazeColor", new Color(0.92f, 0.91f, 0.72f, 1f));
                vistaMat.SetFloat("_Haze", 0.22f);
                vistaMat.SetFloat("_Deep", 1f);
                vistaMat.SetFloat("_Ao", 0f);
            }
            else
                vistaMat = CbArt.Toon("#395738");
            CbArt.Part(CbMesh.Quad, vistaMat, room, new Vector3(0f, 1.38f, 2.25f), new Vector3(3.4f, 1.92f, 1f), shadows: false).name = "groveVista";

            // foliage peeking at the window edge outside
            var fernL = CbArt.Joint(room, -0.78f, 0.56f, 1.82f, "windowFernL");
            if (CbArt.Prop("Cloudborne/Jungle/Fern/Fern", fernL, CbArt.PropOutline * 0.6f) != null)
                fernL.localScale = Vector3.one * 0.72f;
            else Object.Destroy(fernL.gameObject);

            // soft diagonal morning sunbeams streaming in from the grove window
            var sunMat = CbArt.Glow(new Color(1f, 0.94f, 0.72f, 0.11f), CbArt.Shaft);
            CbArt.Part(CbMesh.Quad, sunMat, room, new Vector3(-0.08f, 1.48f, 0.85f), new Vector3(1.1f, 2.6f, 1f),
                Quaternion.Euler(0f, 0f, -24f), shadows: false).name = "sunRayL";
            CbArt.Part(CbMesh.Quad, sunMat, room, new Vector3(0.55f, 1.52f, 0.65f), new Vector3(0.85f, 2.4f, 1f),
                Quaternion.Euler(0f, 0f, -20f), shadows: false).name = "sunRayR";

            // Baba's tea table on the back left ("Coins for an old monkey's tea?") with clay teapot, cup, and ancient scroll
            var table = CbArt.Joint(room, -1.10f, 0f, 0.76f, "teaTable");
            CbArt.Part(CbMesh.Cylinder, woodBeam, table, new Vector3(0, 0.23f, 0), new Vector3(0.11f, 0.46f, 0.11f));
            CbArt.Part(CbMesh.Cylinder, woodTrim, table, new Vector3(0, 0.48f, 0), new Vector3(0.28f, 0.05f, 0.28f));
            BuildTeaSet(table, new Vector3(-0.07f, 0.505f, -0.02f));
            var scroll = CbArt.Joint(table, 0.09f, 0.54f, 0.04f, "teaScroll");
            if (CbArt.Prop("Cloudborne/Items/Scroll/Scroll", scroll, CbArt.PropOutline * 0.7f) != null)
            {
                scroll.localScale = Vector3.one * 0.45f;
                scroll.localRotation = Quaternion.Euler(0f, 28f, 0f);
            }
            else Object.Destroy(scroll.gameObject);

            // treasure chest tucked in the back right corner
            var chest = CbArt.Joint(room, 0.94f, 0f, 1.15f, "workshopChest");
            if (CbArt.Prop("Cloudborne/Jungle/Ways/Chest/Chest", chest, CbArt.PropOutline * 0.7f) != null)
            {
                chest.localScale = Vector3.one * 0.62f;
                chest.localRotation = Quaternion.Euler(0f, 145f, 0f);
            }
            else Object.Destroy(chest.gameObject);

            // moss stone lantern on the right framing the shrine pedestal with warm amber glow
            var lantern = CbArt.Joint(room, 1.12f, 0f, 0.52f, "workshopLantern");
            bool hasLantern = CbArt.Prop("Cloudborne/Jungle/Ways/Lantern/Lantern", lantern, CbArt.PropOutline * 0.7f) != null;
            if (hasLantern)
            {
                lantern.localScale = Vector3.one * 0.62f;
                lantern.localRotation = Quaternion.Euler(0f, -28f, 0f);
            }
            else
            {
                CbArt.Part(CbMesh.Cylinder, CbArt.Toon("#585854", CbArt.PropOutline * 0.6f), lantern, new Vector3(0, 0.55f, 0), new Vector3(0.16f, 1.1f, 0.16f));
                CbArt.Part(CbMesh.Box(), CbArt.Emissive(new Color(1f, 0.74f, 0.26f) * 1.1f), lantern, new Vector3(0, 0.98f, 0), new Vector3(0.18f, 0.18f, 0.18f));
            }
            var lgMat = CbArt.Glow(new Color(1f, 0.68f, 0.22f, 0.26f), CbArt.Soft);
            CbArt.Part(CbMesh.Quad, lgMat, lantern, new Vector3(0f, hasLantern ? 1.62f : 0.98f, -0.05f),
                new Vector3(0.95f, 0.95f, 1f), shadows: false).name = "lanternGlow";
            return lgMat;
        }

        static void BuildTeaSet(Transform parent, Vector3 at)
        {
            var teaRoot = CbArt.Joint(parent, at.x, at.y, at.z, "teaSet");
            var clay = CbArt.Toon("#b85432", CbArt.PropOutline * 0.6f);
            var cupCol = CbArt.Toon("#f3ecdd", CbArt.PropOutline * 0.5f);
            var gold = CbArt.Toon("#e6b348");
            // teapot belly, lid, gold finial, spout, curved handle
            CbArt.Sphere(clay, teaRoot, new Vector3(0f, 0.06f, 0f), 0.068f, new Vector3(1.1f, 0.88f, 1.1f));
            CbArt.Part(CbMesh.Cylinder, clay, teaRoot, new Vector3(0f, 0.118f, 0f), new Vector3(0.048f, 0.016f, 0.048f));
            CbArt.Sphere(gold, teaRoot, new Vector3(0f, 0.138f, 0f), 0.016f);
            CbArt.Part(CbMesh.Frustum(0.55f, 8), clay, teaRoot, new Vector3(0.065f, 0.068f, 0f),
                new Vector3(0.018f, 0.065f, 0.018f), Quaternion.Euler(0f, 0f, -52f));
            CbArt.Part(CbMesh.Torus(0.18f, 12, 6), clay, teaRoot, new Vector3(-0.062f, 0.065f, 0f),
                Vector3.one * 0.036f, Quaternion.identity);
            // small steaming ceramic teacup beside it
            CbArt.Part(CbMesh.Frustum(1.22f, 10), cupCol, teaRoot, new Vector3(0.13f, 0.024f, -0.04f), new Vector3(0.028f, 0.048f, 0.028f));
        }

        void BuildElements()
        {
            // 0: TIGER HEART — Golden Fruit encircled by dual crimson-and-gold orbital halos
            var s0 = itemSlots[0] = CbArt.Joint(itemPivot, 0, 0, 0, "el_heart");
            var fruit = CbArt.Prop("Cloudborne/Items/GoldenFruit/GoldenFruit", s0, CbArt.PropOutline);
            if (fruit != null) FitCentered(fruit, 0.54f);
            else CbArt.Sphere(CbArt.Toon("#ff5a52", CbArt.PropOutline), s0, Vector3.zero, 0.24f);
            CbArt.Part(CbMesh.Torus(0.055f, 24, 8), CbArt.Emissive(CbArt.Hex("#ff584a") * 1.15f), s0, Vector3.zero,
                Vector3.one * 0.34f, Quaternion.Euler(24f, 0f, -16f), shadows: false);
            CbArt.Part(CbMesh.Torus(0.04f, 24, 8), CbArt.Emissive(CbArt.Hex("#ffd050") * 1.1f), s0, Vector3.zero,
                Vector3.one * 0.29f, Quaternion.Euler(-22f, 20f, 18f), shadows: false);

            // 1: EMBER BREATH — Fire Crystal wrapped inside a live animated anime flame collar
            var s1 = itemSlots[1] = CbArt.Joint(itemPivot, 0, 0, 0, "el_ember");
            var crystal = CbArt.Prop("Cloudborne/Ember/FireCrystal/FireCrystal", s1, CbArt.PropOutline)
                          ?? CbArt.Prop("Cloudborne/Items/KiCrystal/KiCrystal", s1, CbArt.PropOutline);
            if (crystal != null) FitCentered(crystal, 0.60f);
            else CbArt.Part(CbMesh.Crystal(6), CbArt.Emissive(CbArt.Hex("#ff9c2e") * 1.2f), s1, Vector3.zero, new Vector3(0.22f, 0.36f, 0.22f));
            var fireShader = Resources.Load<Shader>("Shaders/Campfire");
            if (fireShader != null && fireShader.isSupported)
            {
                var emberFlame = new Material(fireShader);
                emberFlame.SetFloat("_Speed", 1.35f);
                emberFlame.SetFloat("_Lobes", 3f);
                emberFlame.SetFloat("_Sway", 0.20f);
                emberFlame.SetFloat("_IsCore", 0.32f);
                emberFlame.SetFloat("_Cutoff", 0.28f);
                CbArt.Part(CbMesh.FlamePlume(), emberFlame, s1, new Vector3(0f, -0.34f, 0f),
                    new Vector3(0.22f, 0.62f, 0.22f), shadows: false).name = "emberFlame";
            }

            // 2: IRON CLAWS — Sculpted Raja Tiger Claw Gauntlet with crimson wrap & gleaming steel blades
            var s2 = itemSlots[2] = CbArt.Joint(itemPivot, 0, 0, 0, "el_claws");
            BuildIronClaws(s2);

            // 3: STAFFS — All 4 3D staffs with matching elemental aura rings
            var s3 = itemSlots[3] = CbArt.Joint(itemPivot, 0, 0, 0, "el_staffs");
            for (int k = 0; k < StaffModels.Length; k++)
            {
                var holder = staffSlots[k] = CbArt.Joint(s3, 0, 0, 0, "staffSlot" + k);
                var prop = CbArt.Prop("Cloudborne/Props/Staff/" + StaffModels[k], holder, CbArt.PropOutline);
                if (prop != null)
                {
                    prop.localRotation = Quaternion.Euler(0, 0, -28f);
                    FitCentered(prop, 0.90f);
                }
                else
                {
                    var fallback = CbArt.Part(CbMesh.Cylinder, CbArt.Toon(StaffCols[k], CbArt.PropOutline), holder,
                        Vector3.zero, new Vector3(0.06f, 0.44f, 0.06f), Quaternion.Euler(0, 0, -28f));
                    fallback.name = "staffFallback";
                }
                if (k > 0)
                {
                    var halo = CbArt.Part(CbMesh.Torus(0.055f, 20, 6), CbArt.Emissive(StaffCols[k] * 1.15f), holder,
                        new Vector3(0.14f, 0.26f, 0f), Vector3.one * 0.18f, Quaternion.Euler(28f, 0f, -28f), shadows: false);
                    halo.name = "staffHalo";
                    staffHalos[k] = halo;
                }
                holder.gameObject.SetActive(k == 0);
            }
        }

        static void BuildIronClaws(Transform parent)
        {
            var cuff = CbArt.Joint(parent, 0, -0.04f, 0, "clawsModel");
            cuff.localRotation = Quaternion.Euler(18f, -20f, -22f);
            var crimson = CbArt.Toon("#c82e24", CbArt.PropOutline);
            var darkMetal = CbArt.Toon("#3e4652", CbArt.PropOutline);
            var gold = CbArt.Toon("#ffc94a", CbArt.PropOutline);
            var steel = CbArt.Toon("#e2ecf8", CbArt.PropOutline);
            steel.SetFloat("_SpecStrength", 0.85f);
            steel.SetFloat("_SpecSize", 0.35f);

            // crimson sash wrap + gunmetal bracer + hammered gold tiger knuckle guard
            CbArt.Part(CbMesh.Cylinder, crimson, cuff, new Vector3(0, -0.13f, 0), new Vector3(0.21f, 0.14f, 0.17f));
            CbArt.Part(CbMesh.Torus(0.16f, 16, 6), gold, cuff, new Vector3(0, -0.06f, 0), new Vector3(0.125f, 0.08f, 0.105f));
            CbArt.Part(CbMesh.Box(), darkMetal, cuff, new Vector3(0, -0.02f, 0), new Vector3(0.24f, 0.10f, 0.13f));
            CbArt.Part(CbMesh.Box(), gold, cuff, new Vector3(0, 0.055f, 0.01f), new Vector3(0.27f, 0.065f, 0.135f));
            for (int i = -1; i <= 1; i++)
            {
                var blade = CbArt.Joint(cuff, i * 0.092f, 0.085f, 0.025f, "claw" + (i + 1));
                blade.localRotation = Quaternion.Euler(16f, 0f, -i * 11f);
                // faceted tapered tang + keen claw tip
                CbArt.Part(CbMesh.Frustum(0.62f, 6), steel, blade, new Vector3(0f, 0f, 0f), new Vector3(0.036f, 0.14f, 0.024f));
                CbArt.Cone(steel, blade, new Vector3(0, 0.25f, 0), 0.026f, 0.24f, Quaternion.identity, 6);
            }
        }

        static void FitCentered(Transform t, float targetSize)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float maxDim = Mathf.Max(0.01f, Mathf.Max(b.size.y, Mathf.Max(b.size.x, b.size.z)));
            float k = targetSize / maxDim;
            t.localScale *= k;
            var nb = rs[0].bounds;
            foreach (var r in rs) nb.Encapsulate(r.bounds);
            t.position += t.parent.position - nb.center;
        }

        public void SetActive(bool on)
        {
            active = on;
            stageRoot.gameObject.SetActive(on);
            if (cam != null) cam.enabled = on;
        }

        public void Select(int index, int lvl, int currentStaff, int staffsOwned)
        {
            selected = Mathf.Clamp(index, 0, 3);
            level = lvl;
            staffKind = Mathf.Clamp(currentStaff, 0, StaffModels.Length - 1);

            for (int i = 0; i < 4; i++) itemSlots[i].gameObject.SetActive(i == selected);
            for (int k = 0; k < 4; k++) staffSlots[k].gameObject.SetActive(k == staffKind);

            var col = selected == 3 ? StaffCols[staffKind] : ElementCols[selected];
            runeMat.SetColor("_Color", col * 0.72f);
            CbArt.SetGlow(rimGlowMat, col * 1.15f);
            shaftMat.SetColor("_Color", new Color(col.r, col.g, col.b, 0.16f));

            for (int i = 0; i < 3; i++)
            {
                bool lit = selected == 3 ? (staffsOwned & (1 << (i + 1))) != 0 : i < level;
                var oc = lit ? (selected == 3 ? StaffCols[i + 1] : col) : new Color(0.24f, 0.26f, 0.25f);
                orbMats[i].color = oc;
                CbArt.SetGlow(orbMats[i], lit ? oc * 0.95f : Color.black);
            }
        }

        public void TriggerSelect(int index, int lvl, int currentStaff, int staffsOwned)
        {
            Select(index, lvl, currentStaff, staffsOwned);
            if (reactKind == BabaReact.None || reactKind == BabaReact.Select || reactKind == BabaReact.Deny)
                StartReact(BabaReact.Select, 0.72f);
        }

        public void TriggerUpgrade(int index, int lvl, int currentStaff, int staffsOwned)
        {
            Select(index, lvl, currentStaff, staffsOwned);
            StartReact(index == 3 ? BabaReact.Staff : BabaReact.Upgrade, index == 3 ? 1.05f : 0.85f);
            if (!active || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var col = index == 3 ? StaffCols[staffKind] : ElementCols[index];
            var sparkMesh = CbMesh.Crystal(4);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                var m = CbArt.Glow(col * 1.35f, CbArt.Soft);
                var sp = CbArt.Part(sparkMesh, m, pedestal, new Vector3(0, 1.07f, -0.05f), Vector3.one * 0.065f, shadows: false);
                sp.name = "wSpark";
                sparks.Add((sp, m, new Vector3(Mathf.Cos(a) * 0.92f, 0.55f + (i % 3) * 0.36f, Mathf.Sin(a) * 0.55f), 0f, 0.72f));
            }
        }

        public void TriggerDeny()
        {
            StartReact(BabaReact.Deny, 0.85f);
        }

        void StartReact(BabaReact kind, float dur)
        {
            reactKind = kind;
            reactDur = dur;
            reactT = dur;
        }

        /// <summary>Renders a crisp cached 128x128 transparent-background 3D snapshot of element `index` for the Workshop row icon.</summary>
        public Texture2D Thumb(int index, int currentStaff)
        {
            if (cam == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;
            index = Mathf.Clamp(index, 0, 3);
            if (index == 3 && thumbStaffKind != currentStaff)
            {
                if (thumbs[3] != null) Object.Destroy(thumbs[3]);
                thumbs[3] = null;
                thumbStaffKind = currentStaff;
            }
            if (thumbs[index] != null) return thumbs[index];

            // save live state
            bool wasStage = stageRoot.gameObject.activeSelf;
            bool wasRoom = roomRoot.gameObject.activeSelf;
            bool wasBaba = babaRoot.gameObject.activeSelf;
            bool wasOrbs = orbRing.gameObject.activeSelf;
            bool wasMotes = moteRoot.gameObject.activeSelf;
            int prevSel = selected, prevStaff = staffKind;
            var prevSpin = itemPivot.localRotation;
            var prevPos = itemPivot.localPosition;

            stageRoot.gameObject.SetActive(true);
            roomRoot.gameObject.SetActive(false);
            babaRoot.gameObject.SetActive(false);
            moteRoot.gameObject.SetActive(false);
            foreach (Transform child in pedestal)
                if (child != itemPivot) child.gameObject.SetActive(false);
            orbRing.gameObject.SetActive(false);

            for (int i = 0; i < 4; i++) itemSlots[i].gameObject.SetActive(i == index);
            for (int k = 0; k < 4; k++) staffSlots[k].gameObject.SetActive(k == Mathf.Clamp(currentStaff, 0, 3));
            itemPivot.localPosition = new Vector3(0, 1.07f, 0);
            itemPivot.localRotation = Quaternion.Euler(8f, 22f, 0f);

            const int Sz = 128;
            var rt = RenderTexture.GetTemporary(Sz, Sz, 16, RenderTextureFormat.ARGB32);
            var prevCamPos = cam.transform.localPosition;
            var prevCamRot = cam.transform.localRotation;
            var prevBg = cam.backgroundColor;
            var prevTarget = cam.targetTexture;
            bool prevCamOn = cam.enabled;

            cam.enabled = false;
            cam.targetTexture = rt;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.transform.localPosition = new Vector3(PedestalPos.x, 1.07f, -1.92f);
            cam.transform.LookAt(StageOrigin + new Vector3(PedestalPos.x, 1.07f, PedestalPos.z));
            cam.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Sz, Sz, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.ReadPixels(new Rect(0, 0, Sz, Sz), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            // restore live state
            cam.transform.localPosition = prevCamPos;
            cam.transform.localRotation = prevCamRot;
            cam.backgroundColor = prevBg;
            cam.targetTexture = prevTarget;
            cam.enabled = prevCamOn;

            roomRoot.gameObject.SetActive(wasRoom);
            babaRoot.gameObject.SetActive(wasBaba);
            moteRoot.gameObject.SetActive(wasMotes);
            foreach (Transform child in pedestal) child.gameObject.SetActive(true);
            orbRing.gameObject.SetActive(wasOrbs);
            itemPivot.localPosition = prevPos;
            itemPivot.localRotation = prevSpin;
            Select(prevSel, level, prevStaff, CbSave.StaffsOwned);
            stageRoot.gameObject.SetActive(wasStage);

            return thumbs[index] = tex;
        }

        public void Update(float dt)
        {
            if (!active) return;
            clock += dt;
            spin += dt * 42f;
            float bob = Mathf.Sin(clock * 2.4f) * 0.04f;
            itemPivot.localPosition = new Vector3(0, 1.07f + bob, 0);
            itemPivot.localRotation = Quaternion.Euler(0, spin, 0);

            if (runeRim != null) runeRim.localRotation = Quaternion.Euler(0f, -spin * 0.65f, 0f);
            if (lanternGlowMat != null)
            {
                float lf = 0.88f + 0.12f * Mathf.Sin(clock * 6.5f);
                lanternGlowMat.SetColor("_Color", new Color(1f, 0.68f, 0.22f, 0.25f * lf));
            }
            for (int k = 1; k < staffHalos.Length; k++)
                if (staffHalos[k] != null) staffHalos[k].localRotation = Quaternion.Euler(28f, spin * 1.4f, -28f);

            for (int i = 0; i < 3; i++)
            {
                float a = clock * 1.35f + i * Mathf.PI * 2f / 3f;
                orbs[i].localPosition = new Vector3(Mathf.Cos(a) * 0.44f, bob * 0.5f + Mathf.Sin(clock * 3f + i) * 0.045f, Mathf.Sin(a) * 0.24f);
                orbs[i].localRotation = Quaternion.Euler(0f, spin * 1.8f + i * 60f, 18f);
            }

            // gentle sunlit dust motes drifting inside Baba's cabin
            for (int i = 0; i < motes.Count; i++)
            {
                var (mt, mm, home, ph, amp) = motes[i];
                mt.localPosition = home + new Vector3(Mathf.Sin(clock * 0.7f + ph) * 0.14f, Mathf.Sin(clock * 1.1f + ph) * 0.09f, Mathf.Cos(clock * 0.6f + ph) * 0.08f);
                mm.SetColor("_Color", new Color(1f, 0.88f, 0.56f, amp * (0.55f + 0.45f * Mathf.Sin(clock * 2.2f + ph))));
            }

            string reactClip = null;
            float reactU = 0f;
            if (reactT > 0)
            {
                reactT = Mathf.Max(0, reactT - dt);
                reactU = reactDur > 0 ? Mathf.Clamp01(1f - reactT / reactDur) : 1f;
                bool celebrate = reactKind == BabaReact.Upgrade || reactKind == BabaReact.Staff;
                float hop = celebrate ? Mathf.Sin(reactU * Mathf.PI) * 0.08f : 0f;
                babaRoot.localPosition = new Vector3(BabaPos.x, BabaPos.y + hop, BabaPos.z);
                babaRoot.localRotation = Quaternion.Euler(0f, BabaYaw + (reactKind == BabaReact.Select ? Mathf.Sin(reactU * Mathf.PI) * 6f : 0f), 0f);
                itemPivot.localScale = celebrate ? Vector3.one * (1f + Mathf.Sin(reactU * Mathf.PI) * 0.18f) : Vector3.one;

                if (reactKind == BabaReact.Select) reactClip = "Call_Gesture";
                else if (reactKind == BabaReact.Upgrade) reactClip = "Kung_Fu_Punch";
                else if (reactKind == BabaReact.Staff) reactClip = "Relax_arms_then_strike_battle_pose";
                else if (reactKind == BabaReact.Deny) reactClip = "Finger_Wag_No";
                if (reactT <= 0) reactKind = BabaReact.None;
            }
            else
            {
                babaRoot.localPosition = BabaPos;
                babaRoot.localRotation = Quaternion.Euler(0f, BabaYaw, 0f);
                itemPivot.localScale = Vector3.one;
            }

            if (babaModel != null)
            {
                if (reactClip != null && babaModel.HasClip(reactClip))
                    babaModel.Show(reactClip, reactU, 0.12f);
                else if (reactKind == BabaReact.Deny && babaModel.HasClip("Confused_Scratch"))
                    babaModel.Show("Confused_Scratch", reactU, 0.12f);
                else
                    babaModel.LoopSeconds(babaModel.HasClip("Idle_02") ? "Idle_02" : "Idle", clock, 0.15f);
            }
            else if (babaRig != null)
            {
                var p = Pose.Base;
                p.bob = Mathf.Sin(clock * 3f) * 0.025f - 0.03f;
                babaRig.Apply(p, Mathf.Max(dt, 0.001f), 10f);
            }

            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var sp = sparks[i];
                sp.age += dt;
                if (sp.age >= sp.life) { Object.Destroy(sp.t.gameObject); sparks.RemoveAt(i); continue; }
                sp.t.localPosition += sp.v * dt;
                sp.v.y -= 0.7f * dt;
                sp.t.Rotate(0f, dt * 260f, dt * 120f);
                float k = 1f - sp.age / sp.life;
                sp.t.localScale = Vector3.one * (0.065f * k);
                sparks[i] = sp;
            }
        }
    }
}
