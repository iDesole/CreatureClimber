using System.Collections.Generic;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Patrols on a horizontal or vertical axis. Overlapping its tight sprite box ends the run.
    /// Horizontal: full playfield edge-to-edge.
    /// Vertical: constant-speed up/down in the gap between the two pads of a diagonal jump.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CombatantHazard : MonoBehaviour
    {
        /// <summary>When texture isn't readable, shrink full mesh bounds by this.</summary>
        public const float FallbackHitboxScale = 0.72f;

        /// <summary>Extra shrink on the combatant hit box after opaque bounds (horizontal).</summary>
        const float HorizontalHitboxScale = 0.85f;

        /// <summary>Vertical flyers felt too wide — much tighter box so you can slip past.</summary>
        const float VerticalHitboxScale = 0.48f;

        /// <summary>Player hit box shrink — less death from transparent padding on the climber.</summary>
        const float PlayerHitboxScale = 0.7f;

        /// <summary>Target world height of a combatant at database Scale = 1.</summary>
        const float ReferenceWorldHeight = 0.95f;

        SpriteRenderer spriteRenderer;
        CombatantMovement movement = CombatantMovement.Horizontal;
        float minBound;
        float maxBound;
        float fixedAxis;
        float speed = 2.4f;
        float dir = 1f;
        float baseScaleAbs = 1f;
        /// <summary>Local-space tight hit rect (from opaque pixels when possible).</summary>
        Rect tightLocalRect;
        /// <summary>Accumulated travel for stable vertical ping-pong (no drift / free-fall look).</summary>
        float patrolT;
        bool active;
        int gapHeight;

        public int GapHeight => gapHeight;
        public bool IsActive => active && gameObject.activeInHierarchy;

        void Awake()
        {
            CacheRenderer();
        }

        public void Setup(
            Combatant definition,
            int abovePadHeight,
            float midY,
            float verticalSpacing,
            float playfieldHalfWidth,
            Sprite fallbackSprite)
        {
            gapHeight = abovePadHeight;
            movement = definition != null ? definition.movement : CombatantMovement.Horizontal;
            speed = definition != null ? Mathf.Max(0.1f, definition.speed) : 2.4f;

            CacheRenderer();

            var sprite = definition != null && definition.sprite != null
                ? definition.sprite
                : fallbackSprite;
            if (sprite == null)
                sprite = fallbackSprite;

            spriteRenderer.enabled = true;
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;
            spriteRenderer.flipY = false;
            spriteRenderer.sortingOrder = 6;
            spriteRenderer.sortingLayerID = 0;
            CrispVisuals.MakeSpriteCrisp(spriteRenderer);

            // Database Scale multiplies linearly: 1 = reference height, 2 = double, 0.5 = half.
            var userScale = definition != null ? Mathf.Max(0.05f, definition.scale) : 1f;
            baseScaleAbs = ComputeCombatantScale(sprite, userScale);
            transform.localScale = new Vector3(baseScaleAbs, baseScaleAbs, 1f);

            tightLocalRect = SpriteTightBounds.GetLocalHitRect(sprite);

            dir = Random.value < 0.5f ? 1f : -1f;

            var spacing = Mathf.Max(0.5f, verticalSpacing);
            var halfW = Mathf.Max(0.5f, playfieldHalfWidth);

            // Full visual half-size for edge clamping (so art doesn't clip off-screen).
            var halfSpriteX = 0.25f;
            var halfSpriteY = 0.25f;
            if (sprite != null)
            {
                halfSpriteX = Mathf.Max(0.05f, sprite.bounds.extents.x * baseScaleAbs);
                halfSpriteY = Mathf.Max(0.05f, sprite.bounds.extents.y * baseScaleAbs);
            }

            patrolT = 0f;

            if (movement == CombatantMovement.Horizontal)
            {
                minBound = -halfW + halfSpriteX;
                maxBound = halfW - halfSpriteX;
                if (maxBound <= minBound)
                {
                    minBound = -halfW;
                    maxBound = halfW;
                }

                fixedAxis = midY;
                // Start at a random side so patterns vary.
                patrolT = dir > 0f ? 0f : 1f;
                var startX = Mathf.Lerp(minBound, maxBound, patrolT);
                transform.position = CrispVisuals.SnapWorldPosition(new Vector3(startX, fixedAxis, 0f));
            }
            else
            {
                // Patrol in the gap between pad (abovePadHeight) and pad+1 — block the diagonal hop.
                var gapHalf = spacing * CombatantDirector.VerticalGapFill * 0.5f;
                var dipExtra = spacing * CombatantDirector.VerticalDipExtra;
                var along = GameSettings.ClimbSign;
                minBound = midY - along * (gapHalf + dipExtra);
                maxBound = midY + along * gapHalf;
                if (minBound > maxBound)
                {
                    var swap = minBound;
                    minBound = maxBound;
                    maxBound = swap;
                }

                // Slight inset so the sprite doesn't bury into the upper pad.
                if (halfSpriteY * 2f < (maxBound - minBound) * 0.9f)
                    maxBound -= halfSpriteY * 0.25f;

                if (maxBound - minBound < 0.2f)
                {
                    minBound = midY - 0.55f;
                    maxBound = midY + 0.35f;
                }

                fixedAxis = 0f; // center line — crosses the diagonal path
                // Start mid-gap or at an end so the first motion is a clear up/down patrol.
                patrolT = Random.value;
                dir = Random.value < 0.5f ? 1f : -1f;
                var startY = Mathf.Lerp(minBound, maxBound, patrolT);
                transform.position = CrispVisuals.SnapWorldPosition(new Vector3(fixedAxis, startY, 0f));
            }

            FaceDirection();

            active = true;
            gameObject.SetActive(true);
            gameObject.name = definition != null
                ? $"Combatant_{definition.DisplayName}_{abovePadHeight}"
                : $"Combatant_{abovePadHeight}";
        }

        /// <summary>
        /// Scale = 1 → about <see cref="ReferenceWorldHeight"/> tall, independent of source resolution.
        /// Database Scale multiplies that (2 = twice as big).
        /// </summary>
        static float ComputeCombatantScale(Sprite sprite, float userScale)
        {
            userScale = Mathf.Max(0.05f, userScale);
            if (sprite == null)
                return userScale;

            var naturalH = sprite.bounds.size.y;
            if (naturalH < 0.0001f)
                naturalH = sprite.bounds.size.x;
            if (naturalH < 0.0001f)
                return userScale;

            return (ReferenceWorldHeight / naturalH) * userScale;
        }

        public void Retire()
        {
            active = false;
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
            gameObject.SetActive(false);
        }

        void CacheRenderer()
        {
            if (spriteRenderer != null)
                return;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        void Update()
        {
            if (!active) return;

            var gm = GameManager.Instance;
            if (gm != null &&
                (gm.State == GameState.Paused ||
                 gm.State == GameState.Ready ||
                 gm.State == GameState.GameOver ||
                 gm.State == GameState.Falling))
                return;

            var span = maxBound - minBound;
            if (span < 0.01f)
                return;

            // Constant-speed ping-pong along the axis (no gravity, no drift).
            var delta = (speed / span) * Time.deltaTime;
            patrolT += dir * delta;

            if (patrolT >= 1f)
            {
                patrolT = 1f;
                dir = -1f;
                if (movement == CombatantMovement.Horizontal)
                    FaceDirection();
            }
            else if (patrolT <= 0f)
            {
                patrolT = 0f;
                dir = 1f;
                if (movement == CombatantMovement.Horizontal)
                    FaceDirection();
            }

            var along = Mathf.Lerp(minBound, maxBound, patrolT);
            Vector3 pos;
            if (movement == CombatantMovement.Horizontal)
                pos = new Vector3(along, fixedAxis, 0f);
            else
                pos = new Vector3(fixedAxis, along, 0f);

            transform.position = CrispVisuals.SnapWorldPosition(pos);

            if (gm != null &&
                (gm.State == GameState.Playing || gm.State == GameState.Falling))
            {
                TryHitPlayer(gm);
            }
        }

        void FaceDirection()
        {
            if (movement != CombatantMovement.Horizontal)
            {
                transform.localScale = new Vector3(baseScaleAbs, baseScaleAbs, 1f);
                return;
            }

            transform.localScale = new Vector3(
                baseScaleAbs * (dir >= 0f ? 1f : -1f),
                baseScaleAbs,
                1f);
        }

        void TryHitPlayer(GameManager gm)
        {
            CacheRenderer();
            if (spriteRenderer == null || !spriteRenderer.enabled || spriteRenderer.sprite == null)
                return;

            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null || !player.gameObject.activeInHierarchy) return;

            var playerSr = player.GetComponent<SpriteRenderer>();
            if (playerSr == null || !playerSr.enabled || playerSr.sprite == null) return;

            GetWorldHitBox(out var cMin, out var cMax);

            // Player: shrink mesh bounds so transparent padding is less lethal.
            var pb = playerSr.bounds;
            var pCenter = pb.center;
            var pExt = pb.extents * PlayerHitboxScale;
            var pMin = pCenter - pExt;
            var pMax = pCenter + pExt;

            if (AabbOverlap(cMin, cMax, pMin, pMax))
                gm.OnCombatantHit();
        }

        /// <summary>
        /// Axis-aligned world box from the tight local sprite rect + current scale/flip/position.
        /// Vertical combatants use a smaller box so diagonal jumps can slip past the edges.
        /// </summary>
        void GetWorldHitBox(out Vector3 min, out Vector3 max)
        {
            var sx = transform.localScale.x;
            var sy = Mathf.Abs(transform.localScale.y);
            var absX = Mathf.Abs(sx);

            var localCenter = tightLocalRect.center;
            var half = new Vector2(tightLocalRect.width * 0.5f, tightLocalRect.height * 0.5f);

            var tight = movement == CombatantMovement.Vertical
                ? VerticalHitboxScale
                : HorizontalHitboxScale;
            half *= tight;

            // Signed X scale flips the local center when facing left.
            var worldCenter = transform.position + new Vector3(
                localCenter.x * sx,
                localCenter.y * sy,
                0f);

            var halfWorld = new Vector3(half.x * absX, half.y * sy, 0f);
            min = worldCenter - halfWorld;
            max = worldCenter + halfWorld;
        }

        static bool AabbOverlap(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
        {
            return aMin.x <= bMax.x && aMax.x >= bMin.x
                   && aMin.y <= bMax.y && aMax.y >= bMin.y;
        }
    }

    /// <summary>
    /// Per-sprite tight local hit rect. Uses opaque pixels when the texture is readable;
    /// otherwise shrinks the full mesh bounds.
    /// </summary>
    static class SpriteTightBounds
    {
        const byte AlphaThreshold = 16;
        static readonly Dictionary<int, Rect> Cache = new();

        public static Rect GetLocalHitRect(Sprite sprite)
        {
            if (sprite == null)
                return new Rect(-0.25f, -0.25f, 0.5f, 0.5f);

            var id = sprite.GetInstanceID();
            if (Cache.TryGetValue(id, out var cached))
                return cached;

            var rect = Compute(sprite);
            Cache[id] = rect;
            return rect;
        }

        static Rect Compute(Sprite sprite)
        {
            if (TryOpaqueLocalRect(sprite, out var tight))
                return tight;

            // Fallback: shrink full sprite mesh (transparent padding often inflates this).
            var b = sprite.bounds;
            var size = b.size * CombatantHazard.FallbackHitboxScale;
            var c = b.center;
            return new Rect(c.x - size.x * 0.5f, c.y - size.y * 0.5f, size.x, size.y);
        }

        static bool TryOpaqueLocalRect(Sprite sprite, out Rect localRect)
        {
            localRect = default;
            var tex = sprite.texture;
            if (tex == null || !tex.isReadable)
                return false;

            var tr = sprite.textureRect;
            var x0 = Mathf.FloorToInt(tr.x);
            var y0 = Mathf.FloorToInt(tr.y);
            var w = Mathf.Max(1, Mathf.FloorToInt(tr.width));
            var h = Mathf.Max(1, Mathf.FloorToInt(tr.height));

            Color32[] pixels;
            try
            {
                pixels = tex.GetPixels32();
            }
            catch
            {
                return false;
            }

            var texW = tex.width;
            var minX = w;
            var minY = h;
            var maxX = -1;
            var maxY = -1;

            for (var y = 0; y < h; y++)
            {
                var row = (y0 + y) * texW + x0;
                for (var x = 0; x < w; x++)
                {
                    if (pixels[row + x].a < AlphaThreshold)
                        continue;

                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
                return false;

            var ppu = sprite.pixelsPerUnit;
            if (ppu < 0.01f) ppu = 100f;

            var pivot = sprite.pivot; // pixels from bottom-left of the sprite rect
            var localMinX = (minX - pivot.x) / ppu;
            var localMinY = (minY - pivot.y) / ppu;
            var localMaxX = (maxX + 1 - pivot.x) / ppu;
            var localMaxY = (maxY + 1 - pivot.y) / ppu;

            localRect = Rect.MinMaxRect(localMinX, localMinY, localMaxX, localMaxY);
            return localRect.width > 0.001f && localRect.height > 0.001f;
        }
    }
}
