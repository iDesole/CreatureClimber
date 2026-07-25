using System.Collections.Generic;
using UnityEngine;

namespace CreatureClimb
{
    public class LeafSpawner : MonoBehaviour
    {
        [SerializeField] Leaf leafPrefab;
        [SerializeField] Transform leafContainer;
        [SerializeField] float horizontalOffset = 1.575f;
        [SerializeField] float verticalSpacing = 3.4f;
        [SerializeField] int poolSize = 16;

        [Header("Spawn mix")]
        [Tooltip("Chance a mid-climb platform is BREAKABLE (2s after land, or wrong-side miss). Rest are solid.")]
        [Range(0f, 1f)]
        [SerializeField] float breakableChance = 0.75f;

        [Header("Cull old platforms")]
        [Tooltip("Platforms this many height steps below the player are destroyed so you can't fall forever.")]
        [Min(1)]
        [SerializeField] int destroyPlatformsBelow = 5;

        const int MaxConsecutiveSameSide = 3;
        const float ThirdInARowChance = 0.20f;

        readonly List<Leaf> pool = new();
        readonly List<Leaf> activeLeaves = new();

        Leaf currentLeaf;
        Leaf targetLeaf;
        int nextHeightIndex;
        LeafSide lastSide = LeafSide.Left;
        int consecutiveSameSide;

        Platform activePlatform;

        public Leaf CurrentLeaf => currentLeaf;
        public Leaf TargetLeaf => targetLeaf;
        public float VerticalSpacing => verticalSpacing;
        public float HorizontalOffset => horizontalOffset;
        public int DestroyPlatformsBelow => destroyPlatformsBelow;
        public float BreakableChance => breakableChance;
        public IReadOnlyList<Leaf> ActiveLeaves => activeLeaves;

        public void Setup(Leaf prefab, Transform container)
        {
            leafPrefab = prefab;
            leafContainer = container;
        }

        public void SetActivePlatform(Platform platform)
        {
            activePlatform = platform;
        }

        public void Initialize()
        {
            EnsurePool();
            ClearActive();
            ApplyPhoneLayout();

            nextHeightIndex = 0;
            consecutiveSameSide = 0;

            currentLeaf = SpawnLeaf(LeafSide.Left, nextHeightIndex++, canBreak: false);
            RegisterSide(LeafSide.Left);

            targetLeaf = SpawnLeaf(GetNextSide(), nextHeightIndex++, canBreak: true);

            currentLeaf.SetHighlight(false);
            targetLeaf.SetHighlight(true);
        }

        /// <summary>
        /// Place left/right lanes at the center of each screen half for 1080×1920.
        /// </summary>
        public void ApplyPhoneLayout()
        {
            var cam = Camera.main;
            var ortho = cam != null && cam.orthographic
                ? cam.orthographicSize
                : PlatformRuntime.PhoneOrthoSize;
            horizontalOffset = PlatformRuntime.LaneOffset(ortho);
        }

        public void AdvanceToNextLeaf()
        {
            if (targetLeaf == null) return;

            // Safety: if jump path missed disarm, stop shake on the pad we left.
            currentLeaf?.CancelBreakCountdown();
            currentLeaf.SetHighlight(false);
            currentLeaf = targetLeaf;
            currentLeaf.SetHighlight(false);

            var nextIsBreakable = Random.value < breakableChance;
            targetLeaf = SpawnLeaf(GetNextSide(), nextHeightIndex++, canBreak: nextIsBreakable);
            targetLeaf.SetHighlight(true);

            CullPlatformsFarBelow(currentLeaf.HeightIndex);
        }

        public void BreakCurrentLeaf()
        {
            currentLeaf?.Break();
        }

        /// <summary>
        /// Highest platform the player just fell through (solid or breakable).
        /// Used for: land on solid, or shatter breakable and keep falling.
        /// </summary>
        public Leaf FindFallContact(
            float playerX,
            float previousY,
            float currentY,
            float catchRadius,
            Leaf exclude = null)
        {
            Leaf best = null;
            var bestY = float.NegativeInfinity;

            for (var i = 0; i < activeLeaves.Count; i++)
            {
                var leaf = activeLeaves[i];
                if (leaf == null || !leaf.gameObject.activeInHierarchy) continue;
                if (leaf == exclude) continue;
                if (leaf.IsBroken) continue;

                var standY = leaf.GetStandPosition().y;
                // Crossed the pad top while moving downward.
                if (previousY < standY - 0.15f) continue;
                if (currentY > standY + 0.2f) continue;
                if (previousY < currentY) continue;

                var dx = Mathf.Abs(playerX - leaf.transform.position.x);
                if (dx > catchRadius) continue;

                if (standY > bestY)
                {
                    bestY = standY;
                    best = leaf;
                }
            }

            return best;
        }

        public float LaneX(LeafSide side) => (int)side * horizontalOffset;

