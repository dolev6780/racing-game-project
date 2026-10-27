using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Core
{
    /// <summary>
    /// Creates colored materials: toon-shaded (RacingGame/Toon) when available, otherwise
    /// a copy of the default primitive material so it works in any render pipeline.
    /// </summary>
    public static class MaterialFactory
    {
        static Material baseMaterial;
        static readonly Dictionary<(Color, float), Material> cache = new Dictionary<(Color, float), Material>();

        /// <summary>Shared material of a colour; <paramref name="outline"/> &gt; 0 adds a cartoon outline.</summary>
        public static Material Get(Color color, float outline = 0f)
        {
            if (cache.TryGetValue((color, outline), out var mat) && mat != null) return mat;

            if (baseMaterial == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMaterial = temp.GetComponent<Renderer>().sharedMaterial;
                Object.DestroyImmediate(temp);
            }

            // Cartoon shading when the toon shader is available, Unity's default material otherwise.
            mat = ToonStyle.CreateToon(color, outline) ?? new Material(baseMaterial) { color = color };
            cache[(color, outline)] = mat;
            return mat;
        }

        /// <summary>Creates a primitive without collider, colored, parented.</summary>
        public static GameObject Visual(PrimitiveType type, Transform parent, Vector3 localPos,
            Vector3 localScale, Color color, Quaternion? localRot = null, float outline = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Get(color, outline);
            return go;
        }
    }
}
