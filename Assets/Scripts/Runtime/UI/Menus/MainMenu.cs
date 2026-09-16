using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Ready-state hub: Play, Game Mode, Customize, Shop, Settings.
    /// Bottom-left stack, large left-aligned labels. No layout groups.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        const float Left = 36f;
        const float Bottom = 48f;
        const float Row = 124f;
        const float RightPad = 28f;

        [SerializeField] Button playButton;
        [SerializeField] Button gameModeButton;
        [SerializeField] Button customizeButton;
        [SerializeField] Button shopButton;
        [SerializeField] Button settingsButton;

        bool wired;

        public Button Play => playButton;
        public Button GameMode => gameModeButton;
        public Button Customize => customizeButton;
        public Button Shop => shopButton;
        public Button Settings => settingsButton;
        public bool HasSettings => settingsButton != null;

        /// <summary>Build a full-screen host with hub buttons under <paramref name="parent"/>.</summary>
        public static MainMenu Create(Transform parent)
        {
            var go = new GameObject("MainMenu", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var root = go.GetComponent<RectTransform>();
            StretchFull(root);

            var menu = go.AddComponent<MainMenu>();
            menu.settingsButton = menu.MakeButton("SettingsButton", "Settings");
            menu.shopButton = menu.MakeButton("ShopButton", "Shop");
            menu.customizeButton = menu.MakeButton("CustomizeButton", "Customize");
            menu.gameModeButton = menu.MakeButton("GameModeButton", "Game Mode");
            menu.playButton = menu.MakeButton("PlayButton", "Play");
            menu.Layout();
            return menu;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (playButton != null) playButton.gameObject.SetActive(true);
            if (gameModeButton != null) gameModeButton.gameObject.SetActive(true);
            if (customizeButton != null) customizeButton.gameObject.SetActive(true);
            if (shopButton != null) shopButton.gameObject.SetActive(true);
            if (settingsButton != null) settingsButton.gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Pin every item bottom-left. Safe to call every frame / on ready.</summary>
        public void Layout()
        {
            StretchFull(transform as RectTransform);

            // Settings at the floor, Play highest.
            Place(settingsButton, Bottom);
            Place(shopButton, Bottom + Row);
            Place(customizeButton, Bottom + Row * 2f);
            Place(gameModeButton, Bottom + Row * 3f);
            Place(playButton, Bottom + Row * 4f);
        }

        public void Wire(
            UnityAction onPlay,
            UnityAction onGameMode,
            UnityAction onCustomize,
            UnityAction onShop,
            UnityAction onSettings)
        {
            if (wired) return;
            Bind(playButton, onPlay);
            Bind(gameModeButton, onGameMode);
            Bind(customizeButton, onCustomize);
            Bind(shopButton, onShop);
            Bind(settingsButton, onSettings);
            wired = true;
        }

        public void Rebind(
            UnityAction onPlay,
            UnityAction onGameMode,
            UnityAction onCustomize,
            UnityAction onShop,
            UnityAction onSettings)
        {
            wired = false;
            Wire(onPlay, onGameMode, onCustomize, onShop, onSettings);
        }

        Button MakeButton(string name, string label)
        {
            var h = UiTextStyle.MainMenuFontSize + 28f;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(Left, Bottom);
            rect.sizeDelta = new Vector2(HubWidth(transform as RectTransform), h);

            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.None;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(8f, 0f);
            tr.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = UiTextStyle.MainMenuFontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            UiTextStyle.Apply(text);
            textGo.AddComponent<CrispUIText>().SetDesignSize(UiTextStyle.MainMenuFontSize);
            UiTextStyle.StyleMainMenuButton(button);
            return button;
        }

        static void Place(Button button, float y)
        {
            if (button == null) return;
            var rect = button.transform as RectTransform;
            if (rect == null) return;

            var h = UiTextStyle.MainMenuFontSize + 28f;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(Left, y);
            var host = button.transform.parent as RectTransform;
            rect.sizeDelta = new Vector2(HubWidth(host), h);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            UiTextStyle.StyleMainMenuButton(button);
        }

        static float HubWidth(RectTransform host)
        {
            var w = host != null ? host.rect.width : PlatformRuntime.DesignWidth;
            if (w < 80f) w = PlatformRuntime.DesignWidth;
            return Mathf.Max(240f, w - Left - RightPad);
        }

        static void StretchFull(RectTransform root)
        {
            if (root == null) return;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;
            root.localScale = Vector3.one;
            root.localRotation = Quaternion.identity;
        }

        static void Bind(Button button, UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
