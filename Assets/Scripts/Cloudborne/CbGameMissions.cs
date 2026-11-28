using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Hidden missions (the user's ideas list, after Kirby and the Forgotten Land's): three in each stage, "???" until
    /// done. Clearing the stage having done one reveals it on the results ("NEW!"), and it stays done (PlayerPrefs); the
    /// stages page shows how many of each stage's three are done.
    /// </summary>
    public partial class CbGame
    {
        public class Mission
        {
            public string stage, text;
            public System.Func<CbGame, bool> done;
        }

        /// <summary>Each stage's three missions, checked when it is cleared.</summary>
        public static readonly Mission[] Missions =
        {
            new Mission { stage = "1-1", text = "Find all four R-A-J-A letters", done = g => g.LettersFound >= 4 },
            new Mission { stage = "1-1", text = "Break a soldier's guard", done = g => g.SoldierBreaks >= 1 },
            new Mission { stage = "1-1", text = "Win gold in a time trial", done = g => g.TrialGold },
            new Mission { stage = "1-2", text = "Find the treetops' secret", done = g => g.Secrets >= 1 },
            new Mission { stage = "1-2", text = "Collect all three tiger tokens", done = g => g.Tokens >= 3 },
            new Mission { stage = "1-2", text = "Land a combo of 30", done = g => g.maxCombo >= 30 },
            new Mission { stage = "1-3", text = "Clear the grove without a hit", done = g => !g.hurtOnce },
            new Mission { stage = "1-3", text = "Collect all three tiger tokens", done = g => g.Tokens >= 3 },
            new Mission { stage = "1-3", text = "Win gold in Baba's sparring", done = g => g.SparMedal == "GOLD" },
            new Mission { stage = "1-4", text = "Collect all three golden lotuses", done = g => g.Tokens >= 3 },
            new Mission { stage = "1-4", text = "Ride the river without a hit", done = g => !g.hurtOnce },
            new Mission { stage = "1-4", text = "Clear the river in under 4 minutes", done = g => g.time < 240f },
            new Mission { stage = "1-5", text = "Defeat Grak without a hit", done = g => !g.hurtOnce },
            new Mission { stage = "1-5", text = "Break a soldier's guard", done = g => g.SoldierBreaks >= 1 },
            new Mission { stage = "1-5", text = "Land a combo of 40", done = g => g.maxCombo >= 40 },
        };

        /// <summary>A gold medal won in a time trial this run (CbTrials).</summary>
        public bool TrialGold;

        // (kept by the stage and the mission's place in it)
        static string MissionKey(string stage, int nth) => $"cb_mission_{stage}_{nth}";

        /// <summary>Whether the nth mission of a stage has been done.</summary>
        public static bool MissionDone(string stage, int nth) => PlayerPrefs.GetInt(MissionKey(stage, nth), 0) == 1;

        /// <summary>How many of a stage's missions have been done.</summary>
        public static int MissionsDone(string stage)
        {
            int n = 0, nth = 0;
            foreach (var m in Missions)
                if (m.stage == stage) { if (MissionDone(stage, nth)) n++; nth++; }
            return n;
        }

        /// <summary>All the missions forgotten (a fresh start: CbSave.ResetProgress).</summary>
        public static void ForgetMissions()
        {
            foreach (var s in CbMenu.Stages)
                for (int nth = 0; nth < 3; nth++) PlayerPrefs.DeleteKey(MissionKey(s.id, nth));
        }

        /// <summary>
        /// The stage cleared: its missions done this run are kept. The results' lines: each done one in gold (NEW! for
        /// one done just now), the rest "???".
        /// </summary>
        string CheckMissions(bool won)
        {
            var lines = new System.Text.StringBuilder("<color=#ffd34a>MISSIONS</color>");
            int nth = 0;
            foreach (var m in Missions)
            {
                if (m.stage != CbStage.Id) continue;
                bool was = MissionDone(m.stage, nth), now = won && m.done(this);
                if (now && !was) PlayerPrefs.SetInt(MissionKey(m.stage, nth), 1);
                lines.Append('\n').Append(now && !was ? $"<color=#ffd34a>★ {m.text}  NEW!</color>" : was || now ? $"<color=#ffe7a0>★ {m.text}</color>" : "<color=#9a978f>· ???</color>");
                nth++;
            }
            PlayerPrefs.Save();
            return lines.ToString();
        }
    }
}
