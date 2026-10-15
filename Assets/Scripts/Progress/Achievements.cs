using System;
using System.Collections.Generic;
using UnityEngine;
using RacingGame.Garage;
using RacingGame.Race;

namespace RacingGame.Progress
{
    public enum AchievementCategory { Racing, Combat, Career, Championship }

    public class AchievementDef
    {
        public string Id;
        public string Name;
        public string Description;
        public int Reward;
        public AchievementCategory Category;
        /// <summary>Optional: current/target progress for counters. Null = one-off event.</summary>
        public Func<(int current, int target)> Progress;
        /// <summary>Optional: condition checked automatically. Null = unlocked by code (race events).</summary>
        public Func<bool> Condition;
    }

    /// <summary>
    /// 26 achievements with money rewards. Counter-based ones are checked automatically when
    /// stats change; race-moment ones (rampage, comeback...) are unlocked by RaceManager.
    /// </summary>
    public static class Achievements
    {
        public static event Action<AchievementDef> Unlocked;

        static bool dirty = true;
        public static void MarkDirty() => dirty = true;

        public static readonly List<AchievementDef> All = new List<AchievementDef>
        {
            // ----- Racing -----
            Counter("first_race", "ROLLING THUNDER", "Finish your first race", 200, AchievementCategory.Racing, Stat.Races, 1),
            Counter("first_win", "WINNER", "Win a race", 500, AchievementCategory.Racing, Stat.Wins, 1),
            Counter("wins_10", "SPEED KING", "Win 10 races", 2000, AchievementCategory.Racing, Stat.Wins, 10),
            Counter("podiums_25", "PODIUM REGULAR", "Finish in the top 3 twenty-five times", 2000, AchievementCategory.Racing, Stat.Podiums, 25),
            Counter("streak_3", "HAT TRICK", "Win 3 races in a row", 1500, AchievementCategory.Racing, Stat.BestWinStreak, 3),
            Event("comeback", "COMEBACK KID", "Win a race after being in last place", 1500, AchievementCategory.Racing),
            Event("flawless", "UNTOUCHABLE", "Win a race without getting wrecked", 1000, AchievementCategory.Racing),
            Counter("speed_150", "TON FIFTY", "Reach 150 km/h", 500, AchievementCategory.Racing, Stat.TopSpeedKmh, 150),
            Counter("speed_180", "LUDICROUS SPEED", "Reach 180 km/h", 1500, AchievementCategory.Racing, Stat.TopSpeedKmh, 180),
            new AchievementDef
            {
                Id = "all_tracks", Name = "WORLD TOUR", Description = "Race on every track", Reward = 800,
                Category = AchievementCategory.Racing,
                Progress = () => (Stats.CountBits(Stat.TracksRaced), Track.TrackLibrary.Tracks.Count),
            },

            // ----- Combat -----
            Counter("first_wreck", "SCRAP METAL", "Wreck an opponent", 300, AchievementCategory.Combat, Stat.Wrecks, 1),
            Counter("wrecks_50", "DEMOLITION CREW", "Wreck 50 opponents", 2500, AchievementCategory.Combat, Stat.Wrecks, 50),
            Event("rampage", "RAMPAGE", "Wreck 3 opponents in one race", 1000, AchievementCategory.Combat),
            Counter("missiles_100", "TRIGGER HAPPY", "Fire 100 missiles", 800, AchievementCategory.Combat, Stat.MissilesFired, 100),
            Counter("mine_hits_10", "MINESWEEPER", "Hit opponents with your mines 10 times", 800, AchievementCategory.Combat, Stat.MineHits, 10),
            Counter("nitro_50", "NITRO JUNKIE", "Use nitro 50 times", 600, AchievementCategory.Combat, Stat.NitrosUsed, 50),

            // ----- Career -----
            Counter("pickups_50", "MONEY GRABBER", "Collect 50 money pickups", 800, AchievementCategory.Career, Stat.MoneyPickups, 50),
            Counter("first_upgrade", "GREASE MONKEY", "Buy your first upgrade", 300, AchievementCategory.Career, Stat.UpgradesBought, 1),
            new AchievementDef
            {
                Id = "max_upgrade", Name = "MAXED OUT", Description = "Max out any upgrade", Reward = 1000,
                Category = AchievementCategory.Career, Condition = AnyUpgradeMaxed,
            },
            Counter("new_ride", "NEW RIDE", "Buy a new vehicle", 500, AchievementCategory.Career, Stat.VehiclesBought, 1),
            new AchievementDef
            {
                Id = "collector", Name = "COLLECTOR", Description = "Own every vehicle", Reward = 5000,
                Category = AchievementCategory.Career, Progress = () => (OwnedVehicles(), VehicleCatalog.Vehicles.Count),
            },
            new AchievementDef
            {
                Id = "rich", Name = "HIGH ROLLER", Description = "Have $25,000 in the bank", Reward = 2500,
                Category = AchievementCategory.Career, Progress = () => (Mathf.Min(Wallet.Money, 25000), 25000),
            },

            // ----- Championship -----
            League("champ_0", "ROOKIE CHAMPION", "Win the Rookie League", 1000, 0),
            League("champ_1", "PRO CHAMPION", "Win the Pro League", 2000, 1),
            League("champ_2", "ELITE CHAMPION", "Win the Elite League", 4000, 2),
            League("champ_3", "RIOT LEGEND", "Win Riot Masters", 10000, 3),
        };

