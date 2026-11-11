using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// A modelled character (generated in Meshy, cleaned up in Blender): a skinned mesh with legacy animation clips.
    /// It rides on its owner's invisible procedural rig, which still places, turns and flips it. Every frame the owner
    /// names a clip and a moment in it, so the animation always keeps the game's own move timing.
    /// </summary>
    public class CbModel
    {
        public readonly Transform root, rightHand, leftHand, leftForeArm, chest;
        public readonly SkinnedMeshRenderer skin;
        readonly Transform head, headFront;
        public readonly Material material;
        readonly Animation anim;
        readonly Dictionary<string, AnimationState> clips = new Dictionary<string, AnimationState>();
        AnimationState current;
        // Each clip's own turn of the face away from straight ahead (degrees), undone so the character shows its profile.
        // Measured once per model file: every hyena of a kind shares it.
        static readonly Dictionary<string, Dictionary<string, float>> TurnsByModel = new Dictionary<string, Dictionary<string, float>>();
        readonly Dictionary<string, float> clipTurns;
        Quaternion baseRotation;
        // the right hand's own axis that points most nearly forward in the bind pose: what a held weapon lines up with
        readonly Vector3 handForward;
        float yaw, yawTarget;

        public int ClipCount => clips.Count;
        public bool HasClip(string clip) => clips.ContainsKey(clip);

        /// <summary>Where the face points, flattened to the ground (world space).</summary>
        public Vector3 Facing
        {
            get
            {
                var d = headFront.position - head.position;
                d.y = 0;
                return d.normalized;
            }
        }

        /// <summary>
        /// The tip of the snout (world space): the head's front bone sits a tenth of its distance from the head bone
        /// behind the nose (measured on the hyenas).
        /// </summary>
        public Vector3 Snout => headFront.position + (headFront.position - head.position) * 0.1f;

        /// <summary>The way the face points (world space), up or down with the head.</summary>
        public Vector3 FaceDirection => (headFront.position - head.position).normalized;

        static readonly string[] Bones ={ "Hips", "Spine", "Head", "head_end", "headfront", "RightHand", "LeftHand", "LeftForeArm", "RightToeBase" };

        /// <summary>
        /// Loads Resources/path (an FBX with legacy clips, and path_Albedo for its colours) and fits it under parent,
        /// facing +Z, feet on y = 0, height units from feet to crown. Null when it can't be used; the owner then keeps
        /// its procedural look.
        /// </summary>
        public static CbModel Load(string path, float height, Transform parent)
        {
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab);
            string problem = go.GetComponent<Animation>() == null ? "it has no legacy animation (check its import settings)" : null;
            var bones = new Dictionary<string, Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            foreach (var bone in Bones)
                if (problem == null && !bones.ContainsKey(bone)) problem = "its skeleton has no bone named " + bone;
            if (problem != null)
            {
                Debug.LogWarning($"Cloudborne: {path} can't be used ({problem}); showing the procedural character.");
                Object.Destroy(go);
                return null;
            }
            return new CbModel(go, path, height, parent, bones);
        }

        CbModel(GameObject go, string path, float height, Transform parent, Dictionary<string, Transform> bones)
        {
            go.name = path.Substring(path.LastIndexOf('/') + 1) + "Model";
            root = go.transform;
            root.SetParent(parent, false);

            anim = go.GetComponent<Animation>();
            anim.playAutomatically = false;
            anim.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (AnimationState s in anim)
            {
                // Blender names each take "Armature|Clip"
                clips[s.name.Substring(s.name.LastIndexOf('|') + 1)] = s;
                s.speed = 0;
                s.wrapMode = WrapMode.ClampForever;
            }

            rightHand = bones["RightHand"];
            leftHand = bones["LeftHand"];
            leftForeArm = bones["LeftForeArm"];
            chest = bones["Spine"];
            head = bones["Head"];
            headFront = bones["headfront"];

            // Fit the model in its bind pose. The imported prefab may stand in a frame of one of its clips (in Raja's
            // "Idle" his head is turned 90 degrees), so reset it first. Meshy's characters all share one skeleton:
            // the hair or ears rise about 6% of the toe-to-crown span above the head bone's end, the toes sit 4% above the floor.
            skin = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin != null) ToBindPose(skin);
            var fwd = headFront.position - head.position;
            fwd.y = 0;
            root.localRotation = Quaternion.FromToRotation(parent.InverseTransformDirection(fwd.normalized), Vector3.forward) * root.localRotation;
            float toe = parent.InverseTransformPoint(bones["RightToeBase"].position).y;
            float crown = parent.InverseTransformPoint(bones["head_end"].position).y;
            float span = crown - toe;
            root.localScale = Vector3.one * (height / (span * 1.063f));
            var hips = parent.InverseTransformPoint(bones["Hips"].position);
            toe = parent.InverseTransformPoint(bones["RightToeBase"].position).y;
            crown = parent.InverseTransformPoint(bones["head_end"].position).y;
            root.localPosition = new Vector3(-hips.x, -(toe - (crown - toe) * 0.039f), -hips.z);

            var forward = parent.TransformDirection(Vector3.forward);
            float bestDot = 0;
            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                float d = Vector3.Dot(rightHand.rotation * axis, forward);
                if (Mathf.Abs(d) > Mathf.Abs(bestDot)) { bestDot = d; handForward = axis * Mathf.Sign(d); }
            }

            baseRotation = root.localRotation;
            if (!TurnsByModel.TryGetValue(path, out clipTurns))
                TurnsByModel[path] = clipTurns = MeasureClipTurns(parent);

            material = CbArt.AnimeToon(Resources.Load<Texture2D>(path + "_Albedo"));
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }
        }

        /// <summary>World rotation for something held in the right fist whose own long axis is itemAxis: it points forward out of the fist.</summary>
        public Quaternion Grip(Vector3 itemAxis) => rightHand.rotation * Quaternion.FromToRotation(itemAxis, handForward);

        /// <summary>The palm of a hand, a little past the wrist (world space).</summary>
        public Vector3 Palm(Transform hand) => hand.position + (hand.position - hand.parent.position).normalized * 0.05f;

        /// <summary>
        /// Samples each clip and averages where the face points. A fighting stance stands half turned and some clips
        /// turn further; the game wants profiles, so Show turns the model back by this much.
        /// Clips that spin all the way round average out and get no correction.
        /// </summary>
        Dictionary<string, float> MeasureClipTurns(Transform parent)
        {
            var turns = new Dictionary<string, float>();
            foreach (var pair in clips)
            {
                var s = pair.Value;
                foreach (var other in clips.Values) { other.enabled = false; other.weight = 0; }
                s.enabled = true;
                s.weight = 1;
                Vector2 sum = Vector2.zero;
                const int samples = 8;
                for (int i = 0; i < samples; i++)
                {
                    s.normalizedTime = (i + 0.5f) / samples;
                    anim.Sample();
                    var f = parent.InverseTransformDirection(headFront.position - head.position);
                    var flat = new Vector2(f.x, f.z);
                    if (flat.sqrMagnitude > 1e-8f) sum += flat.normalized;
                }
                float steady = sum.magnitude / samples;
                turns[pair.Key] = steady > 0.6f ? Mathf.Clamp(Mathf.Atan2(sum.x, sum.y) * Mathf.Rad2Deg, -75f, 75f) : 0f;
                s.enabled = false;
                s.weight = 0;
            }
            return turns;
        }

        /// <summary>Moves every skinned bone to where it was when the mesh was bound to the skeleton, parents first.</summary>
        static void ToBindPose(SkinnedMeshRenderer skin)
        {
            var bones = skin.bones;
            var bind = skin.sharedMesh != null ? skin.sharedMesh.bindposes : null;
            if (bind == null || bind.Length != bones.Length) return;
            var order = new List<int>();
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) order.Add(i);
            order.Sort((a, b) => Depth(bones[a]).CompareTo(Depth(bones[b])));
            foreach (int i in order)
            {
                var m = skin.transform.localToWorldMatrix * bind[i].inverse;
                bones[i].SetPositionAndRotation(m.GetColumn(3), m.rotation);
            }
        }

        static int Depth(Transform t)
        {
            int d = 0;
            for (; t.parent != null; t = t.parent) d++;
            return d;
        }

        /// <summary>Shows a clip at normalised time t (0..1). Switching clips blends over a few frames.</summary>
        public void Show(string clip, float t, float blend = 0.08f)
        {
            if (!clips.TryGetValue(clip, out var s)) return;
            if (s != current)
            {
                anim.CrossFade(s.name, blend);
                current = s;
                clipTurns.TryGetValue(clip, out yawTarget);
            }
            s.speed = 0;
            s.normalizedTime = Mathf.Clamp01(t);
            yaw = Mathf.LerpAngle(yaw, yawTarget, 1 - Mathf.Exp(-14f * Time.deltaTime));
            root.localRotation = Quaternion.AngleAxis(-yaw, Vector3.up) * baseRotation;
        }

        /// <summary>Plays the part of a clip between from and to as u goes from 0 to 1.</summary>
        public void Window(string clip, float from, float to, float u, float blend = 0.08f) => Show(clip, Mathf.Lerp(from, to, Mathf.Clamp01(u)), blend);

        /// <summary>Loops a clip: cycles counts whole plays of it.</summary>
        public void Loop(string clip, float cycles) => Show(clip, cycles - Mathf.Floor(cycles));

        /// <summary>Loops a clip at its own speed, given a running time in seconds.</summary>
        public void LoopSeconds(string clip, float seconds)
        {
            if (clips.TryGetValue(clip, out var s) && s.length > 0) Loop(clip, seconds / s.length);
        }
    }
}
