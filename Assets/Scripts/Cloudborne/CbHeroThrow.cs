using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The staff throw (the story doc: the hidden scroll of stage 1-2; "the staff flies forward and comes back like a
    /// boomerang", by holding KI; it reaches high fruit and cuts thorn walls). Learned once, kept (CbSave.StaffThrow).
    /// He throws it spinning ThrowReach ahead at chest height, slowing as it goes; it hits every soldier, saw drone,
    /// crate and thorn wall it passes (once each way), takes what it touches (coins, fruit up high) and comes back
    /// faster and faster to his hand, wherever he is by then. While it is away he has only his paws.
    /// </summary>
    public partial class CbHero
    {
        /// <summary>He has learned the staff throw (the scroll in stage 1-2's hidden nest).</summary>
        public static bool StaffThrowLearned => CbSave.StaffThrow;

        public const float ThrowCost = 15f, ThrowReach = 7.5f;
        // the throwing motion, and the moment in it the staff leaves his hand; the flight out; the hurt it does
        const float ThrowTime = 0.34f, ThrowRelease = 0.12f, ThrowOut = 0.5f, ThrowDamage = 12f, ThrowBackMax = 22f;
        const float ThrowHigh = 1.15f, ThrowSpin = 1300f, ThrowStrikeEvery = 0.12f, ThrowCatch = 0.6f, ThrowLost = 3f;

        bool staffFlying, staffComing;
        Vector3 flyPos;
        float flyT, flyFromX, flyY, flyStrike;
        int flyDir;
        readonly HashSet<CbEnemy> flyHits = new HashSet<CbEnemy>();

        /// <summary>The thrown staff is away from his hand (test hook), and where it is.</summary>
        public bool StaffFlying => staffFlying;
        public Vector3 StaffAt => flyPos;

        /// <summary>The staff leaves his hand, spinning.</summary>
        void LetStaffFly()
        {
            staffFlying = true;
            staffComing = false;
            flyT = 0;
            flyStrike = 0;
            flyDir = facing;
            flyFromX = x + facing * 0.4f;
            flyY = y + ThrowHigh;
            flyPos = new Vector3(flyFromX, flyY, -0.2f);
            flyHits.Clear();
            CbFx.Sfx("swing", 0.8f);
            g.hud.SpeedLines(0.35f, 0.2f);
        }

        /// <summary>Back in his hand (or put back there: a fall, a respawn).</summary>
        void CatchStaff(bool silent = false)
        {
            if (!staffFlying) return;
            staffFlying = false;
            staffHold = 0.4f;
            if (silent) return;
            CbFx.Sfx("staff", 0.7f);
            CbFx.Spark(flyPos, CbFx.Hit, 6, 2.5f, 0.25f, 0.25f, 0);
        }

        /// <summary>One frame of the staff's flight: out, back, and what it meets on the way.</summary>
        void UpdateThrownStaff(float dt)
        {
            if (!staffFlying) return;
            flyT += dt;
            var from = flyPos;
            var hand = model != null ? model.Palm(model.rightHand) : new Vector3(x + facing * 0.35f, y + ThrowHigh, -0.2f);
            if (!staffComing)
            {
                // out, slowing to a stop at its furthest, a little rise and fall on the way
                float k = Mathf.Clamp01(flyT / ThrowOut);
                float ease = 1 - (1 - k) * (1 - k);
                flyPos = new Vector3(flyFromX + flyDir * ThrowReach * ease, flyY + Mathf.Sin(k * Mathf.PI) * 0.25f, -0.2f);
                if (k >= 1) { staffComing = true; flyHits.Clear(); }
            }
            else
            {
                // back to his hand, faster and faster
                float speed = Mathf.Min(ThrowBackMax, 4f + (flyT - ThrowOut) * 40f);
                flyPos = Vector3.MoveTowards(flyPos, hand, speed * dt);
                if ((flyPos - hand).sqrMagnitude < ThrowCatch * ThrowCatch || flyT > ThrowLost) { CatchStaff(); return; }
            }
            int dir = flyPos.x >= from.x ? 1 : -1;
            // the soldiers it passes, each once on the way out and once on the way back (a staff blow: shields break)
            foreach (var e in g.enemies)
            {
                if (e.dead || flyHits.Contains(e)) continue;
                if (Mathf.Abs(e.x - flyPos.x) < 0.55f + e.w * 0.5f && Mathf.Abs(e.HitHeight - flyPos.y) < 1.1f)
                {
                    flyHits.Add(e);
                    g.HurtEnemy(e, ThrowDamage, dir, 4f, 1.5f, false, true, fire: false, fromPlayer: true, ki: true);
                    CbFx.Sfx("hit", 0.6f);
                }
            }
            // saw drones, thorn walls, crates (each counts a blow once a moment), and what it touches it takes
            flyStrike -= dt;
            if (flyStrike <= 0)
            {
                flyStrike = ThrowStrikeEvery;
                float sx = flyPos.x - dir * 0.6f, sy = flyPos.y - ThrowHigh;
                g.level?.Strike(sx, sy, dir, 1.2f, staff: true);
                g.items?.HitCrates(sx, sy, dir, 1.2f);
            }
            g.items?.Fetch(flyPos, 0.7f, this);
            // spinning edge-on to the camera like a propeller, a trail of wind behind it
            staff.position = flyPos;
            staff.rotation = Quaternion.AngleAxis(-flyDir * flyT * ThrowSpin, Vector3.forward);
            if (Random.value < 0.5f) CbFx.Spark(flyPos + new Vector3(0, 0, -0.1f), CbFx.Grey * 0.6f, 1, 0.5f, 0.3f, 0.2f, 0);
        }
    }
}
