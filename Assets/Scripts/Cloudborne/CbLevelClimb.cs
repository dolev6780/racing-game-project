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
        const float BranchOver = 3f, HangRope = 0.034f;
        // the fallback branch (when no tree fits): as long as so many times the platform's width; its size as parts of
        // its length (as drawn); where its log's middle lies, as a part of its height (the ropes end in it)
        const float BranchLong = 1.35f, BranchTall = 0.39f, BranchDeep = 0.31f, BranchCore = 0.42f;
        // The tree it hangs from (the user: a beautiful tree in the background whose branch comes out over the path and
        // holds the platform - drawn in Canva, approved, built by Meshy). As drawn: its width and depth as parts of its
        // height; its depth squeezed (TreeSquash: its roots spread far to the front and back); its branch's underside
        // TreeBranchAt of its height up, the stretch the ropes hang from TreeBranchMid of its width from its middle.
        // It stands TreeZ back, turned so its branch reaches forward and over the platform's back.
        const float TreeWide = 1.139f, TreeDeep = 0.785f, TreeSquash = 0.35f, TreeBranchAt = 0.49f, TreeBranchMid = 0.39f,
            TreeZ = 4f, TreeSink = 0.25f, TreeMaxTurn = 40f;

        /// <summary>A platform hanging in the jungle: where it is (in the level), its knots, where its ropes go up from.</summary>
        class Hanger { public Transform t; public float x0, x1, bottom, top, deep; public Vector3 knotL, knotR; public bool branch; }
        readonly List<Hanger> hangers = new List<Hanger>();
        static Material hangRopeMat;

        /// <summary>
        /// A platform of planks lashed with rope, its top the ledge; its two ropes, knotted at its back corners, go up to
        /// the branch of a tree behind the path (HangPlatforms, once the stage is built: a platform over another one's
        /// rope takes that rope).
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
        /// The hanging platforms' ropes, trees and branches. A rope with another platform over it is tied under that one
        /// (the waterfall's ledges, one over the other); a platform with a rope free of them gets a tree behind the path,
        /// its branch over the platform, the ropes tied to the branch where the rays from the knots meet it. Where no
        /// tree's branch can reach, a mossy branch of its own over it (as before).
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
            // the platform over a knot (its underside), or float.MaxValue
            float Under(Hanger h, Vector3 k)
            {
                float end = float.MaxValue;
                foreach (var o in hangers)
                    if (o != h && o.bottom > k.y + 0.3f && o.bottom < end && k.x > o.x0 - Near && k.x < o.x1 + Near) end = o.bottom + 0.06f;
                return end;
            }
            var treeSolids = new List<Collider>();
            foreach (var h in hangers)
            {
                h.branch = Under(h, h.knotL) == float.MaxValue || Under(h, h.knotR) == float.MaxValue;
                if (h.branch && BranchTree(h, treeSolids)) h.branch = false;
            }
            // (trees first: a rope under another's tree's branch is tied to that)
            foreach (var h in hangers)
            {
                foreach (var k in new[] { h.knotL, h.knotR })
                {
                    float under = Under(h, k);
                    Vector3 end;
                    if (under != float.MaxValue) end = new Vector3(k.x, under, k.z);
                    else if (!FindBranch(k, treeSolids, out end))
                    {
                        if (!h.branch) { h.branch = true; }
                        end = new Vector3(k.x, h.top + BranchOver + (h.x1 - h.x0) * BranchLong * BranchTall * BranchCore, k.z);
                    }
                    HangRopeBetween(h.t, k, end);
                }
                if (!h.branch) continue;
                // no tree's branch over it: a branch of its own
                float len = (h.x1 - h.x0) * BranchLong;
                var at = new Vector3((h.x0 + h.x1) / 2, h.top + BranchOver, h.knotL.z) - h.t.localPosition;
                var holder = CbArt.Joint(h.t, at.x, at.y, at.z, "hangBranch");
                if (Model("MossBranch", holder, new Vector3(len, len * BranchTall, len * BranchDeep), 180f) == null)
                    CbArt.Cylinder(Wood, holder, new Vector3(0, len * BranchTall * BranchCore, 0), 0.12f, len, Quaternion.Euler(0, 0, 90));
            }
            // the trees only drawn: nothing in the game touches them
            foreach (var c in treeSolids) if (c != null) Object.DestroyImmediate(c);
        }

        /// <summary>A rope from a knot (in the level) up to where it is tied, in the platform's joint.</summary>
        void HangRopeBetween(Transform t, Vector3 from, Vector3 to)
        {
            var d = to - from;
            var rope = CbArt.Cylinder(hangRopeMat, t, (from + to) / 2 - t.localPosition, HangRope, d.magnitude, Quaternion.FromToRotation(Vector3.up, d.normalized));
            rope.name = "hangRope";
        }

        /// <summary>Where a rope from knot k meets a tree's branch over it: straight up, or as little to the front or back
        /// as it takes (the branch crosses the path at a slant).</summary>
        bool FindBranch(Vector3 k, List<Collider> solids, out Vector3 at)
        {
            at = default;
            if (solids.Count == 0) return false;
            var from0 = root.TransformPoint(k + Vector3.up * 0.4f);
            for (int i = 0; i <= 16; i++)
            {
                float dz = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 0.1f;
                var from = from0 + Vector3.forward * dz;
                float best = float.MaxValue;
                foreach (var c in solids)
                    if (c != null && c.Raycast(new Ray(from, Vector3.up), out var hit, BranchOver * 2.5f) && hit.distance < best)
                    {
                        best = hit.distance;
                        // a little into the branch
                        at = root.InverseTransformPoint(hit.point + Vector3.up * 0.08f);
                    }
                if (best < float.MaxValue && at.y > k.y + 1.2f) return true;
            }
            return false;
        }

        /// <summary>Whether a tree's branch passes through the room over another platform (he would stand in it).</summary>
        bool OverAnother(Hanger h, List<Collider> cs)
        {
            foreach (var o in hangers)
            {
                if (o == h) continue;
                for (float x = o.x0; x <= o.x1 + 0.01f; x += (o.x1 - o.x0) / 4f)
                    for (float z = -0.4f; z <= 0.8f; z += 0.4f)
                    {
                        var from = root.TransformPoint(new Vector3(x, o.top + 0.1f, z));
                        foreach (var c in cs)
                            if (c != null && c.Raycast(new Ray(from, Vector3.up), out _, HeadRoom)) return true;
                    }
            }
            return false;
        }
        // the room over a platform no branch may pass through
        const float HeadRoom = 2.4f;

        /// <summary>
        /// A tree behind the path, its branch over the platform: as tall as puts its branch BranchOver over the
        /// platform, on whichever side its trunk stands on dry ground (out of the streams), turned so its branch
        /// reaches forward to the platform's back. Kept (its branch made solid until the ropes are tied) if rays from
        /// both knots meet the branch.
        /// </summary>
        bool BranchTree(Hanger h, List<Collider> solids)
        {
            float cx = (h.x0 + h.x1) / 2, zk = h.knotL.z;
            float tall = (h.top + BranchOver + TreeSink) / TreeBranchAt, wide = tall * TreeWide;
            float reach = TreeBranchMid * wide;
            float sin = Mathf.Clamp((TreeZ - zk) / reach, 0f, Mathf.Sin(TreeMaxTurn * Mathf.Deg2Rad));
            float turn = Mathf.Asin(sin) * Mathf.Rad2Deg, cos = Mathf.Cos(turn * Mathf.Deg2Rad);
            var size = new Vector3(wide, tall, tall * TreeDeep * TreeSquash);
            // the side with dry ground under the trunk first (the one away from the next platform when both are)
            var sides = new List<int>();
            foreach (int s in new[] { 1, -1 })
            {
                float tx = cx - s * reach * cos;
                if (!InWater(tx, 2f)) sides.Add(s);
            }
            if (sides.Count == 2)
            {
                int i = hangers.IndexOf(h);
                if (i % 2 == 1) sides.Reverse();
            }
            foreach (int s in sides)
            {
                // which way the model's branch points isn't known here: of the two turns for this side, the one whose
                // branch the rays meet
                float[] yaws = s > 0 ? new[] { turn, 180f + turn } : new[] { 180f - turn, -turn };
                foreach (float yaw in yaws)
                {
                    var at = new Vector3(cx - s * reach * cos, -TreeSink, zk + reach * sin);
                    var tree = CbArt.Joint(root, at.x, at.y, at.z, "branchTree");
                    if (Model("BranchTree", tree, size, yaw) == null) { Object.Destroy(tree.gameObject); return false; }
                    var cs = Solidify(tree);
                    Physics.SyncTransforms();
                    if (FindBranch(h.knotL, cs, out var a) && FindBranch(h.knotR, cs, out var b)
                        && a.y < h.top + BranchOver * 1.8f && b.y < h.top + BranchOver * 1.8f && !OverAnother(h, cs))
                    {
                        solids.AddRange(cs);
                        return true;
                    }
                    Object.DestroyImmediate(tree.gameObject);
                }
            }
            return false;
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
