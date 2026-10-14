using System.Collections.Generic;
using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;

namespace RacingGame.Weapons
{
    public class Mine : MonoBehaviour
    {
        const float TriggerRadius = 1.8f;
        const float ArmDelay = 0.5f;
        const float OwnerSafeTime = 2f;
        const float Lifetime = 45f;
        const int MaxMinesOnTrack = 30;

        static readonly List<Mine> active = new List<Mine>();
        static readonly Collider[] overlap = new Collider[16];

        float damage;
        float spawnTime;
        CarHealth owner;
        GameObject lightTop;

        public static void Spawn(Vector3 position, float damage, CarHealth owner)
        {
            if (active.Count >= MaxMinesOnTrack && active[0] != null)
                Destroy(active[0].gameObject);

            var go = new GameObject("Mine");
            go.transform.position = position;
            MaterialFactory.Visual(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(1.1f, 0.1f, 1.1f),
                new Color(0.12f, 0.12f, 0.12f));
            var top = MaterialFactory.Visual(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0.12f, 0f),
                new Vector3(0.35f, 0.35f, 0.35f), new Color(1f, 0.1f, 0.1f));

            var mine = go.AddComponent<Mine>();
            mine.damage = damage;
            mine.owner = owner;
            mine.spawnTime = Time.time;
            mine.lightTop = top;
        }

        public static void ClearAll()
        {
            foreach (var m in active.ToArray())
                if (m) Destroy(m.gameObject);
            active.Clear();
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void Update()
        {
            float age = Time.time - spawnTime;
            if (age > Lifetime) { Destroy(gameObject); return; }
            if (lightTop) lightTop.SetActive(Mathf.Repeat(Time.time * 3f, 1f) > 0.5f);
        }

        void FixedUpdate()
        {
            float age = Time.time - spawnTime;
            if (age < ArmDelay) return;

            int count = Physics.OverlapSphereNonAlloc(transform.position, TriggerRadius, overlap,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hp = overlap[i].GetComponentInParent<CarHealth>();
                if (hp == null || hp.IsWrecked) continue;
                if (hp == owner && age < OwnerSafeTime) continue;

                hp.TakeDamage(damage, owner);
                var rb = hp.Controller.Body;
                if (!rb.isKinematic) rb.AddForce(Vector3.up * 7f, ForceMode.VelocityChange);
                Explosion.Spawn(transform.position, 4f, new Color(1f, 0.4f, 0.1f));
                Destroy(gameObject);
                return;
            }
        }
    }
}
