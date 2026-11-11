using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Things to find on the way: healing mangoes, ki crystals, the rare golden fruit (full health and ki), tiger
    /// coins, and the secret scroll that unlocks a power. Breakable bamboo crates along the path hold some of them,
    /// beaten hyenas drop others, and a few are hidden (a golden fruit at the top of a crumbling climb, the scroll
    /// behind the start). Each item uses its model from Resources/Cloudborne/Items/Name/Name (drawn in Canva, built in
    /// Meshy) or a glowing stand-in, bobs and turns, and is picked up when Raja touches it.
    /// </summary>
    public class CbItems
    {
        public enum Kind { Mango, KiCrystal, GoldenFruit, Coin, Scroll, Token, Key }

        class Item
        {
            public Kind kind;
            public Transform t, glow;
            public Vector3 pos, vel;
            public float phase, age;
            public bool resting;
        }

        class Crate
        {
            public Transform t;
            public float x, cooldown;
            public int hp;
            public Kind[] contents;
        }

        public const float MangoHeal = 25f, KiCrystalKi = 40f;
        const float PickupX = 0.75f, PickupY = 1.7f, CrateHalfWidth = 0.55f;
        // a crate is solid (the user: nothing should be walked through): Raja can't pass through one and can stand on
        // it; its body is the model's, 0.89 m across and 0.95 m tall
        const float CrateBody = 0.445f, CrateTop = 0.95f;
        // a tiger token is a big coin glowing orange
        static readonly string[] ModelNames = { "Mango", "KiCrystal", "GoldenFruit", "Coin", "Scroll", "Coin", "Key" };
        static readonly Color[] Glows =
        {
            new Color(1f, 0.55f, 0.25f, 0.5f), new Color(1f, 0.6f, 0.15f, 0.6f), new Color(1f, 0.85f, 0.3f, 0.7f),
            new Color(1f, 0.8f, 0.25f, 0.45f), new Color(1f, 0.35f, 0.25f, 0.6f), new Color(1f, 0.5f, 0.1f, 0.8f), new Color(1f, 0.9f, 0.4f, 0.7f),
        };
        static readonly float[] Sizes = { 0.5f, 0.55f, 0.55f, 0.45f, 0.65f, 0.8f, 0.6f };

        readonly CbGame g;
        readonly Transform root;
        readonly List<Item> items = new List<Item>();
        readonly List<Crate> crates = new List<Crate>();
        readonly Dictionary<Kind, Material> glowMats = new Dictionary<Kind, Material>();
        Material crateMat, crateWood;

        /// <summary>Tiger coins picked up this run; every coin is also banked for upgrades (PlayerPrefs).</summary>
        public int Coins { get; private set; }
        public int CrateCount => crates.Count;
        public int ItemCount => items.Count;
        public const string BankKey = "cb_coins";

        public CbItems(CbGame game, Transform parent)
        {
            g = game;
            root = new GameObject("Items").transform;
            root.SetParent(parent, false);
        }

        // ---------- the stage's layout ----------

        /// <summary>Clears everything and lays out stage 1-1's crates, coin trails and secrets.</summary>
        public void Reset()
        {
            foreach (var i in items) Object.Destroy(i.t.gameObject);
            items.Clear();
            foreach (var c in crates) Object.Destroy(c.t.gameObject);
            crates.Clear();
            Coins = 0;

            // crates along the path, each with something in it
            AddCrate(9f, Kind.Coin, Kind.Coin, Kind.Coin);
            AddCrate(27f, Kind.Mango, Kind.Coin);
            AddCrate(42f, Kind.KiCrystal, Kind.Coin, Kind.Coin);
            AddCrate(52f, Kind.Mango, Kind.Coin);
            AddCrate(73.5f, Kind.Coin, Kind.Coin, Kind.Coin);
            AddCrate(105.5f, Kind.Mango, Kind.KiCrystal);
            AddCrate(134.5f, Kind.Mango, Kind.Coin);
            AddCrate(191f, Kind.KiCrystal, Kind.Coin, Kind.Coin);
            AddCrate(244f, Kind.Mango, Kind.Coin);
            AddCrate(334f, Kind.KiCrystal, Kind.Coin);
            AddCrate(399.5f, Kind.Mango, Kind.Coin, Kind.Coin);
            AddCrate(474f, Kind.Mango, Kind.KiCrystal);
            AddCrate(528f, Kind.Mango, Kind.Coin);
            AddCrate(534f, Kind.Coin, Kind.Coin, Kind.KiCrystal);
            // coin trails: an arc to jump through, a row along the rope bridge, an arc in the second fight
            for (int i = 0; i < 5; i++) Spawn(Kind.Coin, new Vector3(4 + i * 0.9f, 1.2f + Mathf.Sin(i / 4f * Mathf.PI) * 1.6f, 0), resting: true);
            for (int i = 0; i < 6; i++) Spawn(Kind.Coin, new Vector3(31.8f + i * 0.85f, 0.7f, 0), resting: true);
            for (int i = 0; i < 5; i++) Spawn(Kind.Coin, new Vector3(54 + i * 0.9f, 1.4f + Mathf.Sin(i / 4f * Mathf.PI) * 2.2f, 0), resting: true);
            // over the river: above its stones, logs and the rock, for jumping across
            foreach (var cx in new[] { 80.6f, 83.6f, 86.6f, 92.2f, 95.2f, 97.4f, 99.8f }) Spawn(Kind.Coin, new Vector3(cx, 1.2f, 0), resting: true);
            Spawn(Kind.Coin, new Vector3(89f, 2.4f, 0), resting: true);
            // up the mountain: one over each ledge, a row along the plateau
            foreach (var (cx, top) in new[] { (146.25f, 4.2f), (149.2f, 5.6f), (154.7f, 7.8f), (157.3f, 9f), (171.5f, 10.8f), (181.7f, 8.6f), (184.7f, 5.8f), (187.7f, 3f) })
                Spawn(Kind.Coin, new Vector3(cx, top + 1f, 0), resting: true);
            for (int i = 0; i < 6; i++) Spawn(Kind.Coin, new Vector3(160f + i * 1.2f, 11.2f, 0), resting: true);
            LayOutWays();
            // secrets: a golden fruit on the mountain's summit (past the crumbling ledges), and
            // the scroll on the highest of the hidden ledges above the plateau (CbLevel)
            Spawn(Kind.GoldenFruit, new Vector3(177.5f, CbLevel.SummitTop + 1.2f, 0), resting: true);
            Spawn(Kind.Scroll, new Vector3(160.3f, 15.8f, 0), resting: true);
        }

        void AddCrate(float x, params Kind[] contents)
        {
            // the model sits in a joint that is turned and shaken, so the model's own root rotation (the axis
            // conversion from Blender) stays and the crate stands on its base instead of lying half in the ground
            var t = CbArt.Joint(root, x, 0, 0.1f, "crate");
            if (CbArt.Prop("Cloudborne/Items/Crate/Crate", t, CbArt.PropOutline) == null)
            {
                // stand-in: a bamboo-coloured box with dark bands
                if (crateMat == null) crateMat = CbArt.Toon("#a8783e", CbArt.PropOutline);
                CbArt.Box(crateMat, t, new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.9f));
                var band = CbArt.Toon("#5c7a2e");
                CbArt.Box(band, t, new Vector3(0, 0.45f, -0.46f), new Vector3(0.95f, 0.12f, 0.04f));
                CbArt.Box(band, t, new Vector3(0, 0.12f, -0.46f), new Vector3(0.95f, 0.08f, 0.04f));
                CbArt.Box(band, t, new Vector3(0, 0.82f, -0.46f), new Vector3(0.95f, 0.08f, 0.04f));
            }
            t.localRotation = Quaternion.Euler(0, Random.Range(-12f, 12f), 0);
            crates.Add(new Crate { t = t, x = x, hp = 2, contents = contents });
        }

        /// <summary>
        /// Coins, tokens and the key along the long middle of the stage (CbLevelWays): along each swing and bar, over
        /// the traps, the bridge, the rafts and the log, up the canopy and down the zipline, behind the waterfall.
        /// </summary>
        void LayOutWays()
        {
            void C(float x, float y) => Spawn(Kind.Coin, new Vector3(x, y, 0), resting: true);
            // the vine ravine
            foreach (var x in new[] { 204f, 207.8f, 211.5f, 226.8f }) C(x, 3.4f);
            for (float x = 216.5f; x < 223f; x += 1.3f) C(x, 3.6f);
            for (int i = 0; i < 4; i++) C(235.5f + i * 0.6f, 2f + i * 1.3f);
            Spawn(Kind.Token, new Vector3(238f, 7.3f, 0), resting: true);
            // the temple of traps
            foreach (var x in new[] { 250f, 252f, 258.5f, 260.5f, 267.8f, 271.5f, 279.5f, 286f }) C(x, 1.2f);
            Spawn(Kind.Coin, new Vector3(255.2f, 2.2f, 0), resting: true);
            // the gong's bridge, the hive, the cage
            for (float x = 301f; x < 312f; x += 1.5f) C(x, 1f);
            foreach (var x in new[] { 316f, 324f, 329f }) C(x, 1.2f);
            // the raft river: over the rafts' way, the island, the log
            foreach (var x in new[] { 340f, 349f, 353f, 357f, 361f, 365f, 371f, 373f, 375f, 377f, 382f, 386f, 390f }) C(x, 1.3f);
            C(367.8f, 2f);
            // the canopy: up the vine pillar, along the ledges and the bars, down the zipline
            for (float y = 1.5f; y < 7.5f; y += 1.3f) C(407.4f, y);
            C(411.6f, 9.4f); C(415.6f, 10.2f);
            for (float x = 417.6f; x < 422f; x += 1.1f) C(x, 9.8f);
            Spawn(Kind.Key, new Vector3(423.6f, 10.6f, 0), resting: true);
            for (float x = 426.5f; x < 437f; x += 1.8f) C(x, Mathf.Lerp(11f, 3.4f, Mathf.InverseLerp(424.6f, 437.2f, x)) - 0.8f);
            for (float y = 2.5f; y < 10f; y += 1.6f) C(429.5f, y);
            for (float y = 1.5f; y < 8f; y += 1.5f) C(436.6f, y);
            for (float x = 441f; x < 446f; x += 1.2f) C(x, 10.4f);
            // behind the waterfall
            C(447f, 3.6f); C(448.4f, 5.8f);
            Spawn(Kind.Token, new Vector3(447.2f, 8.1f, 0), resting: true);
            // the boulder road, the shaft on the ridge
            foreach (var x in new[] { 454f, 457f, 460f, 463f }) C(x, 1.8f);
            for (float y = 1.5f; y < 8f; y += 1.4f) C(506.3f, y);
            Spawn(Kind.Mango, new Vector3(506.3f, 9.3f, 0), resting: true);
        }

        // ---------- items ----------

        /// <summary>Puts an item at pos. A dropped one pops up and falls to the path; a resting one floats where it is.</summary>
        public void Spawn(Kind kind, Vector3 pos, bool resting = false)
        {
            string name = ModelNames[(int)kind];
            var t = CbArt.Joint(root, pos.x, pos.y, pos.z, name.ToLowerInvariant());
            var model = CbArt.Prop($"Cloudborne/Items/{name}/{name}", t, CbArt.PropOutline);
            if (kind == Kind.Token && model != null) model.localScale *= 1.8f;
            // the key model lies flat: stood up, its face to the camera
            if (kind == Kind.Key && model != null) model.localRotation = Quaternion.Euler(90, 0, 0) * model.localRotation;
            if (kind == Kind.Key && model == null)
            {
                var gold = CbArt.Toon("#e8b93a", CbArt.PropOutline);
                CbArt.Part(CbMesh.Torus(0.25f), gold, t, new Vector3(0, 0.18f, 0), new Vector3(0.22f, 0.22f, 0.22f), Quaternion.Euler(90, 0, 0));
                CbArt.Box(gold, t, new Vector3(0, -0.12f, 0), new Vector3(0.07f, 0.45f, 0.07f));
                CbArt.Box(gold, t, new Vector3(0.08f, -0.28f, 0), new Vector3(0.14f, 0.06f, 0.07f));
            }
            else if (model == null)
            {
                // stand-in: a glowing ball in the item's colour
                var c = Glows[(int)kind];
                CbArt.Part(CbMesh.Sphere, CbArt.Toon(new Color(c.r, c.g * 0.9f, c.b), CbArt.PropOutline), t, Vector3.zero, Vector3.one * Sizes[(int)kind] * 0.7f);
            }
            if (!glowMats.TryGetValue(kind, out var gm)) glowMats[kind] = gm = CbArt.Glow(Glows[(int)kind] * 1.6f, CbArt.Soft);
            var glow = CbArt.Part(CbMesh.Quad, gm, t, new Vector3(0, 0, 0.2f), Vector3.one * Sizes[(int)kind] * 2.4f, shadows: false);
            var vel = resting ? Vector3.zero : new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(5f, 7f), 0);
            items.Add(new Item { kind = kind, t = t, glow = glow, pos = pos, vel = vel, resting = resting, phase = Random.Range(0f, 6f) });
        }

        /// <summary>What a beaten hyena leaves: always coins, sometimes a mango or a ki crystal.</summary>
        public void Drop(Vector3 at)
        {
            Spawn(Kind.Coin, at);
            Spawn(Kind.Coin, at);
            float r = Random.value;
            if (r < 0.2f) Spawn(Kind.Mango, at);
            else if (r < 0.35f) Spawn(Kind.KiCrystal, at);
        }

        // ---------- crates ----------

        /// <summary>
        /// A blow (Raja's paws, kicks or staff) reaching from x towards dir: crates in reach crack, then burst. True if
        /// a crate took the blow (once per blow, so its sound plays once).
        /// </summary>
        /// <summary>
        /// Narrows a soldier's stretch of path (lo to hi) to the crates either side of him at x: he can't walk through
        /// one that stands (he did: the user), only where it was once it is broken.
        /// </summary>
        public void ClipByCrates(float x, float body, ref float lo, ref float hi)
        {
            foreach (var c in crates)
            {
                if (c.hp <= 0) continue;
                if (c.x <= x) lo = Mathf.Max(lo, c.x + CrateBody + body);
                else hi = Mathf.Min(hi, c.x - CrateBody - body);
            }
        }

        public bool HitCrates(float x, float y, int dir, float range)
        {
            bool any = false;
            for (int i = crates.Count - 1; i >= 0; i--)
            {
                var c = crates[i];
                float dx = (c.x - x) * dir;
                if (dx < -0.5f - CrateHalfWidth || dx > range + CrateHalfWidth || y > 1.6f) continue;
                // a blow lands on it every frame of its hit window: count it once
                if (c.cooldown > 0) continue;
                any = true;
                c.cooldown = 0.2f;
                Break(i, dir, wholly: false);
            }
            return any;
        }

        /// <summary>Moving from x0 to x1 at height y: a crate stops him at its side, like a block.</summary>
        public float BlockX(float x0, float x1, float y)
        {
            foreach (var c in crates)
            {
                if (c.hp <= 0 || y >= CrateTop - 0.05f) continue;
                float left = c.x - CrateBody - CbLevel.HalfWidth, right = c.x + CrateBody + CbLevel.HalfWidth;
                if (x0 <= left + 0.01f && x1 > left) x1 = left;
                else if (x0 >= right - 0.01f && x1 < right) x1 = right;
            }
            return x1;
        }

        /// <summary>The top of a crate under x that a body at fromY can land on; far below if there is none.</summary>
        public float SupportAt(float x, float fromY)
        {
            foreach (var c in crates)
                if (c.hp > 0 && Mathf.Abs(x - c.x) < CrateBody + CbLevel.HalfWidth * 0.5f && CrateTop <= fromY + 0.05f) return CrateTop;
            return float.NegativeInfinity;
        }

        /// <summary>A shot (fireball, beam) at x: a crate there bursts at once. True if one was hit.</summary>
        public bool ShotCrates(float x, float y)
        {
            for (int i = crates.Count - 1; i >= 0; i--)
            {
                if (Mathf.Abs(crates[i].x - x) > CrateHalfWidth + 0.3f || y > 1.8f) continue;
                Break(i, x < crates[i].x ? 1 : -1, wholly: true);
                return true;
            }
            return false;
        }

        void Break(int i, int dir, bool wholly)
        {
            var c = crates[i];
            c.hp = wholly ? 0 : c.hp - 1;
            var at = new Vector3(c.x, 0.5f, -0.4f);
            CbFx.Spark(at, CbFx.Dust, 8, 4, 0.4f, 0.4f, 6);
            if (c.hp > 0)
            {
                // cracked: it jolts
                c.t.localRotation = Quaternion.Euler(0, Random.Range(-15f, 15f), dir * 6f);
                CbFx.Sfx("hit", 0.6f);
                return;
            }
            // burst: boards and splinters of its own wood fly, a puff of dust, and what was inside pops out
            if (crateWood == null)
            {
                var tex = Resources.Load<Texture2D>("Cloudborne/Items/Crate/Crate_Albedo");
                crateWood = tex != null ? CbArt.AnimeToon(tex, CbArt.PropOutline) : crateMat ?? CbArt.Toon("#a8783e", CbArt.PropOutline);
            }
            for (int k = 0; k < 7; k++)
            {
                // two long boards, the rest thin splinters
                var size = k < 2 ? new Vector3(0.62f, 0.13f, 0.05f) : new Vector3(Random.Range(0.22f, 0.4f), 0.05f, 0.035f);
                var chip = CbArt.Part(CbMesh.Box(), crateWood, g.transform, at + Random.insideUnitSphere * 0.3f, size, Random.rotation, shadows: false);
                g.AddDebris(chip, new Vector3(dir * Random.Range(1f, 5f), Random.Range(3f, 7f), Random.Range(-1f, 1f)), Random.Range(-600f, 600f));
            }
            CbFx.Puff(at, new Color(0.85f, 0.76f, 0.6f), 7, 1.6f, 0.7f, 0.45f);
            foreach (var k in c.contents) Spawn(k, new Vector3(c.x, 0.6f, 0));
            CbVfx.Burst(at, CbFx.Hit, 1.6f, 0.15f);
            CbFx.Sfx("heavy", 0.6f);
            Object.Destroy(c.t.gameObject);
            crates.RemoveAt(i);
        }

        // ---------- every frame ----------

        public void Update(float dt, CbHero hero, Camera cam)
        {
            foreach (var c in crates) c.cooldown = Mathf.Max(0, c.cooldown - dt);
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                it.age += dt;
                if (!it.resting)
                {
                    // dropped: arcs out and bounces to rest on the path
                    it.vel.y -= 22 * dt;
                    it.pos += it.vel * dt;
                    if (it.pos.y < 0.55f) { it.pos.y = 0.55f; it.vel.y = Mathf.Abs(it.vel.y) * 0.35f; it.vel.x *= 0.6f; if (it.vel.y < 1f) it.resting = true; }
                }
                float bob = it.resting ? Mathf.Sin(it.age * 3f + it.phase) * 0.12f : 0;
                it.t.localPosition = it.pos + Vector3.up * bob;
                it.t.localRotation = Quaternion.Euler(0, it.age * (it.kind == Kind.Coin ? 220f : 70f), 0);
                if (cam != null) it.glow.rotation = cam.transform.rotation;
                // picked up: Raja touches it (a dropped one only once it has popped out)
                if (it.age > 0.25f && Mathf.Abs(it.pos.x - hero.x) < PickupX && it.pos.y - hero.y > -0.5f && it.pos.y - hero.y < PickupY)
                {
                    Collect(it, hero);
                    Object.Destroy(it.t.gameObject);
                    items.RemoveAt(i);
                }
            }
        }

        void Collect(Item it, CbHero hero)
        {
            var at = it.pos + new Vector3(0, 0, -0.4f);
            switch (it.kind)
            {
                case Kind.Mango:
                    hero.hp = Mathf.Min(hero.MaxHp, hero.hp + MangoHeal);
                    g.hud.Popup(at + Vector3.up, "+" + MangoHeal, CbHud.PopStyle.Normal);
                    CbFx.Spark(at, new Color(0.5f, 1f, 0.5f) * 2f, 12, 3, 0.35f, 0.5f, -3);
                    break;
                case Kind.KiCrystal:
                    hero.ki = Mathf.Min(100, hero.ki + KiCrystalKi);
                    g.hud.Popup(at + Vector3.up, "KI +" + KiCrystalKi, CbHud.PopStyle.Burn);
                    CbFx.Spark(at, CbFx.Fire, 12, 3, 0.35f, 0.5f, -3);
                    break;
                case Kind.GoldenFruit:
                    hero.hp = hero.MaxHp;
                    hero.ki = 100;
                    g.hud.Banner("GOLDEN FRUIT!", true);
                    CbVfx.Ring(at, CbFx.Flame * 0.5f, 2f, 0.4f);
                    CbFx.Spark(at, CbFx.Flame, 24, 5, 0.4f, 0.6f, -3);
                    break;
                case Kind.Coin:
                    Coins++;
                    g.score += 50;
                    PlayerPrefs.SetInt(BankKey, PlayerPrefs.GetInt(BankKey, 0) + 1);
                    CbFx.Spark(at, CbFx.Flame, 5, 2, 0.25f, 0.3f, -2);
                    break;
                case Kind.Token:
                    g.TokenFound();
                    g.score += 1000;
                    CbVfx.Ring(at, CbFx.Fire * 0.5f, 2.2f, 0.45f);
                    CbFx.Spark(at, CbFx.Fire, 24, 5, 0.4f, 0.6f, -3);
                    break;
                case Kind.Key:
                    g.HasKey = true;
                    g.hud.Banner("A KEY! (A CHEST WAITS BY THE WATERFALL)", true);
                    CbFx.Spark(at, CbFx.Flame, 16, 4, 0.35f, 0.5f, -3);
                    break;
                case Kind.Scroll:
                    // the secret: the scroll teaches the special - the staff storm, or once his fire is awake the flame cannon
                    CbHero.SpecialUnlocked = true;
                    g.hud.Banner(CbHero.FireAwake ? "SECRET SCROLL! FLAME CANNON" : "SECRET SCROLL! STAFF STORM", true);
                    CbVfx.Ring(at, CbFx.Fire * 0.5f, 2.2f, 0.45f);
                    CbFx.Spark(at, CbFx.Fire, 24, 5, 0.4f, 0.6f, -3);
                    break;
            }
            g.hud.SetCoins(Coins);
            CbFx.Sfx(it.kind == Kind.Coin ? "coin" : "pickup", 0.7f);
        }
    }
}
