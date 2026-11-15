using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The saw barge, stage 1-4's mini-boss (the story doc: a floating logging machine; it chases from behind and throws
    /// logs; knock down its three fuel barrels - with fireballs in the doc, but Raja's fire wakes only in 1-5, so here:
    /// logs sent back with the perfect guard, the staff throw, or a blow when it has rammed and its saws are stuck). Built
    /// in Meshy from the user's drawing, driven here: in the calm basin at the river's end it comes up from behind,
    /// throws logs at him from its crane (they fly at chest height: jump them, duck them, or swipe into them to send
    /// them back at its barrels), then winds up its saws (a red glow) and rams (jump over the saws); rammed, its saws
    /// stick a moment - its barrels within reach. Three barrels gone and it burns and sinks.
    /// </summary>
    public partial class CbLevel
    {
        enum Barge { Away, Enter, Throw, Wind, Ram, Stuck, Back, Sinking, Sunk }

        const float BargeHome = BasinX - 12.5f, BargeFrom = BasinX - 30f, BargeFront = 3.1f, BargeZ = 0.9f;
        const float BargeThrowEvery = 1.7f, BargeWind = 1.0f, BargeRamSpeed = 12f, BargeStuck = 2.6f, BargeBackSpeed = 4f;
        const float LogSpeed = 10f, LogDamage = 12f, BargeSawDamage = 16f, SawHigh = 1.35f;
        // its three fuel barrels on the deck, toward the camera (the ones to knock down): x from its middle, height over the water
        static readonly float[] FuelOff = { -0.3f, 0.35f, 1.0f };
        const float FuelY = 1.35f, FuelZ = -0.7f;

        Barge barge = Barge.Away;
        float bargeX = BargeFrom, bargeT, bargeSink, bargeRamTo, bargeGlow;
        int bargeThrows, fuelLeft = 3;
        bool sawSinking;
        Transform sawBarge, riverCage;
        readonly Transform[] fuel = new Transform[3];
        Material bargeMat;
        Vector3 cageHome;

        /// <summary>Test hooks: the saw barge's fight.</summary>
        public bool SawBargeFighting => barge != Barge.Away && barge != Barge.Sinking && barge != Barge.Sunk;
        public bool SawBargeBeaten => barge == Barge.Sinking || barge == Barge.Sunk;
        public int FuelLeft => fuelLeft;
        public bool SawBargeStuck => barge == Barge.Stuck;
        public float SawBargeFront => bargeX + BargeFront;
        public float FuelX(int i) => bargeX + FuelOff[i];
        public Transform RiverCage => riverCage;

        void BuildSawBarge()
        {
            sawBarge = CbArt.Joint(root, bargeX, SurfaceAt(BasinX), BargeZ, "sawBarge");
            var model = CbArt.Prop(BargePath, sawBarge, CbArt.PropOutline);
            if (model != null)
            {
                model.localRotation = Quaternion.Euler(0, SawBargeYaw, 0);
                bargeMat = model.GetComponentInChildren<Renderer>().sharedMaterial;
            }
            else
            {
                bargeMat = CbArt.NewToon(CbArt.Hex("#8a5232"), CbArt.PropOutline);
                CbArt.Box(bargeMat, sawBarge, new Vector3(0, 0.7f, 0), new Vector3(6f, 1.4f, 2f));
                foreach (var dy in new[] { 0.4f, 1.1f })
                    CbArt.Part(CbMesh.Cylinder, CbArt.Toon("#b8c0c4", CbArt.PropOutline), sawBarge, new Vector3(BargeFront, dy, -0.3f), new Vector3(1.1f, 0.03f, 1.1f), Quaternion.Euler(90, 0, 0));
            }
            for (int i = 0; i < 3; i++)
            {
                fuel[i] = CbArt.Joint(sawBarge, FuelOff[i], FuelY, FuelZ, "fuelBarrel");
                var b = CbArt.Prop(BarrelPath, fuel[i], CbArt.PropOutline);
                if (b != null)
                {
                    b.localScale = Vector3.one * 0.7f;
                    // red: the fuel, the ones to knock down
                    b.GetComponentInChildren<Renderer>().sharedMaterial.color = new Color(1f, 0.42f, 0.36f);
                }
                else CbArt.Cylinder(CbArt.Toon("#c23a2a", CbArt.PropOutline), fuel[i], new Vector3(0, 0.35f, 0), 0.26f, 0.7f);
            }
            sawBarge.gameObject.SetActive(false);
            // Koda's cage, in his barge (split out of the model: lifted away at the end)
            foreach (var c in kodaBarge.GetComponentsInChildren<Transform>()) if (c.name == "Cage") riverCage = c;
            if (riverCage != null) cageHome = riverCage.localPosition;
        }

        /// <summary>The fight begins (CbGame): the saw barge comes up the basin from behind.</summary>
        public void StartSawBarge()
        {
            barge = Barge.Enter;
            bargeX = BargeFrom;
            fuelLeft = 3;
            for (int i = 0; i < 3; i++) fuel[i].gameObject.SetActive(true);
            sawBarge.gameObject.SetActive(true);
            CbFx.Sfx("saw", 0.8f);
        }

        void SetBarge(Barge b) { barge = b; bargeT = 0; }

        /// <summary>A log thrown from the crane, flying at chest height across the basin (a dart: see CbLevelWays).</summary>
        void ThrowLog(CbHero hero)
        {
            float x = bargeX + BargeFront - 0.4f, y = RiverFloor(BasinX) + 1.3f;
            var t = CbArt.Joint(root, x, y, 0, "thrownLog");
            CbArt.Cylinder(M("#6b4a2a"), t, Vector3.zero, 0.2f, 1.3f, Quaternion.Euler(0, 0, 90));
            darts.Add(new Dart { x = x, y = y, vx = LogSpeed, dmg = LogDamage, t = t, log = true });
            CbFx.Sfx("swing", 0.8f);
        }

        /// <summary>A fuel barrel knocked down: it bursts in fire and black smoke.</summary>
        void FuelBoom(int i)
        {
            if (!fuel[i].gameObject.activeSelf) return;
            fuel[i].gameObject.SetActive(false);
            fuelLeft--;
            var at = fuel[i].position + new Vector3(0, 0.3f, -0.2f);
            CbFx.Spark(at, CbFx.Fire, 26, 7f, 0.5f, 0.7f, -3);
            CbVfx.Ring(at, CbFx.Fire * 0.5f, 2.6f, 0.4f);
            CbVfx.Burst(at, CbFx.Fire, 2.8f, 0.2f);
            CbFx.Puff(at + Vector3.up * 0.5f, new Color(0.15f, 0.13f, 0.12f), 12, 2f, 1.2f, 1f);
            CbFx.Sfx("heavy");
            CbFx.Sfx("fire", 0.8f);
            g.shake = Mathf.Max(g.shake, 0.5f);
            g.hitstop = Mathf.Max(g.hitstop, 0.06f);
            g.hud.ImpactFlash(0.4f);
            g.score += 1000;
            g.hud.Popup(at + Vector3.up * 1.2f, fuelLeft > 0 ? $"FUEL {3 - fuelLeft}/3" : "IT'S GOING DOWN!", CbHud.PopStyle.Big);
            if (fuelLeft <= 0) { SetBarge(Barge.Sinking); sawSinking = true; CbFx.Sfx("roar", 0.6f); }
        }

        /// <summary>The nearest barrel still on the deck to x (a log sent back hits the first in its way).</summary>
        int NearestFuel(float x)
        {
            int best = -1;
            for (int i = 0; i < 3; i++)
                if (fuel[i].gameObject.activeSelf && (best < 0 || Mathf.Abs(FuelX(i) - x) < Mathf.Abs(FuelX(best) - x))) best = i;
            return best;
        }

        /// <summary>A log sent back by the perfect guard reaches the barge: it bursts a barrel.</summary>
        void LogBack()
        {
            int i = NearestFuel(SawBargeFront);
            if (i >= 0) FuelBoom(i);
        }

        bool StrikeSawBarge(float x, float y, int dir, float range)
        {
            if (!SawBargeFighting) return false;
            bool any = false;
            float floor = RiverFloor(BasinX);
            for (int i = 0; i < 3; i++)
            {
                if (!fuel[i].gameObject.activeSelf || !InReach(FuelX(i), x, dir, range)) continue;
                if (y + 2.2f < floor + FuelY || y > floor + FuelY + 1.2f) continue;
                FuelBoom(i);
                any = true;
            }
            return any;
        }

        void UpdateSawBarge(float dt, CbHero hero)
        {
            if (barge == Barge.Away || barge == Barge.Sunk) return;
            bargeT += dt;
            float floor = RiverFloor(BasinX), time = g.clock;
            bargeGlow = Mathf.MoveTowards(bargeGlow, 0, dt * 2f);
            switch (barge)
            {
                case Barge.Enter:
                    bargeX = Mathf.MoveTowards(bargeX, BargeHome, dt * 7f);
                    if (bargeX >= BargeHome) { SetBarge(Barge.Throw); bargeThrows = 0; }
                    break;
                case Barge.Throw:
                    bargeX = Mathf.MoveTowards(bargeX, BargeHome, dt * BargeBackSpeed);
                    if (bargeT >= BargeThrowEvery * (bargeThrows + 1) - 0.8f && bargeThrows < 2 && bargeT < BargeThrowEvery * (bargeThrows + 1))
                        bargeGlow = Mathf.Max(bargeGlow, 0.4f);
                    if (bargeThrows < 2 && bargeT >= BargeThrowEvery * (bargeThrows + 1)) { bargeThrows++; ThrowLog(hero); }
                    if (bargeThrows >= 2 && bargeT >= BargeThrowEvery * 3) { SetBarge(Barge.Wind); CbFx.Sfx("saw", 1f); }
                    break;
                case Barge.Wind:
                    // its saws spin up, the whole barge glowing red: then it rams
                    bargeGlow = 0.35f + 0.45f * (bargeT / BargeWind) * (0.5f + 0.5f * Mathf.Sin(time * 30f));
                    if (bargeT >= BargeWind)
                    {
                        SetBarge(Barge.Ram);
                        bargeRamTo = Mathf.Min(hero.x + 1.2f, BasinX + 9f) - BargeFront;
                    }
                    break;
                case Barge.Ram:
                {
                    bargeX = Mathf.MoveTowards(bargeX, bargeRamTo, dt * BargeRamSpeed);
                    float saw = SawBargeFront;
                    if (Mathf.Abs(hero.x - saw) < 0.8f && hero.y < floor + SawHigh) hero.TakeHit(BargeSawDamage, 1, knock: 8f);
                    if (Random.value < 0.6f) CbFx.Spark(new Vector3(saw, floor + 0.3f, -0.5f), CbFx.Water, 3, 4f, 0.3f, 0.4f, 8, 3f);
                    if (bargeX >= bargeRamTo - 0.01f)
                    {
                        SetBarge(Barge.Stuck);
                        g.shake = Mathf.Max(g.shake, 0.4f);
                        CbFx.Sfx("block", 0.9f);
                        CbFx.Spark(new Vector3(saw, floor + 0.6f, -0.5f), CbFx.Grey, 14, 6f, 0.3f, 0.4f, 6);
                        g.hud.Popup(new Vector3(saw - 1.5f, floor + 3.6f, 0), "ITS SAWS ARE STUCK!", CbHud.PopStyle.Big);
                    }
                    break;
                }
                case Barge.Stuck:
                    if (Random.value < 0.25f) CbFx.Spark(new Vector3(SawBargeFront, floor + 0.7f, -0.5f), CbFx.Grey, 2, 3f, 0.2f, 0.3f, 6);
                    if (bargeT >= BargeStuck) SetBarge(Barge.Back);
                    break;
                case Barge.Back:
                    bargeX = Mathf.MoveTowards(bargeX, BargeHome, dt * BargeBackSpeed);
                    if (bargeX <= BargeHome + 0.01f) { SetBarge(Barge.Throw); bargeThrows = 0; }
                    break;
                case Barge.Sinking:
                    // burning, listing, going down
                    bargeSink = Mathf.MoveTowards(bargeSink, 3f, dt * 1.1f);
                    if (Random.value < 0.5f) CbFx.Spark(sawBarge.position + new Vector3(Random.Range(-2.5f, 2.5f), 1.5f - bargeSink, -0.8f), CbFx.Fire, 2, 2f, 0.5f, 0.6f, -4);
                    if (Random.value < 0.2f) CbFx.Puff(sawBarge.position + new Vector3(Random.Range(-2f, 2f), 2.5f - bargeSink, -0.5f), new Color(0.15f, 0.13f, 0.12f), 2, 1.5f, 1.4f, 1.2f);
                    if (bargeSink >= 3f) { barge = Barge.Sunk; sawBarge.gameObject.SetActive(false); }
                    break;
            }
            float bob = Mathf.Sin(time * 2.2f) * 0.06f;
            sawBarge.localPosition = new Vector3(bargeX, SurfaceAt(BasinX) - 0.25f + bob - bargeSink, BargeZ);
            float list = barge == Barge.Sinking ? bargeSink * 9f : 0f, judder = barge == Barge.Stuck ? Mathf.Sin(time * 45f) * 1.2f : 0f;
            sawBarge.localRotation = Quaternion.Euler(Mathf.Sin(time * 1.6f) * 1.5f + list, 0, judder);
            if (bargeMat != null) CbArt.SetGlow(bargeMat, new Color(1f, 0.25f, 0.15f) * bargeGlow);
            // the chimney's smoke
            if (barge != Barge.Sinking && Random.value < 0.15f)
                CbFx.Puff(sawBarge.position + new Vector3(-1.6f, 3.4f, 0.2f), new Color(0.25f, 0.24f, 0.24f), 1, 1f, 1.1f, 1.4f);
        }

        void ResetSawBarge()
        {
            barge = Barge.Away;
            bargeX = BargeFrom;
            bargeSink = 0;
            bargeGlow = 0;
            fuelLeft = 3;
            sawSinking = false;
            if (sawBarge != null)
            {
                sawBarge.gameObject.SetActive(false);
                for (int i = 0; i < 3; i++) fuel[i].gameObject.SetActive(true);
            }
            if (riverCage != null) { riverCage.localPosition = cageHome; riverCage.gameObject.SetActive(true); }
        }

        /// <summary>A fight left part-way (Raja fell): the barge goes back down the river, ready to come again.</summary>
        public void LeaveSawBarge() { if (SawBargeFighting) ResetSawBarge(); }

        /// <summary>The end (CbStory.RiverEnd): Koda's barge strikes the rocks and breaks up, his cage left on what is left of it.</summary>
        public void KodaBargeBreaks()
        {
            var at = kodaBarge.position;
            CbFx.Sfx("heavy");
            CbFx.Puff(at + new Vector3(0, 0.8f, -1f), new Color(0.6f, 0.5f, 0.4f), 14, 2.5f, 1.2f, 0.8f);
            CbFx.Spark(at + new Vector3(0, 0.5f, -1f), CbFx.Water, 24, 6f, 0.4f, 0.7f, 10);
            for (int k = 0; k < 10; k++)
            {
                var plank = CbArt.Box(M("#6b4a2a"), g.transform, at + new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(0.2f, 1f), -1f), new Vector3(Random.Range(0.6f, 1.4f), 0.12f, 0.3f), Random.rotation);
                g.AddDebris(plank, new Vector3(Random.Range(-4f, 4f), Random.Range(3f, 7f), Random.Range(-1f, 1f)), Random.Range(-400f, 400f));
            }
            g.shake = Mathf.Max(g.shake, 0.4f);
        }

        /// <summary>The airship's rope takes Koda's cage up into the sky (k: 0 on the barge, 1 gone up out of sight).</summary>
        public void LiftKodaCage(float k)
        {
            if (riverCage == null) return;
            // (straight up: turning it from nothing would undo the way it was imported)
            riverCage.localPosition = cageHome + riverCage.parent.InverseTransformVector(Vector3.up * (k * k * 14f));
        }

        public float KodaBargeX => kodaBarge.position.x;
    }
}