        // ---------- Builders ----------

        static AchievementDef Counter(string id, string name, string desc, int reward, AchievementCategory cat, Stat stat, int target) =>
            new AchievementDef
            {
                Id = id, Name = name, Description = desc, Reward = reward, Category = cat,
                Progress = () => (Mathf.Min(Stats.Get(stat), target), target),
            };

        static AchievementDef Event(string id, string name, string desc, int reward, AchievementCategory cat) =>
            new AchievementDef { Id = id, Name = name, Description = desc, Reward = reward, Category = cat };

        static AchievementDef League(string id, string name, string desc, int reward, int league) =>
            new AchievementDef
            {
                Id = id, Name = name, Description = desc, Reward = reward, Category = AchievementCategory.Championship,
                Condition = () => Championship.IsWon(league),
            };

        static bool AnyUpgradeMaxed()
        {
            foreach (var v in VehicleCatalog.Vehicles)
            {
                if (!PlayerGarage.Owns(v)) continue;
                foreach (UpgradeType t in Enum.GetValues(typeof(UpgradeType)))
                    if (PlayerGarage.Level(v, t) >= VehicleCatalog.MaxUpgradeLevel) return true;
            }
            return false;
        }

        static int OwnedVehicles()
        {
            int n = 0;
            foreach (var v in VehicleCatalog.Vehicles) if (PlayerGarage.Owns(v)) n++;
            return n;
        }

        // ---------- State ----------

        public static bool IsUnlocked(string id) => PlayerPrefs.GetInt("ach_" + id, 0) == 1;
        public static int UnlockedCount { get { int n = 0; foreach (var a in All) if (IsUnlocked(a.Id)) n++; return n; } }

        public static AchievementDef Get(string id) => All.Find(a => a.Id == id);

        /// <summary>Unlocks an achievement (once), pays its reward and raises the Unlocked event.</summary>
        public static void Unlock(string id)
        {
            var a = Get(id);
            if (a == null || IsUnlocked(id)) return;
            PlayerPrefs.SetInt("ach_" + id, 1);
            if (a.Reward > 0) Wallet.Money += a.Reward;   // Wallet saves PlayerPrefs
            PlayerPrefs.Save();
            Unlocked?.Invoke(a);
        }

        public static bool IsComplete(AchievementDef a)
        {
            if (a.Condition != null) return a.Condition();
            if (a.Progress != null)
            {
                var (cur, target) = a.Progress();
                return cur >= target;
            }
            return false;
        }

        /// <summary>Checks all automatic achievements if anything changed since the last check.</summary>
        public static void CheckIfDirty()
        {
            if (!dirty) return;
            dirty = false;
            foreach (var a in All)
                if (!IsUnlocked(a.Id) && IsComplete(a)) Unlock(a.Id);
        }
    }
}
