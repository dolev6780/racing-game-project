using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Starts the game when a scene loads: the phone held sideways and kept awake, the frame-rate keeper on, and the
    /// game built from code in its own "Cloudborne" scene or in a new unsaved one. The test runner's scene has a
    /// name of its own, so tests boot the game themselves.
    /// </summary>
    public static class CbBoot
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
            RacingGame.Core.PerformanceGovernor.Install();
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene == "Cloudborne" || string.IsNullOrEmpty(scene)) CbGame.Boot();
        }
    }
}
