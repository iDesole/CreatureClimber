#if UNITY_EDITOR
using CreatureClimb;
using UnityEditor;
using UnityEngine;

namespace CreatureClimb.Editor
{
    public static class CreatureDatabaseSetup
    {
        const string DatabasePath = "Assets/Resources/CreatureDatabase.asset";
        const string FrogSpritePath = "Assets/Sprites/Creatures/Frog Sprite.png";
        const string SpiderSpritePath = "Assets/Sprites/Creatures/Spider Gemini.png";

        [MenuItem("Creature Climb/Creatures/Open Creature Database")]
        public static void OpenDatabase()
        {
            var db = EnsureDatabase();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }

        [MenuItem("Creature Climb/Creatures/Add Frog From Sprites Folder")]
        public static void AddFrogFromSprites()
        {
            UpsertCreatureFromSprite(
                spritePath: FrogSpritePath,
                id: "frog",
                displayName: "Frog",
                description: "Your frog climber.",
                standOffsetY: 0.35f,
                shopPrice: 0,
                unlockedByDefault: true);
        }

        [MenuItem("Creature Climb/Creatures/Add Spider From Gemini Sprite")]
        public static void AddSpiderFromSprites()
        {
            UpsertCreatureFromSprite(
                spritePath: SpiderSpritePath,
                id: "spider",
                displayName: "Spider",
                description: "Your spider climber.",
                standOffsetY: 0.3f,
                shopPrice: 20,
                unlockedByDefault: true);
        }

        static void UpsertCreatureFromSprite(
            string spritePath,
            string id,
            string displayName,
            string description,
            float standOffsetY,
            int shopPrice,
            bool unlockedByDefault)
        {
            var db = EnsureDatabase();
            var sprite = LoadFirstSprite(spritePath);
            if (sprite == null)
            {
                Debug.LogError($"No sprite found at {spritePath}. Import your PNG there first.");
                return;
            }

            Undo.RecordObject(db, $"Set {displayName} Creature");

            var so = new SerializedObject(db);
            var creatures = so.FindProperty("creatures");
            var alreadyHas = false;

            for (var i = 0; i < creatures.arraySize; i++)
            {
                var el = creatures.GetArrayElementAtIndex(i);
                var idProp = el.FindPropertyRelative("id");
                var nameProp = el.FindPropertyRelative("displayName");
                var spriteProp = el.FindPropertyRelative("sprite");
                var matchesId = idProp != null && idProp.stringValue == id;
                var matchesName = nameProp != null && nameProp.stringValue == displayName;
                if (!matchesId && !matchesName)
                    continue;

                if (spriteProp != null)
                    spriteProp.objectReferenceValue = sprite;
                var offsetProp = el.FindPropertyRelative("standOffsetY");
                if (offsetProp != null)
                    offsetProp.floatValue = standOffsetY;
                alreadyHas = true;
                break;
            }

            if (!alreadyHas)
            {
                so.ApplyModifiedProperties();
                db.AddCreature(new Creature
                {
                    id = id,
                    displayName = displayName,
                    description = description,
                    sprite = sprite,
                    proceduralArt = ProceduralCreatureSprite.None,
                    tint = Color.white,
                    scale = 1f,
                    jumpSpeed = 1f,
                    reactionTime = 1f,
                    fallSpeed = 1f,
                    scoreMultiplier = 1f,
                    standOffsetY = standOffsetY,
                    unlockedByDefault = unlockedByDefault,
                    shopPrice = shopPrice
                });
            }
            else
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(db);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
            Debug.Log($"{displayName} set in Creature Database with sprite '{sprite.name}' from {spritePath}.");
        }

        static Sprite LoadFirstSprite(string assetPath)
        {
            Sprite best = null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var a in assets)
            {
                if (a is not Sprite s) continue;
                best = s;
                // Prefer the main/full-rect sprite when multiple sub-assets exist.
                if (s.rect.width >= 8 && s.rect.height >= 8)
                    return s;
            }

            return best;
        }

        static CreatureDatabase EnsureDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<CreatureDatabase>(DatabasePath);
            if (db != null) return db;

            db = CreatureDatabase.Load();
            if (db != null) return db;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            db = ScriptableObject.CreateInstance<CreatureDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
            return db;
        }
    }
}
#endif
