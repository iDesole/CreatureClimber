using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Single entry point for juice: audio + haptics + camera shake.
    /// Call from gameplay; never branch on platform here.
    /// </summary>
    public static class FeedbackService
    {
        public static void Jump()
        {
            AudioService.Instance?.PlayJump();
            HapticService.Jump();
            CameraFollow.Shake(0.04f, 0.08f);
        }

        public static void LandSoft()
        {
            AudioService.Instance?.PlayLand();
            HapticService.Land();
            CameraFollow.Shake(0.03f, 0.06f);
        }

        public static void BreakPad()
        {
            AudioService.Instance?.PlayBreak();
            HapticService.Break();
            CameraFollow.Shake(0.1f, 0.14f);
        }

        public static void FallStart()
        {
            AudioService.Instance?.PlayFail();
            HapticService.Fail();
            CameraFollow.Shake(0.12f, 0.18f);
        }

        public static void Recover()
        {
            AudioService.Instance?.PlayRecover();
            HapticService.Recover();
            CameraFollow.Shake(0.08f, 0.12f);
        }

        public static void GameOver()
        {
            AudioService.Instance?.PlayGameOver();
            HapticService.Fail();
            CameraFollow.Shake(0.14f, 0.22f);
        }

        public static void UiSelect()
        {
            AudioService.Instance?.PlayUi();
            HapticService.Pulse(HapticService.Strength.Light);
        }

        public static void ScoreTick()
        {
            AudioService.Instance?.PlayScoreTick();
        }

        /// <summary>Storm strike cue — soft audio + light haptic (no heavy buzz with the visual).</summary>
        public static void StormLightning()
        {
            AudioService.Instance?.PlayStormThunder();
            HapticService.Pulse(HapticService.Strength.Light);
        }
    }
}

