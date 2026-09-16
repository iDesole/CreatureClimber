using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Smash-style customize roster: Characters / Platforms / Stage grids
    /// that grow with database size. Platform break sprites appear only on hover.
    /// </summary>
    public class CustomizeMenu : MonoBehaviour
    {
        public enum Tab
        {
            Characters,
            Platforms,
            Backgrounds
        }

        [SerializeField] GameObject root;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] Button charactersTab;
        [SerializeField] Button platformsTab;
        [SerializeField] Button backgroundsTab;
        [SerializeField] Button closeButton;
        [SerializeField] RectTransform gridContent;
        [SerializeField] GridLayoutGroup grid;
        [SerializeField] Text selectionLabel;

        readonly List<RosterCell> cells = new();
        Tab currentTab = Tab.Characters;
        bool open;
        Vector2 lastScreen;
        int page;

        public bool IsOpen => open;

        public void Setup(
            GameObject panelRoot,
            Text title,
            Text subtitle,
            Button charTab,
            Button platTab,
            Button bgTab,
            Button close,
            RectTransform content,
            GridLayoutGroup layout,
            Text selection)
        {
            root = panelRoot;
            titleText = title;
            subtitleText = subtitle;
            charactersTab = charTab;
            platformsTab = platTab;
            backgroundsTab = bgTab;
            closeButton = close;
            gridContent = content;
            grid = layout;
            selectionLabel = selection;

            WireTabs();
            RosterScreenLayout.EnsureChevrons(root != null ? root.transform : null, PrevPage, NextPage);
            ApplyCustomizeTextStyles();
            if (root != null)
                root.SetActive(false);
            open = false;
        }

        void ApplyCustomizeTextStyles()
        {
            RosterScreenLayout.PlaceBackChevron(closeButton != null ? closeButton.transform as RectTransform : null);
            RosterScreenLayout.StyleBackChevron(closeButton);
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

            if (backgroundsTab != null)
            {
                backgroundsTab.onClick.RemoveAllListeners();
                backgroundsTab.onClick.AddListener(() => ShowTab(Tab.Backgrounds));
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
            ShowTab(tab);
            LayoutScreen();
            FeedbackService.UiSelect();
        }

        void LayoutScreen()
        {
            if (root == null) return;
            RosterScreenLayout.LayoutChrome(root.transform, shop: false);
            var scroll = root.transform.Find("Scroll") as RectTransform;
            var cell = RosterScreenLayout.FitGrid(grid, scroll, shop: false);
            RosterScreenLayout.ApplyChromeFonts(root.transform, shop: false);
            RosterScreenLayout.PlaceChevrons(root.transform);
            for (var i = 0; i < cells.Count; i++)
                cells[i]?.ApplyLayout(cell);
            lastScreen = new Vector2(Screen.width, Screen.height);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            if (root != null)
                root.SetActive(false);
            FeedbackService.UiSelect();
            GameManager.Instance?.OnCustomizeClosed();
        }

        public void Toggle()
        {
            if (open) Close();
            else Open(currentTab);
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
            StyleTab(backgroundsTab, currentTab == Tab.Backgrounds);

            if (titleText != null)
            {
                titleText.text = currentTab switch
                {
                    Tab.Characters => "CHARACTERS",
                    Tab.Platforms => "PLATFORMS",
                    _ => "STAGE"
                };
            }

            if (subtitleText != null)
            {
                subtitleText.text = currentTab switch
                {
                    Tab.Characters => "Pick who you climb as",
                    Tab.Platforms => "Hover a pad to preview break",
                    _ => "Pick your stage backdrop"
                };
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
            if (label != null && tab.name.Contains("Background") && label.text == "Backgrounds")
                label.text = "Stage";
        }

        void RebuildGrid()
        {
            ClearCells();

            var gm = GameManager.Instance;
            if (gm == null || gridContent == null) return;

            var total = CountItems(gm);
            var pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)RosterScreenLayout.PageSize));
            page = Mathf.Clamp(page, 0, pages - 1);
            var start = page * RosterScreenLayout.PageSize;
            var end = Mathf.Min(start + RosterScreenLayout.PageSize, total);

            switch (currentTab)
            {
                case Tab.Characters:
                    BuildCreatures(gm, start, end);
                    break;
                case Tab.Platforms:
                    BuildPlatforms(gm, start, end);
                    break;
                case Tab.Backgrounds:
                    BuildBackgrounds(gm, start, end);
                    break;
            }

            UpdateSelectionLabel(gm);
            LayoutScreen();
            RosterScreenLayout.RefreshChevrons(root != null ? root.transform : null, page, pages);
            Canvas.ForceUpdateCanvases();
        }

        int CountItems(GameManager gm)
        {
            return currentTab switch
            {
                Tab.Characters => gm.Creatures != null ? gm.Creatures.Count : 0,
                Tab.Platforms => gm.Platforms != null ? gm.Platforms.Count : 0,
                _ => gm.Backgrounds != null ? gm.Backgrounds.Count : 0
            };
        }

        void BuildCreatures(GameManager gm, int start, int end)
        {
            var db = gm.Creatures;
            var selected = gm.PlayAsCreatureIndex;
            for (var i = start; i < end; i++)
            {
                var creature = db.GetCreature(i);
                if (creature == null) continue;
                var owned = ShopCatalog.OwnsCreature(creature);
                var index = i;
                var cell = CreateCell();
                cell.Bind(
                    creature.DisplayName,
                    creature.ResolveSprite(),
                    () =>
                    {
                        if (!ShopCatalog.OwnsCreature(creature)) return;
                        gm.SetPlayAs(index);
                        RebuildGrid();
                    },
                    isLocked: !owned,
                    uiScale: creature.RosterUiScale);
                if (owned)
                    cell.SetSelected(index == selected);
            }
        }

        void BuildPlatforms(GameManager gm, int start, int end)
        {
            var db = gm.Platforms;
            var selected = gm.ActivePlatformIndex;
            for (var i = start; i < end; i++)
            {
                var platform = db.GetPlatform(i);
                if (platform == null) continue;
                var owned = ShopCatalog.OwnsPlatform(platform, gm.Creatures);
                var index = i;
                var solid = platform.ResolveSprite(canBreak: false);
                var brk = platform.breakSprite != null && platform.breakSprite != platform.solidSprite
                    ? platform.breakSprite
                    : null;
                var cell = CreateCell();
                cell.Bind(
                    platform.DisplayName,
                    solid,
                    () =>
                    {
                        if (!ShopCatalog.OwnsPlatform(platform, gm.Creatures)) return;
                        gm.SetActivePlatform(index);
                        RebuildGrid();
                    },
                    hover: brk,
                    isLocked: !owned,
                    uiScale: platform.RosterUiScale);
                if (owned)
                    cell.SetSelected(index == selected);
            }
        }

        void BuildBackgrounds(GameManager gm, int start, int end)
        {
            var db = gm.Backgrounds;
            var selected = gm.ActiveBackgroundIndex;
            for (var i = start; i < end; i++)
            {
                var bg = db.GetBackground(i);
                if (bg == null) continue;
                var owned = ShopCatalog.OwnsBackground(bg, gm.Creatures);
                var index = i;
                var cell = CreateCell();
                cell.Bind(
                    bg.DisplayName,
                    bg.ResolveSprite(),
                    () =>
                    {
                        if (!ShopCatalog.OwnsBackground(bg, gm.Creatures)) return;
                        gm.SetActiveBackground(index);
                        RebuildGrid();
                    },
                    isLocked: !owned);
                if (owned)
                    cell.SetSelected(index == selected);
            }
        }

        RosterCell CreateCell()
        {
            var go = new GameObject("RosterCell", typeof(RectTransform));
            var cell = go.AddComponent<RosterCell>();
            cell.Build(gridContent);
            if (grid != null)
                cell.ApplyLayout(grid.cellSize);
            cells.Add(cell);
            return cell;
        }

        void UpdateSelectionLabel(GameManager gm)
        {
            if (selectionLabel == null || gm == null) return;

            selectionLabel.text = currentTab switch
            {
                Tab.Characters => $"Selected: {gm.SelectedCreature?.DisplayName ?? "—"}",
                Tab.Platforms => $"Selected: {gm.SelectedPlatform?.DisplayName ?? "—"}",
                _ => $"Selected: {gm.SelectedBackground?.DisplayName ?? "—"}"
            };
            var box = selectionLabel.rectTransform.sizeDelta.x;
            if (box < 8f) box = 40 * RosterScreenLayout.Unit;
            RosterScreenLayout.ApplyOneLine(
                selectionLabel,
                RosterScreenLayout.FitOneLine(box, selectionLabel.text, 34, 46));
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
