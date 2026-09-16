using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Right-side race track: finish at top, player + AI creature icons by progress.
    /// </summary>
    public class RaceHud : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] RectTransform track;
        [SerializeField] Image trackFill;
        [SerializeField] Image playerIcon;
        [SerializeField] Image aiIcon;
        [SerializeField] Text finishLabel;
        [SerializeField] Text statusLabel;

        const float TrackWidth = 44f;
        const float TrackHeight = 560f;
        const float IconSize = 84f;
        const float MarginRight = 24f;
        const float FinishFlagHeight = 18f;
        // Tight gaps between bar and labels.
        const float LabelGap = 2f;
        // Bake icons larger than on-screen so UI scale stays smooth (not blocky).
        const int IconBakeSize = 256;

        static Sprite s_checkeredSprite;
        static readonly Dictionary<int, Sprite> SmoothIconCache = new();

        public bool IsVisible => root != null && root.activeSelf;

        public void Setup(
            GameObject panelRoot,
            RectTransform trackRect,
            Image fill,
            Image player,
            Image ai,
            Text finish,
            Text status)
        {
            root = panelRoot;
            track = trackRect;
            trackFill = fill;
            playerIcon = player;
            aiIcon = ai;
            finishLabel = finish;
            statusLabel = status;
            Hide();
        }

        public void Show()
        {
            if (root != null)
                root.SetActive(true);
            ApplyLayoutSizes();
            Refresh();
        }

        void ApplyLayoutSizes()
        {
            if (track != null)
            {
                track.sizeDelta = new Vector2(TrackWidth, TrackHeight);
                // Keep track centered with tight label space above/below.
                track.anchoredPosition = Vector2.zero;
            }

            if (trackFill != null)
            {
                var fr = trackFill.rectTransform;
                fr.sizeDelta = new Vector2(TrackWidth - 10f, TrackHeight);
            }

            if (track != null)
            {
                var flag = track.Find("FinishFlag") as RectTransform;
                if (flag == null)
                {
                    var flagGo = new GameObject("FinishFlag", typeof(RectTransform), typeof(Image));
                    flagGo.transform.SetParent(track, false);
                    flag = flagGo.GetComponent<RectTransform>();
                    var img = flagGo.GetComponent<Image>();
                    img.sprite = GetCheckeredSprite();
                    img.type = Image.Type.Simple;
                    img.color = Color.white;
                    img.raycastTarget = false;
                }

                flag.anchorMin = new Vector2(0.5f, 1f);
                flag.anchorMax = new Vector2(0.5f, 1f);
                flag.pivot = new Vector2(0.5f, 0.5f);
                flag.sizeDelta = new Vector2(TrackWidth + 20f, FinishFlagHeight);
                flag.anchoredPosition = Vector2.zero;
            }

            // "100" snug above the checkered flag.
            if (finishLabel != null && track != null)
            {
                var fr = finishLabel.rectTransform;
                fr.SetParent(track, false);
                fr.anchorMin = new Vector2(0.5f, 1f);
                fr.anchorMax = new Vector2(0.5f, 1f);
                fr.pivot = new Vector2(0.5f, 0f);
                fr.sizeDelta = new Vector2(90f, 28f);
                fr.anchoredPosition = new Vector2(0f, FinishFlagHeight * 0.5f + LabelGap);
            }

            // "LEAD" snug under the bar.
            if (statusLabel != null && track != null)
            {
                var sr = statusLabel.rectTransform;
                sr.SetParent(track, false);
                sr.anchorMin = new Vector2(0.5f, 0f);
                sr.anchorMax = new Vector2(0.5f, 0f);
                sr.pivot = new Vector2(0.5f, 1f);
                sr.sizeDelta = new Vector2(100f, 28f);
                sr.anchoredPosition = new Vector2(0f, -LabelGap);
            }

            if (root != null)
            {
                var rr = root.GetComponent<RectTransform>();
                if (rr != null)
                    rr.sizeDelta = new Vector2(110f, TrackHeight + 80f);
            }
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }

        public void Refresh()
        {
            var race = RaceController.Instance;
            if (race == null) return;

            var finish = Mathf.Max(1, race.FinishHeight);
            var pT = Mathf.Clamp01(race.PlayerHeight / (float)finish);
            var aT = Mathf.Clamp01(race.AiHeight / (float)finish);

            PlaceIcon(playerIcon, pT);
            PlaceIcon(aiIcon, aT);
            ApplyMiniCreature(playerIcon, race.PlayerCreature);
            ApplyMiniCreature(aiIcon, race.Opponent);

            if (playerIcon != null && aiIcon != null)
            {
                if (pT >= aT)
                    playerIcon.transform.SetAsLastSibling();
                else
                    aiIcon.transform.SetAsLastSibling();
            }

            if (finishLabel != null)
            {
                finishLabel.text = finish.ToString();
                UiTextStyle.ApplySized(finishLabel, UiTextStyle.CaptionFontSize, TextAnchor.MiddleCenter);
            }

            if (statusLabel != null)
            {
                if (race.PlayerWon == true)
                    statusLabel.text = "WIN";
                else if (race.PlayerWon == false)
                    statusLabel.text = "LOSE";
                else if (race.PlayerHeight >= race.AiHeight)
                    statusLabel.text = "LEAD";
                else
                    statusLabel.text = "BEHIND";
                UiTextStyle.ApplySized(statusLabel, 32, TextAnchor.MiddleCenter);
            }
        }

        void PlaceIcon(Image icon, float t01)
        {
            if (icon == null || track == null) return;
            var rt = icon.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // Whole-pixel size/position reduces UI blur.
            var size = Mathf.Round(IconSize);
            rt.sizeDelta = new Vector2(size, size);
            var y = Mathf.Round(t01 * TrackHeight);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        static void ApplyMiniCreature(Image icon, Creature creature)
        {
            if (icon == null) return;
            if (creature == null)
            {
                icon.enabled = false;
                return;
            }

            var source = creature.ResolveSprite();
            icon.enabled = source != null;
            if (source == null) return;

            // High-res bilinear bake so mini icons look smooth, not chunky.
            var smooth = GetSmoothHudSprite(source, IconBakeSize);
            icon.sprite = smooth != null ? smooth : source;
            icon.preserveAspect = true;
            icon.color = creature.tint;
            icon.raycastTarget = false;
            icon.type = Image.Type.Simple;
            icon.useSpriteMesh = false;
        }

        /// <summary>
        /// Bakes a high-res bilinear sprite for the race bar so icons stay smooth (not blocky).
        /// Leaves the original in-world texture filter alone.
        /// </summary>
        static Sprite GetSmoothHudSprite(Sprite source, int size)
        {
            if (source == null || source.texture == null) return null;

            size = Mathf.Clamp(size, 64, 512);
            var key = source.GetInstanceID() ^ (size * 73856093);
            if (SmoothIconCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var srcTex = source.texture;
            var prevFilter = srcTex.filterMode;
            srcTex.filterMode = FilterMode.Bilinear;

            // Intermediate RT at bake size — bilinear scale from source.
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevRt = RenderTexture.active;

            // Full-texture blit works for our single-PNG creature sprites.
            Graphics.Blit(srcTex, rt);

            var outTex = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 2,
                name = $"RaceHudIcon_{source.name}"
            };

            RenderTexture.active = rt;
            outTex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            outTex.Apply(true, true); // mipmaps help smooth UI scale

            RenderTexture.active = prevRt;
            RenderTexture.ReleaseTemporary(rt);
            srcTex.filterMode = prevFilter;

            var sp = Sprite.Create(
                outTex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sp.name = outTex.name;
            SmoothIconCache[key] = sp;
            return sp;
        }

        void Update()
        {
            if (!IsVisible) return;
            var race = RaceController.Instance;
            if (race != null && (race.IsRacing || race.PlayerWon != null))
                Refresh();
        }

        public static RaceHud Build(Transform parent)
        {
            var root = new GameObject("RaceHud", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0.5f);
            rootRect.anchorMax = new Vector2(1f, 0.5f);
            rootRect.pivot = new Vector2(1f, 0.5f);
            rootRect.anchoredPosition = new Vector2(-MarginRight, 0f);
            rootRect.sizeDelta = new Vector2(110f, TrackHeight + 80f);

            var trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(root.transform, false);
            var trackRect = trackGo.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0.5f, 0.5f);
            trackRect.anchorMax = new Vector2(0.5f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.sizeDelta = new Vector2(TrackWidth, TrackHeight);
            trackRect.anchoredPosition = Vector2.zero;
            var trackImg = trackGo.GetComponent<Image>();
            trackImg.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);
            trackImg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.5f, 0f);
            fillRect.anchorMax = new Vector2(0.5f, 0f);
            fillRect.pivot = new Vector2(0.5f, 0f);
            fillRect.sizeDelta = new Vector2(TrackWidth - 10f, TrackHeight);
            fillRect.anchoredPosition = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = new Color(0.15f, 0.35f, 0.55f, 0.35f);
            fillImg.raycastTarget = false;

            var flagGo = new GameObject("FinishFlag", typeof(RectTransform), typeof(Image));
            flagGo.transform.SetParent(trackGo.transform, false);
            var flagRect = flagGo.GetComponent<RectTransform>();
            flagRect.anchorMin = new Vector2(0.5f, 1f);
            flagRect.anchorMax = new Vector2(0.5f, 1f);
            flagRect.pivot = new Vector2(0.5f, 0.5f);
            flagRect.sizeDelta = new Vector2(TrackWidth + 20f, FinishFlagHeight);
            flagRect.anchoredPosition = Vector2.zero;
            var flagImg = flagGo.GetComponent<Image>();
            flagImg.sprite = GetCheckeredSprite();
            flagImg.type = Image.Type.Simple;
            flagImg.color = Color.white;
            flagImg.raycastTarget = false;

            // Labels parented to track so gaps stay tight.
            var finishGo = new GameObject("Finish", typeof(RectTransform), typeof(Text));
            finishGo.transform.SetParent(trackGo.transform, false);
            var finishRect = finishGo.GetComponent<RectTransform>();
            finishRect.anchorMin = new Vector2(0.5f, 1f);
            finishRect.anchorMax = new Vector2(0.5f, 1f);
            finishRect.pivot = new Vector2(0.5f, 0f);
            finishRect.anchoredPosition = new Vector2(0f, FinishFlagHeight * 0.5f + LabelGap);
            finishRect.sizeDelta = new Vector2(90f, 28f);
            var finishText = finishGo.GetComponent<Text>();
            finishText.alignment = TextAnchor.MiddleCenter;
            finishText.raycastTarget = false;
            if (finishText.font == null)
                finishText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            finishText.text = "100";
            UiTextStyle.ApplySized(finishText, UiTextStyle.CaptionFontSize, TextAnchor.MiddleCenter);

            var playerImg = CreateIcon(trackGo.transform, "PlayerIcon");
            var aiImg = CreateIcon(trackGo.transform, "AiIcon");

            var statusGo = new GameObject("Status", typeof(RectTransform), typeof(Text));
            statusGo.transform.SetParent(trackGo.transform, false);
            var statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0f);
            statusRect.anchorMax = new Vector2(0.5f, 0f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -LabelGap);
            statusRect.sizeDelta = new Vector2(100f, 28f);
            var statusText = statusGo.GetComponent<Text>();
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.raycastTarget = false;
            if (statusText.font == null)
                statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.text = "RACE";
            UiTextStyle.ApplySized(statusText, 32, TextAnchor.MiddleCenter);

            var hud = root.AddComponent<RaceHud>();
            hud.Setup(root, trackRect, fillImg, playerImg, aiImg, finishText, statusText);
            return hud;
        }

        static Image CreateIcon(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(IconSize, IconSize);
            return img;
        }

        static Sprite GetCheckeredSprite()
        {
            if (s_checkeredSprite != null) return s_checkeredSprite;
            s_checkeredSprite = CreateCheckeredUiSprite();
            return s_checkeredSprite;
        }

        static Sprite CreateCheckeredUiSprite()
        {
            const int w = 48;
            const int h = 16;
            const int cell = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var black = (((x / cell) + (y / cell)) & 1) == 0;
                    tex.SetPixel(x, y, black ? Color.black : Color.white);
                }
            }

            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
