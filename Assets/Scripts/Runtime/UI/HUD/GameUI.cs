using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    public class GameUI : MonoBehaviour, IGameUI
    {
        [SerializeField] Text scoreText;
        [SerializeField] Text highScoreText;
        [SerializeField] Text timerText;
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
        [SerializeField] MainMenu mainMenu;
        [SerializeField] CustomizeMenu customizeMenu;
        [SerializeField] ShopMenu shopMenu;
        [SerializeField] GameModeMenu gameModeMenu;
        [SerializeField] SettingsMenu settingsMenu;
        [SerializeField] GameObject coinHudRoot;
        [SerializeField] Text coinHudText;
        [SerializeField] Image coinHudIcon;
        [SerializeField] RaceHud raceHud;

        bool restartWired;
        bool menuWired;
        bool pauseWired;
        bool pauseMenuWired;
        Coroutine flashRoutine;

        public bool IsCustomizeOpen => customizeMenu != null && customizeMenu.IsOpen;
        public bool IsShopOpen => shopMenu != null && shopMenu.IsOpen;
        public bool IsGameModeOpen => gameModeMenu != null && gameModeMenu.IsOpen;
        public bool IsSettingsOpen => settingsMenu != null && settingsMenu.IsOpen;
        public bool IsOverlayOpen => IsCustomizeOpen || IsShopOpen || IsGameModeOpen || IsSettingsOpen;

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
            MainMenu hub = null,
            CustomizeMenu customize = null,
            ShopMenu shop = null,
            GameModeMenu gameMode = null,
            Button menuBtn = null,
            Button pauseMenuBtn = null,
            SettingsMenu settings = null)
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
            mainMenu = hub;
            customizeMenu = customize;
            shopMenu = shop;
            gameModeMenu = gameMode;
            settingsMenu = settings;
            restartWired = false;
            menuWired = false;
            pauseWired = false;
            pauseMenuWired = false;
            WireRestartButton();
            WireMenuButton();
            WirePauseButtons();
            WirePauseMenuButton();
            WireMainMenu();
        }

        void Awake()
        {
            // Stay above blackout veil (sortingOrder 50) so pause / menus remain usable.
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.sortingOrder < 100)
                canvas.sortingOrder = 100;

            EnsureMainMenu();
            EnsureCustomizeUi();
            EnsureShopUi();
            EnsureGameModeUi();
            EnsureSettingsUi();
            EnsureCoinHud();
            EnsureTimerHud();
            EnsureGameOverMenuButton();
            EnsurePauseMenuButton();
            FitPhoneUi();
            EnsureMenuUnderlays();
            EnsureSharpText();
            ApplyTextStyles();
            RefreshCoinHud();
        }

        void Start()
        {
            EnsureMainMenu();
            EnsureCustomizeUi();
            EnsureShopUi();
            EnsureGameModeUi();
            EnsureSettingsUi();
            EnsureCoinHud();
            EnsureTimerHud();
            EnsureGameOverMenuButton();
            EnsurePauseMenuButton();
            FitPhoneUi();
            EnsureMenuUnderlays();
            EnsureSharpText();
            ApplyTextStyles();
            WireRestartButton();
            WireMenuButton();
            WirePauseButtons();
            WirePauseMenuButton();
            WireMainMenu();
            RefreshCoinHud();
        }

        // Coins + Best top-left. Live score is top-center.
        const float HudLeft = 28f;
        const float HudTop = -16f;
        const float DefaultCoinHudIconSize = 64f;
        const float CoinHudGap = 20f;
        const float CoinRowHeight = 80f;
        const float BestRowHeight = 68f;
        const float TimerRowHeight = 60f;

        float ResolveHudCoinSize()
        {
            var settings = CurrencySettings.Load();
            var fromAsset = settings != null ? settings.HudIconSize : DefaultCoinHudIconSize;
            return Mathf.Max(DefaultCoinHudIconSize, fromAsset);
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
                textGo.AddComponent<CrispUIText>().SetDesignSize(UiTextStyle.CoinHudFontSize);
            }

            LayoutCoinHud();
        }

        /// <summary>
        /// Top-left wallet: icon, then the count to its right (never overlapping).
        /// </summary>
        void LayoutCoinHud()
        {
            if (coinHudRoot == null) return;

            var rootRect = coinHudRoot.GetComponent<RectTransform>();
            if (rootRect == null) return;

            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(HudLeft, HudTop);
            rootRect.sizeDelta = new Vector2(480f, CoinRowHeight);
            rootRect.localScale = Vector3.one;

            var iconSize = ResolveHudCoinSize();

            if (coinHudIcon != null)
            {
                var coinRect = coinHudIcon.rectTransform;
                coinRect.anchorMin = new Vector2(0f, 0.5f);
                coinRect.anchorMax = new Vector2(0f, 0.5f);
                coinRect.pivot = new Vector2(0f, 0.5f);
                coinRect.anchoredPosition = Vector2.zero;
                coinRect.sizeDelta = new Vector2(iconSize, iconSize);
                coinRect.localScale = Vector3.one;

                coinHudIcon.sprite = PixelSpriteFactory.CreateCoinSprite();
                coinHudIcon.preserveAspect = true;
                coinHudIcon.raycastTarget = false;
                coinHudIcon.color = Color.white;
                CrispVisuals.MakeSpriteCrisp(coinHudIcon.sprite);
            }

            if (coinHudText != null)
            {
                var tr = coinHudText.rectTransform;
                // Fixed box to the right of the icon — stretch + CrispUIText used to sit on the coin.
                tr.anchorMin = new Vector2(0f, 0.5f);
                tr.anchorMax = new Vector2(0f, 0.5f);
                tr.pivot = new Vector2(0f, 0.5f);
                tr.anchoredPosition = new Vector2(iconSize + CoinHudGap, 0f);
                tr.sizeDelta = new Vector2(340f, CoinRowHeight);
                tr.localScale = Vector3.one;

                coinHudText.alignment = TextAnchor.MiddleLeft;
                coinHudText.horizontalOverflow = HorizontalWrapMode.Wrap;
                coinHudText.verticalOverflow = VerticalWrapMode.Overflow;
                coinHudText.raycastTarget = false;
                UiTextStyle.ApplySized(coinHudText, UiTextStyle.CoinHudFontSize, TextAnchor.MiddleLeft);
                var crisp = coinHudText.GetComponent<CrispUIText>();
                if (crisp != null)
                    crisp.RefreshDesignLayout();
            }
        }

        public void RefreshCoinHud()
        {
            EnsureCoinHud();
            LayoutCoinHud();
            if (coinHudText != null)
            {
                coinHudText.text = SaveService.Coins.ToString();
                UiTextStyle.ApplySized(coinHudText, UiTextStyle.CoinHudFontSize, TextAnchor.MiddleLeft);
            }

            LayoutHudStack();
        }

        /// <summary>Coins + Best top-left. Live score stays top-center.</summary>
        void LayoutHudStack()
        {
            LayoutCoinHud();
            LayoutLiveScoreTopCenter();

            var y = HudTop;
            if (coinHudRoot != null && coinHudRoot.activeSelf)
                y -= CoinRowHeight;

            if (highScoreText != null && highScoreText.gameObject.activeSelf)
            {
                LayoutBestAt(y);
                y -= BestRowHeight;
            }

            if (timerText != null && timerText.gameObject.activeSelf)
                LayoutTimerAt(y);
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
            UiTextStyle.StyleMenuButton(pauseButton);
            UiTextStyle.StyleMenuButton(resumeButton);
            UiTextStyle.StyleMenuButton(pauseMenuButton);
            UiTextStyle.StyleMenuButton(restartButton);
            UiTextStyle.StyleMenuButton(menuButton);
            mainMenu?.Layout();
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

        void EnsureMainMenu()
        {
            if (mainMenu != null && !mainMenu.HasSettings)
            {
                DestroyImmediate(mainMenu.gameObject);
                mainMenu = null;
            }

            if (mainMenu != null)
            {
                mainMenu.Layout();
                WireMainMenu();
                return;
            }

            mainMenu = GetComponentInChildren<MainMenu>(true);
            if (mainMenu != null && !mainMenu.HasSettings)
            {
                DestroyImmediate(mainMenu.gameObject);
                mainMenu = null;
            }

            if (mainMenu != null)
            {
                mainMenu.Layout();
                WireMainMenu();
                return;
            }

            var host = UiHost();
            if (host == null) return;

            // Drop any plain MainMenu root left by older builds (no MainMenu component).
            var orphan = host.Find("MainMenu");
            if (orphan != null)
                DestroyImmediate(orphan.gameObject);

            mainMenu = MainMenu.Create(host);
            WireMainMenu();
        }

        void WireMainMenu()
        {
            if (mainMenu == null) return;
            mainMenu.Rebind(
                () => GameManager.Instance?.StartGame(),
                () => GameManager.Instance?.OpenGameMode(),
                () => GameManager.Instance?.OpenCustomize(),
                () => GameManager.Instance?.OpenShop(),
                () => GameManager.Instance?.OpenSettings());
        }

        Transform UiHost()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            var safe = canvas.transform.Find("SafeArea");
            return safe != null ? safe : canvas.transform;
        }

        void EnsureGameModeUi()
        {
            if (gameModeMenu == null)
                gameModeMenu = GetComponentInChildren<GameModeMenu>(true);

            if (gameModeMenu != null && !gameModeMenu.IsConfigured)
            {
                Destroy(gameModeMenu.gameObject);
                gameModeMenu = null;
            }

            if (gameModeMenu != null && gameModeMenu.NeedsDurationHitBoxFix())
            {
                Destroy(gameModeMenu.gameObject);
                gameModeMenu = null;
            }

            var host = UiHost();
            if (host == null) return;

            if (gameModeMenu == null)
                gameModeMenu = CreatureClimbSceneBuilder.CreateGameModeMenuPublic(host);
        }

        void EnsureCustomizeUi()
        {
            if (customizeMenu == null)
                customizeMenu = GetComponentInChildren<CustomizeMenu>(true);

            var host = UiHost();
            if (host == null) return;

            if (customizeMenu == null)
                customizeMenu = CreatureClimbSceneBuilder.CreateCustomizeMenuPublic(host);
        }

        void EnsureShopUi()
        {
            if (shopMenu == null)
                shopMenu = GetComponentInChildren<ShopMenu>(true);

            var host = UiHost();
            if (host == null) return;

            if (shopMenu == null)
                shopMenu = CreatureClimbSceneBuilder.CreateShopMenuPublic(host);
        }

        void EnsureSettingsUi()
        {
            if (settingsMenu == null)
                settingsMenu = GetComponentInChildren<SettingsMenu>(true);

            var host = UiHost();
            if (host == null) return;

            if (settingsMenu == null)
                settingsMenu = SettingsMenu.Create(host);
        }

        public void OpenCustomize()
        {
            EnsureCustomizeUi();
            shopMenu?.Close();
            gameModeMenu?.Close();
            settingsMenu?.Close();
            EnsureMenuUnderlays();
            customizeMenu?.Open();
            mainMenu?.Hide();
            if (coinHudRoot != null)
                coinHudRoot.SetActive(false);
        }

        public void OpenShop()
        {
            EnsureShopUi();
            customizeMenu?.Close();
            gameModeMenu?.Close();
            settingsMenu?.Close();
            EnsureMenuUnderlays();
            shopMenu?.Open();
            mainMenu?.Hide();
            if (coinHudRoot != null)
                coinHudRoot.SetActive(false);
        }

        public void OpenGameMode()
        {
            EnsureGameModeUi();
            customizeMenu?.Close();
            shopMenu?.Close();
            settingsMenu?.Close();
            EnsureMenuUnderlays();
            gameModeMenu?.Open();
            mainMenu?.Hide();
            if (coinHudRoot != null)
                coinHudRoot.SetActive(false);
        }

        public void OpenSettings()
        {
            EnsureSettingsUi();
            customizeMenu?.Close();
            shopMenu?.Close();
            gameModeMenu?.Close();
            EnsureMenuUnderlays();
            settingsMenu?.Open();
            mainMenu?.Hide();
            if (coinHudRoot != null)
                coinHudRoot.SetActive(false);
        }

        void EnsureMenuUnderlays()
        {
            MenuScreenUnderlay.EnsureVeil(gameOverPanel != null ? gameOverPanel.transform : null, MenuScreenUnderlay.GameOverVeil);
            MenuScreenUnderlay.EnsureVeil(pausePanel != null ? pausePanel.transform : null, MenuScreenUnderlay.PauseVeil);
            if (customizeMenu != null)
                MenuScreenUnderlay.Ensure(customizeMenu.transform);
            if (shopMenu != null)
                MenuScreenUnderlay.Ensure(shopMenu.transform);
            if (gameModeMenu != null)
                MenuScreenUnderlay.Ensure(gameModeMenu.transform);
            if (settingsMenu != null)
                MenuScreenUnderlay.Ensure(settingsMenu.transform);
        }

        void FitPhoneUi()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.pixelPerfect = false;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                    PlatformRuntime.ApplyCanvasScaler(scaler);
            }

            LayoutHudStack();

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

            mainMenu?.Layout();

            LayoutCenteredMenuButton(resumeButton, 0f);
            LayoutCenteredMenuButton(pauseMenuButton, -(UiTextStyle.MenuFontSize + 40f));
            LayoutCenteredMenuButton(restartButton, -110f);
            LayoutCenteredMenuButton(menuButton, -(UiTextStyle.MenuFontSize + 150f));

            FitLabel(finalScoreText, 900f, UiTextStyle.MenuFontSize + 24f, TextAnchor.MiddleCenter, UiTextStyle.MenuFontSize);
            FitLabel(finalHighScoreText, 900f, UiTextStyle.MenuFontSize + 24f, TextAnchor.MiddleCenter, UiTextStyle.MenuFontSize);
            FitLabel(pauseMessageText, 900f, UiTextStyle.MenuFontSize + 24f, TextAnchor.MiddleCenter, UiTextStyle.MenuFontSize);

            StyleAllMenuButtons();
        }

        void LayoutBestTopLeft() => LayoutHudStack();

        void LayoutBestAt(float y)
        {
            if (highScoreText == null) return;
            var rect = highScoreText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(HudLeft, y);
            rect.sizeDelta = new Vector2(420f, BestRowHeight);
            highScoreText.horizontalOverflow = HorizontalWrapMode.Wrap;
            highScoreText.verticalOverflow = VerticalWrapMode.Overflow;
            highScoreText.raycastTarget = false;
            UiTextStyle.ApplySized(highScoreText, UiTextStyle.HudFontSize, TextAnchor.UpperLeft);
        }

        void LayoutTimerTopLeft() => LayoutHudStack();

        void LayoutTimerAt(float y)
        {
            if (timerText == null) return;
            var rect = timerText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(HudLeft, y);
            rect.sizeDelta = new Vector2(420f, TimerRowHeight);
            timerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            timerText.verticalOverflow = VerticalWrapMode.Overflow;
            timerText.raycastTarget = false;
            UiTextStyle.ApplySized(timerText, UiTextStyle.HudFontSize, TextAnchor.UpperLeft);
        }

        void EnsureTimerHud()
        {
            if (timerText != null) return;

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Transform host = canvas.transform;
            var safe = canvas.transform.Find("SafeArea");
            if (safe != null) host = safe;

            var existing = host.Find("TimerText");
            if (existing != null)
            {
                timerText = existing.GetComponent<Text>();
                if (timerText != null)
                {
                    LayoutTimerTopLeft();
                    timerText.gameObject.SetActive(false);
                    return;
                }
            }

            var go = new GameObject("TimerText", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(host, false);
            timerText = go.GetComponent<Text>();
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.text = "1:30";
            timerText.raycastTarget = false;
            if (go.GetComponent<CrispUIText>() == null)
                go.AddComponent<CrispUIText>().SetDesignSize(UiTextStyle.HudFontSize);
            LayoutTimerTopLeft();
            go.SetActive(false);
        }

        int cachedTimerSeconds = int.MinValue;

        public void ShowTimer(bool show)
        {
            EnsureTimerHud();
            if (timerText == null) return;
            timerText.gameObject.SetActive(show);
            if (show)
                LayoutTimerTopLeft();
            else
                cachedTimerSeconds = int.MinValue;
        }

        public void UpdateTimer(float secondsRemaining)
        {
            EnsureTimerHud();
            if (timerText == null) return;
            if (!timerText.gameObject.activeSelf)
                timerText.gameObject.SetActive(true);

            var whole = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
            if (whole == cachedTimerSeconds) return;
            cachedTimerSeconds = whole;
            timerText.text = GameModeRules.FormatTimer(secondsRemaining);
            UiTextStyle.ApplySized(timerText, UiTextStyle.HudFontSize, TextAnchor.UpperLeft);
        }

        void LayoutLiveScoreTopCenter()
        {
            if (scoreText == null) return;
            var rect = scoreText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, HudTop);
            rect.sizeDelta = new Vector2(560f, UiTextStyle.LiveScoreFontSize + 28f);
            rect.localScale = Vector3.one;
            scoreText.horizontalOverflow = HorizontalWrapMode.Wrap;
            scoreText.verticalOverflow = VerticalWrapMode.Overflow;
            scoreText.raycastTarget = false;
            UiTextStyle.ApplySized(scoreText, UiTextStyle.LiveScoreFontSize, TextAnchor.UpperCenter);
            var crisp = scoreText.GetComponent<CrispUIText>();
            if (crisp != null)
                crisp.RefreshDesignLayout();
        }

        void HideMainMenu() => mainMenu?.Hide();

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
                canvas.pixelPerfect = false;

            var labels = GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null) continue;
                if (label.GetComponent<CrispUIText>() == null)
                    label.gameObject.AddComponent<CrispUIText>();
                UiTextStyle.Apply(label);
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

            // Main menu only on Ready. Death uses game-over until Menu is pressed.
            if (gm == null || gm.State != GameState.Ready ||
                IsOverlayOpen)
                HideMainMenu();
            else
                mainMenu?.Show();

            // Gradient overlays sit under this HUD sibling — hide coins so they don't bleed through.
            if (coinHudRoot != null)
            {
                var showCoins = gm != null && !IsOverlayOpen &&
                                (gm.State == GameState.Ready ||
                                 gm.State == GameState.Playing ||
                                 gm.State == GameState.Falling ||
                                 gm.State == GameState.Paused ||
                                 gm.State == GameState.GameOver);
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
            ShowTimer(false);
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

            if (!IsOverlayOpen)
                mainMenu?.Show();

            FitPhoneUi();
            ApplyTextStyles();
            StyleAllMenuButtons();
            LayoutBestTopLeft();
            RefreshCoinHud();
            if (coinHudRoot != null)
                coinHudRoot.SetActive(!IsOverlayOpen);
        }

        public void ShowPlaying(int score, int highScore)
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (pauseButton != null) pauseButton.gameObject.SetActive(true);
            HideMainMenu();
            // Race bar is shown/hidden by GameManager when a race starts.
            // Force-close overlays (even if open flag desynced) so nothing blocks the run.
            if (customizeMenu != null)
            {
                if (customizeMenu.IsOpen) customizeMenu.Close();
            }

            if (shopMenu != null)
            {
                if (shopMenu.IsOpen) shopMenu.Close();
            }

            if (gameModeMenu != null)
            {
                if (gameModeMenu.IsOpen)
                    gameModeMenu.Close();
                else if (gameModeMenu.gameObject != null)
                {
                    // Panel may still be active if open flag desynced.
                    var panel = gameModeMenu.transform;
                    if (panel != null && panel.gameObject.activeSelf)
                        panel.gameObject.SetActive(false);
                }
            }

            if (settingsMenu != null)
            {
                if (settingsMenu.IsOpen)
                    settingsMenu.Close();
                else if (settingsMenu.gameObject != null && settingsMenu.gameObject.activeSelf)
                    settingsMenu.gameObject.SetActive(false);
            }

            // Keep top-right coins while playing.
            if (coinHudRoot != null)
                coinHudRoot.SetActive(true);
            RefreshCoinHud();

            if (scoreText != null)
                scoreText.gameObject.SetActive(true);
            SetScore(score);
            // Race doesn't use high-score tracking on the HUD.
            var gm = GameManager.Instance;
            var racing = gm != null && GameModeRules.IsRace(gm.SelectedMode);
            var timeTrial = gm != null && GameModeRules.IsTimeTrial(gm.SelectedMode);
            if (highScoreText != null)
            {
                if (racing)
                    highScoreText.gameObject.SetActive(false);
                else
                {
                    SetHighScore(highScore);
                    highScoreText.gameObject.SetActive(true);
                }
            }

            if (timeTrial)
            {
                ShowTimer(true);
                UpdateTimer(gm.TimeTrialRemaining);
            }
            else
            {
                ShowTimer(false);
            }

            if (hintText != null)
                hintText.gameObject.SetActive(false);
            if (centerMessageText != null && flashRoutine == null)
            {
                centerMessageText.gameObject.SetActive(false);
                centerMessageText.text = string.Empty;
            }

            LayoutLiveScoreTopCenter();
            LayoutBestTopLeft();
            if (timeTrial)
                LayoutTimerTopLeft();
            UiTextStyle.StyleMenuButton(pauseButton);
        }

        public void ShowPaused(bool paused)
        {
            if (paused)
                EnsureMenuUnderlays();
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

        public void ShowGameOver(int score, int highScore, string title = null)
        {
            if (centerMessageText != null)
            {
                centerMessageText.text = string.Empty;
                centerMessageText.gameObject.SetActive(false);
            }

            if (pausePanel != null) pausePanel.SetActive(false);
            if (pauseButton != null) pauseButton.gameObject.SetActive(false);
            if (scoreText != null) scoreText.gameObject.SetActive(false);
            ShowTimer(false);
            if (hintText != null) hintText.gameObject.SetActive(false);
            EnsureMenuUnderlays();
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (finalScoreText != null)
            {
                finalScoreText.text = string.IsNullOrEmpty(title)
                    ? $"Score: {score}"
                    : title;
            }

            if (finalHighScoreText != null)
            {
                var gm = GameManager.Instance;
                var racing = gm != null && GameModeRules.IsRace(gm.SelectedMode);
                var timeTrial = gm != null && GameModeRules.IsTimeTrial(gm.SelectedMode);
                if (racing)
                    finalHighScoreText.text = $"Height: {score}";
                else if (timeTrial)
                    finalHighScoreText.text = $"Score: {score}   Best: {highScore}";
                else if (string.IsNullOrEmpty(title))
                    finalHighScoreText.text = $"Best: {highScore}";
                else
                    finalHighScoreText.text = $"Height: {score}   Best: {highScore}";
            }

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

            // Keep race bar visible on race end so final positions stay clear.
            if (raceHud != null && RaceController.Instance != null)
                raceHud.Refresh();
        }

        public void ShowRaceHud()
        {
            EnsureRaceHud();
            if (highScoreText != null)
                highScoreText.gameObject.SetActive(false);
            raceHud?.Show();
        }

        public void HideRaceHud()
        {
            raceHud?.Hide();
        }

        void EnsureRaceHud()
        {
            if (raceHud != null) return;

            raceHud = GetComponentInChildren<RaceHud>(true);
            if (raceHud != null) return;

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Transform host = canvas.transform;
            var safe = canvas.transform.Find("SafeArea");
            if (safe != null) host = safe;

            raceHud = RaceHud.Build(host);
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
