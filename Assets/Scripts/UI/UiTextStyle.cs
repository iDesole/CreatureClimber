using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Global UI type: bold white fill + black outline, sizes in design-space
    /// (CanvasScaler scales them with the phone frame).
    /// </summary>
    public static class UiTextStyle
    {
        /// <summary>Shared size for all menu actions (Play, Customize, Resume, Menu, Try Again, Back).</summary>
        public const int MenuFontSize = 48;

        /// <summary>Shared size for score / best / pause title lines.</summary>
        public const int HudFontSize = 48;

        /// <summary>Live climb score (slightly larger so it reads while playing).</summary>
        public const int LiveScoreFontSize = 64;

        public static void Apply(Text label)
        {
            if (label == null) return;

            label.fontStyle = FontStyle.Bold;
            // Force pure white every time (override any leftover scene/orange tints).
            label.color = new Color(1f, 1f, 1f, 1f);

            // Strip any extra shadow/outline leftovers, then add a single black outline.
            var shadows = label.GetComponents<Shadow>();
            for (var i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && !(shadows[i] is Outline))
                    UnityEngine.Object.Destroy(shadows[i]);
            }

            var outlines = label.GetComponents<Outline>();
            Outline outline = null;
            for (var i = 0; i < outlines.Length; i++)
            {
                if (i == 0) outline = outlines[i];
                else if (outlines[i] != null)
                    UnityEngine.Object.Destroy(outlines[i]);
            }

            if (outline == null)
                outline = label.gameObject.AddComponent<Outline>();

            outline.effectColor = new Color(0f, 0f, 0f, 1f);
            // Design-space outline; CanvasScaler scales this with the screen.
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = true;
        }

        /// <summary>Force every Text under a root to white + black outline.</summary>
        public static void ApplyAllUnder(Transform root)
        {
            if (root == null) return;
            var labels = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
                Apply(labels[i]);
        }

        // Aliases so call sites stay readable.
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

        /// <summary>Text-only control: no highlight plate, white/black label.</summary>
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

        public static Color ButtonPlate => new Color(0.1f, 0.12f, 0.18f, 0.75f);
        public static Color ButtonPlateAlt => new Color(0.14f, 0.16f, 0.22f, 0.85f);
    }
}
