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
        public static bool isPlaying;
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
    }
    public static class EditorUtility { public static void SetDirty(UnityEngine.Object o) { } }
    public class SerializedProperty { public bool boolValue; }
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
#endif
