using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;
using RacingGame.Garage;
using RacingGame.Progress;
using RacingGame.CameraRig;
using RacingGame.Track;
using RacingGame.UI;
using RacingGame.Weapons;

namespace RacingGame.Race
{
    public enum RaceState { Countdown, Racing, Finished }

    public class Racer
    {
        public string Name;
        public Color Color;
        public CarController Car;
        public RacerProgress Progress;
        public CarHealth Health;
        public WeaponSystem Weapons;
        public bool IsPlayer;
        public bool Finished;
        public int FinishPlace;
        public float FinishTime;
        public int Kills;
    }

    /// <summary>
    /// Builds the race (track, cars, camera, HUD) and runs it:
    /// countdown, laps, positions, pause, finish order and prize money.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        public int totalLaps = 4;
        public int opponentCount = 5;
        public float countdownSeconds = 3f;

        public static readonly int[] PrizeMoney = { 1000, 600, 350, 150, 75, 0 };
        public const int KillBonus = 100;

        public RaceState State { get; private set; }
        public bool Paused { get; private set; }
        public float Countdown { get; private set; }
        public float RaceTime { get; private set; }
        public TrackPath Path { get; private set; }
        public Racer Player { get; private set; }
        public List<Racer> Racers { get; } = new List<Racer>();
        public List<Racer> Standings { get; private set; } = new List<Racer>();

        // Payout breakdown (filled when the player finishes)
        public int EarnedPrize { get; private set; }
        public int EarnedKills { get; private set; }
        public int EarnedPickups { get; private set; }
        public int EarnedThisRace => EarnedPrize + EarnedKills + EarnedPickups;

        public RaceRequest Request { get; private set; }
        /// <summary>Championship points the player earned in this race (-1 if not a championship race).</summary>
        public int ChampionshipPoints { get; private set; } = -1;

        // Player's per-race tracking (records & achievements)
        public float LastLapTime { get; private set; }
        public float BestLapTime => Records.BestLap(Request.Track.Name);
        /// <summary>(lap time, is new record) - raised when the player completes a lap.</summary>
        public event System.Action<float, bool> PlayerLapCompleted;
        float playerLapStart = -1f;
        bool playerWasLast;
        int playerTimesWrecked;

        /// <summary>Opponent names in grid order (championship standings use the same roster).</summary>
        public static string OpponentName(int i) => Opponents[i % Opponents.Length].name;

        public static int TotalMoney
        {
            get => Wallet.Money;
            private set => Wallet.Money = value;
        }

        static readonly (string name, Color color, string model)[] Opponents =
        {
            ("Rip Torque", new Color(0.9f, 0.2f, 0.15f), "vehicle-truck-red"),
            ("Vixen", new Color(0.6f, 0.35f, 0.9f), "vehicle-truck-purple"),
            ("Grim Gaskett", new Color(0.25f, 0.75f, 0.35f), "vehicle-truck-green"),
            ("Nova Blaze", new Color(0.95f, 0.3f, 0.2f), "vehicle-truck-red"),
            ("Krusher", new Color(0.55f, 0.3f, 0.9f), "vehicle-truck-purple"),
            ("Dust Devil", new Color(0.3f, 0.8f, 0.4f), "vehicle-truck-green"),
            ("Hex", new Color(0.9f, 0.25f, 0.25f), "vehicle-truck-red"),
        };

        int finishedCount;

        void Awake()
        {
            Instance = this;
            Request = RaceRequest.Current ?? RaceRequest.QuickRace();
            totalLaps = Request.Laps;
            opponentCount = Request.Opponents;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CarHealth.Wrecked -= OnCarWrecked;
            Time.timeScale = 1f;
        }

        void Start()
        {
            GameFlow.SetupEnvironment(Request.Track.Theme);
            Path = TrackBuilder.Build(transform, Request.Track);
            SpawnRacers();
            SetupCamera();
            gameObject.AddComponent<RaceHUD>();

            CarHealth.Wrecked += OnCarWrecked;
            State = RaceState.Countdown;
            Countdown = countdownSeconds;
        }

