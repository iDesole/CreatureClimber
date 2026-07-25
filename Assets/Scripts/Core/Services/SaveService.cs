using UnityEngine;

namespace CreatureClimb
{
    /// <summary>Local save (high score, selections, settings).</summary>
    public static class SaveService
    {
        const string HighScoreKey = "CreatureClimb_HighScore";
        const string PrefCreatureIndex = "CreatureClimb_CreatureIndex";
        const string PrefPlatformIndex = "CreatureClimb_PlatformIndex";
        const string PrefBackgroundIndex = "CreatureClimb_BackgroundIndex";
        const string PrefMasterVolume = "CreatureClimb_MasterVolume";
        const string PrefSfxVolume = "CreatureClimb_SfxVolume";
        const string PrefMusicVolume = "CreatureClimb_MusicVolume";
        const string PrefHaptics = "CreatureClimb_Haptics";
        const string PrefGamesPlayed = "CreatureClimb_GamesPlayed";
        const string PrefLifetimeHeight = "CreatureClimb_LifetimeHeight";
        const string PrefCoins = "CreatureClimb_Coins";
        const string PrefUnlocks = "CreatureClimb_Unlocks";

        public static int HighScore
        {
            get => PlayerPrefs.GetInt(HighScoreKey, 0);
            set
            {
                PlayerPrefs.SetInt(HighScoreKey, Mathf.Max(0, value));
                Flush();
            }
        }

        public static int CreatureIndex
        {
            get => PlayerPrefs.GetInt(PrefCreatureIndex, 0);
            set
            {
                PlayerPrefs.SetInt(PrefCreatureIndex, Mathf.Max(0, value));
                Flush();
            }
        }

        public static int PlatformIndex
        {
            get => PlayerPrefs.GetInt(PrefPlatformIndex, 0);
            set
            {
                PlayerPrefs.SetInt(PrefPlatformIndex, Mathf.Max(0, value));
                Flush();
            }
        }

        public static int BackgroundIndex
        {
            get => PlayerPrefs.GetInt(PrefBackgroundIndex, 0);
            set
            {
                PlayerPrefs.SetInt(PrefBackgroundIndex, Mathf.Max(0, value));
                Flush();
            }
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(PrefMasterVolume, 1f);
            set
            {
                PlayerPrefs.SetFloat(PrefMasterVolume, Mathf.Clamp01(value));
                Flush();
            }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(PrefSfxVolume, 1f);
            set
            {
                PlayerPrefs.SetFloat(PrefSfxVolume, Mathf.Clamp01(value));
                Flush();
            }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(PrefMusicVolume, 0.55f);
            set
            {
                PlayerPrefs.SetFloat(PrefMusicVolume, Mathf.Clamp01(value));
                Flush();
            }
        }

        public static bool HapticsEnabled
        {
            get => PlayerPrefs.GetInt(PrefHaptics, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefHaptics, value ? 1 : 0);
                Flush();
            }
        }

        public static int GamesPlayed
        {
            get => PlayerPrefs.GetInt(PrefGamesPlayed, 0);
            set
            {
                PlayerPrefs.SetInt(PrefGamesPlayed, Mathf.Max(0, value));
                Flush();
            }
        }

        public static int LifetimeHeight
        {
            get => PlayerPrefs.GetInt(PrefLifetimeHeight, 0);
            set
            {
                PlayerPrefs.SetInt(PrefLifetimeHeight, Mathf.Max(0, value));
                Flush();
            }
        }

        public static bool TrySetHighScore(int score)
        {
            if (score <= HighScore) return false;
            HighScore = score;
            return true;
        }

        public static void RecordRun(int peakHeight)
        {
            GamesPlayed++;
            if (peakHeight > LifetimeHeight)
                LifetimeHeight = peakHeight;
        }

        public static int Coins
        {
            get => PlayerPrefs.GetInt(PrefCoins, 0);
            set
            {
                PlayerPrefs.SetInt(PrefCoins, Mathf.Max(0, value));
                Flush();
            }
        }

        public static void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
        }

        public static bool TrySpendCoins(int amount)
        {
            if (amount <= 0) return true;
            if (Coins < amount) return false;
            Coins -= amount;
            return true;
        }

        /// <summary>Unlock key format: "c:frog", "p:lilypad", "b:forest".</summary>
        public static bool IsUnlocked(string unlockKey)
        {
            if (string.IsNullOrEmpty(unlockKey)) return true;
            var raw = PlayerPrefs.GetString(PrefUnlocks, "");
            if (string.IsNullOrEmpty(raw)) return false;
            var parts = raw.Split('|');
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == unlockKey)
                    return true;
            }
            return false;
        }

        public static void Unlock(string unlockKey)
        {
            if (string.IsNullOrEmpty(unlockKey)) return;
            if (IsUnlocked(unlockKey)) return;
            var raw = PlayerPrefs.GetString(PrefUnlocks, "");
            raw = string.IsNullOrEmpty(raw) ? unlockKey : raw + "|" + unlockKey;
            PlayerPrefs.SetString(PrefUnlocks, raw);
            Flush();
        }

        public static string CreatureKey(string id) => "c:" + id;
        public static string PlatformKey(string id) => "p:" + id;
        public static string BackgroundKey(string id) => "b:" + id;

        public static void Flush()
        {
            PlayerPrefs.Save();
        }
    }
}
