// Minimal stand-ins for the Unity Input System API surface used by the game (compile check only).
#if ENABLE_INPUT_SYSTEM
namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl { public bool isPressed => false; public bool wasPressedThisFrame => false; }
    public class Vector2Control { public UnityEngine.Vector2 ReadValue() => default; }
    public class Vector3Control { public UnityEngine.Vector3 ReadValue() => default; }
    public class IntegerControl { public int ReadValue() => 0; }
    public class TouchControl
    {
        public ButtonControl press = new ButtonControl();
        public Vector2Control position = new Vector2Control();
        public IntegerControl touchId = new IntegerControl();
    }
}
namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;
    using UnityEngine.InputSystem.Utilities;
    public class InputDevice { public bool enabled => true; }
    public class Keyboard : InputDevice
    {
        public static Keyboard current;
        public ButtonControl wKey, aKey, sKey, dKey, upArrowKey, downArrowKey, leftArrowKey, rightArrowKey,
            spaceKey, jKey, kKey, eKey, lKey, cKey, rKey, pKey, leftShiftKey, escapeKey;
    }
    public class Mouse : InputDevice { public static Mouse current; public ButtonControl leftButton; public Vector2Control position; }
    public class Touchscreen : InputDevice { public static Touchscreen current; public ReadOnlyArray<TouchControl> touches; }
    public class Accelerometer : InputDevice { public static Accelerometer current; public Vector3Control acceleration; }
    public static class InputSystem { public static void EnableDevice(InputDevice d) { } }
}
namespace UnityEngine.InputSystem.Utilities
{
    public struct ReadOnlyArray<T> : System.Collections.Generic.IEnumerable<T>
    {
        public System.Collections.Generic.IEnumerator<T> GetEnumerator() { yield break; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule { public override void Process() { } }
}
#endif
