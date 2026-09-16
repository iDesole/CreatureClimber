using System.Collections.Generic;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Vertical looping backdrop driven by <see cref="BackgroundDatabase"/>.
    /// As the camera climbs, bands cycle through your list (A → B → C → A …).
    /// </summary>
    public class BackgroundScroller : MonoBehaviour
    {
        [SerializeField] BackgroundDatabase backgroundDatabase;
        [SerializeField] int segmentPoolSize = 8;
        [SerializeField] float fallbackBandHeight = 12f;
        [SerializeField] int preferredBackgroundIndex;
        [SerializeField] bool useSingleBackground;

        readonly List<Segment> segments = new();
        Camera mainCamera;
        Transform container;
        BackgroundDatabase activeDb;

        class Segment
        {
            public Transform root;
            public SpriteRenderer renderer;
            public int bandIndex = int.MinValue;
        }

        void Awake()
        {
            mainCamera = Camera.main;
            EnsureContainer();
            EnsurePool();
            BindDatabase(BackgroundDatabase.Load() ?? backgroundDatabase);
        }

        public void BindDatabase(BackgroundDatabase database)
        {
            BindDatabase(database, preferredBackgroundIndex, useSingleBackground);
        }

        /// <param name="startIndex">Selected stage index (Smash-style pick).</param>
        /// <param name="singleStage">If true, only that background repeats for the whole climb.</param>
        public void BindDatabase(BackgroundDatabase database, int startIndex, bool singleStage = true)
        {
            backgroundDatabase = database;
            activeDb = database;
            preferredBackgroundIndex = Mathf.Max(0, startIndex);
            useSingleBackground = singleStage;
            for (var i = 0; i < segments.Count; i++)
                segments[i].bandIndex = int.MinValue;
        }

        public void CreateDefaultLayers(Transform parent)
        {
            if (parent != null)
                transform.SetParent(parent, false);

            EnsureContainer();
            EnsurePool();
            BindDatabase(BackgroundDatabase.Load() ?? backgroundDatabase);
            RefreshSegments();
        }

        public void FitLayersToCamera()
        {
            RefreshSegments();
        }

        public void ResetToOrigin()
        {
            for (var i = 0; i < segments.Count; i++)
                segments[i].bandIndex = int.MinValue;
            RefreshSegments();
        }

        void LateUpdate()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            RefreshSegments();
        }

        void RefreshSegments()
        {
            EnsureContainer();
            EnsurePool();

            if (mainCamera == null) return;

            var camY = mainCamera.transform.position.y;
            var camX = mainCamera.transform.position.x;
            var viewHalf = mainCamera.orthographic ? mainCamera.orthographicSize : 5f;
            var viewBottom = camY - viewHalf * 1.4f;
            var viewTop = camY + viewHalf * 1.4f;

            CollectVisibleBands(viewBottom, viewTop, out var firstBand, out var lastBand);

            var needed = lastBand - firstBand + 1;
            while (segments.Count < needed + 2)
                segments.Add(CreateSegment(segments.Count));

            var slot = 0;
            for (var band = firstBand; band <= lastBand; band++)
            {
                if (slot >= segments.Count)
                    break;

                PlaceSegment(segments[slot], band, camX, viewHalf);
                slot++;
            }

            for (; slot < segments.Count; slot++)
            {
                if (segments[slot].root.gameObject.activeSelf)
                    segments[slot].root.gameObject.SetActive(false);
                segments[slot].bandIndex = int.MinValue;
            }
        }

        void CollectVisibleBands(float viewBottom, float viewTop, out int firstBand, out int lastBand)
        {
            firstBand = BandIndexAtY(viewBottom, out _);
            lastBand = BandIndexAtY(viewTop, out _);
            if (lastBand < firstBand)
                (firstBand, lastBand) = (lastBand, firstBand);

            firstBand--;
            lastBand++;
        }

        int BandIndexAtY(float worldY, out float bandBottomY)
        {
            if (useSingleBackground && activeDb != null && activeDb.Count > 0)
            {
                var h = BandHeight(0);
                var idx = Mathf.FloorToInt(worldY / h);
                bandBottomY = idx * h;
                return idx;
            }

            if (activeDb != null && activeDb.Count > 0)
            {
                activeDb.GetBackgroundAtWorldY(worldY, out var index, out bandBottomY);
                return index;
            }

            var fh = fallbackBandHeight;
            var fi = Mathf.FloorToInt(worldY / fh);
            bandBottomY = fi * fh;
            return fi;
        }

        float BandBottomY(int bandIndex)
        {
            if (useSingleBackground && activeDb != null && activeDb.Count > 0)
                return bandIndex * BandHeight(0);

            if (activeDb == null || activeDb.Count == 0)
                return bandIndex * fallbackBandHeight;

            var count = activeDb.Count;
            var cycle = activeDb.CycleHeight();
            var loops = bandIndex >= 0
                ? bandIndex / count
                : (bandIndex - (count - 1)) / count;
            var localIndex = bandIndex - loops * count;
            if (localIndex < 0) localIndex += count;

            var y = loops * cycle;
            for (var i = 0; i < localIndex; i++)
            {
                var bg = activeDb.GetBackground(i + preferredBackgroundIndex);
                y += bg != null ? bg.ResolvedHeight : fallbackBandHeight;
            }

            return y;
        }

        float BandHeight(int bandIndex)
        {
            var bg = ResolveBandBackground(bandIndex);
            if (bg != null) return bg.ResolvedHeight;
            return fallbackBandHeight;
        }

        Background ResolveBandBackground(int bandIndex)
        {
            if (activeDb == null || activeDb.Count == 0)
                return null;

            if (useSingleBackground)
                return activeDb.GetBackground(preferredBackgroundIndex);

            // Start climb on the selected stage, then continue looping the list.
            return activeDb.GetBackground(bandIndex + preferredBackgroundIndex);
        }

        void PlaceSegment(Segment seg, int bandIndex, float camX, float viewHalf)
        {
            var bottom = BandBottomY(bandIndex);
            var height = BandHeight(bandIndex);
            var centerY = bottom + height * 0.5f;

            if (!seg.root.gameObject.activeSelf)
                seg.root.gameObject.SetActive(true);

            seg.root.position = new Vector3(camX, centerY, 0f);

            // Cover the live camera view (common frame is 20:9 / 1080×2400).
            var aspect = mainCamera != null
                ? mainCamera.aspect
                : PlatformRuntime.ScreenAspect;
            var width = viewHalf * 2f * aspect * 1.15f;

            var bg = ResolveBandBackground(bandIndex);

            if (seg.bandIndex != bandIndex)
            {
                seg.bandIndex = bandIndex;
                ApplyArt(seg, bg, width, height);
            }
            else
            {
                ScaleRenderer(seg.renderer, width, height, bg != null ? bg.ResolveSprite() : null);
            }
        }

        void ApplyArt(Segment seg, Background bg, float width, float height)
        {
            if (bg != null)
            {
                var spr = bg.ResolveSprite();
                seg.renderer.sprite = spr;
                seg.renderer.color = bg.tint;
                seg.renderer.sortingOrder = bg.sortingOrder;
                ScaleRenderer(seg.renderer, width, height, spr);
            }
            else
            {
                var shade = new Color(0.12f, 0.22f, 0.28f);
                seg.renderer.sprite = PixelSpriteFactory.CreateSolid(8, 8, shade);
                seg.renderer.color = Color.white;
                seg.renderer.sortingOrder = -10;
                ScaleRenderer(seg.renderer, width, height, seg.renderer.sprite);
            }
        }

        static void ScaleRenderer(SpriteRenderer renderer, float worldWidth, float worldHeight, Sprite sprite)
        {
            if (renderer == null) return;

            var spr = sprite != null ? sprite : renderer.sprite;
            if (spr == null)
            {
                renderer.transform.localScale = Vector3.one;
                return;
            }

            // Backgrounds are large stretched fills — bilinear looks better than point.
            // Only write when needed (texture property sets are not free on mobile).
            if (spr.texture != null && spr.texture.filterMode != FilterMode.Bilinear)
                spr.texture.filterMode = FilterMode.Bilinear;

            var spriteW = spr.bounds.size.x;
            var spriteH = spr.bounds.size.y;
            if (spriteW < 0.0001f) spriteW = 1f;
            if (spriteH < 0.0001f) spriteH = 1f;

            renderer.transform.localScale = new Vector3(
                worldWidth / spriteW,
                worldHeight / spriteH,
                1f);
        }

        void EnsureContainer()
        {
            if (container != null) return;
            var go = new GameObject("BackgroundSegments");
            go.transform.SetParent(transform, false);
            container = go.transform;
        }

        void EnsurePool()
        {
            while (segments.Count < segmentPoolSize)
                segments.Add(CreateSegment(segments.Count));
        }

        Segment CreateSegment(int index)
        {
            var root = new GameObject($"BgSegment_{index}");
            root.transform.SetParent(container != null ? container : transform, false);

            var layerGo = new GameObject("Layer");
            layerGo.transform.SetParent(root.transform, false);
            var layer = layerGo.AddComponent<SpriteRenderer>();
            layer.sortingOrder = -10;

            return new Segment
            {
                root = root.transform,
                renderer = layer,
                bandIndex = int.MinValue
            };
        }
    }
}
