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
        public enum Screen { None, Title, Paused, Result, Scene }

        static readonly Color Paper = CbArt.Hex("#fff6e8"), Gold = CbArt.Hex("#ffc94a"), FireCol = CbArt.Hex("#ffb03a"),
            Ember = CbArt.Hex("#ff5a2e"), Ink = new Color(0.07f, 0.09f, 0.06f, 1f), Glass = new Color(0.07f, 0.09f, 0.06f, 0.55f);

        readonly Canvas canvas;
        readonly RectTransform root;
        readonly Image hpFill, kiFill, kiFrame, hurtFlash, slowFx, speedLines, impactFlash;
        float speedT, speedDur, speedStrength, linesFlip, flashT, flashA, flashGap;
        readonly Text waveTxt, tipTxt, scoreTxt, comboN, goTxt, coinTxt;
        readonly RectTransform comboBox, pads, stickBase, stickKnob, paused, result, pauseBtn, pauseMenuBtn, resultMenuBtn;
        readonly Text resultTitle, resultStats;
        // the play HUD (hidden in a cinematic) and the cinematic's own layer: black bars, the speech box, SKIP
        readonly RectTransform play, barTop, barBottom, bubble, skipBtn;
        readonly Text whoTxt, lineTxt, nextTxt;
        // a boss's name and health, at the top in the middle
        readonly RectTransform bossBar;
        readonly Image bossFill;
        readonly Text bossName;
        string lineFull = "";
        float lineShown, barsK, blipT;
        /// <summary>How fast a line is typed into the speech box (characters per second).</summary>
        public const float TypeSpeed = 40f;
        const float BarHeight = 46f;
        readonly List<Pad> buttons = new List<Pad>();
        readonly Pad sp, kiPad;
        // the KI button: bamboo gold for the staff techniques, orange once it shoots fire
        static readonly Color KiPadCol = CbArt.Hex("#c48a2c"), FirePadCol = CbArt.Hex("#e8642a");
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
            public Text label;
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
            // the whole 844 x 390 layout always fits: on a long phone it fills the height (as it was laid out), on a
            // squarer screen (4:3: a tablet, a folding phone) it fills the width and gains height, instead of being cut
            // at the sides with the buttons covering half the screen (the user's 4:3 phone)
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = (RectTransform)go.transform;

            hurtFlash = UIKit.Image(UIKit.Stretch(root, "hurt"), UIKit.Vignette, new Color(1f, 0.2f, 0.15f, 0f));
            slowFx = UIKit.Image(UIKit.Stretch(root, "slow"), UIKit.Vignette, new Color(0.25f, 0.5f, 1f, 0f));
            // anime focus lines and a white impact frame, over the game and under the rest of the HUD
            var lines = CbVfx.SpeedLines(512);
            speedLines = UIKit.Image(UIKit.Stretch(root, "speedLines"), Sprite.Create(lines, new Rect(0, 0, 512, 512), new Vector2(0.5f, 0.5f)), Color.clear);
            speedLines.enabled = false;
            impactFlash = UIKit.Image(UIKit.Stretch(root, "impact"), null, Color.clear);
            impactFlash.enabled = false;
            play = UIKit.Stretch(root, "play");

            // status: portrait, name, bars
            // a round badge with Raja's face (rendered from his model; drawn in code if it's missing), a gold ring, a
            // drop shadow; beside it his name and two glossy bars, like a storybook platformer's
            var status = UIKit.Rect(play, "status", new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(320, 70));
            // a soft dark plate behind the name and bars, so they read over the busy jungle
            var plate = UIKit.Rect(status, "plate", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -8), new Vector2(268, 54));
            Pill(plate, new Color(0.05f, 0.07f, 0.06f, 0.42f));
            // the badge: Raja's face (rendered from his model) in a thin gold ring with a dark edge
            var portrait = UIKit.Rect(status, "portrait", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(68, 68));
            UIKit.Image(UIKit.Rect(portrait, "edge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(72, 72)), UIKit.Circle, Ink);
            UIKit.Image(portrait, UIKit.Circle, Gold);
            var face = UIKit.Rect(portrait, "face", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62, 62));
            var faceImg = face.gameObject.AddComponent<RawImage>();
            var painted = Resources.Load<Texture2D>("Cloudborne/Hud/RajaPortrait");
            faceImg.texture = painted != null ? painted : Portrait();
            faceImg.raycastTarget = false;
            UIKit.Label(status, "RAJA", 18, Paper, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -6), new Vector2(120, 20), TextAnchor.MiddleLeft, true);
            hpFill = Bar(status, new Vector2(80, -29), new Vector2(206, 13), CbArt.Hex("#5ee07e"), out _);
            kiFill = Bar(status, new Vector2(80, -48), new Vector2(160, 9), FireCol, out kiFrame);

            // stage title, wave, tip
            UIKit.Label(play, "STAGE 1-1 · THE JUNGLE GATE", 11, new Color(1, 1, 1, 0.9f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(320, 16), TextAnchor.MiddleCenter);
            waveTxt = UIKit.Label(play, "", 20, Paper, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(360, 26), TextAnchor.MiddleCenter, true);
            var tipBox = UIKit.Panel(play, "tip", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(270, 34), Glass);
            tipTxt = UIKit.Label(tipBox.transform, "", 11, Paper, TextAnchor.MiddleCenter, false, false);
            tipTxt.supportRichText = true;
            tipTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            ((RectTransform)tipTxt.transform).offsetMin = new Vector2(10, 2);
            ((RectTransform)tipTxt.transform).offsetMax = new Vector2(-10, -2);

            // score, pause
            UIKit.Label(play, "SCORE", 10, new Color(1, 1, 1, 0.8f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-76, -14), new Vector2(120, 14), TextAnchor.MiddleRight);
            scoreTxt = UIKit.Label(play, "0", 24, Paper, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-76, -28), new Vector2(160, 28), TextAnchor.MiddleRight, true);
            coinTxt = UIKit.Label(play, "COINS 0", 20, Gold, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-76, -58), new Vector2(160, 24), TextAnchor.MiddleRight, true);
            pauseBtn = UIKit.Rect(root, "pause", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -12), new Vector2(48, 48));
            UIKit.Image(pauseBtn, UIKit.Circle, Glass);
            UIKit.Image(UIKit.Rect(pauseBtn, "ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48)), UIKit.Ring, Paper);
            UIKit.Label(pauseBtn, "II", 18, Paper, TextAnchor.MiddleCenter, true, false);

            // a boss's health: his name over a long red bar, under the tip
            bossBar = UIKit.Rect(play, "boss", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -98), new Vector2(340, 34));
            bossName = UIKit.Label(bossBar, "", 14, Paper, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(340, 18), TextAnchor.MiddleCenter, true);
            var bossFrame = UIKit.Rect(bossBar, "bar", new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(340, 12));
            UIKit.Image(bossFrame, UIKit.RoundedSmall, Paper);
            var bossBg = UIKit.Rect(bossFrame, "bg", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(336, 8));
            UIKit.Image(bossBg, UIKit.RoundedSmall, new Color(0.07f, 0.09f, 0.06f, 0.85f));
            bossFill = UIKit.Image(UIKit.Rect(bossBg, "fill", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(336, 8)), null, Ember);
            bossBar.gameObject.SetActive(false);

            // combo, go
            comboBox = UIKit.Rect(play, "combo", new Vector2(1, 0.62f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(160, 100));
            comboN = UIKit.Label(comboBox, "0", 56, Gold, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(160, 64), TextAnchor.MiddleRight, true);
            UIKit.Label(comboBox, "HITS!", 20, Paper, new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 10), new Vector2(160, 26), TextAnchor.MiddleRight, true);
            comboBox.gameObject.SetActive(false);
            goTxt = UIKit.Label(play, "GO ▶", 40, Gold, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-32, 0), new Vector2(160, 50), TextAnchor.MiddleRight, true);
            goTxt.gameObject.SetActive(false);

            // touch pads
            pads = UIKit.Stretch(root, "pads");
            stickBase = UIKit.Rect(pads, "stick", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(100, 100), new Vector2(120, 120));
            UIKit.Image(stickBase, UIKit.Circle, new Color(0.07f, 0.09f, 0.06f, 0.35f));
            UIKit.Image(UIKit.Rect(stickBase, "ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120)), UIKit.Ring, new Color(1, 1, 1, 0.5f));
            stickKnob = UIKit.Rect(stickBase, "knob", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54));
            UIKit.Image(stickKnob, UIKit.Circle, new Color(0.95f, 0.93f, 0.88f, 0.95f));
            // centres measured from the bottom-right corner: the two attack buttons under the right thumb, jump and
            // guard beside them, dash, fire and the special shot above
            AddPad("atk", "HIT", new Vector2(-14 - 46, 12 + 46), 92, CbArt.Hex("#d8453f"), 22);
            AddPad("heavy", "HEAVY", new Vector2(-14 - 8 - 37, 12 + 106 + 37), 74, CbArt.Hex("#a8322e"), 16);
            AddPad("jump", "JUMP", new Vector2(-14 - 108 - 36, 12 + 36), 72, CbArt.Hex("#3aa862"), 17);
            AddPad("guard", "GUARD", new Vector2(-14 - 196 - 32, 12 + 32), 64, CbArt.Hex("#7d8a99"), 14);
            AddPad("dash", "DASH", new Vector2(-14 - 104 - 32, 12 + 86 + 32), 64, CbArt.Hex("#2f8ae0"), 15);
            // KI: the staff techniques; FIRE once Raja's fire is awake (SetBars keeps it right)
            kiPad = AddPad("ki", "KI", new Vector2(-14 - 186 - 32, 12 + 100 + 32), 64, KiPadCol, 15);
            sp = AddPad("sp", "SPECIAL", new Vector2(-14 - 60 - 30, 12 + 196 + 30), 60, CbArt.Hex("#b8401f"), 12);

            // cinematics: black bars that slide in at the top and bottom, a speech box over the lower one, SKIP
            barTop = UIKit.Rect(root, "barTop", new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(4000, 0));
            UIKit.Image(barTop, null, Color.black);
            barBottom = UIKit.Rect(root, "barBottom", new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(4000, 0));
            UIKit.Image(barBottom, null, Color.black);
            bubble = UIKit.Rect(root, "speech", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(620, 78));
            UIKit.Image(bubble, UIKit.Rounded, new Color(0.07f, 0.09f, 0.06f, 0.88f));
            UIKit.Image(UIKit.Rect(bubble, "edge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 78)), UIKit.Rounded, new Color(1, 1, 1, 0.08f));
            whoTxt = UIKit.Label(bubble, "", 15, Gold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -6), new Vector2(400, 22), TextAnchor.MiddleLeft, true);
            var lineBox = UIKit.Rect(bubble, "line", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            lineBox.anchorMin = Vector2.zero; lineBox.anchorMax = Vector2.one;
            lineBox.offsetMin = new Vector2(18, 8); lineBox.offsetMax = new Vector2(-30, -30);
            lineTxt = UIKit.Label(lineBox, "", 15, Paper, TextAnchor.UpperLeft, false, false);
            lineTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            nextTxt = UIKit.Label(bubble, "▼", 12, Gold, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 8), new Vector2(20, 16), TextAnchor.MiddleCenter);
            bubble.gameObject.SetActive(false);
            skipBtn = UIKit.Rect(root, "skip", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -8), new Vector2(100, 30));
            UIKit.Image(skipBtn, UIKit.Rounded, Glass);
            UIKit.Label(skipBtn, "SKIP ▶▶", 13, Paper, TextAnchor.MiddleCenter, true, false);
            skipBtn.gameObject.SetActive(false);

            // screens
            paused = Overlay("paused");
            UIKit.Label(paused, "PAUSED", 60, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(600, 70), TextAnchor.MiddleCenter, true);
            BigButton(paused, "RESUME", new Vector2(0, -30));
            pauseMenuBtn = (RectTransform)BigButton(paused, "MENU", new Vector2(0, -98)).transform.parent;

            result = Overlay("result");
            resultTitle = UIKit.Label(result, "STAGE CLEAR", 64, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(700, 80), TextAnchor.MiddleCenter, true);
            resultStats = UIKit.Label(result, "", 18, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(760, 64), TextAnchor.MiddleCenter, true);
            BigButton(result, "PLAY AGAIN", new Vector2(0, -50));
            resultMenuBtn = (RectTransform)BigButton(result, "MENU", new Vector2(0, -118)).transform.parent;

            SetScreen(Screen.Title);
        }

        // ---------- building blocks ----------

        /// <summary>
        /// A glossy bar: a dark outline and shadow, a white rim, a dark track, the fill lighter at its top with a shine
        /// along it. The fill's x scale is how full it is.
        /// </summary>
        /// <summary>
        /// A capsule bar: a thin dark edge, a white rim, a dark track, and the fill with round ends and a shine along
        /// its top. How full it is sets the fill's width (SetFill), so its ends stay round.
        /// </summary>
        Image Bar(RectTransform parent, Vector2 pos, Vector2 size, Color color, out Image frame)
        {
            var rt = UIKit.Rect(parent, "bar", new Vector2(0, 1), new Vector2(0, 1), pos, size);
            Pill(UIKit.Rect(rt, "edge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -0.5f), size + new Vector2(5, 5)), Ink);
            frame = Pill(rt, Paper);
            var track = UIKit.Rect(rt, "track", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(3, 3));
            Pill(track, new Color(0.1f, 0.08f, 0.13f, 1f));
            var fill = UIKit.Rect(track, "fill", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, size - new Vector2(3, 3));
            var img = Pill(fill, color);
            // the shine: a pale stripe along the fill's top, as long as the fill
            var shine = UIKit.Rect(fill, "shine", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            shine.anchorMin = new Vector2(0, 1); shine.anchorMax = new Vector2(1, 1); shine.pivot = new Vector2(0.5f, 1);
            float sh = Mathf.Max(2, (size.y - 3) * 0.36f);
            shine.offsetMin = new Vector2(sh * 0.6f, -sh - 1.2f); shine.offsetMax = new Vector2(-sh * 0.6f, -1.2f);
            Pill(shine, new Color(1, 1, 1, 0.5f), sh);
            return img;
        }

        /// <summary>How full a bar is (0 to 1): its fill's width, so the round ends keep their shape.</summary>
        static void SetFill(Image fill, float amount)
        {
            amount = Mathf.Clamp01(amount);
            var rt = fill.rectTransform;
            float full = ((RectTransform)rt.parent).sizeDelta.x;
            rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.y, full * amount), rt.sizeDelta.y);
            fill.enabled = amount > 0.005f;
        }

        static Sprite pill;

        /// <summary>A capsule: round ends whatever its length, for bars and their parts.</summary>
        static Image Pill(RectTransform rt, Color color, float height = 0)
        {
            if (pill == null)
            {
                const int S = 64;
                var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = Mathf.Max(Mathf.Abs(x + 0.5f - S / 2f) - 0.01f, 0), dy = Mathf.Abs(y + 0.5f - S / 2f);
                        t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(S / 2f - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f)));
                    }
                t.Apply();
                pill = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(S / 2f - 1, S / 2f - 1, S / 2f - 1, S / 2f - 1));
            }
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = pill;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            // the ends' radius: half the height
            float h = height > 0 ? height : rt.sizeDelta.y;
            img.pixelsPerUnitMultiplier = 64f / Mathf.Max(1f, h);
            return img;
        }

        Pad AddPad(string id, string label, Vector2 centre, float size, Color color, int font)
        {
            var rt = UIKit.Rect(pads, id, new Vector2(1, 0), new Vector2(0.5f, 0.5f), centre, new Vector2(size, size));
            UIKit.Image(UIKit.Rect(rt, "shadow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(size, size)), UIKit.Circle, Ink);
            var img = UIKit.Image(UIKit.Rect(rt, "fill", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size)), UIKit.Circle, color);
            UIKit.Image(UIKit.Rect(rt, "ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size)), UIKit.Ring, Paper);
            var text = UIKit.Label(rt, label, font, Paper, TextAnchor.MiddleCenter, true, true);
            var p = new Pad { id = id, rt = rt, img = img, radius = size * 0.5f, label = text };
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
            paused.gameObject.SetActive(s == Screen.Paused);
            result.gameObject.SetActive(s == Screen.Result);
            bool playing = s == Screen.None;
            pads.gameObject.SetActive(playing && ShowPads);
            pauseBtn.gameObject.SetActive(playing);
            // the main menu draws itself over the title; the play HUD only shows in the stage
            play.gameObject.SetActive(s != Screen.Scene && s != Screen.Title);
            skipBtn.gameObject.SetActive(s == Screen.Scene);
            if (s != Screen.Scene) HideLine();
        }

        // ---------- cinematics ----------

        /// <summary>A line in the speech box, typed in: who speaks (in their colour) and what they say.</summary>
        public void Say(string who, Color color, string line)
        {
            bubble.gameObject.SetActive(true);
            whoTxt.text = who;
            whoTxt.color = color;
            lineFull = line ?? "";
            lineShown = 0;
            lineTxt.text = "";
            nextTxt.enabled = false;
            bubble.localScale = new Vector3(1, 0.85f, 1);
        }

        /// <summary>The whole line is in the box.</summary>
        public bool LineDone => lineShown >= lineFull.Length;

        /// <summary>Shows the rest of the line at once (a tap while it is being typed).</summary>
        public void CompleteLine() => lineShown = lineFull.Length;

        public void HideLine()
        {
            lineFull = "";
            lineShown = 0;
            if (bubble != null) bubble.gameObject.SetActive(false);
        }

        /// <summary>The touch buttons: on phones, unless the settings say always or never.</summary>
        public static bool ShowPads => CbSave.Pads == 1 || (CbSave.Pads == 0 && Application.isMobilePlatform);

        /// <summary>The results: score, combo, time; tiger tokens, friends freed, stars (and a badge for no hit taken).</summary>
        public void ShowResult(bool won, int score, int maxCombo, float time, int tokens = 0, int friends = 0, int stars = 0, bool flawless = false)
        {
            resultTitle.text = won ? "STAGE CLEAR  " + new string('★', stars) + new string('☆', 3 - stars) : "DEFEATED";
            resultStats.text = $"SCORE {score:N0}    ·    MAX COMBO {maxCombo}    ·    TIME {(int)(time / 60)}:{(int)(time % 60):00}\n" +
                $"TIGER TOKENS {tokens}/3    ·    FRIENDS FREED {friends}/3" + (flawless ? "    ·    NO HIT TAKEN!" : "");
            SetScreen(Screen.Result);
        }

        public void SetWave(string s) => waveTxt.text = s;
        public void SetTip(string s) { tipTxt.text = s; tipTxt.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(s)); }
        public void ShowGo(bool on) => goTxt.gameObject.SetActive(on);

        public void SetBars(float hp, float ki)
        {
            SetFill(hpFill, hp / 100f);
            SetFill(kiFill, ki / 100f);
            bool ready = ki >= 50 && CbHero.SpecialUnlocked;
            bool fire = CbHero.FireAwake;
            if (kiPad.label.text != (fire ? "FIRE" : "KI"))
            {
                kiPad.label.text = fire ? "FIRE" : "KI";
                kiPad.img.color = fire ? FirePadCol : KiPadCol;
            }
            kiFrame.color = ready ? Color.Lerp(Paper, FireCol, 0.5f + 0.5f * Mathf.Sin(Time.time * 8)) : Paper;
            sp.rt.localScale = Vector3.one * (ready ? 1f + 0.04f * Mathf.Sin(Time.time * 7) : 1f);
            sp.img.color = ready ? CbArt.Hex("#d9541f") : new Color(0.72f, 0.25f, 0.12f, 0.45f);
        }

        public void SetScore(int s) => scoreTxt.text = s.ToString("N0");

        /// <summary>A boss's name and a full health bar.</summary>
        public void ShowBoss(string name)
        {
            bossName.text = name;
            bossBar.gameObject.SetActive(true);
            SetBoss(1);
        }

        public void SetBoss(float left) => bossFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(left), 1, 1);

        public void HideBoss() => bossBar.gameObject.SetActive(false);

        /// <summary>Tiger coins picked up this run.</summary>
        public void SetCoins(int n) => coinTxt.text = "COINS " + n;

        public void SetCombo(int n)
        {
            comboBox.gameObject.SetActive(n >= 2);
            comboN.text = n.ToString();
            if (n >= 2) comboPop = 0.18f;
        }

        public void FlashHurt() => hurtT = 0.25f;

        /// <summary>Anime focus lines for a moment (dash, heavy blows, the roar); called every frame, it holds them on.</summary>
        public void SpeedLines(float strength, float time)
        {
            float now = speedDur > 0 ? speedStrength * Mathf.Clamp01(speedT / speedDur) : 0;
            if (strength < now) return;
            speedStrength = strength;
            speedT = speedDur = time;
        }

        /// <summary>
        /// A white frame for an instant: a finishing blow, a guard break, the roar. Never more than about three a
        /// second, and never full white, so a busy fight doesn't strobe.
        /// </summary>
        public void ImpactFlash(float strength)
        {
            if (flashGap > 0) return;
            flashGap = 0.3f;
            flashT = 0.08f;
            flashA = Mathf.Min(strength, 0.45f);
        }
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
            speedT = Mathf.Max(0, speedT - dt);
            speedLines.enabled = speedT > 0;
            if (speedT > 0)
            {
                speedLines.color = new Color(1, 1, 1, speedStrength * Mathf.Sqrt(speedT / speedDur));
                linesFlip -= dt;
                if (linesFlip <= 0)
                {
                    // flicker by mirroring: always covers the screen, unlike turning it
                    linesFlip = 0.05f;
                    float s = Random.Range(1.05f, 1.2f);
                    speedLines.rectTransform.localScale = new Vector3(Random.value < 0.5f ? s : -s, Random.value < 0.5f ? s : -s, 1);
                }
            }
            flashT = Mathf.Max(0, flashT - dt);
            flashGap = Mathf.Max(0, flashGap - dt);
            impactFlash.enabled = flashT > 0;
            if (flashT > 0) impactFlash.color = new Color(1, 1, 1, flashA * flashT / 0.08f);

            // the black bars slide in for a cinematic and out after it; the line types in with a soft blip
            barsK = Mathf.MoveTowards(barsK, screen == Screen.Scene ? 1 : 0, dt * 4f);
            float bar = BarHeight * Mathf.SmoothStep(0, 1, barsK);
            barTop.sizeDelta = new Vector2(4000, bar);
            barBottom.sizeDelta = new Vector2(4000, bar);
            if (bubble.gameObject.activeSelf)
            {
                bubble.localScale = Vector3.MoveTowards(bubble.localScale, Vector3.one, dt * 2f);
                if (lineShown < lineFull.Length)
                {
                    int before = (int)lineShown;
                    lineShown = Mathf.Min(lineFull.Length, lineShown + dt * TypeSpeed);
                    lineTxt.text = lineFull.Substring(0, (int)lineShown);
                    blipT -= dt;
                    if ((int)lineShown != before && blipT <= 0) { blipT = 0.07f; CbFx.Sfx("talk", 0.25f); }
                }
                else if (lineTxt.text.Length != lineFull.Length) lineTxt.text = lineFull;
                nextTxt.enabled = LineDone && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.55f;
            }
        }

        // ---------- touch ----------

        readonly HashSet<int> seen = new HashSet<int>();
        readonly HashSet<int> now = new HashSet<int>();
        int stickId = int.MinValue;
        Vector2 stickStart;

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
                    // in a cinematic SKIP jumps to its end (as pause does); a tap anywhere else moves the talk on
                    // the main menu reads its own taps; MENU on pause and results goes back to it
                    if (!fresh || screen == Screen.Title) continue;
                    if (screen == Screen.Scene && RectTransformUtility.RectangleContainsScreenPoint(skipBtn, p.Position, null)) input.pause = true;
                    else if ((screen == Screen.Paused && RectTransformUtility.RectangleContainsScreenPoint(pauseMenuBtn, p.Position, null)) ||
                             (screen == Screen.Result && RectTransformUtility.RectangleContainsScreenPoint(resultMenuBtn, p.Position, null))) input.menu = true;
                    else input.confirm = true;
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
                        case "ki": input.kiTap = true; break;
                        case "heavy": input.heavy = true; break;
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
                    if (b.id == "heavy") input.heavyDown = true;
                    if (b.id == "guard") input.guard = true;
                    continue;
                }
                b.pointer = int.MinValue;
                b.rt.localScale = Vector3.one;
            }
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
