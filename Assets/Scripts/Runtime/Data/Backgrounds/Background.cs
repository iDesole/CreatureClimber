using System;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// One entry in the Background Database — art that tiles vertically as you climb.
    /// Entries loop in list order (0 → 1 → 2 → … → 0).
    /// </summary>
    [Serializable]
    public class Background
    {
        public string id = "";
        public string displayName = "";

        [Tooltip("Main sprite for this vertical band. Required for your art to show.")]
        public Sprite sprite;

        public Color tint = Color.white;

        [Tooltip("World-unit height of this band before the next background appears.")]
        [Min(1f)]
        public float worldHeight = 10f;

        public int sortingOrder = -10;

        // Drawn by BackgroundDatabaseEditor as exclusive Mode → Price OR Character.
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
                return "background";
            }
        }

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName) ? Id : displayName;

        /// <summary>Resolves legacy data that only had price / character fields set.</summary>
        public ShopUnlockMode ResolvedShopMode
        {
            get
            {
                if (shopMode == ShopUnlockMode.Free &&
                    !string.IsNullOrWhiteSpace(unlockWithCharacterId))
                    return ShopUnlockMode.UnlockWithCharacter;
                if (shopMode == ShopUnlockMode.Free && shopPrice > 0)
                    return ShopUnlockMode.BuyWithCoins;
                return shopMode;
            }
        }

        public Sprite ResolveSprite()
        {
            if (sprite != null) return sprite;
            return PixelSpriteFactory.CreateSolid(8, 8, new Color(0.12f, 0.28f, 0.16f));
        }

        public float ResolvedHeight => Mathf.Max(1f, worldHeight);
    }
}
