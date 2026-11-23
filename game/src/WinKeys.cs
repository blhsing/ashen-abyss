using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace AshenAbyss;

/// <summary>
/// Makes the PrtSc key usable as an in-game screenshot key on Windows.
/// Windows never sends a key-down for PrtSc to applications, and Windows 11 can bind it to the Snipping Tool overlay,
/// which would steal focus from a fullscreen game. A low-level keyboard hook (on its own thread with its own message
/// loop, so a stalled game frame can never delay system keyboard input) watches for PrtSc while our window is in the
/// foreground, swallows it and raises a flag the game polls. When the game is not focused the key is left alone.
/// A GetAsyncKeyState poll is kept as a fallback in case the hook cannot be installed.
/// [fixes 0.4.0] Findings: the hook IS called for real keys (701 key events in a played session of the installed build),
/// and a standalone program's identical hook sees and swallows an injected PrtSc. Inside the game process, injected keys
/// never reach the hook (--hook-selftest), so test runs cannot press PrtSc through it, and whether a real PrtSc is
/// swallowed depends on Windows (with "use the PrtSc key to open screen capture" on, the Snipping Tool may still open).
/// For that case Game.TakeScreenshot grabs the frame at once and writes the clipboard 350 ms later, after Windows' own
/// desktop copy, so the game's picture is the one that stays on the clipboard.
/// </summary>
public static class WinKeys
{
    private const int WH_KEYBOARD_LL = 13, WM_KEYUP = 0x0101, WM_SYSKEYUP = 0x0105, VK_SNAPSHOT = 0x2C, WM_QUIT = 0x0012;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int ptX, ptY; }

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string lpModuleName);

    private static HookProc _proc;           // keep the delegate alive
    private static IntPtr _hook;
    private static Thread _thread;
    private static uint _threadId;
    private static volatile bool _pending;
    private static bool _pollWasDown, _focusedLastFrame;
    private const uint WM_TIMER = 0x0113;
    [DllImport("user32.dll")] private static extern UIntPtr SetTimer(IntPtr hWnd, UIntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);
    private static readonly uint Pid = (uint)Process.GetCurrentProcess().Id;
    public static bool HookInstalled => _hook != IntPtr.Zero;

    private static bool IsOurWindowFocused()
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        GetWindowThreadProcessId(fg, out var pid);
        return pid == Pid;
    }

    public static void Start()
    {
        if (!OperatingSystem.IsWindows() || _thread != null) return;
        _thread = new Thread(() =>
        {
            try
            {
                _threadId = GetCurrentThreadId();
                _proc = Callback;
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
                if (_hook == IntPtr.Zero) { Log.Warn("input", $"PrtSc hook not installed (error {Marshal.GetLastWin32Error()}); using polling fallback"); return; }
                Log.Info("input", "PrtSc keyboard hook installed");
                _selfTest = new Timer(_ => SelfTest(), null, 4000, Timeout.Infinite);
                // Windows silently removes a low-level hook whose callback answered late a few times (a long pause
                // of this process while a level loads is enough), and nothing tells the application. A timer on
                // this thread therefore installs a fresh hook every few seconds and retires the previous one.
                SetTimer(IntPtr.Zero, UIntPtr.Zero, 3000, IntPtr.Zero);
                while (GetMessage(out var m, IntPtr.Zero, 0, 0) > 0)
                {
                    if (m.message != WM_TIMER) continue;
                    var fresh = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
                    if (fresh == IntPtr.Zero) continue;
                    var old = _hook;
                    _hook = fresh;
                    if (old != IntPtr.Zero) UnhookWindowsHookEx(old);
                }
            }
            catch (Exception e) { Log.Error("input", "keyboard hook thread failed", e); }
            finally
            {
                if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            }
        }) { IsBackground = true, Name = "PrtScHook" };
        _thread.Start();
    }

    private static IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            _seenKeys++;
            if (k.vkCode == VK_SNAPSHOT) _seenPrtSc++;
            else if (k.vkCode == 0x87) _seenF24++;
            if (k.vkCode == VK_SNAPSHOT && IsOurWindowFocused())
            {
                int msg = wParam.ToInt32();
                if (msg == WM_KEYUP || msg == WM_SYSKEYUP) _pending = true;
                return (IntPtr)1; // swallow: keeps the Snipping Tool overlay from opening over the game
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>Call once per frame. True when PrtSc was pressed since the last call.</summary>
    public static bool ConsumeScreenshotRequest()
    {
        if (!OperatingSystem.IsWindows()) return false;
        // Poll as well as hook. A key the hook swallowed never reaches the key state, so one press cannot fire both
        // paths; and when Windows has dropped the hook, the poll still sees the key. Focus is remembered from the
        // previous frame because the system's own capture overlay may take the foreground at the moment of release.
        short st = GetAsyncKeyState(VK_SNAPSHOT);
        bool down = (st & 0x8000) != 0;
        bool tapped = (st & 1) != 0 && !down && !_pollWasDown;     // pressed and released between two frames
        bool focused = IsOurWindowFocused();
        bool poll = ((_pollWasDown && !down) || tapped) && (focused || _focusedLastFrame);
        _pollWasDown = down;
        _focusedLastFrame = focused;
        if (_pending) { _pending = false; Log.Info("input", "PrtSc received (keyboard hook)"); return true; }
        if (poll) { Log.Info("input", "PrtSc received (key-state poll; the hook did not see it)"); return true; }
        return false;
    }

    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    /// <summary>
    /// Test harness (--prtsc-at): presses PrtSc. With our window focused and the hook installed, a real synthetic key
    /// travels through the low-level hook (and is swallowed there, so nothing reaches the desktop). Otherwise only the
    /// request flag is raised — a PrtSc sent to another application would open the system's capture overlay.
    /// Returns a description of the path taken, for the log.
    /// </summary>
    public static string SimulatePrtSc()
    {
        // [fixes 0.4.0] Only the request flag. A synthetic PrtSc (SendInput, even with the hardware scan code) never
        // reaches this process's own low-level hook (--hook-selftest: 0 of 2 injected keys seen, while a standalone
        // program's hook sees them and real keys reach ours), so it went on to Windows and opened the Snipping Tool.
        _pending = true;
        return "the request flag (a synthetic key would reach Windows, not our hook)";
    }

    private static volatile int _seenKeys, _seenPrtSc, _seenF24;
    private static Timer _selfTest;

    /// <summary>
    /// --hook-selftest: 4 s after start, injects F24 (a key with no system action, whatever window has the focus) and
    /// logs whether this process's own hook saw it. Tells "the hook is not called" apart from "PrtSc never reached it".
    /// </summary>
    private static void SelfTest()
    {
        try
        {
            if (!(Godot.OS.GetCmdlineUserArgs() is { } a && Array.IndexOf(a, "--hook-selftest") >= 0)) return;
            int before = _seenF24;
            var inputs = new INPUT[2];
            inputs[0].type = 1; inputs[0].wVk = 0x87;
            inputs[1].type = 1; inputs[1].wVk = 0x87; inputs[1].dwFlags = 2;
            uint sent = SendInput(2, inputs, Marshal.SizeOf<INPUT>());
            Thread.Sleep(800);
            Log.Info("input", $"hook self-test: SendInput F24 sent {sent} of 2, hook saw {_seenF24 - before} F24 events (installed={HookInstalled}, focused={IsOurWindowFocused()})");
        }
        catch (Exception e) { Log.Warn("input", "hook self-test failed: " + e.Message); }
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public ushort wVk;
        [FieldOffset(10)] public ushort wScan;
        [FieldOffset(12)] public uint dwFlags;
        [FieldOffset(16)] public uint time;
        [FieldOffset(24)] public IntPtr dwExtraInfo;
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public static void Stop()
    {
        Log.Info("input", $"keyboard hook saw {_seenKeys} key events, {_seenPrtSc} of them PrtSc");
        if (_threadId != 0) PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
    }
}
