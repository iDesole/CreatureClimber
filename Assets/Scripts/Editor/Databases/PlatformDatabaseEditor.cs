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

        const float SliderCol = 40f;
        const float SpawnMin = -1.5f;
        const float SpawnMax = 1.5f;

        // title + name + solid + break + scale + UI size + shop block + padding
        static float ElementHeight()
        {
            var line = EditorGUIUtility.singleLineHeight + 2f;
            return line * 6f + ShopUnlockDrawer.Height() + 12f;
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
                    var fieldsW = Mathf.Max(80f, rect.width - SliderCol - 8f);
                    var prevLabel = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = Mathf.Min(110f, fieldsW * 0.38f);

                    var displayName = el.FindPropertyRelative("displayName");
                    var solidSprite = el.FindPropertyRelative("solidSprite");
                    var breakSprite = el.FindPropertyRelative("breakSprite");
                    var scale = el.FindPropertyRelative("scale");
                    var uiScale = el.FindPropertyRelative("uiScale");
                    var spawnOffset = el.FindPropertyRelative("spawnOffsetY");
                    var shopMode = el.FindPropertyRelative("shopMode");
                    var shopPrice = el.FindPropertyRelative("shopPrice");
                    var unlockChar = el.FindPropertyRelative("unlockWithCharacterId");

                    var title = displayName != null && !string.IsNullOrEmpty(displayName.stringValue)
                        ? displayName.stringValue
                        : $"Platform {index + 1}";

                    EditorGUI.LabelField(new Rect(rect.x, y, fieldsW, lineH), title, EditorStyles.boldLabel);
                    y += lineH + pad;

                    if (displayName != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, fieldsW, lineH), displayName, new GUIContent("Name"));
                    y += lineH + pad;

                    if (solidSprite != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, fieldsW, lineH), solidSprite, new GUIContent("Solid"));
                    y += lineH + pad;

                    if (breakSprite != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, fieldsW, lineH), breakSprite, new GUIContent("Break"));
                    y += lineH + pad;

                    if (scale != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        var next = EditorGUI.FloatField(
                            new Rect(rect.x, y, fieldsW, lineH),
                            "Scale",
                            scale.floatValue);
                        if (EditorGUI.EndChangeCheck())
                            scale.floatValue = Mathf.Max(0.05f, next);
                    }

                    y += lineH + pad;

                    InspectorSliders.FloatSlider(
                        new Rect(rect.x, y, fieldsW, lineH),
                        uiScale,
                        "UI Size",
                        0.25f,
                        3f);

                    y += lineH + pad + 2f;

                    EditorGUIUtility.labelWidth = prevLabel;

                    ShopUnlockDrawer.Draw(
                        new Rect(rect.x, y, fieldsW, ShopUnlockDrawer.Height()),
                        shopMode,
                        shopPrice,
                        unlockChar);

                    DrawSpawnSlider(rect, spawnOffset, lineH);
                },
                onAddCallback = _ =>
                {
                    AddPlatformWizard.Open((PlatformDatabase)target);
                }
            };
        }

        static void DrawSpawnSlider(Rect rect, SerializedProperty spawnOffset, float lineH)
        {
            if (spawnOffset == null) return;

            var col = new Rect(rect.xMax - SliderCol, rect.y + 2f, SliderCol, rect.height - 4f);
            var labelRect = new Rect(col.x, col.y, col.width, lineH);
            var valueRect = new Rect(col.x, col.yMax - lineH, col.width, lineH);
            var sliderW = 16f;
            var sliderRect = new Rect(
                col.x + (col.width - sliderW) * 0.5f,
                labelRect.yMax + 2f,
                sliderW,
                Mathf.Max(24f, valueRect.y - labelRect.yMax - 4f));

            EditorGUI.LabelField(
                labelRect,
                new GUIContent("Y", "Nudge this pad up or down so the creature sits on the landing surface."),
                EditorStyles.centeredGreyMiniLabel);
            EditorGUI.BeginChangeCheck();
            var next = GUI.VerticalSlider(sliderRect, spawnOffset.floatValue, SpawnMax, SpawnMin);
            if (EditorGUI.EndChangeCheck())
                spawnOffset.floatValue = Mathf.Clamp(Mathf.Round(next * 100f) / 100f, SpawnMin, SpawnMax);

            EditorGUI.LabelField(valueRect, spawnOffset.floatValue.ToString("0.00"), EditorStyles.centeredGreyMiniLabel);
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
                "  • UI Size — Customize / Shop tile size (not in-game)\n" +
                "  • Spawn Y slider — nudge the pad up/down so the creature sits on it\n" +
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

            var uiScale = el.FindPropertyRelative("uiScale");
            if (uiScale != null) uiScale.floatValue = 1f;

            var stand = el.FindPropertyRelative("standOffsetY");
            if (stand != null) stand.floatValue = 0.35f;

            var spawn = el.FindPropertyRelative("spawnOffsetY");
            if (spawn != null) spawn.floatValue = 0f;

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
