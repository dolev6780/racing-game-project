using System;
using System.Collections.Generic;
using RacingGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cloudborne
{
    /// <summary>
    /// Drawn, animated cutscenes for the story's big moments: full-screen paintings (drawn in Canva, approved by the
    /// user, in Resources/Cloudborne/Story) brought to life - each one slowly pushed in or panned across, crossfaded
    /// into the next, with drifting stars, embers, sparks or fireflies over it, flashes, a shaking camera and a line of
    /// narration. It plays by itself; SKIP (or pause) ends it.
    /// </summary>
    public class CbComic
    {
        /// <summary>One painting on screen.</summary>
        public class Shot
        {
            /// <summary>Resources/Cloudborne/Story/{image}; a title card when null.</summary>
            public string image;
            /// <summary>Used in place of the image (tests).</summary>
            public Texture2D texture;
            public float dur = 3.5f;
            /// <summary>The view at the start and the end: the centre (0..1 across and up the painting) and the zoom (1 = the whole height fits).</summary>
            public Vector3 from = new Vector3(0.5f, 0.5f, 1f), to = new Vector3(0.5f, 0.5f, 1.12f);
            public string who, caption;
            /// <summary>What drifts over it: stars, embers, purple, sparks, fireflies.</summary>
            public string fx;
            public bool flash, shake;
            public string sfx;
            public string title, subtitle;
        }

        class Mote
        {
            public RectTransform rt;
            public Image img;
            public Vector2 pos, vel;
            public float life, age, size, twinkle;
            public Color col;
        }

        static readonly Color Paper = CbArt.Hex("#fff6e8"), Gold = CbArt.Hex("#ffc94a");
        const float Cross = 0.6f, Bar = 34f;

        readonly RectTransform root, frame;
        readonly RawImage below, above;
        readonly Image flash;
        readonly Text whoTxt, captionTxt, titleTxt, subtitleTxt;
        readonly List<Mote> motes = new List<Mote>();
        Shot[] shots;
        int index;
        float t, crossT, flashT, shakeT, spawnT;
        Action done;
        Texture belowTex;
        Rect belowUv;

        public bool Playing { get; private set; }

        public CbComic(Transform parent)
        {
            var go = new GameObject("Comic", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(844, 390);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            root = (RectTransform)go.transform;
            UIKit.Image(UIKit.Stretch(root, "bg"), null, Color.black);
            frame = UIKit.Stretch(root, "frame");
            below = UIKit.Stretch(frame, "below").gameObject.AddComponent<RawImage>();
            above = UIKit.Stretch(frame, "above").gameObject.AddComponent<RawImage>();
            below.raycastTarget = above.raycastTarget = false;
            var motesLayer = UIKit.Stretch(root, "motes");
            for (int i = 0; i < 48; i++)
            {
                var rt = UIKit.Rect(motesLayer, "mote", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 8);
                var img = UIKit.Image(rt, UIKit.SoftGlow, Color.clear);
                rt.gameObject.SetActive(false);
                motes.Add(new Mote { rt = rt, img = img });
            }
            flash = UIKit.Image(UIKit.Stretch(root, "flash"), null, Color.clear);
            // black bars and the narration over the lower one
            UIKit.Image(UIKit.Rect(root, "barTop", new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(4000, Bar)), null, Color.black);
            UIKit.Image(UIKit.Rect(root, "barBottom", new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(4000, Bar + 22)), null, Color.black);
            whoTxt = UIKit.Label(root, "", 12, Gold, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(700, 16), TextAnchor.MiddleCenter, true);
            captionTxt = UIKit.Label(root, "", 15, Paper, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(760, 26), TextAnchor.MiddleCenter);
            captionTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleTxt = UIKit.Label(root, "", 64, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(800, 80), TextAnchor.MiddleCenter, true);
            titleTxt.gameObject.AddComponent<Outline>().effectColor = CbArt.Hex("#ff5a2e");
            subtitleTxt = UIKit.Label(root, "", 20, Gold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -28), new Vector2(800, 28), TextAnchor.MiddleCenter, true);
            var skip = UIKit.Rect(root, "skip", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -2), new Vector2(100, 30));
            UIKit.Label(skip, "SKIP ▶▶", 13, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, true, false);
            root.gameObject.SetActive(false);
        }

        static Texture2D Load(Shot s) => s.texture != null ? s.texture : s.image != null ? Resources.Load<Texture2D>("Cloudborne/Story/" + s.image) : null;

        /// <summary>Every painting of the cutscene is in the project (until they are, the game goes on without it).</summary>
        public static bool Available(Shot[] shots)
        {
            if (shots == null || shots.Length == 0) return false;
            foreach (var s in shots)
                if (s.title == null && Load(s) == null) return false;
            return true;
        }

        /// <summary>Plays the shots one after another, then calls then.</summary>
        public void Play(Shot[] list, Action then)
        {
            shots = list;
            done = then;
            index = -1;
            Playing = true;
            root.gameObject.SetActive(true);
            belowTex = null;
            below.color = above.color = Color.clear;
            foreach (var m in motes) { m.age = m.life = 0; m.rt.gameObject.SetActive(false); }
            Next();
        }

        /// <summary>Ends the cutscene at once (SKIP).</summary>
        public void Skip()
        {
            if (!Playing) return;
            Finish();
        }

        void Finish()
        {
            Playing = false;
            var d = done;
            done = null;
            d?.Invoke();
        }

        /// <summary>Takes the cutscene off the screen (after the fade to black that follows it).</summary>
        public void Hide()
        {
            Playing = false;
            root.gameObject.SetActive(false);
        }

        void Next()
        {
            // the painting on screen moves to the lower layer and fades out over the next one
            if (index >= 0 && shots[index].title == null)
            {
                belowTex = above.texture;
                belowUv = above.uvRect;
            }
            index++;
            t = 0;
            crossT = Cross;
            if (index >= shots.Length) { Finish(); return; }
            var s = shots[index];
            var tex = Load(s);
            above.texture = tex;
            titleTxt.text = s.title ?? "";
            subtitleTxt.text = s.subtitle ?? "";
            whoTxt.text = s.who ?? "";
            captionTxt.text = s.caption ?? "";
            if (s.flash) flashT = 0.45f;
            if (s.shake) shakeT = 0.9f;
            if (!string.IsNullOrEmpty(s.sfx)) CbFx.Sfx(s.sfx, 0.8f);
            if (s.fx == "sparks") for (int i = 0; i < 24; i++) Spawn("sparks", true);
        }

        /// <summary>
        /// How long a shot stays: its own time made unhurried, and long enough to read its words at an easy pace
        /// (the user found the prologue too fast). A tap moves on sooner.
        /// </summary>
        static float Length(Shot s) => s.title != null ? s.dur * 1.5f : Mathf.Max(s.dur * 1.6f, 3f + (s.caption?.Length ?? 0) * 0.07f);

        /// <summary>A tap: on to the next painting (after a moment, so a tap meant for something else doesn't skip it).</summary>
        public void Tap()
        {
            if (!Playing || shots == null || index < 0 || index >= shots.Length || t < 0.8f) return;
            Next();
        }

        public void Update(float dt)
        {
            if (!Playing || shots == null || index >= shots.Length) return;
            var s = shots[index];
            float dur = Length(s);
            t += dt;
            float k = Mathf.Clamp01(t / dur);
            float ease = k * k * (3 - 2 * k);

            // the painting: covering the screen, its view easing from `from` to `to`
            var view = Vector3.Lerp(s.from, s.to, ease);
            var tex = above.texture;
            if (tex != null) above.uvRect = Cover(tex, view);
            crossT = Mathf.Max(0, crossT - dt);
            float fadeIn = 1 - crossT / Cross;
            above.color = tex != null ? new Color(1, 1, 1, fadeIn) : Color.clear;
            below.texture = belowTex;
            below.uvRect = belowUv;
            below.color = belowTex != null ? new Color(1, 1, 1, 1 - fadeIn) : Color.clear;
            if (fadeIn >= 1) belowTex = null;

            // shake, flash, and the words fading in after a moment and out at the end
            shakeT = Mathf.Max(0, shakeT - dt);
            float sh = shakeT * 9f;
            frame.anchoredPosition = new Vector2(UnityEngine.Random.Range(-sh, sh), UnityEngine.Random.Range(-sh, sh));
            frame.localScale = Vector3.one * (1 + shakeT * 0.03f);
            flashT = Mathf.Max(0, flashT - dt);
            flash.color = new Color(1, 1, 1, flashT / 0.45f * 0.9f);
            float words = Mathf.Clamp01((t - 0.5f) / 0.5f) * Mathf.Clamp01((dur - t) / 0.4f);
            SetAlpha(whoTxt, words); SetAlpha(captionTxt, words);
            float title = s.title != null ? Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((dur - t) / 0.5f) : 0;
            SetAlpha(titleTxt, title); SetAlpha(subtitleTxt, Mathf.Clamp01((t - 0.5f) / 0.6f) * Mathf.Clamp01((dur - t) / 0.5f));
            titleTxt.transform.localScale = Vector3.one * (1.15f - 0.15f * Mathf.Clamp01(t / 0.8f));

            // what drifts over it
            spawnT -= dt;
            if (!string.IsNullOrEmpty(s.fx) && spawnT <= 0)
            {
                spawnT = s.fx == "stars" ? 0.12f : s.fx == "fireflies" ? 0.25f : s.fx == "sparks" ? 0.08f : 0.06f;
                Spawn(s.fx, false);
            }
            foreach (var m in motes)
            {
                if (m.life <= 0) continue;
                m.age += dt;
                if (m.age >= m.life) { m.life = 0; m.rt.gameObject.SetActive(false); continue; }
                m.pos += m.vel * dt;
                if (s.fx == "fireflies") m.vel += new Vector2(Mathf.Sin(Time.unscaledTime * 2 + m.twinkle) * 12, Mathf.Cos(Time.unscaledTime * 1.7f + m.twinkle) * 10) * dt;
                m.rt.anchoredPosition = m.pos;
                float a = Mathf.Sin(m.age / m.life * Mathf.PI) * (0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 9 + m.twinkle));
                m.img.color = new Color(m.col.r, m.col.g, m.col.b, a);
                m.rt.sizeDelta = Vector2.one * m.size;
            }

            if (t >= dur) Next();
        }

        /// <summary>The part of the painting to show so it covers the screen, around a centre, zoomed in by view.z.</summary>
        Rect Cover(Texture tex, Vector3 view)
        {
            float screen = root.rect.width / Mathf.Max(1f, root.rect.height);
            float image = tex.width / (float)Mathf.Max(1, tex.height);
            float w = screen > image ? 1 : screen / image, h = screen > image ? image / screen : 1;
            float z = Mathf.Max(1, view.z);
            w /= z; h /= z;
            float x = Mathf.Clamp(view.x - w / 2, 0, 1 - w), y = Mathf.Clamp(view.y - h / 2, 0, 1 - h);
            return new Rect(x, y, w, h);
        }

        void Spawn(string fx, bool burst)
        {
            Mote m = null;
            foreach (var c in motes) if (c.life <= 0) { m = c; break; }
            if (m == null) return;
            var size = root.rect.size;
            float R(float a, float b) => UnityEngine.Random.Range(a, b);
            switch (fx)
            {
                case "stars":
                    m.pos = new Vector2(R(0, size.x), R(size.y * 0.35f, size.y)); m.vel = Vector2.zero; m.life = R(0.8f, 1.8f); m.size = R(6, 14);
                    m.col = new Color(1, 1, 0.95f); break;
                case "embers":
                    m.pos = new Vector2(R(0, size.x), R(-10, size.y * 0.3f)); m.vel = new Vector2(R(-20, 30), R(40, 110)); m.life = R(1.2f, 2.4f); m.size = R(6, 13);
                    m.col = new Color(1, R(0.45f, 0.75f), 0.15f); break;
                case "purple":
                    m.pos = new Vector2(R(0, size.x), R(0, size.y)); m.vel = new Vector2(R(-60, 60), R(-30, 60)); m.life = R(0.4f, 1f); m.size = R(8, 20);
                    m.col = new Color(0.75f, 0.4f, 1f); break;
                case "sparks":
                    var dir = UnityEngine.Random.insideUnitCircle.normalized;
                    m.pos = size * 0.5f + dir * (burst ? 10 : R(0, 60)); m.vel = dir * R(120, burst ? 420 : 220); m.life = R(0.5f, 1.2f); m.size = R(6, 14);
                    m.col = new Color(1, R(0.8f, 0.95f), 0.5f); break;
                case "fireflies":
                    m.pos = new Vector2(R(0, size.x), R(0, size.y * 0.6f)); m.vel = new Vector2(R(-15, 15), R(-10, 15)); m.life = R(2f, 3.5f); m.size = R(7, 12);
                    m.col = new Color(0.85f, 1f, 0.45f); m.twinkle = R(0, 6); break;
                default: return;
            }
            m.age = 0;
            m.twinkle = R(0, 6);
            m.rt.gameObject.SetActive(true);
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }
    }
}
