using System;
using UnityEngine;
using RacingGame.Car;
using RacingGame.CameraRig;
using RacingGame.Race;

namespace RacingGame.UI
{
    /// <summary>Settings screen, shared by the main menu and the pause menu.</summary>
    public static class SettingsPanel
    {
        static readonly string[] SteeringNames = { "ARROWS", "STICK", "TILT" };
        static readonly string[] OnOff = { "OFF", "ON" };

        public static GameObject Build(Transform parent, Action onBack)
        {
            var root = UIKit.Stretch(parent, "Settings");
            root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<FadeIn>();

            var panel = UIKit.Panel(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(980f, 820f)).rectTransform;
            UIKit.Label(panel, "SETTINGS", 64, UIKit.Accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(900f, 90f), TextAnchor.MiddleCenter, true);

            const float w = 880f;
            float y = -150f;
            UIKit.OptionRow(panel, "STEERING (PHONE)", y, w, SteeringNames, (int)PlayerCarInput.Steering,
                i => PlayerCarInput.Steering = (SteeringMode)i);
            y -= 100f;
            UIKit.OptionRow(panel, "INVERT TILT", y, w, OnOff, PlayerCarInput.InvertTilt ? 1 : 0,
                i => PlayerCarInput.InvertTilt = i == 1);
            y -= 100f;
            UIKit.OptionRow(panel, "CAMERA VIEW", y, w, RaceCamera.ModeNames, (int)RaceConfig.Camera, i =>
            {
                RaceConfig.Camera = (CameraMode)i;
                RaceCamera.Mode = (CameraMode)i;
            });
            y -= 100f;

            var volumes = new string[11];
            for (int v = 0; v <= 10; v++) volumes[v] = (v * 10) + "%";
            UIKit.OptionRow(panel, "VOLUME", y, w, volumes, Mathf.RoundToInt(RaceConfig.Volume * 10f),
                i => RaceConfig.Volume = i / 10f);
            y -= 100f;

#if UNITY_EDITOR || UNITY_STANDALONE
            UIKit.OptionRow(panel, "TOUCH CONTROLS", y, w, OnOff, PlayerCarInput.ShowTouchControls ? 1 : 0,
                i => PlayerCarInput.ShowTouchControls = i == 1);
#endif

            UIKit.Button(panel, "BACK", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f),
                new Vector2(360f, 100f), () =>
                {
                    PlayerPrefs.Save();
                    UnityEngine.Object.Destroy(root.gameObject);
                    onBack?.Invoke();
                }, UIKit.Accent, new Color(0.1f, 0.1f, 0.12f), 44);

            return root.gameObject;
        }
    }
}
