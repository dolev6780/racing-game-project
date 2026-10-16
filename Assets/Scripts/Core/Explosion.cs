using UnityEngine;

namespace RacingGame.Core
{
    /// <summary>Simple placeholder explosion: a glowing sphere that grows then shrinks.</summary>
    public class Explosion : MonoBehaviour
    {
        float size;
        float duration;
        float age;

        /// <summary>(position, size) - lets the camera shake when something blows up nearby.</summary>
        public static event System.Action<Vector3, float> Exploded;

        public static void Spawn(Vector3 position, float size, Color color)
        {
            Exploded?.Invoke(position, size);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Explosion";
            go.transform.position = position;
            go.transform.localScale = Vector3.zero;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Get(color);
            var ex = go.AddComponent<Explosion>();
            ex.size = size;
            ex.duration = 0.45f;
            ModelLibrary.PlayAt("impact", position, Mathf.Clamp01(size / 5f), 0.55f);

            // Debris chunks
            for (int i = 0; i < 6; i++)
            {
                var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.transform.position = position + Random.insideUnitSphere * 0.5f + Vector3.up * 0.5f;
                chunk.transform.localScale = Vector3.one * Random.Range(0.15f, 0.35f) * size * 0.4f;
                chunk.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Get(new Color(0.15f, 0.15f, 0.15f));
                chunk.layer = 2; // Ignore Raycast
                var rb = chunk.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                rb.AddForce((Random.insideUnitSphere + Vector3.up * 1.5f) * 6f, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.VelocityChange);
                Destroy(chunk, 1.5f);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = age / duration;
            if (t >= 1f) { Destroy(gameObject); return; }
            float s = Mathf.Sin(t * Mathf.PI) * size;
            transform.localScale = Vector3.one * s;
        }
    }
}
