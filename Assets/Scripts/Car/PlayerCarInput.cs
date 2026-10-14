using UnityEngine;
using RacingGame.Core;

namespace RacingGame.Car
{
    /// <summary>
    /// Player controls: keyboard (WASD/arrows, Space=fire, E/K=mine, Shift/L=nitro)
    /// and on-screen touch buttons for phones (auto-accelerate, tap to brake).
    /// </summary>
    public class PlayerCarInput : MonoBehaviour, ICarInputSource
    {
        /// <summary>Show touch buttons. On by default on phones; toggle from the HUD in the editor.</summary>
        public static bool ShowTouchControls
        {
            get => showTouch ?? (showTouch = Application.isMobilePlatform).Value;
            set => showTouch = value;
        }
        static bool? showTouch;

        enum Btn { Left, Right, Brake, Fire, Mine, Boost, Count }

        readonly Rect[] rects = new Rect[(int)Btn.Count];
        readonly bool[] held = new bool[(int)Btn.Count];
        readonly bool[] heldLastFrame = new bool[(int)Btn.Count];
        static readonly string[] labels = { "<", ">", "BRAKE", "FIRE", "MINE", "NITRO" };

        GUIStyle buttonStyle;

        public CarInputState ReadInput()
        {
            UpdateTouches();

            var s = new CarInputState();
            float steer = 0f, throttle = 0f;
            if (InputBridge.IsHeld(GameKey.Left)) steer -= 1f;
            if (InputBridge.IsHeld(GameKey.Right)) steer += 1f;
            if (InputBridge.IsHeld(GameKey.Up)) throttle += 1f;
            if (InputBridge.IsHeld(GameKey.Down)) throttle -= 1f;

            if (ShowTouchControls)
            {
                if (held[(int)Btn.Left]) steer -= 1f;
                if (held[(int)Btn.Right]) steer += 1f;
                // Mobile: auto-accelerate unless braking.
                throttle = held[(int)Btn.Brake] ? -1f : 1f;
            }

            s.Steer = Mathf.Clamp(steer, -1f, 1f);
            s.Throttle = Mathf.Clamp(throttle, -1f, 1f);
            s.Fire = InputBridge.WasPressedThisFrame(GameKey.Fire) || Pressed(Btn.Fire);
            s.Mine = InputBridge.WasPressedThisFrame(GameKey.Mine) || Pressed(Btn.Mine);
            s.Boost = InputBridge.WasPressedThisFrame(GameKey.Boost) || Pressed(Btn.Boost);
            return s;
        }

        bool Pressed(Btn b) => held[(int)b] && !heldLastFrame[(int)b];

        void UpdateTouches()
        {
            for (int i = 0; i < held.Length; i++)
            {
                heldLastFrame[i] = held[i];
                held[i] = false;
            }
            if (!ShowTouchControls) return;

            LayoutButtons();
            foreach (var p in InputBridge.GetPointers())
            {
                // Touch positions are bottom-left origin; GUI rects are top-left.
                var gui = new Vector2(p.x, Screen.height - p.y);
                for (int i = 0; i < rects.Length; i++)
                    if (rects[i].Contains(gui)) held[i] = true;
            }
        }

        void LayoutButtons()
        {
            float w = Screen.width, h = Screen.height;
            float u = h * 0.17f;          // button size
            float m = h * 0.03f;          // margin

            rects[(int)Btn.Left] = new Rect(m, h - m - u, u * 1.1f, u);
            rects[(int)Btn.Right] = new Rect(m * 2f + u * 1.1f, h - m - u, u * 1.1f, u);

            float big = u * 1.3f;
            rects[(int)Btn.Fire] = new Rect(w - m - big, h - m - big, big, big);
            rects[(int)Btn.Mine] = new Rect(w - m * 2f - big - u, h - m - u, u, u);
            rects[(int)Btn.Brake] = new Rect(w - m * 3f - big - u * 2f, h - m - u, u, u);
            rects[(int)Btn.Boost] = new Rect(w - m - big + (big - u) * 0.5f, h - m * 2f - big - u, u, u);
        }

        void OnGUI()
        {
            if (!ShowTouchControls) return;
            LayoutButtons();
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
            }
            buttonStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.035f);

            var old = GUI.color;
            for (int i = 0; i < rects.Length; i++)
            {
                GUI.color = held[i] ? new Color(1f, 0.8f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.55f);
                GUI.Box(rects[i], labels[i], buttonStyle);
            }
            GUI.color = old;
        }
    }
}
