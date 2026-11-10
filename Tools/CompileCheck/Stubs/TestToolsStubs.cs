// Minimal stand-ins for Unity Test Framework attributes (compile check only).
#if UNITY_INCLUDE_TESTS
namespace UnityEngine.TestTools
{
    [System.AttributeUsage(System.AttributeTargets.Method)] public class UnityTestAttribute : System.Attribute { }
    public static class LogAssert
    {
        public static bool ignoreFailingMessages { get; set; }
        public static void Expect(UnityEngine.LogType type, string message) { }
        public static void Expect(UnityEngine.LogType type, System.Text.RegularExpressions.Regex message) { }
        public static void NoUnexpectedReceived() { }
    }
    [System.AttributeUsage(System.AttributeTargets.Method)] public class UnitySetUpAttribute : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Method)] public class UnityTearDownAttribute : System.Attribute { }
}
#endif
