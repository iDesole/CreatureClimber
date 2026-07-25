using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Keeps Unity uGUI <see cref="Text"/> sharp under <see cref="CanvasScaler"/>.
    /// Rasterizes glyphs at screen-pixel size, snaps layout to whole pixels,
    /// and uses point-filtered font atlases.
    /// </summary>
    [RequireComponent(typeof(Text))]
    [DisallowMultipleComponent]
    public class CrispUIText : MonoBehaviour
    {
        Text label;
        RectTransform rect;
        Canvas rootCanvas;

        int designFontSize = -1;
        Vector2 designSizeDelta;
        Vector2 designAnchoredPosition;
        bool layoutCaptured;
        float lastScale = -1f;

        void Awake()
        {
            Cache();
            CaptureDesignIfNeeded();
            Apply(true);
        }

        void OnEnable()
        {
            Cache();
            CaptureDesignIfNeeded();
            Apply(true);
        }

        void LateUpdate()
        {
            Apply(false);
        }

        public void SetDesignSize(int size)
        {
            designFontSize = Mathf.Max(1, size);
            layoutCaptured = false;
            CaptureDesignIfNeeded();
            Apply(true);
        }

        public void RefreshDesignLayout()
        {
            layoutCaptured = false;
            if (rect != null)
                rect.localScale = Vector3.one;
            if (label != null && designFontSize > 0)
                label.fontSize = designFontSize;
            CaptureDesignIfNeeded();
            lastScale = -1f;
            Apply(true);
        }

        void Cache()
        {
            if (label == null) label = GetComponent<Text>();
            if (rect == null) rect = transform as RectTransform;
            if (rootCanvas == null)
            {
                var c = GetComponentInParent<Canvas>();
                rootCanvas = c != null ? c.rootCanvas : null;
            }
        }

        void CaptureDesignIfNeeded()
        {
            if (label == null || rect == null) return;

            if (designFontSize < 1)
            {
                var sx = Mathf.Abs(rect.localScale.x);
                if (sx > 0.01f && sx < 0.999f)
                    designFontSize = Mathf.Max(1, Mathf.RoundToInt(label.fontSize * sx));
                else
                    designFontSize = Mathf.Max(1, label.fontSize);
            }

            if (layoutCaptured) return;

            var sx2 = Mathf.Abs(rect.localScale.x);
            if (sx2 < 0.01f) sx2 = 1f;

            if (sx2 < 0.999f)
                designSizeDelta = rect.sizeDelta * sx2;
            else
                designSizeDelta = rect.sizeDelta;

            designAnchoredPosition = rect.anchoredPosition;
            layoutCaptured = true;
        }

        void Apply(bool force)
        {
            if (label == null || rect == null) return;

            if (rootCanvas == null)
            {
                var c = GetComponentInParent<Canvas>();
                rootCanvas = c != null ? c.rootCanvas : null;
            }

            var scale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
            if (scale < 0.01f) scale = 1f;

            if (!force && Mathf.Abs(scale - lastScale) < 0.0005f)
            {
                CrispVisuals.SnapRectToPixels(rect, rootCanvas);
                return;
            }
            lastScale = scale;

            CaptureDesignIfNeeded();
            if (designFontSize < 1)
                designFontSize = Mathf.Max(1, label.fontSize);

            var stretched = (rect.anchorMin - rect.anchorMax).sqrMagnitude > 1e-6f;

            if (scale <= 1.001f)
            {
                // Rasterize at design size; snap layout so glyphs aren't half-pixel shifted.
                if (label.fontSize != designFontSize)
                    label.fontSize = designFontSize;
                rect.localScale = Vector3.one;
                if (!stretched)
                {
                    rect.sizeDelta = designSizeDelta;
                    rect.anchoredPosition = designAnchoredPosition;
                }
                else
                {
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }
            else
            {
                // Upscale case: draw glyphs at final screen size, counter-scale the rect.
                var pixelSize = Mathf.Max(1, Mathf.RoundToInt(designFontSize * scale));
                if (label.fontSize != pixelSize)
                    label.fontSize = pixelSize;

                var inv = 1f / scale;
                rect.localScale = new Vector3(inv, inv, 1f);

                if (!stretched)
                {
                    rect.sizeDelta = designSizeDelta * scale;
                    rect.anchoredPosition = designAnchoredPosition;
                }
                else
                {
                    var parent = rect.parent as RectTransform;
                    if (parent != null)
                    {
                        var parentSize = parent.rect.size;
                        if (parentSize.x > 1f && parentSize.y > 1f)
                        {
                            var grow = parentSize * (scale - 1f) * 0.5f;
                            rect.offsetMin = new Vector2(-grow.x, -grow.y);
                            rect.offsetMax = new Vector2(grow.x, grow.y);
                        }
                    }
                }
            }

            CrispVisuals.MakeUiTextCrisp(label);
            CrispVisuals.SnapRectToPixels(rect, rootCanvas);
            // Keep white + black even if font atlas rebuilds tints the graphic.
            if (label.color.r < 0.99f || label.color.g < 0.99f || label.color.b < 0.99f)
                UiTextStyle.Apply(label);
        }
    }
}
