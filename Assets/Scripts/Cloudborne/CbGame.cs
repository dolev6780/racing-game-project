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
            "Tap <color=#ffb03a>HIT</color> four times for a combo, <color=#ffb03a>HEAVY</color> for the staff",
            "Hold <color=#ffb03a>HEAVY</color> to whirl the staff. Hold <color=#ffb03a>GUARD</color> to block",
            "<color=#ffb03a>FIRE</color> shoots a fireball. Duck under a slash with <color=#ffb03a>↓</color>",
            "Shields stop paws. Use the staff: <color=#ffb03a>HEAVY</color>, <color=#ffb03a>↑ + HEAVY</color> or <color=#ffb03a>DASH → HEAVY</color>",
            "<color=#ffb03a>DASH</color> just before a blow lands: slow motion, double damage. <color=#ffb03a>SPECIAL</color> when ki is high",
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
        float comboT, camX, camY = CamBase, fovKick, overT, atkHeldT, heavyHeldT;
        int zoneIdx, lastScore = -1;
        readonly List<(float at, CbEnemy.Kind kind, float x, Zone zone)> pending = new List<(float, CbEnemy.Kind, float, Zone)>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<(Transform t, Vector3 v, float spin, float life)> debris = new List<(Transform, Vector3, float, float)>();
        Beam beam;
        // A side-on view like 2.5D platformers: a narrow field of view from further back flattens the perspective,
        // and the camera looks almost level, so the path reads as the top of a platform with its front face below
        // and the scenery stands up behind it, instead of a green floor tilting away.
        const float BaseFov = 26f, CamBase = 1.5f, CamRise = 0.6f;

        class Shot { public Transform t, glow; public float x, y, vx, life, crackle; }
        /// <summary>The special shot's beam: its layers, how long it has run, where it starts and which way it points.</summary>
        class Beam
        {
            public Transform holder, outer, mid, core, head;
            public Material outerM, midM, coreM, headM;
            public Vector3 from;
            public float t, tick, ringT;
            public int dir, hits;
        }
        // the flame cannon reaches BeamLength in BeamGrow, holds, then thins away; it hits BeamHits times, the last hard
        const float BeamLength = 16f, BeamGrow = 0.12f, BeamHold = 0.55f, BeamFade = 0.2f;
        const int BeamHits = 6;
        static Material fireCore, fireGlow, fireTail, fireTailInner, fireSprite;
        // the painted fireball (Resources/Cloudborne/Effects/Fireball): its length in metres, and where its ball sits
        const float FireballLength = 2.6f, FireballHead = 0.88f;

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
            CbVfx.Init(transform);
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

            var profile = Resources.Load<VolumeProfile>("Cloudborne/PostFX_Anime");
            if (profile == null) profile = CreatePostProfile();
            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.transform.SetParent(transform, false);
            vol.isGlobal = true;
            vol.priority = 10;
            vol.sharedProfile = profile;
        }

        /// <summary>
        /// The anime look: bloom kept for fire and glow only, neutral tone mapping (ACES washes colours out and shifts
        /// them), a little extra saturation and contrast (more turns the grass fluorescent), a soft vignette.
        /// </summary>
        public static VolumeProfile CreatePostProfile()
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.6f);
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.2f);
            vig.smoothness.Override(0.5f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0f);
            ca.saturation.Override(12f);
            ca.contrast.Override(12f);
            return p;
        }

        void SetupLight()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.72f);
            sun.intensity = 1.8f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            go.transform.rotation = Quaternion.LookRotation(new Vector3(9, -15, 11));

            RenderSettings.ambientMode = AmbientMode.Trilight;
            // cool, darker fill: the shade side reads as a clear second tone, as in cel animation
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.74f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.52f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.24f, 0.17f);
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(new Color(0.5f, 0.62f, 0.56f));
            RenderSettings.ambientProbe = sh;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = CbWorld.Horizon;
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 175f;
            var sky = RacingGame.Core.ToonStyle.Sky(CbArt.Hex("#2a78a8"), CbWorld.Horizon, CbWorld.Horizon);
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
            EndBeam();
            EndWheel();
            waves.Clear();
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
            // a raised shield stops everything except staff blows and the special shot
            if (e.guard && frontal && e.state != CbEnemy.State.Hurt && !breaker)
            {
                CbFx.Spark(at, CbFx.Grey, 8, 5, 0.3f, 0.25f, 4);
                CbVfx.Burst(at, CbFx.Grey, 1.1f, 0.1f);
                hud.Popup(new Vector3(e.x, e.HitHeight + 1, 0), "BLOCK", CbHud.PopStyle.Block);
                CbFx.Sfx("block");
                hero.vx = -dir * 3;
                hitstop = Mathf.Max(hitstop, 0.02f);
                return false;
            }
            if (e.guard && breaker)
            {
                e.BreakGuard(dir);
                CbVfx.Ring(at, CbFx.Hit * 0.4f, 1.4f, 0.3f);
                hud.ImpactFlash(0.4f);
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
            CbVfx.Burst(at, fire ? CbFx.Fire : CbFx.Hit, big ? 2.4f : 1.4f, big ? 0.2f : 0.12f);
            if (big)
            {
                CbVfx.Ring(at, (fire ? CbFx.Fire : CbFx.Hit) * 0.35f, 1.4f, 0.3f);
                hud.ImpactFlash(0.45f);
                hud.SpeedLines(0.55f, 0.3f);
            }
            hud.Popup(new Vector3(e.x, e.HitHeight + 0.9f, 0), Mathf.RoundToInt(dmg).ToString(), big ? CbHud.PopStyle.Big : CbHud.PopStyle.Normal);
            hud.SetCombo(combo);
            if (e.hp <= 0) { KillEnemy(e, dir); return true; }
            e.Stagger(dir, kb, up);
            // Hit-stop only on heavy blows, and short: a pause on every punch read as the game stuttering
            if (big) hitstop = Mathf.Max(hitstop, 0.05f);
            shake = Mathf.Max(shake, big ? 0.3f : 0.12f);
            return true;
        }

        public void KillEnemy(CbEnemy e, int dir)
        {
            if (e.dead) return;
            e.Kill(dir);
            score += e.scoreValue;
            hitstop = Mathf.Max(hitstop, 0.06f);
            shake = Mathf.Max(shake, 0.4f);
            CbFx.Spark(new Vector3(e.x, e.HitHeight, -0.4f), e.burnT > 0 ? CbFx.Fire : CbFx.Purple, 26, 9, 0.6f, 0.6f, 2);
            CbVfx.Burst(new Vector3(e.x, e.HitHeight, -0.5f), CbFx.Hit, 3f, 0.25f);
            CbVfx.Ring(new Vector3(e.x, e.HitHeight, -0.5f), CbFx.Purple * 0.35f, 1.8f, 0.4f);
            hud.ImpactFlash(0.35f);
        }

        public void PerfectDodge()
        {
            if (slow > 0) return;
            slow = 1f;
            hero.empower = true;
            hero.inv = Mathf.Max(hero.inv, 0.6f);
            hud.Banner("PERFECT!", true);
            hud.SetSlow(true);
            hud.SpeedLines(0.7f, 0.6f);
            hud.ImpactFlash(0.3f);
            if (hero.model != null) CbVfx.Ghost(hero.model.skin, new Color(0.4f, 0.75f, 1f, 0.6f) * 2f, 0.5f);
            CbFx.Sfx("perfect");
        }

        /// <summary>
        /// FIRE: a white-hot ball in an orange glow trailing a comet tail of fire, crackling as it flies, bursting
        /// with a ring where it hits.
        /// </summary>
        public void FireShot(Vector3 pos, int dir)
        {
            if (fireCore == null)
            {
                fireCore = CbArt.Emissive(new Color(1f, 0.95f, 0.8f) * 5f);
                fireGlow = CbArt.Glow(new Color(1f, 0.48f, 0.1f, 1f) * 2.8f, CbArt.Soft);
                fireTail = CbArt.Glow(new Color(1f, 0.42f, 0.08f, 0.9f) * 2.2f, CbVfx.Tail);
                fireTailInner = CbArt.Glow(new Color(1f, 0.85f, 0.45f, 1f) * 2.4f, CbVfx.Tail);
            }
            var t = new GameObject("fireball").transform;
            t.SetParent(transform, false);
            t.position = pos;
            var glow = CbArt.Part(CbMesh.Quad, fireGlow, t, new Vector3(0, 0, -0.3f), Vector3.one * 1.6f, shadows: false);
            var sprite = CbVfx.Painted("Fireball");
            if (sprite != null)
            {
                // the painted fireball flies right with its ball at FireballHead of its width; flipped to fly left
                if (fireSprite == null) fireSprite = CbArt.Glow(Color.white * 1.5f, sprite);
                float w = FireballLength, h = w * sprite.height / sprite.width;
                CbArt.Part(CbMesh.Quad, fireSprite, t, new Vector3(-dir * (FireballHead - 0.5f) * w, 0, -0.25f), new Vector3(w * dir, h, 1), shadows: false);
            }
            else
            {
                CbArt.Part(CbMesh.Sphere, fireCore, t, Vector3.zero, Vector3.one * 0.42f, shadows: false);
                // the tail's bright end (u = 1) sits at the ball; flipped for a ball flying left
                CbArt.Part(CbMesh.Quad, fireTail, t, new Vector3(-dir * 1.1f, 0, -0.2f), new Vector3(2.4f * dir, 0.75f, 1), shadows: false);
                CbArt.Part(CbMesh.Quad, fireTailInner, t, new Vector3(-dir * 0.7f, 0, -0.25f), new Vector3(1.5f * dir, 0.32f, 1), shadows: false);
            }
            shots.Add(new Shot { t = t, glow = glow, x = pos.x, y = pos.y, vx = dir * 18, life = 1.6f });
            // from the palm: a burst, a ring, a little jolt
            CbVfx.Burst(pos, CbFx.Fire, 1.6f, 0.12f);
            CbVfx.Ring(pos, CbFx.Flame * 0.5f, 0.9f, 0.2f);
            shake = Mathf.Max(shake, 0.08f);
            CbFx.Sfx("fire");
        }

        /// <summary>Raja's guard stopped a blow from the front: sparks off his arms, a ring, GUARD.</summary>
        public void Blocked(Vector3 at)
        {
            CbFx.Spark(at, CbFx.Grey, 10, 5, 0.3f, 0.25f, 4);
            CbVfx.Burst(at, CbFx.Grey, 1.3f, 0.1f);
            CbVfx.Ring(at, CbFx.Grey * 0.5f, 1.1f, 0.25f);
            hud.Popup(at + Vector3.up * 1.2f, "GUARD", CbHud.PopStyle.Block);
            CbFx.Sfx("block");
            shake = Mathf.Max(shake, 0.12f);
            hitstop = Mathf.Max(hitstop, 0.02f);
        }

        /// <summary>
        /// SPECIAL, the tiger flame cannon: a beam of fire from Raja's hands, BeamLength long, in layers (an orange
        /// outer flame and a yellow one streaming forward, a white-hot core, a blazing head), with rings racing
        /// along it. It hits everything in its path BeamHits times, the last blow hard.
        /// </summary>
        public void SpecialBeam(Vector3 from, int dir)
        {
            EndBeam();
            var holder = new GameObject("flameCannon").transform;
            holder.SetParent(transform, false);
            holder.position = from;
            var b = new Beam
            {
                holder = holder, from = from, dir = dir,
                outerM = CbArt.Glow(new Color(1f, 0.35f, 0.06f, 0.75f) * 2.2f, CbVfx.BeamStreaks),
                midM = CbArt.Glow(new Color(1f, 0.72f, 0.25f, 0.85f) * 2.4f, CbVfx.BeamStreaks),
                coreM = CbArt.Glow(new Color(1f, 0.96f, 0.85f, 1f) * 3f, CbVfx.BeamCore),
                headM = CbArt.Glow(new Color(1f, 0.6f, 0.15f, 1f) * 3f, CbArt.Soft),
            };
            var paintedBeam = CbVfx.Painted("Beam");
            if (paintedBeam != null) { b.outerM.mainTexture = paintedBeam; b.outerM.SetColor("_Color", Color.white * 1.5f); b.midM.SetColor("_Color", new Color(1, 0.8f, 0.4f, 0.35f)); }
            var paintedHead = CbVfx.Painted("Explosion");
            if (paintedHead != null) { b.headM.mainTexture = paintedHead; b.headM.SetColor("_Color", Color.white * 1.5f); }
            b.outer = CbArt.Part(CbMesh.Quad, b.outerM, holder, Vector3.zero, new Vector3(0.01f, 2.2f, 1), shadows: false);
            b.mid = CbArt.Part(CbMesh.Quad, b.midM, holder, new Vector3(0, 0, -0.05f), new Vector3(0.01f, 1.2f, 1), shadows: false);
            b.core = CbArt.Part(CbMesh.Quad, b.coreM, holder, new Vector3(0, 0, -0.1f), new Vector3(0.01f, 0.45f, 1), shadows: false);
            b.head = CbArt.Part(CbMesh.Quad, b.headM, holder, new Vector3(0, 0, -0.15f), Vector3.one * 2.4f, shadows: false);
            beam = b;
            // the blast: a flash, a shock ring, focus lines, the camera punches in
            CbVfx.Burst(from, CbFx.Flame, 3f, 0.2f);
            CbVfx.Ring(from, CbFx.Fire * 0.6f, 2.6f, 0.4f);
            hud.ImpactFlash(0.35f);
            hud.SpeedLines(0.9f, 0.8f);
            fovKick = -4;
            shake = Mathf.Max(shake, 0.4f);
            CbFx.Sfx("roar");
        }

        void UpdateBeam(float dt)
        {
            var b = beam;
            if (b == null) return;
            b.t += dt;
            // the beam stays on his palm while he holds it out
            if (hero.state == CbHero.State.Special) { b.from = hero.HandPoint(); b.holder.position = b.from; }
            float len = BeamLength * Mathf.Clamp01(b.t / BeamGrow);
            float thick = b.t < BeamGrow + BeamHold ? 1f + Mathf.Sin(b.t * 60) * 0.06f : Mathf.Max(0, 1 - (b.t - BeamGrow - BeamHold) / BeamFade);
            foreach (var (layer, h) in new[] { (b.outer, 2.2f), (b.mid, 1.2f), (b.core, 0.45f) })
            {
                layer.localPosition = new Vector3(b.dir * len / 2, 0, layer.localPosition.z);
                layer.localScale = new Vector3(Mathf.Max(0.01f, len), h * thick, 1);
            }
            // the flames stream away from his hands
            // the painted beam keeps its proportions at the outer layer's 2.2 m height; the streaks repeat every 4 m
            var paintedBeam = CbVfx.Painted("Beam");
            float segment = paintedBeam != null ? 2.2f * paintedBeam.width / paintedBeam.height : 4f;
            b.outerM.mainTextureScale = new Vector2(len / segment, 1);
            b.outerM.mainTextureOffset = new Vector2(-b.dir * b.t * 5f, 0);
            b.midM.mainTextureScale = new Vector2(len / 3f, 1);
            b.midM.mainTextureOffset = new Vector2(-b.dir * b.t * 8f, 0.3f);
            b.head.localPosition = new Vector3(b.dir * len, 0, -0.15f);
            b.head.localScale = Vector3.one * (2.2f + Mathf.Sin(b.t * 40) * 0.3f) * thick;
            if (cam != null) b.head.rotation = cam.transform.rotation;
            b.ringT -= dt;
            if (b.ringT <= 0 && thick > 0.5f)
            {
                b.ringT = 0.07f;
                CbVfx.Ring(b.from + Vector3.right * b.dir * Random.Range(1f, Mathf.Max(1.1f, len)), CbFx.Flame * 0.4f, 1.3f, 0.25f);
            }
            if (Random.value < 0.9f)
                CbFx.Spark(b.from + new Vector3(b.dir * Random.Range(0.5f, Mathf.Max(0.6f, len)), Random.Range(-0.5f, 0.5f), -0.3f),
                    Random.value < 0.5f ? CbFx.Fire : CbFx.Flame, 1, 3, 0.4f, 0.3f, -2, b.dir * 12);
            shake = Mathf.Max(shake, 0.15f * thick);
            b.tick -= dt;
            if (b.t >= BeamGrow * 0.5f && b.tick <= 0 && b.hits < BeamHits)
            {
                b.tick = 0.1f;
                b.hits++;
                bool last = b.hits == BeamHits;
                foreach (var e in enemies.ToArray())
                {
                    if (e.dead) continue;
                    float dx = (e.x - b.from.x) * b.dir;
                    if (dx > -0.5f && dx < len + e.w && Mathf.Abs(e.HitHeight - b.from.y) < 1.8f)
                        HurtEnemy(e, last ? 18 : 6, b.dir, last ? 12 : 1.5f, last ? 6 : 0.5f, last, true, true, true);
                }
            }
            if (b.t > BeamGrow + BeamHold + BeamFade) EndBeam();
        }

        // ---------- fire combos ----------

        /// <summary>The ring of fire round Raja during the fire whirl: it follows him, spreads, spins and fades.</summary>
        class Wheel { public Transform holder, ring; public Material mat; public CbHero hero; public float t, life; public int dir; }
        Wheel wheel;
        // the ring runs round his middle
        const float RingHeight = 1.05f;
        // flame bursts still to go off along the ground (the flame wave): when, where, which way
        readonly List<(float at, Vector3 pos, int dir)> waves = new List<(float, Vector3, int)>();

        /// <summary>
        /// The fire whirl: a ring of fire round Raja's middle - a band of the painted beam's flames (its white-hot
        /// core running round it) tilted towards the camera, so it reads as an ellipse whose front half passes in
        /// front of him and back half behind, spinning his way. A burst and a ring of fire on the ground as it starts.
        /// </summary>
        public void FireWheel(CbHero hero, int dir, float life)
        {
            EndWheel();
            var tex = CbVfx.Painted("Beam");
            var holder = new GameObject("fireRing").transform;
            holder.SetParent(transform, false);
            var mat = CbArt.Glow(new Color(1f, 0.75f, 0.4f, 0.95f) * 1.5f, tex != null ? tex : CbVfx.BeamStreaks);
            mat.mainTextureScale = new Vector2(3, 1);
            var ring = CbArt.Part(CbMesh.Frustum(1f, 48, false), mat, holder, new Vector3(0, RingHeight, 0), Vector3.one, CbArt.Rot(-0.38f, 0, 0), shadows: false);
            wheel = new Wheel { holder = holder, ring = ring, mat = mat, hero = hero, life = life, dir = dir };
            UpdateWheel(0);
            CbVfx.Ring(new Vector3(hero.x, 0.05f, 0), CbFx.Fire * 0.6f, 2.6f, 0.45f, ground: true);
            CbVfx.Explosion(new Vector3(hero.x, hero.y + 1f, -0.5f), 2.2f, 0.2f);
            shake = Mathf.Max(shake, 0.2f);
        }

        void UpdateWheel(float dt)
        {
            if (wheel == null) return;
            var w = wheel;
            w.t += dt;
            float k = w.t / w.life;
            // spread out in a tenth of the whirl, burn, then fade in the last fifth
            float spread = Mathf.Clamp01(k / 0.12f), fade = k < 0.8f ? 1 : Mathf.Max(0, 1 - (k - 0.8f) / 0.2f);
            w.holder.position = new Vector3(w.hero.x, w.hero.y, 0);
            float ro = 0.4f + 1f * spread;
            // the band is a short cylinder: its height is the flames' thickness
            w.ring.localScale = new Vector3(ro, 0.48f * (0.5f + 0.5f * fade), ro);
            w.mat.mainTextureOffset = new Vector2(-w.dir * w.t * 2.4f, 0);
            var c = new Color(1f, 0.75f, 0.4f) * 1.5f; c.a = 0.95f * fade; w.mat.SetColor("_Color", c);
            if (Random.value < 0.9f)
            {
                // sparks flung off the outer ring
                float a = Random.Range(0f, Mathf.PI * 2);
                var p = new Vector3(w.hero.x + Mathf.Cos(a) * ro, w.hero.y + RingHeight + Random.Range(-0.2f, 0.2f), Mathf.Sin(a) * ro);
                CbFx.Spark(p, Random.value < 0.5f ? CbFx.Fire : CbFx.Flame, 1, 6, 0.4f, 0.35f, -2, Mathf.Cos(a) * 8);
            }
            if (k >= 1) EndWheel();
        }

        void EndWheel()
        {
            if (wheel == null) return;
            Destroy(wheel.holder.gameObject);
            Destroy(wheel.mat);
            wheel = null;
        }

        /// <summary>The blazing palm: a big explosion of fire point blank that hurls everything in front of him.</summary>
        public void BlazePalm(Vector3 at, int dir)
        {
            CbVfx.Explosion(at, 3.8f, 0.3f);
            CbVfx.Ring(at, CbFx.Fire * 0.6f, 2.4f, 0.35f);
            CbFx.Spark(at, CbFx.Fire, 24, 8, 0.5f, 0.4f, 0, dir * 6);
            foreach (var e in enemies.ToArray())
            {
                if (e.dead) continue;
                float dx = (e.x - at.x) * dir;
                if (dx > -1.6f && dx < 1.8f + e.w && Mathf.Abs(e.HitHeight - at.y) < 1.8f) HurtEnemy(e, 18, dir, 10, 7, true, true, true, true);
            }
            shake = Mathf.Max(shake, 0.35f);
            hud.ImpactFlash(0.3f);
            hud.SpeedLines(0.5f, 0.3f);
            CbFx.Sfx("heavy");
        }

        /// <summary>The flaming staff smash: six bursts of fire run along the ground ahead of him, 1.1 m and 0.06 s apart.</summary>
        public void FlameWave(Vector3 start, int dir)
        {
            for (int i = 0; i < 6; i++) waves.Add((clock + i * 0.06f, start + new Vector3(dir * (0.6f + i * 1.1f), 0.5f, -0.3f), dir));
        }

        void UpdateWaves()
        {
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var (at, pos, dir) = waves[i];
                if (clock < at) continue;
                waves.RemoveAt(i);
                CbVfx.Explosion(pos, 2f, 0.2f);
                CbFx.Spark(pos, CbFx.Fire, 8, 5, 0.4f, 0.3f, -2);
                shake = Mathf.Max(shake, 0.15f);
                foreach (var e in enemies.ToArray())
                {
                    if (e.dead || Mathf.Abs(e.x - pos.x) > 1f + e.w * 0.5f || e.HitHeight > 2.6f) continue;
                    HurtEnemy(e, 8, dir, 3, 5, false, true, true, true);
                }
            }
        }

        void EndBeam()
        {
            if (beam == null) return;
            Destroy(beam.holder.gameObject);
            foreach (var m in new[] { beam.outerM, beam.midM, beam.coreM, beam.headM }) Destroy(m);
            beam = null;
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
                // it crackles as it flies
                s.crackle -= dt;
                if (s.crackle <= 0) { s.crackle = 0.06f; CbVfx.Burst(new Vector3(s.x, s.y, -0.35f), CbFx.Flame, 0.8f, 0.08f); }
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
                    var at = new Vector3(s.x, s.y, -0.3f);
                    CbFx.Spark(at, CbFx.Fire, hit ? 20 : 8, 6, 0.5f, 0.35f, 0);
                    if (hit)
                    {
                        // an explosion of fire where it lands
                        CbVfx.Explosion(at, 2.6f, 0.22f);
                        CbVfx.Ring(at, CbFx.Fire * 0.5f, 1.6f, 0.3f);
                        shake = Mathf.Max(shake, 0.15f);
                    }
                    Destroy(s.t.gameObject);
                    shots.RemoveAt(i);
                }
            }
            UpdateBeam(dt);
            UpdateWheel(dt);
            UpdateWaves();
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
                // a hit-stop slows the fight right down rather than freezing it, so it never reads as a hitch
                if (hitstop > 0) { hitstop -= raw; dt = raw * 0.2f; }
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
            CbVfx.Update(cam, raw);
            hud.Tick(raw);
        }

        void UpdateCamera(float dt)
        {
            fovKick += (0 - fovKick) * (1 - Mathf.Exp(-3 * dt));
            cam.fieldOfView = BaseFov + fovKick;
            float tanH = Mathf.Tan(BaseFov * 0.5f * Mathf.Deg2Rad);
            float dist = Mathf.Clamp(9.8f / (tanH * cam.aspect), 13f, 28f);
            float tx = hero.x + hero.facing * 1.6f;
            if (mode == Mode.Title) tx = 2 + Mathf.Sin(clock * 0.15f) * 3;
            if (lockZone != null) tx = Mathf.Clamp(tx, lockZone.x - 3, lockZone.x + 3);
            camX += (tx - camX) * (1 - Mathf.Exp(-3.5f * dt));
            float ty = CamBase + hero.y * 0.5f;
            camY += (ty - camY) * (1 - Mathf.Exp(-3 * dt));
            float sh = shake;
            cam.transform.position = new Vector3(camX + Random.Range(-1f, 1f) * sh * 0.4f, camY + CamRise + Random.Range(-1f, 1f) * sh * 0.4f, -dist);
            cam.transform.LookAt(new Vector3(camX, camY, 0));
            shake = Mathf.Max(0, shake - dt * 1.6f);
        }

        // ---------- input ----------

        void ReadInput(float dt)
        {
            input.ClearEdges();
            input.x = input.y = 0;
            input.atkDown = input.heavyDown = input.guard = false;
            hud.ReadTouch(input, dt);
            ReadKeys(input, dt);
            TestInput?.Invoke(input);
            // how long HIT has been held since its last press (HIT, HIT, hold = staff whirl)
            if (input.atk) atkHeldT = 0;
            else if (input.atkDown) atkHeldT += dt;
            else atkHeldT = 0;
            input.atkHeldT = atkHeldT;
            if (input.heavy) heavyHeldT = 0;
            else if (input.heavyDown) heavyHeldT += dt;
            else heavyHeldT = 0;
            input.heavyHeldT = heavyHeldT;
            input.x = Mathf.Clamp(input.x, -1, 1);
            input.y = Mathf.Clamp(input.y, -1, 1);
        }

        /// <summary>Test hook: edits the input every frame after touch and keyboard are read.</summary>
        public System.Action<CbInput> TestInput;

        void ReadKeys(CbInput inp, float dt)
        {
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
                if (kb.kKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame) inp.heavy = true;
                if (kb.kKey.isPressed || kb.xKey.isPressed) inp.heavyDown = true;
                if (kb.lKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame) inp.dash = true;
                if (kb.oKey.isPressed || kb.cKey.isPressed) inp.guard = true;
                if (kb.uKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame) inp.kiTap = true;
                if (kb.iKey.wasPressedThisFrame || kb.vKey.wasPressedThisFrame) inp.skill = true;
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
                if (pad.buttonNorth.wasPressedThisFrame) inp.heavy = true;
                if (pad.buttonNorth.isPressed) inp.heavyDown = true;
                if (pad.buttonEast.wasPressedThisFrame) inp.dash = true;
                if (pad.leftShoulder.isPressed) inp.guard = true;
                if (pad.rightTrigger.wasPressedThisFrame) inp.kiTap = true;
                if (pad.rightShoulder.wasPressedThisFrame) inp.skill = true;
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
            if (Input.GetKeyDown(KeyCode.K)) inp.heavy = true;
            if (Input.GetKey(KeyCode.K)) inp.heavyDown = true;
            if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.LeftShift)) inp.dash = true;
            if (Input.GetKey(KeyCode.O)) inp.guard = true;
            if (Input.GetKeyDown(KeyCode.U)) inp.kiTap = true;
            if (Input.GetKeyDown(KeyCode.I)) inp.skill = true;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) inp.pause = true;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J)) inp.confirm = true;
#endif
        }

        /// <summary>Test hook: puts Raja at a position (e.g. next to a fight zone).</summary>
        public void Teleport(float x) => hero.x = x;
    }
}
