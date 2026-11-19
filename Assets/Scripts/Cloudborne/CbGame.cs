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
        static (float x, CbEnemy.Kind kind, float range, bool asleep)[] PostPlan => CbStage.Treetops ? TreetopsPosts : CbStage.Grove || CbStage.River ? GrovePosts
            : CbStage.Ember ? EmberPosts : JunglePosts;
        static (float x, string name)[] Sections => CbStage.Treetops ? TreetopsSections : CbStage.Grove ? GroveSections : CbStage.River ? RiverSections
            : CbStage.Ember ? EmberSections : JungleSections;
        static (float x, int tip, string text)[] Tips => CbStage.Treetops ? TreetopsTips : CbStage.Grove ? GroveTips : CbStage.River ? RiverTips
            : CbStage.Ember ? EmberTips : JungleTips;

        // stage 1-4, The Stormy River: riding, no soldiers; Koda shouting from his barge ahead; the saw barge at the end
        static readonly (float x, string name)[] RiverSections =
        {
            (0f, "The rapids"), (CbLevel.Falls[0] - 6f, "The first falls"), (CbLevel.Falls[1] - 6f, "The second falls"),
            (CbLevel.BasinFrom - 4f, "The basin"),
        };
        static readonly (float x, int tip, string text)[] RiverTips =
        {
            (2f, -1, "On the log! <color=#ffb03a>JUMP</color> the rocks, <color=#ffb03a>stick ↓</color> ducks the branches, <color=#ffb03a>ATTACK</color> smashes the barrels"),
            (36f, -1, "Koda: \"Raja! Tell them I'm not the child of the prophecy!\""),
            (118f, -1, "Koda: \"...OK, maybe a little!\""),
            (172f, -1, "Ledges over the river - something golden glints up there"),
            (CbLevel.Falls[0] - 12f, -1, "The falls! Hold on to the log..."),
            (CbLevel.Falls[1] - 12f, -1, "Another waterfall - and something mechanical roaring behind you..."),
        };

        /// <summary>Stage 1-4: Raja is carried down the river on his log (until the basin at its end).</summary>
        public bool Riding => CbStage.River && mode == Mode.Play && lockZone == null && hero.x < CbLevel.BasinFrom;

        /// <summary>How fast the river carries him, the stick at ix.</summary>
        public float RideVx(float ix) => CbLevel.RideSpeed + Mathf.Clamp(ix, -1f, 1f) * CbLevel.RideLean;

        // stage 1-3, Baba's Grove: no soldiers; Baba's three trials (CbLevelGrove), each shutting the way until it is done
        static readonly (float x, CbEnemy.Kind kind, float range, bool asleep)[] GrovePosts = new (float, CbEnemy.Kind, float, bool)[0];
        // stage 1-5, The Ember Temple: the last of the raiders on the way in through the burning jungle, then Grak
        static readonly (float x, CbEnemy.Kind kind, float range, bool asleep)[] EmberPosts =
        {
            (16f, CbEnemy.Kind.Hyena, 4f, false), (24f, CbEnemy.Kind.Hyena, 4f, false), (33f, CbEnemy.Kind.Shield, 4f, false),
            (41f, CbEnemy.Kind.Thrower, 4f, false),
        };
        static readonly (float x, string name)[] EmberSections =
        {
            (0f, "The burning jungle"), (CbLevel.GrakX - 14f, "The Ember Temple"),
        };
        static readonly (float x, int tip, string text)[] EmberTips =
        {
            (2f, -1, "The jungle is burning... the Ember Temple is just ahead"),
            (CbLevel.GrakX - 16f, -1, "Grak has the fire crystal. Get it back!"),
        };
        static readonly (float x, string name)[] GroveSections =
        {
            (0f, "Baba's grove"), (CbLevel.TrialX[0] - 12f, "The bells"), (CbLevel.TrialX[1] - 12f, "The bamboo dummies"),
            (CbLevel.TrialX[2] - 12f, "The stones"), (CbLevel.TrialX[2] + 11f, "Back to the chase"),
        };
        static readonly (float x, int tip, string text)[] GroveTips =
        {
            (2f, -1, "Baba's lesson: three trials - the bells, the bamboo dummies, the stones"),
        };
        static readonly Zone[] TrialZones =
        {
            new Zone { x = CbLevel.TrialX[0], label = "The bells", spawn = new (CbEnemy.Kind, float)[0] },
            new Zone { x = CbLevel.TrialX[1], label = "The bamboo dummies", spawn = new (CbEnemy.Kind, float)[0] },
            new Zone { x = CbLevel.TrialX[2], label = "The stones", spawn = new (CbEnemy.Kind, float)[0] },
        };
        int trial;

        /// <summary>Stage 1-3: Baba's trials, one after the other - each shuts the way as he reaches it, until it is done.</summary>
        void UpdateTrials()
        {
            if (trial >= CbLevel.Trials || level == null) return;
            var z = TrialZones[trial];
            if (lockZone == null && hero.x > z.x - 10f)
            {
                lockZone = z;
                hud.ShowGo(false);
                hud.Banner(z.label.ToUpperInvariant());
                level.StartTrial(trial);
            }
            if (lockZone == z && level.TrialDone(trial))
            {
                lockZone = null;
                trial++;
                hud.ShowGo(true);
                hud.Banner(trial >= CbLevel.Trials ? "LESSON LEARNED!" : "WELL DONE!", true);
                if (trial >= CbLevel.Trials) hud.SetTip("Baba: \"Now go - Koda won't wait for you!\"");
            }
        }

        /// <summary>Which of Baba's trials Raja is on (stage 1-3; 3: all done). Test hook.</summary>
        public int TrialsDone => trial;

        /// <summary>Test hook: Baba's trials before trial i count as done (the way to it open).</summary>
        public void SkipTrialsTo(int i)
        {
            trial = i;
            lockZone = null;
            level?.LeaveTrial();
            hud.ShowGo(true);
        }

        // stage 1-2, The Treetops: soldiers along the boughs and on the long bridge, a group at the loggers' camp
        static readonly (float x, CbEnemy.Kind kind, float range, bool asleep)[] TreetopsPosts =
        {
            (16f, CbEnemy.Kind.Hyena, 5f, false), (52f, CbEnemy.Kind.Shield, 4f, false), (114f, CbEnemy.Kind.Hyena, 2.5f, true),
            (152f, CbEnemy.Kind.Hyena, 2f, false), (174f, CbEnemy.Kind.Thrower, 3f, false), (264f, CbEnemy.Kind.Hyena, 2f, false),
            (300f, CbEnemy.Kind.Hyena, 3f, false), (314f, CbEnemy.Kind.Shield, 3f, false),
            (326f, CbEnemy.Kind.Hyena, 3f, false), (330f, CbEnemy.Kind.Shield, 3f, false), (334f, CbEnemy.Kind.Thrower, 3f, false),
        };
        static readonly (float x, string name)[] TreetopsSections =
        {
            (0f, "Into the treetops"), (60f, "The vine gap"), (90f, "The saw drones"), (118f, "Crumbling branches"),
            (148f, "The swinging logs"), (180f, "Hand over hand"), (212f, "The big bough"), (250f, "The long bridge"),
            (288f, "Thorny boughs"), (320f, "The loggers' camp"), (345f, "Tusk's clearing"),
        };
        static readonly (float x, int tip, string text)[] TreetopsTips =
        {
            (2f, -1, "The raiders fled into the treetops with Koda - after them!"),
            (20f, -1, "Mind the gaps between the boughs - it's a long way down"),
            (58f, -1, "Swing across on the vines: hold <color=#ffb03a>↔</color> to swing, <color=#ffb03a>JUMP</color> to let go"),
            (90f, -1, "A saw drone! Slide under it (<color=#ffb03a>stick ↓</color>), jump over it, or smash it with the staff (hold <color=#ffb03a>ATTACK</color>)"),
            (118f, -1, "Cracked branches give way under you - don't wait on them"),
            (148f, -1, "Swinging logs! Time your run"),
            (178f, -1, "Hang from the bar and go hand over hand. The wind blows up from below..."),
            (248f, -1, "A long rope bridge - and saw drones flying over it"),
            (286f, -1, "Thorns: cut them with the staff (hold <color=#ffb03a>ATTACK</color>)"),
            (318f, -1, "The loggers' camp! Koda must be here"),
            (342f, -1, "Something big is snorting up ahead..."),
        };
        static readonly (float x, CbEnemy.Kind kind, float range, bool asleep)[] JunglePosts =
        {
            (22f, CbEnemy.Kind.Hyena, 6f, false),
            // (the rope bridge has an ambush of its own; the soldier on the rock in the long river is gone: the user)
            (64f, CbEnemy.Kind.Shield, 5f, false), (71f, CbEnemy.Kind.Hyena, 4f, true),
            // waiting together on the far bank of the long river, by the shrine: three at once, the river at their backs
            (106.5f, CbEnemy.Kind.Hyena, 3f, false), (109.5f, CbEnemy.Kind.Shield, 3f, false), (112.5f, CbEnemy.Kind.Thrower, 3f, false),
            (124f, CbEnemy.Kind.Hyena, 5f, false), (193f, CbEnemy.Kind.Hyena, 2.5f, false),
            (238.6f, CbEnemy.Kind.Hyena, 1.2f, true), (259f, CbEnemy.Kind.Hyena, 1.5f, false), (289f, CbEnemy.Kind.Hyena, 3f, false),
            (293f, CbEnemy.Kind.Shield, 3f, false), (297f, CbEnemy.Kind.Thrower, 3f, false), (363f, CbEnemy.Kind.Shield, 2f, false),
            // (the ridge and its four soldiers are gone: the stage was long for a phone - the user)
        };
        readonly List<Post> posts = new List<Post>();
        const float PostWakes = 13f;

        /// <summary>The stretches of the stage, named at the top of the screen as Raja reaches them.</summary>
        static readonly (float x, string name)[] JungleSections =
        {
            (0f, "Save Koda"), (70f, "The cracked wall"), (79f, "The long river"), (106f, "The old shrine"), (136f, "The flooded gorge"),
            (194f, "The vine ravine"), (240f, "The temple of traps"), (283f, "The monkey cage"), (307f, "The raft river"),
            (365f, "The canopy"), (392f, "The waterfall"), (425f, "The stone bridge"), (452f, "The shrine gate"),
        };
        /// <summary>Tips as Raja reaches them (tip: one of the fight tips; else text).</summary>
        static readonly (float x, int tip, string text)[] JungleTips =
        {
            (17f, 1, null),
            (6f, -1, "<color=#ffb03a>JUMP</color> - and again in the air for a double jump"),
            (21f, -1, "A fallen trunk! Push the <color=#ffb03a>stick ↓</color> to slide under it"),
            (29f, -1, "The rope bridge - careful, it's a fine place for an ambush"),
            (57f, 3, null), (66f, 2, null),
            (97f, -1, "They wait on the bank - knock them back into the river!"),
            (115f, -1, "A springy leaf: jump on it to bounce up to the ledge"),
            (179f, -1, "Another fallen trunk, thorns on top: slide under it"),
            (192f, -1, "Swing on the vines: hold <color=#ffb03a>↔</color> to swing, <color=#ffb03a>JUMP</color> to let go. Hang from the bars"),
            (241f, -1, "The old temple: thorns need the staff (hold <color=#ffb03a>ATTACK</color>), slide under the darts (<color=#ffb03a>stick ↓</color>)"),
            (285f, 4, null),
            (291f, -1, "A monkey in a cage! Break it open and set it free"),
            (307f, -1, "Ride the rafts down the river - jump off before they sink"),
            (365f, -1, "Climb the vines (push into them), ride the wind up to the treetops"),
            (390f, -1, "Down from the treetops, up the waterfall's cliff by its vines. Something glints behind the water..."),
            (423f, -1, "The old stone bridge - the shrine gate lies beyond it"),
            (454f, -1, "Sergeant Snap waits at the shrine gate"),
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
                case 0: return "Tap <color=#ffb03a>ATTACK</color> for a combo, hold it for the staff";
                case 1: return "Keep holding <color=#ffb03a>ATTACK</color> to whirl the staff. <color=#ffb03a>Swipe ↔</color> to dodge a blow";
                case 2:
                    return CbSave.FireAwake
                        ? "<color=#ffb03a>FIRE</color> shoots a fireball. <color=#ffb03a>SPECIAL</color> when it lights up"
                        : "<color=#ffb03a>KI</color>: a staff lunge. <color=#ffb03a>SPECIAL</color> when it lights up";
                case 3: return "Shields stop paws: hold <color=#ffb03a>ATTACK</color> for the staff";
                default: return "<color=#ffb03a>Swipe ↔</color> just before a blow lands: slow motion, double damage";
            }
        }

        public static CbGame I { get; private set; }

        // The mini-boss: Sergeant Snap in stage 1-1, his arena in front of the shrine gate; Tusk in stage 1-2, in his
        // clearing at the end of the loggers' camp
        const float SnapX = 474f;
        static float BossX => CbStage.Treetops ? CbLevel.TuskX : CbStage.River ? CbLevel.BasinX - 2f : CbStage.Ember ? CbLevel.GrakX : SnapX;
        static readonly Zone SnapZone = new Zone { x = SnapX, label = "Sergeant Snap", spawn = new (CbEnemy.Kind, float)[0] };
        static readonly Zone TuskZone = new Zone { x = CbLevel.TuskX, label = "Tusk", spawn = new (CbEnemy.Kind, float)[0] };
        static readonly Zone BargeZone = new Zone { x = CbLevel.BasinX - 3f, label = "The saw barge", spawn = new (CbEnemy.Kind, float)[0] };
        static readonly Zone GrakZone = new Zone { x = CbLevel.GrakX, label = "Commander Grak", spawn = new (CbEnemy.Kind, float)[0] };
        static Zone BossZone => CbStage.Treetops ? TuskZone : CbStage.River ? BargeZone : CbStage.Ember ? GrakZone : SnapZone;
        /// <summary>
        /// An ambush on a bridge: once he is from metres along it, the way shuts either side and raiders come at him
        /// from both ends, wave after wave (offsets from the zone's middle); when they are beaten, it opens.
        /// </summary>
        class Ambush
        {
            public Zone zone;
            public float from, to;
            public string tip, cleared;
            public (CbEnemy.Kind kind, float dx)[][] waves;
            public bool started, done;
            public int wave;
        }
        // On the rope bridge at the start: six raiders in two waves (the user: at least five). On the long stone bridge
        // (the user: a bridge a screen and a half long, with a fight on it): ten in three waves, the last the biggest.
        readonly Ambush ropeAmbush = new Ambush
        {
            zone = new Zone { x = CbLevel.RopeBridgeX, label = "Ambush on the rope bridge", spawn = new (CbEnemy.Kind, float)[0] },
            from = CbLevel.RopeBridgeX - 8f, to = CbLevel.RopeBridgeX + CbLevel.RopeDeckHalf,
            tip = "Raiders on the rope bridge! Shields stop paws: hold <color=#ffb03a>ATTACK</color> for the staff",
            cleared = "BRIDGE CLEARED!",
            waves = new[]
            {
                new[] { (CbEnemy.Kind.Hyena, -8f), (CbEnemy.Kind.Hyena, 8f), (CbEnemy.Kind.Hyena, 9.5f) },
                new[] { (CbEnemy.Kind.Shield, 9f), (CbEnemy.Kind.Hyena, -9f), (CbEnemy.Kind.Hyena, -7f) },
            },
        };
        readonly Ambush stoneAmbush = new Ambush
        {
            zone = new Zone { x = CbLevel.StoneBridgeX, label = "Ambush on the bridge", spawn = new (CbEnemy.Kind, float)[0] },
            from = CbLevel.StoneBridgeX - 4f, to = CbLevel.StoneBridgeX + CbLevel.StoneBridgeHalf,
            tip = "Raiders on the bridge! Hold your ground",
            cleared = "BRIDGE CLEARED!",
            waves = new[]
            {
                new[] { (CbEnemy.Kind.Hyena, -8f), (CbEnemy.Kind.Hyena, 8f), (CbEnemy.Kind.Shield, 9.5f) },
                new[] { (CbEnemy.Kind.Shield, -9.5f), (CbEnemy.Kind.Hyena, -7.5f), (CbEnemy.Kind.Thrower, 9f) },
                new[] { (CbEnemy.Kind.Hyena, -9f), (CbEnemy.Kind.Hyena, 9f), (CbEnemy.Kind.Shield, -7f), (CbEnemy.Kind.Thrower, 9.5f) },
            },
        };
        // stage 1-2: on the big bough, six raiders in two waves
        readonly Ambush boughAmbush = new Ambush
        {
            zone = new Zone { x = CbLevel.BoughAmbushX, label = "Ambush on the big bough", spawn = new (CbEnemy.Kind, float)[0] },
            from = CbLevel.BoughAmbushX - 4f, to = CbLevel.BoughAmbushX + 8f,
            tip = "Raiders on the bough! Hold your ground",
            cleared = "BOUGH CLEARED!",
            waves = new[]
            {
                new[] { (CbEnemy.Kind.Hyena, -8f), (CbEnemy.Kind.Hyena, 8f), (CbEnemy.Kind.Shield, 9.5f) },
                new[] { (CbEnemy.Kind.Thrower, 9f), (CbEnemy.Kind.Hyena, -9f), (CbEnemy.Kind.Hyena, -7f) },
            },
        };
        Ambush[] jungleAmbushes, treetopsAmbushes;
        static readonly Ambush[] NoAmbushes = new Ambush[0];
        // (each stage its own: the rope bridge's ambush, at 35 metres in, shut the river at the same place in stage 1-4 -
        // the river stopped carrying Raja just short of its first rock, and raiders stood on the water)
        Ambush[] Ambushes => CbStage.Treetops ? (treetopsAmbushes ??= new[] { boughAmbush })
            : CbStage.Jungle ? (jungleAmbushes ??= new[] { ropeAmbush, stoneAmbush }) : NoAmbushes;
        bool exiting;
        // The soldiers of the fight going on (the ambush on the bridge, the sergeant and his raiders): the fight is won
        // when they are beaten. (It waited on every soldier on the stage: one left behind - asleep along the way - and
        // the second wave never came, and the bridge stayed shut: the user.)
        readonly List<CbEnemy> arenaFoes = new List<CbEnemy>();
        bool ArenaClear => incoming.Count == 0 && !arenaFoes.Exists(e => !e.dead);
        public bool BridgeFightDone => stoneAmbush.done;
        /// <summary>Which wave of the ambush on the stone bridge is on (0 before it).</summary>
        public int BridgeWave => stoneAmbush.wave;
        /// <summary>The ambush on the rope bridge at the start: which wave is on (0 before it), and whether it is won.</summary>
        public int RopeAmbushWave => ropeAmbush.wave;
        public bool RopeAmbushDone => ropeAmbush.done;
        /// <summary>Sergeant Snap while he is on the stage.</summary>
        public CbEnemy boss;
        bool bossStarted, bossDone;

        public Mode mode = Mode.Title;
        public CbHero hero;
        public CbHud hud;
        public CbWorld world;
        public CbCritters critters;
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
        /// <summary>Whether the stage has been cleared this run.</summary>
        public bool Cleared => cleared;
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
        const float CamLow = -0.9f, CamBackMax = 1.45f, TuskFightBack = 1.35f, BargeFightBack = 1.4f, GrakFightBack = 1.3f, RideLook = 4.5f;
        // in the gorge the camera draws back further and looks at its middle, showing its walls and the water below
        // (the user wanted it further out there)
        const float CamGorge = 1.95f;

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
            // every new game starts at stage 1-1 (the stage chosen is built when it is played)
            CbStage.Set("1-1");
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
            // the graphics' level for this phone (CbQuality: shadows, MSAA, bloom, sparks)
            ApplyQuality();
            CbFx.Init(transform);
            CbVfx.Init(transform);
            world = new CbWorld(transform);
            critters = new CbCritters(transform);
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
            // (a profile saved before bloom was made cheaper gets the same: CreatePostProfile)
            else if (profile.TryGet<Bloom>(out var saved))
            {
                saved.downscale.Override(BloomDownscaleMode.Quarter);
                saved.maxIterations.Override(5);
            }
            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.transform.SetParent(transform, false);
            vol.isGlobal = true;
            vol.priority = 10;
            vol.sharedProfile = profile;
            profile.TryGet(out grading);
            profile.TryGet(out bloomFx);
        }

        Bloom bloomFx;
        float slowFor;

        /// <summary>Sets the graphics for CbQuality's current level (at the start, and when SETTINGS changes it).</summary>
        public void ApplyQuality() => CbQuality.Apply(sun, bloomFx);

        /// <summary>On AUTO, a phone that keeps running slowly in play steps down a level, for good.</summary>
        void WatchSpeed(float raw)
        {
            if (mode != Mode.Play || TestStep > 0 || Fps < 1f) return;
            slowFor = Fps < CbQuality.StepDownFps ? slowFor + raw : Mathf.Max(0, slowFor - raw * 0.5f);
            if (slowFor < CbQuality.StepDownAfter) return;
            slowFor = 0;
            if (CbQuality.StepDown()) ApplyQuality();
        }

        // ---------- impact frames ----------

        // The anime impact frame (the user wanted the look and feel of an anime fighting game): on the biggest blows the
        // picture goes stark black and white for a moment, the hit frozen in it. The grading's saturation and contrast
        // are swung there and back; never more often than ImpactGap.
        ColorAdjustments grading;
        float impactT, impactGap;
        const float ImpactGap = 0.7f, GradeSaturation = 12f, GradeContrast = 12f;

        /// <summary>An impact frame: a moment in stark black and white (a finisher, a crash, a boss going down).</summary>
        public void ImpactFrame(float time = 0.07f)
        {
            if (impactGap > 0 || grading == null) return;
            impactT = time;
            impactGap = ImpactGap;
            hitstop = Mathf.Max(hitstop, time);
            // and the camera punches in on it for a moment (eased back out by UpdateCamera)
            fovKick = Mathf.Min(fovKick, -2.6f);
        }

        /// <summary>Whether an impact frame is showing (test hook).</summary>
        public bool InImpactFrame => impactT > 0;

        void UpdateImpactFrame(float raw)
        {
            impactGap = Mathf.Max(0, impactGap - raw);
            if (grading == null) return;
            bool on = impactT > 0;
            impactT = Mathf.Max(0, impactT - raw);
            grading.saturation.Override(on ? -100f : GradeSaturation);
            grading.contrast.Override(on ? 75f : GradeContrast);
        }

        /// <summary>
        /// The anime look: bloom kept for fire and glow only, neutral tone mapping (ACES washes colours out and shifts
        /// them), a little extra saturation and contrast (more turns the grass fluorescent), a soft vignette.
        /// </summary>
        public static VolumeProfile CreatePostProfile()
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = p.Add<Bloom>(true);
            // (worked out at a quarter of the screen, as cheap on a phone as bloom gets: it only lights the glow)
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(5);
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
            StageLight();
        }

        /// <summary>
        /// The stage's light, fog and sky: the jungle's clear day, or (stage 1-5) the burning temple's - a low orange sun,
        /// warm shade, and smoke for fog (the jungle's pale blue haze showed as a grey band under its burning painting).
        /// </summary>
        // the daylight sun (one painting, one light, for each stage: the time of day of its part of the story)
        static readonly Color DaySun = new Color(1f, 0.9f, 0.72f);
        static readonly Vector3 DaySunDir = new Vector3(9, -15, 11);
        const float DaySunPower = 1.8f;

        void StageLight()
        {
            bool ember = CbStage.Ember;
            if (sun != null)
            {
                sun.color = ember ? new Color(1f, 0.66f, 0.42f) : DaySun;
                sun.intensity = ember ? 1.6f : DaySunPower;
                sun.transform.rotation = Quaternion.LookRotation(DaySunDir);
            }
            RenderSettings.ambientSkyColor = ember ? new Color(0.78f, 0.5f, 0.4f) : new Color(0.58f, 0.74f, 0.8f);
            RenderSettings.ambientEquatorColor = ember ? new Color(0.52f, 0.32f, 0.24f) : new Color(0.4f, 0.52f, 0.44f);
            RenderSettings.ambientGroundColor = ember ? new Color(0.24f, 0.13f, 0.09f) : new Color(0.2f, 0.24f, 0.17f);
            var horizon = ember ? CbArt.Hex("#4a2418") : CbWorld.Horizon;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogStartDistance = ember ? 16f : 24f;
            RenderSettings.fogEndDistance = ember ? 120f : 175f;
            // each stage its own grade over everything (an anime action game's colour, the user's reference): the
            // jungle as painted, the treetops a touch warm, Baba's grove golden, the river cool and stormy, the temple fiery
            if (grading != null)
                grading.colorFilter.Override(CbStage.Treetops ? new Color(1f, 0.98f, 0.94f) : CbStage.Grove ? new Color(1f, 0.96f, 0.86f)
                    : CbStage.River ? new Color(0.86f, 0.94f, 1f) : ember ? new Color(1f, 0.9f, 0.82f) : Color.white);
            var sky = CbArt.Sky(ember ? CbArt.Hex("#2a1410") : CbArt.Hex("#2a78a8"), horizon, horizon);
            if (sky != null) RenderSettings.skybox = sky;
            else { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = horizon; }
        }

        // ---------- flow ----------

        void ResetGame()
        {
            foreach (var e in enemies) e.f.Destroy();
            enemies.Clear();
            arenaFoes.Clear();
            pending.Clear();
            incoming.Clear();
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
            boss = null; bossStarted = bossDone = false; FireAwakened = false;
            // (stage 1-3 has no boss: Baba's trials, then the way on)
            if (CbStage.Grove) bossStarted = bossDone = true;
            trial = 0;
            foreach (var a in Ambushes) { a.started = a.done = false; a.wave = 0; }
            exiting = false;
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
            CbStage.Set(id);
            fade.Loading(CbStage.Title, 1.3f, () =>
            {
                menu.Hide();
                BuildStage();
                // the opening is stage 1-1's; stage 1-2 starts straight away, stage 1-3 with Baba's lesson (drawn)
                if (CbStage.Grove && CbComic.Available(CbStory.BabaLesson)) PlayLesson();
                else if (CbStage.Ember && CbComic.Available(CbStory.CrystalTorn)) PlayDrawnStart(CbStory.CrystalTorn);
                else if (introSeen || !CbStage.Jungle) StartGame();
                else if (CbComic.Available(CbStory.Prologue)) PlayPrologue();
                else PlayOpening();
            });
        }

        string builtStage = "1-1";

        /// <summary>
        /// The stage's scenery, critters, items and level built afresh when another stage is chosen (each is built for
        /// the stage being played: CbStage).
        /// </summary>
        public void BuildStage()
        {
            if (builtStage == CbStage.Id) return;
            builtStage = CbStage.Id;
            foreach (var e in enemies) e.f.Destroy();
            enemies.Clear();
            foreach (var p in posts) if (p.seat != null) Destroy(p.seat.gameObject);
            posts.Clear();
            Destroy(world.root.gameObject);
            critters.Destroy();
            items.Destroy();
            level.Destroy();
            world = new CbWorld(transform);
            critters = new CbCritters(transform);
            items = new CbItems(this, transform);
            level = new CbLevel(this, transform);
            jungleAmbushes = treetopsAmbushes = null;
            StageLight();
        }

        /// <summary>The drawn prologue (the night the cloud temple fell), then a fade into the opening scene.</summary>
        void PlayPrologue()
        {
            ResetGame();
            mode = Mode.Comic;
            hud.SetScreen(CbHud.Screen.Scene);
            comic.Play(CbStory.Prologue, () => fade.FadeThrough(() => { comic.Hide(); PlayOpening(); }));
        }

        /// <summary>Stage 1-3's secret found: a moment later its drawing, whole (the cub in the clouds).</summary>
        public void ShowSecretSoon() => Invoke(nameof(ShowSecret), 0.7f);

        void ShowSecret()
        {
            if (mode != Mode.Play || !CbComic.Available(CbStory.CubInTheClouds)) return;
            mode = Mode.Comic;
            hud.SetScreen(CbHud.Screen.Scene);
            comic.Play(CbStory.CubInTheClouds, () => fade.FadeThrough(() =>
            {
                comic.Hide();
                mode = Mode.Play;
                hud.SetScreen(CbHud.Screen.None);
            }));
        }

        /// <summary>Stage 1-3 opens with Baba's lesson, drawn (the story doc), then the trials begin.</summary>
        void PlayLesson() => PlayDrawnStart(CbStory.BabaLesson);

        /// <summary>A stage that opens with a drawn moment (Baba's lesson, the crystal torn out), then begins.</summary>
        void PlayDrawnStart(CbComic.Shot[] shots)
        {
            ResetGame();
            mode = Mode.Comic;
            hud.SetScreen(CbHud.Screen.Scene);
            comic.Play(shots, () => fade.FadeThrough(() => { comic.Hide(); StartGame(); }));
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
                : "No powers, only the staff: <color=#ffb03a>KI</color> for staff techniques, <color=#ffb03a>SPECIAL</color> when it lights up");
            hud.Banner("SAVE KODA!");
        }

        /// <summary>Raja reaches the arena: the gate is shut, and Sergeant Snap steps out (a scene, then the fight).</summary>
        void StartBoss()
        {
            bossStarted = true;
            lockZone = BossZone;
            hud.ShowGo(false);
            if (CbStage.Treetops) { SpawnTusk(); return; }
            if (CbStage.Ember) { SpawnGrak(); return; }
            if (CbStage.River)
            {
                level.StartSawBarge();
                hud.ShowBoss("THE SAW BARGE");
                hud.SetWave("Sink the saw barge");
                hud.SetTip("<color=#ffb03a>Swipe ↔</color> INTO its logs to send them back at its fuel barrels. Jump its saws - stuck, its barrels are in reach!");
                BossCutIn("THE SAW BARGE!", level.SawBargeCloseUp);
                return;
            }
            mode = Mode.Scene;
            hud.SetScreen(CbHud.Screen.Scene);
            scene = CbStory.SnapIntro(this, BossX);
        }

        /// <summary>The fight with Sergeant Snap begins, with him standing at x.</summary>
        public void SpawnBoss(float x)
        {
            boss = new CbEnemy(this, CbEnemy.Kind.Sergeant, x, actors);
            enemies.Add(boss);
            arenaFoes.Clear();
            arenaFoes.Add(boss);
            hud.ShowBoss("SGT. SNAP");
            hud.SetWave("Defeat Sergeant Snap");
            hud.SetTip("<color=#ffb03a>Swipe ↔</color> to dodge his three blows, then strike. Purple flash: get away! A <color=#ffb03a>KI</color> move stops his whistle");
            BossCutIn(boss, "SGT. SNAP!");
        }

        /// <summary>
        /// Tusk's fight: he waits at the far end of his clearing, rears and roars, and charges from trunk to trunk.
        /// </summary>
        void SpawnTusk()
        {
            boss = new CbEnemy(this, CbEnemy.Kind.Tusk, BossX + 4.5f, actors) { minX = BossX - CbEnemy.TuskCrash, maxX = BossX + CbEnemy.TuskCrash };
            enemies.Add(boss);
            arenaFoes.Clear();
            arenaFoes.Add(boss);
            hud.ShowBoss("TUSK");
            hud.SetWave("Defeat Tusk");
            hud.SetTip("His armour stops blows from the front. Jump or <color=#ffb03a>swipe ↔</color> out of his charge: he rams the tree - hit him from behind!");
            BossCutIn(boss, "TUSK!");
        }

        /// <summary>
        /// Commander Grak's fight (stage 1-5): he waits in the middle of the temple's courtyard, pounds his chest and roars,
        /// and comes on (CbEnemyGrak).
        /// </summary>
        void SpawnGrak()
        {
            boss = new CbEnemy(this, CbEnemy.Kind.Grak, BossX + 5f, actors) { minX = BossX - CbLevel.GrakCrash, maxX = BossX + CbLevel.GrakCrash };
            enemies.Add(boss);
            arenaFoes.Clear();
            arenaFoes.Add(boss);
            hud.ShowBoss("COMMANDER GRAK");
            hud.SetWave("Defeat Commander Grak");
            hud.SetTip("His iron stops blows from the front. Dodge his saw and strike as it misses; jump or <color=#ffb03a>swipe ↔</color> out of his charge - winded against the pillar, he is open!");
            BossCutIn(boss, "COMMANDER GRAK!");
        }

        /// <summary>Grak's last slam knocked the crystal out of his armour: it rolls to Raja, and his fire wakes.</summary>
        public void CrystalLoose() => Invoke(nameof(FireAwakens), CbLevel.CrystalRoll + 0.25f);

        /// <summary>
        /// The crystal reaches Raja (the story doc, "the stripes light up", drawn): he touches it and his fire wakes - his
        /// stripes gold, his ki full, his moves now fire. Grak, his crystal gone, can be beaten.
        /// </summary>
        void FireAwakens()
        {
            if (mode != Mode.Play || !CbStage.Ember) return;
            if (!CbComic.Available(CbStory.FireWakes)) { AwakenFire(); return; }
            mode = Mode.Comic;
            hud.SetScreen(CbHud.Screen.Scene);
            comic.Play(CbStory.FireWakes, () => fade.FadeThrough(() =>
            {
                comic.Hide();
                mode = Mode.Play;
                hud.SetScreen(CbHud.Screen.None);
                AwakenFire();
            }));
        }

        void AwakenFire()
        {
            CbSave.FireAwake = true;
            level?.CrystalTaken();
            hero.ki = 100;
            hero.hp = Mathf.Min(hero.MaxHp, hero.hp + 40f);
            boss?.Awaken();
            FireAwakened = true;
            var at = new Vector3(hero.x, hero.y + 1.2f, -0.4f);
            CbFx.Spark(at, CbFx.Flame, 40, 9, 0.6f, 0.8f, -2);
            CbVfx.Burst(at, CbFx.Flame, 3.4f, 0.3f);
            CbVfx.Ring(at, CbFx.Fire * 0.5f, 3f, 0.5f);
            hud.ImpactFlash(0.6f);
            hud.Banner("THE FIRE AWAKENS!");
            hud.SetTip("Your fire is awake! <color=#ffb03a>SPECIAL</color>: the tiger flame cannon - finish him!");
        }

        /// <summary>Raja's fire woke in this fight (test hook).</summary>
        public bool FireAwakened { get; private set; }

        /// <summary>
        /// Grak beaten, the end of chapter 1 (drawn: the airship over the burnt jungle, Koda on its deck; the Crown's eye
        /// opens), then the stage is clear.
        /// </summary>
        void ChapterEnd()
        {
            if (mode != Mode.Play) return;
            if (!CbComic.Available(CbStory.ShipInTheSky)) { StageCleared(); return; }
            mode = Mode.Comic;
            hud.SetScreen(CbHud.Screen.Scene);
            comic.Play(CbStory.ShipInTheSky, () => fade.FadeThrough(() =>
            {
                comic.Hide();
                mode = Mode.Play;
                hud.SetScreen(CbHud.Screen.None);
                StageCleared();
            }));
        }

        /// <summary>A raider stepping into a fight at x, on the stone bridge's deck if that is over the water.</summary>
        CbEnemy SpawnAt(CbEnemy.Kind kind, float x)
        {
            var e = new CbEnemy(this, kind, x, actors);
            if (level != null && CbLevel.InWater(x)) e.y = Mathf.Max(0, level.SupportAt(x, 50f));
            enemies.Add(e);
            return e;
        }

        /// <summary>
        /// An ambush's soldiers: they come running in from off the screen at the right, one after another (the user:
        /// they just appeared - at the arena's sides, one of them on top of Raja; the offsets now only order them).
        /// </summary>
        void SpawnFoes(params (CbEnemy.Kind kind, float dx)[] foes)
        {
            for (int i = 0; i < foes.Length; i++) incoming.Add((time + i * RunInEvery, foes[i].kind));
            SpawnIncoming();
        }

        // an ambush's soldiers still to come running in: when, and who
        readonly List<(float at, CbEnemy.Kind kind)> incoming = new List<(float, CbEnemy.Kind)>();
        // where they start, past the arena's middle: the camera keeps within 3 m of the middle and shows ViewHalfWidth
        // either side, so they start just out of view - still on a bridge the fight is on (the stone bridge ends 12.2 m
        // past its middle); every RunInEvery seconds the next one, from the same place
        const float RunInFrom = 3f + ViewHalfWidth + 1f, RunInEvery = 0.55f;

        /// <summary>Every soldier of the ambush's wave has come running in.</summary>
        public bool AllRunIn => incoming.Count == 0;

        void SpawnIncoming()
        {
            if (lockZone == null) { incoming.Clear(); return; }
            for (int i = 0; i < incoming.Count; i++)
            {
                if (incoming[i].at > time) continue;
                var e = SpawnAt(incoming[i].kind, lockZone.x + RunInFrom);
                // on what is there to stand on: the stone bridge's deck (from the ground under it he walked under the
                // bridge, never able to step up onto it: the user)
                if (level != null) e.y = Mathf.Max(e.y, level.SupportAt(e.x, 50f));
                e.runningIn = true;
                e.facing = -1;
                arenaFoes.Add(e);
                incoming.RemoveAt(i--);
            }
        }

        /// <summary>His whistle: two raiders run in from the arena's sides.</summary>
        public void SummonRaiders()
        {
            if (lockZone == null) return;
            foreach (var dx in new[] { -9.5f, 9.5f })
            {
                var e = new CbEnemy(this, CbEnemy.Kind.Hyena, lockZone.x + dx, actors);
                enemies.Add(e);
                arenaFoes.Add(e);
            }
            hud.Banner("REINFORCEMENTS!", true);
        }

        void BossBeaten()
        {
            bossDone = true;
            lockZone = null;
            hud.HideBoss();
            level?.OpenGoalGate(true);
            hud.ShowGo(true);
            if (CbStage.River)
            {
                // the end of 1-4: Koda's barge breaks up on the rocks - and his cage is taken up into the sky (a scene)
                hud.Banner("THE SAW BARGE SINKS!");
                cleared = true;
                hud.ShowGo(false);
                mode = Mode.Scene;
                hud.SetScreen(CbHud.Screen.Scene);
                scene = CbStory.RiverEnd(this);
                return;
            }
            if (CbStage.Ember)
            {
                hud.Banner("GRAK DEFEATED!");
                cleared = true;
                hud.ShowGo(false);
                Invoke(nameof(ChapterEnd), 2.4f);
                return;
            }
            if (CbStage.Treetops)
            {
                hud.Banner("TUSK DEFEATED!");
                hud.SetWave("Find Koda's cage");
                hud.SetTip("Koda's cage is at the end of the camp");
                return;
            }
            hud.Banner("SNAP DEFEATED!");
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
            arenaFoes.Clear();
            pending.Clear();
            incoming.Clear();
            if (bossStarted && !bossDone) { bossStarted = false; boss = null; lockZone = null; hud.HideBoss(); hud.ShowGo(true); }
            foreach (var a in Ambushes)
                if (a.started && !a.done) { a.started = false; a.wave = 0; lockZone = null; hud.ShowGo(true); }
            if (CbStage.Grove && lockZone != null) { lockZone = null; level?.LeaveTrial(); hud.ShowGo(true); }
            if (CbStage.River) level?.LeaveSawBarge();
            if (CbStage.Ember) { level?.LeaveGrakFight(); CancelInvoke(nameof(FireAwakens)); }
            hero.RespawnAt(checkpointX);
            level?.Respawned(checkpointX);
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
            hud.Banner(CbStage.River ? $"GOLDEN LOTUS {Tokens}/3" : $"TIGER TOKEN {Tokens}/3", true);
        }

        void EndGame(bool won)
        {
            if (mode != Mode.Play) return;
            mode = Mode.Over;
            overT = 0;
            CbSave.StageResult(CbStage.Id, won, score, time);
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
                        // one out in the river stands on its rock
                        if (level != null && CbLevel.InWater(p.x)) p.enemy.y = Mathf.Max(0, level.SupportAt(p.x, 50f));
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
            // the ambushes on the bridges (their soldiers come running in one after another)
            SpawnIncoming();
            foreach (var a in Ambushes)
            {
                if (lockZone == null && !a.started && hero.x > a.from && hero.x < a.to)
                {
                    a.started = true;
                    a.wave = 1;
                    lockZone = a.zone;
                    arenaFoes.Clear();
                    hud.ShowGo(false);
                    hud.Banner("AMBUSH!");
                    hud.SetTip(a.tip);
                    SpawnFoes(a.waves[0]);
                }
                if (lockZone != a.zone) continue;
                if (ArenaClear)
                {
                    if (a.wave < a.waves.Length)
                    {
                        a.wave++;
                        hud.Banner(a.wave == a.waves.Length && a.waves.Length > 2 ? "THE LAST OF THEM!" : "MORE OF THEM!", true);
                        SpawnFoes(a.waves[a.wave - 1]);
                    }
                    else
                    {
                        a.done = true;
                        lockZone = null;
                        hud.Banner(a.cleared);
                        hud.ShowGo(true);
                    }
                }
                return;
            }
            if (CbStage.Grove) UpdateTrials();
            // the sergeant waits in front of the gate
            if (lockZone == null && !bossStarted && hero.x > BossX - 7f)
            {
                StartBoss();
                return;
            }
            if (lockZone == BossZone && CbStage.River)
            {
                hud.SetBoss(level.FuelLeft / 3f);
                if (level.SawBargeBeaten) BossBeaten();
                return;
            }
            if (lockZone == BossZone)
            {
                if (boss != null) hud.SetBoss(boss.hp / boss.max);
                if (boss != null && boss.dead && ArenaClear) BossBeaten();
                return;
            }
            if (lockZone == null && bossDone && !cleared && hero.x >= CbWorld.GoalX && (!CbStage.Grove || trial >= CbLevel.Trials))
            {
                cleared = true;
                hud.ShowGo(false);
                // stage 1-2: Koda's cage at the end of the camp - empty (a scene, then the stage is clear)
                if (CbStage.Treetops)
                {
                    mode = Mode.Scene;
                    hud.SetScreen(CbHud.Screen.Scene);
                    scene = CbStory.EmptyCage(this, CbLevel.KodaCageX);
                    return;
                }
                hud.Banner("STAGE CLEAR");
                // through the open gate he walks on, before the results come up (he jumped and cheered, which was too
                // much: the user - just open the gate and let him pass)
                exiting = true;
                Invoke(nameof(Won), 2.2f);
            }
        }

        /// <summary>After the scene at the end of a stage (stage 1-2's empty cage): the stage is clear, he walks on.</summary>
        public void StageCleared()
        {
            hud.Banner("STAGE CLEAR");
            exiting = true;
            Invoke(nameof(Won), 1.6f);
        }

        void Won() => EndGame(true);

        // ---------- combat ----------

        /// <summary>
        /// A blow on an enemy. breaker: a staff blow (breaks a raised shield); fire: it sets him burning; ki: a ki move
        /// (a staff technique or fire), which stops the sergeant's whistle.
        /// </summary>
        public bool HurtEnemy(CbEnemy e, float dmg, int dir, float kb, float up, bool big, bool breaker, bool fire, bool fromPlayer, bool ki = false, bool pierce = false)
        {
            if (e.dead) return false;
            e.asleep = false;
            bool frontal = dir == -e.facing;
            var at = new Vector3(e.x - dir * 0.3f, e.HitHeight, -0.5f);
            // Tusk's armour: a blow from in front glances off, unless his tusks are stuck in a tree
            if (!pierce && e.Armoured(dir))
            {
                var plate = new Vector3(e.x - dir * e.w, e.HitHeight + 0.2f, -0.5f);
                CbFx.Spark(plate, CbFx.Grey, 10, 6, 0.3f, 0.25f, 4);
                CbVfx.Burst(plate, CbFx.Grey, 1.3f, 0.1f);
                hud.Popup(new Vector3(e.x - dir * e.w, e.HitHeight + 1.4f, 0), "ARMOUR!", CbHud.PopStyle.Block);
                CbFx.Sfx("block");
                hero.vx = -dir * 4;
                hitstop = Mathf.Max(hitstop, 0.03f);
                return false;
            }
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
            // (Grak can't be beaten before Raja's fire wakes: CbEnemyGrak)
            e.HoldOn();
            e.flash = 0.12f;
            if (fire) { e.burnT = 3; e.burnTick = 0.4f; }
            combo++;
            comboT = 1.8f;
            maxCombo = Mathf.Max(maxCombo, combo);
            score += Mathf.RoundToInt(dmg * 10 * (1 + combo * 0.1f));
            hero.ki = Mathf.Min(100, hero.ki + 3);
            CbFx.Spark(at, fire ? CbFx.Fire : CbFx.Hit, big ? 22 : 12, big ? 11 : 7, big ? 0.6f : 0.4f);
            CbVfx.Burst(at, fire ? CbFx.Fire : CbFx.Hit, big ? 2.4f : 1.4f, big ? 0.2f : 0.12f);
            // a staff blow, or any big one of his, sweeps a slash arc across where it lands
            if (fromPlayer && (breaker || big))
                CbVfx.Slash(at + new Vector3(0, 0, -0.1f), dir, fire ? CbFx.Fire * 0.7f : new Color(1f, 1f, 1f, 0.9f) * 1.6f, big ? 2.6f : 1.9f);
            if (big)
            {
                CbVfx.Ring(at, (fire ? CbFx.Fire : CbFx.Hit) * 0.35f, 1.4f, 0.3f);
                hud.ImpactFlash(0.45f);
                hud.SpeedLines(0.55f, 0.3f);
            }
            hud.Popup(new Vector3(e.x, e.HitHeight + 0.9f, 0), Mathf.RoundToInt(dmg).ToString(), big ? CbHud.PopStyle.Big : CbHud.PopStyle.Normal);
            // the biggest blows get an impact frame: a heavy finisher, or a blow on a boss that lands hard
            if (big && fromPlayer && (dmg >= 14f || (e.Big && dmg >= 10f)) && e.hp > 0) ImpactFrame();
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
            // a boss going down: the longest impact frame
            if (e.Big) { impactGap = 0; ImpactFrame(0.14f); }
            score += e.scoreValue;
            hitstop = Mathf.Max(hitstop, 0.06f);
            shake = Mathf.Max(shake, 0.4f);
            CbFx.Spark(new Vector3(e.x, e.HitHeight, -0.4f), e.burnT > 0 ? CbFx.Fire : CbFx.Purple, 26, 9, 0.6f, 0.6f, 2);
            CbVfx.Burst(new Vector3(e.x, e.HitHeight, -0.5f), CbFx.Hit, 3f, 0.25f);
            CbVfx.Ring(new Vector3(e.x, e.HitHeight, -0.5f), CbFx.Purple * 0.35f, 1.8f, 0.4f);
            hud.ImpactFlash(0.35f);
        }

        /// <summary>
        /// A soldier knocked into the water (or a pit): a big splash and he is gone, beaten - with a bonus, as it took
        /// aim (the user wanted more action: the bridge and the river banks are places to throw them off).
        /// </summary>
        public void EnemyFellIn(CbEnemy e)
        {
            bool water = CbLevel.InWater(e.x) && CbStage.Jungle;
            var at = new Vector3(e.x, water ? CbLevel.WaterLevel + 0.1f : -0.3f, -0.3f);
            if (water)
            {
                CbFx.Spark(at, CbFx.Water, 30, 8, 0.5f, 0.6f, 12);
                CbVfx.Ring(new Vector3(e.x, CbLevel.WaterLevel + 0.02f, 0), CbFx.Water * 0.5f, 2f, 0.45f, ground: true);
                CbFx.Sfx("splash");
            }
            else
            {
                CbFx.Puff(at, new Color(0.78f, 0.74f, 0.66f), 10, 2.2f, 1f, 0.6f);
                CbFx.Sfx("heavy", 0.7f);
            }
            if (!e.dead)
            {
                e.dead = true;
                score += e.scoreValue;
            }
            const int Bonus = 200;
            score += Bonus;
            combo++;
            comboT = 1.8f;
            maxCombo = Mathf.Max(maxCombo, combo);
            hud.SetCombo(combo);
            hud.Popup(new Vector3(e.x, 1.6f, 0), (water ? "SPLASH! +" : "DOWN THE PIT! +") + Bonus, CbHud.PopStyle.Big);
            shake = Mathf.Max(shake, 0.3f);
        }

        /// <summary>
        /// A beaten enemy, having lain there a moment, vanishes in a puff of smoke with a pop and a few stars; what he
        /// carried bursts out of it (the sergeant's purse: more coins and a mango).
        /// </summary>
        public void EnemyPoof(CbEnemy e)
        {
            var at = new Vector3(e.x, e.y + 0.7f, -0.4f);
            CbFx.Puff(at, new Color(0.96f, 0.95f, 0.92f), e.Big ? 22 : 14, e.Big ? 3f : 2.2f, e.Big ? 1.3f : 0.95f, 0.6f);
            CbFx.Puff(at + Vector3.up * 0.3f, new Color(0.82f, 0.8f, 0.86f), 6, 1.2f, 0.7f, 0.5f);
            CbFx.Spark(at, CbFx.Flame, 8, 5f, 0.22f, 0.5f, 2);
            CbVfx.Burst(at, CbFx.Hit, e.Big ? 2.2f : 1.4f, 0.16f);
            CbFx.Sfx("poof", 0.8f);
            items.Drop(new Vector3(e.x, e.y + 0.6f, 0));
            if (e.Big)
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
        /// The special's cut-in (the user's Sparking Zero): the band across the screen with Raja's eyes and the move's
        /// name, the fight slowed right down while it sweeps in (the gathering power runs on behind it).
        /// </summary>
        public void SuperCutIn(bool fire)
        {
            hud.CutIn(fire ? "TIGER FLAME CANNON!" : "STAFF STORM!", fire);
            hitstop = Mathf.Max(hitstop, CutInSlow);
            hud.SpeedLines(0.6f, CbHud.CutInTime);
            shake = Mathf.Max(shake, 0.12f);
        }
        const float CutInSlow = 0.5f;

        // ---------- a boss's cut-in: his own face, close up, from a second camera ----------

        Camera closeCam;
        RenderTexture closeRT;
        System.Func<(Vector3 from, Vector3 at)> closeAim;
        float closeT;

        /// <summary>
        /// A boss comes on: the cut-in's band with his name in red and his own face close up - a second camera's live
        /// picture of it, as he roars (on the LOW graphics level, the name alone).
        /// </summary>
        public void BossCutIn(CbEnemy e, string name) => BossCutIn(name, e != null && e.model != null ? () => FaceCloseUp(e.model) : null);

        /// <summary>The close-up camera at his face, a little to the side towards the view (a three-quarter look).</summary>
        static (Vector3 from, Vector3 at) FaceCloseUp(CbModel m)
        {
            var eyes = m.Eyes;
            var dir = (m.FaceDirection + Vector3.back * 0.7f).normalized;
            // (4.2 face lengths off showed his head and shoulders: closer, his eyes fill the band)
            return (eyes + dir * m.FaceLength * 3f, eyes);
        }

        /// <summary>A boss's cut-in, its close-up aimed (each frame, as it moves) where aim says; none, the name alone.</summary>
        public void BossCutIn(string name, System.Func<(Vector3 from, Vector3 at)> aim)
        {
            Texture face = null;
            if (aim != null && CbQuality.Current != CbQuality.Tier.Low && cam != null)
            {
                if (closeCam == null)
                {
                    closeRT = new RenderTexture(768, Mathf.RoundToInt(768 / CbHud.CloseUpAspect), 16) { name = "closeUp" };
                    var go = new GameObject("closeUp");
                    go.transform.SetParent(transform, false);
                    closeCam = go.AddComponent<Camera>();
                    closeCam.CopyFrom(cam);
                    closeCam.targetTexture = closeRT;
                    closeCam.fieldOfView = 26f;
                    closeCam.nearClipPlane = 0.05f;
                    closeCam.farClipPlane = 220f;
                    closeCam.depth = cam.depth - 1;
                    var data = closeCam.GetUniversalAdditionalCameraData();
                    if (data != null) data.renderPostProcessing = true;
                }
                closeAim = aim;
                closeT = CbHud.CutInTime;
                closeCam.enabled = true;
                AimCloseUp();
                face = closeRT;
            }
            hud.CutIn(name, CbArt.Hex("#e0302a"), CbArt.Hex("#3a0606"), face);
            shake = Mathf.Max(shake, 0.15f);
        }

        void AimCloseUp()
        {
            var (from, at) = closeAim();
            closeCam.transform.position = from;
            closeCam.transform.LookAt(at);
        }

        void UpdateCloseUp(float raw)
        {
            if (closeCam == null || !closeCam.enabled) return;
            closeT -= raw;
            if (closeT <= 0 || closeAim == null) { closeCam.enabled = false; return; }
            AimCloseUp();
        }

        /// <summary>The close-up camera is drawing a boss's face for the cut-in.</summary>
        public bool CloseUpOn => closeCam != null && closeCam.enabled;

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
            // (a frame counts at most a fifteenth of a second: on a phone below 20 frames a second the old cap of a
            // twentieth turned every slow frame into slow motion - the game "moving slowly": the user)
            float raw = TestStep > 0 ? TestStep : Mathf.Min(Time.unscaledDeltaTime, 1f / 15f);
            fpsT += Time.unscaledDeltaTime;
            fpsFrames++;
            if (fpsT >= 1f) { Fps = fpsFrames / fpsT; fpsT = 0; fpsFrames = 0; }
            clock += raw;
            UpdateImpactFrame(raw);
            WatchSpeed(raw);
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
            critters.Update(clock, camX, camY, hero.x, hero.y);
            UpdateCamera(raw);
            CbVfx.Update(cam, raw);
            UpdateCloseUp(raw);
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
            float gorge = mode == Mode.Play && level != null ? level.GorgeView(hero.x) : 0f;
            float back = Mathf.Clamp(Mathf.Max(needHalf / (dist * tanH), 1f + 0.12f * Mathf.Min(stand, 3f), Mathf.Lerp(1f, CamGorge, gorge)),
                1f, Mathf.Lerp(CamBackMax, CamGorge, gorge));
            if (mode != Mode.Play) back = 1f;
            // Tusk's clearing: drawn back to show all of it, trunk to trunk (following Raja, he charged in from off
            // the screen and rammed trees out of sight)
            bool tuskFight = mode == Mode.Play && CbStage.Treetops && lockZone != null && lockZone == BossZone;
            if (tuskFight) back = Mathf.Max(back, TuskFightBack);
            // the saw barge: drawn back a little, the whole barge and its barrels in view with the basin ahead of it
            bool bargeFight = mode == Mode.Play && CbStage.River && lockZone != null && lockZone == BossZone;
            if (bargeFight) back = Mathf.Max(back, BargeFightBack);
            // Grak's courtyard: drawn back like Tusk's clearing, pillar to pillar
            bool grakFight = mode == Mode.Play && CbStage.Ember && lockZone != null && lockZone == BossZone;
            if (grakFight) back = Mathf.Max(back, GrakFightBack);
            camBack += (back - camBack) * (1 - Mathf.Exp(-2f * dt));
            dist *= camBack;
            float tx = hero.x + hero.facing * LookAhead;
            // in the menu Raja stands on the right of the screen, the menu on the left, the camera a little closer
            if (mode == Mode.Title) { tx = hero.x - 3.4f + Mathf.Sin(clock * 0.2f) * 0.3f; dist *= 0.72f; }
            if (lockZone != null) tx = Mathf.Clamp(tx, lockZone.x - 3, lockZone.x + 3);
            if (tuskFight) tx = Mathf.Lerp(lockZone.x, hero.x, 0.15f);
            if (bargeFight) tx = Mathf.Lerp(lockZone.x - 2.5f, hero.x, 0.3f);
            if (grakFight) tx = Mathf.Lerp(lockZone.x, hero.x, 0.3f);
            // riding the river: the view well ahead, to see what is coming
            if (CbStage.River && Riding) tx = hero.x + RideLook;
            tx = Mathf.Lerp(tx, CbLevel.GorgeMiddle, gorge * 0.6f);
            // half his height on the path and in jumps; high up the mountain the camera climbs with him
            float ty = CamBase + Mathf.Max(hero.y * 0.5f, hero.y - 2.5f);
            // on the river the view follows it down the falls (and his jumps from his log, half)
            if (CbStage.River)
            {
                float floor = CbLevel.RiverFloor(hero.x);
                ty = CamBase + floor + Mathf.Max((hero.y - floor) * 0.5f, hero.y - floor - 2.5f);
            }
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
            input.atkDown = input.heavyDown = input.guard = input.jumpHeld = input.kiDown = false;
            hud.ReadTouch(input, dt);
            ReadKeys(input, dt);
            TestControls?.Invoke(input);
            OneButtonMoves(input, dt);
            // a move of the controls done: its hint goes for good
            if (input.slide) hud.Did(CbHud.Hint.StickDown);
            if (input.jump) hud.Did(CbHud.Hint.StickUp);
            if (input.dash) hud.Did(CbHud.Hint.SwipeAcross);
            if (input.heavy) hud.Did(CbHud.Hint.Hold);
            // a test's jumps are full jumps unless it says otherwise
            if (TestInput != null) input.jumpHeld = true;
            TestInput?.Invoke(input);
            // the stage cleared: he walks on through the shrine gate, nothing else
            if (exiting)
            {
                input.x = 0.55f; input.y = 0;
                input.jump = input.atk = input.heavy = input.dash = input.skill = input.kiTap = input.slide = false;
                input.atkDown = input.heavyDown = input.guard = input.jumpHeld = input.sprint = false;
            }
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

        // ATTACK held past HoldHeavy is the staff (HEAVY): the smash, and held on, the whirl. One button for both (the
        // user: too many buttons on a phone).
        const float HoldHeavy = 0.28f, HoldThrow = 0.28f;
        float attackHold, kiHold;
        bool holdFired, kiPending;

        /// <summary>
        /// The moves that share a control: ATTACK held is the staff.
        /// (Before a test's own input, which says the move itself.)
        /// </summary>
        void OneButtonMoves(CbInput inp, float dt)
        {
            // KI, once the staff throw is learned: held past HoldThrow it is the throw, let go before that the usual
            // KI move (so its press waits for the release)
            if (CbHero.StaffThrowLearned)
            {
                if (inp.kiTap) { kiHold = 0; kiPending = true; inp.kiTap = false; }
                if (kiPending)
                {
                    if (!inp.kiDown) { inp.kiTap = true; kiPending = false; }
                    else if ((kiHold += dt) >= HoldThrow) { inp.staffThrow = true; kiPending = false; }
                }
            }
            if (inp.atk) { attackHold = 0; holdFired = false; }
            else if (inp.atkDown) attackHold += dt;
            else { attackHold = 0; holdFired = false; }
            if (inp.atkDown && attackHold >= HoldHeavy)
            {
                if (!holdFired) { inp.heavy = true; holdFired = true; }
                inp.heavyDown = true;
            }
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

        /// <summary>Test hook: the game's time goes on this much each frame, whatever the real time (on CI's uneven
        /// frames, one slow frame used up a test's second of waiting while the game went on a twentieth of one).</summary>
        public float TestStep;

        /// <summary>Frames a second over the last second (shown on the pause screen, to see how the phone keeps up).</summary>
        public static float Fps { get; private set; }
        float fpsT;
        int fpsFrames;
        /// <summary>Test hook: presses the controls themselves (ATTACK held, SKILL), before they are read as moves.</summary>
        public System.Action<CbInput> TestControls;

        // a pad's stick (or its d-pad) pushed down: a slide, once per push
        bool padDown;

        /// <summary>
        /// The keyboard and a pad, laid out like the phone's controls: move, JUMP, ATTACK (held: the staff), SKILL,
        /// SPECIAL, a dodge, and down for a slide (the swipe down). Keyboard: A/D or arrows, Space, J or Z, K or X, I or
        /// V, L or Shift or C, S or down.
        /// </summary>
        void ReadKeys(CbInput inp, float dt)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) inp.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) inp.x += 1;
                if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) inp.slide = true;
                if (kb.spaceKey.wasPressedThisFrame) inp.jump = true;
                if (kb.spaceKey.isPressed) inp.jumpHeld = true;
                // W and up as the stick's up (jump; on vines, climb), S and down held as its down (low, a crawl)
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) { inp.jump = true; inp.stickJump = true; }
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) { inp.jumpHeld = true; inp.y += 1; }
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) inp.y -= 1;
                if (kb.jKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame) inp.atk = true;
                if (kb.jKey.isPressed || kb.zKey.isPressed) inp.atkDown = true;
                if (kb.kKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame) inp.kiTap = true;
                if (kb.kKey.isPressed || kb.xKey.isPressed) inp.kiDown = true;
                if (kb.iKey.wasPressedThisFrame || kb.vKey.wasPressedThisFrame) inp.skill = true;
                if (kb.lKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame) inp.dash = true;
                if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) inp.pause = true;
                if (kb.mKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) inp.menu = true;
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame) inp.confirm = true;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                var s = pad.leftStick.ReadValue();
                if (Mathf.Abs(s.x) > 0.2f) inp.x += s.x;
                if (pad.dpad.left.isPressed) inp.x -= 1;
                if (pad.dpad.right.isPressed) inp.x += 1;
                bool down = s.y < -0.6f || pad.dpad.down.isPressed;
                if (down && !padDown) inp.slide = true;
                padDown = down;
                if (pad.buttonSouth.wasPressedThisFrame) { inp.jump = true; inp.confirm = true; }
                if (pad.buttonSouth.isPressed) inp.jumpHeld = true;
                if (pad.buttonWest.wasPressedThisFrame) inp.atk = true;
                if (pad.buttonWest.isPressed) inp.atkDown = true;
                if (pad.buttonNorth.wasPressedThisFrame) inp.kiTap = true;
                if (pad.buttonNorth.isPressed) inp.kiDown = true;
                if (pad.buttonEast.wasPressedThisFrame) inp.dash = true;
                if (pad.rightShoulder.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame) inp.skill = true;
                if (pad.startButton.wasPressedThisFrame) inp.pause = true;
                if (pad.selectButton.wasPressedThisFrame) inp.menu = true;
            }
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inp.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inp.x += 1;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) inp.slide = true;
            if (Input.GetKeyDown(KeyCode.Space)) inp.jump = true;
            if (Input.GetKey(KeyCode.Space)) inp.jumpHeld = true;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { inp.jump = true; inp.stickJump = true; }
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) { inp.jumpHeld = true; inp.y += 1; }
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) inp.y -= 1;
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Z)) inp.atk = true;
            if (Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.Z)) inp.atkDown = true;
            if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.X)) inp.kiTap = true;
            if (Input.GetKey(KeyCode.K) || Input.GetKey(KeyCode.X)) inp.kiDown = true;
            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.V)) inp.skill = true;
            if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.C)) inp.dash = true;
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
            incoming.Clear();
            foreach (var e in enemies) e.f.Destroy();
            enemies.Clear();
            arenaFoes.Clear();
            lockZone = null;
            foreach (var p in posts) p.spawned = true;
            bossStarted = bossDone = true;
            foreach (var a in Ambushes) a.started = a.done = true;
            if (CbStage.Grove) trial = CbLevel.Trials;
            level?.OpenGoalGate(true);
            boss = null;
            hud.HideBoss();
        }

        /// <summary>Test hook: every fight on the way is won, and Sergeant Snap still waits at the gate.</summary>
        public void SkipToBoss()
        {
            SkipFights();
            bossStarted = bossDone = false;
            level?.OpenGoalGate(false);
        }
    }
}
