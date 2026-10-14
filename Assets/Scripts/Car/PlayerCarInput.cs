using UnityEngine;
using RacingGame.Core;

namespace RacingGame.Car
{
    public enum SteeringMode { Joystick, Tilt, Buttons }

    /// <summary>
    /// Player controls.
    /// Keyboard: WASD/arrows, Space=fire, E/K=mine, Shift/L=nitro.
    /// Phone: auto-accelerate + steering by floating joystick, tilt or buttons, plus action buttons.
    /// </summary>
    public class PlayerCarInput : MonoBehaviour, ICarInputSource
    {
        // ---------- Settings (saved) ----------

        /// <summary>Show touch controls. On by default on phones; toggle from the HUD in the editor.</summary>
        public static bool ShowTouchControls
        {
            get => showTouch ?? (showTouch = Application.isMobilePlatform).Value;
            set => showTouch = value;
        }
        static bool? showTouch;

        public static SteeringMode Steering
        {
            get => (SteeringMode)PlayerPrefs.GetInt("steering", (int)SteeringMode.Joystick);
            set { PlayerPrefs.SetInt("steering", (int)value); PlayerPrefs.Save(); }
        }

        public static bool InvertTilt
        {
            get => PlayerPrefs.GetInt("tiltInvert", 0) == 1;
            set { PlayerPrefs.SetInt("tiltInvert", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Phone rotation (degrees) for full steering lock.</summary>
        public static float TiltMaxAngle = 25f;

        public static void CycleSteering()
        {
            var next = (SteeringMode)(((int)Steering + 1) % 3);
            if (next == SteeringMode.Tilt && !InputBridge.HasTiltSensor && !Application.isEditor)
                next = SteeringMode.Buttons;
            Steering = next;
            calibrateRequested = true;
        }

        public static void RequestTiltCalibration() => calibrateRequested = true;

        // ---------- Buttons ----------

        enum Btn { Left, Right, Brake, Fire, Mine, Boost, Count }

        readonly Rect[] rects = new Rect[(int)Btn.Count];
        readonly bool[] held = new bool[(int)Btn.Count];
        readonly bool[] heldLastFrame = new bool[(int)Btn.Count];
        static readonly string[] labels = { "<", ">", "BRAKE", "FIRE", "MINE", "NITRO" };

        // ---------- Joystick ----------

        const int NoPointer = int.MinValue;
        int joyPointer = NoPointer;
        Vector2 joyOrigin;      // GUI coordinates (top-left origin)
        Vector2 joyKnob;
        float joyRadius;
        float joySteer, joyY;

        // ---------- Tilt ----------

        static bool calibrateRequested = true;
        float tiltNeutral;
        float tiltSteer;

        GUIStyle buttonStyle;
        static Texture2D circleTexture;

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
                switch (Steering)
                {
                    case SteeringMode.Buttons:
                        if (held[(int)Btn.Left]) steer -= 1f;
                        if (held[(int)Btn.Right]) steer += 1f;
                        break;
                    case SteeringMode.Joystick:
                        steer += joySteer;
                        break;
                    case SteeringMode.Tilt:
                        steer += UpdateTilt();
                        break;
                }

                // Mobile: auto-accelerate; brake button or pulling the joystick down reverses.
                bool brake = held[(int)Btn.Brake] || (Steering == SteeringMode.Joystick && joyY > 0.7f);
                throttle = brake ? -1f : 1f;
            }

            s.Steer = Mathf.Clamp(steer, -1f, 1f);
            s.Throttle = Mathf.Clamp(throttle, -1f, 1f);
            s.Fire = InputBridge.WasPressedThisFrame(GameKey.Fire) || Pressed(Btn.Fire);
            s.Mine = InputBridge.WasPressedThisFrame(GameKey.Mine) || Pressed(Btn.Mine);
            s.Boost = InputBridge.WasPressedThisFrame(GameKey.Boost) || Pressed(Btn.Boost);
            return s;
        }

        void Awake() => calibrateRequested = true;   // re-centre tilt every race

        void Update()
        {
            // Keep tilt calibrated to how the player is holding the phone before the race starts.
            if (ShowTouchControls && Steering == SteeringMode.Tilt) UpdateTilt();
        }

        bool Pressed(Btn b) => held[(int)b] && !heldLastFrame[(int)b];

        void UpdateTouches()
        {
            for (int i = 0; i < held.Length; i++)
            {
                heldLastFrame[i] = held[i];
                held[i] = false;
            }
            if (!ShowTouchControls) { joyPointer = NoPointer; joySteer = 0f; return; }

            LayoutButtons();
            var pointers = InputBridge.GetPointers();
            bool joyStillDown = false;

            foreach (var p in pointers)
            {
                // Touch positions are bottom-left origin; GUI rects are top-left.
                var gui = new Vector2(p.Position.x, Screen.height - p.Position.y);

                if (p.Id == joyPointer)
                {
                    joyStillDown = true;
                    UpdateJoystick(gui);
                    continue;
                }

                bool onButton = false;
                for (int i = 0; i < rects.Length; i++)
                {
                    if (rects[i].width > 0f && rects[i].Contains(gui))
                    {
                        held[i] = true;
                        onButton = true;
                    }
                }

                // A new finger on the left part of the screen grabs the floating joystick.
                if (!onButton && joyPointer == NoPointer && Steering == SteeringMode.Joystick && InJoystickZone(gui))
                {
                    joyPointer = p.Id;
                    joyOrigin = gui;
                    joyStillDown = true;
                    UpdateJoystick(gui);
                }
            }

            if (!joyStillDown)
            {
                joyPointer = NoPointer;
                joySteer = 0f;
                joyY = 0f;
            }
        }

        bool InJoystickZone(Vector2 gui) =>
            gui.x < Screen.width * 0.45f && gui.y > Screen.height * 0.3f;

        void UpdateJoystick(Vector2 gui)
        {
            joyRadius = Screen.height * 0.11f;
            Vector2 delta = gui - joyOrigin;
            // If the finger drags past the edge, the base follows it (floating joystick).
            if (delta.magnitude > joyRadius)
            {
                joyOrigin = gui - delta.normalized * joyRadius;
                delta = gui - joyOrigin;
            }
            joyKnob = joyOrigin + delta;

            float x = delta.x / joyRadius;
            const float deadZone = 0.12f;
            joySteer = Mathf.Abs(x) < deadZone ? 0f : Mathf.Sign(x) * Mathf.InverseLerp(deadZone, 1f, Mathf.Abs(x));
            joyY = delta.y / joyRadius;   // positive = finger pulled down (GUI y grows downward)
        }

        /// <summary>Steering from rotating the phone like a steering wheel (landscape).</summary>
        float UpdateTilt()
        {
            Vector3 g = InputBridge.ReadGravity();
            if (g == Vector3.zero) return 0f;

            // In landscape the screen's horizontal axis is the device's long (Y) axis.
            float side = Screen.orientation == ScreenOrientation.LandscapeRight ? g.y : -g.y;
            if (InvertTilt) side = -side;
            float angle = Mathf.Asin(Mathf.Clamp(side, -1f, 1f)) * Mathf.Rad2Deg;

            if (calibrateRequested)
            {
                tiltNeutral = angle;
                calibrateRequested = false;
            }

            float raw = Mathf.Clamp((angle - tiltNeutral) / TiltMaxAngle, -1f, 1f);
            if (Mathf.Abs(raw) < 0.06f) raw = 0f;
            tiltSteer = Mathf.Lerp(tiltSteer, raw, 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime));
            return tiltSteer;
        }

