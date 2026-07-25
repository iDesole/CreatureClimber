#if UNITY_EDITOR
using CreatureClimb;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CreatureClimb.Editor
{
    /// <summary>
    /// Empty list + plus button. Plus opens a form: Name, Solid sprite, Break sprite.
    /// Shop unlock is Mode + only Price or Character (no overlapping fields).
    /// </summary>
    [CustomEditor(typeof(PlatformDatabase))]
    public class PlatformDatabaseEditor : UnityEditor.Editor
    {
        SerializedProperty platformsProp;
        ReorderableList list;

        // title + name + solid + break + scale + shop block + padding
        static float ElementHeight()
        {
            var line = EditorGUIUtility.singleLineHeight + 2f;
            return line * 5f + ShopUnlockDrawer.Height() + 12f;
        }

        void OnEnable()
        {
            platformsProp = serializedObject.FindProperty("platforms");
            BuildList();
        }

        void BuildList()
        {
            list = new ReorderableList(serializedObject, platformsProp, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "Platforms  (empty until you press +)");
                },
                elementHeightCallback = _ => ElementHeight(),
                drawElementCallback = (rect, index, active, focused) =>
                {
                    if (index < 0 || index >= platformsProp.arraySize) return;

                    var el = platformsProp.GetArrayElementAtIndex(index);
                    var lineH = EditorGUIUtility.singleLineHeight;
                    var pad = 2f;
                    var y = rect.y + 4f;
                    var prevLabel = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = Mathf.Min(110f, rect.width * 0.38f);

                    var displayName = el.FindPropertyRelative("displayName");
                    var solidSprite = el.FindPropertyRelative("solidSprite");
                    var breakSprite = el.FindPropertyRelative("breakSprite");
                    var scale = el.FindPropertyRelative("scale");
                    var shopMode = el.FindPropertyRelative("shopMode");
                    var shopPrice = el.FindPropertyRelative("shopPrice");
                    var unlockChar = el.FindPropertyRelative("unlockWithCharacterId");

                    var title = displayName != null && !string.IsNullOrEmpty(displayName.stringValue)
                        ? displayName.stringValue
                        : $"Platform {index + 1}";

                    EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineH), title, EditorStyles.boldLabel);
                    y += lineH + pad;

                    if (displayName != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), displayName, new GUIContent("Name"));
                    y += lineH + pad;

                    if (solidSprite != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), solidSprite, new GUIContent("Solid"));
                    y += lineH + pad;

                    if (breakSprite != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), breakSprite, new GUIContent("Break"));
                    y += lineH + pad;

                    if (scale != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        var next = EditorGUI.FloatField(
                            new Rect(rect.x, y, rect.width, lineH),
                            "Scale",
                            scale.floatValue);
                        if (EditorGUI.EndChangeCheck())
                            scale.floatValue = Mathf.Max(0.05f, next);
                    }

                    y += lineH + pad + 2f;

                    EditorGUIUtility.labelWidth = prevLabel;

                    ShopUnlockDrawer.Draw(
                        new Rect(rect.x, y, rect.width, ShopUnlockDrawer.Height()),
                        shopMode,
                        shopPrice,
                        unlockChar);
                },
                onAddCallback = _ =>
                {
                    AddPlatformWizard.Open((PlatformDatabase)target);
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (platformsProp == null)
                platformsProp = serializedObject.FindProperty("platforms");
            if (list == null)
                BuildList();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Starts empty.\n" +
                "Press + to add a platform:\n" +
                "  • Name\n" +
                "  • Solid (won't break)\n" +
                "  • Break (shatters on wrong-side miss)\n" +
                "  • Scale (1 = normal, 0.5 = half, 2 = double)\n" +
                "Shop unlock: Free / Buy With Coins / Unlock With Character.",
                MessageType.Info);

            if (platformsProp.arraySize == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("No platforms yet.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(4);
                if (GUILayout.Button("+  Add Platform", GUILayout.Height(32)))
                    AddPlatformWizard.Open((PlatformDatabase)target);
            }
            else
            {
                list.DoLayoutList();

                EditorGUILayout.Space(4);
                if (GUILayout.Button("+  Add Platform", GUILayout.Height(28)))
                    AddPlatformWizard.Open((PlatformDatabase)target);
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                Repaint();
        }
    }

    public class AddPlatformWizard : EditorWindow
    {
        PlatformDatabase database;
        string platformName = "Lilypad";
        Sprite solidSprite;
        Sprite breakSprite;

        public static void Open(PlatformDatabase db)
        {
            if (db == null)
            {
                EditorUtility.DisplayDialog("Platform Database", "No Platform Database selected.", "OK");
                return;
            }

            var window = GetWindow<AddPlatformWizard>(true, "Add Platform", true);
            window.database = db;
            window.platformName = "New Platform";
            window.solidSprite = null;
            window.breakSprite = null;
            window.minSize = new Vector2(360, 180);
            window.maxSize = new Vector2(480, 240);
            window.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("New Platform", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            platformName = EditorGUILayout.TextField("Name", platformName);
            solidSprite = (Sprite)EditorGUILayout.ObjectField(
                "Solid Sprite (won't break)", solidSprite, typeof(Sprite), false);
            breakSprite = (Sprite)EditorGUILayout.ObjectField(
                "Break Sprite (will break)", breakSprite, typeof(Sprite), false);

            EditorGUILayout.Space(12);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(28)))
                {
                    Close();
                    return;
                }

                GUI.enabled = !string.IsNullOrWhiteSpace(platformName) && database != null;
                if (GUILayout.Button("Create", GUILayout.Height(28)))
                {
                    CreatePlatform();
                    Close();
                }

                GUI.enabled = true;
            }
        }

        void CreatePlatform()
        {
            if (database == null) return;

            Undo.RecordObject(database, "Add Platform");

            var so = new SerializedObject(database);
            var platforms = so.FindProperty("platforms");
            var index = platforms.arraySize;
            platforms.arraySize++;
            var el = platforms.GetArrayElementAtIndex(index);

            var cleanName = platformName.Trim();
            var id = cleanName.ToLowerInvariant().Replace(' ', '_');

            SetString(el, "id", id);
            SetString(el, "displayName", cleanName);

            var solid = el.FindPropertyRelative("solidSprite");
            if (solid != null) solid.objectReferenceValue = solidSprite;

            var brk = el.FindPropertyRelative("breakSprite");
            if (brk != null) brk.objectReferenceValue = breakSprite;

            var scale = el.FindPropertyRelative("scale");
            if (scale != null) scale.floatValue = 1f;

            var stand = el.FindPropertyRelative("standOffsetY");
            if (stand != null) stand.floatValue = 0.35f;

            var mode = el.FindPropertyRelative("shopMode");
            if (mode != null) mode.enumValueIndex = (int)ShopUnlockMode.Free;

            var price = el.FindPropertyRelative("shopPrice");
            if (price != null) price.intValue = 0;

            var unlock = el.FindPropertyRelative("unlockWithCharacterId");
            if (unlock != null) unlock.stringValue = "";

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);

            Debug.Log($"Added platform '{cleanName}' (solid + break sprites).");
        }

        static void SetString(SerializedProperty el, string field, string value)
        {
            var p = el.FindPropertyRelative(field);
            if (p != null) p.stringValue = value;
        }
    }
}
#endif
