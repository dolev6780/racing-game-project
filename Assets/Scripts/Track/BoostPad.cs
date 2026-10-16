using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;

namespace RacingGame.Track
{
    /// <summary>Glowing arrow pad on the road: any car driving over it gets a short speed boost.</summary>
    public class BoostPad : MonoBehaviour
    {
        public const float Length = 7f, Width = 4.5f;
        const float BoostTime = 1.1f;

        public static BoostPad Create(Transform parent, Vector3 position, Vector3 forward)
        {
            var go = new GameObject("BoostPad");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position + Vector3.up * 0.04f, Quaternion.LookRotation(forward));

            // Visual: a flat quad whose UV.y runs along the driving direction (shader scrolls arrows along it).
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(go.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(Width, Length, 1f);
            var mat = ToonStyle.BoostPad();
            quad.GetComponent<Renderer>().sharedMaterial = mat != null ? mat : MaterialFactory.Get(new Color(1f, 0.6f, 0.1f));

            // Trigger volume (cars have rigidbodies, so OnTriggerEnter fires).
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.8f, 0f);
            trigger.size = new Vector3(Width, 1.6f, Length);

            return go.AddComponent<BoostPad>();
        }

        void OnTriggerEnter(Collider other)
        {
            var car = other.GetComponentInParent<CarController>();
            if (car == null || car.Disabled) return;
            car.StartBoost(BoostTime);
            if (car.IsPlayer) ModelLibrary.PlayAt("impact", transform.position, 0.35f, 2.4f, 0f);
        }
    }
}