        void LayoutButtons()
        {
            float w = Screen.width, h = Screen.height;
            float u = h * 0.17f;          // button size
            float m = h * 0.03f;          // margin

            if (Steering == SteeringMode.Buttons)
            {
                rects[(int)Btn.Left] = new Rect(m, h - m - u, u * 1.1f, u);
                rects[(int)Btn.Right] = new Rect(m * 2f + u * 1.1f, h - m - u, u * 1.1f, u);
            }
            else
            {
                rects[(int)Btn.Left] = Rect.zero;
                rects[(int)Btn.Right] = Rect.zero;
            }

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
                if (rects[i].width <= 0f) continue;
                GUI.color = held[i] ? new Color(1f, 0.8f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.55f);
                GUI.Box(rects[i], labels[i], buttonStyle);
            }

            if (Steering == SteeringMode.Joystick) DrawJoystick();
            else if (Steering == SteeringMode.Tilt) DrawTiltIndicator();
            GUI.color = old;
        }

        void DrawJoystick()
        {
            if (circleTexture == null) circleTexture = MakeCircleTexture(128);
            float r = Screen.height * 0.11f;
            bool active = joyPointer != NoPointer;

            // Idle: show a faint hint where the thumb usually rests.
            Vector2 origin = active ? joyOrigin : new Vector2(Screen.width * 0.14f, Screen.height * 0.75f);
            Vector2 knob = active ? joyKnob : origin;

            GUI.color = new Color(1f, 1f, 1f, active ? 0.35f : 0.18f);
            GUI.DrawTexture(new Rect(origin.x - r, origin.y - r, r * 2f, r * 2f), circleTexture);
            float kr = r * 0.45f;
            GUI.color = new Color(1f, 0.85f, 0.2f, active ? 0.9f : 0.35f);
            GUI.DrawTexture(new Rect(knob.x - kr, knob.y - kr, kr * 2f, kr * 2f), circleTexture);
        }

        void DrawTiltIndicator()
        {
            // A small steering bar so the player sees how much they are turning.
            float w = Screen.height * 0.4f, h = Screen.height * 0.025f;
            var bar = new Rect(Screen.height * 0.05f, Screen.height * 0.9f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.4f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            float cx = bar.x + w * 0.5f + tiltSteer * w * 0.5f;
            GUI.DrawTexture(new Rect(cx - h, bar.y - h * 0.5f, h * 2f, h * 2f), Texture2D.whiteTexture);
        }

        static Texture2D MakeCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                byte a = (byte)(Mathf.Clamp01((1f - d) * size * 0.5f) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
