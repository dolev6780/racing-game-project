using UnityEngine;

namespace RacingGame.Race
{
    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>Race setup chosen in the menu (saved between sessions).</summary>
    public static class RaceConfig
    {
        public static readonly int[] LapOptions = { 2, 3, 4, 5, 6 };
        public static readonly int[] OpponentOptions = { 1, 2, 3, 4, 5, 6, 7 };

        public static int Laps
        {
            get => PlayerPrefs.GetInt("cfg_laps", 4);
            set => PlayerPrefs.SetInt("cfg_laps", value);
        }

        public static int TrackIndex
        {
            get => PlayerPrefs.GetInt("cfg_track", 0);
            set => PlayerPrefs.SetInt("cfg_track", value);
        }

        public static int Opponents
        {
            get => PlayerPrefs.GetInt("cfg_opponents", 5);
            set => PlayerPrefs.SetInt("cfg_opponents", value);
        }

        public static Difficulty Difficulty
        {
            get => (Difficulty)PlayerPrefs.GetInt("cfg_difficulty", (int)Difficulty.Normal);
            set => PlayerPrefs.SetInt("cfg_difficulty", (int)value);
        }

        /// <summary>Prize money multiplier: harder races pay more.</summary>
        public static float PrizeMultiplier =>
            Difficulty == Difficulty.Easy ? 0.75f : Difficulty == Difficulty.Hard ? 1.5f : 1f;

        /// <summary>Master volume 0..1 (applied to AudioListener.volume).</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat("cfg_volume", 0.8f);
            set { PlayerPrefs.SetFloat("cfg_volume", value); AudioListener.volume = value; }
        }

        /// <summary>Camera view for races (top down by default).</summary>
        public static CameraRig.CameraMode Camera
        {
            get => (CameraRig.CameraMode)Mathf.Clamp(PlayerPrefs.GetInt("cfg_camMode", 0), 0, 2);
            set => PlayerPrefs.SetInt("cfg_camMode", (int)value);
        }
    }
}
