using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// On Play: wire the scene and force Creature / Platform / Background databases
    /// so textures always come from your assets.
    /// </summary>
    public static class CreatureClimbBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnSceneLoaded()
        {
            PlatformRuntime.Initialize();

            var gm = CreatureClimbSceneBuilder.EnsureReady();
            if (gm == null) return;

            gm.ReloadDatabasesAndSelections();
            // Rebuild pads with database sprites after load.
            gm.ApplySelectionsNow();
        }
    }
}
