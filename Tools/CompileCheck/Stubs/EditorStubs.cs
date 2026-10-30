// Minimal stand-ins for the UnityEditor API used by the editor menu (compile check only).
#if UNITY_EDITOR
namespace UnityEditor
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class MenuItem : System.Attribute { public MenuItem(string path) { } }
    public class EditorBuildSettingsScene { public string path; public EditorBuildSettingsScene(string p, bool enabled) { path = p; } }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
    public static class EditorApplication
    {
        public delegate void CallbackFunction();
        public static bool isPlaying, isPlayingOrWillChangePlaymode;
        public static CallbackFunction delayCall;
        public static void Exit(int code) { }
    }
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class InitializeOnLoadAttribute : System.Attribute { }
    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static string GetAssetPath(UnityEngine.Object asset) => "";
        public static string MoveAsset(string from, string to) => "";
        public static void SaveAssets() { }
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string path) => null;
        public static void AddObjectToAsset(UnityEngine.Object o, UnityEngine.Object asset) { }
    }
    public static class EditorUtility { public static void SetDirty(UnityEngine.Object o) { } }
    public static class SessionState
    {
        public static bool GetBool(string key, bool defaultValue) => defaultValue;
        public static void SetBool(string key, bool value) { }
    }
    public class SerializedProperty { public bool boolValue; public int intValue; }
    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object o) { }
        public SerializedProperty FindProperty(string name) => null;
        public bool ApplyModifiedPropertiesWithoutUndo() => true;
    }
    public enum BuildTarget { Android, iOS, StandaloneWindows64 }
    [System.Flags] public enum BuildOptions { None = 0, Development = 1 }
    public struct BuildPlayerOptions
    {
        public string[] scenes;
        public string locationPathName;
        public BuildTarget target;
        public BuildOptions options;
    }
    public static class BuildPipeline
    {
        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions options) => null;
    }
    public static class EditorUserBuildSettings { public static bool buildAppBundle; }
    public enum ScriptingImplementation { Mono2x, IL2CPP }
    [System.Flags] public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2 }
    public enum UIOrientation { Portrait, PortraitUpsideDown, LandscapeRight, LandscapeLeft, AutoRotation }
    public static class PlayerSettings
    {
        public static string productName;
        public static UIOrientation defaultInterfaceOrientation;
        public static bool allowedAutorotateToPortrait, allowedAutorotateToPortraitUpsideDown,
            allowedAutorotateToLandscapeLeft, allowedAutorotateToLandscapeRight;
        public static void SetScriptingBackend(UnityEditor.Build.NamedBuildTarget target, ScriptingImplementation backend) { }
        public static void SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget target, string id) { }
        public static class Android { public static AndroidArchitecture targetArchitectures; }
    }
}
namespace UnityEditor.Build
{
    public struct NamedBuildTarget { public static readonly NamedBuildTarget Android; }
}
namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown, Succeeded, Failed, Cancelled }
    public struct BuildSummary { public BuildResult result; public ulong totalSize; public int totalErrors; }
    public class BuildReport { public BuildSummary summary; }
}
namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public static class EditorSceneManager
    {
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true;
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup s, NewSceneMode m) => default;
        public static bool SaveScene(UnityEngine.SceneManagement.Scene s, string path) => true;
    }
}
namespace UnityEditor.TestTools.TestRunner.Api
{
    [System.Flags] public enum TestMode { EditMode = 1, PlayMode = 2 }
    public enum TestStatus { Skipped, Passed, Failed, Inconclusive }
    public class Filter { public TestMode testMode; }
    public class ExecutionSettings { public ExecutionSettings(params Filter[] filtersToExecute) { } }
    public interface ITestAdaptor { string Name { get; } string FullName { get; } int TestCaseCount { get; } }
    public interface ITestResultAdaptor
    {
        ITestAdaptor Test { get; }
        TestStatus TestStatus { get; }
        bool HasChildren { get; }
        string Message { get; }
        string StackTrace { get; }
        double Duration { get; }
        int PassCount { get; }
        int FailCount { get; }
        int SkipCount { get; }
    }
    public interface ICallbacks
    {
        void RunStarted(ITestAdaptor testsToRun);
        void RunFinished(ITestResultAdaptor result);
        void TestStarted(ITestAdaptor test);
        void TestFinished(ITestResultAdaptor result);
    }
    public class TestRunnerApi : UnityEngine.ScriptableObject
    {
        public void RegisterCallbacks<T>(T testCallbacks, int priority = 0) where T : ICallbacks { }
        public string Execute(ExecutionSettings executionSettings) => "";
        public static void SaveResultToFile(ITestResultAdaptor results, string xmlFilePath) { }
    }
}
#endif
