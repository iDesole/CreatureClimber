using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Keeps a label at its design font size. No pixel-snap or point-filter —
    /// those made outlines look static. CanvasScaler handles screen scale.
    /// </summary>
    [RequireComponent(typeof(Text))]
    [DisallowMultipleComponent]
    public class CrispUIText : MonoBehaviour
    {
        Text label;
        RectTransform rect;
        int designFontSize = -1;

        void Awake() => Apply();

        void OnEnable() => Apply();

        public void SetDesignSize(int size)
        {
            designFontSize = Mathf.Max(1, size);
            Apply();
        }

        public void RefreshDesignLayout()
        {
            if (rect == null)
                rect = transform as RectTransform;
            if (rect != null)
                rect.localScale = Vector3.one;
            Apply();
        }

        void Apply()
        {
            if (label == null) label = GetComponent<Text>();
            if (rect == null) rect = transform as RectTransform;
            if (label == null) return;

            if (designFontSize < 1)
                designFontSize = Mathf.Max(1, label.fontSize);

            if (label.fontSize != designFontSize)
                label.fontSize = designFontSize;

            if (rect != null)
                rect.localScale = Vector3.one;

            CrispVisuals.MakeUiTextSmooth(label);
        }
    }
}
