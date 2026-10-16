using UnityEngine;
using RacingGame.Core;

namespace RacingGame.Car
{
    /// <summary>
    /// Particle sparks from the rear wheels while drifting (blue -> orange -> purple as the
    /// drift charges), a burst on mini-turbo release, and exhaust flames while boosting.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class DriftSparks : MonoBehaviour
    {
        static readonly Color[] TierColors =
        {
            new Color(1f, 1f, 0.8f, 0.6f),    // sliding, not charged yet
            new Color(0.3f, 0.65f, 1f, 1f),   // blue
            new Color(1f, 0.55f, 0.1f, 1f),   // orange
            new Color(0.85f, 0.3f, 1f, 1f),   // purple
        };

        CarController car;
        ParticleSystem left, right, exhaust;

        void Start()
        {
            car = GetComponent<CarController>();
            var mat = ToonStyle.Sparks();
            if (mat == null) { enabled = false; return; }   // shader unavailable: no particles

            left = Create("SparksL", new Vector3(-0.95f, 0.15f, -1.3f), Quaternion.Euler(-25f, 200f, 0f), mat, 0.25f);
            right = Create("SparksR", new Vector3(0.95f, 0.15f, -1.3f), Quaternion.Euler(-25f, 160f, 0f), mat, 0.25f);
            exhaust = Create("Exhaust", new Vector3(0f, 0.6f, -2f), Quaternion.Euler(0f, 180f, 0f), mat, 0.45f);
            car.MiniTurbo += OnMiniTurbo;
        }

        void OnDestroy()
        {
            if (car != null) car.MiniTurbo -= OnMiniTurbo;
        }

        ParticleSystem Create(string name, Vector3 localPos, Quaternion localRot, Material mat, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.2f;
            main.maxParticles = 150;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.08f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(g);

            var shrink = ps.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            return ps;
        }

        void Update()
        {
            bool alive = !car.Disabled && car.IsGrounded;
            int tier = car.DriftTier;
            float rate = alive && car.IsDrifting ? (tier == 0 ? 15f : 45f + tier * 15f) : 0f;
            Color c = TierColors[Mathf.Clamp(tier, 0, 3)];
            SetEmission(left, rate, c);
            SetEmission(right, rate, c);
            SetEmission(exhaust, !car.Disabled && car.IsBoosting ? 70f : 0f, new Color(1f, 0.6f, 0.2f, 1f));
        }

        static void SetEmission(ParticleSystem ps, float rate, Color color)
        {
            var e = ps.emission;
            e.rateOverTime = rate;
            var main = ps.main;
            main.startColor = color;
        }

        void OnMiniTurbo(int tier)
        {
            Color c = TierColors[Mathf.Clamp(tier, 0, 3)];
            var main = exhaust.main;
            main.startColor = c;
            exhaust.Emit(12 + tier * 8);
        }
    }
}
