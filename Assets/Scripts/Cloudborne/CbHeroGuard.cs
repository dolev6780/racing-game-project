using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The perfect guard (the story doc: Baba's lesson in stage 1-3 - guard just as a blow lands and it is returned;
    /// stones and arrows too). There is no guard button on the phone, so (the user's choice) a swipe INTO the blow is
    /// the guard: swiped toward a soldier close in front, or toward something thrown that is coming, Raja raises his
    /// guard for ParryTime instead of dodging; a blow from that side landing in it is returned (the soldier takes a
    /// heavy blow, a spear or stone flies back the way it came), with a flash and a moment of slow motion. Swiped any
    /// other time, it is the dodge as before. Learned once, kept (CbSave.PerfectGuard).
    /// </summary>
    public partial class CbHero
    {
        /// <summary>He has learned the perfect guard (Baba, stage 1-3).</summary>
        public static bool PerfectGuardLearned => CbSave.PerfectGuard;

        public const float ParryTime = 0.32f, ParryReach = 2.6f;
        const float ParryDamage = 16f, ParryLook = 4.5f;
        float parryT;

        /// <summary>His guard is up from a swipe into a blow: a blow landing now is returned.</summary>
        public bool Parrying => parryT > 0;

        /// <summary>Something to guard against close by that way: a soldier within reach, or a spear or stone coming.</summary>
        bool IntoTheBlow(int dir)
        {
            if (!PerfectGuardLearned || air) return false;
            foreach (var e in g.enemies)
            {
                if (e.dead || Mathf.Abs(e.y - y) > 1.5f) continue;
                float ahead = (e.x - x) * dir;
                if (ahead > -0.3f && ahead < ParryReach + e.w) return true;
            }
            return g.level != null && g.level.ThrownComing(x, y, dir, ParryLook);
        }

        /// <summary>The guard raised into the blow (from a swipe that way).</summary>
        void StartParry(int dir)
        {
            facing = dir;
            state = State.Guard; t = 0; vx = 0;
            parryT = ParryTime;
            CbFx.Sfx("staff", 0.5f);
        }

        /// <summary>A blow from in front landing while the guard is up from a swipe: returned.</summary>
        void ReturnBlow(int dir)
        {
            parryT = 0;
            blockT = BlockReact;
            var at = new Vector3(x + facing * 0.6f, y + 1.2f, -0.4f);
            CbFx.Spark(at, new Color(1f, 0.85f, 0.4f) * 2.5f, 18, 7, 0.35f, 0.35f, 2);
            CbVfx.Ring(at, CbFx.Hit * 0.6f, 1.8f, 0.3f);
            CbVfx.Burst(at, CbFx.Hit, 2f, 0.15f);
            CbFx.Sfx("perfect");
            CbFx.Sfx("block", 0.8f);
            g.hud.Banner("PERFECT GUARD!", true);
            g.hud.ImpactFlash(0.35f);
            g.hitstop = Mathf.Max(g.hitstop, 0.08f);
            g.shake = Mathf.Max(g.shake, 0.25f);
            // the one who struck, close in front, takes it back hard
            CbEnemy foe = null;
            float best = float.MaxValue;
            foreach (var e in g.enemies)
            {
                if (e.dead || Mathf.Abs(e.y - y) > 1.6f) continue;
                float ahead = (e.x - x) * facing;
                if (ahead > -0.3f && ahead < ParryReach + e.w + 0.6f && ahead < best) { best = ahead; foe = e; }
            }
            if (foe != null) g.HurtEnemy(foe, ParryDamage, facing, 9f, 4f, true, true, fire: false, fromPlayer: true);
        }
    }
}
