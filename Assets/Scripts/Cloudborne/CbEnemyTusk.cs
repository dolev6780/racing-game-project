using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Tusk, the armoured boar at the end of stage 1-2 (the story doc: a giant boar in armour, he charges from side to
    /// side, armoured in front; dodge at the right moment, he gets stuck in a tree and you hit him from behind). His
    /// arena on the big bough at the loggers' camp has a great trunk at either end (CbLevel.AddTuskTrees):
    /// <list type="bullet">
    /// <item>He turns to Raja, paws the ground with his head down (a red glow rising on him), and charges.</item>
    /// <item>The charge runs right across the arena: jump over him, or swipe through him (a dodge just as he arrives is
    /// a perfect one). Caught, Raja is thrown. He can't be walked through either: over him or a dodge through him.</item>
    /// <item>At the end of the arena he rams the trunk and his tusks stick in it: stars over his head, his armour no
    /// use while he heaves to pull them out. Now hit him.</item>
    /// <item>Blows from in front glance off his armour; from behind they count at any time.</item>
    /// <item>Below half his health he is angry: a faster charge, less time stuck, and, if Raja is close when he pulls
    /// free, a tusk toss (a white glow first: get away).</item>
    /// </list>
    /// The model (drawn in Canva, built in Meshy) has no rig: he is moved as a whole - the gallop's bob and rock, the
    /// rearing roar, the heave, the fall onto his side.
    /// </summary>
    public partial class CbEnemy
    {
        public enum TuskMove { Roar, Turn, Scrape, Charge, Stuck, Free, Toss }

        public const string TuskPath = "Cloudborne/Treetops/Tusk/Tusk";
        /// <summary>His health, half his length (his hit width), and how far from the arena's middle a charge stops.</summary>
        public const float TuskHp = 220f, TuskHalf = 1.4f, TuskCrash = 9f;
        /// <summary>How fast he charges, and how long his tusks stay stuck in the trunk (both when calm).</summary>
        public const float TuskCharge = 11f, TuskStuckTime = 2.8f;
        const float TuskRoar = 1.4f, TuskTurn = 0.45f, TuskScrape = 0.95f, TuskFree = 0.7f, TuskToss = 1f, TuskTossHit = 0.6f,
            TuskBump = 14f, TuskThrow = 9f, TuskTossDamage = 10f, TuskBack = 1.45f, TuskDying = 1.8f;
        // his model stands on its feet at its origin; it pitches and rolls about a point inside him
        const float TuskPivotY = 0.9f, TuskYawAngle = 100f;

        public TuskMove tuskMove;
        Transform tuskPivot;
        float tuskT, tuskPh, tuskYaw, tuskDust, tuskStars;
        bool tuskAngry, tuskDown;
        Color tuskGlow;

        /// <summary>Below half his health: a faster charge, less time stuck, a tusk toss up close.</summary>
        public bool Angry => kind == Kind.Tusk && hp < max * 0.5f;
        /// <summary>His tusks are stuck in a trunk: the time to hit him.</summary>
        public bool TuskStuck => kind == Kind.Tusk && !dead && tuskMove == TuskMove.Stuck;
        /// <summary>A blow pushing dir glances off his armour: it comes from in front, and his tusks are not stuck.</summary>
        public bool Armoured(int dir) => kind == Kind.Tusk && !dead && dir == -facing && tuskMove != TuskMove.Stuck;

        void SetUpTusk()
        {
            // the hyena rig only places and turns him
            foreach (var r in f.root.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            tuskPivot = CbArt.Joint(f.body, 0, TuskPivotY, 0, "tusk");
            var m = CbArt.Prop(TuskPath, tuskPivot);
            if (m != null)
            {
                m.localPosition = new Vector3(0, -TuskPivotY, 0);
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                    if (!f.mats.Contains(r.sharedMaterial)) f.mats.Add(r.sharedMaterial);
            }
            else
            {
                // stand-in: a dark hide, an iron plate over his brow, two white tusks
                var hide = f.M("#4a3328");
                CbArt.Box(hide, tuskPivot, new Vector3(0, 0.05f, -0.1f), new Vector3(1.4f, 1.5f, 2.6f));
                CbArt.Box(hide, tuskPivot, new Vector3(0, -0.05f, 1.35f), new Vector3(1f, 1f, 0.8f));
                CbArt.Box(f.M("#7d8590"), tuskPivot, new Vector3(0, 0.35f, 1.3f), new Vector3(1.1f, 0.5f, 0.9f));
                var tusk = f.M("#f2ead8");
                foreach (var s in new[] { -0.35f, 0.35f })
                    CbArt.Cone(tusk, tuskPivot, new Vector3(s, -0.35f, 1.8f), 0.09f, 0.6f, Quaternion.Euler(60, 0, 0), 6);
            }
            facing = -1;
            tuskYaw = facing * TuskYawAngle;
            f.body.localRotation = Quaternion.Euler(0, tuskYaw, 0);
            StartTusk(TuskMove.Roar);
        }

        void StartTusk(TuskMove m)
        {
            tuskMove = m;
            tuskT = 0;
            hitDone = false;
        }

        /// <summary>The charge ends against the trunk: a thud, splinters, the tree shaking, his tusks stuck fast.</summary>
        void Crash()
        {
            vx = 0;
            StartTusk(TuskMove.Stuck);
            var at = new Vector3(x + facing * (TuskHalf + 0.15f), y + 0.8f, -0.2f);
            CbFx.Sfx("heavy");
            CbFx.Sfx("block", 0.6f);
            g.shake = Mathf.Max(g.shake, 0.6f);
            g.hitstop = Mathf.Max(g.hitstop, 0.05f);
            CbVfx.Burst(at, CbFx.Hit, 2.2f, 0.18f);
            CbVfx.Ring(new Vector3(at.x, y + 0.05f, 0), CbFx.Dust * 1.5f, 2.4f, 0.35f, ground: true);
            CbFx.Spark(at, new Color(0.62f, 0.45f, 0.3f), 16, 6f, 0.3f, 0.55f, 9, -facing);
            // (over his back: over the trunk it was cut off at the edge of the screen)
            g.hud.Popup(new Vector3(x - facing * 0.6f, y + 2.8f, 0), "THUD!", CbHud.PopStyle.Big);
            g.level?.ShakeTree(at.x, 1f);
        }

        bool UpdateTusk(float dt)
        {
            t += dt;
            tuskT += dt;
            flash = Mathf.Max(0, flash - dt);
            tele = 0;
            Burn(dt);
            var hero = g.hero;
            float pitch = 0, roll = 0, bob = 0;

            if (state == State.Dying)
            {
                // over onto his side with a crash, stars over him, then gone in a puff of smoke (CbGame.EnemyPoof)
                vx = 0;
                float k = Mathf.SmoothStep(0, 1, t / 0.55f);
                roll = 84f * k;
                bob = -0.15f * k;
                if (!tuskDown && t > 0.55f)
                {
                    tuskDown = true;
                    CbFx.Sfx("heavy");
                    g.shake = Mathf.Max(g.shake, 0.5f);
                    CbFx.Spark(new Vector3(x, y + 0.2f, -0.4f), CbFx.Dust, 22, 5, 0.5f, 0.6f, 4);
                    CbVfx.Ring(new Vector3(x, y + 0.05f, 0), CbFx.Dust * 1.6f, 2.8f, 0.4f, ground: true);
                }
                if (tuskDown) Stars(dt, 1.7f);
                if (t > TuskDying - 0.45f) flash = Mathf.Repeat(t, 0.12f) < 0.06f ? 0.06f : 0f;
                if (t > TuskDying) { g.EnemyPoof(this); return true; }
            }
            else
            {
                if (Angry && !tuskAngry)
                {
                    tuskAngry = true;
                    g.hud.Popup(new Vector3(x, y + 3.2f, 0), "TUSK IS ANGRY!", CbHud.PopStyle.Big);
                    CbFx.Sfx("roar", 0.8f);
                }
                switch (tuskMove)
                {
                    case TuskMove.Roar:
                    {
                        // up on his hind legs, a roar that shakes the bough
                        float u = Mathf.Clamp01(tuskT / (TuskRoar * 0.8f));
                        pitch = -22f * Mathf.Sin(u * Mathf.PI);
                        if (!hitDone && tuskT > 0.3f)
                        {
                            hitDone = true;
                            CbFx.Sfx("roar");
                            g.shake = Mathf.Max(g.shake, 0.4f);
                            CbVfx.Ring(new Vector3(x + facing * TuskHalf, y + 1.6f, -0.3f), CbFx.Grey * 0.3f, 2.6f, 0.4f);
                        }
                        if (tuskT >= TuskRoar) StartTusk(TuskMove.Turn);
                        break;
                    }
                    case TuskMove.Turn:
                        vx = 0;
                        FaceTarget();
                        if (tuskT >= TuskTurn) { StartTusk(TuskMove.Scrape); CbFx.Sfx("charge", 0.7f); }
                        break;
                    case TuskMove.Scrape:
                    {
                        // pawing the ground, head down, a red glow rising on him: then the charge (he no longer turns:
                        // get behind him now and he charges at nothing)
                        float scrape = Angry ? TuskScrape * 0.7f : TuskScrape;
                        pitch = 7f + Mathf.Sin(tuskT * 16f) * 3f;
                        bob = -0.04f;
                        tele = 0.55f * Mathf.Clamp01(tuskT / scrape);
                        tuskGlow = new Color(1f, 0.32f, 0.2f);
                        if ((tuskDust -= dt) <= 0)
                        {
                            tuskDust = 0.14f;
                            CbFx.Spark(new Vector3(x - facing * 1.1f, y + 0.1f, -0.3f), CbFx.Dust, 3, 2.5f, 0.35f, 0.4f, 3, -facing);
                        }
                        if (tuskT >= scrape) { StartTusk(TuskMove.Charge); CbFx.Sfx("roar", 0.5f); }
                        break;
                    }
                    case TuskMove.Charge:
                    {
                        float speed = Angry ? TuskCharge * 1.2f : TuskCharge;
                        vx = Mathf.MoveTowards(vx, facing * speed, 40f * dt);
                        tuskPh += dt * 14f;
                        bob = Mathf.Abs(Mathf.Sin(tuskPh)) * 0.14f;
                        pitch = 4f + Mathf.Cos(tuskPh * 2f) * 4f;
                        if (Random.value < 0.6f)
                            CbFx.Spark(new Vector3(x - facing * 1.2f, y + 0.15f, -0.3f), CbFx.Dust, 1, 2f, 0.4f, 0.45f, 1, -facing);
                        // one hit a charge: under him he is thrown ahead (a dodge as he arrives is a perfect one)
                        if (!hitDone && Mathf.Abs(hero.x - x) < TuskHalf + 0.25f && hero.y < y + TuskBack)
                        {
                            hitDone = true;
                            hero.TakeHit(TuskBump, facing, unblockable: true, knock: TuskThrow);
                        }
                        float edge = facing > 0 ? maxX : minX;
                        if (!float.IsInfinity(edge) && (x - edge) * facing >= 0) { x = edge; Crash(); }
                        break;
                    }
                    case TuskMove.Stuck:
                    {
                        // his tusks deep in the trunk, he heaves harder and harder to pull them out, stars over his head
                        float stuck = Angry ? TuskStuckTime * 0.78f : TuskStuckTime;
                        float heave = Mathf.Clamp01(tuskT / stuck);
                        pitch = 12f + Mathf.Sin(tuskT * 26f) * (0.6f + 2.5f * heave * heave);
                        bob = -0.05f;
                        Stars(dt, 2.1f);
                        if (tuskT >= stuck)
                        {
                            StartTusk(TuskMove.Free);
                            CbFx.Sfx("hit", 0.7f);
                            CbFx.Spark(new Vector3(x + facing * (TuskHalf + 0.1f), y + 0.8f, -0.2f), new Color(0.62f, 0.45f, 0.3f), 8, 4f, 0.25f, 0.4f, 9, -facing);
                            g.level?.ShakeTree(x + facing * (TuskHalf + 0.15f), 0.4f);
                        }
                        break;
                    }
                    case TuskMove.Free:
                    {
                        // pulled free, he staggers back a step and shakes his head
                        float k = Mathf.Clamp01(tuskT / TuskFree);
                        vx = -facing * 3f * (1 - k);
                        pitch = 12f * (1 - k) - 6f * Mathf.Sin(k * Mathf.PI);
                        roll = Mathf.Sin(tuskT * 22f) * 5f * (1 - k);
                        if (tuskT >= TuskFree)
                        {
                            vx = 0;
                            StartTusk(Angry && Mathf.Abs(hero.x - x) < TuskHalf + 2.2f ? TuskMove.Toss : TuskMove.Turn);
                        }
                        break;
                    }
                    case TuskMove.Toss:
                    {
                        // angry, with Raja close: he turns on him, a white glow, and flings his tusks up
                        vx = 0;
                        if (tuskT < 0.25f) FaceTarget();
                        if (tuskT < TuskTossHit)
                        {
                            tele = 0.6f * Mathf.InverseLerp(0.15f, TuskTossHit, tuskT);
                            tuskGlow = Color.white;
                            pitch = 12f * tuskT / TuskTossHit;
                        }
                        else
                        {
                            float k = Mathf.Clamp01((tuskT - TuskTossHit) / 0.35f);
                            pitch = 12f - 36f * Mathf.Sin(k * Mathf.PI);
                            if (!hitDone)
                            {
                                hitDone = true;
                                CbFx.Sfx("swing");
                                CbFx.Spark(new Vector3(x + facing * (TuskHalf + 0.4f), y + 0.9f, -0.4f), CbFx.Grey, 5, 4, 0.3f, 0.2f);
                                float ddx = (hero.x - x) * facing;
                                if (ddx > 0 && ddx < TuskHalf + 1.4f && hero.y < y + 1.8f) hero.TakeHit(TuskTossDamage, facing, knock: 6f);
                            }
                        }
                        if (tuskT >= TuskToss) StartTusk(TuskMove.Turn);
                        break;
                    }
                }
            }

            x = Mathf.Clamp(x + vx * dt, minX, maxX);
            if (g.level != null)
            {
                float floor = g.level.SupportAt(x, y + 0.3f);
                y = floor < -0.6f ? 0 : floor;
            }
            // his bulk: Raja can't walk through him - over him with a jump, or through him with a dodge (while he
            // charges, the charge itself is what hits)
            if (state != State.Dying && tuskMove != TuskMove.Charge && hero.state != CbHero.State.Dash && hero.state != CbHero.State.Dead
                && Mathf.Abs(hero.x - x) < TuskHalf + 0.25f && hero.y < y + TuskBack)
            {
                int side = hero.x > x ? 1 : hero.x < x ? -1 : -facing;
                hero.x = x + side * (TuskHalf + 0.25f);
                if (hero.vx * side < 0) hero.vx = 0;
            }
            f.root.localPosition = new Vector3(x, y, 0);
            tuskYaw = Mathf.LerpAngle(tuskYaw, facing * TuskYawAngle, 1 - Mathf.Exp(-(tuskMove == TuskMove.Turn ? 9f : 14f) * dt));
            f.body.localRotation = Quaternion.Euler(0, tuskYaw, 0);
            // rearing, he rises onto his hind legs rather than sinking his rump into the bough
            if (pitch < 0) bob += Mathf.Sin(-pitch * Mathf.Deg2Rad) * 1.2f;
            tuskPivot.localPosition = new Vector3(0, TuskPivotY + bob, 0);
            tuskPivot.localRotation = Quaternion.Euler(pitch, 0, roll);

            if (flash > 0) f.SetFlash(Color.white, 0.45f);
            else if (tele > 0) f.SetFlash(tuskGlow, tele);
            else if (burnT > 0) f.SetFlash(new Color(1f, 0.48f, 0.1f), 0.25f);
            else f.SetFlash(Color.black, 0);
            return false;
        }

        /// <summary>Little stars circling over his head (stuck, or beaten).</summary>
        void Stars(float dt, float high)
        {
            if ((tuskStars -= dt) > 0) return;
            tuskStars = 0.16f;
            float a = g.clock * 7f;
            var head = new Vector3(x + facing * TuskHalf * 0.7f + Mathf.Cos(a) * 0.45f, y + high + Mathf.Sin(a * 2f) * 0.08f, -0.4f + Mathf.Sin(a) * 0.3f);
            CbFx.Spark(head, new Color(1f, 0.9f, 0.4f) * 2.2f, 1, 0.4f, 0.22f, 0.45f, 0);
        }
    }
}
