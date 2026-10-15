using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Progress
{
    public enum Stat
    {
        Races, Wins, Podiums, WinStreak, BestWinStreak,
        Wrecks, TimesWrecked, MissilesFired, MinesDropped, MineHits, NitrosUsed,
        MoneyPickups, MoneyEarned, TopSpeedKmh, TracksRaced,
        UpgradesBought, VehiclesBought,
    }

    /// <summary>Lifetime player statistics (saved in PlayerPrefs).</summary>
    public static class Stats
    {
        static readonly Dictionary<Stat, int> cache = new Dictionary<Stat, int>();

        static string Key(Stat s) => "stat_" + s;

        public static int Get(Stat s)
        {
            if (!cache.TryGetValue(s, out int v))
            {
                v = PlayerPrefs.GetInt(Key(s), 0);
                cache[s] = v;
            }
            return v;
        }

        public static void Set(Stat s, int value)
        {
            if (Get(s) == value) return;
            cache[s] = value;
            PlayerPrefs.SetInt(Key(s), value);
            Achievements.MarkDirty();
        }

        public static void Add(Stat s, int amount = 1) => Set(s, Get(s) + amount);

        /// <summary>Keeps the highest value seen (e.g. top speed).</summary>
        public static void SetMax(Stat s, int value)
        {
            if (value > Get(s)) Set(s, value);
        }

        /// <summary>Sets a bit in a bitmask stat (e.g. which tracks were raced).</summary>
        public static void SetBit(Stat s, int bit) => Set(s, Get(s) | (1 << bit));

        public static int CountBits(Stat s)
        {
            int v = Get(s), n = 0;
            while (v != 0) { n += v & 1; v >>= 1; }
            return n;
        }

        /// <summary>Forget cached values (after "Reset Saved Progress").</summary>
        public static void ClearCache() => cache.Clear();
    }

    /// <summary>Personal best lap and race times per track.</summary>
    public static class Records
    {
        static string LapKey(string track) => "pb_lap_" + track;
        static string RaceKey(string track, int laps) => $"pb_race_{track}_{laps}";

        public static float BestLap(string track) => PlayerPrefs.GetFloat(LapKey(track), 0f);
        public static float BestRace(string track, int laps) => PlayerPrefs.GetFloat(RaceKey(track, laps), 0f);

        /// <summary>Returns true if this is a new record.</summary>
        public static bool SubmitLap(string track, float time) => Submit(LapKey(track), time);
        public static bool SubmitRace(string track, int laps, float time) => Submit(RaceKey(track, laps), time);

        static bool Submit(string key, float time)
        {
            if (time <= 1f) return false;
            float best = PlayerPrefs.GetFloat(key, 0f);
            if (best > 0f && time >= best) return false;
            PlayerPrefs.SetFloat(key, time);
            return true;
        }
    }
}
