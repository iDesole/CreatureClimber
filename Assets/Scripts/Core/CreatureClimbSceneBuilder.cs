using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CreatureClimb
{
    /// <summary>
    /// Builds a fully wired playable scene for mobile (iOS / Google Play).
    /// </summary>
    public static class CreatureClimbSceneBuilder
    {
        public const string RootName = "--- Creature Climb ---";

        /// <summary>
        /// Guarantees a working GameManager with all 4 references.
        /// Rebuilds if anything is missing or half-set-up.
        /// </summary>
        public static GameManager EnsureReady()
        {
            PlatformRuntime.Initialize();

            var existing = Object.FindFirstObjectByType<GameManager>();
            if (existing != null && existing.IsWired)
            {
                existing.EnsureWired();
                existing.ReloadDatabasesAndSelections();
                return existing;
            }

            // Half-built or empty GameManager in the scene — start clean.
            TearDown();
            return Build();
        }

        public static bool BuildIfNeeded()
        {
            var existing = Object.FindFirstObjectByType<GameManager>();
            if (existing != null && existing.IsWired)
                return false;

            EnsureReady();
            return true;
        }

        public static void TearDown()
        {
            // DestroyImmediate so rebuild does not fight deferred Destroy (singleton race).
            var root = GameObject.Find(RootName);
            if (root != null)
                Object.DestroyImmediate(root);

            DestroyAllImmediate<GameManager>();
            DestroyAllImmediate<LeafSpawner>();
            DestroyAllImmediate<PlayerController>();
            DestroyAllImmediate<GameUI>();
            DestroyAllImmediate<BackgroundScroller>();
        }

        static void DestroyAllImmediate<T>() where T : Component
        {
            var items = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                Object.DestroyImmediate(items[i].gameObject);
            }
        }

        public static GameManager Build()
        {
            PlatformRuntime.Initialize();

            var existingRoot = GameObject.Find(RootName);
            if (existingRoot != null)
                Object.Destroy(existingRoot);

            var gameRoot = new GameObject(RootName);

            // Background (loops Background Database entries as you climb)
            var backgroundRoot = new GameObject("Background");
            backgroundRoot.transform.SetParent(gameRoot.transform);
            var backgroundScroller = backgroundRoot.AddComponent<BackgroundScroller>();
            var backgroundDb = BackgroundDatabase.Load();
            backgroundScroller.BindDatabase(backgroundDb);
            backgroundScroller.CreateDefaultLayers(backgroundRoot.transform);

            // Systems host
            var systems = new GameObject("Systems");
            systems.transform.SetParent(gameRoot.transform);

            // Leaves
            var leafContainer = new GameObject("Leaves");
            leafContainer.transform.SetParent(systems.transform);

            var leafTemplateGo = new GameObject("LeafTemplate");
            leafTemplateGo.SetActive(false);
            leafTemplateGo.transform.SetParent(leafContainer.transform);
            var leafRenderer = leafTemplateGo.AddComponent<SpriteRenderer>();
            leafRenderer.sprite = PixelSpriteFactory.CreateLeafSprite();
            leafRenderer.sortingOrder = 2;
            var leafPrefab = leafTemplateGo.AddComponent<Leaf>();

            var leafSpawner = systems.AddComponent<LeafSpawner>();
            leafSpawner.Setup(leafPrefab, leafContainer.transform);

            // Your databases only — never fabricated packs.
            var creatureDb = CreatureDatabase.Load();
            var platformDb = PlatformDatabase.Load();
            if (backgroundDb == null)
                backgroundDb = BackgroundDatabase.Load();
            var creature = creatureDb != null ? creatureDb.DefaultCreature : null;

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(systems.transform);
            var playerRenderer = playerGo.AddComponent<SpriteRenderer>();
            playerRenderer.sortingOrder = 5;
            var player = playerGo.AddComponent<PlayerController>();
            player.Bind(leafSpawner);
            if (creature != null)
                player.ApplyCreature(creature);
            else
                playerRenderer.sprite = PixelSpriteFactory.CreateFrogSprite();

            // UI
            var gameUI = CreateUI(gameRoot.transform);

            // GameManager — Wire AFTER AddComponent (Awake has already run).
            var gameManager = systems.AddComponent<GameManager>();
            gameManager.Wire(leafSpawner, player, gameUI, creatureDb, platformDb, backgroundDb, backgroundScroller);

            if (!gameManager.IsWired)
                Debug.LogError("[CreatureClimb] Build finished but GameManager is still not wired.");

            var camera = Camera.main;
            if (camera != null)
            {
                camera.backgroundColor = new Color(0.12f, 0.22f, 0.28f);
                camera.orthographicSize = PlatformRuntime.RecommendedOrthoSize(camera);
                var camPos = camera.transform.position;
                camera.transform.position = new Vector3(0f, camPos.y, camPos.z);
                var follow = camera.gameObject.GetComponent<CameraFollow>()
                             ?? camera.gameObject.AddComponent<CameraFollow>();
                follow.SetTarget(playerGo.transform);
            }

            leafSpawner.ApplyPhoneLayout();

            return gameManager;
        }

        static GameUI CreateUI(Transform gameRoot)
        {
            var canvasGo = new GameObject("UI");
            canvasGo.transform.SetParent(gameRoot, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            // Snap UI verts to pixels; pairs with CrispUIText for sharp labels.
            canvas.pixelPerfect = true;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = PlatformRuntime.UiReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Balance width/height so type scales with modern phone frames.
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = CrispVisuals.SpritePPU;
            canvasGo.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            // Safe area root — keeps HUD clear of notches / home indicators.
            var safeGo = new GameObject("SafeArea");
            safeGo.transform.SetParent(canvasGo.transform, false);
            var safeRect = safeGo.AddComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;
            safeGo.AddComponent<SafeAreaFitter>();

            // Content width with side margin inside 1080 (keeps text clear of edges).
            const float contentW = 960f;

            // Live score (hidden until Play). Best score (menu HUD).
            var scoreText = CreateText(safeGo.transform, "ScoreText", "0", UiTextStyle.LiveScoreFontSize, TextAnchor.UpperCenter, new Vector2(0, -16), contentW, 100);
            scoreText.gameObject.SetActive(false);

            var highScoreText = CreateText(safeGo.transform, "HighScoreText", "Best 0", 32, TextAnchor.UpperLeft, new Vector2(28, -18), 280, 44);
            highScoreText.alignment = TextAnchor.UpperLeft;

            var pauseButton = CreateIconButton(safeGo.transform, "PauseButton", "II", new Vector2(-24, -16), TextAnchor.UpperRight);
            UiTextStyle.StyleMenuButton(pauseButton);
            pauseButton.gameObject.SetActive(false);

            // Centered menu: Play, Customize — same size, center-aligned.
            var mainMenu = CreateMainMenu(safeGo.transform, out var playButton, out var customizeButton);

            var centerMessage = CreateText(safeGo.transform, "CenterMessage", "", UiTextStyle.HudFontSize, TextAnchor.MiddleCenter, Vector2.zero, contentW, 200);
            centerMessage.gameObject.SetActive(false);

            var hintText = CreateText(safeGo.transform, "HintText", "", UiTextStyle.HudFontSize, TextAnchor.LowerCenter, new Vector2(0, 100), contentW, 120);
            hintText.gameObject.SetActive(false);

            var gameOverPanel = CreatePanel(safeGo.transform, "GameOverPanel", new Color(0f, 0f, 0f, 0.6f));
            var finalScore = CreateText(gameOverPanel.transform, "FinalScore", "Score: 0", UiTextStyle.HudFontSize, TextAnchor.MiddleCenter, new Vector2(0, 120), contentW, 80);
            var finalBest = CreateText(gameOverPanel.transform, "FinalBest", "Best: 0", UiTextStyle.HudFontSize, TextAnchor.MiddleCenter, new Vector2(0, 50), contentW, 80);
            var restartButton = CreateButton(gameOverPanel.transform, "RestartButton", "Try Again", new Vector2(0, -110));
            var menuButton = CreateButton(gameOverPanel.transform, "MenuButton", "Menu", new Vector2(0, -(UiTextStyle.MenuFontSize + 150)));
            UiTextStyle.StyleMenuButton(restartButton);
            UiTextStyle.StyleMenuButton(menuButton);
            gameOverPanel.SetActive(false);

            var pausePanel = CreatePanel(safeGo.transform, "PausePanel", new Color(0f, 0f, 0f, 0.55f));
            var pauseMessage = CreateText(pausePanel.transform, "PauseMessage", "PAUSED", UiTextStyle.HudFontSize, TextAnchor.MiddleCenter, new Vector2(0, 100), contentW, 80);
            var resumeButton = CreateButton(pausePanel.transform, "ResumeButton", "Resume", new Vector2(0, 0));
            var pauseMenuButton = CreateButton(pausePanel.transform, "PauseMenuButton", "Menu", new Vector2(0, -(UiTextStyle.MenuFontSize + 40)));
            UiTextStyle.StyleMenuButton(resumeButton);
            UiTextStyle.StyleMenuButton(pauseMenuButton);
            pausePanel.SetActive(false);

            var customizeMenu = CreateCustomizeMenu(safeGo.transform);
            var shopMenu = CreateShopMenu(safeGo.transform);
            var shopButton = mainMenu.transform.Find("ShopButton")?.GetComponent<Button>();

            var gameUI = canvasGo.AddComponent<GameUI>();
            gameUI.Setup(
                scoreText,
                highScoreText,
                centerMessage,
                gameOverPanel,
                finalScore,
                finalBest,
                restartButton,
                pausePanel,
                pauseMessage,
                hintText,
                pauseButton,
                resumeButton,
                customizeButton,
                customizeMenu,
                playButton,
                mainMenu,
                menuButton,
                pauseMenuButton,
                shopButton,
                shopMenu);
            return gameUI;
        }

        static GameObject CreateMainMenu(Transform parent, out Button playButton, out Button customizeButton)
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
            playButton = CreateMenuTextButton(root.transform, "PlayButton", "Play", 80f);
            customizeButton = CreateMenuTextButton(root.transform, "CustomizeButton", "Customize", 80f - step);
            CreateMenuTextButton(root.transform, "ShopButton", "Shop", 80f - step * 2f);
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

            var text = CreateText(go.transform, "Label", label, UiTextStyle.MenuFontSize, TextAnchor.MiddleCenter, Vector2.zero, 560, UiTextStyle.MenuFontSize + 36);
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.raycastTarget = false;
            UiTextStyle.Apply(text);
            return button;
        }

        /// <summary>Used by GameUI to inject the roster menu into older scenes.</summary>
        public static CustomizeMenu CreateCustomizeMenuPublic(Transform parent) =>
            CreateCustomizeMenu(parent);

        public static ShopMenu CreateShopMenuPublic(Transform parent) =>
            CreateShopMenu(parent);

        static ShopMenu CreateShopMenu(Transform parent)
        {
            var panel = CreatePanel(parent, "ShopPanel", new Color(0.04f, 0.05f, 0.08f, 0.94f));
            panel.transform.SetAsLastSibling();

            var title = CreateText(panel.transform, "Title", "SHOP · CHARACTERS", UiTextStyle.MenuFontSize, TextAnchor.UpperCenter, new Vector2(0, -36), 960, 80);
            var subtitle = CreateText(panel.transform, "Subtitle", "Tap an item to buy", UiTextStyle.MenuFontSize, TextAnchor.UpperCenter, new Vector2(0, -110), 960, 70);

            // Wallet bottom-center inside the shop (global coin HUD hides while shop is open).
            var walletGo = new GameObject("Wallet", typeof(RectTransform));
            walletGo.transform.SetParent(panel.transform, false);
            var walletRect = walletGo.GetComponent<RectTransform>();
            walletRect.anchorMin = new Vector2(0.5f, 0f);
            walletRect.anchorMax = new Vector2(0.5f, 0f);
            walletRect.pivot = new Vector2(0.5f, 0f);
            walletRect.anchoredPosition = new Vector2(0f, 160f);
            walletRect.sizeDelta = new Vector2(320f, 56f);

            var coinGo = new GameObject("Coin", typeof(RectTransform), typeof(Image));
            coinGo.transform.SetParent(walletGo.transform, false);
            var coinRect = coinGo.GetComponent<RectTransform>();
            coinRect.anchorMin = new Vector2(0.5f, 0.5f);
            coinRect.anchorMax = new Vector2(0.5f, 0.5f);
            coinRect.pivot = new Vector2(1f, 0.5f);
            coinRect.anchoredPosition = new Vector2(-8f, 0f);
            var shopCoinSize = 40f;
            var currency = CurrencySettings.Load();
            if (currency != null)
                shopCoinSize = currency.UiIconSize;
            coinRect.sizeDelta = new Vector2(shopCoinSize, shopCoinSize);
            var coinImg = coinGo.GetComponent<Image>();
            coinImg.sprite = PixelSpriteFactory.CreateCoinSprite();
            coinImg.preserveAspect = true;
            coinImg.raycastTarget = false;

            var walletLabel = CreateText(walletGo.transform, "WalletText", "0", UiTextStyle.MenuFontSize, TextAnchor.MiddleLeft, new Vector2(8, 0), 200, 56);
            walletLabel.alignment = TextAnchor.MiddleLeft;
            var wr = walletLabel.rectTransform;
            wr.anchorMin = new Vector2(0.5f, 0.5f);
            wr.anchorMax = new Vector2(0.5f, 0.5f);
            wr.pivot = new Vector2(0f, 0.5f);
            wr.anchoredPosition = new Vector2(8f, 0f);

            var tabsGo = new GameObject("Tabs", typeof(RectTransform));
            tabsGo.transform.SetParent(panel.transform, false);
            var tabsRect = tabsGo.GetComponent<RectTransform>();
            tabsRect.anchorMin = new Vector2(0.5f, 1f);
            tabsRect.anchorMax = new Vector2(0.5f, 1f);
            tabsRect.pivot = new Vector2(0.5f, 1f);
            tabsRect.anchoredPosition = new Vector2(0f, -230f);
            tabsRect.sizeDelta = new Vector2(960f, 80f);
            var tabsLayout = tabsGo.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 16f;
            tabsLayout.childAlignment = TextAnchor.MiddleCenter;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = true;

            var charTab = CreateTabButton(tabsGo.transform, "CharactersTab", "Characters");
            var platTab = CreateTabButton(tabsGo.transform, "PlatformsTab", "Platforms");
            var stageTab = CreateTabButton(tabsGo.transform, "StageTab", "Stage");

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Mask));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRectTf = scrollGo.GetComponent<RectTransform>();
            scrollRectTf.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRectTf.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRectTf.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTf.anchoredPosition = new Vector2(0f, -20f);
            scrollRectTf.sizeDelta = new Vector2(1000f, 1180f);
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.25f);
            scrollImg.raycastTarget = true;
            var mask = scrollGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(168f, 200f);
            grid.spacing = new Vector2(16f, 16f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            grid.padding = new RectOffset(20, 20, 20, 20);

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = scrollRectTf;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var status = CreateText(panel.transform, "StatusLabel", "", UiTextStyle.MenuFontSize, TextAnchor.LowerCenter, new Vector2(0, 140), 960, 80);

            var closeBtn = CreateButton(panel.transform, "CloseButton", "Back", new Vector2(0, -820));
            UiTextStyle.StyleMenuButton(closeBtn);
            var closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 48f);
            closeRect.sizeDelta = new Vector2(360f, 100f);

            var menu = panel.AddComponent<ShopMenu>();
            menu.Setup(
                panel,
                title,
                subtitle,
                walletLabel,
                coinImg,
                charTab,
                platTab,
                stageTab,
                closeBtn,
                contentRect,
                grid,
                status);
            panel.SetActive(false);
            return menu;
        }

        static CustomizeMenu CreateCustomizeMenu(Transform parent)
        {
            var panel = CreatePanel(parent, "CustomizePanel", new Color(0.04f, 0.05f, 0.08f, 0.94f));
            panel.transform.SetAsLastSibling();

            var title = CreateText(panel.transform, "Title", "CHARACTERS", UiTextStyle.MenuFontSize, TextAnchor.UpperCenter, new Vector2(0, -36), 960, 80);
            var subtitle = CreateText(panel.transform, "Subtitle", "Pick who you climb as", UiTextStyle.MenuFontSize, TextAnchor.UpperCenter, new Vector2(0, -110), 960, 70);

            // Tab row
            var tabsGo = new GameObject("Tabs", typeof(RectTransform));
            tabsGo.transform.SetParent(panel.transform, false);
            var tabsRect = tabsGo.GetComponent<RectTransform>();
            tabsRect.anchorMin = new Vector2(0.5f, 1f);
            tabsRect.anchorMax = new Vector2(0.5f, 1f);
            tabsRect.pivot = new Vector2(0.5f, 1f);
            tabsRect.anchoredPosition = new Vector2(0f, -230f);
            tabsRect.sizeDelta = new Vector2(960f, 80f);
            var tabsLayout = tabsGo.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 16f;
            tabsLayout.childAlignment = TextAnchor.MiddleCenter;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = true;

            var charTab = CreateTabButton(tabsGo.transform, "CharactersTab", "Characters");
            var platTab = CreateTabButton(tabsGo.transform, "PlatformsTab", "Platforms");
            var bgTab = CreateTabButton(tabsGo.transform, "BackgroundsTab", "Stage");

            // Scroll + grid
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Mask));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRectTf = scrollGo.GetComponent<RectTransform>();
            scrollRectTf.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRectTf.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRectTf.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTf.anchoredPosition = new Vector2(0f, -20f);
            scrollRectTf.sizeDelta = new Vector2(1000f, 1180f);
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.25f);
            scrollImg.raycastTarget = true;
            var mask = scrollGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(168f, 188f);
            grid.spacing = new Vector2(16f, 16f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            grid.padding = new RectOffset(20, 20, 20, 20);

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = scrollRectTf;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var selection = CreateText(panel.transform, "SelectionLabel", "Selected: —", UiTextStyle.MenuFontSize, TextAnchor.LowerCenter, new Vector2(0, 140), 960, 80);

            var closeBtn = CreateButton(panel.transform, "CloseButton", "Back", new Vector2(0, -820));
            UiTextStyle.StyleMenuButton(closeBtn);
            var closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 48f);
            closeRect.sizeDelta = new Vector2(360f, 100f);

            var menu = panel.AddComponent<CustomizeMenu>();
            menu.Setup(
                panel,
                title,
                subtitle,
                charTab,
                platTab,
                bgTab,
                closeBtn,
                contentRect,
                grid,
                selection);
            panel.SetActive(false);
            return menu;
        }

        static Button CreateTabButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 72f;
            le.flexibleWidth = 1f;

            var img = go.GetComponent<Image>();
            img.color = UiTextStyle.ButtonPlateAlt;
            img.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;

            var text = CreateText(go.transform, "Label", label, 28, TextAnchor.MiddleCenter, Vector2.zero, 280, 60);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.raycastTarget = false;
            UiTextStyle.ApplyCustomize(text);
            return button;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            // New Input System module works with touch + mouse + gamepad UI.
            if (eventSystemGo.GetComponent<InputSystemUIInputModule>() == null)
                eventSystemGo.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemGo.AddComponent<StandaloneInputModule>();
