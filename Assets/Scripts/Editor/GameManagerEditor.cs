#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CreatureClimb.Editor
{
    [CustomEditor(typeof(GameManager))]
    public class GameManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var gm = (GameManager)target;
            serializedObject.Update();

            var creatureDbProp = serializedObject.FindProperty("creatureDatabase");
            var playAsProp = serializedObject.FindProperty("playAsCreatureIndex");
            var platformDbProp = serializedObject.FindProperty("platformDatabase");
            var platformIndexProp = serializedObject.FindProperty("activePlatformIndex");

            // --- Creature pick ---
            EditorGUILayout.LabelField("Play As (Creature Database)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(creatureDbProp, new GUIContent("Creature Database"));

            var creatureDb = creatureDbProp.objectReferenceValue as CreatureDatabase;
            if (creatureDb == null)
            {
                creatureDb = CreatureDatabase.Load();
                if (creatureDb != null)
                    creatureDbProp.objectReferenceValue = creatureDb;
            }

            EditorGUI.BeginChangeCheck();
            DrawIndexPopup(creatureDb != null ? creatureDb.GetAll().ConvertAll(c => c.DisplayName) : null, playAsProp, "Character");
            var creatureChanged = EditorGUI.EndChangeCheck();

            // --- Platform family pick ---
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Platforms (Platform Database)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Pick Character + Platform here (or in Play:\n" +
                "← → character,  ↑ ↓ platform).\n" +
                "Choices are saved between plays.\n" +
                "Backgrounds: Creature Climb → Backgrounds → Open Database\n" +
                "(loops in list order as you climb).",
                MessageType.Info);
            EditorGUILayout.PropertyField(platformDbProp, new GUIContent("Platform Database"));

            var platformDb = platformDbProp.objectReferenceValue as PlatformDatabase;
            if (platformDb == null)
            {
                platformDb = PlatformDatabase.Load();
                if (platformDb != null)
                    platformDbProp.objectReferenceValue = platformDb;
            }

            EditorGUI.BeginChangeCheck();
            if (platformDb != null && platformDb.Count > 0)
            {
                DrawIndexPopup(
                    platformDb.GetAll().ConvertAll(p => p.DisplayName),
                    platformIndexProp,
                    "Active Platform");
            }
            else
            {
                EditorGUILayout.HelpBox("Platform Database is empty. Open it and press + to add one.", MessageType.Warning);
            }

            var platformChanged = EditorGUI.EndChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Platform Database"))
                    PlatformDatabaseSetup.OpenDatabase();

                if (platformDb == null)
                {
                    if (GUILayout.Button("Create Platform Database"))
                    {
                        PlatformDatabaseSetup.CreateOrRefreshDatabase();
                        platformDbProp.objectReferenceValue = PlatformDatabase.Load();
                    }
                }
            }

            // --- Scene refs ---
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Scene refs", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("leafSpawner"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("player"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("backgroundScroller"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gameUI"));

            if (!gm.IsWired && GUILayout.Button("Setup MVP Scene (wire refs)"))
            {
                CreatureClimbSceneSetup.SetupScene();
                return;
            }

            EditorGUILayout.Space(6);
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "creatureDatabase",
                "playAsCreatureIndex",
                "platformDatabase",
                "activePlatformIndex",
                "backgroundDatabase",
                "leafSpawner",
                "player",
                "backgroundScroller",
                "gameUI");

            serializedObject.ApplyModifiedProperties();

            // Persist inspector picks so Play Mode doesn't reset to Frog / first platform.
            if (creatureChanged || platformChanged)
            {
                SaveService.CreatureIndex = playAsProp.intValue;
                SaveService.PlatformIndex = platformIndexProp.intValue;

                if (Application.isPlaying)
                    gm.ApplySelectionsNow();
            }

            if (GUILayout.Button(Application.isPlaying ? "Apply Selections Now" : "Save Selections"))
            {
                SaveService.CreatureIndex = playAsProp.intValue;
                SaveService.PlatformIndex = platformIndexProp.intValue;
                if (Application.isPlaying)
                    gm.ApplySelectionsNow();
            }
        }

        static void DrawIndexPopup(System.Collections.Generic.List<string> names, SerializedProperty indexProp, string label)
        {
            if (indexProp == null) return;

            if (names == null || names.Count == 0)
            {
                EditorGUILayout.PropertyField(indexProp, new GUIContent(label));
                return;
            }

            var idx = Mathf.Clamp(indexProp.intValue, 0, names.Count - 1);
            var next = EditorGUILayout.Popup(label, idx, names.ToArray());
            if (next != indexProp.intValue)
                indexProp.intValue = next;
        }
    }
}
#endif
