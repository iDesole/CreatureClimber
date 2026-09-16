using System;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Live gameplay settings. Values persist through <see cref="SaveService"/>.
    /// </summary>
    public static class GameSettings
    {
        public static event Action Changed;

        public static bool MusicEnabled
        {
            get => SaveService.MusicEnabled;
            set
            {
                if (SaveService.MusicEnabled == value) return;
                SaveService.MusicEnabled = value;
                Notify();
            }
        }

        public static bool SfxEnabled
        {
            get => SaveService.SfxEnabled;
            set
            {
                if (SaveService.SfxEnabled == value) return;
                SaveService.SfxEnabled = value;
                Notify();
            }
        }

        public static bool PlatformsVisible
        {
            get => SaveService.PlatformsVisible;
            set
            {
                if (SaveService.PlatformsVisible == value) return;
                SaveService.PlatformsVisible = value;
                Notify();
            }
        }

        public static bool MirrorEnabled
        {
            get => SaveService.MirrorEnabled;
            set
            {
                if (SaveService.MirrorEnabled == value) return;
                SaveService.MirrorEnabled = value;
                Notify();
            }
        }

        /// <summary>+1 climb up, −1 hop down (Mirror).</summary>
        public static float ClimbSign => MirrorEnabled ? -1f : 1f;

        public static Vector3 ClimbUp => new Vector3(0f, ClimbSign, 0f);

        public static float PadWorldY(int heightIndex, float spacing) =>
            heightIndex * spacing * ClimbSign;

        /// <summary>World Y → progress along the climb (always increases as you hop).</summary>
        public static float AlongClimb(float worldY) => worldY * ClimbSign;

        public static int HeightIndexFromY(float worldY, float standOffset, float spacing)
        {
            if (spacing < 0.01f) spacing = 0.01f;
            // Stand is always on top of the pad (never flipped). Progress is pad Y.
            var progress = AlongClimb(worldY - standOffset);
            return Mathf.Max(0, Mathf.FloorToInt(progress / spacing + 0.001f));
        }

        /// <summary>Feet sit on top of the pad in both directions — Mirror only changes hop direction.</summary>
        public static Vector3 StandOffset(float amount) =>
            Vector3.up * amount;

        static void Notify()
        {
            SaveService.Flush();
            AudioService.Instance?.ApplyVolumes();
            Changed?.Invoke();
        }
    }
}
