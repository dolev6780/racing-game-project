using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;

namespace RacingGame.Weapons
{
    public class Missile : MonoBehaviour
    {
        const float Radius = 0.35f;
        const float Lifetime = 2.5f;

        Vector3 direction;
        float speed;
        float damage;
        float age;
        CarHealth owner;

        public static void Spawn(Vector3 position, Vector3 direction, float speed, float damage, CarHealth owner, Color color)
        {
            var go = new GameObject("Missile");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            MaterialFactory.Visual(PrimitiveType.Capsule, go.transform, Vector3.zero, new Vector3(0.3f, 0.5f, 0.3f),
                new Color(0.85f, 0.85f, 0.85f), Quaternion.Euler(90f, 0f, 0f));
            MaterialFactory.Visual(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0f, -0.6f), new Vector3(0.35f, 0.35f, 0.7f),
                Color.Lerp(color, new Color(1f, 0.6f, 0.1f), 0.6f));

            var m = go.AddComponent<Missile>();
            m.direction = direction;
            m.speed = speed;
            m.damage = damage;
            m.owner = owner;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age > Lifetime) { Explode(transform.position, null); return; }

            float step = speed * Time.deltaTime;
            var hits = Physics.SphereCastAll(transform.position, Radius, direction, step,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float closest = float.MaxValue;
            RaycastHit? best = null;
            foreach (var hit in hits)
            {
                var hp = hit.collider.GetComponentInParent<CarHealth>();
                if (hp != null && hp == owner) continue;
                if (hit.distance < closest) { closest = hit.distance; best = hit; }
            }

            if (best.HasValue)
            {
                var hit = best.Value;
                Vector3 point = hit.point == Vector3.zero ? transform.position : hit.point;
                Explode(point, hit.collider.GetComponentInParent<CarHealth>());
                return;
            }

            transform.position += direction * step;
        }

        void Explode(Vector3 point, CarHealth victim)
        {
            if (victim != null && !victim.IsWrecked)
            {
                bool blocked = victim.HasShield;
                victim.TakeDamage(damage, owner);
                if (!blocked) victim.Controller.LoseCoins(2);   // a missile hit knocks 2 coins loose
                var rb = victim.Controller.Body;
                if (!rb.isKinematic)
                    rb.AddForce(direction * 6f + Vector3.up * 3f, ForceMode.VelocityChange);
            }
            Explosion.Spawn(point, victim != null ? 3.5f : 2f, new Color(1f, 0.7f, 0.2f));
            Destroy(gameObject);
        }
    }
}
