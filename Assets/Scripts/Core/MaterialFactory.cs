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
                temp.SetActive(false);   // destroyed at the end of the frame; keep it invisible until then
                SafeDestroy(temp);
            }

            // Cartoon shading when the toon shader is available, Unity's default material otherwise.
            mat = ToonStyle.CreateToon(color, outline) ?? new Material(baseMaterial) { color = color };
            cache[(color, outline)] = mat;
            return mat;
        }

        /// <summary>
        /// Removes a collider from a visual-only object. Disabled at once and destroyed at the end of the frame:
        /// DestroyImmediate is not allowed while Unity is reporting collisions (e.g. an explosion from a ramming wreck).
        /// </summary>
        public static void RemoveCollider(Collider collider)
        {
            if (collider == null) return;
            collider.enabled = false;
            SafeDestroy(collider);
        }

        /// <summary>Destroy in play mode (safe inside physics callbacks), DestroyImmediate in edit mode.</summary>
        public static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

        /// <summary>Creates a primitive without collider, colored, parented.</summary>
        public static GameObject Visual(PrimitiveType type, Transform parent, Vector3 localPos,
            Vector3 localScale, Color color, Quaternion? localRot = null, float outline = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            RemoveCollider(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Get(color, outline);
            return go;
        }
    }
}
