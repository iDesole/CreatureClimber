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
        [SerializeField] float jumpArcHeight = 0.7f;
        [SerializeField] float fallCatchRadius = 1.5f;
        [SerializeField] float missUpKick = 1.6f;
        [SerializeField] float fallLanePull = 14f;

        SpriteRenderer spriteRenderer;
        Coroutine movementRoutine;
        bool isBusy;
        float baseJumpDuration;
        float baseFallGravity;
        float standOffsetY = 0.35f;
        Vector3 restScale = Vector3.one;
        Creature creature;

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
            movementRoutine = null;
            transform.rotation = Quaternion.identity;
            transform.localScale = restScale;

            if (creature != null && spriteRenderer != null)
                spriteRenderer.color = creature.tint;
            else if (spriteRenderer != null)
                spriteRenderer.color = Color.white;

            SnapToCurrentLeaf();
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

            // Correct side of the NEXT platform above → climb jump.
            if (tappedSide == leafSpawner.TargetLeaf.Side)
            {
                FeedbackService.Jump();
                movementRoutine = StartCoroutine(JumpToTargetRoutine());
                return;
            }

            // Wrong side → fall through breakable pads immediately (no waiting on timer).
            var current = leafSpawner.CurrentLeaf;
            var breakCurrent = current != null && current.CanBreak && !current.IsBroken;
            movementRoutine = StartCoroutine(FailAndFallRoutine(breakCurrent, tappedSide, current));
        }

        IEnumerator JumpToTargetRoutine()
        {
            isBusy = true;

            // Left this pad successfully — stop its break shake / timer.
            // Wrong-side falls still Break() the pad immediately (not this path).
            GameManager.Instance?.DisarmBreakCountdown(leafSpawner.CurrentLeaf);

            var targetLeaf = leafSpawner.TargetLeaf;
            var target = GetStandPos(targetLeaf);
            var start = transform.position;
            var elapsed = 0f;
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
                transform.position = flat + Vector3.up * arc;

                var stretch = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
                transform.localScale = new Vector3(restScale.x / stretch, restScale.y * stretch, restScale.z);
                transform.rotation = Quaternion.Euler(0f, 0f, faceRight ? -12f * Mathf.Sin(t * Mathf.PI) : 12f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }

            transform.position = CrispVisuals.SnapWorldPosition(target);
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(restScale.x * 1.2f, restScale.y * 0.8f, restScale.z);
            leafSpawner.AdvanceToNextLeaf();
            yield return null;
            transform.localScale = restScale;

            isBusy = false;
            movementRoutine = null;

            GameManager.Instance?.RegisterSuccessfulJump();
        }

        IEnumerator FailAndFallRoutine(bool breakCurrent, LeafSide fallSide, Leaf leftFrom)
        {
            isBusy = true;

            var fallFromHeight = leftFrom != null
                ? leftFrom.HeightIndex
                : leafSpawner?.CurrentLeaf != null
                    ? leafSpawner.CurrentLeaf.HeightIndex
                    : 0;

            var excludePad = leftFrom != null ? leftFrom : leafSpawner?.CurrentLeaf;

            // Wrong-side / timed fall: breakable pad shatters immediately under you.
            if (breakCurrent && leftFrom != null && leftFrom.CanBreak && !leftFrom.IsBroken)
                leftFrom.Break();
            else if (breakCurrent)
                leafSpawner.BreakCurrentLeaf();

            leafSpawner?.DespawnLeavesAbove(fallFromHeight);

            GameManager.Instance?.BeginFall();
            GameManager.Instance?.SetScoreFromHeight(fallFromHeight, force: true);

            var wobbleTime = 0.07f;
            var wobbleElapsed = 0f;
            var origin = transform.position;
            while (wobbleElapsed < wobbleTime)
            {
                wobbleElapsed += Time.deltaTime;
                transform.position = origin + Vector3.right * (Mathf.Sin(wobbleElapsed * 80f) * 0.06f);
                yield return null;
            }

            var laneX = leafSpawner != null
                ? leafSpawner.LaneX(fallSide)
                : (int)fallSide * 2.4f;

            var velocity = new Vector3(0f, missUpKick, 0f);
            var leaveY = excludePad != null ? excludePad.GetStandPosition().y : transform.position.y;
            var canHitPads = false;

            var spacing = leafSpawner != null ? leafSpawner.VerticalSpacing : 2.2f;
            if (spacing < 0.01f) spacing = 2.2f;

            var catchRadius = fallCatchRadius;
            if (leafSpawner != null)
                catchRadius = Mathf.Max(fallCatchRadius, leafSpawner.HorizontalOffset * 0.65f);

            while (transform.position.y > -10f)
            {
                var previousY = transform.position.y;
                var pos = transform.position;

                velocity.y -= fallGravity * Time.deltaTime;
                pos += velocity * Time.deltaTime;

                pos.x = Mathf.MoveTowards(pos.x, laneX, fallLanePull * Time.deltaTime);
                transform.position = pos;
                transform.Rotate(0f, 0f, 400f * Time.deltaTime);

                if (!canHitPads && transform.position.y < leaveY - 0.25f)
                    canHitPads = true;

                leafSpawner?.CullBelowPlayerY(transform.position.y);

                var liveHeight = Mathf.Clamp(
                    Mathf.FloorToInt((transform.position.y - standOffsetY) / spacing + 0.001f),
                    0,
                    fallFromHeight);
                GameManager.Instance?.SetScoreFromHeight(liveHeight);

                if (canHitPads && leafSpawner != null)
                {
                    var hit = leafSpawner.FindFallContact(
                        transform.position.x,
                        previousY,
                        transform.position.y,
                        catchRadius,
                        excludePad);

                    if (hit != null)
                    {
                        if (hit.IsSolid)
                        {
                            yield return RecoverOntoSolid(hit);
                            yield break;
                        }

                        hit.Break();
                        velocity.y = Mathf.Min(velocity.y, -2f);
                        excludePad = hit;
                    }
                }

                yield return null;
            }

            isBusy = false;
            movementRoutine = null;
            GameManager.Instance?.TriggerGameOver();
        }

        IEnumerator RecoverOntoSolid(Leaf save)
        {
            var land = CrispVisuals.SnapWorldPosition(GetStandPos(save));
            transform.position = land;
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(restScale.x * 1.25f, restScale.y * 0.75f, restScale.z);

            leafSpawner.RecoverTo(save);
            GameManager.Instance?.RecoverFromFall(save.HeightIndex);
            transform.position = CrispVisuals.SnapWorldPosition(GetStandPos(leafSpawner.CurrentLeaf));

            yield return null;
            transform.localScale = restScale;

            isBusy = false;
            movementRoutine = null;
        }

        public void ForceFall()
        {
            if (isBusy) return;

            var current = leafSpawner?.CurrentLeaf;
            // If the pad already finished its 2s timer, don't try to break it again.
            var breakCurrent = current != null && current.CanBreak && !current.IsBroken;
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
