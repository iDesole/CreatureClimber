using System;
using System.Collections;
using UnityEngine;

namespace CreatureClimb
{
    public class Leaf : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteRenderer;

        Color baseColor = Color.white;
        Vector3 baseScale = Vector3.one;
        Vector3 restPosition;
        Coroutine pulseRoutine;
        Coroutine breakCountdownRoutine;
        float standOffsetY = 0.35f;
        float spawnOffsetY;
        SpriteRenderer coinRenderer;
        bool hasCoin;
        bool coinCollected;
        Platform platformDef;

        /// <summary>Fallback world size when CurrencySettings is missing.</summary>
        const float DefaultCoinWorldSize = 0.55f;
        const float CoinLocalY = 0.72f;

        /// <summary>How long a breakable pad lasts after you land on it.</summary>
        public const float BreakCountdownSeconds = 0.75f;

        void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public LeafSide Side { get; private set; }
        public int HeightIndex { get; private set; }
        public bool IsBroken { get; private set; }

        /// <summary>Breakable platforms: wrong-side miss, or 0.75s after landing.</summary>
        public bool CanBreak { get; private set; } = true;

        /// <summary>True while the post-land break timer is running.</summary>
        public bool IsCountingDown => breakCountdownRoutine != null;

        /// <summary>Opposite of CanBreak — safe platforms never break and can catch falls.</summary>
        public bool IsSolid => !CanBreak;

        /// <summary>Race finish pad — centered; either tap side is valid.</summary>
        public bool IsFinishPad { get; private set; }

        public bool IsCenter => Side == LeafSide.Center || IsFinishPad;

        public float StandOffsetY => standOffsetY;

        /// <summary>World Y of the pad art (grid Y plus spawn-offset nudge).</summary>
        public float VisualWorldY => VisualRestPosition.y;

        /// <summary>True if this pad still has a pickup coin on it.</summary>
        public bool HasCoin => hasCoin && !coinCollected && !IsBroken;

        public event Action<Leaf> OnBroken;

        /// <summary>Fired when the land countdown expires (just before the pad shatters).</summary>
        public event Action<Leaf> OnBreakCountdownExpired;

        public void Initialize(
            LeafSide side,
            int heightIndex,
            float horizontalOffset,
            float yPosition,
            Platform platform = null,
            bool canBreak = true,
            bool withCoin = false,
            bool isFinishPad = false)
        {
            // Always hard-reset pooled state (break VFX used to leave Z rotation at ±90°).
            ResetPooledState();

            Side = isFinishPad ? LeafSide.Center : side;
            HeightIndex = heightIndex;
            IsBroken = false;
            CanBreak = isFinishPad ? false : canBreak;
            IsFinishPad = isFinishPad;
            hasCoin = isFinishPad ? false : withCoin;
            coinCollected = false;
            platformDef = platform;

            var x = (int)Side * horizontalOffset;
            restPosition = CrispVisuals.SnapWorldPosition(new Vector3(x, yPosition, 0f));

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            ApplyPlatformVisuals(canBreak);
            PlaceAtRest();

            transform.localScale = baseScale;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
                spriteRenderer.flipY = false;
                CrispVisuals.MakeSpriteCrisp(spriteRenderer);
            }

