using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Commander Grak, the boss of stage 1-5 (the story doc: a rhino in iron armour; charges, a giant saw and the burning
    /// arena; three phases). His arena is the Ember Temple's courtyard, a shrine pillar at either end (CbLevelEmber).
    /// <list type="bullet">
    /// <item>The armour (phase 1): his front is iron - blows from in front glance off it. He swings his great saw round
    /// in an arc that ends low in front (jump it, or get away) and brings it down overhead; after each blow he stands
    /// open a moment. From afar
    /// he lowers his head and charges right across the arena (jump or swipe out of it) into the pillar: winded, his
    /// guard down, the time to hit him. Learn to dodge and punish.</item>
    /// <item>The fire (phase 2, from two thirds of his health): he pounds his chest and the arena's edges catch fire and
    /// creep in (beaten back with blows, their heat filling Raja's ki).</item>
    /// <item>The crystal (phase 3, from a third): he fixes the fire crystal to his armour and shoots fire from it (duck,
    /// jump, or swipe into it - the perfect guard sends it back to burn him through his iron) and slams the ground,
    /// two waves of fire running out along it (jump them).</item>
    /// <item>He can't be beaten without Raja's fire: near the end a last slam throws Raja down and knocks the crystal out
    /// of his armour; it rolls to Raja, and at his touch his stripes light up (CbGame.FireAwakens, drawn). Grak, his
    /// armour cracked, staggers - finish him with fire (the tiger flame cannon).</item>
    /// </list>
    /// The model (drawn in Canva, built and rigged in Meshy) carries his saw - a long iron haft with a hammer head and a
    /// spinning blade - made here, in his right fist.
    /// </summary>
    public partial class CbEnemy
    {
        public enum GrakMove { Roar, Stance, Scrape, Charge, Dazed, Swing, Chop, Ignite, Crystal, Fire, Slam, Felled }

        public const string GrakPath = "Cloudborne/Ember/Grak/Grak";
        /// <summary>His health, half his width, and his height (feet to crown).</summary>
        // (bigger: at 3.1 he stood little taller than Raja in the CI's pictures)
        public const float GrakHp = 360f, GrakHalf = 1.15f, GrakHeight = 3.8f;
        /// <summary>How far down his health goes before Raja's fire must wake (his last slam, the crystal knocked loose).</summary>
        public const float GrakAwakeAt = 0.14f;
        /// <summary>How long each of his moves lasts, and when its blow lands (seconds in).</summary>
        // (measured on his clips, the right hand's path in Blender: the saw comes down at 0.86 of Heavy_Hammer_Swing,
        // the chop at 0.59 of Charged_Axe_Chop - a long crouch before it, so only 0.25-0.75 of it plays - the fists hit
        // the ground at 0.6 of Charged_Ground_Slam; the blows landed early, his hand still going up)
        public const float GrakSwingTime = 1.2f, GrakSwingHit = 0.99f, GrakChopTime = 1.5f, GrakChopHit = 1.02f, GrakSlamTime = 1.5f, GrakSlamHit = 0.9f,
            GrakFireTime = 1.7f, GrakDazed = 2.6f;
        const float GrakWalk = 1.7f, GrakChargeSpeed = 10.5f, GrakRoar = 1.7f, GrakScrape = 0.8f, GrakIgniteTime = 1.8f, GrakCrystalTime = 1.6f,
            GrakSwingReach = 3.3f, GrakSwingHigh = 1.25f, GrakSwingDamage = 14f, GrakChopReach = 3.5f, GrakChopDamage = 18f, GrakBump = 16f, GrakThrow = 9f,
            GrakFireballSpeed = 9f, GrakFireballDamage = 10f, GrakWaveSpeed = 9f, GrakWaveLife = 1.3f, GrakWaveDamage = 12f, GrakOpenAfter = 0.85f,
            GrakDying = 2.6f, GrakBack = 2.6f, GrakChargeFrom = 5.5f, GrakCloseIn = 3.4f;
        // (while his hands are out in front of his chest: 0.15-0.6 of Charged_Spell_Cast plays)
        static readonly float[] GrakFireballsAt = { 0.45f, 0.65f, 0.85f };

        public GrakMove grakMove;
        float grakT, openT, grakHurt, walkPh, chargePh, stanceFor, grakStars;
        int grakPhase = 1, fireballs;
        bool crystalOn, awakened, finalSlam, grakDown;
        Color grakGlow;
        Transform saw, sawDisc, chestCrystal;
        class Wave { public float x, life; public int dir; }
        readonly List<Wave> waves = new List<Wave>();

        /// <summary>Which phase of his fight is on: 1 the armour, 2 the fire, 3 the crystal.</summary>
        public int GrakPhase => grakPhase;
        /// <summary>His guard is down: winded against a pillar, just after a blow, or staggering at the end.</summary>
        public bool GrakOpen => kind == Kind.Grak && (grakMove == GrakMove.Dazed || grakMove == GrakMove.Felled || openT > 0);
        /// <summary>The fire crystal is on his armour (phase 3, until his last slam knocks it loose).</summary>
        public bool CrystalOnArmour => crystalOn;
        /// <summary>Raja's fire has woken: now he can be beaten.</summary>
        public bool GrakAwakened => awakened;
        /// <summary>Waves of fire running out along the ground from his slam (test hook).</summary>
        public int FireWaves => waves.Count;

        /// <summary>His health can't go below the mark until Raja's fire wakes (HurtEnemy calls this on every blow).</summary>
        public void HoldOn()
        {
            if (kind == Kind.Grak && !awakened) hp = Mathf.Max(hp, max * GrakAwakeAt);
        }

        /// <summary>Raja's fire has woken (after the drawn moment): Grak, his crystal gone, can be beaten.</summary>
        public void Awaken()
        {
            awakened = true;
            StartGrak(GrakMove.Felled);
        }

        void SetUpGrak()
        {
            foreach (var r in f.body.GetComponentsInChildren<Renderer>(true))
                if (model == null || !r.transform.IsChildOf(model.root)) r.enabled = false;
            if (model != null) f.mats.Add(model.material);
            else
            {
                // stand-in: a big grey body with an iron plate and a horn
                var hide = f.M("#7a7d80");
                var t = CbArt.Joint(f.body, 0, 0, 0, "grakStandIn");
                CbArt.Box(hide, t, new Vector3(0, 1.5f, 0), new Vector3(1.5f, 2.2f, 1.1f));
                CbArt.Box(f.M("#3d434b"), t, new Vector3(0, 1.8f, 0.45f), new Vector3(1.4f, 1.1f, 0.4f));
                CbArt.Cone(f.M("#e8dcc0"), t, new Vector3(0, 2.9f, 0.55f), 0.12f, 0.45f, Quaternion.Euler(-30, 0, 0), 6);
            }
            // his great saw: a long iron haft, a hammer head at its foot, a toothed blade at its head (along +Z)
            saw = CbArt.Joint(f.root, 0, 0, 0, "grakSaw");
            saw.localScale = Vector3.one * 1.25f;
            var iron = f.M("#4a4f57");
            CbArt.Cylinder(f.M("#3a2a1e"), saw, new Vector3(0, 0, 0.3f), 0.05f, 1.9f, Quaternion.Euler(90, 0, 0));
            CbArt.Box(iron, saw, new Vector3(0, 0, -0.62f), new Vector3(0.28f, 0.28f, 0.36f));
            sawDisc = CbArt.Joint(saw, 0, 0, 1.2f, "sawDisc");
            var steel = f.M("#c5ccd4");
            CbArt.Cylinder(steel, sawDisc, Vector3.zero, 0.55f, 0.05f, Quaternion.Euler(0, 0, 90));
            CbArt.Cylinder(iron, sawDisc, Vector3.zero, 0.16f, 0.09f, Quaternion.Euler(0, 0, 90));
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2 / 16;
                CbArt.Cone(steel, sawDisc, new Vector3(0, Mathf.Sin(a) * 0.6f, Mathf.Cos(a) * 0.6f), 0.06f, 0.14f,
                    Quaternion.LookRotation(Vector3.right, new Vector3(0, Mathf.Sin(a), Mathf.Cos(a))), 3);
            }
            facing = -1;
            f.body.localRotation = Quaternion.Euler(0, facing * 98f, 0);
            StartGrak(GrakMove.Roar);
        }

        void StartGrak(GrakMove m)
        {
            grakMove = m;
            grakT = 0;
            hitDone = false;
            fireballs = 0;
            if (m == GrakMove.Stance) stanceFor = Random.Range(0.7f, 1.3f) * (grakPhase == 3 ? 0.8f : 1f);
        }

        /// <summary>The charge ends against a pillar: a crash, the pillar shaking, and he stands winded.</summary>
        void GrakCrash()
        {
            vx = 0;
            StartGrak(GrakMove.Dazed);
            var at = new Vector3(x + facing * (GrakHalf + 0.2f), y + 1.6f, -0.2f);
            CbFx.Sfx("heavy");
            CbFx.Sfx("block", 0.7f);
            g.shake = Mathf.Max(g.shake, 0.7f);
            g.hitstop = Mathf.Max(g.hitstop, 0.06f);
            CbVfx.Burst(at, CbFx.Hit, 2.6f, 0.2f);
            CbVfx.Ring(new Vector3(at.x, y + 0.05f, 0), CbFx.Dust * 1.5f, 2.6f, 0.35f, ground: true);
            CbVfx.Smoke(new Vector3(at.x, y + 1.2f, -0.6f), new Color(0.85f, 0.78f, 0.7f), 3.2f, 0.7f);
            g.hud.Popup(new Vector3(x - facing * 0.6f, y + 3.6f, 0), "CRASH!", CbHud.PopStyle.Big);
            g.level?.PillarHit(x + facing * (GrakHalf + 0.6f));
        }

        bool UpdateGrak(float dt)
        {
            t += dt;
            grakT += dt;
            flash = Mathf.Max(0, flash - dt);
            openT = Mathf.Max(0, openT - dt);
            grakHurt = Mathf.Max(0, grakHurt - dt);
            tele = 0;
            Burn(dt);
            var hero = g.hero;
            float spin = 0;

            if (state == State.Dying)
            {
                vx = 0;
                if (!grakDown && t > 0.9f)
                {
                    grakDown = true;
                    CbFx.Sfx("heavy");
                    g.shake = Mathf.Max(g.shake, 0.6f);
                    CbFx.Spark(new Vector3(x, y + 0.2f, -0.4f), CbFx.Dust, 26, 5, 0.5f, 0.6f, 4);
                    CbVfx.Ring(new Vector3(x, y + 0.05f, 0), CbFx.Dust * 1.6f, 3f, 0.45f, ground: true);
                }
                if (grakDown) GrakStars(dt, 1.2f);
                if (t > GrakDying - 0.45f) flash = Mathf.Repeat(t, 0.12f) < 0.06f ? 0.06f : 0f;
                if (t > GrakDying) { g.EnemyPoof(this); return true; }
            }
            else
            {
                float dx = hero.x - x, dist = Mathf.Abs(dx);
                switch (grakMove)
                {
                    case GrakMove.Roar:
                    case GrakMove.Ignite:
                    {
                        // a pound on his chest and a roar; the second time, the arena's edges catch fire
                        vx = 0;
                        float len = grakMove == GrakMove.Roar ? GrakRoar : GrakIgniteTime;
                        if (!hitDone && grakT > len * 0.45f)
                        {
                            hitDone = true;
                            CbFx.Sfx("roar");
                            g.shake = Mathf.Max(g.shake, 0.45f);
                            CbVfx.Ring(new Vector3(x + facing * 0.6f, y + 2.6f, -0.3f), CbFx.Grey * 0.3f, 3f, 0.45f);
                            if (grakMove == GrakMove.Ignite)
                            {
                                g.level?.LightArenaFire();
                                g.hud.Popup(new Vector3(x, y + 4f, 0), "THE TEMPLE BURNS!", CbHud.PopStyle.Big);
                                g.hud.SetTip("The fire is closing in! Your blows beat it back - and its heat fills your <color=#ffb03a>KI</color>");
                            }
                        }
                        if (grakT >= len) StartGrak(GrakMove.Stance);
                        break;
                    }
                    case GrakMove.Crystal:
                    {
                        // he fixes the crystal to his armour: it flares up on his chest
                        vx = 0;
                        tele = 0.5f * Mathf.Clamp01(grakT / GrakCrystalTime);
                        grakGlow = new Color(1f, 0.45f, 0.1f);
                        if (!hitDone && grakT > GrakCrystalTime * 0.5f)
                        {
                            hitDone = true;
                            crystalOn = true;
                            CbFx.Sfx("fire");
                            CbVfx.Burst(new Vector3(x + facing * 0.4f, y + 2f, -0.6f), CbFx.Fire, 3f, 0.25f);
                            g.hud.Popup(new Vector3(x, y + 4f, 0), "THE FIRE CRYSTAL!", CbHud.PopStyle.Big);
                            g.hud.SetTip("Fire from the crystal: duck it, jump it - or <color=#ffb03a>swipe ↔</color> INTO it to send it back at him. Jump his waves of fire!");
                        }
                        if (grakT >= GrakCrystalTime) StartGrak(GrakMove.Stance);
                        break;
                    }
                    case GrakMove.Stance:
                    {
                        FaceTarget();
                        int want = hp > max * 2f / 3f ? 1 : hp > max / 3f ? 2 : 3;
                        if (want > grakPhase)
                        {
                            grakPhase = want;
                            StartGrak(want == 2 ? GrakMove.Ignite : GrakMove.Crystal);
                            break;
                        }
                        // his last slam, with Raja's fire still asleep
                        if (!awakened && grakPhase == 3 && hp <= max * GrakAwakeAt + 0.01f)
                        {
                            finalSlam = true;
                            StartGrak(GrakMove.Slam);
                            break;
                        }
                        // closing in at a heavy walk
                        float want2 = dist > GrakCloseIn * 0.8f ? Mathf.Sign(dx) * GrakWalk : 0;
                        vx = Mathf.MoveTowards(vx, want2, 12f * dt);
                        if (grakT < stanceFor || hero.state == CbHero.State.Dead || g.mode != CbGame.Mode.Play) break;
                        if (dist > GrakChargeFrom) StartGrak(grakPhase == 3 && Random.value < 0.45f ? GrakMove.Fire : GrakMove.Scrape);
                        else if (grakPhase == 3 && Random.value < 0.35f) StartGrak(dist > 3f ? GrakMove.Fire : GrakMove.Slam);
                        else if (dist < GrakCloseIn) StartGrak(Random.value < 0.55f ? GrakMove.Swing : GrakMove.Chop);
                        else if (grakT > stanceFor + 2.5f) StartGrak(GrakMove.Scrape);
                        break;
                    }
                    case GrakMove.Scrape:
                    {
                        // head down, a red glow rising: then the charge (he no longer turns)
                        vx = 0;
                        tele = 0.55f * Mathf.Clamp01(grakT / GrakScrape);
                        grakGlow = new Color(1f, 0.32f, 0.2f);
                        if (Random.value < 0.4f) CbFx.Spark(new Vector3(x - facing * 0.8f, y + 0.1f, -0.3f), CbFx.Dust, 2, 2.5f, 0.35f, 0.4f, 3, -facing);
                        if (grakT >= GrakScrape) { StartGrak(GrakMove.Charge); CbFx.Sfx("roar", 0.5f); }
                        break;
                    }
                    case GrakMove.Charge:
                    {
                        vx = Mathf.MoveTowards(vx, facing * GrakChargeSpeed * (grakPhase >= 2 ? 1.12f : 1f), 40f * dt);
                        chargePh += dt;
                        spin = 900f;
                        if (Random.value < 0.6f) CbFx.Spark(new Vector3(x - facing * 1f, y + 0.15f, -0.3f), CbFx.Dust, 1, 2f, 0.4f, 0.45f, 1, -facing);
                        // one hit a charge: run down he is thrown ahead (a dodge as he arrives is a perfect one)
                        if (!hitDone && dist < GrakHalf + 0.3f && hero.y < y + GrakBack)
                        {
                            hitDone = true;
                            hero.TakeHit(GrakBump, facing, unblockable: true, knock: GrakThrow);
                        }
                        float edge = facing > 0 ? maxX : minX;
                        if (!float.IsInfinity(edge) && (x - edge) * facing >= 0) { x = edge; GrakCrash(); }
                        break;
                    }
                    case GrakMove.Dazed:
                        // winded against the pillar, stars over his head, his guard down
                        vx = 0;
                        GrakStars(dt, GrakHeight + 0.2f);
                        if (grakT >= GrakDazed * (grakPhase == 3 ? 0.8f : 1f)) StartGrak(GrakMove.Stance);
                        break;
                    case GrakMove.Swing:
                    case GrakMove.Chop:
                    {
                        // the saw: swung round and down in a great arc that ends low in front (jump it), or brought down
                        // overhead (get away)
                        bool chop = grakMove == GrakMove.Chop;
                        float len = chop ? GrakChopTime : GrakSwingTime, hit = chop ? GrakChopHit : GrakSwingHit;
                        vx = 0;
                        if (grakT < 0.25f) FaceTarget();
                        spin = 600f;
                        if (grakT < hit)
                        {
                            tele = 0.6f * Mathf.InverseLerp(0.2f, hit, grakT);
                            grakGlow = Color.white;
                        }
                        else if (!hitDone)
                        {
                            hitDone = true;
                            CbFx.Sfx(chop ? "heavy" : "swing");
                            float ahead = dx * facing;
                            float reach = chop ? GrakChopReach : GrakSwingReach;
                            var at = new Vector3(x + facing * reach * 0.6f, y + (chop ? 0.1f : GrakSwingHigh), -0.4f);
                            CbFx.Spark(at, CbFx.Grey, 10, 6, 0.3f, 0.3f, 4, facing * 2f);
                            if (chop)
                            {
                                g.shake = Mathf.Max(g.shake, 0.5f);
                                CbVfx.Ring(new Vector3(at.x, y + 0.05f, 0), CbFx.Dust * 1.5f, 2.2f, 0.35f, ground: true);
                                // with the crystal on him, the chop sends a wave of fire on along the ground
                                if (crystalOn) waves.Add(new Wave { x = x + facing * reach, dir = facing, life = GrakWaveLife });
                            }
                            if (ahead > -0.3f && ahead < reach && (chop ? hero.y < y + 2.4f : hero.y < y + GrakSwingHigh))
                                hero.TakeHit(chop ? GrakChopDamage : GrakSwingDamage, facing, knock: chop ? 5f : 7f);
                            openT = GrakOpenAfter;
                        }
                        if (grakT >= len) StartGrak(GrakMove.Stance);
                        break;
                    }
                    case GrakMove.Fire:
                    {
                        // fire from the crystal on his chest: three balls at chest height
                        vx = 0;
                        if (grakT < 0.3f) FaceTarget();
                        tele = 0.4f;
                        grakGlow = new Color(1f, 0.5f, 0.1f);
                        if (fireballs < GrakFireballsAt.Length && grakT >= GrakFireTime * GrakFireballsAt[fireballs])
                        {
                            fireballs++;
                            g.level?.ThrowFireball(x + facing * 1.2f, y + 1.35f, facing, GrakFireballSpeed, GrakFireballDamage);
                        }
                        if (grakT >= GrakFireTime) StartGrak(GrakMove.Stance);
                        break;
                    }
                    case GrakMove.Slam:
                    {
                        // both fists on the ground: two waves of fire run out along it (jump them). His last one, with
                        // Raja's fire still asleep, throws Raja down wherever he is and knocks the crystal loose.
                        vx = 0;
                        if (grakT < GrakSlamHit)
                        {
                            tele = 0.6f * Mathf.InverseLerp(0.3f, GrakSlamHit, grakT);
                            grakGlow = finalSlam ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.45f, 0.15f);
                        }
                        else if (!hitDone)
                        {
                            hitDone = true;
                            CbFx.Sfx("heavy");
                            CbFx.Sfx("fire", 0.8f);
                            g.shake = Mathf.Max(g.shake, finalSlam ? 1f : 0.6f);
                            g.hitstop = Mathf.Max(g.hitstop, 0.06f);
                            CbVfx.Ring(new Vector3(x, y + 0.05f, 0), CbFx.Fire * 0.5f, 3.4f, 0.45f, ground: true);
                            CbFx.Spark(new Vector3(x, y + 0.2f, -0.4f), CbFx.Fire, 26, 7, 0.5f, 0.6f, 3);
                            CbVfx.Smoke(new Vector3(x, y + 0.9f, -0.7f), new Color(0.55f, 0.45f, 0.4f), 3.6f, 0.8f);
                            if (finalSlam) LastSlam();
                            else
                            {
                                waves.Add(new Wave { x = x + GrakHalf, dir = 1, life = GrakWaveLife });
                                waves.Add(new Wave { x = x - GrakHalf, dir = -1, life = GrakWaveLife });
                            }
                        }
                        if (grakT >= GrakSlamTime && grakMove == GrakMove.Slam) StartGrak(GrakMove.Stance);
                        break;
                    }
                    case GrakMove.Felled:
                        // the crystal gone, his armour cracked: he staggers, his guard down (finish him)
                        vx = 0;
                        GrakStars(dt, GrakHeight + 0.2f);
                        break;
                }
            }

            // the waves of fire running out along the ground
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                w.x += w.dir * GrakWaveSpeed * dt;
                w.life -= dt;
                if (Random.value < 0.9f) CbFx.Spark(new Vector3(w.x, 0.25f, Random.Range(-0.8f, 0.8f)), CbFx.Fire, 2, 3f, 0.4f, 0.35f, -4, w.dir * 2f);
                if (Mathf.Abs(hero.x - w.x) < 0.5f && hero.y < 0.45f)
                {
                    hero.TakeHit(GrakWaveDamage, w.dir, knock: 5f);
                    w.life = 0;
                }
                if (w.life <= 0 || (g.lockZone != null && Mathf.Abs(w.x - g.lockZone.x) > 10.5f)) waves.RemoveAt(i);
            }

            x = Mathf.Clamp(x + vx * dt, minX, maxX);
            y = 0;
            // his bulk: Raja can't walk through him - over him with a jump, or through him with a dodge
            if (state != State.Dying && grakMove != GrakMove.Charge && hero.state != CbHero.State.Dash && hero.state != CbHero.State.Dead
                && Mathf.Abs(hero.x - x) < GrakHalf + 0.25f && hero.y < y + GrakBack)
            {
                int side = hero.x > x ? 1 : hero.x < x ? -1 : -facing;
                hero.x = x + side * (GrakHalf + 0.25f);
                if (hero.vx * side < 0) hero.vx = 0;
            }
            f.root.localPosition = new Vector3(x, y, 0);
            float target = facing * 98f;
            f.body.localRotation = Quaternion.Euler(0, Mathf.LerpAngle(f.body.localEulerAngles.y, target, 1 - Mathf.Exp(-(grakMove == GrakMove.Charge ? 14f : 9f) * dt)), 0);
            AnimateGrak(dt);
            PlaceSaw(spin, dt);

            if (flash > 0) f.SetFlash(Color.white, 0.45f);
            else if (tele > 0) f.SetFlash(grakGlow, tele);
            else if (burnT > 0) f.SetFlash(new Color(1f, 0.48f, 0.1f), 0.25f);
            else f.SetFlash(Color.black, 0);
            return false;
        }

        /// <summary>His last slam: Raja thrown down wherever he stands, and the crystal knocked out of the armour to him.</summary>
        void LastSlam()
        {
            var hero = g.hero;
            int dir = hero.x >= x ? 1 : -1;
            // (thrown down, not beaten: this is the story's moment)
            hero.hp = Mathf.Max(hero.hp, 30f);
            hero.inv = 0;
            hero.TakeHit(8f, dir, unblockable: true, knock: 7f);
            g.hud.Banner("THE CRYSTAL!", true);
            crystalOn = false;
            var from = chestCrystal != null ? chestCrystal.position : new Vector3(x + facing * 0.5f, y + 2f, -0.4f);
            float toX = Mathf.Clamp(hero.x - dir * 0.9f, x - 9f, x + 9f);
            if (Mathf.Abs(toX - x) < GrakHalf + 0.6f) toX = x + dir * (GrakHalf + 0.8f);
            g.level?.RollCrystal(new Vector3(from.x, from.y, -0.2f), toX);
            StartGrak(GrakMove.Felled);
            g.CrystalLoose();
        }

        /// <summary>Picks the clip, and the moment in it, for what he is doing.</summary>
        void AnimateGrak(float dt)
        {
            if (model == null) return;
            if (state == State.Dying) { model.Window("Knock_Down", 0f, 1f, t / 1.3f); return; }
            if (grakHurt > 0 && (grakMove == GrakMove.Dazed || grakMove == GrakMove.Felled)) { model.Window("Hit_Reaction", 0.1f, 0.6f, 1 - grakHurt / 0.35f); return; }
            switch (grakMove)
            {
                case GrakMove.Roar: model.Window("Chest_Pound_Taunt", 0f, 1f, grakT / GrakRoar); break;
                case GrakMove.Ignite: model.Window("Chest_Pound_Taunt", 0f, 1f, grakT / GrakIgniteTime); break;
                case GrakMove.Crystal: model.Window("Charged_Spell_Cast", 0f, 1f, grakT / GrakCrystalTime); break;
                case GrakMove.Fire: model.Window("Charged_Spell_Cast", 0.15f, 0.6f, grakT / GrakFireTime); break;
                case GrakMove.Scrape: model.Show("Male_Head_Down_Charge", 0.05f, 0.2f); break;
                case GrakMove.Charge: model.LoopSeconds("Male_Head_Down_Charge", chargePh); break;
                case GrakMove.Dazed:
                case GrakMove.Felled: model.LoopSeconds("Catching_Breath", grakT, 0.2f); break;
                case GrakMove.Swing: model.Window("Heavy_Hammer_Swing", 0.2f, 1f, grakT / GrakSwingTime); break;
                case GrakMove.Chop: model.Window("Charged_Axe_Chop", 0.25f, 0.75f, grakT / GrakChopTime); break;
                case GrakMove.Slam: model.Window("Charged_Ground_Slam", 0f, 1f, grakT / GrakSlamTime); break;
                default:
                    if (Mathf.Abs(vx) > 0.3f)
                    {
                        // (paced so his planted foot holds to the ground: measured in CI, the STRIDE test, at the clip's
                        // own pace at his walking speed his foot ran back at 0.79 of his speed - too fast)
                        walkPh += dt * Mathf.Abs(vx) / GrakWalk * 0.56f;
                        model.LoopSeconds("walking_man", walkPh, 0.2f);
                    }
                    else model.LoopSeconds("Axe_Stance", g.clock, 0.2f);
                    break;
            }
        }

        /// <summary>His saw in his right fist (its blade spinning in a blow or a charge), the crystal on his chest.</summary>
        void PlaceSaw(float spin, float dt)
        {
            if (sawDisc != null) sawDisc.localRotation *= Quaternion.Euler(spin * dt, 0, 0);
            if (model != null && saw != null)
            {
                saw.position = model.Palm(model.rightHand);
                saw.rotation = model.Grip(Vector3.forward);
            }
            else if (saw != null)
            {
                saw.localPosition = new Vector3(facing * 0.9f, 1.4f, -0.3f);
                saw.localRotation = Quaternion.Euler(-60, facing * 90, 0);
            }
            if (crystalOn && chestCrystal == null)
            {
                chestCrystal = CbArt.Joint(f.root, 0, 0, 0, "chestCrystal");
                CbArt.Part(CbMesh.Sphere, CbArt.Glow(new Color(1f, 0.45f, 0.1f), CbArt.Shaft), chestCrystal, Vector3.zero, new Vector3(0.32f, 0.5f, 0.32f), shadows: false);
                CbArt.Part(CbMesh.Sphere, CbArt.Glow(new Color(1f, 0.85f, 0.4f), CbArt.Shaft), chestCrystal, Vector3.zero, new Vector3(0.16f, 0.3f, 0.16f), shadows: false);
            }
            if (chestCrystal != null)
            {
                chestCrystal.gameObject.SetActive(crystalOn);
                if (crystalOn)
                {
                    var chest = model != null ? model.chest.position : f.root.position + Vector3.up * 2f;
                    chestCrystal.position = chest + f.body.rotation * new Vector3(0, 0, 0.55f);
                    chestCrystal.rotation = f.body.rotation;
                    if (Random.value < 0.3f) CbFx.Spark(chestCrystal.position + new Vector3(0, 0, -0.2f), CbFx.Fire, 1, 1.2f, 0.25f, 0.4f, -2);
                }
            }
        }

        void GrakStars(float dt, float high)
        {
            if ((grakStars -= dt) > 0) return;
            grakStars = 0.16f;
            float a = g.clock * 7f;
            var head = new Vector3(x + Mathf.Cos(a) * 0.5f, y + high + Mathf.Sin(a * 2f) * 0.08f, -0.4f + Mathf.Sin(a) * 0.3f);
            CbFx.Spark(head, new Color(1f, 0.9f, 0.4f) * 2.2f, 1, 0.4f, 0.22f, 0.45f, 0);
        }
    }
}
