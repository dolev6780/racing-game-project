using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RacingGame.Car;
using RacingGame.Core;
using RacingGame.CameraRig;
using RacingGame.Race;

namespace RacingGame.UI
{
    /// <summary>
    /// In-race HUD built with uGUI: position/lap card, standings, speedometer + armor,
    /// countdown, warnings, kill feed, damage flash, pause menu and results screen.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        RaceManager race;
        Racer player;
        Canvas canvas;

        // Top-left card
        Text posText, posTotal, lapText, timeText, bestText;
        // Standings
        readonly List<(Image bg, Image bar, Text text)> rows = new List<(Image, Image, Text)>();
        // Speedometer
        Image gaugeFill, gaugeGlow;
        Text speedText;
        RectTransform armorFill;
        Image armorFillImage;
        // Ammo (shown when touch controls are hidden)
        GameObject ammoRoot;
        readonly List<Image[]> ammoPips = new List<Image[]>();
        // Center
        Text countdownText;
        Punch countdownPunch;
        GameObject trackBanner;
        GameObject lights;
        Image[] lamps;
        Image speedLines;
        Punch posPunch;
        int lastPos = -1;
        int lastCountdown = -1;
        float goTimer;
        GameObject warningRoot;
        Text warningText;
        GameObject finalLapBanner;
        float finalLapTimer;
        bool finalLapShown;
        // Effects
        Image damageFlash;
        float flash;
        float lastArmor;
        // Kill feed
        RectTransform feedRoot;
        readonly List<(GameObject go, float time)> feed = new List<(GameObject, float)>();
        // Overlays
        GameObject pauseRoot, resultsRoot;
        readonly List<(Text left, Text right)> resultRows = new List<(Text, Text)>();
        float resultsRefresh;

        TouchControlsView touchView;
        Text camLabel;
        // Phone layout: smaller panels and no standings list so nothing covers the touch controls.
        RectTransform positionCard, standingsPanel, minimapPanel, speedometer, armorBar;
        bool compactApplied, compact;

        void Start()
        {
            race = GetComponent<RaceManager>();
            player = race.Player;
            lastArmor = player.Health.Armor;

            canvas = UIKit.CreateCanvas("HUD", transform, 10);
            var root = canvas.transform;

            BuildDamageFlash(root);
            BuildSpeedLines(root);
            touchView = new GameObject("TouchControls", typeof(RectTransform)).AddComponent<TouchControlsView>();
            touchView.Init(root, canvas, player);
            BuildTopLeft(root);
            BuildTopCenter(root);
            BuildStandings(root);
            var minimap = new GameObject("MinimapView", typeof(RectTransform)).AddComponent<Minimap>();
            minimap.Init(race, root);
            minimapPanel = minimap.Panel;
            BuildSpeedometer(root);
            BuildAmmo(root);
            BuildCenter(root);

            CarHealth.Wrecked += OnWrecked;
            race.PlayerLapCompleted += OnLap;
            RacingGame.Track.Pickup.Collected += OnItem;
            player.Car.MiniTurbo += OnMiniTurbo;
        }

        void OnItem(CarController car, string label)
        {
            if (car == player.Car) AddFeed(label, UIKit.Accent);
        }

        static readonly string[] TurboNames = { "", "MINI-TURBO!", "SUPER MINI-TURBO!", "ULTRA MINI-TURBO!" };
        static readonly Color[] TurboColors = { Color.white, new Color(0.4f, 0.7f, 1f), new Color(1f, 0.6f, 0.15f), new Color(0.85f, 0.4f, 1f) };

        void OnMiniTurbo(int tier)
        {
            AddFeed(TurboNames[Mathf.Clamp(tier, 1, 3)], TurboColors[Mathf.Clamp(tier, 1, 3)]);
        }

        void OnDestroy()
        {
            CarHealth.Wrecked -= OnWrecked;
            RacingGame.Track.Pickup.Collected -= OnItem;
            if (race != null) race.PlayerLapCompleted -= OnLap;
            if (player != null && player.Car != null) player.Car.MiniTurbo -= OnMiniTurbo;
        }

