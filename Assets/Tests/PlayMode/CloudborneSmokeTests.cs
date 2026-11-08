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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (game != null) Object.Destroy(game.gameObject);
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
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
            foreach (var fx in new[] { "Fireball", "Beam", "Explosion" })
                Assert.IsNotNull(CbVfx.Painted(fx), $"The painted {fx} effect is missing (Resources/Cloudborne/Effects)");
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
        public IEnumerator HitsThenFireMakeAFireWhirl()
        {
            game = CbGame.Boot();
            yield return Frames(3);
            game.StartGame();
            yield return Frames(2);
            game.hero.ki = 100;
            float t0 = Time.time;
            bool second = false, sawWhirl = false, sawPalm = false;
            // HIT, HIT, then FIRE while the second blow is out
            game.TestInput = i =>
            {
                float tt = Time.time - t0;
                if (tt < 0.01f) i.atk = true;
                if (!second && tt > 0.1f) { i.atk = true; second = true; }
                if (tt > 0.18f && game.hero.state == CbHero.State.Atk) i.kiTap = true;
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
                    case 5: game.hero.ki = 100; i.kiTap = At(0.1f) || At(0.4f); break;      // fireballs
                    case 6: i.guard = pt < 0.6f; break;                                     // guard
                    case 7: game.hero.ki = 100; i.skill = At(0.1f); break;                  // special: flame cannon
                }
                prev = pt;
            };
            int startScore = game.score;
            float t = 0;
            bool sawStaff = false, sawWhirl = false, sawGuard = false, sawSpecial = false;
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
            Assert.IsTrue(sawSpecial, "SPECIAL did not fire the flame cannon");
            Assert.IsNull(game.lockZone, "The first fight zone was not cleared in 25 seconds");
        }
    }
}
