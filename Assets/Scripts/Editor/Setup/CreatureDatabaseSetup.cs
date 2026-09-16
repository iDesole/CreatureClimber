#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CreatureClimb.Editor
{
    public static class CreatureDatabaseSetup
    {
        const string DatabasePath = EditorAssetPaths.CreatureDatabase;

        [MenuItem("Creature Climb/Creatures/Open Creature Database")]
        public static void OpenDatabase()
        {
            var db = EnsureDatabase();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }

        [MenuItem("Creature Climb/Creatures/Create Creature Database")]
        public static void CreateOrRefreshDatabase()
        {
            var db = EnsureDatabase();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
            Debug.Log(
                "Creature Database ready.\n" +
                "Press + to add creatures: name, sprite, stats, shop price.");
        }

        static CreatureDatabase EnsureDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<CreatureDatabase>(DatabasePath);
            if (db != null) return db;

            db = CreatureDatabase.Load();
            if (db != null) return db;

            EditorAssetPaths.EnsureResourcesFolder();

            db = ScriptableObject.CreateInstance<CreatureDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
            return db;
        }
    }
}
#endif
