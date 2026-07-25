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
        SpriteRenderer coinRenderer;
        bool hasCoin;
        bool coinCollected;

        /// <summary>Fallback world size when CurrencySettings is missing.</summary>
        const float DefaultCoinWorldSize = 0.55f;
        const float CoinLocalY = 0.72f;

        /// <summary>How long a breakable pad lasts after you land on it.</summary>
        public const float BreakCountdownSeconds = 2f;

        void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public LeafSide Side { get; private set; }
        public int HeightIndex { get; private set; }
        public bool IsBroken { get; private set; }

        /// <summary>Breakable platforms: wrong-side miss, or 2s after landing.</summary>
        public bool CanBreak { get; private set; } = true;

        /// <summary>True while the post-land break timer is running.</summary>
        public bool IsCountingDown => breakCountdownRoutine != null;

        /// <summary>Opposite of CanBreak — safe platforms never break and can catch falls.</summary>
        public bool IsSolid => !CanBreak;

        public float StandOffsetY => standOffsetY;

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
            bool withCoin = false)
        {
            StopAllCoroutines();
            pulseRoutine = null;
            breakCountdownRoutine = null;

            Side = side;
            HeightIndex = heightIndex;
            IsBroken = false;
            CanBreak = canBreak;
            hasCoin = withCoin;
            coinCollected = false;

            var x = (int)side * horizontalOffset;
            // No rotation — angled sprites sample between texels and look soft.
            transform.rotation = Quaternion.identity;
            restPosition = CrispVisuals.SnapWorldPosition(new Vector3(x, yPosition, 0f));
            transform.position = restPosition;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (platform != null)
            {
                var resolved = platform.ResolveSprite(canBreak);
                if (resolved != null)
                    spriteRenderer.sprite = resolved;
                baseColor = Color.white;
                var req = Mathf.Max(0.1f, platform.scale);
                var fit = PlatformRuntime.FitScaleForLaneSprite(spriteRenderer.sprite, req);
                baseScale = Vector3.one * fit;
                standOffsetY = platform.standOffsetY * (fit / req);
            }
            else
            {
                if (spriteRenderer.sprite == null)
                    spriteRenderer.sprite = PixelSpriteFactory.CreateLeafSprite();
                baseColor = Color.white;
                var fit = PlatformRuntime.FitScaleForLaneSprite(spriteRenderer.sprite, 1f);
                baseScale = Vector3.one * fit;
                standOffsetY = 0.35f * fit;
            }

            transform.localScale = baseScale;
            spriteRenderer.color = baseColor;
            spriteRenderer.enabled = true;
            CrispVisuals.MakeSpriteCrisp(spriteRenderer);
            RefreshCoinVisual();
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Start the 2s land timer on breakable pads. Safe to call on solid pads (no-op).
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
                transform.position = restPosition;
                transform.localScale = baseScale;
                if (spriteRenderer != null)
                    spriteRenderer.color = baseColor;
            }
        }

        IEnumerator BreakCountdownRoutine(float seconds)
        {
            var elapsed = 0f;
            seconds = Mathf.Max(0.05f, seconds);

            while (elapsed < seconds)
            {
                if (IsBroken)
                {
                    breakCountdownRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);

                // Shake + warn color ramps up toward the end.
                var shake = 0.02f + t * 0.08f;
                var shakeSpeed = 28f + t * 40f;
                transform.position = restPosition + new Vector3(
                    Mathf.Sin(Time.time * shakeSpeed) * shake,
                    Mathf.Cos(Time.time * shakeSpeed * 1.3f) * shake * 0.5f,
                    0f);

                if (spriteRenderer != null)
                {
                    var warn = Color.Lerp(baseColor, new Color(1f, 0.45f, 0.25f, 1f), t * 0.85f);
                    var flash = (Mathf.Sin(Time.time * (10f + t * 20f)) + 1f) * 0.5f;
                    spriteRenderer.color = Color.Lerp(warn, Color.white, flash * t * 0.35f);
                }

                yield return null;
            }

            breakCountdownRoutine = null;

            if (IsBroken || !CanBreak)
                yield break;

            transform.position = restPosition;
            // Shatter first, then tell the player to fall if still standing here.
            Break();
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

            transform.position = restPosition;

            // Coin falls with the pad — not collected unless you landed on it.
            if (coinRenderer != null)
                coinRenderer.enabled = false;

            StartCoroutine(BreakRoutine());
        }

        IEnumerator BreakRoutine()
        {
            OnBroken?.Invoke(this);
            FeedbackService.BreakPad();

            var duration = 0.35f;
            var elapsed = 0f;
            var startScale = transform.localScale;
            var startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            var startRot = transform.rotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.35f, t);
                if (spriteRenderer != null)
                    spriteRenderer.color = Color.Lerp(startColor, new Color(0.45f, 0.3f, 0.15f, 0f), t);
                transform.rotation = startRot * Quaternion.Euler(0f, 0f, (Side == LeafSide.Left ? -90f : 90f) * t);
                transform.position += Vector3.down * (2.5f * Time.deltaTime);
                yield return null;
            }

            gameObject.SetActive(false);
        }

        public Vector3 GetStandPosition()
        {
            return transform.position + Vector3.up * standOffsetY;
        }

        public Vector3 GetStandPosition(float verticalOffset)
        {
            return transform.position + Vector3.up * verticalOffset;
        }
    }
}
