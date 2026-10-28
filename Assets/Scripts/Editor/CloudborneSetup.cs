using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Project setup for the Cloudborne prototype: an empty "Cloudborne" scene (the game builds itself from code),
    /// URP with HDR for bloom, the post-processing profile as an asset, and linear fog kept in builds.
    /// Safe to run in batch mode; the Android build calls it first.
    /// </summary>
    public static class CloudborneSetup
    {
        public const string ScenePath = "Assets/Scenes/Cloudborne.unity";
        const string PostPath = "Assets/Resources/Cloudborne/PostFX.asset";

        [MenuItem("Cloudborne/Set Up Scene and Settings")]
        static void Menu()
        {
            Ensure();
            Debug.Log("Cloudborne: scene " + ScenePath + " is the only scene in the build. Open it and press Play.");
        }

        public static string Ensure()
        {
            var urp = UrpSetup.Ensure(true);
            if (urp != null)
            {
                urp.supportsHDR = true;
                urp.shadowDistance = 45f;
                EditorUtility.SetDirty(urp);
            }
            EnsurePostProfile();
            KeepLinearFog();
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            return ScenePath;
        }

        /// <summary>
        /// Saves the bloom / tone mapping profile as an asset, so the build includes those post-processing shaders
        /// (a profile created only at runtime could have them stripped).
        /// </summary>
        static void EnsurePostProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostPath) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(PostPath));
            var profile = Cloudborne.CbGame.CreatePostProfile();
            AssetDatabase.CreateAsset(profile, PostPath);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The scene is empty in the editor and fog is switched on by code, so tell Unity to keep linear fog shaders.</summary>
        static void KeepLinearFog()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var strip = so.FindProperty("m_FogStripping");
            if (strip != null) strip.intValue = 1;   // custom
            var linear = so.FindProperty("m_FogKeepLinear");
            if (linear != null) linear.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
