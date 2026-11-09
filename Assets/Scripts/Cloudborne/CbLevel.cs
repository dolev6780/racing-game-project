using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The shape of the stage beyond its flat path: things to climb, things in the way, and water.
    /// <list type="bullet">
    /// <item>Blocks: solid ground Raja can't walk through but can stand on: the cracked wall, the mountain's
    /// terraces, a rock in the river, the stone arch bridge.</item>
    /// <item>Ledges: floating slabs he jumps up through and lands on; a crumbling one gives way under him.</item>
    /// <item>Water: streams cut into the ground across the path (CbWorld carves their channels). Falling in puts Raja
    /// back on the last safe footing he stood on and costs health. What crosses them: a rope bridge (a deck walked onto
    /// from the bank, sagging in the middle), stepping stones (some sink when stood on), floating logs (some drift
    /// to and fro, carrying him), a rock, and a stone arch bridge.</item>
    /// <item>Falling rocks on the mountain, a dust trail giving each away before it drops.</item>
    /// </list>
    /// Stage 1-1, about 265 m: the village; the first fight; a stream under a rope bridge; the second fight; the
    /// cracked wall (only the staff breaks it); a long river of stones and logs; the ambush at the shrine; a
    /// mountain climbed by terraces and ledges, crumbling ones and falling rocks, to the golden fruit on its
    /// summit (and a hidden path to the scroll above); the fight on the ridge; a brook under a stone arch bridge;
    /// Sergeant Snap at the shrine gate.
    /// </summary>
    public class CbLevel
    {
        class Block
        {
            public float x0, x1, top;
            public bool breakable, broken;
            public int hp;
            public Transform t;
        }

        class Ledge
        {
            public float x0, x1, top;
            public bool crumbles, gone;
            // a hidden ledge shows (and holds him) only once Raja has climbed up near it; then it stays
            public bool hidden, found;
            public float stood, respawn, reveal, glint;
            public Transform t;
            public Vector3 home;
        }

        /// <summary>Something over the water to stand on: a bridge's deck, a stepping stone or a log.</summary>
        class Crossing
        {
            public float cx, half, top, sag, dip, bob, stood, dx, sunk, shake;
            // drifting logs move to and fro by drift metres every period seconds
            public float drift, period;
            // a deck is stepped onto from the bank; the rest only landed on from above
            public bool deck, sinks, bobs;
            // a sinking stone: 0 resting, 1 going down, 2 under, 3 coming back up
            public int phase;
            public float timer;
            public Transform t;
            public Vector3 home;
            public float X0 => cx + dx - half;
            public float X1 => cx + dx + half;
            public bool Safe => deck || (!sinks && !bobs && drift == 0);
            public float Top(float x)
            {
                float u = Mathf.Clamp01((x - X0) / Mathf.Max(0.01f, 2 * half));
                return top - sag * Mathf.Sin(u * Mathf.PI) + bob - dip;
            }
        }

        /// <summary>A rock that drops on the mountain path every so often, a trickle of dust warning of it.</summary>
        class FallingRock
        {
            public float x, period, timer, y, vy;
            public bool falling;
            public Transform t;
        }

        public const float HalfWidth = 0.35f;
        // the cracked wall: taller than a double jump, two staff blows break it
        public const float WallX = 76f, WallHeight = 4.6f;
        const float CrumbleAfter = 0.5f, CrumbleBack = 3f;

        /// <summary>The streams across stage 1-1 (x from, to): the rope bridge's, the long river, the brook.</summary>
        public static readonly (float x0, float x1)[] Waters = { (31.4f, 36.6f), (79f, 103f), (222.2f, 224.6f) };
        /// <summary>The water's surface, below the banks: its channel is cut into the ground.</summary>
        public const float WaterLevel = -0.45f;
        /// <summary>How far back from the path the streams run before they come out of the ground.</summary>
        public const float WaterBack = 6.5f;
        // below the path where the water is: deep enough that falling in is falling in
        const float Deep = -50f, StepUp = 0.3f;
        const float RopeBridgeX = 34f, RopeDeckHalf = 3f, StoneBridgeX = 223.4f;
        // the mountain: where it stands, its summit's height
        public const float MountainFrom = 137f, MountainTo = 189f, SummitTop = 11.4f;
        // falling rocks: how hard they hit, how long the dust warns before one drops
        const float RockDamage = 8f, RockWarn = 0.9f;

        readonly CbGame g;
        readonly Transform root;
        readonly List<Block> blocks = new List<Block>();
        readonly List<Ledge> ledges = new List<Ledge>();
        readonly List<Crossing> crossings = new List<Crossing>();
        readonly List<FallingRock> rocks = new List<FallingRock>();
        Material earth, grass, crackedEarth;

        public bool WallStanding => blocks.Exists(b => b.breakable && !b.broken);

        public CbLevel(CbGame game, Transform parent)
        {
            g = game;
            root = new GameObject("Level").transform;
            root.SetParent(parent, false);
            Build();
        }

        /// <summary>Whether x is over one of the streams (margin widens them, for keeping scenery off the water).</summary>
        public static bool InWater(float x, float margin = 0f)
        {
            foreach (var (x0, x1) in Waters)
                if (x > x0 - margin && x < x1 + margin) return true;
            return false;
        }

        void Build()
        {
            // the cracked wall across the path
            AddBlock(WallX, 1.3f, WallHeight, breakable: true);
            BuildCrossings();
            BuildMountain();
            // the end of the stage: a shrine gate on the path to walk through
            var goal = CbArt.Joint(root, CbWorld.GoalX, 0, 0.8f, "goalGate");
            if (CbArt.Prop("Cloudborne/Jungle/Gate/Gate", goal, CbArt.PropOutline) is Transform gate) gate.localRotation = Quaternion.Euler(0, 180, 0) * gate.localRotation;
        }

        // ---------- the water ----------

        void BuildCrossings()
        {
            // the rope bridge over the first stream: its planks just above the banks at the ends, sagging in the middle
            var rope = PlaceProp("Cloudborne/Jungle/RopeBridge/RopeBridge", new Vector3(RopeBridgeX, -0.25f, 0.15f), Vector3.one, 0);
            crossings.Add(new Crossing { cx = RopeBridgeX, half = RopeDeckHalf, top = 0.145f, sag = 0.29f, deck = true, t = rope,
                home = rope != null ? rope.localPosition : Vector3.zero });

            // the long river past the cracked wall: stones, some sinking under him; logs, some drifting; a rock
            AddStone(80.6f, false);
            AddLog(83.6f, 1.0f, 4.2f);
            AddStone(86.6f, true);
            AddRock(89f, 1.2f, 1.3f);
            AddLog(92.2f, 1.2f, 5.2f);
            AddStone(95.2f, true);
            AddStone(97.4f, false);
            AddLog(99.8f, 0, 0);

            // the brook by the ridge, under a stone arch bridge (jumped onto: it stands as tall as Raja)
            var stone = CbArt.Joint(root, StoneBridgeX, 0, 0.15f, "stoneBridge");
            if (CbArt.Prop("Cloudborne/Jungle/StoneBridge/StoneBridge", stone, CbArt.PropOutline) != null) stone.localScale = Vector3.one * 0.75f;
            else CbArt.Box(CbArt.Toon("#8f9a8a", CbArt.PropOutline), stone, new Vector3(0, 0.82f, 0), new Vector3(3.6f, 1.64f, 1.6f));
            blocks.Add(new Block { x0 = StoneBridgeX - 1.8f, x1 = StoneBridgeX + 1.8f, top = 1.64f, t = stone });
        }

        /// <summary>A stepping stone, its top level with the banks; a sinking one shakes and goes under if he stays.</summary>
        void AddStone(float x, bool sinks)
        {
            // the stone model is low: stood up taller so it reaches down into the water
            var t = PlaceProp("Cloudborne/Jungle/SteppingStone/SteppingStone", new Vector3(x, -0.56f, 0.05f), new Vector3(1f, 2f, 1f), Random.Range(0f, 360f));
            if (t == null)
            {
                t = CbArt.Joint(root, x, 0, 0.05f, "stone");
                CbArt.Box(CbArt.Toon(sinks ? "#9a8f7a" : "#8f9a8a", CbArt.PropOutline), t, new Vector3(0, -0.3f, 0), new Vector3(1.1f, 0.6f, 0.9f));
            }
            // a sinking stone is a little paler, with a crack: a hint
            if (sinks)
                foreach (var r in t.GetComponentsInChildren<Renderer>()) r.material.color *= new Color(1.1f, 1.05f, 0.95f);
            crossings.Add(new Crossing { cx = x, half = 0.55f, top = 0f, sinks = sinks, t = t, home = t.localPosition });
        }

        /// <summary>A floating log, half under water, bobbing; with drift it floats to and fro, carrying him.</summary>
        void AddLog(float x, float drift, float period)
        {
            var t = PlaceProp("Cloudborne/Jungle/Log/Log", new Vector3(x, -0.73f, 0.05f), Vector3.one, 0);
            if (t == null)
            {
                t = CbArt.Joint(root, x, -0.07f, 0.05f, "log");
                CbArt.Cylinder(CbArt.Toon("#8a5a32", CbArt.PropOutline), t, new Vector3(0, -0.33f, 0), 0.33f, 1.9f, CbArt.Rot(0, 0, Mathf.PI / 2));
            }
            crossings.Add(new Crossing { cx = x, half = 0.95f, top = -0.07f, bobs = true, drift = drift, period = Mathf.Max(0.1f, period), t = t, home = t.localPosition });
        }

        /// <summary>A rock standing up out of the river: solid, a safe place to land.</summary>
        void AddRock(float x, float width, float top)
        {
            var t = CbArt.Joint(root, x, 0, 0, "riverRock");
            EarthSlab(t, -width / 2, width / 2, WaterLevel - 0.6f, top, -0.8f, 1f, earth ??= EarthMat(), grass ??= GrassMat());
            blocks.Add(new Block { x0 = x - width / 2, x1 = x + width / 2, top = top, t = t });
        }

        // ---------- the mountain ----------

        /// <summary>
        /// The mountain after the shrine: terraces (solid) and ledges up to its summit, the higher ledges crumbling,
        /// rocks falling on the way; a gap to jump at the top; ledges down the far side. Secret: from the high
        /// plateau two hidden ledges lead up and back to the scroll.
        /// </summary>
        void BuildMountain()
        {
            earth ??= EarthMat();
            grass ??= GrassMat();
            crackedEarth = new Material(earth) { color = earth.color * new Color(1.15f, 1.02f, 0.85f) };
            AddTerrace(137f, 140f, 1.4f, 2.4f);
            AddTerrace(141.5f, 144f, 2.8f, 3.2f);
            AddLedge(146.25f, 1.5f, 4.2f, false);
            AddLedge(149.2f, 1.2f, 5.6f, true);
            AddTerrace(151f, 152.4f, 6.4f, 2.2f);
            AddLedge(154.7f, 1.4f, 7.8f, false);
            AddLedge(157.3f, 1.0f, 9.0f, true);
            AddTerrace(159f, 167f, 10.2f, 5f);
            AddLedge(171.5f, 1.0f, 10.8f, true);
            AddTerrace(174f, 179f, SummitTop, 5f);
            // down the far side
            AddLedge(181.7f, 1.4f, 8.6f, false);
            AddLedge(184.7f, 1.4f, 5.8f, true);
            AddLedge(187.7f, 1.4f, 3.0f, false);
            // a secret: from the plateau, two hidden ledges lead up and back to the scroll (a glint now and then gives
            // them away)
            AddLedge(163.5f, 1.3f, 12.8f, false, hidden: true);
            AddLedge(160.3f, 1.4f, 15f, false, hidden: true);
            // rocks falling on the way up
            foreach (var (x, period) in new[] { (146.6f, 3.4f), (152.2f, 4.1f), (157.6f, 3.7f), (176.5f, 4.6f) })
                rocks.Add(new FallingRock { x = x, period = period, timer = period * Random.Range(0.3f, 1f) });
        }

        /// <summary>A terrace of the mountain: solid earth from the ground up to top, depth metres deep.</summary>
        void AddTerrace(float x0, float x1, float top, float depth)
        {
            var t = CbArt.Joint(root, (x0 + x1) / 2, 0, 0, "terrace");
            EarthSlab(t, x0 - (x0 + x1) / 2, x1 - (x0 + x1) / 2, -0.3f, top, -0.9f, depth, earth, grass);
            blocks.Add(new Block { x0 = x0, x1 = x1, top = top, t = t });
        }

        /// <summary>
        /// Earth like the stage's own ground: the painted cross-section of soil on its front and sides, grass on top,
        /// the painting lined up with the world so neighbouring pieces match.
        /// </summary>
        static void EarthSlab(Transform parent, float x0, float x1, float y0, float y1, float zFront, float zBack, Material face, Material top)
        {
            float w = x1 - x0, h = y1 - y0, d = zBack - zFront, cx = (x0 + x1) / 2;
            float wx = parent.position.x + cx;
            var front = CbArt.Part(CbMesh.Quad, FaceMat(face, w, h, wx - w / 2), parent, new Vector3(cx, (y0 + y1) / 2, zFront), new Vector3(w, h, 1), shadows: false);
            front.name = "face";
            var side = FaceMat(face, d, h, 0);
            CbArt.Part(CbMesh.Quad, side, parent, new Vector3(x0, (y0 + y1) / 2, zFront + d / 2), new Vector3(d, h, 1), Quaternion.Euler(0, 90, 0), shadows: false);
            CbArt.Part(CbMesh.Quad, side, parent, new Vector3(x1, (y0 + y1) / 2, zFront + d / 2), new Vector3(d, h, 1), Quaternion.Euler(0, -90, 0), shadows: false);
            var lid = new Material(top) { mainTextureScale = new Vector2(w / CbWorld.GrassTile, d / CbWorld.GrassTile), mainTextureOffset = new Vector2(wx / CbWorld.GrassTile, 0) };
            CbArt.Part(CbMesh.Quad, lid, parent, new Vector3(cx, y1, zFront + d / 2), new Vector3(w, d, 1), CbArt.Rot(Mathf.PI / 2), shadows: false);
        }

        /// <summary>The soil material for a face w by h: the painting's grassy top row at the top edge, then earth.</summary>
        static Material FaceMat(Material face, float w, float h, float worldX)
        {
            var m = new Material(face);
            float tileW = CbWorld.FaceTileWidth(face.mainTexture);
            float rows = h / CbWorld.FaceHeight;
            m.mainTextureScale = new Vector2(w / tileW, rows);
            m.mainTextureOffset = new Vector2(worldX / tileW, 1 - rows);
            return m;
        }

        static Material EarthMat() => CbWorld.FaceMaterial();
        static Material GrassMat() => CbWorld.GrassMaterial();

        // ---------- blocks and ledges ----------

        /// <summary>Puts everything back: the wall whole, the ledges in place, the stones up, the rocks waiting.</summary>
        public void Reset()
        {
            foreach (var b in blocks)
            {
                if (!b.breakable) continue;
                b.broken = false;
                b.hp = 2;
                b.t.gameObject.SetActive(true);
                b.t.localRotation = Quaternion.identity;
            }
            foreach (var c in crossings) { c.dip = c.bob = c.dx = c.sunk = c.shake = c.stood = c.timer = 0; c.phase = 0; }
            foreach (var r in rocks)
            {
                r.falling = false;
                r.timer = r.period * Random.Range(0.3f, 1f);
                if (r.t != null) r.t.gameObject.SetActive(false);
            }
            foreach (var l in ledges)
            {
                l.gone = false;
                l.stood = l.respawn = 0;
                l.found = false;
                l.reveal = l.hidden ? 0 : 1;
                if (l.hidden) l.t.localScale = Vector3.zero;
                l.t.localPosition = l.home;
                l.t.gameObject.SetActive(true);
            }
        }

        void AddBlock(float x, float width, float height, bool breakable)
        {
            // a shrine pillar stretched to the size (its model, or grey stone), on the path
            var t = CbArt.Joint(root, x, 0, 0.1f, breakable ? "crackedWall" : "pillar");
            var holder = CbArt.Joint(t, 0, 0, 0, "shape");
            holder.localScale = new Vector3(width / 1.48f, height / 3.6f, 0.9f);
            if (CbArt.Prop("Cloudborne/Jungle/Pillar/Pillar", holder, CbArt.PropOutline) == null)
                CbArt.Box(CbArt.Toon("#8f9a8a", CbArt.PropOutline), holder, new Vector3(0, 1.8f, 0), new Vector3(1.48f, 3.6f, 1.5f));
            if (breakable)
            {
                // dark cracks down its face: a hint that something heavy will break it
                var crack = CbArt.Toon("#2b2622");
                for (int k = 0; k < 5; k++)
                    CbArt.Box(crack, t, new Vector3(Mathf.Sin(k * 2.1f) * 0.25f, 0.6f + k * 0.8f, -0.62f), new Vector3(0.06f, 0.7f, 0.03f), CbArt.Rot(0, 0, (k % 2 == 0 ? 0.5f : -0.5f)));
            }
            blocks.Add(new Block { x0 = x - width / 2, x1 = x + width / 2, top = height, breakable = breakable, hp = 2, t = t });
        }

        void AddLedge(float x, float width, float top, bool crumbles, bool hidden = false)
        {
            // a floating slab of earth, grass on top, its underside rough; a crumbling one paler and cracked
            var t = CbArt.Joint(root, x, 0, 0.1f, crumbles ? "crumblingLedge" : "ledge");
            earth ??= EarthMat();
            grass ??= GrassMat();
            EarthSlab(t, -width / 2, width / 2, top - 0.55f, top, -0.75f, 0.9f, crumbles ? (crackedEarth ?? earth) : earth, grass);
            if (crumbles)
            {
                var crack = CbArt.Toon("#2b2622");
                for (int k = 0; k < 3; k++)
                    CbArt.Box(crack, t, new Vector3((k - 1) * width * 0.3f, top - 0.28f, -0.77f), new Vector3(0.05f, 0.4f, 0.02f), CbArt.Rot(0, 0, k % 2 == 0 ? 0.4f : -0.4f));
            }
            ledges.Add(new Ledge { x0 = x - width / 2, x1 = x + width / 2, top = top, crumbles = crumbles, t = t, home = t.localPosition,
                hidden = hidden, reveal = hidden ? 0 : 1 });
            if (hidden) t.localScale = Vector3.zero;
        }

        /// <summary>A model in a joint of its own (so its import rotation stays), sized, turned about Y, on the level.</summary>
        Transform PlaceProp(string path, Vector3 at, Vector3 scale, float yaw)
        {
            var t = CbArt.Joint(root, at.x, at.y, at.z, "prop");
            if (CbArt.Prop(path, t, CbArt.PropOutline) == null) { Object.Destroy(t.gameObject); return null; }
            t.localScale = scale;
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            return t;
        }

        // ---------- what the hero's physics asks ----------

        /// <summary>
        /// The highest surface under x that a body at height fromY can stand on: the path (0, nothing over the water),
        /// a block's top, a ledge's top (only one it was already above: ledges are jumped up through from below), a
        /// bridge's deck (stepped onto from the bank) or a stone or log (landed on from above).
        /// </summary>
        public float SupportAt(float x, float fromY)
        {
            float best = InWater(x) ? Deep : 0;
            foreach (var c in crossings)
            {
                if (x <= c.X0 - HalfWidth * 0.5f || x >= c.X1 + HalfWidth * 0.5f) continue;
                float top = c.Top(x);
                if (top <= fromY + (c.deck ? StepUp : 0.05f) && top > best) best = top;
            }
            foreach (var b in blocks)
                if (!b.broken && x > b.x0 - HalfWidth * 0.5f && x < b.x1 + HalfWidth * 0.5f && b.top <= fromY + 0.05f && b.top > best) best = b.top;
            foreach (var l in ledges)
                if (!l.gone && l.reveal > 0.5f && x > l.x0 - HalfWidth * 0.5f && x < l.x1 + HalfWidth * 0.5f && l.top <= fromY + 0.05f && l.top > best) best = l.top;
            return best;
        }

        /// <summary>
        /// Whether standing at (x, y) is a safe place to come back to after falling into the water: anywhere away from
        /// the water, a bridge, a steady stepping stone, the rock or the stone bridge - not a sinking stone or a log.
        /// </summary>
        public bool SafeAt(float x, float y)
        {
            if (!InWater(x)) return true;
            foreach (var b in blocks)
                if (!b.broken && x > b.x0 - HalfWidth * 0.5f && x < b.x1 + HalfWidth * 0.5f && Mathf.Abs(y - b.top) < 0.05f) return true;
            foreach (var c in crossings)
                if (x > c.X0 - HalfWidth * 0.5f && x < c.X1 + HalfWidth * 0.5f && Mathf.Abs(y - c.Top(x)) < 0.05f) return c.Safe;
            return false;
        }

        /// <summary>Where the middle of the stone, log or bridge nearest x is now (a drifting log moves).</summary>
        public float CrossingAt(float x)
        {
            float best = x, dist = float.MaxValue;
            foreach (var c in crossings)
                if (Mathf.Abs(c.cx - x) < dist) { dist = Mathf.Abs(c.cx - x); best = c.cx + c.dx; }
            return best;
        }

        /// <summary>Moving from x0 to x1 at height y: solid blocks stop him at their side.</summary>
        public float BlockX(float x0, float x1, float y)
        {
            foreach (var b in blocks)
            {
                if (b.broken || y >= b.top - 0.05f) continue;
                if (x0 <= b.x0 - HalfWidth + 0.01f && x1 > b.x0 - HalfWidth) x1 = b.x0 - HalfWidth;
                else if (x0 >= b.x1 + HalfWidth - 0.01f && x1 < b.x1 + HalfWidth) x1 = b.x1 + HalfWidth;
            }
            return x1;
        }

        /// <summary>
        /// A blow from Raja at x towards dir. The cracked wall only gives way to the staff; paws and kicks clang
        /// off it with a hint. True if a block was struck.
        /// </summary>
        public bool Strike(float x, float y, int dir, float range, bool staff)
        {
            foreach (var b in blocks)
            {
                if (!b.breakable || b.broken) continue;
                float near = dir > 0 ? b.x0 - x : x - b.x1;
                if (near < -0.6f || near > range || y > b.top) continue;
                var at = new Vector3(dir > 0 ? b.x0 : b.x1, y + 1.1f, -0.6f);
                if (!staff)
                {
                    CbFx.Spark(at, CbFx.Grey, 6, 4, 0.3f, 0.25f, 4);
                    CbFx.Sfx("block", 0.7f);
                    g.hud.SetTip("Cracked stone: break it with the staff (<color=#ffb03a>HEAVY</color>)");
                    return true;
                }
                b.hp--;
                CbFx.Spark(at, CbFx.Dust, 14, 5, 0.5f, 0.5f, 6);
                g.shake = Mathf.Max(g.shake, 0.3f);
                CbFx.Sfx("heavy", 0.8f);
                if (b.hp > 0) { b.t.localRotation = Quaternion.Euler(0, 0, dir * 2f); return true; }
                // it bursts into tumbling stones
                b.broken = true;
                b.t.gameObject.SetActive(false);
                for (int k = 0; k < 10; k++)
                {
                    var chunk = CbArt.Part(CbMesh.Box(), CbArt.Toon("#8f9a8a"), g.transform, new Vector3((b.x0 + b.x1) / 2, Random.Range(0.3f, b.top), 0),
                        Vector3.one * Random.Range(0.3f, 0.6f), Random.rotation, shadows: false);
                    g.AddDebris(chunk, new Vector3(dir * Random.Range(2f, 7f), Random.Range(2f, 6f), Random.Range(-1f, 1f)), Random.Range(-400f, 400f));
                }
                CbVfx.Burst(at, CbFx.Hit, 3f, 0.25f);
                g.hud.ImpactFlash(0.3f);
                g.hud.SetTip("The way is open!");
                return true;
            }
            return false;
        }

        // ---------- moving parts ----------

        /// <summary>
        /// Crumbling ledges drop when stood on and come back; hidden ledges show when he climbs near; logs bob, dip
        /// under him and drift, carrying him; sinking stones go under; the rope bridge bounces; rocks fall.
        /// </summary>
        public void Update(float dt, CbHero hero)
        {
            UpdateCrossings(dt, hero);
            UpdateRocks(dt, hero);
            foreach (var l in ledges)
            {
                if (l.hidden)
                {
                    float cx = (l.x0 + l.x1) / 2;
                    // found once he is up high close by (on the plateau, or a jump near it)
                    if (!l.found && Mathf.Abs(hero.x - cx) < 4.6f && hero.y > 1.8f && hero.y > l.top - 4.2f)
                    {
                        l.found = true;
                        CbFx.Spark(new Vector3(cx, l.top, -0.4f), CbFx.Hit, 16, 4, 0.4f, 0.5f, -2);
                        CbFx.Sfx("pickup", 0.6f);
                        g.hud.SetTip("A hidden path! Something is up there...");
                    }
                    l.reveal = Mathf.MoveTowards(l.reveal, l.found ? 1 : 0, dt * 3f);
                    l.t.localScale = Vector3.one * Mathf.SmoothStep(0, 1, l.reveal);
                    // a glint now and then where it hides
                    l.glint -= dt;
                    if (!l.found && l.glint <= 0)
                    {
                        l.glint = 2.4f + Random.value;
                        CbFx.Spark(new Vector3(cx + Random.Range(-0.5f, 0.5f), l.top, -0.4f), CbFx.Hit * 0.5f, 3, 1, 0.25f, 0.7f, -1);
                    }
                    continue;
                }
                if (!l.crumbles) continue;
                if (l.gone)
                {
                    l.respawn -= dt;
                    var p = l.t.localPosition;
                    p.y -= dt * 9;
                    l.t.localPosition = p;
                    if (p.y < -3) l.t.gameObject.SetActive(false);
                    if (l.respawn <= 0)
                    {
                        l.gone = false;
                        l.stood = 0;
                        l.t.localPosition = l.home;
                        l.t.gameObject.SetActive(true);
                    }
                    continue;
                }
                bool on = Mathf.Abs(hero.y - l.top) < 0.02f && hero.x > l.x0 - HalfWidth * 0.5f && hero.x < l.x1 + HalfWidth * 0.5f;
                l.stood = on ? l.stood + dt : Mathf.Max(0, l.stood - dt);
                // shaking more and more, then it drops
                float shake = l.stood > 0 ? Mathf.Sin(Time.time * 60) * 0.04f * (l.stood / CrumbleAfter) : 0;
                l.t.localPosition = l.home + new Vector3(shake, 0, 0);
                if (l.stood >= CrumbleAfter)
                {
                    l.gone = true;
                    l.respawn = CrumbleBack;
                    CbFx.Spark(new Vector3((l.x0 + l.x1) / 2, l.top, -0.4f), CbFx.Dust, 12, 3, 0.5f, 0.5f, 6);
                    CbFx.Sfx("hit", 0.5f);
                }
            }
        }

        void UpdateCrossings(float dt, CbHero hero)
        {
            foreach (var c in crossings)
            {
                bool on = hero.x > c.X0 - HalfWidth * 0.5f && hero.x < c.X1 + HalfWidth * 0.5f && Mathf.Abs(hero.y - c.Top(hero.x)) < 0.04f;
                if (c.deck)
                {
                    // the rope bridge bounces a touch under his steps
                    c.bob = on ? Mathf.Sin(Time.time * 9f) * 0.015f : 0;
                    if (c.t != null) c.t.localPosition = c.home + new Vector3(0, c.bob, 0);
                    continue;
                }
                if (c.drift > 0)
                {
                    // to and fro on the current, carrying him with it
                    float to = c.drift * Mathf.Sin(Time.time * Mathf.PI * 2 / c.period + c.cx);
                    if (on) hero.x += to - c.dx;
                    c.dx = to;
                }
                if (c.bobs)
                {
                    c.bob = Mathf.Sin(Time.time * 1.8f + c.cx) * 0.035f;
                    c.dip = Mathf.MoveTowards(c.dip, on ? 0.1f : 0f, dt * 0.5f);
                }
                if (c.sinks) Sink(c, dt, on);
                if (c.t != null)
                {
                    c.t.localPosition = c.home + new Vector3(c.dx + c.shake, c.bob - c.dip, 0);
                    if (c.bobs) c.t.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + c.cx) * 4f, 0, 0);
                }
            }
        }

        /// <summary>A sinking stone: stood on for a moment it shakes, goes under, and comes back up a while later.</summary>
        void Sink(Crossing c, float dt, bool on)
        {
            const float Warn = 0.7f, Under = 2.2f;
            c.shake = 0;
            switch (c.phase)
            {
                case 0:
                    c.stood = on ? c.stood + dt : Mathf.Max(0, c.stood - dt * 0.5f);
                    c.shake = c.stood > 0 ? Mathf.Sin(Time.time * 55) * 0.035f * (c.stood / Warn) : 0;
                    if (c.stood >= Warn) { c.phase = 1; CbFx.Sfx("splash", 0.4f); }
                    break;
                case 1:
                    c.sunk = Mathf.MoveTowards(c.sunk, 1, dt * 2.5f);
                    if (Random.value < 0.5f) CbFx.Spark(new Vector3(c.cx + Random.Range(-0.5f, 0.5f), WaterLevel + 0.05f, -0.3f), CbFx.Water, 1, 2, 0.3f, 0.4f, 6);
                    if (c.sunk >= 1) { c.phase = 2; c.timer = Under; }
                    break;
                case 2:
                    c.timer -= dt;
                    if (c.timer <= 0) c.phase = 3;
                    break;
                default:
                    c.sunk = Mathf.MoveTowards(c.sunk, 0, dt * 1.2f);
                    if (c.sunk <= 0) { c.phase = 0; c.stood = 0; }
                    break;
            }
            c.dip = c.sunk * 0.95f;
        }

        void UpdateRocks(float dt, CbHero hero)
        {
            // only while he is on the mountain
            if (hero.x < MountainFrom - 6 || hero.x > MountainTo + 4) return;
            foreach (var r in rocks)
            {
                if (!r.falling)
                {
                    r.timer -= dt;
                    // a trickle of dust and pebbles from above warns of it
                    if (r.timer < RockWarn && Random.value < 0.5f)
                        CbFx.Spark(new Vector3(r.x + Random.Range(-0.3f, 0.3f), Random.Range(13f, 15f), -0.4f), CbFx.Dust, 1, 0.5f, 0.25f, 1f, 14);
                    if (r.timer <= 0)
                    {
                        r.falling = true;
                        r.y = 18f; r.vy = 0;
                        if (r.t == null)
                        {
                            r.t = CbArt.Joint(root, r.x, r.y, -0.2f, "fallingRock");
                            if (CbArt.Prop("Cloudborne/Jungle/Boulder/Boulder", r.t, CbArt.PropOutline) == null)
                                CbArt.Part(CbMesh.Blob(6, 4), CbArt.Toon("#7f8a78", CbArt.PropOutline), r.t, new Vector3(0, 0.5f, 0), Vector3.one * 0.9f, Quaternion.identity);
                            r.t.localScale = Vector3.one * 0.6f;
                        }
                        r.t.gameObject.SetActive(true);
                    }
                    continue;
                }
                r.vy -= 30 * dt;
                r.y += r.vy * dt;
                r.t.localPosition = new Vector3(r.x, r.y, -0.2f);
                r.t.localRotation = Quaternion.Euler(0, 0, r.y * 40f);
                // it strikes him if it passes through where he stands
                if (Mathf.Abs(hero.x - r.x) < 0.75f && r.y < hero.y + 1.8f && r.y > hero.y - 0.2f)
                {
                    hero.TakeHit(RockDamage, hero.x >= r.x ? 1 : -1);
                    Shatter(r);
                    continue;
                }
                // or breaks on whatever is under it
                float floor = SupportAt(r.x, r.y + 0.1f);
                if (r.y <= Mathf.Max(floor, -1f)) Shatter(r);
            }
        }

        void Shatter(FallingRock r)
        {
            var at = new Vector3(r.x, r.y + 0.3f, -0.4f);
            CbFx.Spark(at, CbFx.Dust, 14, 5, 0.45f, 0.5f, 8);
            CbFx.Spark(at, CbFx.Grey, 6, 6, 0.3f, 0.4f, 12);
            CbFx.Sfx("hit", 0.5f);
            r.falling = false;
            r.timer = r.period;
            r.t.gameObject.SetActive(false);
        }
    }
}
