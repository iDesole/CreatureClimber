namespace CreatureClimb
{
    /// <summary>
    /// How a platform or stage is unlocked. Pick one mode only.
    /// </summary>
    public enum ShopUnlockMode
    {
        /// <summary>Always available — not sold, not locked.</summary>
        Free = 0,

        /// <summary>Sold in the Shop for coins (shopPrice).</summary>
        BuyWithCoins = 1,

        /// <summary>Unlocks when the chosen character is owned. Hidden from Shop; locked in Customize until then.</summary>
        UnlockWithCharacter = 2
    }
}
