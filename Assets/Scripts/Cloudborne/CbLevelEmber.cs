using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Stage 1-5, The Ember Temple (the story doc: a boss stage; Grak tears the fire crystal out and the jungle goes up in
    /// flames; the edges of the arena burn and close in; Raja grabs the crystal and his stripes light up). A short way in
    /// past the last of the raiders, then the temple's courtyard: a stone floor, the empty altar at the back (the crystal's
    /// stone paws, the crystal gone), a shrine pillar at either end of the arena (Grak's charges end against them), embers
    /// rising all the time.
    /// <list type="bullet">
    /// <item>The fire (Grak's second phase): it catches at both ends of the arena and creeps in. Touching it burns. A blow
    /// on it (any blow, the staff's wind) beats it back a little, and its heat fills Raja's ki (the story doc: his fire
    /// draws it in) - his fire is waking.</item>
    /// <item>The crystal: Grak fixes it to his armour for his third phase and shoots fire with it; knocked out of it, it
    /// rolls across the stones to Raja (CbEnemy, CbGame.FireAwakens).</item>
    /// </list>
    /// </summary>
    public partial class CbLevel
    {
        // (longer: the way in was 60 m of plain path, the burning jungle and its fire run before the temple now)
        public const float EmberMax = 122f, EmberGoal = 119f;
        /// <summary>The middle of Grak's arena, and how far from it his charges end (against the pillars).</summary>
        public const float GrakX = 104f, GrakCrash = 9.4f;
        static readonly (float x0, float x1)[] EmberWaters = new (float, float)[0];
        const string CrystalPath = "Cloudborne/Ember/FireCrystal/FireCrystal";
        // the fire: where each side starts, how far in it can creep, how fast, how far a blow beats it back, what it gives
        public const float FireFrom = 9.2f, FireClosest = 4.2f;
        const float FireCreep = 0.42f, FireBeat = 1.3f, FireKi = 7f, FireDamage = 7f, FireTall = 1.6f;

        Transform emberAltar, altarCrystal, looseCrystal;
        readonly List<Transform> emberPillars = new List<Transform>();
        readonly List<Vector3> braziers = new List<Vector3>();
        readonly List<Transform> fireTongues = new List<Transform>();
        Transform fireLeft, fireRight;
        float fireL, fireR, emberT, pillarShake, pillarShakeX;
        bool fireOn;
        // the crystal knocked out of Grak's armour, rolling across the stones to Raja
        Vector3 rollFrom, rollTo;
        float rollT = -1f;

        /// <summary>The fire around the arena is burning (Grak's second phase on), and how far in it has come each side.</summary>
        public bool FireOn => fireOn;
        public float FireLeftX => GrakX - fireL;
        public float FireRightX => GrakX + fireR;
        /// <summary>The crystal is rolling to Raja (it reaches him at the end of CrystalRoll seconds).</summary>
        public bool CrystalRolling => rollT >= 0;
        public const float CrystalRoll = 1.3f;

        void BuildEmber()
        {
            // (no gate behind him at the start: an invisible wall just behind where he starts - the user; CbWorld.StartWall)
            AddEndGate(CbWorld.LevelMax + HalfWidth + 0.15f + EndGateHalf, -1);
            // the courtyard's floor: worn sandstone slabs over the path, from just before the arena to its end
            var slab = CbArt.NewToon(Color.white, 0f, SlabTexture());
            slab.SetFloat("_SpecStrength", 0f);
            float from = GrakX - 13f, to = EmberMax + 6f;
            slab.mainTextureScale = new Vector2((to - from) / 2.4f, 1.4f);
            CbArt.Part(CbMesh.Quad, slab, root, new Vector3((from + to) / 2, 0.015f, 0.4f), new Vector3(to - from, 3.6f, 1), CbArt.Rot(Mathf.PI / 2), shadows: false).name = "slabs";
            // a pillar at either end of the arena: where Grak's charges end (just outside where Raja can go)
            foreach (int side in new[] { -1, 1 })
            {
                var t = CbArt.Joint(root, GrakX + side * (GrakCrash + CbEnemy.GrakHalf + 0.6f), 0, 0.75f, "emberPillar");
                var holder = CbArt.Joint(t, 0, 0, 0, "shape");
                holder.localScale = new Vector3(1.1f / 1.48f, 4.6f / 3.6f, 0.8f);
                if (CbArt.Prop("Cloudborne/Jungle/Pillar/Pillar", holder, CbArt.PropOutline) is Transform pm)
                {
                    // the jungle shrine's mossy stone, scorched (it read bright yellow-green in the fire's light)
                    foreach (var r in pm.GetComponentsInChildren<Renderer>())
                        r.material.color = new Color(0.62f, 0.42f, 0.34f);
                }
                else CbArt.Box(M("#a08a6a"), holder, new Vector3(0, 1.8f, 0), new Vector3(1.48f, 3.6f, 1.5f));
                emberPillars.Add(t);
            }
            // the altar at the back of the courtyard: a stepped block of stone, the crystal's stone paws on top, empty
            emberAltar = CbArt.Joint(root, GrakX, 0, 2.6f, "altar");
            // (of the courtyard's slabs: plain boxes read as cardboard)
            var stone = CbArt.NewToon(new Color(0.85f, 0.75f, 0.68f), CbArt.PropOutline, SlabTexture());
            stone.mainTextureScale = new Vector2(1.2f, 0.4f);
            var dark = CbArt.NewToon(new Color(0.7f, 0.6f, 0.55f), CbArt.PropOutline, SlabTexture());
            dark.mainTextureScale = new Vector2(0.9f, 0.4f);
            CbArt.Box(stone, emberAltar, new Vector3(0, 0.25f, 0), new Vector3(2.6f, 0.5f, 1.8f));
            CbArt.Box(dark, emberAltar, new Vector3(0, 0.75f, 0), new Vector3(1.8f, 0.5f, 1.3f));
            // a fire in a bowl at either side of it
            foreach (int side in new[] { -1, 1 })
            {
                var bowl = CbArt.Joint(emberAltar, side * 1.7f, 0, -0.2f, "brazier");
                CbArt.Cylinder(M("#3a2a22"), bowl, new Vector3(0, 0.45f, 0), 0.08f, 0.9f);
                CbArt.Cylinder(M("#5a4030"), bowl, new Vector3(0, 0.95f, 0), 0.3f, 0.16f);
                braziers.Add(bowl.position + new Vector3(0, 1.1f, 0));
            }
            var crystal = CbArt.Joint(emberAltar, 0, 1.0f, 0, "crystalHolder");
            if (CbArt.Prop(CrystalPath, crystal, CbArt.PropOutline) is Transform holderModel)
                foreach (var c in holderModel.GetComponentsInChildren<Transform>(true))
                    if (c.name == "Crystal") altarCrystal = c;
            // (torn out already when the stage begins: the opening's painting)
            if (altarCrystal != null) altarCrystal.gameObject.SetActive(false);
            // the fire: a row of flames at each side, unlit until Grak sets it going
            fireLeft = FireWall("fireLeft");
            fireRight = FireWall("fireRight");
            BuildEmberWay();
            ResetEmber();
        }

        /// <summary>A wall of fire across the path: tongues of flame from the back to the front, of different heights, each
        /// flickering and swaying on its own (plain glowing quads read as yellow pillars: the CI's pictures).</summary>
        Transform FireWall(string name)
        {
            var t = CbArt.Joint(root, GrakX, 0, 0, name);
            // the painted fire the user approved (the flame drawn in code where it isn't in the project)
            var tex = (Texture)CbVfx.Painted("Fire") ?? FlameTexture();
            // (the painted fire has its own colours: only warmed a little)
            bool painted = CbVfx.Painted("Fire") != null;
            var outer = CbArt.Glow(painted ? new Color(1f, 0.85f, 0.75f, 1f) : new Color(1f, 0.42f, 0.1f, 1f), tex);
            var inner = CbArt.Glow(painted ? new Color(1f, 1f, 0.9f, 0.9f) : new Color(1f, 0.8f, 0.35f, 1f), tex);
            for (int k = 0; k < 12; k++)
            {
                bool core = k % 3 == 2;
                float z = -1.5f + k * 0.32f + Random.Range(-0.1f, 0.1f);
                float h = FireTall * (core ? Random.Range(0.55f, 0.75f) : Random.Range(0.75f, 1.15f));
                var tongue = CbArt.Part(CbMesh.Quad, core ? inner : outer, t, new Vector3(Random.Range(-0.35f, 0.35f), h / 2, z - (core ? 0.05f : 0f)),
                    new Vector3(h * 0.95f, h, 1), shadows: false);
                tongue.name = "flame";
                fireTongues.Add(tongue);
                tongueHeights.Add(h);
            }
            t.gameObject.SetActive(false);
            return t;
        }

        readonly List<float> tongueHeights = new List<float>();
        static Texture2D flameTex;

        /// <summary>A tongue of flame drawn in code: wide and bright at its foot, licking up to a point, its edges soft.</summary>
        internal static Texture2D FlameTexture()
        {
            if (flameTex != null) return flameTex;
            const int W = 64, H = 128;
            flameTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "flame" };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H;
                    // the flame's half width at this height: round at the foot, narrowing to a tip, a little wavy
                    float half = 0.95f * Mathf.Pow(1f - v, 0.75f) * Mathf.Clamp01(v * 6f + 0.35f) + Mathf.Sin(v * 9f) * 0.04f * (1 - v);
                    float d = Mathf.Abs(u) / Mathf.Max(half, 0.001f);
                    float a = Mathf.Clamp01((1f - d) * 2.2f) * Mathf.Clamp01((1f - v) * 1.6f);
                    // white-gold in its heart, orange, then red at the edges
                    var c = Color.Lerp(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.55f, 0.15f), Mathf.Clamp01(d * 1.4f + v * 0.5f));
                    c = Color.Lerp(c, new Color(0.85f, 0.18f, 0.05f), Mathf.Clamp01(d * 1.6f - 0.7f));
                    c.a = a;
                    px[y * W + x] = c;
                }
            flameTex.SetPixels(px);
            flameTex.Apply();
            return flameTex;
        }

        /// <summary>A sandstone slab texture drawn in code: big worn slabs, dark joints, a few cracks and scorch marks.</summary>
        static Texture2D slabTex;

        static Texture2D SlabTexture()
        {
            if (slabTex != null) return slabTex;
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "slabs" };
            var px = new Color[S * S];
            Color sand = CbArt.Hex("#b8946a"), dark = CbArt.Hex("#6b5038"), soot = CbArt.Hex("#4a3a2e");
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    // two slabs across, two deep; every other row shifted half a slab
                    int row = y / 64;
                    int sx = (x + row * 32) % 64, sy = y % 64;
                    float n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.5f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.2f;
                    var c = Color.Lerp(sand * 0.85f, sand * 1.08f, n);
                    if (sx < 2 || sy < 2) c = dark;
                    float scorch = Mathf.PerlinNoise(x * 0.04f + 7f, y * 0.04f + 3f);
                    if (scorch > 0.62f) c = Color.Lerp(c, soot, (scorch - 0.62f) * 1.6f);
                    c.a = 1;
                    px[y * S + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return slabTex = tex;
        }

        /// <summary>Grak's charge ends against a pillar: it shakes and dust falls from it.</summary>
        public void PillarHit(float x)
        {
            pillarShake = 0.5f;
            pillarShakeX = x;
            CbFx.Spark(new Vector3(x, 3.5f, 0.3f), new Color(0.72f, 0.62f, 0.5f), 14, 2f, 0.3f, 0.9f, 8);
            CbFx.Puff(new Vector3(x, 1.2f, 0.2f), new Color(0.78f, 0.7f, 0.6f), 8, 2f, 0.9f, 0.6f);
        }

        /// <summary>Grak's second phase: the fire catches at both ends of the arena.</summary>
        public void LightArenaFire()
        {
            if (fireOn) return;
            fireOn = true;
            fireL = fireR = FireFrom;
            fireLeft.gameObject.SetActive(true);
            fireRight.gameObject.SetActive(true);
            foreach (int side in new[] { -1, 1 })
            {
                var at = new Vector3(GrakX + side * FireFrom, 0.6f, -0.3f);
                CbFx.Spark(at, CbFx.Fire, 30, 7f, 0.5f, 0.7f, -3);
                CbVfx.Burst(at, CbFx.Fire, 3f, 0.25f);
            }
            CbFx.Sfx("fire");
            g.shake = Mathf.Max(g.shake, 0.4f);
        }

        /// <summary>Grak's crystal, knocked out of his armour at from, rolls across the stones to Raja.</summary>
        public void RollCrystal(Vector3 from, float toX)
        {
            if (looseCrystal == null)
            {
                looseCrystal = CbArt.Joint(root, 0, 0, 0, "looseCrystal");
                if (CbArt.Prop(CrystalPath, looseCrystal, CbArt.PropOutline) is Transform m)
                {
                    // only the crystal itself, not its stone paws
                    foreach (var c in m.GetComponentsInChildren<Transform>(true))
                        if (c.name != "Crystal" && c != m && c.GetComponent<Renderer>() != null) c.gameObject.SetActive(false);
                    m.localScale = Vector3.one * 0.7f;
                }
                else CbArt.Part(CbMesh.Sphere, CbArt.Glow(new Color(1f, 0.4f, 0.1f), CbArt.Shaft), looseCrystal, new Vector3(0, 0.3f, 0), new Vector3(0.3f, 0.6f, 0.3f));
            }
            looseCrystal.gameObject.SetActive(true);
            rollFrom = from;
            rollTo = new Vector3(toX, 0f, -0.2f);
            rollT = 0;
            CbFx.Sfx("bell4", 0.8f);
        }

        /// <summary>Grak beaten: the crystal back on its altar, flaring up.</summary>
        public void CrystalHome()
        {
            if (altarCrystal == null) return;
            altarCrystal.gameObject.SetActive(true);
            var at = altarCrystal.position;
            CbVfx.Burst(at, CbFx.Fire, 3f, 0.3f);
            CbFx.Spark(at, CbFx.Fire, 30, 6f, 0.5f, 0.7f, -3);
            CbVfx.Ring(at, CbFx.Fire * 0.5f, 2.6f, 0.45f);
        }

        /// <summary>The crystal is in Raja's hands now (the fire awakens): no longer on the ground.</summary>
        public void CrystalTaken()
        {
            rollT = -1f;
            if (looseCrystal != null) looseCrystal.gameObject.SetActive(false);
        }

        /// <summary>A ball of fire from Grak's crystal, flying at chest height along dir (a dart: ducked, jumped, or sent back
        /// by the perfect guard - then it burns him, armour or not).</summary>
        public void ThrowFireball(float x, float y, int dir, float speed, float dmg)
        {
            var t = CbArt.Joint(root, x, y, 0, "fireball");
            CbArt.Part(CbMesh.Sphere, CbArt.Glow(new Color(1f, 0.5f, 0.12f), CbArt.Shaft), t, Vector3.zero, Vector3.one * 0.42f, shadows: false);
            CbArt.Part(CbMesh.Sphere, CbArt.Glow(new Color(1f, 0.9f, 0.5f), CbArt.Shaft), t, Vector3.zero, Vector3.one * 0.24f, shadows: false);
            darts.Add(new Dart { x = x, y = y, vx = dir * speed, dmg = dmg, t = t, fire = true });
            CbFx.Sfx("fire", 0.7f);
        }

        /// <summary>A blow on the fire beats it back (and its heat fills his ki); a blow anywhere else, nothing here.</summary>
        bool StrikeEmber(float x, float y, int dir, float range, bool staff)
        {
            if (!fireOn || y > FireTall + 0.5f) return false;
            bool any = false;
            if (dir < 0 && InReach(FireLeftX, x, dir, range)) { fireL = Mathf.Min(FireFrom, fireL + FireBeat); any = true; Beaten(FireLeftX); }
            if (dir > 0 && InReach(FireRightX, x, dir, range)) { fireR = Mathf.Min(FireFrom, fireR + FireBeat); any = true; Beaten(FireRightX); }
            return any;
        }

        void Beaten(float at)
        {
            var hero = g.hero;
            hero.ki = Mathf.Min(100f, hero.ki + FireKi);
            // the heat drawn into him: sparks flying from the flames to his chest
            CbFx.Spark(new Vector3(at, 0.8f, -0.3f), CbFx.Fire, 14, 5f, 0.4f, 0.5f, -2, Mathf.Sign(hero.x - at) * 3f);
            CbVfx.Burst(new Vector3(at, 0.8f, -0.4f), CbFx.Flame, 1.6f, 0.15f);
            g.hud.Popup(new Vector3(at, 2.4f, 0), "+KI", CbHud.PopStyle.Normal);
            CbFx.Sfx("fire", 0.6f);
        }

        void UpdateEmber(float dt, CbHero hero)
        {
            float time = g.clock;
            UpdateEmberWay(dt, hero);
            // embers rising all over the courtyard and the burning jungle behind it
            emberT -= dt;
            if (emberT <= 0)
            {
                emberT = 0.05f;
                var at = new Vector3(hero.x + Random.Range(-14f, 14f), Random.Range(0f, 2f), Random.Range(-1f, 8f));
                CbFx.Spark(at, CbFx.Fire * Random.Range(0.5f, 1f), 1, 0.6f, 0.08f, 1.6f, -1.2f, Random.Range(-0.5f, 0.5f));
            }
            // the braziers by the altar
            foreach (var b in braziers)
                if (Random.value < 0.5f) CbFx.Spark(b + new Vector3(Random.Range(-0.15f, 0.15f), 0, 0), CbFx.Fire, 1, 1.2f, 0.35f, 0.5f, -5);
            // a pillar Grak ran into shakes for a moment
            pillarShake = Mathf.Max(0, pillarShake - dt);
            foreach (var p in emberPillars)
            {
                float k = Mathf.Abs(p.localPosition.x - pillarShakeX) < 3f ? pillarShake : 0f;
                p.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 40f) * 2.5f * k);
            }
            // the fire: creeping in from both sides, flickering; it burns him if he touches it
            if (fireOn)
            {
                fireL = Mathf.Max(FireClosest, fireL - FireCreep * dt);
                fireR = Mathf.Max(FireClosest, fireR - FireCreep * dt);
                fireLeft.localPosition = new Vector3(FireLeftX, 0, 0);
                fireRight.localPosition = new Vector3(FireRightX, 0, 0);
                for (int i = 0; i < fireTongues.Count; i++)
                {
                    var t = fireTongues[i];
                    float h0 = tongueHeights[i];
                    float s = 1f + Mathf.Sin(time * (8f + i % 5) + i * 1.7f) * 0.16f + Mathf.Sin(time * 13f + i) * 0.06f;
                    t.localScale = new Vector3(h0 * 0.95f * (1.6f - s * 0.6f), h0 * s, 1);
                    var p = t.localPosition;
                    p.y = h0 * s / 2;
                    t.localPosition = p;
                    t.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 3.1f + i * 0.9f) * 6f);
                }
                if (Random.value < 0.8f)
                    foreach (var fx in new[] { FireLeftX, FireRightX })
                        CbFx.Spark(new Vector3(fx + Random.Range(-0.3f, 0.3f), Random.Range(0.2f, 1.4f), Random.Range(-1.2f, 2f)), CbFx.Fire, 1, 1.5f, 0.25f, 0.5f, -4);
                if (hero.y < FireTall)
                {
                    if (hero.x < FireLeftX + 0.4f) hero.TakeHit(FireDamage, 1, unblockable: true, knock: 6f);
                    else if (hero.x > FireRightX - 0.4f) hero.TakeHit(FireDamage, -1, unblockable: true, knock: 6f);
                }
            }
            // the crystal rolling to Raja: a hop out of Grak's armour, then across the stones, turning over and over
            if (rollT >= 0 && looseCrystal != null)
            {
                rollT += dt;
                float k = Mathf.Clamp01(rollT / CrystalRoll);
                float hop = Mathf.Sin(Mathf.Clamp01(k * 2.2f) * Mathf.PI) * 1.6f * (1 - k);
                var at = Vector3.Lerp(rollFrom, rollTo, 1 - (1 - k) * (1 - k));
                at.y = Mathf.Lerp(rollFrom.y, rollTo.y, Mathf.Clamp01(k * 2.2f)) + hop;
                looseCrystal.localPosition = at;
                looseCrystal.localRotation = Quaternion.Euler(0, 0, -Mathf.Sign(rollTo.x - rollFrom.x) * k * 720f);
                if (Random.value < 0.6f) CbFx.Spark(at + new Vector3(0, 0.3f, -0.2f), CbFx.Flame, 1, 1.5f, 0.25f, 0.4f, 1);
            }
        }

        void ResetEmber()
        {
            fireOn = false;
            fireL = fireR = FireFrom;
            if (fireLeft != null) fireLeft.gameObject.SetActive(false);
            if (fireRight != null) fireRight.gameObject.SetActive(false);
            CrystalTaken();
            ResetEmberWay();
            // (torn out of its altar until Grak is beaten)
            if (altarCrystal != null) altarCrystal.gameObject.SetActive(false);
        }

        /// <summary>Raja fell in Grak's fight: the fire goes out (it starts again with the fight).</summary>
        public void LeaveGrakFight() => ResetEmber();
    }
}
