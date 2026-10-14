using UnityEngine;
using UnityEngine.UI;
using RacingGame.Core;
using RacingGame.Race;
using RacingGame.Track;

namespace RacingGame.UI
{
    /// <summary>
    /// Championship menus: league select, season hub (calendar + standings + next race),
    /// and the season finale.
    /// </summary>
    public class ChampionshipScreen : MonoBehaviour
    {
        MainMenu menu;
        RectTransform content;

        static readonly Color Dark = new Color(0.1f, 0.1f, 0.12f);

        public void Init(MainMenu owner)
        {
            menu = owner;
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            if (Championship.SeasonComplete) ShowFinale();
            else if (Championship.HasActiveSeason) ShowHub();
            else ShowLeagues();
        }

        RectTransform NewPage(string title)
        {
            if (content) Destroy(content.gameObject);
            content = UIKit.Stretch(transform, "Page");
            content.gameObject.AddComponent<CanvasGroup>();
            content.gameObject.AddComponent<FadeIn>();
            UIKit.Label(content, title, 90, UIKit.Accent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -30f),
                new Vector2(1400f, 120f), TextAnchor.MiddleLeft, true);
            return content;
        }

        // ---------- League select ----------

        void ShowLeagues()
        {
            var page = NewPage("CHAMPIONSHIP");
            UIKit.Label(page, "Win a league to unlock the next one. Upgrade your car in the garage between races!", 30,
                UIKit.TextDim, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, -140f), new Vector2(1600f, 44f),
                TextAnchor.MiddleLeft);

            int n = Championship.Leagues.Count;
            const float cardW = 400f, gap = 30f;
            float totalW = n * cardW + (n - 1) * gap;
            for (int i = 0; i < n; i++)
            {
                float x = -totalW * 0.5f + i * (cardW + gap) + cardW * 0.5f;
                LeagueCard(page, i, new Vector2(x, -30f), new Vector2(cardW, 600f));
            }

