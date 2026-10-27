using UnityEngine;
using RacingGame.Car;
using RacingGame.Progress;

namespace RacingGame.Weapons
{
    /// <summary>Special items from item boxes; one can be held at a time and used with the ITEM button.</summary>
    public enum SpecialItem { None, OilSlick, Shield, Shockwave }

    /// <summary>
    /// Rock n' Roll Racing style ammo: a fixed number of missiles, mines and nitros
    /// that refill every lap. Upgrade these numbers in the future garage/shop.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class WeaponSystem : MonoBehaviour
    {
        public int missilesPerLap = 3;
        public int minesPerLap = 2;
        public int boostsPerLap = 2;
        public float missileDamage = 25f;
        public float mineDamage = 35f;
        public float boostDuration = 1.6f;
        public float fireCooldown = 0.35f;

        public int Missiles { get; private set; }
        public int Mines { get; private set; }
        public int Boosts { get; private set; }
        public SpecialItem Special { get; private set; }

        public const float ShieldSeconds = 6f;
        public const float ShockwaveRadius = 8f, ShockwaveDamage = 30f;

        /// <summary>Short label for HUD buttons.</summary>
        public static string ItemLabel(SpecialItem item) =>
            item == SpecialItem.OilSlick ? "OIL" : item == SpecialItem.Shield ? "SHIELD" : item == SpecialItem.Shockwave ? "SHOCK" : "ITEM";

        CarController controller;
        CarHealth health;
        float nextFireTime;

        void Awake()
        {
            controller = GetComponent<CarController>();
            health = GetComponent<CarHealth>();
            Refill();
        }

        public void Refill()
        {
            Missiles = missilesPerLap;
            Mines = minesPerLap;
            Boosts = boostsPerLap;
        }

        void Update()
        {
            if (!controller.InputActive) return;
            var input = controller.CurrentInput;
            if (input.Fire) FireMissile();
            if (input.Mine) DropMine();
            if (input.Boost) UseBoost();
            if (input.Item) UseSpecial();
        }

        /// <summary>Puts an item in the slot; false if one is already held.</summary>
        public bool GiveSpecial(SpecialItem item)
        {
            if (Special != SpecialItem.None || item == SpecialItem.None) return false;
            Special = item;
            return true;
        }

        public void UseSpecial()
        {
            var item = Special;
            if (item == SpecialItem.None) return;
            Special = SpecialItem.None;
            switch (item)
            {
                case SpecialItem.OilSlick:
                    OilSlick.Spawn(transform.position - transform.forward * 3f + Vector3.up * 0.05f, health);
                    break;
                case SpecialItem.Shield:
                    health.ActivateShield(ShieldSeconds);
                    break;
                case SpecialItem.Shockwave:
                    Shockwave.Fire(transform.position, ShockwaveRadius, ShockwaveDamage, health);
                    break;
            }
        }

        /// <summary>Extra ammo from item boxes (can go a little above the per-lap amount).</summary>
        public void AddAmmo(int missiles, int mines, int boosts)
        {
            Missiles = Mathf.Min(Missiles + missiles, missilesPerLap + 2);
            Mines = Mathf.Min(Mines + mines, minesPerLap + 2);
            Boosts = Mathf.Min(Boosts + boosts, boostsPerLap + 2);
        }

        public void FireMissile()
        {
            if (Missiles <= 0 || Time.time < nextFireTime) return;
            Missiles--;
            nextFireTime = Time.time + fireCooldown;
            if (controller.IsPlayer) Stats.Add(Stat.MissilesFired);

            Vector3 forward = transform.forward;
            Vector3 dir = forward;

            // Light aim-assist: lock onto the closest car in a cone in front.
            float bestDist = 70f;
            foreach (var other in CarController.All)
            {
                if (other == controller || other.Disabled) continue;
                Vector3 to = other.transform.position - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist < bestDist && Vector3.Angle(forward, to) < 20f)
                {
                    bestDist = dist;
                    dir = to.normalized;
                }
            }

            Vector3 spawn = transform.position + forward * 2.6f + Vector3.up * 0.7f;
            float speed = Mathf.Max(controller.ForwardSpeed, 0f) + 45f;
            Missile.Spawn(spawn, dir, speed, missileDamage, health, controller.PaintColor);
        }

        public void DropMine()
        {
            if (Mines <= 0) return;
            Mines--;
            if (controller.IsPlayer) Stats.Add(Stat.MinesDropped);
            Mine.Spawn(transform.position - transform.forward * 2.8f + Vector3.up * 0.15f, mineDamage, health);
        }

        public void UseBoost()
        {
            if (Boosts <= 0 || controller.IsBoosting) return;
            Boosts--;
            if (controller.IsPlayer) Stats.Add(Stat.NitrosUsed);
            controller.StartBoost(boostDuration);
        }
    }
}
