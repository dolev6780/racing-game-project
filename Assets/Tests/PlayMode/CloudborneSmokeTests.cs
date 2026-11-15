using System.Collections;
using System.Collections.Generic;
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
            foreach (var scene in new[] { CbStory.Prologue, CbStory.KodaBoast, CbStory.KodaTaken, CbStory.Journey, CbStory.FireWakes })
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
            // near the gate he steps out: a scene, then the fight in a shut arena (his arena stands 14 m before it)
            game.Teleport(CbWorld.GoalX - 20f);
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
            // no cheering: the gate opened and he walked on through it (the user)
            Assert.AreNotEqual(CbHero.State.Victory, game.hero.state, "Raja cheered at the shrine gate");
            Assert.Greater(game.hero.x, CbWorld.GoalX + 1.6f, "Raja did not walk through the shrine gate");
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
            // hanging from a bar and going along it (Raja_More_Clips)
            Assert.AreEqual(47, game.hero.model.ClipCount, "Raja's model is missing animation clips");
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
                         "koda_boast", "koda_taken", "raja_angry", "raja_journey", "baba_lesson1", "baba_lesson2" })
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
                if ((t.name == "ledge" || t.name == "crumblingLedge") && t.Find("fit") != null) kitLedges++;
            }
            // the long stage's vine pillar and waterfall cliff (the ridge and its shaft are gone; the climb is a gorge:
            // its cliffs are built of rock in rows, CbLevelGorge)
            Assert.AreEqual(2, rock, "The terraces are not bare rock");
            Assert.Greater(kitLedges, 5, "The gorge's ledges are not the cliff kit's slabs");
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
        public IEnumerator RajaSlidesAndCrawlsUnderALowPassage()
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
            // running at it, down: a slide, then a crawl, carries him under
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
            // the cliff across the path stops him standing, and he crawls through the gap at its foot
            Assert.LessOrEqual(level.BlockX(135f, 138f, 0f), CbLevel.MountainFrom - CbLevel.HalfWidth + 0.001f, "Raja walked into the cliff");
            Assert.AreEqual(140.5f, level.BlockX(135f, 140.5f, 0f, CbLevel.LowHeight), 0.001f, "Raja can't crawl through the gap under the cliff");
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
            // in he goes: a moment treading water, then back on the stone
            bool swam = false;
            for (float waited = 0; waited < 1.7f; waited += Time.deltaTime)
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
            // down on the stick at the fallen trunk: he slides under it and crawls out the far side by himself
            float trunk = CbLevel.TrunkX;
            game.Teleport(trunk - 3f);
            hero.y = 0;
            hero.facing = 1;
            yield return Settle();
            first = true;
            bool slid = false;
            game.TestControls = i => { i.x = 1; i.slide = first; first = false; };
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
        public IEnumerator TheRopeBridgeAtTheStartIsAnAmbush()
        {
            // at least five raiders on the rope bridge (the user), in waves; beaten, the way opens. No soldier stands on
            // the rock in the long river after it any more (the user)
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
            int raiders = 0;
            for (int wave = 1; wave <= 6 && !game.RopeAmbushDone; wave++)
            {
                var foes = game.enemies.FindAll(e => !e.dead && Mathf.Abs(e.x - CbLevel.RopeBridgeX) < 12f);
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
            // onto the stone bridge: the ambush
            game.Teleport(CbLevel.StoneBridgeX - 2f);
            game.hero.y = CbLevel.StoneDeck;
            game.hero.vy = 0;
            // (the raiders can't beat him while the test beats them)
            game.hero.inv = 999f;
            yield return Frames(3);
            Assert.AreEqual(1, game.BridgeWave, "No ambush on the stone bridge");
            int raiders = 0;
            for (int wave = 1; wave <= 3; wave++)
            {
                Assert.AreEqual(wave, game.BridgeWave, $"Wave {wave} of the ambush did not come");
                var foes = game.enemies.FindAll(e => !e.dead && Mathf.Abs(e.x - CbLevel.StoneBridgeX) < 12f);
                Assert.Greater(foes.Count, 2, $"Wave {wave} has too few raiders");
                raiders += foes.Count;
                foreach (var e in foes) { e.y = CbLevel.StoneDeck; e.vx = e.vy = 0; e.Kill(1); }
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
            foreach (var clip in new[] { "Crawl", "MonkeyBars", "Motion_Divekick", "slide_light" })
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
            // through the gap onto the gorge's floor: a boulder drops into the gap behind him and the water comes
            game.Teleport(CbLevel.GorgeFrom + 1.5f);
            game.hero.y = 0;
            yield return Frames(3);
            Assert.IsTrue(level.Flooding, "The gorge did not flood once he was in it");
            yield return new WaitForSeconds(0.5f);
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
                    case 6: i.guard = pt < 0.6f; break;                                     // guard
                    // special: the staff storm, pressed until it starts (a hyena behind him may have him reeling)
                    case 7: game.hero.ki = 100; i.skill = pt >= 0.1f && !sawSpecial; break;
                }
                prev = pt;
            };
            int startScore = game.score;
            float t = 0;
            bool sawStaff = false, sawWhirl = false, sawGuard = false;
            while (t < 9f && game.mode == CbGame.Mode.Play)
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
            Assert.IsNull(game.lockZone, "A soldier on his own shut an arena");
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
                ("thorns", 243f, 0f), ("temple", 266f, 0f), ("waterfall", 392.5f, 0f), ("stonebridge", CbLevel.StoneBridgeX - 6f, CbLevel.StoneDeck) });
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
            yield return new WaitForSeconds(1.2f);
            if (CbComic.Available(CbStory.CubInTheClouds))
                Assert.AreEqual(CbGame.Mode.Comic, game.mode, "The drawing behind Baba's scroll was not shown");
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
        public IEnumerator TheStickJumpsAndSlides()
        {
            // no JUMP button (the user): up on the stick jumps, held up its full height, let back and up again the double
            // jump; down slides; running with the stick a little up does not jump, up on a slant does
            bool up = false, down = false;
            var inp = new CbInput();
            CbHud.StickMoves(new Vector2(0, 30), ref up, ref down, inp);
            Assert.IsTrue(inp.jump && inp.jumpHeld && inp.stickJump, "Up on the stick did not jump");
            inp = new CbInput();
            CbHud.StickMoves(new Vector2(3, 45), ref up, ref down, inp);
            Assert.IsFalse(inp.jump, "Holding the stick up jumped again");
            Assert.IsTrue(inp.jumpHeld, "Holding the stick up let go of the jump");
            inp = new CbInput();
            CbHud.StickMoves(new Vector2(0, 5), ref up, ref down, inp);
            Assert.IsFalse(inp.jumpHeld, "The stick let back still held the jump");
            inp = new CbInput();
            CbHud.StickMoves(new Vector2(0, 30), ref up, ref down, inp);
            Assert.IsTrue(inp.jump, "Up on the stick again did not jump again (the double jump)");
            up = down = false;
            inp = new CbInput();
            CbHud.StickMoves(new Vector2(60, 18), ref up, ref down, inp);
            Assert.IsFalse(inp.jump, "Running with the stick a little up jumped");
            Assert.AreEqual(1f, inp.x, 0.01f, "Running with the stick a little up was not a full run");
            inp = new CbInput();
            CbHud.StickMoves(new Vector2(45, 40), ref up, ref down, inp);
            Assert.IsTrue(inp.jump, "Up on a slant while running did not jump");
            up = down = false;
            inp = new CbInput();
            CbHud.StickMoves(new Vector2(0, -30), ref up, ref down, inp);
            Assert.IsTrue(inp.slide && !inp.jump, "Down on the stick did not slide");
            inp = new CbInput();
            CbHud.StickMoves(new Vector2(0, -45), ref up, ref down, inp);
            Assert.IsFalse(inp.slide, "Holding the stick down slid again");
            Assert.Less(inp.y, -0.5f, "Holding the stick down did not keep him low");
            yield break;
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
            // Koda's cage: the scene of the empty cage (skipped, it ends the same)
            Assert.AreEqual(CbGame.Mode.Scene, game.mode, "The empty cage's scene did not play after Tusk");
            game.scene.Skip();
            Assert.IsTrue(game.level.KodaCageBroken, "Skipping the scene left Koda's cage whole");
            yield return WaitForMode(CbGame.Mode.Over, 4f);
            Assert.AreEqual(CbGame.Mode.Over, game.mode, "Stage 1-2 did not end after the empty cage");
        }

        [UnityTest]
        public IEnumerator ZzTheCageAtTheEndOfTheTreetopsIsEmpty()
        {
            // the end of stage 1-2 (the story doc): Raja breaks Koda's cage, it is empty, he finds the prophecy scroll
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
            // the game's own camera (Camera.main can be another scene's: the first pictures were of the kart race)
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
                    if (y > 0) game.hero.state = CbHero.State.Air;
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
            float from = hero.x;
            game.TestInput = i => i.x = 1;
            yield return new WaitForSeconds(0.8f);
            game.TestInput = null;
            Assert.Greater(hero.x, from + 1f, "Raja did not go along the bar");
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
