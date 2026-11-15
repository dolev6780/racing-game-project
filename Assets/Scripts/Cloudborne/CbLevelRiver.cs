using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Stage 1-4, The Stormy River (the story doc: riding, 900 m, 3 minutes - Raja on a log down the falls after the
    /// barge with Koda caged on it; rocks to jump, branches to duck, oil barrels to knock away with the staff, two
    /// waterfalls; three golden lotuses on the upper route; the mini-boss, the saw barge). One river the whole way, a
    /// level lower after each waterfall (he goes over and drops with it). Raja runs on a rolling log, carried along
    /// (CbGame.Riding: the stick only speeds him up or holds him back a little); the log stays under him wherever he
    /// is. At the end the river opens into a calm basin: the saw barge's fight (CbGame), then Koda's barge breaks up
    /// and a rope from an airship lifts his cage away (CbStory.RiverEnd).
    /// </summary>
    public partial class CbLevel
    {
        public const float RiverMax = 925f, RiverGoal = 918f;
        /// <summary>Where the two waterfalls are; each drops the river this far.</summary>
        public static readonly float[] Falls = { 300f, 620f };
        public const float FallDrop = 3f;
        /// <summary>The calm basin at the end, where the saw barge fights: its middle, and where riding ends.</summary>
        public const float BasinX = 900f, BasinFrom = 886f;
        /// <summary>How fast the river carries him, and how much the stick speeds him up or holds him back.</summary>
        public const float RideSpeed = 9f, RideLean = 2.5f;
        static readonly (float x0, float x1)[] RiverWaters = { (-60f, Falls[0]), (Falls[0], Falls[1]), (Falls[1], RiverMax + 60f) };

        /// <summary>The water's surface at x: stage 1-4's river steps down at each waterfall.</summary>
        public static float SurfaceAt(float x)
        {
            float s = WaterLevel;
            if (!CbStage.River) return s;
            foreach (var f in Falls) if (x >= f) s -= FallDrop;
            return s;
        }

        /// <summary>The top of Raja's log at x: what he rides on (0 above the first waterfall).</summary>
        public static float RiverFloor(float x) => SurfaceAt(x) - WaterLevel;

        // the way down: rocks to jump (their tops this far over the log), low branches to duck, oil barrels to smash
        static readonly (float x, float h)[] RiverRocks =
        {
            (40f, 1.0f), (78f, 1.2f), (116f, 0.9f), (150f, 1.25f), (236f, 1.1f), (270f, 1.0f), (346f, 1.2f), (378f, 0.95f),
            (408f, 1.3f), (520f, 1.1f), (560f, 1.0f), (594f, 1.2f), (656f, 1.0f), (702f, 1.3f), (744f, 1.1f), (792f, 1.2f), (838f, 1.0f),
        };
        static readonly float[] RiverBranches = { 60f, 98f, 134f, 252f, 330f, 392f, 484f, 540f, 682f, 726f, 772f, 816f, 862f };
        static readonly float[] RiverBarrels = { 52f, 88f, 125f, 166f, 244f, 286f, 362f, 418f, 500f, 576f, 640f, 712f, 762f, 806f, 852f };
        // the upper routes: steps of ledges up from the log (their tops over it), a golden lotus at the top of each
        static readonly (float x, float up)[][] UpperRoutes =
        {
            new[] { (180f, 1.8f), (187f, 3.1f), (194f, 4.2f), (201f, 3.4f) },
            new[] { (436f, 1.8f), (443f, 3.1f), (450f, 4.3f), (457f, 3.2f) },
            new[] { (728f, 1.8f), (735f, 3.0f), (742.5f, 4.4f), (750f, 3.3f) },
        };
        /// <summary>Where the golden lotuses are: over the top step of each upper route.</summary>
        public static Vector3 LotusAt(int i) { var r = UpperRoutes[i][2]; return new Vector3(r.x, RiverFloor(r.x) + r.up + 1.05f, 0); }
        static readonly float[] RiverCheckpoints = { 150f, 312f, 470f, 632f, 790f, BasinFrom };

        const string BargePath = "Cloudborne/River/SawBarge/SawBarge", KodaBargePath = "Cloudborne/River/KodaBarge/KodaBarge",
            BarrelPath = "Cloudborne/River/Barrel/Barrel";
        const float RockZ = 0.15f, BranchLow = 1.15f, BranchHigh = 1.75f, BarrelR = 0.45f;

        class Rock { public float x, h; }
        class Floater { public float x; public bool broken; public Transform t; public float bob; }
        readonly List<Rock> rocks = new List<Rock>();
        readonly List<float> branches = new List<float>();
        readonly List<Floater> floaters = new List<Floater>();
        Transform riverLog, kodaBarge;
        float logRoll, rainT, kodaY;
        int checkIdx;

        /// <summary>Test hooks: the river's obstacles.</summary>
        public float RockX(int i) => RiverRocks[i].x;
        public float BranchX(int i) => RiverBranches[i];
        public float BarrelX(int i) => RiverBarrels[i];
        public bool BarrelBroken(int i) => floaters[i].broken;
        public Transform RiverLog => riverLog;

        void BuildRiver()
        {
            AddEndGate(CbWorld.LevelMin - HalfWidth - 0.15f - EndGateHalf, 1);
            AddEndGate(CbWorld.LevelMax + HalfWidth + 0.15f + EndGateHalf, -1);
            var stone = M("#5d5f5a");
            foreach (var (x, h) in RiverRocks)
            {
                float floor = RiverFloor(x);
                // the jungle's river rock, from under the water up to its top (one grey egg was a stand-in)
                var t = CbArt.Joint(root, x, floor, RockZ, "riverRock");
                if (FitTop(RiverRockPath, t, new Vector3(-0.75f, WaterLevel - 0.6f, -0.7f), new Vector3(0.75f, h, 0.7f)) == null)
                    CbArt.Part(CbMesh.Sphere, stone, t, new Vector3(0, h * 0.4f, 0), new Vector3(1.3f, h * 1.6f, 1.2f));
                rocks.Add(new Rock { x = x, h = h });
            }
            var bark = M("#5b4232");
            var leaf = M("#3f7a3a");
            foreach (var x in RiverBranches)
            {
                // a low bough reaching out over the river from the far bank, leaves along it
                float floor = RiverFloor(x);
                var t = CbArt.Joint(root, x, floor + (BranchLow + BranchHigh) / 2, 0, "lowBranch");
                CbArt.Cylinder(bark, t, new Vector3(0, 0, 2.2f), 0.22f, 6.5f, Quaternion.Euler(90, 0, 0));
                for (int k = 0; k < 4; k++)
                    CbArt.Part(CbMesh.Sphere, leaf, t, new Vector3(Random.Range(-0.3f, 0.3f), 0.25f, -0.6f + k * 0.9f), new Vector3(0.8f, 0.45f, 0.7f), shadows: false);
                branches.Add(x);
            }
            foreach (var x in RiverBarrels)
            {
                var t = CbArt.Joint(root, x, RiverFloor(x) - 0.15f, 0, "oilBarrel");
                if (CbArt.Prop(BarrelPath, t, CbArt.PropOutline) == null)
                    CbArt.Cylinder(M("#7a4a2a"), t, new Vector3(0, 0.45f, 0), BarrelR, 0.95f);
                floaters.Add(new Floater { x = x, t = t, bob = Random.Range(0f, 6f) });
            }
            foreach (var route in UpperRoutes)
                foreach (var (x, up) in route) AddLedge(x, 3f, RiverFloor(x) + up, false);
            // his log: bark round it, pale ends
            riverLog = CbArt.Joint(root, 0, -0.35f, 0, "riverLog");
            var logBark = CbArt.NewToon(Color.white, CbArt.PropOutline, BarkTexture());
            logBark.mainTextureScale = new Vector2(2, 1.5f);
            CbArt.Cylinder(logBark, riverLog, Vector3.zero, 0.35f, 2.6f, Quaternion.Euler(0, 0, 90));
            foreach (var e in new[] { -1.31f, 1.31f })
                CbArt.Cylinder(M("#c9a473"), riverLog, new Vector3(e, 0, 0), 0.3f, 0.03f, Quaternion.Euler(0, 0, 90));
            // Koda's barge, ahead down the river
            kodaBarge = CbArt.Joint(root, 20f, WaterLevel, 3.2f, "kodaBarge");
            var kb = CbArt.Prop(KodaBargePath, kodaBarge, CbArt.PropOutline);
            if (kb != null) kb.localRotation = Quaternion.Euler(0, KodaBargeYaw, 0);
            else CbArt.Box(M("#6b4a2a"), kodaBarge, new Vector3(0, 0.4f, 0), new Vector3(5f, 0.8f, 1.8f));
            BuildFalls();
            BuildSawBarge();
        }

        // which way the barges' models face (their bows to the right, the way the river runs)
        const float KodaBargeYaw = 180f, SawBargeYaw = 180f;
        // how far back the river runs (CbWorld: to under the painting's rapids)
        const float FallsFar = 140f;

        /// <summary>The waterfalls: a cliff of wet rock from the upper river down to the lower, a sheet of white water over it.</summary>
        void BuildFalls()
        {
            var rock = M("#3e423f");
            var foam = CbArt.Glow(new Color(0.9f, 0.97f, 1f, 0.85f), CbArt.Shaft);
            var foamFlat = CbArt.Glow(new Color(0.9f, 0.97f, 1f, 0.5f), CbArt.Shaft);
            foreach (var f in Falls)
            {
                float top = SurfaceAt(f - 1f), bottom = SurfaceAt(f + 1f);
                // (right across the river, back to where its water ends under the painting; a hair under the upper
                // water's surface so the two don't flicker)
                CbArt.Box(rock, root, new Vector3(f - 0.5f, (top + bottom) / 2 - 0.62f, (FallsFar - 1f) / 2), new Vector3(1f, top - bottom + 1.2f, FallsFar + 1f));
                // the foam at its foot, all the way across
                var churn = CbArt.Part(CbMesh.Quad, foamFlat, root, new Vector3(f + 1f, bottom + 0.02f, (FallsFar - 1f) / 2), new Vector3(2f, FallsFar + 1f, 1), CbArt.Rot(Mathf.PI / 2), shadows: false);
                churn.name = "fallsFoam";
                var sheet = CbArt.Part(CbMesh.Quad, foam, root, new Vector3(f + 0.05f, (top + bottom) / 2, -0.4f), new Vector3(2.2f, top - bottom + 0.4f, 1), shadows: false);
                sheet.name = "waterfall";
            }
        }

        bool StrikeRiver(float x, float y, int dir, float range, bool staff)
        {
            bool any = false;
            foreach (var f in floaters)
            {
                if (f.broken || !InReach(f.x, x, dir, range) || y > RiverFloor(f.x) + 1.4f) continue;
                any = true;
                BreakBarrel(f, dir);
            }
            if (StrikeSawBarge(x, y, dir, range)) any = true;
            return any;
        }

        void BreakBarrel(Floater f, int dir)
        {
            f.broken = true;
            f.t.gameObject.SetActive(false);
            var at = new Vector3(f.x, RiverFloor(f.x) + 0.5f, -0.3f);
            CbFx.Spark(at, new Color(0.15f, 0.12f, 0.1f), 14, 5f, 0.35f, 0.6f, 8, dir * 2f);
            CbFx.Puff(at, new Color(0.55f, 0.45f, 0.35f), 8, 2f, 0.8f, 0.5f);
            for (int k = 0; k < 6; k++)
            {
                var stave = CbArt.Box(M("#7a4a2a"), g.transform, at + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.4f), 0), new Vector3(0.12f, 0.6f, 0.04f), Random.rotation);
                g.AddDebris(stave, new Vector3(dir * Random.Range(2f, 6f), Random.Range(3f, 6f), Random.Range(-1f, 1f)), Random.Range(-400f, 400f));
            }
            CbFx.Sfx("hit", 0.8f);
            g.score += 100;
            g.hud.Popup(at + Vector3.up * 1.5f, "100", CbHud.PopStyle.Normal);
        }

        void UpdateRiver(float dt, CbHero hero)
        {
            float floor = RiverFloor(hero.x);
            float time = g.clock;
            // his log, under him wherever he is, rolling as it is carried along
            float speed = g.Riding ? RideSpeed : Mathf.Abs(hero.vx);
            logRoll += speed * dt / 0.35f * Mathf.Rad2Deg;
            riverLog.localPosition = new Vector3(hero.x, floor - 0.32f + Mathf.Sin(time * 3.1f) * 0.04f, 0.05f);
            riverLog.localRotation = Quaternion.Euler(logRoll, 0, Mathf.Sin(time * 2.3f) * 2f);
            // the rocks, the branches, the barrels: run into them and they hurt him
            foreach (var r in rocks)
                if (Mathf.Abs(hero.x - r.x) < 0.65f && hero.y < RiverFloor(r.x) + r.h - 0.15f) hero.TakeHit(10, -1, knock: 4f);
            foreach (var b in branches)
            {
                float bf = RiverFloor(b);
                if (Mathf.Abs(hero.x - b) < 0.45f && hero.y + hero.Height > bf + BranchLow && hero.y < bf + BranchHigh) hero.TakeHit(8, -1);
            }
            foreach (var f in floaters)
            {
                if (f.broken) continue;
                f.t.localPosition = new Vector3(f.x, RiverFloor(f.x) - 0.15f + Mathf.Sin(time * 2.4f + f.bob) * 0.08f, 0);
                f.t.localRotation = Quaternion.Euler(Mathf.Sin(time * 1.7f + f.bob) * 8f, f.bob * 40f, Mathf.Sin(time * 2.1f + f.bob) * 6f);
                if (Mathf.Abs(hero.x - f.x) < BarrelR + 0.3f && hero.y < RiverFloor(f.x) + 0.9f)
                {
                    hero.TakeHit(8, -1);
                    BreakBarrel(f, 1);
                }
            }
            // checkpoints along the way
            while (checkIdx < RiverCheckpoints.Length && hero.x >= RiverCheckpoints[checkIdx]) g.Checkpoint(RiverCheckpoints[checkIdx++]);
            // Koda's barge, ahead: now in sight at the edge of the view, now gone on round the bend
            float bx = hero.x < BasinFrom ? hero.x + 15f + Mathf.Sin(time * 0.35f) * 5f : BasinX + 15f;
            if (sawSinking) bx = Mathf.Min(bx, BasinX + 15f);
            float targetY = SurfaceAt(bx) - 0.15f;
            kodaY = kodaY == 0 ? targetY : Mathf.MoveTowards(kodaY, targetY, dt * 6f);
            kodaBarge.localPosition = new Vector3(bx, kodaY + Mathf.Sin(time * 1.9f) * 0.07f, kodaBarge.localPosition.z);
            kodaBarge.localRotation = Quaternion.Euler(Mathf.Sin(time * 1.3f) * 2.5f, 0, Mathf.Sin(time * 1.7f) * 3f);
            // the storm: rain slanting down across the view, spray where it falls
            rainT -= dt;
            if (rainT <= 0)
            {
                rainT = 0.02f;
                var at = new Vector3(hero.x + Random.Range(-14f, 18f), floor + Random.Range(7f, 10f), Random.Range(-3f, 6f));
                CbFx.Spark(at, new Color(0.75f, 0.85f, 0.95f) * 1.1f, 1, 0.3f, 0.06f, 0.55f, 26f, -2f);
            }
            // mist at the foot of each waterfall
            foreach (var f in Falls)
                if (Mathf.Abs(hero.x - f) < 18f && Random.value < 0.3f)
                    CbFx.Puff(new Vector3(f + Random.Range(0f, 2f), SurfaceAt(f + 1f) + 0.2f, -0.5f), new Color(0.9f, 0.95f, 1f), 1, 1.5f, 1.2f, 0.8f);
            UpdateSawBarge(dt, hero);
        }

        void ResetRiver()
        {
            foreach (var f in floaters) { f.broken = false; f.t.gameObject.SetActive(true); }
            checkIdx = 0;
            kodaY = 0;
            ResetSawBarge();
        }
    }
}
