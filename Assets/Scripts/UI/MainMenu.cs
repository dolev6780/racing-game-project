using UnityEngine;
using UnityEngine.UI;
using RacingGame.CameraRig;
using RacingGame.Core;
using RacingGame.Garage;
using RacingGame.Race;
using RacingGame.Track;

namespace RacingGame.UI
{
    /// <summary>
    /// Main menu: 3D backdrop (the real track, trucks parked at the start arch, orbiting camera)
    /// with Home, Race Setup and Settings screens.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public const string GameTitle = "RIOT RACERS";   // placeholder name - change any time

        static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD" };

        public enum MenuScreen { Home, Championship }
        /// <summary>Screen to open when the menu is next shown (e.g. back to the season hub after a race).</summary>
        public static MenuScreen PendingScreen = MenuScreen.Home;

        Canvas canvas;
        GameObject home, setup, garage, shade, championship;
        bool garageFromChampionship;
        Text bankText;

        // Showroom
        GameObject showcase;
        string showcaseModel;
        Vector3 showcasePos, homeCenter;
        Quaternion showcaseRot;
        MenuCamera orbit;

        void Start()
        {
            GameFlow.SetupEnvironment();
            BuildBackdrop();
            BuildUI();

            if (PendingScreen == MenuScreen.Championship) ShowChampionship();
            PendingScreen = MenuScreen.Home;
        }

        // ---------- 3D backdrop ----------

