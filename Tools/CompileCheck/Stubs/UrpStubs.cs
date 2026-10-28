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

// Volumes and post-processing (com.unity.render-pipelines.core / universal 17.0).
namespace UnityEngine.Rendering
{
    public class VolumeParameter<T> { public T value; public bool overrideState; public void Override(T x) { value = x; overrideState = true; } }
    public class FloatParameter : VolumeParameter<float> { public FloatParameter(float v, bool o = false) { } }
    public class MinFloatParameter : FloatParameter { public MinFloatParameter(float v, float min, bool o = false) : base(v, o) { } }
    public class ClampedFloatParameter : FloatParameter { public ClampedFloatParameter(float v, float min, float max, bool o = false) : base(v, o) { } }
    public class ColorParameter : VolumeParameter<Color> { public ColorParameter(Color v, bool o = false) { } }
    public class BoolParameter : VolumeParameter<bool> { public BoolParameter(bool v, bool o = false) { } }
    public abstract class VolumeComponent : ScriptableObject { public bool active = true; }
    public class VolumeProfile : ScriptableObject
    {
        public System.Collections.Generic.List<VolumeComponent> components = new System.Collections.Generic.List<VolumeComponent>();
        public T Add<T>(bool overrides = false) where T : VolumeComponent => null;
        public bool TryGet<T>(out T component) where T : VolumeComponent { component = null; return false; }
    }
    public class Volume : MonoBehaviour { public bool isGlobal; public float priority; public float weight; public VolumeProfile profile; public VolumeProfile sharedProfile; }
}
namespace UnityEngine.Rendering.Universal
{
    public enum TonemappingMode { None, Neutral, ACES }
    public class TonemappingModeParameter : UnityEngine.Rendering.VolumeParameter<TonemappingMode> { public TonemappingModeParameter(TonemappingMode v, bool o = false) { } }
    public sealed class Bloom : UnityEngine.Rendering.VolumeComponent
    {
        public UnityEngine.Rendering.MinFloatParameter threshold, intensity;
        public UnityEngine.Rendering.ClampedFloatParameter scatter;
        public UnityEngine.Rendering.ColorParameter tint;
        public UnityEngine.Rendering.BoolParameter highQualityFiltering;
    }
    public sealed class Tonemapping : UnityEngine.Rendering.VolumeComponent { public TonemappingModeParameter mode; }
    public sealed class Vignette : UnityEngine.Rendering.VolumeComponent
    {
        public UnityEngine.Rendering.ColorParameter color;
        public UnityEngine.Rendering.ClampedFloatParameter intensity, smoothness;
    }
    public sealed class ColorAdjustments : UnityEngine.Rendering.VolumeComponent
    {
        public UnityEngine.Rendering.FloatParameter postExposure;
        public UnityEngine.Rendering.ClampedFloatParameter contrast, saturation;
        public UnityEngine.Rendering.ColorParameter colorFilter;
    }
    public class UniversalAdditionalCameraData : MonoBehaviour { public bool renderPostProcessing; public bool renderShadows; }
    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null; }
}
