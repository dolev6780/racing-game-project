using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Track
{
    /// <summary>
    /// Closed-loop centerline of the track, evenly sampled. Used for lap counting,
    /// race positions, AI steering and respawning.
    /// </summary>
    public class TrackPath : MonoBehaviour
    {
        public float RoadWidth { get; private set; }
        public float Length { get; private set; }
        public int Count => points.Count;

        readonly List<Vector3> points = new List<Vector3>();
        float[] cumulative;

        public void Initialize(List<Vector3> sampledPoints, float roadWidth)
        {
            points.Clear();
            points.AddRange(sampledPoints);
            RoadWidth = roadWidth;

            cumulative = new float[points.Count];
            float total = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                cumulative[i] = total;
                total += Vector3.Distance(points[i], points[(i + 1) % points.Count]);
            }
            Length = total;
        }

        public int Wrap(int i)
        {
            int n = points.Count;
            return ((i % n) + n) % n;
        }

        public Vector3 Point(int i) => points[Wrap(i)];

        public Vector3 Direction(int i) => (Point(i + 1) - Point(i)).normalized;

        public Vector3 Right(int i) => Vector3.Cross(Vector3.up, Direction(i)).normalized;

        public float DistanceAtIndex(int i) => cumulative[Wrap(i)];

        /// <summary>Closest sample index, searching the whole track.</summary>
        public int FindClosest(Vector3 pos)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float d = (points[i] - pos).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = i; }
            }
            return best;
        }

        /// <summary>Closest sample index near a previous index (prevents shortcut jumps).</summary>
        public int FindClosestLocal(Vector3 pos, int hint, int window = 15)
        {
            int best = Wrap(hint);
            float bestSqr = float.MaxValue;
            for (int o = -window; o <= window; o++)
            {
                int i = Wrap(hint + o);
                float d = (points[i] - pos).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = i; }
            }
            return best;
        }

        /// <summary>Distance along the track from the start line, refined by projecting onto the segment.</summary>
        public float DistanceAlong(Vector3 pos, int index)
        {
            Vector3 a = Point(index);
            Vector3 b = Point(index + 1);
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return cumulative[Wrap(index)] + t * ab.magnitude;
        }

        /// <summary>World point and forward direction at a distance along the track (wraps).</summary>
        public Vector3 PointAtDistance(float distance, out Vector3 direction)
        {
            distance = Mathf.Repeat(distance, Length);
            int i = 0;
            while (i < points.Count - 1 && cumulative[i + 1] <= distance) i++;
            Vector3 a = Point(i);
            Vector3 b = Point(i + 1);
            float segLen = Vector3.Distance(a, b);
            float t = segLen > 0.0001f ? (distance - cumulative[i]) / segLen : 0f;
            direction = (b - a).normalized;
            return Vector3.Lerp(a, b, t);
        }

        void OnDrawGizmos()
        {
            if (points.Count < 2) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < points.Count; i++)
                Gizmos.DrawLine(points[i] + Vector3.up, Point(i + 1) + Vector3.up);
        }
    }
}
