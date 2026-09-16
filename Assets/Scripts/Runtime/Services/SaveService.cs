using System.Collections.Generic;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Local persistence (scores, loadout, settings).
    /// Writes are buffered; <see cref="Flush"/> hits disk. Auto-flushes on OS pause.
    /// </summary>
    public static class SaveService
    {
        const string HighScoreKey = "CreatureClimb_HighScore";
        const string PrefGameMode = "CreatureClimb_GameMode";
        const string PrefDifficulty = "CreatureClimb_Difficulty";
        const string PrefTimeTrialLength = "CreatureClimb_TimeTrialLength";
        const string PrefCreatureIndex = "CreatureClimb_CreatureIndex";
        const string PrefRaceOpponentIndex = "CreatureClimb_RaceOpponentIndex";
        const string PrefPlatformIndex = "CreatureClimb_PlatformIndex";
        const string PrefBackgroundIndex = "CreatureClimb_BackgroundIndex";
        const string PrefMasterVolume = "CreatureClimb_MasterVolume";
        const string PrefSfxVolume = "CreatureClimb_SfxVolume";
        const string PrefMusicVolume = "CreatureClimb_MusicVolume";
        const string PrefMusicEnabled = "CreatureClimb_MusicEnabled";
        const string PrefSfxEnabled = "CreatureClimb_SfxEnabled";
        const string PrefPlatformsVisible = "CreatureClimb_PlatformsVisible";
        const string PrefMirror = "CreatureClimb_Mirror";
        const string PrefHaptics = "CreatureClimb_Haptics";
        const string PrefGamesPlayed = "CreatureClimb_GamesPlayed";
        const string PrefLifetimeHeight = "CreatureClimb_LifetimeHeight";
        const string PrefCoins = "CreatureClimb_Coins";
        const string PrefUnlocks = "CreatureClimb_Unlocks";

        static bool dirty;
        static HashSet<string> unlockCache;
        static bool unlockCacheLoaded;

        /// <summary>Legacy / Classic Easy high score key (kept for existing saves).</summary>
        public static int HighScore
        {
            get => GetHighScore(GameMode.Classic, Difficulty.Easy);
            set => SetHighScore(GameMode.Classic, Difficulty.Easy, value);
        }

        public static GameMode SelectedGameMode
        {
            get => GameModeRules.ClampMode(PlayerPrefs.GetInt(PrefGameMode, (int)GameMode.Classic));
            set
            {
                PlayerPrefs.SetInt(PrefGameMode, (int)value);
                MarkDirty();
            }
        }

        public static Difficulty SelectedDifficulty
        {
            get
            {
                if (!PlayerPrefs.HasKey(PrefDifficulty))
                {
                    // Legacy: old Hard mode (enum value 1) → Hard difficulty.
                    var legacyMode = PlayerPrefs.GetInt(PrefGameMode, 0);
                    return legacyMode == 1 ? Difficulty.Hard : Difficulty.Easy;
                }

                return GameModeRules.ClampDifficulty(PlayerPrefs.GetInt(PrefDifficulty, 0));
            }
            set
            {
                PlayerPrefs.SetInt(PrefDifficulty, (int)value);
                MarkDirty();
            }
        }

        public static TimeTrialLength SelectedTimeTrialLength
        {
            get => GameModeRules.ClampTimeTrialLength(
                PlayerPrefs.GetInt(PrefTimeTrialLength, GameModeRules.TimeTrialSeconds(TimeTrialLength.Seconds90)));
            set
            {
                // Always persist real seconds (90/120/200), not enum ordinals.
                PlayerPrefs.SetInt(PrefTimeTrialLength, GameModeRules.TimeTrialSeconds(value));
                MarkDirty();
            }
        }

        static string HighScoreKeyFor(
            GameMode mode,
            Difficulty difficulty,
            TimeTrialLength timeTrialLength = TimeTrialLength.Seconds90)
        {
            var hard = difficulty == Difficulty.Hard;

            switch (mode)
            {
                case GameMode.TimeTrial:
                {
                    var secs = GameModeRules.TimeTrialSeconds(timeTrialLength);
                    // Fixed keys — no per-call string format for the common durations.
                    if (secs == 90) return hard ? "CreatureClimb_HighScore_TimeTrial_90_Hard" : "CreatureClimb_HighScore_TimeTrial_90_Easy";
                    if (secs == 120) return hard ? "CreatureClimb_HighScore_TimeTrial_120_Hard" : "CreatureClimb_HighScore_TimeTrial_120_Easy";
                    if (secs == 200) return hard ? "CreatureClimb_HighScore_TimeTrial_200_Hard" : "CreatureClimb_HighScore_TimeTrial_200_Easy";
                    return hard
                        ? $"CreatureClimb_HighScore_TimeTrial_{secs}_Hard"
                        : $"CreatureClimb_HighScore_TimeTrial_{secs}_Easy";
                }
                case GameMode.Race:
                    return hard ? "CreatureClimb_HighScore_Race_Hard" : "CreatureClimb_HighScore_Race_Easy";
                case GameMode.Combatant:
                    return hard ? "CreatureClimb_HighScore_Combatant_Hard" : "CreatureClimb_HighScore_Combatant_Easy";
                case GameMode.Blackout:
                    // Easy reuses old Storm key so Blackout/Storm scores carry over.
                    return hard ? "CreatureClimb_HighScore_Blackout_Hard" : "CreatureClimb_HighScore_Storm";
                case GameMode.Tempo:
                    return hard ? "CreatureClimb_HighScore_Tempo_Hard" : "CreatureClimb_HighScore_Tempo_Easy";
                default:
                    return hard ? "CreatureClimb_HighScore_Hard" : HighScoreKey;
            }
        }

        public static int RaceOpponentIndex
        {
            get => PlayerPrefs.GetInt(PrefRaceOpponentIndex, 0);
            set
            {
                PlayerPrefs.SetInt(PrefRaceOpponentIndex, Mathf.Max(0, value));
                MarkDirty();
            }
        }

        public static int GetHighScore(
            GameMode mode,
            Difficulty difficulty = Difficulty.Easy,
            TimeTrialLength timeTrialLength = TimeTrialLength.Seconds90) =>
            PlayerPrefs.GetInt(HighScoreKeyFor(mode, difficulty, timeTrialLength), 0);

        public static void SetHighScore(
            GameMode mode,
            Difficulty difficulty,
            int score,
            TimeTrialLength timeTrialLength = TimeTrialLength.Seconds90)
        {
            PlayerPrefs.SetInt(HighScoreKeyFor(mode, difficulty, timeTrialLength), Mathf.Max(0, score));
            // Progress must survive process death immediately.
            Flush();
        }

        public static bool TrySetHighScore(
            GameMode mode,
            Difficulty difficulty,
            int score,
            TimeTrialLength timeTrialLength = TimeTrialLength.Seconds90)
        {
            if (score <= GetHighScore(mode, difficulty, timeTrialLength)) return false;
            SetHighScore(mode, difficulty, score, timeTrialLength);
            return true;
        }

        /// <summary>Legacy overload — Classic Easy.</summary>
        public static bool TrySetHighScore(GameMode mode, int score) =>
            TrySetHighScore(mode, Difficulty.Easy, score);

        public static int CreatureIndex
        {
            get => PlayerPrefs.GetInt(PrefCreatureIndex, 0);
            set
            {
                PlayerPrefs.SetInt(PrefCreatureIndex, Mathf.Max(0, value));
                MarkDirty();
            }
        }

        public static int PlatformIndex
        {
            get => PlayerPrefs.GetInt(PrefPlatformIndex, 0);
            set
            {
                PlayerPrefs.SetInt(PrefPlatformIndex, Mathf.Max(0, value));
                MarkDirty();
            }
        }

        public static int BackgroundIndex
        {
            get => PlayerPrefs.GetInt(PrefBackgroundIndex, 0);
            set
            {
                PlayerPrefs.SetInt(PrefBackgroundIndex, Mathf.Max(0, value));
                MarkDirty();
            }
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(PrefMasterVolume, 1f);
            set
            {
                PlayerPrefs.SetFloat(PrefMasterVolume, Mathf.Clamp01(value));
                MarkDirty();
            }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(PrefSfxVolume, 1f);
            set
            {
                PlayerPrefs.SetFloat(PrefSfxVolume, Mathf.Clamp01(value));
                MarkDirty();
            }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(PrefMusicVolume, 0.55f);
            set
            {
                PlayerPrefs.SetFloat(PrefMusicVolume, Mathf.Clamp01(value));
                MarkDirty();
            }
        }

        public static bool MusicEnabled
        {
            get => PlayerPrefs.GetInt(PrefMusicEnabled, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefMusicEnabled, value ? 1 : 0);
                MarkDirty();
            }
        }

        public static bool SfxEnabled
        {
            get => PlayerPrefs.GetInt(PrefSfxEnabled, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefSfxEnabled, value ? 1 : 0);
                MarkDirty();
            }
        }

        public static bool PlatformsVisible
        {
            get => PlayerPrefs.GetInt(PrefPlatformsVisible, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefPlatformsVisible, value ? 1 : 0);
                MarkDirty();
            }
        }

        public static bool MirrorEnabled
        {
            get => PlayerPrefs.GetInt(PrefMirror, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefMirror, value ? 1 : 0);
                MarkDirty();
            }
        }

        public static bool HapticsEnabled
        {
            get => PlayerPrefs.GetInt(PrefHaptics, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefHaptics, value ? 1 : 0);
                MarkDirty();
            }
        }

        public static int GamesPlayed
        {
            get => PlayerPrefs.GetInt(PrefGamesPlayed, 0);
            set
            {
                PlayerPrefs.SetInt(PrefGamesPlayed, Mathf.Max(0, value));
                MarkDirty();
            }
        }

        public static int LifetimeHeight
        {
            get => PlayerPrefs.GetInt(PrefLifetimeHeight, 0);
            set
            {
                PlayerPrefs.SetInt(PrefLifetimeHeight, Mathf.Max(0, value));
                MarkDirty();
            }
        }

        public static bool TrySetHighScore(int score) =>
            TrySetHighScore(GameMode.Classic, Difficulty.Easy, score);

        public static void RecordRun(int peakHeight)
        {
            GamesPlayed++;
            if (peakHeight > LifetimeHeight)
                LifetimeHeight = peakHeight;
            Flush();
        }

        public static int Coins
        {
            get => PlayerPrefs.GetInt(PrefCoins, 0);
            set
            {
                PlayerPrefs.SetInt(PrefCoins, Mathf.Max(0, value));
                // Currency is durable — flush so kills mid-shop don't roll back purchases.
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
            EnsureUnlockCache();
            return unlockCache.Contains(unlockKey);
        }

        public static void Unlock(string unlockKey)
        {
            if (string.IsNullOrEmpty(unlockKey)) return;
            EnsureUnlockCache();
            if (!unlockCache.Add(unlockKey)) return;

            var raw = PlayerPrefs.GetString(PrefUnlocks, "");
            raw = string.IsNullOrEmpty(raw) ? unlockKey : raw + "|" + unlockKey;
            PlayerPrefs.SetString(PrefUnlocks, raw);
            Flush();
        }

        public static string CreatureKey(string id) => "c:" + id;
        public static string PlatformKey(string id) => "p:" + id;
        public static string BackgroundKey(string id) => "b:" + id;

        static void EnsureUnlockCache()
        {
            if (unlockCacheLoaded && unlockCache != null) return;

            unlockCache = new HashSet<string>();
            unlockCacheLoaded = true;
            var raw = PlayerPrefs.GetString(PrefUnlocks, "");
            if (string.IsNullOrEmpty(raw)) return;

            var parts = raw.Split('|');
            for (var i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                    unlockCache.Add(parts[i]);
            }
        }

        static void MarkDirty()
        {
            dirty = true;
        }

        /// <summary>
        /// Writes buffered prefs to disk. Cheap no-op when nothing changed.
        /// Call on OS pause / quit; progress writers call this themselves.
        /// </summary>
        public static void Flush()
        {
            if (!dirty) return;
            PlayerPrefs.Save();
            dirty = false;
        }

        /// <summary>Force a disk write even if the dirty flag was missed (shutdown path).</summary>
        public static void ForceFlush()
        {
            PlayerPrefs.Save();
            dirty = false;
        }
    }
}
