using System;
using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;
using RacingGame.Progress;
using RacingGame.Weapons;

namespace RacingGame.Track
{
    public enum PickupType { Money, Armor, ItemBox }

    /// <summary>
    /// Track pickups. The rainbow "?" item box (kart-racer style) gives a random reward:
    /// a missile, a mine, a nitro, armor repair or cash. Pickups respawn after a few seconds.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        public const int MoneyValue = 100;
        const float Radius = 2.2f;
        const float RespawnTime = 6f;

        /// <summary>(car, reward label) - raised whenever a car collects something.</summary>
        public static event Action<CarController, string> Collected;

        static readonly Collider[] overlap = new Collider[8];

        PickupType type;
        GameObject visual;
        float hiddenUntil;
        float bobOffset;

        public static Pickup Create(Transform parent, Vector3 position, PickupType type)
        {
            var go = new GameObject(type + "Pickup");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            GameObject visual;
            switch (type)
            {
                case PickupType.Money:
                    visual = MaterialFactory.Visual(PrimitiveType.Cylinder, go.transform, Vector3.zero,
                        new Vector3(1.2f, 0.12f, 1.2f), new Color(1f, 0.85f, 0.1f), Quaternion.Euler(90f, 0f, 0f));
                    break;
                case PickupType.Armor:
                    visual = MaterialFactory.Visual(PrimitiveType.Cube, go.transform, Vector3.zero,
                        new Vector3(1f, 1f, 1f), new Color(0.2f, 1f, 0.4f));
                    break;
                default:
                    visual = BuildItemBox(go.transform);
                    break;
            }

            var p = go.AddComponent<Pickup>();
            p.type = type;
            p.visual = visual;
            p.bobOffset = UnityEngine.Random.value * 10f;
            return p;
        }

        /// <summary>Rainbow translucent cube with a big "?" inside.</summary>
        static GameObject BuildItemBox(Transform parent)
        {
            var holder = new GameObject("ItemBox");
            holder.transform.SetParent(parent, false);

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(holder.transform, false);
            box.transform.localScale = Vector3.one * 1.5f;
            box.transform.localRotation = Quaternion.Euler(20f, 0f, 20f);
            var mat = ToonStyle.ItemBox();
            box.GetComponent<Renderer>().sharedMaterial = mat != null ? mat : MaterialFactory.Get(new Color(0.9f, 0.6f, 1f));

            // "?" made of a 3D text mesh, readable from both sides.
            for (int side = 0; side < 2; side++)
            {
                var q = new GameObject("QuestionMark");
                q.transform.SetParent(holder.transform, false);
                q.transform.localRotation = Quaternion.Euler(0f, side * 180f, 0f);
                var text = q.AddComponent<TextMesh>();
                text.text = "?";
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.characterSize = 0.12f;
                text.fontSize = 64;
                text.fontStyle = FontStyle.Bold;
                text.color = Color.white;
                var font = Resources.Load<Font>("Fonts/RussoOne");
                if (font != null)
                {
                    text.font = font;
                    q.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                }
            }
            return holder;
        }

        void Update()
        {
            bool available = Time.time >= hiddenUntil;
            if (visual.activeSelf != available) visual.SetActive(available);
            if (!available) return;

            // Spin and bob.
            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
            if (type == PickupType.ItemBox)
                visual.transform.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 2.5f + bobOffset) * 0.2f, 0f);

            int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, overlap,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hp = overlap[i].GetComponentInParent<CarHealth>();
                if (hp == null || hp.IsWrecked) continue;
                Give(hp);
                hiddenUntil = Time.time + RespawnTime;
                return;
            }
        }

        void Give(CarHealth hp)
        {
            var car = hp.Controller;
            string label;
            var t = type;
            if (t == PickupType.ItemBox)
            {
                // Random reward from the box.
                int roll = UnityEngine.Random.Range(0, 5);
                t = roll == 3 ? PickupType.Armor : roll == 4 ? PickupType.Money : PickupType.ItemBox;
                if (t == PickupType.ItemBox)
                {
                    var weapons = car.GetComponent<WeaponSystem>();
                    label = roll == 0 ? "MISSILE" : roll == 1 ? "MINE" : "NITRO";
                    if (weapons != null) weapons.AddAmmo(roll == 0 ? 1 : 0, roll == 1 ? 1 : 0, roll == 2 ? 1 : 0);
                    Finish(car, label);
                    return;
                }
            }

            if (t == PickupType.Money)
            {
                car.MoneyCollected += MoneyValue;
                if (car.IsPlayer) Stats.Add(Stat.MoneyPickups);
                label = "+$" + MoneyValue;
            }
            else
            {
                hp.Repair(40f);
                label = "ARMOR REPAIR";
            }
            Finish(car, label);
        }

        void Finish(CarController car, string label)
        {
            if (car.IsPlayer) ModelLibrary.PlayAt("impact", transform.position, 0.4f, 1.8f, 0f);
            Collected?.Invoke(car, label);
        }
    }
}
