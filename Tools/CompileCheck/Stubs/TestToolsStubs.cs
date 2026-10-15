// Minimal stand-ins for Unity Test Framework attributes (compile check only).
#if UNITY_INCLUDE_TESTS
namespace UnityEngine.TestTools
{
    [System.AttributeUsage(System.AttributeTargets.Method)] public class UnityTestAttribute : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Method)] public class UnitySetUpAttribute : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Method)] public class UnityTearDownAttribute : System.Attribute { }
}
#endif
