using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreatureClimb
{
    /// <summary>
    /// Starts empty. Click + to add a platform (name, solid sprite, break sprite).
    /// </summary>
    [CreateAssetMenu(fileName = "PlatformDatabase", menuName = "Creature Climb/Platform Database", order = 1)]
    public class PlatformDatabase : ScriptableObject
    {
        public const string ResourcesPath = "PlatformDatabase";

        static PlatformDatabase cached;

        [SerializeField] List<Platform> platforms = new();

        public IReadOnlyList<Platform> Platforms => platforms;
        public int Count => platforms?.Count ?? 0;

        public Platform GetPlatform(int index)
        {
            if (platforms == null || platforms.Count == 0)
                return null;

            return platforms[Mathf.Clamp(index, 0, platforms.Count - 1)];
        }

        public List<Platform> GetAll()
        {
            var list = new List<Platform>();
            if (platforms == null) return list;
            foreach (var p in platforms)
            {
                if (p != null)
                    list.Add(p);
            }

            return list;
        }

#if UNITY_EDITOR
        public void AddPlatform(Platform platform)
        {
            if (platform == null) return;
            if (platforms == null)
                platforms = new List<Platform>();
            platforms.Add(platform);
            EditorUtility.SetDirty(this);
        }
#endif

        public static PlatformDatabase Load()
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<PlatformDatabase>(ResourcesPath);
            if (cached != null)
                return cached;

#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets("t:PlatformDatabase");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var db = AssetDatabase.LoadAssetAtPath<PlatformDatabase>(path);
                if (db != null)
                {
                    cached = db;
                    return cached;
                }
            }
#endif

            return null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (platforms == null) return;

            for (var i = 0; i < platforms.Count; i++)
            {
                var p = platforms[i];
                if (p == null) continue;

                if (string.IsNullOrWhiteSpace(p.id) && !string.IsNullOrWhiteSpace(p.displayName))
                    p.id = p.displayName.Trim().ToLowerInvariant().Replace(' ', '_');
            }
        }
#endif
    }
}
