using System;
using System.Collections.Generic;
using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;

namespace RacingGame.Track
{
    /// <summary>
    /// Spinning gold coin on the racing line: +1% top speed (up to CarController.MaxCoins) and +$10.
    /// Respawns a while after being collected.
    /// </summary>
    public class Coin : MonoBehaviour
    {
        public const float Radius = 1.9f;
        public const float RespawnTime = 10f;
        public const int MoneyValue = 10;

        public static readonly List<Coin> All = new List<Coin>();
        /// <summary>Raised when a car picks up a coin.</summary>
        public static event Action<CarController> Collected;

        GameObject visual;
        float hiddenUntil, bobOffset;

        public bool Available => Time.time >= hiddenUntil;

        public static Coin Create(Transform parent, Vector3 position)
        {
            var go = new GameObject("Coin");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var holder = new GameObject("CoinVisual");
            holder.transform.SetParent(go.transform, false);
            var upright = Quaternion.Euler(90f, 0f, 0f);
            MaterialFactory.Visual(PrimitiveType.Cylinder, holder.transform, Vector3.zero, new Vector3(1.1f, 0.07f, 1.1f),
                new Color(1f, 0.76f, 0.12f), upright, ToonStyle.PropOutline);
            MaterialFactory.Visual(PrimitiveType.Cylinder, holder.transform, Vector3.zero, new Vector3(0.68f, 0.09f, 0.68f),
                new Color(1f, 0.9f, 0.4f), upright);
            var coin = go.AddComponent<Coin>();
            coin.visual = holder;
            coin.bobOffset = UnityEngine.Random.value * 10f;
            return coin;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            bool available = Available;
            if (visual.activeSelf != available) visual.SetActive(available);
            if (!available) return;

            visual.transform.localRotation = Quaternion.Euler(0f, Time.time * 200f + bobOffset * 36f, 0f);
            visual.transform.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 4f + bobOffset) * 0.15f, 0f);

            // Six karts at most: a distance check is cheaper than a physics query per coin.
            foreach (var car in CarController.All)
            {
                if (car.Disabled) continue;
                Vector3 d = car.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > Radius * Radius) continue;
                Collect(car);
                return;
            }
        }

        public void Collect(CarController car)
        {
            hiddenUntil = Time.time + RespawnTime;
            car.AddCoin();
            car.MoneyCollected += MoneyValue;
            if (car.IsPlayer) SoundFX.Overtake();
            Collected?.Invoke(car);
        }
    }
}
