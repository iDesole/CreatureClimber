using System;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Race mode brain: AI climbs the same pre-built path as the player.
    /// </summary>
    public class RaceController : MonoBehaviour
    {
        public static RaceController Instance { get; private set; }

        public bool IsRacing { get; private set; }
        public int FinishHeight { get; private set; }
        public int PlayerHeight { get; private set; }
        public int AiHeight { get; private set; }
        public Creature Opponent { get; private set; }
        public Creature PlayerCreature { get; private set; }
        public bool? PlayerWon { get; private set; }

        public event Action OnRaceUpdated;
        public event Action<bool> OnRaceEnded;

        float nextAiJumpAt;
        Difficulty difficulty;
        RaceGhost ghost;
        LeafSpawner spawner;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static RaceController Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("--- Race ---");
            return go.AddComponent<RaceController>();
        }

        public void Begin(
            Creature playerCreature,
            Creature opponent,
            Difficulty diff,
            LeafSpawner leafSpawner)
        {
            PlayerCreature = playerCreature;
            Opponent = opponent;
            difficulty = diff;
            spawner = leafSpawner;
            FinishHeight = GameModeRules.RaceFinishHeight(diff);
            PlayerHeight = 0;
            AiHeight = 0;
            PlayerWon = null;
            IsRacing = true;
            ScheduleNextAiJump();
            EnsureGhost();
            OnRaceUpdated?.Invoke();
        }

        public void Stop()
        {
            IsRacing = false;
            if (ghost != null)
                ghost.Hide();
        }

        public void NotifyPlayerHeight(int height)
        {
            if (!IsRacing) return;

            PlayerHeight = Mathf.Max(0, height);
            OnRaceUpdated?.Invoke();

            if (PlayerHeight >= FinishHeight)
                EndRace(playerWon: true);
        }

        void EnsureGhost()
        {
            if (ghost == null)
                ghost = RaceGhost.Create(transform);

            ghost.Configure(Opponent, spawner, FinishHeight);
            ghost.SetAiHeight(0);
        }

        void Update()
        {
            if (!IsRacing) return;

            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.State == GameState.Paused || gm.State == GameState.GameOver || gm.State == GameState.Ready)
                return;

            if (Time.time < nextAiJumpAt) return;

            // Advance along the same pad indices as the player path.
            AiHeight = Mathf.Min(FinishHeight, AiHeight + 1);
            ghost?.SetAiHeight(AiHeight);
            ScheduleNextAiJump();
            OnRaceUpdated?.Invoke();

            if (AiHeight >= FinishHeight)
                EndRace(playerWon: false);
        }

        void ScheduleNextAiJump()
        {
            var interval = GameModeRules.RaceAiBaseJumpInterval(difficulty);
            if (Opponent != null)
            {
                var jump = Mathf.Max(0.35f, Opponent.jumpSpeed);
                var react = Mathf.Clamp(Opponent.reactionTime, 0.5f, 1.6f);
                interval = interval / jump * react;
            }

            interval = Mathf.Clamp(interval, 0.18f, 1.1f);
            nextAiJumpAt = Time.time + interval;
        }

        void EndRace(bool playerWon)
        {
            if (!IsRacing) return;
            IsRacing = false;
            PlayerWon = playerWon;
            // Safety: clear finish slo-mo if race ends while scale is still reduced.
            if (Time.timeScale > 0.01f && Time.timeScale < 0.99f)
                Time.timeScale = 1f;
            OnRaceEnded?.Invoke(playerWon);
            GameManager.Instance?.OnRaceFinished(playerWon);
        }

        public void NotifyPlayerEliminated()
        {
            if (!IsRacing) return;
            EndRace(playerWon: false);
        }
    }
}
