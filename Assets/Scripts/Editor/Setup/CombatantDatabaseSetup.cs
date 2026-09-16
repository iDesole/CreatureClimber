#if UNITY_EDITOR
using CreatureClimb;
using UnityEditor;
using UnityEngine;

namespace CreatureClimb.Editor
{
    public static class CombatantDatabaseSetup
    {
        const string DatabasePath = EditorAssetPaths.CombatantDatabase;

        [MenuItem("Creature Climb/Combatants/Open Combatant Database")]
        public static void OpenDatabase()
        {
            var db = EnsureDatabase();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }

        [MenuItem("Creature Climb/Combatants/Create Combatant Database")]
        public static void CreateDatabase()
        {
            var db = EnsureDatabase();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
            Debug.Log(
                "Combatant Database ready at Data/Resources/CombatantDatabase.\n" +
                "Add rows: Name, Sprite, Scale, Movement (Horizontal/Vertical), Speed.");
        }

        static CombatantDatabase EnsureDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<CombatantDatabase>(DatabasePath);
            if (db != null) return db;

            EditorAssetPaths.EnsureResourcesFolder();
            db = ScriptableObject.CreateInstance<CombatantDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
            return db;
        }
    }
}
#endif
