# Standalone color-bleed / white-halo defringe for Gemini-imported sprites.
# Usage:
#   powershell -ExecutionPolicy Bypass -File Tools\ColorBleedSprites.ps1
#   powershell -ExecutionPolicy Bypass -File Tools\ColorBleedSprites.ps1 -Paths "Assets\Sprites\**\*.png" -Overwrite
#   powershell -ExecutionPolicy Bypass -File Tools\ColorBleedSprites.ps1 -Paths "Assets\Resources\Gemini*.png" -OutDir "Assets\Sprites\Creatures"

param(
    [string[]]$Paths = @(
        "Assets\Resources\Gemini_Generated_Image_ag63k6ag63k6ag63-Picsart-BackgroundRemover.png",
        "Assets\Sprites\Creatures\*.png",
        "Assets\Sprites\Platforms\*.png",
        "Assets\Snake Sprite.png"
    ),
    [string]$OutDir = "",
    [string]$Suffix = "_clean",
    [switch]$Overwrite,
    [int]$HaloPasses = 3,
    [int]$WhiteMinChannel = 200,
    [int]$WhiteMaxSaturation = 45,
    [int]$FringeAlphaCutoff = 240,
    [switch]$NoHardenAlpha,
    [int]$AlphaThreshold = 128,
    [int]$BleedIterations = 12,
    [switch]$NoTrim,
    [int]$TrimMargin = 2
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$csharp = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class SpriteColorBleed
{
    public struct Settings
    {
        public bool RemoveWhiteHalo;
        public int HaloPasses;
        public int WhiteMinChannel;
        public int WhiteMaxSaturation;
        public int FringeAlphaCutoff;
        public bool HardenAlpha;
        public int AlphaThreshold;
        public bool ColorBleed;
        public int BleedIterations;
        public bool TrimPadding;
        public int TrimMargin;
    }

    public static void ProcessFile(string inputPath, string outputPath, Settings s)
    {
        byte[] rgba;
        int w, h;
        LoadRgba(inputPath, out rgba, out w, out h);

        if (s.RemoveWhiteHalo)
            RemoveWhiteHalo(rgba, w, h, s);

        if (s.HardenAlpha)
            HardenAlpha(rgba, s.AlphaThreshold);

        if (s.ColorBleed)
            ColorBleed(rgba, w, h, s.BleedIterations);

        if (s.TrimPadding)
            TrimTransparent(ref rgba, ref w, ref h, s.TrimMargin);

        SavePng(outputPath, rgba, w, h);
        Console.WriteLine("OK " + w + "x" + h + " -> " + outputPath);
    }

    static void LoadRgba(string path, out byte[] rgba, out int w, out int h)
    {
        using (var bmp = new Bitmap(path))
        {
            w = bmp.Width;
            h = bmp.Height;
            rgba = new byte[w * h * 4];
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                // System.Drawing is BGRA in memory on Windows.
                var bgra = new byte[Math.Abs(data.Stride) * h];
                Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);
                var stride = data.Stride;
                for (int y = 0; y < h; y++)
                {
                    int srcRow = y * stride;
                    int dstRow = y * w * 4;
                    for (int x = 0; x < w; x++)
                    {
                        int si = srcRow + x * 4;
                        int di = dstRow + x * 4;
                        rgba[di + 0] = bgra[si + 2]; // R
                        rgba[di + 1] = bgra[si + 1]; // G
                        rgba[di + 2] = bgra[si + 0]; // B
                        rgba[di + 3] = bgra[si + 3]; // A
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }
    }

    static void SavePng(string path, byte[] rgba, int w, int h)
    {
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var bgra = new byte[Math.Abs(data.Stride) * h];
                var stride = data.Stride;
                for (int y = 0; y < h; y++)
                {
                    int srcRow = y * w * 4;
                    int dstRow = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int si = srcRow + x * 4;
                        int di = dstRow + x * 4;
                        bgra[di + 0] = rgba[si + 2]; // B
                        bgra[di + 1] = rgba[si + 1]; // G
                        bgra[di + 2] = rgba[si + 0]; // R
                        bgra[di + 3] = rgba[si + 3]; // A
                    }
                }
                Marshal.Copy(bgra, 0, data.Scan0, bgra.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            bmp.Save(path, ImageFormat.Png);
        }
    }

    static void RemoveWhiteHalo(byte[] px, int w, int h, Settings s)
    {
        int minCh = Clamp(s.WhiteMinChannel, 0, 255);
        int maxSat = s.WhiteMaxSaturation;
        int fringeA = Clamp(s.FringeAlphaCutoff, 0, 255);
        int n = w * h;

        // Semi-transparent light fringe anywhere.
        for (int i = 0; i < n; i++)
        {
            int o = i * 4;
            int a = px[o + 3];
            if (a == 0 || a >= fringeA) continue;
            if (IsLightHalo(px[o], px[o + 1], px[o + 2], minCh, maxSat))
            {
                px[o] = px[o + 1] = px[o + 2] = px[o + 3] = 0;
            }
        }

        int passes = Math.Max(1, s.HaloPasses);
        var remove = new bool[n];
        for (int pass = 0; pass < passes; pass++)
        {
            Array.Clear(remove, 0, n);
            bool any = false;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    int o = i * 4;
                    if (px[o + 3] == 0) continue;
                    if (!IsLightHalo(px[o], px[o + 1], px[o + 2], minCh, maxSat)) continue;
                    if (!HasTransparentNeighbor(px, w, h, x, y)) continue;
                    remove[i] = true;
                    any = true;
                }
            }
            if (!any) break;
            for (int i = 0; i < n; i++)
            {
                if (!remove[i]) continue;
                int o = i * 4;
                px[o] = px[o + 1] = px[o + 2] = px[o + 3] = 0;
            }
        }
    }

    static bool IsLightHalo(byte r, byte g, byte b, int minChannel, int maxSaturation)
    {
        if (r < minChannel || g < minChannel || b < minChannel) return false;
        int max = Math.Max(r, Math.Max(g, b));
        int min = Math.Min(r, Math.Min(g, b));
        return (max - min) <= maxSaturation;
    }

    static bool HasTransparentNeighbor(byte[] px, int w, int h, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= h) return true;
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                if (nx < 0 || nx >= w) return true;
                if (px[(ny * w + nx) * 4 + 3] == 0) return true;
            }
        }
        return false;
    }

    static void HardenAlpha(byte[] px, int threshold)
    {
        int t = Clamp(threshold, 0, 255);
        for (int i = 3; i < px.Length; i += 4)
            px[i] = (byte)(px[i] >= t ? 255 : 0);
    }

    static void ColorBleed(byte[] px, int w, int h, int iterations)
    {
        iterations = Math.Max(1, iterations);
        int n = w * h;
        var solid = new bool[n];
        for (int i = 0; i < n; i++)
            solid[i] = px[i * 4 + 3] > 0;

        var scratch = new byte[px.Length];
        var newly = new bool[n];

        for (int iter = 0; iter < iterations; iter++)
        {
            Buffer.BlockCopy(px, 0, scratch, 0, px.Length);
            Array.Clear(newly, 0, n);
            bool changed = false;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (solid[i]) continue;

                    int r = 0, g = 0, b = 0, count = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx;
                            if (nx < 0 || nx >= w) continue;
                            int ni = ny * w + nx;
                            if (!solid[ni]) continue;
                            int no = ni * 4;
                            r += px[no];
                            g += px[no + 1];
                            b += px[no + 2];
                            count++;
                        }
                    }
                    if (count == 0) continue;
                    int o = i * 4;
                    scratch[o] = (byte)(r / count);
                    scratch[o + 1] = (byte)(g / count);
                    scratch[o + 2] = (byte)(b / count);
                    scratch[o + 3] = 0;
                    newly[i] = true;
                    changed = true;
                }
            }

            Buffer.BlockCopy(scratch, 0, px, 0, px.Length);
            for (int i = 0; i < n; i++)
                if (newly[i]) solid[i] = true;
            if (!changed) break;
        }
    }

    static void TrimTransparent(ref byte[] px, ref int w, ref int h, int margin)
    {
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (px[(y * w + x) * 4 + 3] == 0) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }
        if (maxX < 0) return;

        margin = Math.Max(0, margin);
        minX = Math.Max(0, minX - margin);
        minY = Math.Max(0, minY - margin);
        maxX = Math.Min(w - 1, maxX + margin);
        maxY = Math.Min(h - 1, maxY + margin);

        int nw = maxX - minX + 1;
        int nh = maxY - minY + 1;
        if (nw == w && nh == h) return;

        var cropped = new byte[nw * nh * 4];
        for (int y = 0; y < nh; y++)
        {
            for (int x = 0; x < nw; x++)
            {
                int si = ((minY + y) * w + (minX + x)) * 4;
                int di = (y * nw + x) * 4;
                cropped[di] = px[si];
                cropped[di + 1] = px[si + 1];
                cropped[di + 2] = px[si + 2];
                cropped[di + 3] = px[si + 3];
            }
        }
        px = cropped;
        w = nw;
        h = nh;
    }

    static int Clamp(int v, int lo, int hi)
    {
        if (v < lo) return lo;
        if (v > hi) return hi;
        return v;
    }
}
'@

