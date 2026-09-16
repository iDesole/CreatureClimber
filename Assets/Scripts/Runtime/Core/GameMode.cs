namespace CreatureClimb
{
    /// <summary>
    /// Climb rule set. Selected from the main menu Game Mode screen.
    /// </summary>
    public enum GameMode
    {
        /// <summary>Standard climb — solid pads mixed with breakables.</summary>
        Classic = 0,

        /// <summary>Classic rules plus brief blackout strikes that hide the screen.</summary>
        Blackout = 1,

        // Value 2 reserved: legacy Storm saves map to Blackout.

        /// <summary>Race an AI climber to a finish height.</summary>
        Race = 3,

        /// <summary>Climb while dodging flying combatants (bees, etc.).</summary>
        Combatant = 4,

        /// <summary>Score as high as you can before the clock hits zero.</summary>
        TimeTrial = 5,

        /// <summary>Screen auto-scrolls with a rising/falling tempo — keep climbing or get left behind.</summary>
        Tempo = 6
    }
}
