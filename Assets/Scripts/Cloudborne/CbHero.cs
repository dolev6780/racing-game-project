using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>One frame of player input: stick, edges (pressed this frame) and holds.</summary>
    public class CbInput
    {
        public float x, y;                 // stick, y > 0 is up
        // atk: HIT (light), heavy: HEAVY (the staff), kiTap: KI (FIRE once his fire is awake), skill: SPECIAL
        public bool jump, atk, heavy, dash, skill, kiTap, pause, confirm, menu;
        public bool atkDown, heavyDown, guard;   // held
        public float atkHeldT, heavyHeldT;

        public void ClearEdges() { jump = atk = heavy = dash = skill = kiTap = pause = confirm = menu = false; }
    }

    /// <summary>
    /// Raja, the tiger cub. HIT: paw and kick combo (down: a low sweep). HEAVY: the staff (a smash; up: launcher;
    /// in the air: slam; after a dash: thrust; held: whirl), which lives on his back and is drawn only for it.
    /// GUARD blocks blows from the front, down ducks. Ki fills by itself and with every hit; DASH just before a blow
    /// is a perfect dodge.
    /// Until he touches the fire crystal (CbSave.FireAwake) he has no powers, and his ki moves are staff techniques:
    /// KI the tiger lunge, HIT, HIT, KI the staff cyclone, the finisher + KI the staff crash, KI in a smash the quake,
    /// SPECIAL the staff storm. With his fire awake they become FIRE: the fireball, the fire whirl, the blazing palm,
    /// the wave of fire and SPECIAL the tiger flame cannon.
    /// </summary>
    public class CbHero
    {
        public enum State { Idle, Run, Air, Atk, Staff, Blast, Special, Guard, Dash, Hurt, Dead, FireCombo, Hang, Climb, Swim, Down, Victory }
        public enum Move { None, Whirl, Launch, Thrust, Slam, Land, Smash, FireWhirl, BlazePalm, Lunge, Cyclone }

        struct Attack { public float dur, a, b, range, dmg, kb, up; public bool fin; }
        static readonly Attack[] Paw =
        {
            new Attack { dur = 0.22f, a = 0.3f, b = 0.65f, range = 1.7f, dmg = 6, kb = 2 },
            new Attack { dur = 0.22f, a = 0.3f, b = 0.65f, range = 1.7f, dmg = 6, kb = 2 },
            new Attack { dur = 0.3f, a = 0.3f, b = 0.65f, range = 2.0f, dmg = 9, kb = 3 },
            new Attack { dur = 0.38f, a = 0.42f, b = 0.72f, range = 2.1f, dmg = 14, kb = 8, up = 5, fin = true },
            // 4: crouch + HIT, a low sweep that knocks the legs out from under them
            new Attack { dur = 0.42f, a = 0.35f, b = 0.7f, range = 2.2f, dmg = 9, kb = 2, up = 6, fin = true },
        };
        const int Sweep = 4;
        // the double jump is a double forward somersault, tucked
        const float FlipTime = 0.62f, FlipTurns = 2f;
        // HEAVY on the ground: an overhead staff smash; holding HEAVY past SmashToWhirl turns it into the whirl
        const float SmashTime = 0.6f, SmashToWhirl = 0.2f;
        // ki comes back by itself (there is no charging any more) and with every hit
        // (slowly: ki crystals and hits are the real source)
        const float KiRegen = 1.5f, FireCost = 12f, SpecialCost = 50f;
        // SPECIAL: the tiger flame cannon. Gathered for SpecialWindup, then the beam (CbGame.SpecialBeam) runs
        const float SpecialWindup = 0.45f, SpecialTime = 1.35f;
        /// <summary>The special unlocks later in the story (the scroll); on for now so it can be tried.</summary>
        public static bool SpecialUnlocked = true;
        /// <summary>His fire is awake (the fire crystal, 1-5): the ki moves are fire. Before that, staff techniques.</summary>
        public static bool FireAwake => CbSave.FireAwake;
        // The staff techniques (before the fire crystal). The tiger lunge: he shoots forward with the staff out for
        // LungeDrive s, through everything in his way. The cyclone: the staff swept round him, hitting on both sides.
        // The crash: a hop, and the staff brought down with a shock on both sides. The storm (SPECIAL): the staff
        // twirled overhead, a storm of thrusts StormStep apart, then a rising blow that throws them up.
        const float LungeTime = 0.42f, LungeDrive = 0.24f, LungeSpeed = 15f, CycloneTime = 0.8f;
        const float StormWindup = 0.3f, StormFlurry = 0.75f, StormTime = 1.4f, StormStep = 0.075f;
        static readonly Color KiGlow = new Color(1f, 0.85f, 0.55f) * 0.55f;
        const float BlockReact = 0.25f;
        // Fire combos (FIRE in the middle of a combo): after a hit or two a fire whirl (flames wheel round him, hitting
        // all round), after the finisher a point-blank blazing palm, during a staff smash a wave of fire along the ground
        const float ComboFireCost = 15f, FireWhirlTime = 0.8f, BlazePalmTime = 0.45f;
        // custom clips made in Meshy from a text description (text to motion) and put on Raja's rig
        // Fire_Palms: a two-palm thrust from the hip (fully out at 0.38, held to 0.74). Power_Gather: a wide low stance,
        // cupped hands drawn back to the hip. Both turned in Blender to face forward (meshy_character.py --take).
        const string FireClip = "Fire_Palms", GatherClip = "Power_Gather", WhirlClip = "Motion_Whirl", CrouchClip = "Motion_Crouch";
        const float BlastTime = 0.4f;
        // Clips from Meshy's animation library. Hanging from an edge: the catch and the hang of "Jump and Grab Wall",
        // the pull-up of "Ladder Climb Finish" (in place; the game lifts him). Measured on the model at its game size,
        // hanging he has his hands HangReach above his feet and HangAhead in front of him.
        public const float HangReach = 1.43f;
        const float HangAhead = 0.38f, ClimbOnto = 0.45f, CatchTime = 0.18f, ClimbTime = 0.6f, GrabRest = 0.35f;
        // how far through the pull-up he has risen, and come forward, by each moment of it (the clip's own hips)
        static readonly float[] ClimbU = { 0f, 0.083f, 0.25f, 0.417f, 0.583f, 0.75f, 0.917f, 1f };
        static readonly float[] ClimbRise = { 0f, 0.145f, 0.42f, 0.52f, 0.97f, 0.98f, 1f, 1f };
        static readonly float[] ClimbFwd = { 0f, 0.013f, 0.12f, 0.2f, 0.49f, 0.8f, 0.99f, 1f };
        // treading water ("Swim Idle") a moment before he is put back on the bank, the water at his shoulders
        const float SwimTime = 0.9f, SwimDepth = 1.12f;
        // knocked down by a heavy blow: thrown on his back ("Knock Down"), lying a moment, up again ("Stand Up 1")
        const float DownLie = 0.35f, GetUpTime = 0.85f, DownGuard = 2.4f;
        // the stage won: a hop and a fist punched high ("Victory Cheer"), then the fist held up
        const float CheerTime = 2f;
        // Walls: pressing into one in the air he slides down it no faster than WallSlide; JUMP there (or just after
        // leaving it) kicks him up and away from it, and for WallLock the stick can't pull him straight back, then it
        // can: so he climbs a wall, or a shaft, jump by jump (the user was stuck at the foot of the mountain's walls)
        const float WallSlide = 3.5f, WallCoyote = 0.12f, WallLock = 0.15f, WallKickUp = 11f, WallKickOut = 6.5f;

        static readonly Vector3 StaffBackPos = new Vector3(0.02f, 0.32f, -0.3f);
        static readonly Quaternion StaffBackRot = CbArt.Rot(-0.12f, 0, 0.82f);
        static readonly Vector3 StaffHandPos = new Vector3(0, -0.02f, 0.02f);

        public readonly Rig f;
        /// <summary>The modelled Raja; null if it could not be loaded, and then the procedural one shows.</summary>
        public readonly CbModel model;
        const string ModelPath = "Cloudborne/Raja/Raja";
        public float x, y, vx, vy, hp = 100, ki = 40, inv;
        /// <summary>Full health: 100, more with Tiger Heart from Baba's workshop.</summary>
        public float MaxHp => CbSave.MaxHp;
        public int facing = 1;
        public State state = State.Idle;
        public Move move = Move.None;
        public bool empower;
        /// <summary>Ducking (down on the stick, on the ground): moves slowly, a slash passes over him, HIT sweeps.</summary>
        public bool crouching;
        public bool Ducking => crouching && y <= 0.01f && (state == State.Idle || state == State.Run || (state == State.Atk && combo == Sweep));
        public bool Guarding => state == State.Guard;
        /// <summary>Which blow of the HIT combo is out (0 to 3, 4 the sweep).</summary>
        public int Combo => combo;
        /// <summary>Hanging from an edge (his weight on it: a crumbling ledge gives way).</summary>
        public bool Hanging => state == State.Hang;
        /// <summary>Up against a wall in the air (or only just off it): a jump now kicks off it.</summary>
        public bool OnWall => wallT > 0;
        /// <summary>The edge he hangs from or climbs over: its corner, and its top.</summary>
        public float GrabX => grabX;
        public float GrabTop => grabTop;

        /// <summary>
        /// Where a shot leaves him: just past the palm of his right hand (it thrusts forward in the casting clips),
        /// on the play plane. Without the model, about chest high in front of him.
        /// </summary>
        public Vector3 HandPoint()
        {
            if (model == null) return new Vector3(x + facing * 0.9f, y + 1.05f, 0);
            var p = model.Palm(model.rightHand);
            return new Vector3(p.x + facing * 0.15f, p.y, 0);
        }
        public float T => t;

        readonly CbGame g;
        readonly Material furMat;
        readonly Transform staff;
        bool staffInHand;
        float t, buffer, heavyBuffer, flipT, runPh, staffHold, sinceDash = 9, dashCd, glow, tick, ghostT, blockT, crouchT, walkT, stowT;
        // the edge he hangs from (its corner and top, which way it is) and where he hangs; a moment before he can catch
        // one again after letting go; knocked down, the time since he landed on his back
        float grabX, grabTop, hangX, hangY, grabCd, downT;
        int grabDir = 1;
        bool downLanded;
        // against a wall: a moment's grace after leaving it, which side it is on, sliding down it, the kick's lock
        float wallT, wallLockT;
        int wallDir = 1;
        bool sliding;
        // putting the staff away: standing, he swings it up over his shoulder onto his back (the raise of the hammer
        // swing clip, both hands up behind his head); it leaves his hand at StowAt of the way
        const float StowTime = 0.32f, StowAt = 0.6f;
        // below WalkSpeed he walks (a light push on the stick), above it he runs; the walk clip's own pace
        const float WalkSpeed = 4.2f, WalkClipPace = 1.4f;
        int combo, jumps, airHits, hits;
        // the last safe place he stood (ground, a bridge, a steady stone): falling into the water puts him back there
        float safeX, safeY;
        /// <summary>Health lost falling into a stream.</summary>
        public const float WaterDamage = 10f;
        bool air, airDashed, holdUsed, fired, flameSmash, kiCrash;
        readonly HashSet<CbEnemy> hitSet = new HashSet<CbEnemy>();

        public CbHero(CbGame game, Transform parent)
        {
            g = game;
            furMat = CbArt.NewToon(Color.white, CbArt.CharacterOutline, CbArt.Fur);
            furMat.SetTexture("_EmissionMap", CbArt.FurMask);
            f = RigBuilder.Build(new RigStyle
            {
                furMat = furMat, paw = CbArt.Hex("#f08a24"), pants = CbArt.Hex("#2b2433"), belt = CbArt.Hex("#c8323c"), wrap = CbArt.Hex("#efe6d4"),
                chestPatch = CbArt.Hex("#fff4e2"), sash = true, tail = true, claws = true,
            }, "Raja");
            RigBuilder.TigerHead(f);
            f.root.SetParent(parent, false);
            // The rig still places, turns and flips Raja; with the model loaded its own parts are not drawn.
            model = CbModel.Load(ModelPath, 1.75f, f.body);
            if (model != null)
                foreach (var r in f.body.GetComponentsInChildren<Renderer>(true))
                    if (!r.transform.IsChildOf(model.root)) r.enabled = false;
            staff = RigBuilder.Staff();
            staff.SetParent(model != null ? f.body : f.chest, false);
            staff.localPosition = StaffBackPos;
            staff.localRotation = StaffBackRot;

        }

        public void Reset()
        {
            x = y = vx = vy = 0; safeX = safeY = 0; facing = 1; hp = MaxHp; ki = 40; state = State.Idle; t = 0; combo = 0; buffer = 0; air = false; jumps = 0;
            flipT = 0; inv = 0; move = Move.None; staffHold = 0; sinceDash = 9; airDashed = false; dashCd = 0; empower = false; holdUsed = false;
            glow = 0; airHits = 0; grabCd = 0; downT = 0; downLanded = false; wallT = wallLockT = 0; sliding = false;
            f.flip.localRotation = Quaternion.identity;
            SetStaff(false, silent: true);
            if (model == null)
            {
                staff.localPosition = StaffBackPos;
                staff.localRotation = StaffBackRot;
            }
            Place();
        }

        void SetStaff(bool hand, bool silent = false)
        {
            if (staffInHand == hand) return;
            staffInHand = hand;
            if (model == null) staff.SetParent(hand ? f.R.hand : f.chest, true);
            if (!silent) CbFx.Sfx("staff", 0.6f);
        }

        void StartPaw(int i)
        {
            state = State.Atk; combo = i; t = 0; buffer = 0; hitSet.Clear();
            if (!air) vx = facing * 2.5f;
            else { vy = Mathf.Max(vy, 2.5f); airHits++; }
            CbFx.Sfx("swing", 0.7f);
        }

        void StartStaff(Move m)
        {
            state = State.Staff; move = m; t = 0; hitSet.Clear(); buffer = 0; tick = 0; hits = 0; kiCrash = false;
            SetStaff(true);
            if (m == Move.Thrust) vx = facing * 9;
            if (m == Move.Lunge)
            {
                vx = facing * LungeSpeed; ghostT = 0;
                g.hud.SpeedLines(0.5f, 0.3f);
                CbFx.Sfx("dash", 0.7f);
            }
            if (m == Move.Cyclone)
            {
                vx = facing * 0.8f;
                CbVfx.Ring(new Vector3(x, y + 0.05f, 0), CbFx.Hit * 0.5f, 2.6f, 0.4f, ground: true);
                CbFx.Sfx("staff");
            }
            if (m == Move.Launch) vx = facing * 1.5f;
            if (m == Move.Whirl) holdUsed = true;
            if (m == Move.Slam) { vy = 6; vx = facing * 2; }
            if (m == Move.Smash) { vx = facing * 1.2f; fired = false; flameSmash = false; }
        }

        void StartFire(Move m)
        {
            ki -= ComboFireCost;
            state = State.FireCombo; move = m; t = 0; hitSet.Clear(); tick = 0; hits = 0; fired = false;
            vx = facing * (m == Move.BlazePalm ? 3f : 0.8f);
            if (m == Move.FireWhirl) g.FireWheel(this, facing, FireWhirlTime);
            CbFx.Sfx("fire");
        }

        /// <summary>The staff crash (the combo finisher + KI, no fire yet): a hop, then the staff comes down hard.</summary>
        void StartCrash()
        {
            StartStaff(Move.Slam);
            kiCrash = true;
            vy = 9; vx = facing * 3; air = true;
            CbFx.Sfx("jump", 0.6f);
        }

        void EndStaff() { state = air ? State.Air : State.Idle; move = Move.None; staffHold = 0.5f; combo = 0; kiCrash = false; }

        bool MeleeHits(float range, float dmg, float kb, float up, bool big, bool breaker, bool ki = false)
        {
            bool any = false;
            foreach (var e in g.enemies)
            {
                if (e.dead || hitSet.Contains(e)) continue;
                float dx = (e.x - x) * facing;
                if (dx > -0.4f - e.w * 0.5f && dx < range + e.w && Mathf.Abs(e.HitHeight - (y + 1.2f)) < 1.5f)
                {
                    hitSet.Add(e);
                    if (g.HurtEnemy(e, dmg, facing, kb, up, big, breaker, fire: false, fromPlayer: true, ki: ki)) any = true;
                }
            }
            // crates in reach crack and burst too; the cracked wall only gives way to the staff
            if (g.items != null && g.items.HitCrates(x, y, facing, range)) any = true;
            if (g.level != null && g.level.Strike(x, y, facing, range, staff: breaker)) any = true;
            if (any) CbFx.Sfx(big ? "heavy" : "hit");
            return any;
        }

        /// <summary>
        /// He fell into a stream: a splash where he went in, health lost, a moment treading water with his shoulders
        /// under, then back where he last stood safely (LeaveWater).
        /// </summary>
        void FellInWater()
        {
            var at = new Vector3(x, CbLevel.WaterLevel + 0.1f, -0.3f);
            CbFx.Spark(at, CbFx.Water, 26, 7, 0.45f, 0.6f, 12);
            CbVfx.Ring(new Vector3(x, CbLevel.WaterLevel + 0.02f, 0), CbFx.Water * 0.5f, 1.6f, 0.4f, ground: true);
            CbFx.Sfx("splash");
            hp = Mathf.Max(0, hp - WaterDamage);
            g.hud.Popup(new Vector3(x, 2.2f, 0), "-" + Mathf.RoundToInt(WaterDamage), CbHud.PopStyle.Hurt);
            g.hud.FlashHurt();
            g.hud.SetTip("Into the water! Pale stones sink, logs drift - jump on quickly");
            state = State.Swim; t = 0; move = Move.None; combo = 0;
            vx = vy = 0; air = false; jumps = 0; airDashed = false; flipT = 0;
            y = CbLevel.WaterLevel - SwimDepth;
        }

        /// <summary>Out of the water: back where he last stood safely, dripping.</summary>
        void LeaveWater()
        {
            x = safeX; y = safeY; vx = vy = 0; air = false;
            state = State.Idle; t = 0;
            inv = Mathf.Max(inv, 1f);
            CbFx.Spark(new Vector3(x, y + 0.8f, -0.3f), CbFx.Water, 10, 2.5f, 0.3f, 0.45f, 6);
            if (hp <= 0) { state = State.Dead; t = 0; g.Invoke(nameof(CbGame.Defeated), 1.4f); }
        }

        /// <summary>Off a wall: kicked up and away from it, with one more jump in the air after it.</summary>
        void WallJump()
        {
            vy = WallKickUp; vx = -wallDir * WallKickOut; facing = -wallDir;
            wallT = 0; wallLockT = WallLock; sliding = false;
            jumps = 1; airDashed = false; flipT = 0; state = State.Air;
            CbFx.Sfx("jump", 0.7f);
            CbFx.Spark(new Vector3(x + wallDir * CbLevel.HalfWidth, y + 0.3f, -0.3f), CbFx.Dust, 8, 3, 0.45f, 0.35f, 2);
        }

        /// <summary>He catches an edge: hands on its corner, hanging against its face.</summary>
        void StartHang(int dir, float edgeX, float top)
        {
            state = State.Hang; t = 0; move = Move.None; combo = 0;
            facing = grabDir = dir; grabX = edgeX; grabTop = top;
            hangX = edgeX - dir * HangAhead; hangY = top - HangReach;
            x = hangX; y = hangY; vx = vy = 0; air = true; jumps = 0; airDashed = false; airHits = 0; flipT = 0;
            CbFx.Spark(new Vector3(edgeX, top + 0.05f, -0.4f), CbFx.Dust, 6, 2, 0.35f, 0.35f, 3);
            CbFx.Sfx("block", 0.4f);
        }

        /// <summary>Hanging: up, a jump or still pressing towards the edge pulls him up; down or away lets go.</summary>
        void UpdateHang(ref Pose pose, CbInput input)
        {
            x = hangX; y = hangY; vx = vy = 0;
            pose.sLx = -2.9f; pose.sRx = -2.9f; pose.eL = -0.2f; pose.eR = -0.2f; pose.hLx = -0.6f; pose.kL = 0.9f; pose.hRx = -0.4f; pose.kR = 0.7f;
            // a crumbling ledge gave way under his hands
            if (g.level != null && !g.level.EdgeHolds(grabX, grabTop)) { LetGo(); return; }
            if (t < CatchTime) return;
            float toward = input.x * grabDir;
            if (input.y < -0.5f || toward < -0.3f) LetGo();
            else if (toward > 0.3f || input.y > 0.5f || input.jump) { state = State.Climb; t = 0; CbFx.Sfx("jump", 0.4f); }
        }

        void LetGo()
        {
            state = State.Air; t = 0; air = true; jumps = 1; vy = 0; vx = -grabDir * 1.5f;
            grabCd = GrabRest;
        }

        /// <summary>Pulling himself up over the edge: lifted and brought forward along the clip's own path, onto the top.</summary>
        void UpdateClimb(ref Pose pose)
        {
            if (g.level != null && !g.level.EdgeHolds(grabX, grabTop)) { LetGo(); return; }
            float u = Mathf.Clamp01(t / ClimbTime);
            int k = 1;
            while (k < ClimbU.Length - 1 && u > ClimbU[k]) k++;
            float s = Mathf.InverseLerp(ClimbU[k - 1], ClimbU[k], u);
            float rise = Mathf.Lerp(ClimbRise[k - 1], ClimbRise[k], s), fwd = Mathf.Lerp(ClimbFwd[k - 1], ClimbFwd[k], s);
            x = hangX + grabDir * (HangAhead + ClimbOnto) * fwd;
            y = hangY + HangReach * rise;
            vx = vy = 0;
            pose.lean = 0.45f; pose.sLx = -1.3f; pose.sRx = -1.3f; pose.eL = -0.4f; pose.eR = -0.4f;
            pose.hLx = -1.4f * (1 - u); pose.kL = 1.6f * (1 - u); pose.hRx = 0.2f; pose.kR = 0.6f * (1 - u);
            if (u < 1) return;
            x = grabX + grabDir * ClimbOnto; y = grabTop;
            state = State.Idle; t = 0; air = false; jumps = 0; airDashed = false; airHits = 0;
            CbFx.Spark(new Vector3(x, y + 0.05f, -0.3f), CbFx.Dust, 6, 2, 0.4f, 0.35f, 2);
        }

        /// <summary>Knocked down: flying back onto his back, lying a moment (a jump or a blow cuts it short), then up.</summary>
        void UpdateDown(ref Pose pose, CbInput input, float dt)
        {
            pose.lean = -1.2f; pose.bob = -0.4f; pose.headX = -0.4f; pose.sLz = 1.1f; pose.sRz = -1.1f; pose.sLx = 0; pose.sRx = 0;
            pose.hLx = -0.9f; pose.hRx = -0.7f; pose.kL = 0.3f; pose.kR = 0.5f;
            if (!downLanded)
            {
                if (air) return;
                downLanded = true; downT = 0;
                CbFx.Spark(new Vector3(x, y + 0.1f, -0.3f), CbFx.Dust, 12, 3, 0.5f, 0.45f, 3);
                g.shake = Mathf.Max(g.shake, 0.2f);
            }
            vx *= 1 - Damp(10, dt);
            downT += dt;
            if (downT > 0.1f && downT < DownLie && (input.jump || input.atk)) downT = DownLie;
            if (downT >= DownLie + GetUpTime) { state = State.Idle; t = 0; }
        }

        /// <summary>The stage is won: he cheers, a hop and a fist punched high, and keeps his fist up.</summary>
        public void Celebrate()
        {
            if (state == State.Dead) return;
            if (state == State.Hang || state == State.Climb || state == State.Swim) air = true;
            state = State.Victory; t = 0; move = Move.None; combo = 0; flipT = 0;
            SetStaff(false, silent: true);
        }

        /// <summary>
        /// The player was struck. An unblockable blow (a purple flash) goes through the guard; a heavy one (knock, how
        /// hard it throws him) knocks him down on his back, and he can't be hurt again until he is up.
        /// </summary>
        public void TakeHit(float dmg, int dir, bool unblockable = false, float knock = 0f)
        {
            // not in the water, nor halfway over an edge (a blow there would leave him inside the rock)
            if (state == State.Dead || state == State.Victory || state == State.Swim || state == State.Climb || g.mode != CbGame.Mode.Play) return;
            if (state == State.Dash && t < 0.2f) { g.PerfectDodge(); return; }
            if (inv > 0) return;
            // guarding: a blow from in front is stopped (dir is the way it pushes him, so the attacker faces -dir)
            if (state == State.Guard && facing == -dir && !unblockable)
            {
                vx = dir * 3.5f;
                blockT = BlockReact;
                g.Blocked(new Vector3(x + facing * 0.6f, y + 1.2f, -0.4f));
                return;
            }
            hp = Mathf.Max(0, hp - dmg);
            inv = 0.9f;
            vx = dir * 6; vy = 4; air = true;
            state = State.Hurt; t = 0; move = Move.None;
            grabCd = GrabRest;
            if (knock > 0)
            {
                state = State.Down; downLanded = false; downT = 0; combo = 0; flipT = 0;
                vx = dir * knock; vy = 6.5f;
                inv = DownGuard;
            }
            g.shake = Mathf.Max(g.shake, 0.35f);
            g.hitstop = Mathf.Max(g.hitstop, 0.04f);
            CbFx.Spark(new Vector3(x, y + 1.4f, -0.4f), CbFx.Red, 14, 7, 0.4f);
            g.hud.Popup(new Vector3(x, y + 2.6f, 0), "-" + Mathf.RoundToInt(dmg), CbHud.PopStyle.Hurt);
            g.hud.FlashHurt();
            CbFx.Sfx("hurt");
            if (hp <= 0) { state = State.Dead; t = 0; g.Invoke(nameof(CbGame.Defeated), 1.4f); }
        }

        public void Update(float dt, CbInput input)
        {
            t += dt;
            inv = Mathf.Max(0, inv - dt);
            dashCd = Mathf.Max(0, dashCd - dt);
            grabCd = Mathf.Max(0, grabCd - dt);
            wallT = Mathf.Max(0, wallT - dt);
            wallLockT = Mathf.Max(0, wallLockT - dt);
            sinceDash += dt;
            if (input.atk) buffer = 0.25f; else buffer = Mathf.Max(0, buffer - dt);
            if (input.heavy) heavyBuffer = 0.25f; else heavyBuffer = Mathf.Max(0, heavyBuffer - dt);
            if (!input.heavyDown) holdUsed = false;
            if (state != State.Dead) ki = Mathf.Min(100, ki + KiRegen * CbSave.KiRegenMul * dt);
            blockT = Mathf.Max(0, blockT - dt);
            var pose = Pose.Base;
            float rate = 14f;

            switch (state)
            {
                case State.Dead:
                    vx *= 1 - Damp(4, dt);
                    pose.lean = -1.4f; pose.bob = -0.6f; pose.sLz = 1.2f; pose.sRz = -1.2f; pose.sLx = 0; pose.sRx = 0; pose.eL = -0.3f; pose.eR = -0.3f;
                    pose.hLx = -0.8f; pose.hRx = -0.6f; pose.kL = 0.2f; pose.kR = 0.4f; pose.headX = -0.4f;
                    break;
                case State.Hurt:
                    pose.lean = -0.45f; pose.headX = -0.4f; pose.sLz = 1f; pose.sRz = -1f; pose.sLx = 0.4f; pose.sRx = 0.4f; pose.eL = -0.5f; pose.eR = -0.5f;
                    pose.hLx = 0.3f; pose.hRx = -0.3f;
                    if (t > 0.35f && !air) state = State.Idle;
                    break;
                case State.Hang: UpdateHang(ref pose, input); break;
                case State.Climb: UpdateClimb(ref pose); break;
                case State.Swim:
                    pose.sLx = -1.2f; pose.sRx = -1.2f; pose.eL = -0.6f; pose.eR = -0.6f; pose.hLx = -0.4f; pose.kL = 0.8f; pose.hRx = 0.3f; pose.kR = 0.6f;
                    y = CbLevel.WaterLevel - SwimDepth + Mathf.Sin(t * 6f) * 0.05f;
                    if (Random.value < 0.25f) CbFx.Spark(new Vector3(x + Random.Range(-0.5f, 0.5f), CbLevel.WaterLevel + 0.05f, -0.4f), CbFx.Water, 1, 1.2f, 0.25f, 0.35f, 2);
                    if (t > SwimTime) LeaveWater();
                    break;
                case State.Down: UpdateDown(ref pose, input, dt); break;
                case State.Victory:
                    vx *= 1 - Damp(10, dt);
                    pose.sRx = -2.8f; pose.eR = -0.3f; pose.sLx = -0.4f; pose.eL = -1.2f; pose.bob = t < 0.4f ? Mathf.Sin(t / 0.4f * Mathf.PI) * 0.2f : 0;
                    break;
                case State.Atk: UpdatePaw(ref pose, input, ref rate); break;
                case State.Staff: UpdateStaff(ref pose, input, dt, ref rate); break;
                case State.Blast:
                {
                    rate = 28;
                    float u = t / BlastTime;
                    if (u < 0.4f) { pose.twist = -0.6f; pose.sRx = 0.5f; pose.eR = -1.9f; pose.sLx = -0.8f; pose.eL = -1.2f; pose.lean = 0.05f; pose.hLx = -0.5f; pose.kL = 0.45f; pose.hRx = 0.5f; pose.kR = 0.4f; pose.bob = -0.1f; }
                    else { pose.twist = 0.55f; pose.sRx = -1.6f; pose.eR = -0.05f; pose.sRz = 0.05f; pose.sLx = -0.3f; pose.eL = -1.6f; pose.lean = 0.2f; pose.hLx = -0.7f; pose.kL = 0.4f; pose.hRx = 0.6f; pose.kR = 0.3f; pose.bob = -0.12f; }
                    if (u >= 0.4f && !fired) { fired = true; g.FireShot(HandPoint(), facing); }
                    if (u >= 1) state = air ? State.Air : State.Idle;
                    break;
                }
                case State.Special when !FireAwake: UpdateStorm(ref pose, dt, ref rate); break;
                case State.Special:
                {
                    rate = 18;
                    var hands = HandPoint();
                    if (t < SpecialWindup)
                    {
                        // gathering: fire drawn in to his hands, focus lines, a rumble
                        pose.lean = -0.2f; pose.sLx = 0.4f; pose.sRx = 0.4f; pose.eL = -1.6f; pose.eR = -1.6f; pose.hLx = -0.5f; pose.kL = 0.7f; pose.hRx = 0.5f; pose.kR = 0.6f; pose.bob = -0.2f;
                        if (Random.value < 0.8f)
                        {
                            var from = hands + new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(-1.2f, 1.4f), Random.Range(-0.5f, 0.5f));
                            CbFx.Spark(from, Random.value < 0.5f ? CbFx.Fire : CbFx.Flame, 1, 0, 0.3f, 0.3f, 0, (hands.x - from.x) * 3f);
                        }
                        g.hud.SpeedLines(0.3f, 0.12f);
                        g.shake = Mathf.Max(g.shake, 0.06f);
                    }
                    else
                    {
                        pose.lean = 0.3f; pose.sLx = -1.5f; pose.sRx = -1.5f; pose.eL = -0.1f; pose.eR = -0.1f; pose.hLx = -0.8f; pose.kL = 0.5f; pose.hRx = 0.7f; pose.kR = 0.4f; pose.bob = -0.18f;
                        if (!fired) { fired = true; g.SpecialBeam(hands, facing); }
                    }
                    if (t > SpecialTime) state = State.Idle;
                    break;
                }
                case State.FireCombo: UpdateFireCombo(ref pose, dt, ref rate); break;
                case State.Guard:
                    pose.sLx = -1.2f; pose.sRx = -1.0f; pose.eL = -1.9f; pose.eR = -1.9f; pose.lean = 0.1f; pose.hLx = -0.4f; pose.kL = 0.5f; pose.hRx = 0.4f; pose.kR = 0.4f; pose.bob = -0.12f;
                    vx *= 1 - Damp(14, dt);
                    if (!input.guard || air) state = State.Idle;
                    break;
                case State.Dash:
                    rate = 30;
                    pose.lean = 0.65f; pose.sLx = 0.9f; pose.sRx = 0.9f; pose.eL = -0.4f; pose.eR = -0.4f; pose.hLx = -0.9f; pose.kL = 1.1f; pose.hRx = 0.6f; pose.kR = 0.9f; pose.bob = -0.15f;
                    if (air) vy = 0;
                    if (Random.value < 0.9f) CbFx.Spark(new Vector3(x - facing * 0.4f, y + Random.Range(0.4f, 1.8f), -0.1f), CbFx.Flame, 1, 0.5f, 0.45f, 0.25f, 0);
                    // afterimages left behind along the dash
                    ghostT -= dt;
                    if (model != null && ghostT <= 0)
                    {
                        ghostT = 0.04f;
                        CbVfx.Ghost(model.skin, new Color(1.2f, 0.66f, 0.18f, 0.4f), 0.25f);
                    }
                    if (input.atk) buffer = 0.3f;
                    if (input.heavy) heavyBuffer = 0.3f;
                    if (t > 0.2f) { state = air ? State.Air : State.Idle; vx = facing * 4; }
                    break;
            }

            bool free = state == State.Idle || state == State.Run || state == State.Air;
            if (free)
            {
                // down on the stick ducks right where he stands (he can still turn round)
                crouching = !air && input.y < -0.5f;
                crouchT = crouching ? crouchT + dt : 0;
                // just kicked off a wall, the stick can't pull him straight back for a moment
                if (wallLockT <= 0)
                {
                    vx += (input.x * (crouching ? 0f : 7f) - vx) * Damp(air ? 6 : 14, dt);
                    if (Mathf.Abs(input.x) > 0.1f) facing = input.x > 0 ? 1 : -1;
                }
                if (input.jump && air && wallT > 0) WallJump();
                else if (input.jump && jumps < 2)
                {
                    vy = jumps == 0 ? 11.5f : 10f;
                    if (jumps == 1) flipT = FlipTime;
                    jumps++; air = true; state = State.Air;
                    CbFx.Sfx("jump", 0.6f);
                    if (jumps == 1) CbFx.Spark(new Vector3(x, 0.1f, -0.3f), CbFx.Dust, 8, 3, 0.5f, 0.4f, 2);
                }
                if (input.dash && dashCd <= 0 && !(air && airDashed))
                {
                    if (Mathf.Abs(input.x) > 0.2f) facing = input.x > 0 ? 1 : -1;
                    state = State.Dash; t = 0; vx = facing * 16; dashCd = 0.35f; sinceDash = 0; ghostT = 0;
                    g.hud.SpeedLines(0.4f, 0.25f);
                    if (air) airDashed = true;
                    CbFx.Sfx("dash", 0.6f);
                }
                else if (input.guard && !air) { state = State.Guard; t = 0; }
                else if (input.heavy || (heavyBuffer > 0 && sinceDash < 0.35f))
                {
                    heavyBuffer = 0;
                    if (air) StartStaff(Move.Slam);
                    else if (input.y > 0.5f) StartStaff(Move.Launch);
                    else if (sinceDash < 0.35f) StartStaff(Move.Thrust);
                    else StartStaff(Move.Smash);
                }
                else if (input.atk || (buffer > 0 && sinceDash < 0.35f))
                {
                    if (!air && input.y < -0.5f) StartPaw(Sweep);
                    else if (!air || airHits < 3) StartPaw(0);
                }
                else if (input.kiTap && ki >= FireCost)
                {
                    ki -= FireCost;
                    // FIRE: the fireball; before the fire crystal, KI: the tiger lunge
                    if (FireAwake) { state = State.Blast; t = 0; fired = false; if (!air) vx *= 0.3f; else vy = Mathf.Max(vy, 1); }
                    else StartStaff(Move.Lunge);
                }
                else if (input.skill && SpecialUnlocked && ki >= SpecialCost && !air)
                {
                    ki -= SpecialCost; state = State.Special; t = 0; vx = 0; fired = false; tick = 0; hits = 0; hitSet.Clear();
                    // the staff storm starts with the staff drawn and twirled
                    if (!FireAwake) { SetStaff(true); CbFx.Sfx("charge", 0.6f); }
                }
                if (state == State.Idle || state == State.Run) state = Mathf.Abs(vx) > 0.6f ? State.Run : State.Idle;
            }
            else if (state == State.Special) vx *= 1 - Damp(12, dt);
            else if (state == State.Atk || state == State.Blast) vx *= 1 - Damp(8, dt);

            if (state == State.Run)
            {
                runPh += dt * Mathf.Abs(vx) * 1.9f;
                walkT += dt * Mathf.Abs(vx) / WalkClipPace;
                Pose.Run(ref pose, runPh);
                if (Random.value < 0.15f) CbFx.Spark(new Vector3(x - facing * 0.3f, 0.1f, Random.Range(-0.3f, 0.3f)), CbFx.Dust, 1, 1.5f, 0.35f, 0.4f, -1);
            }
            else if (state == State.Air)
            {
                bool up = vy > 0;
                pose.hLx = -1f; pose.kL = up ? 1.6f : 0.9f; pose.hRx = 0.3f; pose.kR = up ? 0.9f : 0.5f; pose.sLz = 0.9f; pose.sRz = -0.9f;
                pose.sLx = -0.3f; pose.sRx = -0.3f; pose.eL = -0.6f; pose.eR = -0.6f; pose.lean = 0.15f;
            }
            else if (state == State.Idle)
            {
                pose.bob = Mathf.Sin(g.clock * 3) * 0.025f - 0.05f;
                pose.sLx += Mathf.Sin(g.clock * 3) * 0.05f;
            }

            // physics: the path, the stone blocks he can't walk through, the ledges he lands on (hanging from an edge,
            // pulling himself up or treading water, the game places him itself)
            if (state != State.Hang && state != State.Climb && state != State.Swim) StepPhysics(input, dt);
            float lo = g.lockZone != null ? g.lockZone.x - 10.5f : CbWorld.LevelMin;
            float hi = g.lockZone != null ? g.lockZone.x + 10.5f : CbWorld.LevelMax;
            x = Mathf.Clamp(x, lo, hi);

            if (flipT > 0)
            {
                flipT = Mathf.Max(0, flipT - dt);
                // eased: a quick tuck-and-turn in the middle, slower out of the jump and into the opening
                float p = 1 - flipT / FlipTime;
                p = p * p * (3 - 2 * p);
                f.flip.localRotation = CbArt.Rot(0, 0, -facing * p * FlipTurns * Mathf.PI * 2);
            }
            else f.flip.localRotation = Quaternion.identity;

            f.Apply(pose, dt, rate);
            Place();
            UpdateStaffPlacement(pose, dt);
            UpdateCloth(dt);
            UpdateFire(dt);
            Animate();
        }

        void StepPhysics(CbInput input, float dt)
        {
            if (state != State.Dash || !air) vy -= 30 * dt;
            float prevX = x, prevY = y;
            y += vy * dt;
            x += vx * dt;
            if (g.level != null) x = g.level.BlockX(prevX, x, y);
            // crates are solid too: walked into, stood on
            if (g.items != null) x = g.items.BlockX(prevX, x, y);
            float floor = g.level != null ? g.level.SupportAt(x, prevY) : 0;
            if (g.items != null) floor = Mathf.Max(floor, g.items.SupportAt(x, prevY));
            // still standing: down a sagging bridge, a bobbing log or off a plank's end he keeps his feet
            if (!air && y > floor && y - floor < 0.3f && vy <= 0.01f) y = floor;
            if (y <= floor)
            {
                if (air && vy < -6) CbFx.Spark(new Vector3(x, floor + 0.1f, -0.3f), CbFx.Dust, 10, 3, 0.5f, 0.4f, 2);
                y = floor; vy = 0;
                if (air)
                {
                    air = false; jumps = 0; airDashed = false; airHits = 0;
                    if (state == State.Air) state = State.Idle;
                    if (state == State.Atk) { state = State.Idle; combo = 0; }
                    if (state == State.Hurt && t > 0.3f) state = State.Idle;
                }
            }
            else air = true;
            if (!air && (g.level == null || g.level.SafeAt(x, y))) { safeX = x; safeY = y; }
            // up against a wall in the air, pressing into it: he slides down it slowly, and a jump kicks off it
            sliding = false;
            if (air && state == State.Air && g.level != null)
            {
                int into = input.x > 0.3f ? 1 : input.x < -0.3f ? -1 : 0;
                if (into != 0 && g.level.WallAt(x, y, into))
                {
                    wallT = WallCoyote; wallDir = into;
                    if (vy < 0)
                    {
                        sliding = true; facing = into;
                        vy = Mathf.Max(vy, -WallSlide);
                        if (Random.value < 0.2f) CbFx.Spark(new Vector3(x + into * CbLevel.HalfWidth, y + 1.2f, -0.3f), CbFx.Dust, 1, 1f, 0.3f, 0.3f, -2);
                    }
                }
            }
            // jumping or falling at an edge he catches it: towards the way he presses, or with the stick let go the way
            // he faces; in a somersault too (the user found he did not always catch it)
            if (state == State.Air && vy < 3.5f && grabCd <= 0 && g.level != null)
            {
                int dir = input.x > 0.3f ? 1 : input.x < -0.3f ? -1 : facing;
                if (g.level.FindEdge(x, y, dir, out float edgeX, out float top)) { StartHang(dir, edgeX, top); return; }
            }
            // into the water: a splash, a moment treading water, and back where he last stood safely
            if (y < CbLevel.WaterLevel - 0.35f && CbLevel.InWater(x)) FellInWater();
        }

        /// <summary>
        /// Picks the modelled Raja's clip, and the moment in it, for the current state and move, so each animation
        /// lasts exactly as long as the game's move and lands its blow when the move deals damage.
        /// </summary>
        void Animate()
        {
            if (model == null) return;
            switch (state)
            {
                // ducking (a clip made in Meshy from a description): drops in 0.12 s into a low crouching guard, knees
                // deep, one foot forward, fists up at his chin (hips from 0.75 to 0.43), then breathes in it
                case State.Idle when crouching:
                case State.Run when crouching:
                    if (crouchT < 0.12f) model.Window(CrouchClip, 0f, 0.12f, crouchT / 0.12f, 0.04f);
                    else model.Show(CrouchClip, 0.15f + Mathf.PingPong((crouchT - 0.12f) * 0.25f, 0.65f));
                    break;
                case State.Idle when stowT > 0: model.Window("Heavy_Hammer_Swing", 0.4f, 0.62f, 1 - stowT / StowTime, 0.06f); break;
                case State.Idle: model.LoopSeconds("Combat_Stance", g.clock); break;
                // a light push on the stick walks, a full one runs (a slow run looked like slow motion)
                case State.Run when Mathf.Abs(vx) < WalkSpeed: model.LoopSeconds("Quick_Walk", walkT); break;
                case State.Run: model.Loop("RunFast", runPh / (Mathf.PI * 2)); break;
                // the somersault shows the tuck of the backflip clip (its hips' turn removed): the game turns him
                // sliding down a wall: clinging to it, arms up and knees in (the reach of "Jump and Grab Wall")
                case State.Air when sliding: model.Show("Jump_and_Grab_Wall", 0.62f, 0.1f); break;
                case State.Air when flipT > 0: model.Window("Backflip_Jump", 0.3f, 0.52f, 1 - flipT / FlipTime, 0.06f); break;
                case State.Air: model.Show("Regular_Jump", vy > 0 ? 0.32f : 0.5f, 0.12f); break;
                case State.Atk:
                {
                    float u = t / Paw[combo].dur;
                    // each strike lands inside its hit window (Paw[].a to .b)
                    if (air) model.Window("Jumping_Punch", 0.25f, 0.6f, u);
                    else if (combo == 0) model.Window("Punch_Combo", 0.25f, 0.5f, u);
                    else if (combo == 1) model.Window("Punch_Combo", 0.5f, 0.75f, u);
                    else if (combo == 2) model.Window("Lunge_Spin_Kick", 0.5f, 0.95f, u);
                    else if (combo == 3) model.Window("Rising_Flying_Kick", 0f, 0.5f, u);
                    else model.Window("Sweep_Kick", 0.38f, 0.62f, u);
                    break;
                }
                case State.Staff:
                    if (move == Move.Whirl) model.Window("Double_Blade_Spin", 0.05f, 0.4f, t / 0.95f);
                    else if (move == Move.Launch) model.Window("Charged_Upward_Slash", 0.12f, 0.45f, t / 0.42f);
                    else if (move == Move.Thrust) model.Window("Thrust_Slash", 0f, 0.25f, t / 0.38f);
                    else if (move == Move.Slam) model.Show("Charged_Ground_Slam", 0.45f);
                    else if (move == Move.Land) model.Window("Charged_Ground_Slam", 0.55f, 0.85f, t / 0.3f);
                    // the overhead swing lands at about 0.85 of the clip, inside the smash's hit window
                    else if (move == Move.Smash) model.Window("Heavy_Hammer_Swing", 0.25f, 0.92f, t / SmashTime);
                    // the lunge: the thrust's stab, held out while he flies forward
                    else if (move == Move.Lunge) model.Window("Thrust_Slash", 0f, 0.25f, Mathf.Min(1, t / 0.2f));
                    // the cyclone: the whole-body spin, the staff swept round with it
                    else if (move == Move.Cyclone) model.Window(WhirlClip, 0.05f, 0.85f, t / CycloneTime);
                    break;
                // the staff storm: a twirl, a storm of stabs (the thrust's stab over and over), the rising blow
                case State.Special when !FireAwake:
                    if (t < StormWindup) model.Window("Double_Blade_Spin", 0.05f, 0.2f, t / StormWindup);
                    else if (t < StormWindup + StormFlurry) model.Window("Thrust_Slash", 0f, 0.25f, ((t - StormWindup) / StormStep) % 1f, 0.02f);
                    else model.Window("Charged_Upward_Slash", 0.12f, 0.45f, (t - StormWindup - StormFlurry) / (StormTime - StormWindup - StormFlurry));
                    break;
                // both palms thrust from the hip: the fireball leaves at 0.4 of the shot, as they reach full stretch
                case State.Blast: model.Window(FireClip, 0.1f, 0.8f, t / BlastTime); break;
                // the special: power gathered low at the hip, then both palms pushed out and held while the beam runs
                case State.Special:
                    if (t < SpecialWindup) model.Window(GatherClip, 0f, 1f, t / SpecialWindup);
                    else if (t < SpecialTime - 0.25f) model.Show(FireClip, 0.5f + Mathf.PingPong(t * 0.3f, 0.1f));
                    else model.Window(FireClip, 0.74f, 1f, (t - (SpecialTime - 0.25f)) / 0.25f);
                    break;
                case State.FireCombo:
                    if (move == Move.FireWhirl) model.Window(WhirlClip, 0.05f, 0.85f, t / FireWhirlTime);
                    else model.Window(FireClip, 0.15f, 0.7f, t / BlazePalmTime);
                    break;
                case State.Guard:
                    if (blockT > 0) model.Window("Block1", 0.3f, 0.45f, 1 - blockT / BlockReact);
                    else model.Show("Block1", 0.1f, 0.06f);
                    break;
                case State.Dash: model.Window("Quick_Step_and_Spin_Dodge", 0.1f, 0.75f, t / 0.2f); break;
                case State.Hurt: model.Window("Hit_Reaction", 0.1f, 0.6f, t / 0.35f); break;
                // hanging from an edge: the catch of "Jump and Grab Wall" (arms flung up, knees drawn in), then its hang,
                // swaying a little; the pull-up: "Ladder Climb Finish" from hands on the top (0.25) to standing (0.85)
                case State.Hang:
                    if (t < CatchTime) model.Window("Jump_and_Grab_Wall", 0.45f, 0.75f, t / CatchTime, 0.05f);
                    else model.Show("Jump_and_Grab_Wall", 0.82f + Mathf.PingPong(t * 0.08f, 0.12f));
                    break;
                case State.Climb: model.Window("Ladder_Climb_Finish", 0.25f, 0.85f, t / ClimbTime, 0.06f); break;
                case State.Swim: model.LoopSeconds("Swim_Idle", t); break;
                // knocked down: flung back onto the ground (Knock_Down), then up from lying on his back (Stand_Up1 from
                // 0.22, lying, to 0.76, standing)
                case State.Down:
                    if (!downLanded) model.Window("Knock_Down", 0f, 0.5f, t / 0.5f);
                    else if (downT < DownLie) model.Window("Knock_Down", 0.5f, 1f, downT / DownLie);
                    else model.Window("Stand_Up1", 0.22f, 0.76f, (downT - DownLie) / GetUpTime, 0.1f);
                    break;
                // the stage won: up to 0.55 of the cheer (the fist high), then holding it up
                case State.Victory:
                    if (t < CheerTime) model.Window("Victory_Cheer", 0.03f, 0.55f, t / CheerTime);
                    else model.Show("Victory_Cheer", 0.5f + Mathf.PingPong((t - CheerTime) * 0.04f, 0.06f));
                    break;
                case State.Dead: model.Window("Knock_Down", 0f, 1f, t / 1.4f); break;
            }
        }

        void UpdatePaw(ref Pose pose, CbInput input, ref float rate)
        {
            rate = 30;
            var A = Paw[combo];
            float u = t / A.dur;
            // KI in the middle of the combo: the staff cyclone after the first hits, the staff crash after the finisher;
            // with his fire awake, the fire whirl and the blazing palm
            if (input.kiTap && !air && combo >= 1 && combo != Sweep && ki >= ComboFireCost)
            {
                if (FireAwake) StartFire(combo >= 3 ? Move.BlazePalm : Move.FireWhirl);
                else
                {
                    ki -= ComboFireCost;
                    if (combo >= 3) StartCrash();
                    else StartStaff(Move.Cyclone);
                }
                return;
            }
            if (u > A.a && u < A.b) MeleeHits(A.range, A.dmg, A.kb, A.up, A.fin, false);
            if (air) { pose.hRx = -1.5f; pose.kR = 0.1f; pose.hLx = 0.2f; pose.kL = 1.2f; pose.lean = -0.3f; pose.sLx = -0.5f; pose.eL = -1.6f; pose.sRx = 0.6f; pose.eR = -1f; }
            else if (combo == 0) { pose.twist = -0.45f; pose.sLx = -1.55f; pose.sLz = 0.08f; pose.eL = -0.1f; pose.sRx = -0.5f; pose.eR = -1.9f; pose.lean = 0.22f; pose.hLx = -0.55f; pose.kL = 0.35f; pose.hRx = 0.45f; pose.kR = 0.3f; pose.bob = -0.1f; }
            else if (combo == 1) { pose.twist = 0.45f; pose.sRx = -1.55f; pose.sRz = -0.08f; pose.eR = -0.1f; pose.sLx = -0.5f; pose.eL = -1.9f; pose.lean = 0.22f; pose.hLx = -0.55f; pose.kL = 0.35f; pose.hRx = 0.45f; pose.kR = 0.3f; pose.bob = -0.1f; }
            else if (combo == 2) { pose.hRx = -1.55f; pose.kR = 0.08f; pose.hLx = 0.15f; pose.kL = 0.25f; pose.lean = -0.3f; pose.twist = 0.3f; pose.sLx = -0.6f; pose.eL = -1.7f; pose.sRx = 0.5f; pose.eR = -1.1f; pose.bob = 0.04f; }
            else
            {
                float k = u < 0.42f ? u / 0.42f : 1;
                pose.twist = 0.5f - k * 1.1f; pose.sLx = -2.7f + k * 1.9f; pose.sLz = 0.5f - k * 0.7f; pose.eL = -0.25f; pose.sRx = -0.4f; pose.eR = -1.6f;
                pose.lean = 0.38f; pose.hLx = -0.8f; pose.kL = 0.45f; pose.hRx = 0.55f; pose.kR = 0.3f; pose.bob = -0.14f;
                if (u > 0.42f && u < 0.6f && Random.value < 0.8f) CbFx.Spark(new Vector3(x + facing * Random.Range(0.6f, 1.4f), y + Random.Range(0.9f, 1.9f), -0.5f), CbFx.Hit, 1, 1, 0.25f, 0.15f, 0);
            }
            if (u >= 1)
            {
                if (buffer > 0 && combo < 3 && (!air || airHits < 3)) StartPaw(combo + 1);
                else { state = air ? State.Air : State.Idle; combo = 0; }
            }
            else if (u > 0.6f && buffer > 0 && combo < 3 && (!air || airHits < 3)) StartPaw(combo + 1);
        }

        void UpdateFireCombo(ref Pose pose, float dt, ref float rate)
        {
            rate = 30;
            vx *= 1 - Damp(6, dt);
            if (move == Move.FireWhirl)
            {
                float u = t / FireWhirlTime;
                pose.sLz = 1.3f; pose.sRz = -1.3f; pose.eL = -0.2f; pose.eR = -0.2f; pose.hLx = -0.5f; pose.kL = 0.6f; pose.hRx = 0.5f; pose.kR = 0.5f; pose.bob = -0.15f;
                pose.twist = t * 30;
                // the flames hit everything close round him, every 0.12 s, the last time hard
                tick -= dt;
                if (tick <= 0 && hits < 6)
                {
                    tick = 0.12f; hits++;
                    bool last = hits == 6;
                    foreach (var e in g.enemies)
                    {
                        if (e.dead || Mathf.Abs(e.x - x) > 2.4f + e.w * 0.5f || Mathf.Abs(e.HitHeight - (y + 1f)) > 1.8f) continue;
                        g.HurtEnemy(e, last ? 12 : 5, e.x >= x ? 1 : -1, last ? 7 : 1, last ? 5 : 0.5f, last, true, fire: true, fromPlayer: true);
                    }
                }
                if (u >= 1) { state = State.Idle; move = Move.None; combo = 0; }
            }
            else
            {
                float u = t / BlazePalmTime;
                pose.sLx = -1.5f; pose.sRx = -1.5f; pose.eL = -0.1f; pose.eR = -0.1f; pose.lean = 0.35f; pose.hLx = -0.8f; pose.kL = 0.5f; pose.hRx = 0.7f; pose.kR = 0.3f; pose.bob = -0.15f;
                if (u > 0.42f && !fired)
                {
                    // point blank, as the palms reach full stretch: an explosion of fire right off them
                    fired = true;
                    g.BlazePalm(HandPoint() + new Vector3(facing * 0.9f, 0, 0), facing);
                }
                if (u >= 1) { state = State.Idle; move = Move.None; combo = 0; }
            }
        }

        void UpdateStaff(ref Pose pose, CbInput input, float dt, ref float rate)
        {
            rate = 30;
            switch (move)
            {
                case Move.Smash:
                {
                    float u = t / SmashTime;
                    pose.sRx = -2.8f + Mathf.Clamp01((u - 0.6f) / 0.25f) * 2.6f; pose.sLx = pose.sRx; pose.eR = -0.2f; pose.eL = -0.2f;
                    pose.lean = 0.1f + Mathf.Clamp01((u - 0.6f) / 0.25f) * 0.4f; pose.hLx = -0.6f; pose.kL = 0.5f; pose.hRx = 0.5f; pose.kR = 0.4f; pose.bob = -0.15f;
                    vx *= 1 - Damp(5, dt);
                    // HEAVY still held after the wind-up starts: the swing becomes the staff whirl
                    if (input.heavyDown && t > SmashToWhirl && !holdUsed) { StartStaff(Move.Whirl); break; }
                    // KI while swinging: the smash sends the quake, a ripple of broken ground, ahead of him; with his fire
                    // awake the staff blazes up and it is a wave of fire
                    if (input.kiTap && !flameSmash && !fired && ki >= ComboFireCost) { ki -= ComboFireCost; flameSmash = true; CbFx.Sfx(FireAwake ? "fire" : "charge", 0.7f); }
                    if (flameSmash && Random.value < 0.8f)
                        CbFx.Spark(staff.position, FireAwake ? (Random.value < 0.5f ? CbFx.Fire : CbFx.Flame) : CbFx.Hit, 1, 1.5f, 0.4f, 0.3f, -2);
                    if (u > 0.78f && u < 0.95f) MeleeHits(2.6f, 16, 6, 3, true, true, ki: flameSmash);
                    if (u > 0.82f && !fired)
                    {
                        // the staff hits the ground: dust, a shock ring, a jolt
                        fired = true;
                        var at = new Vector3(x + facing * 1.5f, 0.05f, 0);
                        CbFx.Spark(at + new Vector3(0, 0.1f, -0.4f), CbFx.Dust, 14, 5, 0.6f, 0.5f, 3);
                        CbVfx.Ring(at, CbFx.Hit * 0.5f, 2.4f, 0.35f, ground: true);
                        g.shake = Mathf.Max(g.shake, 0.3f);
                        CbFx.Sfx("heavy", 0.7f);
                        if (flameSmash && FireAwake) g.FlameWave(at, facing);
                        else if (flameSmash) g.QuakeWave(at, facing);
                    }
                    if (u >= 1) EndStaff();
                    break;
                }
                case Move.Whirl:
                {
                    float u = t / 0.95f;
                    pose.sRx = -1.45f; pose.sRz = -0.05f; pose.eR = -0.1f; pose.sLx = -1.2f; pose.eL = -0.7f; pose.twist = 0.15f; pose.lean = 0.18f;
                    pose.hLx = -0.55f; pose.kL = 0.4f; pose.hRx = 0.45f; pose.kR = 0.3f; pose.bob = -0.12f;
                    pose.spin = t * 26;
                    vx = facing * 1.6f;
                    tick -= dt;
                    if (tick <= 0 && hits < 5)
                    {
                        tick = 0.16f; hits++;
                        hitSet.Clear();
                        bool last = hits == 5;
                        MeleeHits(2.7f, last ? 12 : 7, last ? 9 : 1.2f, last ? 5 : 0.5f, last, true);
                        CbFx.Sfx("staff", 0.5f);
                    }
                    if (Random.value < 0.6f) CbFx.Spark(new Vector3(x + facing * Random.Range(0.6f, 1.6f), y + Random.Range(0.6f, 2.2f), -0.5f), CbFx.Hit, 1, 1, 0.2f, 0.12f, 0);
                    if (u >= 1) EndStaff();
                    break;
                }
                case Move.Launch:
                {
                    float u = t / 0.42f, k = u < 0.3f ? 0 : Mathf.Min(1, (u - 0.3f) / 0.25f);
                    pose.sRx = 0.6f - k * 3.1f; pose.sRz = -0.15f; pose.eR = -0.2f; pose.sLx = -0.4f; pose.eL = -1.5f; pose.twist = -0.3f + k * 0.7f;
                    pose.lean = 0.25f - k * 0.45f; pose.hLx = -0.6f; pose.kL = 0.5f - k * 0.3f; pose.hRx = 0.5f; pose.kR = 0.4f; pose.bob = -0.18f + k * 0.15f;
                    if (u > 0.3f && u < 0.65f) MeleeHits(2.4f, 10, 1, 13, false, true);
                    if (u >= 1) EndStaff();
                    break;
                }
                case Move.Thrust:
                {
                    float u = t / 0.38f;
                    pose.sRx = -1.57f; pose.sRz = 0; pose.eR = 0; pose.sLx = -0.9f; pose.eL = -1.2f; pose.twist = 0.65f; pose.lean = 0.45f;
                    pose.hLx = -1f; pose.kL = 0.5f; pose.hRx = 0.75f; pose.kR = 0.2f; pose.bob = -0.2f; pose.staffX = Mathf.PI / 2;
                    vx *= 1 - Damp(6, dt);
                    if (u > 0.22f && u < 0.6f) MeleeHits(3.6f, 12, 7, 1, false, true);
                    if (u >= 1) EndStaff();
                    break;
                }
                case Move.Lunge:
                {
                    float u = t / LungeTime;
                    pose.sRx = -1.57f; pose.sRz = 0; pose.eR = 0; pose.sLx = -0.9f; pose.eL = -1.2f; pose.twist = 0.65f; pose.lean = 0.55f;
                    pose.hLx = -1.1f; pose.kL = 0.5f; pose.hRx = 0.8f; pose.kR = 0.2f; pose.bob = -0.25f; pose.staffX = Mathf.PI / 2;
                    if (t < LungeDrive) { vx = facing * LungeSpeed; if (air) vy = 0.5f; }
                    else vx *= 1 - Damp(10, dt);
                    // through everything in his way, each once
                    if (u < 0.8f) MeleeHits(1.9f, 14, 8, 2.5f, true, true, ki: true);
                    // afterimages and a streak of dust behind him
                    ghostT -= dt;
                    if (model != null && ghostT <= 0 && t < LungeDrive)
                    {
                        ghostT = 0.035f;
                        CbVfx.Ghost(model.skin, new Color(1.2f, 1f, 0.75f, 0.4f), 0.22f);
                    }
                    if (Random.value < 0.8f) CbFx.Spark(new Vector3(x - facing * 0.5f, y + Random.Range(0.1f, 0.4f), -0.2f), CbFx.Dust, 1, 2, 0.45f, 0.35f, -1, -facing * 3);
                    if (u >= 1) EndStaff();
                    break;
                }
                case Move.Cyclone:
                {
                    float u = t / CycloneTime;
                    pose.sRx = -1.4f; pose.sRz = -0.6f; pose.eR = -0.1f; pose.sLz = 1.2f; pose.eL = -0.3f; pose.hLx = -0.5f; pose.kL = 0.6f;
                    pose.hRx = 0.5f; pose.kR = 0.5f; pose.bob = -0.15f; pose.twist = t * 30; pose.spin = t * 30;
                    vx *= 1 - Damp(6, dt);
                    // the staff sweeps everything close round him, every 0.12 s, the last time hard
                    tick -= dt;
                    if (tick <= 0 && hits < 6)
                    {
                        tick = 0.12f; hits++;
                        bool last = hits == 6;
                        foreach (var e in g.enemies)
                        {
                            if (e.dead || Mathf.Abs(e.x - x) > 2.5f + e.w * 0.5f || Mathf.Abs(e.HitHeight - (y + 1f)) > 1.8f) continue;
                            g.HurtEnemy(e, last ? 12 : 5, e.x >= x ? 1 : -1, last ? 7 : 1, last ? 5 : 0.5f, last, true, fire: false, fromPlayer: true, ki: true);
                        }
                        if (g.items != null) { g.items.HitCrates(x, y, 1, 2.5f); g.items.HitCrates(x, y, -1, 2.5f); }
                        CbFx.Sfx("staff", 0.5f);
                    }
                    // a swirl of dust and leaves round him
                    if (Random.value < 0.9f)
                    {
                        float a = Random.value * Mathf.PI * 2;
                        var p = new Vector3(x + Mathf.Cos(a) * 1.9f, y + Random.Range(0.3f, 2f), Mathf.Sin(a) * 0.6f - 0.3f);
                        CbFx.Spark(p, Random.value < 0.6f ? CbFx.Dust : CbFx.Hit, 1, 2, 0.35f, 0.3f, 0, -Mathf.Sin(a) * 7);
                    }
                    if (u >= 1) EndStaff();
                    break;
                }
                case Move.Slam:
                    pose.sRx = -3f; pose.sLx = -3f; pose.sLz = 0.2f; pose.sRz = -0.2f; pose.eR = -0.2f; pose.eL = -0.2f; pose.lean = -0.1f;
                    pose.hLx = -1.2f; pose.kL = 1.4f; pose.hRx = -0.2f; pose.kR = 1.2f; pose.staffX = 0.4f;
                    // the crash hops higher first, then comes down
                    if (t > (kiCrash ? 0.24f : 0.15f)) vy = Mathf.Min(vy, -24);
                    if (!air && t > 0.05f)
                    {
                        // the crash: harder and wider, a shock on both sides
                        float reach = kiCrash ? 3.8f : 3f;
                        foreach (var e in g.enemies)
                        {
                            if (e.dead || Mathf.Abs(e.x - x) > reach + e.w || e.HitHeight - y > 2.6f) continue;
                            g.HurtEnemy(e, kiCrash ? 22 : 14, e.x >= x ? 1 : -1, kiCrash ? 8 : 5, kiCrash ? 9 : 7, true, true, fire: false, fromPlayer: true, ki: kiCrash);
                        }
                        if (g.level != null) g.level.Strike(x, y, facing, 2f, staff: true);
                        if (kiCrash && g.items != null) { g.items.HitCrates(x, y, 1, reach); g.items.HitCrates(x, y, -1, reach); }
                        CbFx.Spark(new Vector3(x + facing * 1.1f, 0.1f, -0.4f), CbFx.Dust, kiCrash ? 30 : 18, 6, 0.6f, 0.5f, 4);
                        CbFx.Spark(new Vector3(x + facing * 1.1f, 0.2f, -0.4f), FireAwake ? CbFx.Fire : CbFx.Hit, 10, 6, 0.4f, 0.4f, 3);
                        g.shake = Mathf.Max(g.shake, kiCrash ? 0.55f : 0.4f);
                        CbFx.Sfx("heavy");
                        CbVfx.Ring(new Vector3(x + facing * 1.1f, 0.05f, 0), CbFx.Hit * 0.6f, kiCrash ? 4.6f : 3.5f, 0.4f, ground: true);
                        if (kiCrash) CbVfx.Ring(new Vector3(x, 0.05f, 0), CbFx.Dust * 0.6f, 5.5f, 0.5f, ground: true);
                        CbVfx.Burst(new Vector3(x + facing * 1.1f, 0.3f, -0.4f), CbFx.Hit, 2.6f, 0.18f);
                        g.hud.ImpactFlash(0.3f);
                        move = Move.Land; t = 0;
                    }
                    break;
                case Move.Land:
                    pose.sRx = -0.7f; pose.sLx = -0.7f; pose.eR = -0.1f; pose.eL = -0.1f; pose.lean = 0.75f; pose.hLx = -1f; pose.kL = 1.3f; pose.hRx = 0.6f; pose.kR = 1f;
                    pose.bob = -0.35f; pose.staffX = 0.4f;
                    if (t > 0.3f) EndStaff();
                    break;
            }
        }

        /// <summary>
        /// SPECIAL before the fire crystal, the staff storm: the staff twirled overhead (dust drawn in), a storm of
        /// thrusts at everything in front, StormStep apart, then a rising blow that throws them all up.
        /// </summary>
        void UpdateStorm(ref Pose pose, float dt, ref float rate)
        {
            rate = 30;
            vx *= 1 - Damp(10, dt);
            float flurryEnd = StormWindup + StormFlurry;
            if (t < StormWindup)
            {
                pose.sRx = -2.6f; pose.sLx = -2.4f; pose.eR = -0.3f; pose.eL = -0.4f; pose.hLx = -0.5f; pose.kL = 0.7f; pose.hRx = 0.5f; pose.kR = 0.6f;
                pose.bob = -0.2f; pose.spin = t * 34;
                if (Random.value < 0.7f)
                {
                    var to = new Vector3(x, y + 1.2f, -0.3f);
                    var from = to + new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(-1f, 1.2f), Random.Range(-0.4f, 0.4f));
                    CbFx.Spark(from, CbFx.Dust, 1, 0, 0.3f, 0.3f, 0, (to.x - from.x) * 3f);
                }
                g.hud.SpeedLines(0.3f, 0.12f);
                g.shake = Mathf.Max(g.shake, 0.05f);
            }
            else if (t < flurryEnd)
            {
                float k = ((t - StormWindup) / StormStep) % 1f;
                pose.sRx = -1.57f; pose.eR = 0; pose.sLx = -0.9f; pose.eL = -1.2f; pose.twist = 0.4f + Mathf.Sin(k * Mathf.PI) * 0.35f; pose.lean = 0.4f;
                pose.hLx = -1f; pose.kL = 0.5f; pose.hRx = 0.75f; pose.kR = 0.2f; pose.bob = -0.2f; pose.staffX = Mathf.PI / 2;
                vx = facing * 0.8f;
                tick -= dt;
                if (tick <= 0)
                {
                    tick = StormStep; hits++;
                    hitSet.Clear();
                    MeleeHits(3.2f, 4, 0.6f, 0.4f, false, true, ki: true);
                    // a white streak where the staff's tip stabs
                    var tip = new Vector3(x + facing * Random.Range(1.6f, 3f), y + Random.Range(0.8f, 1.7f), -0.5f);
                    CbFx.Spark(tip, CbFx.Hit, 3, 3, 0.25f, 0.15f, 0, facing * 8);
                    CbFx.Sfx("swing", 0.35f);
                }
                g.hud.SpeedLines(0.25f, 0.1f);
            }
            else
            {
                float k = Mathf.Min(1, (t - flurryEnd) / 0.15f);
                pose.sRx = 0.6f - k * 3.1f; pose.sRz = -0.15f; pose.eR = -0.2f; pose.sLx = -0.4f; pose.eL = -1.5f; pose.twist = -0.3f + k * 0.7f;
                pose.lean = 0.25f - k * 0.45f; pose.hLx = -0.6f; pose.kL = 0.5f - k * 0.3f; pose.hRx = 0.5f; pose.kR = 0.4f; pose.bob = -0.18f + k * 0.15f;
                if (!fired && t > flurryEnd + 0.08f)
                {
                    // the rising blow
                    fired = true;
                    hitSet.Clear();
                    MeleeHits(3.4f, 22, 6, 13, true, true, ki: true);
                    var at = new Vector3(x + facing * 1.8f, y + 1.2f, -0.5f);
                    CbVfx.Ring(at, CbFx.Hit * 0.6f, 3f, 0.4f);
                    CbVfx.Burst(at, CbFx.Hit, 2.8f, 0.2f);
                    CbFx.Spark(at, CbFx.Hit, 20, 9, 0.4f, 0.4f, 2, facing * 4);
                    g.hud.ImpactFlash(0.4f);
                    g.shake = Mathf.Max(g.shake, 0.4f);
                    CbFx.Sfx("heavy");
                }
            }
            if (t > StormTime) { state = State.Idle; staffHold = 0.5f; }
        }

        void Place()
        {
            f.root.localPosition = new Vector3(x, y, 0);
            // turned from side-on towards the camera (which looks along +Z): the modelled Raja shows his profile
            float target = facing * (90f + (model != null ? 8f : 25f));
            float cur = f.body.localEulerAngles.y;
            f.body.localRotation = Quaternion.Euler(0, Mathf.LerpAngle(cur, target, 1 - Mathf.Exp(-18f * Time.deltaTime)), 0);
        }

        void UpdateStaffPlacement(Pose pose, float dt)
        {
            // the staff storm holds the staff too
            bool holding = state == State.Staff || (state == State.Special && !FireAwake);
            if (!holding)
            {
                staffHold = Mathf.Max(0, staffHold - dt);
                if (stowT > 0)
                {
                    // the put-away swing runs its course; the staff leaves his hand on the way up
                    stowT = Mathf.Max(0, stowT - dt);
                    if (staffInHand && stowT <= StowTime * (1 - StowAt)) SetStaff(false);
                    if (state != State.Idle) { stowT = 0; SetStaff(false); }
                }
                else if (staffHold <= 0 && staffInHand)
                {
                    // standing still he puts it away with a swing over the shoulder; on the move it just goes back
                    if (state == State.Idle && model != null) stowT = StowTime;
                    else SetStaff(false);
                }
            }
            if (model != null)
            {
                PlaceModelStaff(pose, dt);
                return;
            }
            Vector3 tp = staffInHand ? StaffHandPos : StaffBackPos;
            Quaternion tq = staffInHand ? CbArt.Rot(Mathf.PI / 2 + pose.staffX, pose.spin, 0) : StaffBackRot;
            float k = 1 - Mathf.Exp(-(pose.spin != 0 ? 60f : 22f) * dt);
            staff.localPosition = Vector3.Lerp(staff.localPosition, tp, k);
            staff.localRotation = Quaternion.Slerp(staff.localRotation, tq, k);
        }

        /// <summary>
        /// The staff on the modelled Raja: across his back, or out of his right fist (twirling in the whirl).
        /// Placed in world space, as the model's bones have their own axes.
        /// </summary>
        void PlaceModelStaff(Pose pose, float dt)
        {
            Vector3 wp;
            Quaternion wq;
            if (staffInHand)
            {
                wp = model.Palm(model.rightHand);
                wq = model.Grip(Vector3.up);   // the staff's long axis is its local Y
                if (pose.spin != 0) wq = Quaternion.AngleAxis(pose.spin * Mathf.Rad2Deg, f.body.rotation * Vector3.right) * wq;
            }
            else
            {
                wp = model.chest.position + f.body.rotation * new Vector3(0, 0.05f, -0.16f);
                wq = f.body.rotation * StaffBackRot;
            }
            float k = 1 - Mathf.Exp(-(pose.spin != 0 ? 60f : 22f) * dt);
            staff.position = Vector3.Lerp(staff.position, wp, k);
            staff.rotation = Quaternion.Slerp(staff.rotation, wq, k);
        }

        void UpdateCloth(float dt)
        {
            float tt = g.clock;
            float speed = Mathf.Min(Mathf.Abs(vx) / 7f, 1f);
            for (int i = 0; i < f.tail.Count; i++)
                f.tail[i].localRotation = CbArt.Rot((0.35f - speed * 0.3f) * (i == 0 ? 1.4f : 0.6f) + Mathf.Sin(tt * 3 + i * 0.7f) * 0.12f,
                    Mathf.Sin(tt * 2.2f + i * 0.6f) * (0.18f + speed * 0.1f));
            for (int i = 0; i < f.bandTails.Count; i++)
                f.bandTails[i].localRotation = CbArt.Rot(0.3f + Mathf.Sin(tt * 9 + i) * 0.25f + speed * 0.4f, Mathf.Sin(tt * 7 + i * 2) * 0.3f);
            for (int i = 0; i < f.sash.Count; i++)
                f.sash[i].localRotation = CbArt.Rot(-speed * 0.6f + Mathf.Sin(tt * 8 + i) * 0.15f, 0, Mathf.Sin(tt * 5 + i) * 0.1f);
        }

        void UpdateFire(float dt)
        {
            // fire glows in his fur only once it is awake; before that, a perfect dodge's power is a pale shine
            bool fire = FireAwake;
            float target = state == State.Special && fire ? (t < SpecialWindup ? 1.6f + t / SpecialWindup * 1.6f : 3.2f) : empower ? 1.2f : 0f;
            glow += (target - glow) * Damp(10, dt);
            var col = fire ? new Color(1f, 0.48f, 0.1f) : KiGlow;
            CbArt.SetGlow(furMat, col * glow);
            if (model != null) CbArt.SetGlow(model.material, col * glow * 0.35f);
            // blink while invulnerable after a hit
            bool visible = !(inv > 0 && state != State.Dead && g.slow <= 0 && Mathf.FloorToInt(g.clock * 20) % 2 == 0);
            f.body.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Directed by a scene: Raja stands at x facing his way and shows a clip, looped by seconds or at a moment of
        /// it, with fire glowing in his fur. His moves and physics wait until the scene hands him back.
        /// </summary>
        public void Act(float dt, string clip, float time, bool loop, float fireGlow)
        {
            state = State.Idle; vx = vy = 0; y = 0; t = 0;
            f.body.gameObject.SetActive(true);
            var pose = Pose.Base;
            pose.bob = Mathf.Sin(g.clock * 3) * 0.025f - 0.05f;
            f.Apply(pose, dt, 10);
            Place();
            UpdateCloth(dt);
            CbArt.SetGlow(furMat, new Color(1f, 0.48f, 0.1f) * fireGlow);
            if (model == null) return;
            CbArt.SetGlow(model.material, new Color(1f, 0.48f, 0.1f) * fireGlow * 0.35f);
            if (loop) model.LoopSeconds(clip, time);
            else model.Show(clip, time);
            PlaceModelStaff(pose, dt);
        }

        /// <summary>Title screen: idle stance, tail swaying.</summary>
        public void Idle(float dt)
        {
            var pose = Pose.Base;
            pose.bob = Mathf.Sin(g.clock * 3) * 0.025f - 0.05f;
            f.Apply(pose, dt, 10);
            f.root.localPosition = new Vector3(x, 0, 0);
            f.body.localRotation = Quaternion.Euler(0, 140, 0);
            UpdateCloth(dt);
            if (model != null)
            {
                model.LoopSeconds("Idle", g.clock);
                PlaceModelStaff(pose, dt);
            }
        }

        static float Damp(float k, float dt) => 1 - Mathf.Exp(-k * dt);
    }
}
