using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    public class GameUI : MonoBehaviour, IGameUI
    {
        [SerializeField] Text scoreText;
        [SerializeField] Text highScoreText;
        [SerializeField] Text centerMessageText;
        [SerializeField] Text hintText;
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] Text finalScoreText;
        [SerializeField] Text finalHighScoreText;
        [SerializeField] Button restartButton;
        [SerializeField] Button menuButton;
        [SerializeField] GameObject pausePanel;
        [SerializeField] Text pauseMessageText;
        [SerializeField] Button pauseButton;
        [SerializeField] Button resumeButton;
        [SerializeField] Button pauseMenuButton;
        [SerializeField] Button customizeButton;
        [SerializeField] Button shopButton;
        [SerializeField] Button playButton;
        [SerializeField] GameObject mainMenuRoot;
        [SerializeField] CustomizeMenu customizeMenu;
        [SerializeField] ShopMenu shopMenu;
        [SerializeField] GameObject coinHudRoot;
        [SerializeField] Text coinHudText;
        [SerializeField] Image coinHudIcon;

        bool restartWired;
        bool menuWired;
        bool pauseWired;
        bool pauseMenuWired;
        bool customizeWired;
        bool shopWired;
        bool playWired;
        Coroutine flashRoutine;

        public bool IsCustomizeOpen => customizeMenu != null && customizeMenu.IsOpen;
        public bool IsShopOpen => shopMenu != null && shopMenu.IsOpen;

        public void Setup(
            Text score,
            Text highScore,
            Text centerMessage,
            GameObject overPanel,
            Text finalScore,
            Text finalBest,
            Button restart,
            GameObject pause = null,
            Text pauseMessage = null,
            Text hint = null,
            Button pauseBtn = null,
            Button resumeBtn = null,
            Button customizeBtn = null,
            CustomizeMenu customize = null,
            Button playBtn = null,
            GameObject mainMenu = null,
            Button menuBtn = null,
            Button pauseMenuBtn = null,
            Button shopBtn = null,
            ShopMenu shop = null)
        {
            scoreText = score;
            highScoreText = highScore;
            centerMessageText = centerMessage;
            gameOverPanel = overPanel;
            finalScoreText = finalScore;
            finalHighScoreText = finalBest;
            restartButton = restart;
            menuButton = menuBtn;
            pausePanel = pause;
            pauseMessageText = pauseMessage;
            hintText = hint;
            pauseButton = pauseBtn;
            resumeButton = resumeBtn;
            pauseMenuButton = pauseMenuBtn;
            customizeButton = customizeBtn;
            customizeMenu = customize;
            playButton = playBtn;
            mainMenuRoot = mainMenu;
            shopButton = shopBtn;
            shopMenu = shop;
            restartWired = false;
            menuWired = false;
            pauseWired = false;
            pauseMenuWired = false;
            customizeWired = false;
            shopWired = false;
            playWired = false;
            WireRestartButton();
            WireMenuButton();
            WirePauseButtons();
            WirePauseMenuButton();
            WireCustomizeButton();
            WireShopButton();
            WirePlayButton();
        }

        void Awake()
        {
            EnsureMainMenuUi();
            EnsureCustomizeUi();
            EnsureShopUi();
            EnsureCoinHud();
            EnsureGameOverMenuButton();
            EnsurePauseMenuButton();
            FitPhoneUi();
            EnsureSharpText();
            ApplyTextStyles();
            RefreshCoinHud();
        }

        void Start()
        {
            EnsureMainMenuUi();
            EnsureCustomizeUi();
            EnsureShopUi();
            EnsureCoinHud();
            EnsureGameOverMenuButton();
            EnsurePauseMenuButton();
            FitPhoneUi();
            EnsureSharpText();
            ApplyTextStyles();
            WireRestartButton();
            WireMenuButton();
            WirePauseButtons();
            WirePauseMenuButton();
            WireCustomizeButton();
            WireShopButton();
            WirePlayButton();
            RefreshCoinHud();
        }

        // Compact top-right wallet: [coin][gap][count], packed against the right edge.
        const float DefaultCoinHudIconSize = 32f;
        const float CoinHudGap = 12f; // space between icon and number (outline needs room)
        const float CoinHudMarginX = 12f;
        const float CoinHudMarginY = 16f;

        float ResolveHudCoinSize()
        {
            var settings = CurrencySettings.Load();
            return settings != null ? settings.HudIconSize : DefaultCoinHudIconSize;
        }

        void EnsureCoinHud()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Transform host = canvas.transform;
            var safe = canvas.transform.Find("SafeArea");
            if (safe != null) host = safe;

            if (coinHudRoot == null)
            {
                var existing = host.Find("CoinHud");
                if (existing != null)
                {
                    coinHudRoot = existing.gameObject;
                    coinHudText = existing.Find("WalletText")?.GetComponent<Text>();
                    coinHudIcon = existing.Find("Coin")?.GetComponent<Image>();
                }
            }

            if (coinHudRoot == null)
            {
                coinHudRoot = new GameObject("CoinHud", typeof(RectTransform));
                coinHudRoot.transform.SetParent(host, false);
            }
            else if (coinHudRoot.transform.parent != host)
            {
                coinHudRoot.transform.SetParent(host, false);
            }

            if (coinHudIcon == null)
            {
                var coinTf = coinHudRoot.transform.Find("Coin");
                if (coinTf != null)
                    coinHudIcon = coinTf.GetComponent<Image>();
            }

            if (coinHudIcon == null)
            {
                var coinGo = new GameObject("Coin", typeof(RectTransform), typeof(Image));
                coinGo.transform.SetParent(coinHudRoot.transform, false);
                coinHudIcon = coinGo.GetComponent<Image>();
            }

            if (coinHudText == null)
            {
                var textTf = coinHudRoot.transform.Find("WalletText");
                if (textTf != null)
                    coinHudText = textTf.GetComponent<Text>();
            }

            if (coinHudText == null)
            {
                var textGo = new GameObject("WalletText", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(coinHudRoot.transform, false);
                coinHudText = textGo.GetComponent<Text>();
                coinHudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                textGo.AddComponent<CrispUIText>().SetDesignSize(36);
            }

            LayoutCoinHud();
        }

        /// <summary>
        /// Always re-apply so scene leftovers / old layouts get corrected every show.
        /// Layout: icon on the left of a tight strip, count right-aligned against the screen edge.
        /// </summary>
        void LayoutCoinHud()
        {
            if (coinHudRoot == null) return;

            var rootRect = coinHudRoot.GetComponent<RectTransform>();
            if (rootRect == null) return;

            // Tight strip anchored to top-right (not a 260px-wide bar that pulls the icon left).
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-CoinHudMarginX, -CoinHudMarginY);
            rootRect.sizeDelta = new Vector2(148f, 40f);

            var iconSize = ResolveHudCoinSize();

            if (coinHudIcon != null)
            {
                var coinRect = coinHudIcon.rectTransform;
                coinRect.anchorMin = new Vector2(0f, 0.5f);
                coinRect.anchorMax = new Vector2(0f, 0.5f);
                coinRect.pivot = new Vector2(0f, 0.5f);
                coinRect.anchoredPosition = Vector2.zero;
                coinRect.sizeDelta = new Vector2(iconSize, iconSize);

                coinHudIcon.sprite = PixelSpriteFactory.CreateCoinSprite();
                coinHudIcon.preserveAspect = true;
                coinHudIcon.raycastTarget = false;
                coinHudIcon.color = Color.white;
                CrispVisuals.MakeSpriteCrisp(coinHudIcon.sprite);
            }

            if (coinHudText != null)
            {
                var tr = coinHudText.rectTransform;
                // Sit to the right of the icon with a clear gap; hug the right edge.
                var textLeft = iconSize + CoinHudGap;
                tr.anchorMin = new Vector2(0f, 0f);
                tr.anchorMax = new Vector2(1f, 1f);
                tr.offsetMin = new Vector2(textLeft, 0f);
                tr.offsetMax = Vector2.zero;
                tr.pivot = new Vector2(1f, 0.5f);

                coinHudText.fontSize = 36;
                coinHudText.alignment = TextAnchor.MiddleRight;
                coinHudText.horizontalOverflow = HorizontalWrapMode.Overflow;
                coinHudText.verticalOverflow = VerticalWrapMode.Overflow;
                coinHudText.raycastTarget = false;
                UiTextStyle.ApplySized(coinHudText, 36, TextAnchor.MiddleRight);
            }
        }

        public void RefreshCoinHud()
        {
            EnsureCoinHud();
            LayoutCoinHud();
            if (coinHudText != null)
            {
                coinHudText.text = SaveService.Coins.ToString();
                UiTextStyle.ApplySized(coinHudText, 36, TextAnchor.MiddleRight);
            }
        }

        void ApplyTextStyles()
        {
            // Nuke any leftover orange/colored type under this UI root.
            UiTextStyle.ApplyAllUnder(transform);

            // HUD sizes (white + black).
            UiTextStyle.ApplySized(scoreText, UiTextStyle.LiveScoreFontSize, TextAnchor.UpperCenter);
            UiTextStyle.ApplySized(highScoreText, UiTextStyle.MenuFontSize, TextAnchor.MiddleCenter);
            UiTextStyle.ApplySized(centerMessageText, UiTextStyle.MenuFontSize);
            UiTextStyle.ApplySized(finalScoreText, UiTextStyle.MenuFontSize);
            UiTextStyle.ApplySized(finalHighScoreText, UiTextStyle.MenuFontSize);
            UiTextStyle.ApplySized(pauseMessageText, UiTextStyle.MenuFontSize);

            StyleAllMenuButtons();
        }

        void StyleAllMenuButtons()
        {
            // Main menu + every other menu control: identical size, centered, white/black.
            UiTextStyle.StyleMenuButton(playButton);
            UiTextStyle.StyleMenuButton(customizeButton);
            UiTextStyle.StyleMenuButton(shopButton);
            UiTextStyle.StyleMenuButton(pauseButton);
            UiTextStyle.StyleMenuButton(resumeButton);
            UiTextStyle.StyleMenuButton(pauseMenuButton);
            UiTextStyle.StyleMenuButton(restartButton);
            UiTextStyle.StyleMenuButton(menuButton);

            if (mainMenuRoot != null)
            {
                LayoutMenuStack(mainMenuRoot.transform);
                var labels = mainMenuRoot.GetComponentsInChildren<Text>(true);
                for (var i = 0; i < labels.Length; i++)
                    UiTextStyle.ApplySized(labels[i], UiTextStyle.MenuFontSize, TextAnchor.MiddleCenter);
            }

            // Also force any other Text that might still carry old colors.
            UiTextStyle.ApplyAllUnder(transform);
            if (mainMenuRoot != null)
            {
                var labels = mainMenuRoot.GetComponentsInChildren<Text>(true);
                for (var i = 0; i < labels.Length; i++)
                    UiTextStyle.ApplySized(labels[i], UiTextStyle.MenuFontSize, TextAnchor.MiddleCenter);
            }
        }

        void StyleGameOverActions() => StyleAllMenuButtons();

        void EnsurePauseMenuButton()
        {
            if (pauseMenuButton != null) return;
            if (pausePanel == null) return;

            var existing = pausePanel.transform.Find("PauseMenuButton");
            if (existing != null)
            {
                pauseMenuButton = existing.GetComponent<Button>();
                pauseMenuWired = false;
                WirePauseMenuButton();
                return;
            }

            pauseMenuButton = CreateRuntimeButton(
                pausePanel.transform,
                "PauseMenuButton",
                "Menu",
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -160f),
                new Vector2(400f, 100f),
                new Color(1f, 1f, 1f, 0f));
            UiTextStyle.StripButtonChrome(pauseMenuButton);
            pauseMenuWired = false;
            WirePauseMenuButton();
        }

        void EnsureGameOverMenuButton()
        {
            if (menuButton != null) return;
            if (gameOverPanel == null) return;

            var existing = gameOverPanel.transform.Find("MenuButton");
            if (existing != null)
            {
                menuButton = existing.GetComponent<Button>();
                menuWired = false;
                WireMenuButton();
                return;
            }

            menuButton = CreateRuntimeButton(
                gameOverPanel.transform,
                "MenuButton",
                "Menu",
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -230f),
                new Vector2(480f, 110f),
                new Color(1f, 1f, 1f, 0f));
            UiTextStyle.StyleMenuButton(menuButton);
            menuWired = false;
            WireMenuButton();
        }

        static Button CreateRuntimeButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchor,
            Vector2 anchoredPos,
            Vector2 size,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

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
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 36;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            UiTextStyle.Apply(text);
            textGo.AddComponent<CrispUIText>().SetDesignSize(UiTextStyle.MenuFontSize);
            UiTextStyle.StyleMenuButton(button);
            return button;
        }

        void EnsureMainMenuUi()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Transform host = canvas.transform;
            var safe = canvas.transform.Find("SafeArea");
            if (safe != null) host = safe;

            if (mainMenuRoot == null)
            {
                var existing = host.Find("MainMenu");
                if (existing != null)
                    mainMenuRoot = existing.gameObject;
            }

            if (mainMenuRoot == null)
                mainMenuRoot = CreateRuntimeMainMenu(host, out playButton, out customizeButton, out shopButton);
            else
            {
                if (playButton == null)
                {
                    var p = mainMenuRoot.transform.Find("PlayButton");
                    if (p != null) playButton = p.GetComponent<Button>();
                }

                if (customizeButton == null)
                {
                    var c = mainMenuRoot.transform.Find("CustomizeButton");
                    if (c != null) customizeButton = c.GetComponent<Button>();
                }

                if (shopButton == null)
                {
                    var s = mainMenuRoot.transform.Find("ShopButton");
                    if (s != null) shopButton = s.GetComponent<Button>();
                    if (shopButton == null)
                    {
                        var step = UiTextStyle.MenuFontSize + 40f;
                        shopButton = CreateMenuTextButton(mainMenuRoot.transform, "ShopButton", "Shop", 80f - step * 2f);
                    }
                }
            }

            playWired = false;
            customizeWired = false;
            shopWired = false;
            WirePlayButton();
            WireCustomizeButton();
            WireShopButton();
        }

        void EnsureCustomizeUi()
        {
            if (customizeMenu == null)
                customizeMenu = GetComponentInChildren<CustomizeMenu>(true);

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Transform host = canvas.transform;
            var safe = canvas.transform.Find("SafeArea");
            if (safe != null) host = safe;

            if (customizeMenu == null)
                customizeMenu = CreatureClimbSceneBuilder.CreateCustomizeMenuPublic(host);

            customizeWired = false;
            WireCustomizeButton();
        }

        void EnsureShopUi()
        {
            if (shopMenu == null)
                shopMenu = GetComponentInChildren<ShopMenu>(true);

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Transform host = canvas.transform;
            var safe = canvas.transform.Find("SafeArea");
            if (safe != null) host = safe;

            if (shopMenu == null)
                shopMenu = CreatureClimbSceneBuilder.CreateShopMenuPublic(host);

            shopWired = false;
            WireShopButton();
        }

        static GameObject CreateRuntimeMainMenu(Transform parent, out Button playBtn, out Button customizeBtn, out Button shopBtn)
        {
            var root = new GameObject("MainMenu", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(640f, 360f);

            var step = UiTextStyle.MenuFontSize + 40f;
            playBtn = CreateMenuTextButton(root.transform, "PlayButton", "Play", 80f);
            customizeBtn = CreateMenuTextButton(root.transform, "CustomizeButton", "Customize", 80f - step);
            shopBtn = CreateMenuTextButton(root.transform, "ShopButton", "Shop", 80f - step * 2f);
            return root;
        }

        static Button CreateMenuTextButton(Transform parent, string name, string label, float y)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(560f, UiTextStyle.MenuFontSize + 36f);

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
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = UiTextStyle.MenuFontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            UiTextStyle.Apply(text);
            textGo.AddComponent<CrispUIText>().SetDesignSize(UiTextStyle.MenuFontSize);
            return button;
        }

        public void OpenCustomize()
        {
            EnsureMainMenuUi();
            EnsureCustomizeUi();
            if (shopMenu != null && shopMenu.IsOpen)
                shopMenu.Close();
            customizeMenu?.Open();
            if (mainMenuRoot != null)
                mainMenuRoot.SetActive(false);
        }

        public void OpenShop()
        {
            EnsureMainMenuUi();
            EnsureShopUi();
            if (customizeMenu != null && customizeMenu.IsOpen)
                customizeMenu.Close();
            shopMenu?.Open();
            if (mainMenuRoot != null)
                mainMenuRoot.SetActive(false);
            // Shop uses bottom wallet — hide global top-right coin while open.
            if (coinHudRoot != null)
                coinHudRoot.SetActive(false);
        }

        public void WireCustomizeButton()
        {
            if (customizeButton == null || customizeWired) return;
            customizeButton.onClick.RemoveListener(OnCustomizeClicked);
            customizeButton.onClick.AddListener(OnCustomizeClicked);
            customizeWired = true;
        }

        public void WireShopButton()
        {
            if (shopButton == null || shopWired) return;
            shopButton.onClick.RemoveListener(OnShopClicked);
            shopButton.onClick.AddListener(OnShopClicked);
            shopWired = true;
        }

        public void WirePlayButton()
        {
            if (playButton == null || playWired) return;
            playButton.onClick.RemoveListener(OnPlayClicked);
            playButton.onClick.AddListener(OnPlayClicked);
            playWired = true;
        }

        void OnCustomizeClicked()
        {
            GameManager.Instance?.OpenCustomize();
        }

        void OnShopClicked()
        {
            GameManager.Instance?.OpenShop();
        }

        void OnPlayClicked()
        {
            GameManager.Instance?.StartGame();
        }

        void FitPhoneUi()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.pixelPerfect = true;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = PlatformRuntime.UiReferenceResolution;
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0.5f;
                    scaler.referencePixelsPerUnit = CrispVisuals.SpritePPU;
                }
            }

            LayoutBestTopLeft();
            LayoutLiveScoreTopCenter();

            if (pauseButton != null)
            {
                var pr = pauseButton.transform as RectTransform;
                if (pr != null)
                {
                    pr.anchorMin = new Vector2(1f, 1f);
                    pr.anchorMax = new Vector2(1f, 1f);
                    pr.pivot = new Vector2(1f, 1f);
                    pr.anchoredPosition = new Vector2(-24f, -16f);
                    pr.sizeDelta = new Vector2(120f, UiTextStyle.MenuFontSize + 36f);
                }
                UiTextStyle.StyleMenuButton(pauseButton);
            }

            // Main menu stack: screen center, same-size centered options.
            if (mainMenuRoot != null)
            {
                var mr = mainMenuRoot.transform as RectTransform;
                if (mr != null)
                {
                    mr.anchorMin = new Vector2(0.5f, 0.5f);
                    mr.anchorMax = new Vector2(0.5f, 0.5f);
                    mr.pivot = new Vector2(0.5f, 0.5f);
                    mr.anchoredPosition = Vector2.zero;
                    mr.sizeDelta = new Vector2(640f, 280f);
                }

                LayoutMenuStack(mainMenuRoot.transform);
            }

            LayoutCenteredMenuButton(resumeButton, 0f);
            LayoutCenteredMenuButton(pauseMenuButton, -(UiTextStyle.MenuFontSize + 40f));
            LayoutCenteredMenuButton(restartButton, -110f);
            LayoutCenteredMenuButton(menuButton, -(UiTextStyle.MenuFontSize + 150f));

            FitLabel(finalScoreText, 900f, UiTextStyle.MenuFontSize + 24f, TextAnchor.MiddleCenter, UiTextStyle.MenuFontSize);
            FitLabel(finalHighScoreText, 900f, UiTextStyle.MenuFontSize + 24f, TextAnchor.MiddleCenter, UiTextStyle.MenuFontSize);
            FitLabel(pauseMessageText, 900f, UiTextStyle.MenuFontSize + 24f, TextAnchor.MiddleCenter, UiTextStyle.MenuFontSize);

            StyleAllMenuButtons();
        }

        void LayoutBestTopLeft()
        {
            if (highScoreText == null) return;
            var rect = highScoreText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            // Compact top-left so it never reaches the center live score.
            rect.anchoredPosition = new Vector2(28f, -18f);
            rect.sizeDelta = new Vector2(280f, 44f);
            highScoreText.horizontalOverflow = HorizontalWrapMode.Overflow;
            highScoreText.verticalOverflow = VerticalWrapMode.Truncate;
            highScoreText.raycastTarget = false;
            UiTextStyle.ApplySized(highScoreText, 32, TextAnchor.UpperLeft);
        }

        void LayoutLiveScoreTopCenter()
        {
            if (scoreText == null) return;
            var rect = scoreText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            // Sit clearly in the middle top; Best stays left of this.
            rect.anchoredPosition = new Vector2(0f, -18f);
            rect.sizeDelta = new Vector2(420f, UiTextStyle.LiveScoreFontSize + 28f);
            scoreText.horizontalOverflow = HorizontalWrapMode.Overflow;
            scoreText.raycastTarget = false;
            UiTextStyle.ApplySized(scoreText, UiTextStyle.LiveScoreFontSize, TextAnchor.UpperCenter);
        }

        void HideMainMenu()
        {
            if (mainMenuRoot != null)
                mainMenuRoot.SetActive(false);

            // Orphan Play/Customize (not under MainMenu) must also stay off.
            if (playButton != null &&
                (mainMenuRoot == null || !playButton.transform.IsChildOf(mainMenuRoot.transform)))
                playButton.gameObject.SetActive(false);

            if (customizeButton != null &&
                (mainMenuRoot == null || !customizeButton.transform.IsChildOf(mainMenuRoot.transform)))
                customizeButton.gameObject.SetActive(false);

            if (shopButton != null &&
                (mainMenuRoot == null || !shopButton.transform.IsChildOf(mainMenuRoot.transform)))
                shopButton.gameObject.SetActive(false);
        }

        static void LayoutMenuStack(Transform menuRoot)
        {
            if (menuRoot == null) return;
            var y = 40f;
            for (var i = 0; i < menuRoot.childCount; i++)
            {
                var child = menuRoot.GetChild(i) as RectTransform;
                if (child == null) continue;
                child.anchorMin = new Vector2(0.5f, 0.5f);
                child.anchorMax = new Vector2(0.5f, 0.5f);
                child.pivot = new Vector2(0.5f, 0.5f);
                child.anchoredPosition = new Vector2(0f, y);
                child.sizeDelta = new Vector2(560f, UiTextStyle.MenuFontSize + 36f);
                y -= UiTextStyle.MenuFontSize + 40f;

                var btn = child.GetComponent<Button>();
                if (btn != null)
                    UiTextStyle.StyleMenuButton(btn);
            }
        }

        static void LayoutCenteredMenuButton(Button button, float y)
        {
            if (button == null) return;
            var rect = button.transform as RectTransform;
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(560f, UiTextStyle.MenuFontSize + 36f);
            UiTextStyle.StyleMenuButton(button);
        }

        static void FitLabel(
            Text label,
            float width,
            float height,
            TextAnchor align,
            int fontSize = -1,
            FontStyle style = FontStyle.Normal)
        {
            if (label == null) return;
            var rect = label.rectTransform;
            var pivot = rect.pivot;
            var anchorMin = rect.anchorMin;
            var anchorMax = rect.anchorMax;
            anchorMin.x = 0.5f;
            anchorMax.x = 0.5f;
            pivot.x = 0.5f;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(width, Mathf.Max(rect.sizeDelta.y, height));
            var pos = rect.anchoredPosition;
            pos.x = 0f;
            rect.anchoredPosition = pos;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.resizeTextForBestFit = false;
            if (fontSize > 0)
                UiTextStyle.ApplySized(label, fontSize, align);
        }

        void EnsureSharpText()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.pixelPerfect = true;

            var labels = GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null) continue;
                if (label.GetComponent<CrispUIText>() == null)
                    label.gameObject.AddComponent<CrispUIText>();
            }
        }

        void Update()
        {
            RefreshHudVisibility();
        }

        public void WireRestartButton()
        {
            if (restartButton == null || restartWired) return;
            restartButton.onClick.RemoveListener(OnRestartClicked);
            restartButton.onClick.AddListener(OnRestartClicked);
            restartWired = true;
        }

        public void WireMenuButton()
        {
            if (menuButton == null || menuWired) return;
            menuButton.onClick.RemoveListener(OnMenuClicked);
            menuButton.onClick.AddListener(OnMenuClicked);
            menuWired = true;
        }

        public void WirePauseButtons()
        {
            if (pauseWired) return;

            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(OnPauseClicked);
                pauseButton.onClick.AddListener(OnPauseClicked);
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(OnResumeClicked);
                resumeButton.onClick.AddListener(OnResumeClicked);
            }

            pauseWired = pauseButton != null || resumeButton != null;
        }

        public void WirePauseMenuButton()
        {
            if (pauseMenuButton == null || pauseMenuWired) return;
            pauseMenuButton.onClick.RemoveListener(OnMenuClicked);
            pauseMenuButton.onClick.AddListener(OnMenuClicked);
            pauseMenuWired = true;
        }

        void RefreshHudVisibility()
        {
            var gm = GameManager.Instance;
            if (pauseButton != null)
            {
                var showPause = gm != null &&
                                (gm.State == GameState.Playing || gm.State == GameState.Falling);
                if (pauseButton.gameObject.activeSelf != showPause)
                    pauseButton.gameObject.SetActive(showPause);
            }

            // Main menu ONLY on Ready. Death uses game-over panel until Menu is pressed.
            if (gm == null || gm.State != GameState.Ready || IsCustomizeOpen || IsShopOpen)
                HideMainMenu();
            else if (mainMenuRoot != null && !mainMenuRoot.activeSelf)
                mainMenuRoot.SetActive(true);

            // Global coin wallet: top-right always except in Shop (shop has its own bottom wallet).
            if (coinHudRoot != null)
            {
                var showCoins = gm != null && !IsShopOpen &&
                                (gm.State == GameState.Ready ||
                                 gm.State == GameState.Playing ||
                                 gm.State == GameState.Falling ||
                                 gm.State == GameState.Paused ||
                                 gm.State == GameState.GameOver ||
                                 IsCustomizeOpen);
                if (coinHudRoot.activeSelf != showCoins)
                    coinHudRoot.SetActive(showCoins);
                if (showCoins)
                    RefreshCoinHud();
            }
        }

        public void ShowReady(int highScore)
        {
            ShowReadyInternal(highScore);
        }

        public void ShowReady(int highScore, string creatureName)
        {
            ShowReadyInternal(highScore);
        }

        void ShowReadyInternal(int highScore)
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (pauseButton != null) pauseButton.gameObject.SetActive(false);

            // Menu: only Best. Live score stays hidden until Play.
            if (scoreText != null)
                scoreText.gameObject.SetActive(false);
            SetHighScore(highScore);
            if (highScoreText != null)
            {
                highScoreText.gameObject.SetActive(true);
                LayoutBestTopLeft();
            }

            // No how-to / loadout dump — world behind the menu is the preview.
            if (centerMessageText != null)
                centerMessageText.gameObject.SetActive(false);
            if (hintText != null)
                hintText.gameObject.SetActive(false);

            if (!IsCustomizeOpen && !IsShopOpen)
            {
                if (mainMenuRoot != null)
                    mainMenuRoot.SetActive(true);
                if (playButton != null)
                    playButton.gameObject.SetActive(true);
                if (customizeButton != null)
                    customizeButton.gameObject.SetActive(true);
                if (shopButton != null)
                    shopButton.gameObject.SetActive(true);
            }

            // Re-apply so Play/Customize always match (white/black, same size).
            FitPhoneUi();
            ApplyTextStyles();
            StyleAllMenuButtons();
            LayoutBestTopLeft();
            RefreshCoinHud();
            if (coinHudRoot != null)
                coinHudRoot.SetActive(true);
        }

        public void ShowPlaying(int score, int highScore)
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (pauseButton != null) pauseButton.gameObject.SetActive(true);
            HideMainMenu();
            if (customizeMenu != null && customizeMenu.IsOpen)
                customizeMenu.Close();
            if (shopMenu != null && shopMenu.IsOpen)
                shopMenu.Close();
            // Keep top-right coins while playing.
            if (coinHudRoot != null)
                coinHudRoot.SetActive(true);
            RefreshCoinHud();

            if (scoreText != null)
                scoreText.gameObject.SetActive(true);
            SetScore(score);
            SetHighScore(highScore);
            if (highScoreText != null)
                highScoreText.gameObject.SetActive(true);

            if (hintText != null)
                hintText.gameObject.SetActive(false);
            if (centerMessageText != null && flashRoutine == null)
            {
                centerMessageText.gameObject.SetActive(false);
                centerMessageText.text = string.Empty;
            }

            LayoutLiveScoreTopCenter();
            LayoutBestTopLeft();
            UiTextStyle.StyleMenuButton(pauseButton);
        }

        public void ShowPaused(bool paused)
        {
            if (pausePanel != null)
                pausePanel.SetActive(paused);

            if (paused)
            {
                // No control hints — just the title + Resume / Menu buttons.
                if (pauseMessageText != null)
                {
                    pauseMessageText.text = "PAUSED";
                    UiTextStyle.ApplySized(pauseMessageText, UiTextStyle.HudFontSize);
                }

                EnsurePauseMenuButton();
                WirePauseMenuButton();
                LayoutCenteredMenuButton(resumeButton, 0f);
                LayoutCenteredMenuButton(pauseMenuButton, -(UiTextStyle.MenuFontSize + 40f));
            }
            else if (flashRoutine == null)
            {
                if (centerMessageText != null)
                {
                    centerMessageText.text = string.Empty;
                    centerMessageText.gameObject.SetActive(false);
                }
            }
        }

        public void FlashCenterMessage(string message, float seconds = 0.85f)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(message, seconds));
        }

        IEnumerator FlashRoutine(string message, float seconds)
        {
            SetCenterMessage(message);
            var end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
                yield return null;

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            {
                if (centerMessageText != null)
                {
                    centerMessageText.text = string.Empty;
                    centerMessageText.gameObject.SetActive(false);
                }
            }

            flashRoutine = null;
        }

        public void UpdateScore(int score)
        {
            if (scoreText != null && !scoreText.gameObject.activeSelf)
                scoreText.gameObject.SetActive(true);
            SetScore(score);
        }

        public void ShowGameOver(int score, int highScore)
        {
            if (centerMessageText != null)
            {
                centerMessageText.text = string.Empty;
                centerMessageText.gameObject.SetActive(false);
            }

            if (pausePanel != null) pausePanel.SetActive(false);
            if (pauseButton != null) pauseButton.gameObject.SetActive(false);
            if (scoreText != null) scoreText.gameObject.SetActive(false);
            if (hintText != null) hintText.gameObject.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (finalScoreText != null) finalScoreText.text = $"Score: {score}";
            if (finalHighScoreText != null) finalHighScoreText.text = $"Best: {highScore}";
            // Death screen only — hide HUD best + main menu until Menu is pressed.
            if (highScoreText != null)
                highScoreText.gameObject.SetActive(false);
            HideMainMenu();
            EnsureGameOverMenuButton();
            WireRestartButton();
            WireMenuButton();
            if (finalScoreText != null)
            {
                finalScoreText.rectTransform.anchoredPosition = new Vector2(0f, 120f);
                UiTextStyle.ApplySized(finalScoreText, UiTextStyle.MenuFontSize);
            }
            if (finalHighScoreText != null)
            {
                finalHighScoreText.rectTransform.anchoredPosition = new Vector2(0f, 50f);
                UiTextStyle.ApplySized(finalHighScoreText, UiTextStyle.MenuFontSize);
            }
            LayoutCenteredMenuButton(restartButton, -110f);
            LayoutCenteredMenuButton(menuButton, -(UiTextStyle.MenuFontSize + 150f));
            if (gameOverPanel != null)
                UiTextStyle.ApplyAllUnder(gameOverPanel.transform);
            // Do NOT StyleAllMenuButtons here — that can re-layout/show Play+Customize.
            UiTextStyle.StyleMenuButton(restartButton);
            UiTextStyle.StyleMenuButton(menuButton);
        }

        void SetScore(int score)
        {
            if (scoreText != null)
                scoreText.text = score.ToString();
        }

        void SetHighScore(int highScore)
        {
            if (highScoreText != null)
                highScoreText.text = $"Best {highScore}";
        }

        void SetCenterMessage(string message)
        {
            if (centerMessageText == null) return;
            if (string.IsNullOrEmpty(message))
            {
                centerMessageText.text = string.Empty;
                centerMessageText.gameObject.SetActive(false);
                return;
            }

            centerMessageText.gameObject.SetActive(true);
            centerMessageText.text = message;
        }

        void OnRestartClicked()
        {
            GameManager.Instance?.Restart();
        }

        void OnMenuClicked()
        {
            GameManager.Instance?.ReturnToMenu();
        }

        void OnPauseClicked()
        {
            GameManager.Instance?.PauseGame(true);
        }

        void OnResumeClicked()
        {
            GameManager.Instance?.PauseGame(false);
        }
    }
}
