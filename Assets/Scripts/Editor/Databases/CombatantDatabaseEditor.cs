#if UNITY_EDITOR
using CreatureClimb;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CreatureClimb.Editor
{
    [CustomEditor(typeof(CombatantDatabase))]
    public class CombatantDatabaseEditor : UnityEditor.Editor
    {
        SerializedProperty listProp;
        ReorderableList list;

        // title + name + sprite + scale + movement + speed
        static float ElementHeight()
        {
            var line = EditorGUIUtility.singleLineHeight + 2f;
            return line * 6f + 12f;
        }

        void OnEnable()
        {
            listProp = serializedObject.FindProperty("combatants");
            BuildList();
        }

        void BuildList()
        {
            if (listProp == null) return;

            list = new ReorderableList(serializedObject, listProp, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "Combatants  (bee, wasp, \u2026)");
                },
                elementHeightCallback = _ => ElementHeight(),
                drawElementCallback = (rect, index, active, focused) =>
                {
                    if (index < 0 || index >= listProp.arraySize) return;

                    var el = listProp.GetArrayElementAtIndex(index);
                    var lineH = EditorGUIUtility.singleLineHeight;
                    var pad = 2f;
                    var y = rect.y + 4f;
                    var prevLabel = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = Mathf.Min(100f, rect.width * 0.4f);

                    var displayName = el.FindPropertyRelative("displayName");
                    var sprite = el.FindPropertyRelative("sprite");
                    var scale = el.FindPropertyRelative("scale");
                    var movement = el.FindPropertyRelative("movement");
                    var speed = el.FindPropertyRelative("speed");

                    var title = displayName != null && !string.IsNullOrEmpty(displayName.stringValue)
                        ? displayName.stringValue
                        : $"Combatant {index + 1}";

                    EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineH), $"{index + 1}. {title}", EditorStyles.boldLabel);
                    y += lineH + pad;

                    if (displayName != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), displayName, new GUIContent("Name"));
                    y += lineH + pad;

                    if (sprite != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), sprite, new GUIContent("Sprite"));
                    y += lineH + pad;

                    if (scale != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), scale, new GUIContent("Scale"));
                    y += lineH + pad;

                    if (movement != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), movement, new GUIContent("Movement"));
                    y += lineH + pad;

                    if (speed != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), speed, new GUIContent("Speed"));

                    EditorGUIUtility.labelWidth = prevLabel;
                },
                onAddCallback = _ =>
                {
                    AddCombatantWizard.Open((CombatantDatabase)target);
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (listProp == null)
                listProp = serializedObject.FindProperty("combatants");
            if (list == null)
                BuildList();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Combatant mode picks by jump type:\n" +
                "  • Diagonal platforms → Vertical combatant (center, 2.5 pads tall)\n" +
                "  • Same-side platforms → Horizontal combatant (edge to edge)\n" +
                "Scale: 1 ≈ normal size, 2 = double, 0.5 = half.\n" +
                "Hitbox is a tight box around the opaque sprite (not transparent padding).\n" +
                "Add at least one of each Movement so both jump types have art.",
                MessageType.Info);

            if (listProp != null && listProp.arraySize == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("No combatants yet.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(4);
                if (GUILayout.Button("+  Add Combatant", GUILayout.Height(32)))
                    AddCombatantWizard.Open((CombatantDatabase)target);
            }
            else if (list != null)
            {
                list.DoLayoutList();

                EditorGUILayout.Space(4);
                if (GUILayout.Button("+  Add Combatant", GUILayout.Height(28)))
                    AddCombatantWizard.Open((CombatantDatabase)target);
            }
            else
            {
                EditorGUILayout.PropertyField(listProp, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                Repaint();
        }
    }

    public class AddCombatantWizard : EditorWindow
    {
        CombatantDatabase database;
        string combatantName = "Bee";
        Sprite sprite;
        float scale = 1f;
        CombatantMovement movement = CombatantMovement.Horizontal;
        float speed = 2.4f;

        public static void Open(CombatantDatabase db)
        {
            if (db == null)
            {
                EditorUtility.DisplayDialog("Combatant Database", "No Combatant Database selected.", "OK");
                return;
            }

            var window = GetWindow<AddCombatantWizard>(true, "Add Combatant", true);
            window.database = db;
            window.combatantName = "New Combatant";
            window.sprite = null;
            window.scale = 1f;
            window.movement = CombatantMovement.Horizontal;
            window.speed = 2.4f;
            window.minSize = new Vector2(380, 240);
            window.maxSize = new Vector2(520, 300);
            window.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("New Combatant", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            combatantName = EditorGUILayout.TextField("Name", combatantName);
            sprite = (Sprite)EditorGUILayout.ObjectField("Sprite", sprite, typeof(Sprite), false);
            scale = EditorGUILayout.FloatField("Scale", scale);
            if (scale < 0.05f) scale = 0.05f;
            movement = (CombatantMovement)EditorGUILayout.EnumPopup("Movement", movement);
            speed = EditorGUILayout.FloatField("Speed", speed);
            if (speed < 0.1f) speed = 0.1f;

            EditorGUILayout.Space(12);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(28)))
                {
                    Close();
                    return;
                }

                GUI.enabled = !string.IsNullOrWhiteSpace(combatantName) && database != null;
                if (GUILayout.Button("Create", GUILayout.Height(28)))
                {
                    CreateCombatant();
                    Close();
                }

                GUI.enabled = true;
            }
        }

        void CreateCombatant()
        {
            if (database == null) return;

            Undo.RecordObject(database, "Add Combatant");

            var so = new SerializedObject(database);
            var listProp = so.FindProperty("combatants");
            var index = listProp.arraySize;
            listProp.arraySize++;
            var el = listProp.GetArrayElementAtIndex(index);

            var cleanName = combatantName.Trim();
            var id = cleanName.ToLowerInvariant().Replace(' ', '_');

            SetString(el, "id", id);
            SetString(el, "displayName", cleanName);

            var sp = el.FindPropertyRelative("sprite");
            if (sp != null) sp.objectReferenceValue = sprite;

            var scaleProp = el.FindPropertyRelative("scale");
            if (scaleProp != null) scaleProp.floatValue = Mathf.Max(0.05f, scale);

            var moveProp = el.FindPropertyRelative("movement");
            if (moveProp != null) moveProp.enumValueIndex = (int)movement;

            var speedProp = el.FindPropertyRelative("speed");
            if (speedProp != null) speedProp.floatValue = Mathf.Max(0.1f, speed);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);

            Debug.Log($"Added combatant '{cleanName}'.");
        }

        static void SetString(SerializedProperty el, string field, string value)
        {
            var p = el.FindPropertyRelative(field);
            if (p != null) p.stringValue = value;
        }
    }
}
#endif
