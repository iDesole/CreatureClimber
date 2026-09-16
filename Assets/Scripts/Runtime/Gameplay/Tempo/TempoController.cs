using System.Collections;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Tempo mode: auto-scroll camera + rising ocean death zone at the bottom of the view.
    /// Ocean is parented to the camera so it always fills the lower frame.
    /// Pads that touch the waterline are washed away; on defeat the wave flushes upward then fades.
    /// </summary>
    public class TempoController : MonoBehaviour
    {
        public static TempoController Instance { get; private set; }

        public bool IsRunning { get; private set; }
        /// <summary>True while the defeat surge/fade is playing (ocean still visible).</summary>
        public bool IsFlushing => flushing;
        public float CurrentSpeed { get; private set; }
        public float Elapsed { get; private set; }
        public float WaveT { get; private set; }
        /// <summary>World Y of the ocean surface — die below this.</summary>
        public float KillLineY { get; private set; }

        Difficulty difficulty;
        Transform player;
        LeafSpawner spawner;
        Camera cam;
        float graceSeconds;
        bool leftBehindReported;
        bool flushing;
        float flushT;
        Coroutine flushRoutine;

        Transform oceanRoot;
        SpriteRenderer deepWater;
        SpriteRenderer midWater;
        SpriteRenderer surfaceSheen;
        SpriteRenderer foamLine;
        SpriteRenderer waveTop;

        static Sprite s_deep;
        static Sprite s_mid;
        static Sprite s_sheen;
        static Sprite s_foam;
        static Sprite s_wave;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            DestroyOcean();
        }

        public static TempoController Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("--- Tempo ---");
            return go.AddComponent<TempoController>();
        }

        public void Begin(Transform playerTransform, Difficulty diff, LeafSpawner leafSpawner = null)
        {
            if (flushRoutine != null)
            {
                StopCoroutine(flushRoutine);
                flushRoutine = null;
            }

            player = playerTransform;
            spawner = leafSpawner != null ? leafSpawner : FindFirstObjectByType<LeafSpawner>();
            difficulty = diff;
            graceSeconds = GameModeRules.TempoGraceSeconds(diff);
            Elapsed = 0f;
            CurrentSpeed = 0f;
            WaveT = 0.5f;
            leftBehindReported = false;
            flushing = false;
            flushT = 0f;
            IsRunning = true;

            cam = Camera.main;
            if (cam == null)
                cam = FindFirstObjectByType<Camera>();

            var startY = player != null
                ? player.position.y + 1.5f * GameSettings.ClimbSign
                : 0f;
            if (cam != null)
                cam.transform.position = new Vector3(0f, startY, cam.transform.position.z);

            CameraFollow.BeginAutoScroll(startY);
            CameraFollow.SetAutoScrollSpeed(0f);

            // Always rebuild so we never keep a stale hierarchy from older builds.
            BuildOcean();
            RefreshOcean(1f, 0f);
        }

        /// <summary>Instant teardown (menu / restart). Prefer <see cref="BeginDefeatFlush"/> on loss.</summary>
        public void Stop()
        {
            if (flushRoutine != null)
            {
                StopCoroutine(flushRoutine);
                flushRoutine = null;
            }

            flushing = false;
            flushT = 0f;
            IsRunning = false;
            CurrentSpeed = 0f;
            CameraFollow.EndAutoScroll();
            DestroyOcean();
        }

        /// <summary>
        /// Player lost to the wave — surge upward and fade out instead of vanishing.
        /// </summary>
        public void BeginDefeatFlush()
        {
            if (flushing) return;
            leftBehindReported = true;
            flushing = true;
            flushT = 0f;
            CurrentSpeed = 0f;
            CameraFollow.SetAutoScrollSpeed(0f);

            if (flushRoutine != null)
                StopCoroutine(flushRoutine);
            flushRoutine = StartCoroutine(DefeatFlushRoutine());
        }

        IEnumerator DefeatFlushRoutine()
        {
            // Keep visual updates while flushing even though IsRunning may stay true briefly.
            const float duration = 1.35f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                flushT = Mathf.Clamp01(elapsed / duration);
                // Ease: quick rise, then settle/fade.
                var rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flushT / 0.55f));
                var fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((flushT - 0.35f) / 0.65f));
                RefreshOcean(Mathf.Max(0.05f, fade), rise);
                // Keep washing pads the surge reaches.
                WashPlatformsTouchingWave();
                yield return null;
            }

            IsRunning = false;
            flushing = false;
            flushT = 0f;
            flushRoutine = null;
            CameraFollow.EndAutoScroll();
            DestroyOcean();
        }

        void Update()
        {
            if (!IsRunning || flushing) return;

            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.State == GameState.Paused || gm.State == GameState.GameOver || gm.State == GameState.Ready)
                return;

            Elapsed += Time.deltaTime;
            CurrentSpeed = EvaluateScrollSpeed(Elapsed);
            CameraFollow.SetAutoScrollSpeed(CurrentSpeed);
        }

        void LateUpdate()
        {
            if (flushing)
                return;

            if (!IsRunning) return;

            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.State == GameState.Paused || gm.State == GameState.GameOver || gm.State == GameState.Ready)
                return;

            var visibility = Elapsed < graceSeconds ? 0.65f : 1f;
            var proximity = RefreshOcean(visibility, 0f);

            // Pads that dip into the waterline get destroyed (after grace).
            if (Elapsed >= graceSeconds)
                WashPlatformsTouchingWave();

            if (Elapsed < graceSeconds) return;
            if (leftBehindReported) return;
            if (gm.State != GameState.Playing && gm.State != GameState.Falling) return;

            if (proximity > 0.55f)
                CameraFollow.Shake(0.02f * proximity, 0.05f);

            if (player != null && (player.position.y - KillLineY) * GameSettings.ClimbSign < 0f)
            {
                leftBehindReported = true;
                gm.OnTempoLeftBehind();
            }
        }

        void WashPlatformsTouchingWave()
        {
            if (spawner == null)
                spawner = FindFirstObjectByType<LeafSpawner>();
            if (spawner == null) return;

            var killY = KillLineY;
            var sign = GameSettings.ClimbSign;
            // Slight margin so the foam crest also counts as contact.
            var washLine = killY + 0.2f * sign;

            Leaf washedStanding = null;
            var leaves = spawner.ActiveLeaves;
            for (var i = 0; i < leaves.Count; i++)
            {
                var leaf = leaves[i];
                if (leaf == null || !leaf.gameObject.activeInHierarchy || leaf.IsBroken)
                    continue;

                // Pad body sits near transform.y; wash when the wave has reached it.
                if ((leaf.transform.position.y - washLine) * sign > 0f)
                    continue;

                if (leaf == spawner.CurrentLeaf)
                    washedStanding = leaf;

                leaf.WashAway();
            }

            // If the pad under the player is gone, they drop.
            if (washedStanding == null) return;

            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            var playerCtrl = player != null ? player.GetComponent<PlayerController>() : null;
            if (playerCtrl != null && !playerCtrl.IsBusy)
                playerCtrl.ForceFall(interruptBusy: false, breakPad: false);
        }

        float EvaluateScrollSpeed(float t)
        {
            var intro = Mathf.Clamp01(t / Mathf.Max(0.35f, graceSeconds * 0.85f));
            var rampT = Mathf.Clamp01(t / GameModeRules.TempoRampSeconds(difficulty));
            var rampEase = 1f - (1f - rampT) * (1f - rampT);
            var baseSpeed = Mathf.Lerp(
                GameModeRules.TempoBaseSpeed(difficulty),
                GameModeRules.TempoMaxBaseSpeed(difficulty),
                rampEase);

            var period = GameModeRules.TempoWavePeriod(difficulty);
            var phase = (t / Mathf.Max(0.5f, period)) * Mathf.PI * 2f;
            WaveT = 0.5f + 0.5f * Mathf.Sin(phase);
            var amp = GameModeRules.TempoWaveAmplitude(difficulty);
            var mul = Mathf.Lerp(1f - amp, 1f + amp, WaveT);
            return baseSpeed * mul * intro;
        }

        /// <summary>
        /// Positions ocean in camera-local space. Returns proximity 0–1 (1 = at waterline).
        /// <paramref name="flushRise"/> 0 = normal zone, 1 = wave fills most of the view.
        /// </summary>
        float RefreshOcean(float visibility, float flushRise)
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return 0f;
            }

            // Keep parented to the live camera (MainCamera can change in editor).
            if (oceanRoot != null && oceanRoot.parent != cam.transform)
                oceanRoot.SetParent(cam.transform, false);

            var ortho = cam.orthographic ? cam.orthographicSize : 5f;
            var zoneH = GameModeRules.TempoDeathZoneHeight(ortho);
            // Defeat flush: surface surges up toward the top of the view.
            var surge = Mathf.Clamp01(flushRise);
            zoneH = Mathf.Lerp(zoneH, ortho * 1.85f, surge);

            var aspect = cam.aspect > 0.01f ? cam.aspect : PlatformRuntime.ScreenAspect;
            var width = ortho * 2f * aspect * 1.2f;

            var camY = cam.transform.position.y;
            var sign = GameSettings.ClimbSign;
            var viewFallEdge = camY - sign * ortho;
            KillLineY = viewFallEdge + sign * zoneH;

            var vis = Mathf.Clamp01(visibility);
            var t = Time.unscaledTime;
            // Visual bob only during normal play — flush keeps a steady surge.
            var bob = surge > 0.01f
                ? Mathf.Sin(t * 6f) * 0.08f * (1f - surge)
                : Mathf.Sin(t * 1.8f) * 0.05f + Mathf.Sin(t * 3.1f) * 0.025f;

            // Camera-local: fall edge is y = -sign * ortho (bottom normally, top when mirrored).
            var localFallEdge = -sign * ortho;
            var localSurface = localFallEdge + sign * zoneH + bob * sign;
            var localBottom = localFallEdge - sign * (0.5f + surge * ortho * 0.3f);
            var waterH = Mathf.Abs(localSurface - localBottom);

            if (oceanRoot != null)
            {
                oceanRoot.localPosition = Vector3.zero;
                oceanRoot.localRotation = Quaternion.identity;
                oceanRoot.localScale = Vector3.one;
                oceanRoot.gameObject.SetActive(vis > 0.01f);
            }

            // Deep fill — solid blue mass of water.
            if (deepWater != null)
            {
                var center = (localSurface + localBottom) * 0.5f;
                deepWater.transform.localPosition = new Vector3(0f, center, 1f);
                SetSize(deepWater, width * (1f + 0.08f * surge), waterH);
                var c = deepWater.color;
                c.a = vis * (0.92f + 0.08f * surge);
                deepWater.color = c;
            }

            // Mid water band under the surface (lighter).
            if (midWater != null)
            {
                var midH = zoneH * 0.55f;
                midWater.transform.localPosition = new Vector3(
                    Mathf.Sin(t * 0.6f) * 0.2f,
                    localSurface - sign * midH * 0.45f,
                    1f);
                SetSize(midWater, width * 1.05f, midH);
                var c = midWater.color;
                c.a = vis * (0.45f + 0.1f * Mathf.Sin(t * 2f));
                midWater.color = c;
            }

            // Surface sheen.
            if (surfaceSheen != null)
            {
                var sh = Mathf.Max(0.4f, zoneH * 0.32f);
                surfaceSheen.transform.localPosition = new Vector3(
                    Mathf.Sin(t * 0.85f) * 0.15f,
                    localSurface - sign * sh * 0.3f,
                    1f);
                SetSize(surfaceSheen, width, sh);
                var c = surfaceSheen.color;
                c.a = vis * (0.4f + 0.12f * Mathf.Sin(t * 3.2f));
                surfaceSheen.color = c;
            }

            // Foam line at the surface (white, wavy).
            if (foamLine != null)
            {
                var fh = Mathf.Max(0.22f, zoneH * 0.12f);
                foamLine.transform.localPosition = new Vector3(
                    0f,
                    localSurface + Mathf.Sin(t * 4f) * 0.03f,
                    1f);
                var fw = width * (1f + 0.015f * Mathf.Sin(t * 2.8f) + 0.05f * surge);
                SetSize(foamLine, fw, fh);
                var c = foamLine.color;
                c.a = vis * (0.85f + 0.1f * Mathf.Sin(t * 5f));
                foamLine.color = c;
            }

            // Soft wave crest above foam.
            if (waveTop != null)
            {
                var wh = Mathf.Max(0.2f, zoneH * 0.1f);
                waveTop.transform.localPosition = new Vector3(
                    Mathf.Sin(t * 1.3f) * 0.1f,
                    localSurface + sign * (wh * 0.35f + Mathf.Sin(t * 3.5f) * 0.04f),
                    1f);
                SetSize(waveTop, width * 0.98f, wh);
                var c = waveTop.color;
                c.a = vis * (0.35f + 0.15f * Mathf.Sin(t * 4.5f));
                waveTop.color = c;
            }

            if (player == null) return 0f;
            var safeSpan = Mathf.Max(0.5f, ortho * 0.9f);
            var distAhead = (player.position.y - KillLineY) * sign;
            return 1f - Mathf.Clamp01(distAhead / safeSpan);
        }

        void BuildOcean()
        {
            DestroyOcean();
            EnsureSprites();

            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
            }

            var root = new GameObject("TempoOcean");
            root.transform.SetParent(cam.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            oceanRoot = root.transform;

            deepWater = MakeLayer(oceanRoot, "DeepWater", s_deep, 40);
            midWater = MakeLayer(oceanRoot, "MidWater", s_mid, 41);
            surfaceSheen = MakeLayer(oceanRoot, "SurfaceSheen", s_sheen, 42);
            foamLine = MakeLayer(oceanRoot, "FoamLine", s_foam, 43);
            waveTop = MakeLayer(oceanRoot, "WaveTop", s_wave, 44);

            deepWater.color = Color.white;
            midWater.color = Color.white;
            surfaceSheen.color = Color.white;
            foamLine.color = Color.white;
            waveTop.color = Color.white;
        }

        void DestroyOcean()
        {
            if (oceanRoot != null)
            {
                Destroy(oceanRoot.gameObject);
                oceanRoot = null;
            }

            deepWater = null;
            midWater = null;
            surfaceSheen = null;
            foamLine = null;
            waveTop = null;
        }

        static SpriteRenderer MakeLayer(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.maskInteraction = SpriteMaskInteraction.None;
            return sr;
        }

        static void SetSize(SpriteRenderer sr, float worldW, float worldH)
        {
            if (sr == null || sr.sprite == null) return;
            var b = sr.sprite.bounds.size;
            if (b.x < 0.0001f) b.x = 1f;
            if (b.y < 0.0001f) b.y = 1f;
            sr.transform.localScale = new Vector3(worldW / b.x, worldH / b.y, 1f);
        }

        static void EnsureSprites()
        {
            if (s_deep == null) s_deep = CreateDeepWaterSprite(128, 64);
            if (s_mid == null) s_mid = CreateMidWaterSprite(128, 48);
            if (s_sheen == null) s_sheen = CreateSheenSprite(128, 32);
            if (s_foam == null) s_foam = CreateFoamSprite(160, 20);
            if (s_wave == null) s_wave = CreateWaveSpraySprite(128, 24);
        }

        public static void InvalidateSpriteCache()
        {
            s_deep = null;
            s_mid = null;
            s_sheen = null;
            s_foam = null;
            s_wave = null;
        }

        static Sprite CreateDeepWaterSprite(int w, int h)
        {
            var tex = NewTex(w, h);
            for (var y = 0; y < h; y++)
            {
                var v = y / (float)(h - 1);
                var r = Mathf.Lerp(0.02f, 0.08f, v);
                var g = Mathf.Lerp(0.12f, 0.35f, v);
                var b = Mathf.Lerp(0.28f, 0.55f, v);
                var a = Mathf.Lerp(1f, 0.75f, v);
                for (var x = 0; x < w; x++)
                {
                    var m = 0.95f + 0.05f * Mathf.Sin(x * 0.2f + v * 8f);
                    tex.SetPixel(x, y, new Color(r * m, g * m, b * m, a));
                }
            }

            return Finish(tex, w, h);
        }

        static Sprite CreateMidWaterSprite(int w, int h)
        {
            var tex = NewTex(w, h);
            for (var y = 0; y < h; y++)
            {
                var v = y / (float)(h - 1);
                var lobe = Mathf.Sin(v * Mathf.PI);
                var r = 0.1f;
                var g = 0.45f;
                var b = 0.7f;
                var a = lobe * 0.75f;
                for (var x = 0; x < w; x++)
                {
                    var caustic = 0.9f + 0.1f * Mathf.Sin(x * 0.4f + v * 12f);
                    tex.SetPixel(x, y, new Color(r * caustic, g * caustic, b * caustic, a));
                }
            }

            return Finish(tex, w, h);
        }

        static Sprite CreateSheenSprite(int w, int h)
        {
            var tex = NewTex(w, h);
            for (var y = 0; y < h; y++)
            {
                var v = y / (float)(h - 1);
                var a = Mathf.Pow(v, 1.3f) * 0.7f;
                for (var x = 0; x < w; x++)
                {
                    var ripple = 0.92f + 0.08f * Mathf.Sin(x * 0.5f + v * 5f);
                    tex.SetPixel(x, y, new Color(0.35f * ripple, 0.78f * ripple, 0.88f * ripple, a));
                }
            }

            return Finish(tex, w, h);
        }

        static Sprite CreateFoamSprite(int w, int h)
        {
            var tex = NewTex(w, h);
            var mid = (h - 1) * 0.4f;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var nx = x / (float)(w - 1);
                    var wave = Mathf.Sin(nx * Mathf.PI * 7f) * 0.22f
                               + Mathf.Sin(nx * Mathf.PI * 15f) * 0.1f;
                    var crest = mid + wave * h;
                    var d = y - crest;
                    float a;
                    if (d > 3f) a = 0f;
                    else if (d > 0f) a = 1f - d / 3f;
                    else a = Mathf.Clamp01(1f + d / (h * 0.5f)) * 0.95f;

                    tex.SetPixel(x, y, new Color(0.95f, 0.98f, 1f, a));
                }
            }

            return Finish(tex, w, h);
        }

        static Sprite CreateWaveSpraySprite(int w, int h)
        {
            var tex = NewTex(w, h);
            for (var y = 0; y < h; y++)
            {
                var v = y / (float)(h - 1);
                var baseA = (1f - v) * (1f - v) * 0.55f;
                for (var x = 0; x < w; x++)
                {
                    var nx = x / (float)(w - 1);
                    var mound = 0.5f + 0.5f * Mathf.Sin(nx * Mathf.PI * 6f);
                    var a = baseA * mound;
                    if (((x * 3 + y * 5) % 13) == 0)
                        a = Mathf.Max(a, 0.3f * (1f - v));
                    tex.SetPixel(x, y, new Color(0.9f, 0.95f, 1f, Mathf.Clamp01(a)));
                }
            }

            return Finish(tex, w, h);
        }

        static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        static Sprite Finish(Texture2D tex, int w, int h)
        {
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
