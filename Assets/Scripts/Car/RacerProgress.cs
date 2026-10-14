using System;
using UnityEngine;
using RacingGame.Track;

namespace RacingGame.Car
{
    /// <summary>Tracks where the car is on the track: lap, distance, wrong-way, falling off.</summary>
    [RequireComponent(typeof(CarController))]
    public class RacerProgress : MonoBehaviour
    {
        public TrackPath Path { get; private set; }

        /// <summary>0 = on the grid (before the start line), 1 = first lap, ...</summary>
        public int Lap { get; private set; }
        public int CurrentIndex { get; private set; }
        public float DistanceAlong { get; private set; }
        public float TotalProgress => Lap * Path.Length + DistanceAlong;
        public bool WrongWay { get; private set; }

        /// <summary>Fired when the car reaches a new lap for the first time.</summary>
        public event Action<RacerProgress, int> NewLap;

        CarController controller;
        int highestLap;
        float wrongWayTimer;

        void Awake() => controller = GetComponent<CarController>();

        public void Initialize(TrackPath path)
        {
            Path = path;
            CurrentIndex = path.FindClosest(transform.position);
            DistanceAlong = path.DistanceAlong(transform.position, CurrentIndex);
            Lap = 0;
            highestLap = 0;
        }

        void Update()
        {
            if (Path == null || controller.Disabled) return;

            int n = Path.Count;
            int newIndex = Path.FindClosestLocal(transform.position, CurrentIndex);
            int delta = newIndex - CurrentIndex;
            if (delta < -n / 2) Lap++;          // crossed the start line forward
            else if (delta > n / 2) Lap--;      // crossed it backwards
            CurrentIndex = newIndex;
            DistanceAlong = Path.DistanceAlong(transform.position, CurrentIndex);

            if (Lap > highestLap)
            {
                highestLap = Lap;
                NewLap?.Invoke(this, Lap);
            }

            // Wrong-way detection
            float along = Vector3.Dot(controller.Body.linearVelocity, Path.Direction(CurrentIndex));
            wrongWayTimer = along < -3f ? wrongWayTimer + Time.deltaTime : 0f;
            WrongWay = wrongWayTimer > 1.2f;

            // Fell off the world
            if (transform.position.y < -5f) Respawn();
        }

        /// <summary>Put the car back on the centre of the track at its current progress point.</summary>
        public void Respawn()
        {
            if (Path == null) return;
            Vector3 pos = Path.Point(CurrentIndex) + Vector3.up * 0.3f;
            Quaternion rot = Quaternion.LookRotation(Path.Direction(CurrentIndex));
            controller.ResetTo(pos, rot);
        }
    }
}
