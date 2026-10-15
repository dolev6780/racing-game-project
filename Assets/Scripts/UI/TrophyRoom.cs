using UnityEngine;
using UnityEngine.UI;
using RacingGame.Progress;
using RacingGame.Race;
using RacingGame.Track;

namespace RacingGame.UI
{
    /// <summary>Trophy cabinet (league cups), achievement list with progress, career stats and records.</summary>
    public class TrophyRoom : MonoBehaviour
    {
        MainMenu menu;

        public void Init(MainMenu owner)
        {
            menu = owner;
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            gameObject.AddComponent<CanvasGroup>();
            gameObject.AddComponent<FadeIn>();

            UIKit.Label(root, "TROPHY ROOM", 90, UIKit.Accent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -30f),
                new Vector2(1000f, 120f), TextAnchor.MiddleLeft, true);
            UIKit.Label(root, $"{Achievements.UnlockedCount} / {Achievements.All.Count} ACHIEVEMENTS", 34, UIKit.TextMain,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-440f, -62f), new Vector2(700f, 50f), TextAnchor.MiddleRight, true);

            BuildCabinet(root);
            BuildAchievements(root);
            BuildStats(root);

            UIKit.Button(root, "BACK", Vector2.zero, Vector2.zero, new Vector2(50f, 30f), new Vector2(260f, 90f),
                () => menu.ShowHome(), fontSize: 40);
        }

        // ---------- League trophies ----------

        void BuildCabinet(RectTransform root)
        {
            var top = new Vector2(0.5f, 1f);
            var shelf = UIKit.Panel(root, "Cabinet", top, top, new Vector2(0f, -150f), new Vector2(1760f, 250f)).rectTransform;
            int n = Championship.Leagues.Count;
            float slot = 1760f / n;
            for (int i = 0; i < n; i++)
            {
                int best = Championship.BestFinish(i);
                Color cup = best == 1 ? UIKit.Gold : best == 2 ? UIKit.Silver : best == 3 ? UIKit.Bronze : new Color(1f, 1f, 1f, 0.1f);
                string status = best == 0 ? "NOT WON YET" : best == 1 ? "CHAMPION" : $"BEST: {UIKit.Ordinal(best)}";

                var holder = UIKit.Rect(shelf, "League" + i, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(slot * (i + 0.5f), 0f), new Vector2(slot, 250f));
                if (best > 0 && best <= 3)
                {
                    var glow = UIKit.Rect(holder, "Glow", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 20f), new Vector2(230f, 230f));
                    UIKit.Image(glow, UIKit.SoftGlow, new Color(cup.r, cup.g, cup.b, 0.35f));
                }
                var icon = UIKit.Rect(holder, "Cup", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(150f, 150f));
                UIKit.TrophyIcon(icon, cup);

                UIKit.Label(holder, Championship.Leagues[i].Name, 30, best > 0 ? UIKit.TextMain : UIKit.TextDim,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(slot, 40f), TextAnchor.MiddleCenter, true);
                UIKit.Label(holder, status, 24, best == 1 ? UIKit.Accent : UIKit.TextDim,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(slot, 34f), TextAnchor.MiddleCenter);
            }
        }

        // ---------- Achievements (scrollable) ----------

        void BuildAchievements(RectTransform root)
        {
            var panel = UIKit.Panel(root, "Achievements", new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(150f, 130f),
                new Vector2(1040f, 530f)).rectTransform;
            UIKit.Label(panel, "ACHIEVEMENTS", 38, UIKit.Accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                new Vector2(1000f, 56f), TextAnchor.MiddleCenter, true);

            const float rowH = 100f;
            var content = MakeScrollArea(panel, new Vector2(1000f, 440f), Achievements.All.Count * rowH);

            // Unlocked first, then by progress.
            var list = new System.Collections.Generic.List<AchievementDef>(Achievements.All);
            list.Sort((a, b) => Score(b).CompareTo(Score(a)));

            for (int i = 0; i < list.Count; i++)
                AchievementRow(content, list[i], -i * rowH, rowH - 10f);
        }

        static float Score(AchievementDef a)
        {
            if (Achievements.IsUnlocked(a.Id)) return 2f;
            if (a.Progress == null) return 0f;
            var (cur, target) = a.Progress();
            return target > 0 ? (float)cur / target : 0f;
        }

        void AchievementRow(RectTransform content, AchievementDef a, float y, float h)
        {
            bool unlocked = Achievements.IsUnlocked(a.Id);
            var top = new Vector2(0.5f, 1f);
            var row = UIKit.Panel(content, a.Id, top, top, new Vector2(0f, y), new Vector2(980f, h),
                unlocked ? new Color(1f, 0.78f, 0.1f, 0.16f) : UIKit.PanelLight).rectTransform;

            var icon = UIKit.Rect(row, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(70f, 70f));
            UIKit.Image(icon, UIKit.Circle, unlocked ? new Color(1f, 0.78f, 0.1f, 0.25f) : new Color(1f, 1f, 1f, 0.06f));
            UIKit.TrophyIcon(icon, unlocked ? UIKit.Gold : new Color(1f, 1f, 1f, 0.25f));

            UIKit.Label(row, a.Name, 32, unlocked ? UIKit.TextMain : UIKit.TextDim, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(104f, 16f), new Vector2(520f, 40f), TextAnchor.MiddleLeft, true);
            UIKit.Label(row, a.Description, 22, UIKit.TextDim, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(104f, -20f), new Vector2(560f, 30f), TextAnchor.MiddleLeft);

            // Progress bar for counters
            if (!unlocked && a.Progress != null)
            {
                var (cur, target) = a.Progress();
                if (target > 1)
                {
                    var track = UIKit.Rect(row, "Bar", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-180f, -16f), new Vector2(200f, 14f));
                    UIKit.Image(track, UIKit.RoundedSmall, new Color(1f, 1f, 1f, 0.12f));
                    var fill = UIKit.Stretch(track, "Fill");
                    fill.anchorMax = new Vector2(Mathf.Clamp((float)cur / target, 0.04f, 1f), 1f);
                    UIKit.Image(fill, UIKit.RoundedSmall, UIKit.Accent);
                    UIKit.Label(row, $"{cur:N0}/{target:N0}", 22, UIKit.TextDim, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(-180f, 12f), new Vector2(200f, 28f), TextAnchor.MiddleCenter);
                }
            }

            string reward = unlocked ? "DONE" : UIKit.Money(a.Reward);
            UIKit.Label(row, reward, 30, unlocked ? UIKit.Good : UIKit.Accent, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-20f, 0f), new Vector2(150f, 44f), TextAnchor.MiddleRight, true);
        }

        // ---------- Stats & records ----------

        void BuildStats(RectTransform root)
        {
            var panel = UIKit.Panel(root, "Stats", new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(170f, 130f),
                new Vector2(700f, 530f)).rectTransform;
            UIKit.Label(panel, "CAREER", 38, UIKit.Accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                new Vector2(660f, 56f), TextAnchor.MiddleCenter, true);

            int races = Stats.Get(Stat.Races), wins = Stats.Get(Stat.Wins);
            var lines = new System.Collections.Generic.List<(string, string)>
            {
                ("RACES", races.ToString()),
                ("WINS", $"{wins}  ({(races > 0 ? wins * 100 / races : 0)}%)"),
                ("PODIUMS", Stats.Get(Stat.Podiums).ToString()),
                ("BEST WIN STREAK", Stats.Get(Stat.BestWinStreak).ToString()),
                ("OPPONENTS WRECKED", Stats.Get(Stat.Wrecks).ToString()),
                ("TIMES WRECKED", Stats.Get(Stat.TimesWrecked).ToString()),
                ("MISSILES FIRED", Stats.Get(Stat.MissilesFired).ToString()),
                ("MINE HITS", Stats.Get(Stat.MineHits).ToString()),
                ("NITROS USED", Stats.Get(Stat.NitrosUsed).ToString()),
                ("TOP SPEED", Stats.Get(Stat.TopSpeedKmh) + " KM/H"),
                ("MONEY EARNED", UIKit.Money(Stats.Get(Stat.MoneyEarned))),
                ("", ""),
                ("BEST LAPS", ""),
            };
            foreach (var t in TrackLibrary.Tracks)
            {
                float best = Records.BestLap(t.Name);
                lines.Add(("  " + t.Name, best > 0f ? UIKit.Time(best) : "--"));
            }

            const float rowH = 44f;
            var content = MakeScrollArea(panel, new Vector2(660f, 440f), lines.Count * rowH + 10f);
            for (int i = 0; i < lines.Count; i++)
            {
                var (label, value) = lines[i];
                bool header = label == "BEST LAPS";
                UIKit.Label(content, label, 26, header ? UIKit.Accent : UIKit.TextDim, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(20f, -i * rowH), new Vector2(400f, rowH), TextAnchor.MiddleLeft, header);
                UIKit.Label(content, value, 28, UIKit.TextMain, new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-20f, -i * rowH), new Vector2(260f, rowH), TextAnchor.MiddleRight, true);
            }
        }

        /// <summary>Creates a vertically scrolling area (drag/swipe/mouse wheel) and returns its content rect.</summary>
        static RectTransform MakeScrollArea(RectTransform panel, Vector2 size, float contentHeight)
        {
            var viewport = UIKit.Rect(panel, "Viewport", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), size);
            UIKit.Image(viewport, null, new Color(0f, 0f, 0f, 0.001f), true);   // catches drags
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UIKit.Rect(viewport, "Content", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(size.x, Mathf.Max(contentHeight, size.y)));

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            return content;
        }
    }
}
