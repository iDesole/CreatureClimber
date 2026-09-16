using System.Collections;
using UnityEngine;

namespace CreatureClimb
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] LeafSpawner leafSpawner;
        [SerializeField] float jumpDuration = 0.22f;
        [SerializeField] float fallGravity = 14f;
        [Tooltip("Base free-fall terminal speed. Actual cap ramps up to 2× this as you fall.")]
        [SerializeField] float maxFallSpeed = 12f;
        [SerializeField] float jumpArcHeight = 0.7f;
        [SerializeField] float fallCatchRadius = 1.5f;
        [SerializeField] float missUpKick = 1.6f;
        [SerializeField] float fallLanePull = 14f;
        [Tooltip("Death screen after falling this many platform heights.")]
        [SerializeField] int gameOverFallPlatforms = 8;
        [Tooltip("How far below the starter platform the player falls before despawning.")]
        [SerializeField] float fallPastGround = 5f;

        SpriteRenderer spriteRenderer;
        Coroutine movementRoutine;
        bool isBusy;
        float baseJumpDuration;
        float baseFallGravity;
        float standOffsetY = 0.35f;
        Vector3 restScale = Vector3.one;
        Creature creature;
        bool raceFinishSloMoActive;

        const float RaceFinishTimeScale = 0.32f;

        public bool IsBusy => isBusy;
        public Creature CurrentCreature => creature;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            baseJumpDuration = jumpDuration;
            baseFallGravity = fallGravity;
            restScale = transform.localScale;
            spriteRenderer.sortingOrder = 5;

            if (spriteRenderer.sprite == null)
                spriteRenderer.sprite = PixelSpriteFactory.CreateFrogSprite();
        }

        public void Bind(LeafSpawner spawner)
        {
            leafSpawner = spawner;
        }

        public void ApplyCreature(Creature definition)
        {
            if (definition == null) return;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (baseJumpDuration <= 0f)
                baseJumpDuration = jumpDuration;
            if (baseFallGravity <= 0f)
                baseFallGravity = fallGravity;

            creature = definition;
            var resolved = definition.ResolveSprite();
            if (resolved != null && spriteRenderer != null)
                spriteRenderer.sprite = resolved;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = definition.tint;
                spriteRenderer.flipY = false;
                CrispVisuals.MakeSpriteCrisp(spriteRenderer);
            }

            var req = Mathf.Max(0.1f, definition.scale);
            var fit = PlatformRuntime.FitScaleForCreature(
                spriteRenderer != null ? spriteRenderer.sprite : null, req);
            restScale = Vector3.one * fit;
            transform.localScale = restScale;
            standOffsetY = definition.standOffsetY * (fit / req);

            var speed = Mathf.Max(0.1f, definition.jumpSpeed);
            jumpDuration = baseJumpDuration / speed;
            fallGravity = baseFallGravity * Mathf.Max(0.1f, definition.fallSpeed);

            gameObject.name = $"Player_{definition.DisplayName.Replace(' ', '_')}";
        }

        public void ResetToStart()
        {
            if (movementRoutine != null)
                StopCoroutine(movementRoutine);

            isBusy = false;
            EndRaceFinishSloMo();
            movementRoutine = null;
            transform.rotation = Quaternion.identity;
            transform.localScale = restScale;
            if (spriteRenderer != null)
                spriteRenderer.flipY = false;
            CameraFollow.EndFallFollow();
            SetPlayerVisible(true);

            if (creature != null && spriteRenderer != null)
                spriteRenderer.color = creature.tint;
            else if (spriteRenderer != null)
                spriteRenderer.color = Color.white;

            SnapToCurrentLeaf();
        }

        void SetPlayerVisible(bool visible)
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.enabled = visible;
        }

        public void SnapToCurrentLeaf()
        {
            if (leafSpawner?.CurrentLeaf == null) return;
            transform.position = CrispVisuals.SnapWorldPosition(GetStandPos(leafSpawner.CurrentLeaf));
        }

        public void TryHandleTap(LeafSide tappedSide)
        {
            if (isBusy || leafSpawner?.TargetLeaf == null || leafSpawner.CurrentLeaf == null)
                return;

            // Time Trial: countdown starts on the first committed jump, not on Play.
            GameManager.Instance?.BeginTimeTrialClockIfNeeded();

            var current = leafSpawner.CurrentLeaf;
            var target = leafSpawner.TargetLeaf;
            // Last hop onto the race finish (any side counts) — slo-mo either way.
            var isRaceFinishJump = IsRaceFinishJump(target);

            // Finish / center pad: either lane works.
            if (target.IsFinishPad || target.IsCenter)
            {
                FeedbackService.Jump();
                if (isRaceFinishJump)
                    BeginRaceFinishSloMo();
                movementRoutine = StartCoroutine(JumpToTargetRoutine());
                return;
            }

            // Vertical = tap your current lane. Diagonal = tap the other lane.
            // Correct = that choice matches where the next pad actually is.
            var jumpIsVertical = tappedSide == current.Side;
            var targetIsVertical = target.Side == current.Side;
            var correctJump = jumpIsVertical == targetIsVertical;

            FeedbackService.Jump();
            if (isRaceFinishJump)
                BeginRaceFinishSloMo();

            if (correctJump)
            {
                movementRoutine = StartCoroutine(JumpToTargetRoutine());
                return;
            }

            // Wrong diagonal (next pad is same-lane, you crossed): fall (misclick is fine).
            // Wrong vertical (next pad is diagonal, you went straight up):
            //   hop straight up, land back, then:
            //   solid → pad becomes breakable (safe warning)
            //   breakable → pad shatters after land, then fall
            if (jumpIsVertical)
                movementRoutine = StartCoroutine(VerticalMissHopRoutine(current, tappedSide));
            else
            {
                var breakCurrent = current.CanBreak && !current.IsBroken;
                movementRoutine = StartCoroutine(FailAndFallRoutine(breakCurrent, tappedSide, current));
            }
        }

        static bool IsRaceFinishJump(Leaf target)
        {
            if (target == null || !target.IsFinishPad) return false;
            var race = RaceController.Instance;
            return race != null && race.IsRacing;
        }

        void BeginRaceFinishSloMo()
        {
            // Don't fight pause (timeScale already 0).
            if (Time.timeScale < 0.01f) return;
            raceFinishSloMoActive = true;
            Time.timeScale = RaceFinishTimeScale;
        }

        void EndRaceFinishSloMo()
        {
            if (!raceFinishSloMoActive) return;
            raceFinishSloMoActive = false;
            // Restore only if we still own the scale (pause leaves it at 0).
            if (Time.timeScale > 0.01f)
                Time.timeScale = 1f;
        }

        IEnumerator JumpToTargetRoutine()
        {
            isBusy = true;

            // Left this pad successfully — stop its break shake / timer so it
            // cannot shatter under empty air while we are mid-jump.
            GameManager.Instance?.DisarmBreakCountdown(leafSpawner.CurrentLeaf);

            // Reveal the pad after this landing so the next hop is visible mid-air.
            leafSpawner.SpawnLookaheadOnLeap();

            var targetLeaf = leafSpawner.TargetLeaf;
            var target = GetStandPos(targetLeaf);
            var start = transform.position;
            var elapsed = 0f;
            // Same X = vertical hop; different X = diagonal hop.
            var faceRight = target.x >= start.x;

            transform.localScale = new Vector3(restScale.x * 1.15f, restScale.y * 0.85f, restScale.z);
            yield return null;

            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / jumpDuration);
                var eased = EaseOutQuad(t);
                var arc = Mathf.Sin(t * Mathf.PI) * jumpArcHeight;
                var flat = Vector3.Lerp(start, target, eased);
                transform.position = flat + Vector3.up * (arc * GameSettings.ClimbSign);

                var stretch = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
                transform.localScale = new Vector3(restScale.x / stretch, restScale.y * stretch, restScale.z);
                transform.rotation = Quaternion.Euler(0f, 0f, faceRight ? -12f * Mathf.Sin(t * Mathf.PI) : 12f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }

            // Land first — then promote current pad and arm break fuse.
            // Fuse must never run while isBusy / mid-air.
            transform.position = CrispVisuals.SnapWorldPosition(target);
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(restScale.x * 1.2f, restScale.y * 0.8f, restScale.z);
            leafSpawner.AdvanceToNextLeaf();
            yield return null;
            transform.localScale = restScale;

            isBusy = false;
            movementRoutine = null;

            EndRaceFinishSloMo();

            // Only after feet are down and we are no longer busy.
            GameManager.Instance?.RegisterSuccessfulJump();
        }

        /// <summary>
        /// Jumped vertical when the next pad is diagonal.
        /// Always hop straight up and land back first (keeps the climb feel).
        /// Solid: pad becomes breakable (warning, stay safe).
        /// Breakable: after landing, pad shatters, then fall.
        /// </summary>
        IEnumerator VerticalMissHopRoutine(Leaf pad, LeafSide fallSide)
        {
            isBusy = true;

            if (pad == null)
            {
                isBusy = false;
                movementRoutine = null;
                yield break;
            }

            // Pause fuse during the hop so the pad cannot shatter mid-air.
            GameManager.Instance?.DisarmBreakCountdown(pad);

            var wasSolid = pad.IsSolid;
            var start = transform.position;
            var land = GetStandPos(pad);
            var hopDuration = Mathf.Max(0.08f, jumpDuration);
            var hopHeight = jumpArcHeight * 1.15f;

            transform.localScale = new Vector3(restScale.x * 1.15f, restScale.y * 0.85f, restScale.z);
            yield return null;

            // Full up-and-down hop on the same X — pad stays whole in the air.
            var elapsed = 0f;
            while (elapsed < hopDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / hopDuration);
                var arc = Mathf.Sin(t * Mathf.PI) * hopHeight;
                transform.position = start + Vector3.up * (arc * GameSettings.ClimbSign);
                transform.rotation = Quaternion.identity;

                var stretch = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
                transform.localScale = new Vector3(restScale.x / stretch, restScale.y * stretch, restScale.z);
                yield return null;
            }

            // Feet down first.
            transform.position = CrispVisuals.SnapWorldPosition(land);
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(restScale.x * 1.2f, restScale.y * 0.8f, restScale.z);
            yield return null;
            transform.localScale = restScale;

            if (wasSolid)
            {
                // Warning: solid becomes breakable with fuse — stay safe.
                GameManager.Instance?.ConvertSolidPadToBreakable(pad);
                isBusy = false;
                movementRoutine = null;
                yield break;
            }

            // Breakable: land, then shatter under you, then fall.
            if (pad.CanBreak && !pad.IsBroken)
                pad.Break();

            yield return FallFromAirRoutine(
                fallSide: fallSide,
                leftFrom: pad,
                excludePad: pad,
                fallFromHeight: pad.HeightIndex,
                initialUpKick: 0f);
        }

        IEnumerator FailAndFallRoutine(bool breakCurrent, LeafSide fallSide, Leaf leftFrom)
        {
            isBusy = true;
            EndRaceFinishSloMo();

            var fallFromHeight = leftFrom != null
                ? leftFrom.HeightIndex
                : leafSpawner?.CurrentLeaf != null
                    ? leafSpawner.CurrentLeaf.HeightIndex
                    : 0;

            var excludePad = leftFrom != null ? leftFrom : leafSpawner?.CurrentLeaf;

            // Fuse expiry / forced fall: shatter breakable under you.
            if (breakCurrent && leftFrom != null && leftFrom.CanBreak && !leftFrom.IsBroken)
                leftFrom.Break();
            else if (breakCurrent)
                leafSpawner.BreakCurrentLeaf();

            var wobbleTime = 0.07f;
            var wobbleElapsed = 0f;
            var origin = transform.position;
            while (wobbleElapsed < wobbleTime)
            {
                wobbleElapsed += Time.deltaTime;
                transform.position = origin + Vector3.right * (Mathf.Sin(wobbleElapsed * 80f) * 0.06f);
                yield return null;
            }

            yield return FallFromAirRoutine(
                fallSide,
                leftFrom,
                excludePad,
                fallFromHeight,
                missUpKick);
        }

        /// <summary>
        /// Free fall toward the starter platform (ground).
        /// Camera tracks the player down, eases to a stop at ground Y, then the player
        /// drops <see cref="fallPastGround"/> below and is hidden.
        /// </summary>
        IEnumerator FallFromAirRoutine(
            LeafSide fallSide,
            Leaf leftFrom,
            Leaf excludePad,
            int fallFromHeight,
            float initialUpKick)
        {
            leafSpawner?.DespawnLeavesAbove(fallFromHeight);

            // Ground = same Y as the platform we start every run on.
            var groundY = leafSpawner != null ? leafSpawner.GroundY : 0f;
            var sign = GameSettings.ClimbSign;

            GameManager.Instance?.BeginFall();
            GameManager.Instance?.SetScoreFromHeight(fallFromHeight, force: true);
            CameraFollow.BeginFallFollow(groundY);
            SetPlayerVisible(true);

            transform.localScale = restScale;
            transform.rotation = Quaternion.identity;

            var laneX = leafSpawner != null
                ? leafSpawner.LaneX(fallSide)
                : (int)fallSide * 2.4f;

            var velocity = new Vector3(0f, initialUpKick * sign, 0f);
            var leaveY = leftFrom != null ? leftFrom.GetStandPosition().y : transform.position.y;
            var canHitPads = initialUpKick <= 0.01f || (transform.position.y - leaveY) * sign < -0.05f;

            var spacing = leafSpawner != null ? leafSpawner.VerticalSpacing : 2.2f;
            if (spacing < 0.01f) spacing = 2.2f;

            var destroyY = groundY - sign * Mathf.Max(0.1f, fallPastGround);
            var fallDeathY = leaveY - sign * Mathf.Max(1, gameOverFallPlatforms) * spacing;
            var deathY = sign > 0f
                ? Mathf.Max(groundY, fallDeathY)
                : Mathf.Min(groundY, fallDeathY);
            // Ramp terminal velocity from base → 2× base over this much drop.
            var baseCap = Mathf.Max(2f, maxFallSpeed);
            var topCap = baseCap * 2f;
            var rampDistance = Mathf.Max(spacing * 4f, (leaveY - groundY) * sign);

            var catchRadius = fallCatchRadius;
            if (leafSpawner != null)
                catchRadius = Mathf.Max(fallCatchRadius, leafSpawner.HorizontalOffset * 0.65f);

            var gameOverShown = false;

            while ((transform.position.y - destroyY) * sign > 0f)
            {
                var previousY = transform.position.y;
                var pos = transform.position;

                velocity.y -= fallGravity * sign * Time.deltaTime;
                // Speed builds with distance fallen, hard-capped at 2× base maxFallSpeed.
                var fallen = Mathf.Max(0f, (leaveY - pos.y) * sign);
                var rampT = rampDistance > 0.01f ? Mathf.Clamp01(fallen / rampDistance) : 1f;
                var maxDown = Mathf.Lerp(baseCap, topCap, rampT);
                if (velocity.y * sign < -maxDown)
                    velocity.y = -maxDown * sign;

                pos += velocity * Time.deltaTime;
                pos.x = Mathf.MoveTowards(pos.x, laneX, fallLanePull * Time.deltaTime);
                transform.position = pos;
                transform.Rotate(0f, 0f, 400f * Time.deltaTime);
                transform.localScale = restScale;

                if (!canHitPads && (transform.position.y - leaveY) * sign < -0.25f)
                    canHitPads = true;

                leafSpawner?.CullBelowPlayerY(transform.position.y);

                if (!gameOverShown)
                {
                    var liveHeight = Mathf.Clamp(
                        GameSettings.HeightIndexFromY(transform.position.y, standOffsetY, spacing),
                        0,
                        fallFromHeight);
                    GameManager.Instance?.SetScoreFromHeight(liveHeight);
                }

                if (!gameOverShown && (transform.position.y - deathY) * sign <= 0f)
                {
                    gameOverShown = true;
                    GameManager.Instance?.TriggerGameOver();
                }

                // Clutch saves only before death UI, and only on the climb side of ground.
                // Never re-land on the pad / height we just left (vertical miss same-lane trap).
                if (!gameOverShown && canHitPads && (transform.position.y - groundY) * sign > 0f && leafSpawner != null)
                {
                    var hit = leafSpawner.FindFallContact(
                        transform.position.x,
                        previousY,
                        transform.position.y,
                        catchRadius,
                        excludePad,
                        excludeHeightIndex: fallFromHeight);

                    if (hit != null)
                    {
                        if (hit == leftFrom || hit.HeightIndex == fallFromHeight)
                        {
                            // Safety: treat departure height as already failed.
                            if (!hit.IsBroken && hit.CanBreak)
                                hit.Break();
                            if (velocity.y * sign > -2f)
                                velocity.y = -2f * sign;
                            excludePad = hit;
                        }
                        else if (hit.IsSolid)
                        {
                            yield return RecoverOntoSolid(hit);
                            yield break;
                        }
                        else if (!hit.IsBroken)
                        {
                            hit.Break();
                            if (velocity.y * sign > -2f)
                                velocity.y = -2f * sign;
                            excludePad = hit;
                        }
                    }
                }

                yield return null;
            }

            // Dropped past the camera under the starter platform — despawn.
            SetPlayerVisible(false);
            CameraFollow.EndFallFollow();

            if (!gameOverShown)
                GameManager.Instance?.TriggerGameOver();

            isBusy = false;
            movementRoutine = null;
        }

        IEnumerator RecoverOntoSolid(Leaf save)
        {
            CameraFollow.EndFallFollow();

            var land = CrispVisuals.SnapWorldPosition(GetStandPos(save));
            transform.position = land;
            transform.rotation = Quaternion.identity;
            transform.localScale = restScale;

            leafSpawner.RecoverTo(save);
            GameManager.Instance?.RecoverFromFall(save.HeightIndex);
            transform.position = CrispVisuals.SnapWorldPosition(GetStandPos(leafSpawner.CurrentLeaf));

            yield return null;

            isBusy = false;
            movementRoutine = null;
        }

        public void ForceFall()
        {
            ForceFall(interruptBusy: false, breakPad: true);
        }

        /// <summary>
        /// Cancel jump/hop and free-fall from the exact hit world position (no wobble).
        /// Used by combatant hit, tempo wash-out, and crumbled pad underfoot.
        /// </summary>
        public void InterruptAndFallFromHere(bool breakPad)
        {
            if (movementRoutine != null)
            {
                StopCoroutine(movementRoutine);
                movementRoutine = null;
            }

            EndRaceFinishSloMo();

            var holdPos = transform.position;
            transform.rotation = Quaternion.identity;
            transform.localScale = restScale;

            var current = leafSpawner?.CurrentLeaf;
            if (breakPad && current != null && current.CanBreak && !current.IsBroken)
                current.Break();

            var side = current != null
                ? current.Side
                : holdPos.x >= 0f ? LeafSide.Right : LeafSide.Left;

            var fallFromHeight = current != null
                ? current.HeightIndex
                : leafSpawner != null
                    ? GameSettings.HeightIndexFromY(holdPos.y, 0f, leafSpawner.VerticalSpacing)
                    : 0;

            isBusy = true;
            transform.position = holdPos;
            movementRoutine = StartCoroutine(FallFromAirRoutine(
                fallSide: side,
                leftFrom: current,
                excludePad: current,
                fallFromHeight: fallFromHeight,
                initialUpKick: 0f));
        }

        /// <summary>Start a free-fall, optionally interrupting mid-jump.</summary>
        public void ForceFall(bool interruptBusy, bool breakPad)
        {
            if (isBusy)
            {
                if (!interruptBusy) return;
                InterruptAndFallFromHere(breakPad);
                return;
            }

            var current = leafSpawner?.CurrentLeaf;
            var breakCurrent = breakPad &&
                               current != null &&
                               current.CanBreak &&
                               !current.IsBroken;
            var side = current != null ? current.Side : LeafSide.Left;
            movementRoutine = StartCoroutine(FailAndFallRoutine(breakCurrent, side, current));
        }

        Vector3 GetStandPos(Leaf leaf)
        {
            if (leaf == null) return transform.position;
            var offset = creature != null ? creature.standOffsetY : leaf.StandOffsetY;
            return CrispVisuals.SnapWorldPosition(leaf.GetStandPosition(offset));
        }

        static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    }
}