            UIKit.Button(page, "BACK", Vector2.zero, Vector2.zero, new Vector2(50f, 40f), new Vector2(300f, 100f),
                () => menu.ShowHome(), fontSize: 44);
        }

        void LeagueCard(RectTransform parent, int index, Vector2 pos, Vector2 size)
        {
            var l = Championship.Leagues[index];
            bool locked = index > Championship.Unlocked;
            bool won = Championship.IsWon(index);
            bool active = Championship.ActiveLeague == index;

            var c = new Vector2(0.5f, 0.5f);
            var card = UIKit.Panel(parent, "League" + index, c, c, pos, size,
                locked ? new Color(0.05f, 0.05f, 0.08f, 0.7f) : UIKit.PanelDark).rectTransform;
            var top = new Vector2(0.5f, 1f);

            // Colored header strip
            var strip = UIKit.Rect(card, "Strip", top, top, new Vector2(0f, -16f), new Vector2(size.x - 32f, 12f));
            UIKit.Image(strip, UIKit.RoundedSmall, locked ? new Color(1f, 1f, 1f, 0.15f) : Color.Lerp(UIKit.Accent, UIKit.AccentHot, index / 3f));

            UIKit.Label(card, l.Name, 44, locked ? UIKit.TextDim : UIKit.TextMain, top, top, new Vector2(0f, -50f),
                new Vector2(size.x - 20f, 60f), TextAnchor.MiddleCenter, true);
            UIKit.Label(card, $"{l.Tracks.Length} RACES  |  {l.Laps} LAPS", 28, UIKit.TextDim, top, top, new Vector2(0f, -116f),
                new Vector2(size.x, 40f), TextAnchor.MiddleCenter);

            string tracks = "";
            foreach (int t in l.Tracks) tracks += TrackLibrary.Get(t).Name + "\n";
            var tl = UIKit.Label(card, tracks.TrimEnd(), 26, locked ? UIKit.TextDim : UIKit.TextMain, top, top,
                new Vector2(0f, -170f), new Vector2(size.x, 170f), TextAnchor.UpperCenter);
            tl.lineSpacing = 1.2f;

            UIKit.Label(card, $"PRIZES x{l.PrizeMultiplier:0.#}", 32, UIKit.Accent, top, top, new Vector2(0f, -360f),
                new Vector2(size.x, 44f), TextAnchor.MiddleCenter, true);
            UIKit.Label(card, $"WIN BONUS {UIKit.Money(l.WinBonus)}", 28, UIKit.TextMain, top, top, new Vector2(0f, -408f),
                new Vector2(size.x, 40f), TextAnchor.MiddleCenter);

            var bottom = new Vector2(0.5f, 0f);
            var btnSize = new Vector2(size.x - 60f, 96f);
            if (locked)
            {
                UIKit.Label(card, "LOCKED\nwin the previous league", 28, UIKit.TextDim, bottom, bottom, new Vector2(0f, 40f),
                    btnSize, TextAnchor.MiddleCenter, true);
            }
            else if (active)
            {
                UIKit.Button(card, "CONTINUE", bottom, bottom, new Vector2(0f, 30f), btnSize, ShowHub, UIKit.Accent, Dark, 44);
            }
            else
            {
                var enter = UIKit.Button(card, won ? "RACE AGAIN" : "ENTER", bottom, bottom, new Vector2(0f, 30f), btnSize, () =>
                {
                    Championship.StartSeason(index);
                    ShowHub();
                }, won ? UIKit.ButtonDark : UIKit.Accent, won ? UIKit.TextMain : Dark, 44);
                // Only one season at a time - finish or abandon the current one first.
                enter.interactable = !Championship.HasActiveSeason;
            }

            if (won)
            {
                var badge = UIKit.Panel(card, "Won", new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -30f),
                    new Vector2(150f, 56f), UIKit.Good);
                UIKit.Label(badge.transform, "WON", 32, Dark, TextAnchor.MiddleCenter, true);
                badge.rectTransform.localEulerAngles = new Vector3(0f, 0f, -12f);
            }
        }

        // ---------- Season hub ----------

        void ShowHub()
        {
            var l = Championship.Active;
            if (l == null) { ShowLeagues(); return; }
            if (Championship.SeasonComplete) { ShowFinale(); return; }

            var page = NewPage(l.Name);
            int next = Championship.NextRace;
            UIKit.Label(page, $"RACE {next + 1} OF {l.Tracks.Length}", 34, UIKit.TextMain, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(64f, -140f), new Vector2(900f, 44f), TextAnchor.MiddleLeft, true);

            // Calendar (left)
            var cal = UIKit.Panel(page, "Calendar", new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 10f),
                new Vector2(780f, 560f)).rectTransform;
            UIKit.Label(cal, "CALENDAR", 40, UIKit.Accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f),
                new Vector2(700f, 60f), TextAnchor.MiddleCenter, true);
            for (int i = 0; i < l.Tracks.Length; i++)
            {
                var track = TrackLibrary.Get(l.Tracks[i]);
                float y = -100f - i * 108f;
                bool done = i < next, current = i == next;
                var row = UIKit.Panel(cal, "Race" + i, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y),
                    new Vector2(720f, 96f), current ? new Color(1f, 0.78f, 0.1f, 0.22f) : UIKit.PanelLight).rectTransform;

                var swatch = UIKit.Rect(row, "Theme", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(64f, 64f));
                UIKit.Image(swatch, UIKit.Rounded, track.Theme.Ground);
                UIKit.Label(row, track.Name, 34, UIKit.TextMain, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, 14f),
                    new Vector2(400f, 44f), TextAnchor.MiddleLeft, true);
                UIKit.Label(row, track.Subtitle, 22, UIKit.TextDim, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, -22f),
                    new Vector2(400f, 30f), TextAnchor.MiddleLeft);

                string status = done
                    ? $"{UIKit.Ordinal(Championship.RaceResult(i))}  <color=#FFC81A>+{Championship.RacePoints(i)}</color>"
                    : current ? "NEXT" : "";
                UIKit.Label(row, status, 34, current ? UIKit.Accent : UIKit.TextMain, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-24f, 0f), new Vector2(220f, 50f), TextAnchor.MiddleRight, true);
            }

            // Standings (right)
            StandingsPanel(page, new Vector2(20f, 10f), "STANDINGS");

            // Actions
            var bottom = new Vector2(0.5f, 0f);
            UIKit.Button(page, "NEXT RACE", bottom, bottom, new Vector2(0f, 40f), new Vector2(520f, 110f), StartNextRace,
                UIKit.Accent, Dark, 54);
            UIKit.Button(page, "GARAGE", bottom, new Vector2(1f, 0f), new Vector2(-300f, 40f), new Vector2(300f, 100f),
                () => menu.OpenGarage(fromChampionship: true), fontSize: 42);
            UIKit.Button(page, "ABANDON", bottom, new Vector2(0f, 0f), new Vector2(300f, 40f), new Vector2(300f, 100f), () =>
            {
                Championship.Abandon();
                ShowLeagues();
            }, new Color(0.45f, 0.12f, 0.12f, 0.95f), fontSize: 40);
            UIKit.Button(page, "BACK", Vector2.zero, Vector2.zero, new Vector2(50f, 40f), new Vector2(240f, 100f),
                () => menu.ShowHome(), fontSize: 40);
        }

        void StandingsPanel(RectTransform page, Vector2 pos, string title)
        {
            var standings = Championship.Standings();
            var panel = UIKit.Panel(page, "Standings", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), pos,
                new Vector2(620f, 560f)).rectTransform;
            UIKit.Label(panel, title, 40, UIKit.Accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f),
                new Vector2(560f, 60f), TextAnchor.MiddleCenter, true);
            for (int i = 0; i < standings.Count && i < 8; i++)
            {
                var e = standings[i];
                float y = -96f - i * 56f;
                var row = UIKit.Rect(panel, "Row" + i, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y),
                    new Vector2(560f, 50f));
                if (e.IsPlayer) UIKit.Image(row, UIKit.RoundedSmall, new Color(1f, 0.78f, 0.1f, 0.2f));
                var color = e.IsPlayer ? UIKit.Accent : UIKit.TextMain;
                UIKit.Label(row, $"{i + 1}.  {e.Name.ToUpperInvariant()}", 32, color, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(20f, 0f), new Vector2(400f, 50f), TextAnchor.MiddleLeft);
                UIKit.Label(row, $"{e.Points} PTS", 32, color, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-20f, 0f), new Vector2(160f, 50f), TextAnchor.MiddleRight, true);
            }
        }

        void StartNextRace()
        {
            var request = Championship.NextRaceRequest();
            if (request == null) { ShowFinale(); return; }
            RaceRequest.Current = request;
            MainMenu.PendingScreen = MainMenu.MenuScreen.Championship;   // come back to the hub afterwards
            GameFlow.StartRace();
        }

        // ---------- Finale ----------

        void ShowFinale()
        {
            var l = Championship.Active;
            if (l == null) { ShowLeagues(); return; }
            int league = Championship.ActiveLeague;
            int position = Championship.PlayerSeasonPosition();
            int bonus = Championship.SeasonBonus(league, position);
            bool willUnlock = position == 1 && league + 1 < Championship.Leagues.Count && Championship.Unlocked < league + 1;

            var page = NewPage(l.Name + " - FINAL");
            StandingsPanel(page, new Vector2(20f, 10f), "FINAL STANDINGS");

            var c = new Vector2(0.5f, 0.5f);
            var card = UIKit.Panel(page, "Result", c, new Vector2(1f, 0.5f), new Vector2(-20f, 10f), new Vector2(780f, 560f)).rectTransform;
            var top = new Vector2(0.5f, 1f);
            string headline = position == 1 ? "CHAMPION!" : $"{UIKit.Ordinal(position)} OVERALL";
            var h = UIKit.Label(card, headline, 96, position <= 3 ? UIKit.Accent : UIKit.TextMain, top, top, new Vector2(0f, -60f),
                new Vector2(740f, 130f), TextAnchor.MiddleCenter, true);
            h.gameObject.AddComponent<Punch>().Play(1.5f);

            string body = bonus > 0 ? $"SEASON BONUS  <color=#FFC81A>{UIKit.Money(bonus)}</color>" : "NO BONUS THIS TIME";
            UIKit.Label(card, body, 40, UIKit.TextMain, top, top, new Vector2(0f, -230f), new Vector2(740f, 60f),
                TextAnchor.MiddleCenter, true);
            string next = willUnlock
                ? $"{Championship.Leagues[league + 1].Name} UNLOCKED!"
                : position == 1 ? "LEAGUE CONQUERED" : "Win the league to unlock the next one.";
            UIKit.Label(card, next, 34, willUnlock ? UIKit.Good : UIKit.TextDim, top, top, new Vector2(0f, -310f),
                new Vector2(740f, 60f), TextAnchor.MiddleCenter, willUnlock);

            UIKit.Button(page, "COLLECT", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(520f, 110f), () =>
            {
                Championship.FinishSeason();
                menu.RefreshBank();
                ShowLeagues();
            }, UIKit.Accent, Dark, 54);
        }
    }
}
