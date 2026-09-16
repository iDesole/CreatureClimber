using System;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// One platform you add with +.
    /// Name + solid sprite + break sprite.
    /// Breakable pads: wrong-side miss, or 0.75s land countdown then shatter.
    /// </summary>
    [Serializable]
    public class Platform
    {
        public string id = "";
        public string displayName = "";

        [Tooltip("Sprite for the solid version — never breaks.")]
        public Sprite solidSprite;

        [Tooltip("Sprite for the break version — shatters on miss.")]
        public Sprite breakSprite;

        [Tooltip("Sprite size multiplier. 1 = normal, 0.5 = half, 2 = double.")]
        public float scale = 1f;

        [Tooltip("Size on Customize / Shop tiles. 1 = default. Does not change in-game size.")]
        [Range(0.25f, 3f)]
        public float uiScale = 1f;

        /// <summary>Roster tile size. Missing/legacy 0 reads as 1.</summary>
        public float RosterUiScale => uiScale < 0.05f ? 1f : Mathf.Clamp(uiScale, 0.25f, 3f);

        public float standOffsetY = 0.35f;

        [Tooltip("Nudge this pad up (+) or down (−) so the creature sits on the landing surface.")]
        [Range(-1.5f, 1.5f)]
        public float spawnOffsetY = 0f;

        // Drawn by PlatformDatabaseEditor as exclusive Mode → Price OR Character.
        public ShopUnlockMode shopMode = ShopUnlockMode.Free;
        [Min(0)] public int shopPrice = 0;
        public string unlockWithCharacterId = "";

        public string Id
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(id)) return id.Trim();
                if (!string.IsNullOrWhiteSpace(displayName))
                    return displayName.Trim().ToLowerInvariant().Replace(' ', '_');
                return "platform";
            }
        }

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName) ? Id : displayName;

        /// <summary>Resolves legacy data that only had price / character fields set.</summary>
        public ShopUnlockMode ResolvedShopMode
        {
            get
            {
                // Explicit non-Free modes always win.
                if (shopMode == ShopUnlockMode.BuyWithCoins ||
                    shopMode == ShopUnlockMode.UnlockWithCharacter)
                    return shopMode;

                // Legacy assets: Free default but fields were filled.
                if (!string.IsNullOrWhiteSpace(unlockWithCharacterId))
                    return ShopUnlockMode.UnlockWithCharacter;
                if (shopPrice > 0)
                    return ShopUnlockMode.BuyWithCoins;
                return ShopUnlockMode.Free;
            }
        }

        public Sprite ResolveSprite(bool canBreak)
        {
            if (canBreak)
            {
                if (breakSprite != null) return breakSprite;
                if (solidSprite != null) return solidSprite;
            }
            else
            {
                if (solidSprite != null) return solidSprite;
                if (breakSprite != null) return breakSprite;
            }

            return PixelSpriteFactory.CreateLeafSprite();
        }
    }
}
