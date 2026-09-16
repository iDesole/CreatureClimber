using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Full-canvas menu backdrop. Solid midnight black for browse menus; black veil for pause / game over.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MenuScreenUnderlay : MonoBehaviour
    {
        public static readonly Color MidnightBlack = new Color(0.04f, 0.05f, 0.09f, 1f);

        RectTransform rect;
        Rect lastSafe;
        Vector2Int lastScreen;

        public static readonly Color PauseVeil = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color GameOverVeil = new Color(0f, 0f, 0f, 0.6f);

        /// <summary>Full-screen midnight black (shop / customize / settings / game mode).</summary>
        public static void Ensure(Transform panel, Color? color = null) =>
            EnsureInternal(panel, color ?? MidnightBlack);

        /// <summary>Translucent black veil (pause / game over).</summary>
        public static void EnsureVeil(Transform panel, Color? veil = null) =>
            EnsureInternal(panel, veil ?? GameOverVeil);

        static void EnsureInternal(Transform panel, Color fill)
        {
            if (panel == null) return;

            var existing = panel.Find("Underlay");
            GameObject underlayGo;
            if (existing != null)
            {
                underlayGo = existing.gameObject;
            }
            else
            {
                underlayGo = new GameObject("Underlay", typeof(RectTransform), typeof(Image), typeof(MenuScreenUnderlay));
                underlayGo.transform.SetParent(panel, false);
            }

            underlayGo.transform.SetAsFirstSibling();

            var img = underlayGo.GetComponent<Image>();
            if (img == null)
                img = underlayGo.AddComponent<Image>();

            img.sprite = SolidSprite();
            img.color = fill;
            img.type = Image.Type.Simple;
            img.raycastTarget = true;
            img.preserveAspect = false;

            if (underlayGo.GetComponent<MenuScreenUnderlay>() == null)
                underlayGo.AddComponent<MenuScreenUnderlay>();

            var panelImg = panel.GetComponent<Image>();
            if (panelImg != null && panelImg.gameObject == panel.gameObject)
            {
                panelImg.enabled = false;
                panelImg.raycastTarget = false;
            }

            var stretch = underlayGo.GetComponent<MenuScreenUnderlay>();
            stretch.Apply();
        }

        static Sprite s_solid;

        static Sprite SolidSprite()
        {
            if (s_solid != null) return s_solid;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);
            s_solid = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return s_solid;
        }

        void Awake() => Apply();

        void OnEnable() => Apply();

        void LateUpdate()
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
                rect = transform as RectTransform;
            if (rect == null) return;

            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var canvasRt = canvas.rootCanvas != null
                ? canvas.rootCanvas.transform as RectTransform
                : canvas.transform as RectTransform;
            var parent = rect.parent as RectTransform;
            if (canvasRt == null || parent == null) return;

            var corners = new Vector3[4];
            canvasRt.GetWorldCorners(corners);
            var localMin = (Vector2)parent.InverseTransformPoint(corners[0]);
            var localMax = (Vector2)parent.InverseTransformPoint(corners[2]);
            var p = parent.rect;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = localMin - p.min;
            rect.offsetMax = localMax - p.max;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }
}
