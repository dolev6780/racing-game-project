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
            Assert.AreEqual(CbHero.State.Victory, game.hero.state, "Raja did not cheer at the shrine gate");
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
            // 42 in his own file (the slide among them), and the dive kick and the crawl made later on a new rig of the
            // same model (Raja_Clips: its own copy of the slide is not added twice)
            Assert.AreEqual(44, game.hero.model.ClipCount, "Raja's model is missing animation clips");
            // from Meshy's animation library: catching an edge, pulling up over it, treading water, getting up, cheering;
            // hanging from a vine, climbing it, along a bar, pushing, walking the log; the dive kick from a description
            foreach (var clip in new[] { "Jump_and_Grab_Wall", "Ladder_Climb_Finish", "Swim_Idle", "Stand_Up1", "Victory_Cheer",
                "Rope_Hang_Idle", "Climb_Up_Rope", "Slow_Bar_Hang_Left", "Push_and_Walk_Forward", "Tightrope_Walk", "Motion_Divekick", "slide_light", "Motion_Crawl" })
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
            foreach (var m in new[] { "Lantern", "Snapper", "Cage", "Raft", "Statue", "Gate", "Lever", "Gong", "Leaf", "Chest",
                "LogRam", "BambooBar", "Thorns", "PushBlock", "Seesaw", "Lift", "ZipPost" })
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
            // the mountain's five, and the long stage's vine pillar, waterfall cliff and the ridge's shaft
            Assert.AreEqual(9, rock, "The terraces are not bare rock");
            Assert.Greater(kitLedges, 5, "The mountain's ledges are not the cliff kit's slabs");
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
            foreach (var m in new[] { "RopeBridge", "StoneBridge", "Log", "SteppingStone" })
                Assert.IsNotNull(Resources.Load<GameObject>($"Cloudborne/Jungle/{m}/{m}"), $"The {m} model is missing");
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            var level = game.level;
            // over a stream there is nothing to stand on (between two stones of the long river)...
            Assert.Less(level.SupportAt(96.3f, 1f), -1f, "There is ground in the river between the stones");
            // ...but the rope bridge's planks hold him, walked onto from the bank and sagging in the middle - except where
            // its planks are missing
            var (p0, p1, _) = CbLevel.RopeSection(4);
            Assert.That(level.SupportAt((p0 + p1) / 2, 0f), Is.InRange(-0.7f, 0f), "The rope bridge does not hold him in the middle");
            Assert.That(level.SupportAt(CbLevel.RopeBridgeX - CbLevel.RopeDeckHalf + 0.1f, 0f), Is.InRange(0f, 0.3f), "The rope bridge can't be walked onto from the bank");
            var (h0, h1, hole) = CbLevel.RopeSection(5);
            Assert.IsTrue(hole, "The rope bridge has no gap in its planks");
            Assert.Less(level.SupportAt((h0 + h1) / 2, 0f), -1f, "Raja stands on the rope bridge where its planks are missing");
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
            float wallLeft = CbLevel.WallX - 0.65f - CbLevel.HalfWidth;
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
        public IEnumerator ASoldierKnockedIntoAHoleInTheBridgeFallsIn()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            game.SkipFights();
            yield return Frames(2);
            // a soldier on the rope bridge just short of a hole in its planks (his stretch ends before it)
            var (h0, h1, hole) = CbLevel.RopeSection(5);
            Assert.IsTrue(hole);
            var e = new CbEnemy(game, CbEnemy.Kind.Hyena, h0 - 0.9f, game.transform) { minX = h0 - 3f, maxX = h0 - 0.6f };
            game.enemies.Add(e);
            game.Teleport(h0 - 6f);
            yield return Frames(3);
            // walking, he keeps to his stretch; a blow that throws him towards the hole sends him through it
            Assert.Less(e.x, h0, "The soldier walked into the hole");
            int score = game.score;
            game.HurtEnemy(e, 1f, 1, 8f, 2f, false, false, false, true);
            for (float w = 0; w < 3f && game.enemies.Contains(e); w += Time.deltaTime) yield return null;
            Assert.IsFalse(game.enemies.Contains(e), "The soldier was not thrown through the hole");
            Assert.IsTrue(e.dead, "A soldier in the stream is not beaten");
            Assert.Greater(game.score, score + 200, "No bonus for throwing him in");
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
            Assert.IsFalse(level.WallAt(CbLevel.WallX - 0.65f - CbLevel.HalfWidth, 1f, 1), "Raja can wall-jump up the cracked wall");
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
            foreach (var (name, x, y) in new[] { ("start", 6f, 0f), ("trunk", 21.5f, 0f), ("bridge", 40f, 0f), ("river", 75f, 0f), ("gate", 133f, 0f), ("gorge", 145.1f, 5.4f), ("bars", 219f, 4.8f - 1.71f),
                ("thorns", 243f, 0f), ("temple", 266f, 0f), ("seesaw", 433f, 0f) })
            {
                game.Teleport(x);
                game.hero.y = y;
                game.hero.vx = game.hero.vy = 0;
                if (y > 0) game.hero.state = CbHero.State.Air;
                // the gorge with its water on the way up
                if (name == "gorge") game.level.StartFlood(2.4f);
                // the camera eases after him
                yield return new WaitForSeconds(2.5f);
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
        public IEnumerator VinesBarsAndTheZipline()
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
            // the zipline: he slides down it
            var zip = level.zips[0];
            game.Teleport(zip.x0 + 1.5f);
            hero.y = zip.At(zip.x0 + 1.5f) - 1.48f;
            hero.vx = hero.vy = 0;
            hero.state = CbHero.State.Air;
            yield return Frames(3);
            Assert.AreEqual(CbHero.State.Zip, hero.state, "Raja did not take hold of the zipline");
            float x0 = hero.x, y0 = hero.y;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(hero.x, x0 + 1f, "Raja did not slide along the zipline");
            Assert.Less(hero.y, y0, "Raja did not slide down the zipline");
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
            // the hidden pit: stood on, its leaves give way
            game.Teleport(255.2f);
            game.hero.y = 0;
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(level.PitOpen(0), "The leaves over the pit held");
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
            game.Teleport(279.5f);
            game.hero.y = 0;
            game.TestInput = i => i.x = 1;
            for (float w = 0; w < 6f && !level.GateOpen(1); w += Time.deltaTime) yield return null;
            game.TestInput = null;
            Assert.Greater(level.PushBlockX, 281f, "Raja did not push the stone block");
            Assert.IsTrue(level.GateOpen(1), "Pushing the block onto the plate did not open the second gate");
            // the gong lays a bridge across the gorge
            Assert.Less(level.SupportAt(306f, 1f), -1f, "The gorge has a bridge before the gong");
            level.Strike(295.6f, 0f, 1, 1.7f, staff: false);
            Assert.IsTrue(level.GongRung, "The gong was not struck");
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(0f, level.SupportAt(306f, 1f), 0.01f, "The gong did not lay the bridge");
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
            game.Teleport(466.6f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(game.level.ChestOpen, "The chest opened without its key");
            // the key (up in the treetops) opens it, a tiger token inside
            game.HasKey = true;
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(game.level.ChestOpen, "The key did not open the chest");
            int tokens = game.Tokens;
            for (float w = 0; w < 2f && game.Tokens == tokens; w += Time.deltaTime) { game.Teleport(467.5f); yield return null; }
            Assert.AreEqual(tokens + 1, game.Tokens, "No tiger token in the chest");
        }
    }
}
