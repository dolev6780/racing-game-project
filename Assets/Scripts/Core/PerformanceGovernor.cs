using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RacingGame.Core
{
    /// <summary>
    /// Keeps phones near 60 fps: when frames run slow, renders the 3D view at a slightly lower
    /// resolution (URP render scale, down to 70%); raises it again when there is headroom.
    /// The UI is unaffected. Runs only in player builds (never changes the asset in the editor).
    /// </summary>
    public class PerformanceGovernor : MonoBehaviour
    {
        const float Window = 2f;              // seconds per measurement
        const float SlowFrame = 1f / 52f;     // average frame time that counts as "too slow"
        const float FastFrame = 1f / 58f;
        const float MinScale = 0.7f, MaxScale = 1f;

        float elapsed;
        int frames, fastWindows;

        /// <summary>Current 3D render scale (1 = native resolution).</summary>
        public static float RenderScale { get; private set; } = 1f;

        public static void Install()
        {
            if (Application.isEditor || FindAnyObjectByType<PerformanceGovernor>() != null) return;
            var go = new GameObject("PerformanceGovernor");
            DontDestroyOnLoad(go);
            go.AddComponent<PerformanceGovernor>();
        }

        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < Window) return;

            float avg = elapsed / frames;
            elapsed = 0f;
            frames = 0;

            if (avg > SlowFrame && RenderScale > MinScale)
            {
                Apply(RenderScale - 0.1f);
                fastWindows = 0;
            }
            else if (avg < FastFrame && RenderScale < MaxScale)
            {
                // Only step back up after a sustained run of fast frames, to avoid flip-flopping.
                if (++fastWindows >= 3)
                {
                    Apply(RenderScale + 0.05f);
                    fastWindows = 0;
                }
            }
            else fastWindows = 0;
        }

        static void Apply(float scale)
        {
            RenderScale = Mathf.Clamp(scale, MinScale, MaxScale);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = RenderScale;
        }
    }
}
