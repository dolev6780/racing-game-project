using UnityEngine;
using RacingGame.Race;
using RacingGame.Track;
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

        /// <summary>Starts the race described by RaceRequest.Current (a quick race if none is set).</summary>
        public static void StartRace()
        {
            if (RaceRequest.Current == null) RaceRequest.Current = RaceRequest.QuickRace();
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

        /// <summary>Sun, fog and ambient light for a track theme. The sun lives outside the mode roots.</summary>
        public static void SetupEnvironment(TrackTheme theme = null)
        {
            theme ??= TrackLibrary.Get(0).Theme;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = theme.Ambient;
            RenderSettings.fog = true;
            RenderSettings.fogColor = theme.Fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 420f;
            QualitySettings.shadowDistance = 90f;   // crisper shadows near the karts

            // Cartoon sky (falls back to a flat colour if the shader is unavailable).
            var skyMat = ToonStyle.Sky(theme.SkyTop, theme.SkyHorizon, theme.SkyBottom);
            RenderSettings.skybox = skyMat;

            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }

            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            }
            sun.color = theme.Sun;

            var cam = Camera.main;
            if (cam != null) ApplySky(cam);
        }

        static void ApplySky(Camera cam)
        {
            cam.backgroundColor = RenderSettings.fogColor;
            cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
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
            ApplySky(cam);
            cam.farClipPlane = 600f;
            return cam;
        }
    }
}
