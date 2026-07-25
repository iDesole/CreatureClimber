using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreatureClimb
{
    /// <summary>
    /// Your background list. Add rows with Name + Sprite; the scroller loops them as you climb.
    /// </summary>
    [CreateAssetMenu(fileName = "BackgroundDatabase", menuName = "Creature Climb/Background Database", order = 2)]
    public class BackgroundDatabase : ScriptableObject
    {
        public const string ResourcesPath = "BackgroundDatabase";

        [Tooltip("Backgrounds loop in this order as the camera climbs. + to add.")]
        [SerializeField] List<Background> backgrounds = new();

        public IReadOnlyList<Background> Backgrounds => backgrounds;
        public int Count => backgrounds?.Count ?? 0;

        public Background GetBackground(int index)
        {
            if (backgrounds == null || backgrounds.Count == 0)
                return null;

            // Always wrap so looping is safe at any height.
            var i = index % backgrounds.Count;
            if (i < 0) i += backgrounds.Count;
            return backgrounds[i];
        }

        /// <summary>Sum of all band heights in one full loop of the list.</summary>
        public float CycleHeight()
        {
            if (backgrounds == null || backgrounds.Count == 0)
                return 10f;

            var sum = 0f;
            for (var i = 0; i < backgrounds.Count; i++)
            {
                var b = backgrounds[i];
                sum += b != null ? b.ResolvedHeight : 10f;
            }

            return Mathf.Max(1f, sum);
        }

        /// <summary>
        /// Which database entry is showing at world Y (looping bands with per-entry heights).
        /// <paramref name="bandIndex"/> is absolute (grows as you climb) so the scroller can place unique bands.
        /// </summary>
        public Background GetBackgroundAtWorldY(float worldY, out int bandIndex, out float bandBottomY)
        {
            bandIndex = 0;
            bandBottomY = 0f;

            if (backgrounds == null || backgrounds.Count == 0)
                return null;

            var count = backgrounds.Count;
            var cycle = CycleHeight();

            // How many full loops below this Y, then walk within the cycle (O(count)).
            var loops = Mathf.FloorToInt(worldY / cycle);
            var yInCycle = worldY - loops * cycle;
            if (yInCycle < 0f)
            {
                // Floor for negatives can leave a full cycle remainder of 0 at exact boundaries.
                loops--;
                yInCycle = worldY - loops * cycle;
            }

            var local = 0f;
            for (var i = 0; i < count; i++)
            {
                var bg = backgrounds[i];
                var h = bg != null ? bg.ResolvedHeight : 10f;
                if (yInCycle < local + h || i == count - 1)
                {
                    bandIndex = loops * count + i;
                    bandBottomY = loops * cycle + local;
                    return bg;
                }

                local += h;
            }

            bandIndex = loops * count;
            bandBottomY = loops * cycle;
            return backgrounds[0];
        }

        public List<Background> GetAll()
        {
            var list = new List<Background>();
            if (backgrounds == null) return list;
            foreach (var b in backgrounds)
            {
                if (b != null)
                    list.Add(b);
            }

            return list;
        }

#if UNITY_EDITOR
        public void AddBackground(Background background)
        {
            if (background == null) return;
            if (backgrounds == null)
                backgrounds = new List<Background>();
            backgrounds.Add(background);
            EditorUtility.SetDirty(this);
        }
#endif

        public static BackgroundDatabase Load()
        {
            var fromResources = Resources.Load<BackgroundDatabase>(ResourcesPath);
            if (fromResources != null)
                return fromResources;

#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets("t:BackgroundDatabase");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var db = AssetDatabase.LoadAssetAtPath<BackgroundDatabase>(path);
                if (db != null)
                    return db;
            }
#endif

            return null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (backgrounds == null) return;

            for (var i = 0; i < backgrounds.Count; i++)
            {
                var b = backgrounds[i];
                if (b == null) continue;

                if (string.IsNullOrWhiteSpace(b.id) && !string.IsNullOrWhiteSpace(b.displayName))
                    b.id = b.displayName.Trim().ToLowerInvariant().Replace(' ', '_');

                if (b.worldHeight < 1f)
                    b.worldHeight = 1f;
            }
        }
#endif
    }
}
