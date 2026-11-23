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
                if (mf.sharedMesh == null) continue;
                var mc = mf.gameObject.GetComponent<MeshCollider>();
                if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                list.Add(mc);
            }
            Physics.SyncTransforms();
            return list;
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
        public bool SurfaceUnder(float x, float y, out float sy)
        {
            sy = y;
            bool found = false;
            float best = float.MaxValue;
            // along his line and a little either side of it (a thin ledge seen from the front is under him wherever it
            // lies across the path): the drawn top nearest his floor
            foreach (float z in ProbeZ)
            {
                var from = root.TransformPoint(new Vector3(x, y + SurfaceReach, z));
                if (!Physics.Raycast(from, -root.up, out var h, 2 * SurfaceReach * root.lossyScale.y) || Vector3.Dot(h.normal, root.up) < 0.6f) continue;
                float hy = root.InverseTransformPoint(h.point).y;
                if (Mathf.Abs(hy - y) < best) { best = Mathf.Abs(hy - y); sy = hy; found = true; }
            }
            return found;
        }
        public const float SurfaceReach = 0.35f;

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
            foreach (var b in blocks) if (!b.broken && !b.noTop) list.Add((b.x0, b.x1, b.top));
            return list;
        }
    }
}
