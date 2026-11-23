// Standalone repro of WinKeys' low-level keyboard hook, outside Godot.
// Usage: hookrepro <mode>   mode = thread (hook on its own thread + GetMessage, like WinKeys)
//                                  main   (hook on the main thread + GetMessage)
// Sends F24 (no system action) twice through SendInput and counts what the hook saw. Never sends PrtSc.
using System; using System.Runtime.InteropServices; using System.Threading; using System.Diagnostics;
static class P {
  delegate IntPtr HookProc(int n, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }
  [StructLayout(LayoutKind.Explicit, Size = 40)] struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public ushort wVk; [FieldOffset(10)] public ushort wScan; [FieldOffset(12)] public uint dwFlags; [FieldOffset(16)] public uint time; [FieldOffset(24)] public IntPtr extra; }
  [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc p, IntPtr h, uint t);
  [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
  [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint a, uint b);
  [DllImport("user32.dll")] static extern bool PostThreadMessage(uint t, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int cb);
  [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
  [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string n);
  [DllImport("user32.dll")] static extern IntPtr GetThreadDesktop(uint t);
  [DllImport("user32.dll")] static extern IntPtr OpenInputDesktop(uint f, bool inh, uint acc);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetUserObjectInformation(IntPtr h, int idx, System.Text.StringBuilder sb, int len, out int need);
  static HookProc _proc; static IntPtr _hook; static volatile int _seen, _seenF24, _seenSnap; static string _snapLog = ""; static uint _tid;
  static IntPtr Cb(int n, IntPtr w, IntPtr l) { if (n >= 0) { _seen++; int vk = Marshal.ReadInt32(l); if (vk == 0x87) _seenF24++; if (vk == 0x2C) { _seenSnap++; _snapLog += $" msg={w.ToInt32():X} scan={Marshal.ReadInt32(l, 4):X} flags={Marshal.ReadInt32(l, 8):X};"; return (IntPtr)1; } } return CallNextHookEx(_hook, n, w, l); }
  static string DeskName(IntPtr d) { var sb = new System.Text.StringBuilder(256); return d == IntPtr.Zero ? "(none)" : GetUserObjectInformation(d, 2, sb, 512, out _) ? sb.ToString() : "(?)"; }
  static void Loop() { _tid = GetCurrentThreadId(); _proc = Cb; _hook = SetWindowsHookEx(13, _proc, GetModuleHandle(null), 0);
    Console.WriteLine($"hook={_hook} err={Marshal.GetLastWin32Error()} thread={_tid}"); while (GetMessage(out var m, IntPtr.Zero, 0, 0) > 0) { } UnhookWindowsHookEx(_hook); }
  static int Main(string[] a) {
    string mode = a.Length > 0 ? a[0] : "thread";
    var p = Process.GetCurrentProcess();
    Console.WriteLine($"session={p.SessionId} threadDesktop={DeskName(GetThreadDesktop(GetCurrentThreadId()))} inputDesktop={DeskName(OpenInputDesktop(0, false, 0x0001))}");
    var t = new Thread(Loop) { IsBackground = true }; t.Start(); Thread.Sleep(500);
    var k = new INPUT[2]; k[0].type = 1; k[0].wVk = 0x87; k[1].type = 1; k[1].wVk = 0x87; k[1].dwFlags = 2;
    for (int i = 0; i < 2; i++) { uint s = SendInput(2, k, Marshal.SizeOf<INPUT>()); Console.WriteLine($"SendInput sent {s} err={Marshal.GetLastWin32Error()}"); Thread.Sleep(300); }
    if (mode == "snap") { var q = new INPUT[2]; q[0].type = 1; q[0].wVk = 0x2C; q[0].wScan = 0x37; q[0].dwFlags = 1; q[1].type = 1; q[1].wVk = 0x2C; q[1].wScan = 0x37; q[1].dwFlags = 3; Console.WriteLine($"SendInput PrtSc sent {SendInput(2, q, Marshal.SizeOf<INPUT>())}"); }
    Thread.Sleep(int.Parse(a.Length > 1 ? a[1] : "500"));
    Console.WriteLine($"RESULT hook saw {_seen} events, {_seenF24} F24, {_seenSnap} PrtSc{_snapLog}");
    PostThreadMessage(_tid, 0x12, IntPtr.Zero, IntPtr.Zero); return 0; }
}
