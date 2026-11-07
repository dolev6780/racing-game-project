using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The modelled Raja (generated in Meshy, cleaned up in Blender): a skinned mesh with legacy animation clips.
    /// It rides on the hero's invisible procedural rig, which still places, turns and flips him. Every frame the hero
    /// names a clip and a moment in it, so the animation always keeps the game's own move timing.
    /// </summary>
    public class CbHeroModel
    {
        public const string ResourcePath = "Cloudborne/Raja/Raja";
        /// <summary>Feet to the tip of the hair, in world units.</summary>
        public const float Height = 1.75f;

        public readonly Transform root, rightHand, chest;
        public readonly Material material;
        /// <summary>Turns the staff (long axis +Y) so it points forward out of the right fist.</summary>
        public readonly Quaternion grip;
        readonly Animation anim;
        readonly Dictionary<string, AnimationState> clips = new Dictionary<string, AnimationState>();
        AnimationState current;

        public int ClipCount => clips.Count;

        /// <summary>Null when the model or its legacy clips are missing; the hero then keeps the procedural look.</summary>
        public static CbHeroModel Load(Transform parent)
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab);
            string problem = go.GetComponent<Animation>() == null ? "it has no legacy animation (check its import settings)" : null;
            var bones = new Dictionary<string, Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            foreach (var bone in Bones)
                if (problem == null && !bones.ContainsKey(bone)) problem = "its skeleton has no bone named " + bone;
            if (problem != null)
            {
                Debug.LogWarning("Cloudborne: using the procedural Raja, as the model can't be used: " + problem + ".");
                Object.Destroy(go);
                return null;
            }
            return new CbHeroModel(go, parent, bones);
        }

        static readonly string[] Bones = { "Hips", "Spine", "Head", "head_end", "headfront", "RightHand", "RightToeBase" };

        CbHeroModel(GameObject go, Transform parent, Dictionary<string, Transform> bones)
        {
            go.name = "RajaModel";
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
            chest = bones["Spine"];

            // Fit the model while it is still in its bind pose: face +Z, stand on y = 0, make it Height tall.
            // The hair rises about 6% of the toe-to-crown span above the head bone's end; the toes sit 4% above the floor.
            var head = bones["Head"].position;
            var fwd = bones["headfront"].position - head;
            fwd.y = 0;
            root.localRotation = Quaternion.FromToRotation(parent.InverseTransformDirection(fwd.normalized), Vector3.forward);
            float toe = parent.InverseTransformPoint(bones["RightToeBase"].position).y;
            float crown = parent.InverseTransformPoint(bones["head_end"].position).y;
            float span = crown - toe;
            root.localScale = Vector3.one * (Height / (span * 1.063f));
            var hips = parent.InverseTransformPoint(bones["Hips"].position);
            toe = parent.InverseTransformPoint(bones["RightToeBase"].position).y;
            crown = parent.InverseTransformPoint(bones["head_end"].position).y;
            root.localPosition = new Vector3(-hips.x, -(toe - (crown - toe) * 0.039f), -hips.z);

            // grip: of the hand's own axes, the one that points most nearly forward becomes the staff's axis
            var forward = parent.TransformDirection(Vector3.forward);
            Vector3 best = Vector3.up;
            float bestDot = 0;
            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                float d = Vector3.Dot(rightHand.rotation * axis, forward);
                if (Mathf.Abs(d) > Mathf.Abs(bestDot)) { bestDot = d; best = axis * Mathf.Sign(d); }
            }
            grip = Quaternion.FromToRotation(Vector3.up, best);

            // anime cel look: one hard light/shadow step with a cool shadow tint, a warm rim, no plastic highlight
            material = CbArt.NewToon(Color.white, CbArt.CharacterOutline, Resources.Load<Texture2D>(ResourcePath + "_Albedo"));
            material.SetFloat("_Bands", 1);
            material.SetFloat("_BandSoftness", 0.02f);
            material.SetColor("_ShadowColor", new Color(0.62f, 0.55f, 0.76f));
            material.SetFloat("_SpecStrength", 0);
            material.SetColor("_RimColor", new Color(1f, 0.9f, 0.78f));
            material.SetFloat("_RimPower", 3.5f);
            material.SetFloat("_RimStrength", 0.45f);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
        }

        /// <summary>Shows a clip at normalised time t (0..1). Switching clips blends over a few frames.</summary>
        public void Show(string clip, float t, float blend = 0.08f)
        {
            if (!clips.TryGetValue(clip, out var s)) return;
            if (s != current)
            {
                anim.CrossFade(s.name, blend);
                current = s;
            }
            s.speed = 0;
            s.normalizedTime = Mathf.Clamp01(t);
        }

        /// <summary>Plays the part of a clip between from and to as u goes from 0 to 1.</summary>
        public void Window(string clip, float from, float to, float u) => Show(clip, Mathf.Lerp(from, to, Mathf.Clamp01(u)));

        /// <summary>Loops a clip: cycles counts whole plays of it.</summary>
        public void Loop(string clip, float cycles) => Show(clip, cycles - Mathf.Floor(cycles));

        /// <summary>Loops a clip at its own speed, given a running time in seconds.</summary>
        public void LoopSeconds(string clip, float seconds)
        {
            if (clips.TryGetValue(clip, out var s) && s.length > 0) Loop(clip, seconds / s.length);
        }
    }
}
