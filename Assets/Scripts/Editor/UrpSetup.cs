using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Switches the project to the Universal Render Pipeline. Runs automatically when the editor
    /// loads (and before CI builds), so a fresh clone needs no manual setup:
    /// creates Assets/Settings/RacingGame_URP.asset (+ its renderer) tuned for phones and assigns it
    /// as the default pipeline. Re-run from the menu to re-apply the settings.
    /// </summary>
    [InitializeOnLoad]
    public static class UrpSetup
    {
        public const string AssetPath = "Assets/Settings/RacingGame_URP.asset";
        const string RendererPath = "Assets/Settings/RacingGame_URP_Renderer.asset";

        static UrpSetup()
        {
            // Wait until the asset database is ready.
            EditorApplication.delayCall += () => Ensure(false);
        }

        [MenuItem("Racing Game/Set Up URP (Universal Render Pipeline)")]
        static void Menu() => Ensure(true, reapplySettings: true);

        public static UniversalRenderPipelineAsset Ensure(bool log, bool reapplySettings = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            bool created = false;
            if (asset == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                asset = UniversalRenderPipelineAsset.Create();
                AssetDatabase.CreateAsset(asset, AssetPath);
                // Creates and saves the Universal Renderer (with its post-processing data) next to the project root,
                // then moves it beside the pipeline asset.
                var renderer = asset.LoadBuiltinRendererData();
                string tempPath = AssetDatabase.GetAssetPath(renderer);
                if (!string.IsNullOrEmpty(tempPath) && tempPath != RendererPath)
                    AssetDatabase.MoveAsset(tempPath, RendererPath);
                created = true;
            }

            if (created || reapplySettings) Configure(asset);

            if (GraphicsSettings.defaultRenderPipeline != asset)
            {
                GraphicsSettings.defaultRenderPipeline = asset;
                created = true;
            }
            // Quality levels use the default pipeline unless they override it.
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != null && QualitySettings.renderPipeline != asset)
                    QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(current, false);

            if (created || reapplySettings)
            {
                AssetDatabase.SaveAssets();
                if (log || created) Debug.Log("Racing Game: Universal Render Pipeline active (" + AssetPath + ").");
            }
            return asset;
        }

        /// <summary>Phone-friendly settings: no HDR, 4x MSAA, soft shadows from the sun over the visible area only.</summary>
        static void Configure(UniversalRenderPipelineAsset asset)
        {
            asset.supportsHDR = false;
            asset.msaaSampleCount = 4;
            asset.renderScale = 1f;
            asset.supportsCameraDepthTexture = false;
            asset.supportsCameraOpaqueTexture = false;
            asset.useSRPBatcher = true;
            asset.supportsDynamicBatching = false;
            asset.maxAdditionalLightsCount = 2;
            asset.mainLightShadowmapResolution = 2048;
            asset.shadowDistance = 170f;      // the isometric camera sits ~80-130 m from the kart
            asset.shadowCascadeCount = 2;
            asset.cascade2Split = 0.45f;
            asset.shadowDepthBias = 1.5f;

            // A few options only have internal setters.
            var so = new SerializedObject(asset);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void SetBool(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = value;
        }
    }
}
