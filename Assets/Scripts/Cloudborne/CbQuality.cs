using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cloudborne
{
    /// <summary>
    /// The graphics' quality, three levels for phones that differ (the user: each phone gives other performance):
    /// <list type="bullet">
    /// <item>High: soft shadows in two cascades, 4x MSAA, bloom at half the screen, every spark.</item>
    /// <item>Medium: hard shadows in one cascade, 2x MSAA, bloom at a quarter of the screen, three sparks in four.</item>
    /// <item>Low: hard shadows close by only, no MSAA, no bloom (and no HDR), under half the sparks.</item>
    /// </list>
    /// SETTINGS chooses it, or AUTO (the default): picked from the phone's memory and cores the first time, and stepped
    /// down a level for good when the game keeps running under StepDownFps for StepDownAfter seconds of play (the 3D
    /// view's resolution drops first: RacingGame.Core.PerformanceGovernor). Off a phone it is always High.
    /// </summary>
    public static class CbQuality
    {
        public enum Tier { Low, Medium, High }

        const string SettingKey = "cb_graphics", AutoKey = "cb_autotier";
        public const float StepDownFps = 24f, StepDownAfter = 10f;

        /// <summary>The choice in SETTINGS: 0 auto, 1 low, 2 medium, 3 high.</summary>
        public static int Setting
        {
            get => PlayerPrefs.GetInt(SettingKey, 0);
            set { PlayerPrefs.SetInt(SettingKey, ((value % 4) + 4) % 4); PlayerPrefs.Save(); }
        }

        /// <summary>The level in use.</summary>
        public static Tier Current => Setting == 0 ? AutoTier : (Tier)(Setting - 1);

        /// <summary>The automatic choice: detected once, lowered when the phone can't keep up.</summary>
        static Tier AutoTier
        {
            get
            {
                int saved = PlayerPrefs.GetInt(AutoKey, -1);
                if (saved < 0) { saved = (int)Detect(); PlayerPrefs.SetInt(AutoKey, saved); PlayerPrefs.Save(); }
                return (Tier)Mathf.Clamp(saved, 0, 2);
            }
        }

        /// <summary>A first guess from the phone itself: its memory and its cores (off a phone, High).</summary>
        public static Tier Detect()
        {
            if (!Application.isMobilePlatform) return Tier.High;
            int mem = SystemInfo.systemMemorySize, cores = SystemInfo.processorCount;
            if (mem < 3500 || cores <= 4) return Tier.Low;
            if (mem < 6500) return Tier.Medium;
            return Tier.High;
        }

        /// <summary>How many of the sparks asked for are drawn.</summary>
        public static float ParticleShare => Current == Tier.High ? 1f : Current == Tier.Medium ? 0.75f : 0.45f;

        /// <summary>A count of sparks for this level (a fraction of one sometimes rounds up, so single sparks thin out).</summary>
        public static int Particles(int n) => Current == Tier.High ? n : Mathf.FloorToInt(n * ParticleShare + Random.value);

        /// <summary>The label SETTINGS shows.</summary>
        public static string Label => Setting == 0 ? $"AUTO ({Current.ToString().ToUpperInvariant()})" : Current.ToString().ToUpperInvariant();

        /// <summary>Running slowly on AUTO: one level down, for good (false when already at the lowest).</summary>
        public static bool StepDown()
        {
            if (Setting != 0 || AutoTier == Tier.Low) return false;
            PlayerPrefs.SetInt(AutoKey, (int)AutoTier - 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Sets the pipeline, the sun and the bloom for the current level.</summary>
        public static void Apply(Light sun, Bloom bloom)
        {
            var tier = Current;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.msaaSampleCount = tier == Tier.High ? 4 : tier == Tier.Medium ? 2 : 1;
                urp.shadowCascadeCount = tier == Tier.High ? 2 : 1;
                urp.shadowDistance = tier == Tier.Low ? 28f : 45f;
                // (glow above 1.0 is for the bloom: none without it)
                urp.supportsHDR = tier != Tier.Low;
            }
            QualitySettings.shadowDistance = tier == Tier.Low ? 28f : 45f;
            if (sun != null) sun.shadows = tier == Tier.High ? LightShadows.Soft : LightShadows.Hard;
            if (bloom != null)
            {
                bloom.active = tier != Tier.Low;
                bloom.downscale.Override(tier == Tier.High ? BloomDownscaleMode.Half : BloomDownscaleMode.Quarter);
            }
        }
    }
}
