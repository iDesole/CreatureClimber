using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Shop / Customize roster: sizes snap to 16 PPU and fill the live panel.
    /// </summary>
    public static class RosterScreenLayout
    {
        public const int Unit = CrispVisuals.SpritePPU;

        /// <summary>Top inset that clears the coin HUD (5 × 16).</summary>
        public const int HudBand = 5 * Unit;

        public const int GridColumns = 4;
        public const int GridRows = 6;
        public const int PageSize = GridColumns * GridRows;

        /// <summary>Shop wallet coin + count vs the 16-grid chrome (5× the original 38 / 4u).</summary>
        public const float ShopWalletScale = 5f;
        public const int ShopWalletFont = 38 * 5;

        public static float Snap(float pixels)
        {
            if (pixels < Unit) return Unit;
            return Mathf.Round(pixels / Unit) * Unit;
        }

        /// <summary>
        /// One-line bold size that fits <paramref name="sample"/> in <paramref name="boxWidth"/>.
        /// Advance ≈ 0.56em (Legacy bold). Snaps to 4px so it stays on the 16 grid.
        /// Clash / Brawl / Candy Crush roster names stay on one line this way.
        /// </summary>
        public static int FitOneLine(float boxWidth, string sample, int minSize, int maxSize)
        {
            var chars = 1;
            if (!string.IsNullOrEmpty(sample))
                chars = sample.Length;
            var raw = boxWidth / (chars * 0.56f);
            var stepped = Mathf.RoundToInt(Mathf.Clamp(raw, minSize, maxSize) / 4f) * 4;
            return Mathf.Clamp(stepped, minSize, maxSize);
        }

        public static float ContentWidth(RectTransform panel)
        {
            var w = panel != null && panel.rect.width > 8f
                ? panel.rect.width
                : PlatformRuntime.DesignWidth;
            return Snap(Mathf.Min(w - Unit * 2f, 64 * Unit));
        }

        public static void ApplyOneLine(Text label, int fontSize)
        {
            if (label == null) return;
            UiTextStyle.ApplySized(label, fontSize, label.alignment);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = false;
        }

        public static void ApplyWrapped(Text label, int fontSize, TextAnchor align)
        {
            if (label == null) return;
            UiTextStyle.ApplySized(label, fontSize, align);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        public static void PlaceTop(RectTransform rect, float y, float width, float height)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static float ShopWalletIconSize()
        {
            var settings = CurrencySettings.Load();
            var baseSize = settings != null ? settings.UiIconSize : 28f;
            return Snap(baseSize * ShopWalletScale);
        }

        public static float ShopWalletHeight()
        {
            return Snap(Mathf.Max(4 * Unit * ShopWalletScale, ShopWalletIconSize() + Unit));
        }

        public static void LayoutShopWalletContents(RectTransform wallet)
        {
            if (wallet == null) return;

            var iconSize = ShopWalletIconSize();
            var h = wallet.sizeDelta.y > 8f ? wallet.sizeDelta.y : ShopWalletHeight();
            var gap = Unit;

            var coin = wallet.Find("Coin") as RectTransform;
            if (coin != null)
            {
                coin.anchorMin = new Vector2(0.5f, 0.5f);
                coin.anchorMax = new Vector2(0.5f, 0.5f);
                coin.pivot = new Vector2(1f, 0.5f);
                coin.anchoredPosition = new Vector2(-gap, 0f);
                coin.sizeDelta = new Vector2(iconSize, iconSize);
            }

            var text = wallet.Find("WalletText")?.GetComponent<Text>();
            if (text == null) return;

            var tr = text.rectTransform;
            tr.anchorMin = new Vector2(0.5f, 0.5f);
            tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.anchoredPosition = new Vector2(gap, 0f);
            var textW = wallet.sizeDelta.x > 8f
                ? Mathf.Max(Unit * 8f, wallet.sizeDelta.x * 0.5f)
                : 24 * Unit;
            tr.sizeDelta = new Vector2(textW, h);
            ApplyOneLine(text, ShopWalletFont);
        }

        /// <summary>Display size for 16-PPU art inside a UI box, snapped to the PPU grid.</summary>
        public static Vector2 FitSprite(Sprite sprite, float maxBox)
        {
            maxBox = Snap(Mathf.Max(Unit, maxBox));
            if (sprite == null)
                return new Vector2(maxBox, maxBox);

            var px = sprite.rect.size;
            var longest = Mathf.Max(px.x, px.y, 1f);
            var scale = maxBox / longest;
            var stepped = Mathf.Floor(scale * Unit) / Unit;
            if (stepped < 1f / Unit)
                stepped = scale;
            return new Vector2(Snap(px.x * stepped), Snap(px.y * stepped));
        }

        public static Vector2 FitGrid(GridLayoutGroup grid, RectTransform viewport, bool shop)
        {
            if (grid == null)
                return new Vector2(10 * Unit, 12 * Unit);

            Canvas.ForceUpdateCanvases();
            var viewW = viewport != null ? viewport.rect.width : 60f * Unit;
            var viewH = viewport != null ? viewport.rect.height : 48f * Unit;
            if (viewW < 8f) viewW = 60f * Unit;
            if (viewH < 8f) viewH = 48f * Unit;

            var pad = Unit;
            var gap = Unit;
            var innerW = viewW - pad * 2f;
            var innerH = viewH - pad * 2f;
            var cellW = Snap((innerW - gap * (GridColumns - 1)) / GridColumns);
            var cellH = Snap((innerH - gap * (GridRows - 1)) / GridRows);
            if (shop)
                cellH = Mathf.Max(cellH, cellW + 2 * Unit);

            grid.cellSize = new Vector2(cellW, cellH);
            grid.spacing = new Vector2(gap, gap);
            grid.padding = new RectOffset(pad, pad, pad, pad);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumns;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            return grid.cellSize;
        }

        public static void EnsureChevrons(Transform root, UnityEngine.Events.UnityAction onPrev, UnityEngine.Events.UnityAction onNext)
        {
            if (root == null) return;
            var prev = EnsureChevron(root, "PagePrev", "<");
            var next = EnsureChevron(root, "PageNext", ">");
            if (prev != null)
            {
                prev.onClick.RemoveAllListeners();
                if (onPrev != null) prev.onClick.AddListener(onPrev);
            }

            if (next != null)
            {
                next.onClick.RemoveAllListeners();
                if (onNext != null) next.onClick.AddListener(onNext);
            }
        }

        public const float BackChevronSize = 112f;
        public const float BackChevronPad = 36f;
        public const float BackChevronBottom = 40f;

        public static void PlaceBackChevron(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(BackChevronPad, BackChevronBottom);
            rect.sizeDelta = new Vector2(BackChevronSize, BackChevronSize);
        }

        public static void StyleBackChevron(Button button)
        {
            if (button == null) return;

            var labels = button.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                    labels[i].gameObject.SetActive(false);
            }

            var img = button.targetGraphic as Image;
            if (img == null)
                img = button.GetComponent<Image>();
            if (img == null)
                img = button.gameObject.AddComponent<Image>();

            img.sprite = PixelSpriteFactory.CreateBackChevronSprite();
            img.color = Color.white;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = true;
            button.targetGraphic = img;
            button.transition = Selectable.Transition.None;
        }

        public static void PlaceChevrons(Transform root)
        {
            if (root == null) return;

            var size = 6 * Unit;
            var y = BackChevronBottom + BackChevronSize + Unit;
            var scroll = root != null ? root.Find("Scroll") as RectTransform : null;
            if (scroll != null)
            {
                var bot = scroll.offsetMin.y;
                var h = scroll.rect.height;
                if (h > 8f)
                    y = bot + h * 0.5f - size * 0.5f;
                else if (bot > 1f)
                    y = bot + 12 * Unit;
            }

            PlaceCorner(root.Find("PagePrev") as RectTransform, new Vector2(0f, 0f), new Vector2(Unit * 2f, y), size);
            PlaceCorner(root.Find("PageNext") as RectTransform, new Vector2(1f, 0f), new Vector2(-Unit * 2f, y), size);
        }

        public static void RefreshChevrons(Transform root, int page, int pageCount)
        {
            var multi = pageCount > 1;
            SetChevron(root.Find("PagePrev"), multi, page > 0);
            SetChevron(root.Find("PageNext"), multi, page < pageCount - 1);
        }

        static void PlaceCorner(RectTransform rect, Vector2 anchor, Vector2 pos, float size)
        {
            if (rect == null) return;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(size, size);
        }

        static void SetChevron(Transform tf, bool visible, bool interactable)
        {
            if (tf == null) return;
            tf.gameObject.SetActive(visible);
            var button = tf.GetComponent<Button>();
            if (button != null)
                button.interactable = interactable;
            var label = tf.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                var c = label.color;
                c.a = interactable ? 1f : 0.35f;
                label.color = c;
            }
        }

        static Button EnsureChevron(Transform root, string name, string glyph)
        {
            var existing = root.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(root, false);
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
            var label = textGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = glyph;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            UiTextStyle.ApplySized(label, 72, TextAnchor.MiddleCenter);
            return button;
        }

        public static void ApplyChromeFonts(Transform root, bool shop)
        {
            if (root == null) return;

            var title = root.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                var box = title.rectTransform.sizeDelta.x;
                if (box < 8f) box = 40 * Unit;
                var sample = shop ? "SHOP · CHARACTERS" : "CHARACTERS";
                ApplyOneLine(title, FitOneLine(box * 0.85f, sample, 32, 48));
            }

            var sub = root.Find("Subtitle")?.GetComponent<Text>();
            if (sub != null)
            {
                var box = sub.rectTransform.sizeDelta.x;
                if (box < 8f) box = 40 * Unit;
                ApplyWrapped(sub, FitOneLine(box, "Pick who you climb as", 34, 42), TextAnchor.UpperCenter);
            }

            var tabs = root.Find("Tabs") as RectTransform;
            if (tabs != null)
            {
                var tabW = tabs.rect.width > 8f ? tabs.rect.width / 3f : 20 * Unit;
                var tabFont = FitOneLine(tabW - Unit, "Characters", 30, 42);
                ApplyTab(root.Find("Tabs/CharactersTab"), tabFont);
                ApplyTab(root.Find("Tabs/PlatformsTab"), tabFont);
                ApplyTab(root.Find("Tabs/StageTab"), tabFont);
                ApplyTab(root.Find("Tabs/BackgroundsTab"), tabFont);
            }

            var foot = root.Find(shop ? "StatusLabel" : "SelectionLabel")?.GetComponent<Text>();
            if (foot != null)
            {
                var box = foot.rectTransform.sizeDelta.x;
                if (box < 8f) box = 40 * Unit;
                ApplyOneLine(foot, FitOneLine(box, "Selected: HayBaleEaten", 34, 46));
            }

            var wallet = root.Find("Wallet/WalletText")?.GetComponent<Text>();
            if (wallet != null)
                ApplyOneLine(wallet, ShopWalletFont);
        }

        static void ApplyTab(Transform tab, int font)
        {
            if (tab == null) return;
            var label = tab.GetComponentInChildren<Text>(true);
            if (label == null) return;
            if (tab.name.Contains("Background") && label.text == "Backgrounds")
                label.text = "Stage";
            ApplyOneLine(label, font);
        }

        /// <summary>Pin chrome and stretch the scroll to the leftover screen.</summary>
        public static void LayoutChrome(Transform root, bool shop)
        {
            if (root == null) return;
            var panel = root as RectTransform;
            if (panel == null) return;

            var contentW = ContentWidth(panel);

            var hudBand = HudBand;
            var titleH = 3 * Unit;
            var subH = shop ? 5 * Unit : 3 * Unit;
            var tabH = 5 * Unit;
            var backH = BackChevronSize;
            var footH = 4 * Unit;

            PlaceTop(root.Find("Title") as RectTransform, -hudBand, contentW, titleH);
            PlaceTop(root.Find("Subtitle") as RectTransform, -hudBand - titleH, contentW, subH);

            var tabs = root.Find("Tabs") as RectTransform;
            if (tabs != null)
            {
                tabs.anchorMin = new Vector2(0.5f, 1f);
                tabs.anchorMax = new Vector2(0.5f, 1f);
                tabs.pivot = new Vector2(0.5f, 1f);
                tabs.anchoredPosition = new Vector2(0f, -hudBand - titleH - subH - Unit);
                tabs.sizeDelta = new Vector2(contentW, tabH);
            }

            var close = root.Find("CloseButton") as RectTransform;
            if (close != null)
            {
                PlaceBackChevron(close);
                StyleBackChevron(close.GetComponent<Button>());
            }

            var foot = root.Find(shop ? "StatusLabel" : "SelectionLabel") as RectTransform;
            if (foot != null)
            {
                foot.anchorMin = new Vector2(0.5f, 0f);
                foot.anchorMax = new Vector2(0.5f, 0f);
                foot.pivot = new Vector2(0.5f, 0f);
                foot.anchoredPosition = new Vector2(0f, Unit * 2f + backH + Unit);
                foot.sizeDelta = new Vector2(contentW, footH);
            }

            if (shop)
            {
                var wallet = root.Find("Wallet") as RectTransform;
                if (wallet != null)
                {
                    var walletH = ShopWalletHeight();
                    wallet.anchorMin = new Vector2(0.5f, 0f);
                    wallet.anchorMax = new Vector2(0.5f, 0f);
                    wallet.pivot = new Vector2(0.5f, 0f);
                    wallet.anchoredPosition = new Vector2(0f, Unit * 2f + backH + Unit + footH);
                    wallet.sizeDelta = new Vector2(contentW, walletH);
                    LayoutShopWalletContents(wallet);
                }
            }

            var scroll = root.Find("Scroll") as RectTransform;
            if (scroll != null)
            {
                var topCut = hudBand + titleH + subH + Unit + tabH + Unit;
                var botCut = shop
                    ? Unit * 2f + backH + Unit + footH + ShopWalletHeight() + Unit
                    : Unit * 2f + backH + Unit + footH + Unit;
                scroll.anchorMin = Vector2.zero;
                scroll.anchorMax = Vector2.one;
                scroll.pivot = new Vector2(0.5f, 0.5f);
                scroll.offsetMin = new Vector2(Unit, botCut);
                scroll.offsetMax = new Vector2(-Unit, -topCut);
                var scroller = scroll.GetComponent<ScrollRect>();
                if (scroller != null)
                {
                    scroller.vertical = false;
                    scroller.horizontal = false;
                    scroller.movementType = ScrollRect.MovementType.Clamped;
                }
            }

            PlaceChevrons(root);
        }

    }
}