#endif
        }

        static Text CreateText(
            Transform parent,
            string name,
            string text,
            int fontSize,
            TextAnchor anchor,
            Vector2 anchoredPosition,
            float width = 960f,
            float height = 140f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = AnchorToVector(anchor);
            rect.anchorMax = AnchorToVector(anchor);
            rect.pivot = AnchorToVector(anchor);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = anchoredPosition;

            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = anchor;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.resizeTextForBestFit = false;
            UiTextStyle.Apply(label);
            // Rasterize at screen resolution so CanvasScaler does not blur glyphs.
            var crisp = go.AddComponent<CrispUIText>();
            crisp.SetDesignSize(fontSize);
            return label;
        }

        static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        static Button CreateIconButton(Transform parent, string name, string label, Vector2 anchoredPosition, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var pivot = AnchorToVector(anchor);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = pivot;
            rect.anchorMax = pivot;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(96, 96);
            rect.anchoredPosition = anchoredPosition;

            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            var text = CreateText(go.transform, "Label", label, UiTextStyle.MenuFontSize, TextAnchor.MiddleCenter, Vector2.zero);
            text.rectTransform.sizeDelta = new Vector2(120, UiTextStyle.MenuFontSize + 36);
            text.raycastTarget = false;
            UiTextStyle.Apply(text);
            var crisp = text.GetComponent<CrispUIText>();
            if (crisp != null) crisp.RefreshDesignLayout();
            button.transition = Selectable.Transition.None;
            UiTextStyle.SetButtonLabelSize(button, UiTextStyle.MenuFontSize);

            return button;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(560, UiTextStyle.MenuFontSize + 36);
            rect.anchoredPosition = anchoredPosition;

            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            var text = CreateText(go.transform, "Label", label, UiTextStyle.MenuFontSize, TextAnchor.MiddleCenter, Vector2.zero);
            text.rectTransform.sizeDelta = new Vector2(560, UiTextStyle.MenuFontSize + 36);
            text.raycastTarget = false;
            UiTextStyle.Apply(text);
            var crisp = text.GetComponent<CrispUIText>();
            if (crisp != null) crisp.RefreshDesignLayout();
            UiTextStyle.StyleMenuButton(button);

            return button;
        }

        static Vector2 AnchorToVector(TextAnchor anchor)
        {
            return anchor switch
            {
                TextAnchor.UpperLeft => new Vector2(0f, 1f),
                TextAnchor.UpperCenter => new Vector2(0.5f, 1f),
                TextAnchor.UpperRight => new Vector2(1f, 1f),
                TextAnchor.MiddleLeft => new Vector2(0f, 0.5f),
                TextAnchor.MiddleCenter => new Vector2(0.5f, 0.5f),
                TextAnchor.MiddleRight => new Vector2(1f, 0.5f),
                TextAnchor.LowerLeft => new Vector2(0f, 0f),
                TextAnchor.LowerCenter => new Vector2(0.5f, 0f),
                TextAnchor.LowerRight => new Vector2(1f, 0f),
                _ => new Vector2(0.5f, 0.5f)
            };
        }
    }
}
