using UnityEngine;

namespace RacingGame.CameraRig
{
    /// <summary>Slow cinematic orbit around a point, used behind the main menu.</summary>
    public class MenuCamera : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 16f;
        public float height = 5.5f;
        public float degreesPerSecond = 7f;
        public Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);
        float angle = 200f;

        void LateUpdate()
        {
            angle += degreesPerSecond * Time.unscaledDeltaTime;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, height, -radius);
            transform.position = center + offset;
            transform.LookAt(center + lookOffset);
        }
    }
}
