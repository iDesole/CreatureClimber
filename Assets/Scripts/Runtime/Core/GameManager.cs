using System;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Runs the climb. Always loads Creature / Platform / Background databases for textures.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Play as (Creature Database)")]
        [SerializeField] CreatureDatabase creatureDatabase;
        [SerializeField] int playAsCreatureIndex;

        [Header("Platforms (Platform Database)")]
        [SerializeField] PlatformDatabase platformDatabase;
        [SerializeField] int activePlatformIndex;

        [Header("Backgrounds (Background Database)")]
        [SerializeField] BackgroundDatabase backgroundDatabase;
        [SerializeField] int activeBackgroundIndex;

        [Header("Scene refs")]
        [SerializeField] LeafSpawner leafSpawner;
        [SerializeField] PlayerController player;
        [SerializeField] BackgroundScroller backgroundScroller;
        [SerializeField] GameUI gameUI;

        public GameState State { get; private set; } = GameState.Ready;
        public int Score { get; private set; }
        public int PeakScore { get; private set; }
        public int HighScore { get; private set; }

        public CreatureDatabase Creatures => creatureDatabase;
        public PlatformDatabase Platforms => platformDatabase;
        public BackgroundDatabase Backgrounds => backgroundDatabase;
        public int PlayAsCreatureIndex => playAsCreatureIndex;
        public int ActivePlatformIndex => activePlatformIndex;
        public int ActiveBackgroundIndex => activeBackgroundIndex;
        public Creature SelectedCreature { get; private set; }
        public Platform SelectedPlatform { get; private set; }
        public Background SelectedBackground { get; private set; }
        public GameMode SelectedMode { get; private set; } = GameMode.Classic;
        public Difficulty SelectedDifficulty { get; private set; } = Difficulty.Easy;
        public TimeTrialLength SelectedTimeTrialLength { get; private set; } = TimeTrialLength.Seconds90;
        public float TimeTrialRemaining { get; private set; }
        /// <summary>False until the player commits their first jump; clock stays full until then.</summary>
        public bool TimeTrialClockRunning { get; private set; }
        public int RaceOpponentIndex { get; private set; }
        public bool LastRacePlayerWon { get; private set; }
        public bool LastRunTimedOut { get; private set; }

        public bool IsWired =>
            leafSpawner != null &&
            player != null &&
            gameUI != null;

        float creatureScoreMul = 1f;
        bool started;
        GameState stateBeforePause = GameState.Playing;
        float timeScaleBeforePause = 1f;
        /// <summary>Blocks double-triggering the same death hit in one frame.</summary>
        bool deathHitLatched;

        public event Action<GameState> OnStateChanged;
        public event Action<int> OnScoreChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            PlatformRuntime.Initialize();
            LoadSelectionPrefs();
            HighScore = ResolveHighScore();
        }

        void Start()
        {
            if (!EnsureWired())
            {
                Debug.LogError("[GameManager] Missing scene references. Menu: Creature Climb → Build Game Scene");
                enabled = false;
                return;
            }

            // Force database textures every play.
            ReloadDatabasesAndSelections();
            EnterReadyState();
            started = true;
        }

        /// <summary>
        /// Loads Resources databases and applies sprites to player / platforms / background.
        /// </summary>
        public void ReloadDatabasesAndSelections()
        {
            var creatures = CreatureDatabase.Load();
            var platforms = PlatformDatabase.Load();
            var backgrounds = BackgroundDatabase.Load();

            if (creatures != null) creatureDatabase = creatures;
            if (platforms != null) platformDatabase = platforms;
            if (backgrounds != null) backgroundDatabase = backgrounds;

            LoadSelectionPrefs();
            ApplySelectedCreature();
            ApplySelectedPlatforms();
            ApplyBackgroundDatabase();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[GameManager] Using databases — " +
                $"Creatures={creatureDatabase?.Count ?? 0}, " +
                $"Platforms={platformDatabase?.Count ?? 0}, " +
                $"Backgrounds={backgroundDatabase?.Count ?? 0}");
