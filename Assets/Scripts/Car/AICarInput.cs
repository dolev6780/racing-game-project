using UnityEngine;
using RacingGame.Track;
using RacingGame.Weapons;

namespace RacingGame.Car
{
    /// <summary>
    /// How a computer driver races: Aggressive hunts the kart ahead and fires a lot, Defensive guards its line
    /// with mines and shields early, Collector weaves to grab coins and item boxes.
    /// </summary>
    public enum AiPersonality { Balanced, Aggressive, Defensive, Collector }

    /// <summary>
    /// Computer driver: follows the racing line, slows for corners, fires at cars ahead,
    /// drops mines on chasers, uses nitro on straights and special items, does ramp tricks,
    /// and recovers when stuck. Its personality shapes its line and weapon use.
    /// </summary>
    public class AICarInput : MonoBehaviour, ICarInputSource
    {
        [Range(0f, 1f)] public float skill = 0.7f;
        [Range(0f, 1f)] public float aggression = 0.6f;
        public AiPersonality personality = AiPersonality.Balanced;

        CarController controller;
        RacerProgress progress;
        WeaponSystem weapons;

        float laneSeed;
        float stuckTimer;
        float reverseUntil;
        int recoverAttempts;
        float nextFireTime, nextMineTime, nextBoostTime, nextItemCheck;
        bool trickTried;

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
            if (personality == AiPersonality.Defensive) lane *= 0.3f;   // holds the middle of the road
            lane = PersonalityLane(path, idx, lane);
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

            // --- Ramp trick: most of the time, a trick in the air ---
            if (!controller.IsGrounded)
            {
                if (!trickTried && controller.IsAirborne)
                {
                    trickTried = true;
                    s.Trick = Random.value < Mathf.Lerp(0.4f, 0.9f, skill);
                }
            }
            else trickTried = false;

            // --- Weapons ---
            if (weapons != null)
            {
                float t = Time.time;
                float fireGap = personality == AiPersonality.Aggressive ? 0.6f : 1f;
                if (t > nextFireTime && FindCar(40f, 0f, 12f))
                {
                    s.Fire = true;
                    nextFireTime = t + (Mathf.Lerp(3f, 0.8f, aggression) + Random.value) * fireGap;
                }
                float mineGap = personality == AiPersonality.Defensive ? 0.5f : 1f;
                if (t > nextMineTime && FindCar(25f, 150f, 180f))
                {
                    s.Mine = true;
                    nextMineTime = t + (Mathf.Lerp(6f, 2f, aggression) + Random.value * 2f) * mineGap;
                }
                if (weapons.Special != SpecialItem.None && t > nextItemCheck)
                {
                    nextItemCheck = t + 0.25f;
                    s.Item = ShouldUseSpecial(weapons.Special);
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

        /// <summary>
        /// Lane offset (metres from the centre line) for the personality: Aggressive lines up behind the kart
        /// just ahead, Collector heads for the next coin or item box; others keep the wandering lane.
        /// </summary>
        float PersonalityLane(TrackPath path, int idx, float lane)
        {
            Vector3 here = transform.position;
            Vector3 right = path.Right(idx), fwd = path.Direction(idx);
            float limit = path.RoadWidth * 0.38f;
            if (personality == AiPersonality.Aggressive)
            {
                foreach (var other in CarController.All)
                {
                    if (other == controller || other.Disabled) continue;
                    Vector3 d = other.transform.position - here;
                    float ahead = Vector3.Dot(d, fwd);
                    if (ahead < 4f || ahead > 25f) continue;
                    float side = Vector3.Dot(other.transform.position - path.Point(idx), right);
                    return Mathf.Clamp(side, -limit, limit);
                }
            }
            else if (personality == AiPersonality.Collector)
            {
                float best = float.MaxValue;
                float? pick = null;
                foreach (var coin in Coin.All)
                    Consider(coin.Available, coin.transform.position);
                foreach (var box in Pickup.All)
                    Consider(box.Available, box.transform.position);
                if (pick.HasValue) return Mathf.Clamp(pick.Value, -limit, limit);

                void Consider(bool available, Vector3 p)
                {
                    if (!available) return;
                    Vector3 d = p - here;
                    float ahead = Vector3.Dot(d, fwd);
                    if (ahead < 5f || ahead > 35f || ahead >= best) return;
                    best = ahead;
                    pick = Vector3.Dot(p - path.Point(idx), right);
                }
            }
            return lane;
        }

        bool ShouldUseSpecial(SpecialItem item)
        {
            var health = controller.GetComponent<CarHealth>();
            switch (item)
            {
                case SpecialItem.OilSlick:
                    return FindCar(20f, 150f, 180f) || Random.value < 0.02f;   // someone right behind
                case SpecialItem.Shield:
                    bool hurt = health != null && health.Armor01 < (personality == AiPersonality.Defensive ? 0.8f : 0.5f);
                    return hurt || FindCar(30f, 120f, 180f) || Random.value < 0.01f;
                case SpecialItem.Shockwave:
                    return CountCars(WeaponSystem.ShockwaveRadius - 1f) >= 1;
                default:
                    return false;
            }
        }

        int CountCars(float range)
        {
            int n = 0;
            foreach (var other in CarController.All)
            {
                if (other == controller || other.Disabled) continue;
                if ((other.transform.position - transform.position).sqrMagnitude <= range * range) n++;
            }
            return n;
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
