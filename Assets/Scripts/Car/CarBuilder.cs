using UnityEngine;
using RacingGame.Core;
using RacingGame.Weapons;

namespace RacingGame.Car
{
    /// <summary>
    /// Builds a combat car out of primitives (placeholder art). Later, replace the
    /// visual part with a real 3D model prefab - the physics/components stay the same.
    /// </summary>
    public static class CarBuilder
    {
        static PhysicsMaterial carPhysicsMaterial;

        public static CarController Create(string name, Color paint, Vector3 position, Quaternion rotation,
            Transform parent, bool isPlayer)
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
            var t = body.transform;
            var dark = new Color(0.08f, 0.08f, 0.1f);
            var metal = new Color(0.55f, 0.55f, 0.6f);

            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.5f, 3.7f), paint);
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0f, 0.75f, 1.3f), new Vector3(1.6f, 0.25f, 1.0f), paint * 0.8f);
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0f, 1.05f, -0.3f), new Vector3(1.4f, 0.45f, 1.6f), dark);
            // Spoiler
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0f, 1.25f, -1.7f), new Vector3(1.9f, 0.08f, 0.45f), paint);
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(-0.6f, 1.0f, -1.7f), new Vector3(0.08f, 0.45f, 0.3f), dark);
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0.6f, 1.0f, -1.7f), new Vector3(0.08f, 0.45f, 0.3f), dark);
            // Roof cannon
            MaterialFactory.Visual(PrimitiveType.Cylinder, t, new Vector3(0f, 1.35f, 0.2f), new Vector3(0.22f, 0.6f, 0.22f), metal,
                Quaternion.Euler(90f, 0f, 0f));
            // Headlights
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(-0.6f, 0.65f, 1.86f), new Vector3(0.35f, 0.15f, 0.05f), new Color(1f, 0.95f, 0.6f));
            MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0.6f, 0.65f, 1.86f), new Vector3(0.35f, 0.15f, 0.05f), new Color(1f, 0.95f, 0.6f));
            // Player marker
            if (isPlayer)
                MaterialFactory.Visual(PrimitiveType.Cube, t, new Vector3(0f, 2.3f, 0f), new Vector3(0.5f, 0.5f, 0.5f),
                    new Color(0.2f, 1f, 0.3f), Quaternion.Euler(45f, 0f, 45f));

            // Wheels: pivot (steering) -> mesh (spinning)
            var steerPivots = new Transform[2];
            var wheels = new Transform[4];
            Vector3[] wheelPos =
            {
                new Vector3(-0.95f, 0.38f, 1.25f), new Vector3(0.95f, 0.38f, 1.25f),
                new Vector3(-0.95f, 0.38f, -1.25f), new Vector3(0.95f, 0.38f, -1.25f),
            };
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("WheelPivot" + i).transform;
                pivot.SetParent(t, false);
                pivot.localPosition = wheelPos[i];
                var holder = new GameObject("Axle").transform;
                holder.SetParent(pivot, false);
                holder.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var wheel = MaterialFactory.Visual(PrimitiveType.Cylinder, holder, Vector3.zero, new Vector3(0.76f, 0.2f, 0.76f), dark);
                wheels[i] = wheel.transform;
                if (i < 2) steerPivots[i] = pivot;
            }

            var flame = MaterialFactory.Visual(PrimitiveType.Sphere, t, new Vector3(0f, 0.6f, -2.2f), new Vector3(0.5f, 0.5f, 1.2f),
                new Color(0.3f, 0.7f, 1f));
            controller.SetVisuals(steerPivots, wheels, flame);

            var health = root.AddComponent<CarHealth>();
            health.VisualRoot = body;
            root.AddComponent<RacerProgress>();
            root.AddComponent<WeaponSystem>();
            var ai = root.AddComponent<AICarInput>();   // player cars get one too: it drives after the finish line

            if (isPlayer) controller.InputSource = root.AddComponent<PlayerCarInput>();
            else controller.InputSource = ai;

            return controller;
        }
    }
}