#endif
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = 1f;
        }

        void OnValidate()
        {
            if (creatureDatabase != null && creatureDatabase.Count > 0)
                playAsCreatureIndex = Mathf.Clamp(playAsCreatureIndex, 0, creatureDatabase.Count - 1);

            if (platformDatabase != null && platformDatabase.Count > 0)
                activePlatformIndex = Mathf.Clamp(activePlatformIndex, 0, platformDatabase.Count - 1);

            if (backgroundDatabase != null && backgroundDatabase.Count > 0)
                activeBackgroundIndex = Mathf.Clamp(activeBackgroundIndex, 0, backgroundDatabase.Count - 1);
        }

        public void Wire(
            LeafSpawner leaves,
            PlayerController playerController,
            GameUI ui,
            CreatureDatabase creatures = null,
            PlatformDatabase platforms = null,
            BackgroundDatabase backgrounds = null,
            BackgroundScroller background = null)
        {
            leafSpawner = leaves;
            player = playerController;
            gameUI = ui;
            if (background != null)
                backgroundScroller = background;

            creatureDatabase = creatures != null ? creatures : CreatureDatabase.Load();
            platformDatabase = platforms != null ? platforms : PlatformDatabase.Load();
            backgroundDatabase = backgrounds != null ? backgrounds : BackgroundDatabase.Load();

            if (player != null && leafSpawner != null)
                player.Bind(leafSpawner);

            LoadSelectionPrefs();
            ApplySelectedCreature();
            ApplySelectedPlatforms();
            ApplyBackgroundDatabase();
        }

        public bool EnsureWired()
        {
            if (leafSpawner == null) leafSpawner = FindFirstObjectByType<LeafSpawner>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (backgroundScroller == null) backgroundScroller = FindFirstObjectByType<BackgroundScroller>();
            if (gameUI == null) gameUI = FindFirstObjectByType<GameUI>();

            if (IsWired && player != null && leafSpawner != null)
                player.Bind(leafSpawner);

            return IsWired;
        }

        public void SetPlayAs(int index)
        {
            playAsCreatureIndex = index;
            SaveSelectionPrefs();
            ApplySelectedCreature();
            FeedbackService.UiSelect();
            if (State == GameState.Ready)
            {
                player?.ResetToStart();
                RefreshReadyUi();
            }
        }

        public void SetActivePlatform(int index)
        {
            activePlatformIndex = index;
            SaveSelectionPrefs();
            ApplySelectedPlatforms();
            FeedbackService.UiSelect();
            if (State == GameState.Ready || State == GameState.GameOver)
            {
                leafSpawner?.Initialize();
                player?.ResetToStart();
            }

            if (State == GameState.Ready)
                RefreshReadyUi();
        }

        public void SetActiveBackground(int index)
        {
            activeBackgroundIndex = index;
            SaveSelectionPrefs();
            ApplyBackgroundDatabase();
            FeedbackService.UiSelect();
            if (State == GameState.Ready)
            {
                backgroundScroller?.ResetToOrigin();
                RefreshReadyUi();
            }
        }

        public void OpenCustomize()
        {
            if (State != GameState.Ready && State != GameState.GameOver)
                return;
            gameUI?.OpenCustomize();
        }

        public void OpenShop()
        {
            if (State != GameState.Ready && State != GameState.GameOver)
                return;
            gameUI?.OpenShop();
        }

        public void OpenGameMode()
        {
            if (State != GameState.Ready && State != GameState.GameOver)
                return;
            gameUI?.OpenGameMode();
        }

        public void OpenSettings()
        {
            if (State != GameState.Ready && State != GameState.GameOver)
                return;
            gameUI?.OpenSettings();
        }

        /// <summary>Rebuild the ready-world after Mirror / platform-visibility changes.</summary>
        public void ApplySettingsPreview()
        {
            AudioService.Instance?.ApplyVolumes();
            if (State != GameState.Ready && State != GameState.GameOver)
                return;
            if (!EnsureWired()) return;
            leafSpawner?.Initialize();
            leafSpawner?.ApplyPlatformVisibility();
            player?.ResetToStart();
            backgroundScroller?.ResetToOrigin();
        }

        public void OnSettingsClosed()
        {
            ApplySettingsPreview();
            if (State == GameState.Ready)
                RefreshReadyUi();
            else if (State == GameState.GameOver)
                gameUI?.ShowGameOver(Mathf.Max(Score, PeakScore), HighScore, GetGameOverTitle());
        }

        public void SetGameMode(GameMode mode)
        {
            SelectedMode = mode;
            SaveService.SelectedGameMode = mode;
            ApplyModeDifficultyToRun();
        }

        public void SetDifficulty(Difficulty difficulty)
        {
            SelectedDifficulty = difficulty;
            SaveService.SelectedDifficulty = difficulty;
            ApplyModeDifficultyToRun();
        }

        public void SetTimeTrialLength(TimeTrialLength length)
        {
            SelectedTimeTrialLength = GameModeRules.ClampTimeTrialLength(
                GameModeRules.TimeTrialSeconds(length));
            SaveService.SelectedTimeTrialLength = SelectedTimeTrialLength;
            ApplyModeDifficultyToRun();
        }

        int ResolveHighScore() =>
            GameModeRules.IsTimeTrial(SelectedMode)
                ? SaveService.GetHighScore(SelectedMode, SelectedDifficulty, SelectedTimeTrialLength)
                : SaveService.GetHighScore(SelectedMode, SelectedDifficulty);

        public void SetRaceOpponentIndex(int index)
        {
            if (creatureDatabase == null || creatureDatabase.Count == 0)
            {
                RaceOpponentIndex = 0;
                SaveService.RaceOpponentIndex = 0;
                return;
            }

            RaceOpponentIndex = Mathf.Clamp(index, 0, creatureDatabase.Count - 1);
            SaveService.RaceOpponentIndex = RaceOpponentIndex;
            FeedbackService.UiSelect();
        }

        public void CycleRaceOpponent(int delta)
        {
            if (creatureDatabase == null || creatureDatabase.Count == 0) return;
            var next = (RaceOpponentIndex + delta) % creatureDatabase.Count;
            if (next < 0) next += creatureDatabase.Count;
            SetRaceOpponentIndex(next);
        }

        public Creature GetRaceOpponent()
        {
            if (creatureDatabase == null || creatureDatabase.Count == 0) return null;
            var i = Mathf.Clamp(RaceOpponentIndex, 0, creatureDatabase.Count - 1);
            return creatureDatabase.GetCreature(i);
        }

        void ApplyModeDifficultyToRun()
        {
            HighScore = ResolveHighScore();
            leafSpawner?.SetGameMode(SelectedMode, SelectedDifficulty);
            FeedbackService.UiSelect();

            if (State == GameState.Ready)
            {
                leafSpawner?.Initialize();
                player?.ResetToStart();
                RefreshReadyUi();
            }
            else if (State == GameState.GameOver)
            {
                gameUI?.ShowGameOver(Mathf.Max(Score, PeakScore), HighScore, GetGameOverTitle());
            }
        }

        public void OnCustomizeClosed()
        {
            // Return to main menu; world already shows the selected loadout.
            if (State == GameState.Ready)
                RefreshReadyUi();
            else if (State == GameState.GameOver)
                gameUI?.ShowGameOver(Mathf.Max(Score, PeakScore), HighScore, GetGameOverTitle());
        }

        public void OnShopClosed()
        {
            if (State == GameState.Ready)
                RefreshReadyUi();
            else if (State == GameState.GameOver)
                gameUI?.ShowGameOver(Mathf.Max(Score, PeakScore), HighScore, GetGameOverTitle());
        }

        public void OnGameModeClosed()
        {
            if (State == GameState.Ready)
                RefreshReadyUi();
            else if (State == GameState.GameOver)
                gameUI?.ShowGameOver(Mathf.Max(Score, PeakScore), HighScore, GetGameOverTitle());
        }

        public void CycleCreature(int delta)
        {
            if (creatureDatabase == null || creatureDatabase.Count == 0) return;
            var next = (playAsCreatureIndex + delta) % creatureDatabase.Count;
            if (next < 0) next += creatureDatabase.Count;
            SetPlayAs(next);
        }

        public void CyclePlatform(int delta)
        {
            if (platformDatabase == null || platformDatabase.Count == 0) return;
            var next = (activePlatformIndex + delta) % platformDatabase.Count;
            if (next < 0) next += platformDatabase.Count;
            SetActivePlatform(next);
        }

        public void CycleBackground(int delta)
        {
            if (backgroundDatabase == null || backgroundDatabase.Count == 0) return;
            var next = (activeBackgroundIndex + delta) % backgroundDatabase.Count;
            if (next < 0) next += backgroundDatabase.Count;
            SetActiveBackground(next);
        }

        public void ApplySelectionsNow()
        {
            ReloadDatabasesAndSelections();
            SaveSelectionPrefs();
            if (State == GameState.Ready || State == GameState.GameOver || !started)
            {
                leafSpawner?.Initialize();
                player?.ResetToStart();
                if (State == GameState.Ready || !started)
                    RefreshReadyUi();
            }
        }

        void LoadSelectionPrefs()
        {
            SelectedMode = SaveService.SelectedGameMode;
            SelectedDifficulty = SaveService.SelectedDifficulty;
            SelectedTimeTrialLength = SaveService.SelectedTimeTrialLength;
            playAsCreatureIndex = SaveService.CreatureIndex;
            activePlatformIndex = SaveService.PlatformIndex;
            activeBackgroundIndex = SaveService.BackgroundIndex;
            RaceOpponentIndex = SaveService.RaceOpponentIndex;
            if (playAsCreatureIndex < 0) playAsCreatureIndex = 0;
            if (activePlatformIndex < 0) activePlatformIndex = 0;
            if (activeBackgroundIndex < 0) activeBackgroundIndex = 0;
            if (RaceOpponentIndex < 0) RaceOpponentIndex = 0;
            leafSpawner?.SetGameMode(SelectedMode, SelectedDifficulty);
            HighScore = ResolveHighScore();
        }

        void SaveSelectionPrefs()
        {
            SaveService.SelectedGameMode = SelectedMode;
            SaveService.SelectedDifficulty = SelectedDifficulty;
            SaveService.SelectedTimeTrialLength = SelectedTimeTrialLength;
            SaveService.CreatureIndex = playAsCreatureIndex;
            SaveService.PlatformIndex = activePlatformIndex;
            SaveService.BackgroundIndex = activeBackgroundIndex;
            SaveService.RaceOpponentIndex = RaceOpponentIndex;
        }

        void RefreshReadyUi()
        {
            gameUI?.ShowReady(HighScore);
        }

        void Update()
        {
            if (!started || !IsWired) return;

            if (State == GameState.Playing || State == GameState.Falling || State == GameState.Paused)
            {
                if (TapInput.WasPausePressed())
                {
                    TogglePause();
                    return;
                }
            }

            if (State == GameState.Paused)
            {
                // Resume only via Resume button / Esc / Space — not screen-side taps (Menu is there).
                if (TapInput.WasConfirmPressed() || TapInput.WasPausePressed())
                {
                    PauseGame(false);
                    return;
                }

                return;
            }

            TickTimeTrial();

            if (State == GameState.Ready)
            {
                // Main menu / overlays own input — start only via Play (or keyboard confirm).
                if (gameUI != null &&
                    gameUI.IsOverlayOpen)
                    return;

                if (TapInput.WasConfirmPressed())
                {
                    StartGame();
                    return;
                }

                return;
            }

            if (gameUI != null && gameUI.IsOverlayOpen)
                return;

            if (!TapInput.TryGetTapSide(out var side)) return;

            switch (State)
            {
                case GameState.Playing:
                    player.TryHandleTap(side);
                    break;
                case GameState.GameOver:
                    Restart();
                    break;
            }
        }

        int lastTimerWholeSeconds = -1;

        void TickTimeTrial()
        {
            if (!GameModeRules.IsTimeTrial(SelectedMode)) return;
            if (State != GameState.Playing && State != GameState.Falling) return;
            // Wait for the first jump — clock stays at full duration on the ready pad.
            if (!TimeTrialClockRunning) return;

            // timeScale 0 while paused freezes deltaTime automatically.
            TimeTrialRemaining -= Time.deltaTime;
            if (TimeTrialRemaining < 0f)
                TimeTrialRemaining = 0f;

            // UI only when the displayed second ticks (avoid per-frame string + style work).
            var whole = Mathf.CeilToInt(TimeTrialRemaining);
            if (whole != lastTimerWholeSeconds)
            {
                lastTimerWholeSeconds = whole;
                gameUI?.UpdateTimer(TimeTrialRemaining);
            }

            if (TimeTrialRemaining <= 0f && State != GameState.GameOver)
            {
                LastRunTimedOut = true;
                gameUI?.FlashCenterMessage("TIME!", 0.85f);
                TriggerGameOver(timeUp: true);
            }
        }

        /// <summary>
        /// Starts the time-trial countdown on the player's first jump input.
        /// Safe to call repeatedly; only the first call arms the clock.
        /// </summary>
        public void BeginTimeTrialClockIfNeeded()
        {
            if (!GameModeRules.IsTimeTrial(SelectedMode)) return;
            if (TimeTrialClockRunning) return;
            if (State != GameState.Playing && State != GameState.Falling) return;

            TimeTrialClockRunning = true;
            lastTimerWholeSeconds = Mathf.CeilToInt(TimeTrialRemaining);
            gameUI?.UpdateTimer(TimeTrialRemaining);
        }

        public void TogglePause()
        {
            if (State == GameState.Paused)
                PauseGame(false);
            else if (State == GameState.Playing || State == GameState.Falling)
                PauseGame(true);
        }

        public void PauseGame(bool pause)
        {
            if (pause)
            {
                if (State == GameState.Paused || State == GameState.Ready || State == GameState.GameOver)
                    return;

                stateBeforePause = State;
                timeScaleBeforePause = Time.timeScale <= 0.01f ? 1f : Time.timeScale;
                Time.timeScale = 0f;
                SetState(GameState.Paused);
                gameUI?.ShowPaused(true);
                FeedbackService.UiSelect();
            }
            else
            {
                if (State != GameState.Paused) return;

                Time.timeScale = timeScaleBeforePause > 0.01f ? timeScaleBeforePause : 1f;
                SetState(stateBeforePause);
                gameUI?.ShowPaused(false);
                gameUI?.ShowPlaying(Score, HighScore);
                FeedbackService.UiSelect();
            }
        }

        public void StartGame()
        {
            if (!EnsureWired()) return;
            if (leafSpawner == null || player == null || gameUI == null)
            {
                Debug.LogError("[GameManager] Cannot start — scene refs missing after EnsureWired.");
                return;
            }

            deathHitLatched = false;
            Time.timeScale = 1f;
            // Mode systems / blackout off first so a prior run never leaks into this one.
            SyncStormEffects(false);
            StopAllModeSystems();

            ReloadDatabasesAndSelections();
            leafSpawner.SetGameMode(SelectedMode, SelectedDifficulty);
            HighScore = ResolveHighScore();

            Score = 0;
            PeakScore = 0;
            LastRunTimedOut = false;
            TimeTrialClockRunning = false;
            if (GameModeRules.IsTimeTrial(SelectedMode))
            {
                // Re-resolve from prefs so the clock always matches the chip last picked.
                SelectedTimeTrialLength = SaveService.SelectedTimeTrialLength;
                var seconds = GameModeRules.TimeTrialSeconds(SelectedTimeTrialLength);
                TimeTrialRemaining = seconds;
                lastTimerWholeSeconds = seconds;
                // Clock is armed full but frozen until the first jump.
                gameUI?.ShowTimer(true);
                gameUI?.UpdateTimer(TimeTrialRemaining);
            }
            else
            {
                TimeTrialRemaining = 0f;
                lastTimerWholeSeconds = -1;
                gameUI?.ShowTimer(false);
            }

            leafSpawner.Initialize();
            leafSpawner.ApplyPlatformVisibility();
            player.ResetToStart();
            backgroundScroller?.ResetToOrigin();

            SetState(GameState.Playing);
            gameUI.ShowPlaying(Score, HighScore);

            gameUI.FlashCenterMessage(
                BuildRunBanner(),
                GameModeRules.UsesBlackout(SelectedMode) ? 0.7f : 0.55f);

            SyncStormEffects(true);
            BeginModeSystems();
            OnScoreChanged?.Invoke(Score);
        }

        string BuildRunBanner()
        {
            if (GameModeRules.IsRace(SelectedMode))
            {
                var finish = GameModeRules.RaceFinishHeight(SelectedDifficulty);
                return $"RACE TO {finish}";
            }

            if (GameModeRules.IsTimeTrial(SelectedMode))
                return $"TIME TRIAL · {GameModeRules.TimeTrialSecondsLabel(SelectedTimeTrialLength)}";

            if (GameModeRules.IsTempo(SelectedMode))
                return "TEMPO";

            return GameModeRules.CombinedLabel(SelectedMode, SelectedDifficulty).ToUpperInvariant();
        }

        /// <summary>Start only the systems required by the selected mode.</summary>
        void BeginModeSystems()
        {
            BeginRaceIfNeeded();
            BeginCombatantIfNeeded();
            BeginTempoIfNeeded();
        }

        /// <summary>Tear down race / combatant / tempo so modes never leak across runs.</summary>
        void StopAllModeSystems()
        {
            StopRaceIfAny();
            StopCombatantIfAny();
            StopTempoImmediate();
        }

        void BeginRaceIfNeeded()
        {
            if (!GameModeRules.IsRace(SelectedMode))
            {
                gameUI?.HideRaceHud();
                return;
            }

            var race = RaceController.Ensure();
            var opponent = GetRaceOpponent();
            // Prefer a different creature than the player when possible.
            if (opponent != null && SelectedCreature != null &&
                opponent.Id == SelectedCreature.Id &&
                creatureDatabase != null && creatureDatabase.Count > 1)
            {
                CycleRaceOpponent(1);
                opponent = GetRaceOpponent();
            }

            var finish = GameModeRules.RaceFinishHeight(SelectedDifficulty);
            leafSpawner?.SetRaceFinishHeight(finish);

            // Rebuild full race course (all pads pre-generated for shared path).
            leafSpawner?.Initialize();
            player?.ResetToStart();

            race.Begin(SelectedCreature, opponent, SelectedDifficulty, leafSpawner);
            gameUI?.ShowRaceHud();
        }

        void StopRaceIfAny()
        {
            if (RaceController.Instance != null)
                RaceController.Instance.Stop();
            leafSpawner?.ClearRaceFinish();
            gameUI?.HideRaceHud();
        }

        void BeginCombatantIfNeeded()
        {
            if (!GameModeRules.IsCombatant(SelectedMode))
            {
                CombatantDirector.Instance?.Stop();
                return;
            }

            var director = CombatantDirector.Ensure();
            director.Begin(leafSpawner, SelectedDifficulty);
        }

        void StopCombatantIfAny()
        {
            CombatantDirector.Instance?.Stop();
        }

        void BeginTempoIfNeeded()
        {
            if (!GameModeRules.IsTempo(SelectedMode))
            {
                StopTempoIfAny();
                return;
            }

            // Clear any hot-reload sprite cache so ocean art never reuses old red-zone textures.
            TempoController.InvalidateSpriteCache();
            var tempo = TempoController.Ensure();
            var playerTf = player != null ? player.transform : null;
            tempo.Begin(playerTf, SelectedDifficulty, leafSpawner);
        }

        void StopTempoIfAny()
        {
            // Don't cut off a defeat flush mid-surge (game over can fire while it plays).
            if (TempoController.Instance != null && TempoController.Instance.IsFlushing)
                return;

            if (TempoController.Instance != null)
                TempoController.Instance.Stop();
            CameraFollow.EndAutoScroll();
        }

        /// <summary>Hard stop for restart / menu — cancels flush if needed.</summary>
        void StopTempoImmediate()
        {
            if (TempoController.Instance != null)
                TempoController.Instance.Stop();
            CameraFollow.EndAutoScroll();
        }

        /// <summary>Player slipped below the scrolling view in Tempo mode.</summary>
        public void OnTempoLeftBehind()
        {
            if (State != GameState.Playing && State != GameState.Falling) return;
            if (!GameModeRules.IsTempo(SelectedMode)) return;
            if (deathHitLatched || State == GameState.GameOver) return;

            gameUI?.FlashCenterMessage("WASHED AWAY!", 0.85f);
            PlayDeathHitThenFall(flushTempoWave: true);
        }

        /// <summary>Combatant contact — shake and fall from the hit point.</summary>
        public void OnCombatantHit()
        {
            // Climbing/jumping only — ignore while already falling.
            if (State != GameState.Playing) return;
            if (!GameModeRules.IsCombatant(SelectedMode)) return;
            if (deathHitLatched || State == GameState.GameOver) return;

            gameUI?.FlashCenterMessage("BUZZED!", 0.85f);
            PlayDeathHitThenFall();
        }

        public void OnRaceFinished(bool playerWon)
        {
            if (State == GameState.GameOver) return;
            LastRacePlayerWon = playerWon;

            if (playerWon)
            {
                // Snap score to finish for clean end screen.
                var finish = GameModeRules.RaceFinishHeight(SelectedDifficulty);
                SetScoreFromHeight(finish, force: true);
                gameUI?.FlashCenterMessage("YOU WIN!", 1f);
                FeedbackService.Recover();
            }
            else
            {
                var opp = RaceController.Instance != null
                    ? RaceController.Instance.Opponent
                    : GetRaceOpponent();
                gameUI?.FlashCenterMessage(RaceNicknames.OpponentWinsLine(opp), 1f);
                FeedbackService.GameOver();
            }

            TriggerGameOver(raceResult: true);
        }

        public void RegisterSuccessfulJump()
        {
            var height = leafSpawner?.CurrentLeaf != null ? leafSpawner.CurrentLeaf.HeightIndex : Score + 1;
            SetScoreFromHeight(height);
            TryCollectLeafCoin();
            FeedbackService.LandSoft();

            // Breakable pads crumble after land — timer depends on Game Mode.
            ArmBreakCountdownOnCurrentLeaf();
            StormEffects.Instance?.SetClimbHeight(height);
        }

        void ArmBreakCountdownOnCurrentLeaf()
        {
            ArmBreakCountdown(leafSpawner?.CurrentLeaf);
        }

        void ArmBreakCountdown(Leaf leaf)
        {
            if (leaf == null || !leaf.CanBreak || leaf.IsBroken) return;

            leaf.OnBreakCountdownExpired -= OnLeafBreakCountdownExpired;
            leaf.OnBreakCountdownExpired += OnLeafBreakCountdownExpired;
            var height = leaf.HeightIndex;
            leaf.StartBreakCountdown(
                GameModeRules.BreakCountdownSeconds(SelectedMode, SelectedDifficulty, height));
        }

        /// <summary>
        /// Wrong vertical hop on a solid pad: stay safe, flip to breakable, start fuse.
        /// Also used after clutch recover onto a solid.
        /// </summary>
        public void ConvertSolidPadToBreakable(Leaf leaf)
        {
            if (leaf == null || leaf.IsBroken || !leaf.IsSolid) return;
            if (State != GameState.Playing) return;

            leaf.ConvertToBreakable();
            ArmBreakCountdown(leaf);
            FeedbackService.LandSoft();
        }

        /// <summary>
        /// Stop shake/timer when the player successfully jumps off a breakable pad.
        /// Does not break the pad — wrong vertical/diagonal on breakable still uses Break() for instant fall-through.
        /// </summary>
        public void DisarmBreakCountdown(Leaf leaf)
        {
            if (leaf == null) return;
            leaf.OnBreakCountdownExpired -= OnLeafBreakCountdownExpired;
            leaf.CancelBreakCountdown();
        }

        void OnLeafBreakCountdownExpired(Leaf leaf)
        {
            if (leaf == null) return;
            leaf.OnBreakCountdownExpired -= OnLeafBreakCountdownExpired;

            // Never shatter while the player is mid-jump / mid-fall — only when
            // they are actually standing on this pad.
            if (State != GameState.Playing) return;
            if (player != null && player.IsBusy) return;
            if (leafSpawner == null || leafSpawner.CurrentLeaf != leaf) return;
            if (leaf.IsBroken || !leaf.CanBreak) return;
            if (deathHitLatched || State == GameState.GameOver) return;

            leaf.Break();
            gameUI?.FlashCenterMessage("CRUMBLED!", 0.85f);
            PlayDeathHitThenFall();
        }

        /// <summary>
        /// Hit reaction: 0.8s screen shake, cancel mid-jump, fall from this pose.
        /// Combatant director stays live so clutch-saves still get hazards.
        /// </summary>
        void PlayDeathHitThenFall(bool flushTempoWave = false)
        {
            if (deathHitLatched || State == GameState.GameOver) return;
            deathHitLatched = true;

            if (flushTempoWave && TempoController.Instance != null)
                TempoController.Instance.BeginDefeatFlush();

            CameraFollow.Shake(0.28f, 0.8f);
            HapticService.Fail();

            if (player == null)
            {
                TriggerGameOver();
                return;
            }

            player.InterruptAndFallFromHere(breakPad: false);

            if (GameModeRules.IsCombatant(SelectedMode) && CombatantDirector.Instance != null)
            {
                var h = leafSpawner?.CurrentLeaf != null ? leafSpawner.CurrentLeaf.HeightIndex : 0;
                CombatantDirector.Instance.OnPlayerHeight(h);
            }
        }

        void TryCollectLeafCoin()
        {
            var leaf = leafSpawner?.CurrentLeaf;
            if (leaf == null || !leaf.TryCollectCoin()) return;

            FeedbackService.ScoreTick();
            gameUI?.RefreshCoinHud();
        }

        public void SetScoreFromHeight(int heightIndex, bool force = false)
        {
            // Race tracks raw climb height (finish line is exact).
            var next = GameModeRules.IsRace(SelectedMode)
                ? Mathf.Max(0, heightIndex)
                : Mathf.Max(0, Mathf.RoundToInt(heightIndex * creatureScoreMul));
            if (GameModeRules.UsesBlackout(SelectedMode))
                StormEffects.Instance?.SetClimbHeight(heightIndex);

            if (!force && next == Score)
                return;

            Score = next;
            if (Score > PeakScore)
                PeakScore = Score;

            gameUI?.UpdateScore(Score);
            OnScoreChanged?.Invoke(Score);

            if (GameModeRules.IsRace(SelectedMode) && State == GameState.Playing)
                RaceController.Instance?.NotifyPlayerHeight(heightIndex);

            if (GameModeRules.IsCombatant(SelectedMode))
                CombatantDirector.Instance?.OnPlayerHeight(heightIndex);
        }

        public void BeginFall()
        {
            if (State == GameState.Falling || State == GameState.GameOver || State == GameState.Paused) return;
            SetState(GameState.Falling);
            FeedbackService.FallStart();
        }

        public void RecoverFromFall(int heightIndex = -1)
        {
            if (State != GameState.Falling) return;

            if (heightIndex < 0)
                heightIndex = leafSpawner?.CurrentLeaf != null ? leafSpawner.CurrentLeaf.HeightIndex : 0;

            SetScoreFromHeight(heightIndex, force: true);
            StormEffects.Instance?.SetClimbHeight(heightIndex);

            // No coins on fall recovery — only successful jumps onto coin pads.
            SetState(GameState.Playing);
            // Allow combatant hits again after a clutch save (latched on the prior death hit).
            deathHitLatched = false;
            gameUI?.ShowPlaying(Score, HighScore);
            gameUI?.FlashCenterMessage("PHEW!", 0.85f);
            FeedbackService.Recover();

            // Clutch solid pad is converted to breakable in LeafSpawner.RecoverTo — start fuse.
            ArmBreakCountdownOnCurrentLeaf();

            // Combatant mode: keep (or restart) hazards after a clutch save.
            if (GameModeRules.IsCombatant(SelectedMode) && leafSpawner != null)
            {
                var director = CombatantDirector.Instance;
                if (director == null || !director.IsRunning)
                    BeginCombatantIfNeeded();
                else
                    director.OnPlayerHeight(heightIndex);
            }
        }

        public void TriggerGameOver(bool raceResult = false, bool timeUp = false)
        {
            if (State == GameState.GameOver) return;

            deathHitLatched = false;
            Time.timeScale = 1f;
            SyncStormEffects(false);
            // Stop non-race systems first. Race may still need NotifyPlayerEliminated.
            StopCombatantIfAny();
            StopTempoIfAny();
            gameUI?.ShowTimer(false);

            if (timeUp)
                LastRunTimedOut = true;

            // Falling to death mid-race (not a clean race finish) → AI wins.
            if (!raceResult && GameModeRules.IsRace(SelectedMode) &&
                RaceController.Instance != null && RaceController.Instance.IsRacing)
            {
                RaceController.Instance.NotifyPlayerEliminated();
                return;
            }

            StopRaceIfAny();

            var runBest = Mathf.Max(Score, PeakScore);
            if (GameModeRules.IsTimeTrial(SelectedMode))
            {
                if (SaveService.TrySetHighScore(
                        SelectedMode, SelectedDifficulty, runBest, SelectedTimeTrialLength))
                    HighScore = runBest;
                else
                    HighScore = SaveService.GetHighScore(
                        SelectedMode, SelectedDifficulty, SelectedTimeTrialLength);
            }
            else if (SaveService.TrySetHighScore(SelectedMode, SelectedDifficulty, runBest))
                HighScore = runBest;
            else
                HighScore = SaveService.GetHighScore(SelectedMode, SelectedDifficulty);

            SaveService.RecordRun(runBest);

            SetState(GameState.GameOver);
            gameUI?.ShowGameOver(runBest, HighScore, GetGameOverTitle());
            if (!raceResult)
                FeedbackService.GameOver();
        }

        string GetGameOverTitle()
        {
            if (GameModeRules.IsTimeTrial(SelectedMode) && LastRunTimedOut)
                return "TIME!";
            if (!GameModeRules.IsRace(SelectedMode)) return "GAME OVER";
            if (LastRacePlayerWon) return "YOU WIN!";

            var opp = RaceController.Instance != null
                ? RaceController.Instance.Opponent
                : GetRaceOpponent();
            return RaceNicknames.OpponentWinsLine(opp);
        }

        public void Restart()
        {
            if (State != GameState.GameOver && State != GameState.Ready)
                return;

            StartGame();
        }

        /// <summary>Leave game over and open the main menu (Ready) without starting a run.</summary>
        public void ReturnToMenu()
        {
            if (State != GameState.GameOver && State != GameState.Paused)
                return;

            if (State == GameState.Paused)
            {
                Time.timeScale = 1f;
            }

            FeedbackService.UiSelect();
            EnterReadyState();
        }

        void EnterReadyState()
        {
            Time.timeScale = 1f;
            Score = 0;
            PeakScore = 0;
            LastRunTimedOut = false;
            TimeTrialRemaining = 0f;
            TimeTrialClockRunning = false;
            SyncStormEffects(false);
            StopAllModeSystems();
            gameUI?.ShowTimer(false);
            ReloadDatabasesAndSelections();
            leafSpawner?.SetGameMode(SelectedMode, SelectedDifficulty);
            HighScore = ResolveHighScore();

            if (leafSpawner != null)
            {
                leafSpawner.Initialize();
                leafSpawner.ApplyPlatformVisibility();
            }
            player?.ResetToStart();
            backgroundScroller?.ResetToOrigin();

            SetState(GameState.Ready);
            RefreshReadyUi();
        }

        void SetState(GameState newState)
        {
            State = newState;
            // Playing / falling: 60fps + keep-awake. Menus / game-over: 30fps + system sleep.
            // Paused stays awake so the pause UI doesn't dim mid-read.
            var activeClimb =
                newState == GameState.Playing ||
                newState == GameState.Falling;
            var keepScreen =
                activeClimb || newState == GameState.Paused;

            if (PlatformRuntime.IsMobile)
            {
                Application.targetFrameRate = activeClimb ? 60 : 30;
                PlatformRuntime.SetKeepAwake(keepScreen);
            }

            OnStateChanged?.Invoke(State);
        }

        void SyncStormEffects(bool playing)
        {
            try
            {
                var blackout = playing && GameModeRules.UsesBlackout(SelectedMode);
                if (StormEffects.Instance == null && blackout)
                {
                    var go = new GameObject("--- Blackout Effects ---");
                    go.AddComponent<StormEffects>();
                }

                StormEffects.Instance?.SetRunning(blackout);
                if (blackout)
                    StormEffects.Instance?.SetClimbHeight(0);
            }
            catch (System.Exception e)
            {
                // Blackout VFX must never block Classic play or starting a run.
                Debug.LogWarning("[GameManager] Blackout effects error (ignored): " + e.Message);
            }
        }

        void ApplySelectedCreature()
        {
            if (creatureDatabase == null)
                creatureDatabase = CreatureDatabase.Load();

            if (creatureDatabase == null || creatureDatabase.Count == 0)
            {
                SelectedCreature = null;
                creatureScoreMul = 1f;
                Debug.LogWarning("[GameManager] Creature Database empty or missing (Assets/Data/Resources/CreatureDatabase).");
                return;
            }

            playAsCreatureIndex = Mathf.Clamp(playAsCreatureIndex, 0, creatureDatabase.Count - 1);
            SelectedCreature = creatureDatabase.GetCreature(playAsCreatureIndex);

            if (SelectedCreature == null) return;

            player?.ApplyCreature(SelectedCreature);
            creatureScoreMul = Mathf.Max(0.1f, SelectedCreature.scoreMultiplier);
        }

        void ApplySelectedPlatforms()
        {
            if (platformDatabase == null)
                platformDatabase = PlatformDatabase.Load();

            if (platformDatabase == null || platformDatabase.Count == 0)
            {
                SelectedPlatform = null;
                leafSpawner?.SetActivePlatform(null);
                Debug.LogWarning("[GameManager] Platform Database empty or missing (Assets/Data/Resources/PlatformDatabase).");
                return;
            }

            activePlatformIndex = Mathf.Clamp(activePlatformIndex, 0, platformDatabase.Count - 1);
            SelectedPlatform = platformDatabase.GetPlatform(activePlatformIndex);
            leafSpawner?.SetActivePlatform(SelectedPlatform);
        }

        void ApplyBackgroundDatabase()
        {
            if (backgroundDatabase == null)
                backgroundDatabase = BackgroundDatabase.Load();

            if (backgroundScroller == null)
                backgroundScroller = FindFirstObjectByType<BackgroundScroller>();

            if (backgroundDatabase == null || backgroundDatabase.Count == 0)
            {
                SelectedBackground = null;
                backgroundScroller?.BindDatabase(null);
                return;
            }

            activeBackgroundIndex = Mathf.Clamp(activeBackgroundIndex, 0, backgroundDatabase.Count - 1);
            SelectedBackground = backgroundDatabase.GetBackground(activeBackgroundIndex);
            // Single stage pick — selected backdrop for the whole climb.
            backgroundScroller?.BindDatabase(backgroundDatabase, activeBackgroundIndex, singleStage: true);
        }
    }
}
