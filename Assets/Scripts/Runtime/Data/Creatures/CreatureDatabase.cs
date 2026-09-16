using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreatureClimb
{
    /// <summary>
    /// Your creature list. Add rows here with Name + Sprite + Stats.
    /// GameManager picks one of these to play as — nothing is invented at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "CreatureDatabase", menuName = "Creature Climb/Creature Database", order = 0)]
    public class CreatureDatabase : ScriptableObject
    {
        public const string ResourcesPath = "CreatureDatabase";

        static CreatureDatabase cached;

        [Tooltip("Which creature is used when GameManager index is not set.")]
        [SerializeField] int defaultCreatureIndex;

        [Tooltip("Your creatures. + to add. Assign Sprite (e.g. Frog Sprite).")]
        [SerializeField] List<Creature> creatures = new();

        public IReadOnlyList<Creature> Creatures => creatures;
        public int Count => creatures?.Count ?? 0;

        public Creature GetCreature(int index)
        {
            if (creatures == null || creatures.Count == 0)
                return null;

            return creatures[Mathf.Clamp(index, 0, creatures.Count - 1)];
        }

        public Creature DefaultCreature => GetCreature(defaultCreatureIndex);

        public List<Creature> GetAll()
        {
            var list = new List<Creature>();
            if (creatures == null) return list;
            foreach (var c in creatures)
            {
                if (c != null)
                    list.Add(c);
            }

            return list;
        }

#if UNITY_EDITOR
        public void AddCreature(Creature creature)
        {
            if (creature == null) return;
            if (creatures == null)
                creatures = new List<Creature>();
            creatures.Add(creature);
            EditorUtility.SetDirty(this);
        }
#endif

        /// <summary>
        /// Loads YOUR database asset. Never fabricates creatures.
        /// Prefers Resources/CreatureDatabase, then any CreatureDatabase in the project (editor).
        /// </summary>
        public static CreatureDatabase Load()
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<CreatureDatabase>(ResourcesPath);
            if (cached != null)
                return cached;

#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets("t:CreatureDatabase");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                // Skip scene-embedded / non-asset paths.
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                    continue;
                var db = AssetDatabase.LoadAssetAtPath<CreatureDatabase>(path);
                if (db != null)
                {
                    cached = db;
                    return cached;
                }
            }
#endif

            Debug.LogError(
                "[CreatureDatabase] No database found. " +
                "Create one (Create → Creature Climb → Creature Database), " +
                "add your creatures, and assign it on GameManager " +
                "or place it at Resources/CreatureDatabase.");
            return null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (creatures == null) return;

            for (var i = 0; i < creatures.Count; i++)
            {
                var c = creatures[i];
                if (c == null) continue;

                if (string.IsNullOrWhiteSpace(c.id) && !string.IsNullOrWhiteSpace(c.displayName))
                    c.id = c.displayName.Trim().ToLowerInvariant().Replace(' ', '_');
            }

            if (creatures.Count > 0)
                defaultCreatureIndex = Mathf.Clamp(defaultCreatureIndex, 0, creatures.Count - 1);
        }
#endif
    }
}
