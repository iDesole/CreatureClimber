using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Blackout mode VFX — brief blackout only (no white flash).
    /// Photosensitive-safe: no bright flashes, smooth fade in/out, spaced several seconds apart.
    /// </summary>
    public class StormEffects : MonoBehaviour
    {
        public static StormEffects Instance { get; private set; }

        const float BaseLightningInterval = 5f;
        const float BlackoutHoldSeconds = 0.15f;
        const float DropToDarkSeconds = 0.18f;
        const float RecoverSeconds = 0.20f;
        const int PlatformsPerSpeedStep = 20;
        const float SpeedStep = 0.02f;

        // Fully opaque black — no see-through during blackout.
        static readonly Color SoftDark = new Color(0f, 0f, 0f, 1f);
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        Image veil;
        Canvas veilCanvas;
        Coroutine lightningRoutine;
        bool active;
        int climbHeight;
        float nextLightningAt;
        bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("--- Storm Effects ---");
            DontDestroyOnLoad(go);
            go.AddComponent<StormEffects>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            try
            {
                BuildVeil();
                built = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[StormEffects] VFX setup failed (Storm still playable): " + e.Message);
                built = false;
            }

            SetRunning(false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetRunning(bool running)
        {
            active = running && built;
            climbHeight = 0;

            if (veil != null)
            {
                veil.color = Clear;
                veil.raycastTarget = false;
            }

            if (veilCanvas != null)
            {
                // Keep HUD (pause) above blackout if an older build used a higher order.
                if (veilCanvas.sortingOrder >= 100)
                    veilCanvas.sortingOrder = 50;
                veilCanvas.gameObject.SetActive(false);
            }

            if (lightningRoutine != null)
            {
                StopCoroutine(lightningRoutine);
                lightningRoutine = null;
            }

            if (running && built)
            {
                // First event after a calm start — never flash immediately on Play.
                nextLightningAt = Time.time + Mathf.Max(2.5f, CurrentInterval() * 0.5f);
                lightningRoutine = StartCoroutine(LightningLoop());
            }
        }

        public void SetClimbHeight(int heightIndex)
        {
            climbHeight = Mathf.Max(0, heightIndex);
        }

        public float CurrentInterval()
        {
            var steps = climbHeight / PlatformsPerSpeedStep;
            // Cap speed-up so strikes never get rapid enough to approach 3 Hz.
            var speed = Mathf.Min(1.5f, Mathf.Pow(1f + SpeedStep, steps));
            return Mathf.Max(2.5f, BaseLightningInterval / speed);
        }

        IEnumerator LightningLoop()
        {
            while (active)
            {
                while (active && Time.time < nextLightningAt)
                    yield return null;

                if (!active) yield break;

                var gm = GameManager.Instance;
                if (gm != null && gm.State == GameState.Paused)
                {
                    nextLightningAt = Time.time + CurrentInterval();
                    continue;
                }

                if (gm != null &&
                    (gm.State == GameState.Playing || gm.State == GameState.Falling))
                {
                    yield return Strike();
                }

                // Extra safety: never schedule the next strike sooner than 2.5s after this one ends.
                nextLightningAt = Mathf.Max(Time.time + 2.5f, Time.time + CurrentInterval());
            }

            lightningRoutine = null;
        }

        /// <summary>
        /// Fade to black → brief hold (0.15s) → fade clear. No white flash at all.
        /// </summary>
        IEnumerator Strike()
        {
            if (veil == null || veilCanvas == null) yield break;

            veilCanvas.gameObject.SetActive(true);
            veil.color = Clear;

            try
            {
                FeedbackService.StormLightning();
                CameraFollow.Shake(0.02f, 0.05f);
            }
            catch
            {
                // never block strike
            }

            // Blackout only — fade in, hold, fade out.
            yield return Lerp(Clear, SoftDark, DropToDarkSeconds);
            yield return Hold(SoftDark, BlackoutHoldSeconds);
            yield return Lerp(SoftDark, Clear, RecoverSeconds);

            veil.color = Clear;
            if (!active && veilCanvas != null)
                veilCanvas.gameObject.SetActive(false);
        }

        IEnumerator Hold(Color color, float seconds)
        {
            veil.color = color;
            yield return WaitGameplay(seconds);
        }

        IEnumerator Lerp(Color from, Color to, float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            var t = 0f;
            while (t < duration)
            {
                if (!active) yield break;
                if (IsPaused())
                {
                    yield return null;
                    continue;
                }

                t += Time.unscaledDeltaTime;
                // Smooth ease — no hard cuts.
                var u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                veil.color = Color.Lerp(from, to, u);
                yield return null;
            }

            veil.color = to;
        }

        IEnumerator WaitGameplay(float seconds)
        {
            var left = seconds;
            while (left > 0f)
            {
                if (!active) yield break;
                if (IsPaused())
                {
                    yield return null;
                    continue;
                }

                left -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        static bool IsPaused() =>
            GameManager.Instance != null && GameManager.Instance.State == GameState.Paused;

        void BuildVeil()
        {
            var canvasGo = new GameObject("StormVeilCanvas");
            canvasGo.transform.SetParent(transform, false);
            veilCanvas = canvasGo.AddComponent<Canvas>();
            veilCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below main HUD (sortingOrder 100) so pause button / pause panel stay visible & tappable.
            veilCanvas.sortingOrder = 50;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            // No GraphicRaycaster — never eat climb taps or pause presses during blackout.

            var veilGo = new GameObject("Veil", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            veilGo.transform.SetParent(canvasGo.transform, false);
            var rect = veilGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            veil = veilGo.GetComponent<Image>();
            // Solid 1×1 sprite required — null sprite can look semi-transparent / not fill.
            veil.sprite = CreateSolidSprite();
            veil.type = Image.Type.Simple;
            veil.preserveAspect = false;
            veil.color = Clear;
            veil.raycastTarget = false;

            canvasGo.SetActive(false);
        }

        static Sprite CreateSolidSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
