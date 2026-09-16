#if UNITY_EDITOR
using CreatureClimb;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CreatureClimb.Editor
{
    /// <summary>
    /// Matches Platform / Background database inspectors: one field per line,
    /// fixed row height, short labels. Uses plain EditorGUI (not PropertyField)
    /// so [Range]/[Header] drawers cannot stack or overflow the row.
    /// </summary>
    [CustomEditor(typeof(CreatureDatabase))]
    public class CreatureDatabaseEditor : UnityEditor.Editor
    {
        SerializedProperty creaturesProp;
        ReorderableList list;

        const float Pad = 4f;
        const float LabelW = 90f;
        // title + name + race nick + sprite + scale + UI size + price
        const int FieldRows = 7;

        static float LineH => EditorGUIUtility.singleLineHeight;

        static float ElementHeight()
        {
            return Pad + FieldRows * (LineH + Pad) + Pad;
        }

        void OnEnable()
        {
            creaturesProp = serializedObject.FindProperty("creatures");
            BuildList();
        }

        void BuildList()
        {
            if (creaturesProp == null) return;

            list = new ReorderableList(serializedObject, creaturesProp, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "Creatures  (empty until you press +)");
                },
                elementHeight = ElementHeight(),
                elementHeightCallback = _ => ElementHeight(),
                drawElementCallback = DrawElement,
                onAddCallback = _ => AddCreatureWizard.Open((CreatureDatabase)target)
            };
        }

        void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            if (creaturesProp == null || index < 0 || index >= creaturesProp.arraySize)
                return;

            var el = creaturesProp.GetArrayElementAtIndex(index);
            var displayName = el.FindPropertyRelative("displayName");
            var raceNickname = el.FindPropertyRelative("raceNickname");
            var idProp = el.FindPropertyRelative("id");
            var sprite = el.FindPropertyRelative("sprite");
            var scale = el.FindPropertyRelative("scale");
            var uiScale = el.FindPropertyRelative("uiScale");
            var shopPrice = el.FindPropertyRelative("shopPrice");

            var y = rect.y + Pad;
            var x = rect.x;
            var w = rect.width;
            var line = LineH;
            var prevLabel = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelW;

            var title = displayName != null && !string.IsNullOrEmpty(displayName.stringValue)
                ? displayName.stringValue
                : $"Creature {index + 1}";

            // Row 0 — title only
            EditorGUI.LabelField(new Rect(x, y, w, line), title, EditorStyles.boldLabel);
            y += line + Pad;

            // Row 1 — Name
            if (displayName != null)
            {
                EditorGUI.BeginChangeCheck();
                var next = EditorGUI.TextField(new Rect(x, y, w, line), "Name", displayName.stringValue);
                if (EditorGUI.EndChangeCheck())
                    displayName.stringValue = next;
            }

            y += line + Pad;

            // Row 2 — Race nickname (editable; blank uses built-in default)
            if (raceNickname != null)
            {
                var idHint = idProp != null ? idProp.stringValue : "";
                if (string.IsNullOrWhiteSpace(idHint) && displayName != null)
                    idHint = displayName.stringValue;
                var placeholder = RaceNicknames.DefaultForId(idHint, displayName != null ? displayName.stringValue : null);

                EditorGUI.BeginChangeCheck();
                var next = EditorGUI.TextField(
                    new Rect(x, y, w, line),
                    "Race Nick",
                    raceNickname.stringValue);
                if (EditorGUI.EndChangeCheck())
                    raceNickname.stringValue = next;

                // Grey hint when empty so designers see the default.
                if (string.IsNullOrWhiteSpace(raceNickname.stringValue))
                {
                    var hintRect = new Rect(x + LabelW + 4f, y, w - LabelW - 4f, line);
                    var prev = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.35f);
                    EditorGUI.LabelField(hintRect, placeholder);
                    GUI.color = prev;
                }
            }

            y += line + Pad;

            // Row 3 — Sprite (plain object field, never expands)
            if (sprite != null)
            {
                EditorGUI.BeginChangeCheck();
                var next = (Sprite)EditorGUI.ObjectField(
                    new Rect(x, y, w, line),
                    "Sprite",
                    sprite.objectReferenceValue,
                    typeof(Sprite),
                    false);
                if (EditorGUI.EndChangeCheck())
                    sprite.objectReferenceValue = next;
            }

            y += line + Pad;

            // Row 4 — Scale (1 = normal, 0.5 = half, 2 = double)
            if (scale != null)
            {
                EditorGUI.BeginChangeCheck();
                var next = EditorGUI.FloatField(new Rect(x, y, w, line), "Scale", scale.floatValue);
                if (EditorGUI.EndChangeCheck())
                    scale.floatValue = Mathf.Max(0.05f, next);
            }

            y += line + Pad;

            // Row 5 — UI Size (roster / shop tiles only)
            InspectorSliders.FloatSlider(new Rect(x, y, w, line), uiScale, "UI Size", 0.25f, 3f);

            y += line + Pad;

            // Row 6 — Price
            if (shopPrice != null)
            {
                EditorGUI.BeginChangeCheck();
                var next = EditorGUI.IntField(new Rect(x, y, w, line), "Price", shopPrice.intValue);
                if (EditorGUI.EndChangeCheck())
                    shopPrice.intValue = Mathf.Max(0, next);
            }

            EditorGUIUtility.labelWidth = prevLabel;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (creaturesProp == null)
                creaturesProp = serializedObject.FindProperty("creatures");
            if (list == null || list.serializedProperty != creaturesProp)
                BuildList();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Starts empty.\n" +
                "Press + to add a creature:\n" +
                "  • Name\n" +
                "  • Race Nick (shown when this creature is the AI)\n" +
                "  • Sprite\n" +
                "  • Scale (1 = normal, 0.5 = half, 2 = double)\n" +
                "Price 0 = free in the shop. Blank Race Nick uses a built-in default.",
                MessageType.Info);

            if (creaturesProp == null || creaturesProp.arraySize == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("No creatures yet.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(4);
                if (GUILayout.Button("+  Add Creature", GUILayout.Height(32)))
                    AddCreatureWizard.Open((CreatureDatabase)target);
            }
            else
            {
                // Keep height in sync if DPI / skin changes line height.
                list.elementHeight = ElementHeight();
                list.DoLayoutList();

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+  Add Creature", GUILayout.Height(28)))
                        AddCreatureWizard.Open((CreatureDatabase)target);

                    if (GUILayout.Button("Fill Empty Race Nicknames", GUILayout.Height(28)))
                        FillEmptyRaceNicknames((CreatureDatabase)target);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        static void FillEmptyRaceNicknames(CreatureDatabase db)
        {
            if (db == null) return;
            Undo.RecordObject(db, "Fill Race Nicknames");
            var so = new SerializedObject(db);
            var list = so.FindProperty("creatures");
            if (list == null) return;

            var filled = 0;
            for (var i = 0; i < list.arraySize; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                var nick = el.FindPropertyRelative("raceNickname");
                if (nick == null || !string.IsNullOrWhiteSpace(nick.stringValue))
                    continue;

                var id = el.FindPropertyRelative("id")?.stringValue;
                var name = el.FindPropertyRelative("displayName")?.stringValue;
                nick.stringValue = RaceNicknames.DefaultForId(id, name);
                filled++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"Filled {filled} empty race nickname(s).");
        }
    }

    public class AddCreatureWizard : EditorWindow
    {
        CreatureDatabase database;
        string creatureName = "New Creature";
        Sprite sprite;

        public static void Open(CreatureDatabase db)
        {
            if (db == null)
            {
                EditorUtility.DisplayDialog("Creature Database", "No Creature Database selected.", "OK");
                return;
            }

            var window = GetWindow<AddCreatureWizard>(true, "Add Creature", true);
            window.database = db;
            window.creatureName = "New Creature";
            window.sprite = null;
            window.minSize = new Vector2(360, 160);
            window.maxSize = new Vector2(480, 200);
            window.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("New Creature", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            creatureName = EditorGUILayout.TextField("Name", creatureName);
            sprite = (Sprite)EditorGUILayout.ObjectField("Sprite", sprite, typeof(Sprite), false);

            EditorGUILayout.Space(12);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(28)))
                {
                    Close();
                    return;
                }

                GUI.enabled = !string.IsNullOrWhiteSpace(creatureName) && database != null;
                if (GUILayout.Button("Create", GUILayout.Height(28)))
                {
                    CreateCreature();
                    Close();
                }

                GUI.enabled = true;
            }
        }

        void CreateCreature()
        {
            if (database == null) return;

            var cleanName = creatureName.Trim();
            var id = cleanName.ToLowerInvariant().Replace(' ', '_');

            Undo.RecordObject(database, "Add Creature");

            database.AddCreature(new Creature
            {
                id = id,
                displayName = cleanName,
                raceNickname = RaceNicknames.DefaultForId(id, cleanName),
                description = "",
                sprite = sprite,
                proceduralArt = ProceduralCreatureSprite.None,
                tint = Color.white,
                scale = 1f,
                uiScale = 1f,
                jumpSpeed = 1f,
                reactionTime = 1f,
                fallSpeed = 1f,
                scoreMultiplier = 1f,
                standOffsetY = 0.35f,
                unlockedByDefault = true,
                shopPrice = 0
            });

            AssetDatabase.SaveAssets();

            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);

            Debug.Log($"Added creature '{cleanName}'.");
        }
    }
}
#endif
