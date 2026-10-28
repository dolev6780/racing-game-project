using System.Collections;
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
            Assert.Greater(game.world.root.GetComponentsInChildren<MeshRenderer>().Length, 20, "The jungle has too few parts");
            Assert.IsNotNull(Resources.Load<Shader>("Shaders/Glow"), "Glow shader missing");
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
                    case 1: i.atk = At(0) || At(0.12f); i.atkDown = pt < 0.7f; break;      // HIT, HIT, hold: whirl
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
