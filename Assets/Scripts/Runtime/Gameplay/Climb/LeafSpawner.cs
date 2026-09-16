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
        [Tooltip("Chance a mid-climb platform is BREAKABLE (0.75s after land, or wrong-side miss). Rest are solid.")]
        [Range(0f, 1f)]
        [SerializeField] float breakableChance = 0.75f;

        [Header("Cull old platforms")]
        [Tooltip("Platforms this many height steps below the player are destroyed so you can't fall forever.")]
        [Min(1)]
        [SerializeField] int destroyPlatformsBelow = 5;

        const int MaxConsecutiveSameSide = 3;
        const float ThirdInARowChance = 0.20f;
        /// <summary>Combatant mode: always keep this many pads above the player ready (and combatants with them).</summary>
        const int CombatantLookaheadPads = 5;

        readonly List<Leaf> pool = new();
        readonly List<Leaf> activeLeaves = new();
        /// <summary>Race: every pad from ground to finish, index = height.</summary>
        readonly List<Leaf> raceLeaves = new();
        /// <summary>Side chosen for each height index (kept even after pads are culled).</summary>
        readonly List<LeafSide> sidesByHeight = new();

        Leaf currentLeaf;
        Leaf targetLeaf;
        /// <summary>Platform beyond the jump target — spawned on leap so you can plan the next hop mid-air.</summary>
        Leaf lookaheadLeaf;
        int nextHeightIndex;
        LeafSide lastSide = LeafSide.Left;
        int consecutiveSameSide;

        Platform activePlatform;
        GameMode activeMode = GameMode.Classic;
        Difficulty activeDifficulty = Difficulty.Easy;
        float groundY;
        int raceFinishHeight = -1;
        Transform finishLine;
        bool raceCourseBuilt;
        int lastLayoutW = -1;
        int lastLayoutH = -1;

        public Leaf CurrentLeaf => currentLeaf;
        public Leaf TargetLeaf => targetLeaf;
        public Leaf LookaheadLeaf => lookaheadLeaf;
        public float VerticalSpacing => verticalSpacing;
        public float HorizontalOffset => horizontalOffset;
        /// <summary>Highest height index with a recorded side (exclusive upper bound of known sides).</summary>
        public int KnownSideCount => sidesByHeight.Count;
        /// <summary>World Y of the starter platform (height 0) — fall/camera ground.</summary>
        public float GroundY => groundY;
        public int RaceFinishHeight => raceFinishHeight;
        public int DestroyPlatformsBelow => destroyPlatformsBelow;
        public float BreakableChance => breakableChance;
        public GameMode ActiveMode => activeMode;
        public Difficulty ActiveDifficulty => activeDifficulty;
        public IReadOnlyList<Leaf> ActiveLeaves => activeLeaves;

        /// <summary>Apply Classic / Blackout / Race + Easy / Hard spawn rules for the next run.</summary>
        public void SetGameMode(GameMode mode, Difficulty difficulty = Difficulty.Easy)
        {
            activeMode = mode;
            activeDifficulty = difficulty;
            breakableChance = GameModeRules.BreakableChance(mode, difficulty);
            if (!GameModeRules.IsRace(mode))
                ClearRaceFinish();
        }

        /// <summary>
        /// Race: finish pad at this height index (100 easy / 200 hard). Centered; any tap works.
        /// Checkered banner sits between finish-1 and finish.
        /// </summary>
        public void SetRaceFinishHeight(int heightIndex)
        {
            raceFinishHeight = Mathf.Max(1, heightIndex);
            EnsureFinishLine();
        }

        public void ClearRaceFinish()
        {
            raceFinishHeight = -1;
            if (finishLine != null)
                finishLine.gameObject.SetActive(false);
        }

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
            breakableChance = GameModeRules.BreakableChance(activeMode, activeDifficulty);

            nextHeightIndex = 0;
            consecutiveSameSide = 0;
            raceCourseBuilt = false;
            raceLeaves.Clear();

            if (raceFinishHeight > 0)
            {
                BuildRaceCourse();
                return;
            }

            // Starter is always solid so the run can begin cleanly.
            currentLeaf = SpawnLeaf(LeafSide.Left, nextHeightIndex++, canBreak: false);
            RegisterSide(LeafSide.Left);
            groundY = currentLeaf.transform.position.y;

            // First target follows mode + difficulty (Hard → always breakable).
            targetLeaf = SpawnClimbLeaf(nextHeightIndex++);

            // Combatant: pre-spawn several pads so flyers appear with the path, not out of thin air.
            EnsureCombatantLookahead();

            currentLeaf.SetHighlight(false);
            if (targetLeaf != null)
                targetLeaf.SetHighlight(true);
            else
            {
                targetLeaf = FindLeafAtHeight(currentLeaf.HeightIndex + 1);
                if (targetLeaf != null)
                    targetLeaf.SetHighlight(true);
            }
        }

        /// <summary>
        /// Race: pre-build every pad from 0 → finish so player and AI share one path.
        /// </summary>
        void BuildRaceCourse()
        {
            var top = raceFinishHeight;
            // Ensure pool can hold the full course.
            while (pool.Count < top + 1)
                pool.Add(CreateLeafInstance());

            raceLeaves.Capacity = Mathf.Max(raceLeaves.Capacity, top + 1);

            // Height 0 — starter (left, solid).
            var start = SpawnLeaf(LeafSide.Left, 0, canBreak: false);
            RegisterSide(LeafSide.Left);
            raceLeaves.Add(start);
            groundY = start.transform.position.y;

            for (var h = 1; h <= top; h++)
            {
                var leaf = SpawnClimbLeaf(h);
                raceLeaves.Add(leaf);
            }

            nextHeightIndex = top + 1;
            raceCourseBuilt = true;

            currentLeaf = raceLeaves[0];
            targetLeaf = raceLeaves.Count > 1 ? raceLeaves[1] : null;
            lookaheadLeaf = raceLeaves.Count > 2 ? raceLeaves[2] : null;

            currentLeaf.SetHighlight(false);
            if (targetLeaf != null)
                targetLeaf.SetHighlight(true);
            if (lookaheadLeaf != null)
                lookaheadLeaf.SetHighlight(false);

            EnsureFinishLine();
        }

        /// <summary>Pad at height index for the shared race path (null if missing).</summary>
        public Leaf GetRaceLeafAt(int heightIndex)
        {
            if (heightIndex < 0 || heightIndex >= raceLeaves.Count)
                return null;
            return raceLeaves[heightIndex];
        }

        /// <summary>World stand position for a race height (ghost + AI path).</summary>
        public Vector3 GetRaceStandPosition(int heightIndex, float verticalOffset)
        {
            var leaf = GetRaceLeafAt(heightIndex);
            if (leaf != null)
                return leaf.GetStandPosition(verticalOffset);

            // Fallback if leaf was culled — reconstruct from side list via leaf if available.
            var y = GameSettings.PadWorldY(heightIndex, verticalSpacing);
            return new Vector3(0f, y, 0f) + GameSettings.StandOffset(verticalOffset);
        }

        public bool HasRaceCourse => raceCourseBuilt && raceLeaves.Count > 0;

        /// <summary>Show or hide pad sprites from the Platform Visibility setting.</summary>
        public void ApplyPlatformVisibility()
        {
            for (var i = 0; i < activeLeaves.Count; i++)
                activeLeaves[i]?.ApplyVisibility();
            for (var i = 0; i < raceLeaves.Count; i++)
                raceLeaves[i]?.ApplyVisibility();
            if (finishLine != null)
            {
                var banner = finishLine.GetComponent<SpriteRenderer>();
                if (banner != null)
                    banner.enabled = GameSettings.PlatformsVisible;
            }
        }

        void LateUpdate()
        {
            if (Screen.width == lastLayoutW && Screen.height == lastLayoutH)
                return;
            ApplyPhoneLayout();
        }

        /// <summary>
        /// Place left/right lanes at the center of each visible screen half.
        /// Re-runs when the display size changes.
        /// </summary>
        public void ApplyPhoneLayout()
        {
            // Match-width: lane X is constant; only vertical view changes with aspect.
            horizontalOffset = PlatformRuntime.LaneOffset();
            lastLayoutW = Screen.width;
            lastLayoutH = Screen.height;
            RelayoutActiveLeaves();
            if (raceFinishHeight > 0)
                EnsureFinishLine();
        }

        void RelayoutActiveLeaves()
        {
            for (var i = 0; i < activeLeaves.Count; i++)
                activeLeaves[i]?.RelayoutX(horizontalOffset);
            for (var i = 0; i < raceLeaves.Count; i++)
                raceLeaves[i]?.RelayoutX(horizontalOffset);
        }

        /// <summary>
        /// Call when the player leaves the pad. Reveals the platform after the landing
        /// target so the next jump can be planned mid-air.
        /// </summary>
        public void SpawnLookaheadOnLeap()
        {
            if (targetLeaf == null) return;

            // Race course is pre-built — just point at the next pad in the path.
            if (raceCourseBuilt)
            {
                var h = targetLeaf.HeightIndex + 1;
                lookaheadLeaf = GetRaceLeafAt(h);
                if (lookaheadLeaf != null)
                {
                    if (!lookaheadLeaf.gameObject.activeInHierarchy)
                        lookaheadLeaf.gameObject.SetActive(true);
                    lookaheadLeaf.SetHighlight(false);
                }

                return;
            }

            // Combatant: buffer already holds several pads; keep it topped up and point ahead.
            if (GameModeRules.IsCombatant(activeMode))
            {
                EnsureCombatantLookahead();
                lookaheadLeaf = FindLeafAtHeight(targetLeaf.HeightIndex + 1);
                if (lookaheadLeaf != null)
                {
                    Reactivate(lookaheadLeaf);
                    lookaheadLeaf.SetHighlight(false);
                }

                return;
            }

            if (lookaheadLeaf != null && lookaheadLeaf.gameObject.activeInHierarchy)
                return;
            if (raceFinishHeight > 0 && nextHeightIndex > raceFinishHeight)
                return;

            lookaheadLeaf = SpawnClimbLeaf(nextHeightIndex++);
            if (lookaheadLeaf != null)
                lookaheadLeaf.SetHighlight(false);
        }

        public void AdvanceToNextLeaf()
        {
            if (targetLeaf == null) return;

            // Safety: if jump path missed disarm, stop shake on the pad we left.
            currentLeaf?.CancelBreakCountdown();
            currentLeaf.SetHighlight(false);
            currentLeaf = targetLeaf;
            currentLeaf.SetHighlight(false);

            // Finished the race pad — no more climb targets.
            if (currentLeaf.IsFinishPad ||
                (raceFinishHeight > 0 && currentLeaf.HeightIndex >= raceFinishHeight))
            {
                targetLeaf = null;
                lookaheadLeaf = null;
                CullPlatformsFarBelow(currentLeaf.HeightIndex);
                return;
            }

            if (raceCourseBuilt)
            {
                var h = currentLeaf.HeightIndex;
                targetLeaf = GetRaceLeafAt(h + 1);
                lookaheadLeaf = GetRaceLeafAt(h + 2);
                if (targetLeaf != null)
                {
                    if (!targetLeaf.gameObject.activeInHierarchy)
                        targetLeaf.gameObject.SetActive(true);
                    targetLeaf.SetHighlight(true);
                }

                if (lookaheadLeaf != null && !lookaheadLeaf.gameObject.activeInHierarchy)
                    lookaheadLeaf.gameObject.SetActive(true);

                CullPlatformsFarBelow(currentLeaf.HeightIndex);
                return;
            }

            if (GameModeRules.IsCombatant(activeMode))
            {
                EnsureCombatantLookahead();
                targetLeaf = FindLeafAtHeight(currentLeaf.HeightIndex + 1);
                lookaheadLeaf = FindLeafAtHeight(currentLeaf.HeightIndex + 2);
                if (targetLeaf != null)
                {
                    Reactivate(targetLeaf);
                    targetLeaf.SetHighlight(true);
                }

                if (lookaheadLeaf != null)
                {
                    Reactivate(lookaheadLeaf);
                    lookaheadLeaf.SetHighlight(false);
                }

                CullPlatformsFarBelow(currentLeaf.HeightIndex);
                return;
            }

            // Prefer the pad revealed on leap; spawn only if leap never pre-created it.
            if (lookaheadLeaf != null && lookaheadLeaf.gameObject.activeInHierarchy)
            {
                targetLeaf = lookaheadLeaf;
                lookaheadLeaf = null;
            }
            else
            {
                lookaheadLeaf = null;
                if (raceFinishHeight > 0 && nextHeightIndex > raceFinishHeight)
                    targetLeaf = null;
                else
                    targetLeaf = SpawnClimbLeaf(nextHeightIndex++);
            }

            if (targetLeaf != null)
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
        /// <param name="exclude">Pad we just left — never clutch-save back onto it.</param>
        /// <param name="excludeHeightIndex">
        /// Also skip every pad at this height (covers same-lane vertical misses that would
        /// otherwise re-land on the departure pad and convert it).
        /// </param>
        public Leaf FindFallContact(
            float playerX,
            float previousY,
            float currentY,
            float catchRadius,
            Leaf exclude = null,
            int excludeHeightIndex = int.MinValue)
        {
            Leaf best = null;
            var bestY = float.NegativeInfinity;

            for (var i = 0; i < activeLeaves.Count; i++)
            {
                var leaf = activeLeaves[i];
                if (leaf == null || !leaf.gameObject.activeInHierarchy) continue;
                if (leaf == exclude) continue;
                if (excludeHeightIndex != int.MinValue && leaf.HeightIndex == excludeHeightIndex)
                    continue;
                if (leaf.IsBroken) continue;

                var standY = leaf.GetStandPosition().y;
                var sign = GameSettings.ClimbSign;
                var prevP = previousY * sign;
                var curP = currentY * sign;
                var standP = standY * sign;
                // Crossed the stand plane while moving in the fall direction.
                if (prevP < standP - 0.15f) continue;
                if (curP > standP + 0.2f) continue;
                if (prevP < curP) continue;

                var dx = Mathf.Abs(playerX - leaf.transform.position.x);
                if (dx > catchRadius) continue;

                if (standP > bestY)
                {
                    bestY = standP;
                    best = leaf;
                }
            }

            return best;
        }

        public float LaneX(LeafSide side) => (int)side * horizontalOffset;

        /// <summary>
        /// World Y of the pad art at this height (includes spawn-offset nudge).
        /// Combatants use this so raised/lowered pads don't leave them in the old gap.
        /// </summary>
        public float PadVisualWorldY(int heightIndex)
        {
            var leaf = FindLeafAtHeight(heightIndex);
            if (leaf != null)
                return leaf.VisualWorldY;

            var y = GameSettings.PadWorldY(heightIndex, verticalSpacing);
            if (activePlatform != null)
                y += activePlatform.spawnOffsetY;
            return y;
        }

        /// <summary>Side of the pad at this height, if it has been spawned this run.</summary>
        public bool TryGetSideAt(int heightIndex, out LeafSide side)
        {
            if (heightIndex < 0 || heightIndex >= sidesByHeight.Count)
            {
                side = LeafSide.Left;
                return false;
            }

            side = sidesByHeight[heightIndex];
            return true;
        }

        /// <summary>
        /// Jump from <paramref name="fromHeight"/> → fromHeight+1.
        /// Diagonal = different lanes (needs a vertical combatant obstacle).
        /// Same side = vertical jump (needs a horizontal combatant obstacle).
        /// Returns false if either pad side is not known yet.
        /// </summary>
        public bool TryGetJumpIsDiagonal(int fromHeight, out bool isDiagonal)
        {
            isDiagonal = false;
            if (!TryGetSideAt(fromHeight, out var from) || !TryGetSideAt(fromHeight + 1, out var to))
                return false;

            // Center pads: treat as non-diagonal (straight up the middle).
            if (from == LeafSide.Center || to == LeafSide.Center)
            {
                isDiagonal = false;
                return true;
            }

            isDiagonal = from != to;
            return true;
        }

        void RecordSide(int heightIndex, LeafSide side)
        {
            if (heightIndex < 0) return;
            while (sidesByHeight.Count <= heightIndex)
                sidesByHeight.Add(LeafSide.Left);
            sidesByHeight[heightIndex] = side;
        }

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
            lookaheadLeaf = null;

            // Drop side history above the save so re-rolled pads get fresh combatant matching.
            var keepSides = leaf.HeightIndex + 1;
            if (sidesByHeight.Count > keepSides)
                sidesByHeight.RemoveRange(keepSides, sidesByHeight.Count - keepSides);

            CombatantDirector.Instance?.ClearAbove(leaf.HeightIndex);

            currentLeaf = leaf;
            currentLeaf.SetHighlight(false);

            // Clutch land on a solid pad: it becomes breakable so you can't camp it.
            if (currentLeaf.IsSolid)
                currentLeaf.ConvertToBreakable();

            lastSide = currentLeaf.Side;
            consecutiveSameSide = 1;
            nextHeightIndex = currentLeaf.HeightIndex + 1;

            // Immediate target so you can jump again; next-after-that appears on leap.
            if (raceCourseBuilt)
            {
                var h = currentLeaf.HeightIndex;
                targetLeaf = GetRaceLeafAt(h + 1);
                lookaheadLeaf = GetRaceLeafAt(h + 2);
                if (targetLeaf != null)
                {
                    Reactivate(targetLeaf);
                    targetLeaf.SetHighlight(true);
                }
            }
            else if (raceFinishHeight > 0 && nextHeightIndex > raceFinishHeight)
            {
                targetLeaf = null;
            }
            else if (GameModeRules.IsCombatant(activeMode))
            {
                EnsureCombatantLookahead();
                targetLeaf = FindLeafAtHeight(currentLeaf.HeightIndex + 1);
                lookaheadLeaf = FindLeafAtHeight(currentLeaf.HeightIndex + 2);
                if (targetLeaf != null)
                {
                    Reactivate(targetLeaf);
                    targetLeaf.SetHighlight(true);
                }

                if (lookaheadLeaf != null)
                {
                    Reactivate(lookaheadLeaf);
                    lookaheadLeaf.SetHighlight(false);
                }
            }
            else
            {
                targetLeaf = SpawnClimbLeaf(nextHeightIndex++);
                if (targetLeaf != null)
                    targetLeaf.SetHighlight(true);
            }

            CullPlatformsFarBelow(currentLeaf.HeightIndex);
        }

        /// <summary>
        /// Combatant mode: keep pads (and thus combatant gaps) ready up to N heights above the player.
        /// </summary>
        void EnsureCombatantLookahead()
        {
            if (!GameModeRules.IsCombatant(activeMode) || raceCourseBuilt)
                return;

            var baseHeight = currentLeaf != null ? currentLeaf.HeightIndex : 0;
            var wantTop = baseHeight + CombatantLookaheadPads;

            while (nextHeightIndex <= wantTop)
            {
                if (raceFinishHeight > 0 && nextHeightIndex > raceFinishHeight)
                    break;

                var leaf = SpawnClimbLeaf(nextHeightIndex++);
                if (leaf != null)
                    leaf.SetHighlight(false);
            }

            // Point target/lookahead at the pre-spawned path if missing.
            if (currentLeaf != null)
            {
                if (targetLeaf == null || !targetLeaf.gameObject.activeInHierarchy)
                    targetLeaf = FindLeafAtHeight(currentLeaf.HeightIndex + 1);
                if (lookaheadLeaf == null || !lookaheadLeaf.gameObject.activeInHierarchy)
                    lookaheadLeaf = FindLeafAtHeight(currentLeaf.HeightIndex + 2);
            }
        }

        Leaf FindLeafAtHeight(int heightIndex)
        {
            for (var i = 0; i < activeLeaves.Count; i++)
            {
                var leaf = activeLeaves[i];
                if (leaf != null && leaf.HeightIndex == heightIndex && leaf.gameObject.activeInHierarchy)
                    return leaf;
            }

            // Inactive-but-pooled match (re-activated by caller if needed).
            for (var i = 0; i < activeLeaves.Count; i++)
            {
                var leaf = activeLeaves[i];
                if (leaf != null && leaf.HeightIndex == heightIndex)
                    return leaf;
            }

            return null;
        }

        static void Reactivate(Leaf leaf)
        {
            if (leaf != null && !leaf.gameObject.activeInHierarchy)
                leaf.gameObject.SetActive(true);
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
                    if (leaf == targetLeaf) targetLeaf = null;
                    if (leaf == lookaheadLeaf) lookaheadLeaf = null;
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
            var approxIndex = GameSettings.HeightIndexFromY(playerY, 0f, verticalSpacing);
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

                // Never cull standing pad, jump target, leap lookahead, or finish pad.
                if (leaf == currentLeaf || leaf == targetLeaf || leaf == lookaheadLeaf)
                    continue;
                if (leaf.IsFinishPad)
                    continue;

                // Combatant: keep the pre-spawned path above the player (combatants live in those gaps).
                if (GameModeRules.IsCombatant(activeMode) &&
                    leaf.HeightIndex <= referenceHeightIndex + CombatantLookaheadPads)
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
            leaf.ResetPooledState();
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

        Leaf SpawnClimbLeaf(int heightIndex)
        {
            if (raceFinishHeight > 0 && heightIndex == raceFinishHeight)
            {
                // Centered finish pad — solid; either tap side lands it.
                return SpawnLeaf(LeafSide.Center, heightIndex, canBreak: false, isFinish: true);
            }

            var canBreak = Random.value < breakableChance;

            // Combatant Easy: pad under a bee gap is always solid so you can wait.
            if (GameModeRules.IsCombatant(activeMode) &&
                GameModeRules.CombatantEasySolidBeforeBee(activeDifficulty) &&
                CombatantDirector.HasBeeAbovePad(heightIndex, activeDifficulty))
            {
                canBreak = false;
            }

            return SpawnLeaf(GetNextSide(), heightIndex, canBreak);
        }

        Leaf SpawnLeaf(LeafSide side, int heightIndex, bool canBreak, bool isFinish = false)
        {
            var leaf = GetFromPool();
            leaf.Initialize(
                side,
                heightIndex,
                horizontalOffset,
                GameSettings.PadWorldY(heightIndex, verticalSpacing),
                activePlatform,
                canBreak,
                withCoin: !isFinish && ShouldHaveCoin(heightIndex),
                isFinishPad: isFinish);
            RecordSide(heightIndex, isFinish ? LeafSide.Center : side);
            if (!activeLeaves.Contains(leaf))
                activeLeaves.Add(leaf);

            // Combatant mode: new pad sides unlock jump-type matching for hazards.
            if (GameModeRules.IsCombatant(activeMode))
                CombatantDirector.Instance?.NotifyPlatformsChanged();

            return leaf;
        }

        void EnsureFinishLine()
        {
            if (raceFinishHeight <= 0) return;

            if (finishLine == null)
            {
                var go = new GameObject("RaceFinishLine");
                go.transform.SetParent(leafContainer != null ? leafContainer : transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCheckeredBannerSprite();
                sr.sortingOrder = 3;
                CrispVisuals.MakeSpriteCrisp(sr);
                finishLine = go.transform;
            }

            // Banner sits between finish-1 and finish (100th↔101st / 200th↔201st).
            var y = GameSettings.PadWorldY(raceFinishHeight, verticalSpacing)
                    - 0.5f * verticalSpacing * GameSettings.ClimbSign;
            finishLine.position = CrispVisuals.SnapWorldPosition(new Vector3(0f, y, 0f));
            var banner = finishLine.GetComponent<SpriteRenderer>();
            if (banner != null)
                banner.enabled = GameSettings.PlatformsVisible;
            var width = horizontalOffset * 2.6f;
            var h = Mathf.Max(0.35f, verticalSpacing * 0.22f);
            // Sprite is 64×16; scale to world size.
            finishLine.localScale = new Vector3(width / 0.64f, h / 0.16f, 1f);
            finishLine.gameObject.SetActive(true);
        }

        static Sprite CreateCheckeredBannerSprite()
        {
            const int w = 64;
            const int h = 16;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            const int cell = 8;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var cx = x / cell;
                    var cy = y / cell;
                    var black = ((cx + cy) & 1) == 0;
                    tex.SetPixel(x, y, black ? Color.black : Color.white);
                }
            }

            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
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
            raceLeaves.Clear();
            sidesByHeight.Clear();
            raceCourseBuilt = false;
            currentLeaf = null;
            targetLeaf = null;
            lookaheadLeaf = null;
        }
    }
}
