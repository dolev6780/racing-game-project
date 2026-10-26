using UnityEngine;
using RacingGame.Race;
using RacingGame.UI;

namespace RacingGame.Core
{
    /// <summary>
    /// Makes the game playable from any empty scene: press Play and the main menu appears.
    /// If the scene already contains a RaceManager (dev "quick race" scene), the race starts directly.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            AudioListener.volume = RaceConfig.Volume;
            PerformanceGovernor.Install();

            if (Object.FindAnyObjectByType<RaceManager>() != null) return;
            if (Object.FindAnyObjectByType<MainMenu>() != null) return;
            GameFlow.ShowMenu();
        }
    }
}
