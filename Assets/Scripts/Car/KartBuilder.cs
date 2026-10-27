using UnityEngine;
using RacingGame.Core;

namespace RacingGame.Car
{
    /// <summary>
    /// Kart-racer go-kart built from toon-shaded primitives (no model files needed):
    /// chunky round tyres with white rims and team-coloured hubs, a low chassis with nose and bumper,
    /// side pods, rear engine with twin exhausts, and a driver with a big round helmet and visor.
    /// Sized to the 1.9 m x 3.8 m physics box. Faces +Z, origin on the ground.
    /// </summary>
    public static class KartBuilder
    {
        /// <summary>Vehicles that are drawn as karts (the four-wheeled ones; the bike keeps its model).</summary>
        public static bool IsKart(string model) => model != null && model.StartsWith("vehicle-truck");

        public struct Parts
        {
            public Transform[] SteerPivots;   // front wheels, turned around Y
            public Transform[] Wheels;        // all wheels, spun around X
            public GameObject BoostFlames;
        }

        static readonly Color White = new Color(0.96f, 0.96f, 0.98f);
        static readonly Color Metal = new Color(0.48f, 0.5f, 0.56f);
        static readonly Color Chrome = new Color(0.72f, 0.75f, 0.8f);
        static readonly Color Seat = new Color(0.16f, 0.17f, 0.22f);
        static readonly Color Tyre = new Color(0.09f, 0.09f, 0.11f);
        static readonly Color Visor = new Color(0.11f, 0.14f, 0.2f);

        public static Parts Build(Transform t, Color paint)
        {
            float o = ToonStyle.ModelOutline;
            Color dark = Shade(paint, 0.62f), overalls = Shade(paint, 0.85f), light = Color.Lerp(paint, Color.white, 0.45f);

            // Chassis: floor pan, side pods, nose cone, front bumper, rear engine.
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.32f, 0.1f), new Vector3(1.44f, 0.2f, 3f), paint, o);
            Part(PrimitiveType.Cube, t, new Vector3(-0.74f, 0.43f, -0.1f), new Vector3(0.28f, 0.38f, 1.5f), White, o);
            Part(PrimitiveType.Cube, t, new Vector3(0.74f, 0.43f, -0.1f), new Vector3(0.28f, 0.38f, 1.5f), White, o);
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.5f, 1.15f), new Vector3(1f, 0.4f, 1.2f), paint, o);
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.3f, 1.92f), new Vector3(1.56f, 0.24f, 0.26f), White, o);
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.6f, -1.25f), new Vector3(0.92f, 0.42f, 0.6f), Metal, o);
            foreach (float x in new[] { -0.24f, 0.24f })
                Part(PrimitiveType.Cylinder, t, new Vector3(x, 0.8f, -1.62f), new Vector3(0.16f, 0.2f, 0.16f), Chrome, o,
                    Quaternion.Euler(90f, 0f, 0f));

            // Driver: seat back, body in overalls, arms, steering wheel, helmet with stripe and visor.
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.85f, -0.62f), new Vector3(0.8f, 0.7f, 0.22f), Seat, o);
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.82f, -0.22f), new Vector3(0.74f, 0.76f, 0.6f), overalls, o);
            Part(PrimitiveType.Cube, t, new Vector3(-0.32f, 1.03f, 0.18f), new Vector3(0.2f, 0.16f, 0.62f), light, o);
            Part(PrimitiveType.Cube, t, new Vector3(0.32f, 1.03f, 0.18f), new Vector3(0.2f, 0.16f, 0.62f), light, o);
            Part(PrimitiveType.Cylinder, t, new Vector3(0f, 1.02f, 0.52f), new Vector3(0.5f, 0.03f, 0.5f), Seat, o,
                Quaternion.Euler(-60f, 0f, 0f));
            Part(PrimitiveType.Sphere, t, new Vector3(0f, 1.72f, -0.15f), Vector3.one * 1.12f, paint, o);
            Part(PrimitiveType.Sphere, t, new Vector3(0f, 1.73f, -0.15f), new Vector3(0.2f, 1.14f, 1.14f), White, 0f);
            Part(PrimitiveType.Sphere, t, new Vector3(0f, 1.68f, 0.22f), new Vector3(0.86f, 0.42f, 0.56f), Visor, 0f);

            // Wheels: pivot (steering) -> spinner (rolls around X) -> tyre, rim, hub.
            var steer = new Transform[2];
            var wheels = new Transform[4];
            var spots = new (float x, float z, bool front)[]
            {
                (-0.92f, 1.15f, true), (0.92f, 1.15f, true), (-0.98f, -1.05f, false), (0.98f, -1.05f, false),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var (x, z, front) = spots[i];
                float r = front ? 0.38f : 0.48f, width = front ? 0.42f : 0.56f;
                var pivot = new GameObject("WheelPivot" + i).transform;
                pivot.SetParent(t, false);
                pivot.localPosition = new Vector3(x, r, z);
                var spinner = new GameObject("Wheel").transform;
                spinner.SetParent(pivot, false);

                var axle = Quaternion.Euler(0f, 0f, 90f);
                float side = Mathf.Sign(x);
                Part(PrimitiveType.Cylinder, spinner, Vector3.zero, new Vector3(r * 2f, width * 0.5f, r * 2f), Tyre, o, axle);
                Part(PrimitiveType.Cylinder, spinner, new Vector3(side * (width * 0.5f + 0.01f), 0f, 0f),
                    new Vector3(r * 1.24f, 0.02f, r * 1.24f), White, 0f, axle);
                Part(PrimitiveType.Cylinder, spinner, new Vector3(side * (width * 0.5f + 0.03f), 0f, 0f),
                    new Vector3(r * 0.6f, 0.02f, r * 0.6f), paint, 0f, axle);

                wheels[i] = spinner;
                if (front) steer[i] = pivot;
            }

            return new Parts { SteerPivots = steer, Wheels = wheels, BoostFlames = BoostFlames(t) };
        }

        /// <summary>A kart for display only (menu showcase, garage): same look, no physics.</summary>
        public static GameObject BuildDisplay(Transform parent, Color paint)
        {
            var root = new GameObject("Kart");
            root.transform.SetParent(parent, false);
            Build(root.transform, paint);
            return root;
        }

        /// <summary>
        /// Twin exhaust flames. CarController scales this parent to (0.5, 0.5, ~1.5) while boosting,
        /// so the children are placed and sized to land on the two exhaust pipes at that scale.
        /// </summary>
        static GameObject BoostFlames(Transform t)
        {
            var root = new GameObject("BoostFlames");
            root.transform.SetParent(t, false);
            root.transform.localPosition = new Vector3(0f, 0.8f, -1.75f);
            var blue = new Color(0.3f, 0.7f, 1f);
            foreach (float x in new[] { -0.48f, 0.48f })
                Part(PrimitiveType.Sphere, root.transform, new Vector3(x, 0f, -0.25f), new Vector3(0.6f, 0.6f, 0.55f), blue, 0f);
            return root;
        }

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color, float outline,
            Quaternion? rot = null) =>
            MaterialFactory.Visual(type, parent, pos, scale, color, rot, outline);

        static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1f);
    }
}
