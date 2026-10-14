using UnityEngine;

namespace RacingGame.CameraRig
{
    /// <summary>
    /// Slow cinematic orbit around a point, used behind the main menu and in the garage.
    /// Settings ease smoothly when changed, so switching screens glides the camera.
    /// </summary>
    public class MenuCamera : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 16f;
        public float height = 5.5f;
        public float degreesPerSecond = 7f;
        public Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);
        [Tooltip("Shifts the look point sideways so the subject sits left (+) or right (-) of the screen.")]
        public float lateralShift;

        float angle = 200f;
        Vector3 curCenter;
        float curRadius, curHeight, curShift;
        bool initialized;

        public void Configure(Vector3 newCenter, float newRadius, float newHeight, float shift, float speed)
        {
            center = newCenter;
            radius = newRadius;
            height = newHeight;
            lateralShift = shift;
            degreesPerSecond = speed;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (!initialized)
            {
                curCenter = center; curRadius = radius; curHeight = height; curShift = lateralShift;
                initialized = true;
            }
            float k = 1f - Mathf.Exp(-3f * dt);
            curCenter = Vector3.Lerp(curCenter, center, k);
            curRadius = Mathf.Lerp(curRadius, radius, k);
            curHeight = Mathf.Lerp(curHeight, height, k);
            curShift = Mathf.Lerp(curShift, lateralShift, k);

            angle += degreesPerSecond * dt;
            Quaternion orbit = Quaternion.Euler(0f, angle, 0f);
            transform.position = curCenter + orbit * new Vector3(0f, curHeight, -curRadius);
            Vector3 right = orbit * Vector3.right;
            transform.LookAt(curCenter + lookOffset + right * curShift);
        }
    }
}
