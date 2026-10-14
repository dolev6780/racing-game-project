using UnityEngine;
using RacingGame.Track;

namespace RacingGame.Race
{
    /// <summary>Everything needed to set up one race. Built from the quick-race menu or a championship.</summary>
    public class RaceRequest
    {
        public TrackDef Track;
        public int Laps = 4;
        public int Opponents = 5;

        // AI strength
        public float AiTopSpeed = 32f;
        public float AiArmor = 100f;
        public Vector2 AiSpeedMultiplier = new Vector2(0.9f, 0.98f);
        public Vector2 AiSkill = new Vector2(0.45f, 0.95f);
        public Vector2 AiAggression = new Vector2(0.3f, 0.9f);

        public float PrizeMultiplier = 1f;
        public string Label = "NORMAL";

        /// <summary>Set when this race belongs to a championship season.</summary>
        public bool IsChampionship;
        public int ChampionshipRace;

        /// <summary>The race that GameFlow.StartRace() will build.</summary>
        public static RaceRequest Current;

        public static RaceRequest QuickRace()
        {
            var r = new RaceRequest
            {
                Track = TrackLibrary.Get(RaceConfig.TrackIndex),
                Laps = RaceConfig.Laps,
                Opponents = RaceConfig.Opponents,
                PrizeMultiplier = RaceConfig.PrizeMultiplier,
                Label = RaceConfig.Difficulty.ToString().ToUpperInvariant(),
            };
            switch (RaceConfig.Difficulty)
            {
                case Difficulty.Easy:
                    r.AiSpeedMultiplier = new Vector2(0.82f, 0.9f);
                    r.AiSkill = new Vector2(0.25f, 0.55f);
                    r.AiAggression = new Vector2(0.15f, 0.45f);
                    break;
                case Difficulty.Hard:
                    r.AiSpeedMultiplier = new Vector2(0.97f, 1.04f);
                    r.AiSkill = new Vector2(0.75f, 1f);
                    r.AiAggression = new Vector2(0.6f, 1f);
                    r.AiArmor = 120f;
                    break;
            }
            return r;
        }
    }
}
