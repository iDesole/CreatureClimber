using System.Collections.Generic;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Spawns combatants from <see cref="CombatantDatabase"/> in pad gaps.
    /// Movement type matches the jump: diagonal jump → vertical flyer,
    /// same-side (vertical) jump → horizontal flyer.
    /// </summary>
    public class CombatantDirector : MonoBehaviour
    {
        public static CombatantDirector Instance { get; private set; }

        /// <summary>
        /// Vertical combatants patrol inside the gap between two pads
        /// (fraction of one platform spacing).
        /// </summary>
        public const float VerticalGapFill = 0.85f;

        /// <summary>
        /// Extra distance below the gap midpoint (as a fraction of spacing)
        /// so vertical flyers dip further toward the lower pad.
        /// </summary>
        public const float VerticalDipExtra = 0.35f;

        [SerializeField] CombatantDatabase database;

        readonly List<CombatantHazard> active = new();
        readonly List<CombatantHazard> pool = new();
        readonly HashSet<int> spawnedGaps = new();

        static Combatant s_fallback;

        Sprite fallbackBeeSprite;
        LeafSpawner spawner;
        Difficulty difficulty;
        bool running;

        public static CombatantDirector Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("--- Combatants ---");
            return go.AddComponent<CombatantDirector>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool IsRunning => running;

        public void Begin(LeafSpawner leafSpawner, Difficulty diff)
        {
            spawner = leafSpawner;
            difficulty = diff;
            database = CombatantDatabase.Load() ?? database;
            running = true;
            ClearAll();
            EnsureHazardsAround(0);
        }

        public void Stop()
        {
            running = false;
            ClearAll();
        }

        /// <summary>
        /// True if a combatant flies in the gap above this pad (between pad and pad+1).
        /// Easy mode uses this so that pad is solid.
        /// </summary>
        public static bool HasBeeAbovePad(int padHeight, Difficulty difficulty)
        {
            if (padHeight < 1) return false;
            var every = GameModeRules.CombatantBeeEveryNPads(difficulty);
            // Bee above pads 2, 6, 10… (easy every 4) or 2, 5, 8… (hard every 3).
            return padHeight >= 2 && (padHeight - 2) % every == 0;
        }

        public void OnPlayerHeight(int heightIndex)
        {
            if (!running) return;
            EnsureHazardsAround(heightIndex);
            CullFarBelow(heightIndex);
        }

        /// <summary>Call when new pads appear so we can resolve jump types sooner.</summary>
        public void NotifyPlatformsChanged()
        {
            if (!running) return;
            var h = 0;
            if (spawner != null && spawner.CurrentLeaf != null)
                h = spawner.CurrentLeaf.HeightIndex;
            EnsureHazardsAround(h);
        }

        void EnsureHazardsAround(int playerHeight)
        {
            if (spawner == null) return;

            // Match LeafSpawner combatant lookahead so hazards appear with the pre-spawned path.
            var from = Mathf.Max(1, playerHeight - 1);
            var to = playerHeight + 5;

            for (var padH = from; padH <= to; padH++)
            {
                if (!HasBeeAbovePad(padH, difficulty)) continue;
                if (spawnedGaps.Contains(padH)) continue;
                TrySpawnAbove(padH);
            }
        }

        void TrySpawnAbove(int padHeight)
        {
            if (spawner == null) return;

            // Need both pad sides to know if the jump is diagonal or vertical.
            if (!spawner.TryGetJumpIsDiagonal(padHeight, out var isDiagonal))
                return;

            // Diagonal platform layout → vertical combatant blocks the crossing.
            // Same-side (vertical) jump → horizontal combatant sweeps the lane gap.
            var needed = isDiagonal
                ? CombatantMovement.Vertical
                : CombatantMovement.Horizontal;

            // Prefer a DB combatant of the required movement; fall back if DB is empty / incomplete.
            var def = database != null ? database.PickRandom(needed) : null;
            if (def == null || def.movement != needed)
                def = GetFallback(needed);

            var spacing = spawner.VerticalSpacing;
            var lowerY = spawner.PadVisualWorldY(padHeight);
            var upperY = spawner.PadVisualWorldY(padHeight + 1);
            var midY = (lowerY + upperY) * 0.5f;
            var visualGap = Mathf.Abs(upperY - lowerY);
            if (visualGap < 0.2f)
                visualGap = spacing;

            var hazard = RentHazard();
            hazard.Setup(
                def,
                padHeight,
                midY,
                visualGap,
                GetPlayfieldHalfWidth(),
                GetFallbackBeeSprite());

            active.Add(hazard);
            spawnedGaps.Add(padHeight);
        }

        float GetPlayfieldHalfWidth()
        {
            // Edge-to-edge across the visible playfield (constant match-width).
            return PlatformRuntime.WorldHalfWidth();
        }

        CombatantHazard RentHazard()
        {
            for (var i = pool.Count - 1; i >= 0; i--)
            {
                var h = pool[i];
                pool.RemoveAt(i);
                if (h != null)
                    return h;
            }

            var go = new GameObject("CombatantHazard");
            go.transform.SetParent(transform, false);
            // RequireComponent adds SpriteRenderer with CombatantHazard; force it first for clarity.
            var sr = go.AddComponent<SpriteRenderer>();
            sr.enabled = true;
            sr.sortingOrder = 6;
            var hazard = go.AddComponent<CombatantHazard>();
            go.SetActive(false);
            return hazard;
        }

        void ReturnHazard(CombatantHazard hazard)
        {
            if (hazard == null) return;
            hazard.Retire();
            pool.Add(hazard);
        }

        void CullFarBelow(int playerHeight)
        {
            var minKeep = playerHeight - 4;
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var h = active[i];
                if (h == null)
                {
                    active.RemoveAt(i);
                    continue;
                }

                if (h.GapHeight < minKeep)
                {
                    spawnedGaps.Remove(h.GapHeight);
                    ReturnHazard(h);
                    active.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// After a clutch recover, drop hazards at/above this pad so the new path can re-roll them.
        /// </summary>
        public void ClearAbove(int padHeight)
        {
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var h = active[i];
                if (h == null)
                {
                    active.RemoveAt(i);
                    continue;
                }

                if (h.GapHeight >= padHeight)
                {
                    spawnedGaps.Remove(h.GapHeight);
                    ReturnHazard(h);
                    active.RemoveAt(i);
                }
            }
        }

        void ClearAll()
        {
            for (var i = 0; i < active.Count; i++)
                ReturnHazard(active[i]);

            active.Clear();
            spawnedGaps.Clear();
        }

        Combatant GetFallback(CombatantMovement movement)
        {
            if (s_fallback == null)
            {
                s_fallback = new Combatant
                {
                    id = "fallback",
                    displayName = "Bee",
                    scale = 1f,
                    movement = movement,
                    speed = 2.4f
                };
            }

            s_fallback.movement = movement;
            s_fallback.speed = difficulty == Difficulty.Hard ? 3.1f : 2.4f;
            return s_fallback;
        }

        Sprite GetFallbackBeeSprite()
        {
            if (fallbackBeeSprite != null) return fallbackBeeSprite;
            fallbackBeeSprite = CreateSimpleBeeSprite();
            return fallbackBeeSprite;
        }

        static Sprite CreateSimpleBeeSprite()
        {
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (var y = 0; y < s; y++)
            for (var x = 0; x < s; x++)
                tex.SetPixel(x, y, Color.clear);

            for (var y = 5; y <= 10; y++)
            {
                for (var x = 4; x <= 11; x++)
                {
                    var stripe = ((x + y) / 2) % 2 == 0;
                    tex.SetPixel(x, y, stripe
                        ? new Color(1f, 0.85f, 0.15f, 1f)
                        : new Color(0.12f, 0.1f, 0.08f, 1f));
                }
            }

            tex.SetPixel(3, 9, new Color(0.85f, 0.95f, 1f, 0.7f));
            tex.SetPixel(2, 10, new Color(0.85f, 0.95f, 1f, 0.5f));
            tex.SetPixel(12, 9, new Color(0.85f, 0.95f, 1f, 0.7f));
            tex.SetPixel(13, 10, new Color(0.85f, 0.95f, 1f, 0.5f));

            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