            RefreshCoinVisual();
            ApplyVisibility();
            gameObject.SetActive(true);
        }

        /// <summary>Keep this pad on its lane after a resolution / aspect change.</summary>
        public void RelayoutX(float horizontalOffset)
        {
            var x = (int)Side * horizontalOffset;
            restPosition.x = x;
            restPosition = CrispVisuals.SnapWorldPosition(restPosition);
            PlaceAtRest();
        }

        /// <summary>Return to pool-safe defaults (no leftover break spin / scale / color).</summary>
        public void ResetPooledState()
        {
            StopAllCoroutines();
            pulseRoutine = null;
            breakCountdownRoutine = null;
            IsBroken = false;
            spawnOffsetY = 0f;

            transform.localRotation = Quaternion.identity;
            transform.rotation = Quaternion.identity;
            if (spriteRenderer != null)
            {
                spriteRenderer.flipY = false;
                spriteRenderer.color = Color.white;
            }
        }

        void ApplyPlatformVisuals(bool breakableLook)
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (platformDef != null)
            {
                var resolved = platformDef.ResolveSprite(breakableLook);
                if (resolved != null)
                    spriteRenderer.sprite = resolved;
                baseColor = Color.white;
                var req = Mathf.Max(0.1f, platformDef.scale);
                var fit = PlatformRuntime.FitScaleForLaneSprite(spriteRenderer.sprite, req);
                baseScale = Vector3.one * fit;
                standOffsetY = platformDef.standOffsetY * (fit / req);
                spawnOffsetY = platformDef.spawnOffsetY;
            }
            else
            {
                if (spriteRenderer.sprite == null)
                    spriteRenderer.sprite = PixelSpriteFactory.CreateLeafSprite();
                baseColor = Color.white;
                var fit = PlatformRuntime.FitScaleForLaneSprite(spriteRenderer.sprite, 1f);
                baseScale = Vector3.one * fit;
                standOffsetY = 0.35f * fit;
                spawnOffsetY = 0f;
            }

            transform.localScale = baseScale;
            if (spriteRenderer != null)
            {
                // Right-lane pads flip so stems/branches face inward.
                spriteRenderer.flipX = Side == LeafSide.Right;
                spriteRenderer.flipY = false;
                spriteRenderer.color = baseColor;
                CrispVisuals.MakeSpriteCrisp(spriteRenderer);
            }

            ApplyVisibility();
        }

        /// <summary>Hide pad art when Platform Visibility is off. Coins stay visible.</summary>
        public void ApplyVisibility()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) return;
            spriteRenderer.enabled = GameSettings.PlatformsVisible && !IsBroken;
        }

        /// <summary>
        /// Solid pad becomes breakable after a clutch land (sprite + rules flip).
        /// </summary>
        public void ConvertToBreakable()
        {
            if (IsBroken || CanBreak) return;

            CanBreak = true;
            ApplyPlatformVisuals(breakableLook: true);
        }

        /// <summary>
        /// Start the land timer on breakable pads. Safe to call on solid pads (no-op).
        /// </summary>
        public void StartBreakCountdown(float seconds = BreakCountdownSeconds)
        {
            if (!CanBreak || IsBroken || !gameObject.activeInHierarchy)
                return;

            if (breakCountdownRoutine != null)
                StopCoroutine(breakCountdownRoutine);

            breakCountdownRoutine = StartCoroutine(BreakCountdownRoutine(seconds));
        }

        public void CancelBreakCountdown()
        {
            if (breakCountdownRoutine != null)
            {
                StopCoroutine(breakCountdownRoutine);
                breakCountdownRoutine = null;
            }

            if (!IsBroken)
            {
                PlaceAtRest();
                transform.localScale = baseScale;
                if (spriteRenderer != null)
                    spriteRenderer.color = baseColor;
            }
        }

        IEnumerator BreakCountdownRoutine(float seconds)
        {
            var elapsed = 0f;
            seconds = Mathf.Max(0.05f, seconds);

            // Integrate phase so amplitude/freq changes never restart the wave.
            // (Sin(Time * varyingFreq) was the "start / stop / start" pop.)
            var phaseX = 0f;
            var phaseY = 0f;

            while (elapsed < seconds)
            {
                if (IsBroken)
                {
                    breakCountdownRoutine = null;
                    yield break;
                }

                var dt = Time.deltaTime;
                elapsed += dt;
                var t = Mathf.Clamp01(elapsed / seconds);
                var remaining = Mathf.Max(0f, seconds - elapsed);

                // Red tint: slow continuous burn over the whole fuse (main tell).
                var redT = t * t * (3f - 2f * t); // smoothstep

                // Shake: continuous intensity — almost nothing → tiny mid → last 0.5s panic.
                // Mid is intentionally soft (max ~0.08) so it never feels aggressive early.
                const float climaxWindow = 0.5f;
                var preLen = Mathf.Max(0.05f, seconds - climaxWindow);
                var p = Mathf.Clamp01(elapsed / preLen);
                var preIntensity = p * p * p * 0.08f;

                var c = remaining < climaxWindow
                    ? 1f - remaining / climaxWindow
                    : 0f;
                var climaxBlend = c * c * (3f - 2f * c); // smoothstep, zero start slope
                var intensity = preIntensity + climaxBlend * (1f - preIntensity);

                var amp = intensity * 0.1f;
                var freq = 20f + intensity * 18f;
                phaseX += freq * dt;
                phaseY += freq * 1.13f * dt;

                PlaceAtRest(new Vector3(
                    Mathf.Sin(phaseX) * amp,
                    Mathf.Cos(phaseY) * amp * 0.42f,
                    0f));

                if (spriteRenderer != null)
                {
                    var danger = new Color(0.92f, 0.3f, 0.22f, 1f);
                    var col = Color.Lerp(baseColor, danger, redT * 0.9f);
                    // Flash only deep in the climax — not mid-fuse.
                    if (intensity > 0.4f)
                    {
                        var flashAmt = (intensity - 0.4f) / 0.6f;
                        var flash = (Mathf.Sin(phaseX * 2.2f) + 1f) * 0.5f;
                        col = Color.Lerp(col, Color.white, flash * flashAmt * 0.3f);
                    }

                    spriteRenderer.color = col;
                }

                yield return null;
            }

            breakCountdownRoutine = null;

            if (IsBroken || !CanBreak)
                yield break;

            // Reset shake pose; GameManager decides whether to actually shatter
            // (only when the player is standing on this pad — never mid-air).
            PlaceAtRest();
            transform.localScale = baseScale;
            if (spriteRenderer != null)
                spriteRenderer.color = baseColor;

            OnBreakCountdownExpired?.Invoke(this);
        }

        void EnsureCoinRenderer()
        {
            if (coinRenderer != null) return;

            var existing = transform.Find("Coin");
            if (existing != null)
            {
                coinRenderer = existing.GetComponent<SpriteRenderer>();
                if (coinRenderer != null) return;
            }

            var go = new GameObject("Coin");
            go.transform.SetParent(transform, false);
            coinRenderer = go.AddComponent<SpriteRenderer>();
            coinRenderer.sprite = PixelSpriteFactory.CreateCoinSprite();
            coinRenderer.sortingOrder = 4;
            coinRenderer.color = Color.white;
            CrispVisuals.MakeSpriteCrisp(coinRenderer);
        }

        void RefreshCoinVisual()
        {
            EnsureCoinRenderer();
            if (coinRenderer == null) return;

            var show = hasCoin && !coinCollected && !IsBroken;
            coinRenderer.enabled = show;
            if (!show) return;

            if (coinRenderer.sprite == null)
                coinRenderer.sprite = PixelSpriteFactory.CreateCoinSprite();

            // Counter parent scale + fit source art bounds so coin stays CurrencySettings size.
            var sx = Mathf.Max(0.01f, baseScale.x);
            var sy = Mathf.Max(0.01f, baseScale.y);
            var natural = 1f;
            if (coinRenderer.sprite != null)
            {
                var b = coinRenderer.sprite.bounds.size;
                natural = Mathf.Max(b.x, b.y);
                if (natural < 0.001f) natural = 1f;
            }

            var settings = CurrencySettings.Load();
            var worldSize = settings != null ? settings.WorldSize : DefaultCoinWorldSize;
            var fit = worldSize / natural;
            coinRenderer.transform.localScale = new Vector3(fit / sx, fit / sy, 1f);
            coinRenderer.transform.localPosition = new Vector3(0f, CoinLocalY / sy, 0f);
            coinRenderer.transform.localRotation = Quaternion.identity;
            coinRenderer.color = Color.white;
            CrispVisuals.MakeSpriteCrisp(coinRenderer);
        }

        /// <summary>Pickup coin if present. Returns true when a coin was collected.</summary>
        public bool TryCollectCoin()
        {
            if (!hasCoin || coinCollected || IsBroken) return false;

            coinCollected = true;
            hasCoin = false;
            if (coinRenderer != null)
                coinRenderer.enabled = false;

            SaveService.AddCoins(1);
            return true;
        }

        public void SetHighlight(bool highlighted)
        {
            if (spriteRenderer == null) return;

            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
                pulseRoutine = null;
            }

            if (highlighted)
            {
                pulseRoutine = StartCoroutine(PulseHighlight());
            }
            else
            {
                spriteRenderer.color = baseColor;
                transform.localScale = baseScale;
            }

            ApplyVisibility();
        }

        IEnumerator PulseHighlight()
        {
            while (true)
            {
                var t = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                // Color-only pulse — scaling would soft-sample the sprite every frame.
                spriteRenderer.color = Color.Lerp(baseColor, Color.white, 0.25f + t * 0.35f);
                transform.localScale = baseScale;
                yield return null;
            }
        }

        public void Break()
        {
            if (IsBroken || !CanBreak) return;
            Shatter(washedByWave: false);
        }

        /// <summary>
        /// Tempo wave (or similar) destroys any pad — solid or breakable.
        /// </summary>
        public void WashAway()
        {
            if (IsBroken) return;
            Shatter(washedByWave: true);
        }

        void Shatter(bool washedByWave)
        {
            if (IsBroken) return;
            IsBroken = true;

            if (breakCountdownRoutine != null)
            {
                StopCoroutine(breakCountdownRoutine);
                breakCountdownRoutine = null;
            }

            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
                pulseRoutine = null;
            }

            // Never spin the root transform — pooled leaves reused mid-animation used to
            // spawn permanently rotated 90°. VFX is scale + fade + sink only.
            transform.rotation = Quaternion.identity;
            PlaceAtRest();

            if (spriteRenderer != null)
                spriteRenderer.enabled = true;

            if (coinRenderer != null)
                coinRenderer.enabled = false;

            StartCoroutine(BreakRoutine(washedByWave));
        }

        IEnumerator BreakRoutine(bool washedByWave)
        {
            OnBroken?.Invoke(this);
            if (!washedByWave)
                FeedbackService.BreakPad();

            var duration = washedByWave ? 0.42f : 0.35f;
            var elapsed = 0f;
            var startScale = baseScale.sqrMagnitude > 0.0001f ? baseScale : transform.localScale;
            var startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            var startPos = VisualRestPosition;
            // Wave wash: cool blue fade + pull downward into the ocean.
            var endColor = washedByWave
                ? new Color(0.2f, 0.45f, 0.7f, 0f)
                : new Color(0.45f, 0.3f, 0.15f, 0f);
            var sink = washedByWave ? 5.5f : 2.5f;
            var endScaleMul = washedByWave ? 0.2f : 0.35f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.rotation = Quaternion.identity;
                transform.localScale = Vector3.Lerp(startScale, startScale * endScaleMul, t);
                if (spriteRenderer != null)
                    spriteRenderer.color = Color.Lerp(startColor, endColor, t);
                transform.position = startPos + Vector3.down * (sink * duration * t);
                yield return null;
            }

            transform.rotation = Quaternion.identity;
            transform.localScale = baseScale.sqrMagnitude > 0.0001f ? baseScale : Vector3.one;
            gameObject.SetActive(false);
        }

        public Vector3 GetStandPosition()
        {
            return restPosition + GameSettings.StandOffset(standOffsetY);
        }

        public Vector3 GetStandPosition(float verticalOffset)
        {
            return restPosition + GameSettings.StandOffset(verticalOffset);
        }

        Vector3 VisualRestPosition =>
            restPosition + GameSettings.StandOffset(spawnOffsetY);

        void PlaceAtRest(Vector3 extra = default)
        {
            transform.position = CrispVisuals.SnapWorldPosition(VisualRestPosition + extra);
        }
    }
}
