#if UNITY_EDITOR
using CreatureClimb;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CreatureClimb.Editor
{
    [CustomEditor(typeof(BackgroundDatabase))]
    public class BackgroundDatabaseEditor : UnityEditor.Editor
    {
        SerializedProperty backgroundsProp;
        ReorderableList list;

        // title + name + sprite + tint + height + near + nearTint + sky + shop + padding
        static float ElementHeight()
        {
            var line = EditorGUIUtility.singleLineHeight + 2f;
            return line * 8f + ShopUnlockDrawer.Height() + 14f;
        }

        void OnEnable()
        {
            backgroundsProp = serializedObject.FindProperty("backgrounds");
            BuildList();
        }

        void BuildList()
        {
            list = new ReorderableList(serializedObject, backgroundsProp, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "Backgrounds  (loop in this order as you climb)");
                },
                elementHeightCallback = _ => ElementHeight(),
                drawElementCallback = (rect, index, active, focused) =>
                {
                    if (index < 0 || index >= backgroundsProp.arraySize) return;

                    var el = backgroundsProp.GetArrayElementAtIndex(index);
                    var lineH = EditorGUIUtility.singleLineHeight;
                    var pad = 2f;
                    var y = rect.y + 4f;
                    var prevLabel = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = Mathf.Min(120f, rect.width * 0.4f);

                    var displayName = el.FindPropertyRelative("displayName");
                    var sprite = el.FindPropertyRelative("sprite");
                    var tint = el.FindPropertyRelative("tint");
                    var worldHeight = el.FindPropertyRelative("worldHeight");
                    var nearSprite = el.FindPropertyRelative("nearSprite");
                    var nearTint = el.FindPropertyRelative("nearTint");
                    var skyColor = el.FindPropertyRelative("skyColor");
                    var shopMode = el.FindPropertyRelative("shopMode");
                    var shopPrice = el.FindPropertyRelative("shopPrice");
                    var unlockChar = el.FindPropertyRelative("unlockWithCharacterId");

                    var title = displayName != null && !string.IsNullOrEmpty(displayName.stringValue)
                        ? displayName.stringValue
                        : $"Background {index + 1}";

                    EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineH), $"{index + 1}. {title}", EditorStyles.boldLabel);
                    y += lineH + pad;

                    if (displayName != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), displayName, new GUIContent("Name"));
                    y += lineH + pad;

                    if (sprite != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), sprite, new GUIContent("Sprite"));
                    y += lineH + pad;

                    if (tint != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), tint, new GUIContent("Tint"));
                    y += lineH + pad;

                    if (worldHeight != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), worldHeight, new GUIContent("Band Height"));
                    y += lineH + pad;

                    if (nearSprite != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), nearSprite, new GUIContent("Near Layer"));
                    y += lineH + pad;

                    if (nearTint != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), nearTint, new GUIContent("Near Tint"));
                    y += lineH + pad;

                    if (skyColor != null)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), skyColor, new GUIContent("Sky Color"));
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
                    AddBackgroundWizard.Open((BackgroundDatabase)target);
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (backgroundsProp == null)
                backgroundsProp = serializedObject.FindProperty("backgrounds");
            if (list == null)
                BuildList();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Add custom backgrounds in order.\n" +
                "As the player climbs, the game loops through this list:\n" +
                "  1 → 2 → 3 → … → 1 → …\n" +
                "Shop unlock: Free / Buy With Coins / Unlock With Character.",
                MessageType.Info);

            if (backgroundsProp.arraySize == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("No backgrounds yet.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(4);
                if (GUILayout.Button("+  Add Background", GUILayout.Height(32)))
                    AddBackgroundWizard.Open((BackgroundDatabase)target);
            }
            else
            {
                list.DoLayoutList();

                EditorGUILayout.Space(4);
                if (GUILayout.Button("+  Add Background", GUILayout.Height(28)))
                    AddBackgroundWizard.Open((BackgroundDatabase)target);
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                Repaint();
        }
    }

    public class AddBackgroundWizard : EditorWindow
    {
        BackgroundDatabase database;
        string backgroundName = "Forest";
        Sprite sprite;
        Sprite nearSprite;
        float worldHeight = 10f;
        Color tint = Color.white;
        Color skyColor = new Color(0.14f, 0.3f, 0.18f, 1f);

        public static void Open(BackgroundDatabase db)
        {
            if (db == null)
            {
                EditorUtility.DisplayDialog("Background Database", "No Background Database selected.", "OK");
                return;
            }

            var window = GetWindow<AddBackgroundWizard>(true, "Add Background", true);
            window.database = db;
            window.backgroundName = "New Background";
            window.sprite = null;
            window.nearSprite = null;
            window.worldHeight = 10f;
            window.tint = Color.white;
            window.skyColor = new Color(0.14f, 0.3f, 0.18f, 1f);
            window.minSize = new Vector2(380, 260);
            window.maxSize = new Vector2(520, 320);
            window.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("New Background", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            backgroundName = EditorGUILayout.TextField("Name", backgroundName);
            sprite = (Sprite)EditorGUILayout.ObjectField("Sprite", sprite, typeof(Sprite), false);
            nearSprite = (Sprite)EditorGUILayout.ObjectField("Near Layer (optional)", nearSprite, typeof(Sprite), false);
            worldHeight = EditorGUILayout.FloatField("Band Height (world)", worldHeight);
            if (worldHeight < 1f) worldHeight = 1f;
            tint = EditorGUILayout.ColorField("Tint", tint);
            skyColor = EditorGUILayout.ColorField("Sky Color", skyColor);

            EditorGUILayout.Space(12);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(28)))
                {
                    Close();
                    return;
                }

                GUI.enabled = !string.IsNullOrWhiteSpace(backgroundName) && database != null;
                if (GUILayout.Button("Create", GUILayout.Height(28)))
                {
                    CreateBackground();
                    Close();
                }

                GUI.enabled = true;
            }
        }

        void CreateBackground()
        {
            if (database == null) return;

            Undo.RecordObject(database, "Add Background");

            var so = new SerializedObject(database);
            var listProp = so.FindProperty("backgrounds");
            var index = listProp.arraySize;
            listProp.arraySize++;
            var el = listProp.GetArrayElementAtIndex(index);

            var cleanName = backgroundName.Trim();
            var id = cleanName.ToLowerInvariant().Replace(' ', '_');

            SetString(el, "id", id);
            SetString(el, "displayName", cleanName);

            var sp = el.FindPropertyRelative("sprite");
            if (sp != null) sp.objectReferenceValue = sprite;

            var near = el.FindPropertyRelative("nearSprite");
            if (near != null) near.objectReferenceValue = nearSprite;

            var height = el.FindPropertyRelative("worldHeight");
            if (height != null) height.floatValue = Mathf.Max(1f, worldHeight);

            var tintProp = el.FindPropertyRelative("tint");
            if (tintProp != null) tintProp.colorValue = tint;

            var nearTint = el.FindPropertyRelative("nearTint");
            if (nearTint != null) nearTint.colorValue = Color.white;

            var sky = el.FindPropertyRelative("skyColor");
            if (sky != null) sky.colorValue = skyColor;

            var parallax = el.FindPropertyRelative("nearParallax");
            if (parallax != null) parallax.floatValue = 0.35f;

            var sort = el.FindPropertyRelative("sortingOrder");
            if (sort != null) sort.intValue = -10;

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

            Debug.Log($"Added background '{cleanName}' (loops in database order as you climb).");
        }

        static void SetString(SerializedProperty el, string field, string value)
        {
            var p = el.FindPropertyRelative(field);
            if (p != null) p.stringValue = value;
        }
    }
}
#endif