        /// <summary>
        /// Clutch save on a solid pad.
        /// Wipes everything ABOVE the save (no teleport back up) and spawns a fresh target one step higher.
        /// </summary>
        public void RecoverTo(Leaf leaf)
        {
            if (leaf == null) return;

            if (currentLeaf != null && currentLeaf != leaf)
                currentLeaf.SetHighlight(false);
            if (targetLeaf != null)
                targetLeaf.SetHighlight(false);

            // Kill the old climb path above — next jump must be only one step up from here.
            DespawnLeavesAbove(leaf.HeightIndex);
            targetLeaf = null;

            currentLeaf = leaf;
            currentLeaf.SetHighlight(false);

            lastSide = currentLeaf.Side;
            consecutiveSameSide = 1;
            nextHeightIndex = currentLeaf.HeightIndex + 1;

            var nextIsBreakable = Random.value < breakableChance;
            targetLeaf = SpawnLeaf(GetNextSide(), nextHeightIndex++, canBreak: nextIsBreakable);
            targetLeaf.SetHighlight(true);

            CullPlatformsFarBelow(currentLeaf.HeightIndex);
        }

        /// <summary>Remove every platform strictly above the given height index.</summary>
        public void DespawnLeavesAbove(int heightIndex)
        {
            for (var i = activeLeaves.Count - 1; i >= 0; i--)
            {
                var leaf = activeLeaves[i];
                if (leaf == null)
                {
                    activeLeaves.RemoveAt(i);
                    continue;
                }

                if (leaf.HeightIndex > heightIndex)
                {
                    DespawnLeaf(leaf);
                    activeLeaves.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Call while falling so pads more than N steps under the player disappear.
        /// </summary>
        public void CullBelowPlayerY(float playerY)
        {
            if (verticalSpacing <= 0.01f) return;

            // Approximate height index from world Y, then cull older pads.
            var approxIndex = Mathf.FloorToInt(playerY / verticalSpacing);
            CullPlatformsFarBelow(approxIndex);
        }

        public void CullPlatformsFarBelow(int referenceHeightIndex)
        {
            var minKeep = referenceHeightIndex - destroyPlatformsBelow;

            for (var i = activeLeaves.Count - 1; i >= 0; i--)
            {
                var leaf = activeLeaves[i];
                if (leaf == null)
                {
                    activeLeaves.RemoveAt(i);
                    continue;
                }

                // Never cull what you're standing on or the current target.
                if (leaf == currentLeaf || leaf == targetLeaf)
                    continue;

                if (leaf.HeightIndex < minKeep || leaf.IsBroken)
                {
                    DespawnLeaf(leaf);
                    activeLeaves.RemoveAt(i);
                }
            }
        }

        void DespawnLeaf(Leaf leaf)
        {
            if (leaf == null) return;
            leaf.StopAllCoroutines();
            leaf.gameObject.SetActive(false);
        }

        /// <summary>Uses CurrencySettings spawn interval/chance. Starting pad never has one.</summary>
        static bool ShouldHaveCoin(int heightIndex)
        {
            var settings = CurrencySettings.Load();
            if (settings != null)
                return settings.ShouldSpawnCoin(heightIndex);

            // Fallback: every 3rd pad if settings asset is missing.
            return heightIndex > 0 && heightIndex % 3 == 0;
        }

        Leaf SpawnLeaf(LeafSide side, int heightIndex, bool canBreak)
        {
            var leaf = GetFromPool();
            leaf.Initialize(
                side,
                heightIndex,
                horizontalOffset,
                heightIndex * verticalSpacing,
                activePlatform,
                canBreak,
                withCoin: ShouldHaveCoin(heightIndex));
            if (!activeLeaves.Contains(leaf))
                activeLeaves.Add(leaf);
            return leaf;
        }

        LeafSide GetNextSide()
        {
            LeafSide next;

            if (consecutiveSameSide >= MaxConsecutiveSameSide)
            {
                next = Opposite(lastSide);
            }
            else if (consecutiveSameSide == MaxConsecutiveSameSide - 1)
            {
                next = Random.value < ThirdInARowChance ? lastSide : Opposite(lastSide);
            }
            else
            {
                next = Random.value < 0.5f ? LeafSide.Left : LeafSide.Right;
            }

            RegisterSide(next);
            return next;
        }

        void RegisterSide(LeafSide side)
        {
            if (side == lastSide)
                consecutiveSameSide++;
            else
                consecutiveSameSide = 1;

            lastSide = side;
        }

        static LeafSide Opposite(LeafSide side)
        {
            return side == LeafSide.Left ? LeafSide.Right : LeafSide.Left;
        }

        Leaf GetFromPool()
        {
            foreach (var leaf in pool)
            {
                if (!leaf.gameObject.activeInHierarchy)
                    return leaf;
            }

            return CreateLeafInstance();
        }

        void EnsurePool()
        {
            if (leafPrefab == null)
            {
                var go = new GameObject("LeafTemplate");
                go.SetActive(false);
                go.transform.SetParent(leafContainer != null ? leafContainer : transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = PixelSpriteFactory.CreateLeafSprite();
                renderer.sortingOrder = 2;
                leafPrefab = go.AddComponent<Leaf>();
            }

            if (leafContainer == null)
                leafContainer = transform;

            while (pool.Count < poolSize)
                pool.Add(CreateLeafInstance());
        }

        Leaf CreateLeafInstance()
        {
            var parent = leafContainer != null ? leafContainer : transform;
            var instance = Instantiate(leafPrefab, parent);
            instance.gameObject.SetActive(false);
            if (!pool.Contains(instance))
                pool.Add(instance);
            return instance;
        }

        void ClearActive()
        {
            foreach (var leaf in pool)
            {
                if (leaf == null) continue;
                leaf.StopAllCoroutines();
                leaf.gameObject.SetActive(false);
            }

            activeLeaves.Clear();
            currentLeaf = null;
            targetLeaf = null;
        }
    }
}
