using UnityEngine;
using RacingGame.Race;
using RacingGame.UI;
using RacingGame.Weapons;

namespace RacingGame.Core
{
    /// <summary>
    /// Switches between the main menu and a race without loading scenes:
    /// each mode lives under one root GameObject that is destroyed on exit.
    /// </summary>
    public static class GameFlow
    {
        public static void ShowMenu()
        {
            ClearCurrent();
            new GameObject("MainMenu").AddComponent<MainMenu>();
        }

        public static void StartRace()
        {
            ClearCurrent();
            new GameObject("RaceManager").AddComponent<RaceManager>();
        }

        static void ClearCurrent()
        {
            Time.timeScale = 1f;
            Mine.ClearAll();
            var race = Object.FindAnyObjectByType<RaceManager>();
            if (race) Object.Destroy(race.gameObject);
            var menu = Object.FindAnyObjectByType<MainMenu>();
            if (menu) Object.Destroy(menu.gameObject);
        }

        /// <summary>Sun, fog and ambient light shared by menu and race. Lives outside the mode roots.</summary>
        public static void SetupEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.6f, 0.66f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.62f, 0.8f, 0.95f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 120f;
            RenderSettings.fogEndDistance = 320f;

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.92f, 0.85f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        /// <summary>The main camera (created once, outside the mode roots).</summary>
        public static Camera GetCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.farClipPlane = 400f;
            return cam;
        }
    }
}
