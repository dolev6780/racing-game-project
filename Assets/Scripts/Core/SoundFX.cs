using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Core
{
    /// <summary>
    /// Small synthesized UI/race sounds (no audio files needed): countdown beeps, lap chime, overtake blip.
    /// </summary>
    public static class SoundFX
    {
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static AudioSource source;

        public static void CountdownBeep() => Play(Tone("beep_low", 520f, 0.18f), 0.5f);
        public static void GoBeep() => Play(Tone("beep_high", 1040f, 0.45f), 0.55f);
        public static void LapChime() => Play(Chord("lap", new[] { 660f, 880f, 1320f }, 0.5f), 0.45f);
        public static void Overtake() => Play(Tone("blip", 880f, 0.08f), 0.35f);

        static void Play(AudioClip clip, float volume)
        {
            if (clip == null) return;
            if (source == null)
            {
                var go = new GameObject("SoundFX");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
            }
            source.PlayOneShot(clip, volume);
        }

        /// <summary>A soft square-ish tone with a quick fade in/out.</summary>
        static AudioClip Tone(string key, float freq, float seconds) => Chord(key, new[] { freq }, seconds);

        static AudioClip Chord(string key, float[] freqs, float seconds)
        {
            if (clips.TryGetValue(key, out var c) && c != null) return c;
            const int rate = 44100;
            int n = Mathf.CeilToInt(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((seconds - t) / 0.08f);
                float v = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    // Arpeggiate chords: each note starts a little later.
                    float start = f * seconds / (freqs.Length * 2f);
                    if (t < start) continue;
                    float ph = (t - start) * freqs[f];
                    float sine = Mathf.Sin(ph * Mathf.PI * 2f);
                    v += (sine + 0.3f * Mathf.Sign(sine)) * 0.5f;   // a bit of square for a "game" sound
                }
                data[i] = v / freqs.Length * env * 0.6f;
            }
            c = AudioClip.Create(key, n, 1, rate, false);
            c.SetData(data, 0);
            clips[key] = c;
            return c;
        }
    }
}
