using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Pixel-art / UI sharpness helpers: point sampling, integer zoom, position snap.
    /// </summary>
    public static class CrispVisuals
    {
        public const int SpritePPU = 16;

        /// <summary>
        /// Integer screen pixels per source sprite pixel at scale 1 (with phone ortho).
        /// </summary>
        public const int PixelZoom = 11;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            Font.textureRebuilt += KeepFontSmooth;
        }

        static void KeepFontSmooth(Font font)
        {
            if (font == null || font.material == null) return;
            var tex = font.material.mainTexture;
            if (tex != null)
                tex.filterMode = FilterMode.Bilinear;
        }

        /// <summary>Ortho half-height so 1 sprite pixel maps to <see cref="PixelZoom"/> screen pixels.</summary>
        public static float PixelPerfectOrthoSize(float screenHeight = -1f)
        {
            if (screenHeight < 1f)
                screenHeight = PlatformRuntime.ActiveHeight;
            return screenHeight / (2f * SpritePPU * PixelZoom);
        }

        /// <summary>Snap a world-space scale so sprite pixels land on whole screen pixels.</summary>
        public static float SnapSpriteScale(float scale)
        {
            // At scale 1 → PixelZoom screen px per sprite px.
            // Large source art needs tiny transform scales; never force a 1-screen-px floor
            // (that made every Scale value look the same for big sprites).
            scale = Mathf.Max(0.01f, scale);
            var screenPx = PixelZoom * scale;
            if (screenPx < 1f)
                return scale;

            var snapped = Mathf.Round(screenPx);
            return Mathf.Max(1f, snapped) / PixelZoom;
        }

        public static void MakeSpriteCrisp(Sprite sprite)
        {
            if (sprite == null) return;
            var tex = sprite.texture;
            if (tex == null) return;
            if (tex.filterMode != FilterMode.Point)
                tex.filterMode = FilterMode.Point;
            tex.anisoLevel = 0;
            tex.mipMapBias = -0.5f;
            if (tex.wrapMode != TextureWrapMode.Clamp)
                tex.wrapMode = TextureWrapMode.Clamp;
        }

        public static void MakeSpriteCrisp(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            MakeSpriteCrisp(renderer.sprite);
            // Avoid sub-pixel mesh expansion from lighting.
            renderer.allowOcclusionWhenDynamic = false;
        }

        public static Vector3 SnapWorldPosition(Vector3 world, Camera cam = null)
        {
            cam = cam != null ? cam : Camera.main;
            if (cam == null || !cam.orthographic)
                return world;

            // World units per screen pixel.
            var upp = (cam.orthographicSize * 2f) / Mathf.Max(1, Screen.height);
            world.x = Mathf.Round(world.x / upp) * upp;
            world.y = Mathf.Round(world.y / upp) * upp;
            return world;
        }

        public static void SnapRectToPixels(RectTransform rect, Canvas canvas = null)
        {
            if (rect == null) return;
            if (canvas == null)
                canvas = rect.GetComponentInParent<Canvas>();
            var root = canvas != null ? canvas.rootCanvas : null;
            var scale = root != null ? root.scaleFactor : 1f;
            if (scale < 0.01f) scale = 1f;

            var pos = rect.anchoredPosition;
            pos.x = Mathf.Round(pos.x * scale) / scale;
            pos.y = Mathf.Round(pos.y * scale) / scale;
            rect.anchoredPosition = pos;

            var size = rect.sizeDelta;
            size.x = Mathf.Round(size.x * scale) / scale;
            size.y = Mathf.Round(size.y * scale) / scale;
            rect.sizeDelta = size;
        }

        public static void MakeUiTextCrisp(Text label) => MakeUiTextSmooth(label);

        /// <summary>Bilinear glyphs — point sampling made outlines look static / noisy.</summary>
        public static void MakeUiTextSmooth(Text label)
        {
            if (label == null) return;
            label.fontSize = Mathf.Max(1, label.fontSize);
            label.alignByGeometry = false;
            if (label.font != null && label.font.material != null)
            {
                var tex = label.font.material.mainTexture;
                if (tex != null && tex.filterMode != FilterMode.Bilinear)
                    tex.filterMode = FilterMode.Bilinear;
            }
        }
    }
}
