namespace RacingGame.Car
{
    /// <summary>One frame of driving commands. Buttons (fire/mine/boost) are "pressed this frame".</summary>
    public struct CarInputState
    {
        public float Throttle;   // -1 (brake/reverse) .. 1 (full gas)
        public float Steer;      // -1 (left) .. 1 (right)
        public bool Fire;
        public bool Mine;
        public bool Boost;
    }

    /// <summary>Anything that can drive a car: the player, an AI, later a network peer.</summary>
    public interface ICarInputSource
    {
        CarInputState ReadInput();
    }
}
