#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CreatureClimb.Editor
{
    [CustomEditor(typeof(LeafSpawner))]
    public class LeafSpawnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Platform Spawn", EditorStyles.boldLabel);

            var breakable = serializedObject.FindProperty("breakableChance");
            if (breakable != null)
            {
                EditorGUILayout.Slider(breakable, 0f, 1f, new GUIContent(
                    "Breakable Chance",
                    "How often break platforms spawn (0 = all solid, 1 = all breakable). Default 0.75 = 75%."));

                var pct = Mathf.RoundToInt(breakable.floatValue * 100f);
                EditorGUILayout.LabelField($"→ {pct}% breakable  ·  {100 - pct}% solid");
            }

            var cull = serializedObject.FindProperty("destroyPlatformsBelow");
            if (cull != null)
            {
                EditorGUILayout.PropertyField(cull, new GUIContent(
                    "Destroy Platforms Below",
                    "How many height steps under the player stay alive. Older pads are destroyed so you can't fall forever."));
            }

            EditorGUILayout.Space(8);
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "breakableChance",
                "destroyPlatformsBelow");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
