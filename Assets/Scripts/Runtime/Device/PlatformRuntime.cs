using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Mobile-first defaults for iOS and Google Play.
    /// Common design frame is 20:9 (1080×2400). Camera, UI, and lanes follow the real screen.
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

        /// <summary>Common phone width (1080p portrait).</summary>
        public const float DesignWidth = 1080f;

        /// <summary>Common phone height — 20:9 (1080×2400).</summary>
        public const float DesignHeight = 2400f;

        /// <summary>Fallback / editor preview size — same as the 20:9 design frame.</summary>
        public const float ScreenWidth = DesignWidth;
        public const float ScreenHeight = DesignHeight;

        /// <summary>
        /// World half-width kept constant across aspects (match-width).
        /// Derived from the 20:9 frame at pixel-perfect zoom.
        /// </summary>
        public static float ReferenceWorldHalfWidth =>
            DesignWidth / (2f * CrispVisuals.SpritePPU * CrispVisuals.PixelZoom);

        /// <summary>
        /// Ortho half-height on the common 20:9 frame.
        /// Taller or shorter screens change this so lane width stays put.
        /// </summary>
        public static float PhoneOrthoSize => RecommendedOrthoSize();

        /// <summary>Extra shrink after lane-fit so pads/player sit a bit smaller.</summary>
        public const float PlatformSizeMul = 0.82f;
        public const float CreatureSizeMul = 0.78f;

        /// <summary>CanvasScaler match: 0 = width (extra height on phones taller than 20:9).</summary>
        public const float UiMatchWidthOrHeight = 0f;

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
            // 60fps is the Play Store / modern Android target for casual 2D.
            // Re-apply on resume — some OEMs reset targetFrameRate after focus loss.
            Application.targetFrameRate = IsMobile ? 60 : -1;
            QualitySettings.vSyncCount = IsDesktop ? 1 : 0;
            Application.runInBackground = IsDesktop;

            // Always off — MSAA softens pixel art and UI text.
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;

            // Menus idle: allow sleep. Active climb keeps the screen awake.
            SetKeepAwake(false);
        }

        /// <summary>
        /// Call after OS focus returns — frame rate / sleep can be clobbered by the system.
        /// </summary>
        public static void ReapplyRuntimeDefaults()
        {
            if (!initialized)
            {
                Initialize();
                return;
            }

            Application.targetFrameRate = IsMobile ? 60 : -1;
            QualitySettings.vSyncCount = IsDesktop ? 1 : 0;
            QualitySettings.antiAliasing = 0;
        }

        /// <summary>
        /// Keep the display on during active play; restore system sleep in menus.
        /// Critical for Android battery / Play vitals when left on the main menu.
        /// </summary>
        public static void SetKeepAwake(bool keepAwake)
        {
            if (!IsMobile)
            {
                Screen.sleepTimeout = SleepTimeout.SystemSetting;
                return;
            }

            Screen.sleepTimeout = keepAwake
                ? SleepTimeout.NeverSleep
                : SleepTimeout.SystemSetting;
        }

        /// <summary>
        /// Full 60 while climbing; drop while paused/menus to reduce heat and battery drain.
        /// </summary>
        public static void SetGameplayActive(bool gameplayActive)
        {
            if (!IsMobile)
            {
                Application.targetFrameRate = -1;
                return;
            }

            Application.targetFrameRate = gameplayActive ? 60 : 30;
            SetKeepAwake(gameplayActive);
        }

        static void ApplyOrientation()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            // Native / Game-view size is used as-is. Standalone defaults to 1080×2400
            // via Player Settings; do not lock resolution so any aspect can run.
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

        /// <summary>Live pixel size of the display / Game view.</summary>
        public static float ActiveWidth => Mathf.Max(1, Screen.width);

        /// <summary>Live pixel height of the display / Game view.</summary>
        public static float ActiveHeight => Mathf.Max(1, Screen.height);

        /// <summary>Live width/height. Falls back to 20:9 if the screen is not ready.</summary>
        public static float ActiveAspect
        {
            get
            {
                var h = ActiveHeight;
                if (h < 1f) return DesignAspect;
                return ActiveWidth / h;
            }
        }

        /// <summary>UI reference is the common 20:9 frame (1080×2400).</summary>
        public static Vector2 UiReferenceResolution => new Vector2(DesignWidth, DesignHeight);

        public static float ScreenAspect => ActiveAspect;
        public static float DesignAspect => DesignWidth / DesignHeight;

        /// <summary>
        /// Ortho that keeps lane width constant (match-width) on any aspect.
        /// 20:9 is the common case; taller phones show more climb, shorter phones less.
        /// </summary>
        public static float RecommendedOrthoSize(Camera cam = null)
        {
            var aspect = ActiveAspect;
            if (cam != null && cam.pixelHeight > 1)
                aspect = cam.aspect;
            if (aspect < 0.05f)
                aspect = DesignAspect;
            return ReferenceWorldHalfWidth / aspect;
        }

        /// <summary>
        /// Visible world half-width. Constant when using <see cref="RecommendedOrthoSize"/>.
        /// </summary>
        public static float WorldHalfWidth(float orthoSize = -1f)
        {
            if (orthoSize < 0f)
                return ReferenceWorldHalfWidth;
            return orthoSize * ActiveAspect;
        }

        /// <summary>Full camera view half-width (same as the playfield — no letterbox).</summary>
        public static float CameraHalfWidth(float orthoSize = -1f) =>
            WorldHalfWidth(orthoSize);

        /// <summary>
        /// X offset for left/right lanes — center of each half of the visible playfield.
        /// </summary>
        public static float LaneOffset(float orthoSize = -1f) =>
            WorldHalfWidth(orthoSize) * 0.5f;

        /// <summary>
        /// Scale so a sprite centered on a lane stays inside the side borders,
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
        /// Apply the shared 20:9 canvas scaler (match width, live screen size).
        /// </summary>
        public static void ApplyCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = UiMatchWidthOrHeight;
            scaler.referencePixelsPerUnit = CrispVisuals.SpritePPU;
        }

        /// <summary>
        /// Normalized anchors for the live display, inset only by the device safe area
        /// (notch / home indicator). No 16:9 letterbox.
        /// </summary>
        public static void GetPlayfieldNormalizedRect(out Vector2 anchorMin, out Vector2 anchorMax)
        {
            var sw = ActiveWidth;
            var sh = ActiveHeight;

            var safe = Screen.safeArea;
            var minX = Mathf.Clamp(safe.xMin, 0f, sw);
            var maxX = Mathf.Clamp(safe.xMax, 0f, sw);
            var minY = Mathf.Clamp(safe.yMin, 0f, sh);
            var maxY = Mathf.Clamp(safe.yMax, 0f, sh);

            if (maxX - minX < 8f)
            {
                minX = 0f;
                maxX = sw;
            }

            if (maxY - minY < 8f)
            {
                minY = 0f;
                maxY = sh;
            }

            anchorMin = new Vector2(minX / sw, minY / sh);
            anchorMax = new Vector2(maxX / sw, maxY / sh);
        }
    }
}
