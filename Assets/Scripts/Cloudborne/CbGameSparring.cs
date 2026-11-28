using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Baba's sparring (the user's ideas list: a challenge room, after The Lost Crown's; the user chose stage 1-3 for
    /// it): after Baba's three trials, a big bell stands before the grove's way out. Struck, the way shuts either side and
    /// soldiers come at Raja in three waves while a clock runs (shown under the stage's title); beating them all wins
    /// gold, silver or bronze by the time and coins for it, the best time kept. It is there to take or to walk past; once
    /// won it stays quiet for the run. Falling in the fight ends it (struck again, it starts over).
    /// </summary>
    public partial class CbGame
    {
        /// <summary>Where the bell stands, and the middle of the fight it starts.</summary>
        public const float SparBellX = 121.5f, SparX = 117.5f;
        /// <summary>The times for gold, silver and bronze (guesses: to be tuned by playing).</summary>
        public const float SparGold = 40f, SparSilver = 60f, SparBronze = 90f;
        static readonly int[] SparCoins = { 100, 60, 30 };
        const string SparKey = "cb_sparring_best";
        static readonly Zone SparZone = new Zone { x = SparX, label = "Baba's sparring", spawn = new (CbEnemy.Kind, float)[0] };
        static readonly (CbEnemy.Kind kind, float dx)[][] SparWaves =
        {
            new[] { (CbEnemy.Kind.Hyena, -8f), (CbEnemy.Kind.Hyena, 8f), (CbEnemy.Kind.Hyena, 9.5f) },
            new[] { (CbEnemy.Kind.Shield, 8f), (CbEnemy.Kind.Hyena, -8f), (CbEnemy.Kind.Thrower, 9.5f) },
            new[] { (CbEnemy.Kind.Shield, -8f), (CbEnemy.Kind.Hyena, 8f), (CbEnemy.Kind.Hyena, -9.5f), (CbEnemy.Kind.Thrower, 9.5f) },
        };

        Transform sparBell;
        Material sparGlow;
        int sparWave;
        float sparT;
        /// <summary>The sparring's state this run: running, won (and with which medal: "" none yet).</summary>
        public bool Sparring => lockZone == SparZone;
        public bool SparDone { get; private set; }
        public string SparMedal { get; private set; } = "";
        public float SparClock => sparT;
        /// <summary>The best time ever in Baba's sparring (0: never won).</summary>
        public static float SparBest => PlayerPrefs.GetFloat(SparKey, 0f);

        /// <summary>The bell, built with stage 1-3 (on the world's root, so it goes with the stage).</summary>
        void MakeSparBell()
        {
            sparBell = null;
            if (!CbStage.Grove || world == null) return;
            var t = CbArt.Joint(world.root, SparBellX, 0, 0.7f, "sparBell");
            var model = CbArt.Prop("Cloudborne/Grove/Bell/Bell", t, CbArt.PropOutline);
            if (model != null) model.localScale *= 1.25f;
            else
            {
                var bamboo = CbArt.Toon("#c9a45a", CbArt.PropOutline);
                CbArt.Cylinder(bamboo, t, new Vector3(0.6f, 1.1f, 0), 0.06f, 2.2f);
                CbArt.Part(CbMesh.Blob(10, 8), CbArt.Toon("#c98a2e", CbArt.PropOutline), t, new Vector3(0, 1.4f, 0), new Vector3(0.6f, 0.7f, 0.6f));
            }
            // a red glow on the ground in front of it: a challenge, not one of the lesson's bells
            sparGlow = CbArt.Glow(new Color(1f, 0.3f, 0.2f, 0.7f), CbArt.Soft);
            CbArt.Part(CbMesh.Quad, sparGlow, t, new Vector3(0, 0.03f, -0.8f), new Vector3(2.2f, 1.5f, 1), Quaternion.Euler(90, 0, 0), shadows: false);
            sparBell = t;
        }

        /// <summary>Each frame of play in stage 1-3: the bell struck, the waves, the clock, the medal.</summary>
        void UpdateSparring(float dt)
        {
            if (!CbStage.Grove || sparBell == null) return;
            if (sparGlow != null) sparGlow.color = new Color(1f, 0.3f, 0.2f, SparDone ? 0.15f : 0.45f + 0.25f * Mathf.Sin(time * 3f));
            if (lockZone == null && !SparDone && trial >= CbLevel.Trials && Mathf.Abs(hero.x - SparBellX) < 1.4f
                && (hero.state == CbHero.State.Atk || hero.state == CbHero.State.Staff || hero.state == CbHero.State.Kick))
            {
                lockZone = SparZone;
                arenaFoes.Clear();
                sparWave = 1;
                sparT = 0;
                hud.ShowGo(false);
                hud.Banner("BABA'S SPARRING - GO!");
                hud.SetTip("Baba: \"Show me what you learned - and quickly!\"");
                CbFx.Sfx("heavy", 0.6f);
                shake = Mathf.Max(shake, 0.25f);
                SpawnFoes(SparWaves[0]);
            }
            if (lockZone != SparZone) return;
            sparT += dt;
            hud.SetTrial($"SPARRING   {sparT:0.0}s   (gold {SparGold:0}s)");
            if (!ArenaClear) return;
            if (sparWave < SparWaves.Length)
            {
                sparWave++;
                hud.Banner(sparWave == SparWaves.Length ? "THE LAST OF THEM!" : "MORE OF THEM!", true);
                SpawnFoes(SparWaves[sparWave - 1]);
                return;
            }
            // won: the medal for the time, coins for it, the best kept
            lockZone = null;
            SparDone = true;
            hud.SetTrial("");
            hud.ShowGo(true);
            int m = sparT <= SparGold ? 0 : sparT <= SparSilver ? 1 : sparT <= SparBronze ? 2 : 3;
            SparMedal = m == 0 ? "GOLD" : m == 1 ? "SILVER" : m == 2 ? "BRONZE" : "";
            bool record = SparBest <= 0 || sparT < SparBest;
            if (record) { PlayerPrefs.SetFloat(SparKey, sparT); PlayerPrefs.Save(); }
            int coins = m < 3 ? SparCoins[m] : 10;
            CbSave.Coins += coins;
            hud.Banner((SparMedal != "" ? SparMedal + "!  " : "") + $"SPARRING {sparT:0.0}s  +{coins} COINS" + (record ? "  NEW BEST" : ""), true);
            hud.SetTip(m == 0 ? "Baba: \"Ha! Not bad at all, little tiger.\"" : "Baba: \"Faster next time!\"");
            CbFx.Sfx(m == 0 ? "perfect" : "pickup", 0.8f);
            score += m == 0 ? 1500 : m == 1 ? 800 : m == 2 ? 400 : 100;
        }

        /// <summary>Falling in the fight: the sparring is called off (the bell can be struck again).</summary>
        void SparringFallen()
        {
            if (lockZone != SparZone) return;
            hud.SetTrial("");
            sparWave = 0;
        }

        /// <summary>A new run: the bell ready again.</summary>
        void ResetSparring()
        {
            SparDone = false;
            SparMedal = "";
            sparWave = 0;
            sparT = 0;
        }

        /// <summary>Test hook: Raja strikes the bell (as an attack in front of it does).</summary>
        public void StrikeSparBell()
        {
            hero.x = SparBellX - 0.6f;
            hero.state = CbHero.State.Atk;
            UpdateSparring(0f);
        }
    }
}
