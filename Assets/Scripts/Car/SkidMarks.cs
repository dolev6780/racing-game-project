using UnityEngine;
using RacingGame.Core;

namespace RacingGame.Car
{
    /// <summary>Dark tyre marks left on the road by the rear wheels while drifting or sliding.</summary>
    [RequireComponent(typeof(CarController))]
    public class SkidMarks : MonoBehaviour
    {
        CarController car;
        TrailRenderer left, right;

        void Start()
        {
            car = GetComponent<CarController>();
            var mat = ToonStyle.Skid();
            if (mat == null) { enabled = false; return; }
            left = Create("SkidL", new Vector3(-0.95f, 0.04f, -1.25f), mat);
            right = Create("SkidR", new Vector3(0.95f, 0.04f, -1.25f), mat);
        }

        TrailRenderer Create(string name, Vector3 localPos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            // TransformZ alignment: the trail faces along the emitter's Z, so point Z up to lie flat on the road.
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var t = go.AddComponent<TrailRenderer>();
            t.sharedMaterial = mat;
            t.alignment = LineAlignment.TransformZ;
            t.time = 4f;
            t.minVertexDistance = 0.35f;
            t.widthMultiplier = 0.32f;
            t.numCapVertices = 0;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            t.colorGradient = g;
            t.emitting = false;
            return t;
        }

        void Update()
        {
            bool marking = !car.Disabled && car.IsGrounded && (car.IsDrifting || Mathf.Abs(car.LateralSpeed) > 6f);
            left.emitting = marking;
            right.emitting = marking;
        }
    }
}
