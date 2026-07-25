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
        Button button;
        Sprite normalSprite;
        Sprite hoverSprite;
        Action onClick;
        bool selected;
        bool locked;
        bool shopOwned;
        CrispUIText crisp;

        const int LabelSizeNormal = 28;
        const int LabelSizeSelected = 28;

        // Portrait stage: icon is larger than the mask so the subject reads bigger.
        const float PortraitSize = 128f;
        const float IconZoom = 1.22f;

        static readonly Color FrameNormal = new Color(0.10f, 0.12f, 0.16f, 0.94f);
        static readonly Color FrameHover = new Color(0.16f, 0.26f, 0.46f, 0.96f);
        static readonly Color FrameSelected = new Color(0.14f, 0.34f, 0.62f, 0.98f);
        static readonly Color FrameOwned = new Color(0.10f, 0.26f, 0.16f, 0.94f);

        // Stage plate — bright center so dark sprites (spider) and green art both pop.
        static readonly Color PlateRim = new Color(0.95f, 0.92f, 0.55f, 1f);
        static readonly Color PlateFill = new Color(0.38f, 0.72f, 0.92f, 1f);
        static readonly Color PlateFillSelected = new Color(0.55f, 0.82f, 1f, 1f);
        static readonly Color PlateFillLocked = new Color(0.28f, 0.30f, 0.34f, 1f);

        public void Build(Transform parent)
        {
            transform.SetParent(parent, false);

            var rect = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(168f, 200f);

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
            var stageRect = stageGo.GetComponent<RectTransform>();
            stageRect.anchorMin = new Vector2(0.5f, 0.58f);
            stageRect.anchorMax = new Vector2(0.5f, 0.58f);
            stageRect.pivot = new Vector2(0.5f, 0.5f);
            stageRect.sizeDelta = new Vector2(PortraitSize + 10f, PortraitSize + 10f);

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
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            var iconSize = PortraitSize * IconZoom;
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = Color.white;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 36f);
            labelRect.sizeDelta = new Vector2(-12f, 32f);
            label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = LabelSizeNormal;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            UiTextStyle.Apply(label);
            crisp = labelGo.AddComponent<CrispUIText>();
            crisp.SetDesignSize(LabelSizeNormal);

            // Shop price row: coin + number
            priceRow = new GameObject("PriceRow", typeof(RectTransform));
            priceRow.transform.SetParent(transform, false);
            var priceRect = priceRow.GetComponent<RectTransform>();
            priceRect.anchorMin = new Vector2(0.5f, 0f);
            priceRect.anchorMax = new Vector2(0.5f, 0f);
            priceRect.pivot = new Vector2(0.5f, 0f);
            priceRect.anchoredPosition = new Vector2(0f, 6f);
            priceRect.sizeDelta = new Vector2(150f, 28f);
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
            priceLabel.fontSize = 24;
            priceLabel.alignment = TextAnchor.MiddleLeft;
            priceLabel.raycastTarget = false;
            UiTextStyle.Apply(priceLabel);
            priceGo.AddComponent<CrispUIText>().SetDesignSize(24);

            // Customize lock icon — bottom right.
            var lockGo = new GameObject("Lock", typeof(RectTransform), typeof(Image));
            lockGo.transform.SetParent(transform, false);
            var lockRect = lockGo.GetComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(1f, 0f);
            lockRect.anchorMax = new Vector2(1f, 0f);
            lockRect.pivot = new Vector2(1f, 0f);
            lockRect.anchoredPosition = new Vector2(-8f, 8f);
            lockRect.sizeDelta = new Vector2(36f, 36f);
            lockIcon = lockGo.GetComponent<Image>();
            lockIcon.sprite = PixelSpriteFactory.CreateLockSprite();
            lockIcon.preserveAspect = true;
            lockIcon.raycastTarget = false;
            lockIcon.color = Color.white;
            CrispVisuals.MakeSpriteCrisp(lockIcon.sprite);
            lockGo.SetActive(false);
        }

        public void Bind(string displayName, Sprite sprite, Action clicked, Sprite hover = null, bool isLocked = false)
        {
            BindInternal(displayName, sprite, clicked, hover, showPrice: false, price: 0, owned: !isLocked, isLocked: isLocked);
        }

        /// <summary>Shop tile: name + art + coin price (or Owned). Never used for character-gated items.</summary>
        public void BindShop(string displayName, Sprite sprite, int price, bool owned, Action clicked, Sprite hover = null)
        {
            BindInternal(displayName, sprite, clicked, hover, showPrice: true, price, owned, isLocked: false);
        }

        void BindInternal(string displayName, Sprite sprite, Action clicked, Sprite hover, bool showPrice, int price, bool owned, bool isLocked)
        {
            normalSprite = sprite;
            hoverSprite = hover;
            onClick = clicked;
            locked = isLocked;
            shopOwned = showPrice && owned && !isLocked;
            selected = false;

            if (label != null)
            {
                label.text = displayName ?? "";
                UiTextStyle.ApplySized(label, LabelSizeNormal, TextAnchor.MiddleCenter);
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
            }

            if (lockIcon != null)
                lockIcon.gameObject.SetActive(isLocked);

            if (priceRow != null)
            {
                priceRow.SetActive(showPrice && !isLocked);
                if (showPrice && priceLabel != null)
                {
                    if (owned)
                    {
                        priceLabel.text = "Owned";
                        if (coinIcon != null) coinIcon.enabled = false;
                    }
                    else if (price <= 0)
                    {
                        priceLabel.text = "Free";
                        if (coinIcon != null) coinIcon.enabled = true;
                    }
                    else
                    {
                        priceLabel.text = price.ToString();
                        if (coinIcon != null) coinIcon.enabled = true;
                    }
                    UiTextStyle.Apply(priceLabel);
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
            {
                var size = selected ? LabelSizeSelected : LabelSizeNormal;
                UiTextStyle.ApplySized(label, size, TextAnchor.MiddleCenter);
            }
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
