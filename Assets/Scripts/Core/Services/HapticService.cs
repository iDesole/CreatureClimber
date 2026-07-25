using UnityEngine;

namespace CreatureClimb
{
    /// <summary>Mobile vibration; no-ops on editor/desktop.</summary>
    public static class HapticService
    {
        public enum Strength
        {
            Light,
            Medium,
            Heavy
        }

        public static void Pulse(Strength strength = Strength.Light)
        {
            if (!SaveService.HapticsEnabled) return;
            if (!PlatformRuntime.SupportsHaptics && !Application.isMobilePlatform) return;

#if UNITY_IOS || UNITY_ANDROID
            if (!SystemInfo.supportsVibration) return;
            _ = strength;
            Handheld.Vibrate();
#else
            _ = strength;
#endif
        }

        public static void Jump() => Pulse(Strength.Light);
        public static void Break() => Pulse(Strength.Medium);
        public static void Land() => Pulse(Strength.Light);
        public static void Fail() => Pulse(Strength.Heavy);
        public static void Recover() => Pulse(Strength.Medium);
    }
}
