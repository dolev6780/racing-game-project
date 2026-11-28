using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cloudborne
{
    /// <summary>
    /// Live 3D showcase and per-item thumbnail renderer for Baba's Workshop: puts Baba in 3D beside an oak pedestal
    /// displaying a rotating 3D element for whichever upgrade or staff is selected (Golden Fruit for Tiger Heart, Fire
    /// Crystal for Ember Breath, sculpted Iron Tiger Claws for Iron Claws, and the 3D Staff models), plus orbiting level
    /// crystal pips and upgrade celebration sparks.
    /// Safe in headless tests (no graphics device): gracefully skips RenderTexture rendering while keeping menu logic intact.
    /// </summary>
    public class CbWorkshop3D
    {
        static readonly Vector3 StageOrigin = new Vector3(-300f, -120f, 0f);
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

        readonly Transform stageRoot, babaRoot, pedestal, itemPivot, orbRing;
        readonly Camera cam;
        readonly RenderTexture liveRT;
        readonly CbModel babaModel;
        readonly Rig babaRig;
        readonly Material runeMat;
        readonly Transform[] itemSlots = new Transform[4];
        readonly Transform[] staffSlots = new Transform[4];
        readonly Transform[] orbs = new Transform[3];
        readonly Material[] orbMats = new Material[3];
        readonly Texture2D[] thumbs = new Texture2D[4];
        int thumbStaffKind = -1;

        readonly List<(Transform t, Material m, Vector3 v, float age, float life)> sparks =
            new List<(Transform, Material, Vector3, float, float)>();

        int selected, level, staffKind;
        float clock, reactT, spin;
        bool active;

        public Texture LiveTexture => liveRT;

        public CbWorkshop3D(Transform parent)
        {
            stageRoot = new GameObject("Workshop3D").transform;
            stageRoot.SetParent(parent, false);
            stageRoot.position = StageOrigin;

            babaRoot = CbArt.Joint(stageRoot, -0.50f, 0f, 0.25f, "baba");
            babaRoot.localRotation = Quaternion.Euler(0, 148f, 0);
            babaModel = CbModel.Load("Cloudborne/Baba/Baba", 1.78f, babaRoot);
            if (babaModel != null)
                babaModel.LoopSeconds(babaModel.HasClip("Idle_02") ? "Idle_02" : "Idle", 0f);
            else
            {
                babaRig = RigBuilder.Build(CbStory.BabaStyle, "Baba");
                babaRig.root.SetParent(babaRoot, false);
            }

            // wooden counter/pedestal on the right of the diorama
            pedestal = CbArt.Joint(stageRoot, 0.42f, 0f, -0.1f, "pedestal");
            var woodDark = CbArt.Toon("#4a2d18", CbArt.PropOutline);
            var woodMid = CbArt.Toon("#6d4526", CbArt.PropOutline);
            var goldTrim = CbArt.Toon("#e8b84a", CbArt.PropOutline);
            CbArt.Part(CbMesh.Cylinder, woodDark, pedestal, new Vector3(0, 0.08f, 0), new Vector3(0.74f, 0.08f, 0.74f));
            CbArt.Part(CbMesh.Cylinder, woodMid, pedestal, new Vector3(0, 0.34f, 0), new Vector3(0.46f, 0.22f, 0.46f));
            CbArt.Part(CbMesh.Cylinder, woodDark, pedestal, new Vector3(0, 0.60f, 0), new Vector3(0.68f, 0.06f, 0.68f));
            CbArt.Part(CbMesh.Cylinder, goldTrim, pedestal, new Vector3(0, 0.65f, 0), new Vector3(0.62f, 0.022f, 0.62f));

            runeMat = CbArt.Glow(ElementCols[0] * 0.85f, CbArt.Soft);
            CbArt.Part(CbMesh.Quad, runeMat, pedestal, new Vector3(0, 0.678f, 0), new Vector3(0.86f, 0.86f, 1f),
                Quaternion.Euler(90f, 0f, 0f), shadows: false).name = "runeGlow";

            itemPivot = CbArt.Joint(pedestal, 0, 1.06f, 0, "itemPivot");
            orbRing = CbArt.Joint(pedestal, 0, 1.06f, 0, "orbRing");
            for (int i = 0; i < 3; i++)
            {
                orbMats[i] = CbArt.Toon("#3a3a3e", CbArt.PropOutline);
                orbs[i] = CbArt.Part(CbMesh.Box(), orbMats[i], orbRing, Vector3.zero, Vector3.one * 0.085f,
                    Quaternion.Euler(45f, 45f, 0f));
                orbs[i].name = "orb" + i;
            }

            BuildElements();

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                liveRT = new RenderTexture(512, 540, 16, RenderTextureFormat.ARGB32) { name = "workshopLive" };
                var cgo = new GameObject("workshopCam");
                cgo.transform.SetParent(stageRoot, false);
                cam = cgo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.065f, 0.055f, 0.048f, 1f);
                cam.fieldOfView = 26f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 30f;
                cam.allowHDR = true;
                cam.depth = -5;
                cam.targetTexture = liveRT;
                cam.transform.localPosition = new Vector3(0f, 1.05f, -4.65f);
                cam.transform.LookAt(StageOrigin + new Vector3(-0.04f, 0.92f, 0f));
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = false;
                cam.enabled = false;
            }

            SetActive(false);
        }

        void BuildElements()
        {
            // 0: TIGER HEART — Golden Fruit inside a glowing crimson-gold halo ring
            var s0 = itemSlots[0] = CbArt.Joint(itemPivot, 0, 0, 0, "el_heart");
            var fruit = CbArt.Prop("Cloudborne/Items/GoldenFruit/GoldenFruit", s0, CbArt.PropOutline);
            if (fruit != null) FitCentered(fruit, 0.54f);
            else CbArt.Sphere(CbArt.Toon("#ff5a52", CbArt.PropOutline), s0, Vector3.zero, 0.24f);
            CbArt.Part(CbMesh.Torus(0.06f), CbArt.Emissive(CbArt.Hex("#ff6a55") * 1.1f), s0, Vector3.zero,
                Vector3.one * 0.34f, Quaternion.Euler(22f, 0f, -15f), shadows: false);

            // 1: EMBER BREATH — Fire Crystal (or Ki Crystal fallback)
            var s1 = itemSlots[1] = CbArt.Joint(itemPivot, 0, 0, 0, "el_ember");
            var crystal = CbArt.Prop("Cloudborne/Ember/FireCrystal/FireCrystal", s1, CbArt.PropOutline)
                          ?? CbArt.Prop("Cloudborne/Items/KiCrystal/KiCrystal", s1, CbArt.PropOutline);
            if (crystal != null) FitCentered(crystal, 0.62f);
            else CbArt.Part(CbMesh.Box(), CbArt.Emissive(CbArt.Hex("#ff9c2e") * 1.2f), s1, Vector3.zero, Vector3.one * 0.34f, Quaternion.Euler(45f, 45f, 0f));

            // 2: IRON CLAWS — Sculpted 3D Tiger Claw Gauntlet with 3 gleaming steel blades
            var s2 = itemSlots[2] = CbArt.Joint(itemPivot, 0, 0, 0, "el_claws");
            BuildIronClaws(s2);

            // 3: STAFFS — All 4 3D staffs tilted heroically for full showcase
            var s3 = itemSlots[3] = CbArt.Joint(itemPivot, 0, 0, 0, "el_staffs");
            for (int k = 0; k < StaffModels.Length; k++)
            {
                var holder = staffSlots[k] = CbArt.Joint(s3, 0, 0, 0, "staffSlot" + k);
                var prop = CbArt.Prop("Cloudborne/Props/Staff/" + StaffModels[k], holder, CbArt.PropOutline);
                if (prop != null)
                {
                    prop.localRotation = Quaternion.Euler(0, 0, -28f);
                    FitCentered(prop, 0.88f);
                }
                else
                {
                    var fallback = CbArt.Part(CbMesh.Cylinder, CbArt.Toon(StaffCols[k], CbArt.PropOutline), holder,
                        Vector3.zero, new Vector3(0.06f, 0.44f, 0.06f), Quaternion.Euler(0, 0, -28f));
                    fallback.name = "staffFallback";
                }
                holder.gameObject.SetActive(k == 0);
            }
        }

        static void BuildIronClaws(Transform parent)
        {
            var cuff = CbArt.Joint(parent, 0, -0.04f, 0, "clawsModel");
            cuff.localRotation = Quaternion.Euler(18f, -20f, -22f);
            var orange = CbArt.Toon("#e06e1e", CbArt.PropOutline);
            var gold = CbArt.Toon("#ffc94a", CbArt.PropOutline);
            var steel = CbArt.Toon("#dce6f2", CbArt.PropOutline);
            steel.SetFloat("_SpecStrength", 0.75f);
            steel.SetFloat("_SpecSize", 0.35f);

            // gauntlet cuff & tiger-gold knuckle plate
            CbArt.Part(CbMesh.Cylinder, orange, cuff, new Vector3(0, -0.11f, 0), new Vector3(0.24f, 0.14f, 0.19f));
            CbArt.Part(CbMesh.Box(), gold, cuff, new Vector3(0, 0.03f, 0), new Vector3(0.26f, 0.08f, 0.12f));
            for (int i = -1; i <= 1; i++)
            {
                var blade = CbArt.Joint(cuff, i * 0.09f, 0.07f, 0.02f, "claw" + (i + 1));
                blade.localRotation = Quaternion.Euler(14f, 0f, -i * 11f);
                CbArt.Cone(steel, blade, new Vector3(0, 0.18f, 0), 0.042f, 0.36f, Quaternion.identity, 6);
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
            runeMat.SetColor("_Color", col * 0.85f);

            int orbCount = selected == 3 ? 3 : 3;
            for (int i = 0; i < orbCount; i++)
            {
                bool lit = selected == 3 ? (staffsOwned & (1 << (i + 1))) != 0 : i < level;
                var oc = lit ? (selected == 3 ? StaffCols[i + 1] : col) : new Color(0.24f, 0.26f, 0.25f);
                orbMats[i].color = oc;
                CbArt.SetGlow(orbMats[i], lit ? oc * 0.9f : Color.black);
            }
        }

        public void TriggerUpgrade(int index, int lvl, int currentStaff, int staffsOwned)
        {
            Select(index, lvl, currentStaff, staffsOwned);
            reactT = 0.85f;
            if (!active || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var col = index == 3 ? StaffCols[staffKind] : ElementCols[index];
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                var m = CbArt.Glow(col * 1.4f, CbArt.Soft);
                var sp = CbArt.Part(CbMesh.Quad, m, pedestal, new Vector3(0, 1.05f, -0.1f), Vector3.one * 0.14f, shadows: false);
                sp.name = "wSpark";
                sparks.Add((sp, m, new Vector3(Mathf.Cos(a) * 0.9f, 0.55f + (i % 3) * 0.35f, Mathf.Sin(a) * 0.6f), 0f, 0.65f));
            }
        }

        /// <summary>Renders a crisp cached 3D snapshot of element `index` for the Workshop row icon.</summary>
        public Texture2D Thumb(int index, int currentStaff)
        {
            if (cam == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;
            index = Mathf.Clamp(index, 0, 3);
            if (index == 3 && thumbStaffKind != currentStaff)
            {
                if (thumbs[3] != null) UnityEngine.Object.Destroy(thumbs[3]);
                thumbs[3] = null;
                thumbStaffKind = currentStaff;
            }
            if (thumbs[index] != null) return thumbs[index];

            // save live state
            bool wasStage = stageRoot.gameObject.activeSelf;
            bool wasBaba = babaRoot.gameObject.activeSelf;
            bool wasPedestal = pedestal.gameObject.activeSelf;
            bool wasOrbs = orbRing.gameObject.activeSelf;
            int prevSel = selected, prevStaff = staffKind;
            var prevSpin = itemPivot.localRotation;
            var prevPos = itemPivot.localPosition;

            stageRoot.gameObject.SetActive(true);
            babaRoot.gameObject.SetActive(false);
            foreach (Transform child in pedestal)
                if (child != itemPivot) child.gameObject.SetActive(false);
            orbRing.gameObject.SetActive(false);

            for (int i = 0; i < 4; i++) itemSlots[i].gameObject.SetActive(i == index);
            for (int k = 0; k < 4; k++) staffSlots[k].gameObject.SetActive(k == Mathf.Clamp(currentStaff, 0, 3));
            itemPivot.localPosition = new Vector3(0, 1.06f, 0);
            itemPivot.localRotation = Quaternion.Euler(8f, 22f, 0f);

            var rt = RenderTexture.GetTemporary(96, 96, 16, RenderTextureFormat.ARGB32);
            var prevCamPos = cam.transform.localPosition;
            var prevCamRot = cam.transform.localRotation;
            var prevBg = cam.backgroundColor;
            var prevTarget = cam.targetTexture;
            bool prevCamOn = cam.enabled;

            cam.enabled = false;
            cam.targetTexture = rt;
            cam.backgroundColor = new Color(0.11f, 0.13f, 0.11f, 1f);
            cam.transform.localPosition = new Vector3(0.42f, 1.06f, -2.05f);
            cam.transform.LookAt(StageOrigin + new Vector3(0.42f, 1.06f, -0.1f));
            cam.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.ReadPixels(new Rect(0, 0, 96, 96), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            // restore live state
            cam.transform.localPosition = prevCamPos;
            cam.transform.localRotation = prevCamRot;
            cam.backgroundColor = prevBg;
            cam.targetTexture = prevTarget;
            cam.enabled = prevCamOn;

            babaRoot.gameObject.SetActive(wasBaba);
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
            itemPivot.localPosition = new Vector3(0, 1.06f + bob, 0);
            itemPivot.localRotation = Quaternion.Euler(0, spin, 0);

            for (int i = 0; i < 3; i++)
            {
                float a = clock * 1.4f + i * Mathf.PI * 2f / 3f;
                orbs[i].localPosition = new Vector3(Mathf.Cos(a) * 0.44f, bob * 0.5f + Mathf.Sin(clock * 3f + i) * 0.04f, Mathf.Sin(a) * 0.24f);
                orbs[i].localRotation = Quaternion.Euler(45f, spin * 1.5f + i * 60f, 0f);
            }

            if (reactT > 0)
            {
                reactT = Mathf.Max(0, reactT - dt);
                float u = 1f - reactT / 0.85f;
                float hop = Mathf.Sin(u * Mathf.PI) * 0.14f;
                babaRoot.localPosition = new Vector3(-0.50f, hop, 0.25f);
                babaRoot.localRotation = Quaternion.Euler(Mathf.Sin(u * Mathf.PI * 2f) * 8f, 148f, 0f);
                itemPivot.localScale = Vector3.one * (1f + Mathf.Sin(u * Mathf.PI) * 0.18f);
            }
            else
            {
                babaRoot.localPosition = new Vector3(-0.50f, 0f, 0.25f);
                babaRoot.localRotation = Quaternion.Euler(0f, 148f, 0f);
                itemPivot.localScale = Vector3.one;
            }

            if (babaModel != null)
            {
                string clip = reactT > 0 && babaModel.HasClip("Casual_Walk") ? "Casual_Walk"
                    : babaModel.HasClip("Idle_02") ? "Idle_02" : "Idle";
                babaModel.LoopSeconds(clip, clock, 0.15f);
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
                if (sp.age >= sp.life) { UnityEngine.Object.Destroy(sp.t.gameObject); sparks.RemoveAt(i); continue; }
                sp.t.localPosition += sp.v * dt;
                sp.v.y -= 0.7f * dt;
                float k = 1f - sp.age / sp.life;
                sp.t.localScale = Vector3.one * (0.14f * k);
                sparks[i] = sp;
            }
        }
    }
}
