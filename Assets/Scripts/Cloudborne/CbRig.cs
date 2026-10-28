using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>Joint angles (radians) of a humanoid; the demo's pose numbers carry over unchanged.</summary>
    public struct Pose
    {
        public float bob, lean, twist, headX, sLx, sLz, eL, sRx, sRz, eR, hLx, kL, hRx, kR, staffX, spin;

        /// <summary>Fighting stance.</summary>
        public static Pose Base => new Pose
        {
            bob = 0, lean = 0.08f, twist = 0, headX = 0, sLx = -0.35f, sLz = 0.2f, eL = -1.6f, sRx = -0.2f, sRz = -0.2f, eR = -1.4f,
            hLx = -0.25f, kL = 0.35f, hRx = 0.25f, kR = 0.35f,
        };

        public static void Run(ref Pose p, float ph, float amt = 1f)
        {
            float s = Mathf.Sin(ph), c = Mathf.Cos(ph);
            p.hLx = -s * 0.95f * amt; p.hRx = s * 0.95f * amt;
            p.kL = (0.25f + Mathf.Max(0, c) * 1.3f) * amt; p.kR = (0.25f + Mathf.Max(0, -c) * 1.3f) * amt;
            p.sLx = s * 0.9f * amt; p.sRx = -s * 0.9f * amt; p.eL = -1.4f; p.eR = -1.4f;
            p.sLz = 0.15f; p.sRz = -0.15f;
            p.lean = 0.32f; p.bob = Mathf.Abs(c) * 0.08f - 0.03f;
            p.twist = s * 0.2f;
        }
    }

    public class Limb { public Transform sh, el, hand; }
    public class Leg { public Transform hp, kn; }

    /// <summary>
    /// A procedural humanoid built from primitives: hips, chest, head, arms and legs on joints, posed by angles.
    /// The model faces +Z; its root turns it towards left or right.
    /// </summary>
    public class Rig
    {
        public Transform root, flip, body, hips, chest, head;
        public Limb L, R;
        public Leg LL, RL;
        public readonly List<Transform> tail = new List<Transform>(), bandTails = new List<Transform>(), sash = new List<Transform>();
        public readonly List<Material> mats = new List<Material>();
        public Material fur, paw;
        public Transform shield;
        Pose cur;
        bool posed;

        public Material M(Color c, float outline = CbArt.CharacterOutline, Texture tex = null)
        {
            var m = CbArt.NewToon(c, outline, tex);
            mats.Add(m);
            return m;
        }

        public Material M(string hex, float outline = CbArt.CharacterOutline) => M(CbArt.Hex(hex), outline);

        public void Apply(Pose t, float dt, float k)
        {
            float a = posed ? 1f - Mathf.Exp(-k * dt) : 1f;
            posed = true;
            cur.bob += (t.bob - cur.bob) * a; cur.lean += (t.lean - cur.lean) * a; cur.twist += (t.twist - cur.twist) * a;
            cur.headX += (t.headX - cur.headX) * a;
            cur.sLx += (t.sLx - cur.sLx) * a; cur.sLz += (t.sLz - cur.sLz) * a; cur.eL += (t.eL - cur.eL) * a;
            cur.sRx += (t.sRx - cur.sRx) * a; cur.sRz += (t.sRz - cur.sRz) * a; cur.eR += (t.eR - cur.eR) * a;
            cur.hLx += (t.hLx - cur.hLx) * a; cur.kL += (t.kL - cur.kL) * a; cur.hRx += (t.hRx - cur.hRx) * a; cur.kR += (t.kR - cur.kR) * a;

            hips.localPosition = new Vector3(0, 0.92f + cur.bob, 0);
            hips.localRotation = CbArt.Rot(cur.lean);
            chest.localRotation = CbArt.Rot(0, cur.twist);
            head.localRotation = CbArt.Rot(cur.headX);
            L.sh.localRotation = CbArt.Rot(cur.sLx, 0, cur.sLz);
            L.el.localRotation = CbArt.Rot(cur.eL);
            R.sh.localRotation = CbArt.Rot(cur.sRx, 0, cur.sRz);
            R.el.localRotation = CbArt.Rot(cur.eR);
            LL.hp.localRotation = CbArt.Rot(cur.hLx);
            LL.kn.localRotation = CbArt.Rot(cur.kL);
            RL.hp.localRotation = CbArt.Rot(cur.hRx);
            RL.kn.localRotation = CbArt.Rot(cur.kR);
        }

        public void SetFlash(Color c, float amount)
        {
            var e = amount > 0 ? c * amount : Color.black;
            foreach (var m in mats) CbArt.SetGlow(m, e);
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root.gameObject);
            // a little later: a dropped shield may still be falling with these materials
            foreach (var m in mats) if (m != null) Object.Destroy(m, 3f);
        }
    }

    public class RigStyle
    {
        public Material furMat;
        public Color fur, paw, pants, belt, wrap;
        public Color? top, chestPatch, armor;
        public float bulk = 1f, scale = 1f;
        public bool sash, tail, claws;
    }

    public static class RigBuilder
    {
        public static Rig Build(RigStyle o, string name)
        {
            var f = new Rig();
            f.root = new GameObject(name).transform;
            f.flip = CbArt.Joint(f.root, 0, 1f, 0, "flip");
            f.body = CbArt.Joint(f.flip, 0, -1f, 0, "body");
            f.fur = o.furMat != null ? o.furMat : f.M(o.fur);
            if (o.furMat != null) f.mats.Add(o.furMat);
            f.paw = f.M(o.paw);
            var pants = f.M(o.pants);
            var belt = f.M(o.belt);
            var wrap = f.M(o.wrap);
            var top = o.top.HasValue ? f.M(o.top.Value) : f.fur;
            float b = o.bulk;

            f.hips = CbArt.Joint(f.body, 0, 0.92f, 0, "hips");
            CbArt.Sphere(pants, f.hips, Vector3.zero, 0.22f, new Vector3(1.15f * b, 0.75f, 0.85f * b));
            CbArt.Cylinder(belt, f.hips, new Vector3(0, 0.1f, 0), 0.255f * b, 0.1f);
            if (o.sash)
                foreach (float dz in new[] { -0.05f, 0.06f })
                {
                    var sj = CbArt.Joint(f.hips, 0.25f * b, 0.08f, dz, "sash");
                    CbArt.Box(belt, sj, new Vector3(0, -0.19f, 0), new Vector3(0.05f, 0.38f, 0.09f));
                    f.sash.Add(sj);
                }
            f.chest = CbArt.Joint(f.hips, 0, 0.12f, 0, "chest");
            var torso = CbArt.Capsule(top, f.chest, new Vector3(0, 0.3f, 0), 0.24f, 0.3f);
            torso.localScale = Vector3.Scale(torso.localScale, new Vector3(1.15f * b, 1, 0.82f * b));
            if (o.chestPatch.HasValue)
                CbArt.Sphere(f.M(o.chestPatch.Value, 0f), f.chest, new Vector3(0, 0.3f, 0.115f), 0.2f, new Vector3(0.95f, 1.25f, 0.6f));
            if (o.armor.HasValue)
            {
                var am = f.M(o.armor.Value);
                foreach (int sx in new[] { -1, 1 })
                    CbArt.Part(CbMesh.Dome(0.5f), am, f.chest, new Vector3(sx * 0.34f * b, 0.6f, 0), new Vector3(0.15f * 1.25f, 0.15f * 0.9f, 0.15f * 1.25f));
            }
            f.head = CbArt.Joint(f.chest, 0, 0.72f, 0, "head");

            Limb Arm(int side)
            {
                var sh = CbArt.Joint(f.chest, side * 0.34f * b, 0.52f, 0, side > 0 ? "armL" : "armR");
                CbArt.Sphere(top, sh, Vector3.zero, 0.11f);
                CbArt.Capsule(f.fur, sh, new Vector3(0, -0.15f, 0), 0.078f, 0.2f);
                var el = CbArt.Joint(sh, 0, -0.32f, 0, "elbow");
                CbArt.Capsule(f.fur, el, new Vector3(0, -0.13f, 0), 0.072f, 0.18f);
                CbArt.Cylinder(wrap, el, new Vector3(0, -0.19f, 0), 0.084f, 0.13f);
                var hand = CbArt.Joint(el, 0, -0.3f, 0, "hand");
                CbArt.Sphere(f.paw, hand, Vector3.zero, 0.1f);
                if (o.claws)
                {
                    var claw = f.M(CbArt.Hex("#fffbf0"), 0f);
                    foreach (float dx in new[] { -0.045f, 0f, 0.045f })
                        CbArt.Cone(claw, hand, new Vector3(dx, -0.07f, 0.07f), 0.018f, 0.08f, CbArt.Rot(Mathf.PI * 0.75f), 5);
                }
                return new Limb { sh = sh, el = el, hand = hand };
            }
            f.L = Arm(1); f.R = Arm(-1);

            Leg MakeLeg(int side)
            {
                var hp = CbArt.Joint(f.hips, side * 0.13f, -0.05f, 0, "leg");
                CbArt.Capsule(pants, hp, new Vector3(0, -0.2f, 0), 0.1f, 0.26f);
                var kn = CbArt.Joint(hp, 0, -0.42f, 0, "knee");
                CbArt.Capsule(pants, kn, new Vector3(0, -0.17f, 0), 0.085f, 0.22f);
                CbArt.Cylinder(wrap, kn, new Vector3(0, -0.34f, 0), 0.09f, 0.1f);
                CbArt.Sphere(f.paw, kn, new Vector3(0, -0.43f, 0.06f), 0.11f, new Vector3(1, 0.6f, 1.5f));
                return new Leg { hp = hp, kn = kn };
            }
            f.LL = MakeLeg(1); f.RL = MakeLeg(-1);

            if (o.tail)
            {
                var black = f.M(CbArt.Hex("#1d1310"));
                var parent = CbArt.Joint(f.hips, 0, -0.02f, -0.18f, "tail");
                for (int k = 0; k < 6; k++)
                {
                    f.tail.Add(parent);
                    CbArt.Capsule(k % 2 == 1 || k == 5 ? black : f.paw, parent, new Vector3(0, 0, -0.11f), 0.055f, 0.14f, CbArt.Rot(Mathf.PI / 2));
                    parent = CbArt.Joint(parent, 0, 0, -0.21f, "tail");
                }
            }
            f.root.localScale = Vector3.one * o.scale;
            return f;
        }

        // ---------- heads ----------

        public static void TigerHead(Rig f)
        {
            var h = f.head;
            var white = f.M(CbArt.Hex("#fff4e2"));
            var whiteFlat = f.M(CbArt.Hex("#fff4e2"), 0f);
            CbArt.Sphere(f.fur, h, new Vector3(0, 0.2f, 0), 0.27f, new Vector3(1.06f, 0.98f, 1f));
            CbArt.Sphere(white, h, new Vector3(0, 0.11f, 0.19f), 0.13f, new Vector3(1.35f, 0.82f, 0.95f));
            CbArt.Sphere(f.M(CbArt.Hex("#3a1f22"), 0f), h, new Vector3(0, 0.17f, 0.3f), 0.045f, new Vector3(1.3f, 0.75f, 0.8f));
            var eyeW = CbArt.Emissive(CbArt.Hex("#fffbea"));
            var iris = CbArt.Emissive(CbArt.Hex("#f2a20c"));
            var pupil = CbArt.Emissive(CbArt.Hex("#140c08"));
            var brow = f.M(CbArt.Hex("#1d1310"), 0f);
            foreach (int sx in new[] { -1, 1 })
            {
                CbArt.Cone(white, h, new Vector3(sx * 0.26f, 0.1f, 0.03f), 0.075f, 0.2f, CbArt.Rot(0, 0, sx * (Mathf.PI / 2 + 0.45f)), 6);
                var ear = CbArt.Cone(f.paw, h, new Vector3(sx * 0.17f, 0.44f, -0.03f), 0.1f, 0.18f, CbArt.Rot(0, 0, -sx * 0.35f), 8);
                ear.localScale = new Vector3(1, 1, 0.55f);
                var inner = CbArt.Cone(whiteFlat, h, new Vector3(sx * 0.163f, 0.425f, 0f), 0.06f, 0.11f, CbArt.Rot(0, 0, -sx * 0.35f), 8);
                inner.localScale = new Vector3(1, 1, 0.4f);
                CbArt.Part(CbMesh.Sphere, eyeW, h, new Vector3(sx * 0.1f, 0.25f, 0.232f), new Vector3(0.112f, 0.129f, 0.05f), shadows: false);
                CbArt.Part(CbMesh.Sphere, iris, h, new Vector3(sx * 0.098f, 0.245f, 0.255f), new Vector3(0.068f, 0.075f, 0.034f), shadows: false);
                CbArt.Part(CbMesh.Sphere, pupil, h, new Vector3(sx * 0.097f, 0.245f, 0.268f), Vector3.one * 0.032f, shadows: false);
                CbArt.Box(brow, h, new Vector3(sx * 0.1f, 0.325f, 0.235f), new Vector3(0.1f, 0.026f, 0.03f), CbArt.Rot(0, 0, sx * 0.3f));
            }
            var red = f.M(CbArt.Hex("#c8323c"));
            // headband: a torus in the XZ plane, tilted forward a little
            CbArt.Part(CbMesh.Torus(0.03f / 0.276f), red, h, new Vector3(0, 0.31f, 0), Vector3.one * 0.276f, CbArt.Rot(0.12f));
            foreach (int sx in new[] { -1, 1 })
            {
                var tj = CbArt.Joint(h, sx * 0.05f, 0.31f, -0.26f, "bandTail");
                CbArt.Box(red, tj, new Vector3(0, 0, -0.2f), new Vector3(0.06f, 0.03f, 0.42f));
                f.bandTails.Add(tj);
            }
        }

        public static void HyenaHead(Rig f)
        {
            var h = f.head;
            CbArt.Sphere(f.fur, h, new Vector3(0, 0.19f, 0), 0.24f);
            CbArt.Capsule(f.M(CbArt.Hex("#8a6a40")), h, new Vector3(0, 0.12f, 0.22f), 0.09f, 0.16f, CbArt.Rot(Mathf.PI / 2));
            CbArt.Sphere(f.M(CbArt.Hex("#1a1210"), 0f), h, new Vector3(0, 0.14f, 0.37f), 0.045f);
            var dark = f.M(CbArt.Hex("#4a3626"));
            var eye = CbArt.Emissive(CbArt.Hex("#ffd84a") * 2.2f);
            foreach (int sx in new[] { -1, 1 })
            {
                CbArt.Sphere(dark, h, new Vector3(sx * 0.16f, 0.4f, -0.02f), 0.09f, new Vector3(1, 1.15f, 0.4f));
                CbArt.Part(CbMesh.Sphere, eye, h, new Vector3(sx * 0.08f, 0.24f, 0.2f), Vector3.one * 0.056f, shadows: false);
            }
            for (int k = 0; k < 4; k++)
                CbArt.Cone(dark, h, new Vector3(0, 0.36f - k * 0.09f, -0.2f - k * 0.03f), 0.06f, 0.24f, CbArt.Rot(-0.9f), 5);
            CbArt.Part(CbMesh.Dome(0.42f), f.M(CbArt.Hex("#6d7480")), h, new Vector3(0, 0.2f, 0), Vector3.one * 0.262f, CbArt.Rot(-0.12f));
            var gold = f.M(CbArt.Hex("#d9a53a"), 0f);
            for (int k = -2; k <= 2; k++)
                CbArt.Cone(gold, h, new Vector3(Mathf.Sin(k * 0.35f) * 0.22f, 0.42f, Mathf.Cos(k * 0.35f) * 0.13f), 0.03f, 0.1f, CbArt.Rot(0.3f), 4);
        }

        /// <summary>Bamboo staff along its local Y axis; the grip (origin) sits a third of the way along.</summary>
        public static Transform Staff()
        {
            var g = new GameObject("staff").transform;
            var inner = CbArt.Joint(g, 0, 0.35f, 0, "bamboo");
            CbArt.Part(CbMesh.Frustum(0.92f, 10, true), CbArt.Toon("#86b84e", CbArt.CharacterOutline), inner, new Vector3(0, -1.05f, 0), new Vector3(0.05f, 2.1f, 0.05f));
            var node = CbArt.Toon("#4f7f2a");
            for (int k = -3; k <= 3; k++) CbArt.Cylinder(node, inner, new Vector3(0, k * 0.3f, 0), 0.057f, 0.04f);
            var rope = CbArt.Toon("#c8323c");
            foreach (float y in new[] { -0.42f, -0.28f, 0.9f }) CbArt.Cylinder(rope, inner, new Vector3(0, y, 0), 0.056f, 0.07f);
            return g;
        }
    }
}
