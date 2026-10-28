using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Sparks, embers and dust: three shared particle systems (falling, rising, floating) fed with Emit,
    /// plus synthesized sound effects. Everything is created on first use.
    /// </summary>
    public static class CbFx
    {
        public static readonly Color Hit = new Color(1f, 0.91f, 0.66f) * 3f;
        public static readonly Color Fire = new Color(1f, 0.54f, 0.12f) * 3.4f;
        public static readonly Color Flame = new Color(1f, 0.81f, 0.35f) * 3f;
        public static readonly Color Red = new Color(1f, 0.29f, 0.23f) * 3f;
        public static readonly Color Purple = new Color(0.75f, 0.42f, 1f) * 3f;
        public static readonly Color Dust = new Color(0.79f, 0.69f, 0.54f) * 0.8f;
        public static readonly Color Grey = new Color(0.84f, 0.87f, 0.91f) * 1.6f;

        static ParticleSystem falling, rising, floating;
        static Transform holder;

        public static void Init(Transform parent)
        {
            holder = new GameObject("FX").transform;
            holder.SetParent(parent, false);
            falling = Make("sparks", 0.6f);
            rising = Make("embers", -0.5f);
            floating = Make("trails", 0f);
        }

        static ParticleSystem Make(string name, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;   // keeps playing so emitted particles always simulate (emission itself is off)
            main.playOnAwake = false;
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSpeed = 0;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1.4f), new Keyframe(1, 0.4f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = CbArt.Sparks;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>
        /// A burst of <paramref name="n"/> particles. <paramref name="grav"/> &gt; 0 falls, &lt; 0 rises, 0 floats
        /// (the three demo cases); <paramref name="dirX"/> pushes the burst sideways.
        /// </summary>
        public static void Spark(Vector3 pos, Color c, int n, float speed = 7f, float size = 0.35f, float life = 0.4f, float grav = 6f, float dirX = 0f)
        {
            if (falling == null) return;
            var ps = grav > 0.5f ? falling : grav < -0.5f ? rising : floating;
            // the sparks shader brightens the core itself; keep vertex colours in 0..1 and pass brightness as size
            var col = (Color32)new Color(Mathf.Clamp01(c.r / 3f), Mathf.Clamp01(c.g / 3f), Mathf.Clamp01(c.b / 3f), 1f);
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < n; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), v = Random.Range(0.3f, 1f) * speed;
                p.position = pos;
                p.velocity = new Vector3(Mathf.Cos(a) * v + dirX, Mathf.Sin(a) * v, Random.Range(-1f, 1f) * speed * 0.3f);
                p.startLifetime = life * Random.Range(0.6f, 1.2f);
                p.startSize = size * Random.Range(0.6f, 1.3f) * 1.4f;
                p.startColor = col;
                ps.Emit(p, 1);
            }
        }

        // ---------- sound ----------

        static AudioSource source;
        static readonly System.Collections.Generic.Dictionary<string, AudioClip> clips = new System.Collections.Generic.Dictionary<string, AudioClip>();

        public static void Sfx(string type, float volume = 1f)
        {
            if (source == null)
            {
                if (holder == null) return;
                source = holder.gameObject.AddComponent<AudioSource>();
                source.spatialBlend = 0f;
            }
            var clip = Clip(type);
            if (clip != null) source.PlayOneShot(clip, volume);
        }

        static AudioClip Clip(string type)
        {
            if (clips.TryGetValue(type, out var c)) return c;
            switch (type)
            {
                case "swing": c = Synth(type, 0.1f, (t, d) => Noise() * Band(t, 0.1f) * 0.3f); break;
                case "staff": c = Synth(type, 0.16f, (t, d) => (Noise() * 0.35f + Saw(t, 520 - 1400 * t) * 0.08f) * Band(t, 0.16f)); break;
                case "hit": c = Synth(type, 0.2f, (t, d) => (Mathf.Sin(2 * Mathf.PI * (160 - 500 * t) * t) * 0.7f + Noise() * 0.4f * Mathf.Clamp01(1 - t / 0.08f)) * Band(t, 0.2f)); break;
                case "heavy": c = Synth(type, 0.35f, (t, d) => (Mathf.Sin(2 * Mathf.PI * (110 - 200 * t) * t) * 0.9f + Noise() * 0.5f * Mathf.Clamp01(1 - t / 0.2f)) * Band(t, 0.35f)); break;
                case "fire": c = Synth(type, 0.3f, (t, d) => (Noise() * 0.5f + Saw(t, 180 + 800 * t) * 0.06f) * Band(t, 0.3f)); break;
                case "roar": c = Synth(type, 0.9f, (t, d) => (Saw(t, 120 - 70 * t) * 0.25f + Noise() * 0.45f) * Band(t, 0.9f)); break;
                case "block": c = Synth(type, 0.09f, (t, d) => (Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 900 * t)) * 0.1f + Noise() * 0.2f) * Band(t, 0.09f)); break;
                case "hurt": c = Synth(type, 0.2f, (t, d) => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * (220 - 300 * t) * t)) * 0.15f * Band(t, 0.2f)); break;
                case "jump": c = Synth(type, 0.12f, (t, d) => Mathf.Sin(2 * Mathf.PI * (300 + 1200 * t) * t) * 0.15f * Band(t, 0.12f)); break;
                case "dash": c = Synth(type, 0.18f, (t, d) => Noise() * 0.3f * Band(t, 0.18f)); break;
                case "perfect": c = Synth(type, 0.3f, (t, d) => (Mathf.Sin(2 * Mathf.PI * (880 + 1500 * t) * t) * 0.15f + Mathf.Sin(2 * Mathf.PI * (660 + 1100 * t) * t) * 0.1f) * Band(t, 0.3f)); break;
                case "charge": c = Synth(type, 0.25f, (t, d) => Mathf.PingPong(t * (200 + 400 * t) * 2, 1) * 0.06f * Band(t, 0.25f)); break;
            }
            clips[type] = c;
            return c;
        }

        static float Noise() => Random.Range(-1f, 1f);
        static float Saw(float t, float f) => (t * f) % 1f * 2f - 1f;
        static float Band(float t, float len) => Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((len - t) / (len * 0.8f));

        static AudioClip Synth(string key, float seconds, System.Func<float, float, float> f)
        {
            const int rate = 22050;
            int n = Mathf.CeilToInt(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)rate, seconds), -1f, 1f);
            var c = AudioClip.Create(key, n, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
