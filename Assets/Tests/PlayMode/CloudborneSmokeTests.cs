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
    /// (paw combo, staff whirl, launcher, thrust, slam, fireball, charge, roar, dash). Any exception or
    /// Debug.LogError fails the test.
    /// </summary>
    public class CloudborneSmokeTests
    {
        CbGame game;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (game != null) Object.Destroy(game.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest]
        public IEnumerator BuildsTheJungleAndRaja()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            Assert.AreEqual(CbGame.Mode.Title, game.mode);
            Assert.IsNotNull(game.hero.f.root, "Raja was not built");
            Assert.IsNotNull(game.hero.model, "Raja's model did not load (Resources/Cloudborne/Raja, legacy animation)");
            Assert.AreEqual(20, game.hero.model.ClipCount, "Raja's model is missing animation clips");
            foreach (var prop in new[] { "Staff", "Machete", "Shield" })
                Assert.IsNotNull(Resources.Load<GameObject>($"Cloudborne/Props/{prop}/{prop}"), $"The {prop} model is missing");
            foreach (var plant in new[] { "Tree", "Palm", "Fern", "Leaves" })
            {
                var model = Resources.Load<GameObject>($"Cloudborne/Jungle/{plant}/{plant}");
                Assert.IsNotNull(model, $"The jungle's {plant} model is missing");
                Assert.IsTrue(model.GetComponentInChildren<MeshFilter>().sharedMesh.isReadable, $"The {plant} mesh must be readable to be merged");
            }
            // the plants are merged into the scenery: no copy of a model is left standing on its own
            Assert.AreEqual(0, game.world.root.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Tree") || t.name.StartsWith("Palm")),
                "Copies of the plant models were left unmerged");
            Assert.Greater(game.world.root.GetComponentsInChildren<MeshRenderer>().Length, 20, "The jungle has too few parts");
            Assert.IsNotNull(Resources.Load<Shader>("Shaders/Glow"), "Glow shader missing");
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
                    // HIT, HIT, hold: whirl. Waits for phase 0's four-hit combo, which runs about 0.3 s past it
                    case 1: i.atk = At(0.25f) || At(0.37f); i.atkDown = pt > 0.37f; break;
                    case 2: i.y = 1; i.atk = At(0.1f); break;                               // launcher
                    case 3: i.dash = At(0.05f); i.atk = At(0.12f); break;                   // dash, then thrust
                    case 4: i.jump = At(0); if (pt > 0.25f) i.y = -1; i.atk = At(0.3f); break; // slam
                    case 5: game.hero.ki = 100; i.kiTap = At(0.1f) || At(0.4f); break;      // fireballs
                    case 6: i.charging = pt < 0.6f; break;                                  // charge
                    case 7: game.hero.ki = 100; i.skill = At(0.1f); break;                  // roar
                }
                prev = pt;
            };
            int startScore = game.score;
            float t = 0;
            bool sawStaff = false, sawWhirl = false;
            while (t < 25f && game.mode == CbGame.Mode.Play && game.lockZone != null || t < 1f)
            {
                t += Time.deltaTime;
                if (game.hero.state == CbHero.State.Staff) sawStaff = true;
                if (game.hero.move == CbHero.Move.Whirl) sawWhirl = true;
                yield return null;
            }
            game.TestInput = null;
            Assert.Greater(game.score, startScore, "No hit landed");
            Assert.IsTrue(sawStaff, "No staff move was used");
            Assert.IsTrue(sawWhirl, "HIT, HIT, hold did not whirl the staff");
            Assert.IsNull(game.lockZone, "The first fight zone was not cleared in 25 seconds");
        }
    }
}
