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
    /// Builds the world (track, cars, camera, HUD) and runs the race:
    /// countdown, laps, positions, finish order and prize money.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        [Header("Race")]
        public int totalLaps = 4;
        public int opponentCount = 5;
        public float countdownSeconds = 3f;

        public static readonly int[] PrizeMoney = { 1000, 600, 350, 150, 75, 0 };
        public const int KillBonus = 100;
        const string MoneyKey = "money";

        public RaceState State { get; private set; }
        public float Countdown { get; private set; }
        public float RaceTime { get; private set; }
        public TrackPath Path { get; private set; }
        public Racer Player { get; private set; }
        public List<Racer> Racers { get; } = new List<Racer>();
        public List<Racer> Standings { get; private set; } = new List<Racer>();
        public int EarnedThisRace { get; private set; }

        public static int TotalMoney
        {
            get => PlayerPrefs.GetInt(MoneyKey, 0);
            private set { PlayerPrefs.SetInt(MoneyKey, value); PlayerPrefs.Save(); }
        }

        static readonly (string name, Color color)[] Opponents =
        {
            ("Rip Torque", new Color(0.9f, 0.2f, 0.15f)),
            ("Vixen", new Color(0.95f, 0.45f, 0.8f)),
            ("Grim Gaskett", new Color(0.4f, 0.4f, 0.45f)),
            ("Nova Blaze", new Color(1f, 0.6f, 0.1f)),
            ("Krusher", new Color(0.55f, 0.3f, 0.9f)),
            ("Dust Devil", new Color(0.8f, 0.7f, 0.4f)),
            ("Hex", new Color(0.1f, 0.8f, 0.7f)),
        };

        int finishedCount;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CarHealth.Wrecked -= OnCarWrecked;
        }

        void Start()
        {
            SetupEnvironment();
            Path = TrackBuilder.Build(transform);
            SpawnRacers();
            SetupCamera();
            gameObject.AddComponent<RaceHUD>();

            CarHealth.Wrecked += OnCarWrecked;
            State = RaceState.Countdown;
            Countdown = countdownSeconds;
        }

        void SetupEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.38f, 0.5f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.32f, 0.16f, 0.35f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 260f;

            bool hasSun = FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l => l.type == LightType.Directional);
            if (!hasSun)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.transform.SetParent(transform, false);
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.color = new Color(1f, 0.92f, 0.85f);
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            }
        }

        void SpawnRacers()
        {
            int total = opponentCount + 1;
            int playerSlot = Mathf.Min(3, total - 1);   // start mid-pack, like the classics
            int opponentIndex = 0;

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
                string name;
                Color color;
                if (isPlayer) { name = "You"; color = new Color(0.15f, 0.5f, 1f); }
                else
                {
                    var o = Opponents[opponentIndex % Opponents.Length];
                    name = o.name; color = o.color;
                    opponentIndex++;
                }

                var car = CarBuilder.Create(name, color, pos, rot, transform, isPlayer);
                var racer = new Racer
                {
                    Name = name,
                    Car = car,
                    Progress = car.GetComponent<RacerProgress>(),
                    Health = car.GetComponent<CarHealth>(),
                    Weapons = car.GetComponent<WeaponSystem>(),
                    IsPlayer = isPlayer,
                };
                racer.Progress.Initialize(Path);
                racer.Progress.NewLap += (p, lap) => racer.Weapons.Refill();

                if (!isPlayer)
                {
                    var ai = car.GetComponent<AICarInput>();
                    ai.skill = Random.Range(0.45f, 0.95f);
                    ai.aggression = Random.Range(0.3f, 0.9f);
                    car.speedMultiplier = Random.Range(0.9f, 0.98f);
                }
                else Player = racer;

                Racers.Add(racer);
            }
            Standings = new List<Racer>(Racers);
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                go.transform.SetParent(transform, false);
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.farClipPlane = 400f;
            var rig = cam.GetComponent<RaceCamera>();
            if (rig == null) rig = cam.gameObject.AddComponent<RaceCamera>();
            rig.SetTarget(Player.Car);
        }

        void Update()
        {
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

            if (InputBridge.WasPressedThisFrame(GameKey.Restart)) Restart();
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
            int prize = player.FinishPlace - 1 < PrizeMoney.Length ? PrizeMoney[player.FinishPlace - 1] : 0;
            EarnedThisRace = prize + player.Kills * KillBonus + player.Car.MoneyCollected;
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

        void OnCarWrecked(CarHealth victim, CarHealth attacker)
        {
            if (attacker == null || attacker == victim) return;
            var r = Racers.FirstOrDefault(x => x.Health == attacker);
            if (r != null && !r.Finished) r.Kills++;
        }

        /// <summary>Rebuilds the whole race from scratch (works even in an unsaved scene).</summary>
        public void Restart()
        {
            Mine.ClearAll();
            var cam = Camera.main;
            if (cam != null && cam.transform.IsChildOf(transform)) cam.transform.SetParent(null);
            foreach (var light in GetComponentsInChildren<Light>()) light.transform.SetParent(null);
            new GameObject("RaceManager").AddComponent<RaceManager>();
            Destroy(gameObject);
        }
    }
}
