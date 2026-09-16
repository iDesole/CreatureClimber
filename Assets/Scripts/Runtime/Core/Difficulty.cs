namespace CreatureClimb
{
    /// <summary>
    /// Easy / Hard toggle layered on top of <see cref="GameMode"/>.
    /// </summary>
    public enum Difficulty
    {
        /// <summary>Mixed solid + breakable pads; more forgiving.</summary>
        Easy = 0,

        /// <summary>Every climb pad is breakable — jump before it shatters.</summary>
        Hard = 1
    }
}
