using System;
using UnityEngine;

namespace CreatureClimb
{
    public interface IGameUI
    {
        void ShowReady(int highScore);
        void ShowPlaying(int score, int highScore);
        void UpdateScore(int score);
        void ShowGameOver(int score, int highScore);
        void ShowPaused(bool paused);
    }

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

        public bool IsWired =>
            leafSpawner != null &&
            player != null &&
            gameUI != null;

        float creatureScoreMul = 1f;
        bool started;
        GameState stateBeforePause = GameState.Playing;
        float timeScaleBeforePause = 1f;

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
            HighScore = SaveService.HighScore;
            LoadSelectionPrefs();
        }

        void Start()
        {
            if (!EnsureWired())
            {
                Debug.LogError("[GameManager] Missing scene refs. Menu: Creature Climb → Setup MVP Scene");
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

            Debug.Log(
                $"[GameManager] Using databases — " +
                $"Creatures={creatureDatabase?.Count ?? 0}, " +
                $"Platforms={platformDatabase?.Count ?? 0}, " +
                $"Backgrounds={backgroundDatabase?.Count ?? 0}");
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

        public void OnCustomizeClosed()
        {
            // Return to main menu; world already shows the selected loadout.
            if (State == GameState.Ready)
                RefreshReadyUi();
            else if (State == GameState.GameOver)
                gameUI?.ShowGameOver(Mathf.Max(Score, PeakScore), HighScore);
        }

        public void OnShopClosed()
        {
            if (State == GameState.Ready)
                RefreshReadyUi();
            else if (State == GameState.GameOver)
                gameUI?.ShowGameOver(Mathf.Max(Score, PeakScore), HighScore);
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
            playAsCreatureIndex = SaveService.CreatureIndex;
            activePlatformIndex = SaveService.PlatformIndex;
            activeBackgroundIndex = SaveService.BackgroundIndex;
            if (playAsCreatureIndex < 0) playAsCreatureIndex = 0;
            if (activePlatformIndex < 0) activePlatformIndex = 0;
            if (activeBackgroundIndex < 0) activeBackgroundIndex = 0;
        }

        void SaveSelectionPrefs()
        {
            SaveService.CreatureIndex = playAsCreatureIndex;
            SaveService.PlatformIndex = activePlatformIndex;
            SaveService.BackgroundIndex = activeBackgroundIndex;
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

            if (State == GameState.Ready)
            {
                // Main menu / customize / shop own input — start only via Play (or keyboard confirm).
                if (gameUI != null && (gameUI.IsCustomizeOpen || gameUI.IsShopOpen))
                    return;

                if (TapInput.WasConfirmPressed())
                {
                    StartGame();
                    return;
                }

                return;
            }

            if (gameUI != null && (gameUI.IsCustomizeOpen || gameUI.IsShopOpen))
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

            Time.timeScale = 1f;
            ReloadDatabasesAndSelections();

            Score = 0;
            PeakScore = 0;

            leafSpawner.Initialize();
            player.ResetToStart();
            backgroundScroller?.ResetToOrigin();

            SetState(GameState.Playing);
            gameUI.ShowPlaying(Score, HighScore);
            OnScoreChanged?.Invoke(Score);
        }

        public void RegisterSuccessfulJump()
        {
            var height = leafSpawner?.CurrentLeaf != null ? leafSpawner.CurrentLeaf.HeightIndex : Score + 1;
            SetScoreFromHeight(height);
            TryCollectLeafCoin();
            FeedbackService.LandSoft();

            // Breakable pads crumble 2s after you land — jump off before they go.
            ArmBreakCountdownOnCurrentLeaf();
        }

        void ArmBreakCountdownOnCurrentLeaf()
        {
            var leaf = leafSpawner?.CurrentLeaf;
            if (leaf == null || !leaf.CanBreak || leaf.IsBroken) return;

            leaf.OnBreakCountdownExpired -= OnLeafBreakCountdownExpired;
            leaf.OnBreakCountdownExpired += OnLeafBreakCountdownExpired;
            leaf.StartBreakCountdown(Leaf.BreakCountdownSeconds);
        }

        /// <summary>
        /// Stop shake/timer when the player successfully jumps off a breakable pad.
        /// Does not break the pad — wrong-side still uses Break() for instant fall-through.
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

            // Only drop the player if they are still standing on this pad.
            if (State != GameState.Playing) return;
            if (leafSpawner == null || leafSpawner.CurrentLeaf != leaf) return;
            if (player == null || player.IsBusy) return;

            // Pad already shattered; ForceFall still starts the fall (Break is a no-op).
            player.ForceFall();
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
            var next = Mathf.Max(0, Mathf.RoundToInt(heightIndex * creatureScoreMul));
            if (!force && next == Score)
                return;

            Score = next;
            if (Score > PeakScore)
                PeakScore = Score;

            gameUI?.UpdateScore(Score);
            OnScoreChanged?.Invoke(Score);
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

            // No coins on fall recovery — only successful jumps onto coin pads.
            SetState(GameState.Playing);
            gameUI?.ShowPlaying(Score, HighScore);
            gameUI?.FlashCenterMessage("PHEW!", 0.85f);
            FeedbackService.Recover();
        }

        public void TriggerGameOver()
        {
            if (State == GameState.GameOver) return;

            Time.timeScale = 1f;

            var runBest = Mathf.Max(Score, PeakScore);
            if (SaveService.TrySetHighScore(runBest))
                HighScore = runBest;
            else
                HighScore = SaveService.HighScore;

            SaveService.RecordRun(runBest);
            // Coins only from landing on every-3rd coin pads — never from score.

            SetState(GameState.GameOver);
            gameUI?.ShowGameOver(runBest, HighScore);
            FeedbackService.GameOver();
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
            HighScore = SaveService.HighScore;
            ReloadDatabasesAndSelections();

            leafSpawner.Initialize();
            player.ResetToStart();
            backgroundScroller?.ResetToOrigin();

            SetState(GameState.Ready);
            RefreshReadyUi();
        }

        void SetState(GameState newState)
        {
            State = newState;
            OnStateChanged?.Invoke(State);
        }

        void ApplySelectedCreature()
        {
            if (creatureDatabase == null)
                creatureDatabase = CreatureDatabase.Load();

            if (creatureDatabase == null || creatureDatabase.Count == 0)
            {
                SelectedCreature = null;
                creatureScoreMul = 1f;
                Debug.LogWarning("[GameManager] Creature Database empty or missing (Assets/Resources/CreatureDatabase).");
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
                Debug.LogWarning("[GameManager] Platform Database empty or missing (Assets/Resources/PlatformDatabase).");
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
