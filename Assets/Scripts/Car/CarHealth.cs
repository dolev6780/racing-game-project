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
        /// <summary>Shield item: blocks the next hit (damage or oil) while it lasts.</summary>
        public bool HasShield => Time.time < shieldUntil;

        /// <summary>(victim, attacker) - attacker may be null.</summary>
        public static event Action<CarHealth, CarHealth> Wrecked;
        /// <summary>(victim, damage amount) - raised on every hit that lands.</summary>
        public static event Action<CarHealth, float> Damaged;
        /// <summary>Raised when a shield absorbs a hit.</summary>
        public static event Action<CarHealth> ShieldBlocked;

        public CarController Controller { get; private set; }
        public GameObject VisualRoot { get; set; }

        float invulnerableUntil;
        float shieldUntil;
        GameObject shieldBubble;
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
            if (TryBlock()) return;
            Armor -= amount;
            Damaged?.Invoke(this, amount);
            if (Armor <= 0f)
            {
                Armor = 0f;
                StartCoroutine(WreckRoutine(attacker));
            }
        }

        public void ActivateShield(float seconds) => shieldUntil = Mathf.Max(shieldUntil, Time.time + seconds);

        /// <summary>If a shield is up, it absorbs this hit and pops. Returns true when blocked.</summary>
        public bool TryBlock()
        {
            if (!HasShield) return false;
            shieldUntil = 0f;
            ShieldBlocked?.Invoke(this);
            return true;
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
            Controller.HalveCoins();   // a wreck costs half your coins
            shieldUntil = 0f;
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
            UpdateShieldBubble();

            // Blink while invulnerable after respawn.
            if (VisualRoot && !IsWrecked)
            {
                bool visible = !IsInvulnerable || Mathf.Repeat(Time.time * 10f, 1f) > 0.35f;
                if (VisualRoot.activeSelf != visible) VisualRoot.SetActive(visible);
            }
        }

        /// <summary>See-through rainbow bubble around the kart while the shield is up.</summary>
        void UpdateShieldBubble()
        {
            bool show = HasShield && !IsWrecked;
            if (show && shieldBubble == null)
            {
                shieldBubble = MaterialFactory.Visual(PrimitiveType.Sphere, transform, new Vector3(0f, 1f, 0f),
                    new Vector3(3.4f, 3f, 4.8f), new Color(0.5f, 0.8f, 1f));
                var mat = ToonStyle.ItemBox();
                if (mat != null) shieldBubble.GetComponent<Renderer>().sharedMaterial = mat;
                shieldBubble.name = "ShieldBubble";
            }
            if (shieldBubble != null && shieldBubble.activeSelf != show) shieldBubble.SetActive(show);
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
