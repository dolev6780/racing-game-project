using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Stage 1-1's time of day and its rain (the user's ideas list: the day turning as the stage goes on, a stretch of
    /// rain). Over the stage the sun warms from morning's pale gold to a deep afternoon gold and sinks a little lower;
    /// over the canopy and the waterfall (RainFrom to RainTo) clouds close in: the light dims and cools, rain streaks
    /// fall round the camera, and now and then lightning whitens everything for a moment. Only in 1-1; the other stages
    /// keep their own light (CbGame.StageLight).
    /// </summary>
    public class CbWeather
    {
        // (run 301's picture: thin pale streaks were lost against the leaves - wider, longer, brighter now)
        // where the rain falls (the canopy and the waterfall), how far it fades in and out on either side
        public const float RainFrom = 362f, RainTo = 410f, RainEdge = 10f;
        const int Drops = 170;
        // the drops: a box round the camera's aim they fall through, how fast, how long each streak is
        const float BoxW = 26f, BoxH = 16f, BoxNear = -3f, BoxFar = 8f, FallSpeed = 22f, Streak = 0.85f, Slant = 0.18f;
        // the afternoon at the stage's end: the sun's colour and strength, its fall from the morning's direction
        static readonly Color Morning = new Color(1f, 0.93f, 0.78f), Afternoon = new Color(1f, 0.76f, 0.5f), Storm = new Color(0.72f, 0.8f, 0.9f);
        const float StormDim = 0.55f;

        readonly Light sun;
        readonly Transform root;
        readonly Transform[] drops = new Transform[Drops];
        readonly Vector3[] dropAt = new Vector3[Drops];
        readonly Color baseSky, baseEquator, baseGround;
        readonly float basePower;
        readonly Quaternion baseDir;
        float flash, nextFlash = 3f;

        /// <summary>How much it rains where the camera is (0 none, 1 full).</summary>
        public float Rain { get; private set; }
        /// <summary>How far through the day it is (0 morning at the start, 1 afternoon at the end).</summary>
        public float Day { get; private set; }

        public CbWeather(Transform parent, Light sun)
        {
            this.sun = sun;
            root = new GameObject("Weather").transform;
            root.SetParent(parent, false);
            basePower = sun != null ? sun.intensity : 1.8f;
            baseDir = sun != null ? sun.transform.rotation : Quaternion.identity;
            baseSky = RenderSettings.ambientSkyColor;
            baseEquator = RenderSettings.ambientEquatorColor;
            baseGround = RenderSettings.ambientGroundColor;
            var mat = CbArt.Glow(new Color(0.85f, 0.92f, 1f, 0.8f), CbArt.Soft);
            for (int i = 0; i < Drops; i++)
            {
                drops[i] = CbArt.Part(CbMesh.Quad, mat, root, Vector3.zero, new Vector3(0.04f, Streak, 1), Quaternion.Euler(0, 0, Slant * Mathf.Rad2Deg), shadows: false);
                drops[i].name = "rain";
                dropAt[i] = new Vector3(Random.Range(-BoxW / 2, BoxW / 2), Random.Range(0, BoxH), Random.Range(BoxNear, BoxFar));
                drops[i].gameObject.SetActive(false);
            }
        }

        public void Destroy() => Object.Destroy(root.gameObject);

        /// <summary>Each frame: the light for where the camera looks, the rain round it.</summary>
        public void Update(float dt, Vector3 camAim)
        {
            float x = camAim.x;
            Day = Mathf.Clamp01(x / Mathf.Max(1f, CbWorld.LevelMax));
            Rain = Mathf.Clamp01(Mathf.Min((x - (RainFrom - RainEdge)) / RainEdge, ((RainTo + RainEdge) - x) / RainEdge));
            // lightning now and then while it rains
            flash = Mathf.Max(0, flash - dt * 3.5f);
            if (Rain > 0.6f && (nextFlash -= dt) <= 0)
            {
                nextFlash = Random.Range(4f, 9f);
                flash = 1f;
                CbFx.Sfx("heavy", 0.35f);
            }
            if (sun != null)
            {
                var day = Color.Lerp(Morning, Afternoon, Day * Day);
                sun.color = Color.Lerp(day, Storm, Rain) + Color.white * flash * 0.6f;
                sun.intensity = basePower * Mathf.Lerp(1f, StormDim, Rain) * (1f - 0.12f * Day) + flash * 1.6f;
                sun.transform.rotation = Quaternion.AngleAxis(Day * 10f, Vector3.right) * baseDir;
            }
            float dim = Mathf.Lerp(1f, 0.72f, Rain) + flash * 0.5f;
            RenderSettings.ambientSkyColor = baseSky * dim;
            RenderSettings.ambientEquatorColor = baseEquator * dim;
            RenderSettings.ambientGroundColor = baseGround * dim;
            // the drops fall through a box round the camera's aim, wrapping back to its top
            bool raining = Rain > 0.01f;
            int shown = Mathf.RoundToInt(Drops * Rain);
            for (int i = 0; i < Drops; i++)
            {
                bool on = raining && i < shown;
                if (drops[i].gameObject.activeSelf != on) drops[i].gameObject.SetActive(on);
                if (!on) continue;
                var p = dropAt[i];
                p.y -= FallSpeed * dt;
                p.x -= FallSpeed * Slant * dt;
                if (p.y < 0) { p.y += BoxH; p.x = Random.Range(-BoxW / 2, BoxW / 2); }
                if (p.x < -BoxW / 2) p.x += BoxW;
                dropAt[i] = p;
                drops[i].localPosition = new Vector3(camAim.x + p.x, camAim.y - BoxH * 0.45f + p.y, p.z);
            }
        }
    }
}
