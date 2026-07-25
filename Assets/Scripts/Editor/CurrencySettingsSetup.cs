#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CreatureClimb.Editor
{
    public static class CurrencySettingsSetup
    {
        const string DatabasePath = "Assets/Resources/CurrencySettings.asset";
        const string DefaultCoinPath = "Assets/Sprites/Coin.png";

        [MenuItem("Creature Climb/Currency/Open Currency Settings")]
        public static void OpenSettings()
        {
            var settings = EnsureSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Creature Climb/Currency/Create Currency Settings")]
        public static void CreateOrRefreshSettings()
        {
            var settings = EnsureSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
            Debug.Log(
                "Currency Settings ready at Resources/CurrencySettings.\n" +
                "Assign Coin Sprite, World Size, HUD/UI sizes, and Spawn Every N Pads.");
        }

        static CurrencySettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CurrencySettings>(DatabasePath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                settings = ScriptableObject.CreateInstance<CurrencySettings>();
                AssetDatabase.CreateAsset(settings, DatabasePath);
            }

            // Fill default sprite if empty.
            var so = new SerializedObject(settings);
            var spriteProp = so.FindProperty("coinSprite");
            if (spriteProp != null && spriteProp.objectReferenceValue == null)
            {
                var coin = LoadDefaultCoinSprite();
                if (coin != null)
                {
                    spriteProp.objectReferenceValue = coin;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                }
            }

            AssetDatabase.SaveAssets();
            return settings;
        }

        static Sprite LoadDefaultCoinSprite()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(DefaultCoinPath);
            if (assets == null) return null;
            foreach (var a in assets)
            {
                if (a is Sprite s)
                    return s;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(DefaultCoinPath);
        }
    }
}
#endif
