using System;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Easy/Hard-style oval pill: Off (left, gray) / On (right, green).
    /// </summary>
    public class OnOffToggle : MonoBehaviour
    {
        static readonly Color OffTrack = new Color(0.36f, 0.38f, 0.42f, 1f);
        static readonly Color OnTrack = new Color(0.22f, 0.78f, 0.40f, 1f);
        static readonly Color KnobFace = Color.white;

        const float TrackWidth = 168f;
        const float TrackHeight = 64f;
        const float KnobSize = 52f;
        const float KnobPad = 6f;
        const int LabelFont = 38;

        static Sprite s_circleSprite;
        static Sprite s_pixelSprite;

        [SerializeField] Button button;
        [SerializeField] Image trackMid;
        [SerializeField] Image trackLeft;
        [SerializeField] Image trackRight;
        [SerializeField] Image knob;
        [SerializeField] Text label;
        [SerializeField] RectTransform knobRect;

        bool isOn;
        float knobT;
        Action<bool> onChanged;

        public bool IsOn => isOn;

        public static OnOffToggle Build(Transform parent, string name)
        {
            EnsureSprites();

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(OnOffToggle));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(TrackWidth + 48f, TrackHeight + 28f);

            var hit = go.GetComponent<Image>();
            hit.sprite = s_pixelSprite;
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            hit.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;

            var trackRoot = new GameObject("Track", typeof(RectTransform));
            trackRoot.transform.SetParent(go.transform, false);
            var tr = trackRoot.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.5f, 0.5f);
            tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(TrackWidth, TrackHeight);
            tr.anchoredPosition = Vector2.zero;

            var trackLeft = CreateFilled(trackRoot.transform, "LeftCap", s_circleSprite);
            var leftR = trackLeft.rectTransform;
            leftR.anchorMin = new Vector2(0f, 0.5f);
            leftR.anchorMax = new Vector2(0f, 0.5f);
            leftR.pivot = new Vector2(0f, 0.5f);
            leftR.sizeDelta = new Vector2(TrackHeight, TrackHeight);
            leftR.anchoredPosition = Vector2.zero;

            var trackRight = CreateFilled(trackRoot.transform, "RightCap", s_circleSprite);
            var rightR = trackRight.rectTransform;
            rightR.anchorMin = new Vector2(1f, 0.5f);
            rightR.anchorMax = new Vector2(1f, 0.5f);
            rightR.pivot = new Vector2(1f, 0.5f);
            rightR.sizeDelta = new Vector2(TrackHeight, TrackHeight);
            rightR.anchoredPosition = Vector2.zero;

            var trackMid = CreateFilled(trackRoot.transform, "Mid", s_pixelSprite);
            var midR = trackMid.rectTransform;
            midR.anchorMin = new Vector2(0f, 0f);
            midR.anchorMax = new Vector2(1f, 1f);
            midR.pivot = new Vector2(0.5f, 0.5f);
            midR.offsetMin = new Vector2(TrackHeight * 0.5f - 1f, 0f);
            midR.offsetMax = new Vector2(-(TrackHeight * 0.5f - 1f), 0f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lr = labelGo.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0.5f, 0.5f);
            lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(78f, TrackHeight);

            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.fontStyle = FontStyle.Bold;
            label.fontSize = LabelFont;
            label.color = Color.white;
            UiTextStyle.Apply(label);
            if (labelGo.GetComponent<CrispUIText>() == null)
                labelGo.AddComponent<CrispUIText>().SetDesignSize(LabelFont);

            var knob = CreateFilled(go.transform, "Knob", s_circleSprite);
            var kr = knob.rectTransform;
            kr.anchorMin = new Vector2(0.5f, 0.5f);
            kr.anchorMax = new Vector2(0.5f, 0.5f);
            kr.pivot = new Vector2(0.5f, 0.5f);
            kr.sizeDelta = new Vector2(KnobSize, KnobSize);
            knob.color = KnobFace;
            knob.raycastTarget = false;

            trackRoot.transform.SetSiblingIndex(0);
            labelGo.transform.SetSiblingIndex(1);
            knob.transform.SetAsLastSibling();

            var toggle = go.GetComponent<OnOffToggle>();
            toggle.button = button;
            toggle.trackMid = trackMid;
            toggle.trackLeft = trackLeft;
            toggle.trackRight = trackRight;
            toggle.knob = knob;
            toggle.label = label;
            toggle.knobRect = kr;
            toggle.Wire();
            toggle.Set(false, instant: true);
            return toggle;
        }

        public void Bind(Action<bool> handler)
        {
            onChanged = handler;
        }

        public void Set(bool on, bool instant = false)
        {
            isOn = on;
            if (instant)
            {
                knobT = on ? 1f : 0f;
                ApplyVisual();
            }
        }

        void Awake() => Wire();

        void Wire()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
        }

        void OnClicked()
        {
            isOn = !isOn;
            FeedbackService.UiSelect();
            onChanged?.Invoke(isOn);
        }

        void Update()
        {
            var target = isOn ? 1f : 0f;
            if (Mathf.Approximately(knobT, target))
                return;
            knobT = Mathf.MoveTowards(knobT, target, 10f * Time.unscaledDeltaTime);
            ApplyVisual();
        }

        void ApplyVisual()
        {
            var color = Color.Lerp(OffTrack, OnTrack, knobT);
            if (trackMid != null) trackMid.color = color;
            if (trackLeft != null) trackLeft.color = color;
            if (trackRight != null) trackRight.color = color;
            if (knob != null) knob.color = KnobFace;

            if (label != null)
            {
                label.text = isOn ? "On" : "Off";
                label.color = Color.white;
                UiTextStyle.Apply(label);
                var lr = label.rectTransform;
                lr.anchoredPosition = new Vector2(isOn ? -28f : 28f, 0f);
            }

            if (knobRect != null)
            {
                var minX = -(TrackWidth * 0.5f) + KnobSize * 0.5f + KnobPad;
                var maxX = (TrackWidth * 0.5f) - KnobSize * 0.5f - KnobPad;
                knobRect.anchoredPosition = new Vector2(Mathf.Lerp(minX, maxX, knobT), 0f);
            }
        }

        static Image CreateFilled(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;
            return img;
        }

        static void EnsureSprites()
        {
            if (s_circleSprite == null)
                s_circleSprite = CreateCircleSprite(128);
            if (s_pixelSprite == null)
                s_pixelSprite = CreatePixelSprite();
        }

        static Sprite CreatePixelSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        static Sprite CreateCircleSprite(int size)
        {
            size = Mathf.Max(32, size);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var r = (size - 1) * 0.5f;
            const float aa = 1.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - r;
                    var dy = y - r;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = 1f - Mathf.Clamp01((dist - (r - aa)) / aa);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
