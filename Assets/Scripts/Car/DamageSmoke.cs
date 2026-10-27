using UnityEngine;
using RacingGame.Core;

namespace RacingGame.Car
{
    /// <summary>Grey smoke puffs from a badly damaged kart: the lower its armor, the thicker the smoke.</summary>
    [RequireComponent(typeof(CarHealth))]
    public class DamageSmoke : MonoBehaviour
    {
        const float StartsBelow = 0.4f;   // armor fraction

        CarHealth health;
        ParticleSystem smoke;
        ParticleSystem.EmissionModule emission;

        void Start()
        {
            health = GetComponent<CarHealth>();
            var mat = ToonStyle.Smoke();
            if (mat == null) { enabled = false; return; }

            var go = new GameObject("DamageSmoke");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, -1.2f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // emit upwards

            smoke = go.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.25f, 0.28f, 0.7f), new Color(0.45f, 0.45f, 0.5f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.2f;

            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));

            var fade = smoke.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            emission = smoke.emission;
            emission.rateOverTime = 0f;
            smoke.Play();
        }

        void Update()
        {
            float a = health.Armor01;
            bool smoking = !health.IsWrecked && a < StartsBelow;
            emission.rateOverTime = smoking ? Mathf.Lerp(18f, 4f, a / StartsBelow) : 0f;
        }
    }
}
