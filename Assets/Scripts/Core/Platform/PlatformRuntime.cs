using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Mobile-first defaults for iOS and Google Play.
    /// Screen: modern 19.5:9 (1080×2340). Playfield/UI: original 1080×1920.
    /// </summary>
    public static class PlatformRuntime
    {
        public enum HostKind
        {
            Editor,
            Mobile,
            Desktop
        }

        public enum StoreTarget
        {
            Unknown,
            GooglePlay,
            AppStore,
            Other
        }

        static bool initialized;

        public static HostKind Host { get; private set; }
        public static StoreTarget Store { get; private set; }
        public static bool IsMobile => Host == HostKind.Mobile;
        public static bool IsDesktop => Host == HostKind.Desktop || Host == HostKind.Editor;
        public static bool SupportsTouch { get; private set; }
        public static bool SupportsGamepad { get; private set; }
        public static bool SupportsKeyboard { get; private set; }
        public static bool SupportsHaptics { get; private set; }
        public static bool PreferPortrait { get; private set; }

        public static string ConfirmHint { get; private set; } = "Tap to start";
        public static string LeftRightHint { get; private set; } = "Tap left / right";
        public static string CycleHint { get; private set; } = "← → character   ↑ ↓ platform";

        /// <summary>Full display — modern tall phone (19.5:9).</summary>
        public const float ScreenWidth = 1080f;
        public const float ScreenHeight = 2340f;

        /// <summary>Original content / UI safe frame (16:9 portrait).</summary>
        public const float DesignWidth = 1080f;
        public const float DesignHeight = 1920f;

        /// <summary>
        /// Pixel-perfect ortho: 1 sprite pixel → <see cref="CrispVisuals.PixelZoom"/> screen pixels
        /// on the 1080×2340 frame (keeps art sharp).
        /// </summary>
        public static float PhoneOrthoSize => CrispVisuals.PixelPerfectOrthoSize(ScreenHeight);

        /// <summary>Extra shrink after lane-fit so pads/player sit a bit smaller.</summary>
        public const float PlatformSizeMul = 0.82f;
        public const float CreatureSizeMul = 0.78f;

        const float EdgePadding = 0.1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            Initialize();
        }

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            DetectHost();
            DetectCapabilities();
            ApplyPerformanceDefaults();
            ApplyOrientation();
            BuildInputHints();
        }

        static void DetectHost()
        {
            PreferPortrait = true;
#if UNITY_EDITOR
            Host = HostKind.Editor;
            Store = StoreTarget.Unknown;
#elif UNITY_ANDROID
            Host = HostKind.Mobile;
            Store = StoreTarget.GooglePlay;
#elif UNITY_IOS
            Host = HostKind.Mobile;
            Store = StoreTarget.AppStore;
#else
            Host = HostKind.Desktop;
            Store = StoreTarget.Other;
#endif
        }

        static void DetectCapabilities()
        {
            SupportsTouch = Input.touchSupported || IsMobile;
            SupportsKeyboard = !IsMobile || Application.isEditor;
            SupportsGamepad = true;
            SupportsHaptics = IsMobile
#if UNITY_IOS || UNITY_ANDROID
                              || SystemInfo.supportsVibration
#endif
                ;
        }

        static void ApplyPerformanceDefaults()
        {
            Application.targetFrameRate = IsMobile ? 60 : -1;
            QualitySettings.vSyncCount = IsDesktop ? 1 : 0;
            Application.runInBackground = IsDesktop;

            // Always off — MSAA softens pixel art and UI text.
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            if (IsMobile)
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            else
                Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        static void ApplyOrientation()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;

            // Standalone / editor: lock to modern tall phone resolution.
            if (!IsMobile)
            {
                var w = Mathf.RoundToInt(ScreenWidth);
                var h = Mathf.RoundToInt(ScreenHeight);
                if (Screen.width != w || Screen.height != h)
                    Screen.SetResolution(w, h, FullScreenMode.Windowed);
            }
        }

        static void BuildInputHints()
        {
            if (IsMobile && SupportsTouch)
            {
                ConfirmHint = "Tap to start";
                LeftRightHint = "Tap left / right side";
                CycleHint = "Customize · roster";
            }
            else if (SupportsGamepad && !SupportsKeyboard)
            {
                ConfirmHint = "A to start";
                LeftRightHint = "D-Pad / stick L-R";
                CycleHint = "Customize · roster";
            }
            else
            {
                ConfirmHint = "Space / click to start";
                LeftRightHint = "← → · A/D · click side";
                CycleHint = "Customize · roster";
            }
        }

        /// <summary>UI reference stays on the original 1080×1920 content frame.</summary>
        public static Vector2 UiReferenceResolution => new Vector2(DesignWidth, DesignHeight);

        public static float ScreenAspect => ScreenWidth / ScreenHeight;
        public static float DesignAspect => DesignWidth / DesignHeight;

        public static float RecommendedOrthoSize(Camera cam) => PhoneOrthoSize;

        /// <summary>
        /// World half-width of the playfield (original horizontal framing).
        /// Uses design aspect so lanes match the 1080×1920 borders, not the taller letterbox.
        /// </summary>
        public static float WorldHalfWidth(float orthoSize = -1f)
        {
            if (orthoSize < 0f) orthoSize = PhoneOrthoSize;
            // Same horizontal span as when ortho was 5.6 on 1080×1920.
            return (orthoSize * DesignHeight / ScreenHeight) * DesignAspect;
        }

        /// <summary>Full camera view half-width (taller 19.5:9 frame — background uses this).</summary>
        public static float CameraHalfWidth(float orthoSize = -1f)
        {
            if (orthoSize < 0f) orthoSize = PhoneOrthoSize;
            return orthoSize * ScreenAspect;
        }

        /// <summary>
        /// X offset for left/right lanes — center of each half of the original playfield.
        /// </summary>
        public static float LaneOffset(float orthoSize = -1f) =>
            WorldHalfWidth(orthoSize) * 0.5f;

        /// <summary>
        /// Scale so a sprite centered on a lane stays inside the original side borders,
        /// then apply a slight overall shrink. Database Scale multiplies after that fit
        /// so 0.5 / 2 always half/double the fitted size (even for large source art).
        /// </summary>
        public static float FitScaleForLaneSprite(Sprite sprite, float requestedScale, float orthoSize = -1f)
        {
            if (orthoSize < 0f) orthoSize = PhoneOrthoSize;
            var userScale = Mathf.Max(0.05f, requestedScale);
            if (sprite == null)
                return CrispVisuals.SnapSpriteScale(userScale * PlatformSizeMul);

            var halfW = WorldHalfWidth(orthoSize);
            var lane = LaneOffset(orthoSize);
            var maxOuter = halfW * (1f - EdgePadding);
            var maxHalfSprite = Mathf.Max(0.12f, maxOuter - lane);

            // Fit at Scale=1 first, then apply the database scalar.
            var baseScale = PlatformSizeMul;
            var naturalHalf = sprite.bounds.size.x * 0.5f * baseScale;
            if (naturalHalf > maxHalfSprite)
                baseScale *= maxHalfSprite / naturalHalf;

            return CrispVisuals.SnapSpriteScale(baseScale * userScale);
        }

        /// <summary>
        /// Fit creature into the playfield at Scale=1, then multiply by database Scale.
        /// Half / double always work — including high-res art that would otherwise
        /// always clamp to the same max size.
        /// </summary>
        public static float FitScaleForCreature(Sprite sprite, float requestedScale, float orthoSize = -1f)
        {
            if (orthoSize < 0f) orthoSize = PhoneOrthoSize;
            var userScale = Mathf.Max(0.05f, requestedScale);
            if (sprite == null)
                return CrispVisuals.SnapSpriteScale(userScale * CreatureSizeMul);

            var halfW = WorldHalfWidth(orthoSize);
            var lane = LaneOffset(orthoSize);
            var maxOuter = halfW * (1f - EdgePadding);
            var maxHalfSprite = Mathf.Max(0.12f, maxOuter - lane);
            var maxWorld = halfW * 0.48f;

            // Fit at Scale=1 first, then apply the database scalar.
            var baseScale = CreatureSizeMul;
            var naturalHalf = sprite.bounds.size.x * 0.5f * baseScale;
            if (naturalHalf > maxHalfSprite)
                baseScale *= maxHalfSprite / naturalHalf;

            var dim = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) * baseScale;
            if (dim > maxWorld)
                baseScale *= maxWorld / dim;

            return CrispVisuals.SnapSpriteScale(baseScale * userScale);
        }

        /// <summary>
        /// Normalized anchors for the original 1080×1920 content box, centered on a taller screen.
        /// Intersected with the device safe area (notches).
        /// </summary>
        public static void GetPlayfieldNormalizedRect(out Vector2 anchorMin, out Vector2 anchorMax)
        {
            var sw = Mathf.Max(1, Screen.width);
            var sh = Mathf.Max(1, Screen.height);

            // Ideal playfield height if width maps 1:1 to design width ratio.
            var playfieldH = sw * (DesignHeight / DesignWidth);
            if (playfieldH > sh)
                playfieldH = sh;

            var y0 = (sh - playfieldH) * 0.5f;
            var y1 = y0 + playfieldH;

            // Device safe area (notch / home indicator).
            var safe = Screen.safeArea;
            var minX = safe.xMin;
            var maxX = safe.xMax;
            var minY = Mathf.Max(safe.yMin, y0);
            var maxY = Mathf.Min(safe.yMax, y1);

            // If safe area + playfield conflict badly, prefer intersection of both.
            if (minY >= maxY)
            {
                minY = safe.yMin;
                maxY = safe.yMax;
            }

            anchorMin = new Vector2(minX / sw, minY / sh);
            anchorMax = new Vector2(maxX / sw, maxY / sh);
        }
    }
}
