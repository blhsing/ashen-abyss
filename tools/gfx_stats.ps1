# Tonal statistics of screenshots: mean luma, share of pixels below mid-grey (sRGB 0.5), share that is near black (< 0.06)
# and share that is clipped (> 0.97). The art direction asks for 60-70 % of the frame below mid-grey.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\gfx_stats.ps1 -Dir Y:\shots\gfx\final [-Pattern "*.png"]
param([string]$Dir, [string]$Pattern = '*.png', [int]$Step = 8)
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public static class GfxStats {
  public static string Measure(string path, int step) {
    using (var bmp = new Bitmap(path)) {
      var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
      var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
      int stride = data.Stride; var buf = new byte[stride * bmp.Height];
      Marshal.Copy(data.Scan0, buf, 0, buf.Length); bmp.UnlockBits(data);
      long n = 0, dark = 0, black = 0, clip = 0; double sum = 0, sat = 0;
      for (int y = 0; y < bmp.Height; y += step) for (int x = 0; x < bmp.Width; x += step) {
        int i = y * stride + x * 3; double b = buf[i] / 255.0, g = buf[i + 1] / 255.0, r = buf[i + 2] / 255.0;
        double l = 0.2126 * r + 0.7152 * g + 0.0722 * b; sum += l; n++;
        if (l < 0.5) dark++; if (l < 0.06) black++; if (l > 0.97) clip++;
        double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)); sat += mx > 0.001 ? (mx - mn) / mx : 0;
      }
      return string.Format("mean={0,5:0.000}  below-mid={1,5:0.0}%  near-black={2,5:0.0}%  clipped={3,4:0.00}%  saturation={4,5:0.00}", sum / n, 100.0 * dark / n, 100.0 * black / n, 100.0 * clip / n, sat / n);
    }
  }
}
"@ -ReferencedAssemblies System.Drawing
Get-ChildItem $Dir -Filter $Pattern | Sort-Object Name | ForEach-Object { "{0,-26} {1}" -f $_.BaseName, [GfxStats]::Measure($_.FullName, $Step) }
