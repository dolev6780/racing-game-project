using System;
using RacingGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cloudborne
{
    /// <summary>
    /// The black between places: a fade to black and back (between a drawn cutscene and the game), and the loading
    /// screen when a stage starts - the stage's name, four crystals circling, a filling bar and a tip - after which
    /// the screen fades open on what comes next. Drawn over everything else.
    /// </summary>
    public class CbTransition
    {
        static readonly Color Paper = CbArt.Hex("#fff6e8"), Gold = CbArt.Hex("#ffc94a"), Ink = new Color(0.04f, 0.05f, 0.04f, 1f);
        static readonly Color[] CrystalCols = { CbArt.Hex("#ff6a3a"), CbArt.Hex("#4ab8ff"), CbArt.Hex("#6fe08a"), CbArt.Hex("#f4f4ff") };

        static readonly string[] Tips =
        {
            "Break crates with two blows: mangoes heal, coins buy upgrades in Baba's workshop.",
            "Cracked stone gives way only to the staff (HEAVY).",
            "Hold GUARD to stop blows from the front.",
            "HIT, HIT, KI sweeps the staff all around Raja.",
            "DASH just before a blow lands for slow motion and a double-strength hit.",
            "Some ledges only show themselves to those who climb high.",
            "The golden fruit restores everything. It is never in an easy place.",
        };

        readonly RectTransform root, loading;
        readonly Image black, barFill;
        readonly Text titleTxt, tipTxt;
        readonly RectTransform[] crystals = new RectTransform[4];
        enum Phase { Idle, Out, Hold, In }
        Phase phase;
        float t, holdTime, outTime, inTime;
        bool showLoading;
        Action mid;

        /// <summary>A transition is running (input to the game waits).</summary>
        public bool Busy => phase != Phase.Idle;

        public CbTransition(Transform parent)
        {
            var go = new GameObject("Transition", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(844, 390);
            // the whole layout always fits, on a long phone or a squarer one (see CbHud)
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = (RectTransform)go.transform;
            black = UIKit.Image(UIKit.Stretch(root, "black"), null, Ink);

            loading = UIKit.Stretch(root, "loading");
            var logo = UIKit.Label(loading, "RAJA", 40, Paper, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(400, 46), TextAnchor.MiddleCenter, true);
            logo.gameObject.AddComponent<Outline>().effectColor = CbArt.Hex("#ff5a2e");
            UIKit.Label(loading, "AND THE FOUR CRYSTALS", 13, Gold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(400, 18), TextAnchor.MiddleCenter, true);
            for (int i = 0; i < 4; i++)
            {
                crystals[i] = UIKit.Rect(loading, "crystal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(16, 16));
                UIKit.Image(crystals[i], null, CrystalCols[i]);
            }
            titleTxt = UIKit.Label(loading, "", 20, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -52), new Vector2(700, 28), TextAnchor.MiddleCenter, true);
            var bar = UIKit.Rect(loading, "bar", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 76), new Vector2(360, 8));
            UIKit.Image(bar, UIKit.RoundedSmall, new Color(1, 1, 1, 0.15f));
            var fill = UIKit.Rect(bar, "fill", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(360, 8));
            barFill = UIKit.Image(fill, UIKit.RoundedSmall, Gold);
            tipTxt = UIKit.Label(loading, "", 12, new Color(1, 1, 1, 0.75f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(700, 20), TextAnchor.MiddleCenter);
            root.gameObject.SetActive(false);
        }

        /// <summary>The loading screen: fades in, shows the stage's name and fills for seconds, runs then, fades open.</summary>
        public void Loading(string title, float seconds, Action then)
        {
            titleTxt.text = title;
            tipTxt.text = Tips[UnityEngine.Random.Range(0, Tips.Length)];
            Begin(0.3f, seconds, 0.5f, then, true);
        }

        /// <summary>A fade to black, then runs then, and fades open again.</summary>
        public void FadeThrough(Action then, float outSeconds = 0.35f, float inSeconds = 0.45f) => Begin(outSeconds, 0.05f, inSeconds, then, false);

        void Begin(float o, float hold, float i, Action then, bool withLoading)
        {
            // a fade begun while another runs (SKIP as the loading screen opens) goes on from the darkness it is at
            float from = phase == Phase.Idle ? 0 : black.color.a;
            outTime = o; holdTime = hold; inTime = i; mid = then; showLoading = withLoading;
            phase = Phase.Out;
            t = from * o;
            root.gameObject.SetActive(true);
            SetAlpha(from);
            barFill.rectTransform.localScale = new Vector3(0, 1, 1);
        }

        public void Update(float dt)
        {
            if (phase == Phase.Idle) return;
            t += dt;
            switch (phase)
            {
                case Phase.Out:
                    SetAlpha(Mathf.Clamp01(t / outTime));
                    if (t >= outTime) { phase = Phase.Hold; t = 0; }
                    break;
                case Phase.Hold:
                    SetAlpha(1);
                    barFill.rectTransform.localScale = new Vector3(Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / holdTime)), 1, 1);
                    if (t >= holdTime)
                    {
                        phase = Phase.In; t = 0;
                        var m = mid; mid = null;
                        m?.Invoke();
                    }
                    break;
                case Phase.In:
                    SetAlpha(1 - Mathf.Clamp01(t / inTime));
                    if (t >= inTime) { phase = Phase.Idle; root.gameObject.SetActive(false); }
                    break;
            }
            // the four crystals circle the centre, each pulsing
            for (int i = 0; i < 4; i++)
            {
                float a = Time.unscaledTime * 2.2f + i * Mathf.PI / 2;
                crystals[i].anchoredPosition = new Vector2(Mathf.Cos(a) * 34, 18 + Mathf.Sin(a) * 34);
                crystals[i].localRotation = Quaternion.Euler(0, 0, 45 + a * 30);
                crystals[i].localScale = Vector3.one * (0.9f + 0.25f * Mathf.Sin(Time.unscaledTime * 5 + i));
            }
        }

        void SetAlpha(float a)
        {
            black.color = new Color(Ink.r, Ink.g, Ink.b, a);
            loading.gameObject.SetActive(showLoading && a > 0.01f);
            foreach (var g in loading.GetComponentsInChildren<Graphic>())
            {
                var c = g.color;
                // keep each one's own transparency (the tip, the empty bar) under the fade
                float own = g == tipTxt ? 0.75f : g.name == "bar" ? 0.15f : 1f;
                c.a = own * a;
                g.color = c;
            }
        }
    }
}
