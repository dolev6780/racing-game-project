using System.Collections;
using System.Linq;
using Cloudborne;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    /// <summary>
    /// Plays the Cloudborne prototype for real: starts a run, walks into the first fight zone and uses every move
    /// (paw combo, staff smash and whirl, launcher, thrust, slam, fireball, guard, special, dash). Any exception or
    /// Debug.LogError fails the test.
    /// </summary>
    public class CloudborneSmokeTests
    {
        CbGame game;

        // every test starts from a fresh save: no coins, upgrades or opening seen
        [SetUp]
        public void FreshSave() => CbSave.ResetProgress();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (game != null) Object.Destroy(game.gameObject);
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>Waits (real time) until the game is in the mode, or the time is up.</summary>
        IEnumerator WaitForMode(CbGame.Mode mode, float seconds)
        {
            for (float waited = 0; game.mode != mode && waited < seconds; waited += Time.unscaledDeltaTime) yield return null;
            // let a fade that was running finish its frame
            yield return null;
        }

        [UnityTest]
        public IEnumerator DrawnCutscenePlaysAndCanBeSkipped()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            Texture2D Paint(Color c)
            {
                var t = new Texture2D(64, 36);
                var px = new Color[64 * 36];
                for (int i = 0; i < px.Length; i++) px[i] = c;
                t.SetPixels(px);
                t.Apply();
                return t;
            }
            var shots = new[]
            {
                new CbComic.Shot { texture = Paint(Color.blue), dur = 0.6f, fx = "stars", caption = "One", flash = true },
                new CbComic.Shot { texture = Paint(Color.red), dur = 0.6f, fx = "sparks", shake = true, who = "RAJA", caption = "Two" },
                new CbComic.Shot { title = "RAJA", subtitle = "AND THE FOUR CRYSTALS", dur = 0.5f, fx = "embers" },
            };
            Assert.IsTrue(CbComic.Available(shots));
            Assert.IsFalse(CbComic.Available(new[] { new CbComic.Shot { image = "no_such_painting" } }), "A missing painting counts as there");
            bool done = false;
            game.comic.Play(shots, () => done = true);
            Assert.IsTrue(game.comic.Playing);
            for (float waited = 0; !done && waited < 5f; waited += Time.unscaledDeltaTime)
            {
                game.comic.Update(Time.unscaledDeltaTime);
                yield return null;
            }
            Assert.IsTrue(done, "The drawn cutscene never finished");
            Assert.IsFalse(game.comic.Playing);
            // SKIP ends it at once
            done = false;
            game.comic.Play(shots, () => done = true);
            game.comic.Skip();
            Assert.IsTrue(done, "SKIP did not end the drawn cutscene");
            game.comic.Hide();
        }

        [UnityTest]
        public IEnumerator SergeantSnapGuardsTheShrineGate()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipToBoss();
            yield return Frames(2);
            // near the gate he steps out: a scene, then the fight in a shut arena
            game.Teleport(118f);
            yield return WaitForMode(CbGame.Mode.Scene, 3f);
            Assert.AreEqual(CbGame.Mode.Scene, game.mode, "Sergeant Snap's scene did not play");
            game.scene.Skip();
            yield return Frames(3);
            Assert.AreEqual(CbGame.Mode.Play, game.mode);
            var snap = game.boss;
            Assert.IsNotNull(snap, "Sergeant Snap did not appear");
            Assert.AreEqual(CbEnemy.Kind.Sergeant, snap.kind);
            Assert.IsNotNull(snap.model, "Snap's model did not load");
            Assert.IsNotNull(game.lockZone, "The arena is not shut");
            // he attacks (Raja made tough enough to watch)
            game.hero.hp = 9999;
            bool attacked = false;
            for (float waited = 0; waited < 6f && !attacked; waited += Time.deltaTime)
            {
                yield return null;
                attacked |= snap.state == CbEnemy.State.Atk;
            }
            Assert.IsTrue(attacked, "Snap never attacked");
            // the gate stays out of reach while he stands
            game.Teleport(CbWorld.GoalX + 1f);
            yield return Frames(2);
            Assert.Less(game.hero.x, CbWorld.GoalX, "Raja left the arena past Snap");
            // beaten (and anyone he whistled for): the way opens and the stage clears at the gate
            for (int k = 0; k < 60 && game.enemies.Exists(e => !e.dead); k++)
            {
                foreach (var e in game.enemies.ToArray())
                    if (!e.dead) game.HurtEnemy(e, 40, 1, 8, 2, true, true, false, true);
                yield return Frames(2);
            }
            Assert.IsTrue(snap.dead, "Snap could not be beaten");
            yield return Frames(3);
            Assert.IsNull(game.lockZone, "The arena did not open after Snap was beaten");
            game.Teleport(CbWorld.GoalX + 0.5f);
            yield return WaitForMode(CbGame.Mode.Over, 4f);
            Assert.AreEqual(CbGame.Mode.Over, game.mode, "The stage did not clear at the shrine gate");
            Assert.Greater(game.hero.hp, 0f);
        }

        [UnityTest]
        public IEnumerator TheScrollHidesOnLedgesThatShowWhenYouClimb()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var level = game.level;
            // hidden: nothing to stand on up there while Raja is on the path
            Assert.AreEqual(0f, level.SupportAt(97.6f, 6f), 0.001f, "The hidden ledge holds him before it was found");
            // up on the tall pillar, close by, the first hidden ledge shows itself
            game.Teleport(101.8f);
            game.hero.y = 2.6f;
            for (float waited = 0; waited < 1f; waited += Time.deltaTime) yield return null;
            Assert.AreEqual(5.2f, level.SupportAt(97.6f, 6f), 0.001f, "Climbing near did not reveal the hidden ledge");
        }

        [UnityTest]
        public IEnumerator BuildsTheJungleAndRaja()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            Assert.AreEqual(CbGame.Mode.Title, game.mode);
            Assert.IsNotNull(game.hero.f.root, "Raja was not built");
            Assert.IsNotNull(game.hero.model, "Raja's model did not load (Resources/Cloudborne/Raja, legacy animation)");
            Assert.AreEqual(29, game.hero.model.ClipCount, "Raja's model is missing animation clips");
            foreach (var prop in new[] { "Staff", "Machete", "Shield" })
                Assert.IsNotNull(Resources.Load<GameObject>($"Cloudborne/Props/{prop}/{prop}"), $"The {prop} model is missing");
            var scenery = new[] { "Tree", "Palm", "Fern", "Leaves", "Boulder", "Pillar", "Gate", "Karst" };
            foreach (var name in scenery)
            {
                var model = Resources.Load<GameObject>($"Cloudborne/Jungle/{name}/{name}");
                Assert.IsNotNull(model, $"The jungle's {name} model is missing");
                Assert.IsTrue(model.GetComponentInChildren<MeshFilter>().sharedMesh.isReadable, $"The {name} mesh must be readable to be merged");
            }
            // the scenery is merged: no copy of a model is left standing on its own
            Assert.AreEqual(0, game.world.root.GetComponentsInChildren<Transform>().Count(t => scenery.Any(n => t.name.StartsWith(n))),
                "Copies of the scenery models were left unmerged");
            // standing upright behind the path: nothing near leans out towards the camera, lies down or towers up
            foreach (var r in game.world.root.Find("near").GetComponentsInChildren<MeshRenderer>())
            {
                var b = r.bounds;
                Assert.Greater(b.min.z, 1f, $"Scenery near the path reaches out to z = {b.min.z:0.0} ({r.name})");
                Assert.Greater(b.min.y, -1.5f, $"Scenery near the path sinks to y = {b.min.y:0.0} ({r.name})");
                Assert.Less(b.max.y, 14f, $"Scenery near the path rises to y = {b.max.y:0.0} ({r.name})");
            }
            var backdrop = game.world.root.Find("backdrop");
            Assert.IsNotNull(backdrop, "The painted backdrop is missing (Resources/Cloudborne/Jungle/Backdrop, Shaders/Backdrop)");
            Assert.IsTrue(backdrop.GetComponent<MeshRenderer>().sharedMaterial.shader.isSupported, "The backdrop shader doesn't run");
            Assert.Greater(game.world.root.GetComponentsInChildren<MeshRenderer>().Length, 20, "The jungle has too few parts");
            Assert.IsNotNull(Resources.Load<Shader>("Shaders/Glow"), "Glow shader missing");
            Assert.IsTrue(Resources.Load<Shader>("Shaders/Toon").isSupported, "The toon shader doesn't run");
            foreach (var item in new[] { "Mango", "KiCrystal", "GoldenFruit", "Coin", "Scroll", "Crate" })
                Assert.IsNotNull(Resources.Load<GameObject>($"Cloudborne/Items/{item}/{item}"), $"The {item} model is missing");
            foreach (var fx in new[] { "Fireball", "Beam", "Explosion" })
                Assert.IsNotNull(CbVfx.Painted(fx), $"The painted {fx} effect is missing (Resources/Cloudborne/Effects)");
        }

        [UnityTest]
        public IEnumerator OpeningSceneTakesKodaAndLeadsIntoTheChase()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            // START on the title plays the opening the first time
            bool pressed = false;
            game.TestInput = i => { if (!pressed) { i.confirm = true; pressed = true; } };
            yield return Frames(2);
            Assert.AreEqual(CbGame.Mode.Loading, game.mode, "PLAY did not go to the loading screen");
            // the first time, the drawn prologue plays first; SKIP fades from it into the opening scene
            yield return WaitForMode(CbGame.Mode.Comic, 6f);
            Assert.AreEqual(CbGame.Mode.Comic, game.mode, "The loading screen did not open on the drawn prologue");
            Assert.IsTrue(game.comic.Playing, "The prologue is not playing");
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(CbGame.Mode.Comic, game.mode, "The prologue ended by itself after a second");
            bool skipped = false;
            game.TestInput = i => { if (!skipped) { i.pause = true; skipped = true; } };
            yield return WaitForMode(CbGame.Mode.Scene, 4f);
            Assert.AreEqual(CbGame.Mode.Scene, game.mode, "SKIP on the prologue did not lead into the opening scene");
            Assert.IsNotNull(game.scene);
            // tapping through the talk: every line shows, then goes on, and the stage begins
            Time.captureDeltaTime = 1f / 20f;
            bool sawKoda = false, sawRaiders = false, sawNet = false, sawDrawn = false;
            int frames = 0;
            game.TestInput = i => i.confirm = Time.frameCount % 8 == 0;
            while (game.mode == CbGame.Mode.Scene && frames++ < 2000)
            {
                yield return null;
                var stage = game.transform.Find("Opening");
                if (stage == null) continue;
                sawKoda |= stage.Find("Koda") != null;
                sawRaiders |= stage.Find("SgtSnap") != null && stage.Find("Raider") != null;
                sawNet |= stage.Find("Koda/net") != null;
                sawDrawn |= game.comic.Playing;
            }
            game.TestInput = null;
            Time.captureDeltaTime = 0f;
            // objects are removed at the end of the frame
            yield return Frames(2);
            Assert.AreEqual(CbGame.Mode.Play, game.mode, "The opening never handed over to the stage");
            Assert.IsTrue(sawKoda, "Koda was not in the opening");
            Assert.IsTrue(sawRaiders, "Sergeant Snap and his raiders never came");
            Assert.IsTrue(sawNet, "Koda was never netted");
            Assert.IsTrue(sawDrawn, "The opening's drawn panels (Koda boasting, Koda taken) never played");
            // no powers yet: his fire wakes only at the fire crystal (1-5)
            Assert.IsFalse(CbHero.FireAwake, "Raja's fire woke in the opening");
            Assert.IsNull(game.transform.Find("Opening"), "The scene's cast was left on the path");
            Assert.IsNull(game.scene);
            Assert.AreEqual(CbHero.State.Idle, game.hero.state);
            // and Raja can move again
            float x0 = game.hero.x;
            game.TestInput = i => i.x = 1;
            yield return new WaitForSeconds(0.5f);
            game.TestInput = null;
            Assert.Greater(game.hero.x, x0 + 1f, "Raja doesn't run after the opening");
        }

        [UnityTest]
        public IEnumerator MainMenuWorkshopAndBackToMenu()
        {
            CbSave.Coins = 100;
            game = CbGame.Boot();
            yield return Frames(3);
            Assert.IsTrue(game.menu.Visible, "The main menu is not shown at start");
            Assert.AreEqual(CbMenu.Page.Main, game.menu.Current);
            // every page opens, and back (Esc) returns to the main page
            foreach (var page in new[] { CbMenu.Page.Stages, CbMenu.Page.Settings, CbMenu.Page.Help, CbMenu.Page.Workshop })
            {
                game.menu.Show(page);
                yield return Frames(2);
                Assert.AreEqual(page, game.menu.Current);
            }
            // Baba's workshop: Tiger Heart costs 60, the next level 150
            Assert.IsTrue(game.menu.Press("BUY"), "No BUY button in the workshop");
            Assert.AreEqual(1, CbSave.Level(CbSave.Upgrades[0]), "Tiger Heart was not bought");
            Assert.AreEqual(40, CbSave.Coins, "The price was not paid");
            game.menu.Press("BUY");
            Assert.AreEqual(1, CbSave.Level(CbSave.Upgrades[0]), "Bought without enough coins");
            bool back = false;
            game.TestInput = i => { if (!back) { i.pause = true; back = true; } };
            yield return Frames(2);
            game.TestInput = null;
            Assert.AreEqual(CbMenu.Page.Main, game.menu.Current, "Back did not return to the main page");

            // PLAY: the opening (skipped), then the stage with 20 more health
            game.PlayStage("1-1");
            yield return WaitForMode(CbGame.Mode.Comic, 6f);
            game.comic.Skip();
            yield return WaitForMode(CbGame.Mode.Scene, 4f);
            Assert.IsFalse(game.menu.Visible);
            Assert.AreEqual(CbGame.Mode.Scene, game.mode);
            game.scene.Skip();
            yield return Frames(2);
            Assert.AreEqual(CbGame.Mode.Play, game.mode);
            Assert.AreEqual(120f, game.hero.hp, 0.01f, "Tiger Heart did not raise Raja's health");

            // pause, then MENU: the run ends and the menu is back (no pausing while the loading screen still fades open)
            for (float waited = 0; game.fade.Busy && waited < 2f; waited += Time.unscaledDeltaTime) yield return null;
            bool paused = false, quit = false;
            game.TestInput = i => { if (!paused) { i.pause = true; paused = true; } };
            yield return Frames(2);
            Assert.AreEqual(CbGame.Mode.Paused, game.mode);
            game.TestInput = i => { if (!quit) { i.menu = true; quit = true; } };
            yield return WaitForMode(CbGame.Mode.Title, 4f);
            game.TestInput = null;
            Assert.AreEqual(CbGame.Mode.Title, game.mode, "MENU did not leave the run");
            Assert.IsTrue(game.menu.Visible, "The menu did not come back");
            // the opening was seen: PLAY goes straight to the stage now
            for (float waited = 0; game.fade.Busy && waited < 2f; waited += Time.unscaledDeltaTime) yield return null;
            game.PlayStage("1-1");
            yield return WaitForMode(CbGame.Mode.Play, 6f);
            Assert.AreEqual(CbGame.Mode.Play, game.mode);
        }

        [UnityTest]
        public IEnumerator ApprovedStoryPaintingsLoad()
        {
            yield return null;
            foreach (var name in new[] { "prologue_temple", "prologue_crown", "prologue_shatter", "prologue_leap", "prologue_jungle",
                         "koda_boast", "koda_taken", "raja_angry", "raja_journey" })
            {
                var tex = Resources.Load<Texture2D>("Cloudborne/Story/" + name);
                Assert.IsNotNull(tex, $"The painting {name} is missing (Resources/Cloudborne/Story)");
                Assert.AreEqual(1680, tex.width, $"The painting {name} was resized on import");
            }
            Assert.IsTrue(CbComic.Available(CbStory.KodaBoast), "Koda's boast can't be shown");
            Assert.IsTrue(CbComic.Available(CbStory.KodaTaken), "Koda being taken can't be shown");
            Assert.IsTrue(CbComic.Available(CbStory.Journey), "Raja getting ready for the road can't be shown");
            Assert.IsTrue(CbComic.Available(CbStory.Prologue), "The prologue can't be shown");
        }

        [UnityTest]
        public IEnumerator StoryCastHaveTheirModels()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            // Koda, Sergeant Snap and Baba Orang: built in Meshy from the approved drawings, each with 9 clips
            foreach (var (path, clip) in new[] { ("Cloudborne/Koda/Koda", "Show_Both_Arm_Muscles"), ("Cloudborne/HyenaSergeant/HyenaSergeant", "Triple_Combo_Attack"), ("Cloudborne/Baba/Baba", "Dozing_Elderly") })
            {
                var holder = new GameObject("holder").transform;
                var model = CbModel.Load(path, 1.75f, holder);
                Assert.IsNotNull(model, $"{path} did not load (legacy animation, bones)");
                Assert.AreEqual(9, model.ClipCount, $"{path} is missing animation clips");
                model.Show(clip, 0.5f);
                Object.Destroy(holder.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator OpeningSceneCanBeSkipped()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.PlayOpening();
            yield return Frames(10);
            Assert.AreEqual(CbGame.Mode.Scene, game.mode);
            bool skipped = false;
            game.TestInput = i => { if (!skipped) { i.pause = true; skipped = true; } };
            yield return Frames(3);
            game.TestInput = null;
            Assert.AreEqual(CbGame.Mode.Play, game.mode, "SKIP did not end the scene");
            Assert.IsNull(game.transform.Find("Opening"), "The scene's cast was left on the path after SKIP");
            // a later run starts straight in the stage
            game.StartGame();
            Assert.AreEqual(CbGame.Mode.Play, game.mode);
        }

        [UnityTest]
        public IEnumerator ShieldHyenaHasItsModelAndClips()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            var guard = new CbEnemy(game, CbEnemy.Kind.Shield, 0, game.transform);
            Assert.IsNotNull(guard.model, "The shield hyena's model did not load (Resources/Cloudborne/HyenaGuard)");
            Assert.AreEqual(8, guard.model.ClipCount, "The shield hyena's model is missing animation clips");
            Assert.IsNotNull(guard.f.shield, "The shield hyena has no shield");
            guard.f.Destroy();
        }

        [UnityTest]
        public IEnumerator RajaFacesTheWayHeIsGoing()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            Assert.IsNotNull(game.hero.model, "Raja's model did not load");
            // in profile: his face points along the run (within about 30 degrees), running and standing alike
            foreach (int dir in new[] { 1, -1, 1 })
            {
                game.TestInput = i => i.x = dir;
                yield return new WaitForSeconds(0.6f);
                var face = game.hero.model.Facing;
                Assert.Greater(face.x * dir, 0.85f, $"Running {(dir > 0 ? "right" : "left")}, Raja faces {face}");
            }
            game.TestInput = i => i.x = 0;
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(CbHero.State.Idle, game.hero.state);
            Assert.Greater(game.hero.model.Facing.x, 0.85f, $"Standing after running right, Raja faces {game.hero.model.Facing}");
        }

        [UnityTest]
        public IEnumerator CrouchesAndSweeps()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            bool sweep = false;
            game.TestInput = i => { i.y = -1; i.atk = sweep; sweep = false; };
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(game.hero.Ducking, "Down on the stick did not make Raja duck");
            sweep = true;
            yield return Frames(2);
            Assert.AreEqual(CbHero.State.Atk, game.hero.state, "Crouch + HIT did not start the sweep");
            game.TestInput = null;
            yield return new WaitForSeconds(0.8f);
            Assert.IsFalse(game.hero.Ducking, "Raja stayed down after letting go");
        }

        [UnityTest]
        public IEnumerator GuardStopsBlowsFromTheFront()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            game.TestInput = i => i.guard = true;
            yield return Frames(3);
            Assert.IsTrue(game.hero.Guarding, "Holding GUARD did not raise Raja's guard");
            float hp = game.hero.hp;
            // a blow from in front pushes him the way he is not facing
            game.hero.TakeHit(10, -game.hero.facing);
            Assert.AreEqual(hp, game.hero.hp, "A blow from the front got through the guard");
            Assert.IsTrue(game.hero.Guarding, "Blocking a blow dropped the guard");
            // from behind it still hurts
            game.hero.TakeHit(10, game.hero.facing);
            Assert.Less(game.hero.hp, hp, "A blow from behind was blocked too");
            game.TestInput = null;
        }

        [UnityTest]
        public IEnumerator StaffTechniquesBeforeTheFireCrystal()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            Assert.IsFalse(CbHero.FireAwake, "Raja's fire should sleep until the fire crystal");

            // KI: the tiger lunge, forward with the staff out; no fireball
            game.hero.ki = 100;
            float x0 = game.hero.x;
            bool pressed = false, sawLunge = false;
            game.TestInput = i => { if (!pressed) { i.kiTap = true; pressed = true; } };
            for (float w = 0; w < 0.6f; w += Time.deltaTime)
            {
                yield return null;
                if (game.hero.move == CbHero.Move.Lunge) sawLunge = true;
                Assert.IsNull(GameObject.Find("fireball"), "KI shot a fireball before the fire crystal");
            }
            game.TestInput = null;
            Assert.IsTrue(sawLunge, "KI did not lunge with the staff");
            Assert.Greater(game.hero.x, x0 + 2f, "The lunge did not carry Raja forward");
            Assert.Less(game.hero.ki, 100f, "The lunge took no ki");
            yield return new WaitForSeconds(0.5f);

            // HIT, HIT, KI: the staff cyclone (pressed by state, so a slow frame rate can't skip a press)
            game.Teleport(0f);   // well short of the first fight
            game.hero.ki = 100;
            float t0 = Time.time;
            int hits = 0;
            bool sawCyclone = false;
            game.TestInput = i =>
            {
                if (hits == 0) { i.atk = true; hits = 1; return; }
                if (hits == 1 && game.hero.state == CbHero.State.Atk && Time.time - t0 > 0.08f) { i.atk = true; hits = 2; }
                if (game.hero.state == CbHero.State.Atk && game.hero.Combo >= 1) i.kiTap = true;
            };
            for (float w = 0; w < 1.5f && !sawCyclone; w += Time.deltaTime)
            {
                yield return null;
                if (game.hero.move == CbHero.Move.Cyclone) sawCyclone = true;
                Assert.AreNotEqual(CbHero.Move.FireWhirl, game.hero.move, "HIT, HIT, KI made fire before the fire crystal");
            }
            game.TestInput = null;
            Assert.IsTrue(sawCyclone, "HIT, HIT, KI did not sweep the staff round him");
            yield return new WaitForSeconds(1f);

            // the finisher + KI: the staff crash, a hop and the slam
            game.Teleport(0f);   // well short of the first fight
            game.hero.ki = 100;
            t0 = Time.time;
            int presses = 0;
            bool sawCrash = false;
            game.TestInput = i =>
            {
                float tt = Time.time - t0;
                if (presses < 4 && tt > presses * 0.12f) { i.atk = true; presses++; }
                if (game.hero.state == CbHero.State.Atk && game.hero.Combo == 3) i.kiTap = true;
            };
            for (float w = 0; w < 2f && !sawCrash; w += Time.deltaTime)
            {
                yield return null;
                if (game.hero.move == CbHero.Move.Slam || game.hero.move == CbHero.Move.Land) sawCrash = true;
                Assert.AreNotEqual(CbHero.Move.BlazePalm, game.hero.move, "The finisher + KI made fire before the fire crystal");
            }
            game.TestInput = null;
            Assert.IsTrue(sawCrash, "The finisher + KI did not bring the staff crashing down");
            yield return new WaitForSeconds(1f);

            // SPECIAL: the staff storm, which runs its course and ends
            game.Teleport(0f);   // well short of the first fight
            game.hero.ki = 100;
            pressed = false;
            bool sawStorm = false;
            game.TestInput = i => { if (!pressed) { i.skill = true; pressed = true; } };
            for (float w = 0; w < 1.8f; w += Time.deltaTime)
            {
                yield return null;
                if (game.hero.state == CbHero.State.Special) sawStorm = true;
            }
            game.TestInput = null;
            Assert.IsTrue(sawStorm, "SPECIAL did not start the staff storm");
            Assert.AreNotEqual(CbHero.State.Special, game.hero.state, "The staff storm never ended");
            Assert.Less(game.hero.ki, 70f, "The staff storm took no ki");
        }

        [UnityTest]
        public IEnumerator HitsThenFireMakeAFireWhirl()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            // the fire moves come with the fire crystal
            CbSave.FireAwake = true;
            game.hero.ki = 100;
            float t0 = Time.time;
            int hits = 0;
            bool sawWhirl = false, sawPalm = false;
            // HIT, HIT (pressed during the first blow, so it chains), then FIRE once the second blow is out;
            // counted by presses and states, not by time, so a slow frame rate can't skip a press
            game.TestInput = i =>
            {
                if (hits == 0) { i.atk = true; hits = 1; return; }
                if (hits == 1 && game.hero.state == CbHero.State.Atk && Time.time - t0 > 0.08f) { i.atk = true; hits = 2; }
                if (game.hero.state == CbHero.State.Atk && game.hero.Combo >= 1) i.kiTap = true;
            };
            for (float waited = 0; waited < 1.5f && !sawWhirl; waited += Time.deltaTime)
            {
                yield return null;
                if (game.hero.move == CbHero.Move.FireWhirl) sawWhirl = true;
            }
            Assert.IsTrue(sawWhirl, "HIT, HIT, FIRE did not start the fire whirl");
            // the flames wheel round him for the whole whirl and take ki
            Assert.Less(game.hero.ki, 100f, "The fire whirl took no ki");
            game.TestInput = null;
            yield return new WaitForSeconds(1f);
            // after the four-hit combo, FIRE is the blazing palm
            game.hero.ki = 100;
            t0 = Time.time;
            int presses = 0;
            game.TestInput = i =>
            {
                float tt = Time.time - t0;
                if (presses < 4 && tt > presses * 0.12f) { i.atk = true; presses++; }
                if (game.hero.state == CbHero.State.Atk && game.hero.Combo == 3) i.kiTap = true;
            };
            for (float waited = 0; waited < 2f && !sawPalm; waited += Time.deltaTime)
            {
                yield return null;
                if (game.hero.move == CbHero.Move.BlazePalm) sawPalm = true;
            }
            game.TestInput = null;
            Assert.IsTrue(sawPalm, "The combo finisher + FIRE did not make the blazing palm");
        }

        [UnityTest]
        public IEnumerator PickupsHealAndCratesBreak()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            Assert.Greater(game.items.CrateCount, 0, "The stage has no crates");
            // a mango where Raja stands heals him
            game.hero.hp = 50;
            game.items.Spawn(CbItems.Kind.Mango, new Vector3(game.hero.x, 0.6f, 0), resting: true);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(50 + CbItems.MangoHeal, game.hero.hp, 0.01f, "The mango did not heal Raja");
            // two blows break the first crate (at 9 m) and what was in it pops out
            game.Teleport(7.9f);
            game.hero.facing = 1;
            int crates = game.items.CrateCount, things = game.items.ItemCount;
            int presses = 0;
            float t0 = Time.time;
            game.TestInput = i => { if (presses < 4 && Time.time - t0 > presses * 0.3f) { i.atk = true; presses++; } };
            for (float waited = 0; waited < 2f && game.items.CrateCount == crates; waited += Time.deltaTime) yield return null;
            game.TestInput = null;
            Assert.AreEqual(crates - 1, game.items.CrateCount, "Blows did not break the crate");
            Assert.Greater(game.items.ItemCount, things, "The broken crate held nothing");
        }

        [UnityTest]
        public IEnumerator WallsLedgesAndTheStaff()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            var level = game.level;
            float wallLeft = CbLevel.WallX - 0.65f - CbLevel.HalfWidth;
            // the cracked wall stops him on the path, and is taller than a double jump
            Assert.LessOrEqual(level.BlockX(57f, 60f, 0f), wallLeft + 0.001f, "Raja walked into the cracked wall");
            Assert.LessOrEqual(level.BlockX(57f, 60f, 3.9f), wallLeft + 0.001f, "A double jump clears the cracked wall");
            // paws and kicks clang off it; two staff blows break it and the way is open
            level.Strike(wallLeft, 0f, 1, 1.7f, staff: false);
            Assert.IsTrue(level.WallStanding, "A paw broke the cracked wall");
            level.Strike(wallLeft, 0f, 1, 2.6f, staff: true);
            level.Strike(wallLeft, 0f, 1, 2.6f, staff: true);
            Assert.IsFalse(level.WallStanding, "Two staff blows did not break the cracked wall");
            Assert.AreEqual(60f, level.BlockX(57f, 60f, 0f), 0.001f, "The broken wall still blocks the path");
            // a ledge is landed on from above and jumped through from below
            Assert.AreEqual(3.9f, level.SupportAt(105f, 4.5f), 0.001f, "Raja does not land on the first ledge");
            Assert.AreEqual(0f, level.SupportAt(105f, 3f), 0.001f, "The ledge stops him jumping up through it");
            // the hero really stands on it: dropped above it, he comes to rest on top
            game.SkipFights();
            game.hero.y = 4.6f;
            game.hero.vy = 0;
            game.hero.x = 105f;
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(3.9f, game.hero.y, 0.05f, "Raja fell through the ledge");
        }

        [UnityTest]
        public IEnumerator StreamsBridgesAndTheRiver()
        {
            foreach (var m in new[] { "RopeBridge", "StoneBridge", "Log", "SteppingStone" })
                Assert.IsNotNull(Resources.Load<GameObject>($"Cloudborne/Jungle/{m}/{m}"), $"The {m} model is missing");
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var level = game.level;
            // over a stream there is nothing to stand on...
            Assert.Less(level.SupportAt(64.15f, 1f), -1f, "There is ground in the river between the stone and the log");
            // ...but the rope bridge's planks hold him, walked onto from the bank and sagging in the middle
            float mid = level.SupportAt(31f, 0f);
            Assert.That(mid, Is.InRange(-0.3f, 0f), "The rope bridge does not hold him in the middle");
            Assert.That(level.SupportAt(28.1f, 0f), Is.InRange(0f, 0.3f), "The rope bridge can't be walked onto from the bank");
            // stepping stones and the log are landed on from above, not walked onto from the bank
            Assert.Greater(level.SupportAt(63.3f, 1f), 0.1f, "The stepping stone does not hold him");
            Assert.Less(level.SupportAt(63.3f, 0f), -1f, "The stepping stone is walked onto without a jump");
            // the stone arch bridge is jumped onto and stood on
            Assert.AreEqual(1.64f, level.SupportAt(94.6f, 2f), 0.001f, "The stone bridge does not hold him");

            // falling in: a splash, health lost, and back on the bank he came from
            float hp0 = game.hero.hp;
            game.Teleport(64.15f);
            game.hero.y = 1f;
            game.hero.vy = 0;
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(CbLevel.Waters[1].x0 - 0.8f, game.hero.x, 0.3f, "Falling into the river did not put Raja back on its near bank");
            Assert.AreEqual(0f, game.hero.y, 0.05f);
            Assert.AreEqual(hp0 - CbHero.WaterDamage, game.hero.hp, 0.01f, "Falling into the river cost no health");
            // dropped onto the log he rides it, dipping a little
            game.Teleport(65.4f);
            game.hero.y = 1.2f;
            game.hero.vy = 0;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(game.hero.y, 0.1f, "Raja sank through the log");
            Assert.AreEqual(65.4f, game.hero.x, 0.2f);
            Assert.AreEqual(hp0 - CbHero.WaterDamage, game.hero.hp, 0.01f, "Standing on the log cost health");
        }

        [UnityTest]
        public IEnumerator DoubleJumpIsADoubleSomersault()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            // a steady 60 fps, so no frame turns him so far that the angle wraps round
            Time.captureDeltaTime = 1f / 60f;
            int presses = 0;
            game.TestInput = i => { if (presses < 2 && (presses == 0 || game.hero.vy < 4)) { i.jump = true; presses++; } };
            // add up how far the somersault turns him, frame by frame
            var flip = game.hero.f.flip;
            float turned = 0, last = 0;
            for (float waited = 0; waited < 1.5f; waited += Time.deltaTime)
            {
                yield return null;
                float a = flip.localEulerAngles.z;
                turned += Mathf.Abs(Mathf.DeltaAngle(last, a));
                last = a;
            }
            game.TestInput = null;
            Assert.AreEqual(2, presses, "The second jump was not made");
            Assert.That(turned, Is.InRange(700f, 740f), $"The double jump turned him {turned:0} degrees, not two full somersaults");
        }

        [UnityTest]
        public IEnumerator DashAfterimagesAreRajaSized()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            bool sent = false;
            game.TestInput = i => { if (!sent) { i.dash = true; sent = true; } };
            // the dash leaves an afterimage every 0.04 s, each fading in 0.25 s: look at the first one
            MeshFilter ghost = null;
            for (float waited = 0; ghost == null && waited < 1f; waited += Time.deltaTime)
            {
                yield return null;
                foreach (var mf in game.transform.Find("VFX").GetComponentsInChildren<MeshFilter>())
                    if (mf.name == "afterimage") ghost = mf;
            }
            game.TestInput = null;
            Assert.IsNotNull(ghost, "The dash left no afterimage");
            // Without a graphics device (the CI runs headless) Unity does not skin meshes, so the copy comes out empty
            if (ghost.sharedMesh.vertexCount == 0 || ghost.sharedMesh.bounds.size == Vector3.zero)
                Assert.Ignore("No skinning without a graphics device: the afterimage's size can't be checked here");
            var raja = game.hero.model.skin.bounds.size;
            // the copy's own size: its mesh times its scale (a headless Unity leaves renderer bounds at zero)
            var size = Vector3.Scale(ghost.sharedMesh.bounds.size, ghost.transform.lossyScale);
            float ratio = size.magnitude / raja.magnitude;
            Assert.That(ratio, Is.InRange(0.6f, 1.6f), $"The afterimage is {ratio:0.00} times Raja's size ({size} vs {raja})");
        }

        [UnityTest]
        public IEnumerator EveryMoveWorksInTheFirstFight()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.Teleport(14f);
            yield return Frames(2);
            Assert.AreEqual(CbGame.Mode.Play, game.mode);

            // wait for the three hyenas of the first zone
            float waited = 0;
            while (game.enemies.Count < 3 && waited < 5f) { waited += Time.deltaTime; yield return null; }
            Assert.AreEqual(3, game.enemies.Count, "The first fight zone did not spawn three hyenas");
            foreach (var e in game.enemies)
                Assert.IsNotNull(e.model, "A hyena raider's model did not load (Resources/Cloudborne/HyenaRaider)");

            // one move every 0.8 s, timed in seconds so the test does not depend on the frame rate
            float t0 = Time.time, prev = -1;
            bool sawSpecial = false;
            game.TestInput = i =>
            {
                game.hero.hp = 100;   // keep Raja alive, this is about the moves
                float tt = Time.time - t0;
                int phase = (int)(tt / 0.8f) % 8;
                float pt = tt % 0.8f;
                bool At(float when) => pt >= when && (prev < when || prev > pt);
                if (game.enemies.Count > 0 && phase != 6 && phase != 7) i.x = Mathf.Sign(game.enemies[0].x - game.hero.x) * 0.6f;
                switch (phase)
                {
                    case 0: i.atk = At(0) || At(0.12f) || At(0.24f) || At(0.36f); break;    // paw combo
                    // hold HEAVY: the smash turns into the whirl. Waits for phase 0's combo, which runs about 0.3 s past it
                    case 1: i.heavy = At(0.35f); i.heavyDown = pt > 0.35f; break;
                    case 2: i.y = 1; i.heavy = At(0.1f); break;                             // launcher
                    case 3: i.dash = At(0.05f); i.heavy = At(0.12f); break;                 // dash, then thrust
                    case 4: i.jump = At(0); i.heavy = At(0.3f); break;                      // slam
                    case 5: game.hero.ki = 100; i.kiTap = At(0.1f) || At(0.5f); break;      // KI: staff lunges
                    case 6: i.guard = pt < 0.6f; break;                                     // guard
                    // special: the staff storm, pressed until it starts (a hyena behind him may have him reeling)
                    case 7: game.hero.ki = 100; i.skill = pt >= 0.1f && !sawSpecial; break;
                }
                prev = pt;
            };
            int startScore = game.score;
            float t = 0;
            bool sawStaff = false, sawWhirl = false, sawGuard = false;
            while (t < 25f && game.mode == CbGame.Mode.Play && game.lockZone != null || t < 1f)
            {
                t += Time.deltaTime;
                if (game.hero.state == CbHero.State.Staff) sawStaff = true;
                if (game.hero.move == CbHero.Move.Whirl) sawWhirl = true;
                if (game.hero.state == CbHero.State.Guard) sawGuard = true;
                if (game.hero.state == CbHero.State.Special) sawSpecial = true;
                yield return null;
            }
            game.TestInput = null;
            Assert.Greater(game.score, startScore, "No hit landed");
            Assert.IsTrue(sawStaff, "No staff move was used");
            Assert.IsTrue(sawWhirl, "Holding HEAVY did not whirl the staff");
            Assert.IsTrue(sawGuard, "GUARD did not raise Raja's guard");
            Assert.IsTrue(sawSpecial, "SPECIAL did not start the staff storm");
            Assert.IsNull(game.lockZone, "The first fight zone was not cleared in 25 seconds");
        }
    }
}
