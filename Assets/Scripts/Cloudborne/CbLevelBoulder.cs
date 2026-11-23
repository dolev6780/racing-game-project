using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Stage 1-1's run (the user: instead of raiders to escape from, a great rolling boulder to run from): off the
    /// mountain's cliff behind Raja, bouncing down, then rolling after him a little slower than he runs. It smashes
    /// the fallen trunks he slides under (their splinters flying), bursts crates and flattens raiders in its way,
    /// and at the river it plunges into the water. Caught by it, he is thrown on, hurt (CbGame's run, CbGameRuns).
    /// </summary>
    public partial class CbLevel
    {
        /// <summary>The boulder's radius.</summary>
        public const float BoulderR = 1.2f;
        // how fast it rolls (he runs 7 m/s), how hard it hits him, how hard a raider
        const float BoulderSpeed = 5.8f, BoulderHit = 22f, BoulderCrush = 40f;

        Transform boulder, boulderSpin;
        float bx, by, bvy, sinkT, boulderHitCd;
        bool rolling, sinking;
        readonly List<CbEnemy> crushed = new List<CbEnemy>();

        /// <summary>The boulder is rolling (or sinking); where it is; it has gone into the river; how often it caught him.</summary>
        public bool BoulderRolling => rolling;
        public float BoulderX => bx;
        public bool BoulderSank { get; private set; }
        public int BoulderHits { get; private set; }

        /// <summary>The boulder tips off the cliff's top at (x, y) and comes after him.</summary>
        public void StartBoulder(float x, float y)
        {
            if (boulder == null)
            {
                boulder = CbArt.Joint(root, x, y, 0.2f, "chaseBoulder");
                boulderSpin = CbArt.Joint(boulder, 0, BoulderR, 0, "spin");
                var holder = CbArt.Joint(boulderSpin, 0, 0, 0, "rock");
                if (Fit(RiverStonePath, holder, new Vector3(-BoulderR, -BoulderR, -BoulderR), new Vector3(BoulderR, BoulderR, BoulderR)) == null)
                    CbArt.Part(CbMesh.Sphere, M("#7a7468"), holder, Vector3.zero, Vector3.one * BoulderR * 2f);
            }
            bx = x; by = y; bvy = 0; sinkT = 0;
            rolling = true; sinking = false;
            BoulderSank = false; BoulderHits = 0;
            crushed.Clear();
            boulder.gameObject.SetActive(true);
            PlaceBoulder();
            CbFx.Sfx("roar", 0.5f);
        }

        /// <summary>The run is over (he fell, or the stage reset): the boulder gone, the trunks it smashed back.</summary>
        public void StopBoulder()
        {
            rolling = sinking = false;
            if (boulder != null) boulder.gameObject.SetActive(false);
            foreach (var o in overhangs)
                if (o.smashed) { o.smashed = false; o.t.gameObject.SetActive(true); }
        }

        void PlaceBoulder()
        {
            boulder.localPosition = new Vector3(bx, by, 0.2f);
        }

        void UpdateBoulder(float dt, CbHero hero)
        {
            if (!rolling) return;
            if (sinking)
            {
                // under the water, a last turn as it goes
                sinkT += dt;
                bx += dt * 1.5f;
                by -= dt * 3.2f;
                boulderSpin.localRotation *= Quaternion.Euler(0, 0, -60f * dt);
                PlaceBoulder();
                if (sinkT > 1.1f) { rolling = false; boulder.gameObject.SetActive(false); BoulderSank = true; }
                return;
            }
            bx += BoulderSpeed * dt;
            // down off the cliff, bouncing, then rolling on the path
            bvy -= 30f * dt;
            by += bvy * dt;
            if (by <= 0f)
            {
                if (bvy < -6f)
                {
                    bvy = -bvy * 0.35f;
                    g.shake = Mathf.Max(g.shake, 0.6f);
                    CbFx.Sfx("heavy", 0.9f);
                    CbFx.Puff(new Vector3(bx, 0.3f, -0.4f), new Color(0.75f, 0.7f, 0.6f), 10, 2.5f, 1.2f, 0.7f);
                }
                else { by = 0f; bvy = 0f; }
            }
            boulderSpin.localRotation *= Quaternion.Euler(0, 0, -BoulderSpeed * dt / BoulderR * Mathf.Rad2Deg);
            PlaceBoulder();
            if (by <= 0.01f)
            {
                g.shake = Mathf.Max(g.shake, 0.12f);
                if (Random.value < 0.6f) CbFx.Spark(new Vector3(bx - BoulderR * 0.6f, 0.1f, -0.3f), CbFx.Dust, 2, 2.5f, 0.45f, 0.45f, 1, -2f);
            }
            float front = bx + BoulderR;
            // into the river: a great splash, and down it goes
            if (by <= 0.01f && InWater(bx + BoulderR * 0.3f))
            {
                sinking = true;
                CbFx.Sfx("splash", 1f);
                CbFx.Spark(new Vector3(bx + BoulderR * 0.5f, WaterLevel + 0.2f, -0.4f), CbFx.Water, 40, 8f, 0.6f, 0.9f, 8);
                g.shake = Mathf.Max(g.shake, 0.5f);
                return;
            }
            // the fallen trunks in its way smashed, their splinters flying
            foreach (var o in overhangs)
                if (!o.smashed && front > o.x0 && bx - BoulderR < o.x1 && o.bottom < by + BoulderR * 2f) Smash(o);
            // crates burst, raiders flattened
            g.items?.ShotCrates(front - 0.2f, 0.5f);
            foreach (var e in g.enemies)
            {
                if (e.dead || crushed.Contains(e) || Mathf.Abs(e.x - bx) > BoulderR + e.w * 0.5f || e.y > by + BoulderR * 2f) continue;
                crushed.Add(e);
                g.HurtEnemy(e, BoulderCrush, 1, 12f, 8f, true, true, false, false);
            }
            // and Raja, if it catches him: thrown on ahead of it
            boulderHitCd = Mathf.Max(0, boulderHitCd - dt);
            if (boulderHitCd <= 0 && hero.state != CbHero.State.Dead && hero.x > bx - BoulderR && hero.x < front + HalfWidth && hero.y < by + BoulderR * 1.7f)
            {
                boulderHitCd = 1f;
                if (hero.inv <= 0) BoulderHits++;
                hero.TakeHit(BoulderHit, 1, unblockable: true, knock: 12f);
                g.hud.Popup(new Vector3(hero.x, 2.6f, 0), "SQUASHED!", CbHud.PopStyle.Hurt);
            }
        }

        /// <summary>A fallen trunk smashed by the boulder: gone in a burst of splinters and dust.</summary>
        void Smash(Overhang o)
        {
            o.smashed = true;
            o.t.gameObject.SetActive(false);
            float mid = (o.x0 + o.x1) / 2;
            var at = new Vector3(mid, (o.bottom + o.top) / 2, -0.4f);
            var wood = M("#8a5a32");
            for (int k = 0; k < 9; k++)
            {
                var chip = CbArt.Part(CbMesh.Box(), wood, g.transform, at + Random.insideUnitSphere * 0.5f,
                    new Vector3(Random.Range(0.25f, 0.7f), Random.Range(0.12f, 0.25f), Random.Range(0.12f, 0.25f)), Random.rotation, shadows: false);
                g.AddDebris(chip, new Vector3(Random.Range(2f, 8f), Random.Range(3f, 8f), Random.Range(-2f, 2f)), Random.Range(-700f, 700f));
            }
            CbFx.Puff(at, new Color(0.8f, 0.7f, 0.55f), 10, 2.5f, 1f, 0.6f);
            CbFx.Sfx("heavy", 1f);
            g.shake = Mathf.Max(g.shake, 0.5f);
        }
    }
}
