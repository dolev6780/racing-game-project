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
        [Tooltip("Set by the race manager to keep races close (AI only).")]
        public float catchUpMultiplier = 1f;

        [Header("Handling")]
        public float turnSpeed = 130f;        // deg/s at full lock
        [Tooltip("Higher = less sliding.")]
        public float grip = 6f;

        [Header("Boost")]
        public float boostMultiplier = 1.5f;

        [Header("Drift button")]
        [Tooltip("Grip multiplier while the drift button is held at speed.")]
        public float driftButtonGrip = 0.3f;

        /// <summary>Coins carried: each adds 1% top speed (up to MaxCoins).</summary>
        public const int MaxCoins = 10;
        public int Coins { get; private set; }
        /// <summary>Set by RacerProgress when the car is off the road (top speed 60%).</summary>
        public bool OffRoad { get; set; }
        public const float OffRoadSpeedFactor = 0.6f;
        /// <summary>Grip used in the last physics step (lower while drifting with the button).</summary>
        public float CurrentGrip { get; private set; }
        public bool IsSpinning => Time.time < spinUntil;
        public bool IsAirborne => airTime > 0.12f;
        /// <summary>Longest time in the air during the last jump (seconds).</summary>
        public float LastAirTime { get; private set; }
        /// <summary>Raised when a ramp trick is started / landed (landing gives a boost).</summary>
        public event System.Action TrickStarted, TrickLanded;
        /// <summary>Raised when an oil slick spins the car out.</summary>
        public event System.Action SpunOut;

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
        float spinUntil;
        float airTime;
        bool trickRequested, trickActive;
        float trickSpin;
        Transform visualBody;
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
            CurrentGrip = grip;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void SetVisuals(Transform[] frontSteerPivots, Transform[] wheels, GameObject flame)
        {
            steerPivots = frontSteerPivots;
            wheelMeshes = wheels;
            visualBody = transform.Find("Body");
            boostFlame = flame;
            if (boostFlame) boostFlame.SetActive(false);
        }

        void Update()
        {
            bool paused = Time.timeScale <= 0f;
            CurrentInput = InputActive && !paused && InputSource != null ? InputSource.ReadInput() : default;
            // Latched here so a tap between physics steps isn't lost.
            if (CurrentInput.Trick && !IsGrounded && !trickActive) trickRequested = true;
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
            if (!IsGrounded)
            {
                // Airborne (off a ramp): a trick now pays a boost on landing.
                airTime += dt;
                LastAirTime = Mathf.Max(LastAirTime, airTime);
                if (trickRequested && !trickActive && airTime > 0.08f)
                {
                    trickActive = true;
                    trickSpin = 0f;
                    TrickStarted?.Invoke();
                }
                trickRequested = false;
                return;
            }
            if (airTime > 0f)
            {
                if (trickActive)
                {
                    StartBoost(0.9f);
                    TrickLanded?.Invoke();
                }
                if (airTime > 0.12f) LastAirTime = airTime;
                airTime = 0f;
                trickActive = false;
                trickRequested = false;
            }

            if (IsSpinning)
            {
                // Spun out on oil: no control, the kart twirls and scrubs off speed.
                Vector3 flat = Body.linearVelocity;
                Body.AddForce(-new Vector3(flat.x, 0f, flat.z) * Mathf.Clamp01(1.6f * dt), ForceMode.VelocityChange);
                Vector3 spin = Body.angularVelocity;
                spin.y = 10f;
                Body.angularVelocity = spin;
                IsDrifting = false;
                DriftTier = 0;
                driftTime = 0f;
                return;
            }

            var input = CurrentInput;
            float throttle = input.Throttle;
            float boost = IsBoosting ? boostMultiplier : 1f;
            if (IsBoosting) throttle = 1f;
            float topSpeed = maxSpeed * speedMultiplier * catchUpMultiplier * boost
                             * (1f + Coins * 0.01f) * (OffRoad ? OffRoadSpeedFactor : 1f);

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
            // Holding the drift button at speed loosens the grip so the kart slides into a drift.
            bool driftHeld = input.Drift && fwdSpeed > 12f;
            CurrentGrip = driftHeld ? grip * driftButtonGrip : grip;
            Body.AddForce(-right * lateral * Mathf.Clamp01(CurrentGrip * dt), ForceMode.VelocityChange);

            UpdateDrift(fwdSpeed, lateral, input.Steer, driftHeld, dt);

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
        void UpdateDrift(float fwdSpeed, float lateral, float steer, bool driftHeld, float dt)
        {
            bool sliding = fwdSpeed > 12f && Mathf.Abs(steer) > 0.6f && Mathf.Abs(lateral) > 4f;
            // With the drift button, a gentler steer and slide is enough.
            if (driftHeld && Mathf.Abs(steer) > 0.3f && Mathf.Abs(lateral) > 2f) sliding = true;
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

        public void AddCoin() => Coins = Mathf.Min(MaxCoins, Coins + 1);
        public void LoseCoins(int count) => Coins = Mathf.Max(0, Coins - count);
        public void HalveCoins() => Coins /= 2;

        /// <summary>Oil slick: lose control and twirl for a moment.</summary>
        public void SpinOut(float duration)
        {
            spinUntil = Mathf.Max(spinUntil, Time.time + duration);
            SpunOut?.Invoke();
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
            spinUntil = 0f;
            airTime = 0f;
            trickActive = trickRequested = false;
        }

        void UpdateVisuals()
        {
            // Ramp trick: one barrel roll of the kart body while in the air.
            if (visualBody)
            {
                if (trickActive) trickSpin = Mathf.Min(360f, trickSpin + 900f * Time.deltaTime);
                else trickSpin = 0f;
                visualBody.localRotation = Quaternion.Euler(0f, 0f, trickSpin);
            }

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
