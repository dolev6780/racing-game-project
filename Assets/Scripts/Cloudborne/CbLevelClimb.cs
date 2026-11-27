using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The climbing kit (the user: something reasonable to stand on and climb, tied to the jungle, for many stages -
    /// drawn in Canva, approved, built by Meshy). In the jungle a platform of lashed planks hanging on two ropes from a
    /// mossy branch over it, as if from the trees behind (the user: the rock pillars under the ledges looked bad - wooden
    /// ledges hanging from the background's trees instead); in the treetops a platform of planks round a trunk with a
    /// rope ladder (TreePlatform). Without the models the old slab and its support show.
    /// </summary>
    public partial class CbLevel
    {
        // the model's own size, as parts of its height
        const float PlatformTop = 0.70f, PlatformWide = 0.5f;

        /// <summary>The ledge at x (top, width) built from the climbing kit; false if this stage or this ledge has none
        /// (then the old slab is built).</summary>
        bool ClimbKitLedge(Transform t, float x, float width, float top)
        {
            if (!CbStage.Jungle && !CbStage.Treetops) return false;
            // (up the gorge and over the summit the ledges jut from the cliff's rock: its own slabs)
            if (CbStage.Jungle && x > MountainFrom - 1f && x < MountainTo + 1f) return false;
            if (CbStage.Jungle) return HangingPlatform(t, x, width, top);
            float baseY = Mathf.Max(SupportAt(x, top - 0.3f, false), -2.5f);
            if (top - baseY + 0.2f < 0.6f) return false;
            return TreePlatform(t, x, width, top, baseY);
        }

        // ---------- the hanging platforms ----------

        // how far over its top the branch it hangs from lies; the ropes' thickness
        const float BranchOver = 3.2f, HangRope = 0.034f;
        // the branch: as long as so many times the platform's width; its size as parts of its length (as drawn); where
        // its log's middle lies, as a part of its height (the ropes end in it)
        const float BranchLong = 1.35f, BranchTall = 0.39f, BranchDeep = 0.31f, BranchCore = 0.42f;

        /// <summary>A platform hanging in the jungle: where it is (in the level), its knots, where its ropes go up from.</summary>
        class Hanger { public Transform t; public float x0, x1, bottom, top, deep; public Vector3 knotL, knotR; public bool branch; }
        readonly List<Hanger> hangers = new List<Hanger>();
        static Material hangRopeMat;

        /// <summary>
        /// A platform of planks lashed with rope, its top the ledge; its two ropes, knotted at its back corners, go up to
        /// a branch (HangPlatforms, once the stage is built: a platform over another one's rope takes that rope).
        /// </summary>
        bool HangingPlatform(Transform t, float x, float width, float top)
        {
            // a narrow ledge: the small platform (its planks across)
            bool small = width <= 1.3f;
            float wide = width + 0.3f;
            // as drawn: its depth and thickness as parts of its width, where its ropes are knotted as parts of its half
            // width and half depth
            float deep = wide * (small ? 0.774f : 0.783f), thick = wide * (small ? 0.29f : 0.186f);
            float kx = small ? 0.82f : 0.885f, kz = small ? 0.48f : 0.72f;
            var holder = CbArt.Joint(t, 0, top - thick, 0f, "woodPlatform");
            if (Model(small ? "WoodPlatformSmall" : "WoodPlatform", holder, new Vector3(wide, thick, deep), 180f) == null)
            {
                Object.Destroy(holder.gameObject);
                return false;
            }
            // its planks' top where the ledge is (the knots stand a little higher)
            AddMeasuredLedge(t, holder, x, wide / 2 + 0.3f, top, width);
            var p = t.localPosition + holder.localPosition;
            hangers.Add(new Hanger
            {
                t = t, x0 = p.x - wide / 2, x1 = p.x + wide / 2, bottom = p.y, top = top, deep = deep,
                knotL = p + new Vector3(-kx * wide / 2, thick * 0.92f, kz * deep / 2),
                knotR = p + new Vector3(kx * wide / 2, thick * 0.92f, kz * deep / 2),
            });
            return true;
        }

        /// <summary>
        /// The hanging platforms' ropes and branches: each rope goes straight up from its knot to the first thing over
        /// it - another platform's underside or another's branch (the waterfall's ledges, one over the other), else the
        /// branch over its own platform. A platform whose ropes both end under others' has no branch of its own.
        /// </summary>
        void HangPlatforms()
        {
            if (hangers.Count == 0) return;
            if (hangRopeMat == null)
            {
                hangRopeMat = CbArt.NewToon(CbArt.Hex("#c4a272"), CbArt.PropOutline);
                hangRopeMat.SetFloat("_SpecStrength", 0f);
            }
            // a rope this close to a platform over it is tied under it (not brushing past its end)
            const float Near = 0.15f;
            float Len(Hanger h) => (h.x1 - h.x0) * BranchLong;
            float Tie(Hanger h) => h.top + BranchOver + Len(h) * BranchTall * BranchCore;
            // what is over a knot: the lowest platform (its underside), or with branches known, the lowest branch
            float Over(Hanger h, Vector3 k, bool branches)
            {
                float end = branches ? float.MaxValue : Tie(h);
                foreach (var o in hangers)
                {
                    if (o == h) continue;
                    if (o.bottom > k.y + 0.3f && o.bottom < end && k.x > o.x0 - Near && k.x < o.x1 + Near) end = o.bottom + 0.06f;
                    if (!branches || !o.branch) continue;
                    float half = Len(o) / 2 - 0.15f, mid = (o.x0 + o.x1) / 2;
                    if (Tie(o) > k.y + 0.3f && Tie(o) < end && Mathf.Abs(k.x - mid) < half) end = Tie(o);
                }
                return branches && h.branch ? Mathf.Min(end, Tie(h)) : end;
            }
            // first which have branches of their own (a rope with nothing over it), then the ropes
            foreach (var h in hangers) h.branch = Over(h, h.knotL, false) >= Tie(h) - 0.01f || Over(h, h.knotR, false) >= Tie(h) - 0.01f;
            foreach (var h in hangers)
            {
                foreach (var k in new[] { h.knotL, h.knotR })
                {
                    float end = Over(h, k, true);
                    // (in the platform's joint, as its ledge's)
                    var from = k - h.t.localPosition;
                    var rope = CbArt.Cylinder(hangRopeMat, h.t, new Vector3(from.x, (from.y + end - h.t.localPosition.y) / 2, from.z), HangRope, end - k.y);
                    rope.name = "hangRope";
                }
                if (!h.branch) continue;
                float len = Len(h);
                var at = new Vector3((h.x0 + h.x1) / 2, h.top + BranchOver, h.knotL.z) - h.t.localPosition;
                var holder = CbArt.Joint(h.t, at.x, at.y, at.z, "hangBranch");
                if (Model("MossBranch", holder, new Vector3(len, len * BranchTall, len * BranchDeep), 180f) == null)
                    CbArt.Cylinder(Wood, holder, new Vector3(0, len * BranchTall * BranchCore, 0), 0.12f, len, Quaternion.Euler(0, 0, 90));
            }
        }

        /// <summary>A platform of planks round a trunk, its top the ledge; the trunk behind the path.</summary>
        bool TreePlatform(Transform t, float x, float width, float top, float baseY)
        {
            float wide = width + 0.3f, h = (top - baseY) / PlatformTop;
            var holder = CbArt.Joint(t, 0, baseY, wide * 0.28f, "treePlatform");
            if (Model("TreePlatform", holder, new Vector3(wide, h, wide), 180f) == null) { Object.Destroy(holder.gameObject); return false; }
            AddMeasuredLedge(t, holder, x, wide / 2 + 0.3f, top, width);
            return true;
        }

        /// <summary>
        /// A ledge on a model's flat top: the model moved so its top lies on his line at `top`, the ledge as wide as the
        /// top is drawn there (as the stage laid it out, `width`, if the rays find none).
        /// </summary>
        void AddMeasuredLedge(Transform t, Transform holder, float x, float half, float top, float width)
        {
            var cs = Solidify(holder);
            float x0 = x - width / 2, x1 = x + width / 2;
            if (FindTop(cs, x, half, top, 0.8f, out float found, out _, out _))
            {
                Shift(holder, new Vector3(0, top - found, 0));
                FindTop(cs, x, half, top, 0.3f, out _, out x0, out x1);
            }
            else
            {
                Shift(holder, new Vector3(0, 0, -BestDepth(cs, x, half, top, 0.8f)));
                if (FindTop(cs, x, half, top, 0.8f, out found, out _, out _))
                {
                    Shift(holder, new Vector3(0, top - found, 0));
                    FindTop(cs, x, half, top, 0.3f, out _, out x0, out x1);
                }
            }
            ledges.Add(new Ledge { x0 = x0, x1 = x1, top = top, t = t, home = t.localPosition });
        }

        // ---------- a swing pole (the user: like the one in his picture, to swing round and fling off) ----------

        /// <summary>A pole sticking out of a ruin's pillar: he catches it, swings round it and lets go flying on.</summary>
        public class Pole { public float x, y, cool; public Transform t; }
        readonly List<Pole> poles = new List<Pole>();

        // the model's bar is PoleBarAt of its height up; turned PoleYaw it points mostly out of the screen, a little
        // slanted so its length shows; its middle stands PoleBack behind the path, the far end in the pillar
        const float PoleBarAt = 0.8f, PoleYaw = 115f, PoleBack = 0.75f, PoleLong = 1.5f, PoleBarRadius = 0.06f;

        // (none for now: the user takes them out of the river and will say where they go)
        void BuildPoles() { }

        /// <summary>Test hook: a swing pole at (x, y), its bar's middle there.</summary>
        public void TestAddPole(float x, float y) => AddPole(x, y);

        void AddPole(float x, float y)
        {
            var t = CbArt.Joint(root, x, 0, 0, "swingPole");
            // the pillar it comes out of, from below the water up past it, behind the path
            var pil = CbArt.Joint(t, 0, -2f, PoleBack + 0.75f, "pillar");
            float ph = y + 2.9f;
            pil.localScale = new Vector3(0.9f / 1.48f, ph / 3.6f, 0.9f / 1.5f);
            if (CbArt.Prop("Cloudborne/Jungle/Pillar/Pillar", pil, CbArt.PropOutline) == null)
                CbArt.Box(StoneGrey, pil, new Vector3(0, 1.8f, 0), new Vector3(1.48f, 3.6f, 1.5f));
            float h = PoleLong / 1.18f;
            var bar = CbArt.Joint(t, 0, y - PoleBarAt * h, PoleBack, "pole");
            float py = y;
            if (Model("SwingPole", bar, new Vector3(PoleLong, h, PoleLong * 0.28f), PoleYaw) == null)
                CbArt.Cylinder(Bamboo, bar, new Vector3(0, PoleBarAt * h, 0), 0.07f, PoleLong, Quaternion.Euler(90, 0, 0));
            else
            {
                // his hands on the bar itself (the user: touching the pole, not in the air): the bar found where it is
                // drawn, across his line, his hands' height its middle
                var cs = Solidify(bar);
                Shift(bar, new Vector3(0, 0, -BestDepth(cs, x, 0.15f, y, 0.4f)));
                float top = float.NegativeInfinity;
                foreach (float z in ProbeZ)
                    if (TopOf(cs, x, z, y - 0.4f, y + 0.4f, out float tz)) top = Mathf.Max(top, tz);
                if (top > float.NegativeInfinity) py = top - PoleBarRadius;
            }
            poles.Add(new Pole { x = x, y = py, t = t });
        }

        /// <summary>The pole his hands reach at (hx, hy), if one does and it isn't just let go of.</summary>
        public Pole PoleNear(float hx, float hy)
        {
            foreach (var p in poles) if (p.cool <= 0 && Mathf.Abs(hx - p.x) < 0.6f && Mathf.Abs(hy - p.y) < 0.55f) return p;
            return null;
        }

        void UpdatePoles(float dt)
        {
            foreach (var p in poles) p.cool = Mathf.Max(0, p.cool - dt);
        }
    }
}
