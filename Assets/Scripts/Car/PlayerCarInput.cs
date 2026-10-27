using UnityEngine;
using RacingGame.CameraRig;
using RacingGame.Core;

namespace RacingGame.Car
{
    /// <summary>
    /// How the left thumb steers on a phone:
    /// Arrows - big left/right buttons turn the kart;
    /// Stick  - point where you want to go on screen, the kart turns toward it (reversing too);
    /// Tilt   - rotate the phone like a steering wheel (auto-gas).
    /// </summary>
    public enum SteeringMode { Arrows, Stick, Tilt }

    /// <summary>
    /// Player controls.
    /// Keyboard: WASD/arrows, Space=fire, E/K=mine, Shift/L=nitro. Gamepad: left stick, triggers, A/B/X.
    /// Phone: left thumb steers (arrows, stick or tilt), right thumb holds GAS or REV, with FIRE/MINE/NITRO above.
    /// Every finger is tracked separately, so thumbs can slide between buttons and press several at once.
    /// </summary>
    public class PlayerCarInput : MonoBehaviour, ICarInputSource
    {
        // ---------- Settings (saved) ----------

        /// <summary>Show touch controls. On by default on phones; toggle in Settings in the editor.</summary>
        public static bool ShowTouchControls
        {
            get => showTouch ?? (showTouch = Application.isMobilePlatform).Value;
            set => showTouch = value;
        }
        static bool? showTouch;

        public static SteeringMode Steering
        {
            get => (SteeringMode)Mathf.Clamp(PlayerPrefs.GetInt("touchSteering", (int)SteeringMode.Arrows), 0, 2);
            set { PlayerPrefs.SetInt("touchSteering", (int)value); PlayerPrefs.Save(); calibrateRequested = true; }
        }

