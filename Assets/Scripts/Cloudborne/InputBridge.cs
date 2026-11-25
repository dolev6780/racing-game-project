using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Cloudborne
{
    /// <summary>A finger on the screen (or the mouse, id -1). Position origin is bottom-left.</summary>
    public struct TouchPointer
    {
        public int Id;
        public Vector2 Position;
    }

    /// <summary>
    /// The fingers on the screen, with the new Input System or the legacy Input Manager (the touch controls and the
    /// menu read them). Screen positions: origin at bottom-left.
    /// </summary>
    public static class InputBridge
    {
        static readonly List<TouchPointer> touchBuffer = new List<TouchPointer>(10);
        const int MouseId = -1;

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
    }
}
