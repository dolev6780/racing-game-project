using System;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Raja's hands and feet put where they belong after his clip is drawn each frame (the user: wherever he stands,
    /// hangs or climbs he must touch it - not in the air): a two-bone solve (thigh, shin, foot; upper arm, forearm,
    /// hand) bending each limb in the plane the clip bends it in.
    /// - Feet: standing, each foot on the surface drawn under it (a stair's step, a slab, a slope) - a foot planted on
    ///   the floor goes onto what is under it; a foot lifted in a stride only kept out of it.
    /// - Hands: holding on (an edge, a pole, a bar, a vine), each palm on the grip, the clip's own spacing of the hands
    ///   across the path kept.
    /// Runs in LateUpdate, after the legacy Animation has set the bones.
    /// </summary>
    public class CbLimbIK : MonoBehaviour
    {
        public class Limb
        {
            public Transform upper, lower, end, tip;
            public float weight;
        }

        public Limb leftLeg, rightLeg, leftArm, rightArm;
        /// <summary>Standing: the feet onto what is drawn under them.</summary>
        public bool feet;
        /// <summary>The floor he stands on (world y) and how far the toe bones sit over the soles.</summary>
        public float floorY, toeLift;
        /// <summary>The drawn surface under a point (world): its height, if there is one near.</summary>
        public Func<Vector3, float?> surface;
        /// <summary>Holding on: the grip (world) the palms go to; across: along x too (a vine), not only up and down.</summary>
        public bool hands, handsAcross;
        public Vector3 grip;

        // how far a foot may be moved up or down, how near the floor a toe counts as planted, how fast the limbs ease in
        const float FootReach = 0.55f, Planted = 0.1f, Ease = 14f, PalmOut = 0.05f;
        /// <summary>
        /// The deepest step a standing foot goes down to (one foot on a step, the other on the one below). Over a deeper
        /// drop (the stone stairs' steps are 0.58) the foot stays level with the step he stands on, its toes over the
        /// edge: it hung in the air halfway down (the touch test).
        /// </summary>
        public const float StepDown = 0.42f;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Foot(leftLeg, dt);
            Foot(rightLeg, dt);
            Hand(leftArm, dt);
            Hand(rightArm, dt);
        }

        void Foot(Limb l, float dt)
        {
            if (l == null || l.tip == null) return;
            l.weight = Mathf.MoveTowards(l.weight, feet ? 1 : 0, Ease * dt);
            if (l.weight <= 0 || surface == null) return;
            var toe = l.tip.position;
            float? s = surface(toe);
            if (s.HasValue && s.Value < floorY - StepDown) s = null;
            float ground = s ?? floorY;
            float want = ground + toeLift;
            bool planted = toe.y - floorY < toeLift + Planted;
            // planted: onto what is under it; lifted in a stride: only out of it
            float dy = planted ? want - toe.y : Mathf.Max(0, want - toe.y);
            dy = Mathf.Clamp(dy, -FootReach, FootReach) * l.weight;
            if (Mathf.Abs(dy) < 0.002f) return;
            var keep = l.end.rotation;
            Solve(l, l.end.position + Vector3.up * dy);
            l.end.rotation = keep;
        }

        void Hand(Limb l, float dt)
        {
            if (l == null) return;
            l.weight = Mathf.MoveTowards(l.weight, hands ? 1 : 0, Ease * dt);
            if (l.weight <= 0) return;
            // the palm a little out past the wrist, along the hand
            var palmOff = (l.end.position - l.lower.position).normalized * PalmOut;
            var palm = l.end.position + palmOff;
            var at = new Vector3(handsAcross ? grip.x : palm.x, grip.y, palm.z);
            var to = Vector3.Lerp(palm, at, l.weight) - palmOff;
            Solve(l, to);
        }

        /// <summary>Two-bone solve: upper and lower turned so the end reaches target (as near as the limb reaches).</summary>
        static void Solve(Limb l, Vector3 target)
        {
            Vector3 a = l.upper.position, b = l.lower.position, c = l.end.position;
            float lab = Vector3.Distance(a, b), lcb = Vector3.Distance(b, c);
            float lat = Mathf.Clamp(Vector3.Distance(a, target), 0.01f, lab + lcb - 0.001f);
            // the bend's plane: the clip's own, or (a straight limb) across the path
            var axis = Vector3.Cross(c - a, b - a);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.forward;
            axis.Normalize();
            float abNow = Angle(c - a, b - a), bcNow = Angle(a - b, c - b);
            float abWant = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2 * lab * lat), -1, 1));
            float bcWant = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2 * lab * lcb), -1, 1));
            l.upper.rotation = Quaternion.AngleAxis((abWant - abNow) * Mathf.Rad2Deg, axis) * l.upper.rotation;
            l.lower.rotation = Quaternion.AngleAxis((bcWant - bcNow) * Mathf.Rad2Deg, axis) * l.lower.rotation;
            // then the whole limb turned to point at the target
            var reach = l.end.position - l.upper.position;
            l.upper.rotation = Quaternion.FromToRotation(reach, target - l.upper.position) * l.upper.rotation;
        }

        static float Angle(Vector3 u, Vector3 v) => Mathf.Acos(Mathf.Clamp(Vector3.Dot(u.normalized, v.normalized), -1, 1));
    }
}
