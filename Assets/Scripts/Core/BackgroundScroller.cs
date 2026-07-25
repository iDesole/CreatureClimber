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
        Color lastSky = new Color(0.14f, 0.3f, 0.18f);

        class Segment
        {
            public Transform root;
            public SpriteRenderer far;
            public SpriteRenderer near;
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

            ApplySkyForCameraY(camY);
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

            // Full modern phone frame (19.5:9 / 1080×2340) — always cover the camera view.
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
                ScaleRenderer(seg.far, width, height, bg != null ? bg.ResolveSprite() : null);
                if (seg.near != null && seg.near.enabled)
                    ScaleRenderer(seg.near, width * 0.92f, height * 0.85f, bg != null ? bg.nearSprite : null);
            }

            if (seg.near != null && seg.near.enabled && bg != null)
            {
                var parallax = Mathf.Clamp01(bg.nearParallax);
                var camY = mainCamera.transform.position.y;
                var nearY = centerY + (camY - centerY) * parallax * 0.15f;
                seg.near.transform.position = new Vector3(camX, nearY, 0f);
            }
        }

        void ApplyArt(Segment seg, Background bg, float width, float height)
        {
            if (bg != null)
            {
                var farSprite = bg.ResolveSprite();
                seg.far.sprite = farSprite;
                seg.far.color = bg.tint;
                seg.far.sortingOrder = bg.sortingOrder;
                ScaleRenderer(seg.far, width, height, farSprite);

                if (bg.nearSprite != null)
                {
                    seg.near.enabled = true;
                    seg.near.sprite = bg.nearSprite;
                    seg.near.color = bg.nearTint;
                    seg.near.sortingOrder = bg.sortingOrder + 1;
                    ScaleRenderer(seg.near, width * 0.92f, height * 0.85f, bg.nearSprite);
                }
                else
                {
                    seg.near.enabled = false;
                }
            }
            else
            {
                var shade = new Color(0.12f, 0.22f, 0.28f);
                seg.far.sprite = PixelSpriteFactory.CreateSolid(8, 8, shade);
                seg.far.color = Color.white;
                seg.far.sortingOrder = -10;
                ScaleRenderer(seg.far, width, height, seg.far.sprite);
                seg.near.enabled = false;
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
            if (spr.texture != null)
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

        void ApplySkyForCameraY(float camY)
        {
            if (mainCamera == null) return;

            Color sky;
            if (useSingleBackground && activeDb != null && activeDb.Count > 0)
            {
                var only = activeDb.GetBackground(preferredBackgroundIndex);
                if (only != null)
                {
                    sky = only.skyColor;
                    sky.a = 1f;
                    if (sky != lastSky)
                    {
                        lastSky = sky;
                        mainCamera.backgroundColor = sky;
                    }
                    return;
                }
            }

            if (activeDb != null && activeDb.Count > 0)
            {
                var bg = activeDb.GetBackgroundAtWorldY(camY, out _, out _);
                sky = bg != null ? bg.skyColor : lastSky;
            }
            else
            {
                sky = new Color(0.12f, 0.22f, 0.28f);
            }

            sky.a = 1f;
            if (sky != lastSky)
            {
                lastSky = sky;
                mainCamera.backgroundColor = sky;
            }
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

            var farGo = new GameObject("Far");
            farGo.transform.SetParent(root.transform, false);
            var far = farGo.AddComponent<SpriteRenderer>();
            far.sortingOrder = -10;

            var nearGo = new GameObject("Near");
            nearGo.transform.SetParent(root.transform, false);
            var near = nearGo.AddComponent<SpriteRenderer>();
            near.sortingOrder = -9;
            near.enabled = false;

            return new Segment
            {
                root = root.transform,
                far = far,
                near = near,
                bandIndex = int.MinValue
            };
        }
    }
}
