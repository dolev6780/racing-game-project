using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Iron Crown hyena soldiers. The plain one comes at Raja (a charge from afar, a fighting walk up close), swings
    /// his machete (a glint on the blade as he winds up) and steps back after; he shouts with his blade raised as he
    /// first sees Raja;
    /// the shield variant blocks blows from the front until a staff move or the roar breaks its guard.
    /// Sergeant Snap, the mini-boss at the end of stage 1-1, fights with a set of his own (see BossAttack).
    /// </summary>
    public class CbEnemy
    {
        public enum Kind { Hyena, Shield, Sergeant }
        public enum State { Move, Atk, Hurt, Dying }
        /// <summary>Sergeant Snap's attacks.</summary>
        public enum BossMove { Combo, Chop, Leap, Whistle }

        public readonly Kind kind;
        public readonly Rig f;
        /// <summary>The modelled hyena; null if it could not be loaded, and then the procedural one shows.</summary>
        public readonly CbModel model;
        readonly Transform blade;
        public float x, y, vx, vy, hp, max, burnT, burnTick, flash, tele;
        public int facing = -1;
        public State state = State.Move;
        public bool dead, guard;
        public readonly float w = 0.5f, hy = 1.3f;
        public float HitHeight => y + hy;
        public readonly int scoreValue;

        readonly CbGame g;
        readonly float speed, dmg, reach;
        float t, cd, runPh;
        bool hitDone;
        // a soldier's moves between blows (with their clips, when his model has them): a charge from afar, stepping
        // back after a blow, a shout with the blade raised as he first sees Raja
        const float ChargeFrom = 3.6f, ChargeBoost = 1.45f, BackTime = 0.5f, ShoutTime = 1.6f;
        const string ChargeClip = "Standard_Forward_Charge_inplace", ShoutClip = "Sword_Shout";
        float backT, shoutT, chargeT, backPh;
        bool charging, noticed;

        // ---- Sergeant Snap ----
        public bool Boss => kind == Kind.Sergeant;
        /// <summary>
        /// A soldier at his post along the way (CbGame): he keeps between minX and maxX (his own stretch of dry path),
        /// goes for Raja only once he comes within Notice, and an asleep one waits until Raja is right by him or
        /// strikes him.
        /// </summary>
        public float minX = float.NegativeInfinity, maxX = float.PositiveInfinity;
        public bool asleep;
        const float Notice = 10f, WakeAt = 2.2f;
        // Asleep (the user: he didn't look asleep), a soldier with the clip for it dozes on a log at the side of the
        // path (CbGame puts it behind him, a little back from the path), his head nodding, his eyes painted shut (the
        // model's _Asleep texture), a snot bubble swelling and shrinking with his breath and every third breath
        // popping, a stream of Zs rising from his head and a soft snore. Woken, he jumps up with a start, his blade
        // raised, before he fights.
        public const float SeatZ = 0.55f, SeatBack = 0.63f;
        const string SleepClip = "Sit_and_Doze_Off";
        const float Breath = 2.6f, WakeTime = 0.55f, SnoreEvery = 0.85f;
        bool dozing;
        float sleepT, breathT, snoreT, bubbleWait, bubbleD, wakeT, seatZ;
        int breaths, zs;
        Vector3 bubbleAt;
        readonly string modelPath;
        Texture openEyes;
        public BossMove bossMove;
        /// <summary>After his combo or his chop he stands open (panting, the cleaver in the ground): the time to hit back.</summary>
        public float open;
        public bool Whistling => Boss && state == State.Atk && bossMove == BossMove.Whistle && !hitDone;
        int blow;
        bool calledAt66, calledAt33, airborne, whistled, telePurple;
        // how long each attack lasts, and when the combo's three blows land (seconds in)
        const float ComboTime = 1.7f, ChopTime = 1.5f, ChopHit = 0.75f, LeapTime = 1.2f, WhistleTime = 1.6f;
        static readonly float[] ComboBlows = { 0.42f, 0.78f, 1.14f };

        public CbEnemy(CbGame game, Kind k, float x0, Transform parent)
        {
            g = game;
            kind = k;
            x = x0;
            bool shield = k == Kind.Shield, sergeant = k == Kind.Sergeant;
            hp = max = sergeant ? 320 : shield ? 46 : 34;
            speed = sergeant ? 2.3f : shield ? 2.1f : 2.7f;
            dmg = sergeant ? 9 : shield ? 10 : 8;
            reach = sergeant ? 1.9f : shield ? 1.6f : 1.5f;
            scoreValue = sergeant ? 3000 : shield ? 500 : 300;
            if (shield) w = 0.6f;
            if (sergeant) { w = 0.7f; hy = 1.5f; }
            guard = shield;
            cd = Random.Range(0.5f, 1.4f);
            runPh = Random.Range(0f, 6f);

            f = RigBuilder.Build(new RigStyle
            {
                fur = CbArt.Hex("#b8925a"), paw = CbArt.Hex("#8a6a40"), top = CbArt.Hex("#6b727d"), armor = CbArt.Hex("#7d8590"),
                pants = CbArt.Hex("#4a3a2e"), belt = CbArt.Hex("#2a1f1a"), wrap = CbArt.Hex("#4a3a2e"), bulk = sergeant ? 1.25f : shield ? 1.12f : 1f,
                scale = sergeant ? 1.15f : 1f,
            }, sergeant ? "SgtSnap" : shield ? "ShieldHyena" : "Hyena");
            RigBuilder.HyenaHead(f);
            // the modelled machete and shield when they are in the project, else boxes as before
            blade = CbArt.Joint(f.R.hand, 0, -0.02f, 0.02f, "machete");
            if (CbArt.Prop("Cloudborne/Props/Machete/Machete", blade) == null)
            {
                CbArt.Box(f.M("#c9ced6"), blade, new Vector3(0, 0, 0.38f), new Vector3(0.03f, 0.1f, 0.62f));
                CbArt.Box(f.M("#2a1f1a"), blade, new Vector3(0, 0, 0.02f), new Vector3(0.05f, 0.06f, 0.16f));
            }
            if (shield)
            {
                f.shield = CbArt.Joint(f.chest, 0.05f, 0.28f, 0.42f, "shield");
                if (CbArt.Prop("Cloudborne/Props/Shield/Shield", f.shield) == null)
                {
                    CbArt.Box(f.M("#5d646e"), f.shield, Vector3.zero, new Vector3(0.85f, 1.15f, 0.08f));
                    CbArt.Box(f.M("#3d434b"), f.shield, new Vector3(0, 0.55f, 0.01f), new Vector3(0.95f, 0.08f, 0.1f));
                    var gold = f.M("#d9a53a", 0f);
                    for (int i = -2; i <= 2; i++)
                        CbArt.Cone(gold, f.shield, new Vector3(i * 0.1f, 0.12f + (2 - Mathf.Abs(i)) * 0.04f, 0.06f), 0.05f, 0.16f, null, 4);
                }
            }
            f.root.SetParent(parent, false);
            f.root.localPosition = new Vector3(x, 0, 0);
            f.body.localRotation = Quaternion.Euler(0, -115, 0);

            // The rig still places, turns and flips the hyena; with the model loaded only its machete and shield are drawn.
            modelPath = sergeant ? "Cloudborne/HyenaSergeant/HyenaSergeant" : shield ? "Cloudborne/HyenaGuard/HyenaGuard" : "Cloudborne/HyenaRaider/HyenaRaider";
            model = CbModel.Load(modelPath, sergeant ? 2.55f : shield ? 2.1f : 2.25f, f.body);
            if (model != null)
            {
                foreach (var r in f.body.GetComponentsInChildren<Renderer>(true))
                    if (!r.transform.IsChildOf(model.root) && !r.transform.IsChildOf(blade) && (f.shield == null || !r.transform.IsChildOf(f.shield)))
                        r.enabled = false;
                blade.SetParent(f.body, true);
                if (f.shield != null) f.shield.SetParent(f.body, true);
                f.mats.Add(model.material);
                PlaceGear(1f);
            }
        }

        /// <summary>
        /// The modelled hyena's machete in its right fist, and the guard's shield on its left forearm, facing forward.
        /// Placed in world space, as the model's bones have their own axes.
        /// </summary>
        void PlaceGear(float k)
        {
            if (model == null) return;
            blade.position = model.Palm(model.rightHand);
            blade.rotation = model.Grip(Vector3.forward);   // the blade runs along its local Z
            if (f.shield == null) return;
            var arm = (model.leftForeArm.position + model.leftHand.position) * 0.5f;
            f.shield.position = Vector3.Lerp(f.shield.position, arm + f.body.rotation * new Vector3(0, 0, 0.14f), k);
            f.shield.rotation = Quaternion.Slerp(f.shield.rotation, f.body.rotation, k);
        }

        /// <summary>Picks the modelled hyena's clip, and the moment in it, for the current state.</summary>
        void Animate()
        {
            if (model == null) return;
            if (Boss) { AnimateBoss(); return; }
            bool shield = kind == Kind.Shield;
            switch (state)
            {
                case State.Dying: model.Window("dying_backwards", 0f, 0.85f, t); break;
                case State.Hurt:
                    if (y > 0.25f) model.Show("BeHit_FlyUp", vy > 0 ? 0.3f : 0.5f, 0.1f);
                    else model.Window("Hit_Reaction", 0.1f, 0.6f, t / 0.42f);
                    break;
                case State.Atk:
                    // the blow lands at the end of the 0.45 s wind-up, a little past the middle of the clip
                    if (shield && guard) model.Window("Shield_Push_Left", 0.1f, 0.8f, t / 0.8f);
                    else model.Window("Right_Hand_Sword_Slash", 0.1f, 0.85f, t / 0.8f);
                    break;
                default:
                {
                    string back = shield ? "Walk_Backward_with_Sword_Shield" : "Walk_Backward_with_Sword_inplace";
                    // asleep: dozing, his head nodding (the clip plays for 17 seconds and loops); woken, he springs up out
                    // of his seat with his blade raised (blending from sitting into the shout's start as he jumps)
                    if (dozing) model.LoopSeconds(SleepClip, sleepT);
                    else if (wakeT > 0 && model.HasClip(ShoutClip)) model.Window(ShoutClip, 0.04f, 0.32f, 1 - wakeT / WakeTime, 0.2f);
                    // the shout's first part: the blade raised high over his head (the clip goes on for six seconds)
                    else if (shoutT > 0 && model.HasClip(ShoutClip)) model.Window(ShoutClip, 0.04f, 0.4f, 1 - shoutT / ShoutTime);
                    else if (backT > 0 && Mathf.Abs(vx) > 0.3f && model.HasClip(back))
                    {
                        backPh += Time.deltaTime;
                        model.LoopSeconds(back, backPh);
                    }
                    else if (Mathf.Abs(vx) > 0.4f && charging && model.HasClip(ChargeClip))
                    {
                        chargeT += Time.deltaTime * Mathf.Abs(vx) / (speed * ChargeBoost);
                        model.LoopSeconds(ChargeClip, chargeT);
                    }
                    else if (Mathf.Abs(vx) > 0.4f) model.Loop("Walk_Fight_Forward", runPh / (Mathf.PI * 2));
                    else if (shield && guard) model.Show("Block1", 0.15f);
                    else model.LoopSeconds("Combat_Stance", g.clock + runPh);
                    break;
                }
            }
        }

        void AnimateBoss()
        {
            switch (state)
            {
                case State.Dying: model.Window("dying_backwards", 0f, 0.85f, t); break;
                case State.Hurt:
                    if (y > 0.25f) model.Show("BeHit_FlyUp", vy > 0 ? 0.3f : 0.5f, 0.1f);
                    else model.Window("Hit_Reaction", 0.1f, 0.6f, t / 0.3f);
                    break;
                case State.Atk:
                    if (bossMove == BossMove.Combo) model.Window("Triple_Combo_Attack", 0f, 1f, t / ComboTime);
                    else if (bossMove == BossMove.Chop) model.Window("Charged_Axe_Chop", 0f, 1f, t / ChopTime);
                    else if (bossMove == BossMove.Leap) model.Window("Basic_Jump", 0f, 1f, t / LeapTime);
                    else model.Window("Shouting_Angrily", 0f, 1f, t / WhistleTime);
                    break;
                default:
                    if (Mathf.Abs(vx) > 0.4f) model.Loop("Walk_Fight_Forward", runPh / (Mathf.PI * 2));
                    else model.LoopSeconds("Combat_Stance", g.clock * (open > 0 ? 1.8f : 1f));
                    break;
            }
        }

        void FaceTarget() => facing = g.hero.x >= x ? 1 : -1;

        /// <summary>He nods off on his log: facing the way Raja comes, back from the path, his eyes shut.</summary>
        void FallAsleep()
        {
            dozing = true;
            facing = -1;
            seatZ = SeatZ;
            sleepT = Random.Range(0f, 12f);
            breathT = Random.Range(0f, Breath);
            breaths = 0;
            bubbleWait = 0;
            snoreT = 0.4f;
            var shut = Resources.Load<Texture2D>(modelPath + "_Asleep");
            if (shut != null && model.material != null)
            {
                openEyes = model.material.mainTexture;
                model.material.mainTexture = shut;
            }
        }

        /// <summary>
        /// One frame of his sleep: his breath (his chest, the bubble that swells as he breathes out and shrinks as he
        /// breathes in, the third one swelling bigger and popping), the Zs and the snore.
        /// </summary>
        void Doze(float dt, float dist)
        {
            sleepT += dt;
            breathT += dt;
            bool newBreath = breathT >= Breath;
            if (newBreath)
            {
                breathT -= Breath;
                breaths++;
                if (dist < 9f) CbFx.Sfx("snore", 0.35f);
            }
            float u = breathT / Breath, s = 0.5f - 0.5f * Mathf.Cos(2 * Mathf.PI * u);
            f.body.localScale = new Vector3(1, 1 + 0.03f * (1 - s), 1);
            bool popper = breaths % 3 == 2;
            if (bubbleWait > 0) { bubbleWait -= dt; bubbleD = 0; }
            else
            {
                bubbleD = (0.05f + 0.3f * s) * (popper ? 1.3f : 1f);
                var face = model.FaceDirection;
                bubbleAt = model.Snout + face * (bubbleD * 0.42f) + Vector3.down * (bubbleD * 0.2f);
                if (popper && u > 0.5f) { PopBubble(); bubbleWait = Breath * 0.5f; }
                else g.hud.Bubble(this, bubbleAt, bubbleD);
            }
            if ((snoreT -= dt) <= 0)
            {
                snoreT = SnoreEvery;
                g.hud.Snore(model.Snout + new Vector3(-facing * 0.1f, 0.5f, 0), facing, ++zs % 2 == 1);
            }
        }

        /// <summary>The bubble bursts: a few droplets and a pop.</summary>
        void PopBubble()
        {
            if (bubbleD < 0.06f) return;
            CbFx.Spark(bubbleAt + new Vector3(0, 0, -0.3f), new Color(0.8f, 0.92f, 1f) * 1.6f, 7, 2.4f, 0.12f, 0.3f, 5);
            CbFx.Sfx("pop", 0.5f);
            bubbleD = 0;
        }

        /// <summary>Woken: his eyes open, the bubble bursts, and unless a blow woke him he jumps up with a start.</summary>
        void WakeUp()
        {
            dozing = false;
            if (openEyes != null) model.material.mainTexture = openEyes;
            f.body.localScale = Vector3.one;
            if (bubbleWait <= 0) PopBubble();
            noticed = true;
            if (state == State.Hurt || state == State.Dying) return;
            g.hud.Popup(new Vector3(x, HitHeight + 1.2f, 0), "!", CbHud.PopStyle.Big);
            CbFx.Sfx("jump", 0.6f);
            FaceTarget();
            wakeT = WakeTime;
            vy = 6.5f;
        }

        public void Stagger(int dir, float kb, float up)
        {
            if (Boss)
            {
                // armoured: he takes the blows and keeps going, unless he stands open or a big blow lands
                if (open <= 0 && state == State.Atk) return;
                if (open <= 0 && kb < 7) { vx = dir * kb * 0.3f; return; }
                kb *= 0.5f;
                up = Mathf.Min(up, 3);
            }
            vx = dir * kb;
            vy = up;
            state = State.Hurt; t = 0;
        }

        /// <summary>How long a beaten enemy lies there before he vanishes in a puff of smoke.</summary>
        public const float DyingTime = 1.05f;

        public void Kill(int dir)
        {
            dead = true; state = State.Dying; t = 0;
            vx = dir * 10; vy = 7;
        }

        /// <summary>A ki move (a staff technique, or fire) stops his whistle before it calls anyone.</summary>
        public void BreakWhistle()
        {
            if (!Whistling) return;
            hitDone = true;
            state = State.Hurt; t = 0; vx = 0;
            g.hud.Popup(new Vector3(x, y + 3f, 0), "WHISTLE BROKEN!", CbHud.PopStyle.Big);
            CbFx.Sfx("block");
        }

        void StartBossMove(BossMove m)
        {
            state = State.Atk; bossMove = m; t = 0; hitDone = false; blow = 0; airborne = false; whistled = false; vx = 0;
            FaceTarget();
        }

        /// <summary>Sergeant Snap between attacks: walks up, and picks his next one.</summary>
        void BossThink(ref Pose pose, float dt, CbHero hero, float dx, float dist)
        {
            if (open > 0)
            {
                open -= dt;
                vx = Mathf.MoveTowards(vx, 0, 20 * dt);
                pose.lean = -0.25f; pose.bob = Mathf.Sin(g.clock * 8) * 0.04f - 0.08f;
                return;
            }
            bool can = cd <= 0 && hero.state != CbHero.State.Dead && g.mode == CbGame.Mode.Play;
            float left = hp / max;
            if (can && ((left < 0.66f && !calledAt66) || (left < 0.33f && !calledAt33)))
            {
                if (left < 0.33f) calledAt33 = true;
                calledAt66 = true;
                StartBossMove(BossMove.Whistle);
                return;
            }
            if (can && dist > 5f) { StartBossMove(BossMove.Leap); return; }
            if (can && dist <= reach + 0.4f) { StartBossMove(Random.value < 0.6f ? BossMove.Combo : BossMove.Chop); return; }
            float want = dist > reach ? Mathf.Sign(dx) * speed : 0;
            vx += (want - vx) * (1 - Mathf.Exp(-8 * dt));
            if (Mathf.Abs(vx) > 0.4f) { runPh += dt * Mathf.Abs(vx) * 2.1f; Pose.Run(ref pose, runPh, 0.9f); }
            else pose.bob = Mathf.Sin(g.clock * 3) * 0.02f - 0.05f;
        }

        /// <summary>
        /// Sergeant Snap's attacks. The combo: three cleaver blows, each flashing white just before it lands (GUARD
        /// stops them), then he stands open. The chop: a purple flash (it goes through a guard: get out of reach),
        /// the cleaver bites the ground and sticks a moment. The leap: he jumps at Raja and lands with a shock that hits
        /// anyone standing near (jump over it). The whistle: he calls two raiders, unless a ki move hits him first.
        /// </summary>
        void BossAttack(ref Pose pose, float dt, CbHero hero)
        {
            float ddx = (hero.x - x) * facing;
            switch (bossMove)
            {
                case BossMove.Combo:
                    if (blow < ComboBlows.Length)
                    {
                        float next = ComboBlows[blow];
                        // a glow rising to the blow (it flickered: the user)
                        if (t > next - 0.28f && t < next) tele = 0.6f * Mathf.InverseLerp(next - 0.28f, next, t);
                        if (t >= next)
                        {
                            vx = facing * 2.5f;
                            if (ddx > -0.3f && ddx < reach + 0.5f && hero.y < 2f) hero.TakeHit(dmg, facing);
                            CbFx.Sfx("swing", 0.7f);
                            CbFx.Spark(new Vector3(x + facing * 1.2f, 1.2f, -0.4f), CbFx.Grey, 4, 4, 0.3f, 0.2f);
                            blow++;
                        }
                    }
                    vx = Mathf.MoveTowards(vx, 0, 8 * dt);
                    if (t >= ComboTime) { state = State.Move; open = 0.9f; cd = 1.2f; }
                    break;
                case BossMove.Chop:
                    vx = 0;
                    if (t < ChopHit) { tele = 0.25f + 0.55f * (t / ChopHit); telePurple = true; }
                    else if (!hitDone)
                    {
                        hitDone = true;
                        var at = new Vector3(x + facing * 1.6f, 0.1f, -0.3f);
                        CbVfx.Ring(at, CbFx.Purple * 0.5f, 2.2f, 0.35f, ground: true);
                        CbFx.Spark(at, CbFx.Dust, 18, 6, 0.5f, 0.5f, 6);
                        g.shake = Mathf.Max(g.shake, 0.45f);
                        CbFx.Sfx("heavy");
                        if (ddx > -0.3f && ddx < 2.6f && hero.y < 1.6f) hero.TakeHit(20, facing, unblockable: true, knock: 6.5f);
                    }
                    if (t >= ChopTime) { state = State.Move; open = 1.1f; cd = 1.3f; }
                    break;
                case BossMove.Leap:
                    if (!airborne && !hitDone && t > 0.25f)
                    {
                        airborne = true;
                        vy = 9.5f;
                        float air = 2 * vy / 30f;
                        float target = hero.x;
                        if (g.lockZone != null) target = Mathf.Clamp(target, g.lockZone.x - 9.5f, g.lockZone.x + 9.5f);
                        vx = Mathf.Clamp((target - x) / air, -11f, 11f);
                        CbFx.Sfx("jump", 0.8f);
                    }
                    else if (airborne && !hitDone && y <= 0.01f && vy <= 0)
                    {
                        hitDone = true;
                        airborne = false;
                        vx = 0;
                        var at = new Vector3(x, 0.05f, 0);
                        CbVfx.Ring(at, CbFx.Dust * 2f, 2.4f, 0.35f, ground: true);
                        CbFx.Spark(at + new Vector3(0, 0.2f, -0.3f), CbFx.Dust, 20, 6, 0.5f, 0.5f, 6);
                        g.shake = Mathf.Max(g.shake, 0.5f);
                        CbFx.Sfx("heavy");
                        if (Mathf.Abs(hero.x - x) < 1.9f && hero.y < 0.35f) hero.TakeHit(14, hero.x >= x ? 1 : -1, knock: 5f);
                    }
                    if (t >= LeapTime && !airborne) { state = State.Move; cd = 1.0f; }
                    break;
                case BossMove.Whistle:
                    vx = 0;
                    if (t > 0.5f && !whistled)
                    {
                        whistled = true;
                        CbFx.Sfx("whistle", 0.8f);
                        g.hud.Popup(new Vector3(x, y + 3f, 0), "!", CbHud.PopStyle.Big);
                    }
                    if (t > 1.2f && !hitDone) { hitDone = true; g.SummonRaiders(); }
                    if (t >= WhistleTime) { state = State.Move; cd = 0.8f; }
                    break;
            }
        }

        public void BreakGuard(int dir)
        {
            guard = false;
            if (f.shield == null) return;
            var sh = f.shield;
            sh.SetParent(g.transform, true);
            g.AddDebris(sh, new Vector3(-dir * 4, 5, 0), Random.Range(-340f, 340f));
            f.shield = null;
        }

        /// <summary>Returns true when the enemy is gone and can be removed.</summary>
        public bool Update(float dt)
        {
            t += dt;
            cd -= dt;
            flash = Mathf.Max(0, flash - dt);
            tele = 0;
            telePurple = false;
            if (burnT > 0 && !dead)
            {
                burnT -= dt; burnTick -= dt;
                if (Random.value < 0.5f) CbFx.Spark(new Vector3(x + Random.Range(-0.4f, 0.4f), HitHeight + Random.Range(-0.6f, 0.6f), -0.4f), Random.value < 0.5f ? CbFx.Fire : CbFx.Flame, 1, 0.6f, 0.35f, 0.5f, -4);
                if (burnTick <= 0)
                {
                    burnTick = 0.4f;
                    hp -= 2;
                    g.score += 20;
                    g.hud.Popup(new Vector3(x, HitHeight + 0.7f, 0), "2", CbHud.PopStyle.Burn);
                    if (hp <= 0) g.KillEnemy(this, -facing);
                }
            }

            var hero = g.hero;
            float dx = hero.x - x, dist = Mathf.Abs(dx);
            if (asleep && !dozing && model != null && model.HasClip(SleepClip)) FallAsleep();
            // struck awake (CbGame.HurtEnemy wakes him)
            else if (dozing && !asleep) WakeUp();
            if (!dozing) seatZ = Mathf.MoveTowards(seatZ, 0, dt * 2.2f);
            if (state != State.Dying && state != State.Hurt && state != State.Atk && !dozing) FaceTarget();
            var pose = Pose.Base;
            pose.sRx = 0.1f; pose.eR = -1.6f; pose.sRz = -0.3f;
            if (guard) { pose.sLx = -1.1f; pose.eL = -0.9f; pose.sLz = 0.1f; }

            switch (state)
            {
                case State.Dying:
                    // thrown back, down on his back, dazed stars over him, blinking - then gone in a puff of smoke
                    // (CbGame.EnemyPoof), what he carried bursting out of it
                    pose.lean = -1.3f; pose.sLz = 1.4f; pose.sRz = -1.4f; pose.hLx = -1; pose.hRx = 0.3f; pose.headX = -0.5f;
                    if (model == null) f.flip.localRotation *= Quaternion.Euler(0, 0, facing * dt * 6 * Mathf.Max(0, 0.5f - t) * Mathf.Rad2Deg);
                    if (t > 0.55f && y <= 0.01f && Random.value < 0.25f)
                        CbFx.Spark(new Vector3(x + Random.Range(-0.4f, 0.4f), y + 0.9f, -0.4f), CbFx.Flame, 1, 1.2f, 0.18f, 0.45f, 0);
                    if (t > 0.7f) flash = Mathf.Repeat(t, 0.12f) < 0.06f ? 0.06f : 0f;
                    if (t > DyingTime) { g.EnemyPoof(this); return true; }
                    break;
                case State.Hurt:
                    pose.lean = -0.6f; pose.headX = -0.5f; pose.sLz = 1.1f; pose.sRz = -1.1f; pose.sLx = 0.5f; pose.sRx = 0.5f;
                    if (t > (Boss ? 0.3f : 0.42f) && y <= 0) state = State.Move;
                    break;
                case State.Atk when Boss:
                    BossAttack(ref pose, dt, hero);
                    break;
                case State.Move when Boss:
                    BossThink(ref pose, dt, hero, dx, dist);
                    break;
                case State.Atk:
                {
                    const float wind = 0.45f;
                    if (t < wind)
                    {
                        pose.twist = -0.8f; pose.sRx = 0.9f; pose.eR = -1.8f; pose.lean = -0.1f; pose.hLx = -0.4f; pose.kL = 0.5f; pose.bob = -0.08f;
                        // the warning: a glint on the blade as he winds up (he blinked white every blow: the user)
                        if (t <= dt && blade != null)
                            CbFx.Spark(blade.position + new Vector3(0, 0, -0.3f), new Color(1f, 0.95f, 0.7f) * 2f, 5, 2.2f, 0.12f, 0.22f, 0);
                    }
                    else
                    {
                        pose.twist = 0.6f; pose.sRx = -1.6f; pose.eR = 0; pose.lean = 0.35f; pose.hLx = -0.9f; pose.kL = 0.4f; pose.hRx = 0.6f; pose.bob = -0.12f;
                        if (!hitDone)
                        {
                            hitDone = true;
                            float ddx = (hero.x - x) * facing;
                            // a raider's slash goes over a ducking Raja; the guard's shield push still hits him
                            bool ducked = kind == Kind.Hyena && hero.Ducking;
                            if (ddx > -0.3f && ddx < reach + 0.4f && hero.y < 1.8f && !ducked) hero.TakeHit(dmg, facing);
                            CbFx.Sfx("swing", 0.5f);
                        }
                    }
                    if (t > wind + 0.35f)
                    {
                        state = State.Move; cd = Random.Range(1f, 1.9f);
                        // most of the time a step back out of reach after the blow
                        if (Random.value < 0.65f) backT = BackTime;
                    }
                    break;
                }
                default:
                {
                    if (asleep)
                    {
                        // asleep at his post; Raja right by him wakes him
                        vx = 0;
                        if (dozing) Doze(dt, dist);
                        else
                        {
                            // no clip to doze with: slumped where he stands, the Zs over him
                            pose.lean = 0.35f; pose.headX = 0.6f; pose.bob = -0.15f; pose.sLx = 0.2f; pose.sRx = 0.2f;
                            if ((snoreT -= dt) <= 0) { snoreT = SnoreEvery; g.hud.Snore(new Vector3(x + facing * 0.3f, HitHeight + 0.9f, 0), facing, ++zs % 2 == 0); }
                        }
                        if (dist < WakeAt)
                        {
                            asleep = false;
                            if (dozing) WakeUp();
                            else g.hud.Popup(new Vector3(x, HitHeight + 1f, 0), "!", CbHud.PopStyle.Hurt);
                        }
                        break;
                    }
                    // jumping up with a start: no blow yet
                    if (wakeT > 0) { wakeT -= dt; vx = 0; break; }
                    float push = 0;
                    foreach (var o in g.enemies)
                    {
                        if (o == this || o.dead) continue;
                        float d = x - o.x;
                        if (Mathf.Abs(d) < 1.1f) push += (d != 0 ? Mathf.Sign(d) : Random.Range(-1f, 1f)) * (1.1f - Mathf.Abs(d)) * 3;
                    }
                    // the first time he sees Raja: a shout with his blade raised
                    if (!noticed && dist < Notice)
                    {
                        noticed = true;
                        if (model != null && model.HasClip(ShoutClip) && dist > reach + 1.5f) shoutT = ShoutTime;
                    }
                    float want;
                    if (shoutT > 0) { shoutT -= dt; want = 0; charging = false; }
                    else if (backT > 0) { backT -= dt; want = -Mathf.Sign(dx) * speed * 0.6f; charging = false; }
                    else
                    {
                        // from afar he charges, up close he comes on in his fighting walk
                        charging = dist > ChargeFrom && dist < Notice;
                        want = dist > reach && (Boss || dist < Notice) ? Mathf.Sign(dx) * speed * (charging ? ChargeBoost : 1f) : 0;
                    }
                    vx += (want + push - vx) * (1 - Mathf.Exp(-8 * dt));
                    if (dist <= reach + 0.2f && cd <= 0 && shoutT <= 0 && hero.state != CbHero.State.Dead && g.mode == CbGame.Mode.Play)
                    {
                        state = State.Atk; t = 0; hitDone = false; vx = 0; backT = 0; charging = false;
                    }
                    if (Mathf.Abs(vx) > 0.4f)
                    {
                        runPh += dt * Mathf.Abs(vx) * 2.1f;
                        Pose.Run(ref pose, runPh, 0.9f);
                        pose.sRx = -0.2f;
                        if (guard) { pose.sLx = -1.1f; pose.eL = -0.9f; }
                    }
                    else pose.bob = Mathf.Sin(g.clock * 3 + runPh) * 0.02f - 0.05f;
                    break;
                }
            }

            vy -= 30 * dt;
            y += vy * dt;
            x += vx * dt;
            // the ground under him: the path, or a bridge's deck (he walks onto it and follows its sag)
            float floor = 0;
            if (g.level != null && !Boss)
            {
                floor = g.level.SupportAt(x, Mathf.Max(y, 0) + 0.1f);
                if (floor < -0.6f) floor = 0;
            }
            if (y <= floor)
            {
                y = floor; vy = 0;
                if (state != State.Move && state != State.Atk) vx *= 1 - (1 - Mathf.Exp(-6 * dt));
            }
            // the sergeant keeps to his arena, a soldier to his stretch of path
            if (Boss && g.lockZone != null) x = Mathf.Clamp(x, g.lockZone.x - 10f, g.lockZone.x + 10f);
            else if (!Boss)
            {
                // at the end of his stretch he stops (he walked on the spot there: the user)
                float held = Mathf.Clamp(x, minX, maxX);
                if (held != x && (held - x) * vx < 0) vx = 0;
                x = held;
            }
            f.Apply(pose, dt, state == State.Atk ? 22 : 12);
            f.root.localPosition = new Vector3(x, y, seatZ);
            float target = facing * (90f + (model != null ? 8f : 25f));
            f.body.localRotation = Quaternion.Euler(0, Mathf.LerpAngle(f.body.localEulerAngles.y, target, 1 - Mathf.Exp(-12 * dt)), 0);
            Animate();
            PlaceGear(1 - Mathf.Exp(-25 * dt));

            // white flash when hit, white blink on wind-up, orange while burning
            // white on a hit; the modelled hyenas take less, or bloom turns them into a white cut-out
            if (flash > 0) f.SetFlash(Color.white, model != null ? 0.45f : 0.9f);
            else if (tele > 0) f.SetFlash(telePurple ? new Color(0.7f, 0.3f, 1f) : Color.white, tele);
            else if (burnT > 0) f.SetFlash(new Color(1f, 0.48f, 0.1f), 0.25f);
            else f.SetFlash(Color.black, 0);
            return false;
        }
    }
}
