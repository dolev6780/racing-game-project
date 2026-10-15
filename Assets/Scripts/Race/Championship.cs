using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RacingGame.Garage;
using RacingGame.Track;

namespace RacingGame.Race
{
    public class LeagueDef
    {
        public string Name;
        public int[] Tracks;           // indices into TrackLibrary, in race order
        public int Laps;
        public int Opponents = 5;
        public float PrizeMultiplier;
        public int WinBonus;           // paid for winning the season (2nd: 40%, 3rd: 20%)
        public float AiTopSpeed, AiArmor;
        public Vector2 AiSpeedMultiplier, AiSkill, AiAggression;
    }

    public struct StandingEntry
    {
        public string Name;
        public int Points;
        public bool IsPlayer;
    }

    /// <summary>
    /// Championship: four leagues of increasing difficulty. Each season is a series of races;
    /// finishing positions earn points. Win a league to unlock the next one.
    /// All progress is saved in PlayerPrefs.
    /// </summary>
    public static class Championship
    {
        public const string PlayerName = "You";
        public static readonly int[] PointsTable = { 10, 7, 5, 3, 2, 1 };

        public static readonly List<LeagueDef> Leagues = new List<LeagueDef>
        {
            new LeagueDef
            {
                Name = "ROOKIE LEAGUE", Tracks = new[] { 0, 1, 2, 3 }, Laps = 3, PrizeMultiplier = 1f, WinBonus = 3000,
                AiTopSpeed = 30f, AiArmor = 90f, AiSpeedMultiplier = new Vector2(0.86f, 0.94f),
                AiSkill = new Vector2(0.3f, 0.6f), AiAggression = new Vector2(0.2f, 0.5f),
            },
            new LeagueDef
            {
                Name = "PRO LEAGUE", Tracks = new[] { 1, 3, 0, 2 }, Laps = 4, PrizeMultiplier = 2f, WinBonus = 6000,
                AiTopSpeed = 33f, AiArmor = 110f, AiSpeedMultiplier = new Vector2(0.92f, 1f),
                AiSkill = new Vector2(0.5f, 0.8f), AiAggression = new Vector2(0.4f, 0.7f),
            },
            new LeagueDef
            {
                Name = "ELITE LEAGUE", Tracks = new[] { 2, 0, 3, 1 }, Laps = 4, PrizeMultiplier = 3.5f, WinBonus = 10000,
                AiTopSpeed = 36f, AiArmor = 130f, AiSpeedMultiplier = new Vector2(0.95f, 1.03f),
                AiSkill = new Vector2(0.65f, 0.95f), AiAggression = new Vector2(0.55f, 0.9f),
            },
            new LeagueDef
            {
                Name = "RIOT MASTERS", Tracks = new[] { 3, 2, 1, 0 }, Laps = 5, PrizeMultiplier = 5f, WinBonus = 20000,
                AiTopSpeed = 39f, AiArmor = 150f, AiSpeedMultiplier = new Vector2(0.98f, 1.06f),
                AiSkill = new Vector2(0.8f, 1f), AiAggression = new Vector2(0.7f, 1f),
            },
        };

        // ---------- Saved state ----------

        /// <summary>Highest league index the player may enter.</summary>
        public static int Unlocked => Mathf.Clamp(PlayerPrefs.GetInt("champ_unlocked", 0), 0, Leagues.Count - 1);
        public static bool IsWon(int league) => PlayerPrefs.GetInt("champ_won_" + league, 0) == 1;
        /// <summary>Best season finish in a league (0 = never completed). Drives the trophy color.</summary>
        public static int BestFinish(int league) => PlayerPrefs.GetInt("champ_best_" + league, 0);

        /// <summary>League of the season in progress, or -1.</summary>
        public static int ActiveLeague => PlayerPrefs.GetInt("champ_active", -1);
        public static bool HasActiveSeason => ActiveLeague >= 0;
        public static LeagueDef Active => HasActiveSeason ? Leagues[ActiveLeague] : null;

        /// <summary>Index of the next race to run in the active season.</summary>
        public static int NextRace => PlayerPrefs.GetInt("champ_race", 0);
        public static bool SeasonComplete => HasActiveSeason && NextRace >= Active.Tracks.Length;

        /// <summary>Player's finishing place in each completed race of the active season.</summary>
        public static int RaceResult(int race) => PlayerPrefs.GetInt("champ_result_" + race, 0);
        public static int RacePoints(int race) => PlayerPrefs.GetInt("champ_respts_" + race, 0);

        public static void StartSeason(int league)
        {
            if (league > Unlocked) return;
            ClearSeason();
            PlayerPrefs.SetInt("champ_active", league);
            PlayerPrefs.SetInt("champ_race", 0);

            // Everyone starts the season on zero points.
            var points = new Dictionary<string, int> { [PlayerName] = 0 };
            for (int i = 0; i < Leagues[league].Opponents; i++) points[RaceManager.OpponentName(i)] = 0;
            SavePoints(points);
            PlayerPrefs.Save();
        }

