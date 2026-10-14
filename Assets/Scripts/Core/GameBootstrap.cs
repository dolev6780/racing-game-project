using UnityEngine;
using UnityEngine.SceneManagement;
using RacingGame.Race;

namespace RacingGame.Core
{
    /// <summary>
    /// Makes the game playable from any empty scene: press Play and a full race is built.
    /// Scenes listed in <see cref="ScenesWithoutAutoRace"/> (e.g. future menus) are skipped.
    /// </summary>
    public static class GameBootstrap
    {
        static readonly string[] ScenesWithoutAutoRace = { "MainMenu", "Garage" };

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

            string sceneName = SceneManager.GetActiveScene().name;
            foreach (var skip in ScenesWithoutAutoRace)
                if (sceneName == skip) return;

            if (Object.FindAnyObjectByType<RaceManager>() == null)
                new GameObject("RaceManager").AddComponent<RaceManager>();
        }
    }
}
