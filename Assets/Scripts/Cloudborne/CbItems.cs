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
        public enum Kind { Mango, KiCrystal, GoldenFruit, Coin, Scroll }

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
        static readonly string[] ModelNames = { "Mango", "KiCrystal", "GoldenFruit", "Coin", "Scroll" };
        static readonly Color[] Glows =
        {
            new Color(1f, 0.55f, 0.25f, 0.5f), new Color(1f, 0.6f, 0.15f, 0.6f), new Color(1f, 0.85f, 0.3f, 0.7f),
            new Color(1f, 0.8f, 0.25f, 0.45f), new Color(1f, 0.35f, 0.25f, 0.6f),
        };
        static readonly float[] Sizes = { 0.5f, 0.55f, 0.55f, 0.45f, 0.65f };

        readonly CbGame g;
        readonly Transform root;
        readonly List<Item> items = new List<Item>();
        readonly List<Crate> crates = new List<Crate>();
        readonly Dictionary<Kind, Material> glowMats = new Dictionary<Kind, Material>();
        Material crateMat;

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
            AddCrate(38f, Kind.KiCrystal, Kind.Coin, Kind.Coin);
            AddCrate(57f, Kind.Mango, Kind.Coin);
            AddCrate(62f, Kind.Coin, Kind.Coin, Kind.Coin);
            AddCrate(94f, Kind.Mango, Kind.KiCrystal);
            // coin trails: an arc to jump through, a row on the path
            for (int i = 0; i < 5; i++) Spawn(Kind.Coin, new Vector3(4 + i * 0.9f, 1.2f + Mathf.Sin(i / 4f * Mathf.PI) * 1.6f, 0), resting: true);
            for (int i = 0; i < 6; i++) Spawn(Kind.Coin, new Vector3(30 + i * 0.8f, 0.7f, 0), resting: true);
            for (int i = 0; i < 5; i++) Spawn(Kind.Coin, new Vector3(50 + i * 0.9f, 1.4f + Mathf.Sin(i / 4f * Mathf.PI) * 2.2f, 0), resting: true);
            // secrets: a golden fruit high above the path at the top of the climb (the last ledges crumble), the scroll
            // behind the start
            Spawn(Kind.GoldenFruit, new Vector3(111.6f, 8.3f, 0), resting: true);
            Spawn(Kind.Scroll, new Vector3(CbWorld.LevelMin + 1.2f, 0.8f, 0), resting: true);
        }

        void AddCrate(float x, params Kind[] contents)
        {
            var t = CbArt.Prop("Cloudborne/Items/Crate/Crate", root, CbArt.PropOutline);
            if (t == null)
            {
                // stand-in: a bamboo-coloured box with dark bands
                t = CbArt.Joint(root, 0, 0, 0, "crate");
                if (crateMat == null) crateMat = CbArt.Toon("#a8783e", CbArt.PropOutline);
                CbArt.Box(crateMat, t, new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.9f));
                var band = CbArt.Toon("#5c7a2e");
                CbArt.Box(band, t, new Vector3(0, 0.45f, -0.46f), new Vector3(0.95f, 0.12f, 0.04f));
                CbArt.Box(band, t, new Vector3(0, 0.12f, -0.46f), new Vector3(0.95f, 0.08f, 0.04f));
                CbArt.Box(band, t, new Vector3(0, 0.82f, -0.46f), new Vector3(0.95f, 0.08f, 0.04f));
            }
            t.localPosition = new Vector3(x, 0, 0.1f);
            t.localRotation = Quaternion.Euler(0, Random.Range(-12f, 12f), 0);
            crates.Add(new Crate { t = t, x = x, hp = 2, contents = contents });
        }

        // ---------- items ----------

        /// <summary>Puts an item at pos. A dropped one pops up and falls to the path; a resting one floats where it is.</summary>
        public void Spawn(Kind kind, Vector3 pos, bool resting = false)
        {
            string name = ModelNames[(int)kind];
            var t = CbArt.Joint(root, pos.x, pos.y, pos.z, name.ToLowerInvariant());
            var model = CbArt.Prop($"Cloudborne/Items/{name}/{name}", t, CbArt.PropOutline);
            if (model == null)
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

        /// <summary>A blow (Raja's paws, kicks or staff) reaching from x towards dir: crates in reach crack, then burst.</summary>
        public bool HitCrates(float x, float y, int dir, float range)
        {
            bool any = false;
            for (int i = crates.Count - 1; i >= 0; i--)
            {
                var c = crates[i];
                float dx = (c.x - x) * dir;
                if (dx < -0.5f - CrateHalfWidth || dx > range + CrateHalfWidth || y > 1.6f) continue;
                any = true;
                // a blow lands on it every frame of its hit window: count it once
                if (c.cooldown > 0) continue;
                c.cooldown = 0.2f;
                Break(i, dir, wholly: false);
            }
            return any;
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
            // burst: splinters fly and what was inside pops out
            for (int k = 0; k < 6; k++)
            {
                var chip = CbArt.Part(CbMesh.Box(), crateMat ?? CbArt.Toon("#a8783e"), g.transform, at + Random.insideUnitSphere * 0.3f,
                    new Vector3(0.35f, 0.12f, 0.08f), Random.rotation, shadows: false);
                g.AddDebris(chip, new Vector3(dir * Random.Range(1f, 5f), Random.Range(3f, 7f), Random.Range(-1f, 1f)), Random.Range(-600f, 600f));
            }
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
                    hero.hp = Mathf.Min(100, hero.hp + MangoHeal);
                    g.hud.Popup(at + Vector3.up, "+" + MangoHeal, CbHud.PopStyle.Normal);
                    CbFx.Spark(at, new Color(0.5f, 1f, 0.5f) * 2f, 12, 3, 0.35f, 0.5f, -3);
                    break;
                case Kind.KiCrystal:
                    hero.ki = Mathf.Min(100, hero.ki + KiCrystalKi);
                    g.hud.Popup(at + Vector3.up, "KI +" + KiCrystalKi, CbHud.PopStyle.Burn);
                    CbFx.Spark(at, CbFx.Fire, 12, 3, 0.35f, 0.5f, -3);
                    break;
                case Kind.GoldenFruit:
                    hero.hp = 100;
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
                case Kind.Scroll:
                    // the secret: the scroll teaches the flame cannon (SPECIAL)
                    CbHero.SpecialUnlocked = true;
                    g.hud.Banner("SECRET SCROLL! FLAME CANNON", true);
                    CbVfx.Ring(at, CbFx.Fire * 0.5f, 2.2f, 0.45f);
                    CbFx.Spark(at, CbFx.Fire, 24, 5, 0.4f, 0.6f, -3);
                    break;
            }
            g.hud.SetCoins(Coins);
            CbFx.Sfx(it.kind == Kind.Coin ? "coin" : "pickup", 0.7f);
        }
    }
}
