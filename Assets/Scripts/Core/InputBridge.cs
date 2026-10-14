using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RacingGame.Core
{
    public enum GameKey { Up, Down, Left, Right, Fire, Mine, Boost, Camera, Restart, Pause }

    /// <summary>A finger on the screen (or the mouse, id -1). Position origin is bottom-left.</summary>
    public struct TouchPointer
    {
        public int Id;
        public Vector2 Position;
    }

    /// <summary>
    /// Thin wrapper so the game works with the new Input System, the legacy Input Manager, or both.
    /// Screen positions use Unity's convention: origin at bottom-left.
    /// </summary>
    public static class InputBridge
    {
        static readonly List<TouchPointer> touchBuffer = new List<TouchPointer>(10);
        const int MouseId = -1;

        public static bool IsHeld(GameKey key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (key)
            {
                case GameKey.Up: return kb.wKey.isPressed || kb.upArrowKey.isPressed;
                case GameKey.Down: return kb.sKey.isPressed || kb.downArrowKey.isPressed;
                case GameKey.Left: return kb.aKey.isPressed || kb.leftArrowKey.isPressed;
                case GameKey.Right: return kb.dKey.isPressed || kb.rightArrowKey.isPressed;
                case GameKey.Fire: return kb.spaceKey.isPressed || kb.jKey.isPressed;
                case GameKey.Mine: return kb.kKey.isPressed || kb.eKey.isPressed;
                case GameKey.Boost: return kb.leftShiftKey.isPressed || kb.lKey.isPressed;
                case GameKey.Camera: return kb.cKey.isPressed;
                case GameKey.Restart: return kb.rKey.isPressed;
            }
            return false;
#else
            switch (key)
            {
                case GameKey.Up: return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
                case GameKey.Down: return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
                case GameKey.Left: return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
                case GameKey.Right: return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
                case GameKey.Fire: return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.J);
                case GameKey.Mine: return Input.GetKey(KeyCode.K) || Input.GetKey(KeyCode.E);
                case GameKey.Boost: return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.L);
                case GameKey.Camera: return Input.GetKey(KeyCode.C);
                case GameKey.Restart: return Input.GetKey(KeyCode.R);
            }
            return false;
#endif
        }

        public static bool WasPressedThisFrame(GameKey key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (key)
            {
                case GameKey.Fire: return kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame;
                case GameKey.Mine: return kb.kKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame;
                case GameKey.Boost: return kb.leftShiftKey.wasPressedThisFrame || kb.lKey.wasPressedThisFrame;
                case GameKey.Camera: return kb.cKey.wasPressedThisFrame;
                case GameKey.Restart: return kb.rKey.wasPressedThisFrame;
                case GameKey.Pause: return kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame;
                default: return false;
            }
#else
            switch (key)
            {
                case GameKey.Fire: return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J);
                case GameKey.Mine: return Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.E);
                case GameKey.Boost: return Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.L);
                case GameKey.Camera: return Input.GetKeyDown(KeyCode.C);
                case GameKey.Restart: return Input.GetKeyDown(KeyCode.R);
                case GameKey.Pause: return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
                default: return false;
            }
#endif
        }

        /// <summary>
        /// Current touch points (plus the mouse while its left button is held, so touch
        /// controls can be tested in the editor).
        /// </summary>
        public static List<TouchPointer> GetPointers()
        {
            touchBuffer.Clear();
            bool anyTouch = false;
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var touch in ts.touches)
                {
                    if (!touch.press.isPressed) continue;
                    touchBuffer.Add(new TouchPointer { Id = touch.touchId.ReadValue(), Position = touch.position.ReadValue() });
                    anyTouch = true;
                }
            }
            var mouse = Mouse.current;
            if (!anyTouch && mouse != null && mouse.leftButton.isPressed)
                touchBuffer.Add(new TouchPointer { Id = MouseId, Position = mouse.position.ReadValue() });
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                touchBuffer.Add(new TouchPointer { Id = t.fingerId, Position = t.position });
                anyTouch = true;
            }
            if (!anyTouch && Input.GetMouseButton(0))
                touchBuffer.Add(new TouchPointer { Id = MouseId, Position = Input.mousePosition });
#endif
            return touchBuffer;
        }

        public static bool HasTiltSensor
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Accelerometer.current != null;
#else
                return SystemInfo.supportsAccelerometer;
#endif
            }
        }

        /// <summary>
        /// Gravity direction in the device's natural (portrait) frame, normalized.
        /// Returns Vector3.zero when no accelerometer is available.
        /// </summary>
        public static Vector3 ReadGravity()
        {
#if ENABLE_INPUT_SYSTEM
            var acc = Accelerometer.current;
            if (acc == null) return Vector3.zero;
            if (!acc.enabled) InputSystem.EnableDevice(acc);
            Vector3 g = acc.acceleration.ReadValue();
#else
            Vector3 g = Input.acceleration;
#endif
            return g.sqrMagnitude > 0.0001f ? g.normalized : Vector3.zero;
        }
    }
}
