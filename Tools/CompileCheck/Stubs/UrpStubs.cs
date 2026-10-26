// Minimal stand-ins for the Universal Render Pipeline API used by the game (compile check only).
// Signatures follow com.unity.render-pipelines.universal 17.0 (Unity 6000.0).
namespace UnityEngine.Rendering.Universal
{
    public abstract class ScriptableRendererData : ScriptableObject { }

    public class UniversalRenderPipelineAsset : UnityEngine.Rendering.RenderPipelineAsset
    {
        public override UnityEngine.Rendering.RenderPipeline CreatePipeline() => null;  // public in the reference assemblies
        public float renderScale { get; set; }
        public bool supportsHDR { get; set; }
        public int msaaSampleCount { get; set; }
        public bool supportsCameraDepthTexture { get; set; }
        public bool supportsCameraOpaqueTexture { get; set; }
        public bool useSRPBatcher { get; set; }
        public bool supportsDynamicBatching { get; set; }
        public int maxAdditionalLightsCount { get; set; }
        public int mainLightShadowmapResolution { get; set; }
        public float shadowDistance { get; set; }
        public int shadowCascadeCount { get; set; }
        public float cascade2Split { get; set; }
        public float shadowDepthBias { get; set; }
#if UNITY_EDITOR
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) => null;
        public ScriptableRendererData LoadBuiltinRendererData() => null;
#endif
    }
}
