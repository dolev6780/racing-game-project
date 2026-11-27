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
            TreeZ = 5.5f, TreeSink = 0.25f, TreeMaxTurn = 45f;

        /// <summary>A platform hanging in the jungle: where it is (in the level), its knots, where its ropes go up from.</summary>
        class Hanger { public Transform t; public float x0, x1, bottom, top, deep, over; public Vector3 knotL, knotR; public bool branch; }
        // how far under its branch each platform hangs, in turn (the user: the platforms at a few heights under the branch)
        static readonly float[] HangOvers = { 2.7f, 3.6f, 3.1f, 4.3f };
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
                t = t, x0 = p.x - wide / 2, x1 = p.x + wide / 2, bottom = p.y, top = top, deep = deep, over = HangOvers[hangers.Count % HangOvers.Length],
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
                hangRopeMat.SetFloat("_Cull", 0f);
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
                    Lashing lash = null;
                    if (under != float.MaxValue) end = new Vector3(k.x, under, k.z);
                    else if (FindBranch(k, treeSolids, out end))
                    {
                        // wound round the branch where it meets it (the user: they only touched it)
                        lash = Lash(end, treeSolids);
                        if (lash != null) end = lash.centre - Vector3.up * lash.radius;
                        else end += Vector3.up * 0.08f;
                    }
                    else
                    {
                        if (!h.branch) { h.branch = true; }
                        end = new Vector3(k.x, h.top + h.over + (h.x1 - h.x0) * BranchLong * BranchTall * BranchCore, k.z);
                    }
                    HangRopeBetween(h.t, k, end, lash);
                }
                if (!h.branch) continue;
                // no tree's branch over it: a branch of its own
                float len = (h.x1 - h.x0) * BranchLong;
                var at = new Vector3((h.x0 + h.x1) / 2, h.top + h.over, h.knotL.z) - h.t.localPosition;
                var holder = CbArt.Joint(h.t, at.x, at.y, at.z, "hangBranch");
                if (Model("MossBranch", holder, new Vector3(len, len * BranchTall, len * BranchDeep), 180f) == null)
                    CbArt.Cylinder(Wood, holder, new Vector3(0, len * BranchTall * BranchCore, 0), 0.12f, len, Quaternion.Euler(0, 0, 90));
            }
            // the trees only drawn: nothing in the game touches them
            foreach (var c in treeSolids) if (c != null) Object.DestroyImmediate(c);
        }

        /// <summary>Where a rope is wound round a branch (in the level): the branch's middle there, its way, its
        /// half-thickness.</summary>
        class Lashing { public Vector3 centre, axis; public float radius; }

        /// <summary>
        /// The branch round the point under it (in the level) where a rope meets it: its thickness from a ray down onto
        /// its top, its way from rays up at a little distance round about (where they still meet it on both sides).
        /// Null if it can't be told (the rope then just goes into it).
        /// </summary>
        Lashing Lash(Vector3 under, List<Collider> solids)
        {
            bool Up(Vector3 p, out float y)
            {
                y = 0;
                var from = root.TransformPoint(new Vector3(p.x, under.y - 0.6f, p.z));
                float best = float.MaxValue;
                foreach (var c in solids)
                    if (c != null && c.Raycast(new Ray(from, Vector3.up), out var hit, 1.4f) && hit.distance < best) best = hit.distance;
                if (best == float.MaxValue) return false;
                y = under.y - 0.6f + best;
                return true;
            }
            // its top, straight over: from above, down
            var above = root.TransformPoint(under + Vector3.up * 2f);
            float topDist = float.MaxValue;
            foreach (var c in solids)
                if (c != null && c.Raycast(new Ray(above, Vector3.down), out var hit, 2f) && hit.distance < topDist) topDist = hit.distance;
            if (topDist == float.MaxValue) return null;
            float thick = (under.y + 2f - topDist) - under.y;
            if (thick < 0.08f || thick > 1.4f) return null;
            // its way: the turn round which the branch's underside goes on, both ways, most evenly
            Vector3 best = Vector3.zero;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < 12; i++)
            {
                float ang = i * Mathf.PI / 12;
                var d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 0.3f;
                if (!Up(under + d, out float ya) || !Up(under - d, out float yb)) continue;
                float diff = Mathf.Abs(ya - under.y) + Mathf.Abs(yb - under.y);
                if (diff < bestDiff) { bestDiff = diff; best = d.normalized; }
            }
            if (best == Vector3.zero) return null;
            return new Lashing { centre = under + Vector3.up * thick / 2, axis = best, radius = thick / 2 + HangRope * 0.6f };
        }

        /// <summary>
        /// A rope from a knot (in the level) up to where it is tied, in the platform's joint: two strands twisted round
        /// each other like the platform's own lashings (a plain stick looked poor: the user); wound three times round
        /// the branch where it meets it (it only touched the branch: the user).
        /// </summary>
        void HangRopeBetween(Transform t, Vector3 from, Vector3 to, Lashing lash = null)
        {
            var d = to - from;
            float len = d.magnitude;
            var axis = d / Mathf.Max(0.001f, len);
            var side = Vector3.Cross(axis, Vector3.forward);
            if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(axis, Vector3.right);
            side.Normalize();
            var up = Vector3.Cross(side, axis);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var o = from - t.localPosition;
            for (int strand = 0; strand < 2; strand++)
            {
                var pts = new List<Vector3>();
                var radii = new List<float>();
                for (float s = 0; s <= len + 0.001f; s += Mathf.Min(0.04f, len / 8f))
                {
                    // a twist about every 9 cm
                    float turn = s / 0.09f * Mathf.PI * 2 + strand * Mathf.PI;
                    pts.Add(o + axis * s + (side * Mathf.Cos(turn) + up * Mathf.Sin(turn)) * HangRope * 0.55f);
                    radii.Add(HangRope * 0.6f);
                }
                TaperedRope(verts, tris, pts, radii, 5);
            }
            if (lash != null)
            {
                // the turns round the branch: a coil along it, a rope's width apart, from the rope's own side
                var c = lash.centre - t.localPosition;
                var a = lash.axis;
                var u = Vector3.Cross(a, Vector3.up).normalized;
                if (u.sqrMagnitude < 0.01f) u = Vector3.forward;
                const int Turns = 3, Steps = 18;
                var pts = new List<Vector3>();
                var radii = new List<float>();
                for (int i = 0; i <= Turns * Steps; i++)
                {
                    float f = i / (float)Steps, ang = f * Mathf.PI * 2 - Mathf.PI / 2;
                    pts.Add(c + a * (f - Turns / 2f) * HangRope * 2.3f + (Vector3.up * Mathf.Sin(ang) + u * Mathf.Cos(ang)) * lash.radius);
                    radii.Add(HangRope * 0.75f);
                }
                TaperedRope(verts, tris, pts, radii, 5);
            }
            var mesh = new Mesh { name = "hangRope" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("hangRope");
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = hangRopeMat;
        }

        /// <summary>Where a rope from knot k meets a tree's branch over it: straight up, or as little to the front or back
        /// as it takes (the branch crosses the path at a slant).</summary>
        bool FindBranch(Vector3 k, List<Collider> solids, out Vector3 at)
        {
            at = default;
            if (solids.Count == 0) return false;
            var from0 = root.TransformPoint(k + Vector3.up * 0.4f);
            for (int i = 0; i <= 24; i++)
            {
                float dz = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 0.1f;
                var from = from0 + Vector3.forward * dz;
                float best = float.MaxValue;
                foreach (var c in solids)
                    if (c != null && c.Raycast(new Ray(from, Vector3.up), out var hit, 12f) && hit.distance < best)
                    {
                        best = hit.distance;
                        at = root.InverseTransformPoint(hit.point);
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
        /// A tree behind the path, its branch over the platform: as tall as puts its branch the platform's `over` above the
        /// platform, on whichever side its trunk stands on dry ground (out of the streams), turned so its branch
        /// reaches forward to the platform's back. Kept (its branch made solid until the ropes are tied) if rays from
        /// both knots meet the branch.
        /// </summary>
        bool BranchTree(Hanger h, List<Collider> solids)
        {
            float cx = (h.x0 + h.x1) / 2, zk = h.knotL.z;
            float tall = (h.top + h.over + TreeSink) / TreeBranchAt, wide = tall * TreeWide;
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
                    // its leaves seen from both sides (from behind they were holes in the crown)
                    foreach (var r in tree.GetComponentsInChildren<Renderer>())
                        if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Cull")) r.sharedMaterial.SetFloat("_Cull", 0f);
                    var cs = Solidify(tree);
                    Physics.SyncTransforms();
                    if (FindBranch(h.knotL, cs, out var a) && FindBranch(h.knotR, cs, out var b)
                        && a.y < h.top + h.over * 1.8f && b.y < h.top + h.over * 1.8f && !OverAnother(h, cs))
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
        void AddMeasuredLedge(Transform t, Transform holder, float x, float half, float top, float width, bool crumbles = false)
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
            ledges.Add(new Ledge { x0 = x0, x1 = x1, top = top, crumbles = crumbles, t = t, home = t.localPosition });
        }

        // ---------- the gorge's rock ledges ----------

        // The rock kit (the user rejected two sheets of rocks as not climbable-looking and approved the third, built by
        // Meshy): a ledge jutting from a cliff, its top flat, a rounded lip at its front. Up the gorge it juts from the
        // back wall: its root GorgeLedgeInto into the wall, its lip at GorgeLedgeFront; as tall as drawn for that length.
        const float GorgeLedgeFront = -0.75f, GorgeLedgeInto = 0.6f, RockLedgeTall = 1.151f / 2f;
        static Material crumbleRock;

        /// <summary>
        /// A ledge of the gorge: the kit's rock ledge, its flat top the ledge, jutting from the back wall (no column under
        /// it). Which way the model's root lies isn't known here: of the two turns, the one with the deep rock at the
        /// back and the thin lip at the front (rays up from under it). A crumbling one is darker, its cracks showing.
        /// </summary>
        bool GorgeLedge(Transform t, float x, float width, float top, bool crumbles)
        {
            float back = GorgeBack + GorgeLedgeInto, deep = back - GorgeLedgeFront, tall = deep * RockLedgeTall, wide = width + 0.2f;
            float mid = (GorgeLedgeFront + back) / 2 - t.localPosition.z;
            // how far down the rock goes under a point (from below, up to its underside)
            float Under(List<Collider> cs, float z)
            {
                var from = root.TransformPoint(new Vector3(x, top - tall - 0.5f, z));
                float best = float.MaxValue;
                foreach (var c in cs)
                    if (c != null && c.Raycast(new Ray(from, Vector3.up), out var hit, tall + 1f) && hit.distance < best) best = hit.distance;
                return best;
            }
            foreach (float yaw in new[] { 90f, -90f })
            {
                var holder = CbArt.Joint(t, 0, top - tall, mid, "rockLedge");
                if (Model("RockLedge", holder, new Vector3(deep, tall, wide), yaw) == null) { Object.Destroy(holder.gameObject); return false; }
                var cs = Solidify(holder);
                Physics.SyncTransforms();
                // the root deeper than the lip: else the other turn
                if (yaw == 90f && Under(cs, back - 0.5f) > Under(cs, GorgeLedgeFront + 0.4f))
                {
                    rockSolids.RemoveAll(c => cs.Contains(c));
                    Object.DestroyImmediate(holder.gameObject);
                    continue;
                }
                if (crumbles)
                {
                    if (crumbleRock == null)
                    {
                        var r0 = holder.GetComponentInChildren<Renderer>();
                        crumbleRock = new Material(r0.sharedMaterial) { color = r0.sharedMaterial.color * new Color(0.82f, 0.72f, 0.62f) };
                    }
                    foreach (var r in holder.GetComponentsInChildren<Renderer>()) r.sharedMaterial = crumbleRock;
                    var crack = CbArt.Toon("#2b2622");
                    for (int k = 0; k < 3; k++)
                        CbArt.Box(crack, t, new Vector3((k - 1) * width * 0.3f, top - 0.25f, GorgeLedgeFront - 0.02f - t.localPosition.z), new Vector3(0.05f, 0.4f, 0.02f), CbArt.Rot(0, 0, k % 2 == 0 ? 0.4f : -0.4f));
                }
                AddMeasuredLedge(t, holder, x, wide / 2 + 0.3f, top, width, crumbles);
                return true;
            }
            return false;
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
