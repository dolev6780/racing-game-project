using UnityEngine;
using RacingGame.Track;
using RacingGame.Weapons;

namespace RacingGame.Car
{
    /// <summary>
    /// Computer driver: follows the racing line, slows for corners, fires at cars ahead,
    /// drops mines on chasers, uses nitro on straights and recovers when stuck.
    /// </summary>
    public class AICarInput : MonoBehaviour, ICarInputSource
    {
        [Range(0f, 1f)] public float skill = 0.7f;
        [Range(0f, 1f)] public float aggression = 0.6f;

        CarController controller;
        RacerProgress progress;
        WeaponSystem weapons;

        float laneSeed;
        float stuckTimer;
        float reverseUntil;
        int recoverAttempts;
        float nextFireTime, nextMineTime, nextBoostTime;

        void Awake()
        {
            controller = GetComponent<CarController>();
            progress = GetComponent<RacerProgress>();
            weapons = GetComponent<WeaponSystem>();
            laneSeed = Random.value * 100f;
        }

        public CarInputState ReadInput()
        {
            var s = new CarInputState();
            TrackPath path = progress ? progress.Path : null;
            if (path == null) return s;

            float speed = controller.ForwardSpeed;
            int idx = progress.CurrentIndex;

            // --- Steering towards a look-ahead point with a slowly wandering lane offset ---
            int look = 3 + Mathf.RoundToInt(Mathf.Abs(speed) * 0.2f);
            float lane = (Mathf.PerlinNoise(laneSeed, Time.time * 0.15f) - 0.5f) * path.RoadWidth * 0.5f;
            Vector3 target = path.Point(idx + look) + path.Right(idx + look) * lane;
            Vector3 to = target - transform.position;
            to.y = 0f;
            float angle = Vector3.SignedAngle(transform.forward, to, Vector3.up);
            s.Steer = Mathf.Clamp(angle / 25f, -1f, 1f);

            // --- Speed: ease off before sharp corners ---
            float corner = Vector3.Angle(path.Direction(idx + look), path.Direction(idx + look + 10));
            float cornerSpeed = Mathf.Lerp(controller.maxSpeed, controller.maxSpeed * 0.45f, Mathf.InverseLerp(20f, 80f, corner));
            cornerSpeed *= Mathf.Lerp(0.9f, 1.05f, skill);
            s.Throttle = speed > cornerSpeed ? -0.4f : 1f;
            if (Mathf.Abs(angle) > 70f && speed > 10f) s.Throttle = -0.6f;

            // --- Stuck recovery ---
            if (Time.time < reverseUntil)
            {
                s.Throttle = -1f;
                s.Steer = -s.Steer;
            }
            else
            {
                stuckTimer = Mathf.Abs(speed) < 1.5f ? stuckTimer + Time.deltaTime : 0f;
                if (stuckTimer > 1.5f)
                {
                    stuckTimer = 0f;
                    recoverAttempts++;
                    if (recoverAttempts > 2) { progress.Respawn(); recoverAttempts = 0; }
                    else reverseUntil = Time.time + 1.2f;
                }
                else if (Mathf.Abs(speed) > 5f) recoverAttempts = 0;
            }

            // --- Weapons ---
            if (weapons != null)
            {
                float t = Time.time;
                if (t > nextFireTime && FindCar(40f, 0f, 12f))
                {
                    s.Fire = true;
                    nextFireTime = t + Mathf.Lerp(3f, 0.8f, aggression) + Random.value;
                }
                if (t > nextMineTime && FindCar(25f, 150f, 180f))
                {
                    s.Mine = true;
                    nextMineTime = t + Mathf.Lerp(6f, 2f, aggression) + Random.value * 2f;
                }
                if (t > nextBoostTime && corner < 8f && speed > 15f
                    && Vector3.Angle(path.Direction(idx), path.Direction(idx + 20)) < 10f)
                {
                    s.Boost = true;
                    nextBoostTime = t + Random.Range(4f, 10f);
                }
            }
            return s;
        }

        /// <summary>Is there another live car within range whose bearing is between minAngle and maxAngle?</summary>
        bool FindCar(float range, float minAngle, float maxAngle)
        {
            foreach (var other in CarController.All)
            {
                if (other == controller || other.Disabled) continue;
                Vector3 d = other.transform.position - transform.position;
                if (d.sqrMagnitude > range * range) continue;
                float a = Vector3.Angle(transform.forward, d);
                if (a >= minAngle && a <= maxAngle) return true;
            }
            return false;
        }
    }
}
