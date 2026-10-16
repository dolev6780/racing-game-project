using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Car
{
    /// <summary>
    /// Arcade car physics: tight steering, adjustable grip (drift), boost.
    /// Pitch/roll are frozen so the car never flips - perfect for combat racing.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        public static readonly List<CarController> All = new List<CarController>();

        [Header("Engine")]
        public float maxSpeed = 32f;          // m/s (~115 km/h)
        public float acceleration = 18f;
        public float reverseSpeed = 12f;
        public float brakeForce = 40f;
        public float coastDeceleration = 5f;
        [Tooltip("Upgrades / AI skill multiply top speed.")]
        public float speedMultiplier = 1f;

        [Header("Handling")]
        public float turnSpeed = 130f;        // deg/s at full lock
        [Tooltip("Higher = less sliding.")]
        public float grip = 6f;

        [Header("Boost")]
        public float boostMultiplier = 1.5f;

        public string DisplayName { get; set; }
        public Color PaintColor { get; set; }
        public bool IsPlayer { get; set; }
        public int MoneyCollected { get; set; }

        /// <summary>Set by the race manager (countdown / finish).</summary>
        public bool InputEnabled { get; set; }
        /// <summary>Set while the car is wrecked and waiting to respawn.</summary>
        public bool Disabled { get; set; }
        public bool InputActive => InputEnabled && !Disabled;

        public ICarInputSource InputSource { get; set; }
        public CarInputState CurrentInput { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float LateralSpeed { get; private set; }
        public float SpeedKmh => Mathf.Abs(ForwardSpeed) * 3.6f;
        public bool IsBoosting => boostTimer > 0f;

        /// <summary>Drift charge: 0 = none, 1 = blue, 2 = orange, 3 = purple sparks.</summary>
        public int DriftTier { get; private set; }
        public bool IsDrifting { get; private set; }
        /// <summary>Raised when a drift is released with a mini-turbo (argument = tier 1..3).</summary>
        public event System.Action<int> MiniTurbo;

        static readonly float[] DriftTierTimes = { 0.6f, 1.4f, 2.4f };
        static readonly float[] MiniTurboTimes = { 0.4f, 0.7f, 1.0f };
        float driftTime, driftGrace;
        public bool IsGrounded { get; private set; }
        public Rigidbody Body { get; private set; }

        float boostTimer;
        Transform[] steerPivots = new Transform[0];
        Transform[] wheelMeshes = new Transform[0];
        GameObject boostFlame;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = 1000f;
            Body.linearDamping = 0.05f;
            Body.angularDamping = 3f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            Body.centerOfMass = new Vector3(0f, 0.3f, 0f);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void SetVisuals(Transform[] frontSteerPivots, Transform[] wheels, GameObject flame)
        {
            steerPivots = frontSteerPivots;
            wheelMeshes = wheels;
            boostFlame = flame;
            if (boostFlame) boostFlame.SetActive(false);
        }

        void Update()
        {
            bool paused = Time.timeScale <= 0f;
            CurrentInput = InputActive && !paused && InputSource != null ? InputSource.ReadInput() : default;
            UpdateVisuals();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (boostTimer > 0f) boostTimer -= dt;

            if (Body.isKinematic) return;

            IsGrounded = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 0.85f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            Vector3 velocity = Body.linearVelocity;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            float fwdSpeed = Vector3.Dot(velocity, forward);
            ForwardSpeed = fwdSpeed;
            LateralSpeed = Vector3.Dot(velocity, right);

            // Extra gravity keeps the car planted.
            Body.AddForce(Vector3.down * 12f, ForceMode.Acceleration);
            if (!IsGrounded) return;

            var input = CurrentInput;
            float throttle = input.Throttle;
            float boost = IsBoosting ? boostMultiplier : 1f;
            if (IsBoosting) throttle = 1f;
            float topSpeed = maxSpeed * speedMultiplier * boost;

            // --- Longitudinal ---
            float deltaV = 0f;
            if (throttle > 0.01f)
            {
                if (fwdSpeed < -0.5f) deltaV = brakeForce * throttle * dt;
                else if (fwdSpeed < topSpeed) deltaV = Mathf.Min(acceleration * boost * throttle * dt, topSpeed - fwdSpeed);
                else deltaV = -Mathf.Min(coastDeceleration * 2f * dt, fwdSpeed - topSpeed);
            }
            else if (throttle < -0.01f)
            {
                if (fwdSpeed > 0.5f) deltaV = brakeForce * throttle * dt;
                else if (fwdSpeed > -reverseSpeed) deltaV = acceleration * 0.6f * throttle * dt;
            }
            else
            {
                deltaV = -Mathf.Sign(fwdSpeed) * Mathf.Min(coastDeceleration * dt, Mathf.Abs(fwdSpeed));
            }
            Body.AddForce(forward * deltaV, ForceMode.VelocityChange);

            // --- Lateral grip (kills sideways sliding; lower grip = more drift) ---
            float lateral = Vector3.Dot(velocity, right);
            Body.AddForce(-right * lateral * Mathf.Clamp01(grip * dt), ForceMode.VelocityChange);

            UpdateDrift(fwdSpeed, lateral, input.Steer, dt);

            // --- Steering ---
            float speedFactor = Mathf.Clamp01(Mathf.Abs(fwdSpeed) / 8f);
            float direction = fwdSpeed >= -0.1f ? 1f : -1f;
            float yawRate = input.Steer * turnSpeed * speedFactor * direction * Mathf.Deg2Rad;
            Vector3 av = Body.angularVelocity;
            av.y = yawRate;
            Body.angularVelocity = av;
        }

        /// <summary>
        /// Kart-style drifting: sliding hard through a corner charges sparks (blue, orange, purple);
        /// releasing the drift gives a mini-turbo whose length depends on the charge.
        /// </summary>
        void UpdateDrift(float fwdSpeed, float lateral, float steer, float dt)
        {
            bool sliding = fwdSpeed > 12f && Mathf.Abs(steer) > 0.6f && Mathf.Abs(lateral) > 4f;
            if (sliding)
            {
                driftGrace = 0.15f;
                driftTime += dt;
            }
            else if (driftGrace > 0f)
            {
                driftGrace -= dt;   // brief tolerance so a wobble doesn't cancel the drift
            }
            else if (driftTime > 0f)
            {
                if (DriftTier > 0)
                {
                    StartBoost(MiniTurboTimes[DriftTier - 1]);
                    MiniTurbo?.Invoke(DriftTier);
                }
                driftTime = 0f;
            }

            IsDrifting = driftTime > 0f;
            int tier = 0;
            for (int i = 0; i < DriftTierTimes.Length; i++)
                if (driftTime >= DriftTierTimes[i]) tier = i + 1;
            DriftTier = tier;
        }

        public void StartBoost(float duration)
        {
            boostTimer = Mathf.Max(boostTimer, duration);
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Body.position = position;
            Body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            boostTimer = 0f;
            driftTime = 0f;
            DriftTier = 0;
            IsDrifting = false;
        }

        void UpdateVisuals()
        {
            float steerAngle = CurrentInput.Steer * 28f;
            foreach (var pivot in steerPivots)
                if (pivot) pivot.localRotation = Quaternion.Euler(0f, steerAngle, 0f);

            // Wheels spin around their local X axis (the axle).
            float spin = ForwardSpeed / 0.39f * Mathf.Rad2Deg * Time.deltaTime;
            foreach (var wheel in wheelMeshes)
                if (wheel) wheel.Rotate(Vector3.right, spin, Space.Self);

            if (boostFlame)
            {
                boostFlame.SetActive(IsBoosting);
                if (IsBoosting)
                    boostFlame.transform.localScale = new Vector3(0.5f, 0.5f, 1.2f + Mathf.PingPong(Time.time * 8f, 0.6f));
            }
        }
    }
}
