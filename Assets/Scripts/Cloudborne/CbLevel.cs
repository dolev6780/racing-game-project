using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The shape of the stage beyond its flat path: things to climb and things in the way.
    /// <list type="bullet">
    /// <item>Blocks: solid stone (pillars, the cracked wall) Raja can't walk through but can stand on.</item>
    /// <item>Ledges: floating slabs he jumps up through and lands on; a crumbling one gives way under him.</item>
    /// <item>Water: streams across the path. Falling in puts Raja back on the bank he came from and costs health.
    /// What crosses them: a rope bridge (a deck he walks on, sagging in the middle), stepping stones and a floating
    /// log (landed on from above, the log dipping under him), a stone arch bridge (a block to jump onto).</item>
    /// </list>
    /// Stage 1-1: a stream crossed by a rope bridge after the first fight; past the cracked wall (which only the
    /// staff breaks) a river crossed on stepping stones and a log; a brook under a stone arch bridge by the shrine;
    /// and after the last fight a climb of pillars and ledges, the last ones crumbling, to the golden fruit.
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

        /// <summary>A surface over the water: a deck (the rope bridge, walked onto from the bank) or a floater.</summary>
        class Crossing
        {
            public float x0, x1, top, sag, dip, bob, stood;
            // a deck is stepped onto from the bank (StepUp); a floater only landed on from above
            public bool deck;
            public Transform t;
            public Vector3 home;
            public float Top(float x)
            {
                float u = Mathf.Clamp01((x - x0) / Mathf.Max(0.01f, x1 - x0));
                return top - sag * Mathf.Sin(u * Mathf.PI) + bob - dip;
            }
        }

        public const float HalfWidth = 0.35f;
        /// <summary>The streams across stage 1-1 (x from, to). Their surface is at WaterLevel.</summary>
        public static readonly (float x0, float x1)[] Waters = { (28.4f, 33.6f), (62.0f, 68.8f), (93.4f, 95.8f) };
        public const float WaterLevel = 0.03f;
        // below the path where the water is: deep enough that falling in is falling in
        const float Deep = -50f, StepUp = 0.3f;
        // the rope bridge's planks: at the ends a little above the banks, 0.29 lower in the middle
        const float RopeBridgeX = 31f, RopeDeckHalf = 3f;
        const float StoneBridgeX = 94.6f;
        Material waterMat, fallMat;
        // the cracked wall: taller than a double jump, two staff blows break it
        public const float WallX = 60f, WallHeight = 4.6f;
        const float CrumbleAfter = 0.5f, CrumbleBack = 3f;

        readonly CbGame g;
        readonly Transform root;
        readonly List<Block> blocks = new List<Block>();
        readonly List<Ledge> ledges = new List<Ledge>();
        readonly List<Crossing> crossings = new List<Crossing>();

        /// <summary>Whether x is over one of the streams (margin widens them, for keeping scenery off the water).</summary>
        public static bool InWater(float x, float margin = 0f)
        {
            foreach (var (x0, x1) in Waters)
                if (x > x0 - margin && x < x1 + margin) return true;
            return false;
        }

        /// <summary>The bank to put Raja back on after he fell in at x, on the side he came from (fromX).</summary>
        public static float BankFor(float x, float fromX)
        {
            foreach (var (x0, x1) in Waters)
                if (x > x0 && x < x1) return fromX <= x0 ? x0 - 0.8f : fromX >= x1 ? x1 + 0.8f : (x - x0 < x1 - x ? x0 - 0.8f : x1 + 0.8f);
            return x;
        }

        public bool WallStanding => blocks.Exists(b => b.breakable && !b.broken);

        public CbLevel(CbGame game, Transform parent)
        {
            g = game;
            root = new GameObject("Level").transform;
            root.SetParent(parent, false);
            Build();
        }

        void Build()
        {
            // the cracked wall across the path
            AddBlock(WallX, 1.3f, WallHeight, breakable: true);
            // after the last fight: two pillars on the path to climb over...
            AddBlock(99f, 1.1f, 1.4f, false);
            AddBlock(101.8f, 1.1f, 2.6f, false);
            // ...and ledges up to the golden fruit; the two highest crumble under him
            AddLedge(105f, 1.8f, 3.9f, false);
            AddLedge(108.4f, 1.3f, 5.3f, true);
            AddLedge(111.6f, 1.1f, 6.5f, true);
            // a secret: from the top of the tall pillar, two hidden ledges lead up and back to the left, to the scroll
            // high above the path (a glint now and then gives them away)
            AddLedge(97.6f, 1.3f, 5.2f, false, hidden: true);
            AddLedge(94.2f, 1.4f, 7.3f, false, hidden: true);
            BuildWater();
            // the end of the stage: a shrine gate on the path to walk through
            var goal = CbArt.Joint(root, CbWorld.GoalX, 0, 0.8f, "goalGate");
            if (CbArt.Prop("Cloudborne/Jungle/Gate/Gate", goal, CbArt.PropOutline) is Transform gate) gate.localRotation = Quaternion.Euler(0, 180, 0) * gate.localRotation;
        }

        /// <summary>
        /// The streams: a flowing surface level with the path from the front edge back to the rocks it comes out of,
        /// pouring over the front edge; and what crosses each one.
        /// </summary>
        void BuildWater()
        {
            var tex = WaterTexture();
            waterMat = CbArt.NewToon(new Color(0.85f, 0.95f, 1f), 0f, tex);
            waterMat.SetFloat("_SpecStrength", 0.4f);
            fallMat = CbArt.NewToon(new Color(0.9f, 0.98f, 1f), 0f, tex);
            fallMat.SetFloat("_SpecStrength", 0f);
            const float front = -1.72f, back = 9.6f;
            foreach (var (x0, x1) in Waters)
            {
                float cx = (x0 + x1) / 2, w = x1 - x0;
                var surface = CbArt.Part(CbMesh.Quad, waterMat, root, new Vector3(cx, WaterLevel, (front + back) / 2), new Vector3(w, back - front, 1),
                    CbArt.Rot(Mathf.PI / 2), shadows: false);
                surface.name = "water";
                // over the front edge it falls away out of view
                CbArt.Part(CbMesh.Quad, fallMat, root, new Vector3(cx, (WaterLevel - 4f) / 2, front - 0.01f), new Vector3(w, 4f + WaterLevel, 1), shadows: false)
                    .name = "waterfall";
                // it comes out from between mossy rocks at the back, and wet stones line the banks
                for (int k = 0; k < 3; k++)
                    PlaceProp("Cloudborne/Jungle/Boulder/Boulder", new Vector3(x0 + w * (0.15f + 0.35f * k), -0.1f, back + Random.Range(-0.3f, 0.3f)),
                        new Vector3(1.4f, 1.1f, 1.2f) * Random.Range(0.9f, 1.3f), Random.Range(0f, 360f));
                foreach (var bank in new[] { x0, x1 })
                    PlaceProp("Cloudborne/Jungle/Boulder/Boulder", new Vector3(bank, -0.12f, Random.Range(2.2f, 3.4f)), Vector3.one * Random.Range(0.45f, 0.6f), Random.Range(0f, 360f));
            }
            // the rope bridge over the first stream: its planks just above the banks at the ends, sagging in the middle
            var rope = PlaceProp("Cloudborne/Jungle/RopeBridge/RopeBridge", new Vector3(RopeBridgeX, -0.25f, 0.15f), Vector3.one, 0);
            crossings.Add(new Crossing { x0 = RopeBridgeX - RopeDeckHalf, x1 = RopeBridgeX + RopeDeckHalf, top = 0.145f, sag = 0.29f, deck = true, t = rope,
                home = rope != null ? rope.localPosition : Vector3.zero });
            // the river past the cracked wall: a stepping stone, a floating log, a stepping stone
            AddFloater("Cloudborne/Jungle/SteppingStone/SteppingStone", 63.3f, 1.1f, 0.22f, -0.02f, Vector3.one);
            AddFloater("Cloudborne/Jungle/Log/Log", 65.4f, 1.9f, 0.3f, -0.32f, Vector3.one, bobs: true);
            AddFloater("Cloudborne/Jungle/SteppingStone/SteppingStone", 67.5f, 1.1f, 0.22f, -0.02f, Vector3.one);
            // the brook by the shrine, under a stone arch bridge (jumped onto: it stands as tall as Raja)
            var stone = CbArt.Joint(root, StoneBridgeX, 0, 0.15f, "stoneBridge");
            if (CbArt.Prop("Cloudborne/Jungle/StoneBridge/StoneBridge", stone, CbArt.PropOutline) != null) stone.localScale = Vector3.one * 0.75f;
            else CbArt.Box(CbArt.Toon("#8f9a8a", CbArt.PropOutline), stone, new Vector3(0, 0.82f, 0), new Vector3(3.6f, 1.64f, 1.6f));
            blocks.Add(new Block { x0 = StoneBridgeX - 1.8f, x1 = StoneBridgeX + 1.8f, top = 1.64f, t = stone });
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

        /// <summary>Something to land on in the river: its top `top` above the water while nobody stands on it.</summary>
        void AddFloater(string path, float x, float width, float top, float y, Vector3 scale, bool bobs = false)
        {
            var t = PlaceProp(path, new Vector3(x, y, 0.05f), scale, 0);
            if (t == null)
            {
                t = CbArt.Joint(root, x, y, 0.05f, "floater");
                CbArt.Box(CbArt.Toon(bobs ? "#8a5a32" : "#8f9a8a", CbArt.PropOutline), t, new Vector3(0, (top - y) / 2, 0), new Vector3(width, top - y, 0.9f));
            }
            crossings.Add(new Crossing { x0 = x - width / 2, x1 = x + width / 2, top = WaterLevel + top, t = t, home = t.localPosition, bob = 0, dip = 0,
                sag = 0, deck = false, stood = bobs ? 0 : -1 });
        }

        /// <summary>A water texture drawn in code (the painted one waits for the user's approval): ripples of light on blue.</summary>
        static Texture2D WaterTexture()
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "water" };
            Color deep = CbArt.Hex("#1f7fa6"), mid = CbArt.Hex("#3aa6c8"), light = CbArt.Hex("#c9f1fb");
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float wave = Mathf.Sin((v * 3 + Mathf.Sin(u * Mathf.PI * 2) * 0.35f) * Mathf.PI * 2);
                    float ripple = Mathf.Sin((v * 7 + Mathf.Sin(u * Mathf.PI * 4 + 1) * 0.5f) * Mathf.PI * 2);
                    var c = Color.Lerp(deep, mid, 0.5f + 0.5f * wave);
                    if (ripple > 0.82f) c = Color.Lerp(c, light, (ripple - 0.82f) / 0.18f);
                    px[y * S + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Puts everything back: the wall whole, the ledges in place.</summary>
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
            foreach (var c in crossings) { c.dip = c.bob = 0; if (c.stood > 0) c.stood = 0; }
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
            // a flat mossy slab of rock (the boulder squashed), its top at `top`
            var t = CbArt.Joint(root, x, top - 0.35f, 0.1f, crumbles ? "crumblingLedge" : "ledge");
            var holder = CbArt.Joint(t, 0, 0, 0, "shape");
            holder.localScale = new Vector3(width / 1.3f, 0.3f, 1.1f);
            if (CbArt.Prop("Cloudborne/Jungle/Boulder/Boulder", holder, CbArt.PropOutline) == null)
                CbArt.Box(CbArt.Toon(crumbles ? "#9a8f7a" : "#7f8a78", CbArt.PropOutline), holder, new Vector3(0, 0.6f, 0), new Vector3(1.3f, 1.2f, 1.2f));
            ledges.Add(new Ledge { x0 = x - width / 2, x1 = x + width / 2, top = top, crumbles = crumbles, t = t, home = t.localPosition,
                hidden = hidden, reveal = hidden ? 0 : 1 });
            if (hidden) t.localScale = Vector3.zero;
        }

        // ---------- what the hero's physics asks ----------

        /// <summary>
        /// The highest surface under x that a body at height fromY can stand on: the path (0), a block's top, or a
        /// ledge's top - but only one it was already above (ledges are jumped up through from below).
        /// </summary>
        public float SupportAt(float x, float fromY)
        {
            // the path, or nothing at all over the water
            float best = InWater(x) ? Deep : 0;
            // a bridge's deck is walked onto from the bank; a stone or the log only landed on from above
            foreach (var c in crossings)
            {
                if (x <= c.x0 - HalfWidth * 0.5f || x >= c.x1 + HalfWidth * 0.5f) continue;
                float top = c.Top(x);
                if (top <= fromY + (c.deck ? StepUp : 0.05f) && top > best) best = top;
            }
            foreach (var b in blocks)
                if (!b.broken && x > b.x0 - HalfWidth * 0.5f && x < b.x1 + HalfWidth * 0.5f && b.top <= fromY + 0.05f && b.top > best) best = b.top;
            foreach (var l in ledges)
                if (!l.gone && l.reveal > 0.5f && x > l.x0 - HalfWidth * 0.5f && x < l.x1 + HalfWidth * 0.5f && l.top <= fromY + 0.05f && l.top > best) best = l.top;
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

        /// <summary>
        /// Crumbling ledges: stood on for a moment they shake and drop, and come back a while later. The water flows,
        /// the log bobs and dips under Raja, the rope bridge sways a little as he crosses.
        /// </summary>
        public void Update(float dt, CbHero hero)
        {
            if (waterMat != null)
            {
                waterMat.mainTextureOffset += new Vector2(0, -dt * 0.18f);
                fallMat.mainTextureOffset += new Vector2(0, dt * 0.9f);
            }
            foreach (var c in crossings)
            {
                bool on = hero.x > c.x0 - HalfWidth * 0.5f && hero.x < c.x1 + HalfWidth * 0.5f && Mathf.Abs(hero.y - c.Top(hero.x)) < 0.03f;
                if (c.deck)
                {
                    // the rope bridge bounces a touch under his steps
                    c.bob = on ? Mathf.Sin(Time.time * 9f) * 0.015f : 0;
                    if (c.t != null) c.t.localPosition = c.home + new Vector3(0, c.bob, 0);
                    continue;
                }
                if (c.stood < 0) continue;   // a stepping stone stays put
                c.stood = on ? c.stood + dt : 0;
                c.dip = Mathf.MoveTowards(c.dip, on ? 0.1f : 0f, dt * 0.5f);
                c.bob = Mathf.Sin(Time.time * 1.8f + c.x0) * 0.035f;
                if (c.t != null)
                {
                    c.t.localPosition = c.home + new Vector3(0, c.bob - c.dip, 0);
                    c.t.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + c.x0) * 4f, 0, 0);
                }
            }
            foreach (var l in ledges)
            {
                if (l.hidden)
                {
                    float cx = (l.x0 + l.x1) / 2;
                    // found once he is up high close by (on the tall pillar, or a jump near it)
                    if (!l.found && Mathf.Abs(hero.x - cx) < 4.6f && hero.y > 1.8f && hero.y > l.top - 4.2f)
                    {
                        l.found = true;
                        CbFx.Spark(new Vector3(cx, l.top, -0.4f), CbFx.Hit, 16, 4, 0.4f, 0.5f, -2);
                        CbFx.Sfx("pickup", 0.6f);
                        if (l.top < 6) g.hud.SetTip("A hidden path! Something is up there...");
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
    }
}
