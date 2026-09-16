namespace CreatureClimb
{
    /// <summary>
    /// Resolves silly racer handles for Race mode.
    /// Prefer <see cref="Creature.raceNickname"/> (editable in Creature Database);
    /// blank fields use built-in defaults.
    /// </summary>
    public static class RaceNicknames
    {
        public static string ForCreature(Creature creature)
        {
            if (creature == null) return "RIVAL";

            if (!string.IsNullOrWhiteSpace(creature.raceNickname))
                return creature.raceNickname.Trim().ToUpperInvariant();

            return DefaultForId(creature.Id, creature.DisplayName);
        }

        /// <summary>End-screen / flash when the opponent wins.</summary>
        public static string OpponentWinsLine(Creature opponent) =>
            $"{ForCreature(opponent)} WINS!";

        /// <summary>Built-in defaults when the database field is empty.</summary>
        public static string DefaultForId(string id, string displayName = null)
        {
            var key = id;
            if (string.IsNullOrWhiteSpace(key))
                key = displayName;
            if (string.IsNullOrWhiteSpace(key))
                return "RIVAL";

            key = key.Trim().ToLowerInvariant().Replace(' ', '_');

            return key switch
            {
                "frog" => "HOPPER",
                "spider" => "WEBBY",
                "snake" => "SLITHERYN",
                "capybara" => "CHILLIAM",
                "dog" => "BARKLEY",
                "cat" => "MIAOWENA",
                "rabbit" => "THUMPERINI",
                "goldfish" => "BUBBLES",
                "pony" => "NEIGHOMI",
                "otter" => "SPLASHINGTON",
                "owl" => "HOOTINI",
                "tiger" => "STRIPES",
                "lion" => "ROARETH",
                "pig" => "SNOUTLOAF",
                "chimp" => "BANANO",
                "duck" => "QUACKERS",
                "chicken" => "CLUCKY",
                "cow" => "MOOLYSSA",
                "panda" => "BAMBOOZLE",
                "fox" => "SNICKERS",
                _ => FunnyFallback(displayName ?? id)
            };
        }

        static string FunnyFallback(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "RIVAL";

            var clean = displayName.Trim().ToUpperInvariant();
            if (clean.Length <= 10)
                return clean + "Y";
            return clean;
        }
    }
}
