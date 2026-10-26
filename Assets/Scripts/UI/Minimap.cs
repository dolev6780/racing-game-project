using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RacingGame.Race;

namespace RacingGame.UI
{
    /// <summary>
    /// Corner track map with a coloured dot for every racer (the player's dot is larger).
    /// Drawn at the isometric camera's angle (rotated 45 degrees and squashed) so it matches the view.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        const float MapSize = 290f;
        const int TexSize = 256;

        RaceManager race;
        readonly List<(Racer racer, RectTransform dot)> dots = new List<(Racer, RectTransform)>();
        Vector2 mapMin;
        float mapSpan;
        Vector3 axisRight, axisUp;   // world directions that point right / up on the map
        float squash;

        public RectTransform Panel { get; private set; }

        public void Init(RaceManager owner, Transform parent)
        {
            race = owner;
            var panel = UIKit.Panel(parent, "Minimap", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f),
                new Vector2(MapSize + 30f, MapSize + 30f)).rectTransform;
            Panel = panel;
            transform.SetParent(panel, false);

            var view = Quaternion.Euler(0f, RacingGame.CameraRig.RaceCamera.IsoYaw, 0f);
            axisRight = view * Vector3.right;
            axisUp = view * Vector3.forward;
            squash = Mathf.Sin(RacingGame.CameraRig.RaceCamera.IsoPitch * Mathf.Deg2Rad);

            // Bounds of the projected track (square, so the map keeps its proportions).
            var path = race.Path;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < path.Count; i++)
            {
                var p = Project(path.Point(i));
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            mapSpan = Mathf.Max(max.x - min.x, max.y - min.y) + 30f;
            mapMin = (min + max) * 0.5f - Vector2.one * mapSpan * 0.5f;

            var mapRt = UIKit.Rect(panel, "Track", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(MapSize, MapSize));
            var raw = mapRt.gameObject.AddComponent<RawImage>();
            raw.texture = DrawTrack();
            raw.raycastTarget = false;

            // Dots: opponents first, player last so it draws on top.
            foreach (var r in race.Racers) if (!r.IsPlayer) AddDot(mapRt, r, 16f);
            AddDot(mapRt, race.Player, 26f);
        }

        void AddDot(RectTransform map, Racer r, float size)
        {
            var dot = UIKit.Rect(map, "Dot_" + r.Name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size, size));
            UIKit.Image(dot, UIKit.Circle, new Color(0.05f, 0.05f, 0.08f, 0.9f));   // dark border
            var inner = UIKit.Stretch(dot, "Fill");
            inner.offsetMin = new Vector2(3f, 3f);
            inner.offsetMax = new Vector2(-3f, -3f);
            UIKit.Image(inner, UIKit.Circle, r.IsPlayer ? UIKit.Gold : r.Color);
            dots.Add((r, dot));
        }

        /// <summary>World position to the map plane (the camera's view of the ground).</summary>
        Vector2 Project(Vector3 world) => new Vector2(Vector3.Dot(world, axisRight), Vector3.Dot(world, axisUp) * squash);

        Vector2 ToMap(Vector3 world)
        {
            Vector2 p = (Project(world) - mapMin) / mapSpan - new Vector2(0.5f, 0.5f);
            return p * MapSize;
        }

        Texture2D DrawTrack()
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[TexSize * TexSize];
            var path = race.Path;
            var road = new Color32(255, 255, 255, 230);
            var start = new Color32(255, 200, 30, 255);

            for (int i = 0; i < path.Count; i++)
            {
                Vector2 a = ToTex(path.Point(i)), b = ToTex(path.Point(i + 1));
                int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f) + 1;
                for (int s = 0; s <= steps; s++)
                    Stamp(px, Vector2.Lerp(a, b, s / (float)steps), 3.2f, i < 2 ? start : road);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        Vector2 ToTex(Vector3 world) => (Project(world) - mapMin) / mapSpan * TexSize;

        static void Stamp(Color32[] px, Vector2 c, float radius, Color32 color)
        {
            int r = Mathf.CeilToInt(radius);
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                int ix = Mathf.RoundToInt(c.x) + x, iy = Mathf.RoundToInt(c.y) + y;
                if (ix < 0 || iy < 0 || ix >= TexSize || iy >= TexSize) continue;
                if (x * x + y * y > radius * radius) continue;
                px[iy * TexSize + ix] = color;
            }
        }

        void Update()
        {
            foreach (var (racer, dot) in dots)
            {
                if (racer.Car == null) continue;
                dot.anchoredPosition = ToMap(racer.Car.transform.position);
                dot.gameObject.SetActive(!racer.Health.IsWrecked || Mathf.Repeat(Time.time * 4f, 1f) > 0.5f);
            }
        }
    }
}
