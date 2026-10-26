using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;

namespace RacingGame.CameraRig
{
    public enum CameraMode { Close, Wide, Chase }

    /// <summary>
    /// Race camera, switched with C or the VIEW button:
    /// Close / Wide - fixed 3/4 isometric view (modern Rock n' Roll Racing): high, rotated 45 degrees,
    ///                never turning with the kart, looking ahead along its velocity, zooming out with speed;
    /// Chase        - close behind the kart, kart-racer style.
    /// </summary>
    public class RaceCamera : MonoBehaviour
    {
        public static CameraMode Mode = CameraMode.Close;
        public static readonly string[] ModeNames = { "CLOSE", "WIDE", "CHASE" };

        /// <summary>Fixed view angles of the isometric camera (also used to orient the minimap and stick steering).</summary>
        public const float IsoPitch = 35f, IsoYaw = 45f;

        [Header("Isometric")]
        public float isoFov = 25f;             // narrow lens: reads as isometric but keeps depth
        public float closeMeters = 34f;        // metres visible from top to bottom of the screen
        public float wideMeters = 46f;
        public float lookAhead = 0.32f;        // seconds of velocity to look ahead
        public float followSharpness = 5f;
        public float speedZoomOut = 0.2f;      // up to +20% view at top speed / boost

        [Header("Chase")]
        public float chaseDistance = 6.8f;
        public float chaseHeight = 2.7f;
        public float chaseFov = 62f;
        public float speedFovBonus = 8f;       // wider view at top speed
        public float boostFovBonus = 12f;      // extra "whoosh" while boosting
        public float chaseSmoothing = 6f;

        CarController target;
        Camera cam;
        float trauma;   // 0..1, decays over time; shake = trauma^2
        Vector3 focus;  // world point the isometric camera looks at
        float zoom = 1f;

        public static bool IsIsometric => Mode != CameraMode.Chase;

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
            if (target != null) focus = target.transform.position;
            zoom = 1f;
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
            ComputeDesired(out Vector3 pos, out Quaternion rot, out float fov, 1f);
            transform.SetPositionAndRotation(pos, rot);
            if (cam) cam.fieldOfView = fov;
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (InputBridge.WasPressedThisFrame(GameKey.Camera)) Toggle();

            float dt = Time.deltaTime;
            ComputeDesired(out Vector3 pos, out Quaternion rot, out float fov, dt);
            if (IsIsometric)
            {
                // The focus is already smoothed; the camera rigidly follows it at a fixed angle.
                transform.SetPositionAndRotation(pos, rot);
                if (cam) cam.fieldOfView = fov;
            }
            else
            {
                float k = 1f - Mathf.Exp(-chaseSmoothing * dt);
                transform.position = Vector3.Lerp(transform.position, pos, k);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, k);
                if (cam) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-4f * dt));
            }

            // Screen shake (smooth Perlin noise, scaled by trauma squared).
            if (trauma > 0f)
            {
                float s = trauma * trauma;
                float t = Time.time * 25f;
                float amp = IsIsometric ? 1.6f : 1.2f;
                transform.position += transform.right * ((Mathf.PerlinNoise(t, 0f) - 0.5f) * amp * s)
                                    + transform.up * ((Mathf.PerlinNoise(0f, t) - 0.5f) * amp * s);
                transform.rotation *= Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t, t) - 0.5f) * 4f * s);
                trauma = Mathf.Max(0f, trauma - dt * 1.5f);
            }
        }

        float Speed01 => target.maxSpeed > 0f ? Mathf.Clamp(Mathf.Abs(target.ForwardSpeed) / target.maxSpeed, 0f, 1.5f) : 0f;

        void ComputeDesired(out Vector3 position, out Quaternion rotation, out float fov, float dt)
        {
            Vector3 carPos = target.transform.position;
            if (IsIsometric)
            {
                // Look ahead along the velocity, eased.
                Vector3 vel = target.Body != null ? target.Body.linearVelocity : Vector3.zero;
                vel.y = 0f;
                Vector3 aim = carPos + Vector3.ClampMagnitude(vel * lookAhead, 14f);
                // Snap after a teleport (respawn) instead of sweeping across the track.
                bool jump = (aim - focus).sqrMagnitude > 40f * 40f;
                focus = dt >= 1f || jump ? aim : Vector3.Lerp(focus, aim, 1f - Mathf.Exp(-followSharpness * dt));

                // Zoom out a little with speed and boost.
                float z = 1f + Mathf.Min(speedZoomOut, Speed01 * speedZoomOut * 0.6f + (target.IsBoosting ? speedZoomOut * 0.4f : 0f));
                zoom = dt >= 1f ? z : Mathf.Lerp(zoom, z, 1f - Mathf.Exp(-2.5f * dt));

                float meters = Mode == CameraMode.Wide ? wideMeters : closeMeters;
                // Distance at which the lens shows `meters` across the screen height.
                float distance = meters * zoom / (2f * Mathf.Tan(isoFov * 0.5f * Mathf.Deg2Rad));
                rotation = Quaternion.Euler(IsoPitch, IsoYaw, 0f);
                position = focus - rotation * Vector3.forward * distance;
                fov = isoFov;
            }
            else
            {
                Vector3 fwd = target.transform.forward;
                position = carPos - fwd * chaseDistance + Vector3.up * chaseHeight;
                rotation = Quaternion.LookRotation(carPos + fwd * 5f + Vector3.up * 1f - position);
                fov = chaseFov + Mathf.Clamp01(Speed01) * speedFovBonus + (target.IsBoosting ? boostFovBonus : 0f);
            }
        }

        /// <summary>
        /// Turns a direction on the screen (x right, y up) into a flat world direction on the ground,
        /// as seen by the isometric camera. Used by stick steering ("point where you want to go").
        /// </summary>
        public static Vector3 ScreenToGroundDirection(Vector2 screenDir)
        {
            var rot = Quaternion.Euler(0f, IsoYaw, 0f);
            Vector3 right = rot * Vector3.right, forward = rot * Vector3.forward;
            // Ground depth is squashed by sin(pitch) on screen, so stretch the vertical part back.
            float depth = screenDir.y / Mathf.Sin(IsoPitch * Mathf.Deg2Rad);
            Vector3 d = right * screenDir.x + forward * depth;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;
        }
    }
}
