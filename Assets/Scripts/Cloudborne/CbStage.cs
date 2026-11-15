namespace Cloudborne
{
    /// <summary>
    /// Which stage is being played: 1-1 The Jungle Gate, 1-2 The Treetops (the user: on to the next stage), or 1-3
    /// Baba's Grove. The level, the scenery, the soldiers, the tips and the boss follow it; CbGame rebuilds them when it
    /// changes.
    /// </summary>
    public static class CbStage
    {
        public static string Id { get; private set; } = "1-1";

        /// <summary>Stage 1-1, The Jungle Gate: the chase through the jungle to the shrine gate.</summary>
        public static bool Jungle => Id == "1-1";

        /// <summary>Stage 1-2, The Treetops: the chase among the branches to the loggers' camp.</summary>
        public static bool Treetops => Id == "1-2";

        /// <summary>Stage 1-3, Baba's Grove: training - the bells, the bamboo dummies, the stones.</summary>
        public static bool Grove => Id == "1-3";

        /// <summary>Stage 1-4, The Stormy River: riding a log down the falls after Koda's barge; the saw barge.</summary>
        public static bool River => Id == "1-4";

        /// <summary>The stage's name, as the loading screen shows it.</summary>
        public static string Title => Treetops ? "STAGE 1-2 · THE TREETOPS" : Grove ? "STAGE 1-3 · BABA'S GROVE" : River ? "STAGE 1-4 · THE STORMY RIVER" : "STAGE 1-1 · THE JUNGLE GATE";

        public static void Set(string id) => Id = id == "1-2" || id == "1-3" || id == "1-4" ? id : "1-1";
    }
}
