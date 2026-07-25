#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CreatureClimb.Editor
{
    public static class BackgroundDatabaseSetup
    {
        const string DatabasePath = "Assets/Resources/BackgroundDatabase.asset";

        [MenuItem("Creature Climb/Backgrounds/Open Background Database")]
        public static void OpenDatabase()
        {
            var db = EnsureEmptyDatabase();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }

        [MenuItem("Creature Climb/Backgrounds/Create Background Database")]
        public static void CreateOrRefreshDatabase()
        {
            var db = EnsureEmptyDatabase();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
            Debug.Log(
                "Background Database ready (empty).\n" +
                "Press + to add: Name, Sprite, Band Height, optional Near layer, Sky color.\n" +
                "Entries loop in list order as the player climbs.");
        }

        static BackgroundDatabase EnsureEmptyDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<BackgroundDatabase>(DatabasePath);
            if (db != null) return db;

            db = ScriptableObject.CreateInstance<BackgroundDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
            return db;
        }
    }
}
#endif
