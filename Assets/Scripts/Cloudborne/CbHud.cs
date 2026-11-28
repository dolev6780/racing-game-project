using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cloudborne
{
    /// <summary>
    /// Screen layer: health and ki bars, stage title and tips, score, combo counter, banners and damage numbers,
    /// the touch controls (a floating stick for left and right; JUMP, ATTACK, SKILL and SPECIAL; swipes on the right of
    /// the screen: down a slide, across a dodge) and the title / pause / result screens.
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
        readonly Text waveTxt, tipTxt, scoreTxt, comboN, goTxt, coinTxt, stageTxt, foeLeft, foeRight;
        float foeT;
        readonly RectTransform comboBox, pads, stickBase, stickKnob, paused, result, pauseBtn, pauseMenuBtn, resultMenuBtn;
        readonly Text resultTitle, resultStats, fpsTxt, perfTxt, trialTxt, resultMissions;
        /// <summary>A time trial's clock (empty: none running).</summary>
        public void SetTrial(string s) { trialTxt.text = s; }
        /// <summary>The frame counter at the bottom of the screen while playing (the user: an APK with an FPS counter,
        /// to see how the phone keeps up): frames a second, the slowest frame of the last second, the mean frame's
        /// time, the quality level.</summary>
        public static bool ShowFps = true;
        float perfT;
        // the play HUD (hidden in a cinematic) and the cinematic's own layer: black bars, the speech box, SKIP
        readonly RectTransform play, barTop, barBottom, bubble, skipBtn;
        readonly Text whoTxt, lineTxt, nextTxt;
        // a boss's name and health, at the top in the middle
        readonly RectTransform bossBar, breakBar;
        readonly Image bossFill, breakFill;
        readonly Text bossName;
        string lineFull = "";
        float lineShown, barsK, blipT;
        /// <summary>How fast a line is typed into the speech box (characters per second).</summary>
        public const float TypeSpeed = 40f;
        const float BarHeight = 46f;
        readonly List<Pad> buttons = new List<Pad>();
        readonly Pad kiPad, sp, atkPad;
        // The controls' hints (text alone didn't show a phone player what a swipe was): a finger sliding down or
        // across in the swipe area, or HOLD pulsing over ATTACK, while a tip names that move, until it is done once.
        public enum Hint { None = 0, SwipeAcross = 2, Hold = 4, StickUp = 8, StickDown = 16 }
        readonly RectTransform gesture, finger;
        readonly Text gestureTxt, holdTxt;
        readonly Image[] fingerImgs;
        Hint hint;
        // the swipe's note under the pads: hidden while its own hint shows on the same row (it read twice)
        GameObject swipeNote;
        float hintT;
        // the SKILL button: bamboo gold for the staff techniques, orange once it shoots fire
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
            BuildCutIn();
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
            stageTxt = UIKit.Label(play, CbStage.Title, 11, new Color(1, 1, 1, 0.9f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(320, 16), TextAnchor.MiddleCenter);
            waveTxt = UIKit.Label(play, "", 20, Paper, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(360, 26), TextAnchor.MiddleCenter, true);
            var tipBox = UIKit.Panel(play, "tip", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(270, 34), Glass);
            tipTxt = UIKit.Label(tipBox.transform, "", 11, Paper, TextAnchor.MiddleCenter, false, false);
            tipTxt.supportRichText = true;
            tipTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            ((RectTransform)tipTxt.transform).offsetMin = new Vector2(10, 2);
            ((RectTransform)tipTxt.transform).offsetMax = new Vector2(-10, -2);

            // score, pause
            UIKit.Label(play, "SCORE", 10, new Color(1, 1, 1, 0.8f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-76, -14), new Vector2(120, 14), TextAnchor.MiddleRight);
            // a time trial's clock under the stage's title (CbTrials)
            trialTxt = UIKit.Label(play, "", 22, Gold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(420, 28), TextAnchor.MiddleCenter, true);
            perfTxt = UIKit.Label(play, "", 13, new Color(1, 1, 1, 0.85f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(420, 18), TextAnchor.MiddleCenter, true);
            scoreTxt = UIKit.Label(play, "0", 24, Paper, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-76, -28), new Vector2(160, 28), TextAnchor.MiddleRight, true);
            coinTxt = UIKit.Label(play, "COINS 0", 20, Gold, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-76, -58), new Vector2(160, 24), TextAnchor.MiddleRight, true);
            // (they pop about their right ends, where the right-aligned numbers are)
            scoreTxt.rectTransform.pivot = coinTxt.rectTransform.pivot = new Vector2(1, 0.5f);
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
            // Sergeant Snap's break gauge, under his health: filling yellow; broken, it flashes and drains as his daze wears off
            breakBar = UIKit.Rect(bossBar, "break", new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -3), new Vector2(240, 8));
            UIKit.Image(breakBar, UIKit.RoundedSmall, new Color(0.07f, 0.09f, 0.06f, 0.85f));
            breakFill = UIKit.Image(UIKit.Rect(breakBar, "fill", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(236, 4)), null, Gold);
            breakBar.gameObject.SetActive(false);
            bossBar.gameObject.SetActive(false);

            // combo, go
            comboBox = UIKit.Rect(play, "combo", new Vector2(1, 0.62f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(160, 100));
            comboN = UIKit.Label(comboBox, "0", 56, Gold, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(160, 64), TextAnchor.MiddleRight, true);
            UIKit.Label(comboBox, "HITS!", 20, Paper, new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 10), new Vector2(160, 26), TextAnchor.MiddleRight, true);
            comboBox.gameObject.SetActive(false);
            // (above the swipe's note and the KI button: in the middle it lay over "SWIPE ↔ dodge" - the survey's pictures)
            goTxt = UIKit.Label(play, "GO ▶", 40, Gold, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-32, 78), new Vector2(160, 50), TextAnchor.MiddleRight, true);
            goTxt.gameObject.SetActive(false);
            // a fight's soldiers out of view: an arrow at that side of the screen, pulsing (above the stick and the buttons)
            foeLeft = UIKit.Label(play, "◀ !", 54, FoeArrow, new Vector2(0, 0.7f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(140, 70), TextAnchor.MiddleLeft, true);
            foeRight = UIKit.Label(play, "! ▶", 54, FoeArrow, new Vector2(1, 0.7f), new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(140, 70), TextAnchor.MiddleRight, true);
            foeLeft.gameObject.SetActive(false);
            foeRight.gameObject.SetActive(false);

            // touch pads
            pads = UIKit.Stretch(root, "pads");
            stickBase = UIKit.Rect(pads, "stick", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(100, 100), new Vector2(120, 120));
            UIKit.Image(stickBase, UIKit.Circle, new Color(0.07f, 0.09f, 0.06f, 0.35f));
            UIKit.Image(UIKit.Rect(stickBase, "ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120)), UIKit.Ring, new Color(1, 1, 1, 0.5f));
            stickKnob = UIKit.Rect(stickBase, "knob", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54));
            UIKit.Image(stickKnob, UIKit.Circle, new Color(0.95f, 0.93f, 0.88f, 0.95f));
            // four buttons under the right thumb (seven, and their combinations, were too much on a phone: the user;
            // the JUMP button went for a while - the stick jumped - and came back: the user), centres measured from the
            // bottom-right corner: ATTACK in the corner (held: the staff), JUMP beside it, SKILL above ATTACK - KI, FIRE
            // once Raja's fire is awake (SetBars keeps it right) - and SPECIAL above JUMP, a button of its own (the
            // user), lit when ki is high enough. The rest of the right side takes swipes: across, a dodge; down, a slide.
            atkPad = AddPad("atk", "ATTACK", new Vector2(-14 - 52, 12 + 52), 104, CbArt.Hex("#d8453f"), 17);
            AddPad("jump", "JUMP", new Vector2(-14 - 124 - 44, 12 + 44), 88, CbArt.Hex("#3aa862"), 17);
            kiPad = AddPad("ki", "KI", new Vector2(-14 - 40 - 38, 12 + 124 + 38), 76, KiPadCol, 15);
            sp = AddPad("sp", "SPECIAL", new Vector2(-14 - 150 - 34, 12 + 112 + 34), 68, CbArt.Hex("#b8401f"), 12);
            swipeNote = UIKit.Label(pads, "SWIPE  ↔ dodge", 13, new Color(1, 1, 1, 0.55f), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-14, 12 + 214), new Vector2(260, 20), TextAnchor.MiddleRight, true).transform.parent.gameObject;
            UIKit.Label(pads, "STICK  ↓ slide", 13, new Color(1, 1, 1, 0.55f), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(24, 12 + 176), new Vector2(260, 20), TextAnchor.MiddleLeft, true);
            // the hints: a finger in the swipe area over the buttons, HOLD over ATTACK
            gesture = UIKit.Rect(pads, "gesture", new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-170, 330), new Vector2(160, 160));
            gestureTxt = UIKit.Label(gesture, "", 15, Gold, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -24), new Vector2(200, 22), TextAnchor.MiddleCenter, true);
            finger = UIKit.Rect(gesture, "finger", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
            UIKit.Image(finger, UIKit.Circle, new Color(1, 1, 1, 0.85f));
            UIKit.Image(UIKit.Rect(finger, "ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44)), UIKit.Ring, Gold);
            fingerImgs = finger.GetComponentsInChildren<Image>();
            gesture.gameObject.SetActive(false);
            holdTxt = UIKit.Label(atkPad.rt, "HOLD", 14, Gold, new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(120, 20), TextAnchor.MiddleCenter, true);
            holdTxt.gameObject.SetActive(false);
            // the special's cut-in over the play screen and the pads' notes (under the cinematics and the menus)
            cutIn.SetAsLastSibling();

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
            // how the phone keeps up (frames a second over the last second before the pause)
            fpsTxt = UIKit.Label(paused, "", 16, new Color(1, 1, 1, 0.7f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(600, 24), TextAnchor.MiddleCenter, true);

            result = Overlay("result");
            resultTitle = UIKit.Label(result, "STAGE CLEAR", 60, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 130), new Vector2(700, 76), TextAnchor.MiddleCenter, true);
            for (int i = 0; i < 3; i++)
                resultStars[i] = UIKit.Label(result, "★", 50, Gold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 60, 76), new Vector2(64, 60), TextAnchor.MiddleCenter, true);
            resultStats = UIKit.Label(result, "", 18, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(760, 64), TextAnchor.MiddleCenter, true);
            // the rank, stamped in at the right once the rest has counted up
            resultRank = UIKit.Label(result, "", 116, Gold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300, 92), new Vector2(170, 150), TextAnchor.MiddleCenter, true);
            resultRank.fontStyle = FontStyle.Italic;
            rankOutline = resultRank.gameObject.AddComponent<Outline>();
            rankOutline.effectDistance = new Vector2(4, -4);
            // the stage's hidden missions, left of the buttons (CbGame.Missions; the canvas is 844 x 390)
            resultMissions = UIKit.Label(result, "", 11, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-226, -88), new Vector2(214, 104), TextAnchor.UpperLeft, true);
            resultMissions.supportRichText = true;
            resultRankLabel = UIKit.Label(result, "RANK", 16, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300, 158), new Vector2(120, 20), TextAnchor.MiddleCenter, true);
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
            if (s == Screen.Paused) fpsTxt.text = $"{Mathf.RoundToInt(CbGame.Fps)} FPS";
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

        /// <summary>
        /// The touch buttons: on phones and in the Unity editor (to try them there with the mouse and the keyboard: the
        /// user), unless the settings say always or never.
        /// </summary>
        public static bool ShowPads => CbSave.Pads == 1 || (CbSave.Pads == 0 && (Application.isMobilePlatform || Application.isEditor));

        /// <summary>The results: score, combo, time; tiger tokens, friends freed, stars (and a badge for no hit taken).</summary>
        public void ShowResult(bool won, int score, int maxCombo, float time, int tokens = 0, int friends = 0, int stars = 0, bool flawless = false, int letters = 0, string missions = "")
        {
            resultMissions.text = missions;
            Missions = missions;
            res = (won, score, maxCombo, time, tokens, friends, stars, flawless);
            resLetters = letters;
            resultTitle.text = won ? "STAGE CLEAR" : "DEFEATED";
            Rank = won ? RankOf(stars, flawless) : "";
            resultRank.text = Rank;
            var (fill, edge) = Rank == "S" ? (Gold, CbArt.Hex("#a3200c")) : Rank == "A" ? (Ember, CbArt.Hex("#4a0f05"))
                : Rank == "B" ? (CbArt.Hex("#5ab4ff"), CbArt.Hex("#0d2a5c")) : (CbArt.Hex("#c9c4b8"), CbArt.Hex("#2b2a26"));
            resultRank.color = fill;
            rankOutline.effectColor = edge;
            resultRankLabel.gameObject.SetActive(won);
            resultT = 0;
            SetScreen(Screen.Result);
            TickResult(0);
        }

        /// <summary>
        /// The stage's rank: S for every star with no hit taken, A for every star (or two and no hit), B for two, C else.
        /// </summary>
        public static string RankOf(int stars, bool flawless) =>
            stars >= 3 && flawless ? "S" : stars >= 3 || (stars == 2 && flawless) ? "A" : stars == 2 ? "B" : "C";

        /// <summary>The missions' lines the results show (for the tests).</summary>
        public string Missions { get; private set; } = "";

        /// <summary>The rank the results show ("" when defeated).</summary>
        public string Rank { get; private set; } = "";

        // The results come in like an anime game's (the user's reference): the title slams down, the stars pop in one
        // by one, the numbers count up, and the rank is stamped in last.
        readonly Text[] resultStars = new Text[3];
        Text resultRank, resultRankLabel;
        Outline rankOutline;
        (bool won, int score, int combo, float time, int tokens, int friends, int stars, bool flawless) res;
        float resultT = -1;
        int resLetters;
        const float StarsAt = 0.3f, StarGap = 0.2f, CountFrom = 0.15f, CountTime = 0.9f, RankAt = 1.3f;

        /// <summary>The results have finished coming in (the rank is down).</summary>
        public bool ResultsIn => resultT >= RankAt + 0.2f;

        static float Ease(float k) { k = Mathf.Clamp01(k); return 1 - (1 - k) * (1 - k) * (1 - k); }

        void TickResult(float dt)
        {
            if (resultT < 0 || screen != Screen.Result) return;
            float before = resultT;
            resultT += dt;
            float t = resultT;
            // the title slams down
            float k = Ease(t / 0.18f);
            resultTitle.transform.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, k);
            resultTitle.color = new Color(Paper.r, Paper.g, Paper.b, Mathf.Clamp01(t / 0.12f));
            // the stars pop in, the earned ones gold with a chime, the rest dim
            for (int i = 0; i < 3; i++)
            {
                float at = StarsAt + i * StarGap, u = (t - at) / 0.16f;
                bool earned = i < res.stars && res.won;
                var star = resultStars[i];
                star.gameObject.SetActive(res.won);
                star.color = earned ? Gold : new Color(0.3f, 0.3f, 0.32f, 0.85f);
                star.transform.localScale = Vector3.one * (u < 0 ? 0f : earned ? 1f + 1.1f * (1 - Ease(u)) * (1 - Ease(u)) : Ease(u));
                if (earned && before < at && t >= at) CbFx.Sfx("pickup", 0.5f);
            }
            // the numbers count up
            if (before < CountFrom + CountTime + 0.05f)
            {
                float c = Ease((t - CountFrom) / CountTime);
                float time = res.time * c;
                resultStats.text = $"SCORE {Mathf.RoundToInt(res.score * c):N0}    ·    MAX COMBO {Mathf.RoundToInt(res.combo * c)}    ·    TIME {(int)(time / 60)}:{(int)(time % 60):00}\n" +
                    $"{(CbStage.River ? "GOLDEN LOTUSES" : "TIGER TOKENS")} {res.tokens}/3    ·    FRIENDS FREED {res.friends}/3" + (CbStage.Jungle ? $"    ·    R-A-J-A {resLetters}/4" : "") + (res.flawless ? "    ·    NO HIT TAKEN!" : "") + (CbSave.Hard ? "    ·    <color=#ff6a3a>HARD</color>" : "");
            }
            // the rank, stamped in with a thud
            float r = (t - RankAt) / 0.14f;
            resultRank.transform.localScale = Vector3.one * (r < 0 ? 0f : Mathf.Lerp(3f, 1f, Ease(r)));
            resultRank.transform.localRotation = Quaternion.Euler(0, 0, -8f);
            resultRankLabel.color = new Color(Paper.r, Paper.g, Paper.b, Mathf.Clamp01(r));
            // the missions come in with it
            resultMissions.color = new Color(Paper.r, Paper.g, Paper.b, Mathf.Clamp01(r));
            if (res.won && before < RankAt && t >= RankAt) CbFx.Sfx("heavy", 0.8f);
        }

        public void SetWave(string s) { waveTxt.text = s; stageTxt.text = CbStage.Title; }
        public void SetTip(string s)
        {
            tipTxt.text = s;
            tipTxt.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(s));
            // a tip naming a move of the controls shows its hint, until it has been done once
            var want = Hint.None;
            if (!string.IsNullOrEmpty(s))
            {
                string low = s.ToLowerInvariant();
                if (low.Contains("stick ↓")) want = Hint.StickDown;
                else if (low.Contains("stick ↑")) want = Hint.StickUp;
                else if (low.Contains("swipe ↔")) want = Hint.SwipeAcross;
                else if (low.Contains("hold <color=#ffb03a>attack") || low.Contains("hold it for the staff") || low.Contains("holding <color=#ffb03a>attack")) want = Hint.Hold;
            }
            if (want != Hint.None && (CbSave.Learned & (int)want) != 0) want = Hint.None;
            ShowHint(want);
        }

        /// <summary>The hint shown now.</summary>
        public Hint ShownHint => hint;

        void ShowHint(Hint h)
        {
            hint = ShowPads ? h : Hint.None;
            hintT = 0;
            bool stick = hint == Hint.StickUp || hint == Hint.StickDown;
            gesture.gameObject.SetActive(stick || hint == Hint.SwipeAcross);
            holdTxt.gameObject.SetActive(hint == Hint.Hold);
            if (swipeNote != null) swipeNote.SetActive(hint != Hint.SwipeAcross);
            gestureTxt.text = hint == Hint.StickUp ? "STICK ↑" : hint == Hint.StickDown ? "STICK ↓" : hint == Hint.SwipeAcross ? "SWIPE ↔" : "";
            // the stick's hints over the stick (bottom left), the swipe's in the swipe area (right)
            gesture.anchorMin = gesture.anchorMax = stick ? Vector2.zero : new Vector2(1, 0);
            gesture.anchoredPosition = stick ? new Vector2(110, 130) : new Vector2(-170, 330);
        }

        /// <summary>The player did a move of the controls: its hint goes, for good.</summary>
        public void Did(Hint h)
        {
            if ((CbSave.Learned & (int)h) == 0) CbSave.Learned |= (int)h;
            if (hint == h) ShowHint(Hint.None);
        }
        public void ShowGo(bool on) => goTxt.gameObject.SetActive(on);

        static readonly Color FoeArrow = CbArt.Hex("#ff5a3a");

        /// <summary>The arrows for a fight's soldiers out of view at either side.</summary>
        public void SetFoeArrows(bool left, bool right)
        {
            if (foeLeft.gameObject.activeSelf != left) foeLeft.gameObject.SetActive(left);
            if (foeRight.gameObject.activeSelf != right) foeRight.gameObject.SetActive(right);
        }

        /// <summary>Test hook: which of the arrows for soldiers out of view show.</summary>
        public bool FoeArrowLeft => foeLeft.gameObject.activeSelf;
        public bool FoeArrowRight => foeRight.gameObject.activeSelf;

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
            if (sp.pointer == int.MinValue) sp.rt.localScale = Vector3.one * (ready ? 1f + 0.05f * Mathf.Sin(Time.time * 7) : 1f);
            sp.img.color = ready ? CbArt.Hex("#d9541f") : new Color(0.72f, 0.25f, 0.12f, 0.45f);
        }

        /// <summary>What SKILL says it does: "KI" or "FIRE".</summary>
        public string SkillLabel => kiPad.label.text;
        /// <summary>Whether the SPECIAL button is lit (ki high enough).</summary>
        public bool SpecialLit => sp.img.color.a > 0.9f;

        // The score counts up to a new total rather than jumping to it, and the counters pop as they rise (a new Mario's
        // counters: the user's reference)
        int scoreTarget;
        float scoreShown, scorePop, coinPop;
        const float CounterPop = 0.16f;

        public void SetScore(int s)
        {
            if (s > scoreTarget) scorePop = CounterPop;
            scoreTarget = s;
            // (back to nought at a new run: at once)
            if (s < scoreShown) { scoreShown = s; scoreTxt.text = s.ToString("N0"); }
        }

        /// <summary>The score the counter shows now (it counts up to the game's).</summary>
        public int ScoreShown => Mathf.RoundToInt(scoreShown);

        void TickCounters(float dt)
        {
            if (scoreShown < scoreTarget)
            {
                // most of the way in about a fifth of a second, never slower than 400 a second
                scoreShown = Mathf.Min(scoreTarget, scoreShown + Mathf.Max(400f * dt, (scoreTarget - scoreShown) * (1 - Mathf.Exp(-12f * dt))));
                scoreTxt.text = Mathf.RoundToInt(scoreShown).ToString("N0");
            }
            scorePop = Mathf.Max(0, scorePop - dt);
            coinPop = Mathf.Max(0, coinPop - dt);
            float sp = scorePop / CounterPop, cp = coinPop / CounterPop;
            scoreTxt.transform.localScale = Vector3.one * (1 + 0.28f * sp * sp);
            coinTxt.transform.localScale = Vector3.one * (1 + 0.35f * cp * cp);
            coinTxt.color = Color.Lerp(Gold, Color.white, cp);
        }

        /// <summary>A boss's name and a full health bar.</summary>
        public void ShowBoss(string name, bool breakGauge = false)
        {
            bossName.text = name;
            bossBar.gameObject.SetActive(true);
            breakBar.gameObject.SetActive(breakGauge);
            SetBoss(1);
            SetBossBreak(0, false);
        }

        /// <summary>The boss's break gauge (0 to 1); broken, it flashes white and gold as it drains.</summary>
        public void SetBossBreak(float v, bool broken)
        {
            breakFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(v), 1, 1);
            breakFill.color = broken ? Color.Lerp(Gold, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f)) : Gold;
        }

        public void SetBoss(float left) => bossFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(left), 1, 1);

        public void HideBoss() => bossBar.gameObject.SetActive(false);

        /// <summary>Tiger coins picked up this run.</summary>
        public void SetCoins(int n)
        {
            if (coinTxt.text != "COINS " + n && n > 0) coinPop = CounterPop;
            coinTxt.text = "COINS " + n;
        }

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
        // ---------- the special's cut-in (the user's Sparking Zero): a dark band across the screen, Raja's eyes, the name ----------

        RectTransform cutIn, cutFace, cutName;
        Image cutEdge, cutSlash;
        Text cutText;
        Outline cutOutline;
        readonly List<(RectTransform rt, float speed)> cutStreaks = new List<(RectTransform, float)>();
        float cutT = -1;
        /// <summary>How long the cut-in shows, in real time.</summary>
        public const float CutInTime = 0.95f;
        const float CutInWide = 1500f, CutInHigh = 118f, CutFaceX = -250f, CutNameX = 190f;

        /// <summary>The cut-in is on the screen.</summary>
        public bool CuttingIn => cutT >= 0;

        void BuildCutIn()
        {
            // tilted a little, above the middle; a glowing edge behind the band, the band masking what is on it
            cutIn = UIKit.Rect(root, "cutIn", new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CutInWide, CutInHigh));
            cutIn.localRotation = Quaternion.Euler(0, 0, 5f);
            cutEdge = UIKit.Image(UIKit.Rect(cutIn, "edge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CutInWide, CutInHigh + 10)), null, Ember);
            var band = UIKit.Rect(cutIn, "band", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CutInWide, CutInHigh));
            // (opaque, as the slant's wedge over his face is: a see-through band showed the wedge as a darker block)
            UIKit.Image(band, null, new Color(0.06f, 0.035f, 0.03f, 1f));
            band.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            // streaks of light racing across it
            for (int i = 0; i < 9; i++)
            {
                var st = UIKit.Rect(band, "streak", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(Random.Range(-CutInWide / 2, CutInWide / 2), Random.Range(-CutInHigh / 2 + 6, CutInHigh / 2 - 6)),
                    new Vector2(Random.Range(160f, 520f), Random.Range(2f, 6f)));
                UIKit.Image(st, null, new Color(1f, Random.Range(0.45f, 0.8f), 0.25f, Random.Range(0.25f, 0.6f)));
                cutStreaks.Add((st, Random.Range(1800f, 3200f)));
            }
            // his face, large: the band crops it to his eyes (the painted badge: its eyes sit 41% up, a little left)
            cutFace = UIKit.Rect(band, "face", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(CutFaceX, 0), new Vector2(300, 300));
            var face = cutFaceImg = cutFace.gameObject.AddComponent<RawImage>();
            var painted = Resources.Load<Texture2D>("Cloudborne/Hud/RajaPortrait");
            face.texture = cutPortrait = painted != null ? painted : Portrait();
            face.uvRect = PortraitEyes;
            face.raycastTarget = false;
            cutFace.sizeDelta = new Vector2(CutFaceWide, CutInHigh * 2f);
            // its right side cut on a slant, with a bright line along the cut
            var wedge = UIKit.Rect(cutFace, "wedge", new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(-24, 0), new Vector2(160, CutInHigh * 1.6f));
            wedge.localRotation = Quaternion.Euler(0, 0, -14f);
            UIKit.Image(wedge, null, new Color(0.06f, 0.035f, 0.03f, 1f));
            var slash = UIKit.Rect(cutFace, "slash", new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24, 0), new Vector2(6, CutInHigh * 1.6f));
            slash.localRotation = Quaternion.Euler(0, 0, -14f);
            cutSlash = UIKit.Image(slash, null, Ember);
            // the move's name
            cutName = UIKit.Rect(band, "name", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(CutNameX, 0), new Vector2(560, CutInHigh));
            cutText = UIKit.Label(cutName, "", 40, Paper, TextAnchor.MiddleCenter, true);
            cutText.fontStyle = FontStyle.Italic;
            cutOutline = cutText.gameObject.AddComponent<Outline>();
            cutOutline.effectDistance = new Vector2(3, -3);
            cutIn.gameObject.SetActive(false);
        }

        /// <summary>The special's cut-in: the band sweeps in with Raja's eyes and the move's name, holds, and closes.</summary>
        public void CutIn(string name, bool fire) =>
            CutIn(name, fire ? Ember : Gold, fire ? CbArt.Hex("#a3200c") : CbArt.Hex("#7a5410"), null);

        // Raja's eyes on the painted badge: 41% up it, a little left; the band shows the middle half of this
        static readonly Rect PortraitEyes = new Rect(0.06f, 0.16f, 0.76f, 0.5f);
        const float CutFaceWide = CutInHigh * 2f * 0.76f / 0.5f;
        RawImage cutFaceImg;
        Texture cutPortrait;

        /// <summary>The shape of a close-up for the band (width over height): a camera's picture of a boss's face.</summary>
        public const float CloseUpAspect = CutFaceWide / CutInHigh;

        /// <summary>
        /// A cut-in in these colours; face, when given, is a close-up (CloseUpAspect wide to one high) in place of
        /// Raja's eyes - a boss's own face (CbGame.BossCutIn).
        /// </summary>
        public void CutIn(string name, Color edge, Color outline, Texture face)
        {
            cutFaceImg.texture = face != null ? face : cutPortrait;
            cutFaceImg.uvRect = face != null ? new Rect(0, 0, 1, 1) : PortraitEyes;
            cutFace.sizeDelta = new Vector2(CutFaceWide, face != null ? CutInHigh : CutInHigh * 2f);
            cutEdge.color = edge;
            cutSlash.color = edge;
            cutText.text = name;
            cutOutline.effectColor = outline;
            cutT = 0;
            cutIn.gameObject.SetActive(true);
            TickCutIn(0);
        }

        void TickCutIn(float dt)
        {
            if (cutT < 0) return;
            cutT += dt;
            float u = cutT / CutInTime;
            if (u >= 1) { cutT = -1; cutIn.gameObject.SetActive(false); return; }
            // sweeps in from the right, opens out, and at the end closes to a line
            float inK = Mathf.Clamp01(u / 0.14f), outK = Mathf.Clamp01((u - 0.84f) / 0.16f);
            float ease = 1 - (1 - inK) * (1 - inK) * (1 - inK);
            cutIn.anchoredPosition = new Vector2((1 - ease) * CutInWide, 0);
            cutIn.localScale = new Vector3(1, Mathf.Lerp(0.25f, 1f, ease) * (1 - outK * outK), 1);
            // his eyes slide in from the left and drift on; the name drifts the other way
            float faceIn = Mathf.Clamp01((u - 0.05f) / 0.16f);
            faceIn = 1 - (1 - faceIn) * (1 - faceIn);
            cutFace.anchoredPosition = new Vector2(Mathf.Lerp(CutFaceX - 500f, CutFaceX, faceIn) - 30f * u, 0);
            float nameIn = Mathf.Clamp01((u - 0.12f) / 0.12f);
            cutName.anchoredPosition = new Vector2(CutNameX + 40f * u, 0);
            cutName.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, nameIn * (2 - nameIn));
            cutText.color = new Color(Paper.r, Paper.g, Paper.b, nameIn);
            foreach (var (st, speed) in cutStreaks)
            {
                var p = st.anchoredPosition;
                p.x -= speed * dt;
                if (p.x < -CutInWide / 2 - 300) p.x += CutInWide + 600;
                st.anchoredPosition = p;
            }
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

        // ---------- a sleeping soldier: rising Zs and a snot bubble, kept on him as the camera moves ----------

        // the user: the "z" now and then was hard to notice. A stream of big Zs instead, each drifting up and away
        // from his head, swaying and growing as it fades.
        readonly List<(RectTransform rt, Text t, float age, Vector3 at, float dir, float sway)> snores =
            new List<(RectTransform, Text, float, Vector3, float, float)>();
        // the bubbles shown this frame, by their sleeper: one he stops placing is taken away
        readonly Dictionary<object, (Image img, int frame)> bubbles = new Dictionary<object, (Image, int)>();
        static Sprite bubbleSprite;
        static readonly Color SnoreCol = CbArt.Hex("#eef4ff");
        const float SnoreLife = 2.4f;

        /// <summary>A "Z" rising from a sleeper's head (world point), drifting up and toward dir (+1 right, -1 left).</summary>
        public void Snore(Vector3 world, float dir, bool big)
        {
            if (!ToLocal(world, out var local)) return;
            var rt = UIKit.Rect(root, "snore", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), local, new Vector2(90, 90));
            var t = UIKit.Label(rt, "Z", big ? 40 : 30, SnoreCol, TextAnchor.MiddleCenter, true);
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.16f, 0.2f, 0.42f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
            rt.SetAsFirstSibling();
            snores.Add((rt, t, 0f, world, dir, Random.value * 6f));
        }

        /// <summary>
        /// A sleeper's snot bubble this frame: centred at a world point, diameter in world units. Call it every frame
        /// he shows one; once he stops (it popped, he woke, he is gone) it is taken away.
        /// </summary>
        public void Bubble(object sleeper, Vector3 world, float diameter)
        {
            if (!bubbles.TryGetValue(sleeper, out var b))
            {
                var rt = UIKit.Rect(root, "snotBubble", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
                rt.SetAsFirstSibling();
                b = (UIKit.Image(rt, BubbleSprite, Color.white), 0);
            }
            bool seen = ToLocal(world, out var a) & ToLocal(world + Vector3.up * diameter, out var top);
            b.img.enabled = seen;
            if (seen)
            {
                float size = (top - a).magnitude;
                b.img.rectTransform.anchoredPosition = a;
                b.img.rectTransform.sizeDelta = new Vector2(size, size);
            }
            bubbles[sleeper] = (b.img, Time.frameCount);
        }

        /// <summary>A world point in the HUD's own coordinates (false behind the camera).</summary>
        bool ToLocal(Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0) return false;
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, uiCam, out local);
        }

        /// <summary>A soap-film bubble: a faint pale-blue fill, a blue rim darker at its edge, a white gleam.</summary>
        static Sprite BubbleSprite
        {
            get
            {
                if (bubbleSprite != null) return bubbleSprite;
                const int S = 96;
                var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S * 2 - 1, r = Mathf.Sqrt(u * u + v * v);
                        float inside = Mathf.Clamp01((0.98f - r) / 0.03f);
                        var c = new Color(0.78f, 0.9f, 1f, 0.22f);
                        float rim = Mathf.Clamp01((r - 0.72f) / 0.24f);
                        c = Color.Lerp(c, new Color(0.62f, 0.8f, 1f, 0.75f), rim * rim);
                        if (r > 0.9f) c = Color.Lerp(c, new Color(0.3f, 0.48f, 0.85f, 0.95f), Mathf.Clamp01((r - 0.9f) / 0.05f));
                        // the gleam, up and to one side, and a smaller one under it
                        float g1 = Mathf.Clamp01(1 - Vector2.Distance(new Vector2(u, v), new Vector2(-0.38f, 0.4f)) / 0.2f);
                        float g2 = Mathf.Clamp01(1 - Vector2.Distance(new Vector2(u, v), new Vector2(-0.58f, 0.08f)) / 0.08f);
                        float gleam = Mathf.Clamp01(Mathf.Max(g1 * 2.2f, g2 * 2.5f));
                        c = Color.Lerp(c, Color.white, gleam);
                        c.a = Mathf.Max(c.a, gleam) * inside;
                        px[y * S + x] = c;
                    }
                t.SetPixels(px);
                t.Apply();
                return bubbleSprite = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            }
        }

        void TickSleepers(float dt)
        {
            for (int i = snores.Count - 1; i >= 0; i--)
            {
                var s = snores[i];
                s.age += dt;
                float k = s.age / SnoreLife;
                // up and away from his face, swaying, growing
                var at = s.at + new Vector3(s.dir * (0.4f * s.age + 0.14f * Mathf.Sin(s.age * 3.2f + s.sway)), 0.62f * s.age, 0);
                if (k >= 1 || !ToLocal(at, out var local)) { Object.Destroy(s.rt.gameObject); snores.RemoveAt(i); continue; }
                s.rt.anchoredPosition = local;
                s.rt.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.45f, k);
                s.rt.localRotation = Quaternion.Euler(0, 0, -s.dir * 14f + 10f * Mathf.Sin(s.age * 2.6f + s.sway));
                var c = s.t.color; c.a = k < 0.12f ? k / 0.12f : k > 0.6f ? (1 - k) / 0.4f : 1f; s.t.color = c;
                snores[i] = s;
            }
            if (bubbles.Count == 0) return;
            List<object> gone = null;
            foreach (var pair in bubbles)
                if (pair.Value.frame < Time.frameCount - 1 || pair.Value.img == null) (gone ??= new List<object>()).Add(pair.Key);
            if (gone == null) return;
            foreach (var key in gone)
            {
                if (bubbles[key].img != null) Object.Destroy(bubbles[key].img.gameObject);
                bubbles.Remove(key);
            }
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
            perfT -= Time.unscaledDeltaTime;
            if (perfTxt != null && perfT <= 0)
            {
                perfT = 0.5f;
                perfTxt.gameObject.SetActive(ShowFps);
                if (ShowFps)
                    perfTxt.text = $"{Mathf.RoundToInt(CbGame.Fps)} FPS  ·  low {Mathf.RoundToInt(CbGame.WorstFps)}  ·  {CbGame.FrameMs:F1} ms  ·  {CbQuality.Label}";
            }
            // the arrows for soldiers out of view pulse, nudging that way
            if (foeLeft.gameObject.activeSelf || foeRight.gameObject.activeSelf)
            {
                foeT += dt;
                float k = 0.5f + 0.5f * Mathf.Sin(foeT * 9f);
                foeLeft.rectTransform.anchoredPosition = new Vector2(26 - 10 * k, 0);
                foeRight.rectTransform.anchoredPosition = new Vector2(-26 + 10 * k, 0);
                foeLeft.transform.localScale = foeRight.transform.localScale = Vector3.one * (1f + 0.12f * k);
            }
            // the hints: the finger slides down (or across and back), fading in and out; HOLD pulses
            if (hint != Hint.None)
            {
                hintT += dt;
                float u = Mathf.Repeat(hintT, 1.3f) / 1.3f, k = Mathf.Clamp01(u / 0.75f);
                float fade = Mathf.Sin(Mathf.Clamp01(u / 0.85f) * Mathf.PI);
                if (hint == Hint.StickDown) finger.anchoredPosition = new Vector2(0, Mathf.Lerp(0, -50, k * k * (3 - 2 * k)));
                else if (hint == Hint.StickUp) finger.anchoredPosition = new Vector2(0, Mathf.Lerp(0, 50, k * k * (3 - 2 * k)));
                else if (hint == Hint.SwipeAcross) finger.anchoredPosition = new Vector2(Mathf.Lerp(-60, 60, k * k * (3 - 2 * k)) * (Mathf.Repeat(hintT, 2.6f) < 1.3f ? 1 : -1), 0);
                foreach (var img in fingerImgs) { var c = img.color; c.a = 0.85f * fade; img.color = c; }
                holdTxt.transform.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(hintT * 6f));
            }
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
            TickSleepers(dt);
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
            TickCounters(dt);
            TickResult(dt);
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
            TickCutIn(dt);
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
        readonly List<int> gone = new List<int>();
        int stickId = int.MinValue;
        Vector2 stickStart;
        bool stickDown;

        // The stick: left and right move him; pushed down past StickFlick (straight down, or on a slant no flatter than
        // StickSlant), a slide (held down, he stays low: a crawl; down also goes to the hero as the stick's y). Up does
        // nothing for now (the user: the JUMP button is back - for a while up on the stick jumped).
        const float StickFlick = 24f, StickBack = 12f, StickSlant = 0.6f, StickFull = 50f;

        /// <summary>
        /// One frame of the stick pushed d (canvas units from where the finger went down): left and right, and the slide
        /// as it crosses into down (down: whether it was pushed down a frame ago).
        /// </summary>
        public static void StickMoves(Vector2 d, ref bool down, CbInput input)
        {
            input.x = Mathf.Abs(d.x) < 8 ? 0 : Mathf.Clamp(d.x / StickFull, -1f, 1f);
            input.y = d.y > -8 ? 0 : Mathf.Clamp(d.y / StickFull, -1f, 0f);
            bool downNow = down ? -d.y > StickBack : -d.y > StickFlick && -d.y >= Mathf.Abs(d.x) * StickSlant;
            if (downNow && !down) input.slide = true;
            down = downNow;
        }
        // fingers put down on the right of the screen off the buttons: a swipe - down SwipeMin (canvas units) within
        // SwipeTime a slide, across a dodge that way; each finger swipes once
        const float SwipeMin = 42f, SwipeTime = 0.4f;
        readonly Dictionary<int, (Vector2 start, float t, bool done)> swipes = new Dictionary<int, (Vector2, float, bool)>();

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
                    stickKnob.anchoredPosition = Vector2.ClampMagnitude(d, 50f);
                    StickMoves(d, ref stickDown, input);
                    continue;
                }
                if (swipes.TryGetValue(p.Id, out var sw))
                {
                    var d = (p.Position - sw.start) / scale;
                    if (!sw.done && Time.unscaledTime - sw.t < SwipeTime)
                    {
                        if (-d.y > SwipeMin && -d.y > Mathf.Abs(d.x)) { input.slide = true; sw.done = true; }
                        else if (Mathf.Abs(d.x) > SwipeMin && Mathf.Abs(d.x) > Mathf.Abs(d.y)) { input.dash = true; input.dashDir = d.x > 0 ? 1 : -1; sw.done = true; }
                        swipes[p.Id] = sw;
                    }
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
                        case "jump": input.jump = true; input.jumpHeld = true; break;
                        case "ki": input.kiTap = true; break;
                        case "sp": input.skill = true; break;
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
                else if (p.Position.x >= UnityEngine.Screen.width * 0.45f && p.Position.y < UnityEngine.Screen.height * 0.85f)
                    swipes[p.Id] = (p.Position, Time.unscaledTime, false);
            }

            // releases
            foreach (var b in buttons)
            {
                if (b.pointer == int.MinValue) continue;
                if (now.Contains(b.pointer))
                {
                    if (b.id == "atk") input.atkDown = true;
                    if (b.id == "jump") input.jumpHeld = true;
                    if (b.id == "ki") input.kiDown = true;
                    if (b.id == "heavy") input.heavyDown = true;
                    if (b.id == "guard") input.guard = true;
                    continue;
                }
                b.pointer = int.MinValue;
                b.rt.localScale = Vector3.one;
            }
            if (swipes.Count > 0)
            {
                gone.Clear();
                foreach (var id in swipes.Keys) if (!now.Contains(id)) gone.Add(id);
                foreach (var id in gone) swipes.Remove(id);
            }
            if (stickId != int.MinValue && !now.Contains(stickId))
            {
                stickId = int.MinValue;
                stickDown = false;
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
