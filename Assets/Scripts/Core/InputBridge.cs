using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RacingGame.Core
{
    public enum GameKey { Up, Down, Left, Right, Fire, Mine, Boost, Camera, Restart }

    /// <summary>
    /// Thin wrapper so the game works with the new Input System, the legacy Input Manager, or both.
    /// Screen positions use Unity's convention: origin at bottom-left.
    /// </summary>
    public static class InputBridge
    {
        static readonly List<Vector2> touchBuffer = new List<Vector2>(10);

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
                default: return false;
            }
#endif
        }

        /// <summary>
        /// Current touch points (plus the mouse while its left button is held, so touch
        /// controls can be tested in the editor).
        /// </summary>
        public static List<Vector2> GetPointers()
        {
            touchBuffer.Clear();
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var touch in ts.touches)
                    if (touch.press.isPressed)
                        touchBuffer.Add(touch.position.ReadValue());
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                touchBuffer.Add(mouse.position.ReadValue());
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled)
                    touchBuffer.Add(t.position);
            }
            if (Input.touchCount == 0 && Input.GetMouseButton(0))
                touchBuffer.Add(Input.mousePosition);
#endif
            return touchBuffer;
        }
    }
}
