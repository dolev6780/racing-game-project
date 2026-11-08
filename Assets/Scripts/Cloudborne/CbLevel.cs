using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The shape of the stage beyond its flat path: things to climb and things in the way.
    /// <list type="bullet">
    /// <item>Blocks: solid stone (pillars, the cracked wall) Raja can't walk through but can stand on.</item>
    /// <item>Ledges: floating slabs he jumps up through and lands on; a crumbling one gives way under him.</item>
    /// </list>
    /// Stage 1-1: a cracked wall across the path that only the staff (HEAVY) breaks, and after the last fight a
    /// climb of pillars and ledges, the last ones crumbling, to the golden fruit high above the path.
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
            public float stood, respawn;
            public Transform t;
            public Vector3 home;
        }

        public const float HalfWidth = 0.35f;
        // the cracked wall: taller than a double jump, two staff blows break it
        public const float WallX = 60f, WallHeight = 4.6f;
        const float CrumbleAfter = 0.5f, CrumbleBack = 3f;

        readonly CbGame g;
        readonly Transform root;
        readonly List<Block> blocks = new List<Block>();
        readonly List<Ledge> ledges = new List<Ledge>();

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
            // the end of the stage: a shrine gate on the path to walk through
            var goal = CbArt.Joint(root, CbWorld.GoalX, 0, 0.8f, "goalGate");
            if (CbArt.Prop("Cloudborne/Jungle/Gate/Gate", goal, CbArt.PropOutline) is Transform gate) gate.localRotation = Quaternion.Euler(0, 180, 0) * gate.localRotation;
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
            foreach (var l in ledges)
            {
                l.gone = false;
                l.stood = l.respawn = 0;
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

        void AddLedge(float x, float width, float top, bool crumbles)
        {
            // a flat mossy slab of rock (the boulder squashed), its top at `top`
            var t = CbArt.Joint(root, x, top - 0.35f, 0.1f, crumbles ? "crumblingLedge" : "ledge");
            var holder = CbArt.Joint(t, 0, 0, 0, "shape");
            holder.localScale = new Vector3(width / 1.3f, 0.3f, 1.1f);
            if (CbArt.Prop("Cloudborne/Jungle/Boulder/Boulder", holder, CbArt.PropOutline) == null)
                CbArt.Box(CbArt.Toon(crumbles ? "#9a8f7a" : "#7f8a78", CbArt.PropOutline), holder, new Vector3(0, 0.6f, 0), new Vector3(1.3f, 1.2f, 1.2f));
            ledges.Add(new Ledge { x0 = x - width / 2, x1 = x + width / 2, top = top, crumbles = crumbles, t = t, home = t.localPosition });
        }

        // ---------- what the hero's physics asks ----------

        /// <summary>
        /// The highest surface under x that a body at height fromY can stand on: the path (0), a block's top, or a
        /// ledge's top - but only one it was already above (ledges are jumped up through from below).
        /// </summary>
        public float SupportAt(float x, float fromY)
        {
            float best = 0;
            foreach (var b in blocks)
                if (!b.broken && x > b.x0 - HalfWidth * 0.5f && x < b.x1 + HalfWidth * 0.5f && b.top <= fromY + 0.05f && b.top > best) best = b.top;
            foreach (var l in ledges)
                if (!l.gone && x > l.x0 - HalfWidth * 0.5f && x < l.x1 + HalfWidth * 0.5f && l.top <= fromY + 0.05f && l.top > best) best = l.top;
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

        /// <summary>Crumbling ledges: stood on for a moment they shake and drop, and come back a while later.</summary>
        public void Update(float dt, CbHero hero)
        {
            foreach (var l in ledges)
            {
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
