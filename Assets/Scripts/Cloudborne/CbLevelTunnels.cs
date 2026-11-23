using System.Collections.Generic;
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
        const float MouthWide = 2f, MouthHigh = 2.5f;
        /// <summary>How fast a tunnel's gate sinks or rises (a whole way in 1 / this seconds), and how long it stays down
        /// after the last raider was called out of it.</summary>
        const float TunnelGateSpeed = 2.6f, TunnelHold = 0.7f, TunnelGateTall = 2.9f;

        /// <summary>A tunnel and its gate: shut, so no one takes it for a way in (the user); it sinks into the ground
        /// while an ambush's raiders come out of it, and rises behind them.</summary>
        class Tunnel { public float x, open, hold, tall = TunnelGateTall; public bool hollow; public Transform gate; public Vector3 home; public Renderer[] rs; }
        readonly List<Tunnel> tunnels = new List<Tunnel>();

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
            // the kit's cave (drawn in Canva, approved, built by Meshy): a temple's arch in a mossy rock, ferns over it,
            // its opening dark; across it the kit's gate of bamboo bars, which sinks into the ground for an ambush
            if (!hollow && CaveArch(t, x)) return;
            var dark = CbArt.Toon("#120e0c");
            if (hollow)
            {
                // a great trunk rising out of the bough, a dark hollow at its foot
                var bark = CbArt.NewToon(new Color(0.62f, 0.52f, 0.44f), CbArt.PropOutline, BarkTexture());
                bark.mainTextureScale = new Vector2(2f, 4f);
                CbArt.Cylinder(bark, t, new Vector3(0, 4f, MouthZ + 1.2f), 1.5f, 12f);
            }
            else if (!RockPieces(t, -2.7f, 2.7f, -0.3f, 4f, MouthZ - 0.2f, MouthZ + 2.4f, Mathf.RoundToInt(x * 10)))
                CbArt.Box(M("#6e6a5e"), t, new Vector3(0, 1.85f, MouthZ + 1.1f), new Vector3(5.4f, 4f, 2.4f));
            // the opening: dark, round-topped, its foot on the ground
            float front = MouthZ - 0.25f;
            CbArt.Part(CbMesh.Cube, dark, t, new Vector3(0, MouthHigh * 0.38f, front), new Vector3(MouthWide, MouthHigh * 0.76f, 0.05f), shadows: false);
            CbArt.Part(CbMesh.Sphere, dark, t, new Vector3(0, MouthHigh * 0.76f, front), new Vector3(MouthWide, MouthHigh * 0.48f, 0.05f), shadows: false);
            // vines hanging over it from the rock (or the trunk), leaves along them
            var leaf = CbArt.Toon("#4f8a3c", CbArt.PropOutline);
            foreach (var (vx, len) in new[] { (-1.25f, 1.5f), (-0.7f, 0.8f), (0.55f, 1.1f), (1.3f, 1.8f) })
            {
                float topY = TunnelGateTall + 0.5f;
                CbArt.Cylinder(VineGreen, t, new Vector3(vx, topY - len / 2, front - 0.45f), 0.03f, len);
                for (float ly = topY - 0.2f; ly > topY - len; ly -= 0.35f)
                    CbArt.Part(CbMesh.Blob(6, 4), leaf, t, new Vector3(vx + (ly * 7 % 2 > 1 ? 0.09f : -0.09f), ly, front - 0.47f),
                        new Vector3(0.2f, 0.12f, 0.06f), Quaternion.Euler(0, 0, ly * 40f), shadows: false);
            }
            // moss over the top of the opening
            CbArt.Part(CbMesh.Blob(10, 6), CbArt.Toon("#5d8f3a", CbArt.PropOutline), t, new Vector3(0, TunnelGateTall + 0.35f, front - 0.3f),
                new Vector3(3.2f, 0.45f, 0.6f), Quaternion.identity, shadows: false);
            // the gate: the same stone posts and bamboo bars as the temple's (one look with the stage), shut across it
            var gate = CbArt.Joint(t, 0, 0, front - 0.25f, "tunnelGate");
            if (Model("Gate", gate, new Vector3(2.75f, TunnelGateTall, 0.8f), 180f) == null)
            {
                for (float bx = -0.95f; bx <= 0.96f; bx += 0.27f)
                    CbArt.Cylinder(Bamboo, gate, new Vector3(bx, TunnelGateTall / 2, 0), 0.07f, TunnelGateTall);
                CbArt.Box(Wood, gate, new Vector3(0, TunnelGateTall - 0.3f, 0), new Vector3(2.3f, 0.18f, 0.18f));
                CbArt.Box(Wood, gate, new Vector3(0, 0.6f, 0), new Vector3(2.3f, 0.18f, 0.18f));
                foreach (int side in new[] { -1, 1 })
                    CbArt.Box(StoneGrey, gate, new Vector3(side * 1.2f, TunnelGateTall / 2, 0), new Vector3(0.4f, TunnelGateTall, 0.5f));
            }
            tunnels.Add(new Tunnel { x = x, hollow = hollow, gate = gate, home = gate.localPosition, rs = gate.GetComponentsInChildren<Renderer>() });
        }

        // the arch as parts of its height: as tall as CaveTall, its width and depth; its opening CaveDoorWide wide and
        // CaveDoorHigh high in the middle, its face CaveFace in front of its middle
        const float CaveTall = 4.3f, CaveWide = 0.973f, CaveDeep = 0.294f, CaveDoorWide = 0.42f, CaveDoorHigh = 0.62f, CaveFace = 0.05f;

        bool CaveArch(Transform t, float x)
        {
            var arch = CbArt.Joint(t, 0, -0.05f, MouthZ, "caveArch");
            if (Model("CaveArch", arch, new Vector3(CaveWide * CaveTall, CaveTall, CaveDeep * CaveTall), 180f) == null) { Object.Destroy(arch.gameObject); return false; }
            float doorW = CaveDoorWide * CaveTall, doorH = CaveDoorHigh * CaveTall;
            var gate = CbArt.Joint(t, 0, 0, MouthZ - CaveFace * CaveTall + 0.02f, "tunnelGate");
            if (Model("CaveGate", gate, new Vector3(doorW, doorH, 0.18f), 180f) == null)
                for (float bx = -doorW / 2 + 0.15f; bx <= doorW / 2 - 0.1f; bx += 0.3f)
                    CbArt.Cylinder(Bamboo, gate, new Vector3(bx, doorH / 2, 0), 0.07f, doorH);
            tunnels.Add(new Tunnel { x = x, gate = gate, home = gate.localPosition, rs = gate.GetComponentsInChildren<Renderer>(), tall = doorH });
            return true;
        }

        Tunnel TunnelNear(float x)
        {
            foreach (var tn in tunnels) if (Mathf.Abs(tn.x - x) < 1.5f) return tn;
            return null;
        }

        /// <summary>A raider is about to come out of the tunnel at x (or is coming out): its gate sinks, and stays down a
        /// moment after the last call.</summary>
        public void CallTunnel(float x)
        {
            var tn = TunnelNear(x);
            if (tn != null) tn.hold = TunnelHold;
        }

        /// <summary>How far the gate of the tunnel at x is down: 0 shut, 1 all the way open (1 where there is no tunnel).</summary>
        public float TunnelOpen(float x) => TunnelNear(x)?.open ?? 1f;

        void UpdateTunnels(float dt)
        {
            foreach (var tn in tunnels)
            {
                tn.hold = Mathf.Max(0, tn.hold - dt);
                float was = tn.open;
                tn.open = Mathf.MoveTowards(tn.open, tn.hold > 0 ? 1 : 0, dt * TunnelGateSpeed);
                if (tn.open == was) continue;
                ShowTunnelGate(tn);
                // a rumble of stone as it starts, dust along its foot
                if ((was == 0 || was == 1) && tn.open != was) CbFx.Sfx("block", 0.5f);
                if (Random.value < 0.5f)
                    CbFx.Puff(root.TransformPoint(new Vector3(tn.x + Random.Range(-1.2f, 1.2f), 0.15f, tn.home.z - 0.3f)), new Color(0.75f, 0.68f, 0.58f), 1, 1f, 0.7f, 0.5f);
            }
        }

        /// <summary>The gate where its tunnel has it: in the jungle it sinks into the ground; on the bough (air under it)
        /// it folds down into the wood instead of poking out below. All the way down it is hidden.</summary>
        void ShowTunnelGate(Tunnel tn)
        {
            float k = tn.open * tn.open * (3 - 2 * tn.open);
            if (tn.hollow) tn.gate.localScale = new Vector3(1, Mathf.Max(0.02f, 1 - k), 1);
            else tn.gate.localPosition = tn.home + Vector3.down * k * (tn.tall + 0.1f);
            bool show = tn.open < 0.999f;
            foreach (var r in tn.rs) if (r != null) r.enabled = show;
        }

        void ResetTunnels()
        {
            foreach (var tn in tunnels) { tn.open = tn.hold = 0; tn.gate.localScale = Vector3.one; ShowTunnelGate(tn); }
        }
    }
}