        void SpawnRacers()
        {
            int total = opponentCount + 1;
            int playerSlot = Mathf.Min(3, total - 1);   // start mid-pack, like the classics
            int opponentIndex = 0;
            var playerVehicle = PlayerGarage.Selected;

            for (int slot = 0; slot < total; slot++)
            {
                int row = slot / 2;
                float lateral = (slot % 2 == 0 ? -1f : 1f) * Path.RoadWidth * 0.22f;
                float distBehind = 8f + row * 8f + (slot % 2) * 3f;
                Vector3 pos = Path.PointAtDistance(-distBehind, out Vector3 dir);
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                pos += right * lateral + Vector3.up * 0.05f;
                Quaternion rot = Quaternion.LookRotation(dir);

                bool isPlayer = slot == playerSlot;
                string name, model;
                Color color;
                if (isPlayer) { name = "You"; color = playerVehicle.Color; model = playerVehicle.Model; }
                else
                {
                    var o = Opponents[opponentIndex % Opponents.Length];
                    name = o.name; color = o.color; model = o.model;
                    opponentIndex++;
                }

                var car = CarBuilder.Create(name, color, model, pos, rot, transform, isPlayer);
                var racer = new Racer
                {
                    Name = name,
                    Color = color,
                    Car = car,
                    Progress = car.GetComponent<RacerProgress>(),
                    Health = car.GetComponent<CarHealth>(),
                    Weapons = car.GetComponent<WeaponSystem>(),
                    IsPlayer = isPlayer,
                };
                racer.Progress.Initialize(Path);
                racer.Progress.NewLap += (p, lap) => racer.Weapons.Refill();

                if (!isPlayer) ConfigureAI(car, Request);
                else
                {
                    PlayerGarage.ApplyTo(car, PlayerGarage.Stats(playerVehicle));
                    Player = racer;
                    racer.Progress.NewLap += OnPlayerLap;
                }

                Racers.Add(racer);
            }
            Standings = new List<Racer>(Racers);
        }

        static void ConfigureAI(CarController car, RaceRequest req)
        {
            var ai = car.GetComponent<AICarInput>();
            ai.skill = Random.Range(req.AiSkill.x, req.AiSkill.y);
            ai.aggression = Random.Range(req.AiAggression.x, req.AiAggression.y);
            car.maxSpeed = req.AiTopSpeed;
            car.speedMultiplier = Random.Range(req.AiSpeedMultiplier.x, req.AiSpeedMultiplier.y);
            var health = car.GetComponent<CarHealth>();
            if (health) health.SetMaxArmor(req.AiArmor);
        }

        void SetupCamera()
        {
            var cam = GameFlow.GetCamera();
            var menuCam = cam.GetComponent<MenuCamera>();
            if (menuCam) menuCam.enabled = false;

            // Hear the race from the player's car, not from the high camera.
            var camListener = cam.GetComponent<AudioListener>();
            if (camListener) camListener.enabled = false;
            Player.Car.gameObject.AddComponent<AudioListener>();

            RaceCamera.Mode = RaceConfig.Camera;
            var rig = cam.GetComponent<RaceCamera>();
            if (rig == null) rig = cam.gameObject.AddComponent<RaceCamera>();
            rig.enabled = true;
            rig.SetTarget(Player.Car);
        }

        void Update()
        {
            if (InputBridge.WasPressedThisFrame(GameKey.Pause) && State != RaceState.Finished) SetPaused(!Paused);
            if (Paused) return;

            switch (State)
            {
                case RaceState.Countdown:
                    Countdown -= Time.deltaTime;
                    if (Countdown <= 0f)
                    {
                        State = RaceState.Racing;
                        foreach (var r in Racers) r.Car.InputEnabled = true;
                    }
                    break;

                case RaceState.Racing:
                case RaceState.Finished:
                    RaceTime += Time.deltaTime;
                    CheckFinishers();
                    TrackPlayer();
                    break;
            }

            UpdateStandings();

            if (State == RaceState.Finished && InputBridge.WasPressedThisFrame(GameKey.Restart)) Restart();
        }

        void TrackPlayer()
        {
            if (Player.Finished) return;
            Stats.SetMax(Stat.TopSpeedKmh, Mathf.RoundToInt(Player.Car.SpeedKmh));
            if (RaceTime > 3f && PositionOf(Player) == Racers.Count && Racers.Count > 1) playerWasLast = true;
        }

