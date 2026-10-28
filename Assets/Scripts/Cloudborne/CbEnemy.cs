using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Iron Crown hyena soldiers. The plain one walks up and swings a machete (white flash = wind-up);
    /// the shield variant blocks blows from the front until a staff move or the roar breaks its guard.
    /// </summary>
    public class CbEnemy
    {
        public enum Kind { Hyena, Shield }
        public enum State { Move, Atk, Hurt, Dying }

        public readonly Kind kind;
        public readonly Rig f;
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

        public CbEnemy(CbGame game, Kind k, float x0, Transform parent)
        {
            g = game;
            kind = k;
            x = x0;
            bool shield = k == Kind.Shield;
            hp = max = shield ? 46 : 34;
            speed = shield ? 2.1f : 2.7f;
            dmg = shield ? 10 : 8;
            reach = shield ? 1.6f : 1.5f;
            scoreValue = shield ? 500 : 300;
            if (shield) w = 0.6f;
            guard = shield;
            cd = Random.Range(0.5f, 1.4f);
            runPh = Random.Range(0f, 6f);

            f = RigBuilder.Build(new RigStyle
            {
                fur = CbArt.Hex("#b8925a"), paw = CbArt.Hex("#8a6a40"), top = CbArt.Hex("#6b727d"), armor = CbArt.Hex("#7d8590"),
                pants = CbArt.Hex("#4a3a2e"), belt = CbArt.Hex("#2a1f1a"), wrap = CbArt.Hex("#4a3a2e"), bulk = shield ? 1.12f : 1f,
            }, shield ? "ShieldHyena" : "Hyena");
            RigBuilder.HyenaHead(f);
            var blade = CbArt.Joint(f.R.hand, 0, -0.02f, 0.02f, "machete");
            CbArt.Box(f.M("#c9ced6"), blade, new Vector3(0, 0, 0.38f), new Vector3(0.03f, 0.1f, 0.62f));
            CbArt.Box(f.M("#2a1f1a"), blade, new Vector3(0, 0, 0.02f), new Vector3(0.05f, 0.06f, 0.16f));
            if (shield)
            {
                f.shield = CbArt.Joint(f.chest, 0.05f, 0.28f, 0.42f, "shield");
                CbArt.Box(f.M("#5d646e"), f.shield, Vector3.zero, new Vector3(0.85f, 1.15f, 0.08f));
                CbArt.Box(f.M("#3d434b"), f.shield, new Vector3(0, 0.55f, 0.01f), new Vector3(0.95f, 0.08f, 0.1f));
                var gold = f.M("#d9a53a", 0f);
                for (int i = -2; i <= 2; i++)
                    CbArt.Cone(gold, f.shield, new Vector3(i * 0.1f, 0.12f + (2 - Mathf.Abs(i)) * 0.04f, 0.06f), 0.05f, 0.16f, null, 4);
            }
            f.root.SetParent(parent, false);
            f.root.localPosition = new Vector3(x, 0, 0);
            f.body.localRotation = Quaternion.Euler(0, -115, 0);
        }

        void FaceTarget() => facing = g.hero.x >= x ? 1 : -1;

        public void Stagger(int dir, float kb, float up)
        {
            vx = dir * kb;
            vy = up;
            state = State.Hurt; t = 0;
        }

        public void Kill(int dir)
        {
            dead = true; state = State.Dying; t = 0;
            vx = dir * 10; vy = 7;
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
            if (state != State.Dying && state != State.Hurt && state != State.Atk) FaceTarget();
            var pose = Pose.Base;
            pose.sRx = 0.1f; pose.eR = -1.6f; pose.sRz = -0.3f;
            if (guard) { pose.sLx = -1.1f; pose.eL = -0.9f; pose.sLz = 0.1f; }

            switch (state)
            {
                case State.Dying:
                    pose.lean = -1.3f; pose.sLz = 1.4f; pose.sRz = -1.4f; pose.hLx = -1; pose.hRx = 0.3f; pose.headX = -0.5f;
                    f.flip.localRotation *= Quaternion.Euler(0, 0, facing * dt * 10 * Mathf.Max(0, 1 - t) * Mathf.Rad2Deg);
                    if (t > 0.9f)
                    {
                        f.root.localScale = Vector3.one * Mathf.Max(0.01f, 1 - (t - 0.9f) / 0.5f);
                        if (Random.value < 0.5f) CbFx.Spark(new Vector3(x, y + 1, -0.3f), CbFx.Purple, 1, 3, 0.4f, 0.5f, -3);
                    }
                    if (t > 1.4f) return true;
                    break;
                case State.Hurt:
                    pose.lean = -0.6f; pose.headX = -0.5f; pose.sLz = 1.1f; pose.sRz = -1.1f; pose.sLx = 0.5f; pose.sRx = 0.5f;
                    if (t > 0.42f && y <= 0) state = State.Move;
                    break;
                case State.Atk:
                {
                    const float wind = 0.45f;
                    if (t < wind)
                    {
                        pose.twist = -0.8f; pose.sRx = 0.9f; pose.eR = -1.8f; pose.lean = -0.1f; pose.hLx = -0.4f; pose.kL = 0.5f; pose.bob = -0.08f;
                        tele = Mathf.Sin(t * 50) > 0 ? 0.5f : 0;
                    }
                    else
                    {
                        pose.twist = 0.6f; pose.sRx = -1.6f; pose.eR = 0; pose.lean = 0.35f; pose.hLx = -0.9f; pose.kL = 0.4f; pose.hRx = 0.6f; pose.bob = -0.12f;
                        if (!hitDone)
                        {
                            hitDone = true;
                            float ddx = (hero.x - x) * facing;
                            if (ddx > -0.3f && ddx < reach + 0.4f && hero.y < 1.8f) hero.TakeHit(dmg, facing);
                            CbFx.Sfx("swing", 0.5f);
                        }
                    }
                    if (t > wind + 0.35f) { state = State.Move; cd = Random.Range(1f, 1.9f); }
                    break;
                }
                default:
                {
                    float push = 0;
                    foreach (var o in g.enemies)
                    {
                        if (o == this || o.dead) continue;
                        float d = x - o.x;
                        if (Mathf.Abs(d) < 1.1f) push += (d != 0 ? Mathf.Sign(d) : Random.Range(-1f, 1f)) * (1.1f - Mathf.Abs(d)) * 3;
                    }
                    float want = dist > reach ? Mathf.Sign(dx) * speed : 0;
                    vx += (want + push - vx) * (1 - Mathf.Exp(-8 * dt));
                    if (dist <= reach + 0.2f && cd <= 0 && hero.state != CbHero.State.Dead && g.mode == CbGame.Mode.Play)
                    {
                        state = State.Atk; t = 0; hitDone = false; vx = 0;
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
            if (y <= 0)
            {
                y = 0; vy = 0;
                if (state != State.Move && state != State.Atk) vx *= 1 - (1 - Mathf.Exp(-6 * dt));
            }
            f.Apply(pose, dt, state == State.Atk ? 22 : 12);
            f.root.localPosition = new Vector3(x, y, 0);
            float target = facing * (90f + 25f);
            f.body.localRotation = Quaternion.Euler(0, Mathf.LerpAngle(f.body.localEulerAngles.y, target, 1 - Mathf.Exp(-12 * dt)), 0);

            // white flash when hit, white blink on wind-up, orange while burning
            if (flash > 0) f.SetFlash(Color.white, 0.9f);
            else if (tele > 0) f.SetFlash(Color.white, tele);
            else if (burnT > 0) f.SetFlash(new Color(1f, 0.48f, 0.1f), 0.25f);
            else f.SetFlash(Color.black, 0);
            return false;
        }
    }
}