        /// <summary>Show a DRIFT touch button (drifting also works by steering hard at speed).</summary>
        public static bool DriftButton
        {
            get => PlayerPrefs.GetInt("driftButton", 0) == 1;
            set { PlayerPrefs.SetInt("driftButton", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool InvertTilt
        {
            get => PlayerPrefs.GetInt("tiltInvert", 0) == 1;
            set { PlayerPrefs.SetInt("tiltInvert", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Phone rotation (degrees) for full steering lock.</summary>
        public static float TiltMaxAngle = 25f;

        public static void RequestTiltCalibration() => calibrateRequested = true;

        // ---------- Buttons ----------

        public enum Btn { Left, Right, Gas, Reverse, Fire, Mine, Boost, Item, Drift, Count }

        readonly Rect[] rects = new Rect[(int)Btn.Count];
        readonly bool[] held = new bool[(int)Btn.Count];
        readonly bool[] heldLastFrame = new bool[(int)Btn.Count];

        // ---------- Stick ----------

        const int NoPointer = int.MinValue;
        int stickPointer = NoPointer;
        Vector2 stickOrigin;     // GUI coordinates (top-left origin)
        Vector2 stickKnob;

        // ---------- Tilt ----------

        static bool calibrateRequested = true;
        float tiltNeutral;
        float tiltSteer;

        CarController car;

        // ---------- Layout (GUI pixels, top-left origin), shared with TouchControlsView ----------

        float Unit => Mathf.Min(Screen.height * 0.2f, Screen.width * 0.11f);
        float Margin => Unit * 0.22f;

        public Rect ButtonRect(Btn b) { LayoutButtons(); return rects[(int)b]; }
        public bool IsHeld(Btn b) => held[(int)b];
        public bool StickActive => stickPointer != NoPointer;
        public Vector2 StickOrigin => StickActive ? stickOrigin : StickHome;
        public Vector2 StickKnob => StickActive ? stickKnob : StickHome;
        public float StickRadius => Unit * 0.95f;
        Vector2 StickHome => new Vector2(Margin + Unit * 1.25f, Screen.height - Margin - Unit * 1.25f);
        public float TiltSteer => tiltSteer;

        void Awake()
        {
            calibrateRequested = true;   // re-centre tilt every race
            car = GetComponent<CarController>();
        }

        void Update()
        {
            // Keep tilt calibrated to how the player is holding the phone before the race starts.
            if (ShowTouchControls && Steering == SteeringMode.Tilt) UpdateTilt();
        }

        public CarInputState ReadInput()
        {
            UpdateTouches();

            var s = new CarInputState();
            float steer = InputBridge.SteerAxis(), throttle = InputBridge.ThrottleAxis();

            if (ShowTouchControls)
            {
                if (held[(int)Btn.Left]) steer -= 1f;
                if (held[(int)Btn.Right]) steer += 1f;
                if (Steering == SteeringMode.Stick && StickActive) steer += StickSteer();
                if (Steering == SteeringMode.Tilt) steer += UpdateTilt();

                if (held[(int)Btn.Gas]) throttle += 1f;
                if (held[(int)Btn.Reverse]) throttle -= 1f;
                // Tilt needs both hands on the phone: drive automatically unless reversing.
                if (Steering == SteeringMode.Tilt && !held[(int)Btn.Reverse]) throttle = Mathf.Max(throttle, 1f);
            }

            s.Steer = Mathf.Clamp(steer, -1f, 1f);
            s.Throttle = Mathf.Clamp(throttle, -1f, 1f);
            s.Fire = InputBridge.WasPressedThisFrame(GameKey.Fire) || Pressed(Btn.Fire);
            s.Mine = InputBridge.WasPressedThisFrame(GameKey.Mine) || Pressed(Btn.Mine);
            s.Boost = InputBridge.WasPressedThisFrame(GameKey.Boost) || Pressed(Btn.Boost);
            s.Item = InputBridge.WasPressedThisFrame(GameKey.Item) || Pressed(Btn.Item);
            s.Drift = InputBridge.IsHeld(GameKey.Drift) || (ShowTouchControls && held[(int)Btn.Drift]);
            // Ramp trick: the drift button, or on a phone simply tapping GAS again, while in the air.
            s.Trick = InputBridge.WasPressedThisFrame(GameKey.Drift) || Pressed(Btn.Drift) || Pressed(Btn.Gas);
            return s;
        }

        bool Pressed(Btn b) => held[(int)b] && !heldLastFrame[(int)b];

        /// <summary>
        /// Stick steering. With the isometric camera the stick points where the kart should go on screen:
        /// the kart turns toward that direction (and backs toward it when reversing).
        /// With the chase camera it simply steers left/right.
        /// </summary>
        float StickSteer()
        {
            Vector2 d = (stickKnob - stickOrigin) / StickRadius;   // GUI: y grows downward
            if (d.magnitude < 0.25f) return 0f;
            if (!RaceCamera.IsIsometric || car == null)
                return Mathf.Clamp(d.x, -1f, 1f);

            Vector3 target = RaceCamera.ScreenToGroundDirection(new Vector2(d.x, -d.y));
            return AimSteer(car.transform.forward, target, car.ForwardSpeed < -0.1f);
        }

        /// <summary>Steering (-1..1) that turns a kart facing <paramref name="forward"/> toward a ground direction.</summary>
        public static float AimSteer(Vector3 forward, Vector3 targetDir, bool reversing)
        {
            forward.y = 0f;
            targetDir.y = 0f;
            if (forward.sqrMagnitude < 1e-6f || targetDir.sqrMagnitude < 1e-6f) return 0f;
            if (reversing) targetDir = -targetDir;   // back the tail toward it
            float diff = Vector3.SignedAngle(forward, targetDir, Vector3.up) * Mathf.Deg2Rad;
            if (reversing) diff = -diff;             // steering is mirrored when moving backwards
            return Mathf.Clamp(diff * 2.2f, -1f, 1f);
        }

        void UpdateTouches()
        {
            for (int i = 0; i < held.Length; i++)
            {
                heldLastFrame[i] = held[i];
                held[i] = false;
            }
            if (!ShowTouchControls) { stickPointer = NoPointer; return; }

            LayoutButtons();
            var pointers = InputBridge.GetPointers();
            bool stickStillDown = false;
            float slack = Margin * 0.4f;   // hit areas a bit larger than the drawn buttons

            foreach (var p in pointers)
            {
                // Touch positions are bottom-left origin; GUI rects are top-left.
                var gui = new Vector2(p.Position.x, Screen.height - p.Position.y);

                if (p.Id == stickPointer)
                {
                    stickStillDown = true;
                    MoveStick(gui);
                    continue;
                }

                bool onButton = false;
                for (int i = 0; i < rects.Length; i++)
                {
                    var r = rects[i];
                    if (r.width <= 0f) continue;
                    if (new Rect(r.x - slack, r.y - slack, r.width + slack * 2f, r.height + slack * 2f).Contains(gui))
                    {
                        held[i] = true;
                        onButton = true;
                        break;
                    }
                }

                // A new finger on the left part of the screen grabs the floating stick.
                if (!onButton && stickPointer == NoPointer && Steering == SteeringMode.Stick && gui.x < Screen.width * 0.45f)
                {
                    stickPointer = p.Id;
                    stickOrigin = gui;
                    stickKnob = gui;
                    stickStillDown = true;
                }
            }

            if (!stickStillDown) stickPointer = NoPointer;
        }

        void MoveStick(Vector2 gui)
        {
            stickKnob = gui;
            // Past the rim, the base follows the finger so steering never "runs out".
            Vector2 delta = stickKnob - stickOrigin;
            if (delta.magnitude > StickRadius) stickOrigin = stickKnob - delta.normalized * StickRadius;
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

        /// <summary>Landscape phone layout, sized from the screen so it fits any device.</summary>
        void LayoutButtons()
        {
            float w = Screen.width, h = Screen.height;
            float u = Unit, m = Margin, gap = m * 0.6f;

            if (Steering == SteeringMode.Arrows)
            {
                rects[(int)Btn.Left] = new Rect(m, h - m - u * 1.3f, u * 1.2f, u * 1.3f);
                rects[(int)Btn.Right] = new Rect(m + u * 1.2f + gap, h - m - u * 1.3f, u * 1.2f, u * 1.3f);
            }
            else
            {
                rects[(int)Btn.Left] = Rect.zero;
                rects[(int)Btn.Right] = Rect.zero;
            }

            var gas = new Rect(w - m - u * 1.15f, h - m - u * 1.7f, u * 1.15f, u * 1.7f);
            var rev = new Rect(gas.xMin - gap - u * 1.05f, h - m - u * 1.15f, u * 1.05f, u * 1.15f);
            float a = u * 0.9f;
            rects[(int)Btn.Gas] = gas;
            rects[(int)Btn.Reverse] = rev;
            rects[(int)Btn.Fire] = new Rect(gas.center.x - a * 0.5f, gas.yMin - gap - a, a, a);
            rects[(int)Btn.Mine] = new Rect(rev.center.x - a * 0.5f, rev.yMin - gap - a, a, a);
            rects[(int)Btn.Boost] = new Rect(rev.center.x - a * 1.5f - gap, rev.yMin - gap - a, a, a);
            rects[(int)Btn.Item] = new Rect(gas.center.x - a * 0.5f, gas.yMin - gap * 2f - a * 2f, a, a);   // above FIRE
            float dr = u * 0.95f;
            rects[(int)Btn.Drift] = DriftButton ? new Rect(rev.xMin - gap - dr, h - m - dr, dr, dr) : Rect.zero;
        }
    }
}
