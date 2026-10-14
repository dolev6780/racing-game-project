using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Core
{
    /// <summary>
    /// Creates colored materials from the default primitive material, so it works with
    /// both the Built-in render pipeline and URP without needing Shader.Find.
    /// </summary>
    public static class MaterialFactory
    {
        static Material baseMaterial;
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

        public static Material Get(Color color)
        {
            if (cache.TryGetValue(color, out var mat) && mat != null) return mat;

            if (baseMaterial == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMaterial = temp.GetComponent<Renderer>().sharedMaterial;
                Object.DestroyImmediate(temp);
            }

            mat = new Material(baseMaterial) { color = color };
            cache[color] = mat;
            return mat;
        }

        /// <summary>Creates a primitive without collider, colored, parented.</summary>
        public static GameObject Visual(PrimitiveType type, Transform parent, Vector3 localPos,
            Vector3 localScale, Color color, Quaternion? localRot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Get(color);
            return go;
        }
    }
}
