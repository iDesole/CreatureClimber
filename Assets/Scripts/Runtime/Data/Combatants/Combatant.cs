using System;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// One combatant variety for Combatant mode (bee, wasp, etc.).
    /// Assign sprite, scale, movement axis, and speed in the Combatant Database.
    /// </summary>
    [Serializable]
    public class Combatant
    {
        public string id = "";
        public string displayName = "Bee";

        [Tooltip("Sprite for this combatant.")]
        public Sprite sprite;

        [Tooltip("Visual size multiplier. 1 ≈ 1 world-unit tall, 2 = double, 0.5 = half.")]
        [Min(0.05f)]
        public float scale = 1f;

        [Tooltip("Patrol axis: left/right or up/down.")]
        public CombatantMovement movement = CombatantMovement.Horizontal;

        [Tooltip("Patrol speed in world units per second.")]
        [Min(0.1f)]
        public float speed = 2.4f;

        public string Id
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(id)) return id.Trim();
                if (!string.IsNullOrWhiteSpace(displayName))
                    return displayName.Trim().ToLowerInvariant().Replace(' ', '_');
                return "combatant";
            }
        }

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
    }
}
