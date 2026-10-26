using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RacingGame.CameraRig;
using RacingGame.Car;
using RacingGame.Core;
using RacingGame.Garage;
using RacingGame.Progress;
using RacingGame.Race;
using RacingGame.Track;
using RacingGame.UI;

namespace RacingGame.Tests
{
    /// <summary>
    /// End-to-end smoke tests: they start the real game (menu, races, championship) in play mode.
    /// Any exception or Debug.LogError during a test makes it fail automatically.
    /// Note: like playing the game, these tests change saved money/stats in the editor.
    /// </summary>
    public class GameSmokeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            // The bootstrap opens the main menu on play; give it a few frames.
            yield return Frames(5);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            RaceRequest.Current = null;
            var race = Object.FindAnyObjectByType<RaceManager>();
            if (race) Object.Destroy(race.gameObject);
            var menu = Object.FindAnyObjectByType<MainMenu>();
            if (menu) Object.Destroy(menu.gameObject);
            yield return null;
        }

        // ---------- Assets ----------

        [Test]
        public void ModelsSoundsAndFontsLoad()
        {
            foreach (var v in VehicleCatalog.Vehicles)
                Assert.IsNotNull(Resources.Load<GameObject>("Models/" + v.Model),
                    $"Vehicle model '{v.Model}' did not load - is the glTFast package installed?");
            foreach (var m in new[] { "track-finish", "decoration-forest", "decoration-tents" })
                Assert.IsNotNull(Resources.Load<GameObject>("Models/" + m), $"Model '{m}' did not load");
            foreach (var c in new[] { "engine", "impact", "skid" })
                Assert.IsNotNull(Resources.Load<AudioClip>("Audio/" + c), $"Sound '{c}' did not load");
            Assert.IsNotNull(Resources.Load<Font>("Fonts/RussoOne"), "Title font missing");
            Assert.IsNotNull(Resources.Load<Font>("Fonts/RubikBold"), "Body font missing");
            foreach (var sh in new[] { "Toon", "SkyGradient", "BoostPad", "ItemBox", "Sparks", "Skid" })
                Assert.IsNotNull(Resources.Load<Shader>("Shaders/" + sh), $"Shader '{sh}' did not load");
        }

        [Test]
        public void UniversalRenderPipelineIsActive()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            Assert.IsNotNull(pipeline, "No render pipeline asset assigned - run Racing Game > Set Up URP");
            StringAssert.Contains("Universal", pipeline.GetType().Name);
            foreach (var sh in new[] { "Toon", "SkyGradient", "BoostPad", "ItemBox", "Sparks", "Skid" })
                Assert.IsTrue(Resources.Load<Shader>("Shaders/" + sh).isSupported, $"Shader '{sh}' is not supported under URP");
        }

        // ---------- Camera and controls ----------

        [Test]
        public void StickPointsWhereTheKartShouldGo()
        {
            // Screen-up on the isometric view is the camera's flat forward direction.
            Vector3 up = RaceCamera.ScreenToGroundDirection(Vector2.up);
            Assert.Less(Vector3.Angle(up, Quaternion.Euler(0f, RaceCamera.IsoYaw, 0f) * Vector3.forward), 0.5f);

            Vector3 facing = Vector3.forward;
            Assert.AreEqual(0f, PlayerCarInput.AimSteer(facing, facing, false), 0.01f, "Already facing the target");
            Assert.Greater(PlayerCarInput.AimSteer(facing, Vector3.right, false), 0.5f, "Target to the right turns right");
            Assert.Less(PlayerCarInput.AimSteer(facing, Vector3.left, false), -0.5f, "Target to the left turns left");
            // Reversing toward a target behind: tail already points there, so no steering.
            Assert.AreEqual(0f, PlayerCarInput.AimSteer(facing, Vector3.back, true), 0.01f);
        }

        [UnityTest]
        public IEnumerator IsometricCameraKeepsThePlayerInView()
        {
            var saved = RaceConfig.Camera;
            RaceConfig.Camera = CameraMode.Close;
            try
            {
                RaceRequest.Current = new RaceRequest { Track = TrackLibrary.Get(1), Laps = 1, Opponents = 3 };
                GameFlow.StartRace();
                yield return Frames(5);
                var race = RaceManager.Instance;
                race.Player.Car.InputSource = race.Player.Car.GetComponent<AICarInput>();
                Time.timeScale = 3f;
                var cam = Camera.main;
                float end = Time.time + 20f;
                while (Time.time < end)
                {
                    Assert.Less(Quaternion.Angle(cam.transform.rotation, Quaternion.Euler(RaceCamera.IsoPitch, RaceCamera.IsoYaw, 0f)), 6f,
                        "Isometric camera must not turn with the kart");
                    Vector3 vp = cam.WorldToViewportPoint(race.Player.Car.transform.position);
                    Assert.IsTrue(vp.z > 0f && vp.x > 0.1f && vp.x < 0.9f && vp.y > 0.1f && vp.y < 0.9f,
                        $"Player left the view (viewport {vp})");
                    yield return null;
                }
            }
            finally
            {
                Time.timeScale = 1f;
                RaceConfig.Camera = saved;
            }
        }

        [UnityTest]
        public IEnumerator AllTracksBuild()
        {
            foreach (var def in TrackLibrary.Tracks)
            {
                var root = new GameObject("TrackTest");
                var path = TrackBuilder.Build(root.transform, def);
                Assert.Greater(path.Length, 400f, $"{def.Name} is suspiciously short");
                Assert.Greater(path.Count, 100, $"{def.Name} has too few samples");
                yield return null;
                Object.Destroy(root);
                yield return null;
            }
        }

        // ---------- Game flow ----------

        [UnityTest]
        public IEnumerator MainMenuStarts()
        {
            if (Object.FindAnyObjectByType<MainMenu>() == null) GameFlow.ShowMenu();
            yield return Frames(10);
            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenu>(), "Main menu was not created");
            Assert.IsNotNull(GameObject.Find("MenuCanvas"), "Menu UI was not built");
        }

        [UnityTest]
        public IEnumerator QuickRaceRunsToTheFinish()
        {
            RaceRequest.Current = new RaceRequest { Track = TrackLibrary.Get(0), Laps = 1, Opponents = 3 };
            GameFlow.StartRace();
            yield return Frames(5);

            var race = RaceManager.Instance;
            Assert.IsNotNull(race, "RaceManager was not created");
            Assert.AreEqual(4, race.Racers.Count, "Expected player + 3 opponents");
            Assert.IsNotNull(GameObject.Find("HUD"), "HUD was not built");

            yield return RunRaceToFinish(race, 150f);
            Assert.AreEqual(RaceState.Finished, race.State, "Player did not finish the race in time");

            // Every car must have actually driven.
            foreach (var r in race.Racers)
                Assert.Greater(r.Progress.TotalProgress, race.Path.Length * 0.5f, $"{r.Name} barely moved");

            yield return Frames(10);
            Assert.IsNotNull(GameObject.Find("Results"), "Results screen did not appear");

            race.ExitToMenu();
            yield return Frames(10);
            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenu>(), "Did not return to the main menu");
        }

        [UnityTest]
        public IEnumerator ChampionshipRaceAwardsPoints()
        {
            if (Championship.HasActiveSeason)
                Assert.Ignore("A championship season is in progress in this editor - not touching it.");

            Championship.StartSeason(0);
            try
            {
                Assert.AreEqual(0, Championship.NextRace);
                Assert.AreEqual(Championship.Leagues[0].Opponents + 1, Championship.Standings().Count,
                    "Season should start with everyone on zero points");

                var request = Championship.NextRaceRequest();
                request.Laps = 1;   // keep the test short
                RaceRequest.Current = request;
                GameFlow.StartRace();
                yield return Frames(5);

                var race = RaceManager.Instance;
                yield return RunRaceToFinish(race, 150f);
                Assert.AreEqual(RaceState.Finished, race.State, "Championship race did not finish in time");
                Assert.AreEqual(1, Championship.NextRace, "Race result was not recorded");
                Assert.GreaterOrEqual(race.ChampionshipPoints, 0);

                int total = 0;
                foreach (var e in Championship.Standings()) total += e.Points;
                Assert.AreEqual(10 + 7 + 5 + 3 + 2 + 1, total, "Points table not applied to all 6 racers");
            }
            finally
            {
                Championship.Abandon();
            }
        }

        // ---------- Logic ----------

        [Test]
        public void UpgradesImproveStats()
        {
            var v = VehicleCatalog.Vehicles[0];
            var stock = VehicleCatalog.ComputeStats(v, 0, 0, 0, 0, 0);
            var maxed = VehicleCatalog.ComputeStats(v, 4, 4, 4, 4, 4);
            Assert.Greater(maxed.TopSpeed, stock.TopSpeed);
            Assert.Greater(maxed.TurnSpeed, stock.TurnSpeed);
            Assert.Greater(maxed.Armor, stock.Armor);
            Assert.Greater(maxed.Missiles, stock.Missiles);
            Assert.Greater(maxed.Boosts, stock.Boosts);
            Assert.AreEqual(-1, VehicleCatalog.UpgradePrice(UpgradeType.Engine, VehicleCatalog.MaxUpgradeLevel));
        }

        [Test]
        public void AchievementIdsAreUnique()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var a in Achievements.All)
                Assert.IsTrue(seen.Add(a.Id), "Duplicate achievement id " + a.Id);
            Assert.AreEqual(26, Achievements.All.Count);
        }

        // ---------- Helpers ----------

        /// <summary>Lets the AI drive the player's car too, speeds time up, and waits for the finish.</summary>
        static IEnumerator RunRaceToFinish(RaceManager race, float timeoutSeconds)
        {
            race.Player.Car.InputSource = race.Player.Car.GetComponent<AICarInput>();
            Time.timeScale = 4f;
            float start = Time.realtimeSinceStartup;
            while (race != null && race.State != RaceState.Finished &&
                   Time.realtimeSinceStartup - start < timeoutSeconds)
                yield return null;
            Time.timeScale = 1f;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }
    }
}
