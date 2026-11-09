using System;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// A character in a scene (Koda, Sergeant Snap, the raiders): stands, walks and plays clips when the script says so.
    /// It shows the first model of its list that is in the project (each made in Meshy from a drawing the user approved);
    /// until then a procedural stand-in takes its place.
    /// </summary>
    public class CbActor
    {
        public readonly string name;
        public readonly Rig f;
        /// <summary>The modelled character; null while only the stand-in exists.</summary>
        public readonly CbModel model;
        public float x, y;
        public int facing = 1;
        /// <summary>Curled up and wriggling, as when caught in a net (the stand-in; the model holds its clip).</summary>
        public bool struggle;

        readonly Transform prop;
        readonly Vector3 propAxis;
        string clip;
        float clipT, from, to, dur, walkT;
        bool once;
        float goal = float.NaN, speed;
        string walkClip;

        public bool Walking => !float.IsNaN(goal);

        public CbActor(Transform parent, string name, string[] models, float height, RigStyle style, Action<Rig> dress,
            float x, int facing, string propPath = null, Vector3? propAxis = null)
        {
            this.name = name;
            this.x = x;
            this.facing = facing;
            f = RigBuilder.Build(style, name);
            dress?.Invoke(f);
            f.root.SetParent(parent, false);
            foreach (var path in models)
                if (model == null && Resources.Load<GameObject>(path) != null) model = CbModel.Load(path, height, f.body);
            if (model != null)
            {
                foreach (var r in f.body.GetComponentsInChildren<Renderer>(true))
                    if (!r.transform.IsChildOf(model.root)) r.enabled = false;
                f.mats.Add(model.material);
            }
            if (propPath != null)
            {
                prop = CbArt.Joint(model != null ? f.body : f.R.hand, 0, -0.02f, 0.02f, name + "Prop");
                CbArt.Prop(propPath, prop);
                this.propAxis = propAxis ?? Vector3.forward;
            }
            f.body.localRotation = Quaternion.Euler(0, facing * 100f, 0);
            Update(0, 0);
        }

        /// <summary>Loops a clip at its own pace.</summary>
        public void Play(string clipName)
        {
            if (clip == clipName && !once) return;
            clip = clipName; once = false; clipT = 0;
        }

        /// <summary>Plays the part of a clip from a to b (normalised) once, over seconds.</summary>
        public void Once(string clipName, float a, float b, float seconds)
        {
            clip = clipName; once = true; from = a; to = b; dur = Mathf.Max(0.01f, seconds); clipT = 0;
        }

        /// <summary>Walks (or runs, with a high speed) to x, facing the way it goes, then stands.</summary>
        public void WalkTo(float target, float metresPerSecond, string clipName = "Walk_Fight_Forward")
        {
            goal = target; speed = metresPerSecond; walkClip = clipName;
        }

        public void Update(float dt, float clock)
        {
            if (Walking)
            {
                float d = goal - x, step = speed * dt;
                facing = d >= 0 ? 1 : -1;
                if (Mathf.Abs(d) <= step) { x = goal; goal = float.NaN; }
                else x += Mathf.Sign(d) * step;
                walkT += dt * speed / 2.7f;
            }
            clipT += dt;
            f.root.localPosition = new Vector3(x, y, 0);
            float target = facing * (90f + (model != null ? 8f : 25f));
            f.body.localRotation = Quaternion.Euler(0, Mathf.LerpAngle(f.body.localEulerAngles.y, target, dt <= 0 ? 1 : 1 - Mathf.Exp(-14f * dt)), 0);

            var pose = Pose.Base;
            if (Walking) Pose.Run(ref pose, walkT * Mathf.PI * 2);
            else pose.bob = Mathf.Sin(clock * 3 + x) * 0.02f - 0.04f;
            if (struggle)
            {
                // curled up inside the net, paws at his head, wriggling
                float w = Mathf.Sin(clock * 14);
                pose.sLx = -1.9f + w * 0.2f; pose.sRx = -1.9f - w * 0.2f; pose.eL = pose.eR = -2.2f;
                pose.hLx = -0.6f + Mathf.Sin(clock * 12) * 0.25f; pose.hRx = -0.6f - Mathf.Sin(clock * 12) * 0.25f; pose.kL = pose.kR = 1.1f;
                pose.lean = 0.35f; pose.bob = -0.15f;
            }
            f.Apply(pose, Mathf.Max(dt, 0.001f), 12);
            for (int i = 0; i < f.tail.Count; i++)
                f.tail[i].localRotation = CbArt.Rot(0.3f * (i == 0 ? 1.4f : 0.5f) + Mathf.Sin(clock * 3 + i * 0.7f) * 0.15f, Mathf.Sin(clock * 2.2f + i * 0.6f) * 0.2f);
            for (int i = 0; i < f.bandTails.Count; i++)
                f.bandTails[i].localRotation = CbArt.Rot(0.3f + Mathf.Sin(clock * 9 + i) * 0.25f, Mathf.Sin(clock * 7 + i * 2) * 0.3f);

            if (model == null) return;
            if (Walking && walkClip != null) model.LoopSeconds(walkClip, walkT);
            else if (clip != null)
            {
                if (once) model.Window(clip, from, to, clipT / dur);
                else model.LoopSeconds(clip, clipT);
            }
            if (prop != null)
            {
                prop.position = model.Palm(model.rightHand);
                prop.rotation = model.Grip(propAxis);
            }
        }

        public void Destroy() => f.Destroy();
    }
}
