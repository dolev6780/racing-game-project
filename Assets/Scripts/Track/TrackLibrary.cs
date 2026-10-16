using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Track
{
    /// <summary>Look of a track: colors, fog and scenery style.</summary>
    public class TrackTheme
    {
        public Color Ground, Road, WallA, WallB, Rock, Crystal;
        public Color Fog, Ambient, Sun;
        public Color SkyTop, SkyHorizon, SkyBottom;   // cartoon sky gradient
        public bool Forest;            // Kenney forest tiles (green planet) vs rocks & crystals
        [Range(0f, 1f)] public float CrystalChance = 0.25f;
        public int SceneryCount = 110;
    }

    public class TrackDef
    {
        public string Name;
        public string Subtitle;
        public Vector2[] Layout;       // closed loop control points (x, z); start line at the first point
        public TrackTheme Theme;
    }

    /// <summary>
    /// All tracks. Layouts were checked so the road never overlaps itself, corners are drivable
    /// (radius > 11m) and the starting grid is straight.
    /// </summary>
    public static class TrackLibrary
    {
        public static readonly List<TrackDef> Tracks = new List<TrackDef>
        {
            new TrackDef
            {
                Name = "GREEN VALLEY", Subtitle = "Forest circuit",
                Layout = new[]
                {
                    V(0, -20), V(0, 40), V(10, 80), V(40, 100), V(80, 95), V(100, 70), V(95, 40), V(70, 20),
                    V(60, -10), V(80, -40), V(110, -60), V(115, -100), V(90, -125), V(40, -125), V(8, -112), V(0, -80),
                },
                Theme = new TrackTheme
                {
                    Ground = C(0.38f, 0.78f, 0.3f), Road = C(0.42f, 0.43f, 0.5f),
                    WallA = C(0.98f, 0.98f, 1f), WallB = C(0.2f, 0.5f, 1f),
                    Rock = C(0.62f, 0.6f, 0.58f), Crystal = C(0.2f, 0.9f, 1f),
                    Fog = C(0.72f, 0.88f, 1f), Ambient = C(0.62f, 0.66f, 0.75f), Sun = C(1f, 0.96f, 0.88f),
                    SkyTop = C(0.2f, 0.5f, 1f), SkyHorizon = C(0.75f, 0.9f, 1f), SkyBottom = C(0.45f, 0.7f, 0.4f),
                    Forest = true,
                },
            },
            new TrackDef
            {
                Name = "DESERT CANYON", Subtitle = "Long straights, fast sweepers",
                Layout = new[]
                {
                    V(0, -20), V(0, 60), V(15, 95), V(50, 108), V(80, 90), V(75, 55), V(50, 35), V(50, 5),
                    V(75, -15), V(115, -10), V(140, 15), V(160, 50), V(185, 50), V(200, 15), V(195, -45),
                    V(165, -85), V(115, -105), V(55, -105), V(15, -95), V(0, -65),
                },
                Theme = new TrackTheme
                {
                    Ground = C(0.98f, 0.78f, 0.45f), Road = C(0.5f, 0.42f, 0.38f),
                    WallA = C(1f, 0.95f, 0.85f), WallB = C(1f, 0.45f, 0.2f),
                    Rock = C(0.88f, 0.48f, 0.28f), Crystal = C(1f, 0.75f, 0.3f),
                    Fog = C(1f, 0.88f, 0.7f), Ambient = C(0.75f, 0.66f, 0.58f), Sun = C(1f, 0.92f, 0.75f),
                    SkyTop = C(0.25f, 0.55f, 1f), SkyHorizon = C(1f, 0.88f, 0.7f), SkyBottom = C(0.9f, 0.7f, 0.45f),
                    CrystalChance = 0.08f, SceneryCount = 120,
                },
            },
            new TrackDef
            {
                Name = "FROST PEAK", Subtitle = "Icy hairpins",
                Layout = new[]
                {
                    V(0, -20), V(0, 50), V(-10, 90), V(15, 125), V(70, 130), V(115, 115), V(130, 75), V(108, 45),
                    V(125, 12), V(155, -10), V(160, -60), V(130, -100), V(80, -110), V(30, -115), V(5, -95), V(0, -65),
                },
                Theme = new TrackTheme
                {
                    Ground = C(0.95f, 0.97f, 1f), Road = C(0.42f, 0.47f, 0.58f),
                    WallA = C(0.3f, 0.6f, 1f), WallB = C(1f, 1f, 1f),
                    Rock = C(0.7f, 0.78f, 0.9f), Crystal = C(0.5f, 0.85f, 1f),
                    Fog = C(0.85f, 0.93f, 1f), Ambient = C(0.72f, 0.78f, 0.9f), Sun = C(0.95f, 0.97f, 1f),
                    SkyTop = C(0.35f, 0.6f, 1f), SkyHorizon = C(0.88f, 0.95f, 1f), SkyBottom = C(0.85f, 0.92f, 1f),
                    CrystalChance = 0.35f, SceneryCount = 110,
                },
            },
            new TrackDef
            {
                Name = "ALIEN WASTES", Subtitle = "Twisty purple planet",
                Layout = new[]
                {
                    V(0, -20), V(0, 40), V(20, 75), V(60, 72), V(80, 40), V(115, 40), V(135, 75), V(175, 80),
                    V(200, 45), V(185, 0), V(145, -20), V(145, -65), V(115, -100), V(65, -112), V(25, -110),
                    V(4, -88), V(0, -60),
                },
                Theme = new TrackTheme
                {
                    Ground = C(0.55f, 0.3f, 0.75f), Road = C(0.3f, 0.28f, 0.38f),
                    WallA = C(0.2f, 1f, 0.8f), WallB = C(1f, 0.3f, 0.8f),
                    Rock = C(0.4f, 0.2f, 0.55f), Crystal = C(0.3f, 1f, 0.85f),
                    Fog = C(0.55f, 0.35f, 0.75f), Ambient = C(0.6f, 0.5f, 0.75f), Sun = C(1f, 0.85f, 1f),
                    SkyTop = C(0.15f, 0.08f, 0.4f), SkyHorizon = C(0.95f, 0.45f, 0.75f), SkyBottom = C(0.4f, 0.2f, 0.55f),
                    CrystalChance = 0.45f, SceneryCount = 120,
                },
            },
        };

        public static TrackDef Get(int index) => Tracks[Mathf.Clamp(index, 0, Tracks.Count - 1)];

        static Vector2 V(float x, float z) => new Vector2(x, z);
        static Color C(float r, float g, float b) => new Color(r, g, b);
    }
}
