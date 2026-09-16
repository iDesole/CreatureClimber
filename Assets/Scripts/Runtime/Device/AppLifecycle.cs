using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Android / iOS lifecycle: pause mid-climb on background, flush saves, restore perf defaults.
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
            HandleBackground(!hasFocus);
        }

        void OnApplicationPause(bool pauseStatus)
        {
            // Fires on iOS/Android when suspended (home, call, recents).
            HandleBackground(pauseStatus);
        }

        void OnApplicationQuit()
        {
            SaveService.ForceFlush();
        }

        void HandleBackground(bool goingToBackground)
        {
            if (goingToBackground)
            {
                // Always persist prefs when the OS may kill the process next.
                SaveService.Flush();

                var gm = GameManager.Instance;
                if (gm != null &&
                    (gm.State == GameState.Playing || gm.State == GameState.Falling))
                {
                    gm.PauseGame(true);
                    pausedByOs = true;
                }
            }
            else
            {
                PlatformRuntime.ReapplyRuntimeDefaults();
                AudioService.Instance?.ApplyVolumes();

                // Leave the player on the pause screen — never auto-resume mid-jump.
                if (pausedByOs)
                    pausedByOs = false;
            }
        }
    }
}
