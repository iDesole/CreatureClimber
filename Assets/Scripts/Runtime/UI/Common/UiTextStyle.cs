using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Large bold UI type. Smooth bilinear glyphs, soft outline (not a noisy 8-copy stroke).
    /// Wraps inside the parent so nothing leaves the screen.
    /// CanvasScaler match-width grows sizes with the phone.
    /// </summary>
    public static class UiTextStyle
    {
        public const int MenuFontSize = 72;
        public const int MainMenuFontSize = 96;
        public const int HudFontSize = 64;
        public const int LiveScoreFontSize = 96;
        public const int CoinHudFontSize = 64;
        public const int CaptionFontSize = 58;

        public static float ScreenScale =>
            PlatformRuntime.ActiveWidth / Mathf.Max(1f, PlatformRuntime.DesignWidth);

        public static void Apply(Text label)
        {
            if (label == null) return;

            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignByGeometry = false;
            label.resizeTextForBestFit = false;

            // Wrap inside the box. Overflow was walking off the screen.
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            SmoothOutline(label);
            CrispVisuals.MakeUiTextSmooth(label);
        }

        /// <summary>
        /// Soft drop only. Unity Outline stamps eight glyph copies and reads
        /// as static / pixel crawl, especially at large sizes.
        /// </summary>
        static void SmoothOutline(Text label)
        {
            var extras = label.GetComponents<Shadow>();
            Shadow drop = null;
            for (var i = 0; i < extras.Length; i++)
            {
                var s = extras[i];
                if (s == null) continue;
                if (s is Outline)
                {
                    UnityEngine.Object.Destroy(s);
                    continue;
                }

                if (drop == null) drop = s;
                else UnityEngine.Object.Destroy(s);
            }

            if (drop == null)
                drop = label.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.45f);
            drop.effectDistance = new Vector2(0f, -2f);
            drop.useGraphicAlpha = true;
        }

        /// <summary>Keep a label's box inside its parent (safe-area / screen).</summary>
        public static void ConstrainToParent(Text label, float pad = 16f)
        {
            if (label == null) return;
            var rect = label.rectTransform;
            var parent = rect.parent as RectTransform;
            if (rect == null || parent == null) return;

            var parentW = parent.rect.width;
            if (parentW < 8f) return;

            var maxW = Mathf.Max(48f, parentW - pad * 2f);
            var stretched = Mathf.Abs(rect.anchorMin.x - rect.anchorMax.x) > 1e-4f;
            if (stretched) return;

            var size = rect.sizeDelta;
            if (size.x > maxW)
            {
                size.x = maxW;
                rect.sizeDelta = size;
            }

            // Nudge back if the box itself crosses the parent.
            var local = rect.anchoredPosition;
            var pivot = rect.pivot;
            var left = local.x - size.x * pivot.x;
            var right = local.x + size.x * (1f - pivot.x);
            var pMin = parent.rect.xMin + pad;
            var pMax = parent.rect.xMax - pad;
            if (left < pMin)
                local.x += pMin - left;
            if (right > pMax)
                local.x -= right - pMax;
            rect.anchoredPosition = local;
        }

        public static void ApplyAllUnder(Transform root)
        {
            if (root == null) return;
            var labels = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
                Apply(labels[i]);
        }

        public static void ApplyImportant(Text label, bool muted = false) => Apply(label);
        public static void ApplyCustomize(Text label) => Apply(label);
        public static void ApplyScore(Text label) => Apply(label);

        public static void ApplySized(Text label, int fontSize, TextAnchor align = TextAnchor.MiddleCenter)
        {
            if (label == null) return;
            label.fontSize = fontSize;
            label.alignment = align;
            Apply(label);
            var crisp = label.GetComponent<CrispUIText>();
            if (crisp != null)
                crisp.SetDesignSize(fontSize);
        }

        public static void StripButtonChrome(Button button, bool whiteBlack = true)
        {
            if (button == null) return;

            button.transition = Selectable.Transition.None;

            var img = button.targetGraphic as Image;
            if (img == null)
                img = button.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(1f, 1f, 1f, 0f);
                img.raycastTarget = true;
                button.targetGraphic = img;
            }

            SetButtonLabelSize(button, MenuFontSize);
        }

        public static void SetButtonLabelSize(Button button, int fontSize)
        {
            if (button == null) return;
            var labels = button.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var t = labels[i];
                if (t == null) continue;
                t.fontSize = fontSize;
                t.alignment = TextAnchor.MiddleCenter;
                Apply(t);
                var crisp = t.GetComponent<CrispUIText>();
                if (crisp != null)
                    crisp.SetDesignSize(fontSize);
            }
        }

        public static void StyleMenuButton(Button button)
        {
            StripButtonChrome(button);
            SetButtonLabelSize(button, MenuFontSize);
        }

        public static void StyleMainMenuButton(Button button)
        {
            if (button == null) return;

            StripButtonChrome(button);

            var labels = button.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var t = labels[i];
                if (t == null) continue;
                t.fontSize = MainMenuFontSize;
                t.alignment = TextAnchor.MiddleLeft;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                Apply(t);
                var crisp = t.GetComponent<CrispUIText>();
                if (crisp != null)
                    crisp.SetDesignSize(MainMenuFontSize);
            }
        }

        public static Color ButtonPlate => new Color(0.1f, 0.12f, 0.18f, 0.75f);
        public static Color ButtonPlateAlt => new Color(0.14f, 0.16f, 0.22f, 0.85f);
    }
}
