// Minimal stand-ins for the UnityEditor API used by the editor menu (compile check only).
#if UNITY_EDITOR
namespace UnityEditor
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class MenuItem : System.Attribute { public MenuItem(string path) { } }
    public class EditorBuildSettingsScene { public string path; public EditorBuildSettingsScene(string p, bool enabled) { path = p; } }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
    public static class EditorApplication { public static bool isPlaying; }
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
