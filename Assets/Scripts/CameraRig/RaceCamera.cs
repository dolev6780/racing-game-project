using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;

namespace RacingGame.CameraRig
{
    public enum CameraMode { TopDown, Chase, Isometric }

    /// <summary>
    /// Three views, switched with C or the CAM button:
    /// TopDown   - high above the car looking down, turning with the car (left on screen = your left),
    /// Chase     - close behind the kart, kart-racer style,
    /// Isometric - classic fixed-angle view (Rock n' Roll Racing style).
    /// </summary>
    public class RaceCamera : MonoBehaviour
    {
        public static CameraMode Mode = CameraMode.TopDown;
        public static readonly string[] ModeNames = { "TOP DOWN", "CHASE", "ISOMETRIC" };

        [Header("Top down")]
        public float topHeight = 34f;
        public float topPitch = 72f;           // 90 = straight down; a little less shows what's ahead
        public float topLookAhead = 9f;        // keep the car in the lower part of the screen
        public float topFov = 50f;
        public float topYawSmoothing = 3f;

        [Header("Isometric")]
        public Vector3 isoAngles = new Vector3(50f, 45f, 0f);
        public float isoDistance = 60f;
        public float isoFov = 32f;
        public float lookAhead = 0.4f;

        [Header("Chase")]
        public float chaseDistance = 6.8f;
        public float chaseHeight = 2.7f;
        public float chaseFov = 62f;
        public float speedFovBonus = 8f;       // wider view at top speed
        public float boostFovBonus = 12f;      // extra "whoosh" while boosting

        public float smoothing = 6f;

        CarController target;
        Camera cam;
        float trauma;   // 0..1, decays over time; shake = trauma^2
        Vector3 smoothedLookAhead;
        float smoothedYaw;

        void Awake() => cam = GetComponent<Camera>();

        void OnEnable()
        {
            CarHealth.Damaged += OnDamaged;
            Explosion.Exploded += OnExploded;
        }

        void OnDisable()
        {
            CarHealth.Damaged -= OnDamaged;
            Explosion.Exploded -= OnExploded;
        }

        /// <summary>Adds screen shake (0..1).</summary>
        public void Shake(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        void OnDamaged(CarHealth victim, float amount)
        {
            if (target != null && victim.Controller == target) Shake(Mathf.Clamp(amount / 40f, 0.15f, 0.7f));
        }

        void OnExploded(Vector3 position, float size)
        {
            if (target == null) return;
            float dist = Vector3.Distance(position, target.transform.position);
            if (dist < 30f) Shake(size * 0.06f * (1f - dist / 30f));
        }

        public void SetTarget(CarController car)
        {
            target = car;
            if (target != null) smoothedYaw = target.transform.eulerAngles.y;
            Snap();
        }

        /// <summary>Next camera mode (saved for the next race).</summary>
        public static void Toggle()
        {
            Mode = (CameraMode)(((int)Mode + 1) % 3);
            RacingGame.Race.RaceConfig.Camera = Mode;
        }

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

            // Screen shake (smooth Perlin noise, scaled by trauma squared).
            if (trauma > 0f)
            {
                float s = trauma * trauma;
                float t = Time.time * 25f;
                transform.position += transform.right * ((Mathf.PerlinNoise(t, 0f) - 0.5f) * 1.2f * s)
                                    + transform.up * ((Mathf.PerlinNoise(0f, t) - 0.5f) * 1.2f * s);
                transform.rotation *= Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t, t) - 0.5f) * 6f * s);
                trauma = Mathf.Max(0f, trauma - Time.deltaTime * 1.5f);
            }

            if (cam) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, DesiredFov(), 1f - Mathf.Exp(-4f * Time.deltaTime));
        }

        float DesiredFov()
        {
            float speed01 = target.maxSpeed > 0f ? Mathf.Clamp01(Mathf.Abs(target.ForwardSpeed) / target.maxSpeed) : 0f;
            switch (Mode)
            {
                case CameraMode.Chase:
                    return chaseFov + speed01 * speedFovBonus + (target.IsBoosting ? boostFovBonus : 0f);
                case CameraMode.TopDown:
                    return topFov + speed01 * 4f + (target.IsBoosting ? 6f : 0f);
                default:
                    return isoFov;
            }
        }

        void ComputeDesired(out Vector3 position, out Quaternion rotation)
        {
            Vector3 carPos = target.transform.position;
            switch (Mode)
            {
                case CameraMode.TopDown:
                {
                    // Follow the car's heading smoothly so the view doesn't jitter on small steering.
                    float yaw = target.transform.eulerAngles.y;
                    smoothedYaw = Mathf.LerpAngle(smoothedYaw, yaw, 1f - Mathf.Exp(-topYawSmoothing * Time.deltaTime));
                    rotation = Quaternion.Euler(topPitch, smoothedYaw, 0f);
                    Vector3 flatForward = Quaternion.Euler(0f, smoothedYaw, 0f) * Vector3.forward;
                    Vector3 focus = carPos + flatForward * topLookAhead;
                    float distance = topHeight / Mathf.Sin(topPitch * Mathf.Deg2Rad);
                    position = focus - rotation * Vector3.forward * distance;
                    break;
                }
                case CameraMode.Isometric:
                {
                    Vector3 vel = target.Body != null ? target.Body.linearVelocity : Vector3.zero;
                    vel.y = 0f;
                    Vector3 ahead = Vector3.ClampMagnitude(vel * lookAhead, 14f);
                    smoothedLookAhead = Vector3.Lerp(smoothedLookAhead, ahead, 1f - Mathf.Exp(-2f * Time.deltaTime));
                    rotation = Quaternion.Euler(isoAngles);
                    position = carPos + smoothedLookAhead - rotation * Vector3.forward * isoDistance;
                    break;
                }
                default:
                {
                    Vector3 fwd = target.transform.forward;
                    position = carPos - fwd * chaseDistance + Vector3.up * chaseHeight;
                    rotation = Quaternion.LookRotation(carPos + fwd * 5f + Vector3.up * 1f - position);
                    break;
                }
            }
        }
    }
}
