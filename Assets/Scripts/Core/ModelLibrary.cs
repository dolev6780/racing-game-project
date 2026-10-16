using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Core
{
    /// <summary>
    /// Loads 3D models (.glb, imported by glTFast) from Assets/Resources/Models
    /// and sounds from Assets/Resources/Audio. Returns null when an asset is missing,
    /// so callers can fall back to primitive placeholder art.
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static bool warned;

        /// <summary>Instantiates a model with cartoon shading; vehicles get a thicker outline than props.</summary>
        public static GameObject Instantiate(string name, Transform parent)
        {
            var go = InstantiateRaw(name, parent);
            if (go != null) ToonStyle.Apply(go, name.StartsWith("vehicle") ? ToonStyle.ModelOutline : ToonStyle.PropOutline);
            return go;
        }

        static GameObject InstantiateRaw(string name, Transform parent)
        {
            if (!models.TryGetValue(name, out var prefab))
            {
                prefab = Resources.Load<GameObject>("Models/" + name);
                models[name] = prefab;
                if (prefab == null && !warned)
                {
                    warned = true;
                    Debug.LogWarning($"[ModelLibrary] Model '{name}' not found. Is the glTFast package " +
                                     "(com.unity.cloud.gltfast) installed? Using placeholder art.");
                }
            }
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab, parent, false);
            go.name = name;
            foreach (var col in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            return go;
        }

        public static AudioClip Clip(string name)
        {
            if (!clips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                clips[name] = clip;
            }
            return clip;
        }

        /// <summary>Plays a one-shot sound at a position (mostly 2D so it is audible from the high camera).</summary>
        public static void PlayAt(string clipName, Vector3 position, float volume = 1f, float pitch = 1f,
            float spatialBlend = 0.6f)
        {
            var clip = Clip(clipName);
            if (clip == null) return;
            var go = new GameObject("Sfx_" + clipName);
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = spatialBlend;
            src.ignoreListenerPause = spatialBlend <= 0f;   // UI sounds still play while paused
            src.rolloffMode = AudioRolloffMode.Linear;
            src.maxDistance = 120f;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(pitch, 0.1f) + 0.1f);
        }
    }
}
