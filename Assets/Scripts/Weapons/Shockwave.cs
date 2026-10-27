using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;

namespace RacingGame.Weapons
{
    /// <summary>Blast ring around a kart: damages and shoves every other kart within range.</summary>
    public class Shockwave : MonoBehaviour
    {
        const float RingTime = 0.45f;

        float radius, age;

        /// <summary>Returns how many karts were hit.</summary>
        public static int Fire(Vector3 center, float radius, float damage, CarHealth owner)
        {
            int hits = 0;
            foreach (var car in CarController.All.ToArray())
            {
                if (car.Disabled) continue;
                var hp = car.GetComponent<CarHealth>();
                if (hp == null || hp == owner || hp.IsWrecked) continue;
                Vector3 to = car.transform.position - center;
                to.y = 0f;
                if (to.magnitude > radius) continue;
                hits++;
                bool blocked = hp.HasShield;
                hp.TakeDamage(damage, owner);
                if (!blocked && !car.Body.isKinematic)
                    car.Body.AddForce((to.sqrMagnitude > 0.01f ? to.normalized : car.transform.forward) * 10f + Vector3.up * 3f,
                        ForceMode.VelocityChange);
            }

            // Expanding flat ring.
            var go = new GameObject("Shockwave");
            go.transform.position = center + Vector3.up * 0.5f;
            MaterialFactory.Visual(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(1f, 0.05f, 1f), new Color(0.55f, 0.85f, 1f));
            go.AddComponent<Shockwave>().radius = radius;
            Explosion.Spawn(center + Vector3.up, 3f, new Color(0.5f, 0.8f, 1f));
            return hits;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = age / RingTime;
            if (t >= 1f) { Destroy(gameObject); return; }
            float d = Mathf.Lerp(1f, radius * 2f, Mathf.Sqrt(t));
            transform.localScale = new Vector3(d, 1f, d);
        }
    }
}
