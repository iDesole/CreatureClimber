using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreatureClimb
{
    /// <summary>
    /// Coin art, sizes, and world spawn rate. Edit via
    /// Creature Climb → Currency → Open Currency Settings.
    /// </summary>
    [CreateAssetMenu(fileName = "CurrencySettings", menuName = "Creature Climb/Currency Settings", order = 3)]
    public class CurrencySettings : ScriptableObject
    {
        public const string ResourcesPath = "CurrencySettings";

        static CurrencySettings cached;

        [Header("Art")]
        [Tooltip("Coin sprite for world pickups, HUD, and shop UI.")]
        [SerializeField] Sprite coinSprite;

        [Header("Size")]
        [Tooltip("World pickup size on platforms (world units).")]
        [Min(0.05f)]
        [SerializeField] float worldSize = 0.55f;

        [Tooltip("Top-right HUD coin icon size (UI pixels).")]
        [Min(8f)]
        [SerializeField] float hudIconSize = 32f;

        [Tooltip("Shop wallet and price-tag coin icon size (UI pixels).")]
        [Min(8f)]
        [SerializeField] float uiIconSize = 28f;

        [Header("Spawning")]
        [Tooltip("Place a coin every N platforms (height index). 3 = every 3rd pad. 1 = every pad. 0 = never.")]
        [Min(0)]
        [SerializeField] int spawnEveryNPads = 3;

        [Tooltip("When a pad is eligible (every N), chance it actually gets a coin. 1 = always.")]
        [Range(0f, 1f)]
        [SerializeField] float spawnChance = 1f;

        public Sprite CoinSprite => coinSprite;

        public float WorldSize => Mathf.Max(0.05f, worldSize);

        public float HudIconSize => Mathf.Max(8f, hudIconSize);

        public float UiIconSize => Mathf.Max(8f, uiIconSize);

        public int SpawnEveryNPads => Mathf.Max(0, spawnEveryNPads);

        public float SpawnChance => Mathf.Clamp01(spawnChance);

        /// <summary>True if this height should get a coin (starting pad never does).</summary>
        public bool ShouldSpawnCoin(int heightIndex)
        {
            if (heightIndex <= 0) return false;
            if (SpawnEveryNPads <= 0) return false;
            if (heightIndex % SpawnEveryNPads != 0) return false;
            if (SpawnChance >= 0.999f) return true;
            if (SpawnChance <= 0.001f) return false;
            return Random.value < SpawnChance;
        }

        /// <summary>
        /// Loads Resources/CurrencySettings, then any CurrencySettings asset (editor).
        /// </summary>
        public static CurrencySettings Load()
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<CurrencySettings>(ResourcesPath);
            if (cached != null)
                return cached;

#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets("t:CurrencySettings");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                    continue;
                var asset = AssetDatabase.LoadAssetAtPath<CurrencySettings>(path);
                if (asset != null)
                {
                    cached = asset;
                    return cached;
                }
            }
#endif

            return null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            worldSize = Mathf.Max(0.05f, worldSize);
            hudIconSize = Mathf.Max(8f, hudIconSize);
            uiIconSize = Mathf.Max(8f, uiIconSize);
            spawnEveryNPads = Mathf.Max(0, spawnEveryNPads);
            spawnChance = Mathf.Clamp01(spawnChance);
            cached = this;
        }
#endif
    }
}
