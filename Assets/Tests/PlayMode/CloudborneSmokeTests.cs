using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cloudborne;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cloudborne.Tests
{
    /// <summary>
    /// Plays the Cloudborne prototype for real: starts a run, walks into the first fight zone and uses every move
    /// (paw combo, staff smash and whirl, launcher, thrust, slam, fireball, guard, special, dash). Any exception or
    /// Debug.LogError fails the test.
    /// </summary>
    public class CloudborneSmokeTests
    {
        CbGame game;

        // every test starts from a fresh save: no coins, upgrades or opening seen; the cutscenes off, as in the game
        // (a test of a cutscene itself turns them on)
        [SetUp]
        public void FreshSave()
        {
            CbSave.ResetProgress();
            CbStory.Cutscenes = false;
        }

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
            // each painting stays long enough to read its words (about 3 s here), and a tap moves on sooner; it shows
            // whole, its own shape, inside the screen, and moving in it never loses more than a little of it
            float least = 1;
            for (float waited = 0; !done && waited < 12f; waited += Time.unscaledDeltaTime)
            {
                game.comic.Update(Time.unscaledDeltaTime);
                // (a headless run may have no screen to measure)
                if (waited < 1f && game.comic.ScreenSize.y > 1 && game.comic.ShownSize.y > 1)
                {
                    var size = game.comic.ShownSize;
                    Assert.AreEqual(64f / 36f, size.x / size.y, 0.02f, "The painting is not shown in its own shape");
                    Assert.LessOrEqual(size.x, game.comic.ScreenSize.x + 0.5f, "The painting is wider than the screen");
                    Assert.LessOrEqual(size.y, game.comic.ScreenSize.y + 0.5f, "The painting is taller than the screen");
                }
                least = Mathf.Min(least, game.comic.Shown.width, game.comic.Shown.height);
                yield return null;
            }
            Assert.Greater(least, 0.9f, "Moving in, the cutscene cut away much of the painting");
            // the story's own moves in keep nearly all of it too
            foreach (var scene in new[] { CbStory.Prologue, CbStory.KodaBoast, CbStory.KodaTaken, CbStory.Journey, CbStory.FireWakes,
                         CbStory.CrystalTorn, CbStory.ShipInTheSky, CbStory.BabaLesson })
                foreach (var shot in scene)
                    if (shot.title == null) Assert.LessOrEqual(Mathf.Max(shot.from.z, shot.to.z), 1.5f, $"{shot.image} moves in too far");
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
            // near the gate he steps out, and the fight is on in a shut arena (his arena stands 14 m before it)
            game.Teleport(CbWorld.GoalX - 20f);
            for (float w = 0; w < 3f && game.boss == null; w += Time.deltaTime) yield return null;
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
            // no cheering: the gate opened and he walked on through it (the user)
            Assert.AreNotEqual(CbHero.State.Victory, game.hero.state, "Raja cheered at the shrine gate");
            Assert.Greater(game.hero.x, CbWorld.GoalX + 1.6f, "Raja did not walk through the shrine gate");
            // the results come in: the stars, the numbers counting up, the rank stamped in last
            for (float w = 0; w < 1.8f; w += Time.deltaTime) yield return null;
            Assert.IsTrue(game.hud.ResultsIn, "The results never finished coming in");
            Assert.AreNotEqual("", game.hud.Rank, "A cleared stage got no rank");
        }

        [UnityTest]
        public IEnumerator SnapBreaksAndFliesIntoARage()
        {
            // the first boss made more fun (the user): blows fill his break gauge and break him - dazed, taking more -
            // and half beaten he flies into a rage
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipToBoss();
            yield return Frames(2);
            game.Teleport(CbWorld.GoalX - 20f);
            for (float w = 0; w < 3f && game.boss == null; w += Time.deltaTime) yield return null;
            yield return Frames(3);
            var snap = game.boss;
            Assert.IsNotNull(snap, "Sergeant Snap did not appear");
            game.hero.hp = 9999;
            game.hero.inv = 999f;
            for (int k = 0; k < 20 && !snap.Stunned; k++)
            {
                game.HurtEnemy(snap, 12, 1, 2, 0, false, false, false, true);
                yield return Frames(2);
            }
            Assert.IsTrue(snap.Stunned, "Raja's blows never broke Snap");
            Assert.AreEqual(1, snap.Breaks);
            float was = snap.hp;
            game.HurtEnemy(snap, 10, 1, 2, 0, false, false, false, true);
            Assert.Greater(was - snap.hp, 10f * CbSave.DamageMul * 1.4f, "Broken, Snap took no more from a blow");
            // half beaten: once his daze wears off, the roar, and the rage
            snap.hp = snap.max * 0.45f;
            for (float w = 0; w < 8f && !snap.furious; w += Time.deltaTime) yield return null;
            Assert.IsTrue(snap.furious, "Half beaten, Snap never flew into a rage");
        }

        [UnityTest]
        public IEnumerator StageOnesSecretsCanBeFound()
        {
            // secrets worth playing a stage again for (the user): stage 1-1's hidden ledge over the start, up from a
            // springy leaf (bounced on, steered back left), and its cracked boulder, broken open
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            game.hero.inv = 999f;
            yield return Frames(2);
            // (one: the hidden ledge over the start, its tower and its springy leaf, are gone - the user)
            Assert.AreEqual(1, CbGame.SecretsInStage);
            var hero = game.hero;
            // the boulder: three blows and it breaks, its treasure inside
            game.Teleport(CbLevel.BoulderStashX - 1.2f);
            hero.y = 0; hero.facing = 1;
            yield return Frames(3);
            int crates = game.items.CrateCount;
            for (int k = 0; k < 3; k++) { game.items.HitCrates(hero.x, 0.5f, 1, 2f); yield return new WaitForSeconds(0.3f); }
            Assert.AreEqual(crates - 1, game.items.CrateCount, "Three blows did not break the cracked boulder");
            // (it pops out and lands near by: walked along beside it)
            yield return new WaitForSeconds(1.2f);
            for (float dx = -2.5f; dx <= 2.5f && game.Secrets < 1; dx += 0.4f)
            {
                game.Teleport(CbLevel.BoulderStashX + dx);
                hero.y = 0;
                yield return Frames(3);
            }
            Assert.AreEqual(1, game.Secrets, "The cracked boulder held no treasure");
            // stage 1-2's: pictures of the high branch over the vine gap and the old stump
            CbStage.Set("1-2");
            game.BuildStage();
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            yield return TakePictures(new[] { ("secret_vine", CbLevel.VineSecretX - 2f, 0f), ("secret_stump", CbLevel.StumpStashX - 3f, 0f) }, 1f);
            CbStage.Set("1-1");
        }

        [UnityTest]
        public IEnumerator TheFireRunInTheBurningJungle()
        {
            // stage 1-5's way in (it was plain path): fires on the path to jump, and the fire run - the jungle goes up
            // behind him and the fire comes on after him, burning branches crash down ahead, the temple's threshold
            // is safe
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-5");
            game.BuildStage();
            game.StartGame();
            yield return Frames(2);
            game.SkipFights();
            game.ArmFireRun();
            var hero = game.hero;
            hero.hp = 9999;
            // a fire on the path burns him, and throws him back
            hero.inv = 0;
            game.Teleport(CbLevel.EmberPatches[0] - 0.2f);
            hero.y = 0;
            yield return Frames(3);
            Assert.Less(hero.hp, 9999f, "Standing in the fire on the path did not burn Raja");
            Assert.Less(hero.x, CbLevel.EmberPatches[0] - 0.2f, "The fire did not throw him back");
            // the fire run: the fire rises behind him and comes on
            hero.inv = 999f;
            game.Teleport(CbLevel.FireRunFrom + 1f);
            hero.y = 0;
            yield return Frames(3);
            Assert.IsTrue(game.FireRunStarted, "No fire run past the raiders");
            Assert.IsTrue(game.level.FireChasing, "The jungle behind him did not go up");
            Assert.Less(game.level.FireChaseX, hero.x - 5f, "The fire rose on top of him, not behind");
            float fireWas = game.level.FireChaseX;
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(game.level.FireChaseX, fireWas + 2f, "The fire did not come on after him");
            // standing still, it catches him
            hero.inv = 0;
            hero.hp = 9999;
            for (float w = 0; w < 4f && hero.hp >= 9999f; w += Time.deltaTime) yield return null;
            Assert.Less(hero.hp, 9999f, "The fire behind him never caught him standing still");
            // a burning branch crashes down ahead of him as he comes near
            hero.inv = 999f;
            game.Teleport(CbLevel.EmberBranches[0] - 4f);
            hero.y = 0;
            for (float w = 0; w < 2.5f && game.level.BranchesDown == 0; w += Time.deltaTime) yield return null;
            Assert.AreEqual(1, game.level.BranchesDown, "The burning branch did not come down ahead of him");
            // the temple's threshold: safe, the fire dies down
            game.Teleport(CbLevel.FireRunTo + 0.5f);
            hero.y = 0;
            yield return Frames(3);
            Assert.IsTrue(game.FireRunEscaped, "Reaching the temple's threshold was no escape");
            Assert.IsFalse(game.level.FireChasing, "The fire went on past the temple's threshold");
            CbStage.Set("1-1");
        }

        [UnityTest]
        public IEnumerator TheShortRunsChaseAndThief()
        {
            // short runs for a change of pace (the user): in 1-1 a pack bursts in from behind - reaching the lantern,
            // he has escaped; in 1-2 a thief runs off with a sack - caught, it bursts
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            game.hero.inv = 999f;
            game.hero.hp = 9999;
            game.Teleport(CbGame.ChaseFrom + 1f);
            game.hero.y = 0;
            yield return Frames(3);
            // a boulder off the mountain behind him (the user: a boulder to run from, not raiders)
            Assert.IsTrue(game.ChaseStarted, "No run on the stretch after the gorge");
            Assert.IsTrue(game.level.BoulderRolling, "No boulder came down after Raja");
            float was = game.level.BoulderX;
            yield return new WaitForSeconds(1f);
            Assert.Greater(game.level.BoulderX, was + 3f, "The boulder did not roll after Raja");
            // out of its way, he watches it smash the fallen trunks and go into the river
            float logX = CbLevel.LowLogXs[0];
            game.Teleport(CbGame.ChaseFrom - 6f);
            game.hero.y = 0;
            for (float w = 0; w < 12f && !game.level.BoulderSank; w += Time.deltaTime) yield return null;
            Assert.IsTrue(game.level.BoulderSank, "The boulder never went into the river");
            Assert.IsTrue(float.IsPositiveInfinity(game.level.CeilingAt(logX, 0f)), "The boulder did not smash the fallen trunk in its way");
            yield return Frames(3);
            Assert.IsTrue(game.ChaseEscaped, "Never caught by the boulder, he had not escaped");
            // 1-2: the thief
            CbStage.Set("1-2");
            game.BuildStage();
            game.StartGame();
            yield return Frames(2);
            game.hero.inv = 999f;
            game.Teleport(CbGame.ThiefFrom + 1f);
            game.hero.y = 0;
            yield return Frames(3);
            Assert.IsTrue(game.ThiefStarted, "No thief on the bough");
            var thief = game.Thief;
            Assert.IsNotNull(thief);
            Assert.Greater(thief.x, game.hero.x + 3f, "The thief did not start ahead of Raja");
            was = thief.x;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(thief.x, was + 1.5f, "The thief did not run off");
            yield return TakePictures(new[] { ("run_thief", float.NaN, 0f) }, 0.3f);
            int items = game.items.ItemCount;
            game.HurtEnemy(thief, 40, 1, 2, 0, false, false, false, true);
            yield return Frames(3);
            Assert.IsTrue(game.ThiefCaught, "Struck down, the thief was not caught");
            Assert.Greater(game.items.ItemCount, items + 5, "The thief's sack did not burst");
            CbStage.Set("1-1");
        }

        [UnityTest]
        public IEnumerator ZzResultsComeIn()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            // S for every star and no hit, A for every star (or two and no hit), B for two, C else
            Assert.AreEqual("S", CbHud.RankOf(3, true));
            Assert.AreEqual("A", CbHud.RankOf(3, false));
            Assert.AreEqual("A", CbHud.RankOf(2, true));
            Assert.AreEqual("B", CbHud.RankOf(2, false));
            Assert.AreEqual("C", CbHud.RankOf(1, true));
            game.hud.ShowResult(true, 12840, 23, 251f, 3, 2, 3, false);
            yield return TakePictures(new[] { ("results", float.NaN, 0f) }, 1.8f);
            Assert.IsTrue(game.hud.ResultsIn, "The results never finished coming in");
            Assert.AreEqual("A", game.hud.Rank, "Every star with a hit taken is not an A");
        }

        [UnityTest]
        public IEnumerator TheScrollsLedgesAreThereFromTheStart()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            // above the gorge's summit two ledges lead up to the scroll: there all along (they popped up as he came
            // near, which looked wrong: the user)
            Assert.AreEqual(13.4f, game.level.SupportAt(156.6f, 14f), 0.001f, "The first ledge to the scroll is not there");
            Assert.AreEqual(15.4f, game.level.SupportAt(153.6f, 16f), 0.001f, "The second ledge to the scroll is not there");
        }

        [UnityTest]
        public IEnumerator BuildsTheJungleAndRaja()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            Assert.AreEqual(CbGame.Mode.Title, game.mode);
            Assert.IsNotNull(game.hero.f.root, "Raja was not built");
            Assert.IsNotNull(game.hero.model, "Raja's model did not load (Resources/Cloudborne/Raja, legacy animation)");
            // 42 in his own file (the slide among them), the dive kick and a first crawl made later on a new rig of the
            // same model (Raja_Clips: its own copy of the slide is not added twice), and the crawl on hands and knees,
            // hanging from a bar and going along it (Raja_More_Clips), the backflip over a spear (Raja_Dodge_Clips), and the
            // six the user chose in Meshy's library (Raja_Climb_Clips: the flying fist, the spell cast, the hang and climb
            // left and right, the wall flip, the jump and hang on a bar)
            foreach (var clip in new[] { "Flying_Fist_Kick", "mage_soell_cast_3", "Hang_and_Climb_Left", "Hang_and_Climb_Rght", "Wall_Flip", "Jump_and_Hang_on_Bar" })
                Assert.IsTrue(game.hero.model.HasClip(clip), $"Raja has no {clip} clip");
            Assert.AreEqual(53, game.hero.model.ClipCount, "Raja's model is missing animation clips");
            // from Meshy's animation library: catching an edge, pulling up over it, treading water, getting up, cheering;
            // hanging from a vine, climbing it, along a bar, pushing, walking the log; the dive kick from a description
            foreach (var clip in new[] { "Jump_and_Grab_Wall", "Ladder_Climb_Finish", "Swim_Idle", "Stand_Up1", "Victory_Cheer",
                "Rope_Hang_Idle", "Climb_Up_Rope", "Slow_Bar_Hang_Left", "Push_and_Walk_Forward", "Tightrope_Walk", "Motion_Divekick", "slide_light", "Crawl", "Bar_Hang_Idle", "MonkeyBars" })
                Assert.IsTrue(game.hero.model.HasClip(clip), $"Raja's model has no {clip} clip");
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
            // (a cutscene: turned on for this test)
            CbStory.Cutscenes = true;
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
            bool sawKoda = false, sawRaiders = false, sawNet = false, sawBaba = false, sawKids = false, scarfTied = false;
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
                sawBaba |= stage.Find("Baba") != null;
                sawKids |= stage.Find("Kid") != null;
                scarfTied |= game.hero.Staff != null && game.hero.Staff.Find("kodaScarf") != null;
            }
            game.TestInput = null;
            Time.captureDeltaTime = 0f;
            // objects are removed at the end of the frame
            yield return Frames(2);
            Assert.AreEqual(CbGame.Mode.Play, game.mode, "The opening never handed over to the stage");
            Assert.IsTrue(sawKoda, "Koda was not in the opening");
            Assert.IsTrue(sawRaiders, "Sergeant Snap and his raiders never came");
            Assert.IsTrue(sawNet, "Koda was never netted");
            // the opening is a cinematic in the engine now (the user): the village kids, Baba's word, Koda's scarf tied
            // to the staff
            Assert.IsTrue(sawKids, "The village kids were not in the opening");
            Assert.IsTrue(sawBaba, "Baba was not in the opening");
            Assert.IsTrue(scarfTied, "Raja never tied Koda's scarf to his staff");
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

            // PLAY: the stage (the cutscenes off), with 20 more health
            game.PlayStage("1-1");
            yield return WaitForMode(CbGame.Mode.Play, 6f);
            Assert.IsFalse(game.menu.Visible);
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
                         "koda_boast", "koda_taken", "raja_angry", "raja_journey", "baba_lesson1", "baba_lesson2", "cub_clouds",
                         "crystal_torn", "stripes_ignite", "ship_in_sky" })
            {
                var tex = Resources.Load<Texture2D>("Cloudborne/Story/" + name);
                Assert.IsNotNull(tex, $"The painting {name} is missing (Resources/Cloudborne/Story)");
                Assert.AreEqual(1680, tex.width, $"The painting {name} was resized on import");
            }
            Assert.IsTrue(CbComic.Available(CbStory.KodaBoast), "Koda's boast can't be shown");
            Assert.IsTrue(CbComic.Available(CbStory.KodaTaken), "Koda being taken can't be shown");
            Assert.IsTrue(CbComic.Available(CbStory.Journey), "Raja getting ready for the road can't be shown");
            Assert.IsTrue(CbComic.Available(CbStory.Prologue), "The prologue can't be shown");
            Assert.IsTrue(CbComic.Available(CbStory.BabaLesson), "Baba's lesson can't be shown");
            Assert.IsTrue(CbComic.Available(CbStory.CubInTheClouds), "The drawing behind Baba's scroll can't be shown");
            Assert.IsTrue(CbComic.Available(CbStory.CrystalTorn), "Grak tearing out the crystal can't be shown");
            Assert.IsTrue(CbComic.Available(CbStory.FireWakes), "Raja's fire waking can't be shown");
            Assert.IsTrue(CbComic.Available(CbStory.ShipInTheSky), "The end of chapter 1 can't be shown");
        }

        [UnityTest]
        public IEnumerator PaintedKitIsInTheProject()
        {
            // drawn in Canva and approved: grass and flower cards, the far jungle layer, the path, the water, the cliffs
            foreach (var n in new[] { "Grass1", "Grass2", "Grass3", "Grass4", "Grass5", "Flower1", "Flower2", "Flower3", "Flower4", "Flower5" })
            {
                var card = Resources.Load<Texture2D>("Cloudborne/Jungle/Cards/" + n);
                Assert.IsNotNull(card, $"The {n} card is missing");
            }
            foreach (var t in new[] { "Backdrop/JungleMid", "Backdrop/JungleNear", "Ground/Path", "Water/Water", "Cliff/RockFace" })
                Assert.IsNotNull(Resources.Load<Texture2D>("Cloudborne/Jungle/" + t), $"The painted {t} is missing");
            foreach (var m in new[] { "Banyan", "Kapok", "Bamboo" })
                Assert.IsNotNull(Resources.Load<GameObject>($"Cloudborne/Jungle/{m}/{m}"), $"The {m} tree is missing");
            // the long stage's things drawn in Canva, approved and built by Meshy
            foreach (var m in new[] { "Lantern", "Cage", "Raft", "Statue", "Gate", "Lever", "Leaf", "Chest",
                "LogRam", "BambooBar", "Thorns", "PushBlock", "ZipPost" })
                Assert.IsNotNull(Resources.Load<GameObject>($"Cloudborne/Jungle/Ways/{m}/{m}"), $"The {m} model is missing");
            Assert.IsNotNull(Resources.Load<GameObject>("Cloudborne/Items/Key/Key"), "The key model is missing");
            foreach (var m in new[] { "Terrace", "Pillar", "Ledge", "CrumblingLedge" })
                Assert.IsNotNull(Resources.Load<GameObject>($"Cloudborne/Jungle/Cliff/{m}/{m}"), $"The cliff kit's {m} is missing");
            var shader = Resources.Load<Shader>("Shaders/PaintedCutout");
            Assert.IsNotNull(shader, "The cut-out shader is missing");
            // the streams' moving, see-through front
            Assert.IsNotNull(Resources.Load<Shader>("Shaders/WaterFront"), "The water-front shader is missing");
            game = CbGame.Boot();
            yield return Frames(3);
            // the mountain: terraces of rock (not blocks piled up), ledges of the kit
            var level = game.transform.Find("Level");
            Assert.IsNotNull(level);
            // no wall of rock behind the climb: its terraces stand against the jungle, natural rocks (the painted boxes
            // didn't look like real rock: the user)
            Assert.IsNull(level.Find("mountainBody"), "A wall of rock still fills the background behind the climb");
            var face = level.Find("terrace/rock");
            Assert.IsNotNull(face, "A terrace has no rock");
            var rockSkin = face.GetComponentInChildren<MeshRenderer>().sharedMaterial.mainTexture.name;
            Assert.That(rockSkin, Is.EqualTo("CliffRock_Albedo").Or.EqualTo("RiverRock_Albedo"), "The terraces are not the modelled rocks");
            foreach (var m in new[] { "RiverStone", "RiverRock", "CrackedRock", "CliffRock" })
                Assert.IsNotNull(Resources.Load<GameObject>($"Cloudborne/Jungle/{m}/{m}"), $"The {m} model is missing");
            int rock = 0, kitLedges = 0;
            foreach (Transform t in level)
            {
                // bare rock, no band of grass along the top
                if (t.name == "terrace" && t.Find("rock") != null && t.Find("lip") == null) rock++;
                // (a crumbling one the cliff kit's slab; a solid one the cliff kit's slab or a hanging platform)
                if ((t.name == "ledge" || t.name == "crumblingLedge") && (t.Find("fit") != null || t.Find("woodPlatform") != null)) kitLedges++;
            }
            // the long stage's vine pillar and waterfall cliff (the ridge and its shaft are gone; the climb is a gorge:
            // its cliffs are built of rock in rows, CbLevelGorge)
            Assert.AreEqual(2, rock, "The terraces are not bare rock");
            Assert.Greater(kitLedges, 5, "The gorge's ledges are not the kits' slabs and pillars");
            int cliffs = 0;
            foreach (Transform t in level) if ((t.name == "cliff" || t.name == "gorgeGate") && t.Find("piece") != null) cliffs++;
            Assert.AreEqual(3, cliffs, "The gorge's cliffs are not built of rock");
            // with the painted jungle behind the path, no modelled trees stand in front of it (faceted crowns)
            var treeSkins = new[] { "Tree_Albedo", "Banyan_Albedo", "Kapok_Albedo", "Bamboo_Albedo" };
            foreach (var r in game.world.root.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.mainTexture != null)
                        Assert.IsFalse(treeSkins.Contains(m.mainTexture.name), $"A modelled tree ({m.mainTexture.name}) still stands in the jungle");
            // the painted layer stands behind the path wherever the shader can draw
            if (shader.isSupported) Assert.IsNotNull(GameObject.Find("JungleMid"), "The far jungle layer was not put up");
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
        public IEnumerator RajaSlidesUnderALowPassage()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var level = game.level;
            float x = CbLevel.TrunkX, near = x - 0.35f - CbLevel.HalfWidth;
            // standing, the fallen trunk stops him; low, he passes under it
            Assert.Less(level.BlockX(x - 3f, x + 1f, 0f), near + 0.01f, "The low passage does not stop him standing");
            Assert.AreEqual(x + 1f, level.BlockX(x - 3f, x + 1f, 0f, CbLevel.LowHeight), 0.001f, "The low passage stops him even low");
            // more of them along the stage
            foreach (var lx in CbLevel.LowLogXs)
            {
                Assert.Less(level.BlockX(lx - 3f, lx + 1f, 0f), lx - 0.35f - CbLevel.HalfWidth + 0.01f, $"The trunk at {lx} does not stop him standing");
                Assert.AreEqual(lx + 1f, level.BlockX(lx - 3f, lx + 1f, 0f, CbLevel.LowHeight), 0.001f, $"The trunk at {lx} stops him even low");
            }
            // running at it, down: a slide carries him under (on until he is out from under it: no crawl, the user)
            game.Teleport(x - 6f);
            game.hero.y = 0;
            float t0 = Time.time;
            game.TestInput = i => { i.x = 1; if (Time.time - t0 > 0.5f) i.y = -1; };
            bool slid = false;
            for (float w = 0; w < 4f && game.hero.x < x + 1.2f; w += Time.deltaTime)
            {
                slid |= game.hero.state == CbHero.State.Slide;
                yield return null;
            }
            game.TestInput = null;
            Assert.IsTrue(slid, "Running, down did not make Raja slide");
            Assert.Greater(game.hero.x, x + 1f, "Raja did not get under the low passage");
        }

        [UnityTest]
        public IEnumerator ASleepingSoldierLooksAsleepAndWakesWithAStart()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            game.Teleport(60f);
            yield return Frames(2);
            // the user: he didn't look asleep. He dozes sitting, his eyes shut, a snot bubble and Zs rising from him
            var e = new CbEnemy(game, CbEnemy.Kind.Hyena, 66f, game.transform) { asleep = true };
            game.enemies.Add(e);
            Assert.IsNotNull(e.model, "The raider's model did not load");
            Assert.IsTrue(e.model.HasClip("Sit_and_Doze_Off"), "The raider has no clip to doze with");
            Assert.IsNotNull(Resources.Load<Texture2D>("Cloudborne/HyenaRaider/HyenaRaider_Asleep"), "No shut-eyed texture for the raider");
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(e.asleep, "He woke with Raja still far away");
            Assert.AreEqual("HyenaRaider_Asleep", e.model.material.mainTexture.name, "His eyes are not shut");
            Assert.IsNotNull(GameObject.Find("snore"), "No Zs rise from him");
            // Raja beside him: he wakes, his eyes open, and he jumps up with a start
            game.Teleport(64.5f);
            yield return Frames(2);
            Assert.IsFalse(e.asleep, "Raja beside him did not wake him");
            Assert.AreEqual("HyenaRaider_Albedo", e.model.material.mainTexture.name, "His eyes stayed shut");
            float top = 0;
            for (float t = 0; t < 0.6f; t += Time.deltaTime) { top = Mathf.Max(top, e.y); yield return null; }
            Assert.Greater(top, 0.3f, "He did not jump up with a start");
        }

        [UnityTest]
        public IEnumerator ShieldHyenaHasItsModelAndClips()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            var guard = new CbEnemy(game, CbEnemy.Kind.Shield, 0, game.transform);
            Assert.IsNotNull(guard.model, "The shield hyena's model did not load (Resources/Cloudborne/HyenaGuard)");
            Assert.AreEqual(11, guard.model.ClipCount, "The shield hyena's model is missing animation clips");
            // a charge from afar and a step back after a blow (they walked like zombies)
            foreach (var clip in new[] { "Standard_Forward_Charge_inplace", "Walk_Backward_with_Sword_Shield" })
                Assert.IsTrue(guard.model.HasClip(clip), $"The shield hyena has no {clip} clip");
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
        public IEnumerator HitInTheAirIsADiveKick()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var hero = game.hero;
            float x0 = hero.x;
            bool jumped = false, kicked = false;
            game.TestInput = i =>
            {
                if (!jumped) { i.jump = true; jumped = true; }
                else if (!kicked && hero.y > 1.2f) { i.atk = true; kicked = true; }
            };
            bool sawKick = false;
            for (float w = 0; w < 2f; w += Time.deltaTime)
            {
                sawKick |= hero.state == CbHero.State.Kick;
                yield return null;
            }
            game.TestInput = null;
            Assert.IsTrue(sawKick, "HIT in the air did not start the dive kick");
            Assert.AreEqual(0f, hero.y, 0.05f, "The dive kick did not bring him down to the ground");
            Assert.Greater((hero.x - x0) * hero.facing, 1.5f, "The dive kick did not carry him forward");
            Assert.AreNotEqual(CbHero.State.Kick, hero.state, "The dive kick never ended");
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
            // (its cut-in slows the fight for a moment first)
            bool sawCutIn = false;
            for (float w = 0; w < 2.4f; w += Time.deltaTime)
            {
                yield return null;
                if (game.hero.state == CbHero.State.Special) sawStorm = true;
                sawCutIn |= game.hud.CuttingIn;
            }
            game.TestInput = null;
            Assert.IsTrue(sawStorm, "SPECIAL did not start the staff storm");
            Assert.IsTrue(sawCutIn, "SPECIAL showed no cut-in");
            Assert.IsFalse(game.hud.CuttingIn, "The cut-in stayed on the screen");
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
            // a crate is solid: walking into the first one (at 9 m) stops him at its side; he can stand on it
            game.Teleport(7f);
            game.TestInput = i => i.x = 1;
            yield return new WaitForSeconds(0.6f);
            game.TestInput = null;
            Assert.Less(game.hero.x, 9f - 0.44f - CbLevel.HalfWidth + 0.02f, "Raja walked through a crate");
            Assert.AreEqual(0.95f, game.items.SupportAt(9f, 1.2f), 0.01f, "A crate can't be stood on");
            // two blows break the first crate and what was in it pops out
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
            // HEAVY in a jump: the staff slam comes down beside the next crate and its shock bursts it at once
            game.SkipFights();
            var hero = game.hero;
            float crateX = game.items.CrateXs()[0];
            game.Teleport(crateX - 1.6f);
            hero.y = 2.5f;
            hero.vx = hero.vy = 0;
            hero.facing = 1;
            hero.state = CbHero.State.Air;
            crates = game.items.CrateCount;
            // (pressed once he is falling: on the very first frame he still counts as standing, and HEAVY is the smash)
            bool slammed = false;
            yield return Frames(3);
            game.TestInput = i => { if (!slammed && hero.vy < 0) { i.heavy = true; slammed = true; } };
            for (float waited = 0; waited < 2f && game.items.CrateCount == crates; waited += Time.deltaTime) yield return null;
            game.TestInput = null;
            Assert.AreEqual(crates - 1, game.items.CrateCount, "The slam from a jump did not break the crate beside it");
        }

        [UnityTest]
        public IEnumerator WallsLedgesAndTheStaff()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            var level = game.level;
            float wallLeft = CbLevel.WallX - 0.75f - CbLevel.HalfWidth;
            // the cracked wall stops him on the path, and is taller than a double jump
            Assert.LessOrEqual(level.BlockX(CbLevel.WallX - 3f, CbLevel.WallX, 0f), wallLeft + 0.001f, "Raja walked into the cracked wall");
            Assert.LessOrEqual(level.BlockX(CbLevel.WallX - 3f, CbLevel.WallX, 3.9f), wallLeft + 0.001f, "A double jump clears the cracked wall");
            // paws and kicks clang off it; two staff blows break it and the way is open
            level.Strike(wallLeft, 0f, 1, 1.7f, staff: false);
            Assert.IsTrue(level.WallStanding, "A paw broke the cracked wall");
            level.Strike(wallLeft, 0f, 1, 2.6f, staff: true);
            level.Strike(wallLeft, 0f, 1, 2.6f, staff: true);
            Assert.IsFalse(level.WallStanding, "Two staff blows did not break the cracked wall");
            Assert.AreEqual(CbLevel.WallX, level.BlockX(CbLevel.WallX - 3f, CbLevel.WallX, 0f), 0.001f, "The broken wall still blocks the path");
            // the gorge's first ledge is landed on from above and jumped through from below
            Assert.AreEqual(2f, level.SupportAt(141.9f, 2.6f), 0.001f, "Raja does not land on the first ledge");
            Assert.AreEqual(0f, level.SupportAt(141.9f, 1f), 0.001f, "The ledge stops him jumping up through it");
            // the cliff across the path stops him standing, and he slides through the gap at its foot
            Assert.LessOrEqual(level.BlockX(135f, 138f, 0f), CbLevel.MountainFrom - CbLevel.HalfWidth + 0.001f, "Raja walked into the cliff");
            Assert.AreEqual(140.5f, level.BlockX(135f, 140.5f, 0f, CbLevel.LowHeight), 0.001f, "Raja can't slide through the gap under the cliff");
            Assert.AreEqual(CbLevel.SummitTop, level.SupportAt(154f, 12f), 0.001f, "The summit does not hold him");
            // the hero really stands on the ledge: dropped above it, he comes to rest on top
            game.SkipFights();
            game.hero.y = 2.6f;
            game.hero.vy = 0;
            game.hero.x = 141.9f;
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(2f, game.hero.y, 0.05f, "Raja fell through the ledge");
        }

        [UnityTest]
        public IEnumerator StreamsBridgesAndTheRiver()
        {
            foreach (var m in new[] { "RopeBridge", "StoneBridge", "LongBridge", "ClimbVine", "Log", "SteppingStone" })
                Assert.IsNotNull(Resources.Load<GameObject>($"Cloudborne/Jungle/{m}/{m}"), $"The {m} model is missing");
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var level = game.level;
            // the banks end in mossy stones and grass, not a square cut like a block in a building game (the user)
            Assert.Greater(level.BankStones, 20, "The river banks end in a square cut");
            // over a stream there is nothing to stand on (between two stones of the long river)...
            Assert.Less(level.SupportAt(96.3f, 1f), -1f, "There is ground in the river between the stones");
            // ...but the rope bridge's planks hold him, walked onto from the bank and sagging in the middle - all along
            // it (soldiers fight him on it: the user wanted it whole)
            var (p0, p1, _) = CbLevel.RopeSection(4);
            Assert.That(level.SupportAt((p0 + p1) / 2, 0f), Is.InRange(-0.7f, 0f), "The rope bridge does not hold him in the middle");
            Assert.That(level.SupportAt(CbLevel.RopeBridgeX - CbLevel.RopeDeckHalf + 0.1f, 0f), Is.InRange(0f, 0.3f), "The rope bridge can't be walked onto from the bank");
            for (int k = 0; k < CbLevel.RopeSections; k++)
            {
                var (s0, s1, gap) = CbLevel.RopeSection(k);
                Assert.IsFalse(gap, "The rope bridge has a gap in its planks");
                Assert.Greater(level.SupportAt((s0 + s1) / 2, 0f), -1f, "Raja falls through the rope bridge");
            }
            Assert.Greater(CbLevel.RopeDeckHalf * 2, 20f, "The rope bridge is short (a screen and a half: the user)");
            // a stepping stone is landed on from above, not climbed onto from the water
            Assert.That(level.SupportAt(97.4f, 1f), Is.InRange(-0.1f, 0.1f), "The stepping stone does not hold him");
            Assert.Less(level.SupportAt(97.4f, CbLevel.WaterLevel), -1f, "The stepping stone is climbed onto from the water");
            // the rock in the river and the stone arch bridge are stood on
            Assert.AreEqual(1.3f, level.SupportAt(89f, 2f), 0.001f, "The rock in the river does not hold him");
            Assert.AreEqual(CbLevel.StoneDeck, level.SupportAt(CbLevel.StoneBridgeX, 2f), 0.001f, "The stone bridge does not hold him");
            // half a screen long, walked on between its parapets (not along their tops: the user)
            Assert.GreaterOrEqual(CbLevel.StoneBridgeHalf * 2, 8f, "The stone bridge is short");
            Assert.Less(CbLevel.StoneDeck, 1.64f * 0.9f, "He walks along the stone bridge's parapet");

            // standing on a steady stone, then falling in: a splash, health lost, and back on that stone
            game.Teleport(97.4f);
            game.hero.y = 0.6f;
            game.hero.vy = 0;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0f, game.hero.y, 0.1f, "Raja did not land on the stepping stone");
            float hp0 = game.hero.hp;
            game.Teleport(96.3f);
            game.hero.y = 1f;
            game.hero.vy = 0;
            // in he goes: under, up, a moment treading water, under again, then dropped back onto the stone
            bool swam = false;
            for (float waited = 0; waited < 3.5f && !(swam && game.hero.state != CbHero.State.Swim && game.hero.state != CbHero.State.Air); waited += Time.deltaTime)
            {
                yield return null;
                swam |= game.hero.state == CbHero.State.Swim;
            }
            Assert.IsTrue(swam, "Raja did not tread water in the river");
            Assert.AreEqual(97.4f, game.hero.x, 0.3f, "Falling into the river did not put Raja back on the stone he stood on");
            Assert.AreEqual(hp0 - CbHero.WaterDamage, game.hero.hp, 0.01f, "Falling into the river cost no health");
            // a sinking stone goes under him after a moment, into the water he goes
            game.Teleport(95.2f);
            game.hero.y = 0.6f;
            game.hero.vy = 0;
            yield return new WaitForSeconds(3.4f);
            Assert.AreEqual(hp0 - 2 * CbHero.WaterDamage, game.hero.hp, 0.01f, "The pale stone did not sink under him");
            // (out of the water and back on his feet before he is moved on)
            for (float w = 0; w < 3f && (game.hero.state == CbHero.State.Swim || game.hero.state == CbHero.State.Air); w += Time.deltaTime) yield return null;
            // a drifting log carries him along with it
            game.Teleport(level.CrossingAt(92.2f));
            game.hero.y = 0.6f;
            game.hero.vy = 0;
            yield return new WaitForSeconds(0.25f);
            float onLog = game.hero.x, carried = 0;
            Assert.Greater(game.hero.y, -0.3f, "Raja did not land on the drifting log");
            for (float waited = 0; waited < 1.2f; waited += Time.deltaTime)
            {
                yield return null;
                carried = Mathf.Max(carried, Mathf.Abs(game.hero.x - onLog));
            }
            Assert.Greater(carried, 0.1f, "The drifting log did not carry him");
            Assert.Greater(game.hero.y, -0.3f, "Raja fell off the drifting log standing still");
        }

        [UnityTest]
        public IEnumerator ThreeButtonsAndTwoSwipesDoEverything()
        {
            // the phone's controls (seven buttons and their combinations were too much: the user): ATTACK tapped is the
            // combo and held the staff; KI the ki move; SPECIAL a button of its own (the user), lit when ki is high; a
            // swipe down slides (under a low trunk), a swipe across dodges that way; pushing into vines climbs them
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var hero = game.hero;
            IEnumerator Settle()
            {
                game.TestControls = null;
                for (float w = 0; w < 2f && hero.state != CbHero.State.Idle; w += Time.deltaTime) yield return null;
                yield return Frames(2);
            }
            game.Teleport(8f);
            hero.y = 0;
            yield return Settle();
            // ATTACK tapped: a paw, not the staff
            bool first = true, staff = false, paw = false;
            game.TestControls = i => { i.atk = first; i.atkDown = first; first = false; };
            for (float w = 0; w < 0.5f; w += Time.deltaTime) { paw |= hero.state == CbHero.State.Atk; staff |= hero.state == CbHero.State.Staff; yield return null; }
            Assert.IsTrue(paw, "A tap of ATTACK did not strike");
            Assert.IsFalse(staff, "A tap of ATTACK drew the staff");
            yield return Settle();
            // ATTACK held: the staff
            first = true; staff = false;
            game.TestControls = i => { i.atk = first; i.atkDown = true; first = false; };
            for (float w = 0; w < 0.8f; w += Time.deltaTime) { staff |= hero.state == CbHero.State.Staff; yield return null; }
            Assert.IsTrue(staff, "Holding ATTACK did not bring out the staff");
            yield return Settle();
            // KI: the ki move, never the special, even with ki full
            hero.ki = 100f;
            yield return null;
            Assert.AreEqual(CbHero.FireAwake ? "FIRE" : "KI", game.hud.SkillLabel, "The KI button says something else");
            first = true;
            bool special = false;
            game.TestControls = i => { i.kiTap = first; first = false; };
            for (float w = 0; w < 0.3f; w += Time.deltaTime) { special |= hero.state == CbHero.State.Special; yield return null; }
            Assert.IsFalse(special, "KI fired the special");
            yield return Settle();
            // SPECIAL: its own button, lit when ki is high
            hero.ki = 100f;
            yield return null;
            Assert.IsTrue(game.hud.SpecialLit, "The SPECIAL button is not lit with ki full");
            first = true;
            game.TestControls = i => { i.skill = first; first = false; };
            for (float w = 0; w < 0.3f; w += Time.deltaTime) { special |= hero.state == CbHero.State.Special; yield return null; }
            Assert.IsTrue(special, "SPECIAL did not fire the special");
            for (float w = 0; w < 4f && hero.state == CbHero.State.Special; w += Time.deltaTime) yield return null;
            yield return Settle();
            // a swipe across: a dodge that way, whichever way he faced
            hero.facing = 1;
            first = true;
            bool dodged = false;
            game.TestControls = i => { if (first) { i.dash = true; i.dashDir = -1; } first = false; };
            for (float w = 0; w < 0.2f; w += Time.deltaTime) { dodged |= hero.state == CbHero.State.Dash && hero.facing == -1; yield return null; }
            Assert.IsTrue(dodged, "A swipe to the left did not dodge to the left");
            yield return Settle();
            // a tip naming a move of the stick shows its hint (a finger pushing it) until it is done once
            CbSave.Learned = 0;
            game.hud.SetTip("A fallen trunk! Push the <color=#ffb03a>stick ↓</color> to slide under it");
            if (CbHud.ShowPads) Assert.AreEqual(CbHud.Hint.StickDown, game.hud.ShownHint, "No hint for the stick down");
            // down on the stick at the fallen trunk: he slides under it and on out the far side
            float trunk = CbLevel.TrunkX;
            game.Teleport(trunk - 3f);
            hero.y = 0;
            hero.facing = 1;
            yield return Settle();
            first = true;
            bool slid = false;
            // (running: from a stand, down only crouches - the user)
            float started = Time.time;
            game.TestControls = i => { i.x = 1; if (first && Time.time - started > 0.35f) { i.slide = true; first = false; } };
            for (float w = 0; w < 5f && hero.x < trunk + 1.2f; w += Time.deltaTime) { slid |= hero.state == CbHero.State.Slide; yield return null; }
            Assert.IsTrue(slid, "Down on the stick did not slide");
            Assert.AreEqual(CbHud.Hint.None, game.hud.ShownHint, "The stick's hint stays after the slide");
            game.hud.SetTip("<color=#ffb03a>Stick ↓</color> again");
            Assert.AreEqual(CbHud.Hint.None, game.hud.ShownHint, "The stick's hint comes back once it was learned");
            Assert.Greater(hero.x, trunk + 1.2f, "Raja did not get under the fallen trunk with a slide");
            yield return Settle();
            // pushing into the vine-grown pillar: he takes hold and climbs
            game.Teleport(371.6f);
            hero.y = 0;
            yield return Settle();
            bool held = false;
            float top = 0;
            game.TestControls = i => { i.x = 1; };
            for (float w = 0; w < 2.5f; w += Time.deltaTime) { held |= hero.state == CbHero.State.Vine; top = Mathf.Max(top, hero.y); yield return null; }
            game.TestControls = null;
            Assert.IsTrue(held, "Pushing into the vines did not take hold of them");
            Assert.Greater(top, 1.5f, "Pushing into the vines did not climb them");
        }

        [UnityTest]
        public IEnumerator AStrikeTurnsHimToAFoeCloseBehind()
        {
            // on a phone he was often left facing the wrong way and struck thin air: a strike turns him to a soldier
            // close behind him, if none is in front
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            game.Teleport(8f);
            game.hero.y = 0;
            yield return Frames(3);
            game.hero.facing = 1;
            var e = new CbEnemy(game, CbEnemy.Kind.Hyena, 6.6f, game.transform) { minX = 5f, maxX = 7f };
            game.enemies.Add(e);
            bool first = true;
            game.TestControls = i => { i.atk = first; first = false; };
            yield return Frames(2);
            game.TestControls = null;
            Assert.AreEqual(-1, game.hero.facing, "A strike did not turn him to the soldier close behind him");
        }

        [UnityTest]
        public IEnumerator SoldiersComeForRajaAndHopWhatIsInTheirWay()
        {
            // the user: the soldiers didn't come for Raja - only right next to him did they fight. At his post a soldier
            // steps out as Raja comes near and comes at him from well off; at the end of his stretch, Raja beyond a low
            // rock, a log or a crate, he hops over it
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            var hero = game.hero;
            hero.hp = 9999;
            hero.inv = 999f;
            // the first soldier's post is at 22 m: Raja 11 m short of it
            game.Teleport(11f);
            hero.y = 0;
            CbEnemy e = null;
            for (float w = 0; w < 1f && e == null; w += Time.deltaTime)
            {
                yield return null;
                e = game.enemies.Find(o => o.kind == CbEnemy.Kind.Hyena && !o.dead);
            }
            Assert.IsNotNull(e, "No soldier stepped out at his post as Raja came near");
            float from = e.x;
            yield return new WaitForSeconds(2f);
            Assert.Less(e.x, from - 3f, "The soldier did not come for Raja from 11 m off");
            // a soldier at the end of his stretch, a crate in his way: he hops it
            game.SkipFights();
            var s = new CbEnemy(game, CbEnemy.Kind.Hyena, 30.5f, game.transform)
                { minX = 29f, maxX = 32f, hopMinX = 20f, hopMaxX = 32f };
            game.enemies.Add(s);
            game.Teleport(24f);
            hero.y = 0;
            float highest = 0;
            for (float w = 0; w < 4f && s.x > 28f; w += Time.deltaTime)
            {
                yield return null;
                highest = Mathf.Max(highest, s.y);
            }
            Assert.Less(s.x, 28f, "The soldier did not get past the end of his stretch to Raja");
            Assert.Greater(highest, 0.8f, "The soldier did not hop over what was in his way");
        }

        [UnityTest]
        public IEnumerator BirdsOnTheGroundTakeOffAsRajaComes()
        {
            // the user: the birds should move when he comes near, like the butterflies
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var critters = game.critters;
            if (critters.BirdsPerched == 0) { Assert.IsFalse(critters.HasBirds, "No birds on the ground in the jungle"); yield break; }
            float x = critters.FirstPerchX;
            game.Teleport(x - 9f);
            game.hero.y = 0;
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0, critters.BirdsTakenOff, "The birds took off with Raja far away");
            game.Teleport(x - 1.5f);
            game.hero.y = 0;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(critters.BirdsTakenOff, 0, "The birds did not take off as Raja came by");
        }

        [UnityTest]
        public IEnumerator StandingDownCrouchesRunningDownSlides()
        {
            // the user: only running does he slide; standing, down crouches
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var hero = game.hero;
            game.Teleport(184f);
            hero.y = 0;
            yield return new WaitForSeconds(0.3f);
            bool slid = false, first = true;
            game.TestControls = i => { i.y = -1; i.slide = first; first = false; };
            for (int k = 0; k < 20; k++) { slid |= hero.state == CbHero.State.Slide; yield return null; }
            Assert.IsFalse(slid, "Standing, down slid");
            Assert.IsTrue(hero.Ducking, "Standing, down did not crouch");
            game.TestControls = null;
            yield return new WaitForSeconds(0.2f);
            float started = Time.time;
            first = true;
            game.TestControls = i => { i.x = 1; if (first && Time.time - started > 0.4f) { i.slide = true; first = false; } };
            for (float w = 0; w < 1.2f && !slid; w += Time.deltaTime) { slid |= hero.state == CbHero.State.Slide; yield return null; }
            game.TestControls = null;
            Assert.IsTrue(slid, "Running, down did not slide");
        }

        [UnityTest]
        public IEnumerator TheRaftGoesUnderTheFootbridgeAndRajaDoesNot()
        {
            // the user: one raft from bank to bank and back, and a low footbridge it passes under - he can't, he must
            // jump up onto it and back down onto the raft
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var level = game.level;
            var hero = game.hero;
            hero.hp = 9999;
            Time.captureDeltaTime = game.TestStep = 1f / 30f;
            game.Teleport(level.RaftX);
            hero.y = 0.1f;
            hero.vx = hero.vy = 0;
            yield return Frames(3);
            float bridge = CbLevel.RaftBridgeX;
            for (float w = 0; w < 25f && level.RaftX < bridge + 2f; w += Time.deltaTime) yield return null;
            Time.captureDeltaTime = game.TestStep = 0f;
            Assert.Greater(level.RaftX, bridge + 2f, "The raft did not go on under the footbridge");
            Assert.Less(hero.x, bridge, "Raja on the raft went through the footbridge");
        }

        [UnityTest]
        public IEnumerator ADodgeCutsABlowShort()
        {
            // more precise control (the user): once a paw blow has struck, a dodge cuts the rest of it short
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var hero = game.hero;
            game.Teleport(184f);
            hero.y = 0;
            yield return Frames(2);
            bool first = true;
            game.TestControls = i => { i.atk = first; first = false; };
            for (int k = 0; k < 60 && hero.state != CbHero.State.Atk; k++) yield return null;
            game.TestControls = null;
            Assert.AreEqual(CbHero.State.Atk, hero.state, "No blow");
            // past its hit (two thirds of a 0.22 s blow)
            yield return new WaitForSeconds(0.17f);
            if (hero.state == CbHero.State.Atk)
            {
                bool dodge = true;
                game.TestControls = i => { i.dash = dodge; i.dashDir = 1; dodge = false; };
                yield return Frames(2);
                game.TestControls = null;
                Assert.AreEqual(CbHero.State.Dash, hero.state, "A dodge did not cut the blow short");
            }
        }

        [UnityTest]
        public IEnumerator SoldiersTakeTurnsAndACounterHitsHarder()
        {
            // smarter fights (the user): at most two soldiers strike at Raja at once, the rest wait their turn; a blow on
            // one winding up his own is a counter - half as much again
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var hero = game.hero;
            hero.hp = 9999;
            hero.inv = 999f;
            game.Teleport(184f);
            hero.y = 0;
            var foes = new List<CbEnemy>();
            foreach (float off in new[] { -1.3f, 1.3f, 1.9f, -1.9f })
            {
                var e = new CbEnemy(game, CbEnemy.Kind.Hyena, 184f + off, game.transform);
                game.enemies.Add(e);
                foes.Add(e);
            }
            int most = 0;
            for (float w = 0; w < 4f; w += Time.deltaTime)
            {
                yield return null;
                int n = foes.FindAll(e => e.Striking).Count;
                most = Mathf.Max(most, n);
            }
            Assert.LessOrEqual(most, CbGame.MaxStrikers, "More soldiers struck at Raja at once than may");
            Assert.Greater(most, 0, "No soldier struck at Raja");
            // a counter
            CbEnemy winding = null;
            for (float w = 0; w < 4f && winding == null; w += Time.deltaTime)
            {
                yield return null;
                winding = foes.Find(e => e.WindingUp);
            }
            Assert.IsNotNull(winding, "No soldier wound up a blow");
            winding.hp = 100;
            game.HurtEnemy(winding, 10f, winding.x > hero.x ? 1 : -1, 2f, 0f, false, false, false, true);
            Assert.AreEqual(1, game.Counters, "A blow on a soldier winding up was no counter");
            Assert.Less(winding.hp, 100f - 10f * CbSave.DamageMul - 0.01f, "The counter hit no harder");
        }

        [UnityTest]
        public IEnumerator AFightsSoldierOutOfSightComesBackAndIsPointedTo()
        {
            // in a fight, a soldier out of sight (knocked away, or come in at the far side) is shown by an arrow at that
            // side of the screen, and comes back for Raja himself (he stood there out of sight, and the player had to
            // go back to find him: the user)
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            game.hero.inv = 999f;
            game.Teleport(CbLevel.RopeBridgeX - 6f);
            game.hero.y = game.level.SupportAt(game.hero.x, 2f);
            game.hero.vy = 0;
            yield return Frames(3);
            Assert.AreEqual(1, game.RopeAmbushWave, "No ambush on the rope bridge");
            for (float w = 0; w < 3f && !game.AllRunIn; w += Time.deltaTime) yield return null;
            var foe = game.enemies.Find(e => !e.dead && Mathf.Abs(e.x - CbLevel.RopeBridgeX) < 20f);
            Assert.IsNotNull(foe, "No raider came for the ambush");
            // thrown far off to the left, out of sight, standing there
            foe.x = game.hero.x - 15f;
            foe.vx = foe.vy = 0;
            foe.runningIn = false;
            yield return Frames(3);
            Assert.IsTrue(game.hud.FoeArrowLeft, "No arrow points to the raider out of sight at the left");
            float was = Mathf.Abs(foe.x - game.hero.x);
            yield return new WaitForSeconds(2f);
            Assert.Less(Mathf.Abs(foe.x - game.hero.x), was - 4f, "A raider out of sight stood there instead of coming back for Raja");
        }

        [UnityTest]
        public IEnumerator TheRopeBridgeAtTheStartIsAnAmbush()
        {
            // at least five raiders on the rope bridge (the user), in waves; beaten, the way opens. No soldier stands on
            // the rock in the long river after it any more (the user)
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            game.hero.inv = 999f;
            // the tunnels' gates are shut: no one takes them for a way in (the user)
            Assert.AreEqual(0f, game.level.TunnelOpen(CbLevel.RopeMouthL), "A tunnel stood open before its ambush");
            Assert.AreEqual(0f, game.level.TunnelOpen(CbLevel.RopeMouthR), "A tunnel stood open before its ambush");
            game.Teleport(CbLevel.RopeBridgeX - 6f);
            game.hero.y = game.level.SupportAt(game.hero.x, 2f);
            game.hero.vy = 0;
            yield return Frames(3);
            Assert.AreEqual(1, game.RopeAmbushWave, "No ambush on the rope bridge");
            // they step out of the tunnels by the bridge's ends, from both sides, one after another (the user), each
            // once his tunnel's gate has sunk
            List<CbEnemy> firstIn = null;
            for (float w = 0; w < 2f; w += Time.deltaTime)
            {
                firstIn = game.enemies.FindAll(e => !e.dead && Mathf.Abs(e.x - CbLevel.RopeBridgeX) < 20f);
                if (firstIn.Count > 0) break;
                yield return null;
            }
            Assert.Greater(firstIn.Count, 0, "No raider came for the ambush");
            Assert.IsTrue(firstIn.TrueForAll(e => game.level.TunnelOpen(e.x) > 0.9f), "A raider came out through a shut gate");
            Assert.IsTrue(firstIn.TrueForAll(e => Mathf.Min(Mathf.Abs(e.x - CbLevel.RopeMouthL), Mathf.Abs(e.x - CbLevel.RopeMouthR)) < 1.5f),
                "An ambush's raider did not come out of a tunnel by the bridge");
            Assert.IsTrue(firstIn.TrueForAll(e => e.emergeZ > 0f), "An ambush's raider did not step out from inside the tunnel");
            int raiders = 0;
            for (int wave = 1; wave <= 6 && !game.RopeAmbushDone; wave++)
            {
                for (float w = 0; w < 3f && !game.AllRunIn; w += Time.deltaTime) yield return null;
                var foes = game.enemies.FindAll(e => !e.dead && Mathf.Abs(e.x - CbLevel.RopeBridgeX) < 20f);
                Assert.Greater(foes.Count, 0, $"Wave {wave} of the rope bridge's ambush has no raiders");
                raiders += foes.Count;
                foreach (var e in foes) { e.vx = e.vy = 0; e.Kill(1); }
                yield return new WaitForSeconds(0.5f);
            }
            Assert.GreaterOrEqual(raiders, 5, "The rope bridge's ambush is too small (at least five: the user)");
            Assert.IsTrue(game.RopeAmbushDone, "The rope bridge's ambush did not end");
            Assert.IsNull(game.lockZone, "The rope bridge stays shut after its ambush");
            // past it, the long river: no soldier on its rock
            game.Teleport(84f);
            game.hero.y = 0;
            yield return Frames(5);
            Assert.IsFalse(game.enemies.Exists(e => !e.dead && Mathf.Abs(e.x - 89f) < 1.5f), "A soldier still stands on the rock in the long river");
            // the gates rose again behind the last of them
            for (float w = 0; w < 3f && (game.level.TunnelOpen(CbLevel.RopeMouthL) > 0 || game.level.TunnelOpen(CbLevel.RopeMouthR) > 0); w += Time.deltaTime) yield return null;
            Assert.AreEqual(0f, game.level.TunnelOpen(CbLevel.RopeMouthL), "A tunnel's gate stayed down after the ambush");
            Assert.AreEqual(0f, game.level.TunnelOpen(CbLevel.RopeMouthR), "A tunnel's gate stayed down after the ambush");
        }

        [UnityTest]
        public IEnumerator SpringyLeavesLeadSomewhere()
        {
            // the leaf before the rope bridge led nowhere (the user): each leaf now throws him up to something; over
            // the old shrine, up to a ledge with a ki crystal
            Assert.IsFalse(System.Array.Exists(CbLevel.LeafXs, x => x < 100f), "A springy leaf still stands before the rope bridge");
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            float leaf = CbLevel.LeafXs[0];
            Assert.That(leaf, Is.InRange(110f, 130f), "No springy leaf at the old shrine");
            Assert.AreEqual(CbLevel.ShrineLedgeTop, game.level.SupportAt(CbLevel.ShrineLedgeX, 20f), 0.01f, "No ledge over the old shrine");
            // dropped onto it, he bounces up past the ledge's height
            game.Teleport(leaf);
            game.hero.y = 3f;
            game.hero.vy = 0;
            game.hero.state = CbHero.State.Air;
            float high = 0;
            for (float w = 0; w < 1.5f; w += Time.deltaTime) { high = Mathf.Max(high, game.hero.y); yield return null; }
            Assert.Greater(high, CbLevel.ShrineLedgeTop + 0.3f, "The springy leaf at the old shrine does not throw him up to its ledge");
            // walking into it he stops against its pad (he walked through it, the pad across his chest: the user)
            // (last: up against it, he can be left hanging from its edge)
            for (float w = 0; w < 3f && game.hero.state != CbHero.State.Idle && game.hero.state != CbHero.State.Run; w += Time.deltaTime) yield return null;
            game.Teleport(leaf - 2.5f);
            game.hero.y = 0;
            game.hero.vx = game.hero.vy = 0;
            game.TestInput = i => i.x = 1f;
            yield return new WaitForSeconds(1f);
            game.TestInput = null;
            Assert.Less(game.hero.x, leaf - 0.6f, "Raja walked through the springy leaf's pad");
        }

        [UnityTest]
        public IEnumerator HeStandsOnTheRocksNotAboveThem()
        {
            // he looked to walk in the air over some rocks (the user): along the line he walks, the top of each rock's
            // model must be where he stands
            game = CbGame.Boot();
            yield return Frames(3);
            var bad = new List<string>();
            int measured = 0;
            foreach (var (x0, x1, top, t) in game.level.Standables())
            {
                if (top < 0.25f) continue;
                float a = x0 + 0.2f, b = x1 - 0.2f;
                if (b - a < 0.3f) { a = (x0 + x1) / 2 - 0.15f; b = a + 0.3f; }
                // the model's triangles near the line he walks, in the world (a flat top has its corners far apart:
                // its vertices alone missed it)
                var tris = new List<(Vector3 p, Vector3 q, Vector3 r)>();
                foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = mf.sharedMesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    var m = mf.transform.localToWorldMatrix;
                    var vs = mesh.vertices;
                    var w = new Vector3[vs.Length];
                    for (int i = 0; i < vs.Length; i++) w[i] = m.MultiplyPoint3x4(vs[i]);
                    var ix = mesh.triangles;
                    for (int k = 0; k < ix.Length; k += 3)
                    {
                        Vector3 p = w[ix[k]], q = w[ix[k + 1]], r = w[ix[k + 2]];
                        if (Mathf.Max(p.x, q.x, r.x) < a || Mathf.Min(p.x, q.x, r.x) > b) continue;
                        if (Mathf.Max(p.z, q.z, r.z) < -0.3f || Mathf.Min(p.z, q.z, r.z) > 0.1f) continue;
                        tris.Add((p, q, r));
                    }
                }
                if (tris.Count == 0) continue;
                // straight down onto them at (x, z): the highest surface there
                float Down(float x, float z)
                {
                    float best = float.MinValue;
                    foreach (var (p, q, r) in tris)
                    {
                        float d = (q.z - r.z) * (p.x - r.x) + (r.x - q.x) * (p.z - r.z);
                        if (Mathf.Abs(d) < 1e-9f) continue;
                        float l1 = ((q.z - r.z) * (x - r.x) + (r.x - q.x) * (z - r.z)) / d;
                        float l2 = ((r.z - p.z) * (x - r.x) + (p.x - r.x) * (z - r.z)) / d;
                        float l3 = 1 - l1 - l2;
                        if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;
                        best = Mathf.Max(best, l1 * p.y + l2 * q.y + l3 * r.y);
                    }
                    return best;
                }
                const int Slices = 8;
                var found = new List<float>();
                for (int i = 0; i < Slices; i++)
                {
                    float x = Mathf.Lerp(a, b, (i + 0.5f) / Slices);
                    // under his feet and just in front of them (what the camera sees him stand on)
                    float y = Mathf.Max(Down(x, 0f), Down(x, -0.2f));
                    if (y > float.MinValue) found.Add(y);
                }
                if (found.Count < Slices / 2) continue;
                found.Sort();
                measured++;
                float surface = found[found.Count / 2];
                Debug.Log($"STAND {x0:0.0}-{x1:0.0} top {top:0.00} surface {surface:0.00} low {found[0]:0.00} high {found[found.Count - 1]:0.00} ({t.name})");
                if (surface < top - 0.2f) bad.Add($"{x0:0.0}-{x1:0.0} ({t.name}): he stands at {top:0.00}, the rock under him is at {surface:0.00}");
            }
            Assert.Greater(measured, 5, "Too few rocks measured");
            Assert.IsEmpty(bad, "Raja stands in the air over:\n" + string.Join("\n", bad));
        }

        [UnityTest]
        public IEnumerator TheAmbushOnTheStoneBridgeComesInWavesAndLetsHimGo()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            // a soldier left behind at his post before the canopy (the ambush waited on one left behind: its second wave
            // never came, and the bridge stayed shut: the user)
            game.Teleport(361f);
            game.hero.y = 0;
            yield return Frames(3);
            Assert.IsTrue(game.enemies.Exists(e => !e.dead && Mathf.Abs(e.x - 363f) < 3f), "No soldier at his post before the canopy");
            // up the ramp onto the stone bridge, walking (he had to jump up onto it: the user): the ambush
            game.Teleport(CbLevel.StoneBridgeX - CbLevel.StoneBridgeHalf - 3.2f);   // (past the chest, at the ramp's foot)
            game.hero.y = 0;
            game.hero.vy = 0;
            // (the raiders can't beat him while the test beats them)
            game.hero.inv = 999f;
            game.TestInput = i => { i.x = 1; };
            for (float w = 0; w < 5f && game.BridgeWave == 0; w += Time.deltaTime) yield return null;
            game.TestInput = null;
            Assert.AreEqual(1, game.BridgeWave, "No ambush on the stone bridge");
            Assert.AreEqual(CbLevel.StoneDeck, game.hero.y, 0.08f, "Raja did not walk up onto the stone bridge");
            int raiders = 0;
            for (int wave = 1; wave <= 3; wave++)
            {
                Assert.AreEqual(wave, game.BridgeWave, $"Wave {wave} of the ambush did not come");
                for (float w = 0; w < 3f && !game.AllRunIn; w += Time.deltaTime) yield return null;
                var foes = game.enemies.FindAll(e => !e.dead && Mathf.Abs(e.x - CbLevel.StoneBridgeX) < 20f);
                Assert.Greater(foes.Count, 2, $"Wave {wave} has too few raiders");
                if (wave == 1) yield return TakePictures(new[] { ("stone_ambush", float.NaN, 0f) }, 0.2f);
                // they come along the bridge's deck, not on the ground under it (the user)
                foreach (var e in foes)
                    if (Mathf.Abs(e.x - CbLevel.StoneBridgeX) < CbLevel.StoneBridgeHalf - 0.5f)
                        Assert.AreEqual(CbLevel.StoneDeck, e.y, 0.05f, "A raider of the ambush is under the stone bridge");
                raiders += foes.Count;
                foreach (var e in foes) { e.vx = e.vy = 0; e.Kill(1); }
                // their coins come to rest on the deck, not under it where he can't reach them
                yield return new WaitForSeconds(CbEnemy.DyingTime + 1.2f);
                foreach (var (pos, resting) in game.items.ItemsNear(CbLevel.StoneBridgeX, 11f))
                    if (resting) Assert.Greater(pos.y, CbLevel.StoneDeck, "A raider's coin lies under the stone bridge's deck");
            }
            Assert.GreaterOrEqual(raiders, 7, "The ambush is too short (at least seven: the user)");
            Assert.IsTrue(game.BridgeFightDone, "The ambush did not end with every raider beaten");
            Assert.IsNull(game.lockZone, "The bridge stays shut after the ambush");
            // and he walks on off the bridge
            game.TestInput = i => { i.x = 1; };
            for (float w = 0; w < 4f && game.hero.x < CbLevel.StoneBridgeX + CbLevel.StoneBridgeHalf + 1f; w += Time.deltaTime) yield return null;
            game.TestInput = null;
            Assert.Greater(game.hero.x, CbLevel.StoneBridgeX + CbLevel.StoneBridgeHalf + 1f, "Raja can't walk off the bridge after the ambush");
        }

        [UnityTest]
        public IEnumerator RajaCatchesAnEdgeAndPullsHimselfUp()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var level = game.level;
            // a cliff's top corner is caught (the shoulder of the summit's cliff, in the gorge); the cracked wall is
            // there to be broken, not climbed
            Assert.IsTrue(level.FindEdge(CbLevel.ShoulderX - CbLevel.HalfWidth, CbLevel.ShoulderTop - CbHero.HangReach, 1, out float edgeX, out float top),
                "The shoulder's corner can't be caught");
            Assert.AreEqual(CbLevel.ShoulderX, edgeX, 0.001f);
            Assert.AreEqual(CbLevel.ShoulderTop, top, 0.001f);
            float wallLeft = CbLevel.WallX - 0.75f - CbLevel.HalfWidth;
            Assert.IsFalse(level.FindEdge(wallLeft, CbLevel.WallHeight - CbHero.HangReach, 1, out _, out _), "Raja can climb over the cracked wall");
            // on the gorge's floor under the shoulder: a jump at it, holding towards it; he catches its edge at the top
            // of the jump, hangs, and pulls himself up onto it
            game.Teleport(CbLevel.ShoulderX - 0.8f);
            game.hero.y = 0;
            game.hero.vx = game.hero.vy = 0;
            yield return Frames(2);
            bool jumped = false, hung = false, climbed = false;
            game.TestInput = i => { i.x = 1; if (!jumped) { i.jump = true; jumped = true; } };
            for (float waited = 0; waited < 3f && !(climbed && game.hero.state != CbHero.State.Climb); waited += Time.deltaTime)
            {
                yield return null;
                hung |= game.hero.state == CbHero.State.Hang;
                climbed |= game.hero.state == CbHero.State.Climb;
            }
            game.TestInput = null;
            Assert.IsTrue(hung, "Raja did not catch the shoulder's edge");
            Assert.IsTrue(climbed, "Raja did not pull himself up");
            Assert.AreEqual(CbLevel.ShoulderTop, game.hero.y, 0.05f, "Raja is not standing on the shoulder");
            Assert.Greater(game.hero.x, CbLevel.ShoulderX, "Raja did not come up onto the shoulder");
        }

        [UnityTest]
        public IEnumerator ASoldierKnockedIntoTheRiverFallsIn()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            // a soldier on the far bank of the long river, his back to it (his stretch ends short of the water)
            float bank = CbLevel.Waters[1].x1;
            var e = new CbEnemy(game, CbEnemy.Kind.Hyena, bank + 0.9f, game.transform) { minX = bank + 0.6f, maxX = bank + 3f };
            game.enemies.Add(e);
            game.Teleport(bank + 6f);
            yield return Frames(3);
            // walking, he keeps to his stretch; a blow that throws him towards the river sends him into it
            Assert.Greater(e.x, bank, "The soldier walked into the river");
            int score = game.score;
            game.HurtEnemy(e, 1f, -1, 8f, 2f, false, false, false, true);
            for (float w = 0; w < 3f && game.enemies.Contains(e); w += Time.deltaTime) yield return null;
            Assert.IsFalse(game.enemies.Contains(e), "The soldier was not thrown into the river");
            Assert.IsTrue(e.dead, "A soldier in the river is not beaten");
            Assert.Greater(game.score, score + 200, "No bonus for throwing him in");
            // the counter counts up to the new score
            Assert.Less(game.hud.ScoreShown, game.score, "The score jumped straight to its new total");
            for (float w = 0; w < 1f; w += Time.deltaTime) yield return null;
            Assert.AreEqual(game.score, game.hud.ScoreShown, "The score counter did not catch up");
        }

        [UnityTest]
        public IEnumerator ABlowSquashesASoldierForAMoment()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            // on open ground, a soldier struck lightly: squashed (wider, shorter) for a moment, then springing back
            game.Teleport(184f);
            var e = new CbEnemy(game, CbEnemy.Kind.Hyena, 192f, game.transform);
            game.enemies.Add(e);
            yield return Frames(3);
            Assert.AreEqual(1f, e.f.body.localScale.y, 0.001f, "The soldier was squashed before any blow");
            game.HurtEnemy(e, 1f, 1, 0.5f, 0f, false, false, false, true);
            yield return null;
            Assert.Less(e.f.body.localScale.y, 0.97f, "A blow did not squash the soldier");
            Assert.Greater(e.f.body.localScale.x, 1.03f, "A squashed soldier did not widen");
            for (float w = 0; w < 0.4f; w += Time.deltaTime) yield return null;
            Assert.IsFalse(e.dead, "A light blow beat the soldier");
            Assert.AreEqual(1f, e.f.body.localScale.y, 0.001f, "The soldier did not spring back from the squash");
            Assert.AreEqual(1f, e.f.body.localScale.x, 0.001f, "The soldier stayed wide after the squash");
        }

        [UnityTest]
        public IEnumerator UpAsASpearStrikesIsABackflipOverIt()
        {
            // the user: up at the perfect moment as a spear comes, he dodges it - a backflip (Meshy's, his choice)
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var hero = game.hero;
            Assert.IsTrue(hero.model == null || hero.model.HasClip("Backflip_inplace"), "Raja has no backflip clip");
            game.Teleport(184f);
            hero.y = 0;
            var e = new CbEnemy(game, CbEnemy.Kind.Thrower, 191f, game.transform);
            game.enemies.Add(e);
            hero.hp = 100;
            float hp = hero.hp;
            // up as the spear is about to strike
            game.TestInput = i => { if (hero.state != CbHero.State.Air && (game.level.ThrownComing(hero.x, hero.y, 1, 2.2f))) i.jump = true; };
            for (float w = 0; w < 6f && hero.SpearsDodged == 0; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.5f);
            game.TestInput = null;
            Assert.Greater(hero.SpearsDodged, 0, "Up as a spear struck did not dodge it");
            Assert.AreEqual(hp, hero.hp, 0.01f, "The spear he dodged still hurt him");
        }

        [UnityTest]
        public IEnumerator ASpearThrowerKeepsHisDistanceAndThrows()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            // on open ground, a spear thrower seven metres off: he stays away and throws spears at chest height
            game.Teleport(184f);
            game.hero.y = 0;
            var e = new CbEnemy(game, CbEnemy.Kind.Thrower, 191f, game.transform);
            game.enemies.Add(e);
            float hp = game.hero.hp;
            float closest = float.MaxValue;
            for (float w = 0; w < 5f && game.hero.hp >= hp; w += Time.deltaTime)
            {
                closest = Mathf.Min(closest, Mathf.Abs(e.x - game.hero.x));
                yield return null;
            }
            Assert.Less(game.hero.hp, hp, "No spear hit Raja standing in its way");
            Assert.Greater(closest, 3f, "The thrower came up close instead of throwing");
            // ducking, the spears pass over him
            game.hero.hp = hp = 100;
            game.TestInput = i => i.y = -1;
            yield return new WaitForSeconds(4f);
            game.TestInput = null;
            Assert.AreEqual(hp, game.hero.hp, 0.01f, "A spear hit Raja while he ducked");
        }

        [UnityTest]
        public IEnumerator RajasLaterClipsMoveHim()
        {
            // clips made later on a new rig (Raja_Clips, Raja_More_Clips) must drive his own bones: a file whose skeleton
            // had another name added clips that moved nothing, and he slid about frozen as he crawled and on the bars
            game = CbGame.Boot();
            yield return Frames(3);
            var model = game.hero.model;
            Assert.IsNotNull(model);
            var anim = model.root.GetComponent<Animation>();
            foreach (var clip in new[] { "Crawl", "MonkeyBars", "Motion_Divekick", "slide_light", "Backflip_inplace" })
            {
                AnimationState state = null;
                foreach (AnimationState st in anim) if (st.name.EndsWith(clip)) state = st;
                Assert.IsNotNull(state, $"Raja has no {clip} clip");
                foreach (AnimationState st in anim) { st.enabled = false; st.weight = 0; }
                state.enabled = true;
                state.weight = 1;
                state.normalizedTime = 0f;
                anim.Sample();
                var restHand = model.rightHand.position;
                float moved = 0;
                for (float u = 0.1f; u <= 1f; u += 0.1f)
                {
                    state.normalizedTime = u;
                    anim.Sample();
                    moved = Mathf.Max(moved, (model.rightHand.position - restHand).magnitude);
                }
                Assert.Greater(moved, 0.02f, $"Raja's {clip} clip does not move his hands at all");
            }
            // he crawls flat on his belly, on his elbows (on hands and knees he was up on all fours: the user): his chest
            // is down near the ground, under half as high as standing (on all fours it was well over half)
            float ChestAt(string clip, float u)
            {
                AnimationState st = null;
                foreach (AnimationState a in anim) { a.enabled = false; a.weight = 0; if (a.name.EndsWith(clip)) st = a; }
                Assert.IsNotNull(st, $"Raja has no {clip} clip");
                st.enabled = true;
                st.weight = 1;
                st.normalizedTime = u;
                anim.Sample();
                return model.chest.position.y - model.root.position.y;
            }
            float standing = ChestAt("Combat_Stance", 0.2f);
            for (float u = 0.1f; u < 1f; u += 0.2f)
                Assert.Less(ChestAt("Crawl", u), standing * 0.55f, "Raja crawls up on all fours, not flat on his elbows");
            // hanging from the bar, his hands are up over his chest
            AnimationState hang = null;
            foreach (AnimationState st in anim) { st.enabled = false; st.weight = 0; if (st.name.EndsWith("Bar_Hang_Idle")) hang = st; }
            Assert.IsNotNull(hang, "Raja has no Bar_Hang_Idle clip");
            hang.enabled = true;
            hang.weight = 1;
            hang.normalizedTime = 0.3f;
            anim.Sample();
            Assert.Greater(model.rightHand.position.y, model.chest.position.y + 0.3f, "Hanging from the bar, Raja's hands are not up");
        }

        [UnityTest]
        public IEnumerator TheGorgeFloodsBehindHimAndTheWaterCatchesHim()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var level = game.level;
            Assert.IsFalse(level.Flooding, "The gorge floods before Raja is in it");
            // through the gap and straight up onto the first ledge, never on the gorge's floor (the user: the water
            // didn't always come): a boulder drops into the gap behind him and the water comes
            game.Teleport(141.9f);
            game.hero.y = 2.0f;
            game.hero.vy = 0;
            yield return Frames(3);
            Assert.IsTrue(level.Flooding, "The gorge did not flood once he was in it");
            yield return new WaitForSeconds(0.5f);
            // it comes from somewhere: it bursts out of cracks in the cliff behind him (the user: nothing showed where)
            Assert.Greater(level.SpoutsPouring, 0, "No water poured out of the cliff into the gorge");
            game.Teleport(CbLevel.GorgeFrom + 1.5f);
            game.hero.y = 0;
            yield return Frames(2);
            Assert.AreEqual(CbLevel.GorgeFrom + CbLevel.HalfWidth, level.BlockX(CbLevel.GorgeFrom + 1f, CbLevel.MountainFrom - 2f, 0f, CbLevel.LowHeight), 0.001f,
                "The gap is still open behind him");
            // the water rises over him where he stands: caught, he loses health, treads water, and is back at the
            // foot of the gorge with the water drained
            float hp = game.hero.hp;
            level.StartFlood(1.5f);
            yield return Frames(2);
            Assert.AreEqual(CbHero.State.Swim, game.hero.state, "The water rose over him and he did not notice");
            Assert.AreEqual(hp - CbHero.WaterDamage, game.hero.hp, 0.01f, "The water cost him nothing");
            for (float w = 0; w < 3f && game.hero.state == CbHero.State.Swim; w += Time.deltaTime) yield return null;
            // (dropped back onto the floor from a little above it)
            for (float w = 0; w < 1.5f && game.hero.state == CbHero.State.Air; w += Time.deltaTime) yield return null;
            yield return Frames(2);
            Assert.AreEqual(0f, game.hero.y, 0.05f, "Raja is not back on the gorge's floor");
            Assert.That(game.hero.x, Is.InRange(CbLevel.GorgeFrom, CbLevel.GorgeFrom + 2f), "Raja is not back at the foot of the gorge");
            Assert.Less(level.FloodLevel, 0.5f, "The water did not drain when it caught him");
            // out at the top: the water drains away and stays down
            game.Teleport(154f);
            game.hero.y = CbLevel.SummitTop;
            game.hero.vy = 0;
            yield return Frames(3);
            Assert.IsFalse(level.Flooding, "The water still rises once he is out at the top");
        }

        [UnityTest]
        public IEnumerator WallJumpsGetHimOutOfAGapBetweenWalls()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var level = game.level;
            // the cracked wall is not jumped off; a terrace's side is
            Assert.IsFalse(level.WallAt(CbLevel.WallX - 0.75f - CbLevel.HalfWidth, 1f, 1), "Raja can wall-jump up the cracked wall");
            Assert.IsTrue(level.WallAt(CbLevel.GorgeTo - CbLevel.HalfWidth, 5f, 1), "The summit's cliff is not a wall to jump off");
            // on the shoulder in the gorge, the summit's cliff before him far too high to jump: pressing into it he
            // jumps, slides down it, kicks off it again and again, catches its top edge and pulls himself up onto it
            game.Teleport(CbLevel.GorgeTo - 0.6f);
            game.hero.y = CbLevel.ShoulderTop;
            game.hero.vx = game.hero.vy = 0;
            yield return Frames(2);
            bool started = false, climbing = false;
            int kicks = 0;
            game.TestInput = i =>
            {
                climbing |= game.hero.state == CbHero.State.Climb;
                i.x = climbing ? 0 : 1;
                if (!started) { i.jump = true; started = true; }
                else i.jump = game.hero.OnWall && game.hero.vy < 0;
            };
            float lastVy = 0;
            for (float waited = 0; waited < 8f && !(climbing && game.hero.state == CbHero.State.Idle); waited += Time.deltaTime)
            {
                yield return null;
                if (game.hero.vy > 9.5f && lastVy < 0 && game.hero.facing == -1) kicks++;
                lastVy = game.hero.vy;
            }
            game.TestInput = null;
            Assert.Greater(kicks, 0, "Raja never kicked off the wall");
            Assert.IsTrue(climbing, "Raja never caught the terrace's top");
            Assert.AreEqual(CbLevel.SummitTop, game.hero.y, 0.05f, "Raja did not get up the cliff onto the summit");
        }

        [UnityTest]
        public IEnumerator HeavyBlowKnocksHimDownAndHeGetsUp()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            game.Teleport(10f);
            yield return Frames(2);
            float hp0 = game.hero.hp;
            game.hero.TakeHit(5, 1, knock: 6f);
            Assert.AreEqual(CbHero.State.Down, game.hero.state, "A heavy blow did not knock Raja down");
            // down, he can't be hurt again until he is up
            yield return Frames(2);
            game.hero.TakeHit(5, 1);
            Assert.AreEqual(hp0 - 5, game.hero.hp, 0.01f, "Raja was hurt again while knocked down");
            bool landed = false;
            for (float waited = 0; waited < 3f && game.hero.state == CbHero.State.Down; waited += Time.deltaTime)
            {
                yield return null;
                landed |= game.hero.y < 0.01f;
            }
            Assert.IsTrue(landed, "Raja never came down");
            Assert.AreEqual(CbHero.State.Idle, game.hero.state, "Raja did not get up again");
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
            // the first soldier, on his own (no arena, no warning)
            while (game.enemies.Count < 1 && waited < 5f) { waited += Time.deltaTime; yield return null; }
            Assert.AreEqual(1, game.enemies.Count, "The first post did not send out one hyena");
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
                    // guard: held the whole phase (a staff lunge or a blow still on him at its start ate a shorter press)
                    case 6: i.guard = true; break;
                    // special: the staff storm, pressed until it starts (a hyena behind him may have him reeling)
                    case 7: game.hero.ki = 100; i.skill = pt >= 0.1f && !sawSpecial; break;
                }
                prev = pt;
            };
            int startScore = game.score;
            float t = 0;
            bool sawStaff = false, sawWhirl = false, sawGuard = false;
            var guardStates = new List<string>();
            // a second round of the moves if one was missed (a guard while he was thrown in the air does not rise)
            while ((t < 9f || (t < 19.5f && !(sawStaff && sawWhirl && sawGuard && sawSpecial))) && game.mode == CbGame.Mode.Play)
            {
                t += Time.deltaTime;
                if (game.hero.state == CbHero.State.Staff) sawStaff = true;
                if (game.hero.move == CbHero.Move.Whirl) sawWhirl = true;
                if (game.hero.state == CbHero.State.Guard) sawGuard = true;
                if (game.hero.state == CbHero.State.Special) sawSpecial = true;
                // what he was doing while GUARD was held (to see what kept it down, if it did)
                string was = game.hero.state + (game.hero.InAir ? " (air)" : "");
                if ((int)((Time.time - t0) / 0.8f) % 8 == 6 && !guardStates.Contains(was)) guardStates.Add(was);
                yield return null;
            }
            game.TestInput = null;
            Assert.Greater(game.score, startScore, "No hit landed");
            Assert.IsTrue(sawStaff, "No staff move was used");
            Assert.IsTrue(sawWhirl, "Holding HEAVY did not whirl the staff");
            Assert.IsTrue(sawGuard, "GUARD did not raise Raja's guard (while it was held he was: " + string.Join(", ", guardStates) + ")");
            Assert.IsTrue(sawSpecial, "SPECIAL did not start the staff storm");
            Assert.IsNull(game.lockZone, "A soldier on his own shut an arena");
        }

        /// <summary>
        /// The jungle's ledges (out of the gorge) are wooden platforms hanging on two ropes from a branch over them, or
        /// from the platform over them (the user: the rock pillars under them looked bad - wood hanging from the
        /// background's trees). No rock under them; the ropes straight up from the back corners. Pictures from on them,
        /// named to run late.
        /// </summary>
        [UnityTest]
        public IEnumerator ZzzLedgesHangOnRopesFromBranches()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            game.hero.inv = 999f;
            foreach (var n in new[] { "WoodPlatform", "WoodPlatformSmall", "MossBranch" })
                Assert.IsNotNull(Resources.Load<GameObject>($"Cloudborne/Jungle/Ways/{n}/{n}"), $"The {n} model is missing");
            int hung = 0;
            foreach (Transform t in game.level.Root)
            {
                if (t.name != "ledge") continue;
                bool gorge = t.position.x > CbLevel.MountainFrom - 1f && t.position.x < CbLevel.MountainTo + 1f;
                var platform = t.Find("woodPlatform");
                if (gorge) { Assert.IsNull(platform, $"The gorge's ledge at x {t.position.x:0.0} hangs on ropes"); continue; }
                Assert.IsNotNull(platform, $"The ledge at x {t.position.x:0.0} is not a hanging platform");
                Assert.IsNull(t.Find("fit"), $"The ledge at x {t.position.x:0.0} still has the cliff's slab");
                var ropes = new List<Transform>();
                foreach (Transform c in t) if (c.name == "hangRope") ropes.Add(c);
                Assert.AreEqual(2, ropes.Count, $"The platform at x {t.position.x:0.0} doesn't hang on two ropes");
                var pb = platform.GetComponentInChildren<Renderer>().bounds;
                foreach (var r in ropes)
                {
                    var rb = r.GetComponent<Renderer>().bounds;
                    // straight up from its back corner, from its top to well over it
                    Assert.Less(rb.size.x, 0.12f, $"A rope at x {t.position.x:0.0} is not straight up");
                    Assert.Greater(rb.center.z, pb.center.z, $"A rope at x {t.position.x:0.0} is not at the platform's back");
                    Assert.AreEqual(pb.max.y, rb.min.y, 0.15f, $"A rope at x {t.position.x:0.0} does not start at the platform's knots");
                    Assert.Greater(rb.max.y, pb.max.y + 1.2f, $"A rope at x {t.position.x:0.0} does not go up to a branch");
                    Debug.Log($"HANG ledge x {t.position.x:0.00} rope x {rb.center.x:0.00} z {rb.center.z:0.00} y {rb.min.y:0.00}..{rb.max.y:0.00} platform top {pb.max.y:0.00}");
                }
                // no rock column under it
                Assert.IsFalse(System.Array.Exists(GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None),
                    o => o.name == "ledgeSupport" && Mathf.Abs(o.position.x - t.position.x) < 0.05f), $"A rock column still stands under the ledge at x {t.position.x:0.0}");
                hung++;
            }
            Assert.GreaterOrEqual(hung, 8, "Too few of the jungle's ledges hang from branches");
            Assert.IsNotNull(GameObject.Find("hangBranch"), "No branch for the platforms to hang from");
            yield return TakePictures(new[] { ("hang_shrine", CbLevel.ShrineLedgeX, CbLevel.ShrineLedgeTop), ("hang_shrine_below", CbLevel.ShrineLedgeX - 2.5f, 0f),
                ("hang_238", 238f, 6.2f), ("hang_canopy", 380.6f, 9.2f), ("hang_waterfall", 401f, 2.6f), ("hang_waterfall_top", 401.2f, 7f) });
        }

        /// <summary>
        /// Not a check: the streams seen from their banks (the user: water clear like in their picture - the bed, the
        /// banks under the water, stones and weed showing through). Named to run late, after the stage's pictures.
        /// </summary>
        [UnityTest]
        public IEnumerator ZzzStreamsSeenThroughTheirClearWater()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            game.hero.inv = 999f;
            Assert.IsNotNull(GameObject.Find("streamBed"), "The streams have no bed to see through their water");
            var top = Resources.Load<Shader>("Shaders/WaterTop");
            Assert.IsNotNull(top, "The water-top shader is missing");
            if (top.isSupported)
                Assert.AreEqual("Cloudborne/WaterTop", GameObject.Find("water").GetComponent<Renderer>().sharedMaterial.shader.name, "The streams' top is not the rippling water");
            // (and the ground's front edge on open ground: the face's own painted grass, no lip laid over it)
            yield return TakePictures(new[] { ("stream_long", 77.5f, 0f), ("stream_vines", 198.5f, 0f), ("stream_raft", 308.5f, 0f), ("ground_edge", 12f, 0f) });
        }

        [UnityTest]
        public IEnumerator RajasHeadbandTailStirsALittle()
        {
            // the user: standing still, his headband's tail moves just slightly (a bone added for it: CbHeadband)
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var model = game.hero.model;
            Assert.IsNotNull(model, "Raja's model did not load");
            Assert.IsNotNull(model.headband, "Raja's headband tail was not found on his model");
            Assert.Contains(model.headband.transform, model.skin.bones, "The headband tail's bone does not move his skin");
            float most = 0;
            var first = model.headband.transform.localRotation;
            for (float w = 0; w < 1.5f; w += Time.deltaTime)
            {
                most = Mathf.Max(most, Quaternion.Angle(first, model.headband.transform.localRotation));
                yield return null;
            }
            Debug.Log($"HEADBAND swayed up to {most:F1} degrees standing still");
            Assert.Greater(most, 1f, "Raja's headband tail does not stir");
            Assert.Less(most, 30f, "Raja's headband tail flaps too much standing still");
        }

        [UnityTest]
        public IEnumerator ABigBlowKnocksASoldierDownAndHeTakesHisTimeGettingUp()
        {
            // the user: struck down, they were up again at once and straight back into the fight
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            // (near the first post, as the first fight's test stands him: it sends its soldier out as he comes)
            game.Teleport(14f);
            yield return Frames(2);
            var hero = game.hero;
            hero.inv = 999f;
            float waited = 0;
            while (game.enemies.Count < 1 && waited < 6f) { waited += Time.deltaTime; yield return null; }
            Assert.AreEqual(1, game.enemies.Count, "The first post sent out no soldier");
            var e = game.enemies[0];
            e.hp = 999f;
            // a finisher's blow
            game.HurtEnemy(e, 1f, e.x >= hero.x ? 1 : -1, 8f, 5f, true, false, false, true);
            Assert.IsTrue(e.Floored, "A big blow did not knock the soldier down");
            float down = 0, rising = 0;
            bool struck = false;
            while (e.Floored && down < 5f)
            {
                down += Time.deltaTime;
                if (e.Rising) rising += Time.deltaTime;
                if (e.state == CbEnemy.State.Atk) struck = true;
                yield return null;
            }
            Debug.Log($"KNOCKDOWN down {down:F2} s, getting up {rising:F2} s");
            Assert.IsFalse(e.Floored, "The soldier never got up");
            Assert.IsFalse(struck, "The soldier struck out while he was down");
            Assert.Greater(down, CbEnemy.LieTime + CbEnemy.RiseTime * 0.8f, "The soldier was up again at once");
            Assert.Greater(rising, CbEnemy.RiseTime * 0.8f, "The soldier sprang up instead of getting up");
            // and up, a moment before he swings again
            float calm = 0;
            while (e.state != CbEnemy.State.Atk && calm < 0.5f) { calm += Time.deltaTime; yield return null; }
            Assert.GreaterOrEqual(calm, 0.5f, "Up again, the soldier swung at once");
        }

        [UnityTest]
        public IEnumerator AtTheStartAnInvisibleWallIsBehindHim()
        {
            // the user: nothing behind Raja at the start - no gate there, an invisible wall he can't pass
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var hero = game.hero;
            hero.inv = 999f;
            foreach (var t in game.level.Root.GetComponentsInChildren<Transform>(true))
                Assert.IsFalse(t.name == "endGate" && t.localPosition.x < 10f, "A gate still stands behind him at the start");
            game.TestInput = i => i.x = -1;
            yield return new WaitForSeconds(1.5f);
            game.TestInput = null;
            Assert.GreaterOrEqual(hero.x, CbWorld.StartWall - 0.01f, "Raja went back past the start");
            Assert.Less(hero.x, CbWorld.StartWall + 0.3f, "Raja did not walk back to the wall behind him");
        }

        [UnityTest]
        public IEnumerator ButterfliesScatterAndBirdsCrossTheSky()
        {
            // the jungle's small life (it stood still): butterflies flutter about the meadow behind the path and scatter
            // up and away when he comes by; now and then a flock of birds crosses the sky
            game = CbGame.Boot();
            yield return Frames(3);
            var critters = game.critters;
            Assert.IsNotNull(critters);
            if (critters.Butterflies == 0) Assert.Ignore("The butterfly's painting is not in the project");
            game.StartGame();
            game.SkipFights();
            // a patch on the open ground near the start, and Raja well away from it
            var patch = critters.ButterfliesNear(12f, 8f);
            Assert.Greater(patch.Count, 0, "No butterflies near the start");
            var home = patch[0].home;
            Assert.Greater(home.z, 2f, "A butterfly flutters on the path, not behind it");
            game.Teleport(Mathf.Max(home.x - 12f, -5f));
            game.hero.y = 0;
            yield return new WaitForSeconds(0.6f);
            var calm = critters.ButterfliesNear(home.x, 0.01f)[0];
            Assert.Less(calm.fear, 0.05f, "A butterfly is frightened with Raja far off");
            Assert.Less(Mathf.Abs(calm.at.y - home.y), 0.6f, "A calm butterfly strays from its patch");
            // he comes by: it flies up and away
            game.Teleport(home.x - 0.5f);
            yield return new WaitForSeconds(1f);
            var fled = critters.ButterfliesNear(home.x, 0.01f)[0];
            Assert.Greater(fled.fear, 0.8f, "A butterfly isn't frightened with Raja beside it");
            Assert.Greater(fled.at.y, home.y + 1f, "A frightened butterfly doesn't fly up");
            Assert.Greater(fled.at.x, home.x + 0.5f, "A frightened butterfly doesn't fly away from him");
            if (!critters.HasBirds) yield break;
            critters.FlockNow();
            yield return Frames(3);
            Assert.Greater(critters.BirdsFlying, 1, "No flock of birds set off");
            // by the long river, a fish leaps out of it
            if (!critters.HasFish) yield break;
            game.Teleport(78f);
            game.hero.y = 0;
            yield return new WaitForSeconds(1.5f);
            critters.FishNow();
            yield return Frames(3);
            Assert.Greater(critters.FishLeaping, 0, "No fish leapt from the river in view");
        }

        [UnityTest]
        public IEnumerator SoldiersStandAloneAndLanternsBringHimBack()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            // the first soldier steps out on his own as Raja draws near: no arena, no warning
            game.Teleport(12f);
            yield return Frames(3);
            Assert.AreEqual(1, game.enemies.Count, "Not one soldier on his own at the first post");
            Assert.IsNull(game.lockZone, "A soldier on his own shut an arena");
            // a stone lantern passed: fallen, Raja comes back there, whole again
            game.SkipFights();
            game.Teleport(108.5f);
            yield return Frames(3);
            Assert.AreEqual(108f, game.CheckpointX, 0.01f, "Passing the lantern did not make it the checkpoint");
            game.Teleport(125f);
            yield return Frames(2);
            game.hero.TakeHit(9999, 1);
            for (float w = 0; w < 4f && !(game.hero.state == CbHero.State.Idle && Mathf.Abs(game.hero.x - 108f) < 0.5f); w += Time.deltaTime) yield return null;
            Assert.AreEqual(CbGame.Mode.Play, game.mode, "Falling ended the run");
            Assert.AreEqual(108f, game.hero.x, 0.5f, "Raja did not come back at the lantern");
            Assert.AreEqual(game.hero.MaxHp, game.hero.hp, 0.01f, "Raja did not come back whole");
        }

        /// <summary>
        /// Not a check: pictures of the stage as the game camera sees it, for looking at the art from CI. Each is saved
        /// in artifacts/shots and written to the log in base64 lines ("SHOT name i data", then "SHOT name END n").
        /// Where nothing can be drawn (no graphics device) it says so and passes.
        /// </summary>
        [UnityTest]
        public IEnumerator ZzPicturesOfTheStage()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            yield return TakePictures(new[] { ("start", 6f, 0f), ("trunk", 21.5f, 0f), ("bridge", 40f, 0f), ("river", 75f, 0f), ("gate", 133f, 0f), ("gorge", 145.1f, 5.4f), ("summit", 154f, CbLevel.SummitTop), ("bars", 219f, 4.8f - 1.71f),
                ("thorns", 243f, 0f), ("temple", 266f, 0f), ("waterfall", 392.5f, 0f), ("stonebridge", CbLevel.StoneBridgeX - 6f, CbLevel.StoneDeck),
                ("tunnel", CbLevel.RopeMouthL + 2.5f, 0f), ("shrine_ledge", CbLevel.ShrineLedgeX + 3f, 0f),
                ("stairs", CbLevel.StoneBridgeX - CbLevel.StoneBridgeHalf - 3f, 0f), ("vine_tree", 199f, 0f) });
            // no ruin towers anywhere (the user took them all out: the stage is to be laid out anew from a Canva plan) -
            // the shrine's high ledge is still there, on its column of rock
            Assert.IsFalse(System.Array.Exists(game.level.Root.GetComponentsInChildren<Transform>(true), t => t.name == "ruinTower" || t.name == "towerLedge"),
                "A ruin tower is still in the stage");
            Assert.AreEqual(CbLevel.ShrineLedgeTop, game.level.SupportAt(CbLevel.ShrineLedgeX, CbLevel.ShrineLedgeTop + 0.5f), 0.01f, "The shrine's high ledge is gone");
            // nothing at the start: its springy leaf is gone (the user)
            Assert.IsFalse(System.Array.Exists(CbLevel.LeafXs, x => x < 30f), "A springy leaf is still at the start");
            // and the same tunnel with its gate sunk, as for an ambush's raiders
            // (called down every frame while the picture is taken: once, it rose again before the picture)
            game.TestInput = i => game.level.CallTunnel(CbLevel.RopeMouthL);
            yield return TakePictures(new[] { ("tunnel_open", CbLevel.RopeMouthL - 1.5f, 0f) }, 2.5f);
            game.TestInput = null;
        }

        /// <summary>
        /// Not a check: stage 1-4 photographed every 40 metres from start to end, on the river where he rides, for going
        /// over the whole stage for glitches (the user: stage by stage; 1-1 to 1-3 done).
        /// </summary>
        [UnityTest]
        public IEnumerator ZzSurveyOfTheRiver()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-4");
            game.BuildStage();
            game.StartGame();
            game.SkipFights();
            game.hero.inv = 9999f;
            yield return Frames(2);
            var spots = new List<(string, float, float)>();
            for (float x = 10f; x < CbWorld.GoalX + 4f; x += 40f)
                spots.Add(($"survey14_{(int)x:000}", x, CbLevel.RiverFloor(x)));
            // and each waterfall from just above it
            foreach (var f in CbLevel.Falls) spots.Add(($"survey14_falls{(int)f}", f - 7f, CbLevel.RiverFloor(f - 7f)));
            yield return TakePictures(spots.ToArray(), 1.4f);
            CbStage.Set("1-1");
        }

        [UnityTest]
        public IEnumerator ZzTreetopsBridgeAndHollow()
        {
            // stage 1-2's first rope bridge close up (its ends on their posts, the bough ends either side, the drop under
            // it) and the firefly hollow's trunk
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-2");
            game.BuildStage();
            game.StartGame();
            game.SkipFights();
            game.hero.inv = 9999f;
            game.hero.hp = 9999;
            yield return Frames(2);
            yield return TakePictures(new[]
            {
                ("tt_bridge_bank", 21.6f, 0f), ("tt_bridge_end", 23.6f, 0.4f), ("tt_bridge_mid", 27.5f, 0.4f),
                ("tt_hollow", CbLevel.HollowX - 2.5f, 0f),
            }, 1.2f);
            CbStage.Set("1-1");
        }

        [UnityTest]
        public IEnumerator ZzSurveyOfTheTemple()
        {
            // stage 1-5's glitch pass: a picture every 10 m along the way in and the courtyard
            CbSave.FireAwake = false;
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-5");
            game.BuildStage();
            game.StartGame();
            game.SkipFights();
            game.hero.inv = 9999f;
            game.hero.hp = 9999;
            yield return Frames(2);
            var spots = new List<(string, float, float)>();
            for (float x = 2f; x < CbWorld.GoalX + 2f; x += 10f)
                spots.Add(($"survey15_{(int)x:000}", x, 0f));
            yield return TakePictures(spots.ToArray(), 1.4f);
            CbStage.Set("1-1");
        }

        [UnityTest]
        public IEnumerator TheTreetopsStageIsBuiltAndPlays()
        {
            // stage 1-2 (the user: on to the next stage): built when chosen, its own length, gaps, drones, bridges
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-2");
            game.BuildStage();
            game.StartGame();
            game.SkipFights();
            yield return Frames(3);
            Assert.AreEqual(CbLevel.TreetopsGoal, CbWorld.GoalX, 0.01f, "Stage 1-2 has stage 1-1's goal");
            Assert.Greater(game.level.SawCount, 3, "Stage 1-2 has too few saw drones");
            Assert.IsTrue(CbLevel.InWater(70f), "No gap between the boughs at the vine gap");
            // the rope bridge over the first gap holds him
            Assert.Greater(game.level.SupportAt(27.5f, 1f), -0.5f, "The first rope bridge does not hold him");
            // down a gap: health lost, back on the bough he stood on
            game.Teleport(58f);
            game.hero.y = 0;
            yield return new WaitForSeconds(0.4f);
            float hp = game.hero.hp;
            game.Teleport(70f);
            game.hero.y = 1f;
            game.hero.vy = 0;
            game.hero.state = CbHero.State.Air;
            for (float w = 0; w < 2.5f && game.hero.hp >= hp; w += Time.deltaTime) yield return null;
            Assert.Less(game.hero.hp, hp, "Falling down a gap cost no health");
            Assert.Less(game.hero.x, 62f, "Falling down a gap did not put him back on the bough");
            // a saw drone: a paw clangs off it, two staff blows smash it
            yield return new WaitForSeconds(1.2f);
            var blade = game.level.SawBlade(0);
            int flying = game.level.SawsFlying;
            game.level.Strike(blade.x - 1f, blade.y - 1f, 1, 2.2f, staff: false);
            Assert.AreEqual(flying, game.level.SawsFlying, "A paw smashed a saw drone");
            blade = game.level.SawBlade(0);
            game.level.Strike(blade.x - 1f, blade.y - 1f, 1, 2.2f, staff: true);
            blade = game.level.SawBlade(0);
            game.level.Strike(blade.x - 1f, blade.y - 1f, 1, 2.2f, staff: true);
            Assert.AreEqual(flying - 1, game.level.SawsFlying, "Two staff blows did not smash the saw drone");
            // the end: at the goal the stage is cleared
            game.Teleport(CbWorld.GoalX + 0.5f);
            game.hero.y = 0;
            for (float w = 0; w < 2f && game.mode == CbGame.Mode.Play && !game.Cleared; w += Time.deltaTime) yield return null;
            Assert.IsTrue(game.Cleared, "Reaching the loggers' camp's end did not clear stage 1-2");
            // and back to stage 1-1: its rivers again
            CbStage.Set("1-1");
            game.BuildStage();
            Assert.IsTrue(CbLevel.InWater(90f), "Stage 1-1's long river is gone after playing stage 1-2");
            Assert.AreEqual(488f, CbWorld.GoalX, 0.01f, "Stage 1-1's goal did not come back");
        }

        [UnityTest]
        public IEnumerator BabasGroveTrialsThenTheWayOn()
        {
            // stage 1-3 (the story doc): ring five bells in order, break ten bamboo dummies with the staff storm, send
            // three stones back with the perfect guard - each trial shuts the way until it is done
            CbSave.PerfectGuard = false;
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-3");
            game.BuildStage();
            game.StartGame();
            yield return Frames(2);
            var hero = game.hero;
            var level = game.level;
            Assert.IsFalse(CbLevel.InWater(40f), "Baba's grove has water in it");
            Assert.AreEqual(CbLevel.GroveGoal, CbWorld.GoalX, 0.01f, "Stage 1-3 has another stage's goal");
            // the goal stays shut before the trials
            game.Teleport(CbWorld.GoalX + 0.5f);
            hero.y = 0;
            yield return Frames(3);
            Assert.IsFalse(game.Cleared, "Stage 1-3 cleared without Baba's trials");
            // the bells: Baba rings the order, a wrong bell starts it over, the right order passes
            game.Teleport(CbLevel.TrialX[0] - 8f);
            hero.y = 0;
            for (float w = 0; w < 2f && level.Trial != 0; w += Time.deltaTime) yield return null;
            Assert.AreEqual(0, level.Trial, "The bells' trial did not start");
            Assert.IsNotNull(game.lockZone, "The bells' trial did not shut the way");
            for (float w = 0; w < 8f && level.BellsDemoing; w += Time.deltaTime) yield return null;
            Assert.IsFalse(level.BellsDemoing, "Baba never finished ringing the order");
            int wrong = level.BellInOrder(1);
            level.Strike(level.BellX(wrong) - 1f, level.BellTop(wrong) + 0.5f, 1, 1.5f, staff: false);
            Assert.AreEqual(0, level.BellsRung, "A wrong bell counted");
            Assert.IsTrue(level.BellsDemoing, "After a wrong bell Baba did not ring the order again");
            for (float w = 0; w < 10f && level.BellsDemoing; w += Time.deltaTime) yield return null;
            for (int k = 0; k < CbLevel.BellCount; k++)
            {
                int i = level.BellInOrder(k);
                level.Strike(level.BellX(i) - 1f, level.BellTop(i) + 0.5f, 1, 1.5f, staff: false);
                yield return Frames(2);
            }
            Assert.AreEqual(CbLevel.BellCount, level.BellsRung, "Ringing the bells in Baba's order did not pass");
            yield return Frames(3);
            Assert.IsNull(game.lockZone, "The way did not open after the bells");
            // the dummies: blows only rock them (and fill his ki); the staff storm breaks them
            game.Teleport(CbLevel.TrialX[1] - 9.2f);
            hero.y = 0;
            for (float w = 0; w < 2f && level.Trial != 1; w += Time.deltaTime) yield return null;
            Assert.AreEqual(1, level.Trial, "The dummies' trial did not start");
            hero.ki = 0;
            level.Strike(level.DummyX(0) - 1f, 0, 1, 1.5f, staff: true);
            Assert.AreEqual(0, level.DummiesBroken, "A plain blow broke a dummy");
            Assert.Greater(hero.ki, 0f, "Hitting a dummy did not fill his ki");
            for (int tries = 0; tries < 14 && level.DummiesBroken < CbLevel.DummyCount; tries++)
            {
                int next = 0;
                while (level.DummiesBroken < CbLevel.DummyCount && next < CbLevel.DummyCount && IsBroken(next)) next++;
                game.Teleport(level.DummyX(next) - 1.3f);
                hero.y = 0;
                hero.facing = 1;
                hero.state = CbHero.State.Idle;
                hero.ki = 100;
                yield return Frames(2);
                bool pressed = false;
                game.TestControls = i => { i.skill = !pressed; pressed = true; };
                for (float w = 0; w < 0.3f && hero.state != CbHero.State.Special; w += Time.deltaTime) yield return null;
                game.TestControls = null;
                for (float w = 0; w < 3f && hero.state == CbHero.State.Special; w += Time.deltaTime) yield return null;
            }
            Assert.AreEqual(CbLevel.DummyCount, level.DummiesBroken, "The staff storm did not break all ten dummies");
            yield return Frames(3);
            Assert.IsNull(game.lockZone, "The way did not open after the dummies");
            // the stones: Baba teaches the perfect guard; three swipes into his stones send them back
            game.Teleport(CbLevel.TrialX[2] - 8.5f);
            hero.y = 0;
            hero.facing = 1;
            for (float w = 0; w < 2f && level.Trial != 2; w += Time.deltaTime) yield return null;
            Assert.AreEqual(2, level.Trial, "The stones' trial did not start");
            Assert.IsTrue(CbHero.PerfectGuardLearned, "Baba did not teach the perfect guard");
            hero.hp = 9999;
            for (float w = 0; w < 25f && level.StonesReturned < CbLevel.StonesToReturn; w += Time.deltaTime)
            {
                yield return null;
                hero.facing = 1;
                if (!level.ThrownComing(hero.x, hero.y, 1, 2.2f) || hero.state != CbHero.State.Idle) continue;
                bool swiped = false;
                game.TestControls = i => { if (!swiped) { i.dash = true; i.dashDir = 1; } swiped = true; };
                yield return Frames(2);
                game.TestControls = null;
            }
            Assert.AreEqual(CbLevel.StonesToReturn, level.StonesReturned, "Three stones were not sent back to Baba");
            yield return Frames(3);
            Assert.IsNull(game.lockZone, "The way did not open after the stones");
            Assert.AreEqual(CbLevel.Trials, game.TrialsDone, "Not every trial counted as done");
            // the way on: the stage clears at its end
            game.Teleport(CbWorld.GoalX + 0.5f);
            hero.y = 0;
            for (float w = 0; w < 3f && !game.Cleared; w += Time.deltaTime) yield return null;
            Assert.IsTrue(game.Cleared, "Stage 1-3 did not clear after Baba's trials");
            CbSave.PerfectGuard = false;
            CbStage.Set("1-1");
            game.BuildStage();

            bool IsBroken(int i) => level.DummyBroken(i);
        }

        [UnityTest]
        public IEnumerator TheRiverIsRiddenToTheSawBarge()
        {
            // stage 1-4 (the story doc): riding a log down the river - rocks to jump, branches to duck, barrels to smash,
            // two waterfalls, golden lotuses up high; the saw barge in the basin at the end; then Koda's cage is taken away
            CbSave.PerfectGuard = true;
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-4");
            game.BuildStage();
            game.StartGame();
            yield return Frames(3);
            var hero = game.hero;
            var level = game.level;
            // a steady 30 frames a second, the game's time and the test's going together (on CI's uneven frames
            // a slow one used up the test's second of waiting while the game went on a twentieth of one: he never
            // reached the first rock)
            Time.captureDeltaTime = game.TestStep = 1f / 30f;
            Assert.IsTrue(game.Riding, "Raja is not riding the river");
            hero.hp = 9999;
            float x0 = hero.x;
            yield return new WaitForSeconds(1f);
            Assert.Greater(hero.x - x0, 6f, "The river did not carry him along");
            Assert.AreEqual(0f, hero.y, 0.3f, "Raja is not on his log on the water");
            // a rock in the way hurts him
            game.Teleport(level.RockX(0) - 3f);
            hero.y = 0;
            float hp = hero.hp;
            for (float w = 0; w < 1f && hero.hp >= hp; w += Time.deltaTime) yield return null;
            Assert.Less(hero.hp, hp, "Running into a rock did not hurt");
            yield return new WaitForSeconds(1.2f);
            // ducking (the stick held down) he passes under a low branch
            game.Teleport(level.BranchX(0) - 4f);
            hero.y = 0;
            hero.inv = 0;
            hero.state = CbHero.State.Idle;
            yield return Frames(2);
            hp = hero.hp;
            bool first = true;
            game.TestControls = i => { i.y = -1; i.slide = first; first = false; };
            for (float w = 0; w < 1f && hero.x < level.BranchX(0) + 1f; w += Time.deltaTime) yield return null;
            game.TestControls = null;
            Assert.Greater(hero.x, level.BranchX(0), "He was not carried past the low branch");
            Assert.AreEqual(hp, hero.hp, 0.01f, "Ducking under a low branch still hurt him");
            // the staff smashes an oil barrel
            game.Teleport(level.BarrelX(1) - 2.2f);
            hero.y = 0;
            level.Strike(level.BarrelX(1) - 1.4f, 0f, 1, 2f, staff: true);
            Assert.IsTrue(level.BarrelBroken(1), "A blow did not smash the oil barrel");
            // over the first waterfall: down a level, still on his log (not swimming)
            game.Teleport(CbLevel.Falls[0] - 4f);
            hero.y = 0;
            for (float w = 0; w < 2.5f && hero.y > CbLevel.RiverFloor(CbLevel.Falls[0] + 1f) + 0.1f; w += Time.deltaTime) yield return null;
            Assert.AreEqual(CbLevel.RiverFloor(CbLevel.Falls[0] + 1f), hero.y, 0.3f, "He did not go down with the waterfall onto the lower river");
            Assert.AreNotEqual(CbHero.State.Swim, hero.state, "After the waterfall he swims instead of riding");
            // a golden lotus up on the first route
            var lotus = CbLevel.LotusAt(0);
            game.Teleport(lotus.x - 0.2f);
            hero.y = lotus.y - 1.05f;
            hero.vy = 0;
            for (float w = 0; w < 1f && game.Tokens < 1; w += Time.deltaTime) yield return null;
            Assert.AreEqual(1, game.Tokens, "The golden lotus up on the route was not taken");
            // the basin: the saw barge
            game.Teleport(CbLevel.BasinX + 2f);
            hero.y = CbLevel.RiverFloor(CbLevel.BasinX);
            for (float w = 0; w < 3f && !level.SawBargeFighting; w += Time.deltaTime) yield return null;
            Assert.IsTrue(level.SawBargeFighting, "The saw barge did not come");
            Assert.IsNotNull(game.lockZone, "The basin did not shut for the saw barge");
            Assert.IsFalse(game.Riding, "Still riding in the basin");
            // one of its logs sent back with the perfect guard bursts a fuel barrel
            for (float w = 0; w < 12f && level.FuelLeft == 3; w += Time.deltaTime)
            {
                yield return null;
                if (!level.ThrownComing(hero.x, hero.y, -1, 2.2f) || hero.state != CbHero.State.Idle) continue;
                bool swiped = false;
                game.TestControls = i => { if (!swiped) { i.dash = true; i.dashDir = -1; } swiped = true; };
                yield return Frames(2);
                game.TestControls = null;
            }
            Assert.AreEqual(2, level.FuelLeft, "A log sent back did not burst a fuel barrel");
            // the other two with the staff: it sinks, Koda's barge breaks up, his cage goes up
            for (int i = 0; i < 3; i++)
                level.Strike(level.FuelX(i) + 1f, CbLevel.RiverFloor(CbLevel.BasinX) + 0.5f, -1, 1.6f, staff: true);
            Assert.AreEqual(0, level.FuelLeft, "The staff did not knock down the fuel barrels");
            Assert.IsTrue(level.SawBargeBeaten, "With its barrels gone the saw barge did not sink");
            yield return Frames(2);
            Assert.IsTrue(game.Cleared, "Stage 1-4 was not cleared");
            yield return WaitForMode(CbGame.Mode.Over, 6f);
            Assert.AreEqual(CbGame.Mode.Over, game.mode, "Stage 1-4 did not end");
            CbSave.PerfectGuard = false;
            CbStage.Set("1-1");
            game.BuildStage();
        }

        [UnityTest]
        public IEnumerator ZzRiverPictures()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-4");
            game.BuildStage();
            game.StartGame();
            yield return Frames(2);
            game.hero.hp = 9999;
            game.hero.inv = 999f;
            // (he is carried along while the camera settles: put him that far before each spot; at a steady 30 frames
            // a second, so the game's time goes as the wait's)
            Time.captureDeltaTime = game.TestStep = 1f / 30f;
            float lead = CbLevel.RideSpeed * 2f;
            game.Teleport(game.level.RockX(1) - 3f - lead);
            yield return TakePictures(new[] { ("river_rocks", float.NaN, 0f) }, 2f);
            game.Teleport(CbLevel.Falls[0] - 1f - lead);
            yield return TakePictures(new[] { ("river_falls", float.NaN, 0f) }, 2f);
            game.Teleport(176f - lead);
            yield return TakePictures(new[] { ("river_route", float.NaN, 0f) }, 2f);
            game.Teleport(CbLevel.BasinX + 2f);
            game.hero.y = CbLevel.RiverFloor(CbLevel.BasinX);
            // its cut-in: the band with its name and its saws close up
            yield return TakePictures(new[] { ("barge_cut_in", float.NaN, 0f) }, 0.45f);
            Assert.IsTrue(game.hud.CuttingIn, "The saw barge came on with no cut-in");
            Assert.IsTrue(game.CloseUpOn, "No close-up of the saw barge in its cut-in");
            yield return TakePictures(new[] { ("river_barge", float.NaN, 0f) }, 4f);
        }

        [UnityTest]
        public IEnumerator RajasFireWakesBeforeGraksThreePhases()
        {
            // stage 1-5: Raja's fire wakes before the fight (the user: he fights Grak with his fire); then Commander Grak - his armour (charges into the
            // pillars, his saw), the fire round the arena (beaten back, filling ki), his burning saw (fire, waves); beaten,
            // the end of chapter 1 (drawn)
            CbSave.FireAwake = false;
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-5");
            game.BuildStage();
            game.StartGame();
            Time.captureDeltaTime = game.TestStep = 1f / 30f;
            yield return Frames(2);
            var hero = game.hero;
            var level = game.level;
            hero.hp = 9999;
            game.SkipFights();
            game.SkipToBoss();
            // (his fight starts 7 metres before the middle of the courtyard)
            game.Teleport(CbLevel.GrakX - 6.5f);
            hero.y = 0;
            yield return Frames(3);
            // his fire wakes as the fight begins (the cutscenes off: no scene before it)
            Assert.AreEqual(CbGame.Mode.Play, game.mode, "Grak's fight did not begin");
            Assert.IsTrue(game.FireAwakened, "Raja's fire did not wake before the fight");
            Assert.IsTrue(CbSave.FireAwake, "Raja's fire woke but was not kept");
            Assert.AreEqual(100f, hero.ki, 0.5f, "His fire woke but his ki is not full");
            yield return Frames(2);
            var grak = game.boss;
            Assert.IsNotNull(grak, "Grak did not come");
            Assert.AreEqual(CbEnemy.Kind.Grak, grak.kind);
            Assert.IsNotNull(grak.model, "Grak's model did not load");
            // his iron: a blow from in front glances off
            float hp = grak.hp;
            game.HurtEnemy(grak, 10f, -grak.facing, 2f, 0f, false, false, fire: false, fromPlayer: true);
            Assert.AreEqual(hp, grak.hp, 0.01f, "A blow from in front got through Grak's armour");
            // far from Raja he charges - into the pillar, winded: now he is open
            for (float w = 0; w < 12f && grak.grakMove != CbEnemy.GrakMove.Dazed; w += Time.deltaTime)
            {
                yield return null;
                if (grak.grakMove != CbEnemy.GrakMove.Charge && grak.grakMove != CbEnemy.GrakMove.Scrape) { hero.x = grak.x < CbLevel.GrakX ? CbLevel.GrakX + 9f : CbLevel.GrakX - 9f; hero.y = 0; }
            }
            Assert.AreEqual(CbEnemy.GrakMove.Dazed, grak.grakMove, "Grak's charge did not end against a pillar");
            Assert.IsTrue(grak.GrakOpen, "Winded against the pillar, Grak's guard is still up");
            hp = grak.hp;
            game.HurtEnemy(grak, 10f, -grak.facing, 2f, 0f, false, false, fire: false, fromPlayer: true);
            Assert.Less(grak.hp, hp, "A blow on Grak while he is winded did nothing");
            // two thirds down: the arena's edges catch fire and creep in
            grak.hp = grak.max * 0.6f;
            for (float w = 0; w < 10f && !level.FireOn; w += Time.deltaTime) yield return null;
            Assert.IsTrue(level.FireOn, "The fire did not catch round the arena");
            Assert.AreEqual(2, grak.GrakPhase);
            float left = level.FireLeftX;
            yield return new WaitForSeconds(1f);
            Assert.Greater(level.FireLeftX, left, "The fire did not creep in");
            // a blow on it beats it back, and its heat fills his ki
            left = level.FireLeftX;
            hero.x = left + 1.3f; hero.y = 0; hero.ki = 0;
            level.Strike(hero.x, 0.5f, -1, 1.5f, staff: true);
            Assert.Less(level.FireLeftX, left, "A blow did not beat the fire back");
            Assert.Greater(hero.ki, 0f, "The fire's heat did not fill Raja's ki");
            hero.x = CbLevel.GrakX - 2f;
            // a third down: his saw catches fire, and fire comes off it
            grak.hp = grak.max * 0.3f;
            for (float w = 0; w < 10f && !grak.SawAblaze; w += Time.deltaTime) yield return null;
            Assert.IsTrue(grak.SawAblaze, "Grak's saw did not catch fire");
            bool fire = false;
            var moves = new List<string>();
            for (float w = 0; w < 30f && !fire; w += Time.deltaTime)
            {
                yield return null;
                fire = grak.FireWaves > 0 || level.ThrownComing(hero.x, 0f, hero.x < grak.x ? 1 : -1, 30f);
                if (moves.Count == 0 || moves[moves.Count - 1] != grak.grakMove.ToString()) moves.Add(grak.grakMove.ToString());
                if (Mathf.Abs(hero.x - grak.x) < 4f) hero.x = grak.x < CbLevel.GrakX ? CbLevel.GrakX + 6f : CbLevel.GrakX - 6f;
            }
            Assert.IsTrue(fire, $"With his saw ablaze Grak flung no fire and sent out no waves (his moves: {string.Join(", ", moves)}; Raja at {hero.x:F1}, Grak at {grak.x:F1})");
            // beaten: then the end of chapter 1 (drawn), and the stage is clear
            game.HurtEnemy(grak, 9999f, 1, 2f, 0f, true, false, fire: true, fromPlayer: true, pierce: true);
            Assert.IsTrue(grak.dead, "Grak could not be beaten");
            yield return Frames(2);
            Assert.IsTrue(game.Cleared, "Stage 1-5 was not cleared");
            yield return WaitForMode(CbGame.Mode.Over, 8f);
            Assert.AreEqual(CbGame.Mode.Over, game.mode, "Stage 1-5 did not end");
            CbSave.FireAwake = false;
            CbStage.Set("1-1");
            game.BuildStage();
        }

        [UnityTest]
        public IEnumerator ZzEmberPictures()
        {
            CbSave.FireAwake = false;
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-5");
            game.BuildStage();
            game.StartGame();
            Time.captureDeltaTime = game.TestStep = 1f / 30f;
            yield return Frames(2);
            game.hero.hp = 9999;
            game.hero.inv = 999f;
            yield return TakePictures(new[] { ("ember_start", 6f, 0f) }, 2f);
            game.SkipFights();
            game.SkipToBoss();
            game.Teleport(CbLevel.GrakX - 6.5f);
            game.hero.y = 0;
            // his cut-in: the band with his name, his own face close up from a second camera (checked at once: the fire
            // waking slows the game a moment, and a wait in game time outlasted the band)
            yield return Frames(2);
            Assert.IsTrue(game.hud.CuttingIn, "Grak came on with no cut-in");
            Assert.IsTrue(game.CloseUpOn, "No close-up of Grak's face in his cut-in");
            yield return TakePictures(new[] { ("grak_cut_in", float.NaN, 0f) }, 0.1f);
            yield return TakePictures(new[] { ("ember_grak", float.NaN, 0f) }, 0.75f);
            var grak = game.boss;
            Assert.IsNotNull(grak, "Grak did not come");
            for (float w = 0; w < 10f && grak.grakMove != CbEnemy.GrakMove.Swing && grak.grakMove != CbEnemy.GrakMove.Chop && grak.grakMove != CbEnemy.GrakMove.Charge; w += Time.deltaTime)
                yield return null;
            yield return TakePictures(new[] { ("ember_blow", float.NaN, 0f) }, 0.4f);
            grak.hp = grak.max * 0.6f;
            for (float w = 0; w < 10f && !game.level.FireOn; w += Time.deltaTime) yield return null;
            yield return TakePictures(new[] { ("ember_fire", float.NaN, 0f) }, 1.5f);
            grak.hp = grak.max * 0.3f;
            for (float w = 0; w < 10f && !grak.SawAblaze; w += Time.deltaTime) yield return null;
            for (float w = 0; w < 10f && grak.grakMove != CbEnemy.GrakMove.Fire && grak.grakMove != CbEnemy.GrakMove.Slam; w += Time.deltaTime) yield return null;
            yield return TakePictures(new[] { ("ember_saw", float.NaN, 0f) }, 0.7f);
            // his fire awake: his stripes glow gold
            Assert.IsTrue(CbSave.FireAwake, "Raja's fire was not awake in Grak's fight");
            game.Teleport(CbLevel.GrakX - 8f);
            yield return TakePictures(new[] { ("ember_awake", float.NaN, 0f) }, 0.6f);
            CbSave.FireAwake = false;
        }

        [UnityTest]
        public IEnumerator GraphicsLevelsChooseWhatIsDrawn()
        {
            // the graphics' levels (each phone differs: the user): SETTINGS cycles AUTO, LOW, MEDIUM, HIGH; a lower level
            // draws fewer sparks and no bloom
            game = CbGame.Boot();
            yield return Frames(2);
            int was = CbQuality.Setting;
            CbQuality.Setting = 1;
            Assert.AreEqual(CbQuality.Tier.Low, CbQuality.Current);
            int sparks = 0;
            for (int i = 0; i < 50; i++) sparks += CbQuality.Particles(10);
            Assert.Less(sparks, 300, "The low level draws as many sparks as the high one");
            game.ApplyQuality();
            CbQuality.Setting = 3;
            Assert.AreEqual(CbQuality.Tier.High, CbQuality.Current);
            Assert.AreEqual(10, CbQuality.Particles(10));
            game.ApplyQuality();
            CbQuality.Setting = 0;
            StringAssert.StartsWith("AUTO", CbQuality.Label);
            CbQuality.Setting = was;
            game.ApplyQuality();
        }

        /// <summary>
        /// How fast a character's planted foot moves over the ground as a share of its own speed, sampled every frame
        /// for frames frames (0: its feet hold; above 0 they slide on with it, its cycle too slow; below, too fast).
        /// </summary>
        static IEnumerator PlantedFootShare(Transform model, System.Func<float> speed, int frames, float fps, List<float> shares)
        {
            Transform left = null, right = null;
            foreach (var b in model.GetComponentsInChildren<Transform>())
            {
                if (b.name == "LeftToeBase") left = b;
                if (b.name == "RightToeBase") right = b;
            }
            if (left == null || right == null) yield break;
            var samples = new List<(float lx, float ly, float rx, float ry, float vx)>();
            for (int k = 0; k < frames; k++)
            {
                yield return null;
                samples.Add((left.position.x, left.position.y, right.position.x, right.position.y, speed()));
            }
            float lowL = samples.Min(p => p.ly), lowR = samples.Min(p => p.ry);
            for (int k = 1; k < samples.Count; k++)
            {
                var a = samples[k - 1]; var b = samples[k];
                if (Mathf.Abs(b.vx) < 1f) continue;
                if (a.ly < lowL + 0.05f && b.ly < lowL + 0.05f) shares.Add((b.lx - a.lx) * fps / b.vx);
                if (a.ry < lowR + 0.05f && b.ry < lowR + 0.05f) shares.Add((b.rx - a.rx) * fps / b.vx);
            }
        }

        static float Median(List<float> v) { if (v.Count == 0) return float.NaN; v.Sort(); return v[v.Count / 2]; }

        [UnityTest]
        public IEnumerator ZzStrideOfTheHyenas()
        {
            // the hyena raider's feet as he comes at Raja: charging from afar, then his fighting walk - logged as STRIDE
            // charge / walk (see PlantedFootShare)
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-5");
            game.BuildStage();
            game.StartGame();
            yield return Frames(2);
            game.SkipFights();
            var hero = game.hero;
            hero.hp = 9999;
            hero.inv = 999f;
            game.Teleport(40f);
            Time.captureDeltaTime = game.TestStep = 1f / 120f;
            var e = new CbEnemy(game, CbEnemy.Kind.Hyena, 49.6f, game.transform);
            game.enemies.Add(e);
            Assert.IsNotNull(e.model, "The raider's model did not load");
            yield return Frames(6);
            var charge = new List<float>();
            yield return PlantedFootShare(e.model.root, () => e.vx, 150, 120f, charge);
            // (his fighting walk only from inside ChargeFrom: until then he still charges)
            for (int k = 0; k < 240 && Mathf.Abs(e.x - hero.x) > 3.3f; k++) yield return null;
            var walk = new List<float>();
            yield return PlantedFootShare(e.model.root, () => e.vx, 60, 120f, walk);
            Debug.Log($"STRIDE hyena charge {Median(charge):F2} over {charge.Count}, walk {Median(walk):F2} over {walk.Count}");
            e.f.Destroy();
            game.enemies.Remove(e);
            // Commander Grak's heavy walk, coming in on Raja after his roar
            var grak = new CbEnemy(game, CbEnemy.Kind.Grak, hero.x + 4.6f, game.transform) { minX = hero.x - 20f, maxX = hero.x + 20f };
            game.enemies.Add(grak);
            Assert.IsNotNull(grak.model, "Grak's model did not load");
            for (int k = 0; k < 600 && grak.grakMove != CbEnemy.GrakMove.Stance; k++) yield return null;
            var heavy = new List<float>();
            yield return PlantedFootShare(grak.model.root, () => grak.vx, 100, 120f, heavy);
            Debug.Log($"STRIDE grak walk {Median(heavy):F2} over {heavy.Count}");
            game.TestStep = 0f;
        }

        [UnityTest]
        public IEnumerator ZzRunFrames()
        {
            // Raja running flat out on an open stretch (the way into the Ember Temple, its raiders skipped; on stage 1-1
            // he ran into its crates): how fast his planted foot moves over the ground, as a share of his own speed (0:
            // his feet hold to the ground; above 0 they slide on with him, the run cycle too slow; below, too fast) -
            // logged as STRIDE, with four frames of it; then an impact frame
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-5");
            game.BuildStage();
            game.StartGame();
            Time.captureDeltaTime = game.TestStep = 1f / 30f;
            yield return Frames(2);
            var hero = game.hero;
            hero.hp = 9999;
            hero.inv = 999f;
            game.SkipFights();
            // (past the last fire on the path: from there to the temple's threshold, open ground)
            game.Teleport(CbLevel.EmberPatches[3] + 1.5f);
            game.TestControls = i => i.x = 1f;
            yield return new WaitForSeconds(0.5f);
            Transform left = null, right = null;
            if (hero.model != null)
                foreach (var b in hero.model.root.GetComponentsInChildren<Transform>())
                {
                    if (b.name == "LeftToeBase") left = b;
                    if (b.name == "RightToeBase") right = b;
                }
            Assert.IsNotNull(left, "Raja's model has no toe bones");
            // (measured finely, a hundred and twentieth of a second a frame over a second and a half: at a thirtieth his
            // foot was down for only three or so frames a run, and the number swung from +0.1 to -0.44)
            Time.captureDeltaTime = game.TestStep = 1f / 120f;
            var samples = new List<(float lx, float ly, float rx, float ry, float vx)>();
            for (int k = 0; k < 180; k++)
            {
                yield return null;
                samples.Add((left.position.x, left.position.y, right.position.x, right.position.y, hero.vx));
            }
            Time.captureDeltaTime = game.TestStep = 1f / 30f;
            float lowL = samples.Min(p => p.ly), lowR = samples.Min(p => p.ry);
            var shares = new List<float>();
            for (int k = 1; k < samples.Count; k++)
            {
                var a = samples[k - 1]; var b = samples[k];
                if (Mathf.Abs(b.vx) < 1f) continue;
                if (a.ly < lowL + 0.05f && b.ly < lowL + 0.05f) shares.Add((b.lx - a.lx) * 120f / b.vx);
                if (a.ry < lowR + 0.05f && b.ry < lowR + 0.05f) shares.Add((b.rx - a.rx) * 120f / b.vx);
            }
            shares.Sort();
            float median = shares.Count > 0 ? shares[shares.Count / 2] : float.NaN;
            Debug.Log($"STRIDE planted foot share {median:F2} over {shares.Count} samples, speed {hero.vx:F1}");
            for (int k = 0; k < 4; k++)
                yield return TakePictures(new[] { ($"run{k}", float.NaN, 0f) }, 1f / 30f);
            game.TestControls = null;
            Assert.Greater(shares.Count, 3, "No frames with his foot on the ground");
            // the painted effects the user approved, all at once beside him
            var at = new Vector3(hero.x + 2f, hero.y + 1.2f, -0.5f);
            CbVfx.Burst(at, CbFx.Hit, 2.2f, 0.6f);
            CbVfx.Slash(at + new Vector3(1.6f, 0, 0), 1, Color.white * 1.6f, 2.4f, 0.6f);
            CbVfx.Sparkle(at + new Vector3(-2.6f, 0.4f, 0), new Color(1f, 0.9f, 0.5f) * 1.6f, 1.6f, 0.6f);
            CbVfx.Smoke(new Vector3(hero.x - 3.2f, hero.y + 0.8f, -0.5f), new Color(0.85f, 0.8f, 0.7f), 2.6f, 0.6f);
            CbVfx.Ring(new Vector3(hero.x, hero.y + 0.05f, 0), CbFx.Hit * 0.5f, 1.8f, 0.6f, ground: true);
            yield return TakePictures(new[] { ("effects", float.NaN, 0f) }, 0.1f);
            // the same on the LOW graphics level (a weak phone): no bloom, no HDR, fewer sparks - still clear
            int setting = CbQuality.Setting;
            CbQuality.Setting = 1;
            game.ApplyQuality();
            CbVfx.Burst(at, CbFx.Hit, 2.2f, 0.6f);
            CbVfx.Slash(at + new Vector3(1.6f, 0, 0), 1, Color.white * 1.6f, 2.4f, 0.6f);
            yield return TakePictures(new[] { ("effects_low", float.NaN, 0f) }, 0.1f);
            CbQuality.Setting = setting;
            game.ApplyQuality();
            game.ImpactFrame(0.5f);
            yield return null;
            Assert.IsTrue(game.InImpactFrame, "The impact frame did not hold");
            yield return TakePictures(new[] { ("impact_frame", float.NaN, 0f) }, 1f / 30f);
            // the special's cut-in, swept in and holding
            yield return new WaitForSeconds(0.3f);
            hero.ki = 100f;
            bool once = true;
            game.TestControls = i => { i.skill = once; once = false; };
            yield return TakePictures(new[] { ("cut_in", float.NaN, 0f) }, 0.4f);
            game.TestControls = null;
            Assert.IsTrue(game.hud.CuttingIn, "SPECIAL showed no cut-in");
        }

        [UnityTest]
        public IEnumerator BabasScrollHidesTheCubInTheClouds()
        {
            // stage 1-3's secret (the story doc): behind a scroll by Baba's hut, a drawing of a cub in the clouds
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-3");
            game.BuildStage();
            game.StartGame();
            yield return Frames(2);
            Assert.IsFalse(game.level.SecretFound, "Baba's secret is found from the start");
            int score = game.score;
            game.level.Strike(CbLevel.SecretScrollX - 1f, 0f, 1, 1.5f, staff: false);
            Assert.IsTrue(game.level.SecretFound, "A blow on Baba's scroll did not show what is behind it");
            Assert.Greater(game.score, score, "Finding Baba's secret scored nothing");
            // (its drawing is a cutscene: off, the game goes on)
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(CbGame.Mode.Play, game.mode, "Finding Baba's secret stopped the game");
            CbStage.Set("1-1");
            game.BuildStage();
        }

        [UnityTest]
        public IEnumerator ZzGrovePictures()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-3");
            game.BuildStage();
            game.StartGame();
            yield return Frames(2);
            yield return TakePictures(new[] { ("grove_start", 7f, 0f) });
            game.Teleport(CbLevel.TrialX[0] - 8f);
            yield return TakePictures(new[] { ("grove_bells", float.NaN, 0f) }, 1.6f);
            game.SkipTrialsTo(1);
            yield return TakePictures(new[] { ("grove_dummies", CbLevel.TrialX[1] - 9.2f, 0f) });
            game.SkipTrialsTo(2);
            yield return TakePictures(new[] { ("grove_stones", CbLevel.TrialX[2] - 8.5f, 0f) }, 3.2f);
            CbSave.PerfectGuard = false;
        }

        [UnityTest]
        public IEnumerator ASwipeIntoTheBlowIsAPerfectGuard()
        {
            // Baba's lesson (the user: a swipe into the blow is the guard): swiped toward a soldier as his blow lands,
            // the blow is returned; a thrown spear met the same way flies back; with nothing coming, it is the dodge
            CbSave.PerfectGuard = true;
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var hero = game.hero;
            IEnumerator Settle()
            {
                game.TestControls = null;
                for (float w = 0; w < 2f && hero.state != CbHero.State.Idle; w += Time.deltaTime) yield return null;
                yield return Frames(2);
            }
            game.Teleport(6f);
            hero.y = 0;
            hero.facing = 1;
            yield return Settle();
            bool first = true, dodged = false;
            game.TestControls = i => { if (first) { i.dash = true; i.dashDir = 1; } first = false; };
            for (float w = 0; w < 0.2f; w += Time.deltaTime) { dodged |= hero.state == CbHero.State.Dash; yield return null; }
            game.TestControls = null;
            Assert.IsTrue(dodged, "With nothing coming a swipe did not dodge");
            yield return Settle();
            // a soldier's blow, a swipe into it as it lands: returned
            hero.facing = 1;
            var e = new CbEnemy(game, CbEnemy.Kind.Hyena, hero.x + 1.4f, game.transform) { minX = hero.x + 1.2f, maxX = hero.x + 1.6f };
            game.enemies.Add(e);
            float hp = hero.hp;
            for (float w = 0; w < 5f && e.state != CbEnemy.State.Atk; w += Time.deltaTime) yield return null;
            Assert.AreEqual(CbEnemy.State.Atk, e.state, "The soldier never struck");
            yield return new WaitForSeconds(0.22f);
            first = true;
            bool guarded = false;
            game.TestControls = i => { if (first) { i.dash = true; i.dashDir = 1; } first = false; };
            for (float w = 0; w < 0.6f; w += Time.deltaTime) { guarded |= hero.Parrying; yield return null; }
            game.TestControls = null;
            Assert.IsTrue(guarded, "A swipe into the blow did not raise the guard");
            Assert.AreEqual(hp, hero.hp, 0.01f, "The perfect guard let the blow hurt him");
            Assert.Less(e.hp, e.max, "The perfect guard did not return the blow");
            // a spear thrown at him, a swipe into it: it flies back
            game.SkipFights();
            yield return Settle();
            hero.facing = 1;
            hp = hero.hp;
            game.level.ThrowSpear(hero.x + 6f, hero.y + 1.25f, -12f, 8f);
            for (float w = 0; w < 1f && !game.level.ThrownComing(hero.x, hero.y, 1, 2.5f); w += Time.deltaTime) yield return null;
            first = true;
            bool back = false;
            game.TestControls = i => { if (first) { i.dash = true; i.dashDir = 1; } first = false; };
            for (float w = 0; w < 0.6f && !back; w += Time.deltaTime) { yield return null; back = game.level.AnyThrownBack; }
            game.TestControls = null;
            Assert.IsTrue(back, "A swipe into a thrown spear did not send it back");
            Assert.AreEqual(hp, hero.hp, 0.01f, "The returned spear hurt him");
            CbSave.PerfectGuard = false;
        }

        [UnityTest]
        public IEnumerator TheJumpButtonIsBackAndTheStickSlides()
        {
            // the JUMP button is back (the user), and up on the stick does nothing for now; down on the stick slides,
            // held down he stays low; left and right still move him
            game = CbGame.Boot();
            yield return Frames(3);
            Assert.IsTrue(Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(t => t.name == "jump"),
                "There is no JUMP button");
            bool down = false;
            var inp = new CbInput();
            CbHud.StickMoves(new Vector2(0, 40), ref down, inp);
            Assert.IsFalse(inp.jump || inp.jumpHeld, "Up on the stick jumped");
            Assert.AreEqual(0f, inp.y, 0.001f, "Up on the stick still did something");
            inp = new CbInput();
            CbHud.StickMoves(new Vector2(60, 25), ref down, inp);
            Assert.AreEqual(1f, inp.x, 0.01f, "The stick across was not a full run");
            down = false;
            inp = new CbInput();
            CbHud.StickMoves(new Vector2(0, -30), ref down, inp);
            Assert.IsTrue(inp.slide && !inp.jump, "Down on the stick did not slide");
            inp = new CbInput();
            CbHud.StickMoves(new Vector2(0, -45), ref down, inp);
            Assert.IsFalse(inp.slide, "Holding the stick down slid again");
            Assert.Less(inp.y, -0.5f, "Holding the stick down did not keep him low");
        }

        [UnityTest]
        public IEnumerator TheNestScrollTeachesTheStaffThrow()
        {
            // the story doc: stage 1-2's hidden scroll teaches the staff throw - KI held, the staff flies out like a
            // boomerang and comes back, hitting what it passes and taking what it touches
            CbSave.StaffThrow = false;
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-2");
            game.BuildStage();
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            Assert.IsFalse(CbHero.StaffThrowLearned, "The staff throw is known from the start");
            // the scroll on the hidden nest high over the bars
            game.Teleport(CbLevel.NestX - 0.3f);
            game.hero.y = CbLevel.NestTop;
            for (float w = 0; w < 2f && !CbHero.StaffThrowLearned; w += Time.deltaTime) yield return null;
            Assert.IsTrue(CbHero.StaffThrowLearned, "The nest's scroll did not teach the staff throw");
            // on the first bough, a soldier and a coin up high ahead: KI held throws the staff
            game.Teleport(12f);
            game.hero.y = 0;
            game.hero.vy = 0;
            game.hero.state = CbHero.State.Idle;
            game.hero.facing = 1;
            var e = new CbEnemy(game, CbEnemy.Kind.Hyena, 17f, game.transform) { minX = 16.5f, maxX = 17.5f };
            game.enemies.Add(e);
            game.items.Spawn(CbItems.Kind.Coin, new Vector3(18.6f, 1.4f, 0), resting: true);
            yield return new WaitForSeconds(0.5f);
            game.hero.ki = 100;
            int coins = game.items.Coins;
            float held = 0;
            game.TestControls = inp => { inp.kiTap = held == 0; inp.kiDown = held < 0.45f; held += Time.deltaTime; };
            bool flew = false;
            float furthest = 0;
            for (float w = 0; w < 3f && !(flew && !game.hero.StaffFlying); w += Time.deltaTime)
            {
                yield return null;
                if (game.hero.StaffFlying) { flew = true; furthest = Mathf.Max(furthest, game.hero.StaffAt.x - game.hero.x); }
            }
            game.TestControls = null;
            Assert.IsTrue(flew, "Holding KI did not throw the staff");
            Assert.Greater(furthest, 5f, "The thrown staff did not fly out far");
            Assert.IsFalse(game.hero.StaffFlying, "The thrown staff never came back to his hand");
            Assert.Less(e.hp, e.max, "The thrown staff did not hit the soldier in its way");
            Assert.Greater(game.items.Coins, coins, "The thrown staff did not take the coin it touched");
            // up on the ledge the springy leaf throws him to: the golden fruit hanging out of reach, brought down by it
            game.Teleport(CbLevel.FruitLedgeX + 0.5f);
            game.hero.y = CbLevel.FruitLedgeTop;
            game.hero.vy = 0;
            game.hero.state = CbHero.State.Idle;
            game.hero.facing = 1;
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(game.items.ItemsNear(CbLevel.HighFruitX, 0.5f).Count, 0, "No golden fruit hangs out of reach off the ledge");
            game.hero.hp = 40;
            game.hero.ki = 100;
            held = 0;
            flew = false;
            game.TestControls = inp => { inp.kiTap = held == 0; inp.kiDown = held < 0.45f; held += Time.deltaTime; };
            for (float w = 0; w < 3f && !(flew && !game.hero.StaffFlying); w += Time.deltaTime)
            {
                yield return null;
                flew |= game.hero.StaffFlying;
            }
            game.TestControls = null;
            Assert.AreEqual(0, game.items.ItemsNear(CbLevel.HighFruitX, 0.5f).Count, "The thrown staff did not bring down the golden fruit");
            Assert.AreEqual(game.hero.MaxHp, game.hero.hp, 0.01f, "The golden fruit the staff brought down did not heal him");
            CbSave.StaffThrow = false;
        }

        [UnityTest]
        public IEnumerator TuskChargesIntoATreeAndIsBeatenFromBehind()
        {
            // Tusk (the story doc: he charges from side to side, armoured in front; dodge, he sticks in a tree, and you
            // hit him from behind)
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-2");
            game.BuildStage();
            game.StartGame();
            game.SkipToBoss();
            yield return Frames(2);
            Assert.AreEqual(2, game.level.TuskTrees, "Tusk's clearing has no trunks at its ends");
            game.Teleport(CbLevel.TuskX - 6f);
            game.hero.y = 0;
            for (float w = 0; w < 2f && game.boss == null; w += Time.deltaTime) yield return null;
            var tusk = game.boss;
            Assert.IsNotNull(tusk, "Tusk did not appear");
            Assert.AreEqual(CbEnemy.Kind.Tusk, tusk.kind);
            Assert.AreEqual(CbGame.Mode.Play, game.mode, "Tusk's fight did not start");
            Assert.IsNotNull(game.lockZone, "Tusk's arena is not shut");
            game.hero.hp = 9999;
            // his armour: a blow from in front glances off
            float hp0 = tusk.hp;
            Assert.IsFalse(game.HurtEnemy(tusk, 20, -tusk.facing, 6, 2, true, true, false, true), "A blow from in front got through Tusk's armour");
            Assert.AreEqual(hp0, tusk.hp, 0.01f, "Tusk's armour let a blow from in front hurt him");
            // he charges right across his arena, and rams the trunk at its end: stuck
            bool charged = false;
            for (float w = 0; w < 7f && !tusk.TuskStuck; w += Time.deltaTime)
            {
                yield return null;
                charged |= tusk.tuskMove == CbEnemy.TuskMove.Charge;
            }
            Assert.IsTrue(charged, "Tusk never charged");
            Assert.IsTrue(tusk.TuskStuck, "Tusk's charge did not end with his tusks stuck in a tree");
            Assert.AreEqual(CbLevel.TuskX - CbEnemy.TuskCrash, tusk.x, 0.05f, "Tusk did not charge right across his arena");
            Assert.Greater(game.level.TreeShake(0), 0f, "The trunk he rammed did not shake");
            // stuck, a blow from behind hurts him
            hp0 = tusk.hp;
            Assert.IsTrue(game.HurtEnemy(tusk, 20, tusk.facing, 6, 2, true, true, false, true), "A blow from behind did not hurt the stuck Tusk");
            Assert.Less(tusk.hp, hp0, "A blow from behind did not hurt the stuck Tusk");
            // he pulls free after a while
            for (float w = 0; w < CbEnemy.TuskStuckTime + 1f && tusk.TuskStuck; w += Time.deltaTime) yield return null;
            Assert.IsFalse(tusk.TuskStuck, "Tusk stayed stuck in the tree");
            // beaten: the arena opens, and the stage clears at the end of the camp
            for (int k = 0; k < 60 && !tusk.dead; k++)
            {
                game.HurtEnemy(tusk, 40, tusk.facing, 8, 2, true, true, false, true);
                yield return Frames(2);
            }
            Assert.IsTrue(tusk.dead, "Tusk could not be beaten");
            yield return Frames(3);
            Assert.IsNull(game.lockZone, "Tusk's arena did not open after he was beaten");
            for (float w = 0; w < 3f && game.enemies.Contains(tusk); w += Time.deltaTime) yield return null;
            Assert.IsFalse(game.enemies.Contains(tusk), "Beaten, Tusk never went");
            game.Teleport(CbWorld.GoalX + 0.5f);
            game.hero.y = 0;
            for (float w = 0; w < 3f && !game.Cleared; w += Time.deltaTime) yield return null;
            Assert.IsTrue(game.Cleared, "Stage 1-2 did not clear after Tusk");
            // Koda's cage: broken, and empty (its scene is a cutscene, off)
            Assert.IsTrue(game.level.KodaCageBroken, "Koda's cage was left whole");
            yield return WaitForMode(CbGame.Mode.Over, 6f);
            Assert.AreEqual(CbGame.Mode.Over, game.mode, "Stage 1-2 did not end after the empty cage");
        }

        [UnityTest]
        public IEnumerator ZzTheCageAtTheEndOfTheTreetopsIsEmpty()
        {
            // the end of stage 1-2 (the story doc): Raja breaks Koda's cage, it is empty, he finds the prophecy scroll
            // (a cutscene: turned on for this test)
            CbStory.Cutscenes = true;
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-2");
            game.BuildStage();
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            Assert.IsFalse(game.level.KodaCageBroken, "Koda's cage is broken from the start");
            game.Teleport(CbWorld.GoalX - 6f);
            game.hero.y = 0;
            yield return new WaitForSeconds(2f);
            game.Teleport(CbWorld.GoalX + 0.5f);
            yield return WaitForMode(CbGame.Mode.Scene, 3f);
            Assert.AreEqual(CbGame.Mode.Scene, game.mode, "The empty cage's scene did not play");
            // each line waits for a tap: the pictures at "Hold on", "...Empty?" and the scroll
            yield return TakePictures(new[] { ("cage_holdon", float.NaN, 0f) }, 1.6f);
            yield return Tap();
            yield return new WaitForSeconds(2.2f);
            Assert.IsTrue(game.level.KodaCageBroken, "Raja did not break Koda's cage");
            yield return TakePictures(new[] { ("cage_empty", float.NaN, 0f) }, 0.1f);
            yield return Tap();
            yield return new WaitForSeconds(2.4f);
            yield return TakePictures(new[] { ("cage_scroll", float.NaN, 0f) }, 0.1f);
            Assert.Less(Mathf.Abs(game.level.ProphecyScroll.position.x - game.hero.x), 1.5f, "The scroll did not come up to Raja");
            Assert.Greater(game.level.ProphecyScroll.position.y, 0.8f, "The scroll did not come up to Raja");
            for (int k = 0; k < 6 && game.mode == CbGame.Mode.Scene; k++) yield return Tap();
            Assert.AreNotEqual(CbGame.Mode.Scene, game.mode, "The empty cage's scene never ended");
            yield return WaitForMode(CbGame.Mode.Over, 4f);
            Assert.AreEqual(CbGame.Mode.Over, game.mode, "Stage 1-2 did not end after the empty cage");
        }

        /// <summary>A tap on the screen in a scene: the line shown in full, then the next one.</summary>
        IEnumerator Tap()
        {
            for (int i = 0; i < 2; i++)
            {
                game.TestInput = inp => inp.confirm = true;
                yield return null;
                game.TestInput = null;
                yield return Frames(2);
            }
            yield return new WaitForSeconds(0.3f);
        }

        [UnityTest]
        public IEnumerator ZzTuskPictures()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-2");
            game.BuildStage();
            game.StartGame();
            game.SkipToBoss();
            yield return Frames(2);
            // the camera settled at the clearing, then into the fight: he rears and roars, charges, sticks in a tree
            game.Teleport(CbLevel.TuskX - 8.5f);
            game.hero.y = 0;
            yield return new WaitForSeconds(2.5f);
            game.hero.inv = 999f;
            game.Teleport(CbLevel.TuskX - 5f);
            yield return TakePictures(new[] { ("tusk_roar", float.NaN, 0f) }, 0.5f);
            var tusk = game.boss;
            Assert.IsNotNull(tusk, "Tusk did not appear");
            for (float w = 0; w < 5f && tusk.tuskMove != CbEnemy.TuskMove.Charge; w += Time.deltaTime) yield return null;
            yield return TakePictures(new[] { ("tusk_charge", float.NaN, 0f) }, 0.12f);
            for (float w = 0; w < 5f && !tusk.TuskStuck; w += Time.deltaTime) yield return null;
            game.Teleport(tusk.x - tusk.facing * 3.2f);
            game.hero.facing = tusk.facing;
            yield return TakePictures(new[] { ("tusk_stuck", float.NaN, 0f) }, 0.35f);
        }

        [UnityTest]
        public IEnumerator ZzTreetopsPictures()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            CbStage.Set("1-2");
            game.BuildStage();
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            yield return TakePictures(new[] { ("treetops_start", 8f, 0f), ("treetops_vines", 60.5f, 0f), ("treetops_saw", 98f, 0f),
                ("treetops_ledges", 132.6f, 2.8f), ("treetops_logs", 160f, 0f), ("treetops_bridge", 262f, 0f), ("treetops_camp", 330f, 0f),
                ("treetops_hollow", 219f, 0f), ("treetops_fruit", CbLevel.FruitLedgeX + 0.5f, CbLevel.FruitLedgeTop) });
        }

        /// <summary>
        /// Photographs the stage being played at each spot (Raja there, the camera eased after him) and logs the pictures
        /// for the CI to collect ("SHOT name i base64").
        /// </summary>
        IEnumerator TakePictures((string name, float x, float y)[] spots, float wait = 2.5f)
        {
            // the game's own camera (Camera.main can be another scene's)
            var cam = game.GetComponentInChildren<Camera>();
            Assert.IsNotNull(cam, "No game camera");
            // Only here is the game drawn in CI (into a picture): the shaders compile for the first time and any
            // error of theirs is told in the log ("SHOT ERROR"), not failing the run before the pictures are taken
            LogAssert.ignoreFailingMessages = true;
            void Told(string message, string trace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception) Debug.Log("SHOT ERROR " + message.Split('\n')[0]);
            }
            Application.logMessageReceived += Told;
            // the HUD drawn by the camera too (an overlay canvas isn't in a camera's picture)
            var hud = GameObject.Find("HUD")?.GetComponent<Canvas>();
            if (hud != null) { hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = cam; hud.planeDistance = 0.5f; }
            const int W = 960, H = 540;
            cam.aspect = W / (float)H;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var oldTrace = Application.GetStackTraceLogType(LogType.Log);
            // where, and how high (the bars are over the river: he hangs from them, or he would be in the water)
            // (x NaN: he stays where he is, for a moment in a fight)
            foreach (var (name, x, y) in spots)
            {
                if (!float.IsNaN(x))
                {
                    game.Teleport(x);
                    game.hero.y = y;
                    game.hero.vx = game.hero.vy = 0;
                    // (whatever he was doing - hanging from an edge held him there, and the pictures after it were all of it)
                    game.hero.state = y > 0 ? CbHero.State.Air : CbHero.State.Idle;
                }
                // the gorge with its water on the way up
                if (name == "gorge") game.level.StartFlood(2.4f);
                // the camera eases after him
                yield return new WaitForSeconds(wait);
                // drawn by the pipeline as usual, for a frame, into the picture (WaitForEndOfFrame never comes in
                // batch mode: two plain frames, the second after the first was drawn)
                cam.targetTexture = rt;
                cam.aspect = W / (float)H;
                yield return null;
                yield return null;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                // what drawing this picture took: the things drawn and their triangles (to keep the phone fast)
                int drawn = 0; long tris = 0;
                var heavy = new Dictionary<string, long>();
                foreach (var rd in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!rd.isVisible || !rd.enabled) continue;
                    drawn++;
                    // (TryGetComponent: a particle system has no mesh filter, and Unity's missing one threw past ?.)
                    var mesh = rd is SkinnedMeshRenderer sk ? sk.sharedMesh : rd.TryGetComponent<MeshFilter>(out var mfr) ? mfr.sharedMesh : null;
                    if (mesh == null) continue;
                    // (a piece of a static batch shares one combined mesh with the rest: only its own part of it)
                    long nt = 0;
                    int s0 = 0, s1 = mesh.subMeshCount;
                    if (rd.isPartOfStaticBatch && rd is MeshRenderer mrb) { s0 = mrb.subMeshStartIndex; s1 = Mathf.Min(mesh.subMeshCount, s0 + rd.sharedMaterials.Length); }
                    for (int sm = s0; sm < s1; sm++) nt += mesh.GetIndexCount(sm) / 3;
                    tris += nt;
                    // by what it is: the model's name, or the joint it was built in
                    string what = rd.transform.parent != null ? rd.transform.parent.name + "/" + rd.name : rd.name;
                    heavy[what] = (heavy.TryGetValue(what, out long h0) ? h0 : 0) + nt;
                }
                var top5 = heavy.OrderByDescending(kv => kv.Value).Take(6).Select(kv => $"{kv.Key} {kv.Value / 1000}k");
                Debug.Log($"PERF {name}: {drawn} drawn, {tris / 1000}k triangles - most: {string.Join(", ", top5)}");
                RenderTexture.active = prev;
                cam.targetTexture = null;
                // a blank picture: nothing was drawn
                var px = tex.GetPixels32();
                bool blank = true;
                for (int i = 1; i < px.Length && blank; i += 997)
                    blank = px[i].r == px[0].r && px[i].g == px[0].g && px[i].b == px[0].b;
                if (blank) { Debug.Log($"SHOT {name} BLANK (nothing drawn)"); Object.Destroy(tex); continue; }
                var jpg = tex.EncodeToJPG(82);
                Object.Destroy(tex);
                System.IO.Directory.CreateDirectory("artifacts/shots");
                System.IO.File.WriteAllBytes($"artifacts/shots/{name}.jpg", jpg);
                var b64 = System.Convert.ToBase64String(jpg);
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                // a few lines a frame: logged all at once, some of them never reached the log
                int n = 0;
                for (int i = 0; i < b64.Length; i += 4000, n++)
                {
                    Debug.Log($"SHOT {name} {n} {b64.Substring(i, Mathf.Min(4000, b64.Length - i))}");
                    if (n % 3 == 2) yield return null;
                }
                Debug.Log($"SHOT {name} END {n}");
                Application.SetStackTraceLogType(LogType.Log, oldTrace);
            }
            cam.ResetAspect();
            rt.Release();
            if (hud != null) hud.renderMode = RenderMode.ScreenSpaceOverlay;
            Application.logMessageReceived -= Told;
        }



        /// <summary>
        /// The user: he stood and hung in the air beside the models - wherever he stands (the ground, a rock, a ledge,
        /// the stairs, a platform) his feet must touch what is drawn, and hanging from an edge his hands must be on it
        /// (his legs straight under a ledge, against the wall under a block's edge). Every place in stages 1-1 and 1-2.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator RajaTouchesWhatHeStandsOnAndHangsFrom()
        {
            var fails = new List<string>();
            int stood = 0, hung = 0;
            foreach (var stage in new[] { "1-1", "1-2" })
            {
                game = CbGame.Boot();
                yield return Frames(3);
                if (stage != "1-1") { CbStage.Set(stage); game.BuildStage(); }
                game.StartGame();
                game.SkipFights();
                yield return Frames(3);
                var level = game.transform.Find("Level");
                Assert.IsNotNull(level, "No level");
                var hero = game.hero;
                // everything drawn in the stage solid for the rays - the level and the world's ground (the game gives
                // only its models colliders) - but not Raja himself or what he carries
                // (his whole rig: up from his model to just under the game's own containers)
                var raja = hero.model.root;
                while (raja.parent != null && raja.parent != game.transform && raja.parent.parent != game.transform) raja = raja.parent;
                foreach (var mf in game.GetComponentsInChildren<MeshFilter>())
                    // (not the flat cut-out cards of the birds, fish and sparks: nothing to stand on)
                    if (mf.sharedMesh != null && mf.sharedMesh.isReadable && mf.sharedMesh != CbMesh.Quad && mf.sharedMesh.name != "Quad" && !mf.TryGetComponent<Collider>(out _)
                        && (raja == null || !mf.transform.IsChildOf(raja)) && !UnderLighterCopy(mf.transform))
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                Physics.SyncTransforms();
                hero.inv = 999f;
                // where he stands: on every ledge (its middle and near its ends), every block, and along the stage
                // (and the skinned props - the chest - as they stand)
                foreach (var smr in game.level.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr.sharedMesh == null || smr.TryGetComponent<Collider>(out _)) continue;
                    var baked = new Mesh();
                    // (in its own space, with its scale, as the collider on it will be)
                    smr.BakeMesh(baked, true);
                    smr.gameObject.AddComponent<MeshCollider>().sharedMesh = baked;
                }
                Physics.SyncTransforms();
                var spots = new List<(string what, float x, float top)>();
                foreach (var (x0, x1, top) in game.level.LedgeTops())
                {
                    spots.Add(("ledge", (x0 + x1) / 2, top));
                    if (x1 - x0 > 0.9f) { spots.Add(("ledge's end", x0 + 0.3f, top)); spots.Add(("ledge's end", x1 - 0.3f, top)); }
                }
                foreach (var (x0, x1, top) in game.level.BlockTops()) spots.Add(("block", (x0 + x1) / 2, top));
                for (float x = CbWorld.StartWall + 0.5f; x < CbWorld.LevelMax - 4f; x += 3f)
                    // (not right at an edge: there his feet are out over it - the ledges' ends are tried on their own)
                    if (!CbLevel.InWater(x, 0.6f) && game.level.StandableAt(x)
                        && Mathf.Abs(game.level.SupportAt(x - 0.4f, 40f) - game.level.SupportAt(x, 40f)) < 0.3f && Mathf.Abs(game.level.SupportAt(x + 0.4f, 40f) - game.level.SupportAt(x, 40f)) < 0.3f)
                        spots.Add(("way", x, game.level.SupportAt(x, 40f)));
                // up the stone bridge's stairs, step by step, at both ends
                if (stage == "1-1")
                    foreach (int side in new[] { -1, 1 })
                        for (float d = 0.2f; d < 3.4f; d += 0.3f)
                        {
                            float sx = CbLevel.StoneBridgeX + side * (CbLevel.StoneBridgeHalf + d);
                            spots.Add(("stairs", sx, game.level.SupportAt(sx, 5f)));
                        }
                foreach (var (what, x, top) in spots)
                {
                    if (top < -1f) continue;
                    game.Teleport(x);
                    hero.y = game.level.SupportAt(x, top + 0.3f);
                    hero.vx = hero.vy = 0;
                    hero.state = CbHero.State.Idle;
                    game.TestInput = i => { };
                    for (float w = 0; w < 0.3f; w += Time.deltaTime) yield return null;
                    if (hero.state != CbHero.State.Idle || !hero.model.Toes(out var l, out var r)) continue;
                    stood++;
                    // each foot (standing, both are down) on the drawn surface under it - not over it, not sunk in it.
                    // At an edge one foot may be out over it (the other on it)
                    bool atEdge = Mathf.Abs(game.level.SupportAt(x - 0.6f, top + 0.3f) - top) > 0.3f || Mathf.Abs(game.level.SupportAt(x + 0.6f, top + 0.3f) - top) > 0.3f;
                    int outOver = 0;
                    foreach (var (foot, toe) in new[] { ("left", l), ("right", r) })
                    {
                        // (from a little over the toe: leaves and strands hanging over a ledge are not his foot sunk in
                        // it; under the toe, on his line and a little either side - seen from the front a thin ledge is
                        // under his foot wherever it lies across the path: the nearest top)
                        RaycastHit h = default;
                        bool any = false;
                        float bestGap = float.MaxValue;
                        foreach (float z in new[] { toe.z, 0f, toe.z - 0.15f, toe.z + 0.15f, -0.12f, 0.12f, -0.3f, 0.3f })
                            if (Physics.Raycast(new Vector3(toe.x, toe.y + 0.22f, z), Vector3.down, out var hz, 0.95f) && Mathf.Abs(toe.y - hz.point.y) < bestGap)
                            { bestGap = Mathf.Abs(toe.y - hz.point.y); h = hz; any = true; }
                        if (!any)
                        {
                            if (atEdge && ++outOver == 1) continue;
                            // what is solid near it (to see why nothing was under it)
                            var near = new List<string>();
                            foreach (var c in Physics.OverlapSphere(toe, 1.2f))
                                if (c.transform.root == game.transform.root && near.Count < 6)
                                    near.Add($"{c.name}[{c.bounds.min.x:F2}..{c.bounds.max.x:F2}, y {c.bounds.min.y:F2}..{c.bounds.max.y:F2}, z {c.bounds.min.z:F2}..{c.bounds.max.z:F2}{(c.enabled && c.gameObject.activeInHierarchy ? "" : " off")}]");
                            fails.Add($"{stage} {what} x {x:F1}: his {foot} foot in the air (nothing drawn under it) (he at {hero.x:F2},{hero.y:F2}; toe at {toe.x:F2},{toe.y:F2},{toe.z:F2}; near: {string.Join(" ", near)})");
                        }
                        else
                        {
                            float gap = toe.y - h.point.y;
                            string at = $"(he at {hero.x:F2},{hero.y:F2}; toe at {toe.x:F2},{toe.y:F2},{toe.z:F2}; drawn at {h.point.y:F2})";
                            // (at an edge one foot may be out over a drop too deep to step down to: level with the
                            // step he stands on, his toes over its edge)
                            // (likewise one foot over a drawn step deeper than a foot steps down to, on stairs whose
                            // drawn steps are deeper than the way he walks up them)
                            if (gap > 0.18f && Mathf.Abs(toe.y - top) < 0.15f && gap > CbLimbIK.StepDown && ++outOver == 1) continue;
                            if (gap > 0.18f) fails.Add($"{stage} {what} x {x:F1} top {top:F2}: his {foot} foot {gap:F2} over what is drawn ({h.collider.name}) {at}");
                            else if (gap < -0.12f) fails.Add($"{stage} {what} x {x:F1} top {top:F2}: his {foot} foot {-gap:F2} into what is drawn ({h.collider.name}) {at}");
                        }
                    }
                }
                // hanging from every ledge's edges: his hands on the corner; under a ledge his legs hang straight
                foreach (var (x0, x1, top) in game.level.LedgeTops())
                {
                    if (top < 1.7f) continue;
                    foreach (int dir in new[] { 1, -1 })
                    {
                        float edge = dir > 0 ? x0 : x1;
                        hero.TestHang(dir, edge, top);
                        game.TestInput = i => { };
                        for (float w = 0; w < 0.5f; w += Time.deltaTime) yield return null;
                        if (hero.state != CbHero.State.Hang) continue;
                        hung++;
                        var palms = (hero.model.Palm(hero.model.leftHand) + hero.model.Palm(hero.model.rightHand)) * 0.5f;
                        float off = Vector2.Distance(new Vector2(palms.x, palms.y), new Vector2(edge, top));
                        if (off > 0.12f) fails.Add($"{stage} hanging at x {edge:F1} top {top:F2}: his hands {off:F2} off the edge");
                        // (every hang a still one at arm's length, at a wall too: the user)
                        if (hero.model.Toes(out var lt, out var rt) && Mathf.Max(Mathf.Abs(lt.x - palms.x), Mathf.Abs(rt.x - palms.x)) > 0.6f)
                            fails.Add($"{stage} hanging at x {edge:F1} top {top:F2}: his legs not straight down");
                    }
                }
                game.TestInput = null;
                Debug.Log($"TOUCH {stage}: {game.level.RockSteps} steps drawn on the rocks made standable");
                Object.Destroy(game.gameObject);
                game = null;
                yield return Frames(2);
            }
            foreach (var f in fails) Debug.Log("TOUCH " + f);
            Debug.Log($"TOUCH stood at {stood} places, hung from {hung} edges, {fails.Count} wrong");
            Assert.Greater(stood, 60, "Raja was stood at too few places");
            // (all of them in the message: the log's own lines are cut off by the pictures after them)
            Assert.IsEmpty(fails, $"Raja does not touch what he stands on or hangs from ({fails.Count}): " + string.Join(" | ", fails));
        }

        static bool UnderLighterCopy(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name == "lod1") return true;
            return false;
        }

        [UnityTest]
        public IEnumerator ShortOfALedgeHeDoesNotHangWithHisBackToUs()
        {
            // the user: no hanging along a ledge's front lip, his back to the camera - only from its ends, from the side
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var hero = game.hero;
            hero.inv = 999f;
            var ledge = game.level.LedgeTops().Find(l => l.x1 - l.x0 > 1.4f && l.top > 2f && l.top < 9f);
            Assert.Greater(ledge.x1, ledge.x0, "No ledge wide enough to try");
            float mid = (ledge.x0 + ledge.x1) / 2;
            game.Teleport(mid);
            hero.y = ledge.top - 1.75f;
            hero.vx = 0; hero.vy = -0.5f;
            hero.state = CbHero.State.Air;
            for (float w = 0; w < 0.5f; w += Time.deltaTime)
            {
                Assert.IsFalse(hero.OnLip, "Raja hung along a ledge's lip, his back to the camera");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ZzSwingPoleRajaHangsFromAndSwingsOn()
        {
            // the user: catching the pole he just hangs from it; left and right swing him, with reasonable physics, his
            // hands on the pole all the while (not in the air). (None stands in the stages until the user says where:
            // one put up here for the test.)
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            const float PX = 70f, PY = 3.4f;
            Assert.Less(game.level.SupportAt(PX, 10f), 0.5f, "Something stands where the test puts its pole");
            game.level.TestAddPole(PX, PY);
            yield return Frames(2);
            var hero = game.hero;
            hero.inv = 999f;
            // reaching it in a jump he takes hold
            game.Teleport(PX - 0.1f);
            hero.y = PY - 1.71f;
            hero.vx = 0.5f; hero.vy = 0.5f;
            hero.state = CbHero.State.Air;
            yield return Frames(3);
            Assert.AreEqual(CbHero.State.Pole, hero.state, "Raja did not take hold of the swing pole");
            float handsOff = 0;
            float HandsOff()
            {
                var palms = (hero.model.Palm(hero.model.leftHand) + hero.model.Palm(hero.model.rightHand)) * 0.5f;
                var bar = hero.OnPole;
                return bar == null ? 0 : Vector2.Distance(new Vector2(palms.x, palms.y), new Vector2(bar.x, bar.y));
            }
            // left alone he just hangs: the swing dies away
            for (float w = 0; w < 2.5f; w += Time.deltaTime) { yield return null; if (w > 0.3f) handsOff = Mathf.Max(handsOff, HandsOff()); }
            Assert.AreEqual(CbHero.State.Pole, hero.state, "Raja let go of the pole by himself");
            Assert.Less(Mathf.Abs(hero.PoleAngle), 0.25f, "Left alone on the pole, Raja's swing did not die away");
            // pushed left and right with the swing, it grows
            float last = hero.PoleAngle, most = 0;
            game.TestInput = i => { float now = hero.PoleAngle; i.x = now >= last ? 1 : -1; last = now; };
            for (float w = 0; w < 5f; w += Time.deltaTime)
            {
                yield return null;
                most = Mathf.Max(most, Mathf.Abs(hero.PoleAngle));
                handsOff = Mathf.Max(handsOff, HandsOff());
            }
            Assert.Greater(most, 1.0f, "Pumping left and right did not swing Raja high");
            Assert.Less(handsOff, 0.2f, "Raja's hands left the pole as he hung and swung on it");
            yield return TakePictures(new[] { ("swing_pole", float.NaN, 0f) }, 0f);
            // let go as he swings forward and up: carried on with the swing
            game.TestInput = i => { float now = hero.PoleAngle; i.x = now >= last ? 1 : -1; last = now; };
            float prevA = hero.PoleAngle;
            for (float w = 0; w < 3f; w += Time.deltaTime)
            {
                yield return null;
                float a = hero.PoleAngle;
                if (a > 0.3f && a > prevA) break;
                prevA = a;
            }
            bool let = false;
            game.TestInput = i => { if (!let) { i.jump = true; let = true; } };
            yield return Frames(2);
            game.TestInput = null;
            Assert.AreEqual(CbHero.State.Air, hero.state, "JUMP did not let go of the pole");
            Assert.Greater(hero.vx, 2f, "Letting go on the forward swing did not carry Raja on");
            Assert.Greater(hero.vy, 3f, "Letting go on the forward swing did not carry Raja up");
        }

        [UnityTest]
        public IEnumerator VinesAndBars()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var level = game.level;
            var hero = game.hero;
            // a vine: reaching its end in a jump he takes hold and swings; JUMP lets go
            var vine = level.vines[0];
            game.Teleport(vine.ax + Mathf.Sin(vine.angle) * vine.len - 0.2f);
            hero.y = vine.ay - Mathf.Cos(vine.angle) * vine.len - 1.48f;
            hero.vx = 2f; hero.vy = 0;
            hero.state = CbHero.State.Air;
            yield return Frames(3);
            Assert.AreEqual(CbHero.State.Swing, hero.state, "Raja did not take hold of the vine");
            game.TestInput = i => i.x = 1;
            yield return new WaitForSeconds(0.8f);
            bool let = false;
            game.TestInput = i => { if (!let) { i.jump = true; let = true; } };
            yield return Frames(3);
            game.TestInput = null;
            Assert.AreEqual(CbHero.State.Air, hero.state, "JUMP did not let go of the vine");
            // a moment after letting go he can take hold again
            yield return new WaitForSeconds(0.35f);
            // a bar: he hangs from it and goes along it hand over hand
            var bar = level.bars[0];
            game.Teleport(bar.x0 + 1f);
            hero.y = bar.y - 1.71f;
            hero.vx = hero.vy = 0;
            hero.state = CbHero.State.Air;
            yield return Frames(3);
            Assert.AreEqual(CbHero.State.Bars, hero.state, "Raja did not take hold of the bar");
            float from = hero.x, hp0 = hero.hp;
            // (short of the first thorns: a prick there keeps his hands off the bar a moment, GrabRest)
            game.TestInput = i => i.x = 1;
            yield return new WaitForSeconds(0.55f);
            game.TestInput = null;
            Assert.Greater(hero.x, from + 1f, "Raja did not go along the bar");
            Assert.AreEqual(hp0, hero.hp, "Raja was hurt going along the bar before its thorns");
            yield return new WaitForSeconds(0.4f);
            // thorns on the bar (the user): his hands on them hurt; a jump along the bar clears them and he catches on
            Assert.Greater(bar.thorns.Length, 0, "No thorns on the bar");
            float th = bar.thorns[0];
            game.Teleport(th - 1f);
            hero.y = bar.y - 1.71f;
            hero.vx = hero.vy = 0;
            hero.inv = 0;
            hero.state = CbHero.State.Air;
            yield return Frames(3);
            Assert.AreEqual(CbHero.State.Bars, hero.state, "Raja did not take hold of the bar before its thorns");
            float hp = hero.hp;
            game.TestInput = i => i.x = 1;
            yield return new WaitForSeconds(0.7f);
            game.TestInput = null;
            Assert.Less(hero.hp, hp, "The thorns on the bar did not hurt");
            Assert.Less(hero.x, th, "Raja went along the bar through its thorns");
            yield return new WaitForSeconds(1.2f);
            bool hop = false;
            game.TestInput = i => { i.x = 1; if (!hop) { i.jump = true; hop = true; } };
            for (float w = 0; w < 2f && !(hero.state == CbHero.State.Bars && hero.x > th); w += Time.deltaTime) yield return null;
            game.TestInput = null;
            Assert.AreEqual(CbHero.State.Bars, hero.state, "A jump along the bar did not catch it again past the thorns");
            Assert.Greater(hero.x, th, "A jump along the bar did not clear the thorns");
        }

        [UnityTest]
        public IEnumerator TempleTrapsAndPuzzles()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var level = game.level;
            // thorns stop him; a paw clangs off them, two staff blows cut them
            Assert.Less(level.BlockX(244f, 247f, 0f), 245.4f - CbLevel.HalfWidth + 0.01f, "Raja walks through the thorns");
            level.Strike(244.9f, 0f, 1, 2.6f, staff: false);
            Assert.IsFalse(level.ThornsCut(0), "A paw cut the thorns");
            level.Strike(244.9f, 0f, 1, 2.6f, staff: true);
            yield return new WaitForSeconds(0.35f);
            level.Strike(244.9f, 0f, 1, 2.6f, staff: true);
            Assert.IsTrue(level.ThornsCut(0), "Two staff blows did not cut the thorns");
            // no hidden pit any more (the user saw no point in it): solid ground where it was
            Assert.AreEqual(0f, level.SupportAt(255.2f, 0.5f), 0.001f, "There is still a pit in the temple");
            // the lever opens the first gate - for a few seconds only (a race past the swinging logs and under a low
            // log); then it shuts and the lever springs back
            Assert.Less(level.BlockX(CbLevel.RaceGateX - 2f, CbLevel.RaceGateX + 2f, 0f), CbLevel.RaceGateX - 0.4f - CbLevel.HalfWidth + 0.01f, "The first gate does not stop him");
            Assert.Less(level.BlockX(CbLevel.RaceLogX - 2f, CbLevel.RaceLogX + 1f, 0f), CbLevel.RaceLogX - 0.35f - CbLevel.HalfWidth + 0.01f, "The low log does not stop him standing");
            level.Strike(CbLevel.LeverX - 0.5f, 0f, 1, 1.7f, staff: false);
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(level.GateOpen(0), "The lever did not open the gate");
            Assert.IsTrue(level.LeverPulled);
            yield return new WaitForSeconds(CbLevel.LeverOpenTime);
            Assert.IsFalse(level.GateOpen(0), "The lever's gate stays open");
            Assert.IsFalse(level.LeverPulled, "The lever did not spring back when its gate shut");
            level.Strike(CbLevel.LeverX - 0.5f, 0f, 1, 1.7f, staff: false);
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(level.GateOpen(0), "The lever did not open the gate again");
            // the stone block pushed onto the plate opens the second gate
            Assert.IsFalse(level.GateOpen(1), "The second gate stands open");
            game.Teleport(270.5f);
            game.hero.y = 0;
            game.TestInput = i => i.x = 1;
            for (float w = 0; w < 6f && !level.GateOpen(1); w += Time.deltaTime) yield return null;
            game.TestInput = null;
            Assert.Greater(level.PushBlockX, 272f, "Raja did not push the stone block");
            Assert.IsTrue(level.GateOpen(1), "Pushing the block onto the plate did not open the second gate");
            // past the temple the path is plain ground up to the raft river (the gong and its gorge are gone)
            Assert.AreEqual(0f, level.SupportAt(306f, 1f), 0.01f, "There is still a gorge after the temple");
        }

        [UnityTest]
        public IEnumerator TheKeyOpensTheChestAndATokenIsInside()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            // shut without the key
            game.Teleport(420.6f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(game.level.ChestOpen, "The chest opened without its key");
            // the key (up in the treetops) opens it, a tiger token inside
            game.HasKey = true;
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(game.level.ChestOpen, "The key did not open the chest");
            int tokens = game.Tokens;
            for (float w = 0; w < 2f && game.Tokens == tokens; w += Time.deltaTime) { game.Teleport(421.5f); yield return null; }
            Assert.AreEqual(tokens + 1, game.Tokens, "No tiger token in the chest");
        }
    }
}
