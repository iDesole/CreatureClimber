using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CreatureClimb
{
    /// <summary>One entry in the Creature Database — name, your sprite, stats.</summary>
    [Serializable]
    public class Creature
    {
        public string id = "";

        // Was "name" — that field path breaks Unity's SerializedProperty (FindPropertyRelative).
        [FormerlySerializedAs("name")]
        public string displayName = "New Creature";

        public string description = "";

        [Tooltip("Your sprite. Required for your art to show.")]
        public Sprite sprite;

        [Tooltip("Only used if Sprite is empty.")]
        public ProceduralCreatureSprite proceduralArt = ProceduralCreatureSprite.None;

        public Color tint = Color.white;

        [Tooltip("Sprite size multiplier. 1 = normal, 0.5 = half, 2 = double.")]
        public float scale = 1f;

        public float jumpSpeed = 1f;
        public float reactionTime = 1f;
        public float fallSpeed = 1f;
        public float scoreMultiplier = 1f;
        public float standOffsetY = 0.35f;
        public bool unlockedByDefault = true;

        [Tooltip("Coin cost in the Shop. 0 = free / always available.")]
        public int shopPrice = 0;

        [NonSerialized] Sprite cachedProcedural;

        public string Id
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(id)) return id.Trim();
                if (!string.IsNullOrWhiteSpace(displayName))
                    return displayName.Trim().ToLowerInvariant().Replace(' ', '_');
                return "creature";
            }
        }

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName) ? Id : displayName;

        public Sprite ResolveSprite()
        {
            if (sprite != null)
                return sprite;

            if (proceduralArt == ProceduralCreatureSprite.None)
                return null;

            if (cachedProcedural == null)
                cachedProcedural = PixelSpriteFactory.CreateCreatureSprite(proceduralArt);

            return cachedProcedural;
        }
    }
}
