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
        Tab currentTab = Tab.Characters;
        bool open;

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
            if (walletCoin != null)
            {
                walletCoin.sprite = PixelSpriteFactory.CreateCoinSprite();
                walletCoin.preserveAspect = true;
                CrispVisuals.MakeSpriteCrisp(walletCoin.sprite);
                var settings = CurrencySettings.Load();
                if (settings != null)
                {
                    var s = settings.UiIconSize;
                    walletCoin.rectTransform.sizeDelta = new Vector2(s, s);
                }
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
            EnsureTabLayout();
            RefreshWallet();
            ShowTab(tab);
            if (root != null)
                UiTextStyle.ApplyAllUnder(root.transform);
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

        void EnsureTabLayout()
        {
            if (root == null) return;
            var tabs = root.transform.Find("Tabs") as RectTransform;
            if (tabs == null) return;
            tabs.anchorMin = new Vector2(0.5f, 1f);
            tabs.anchorMax = new Vector2(0.5f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.anchoredPosition = new Vector2(0f, -230f);
        }

        void ShowTab(Tab tab)
        {
            currentTab = tab;
            RefreshTabStyles();
            RebuildGrid();
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
                UiTextStyle.ApplySized(titleText, UiTextStyle.MenuFontSize, TextAnchor.MiddleCenter);
            }

            if (subtitleText != null)
            {
                subtitleText.text = "Set Mode on each database entry: Free, Buy With Coins, or Unlock With Character";
                UiTextStyle.ApplySized(subtitleText, UiTextStyle.MenuFontSize, TextAnchor.MiddleCenter);
            }
        }

        static void StyleTab(Button tab, bool active)
        {
            if (tab == null) return;
            var img = tab.targetGraphic as Image;
            if (img != null)
                img.color = active
                    ? new Color(0.15f, 0.35f, 0.65f, 1f)
                    : UiTextStyle.ButtonPlateAlt;

            var label = tab.GetComponentInChildren<Text>(true);
            if (label != null)
                UiTextStyle.ApplySized(label, UiTextStyle.MenuFontSize);
        }

        void RefreshWallet()
        {
            if (walletText != null)
            {
                walletText.text = SaveService.Coins.ToString();
                UiTextStyle.ApplySized(walletText, UiTextStyle.MenuFontSize, TextAnchor.MiddleLeft);
            }
        }

        void RebuildGrid()
        {
            ClearCells();
            var gm = GameManager.Instance;
            if (gm == null || gridContent == null) return;

            switch (currentTab)
            {
                case Tab.Characters:
                    BuildCreatures(gm);
                    break;
                case Tab.Platforms:
                    BuildPlatforms(gm);
                    break;
                case Tab.Stage:
                    BuildStages(gm);
                    break;
            }

            RefreshWallet();
            SetStatus("");
            Canvas.ForceUpdateCanvases();
        }

        void BuildCreatures(GameManager gm)
        {
            var db = gm.Creatures;
            var count = db != null ? db.Count : 0;
            for (var i = 0; i < count; i++)
            {
                var creature = db.GetCreature(i);
                if (creature == null) continue;
                var c = creature;
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
                    });
            }
        }

        void BuildPlatforms(GameManager gm)
        {
            var db = gm.Platforms;
            var count = db != null ? db.Count : 0;
            for (var i = 0; i < count; i++)
            {
                var platform = db.GetPlatform(i);
                if (platform == null) continue;
                // Character-gated platforms unlock with that character — not sold here.
                if (!ShopCatalog.AppearsInShop(platform)) continue;
                var p = platform;
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
                    hover: brk);
            }
        }

        void BuildStages(GameManager gm)
        {
            var db = gm.Backgrounds;
            var count = db != null ? db.Count : 0;
            for (var i = 0; i < count; i++)
            {
                var bg = db.GetBackground(i);
                if (bg == null) continue;
                // Character-gated stages unlock with that character — not sold here.
                if (!ShopCatalog.AppearsInShop(bg)) continue;
                var b = bg;
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
            UiTextStyle.ApplySized(statusLabel, UiTextStyle.MenuFontSize, TextAnchor.MiddleCenter);
        }

        RosterCell CreateCell()
        {
            var go = new GameObject("ShopCell", typeof(RectTransform));
            var cell = go.AddComponent<RosterCell>();
            cell.Build(gridContent);
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
                Close();
        }
    }
}
