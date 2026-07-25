#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CreatureClimb.Editor
{
    /// <summary>
    /// Fixes white/light edge halos common on AI-generated sprites (Gemini, etc.)
    /// and bleeds opaque RGB into transparent pixels so filtering never samples
    /// white/black fringe. Also trims empty padding and can harden alpha for
    /// crisp pixel art.
    /// </summary>
    public sealed class SpriteColorBleedTool : EditorWindow
    {
        [SerializeField] List<Texture2D> textures = new List<Texture2D>();
        [SerializeField] DefaultAsset folder;

        // Defringe
        [SerializeField] bool removeWhiteHalo = true;
        [SerializeField] int haloPasses = 4;
        [SerializeField] float whiteMinChannel = 180f;
        [SerializeField] float whiteMaxSaturation = 50f;
        [SerializeField] float fringeAlphaCutoff = 240f;

        // Alpha
        [SerializeField] bool hardenAlpha = true;
        [SerializeField] float alphaThreshold = 128f;

        // Color bleed
        [SerializeField] bool colorBleed = true;
        [SerializeField] int bleedIterations = 6;

        // Optimize
        [SerializeField] bool trimPadding = true;
        [SerializeField] int trimMargin = 2;
        [SerializeField] bool writeAlongside = true;
        [SerializeField] string suffix = "_clean";

        Vector2 scroll;

        [MenuItem("Creature Climb/Sprites/Color Bleed & Defringe Tool")]
        public static void Open()
        {
            var w = GetWindow<SpriteColorBleedTool>("Color Bleed");
            w.minSize = new Vector2(420, 520);
            w.Show();
        }

        [MenuItem("Assets/Creature Climb/Color Bleed Selected Sprites", false, 1200)]
        public static void ProcessSelectedMenu()
        {
            var paths = CollectSelectedTexturePaths();
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("Color Bleed", "Select one or more PNG/texture assets first.", "OK");
                return;
            }

            var settings = DefaultSettings();
            var count = 0;
            foreach (var path in paths)
            {
                if (ProcessAssetPath(path, settings, overwrite: true))
                    count++;
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Color Bleed", $"Processed {count} texture(s).", "OK");
        }

        [MenuItem("Assets/Creature Climb/Color Bleed Selected Sprites", true)]
        static bool ProcessSelectedMenuValidate() => CollectSelectedTexturePaths().Count > 0;

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gemini / AI sprites often leave a white or light halo on cutout edges. " +
                "This tool peels that fringe, hardens alpha for pixel art, bleeds color into " +
                "transparent pixels (so mip/filter edges stay clean), and trims empty padding.",
                MessageType.Info);

            folder = (DefaultAsset)EditorGUILayout.ObjectField("Folder (optional)", folder, typeof(DefaultAsset), false);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);
            var so = new SerializedObject(this);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("textures"), true);
            so.ApplyModifiedProperties();

            if (GUILayout.Button("Add Selected Project Textures"))
                AddSelectedTextures();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("1. White Halo Defringe", EditorStyles.boldLabel);
            removeWhiteHalo = EditorGUILayout.Toggle("Remove white/light edge halo", removeWhiteHalo);
            using (new EditorGUI.DisabledScope(!removeWhiteHalo))
            {
                haloPasses = EditorGUILayout.IntSlider("Halo peel passes", haloPasses, 1, 6);
                whiteMinChannel = EditorGUILayout.Slider("Min channel (R/G/B)", whiteMinChannel, 160f, 250f);
                whiteMaxSaturation = EditorGUILayout.Slider("Max saturation", whiteMaxSaturation, 10f, 80f);
                fringeAlphaCutoff = EditorGUILayout.Slider("Semi-trans fringe alpha", fringeAlphaCutoff, 1f, 255f);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("2. Alpha", EditorStyles.boldLabel);
            hardenAlpha = EditorGUILayout.Toggle("Harden alpha (crisp pixel edges)", hardenAlpha);
            using (new EditorGUI.DisabledScope(!hardenAlpha))
                alphaThreshold = EditorGUILayout.Slider("Alpha threshold", alphaThreshold, 1f, 254f);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("3. Color Bleed", EditorStyles.boldLabel);
            colorBleed = EditorGUILayout.Toggle("Bleed opaque color into transparent", colorBleed);
            using (new EditorGUI.DisabledScope(!colorBleed))
                bleedIterations = EditorGUILayout.IntSlider("Bleed iterations", bleedIterations, 1, 32);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("4. Optimize", EditorStyles.boldLabel);
            trimPadding = EditorGUILayout.Toggle("Trim transparent padding", trimPadding);
            using (new EditorGUI.DisabledScope(!trimPadding))
                trimMargin = EditorGUILayout.IntSlider("Trim margin (px)", trimMargin, 0, 16);

            writeAlongside = EditorGUILayout.Toggle("Write as new file (*_clean)", writeAlongside);
            using (new EditorGUI.DisabledScope(!writeAlongside))
                suffix = EditorGUILayout.TextField("Suffix", string.IsNullOrEmpty(suffix) ? "_clean" : suffix);

            EditorGUILayout.Space(12);
            using (new EditorGUI.DisabledScope(!HasAnySource()))
            {
                if (GUILayout.Button("Process", GUILayout.Height(32)))
                    RunProcess(overwrite: !writeAlongside);

                if (GUILayout.Button("Process & Overwrite Originals", GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog(
                            "Overwrite sprites?",
                            "This will overwrite the source PNG files. Continue?",
                            "Overwrite", "Cancel"))
                    {
                        RunProcess(overwrite: true);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        bool HasAnySource()
        {
            if (textures != null)
            {
                foreach (var t in textures)
                    if (t != null) return true;
            }

            return folder != null;
        }

        void AddSelectedTextures()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is Texture2D tex && !textures.Contains(tex))
                    textures.Add(tex);
            }
        }

        void RunProcess(bool overwrite)
        {
            var settings = CurrentSettings();
            var paths = new HashSet<string>();

            if (textures != null)
            {
                foreach (var t in textures)
                {
                    if (t == null) continue;
                    var p = AssetDatabase.GetAssetPath(t);
                    if (!string.IsNullOrEmpty(p)) paths.Add(p);
                }
            }

            if (folder != null)
            {
                var folderPath = AssetDatabase.GetAssetPath(folder);
                if (AssetDatabase.IsValidFolder(folderPath))
                {
                    var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                    foreach (var g in guids)
                        paths.Add(AssetDatabase.GUIDToAssetPath(g));
                }
            }

            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("Color Bleed", "No textures to process.", "OK");
                return;
            }

            var ok = 0;
            var i = 0;
            try
            {
                foreach (var path in paths)
                {
                    i++;
                    EditorUtility.DisplayProgressBar("Color Bleed", path, (float)i / paths.Count);
                    if (ProcessAssetPath(path, settings, overwrite))
                        ok++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Color Bleed", $"Done. Processed {ok}/{paths.Count} texture(s).", "OK");
        }

        struct Settings
        {
            public bool removeWhiteHalo;
            public int haloPasses;
            public float whiteMinChannel;
            public float whiteMaxSaturation;
            public float fringeAlphaCutoff;
            public bool hardenAlpha;
            public float alphaThreshold;
            public bool colorBleed;
            public int bleedIterations;
            public bool trimPadding;
            public int trimMargin;
            public string suffix;
        }

        Settings CurrentSettings() => new Settings
        {
            removeWhiteHalo = removeWhiteHalo,
            haloPasses = haloPasses,
            whiteMinChannel = whiteMinChannel,
            whiteMaxSaturation = whiteMaxSaturation,
            fringeAlphaCutoff = fringeAlphaCutoff,
            hardenAlpha = hardenAlpha,
            alphaThreshold = alphaThreshold,
            colorBleed = colorBleed,
            bleedIterations = bleedIterations,
            trimPadding = trimPadding,
            trimMargin = trimMargin,
            suffix = string.IsNullOrEmpty(suffix) ? "_clean" : suffix,
        };

        static Settings DefaultSettings() => new Settings
        {
            removeWhiteHalo = true,
            haloPasses = 4,
            whiteMinChannel = 180f,
            whiteMaxSaturation = 50f,
            fringeAlphaCutoff = 240f,
            hardenAlpha = true,
            alphaThreshold = 128f,
            colorBleed = true,
            bleedIterations = 6,
            trimPadding = true,
            trimMargin = 2,
            suffix = "_clean",
        };

        static List<string> CollectSelectedTexturePaths()
        {
            var list = new List<string>();
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
                    list.Add(path);
            }

            return list;
        }

        static bool ProcessAssetPath(string assetPath, Settings s, bool overwrite)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                return false;

            // Only rewrite PNG files we can encode losslessly.
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            if (ext != ".png")
            {
                Debug.LogWarning($"[ColorBleed] Skip non-PNG: {assetPath}");
                return false;
            }

            // Load raw file bytes (not the imported Texture2D) so we keep full
            // fidelity for large Gemini exports without toggling isReadable.
            var abs = Path.GetFullPath(assetPath);
            var raw = File.ReadAllBytes(abs);
            var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tmp.LoadImage(raw, false))
            {
                Object.DestroyImmediate(tmp);
                Debug.LogError($"[ColorBleed] Could not decode PNG: {assetPath}");
                return false;
            }

            var pixels = tmp.GetPixels32();
            var w = tmp.width;
            var h = tmp.height;
            Object.DestroyImmediate(tmp);

            ProcessPixels(ref pixels, ref w, ref h, s);

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels32(pixels);
            outTex.Apply(false, false);
            var png = outTex.EncodeToPNG();
            Object.DestroyImmediate(outTex);

            string outPath;
            if (overwrite)
            {
                outPath = assetPath;
            }
            else
            {
                var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "Assets";
                var name = Path.GetFileNameWithoutExtension(assetPath);
                outPath = $"{dir}/{name}{s.suffix}.png";
            }

            File.WriteAllBytes(outPath, png);
            Debug.Log($"[ColorBleed] Wrote {outPath} ({w}x{h})");
            return true;
        }

        static void ProcessPixels(ref Color32[] pixels, ref int w, ref int h, Settings s)
        {
            if (pixels == null || pixels.Length != w * h || w <= 0 || h <= 0)
                return;

            if (s.removeWhiteHalo)
                RemoveWhiteHalo(pixels, w, h, s);

            if (s.hardenAlpha)
                HardenAlpha(pixels, s.alphaThreshold);

            if (s.colorBleed)
                ColorBleed(pixels, w, h, s.bleedIterations);

            if (s.trimPadding)
                TrimTransparent(ref pixels, ref w, ref h, s.trimMargin);
        }

        static void RemoveWhiteHalo(Color32[] px, int w, int h, Settings s)
        {
            var minCh = (byte)Mathf.Clamp(Mathf.RoundToInt(s.whiteMinChannel), 0, 255);
            var maxSat = s.whiteMaxSaturation;
            var fringeA = (byte)Mathf.Clamp(Mathf.RoundToInt(s.fringeAlphaCutoff), 0, 255);
            var passes = Mathf.Max(1, s.haloPasses);

            // Pass 0: kill semi-transparent light fringe anywhere (classic bg-removal smear).
            for (var i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a == 0 || c.a >= fringeA) continue;
                if (IsLightHalo(c, minCh, maxSat))
                    px[i] = new Color32(0, 0, 0, 0);
            }

            // Subsequent peels: opaque/near-opaque light pixels only if on silhouette edge.
            // Interior whites (fangs, eyes, highlights) stay — they have no transparent neighbor.
            for (var pass = 0; pass < passes; pass++)
            {
                var remove = new bool[px.Length];
                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        var i = y * w + x;
                        var c = px[i];
                        if (c.a == 0) continue;
                        if (!IsLightHalo(c, minCh, maxSat)) continue;
                        if (!HasTransparentNeighbor(px, w, h, x, y)) continue;
                        remove[i] = true;
                    }
                }

                var any = false;
                for (var i = 0; i < px.Length; i++)
                {
                    if (!remove[i]) continue;
                    px[i] = new Color32(0, 0, 0, 0);
                    any = true;
                }

                if (!any) break;
            }
        }

        static bool IsLightHalo(Color32 c, byte minChannel, float maxSaturation)
        {
            if (c.r < minChannel || c.g < minChannel || c.b < minChannel)
                return false;

            // Cheap saturation proxy in 0–255 space: (max-min).
            var max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            var min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return (max - min) <= maxSaturation;
        }

        static bool HasTransparentNeighbor(Color32[] px, int w, int h, int x, int y)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                var ny = y + dy;
                if (ny < 0 || ny >= h) return true; // image border counts as outside
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var nx = x + dx;
                    if (nx < 0 || nx >= w) return true;
                    if (px[ny * w + nx].a == 0) return true;
                }
            }

            return false;
        }

        static void HardenAlpha(Color32[] px, float threshold)
        {
            var t = (byte)Mathf.Clamp(Mathf.RoundToInt(threshold), 0, 255);
            for (var i = 0; i < px.Length; i++)
            {
                var c = px[i];
                c.a = c.a >= t ? (byte)255 : (byte)0;
                px[i] = c;
            }
        }

        /// <summary>
        /// Classic sprite color bleed: transparent pixels inherit average RGB of
        /// neighboring solid pixels while keeping alpha = 0. Prevents white/black
        /// fringes under bilinear filtering and sprite extrusion.
        /// </summary>
        static void ColorBleed(Color32[] px, int w, int h, int iterations)
        {
            iterations = Mathf.Max(1, iterations);
            var solid = new bool[px.Length];
            for (var i = 0; i < px.Length; i++)
                solid[i] = px[i].a > 0;

            var scratch = new Color32[px.Length];

            for (var iter = 0; iter < iterations; iter++)
            {
                System.Array.Copy(px, scratch, px.Length);
                var newly = new bool[px.Length];
                var changed = false;

                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        var i = y * w + x;
                        if (solid[i]) continue;

                        var r = 0;
                        var g = 0;
                        var b = 0;
                        var n = 0;

                        for (var dy = -1; dy <= 1; dy++)
                        {
                            var ny = y + dy;
                            if (ny < 0 || ny >= h) continue;
                            for (var dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                var nx = x + dx;
                                if (nx < 0 || nx >= w) continue;
                                var ni = ny * w + nx;
                                if (!solid[ni]) continue;
                                var c = px[ni];
                                r += c.r;
                                g += c.g;
                                b += c.b;
                                n++;
                            }
                        }

                        if (n == 0) continue;

                        scratch[i] = new Color32(
                            (byte)(r / n),
                            (byte)(g / n),
                            (byte)(b / n),
                            0);
                        newly[i] = true;
                        changed = true;
                    }
                }

                System.Array.Copy(scratch, px, px.Length);
                for (var i = 0; i < solid.Length; i++)
                {
                    if (newly[i]) solid[i] = true;
                }

                if (!changed) break;
            }
        }

        static void TrimTransparent(ref Color32[] px, ref int w, ref int h, int margin)
        {
            var minX = w;
            var minY = h;
            var maxX = -1;
            var maxY = -1;

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (px[y * w + x].a == 0) continue;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0) return; // fully transparent

            margin = Mathf.Max(0, margin);
            minX = Mathf.Max(0, minX - margin);
            minY = Mathf.Max(0, minY - margin);
            maxX = Mathf.Min(w - 1, maxX + margin);
            maxY = Mathf.Min(h - 1, maxY + margin);

            var nw = maxX - minX + 1;
            var nh = maxY - minY + 1;
            if (nw == w && nh == h) return;

            var cropped = new Color32[nw * nh];
            for (var y = 0; y < nh; y++)
            {
                for (var x = 0; x < nw; x++)
                    cropped[y * nw + x] = px[(minY + y) * w + (minX + x)];
            }

            px = cropped;
            w = nw;
            h = nh;
        }
    }
}
#endif
