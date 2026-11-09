using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// A cinematic played in the game engine: a list of beats run one after another. A beat is an instant action, a
    /// timed stretch (with something happening every frame), a drawn cutscene played inside it, or a line of dialogue
    /// in the speech box, which waits for the player: the first tap shows the whole line, the next goes on.
    /// The scene directs the camera, Raja and its own cast; SKIP (or pause) jumps to the end, where
    /// <see cref="Finally"/> leaves the world as the scene would have.
    /// </summary>
    public class CbScene
    {
        class Beat
        {
            public float dur;
            public Action start;
            public Action<float> tick;
            public string who, line;
            public Color color;
            public CbComic.Shot[] comic;
        }

        readonly CbGame g;
        readonly List<Beat> beats = new List<Beat>();
        readonly List<CbActor> cast = new List<CbActor>();
        /// <summary>Holds the cast and props made for the scene; removed with it.</summary>
        public readonly Transform stage;
        Action finish;
        int index = -1;
        float t;

        /// <summary>Where the camera looks (x, y) and how close it stands (1 = as in play, less is closer).</summary>
        public float camX, camY = 1.5f, zoom = 1f;
        float camToX, camToY, camToZoom = 1f, camRate = 2.5f;

        // what Raja does: a clip looped at its pace, a part of one played once, or a run to a point
        string rajaClip = "Idle";
        float rajaT, rajaFrom, rajaTo, rajaDur, rajaGoal = float.NaN, rajaSpeed;
        bool rajaOnce;
        /// <summary>Fire glowing in Raja's fur (0 = none).</summary>
        public float rajaGlow;

        /// <summary>Runs every frame from when a beat sets it (e.g. a captive carried along between two raiders).</summary>
        public Action Always;

        public bool Done { get; private set; }
        /// <summary>Which beat is playing (-1 before the first).</summary>
        public int BeatIndex => index;

        public CbScene(CbGame game, string name)
        {
            g = game;
            stage = new GameObject(name).transform;
            stage.SetParent(game.transform, false);
            camX = camToX = game.hero.x;
            camY = camToY = 1.5f;
        }

        // ---------- writing the scene ----------

        /// <summary>Something that happens at once.</summary>
        public CbScene Do(Action a)
        {
            beats.Add(new Beat { start = a });
            return this;
        }

        /// <summary>A stretch of seconds; tick gets how far through it is (0 to 1) every frame.</summary>
        public CbScene Wait(float seconds, Action<float> tick = null, Action start = null)
        {
            beats.Add(new Beat { dur = seconds, tick = tick, start = start });
            return this;
        }

        /// <summary>A line in the speech box; tick gets the seconds since it began.</summary>
        public CbScene Say(string who, Color color, string line, Action start = null, Action<float> tick = null)
        {
            beats.Add(new Beat { who = who, color = color, line = line, start = start, tick = tick });
            return this;
        }

        /// <summary>A drawn cutscene inside the scene, which waits for it; left out while its paintings are not in the project.</summary>
        public CbScene Comic(CbComic.Shot[] shots)
        {
            beats.Add(new Beat { comic = shots });
            return this;
        }

        /// <summary>What must be true when the scene is over, played through or skipped.</summary>
        public CbScene Finally(Action a)
        {
            finish = a;
            return this;
        }

        /// <summary>A character for this scene, removed when it ends.</summary>
        public CbActor Cast(string name, string[] models, float height, RigStyle style, Action<Rig> dress, float x, int facing,
            string propPath = null, Vector3? propAxis = null)
        {
            var a = new CbActor(stage, name, models, height, style, dress, x, facing, propPath, propAxis);
            cast.Add(a);
            return a;
        }

        // ---------- directing ----------

        /// <summary>Moves the camera to look at (x, y) from zoom times its usual distance, easing at rate.</summary>
        public void Camera(float x, float y, float zoomTo, float rate = 2.5f)
        {
            camToX = x; camToY = y; camToZoom = zoomTo; camRate = rate;
        }

        /// <summary>Puts the camera there at once.</summary>
        public void Cut(float x, float y, float zoomTo)
        {
            Camera(x, y, zoomTo);
            camX = x; camY = y; zoom = zoomTo;
        }

        public void Raja(string clip)
        {
            if (rajaClip == clip && !rajaOnce) return;
            rajaClip = clip; rajaOnce = false; rajaT = 0; rajaGoal = float.NaN;
        }

        public void RajaOnce(string clip, float from, float to, float seconds)
        {
            rajaClip = clip; rajaOnce = true; rajaFrom = from; rajaTo = to; rajaDur = Mathf.Max(0.01f, seconds); rajaT = 0; rajaGoal = float.NaN;
        }

        public void RajaRunTo(float x, float speed)
        {
            rajaGoal = x; rajaSpeed = speed; rajaT = 0;
        }

        // ---------- playing ----------

        public void Update(float dt, CbInput input)
        {
            if (Done) return;
            if (index < 0) Next();
            if (Done) return;
            t += dt;
            var b = beats[index];
            if (b.line != null)
            {
                b.tick?.Invoke(t);
                // the talk only moves on when the player taps
                if (input.confirm)
                {
                    if (!g.hud.LineDone) g.hud.CompleteLine();
                    else Next();
                }
            }
            else if (b.comic != null)
            {
                if (!g.comic.Playing) Next();
            }
            else
            {
                b.tick?.Invoke(Mathf.Clamp01(t / Mathf.Max(b.dur, 0.0001f)));
                if (t >= b.dur) Next();
            }
            if (Done) return;

            Always?.Invoke();
            foreach (var a in cast) a.Update(dt, g.clock);
            UpdateRaja(dt);
            float k = 1 - Mathf.Exp(-camRate * dt);
            camX += (camToX - camX) * k;
            camY += (camToY - camY) * k;
            zoom += (camToZoom - zoom) * k;
        }

        void Next()
        {
            while (true)
            {
                index++;
                t = 0;
                if (index >= beats.Count) { End(); return; }
                var b = beats[index];
                b.start?.Invoke();
                if (b.line != null) { g.hud.Say(b.who, b.color, b.line); return; }
                g.hud.HideLine();
                if (b.comic != null)
                {
                    if (!CbComic.Available(b.comic)) continue;
                    // a quick fade back to the game when the drawn part ends
                    g.comic.Play(b.comic, () => g.fade.FadeThrough(() => g.comic.Hide(), 0.15f, 0.3f));
                    return;
                }
                if (b.dur > 0) return;
            }
        }

        void UpdateRaja(float dt)
        {
            var h = g.hero;
            rajaT += dt;
            if (!float.IsNaN(rajaGoal))
            {
                float d = rajaGoal - h.x, step = rajaSpeed * dt;
                h.facing = d >= 0 ? 1 : -1;
                if (Mathf.Abs(d) <= step) { h.x = rajaGoal; rajaGoal = float.NaN; rajaClip = "Combat_Stance"; rajaOnce = false; }
                else h.x += Mathf.Sign(d) * step;
                h.Act(dt, "RunFast", rajaT * rajaSpeed / 7f, true, rajaGlow);
                return;
            }
            if (rajaOnce) h.Act(dt, rajaClip, Mathf.Lerp(rajaFrom, rajaTo, Mathf.Clamp01(rajaT / rajaDur)), false, rajaGlow);
            else h.Act(dt, rajaClip, rajaT, true, rajaGlow);
        }

        /// <summary>Jumps to the end of the scene (SKIP).</summary>
        public void Skip()
        {
            if (!Done) End();
        }

        void End()
        {
            Done = true;
            if (g.comic.Playing) g.comic.Hide();
            foreach (var a in cast) a.Destroy();
            cast.Clear();
            if (stage != null) UnityEngine.Object.Destroy(stage.gameObject);
            g.hud.HideLine();
            finish?.Invoke();
            g.EndScene();
        }
    }
}
