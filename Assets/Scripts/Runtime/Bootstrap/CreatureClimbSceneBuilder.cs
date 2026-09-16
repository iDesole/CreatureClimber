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
            // Pixel-perfect + point-filtered type looked static. Smooth overlay.
            canvas.pixelPerfect = false;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            PlatformRuntime.ApplyCanvasScaler(scaler);
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

            // Content width with side margin inside the 1080-wide 20:9 frame.
            const float contentW = 960f;

            // Live score (hidden until Play). Best score (menu HUD).
            var scoreText = CreateText(safeGo.transform, "ScoreText", "0", UiTextStyle.LiveScoreFontSize, TextAnchor.UpperCenter, new Vector2(0, -16), 560, 120);
            scoreText.gameObject.SetActive(false);

            var highScoreText = CreateText(safeGo.transform, "HighScoreText", "Best 0", UiTextStyle.HudFontSize, TextAnchor.UpperLeft, new Vector2(28, -90), 420, 56);
            highScoreText.alignment = TextAnchor.UpperLeft;

            var pauseButton = CreateIconButton(safeGo.transform, "PauseButton", "II", new Vector2(-24, -16), TextAnchor.UpperRight);
            UiTextStyle.StyleMenuButton(pauseButton);
            pauseButton.gameObject.SetActive(false);

            var mainMenu = MainMenu.Create(safeGo.transform);

            var centerMessage = CreateText(safeGo.transform, "CenterMessage", "", UiTextStyle.HudFontSize, TextAnchor.MiddleCenter, Vector2.zero, contentW, 200);
            centerMessage.gameObject.SetActive(false);

            var hintText = CreateText(safeGo.transform, "HintText", "", UiTextStyle.HudFontSize, TextAnchor.LowerCenter, new Vector2(0, 100), contentW, 120);
            hintText.gameObject.SetActive(false);

            var gameOverPanel = CreateVeilPanel(safeGo.transform, "GameOverPanel", MenuScreenUnderlay.GameOverVeil);
            var finalScore = CreateText(gameOverPanel.transform, "FinalScore", "Score: 0", UiTextStyle.HudFontSize, TextAnchor.MiddleCenter, new Vector2(0, 120), contentW, 80);
            var finalBest = CreateText(gameOverPanel.transform, "FinalBest", "Best: 0", UiTextStyle.HudFontSize, TextAnchor.MiddleCenter, new Vector2(0, 50), contentW, 80);
            var restartButton = CreateButton(gameOverPanel.transform, "RestartButton", "Try Again", new Vector2(0, -110));
            var menuButton = CreateButton(gameOverPanel.transform, "MenuButton", "Menu", new Vector2(0, -(UiTextStyle.MenuFontSize + 150)));
            UiTextStyle.StyleMenuButton(restartButton);
            UiTextStyle.StyleMenuButton(menuButton);
            gameOverPanel.SetActive(false);

            var pausePanel = CreateVeilPanel(safeGo.transform, "PausePanel", MenuScreenUnderlay.PauseVeil);
            var pauseMessage = CreateText(pausePanel.transform, "PauseMessage", "PAUSED", UiTextStyle.HudFontSize, TextAnchor.MiddleCenter, new Vector2(0, 100), contentW, 80);
            var resumeButton = CreateButton(pausePanel.transform, "ResumeButton", "Resume", new Vector2(0, 0));
            var pauseMenuButton = CreateButton(pausePanel.transform, "PauseMenuButton", "Menu", new Vector2(0, -(UiTextStyle.MenuFontSize + 40)));
            UiTextStyle.StyleMenuButton(resumeButton);
            UiTextStyle.StyleMenuButton(pauseMenuButton);
            pausePanel.SetActive(false);

            var customizeMenu = CreateCustomizeMenu(safeGo.transform);
            var shopMenu = CreateShopMenu(safeGo.transform);
            var gameModeMenu = CreateGameModeMenu(safeGo.transform);
            var settingsMenu = SettingsMenu.Create(safeGo.transform);

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
                mainMenu,
                customizeMenu,
                shopMenu,
                gameModeMenu,
                menuButton,
                pauseMenuButton,
                settingsMenu);
            return gameUI;
        }

        /// <summary>Used by GameUI to inject the mode menu into older scenes.</summary>
        public static GameModeMenu CreateGameModeMenuPublic(Transform parent) =>
            CreateGameModeMenu(parent);

        static GameModeMenu CreateGameModeMenu(Transform parent)
        {
            var panel = CreatePanel(parent, "GameModePanel");
            panel.transform.SetAsLastSibling();

            var title = CreateText(panel.transform, "Title", "GAME MODE", UiTextStyle.MenuFontSize, TextAnchor.UpperCenter, new Vector2(0, -60), 960, 80);
            var subtitle = CreateText(
                panel.transform,
                "Subtitle",
                GameModeRules.Description(GameMode.Classic, Difficulty.Easy),
                28,
                TextAnchor.UpperCenter,
                new Vector2(0, -150),
                920,
                100);

            // 2×3 grid (final positions every refresh in GameModeMenu — mobile layout).
            const float cellW = 320f;
            const float cellH = 100f;
            const float colGap = 20f;
            const float rowGap = 14f;
            const float topY = 120f;
            var colX = (cellW + colGap) * 0.5f;
            var rowStep = cellH + rowGap;
            var classic = CreateButton(panel.transform, "ClassicButton", "Classic", new Vector2(-colX, topY));
            var blackout = CreateButton(panel.transform, "BlackoutButton", "Blackout", new Vector2(colX, topY));
            var race = CreateButton(panel.transform, "RaceButton", "Race", new Vector2(-colX, topY - rowStep));
            var combatant = CreateButton(panel.transform, "CombatantButton", "Combatant", new Vector2(colX, topY - rowStep));
            var timeTrial = CreateButton(panel.transform, "TimeTrialButton", "Time Trial", new Vector2(-colX, topY - rowStep * 2f));
            var tempo = CreateButton(panel.transform, "TempoButton", "Tempo", new Vector2(colX, topY - rowStep * 2f));
            void SizeModeCell(Button b)
            {
                if (b == null) return;
                var r = b.GetComponent<RectTransform>();
                if (r != null) r.sizeDelta = new Vector2(cellW, cellH);
            }
            SizeModeCell(classic);
            SizeModeCell(blackout);
            SizeModeCell(race);
            SizeModeCell(combatant);
            SizeModeCell(timeTrial);
            SizeModeCell(tempo);
            UiTextStyle.StyleMenuButton(classic);
            UiTextStyle.StyleMenuButton(blackout);
            UiTextStyle.StyleMenuButton(race);
            UiTextStyle.StyleMenuButton(combatant);
            UiTextStyle.StyleMenuButton(timeTrial);
            UiTextStyle.StyleMenuButton(tempo);

            GameModeMenu.BuildDifficultySlide(
                panel.transform,
                out var difficultySlide,
                out var trackMid,
                out var trackLeft,
                out var trackRight,
                out var slideKnob,
                out var slideLabel);

            // Race opponent picker (shown only when Race is selected).
            // Layout: large centered creature art, name under it between < > arrows.
            var oppRow = new GameObject("OpponentRow", typeof(RectTransform));
            oppRow.transform.SetParent(panel.transform, false);
            var oppRect = oppRow.GetComponent<RectTransform>();
            oppRect.anchorMin = new Vector2(0.5f, 0f);
            oppRect.anchorMax = new Vector2(0.5f, 0f);
            oppRect.pivot = new Vector2(0.5f, 0f);
            oppRect.anchoredPosition = new Vector2(0f, 260f);
            oppRect.sizeDelta = new Vector2(720f, 280f);

            // Icon first (top/center) — phone-scaled portrait.
            var iconGo = new GameObject("OppIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(oppRow.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(0f, 70f);
            iconRt.sizeDelta = new Vector2(180f, 180f);
            var oppIcon = iconGo.GetComponent<Image>();
            oppIcon.preserveAspect = true;
            oppIcon.raycastTarget = false;

            // Name under icon; arrows flank the name.
            const float nameY = -70f;
            var prev = CreateButton(oppRow.transform, "OppPrev", "<", new Vector2(-220f, nameY));
            var next = CreateButton(oppRow.transform, "OppNext", ">", new Vector2(220f, nameY));
            UiTextStyle.StyleMenuButton(prev);
            UiTextStyle.StyleMenuButton(next);
            prev.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 80f);
            next.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 80f);

            var oppLabel = CreateText(
                oppRow.transform,
                "OppLabel",
                "Opponent",
                32,
                TextAnchor.MiddleCenter,
                new Vector2(0f, nameY),
                360,
                64);

            // Time Trial duration picker (shown only when Time Trial is selected).
            // Must use compact hit boxes — CreateButton defaults to 560px wide menu buttons
            // which would stack and make the last option (200s) steal every click.
            var durRow = new GameObject("DurationRow", typeof(RectTransform));
            durRow.transform.SetParent(panel.transform, false);
            var durRect = durRow.GetComponent<RectTransform>();
            durRect.anchorMin = new Vector2(0.5f, 0f);
            durRect.anchorMax = new Vector2(0.5f, 0f);
            durRect.pivot = new Vector2(0.5f, 0f);
            // Y from panel bottom — matches GameModeMenu.ContextRowY.
            durRect.anchoredPosition = new Vector2(0f, 300f);
            durRect.sizeDelta = new Vector2(720f, 120f);

            var dur90 = CreateDurationButton(durRow.transform, "Dur90", "90s", -220f);
            var dur120 = CreateDurationButton(durRow.transform, "Dur120", "120s", 0f);
            var dur200 = CreateDurationButton(durRow.transform, "Dur200", "200s", 220f);

            var closeBtn = CreateButton(panel.transform, "CloseButton", "Back", new Vector2(0, -820));
            RosterScreenLayout.PlaceBackChevron(closeBtn.GetComponent<RectTransform>());
            RosterScreenLayout.StyleBackChevron(closeBtn);

            var menu = panel.AddComponent<GameModeMenu>();
            menu.Setup(
                panel,
                title,
                subtitle,
                classic,
                blackout,
                race,
                combatant,
                timeTrial,
                closeBtn,
                difficultySlide,
                trackMid,
                trackLeft,
                trackRight,
                slideKnob,
                slideLabel,
                oppRow,
                prev,
                next,
                oppIcon,
                oppLabel,
                durRow,
                dur90,
                dur120,
                dur200,
                tempo);
            oppRow.SetActive(false);
            durRow.SetActive(false);
            panel.SetActive(false);
            return menu;
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
            var panel = CreatePanel(parent, "ShopPanel");
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
            walletRect.sizeDelta = new Vector2(960f, RosterScreenLayout.ShopWalletHeight());

            var coinGo = new GameObject("Coin", typeof(RectTransform), typeof(Image));
            coinGo.transform.SetParent(walletGo.transform, false);
            var coinRect = coinGo.GetComponent<RectTransform>();
            coinRect.anchorMin = new Vector2(0.5f, 0.5f);
            coinRect.anchorMax = new Vector2(0.5f, 0.5f);
            coinRect.pivot = new Vector2(1f, 0.5f);
            coinRect.anchoredPosition = new Vector2(-RosterScreenLayout.Unit, 0f);
            var shopCoinSize = RosterScreenLayout.ShopWalletIconSize();
            coinRect.sizeDelta = new Vector2(shopCoinSize, shopCoinSize);
            var coinImg = coinGo.GetComponent<Image>();
            coinImg.sprite = PixelSpriteFactory.CreateCoinSprite();
            coinImg.preserveAspect = true;
            coinImg.raycastTarget = false;

            var walletLabel = CreateText(walletGo.transform, "WalletText", "0", RosterScreenLayout.ShopWalletFont, TextAnchor.MiddleLeft, new Vector2(RosterScreenLayout.Unit, 0), 480, RosterScreenLayout.ShopWalletHeight());
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
            RosterScreenLayout.PlaceBackChevron(closeBtn.GetComponent<RectTransform>());
            RosterScreenLayout.StyleBackChevron(closeBtn);

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
            var panel = CreatePanel(parent, "CustomizePanel");
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
            RosterScreenLayout.PlaceBackChevron(closeBtn.GetComponent<RectTransform>());
            RosterScreenLayout.StyleBackChevron(closeBtn);

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

            var text = CreateText(go.transform, "Label", label, UiTextStyle.CaptionFontSize, TextAnchor.MiddleCenter, Vector2.zero, 280, 68);
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

        static GameObject CreatePanel(Transform parent, string name)
        {
            var go = NewStretchPanel(parent, name);
            MenuScreenUnderlay.Ensure(go.transform);
            return go;
        }

        static GameObject CreateVeilPanel(Transform parent, string name, Color veil)
        {
            var go = NewStretchPanel(parent, name);
            MenuScreenUnderlay.EnsureVeil(go.transform, veil);
            return go;
        }

        static GameObject NewStretchPanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

        /// <summary>
        /// Compact side-by-side duration chips (90s / 120s / 200s).
        /// Must not use full-width menu button hit boxes or they steal each other's clicks.
        /// </summary>
        static Button CreateDurationButton(Transform parent, string name, string label, float x)
        {
            const float w = 200f;
            const float h = 100f;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(w, h);

            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.12f);
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
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
            text.fontSize = UiTextStyle.CaptionFontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            UiTextStyle.Apply(text);
            textGo.AddComponent<CrispUIText>().SetDesignSize(UiTextStyle.CaptionFontSize);

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
