using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Raja's camp (the user's ideas list: a home between the stages, as in Kirby's town): a clearing in the jungle left
    /// of stage 1-1's start, behind the invisible wall, seen only from the main menu. Raja stands by his campfire, his hut
    /// behind him (Canva MAHcKOfhc3g approved, Meshy CampHut, Campfire, TrophyShelf in Resources/Cloudborne/Camp), the
    /// staffs he owns leaning on it, and on the trophy shelf what he has found in the stages: the R-A-J-A letters, the
    /// totem masks worn, the time trials' medals. Rebuilt with the stage (CbGame.BuildStage); refreshed each time the menu
    /// opens.
    /// </summary>
    public class CbCamp
    {
        /// <summary>Where Raja stands in the menu (the camera keeps him on the screen's right, the menu on the left).</summary>
        public const float HeroX = -30f;
        // the campfire in front of him on his left, the hut behind him, the shelf behind on his right
        public const float FireX = HeroX - 1.65f, FireZ = 0.55f, HutX = HeroX - 0.6f, HutZ = 3.6f;
        const float ShelfX = HeroX + 2.3f, ShelfZ = 1.6f, ShelfYaw = 200f;
        // the shelf's three floors as modelled (measured by rays down through it, for its 1.7 m), its inner width
        static readonly float[] ShelfFloors = { 0.984f, 0.594f, 0.21f };
        const float ShelfInner = 1.36f, ShelfRoom = 0.27f;
        const string Dir = "Cloudborne/Camp/";
        static readonly string[] StaffModels = { "Staff", "StaffFire", "StaffWind", "StaffVine" };

        readonly Transform root;
        Transform shelf, trophies, staffs;
        readonly List<(Transform t, float h, float baseX, float tilt)> tongues = new List<(Transform, float, float, float)>();
        float time;

        public Transform Root => root;
        /// <summary>How many trophies stand on the shelf now (for the tests).</summary>
        public int Trophies => trophies != null ? trophies.childCount : 0;
        /// <summary>How many staffs lean on the hut now.</summary>
        public int Staffs => staffs != null ? staffs.childCount : 0;

        /// <summary>True inside the hut footprint, firepit ring and foreground clearing so jungle foliage never pokes through them.</summary>
        public static bool InCampClearing(float x, float z) =>
            (Mathf.Abs(x - HutX) < 2.25f && z > 1.75f && z < 5.8f) ||
            (Mathf.Abs(x - FireX) < 1.05f && z > -0.25f && z < 1.85f) ||
            (Mathf.Abs(x - HeroX) < 3.1f && z > -0.2f && z < 1.75f);

        public CbCamp(Transform parent)
        {
            root = new GameObject("Camp").transform;
            root.SetParent(parent, false);
            var hut = Place("CampHut", HutX, HutZ, 180f + 12f);
            if (hut != null) HutInterior(hut);
            Place("Campfire", FireX, FireZ, 180f);
            shelf = Place("TrophyShelf", ShelfX, ShelfZ, ShelfYaw);
            Fire();
            Refresh();
        }

        Transform Place(string name, float x, float z, float yaw)
        {
            var t = CbArt.Joint(root, x, 0, z, name.ToLowerInvariant());
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            if (CbArt.Prop(Dir + name, t, CbArt.PropOutline) == null) { Object.Destroy(t.gameObject); return null; }
            return t;
        }

        /// <summary>A dark cozy cabin interior backing inside the hut so the open doorway never shows bushes behind it.</summary>
        void HutInterior(Transform hut)
        {
            var rs = hut.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var dark = CbArt.Toon("#24150d");
            dark.SetFloat("_SpecStrength", 0f);
            dark.SetFloat("_RimStrength", 0f);
            // seated safely inside the cabin walls below the eaves
            var back = CbArt.Part(CbMesh.Quad, dark, root,
                new Vector3(b.center.x, b.min.y + b.size.y * 0.42f, b.center.z + b.size.z * 0.14f),
                new Vector3(b.size.x * 0.50f, b.size.y * 0.38f, 1f),
                Quaternion.Euler(0, 12f, 0), shadows: false);
            back.name = "hutInterior";
            var glow = CbArt.Glow(new Color(1f, 0.55f, 0.2f, 0.24f), CbArt.Soft);
            CbArt.Part(CbMesh.Quad, glow, root,
                new Vector3(b.center.x, b.min.y + b.size.y * 0.39f, b.center.z + b.size.z * 0.08f),
                new Vector3(b.size.x * 0.34f, b.size.y * 0.28f, 1f),
                Quaternion.Euler(0, 12f, 0), shadows: false).name = "hutWarmth";
        }

        /// <summary>The fire on the logs: crisp cel-shaded flame tongues without HDR whiteout, a gentle halo and ground glow.</summary>
        void Fire()
        {
            var t = CbArt.Joint(root, FireX, 0.14f, FireZ, "campFire");
            var tex = CbVfx.Painted("Fire");
            var cutout = Resources.Load<Shader>("Shaders/PaintedCutout");
            Material flameMat = null;
            if (tex != null && cutout != null && cutout.isSupported)
            {
                flameMat = new Material(cutout) { mainTexture = tex };
                flameMat.SetColor("_Color", new Color(1f, 0.96f, 0.9f, 1f));
                flameMat.SetFloat("_Cutoff", 0.44f);
                flameMat.SetFloat("_Haze", 0f);
                flameMat.SetFloat("_Deep", 1f);
                flameMat.SetFloat("_Ao", 0f);
            }
            var fallbackMat = flameMat ?? CbArt.Glow(new Color(1f, 0.55f, 0.18f, 0.55f), (Texture)tex ?? CbLevel.FlameTexture());

            var specs = new[]
            {
                (x: 0f, z: 0f, h: 0.64f, tilt: 0f, flip: 1f),
                (x: -0.15f, z: -0.04f, h: 0.44f, tilt: 10f, flip: -1f),
                (x: 0.16f, z: 0.03f, h: 0.40f, tilt: -9f, flip: 1f),
            };
            foreach (var s in specs)
            {
                var tongue = CbArt.Part(CbMesh.Quad, fallbackMat, t, new Vector3(s.x, s.h / 2f, s.z),
                    new Vector3(s.h * 0.85f * s.flip, s.h, 1f), Quaternion.Euler(0, 0, s.tilt), shadows: false);
                tongue.name = "flame";
                tongues.Add((tongue, s.h, s.x, s.tilt));
            }
            // single soft warm halo behind the flame and warm light on the ground around the stones
            var halo = CbArt.Glow(new Color(1f, 0.52f, 0.14f, 0.24f), CbArt.Soft);
            CbArt.Part(CbMesh.Quad, halo, t, new Vector3(0, 0.34f, 0.08f), new Vector3(1.25f, 1.1f, 1f), shadows: false).name = "fireHalo";
            var glow = CbArt.Glow(new Color(1f, 0.56f, 0.2f, 0.32f), CbArt.Soft);
            CbArt.Part(CbMesh.Quad, glow, t, new Vector3(0, -0.12f, 0), new Vector3(2.6f, 2.0f, 1), Quaternion.Euler(90, 0, 0), shadows: false).name = "fireGround";
        }

        /// <summary>What he has found and owns, put out again (the menu opened: CbGame.ToMenu).</summary>
        public void Refresh()
        {
            if (trophies != null) Object.Destroy(trophies.gameObject);
            if (staffs != null) Object.Destroy(staffs.gameObject);
            trophies = staffs = null;
            if (shelf != null)
            {
                trophies = CbArt.Joint(shelf, 0, 0, 0, "trophies");
                // (in the shelf's own space its front is +z, as a model's front: a letter as it is; a mask, made to
                // face out from his head along its -z, turned round)
                int letters = CbSave.LettersEver, masks = CbSave.MasksEver;
                for (int i = 0; i < 4; i++)
                    if ((letters & (1 << i)) != 0) Trophy($"Cloudborne/Items/Letters/{CbItems.LetterModels[i]}", 0, i, 4);
                for (int i = 0; i < CbHero.MaskModels.Length; i++)
                    if ((masks & (1 << i)) != 0) Trophy($"Cloudborne/Items/Masks/{CbHero.MaskModels[i]}", 1, i, 3, 180f);
                for (int i = 0; i < CbTrials.WaysTrials.Length; i++)
                {
                    var tr = CbTrials.WaysTrials[i];
                    float best = PlayerPrefs.GetFloat(CbTrials.Key(tr.name), 0f);
                    if (best <= 0 || best > tr.bronze) continue;
                    Medal(best <= tr.gold ? "#ffd34a" : best <= tr.silver ? "#d9e2ea" : "#d08a4e", i, CbTrials.WaysTrials.Length);
                }
            }
            // the staffs he owns lean on the hut's front, the one he carries out with him
            staffs = CbArt.Joint(root, HutX + 1.25f, 0, HutZ - 1.3f, "campStaffs");
            int owned = CbSave.StaffsOwned, n = 0;
            for (int k = 0; k < StaffModels.Length; k++)
            {
                if ((owned & (1 << k)) == 0 || k == CbSave.StaffKind) continue;
                var s = CbArt.Joint(staffs, n * 0.3f, 0, n * 0.05f, "staff" + k);
                // (the staff's origin is its grip, 0.35 of the way up: stood on its foot)
                if (CbArt.Prop("Cloudborne/Props/Staff/" + StaffModels[k], s) == null) { Object.Destroy(s.gameObject); continue; }
                // (its long axis is up, its grip at the origin: raised onto its foot, its top leaning back on the hut)
                s.localScale = Vector3.one * CbHero.StaffScale;
                s.localRotation = Quaternion.Euler(13f, 0, -6f + n * 5f);
                s.localPosition += new Vector3(0, 0.35f * 1.75f * CbHero.StaffScale, 0);
                n++;
            }
        }

        /// <summary>A trophy on a floor of the shelf (row 0 the top), the i-th of n across it, fitted into the room there.</summary>
        void Trophy(string path, int row, int i, int n, float yaw = 0f)
        {
            var t = CbArt.Joint(trophies, 0, 0, 0, "trophy");
            if (CbArt.Prop(path, t, CbArt.PropOutline) == null) { Object.Destroy(t.gameObject); return; }
            Fit(t, ShelfRoom);
            float x = (i + 0.5f) / n * ShelfInner - ShelfInner / 2;
            t.localPosition += new Vector3(x, ShelfFloors[row], 0.02f);
            t.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        /// <summary>A time trial's medal on the bottom floor: gold, silver or bronze, on a red ribbon.</summary>
        void Medal(string colour, int i, int n)
        {
            float x = (i + 0.5f) / n * ShelfInner - ShelfInner / 2;
            var t = CbArt.Joint(trophies, x, ShelfFloors[2], 0.02f, "medal");
            var metal = CbArt.Toon(colour, CbArt.PropOutline);
            CbArt.Part(CbMesh.Cylinder, metal, t, new Vector3(0, 0.12f, 0), new Vector3(0.2f, 0.012f, 0.2f), Quaternion.Euler(90, 0, 0));
            var ribbon = CbArt.Toon("#c8323c");
            CbArt.Part(CbMesh.Quad, ribbon, t, new Vector3(-0.04f, 0.24f, 0.01f), new Vector3(0.06f, 0.16f, 1), Quaternion.Euler(0, 0, 18), shadows: false);
            CbArt.Part(CbMesh.Quad, ribbon, t, new Vector3(0.04f, 0.24f, 0.01f), new Vector3(0.06f, 0.16f, 1), Quaternion.Euler(0, 0, -18), shadows: false);
            t.localRotation = Quaternion.Euler(0, 180f, 0);
        }

        /// <summary>A model scaled to stand `tall` high, its foot on its joint (models come in their own sizes).</summary>
        static void Fit(Transform t, float tall)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float k = tall / Mathf.Max(0.01f, Mathf.Max(b.size.y, b.size.x * 0.75f));
            t.localScale *= k;
            float foot = (b.min.y - t.position.y) * k;
            t.localPosition -= new Vector3(0, foot, 0);
        }

        /// <summary>Each frame in the menu: the flames flicker, now and then a spark rises.</summary>
        public void Update(float dt)
        {
            time += dt;
            for (int i = 0; i < tongues.Count; i++)
            {
                var (t, h0, bx, tilt) = tongues[i];
                float s = 1f + Mathf.Sin(time * (6.8f + i * 1.9f) + i * 1.7f) * 0.11f + Mathf.Sin(time * 11f + i) * 0.04f;
                float flip = i == 1 ? -1f : 1f;
                t.localScale = new Vector3(h0 * 0.85f * (1.28f - s * 0.28f) * flip, h0 * s, 1);
                t.localPosition = new Vector3(bx + Mathf.Sin(time * 4f + i) * 0.012f, h0 * s / 2f, t.localPosition.z);
                t.localRotation = Quaternion.Euler(0, 0, tilt + Mathf.Sin(time * 3.1f + i * 0.9f) * 4.5f);
            }
            if (tongues.Count > 0 && Random.value < dt * 2.5f)
                CbFx.Spark(new Vector3(FireX + Random.Range(-0.2f, 0.2f), 0.55f, FireZ - 0.1f), CbFx.Flame, 1, 1.2f, 0.10f, 0.9f, 1);
        }

        public void Destroy() => Object.Destroy(root.gameObject);
    }
}
