using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Pins UI to the original 1080×1920 playfield (centered on taller 1080×2340 screens)
    /// and intersects device safeArea for notches / home indicators.
    /// Background is free to fill the full modern phone frame outside this rect.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rect;
        Rect lastSafe;
        Vector2Int lastScreen;

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            Apply();
        }

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != lastSafe ||
                Screen.width != lastScreen.x ||
                Screen.height != lastScreen.y)
            {
                Apply();
            }
        }

        public void Apply()
        {
            if (rect == null)
                rect = GetComponent<RectTransform>();

            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);

            PlatformRuntime.GetPlayfieldNormalizedRect(out var min, out var max);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
