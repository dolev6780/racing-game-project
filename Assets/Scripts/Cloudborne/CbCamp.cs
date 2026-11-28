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
        const float FireX = HeroX - 1.7f, FireZ = -0.3f, HutX = HeroX - 0.6f, HutZ = 3.6f, ShelfX = HeroX + 2.3f, ShelfZ = 1.6f, ShelfYaw = 200f;
        // the shelf's three floors as modelled (measured by rays down through it, for its 1.7 m), its inner width
        static readonly float[] ShelfFloors = { 0.984f, 0.594f, 0.21f };
        const float ShelfInner = 1.36f, ShelfRoom = 0.27f;
        const string Dir = "Cloudborne/Camp/";
        static readonly string[] StaffModels = { "Staff", "StaffFire", "StaffWind", "StaffVine" };

        readonly Transform root;
        Transform shelf, trophies, staffs;
        readonly List<(Transform t, float h)> tongues = new List<(Transform, float)>();
        float time;

        public Transform Root => root;
        /// <summary>How many trophies stand on the shelf now (for the tests).</summary>
        public int Trophies => trophies != null ? trophies.childCount : 0;
        /// <summary>How many staffs lean on the hut now.</summary>
        public int Staffs => staffs != null ? staffs.childCount : 0;

        public CbCamp(Transform parent)
        {
            root = new GameObject("Camp").transform;
            root.SetParent(parent, false);
            var hut = Place("CampHut", HutX, HutZ, 180f + 12f);
            var fire = Place("Campfire", FireX, FireZ, 180f);
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

        /// <summary>The fire on the logs: tongues of flame flickering each on its own, a warm glow on the ground.</summary>
        void Fire()
        {
            var t = CbArt.Joint(root, FireX, 0.12f, FireZ, "campFire");
            var tex = (Texture)CbVfx.Painted("Fire") ?? CbLevel.FlameTexture();
            bool painted = CbVfx.Painted("Fire") != null;
            var outer = CbArt.Glow(painted ? new Color(1f, 0.85f, 0.75f, 1f) : new Color(1f, 0.45f, 0.12f, 1f), tex);
            var inner = CbArt.Glow(painted ? new Color(1f, 1f, 0.9f, 0.9f) : new Color(1f, 0.82f, 0.4f, 1f), tex);
            for (int k = 0; k < 7; k++)
            {
                bool core = k % 3 == 1;
                float h = core ? Random.Range(0.45f, 0.6f) : Random.Range(0.6f, 0.85f);
                var tongue = CbArt.Part(CbMesh.Quad, core ? inner : outer, t, new Vector3(Random.Range(-0.22f, 0.22f), h / 2, -0.15f + k * 0.05f - (core ? 0.06f : 0f)),
                    new Vector3(h * 0.9f, h, 1), shadows: false);
                tongue.name = "flame";
                tongues.Add((tongue, h));
            }
            // the warm light it throws on the ground round it
            var glow = CbArt.Glow(new Color(1f, 0.6f, 0.25f, 0.45f), CbArt.Soft);
            CbArt.Part(CbMesh.Quad, glow, t, new Vector3(0, -0.1f, 0), new Vector3(3.2f, 2.4f, 1), Quaternion.Euler(90, 0, 0), shadows: false);
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
                var (t, h0) = tongues[i];
                float s = 1f + Mathf.Sin(time * (8f + i % 5) + i * 1.7f) * 0.16f + Mathf.Sin(time * 13f + i) * 0.06f;
                t.localScale = new Vector3(h0 * 0.9f * (1.6f - s * 0.6f), h0 * s, 1);
                var p = t.localPosition;
                p.y = h0 * s / 2;
                t.localPosition = p;
                t.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 3.1f + i * 0.9f) * 6f);
            }
            if (tongues.Count > 0 && Random.value < dt * 2.5f)
                CbFx.Spark(new Vector3(FireX + Random.Range(-0.2f, 0.2f), 0.6f, FireZ - 0.2f), CbFx.Flame, 1, 1.2f, 0.12f, 0.9f, 1);
        }

        public void Destroy() => Object.Destroy(root.gameObject);
    }
}
