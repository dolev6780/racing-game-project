using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>One frame of player input: stick, edges (pressed this frame) and holds.</summary>
    public class CbInput
    {
        public float x, y;                 // stick, y > 0 is up
        public bool jump, atk, dash, skill, kiTap, pause, confirm;
        public bool atkDown, charging;     // held
        public float atkHeldT;

        public void ClearEdges() { jump = atk = dash = skill = kiTap = pause = confirm = false; }
    }

    /// <summary>
    /// Raja, the fire tiger. Paw combo, four staff moves (the staff lives on his back and is drawn only for them),
    /// fireball, ki charge, fire roar, dash with perfect dodge. A straight port of the web demo's numbers.
    /// </summary>
    public class CbHero
    {
        public enum State { Idle, Run, Air, Atk, Staff, Blast, Roar, Charge, Dash, Hurt, Dead }
        public enum Move { None, Whirl, Launch, Thrust, Slam, Land }

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
        public float T => t;

        readonly CbGame g;
        readonly Material furMat, auraMat;
        readonly Transform staff, aura;
        bool staffInHand;
        float t, buffer, flipT, runPh, staffHold, sinceDash = 9, dashCd, glow, tick, auraA, ghostT, ringT;
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

            auraMat = CbArt.Glow(new Color(1f, 0.54f, 0.12f, 0f) * 2.4f, CbArt.Flame);
            aura = CbArt.Part(CbMesh.Frustum(0.75f, 24, false), auraMat, f.root, new Vector3(0, -0.15f, 0), new Vector3(1f, 2.8f, 1f), shadows: false);
            aura.gameObject.SetActive(false);
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
            if (!input.atkDown) holdUsed = false;
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
                case State.Staff: UpdateStaff(ref pose, dt, ref rate); break;
                case State.Blast:
                {
                    rate = 28;
                    float u = t / 0.32f;
                    if (u < 0.4f) { pose.twist = -0.6f; pose.sRx = 0.5f; pose.eR = -1.9f; pose.sLx = -0.8f; pose.eL = -1.2f; pose.lean = 0.05f; pose.hLx = -0.5f; pose.kL = 0.45f; pose.hRx = 0.5f; pose.kR = 0.4f; pose.bob = -0.1f; }
                    else { pose.twist = 0.55f; pose.sRx = -1.6f; pose.eR = -0.05f; pose.sRz = 0.05f; pose.sLx = -0.3f; pose.eL = -1.6f; pose.lean = 0.2f; pose.hLx = -0.7f; pose.kL = 0.4f; pose.hRx = 0.6f; pose.kR = 0.3f; pose.bob = -0.12f; }
                    if (u >= 0.4f && !fired) { fired = true; g.FireShot(new Vector3(x + facing * 1f, y + 1.45f, 0), facing); }
                    if (u >= 1) state = air ? State.Air : State.Idle;
                    break;
                }
                case State.Roar:
                    rate = 18;
                    if (t < 0.32f) { pose.lean = -0.35f; pose.headX = -0.35f; pose.sLz = 0.6f; pose.sRz = -0.6f; pose.sLx = 0.5f; pose.sRx = 0.5f; pose.eL = -1.8f; pose.eR = -1.8f; pose.hLx = -0.4f; pose.kL = 0.7f; pose.hRx = -0.4f; pose.kR = 0.7f; pose.bob = -0.22f; }
                    else { pose.lean = 0.3f; pose.headX = -0.55f; pose.sLz = 1.25f; pose.sRz = -1.25f; pose.sLx = -0.5f; pose.sRx = -0.5f; pose.eL = -0.6f; pose.eR = -0.6f; pose.hLx = -0.7f; pose.kL = 0.5f; pose.hRx = 0.6f; pose.kR = 0.5f; pose.bob = -0.15f; }
                    if (t >= 0.32f && !fired) { fired = true; g.RoarWave(new Vector3(x, y, 0), facing); }
                    if (t > 1f) state = State.Idle;
                    break;
                case State.Charge:
                    pose.sLz = 0.65f; pose.sRz = -0.65f; pose.sLx = 0.3f; pose.sRx = 0.3f; pose.eL = -0.5f; pose.eR = -0.5f; pose.hLx = -0.5f; pose.hRx = -0.5f;
                    pose.kL = 0.85f; pose.kR = 0.85f; pose.bob = -0.2f + Mathf.Sin(g.clock * 50) * 0.01f; pose.headX = -0.3f; pose.lean = -0.05f;
                    ki = Mathf.Min(100, ki + 34 * dt);
                    if (Random.value < 0.7f) CbFx.Spark(new Vector3(x + Random.Range(-0.7f, 0.7f), y + Random.Range(0f, 0.5f), Random.Range(-0.4f, 0.4f)), Random.value < 0.5f ? CbFx.Fire : CbFx.Flame, 1, 0.5f, 0.35f, 0.8f, -6);
                    if (Random.value < 0.04f) CbFx.Sfx("charge", 0.5f);
                    // powering up: rings of fire running out over the ground, focus lines, a rumble
                    ringT -= dt;
                    if (ringT <= 0)
                    {
                        ringT = 0.3f;
                        CbVfx.Ring(new Vector3(x, y + 0.05f, 0), CbFx.Fire * 0.5f, 1.6f, 0.45f, ground: true);
                    }
                    g.hud.SpeedLines(0.22f, 0.15f);
                    g.shake = Mathf.Max(g.shake, 0.04f);
                    if (!input.charging || air) state = State.Idle;
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
                    if (t > 0.2f) { state = air ? State.Air : State.Idle; vx = facing * 4; }
                    break;
            }

            bool free = state == State.Idle || state == State.Run || state == State.Air;
            if (free)
            {
                crouching = !air && input.y < -0.5f;
                vx += (input.x * (crouching ? 2.5f : 7f) - vx) * Damp(air ? 6 : 14, dt);
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
                else if (input.atk || (buffer > 0 && sinceDash < 0.35f))
                {
                    if (air && input.y < -0.5f) StartStaff(Move.Slam);
                    else if (!air && input.y > 0.5f) StartStaff(Move.Launch);
                    else if (!air && input.y < -0.5f) StartPaw(Sweep);
                    else if (!air && sinceDash < 0.35f) StartStaff(Move.Thrust);
                    else if (!air || airHits < 3) StartPaw(0);
                }
                else if (input.kiTap && ki >= 12) { ki -= 12; state = State.Blast; t = 0; fired = false; if (!air) vx *= 0.3f; else vy = Mathf.Max(vy, 1); }
                else if (input.skill && ki >= 60 && !air) { ki -= 60; state = State.Roar; t = 0; vx = 0; fired = false; }
                else if (input.charging && !air) { state = State.Charge; t = 0; }
                if (state == State.Idle || state == State.Run) state = Mathf.Abs(vx) > 0.6f ? State.Run : State.Idle;
            }
            else if (state == State.Charge || state == State.Roar) vx *= 1 - Damp(12, dt);
            else if (state == State.Atk || state == State.Blast) vx *= 1 - Damp(8, dt);

            if (state == State.Run)
            {
                runPh += dt * Mathf.Abs(vx) * 1.9f;
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
                case State.Idle when crouching: model.Show("Cautious_Crouch_Walk_Forward", 0.2f, 0.1f); break;
                case State.Run when crouching: model.Loop("Cautious_Crouch_Walk_Forward", runPh / (Mathf.PI * 2) * 0.6f); break;
                case State.Idle: model.LoopSeconds("Combat_Stance", g.clock); break;
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
                    break;
                case State.Blast: model.Window("Charged_Spell_Cast", 0f, 0.3f, t / 0.32f); break;
                case State.Charge: model.Show("Charged_Spell_Cast", 0.45f + Mathf.PingPong(t * 0.2f, 0.08f)); break;
                case State.Roar: model.Window("Chest_Pound_Taunt", 0.2f, 0.75f, t); break;
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
            // HIT, HIT, then hold HIT: staff whirl. The hit lasts about as long as the hold takes, so a hold still on
            // when the hit ends counts too (a quick tap in a mashed combo is shorter than 0.12 s).
            bool held = input.atkHeldT > 0.22f || (u >= 1 && input.atkHeldT > 0.12f);
            if (!air && combo >= 1 && input.atkDown && held && !holdUsed) StartStaff(Move.Whirl);
            else if (u >= 1)
            {
                if (buffer > 0 && combo < 3 && (!air || airHits < 3)) StartPaw(combo + 1);
                else { state = air ? State.Air : State.Idle; combo = 0; }
            }
            else if (u > 0.6f && buffer > 0 && combo < 3 && (!air || airHits < 3)) StartPaw(combo + 1);
        }

        void UpdateStaff(ref Pose pose, float dt, ref float rate)
        {
            rate = 30;
            switch (move)
            {
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
            bool burning = state == State.Charge || state == State.Roar;
            auraA += ((burning ? 1f : 0f) - auraA) * Damp(8, dt);
            aura.gameObject.SetActive(auraA > 0.02f);
            if (aura.gameObject.activeSelf)
            {
                // the modelled Raja is a small cub: a slimmer, see-through flame around him, not a pillar of light
                bool cub = model != null;
                var c = new Color(1f, 0.54f, 0.12f) * (cub ? 1.3f : 2.4f);
                c.a = auraA * (cub ? 0.55f : 0.9f);
                auraMat.SetColor("_Color", c);
                auraMat.mainTextureOffset = new Vector2(g.clock * 0.6f, 0);
                float w = (cub ? 0.58f : 1f) * (1 + Mathf.Sin(g.clock * 20) * 0.04f);
                aura.localScale = new Vector3(w, cub ? 2.1f : 2.8f, w);
            }
            float target = state == State.Roar ? 3.2f : state == State.Charge ? 1.6f + Mathf.Sin(g.clock * 12) * 0.6f : empower ? 1.2f : 0f;
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
