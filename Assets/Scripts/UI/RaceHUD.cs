using UnityEngine;
using RacingGame.Car;
using RacingGame.CameraRig;
using RacingGame.Race;

namespace RacingGame.UI
{
    /// <summary>
    /// Placeholder HUD drawn with IMGUI (no prefabs/scenes needed).
    /// Will be replaced by a proper uGUI/UI Toolkit HUD with art later.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        const float RefHeight = 720f;

        RaceManager race;
        GUIStyle label, big, small, panel, button;
        float goTimer;

        void Awake() => race = GetComponent<RaceManager>();

        void EnsureStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 18 };
            big = new GUIStyle(label) { fontSize = 120, alignment = TextAnchor.MiddleCenter };
            panel = new GUIStyle(GUI.skin.box);
            button = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            if (race == null || race.Player == null) return;
            EnsureStyles();

            float scale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            float h = RefHeight;

            var player = race.Player;
            DrawTopLeft(player);
            DrawStandings(w);
            DrawTopButtons(w);
            DrawCenterMessages(player, w, h);
            if (race.State == RaceState.Finished) DrawResults(w, h);

            GUI.matrix = Matrix4x4.identity;
        }

        void DrawTopLeft(Racer player)
        {
            GUI.Box(new Rect(10, 10, 300, 200), GUIContent.none, panel);
            int lap = Mathf.Clamp(player.Progress.Lap, 1, race.totalLaps);
            Shadowed(new Rect(22, 14, 280, 34), $"LAP {lap}/{race.totalLaps}", label);
            Shadowed(new Rect(22, 46, 280, 34), $"POS {race.PositionOf(player)}/{race.Racers.Count}", label);
            Shadowed(new Rect(22, 78, 280, 30), $"{player.Car.SpeedKmh:0} km/h    {FormatTime(race.RaceTime)}", small);

            // Armor bar
            var bar = new Rect(22, 110, 270, 18);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            float a = player.Health.Armor01;
            GUI.color = Color.Lerp(Color.red, Color.green, a);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * a, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Shadowed(new Rect(22, 106, 270, 26), "ARMOR", small);

            var wpn = player.Weapons;
            Shadowed(new Rect(22, 134, 280, 26), $"MISSILES {Pips(wpn.Missiles, wpn.missilesPerLap)}", small);
            Shadowed(new Rect(22, 156, 280, 26), $"MINES    {Pips(wpn.Mines, wpn.minesPerLap)}", small);
            Shadowed(new Rect(22, 178, 280, 26), $"NITRO    {Pips(wpn.Boosts, wpn.boostsPerLap)}", small);
        }

        void DrawStandings(float w)
        {
            float x = w - 230, y = 60;
            GUI.Box(new Rect(x, y, 220, 26 * race.Standings.Count + 10), GUIContent.none, panel);
            for (int i = 0; i < race.Standings.Count; i++)
            {
                var r = race.Standings[i];
                GUI.color = r.IsPlayer ? new Color(0.4f, 1f, 0.4f) : Color.white;
                string suffix = r.Finished ? "  FIN" : r.Health.IsWrecked ? "  X" : "";
                Shadowed(new Rect(x + 10, y + 4 + i * 26, 210, 26), $"{i + 1}. {r.Name}{suffix}", small);
            }
            GUI.color = Color.white;
        }

        void DrawTopButtons(float w)
        {
            if (GUI.Button(new Rect(w - 120, 10, 110, 40), RaceCamera.Isometric ? "CAM: ISO" : "CAM: CHASE", button))
                RaceCamera.Toggle();
#if UNITY_EDITOR || UNITY_STANDALONE
            if (GUI.Button(new Rect(w - 240, 10, 110, 40), PlayerCarInput.ShowTouchControls ? "TOUCH: ON" : "TOUCH: OFF", button))
                PlayerCarInput.ShowTouchControls = !PlayerCarInput.ShowTouchControls;
#endif
        }

        void DrawCenterMessages(Racer player, float w, float h)
        {
            var center = new Rect(0, h * 0.25f, w, 160);
            if (race.State == RaceState.Countdown)
            {
                int n = Mathf.CeilToInt(race.Countdown);
                Shadowed(center, n.ToString(), big);
                goTimer = 1f;
            }
            else if (goTimer > 0f)
            {
                goTimer -= Time.deltaTime;
                GUI.color = new Color(1f, 0.85f, 0.1f);
                Shadowed(center, "GO!", big);
                GUI.color = Color.white;
            }

            if (race.State == RaceState.Racing)
            {
                var msg = new GUIStyle(label) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
                if (player.Health.IsWrecked) Shadowed(new Rect(0, h * 0.4f, w, 80), "WRECKED!", msg);
                else if (player.Progress.WrongWay) Shadowed(new Rect(0, h * 0.4f, w, 80), "WRONG WAY!", msg);
                else if (player.Progress.Lap == race.totalLaps && race.RaceTime > 1f)
                    Shadowed(new Rect(0, 12, w, 50), "FINAL LAP", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            }
        }

        void DrawResults(float w, float h)
        {
            float pw = 520, ph = 120 + race.Standings.Count * 30 + 110;
            var r = new Rect((w - pw) * 0.5f, (h - ph) * 0.5f, pw, ph);
            GUI.Box(r, GUIContent.none, panel);
            GUI.Box(r, GUIContent.none, panel);

            var title = new GUIStyle(label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            int place = race.Player.FinishPlace;
            Shadowed(new Rect(r.x, r.y + 10, pw, 50), place == 1 ? "YOU WIN!" : $"YOU FINISHED {Ordinal(place)}", title);

            float y = r.y + 70;
            for (int i = 0; i < race.Standings.Count; i++)
            {
                var s = race.Standings[i];
                GUI.color = s.IsPlayer ? new Color(0.4f, 1f, 0.4f) : Color.white;
                string time = s.Finished ? FormatTime(s.FinishTime) : "racing...";
                Shadowed(new Rect(r.x + 30, y, 300, 30), $"{i + 1}. {s.Name}", small);
                Shadowed(new Rect(r.x + 330, y, 160, 30), time, small);
                y += 30;
            }
            GUI.color = Color.white;

            y += 10;
            var p = race.Player;
            Shadowed(new Rect(r.x + 30, y, pw - 60, 30),
                $"Earned ${race.EarnedThisRace}   (kills: {p.Kills}, pickups: ${p.Car.MoneyCollected})", small);
            Shadowed(new Rect(r.x + 30, y + 26, pw - 60, 30), $"Bank: ${RaceManager.TotalMoney}", small);

            if (GUI.Button(new Rect(r.x + pw * 0.5f - 100, r.yMax - 55, 200, 45), "RACE AGAIN", button))
                race.Restart();
        }

        static void Shadowed(Rect r, string text, GUIStyle style)
        {
            var c = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f * c.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            GUI.color = c;
            GUI.Label(r, text, style);
        }

        static string Pips(int have, int max)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < max; i++) sb.Append(i < have ? "■ " : "□ ");
            return sb.ToString();
        }

        static string FormatTime(float t) => $"{(int)(t / 60f)}:{t % 60f:00.00}";

        static string Ordinal(int n) => n == 1 ? "1ST" : n == 2 ? "2ND" : n == 3 ? "3RD" : n + "TH";
    }
}
