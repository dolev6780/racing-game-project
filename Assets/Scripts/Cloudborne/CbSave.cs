using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// What the game keeps between sessions (PlayerPrefs): banked tiger coins, the workshop's upgrades, whether the
    /// opening was seen, whether Raja's fire is awake, each stage's best result, and the settings.
    /// </summary>
    public static class CbSave
    {
        /// <summary>An upgrade bought in Baba's workshop: three levels, each dearer.</summary>
        public class Upgrade
        {
            public string id, name, desc;
            public int[] prices;
            public int Max => prices.Length;
        }

        public static readonly Upgrade[] Upgrades =
        {
            new Upgrade { id = "heart", name = "TIGER HEART", desc = "+20 max health", prices = new[] { 60, 150, 300 } },
            new Upgrade { id = "ember", name = "EMBER BREATH", desc = "Ki refills 40% faster", prices = new[] { 50, 120, 250 } },
            new Upgrade { id = "claws", name = "IRON CLAWS", desc = "Every blow hits 10% harder", prices = new[] { 80, 180, 350 } },
        };

        const string IntroKey = "cb_intro", FireKey = "cb_fire", VolumeKey = "cb_volume", PadsKey = "cb_pads", UpgradePrefix = "cb_upg_";

        public static int Coins
        {
            get => PlayerPrefs.GetInt(CbItems.BankKey, 0);
            set { PlayerPrefs.SetInt(CbItems.BankKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int Level(Upgrade u) => Mathf.Clamp(PlayerPrefs.GetInt(UpgradePrefix + u.id, 0), 0, u.Max);
        static int Level(int i) => Level(Upgrades[i]);

        /// <summary>The next level's price, or 0 when it is at its last level.</summary>
        public static int Price(Upgrade u) => Level(u) < u.Max ? u.prices[Level(u)] : 0;

        /// <summary>Buys the next level if there are coins enough. True if bought.</summary>
        public static bool Buy(Upgrade u)
        {
            int price = Price(u);
            if (price <= 0 || Coins < price) return false;
            Coins -= price;
            PlayerPrefs.SetInt(UpgradePrefix + u.id, Level(u) + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static float MaxHp => 100 + 20 * Level(0);
        public static float KiRegenMul => 1 + 0.4f * Level(1);
        public static float DamageMul => 1 + 0.1f * Level(2);

        public static bool IntroSeen
        {
            get => PlayerPrefs.GetInt(IntroKey, 0) == 1;
            set { PlayerPrefs.SetInt(IntroKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// Raja's fire is awake: he has touched the fire crystal (stage 1-5). Until then he has no powers - paws and the
        /// staff only, and his ki moves are staff techniques; only Baba knows what he is.
        /// </summary>
        public static bool FireAwake
        {
            // asked several times a frame (Raja's moves, the HUD): kept in memory after the first read
            get
            {
                if (fireAwake < 0) fireAwake = PlayerPrefs.GetInt(FireKey, 0);
                return fireAwake == 1;
            }
            set
            {
                fireAwake = value ? 1 : 0;
                PlayerPrefs.SetInt(FireKey, fireAwake);
                PlayerPrefs.Save();
            }
        }
        static int fireAwake = -1;

        // ---------- stages ----------

        static string Key(string stage, string what) => $"cb_{what}_{stage}";
        public static bool Cleared(string stage) => PlayerPrefs.GetInt(Key(stage, "clear"), 0) == 1;
        public static int Best(string stage) => PlayerPrefs.GetInt(Key(stage, "best"), 0);
        /// <summary>The fastest clear in seconds, 0 if never cleared.</summary>
        public static float BestTime(string stage) => PlayerPrefs.GetFloat(Key(stage, "time"), 0);

        /// <summary>Keeps a run's result: the best score, and on a clear the fastest time.</summary>
        public static void StageResult(string stage, bool won, int score, float time)
        {
            if (score > Best(stage)) PlayerPrefs.SetInt(Key(stage, "best"), score);
            if (won)
            {
                PlayerPrefs.SetInt(Key(stage, "clear"), 1);
                if (BestTime(stage) <= 0 || time < BestTime(stage)) PlayerPrefs.SetFloat(Key(stage, "time"), time);
            }
            PlayerPrefs.Save();
        }

        // ---------- settings ----------

        /// <summary>Sound effects volume, 0 to 1.</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 1f);
            set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); AudioListener.volume = Volume; }
        }

        /// <summary>The touch buttons: 0 shown on phones only (auto), 1 always, 2 never (a controller).</summary>
        public static int Pads
        {
            get => PlayerPrefs.GetInt(PadsKey, 0);
            set { PlayerPrefs.SetInt(PadsKey, ((value % 3) + 3) % 3); PlayerPrefs.Save(); }
        }

        /// <summary>Forgets coins, upgrades, the opening, the fire and every stage's results; settings stay.</summary>
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(CbItems.BankKey);
            PlayerPrefs.DeleteKey(IntroKey);
            PlayerPrefs.DeleteKey(FireKey);
            fireAwake = 0;
            foreach (var u in Upgrades) PlayerPrefs.DeleteKey(UpgradePrefix + u.id);
            foreach (var s in CbMenu.Stages)
                foreach (var what in new[] { "clear", "best", "time" }) PlayerPrefs.DeleteKey(Key(s.id, what));
            PlayerPrefs.Save();
        }
    }
}
