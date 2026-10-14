using UnityEngine;
using RacingGame.Core;

namespace RacingGame.Car
{
    /// <summary>Engine loop (pitch follows speed), tyre skid when sliding, impact sounds.</summary>
    [RequireComponent(typeof(CarController))]
    public class CarAudio : MonoBehaviour
    {
        CarController car;
        AudioSource engine;
        AudioSource skid;

        void Start()
        {
            car = GetComponent<CarController>();
            float baseVolume = car.IsPlayer ? 0.55f : 0.3f;
            float blend = car.IsPlayer ? 0f : 1f;

            engine = CreateLoop("engine", baseVolume, blend);
            skid = CreateLoop("skid", 0f, blend);
        }

        AudioSource CreateLoop(string clip, float volume, float spatialBlend)
        {
            var c = ModelLibrary.Clip(clip);
            if (c == null) return null;
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = c;
            src.loop = true;
            src.volume = volume;
            src.spatialBlend = spatialBlend;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 5f;
            src.maxDistance = 70f;
            src.dopplerLevel = 0f;
            src.time = Random.Range(0f, c.length);
            src.Play();
            return src;
        }

        void Update()
        {
            bool alive = !car.Disabled;
            if (engine)
            {
                float speed01 = Mathf.Clamp01(Mathf.Abs(car.ForwardSpeed) / car.maxSpeed);
                float throttle = Mathf.Abs(car.CurrentInput.Throttle);
                float targetPitch = 0.7f + speed01 * 1.1f + throttle * 0.15f + (car.IsBoosting ? 0.25f : 0f);
                engine.pitch = Mathf.Lerp(engine.pitch, targetPitch, Time.deltaTime * 6f);
                engine.mute = !alive;
            }
            if (skid)
            {
                bool sliding = alive && car.IsGrounded && Mathf.Abs(car.LateralSpeed) > 4f;
                float target = sliding ? Mathf.Clamp01((Mathf.Abs(car.LateralSpeed) - 4f) / 6f) * (car.IsPlayer ? 0.5f : 0.25f) : 0f;
                skid.volume = Mathf.MoveTowards(skid.volume, target, Time.deltaTime * 3f);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            float impact = collision.relativeVelocity.magnitude;
            if (impact < 5f) return;
            float vol = Mathf.Clamp01(impact / 25f) * (car.IsPlayer ? 1f : 0.6f);
            ModelLibrary.PlayAt("impact", collision.GetContact(0).point, vol, Random.Range(0.9f, 1.1f));
        }
    }
}
