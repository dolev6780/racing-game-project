namespace Cloudborne
{
    /// <summary>
    /// Which stage is being played: 1-1 The Jungle Gate, or 1-2 The Treetops (the user: on to the next stage). The
    /// level, the scenery, the soldiers, the tips and the boss follow it; CbGame rebuilds them when it changes.
    /// </summary>
    public static class CbStage
    {
        public static string Id { get; private set; } = "1-1";

        /// <summary>Stage 1-2, The Treetops: the chase among the branches to the loggers' camp.</summary>
        public static bool Treetops => Id == "1-2";

        /// <summary>The stage's name, as the loading screen shows it.</summary>
        public static string Title => Treetops ? "STAGE 1-2 · THE TREETOPS" : "STAGE 1-1 · THE JUNGLE GATE";

        public static void Set(string id) => Id = id == "1-2" ? "1-2" : "1-1";
    }
}
