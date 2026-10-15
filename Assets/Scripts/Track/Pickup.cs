using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;
using RacingGame.Progress;

namespace RacingGame.Track
{
    public enum PickupType { Money, Armor }

    /// <summary>Money bags and armor repairs on the track. They respawn after a few seconds.</summary>
    public class Pickup : MonoBehaviour
    {
        public const int MoneyValue = 100;
        const float Radius = 2.2f;
        const float RespawnTime = 8f;

        static readonly Collider[] overlap = new Collider[8];

        PickupType type;
        GameObject visual;
        float hiddenUntil;

        public static Pickup Create(Transform parent, Vector3 position, PickupType type)
        {
            var go = new GameObject(type + "Pickup");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            GameObject visual;
            if (type == PickupType.Money)
                visual = MaterialFactory.Visual(PrimitiveType.Cylinder, go.transform, Vector3.zero,
                    new Vector3(1.2f, 0.12f, 1.2f), new Color(1f, 0.85f, 0.1f), Quaternion.Euler(90f, 0f, 0f));
            else
                visual = MaterialFactory.Visual(PrimitiveType.Cube, go.transform, Vector3.zero,
                    new Vector3(1f, 1f, 1f), new Color(0.2f, 1f, 0.4f));

            var p = go.AddComponent<Pickup>();
            p.type = type;
            p.visual = visual;
            return p;
        }

        void Update()
        {
            bool available = Time.time >= hiddenUntil;
            if (visual.activeSelf != available) visual.SetActive(available);
            if (!available) return;

            transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);

            int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, overlap,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hp = overlap[i].GetComponentInParent<CarHealth>();
                if (hp == null || hp.IsWrecked) continue;

                if (type == PickupType.Money)
                {
                    hp.Controller.MoneyCollected += MoneyValue;
                    if (hp.Controller.IsPlayer) Stats.Add(Stat.MoneyPickups);
                }
                else hp.Repair(40f);

                hiddenUntil = Time.time + RespawnTime;
                return;
            }
        }
    }
}
