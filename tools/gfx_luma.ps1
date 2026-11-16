# Luminance check for screenshots.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\gfx_luma.ps1 -Dir Y:\shots\gfx\x [-Pattern "*.png"] [-Hud]
# Per image: median, 75th and 95th percentile of sRGB luma in percent (the dark-look targets are median 6-10,
# p75 < 18, p95 < 45), and the share of the frame above 18 % ("lit"). -Hud leaves out the regions the HUD covers in a
# 16:9 gameplay shot (action bar and globes, minimap and tracker).
# If <name>_mask.png exists next to an image (EnvProbe --gfx-mask: hero flat green, other actors flat magenta), it
# also prints the mean sRGB luma of the hero's pixels, of the other actors' pixels and of the ground within about
# 2 m of each, and the ratios: a figure that belongs in the scene's light sits at roughly 0.9-1.3 x its ground.
param([string]$Dir, [string]$Pattern = '*.png', [switch]$Hud)
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public static class GfxLuma {
  static byte[] Load(string path, out int w, out int h, out int stride) {
    using (var bmp = new Bitmap(path)) {
      w = bmp.Width; h = bmp.Height;
      var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
      stride = data.Stride; var buf = new byte[stride * h];
      Marshal.Copy(data.Scan0, buf, 0, buf.Length); bmp.UnlockBits(data);
      return buf;
    }
  }
  static bool InHud(int x, int y, int w, int h) {
    double u = (double)x / w, v = (double)y / h;
    if (v > 0.82 && u > 0.24 && u < 0.76) return true;     // action bar, globes
    if (u > 0.78 && v < 0.52) return true;                  // minimap, quest tracker
    return false;
  }
  public static string Measure(string path, string maskPath, bool hud) {
    int w, h, stride; var buf = Load(path, out w, out h, out stride);
    var hist = new long[256]; long n = 0;
    var luma = new byte[w * h];
    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
      int i = y * stride + x * 3;
      double l = 0.2126 * buf[i + 2] + 0.7152 * buf[i + 1] + 0.0722 * buf[i];
      byte b = (byte)Math.Min(255, (int)Math.Round(l)); luma[y * w + x] = b;
      if (hud && InHud(x, y, w, h)) continue;
      hist[b]++; n++;
    }
    Func<double, double> pct = p => { long acc = 0, want = (long)(n * p); for (int i = 0; i < 256; i++) { acc += hist[i]; if (acc >= want) return i / 2.55; } return 100; };
    long lit = 0; for (int i = 46; i < 256; i++) lit += hist[i];
    long white = 0; for (int i = 230; i < 256; i++) white += hist[i];
    string s = string.Format("median={0,5:0.0}  p75={1,5:0.0}  p95={2,5:0.0}  lit(>18%)={3,4:0}%  white(>90%)={4,5:0.00}%", pct(0.5), pct(0.75), pct(0.95), 100.0 * lit / Math.Max(1, n), 100.0 * white / Math.Max(1, n));
    if (string.IsNullOrEmpty(maskPath)) return s;

    int mw, mh, ms; var m = Load(maskPath, out mw, out mh, out ms);
    if (mw != w || mh != h) return s + "  (mask size differs)";
    const int B = 4, R = 26;                                // blocks of 4 px; ground ring out to about 104 px (~1.6 m at default zoom)
    int bw = w / B, bh = h / B;
    var kind = new byte[bw * bh];                           // 1 hero, 2 other actor
    double hs = 0, os = 0; long hn = 0, on = 0;
    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
      int i = y * ms + x * 3; int b = m[i], g = m[i + 1], r = m[i + 2];
      byte k = 0;
      if (g > 150 && g > r * 1.3 && g > b * 1.6) k = 1; else if (r > 120 && b > 120 && g < Math.Min(r, b) * 0.75) k = 2;
      if (k == 0) continue;
      int bi = Math.Min(bh - 1, y / B) * bw + Math.Min(bw - 1, x / B);
      if (kind[bi] == 0 || k == 1) kind[bi] = k;
      if (k == 1) { hs += luma[y * w + x]; hn++; } else { os += luma[y * w + x]; on++; }
    }
    double ghs = 0, gos = 0; long ghn = 0, gon = 0;
    for (int by = 0; by < bh; by++) for (int bx = 0; bx < bw; bx++) {
      if (kind[by * bw + bx] != 0) continue;
      if (hud && InHud(bx * B, by * B, w, h)) continue;
      bool nearH = false, nearO = false;
      for (int dy = -R; dy <= R && !(nearH && nearO); dy += 2) { int yy = by + dy; if (yy < 0 || yy >= bh) continue;
        for (int dx = -R; dx <= R; dx += 2) { int xx = bx + dx; if (xx < 0 || xx >= bw || dx * dx + dy * dy > R * R) continue;
          byte k = kind[yy * bw + xx]; if (k == 1) nearH = true; else if (k == 2) nearO = true; } }
      if (!nearH && !nearO) continue;
      double sum = 0; for (int yy = 0; yy < B; yy++) for (int xx = 0; xx < B; xx++) sum += luma[(by * B + yy) * w + bx * B + xx];
      sum /= B * B;
      if (sum < 4.0) continue;                              // the black lid over solid rock and the void are not ground
      if (nearH) { ghs += sum; ghn++; } if (nearO) { gos += sum; gon++; }
    }
    double hm = hn > 0 ? hs / hn / 2.55 : 0, om = on > 0 ? os / on / 2.55 : 0, gh = ghn > 0 ? ghs / ghn / 2.55 : 0, go = gon > 0 ? gos / gon / 2.55 : 0;
    s += string.Format("  | hero={0,5:0.0} ground={1,5:0.0} ratio={2,4:0.00}", hm, gh, gh > 0.01 ? hm / gh : 0);
    if (on > 200) s += string.Format("  | actors={0,5:0.0} ground={1,5:0.0} ratio={2,4:0.00}", om, go, go > 0.01 ? om / go : 0);
    return s;
  }
}
"@ -ReferencedAssemblies System.Drawing
Get-ChildItem $Dir -Filter $Pattern | Where-Object { $_.Name -notmatch '_mask\.png$|^sheet' } | Sort-Object Name | ForEach-Object {
    $mask = Join-Path $_.DirectoryName ($_.BaseName + '_mask.png')
    if (-not (Test-Path $mask)) { $mask = $null }
    "{0,-24} {1}" -f $_.BaseName, [GfxLuma]::Measure($_.FullName, $mask, [bool]$Hud)
}
