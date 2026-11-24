using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// What he stands on and hangs from, where it is drawn (the user: he stood and hung in the air beside the models -
    /// on everything he steps on, hangs from or climbs he must touch it). The kit's models get colliders of their own
    /// meshes; rays against them find their real tops and edges, and the ledges are put there (the model moved, if
    /// need be, so its top is the height the stage is laid out for). His feet find the drawn surface under them too
    /// (SurfaceUnder, CbHero).
    /// </summary>
    public partial class CbLevel
    {
        // the lines across the path's depth along which a top is looked for (his feet are on the middle one)
        static readonly float[] ProbeZ = { -0.12f, 0f, 0.12f };
        const float ProbeStep = 0.05f;

        /// <summary>Colliders of a model's own meshes (kept: his feet find the drawn surface on them).</summary>
        static List<Collider> Solidify(Transform model)
        {
            var list = new List<Collider>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                // (not its lighter copy: the same surface twice)
                if (mf.sharedMesh == null || IsLighterCopy(mf.transform)) continue;
                var mc = mf.gameObject.GetComponent<MeshCollider>();
                if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                list.Add(mc);
            }
            Physics.SyncTransforms();
            return list;
        }

        static bool IsLighterCopy(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name == "lod1") return true;
            return false;
        }

        /// <summary>The highest surface facing up among cs at (x, z) of the level, between y0 and y1.</summary>
        bool TopOf(List<Collider> cs, float x, float z, float y0, float y1, out float y)
        {
            y = float.NegativeInfinity;
            var from = root.TransformPoint(new Vector3(x, y1, z));
            var to = root.TransformPoint(new Vector3(x, y0, z));
            foreach (var h in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to)))
            {
                if (!cs.Contains(h.collider) || Vector3.Dot(h.normal, root.up) < 0.5f) continue;
                y = Mathf.Max(y, root.InverseTransformPoint(h.point).y);
            }
            return y > float.NegativeInfinity;
        }

        /// <summary>The lowest surface facing down among cs at (x, z), between y0 and y1 (an underside, from below).</summary>
        bool UnderOf(List<Collider> cs, float x, float z, float y0, float y1, out float y)
        {
            y = float.PositiveInfinity;
            var from = root.TransformPoint(new Vector3(x, y0, z));
            var to = root.TransformPoint(new Vector3(x, y1, z));
            foreach (var h in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to)))
            {
                if (!cs.Contains(h.collider) || Vector3.Dot(h.normal, root.up) > -0.5f) continue;
                y = Mathf.Min(y, root.InverseTransformPoint(h.point).y);
            }
            return y < float.PositiveInfinity;
        }

        /// <summary>
        /// The flat top a model shows near height `near` (within `band`) across cx - half .. cx + half along his line:
        /// its height (the commonest of the tops found) and the longest run of x where the top lies at it. False if
        /// there is no top there at least MinTopRun long.
        /// </summary>
        bool FindTop(List<Collider> cs, float cx, float half, float near, float band, out float top, out float x0, out float x1)
        {
            top = near; x0 = x1 = cx;
            int n = Mathf.Max(2, Mathf.CeilToInt(2 * half / ProbeStep));
            var hs = new float[n + 1];
            var counts = new Dictionary<int, int>();
            for (int i = 0; i <= n; i++)
            {
                float x = cx - half + i * ProbeStep, h = float.NegativeInfinity;
                foreach (float z in ProbeZ)
                    if (TopOf(cs, x, z, near - band, near + band, out float hz)) h = Mathf.Max(h, hz);
                hs[i] = h;
                if (h > float.NegativeInfinity) { int k = Mathf.RoundToInt(h / 0.05f); counts[k] = counts.TryGetValue(k, out int c) ? c + 1 : 1; }
            }
            if (counts.Count == 0) return false;
            int best = 0, bestK = 0;
            foreach (var kv in counts) if (kv.Value > best) { best = kv.Value; bestK = kv.Key; }
            // the height: the mean of the tops near the commonest one
            float sum = 0; int m = 0;
            foreach (var h in hs) if (Mathf.Abs(h - bestK * 0.05f) < 0.1f) { sum += h; m++; }
            top = sum / m;
            // the longest run of x at that height
            int runFrom = -1, bestFrom = 0, bestLen = 0;
            for (int i = 0; i <= n + 1; i++)
            {
                bool on = i <= n && Mathf.Abs(hs[i] - top) < 0.12f;
                if (on && runFrom < 0) runFrom = i;
                if (!on && runFrom >= 0)
                {
                    if (i - runFrom > bestLen) { bestLen = i - runFrom; bestFrom = runFrom; }
                    runFrom = -1;
                }
            }
            if ((bestLen - 1) * ProbeStep < MinTopRun) return false;
            x0 = cx - half + bestFrom * ProbeStep;
            x1 = cx - half + (bestFrom + bestLen - 1) * ProbeStep;
            return true;
        }
        /// <summary>The shortest top he is given to stand on (narrower, the model's bump is not a ledge).</summary>
        const float MinTopRun = 0.45f;

        /// <summary>How far to move a model across the path's depth so the top near `near` along cx +- half lies on his
        /// line: the depth where most of it is found (0 if it already is, or nowhere).</summary>
        float BestDepth(List<Collider> cs, float cx, float half, float near, float band)
        {
            float bestZ = 0; int best = -1;
            for (float z = -1.2f; z <= 1.21f; z += 0.1f)
            {
                int c = 0;
                for (float x = cx - half; x <= cx + half; x += ProbeStep * 2)
                    if (TopOf(cs, x, z, near - band, near + band, out _)) c++;
                if (c > best || (c == best && Mathf.Abs(z) < Mathf.Abs(bestZ))) { best = c; bestZ = z; }
            }
            return best > 0 ? bestZ : 0f;
        }

        /// <summary>Moves a model (its joint) by d in the level, and its colliders with it.</summary>
        static void Shift(Transform joint, Vector3 d)
        {
            joint.localPosition += d;
            Physics.SyncTransforms();
        }

        /// <summary>
        /// The drawn surface under (x, about y) on his line - the top of a modelled thing his feet are over, from up to
        /// SurfaceReach over y to as far under it - if one is near; for his feet to stand on what is drawn.
        /// </summary>
        public bool SurfaceUnder(float x, float y, out float sy) => SurfaceUnder(x, y, 0f, out sy);

        /// <summary>The drawn surface under (x, about y) at depth z (where his foot is across the path): right under it
        /// first (a tower's top seen from the front: its rim in front lower than its middle - his foot over the rim stood
        /// in the air in front of it); if nothing is there, along his line and a little either side, the nearest top.</summary>
        public bool SurfaceUnder(float x, float y, float z, out float sy)
        {
            // as the camera sees it, from in front: under his foot, or across the path's depth near it (his feet stand
            // apart across the path; a ledge's top a little behind or in front of a foot, at its height, is what the
            // foot is seen standing on) - the top nearest his floor
            sy = y;
            bool found = false;
            float best = float.MaxValue;
            foreach (float pz in new[] { z, 0f, -0.12f, 0.12f, -0.3f, 0.3f })
                if (RayDown(x, y, pz, out float hy) && Mathf.Abs(hy - y) < best) { best = Mathf.Abs(hy - y); sy = hy; found = true; }
            return found;
        }

        bool RayDown(float x, float y, float z, out float hy)
        {
            hy = y;
            var from = root.TransformPoint(new Vector3(x, y + SurfaceReach, z));
            if (!Physics.Raycast(from, -root.up, out var h, 2 * SurfaceReach * root.lossyScale.y) || Vector3.Dot(h.normal, root.up) < 0.4f) return false;
            hy = root.InverseTransformPoint(h.point).y;
            return true;
        }
        public const float SurfaceReach = 0.55f;

        /// <summary>Test hook: the level's own root (its models under it).</summary>
        public Transform Root => root;

        /// <summary>Whether this stage's level is still built (not destroyed by a stage built after it).</summary>
        public bool Alive => root != null;

        /// <summary>Whether a wall goes down under the edge at (edgeX, top): a block's side (his feet against it as he
        /// hangs); under a ledge there is none (he hangs from it with his legs straight).</summary>
        public bool FaceUnder(float edgeX, float top)
        {
            foreach (var b in blocks)
                if (!b.broken && Mathf.Abs(b.top - top) < 0.02f && edgeX >= b.x0 - 0.01f && edgeX <= b.x1 + 0.01f && b.bottom < top - 1.2f) return true;
            return false;
        }

        // the rocks' own colliders (the cliffs, terraces, river rocks and stones), for finding their steps
        static readonly List<Collider> rockSolids = new List<Collider>();
        const float RockStepMin = 0.5f, RockStepRise = 0.06f, RockStepSpread = 0.25f;

        /// <summary>
        /// Every step drawn on a rock can be stood on (the user: a step or two on a rock that looks like one to step on,
        /// and he fell through it): along the whole stage the rocks' tops are looked for on his line (and a little either
        /// side), each with room over it to stand; a run of them at least RockStepMin long and near level is a step. A
        /// step that what he already stands on there does not cover becomes a ledge of its own (to stand on, and to
        /// catch the edge of).
        /// </summary>
        void AddRockSteps()
        {
            rockSolids.RemoveAll(c => c == null);
            if (rockSolids.Count == 0) return;
            Physics.SyncTransforms();
            float x0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var c in rockSolids)
            {
                var b = c.bounds;
                var lo = root.InverseTransformPoint(b.min);
                var hi = root.InverseTransformPoint(b.max);
                x0 = Mathf.Min(x0, Mathf.Min(lo.x, hi.x)); x1 = Mathf.Max(x1, Mathf.Max(lo.x, hi.x)); y1 = Mathf.Max(y1, Mathf.Max(lo.y, hi.y));
            }
            var solid = new HashSet<Collider>(rockSolids);
            // open runs of tops, step by step along x: (from, last height, sum, count)
            var open = new List<(float from, float last, float sum, int n, float lowest, float highest)>();
            var steps = new List<(float from, float to, float top)>();
            for (float x = x0; x <= x1 + ProbeStep; x += ProbeStep * 2)
            {
                var heights = new List<float>();
                if (x <= x1)
                    foreach (float z in ProbeZ)
                    {
                        var from = root.TransformPoint(new Vector3(x, y1 + 1f, z));
                        foreach (var h in Physics.RaycastAll(from, -root.up, (y1 + 4f) * root.lossyScale.y))
                        {
                            if (!solid.Contains(h.collider) || Vector3.Dot(h.normal, root.up) < 0.6f) continue;
                            float hy = root.InverseTransformPoint(h.point).y;
                            if (hy < WaterLevel + 0.2f) continue;
                            // room over it to stand
                            if (Physics.Raycast(h.point + root.up * 0.05f, root.up, StandHeight * root.lossyScale.y)) continue;
                            if (!heights.Exists(k => Mathf.Abs(k - hy) < 0.05f)) heights.Add(hy);
                        }
                    }
                var next = new List<(float from, float last, float sum, int n, float lowest, float highest)>();
                foreach (float h in heights)
                {
                    int i = open.FindIndex(r => Mathf.Abs(r.last - h) < RockStepRise && Mathf.Max(r.highest, h) - Mathf.Min(r.lowest, h) < RockStepSpread);
                    if (i >= 0) { var r = open[i]; open.RemoveAt(i); next.Add((r.from, h, r.sum + h, r.n + 1, Mathf.Min(r.lowest, h), Mathf.Max(r.highest, h))); }
                    else next.Add((x, h, h, 1, h, h));
                }
                // the runs that did not go on are done
                foreach (var r in open)
                {
                    float to = x - ProbeStep * 2;
                    if (to - r.from >= RockStepMin) steps.Add((r.from, to, r.sum / r.n));
                }
                open = next;
            }
            foreach (var (a, b, top) in steps)
            {
                float mid = (a + b) / 2;
                // what he already stands on there covers it
                if (Mathf.Abs(SupportAt(mid, top + 0.25f) - top) < 0.15f) continue;
                var t = CbArt.Joint(root, mid, 0, 0, "rockStep");
                ledges.Add(new Ledge { x0 = a, x1 = b, top = top, t = t, home = t.localPosition });
                RockSteps++;
            }
        }

        /// <summary>
        /// The front edge of a ledge his hands reach at (x, hy), with room either side of him along it (not its ends -
        /// those are caught from the side, FindEdge): its top and its span. For hanging from its lip, facing it.
        /// </summary>
        public bool LipNear(float x, float hy, out float top, out float x0, out float x1)
        {
            top = x0 = x1 = 0;
            foreach (var l in ledges)
            {
                if (l.gone || x < l.x0 + LipInset || x > l.x1 - LipInset || hy < l.top - 0.2f || hy > l.top + 0.25f) continue;
                top = l.top; x0 = l.x0; x1 = l.x1;
                return true;
            }
            return false;
        }
        public const float LipInset = 0.25f;

        /// <summary>How many steps drawn on the rocks were found that nothing else let him stand on.</summary>
        public int RockSteps { get; private set; }

        /// <summary>Test hook: every steady ledge's top (x0, x1, top), as he stands on them (not the crumbling ones:
        /// they give way under him).</summary>
        public List<(float x0, float x1, float top)> LedgeTops()
        {
            var list = new List<(float, float, float)>();
            foreach (var l in ledges) if (!l.gone && !l.crumbles) list.Add((l.x0, l.x1, l.top));
            return list;
        }

        /// <summary>Test hook: every block's top he can stand on (x0, x1, top).</summary>
        public List<(float x0, float x1, float top)> BlockTops()
        {
            var list = new List<(float, float, float)>();
            foreach (var b in blocks) if (!b.broken && !b.noTop && !b.smooth) list.Add((b.x0, b.x1, b.top));
            return list;
        }

        /// <summary>Test hook: whether he is meant to stand at x on the highest thing there - not on a gate's top or a
        /// thorn bush (blocks he is kept off or thrown off).</summary>
        public bool StandableAt(float x)
        {
            foreach (var b in blocks)
                if (!b.broken && (b.noTop || b.smooth) && x > b.x0 - HalfWidth && x < b.x1 + HalfWidth) return false;
            return true;
        }
    }
}
