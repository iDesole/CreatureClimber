using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Pause the climb when the OS backgrounds the app (call, home button, etc.).
    /// </summary>
    public class AppLifecycle : MonoBehaviour
    {
        static AppLifecycle instance;
        bool pausedByOs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (instance != null) return;
            var go = new GameObject("--- App Lifecycle ---");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AppLifecycle>();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!PlatformRuntime.IsMobile) return;
            HandlePause(!hasFocus, "focus");
        }

        void OnApplicationPause(bool pauseStatus)
        {
            // Fires on iOS/Android when suspended.
            HandlePause(pauseStatus, "pause");
        }

        void HandlePause(bool shouldPause, string reason)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (shouldPause)
            {
                if (gm.State == GameState.Playing || gm.State == GameState.Falling)
                {
                    gm.PauseGame(true);
                    pausedByOs = true;
                    Debug.Log($"[AppLifecycle] OS pause ({reason})");
                }
            }
            else if (pausedByOs)
            {
                // Leave the player in explicit pause UI — don't auto-resume mid-jump.
                pausedByOs = false;
                Debug.Log($"[AppLifecycle] OS resume ({reason}) — waiting for player");
            }
        }
    }
}
