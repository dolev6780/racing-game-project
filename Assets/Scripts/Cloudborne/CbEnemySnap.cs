using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Sergeant Snap, the first boss (stage 1-1), made more of a fight to want to win (the user: easy, as the first boss
    /// should be, but more fun - the game should make you want to keep playing):
    /// <list type="bullet">
    /// <item>His break gauge (the yellow bar under his health): Raja's blows fill it, a perfect dodge of one of his
    /// fills a third of it, and blows while his cleaver is stuck fill it twice as fast. Full, he is BROKEN: dizzy for
    /// SnapStunTime seconds, stars round his head, taking half as much again from every blow, coins spilling from his
    /// purse. Each break makes the next a little slower to fill.</item>
    /// <item>His chop: the cleaver bites the ground and sticks there for SnapStuckTime, while he tugs at it - the moment
    /// to strike.</item>
    /// <item>Half beaten, he flies into a rage: a roar that shoves Raja back, then faster, sooner to strike again, a
    /// fourth blow at the end of his combo, and a second leap after the first.</item>
    /// </list>
    /// </summary>
    public partial class CbEnemy
    {
        /// <summary>His break gauge, 0 to 1 (full: broken).</summary>
        public float breakMeter;
        /// <summary>Seconds left of his daze after a break.</summary>
        public float stunT;
        /// <summary>Half beaten: in a rage.</summary>
        public bool furious;
        // seconds left of his cleaver stuck in the ground after a chop; breaks so far; a second leap still to come
        float stuckT;
        int breaks;
        bool leapAgain, secondLeap;

        public bool Stunned => stunT > 0;
        public bool Stuck => stuckT > 0;
        public int Breaks => breaks;

        public const float SnapStunTime = 3f, SnapStuckTime = 2f;
        // how much of his health in Raja's blows fills the gauge (each break after the first a third more); a perfect dodge's share
        public const float SnapBreakHp = 70f, SnapBreakDodge = 0.34f;
        // in a rage: how much faster he moves, how much sooner he strikes again
        const float FurySpeed = 1.3f, FuryRest = 0.7f;
        // his combo in a rage: a fourth blow, heavier, at the end
        static readonly float[] FuryComboBlows = { 0.36f, 0.66f, 0.96f, 1.32f };
        const float FuryComboTime = 1.8f;

        float[] Blows => furious ? FuryComboBlows : ComboBlows;
        float ComboLength => furious ? FuryComboTime : ComboTime;
        float Rest(float s) => furious ? s * FuryRest : s;

        /// <summary>Fills his break gauge by amount (a share of it); full, he is broken.</summary>
        public void AddBreak(float amount)
        {
            if (!Boss || dead || stunT > 0) return;
            breakMeter = Mathf.Min(1f, breakMeter + amount / (1f + 0.35f * breaks));
            if (breakMeter >= 1f) Break();
        }

        void Break()
        {
            breaks++;
            breakMeter = 0;
            stunT = SnapStunTime;
            stuckT = 0;
            open = 0;
            whistled = hitDone = true;
            leapAgain = false;
            if (state == State.Atk) { state = State.Move; t = 0; }
            vx = 0;
            g.SnapBroken(this);
        }

        /// <summary>Dazed: swaying on his feet, stars circling his head.</summary>
        void Dizzy(ref Pose pose, float dt)
        {
            stunT = Mathf.Max(0, stunT - dt);
            vx = Mathf.MoveTowards(vx, 0, 20 * dt);
            pose.lean = -0.35f + Mathf.Sin(g.clock * 3.2f) * 0.15f;
            pose.headX = -0.4f;
            for (int k = 0; k < 3; k++)
            {
                float a = g.clock * 5f + k * Mathf.PI * 2f / 3f;
                var at = new Vector3(x + Mathf.Cos(a) * 0.55f, HitHeight + 1.25f + Mathf.Sin(a * 2f) * 0.06f, -0.5f + Mathf.Sin(a) * 0.3f);
                CbFx.Spark(at, new Color(1f, 0.9f, 0.35f) * 2.2f, 1, 0f, 0.16f, 0.08f, 0);
            }
            if (stunT <= 0)
            {
                // shaking it off
                cd = 0.6f;
                CbFx.Puff(new Vector3(x, HitHeight + 0.6f, -0.4f), new Color(0.95f, 0.92f, 0.85f), 6, 1.5f, 0.6f, 0.4f);
            }
        }

        /// <summary>His cleaver stuck in the ground: he tugs at it, dust kicking up, until it comes free with a jerk.</summary>
        void Tug(ref Pose pose, float dt)
        {
            stuckT = Mathf.Max(0, stuckT - dt);
            vx = 0;
            pose.lean = 0.45f + Mathf.Sin(g.clock * 22f) * 0.05f;
            if (Random.value < 0.3f)
                CbFx.Spark(new Vector3(x + facing * 1.6f, 0.15f, -0.3f), CbFx.Dust, 1, 2f, 0.25f, 0.3f, 4);
            if (stuckT <= 0)
            {
                open = 0;
                cd = Rest(0.9f);
                CbFx.Spark(new Vector3(x + facing * 1.6f, 0.2f, -0.3f), CbFx.Dust, 10, 4f, 0.4f, 0.4f, 4);
                CbFx.Sfx("swing", 0.6f);
            }
        }

        /// <summary>Half beaten: the roar, shoving Raja back if he stands close; from then on, the rage.</summary>
        void Roar(ref Pose pose, CbHero hero)
        {
            vx = 0;
            tele = 0.35f;
            if (t > 0.3f && !hitDone)
            {
                hitDone = true;
                furious = true;
                g.SnapFurious(this);
                if (Mathf.Abs(hero.x - x) < 3.5f && hero.state != CbHero.State.Dead)
                    hero.vx = (hero.x >= x ? 1 : -1) * 9f;
            }
            if (t >= RoarTime) { state = State.Move; cd = 0.5f; }
        }

        const float RoarTime = 1.2f;

        /// <summary>The rage's colour on him: a red glow, pulsing.</summary>
        bool FuryGlow(out float k)
        {
            k = 0.1f + 0.08f * Mathf.Sin(g.clock * 6f);
            return furious && !dead;
        }
    }
}
