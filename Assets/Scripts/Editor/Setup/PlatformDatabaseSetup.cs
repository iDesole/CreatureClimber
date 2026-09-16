#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CreatureClimb.Editor
{
    public static class PlatformDatabaseSetup
    {
        const string DatabasePath = EditorAssetPaths.PlatformDatabase;

        [MenuItem("Creature Climb/Platforms/Open Platform Database")]
        public static void OpenDatabase()
        {
            var db = EnsureEmptyDatabase();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }

        [MenuItem("Creature Climb/Platforms/Create Platform Database")]
        public static void CreateOrRefreshDatabase()
        {
            var db = EnsureEmptyDatabase();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
            Debug.Log(
                "Platform Database ready (empty).\n" +
                "Press + to add: Name, Solid sprite, Break sprite.");
        }

        static PlatformDatabase EnsureEmptyDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<PlatformDatabase>(DatabasePath);
            if (db != null) return db;

            EditorAssetPaths.EnsureResourcesFolder();
            db = ScriptableObject.CreateInstance<PlatformDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
            return db;
        }
    }
}
#endif
