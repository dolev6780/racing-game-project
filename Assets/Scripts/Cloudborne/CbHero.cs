using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>One frame of player input: stick, edges (pressed this frame) and holds.</summary>
    public class CbInput
    {
        public float x, y;                 // stick, y > 0 is up
        // atk: HIT (light), heavy: HEAVY (the staff), kiTap: FIRE, skill: SPECIAL
        public bool jump, atk, heavy, dash, skill, kiTap, pause, confirm;
        public bool atkDown, heavyDown, guard;   // held
        public float atkHeldT, heavyHeldT;

        public void ClearEdges() { jump = atk = heavy = dash = skill = kiTap = pause = confirm = false; }
    }

    /// <summary>
    /// Raja, the fire tiger. HIT: paw and kick combo (down: a low sweep). HEAVY: the staff (a smash; up: launcher;
    /// in the air: slam; after a dash: thrust; held: whirl), which lives on his back and is drawn only for it.
    /// GUARD blocks blows from the front, down ducks, FIRE shoots a fireball, SPECIAL the tiger flame cannon.
    /// Ki fills by itself and with every hit; DASH just before a blow is a perfect dodge.
    /// </summary>
    public class CbHero
    {
        public enum State { Idle, Run, Air, Atk, Staff, Blast, Special, Guard, Dash, Hurt, Dead }
        public enum Move { None, Whirl, Launch, Thrust, Slam, Land, Smash }

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
        const float KiRegen = 5f, FireCost = 12f, SpecialCost = 50f;
        // SPECIAL: the tiger flame cannon. Gathered for SpecialWindup, then the beam (CbGame.SpecialBeam) runs
        const float SpecialWindup = 0.45f, SpecialTime = 1.35f;
        /// <summary>The special shot unlocks later in the story; on for now so it can be tried.</summary>
        public static bool SpecialUnlocked = true;
        const float BlockReact = 0.25f;

        static readonly Vector3 StaffBackPos = new Vector3(0.02f, 0.32f, -0.3f);
        static readonly Quaternion StaffBackRot = CbArt.Rot(-0.12f, 0, 0.82f);
        static readonly Vector3 StaffHandPos = new Vector3(0, -0.02f, 0.02f);

        public readonly Rig f;
        /// <summary>The modelled Raja; null if it could not be loaded, and then the procedural one shows.</summary>
        public readonly CbModel model;
        const string ModelPath = "Cloudborne/Raja/Raja";
        public float x, y, vx, vy, hp = 100, ki = 40, inv;
        public int facing = 1;
        public State state = State.Idle;
        public Move move = Move.None;
        public bool empower;
        /// <summary>Ducking (down on the stick, on the ground): moves slowly, a slash passes over him, HIT sweeps.</summary>
        public bool crouching;
        public bool Ducking => crouching && y <= 0.01f && (state == State.Idle || state == State.Run || (state == State.Atk && combo == Sweep));
        public bool Guarding => state == State.Guard;

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
        float t, buffer, heavyBuffer, flipT, runPh, staffHold, sinceDash = 9, dashCd, glow, tick, ghostT, blockT, crouchT, walkT;
        // below WalkSpeed he walks (a light push on the stick), above it he runs; the walk clip's own pace
        const float WalkSpeed = 4.2f, WalkClipPace = 1.4f;
        int combo, jumps, airHits, hits;
        bool air, airDashed, holdUsed, fired;
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
            x = y = vx = vy = 0; facing = 1; hp = 100; ki = 40; state = State.Idle; t = 0; combo = 0; buffer = 0; air = false; jumps = 0;
            flipT = 0; inv = 0; move = Move.None; staffHold = 0; sinceDash = 9; airDashed = false; dashCd = 0; empower = false; holdUsed = false;
            glow = 0; airHits = 0;
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
            state = State.Staff; move = m; t = 0; hitSet.Clear(); buffer = 0; tick = 0; hits = 0;
            SetStaff(true);
            if (m == Move.Thrust) vx = facing * 9;
            if (m == Move.Launch) vx = facing * 1.5f;
            if (m == Move.Whirl) holdUsed = true;
            if (m == Move.Slam) { vy = 6; vx = facing * 2; }
            if (m == Move.Smash) { vx = facing * 1.2f; fired = false; }
        }

        void EndStaff() { state = air ? State.Air : State.Idle; move = Move.None; staffHold = 0.5f; combo = 0; }

        bool MeleeHits(float range, float dmg, float kb, float up, bool big, bool breaker)
        {
            bool any = false;
            foreach (var e in g.enemies)
            {
                if (e.dead || hitSet.Contains(e)) continue;
                float dx = (e.x - x) * facing;
                if (dx > -0.4f - e.w * 0.5f && dx < range + e.w && Mathf.Abs(e.HitHeight - (y + 1.2f)) < 1.5f)
                {
                    hitSet.Add(e);
                    if (g.HurtEnemy(e, dmg, facing, kb, up, big, breaker, fire: false, fromPlayer: true)) any = true;
                }
            }
            if (any) CbFx.Sfx(big ? "heavy" : "hit");
            return any;
        }

        /// <summary>The player was struck. Returns false if the blow was dodged.</summary>
        public void TakeHit(float dmg, int dir)
        {
            if (state == State.Dead || g.mode != CbGame.Mode.Play) return;
            if (state == State.Dash && t < 0.2f) { g.PerfectDodge(); return; }
            if (inv > 0) return;
            // guarding: a blow from in front is stopped (dir is the way it pushes him, so the attacker faces -dir)
            if (state == State.Guard && facing == -dir)
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
            sinceDash += dt;
            if (input.atk) buffer = 0.25f; else buffer = Mathf.Max(0, buffer - dt);
            if (input.heavy) heavyBuffer = 0.25f; else heavyBuffer = Mathf.Max(0, heavyBuffer - dt);
            if (!input.heavyDown) holdUsed = false;
            if (state != State.Dead) ki = Mathf.Min(100, ki + KiRegen * dt);
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
                case State.Atk: UpdatePaw(ref pose, input, ref rate); break;
                case State.Staff: UpdateStaff(ref pose, input, dt, ref rate); break;
                case State.Blast:
                {
                    rate = 28;
                    float u = t / 0.32f;
                    if (u < 0.4f) { pose.twist = -0.6f; pose.sRx = 0.5f; pose.eR = -1.9f; pose.sLx = -0.8f; pose.eL = -1.2f; pose.lean = 0.05f; pose.hLx = -0.5f; pose.kL = 0.45f; pose.hRx = 0.5f; pose.kR = 0.4f; pose.bob = -0.1f; }
                    else { pose.twist = 0.55f; pose.sRx = -1.6f; pose.eR = -0.05f; pose.sRz = 0.05f; pose.sLx = -0.3f; pose.eL = -1.6f; pose.lean = 0.2f; pose.hLx = -0.7f; pose.kL = 0.4f; pose.hRx = 0.6f; pose.kR = 0.3f; pose.bob = -0.12f; }
                    if (u >= 0.4f && !fired) { fired = true; g.FireShot(HandPoint(), facing); }
                    if (u >= 1) state = air ? State.Air : State.Idle;
                    break;
                }
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
                vx += (input.x * (crouching ? 0f : 7f) - vx) * Damp(air ? 6 : 14, dt);
                if (Mathf.Abs(input.x) > 0.1f) facing = input.x > 0 ? 1 : -1;
                if (input.jump && jumps < 2)
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
                else if (input.kiTap && ki >= FireCost) { ki -= FireCost; state = State.Blast; t = 0; fired = false; if (!air) vx *= 0.3f; else vy = Mathf.Max(vy, 1); }
                else if (input.skill && SpecialUnlocked && ki >= SpecialCost && !air) { ki -= SpecialCost; state = State.Special; t = 0; vx = 0; fired = false; }
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

            // physics
            if (state != State.Dash || !air) vy -= 30 * dt;
            y += vy * dt;
            x += vx * dt;
            if (y <= 0)
            {
                if (air && vy < -6) CbFx.Spark(new Vector3(x, 0.1f, -0.3f), CbFx.Dust, 10, 3, 0.5f, 0.4f, 2);
                y = 0; vy = 0;
                if (air)
                {
                    air = false; jumps = 0; airDashed = false; airHits = 0;
                    if (state == State.Air) state = State.Idle;
                    if (state == State.Atk) { state = State.Idle; combo = 0; }
                    if (state == State.Hurt && t > 0.3f) state = State.Idle;
                }
            }
            else air = true;
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

        /// <summary>
        /// Picks the modelled Raja's clip, and the moment in it, for the current state and move, so each animation
        /// lasts exactly as long as the game's move and lands its blow when the move deals damage.
        /// </summary>
        void Animate()
        {
            if (model == null) return;
            switch (state)
            {
                // ducking: drops quickly into the bottom of a squat, arms out in front (hips from 0.75 to 0.43)
                case State.Idle when crouching:
                case State.Run when crouching: model.Window("air_squat", 0.15f, 0.45f, crouchT / 0.12f, 0.05f); break;
                case State.Idle: model.LoopSeconds("Combat_Stance", g.clock); break;
                // a light push on the stick walks, a full one runs (a slow run looked like slow motion)
                case State.Run when Mathf.Abs(vx) < WalkSpeed: model.LoopSeconds("Quick_Walk", walkT); break;
                case State.Run: model.Loop("RunFast", runPh / (Mathf.PI * 2)); break;
                // the somersault shows the tuck of the backflip clip (its hips' turn removed): the game turns him
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
                    break;
                // a right palm thrust: the fireball leaves at 0.4 of the cast, as the palm reaches out
                case State.Blast: model.Window("Charged_Spell_Cast_1", 0.38f, 0.62f, t / 0.32f); break;
                case State.Special:
                    if (t < SpecialWindup) model.Window("Charged_Spell_Cast_1", 0f, 0.45f, t / SpecialWindup);
                    else if (t < SpecialTime - 0.25f) model.Show("Charged_Spell_Cast_1", 0.5f + Mathf.PingPong(t * 0.3f, 0.12f));
                    else model.Window("Charged_Spell_Cast_1", 0.66f, 0.85f, (t - (SpecialTime - 0.25f)) / 0.25f);
                    break;
                case State.Guard:
                    if (blockT > 0) model.Window("Block1", 0.3f, 0.45f, 1 - blockT / BlockReact);
                    else model.Show("Block1", 0.1f, 0.06f);
                    break;
                case State.Dash: model.Window("Quick_Step_and_Spin_Dodge", 0.1f, 0.75f, t / 0.2f); break;
                case State.Hurt: model.Window("Hit_Reaction", 0.1f, 0.6f, t / 0.35f); break;
                case State.Dead: model.Window("Knock_Down", 0f, 1f, t / 1.4f); break;
            }
        }

        void UpdatePaw(ref Pose pose, CbInput input, ref float rate)
        {
            rate = 30;
            var A = Paw[combo];
            float u = t / A.dur;
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
                    if (u > 0.78f && u < 0.95f) MeleeHits(2.6f, 16, 6, 3, true, true);
                    if (u > 0.82f && !fired)
                    {
                        // the staff hits the ground: dust, a shock ring, a jolt
                        fired = true;
                        var at = new Vector3(x + facing * 1.5f, 0.05f, 0);
                        CbFx.Spark(at + new Vector3(0, 0.1f, -0.4f), CbFx.Dust, 14, 5, 0.6f, 0.5f, 3);
                        CbVfx.Ring(at, CbFx.Hit * 0.5f, 2.4f, 0.35f, ground: true);
                        g.shake = Mathf.Max(g.shake, 0.3f);
                        CbFx.Sfx("heavy", 0.7f);
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
                case Move.Slam:
                    pose.sRx = -3f; pose.sLx = -3f; pose.sLz = 0.2f; pose.sRz = -0.2f; pose.eR = -0.2f; pose.eL = -0.2f; pose.lean = -0.1f;
                    pose.hLx = -1.2f; pose.kL = 1.4f; pose.hRx = -0.2f; pose.kR = 1.2f; pose.staffX = 0.4f;
                    if (t > 0.15f) vy = Mathf.Min(vy, -24);
                    if (!air && t > 0.05f)
                    {
                        foreach (var e in g.enemies)
                        {
                            if (e.dead || Mathf.Abs(e.x - x) > 3f + e.w || e.HitHeight - y > 2.6f) continue;
                            g.HurtEnemy(e, 14, e.x >= x ? 1 : -1, 5, 7, true, true, fire: false, fromPlayer: true);
                        }
                        CbFx.Spark(new Vector3(x + facing * 1.1f, 0.1f, -0.4f), CbFx.Dust, 18, 6, 0.6f, 0.5f, 4);
                        CbFx.Spark(new Vector3(x + facing * 1.1f, 0.2f, -0.4f), CbFx.Fire, 10, 6, 0.4f, 0.4f, 3);
                        g.shake = Mathf.Max(g.shake, 0.4f);
                        CbFx.Sfx("heavy");
                        CbVfx.Ring(new Vector3(x + facing * 1.1f, 0.05f, 0), CbFx.Hit * 0.6f, 3.5f, 0.4f, ground: true);
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
            if (state != State.Staff)
            {
                staffHold = Mathf.Max(0, staffHold - dt);
                if (staffHold <= 0 && staffInHand) SetStaff(false);
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
            float target = state == State.Special ? (t < SpecialWindup ? 1.6f + t / SpecialWindup * 1.6f : 3.2f) : empower ? 1.2f : 0f;
            glow += (target - glow) * Damp(10, dt);
            CbArt.SetGlow(furMat, new Color(1f, 0.48f, 0.1f) * glow);
            if (model != null) CbArt.SetGlow(model.material, new Color(1f, 0.48f, 0.1f) * glow * 0.35f);
            // blink while invulnerable after a hit
            bool visible = !(inv > 0 && state != State.Dead && g.slow <= 0 && Mathf.FloorToInt(g.clock * 20) % 2 == 0);
            f.body.gameObject.SetActive(visible);
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
