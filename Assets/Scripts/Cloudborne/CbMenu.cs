using System;
using System.Collections.Generic;
using RacingGame.Core;
using RacingGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cloudborne
{
    /// <summary>
    /// The main menu, over the live jungle with Raja standing on the right: PLAY, STAGES (the chapter's stages and
    /// their best results), WORKSHOP (Baba's upgrades, paid in banked tiger coins), SETTINGS and HOW TO PLAY.
    /// Works by touch (a tap presses a button), keyboard and controller (up/down or left/right move, confirm presses,
    /// back or Esc returns). It reads touches itself while the HUD stands aside.
    /// </summary>
    public class CbMenu
    {
        public enum Page { Main, Stages, Workshop, Settings, Help }

        public class StageInfo
        {
            public string id, name;
            public bool open;
        }

        /// <summary>Chapter 1's stages, as in the story doc; only 1-1 is built so far.</summary>
        public static readonly StageInfo[] Stages =
        {
            new StageInfo { id = "1-1", name = "THE JUNGLE GATE", open = true },
            new StageInfo { id = "1-2", name = "THE TREETOPS" },
            new StageInfo { id = "1-3", name = "BABA'S GROVE" },
            new StageInfo { id = "1-4", name = "THE STORMY RIVER" },
            new StageInfo { id = "1-5", name = "THE EMBER TEMPLE" },
        };

        static readonly Color Paper = CbArt.Hex("#fff6e8"), Gold = CbArt.Hex("#ffc94a"), Ember = CbArt.Hex("#ff5a2e"),
            Ink = new Color(0.07f, 0.09f, 0.06f, 1f), Glass = new Color(0.07f, 0.09f, 0.06f, 0.72f), PanelCol = new Color(0.06f, 0.08f, 0.06f, 0.86f),
            Orange = CbArt.Hex("#e2541c"), OrangeHot = CbArt.Hex("#ff7a2e"), Dim = new Color(1, 1, 1, 0.6f);

        class Btn
        {
            public RectTransform rt;
            public Image fill;
            public Text label;
            public Action press;
            public Color idle, hot;
        }

        readonly CbGame g;
        readonly RectTransform root;
        readonly CanvasGroup group;
        readonly Dictionary<Page, RectTransform> pages = new Dictionary<Page, RectTransform>();
        readonly Dictionary<Page, List<Btn>> lists = new Dictionary<Page, List<Btn>>();
        Page page;
        int focus;
        float prevX, prevY, shopMsgT;
        bool resetArmed;
        readonly HashSet<int> seen = new HashSet<int>(), now = new HashSet<int>();

        Text coinsTxt, shopCoins, shopMsg, volumeTxt, padsTxt, replayTxt, resetTxt;
        Btn resetBtn;
        readonly List<(Text status, StageInfo stage)> stageCards = new List<(Text, StageInfo)>();
        readonly List<(Btn buy, Image[] pips, CbSave.Upgrade up)> shopRows = new List<(Btn, Image[], CbSave.Upgrade)>();

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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            root = (RectTransform)go.transform;
            group = go.AddComponent<CanvasGroup>();

            BuildMain();
            BuildStages();
            BuildWorkshop();
            BuildSettings();
            BuildHelp();
            Hide();
        }

        // ---------- building ----------

        RectTransform NewPage(Page p)
        {
            var rt = UIKit.Stretch(root, p.ToString());
            pages[p] = rt;
            lists[p] = new List<Btn>();
            return rt;
        }

        Btn Button(Page p, RectTransform parent, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Action press,
            int font = 17, bool primary = false, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = UIKit.Rect(parent, text, anchor, pivot, pos, size);
            UIKit.Image(UIKit.Rect(rt, "shadow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), size), UIKit.Rounded, new Color(0, 0, 0, 0.45f));
            var fill = UIKit.Image(UIKit.Rect(rt, "fill", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size), UIKit.Rounded, Glass);
            UIKit.Image(UIKit.Rect(rt, "edge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size), UIKit.Rounded, new Color(1, 1, 1, 0.07f));
            var label = UIKit.Label(rt, text, font, Paper, align, true);
            if (align != TextAnchor.MiddleCenter) label.rectTransform.offsetMin = new Vector2(18, 0);
            var b = new Btn { rt = rt, fill = fill, label = label, press = press, idle = primary ? Orange : Glass, hot = primary ? OrangeHot : Orange };
            lists[p].Add(b);
            return b;
        }

        RectTransform Panel(RectTransform page, string heading, out Text headingTxt)
        {
            var panel = UIKit.Panel(page, "panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(790, 340), PanelCol).rectTransform;
            headingTxt = UIKit.Label(panel, heading, 24, Paper, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(700, 32), TextAnchor.MiddleCenter, true);
            headingTxt.gameObject.AddComponent<Outline>().effectColor = new Color(Ember.r, Ember.g, Ember.b, 0.8f);
            return panel;
        }

        Btn Back(Page p, RectTransform panel) =>
            Button(p, panel, "BACK", new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 16), new Vector2(150, 40), () => Show(Page.Main), 16);

        void BuildMain()
        {
            var p = NewPage(Page.Main);
            // a soft dark shade on the left so the menu reads over the jungle; Raja stands in the light on the right
            var shade = UIKit.Rect(p, "shade", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = new Vector2(0.72f, 1); shade.offsetMin = shade.offsetMax = Vector2.zero;
            UIKit.Image(shade, ShadeSprite(), Color.white);

            var logo = UIKit.Label(p, "RAJA", 62, Paper, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -14), new Vector2(420, 70), TextAnchor.MiddleLeft, true);
            var o = logo.gameObject.AddComponent<Outline>();
            o.effectColor = Ember; o.effectDistance = new Vector2(3, -4);
            UIKit.Label(p, "AND THE FOUR CRYSTALS", 18, Gold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -80), new Vector2(420, 24), TextAnchor.MiddleLeft, true);

            float y = -118;
            var play = Button(Page.Main, p, "PLAY", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, y), new Vector2(270, 46), () => g.PlayStage("1-1"), 22, true, TextAnchor.MiddleLeft);
            UIKit.Label(play.rt, "STAGE 1-1", 11, Paper, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(120, 20), TextAnchor.MiddleRight, true);
            y -= 54;
            foreach (var (text, target) in new[] { ("STAGES", Page.Stages), ("WORKSHOP", Page.Workshop), ("SETTINGS", Page.Settings), ("HOW TO PLAY", Page.Help) })
            {
                var to = target;
                Button(Page.Main, p, text, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, y), new Vector2(240, 40), () => Show(to), 17, false, TextAnchor.MiddleLeft);
                y -= 46;
            }

            // coins, top right
            var coins = UIKit.Panel(p, "coins", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -14), new Vector2(150, 36), Glass).rectTransform;
            UIKit.Image(UIKit.Rect(coins, "coin", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(22, 22)), UIKit.Circle, Gold);
            coinsTxt = UIKit.Label(coins, "0", 18, Paper, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(110, 26), TextAnchor.MiddleRight, true);
            UIKit.Label(p, "DEMO BUILD · CHAPTER 1", 10, Dim, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 10), new Vector2(300, 16), TextAnchor.MiddleRight);
        }

        void BuildStages()
        {
            var p = NewPage(Page.Stages);
            var panel = Panel(p, "CHAPTER 1 · THE GREAT JUNGLE", out _);
            UIKit.Label(panel, "Find the fire crystal and bring Koda home", 13, Gold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(700, 20), TextAnchor.MiddleCenter);
            const float w = 138, gap = 11;
            float x0 = -(Stages.Length * w + (Stages.Length - 1) * gap) / 2 + w / 2;
            for (int i = 0; i < Stages.Length; i++)
            {
                var s = Stages[i];
                var pos = new Vector2(x0 + i * (w + gap), 8);
                RectTransform card;
                if (s.open)
                {
                    var b = Button(Page.Stages, panel, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(w, 158), () => g.PlayStage(s.id));
                    b.idle = new Color(0.16f, 0.3f, 0.16f, 0.95f);
                    card = b.rt;
                }
                else
                {
                    card = UIKit.Panel(panel, s.id, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(w, 158), new Color(1, 1, 1, 0.06f)).rectTransform;
                }
                var col = s.open ? Paper : Dim;
                UIKit.Label(card, s.id, 26, s.open ? Gold : Dim, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(w, 32), TextAnchor.MiddleCenter, true);
                var name = UIKit.Label(card, s.name, 13, col, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(w - 16, 40), TextAnchor.UpperCenter, true);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                var status = UIKit.Label(card, s.open ? "" : "LOCKED", 12, s.open ? Gold : Dim, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(w - 12, 36), TextAnchor.LowerCenter);
                status.horizontalOverflow = HorizontalWrapMode.Wrap;
                stageCards.Add((status, s));
            }
            UIKit.Label(panel, "LATER CHAPTERS: CORAL BAY · ROCK MOUNTAINS · WIND PEAKS · ASH VALLEY · THE CLOUD CITADEL", 11, Dim,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(80, 24), new Vector2(600, 18), TextAnchor.MiddleCenter);
            Back(Page.Stages, panel);
        }

        void BuildWorkshop()
        {
            var p = NewPage(Page.Workshop);
            var panel = Panel(p, "BABA'S WORKSHOP", out _);
            UIKit.Label(panel, "\"Coins for an old monkey's tea? A fair trade.\"  — Baba", 12, Dim, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(700, 18), TextAnchor.MiddleCenter);
            shopCoins = UIKit.Label(panel, "", 16, Gold, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -16), new Vector2(200, 24), TextAnchor.MiddleRight, true);
            for (int i = 0; i < CbSave.Upgrades.Length; i++)
            {
                var u = CbSave.Upgrades[i];
                var row = UIKit.Panel(panel, u.id, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -72 - i * 64), new Vector2(740, 56), new Color(1, 1, 1, 0.06f)).rectTransform;
                UIKit.Label(row, u.name, 17, Gold, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 10), new Vector2(300, 22), TextAnchor.MiddleLeft, true);
                UIKit.Label(row, u.desc, 12, Paper, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, -12), new Vector2(340, 18), TextAnchor.MiddleLeft);
                var pips = new Image[u.Max];
                for (int k = 0; k < u.Max; k++)
                    pips[k] = UIKit.Image(UIKit.Rect(row, "pip", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400 + k * 26, 0), new Vector2(18, 18)), UIKit.Circle, Dim);
                var up = u;
                var buy = Button(Page.Workshop, row, "BUY", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(150, 40), () => Buy(up), 16);
                shopRows.Add((buy, pips, u));
            }
            shopMsg = UIKit.Label(panel, "", 13, Paper, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(80, 26), new Vector2(500, 20), TextAnchor.MiddleCenter);
            Back(Page.Workshop, panel);
        }

        void BuildSettings()
        {
            var p = NewPage(Page.Settings);
            var panel = Panel(p, "SETTINGS", out _);
            float y = -56;
            void Row(string name) =>
                UIKit.Label(panel, name, 16, Paper, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, y), new Vector2(300, 40), TextAnchor.MiddleLeft, true);
            Row("SOUND");
            Button(Page.Settings, panel, "-", new Vector2(0, 1), new Vector2(0, 1), new Vector2(380, y), new Vector2(48, 40), () => { CbSave.Volume -= 0.1f; Refresh(); }, 22);
            volumeTxt = UIKit.Label(panel, "", 17, Gold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(434, y), new Vector2(90, 40), TextAnchor.MiddleCenter, true);
            Button(Page.Settings, panel, "+", new Vector2(0, 1), new Vector2(0, 1), new Vector2(530, y), new Vector2(48, 40), () => { CbSave.Volume += 0.1f; Refresh(); }, 22);
            y -= 45;
            Row("TOUCH BUTTONS");
            padsTxt = Button(Page.Settings, panel, "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(380, y), new Vector2(198, 40), () => { CbSave.Pads++; Refresh(); }, 15).label;
            y -= 45;
            Row("OPENING SCENE");
            replayTxt = Button(Page.Settings, panel, "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(380, y), new Vector2(198, 40), () => { g.ReplayOpening(); Refresh(); }, 15).label;
            y -= 45;
            Row("PROGRESS");
            resetBtn = Button(Page.Settings, panel, "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(380, y), new Vector2(198, 40), ResetProgress, 15);
            resetTxt = resetBtn.label;
            y -= 45;
            Row("LANGUAGE");
            UIKit.Label(panel, "ENGLISH  ·  Hebrew coming later", 13, Dim, new Vector2(0, 1), new Vector2(0, 1), new Vector2(380, y), new Vector2(330, 40), TextAnchor.MiddleLeft);
            Back(Page.Settings, panel);
        }

        void BuildHelp()
        {
            var p = NewPage(Page.Help);
            var panel = Panel(p, "HOW TO PLAY", out _);
            var t = UIKit.Label(panel,
                "<color=#ffc94a>HIT</color>  paw and kick combo   ·   <color=#ffc94a>↓ + HIT</color>  low sweep\n" +
                "<color=#ffc94a>HEAVY</color>  staff smash   ·   <color=#ffc94a>↑ + HEAVY</color>  launcher   ·   hold <color=#ffc94a>HEAVY</color>  staff whirl\n" +
                "<color=#ffc94a>DASH → HEAVY</color>  thrust   ·   <color=#ffc94a>HEAVY</color> in the air  slam   ·   <color=#ffc94a>JUMP</color> twice  double somersault\n" +
                "<color=#ffc94a>FIRE</color>  fireball   ·   <color=#ffc94a>HIT, HIT, FIRE</color>  ring of fire   ·   <color=#ffc94a>SPECIAL</color>  flame cannon\n" +
                "hold <color=#ffc94a>GUARD</color>  block from the front   ·   <color=#ffc94a>↓</color>  duck   ·   <color=#ffc94a>DASH</color> just before a blow  slow motion\n\n" +
                "Break crates for mangoes and coins. Cracked stone gives way only to the staff.\n" +
                "Spend coins in Baba's workshop.\n\n" +
                "<color=#ffffff99>Keyboard: A/D move · W/S up/down · Space jump · J hit · K heavy · O guard · U fire · I special · L dash · Esc pause</color>",
                13, Paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(740, 230), TextAnchor.MiddleCenter);
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            Back(Page.Help, panel);
        }

        /// <summary>A dark band fading out to the right, behind the menu's left side.</summary>
        static Sprite ShadeSprite()
        {
            const int W = 128;
            var t = new Texture2D(W, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < W; x++)
            {
                float u = x / (W - 1f);
                t.SetPixel(x, 0, new Color(0.04f, 0.06f, 0.04f, 0.78f * (1 - Mathf.SmoothStep(0.35f, 1f, u))));
            }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, 1), new Vector2(0.5f, 0.5f));
        }

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
            if (shopMsg != null) shopMsg.text = "";
            Refresh();
            CbFx.Sfx("talk", 0.4f);
        }

        void Refresh()
        {
            coinsTxt.text = CbSave.Coins.ToString("N0");
            shopCoins.text = "COINS " + CbSave.Coins.ToString("N0");
            foreach (var (status, s) in stageCards)
            {
                if (!s.open) continue;
                if (CbSave.Cleared(s.id))
                {
                    float t = CbSave.BestTime(s.id);
                    status.text = $"CLEARED  {(int)(t / 60)}:{(int)(t % 60):00}\nBEST {CbSave.Best(s.id):N0}";
                }
                else status.text = CbSave.Best(s.id) > 0 ? $"BEST {CbSave.Best(s.id):N0}\nPLAY" : "PLAY";
            }
            foreach (var (buy, pips, u) in shopRows)
            {
                int lvl = CbSave.Level(u), price = CbSave.Price(u);
                for (int k = 0; k < pips.Length; k++) pips[k].color = k < lvl ? Gold : new Color(1, 1, 1, 0.18f);
                buy.label.text = price > 0 ? $"BUY  {price}" : "MAX";
                buy.label.color = price > 0 && CbSave.Coins >= price ? Paper : Dim;
            }
            volumeTxt.text = Mathf.RoundToInt(CbSave.Volume * 100) + "%";
            padsTxt.text = CbSave.Pads == 0 ? "PHONES ONLY" : CbSave.Pads == 1 ? "ALWAYS" : "OFF";
            replayTxt.text = CbSave.IntroSeen ? "PLAY IT AGAIN" : "PLAYS NEXT TIME";
            resetTxt.text = resetArmed ? "SURE? TAP AGAIN" : "RESET";
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

        // ---------- input ----------

        public void Update(float dt, CbInput input)
        {
            if (!Visible) return;
            group.alpha = Mathf.MoveTowards(group.alpha, 1, dt * 4);
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
            for (int i = 0; i < list.Count; i++)
            {
                bool hot = i == focus;
                var b = list[i];
                b.fill.color = Color.Lerp(b.fill.color, hot ? b.hot : b.idle, 1 - Mathf.Exp(-18 * dt));
                float s = hot ? 1.04f + 0.012f * Mathf.Sin(Time.unscaledTime * 6) : 1f;
                b.rt.localScale = Vector3.Lerp(b.rt.localScale, Vector3.one * s, 1 - Mathf.Exp(-14 * dt));
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
