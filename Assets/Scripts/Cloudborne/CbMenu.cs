using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cloudborne
{
    /// <summary>
    /// The main menu over Raja's camp in the live 3D jungle: an arcade-anime layout with slanted action buttons,
    /// faceted Four-Crystals emblems, floating embers, smart Play/Continue progression, and chamfered sub-pages for
    /// STAGES (with hidden missions), BABA'S WORKSHOP (upgrades and staffs), SETTINGS (including HARD mode) and HOW TO PLAY.
    /// Works by touch, mouse, keyboard and controller; reads pointers itself while the play HUD stands aside.
    /// </summary>
    public class CbMenu
    {
        public enum Page { Main, Stages, Workshop, Settings, Help }

        public class StageInfo
        {
            public string id, name;
            public bool open;
        }

        /// <summary>Chapter 1's stages, as in the story doc.</summary>
        public static readonly StageInfo[] Stages =
        {
            new StageInfo { id = "1-1", name = "THE JUNGLE GATE", open = true },
            // (open to try while it is being built)
            new StageInfo { id = "1-2", name = "THE TREETOPS", open = true },
            new StageInfo { id = "1-3", name = "BABA'S GROVE", open = true },
            new StageInfo { id = "1-4", name = "THE STORMY RIVER", open = true },
            new StageInfo { id = "1-5", name = "THE EMBER TEMPLE", open = true },
        };

        static readonly Color Paper = CbArt.Hex("#fff6e8"), Gold = CbArt.Hex("#ffc94a"), Ember = CbArt.Hex("#ff5a2e"),
            Ink = new Color(0.05f, 0.06f, 0.05f, 1f), Glass = new Color(0.07f, 0.09f, 0.07f, 0.86f),
            PanelCol = new Color(0.055f, 0.07f, 0.06f, 0.93f), CardCol = new Color(0.1f, 0.13f, 0.11f, 0.92f),
            Orange = CbArt.Hex("#e2541c"), OrangeHot = CbArt.Hex("#ff7a2e"), RimIdle = new Color(1f, 0.94f, 0.82f, 0.22f),
            Dim = new Color(1f, 1f, 1f, 0.62f), Mint = CbArt.Hex("#7be696"), Danger = CbArt.Hex("#a8261e"),
            DangerHot = CbArt.Hex("#df3a2c");

        static readonly Color[] CrystalCols =
            { CbArt.Hex("#ff6a3a"), CbArt.Hex("#4ab8ff"), CbArt.Hex("#6fe08a"), CbArt.Hex("#f4f4ff") };

        static readonly Color[] StageAccents =
            { CbArt.Hex("#4cc968"), CbArt.Hex("#88d840"), CbArt.Hex("#ffc94a"), CbArt.Hex("#4ab8ff"), CbArt.Hex("#ff5a2e") };

        static readonly Color[] StaffCols =
            { CbArt.Hex("#d8b07a"), CbArt.Hex("#ff6a3a"), CbArt.Hex("#78d8ff"), CbArt.Hex("#6fe08a") };

        class Btn
        {
            public RectTransform rt;
            public Image fill, rim, accent;
            public Text label;
            public Action press;
            public Color idle, hot, rimIdle, rimHot, accentIdle, accentHot;
            public Vector2 home;
        }

        class Mote
        {
            public RectTransform rt;
            public Image img;
            public Vector2 pos, vel;
            public float phase, amp;
        }

        readonly CbGame g;
        readonly RectTransform root;
        readonly CanvasGroup group;
        readonly Dictionary<Page, RectTransform> pages = new Dictionary<Page, RectTransform>();
        readonly Dictionary<Page, RectTransform> panels = new Dictionary<Page, RectTransform>();
        readonly Dictionary<Page, List<Btn>> lists = new Dictionary<Page, List<Btn>>();
        readonly List<Mote> motes = new List<Mote>();
        readonly RectTransform[] logoCrystals = new RectTransform[4];
        RectTransform mainLogo, mainButtons;
        Page page;
        int focus;
        float prevX, prevY, shopMsgT, animT = 1f;
        bool resetArmed;
        readonly HashSet<int> seen = new HashSet<int>(), now = new HashSet<int>();

        Btn playBtn, resetBtn, staffActionBtn;
        Text playSubTxt, stagesTag, workshopTag, coinsTxt, progressTxt, shopCoins, shopMsg,
            volumeTxt, padsTxt, graphicsTxt, hardTxt, replayTxt, resetTxt, staffName, staffDesc, staffBtn;
        Image volumeBarFill, staffEmblem;
        readonly Image[] staffPips = new Image[4];
        readonly List<(Text status, Image badge, StageInfo stage, int index)> stageCards = new List<(Text, Image, StageInfo, int)>();
        readonly List<(Btn buy, Image[] pips, Text stat, CbSave.Upgrade up)> shopRows = new List<(Btn, Image[], Text, CbSave.Upgrade)>();

        public bool Visible { get; private set; }
        public Page Current => page;

        public CbMenu(CbGame game, Transform parent)
        {
            g = game;
            var go = new GameObject("Menu", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(844, 390);
            // the whole layout always fits, on a long phone or a squarer one (see CbHud)
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = (RectTransform)go.transform;
            group = go.AddComponent<CanvasGroup>();

            BuildMain();
            BuildStages();
            BuildWorkshop();
            BuildSettings();
            BuildHelp();
            Hide();
        }

        // ---------- procedural anime sprites (cached once) ----------

        static Sprite shadeSprite, slantSprite, chamferSprite, diamondSprite, slashSprite;

        static Sprite ShadeSprite
        {
            get
            {
                if (shadeSprite != null) return shadeSprite;
                const int W = 128;
                var t = new Texture2D(W, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int x = 0; x < W; x++)
                {
                    float u = x / (W - 1f);
                    t.SetPixel(x, 0, new Color(0.03f, 0.05f, 0.04f, 0.86f * (1 - Mathf.SmoothStep(0.32f, 1f, u))));
                }
                t.Apply();
                return shadeSprite = Sprite.Create(t, new Rect(0, 0, W, 1), new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>A forward-slanted parallelogram sprite for arcade-anime buttons and tags.</summary>
        static Sprite SlantSprite
        {
            get
            {
                if (slantSprite != null) return slantSprite;
                const int W = 256, H = 64;
                const float Skew = 18f, Pad = 2f;
                var t = NewTex(W, H);
                var px = new Color32[W * H];
                for (int y = 0; y < H; y++)
                {
                    float v = (y + 0.5f) / H;
                    float left = Pad + v * Skew;
                    float right = W - Pad - (1f - v) * Skew;
                    float dy = Mathf.Min(y + 0.5f - Pad, H - Pad - (y + 0.5f));
                    for (int x = 0; x < W; x++)
                    {
                        float dx = Mathf.Min(x + 0.5f - left, right - (x + 0.5f));
                        float a = Mathf.Clamp01(Mathf.Min(dx, dy) + 0.5f);
                        px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                }
                t.SetPixels32(px);
                t.Apply();
                return slantSprite = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>A 9-sliced panel sprite with crisp 45-degree chamfered corners.</summary>
        static Sprite ChamferSprite
        {
            get
            {
                if (chamferSprite != null) return chamferSprite;
                const int S = 128;
                const float Cut = 16f, Pad = 1.5f;
                var t = NewTex(S, S);
                var px = new Color32[S * S];
                float half = S * 0.5f - Pad;
                float limit = half * 2f - Cut;
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float ax = Mathf.Abs(x + 0.5f - S * 0.5f);
                        float ay = Mathf.Abs(y + 0.5f - S * 0.5f);
                        float dBox = half - Mathf.Max(ax, ay);
                        float dCut = (limit - (ax + ay)) * 0.7071f;
                        float a = Mathf.Clamp01(Mathf.Min(dBox, dCut) + 0.5f);
                        px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                t.SetPixels32(px);
                t.Apply();
                return chamferSprite = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
            }
        }

        /// <summary>A faceted diamond crystal with subtle upper-left specular shading.</summary>
        static Sprite DiamondSprite
        {
            get
            {
                if (diamondSprite != null) return diamondSprite;
                const int S = 64;
                var t = NewTex(S, S);
                var px = new Color32[S * S];
                float c = S * 0.5f, r = c - 3f;
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = x + 0.5f - c, dy = y + 0.5f - c;
                        float d = (r - (Mathf.Abs(dx) + Mathf.Abs(dy))) * 0.7071f;
                        float a = Mathf.Clamp01(d + 0.5f);
                        float shade = dy > 0 ? (dx < 0 ? 1f : 0.88f) : (dx < 0 ? 0.84f : 0.72f);
                        byte ch = (byte)(shade * 255);
                        px[y * S + x] = new Color32(ch, ch, ch, (byte)(a * 255));
                    }
                t.SetPixels32(px);
                t.Apply();
                return diamondSprite = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>A tapered diagonal anime brush slash for title highlights.</summary>
        static Sprite SlashSprite
        {
            get
            {
                if (slashSprite != null) return slashSprite;
                const int W = 256, H = 48;
                var t = NewTex(W, H);
                var px = new Color32[W * H];
                for (int y = 0; y < H; y++)
                {
                    float v = (y + 0.5f) / H * 2f - 1f;
                    for (int x = 0; x < W; x++)
                    {
                        float u = (x + 0.5f) / W;
                        float thick = Mathf.Sin(u * Mathf.PI) * (1f - 0.35f * u);
                        float a = Mathf.Clamp01((thick - Mathf.Abs(v)) * 6f) * Mathf.SmoothStep(0f, 0.18f, u) * Mathf.SmoothStep(1f, 0.65f, u);
                        px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                }
                t.SetPixels32(px);
                t.Apply();
                return slashSprite = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
            }
        }

        static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        // ---------- building blocks ----------

        RectTransform NewPage(Page p)
        {
            var rt = UIKit.Stretch(root, p.ToString());
            pages[p] = rt;
            lists[p] = new List<Btn>();
            return rt;
        }

        Btn Button(Page p, RectTransform parent, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Action press,
            int font = 17, bool primary = false, TextAnchor align = TextAnchor.MiddleCenter, bool chamfered = false)
        {
            var rt = UIKit.Rect(parent, text, anchor, pivot, pos, size);
            var sprite = chamfered ? ChamferSprite : SlantSprite;
            var shadow = UIKit.Image(UIKit.Rect(rt, "shadow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(3, -4), size), sprite, Ink);
            var rim = UIKit.Image(UIKit.Rect(rt, "rim", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(4, 4)), sprite, primary ? Gold : RimIdle);
            var fill = UIKit.Image(UIKit.Rect(rt, "fill", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size), sprite, primary ? Orange : Glass);
            if (chamfered) shadow.type = rim.type = fill.type = Image.Type.Sliced;

            var sheen = UIKit.Rect(rt, "sheen", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(chamfered ? 0 : 4, -2),
                new Vector2(size.x - (chamfered ? 30 : 28), 2));
            UIKit.Image(sheen, null, new Color(1, 1, 1, primary ? 0.28f : 0.1f));

            // slanted focus bar on the left for left-aligned menu rows
            Image accent = null;
            if (!chamfered && align != TextAnchor.MiddleCenter)
            {
                var accRt = UIKit.Rect(rt, "accent", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(11, 0), new Vector2(6, size.y - 12));
                accent = UIKit.Image(accRt, SlantSprite, primary ? Gold : new Color(1, 1, 1, 0.18f));
            }

            var label = UIKit.Label(rt, text, font, Paper, align, true);
            if (align != TextAnchor.MiddleCenter) label.rectTransform.offsetMin = new Vector2(24, 0);
            var b = new Btn
            {
                rt = rt,
                fill = fill,
                rim = rim,
                accent = accent,
                label = label,
                press = press,
                idle = primary ? Orange : Glass,
                hot = primary ? OrangeHot : Orange,
                rimIdle = primary ? new Color(Gold.r, Gold.g, Gold.b, 0.72f) : RimIdle,
                rimHot = Gold,
                accentIdle = primary ? Gold : new Color(1f, 0.8f, 0.35f, 0.25f),
                accentHot = Paper,
                home = pos,
            };
            lists[p].Add(b);
            return b;
        }

        RectTransform Panel(Page pageKey, RectTransform pageRt, string heading, string subtitle)
        {
            var dim = UIKit.Stretch(pageRt, "dim");
            UIKit.Image(dim, null, new Color(0.03f, 0.045f, 0.035f, 0.64f));

            var panel = UIKit.Rect(pageRt, "panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(804, 352));
            var shadow = UIKit.Image(UIKit.Rect(panel, "shadow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4, -6), new Vector2(804, 352)), ChamferSprite, Ink);
            var rim = UIKit.Image(UIKit.Rect(panel, "rim", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(808, 356)), ChamferSprite, new Color(Gold.r, Gold.g, Gold.b, 0.42f));
            var fill = UIKit.Image(UIKit.Rect(panel, "fill", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(804, 352)), ChamferSprite, PanelCol);
            shadow.type = rim.type = fill.type = Image.Type.Sliced;

            var headerBar = UIKit.Rect(panel, "header", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(746, 3));
            UIKit.Image(headerBar, null, new Color(Ember.r, Ember.g, Ember.b, 0.8f));

            var hTxt = UIKit.Label(panel, heading, 23, Paper, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(720, 30), TextAnchor.MiddleCenter, true);
            var hOut = hTxt.gameObject.AddComponent<Outline>();
            hOut.effectColor = new Color(Ember.r, Ember.g, Ember.b, 0.85f);
            hOut.effectDistance = new Vector2(2, -2);
            if (!string.IsNullOrEmpty(subtitle))
                UIKit.Label(panel, subtitle, 12, Gold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -47), new Vector2(700, 18), TextAnchor.MiddleCenter);

            panels[pageKey] = panel;
            return panel;
        }

        Btn Back(Page p, RectTransform panel) =>
            Button(p, panel, "BACK", new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 16), new Vector2(146, 38), () => Show(Page.Main), 15);

        void BuildMain()
        {
            var p = NewPage(Page.Main);

            // soft left dark wash so typography and slanted buttons read clearly over the sunlit camp
            var shade = UIKit.Rect(p, "shade", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = new Vector2(0.74f, 1); shade.offsetMin = shade.offsetMax = Vector2.zero;
            UIKit.Image(shade, ShadeSprite, Color.white);

            var band = UIKit.Rect(p, "band", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(115, 0), new Vector2(360, 560));
            band.localRotation = Quaternion.Euler(0, 0, -10f);
            UIKit.Image(band, null, new Color(0.03f, 0.045f, 0.035f, 0.34f));

            // ambient floating embers / jungle fireflies across the backdrop
            var fxLayer = UIKit.Stretch(p, "embers");
            for (int i = 0; i < 16; i++)
            {
                float sz = 6f + (i % 4) * 3.5f;
                var pos = new Vector2(-410f + (i * 57f) % 860f, -190f + (i * 73f) % 380f);
                var rt = UIKit.Rect(fxLayer, "mote", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(sz, sz));
                var c = i % 3 == 0 ? Ember : i % 3 == 1 ? Gold : new Color(0.65f, 1f, 0.58f, 1f);
                c.a = 0.45f;
                var img = UIKit.Image(rt, UIKit.SoftGlow, c);
                motes.Add(new Mote
                {
                    rt = rt,
                    img = img,
                    pos = pos,
                    vel = new Vector2(12f + (i % 5) * 4f, 16f + (i % 4) * 6f),
                    phase = i * 0.9f,
                    amp = 0.25f + (i % 3) * 0.14f,
                });
            }

            // logo block (top left)
            mainLogo = UIKit.Rect(p, "logoBlock", new Vector2(0, 1), new Vector2(0, 1), new Vector2(38, -14), new Vector2(440, 100));
            var slashRt = UIKit.Rect(mainLogo, "slash", new Vector2(0, 1), new Vector2(0, 1), new Vector2(-8, -28), new Vector2(320, 44));
            slashRt.localRotation = Quaternion.Euler(0, 0, -3f);
            UIKit.Image(slashRt, SlashSprite, new Color(Ember.r, Ember.g, Ember.b, 0.38f));

            var logo = UIKit.Label(mainLogo, "RAJA", 66, Paper, new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, 2), new Vector2(360, 74), TextAnchor.MiddleLeft, true);
            logo.fontStyle = FontStyle.Italic;
            var lo = logo.gameObject.AddComponent<Outline>();
            lo.effectColor = Ember; lo.effectDistance = new Vector2(3, -3);

            var rule = UIKit.Rect(mainLogo, "rule", new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -69), new Vector2(268, 2));
            UIKit.Image(rule, null, new Color(Gold.r, Gold.g, Gold.b, 0.85f));
            for (int i = 0; i < 4; i++)
            {
                var cr = UIKit.Rect(mainLogo, "crystal" + i, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(10 + i * 16, -84), new Vector2(12, 12));
                UIKit.Image(cr, DiamondSprite, CrystalCols[i]);
                logoCrystals[i] = cr;
            }
            var sub = UIKit.Label(mainLogo, "AND THE FOUR CRYSTALS", 15, Gold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(74, -74), new Vector2(340, 22), TextAnchor.MiddleLeft, true);
            sub.fontStyle = FontStyle.Italic;

            // primary + secondary slanted action buttons (left side)
            mainButtons = UIKit.Rect(p, "buttons", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(360, 390));
            playBtn = Button(Page.Main, mainButtons, "PLAY", new Vector2(0, 1), new Vector2(0, 1), new Vector2(42, -124), new Vector2(296, 50), PlayNextStage, 22, true, TextAnchor.MiddleLeft);
            playSubTxt = UIKit.Label(playBtn.rt, "STAGE 1-1 ▶", 11, Paper, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(150, 20), TextAnchor.MiddleRight, true);

            var secondary = new[]
            {
                ("STAGES", Page.Stages),
                ("WORKSHOP", Page.Workshop),
                ("SETTINGS", Page.Settings),
                ("HOW TO PLAY", Page.Help),
            };
            float y = -186;
            for (int i = 0; i < secondary.Length; i++)
            {
                var (text, target) = secondary[i];
                var b = Button(Page.Main, mainButtons, text, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40 - i * 2, y), new Vector2(254, 38), () => Show(target), 16, false, TextAnchor.MiddleLeft);
                if (target == Page.Stages)
                    stagesTag = UIKit.Label(b.rt, "", 11, Gold, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(70, 18), TextAnchor.MiddleRight, true);
                else if (target == Page.Workshop)
                    workshopTag = UIKit.Label(b.rt, "", 10, Gold, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(96, 18), TextAnchor.MiddleRight, true);
                y -= 44;
            }

            // top-right status cluster: banked coins + chapter progress
            var coins = UIKit.Rect(p, "coins", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -14), new Vector2(164, 36));
            UIKit.Image(UIKit.Rect(coins, "shadow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2, -3), new Vector2(164, 36)), ChamferSprite, Ink).type = Image.Type.Sliced;
            UIKit.Image(UIKit.Rect(coins, "rim", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(168, 40)), ChamferSprite, new Color(Gold.r, Gold.g, Gold.b, 0.55f)).type = Image.Type.Sliced;
            UIKit.Image(coins, ChamferSprite, Glass).type = Image.Type.Sliced;
            UIKit.Image(UIKit.Rect(coins, "coin", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(20, 20)), DiamondSprite, Gold);
            coinsTxt = UIKit.Label(coins, "0", 17, Paper, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(110, 24), TextAnchor.MiddleRight, true);

            var prog = UIKit.Rect(p, "progress", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -56), new Vector2(164, 24));
            UIKit.Image(prog, SlantSprite, new Color(0.05f, 0.07f, 0.05f, 0.72f));
            progressTxt = UIKit.Label(prog, "CHAPTER 1 · 0/5", 10, Gold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 18), TextAnchor.MiddleCenter, true);

            UIKit.Label(p, "DEMO BUILD · CHAPTER 1", 10, Dim, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-18, 12), new Vector2(260, 16), TextAnchor.MiddleRight);
        }

        void BuildStages()
        {
            var p = NewPage(Page.Stages);
            var panel = Panel(Page.Stages, p, "CHAPTER 1 · THE GREAT JUNGLE", "Find the fire crystal and bring Koda home");

            const float w = 142, gap = 10;
            float x0 = -(Stages.Length * w + (Stages.Length - 1) * gap) / 2 + w / 2;
            for (int i = 0; i < Stages.Length; i++)
            {
                var s = Stages[i];
                var accent = StageAccents[i % StageAccents.Length];
                var pos = new Vector2(x0 + i * (w + gap), 4);
                RectTransform card;
                if (s.open)
                {
                    var b = Button(Page.Stages, panel, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(w, 174), () => g.PlayStage(s.id), 14, false, TextAnchor.MiddleCenter, true);
                    b.idle = CardCol;
                    b.hot = new Color(0.22f, 0.14f, 0.1f, 0.98f);
                    b.rimIdle = new Color(accent.r, accent.g, accent.b, 0.45f);
                    b.rimHot = Gold;
                    card = b.rt;
                }
                else
                {
                    card = UIKit.Rect(panel, s.id, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(w, 174));
                    UIKit.Image(card, ChamferSprite, new Color(1, 1, 1, 0.05f)).type = Image.Type.Sliced;
                }

                var stripe = UIKit.Rect(card, "stripe", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(w - 28, 4));
                var stripeImg = UIKit.Image(stripe, null, s.open ? accent : Dim);

                UIKit.Label(card, s.id, 28, s.open ? accent : Dim, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(w, 34), TextAnchor.MiddleCenter, true);
                var name = UIKit.Label(card, s.name, 12, s.open ? Paper : Dim, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -54), new Vector2(w - 18, 42), TextAnchor.UpperCenter, true);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;

                UIKit.Image(UIKit.Rect(card, "div", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(w - 28, 1)), null, new Color(1, 1, 1, 0.14f));
                var status = UIKit.Label(card, s.open ? "" : "LOCKED", 11, s.open ? Gold : Dim, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(w - 14, 52), TextAnchor.LowerCenter, true);
                status.horizontalOverflow = HorizontalWrapMode.Wrap;
                stageCards.Add((status, stripeImg, s, i));
            }

            UIKit.Label(panel, "LATER CHAPTERS: CORAL BAY · ROCK MOUNTAINS · WIND PEAKS · ASH VALLEY · CLOUD CITADEL", 10, Dim,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(78, 25), new Vector2(600, 18), TextAnchor.MiddleCenter);
            Back(Page.Stages, panel);
        }

        void BuildWorkshop()
        {
            var p = NewPage(Page.Workshop);
            var panel = Panel(Page.Workshop, p, "BABA'S WORKSHOP", "\"Coins for an old monkey's tea? A fair trade.\"  — Baba");

            var coinBox = UIKit.Rect(panel, "shopCoinsBox", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -22), new Vector2(150, 28));
            UIKit.Image(coinBox, SlantSprite, new Color(0, 0, 0, 0.45f));
            UIKit.Image(UIKit.Rect(coinBox, "gem", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(14, 14)), DiamondSprite, Gold);
            shopCoins = UIKit.Label(coinBox, "", 14, Gold, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(115, 22), TextAnchor.MiddleRight, true);

            RectTransform ShopRow(string name, int idx, Color emCol, out Image emblem)
            {
                var row = UIKit.Rect(panel, name, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -72 - idx * 54), new Vector2(752, 48));
                UIKit.Image(row, ChamferSprite, CardCol).type = Image.Type.Sliced;
                UIKit.Image(UIKit.Rect(row, "edge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(754, 50)), ChamferSprite, new Color(1, 1, 1, 0.1f)).type = Image.Type.Sliced;
                UIKit.Image(UIKit.Rect(row, "emblemBg", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(26, 26)), DiamondSprite, new Color(emCol.r, emCol.g, emCol.b, 0.25f));
                emblem = UIKit.Image(UIKit.Rect(row, "emblem", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(18, 18)), DiamondSprite, emCol);
                return row;
            }

            Color[] emblemCols = { CbArt.Hex("#ff5a52"), CbArt.Hex("#ff9c2e"), CbArt.Hex("#78d4ff") };
            for (int i = 0; i < CbSave.Upgrades.Length; i++)
            {
                var u = CbSave.Upgrades[i];
                var row = ShopRow(u.id, i, emblemCols[i % emblemCols.Length], out _);
                UIKit.Label(row, u.name, 15, Gold, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(48, 9), new Vector2(250, 18), TextAnchor.MiddleLeft, true);
                UIKit.Label(row, u.desc, 11, Paper, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(48, -9), new Vector2(270, 16), TextAnchor.MiddleLeft);

                var stat = UIKit.Label(row, "", 11, Mint, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(314, 0), new Vector2(130, 20), TextAnchor.MiddleLeft, true);

                var pips = new Image[u.Max];
                for (int k = 0; k < u.Max; k++)
                    pips[k] = UIKit.Image(UIKit.Rect(row, "pip", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(464 + k * 26, 0), new Vector2(16, 16)), DiamondSprite, Dim);

                var up = u;
                var buy = Button(Page.Workshop, row, "BUY", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(164, 34), () => Buy(up), 14, true);
                shopRows.Add((buy, pips, stat, u));
            }

            // the staff carried: tap for the next (bought if not owned yet), updating the camp hut shelf too
            {
                var row = ShopRow("staffs", CbSave.Upgrades.Length, StaffCols[0], out staffEmblem);
                staffName = UIKit.Label(row, "", 15, Gold, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(48, 9), new Vector2(320, 18), TextAnchor.MiddleLeft, true);
                staffDesc = UIKit.Label(row, "", 11, Paper, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(48, -9), new Vector2(390, 16), TextAnchor.MiddleLeft);
                for (int k = 0; k < staffPips.Length; k++)
                    staffPips[k] = UIKit.Image(UIKit.Rect(row, "spip" + k, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(452 + k * 24, 0), new Vector2(14, 14)), DiamondSprite, Dim);
                staffActionBtn = Button(Page.Workshop, row, "", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(164, 34), () =>
                {
                    string msg = CbSave.NextStaff();
                    if (msg != "") { Message(msg, Ember); CbFx.Sfx("block", 0.6f); }
                    else { Message($"{CbSave.StaffNames[CbSave.StaffKind]} in hand.", Gold); CbFx.Sfx("pickup", 0.6f); g.camp?.Refresh(); }
                    Refresh();
                }, 13, true);
                staffBtn = staffActionBtn.label;
            }

            shopMsg = UIKit.Label(panel, "", 12, Paper, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(78, 25), new Vector2(500, 18), TextAnchor.MiddleCenter, true);
            Back(Page.Workshop, panel);
        }

        void BuildSettings()
        {
            var p = NewPage(Page.Settings);
            var panel = Panel(Page.Settings, p, "SETTINGS", "Audio, touch controls, difficulty and save data");

            float y = -68;
            RectTransform RowPlate(string name)
            {
                var row = UIKit.Rect(panel, "row_" + name, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(736, 36));
                UIKit.Image(row, ChamferSprite, new Color(1, 1, 1, 0.055f)).type = Image.Type.Sliced;
                UIKit.Label(row, name, 14, Paper, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(280, 28), TextAnchor.MiddleLeft, true);
                return row;
            }

            var soundRow = RowPlate("SOUND");
            Button(Page.Settings, soundRow, "-", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-216, 0), new Vector2(46, 28), () => { CbSave.Volume -= 0.1f; Refresh(); }, 18);
            var barBg = UIKit.Rect(soundRow, "volBar", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-72, 0), new Vector2(134, 24));
            UIKit.Image(barBg, ChamferSprite, Ink).type = Image.Type.Sliced;
            var barFillRt = UIKit.Rect(barBg, "fill", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(126, 16));
            volumeBarFill = UIKit.Image(barFillRt, null, new Color(Gold.r, Gold.g, Gold.b, 0.32f));
            volumeTxt = UIKit.Label(barBg, "", 13, Gold, TextAnchor.MiddleCenter, true);
            Button(Page.Settings, soundRow, "+", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(46, 28), () => { CbSave.Volume += 0.1f; Refresh(); }, 18);

            y -= 40;
            var padsRow = RowPlate("TOUCH BUTTONS");
            padsTxt = Button(Page.Settings, padsRow, "", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(246, 28), () => { CbSave.Pads++; Refresh(); }, 14).label;

            y -= 40;
            var gfxRow = RowPlate("GRAPHICS");
            graphicsTxt = Button(Page.Settings, gfxRow, "", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(246, 28),
                () => { CbQuality.Setting++; g.ApplyQuality(); Refresh(); }, 14).label;

            y -= 40;
            var openRow = RowPlate("OPENING SCENE");
            replayTxt = Button(Page.Settings, openRow, "", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(246, 28), () => { g.ReplayOpening(); Refresh(); }, 14).label;

            y -= 40;
            var progRow = RowPlate("PROGRESS");
            resetBtn = Button(Page.Settings, progRow, "", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(246, 28), ResetProgress, 14);
            resetTxt = resetBtn.label;

            y -= 40;
            var diffRow = RowPlate("DIFFICULTY");
            hardTxt = Button(Page.Settings, diffRow, "", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(246, 28), () => { CbSave.Hard = !CbSave.Hard; Refresh(); }, 14).label;

            Back(Page.Settings, panel);
        }

        void BuildHelp()
        {
            var p = NewPage(Page.Help);
            var panel = Panel(Page.Help, p, "HOW TO PLAY", "Controls, combos and jungle tips");

            void HelpCard(float cx, string heading, Color accent, string body)
            {
                var card = UIKit.Rect(panel, heading, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cx, -2), new Vector2(368, 204));
                UIKit.Image(card, ChamferSprite, CardCol).type = Image.Type.Sliced;
                UIKit.Image(UIKit.Rect(card, "top", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(336, 4)), null, accent);
                UIKit.Label(card, heading, 14, accent, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(336, 22), TextAnchor.MiddleCenter, true);
                var txt = UIKit.Label(card, body, 12, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(336, 156), TextAnchor.UpperLeft);
                txt.supportRichText = true;
                txt.horizontalOverflow = HorizontalWrapMode.Wrap;
                txt.lineSpacing = 1.25f;
            }

            HelpCard(-192, "MOVEMENT & AGILITY", CrystalCols[1],
                "<color=#ffc94a>STICK ↔</color>  Walk (light), run (full), sprint (tap twice)\n" +
                "<color=#ffc94a>JUMP</color>  Jump, again in air for double somersault\n" +
                "<color=#ffc94a>WALL / VINES</color>  Jump off walls · push into vines to climb\n" +
                "<color=#ffc94a>STICK ↓</color>  Slide under trunks · hold to duck\n" +
                "<color=#ffc94a>SWIPE ↔</color>  Dodge — right before a hit for slow motion");

            HelpCard(192, "COMBAT & SECRETS", Ember,
                "<color=#ffc94a>ATTACK</color>  Paw & kick combo · in the air: dive kick\n" +
                "<color=#ffc94a>HOLD ATTACK</color>  Staff smash, keep holding to whirl\n" +
                "<color=#ffc94a>KI / FIRE</color>  Staff lunge (or fire) · hold: staff throw\n" +
                "<color=#ffc94a>SPECIAL</color>  Unleash when charged\n" +
                "<color=#7be696>TIPS</color>  Break crates for mangoes & coins · Staff breaks cracked stone");

            var keysBar = UIKit.Rect(panel, "keys", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-22, 20), new Vector2(586, 26));
            UIKit.Image(keysBar, SlantSprite, new Color(0, 0, 0, 0.45f));
            UIKit.Label(keysBar, "KEYS: A/D Move · W/Space Jump · S Slide · J Attack · K Ki · I Special · Shift/L Dodge · Esc Pause",
                10, Dim, TextAnchor.MiddleCenter);

            Back(Page.Help, panel);
        }

        // ---------- progression helpers ----------

        static StageInfo NextPlayableStage()
        {
            foreach (var s in Stages)
                if (s.open && !CbSave.Cleared(s.id)) return s;
            return Stages[0];
        }

        static int ClearedCount()
        {
            int n = 0;
            foreach (var s in Stages) if (CbSave.Cleared(s.id)) n++;
            return n;
        }

        static bool CanAffordWorkshop()
        {
            foreach (var u in CbSave.Upgrades)
            {
                int price = CbSave.Price(u);
                if (price > 0 && CbSave.Coins >= price) return true;
            }
            for (int i = 1; i < CbSave.StaffPrices.Length; i++)
                if ((CbSave.StaffsOwned & (1 << i)) == 0 && CbSave.Coins >= CbSave.StaffPrices[i]) return true;
            return false;
        }

        static string StatPreview(CbSave.Upgrade u, int lvl)
        {
            if (u.id == "heart") return lvl < u.Max ? $"HP {100 + 20 * lvl} → {100 + 20 * (lvl + 1)}" : $"HP {100 + 20 * lvl} (MAX)";
            if (u.id == "ember") return lvl < u.Max ? $"KI +{40 * lvl}% → +{40 * (lvl + 1)}%" : $"KI +{40 * lvl}% (MAX)";
            return lvl < u.Max ? $"DMG {100 + 10 * lvl}% → {100 + 10 * (lvl + 1)}%" : $"DMG {100 + 10 * lvl}% (MAX)";
        }

        void PlayNextStage() => g.PlayStage(NextPlayableStage().id);

        // ---------- showing ----------

        public void Open()
        {
            Visible = true;
            root.gameObject.SetActive(true);
            group.alpha = 0;
            Show(Page.Main);
        }

        public void Hide()
        {
            Visible = false;
            root.gameObject.SetActive(false);
        }

        public void Show(Page p)
        {
            page = p;
            foreach (var kv in pages) kv.Value.gameObject.SetActive(kv.Key == p);
            focus = 0;
            resetArmed = false;
            shopMsgT = 0;
            animT = 0;
            if (shopMsg != null) shopMsg.text = "";
            Refresh();
            ApplyEntrance(0f);
            CbFx.Sfx("talk", 0.4f);
        }

        void Refresh()
        {
            int cleared = ClearedCount();
            var next = NextPlayableStage();
            if (playBtn != null)
            {
                playBtn.label.text = cleared > 0 ? "CONTINUE" : "PLAY";
                playSubTxt.text = $"{next.id} · {next.name} ▶";
            }
            if (stagesTag != null) stagesTag.text = $"{cleared}/{Stages.Length}";
            if (workshopTag != null) workshopTag.text = CanAffordWorkshop() ? "UPGRADE!" : "";

            coinsTxt.text = CbSave.Coins.ToString("N0");
            progressTxt.text = CbSave.FireAwake ? $"FIRE AWAKE · {cleared}/{Stages.Length}" : $"CHAPTER 1 · {cleared}/{Stages.Length}";
            progressTxt.color = CbSave.FireAwake ? Ember : Gold;
            shopCoins.text = "COINS " + CbSave.Coins.ToString("N0");

            foreach (var (status, badge, s, idx) in stageCards)
            {
                if (!s.open) continue;
                bool isNext = s.id == next.id && !CbSave.Cleared(s.id);
                badge.color = CbSave.Cleared(s.id) ? Mint : StageAccents[idx % StageAccents.Length];
                if (CbSave.Cleared(s.id))
                {
                    float t = CbSave.BestTime(s.id);
                    // (and how many of its hidden missions are done: CbGame.Missions)
                    status.text = $"CLEARED  {(int)(t / 60)}:{(int)(t % 60):00}\n★ {CbGame.MissionsDone(s.id)}/3 · {CbSave.Best(s.id):N0}";
                    status.color = Mint;
                }
                else if (CbSave.Best(s.id) > 0)
                {
                    status.text = $"BEST {CbSave.Best(s.id):N0}\nPLAY";
                    status.color = Gold;
                }
                else
                {
                    status.text = isNext ? "NEXT ▶" : "PLAY";
                    status.color = isNext ? Gold : Paper;
                }
            }

            foreach (var (buy, pips, stat, u) in shopRows)
            {
                int lvl = CbSave.Level(u), price = CbSave.Price(u);
                for (int k = 0; k < pips.Length; k++) pips[k].color = k < lvl ? Gold : new Color(1, 1, 1, 0.16f);
                stat.text = StatPreview(u, lvl);
                stat.color = lvl >= u.Max ? Gold : Mint;
                buy.label.text = price > 0 ? $"BUY  {price}" : "MAX";
                bool canBuy = price > 0 && CbSave.Coins >= price;
                buy.label.color = canBuy ? Paper : Dim;
                buy.idle = canBuy ? Orange : Glass;
                buy.rimIdle = canBuy ? Gold : RimIdle;
            }

            {
                int k = CbSave.StaffKind, nextStaff = (k + 1) % CbSave.StaffNames.Length;
                staffName.text = "STAFF: " + CbSave.StaffNames[k];
                staffDesc.text = k == 0 ? "Plain oak. Tap for the next staff." : CbSave.StaffDescs[k];
                staffEmblem.color = StaffCols[k % StaffCols.Length];
                for (int i = 0; i < staffPips.Length; i++)
                {
                    bool has = (CbSave.StaffsOwned & (1 << i)) != 0;
                    staffPips[i].color = i == k ? Paper : has ? StaffCols[i] : new Color(1, 1, 1, 0.14f);
                }
                bool owned = (CbSave.StaffsOwned & (1 << nextStaff)) != 0;
                bool canBuy = !owned && CbSave.Coins >= CbSave.StaffPrices[nextStaff];
                string shortName = CbSave.StaffNames[nextStaff].Split(' ')[0];
                staffBtn.text = owned ? "CARRY " + shortName : $"BUY {shortName} {CbSave.StaffPrices[nextStaff]}";
                staffBtn.color = owned || canBuy ? Paper : Dim;
                staffActionBtn.idle = owned || canBuy ? Orange : Glass;
                staffActionBtn.rimIdle = owned || canBuy ? Gold : RimIdle;
            }

            volumeTxt.text = Mathf.RoundToInt(CbSave.Volume * 100) + "%";
            if (volumeBarFill != null) volumeBarFill.rectTransform.sizeDelta = new Vector2(126f * Mathf.Clamp01(CbSave.Volume), 16f);
            padsTxt.text = CbSave.Pads == 0 ? "AUTO" : CbSave.Pads == 1 ? "ALWAYS" : "OFF";
            graphicsTxt.text = CbQuality.Label;
            hardTxt.text = CbSave.Hard ? "HARD" : "NORMAL";
            hardTxt.color = CbSave.Hard ? Ember : Paper;
            replayTxt.text = CbSave.IntroSeen ? "PLAY IT AGAIN" : "PLAYS NEXT TIME";
            resetTxt.text = resetArmed ? "SURE? TAP AGAIN" : "RESET";
            if (resetBtn != null)
            {
                resetBtn.idle = resetArmed ? Danger : Glass;
                resetBtn.hot = resetArmed ? DangerHot : Orange;
                resetBtn.rimIdle = resetArmed ? Gold : RimIdle;
            }
        }

        void Buy(CbSave.Upgrade u)
        {
            int price = CbSave.Price(u);
            if (price <= 0) { Message("Already at its best.", Dim); CbFx.Sfx("block", 0.6f); return; }
            if (!CbSave.Buy(u)) { Message($"Not enough coins: {price - CbSave.Coins} more to find.", Ember); CbFx.Sfx("block", 0.6f); return; }
            Message($"{u.name} {CbSave.Level(u)}!", Gold);
            CbFx.Sfx("coin");
            CbFx.Sfx("pickup", 0.7f);
            Refresh();
        }

        void Message(string text, Color c)
        {
            shopMsg.text = text;
            shopMsg.color = c;
            shopMsgT = 2.5f;
        }

        void ResetProgress()
        {
            if (!resetArmed) { resetArmed = true; Refresh(); return; }
            CbSave.ResetProgress();
            g.ReplayOpening();
            resetArmed = false;
            Refresh();
            CbFx.Sfx("block");
        }

        /// <summary>Presses the button on the current page whose label starts with text (test hook).</summary>
        public bool Press(string text)
        {
            foreach (var b in lists[page])
                if (b.label.text.StartsWith(text)) { b.press(); return true; }
            return false;
        }

        // ---------- animations & input ----------

        void ApplyEntrance(float k)
        {
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f);
            if (page == Page.Main)
            {
                if (mainLogo != null) mainLogo.anchoredPosition = new Vector2(Mathf.Lerp(16f, 38f, ease), -14f);
                var list = lists[Page.Main];
                for (int i = 0; i < list.Count; i++)
                {
                    float u = Mathf.Clamp01((k - i * 0.06f) / 0.7f);
                    float be = 1f - Mathf.Pow(1f - u, 3f);
                    list[i].rt.anchoredPosition = new Vector2(list[i].home.x - (1f - be) * 28f, list[i].home.y);
                }
            }
            else if (panels.TryGetValue(page, out var panel))
            {
                panel.anchoredPosition = new Vector2(0, Mathf.Lerp(-16f, 0f, ease));
                panel.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, ease);
            }
        }

        void AnimateBackdrop(float dt)
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < 4; i++)
            {
                if (logoCrystals[i] == null) continue;
                logoCrystals[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2f + i * 0.9f) * 10f);
                logoCrystals[i].localScale = Vector3.one * (0.94f + 0.12f * Mathf.Sin(t * 3.4f + i * 1.1f));
            }
            if (page != Page.Main) return;
            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                m.pos += m.vel * dt;
                if (m.pos.x > 470f) m.pos.x -= 940f;
                if (m.pos.y > 220f) { m.pos.y = -220f; m.pos.x = -430f + (m.pos.x + 610f) % 860f; }
                m.rt.anchoredPosition = m.pos + new Vector2(Mathf.Sin(t * 1.8f + m.phase) * 8f, 0f);
                var c = m.img.color;
                c.a = m.amp * (0.55f + 0.45f * Mathf.Sin(t * 2.6f + m.phase));
                m.img.color = c;
            }
        }

        public void Update(float dt, CbInput input)
        {
            if (!Visible) return;
            group.alpha = Mathf.MoveTowards(group.alpha, 1, dt * 5f);
            if (animT < 1f)
            {
                animT = Mathf.Min(1f, animT + dt / 0.24f);
                ApplyEntrance(animT);
            }
            AnimateBackdrop(dt);
            if (shopMsgT > 0) { shopMsgT -= dt; if (shopMsgT <= 0) shopMsg.text = ""; }
            var list = lists[page];

            // keyboard and controller: a push of the stick or arrows moves once; confirm presses; back returns
            bool next = (input.y < -0.5f && prevY >= -0.5f) || (input.x > 0.5f && prevX <= 0.5f);
            bool prev = (input.y > 0.5f && prevY <= 0.5f) || (input.x < -0.5f && prevX >= -0.5f);
            prevX = input.x; prevY = input.y;
            if (list.Count > 0)
            {
                if (next) { focus = (focus + 1) % list.Count; CbFx.Sfx("talk", 0.3f); }
                if (prev) { focus = (focus + list.Count - 1) % list.Count; CbFx.Sfx("talk", 0.3f); }
            }
            if (input.pause && page != Page.Main) Show(Page.Main);
            else if (input.confirm && list.Count > 0) Press(list[focus]);

            // touch: a tap presses the button under it
            now.Clear();
            foreach (var p in InputBridge.GetPointers())
            {
                now.Add(p.Id);
                if (seen.Contains(p.Id) || !Visible) continue;
                var current = lists[page];
                for (int i = 0; i < current.Count; i++)
                    if (current[i].rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(current[i].rt, p.Position, null))
                    {
                        focus = i;
                        Press(current[i]);
                        break;
                    }
            }
            seen.Clear();
            foreach (var id in now) seen.Add(id);

            if (!Visible) return;
            list = lists[page];
            if (focus >= list.Count) focus = 0;
            float kCol = 1f - Mathf.Exp(-18f * dt);
            float kScale = 1f - Mathf.Exp(-14f * dt);
            for (int i = 0; i < list.Count; i++)
            {
                bool hot = i == focus;
                var b = list[i];
                b.fill.color = Color.Lerp(b.fill.color, hot ? b.hot : b.idle, kCol);
                if (b.rim != null) b.rim.color = Color.Lerp(b.rim.color, hot ? b.rimHot : b.rimIdle, kCol);
                if (b.accent != null) b.accent.color = Color.Lerp(b.accent.color, hot ? b.accentHot : b.accentIdle, kCol);
                float s = hot ? 1.045f + 0.012f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
                b.rt.localScale = Vector3.Lerp(b.rt.localScale, Vector3.one * s, kScale);
            }
        }

        void Press(Btn b)
        {
            b.rt.localScale = Vector3.one * 0.94f;
            CbFx.Sfx("swing", 0.5f);
            b.press?.Invoke();
        }
    }
}
