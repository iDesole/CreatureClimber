using UnityEngine;

namespace CreatureClimb
{
    /// <summary>Helpers for shop prices, character gates, and ownership from the databases.</summary>
    public static class ShopCatalog
    {
        public static int GetCreaturePrice(Creature c)
        {
            if (c == null) return 0;
            return c.shopPrice < 0 ? 0 : c.shopPrice;
        }

        public static ShopUnlockMode GetMode(Platform p) =>
            p != null ? p.ResolvedShopMode : ShopUnlockMode.Free;

        public static ShopUnlockMode GetMode(Background b) =>
            b != null ? b.ResolvedShopMode : ShopUnlockMode.Free;

        public static int GetPlatformPrice(Platform p)
        {
            if (p == null) return 0;
            if (GetMode(p) != ShopUnlockMode.BuyWithCoins) return 0;
            return p.shopPrice < 0 ? 0 : p.shopPrice;
        }

        public static int GetBackgroundPrice(Background b)
        {
            if (b == null) return 0;
            if (GetMode(b) != ShopUnlockMode.BuyWithCoins) return 0;
            return b.shopPrice < 0 ? 0 : b.shopPrice;
        }

        public static bool HasCharacterGate(Platform p) =>
            p != null && GetMode(p) == ShopUnlockMode.UnlockWithCharacter;

        public static bool HasCharacterGate(Background b) =>
            b != null && GetMode(b) == ShopUnlockMode.UnlockWithCharacter;

        /// <summary>Free + coin items appear in Shop. Character-gated never do.</summary>
        public static bool AppearsInShop(Platform p) =>
            p != null && GetMode(p) != ShopUnlockMode.UnlockWithCharacter;

        public static bool AppearsInShop(Background b) =>
            b != null && GetMode(b) != ShopUnlockMode.UnlockWithCharacter;

        public static bool AppearsInShop(Creature c) => c != null;

        public static bool OwnsCreature(Creature c)
        {
            if (c == null) return false;
            if (c.unlockedByDefault || GetCreaturePrice(c) <= 0)
                return true;
            return SaveService.IsUnlocked(SaveService.CreatureKey(c.Id));
        }

        public static bool OwnsCreatureId(string characterId, CreatureDatabase db = null)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return true;
            db = db != null ? db : CreatureDatabase.Load();
            if (db == null) return false;
            var id = characterId.Trim();
            for (var i = 0; i < db.Count; i++)
            {
                var c = db.GetCreature(i);
                if (c == null) continue;
                if (string.Equals(c.Id, id, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.displayName, id, System.StringComparison.OrdinalIgnoreCase))
                    return OwnsCreature(c);
            }

            return SaveService.IsUnlocked(SaveService.CreatureKey(id));
        }

        public static bool OwnsPlatform(Platform p, CreatureDatabase creatures = null)
        {
            if (p == null) return false;

            switch (GetMode(p))
            {
                case ShopUnlockMode.UnlockWithCharacter:
                    return OwnsCreatureId(p.unlockWithCharacterId, creatures);
                case ShopUnlockMode.BuyWithCoins:
                    if (GetPlatformPrice(p) <= 0) return true;
                    return SaveService.IsUnlocked(SaveService.PlatformKey(p.Id));
                default:
                    return true; // Free
            }
        }

        public static bool OwnsBackground(Background b, CreatureDatabase creatures = null)
        {
            if (b == null) return false;

            switch (GetMode(b))
            {
                case ShopUnlockMode.UnlockWithCharacter:
                    return OwnsCreatureId(b.unlockWithCharacterId, creatures);
                case ShopUnlockMode.BuyWithCoins:
                    if (GetBackgroundPrice(b) <= 0) return true;
                    return SaveService.IsUnlocked(SaveService.BackgroundKey(b.Id));
                default:
                    return true; // Free
            }
        }

        public static bool TryBuyCreature(Creature c)
        {
            if (c == null || OwnsCreature(c)) return false;
            var price = GetCreaturePrice(c);
            if (!SaveService.TrySpendCoins(price)) return false;
            SaveService.Unlock(SaveService.CreatureKey(c.Id));
            return true;
        }

        public static bool TryBuyPlatform(Platform p)
        {
            if (p == null || OwnsPlatform(p) || GetMode(p) != ShopUnlockMode.BuyWithCoins)
                return false;
            var price = GetPlatformPrice(p);
            if (!SaveService.TrySpendCoins(price)) return false;
            SaveService.Unlock(SaveService.PlatformKey(p.Id));
            return true;
        }

        public static bool TryBuyBackground(Background b)
        {
            if (b == null || OwnsBackground(b) || GetMode(b) != ShopUnlockMode.BuyWithCoins)
                return false;
            var price = GetBackgroundPrice(b);
            if (!SaveService.TrySpendCoins(price)) return false;
            SaveService.Unlock(SaveService.BackgroundKey(b.Id));
            return true;
        }

        public static string CharacterGateLabel(string characterId, CreatureDatabase db = null)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return "";
            db = db != null ? db : CreatureDatabase.Load();
            if (db == null) return characterId;
            var id = characterId.Trim();
            for (var i = 0; i < db.Count; i++)
            {
                var c = db.GetCreature(i);
                if (c == null) continue;
                if (string.Equals(c.Id, id, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.displayName, id, System.StringComparison.OrdinalIgnoreCase))
                    return c.DisplayName;
            }

            return id;
        }

        /// <summary>Popup options for character picker: "None" + each creature display name; values are creature ids.</summary>
        public static void GetCreatureChoices(out string[] labels, out string[] ids)
        {
            var db = CreatureDatabase.Load();
            if (db == null || db.Count == 0)
            {
                labels = new[] { "(no creatures)" };
                ids = new[] { "" };
                return;
            }

            labels = new string[db.Count];
            ids = new string[db.Count];
            for (var i = 0; i < db.Count; i++)
            {
                var c = db.GetCreature(i);
                if (c == null)
                {
                    labels[i] = $"Creature {i}";
                    ids[i] = "";
                }
                else
                {
                    labels[i] = c.DisplayName;
                    ids[i] = c.Id;
                }
            }
        }
    }
}
