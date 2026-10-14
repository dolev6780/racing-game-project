using System;
using System.Collections;
using UnityEngine;
using RacingGame.Core;

namespace RacingGame.Car
{
    /// <summary>Armor, damage from weapons and collisions, wreck + respawn.</summary>
    [RequireComponent(typeof(CarController))]
    public class CarHealth : MonoBehaviour
    {
        public float maxArmor = 100f;
        public float respawnDelay = 2f;
        public float invulnerableTime = 2f;

        public float Armor { get; private set; }
        public float Armor01 => Armor / maxArmor;
        public bool IsWrecked { get; private set; }
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        /// <summary>(victim, attacker) - attacker may be null.</summary>
        public static event Action<CarHealth, CarHealth> Wrecked;

        public CarController Controller { get; private set; }
        public GameObject VisualRoot { get; set; }

        float invulnerableUntil;
        Collider[] colliders;

        void Awake()
        {
            Controller = GetComponent<CarController>();
            Armor = maxArmor;
            colliders = GetComponentsInChildren<Collider>();
        }

        public void TakeDamage(float amount, CarHealth attacker)
        {
            if (IsWrecked || IsInvulnerable || amount <= 0f) return;
            Armor -= amount;
            if (Armor <= 0f)
            {
                Armor = 0f;
                StartCoroutine(WreckRoutine(attacker));
            }
        }

        /// <summary>Used by the garage to apply armor upgrades before the race.</summary>
        public void SetMaxArmor(float value)
        {
            maxArmor = value;
            Armor = value;
        }

        public void Repair(float amount)
        {
            if (IsWrecked) return;
            Armor = Mathf.Min(maxArmor, Armor + amount);
        }

        IEnumerator WreckRoutine(CarHealth attacker)
        {
            IsWrecked = true;
            Controller.Disabled = true;
            Explosion.Spawn(transform.position + Vector3.up, 6f, new Color(1f, 0.55f, 0.1f));
            Wrecked?.Invoke(this, attacker);

            SetPresent(false);
            yield return new WaitForSeconds(respawnDelay);

            var progress = GetComponent<RacerProgress>();
            if (progress) progress.Respawn();
            SetPresent(true);

            Armor = maxArmor;
            IsWrecked = false;
            Controller.Disabled = false;
            invulnerableUntil = Time.time + invulnerableTime;
        }

        void SetPresent(bool present)
        {
            Controller.Body.isKinematic = !present;
            foreach (var c in colliders) c.enabled = present;
            if (VisualRoot) VisualRoot.SetActive(present);
        }

        void Update()
        {
            // Blink while invulnerable after respawn.
            if (VisualRoot && !IsWrecked)
            {
                bool visible = !IsInvulnerable || Mathf.Repeat(Time.time * 10f, 1f) > 0.35f;
                if (VisualRoot.activeSelf != visible) VisualRoot.SetActive(visible);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            float impact = collision.relativeVelocity.magnitude;
            var other = collision.collider.GetComponentInParent<CarHealth>();
            if (other != null)
            {
                // Ramming: the faster car deals more damage.
                if (impact > 8f) TakeDamage((impact - 8f) * 1.2f, other);
            }
            else if (impact > 20f)
            {
                TakeDamage((impact - 20f) * 0.5f, null);
            }
        }
    }
}
