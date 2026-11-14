using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Stage 1-2, The Treetops (the story doc: the chase among the branches to the loggers' camp, about 350 m; vines,
    /// thorn walls the staff cuts, swinging logs, saw drones; the staff-throw scroll in a hidden nest; Tusk at the
    /// camp). He runs along great boughs high in the canopy; between them the gaps drop away (TreetopsDrops: falling
    /// in, he loses health and is back on the last bough he stood on). Built from the stage's own parts and 1-1's:
    /// <list type="bullet">
    /// <item>Into the treetops (0-60): a short rope bridge, thorns.</item>
    /// <item>The vine gap (60-90): swinging vines across a wide gap.</item>
    /// <item>The saw drones (90-118): a drone flying to and fro at head height - slide under, jump over or smash it.</item>
    /// <item>Crumbling branches (118-148): ledges across a gap, some cracked, a drone rising and falling between them.</item>
    /// <item>The swinging logs (148-180).</item>
    /// <item>Hand over hand (180-212): bars over a gap; an updraft up to a hidden nest with the staff-throw scroll.</item>
    /// <item>The big bough (212-250): an ambush.</item>
    /// <item>The long bridge (250-288): a long rope bridge, drones flying over it.</item>
    /// <item>Thorny boughs (288-320): thorns, a springy leaf up to a ledge.</item>
    /// <item>The loggers' camp (320-352): the last soldiers (Tusk to come).</item>
    /// </list>
    /// </summary>
    public partial class CbLevel
    {
        public const float TreetopsMax = 352f, TreetopsGoal = 346f;
        static readonly (float x0, float x1)[] TreetopsDrops = { (24f, 31f), (62f, 84f), (122f, 146f), (182f, 210f), (252f, 286f) };
        /// <summary>The hidden nest high over the gap with the bars: its ledge's middle and top (the staff-throw scroll).</summary>
        public const float NestX = 202.5f, NestTop = 9.5f;
        /// <summary>The ambush on the big bough, and the long rope bridge's middle and half length.</summary>
        public const float BoughAmbushX = 230f, LongBridgeX = 269f, LongBridgeHalf = 17.5f;

        void BuildTreetops()
        {
            foreach (var x in new[] { 60f, 118f, 178f, 248f, 318f }) AddLantern(x);
            AddEndGate(CbWorld.LevelMin - HalfWidth - 0.15f - EndGateHalf, 1);
            AddEndGate(CbWorld.LevelMax + HalfWidth + 0.15f + EndGateHalf, -1);

            // ---- into the treetops: a short rope bridge over the first gap, thorns on the bough after it
            AddRopeBridge(27.5f, 4.5f);
            AddThorns(46f);

            // ---- the vine gap
            AddVine(66f, 9f, 5f);
            AddVine(72.5f, 9f, 5f);
            AddVine(79f, 9f, 5f);

            // ---- the saw drones: one flying to and fro at head height along the bough
            AddSaw(new Vector2(96f, 1.35f), new Vector2(110f, 1.35f), 5f, 0f);

            // ---- crumbling branches across a gap, a drone rising and falling between them
            AddLedge(124.6f, 1.6f, 1.2f, false);
            AddLedge(128.6f, 1.4f, 2.2f, true);
            AddLedge(132.6f, 1.6f, 2.8f, false);
            AddLedge(136.6f, 1.4f, 2.2f, true);
            AddLedge(140.6f, 1.6f, 1.6f, false);
            AddLedge(144.2f, 1.2f, 0.8f, false);
            AddSaw(new Vector2(134.6f, 2.2f), new Vector2(134.6f, 5.6f), 3.2f, 0.25f);
            // over them, a high ledge with a tiger token (a double jump up from the ledge below)
            AddLedge(132.2f, 1.2f, 5.4f, false);

            // ---- the swinging logs
            AddPendulum(158f, 6.2f, 4.6f, 2.4f, 0f);
            AddPendulum(166f, 6.2f, 4.6f, 2.4f, 1.2f);

            // ---- hand over hand: bars over the gap, a ledge after them, and from it an updraft up to the hidden nest
            AddBar(184f, 194f, 4.4f);
            AddLedge(196f, 1.6f, 2.2f, false);
            AddDraft(199.2f, 200.6f, 0f, NestTop + 1f);
            AddLedge(NestX, 1.8f, NestTop, false);
            AddLedge(204.4f, 1.4f, 2.4f, false);
            AddLedge(207.8f, 1.4f, 1.2f, false);

            // ---- the big bough (the ambush: CbGame), thorns after it
            AddThorns(244f);

            // ---- the long rope bridge, drones flying over it
            AddRopeBridge(LongBridgeX, LongBridgeHalf);
            AddSaw(new Vector2(257f, 1.4f), new Vector2(268f, 1.4f), 4.2f, 0f);
            AddSaw(new Vector2(270f, 1.6f), new Vector2(281f, 1.6f), 4.2f, 0.5f);

            // ---- thorny boughs: thorns, a springy leaf up to a ledge
            AddThorns(296f);
            AddLeaf(304f, 0.45f);
            AddLedge(306.6f, 1.6f, 6.2f, false);
        }

        /// <summary>A rope bridge over a gap, from cx - half to cx + half.</summary>
        void AddRopeBridge(float cx, float half)
        {
            var bridge = new Crossing { cx = cx, half = half, top = 0.145f, sag = RopeSag, deck = true };
            bridge.t = StringRopeBridge(bridge);
            bridge.home = bridge.t != null ? bridge.t.localPosition : Vector3.zero;
            crossings.Add(bridge);
        }

        // ---------- saw drones ----------

        // A saw drone (drawn in Canva, built in Meshy: its blade and propeller split to spin): it flies to and fro
        // between two points every period seconds, its blade spinning; touching the blade hurts him (sliding passes
        // under one at head height, a jump over it); a paw only clangs off it, two staff blows smash it.
        const string SawDronePath = "Cloudborne/Treetops/SawDrone/SawDrone";
        // the blade's middle under the drone's, its reach, the hurt it does, the score for smashing one
        const float SawBladeDrop = 0.305f, SawReach = 0.42f, SawDamage = 12f;
        const int SawScore = 300;

        class Saw
        {
            public Vector2 a, b;
            public float period, phase, flash;
            public int hp;
            public bool smashed;
            public Transform t, blade, prop;
        }

        readonly List<Saw> saws = new List<Saw>();

        /// <summary>How many saw drones the stage has, and how many are still flying (test hooks).</summary>
        public int SawCount => saws.Count;
        public int SawsFlying => saws.FindAll(s => !s.smashed).Count;
        /// <summary>Where saw drone i's blade is now (test hook).</summary>
        public Vector2 SawBlade(int i) { var p = saws[i].t.position; return new Vector2(p.x, p.y - SawBladeDrop); }

        void AddSaw(Vector2 a, Vector2 b, float period, float phase)
        {
            var t = CbArt.Joint(root, a.x, a.y, 0f, "sawDrone");
            var s = new Saw { a = a, b = b, period = period, phase = phase, hp = 2, t = t };
            var model = CbArt.Prop(SawDronePath, t, CbArt.PropOutline);
            if (model != null)
            {
                foreach (var c in model.GetComponentsInChildren<Transform>())
                {
                    if (c.name == "Saw") s.blade = c;
                    else if (c.name == "Propeller") s.prop = c;
                }
            }
            else
            {
                // stand-in: a rusty barrel, a steel disc under it
                CbArt.Box(CbArt.Toon("#8a5232", CbArt.PropOutline), t, Vector3.zero, new Vector3(0.6f, 0.4f, 0.4f));
                s.blade = CbArt.Part(CbMesh.Cylinder, CbArt.Toon("#b8c0c4", CbArt.PropOutline), t, new Vector3(0, -SawBladeDrop, 0),
                    new Vector3(0.38f, 0.01f, 0.38f), Quaternion.Euler(90, 0, 0));
            }
            saws.Add(s);
        }

        void UpdateTreetops(float dt, CbHero hero)
        {
            float time = g.clock;
            foreach (var s in saws)
            {
                if (s.smashed) continue;
                // to and fro, easing at each end, bobbing a little
                float u = 0.5f - 0.5f * Mathf.Cos((time / s.period + s.phase) * Mathf.PI * 2f);
                var p = Vector2.Lerp(s.a, s.b, u);
                s.t.localPosition = new Vector3(p.x, p.y + Mathf.Sin(time * 3.1f + s.phase * 5f) * 0.06f, 0f);
                if (s.blade != null) s.blade.Rotate(Vector3.forward, -900f * dt, Space.World);
                if (s.prop != null) s.prop.Rotate(Vector3.up, 1400f * dt, Space.World);
                s.flash = Mathf.Max(0, s.flash - dt);
                // the blade cuts him if it meets him (low in a slide, he passes under one at head height)
                float bx = p.x, by = p.y - SawBladeDrop;
                if (Mathf.Abs(hero.x - bx) < SawReach + HalfWidth * 0.5f && hero.y < by + SawReach && hero.y + hero.Height > by - SawReach)
                {
                    hero.TakeHit(SawDamage, hero.x < bx ? -1 : 1);
                    if (Random.value < 0.5f) CbFx.Spark(new Vector3(bx, by, -0.4f), CbFx.Grey, 4, 4, 0.2f, 0.2f, 4);
                }
            }
        }

        bool StrikeSaws(float x, float y, int dir, float range, bool staff)
        {
            foreach (var s in saws)
            {
                if (s.smashed) continue;
                var p = s.t.position;
                float ahead = (p.x - x) * dir;
                if (ahead < -0.4f || ahead > range + 0.5f || p.y - SawBladeDrop - 0.5f > y + 2.2f || p.y < y - 0.2f) continue;
                var at = new Vector3(p.x, p.y - SawBladeDrop * 0.5f, -0.4f);
                if (!staff)
                {
                    // a paw clangs off the steel
                    CbFx.Spark(at, CbFx.Grey, 8, 5, 0.3f, 0.25f, 4);
                    CbFx.Sfx("block", 0.7f);
                    g.hud.SetTip("A saw drone! Smash it with the staff (hold <color=#ffb03a>ATTACK</color>), or slide under it (<color=#ffb03a>swipe ↓</color>)");
                    return true;
                }
                s.hp--;
                s.flash = 0.15f;
                CbFx.Spark(at, CbFx.Hit, 12, 6, 0.35f, 0.35f, 6);
                CbFx.Sfx("heavy", 0.8f);
                g.shake = Mathf.Max(g.shake, 0.25f);
                if (s.hp > 0) return true;
                // smashed: it falls apart in sparks and purple oil, and drops away
                s.smashed = true;
                s.t.gameObject.SetActive(false);
                CbFx.Spark(at, CbFx.Purple, 18, 7, 0.4f, 0.6f, 9);
                CbFx.Puff(at, new Color(0.35f, 0.3f, 0.3f), 8, 1.8f, 0.8f, 0.5f);
                g.score += SawScore;
                g.hud.Popup(new Vector3(p.x, p.y + 0.8f, 0), "SMASHED! +" + SawScore, CbHud.PopStyle.Big);
                return true;
            }
            return false;
        }

        void ResetTreetops()
        {
            foreach (var s in saws) { s.smashed = false; s.hp = 2; s.flash = 0; s.t.gameObject.SetActive(true); }
        }
    }
}
