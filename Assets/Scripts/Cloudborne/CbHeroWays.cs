using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Raja on the things along the long stage (CbLevelWays): swinging on vines, climbing vine-grown rock, hanging from
    /// bamboo bars, sliding down a zipline, pushing the stone block, riding an updraft, bouncing off a springy leaf,
    /// falling into a pit, and coming back at a stone lantern. Clips from Meshy's library: "Rope Hang Idle", "Climb Up
    /// Rope", "Slow Bar Hang Left", "Push and Walk Forward".
    /// </summary>
    public partial class CbHero
    {
        // his hands above his feet hanging from a rope or vine, and from a bar (measured on the model at its game size)
        const float RopeReach = 1.48f, BarReach = 1.71f;
        const string BarHangClip = "Bar_Hang_Idle", BarMoveClip = "MonkeyBars";
        // the swing's soft limit, how hard it turns him back past it, and the hard one
        const float SoftSwing = 0.95f, SwingWall = 40f;
        const float MaxSwing = 1.3f, VineClimbSpeed = 2.6f, BarSpeed = 2.4f, ZipTop = 13f, PitDamage = 10f, PushSpeed = 1.6f;

        CbLevel.Vine swingVine;
        CbLevel.Bar bar;
        CbLevel.Zip zip;
        float climbPh, barPh, zipSpeed, vineEdge, vineTop;
        int vineDir = 1;
        bool barMoving, pushing;
        /// <summary>The vine he swings on, if he does.</summary>
        public CbLevel.Vine SwingingOn => state == State.Swing ? swingVine : null;
        public bool Pushing => pushing;

        /// <summary>
        /// In the air (or on the ground for a vine wall), whatever he can take hold of: a vine-grown side pressing
        /// towards it and up; a vine, a bar or a zipline within reach of his hands. True if he took hold.
        /// </summary>
        bool TryCatch(CbInput input)
        {
            var lv = g.level;
            int into = input.x > 0.3f ? 1 : input.x < -0.3f ? -1 : 0;
            // pushing into it is enough (there is no up on the controls now)
            if (into != 0 && input.y > -0.3f && lv.VineWallAt(x, y, into, out float edgeX, out float top)) { StartVine(into, edgeX, top); return true; }
            if (state != State.Air || grabCd > 0) return false;
            var po = lv.PoleNear(x, y + RopeReach);
            if (po != null && vy < 6f) { StartPole(po); return true; }
            var v = lv.VineNear(x, y + RopeReach);
            if (v != null) { StartSwing(v); return true; }
            var b = lv.BarNear(x, y + BarReach);
            if (b != null && vy < 4f) { StartBars(b); return true; }
            var z = lv.ZipNear(x, y + RopeReach);
            if (z != null) { StartZip(z); return true; }
            return false;
        }

        // ---------- his hands on what he holds ----------

        // (the user: swinging, his hands held the air above his head; on the bars the pole ran through his forearms)
        // he is moved so his palms meet the vine's end, the bar or the zipline, measured from the pose the animation
        // gave him the frame before; the bamboo pole's middle is BarPoleUp over the bar's line
        const float BarPoleUp = 0.02f, GripReach = 0.7f;
        Vector3 gripFix;

        /// <summary>Where his hands hold on (world); across: whether along x too (the vine) or only up and down.</summary>
        bool GripPoint(out Vector3 at, out bool across)
        {
            at = default;
            across = false;
            switch (state)
            {
                case State.Swing when swingVine != null:
                    float vl = swingVine.len + swingVine.stretch;
                    at = new Vector3(swingVine.ax + Mathf.Sin(swingVine.angle) * vl, swingVine.ay - Mathf.Cos(swingVine.angle) * vl, 0);
                    across = true;
                    return true;
                case State.Bars when bar != null: at = new Vector3(0, bar.y + BarPoleUp, 0); return true;
                case State.Pole when pole != null: at = new Vector3(pole.x, pole.y, 0); across = true; return true;
                // hanging from an edge: his palms on its corner
                case State.Hang: at = new Vector3(grabX - grabDir * 0.06f, grabTop + 0.02f, 0); across = true; return true;
                case State.Zip when zip != null: at = new Vector3(0, zip.At(x), 0); return true;
            }
            return false;
        }

        /// <summary>What to add to where he is placed so his palms are on what he holds (eased; none when he lets go).</summary>
        Vector3 GripFix(float dt)
        {
            var want = Vector3.zero;
            if (model != null && GripPoint(out var at, out bool across))
            {
                // his palms from his root as last drawn (the root has not been moved yet this frame)
                var palms = (model.Palm(model.leftHand) + model.Palm(model.rightHand)) * 0.5f - f.root.position;
                var placed = f.root.parent != null ? f.root.parent.TransformPoint(new Vector3(x, y, 0)) : new Vector3(x, y, 0);
                want = new Vector3(across ? at.x - (placed.x + palms.x) : 0, at.y - (placed.y + palms.y), 0);
                want = Vector3.ClampMagnitude(want, GripReach);
            }
            gripFix = Vector3.Lerp(gripFix, want, 1 - Mathf.Exp(-18f * dt));
            return gripFix;
        }

        void TakeHold()
        {
            t = 0; move = Move.None; combo = 0; flipT = 0;
            vx = vy = 0; air = true; jumps = 0; airDashed = false; airHits = 0;
        }

        void ArmsUp(ref Pose pose)
        {
            pose.sLx = -2.9f; pose.sRx = -2.9f; pose.eL = -0.2f; pose.eR = -0.2f;
            pose.hLx = -0.4f; pose.kL = 0.6f; pose.hRx = 0.2f; pose.kR = 0.4f;
        }

        // ---------- a vine to swing on ----------

        void StartSwing(CbLevel.Vine v)
        {
            float speed = vx, vyIn = vy;
            TakeHold();
            state = State.Swing;
            swingVine = v;
            float dx = x - v.ax, dy = v.ay - (y + RopeReach);
            v.angle = Mathf.Clamp(Mathf.Atan2(dx, Mathf.Max(0.5f, dy)), -1.1f, 1.1f);
            v.speed = Mathf.Clamp(speed * Mathf.Cos(v.angle) / v.len, -2.5f, 2.5f);
            // the vine gives under his weight, stretching and springing back
            v.stretchV += Mathf.Max(2.5f, -vyIn * 0.45f);
            CbFx.Sfx("staff", 0.5f);
        }

        /// <summary>Swinging: a pendulum pumped by the stick; JUMP lets go, flung along the swing (down just drops).</summary>
        void UpdateSwing(ref Pose pose, CbInput input, float dt)
        {
            var v = swingVine;
            float acc = -(30f / v.len) * Mathf.Sin(v.angle) + input.x * 1.8f;
            // near the top of the swing a soft limit eases him round and back (a hard one stopped him dead there, and he
            // stuck: the user)
            float over = Mathf.Abs(v.angle) - SoftSwing;
            if (over > 0) acc -= Mathf.Sign(v.angle) * over * SwingWall;
            v.speed = (v.speed + acc * dt) * (1 - 0.15f * dt);
            float next = v.angle + v.speed * dt;
            if (Mathf.Abs(next) > MaxSwing) { next = Mathf.Sign(next) * MaxSwing; v.speed = -v.speed * 0.3f; }
            v.angle = next;
            // swinging fast, the springy vine stretches a little
            v.stretchV += v.speed * v.speed * 0.6f * dt;
            float len = v.len + v.stretch;
            x = v.ax + Mathf.Sin(v.angle) * len;
            y = v.ay - Mathf.Cos(v.angle) * len - RopeReach;
            vx = vy = 0;
            if (Mathf.Abs(input.x) > 0.3f) facing = input.x > 0 ? 1 : -1;
            ArmsUp(ref pose);
            if (t > 0.15f && (input.jump || input.y < -0.5f))
            {
                vx = Mathf.Cos(v.angle) * v.speed * v.len * 1.1f;
                vy = Mathf.Sin(v.angle) * v.speed * v.len + (input.jump ? 5f : 0);
                v.cool = 0.6f;
                state = State.Air; t = 0; air = true; jumps = 1; grabCd = 0.25f;
                CbFx.Sfx("jump", 0.6f);
            }
        }

        // ---------- vine-grown rock to climb ----------

        void StartVine(int dir, float edgeX, float top)
        {
            TakeHold();
            state = State.Vine;
            vineDir = facing = dir; vineEdge = edgeX; vineTop = top;
            x = edgeX - dir * CbLevel.HalfWidth;
        }

        /// <summary>Climbing the vines up or down; at the top he pulls himself over; JUMP kicks off; away lets go.</summary>
        void UpdateVine(ref Pose pose, CbInput input, float dt)
        {
            x = vineEdge - vineDir * CbLevel.HalfWidth;
            // up while he pushes into the rock (or up on a stick), down with down; let go, he holds on
            float climb = Mathf.Abs(input.y) > 0.3f ? input.y : input.x * vineDir > 0.3f ? 1f : 0f;
            float step = climb * VineClimbSpeed * dt;
            y = Mathf.Max(0, y + step);
            vx = vy = 0;
            climbPh += Mathf.Abs(step) * 0.6f;
            ArmsUp(ref pose);
            if (y >= vineTop - HangReach)
            {
                StartHang(vineDir, vineEdge, vineTop);
                state = State.Climb; t = 0;
                return;
            }
            // (up on the stick climbs here: only a jump of another kind kicks off)
            if (input.jump && !input.stickJump)
            {
                vy = 9f; vx = -vineDir * 5f; facing = -vineDir;
                state = State.Air; t = 0; air = true; jumps = 1; grabCd = 0.3f; wallLockT = WallLock;
                CbFx.Sfx("jump", 0.6f);
            }
            else if (input.x * vineDir < -0.3f || (y <= 0.01f && input.y < -0.3f))
            {
                air = y > 0.01f;
                state = air ? State.Air : State.Idle; t = 0; grabCd = 0.3f;
            }
        }

        // ---------- bamboo bars ----------

        void StartBars(CbLevel.Bar b)
        {
            TakeHold();
            state = State.Bars;
            bar = b;
            y = b.y - BarReach;
            CbFx.Sfx("staff", 0.4f);
        }

        /// <summary>Hand over hand along the bar; off its end, down or JUMP lets go (a jump flings him on).</summary>
        void UpdateBars(ref Pose pose, CbInput input, float dt)
        {
            float step = Mathf.Abs(input.x) > 0.3f ? Mathf.Sign(input.x) * BarSpeed * dt : 0;
            if (step != 0) facing = step > 0 ? 1 : -1;
            x += step;
            y = bar.y - BarReach;
            vx = vy = 0;
            barMoving = step != 0;
            if (barMoving) barPh += dt * 1.1f;
            // thorns on the bar: his hands on them, it hurts and he jerks back (a jump along the bar clears them). They
            // stop him even while a blow before keeps him unhurt (he walked through them then)
            foreach (float th in bar.thorns)
                if (Mathf.Abs(x - th) < CbLevel.ThornWide * 0.5f + 0.1f)
                {
                    int back = x < th ? -1 : 1;
                    if (inv <= 0) TakeHit(8f, back, unblockable: true);
                    x = th + back * (CbLevel.ThornWide * 0.5f + (inv > 0 ? 0.12f : 0.45f));
                    state = State.Bars;
                    break;
                }
            ArmsUp(ref pose);
            if (x < bar.x0 || x > bar.x1 || input.jump || input.y < -0.5f)
            {
                vx = facing * (input.jump ? 4.5f : 1.5f);
                vy = input.jump ? 6.5f : 0;
                state = State.Air; t = 0; air = true; jumps = 1; grabCd = 0.35f;
                if (input.jump) CbFx.Sfx("jump", 0.6f);
            }
        }

        // ---------- a swing pole ----------

        CbLevel.Pole pole;
        float poleAngle, poleSpeed;
        /// <summary>The swing pole he hangs from, if he does.</summary>
        public CbLevel.Pole OnPole => state == State.Pole ? pole : null;
        /// <summary>How far he has swung from hanging straight down (radians, + his feet to the right).</summary>
        public float PoleAngle => poleAngle;
        // a pendulum from his hands on the bar: its pull (gravity over the length from his hands to his middle), how hard
        // the stick pumps it, how it dies away, how fast it can go; the reach from his turning point to his hands
        const float PolePull = 30f / PoleLength, PoleLength = 1.05f, PolePump = 7f, PoleDrag = 0.35f, PoleMaxSpeed = 7.5f;
        /// <summary>Letting go with JUMP: the swing's speed carried (from his middle, PoleLength out), and a jump's lift.</summary>
        public const float PoleLift = 5f;
        // his hands over the turning point of his model (a metre over his feet), hanging at arm's length
        const float PoleHands = 0.71f;

        void StartPole(CbLevel.Pole p)
        {
            float vxIn = vx;
            TakeHold();
            state = State.Pole;
            pole = p;
            // hanging from it as he caught it: leaning the way he came, a little, and swinging on with his speed
            poleAngle = Mathf.Clamp(Mathf.Atan2(x - p.x, Mathf.Max(0.3f, p.y - (y + 1f))), -0.6f, 0.6f);
            poleSpeed = Mathf.Clamp(vxIn * Mathf.Cos(poleAngle) / PoleLength, -3f, 3f);
            CbFx.Sfx("staff", 0.5f);
        }

        /// <summary>
        /// Hanging from a swing pole (the user: catch it and hang; left and right swing him, with reasonable physics;
        /// his hands on the pole): a pendulum from his hands - the stick pumps it the way it is pushed, the swing dies
        /// away by itself; JUMP lets go, carried on along the swing with a jump's lift; down drops him.
        /// </summary>
        void UpdatePole(ref Pose pose, CbInput input, float dt)
        {
            float acc = -PolePull * Mathf.Sin(poleAngle) + input.x * PolePump - poleSpeed * PoleDrag;
            poleSpeed = Mathf.Clamp(poleSpeed + acc * dt, -PoleMaxSpeed, PoleMaxSpeed);
            poleAngle += poleSpeed * dt;
            // all the way over the bar and round (a giant swing) is allowed; the angle kept in -pi..pi
            if (poleAngle > Mathf.PI) poleAngle -= 2 * Mathf.PI;
            if (poleAngle < -Mathf.PI) poleAngle += 2 * Mathf.PI;
            // his hands on the bar: his turning point under it along the swing
            x = pole.x + PoleHands * Mathf.Sin(poleAngle);
            y = pole.y - PoleHands * Mathf.Cos(poleAngle) - 1f;
            vx = vy = 0;
            if (Mathf.Abs(input.x) > 0.3f) facing = input.x > 0 ? 1 : -1;
            ArmsUp(ref pose);
            // legs: swung forward up the swing (the body leads), trailing back down it
            float lead = Mathf.Clamp(poleSpeed * 0.12f, -0.6f, 0.6f) * facing;
            pose.hLx = pose.hRx = -0.2f - Mathf.Max(0, lead);
            pose.kL = pose.kR = 0.15f + Mathf.Max(0, -lead) * 0.5f;
            if (t > 0.15f && (input.jump || input.y < -0.5f))
            {
                bool fling = input.jump;
                // the swing's own speed at his middle, along the swing
                float v = poleSpeed * PoleLength;
                vx = v * Mathf.Cos(poleAngle);
                vy = v * Mathf.Sin(poleAngle) + (fling ? PoleLift : 0f);
                if (Mathf.Abs(vx) > 0.5f) facing = vx > 0 ? 1 : -1;
                pole.cool = 0.5f;
                state = State.Air; t = 0; air = true; jumps = 1; grabCd = 0.3f;
                if (fling) CbFx.Sfx("jump", 0.7f);
            }
        }

        // ---------- a zipline ----------

        void StartZip(CbLevel.Zip z)
        {
            float speed = Mathf.Abs(vx);
            TakeHold();
            state = State.Zip;
            zip = z;
            zipSpeed = Mathf.Max(3f, speed);
            facing = z.x1 > z.x0 ? 1 : -1;
            CbFx.Sfx("dash", 0.6f);
        }

        /// <summary>Sliding down, faster and faster; at its end (or with JUMP) he flies on.</summary>
        void UpdateZip(ref Pose pose, CbInput input, float dt)
        {
            int dir = zip.x1 > zip.x0 ? 1 : -1;
            zipSpeed = Mathf.Min(ZipTop, zipSpeed + 9f * dt);
            x += dir * zipSpeed * dt;
            y = zip.At(x) - RopeReach;
            vx = vy = 0;
            ArmsUp(ref pose);
            if (Random.value < 0.4f) CbFx.Spark(new Vector3(x, y + RopeReach, -0.3f), CbFx.Flame, 1, 2f, 0.15f, 0.25f, 6);
            bool end = dir > 0 ? x >= zip.x1 - 0.6f : x <= zip.x1 + 0.6f;
            if (end || input.jump)
            {
                vx = dir * zipSpeed * 0.7f;
                vy = input.jump ? 7f : 2f;
                state = State.Air; t = 0; air = true; jumps = 1; grabCd = 0.4f;
            }
        }

        // ---------- thrown, fallen, back again ----------

        /// <summary>Into a pit of spikes: health lost, and back where he last stood safely.</summary>
        public void FellInPit()
        {
            if (state == State.Dead) return;
            hp = Mathf.Max(0, hp - PitDamage);
            g.hud.Popup(new Vector3(x, y + 2.6f, 0), "-" + Mathf.RoundToInt(PitDamage), CbHud.PopStyle.Hurt);
            g.hud.FlashHurt();
            g.hud.SetTip("A pit under the leaves! Look for loose ground - jump over it");
            CbFx.Sfx("hurt");
            x = safeX; y = safeY; vx = vy = 0; air = false; jumps = 0; flipT = 0;
            state = State.Idle; t = 0; move = Move.None; combo = 0;
            inv = Mathf.Max(inv, 1f);
            CbFx.Puff(new Vector3(x, y + 0.6f, -0.3f), new Color(0.8f, 0.75f, 0.65f), 6, 1.4f, 0.7f, 0.45f);
            if (hp <= 0) { state = State.Dead; t = 0; g.Invoke(nameof(CbGame.Defeated), 1.4f); }
        }

        /// <summary>Fallen: back at a stone lantern, whole again, a moment untouchable.</summary>
        public void RespawnAt(float at)
        {
            Reset();
            x = safeX = at; y = safeY = 0;
            inv = 2f;
            Place();
            CbFx.Spark(new Vector3(x, 1f, -0.3f), CbFx.Flame, 16, 3, 0.35f, 0.6f, -3);
        }

        /// <summary>The clip for the ways' states (Animate asks first); false if none of them.</summary>
        bool AnimateWays()
        {
            switch (state)
            {
                // hanging from a vine, a rope or the zipline
                case State.Swing:
                case State.Zip: model.Show("Rope_Hang_Idle", 0.5f, 0.1f); return true;
                // round a swing pole: hanging by both hands, arms straight up, turned about it (CbHero.Update)
                case State.Pole when model.HasClip(BarHangClip): model.LoopSeconds(BarHangClip, g.clock, 0.15f); return true;
                case State.Pole: model.Show("Rope_Hang_Idle", 0.5f, 0.1f); return true;
                case State.Vine: model.Loop("Climb_Up_Rope", climbPh); return true;
                // the bar: hanging from it by both hands, arms straight up (Meshy's "Bar Hang Idle"), and along it hand
                // over hand, facing the way he goes, legs swinging (made in Meshy from a description); the shimmy he
                // had looked like clinging to a wall, not a pole (the user)
                case State.Bars when barMoving && model.HasClip(BarMoveClip): model.Loop(BarMoveClip, barPh, 0.12f); return true;
                case State.Bars when model.HasClip(BarHangClip): model.LoopSeconds(BarHangClip, g.clock, 0.15f); return true;
                case State.Bars:
                    if (barMoving) model.Loop("Slow_Bar_Hang_Left", barPh);
                    else model.Show("Slow_Bar_Hang_Left", 0f, 0.1f);
                    return true;
                case State.Run when pushing: model.LoopSeconds("Push_and_Walk_Forward", walkT * 2.2f); return true;
            }
            return false;
        }
    }
}
