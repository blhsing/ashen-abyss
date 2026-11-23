using Godot;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AshenAbyss;

/// <summary>
/// Puts a captured frame on the Windows clipboard as a picture (Godot's own clipboard is text only), so PrtSc pastes
/// straight into Paint, Discord or Word. Two formats are offered: CF_DIB (32-bit, every program reads it) and the
/// registered "PNG" format (keeps the exact pixels for programs that prefer it). The work runs off the game thread,
/// after an optional delay: when the PrtSc key also reaches Windows (the keyboard hook could not swallow it), Windows
/// copies the whole desktop to the clipboard on its own, and writing ours a moment later makes the game's frame win.
/// Results are queued for the game thread (<see cref="Done"/>), which shows the toast.
/// </summary>
public static class ScreenClipboard
{
    private const uint CF_DIB = 8, GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string lpszFormat);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalFree(IntPtr hMem);

    /// <summary>Finished copies: ok, size of the picture, error text. Drained by the game thread (Game.DrainShots).</summary>
    public static readonly ConcurrentQueue<(bool ok, int w, int h, string err)> Done = new();

    public static bool Supported => OperatingSystem.IsWindows();

    /// <summary>
    /// Copies <paramref name="img"/> (any format; it is converted on a worker thread) to the clipboard after
    /// <paramref name="delayMs"/>. <paramref name="owner"/> is the game window's native handle: SetClipboardData needs an
    /// owner window. Returns false when the copy could not be started.
    /// </summary>
    public static bool CopyLater(Image img, IntPtr owner, int delayMs)
    {
        if (!Supported || img == null || img.IsEmpty()) return false;
        Task.Run(async () =>
        {
            try
            {
                if (delayMs > 0) await Task.Delay(delayMs);
                if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);
                int w = img.GetWidth(), h = img.GetHeight();
                byte[] rgba = img.GetData();
                byte[] png = img.SavePngToBuffer();
                string err = Put(owner, w, h, rgba, png);
                Done.Enqueue((err == null, w, h, err));
            }
            catch (Exception e) { Done.Enqueue((false, 0, 0, e.Message)); }
        });
        return true;
    }

    /// <summary>Writes the picture; null on success, otherwise what went wrong.</summary>
    private static string Put(IntPtr owner, int w, int h, byte[] rgba, byte[] png)
    {
        // a 32-bit bottom-up DIB: BITMAPINFOHEADER then the rows in BGRA order, last row first
        int stride = w * 4, header = 40;
        var dib = new byte[header + stride * h];
        BitConverter.GetBytes(header).CopyTo(dib, 0);          // biSize
        BitConverter.GetBytes(w).CopyTo(dib, 4);               // biWidth
        BitConverter.GetBytes(h).CopyTo(dib, 8);               // biHeight (positive: bottom-up)
        BitConverter.GetBytes((short)1).CopyTo(dib, 12);       // biPlanes
        BitConverter.GetBytes((short)32).CopyTo(dib, 14);      // biBitCount
        BitConverter.GetBytes(0).CopyTo(dib, 16);              // biCompression = BI_RGB
        BitConverter.GetBytes(stride * h).CopyTo(dib, 20);     // biSizeImage
        for (int y = 0; y < h; y++)
        {
            int src = y * stride, dst = header + (h - 1 - y) * stride;
            for (int x = 0; x < stride; x += 4)
            {
                dib[dst + x] = rgba[src + x + 2];       // B
                dib[dst + x + 1] = rgba[src + x + 1];   // G
                dib[dst + x + 2] = rgba[src + x];       // R
                dib[dst + x + 3] = 255;                 // the frame is opaque
            }
        }

        // another program may hold the clipboard for a moment: try for about half a second
        bool open = false;
        for (int i = 0; i < 25 && !(open = OpenClipboard(owner)); i++) Thread.Sleep(20);
        if (!open) return $"the clipboard is busy (error {Marshal.GetLastWin32Error()})";
        try
        {
            if (!EmptyClipboard()) return $"EmptyClipboard failed (error {Marshal.GetLastWin32Error()})";
            string e1 = SetBytes(CF_DIB, dib);
            if (e1 != null) return "CF_DIB: " + e1;
            uint pngFormat = RegisterClipboardFormat("PNG");
            if (pngFormat != 0 && png is { Length: > 0 }) SetBytes(pngFormat, png);     // optional: the DIB is already there
            return null;
        }
        finally { CloseClipboard(); }
    }

    private static string SetBytes(uint format, byte[] data)
    {
        IntPtr mem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)data.Length);
        if (mem == IntPtr.Zero) return $"GlobalAlloc failed (error {Marshal.GetLastWin32Error()})";
        IntPtr p = GlobalLock(mem);
        if (p == IntPtr.Zero) { GlobalFree(mem); return $"GlobalLock failed (error {Marshal.GetLastWin32Error()})"; }
        Marshal.Copy(data, 0, p, data.Length);
        GlobalUnlock(mem);
        if (SetClipboardData(format, mem) == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            GlobalFree(mem);                                    // the clipboard did not take it: still ours to free
            return $"SetClipboardData failed (error {err})";
        }
        return null;                                            // the clipboard owns the memory now
    }
}
