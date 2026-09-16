using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Shop roster (like Customize): Characters / Platforms / Stage with coin prices
    /// from each database's shopPrice field.
    /// </summary>
    public class ShopMenu : MonoBehaviour
    {
        public enum Tab
        {
            Characters,
            Platforms,
            Stage
        }

        [SerializeField] GameObject root;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] Text walletText;
        [SerializeField] Image walletCoin;
        [SerializeField] Button charactersTab;
        [SerializeField] Button platformsTab;
        [SerializeField] Button stageTab;
        [SerializeField] Button closeButton;
        [SerializeField] RectTransform gridContent;
        [SerializeField] GridLayoutGroup grid;
        [SerializeField] Text statusLabel;

        readonly List<RosterCell> cells = new();
        readonly List<int> pageItems = new();
        Tab currentTab = Tab.Characters;
        bool open;
        Vector2 lastScreen;
        int page;

        public bool IsOpen => open;

        public void Setup(
            GameObject panelRoot,
            Text title,
            Text subtitle,
            Text wallet,
            Image coin,
            Button charTab,
            Button platTab,
            Button stgTab,
            Button close,
            RectTransform content,
            GridLayoutGroup layout,
            Text status)
        {
            root = panelRoot;
            titleText = title;
            subtitleText = subtitle;
            walletText = wallet;
            walletCoin = coin;
            charactersTab = charTab;
            platformsTab = platTab;
            stageTab = stgTab;
            closeButton = close;
            gridContent = content;
            grid = layout;
            statusLabel = status;

            WireTabs();
            RosterScreenLayout.EnsureChevrons(root != null ? root.transform : null, PrevPage, NextPage);
            if (walletCoin != null)
            {
                walletCoin.sprite = PixelSpriteFactory.CreateCoinSprite();
                walletCoin.preserveAspect = true;
                CrispVisuals.MakeSpriteCrisp(walletCoin.sprite);
                var s = RosterScreenLayout.ShopWalletIconSize();
                walletCoin.rectTransform.sizeDelta = new Vector2(s, s);
            }

            if (root != null)
                root.SetActive(false);
            open = false;
        }

        void WireTabs()
        {
            if (charactersTab != null)
            {
                charactersTab.onClick.RemoveAllListeners();
                charactersTab.onClick.AddListener(() => ShowTab(Tab.Characters));
            }

            if (platformsTab != null)
            {
                platformsTab.onClick.RemoveAllListeners();
                platformsTab.onClick.AddListener(() => ShowTab(Tab.Platforms));
            }

            if (stageTab != null)
            {
                stageTab.onClick.RemoveAllListeners();
                stageTab.onClick.AddListener(() => ShowTab(Tab.Stage));
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
        }

        public void Open(Tab tab = Tab.Characters)
        {
            if (root == null) return;
            open = true;
            root.SetActive(true);
            LayoutScreen();
            RefreshWallet();
            ShowTab(tab);
            LayoutScreen();
            FeedbackService.UiSelect();
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            if (root != null)
                root.SetActive(false);
            FeedbackService.UiSelect();
            GameManager.Instance?.OnShopClosed();
        }

        void LayoutScreen()
        {
            if (root == null) return;
            RosterScreenLayout.LayoutChrome(root.transform, shop: true);
            var scroll = root.transform.Find("Scroll") as RectTransform;
            var cell = RosterScreenLayout.FitGrid(grid, scroll, shop: true);
            RosterScreenLayout.ApplyChromeFonts(root.transform, shop: true);
            RosterScreenLayout.PlaceChevrons(root.transform);
            for (var i = 0; i < cells.Count; i++)
                cells[i]?.ApplyLayout(cell);
            lastScreen = new Vector2(Screen.width, Screen.height);
        }

        void ShowTab(Tab tab)
        {
            currentTab = tab;
            page = 0;
            RefreshTabStyles();
            RebuildGrid();
        }

        void PrevPage()
        {
            if (page <= 0) return;
            page--;
            RebuildGrid();
            FeedbackService.UiSelect();
        }

        void NextPage()
        {
            page++;
            RebuildGrid();
            FeedbackService.UiSelect();
        }

        void RefreshTabStyles()
        {
            StyleTab(charactersTab, currentTab == Tab.Characters);
            StyleTab(platformsTab, currentTab == Tab.Platforms);
            StyleTab(stageTab, currentTab == Tab.Stage);

            if (titleText != null)
            {
                titleText.text = currentTab switch
                {
                    Tab.Characters => "SHOP · CHARACTERS",
                    Tab.Platforms => "SHOP · PLATFORMS",
                    _ => "SHOP · STAGE"
                };
            }

            if (subtitleText != null)
                subtitleText.text = "Tap an item to buy";
        }

        static void StyleTab(Button tab, bool active)
        {
            if (tab == null) return;
            var img = tab.targetGraphic as Image;
            if (img != null)
                img.color = active
                    ? new Color(0.15f, 0.35f, 0.65f, 1f)
                    : UiTextStyle.ButtonPlateAlt;
        }

        void RefreshWallet()
        {
            if (walletText != null)
            {
                walletText.text = SaveService.Coins.ToString();
                RosterScreenLayout.ApplyOneLine(walletText, RosterScreenLayout.ShopWalletFont);
            }
        }

        void RebuildGrid()
        {
            ClearCells();
            var gm = GameManager.Instance;
            if (gm == null || gridContent == null) return;

            CollectPageItems(gm);
            var pages = Mathf.Max(1, Mathf.CeilToInt(pageItems.Count / (float)RosterScreenLayout.PageSize));
            page = Mathf.Clamp(page, 0, pages - 1);
            var start = page * RosterScreenLayout.PageSize;
            var end = Mathf.Min(start + RosterScreenLayout.PageSize, pageItems.Count);

            switch (currentTab)
            {
                case Tab.Characters:
                    BuildCreatures(gm, start, end);
                    break;
                case Tab.Platforms:
                    BuildPlatforms(gm, start, end);
                    break;
                case Tab.Stage:
                    BuildStages(gm, start, end);
                    break;
            }

            RefreshWallet();
            SetStatus("");
            LayoutScreen();
            RosterScreenLayout.RefreshChevrons(root != null ? root.transform : null, page, pages);
            Canvas.ForceUpdateCanvases();
        }

        void CollectPageItems(GameManager gm)
        {
            pageItems.Clear();
            switch (currentTab)
            {
                case Tab.Characters:
                {
                    var n = gm.Creatures != null ? gm.Creatures.Count : 0;
                    for (var i = 0; i < n; i++)
                    {
                        if (gm.Creatures.GetCreature(i) != null)
                            pageItems.Add(i);
                    }

                    break;
                }
                case Tab.Platforms:
                {
                    var n = gm.Platforms != null ? gm.Platforms.Count : 0;
                    for (var i = 0; i < n; i++)
                    {
                        var p = gm.Platforms.GetPlatform(i);
                        if (p != null && ShopCatalog.AppearsInShop(p))
                            pageItems.Add(i);
                    }

                    break;
                }
                default:
                {
                    var n = gm.Backgrounds != null ? gm.Backgrounds.Count : 0;
                    for (var i = 0; i < n; i++)
                    {
                        var b = gm.Backgrounds.GetBackground(i);
                        if (b != null && ShopCatalog.AppearsInShop(b))
                            pageItems.Add(i);
                    }

                    break;
                }
            }
        }

        void BuildCreatures(GameManager gm, int start, int end)
        {
            var db = gm.Creatures;
            for (var n = start; n < end; n++)
            {
                var i = pageItems[n];
                var c = db.GetCreature(i);
                if (c == null) continue;
                var owned = ShopCatalog.OwnsCreature(c);
                var price = ShopCatalog.GetCreaturePrice(c);
                var cell = CreateCell();
                cell.BindShop(
                    c.DisplayName,
                    c.ResolveSprite(),
                    price,
                    owned,
                    () =>
                    {
                        if (owned)
                        {
                            SetStatus("Already owned");
                            return;
                        }

                        if (ShopCatalog.TryBuyCreature(c))
                        {
                            FeedbackService.UiSelect();
                            SetStatus($"Bought {c.DisplayName}!");
                            RebuildGrid();
                        }
                        else
                        {
                            SetStatus("Not enough coins");
                        }
                    },
                    uiScale: c.RosterUiScale);
            }
        }

        void BuildPlatforms(GameManager gm, int start, int end)
        {
            var db = gm.Platforms;
            for (var n = start; n < end; n++)
            {
                var i = pageItems[n];
                var p = db.GetPlatform(i);
                if (p == null) continue;
                var owned = ShopCatalog.OwnsPlatform(p, gm.Creatures);
                var price = ShopCatalog.GetPlatformPrice(p);
                var solid = p.ResolveSprite(canBreak: false);
                var brk = p.breakSprite != null && p.breakSprite != p.solidSprite ? p.breakSprite : null;
                var cell = CreateCell();
                cell.BindShop(
                    p.DisplayName,
                    solid,
                    price,
                    owned,
                    () =>
                    {
                        if (owned)
                        {
                            SetStatus("Already owned");
                            return;
                        }

                        if (ShopCatalog.TryBuyPlatform(p))
                        {
                            FeedbackService.UiSelect();
                            SetStatus($"Bought {p.DisplayName}!");
                            RebuildGrid();
                        }
                        else
                        {
                            SetStatus("Not enough coins");
                        }
                    },
                    hover: brk,
                    uiScale: p.RosterUiScale);
            }
        }

        void BuildStages(GameManager gm, int start, int end)
        {
            var db = gm.Backgrounds;
            for (var n = start; n < end; n++)
            {
                var i = pageItems[n];
                var b = db.GetBackground(i);
                if (b == null) continue;
                var owned = ShopCatalog.OwnsBackground(b, gm.Creatures);
                var price = ShopCatalog.GetBackgroundPrice(b);
                var cell = CreateCell();
                cell.BindShop(
                    b.DisplayName,
                    b.ResolveSprite(),
                    price,
                    owned,
                    () =>
                    {
                        if (owned)
                        {
                            SetStatus("Already owned");
                            return;
                        }

                        if (ShopCatalog.TryBuyBackground(b))
                        {
                            FeedbackService.UiSelect();
                            SetStatus($"Bought {b.DisplayName}!");
                            RebuildGrid();
                        }
                        else
                        {
                            SetStatus("Not enough coins");
                        }
                    });
            }
        }

        void SetStatus(string msg)
        {
            if (statusLabel == null) return;
            statusLabel.text = msg ?? "";
            var box = statusLabel.rectTransform.sizeDelta.x;
            if (box < 8f) box = 40 * RosterScreenLayout.Unit;
            RosterScreenLayout.ApplyOneLine(
                statusLabel,
                RosterScreenLayout.FitOneLine(box, string.IsNullOrEmpty(msg) ? "Already owned" : msg, 34, 46));
        }

        RosterCell CreateCell()
        {
            var go = new GameObject("ShopCell", typeof(RectTransform));
            var cell = go.AddComponent<RosterCell>();
            cell.Build(gridContent);
            if (grid != null)
                cell.ApplyLayout(grid.cellSize);
            cells.Add(cell);
            return cell;
        }

        void ClearCells()
        {
            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i] != null)
                    Destroy(cells[i].gameObject);
            }

            cells.Clear();
            if (gridContent == null) return;
            for (var i = gridContent.childCount - 1; i >= 0; i--)
            {
                var child = gridContent.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }
        }

        void Update()
        {
            if (!open) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (Screen.width != lastScreen.x || Screen.height != lastScreen.y)
                LayoutScreen();
        }
    }
}
