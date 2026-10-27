using System.Collections.Generic;
using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;

namespace RacingGame.Weapons
{
    /// <summary>A puddle of oil dropped behind a kart: anyone who drives over it spins out.</summary>
    public class OilSlick : MonoBehaviour
    {
        public const float Radius = 2.4f;
        public const float SpinSeconds = 1.1f;
        const float Lifetime = 20f;
        const float OwnerSafeTime = 1.5f;
        const int MaxOnTrack = 12;

        static readonly List<OilSlick> active = new List<OilSlick>();
        static readonly Collider[] overlap = new Collider[16];

        CarHealth owner;
        float spawnTime;
        readonly Dictionary<CarHealth, float> lastHit = new Dictionary<CarHealth, float>();

        public static OilSlick Spawn(Vector3 position, CarHealth owner)
        {
            if (active.Count >= MaxOnTrack && active[0] != null) Destroy(active[0].gameObject);

            var go = new GameObject("OilSlick");
            go.transform.position = position;
            MaterialFactory.Visual(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(Radius * 2f, 0.02f, Radius * 1.7f),
                new Color(0.07f, 0.06f, 0.1f));
            MaterialFactory.Visual(PrimitiveType.Cylinder, go.transform, new Vector3(-0.5f, 0.015f, 0.3f), new Vector3(1.2f, 0.02f, 0.7f),
                new Color(0.32f, 0.25f, 0.5f));   // oily sheen

            var oil = go.AddComponent<OilSlick>();
            oil.owner = owner;
            oil.spawnTime = Time.time;
            return oil;
        }

        public static void ClearAll()
        {
            foreach (var o in active.ToArray())
                if (o) Destroy(o.gameObject);
            active.Clear();
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void FixedUpdate()
        {
            float age = Time.time - spawnTime;
            if (age > Lifetime) { Destroy(gameObject); return; }

            int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, overlap,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hp = overlap[i].GetComponentInParent<CarHealth>();
                if (hp == null || hp.IsWrecked) continue;
                if (hp == owner && age < OwnerSafeTime) continue;
                if (lastHit.TryGetValue(hp, out float t) && Time.time - t < 2f) continue;   // one spin per pass
                lastHit[hp] = Time.time;
                if (hp.TryBlock()) continue;
                hp.Controller.SpinOut(SpinSeconds);
            }
        }
    }
}
