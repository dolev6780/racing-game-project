using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RacingGame.Car;
using RacingGame.Core;
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
        const string MoneyKey = "money";

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

        public static int TotalMoney
        {
            get => PlayerPrefs.GetInt(MoneyKey, 0);
            private set { PlayerPrefs.SetInt(MoneyKey, value); PlayerPrefs.Save(); }
        }

        public const string PlayerModel = "vehicle-truck-yellow";
        public static readonly Color PlayerColor = new Color(1f, 0.8f, 0.15f);

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
            totalLaps = RaceConfig.Laps;
            opponentCount = RaceConfig.Opponents;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CarHealth.Wrecked -= OnCarWrecked;
            Time.timeScale = 1f;
        }

        void Start()
        {
            GameFlow.SetupEnvironment();
            Path = TrackBuilder.Build(transform);
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
            var difficulty = RaceConfig.Difficulty;

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
                if (isPlayer) { name = "You"; color = PlayerColor; model = PlayerModel; }
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

                if (!isPlayer) ConfigureAI(car, difficulty);
                else Player = racer;

                Racers.Add(racer);
            }
            Standings = new List<Racer>(Racers);
        }

        static void ConfigureAI(CarController car, Difficulty difficulty)
        {
            var ai = car.GetComponent<AICarInput>();
            switch (difficulty)
            {
                case Difficulty.Easy:
                    ai.skill = Random.Range(0.25f, 0.55f);
                    ai.aggression = Random.Range(0.15f, 0.45f);
                    car.speedMultiplier = Random.Range(0.82f, 0.9f);
                    break;
                case Difficulty.Hard:
                    ai.skill = Random.Range(0.75f, 1f);
                    ai.aggression = Random.Range(0.6f, 1f);
                    car.speedMultiplier = Random.Range(0.97f, 1.04f);
                    break;
                default:
                    ai.skill = Random.Range(0.45f, 0.95f);
                    ai.aggression = Random.Range(0.3f, 0.9f);
                    car.speedMultiplier = Random.Range(0.9f, 0.98f);
                    break;
            }
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

            RaceCamera.Isometric = RaceConfig.IsometricCamera;
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
                    break;
            }

            UpdateStandings();

            if (State == RaceState.Finished && InputBridge.WasPressedThisFrame(GameKey.Restart)) Restart();
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
            float mult = RaceConfig.PrizeMultiplier;
            int prize = player.FinishPlace - 1 < PrizeMoney.Length ? PrizeMoney[player.FinishPlace - 1] : 0;
            EarnedPrize = Mathf.RoundToInt(prize * mult);
            EarnedKills = player.Kills * KillBonus;
            EarnedPickups = player.Car.MoneyCollected;
            TotalMoney += EarnedThisRace;
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
            if (attacker == null || attacker == victim) return;
            var r = RacerFor(attacker);
            if (r != null && !r.Finished) r.Kills++;
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