        public static void Abandon()
        {
            ClearSeason();
            PlayerPrefs.Save();
        }

        static void ClearSeason()
        {
            PlayerPrefs.DeleteKey("champ_active");
            PlayerPrefs.DeleteKey("champ_race");
            PlayerPrefs.DeleteKey("champ_points");
            for (int i = 0; i < 8; i++)
            {
                PlayerPrefs.DeleteKey("champ_result_" + i);
                PlayerPrefs.DeleteKey("champ_respts_" + i);
            }
        }

        // ---------- Points ----------

        static Dictionary<string, int> LoadPoints()
        {
            var d = new Dictionary<string, int>();
            string raw = PlayerPrefs.GetString("champ_points", "");
            foreach (var part in raw.Split(';'))
            {
                int sep = part.LastIndexOf(':');
                if (sep <= 0) continue;
                if (int.TryParse(part.Substring(sep + 1), out int pts)) d[part.Substring(0, sep)] = pts;
            }
            return d;
        }

        static void SavePoints(Dictionary<string, int> d)
        {
            PlayerPrefs.SetString("champ_points", string.Join(";", d.Select(kv => kv.Key + ":" + kv.Value)));
        }

        public static int PointsFor(int place) => place >= 1 && place <= PointsTable.Length ? PointsTable[place - 1] : 0;

        /// <summary>Season standings, best first. Includes every racer that has raced this season.</summary>
        public static List<StandingEntry> Standings()
        {
            return LoadPoints()
                .Select(kv => new StandingEntry { Name = kv.Key, Points = kv.Value, IsPlayer = kv.Key == PlayerName })
                .OrderByDescending(e => e.Points)
                .ThenByDescending(e => e.IsPlayer)   // ties go to the player
                .ToList();
        }

        public static int PlayerSeasonPosition()
        {
            var s = Standings();
            int i = s.FindIndex(e => e.IsPlayer);
            return i < 0 ? s.Count + 1 : i + 1;
        }

        /// <summary>Records a finished championship race. finishOrder = racer names, winner first.</summary>
        public static int RecordRace(int raceIndex, IList<string> finishOrder)
        {
            if (!HasActiveSeason || raceIndex != NextRace) return 0;
            var points = LoadPoints();
            int playerPoints = 0;
            for (int i = 0; i < finishOrder.Count; i++)
            {
                string name = finishOrder[i];
                int pts = PointsFor(i + 1);
                points.TryGetValue(name, out int current);
                points[name] = current + pts;
                if (name == PlayerName)
                {
                    playerPoints = pts;
                    PlayerPrefs.SetInt("champ_result_" + raceIndex, i + 1);
                    PlayerPrefs.SetInt("champ_respts_" + raceIndex, pts);
                }
            }
            SavePoints(points);
            PlayerPrefs.SetInt("champ_race", raceIndex + 1);
            PlayerPrefs.Save();
            return playerPoints;
        }

        // ---------- Season end ----------

        public static int SeasonBonus(int league, int position)
        {
            int bonus = Leagues[league].WinBonus;
            return position == 1 ? bonus : position == 2 ? bonus * 2 / 5 : position == 3 ? bonus / 5 : 0;
        }

        /// <summary>Pays the season bonus, unlocks the next league on a win, and closes the season.</summary>
        public static (int position, int bonus, bool unlockedNext) FinishSeason()
        {
            if (!SeasonComplete) return (0, 0, false);
            int league = ActiveLeague;
            int position = PlayerSeasonPosition();
            int bonus = SeasonBonus(league, position);
            Wallet.Money += bonus;

            int best = BestFinish(league);
            if (best == 0 || position < best) PlayerPrefs.SetInt("champ_best_" + league, position);

            bool unlockedNext = false;
            if (position == 1)
            {
                PlayerPrefs.SetInt("champ_won_" + league, 1);
                if (league + 1 < Leagues.Count && Unlocked < league + 1)
                {
                    PlayerPrefs.SetInt("champ_unlocked", league + 1);
                    unlockedNext = true;
                }
            }
            ClearSeason();
            PlayerPrefs.Save();
            Progress.Achievements.MarkDirty();
            return (position, bonus, unlockedNext);
        }

        // ---------- Race setup ----------

        public static RaceRequest NextRaceRequest()
        {
            var l = Active;
            if (l == null || SeasonComplete) return null;
            return new RaceRequest
            {
                Track = TrackLibrary.Get(l.Tracks[NextRace]),
                Laps = l.Laps,
                Opponents = l.Opponents,
                AiTopSpeed = l.AiTopSpeed,
                AiArmor = l.AiArmor,
                AiSpeedMultiplier = l.AiSpeedMultiplier,
                AiSkill = l.AiSkill,
                AiAggression = l.AiAggression,
                PrizeMultiplier = l.PrizeMultiplier,
                Label = l.Name,
                IsChampionship = true,
                ChampionshipRace = NextRace,
            };
        }
    }
}
