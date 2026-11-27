using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Time trials (the user's ideas list: short runs against the clock for gold, silver or bronze, as in the games
    /// they named). A stretch of stage 1-1 between two glowing stones: crossing the first starts the clock (shown under
    /// the stage's title), reaching the second stops it - a medal for the time, the best kept. Going back past the start,
    /// or falling, ends the try; crossing the first stone again starts a new one.
    /// </summary>
    public class CbTrials
    {
        public class Trial
        {
            public string name;
            public float from, to, gold, silver, bronze;
            public float best;
            public Transform startStone, endStone;
        }

        /// <summary>Stage 1-1's trials: the vine ravine and the canopy (medal times to be tuned by playing).</summary>
        public static readonly (string name, float from, float to, float gold, float silver, float bronze)[] WaysTrials =
        {
            ("VINE RAVINE", 199.5f, 239.5f, 22f, 32f, 45f),
            ("CANOPY", 371f, 400.5f, 20f, 30f, 42f),
        };

        readonly CbGame g;
        readonly Transform root;
        readonly List<Trial> trials = new List<Trial>();
        Trial running;
        float clock, lastX;

        /// <summary>The trial running now (null: none).</summary>
        public Trial Running => running;
        /// <summary>The last medal won ("" none yet): GOLD, SILVER, BRONZE or "" for a time over bronze.</summary>
        public string LastMedal { get; private set; } = "";
        public IReadOnlyList<Trial> All => trials;

        public CbTrials(CbGame game, Transform parent)
        {
            g = game;
            root = new GameObject("Trials").transform;
            root.SetParent(parent, false);
            if (!CbStage.Jungle) return;
            foreach (var (name, from, to, gold, silver, bronze) in WaysTrials)
            {
                var tr = new Trial { name = name, from = from, to = to, gold = gold, silver = silver, bronze = bronze, best = PlayerPrefs.GetFloat(Key(name), 0f) };
                tr.startStone = Stone(from, "trialStart");
                tr.endStone = Stone(to, "trialEnd");
                trials.Add(tr);
            }
        }

        static string Key(string name) => "cb_trial_" + name.Replace(' ', '_').ToLowerInvariant();

        /// <summary>A glowing stone at the path's back edge: a ring of light on the ground in front of it.</summary>
        Transform Stone(float x, string name)
        {
            var t = CbArt.Joint(root, x, 0, 0.6f, name);
            var stone = CbArt.Toon("#8f9a8a", CbArt.PropOutline);
            CbArt.Part(CbMesh.Blob(10, 8), stone, t, new Vector3(0, 0.35f, 0.2f), new Vector3(0.55f, 0.75f, 0.45f));
            var glow = CbArt.Glow(new Color(1f, 0.85f, 0.35f, 0.8f), CbArt.Soft);
            CbArt.Part(CbMesh.Quad, glow, t, new Vector3(0, 0.03f, -0.6f), new Vector3(1.6f, 1.2f, 1), Quaternion.Euler(90, 0, 0), shadows: false);
            CbArt.Part(CbMesh.Quad, glow, t, new Vector3(0, 0.6f, -0.1f), new Vector3(1.1f, 1.1f, 1), shadows: false);
            return t;
        }

        public void Destroy() => Object.Destroy(root.gameObject);

        /// <summary>Each frame of play: a trial started, timed, won or given up.</summary>
        public void Update(float dt, CbHero hero)
        {
            float x = hero.x;
            if (running == null)
            {
                foreach (var tr in trials)
                    if (lastX < tr.from && x >= tr.from && hero.state != CbHero.State.Dead)
                    {
                        running = tr;
                        clock = 0;
                        g.hud.Banner("TRIAL: " + tr.name + " - GO!", true);
                        CbFx.Sfx("perfect", 0.5f);
                        break;
                    }
            }
            else
            {
                clock += dt;
                g.hud.SetTrial($"{running.name}   {clock:0.0}s   (gold {running.gold:0}s)");
                if (x >= running.to) Finish();
                else if (x < running.from - 3f || hero.state == CbHero.State.Dead || hero.state == CbHero.State.Swim)
                {
                    g.hud.Banner("TRIAL OVER - TRY AGAIN", true);
                    Stop();
                }
            }
            lastX = x;
        }

        void Finish()
        {
            var tr = running;
            string medal = clock <= tr.gold ? "GOLD" : clock <= tr.silver ? "SILVER" : clock <= tr.bronze ? "BRONZE" : "";
            LastMedal = medal;
            bool record = tr.best <= 0 || clock < tr.best;
            if (record) { tr.best = clock; PlayerPrefs.SetFloat(Key(tr.name), clock); PlayerPrefs.Save(); }
            g.hud.Banner((medal != "" ? medal + "!  " : "") + $"{tr.name} {clock:0.0}s" + (record ? "  NEW BEST" : ""), true);
            CbFx.Sfx(medal == "GOLD" ? "perfect" : "pickup", 0.8f);
            if (medal != "") g.score += medal == "GOLD" ? 1500 : medal == "SILVER" ? 800 : 400;
            Stop();
        }

        void Stop()
        {
            running = null;
            g.hud.SetTrial("");
        }

        /// <summary>Test hook: the clock of the trial running.</summary>
        public float Clock => clock;
    }
}
