using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace AshenAbyss;

/// <summary>
/// Only one copy of the game may run at a time (two copies would fight over the save files, the settings file and the
/// fullscreen display). The first copy holds a named mutex for its whole life; Windows releases it when the process
/// ends, however it ends. A second copy finds the mutex taken, brings the running copy's window to the front and exits.
/// </summary>
public static class SingleInstance
{
    private const string Name = @"Local\AshenAbyss.Game.SingleInstance";
    private const string TestName = @"Local\AshenAbyss.Game.TestInstance";
    private static Mutex _mutex;            // kept referenced for the life of the process

    /// <summary>True when the player's copy (the lock of normal launches) is held by another process.</summary>
    public static bool PlayerRunning()
    {
        try { return Mutex.TryOpenExisting(Name, out var m) && Dispose(m); }
        catch { return false; }
    }
    private static bool Dispose(Mutex m) { m.Dispose(); return true; }

    /// <summary>True when this process is the only running copy of its kind (it now owns the lock).</summary>
    public static bool Acquire(bool test = false)
    {
        try
        {
            _mutex = new Mutex(true, test ? TestName : Name, out bool created);
            if (created) return true;
            // the previous owner crashed without releasing it: Windows hands the abandoned mutex to the next waiter
            try { if (_mutex.WaitOne(0)) return true; }
            catch (AbandonedMutexException) { return true; }
            return false;
        }
        catch (Exception e)
        {
            Log.Warn("boot", $"single-instance check failed ({e.Message}); starting anyway");
            return true;
        }
    }

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

    /// <summary>Restores and focuses the window of the copy that is already running. Returns a line for the log.</summary>
    public static string ActivateOther()
    {
        if (!OperatingSystem.IsWindows()) return "not on Windows";
        try
        {
            var me = Process.GetCurrentProcess();
            foreach (var name in new[] { me.ProcessName, "AshenAbyss", "Godot_v4.7.2-stable_mono_win64", "Godot_v4.7.2-stable_mono_win64_console" })
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    if (p.Id == me.Id) continue;
                    IntPtr h = p.MainWindowHandle;
                    if (h == IntPtr.Zero) continue;
                    if (IsIconic(h)) ShowWindow(h, 9);     // SW_RESTORE
                    bool ok = SetForegroundWindow(h);
                    return $"brought the running copy (pid {p.Id}) to the front: {(ok ? "ok" : "refused by Windows")}";
                }
            }
            return "the running copy has no window to bring to the front";
        }
        catch (Exception e) { return $"could not activate the running copy ({e.Message})"; }
    }
}
