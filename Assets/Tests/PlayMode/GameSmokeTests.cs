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
using RacingGame.Weapons;

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
        public IEnumerator KartsHaveDriversWheelsAndSmoke()
        {
            RaceRequest.Current = new RaceRequest { Track = TrackLibrary.Get(0), Laps = 1, Opponents = 3 };
            GameFlow.StartRace();
            yield return Frames(5);
            foreach (var r in RaceManager.Instance.Racers)
            {
                if (r.IsPlayer && !KartBuilder.IsKart(PlayerGarage.Selected.Model)) continue;   // the bike has no kart body
                var car = r.Car;
                int pivots = 0;
                foreach (var t in car.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("WheelPivot")) pivots++;
                Assert.AreEqual(4, pivots, $"{r.Name} should be a four-wheeled kart");
                Assert.IsNotNull(car.transform.Find("Body/BoostFlames"), $"{r.Name} has no exhaust flames");
                Assert.IsNotNull(car.GetComponent<DamageSmoke>(), $"{r.Name} has no damage smoke");
            }
        }


        // ---------- Phase 3: items, coins, ramps, drifting, off-road, AI ----------

        class ScriptedInput : ICarInputSource
        {
            public CarInputState State;
            public CarInputState ReadInput() => State;
        }

        static IEnumerator StartRace(int track, int opponents)
        {
            RaceRequest.Current = new RaceRequest { Track = TrackLibrary.Get(track), Laps = 1, Opponents = opponents };
            GameFlow.StartRace();
            yield return Frames(5);
        }

        static IEnumerator WaitForGreenLight(RaceManager race)
        {
            float end = Time.time + 6f;
            while (race.State != RaceState.Racing && Time.time < end) yield return null;
            Assert.AreEqual(RaceState.Racing, race.State, "Race did not start");
        }

        static Racer Opponent(RaceManager race, int n)
        {
            foreach (var r in race.Racers)
                if (!r.IsPlayer && n-- == 0) return r;
            return null;
        }

        [UnityTest]
        public IEnumerator CoinsAddSpeedAndAreLostOnHitsAndWrecks()
        {
            yield return StartRace(0, 3);
            var race = RaceManager.Instance;
            var car = race.Player.Car;
            for (int i = 0; i < 12; i++) car.AddCoin();
            Assert.AreEqual(CarController.MaxCoins, car.Coins, "Coins should cap at the maximum");
            car.LoseCoins(2);
            Assert.AreEqual(CarController.MaxCoins - 2, car.Coins);
            race.Player.Health.TakeDamage(10000f, null);
            yield return Frames(2);
            Assert.AreEqual((CarController.MaxCoins - 2) / 2, car.Coins, "A wreck should cost half the coins");
        }

        [UnityTest]
        public IEnumerator CoinPickupGivesACoinAndRespawnsLater()
        {
            yield return StartRace(0, 3);
            var car = RaceManager.Instance.Player.Car;
            Assert.Greater(Coin.All.Count, 20, "Track has no coins");
            var coin = Coin.All[0];
            coin.transform.position = car.transform.position + Vector3.up;
            yield return Frames(3);
            Assert.AreEqual(1, car.Coins, "Driving through a coin should collect it");
            Assert.IsFalse(coin.Available, "A collected coin should disappear until it respawns");
        }

        [Test]
        public void ItemBoxOddsFavourKartsAtTheBack()
        {
            int frontStrong = 0, backStrong = 0, frontShock = 0;
            for (int i = 0; i < 1000; i++)
            {
                float roll = (i + 0.5f) / 1000f;
                var front = Pickup.RollItem(0f, roll);
                var back = Pickup.RollItem(1f, roll);
                if (front == ItemReward.Shield || front == ItemReward.Shockwave) frontStrong++;
                if (back == ItemReward.Shield || back == ItemReward.Shockwave) backStrong++;
                if (front == ItemReward.Shockwave) frontShock++;
            }
            Assert.AreEqual(0, frontShock, "The leader never gets a shockwave");
            Assert.Greater(backStrong, frontStrong * 3, "Last place should get shields and shockwaves far more often");
        }

        [UnityTest]
        public IEnumerator SpecialItemSlotHoldsOneItem()
        {
            yield return StartRace(0, 3);
            var weapons = RaceManager.Instance.Player.Weapons;
            Assert.IsTrue(weapons.GiveSpecial(SpecialItem.Shield));
            Assert.IsFalse(weapons.GiveSpecial(SpecialItem.OilSlick), "The slot holds one item at a time");
            weapons.UseSpecial();
            Assert.AreEqual(SpecialItem.None, weapons.Special);
            Assert.IsTrue(RaceManager.Instance.Player.Health.HasShield, "Using a shield item should raise the shield");
        }

        [UnityTest]
        public IEnumerator ShieldBlocksOneHit()
        {
            yield return StartRace(0, 3);
            var hp = RaceManager.Instance.Player.Health;
            hp.ActivateShield(WeaponSystem.ShieldSeconds);
            float armor = hp.Armor;
            hp.TakeDamage(30f, null);
            Assert.AreEqual(armor, hp.Armor, "The shield should absorb the hit");
            Assert.IsFalse(hp.HasShield, "The shield pops after one hit");
            hp.TakeDamage(30f, null);
            Assert.Less(hp.Armor, armor, "Without the shield the next hit lands");
        }

        [UnityTest]
        public IEnumerator OilSlickSpinsOutAKart()
        {
            yield return StartRace(0, 3);
            var victim = Opponent(RaceManager.Instance, 0).Car;
            OilSlick.Spawn(victim.transform.position, null);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(victim.IsSpinning, "Driving onto oil should spin the kart out");
        }

        [UnityTest]
        public IEnumerator ShockwaveHitsKartsInRangeOnly()
        {
            yield return StartRace(0, 3);
            var race = RaceManager.Instance;
            var me = race.Player.Car;
            var near = Opponent(race, 0);
            var far = Opponent(race, 1);
            near.Car.ResetTo(me.transform.position + me.transform.right * 4f, me.transform.rotation);
            far.Car.ResetTo(me.transform.position + me.transform.forward * 40f, me.transform.rotation);
            yield return new WaitForFixedUpdate();
            float nearArmor = near.Health.Armor, farArmor = far.Health.Armor;
            Shockwave.Fire(me.transform.position, WeaponSystem.ShockwaveRadius, WeaponSystem.ShockwaveDamage, race.Player.Health);
            Assert.Less(near.Health.Armor, nearArmor, "A kart within range should be hit");
            Assert.AreEqual(farArmor, far.Health.Armor, "A kart out of range should not be hit");
        }

        [UnityTest]
        public IEnumerator DriftButtonLoosensGrip()
        {
            yield return StartRace(1, 1);
            var race = RaceManager.Instance;
            yield return WaitForGreenLight(race);
            var car = race.Player.Car;
            car.InputSource = new ScriptedInput { State = new CarInputState { Throttle = 1f, Steer = 0.4f, Drift = true } };
            Assert.AreEqual(car.grip, car.CurrentGrip, "Full grip before the drift button does anything");
            float lowest = car.grip;
            bool fast = false;
            float end = Time.time + 4f;
            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();
                if (car.ForwardSpeed < 13f) continue;   // the drift button only works at speed
                fast = true;
                lowest = Mathf.Min(lowest, car.CurrentGrip);
            }
            Assert.IsTrue(fast, "The kart never got up to drifting speed");
            Assert.Less(lowest, car.grip * 0.5f, "Holding drift at speed should loosen the grip");
        }

        [UnityTest]
        public IEnumerator RampLaunchesKartsAndTricksBoost()
        {
            yield return StartRace(0, 1);
            var race = RaceManager.Instance;
            yield return WaitForGreenLight(race);
            var ramp = Object.FindAnyObjectByType<Ramp>();
            Assert.IsNotNull(ramp, "Track has no ramp");
            var car = race.Player.Car;
            bool landed = false;
            car.TrickLanded += () => landed = true;
            car.InputSource = new ScriptedInput { State = new CarInputState { Throttle = 1f, Trick = true } };
            car.ResetTo(ramp.Foot - ramp.Direction * 14f + Vector3.up * 0.1f, Quaternion.LookRotation(ramp.Direction));
            car.Body.linearVelocity = ramp.Direction * 26f;
            float end = Time.time + 3f;
            while (Time.time < end && !landed) yield return null;
            Assert.Greater(car.LastAirTime, 0.25f, "The ramp should launch the kart into the air");
            Assert.IsTrue(landed, "A trick in the air should land with a boost");
        }

        [UnityTest]
        public IEnumerator OffRoadIsDetected()
        {
            yield return StartRace(0, 1);
            var race = RaceManager.Instance;
            var car = race.Player.Car;
            int i = race.Player.Progress.CurrentIndex;
            car.ResetTo(race.Path.Point(i) + race.Path.Right(i) * 25f + Vector3.up * 0.3f, car.transform.rotation);
            yield return Frames(3);
            Assert.IsTrue(car.OffRoad, "25 m from the centre line is off the road");
            car.ResetTo(race.Path.Point(i) + Vector3.up * 0.3f, car.transform.rotation);
            yield return Frames(3);
            Assert.IsFalse(car.OffRoad, "On the centre line is on the road");
        }

        [UnityTest]
        public IEnumerator AiDriversHaveMixedPersonalities()
        {
            yield return StartRace(0, 5);
            var seen = new System.Collections.Generic.HashSet<AiPersonality>();
            foreach (var r in RaceManager.Instance.Racers)
                if (!r.IsPlayer) seen.Add(r.Car.GetComponent<AICarInput>().personality);
            Assert.GreaterOrEqual(seen.Count, 3, "Expected aggressive, defensive and collector drivers");
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
                var track = root.transform.Find("Track");
                Assert.GreaterOrEqual(track.Find("ChevronSigns").childCount, 4 * 5, $"{def.Name}: expected chevron signs on its sharp bends");
                Assert.Greater(track.Find("Flags").childCount, 0, $"{def.Name}: no flags at the start");
                Assert.AreEqual(1, track.Find("Ramps").childCount, $"{def.Name}: expected one ramp");
                Assert.Greater(track.Find("Coins").childCount, 20, $"{def.Name}: too few coins");
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
