using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;

namespace RacingGame.CameraRig
{
    /// <summary>
    /// Two views: classic isometric (Rock n' Roll Racing style, fixed angle) and a chase cam.
    /// Press C (or the CAM button) to switch.
    /// </summary>
    public class RaceCamera : MonoBehaviour
    {
        public static bool Isometric = true;

        [Header("Isometric")]
        public Vector3 isoAngles = new Vector3(50f, 45f, 0f);
        public float isoDistance = 60f;
        public float isoFov = 32f;
        public float lookAhead = 0.4f;

        [Header("Chase")]
        public float chaseDistance = 9f;
        public float chaseHeight = 3.8f;
        public float chaseFov = 60f;

        public float smoothing = 6f;

        CarController target;
        Camera cam;
        Vector3 smoothedLookAhead;

        void Awake() => cam = GetComponent<Camera>();

        public void SetTarget(CarController car)
        {
            target = car;
            Snap();
        }

        public static void Toggle() => Isometric = !Isometric;

        void Snap()
        {
            if (target == null) return;
            ComputeDesired(out Vector3 pos, out Quaternion rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (InputBridge.WasPressedThisFrame(GameKey.Camera)) Toggle();

            ComputeDesired(out Vector3 pos, out Quaternion rot);
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, k);
            if (cam) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Isometric ? isoFov : chaseFov, k);
        }

        void ComputeDesired(out Vector3 position, out Quaternion rotation)
        {
            Vector3 carPos = target.transform.position;
            if (Isometric)
            {
                Vector3 vel = target.Body != null ? target.Body.linearVelocity : Vector3.zero;
                vel.y = 0f;
                Vector3 ahead = Vector3.ClampMagnitude(vel * lookAhead, 14f);
                smoothedLookAhead = Vector3.Lerp(smoothedLookAhead, ahead, 1f - Mathf.Exp(-2f * Time.deltaTime));
                rotation = Quaternion.Euler(isoAngles);
                position = carPos + smoothedLookAhead - rotation * Vector3.forward * isoDistance;
            }
            else
            {
                Vector3 fwd = target.transform.forward;
                position = carPos - fwd * chaseDistance + Vector3.up * chaseHeight;
                rotation = Quaternion.LookRotation(carPos + fwd * 5f + Vector3.up * 1f - position);
            }
        }
    }
}
