using System.Collections.Generic;
using UnityEngine;
using RacingGame.Car;
using RacingGame.Weapons;

namespace RacingGame.Garage
{
    public enum UpgradeType { Engine, Tires, Armor, Weapons, Nitro }

    /// <summary>Base stats of a vehicle you can buy.</summary>
    public class VehicleDef
    {
        public string Id;
        public string Name;
        public string Model;          // .glb in Resources/Models
        public Color Color;           // HUD / missile color
        public int Price;
        public string Description;

        public float TopSpeed;        // m/s
        public float Acceleration;
        public float TurnSpeed;       // deg/s
        public float Grip;
        public float Armor;
        public int Missiles, Mines, Boosts;
    }

    /// <summary>Final stats after upgrades, applied to the car at race start.</summary>
    public struct CarStats
    {
        public float TopSpeed, Acceleration, TurnSpeed, Grip, Armor;
        public int Missiles, Mines, Boosts;
        public float MissileDamage, BoostDuration;
    }

    /// <summary>All vehicles and upgrades in the game.</summary>
    public static class VehicleCatalog
    {
        public const int MaxUpgradeLevel = 4;

        public static readonly List<VehicleDef> Vehicles = new List<VehicleDef>
        {
            new VehicleDef
            {
                Id = "rumbler", Name = "RUMBLER", Model = "vehicle-truck-yellow", Color = new Color(1f, 0.8f, 0.15f),
                Price = 0, Description = "Trusty all-rounder. Every legend starts somewhere.",
                TopSpeed = 32f, Acceleration = 18f, TurnSpeed = 130f, Grip = 6f, Armor = 100f,
                Missiles = 3, Mines = 2, Boosts = 2,
            },
            new VehicleDef
            {
                Id = "bruiser", Name = "BRUISER", Model = "vehicle-truck-green", Color = new Color(0.3f, 0.85f, 0.4f),
                Price = 2500, Description = "Heavy armor and extra mines. Slow, but hard to wreck.",
                TopSpeed = 30f, Acceleration = 16f, TurnSpeed = 118f, Grip = 7f, Armor = 150f,
                Missiles = 3, Mines = 3, Boosts = 2,
            },
            new VehicleDef
            {
                Id = "viper", Name = "VIPER", Model = "vehicle-truck-red", Color = new Color(1f, 0.3f, 0.2f),
                Price = 4500, Description = "Fast and twitchy with an extra nitro. Thin armor.",
                TopSpeed = 36f, Acceleration = 21f, TurnSpeed = 140f, Grip = 5.5f, Armor = 85f,
                Missiles = 3, Mines = 2, Boosts = 3,
            },
            new VehicleDef
            {
                Id = "phantom", Name = "PHANTOM", Model = "vehicle-truck-purple", Color = new Color(0.65f, 0.4f, 1f),
                Price = 8000, Description = "Elite combat truck. Strong in every area, extra missile.",
                TopSpeed = 35f, Acceleration = 20f, TurnSpeed = 136f, Grip = 6.5f, Armor = 120f,
                Missiles = 4, Mines = 2, Boosts = 2,
            },
            new VehicleDef
            {
                Id = "hellcat", Name = "HELLCAT", Model = "vehicle-motorcycle", Color = new Color(0.3f, 0.8f, 1f),
                Price = 12000, Description = "Insane speed and handling, lots of nitro. One mistake and you're toast.",
                TopSpeed = 39f, Acceleration = 24f, TurnSpeed = 160f, Grip = 5f, Armor = 65f,
                Missiles = 2, Mines = 1, Boosts = 4,
            },
        };

        static readonly int[][] UpgradePrices =
        {
            new[] { 600, 1200, 2000, 3200 },   // Engine
            new[] { 500, 1000, 1700, 2600 },   // Tires
            new[] { 500, 1000, 1700, 2600 },   // Armor
            new[] { 700, 1400, 2200, 3300 },   // Weapons
            new[] { 600, 1200, 2000, 3000 },   // Nitro
        };

        public static readonly string[] UpgradeNames = { "ENGINE", "TIRES", "ARMOR", "WEAPONS", "NITRO" };
        public static readonly string[] UpgradeEffects =
        {
            "+5% top speed, +8% acceleration",
            "+7% steering, +10% grip",
            "+15% armor",
            "+1 missile, +6% damage (+1 mine at lv 2/4)",
            "+0.15s nitro (+1 nitro at lv 1/3)",
        };

