using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core;
using RacingGame.Weapons;

namespace RacingGame.Car
{
    /// <summary>
    /// Builds a combat car: physics + gameplay components, and the 3D visual: a go-kart with a driver
    /// (KartBuilder) for four-wheeled vehicles, the Kenney .glb model for the bike, or a primitive fallback.
    /// </summary>
    public static class CarBuilder
    {
        /// <summary>Kenney trucks are 1.5m x 2.8m; scale them to fit our 1.9m x 3.8m physics box.</summary>
        const float ModelScale = 1.3f;

        static PhysicsMaterial carPhysicsMaterial;

        public static CarController Create(string name, Color paint, string modelName, Vector3 position,
            Quaternion rotation, Transform parent, bool isPlayer)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);

            if (carPhysicsMaterial == null)
            {
                carPhysicsMaterial = new PhysicsMaterial("Car")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0.25f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Maximum,
                };
            }

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.size = new Vector3(1.9f, 0.9f, 3.8f);
            col.sharedMaterial = carPhysicsMaterial;

            root.AddComponent<Rigidbody>();
            var controller = root.AddComponent<CarController>();
            controller.DisplayName = name;
            controller.PaintColor = paint;
            controller.IsPlayer = isPlayer;

            // ----- Visuals -----
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);

            bool kart = KartBuilder.IsKart(modelName);
            if (kart)
            {
                var parts = KartBuilder.Build(body.transform, paint);
                controller.SetVisuals(parts.SteerPivots, parts.Wheels, parts.BoostFlames);
            }
            else if (!BuildModelVisual(body.transform, modelName, controller))
                BuildPrimitiveVisual(body.transform, paint, controller);

            // Weapon: a short cannon on the kart's nose (on the roof of other vehicles), and the player marker on top.
            var gunmetal = new Color(0.3f, 0.3f, 0.35f);
            if (kart)
                MaterialFactory.Visual(PrimitiveType.Cylinder, body.transform, new Vector3(0f, 0.8f, 1.35f),
                    new Vector3(0.16f, 0.4f, 0.16f), gunmetal, Quaternion.Euler(90f, 0f, 0f), ToonStyle.ModelOutline);
            else
                MaterialFactory.Visual(PrimitiveType.Cylinder, body.transform, new Vector3(0f, 1.75f, -0.2f),
                    new Vector3(0.2f, 0.55f, 0.2f), gunmetal, Quaternion.Euler(90f, 0f, 0f));
            if (isPlayer)
                MaterialFactory.Visual(PrimitiveType.Cube, body.transform, new Vector3(0f, 2.95f, 0f),
                    new Vector3(0.5f, 0.5f, 0.5f), new Color(0.2f, 1f, 0.3f), Quaternion.Euler(45f, 0f, 45f));

            var health = root.AddComponent<CarHealth>();
            health.VisualRoot = body;
            root.AddComponent<RacerProgress>();
            root.AddComponent<WeaponSystem>();
            root.AddComponent<CarAudio>();
            root.AddComponent<DriftSparks>();
            root.AddComponent<DamageSmoke>();
            root.AddComponent<SkidMarks>();
            var ai = root.AddComponent<AICarInput>();   // player cars get one too: it drives after the finish line

            if (isPlayer) controller.InputSource = root.AddComponent<PlayerCarInput>();
            else controller.InputSource = ai;

            return controller;
        }

        /// <summary>
        /// Uses a Kenney vehicle model. Expects children named "body" and "wheel-*"
        /// with each wheel's pivot at its centre.
        /// </summary>
        static bool BuildModelVisual(Transform parent, string modelName, CarController controller)
        {
            if (string.IsNullOrEmpty(modelName)) return false;
            var model = ModelLibrary.Instantiate(modelName, parent);
            if (model == null) return false;

            model.transform.localScale = Vector3.one * ModelScale;

            var wheelNodes = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>())
                if (t != model.transform && t.name.StartsWith("wheel")) wheelNodes.Add(t);

            // Make sure the model faces +Z (front wheels in front), whatever the importer's axis convention.
            float frontZ = 0f;
            foreach (var w in wheelNodes)
                if (w.name.Contains("front")) frontZ += model.transform.InverseTransformPoint(w.position).z;
            if (frontZ < 0f) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Wrap each wheel: steering pivot (Y rotation) -> wheel (spins around X).
            var steer = new List<Transform>();
            var spin = new List<Transform>();
            foreach (var w in wheelNodes)
            {
                var pivot = new GameObject(w.name + "-pivot").transform;
                pivot.SetParent(w.parent, false);
                pivot.localPosition = w.localPosition;
                pivot.localRotation = Quaternion.identity;
                pivot.localScale = Vector3.one;
                w.SetParent(pivot, true);

                spin.Add(w);
                bool isFront = parent.InverseTransformPoint(w.position).z > 0f;
                if (isFront) steer.Add(pivot);
            }

            controller.SetVisuals(steer.ToArray(), spin.ToArray(), CreateBoostFlame(parent));
            return true;
        }

        /// <summary>Placeholder car made of primitives (used if the models can't be loaded).</summary>
        static void BuildPrimitiveVisual(Transform t, Color paint, CarController controller)
        {
            var dark = new Color(0.08f, 0.08f, 0.1f);

            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.5f, 3.7f), paint);
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0f, 0.75f, 1.3f), new Vector3(1.6f, 0.25f, 1.0f), paint * 0.8f);
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0f, 1.05f, -0.3f), new Vector3(1.4f, 0.45f, 1.6f), dark);
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0f, 1.25f, -1.7f), new Vector3(1.9f, 0.08f, 0.45f), paint);
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(-0.6f, 0.65f, 1.86f), new Vector3(0.35f, 0.15f, 0.05f), new Color(1f, 0.95f, 0.6f));
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0.6f, 0.65f, 1.86f), new Vector3(0.35f, 0.15f, 0.05f), new Color(1f, 0.95f, 0.6f));

            // Wheels: pivot (steering) -> spinner (rotates around X) -> cylinder mesh
            var steerPivots = new Transform[2];
            var wheels = new Transform[4];
            Vector3[] wheelPos =
            {
                new Vector3(-0.95f, 0.39f, 1.25f), new Vector3(0.95f, 0.39f, 1.25f),
                new Vector3(-0.95f, 0.39f, -1.25f), new Vector3(0.95f, 0.39f, -1.25f),
            };
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("WheelPivot" + i).transform;
                pivot.SetParent(t, false);
                pivot.localPosition = wheelPos[i];
                var spinner = new GameObject("Wheel").transform;
                spinner.SetParent(pivot, false);
                MaterialFactory.Visual(PrimitiveType.Cylinder, spinner, Vector3.zero, new Vector3(0.78f, 0.2f, 0.78f), dark,
                    Quaternion.Euler(0f, 0f, 90f));
                wheels[i] = spinner;
                if (i < 2) steerPivots[i] = pivot;
            }

            controller.SetVisuals(steerPivots, wheels, CreateBoostFlame(t));
        }

        static GameObject CreateBoostFlame(Transform parent)
        {
            return MaterialFactory.Visual(PrimitiveType.Sphere, parent, new Vector3(0f, 0.6f, -2.2f),
                new Vector3(0.5f, 0.5f, 1.2f), new Color(0.3f, 0.7f, 1f));
        }
    }
}