        void BuildBackdrop()
        {
            var path = TrackBuilder.Build(transform);
            Vector3 start = path.Point(0);
            Vector3 dir = path.Direction(0);
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

            // Your selected vehicle in front, two rivals behind.
            showcasePos = start + dir * 6f;
            showcaseRot = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, -25f, 0f);
            ShowcaseVehicle(PlayerGarage.Selected);
            PlaceTruck("vehicle-truck-red", start - dir * 1f - right * 4f, Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 15f, 0f));
            PlaceTruck("vehicle-truck-purple", start - dir * 2f + right * 4.5f, Quaternion.LookRotation(dir) * Quaternion.Euler(0f, -10f, 0f));

            var cam = GameFlow.GetCamera();
            var raceCam = cam.GetComponent<RaceCamera>();
            if (raceCam) raceCam.enabled = false;
            var listener = cam.GetComponent<AudioListener>();
            if (listener == null) listener = cam.gameObject.AddComponent<AudioListener>();
            listener.enabled = true;
            cam.fieldOfView = 45f;

            orbit = cam.GetComponent<MenuCamera>();
            if (orbit == null) orbit = cam.gameObject.AddComponent<MenuCamera>();
            orbit.enabled = true;
            homeCenter = start + dir * 3f;
            orbit.Configure(homeCenter, 16f, 5.5f, 0f, 7f);
        }

        /// <summary>Swaps the vehicle on display (used by the garage when browsing).</summary>
        public void ShowcaseVehicle(VehicleDef v)
        {
            if (showcase != null && showcaseModel == v.Model) return;
            if (showcase) Destroy(showcase);
            showcaseModel = v.Model;
            showcase = ModelLibrary.Instantiate(v.Model, transform);
            if (showcase == null) return;
            showcase.transform.SetPositionAndRotation(showcasePos, showcaseRot);
            showcase.transform.localScale = Vector3.one * 1.3f;
        }

        public void RefreshBank()
        {
            if (bankText) bankText.text = UIKit.Money(Wallet.Money);
        }

        void PlaceTruck(string model, Vector3 pos, Quaternion rot)
        {
            var go = ModelLibrary.Instantiate(model, transform);
            if (go == null) return;
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * 1.3f;
        }

        // ---------- UI ----------

        void BuildUI()
        {
            canvas = UIKit.CreateCanvas("MenuCanvas", transform, 20);

            // Darken the left side so the menu text pops over the 3D scene.
            var shadeRt = UIKit.Rect(canvas.transform, "Shade", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(1500f, 2400f));
            UIKit.Image(shadeRt, UIKit.SoftGlow, new Color(0f, 0f, 0f, 0.55f));
            shade = shadeRt.gameObject;

            // Bank chip (top-right)
            var bank = UIKit.Panel(canvas.transform, "Bank", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -36f),
                new Vector2(360f, 90f)).rectTransform;
            bankText = UIKit.Label(bank, UIKit.Money(RaceManager.TotalMoney), 44, UIKit.Accent, TextAnchor.MiddleCenter, true);

            ShowHome();
        }

        void ClearScreens()
        {
            if (setup) Destroy(setup);
            if (home) Destroy(home);
            if (garage) Destroy(garage);
            if (championship) Destroy(championship);
        }

        void HomeCamera()
        {
            shade.SetActive(true);
            ShowcaseVehicle(PlayerGarage.Selected);
            orbit.lookOffset = new Vector3(0f, 1.2f, 0f);
            orbit.Configure(homeCenter, 16f, 5.5f, 0f, 7f);
        }

        public void ShowHome()
        {
            ClearScreens();
            RefreshBank();
            HomeCamera();

            var root = UIKit.Stretch(canvas.transform, "Home");
            home = root.gameObject;
            home.AddComponent<CanvasGroup>();
            home.AddComponent<FadeIn>();

            var left = new Vector2(0f, 0.5f);
            UIKit.Label(root, GameTitle, 130, UIKit.Accent, left, left, new Vector2(110f, 300f), new Vector2(1200f, 170f),
                TextAnchor.MiddleLeft, true);
            UIKit.Label(root, "COMBAT RACING", 46, UIKit.TextMain, left, left, new Vector2(118f, 200f), new Vector2(1000f, 60f),
                TextAnchor.MiddleLeft);

            // Hot stripe under the title
            var stripe = UIKit.Rect(root, "Stripe", left, left, new Vector2(116f, 160f), new Vector2(420f, 10f));
            UIKit.Image(stripe, UIKit.RoundedSmall, UIKit.AccentHot);

            var btnSize = new Vector2(560f, 100f);
            string champLabel = Championship.HasActiveSeason ? "CONTINUE SEASON" : "CHAMPIONSHIP";
            UIKit.Button(root, champLabel, left, left, new Vector2(110f, 50f), btnSize, ShowChampionship,
                UIKit.Accent, new Color(0.1f, 0.1f, 0.12f), 48);
            UIKit.Button(root, "QUICK RACE", left, left, new Vector2(110f, -64f), btnSize, ShowSetup, fontSize: 44);
            UIKit.Button(root, "GARAGE", left, left, new Vector2(110f, -178f), btnSize, () => OpenGarage(false), fontSize: 44);
            UIKit.Button(root, "SETTINGS", left, left, new Vector2(110f, -292f), btnSize, ShowSettings, fontSize: 44);
            if (Application.platform != RuntimePlatform.IPhonePlayer)
                UIKit.Button(root, "QUIT", left, left, new Vector2(110f, -406f), btnSize, Quit, fontSize: 44);

            UIKit.Label(root, "3D art & sounds: Kenney (CC0)", 26, UIKit.TextDim, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 24f), new Vector2(700f, 40f), TextAnchor.LowerRight);
        }

        public void ShowChampionship()
        {
            ClearScreens();
            HomeCamera();
            RefreshBank();
            championship = new GameObject("Championship", typeof(RectTransform));
            championship.transform.SetParent(canvas.transform, false);
            championship.AddComponent<ChampionshipScreen>().Init(this);
        }

        void ShowSetup()
        {
            ClearScreens();
            var root = UIKit.Stretch(canvas.transform, "Setup");
            setup = root.gameObject;
            setup.AddComponent<CanvasGroup>();
            setup.AddComponent<FadeIn>();

            var panel = UIKit.Panel(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(980f, 820f)).rectTransform;
            UIKit.Label(panel, "QUICK RACE", 64, UIKit.Accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(900f, 90f), TextAnchor.MiddleCenter, true);

            const float w = 880f;
            var laps = System.Array.ConvertAll(RaceConfig.LapOptions, l => l.ToString());
            var trackNames = TrackLibrary.Tracks.ConvertAll(t => t.Name).ToArray();
            UIKit.OptionRow(panel, "TRACK", -150f, w, trackNames, Mathf.Clamp(RaceConfig.TrackIndex, 0, trackNames.Length - 1),
                i => RaceConfig.TrackIndex = i);
            UIKit.OptionRow(panel, "LAPS", -250f, w, laps, Mathf.Max(0, System.Array.IndexOf(RaceConfig.LapOptions, RaceConfig.Laps)),
                i => RaceConfig.Laps = RaceConfig.LapOptions[i]);
            var opp = System.Array.ConvertAll(RaceConfig.OpponentOptions, o => o.ToString());
            UIKit.OptionRow(panel, "OPPONENTS", -350f, w, opp,
                Mathf.Max(0, System.Array.IndexOf(RaceConfig.OpponentOptions, RaceConfig.Opponents)),
                i => RaceConfig.Opponents = RaceConfig.OpponentOptions[i]);
            UIKit.OptionRow(panel, "DIFFICULTY", -450f, w, DifficultyNames, (int)RaceConfig.Difficulty,
                i => RaceConfig.Difficulty = (Difficulty)i);

            UIKit.Label(panel, "Harder races pay more prize money", 28, UIKit.TextDim, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -550f), new Vector2(880f, 50f), TextAnchor.MiddleCenter);

            UIKit.Button(panel, "BACK", new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 36f),
                new Vector2(320f, 100f), ShowHome, fontSize: 44);
            UIKit.Button(panel, "START", new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20f, 36f),
                new Vector2(380f, 100f), StartRace, UIKit.Accent, new Color(0.1f, 0.1f, 0.12f), 52);
        }

        public void OpenGarage(bool fromChampionship)
        {
            ClearScreens();
            garageFromChampionship = fromChampionship;
            shade.SetActive(false);
            // Close-up orbit, vehicle framed on the left, panel on the right.
            orbit.lookOffset = new Vector3(0f, 0.9f, 0f);
            orbit.Configure(showcasePos, 8.5f, 3.2f, 2.8f, 10f);

            garage = new GameObject("Garage", typeof(RectTransform));
            garage.transform.SetParent(canvas.transform, false);
            garage.AddComponent<GarageScreen>().Init(this);
        }

        /// <summary>Garage BACK: return to wherever the garage was opened from.</summary>
        public void CloseGarage()
        {
            if (garageFromChampionship && Championship.HasActiveSeason) ShowChampionship();
            else ShowHome();
        }

        void ShowSettings()
        {
            ClearScreens();
            SettingsPanel.Build(canvas.transform, ShowHome);
        }

        void StartRace()
        {
            PlayerPrefs.Save();
            RaceRequest.Current = RaceRequest.QuickRace();
            PendingScreen = MenuScreen.Home;
            GameFlow.StartRace();
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
