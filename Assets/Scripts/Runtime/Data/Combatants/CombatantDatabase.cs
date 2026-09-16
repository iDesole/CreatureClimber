using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreatureClimb
{
    /// <summary>
    /// Your combatant list. Add rows with Sprite, Scale, Movement, Speed.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatantDatabase", menuName = "Creature Climb/Combatant Database", order = 4)]
    public class CombatantDatabase : ScriptableObject
    {
        public const string ResourcesPath = "CombatantDatabase";

        static CombatantDatabase cached;

        [Tooltip("Combatant varieties. Game picks among these when spawning hazards.")]
        [SerializeField] List<Combatant> combatants = new();

        public IReadOnlyList<Combatant> Combatants => combatants;
        public int Count => combatants?.Count ?? 0;

        public Combatant GetCombatant(int index)
        {
            if (combatants == null || combatants.Count == 0)
                return null;
            return combatants[Mathf.Clamp(index, 0, combatants.Count - 1)];
        }

        public Combatant PickRandom()
        {
            if (combatants == null || combatants.Count == 0)
                return null;
            return combatants[Random.Range(0, combatants.Count)];
        }

        /// <summary>
        /// Random combatant with the requested movement axis, or null if none match.
        /// </summary>
        public Combatant PickRandom(CombatantMovement movement)
        {
            if (combatants == null || combatants.Count == 0)
                return null;

            var matchCount = 0;
            for (var i = 0; i < combatants.Count; i++)
            {
                var c = combatants[i];
                if (c != null && c.movement == movement)
                    matchCount++;
            }

            if (matchCount == 0)
                return null;

            var pick = Random.Range(0, matchCount);
            for (var i = 0; i < combatants.Count; i++)
            {
                var c = combatants[i];
                if (c == null || c.movement != movement) continue;
                if (pick == 0) return c;
                pick--;
            }

            return null;
        }

        public List<Combatant> GetAll()
        {
            var list = new List<Combatant>();
            if (combatants == null) return list;
            foreach (var c in combatants)
            {
                if (c != null)
                    list.Add(c);
            }

            return list;
        }

        public static CombatantDatabase Load()
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<CombatantDatabase>(ResourcesPath);
            if (cached != null)
                return cached;

#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets("t:CombatantDatabase");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                    continue;
                var db = AssetDatabase.LoadAssetAtPath<CombatantDatabase>(path);
                if (db != null)
                {
                    cached = db;
                    return cached;
                }
            }
#endif
            return null;
        }

#if UNITY_EDITOR
        public void AddCombatant(Combatant c)
        {
            if (c == null) return;
            if (combatants == null)
                combatants = new List<Combatant>();
            combatants.Add(c);
            EditorUtility.SetDirty(this);
        }

        void OnValidate()
        {
            if (combatants == null) return;
            for (var i = 0; i < combatants.Count; i++)
            {
                var c = combatants[i];
                if (c == null) continue;

                if (string.IsNullOrWhiteSpace(c.id) && !string.IsNullOrWhiteSpace(c.displayName))
                    c.id = c.displayName.Trim().ToLowerInvariant().Replace(' ', '_');

                if (c.scale < 0.05f) c.scale = 0.05f;
                if (c.speed < 0.1f) c.speed = 0.1f;
            }
        }
#endif
    }
}
