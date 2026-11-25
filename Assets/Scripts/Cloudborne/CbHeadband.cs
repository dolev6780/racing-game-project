using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Raja's headband tail stirs a little (the user: standing still, it should move just slightly). Meshy's rig has no
    /// bone for it, so one is added: the tail is the part of the head that reaches out sideways past the head's other
    /// side (its knot to its tip), its vertices handed to a new bone at the knot, more of them the further out they lie.
    /// That bone sways in a light breeze and trails a little behind him as he moves. The mesh must be readable
    /// (CharacterImport). Nothing happens if the tail can't be found.
    /// </summary>
    public class CbHeadband : MonoBehaviour
    {
        // how far it sways in the breeze, and how far it trails at a run (degrees)
        const float Breeze = 6f, Trail = 14f, TrailSpeed = 5f;

        Transform head;
        Vector3 tailLocal, lastPos;
        float trail;

        /// <summary>Adds the tail's bone to Raja's skin (in its bind pose); the component that sways it, or null.</summary>
        public static CbHeadband Add(SkinnedMeshRenderer skin, Transform head, Transform headEnd, Transform headFront)
        {
            if (skin == null || head == null || headEnd == null || headFront == null) return null;
            var source = skin.sharedMesh;
            if (source == null || !source.isReadable) return null;
            var bones = skin.bones;
            int headIdx = System.Array.IndexOf(bones, head);
            var bind = source.bindposes;
            if (headIdx < 0 || bind.Length != bones.Length) return null;

            // the head's frame in the mesh's space (the skeleton stands in its bind pose)
            Vector3 M(Transform t) => skin.transform.InverseTransformPoint(t.position);
            var h = bind[headIdx].inverse.MultiplyPoint3x4(Vector3.zero);
            var up = (M(headEnd) - M(head)).normalized;
            var fwd = Vector3.ProjectOnPlane(M(headFront) - M(head), up).normalized;
            var side = Vector3.Cross(up, fwd).normalized;
            if (up == Vector3.zero || fwd == Vector3.zero) return null;

            var verts = source.vertices;
            var weights = source.boneWeights;
            if (weights.Length != verts.Length) return null;
            // the head's own vertices: how far they reach to either side
            var onHead = new List<int>();
            float pos = 0, neg = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                var w = weights[i];
                if (w.boneIndex0 != headIdx || w.weight0 < 0.5f) continue;
                onHead.Add(i);
                float s = Vector3.Dot(verts[i] - h, side);
                pos = Mathf.Max(pos, s); neg = Mathf.Max(neg, -s);
            }
            // the tail reaches out on one side well past the head's other side
            float sign = pos > neg ? 1f : -1f, reach = Mathf.Max(pos, neg), other = Mathf.Min(pos, neg);
            if (onHead.Count == 0 || reach < other * 1.25f) return null;
            float from = other * 1.05f;
            var tail = new List<(int i, float k)>();
            Vector3 knot = Vector3.zero, tip = Vector3.zero;
            int nKnot = 0, nTip = 0;
            foreach (int i in onHead)
            {
                float s = Vector3.Dot(verts[i] - h, side) * sign;
                if (s <= from) continue;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, reach, s));
                tail.Add((i, k));
                if (k < 0.15f) { knot += verts[i]; nKnot++; }
                if (k > 0.85f) { tip += verts[i]; nTip++; }
            }
            if (tail.Count < 8 || nKnot == 0 || nTip == 0) return null;
            knot /= nKnot; tip /= nTip;

            // the new bone at the knot, in the head's own space
            var knotOnHead = bind[headIdx].MultiplyPoint3x4(knot);
            var bone = new GameObject("headbandTail").transform;
            bone.SetParent(head, false);
            bone.localPosition = knotOnHead;
            bone.localRotation = Quaternion.identity;
            bone.localScale = Vector3.one;

            var mesh = Object.Instantiate(source);
            mesh.name = source.name + "_Headband";
            int idx = bones.Length;
            var newBind = new Matrix4x4[idx + 1];
            System.Array.Copy(bind, newBind, idx);
            newBind[idx] = Matrix4x4.Translate(-knotOnHead) * bind[headIdx];
            foreach (var (i, k) in tail)
                weights[i] = new BoneWeight { boneIndex0 = idx, weight0 = k, boneIndex1 = headIdx, weight1 = 1f - k };
            mesh.boneWeights = weights;
            mesh.bindposes = newBind;
            var newBones = new Transform[idx + 1];
            System.Array.Copy(bones, newBones, idx);
            newBones[idx] = bone;
            skin.sharedMesh = mesh;
            skin.bones = newBones;

            var c = bone.gameObject.AddComponent<CbHeadband>();
            c.head = head;
            c.tailLocal = (bind[headIdx].MultiplyPoint3x4(tip) - knotOnHead).normalized;
            c.lastPos = head.position;
            return c;
        }

        void LateUpdate()
        {
            if (head == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            // he moves: the tail trails behind (how fast the head goes across the view, eased)
            var v = (head.position - lastPos) / dt;
            lastPos = head.position;
            float want = Mathf.Clamp(v.magnitude / 6f, 0f, 1f);
            trail += (want - trail) * (1f - Mathf.Exp(-TrailSpeed * dt));
            // in the head's space: up (the world's), the tail's way, and across it
            var upL = head.InverseTransformDirection(Vector3.up).normalized;
            var across = Vector3.Cross(tailLocal, upL);
            if (across.sqrMagnitude < 1e-6f) return;
            across.Normalize();
            float t = Time.time;
            float swing = Breeze * (0.7f * Mathf.Sin(t * 2.1f) + 0.3f * Mathf.Sin(t * 3.7f + 1f)) * (1f + trail);
            float lift = Breeze * 0.6f * Mathf.Sin(t * 1.6f + 0.5f) + Trail * trail;
            transform.localRotation = Quaternion.AngleAxis(swing, upL) * Quaternion.AngleAxis(lift, across);
        }
    }
}