        public static VehicleDef Get(string id)
        {
            foreach (var v in Vehicles) if (v.Id == id) return v;
            return Vehicles[0];
        }

        /// <summary>Price of the next level, or -1 when maxed.</summary>
        public static int UpgradePrice(UpgradeType type, int currentLevel) =>
            currentLevel >= MaxUpgradeLevel ? -1 : UpgradePrices[(int)type][currentLevel];

        public static CarStats ComputeStats(VehicleDef v, int engine, int tires, int armor, int weapons, int nitro)
        {
            return new CarStats
            {
                TopSpeed = v.TopSpeed * (1f + 0.05f * engine),
                Acceleration = v.Acceleration * (1f + 0.08f * engine),
                TurnSpeed = v.TurnSpeed * (1f + 0.07f * tires),
                Grip = v.Grip * (1f + 0.1f * tires),
                Armor = v.Armor * (1f + 0.15f * armor),
                Missiles = v.Missiles + weapons,
                Mines = v.Mines + weapons / 2,
                Boosts = v.Boosts + (nitro + 1) / 2,
                MissileDamage = 25f * (1f + 0.06f * weapons),
                BoostDuration = 1.6f + 0.15f * nitro,
            };
        }
    }

    /// <summary>Player money (saved).</summary>
    public static class Wallet
    {
        const string Key = "money";

        public static int Money
        {
            get => PlayerPrefs.GetInt(Key, 0);
            set { PlayerPrefs.SetInt(Key, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static bool TrySpend(int amount)
        {
            if (amount < 0 || Money < amount) return false;
            Money -= amount;
            return true;
        }
    }

    /// <summary>What the player owns: vehicles, upgrade levels, and the selected vehicle (saved).</summary>
    public static class PlayerGarage
    {
        public static VehicleDef Selected
        {
            get
            {
                var v = VehicleCatalog.Get(PlayerPrefs.GetString("garage_selected", VehicleCatalog.Vehicles[0].Id));
                return Owns(v) ? v : VehicleCatalog.Vehicles[0];
            }
        }

        public static bool Owns(VehicleDef v) => v.Price == 0 || PlayerPrefs.GetInt("garage_own_" + v.Id, 0) == 1;

        public static void Select(VehicleDef v)
        {
            if (!Owns(v)) return;
            PlayerPrefs.SetString("garage_selected", v.Id);
            PlayerPrefs.Save();
        }

        public static bool Buy(VehicleDef v)
        {
            if (Owns(v) || !Wallet.TrySpend(v.Price)) return false;
            PlayerPrefs.SetInt("garage_own_" + v.Id, 1);
            Select(v);
            return true;
        }

        public static int Level(VehicleDef v, UpgradeType t) => PlayerPrefs.GetInt($"garage_upg_{v.Id}_{t}", 0);

        public static bool Upgrade(VehicleDef v, UpgradeType t)
        {
            if (!Owns(v)) return false;
            int price = VehicleCatalog.UpgradePrice(t, Level(v, t));
            if (price < 0 || !Wallet.TrySpend(price)) return false;
            PlayerPrefs.SetInt($"garage_upg_{v.Id}_{t}", Level(v, t) + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static CarStats Stats(VehicleDef v) => VehicleCatalog.ComputeStats(v,
            Level(v, UpgradeType.Engine), Level(v, UpgradeType.Tires), Level(v, UpgradeType.Armor),
            Level(v, UpgradeType.Weapons), Level(v, UpgradeType.Nitro));

        /// <summary>Applies vehicle + upgrades to a freshly built car.</summary>
        public static void ApplyTo(CarController car, CarStats s)
        {
            car.maxSpeed = s.TopSpeed;
            car.acceleration = s.Acceleration;
            car.turnSpeed = s.TurnSpeed;
            car.grip = s.Grip;

            var health = car.GetComponent<CarHealth>();
            if (health) health.SetMaxArmor(s.Armor);

            var weapons = car.GetComponent<WeaponSystem>();
            if (weapons)
            {
                weapons.missilesPerLap = s.Missiles;
                weapons.minesPerLap = s.Mines;
                weapons.boostsPerLap = s.Boosts;
                weapons.missileDamage = s.MissileDamage;
                weapons.boostDuration = s.BoostDuration;
                weapons.Refill();
            }
        }
    }
}
