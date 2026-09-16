using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Short, throttled mobile vibration. No-ops on editor/desktop.
    /// Avoids Android's long <see cref="Handheld.Vibrate"/> pulse on every jump.
    /// </summary>
    public static class HapticService
    {
        public enum Strength
        {
            Light,
            Medium,
            Heavy
        }

        // Prevent spam from rapid jumps / UI taps (battery + store review risk).
        const float MinIntervalLight = 0.07f;
        const float MinIntervalMedium = 0.11f;
        const float MinIntervalHeavy = 0.18f;

        static float nextAllowedAt;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int androidSdk = -1;
        static bool vibratorResolved;
#endif

        public static void Pulse(Strength strength = Strength.Light)
        {
            if (!SaveService.HapticsEnabled) return;
            if (!PlatformRuntime.SupportsHaptics && !Application.isMobilePlatform) return;

            var now = Time.unscaledTime;
            var gap = strength switch
            {
                Strength.Heavy => MinIntervalHeavy,
                Strength.Medium => MinIntervalMedium,
                _ => MinIntervalLight
            };
            if (now < nextAllowedAt) return;
            nextAllowedAt = now + gap;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!SystemInfo.supportsVibration) return;
            var ms = strength switch
            {
                Strength.Heavy => 40L,
                Strength.Medium => 28L,
                _ => 16L
            };
            VibrateAndroid(ms);
#elif UNITY_IOS && !UNITY_EDITOR
            if (!SystemInfo.supportsVibration) return;
            // iOS only exposes the generic pulse through Handheld.
            Handheld.Vibrate();
#else
            // Editor / other: no-op (keeps desktop quiet).
#endif
        }

        public static void Jump() => Pulse(Strength.Light);
        public static void Break() => Pulse(Strength.Medium);
        public static void Land() => Pulse(Strength.Light);
        public static void Fail() => Pulse(Strength.Heavy);
        public static void Recover() => Pulse(Strength.Medium);

#if UNITY_ANDROID && !UNITY_EDITOR
        static void VibrateAndroid(long milliseconds)
        {
            try
            {
                EnsureVibrator();
                if (vibrator == null) return;

                if (androidSdk >= 26)
                {
                    using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = effectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot",
                        milliseconds,
                        effectClass.GetStatic<int>("DEFAULT_AMPLITUDE"));
                    vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception)
            {
                // Fallback — still throttled by nextAllowedAt.
                try { Handheld.Vibrate(); }
                catch { /* ignored */ }
            }
        }

        static void EnsureVibrator()
        {
            if (vibratorResolved) return;
            vibratorResolved = true;

            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                androidSdk = version.GetStatic<int>("SDK_INT");

                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return;

                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            catch
            {
                vibrator = null;
            }
        }
#endif
    }
}
