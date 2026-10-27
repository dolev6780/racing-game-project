using UnityEngine;
using RacingGame.Core;

namespace RacingGame.Track
{
    /// <summary>
    /// A wedge ramp in one lane: karts launch off it; pressing the trick button in the air
    /// (DRIFT, or tapping GAS on a phone) gives a boost on landing.
    /// </summary>
    public class Ramp : MonoBehaviour
    {
        public const float Width = 5f, Length = 7f, Height = 1.2f;

        /// <summary>Foot of the ramp (where karts drive on) and the driving direction.</summary>
        public Vector3 Foot => transform.position;
        public Vector3 Direction => transform.forward;

        public static Ramp Create(Transform parent, Vector3 foot, Vector3 forward)
        {
            var go = new GameObject("Ramp");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(foot, Quaternion.LookRotation(forward));

            var mesh = Wedge();
            var top = new GameObject("RampTop");
            top.transform.SetParent(go.transform, false);
            top.AddComponent<MeshFilter>().sharedMesh = mesh;
            var chevron = ToonStyle.Chevron();
            top.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                chevron != null ? chevron : MaterialFactory.Get(new Color(1f, 0.82f, 0.12f)),
                MaterialFactory.Get(new Color(1f, 0.82f, 0.12f), ToonStyle.PropOutline),
            };

            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = true;
            return go.AddComponent<Ramp>();
        }

        /// <summary>Slope (submesh 0, chevrons pointing forward), back and sides (submesh 1).</summary>
        static Mesh Wedge()
        {
            float w = Width * 0.5f, l = Length, h = Height;
            var v = new[]
            {
                // slope
                new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f), new Vector3(-w, h, l), new Vector3(w, h, l),
                // back face (z = l)
                new Vector3(-w, 0f, l), new Vector3(w, 0f, l), new Vector3(-w, h, l), new Vector3(w, h, l),
                // left side, right side
                new Vector3(-w, 0f, 0f), new Vector3(-w, 0f, l), new Vector3(-w, h, l),
                new Vector3(w, 0f, 0f), new Vector3(w, h, l), new Vector3(w, 0f, l),
            };
            // Chevron texture points along +U, so U runs up the slope.
            var uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
            };
            var mesh = new Mesh { name = "RampWedge", subMeshCount = 2 };
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.SetTriangles(new[] { 0, 2, 3, 0, 3, 1 }, 0);
            mesh.SetTriangles(new[] { 4, 5, 7, 4, 7, 6, 8, 9, 10, 11, 12, 13 }, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
