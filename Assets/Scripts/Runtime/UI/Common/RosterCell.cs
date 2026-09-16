using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// One Smash-style roster tile. Platforms show solid art; break art only on hover.
    /// Portrait sits on a bright stage plate and is slightly zoomed so art pops.
    /// </summary>
    public class RosterCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Image frame;
        Image plateOuter;
        Image plateInner;
        Image icon;
        Image coinIcon;
        Image lockIcon;
        Text label;
        Text priceLabel;
        GameObject priceRow;
        RectTransform stageRect;
        RectTransform iconRect;
        RectTransform labelRect;
        RectTransform priceRect;
        RectTransform lockRect;
        Button button;
        Sprite normalSprite;
        Sprite hoverSprite;
        Action onClick;
        bool selected;
        bool locked;
        bool shopOwned;
        CrispUIText crisp;

        int nameFont = 24;
        float uiScale = 1f;

        const float IconZoom = 1.125f;

        static readonly Color FrameNormal = new Color(0.10f, 0.12f, 0.16f, 0.94f);
        static readonly Color FrameHover = new Color(0.16f, 0.26f, 0.46f, 0.96f);
        static readonly Color FrameSelected = new Color(0.14f, 0.34f, 0.62f, 0.98f);
        static readonly Color FrameOwned = new Color(0.10f, 0.26f, 0.16f, 0.94f);

        // Stage plate — bright blue–purple so dark sprites (spider) and green art both pop.
        static readonly Color PlateRim = new Color(0.95f, 0.92f, 0.55f, 1f);
        static readonly Color PlateFill = new Color(0.46f, 0.40f, 0.90f, 1f);
        static readonly Color PlateFillSelected = new Color(0.60f, 0.50f, 1f, 1f);
        static readonly Color PlateFillLocked = new Color(0.28f, 0.30f, 0.34f, 1f);

        public void Build(Transform parent)
        {
            transform.SetParent(parent, false);

            var rect = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(11 * RosterScreenLayout.Unit, 13 * RosterScreenLayout.Unit);

            frame = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            frame.color = FrameNormal;
            frame.raycastTarget = true;

            button = gameObject.GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());

            // Portrait stage: gold rim + sky fill + zoomed/cropped icon.
            var stageGo = new GameObject("PortraitStage", typeof(RectTransform));
            stageGo.transform.SetParent(transform, false);
            stageRect = stageGo.GetComponent<RectTransform>();
            stageRect.anchorMin = new Vector2(0.5f, 0.58f);
            stageRect.anchorMax = new Vector2(0.5f, 0.58f);
            stageRect.pivot = new Vector2(0.5f, 0.5f);
            var portrait = 8 * RosterScreenLayout.Unit;
            stageRect.sizeDelta = new Vector2(portrait, portrait);

            var rimGo = new GameObject("PlateRim", typeof(RectTransform), typeof(Image));
            rimGo.transform.SetParent(stageGo.transform, false);
            var rimRect = rimGo.GetComponent<RectTransform>();
            rimRect.anchorMin = Vector2.zero;
            rimRect.anchorMax = Vector2.one;
            rimRect.offsetMin = Vector2.zero;
            rimRect.offsetMax = Vector2.zero;
            plateOuter = rimGo.GetComponent<Image>();
            plateOuter.sprite = PixelSpriteFactory.CreateSolid(8, 8, Color.white);
            plateOuter.color = PlateRim;
            plateOuter.raycastTarget = false;
            CrispVisuals.MakeSpriteCrisp(plateOuter.sprite);

            var fillGo = new GameObject("PlateFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(stageGo.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(5f, 5f);
            fillRect.offsetMax = new Vector2(-5f, -5f);
            plateInner = fillGo.GetComponent<Image>();
            plateInner.sprite = PixelSpriteFactory.CreateSolid(8, 8, Color.white);
            plateInner.color = PlateFill;
            plateInner.raycastTarget = false;
            CrispVisuals.MakeSpriteCrisp(plateInner.sprite);

            // Clip the zoomed sprite so it fills the plate without overflowing the card.
            var maskGo = new GameObject("PortraitMask", typeof(RectTransform), typeof(RectMask2D));
            maskGo.transform.SetParent(stageGo.transform, false);
            var maskRect = maskGo.GetComponent<RectTransform>();
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = new Vector2(5f, 5f);
            maskRect.offsetMax = new Vector2(-5f, -5f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(maskGo.transform, false);
            iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(portrait, portrait);
            icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = Color.white;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(transform, false);
            labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 2 * RosterScreenLayout.Unit);
            labelRect.sizeDelta = new Vector2(-RosterScreenLayout.Unit, 2 * RosterScreenLayout.Unit);
            label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = nameFont;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            UiTextStyle.Apply(label);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            crisp = labelGo.AddComponent<CrispUIText>();
            crisp.SetDesignSize(nameFont);

            // Shop price row: coin + number
            priceRow = new GameObject("PriceRow", typeof(RectTransform));
            priceRow.transform.SetParent(transform, false);
            priceRect = priceRow.GetComponent<RectTransform>();
            priceRect.anchorMin = new Vector2(0.5f, 0f);
            priceRect.anchorMax = new Vector2(0.5f, 0f);
            priceRect.pivot = new Vector2(0.5f, 0f);
            priceRect.anchoredPosition = new Vector2(0f, RosterScreenLayout.Unit * 0.5f);
            priceRect.sizeDelta = new Vector2(10 * RosterScreenLayout.Unit, 2 * RosterScreenLayout.Unit);
            priceRow.SetActive(false);

            var coinGo = new GameObject("Coin", typeof(RectTransform), typeof(Image));
            coinGo.transform.SetParent(priceRow.transform, false);
            var coinRect = coinGo.GetComponent<RectTransform>();
            coinRect.anchorMin = new Vector2(0f, 0.5f);
            coinRect.anchorMax = new Vector2(0f, 0.5f);
            coinRect.pivot = new Vector2(0f, 0.5f);
            coinRect.anchoredPosition = new Vector2(18f, 0f);
            var coinUiSize = 24f;
            var currency = CurrencySettings.Load();
            if (currency != null)
                coinUiSize = currency.UiIconSize;
            coinRect.sizeDelta = new Vector2(coinUiSize, coinUiSize);
            coinIcon = coinGo.GetComponent<Image>();
            coinIcon.sprite = PixelSpriteFactory.CreateCoinSprite();
            coinIcon.preserveAspect = true;
            coinIcon.raycastTarget = false;
            CrispVisuals.MakeSpriteCrisp(coinIcon.sprite);

            var priceGo = new GameObject("Price", typeof(RectTransform), typeof(Text));
            priceGo.transform.SetParent(priceRow.transform, false);
            var pr = priceGo.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0f, 0f);
            pr.anchorMax = new Vector2(1f, 1f);
            pr.offsetMin = new Vector2(46f, 0f);
            pr.offsetMax = new Vector2(-8f, 0f);
            priceLabel = priceGo.GetComponent<Text>();
            priceLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            priceLabel.fontSize = 32;
            priceLabel.alignment = TextAnchor.MiddleLeft;
            priceLabel.raycastTarget = false;
            UiTextStyle.Apply(priceLabel);
            priceGo.AddComponent<CrispUIText>().SetDesignSize(32);

            // Customize lock icon — bottom right.
            var lockGo = new GameObject("Lock", typeof(RectTransform), typeof(Image));
            lockGo.transform.SetParent(transform, false);
            lockRect = lockGo.GetComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(1f, 0f);
            lockRect.anchorMax = new Vector2(1f, 0f);
            lockRect.pivot = new Vector2(1f, 0f);
            lockRect.anchoredPosition = new Vector2(-RosterScreenLayout.Unit * 0.5f, RosterScreenLayout.Unit * 0.5f);
            lockRect.sizeDelta = new Vector2(2 * RosterScreenLayout.Unit, 2 * RosterScreenLayout.Unit);
            lockIcon = lockGo.GetComponent<Image>();
            lockIcon.sprite = PixelSpriteFactory.CreateLockSprite();
            lockIcon.preserveAspect = true;
            lockIcon.raycastTarget = false;
            lockIcon.color = Color.white;
            CrispVisuals.MakeSpriteCrisp(lockIcon.sprite);
            lockGo.SetActive(false);
        }

        /// <summary>Resize the card to a 16-PPU-snapped cell from the live grid.</summary>
        public void ApplyLayout(Vector2 cellSize)
        {
            var rect = transform as RectTransform;
            if (rect != null)
                rect.sizeDelta = cellSize;

            var u = RosterScreenLayout.Unit;
            var portrait = RosterScreenLayout.Snap(Mathf.Min(cellSize.x - u * 2f, cellSize.y * 0.58f));
            if (stageRect != null)
                stageRect.sizeDelta = new Vector2(portrait, portrait);

            FitIcon(normalSprite != null ? normalSprite : icon != null ? icon.sprite : null, portrait);

            if (labelRect != null)
            {
                var priceUp = priceRow != null && priceRow.activeSelf;
                labelRect.anchoredPosition = new Vector2(0f, priceUp ? u * 2.6f : u * 1.15f);
                labelRect.sizeDelta = new Vector2(-u, u * 2.25f);
                FitNameFont(cellSize.x - u);
            }

            if (priceRect != null)
            {
                priceRect.anchoredPosition = new Vector2(0f, u * 0.75f);
                priceRect.sizeDelta = new Vector2(RosterScreenLayout.Snap(cellSize.x - u), u * 2f);
            }

            if (lockRect != null)
                lockRect.sizeDelta = new Vector2(u * 2f, u * 2f);
        }

        void FitNameFont(float boxWidth)
        {
            if (label == null) return;
            var sample = string.IsNullOrEmpty(label.text) ? "HayBaleEaten" : label.text.Replace(" ", "");
            nameFont = RosterScreenLayout.FitOneLine(boxWidth, sample, 20, 30);
            RosterScreenLayout.ApplyOneLine(label, nameFont);
        }

        void FitIcon(Sprite sprite, float maxBox)
        {
            if (iconRect == null) return;
            var mul = uiScale < 0.05f ? 1f : Mathf.Clamp(uiScale, 0.25f, 3f);
            iconRect.sizeDelta = RosterScreenLayout.FitSprite(sprite, maxBox * IconZoom) * mul;
        }

        public void Bind(string displayName, Sprite sprite, Action clicked, Sprite hover = null, bool isLocked = false, float uiScale = 1f)
        {
            BindInternal(displayName, sprite, clicked, hover, showPrice: false, price: 0, owned: !isLocked, isLocked: isLocked, uiScale);
        }

        /// <summary>Shop tile: name + art + coin price. Owned items show no price line.</summary>
        public void BindShop(string displayName, Sprite sprite, int price, bool owned, Action clicked, Sprite hover = null, float uiScale = 1f)
        {
            BindInternal(displayName, sprite, clicked, hover, showPrice: true, price, owned, isLocked: false, uiScale);
        }

        void BindInternal(string displayName, Sprite sprite, Action clicked, Sprite hover, bool showPrice, int price, bool owned, bool isLocked, float uiScale)
        {
            this.uiScale = uiScale;
            normalSprite = sprite;
            hoverSprite = hover;
            onClick = clicked;
            locked = isLocked;
            shopOwned = showPrice && owned && !isLocked;
            selected = false;

            if (label != null)
            {
                label.text = displayName ?? "";
                var box = labelRect != null ? labelRect.rect.width : 8 * RosterScreenLayout.Unit;
                if (box < 8f) box = 8 * RosterScreenLayout.Unit;
                FitNameFont(box);
            }

            if (icon != null)
            {
                icon.sprite = normalSprite;
                icon.enabled = normalSprite != null;
                icon.color = isLocked ? new Color(0.55f, 0.55f, 0.58f, 1f) : Color.white;
                if (normalSprite != null)
                    CrispVisuals.MakeSpriteCrisp(normalSprite);
                if (hoverSprite != null)
                    CrispVisuals.MakeSpriteCrisp(hoverSprite);
                if (stageRect != null)
                    FitIcon(normalSprite, stageRect.sizeDelta.x);
            }

            if (lockIcon != null)
                lockIcon.gameObject.SetActive(isLocked);

            if (priceRow != null)
            {
                var showCost = showPrice && !isLocked && !owned;
                priceRow.SetActive(showCost);
                if (showCost && priceLabel != null)
                {
                    if (price <= 0)
                    {
                        priceLabel.text = "Free";
                        if (coinIcon != null) coinIcon.enabled = true;
                    }
                    else
                    {
                        priceLabel.text = price.ToString();
                        if (coinIcon != null) coinIcon.enabled = true;
                    }

                    UiTextStyle.ApplySized(priceLabel, 34, TextAnchor.MiddleLeft);
                }
            }

            if (shopOwned && frame != null)
            {
                frame.color = FrameOwned;
                RefreshPlateColors();
            }
            else if (isLocked && frame != null)
            {
                frame.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
                RefreshPlateColors();
            }
            else
            {
                SetSelected(false);
            }
        }

        public void SetSelected(bool value)
        {
            selected = value;
            if (locked) return;
            if (frame != null)
            {
                if (shopOwned && !selected)
                    frame.color = FrameOwned;
                else
                    frame.color = selected ? FrameSelected : FrameNormal;
            }

            RefreshPlateColors();

            if (label != null)
                RosterScreenLayout.ApplyOneLine(label, nameFont);
        }

        void RefreshPlateColors()
        {
            if (plateOuter != null)
            {
                plateOuter.color = locked
                    ? new Color(0.45f, 0.45f, 0.48f, 1f)
                    : selected
                        ? new Color(1f, 0.95f, 0.45f, 1f)
                        : shopOwned
                            ? new Color(0.65f, 0.95f, 0.55f, 1f)
                            : PlateRim;
            }

            if (plateInner != null)
            {
                plateInner.color = locked
                    ? PlateFillLocked
                    : selected
                        ? PlateFillSelected
                        : shopOwned
                            ? new Color(0.42f, 0.78f, 0.55f, 1f)
                            : PlateFill;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (locked) return;
            if (frame != null && !selected)
                frame.color = FrameHover;

            if (!selected && plateInner != null)
            {
                var baseFill = shopOwned ? new Color(0.42f, 0.78f, 0.55f, 1f) : PlateFill;
                plateInner.color = Color.Lerp(baseFill, PlateFillSelected, 0.45f);
            }

            if (!selected && plateOuter != null)
            {
                var baseRim = shopOwned ? new Color(0.65f, 0.95f, 0.55f, 1f) : PlateRim;
                plateOuter.color = Color.Lerp(baseRim, new Color(1f, 0.95f, 0.45f, 1f), 0.35f);
            }

            // Platforms: break art only while hovered (not a separate roster entry).
            if (icon != null && hoverSprite != null)
                icon.sprite = hoverSprite;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (locked) return;
            if (frame != null)
            {
                if (selected)
                    frame.color = FrameSelected;
                else if (shopOwned)
                    frame.color = FrameOwned;
                else
                    frame.color = FrameNormal;
            }

            RefreshPlateColors();

            if (icon != null && normalSprite != null)
                icon.sprite = normalSprite;
        }
    }
}
