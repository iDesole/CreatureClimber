using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Pins interactive UI to the device safe area (notch / home indicator).
    /// The playfield follows the live screen; 20:9 (1080×2400) is the common frame.
    /// Menu dimmers stretch to the full canvas via <see cref="MenuScreenUnderlay"/>.
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
