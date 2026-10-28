using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Cloudborne
{
    /// <summary>
    /// Stage 1-1 of the Cloudborne vertical prototype: sets up camera, light, post-processing and the jungle,
    /// runs Raja and the hyenas, the fight zones, hit-stop and slow motion, and the title/pause/result flow.
    /// Started by <see cref="Boot"/> from the bootstrap when the "Cloudborne" scene loads.
    /// </summary>
    public class CbGame : MonoBehaviour
    {
        public enum Mode { Title, Play, Paused, Over }

        public class Zone
        {
            public float x;
            public string label;
            public (CbEnemy.Kind kind, float offset)[] spawn;
            public int tip;
        }

        static readonly Zone[] Zones =
        {
            new Zone { x = 16, label = "Iron Crown raiders!", tip = 1, spawn = new[] { (CbEnemy.Kind.Hyena, 12f), (CbEnemy.Kind.Hyena, -12f), (CbEnemy.Kind.Hyena, 14f) } },
            new Zone { x = 46, label = "Shield bearers", tip = 3, spawn = new[] { (CbEnemy.Kind.Shield, 12f), (CbEnemy.Kind.Hyena, -12f), (CbEnemy.Kind.Hyena, 13.5f) } },
            new Zone { x = 80, label = "Ambush at the shrine", tip = 4, spawn = new[] { (CbEnemy.Kind.Shield, 12f), (CbEnemy.Kind.Shield, -12f), (CbEnemy.Kind.Hyena, 13.5f), (CbEnemy.Kind.Hyena, -13.5f) } },
        };

        static readonly string[] Tips =
        {
            "Tap <color=#ffb03a>HIT</color> four times for a paw combo",
            "<color=#ffb03a>HIT, HIT,</color> then hold <color=#ffb03a>HIT</color> to whirl the staff",
            "Tap <color=#ffb03a>KI</color> for a fireball. Hold <color=#ffb03a>KI</color> to charge",
            "Shields stop paws. Use the staff: <color=#ffb03a>↑ + HIT</color> or <color=#ffb03a>DASH → HIT</color>",
            "<color=#ffb03a>DASH</color> just before a blow lands: slow motion, double damage. <color=#ffb03a>ROAR</color> when ki is full",
        };

        public static CbGame I { get; private set; }

        public Mode mode = Mode.Title;
        public CbHero hero;
        public CbHud hud;
        public CbWorld world;
        public readonly List<CbEnemy> enemies = new List<CbEnemy>();
        public Zone lockZone;
        public float clock, time, hitstop, shake, slow;
        public int score, combo, maxCombo;
        public readonly CbInput input = new CbInput();

        Camera cam;
        Light sun;
        Transform actors;
        float comboT, camX, camY = 3.2f, fovKick, overT, atkHeldT, kiKeyT = -1;
        int zoneIdx, lastScore = -1;
        readonly List<(float at, CbEnemy.Kind kind, float x, Zone zone)> pending = new List<(float, CbEnemy.Kind, float, Zone)>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<(Transform t, Vector3 v, float spin, float life)> debris = new List<(Transform, Vector3, float, float)>();
        Roar roar;
        const float BaseFov = 32f;

        class Shot { public Transform t, glow; public float x, y, vx, life; }
        class Roar { public Transform holder; public Material outer, inner; public float t; public int dir; }

        public static CbGame Boot()
        {
            if (I != null) return I;
            var go = new GameObject("Cloudborne");
            return go.AddComponent<CbGame>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.shadowDistance = 45f;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.supportsHDR = true;     // glow above 1.0 for bloom
                urp.shadowDistance = 45f;   // the camera sits 11-22 m from the action
            }
            SetupCamera();
            SetupLight();
            CbFx.Init(transform);
            world = new CbWorld(transform);
            actors = new GameObject("Actors").transform;
            actors.SetParent(transform, false);
            hero = new CbHero(this, actors);
            hud = new CbHud(transform);
            ResetGame();
            mode = Mode.Title;
            hud.SetScreen(CbHud.Screen.Title);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        // ---------- setup ----------

        void SetupCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.fieldOfView = BaseFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 900f;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.depth = 10;
            if (FindAnyObjectByType<AudioListener>() == null) go.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;

            var profile = Resources.Load<VolumeProfile>("Cloudborne/PostFX");
            if (profile == null) profile = CreatePostProfile();
            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.transform.SetParent(transform, false);
            vol.isGlobal = true;
            vol.priority = 10;
            vol.sharedProfile = profile;
        }

        /// <summary>Bloom for fire and glow, ACES tone mapping, a soft vignette and a little extra saturation.</summary>
        public static VolumeProfile CreatePostProfile()
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.62f);
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.5f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.25f);
            ca.saturation.Override(12f);
            ca.contrast.Override(6f);
            return p;
        }

        void SetupLight()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.75f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            go.transform.rotation = Quaternion.LookRotation(new Vector3(9, -15, 11));

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.9f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.66f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.33f, 0.22f);
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(new Color(0.62f, 0.74f, 0.56f));
            RenderSettings.ambientProbe = sh;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = CbWorld.Horizon;
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 150f;
            var sky = RacingGame.Core.ToonStyle.Sky(CbArt.Hex("#3f8a96"), CbWorld.Horizon, CbWorld.Horizon);
            if (sky != null) RenderSettings.skybox = sky;
            else { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = CbWorld.Horizon; }
        }

        // ---------- flow ----------

        void ResetGame()
        {
            foreach (var e in enemies) e.f.Destroy();
            enemies.Clear();
            pending.Clear();
            foreach (var s in shots) Destroy(s.t.gameObject);
            shots.Clear();
            foreach (var d in debris) if (d.t != null) Destroy(d.t.gameObject);
            debris.Clear();
            if (roar != null) { Destroy(roar.holder.gameObject); roar = null; }
            hero.Reset();
            time = score = 0; combo = maxCombo = 0; comboT = 0; hitstop = shake = slow = 0; lockZone = null; zoneIdx = 0; fovKick = 0;
            hud.SetWave("Chase the raiders");
            hud.SetTip(Tips[0]);
            hud.ShowGo(false);
            hud.SetCombo(0);
            hud.SetSlow(false);
            hud.SetScore(0);
            lastScore = 0;
        }

        /// <summary>Starts a run (also used by the play mode tests).</summary>
        public void StartGame()
        {
            ResetGame();
            mode = Mode.Play;
            hud.SetScreen(CbHud.Screen.None);
            hud.Banner("FIGHT!");
        }

        void TogglePause()
        {
            if (mode == Mode.Play) { mode = Mode.Paused; hud.SetScreen(CbHud.Screen.Paused); AudioListener.pause = true; }
            else if (mode == Mode.Paused) { mode = Mode.Play; hud.SetScreen(CbHud.Screen.None); AudioListener.pause = false; }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && mode == Mode.Play) TogglePause();
        }

        public void Defeated() => EndGame(false);

        void EndGame(bool won)
        {
            if (mode != Mode.Play) return;
            mode = Mode.Over;
            overT = 0;
            hud.ShowResult(won, score, maxCombo, time);
        }

        void UpdateFlow(float dt)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].at > time) continue;
                if (pending[i].zone == lockZone) enemies.Add(new CbEnemy(this, pending[i].kind, pending[i].x, actors));
                pending.RemoveAt(i);
            }
            if (lockZone == null)
            {
                if (zoneIdx < Zones.Length && hero.x > Zones[zoneIdx].x - 3)
                {
                    var z = lockZone = Zones[zoneIdx];
                    hud.ShowGo(false);
                    hud.SetWave(z.label);
                    hud.Banner(z.label.ToUpperInvariant());
                    hud.SetTip(Tips[z.tip]);
                    for (int k = 0; k < z.spawn.Length; k++) pending.Add((time + 0.3f + k * 0.65f, z.spawn[k].kind, z.x + z.spawn[k].offset, z));
                }
            }
            else if (pending.Count == 0 && !enemies.Exists(e => !e.dead))
            {
                lockZone = null;
                zoneIdx++;
                if (zoneIdx >= Zones.Length)
                {
                    hud.Banner("STAGE CLEAR");
                    Invoke(nameof(Won), 1.2f);
                }
                else
                {
                    hud.ShowGo(true);
                    hud.SetWave("Keep moving");
                    hud.Banner("CLEAR!");
                    hud.SetTip(Tips[zoneIdx == 1 ? 2 : 4]);
                }
            }
            if (comboT > 0)
            {
                comboT -= dt;
                if (comboT <= 0) { combo = 0; hud.SetCombo(0); }
            }
        }

        void Won() => EndGame(true);

        // ---------- combat ----------

        public bool HurtEnemy(CbEnemy e, float dmg, int dir, float kb, float up, bool big, bool breaker, bool fire, bool fromPlayer)
        {
            if (e.dead) return false;
            bool frontal = dir == -e.facing;
            var at = new Vector3(e.x - dir * 0.3f, e.HitHeight, -0.5f);
            // a raised shield stops everything except staff blows and the roar
            if (e.guard && frontal && e.state != CbEnemy.State.Hurt && !breaker)
            {
                CbFx.Spark(at, CbFx.Grey, 8, 5, 0.3f, 0.25f, 4);
                hud.Popup(new Vector3(e.x, e.HitHeight + 1, 0), "BLOCK", CbHud.PopStyle.Block);
                CbFx.Sfx("block");
                hero.vx = -dir * 3;
                hitstop = Mathf.Max(hitstop, 0.04f);
                return false;
            }
            if (e.guard && breaker)
            {
                e.BreakGuard(dir);
                hud.Popup(new Vector3(e.x, e.y + 2.6f, 0), "GUARD BREAK", CbHud.PopStyle.Big);
                CbFx.Sfx("heavy");
            }
            if (hero.empower && fromPlayer) { dmg *= 2; hero.empower = false; big = true; }
            e.hp -= dmg;
            e.flash = 0.12f;
            if (fire) { e.burnT = 3; e.burnTick = 0.4f; }
            combo++;
            comboT = 1.8f;
            maxCombo = Mathf.Max(maxCombo, combo);
            score += Mathf.RoundToInt(dmg * 10 * (1 + combo * 0.1f));
            hero.ki = Mathf.Min(100, hero.ki + 3);
            CbFx.Spark(at, fire ? CbFx.Fire : CbFx.Hit, big ? 22 : 12, big ? 11 : 7, big ? 0.6f : 0.4f);
            hud.Popup(new Vector3(e.x, e.HitHeight + 0.9f, 0), Mathf.RoundToInt(dmg).ToString(), big ? CbHud.PopStyle.Big : CbHud.PopStyle.Normal);
            hud.SetCombo(combo);
            if (e.hp <= 0) { KillEnemy(e, dir); return true; }
            e.Stagger(dir, kb, up);
            hitstop = Mathf.Max(hitstop, big ? 0.1f : 0.055f);
            shake = Mathf.Max(shake, big ? 0.3f : 0.12f);
            return true;
        }

        public void KillEnemy(CbEnemy e, int dir)
        {
            if (e.dead) return;
            e.Kill(dir);
            score += e.scoreValue;
            hitstop = 0.12f;
            shake = Mathf.Max(shake, 0.4f);
            CbFx.Spark(new Vector3(e.x, e.HitHeight, -0.4f), e.burnT > 0 ? CbFx.Fire : CbFx.Purple, 26, 9, 0.6f, 0.6f, 2);
        }

        public void PerfectDodge()
        {
            if (slow > 0) return;
            slow = 1f;
            hero.empower = true;
            hero.inv = Mathf.Max(hero.inv, 0.6f);
            hud.Banner("PERFECT!", true);
            hud.SetSlow(true);
            CbFx.Sfx("perfect");
        }

        public void FireShot(Vector3 pos, int dir)
        {
            var t = new GameObject("fireball").transform;
            t.SetParent(transform, false);
            t.position = pos;
            CbArt.Part(CbMesh.Sphere, CbArt.Emissive(new Color(1f, 0.95f, 0.77f) * 5f), t, Vector3.zero, Vector3.one * 0.48f, shadows: false);
            var glow = CbArt.Part(CbMesh.Quad, CbArt.Glow(new Color(1f, 0.48f, 0.1f, 1f) * 2.8f, CbArt.Soft), t, new Vector3(0, 0, -0.3f), Vector3.one * 1.6f, shadows: false);
            shots.Add(new Shot { t = t, glow = glow, x = pos.x, y = pos.y, vx = dir * 18, life = 1.6f });
            CbFx.Sfx("fire");
        }

        public void RoarWave(Vector3 p, int dir)
        {
            var holder = new GameObject("roar").transform;
            holder.SetParent(transform, false);
            holder.position = new Vector3(p.x + dir * 0.6f, p.y + 1.55f, -0.2f);
            holder.localRotation = Quaternion.Euler(0, 0, 90f * dir);   // the cone's -Y axis points forward
            var outer = CbArt.Glow(new Color(1f, 0.29f, 0.06f, 0.4f), CbArt.Shaft);
            var inner = CbArt.Glow(new Color(1f, 0.67f, 0.25f, 0.45f) * 1.3f, CbArt.Shaft);
            CbArt.Part(CbMesh.Cone(28), outer, holder, new Vector3(0, -9.5f, 0), new Vector3(2.6f, 9.5f, 2.6f), shadows: false);
            CbArt.Part(CbMesh.Cone(28), inner, holder, new Vector3(0, -9.5f, 0), new Vector3(1.3f, 9.5f, 1.3f), shadows: false);
            holder.localScale = Vector3.one * 0.01f;
            roar = new Roar { holder = holder, outer = outer, inner = inner, t = 0, dir = dir };
            for (int i = 0; i < 70; i++)
                CbFx.Spark(new Vector3(p.x + dir * Random.Range(0.5f, 3f), p.y + 1.5f + Random.Range(-0.6f, 0.6f), Random.Range(-0.6f, 0.6f)),
                    Random.value < 0.5f ? CbFx.Fire : CbFx.Flame, 1, 6, 0.7f, 0.7f, -2, dir * Random.Range(6f, 14f));
            foreach (var e in enemies.ToArray())
            {
                if (e.dead) continue;
                float dx = (e.x - p.x) * dir;
                if (dx > -0.5f && dx < 10 + e.w && Mathf.Abs(e.HitHeight - (p.y + 1.4f)) < 3) HurtEnemy(e, 26, dir, 10, 6, true, true, true, true);
            }
            shake = Mathf.Max(shake, 0.55f);
            hitstop = 0.1f;
            fovKick = -5;
            CbFx.Sfx("roar");
        }

        public void AddDebris(Transform t, Vector3 velocity, float spinDeg) => debris.Add((t, velocity, spinDeg, 2.2f));

        void UpdateShots(float dt)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                s.life -= dt;
                s.x += s.vx * dt;
                s.t.position = new Vector3(s.x, s.y, 0);
                s.glow.localScale = Vector3.one * (1.4f + Mathf.Sin(clock * 40) * 0.25f);
                if (Random.value < 0.9f) CbFx.Spark(new Vector3(s.x, s.y + Random.Range(-0.1f, 0.1f), -0.2f), Random.value < 0.5f ? CbFx.Fire : CbFx.Flame, 1, 0.8f, 0.5f, 0.3f, -2);
                bool hit = false;
                foreach (var e in enemies.ToArray())
                {
                    if (e.dead) continue;
                    if (Mathf.Abs(e.x - s.x) < 0.5f + e.w * 0.6f && Mathf.Abs(e.HitHeight - s.y) < 1.25f)
                    {
                        if (HurtEnemy(e, 12, s.vx > 0 ? 1 : -1, 4, 2, false, false, true, true)) CbFx.Sfx("hit");
                        hit = true;
                        break;
                    }
                }
                if (hit || s.life <= 0)
                {
                    CbFx.Spark(new Vector3(s.x, s.y, -0.2f), CbFx.Fire, 14, 6, 0.5f, 0.35f, 0);
                    Destroy(s.t.gameObject);
                    shots.RemoveAt(i);
                }
            }
            if (roar != null)
            {
                roar.t += dt;
                float u = roar.t;
                float grow = Mathf.Min(1, u / 0.22f), fade = u < 0.35f ? 1 : Mathf.Max(0, 1 - (u - 0.35f) / 0.4f);
                float side = 0.3f + grow * 0.7f;
                roar.holder.localScale = new Vector3(side + Mathf.Sin(u * 50) * 0.04f, 0.2f + grow * 0.8f, side);
                var oc = roar.outer.GetColor("_Color"); oc.a = 0.4f * fade; roar.outer.SetColor("_Color", oc);
                var ic = roar.inner.GetColor("_Color"); ic.a = 0.45f * fade; roar.inner.SetColor("_Color", ic);
                if (u > 0.8f) { Destroy(roar.holder.gameObject); Destroy(roar.outer); Destroy(roar.inner); roar = null; }
            }
            for (int i = debris.Count - 1; i >= 0; i--)
            {
                var d = debris[i];
                d.life -= dt;
                d.v.y -= 20 * dt;
                var p = d.t.position + d.v * dt;
                if (p.y < 0.1f) { p.y = 0.1f; d.v.y = 0; d.v.x *= 0.8f; d.spin *= 0.7f; }
                d.t.position = p;
                d.t.Rotate(0, 0, d.spin * dt, Space.World);
                if (d.life < 0.4f) d.t.localScale = Vector3.one * Mathf.Max(0.01f, d.life / 0.4f);
                if (d.life <= 0) { Destroy(d.t.gameObject); debris.RemoveAt(i); }
                else debris[i] = d;
            }
        }

        // ---------- loop ----------

        void Update()
        {
            float raw = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            clock += raw;
            ReadInput(raw);

            if (input.pause) TogglePause();
            if (mode == Mode.Title)
            {
                hero.Idle(raw);
                if (input.confirm) StartGame();
            }
            else if (mode == Mode.Paused)
            {
                if (input.confirm) TogglePause();
            }
            else
            {
                float dt = raw;
                if (hitstop > 0) { hitstop -= raw; dt = raw * 0.05f; }
                float edt = dt;
                if (slow > 0) { slow -= raw; edt = dt * 0.3f; if (slow <= 0) hud.SetSlow(false); }
                if (mode == Mode.Play) time += raw;
                hero.Update(dt, input);
                for (int i = enemies.Count - 1; i >= 0; i--)
                    if (enemies[i].Update(edt)) { enemies[i].f.Destroy(); enemies.RemoveAt(i); }
                UpdateShots(dt);
                if (mode == Mode.Play) UpdateFlow(dt);
                hud.SetBars(hero.hp, hero.ki);
                if (score != lastScore) { lastScore = score; hud.SetScore(score); }
                if (mode == Mode.Over)
                {
                    overT += raw;
                    if (overT > 0.8f && input.confirm) StartGame();
                }
            }
            world.Update(clock, camX);
            UpdateCamera(raw);
            hud.Tick(raw);
        }

        void UpdateCamera(float dt)
        {
            fovKick += (0 - fovKick) * (1 - Mathf.Exp(-3 * dt));
            cam.fieldOfView = BaseFov + fovKick;
            float tanH = Mathf.Tan(BaseFov * 0.5f * Mathf.Deg2Rad);
            float dist = Mathf.Clamp(9.8f / (tanH * cam.aspect), 11f, 22f);
            float tx = hero.x + hero.facing * 1.6f;
            if (mode == Mode.Title) tx = 2 + Mathf.Sin(clock * 0.15f) * 3;
            if (lockZone != null) tx = Mathf.Clamp(tx, lockZone.x - 3, lockZone.x + 3);
            camX += (tx - camX) * (1 - Mathf.Exp(-3.5f * dt));
            float ty = 3f + hero.y * 0.3f;
            camY += (ty - camY) * (1 - Mathf.Exp(-3 * dt));
            float sh = shake;
            cam.transform.position = new Vector3(camX + Random.Range(-1f, 1f) * sh * 0.4f, camY + 1 + Random.Range(-1f, 1f) * sh * 0.4f, -dist);
            cam.transform.LookAt(new Vector3(camX, camY - 0.8f, 0));
            shake = Mathf.Max(0, shake - dt * 1.6f);
        }

        // ---------- input ----------

        void ReadInput(float dt)
        {
            input.ClearEdges();
            input.x = input.y = 0;
            input.atkDown = input.charging = false;
            hud.ReadTouch(input, dt);
            ReadKeys(input, dt);
            TestInput?.Invoke(input);
            // how long HIT has been held since its last press (HIT, HIT, hold = staff whirl)
            if (input.atk) atkHeldT = 0;
            else if (input.atkDown) atkHeldT += dt;
            else atkHeldT = 0;
            input.atkHeldT = atkHeldT;
            input.x = Mathf.Clamp(input.x, -1, 1);
            input.y = Mathf.Clamp(input.y, -1, 1);
        }

        /// <summary>Test hook: edits the input every frame after touch and keyboard are read.</summary>
        public System.Action<CbInput> TestInput;

        void ReadKeys(CbInput inp, float dt)
        {
            bool kiHeld = false, kiPressed = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) inp.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) inp.x += 1;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) inp.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) inp.y -= 1;
                if (kb.spaceKey.wasPressedThisFrame) inp.jump = true;
                if (kb.jKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame) inp.atk = true;
                if (kb.jKey.isPressed || kb.zKey.isPressed) inp.atkDown = true;
                if (kb.lKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame) inp.dash = true;
                if (kb.iKey.wasPressedThisFrame || kb.uKey.wasPressedThisFrame) inp.skill = true;
                kiHeld = kb.kKey.isPressed || kb.xKey.isPressed;
                kiPressed = kb.kKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame;
                if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) inp.pause = true;
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame) inp.confirm = true;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                var s = pad.leftStick.ReadValue();
                if (Mathf.Abs(s.x) > 0.2f) inp.x += s.x;
                if (Mathf.Abs(s.y) > 0.4f) inp.y += s.y;
                if (pad.dpad.left.isPressed) inp.x -= 1;
                if (pad.dpad.right.isPressed) inp.x += 1;
                if (pad.buttonSouth.wasPressedThisFrame) { inp.jump = true; inp.confirm = true; }
                if (pad.buttonWest.wasPressedThisFrame) inp.atk = true;
                if (pad.buttonWest.isPressed) inp.atkDown = true;
                if (pad.buttonEast.wasPressedThisFrame) inp.dash = true;
                if (pad.rightShoulder.wasPressedThisFrame) inp.skill = true;
                kiHeld |= pad.buttonNorth.isPressed;
                kiPressed |= pad.buttonNorth.wasPressedThisFrame;
                if (pad.startButton.wasPressedThisFrame) inp.pause = true;
            }
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inp.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inp.x += 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) inp.y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) inp.y -= 1;
            if (Input.GetKeyDown(KeyCode.Space)) inp.jump = true;
            if (Input.GetKeyDown(KeyCode.J)) inp.atk = true;
            if (Input.GetKey(KeyCode.J)) inp.atkDown = true;
            if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.LeftShift)) inp.dash = true;
            if (Input.GetKeyDown(KeyCode.I)) inp.skill = true;
            kiHeld = Input.GetKey(KeyCode.K);
            kiPressed = Input.GetKeyDown(KeyCode.K);
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) inp.pause = true;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J)) inp.confirm = true;
#endif
            // KI: a short press is a fireball, holding it charges
            if (kiPressed) kiKeyT = 0;
            if (kiKeyT >= 0)
            {
                if (kiHeld) { kiKeyT += dt; if (kiKeyT >= 0.22f) inp.charging = true; }
                else { if (kiKeyT < 0.22f) inp.kiTap = true; kiKeyT = -1; }
            }
        }

        /// <summary>Test hook: puts Raja at a position (e.g. next to a fight zone).</summary>
        public void Teleport(float x) => hero.x = x;
    }
}
