using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Settings overlay: label on the left, On/Off pill on the right.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] Button closeButton;
        [SerializeField] OnOffToggle musicToggle;
        [SerializeField] OnOffToggle sfxToggle;
        [SerializeField] OnOffToggle visibilityToggle;
        [SerializeField] OnOffToggle mirrorToggle;

        bool open;

        public bool IsOpen => open;

        public static SettingsMenu Create(Transform parent)
        {
            var panel = new GameObject("SettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rootRect = panel.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            MenuScreenUnderlay.Ensure(panel.transform);

            var title = MakeText(panel.transform, "Title", "SETTINGS", UiTextStyle.MenuFontSize, TextAnchor.UpperCenter);
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0.5f, 1f);
            tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -RosterScreenLayout.HudBand);
            tr.sizeDelta = new Vector2(960f, 3f * RosterScreenLayout.Unit);

            var music = MakeRow(panel.transform, "Music", 0);
            var sfx = MakeRow(panel.transform, "SFX", 1);
            var vis = MakeRow(panel.transform, "Platform Visibility", 2);
            var mirror = MakeRow(panel.transform, "Mirror", 3);

            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panel.transform, false);
            var cImg = closeGo.GetComponent<Image>();
            cImg.color = Color.white;
            cImg.raycastTarget = true;
            var close = closeGo.GetComponent<Button>();
            close.targetGraphic = cImg;
            close.transition = Selectable.Transition.None;
            RosterScreenLayout.PlaceBackChevron(closeGo.GetComponent<RectTransform>());
            RosterScreenLayout.StyleBackChevron(close);

            var menu = panel.AddComponent<SettingsMenu>();
            menu.Setup(panel, close, music, sfx, vis, mirror);
            panel.SetActive(false);
            return menu;
        }

        public void Setup(
            GameObject panelRoot,
            Button close,
            OnOffToggle music,
            OnOffToggle sfx,
            OnOffToggle visibility,
            OnOffToggle mirror)
        {
            root = panelRoot;
            closeButton = close;
            musicToggle = music;
            sfxToggle = sfx;
            visibilityToggle = visibility;
            mirrorToggle = mirror;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            musicToggle?.Bind(on =>
            {
                GameSettings.MusicEnabled = on;
            });
            sfxToggle?.Bind(on =>
            {
                GameSettings.SfxEnabled = on;
            });
            visibilityToggle?.Bind(on =>
            {
                GameSettings.PlatformsVisible = on;
                GameManager.Instance?.ApplySettingsPreview();
            });
            mirrorToggle?.Bind(on =>
            {
                GameSettings.MirrorEnabled = on;
                GameManager.Instance?.ApplySettingsPreview();
            });

            if (root != null)
                root.SetActive(false);
            open = false;
        }

        public void Open()
        {
            if (root == null) return;
            open = true;
            root.SetActive(true);
            RefreshToggles();
            EnsureLabelLayout();
            RosterScreenLayout.PlaceBackChevron(closeButton != null ? closeButton.transform as RectTransform : null);
            RosterScreenLayout.StyleBackChevron(closeButton);
            FeedbackService.UiSelect();
        }

        public void Close()
        {
            if (!open)
            {
                if (root != null && root.activeSelf)
                    root.SetActive(false);
                return;
            }

            open = false;
            if (root != null)
                root.SetActive(false);
            FeedbackService.UiSelect();
            GameManager.Instance?.OnSettingsClosed();
        }

        void Update()
        {
            if (!open) return;
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        void RefreshToggles()
        {
            musicToggle?.Set(GameSettings.MusicEnabled, instant: true);
            sfxToggle?.Set(GameSettings.SfxEnabled, instant: true);
            visibilityToggle?.Set(GameSettings.PlatformsVisible, instant: true);
            mirrorToggle?.Set(GameSettings.MirrorEnabled, instant: true);
        }

        const int RowLabelSize = 56;

        void EnsureLabelLayout()
        {
            if (root == null) return;

            var title = root.transform.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                var host = root.transform as RectTransform;
                var tw = RosterScreenLayout.ContentWidth(host);
                RosterScreenLayout.PlaceTop(title.rectTransform, -RosterScreenLayout.HudBand, tw, 4f * RosterScreenLayout.Unit);
                RosterScreenLayout.ApplyOneLine(title, 64);
                title.alignment = TextAnchor.MiddleCenter;
            }

            var labels = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null || label.gameObject.name != "Label") continue;
                if (label.transform.parent != null && label.transform.parent.name == "CloseButton")
                    continue;
                if (label.transform.parent != null && label.transform.parent.name == "Toggle")
                    continue;
                StyleRowLabel(label);
            }
        }

        static void StyleRowLabel(Text label)
        {
            if (label == null) return;
            var tr = label.rectTransform;
            tr.anchorMin = new Vector2(0f, 0.5f);
            tr.anchorMax = new Vector2(0f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.anchoredPosition = new Vector2(RosterScreenLayout.Unit, 0f);
            var row = label.rectTransform.parent as RectTransform;
            var maxW = 40f * RosterScreenLayout.Unit;
            if (row != null && row.rect.width > 80f)
                maxW = Mathf.Max(16f * RosterScreenLayout.Unit, row.rect.width - 16f * RosterScreenLayout.Unit);
            tr.sizeDelta = new Vector2(maxW, 4f * RosterScreenLayout.Unit);
            tr.localScale = Vector3.one;
            RosterScreenLayout.ApplyOneLine(label, RowLabelSize);
            label.alignment = TextAnchor.MiddleLeft;
        }

        static OnOffToggle MakeRow(Transform parent, string label, int index)
        {
            var top = -RosterScreenLayout.HudBand - 5f * RosterScreenLayout.Unit;
            var step = 11f * RosterScreenLayout.Unit;
            var y = top - index * step;

            var row = new GameObject(label.Replace(" ", "") + "Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rr = row.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.5f, 1f);
            rr.anchorMax = new Vector2(0.5f, 1f);
            rr.pivot = new Vector2(0.5f, 1f);
            rr.anchoredPosition = new Vector2(0f, y);
            rr.sizeDelta = new Vector2(1000f, 130f);

            var text = MakeText(row.transform, "Label", label, RowLabelSize, TextAnchor.MiddleLeft);
            StyleRowLabel(text);

            var toggle = OnOffToggle.Build(row.transform, "Toggle");
            var tg = toggle.transform as RectTransform;
            tg.anchorMin = new Vector2(1f, 0.5f);
            tg.anchorMax = new Vector2(1f, 0.5f);
            tg.pivot = new Vector2(1f, 0.5f);
            tg.anchoredPosition = new Vector2(-8f, 0f);
            return toggle;
        }

        static Text MakeText(Transform parent, string name, string value, int size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var tr = go.GetComponent<RectTransform>();
            tr.sizeDelta = new Vector2(720f, 96f);

            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = value;
            label.fontSize = size;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            label.raycastTarget = false;
            UiTextStyle.Apply(label);
            go.AddComponent<CrispUIText>().SetDesignSize(size);
            return label;
        }
    }
}
