using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Where an ambush's raiders come from (the user: from both sides, out of a tunnel by each end of the bridge): a
    /// mound of the mountain's rock behind the path with a dark opening in it, a mine's timber frame round its mouth -
    /// in the treetops a hollow in a great trunk. The raiders step out of it (from behind the path, CbEnemy.emergeZ)
    /// and run at Raja (CbGame.SpawnIncoming).
    /// </summary>
    public partial class CbLevel
    {
        /// <summary>The tunnels' mouths: by each end of the rope bridge and of the stone bridge (1-1), either side of the
        /// big bough (1-2).</summary>
        public const float RopeMouthL = RopeBridgeX - RopeDeckHalf - 2.4f, RopeMouthR = RopeBridgeX + RopeDeckHalf + 2.4f;
        public const float StoneMouthL = StoneBridgeX - StoneBridgeHalf - 3.4f, StoneMouthR = StoneBridgeX + StoneBridgeHalf + 3.4f;
        public const float BoughMouthL = BoughAmbushX - 11f, BoughMouthR = BoughAmbushX + 12f;
        /// <summary>How far back the mouth stands (a raider steps forward from there to the path).</summary>
        public const float MouthZ = 1.5f;
        const float MouthWide = 1.5f, MouthHigh = 2.3f;

        void BuildTunnels()
        {
            if (CbStage.Jungle)
                foreach (var x in new[] { RopeMouthL, RopeMouthR, StoneMouthL, StoneMouthR }) AddTunnel(x, false);
            else if (CbStage.Treetops)
                foreach (var x in new[] { BoughMouthL, BoughMouthR }) AddTunnel(x, true);
        }

        void AddTunnel(float x, bool hollow)
        {
            var t = CbArt.Joint(root, x, 0, 0, hollow ? "hollow" : "tunnel");
            var dark = CbArt.Toon("#120e0c");
            if (hollow)
            {
                // a great trunk rising out of the bough, a dark hollow at its foot
                var bark = CbArt.NewToon(new Color(0.62f, 0.52f, 0.44f), CbArt.PropOutline, BarkTexture());
                bark.mainTextureScale = new Vector2(2f, 4f);
                CbArt.Cylinder(bark, t, new Vector3(0, 4f, MouthZ + 1.1f), 1.3f, 12f);
            }
            else if (!RockPieces(t, -2f, 2f, -0.3f, 3.4f, MouthZ - 0.2f, MouthZ + 2.4f, Mathf.RoundToInt(x * 10)))
                CbArt.Box(M("#6e6a5e"), t, new Vector3(0, 1.6f, MouthZ + 1.1f), new Vector3(4f, 3.4f, 2.4f));
            // the opening: dark, round-topped, its foot on the ground
            float front = MouthZ - (hollow ? 0.25f : 0.25f);
            CbArt.Part(CbMesh.Cube, dark, t, new Vector3(0, MouthHigh * 0.38f, front), new Vector3(MouthWide, MouthHigh * 0.76f, 0.05f), shadows: false);
            CbArt.Part(CbMesh.Sphere, dark, t, new Vector3(0, MouthHigh * 0.76f, front), new Vector3(MouthWide, MouthHigh * 0.48f, 0.05f), shadows: false);
            if (hollow) return;
            // a mine's frame round it: two posts and a beam over them
            foreach (int side in new[] { -1, 1 })
                CbArt.Box(Wood, t, new Vector3(side * (MouthWide / 2 + 0.1f), MouthHigh * 0.5f, front - 0.08f), new Vector3(0.2f, MouthHigh, 0.2f));
            CbArt.Box(Wood, t, new Vector3(0, MouthHigh + 0.05f, front - 0.08f), new Vector3(MouthWide + 0.7f, 0.22f, 0.24f));
        }
    }
}
