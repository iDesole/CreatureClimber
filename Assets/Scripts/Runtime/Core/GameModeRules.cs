using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Pure data for each <see cref="GameMode"/> + <see cref="Difficulty"/>.
    /// Keep rules here — not scattered in spawner/UI.
    /// </summary>
    public static class GameModeRules
    {
        /// <summary>Menu order for the Game Mode screen (skip reserved/legacy slots).</summary>
        public static readonly GameMode[] MenuOrder =
        {
            GameMode.Classic,
            GameMode.Blackout,
            GameMode.Race,
            GameMode.Combatant,
            GameMode.TimeTrial,
            GameMode.Tempo
        };

        public static string DisplayName(GameMode mode) => mode switch
        {
            GameMode.Blackout => "Blackout",
            GameMode.Race => "Race",
            GameMode.Combatant => "Combatant",
            GameMode.TimeTrial => "Time Trial",
            GameMode.Tempo => "Tempo",
            _ => "Classic"
        };

        public static string DifficultyName(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Hard => "Hard",
            _ => "Easy"
        };

        public static string Description(GameMode mode, Difficulty difficulty)
        {
            return mode switch
            {
                GameMode.Blackout => difficulty == Difficulty.Hard
                    ? "Blackouts hide the climb. Every pad breaks — jump fast."
                    : "Blackouts hide the climb. Solid pads and breakables mixed in.",
                GameMode.Race => difficulty == Difficulty.Hard
                    ? "Race an AI to 200. Every pad breaks — don't fall behind."
                    : "Race an AI to 100. First to the finish wins.",
                GameMode.Combatant => difficulty == Difficulty.Hard
                    ? "Dodge flying foes between pads. Pads break in 1.15s — time your jumps."
                    : "Dodge flying foes. Pads under bees are solid so you can wait them out.",
                GameMode.TimeTrial => difficulty == Difficulty.Hard
                    ? "Beat the clock. Every pad breaks — climb as high as you can."
                    : "Beat the clock. Climb as high as you can before time runs out.",
                GameMode.Tempo => difficulty == Difficulty.Hard
                    ? "Camera scrolls on its own. Stay above the rising ocean. Pads last 3s — every one breaks."
                    : "Camera scrolls on its own. Stay above the rising ocean. Pads last 3s. Match the tempo.",
                _ => difficulty == Difficulty.Hard
                    ? "Classic climb. Every platform breaks — jump before it shatters."
                    : "Classic climb. Solid pads and breakables mixed in."
            };
        }

        public static string CombinedLabel(GameMode mode, Difficulty difficulty) =>
            $"{DisplayName(mode)} · {DifficultyName(difficulty)}";

        public static string CombinedLabel(GameMode mode, Difficulty difficulty, TimeTrialLength length) =>
            IsTimeTrial(mode)
                ? $"{DisplayName(mode)} · {TimeTrialSecondsLabel(length)} · {DifficultyName(difficulty)}"
                : CombinedLabel(mode, difficulty);

        /// <summary>Clock length in whole seconds for a time-trial option.</summary>
        public static int TimeTrialSeconds(TimeTrialLength length)
        {
            // Enum values are the seconds (90 / 120 / 200).
            var n = (int)length;
            if (n == 90 || n == 120 || n == 200)
                return n;

            // Legacy ordinal saves: 0 / 1 / 2.
            if (n == 1) return 120;
            if (n == 2) return 200;
            return 90;
        }

        public static string TimeTrialSecondsLabel(TimeTrialLength length) =>
            $"{TimeTrialSeconds(length)}s";

        public static string FormatTimer(float secondsRemaining)
        {
            var whole = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
            var m = whole / 60;
            var sec = whole % 60;
            // Always mm:ss so 120s reads "2:00", never ambiguous bare numbers.
            return $"{m}:{sec:00}";
        }

        public static float BreakableChance(GameMode mode, Difficulty difficulty)
        {
            if (difficulty == Difficulty.Hard)
                return 1f;
            return 0.75f;
        }

        public static float BreakCountdownSeconds(
            GameMode mode,
            Difficulty difficulty,
            int heightIndex = 0)
        {
            // Tempo: long fuse — pressure is the scroll, not the pad timer.
            if (mode == GameMode.Tempo)
                return TempoBreakSeconds;

            // Combatant: longer fuse so you can time a jump past the flyer.
            if (mode == GameMode.Combatant)
                return 3f;

            if (mode == GameMode.Blackout && difficulty == Difficulty.Hard)
                return Mathf.Max(0.55f, 0.9f - heightIndex * 0.012f);

            return 0.75f;
        }

        public static bool UsesBlackout(GameMode mode) => mode == GameMode.Blackout;

        public static bool IsRace(GameMode mode) => mode == GameMode.Race;

        public static bool IsCombatant(GameMode mode) => mode == GameMode.Combatant;

        public static bool IsTimeTrial(GameMode mode) => mode == GameMode.TimeTrial;

        public static bool IsTempo(GameMode mode) => mode == GameMode.Tempo;

        // ── Tempo scroll tuning (world units / second) ─────────────────────────

        /// <summary>Breakable pad fuse in Tempo (scroll is the real pressure).</summary>
        public const float TempoBreakSeconds = 3f;

        /// <summary>
        /// Visible danger band height as a fraction of camera half-height.
        /// Player dies when their Y drops into this band.
        /// </summary>
        public const float TempoDeathZoneOrthoFraction = 0.28f;

        /// <summary>Minimum world height of the Tempo death band.</summary>
        public const float TempoDeathZoneMinHeight = 1.35f;

        public static float TempoDeathZoneHeight(float orthographicSize) =>
            Mathf.Max(TempoDeathZoneMinHeight, orthographicSize * TempoDeathZoneOrthoFraction);

        /// <summary>Seconds of gentle ramp before full tempo and kill checks.</summary>
        public static float TempoGraceSeconds(Difficulty difficulty) =>
            difficulty == Difficulty.Hard ? 1.25f : 1.75f;

        /// <summary>Baseline scroll near the start of a run.</summary>
        public static float TempoBaseSpeed(Difficulty difficulty) =>
            difficulty == Difficulty.Hard ? 1.9f : 1.35f;

        /// <summary>Ceiling for the rising baseline (after long climb).</summary>
        public static float TempoMaxBaseSpeed(Difficulty difficulty) =>
            difficulty == Difficulty.Hard ? 4.6f : 3.2f;

        /// <summary>How many seconds until baseline approaches max.</summary>
        public static float TempoRampSeconds(Difficulty difficulty) =>
            difficulty == Difficulty.Hard ? 45f : 70f;

        /// <summary>
        /// Half-range of the speed wave as a fraction of baseline
        /// (0.5 → speed swings from 0.5× to 1.5× the current base).
        /// </summary>
        public static float TempoWaveAmplitude(Difficulty difficulty) =>
            difficulty == Difficulty.Hard ? 0.55f : 0.42f;

        /// <summary>Seconds for one full slow→fast→slow cycle.</summary>
        public static float TempoWavePeriod(Difficulty difficulty) =>
            difficulty == Difficulty.Hard ? 5.5f : 7.5f;

        /// <summary>
        /// Height index of the centered finish pad.
        /// Easy: pad 101 (between 100th and 101st). Hard: pad 201.
        /// </summary>
        public static int RaceFinishHeight(Difficulty difficulty) =>
            difficulty == Difficulty.Hard ? 200 : 100;

        /// <summary>
        /// Seconds between AI jumps before creature stat modifiers.
        /// Hard AI is a bit snappier.
        /// </summary>
        public static float RaceAiBaseJumpInterval(Difficulty difficulty) =>
            difficulty == Difficulty.Hard ? 0.38f : 0.48f;

        /// <summary>
        /// On Easy Combatant, the pad you stand on before a bee gap is always solid.
        /// </summary>
        public static bool CombatantEasySolidBeforeBee(Difficulty difficulty) =>
            difficulty == Difficulty.Easy;

        /// <summary>How often a bee gap appears (every Nth pad after the start).</summary>
        public static int CombatantBeeEveryNPads(Difficulty difficulty) =>
            difficulty == Difficulty.Hard ? 3 : 4;

        public static GameMode ClampMode(int raw)
        {
            // Legacy: 2 = Storm → Blackout. 3 = Race. 4 = Combatant. 5 = Time Trial. 6 = Tempo.
            if (raw == (int)GameMode.Tempo) return GameMode.Tempo;
            if (raw == (int)GameMode.TimeTrial) return GameMode.TimeTrial;
            if (raw == (int)GameMode.Combatant) return GameMode.Combatant;
            if (raw == (int)GameMode.Race) return GameMode.Race;
            if (raw == (int)GameMode.Blackout || raw == 2) return GameMode.Blackout;
            return GameMode.Classic;
        }

        public static Difficulty ClampDifficulty(int raw)
        {
            if (raw < 0) return Difficulty.Easy;
            if (raw > (int)Difficulty.Hard) return Difficulty.Easy;
            return (Difficulty)raw;
        }

        /// <summary>
        /// Accepts either real seconds (90/120/200) or legacy ordinals (0/1/2).
        /// </summary>
        public static TimeTrialLength ClampTimeTrialLength(int raw)
        {
            // Preferred storage: actual seconds.
            if (raw == 120 || raw == (int)TimeTrialLength.Seconds120)
                return TimeTrialLength.Seconds120;
            if (raw == 200 || raw == (int)TimeTrialLength.Seconds200)
                return TimeTrialLength.Seconds200;
            if (raw == 90 || raw == (int)TimeTrialLength.Seconds90)
                return TimeTrialLength.Seconds90;

            // Legacy ordinal storage from the first Time Trial build.
            if (raw == 1) return TimeTrialLength.Seconds120;
            if (raw == 2) return TimeTrialLength.Seconds200;
            return TimeTrialLength.Seconds90;
        }

        public static GameMode Clamp(int raw) => ClampMode(raw);
    }
}