        void OnPlayerLap(RacerProgress progress, int lap)
        {
            if (Player == null || Player.Finished) return;
            if (lap >= 2 && playerLapStart >= 0f)
            {
                LastLapTime = RaceTime - playerLapStart;
                bool record = Records.SubmitLap(Request.Track.Name, LastLapTime);
                PlayerLapCompleted?.Invoke(LastLapTime, record);
            }
            playerLapStart = RaceTime;
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }

        void CheckFinishers()
        {
            foreach (var r in Racers)
            {
                if (r.Finished || r.Progress.Lap <= totalLaps) continue;
                r.Finished = true;
                r.FinishPlace = ++finishedCount;
                r.FinishTime = RaceTime;

                if (r.IsPlayer)
                {
                    // Hand the car to the AI for a victory lap and pay out.
                    r.Car.InputSource = r.Car.GetComponent<AICarInput>();
                    State = RaceState.Finished;
                    AwardMoney(r);
                }
            }
        }

        void AwardMoney(Racer player)
        {
            float mult = Request.PrizeMultiplier;
            int prize = player.FinishPlace - 1 < PrizeMoney.Length ? PrizeMoney[player.FinishPlace - 1] : 0;
            EarnedPrize = Mathf.RoundToInt(prize * mult);
            EarnedKills = player.Kills * KillBonus;
            EarnedPickups = player.Car.MoneyCollected;
            TotalMoney += EarnedThisRace;
            RecordCareer(player);

            if (Request.IsChampionship)
            {
                // Remaining racers are ranked by their current race position.
                UpdateStandings();
                var order = Standings.Select(r => r.Name).ToList();
                ChampionshipPoints = Championship.RecordRace(Request.ChampionshipRace, order);
            }
        }

        /// <summary>Lifetime stats, records and race achievements when the player finishes.</summary>
        void RecordCareer(Racer player)
        {
            int place = player.FinishPlace;
            Stats.Add(Stat.Races);
            Stats.Add(Stat.MoneyEarned, EarnedThisRace);
            Stats.SetBit(Stat.TracksRaced, Mathf.Max(0, TrackLibrary.Tracks.IndexOf(Request.Track)));
            if (place <= 3) Stats.Add(Stat.Podiums);
            if (place == 1)
            {
                Stats.Add(Stat.Wins);
                Stats.Add(Stat.WinStreak);
                Stats.SetMax(Stat.BestWinStreak, Stats.Get(Stat.WinStreak));
            }
            else Stats.Set(Stat.WinStreak, 0);

            Records.SubmitRace(Request.Track.Name, totalLaps, player.FinishTime);

            if (player.Kills >= 3) Achievements.Unlock("rampage");
            if (place == 1 && playerWasLast) Achievements.Unlock("comeback");
            if (place == 1 && playerTimesWrecked == 0) Achievements.Unlock("flawless");
            PlayerPrefs.Save();
        }

        void UpdateStandings()
        {
            Standings = Racers
                .OrderByDescending(r => r.Finished)
                .ThenBy(r => r.Finished ? r.FinishPlace : 0)
                .ThenByDescending(r => r.Progress.TotalProgress)
                .ToList();
        }

        public int PositionOf(Racer racer) => Standings.IndexOf(racer) + 1;

        public Racer RacerFor(CarHealth health) => Racers.FirstOrDefault(r => r.Health == health);

        void OnCarWrecked(CarHealth victim, CarHealth attacker)
        {
            if (Player != null && victim == Player.Health && !Player.Finished)
            {
                playerTimesWrecked++;
                Stats.Add(Stat.TimesWrecked);
            }
            if (attacker == null || attacker == victim) return;
            var r = RacerFor(attacker);
            if (r != null && !r.Finished)
            {
                r.Kills++;
                if (r.IsPlayer) Stats.Add(Stat.Wrecks);
            }
        }

        public void Restart()
        {
            AudioListener.pause = false;
            GameFlow.StartRace();
        }

        public void ExitToMenu()
        {
            AudioListener.pause = false;
            GameFlow.ShowMenu();
        }
    }
}
