using Godot;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AshenAbyss;

/// <summary>
/// Structured game log for troubleshooting. One file per run in user://logs (…\AppData\Roaming\AshenAbyss\logs),
/// named game_yyyyMMdd_HHmmss.log; the newest 20 are kept. Every line: time since start, level, category, message.
/// The engine's own log (engine.log) sits in the same folder.
/// </summary>
public static class Log
{
    private static StreamWriter _w;
    private static readonly object Lock = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    public static string Dir { get; private set; }
    public static string FilePath { get; private set; }
    public static bool Verbose;
    public static int ErrorCount, WarnCount;

    public static void Init()
    {
        try
        {
            Dir = ProjectSettings.GlobalizePath("user://logs");
            Directory.CreateDirectory(Dir);
            foreach (var old in new DirectoryInfo(Dir).GetFiles("game_*.log").OrderByDescending(f => f.CreationTimeUtc).Skip(19))
            {
                try { old.Delete(); } catch { }
            }
            FilePath = Path.Combine(Dir, $"game_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            _w = new StreamWriter(new FileStream(FilePath, FileMode.Create, System.IO.FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch (Exception e)
        {
            GD.PrintErr("[log] cannot open log file: " + e.Message);
        }

        AppDomain.CurrentDomain.UnhandledException += (_, a) => Error("crash", "Unhandled exception", a.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, a) => { Error("crash", "Unobserved task exception", a.Exception); a.SetObserved(); };

        Info("boot", $"==== 燼淵 Ashen Abyss {ProjectSettings.GetSetting("application/config/version")} ====");
        Info("boot", $"time={DateTime.Now:yyyy-MM-dd HH:mm:ss zzz} os={OS.GetName()} {OS.GetVersion()} locale={OS.GetLocale()}");
        Info("boot", $"cpu={OS.GetProcessorName()} threads={OS.GetProcessorCount()} ram={OS.GetMemoryInfo()["physical"].AsInt64() / (1024 * 1024)}MB");
        Info("boot", $"gpu={RenderingServer.GetVideoAdapterName()} vendor={RenderingServer.GetVideoAdapterVendor()} api={RenderingServer.GetVideoAdapterApiVersion()} method={RenderingServer.GetCurrentRenderingMethod()}");
        Info("boot", $"engine={Engine.GetVersionInfo()["string"]} dotnet={System.Environment.Version} debug={OS.IsDebugBuild()}");
        Info("boot", $"exe={OS.GetExecutablePath()} userdir={OS.GetUserDataDir()}");
        Info("boot", $"args=[{string.Join(' ', OS.GetCmdlineArgs())}] userargs=[{string.Join(' ', OS.GetCmdlineUserArgs())}]");
    }

    private static void Write(string level, string cat, string msg)
    {
        var line = $"{Clock.Elapsed.TotalSeconds,9:F3} {level} [{cat}] {msg}";
        lock (Lock)
        {
            try { _w?.WriteLine(line); } catch { }
        }
        if (level == "ERR") GD.PrintErr(line); else GD.Print(line);
    }

    public static void Debug(string cat, string msg) { if (Verbose) Write("DBG", cat, msg); }
    public static void Info(string cat, string msg) => Write("INF", cat, msg);
    public static void Warn(string cat, string msg) { WarnCount++; Write("WRN", cat, msg); }

    public static void Error(string cat, string msg, Exception e = null)
    {
        ErrorCount++;
        Write("ERR", cat, e == null ? msg : $"{msg}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
    }

    /// <summary>Runs an action and logs (instead of propagating) any exception, so one bad frame can't kill the game.</summary>
    public static void Guard(string cat, Action a)
    {
        try { a(); } catch (Exception e) { Error(cat, "guarded call failed", e); }
    }

    public static void Close()
    {
        Info("boot", $"shutdown errors={ErrorCount} warnings={WarnCount} uptime={Clock.Elapsed.TotalSeconds:F0}s");
        lock (Lock) { try { _w?.Flush(); _w?.Dispose(); } catch { } _w = null; }
    }
}
