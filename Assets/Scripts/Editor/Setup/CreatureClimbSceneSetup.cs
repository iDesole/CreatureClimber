#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CreatureClimb.Editor
{
    public static class CreatureClimbSceneSetup
    {
        [MenuItem("Creature Climb/Build Game Scene")]
        public static void SetupScene()
        {
            var root = GameObject.Find(CreatureClimbSceneBuilder.RootName);
            if (root != null)
                Object.DestroyImmediate(root);

            DestroyImmediateAll<GameManager>();
            DestroyImmediateAll<LeafSpawner>();
            DestroyImmediateAll<PlayerController>();
            DestroyImmediateAll<GameUI>();
            DestroyImmediateAll<BackgroundScroller>();

            var gm = CreatureClimbSceneBuilder.Build();

            if (gm == null || !gm.IsWired)
            {
                Debug.LogError("Build failed — GameManager still missing required references.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = gm.gameObject;
            EditorGUIUtility.PingObject(gm);

            Debug.Log(
                "Game scene ready.\n" +
                "←→ character, ↑↓ platform, tap/space to start, Esc/II pause.");
        }

        static void DestroyImmediateAll<T>() where T : Component
        {
            var items = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] != null)
                    Object.DestroyImmediate(items[i].gameObject);
            }
        }
    }
}
#endif