Add-Type -TypeDefinition $csharp -ReferencedAssemblies System.Drawing

$settings = New-Object SpriteColorBleed+Settings
$settings.RemoveWhiteHalo = $true
$settings.HaloPasses = $HaloPasses
$settings.WhiteMinChannel = $WhiteMinChannel
$settings.WhiteMaxSaturation = $WhiteMaxSaturation
$settings.FringeAlphaCutoff = $FringeAlphaCutoff
$settings.HardenAlpha = -not $NoHardenAlpha
$settings.AlphaThreshold = $AlphaThreshold
$settings.ColorBleed = $true
$settings.BleedIterations = $BleedIterations
$settings.TrimPadding = -not $NoTrim
$settings.TrimMargin = $TrimMargin

$files = @()
foreach ($pattern in $Paths) {
    $resolved = Resolve-Path -Path $pattern -ErrorAction SilentlyContinue
    if ($resolved) {
        foreach ($r in $resolved) { $files += $r.Path }
    } else {
        # Try relative to repo root
        $candidate = Join-Path (Get-Location) $pattern
        $resolved = Resolve-Path -Path $candidate -ErrorAction SilentlyContinue
        if ($resolved) {
            foreach ($r in $resolved) { $files += $r.Path }
        } else {
            Write-Warning "No match: $pattern"
        }
    }
}

# Skip already-processed outputs and non-PNG.
$files = $files |
    Where-Object { $_ -match '\.png$' -and $_ -notmatch '_clean\.png$' } |
    Select-Object -Unique
if ($files.Count -eq 0) {
    Write-Error "No input files found."
    exit 1
}

foreach ($inPath in $files) {
    $dir = if ($OutDir) { $OutDir } else { Split-Path $inPath -Parent }
    $base = [System.IO.Path]::GetFileNameWithoutExtension($inPath)
    $outPath = if ($Overwrite) {
        $inPath
    } else {
        Join-Path $dir ($base + $Suffix + ".png")
    }

    if ($OutDir -and -not (Test-Path $OutDir)) {
        New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
    }

    Write-Host "Processing: $inPath"
    [SpriteColorBleed]::ProcessFile($inPath, $outPath, $settings)
}

Write-Host "Done. $($files.Count) file(s)."
