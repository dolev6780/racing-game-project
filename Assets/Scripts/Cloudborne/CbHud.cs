using System.Collections.Generic;
using RacingGame.Core;
using RacingGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cloudborne
{
    /// <summary>
    /// Screen layer: health and ki bars, stage title and tips, score, combo counter, banners and damage numbers,
    /// the touch controls (floating stick + five buttons) and the title / pause / result screens.
    /// Touches are read directly (no EventSystem), so several fingers work at once.
    /// </summary>
    public class CbHud
    {
        public enum PopStyle { Normal, Big, Burn, Hurt, Block }
        public enum Screen { None, Title, Paused, Result }

        static readonly Color Paper = CbArt.Hex("#fff6e8"), Gold = CbArt.Hex("#ffc94a"), FireCol = CbArt.Hex("#ffb03a"),
            Ember = CbArt.Hex("#ff5a2e"), Ink = new Color(0.07f, 0.09f, 0.06f, 1f), Glass = new Color(0.07f, 0.09f, 0.06f, 0.55f);

        readonly Canvas canvas;
        readonly RectTransform root;
        readonly Image hpFill, kiFill, kiFrame, hurtFlash, slowFx;
        readonly Text waveTxt, tipTxt, scoreTxt, comboN, goTxt;
        readonly RectTransform comboBox, pads, stickBase, stickKnob, title, paused, result, pauseBtn;
        readonly Text resultTitle, resultStats, startLabel;
        readonly List<Pad> buttons = new List<Pad>();
        readonly Pad sp;
        readonly List<(RectTransform rt, Text t, float age, Vector2 from)> pops = new List<(RectTransform, Text, float, Vector2)>();
        readonly List<(RectTransform rt, float age, float life)> banners = new List<(RectTransform, float, float)>();
        float comboPop, hurtT;
        Screen screen = Screen.Title;

        class Pad
        {
            public string id;
            public RectTransform rt;
            public Image img;
            public float radius;
            public int pointer = int.MinValue;
        }

        public CbHud(Transform parent)
        {
            var go = new GameObject("HUD", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(844, 390);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            root = (RectTransform)go.transform;

            hurtFlash = UIKit.Image(UIKit.Stretch(root, "hurt"), UIKit.Vignette, new Color(1f, 0.2f, 0.15f, 0f));
            slowFx = UIKit.Image(UIKit.Stretch(root, "slow"), UIKit.Vignette, new Color(0.25f, 0.5f, 1f, 0f));

            // status: portrait, name, bars
            var status = UIKit.Rect(root, "status", new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -12), new Vector2(300, 60));
            var portrait = UIKit.Rect(status, "portrait", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(56, 56));
            UIKit.Image(portrait, UIKit.Circle, Paper);
            var face = UIKit.Rect(portrait, "face", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50, 50));
            var faceImg = face.gameObject.AddComponent<RawImage>();
            faceImg.texture = Portrait();
            faceImg.raycastTarget = false;
            UIKit.Label(status, "RAJA", 18, Paper, new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, 0), new Vector2(120, 22), TextAnchor.MiddleLeft, true);
            hpFill = Bar(status, new Vector2(66, -24), new Vector2(200, 12), CbArt.Hex("#6fe08a"), out _);
            kiFill = Bar(status, new Vector2(66, -42), new Vector2(160, 9), FireCol, out kiFrame);

            // stage title, wave, tip
            UIKit.Label(root, "STAGE 1-1 · THE JUNGLE GATE", 11, new Color(1, 1, 1, 0.9f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(320, 16), TextAnchor.MiddleCenter);
            waveTxt = UIKit.Label(root, "", 20, Paper, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(360, 26), TextAnchor.MiddleCenter, true);
            var tipBox = UIKit.Panel(root, "tip", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(270, 34), Glass);
            tipTxt = UIKit.Label(tipBox.transform, "", 11, Paper, TextAnchor.MiddleCenter, false, false);
            tipTxt.supportRichText = true;
            tipTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            ((RectTransform)tipTxt.transform).offsetMin = new Vector2(10, 2);
            ((RectTransform)tipTxt.transform).offsetMax = new Vector2(-10, -2);

            // score, pause
            UIKit.Label(root, "SCORE", 10, new Color(1, 1, 1, 0.8f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-76, -14), new Vector2(120, 14), TextAnchor.MiddleRight);
            scoreTxt = UIKit.Label(root, "0", 24, Paper, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-76, -28), new Vector2(160, 28), TextAnchor.MiddleRight, true);
            pauseBtn = UIKit.Rect(root, "pause", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -12), new Vector2(48, 48));
            UIKit.Image(pauseBtn, UIKit.Circle, Glass);
            UIKit.Image(UIKit.Rect(pauseBtn, "ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48)), UIKit.Ring, Paper);
            UIKit.Label(pauseBtn, "II", 18, Paper, TextAnchor.MiddleCenter, true, false);

            // combo, go
            comboBox = UIKit.Rect(root, "combo", new Vector2(1, 0.62f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(160, 100));
            comboN = UIKit.Label(comboBox, "0", 56, Gold, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(160, 64), TextAnchor.MiddleRight, true);
            UIKit.Label(comboBox, "HITS!", 20, Paper, new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 10), new Vector2(160, 26), TextAnchor.MiddleRight, true);
            comboBox.gameObject.SetActive(false);
            goTxt = UIKit.Label(root, "GO ▶", 40, Gold, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-32, 0), new Vector2(160, 50), TextAnchor.MiddleRight, true);
            goTxt.gameObject.SetActive(false);

            // touch pads
            pads = UIKit.Stretch(root, "pads");
            stickBase = UIKit.Rect(pads, "stick", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(100, 100), new Vector2(120, 120));
            UIKit.Image(stickBase, UIKit.Circle, new Color(0.07f, 0.09f, 0.06f, 0.35f));
            UIKit.Image(UIKit.Rect(stickBase, "ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120)), UIKit.Ring, new Color(1, 1, 1, 0.5f));
            stickKnob = UIKit.Rect(stickBase, "knob", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54));
            UIKit.Image(stickKnob, UIKit.Circle, new Color(0.95f, 0.93f, 0.88f, 0.95f));
            // centres measured from the bottom-right corner, as in the web demo
            AddPad("atk", "HIT", new Vector2(-14 - 46, 12 + 46), 92, CbArt.Hex("#d8453f"), 22);
            AddPad("jump", "JUMP", new Vector2(-14 - 108 - 36, 12 + 36), 72, CbArt.Hex("#3aa862"), 17);
            AddPad("dash", "DASH", new Vector2(-14 - 104 - 32, 12 + 86 + 32), 64, CbArt.Hex("#2f8ae0"), 15);
            AddPad("ki", "KI", new Vector2(-14 - 8 - 37, 12 + 106 + 37), 74, CbArt.Hex("#e8642a"), 20);
            sp = AddPad("sp", "ROAR", new Vector2(-14 - 186 - 32, 12 + 76 + 32), 64, CbArt.Hex("#b8401f"), 15);

            // screens
            title = Overlay("title");
            UIKit.Label(title, "DEMO BUILD · STAGE 1-1 — THE JUNGLE GATE", 13, Gold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 128), new Vector2(700, 20), TextAnchor.MiddleCenter);
            var logo = UIKit.Label(title, "CLOUDBORNE", 76, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 72), new Vector2(800, 90), TextAnchor.MiddleCenter, true);
            var logoShadow = logo.gameObject.AddComponent<Outline>();
            logoShadow.effectColor = Ember; logoShadow.effectDistance = new Vector2(3, -4);
            logo.transform.parent.localRotation = Quaternion.Euler(0, 0, 3);
            UIKit.Label(title, "RAJA, THE FIRE TIGER", 13, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(400, 20), TextAnchor.MiddleCenter);
            startLabel = BigButton(title, "START", new Vector2(0, -26));
            var keys = UIKit.Label(title, "<color=#ffb03a>HIT ×4</color> paw combo · <color=#ffb03a>HIT, HIT, hold HIT</color> staff whirl · <color=#ffb03a>↑ + HIT</color> launcher · <color=#ffb03a>DASH → HIT</color> thrust · <color=#ffb03a>↓ + HIT</color> in the air slam\ntap <color=#ffb03a>KI</color> fireball · hold <color=#ffb03a>KI</color> charge · <color=#ffb03a>ROAR</color> when the ki bar glows · keyboard: A/D W/S Space J K L I",
                11, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(780, 44), TextAnchor.MiddleCenter);
            keys.supportRichText = true;

            paused = Overlay("paused");
            UIKit.Label(paused, "PAUSED", 60, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(600, 70), TextAnchor.MiddleCenter, true);
            BigButton(paused, "RESUME", new Vector2(0, -30));

            result = Overlay("result");
            resultTitle = UIKit.Label(result, "STAGE CLEAR", 64, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(700, 80), TextAnchor.MiddleCenter, true);
            resultStats = UIKit.Label(result, "", 18, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(700, 40), TextAnchor.MiddleCenter, true);
            BigButton(result, "PLAY AGAIN", new Vector2(0, -62));

            SetScreen(Screen.Title);
        }

        // ---------- building blocks ----------

        Image Bar(RectTransform parent, Vector2 pos, Vector2 size, Color color, out Image frame)
        {
            var rt = UIKit.Rect(parent, "bar", new Vector2(0, 1), new Vector2(0, 1), pos, size);
            frame = UIKit.Image(rt, UIKit.RoundedSmall, Paper);
            var inner = UIKit.Rect(rt, "bg", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(4, 4));
            UIKit.Image(inner, UIKit.RoundedSmall, new Color(0.07f, 0.09f, 0.06f, 0.85f));
            var fill = UIKit.Rect(inner, "fill", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, size - new Vector2(4, 4));
            var img = UIKit.Image(fill, null, color);
            return img;
        }

        Pad AddPad(string id, string label, Vector2 centre, float size, Color color, int font)
        {
            var rt = UIKit.Rect(pads, id, new Vector2(1, 0), new Vector2(0.5f, 0.5f), centre, new Vector2(size, size));
            UIKit.Image(UIKit.Rect(rt, "shadow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(size, size)), UIKit.Circle, Ink);
            var img = UIKit.Image(UIKit.Rect(rt, "fill", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size)), UIKit.Circle, color);
            UIKit.Image(UIKit.Rect(rt, "ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size)), UIKit.Ring, Paper);
            UIKit.Label(rt, label, font, Paper, TextAnchor.MiddleCenter, true, true);
            var p = new Pad { id = id, rt = rt, img = img, radius = size * 0.5f };
            buttons.Add(p);
            return p;
        }

        RectTransform Overlay(string name)
        {
            var rt = UIKit.Stretch(root, name);
            var bg = UIKit.Image(rt, null, new Color(0.07f, 0.09f, 0.06f, 0.55f));
            bg.raycastTarget = false;
            return rt;
        }

        Text BigButton(RectTransform parent, string label, Vector2 pos)
        {
            var rt = UIKit.Rect(parent, "button", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(220, 56));
            UIKit.Image(UIKit.Rect(rt, "shadow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -5), new Vector2(220, 56)), UIKit.Rounded, Ink);
            UIKit.Image(UIKit.Rect(rt, "fill", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 56)), UIKit.Rounded, CbArt.Hex("#e2541c"));
            return UIKit.Label(rt, label, 26, Paper, TextAnchor.MiddleCenter, true);
        }

        /// <summary>Raja's face drawn into a small texture (orange head, stripes, red band, white muzzle).</summary>
        static Texture2D Portrait()
        {
            const int S = 96;
            var px = new Color[S * S];
            Color bg = CbArt.Hex("#2b4a2a"), orange = CbArt.Hex("#f08a24"), dark = CbArt.Hex("#1d1310"), white = CbArt.Hex("#fff4e2"),
                red = CbArt.Hex("#c8323c"), amber = CbArt.Hex("#f2a20c");
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = 1f - y / (float)S;   // v grows downwards, like the SVG
                    var c = bg;
                    bool Tri(float ax, float ay, float bx, float by, float cx, float cy)
                    {
                        float d1 = (u - bx) * (ay - by) - (ax - bx) * (v - by), d2 = (u - cx) * (by - cy) - (bx - cx) * (v - cy), d3 = (u - ax) * (cy - ay) - (cx - ax) * (v - ay);
                        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
                    }
                    bool Ell(float cx, float cy, float rx, float ry) => (u - cx) * (u - cx) / (rx * rx) + (v - cy) * (v - cy) / (ry * ry) < 1f;
                    if (Tri(0.21f, 0.25f, 0.32f, 0.11f, 0.41f, 0.27f) || Tri(0.79f, 0.25f, 0.68f, 0.11f, 0.59f, 0.27f)) c = orange;
                    if (Ell(0.5f, 0.55f, 0.32f, 0.32f)) c = orange;
                    if (Tri(0.5f, 0.25f, 0.46f, 0.38f, 0.54f, 0.38f) || Tri(0.3f, 0.39f, 0.39f, 0.45f, 0.3f, 0.48f) || Tri(0.7f, 0.39f, 0.61f, 0.45f, 0.7f, 0.48f)) c = dark;
                    if (v > 0.32f && v < 0.39f && Ell(0.5f, 0.55f, 0.32f, 0.32f)) c = red;
                    if (Ell(0.5f, 0.68f, 0.18f, 0.12f)) c = white;
                    if (Ell(0.375f, 0.52f, 0.057f, 0.064f) || Ell(0.625f, 0.52f, 0.057f, 0.064f)) c = white;
                    if (Ell(0.385f, 0.527f, 0.032f, 0.032f) || Ell(0.615f, 0.527f, 0.032f, 0.032f)) c = amber;
                    if (Tri(0.45f, 0.62f, 0.55f, 0.62f, 0.5f, 0.68f)) c = CbArt.Hex("#3a1f22");
                    px[y * S + x] = c;
                }
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // ---------- updates from the game ----------

        public void SetScreen(Screen s)
        {
            screen = s;
            title.gameObject.SetActive(s == Screen.Title);
            paused.gameObject.SetActive(s == Screen.Paused);
            result.gameObject.SetActive(s == Screen.Result);
            bool playing = s == Screen.None;
            pads.gameObject.SetActive(playing && ShowPads);
            pauseBtn.gameObject.SetActive(playing);
        }

        public static bool ShowPads => Application.isMobilePlatform;

        public void ShowResult(bool won, int score, int maxCombo, float time)
        {
            resultTitle.text = won ? "STAGE CLEAR" : "DEFEATED";
            resultStats.text = $"SCORE {score:N0}    ·    MAX COMBO {maxCombo}    ·    TIME {(int)(time / 60)}:{(int)(time % 60):00}";
            SetScreen(Screen.Result);
        }

        public void SetWave(string s) => waveTxt.text = s;
        public void SetTip(string s) { tipTxt.text = s; tipTxt.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(s)); }
        public void ShowGo(bool on) => goTxt.gameObject.SetActive(on);

        public void SetBars(float hp, float ki)
        {
            hpFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(hp / 100f), 1, 1);
            kiFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(ki / 100f), 1, 1);
            bool ready = ki >= 60;
            kiFrame.color = ready ? Color.Lerp(Paper, FireCol, 0.5f + 0.5f * Mathf.Sin(Time.time * 8)) : Paper;
            sp.rt.localScale = Vector3.one * (ready ? 1f + 0.04f * Mathf.Sin(Time.time * 7) : 1f);
            sp.img.color = ready ? CbArt.Hex("#d9541f") : new Color(0.72f, 0.25f, 0.12f, 0.45f);
        }

        public void SetScore(int s) => scoreTxt.text = s.ToString("N0");

        public void SetCombo(int n)
        {
            comboBox.gameObject.SetActive(n >= 2);
            comboN.text = n.ToString();
            if (n >= 2) comboPop = 0.18f;
        }

        public void FlashHurt() => hurtT = 0.25f;
        public void SetSlow(bool on) => slowFx.color = new Color(0.25f, 0.5f, 1f, on ? 0.5f : 0f);

        public void Banner(string text, bool small = false)
        {
            var rt = UIKit.Rect(root, "banner", new Vector2(0.5f, small ? 0.4f : 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 100));
            var t = UIKit.Label(rt, text, small ? 44 : 64, Paper, TextAnchor.MiddleCenter, true);
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = small ? CbArt.Hex("#2e6bff") : Ember;
            o.effectDistance = new Vector2(3, -4);
            banners.Add((rt, 0f, small ? 0.9f : 1.6f));
        }

        public void Popup(Vector3 world, string text, PopStyle style)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 sp3 = cam.WorldToScreenPoint(world);
            if (sp3.z < 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp3, null, out var local);
            var rt = UIKit.Rect(root, "pop", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), local, new Vector2(200, 50));
            int size = style == PopStyle.Big ? 34 : style == PopStyle.Burn ? 18 : style == PopStyle.Block ? 20 : 26;
            var col = style == PopStyle.Big ? Gold : style == PopStyle.Burn ? FireCol : style == PopStyle.Hurt ? Ember : style == PopStyle.Block ? CbArt.Hex("#c9d2de") : Paper;
            var t = UIKit.Label(rt, text, size, col, TextAnchor.MiddleCenter, true);
            pops.Add((rt, t, 0f, local));
        }

        /// <summary>Animates popups, banners and flashes. Unscaled time, so it keeps moving during hit-stop.</summary>
        public void Tick(float dt)
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i];
                p.age += dt;
                float k = p.age / 0.8f;
                p.rt.anchoredPosition = p.from + new Vector2(0, 60 * k);
                p.rt.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(k * 3));
                var c = p.t.color; c.a = 1 - k; p.t.color = c;
                if (k >= 1) { Object.Destroy(p.rt.gameObject); pops.RemoveAt(i); }
                else pops[i] = p;
            }
            for (int i = banners.Count - 1; i >= 0; i--)
            {
                var b = banners[i];
                b.age += dt;
                float k = b.age / b.life;
                b.rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(2.4f, 1f, k / 0.15f) : 1f + (k > 0.8f ? (k - 0.8f) * 0.25f : 0));
                foreach (var g in b.rt.GetComponentsInChildren<Graphic>())
                {
                    var c = g.color;
                    c.a = k < 0.15f ? k / 0.15f : k > 0.8f ? (1 - k) / 0.2f : 1f;
                    g.color = c;
                }
                if (k >= 1) { Object.Destroy(b.rt.gameObject); banners.RemoveAt(i); }
                else banners[i] = b;
            }
            if (comboPop > 0) { comboPop -= dt; comboN.transform.localScale = Vector3.one * (1 + Mathf.Max(0, comboPop) * 2f); }
            hurtT = Mathf.Max(0, hurtT - dt);
            hurtFlash.color = new Color(1f, 0.2f, 0.15f, hurtT > 0 ? 0.75f * hurtT / 0.25f : 0);
        }

        // ---------- touch ----------

        readonly HashSet<int> seen = new HashSet<int>();
        readonly HashSet<int> now = new HashSet<int>();
        int stickId = int.MinValue;
        Vector2 stickStart;
        float kiT = -1;

        /// <summary>Feeds touches (and the mouse) into the input: stick, button presses and holds, taps on screens.</summary>
        public void ReadTouch(CbInput input, float dt)
        {
            now.Clear();
            var pointers = InputBridge.GetPointers();
            float scale = canvas.scaleFactor;
            foreach (var p in pointers)
            {
                now.Add(p.Id);
                bool fresh = !seen.Contains(p.Id);
                if (screen != Screen.None)
                {
                    if (fresh) input.confirm = true;
                    continue;
                }
                if (p.Id == stickId)
                {
                    var d = (p.Position - stickStart) / scale;
                    d = Vector2.ClampMagnitude(d, 50f);
                    stickKnob.anchoredPosition = d;
                    input.x = Mathf.Abs(d.x) < 8 ? 0 : d.x / 50f;
                    input.y = Mathf.Abs(d.y) < 14 ? 0 : d.y / 50f;
                    continue;
                }
                if (!fresh) continue;
                if (Hit(pauseBtn, p.Position, 30)) { input.pause = true; continue; }
                if (!pads.gameObject.activeSelf) continue;
                Pad hit = null;
                foreach (var b in buttons) if (b.pointer == int.MinValue && Hit(b.rt, p.Position, b.radius * 1.15f)) { hit = b; break; }
                if (hit != null)
                {
                    hit.pointer = p.Id;
                    hit.rt.localScale = Vector3.one * 0.93f;
                    switch (hit.id)
                    {
                        case "atk": input.atk = true; break;
                        case "jump": input.jump = true; break;
                        case "dash": input.dash = true; break;
                        case "sp": input.skill = true; break;
                        case "ki": kiT = 0; break;
                    }
                }
                else if (stickId == int.MinValue && p.Position.x < UnityEngine.Screen.width * 0.45f && p.Position.y < UnityEngine.Screen.height * 0.7f)
                {
                    stickId = p.Id;
                    stickStart = p.Position;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(pads, p.Position, null, out var local);
                    stickBase.anchorMin = stickBase.anchorMax = new Vector2(0.5f, 0.5f);
                    stickBase.anchoredPosition = local;
                }
            }

            // releases
            foreach (var b in buttons)
            {
                if (b.pointer == int.MinValue) continue;
                if (now.Contains(b.pointer))
                {
                    if (b.id == "atk") input.atkDown = true;
                    continue;
                }
                b.pointer = int.MinValue;
                b.rt.localScale = Vector3.one;
                if (b.id == "ki")
                {
                    if (kiT >= 0 && kiT < 0.22f) input.kiTap = true;
                    kiT = -1;
                }
            }
            if (kiT >= 0) { kiT += dt; if (kiT >= 0.22f) input.charging = true; }
            if (stickId != int.MinValue && !now.Contains(stickId))
            {
                stickId = int.MinValue;
                stickKnob.anchoredPosition = Vector2.zero;
                stickBase.anchorMin = stickBase.anchorMax = Vector2.zero;
                stickBase.anchoredPosition = new Vector2(100, 100);
            }
            seen.Clear();
            foreach (var id in now) seen.Add(id);
        }

        bool Hit(RectTransform rt, Vector2 screenPos, float radius)
        {
            if (!rt.gameObject.activeInHierarchy) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out var local);
            return local.magnitude <= radius;
        }
    }
}
