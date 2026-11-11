using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The climb, rebuilt as a gorge with water rising in it (the old climb was terraces and ledges with nothing pressing
    /// him on - "just climbing, no challenge", its rocks dull and its way muddled: the user).
    /// A cliff closes the path with a low gap at its foot: Raja slides or crawls through, a boulder drops into the gap
    /// behind him and water bursts in. Up the gorge a zigzag of ledges - right, back left, right again, some crumbling -
    /// to a rock jutting from the back wall and the summit's cliff, the way out at its top. Stones tumble from the rim
    /// in two places, and the water rises behind him, faster while he is far ahead of it. Caught by it he loses health
    /// and starts again at the bottom, the water drained. Off the way, high in the corner over the cliff, the golden
    /// fruit; from the summit, two ledges up to the scroll; ledges down the far side.
    /// </summary>
    public partial class CbLevel
    {
        // the cliff with the gap (MountainFrom to GorgeFrom), the gorge between it and the summit's cliff, the rim
        public const float GorgeFrom = 139.4f, GorgeTo = 151f, GorgeRim = 13f, SummitTo = 158f;
        public const float GateSlot = 0.95f;
        // the summit cliff's shoulder, the first rock of the climb on the gorge's right: caught from the floor at a jump
        public const float ShoulderX = 148.4f, ShoulderTop = 4f;
        // the water: its foot (under the gorge's floor), as high as it comes, how fast it rises (faster while he is
        // more than FloodLead above it), how long after the boulder drops it starts, how far over his feet catches him
        public const float FloodFloor = -0.4f, FloodTop = 9.0f, FloodRise = 0.38f, FloodChase = 1.1f, FloodLead = 4.5f, FloodCatch = 0.9f;
        const float FloodWait = 1.6f, FloodDrain = 1.6f;
        // the water's front, a little in front of everything in the gorge, and the gorge's back wall
        const float FloodFront = -1.15f, GorgeBack = 2.4f;
        // stones from the rim: where (clear of the ledges), how often, the warning before one falls, how hard it falls
        static readonly float[] DropColumns = { 143.1f, 146.3f, 150.3f };
        const float DropEvery = 2.8f, DropWarn = 0.8f, DropGravity = 22f, DropSize = 0.5f, DropDamage = 8f;

        enum Gorge { Open, Flooding, Done }
        Gorge gorge;
        bool floodCaught;
        float floodWait;
        /// <summary>The water's surface in the gorge (world height).</summary>
        public float FloodLevel { get; private set; } = FloodFloor;
        public bool Flooding => gorge == Gorge.Flooding;
        Block gate;
        Transform gateBoulder;
        float sealT = -1f;
        Material floodMat;
        Transform floodFrontQuad, floodSurface;

        class Dropper
        {
            public float x, timer, y, vy;
            public bool falling;
            public Transform rock;
        }
        readonly List<Dropper> droppers = new List<Dropper>();

        static bool InGorge(float x) => x > GorgeFrom && x < GorgeTo;

        /// <summary>The gorge and the summit beyond it (Build calls this where the mountain was).</summary>
        void BuildMountain()
        {
            earth ??= EarthMat();
            grass ??= GrassMat();
            crackedEarth = new Material(earth) { color = earth.color * new Color(1.15f, 1.02f, 0.85f) };

            // the cliff across the path, the gap at its foot
            var gateT = CbArt.Joint(root, (MountainFrom + GorgeFrom) / 2, 0, 0, "gorgeGate");
            float half = (GorgeFrom - MountainFrom) / 2;
            if (!RockWall(gateT, -half, half, GateSlot, GorgeRim, -0.9f, GorgeBack, 1370, 0f))
                CbArt.Box(StoneGrey, gateT, new Vector3(0, (GateSlot + GorgeRim) / 2, (GorgeBack - 0.9f) / 2), new Vector3(half * 2, GorgeRim - GateSlot, GorgeBack + 0.9f));
            // the gap is a low tunnel through it: dark at its back, a stone either side of its mouth
            CbArt.Box(CbArt.Toon("#1d1a17"), gateT, new Vector3(0, GateSlot / 2, 1.6f), new Vector3(half * 2, GateSlot + 0.1f, 1.6f));
            foreach (float side in new[] { -1f, 1f })
            {
                var r = CbArt.Joint(gateT, side * (half - 0.15f), 0, -0.75f, "mouthStone");
                Fit(RiverStonePath, r, new Vector3(-0.4f, 0, -0.3f), new Vector3(0.4f, GateSlot + 0.1f, 0.3f));
            }
            gate = new Block { x0 = MountainFrom, x1 = GorgeFrom, top = GorgeRim, bottom = GateSlot, t = gateT, smooth = true };
            blocks.Add(gate);
            // the boulder that drops into the gap behind him (hidden till then)
            gateBoulder = CbArt.Joint(gateT, 0, GorgeRim, -0.45f, "sealBoulder");
            if (Fit("Cloudborne/Jungle/Boulder/Boulder", gateBoulder, new Vector3(-half - 0.1f, 0, -0.5f), new Vector3(half + 0.1f, GateSlot + 0.35f, 0.9f)) == null)
                CbArt.Box(StoneGrey, gateBoulder, new Vector3(0, GateSlot / 2, 0.2f), new Vector3(half * 2, GateSlot + 0.3f, 1.4f));
            gateBoulder.gameObject.SetActive(false);

            // the gorge's back wall, darker, so the ledges and rocks stand out against it
            var back = CbArt.Joint(root, 0, 0, 0, "gorgeBack");
            RockWall(back, MountainFrom, SummitTo, -0.3f, GorgeRim + 0.5f, GorgeBack, GorgeBack + 1.8f, 1390, 0.35f, 0.62f, 4.4f);

            // up the gorge: right, back left, right again (ledges, a shoulder of the summit's cliff, a rock from the back wall)
            AddLedge(141.9f, 1.8f, 2.0f, false);
            AddLedge(145.4f, 1.3f, 3.3f, true);
            AddCliff(ShoulderX, GorgeTo, ShoulderTop, 1484);
            AddLedge(145.1f, 1.4f, 5.4f, false);
            AddLedge(142.2f, 1.2f, 6.7f, true);
            AddLedge(140.2f, 1.4f, 8.0f, false);
            AddLedge(144.0f, 1.2f, 9.4f, true);
            AddJut(146.8f, 148.6f, 8.8f, 10.4f, 1468);
            // the summit's cliff: its top the way out
            AddCliff(GorgeTo, SummitTo, SummitTop, 1510);
            // off the way, in the corner over the cliff with the gap: the golden fruit's ledge
            AddLedge(140.1f, 1.0f, 11.2f, false);
            // from the summit, two ledges up to the scroll
            AddLedge(156.6f, 1.3f, 13.4f, false);
            AddLedge(153.6f, 1.4f, 15.4f, false);
            // down the far side
            AddLedge(160.4f, 1.4f, 8.6f, false);
            AddLedge(163.2f, 1.4f, 5.8f, true);
            AddLedge(166.0f, 1.4f, 3.0f, false);

            // the water (see-through front with rolling waves, its painted surface over it), drawn as it rises
            var frontShader = Resources.Load<Shader>("Shaders/WaterFront");
            if (frontShader != null && frontShader.isSupported)
            {
                floodMat = new Material(frontShader) { name = "floodFront", renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent };
                floodFrontQuad = CbArt.Part(CbMesh.Quad, floodMat, root, new Vector3((GorgeFrom + GorgeTo) / 2, 0, FloodFront), Vector3.one, shadows: false);
                floodFrontQuad.name = "floodFront";
            }
            var surface = CbWorld.WaterSurface(GorgeTo - GorgeFrom, GorgeBack - FloodFront);
            if (surface == null) { surface = CbArt.NewToon(new Color(0.35f, 0.72f, 0.92f), 0f); }
            floodSurface = CbArt.Part(CbMesh.Quad, surface, root, new Vector3((GorgeFrom + GorgeTo) / 2, 0, (FloodFront + GorgeBack) / 2),
                new Vector3(GorgeTo - GorgeFrom, GorgeBack - FloodFront, 1), CbArt.Rot(Mathf.PI / 2), shadows: false);
            floodSurface.name = "floodSurface";

            // the stones that tumble from the rim
            foreach (var x in DropColumns)
            {
                var rock = CbArt.Joint(root, x, GorgeRim, -0.2f, "fallingStone");
                var holder = CbArt.Joint(rock, 0, 0, 0, "spin");
                if (Fit("Cloudborne/Jungle/Boulder/Boulder", holder, new Vector3(-DropSize / 2, -DropSize / 2, -DropSize / 2), new Vector3(DropSize / 2, DropSize / 2, DropSize / 2)) == null)
                    CbArt.Box(StoneGrey, holder, Vector3.zero, Vector3.one * DropSize);
                rock.gameObject.SetActive(false);
                droppers.Add(new Dropper { x = x, timer = DropEvery * (droppers.Count + 1) * 0.5f, rock = rock });
            }
            ShowFlood();
        }

        /// <summary>
        /// A wall of rock from y0 to y1 between x0 and x1, zFront to zBack deep: the cliff block in pieces about cell
        /// metres wide, in rows, overlapping, mirrored this way and that, pushed back up to jitter metres at random,
        /// shade times as bright. False without the model.
        /// </summary>
        bool RockWall(Transform parent, float x0, float x1, float y0, float y1, float zFront, float zBack, int seed, float jitter, float shade = 1f, float cell = 3.2f)
        {
            const float Aspect = 0.85f, Overlap = 0.3f;
            float w = x1 - x0, h = y1 - y0;
            int cols = Mathf.Max(1, Mathf.RoundToInt(w / cell));
            float cw = w / cols;
            int rows = Mathf.Max(1, Mathf.RoundToInt(h / (cw / Aspect)));
            float rh = h / rows;
            var rng = new System.Random(seed);
            Material tone = null;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    float a = x0 + c * cw - (c > 0 ? Overlap : 0), b = x0 + (c + 1) * cw + (c < cols - 1 ? Overlap : 0);
                    float lo = y0 + r * rh - (r > 0 ? Overlap : 0), hi = y0 + (r + 1) * rh + (r == rows - 1 ? 0.06f : Overlap);
                    float dz = (float)rng.NextDouble() * jitter;
                    var piece = CbArt.Joint(parent, 0, 0, 0, "piece");
                    if (Fit(CliffRockPath, piece, new Vector3(a, lo, zFront + dz), new Vector3(b, hi, zBack)) == null)
                    {
                        Object.Destroy(piece.gameObject);
                        return false;
                    }
                    if (rng.NextDouble() < 0.5)
                    {
                        piece.localScale = new Vector3(-1, 1, 1);
                        piece.localPosition = new Vector3(a + b, 0, 0);
                    }
                    if (shade < 1f)
                        foreach (var rend in piece.GetComponentsInChildren<Renderer>())
                        {
                            tone ??= new Material(rend.sharedMaterial) { color = NaturalRockTone * shade };
                            rend.sharedMaterial = tone;
                        }
                }
            return true;
        }

        /// <summary>A cliff of the gorge: solid rock from the ground up to top, as deep as the gorge.</summary>
        void AddCliff(float x0, float x1, float top, int seed)
        {
            float cx = (x0 + x1) / 2;
            var t = CbArt.Joint(root, cx, 0, 0, "cliff");
            if (!RockWall(t, x0 - cx, x1 - cx, -0.3f, top, -0.9f, GorgeBack, seed, 0f))
                RockFormation(t, x0 - cx, x1 - cx, -0.3f, top, -0.9f, GorgeBack, seed);
            blocks.Add(new Block { x0 = x0, x1 = x1, top = top, t = t });
        }

        /// <summary>A rock jutting from the gorge's back wall, from bottom to top: stood on, jumped off, bumped from below.</summary>
        void AddJut(float x0, float x1, float bottom, float top, int seed)
        {
            float cx = (x0 + x1) / 2;
            var t = CbArt.Joint(root, cx, 0, 0, "jut");
            if (!RockWall(t, x0 - cx, x1 - cx, bottom, top, -0.9f, GorgeBack, seed, 0f))
                CbArt.Box(StoneGrey, t, new Vector3(0, (bottom + top) / 2, (GorgeBack - 0.9f) / 2), new Vector3(x1 - x0, top - bottom, GorgeBack + 0.9f));
            blocks.Add(new Block { x0 = x0, x1 = x1, top = top, bottom = bottom, t = t });
        }

        /// <summary>
        /// Test hook (the stage's pictures): the gorge sealed and its water already at level, rising from there.
        /// </summary>
        public void StartFlood(float level)
        {
            Seal();
            FloodLevel = level;
            floodWait = 0;
            ShowFlood();
        }

        /// <summary>The boulder drops into the gap behind him; the water bursts in.</summary>
        void Seal()
        {
            gorge = Gorge.Flooding;
            gate.bottom = 0;
            sealT = 0;
            gateBoulder.gameObject.SetActive(true);
            FloodLevel = FloodFloor;
            floodWait = FloodWait;
            floodCaught = false;
            foreach (var d in droppers) { d.timer = DropEvery * 0.5f + (d.x - DropColumns[0]) * 0.4f; d.falling = false; d.rock.gameObject.SetActive(false); }
        }

        /// <summary>Back as built: the gap open, the gorge dry (Reset, and when he comes back at a lantern before it).</summary>
        void ResetGorge()
        {
            gorge = Gorge.Open;
            floodCaught = false;
            FloodLevel = FloodFloor;
            if (gate != null) gate.bottom = GateSlot;
            sealT = -1f;
            if (gateBoulder != null) gateBoulder.gameObject.SetActive(false);
            foreach (var d in droppers) { d.falling = false; d.rock.gameObject.SetActive(false); }
            ShowFlood();
        }

        /// <summary>He came back at the lantern at x: if that is before the gorge, it is as it was.</summary>
        public void Respawned(float x)
        {
            if (x < MountainFrom) ResetGorge();
        }

        void UpdateGorge(float dt, CbHero hero)
        {
            // the boulder dropping into the gap, with a thud, dust and a shake
            if (sealT >= 0 && sealT < 1f)
            {
                float before = sealT;
                sealT += dt / 0.35f;
                float k = Mathf.Clamp01(sealT);
                gateBoulder.localPosition = new Vector3(0, Mathf.Lerp(GorgeRim * 0.4f, 0, k * k), -0.45f);
                if (before < 1f && sealT >= 1f)
                {
                    float gx = (MountainFrom + GorgeFrom) / 2;
                    CbFx.Puff(new Vector3(gx + 0.8f, 0.4f, -0.6f), new Color(0.78f, 0.74f, 0.66f), 10, 2.6f, 1.2f, 0.8f);
                    CbFx.Sfx("heavy", 0.9f);
                    g.shake = Mathf.Max(g.shake, 0.5f);
                    g.hud.SetTip("Trapped! The water is rising - <color=#ffb03a>climb!</color>");
                }
            }
            switch (gorge)
            {
                case Gorge.Open:
                    // through the gap and onto the gorge's floor: the trap is sprung
                    if (hero.x > GorgeFrom + 0.8f && hero.x < GorgeTo && hero.y < 0.5f && hero.state != CbHero.State.Dead) Seal();
                    break;
                case Gorge.Flooding:
                    // out at the top: the water drains away
                    if ((hero.x > GorgeTo && hero.y > SummitTop - 0.1f) || hero.x > SummitTo)
                    {
                        gorge = Gorge.Done;
                        foreach (var d in droppers) { d.falling = false; d.rock.gameObject.SetActive(false); }
                        if (hero.x < SummitTo) g.hud.SetTip("Made it out! The water drains away");
                        break;
                    }
                    if (floodCaught)
                    {
                        // treading water a moment; once he is back at the bottom the water is too
                        if (hero.state != CbHero.State.Swim)
                        {
                            floodCaught = false;
                            FloodLevel = FloodFloor;
                            floodWait = FloodWait;
                            foreach (var l in ledges) if (InGorge((l.x0 + l.x1) / 2)) { l.gone = false; l.stood = 0; l.t.localPosition = l.home; l.t.gameObject.SetActive(true); }
                        }
                        break;
                    }
                    if (floodWait > 0)
                    {
                        floodWait -= dt;
                        // bursting in through the cracks
                        if (Random.value < 0.5f)
                            CbFx.Spark(new Vector3(GorgeFrom + 0.2f, Random.Range(0.2f, 1.6f), Random.Range(-0.6f, 0.6f)), CbFx.Water, 2, 4f, 0.3f, 0.5f, 8, 1f);
                    }
                    else FloodLevel = Mathf.Min(FloodTop, FloodLevel + dt * (hero.y - FloodLevel > FloodLead ? FloodChase : FloodRise));
                    if (InGorge(hero.x) && hero.y + FloodCatch < FloodLevel && hero.state != CbHero.State.Swim && hero.state != CbHero.State.Dead)
                    {
                        floodCaught = true;
                        hero.CaughtByFlood(FloodLevel, GorgeFrom + 1.4f);
                    }
                    UpdateDroppers(dt, hero);
                    break;
                case Gorge.Done:
                    FloodLevel = Mathf.Max(FloodFloor, FloodLevel - dt * FloodDrain);
                    break;
            }
            ShowFlood();
        }

        /// <summary>Stones tumbling from the rim: a trickle of grit first, then the stone; it bursts on what it hits.</summary>
        void UpdateDroppers(float dt, CbHero hero)
        {
            foreach (var d in droppers)
            {
                if (!d.falling)
                {
                    d.timer -= dt;
                    // grit trickling down where it will fall, at his height so he sees it
                    if (d.timer < DropWarn && Random.value < 0.35f)
                        CbFx.Spark(new Vector3(d.x + Random.Range(-0.2f, 0.2f), Mathf.Min(GorgeRim, hero.y + 4f), -0.2f), CbFx.Dust, 1, 0.5f, 0.18f, 0.6f, 14);
                    if (d.timer <= 0)
                    {
                        d.falling = true;
                        d.y = GorgeRim;
                        d.vy = 0;
                        d.rock.gameObject.SetActive(true);
                        d.timer = DropEvery;
                    }
                    continue;
                }
                float prevY = d.y;
                d.vy -= DropGravity * dt;
                d.y += d.vy * dt;
                d.rock.localPosition = new Vector3(d.x, d.y, -0.2f);
                d.rock.GetChild(0).localRotation = Quaternion.Euler(0, 0, d.y * 70f);
                bool burst = false;
                // on him
                if (Mathf.Abs(hero.x - d.x) < DropSize * 0.5f + HalfWidth && d.y - DropSize * 0.5f < hero.y + 1.6f && d.y + DropSize * 0.5f > hero.y)
                {
                    hero.TakeHit(DropDamage, hero.x < d.x ? -1 : 1, unblockable: true, knock: 3f);
                    burst = true;
                }
                // on a ledge, a rock, the floor; into the water
                float under = SupportAt(d.x, prevY - DropSize * 0.5f);
                if (d.y - DropSize * 0.5f <= under) burst = true;
                if (d.y < FloodLevel)
                {
                    CbFx.Spark(new Vector3(d.x, FloodLevel + 0.1f, -0.3f), CbFx.Water, 12, 5, 0.35f, 0.5f, 12);
                    CbFx.Sfx("splash", 0.4f);
                    d.falling = false;
                    d.rock.gameObject.SetActive(false);
                    continue;
                }
                if (burst)
                {
                    CbFx.Puff(new Vector3(d.x, d.y, -0.4f), new Color(0.78f, 0.74f, 0.66f), 5, 1.8f, 0.6f, 0.45f);
                    CbFx.Spark(new Vector3(d.x, d.y, -0.4f), CbFx.Grey, 8, 4, 0.25f, 0.4f, 10);
                    CbFx.Sfx("block", 0.5f);
                    d.falling = false;
                    d.rock.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>The water's front and surface where its level is now; out of sight under the floor.</summary>
        void ShowFlood()
        {
            bool shown = FloodLevel > 0.02f;
            float top = FloodLevel + 0.25f, bottom = -0.6f;
            if (floodFrontQuad != null)
            {
                floodFrontQuad.gameObject.SetActive(shown);
                floodFrontQuad.localPosition = new Vector3((GorgeFrom + GorgeTo) / 2, (top + bottom) / 2, FloodFront);
                floodFrontQuad.localScale = new Vector3(GorgeTo - GorgeFrom, top - bottom, 1);
                floodMat.SetFloat("_Level", FloodLevel);
            }
            if (floodSurface != null)
            {
                floodSurface.gameObject.SetActive(shown);
                var p = floodSurface.localPosition;
                floodSurface.localPosition = new Vector3(p.x, FloodLevel, p.z);
            }
        }
    }
}
