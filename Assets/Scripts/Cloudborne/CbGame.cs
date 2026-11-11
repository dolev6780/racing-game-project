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
        public enum Mode { Title, Play, Paused, Over, Scene, Loading, Comic }

        public class Zone
        {
            public float x;
            public string label;
            public (CbEnemy.Kind kind, float offset)[] spawn;
            public int tip;
        }

        /// <summary>
        /// A soldier on his own along the way (the user: here and there one soldier, no warning that they come): he
        /// steps out at his post as Raja draws near, keeps to his stretch of path (range either side), and an asleep
        /// one waits until Raja is right by him. No arena: only Sergeant Snap shuts one.
        /// </summary>
        class Post { public float x, range; public CbEnemy.Kind kind; public bool asleep, spawned; public CbEnemy enemy; public Transform seat; }
        static readonly (float x, CbEnemy.Kind kind, float range, bool asleep)[] PostPlan =
        {
            (22f, CbEnemy.Kind.Hyena, 6f, false), (64f, CbEnemy.Kind.Shield, 5f, false), (71f, CbEnemy.Kind.Hyena, 4f, true),
            (108f, CbEnemy.Kind.Shield, 1.5f, false), (124f, CbEnemy.Kind.Hyena, 5f, false), (193f, CbEnemy.Kind.Hyena, 2.5f, false),
            (238.6f, CbEnemy.Kind.Hyena, 1.2f, true), (259f, CbEnemy.Kind.Hyena, 1.5f, false), (318f, CbEnemy.Kind.Hyena, 3f, false),
            (322f, CbEnemy.Kind.Shield, 3f, false), (341f, CbEnemy.Kind.Hyena, 2.5f, false), (398f, CbEnemy.Kind.Shield, 2f, false),
            (476f, CbEnemy.Kind.Hyena, 4f, false), (486f, CbEnemy.Kind.Shield, 4f, false), (499f, CbEnemy.Kind.Hyena, 3f, true),
            (515f, CbEnemy.Kind.Hyena, 3f, false),
        };
        readonly List<Post> posts = new List<Post>();
        const float PostWakes = 13f;

        /// <summary>The stretches of the stage, named at the top of the screen as Raja reaches them.</summary>
        static readonly (float x, string name)[] Sections =
        {
            (0f, "Save Koda"), (70f, "The cracked wall"), (79f, "The long river"), (106f, "The old shrine"), (136f, "The mountain"),
            (194f, "The vine ravine"), (240f, "The temple of traps"), (292f, "The gong gorge"), (336f, "The raft river"),
            (400f, "The canopy"), (438f, "The waterfall"), (470f, "The ridge"), (520f, "The shrine gate"),
        };
        /// <summary>Tips as Raja reaches them (tip: one of the fight tips; else text).</summary>
        static readonly (float x, int tip, string text)[] Tips =
        {
            (17f, 1, null), (57f, 3, null), (66f, 2, null),
            (192f, -1, "Swing on the vines: hold <color=#ffb03a>↔</color> to swing, <color=#ffb03a>JUMP</color> to let go. Hang from the bars"),
            (241f, -1, "The old temple: thorns need the staff, mind the floor, duck <color=#ffb03a>↓</color> the darts"),
            (292f, -1, "Strike the gong!"),
            (336f, -1, "Mud slows you. Ride the rafts; the spinning log rolls you back - keep walking"),
            (400f, -1, "Climb the vines (hold <color=#ffb03a>↑</color>), ride the wind up to the treetops"),
            (438f, -1, "Something glints behind the waterfall..."),
            (450f, -1, "Boulders! Jump over them or smash them with <color=#ffb03a>HEAVY</color>"),
            (470f, 4, null),
            (530f, -1, "Sergeant Snap waits at the shrine gate"),
        };
        int sectionIdx, tipIdx;
        float checkpointX, lastHp;
        bool hurtOnce;
        /// <summary>Tiger tokens found this run (three hidden on the stage).</summary>
        public int Tokens { get; private set; }
        /// <summary>The chest's key, found up in the treetops.</summary>
        public bool HasKey { get; set; }
        public float CheckpointX => checkpointX;

        /// <summary>The fight tips; the ki moves are staff techniques until Raja's fire wakes (the fire crystal).</summary>
        static string Tip(int i)
        {
            switch (i)
            {
                case 0: return "Tap <color=#ffb03a>HIT</color> four times for a combo, <color=#ffb03a>HEAVY</color> for the staff";
                case 1: return "Hold <color=#ffb03a>HEAVY</color> to whirl the staff. Hold <color=#ffb03a>GUARD</color> to block";
                case 2:
                    return CbSave.FireAwake
                        ? "<color=#ffb03a>FIRE</color> shoots a fireball. Duck under a slash with <color=#ffb03a>↓</color>"
                        : "<color=#ffb03a>KI</color>: a staff lunge. <color=#ffb03a>HIT, HIT, KI</color>: the staff cyclone. Duck with <color=#ffb03a>↓</color>";
                case 3: return "Shields stop paws. Use the staff: <color=#ffb03a>HEAVY</color>, <color=#ffb03a>↑ + HEAVY</color> or <color=#ffb03a>DASH → HEAVY</color>";
                default: return "<color=#ffb03a>DASH</color> just before a blow lands: slow motion, double damage. <color=#ffb03a>SPECIAL</color> when ki is high";
            }
        }

        public static CbGame I { get; private set; }

        // Sergeant Snap, the mini-boss: his arena stands in front of the shrine gate
        const float BossX = 550f;
        static readonly Zone BossZone = new Zone { x = BossX, label = "Sergeant Snap", spawn = new (CbEnemy.Kind, float)[0] };
        /// <summary>Sergeant Snap while he is on the stage.</summary>
        public CbEnemy boss;
        bool bossStarted, bossDone;

        public Mode mode = Mode.Title;
        public CbHero hero;
        public CbHud hud;
        public CbWorld world;
        /// <summary>Pickups, crates and secrets.</summary>
        public CbItems items;
        /// <summary>Things to climb and things in the way.</summary>
        public CbLevel level;
        /// <summary>The main menu, shown in mode Title.</summary>
        public CbMenu menu;
        /// <summary>Drawn, animated cutscenes (mode Comic, or inside a scene).</summary>
        public CbComic comic;
        /// <summary>Fades and the loading screen.</summary>
        public CbTransition fade;
        /// <summary>The cinematic playing (mode Scene), else null.</summary>
        public CbScene scene;
        bool cleared, introSeen;
        public readonly List<CbEnemy> enemies = new List<CbEnemy>();
        public Zone lockZone;
        public float clock, time, hitstop, shake, slow;
        public int score, combo, maxCombo;
        public readonly CbInput input = new CbInput();

        Camera cam;
        Light sun;
        Transform actors;
        float comboT, camX, camY = CamBase, fovKick, overT, atkHeldT, heavyHeldT, camBack = 1f;
        int lastScore = -1;
        readonly List<(float at, CbEnemy.Kind kind, float x, Zone zone)> pending = new List<(float, CbEnemy.Kind, float, Zone)>();
        readonly List<Shot> shots = new List<Shot>();
        // pieces of things broken: their transform, velocity, spin, time left and their own scale (they shrink from it)
        readonly List<(Transform t, Vector3 v, float spin, float life, Vector3 scale)> debris = new List<(Transform, Vector3, float, float, Vector3)>();
        Beam beam;
        // A side-on view like 2.5D platformers: a narrow field of view from further back flattens the perspective,
        // and the camera looks almost level, so the path reads as the top of a platform with its front face below
        // and the scenery stands up behind it, instead of a green floor tilting away.
        // the camera looks a little above his head: the ground sits low in the view, the painted jungle fills it
        const float BaseFov = 26f, CamBase = 2.1f, CamRise = 0.6f, ViewHalfWidth = 8f, LookAhead = 2.4f;
        // drawn back over high ground: down to CamLow (just under the water) in view, at most CamBackMax times as far
        const float CamLow = -0.9f, CamBackMax = 1.45f;

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
            items = new CbItems(this, transform);
            level = new CbLevel(this, transform);
            actors = new GameObject("Actors").transform;
            actors.SetParent(transform, false);
            hero = new CbHero(this, actors);
            hud = new CbHud(transform);
            menu = new CbMenu(this, transform);
            comic = new CbComic(transform);
            fade = new CbTransition(transform);
            introSeen = CbSave.IntroSeen;
            AudioListener.volume = CbSave.Volume;
            ResetGame();
            mode = Mode.Title;
            hud.SetScreen(CbHud.Screen.Title);
            menu.Open();
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
            var sky = CbArt.Sky(CbArt.Hex("#2a78a8"), CbWorld.Horizon, CbWorld.Horizon);
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
            items?.Reset();
            level?.Reset();
            cleared = false;
            hud?.SetCoins(0);
            EndBeam();
            EndWheel();
            waves.Clear();
            hero.Reset();
            time = score = 0; combo = maxCombo = 0; comboT = 0; hitstop = shake = slow = 0; lockZone = null; fovKick = 0;
            foreach (var p in posts) if (p.seat != null) Destroy(p.seat.gameObject);
            posts.Clear();
            foreach (var (x, kind, range, asleep) in PostPlan)
                posts.Add(new Post { x = x, kind = kind, range = range, asleep = asleep, seat = asleep ? Seat(x) : null });
            sectionIdx = tipIdx = 0; checkpointX = 0; Tokens = 0; HasKey = false; hurtOnce = false; lastHp = hero.hp;
            boss = null; bossStarted = bossDone = false;
            hud?.HideBoss();
            hud.SetWave("Chase the raiders");
            hud.SetTip(Tip(0));
            hud.ShowGo(true);
            hud.SetCombo(0);
            hud.SetSlow(false);
            hud.SetScore(0);
            lastScore = 0;
        }

        /// <summary>
        /// The first START: the opening scene (Koda carried off, Raja setting out after him), then the stage. Later runs
        /// skip it.
        /// </summary>
        public void PlayOpening()
        {
            menu?.Hide();
            ResetGame();
            mode = Mode.Scene;
            hud.SetScreen(CbHud.Screen.Scene);
            scene = CbStory.Opening(this);
        }

        /// <summary>
        /// PLAY (or a stage card) in the menu: the loading screen, then the first time the drawn prologue and the
        /// opening scene, later straight into the stage.
        /// </summary>
        public void PlayStage(string id)
        {
            if (mode == Mode.Loading || fade.Busy) return;
            mode = Mode.Loading;
            fade.Loading("STAGE 1-1 · THE JUNGLE GATE", 1.3f, () =>
            {
                menu.Hide();
                if (introSeen) StartGame();
                else if (CbComic.Available(CbStory.Prologue)) PlayPrologue();
                else PlayOpening();
            });
        }

        /// <summary>The drawn prologue (the night the cloud temple fell), then a fade into the opening scene.</summary>
        void PlayPrologue()
        {
            ResetGame();
            mode = Mode.Comic;
            hud.SetScreen(CbHud.Screen.Scene);
            comic.Play(CbStory.Prologue, () => fade.FadeThrough(() => { comic.Hide(); PlayOpening(); }));
        }

        /// <summary>MENU on the pause or result screen: the run ends and the main menu comes back.</summary>
        public void ToMenu()
        {
            CancelInvoke();
            AudioListener.pause = false;
            ResetGame();
            mode = Mode.Title;
            hud.SetScreen(CbHud.Screen.Title);
            menu.Open();
        }

        /// <summary>The opening scene will play again on the next PLAY (settings).</summary>
        public void ReplayOpening()
        {
            introSeen = false;
            CbSave.IntroSeen = false;
        }

        /// <summary>A scene has ended (played through or skipped): back to the stage.</summary>
        public void EndScene()
        {
            scene = null;
            mode = Mode.Play;
            hud.SetScreen(CbHud.Screen.None);
        }

        /// <summary>After the opening: it counts as seen, and the chase begins.</summary>
        public void OpeningDone()
        {
            introSeen = true;
            CbSave.IntroSeen = true;
            hud.SetWave("Chase the raiders");
            hud.SetTip(CbSave.FireAwake ? "<color=#ffb03a>FIRE</color> shoots a fireball"
                : "No powers, only the staff: <color=#ffb03a>KI</color> for staff techniques, <color=#ffb03a>SPECIAL</color> when ki is high");
            hud.Banner("SAVE KODA!");
        }

        /// <summary>Raja reaches the arena: the gate is shut, and Sergeant Snap steps out (a scene, then the fight).</summary>
        void StartBoss()
        {
            bossStarted = true;
            lockZone = BossZone;
            hud.ShowGo(false);
            mode = Mode.Scene;
            hud.SetScreen(CbHud.Screen.Scene);
            scene = CbStory.SnapIntro(this, BossX);
        }

        /// <summary>The fight with Sergeant Snap begins, with him standing at x.</summary>
        public void SpawnBoss(float x)
        {
            boss = new CbEnemy(this, CbEnemy.Kind.Sergeant, x, actors);
            enemies.Add(boss);
            hud.ShowBoss("SGT. SNAP");
            hud.SetWave("Defeat Sergeant Snap");
            hud.SetTip("<color=#ffb03a>GUARD</color> his three blows, then strike. Purple flash: get away! A <color=#ffb03a>KI</color> move stops his whistle");
            hud.Banner("SGT. SNAP!");
        }

        /// <summary>His whistle: two raiders run in from the arena's sides.</summary>
        public void SummonRaiders()
        {
            if (lockZone == null) return;
            enemies.Add(new CbEnemy(this, CbEnemy.Kind.Hyena, lockZone.x - 9.5f, actors));
            enemies.Add(new CbEnemy(this, CbEnemy.Kind.Hyena, lockZone.x + 9.5f, actors));
            hud.Banner("REINFORCEMENTS!", true);
        }

        void BossBeaten()
        {
            bossDone = true;
            lockZone = null;
            hud.HideBoss();
            hud.Banner("SNAP DEFEATED!");
            hud.ShowGo(true);
            hud.SetWave("Reach the shrine gate");
            hud.SetTip("The way to the shrine gate is open");
        }

        /// <summary>Starts a run (also used by the play mode tests).</summary>
        public void StartGame()
        {
            menu?.Hide();
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

        /// <summary>
        /// Raja has fallen. The stage is long: he comes back at the last stone lantern he lit, whole again (a few
        /// hundred points lost); the soldiers still standing go back to their posts, and Snap's fight starts over.
        /// </summary>
        public void Defeated()
        {
            if (mode != Mode.Play) return;
            fade.FadeThrough(Respawn);
        }

        void Respawn()
        {
            foreach (var p in posts) if (p.enemy != null && !p.enemy.dead) { p.spawned = false; p.enemy = null; }
            foreach (var e in enemies) e.f.Destroy();
            enemies.Clear();
            pending.Clear();
            if (bossStarted && !bossDone) { bossStarted = false; boss = null; lockZone = null; hud.HideBoss(); hud.ShowGo(true); }
            hero.RespawnAt(checkpointX);
            lastHp = hero.hp;
            score = Mathf.Max(0, score - 500);
            hud.Banner("TRY AGAIN!", true);
        }

        /// <summary>
        /// The log an asleep soldier dozes on (CbEnemy): it lies along the path behind him (he faces the way Raja
        /// comes), a little back from it, his seat at its near end. It stays when he wakes.
        /// </summary>
        Transform Seat(float x)
        {
            float floor = level != null ? level.SupportAt(x, 0.1f) : 0;
            if (floor < -0.6f) floor = 0;
            var t = CbArt.Joint(actors, x + CbEnemy.SeatBack, floor, CbEnemy.SeatZ, "sleepersLog");
            if (CbArt.Prop("Cloudborne/Jungle/Log/Log", t, CbArt.PropOutline) == null) { Destroy(t.gameObject); return null; }
            // the model is 2 m long and 0.7 m thick: shorter, and as high as his seat when he sits
            t.localScale = new Vector3(0.75f, 0.87f, 0.87f);
            return t;
        }

        /// <summary>A stone lantern lit: where Raja comes back after falling.</summary>
        public void Checkpoint(float x)
        {
            if (x <= checkpointX) return;
            checkpointX = x;
            hud.Popup(new Vector3(x, 2.6f, 0), "CHECKPOINT", CbHud.PopStyle.Normal);
            CbFx.Sfx("pickup", 0.5f);
        }

        /// <summary>A tiger token found (CbItems).</summary>
        public void TokenFound()
        {
            Tokens++;
            hud.Banner($"TIGER TOKEN {Tokens}/3", true);
        }

        void EndGame(bool won)
        {
            if (mode != Mode.Play) return;
            mode = Mode.Over;
            overT = 0;
            CbSave.StageResult("1-1", won, score, time);
            // stars: cleared, every tiger token, and under seven minutes; a badge for no hit taken
            int stars = (won ? 1 : 0) + (Tokens >= 3 ? 1 : 0) + (won && time < 420f ? 1 : 0);
            hud.ShowResult(won, score, maxCombo, time, Tokens, level.CagesFreed, stars, won && !hurtOnce);
        }

        void UpdateFlow(float dt)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].at > time) continue;
                if (pending[i].zone == lockZone) enemies.Add(new CbEnemy(this, pending[i].kind, pending[i].x, actors));
                pending.RemoveAt(i);
            }
            if (comboT > 0)
            {
                comboT -= dt;
                if (comboT <= 0) { combo = 0; hud.SetCombo(0); }
            }
            // the soldiers along the way: each steps out at his post as Raja draws near; no warning, no arena
            if (lockZone == null)
                foreach (var p in posts)
                {
                    if (!p.spawned && Mathf.Abs(hero.x - p.x) < PostWakes)
                    {
                        p.spawned = true;
                        p.enemy = new CbEnemy(this, p.kind, p.x, actors) { asleep = p.asleep };
                        enemies.Add(p.enemy);
                    }
                    // he roams his stretch of open path, and it widens when a wall in it is broken (not a line no one
                    // can see: the user saw a soldier stop where a box had been, even after it was gone)
                    if (p.enemy != null && !p.enemy.dead && level != null)
                    {
                        level.FreeStretch(p.x, 18f, out float lo, out float hi);
                        items?.ClipByCrates(p.enemy.x, p.enemy.w * 0.8f, ref lo, ref hi);
                        p.enemy.minX = lo; p.enemy.maxX = hi;
                    }
                }
            // the stretch's name and a tip as he reaches them
            while (sectionIdx < Sections.Length && hero.x >= Sections[sectionIdx].x) hud.SetWave(Sections[sectionIdx++].name);
            while (tipIdx < Tips.Length && hero.x >= Tips[tipIdx].x) { var tip = Tips[tipIdx++]; hud.SetTip(tip.text ?? Tip(tip.tip)); }
            // the sergeant waits in front of the gate
            if (lockZone == null && !bossStarted && hero.x > BossX - 7f)
            {
                StartBoss();
                return;
            }
            if (lockZone == BossZone)
            {
                if (boss != null) hud.SetBoss(boss.hp / boss.max);
                if (boss != null && boss.dead && !enemies.Exists(e => !e.dead)) BossBeaten();
                return;
            }
            if (lockZone == null && bossDone && !cleared && hero.x >= CbWorld.GoalX)
            {
                cleared = true;
                hud.ShowGo(false);
                hud.Banner("STAGE CLEAR");
                // Raja cheers (a hop and a fist punched high) before the results come up
                hero.Celebrate();
                Invoke(nameof(Won), 2.2f);
            }
        }

        void Won() => EndGame(true);

        // ---------- combat ----------

        /// <summary>
        /// A blow on an enemy. breaker: a staff blow (breaks a raised shield); fire: it sets him burning; ki: a ki move
        /// (a staff technique or fire), which stops the sergeant's whistle.
        /// </summary>
        public bool HurtEnemy(CbEnemy e, float dmg, int dir, float kb, float up, bool big, bool breaker, bool fire, bool fromPlayer, bool ki = false)
        {
            if (e.dead) return false;
            e.asleep = false;
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
            if (fromPlayer) dmg *= CbSave.DamageMul;
            // a ki move (a staff technique, or fire) on the sergeant while he whistles stops the call
            if ((fire || ki) && e.Whistling) e.BreakWhistle();
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

        /// <summary>
        /// A beaten enemy, having lain there a moment, vanishes in a puff of smoke with a pop and a few stars; what he
        /// carried bursts out of it (the sergeant's purse: more coins and a mango).
        /// </summary>
        public void EnemyPoof(CbEnemy e)
        {
            var at = new Vector3(e.x, e.y + 0.7f, -0.4f);
            CbFx.Puff(at, new Color(0.96f, 0.95f, 0.92f), e.Boss ? 22 : 14, e.Boss ? 3f : 2.2f, e.Boss ? 1.3f : 0.95f, 0.6f);
            CbFx.Puff(at + Vector3.up * 0.3f, new Color(0.82f, 0.8f, 0.86f), 6, 1.2f, 0.7f, 0.5f);
            CbFx.Spark(at, CbFx.Flame, 8, 5f, 0.22f, 0.5f, 2);
            CbVfx.Burst(at, CbFx.Hit, e.Boss ? 2.2f : 1.4f, 0.16f);
            CbFx.Sfx("poof", 0.8f);
            items.Drop(new Vector3(e.x, e.y + 0.6f, 0));
            if (e.Boss)
            {
                for (int k = 0; k < 4; k++) items.Drop(new Vector3(e.x, e.y + 0.6f, 0));
                items.Spawn(CbItems.Kind.Mango, new Vector3(e.x, e.y + 0.6f, 0));
            }
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
        readonly List<(float at, Vector3 pos, int dir, bool fire)> waves = new List<(float, Vector3, int, bool)>();

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
            for (int i = 0; i < 6; i++) waves.Add((clock + i * 0.06f, start + new Vector3(dir * (0.6f + i * 1.1f), 0.5f, -0.3f), dir, true));
        }

        /// <summary>
        /// The quake (KI in a staff smash, before Raja's fire wakes): the ground breaks in six bursts of dust and stones
        /// ahead of him, 1.1 m and 0.06 s apart, throwing up whoever stands there.
        /// </summary>
        public void QuakeWave(Vector3 start, int dir)
        {
            for (int i = 0; i < 6; i++) waves.Add((clock + i * 0.06f, start + new Vector3(dir * (0.6f + i * 1.1f), 0.1f, -0.3f), dir, false));
        }

        void UpdateWaves()
        {
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var (at, pos, dir, fire) = waves[i];
                if (clock < at) continue;
                waves.RemoveAt(i);
                if (fire)
                {
                    CbVfx.Explosion(pos, 2f, 0.2f);
                    CbFx.Spark(pos, CbFx.Fire, 8, 5, 0.4f, 0.3f, -2);
                }
                else
                {
                    // broken ground: a jet of dust and stones, a ring on the ground
                    CbFx.Spark(pos, CbFx.Dust, 12, 6, 0.55f, 0.45f, 8);
                    CbFx.Spark(pos, CbFx.Grey, 6, 7, 0.3f, 0.5f, 14);
                    CbVfx.Ring(new Vector3(pos.x, pos.y - 0.05f, 0), CbFx.Dust * 0.6f, 1.4f, 0.3f, ground: true);
                }
                shake = Mathf.Max(shake, 0.15f);
                foreach (var e in enemies.ToArray())
                {
                    // the wave runs along the ground it was struck on: not up or down to another level
                    if (e.dead || Mathf.Abs(e.x - pos.x) > 1f + e.w * 0.5f || Mathf.Abs(e.y - (pos.y - 0.1f)) > 1.2f) continue;
                    HurtEnemy(e, 8, dir, 3, fire ? 5 : 7, false, true, fire, true, ki: true);
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

        public void AddDebris(Transform t, Vector3 velocity, float spinDeg) => debris.Add((t, velocity, spinDeg, 1.6f, t.localScale));

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
                if (!hit && items.ShotCrates(s.x, s.y)) hit = true;
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
                // shrinking away in its own shape (scaling it to a cube made splinters into square pixels)
                if (d.life < 0.4f) d.t.localScale = d.scale * Mathf.Max(0.01f, d.life / 0.4f);
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

            if (input.pause)
            {
                // SKIP or pause: a drawn cutscene ends, else a scene, else the game pauses
                if (comic.Playing) comic.Skip();
                else if (mode == Mode.Scene) scene?.Skip();
                else if (!fade.Busy) TogglePause();
            }
            // a tap during a drawn cutscene moves on to its next painting
            else if (input.confirm && comic.Playing) comic.Tap();
            if (mode == Mode.Title)
            {
                hero.Idle(raw);
                menu.Update(raw, input);
            }
            else if (mode == Mode.Scene)
            {
                scene?.Update(raw, input);
                comic.Update(raw);
                UpdateShots(raw);
            }
            else if (mode == Mode.Loading) hero.Idle(raw);
            else if (mode == Mode.Comic) comic.Update(raw);
            else if (mode == Mode.Paused)
            {
                if (input.menu && !fade.Busy) fade.FadeThrough(ToMenu);
                else if (input.confirm && !fade.Busy) TogglePause();
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
                // a hit taken (for the no-damage badge)
                if (hero.hp < lastHp - 0.01f) hurtOnce = true;
                lastHp = hero.hp;
                for (int i = enemies.Count - 1; i >= 0; i--)
                    if (enemies[i].Update(edt)) { enemies[i].f.Destroy(); enemies.RemoveAt(i); }
                UpdateShots(dt);
                if (mode == Mode.Play) { UpdateFlow(dt); items.Update(dt, hero, cam); level.Update(dt, hero); }
                hud.SetBars(hero.hp / hero.MaxHp * 100f, hero.ki);
                if (score != lastScore) { lastScore = score; hud.SetScore(score); }
                if (mode == Mode.Over)
                {
                    overT += raw;
                    if (overT > 0.8f && !fade.Busy && input.menu) fade.FadeThrough(ToMenu);
                    else if (overT > 0.8f && !fade.Busy && input.confirm) fade.FadeThrough(StartGame);
                }
            }
            world.Update(clock, camX);
            UpdateCamera(raw);
            CbVfx.Update(cam, raw);
            hud.Tick(raw);
            fade.Update(raw);
        }

        void UpdateCamera(float dt)
        {
            fovKick += (0 - fovKick) * (1 - Mathf.Exp(-3 * dt));
            cam.fieldOfView = BaseFov + fovKick;
            float tanH = Mathf.Tan(BaseFov * 0.5f * Mathf.Deg2Rad);
            // ViewHalfWidth metres of the path either side of the centre: close enough that Raja and the soldiers are
            // big on the screen (the user's reference), looking further ahead the way he faces to make up for it
            float dist = Mathf.Clamp(ViewHalfWidth / (tanH * cam.aspect), 11f, 24f);
            // up high (a rock in the river, a ledge, the mountain, the bars) the camera draws back to show the ground
            // or the water below him and the way on (the user, on the rock in the river: you can't see where to go).
            // From where he stands, so a jump doesn't pull it in and out; never more than CamBackMax.
            float stand = Mathf.Max(0, hero.StandY);
            float needHalf = (stand + 2.4f - CamLow) / 2 + 0.3f;
            float back = Mathf.Clamp(Mathf.Max(needHalf / (dist * tanH), 1f + 0.12f * Mathf.Min(stand, 3f)), 1f, CamBackMax);
            if (mode != Mode.Play) back = 1f;
            camBack += (back - camBack) * (1 - Mathf.Exp(-2f * dt));
            dist *= camBack;
            float tx = hero.x + hero.facing * LookAhead;
            // in the menu Raja stands on the right of the screen, the menu on the left, the camera a little closer
            if (mode == Mode.Title) { tx = hero.x - 3.4f + Mathf.Sin(clock * 0.2f) * 0.3f; dist *= 0.72f; }
            if (lockZone != null) tx = Mathf.Clamp(tx, lockZone.x - 3, lockZone.x + 3);
            // half his height on the path and in jumps; high up the mountain the camera climbs with him
            float ty = CamBase + Mathf.Max(hero.y * 0.5f, hero.y - 2.5f);
            // up high, the view's foot rests just below the ground (as far as it can, him still in view)
            float high = mode == Mode.Play ? Mathf.Clamp01((stand - 0.4f) / 0.8f) : 0f;
            if (high > 0)
            {
                float half = dist * tanH;
                ty = Mathf.Lerp(ty, Mathf.Max(CamLow + half, stand + 2.7f - half), high);
                // ...but it follows him up when he jumps from there
                ty = Mathf.Max(ty, hero.y + 2.2f - half);
            }
            // a scene directs the camera itself: where it looks and how close it comes
            if (mode == Mode.Scene && scene != null) { tx = camX = scene.camX; ty = camY = scene.camY; dist *= scene.zoom; }
            camX += (tx - camX) * (1 - Mathf.Exp(-3.5f * dt));
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
            ReadSprint(input);
        }

        // a press is the stick pushed past SprintPress (a light push only walks); pressed again the same way within
        // SprintTap of letting go, he sprints until it is let go
        const float SprintPress = 0.6f, SprintTap = 0.3f;
        int pressDir, lastPressDir;
        float releasedAt = -9f;
        bool sprinting;

        void ReadSprint(CbInput inp)
        {
            int dir = inp.x > SprintPress ? 1 : inp.x < -SprintPress ? -1 : 0;
            if (dir != 0 && pressDir == 0)
            {
                sprinting = dir == lastPressDir && Time.unscaledTime - releasedAt < SprintTap;
                lastPressDir = dir;
            }
            if (Mathf.Abs(inp.x) < 0.3f)
            {
                if (pressDir != 0) releasedAt = Time.unscaledTime;
                sprinting = false;
            }
            if (dir != 0 || Mathf.Abs(inp.x) < 0.3f) pressDir = dir;
            inp.sprint = sprinting && dir != 0;
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
                if (kb.mKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) inp.menu = true;
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
                if (pad.selectButton.wasPressedThisFrame) inp.menu = true;
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
            if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Backspace)) inp.menu = true;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J)) inp.confirm = true;
#endif
        }

        /// <summary>Test hook: puts Raja at a position (e.g. next to a fight zone).</summary>
        public void Teleport(float x) => hero.x = x;

        /// <summary>Test hook: counts every fight as won, so Raja can go anywhere on the stage.</summary>
        public void SkipFights()
        {
            pending.Clear();
            foreach (var e in enemies) e.f.Destroy();
            enemies.Clear();
            lockZone = null;
            foreach (var p in posts) p.spawned = true;
            bossStarted = bossDone = true;
            boss = null;
            hud.HideBoss();
        }

        /// <summary>Test hook: every fight on the way is won, and Sergeant Snap still waits at the gate.</summary>
        public void SkipToBoss()
        {
            SkipFights();
            bossStarted = bossDone = false;
        }
    }
}
