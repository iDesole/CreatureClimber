#if UNITY_EDITOR
using UnityEditor;

namespace CreatureClimb.Editor
{
    /// <summary>
    /// Shared asset paths used by editor setup menus.
    /// </summary>
    public static class EditorAssetPaths
    {
        public const string DataRoot = "Assets/Data";
        public const string ResourcesRoot = "Assets/Data/Resources";
        public const string ArtSpritesRoot = "Assets/Art/Sprites";

        public const string CreatureDatabase = ResourcesRoot + "/CreatureDatabase.asset";
        public const string PlatformDatabase = ResourcesRoot + "/PlatformDatabase.asset";
        public const string BackgroundDatabase = ResourcesRoot + "/BackgroundDatabase.asset";
        public const string CombatantDatabase = ResourcesRoot + "/CombatantDatabase.asset";
        public const string CurrencySettings = ResourcesRoot + "/CurrencySettings.asset";

        public const string CreaturesFolder = ArtSpritesRoot + "/Creatures";
        public const string PlatformsFolder = ArtSpritesRoot + "/Platforms";
        public const string BackgroundsFolder = ArtSpritesRoot + "/Backgrounds";
        public const string UiSpritesFolder = ArtSpritesRoot + "/UI";

        public const string DefaultCoinSprite = UiSpritesFolder + "/Griffin Coin.png";

        public static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder(DataRoot))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
                AssetDatabase.CreateFolder(DataRoot, "Resources");
        }
    }
}
#endif
