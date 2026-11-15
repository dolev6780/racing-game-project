using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Stage 1-3, Baba's Grove (the story doc: training, about 3 minutes - ring five bells in order, break ten bamboo
    /// dummies with the staff storm, block three stones with timing; Baba teaches that fire held by the heart protects,
    /// and dodges questions about the prophecy). A sunny clearing by Baba's hut; three trials one after the other, each
    /// shutting the way until it is done (CbGame.UpdateTrials):
    /// <list type="bullet">
    /// <item>The bells (around BellsX): Baba rings the five bells in an order, Raja rings them back the same way (hit
    /// them: paws, staff, the staff throw). A wrong bell and Baba wags his finger and rings them again.</item>
    /// <item>The dummies (around DummiesX): ten bamboo dummies. Blows only rock them, but each one he lands fills his ki;
    /// the staff storm (SPECIAL) breaks them.</item>
    /// <item>The stones (around StonesX): Baba teaches the perfect guard and throws stones; Raja swipes into them as they
    /// reach him and sends three back (Baba swats them away, laughing).</item>
    /// </list>
    /// The bell and the dummy were drawn in Canva and built in Meshy; Baba is the opening's model.
    /// </summary>
    public partial class CbLevel
    {
        public const float GroveMax = 132f, GroveGoal = 126f;
        static readonly (float x0, float x1)[] GroveWaters = new (float, float)[0];
        /// <summary>The middles of the three trials' places, in order: the bells, the dummies, the stones.</summary>
        public static readonly float[] TrialX = { 35f, 75f, 110f };
        public const int Trials = 3, BellCount = 5, DummyCount = 10, StonesToReturn = 3;

        const string BellPath = "Cloudborne/Grove/Bell/Bell", DummyPath = "Cloudborne/Grove/Dummy/Dummy", BabaPath = "Cloudborne/Baba/Baba";
        // the bells: where they stand (two up on ledges), the order Baba rings them in
        static readonly float[] BellXs = { 26f, 30.5f, 35f, 39.5f, 44f }, BellTops = { 0f, 1.8f, 0f, 2.6f, 0f };
        static readonly int[] BellOrder = { 2, 0, 3, 1, 4 };
        // the bell model's swinging part hangs a little left of the frame's middle; the bell's middle there
        const float BellOffX = -0.3f, BellLow = 0.55f, BellHigh = 1.95f, BellZ = 0.45f, DemoStep = 0.8f;
        static readonly float[] DummyXs = { 66f, 67.6f, 69.2f, 73.4f, 74.9f, 76.4f, 77.9f, 81.6f, 83.1f, 84.6f };
        const float DummyZ = 0.4f, DummyKi = 6f;
        /// <summary>Where Baba stands to throw his stones.</summary>
        public const float BabaStonesX = 118.5f;
        const float StoneEvery = 2.4f, StoneSpeed = 9f, StoneDamage = 5f;

        class Bell { public float x, top, sway, swayV, glow; public bool lit; public Transform t, swing; public Material mat; }
        class Dummy { public float x, wob, wobV, cool; public bool broken; public Transform t; }

        readonly List<Bell> bells = new List<Bell>();
        readonly List<Dummy> dummies = new List<Dummy>();
        int trial = -1, bellNext, demoI, returned;
        float demoT, stoneT, babaT, babaAct;
        bool demoing;
        string babaClip = "Idle_02";
        Transform baba;
        CbModel babaModel;

        /// <summary>Test hooks: which trial is on (-1 none yet), bells rung right so far, dummies broken, stones sent back.</summary>
        public int Trial => trial;
        public int BellsRung => bellNext;
        public bool BellsDemoing => demoing;
        public int DummiesBroken => dummies.FindAll(d => d.broken).Count;
        public int StonesReturned => returned;
        public float BellX(int i) => BellXs[i] + BellOffX;
        public float BellTop(int i) => BellTops[i];
        public int BellInOrder(int k) => BellOrder[k];
        public float DummyX(int i) => DummyXs[i];
        public bool DummyBroken(int i) => dummies[i].broken;

        void BuildGrove()
        {
            AddLantern(4f);
            AddLantern(56f);
            AddLantern(94f);
            AddEndGate(CbWorld.LevelMin - HalfWidth - 0.15f - EndGateHalf, 1);
            AddEndGate(CbWorld.LevelMax + HalfWidth + 0.15f + EndGateHalf, -1);
            for (int i = 0; i < BellCount; i++)
            {
                if (BellTops[i] > 0) AddLedge(BellXs[i] + BellOffX, 2.2f, BellTops[i], false);
                var t = CbArt.Joint(root, BellXs[i], BellTops[i], BellZ, "bell");
                var model = CbArt.Prop(BellPath, t, CbArt.PropOutline);
                var b = new Bell { x = BellXs[i], top = BellTops[i], t = t };
                if (model != null)
                {
                    foreach (var c in model.GetComponentsInChildren<Transform>()) if (c.name == "Swing") b.swing = c;
                    b.mat = model.GetComponentInChildren<Renderer>().sharedMaterial;
                }
                else
                {
                    // stand-in: a bamboo post and arm, a bronze bell
                    var bamboo = M("#c9a45a");
                    CbArt.Cylinder(bamboo, t, new Vector3(0.6f, 1.1f, 0), 0.06f, 2.2f);
                    CbArt.Cylinder(bamboo, t, new Vector3(0, 2.15f, 0), 0.06f, 1.5f, Quaternion.Euler(0, 0, 90));
                    b.swing = CbArt.Joint(t, BellOffX, 2.1f, 0, "Swing");
                    b.mat = CbArt.NewToon(CbArt.Hex("#9a7a3a"), CbArt.PropOutline);
                    CbArt.Cone(b.mat, b.swing, new Vector3(0, -0.85f, 0), 0.38f, 0.75f, null, 12);
                }
                bells.Add(b);
            }
            for (int i = 0; i < DummyCount; i++)
            {
                var t = CbArt.Joint(root, DummyXs[i], 0, DummyZ, "dummy");
                t.localRotation = Quaternion.Euler(0, 180f + (i % 3 - 1) * 12f, 0);
                if (CbArt.Prop(DummyPath, t, CbArt.PropOutline) == null)
                {
                    var bamboo = M("#9fae4a");
                    CbArt.Cylinder(bamboo, t, new Vector3(0, 0.7f, 0), 0.14f, 1.3f);
                    CbArt.Cylinder(bamboo, t, new Vector3(0, 1.05f, 0), 0.07f, 0.9f, Quaternion.Euler(0, 0, 90));
                    CbArt.Sphere(M("#eee8d8"), t, new Vector3(0, 1.45f, 0), 0.17f);
                }
                dummies.Add(new Dummy { x = DummyXs[i], t = t });
            }
            // Baba: dozing by his hut at first, then at each trial in turn
            baba = CbArt.Joint(root, 13f, 0, 0.6f, "baba");
            babaModel = CbModel.Load(BabaPath, 2.0f, baba);
            if (babaModel == null)
            {
                var fur = M("#c8642a");
                CbArt.Part(CbMesh.Sphere, fur, baba, new Vector3(0, 0.8f, 0), new Vector3(0.9f, 1.2f, 0.7f));
                CbArt.Sphere(fur, baba, new Vector3(0, 1.6f, 0), 0.3f);
                CbArt.Part(CbMesh.Sphere, M("#f0eee8"), baba, new Vector3(0, 1.38f, -0.18f), new Vector3(0.3f, 0.4f, 0.2f));
            }
            SetBaba(13f, 1, "Dozing_Elderly");
        }

        /// <summary>Baba stands at x, facing the way facing says (turned a little to the camera), playing clip.</summary>
        void SetBaba(float x, int facing, string clip)
        {
            baba.localPosition = new Vector3(x, 0, baba.localPosition.z);
            baba.localRotation = Quaternion.Euler(0, facing * 115f, 0);
            BabaDoes(clip, 0);
        }

        /// <summary>Baba plays clip (for seconds, then back to his idle; 0: until told otherwise).</summary>
        void BabaDoes(string clip, float seconds)
        {
            babaClip = clip;
            babaT = 0;
            babaAct = seconds;
        }

        /// <summary>A trial begins (CbGame): Baba comes to it, and it starts from the beginning.</summary>
        public void StartTrial(int i)
        {
            trial = i;
            switch (i)
            {
                case 0:
                    foreach (var b in bells) { b.lit = false; b.glow = 0; }
                    bellNext = 0;
                    SetBaba(TrialX[0] + 12f, -1, "Call_Gesture");
                    StartDemo(0.8f);
                    g.hud.SetTip("Watch Baba ring the bells - then ring them back in the same order");
                    break;
                case 1:
                    SetBaba(TrialX[1] + 13f, -1, "Idle_02");
                    g.hud.SetTip("Blows only rock the dummies, but fill your ki. Then the staff storm (<color=#ffb03a>SPECIAL</color>) breaks them!");
                    break;
                case 2:
                    returned = 0;
                    stoneT = 2.2f;
                    SetBaba(BabaStonesX, -1, "Relax_arms_then_strike_battle_pose");
                    // Baba's lesson: the perfect guard, from now on
                    CbSave.PerfectGuard = true;
                    g.hud.Banner("PERFECT GUARD", true);
                    g.hud.SetTip("<color=#ffb03a>Swipe ↔</color> INTO a stone just as it reaches you: the perfect guard sends it back (3)");
                    break;
            }
        }

        /// <summary>The trial is done (CbGame opens the way on).</summary>
        public bool TrialDone(int i) => i == 0 ? bellNext >= BellCount : i == 1 ? DummiesBroken >= DummyCount : returned >= StonesToReturn;

        void StartDemo(float wait)
        {
            demoing = true;
            demoI = 0;
            demoT = -wait;
        }

        void RingBell(Bell b, int dir, bool byBaba)
        {
            int i = bells.IndexOf(b);
            b.swayV += dir * 160f;
            b.glow = 1f;
            CbFx.Sfx("bell" + i, 0.9f);
            CbFx.Spark(new Vector3(b.x + BellOffX, b.top + 1.2f, BellZ - 0.6f), new Color(1f, 0.85f, 0.4f) * 2f, 8, 3f, 0.25f, 0.4f, 0);
            if (byBaba || trial != 0 || demoing || bellNext >= BellCount) return;
            if (i == BellOrder[bellNext])
            {
                b.lit = true;
                bellNext++;
                g.hud.Popup(new Vector3(b.x + BellOffX, b.top + 2.6f, 0), bellNext + "/" + BellCount, CbHud.PopStyle.Big);
                if (bellNext >= BellCount) { CbFx.Sfx("perfect"); BabaDoes("Call_Gesture", 1.5f); }
            }
            else
            {
                // wrong: the bells go quiet, Baba wags his finger and rings them again
                foreach (var o in bells) o.lit = false;
                bellNext = 0;
                CbFx.Sfx("block", 0.8f);
                g.hud.Popup(new Vector3(b.x + BellOffX, b.top + 2.6f, 0), "WRONG BELL!", CbHud.PopStyle.Hurt);
                g.hud.SetTip("Not that one! Watch again...");
                BabaDoes("Finger_Wag_No", 1.6f);
                StartDemo(1.6f);
            }
        }

        bool StrikeGrove(float x, float y, int dir, float range, bool staff)
        {
            bool any = false;
            foreach (var b in bells)
            {
                float bx = b.x + BellOffX;
                if (!InReach(bx, x, dir, range) || y + 2f < b.top + BellLow || y > b.top + BellHigh) continue;
                any = true;
                if (b.glow > 0.7f) continue;
                RingBell(b, dir, false);
            }
            foreach (var d in dummies)
            {
                if (d.broken || !InReach(d.x, x, dir, range) || y > 1.2f) continue;
                any = true;
                // the staff storm breaks them; any other blow rocks them, and fills his ki
                if (g.hero.Storming) { BreakDummy(d, dir); continue; }
                if (d.cool > 0) continue;
                d.cool = 0.3f;
                d.wobV += dir * 220f;
                g.hero.ki = Mathf.Min(100, g.hero.ki + DummyKi);
                CbFx.Spark(new Vector3(d.x, 1f, DummyZ - 0.5f), new Color(0.75f, 0.85f, 0.4f) * 1.5f, 6, 3f, 0.25f, 0.3f, 4);
                CbFx.Sfx("hit", 0.5f);
                g.hud.Popup(new Vector3(d.x, 2.2f, 0), "KI +" + (int)DummyKi, CbHud.PopStyle.Burn);
            }
            return any;
        }

        void BreakDummy(Dummy d, int dir)
        {
            d.broken = true;
            d.t.gameObject.SetActive(false);
            for (int k = 0; k < 8; k++)
            {
                var bar = CbArt.Cylinder(M(k < 6 ? "#9fae4a" : "#c9a45a"), g.transform, new Vector3(d.x + Random.Range(-0.3f, 0.3f), Random.Range(0.3f, 1.4f), DummyZ - 0.3f),
                    0.06f, Random.Range(0.4f, 0.9f), Random.rotation);
                g.AddDebris(bar, new Vector3(dir * Random.Range(1.5f, 5f), Random.Range(3f, 6f), Random.Range(-1f, 1f)), Random.Range(-500f, 500f));
            }
            CbFx.Puff(new Vector3(d.x, 0.9f, DummyZ - 0.3f), new Color(0.85f, 0.8f, 0.6f), 8, 1.8f, 0.8f, 0.5f);
            CbFx.Sfx("hit", 0.8f);
            int n = DummiesBroken;
            g.hud.Popup(new Vector3(d.x, 2.4f, 0), n + "/" + DummyCount, CbHud.PopStyle.Big);
        }

        /// <summary>One of Baba's stones, thrown at Raja.</summary>
        void ThrowStone()
        {
            var t = CbArt.Joint(root, BabaStonesX - 0.8f, 1.3f, 0, "stone");
            CbArt.Part(CbMesh.Sphere, M("#8d8a82"), t, Vector3.zero, new Vector3(0.28f, 0.24f, 0.26f));
            darts.Add(new Dart { x = BabaStonesX - 0.8f, y = 1.3f, vx = -StoneSpeed, dmg = StoneDamage, t = t, stone = true });
            CbFx.Sfx("swing", 0.6f);
        }

        /// <summary>A stone sent back by the perfect guard reaches Baba: he swats it away, pleased.</summary>
        void StoneBack()
        {
            returned++;
            BabaDoes("Kung_Fu_Punch", 0.7f);
            CbFx.Sfx("block", 0.7f);
            CbFx.Spark(new Vector3(BabaStonesX - 0.5f, 1.4f, -0.4f), CbFx.Grey, 10, 5f, 0.3f, 0.3f, 6);
            g.hud.Popup(new Vector3(BabaStonesX, 2.8f, 0), returned + "/" + StonesToReturn, CbHud.PopStyle.Big);
            if (returned >= StonesToReturn) g.hud.SetTip("Good! The guard that gives the blow back. Fire held by the heart is like that");
        }

        void UpdateGrove(float dt, CbHero hero)
        {
            // the bells swing and settle, their glow fading (a bell rung right keeps a soft one)
            foreach (var b in bells)
            {
                b.swayV += (-b.sway * 60f - b.swayV * 3f) * dt;
                b.sway += b.swayV * dt;
                if (b.swing != null) b.swing.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(b.sway, -35f, 35f));
                b.glow = Mathf.Max(b.lit ? 0.35f : 0f, b.glow - dt * 1.5f);
                if (b.mat != null) CbArt.SetGlow(b.mat, new Color(1f, 0.75f, 0.3f) * b.glow * 0.8f);
            }
            // Baba rings the order
            if (demoing && trial == 0)
            {
                demoT += dt;
                if (demoT >= 0)
                {
                    if (demoI < BellCount && demoT >= demoI * DemoStep)
                    {
                        var b = bells[BellOrder[demoI]];
                        RingBell(b, -1, true);
                        g.hud.Popup(new Vector3(b.x + BellOffX, b.top + 2.6f, 0), (demoI + 1).ToString(), CbHud.PopStyle.Normal);
                        demoI++;
                    }
                    if (demoI >= BellCount && demoT > BellCount * DemoStep) { demoing = false; g.hud.SetTip("Now you: ring the bells in the same order"); }
                }
            }
            foreach (var d in dummies)
            {
                d.cool = Mathf.Max(0, d.cool - dt);
                d.wobV += (-d.wob * 90f - d.wobV * 5f) * dt;
                d.wob += d.wobV * dt;
                if (!d.broken) d.t.localRotation = Quaternion.Euler(0, d.t.localEulerAngles.y, Mathf.Clamp(d.wob, -25f, 25f));
            }
            // Baba throws his stones while the trial lasts
            if (trial == 2 && returned < StonesToReturn && g.mode == CbGame.Mode.Play)
            {
                stoneT -= dt;
                if (stoneT <= 0.35f && babaClip != "Kung_Fu_Punch") BabaDoes("Kung_Fu_Punch", 0.7f);
                if (stoneT <= 0) { stoneT = StoneEvery; ThrowStone(); }
            }
            // Baba himself
            babaT += dt;
            if (babaAct > 0 && babaT > babaAct) BabaDoes(trial < 0 ? "Dozing_Elderly" : "Idle_02", 0);
            if (babaModel != null)
            {
                if (babaAct > 0) babaModel.Window(babaClip, 0f, 1f, babaT / babaAct);
                else babaModel.LoopSeconds(babaClip, babaT);
            }
        }

        void ResetGrove()
        {
            trial = -1;
            bellNext = 0;
            demoing = false;
            returned = 0;
            foreach (var b in bells) { b.lit = false; b.glow = b.sway = b.swayV = 0; }
            foreach (var d in dummies) { d.broken = false; d.wob = d.wobV = 0; d.t.gameObject.SetActive(true); }
            if (baba != null) SetBaba(13f, 1, "Dozing_Elderly");
        }

        /// <summary>A trial left part-way (Raja fell): it starts over when he comes back to it.</summary>
        public void LeaveTrial()
        {
            demoing = false;
            if (trial == 0) { bellNext = 0; foreach (var b in bells) b.lit = false; }
            trial = -1;
        }
    }
}