        void OnLap(float time, bool record)
        {
            SoundFX.LapChime();
            if (record) AddFeed($"NEW LAP RECORD!  {UIKit.Time(time)}", UIKit.Good);
            else AddFeed($"LAP  {UIKit.Time(time)}", UIKit.TextMain);
        }

        // ---------- Build ----------

        void BuildDamageFlash(Transform root)
        {
            var rt = UIKit.Stretch(root, "DamageFlash");
            damageFlash = UIKit.Image(rt, UIKit.Vignette, new Color(1f, 0.1f, 0.05f, 0f));
        }

        void BuildSpeedLines(Transform root)
        {
            var rt = UIKit.Rect(root, "SpeedLines", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(2600f, 2600f));
            speedLines = UIKit.Image(rt, UIKit.SpeedLines, new Color(1f, 1f, 1f, 0f));
        }

        void BuildTopLeft(Transform root)
        {
            var card = UIKit.Panel(root, "PositionCard", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f),
                new Vector2(470f, 220f)).rectTransform;
            positionCard = card;

            var accent = UIKit.Rect(card, "Accent", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f),
                new Vector2(10f, 180f));
            UIKit.Image(accent, UIKit.RoundedSmall, UIKit.Accent);

            posText = UIKit.Label(card, "1", 130, UIKit.TextMain, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(40f, 6f), new Vector2(220f, 160f), TextAnchor.MiddleLeft, true);
            posPunch = posText.gameObject.AddComponent<Punch>();
            posTotal = UIKit.Label(card, "/6", 40, UIKit.TextDim, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(40f, -58f), new Vector2(220f, 50f), TextAnchor.MiddleLeft, true);

            UIKit.Label(card, "LAP", 30, UIKit.TextDim, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -18f),
                new Vector2(200f, 40f), TextAnchor.MiddleRight);
            lapText = UIKit.Label(card, "1/4", 70, UIKit.Accent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -54f),
                new Vector2(220f, 80f), TextAnchor.MiddleRight, true);
            timeText = UIKit.Label(card, "0:00.00", 34, UIKit.TextMain, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-30f, 44f), new Vector2(220f, 44f), TextAnchor.MiddleRight);
            bestText = UIKit.Label(card, "", 22, UIKit.TextDim, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-30f, 12f), new Vector2(300f, 30f), TextAnchor.MiddleRight);
        }

        void BuildTopCenter(Transform root)
        {
            var top = new Vector2(0.5f, 1f);
            var pause = UIKit.Button(root, "II", top, top, new Vector2(0f, -30f), new Vector2(110f, 110f),
                () => SetPaused(true), fontSize: 46);
            pause.GetComponent<Image>().sprite = UIKit.Circle;
            pause.GetComponent<Image>().type = Image.Type.Simple;

            var cam = UIKit.Button(root, "VIEW", top, top, new Vector2(190f, -42f), new Vector2(230f, 86f),
                RaceCamera.Toggle, fontSize: 30);
            cam.GetComponent<Image>().color = UIKit.PanelDark;
            camLabel = cam.GetComponentInChildren<Text>();

            finalLapBanner = UIKit.Panel(root, "FinalLap", top, top, new Vector2(0f, -170f), new Vector2(520f, 100f),
                UIKit.AccentHot).gameObject;
            UIKit.Label(finalLapBanner.transform, "FINAL LAP!", 60, UIKit.TextMain, TextAnchor.MiddleCenter, true);
            finalLapBanner.AddComponent<Punch>();
            finalLapBanner.SetActive(false);

            feedRoot = UIKit.Rect(root, "KillFeed", top, top, new Vector2(0f, -290f), new Vector2(900f, 400f));
        }

        void BuildStandings(Transform root)
        {
            int n = race.Racers.Count;
            const float rowH = 50f;
            var panel = UIKit.Panel(root, "Standings", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -370f),
                new Vector2(380f, n * rowH + 24f)).rectTransform;
            standingsPanel = panel;

            for (int i = 0; i < n; i++)
            {
                var row = UIKit.Rect(panel, "Row" + i, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -12f - i * rowH), new Vector2(356f, rowH - 6f));
                var bg = UIKit.Image(row, UIKit.RoundedSmall, new Color(1f, 1f, 1f, 0f));
                var barRt = UIKit.Rect(row, "Bar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f),
                    new Vector2(8f, 32f));
                var bar = UIKit.Image(barRt, UIKit.RoundedSmall, Color.white);
                var text = UIKit.Label(row, "", 30, UIKit.TextMain, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(28f, 0f), new Vector2(320f, rowH), TextAnchor.MiddleLeft);
                rows.Add((bg, bar, text));
            }
        }

        void BuildSpeedometer(Transform root)
        {
            var bottom = new Vector2(0.5f, 0f);
            var gauge = UIKit.Rect(root, "Speedometer", bottom, bottom, new Vector2(0f, 70f), new Vector2(300f, 300f));
            speedometer = gauge;
            UIKit.Image(gauge, UIKit.Circle, new Color(0.05f, 0.06f, 0.1f, 0.6f));

            var glowRt = UIKit.Rect(gauge, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 420f));
            gaugeGlow = UIKit.Image(glowRt, UIKit.SoftGlow, new Color(UIKit.Nitro.r, UIKit.Nitro.g, UIKit.Nitro.b, 0f));
            glowRt.SetAsFirstSibling();

            gaugeFill = MakeArc(gauge, "Track", new Color(1f, 1f, 1f, 0.15f), 0.75f);
            gaugeFill = MakeArc(gauge, "Fill", UIKit.Accent, 0f);

            speedText = UIKit.Label(gauge, "0", 96, UIKit.TextMain, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 14f), new Vector2(280f, 110f), TextAnchor.MiddleCenter, true);
            UIKit.Label(gauge, "KM/H", 28, UIKit.TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -54f), new Vector2(200f, 40f), TextAnchor.MiddleCenter);

            // Armor bar under the gauge
            var armorBg = UIKit.Rect(root, "Armor", bottom, bottom, new Vector2(0f, 26f), new Vector2(340f, 30f));
            armorBar = armorBg;
            UIKit.Image(armorBg, UIKit.RoundedSmall, new Color(0f, 0f, 0f, 0.55f));
            armorFill = UIKit.Stretch(armorBg, "Fill");
            armorFill.offsetMin = new Vector2(4f, 4f);
            armorFill.offsetMax = new Vector2(-4f, -4f);
            armorFillImage = UIKit.Image(armorFill, UIKit.RoundedSmall, UIKit.Good);
            UIKit.Label(armorBg, "ARMOR", 20, UIKit.TextMain, TextAnchor.MiddleCenter);
        }

        static Image MakeArc(RectTransform parent, string name, Color color, float amount)
        {
            var rt = UIKit.Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 280f));
            rt.localEulerAngles = new Vector3(0f, 0f, -45f);   // 270° gauge with the gap at the bottom
            var img = UIKit.Image(rt, UIKit.Ring, color);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Bottom;
            img.fillClockwise = true;
            img.fillAmount = amount;
            return img;
        }

        void BuildAmmo(Transform root)
        {
            var origin = new Vector2(0f, 0f);
            var panel = UIKit.Panel(root, "Ammo", origin, origin, new Vector2(30f, 30f), new Vector2(500f, 210f));
            ammoRoot = panel.gameObject;
            string[] names = { "MISSILES", "MINES", "NITRO" };
            Color[] colors = { UIKit.AccentHot, new Color(0.85f, 0.85f, 0.9f), UIKit.Nitro };
            for (int i = 0; i < 3; i++)
            {
                float y = -24f - i * 60f;
                UIKit.Label(panel.transform, names[i], 30, UIKit.TextMain, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(26f, y), new Vector2(200f, 50f), TextAnchor.MiddleLeft);
                var pips = new Image[8];
                for (int k = 0; k < pips.Length; k++)
                {
                    var p = UIKit.Rect(panel.transform, "Pip", new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(220f + k * 32f, y - 12f), new Vector2(26f, 26f));
                    pips[k] = UIKit.Image(p, UIKit.Circle, colors[i]);
                }
                ammoPips.Add(pips);
            }
        }

        void BuildCenter(Transform root)
        {
            var center = new Vector2(0.5f, 0.5f);
            var cd = UIKit.Rect(root, "Countdown", center, center, new Vector2(0f, 20f), new Vector2(800f, 320f));
            countdownText = UIKit.Label(cd, "", 260, UIKit.TextMain, TextAnchor.MiddleCenter, true);
            countdownPunch = cd.gameObject.AddComponent<Punch>();

            // Start lights: three lamps turn red one by one, then all go green.
            var housing = UIKit.Panel(root, "StartLights", center, center, new Vector2(0f, 210f), new Vector2(430f, 150f),
                new Color(0.05f, 0.05f, 0.07f, 0.92f));
            lights = housing.gameObject;
            lamps = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var lamp = UIKit.Rect(housing.transform, "Lamp" + i, center, center, new Vector2((i - 1) * 135f, 0f),
                    new Vector2(112f, 112f));
                lamps[i] = UIKit.Image(lamp, UIKit.Circle, new Color(0.25f, 0.08f, 0.08f));
            }

            // Track / event banner shown during the countdown
            var req = race.Request;
            string evt = req.IsChampionship
                ? $"{req.Label}  -  RACE {req.ChampionshipRace + 1}/{Championship.Active?.Tracks.Length ?? 1}"
                : $"QUICK RACE  -  {req.Label}";
            var banner = UIKit.Panel(root, "TrackBanner", center, center, new Vector2(0f, 380f), new Vector2(900f, 140f));
            trackBanner = banner.gameObject;
            UIKit.Label(banner.transform, req.Track.Name, 64, UIKit.Accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(880f, 80f), TextAnchor.MiddleCenter, true);
            UIKit.Label(banner.transform, $"{evt}  -  {race.totalLaps} LAPS", 28, UIKit.TextMain, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(880f, 40f), TextAnchor.MiddleCenter);

            warningRoot = UIKit.Panel(root, "Warning", center, center, new Vector2(0f, 60f), new Vector2(640f, 120f),
                new Color(0.75f, 0.08f, 0.08f, 0.85f)).gameObject;
            warningText = UIKit.Label(warningRoot.transform, "", 64, UIKit.TextMain, TextAnchor.MiddleCenter, true);
            warningRoot.SetActive(false);
        }

        // ---------- Update ----------

        void Update()
        {
            if (race == null || player == null) return;

            UpdateLayout();
            UpdateTopLeft();
            UpdateStandings();
            UpdateSpeedometer();
            UpdateAmmo();
            UpdateCountdown();
            UpdateSpeedLines();
            UpdateWarnings();
            UpdateDamageFlash();
            UpdateFeed();

            if (race.State == RaceState.Finished && resultsRoot == null) ShowResults();
            if (resultsRoot != null) UpdateResults();
        }

        /// <summary>Compact HUD while touch controls are shown (phones), full HUD otherwise.</summary>
        void UpdateLayout()
        {
            if (camLabel != null) camLabel.text = "VIEW: " + RaceCamera.ModeNames[(int)RaceCamera.Mode];
            bool want = PlayerCarInput.ShowTouchControls;
            if (compactApplied && want == compact) return;
            compactApplied = true;
            compact = want;
            float s = compact ? 0.78f : 1f;
            foreach (var rt in new[] { positionCard, minimapPanel, speedometer, armorBar })
                if (rt != null) rt.localScale = new Vector3(s, s, 1f);
            if (standingsPanel != null) standingsPanel.gameObject.SetActive(!compact);
            // Between the steering buttons and the pedals, low on the screen.
            if (speedometer != null) speedometer.anchoredPosition = new Vector2(0f, compact ? 40f : 70f);
            if (armorBar != null) armorBar.anchoredPosition = new Vector2(0f, compact ? 14f : 26f);
        }

        void UpdateTopLeft()
        {
            int pos = race.PositionOf(player);
            if (lastPos > 0 && pos != lastPos && race.State == RaceState.Racing)
            {
                posPunch.Play(pos < lastPos ? 1.5f : 1.2f);
                if (pos < lastPos) SoundFX.Overtake();
            }
            lastPos = pos;
            posText.text = $"{pos}<size=56>{UIKit.OrdinalSuffix(pos)}</size>";
            posText.color = pos == 1 ? UIKit.Gold : pos == 2 ? UIKit.Silver : pos == 3 ? UIKit.Bronze : UIKit.TextMain;
            posTotal.text = "/" + race.Racers.Count;
            int lap = Mathf.Clamp(player.Progress.Lap, 1, race.totalLaps);
            lapText.text = $"{lap}/{race.totalLaps}";
            timeText.text = UIKit.Time(race.RaceTime);
            float best = race.BestLapTime;
            bestText.text = best > 0f ? "BEST LAP " + UIKit.Time(best) : "";
        }

        void UpdateStandings()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (i >= race.Standings.Count) { row.text.text = ""; continue; }
                var r = race.Standings[i];
                string status = r.Finished ? "  <color=#9CF29C>FIN</color>" : r.Health.IsWrecked ? "  <color=#FF6060>WRECK</color>" : "";
                row.text.text = $"{i + 1}  {r.Name.ToUpperInvariant()}{status}";
                row.text.color = r.IsPlayer ? UIKit.Accent : UIKit.TextMain;
                row.bar.color = r.Color;
                row.bg.color = r.IsPlayer ? new Color(1f, 0.78f, 0.1f, 0.18f) : new Color(1f, 1f, 1f, 0f);
            }
        }

        void UpdateSpeedometer()
        {
            var car = player.Car;
            float speed01 = Mathf.Clamp01(Mathf.Abs(car.ForwardSpeed) / (car.maxSpeed * car.boostMultiplier));
            gaugeFill.fillAmount = Mathf.Lerp(gaugeFill.fillAmount, 0.75f * speed01, 1f - Mathf.Exp(-12f * Time.deltaTime));
            gaugeFill.color = car.IsBoosting ? UIKit.Nitro : Color.Lerp(UIKit.Accent, UIKit.AccentHot, speed01);
            var g = gaugeGlow.color;
            g.a = Mathf.MoveTowards(g.a, car.IsBoosting ? 0.6f : 0f, Time.deltaTime * 3f);
            gaugeGlow.color = g;
            speedText.text = Mathf.RoundToInt(car.SpeedKmh).ToString();

            float a = player.Health.Armor01;
            armorFill.anchorMax = new Vector2(Mathf.Max(a, 0.001f), 1f);
            armorFillImage.color = a > 0.5f ? UIKit.Good : a > 0.25f ? UIKit.Accent : UIKit.Bad;
            armorFillImage.enabled = a > 0.01f;
        }

        void UpdateAmmo()
        {
            ammoRoot.SetActive(!PlayerCarInput.ShowTouchControls);
            if (!ammoRoot.activeSelf) return;
            var w = player.Weapons;
            SetPips(ammoPips[0], w.Missiles, w.missilesPerLap);
            SetPips(ammoPips[1], w.Mines, w.minesPerLap);
            SetPips(ammoPips[2], w.Boosts, w.boostsPerLap);
        }

        static void SetPips(Image[] pips, int have, int max)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].enabled = i < max;
                var c = pips[i].color;
                c.a = i < have ? 1f : 0.2f;
                pips[i].color = c;
            }
        }

        static readonly Color LampOff = new Color(0.25f, 0.08f, 0.08f);
        static readonly Color LampRed = new Color(1f, 0.15f, 0.1f);
        static readonly Color LampGreen = new Color(0.25f, 1f, 0.35f);

        void UpdateCountdown()
        {
            if (race.State == RaceState.Countdown)
            {
                // 3 -> one red lamp, 2 -> two, 1 -> three.
                int n = Mathf.Clamp(Mathf.CeilToInt(race.Countdown), 1, 3);
                if (n != lastCountdown)
                {
                    lastCountdown = n;
                    for (int i = 0; i < lamps.Length; i++) lamps[i].color = i < 4 - n ? LampRed : LampOff;
                    SoundFX.CountdownBeep();
                }
                goTimer = 1.2f;
                return;
            }

            if (trackBanner.activeSelf) trackBanner.SetActive(false);
            if (goTimer <= 0f) return;

            if (lastCountdown != 0)
            {
                lastCountdown = 0;
                foreach (var lamp in lamps) lamp.color = LampGreen;
                countdownText.text = "GO!";
                countdownText.color = UIKit.Accent;
                countdownPunch.Play(2f);
                SoundFX.GoBeep();
            }
            goTimer -= Time.deltaTime;
            var c = countdownText.color;
            c.a = Mathf.Clamp01(goTimer / 0.4f);
            countdownText.color = c;
            if (goTimer <= 0f)
            {
                countdownText.text = "";
                lights.SetActive(false);
            }
        }

        void UpdateSpeedLines()
        {
            var car = player.Car;
            float speed01 = Mathf.Clamp01(Mathf.Abs(car.ForwardSpeed) / Mathf.Max(car.maxSpeed, 1f));
            float target = race.State != RaceState.Racing ? 0f
                : car.IsBoosting ? 0.55f
                : Mathf.Clamp01((speed01 - 0.85f) / 0.15f) * 0.18f;
            var col = speedLines.color;
            col.a = Mathf.MoveTowards(col.a, target, Time.deltaTime * 3f);
            speedLines.color = col;
            if (col.a > 0.01f)
            {
                // Flicker: small random spin and scale each frame.
                speedLines.rectTransform.localEulerAngles = new Vector3(0f, 0f, Random.Range(-4f, 4f));
                speedLines.rectTransform.localScale = Vector3.one * Random.Range(1f, 1.06f);
            }
        }

        void UpdateWarnings()
        {
            string msg = null;
            if (race.State == RaceState.Racing)
            {
                if (player.Health.IsWrecked) msg = "WRECKED!";
                else if (player.Progress.WrongWay) msg = "WRONG WAY!";
            }
            bool show = msg != null && (msg == "WRECKED!" || Mathf.Repeat(Time.time * 2.5f, 1f) > 0.3f);
            warningRoot.SetActive(show);
            if (show) warningText.text = msg;

            if (!finalLapShown && race.State == RaceState.Racing && race.totalLaps > 1 && player.Progress.Lap == race.totalLaps)
            {
                finalLapShown = true;
                finalLapTimer = 2.5f;
                finalLapBanner.SetActive(true);
                finalLapBanner.GetComponent<Punch>().Play(1.4f);
            }
            if (finalLapTimer > 0f)
            {
                finalLapTimer -= Time.deltaTime;
                if (finalLapTimer <= 0f) finalLapBanner.SetActive(false);
            }
        }

        void UpdateDamageFlash()
        {
            float armor = player.Health.Armor;
            if (armor < lastArmor - 0.5f) flash = Mathf.Clamp01(flash + (lastArmor - armor) / 30f + 0.3f);
            lastArmor = armor;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 1.8f);
            var c = damageFlash.color;
            c.a = flash * 0.7f;
            damageFlash.color = c;
        }

        // ---------- Kill feed ----------

        void OnWrecked(CarHealth victim, CarHealth attacker)
        {
            if (race == null) return;
            var v = race.RacerFor(victim);
            var a = attacker != null ? race.RacerFor(attacker) : null;
            if (v == null) return;

            if (a != null && a.IsPlayer && !v.IsPlayer) AddFeed($"YOU WRECKED {v.Name.ToUpperInvariant()}  +{UIKit.Money(RaceManager.KillBonus)}", UIKit.Accent);
            else if (v.IsPlayer) AddFeed(a != null ? $"{a.Name.ToUpperInvariant()} WRECKED YOU!" : "YOU WRECKED YOURSELF!", UIKit.Bad);
            else if (a != null) AddFeed($"{a.Name.ToUpperInvariant()} WRECKED {v.Name.ToUpperInvariant()}", UIKit.TextMain);
        }

        void AddFeed(string message, Color color)
        {
            var item = UIKit.Panel(feedRoot, "Feed", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(Mathf.Max(420f, message.Length * 24f), 70f), UIKit.PanelDark);
            UIKit.Label(item.transform, message, 34, color, TextAnchor.MiddleCenter, true);
            item.gameObject.AddComponent<Punch>().Play(1.25f);
            feed.Insert(0, (item.gameObject, Time.time));
            if (feed.Count > 4)
            {
                Destroy(feed[feed.Count - 1].go);
                feed.RemoveAt(feed.Count - 1);
            }
        }

        void UpdateFeed()
        {
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                var (go, t) = feed[i];
                float age = Time.time - t;
                if (age > 3f || go == null)
                {
                    if (go) Destroy(go);
                    feed.RemoveAt(i);
                    continue;
                }
                var rt = (RectTransform)go.transform;
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, new Vector2(0f, -i * 80f), 1f - Mathf.Exp(-12f * Time.deltaTime));
                var cg = go.GetComponent<CanvasGroup>();
                if (cg == null) cg = go.AddComponent<CanvasGroup>();
                cg.alpha = Mathf.Clamp01((3f - age) / 0.5f);
            }
        }

        // ---------- Pause ----------

        void SetPaused(bool paused)
        {
            if (race.State == RaceState.Finished) return;
            race.SetPaused(paused);
        }

        void LateUpdate()
        {
            // Keep the pause overlay in sync with the race (Esc/P/back button can pause too).
            if (race.Paused && pauseRoot == null) ShowPause();
            else if (!race.Paused && pauseRoot != null) { Destroy(pauseRoot); pauseRoot = null; }
        }

        void ShowPause()
        {
            var root = UIKit.Stretch(canvas.transform, "Pause");
            pauseRoot = root.gameObject;
            UIKit.Image(root, null, new Color(0f, 0f, 0f, 0.6f), true);   // blocks clicks to the HUD
            BuildPauseMenu(root);
        }

        void BuildPauseMenu(RectTransform root)
        {
            var menu = UIKit.Stretch(root, "Menu");
            menu.gameObject.AddComponent<CanvasGroup>();
            menu.gameObject.AddComponent<FadeIn>();

            var c = new Vector2(0.5f, 0.5f);
            var panel = UIKit.Panel(menu, "Panel", c, c, Vector2.zero, new Vector2(640f, 700f)).rectTransform;
            UIKit.Label(panel, "PAUSED", 76, UIKit.Accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(600f, 100f), TextAnchor.MiddleCenter, true);

            var top = new Vector2(0.5f, 1f);
            var size = new Vector2(480f, 100f);
            var dark = new Color(0.1f, 0.1f, 0.12f);
            UIKit.Button(panel, "RESUME", top, top, new Vector2(0f, -160f), size, () => race.SetPaused(false), UIKit.Accent, dark, 48);
            UIKit.Button(panel, "RESTART", top, top, new Vector2(0f, -280f), size, race.Restart, fontSize: 44);
            UIKit.Button(panel, "SETTINGS", top, top, new Vector2(0f, -400f), size, () =>
            {
                Destroy(menu.gameObject);
                SettingsPanel.Build(root, () => BuildPauseMenu(root));
            }, fontSize: 44);
            UIKit.Button(panel, "MAIN MENU", top, top, new Vector2(0f, -520f), size, race.ExitToMenu, fontSize: 44);
        }

        // ---------- Results ----------

        void ShowResults()
        {
            var root = UIKit.Stretch(canvas.transform, "Results");
            resultsRoot = root.gameObject;
            UIKit.Image(root, null, new Color(0f, 0f, 0f, 0.55f), true);
            var content = UIKit.Stretch(root, "Content");
            content.gameObject.AddComponent<CanvasGroup>();
            content.gameObject.AddComponent<FadeIn>().duration = 0.4f;

            int n = race.Racers.Count;
            float listH = n * 58f;
            var c = new Vector2(0.5f, 0.5f);
            float extra = race.ChampionshipPoints >= 0 ? 80f : 0f;
            var panel = UIKit.Panel(content, "Panel", c, c, Vector2.zero, new Vector2(1400f, 330f + listH + extra)).rectTransform;

            int place = player.FinishPlace;
            string title = place == 1 ? "VICTORY!" : $"YOU FINISHED {UIKit.Ordinal(place)}";
            UIKit.Label(panel, title, 90, place <= 3 ? UIKit.Accent : UIKit.TextMain, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(1300f, 120f), TextAnchor.MiddleCenter, true);

            // Finishing order (left)
            for (int i = 0; i < n; i++)
            {
                var left = UIKit.Label(panel, "", 34, UIKit.TextMain, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(60f, -160f - i * 58f), new Vector2(560f, 56f), TextAnchor.MiddleLeft);
                var right = UIKit.Label(panel, "", 34, UIKit.TextMain, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(620f, -160f - i * 58f), new Vector2(220f, 56f), TextAnchor.MiddleRight);
                resultRows.Add((left, right));
            }
            UpdateResults();

            // Earnings (right)
            var earn = UIKit.Panel(panel, "Earnings", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -160f),
                new Vector2(480f, 330f), UIKit.PanelLight).rectTransform;
            string diff = race.Request.Label;
            string lines =
                $"PRIZE ({diff})\nWRECKS x{player.Kills}\nPICKUPS\n\nTOTAL\nBANK";
            string values =
                $"{UIKit.Money(race.EarnedPrize)}\n{UIKit.Money(race.EarnedKills)}\n{UIKit.Money(race.EarnedPickups)}\n\n" +
                $"<color=#FFC81A>{UIKit.Money(race.EarnedThisRace)}</color>\n{UIKit.Money(RaceManager.TotalMoney)}";
            var l = UIKit.Label(earn, lines, 32, UIKit.TextDim, TextAnchor.MiddleLeft);
            ((RectTransform)l.transform).offsetMin = new Vector2(30f, 0f);
            l.lineSpacing = 1.15f;
            var v = UIKit.Label(earn, values, 32, UIKit.TextMain, TextAnchor.MiddleRight, true);
            ((RectTransform)v.transform).offsetMax = new Vector2(-30f, 0f);
            v.lineSpacing = 1.15f;

            var bottom = new Vector2(0.5f, 0f);
            if (race.ChampionshipPoints >= 0)
            {
                // Championship: points earned, then back to the season hub.
                UIKit.Label(panel, $"+{race.ChampionshipPoints} CHAMPIONSHIP POINTS", 44, UIKit.Accent, bottom, bottom,
                    new Vector2(0f, 160f), new Vector2(1300f, 60f), TextAnchor.MiddleCenter, true);
                UIKit.Button(panel, "CONTINUE", bottom, bottom, new Vector2(0f, 36f), new Vector2(520f, 110f),
                    race.ExitToMenu, UIKit.Accent, new Color(0.1f, 0.1f, 0.12f), 50);
            }
            else
            {
                UIKit.Button(panel, "MAIN MENU", bottom, new Vector2(1f, 0f), new Vector2(-20f, 36f), new Vector2(420f, 110f),
                    race.ExitToMenu, fontSize: 44);
                UIKit.Button(panel, "RACE AGAIN", bottom, new Vector2(0f, 0f), new Vector2(20f, 36f), new Vector2(420f, 110f),
                    race.Restart, UIKit.Accent, new Color(0.1f, 0.1f, 0.12f), 48);
            }
        }

        void UpdateResults()
        {
            if (Time.unscaledTime < resultsRefresh) return;
            resultsRefresh = Time.unscaledTime + 0.25f;
            for (int i = 0; i < resultRows.Count && i < race.Standings.Count; i++)
            {
                var r = race.Standings[i];
                string color = r.IsPlayer ? "FFC81A" : ColorUtility.ToHtmlStringRGB(Color.Lerp(r.Color, Color.white, 0.35f));
                var (left, right) = resultRows[i];
                left.text = $"{UIKit.Ordinal(i + 1)}   <color=#{color}>{r.Name.ToUpperInvariant()}</color>";
                right.text = r.Finished ? UIKit.Time(r.FinishTime) : "racing...";
                right.color = r.Finished ? UIKit.TextMain : UIKit.TextDim;
            }
        }
    }
}
