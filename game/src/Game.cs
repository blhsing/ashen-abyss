using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace AshenAbyss;

public sealed partial class SaveData
{
    /// <summary>How many times each area has been generated for this character; feeds the level seed (see Game.SeedFor).</summary>
    public Dictionary<string, int> AreaVisits = new();
}

/// <summary>
/// Cheap frame profiler for the perf log line: game code adds Stopwatch ticks to a bucket, Game prints milliseconds per
/// frame every five seconds. Costs two timestamp reads per measured call.
/// </summary>
public static class Prof
{
    public static long Monsters, MonstersAwake, Separation, Player, Shots, Level, Bot, Tree, FrameStart;
    public static int Awake;
    public static long Now => Stopwatch.GetTimestamp();

    public static string Report(int frames)
    {
        double k = 1000.0 / Stopwatch.Frequency / Math.Max(1, frames);
        string s = $"whole tree={Tree * k:0.00}ms | monsters={Monsters * k:0.00}ms (awake {Awake / Math.Max(1, frames)}: {MonstersAwake * k:0.00}ms, separation {Separation * k:0.00}ms) player={Player * k:0.00}ms shots+zones={Shots * k:0.00}ms level={Level * k:0.00}ms bot={Bot * k:0.00}ms";
        Monsters = MonstersAwake = Separation = Player = Shots = Level = Bot = Tree = 0; Awake = 0;
        return s;
    }
}

/// <summary>
/// The spine of the game and root of scenes/Main.tscn: boot order, the state flow (menu → loading → play), area changes
/// with their loading screen, saving, pausing, the death flow, hit-stop, screenshots, window/graphics settings and the
/// command-line test harness. Everything else reaches the rest of the game through <see cref="I"/>.
/// </summary>
public partial class Game : Node
{
    public static Game I;
    public Settings Cfg;
    public Hero Hero;                       // null in the menu
    public int Slot;
    public Player Player; public Level Level; public CameraRig Cam;
    public Hud Hud; public Ui Ui; public AudioMan Audio;
    public Node3D WorldRoot;                // parent of the current Level
    public CanvasLayer UiLayer;
    public enum GameState { Boot, Menu, Loading, Play }
    public GameState State;
    public int PauseLocks;                  // Ui increments while a pausing panel is open
    public bool Paused => State != GameState.Play || PauseLocks > 0;
    public bool UiBlocksMouse => Ui != null && (Ui.AnyOpen || Ui.MouseOverUi);
    public double Time;                     // unpaused play seconds since launch
    public int WorldTier => Math.Clamp(Hero?.D.Tier ?? 0, 0, 3);
    public event Action<string> AreaEntered;

    /// <summary>The combat area a town portal was opened from (null when there is none). A portal in town calls <see cref="UseReturnPortal"/>.</summary>
    public string ReturnArea;
    /// <summary>Where in <see cref="ReturnArea"/> the hero stood when the portal was opened.</summary>
    public Vector2 ReturnPos;
    /// <summary>True when any test-harness switch was given on the command line.</summary>
    public bool Harness => _args.Count > 0;
    /// <summary>The self-playing test hero (--bot), if enabled.</summary>
    public TestBot Bot;
    /// <summary>Demo mode (DemoMode.cs): nothing is written — no autosave, no area / level-up / menu / quit saves.</summary>
    public bool NoSave;
    /// <summary>Demo mode: the loading card shows these instead of the area name, its subtitle and a tip (null = the usual text).</summary>
    public string CardTitle, CardSub, CardTip;

    public const int HarnessSlot = 0;      // --new characters live in slot0.json, which the menu (slots 1..8) never lists
    /// <summary>The slot of a test hero (--new, the UI test layer "newgame", TestSuite.StartHero): --slot when it is 0 or 90..99, else slot 0; never a menu slot 1-8.</summary>
    public int TestSlot => int.TryParse(ArgValue("slot"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) && (s == 0 || s >= 90 && s <= 99) ? s : HarnessSlot;
    private static readonly StringName HeroPosGlobal = "hero_pos";      // [shader_globals] in project.godot

    // ── boot / window ──
    private readonly Dictionary<string, string> _args = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] KnownSwitches =
    {
        "win", "new", "load", "area", "level", "gear", "god", "bot", "ui", "shot", "shot-at", "quit-at", "mute", "tier", "verbose",
        // QA harness (see SetupHarness): time scale, scripted suites, directed bot, death / screenshot-key / save tests
        "speed", "suite", "focus", "campaign", "allskills", "hardcore", "slot", "die-at", "prtsc-at", "f12-at", "ui-tour", "paragon", "tour", "seed", "perf-off", "bossrush", "blur-at", "focus-at",
        // switches read by the UI and rendering test layers (listed so they are accepted without the "--" separator too)
        "textscale", "menu", "uido", "uiclick", "uikeys", "hover", "uidir", "gfx-tour",
        "portrait-stats", "portrait-off", "portrait-scale", "portrait-model",       // inventory hero portrait (HeroPortrait3D)
        "bestiary", "no-bestiary", "spawn", "spawn-aggro",                             // monster roster (Bestiary.cs, Populate.HarnessSpawns)
        "gptest", "gpexpect", "gpthen", "gpclasses", "gpcancel", "gpout", "gpcases", "gpslots", "gpold", "gpresume", "stash-keep",                                           // gameplay harness (GameplayTest.cs): click-to-attack, save resume
        "demo", "demo-idle", "demo-secs", "demo-log", "pilot-at", "pilot-test",                      // demo mode (DemoMode.cs) and the AI pilot (AiPilot.cs)
        "gear-off", "gear-baked", "gear-ph", "gear-dir", "icon3d-off", "gear-ik",                // modular gear and 3D item icons (GearVisual.cs, ItemIcon3D.cs)
    };
    private bool _forceWindow, _windowPlaced, _quitting;
    private bool? _cfgFullscreen;
    private double _gfxDirty = -1;
    private Node _menu;
    private ulong _lastTickMs, _bootMs;

    // ── loading ──
    private enum LoadStep { Idle, FadeOut, Hold, Build, Settle, FadeIn }
    private LoadStep _load;
    private string _loadArea, _loadEntry, _failNote;
    private bool _loadReturn, _travelGuard;
    private int _loadFrames;
    private double _loadT;
    private ulong _overlayShownMs;
    private float _ovAlpha;
    private Action _afterLoad;
    private CanvasLayer _ovLayer;
    private Control _overlay;
    private Label _ovTitle, _ovSub, _ovTip;
    private int _lastTip = -1;
    private int _levelSeed, _returnSeed;
    private LevelSnapshot _resume, _returnState;      // the state to rebuild on loading a save / on taking the town portal back (LevelState)
    private readonly Dictionary<string, (int seed, bool[] seen)> _seen = new();

    // ── play ──
    private double _combatT, _perfT, _autosaveT, _perfWorst, _perfSum;
    private int _perfFrames;
    private float _combat;
    private string _saveReq;
    private ulong _saveReqAt;
    private bool _deathPending;
    private double _deathClock, _deathNext;
    private int _deathTries;
    private ulong _lastHitStopMs, _hitStopUntilMs;
    private int _hitStopToken;

    // ── screenshots ──
    private bool _shotRequested;
    private readonly ConcurrentQueue<(string path, bool ok, string err, bool announce)> _shotDone = new();
    private readonly List<Task> _shotTasks = new();
    private readonly HashSet<string> _shotNames = new(StringComparer.OrdinalIgnoreCase);

    // ── test harness ──
    private string _hArea, _hUi, _shotPath;
    private readonly List<double> _shotTimes = new();
    private int _shotIndex;
    private double _hClock = -1, _quitAt = -1;
    private bool _god, _hAreaUsed, _hUiUsed;
    private double _baseScale = 1.0;        // --speed: the time scale the game returns to (hit-stop is skipped when it is not 1)
    private double _dieAt = -1, _prtscAt = -1, _f12At = -1;
    private int _hLevel = -1, _hTier = -1, _hParagon = -1;
    private bool _hGear;
    // --ui-tour: every panel in turn, one screenshot each
    private List<(string id, string arg)> _tour;
    private int _tourIndex = -1, _tourPhase;
    private double _tourT;
    /// <summary>The time scale the game runs at outside hit-stop (1 unless --speed was given).</summary>
    public double BaseTimeScale => _baseScale;
    /// <summary>True when --god keeps the hero alive.</summary>
    public bool God => _god;

    private static readonly string[] Tips =
    {
        "按住 Shift 再施放技能，就能站在原地出手，不會朝游標走過去。",
        "以空白鍵閃避的那一瞬間，任何攻擊都傷不到你。",
        "地上亮起的紅色區域，是即將落下的攻擊。離開它。",
        "點亮一個區域的燼塔之後，就能從地圖隨時往返該地。",
        "菁英怪物的名字下方寫著牠的詞綴。先讀，再動手。",
        "控制效果會累積頭目的踉蹌條；填滿時牠會倒地，並承受更重的傷害。",
        "藥水每三十秒自行恢復一瓶，別捨不得喝。",
        "拆解不要的裝備能換得強化材料；傳奇威能會被永久記入圖鑑。",
        "脫離戰鬥時按 T 可以開啟回鎮的燼門，鎮上的燼門會送你回到原處。",
        "每一座燼火祭壇都會永久提高你的全部屬性，值得繞路去找。",
        "技能點數隨時可以免費重置，放心嘗試不同的搭配。",
        "按住 Alt 可以顯示地上所有戰利品的名稱。",
        "十五級時可以選擇職業專精，它會改變你所有技能的運作方式。",
        "世界階級越高，怪物越兇猛，掉落的寶物也越好。",
        "滾動滑鼠滾輪可以拉近或拉遠視角。",
        "按 PrtSc 或 F12 擷取畫面：預設複製到剪貼簿，可直接貼上；在設定中可改為存成檔案（圖片\\AshenAbyss）。",
    };

    public Game() { I = this; }

    // ═════════════════════════ boot ═════════════════════════

    public override void _Ready()
    {
        I = this;
        Log.Init();
        _bootMs = Godot.Time.GetTicksMsec();
        // Test launches (any command-line switches) use their own lock: they never block the player's game, and they
        // never run while it runs (SingleInstance.PlayerRunning), so a test can not disturb a session in progress.
        bool test = OS.GetCmdlineUserArgs().Length > 0;
        if (!SingleInstance.Acquire(test) || (test && SingleInstance.PlayerRunning()))
        {
            // another copy is already running: hand the screen back to it and leave without touching saves or settings
            Log.Guard("boot", () => DisplayServer.WindowSetMode(DisplayServer.WindowMode.Minimized));
            Log.Info("boot", test ? "the player's game (or another test) is running; this test launch exits without touching it"
                                  : $"another instance is already running; {SingleInstance.ActivateOther()}; this one exits");
            GetTree().Quit();
            return;
        }
        try { Boot(); }
        catch (Exception e) { Log.Error("boot", "boot failed", e); }
    }

    private void Boot()
    {
        ParseArgs();
        if (Arg("verbose")) Log.Verbose = true;
        Cfg = Settings.Load() ?? new Settings();
        GetTree().AutoAcceptQuit = false;
        _forceWindow = Arg("win");
        Log.Guard("boot", ApplyWindowMode);
        Log.Guard("boot", WinKeys.Start);
        Log.Guard("boot", SetAppIcon);

        WorldRoot = new Node3D { Name = "World" };
        AddChild(WorldRoot);
        Cam = new CameraRig();
        AddChild(Cam);
        UiLayer = new CanvasLayer { Name = "UiLayer", Layer = 1 };
        AddChild(UiLayer);

        Log.Guard("boot", () => { var theme = UiTheme.Theme; if (theme != null) GetTree().Root.Theme = theme; });
        Log.Guard("boot", () => { Audio = new AudioMan(); AddChild(Audio); });
        Log.Guard("boot", () =>
        {
            Hud = new Hud();
            UiLayer.AddChild(Hud);
            Hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            Hud.Visible = false;
        });
        Log.Guard("boot", () =>
        {
            Ui = new Ui();
            UiLayer.AddChild(Ui);
            Ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        });
        Log.Guard("boot", BuildOverlay);
        Log.Guard("boot", () => AddChild(new Cursors { Name = "Cursors" }));        // contextual mouse cursor (ticks itself)
        Log.Guard("boot", () => AddChild(new AiPilot { Name = "AiPilot" }));        // AI 代打: F8 / pause menu (AiPilot.cs)
        Log.Guard("boot", () => MinionSystem.Install(this));                         // [golem] minion persistence, golems, the book (MinionSystem.cs)

        ApplySettings();
        if (Arg("mute")) Log.Guard("boot", () => Audio?.Mute(true));

        World.Event += OnWorldEvent;
        GetTree().Root.SizeChanged += OnWindowResized;

        Log.Guard("boot", Bestiary.Boot);           // the designed roster: one summary line, the data checks, models and stand-ins
        SetupHarness();
        if (_args.Keys.Any(k => k.StartsWith("gfx", StringComparison.Ordinal))) Log.Guard("test", () => AddChild(new EnvProbe()));   // graphics review harness (--gfx-*)
        StartFromArgs();
        Log.Info("boot", $"boot finished in {Godot.Time.GetTicksMsec() - _bootMs} ms, state={State}");
    }

    private void ParseArgs()
    {
        foreach (var raw in OS.GetCmdlineUserArgs()) AddArg(raw, false);
        foreach (var raw in OS.GetCmdlineArgs()) AddArg(raw, true);        // forgiving: our switches given without the "--" separator
        if (_args.Count > 0)
            Log.Info("boot", "switches: " + string.Join(" ", _args.Select(kv => kv.Value.Length > 0 ? $"--{kv.Key}={kv.Value}" : $"--{kv.Key}")));
    }

    private void AddArg(string raw, bool knownOnly)
    {
        if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith('-')) return;
        string s = raw.TrimStart('-');
        string name = s, value = "";
        int eq = s.IndexOf('=');
        if (eq >= 0) { name = s[..eq]; value = s[(eq + 1)..].Trim().Trim('"'); }
        name = name.Trim().ToLowerInvariant();
        if (name.Length == 0) return;
        if (knownOnly && Array.IndexOf(KnownSwitches, name) < 0) return;
        if (!_args.ContainsKey(name)) _args[name] = value;
    }

    /// <summary>True when the switch was given (with or without a value). The leading dashes are optional: Arg("win") == Arg("--win").</summary>
    public bool Arg(string name) => name != null && _args.ContainsKey(name.TrimStart('-'));
    /// <summary>The value of --name=value; "" when the switch has no value; null when it was not given.</summary>
    public string ArgValue(string name) => name != null && _args.TryGetValue(name.TrimStart('-'), out var v) ? v : null;

    private void SetAppIcon()
    {
        // icons under res://assets/icons are not imported (.gdignore): look on disk, next to the executable in a build
        string exeDir = System.IO.Path.GetDirectoryName(OS.GetExecutablePath()) ?? "";
        string[] candidates =
        {
            ProjectSettings.GlobalizePath("res://assets/icons/app_icon.png"), ProjectSettings.GlobalizePath("res://icon.png"),
            System.IO.Path.Combine(exeDir, "assets", "icons", "app_icon.png"), System.IO.Path.Combine(exeDir, "icon.png"),
        };
        foreach (var path in candidates)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) continue;
            var img = Image.LoadFromFile(path);
            if (img == null || img.IsEmpty()) continue;
            DisplayServer.SetIcon(img);
            Log.Info("boot", $"window icon: {path}");
            return;
        }
    }

    private void StartFromArgs()
    {
        if (Arg("gpresume")) { LevelState.Enabled = true; Log.Info("test", "--gpresume: exact save resume switched on for this run"); }
        if (Arg("suite"))
        {
            // a scripted suite creates its own characters; the bot node hosts it
            ShowMenu();
            Bot = new TestBot { Name = "TestBot" };
            AddChild(Bot);
            Log.Info("test", $"TestBot enabled for suite '{ArgValue("suite")}'");
            return;
        }
        bool wantNew = Arg("new") || (Arg("bot") && !Arg("load"));
        if (Arg("load"))
        {
            if (int.TryParse(ArgValue("load"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int slot)) LoadGame(slot);
            else Log.Error("test", $"--load needs a slot number, got '{ArgValue("load")}'");
        }
        else if (wantNew)
        {
            string key = (ArgValue("new") ?? "").Trim().ToLowerInvariant();
            int idx = Array.IndexOf(Names.ClassKeys, key);
            if (idx < 0)
            {
                if (key.Length > 0) Log.Warn("test", $"--new: unknown class '{key}', using {Names.ClassKeys[0]} (valid: {string.Join(", ", Names.ClassKeys)})");
                idx = 0;
            }
            // 0 is the shared harness slot; 90-99 are private harness slots (never listed by the menu) for runs that are continued
            // with --load. A menu slot (1-8: the player's characters) is never written by a test hero.
            int slot = TestSlot;
            if (Arg("slot") && ArgValue("slot") != slot.ToString(CultureInfo.InvariantCulture)) Log.Warn("test", $"--slot={ArgValue("slot")} is not a test slot (0, 90-99): the test hero uses slot {slot}");
            NewGame(slot, "試煉者", (ClassId)idx, Arg("hardcore"));
        }
        if (Hero == null) ShowMenu();
        if (Arg("bot"))
        {
            Bot = new TestBot { Name = "TestBot" };
            AddChild(Bot);
            Log.Info("test", "TestBot enabled");
        }
        if (Arg("gptest")) AddChild(new GameplayTest { Name = "GameplayTest" });      // gameplay harness (bot off)
        if (Arg("demo") && Hero == null) Callable.From(() => DemoMode.Start(false)).CallDeferred();     // --demo: 展示模式 straight from the title
    }

    // ═════════════════════════ settings / window ═════════════════════════

    private bool WantFullscreen => Cfg != null && Cfg.Fullscreen && !_forceWindow;

    private void ApplyWindowMode()
    {
        var mode = DisplayServer.WindowGetMode();
        bool isFull = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        if (WantFullscreen)
        {
            if (mode != DisplayServer.WindowMode.ExclusiveFullscreen)
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                Log.Info("cfg", "window → exclusive fullscreen");
            }
            _windowPlaced = false;
        }
        else if (isFull || !_windowPlaced)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
            int screen = DisplayServer.WindowGetCurrentScreen();
            Rect2I area = DisplayServer.ScreenGetUsableRect(screen);
            var size = new Vector2I(1600, 900);
            if (area.Size.X > 200 && area.Size.Y > 200)
                size = new Vector2I(Math.Min(1600, area.Size.X - 40), Math.Min(900, area.Size.Y - 80));
            DisplayServer.WindowSetSize(size);
            if (area.Size.X > 200) DisplayServer.WindowSetPosition(area.Position + (area.Size - size) / 2);
            _windowPlaced = true;
            Log.Info("cfg", $"window → {size.X}x{size.Y} centred on screen {screen}");
        }
    }

    /// <summary>Window mode, vsync, graphics quality and volumes from <see cref="Cfg"/>. Cheap to call after every settings change.</summary>
    public void ApplySettings()
    {
        if (Cfg == null) return;
        // choosing a display mode in the settings ends the --win override
        if (_cfgFullscreen.HasValue && _cfgFullscreen.Value != Cfg.Fullscreen) _forceWindow = false;
        _cfgFullscreen = Cfg.Fullscreen;
        Log.Guard("cfg", ApplyWindowMode);
        Log.Guard("cfg", () =>
        {
            DisplayServer.WindowSetVsyncMode(Cfg.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = Cfg.VSync ? 0 : 240;
        });
        Gfx.Apply(Cfg, GetViewport());
        var lv = Level;
        if (lv != null && IsInstanceValid(lv) && lv.Env != null && IsInstanceValid(lv.Env) && lv.Env.Environment != null)
            Gfx.TuneEnvironment(lv.Env.Environment, Cfg);
        Log.Guard("cfg", () => Audio?.ApplyVolumes(Cfg));
        Log.Guard("cfg", RefreshOverlayFonts);
        _gfxDirty = 0.35;         // once more when the window has settled into its new size (the automatic scale depends on it)
    }

    private void OnWindowResized() => _gfxDirty = 0.3;

    private void ToggleFullscreen()
    {
        bool now = WantFullscreen;
        _forceWindow = false;
        Cfg.Fullscreen = !now;
        Cfg.Save();
        Log.Info("cfg", $"F11: fullscreen={Cfg.Fullscreen}");
        ApplySettings();
    }

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
        if (k.Keycode == Key.F12)
        {
            _shotRequested = true;
            GetViewport().SetInputAsHandled();
        }
        else if (k.Keycode == Key.F11)
        {
            Log.Guard("cfg", ToggleFullscreen);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) Quit();
        else if (what == NotificationApplicationFocusOut) Log.Guard("game", () => SetAppFocus(false, "window"));
        else if (what == NotificationApplicationFocusIn) Log.Guard("game", () => SetAppFocus(true, "window"));
    }

    // ───────── Alt-Tab / minimise: the game pauses (pause menu) and goes silent while it is not the active window ─────────
    private bool _appFocused = true;
    public bool AppFocused => _appFocused;
    private double _blurAt = -1, _focusAt = -1;

    private void SetAppFocus(bool focused, string source)
    {
        // Harness runs are started in windows that are usually not in front; real focus events would freeze every
        // test. They are ignored there; --blur-at / --focus-at simulate the events instead.
        if (Harness && source == "window") return;
        if (_appFocused == focused) return;
        _appFocused = focused;
        Log.Info("game", focused ? $"application focus regained ({source})" : $"application focus lost ({source})");
        Audio?.Mute(!focused || Arg("mute"));
        if (!focused) PauseForFocusLoss();
    }

    /// <summary>Opens the pause menu while the window is in the background (called on focus loss and every frame).</summary>
    private void PauseForFocusLoss()
    {
        if (_appFocused || State != GameState.Play || Player == null || Ui == null || DemoMode.Active) return;   // the demo ends itself instead
        if (Ui.IsOpen("pause") || Ui.IsOpen("death") || Ui.IsOpen("settings")) return;
        Ui.Open("pause");
        Log.Info("game", "pause menu opened because the game window is in the background");
    }

    // ═════════════════════════ frame ═════════════════════════

    public override void _Process(double delta)
    {
        // Game is the root: its _Process opens the frame. The pre-draw signal closes it, so the difference is everything the
        // scene tree did this frame (scripts, animation, tweens, UI), i.e. the CPU side of the frame without rendering.
        Prof.FrameStart = Prof.Now;
        if (!_preDrawHooked) { _preDrawHooked = true; RenderingServer.FramePreDraw += () => { if (Prof.FrameStart != 0) Prof.Tree += Prof.Now - Prof.FrameStart; Prof.FrameStart = 0; }; }
        ulong now = Godot.Time.GetTicksMsec();
        double real = _lastTickMs == 0 ? delta : Math.Min(0.25, (now - _lastTickMs) / 1000.0);
        _lastTickMs = now;
        try { Tick(delta, real, now); }
        catch (Exception e) { Log.Error("game", "frame failed", e); }
    }

    private void Tick(double delta, double real, ulong now)
    {
        // hit-stop must never outlive its window, whatever happens to the timer
        if (_hitStopUntilMs != 0) { if (now >= _hitStopUntilMs + 20) EndHitStop(); }
        else if (Engine.TimeScale != _baseScale) Engine.TimeScale = _baseScale;

        // a level that finishes loading while the player is in another window starts paused
        if (!_appFocused) PauseForFocusLoss();

        bool prtsc = WinKeys.ConsumeScreenshotRequest();
        if (prtsc || _shotRequested)
        {
            _shotRequested = false;
            TakeScreenshot(prtsc);
        }
        DrainShots();

        if (_gfxDirty >= 0)
        {
            _gfxDirty -= real;
            if (_gfxDirty < 0) Gfx.Apply(Cfg, GetViewport());
        }

        if (_load != LoadStep.Idle) TickLoading(real, now);

        if (State == GameState.Play)
        {
            if (!Paused) Time += delta;
            if (Hero != null) Hero.D.PlayTime += real;
            TickPlay(real, now);
        }
        TickHarness(real, now);
    }

    private void TickPlay(double real, ulong now)
    {
        _combatT += real;
        if (_combatT >= 0.5)
        {
            _combatT = 0;
            Log.Guard("game", CombatTick);
            if (_god && Hero != null) EnsureGod(Hero);
        }

        _perfFrames++; _perfSum += real; if (real > _perfWorst) _perfWorst = real;
        _perfT += real;
        if (_perfT >= 5.0)
        {
            double avg = _perfFrames > 0 ? _perfSum / _perfFrames * 1000.0 : 0;
            Log.Info("perf", $"fps={Engine.GetFramesPerSecond():0} frame={avg:0.0}ms worst={_perfWorst * 1000.0:0.0}ms slowestProcess={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0:0.0}ms area={Level?.AreaId ?? "-"} " +
                $"monsters={World.Monsters.Count} minions={World.Minions.Count} objects={Interactable.All.Count} " +
                $"nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0} draws={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0} " +
                $"prims={Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame):0} vram={Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / (1024.0 * 1024.0):0}MB " +
                $"mem={Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024.0 * 1024.0):0}MB scale={Gfx.Scale:0.00} q={Gfx.Quality} errors={Log.ErrorCount}");
            Log.Info("perf", "game code per frame: " + Prof.Report(_perfFrames));
            _perfT = 0; _perfFrames = 0; _perfSum = 0; _perfWorst = 0;
        }

        var p = Player;
        bool alive = p != null && IsInstanceValid(p) && !p.Dead;
        _autosaveT += real;
        if (_autosaveT >= 180.0 && alive && _load == LoadStep.Idle) SaveNow("auto");
        if (_saveReq != null && now >= _saveReqAt && _load == LoadStep.Idle) SaveNow(_saveReq);

        if (_deathPending)
        {
            if (alive || p == null || !IsInstanceValid(p)) _deathPending = false;
            else
            {
                _deathClock += real;
                bool open = false;
                if (Ui != null) { try { open = Ui.IsOpen("death"); } catch (Exception e) { Log.Error("ui", "IsOpen(death) failed", e); } }
                if (!open && _deathClock >= _deathNext)
                {
                    if (Ui != null && _deathTries < 3)
                    {
                        _deathTries++;
                        _deathNext = _deathClock + 2.5;
                        Log.Guard("ui", () => Ui.Open("death"));
                    }
                    else
                    {
                        Log.Warn("game", "death screen unavailable or dismissed; continuing automatically");
                        Respawn();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Twice a second: how hot the fight around the hero is, which drives the music's combat layer. Normal monsters that
    /// have noticed the hero within 22 m count 1, champions 2, elites 3, named ones double; an engaged boss fills it.
    /// (Boss music and the boss-fight zoom belong to Level, which knows when a fight starts, resets and ends.)
    /// </summary>
    private void CombatTick()
    {
        var p = Player;
        if (p == null || !IsInstanceValid(p) || !p.IsInsideTree()) return;
        float sum = 0f;
        if (!p.Dead)
        {
            Vector2 at = p.Pos2;
            var list = World.Monsters;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null || m.Dead || !IsInstanceValid(m) || !m.IsInsideTree()) continue;
                float d2 = m.Pos2.DistanceSquaredTo(at);
                if (m.Brain != null)
                {
                    if (d2 < 45f * 45f && (m.Aggro || m.Life < m.MaxLife)) sum += 6f;
                    continue;
                }
                if (!m.Aggro || d2 > 22f * 22f) continue;
                sum += (m.Tier == MonTier.Elite ? 3f : m.Tier == MonTier.Champion ? 2f : 1f) * (m.MiniBoss ? 2f : 1f);
            }
        }
        float target = Mathf.Clamp(sum / 6f, 0f, 1f);
        // rise at once, fall slowly, so the combat layer does not flutter between packs
        _combat = target >= _combat ? target : Mathf.MoveToward(_combat, target, 0.07f);
        Audio?.SetCombat(_combat);
    }

    // ═════════════════════════ session start ═════════════════════════

    public void NewGame(int slot, string name, ClassId cls, bool hardcore)
    {
        if (State == GameState.Loading) { Log.Warn("game", "NewGame ignored: a level is loading"); return; }
        if (string.IsNullOrWhiteSpace(name)) name = Names.Class(cls);
        name = name.Trim();
        Log.Info("game", $"new game: slot={slot} name={name} class={Names.ClassKey(cls)} hardcore={hardcore}");
        SaveData d;
        try { d = AshenAbyss.Hero.NewCharacter(name, cls, hardcore); }
        catch (Exception e) { Log.Error("game", "creating the character failed", e); Notify("無法創建角色。詳情請見日誌。"); return; }
        BeginSession(slot, d, true);
    }

    public void LoadGame(int slot)
    {
        if (State == GameState.Loading) { Log.Warn("game", "LoadGame ignored: a level is loading"); return; }
        var d = SaveSystem.Load(slot);
        if (d == null)
        {
            Log.Warn("game", $"slot {slot}: nothing to load");
            Notify("這個欄位沒有可以讀取的紀錄。");
            return;
        }
        if (d.Hardcore && d.Dead)
        {
            Log.Warn("game", $"slot {slot}: hardcore character '{d.Name}' is dead; load refused");
            Notify("這位持燼者的燼火已經熄滅，再也無法喚醒。");
            return;
        }
        Log.Info("game", $"load game: slot={slot} name={d.Name} lv{d.Level} area={d.Area} playtime={d.PlayTime:0}s");
        BeginSession(slot, d, false);
    }

    private void BeginSession(int slot, SaveData d, bool isNew)
    {
        if (Hero != null)          // switching characters without passing through the menu
        {
            SaveNow("switch");
            Log.Guard("game", TearDownLevel);
            Log.Guard("quest", () => Quests.Detach());
        }
        Slot = slot;
        d.AreaVisits ??= new();
        ClearReturn();
        _seen.Clear();
        _deathPending = false;
        _saveReq = null;
        _autosaveT = 0;

        ApplyHarnessToSave(d);
        Log.Guard("stash", () => SharedStash.Attach(d, slot, isNew));      // the account's shared stash, read fresh (SharedStash.cs)
        try { Hero = new Hero(d); Hero.PreloadSetModels(); }      // RENDER: every saved set's models loaded and pinned (instant swaps)
        catch (Exception e)
        {
            Log.Error("game", "building the hero failed", e);
            Hero = null;
            Notify("這份紀錄無法讀取。詳情請見日誌。");
            if (State != GameState.Menu || _menu == null) ShowMenu();
            return;
        }
        Hero.ParagonUp += OnParagonUp;
        ApplyHarnessToHero(Hero);
        Log.Guard("quest", () => Quests.Attach(Hero));
        if (slot >= 1 && slot <= SaveSystem.SlotCount && Cfg.LastSlot != slot) { Cfg.LastSlot = slot; Cfg.Save(); }

        if (DemoMode.Active) HideMenu(); else CloseMenu();      // the demo keeps the title screen for its return
        Log.Guard("ui", () => Ui?.CloseAll());
        PauseLocks = 0;

        string entry = null;
        string start = isNew ? ZoneDb.Town : ResumeArea(d, out entry);
        _resume = null;
        if (!isNew)
        {
            try { if (TakeResume(d)) { start = _resume.Area; entry = null; } }
            catch (Exception e) { Log.Error("save", "reading the resume state failed; waking at a portal as before", e); _resume = null; ClearReturn(); }
        }
        Action after = null;
        bool skipIntro = !isNew || _hUi != null || _hArea != null || Arg("bot") || _shotTimes.Count > 0;
        if (!skipIntro) after = PlayIntro;
        if (_hArea != null && !_hAreaUsed)
        {
            _hAreaUsed = true;
            if (IsKnownArea(_hArea)) { start = _hArea; entry = null; _resume = null; ClearReturn(); }
            else Log.Warn("test", $"--area: unknown area '{_hArea}', starting in {start}");
        }
        if (_hUi != null && !_hUiUsed) { _hUiUsed = true; after = OpenHarnessUi; }

        if (!Travel(start, entry, true, after))
        {
            Log.Error("game", $"could not start travelling to {start}");
            ToMenu();
        }
    }

    /// <summary>Where a loaded character wakes up: town, the overworld they were in, or the mouth of the dungeon they were in.</summary>
    private string ResumeArea(SaveData d, out string entry)
    {
        entry = null;
        string a = d.Area;
        if (string.IsNullOrEmpty(a) || a == ZoneDb.Town || a.StartsWith("rift")) return ZoneDb.Town;
        if (ZoneDb.Region(a) != null)
        {
            if (!CanEnterSafe(a)) return ZoneDb.Town;
            entry = d.Waypoints != null && d.Waypoints.Contains(a) ? "waypoint" : null;
            return a;
        }
        var parent = ZoneDb.RegionOf(a);
        if (parent != null && CanEnterSafe(parent.Id)) { entry = a; return parent.Id; }
        return ZoneDb.Town;
    }

    private void PlayIntro()
    {
        var script = DialogueDb.Get("intro");
        if (script == null) { Log.Warn("story", "intro dialogue is missing"); return; }
        if (Ui == null) return;
        Ui.Open("dialogue", script);
        Log.Info("story", "intro dialogue started");
    }

    private void OnParagonUp(int level)
    {
        Log.Info("hero", $"paragon level → {level}");
        Log.Guard("hud", () => Hud?.Toast($"燼紋等級提升：{level}", Palette.Ember));
        RequestSave("paragon", 500);
    }

    private void Notify(string text)
    {
        Log.Guard("ui", () =>
        {
            Snd.Ui("ui_error");
            if (State == GameState.Play && Hud != null) Hud.Warn(text);
            else Ui?.Confirm(text, () => { }, "明白", "關閉");
        });
    }

    // ═════════════════════════ menu ═════════════════════════

    private void SetState(GameState s)
    {
        if (State != s) { Log.Info("game", $"state {State} → {s}"); State = s; }
        if (Hud != null && IsInstanceValid(Hud)) Hud.Visible = s == GameState.Play;
    }

    private void ShowMenu()
    {
        SetState(GameState.Menu);
        if (_menu == null || !IsInstanceValid(_menu))
        {
            Log.Guard("menu", () =>
            {
                Node m = new MainMenu();
                _menu = m;
                UiLayer.AddChild(m);
                if (m is Control c) c.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                if (Ui != null && IsInstanceValid(Ui) && Ui.GetParent() == UiLayer && m.GetParent() == UiLayer)
                    UiLayer.MoveChild(m, Ui.GetIndex());        // panels (settings) open above the menu
            });
        }
        else if (_menu is CanvasItem shown && !shown.Visible) shown.Visible = true;      // back from the demo: the same title screen
        Log.Guard("audio", () => { Audio?.SetCombat(0f); Audio?.PlayMusic("menu_theme"); });
        if (_hClock < 0) StartHarnessClock("menu");
    }

    private void CloseMenu()
    {
        if (_menu != null && IsInstanceValid(_menu)) _menu.QueueFree();
        _menu = null;
    }

    /// <summary>Demo mode: the title screen stays (hidden; it gates its own backdrop) and comes back as it was.</summary>
    private void HideMenu()
    {
        if (_menu is CanvasItem ci && IsInstanceValid(ci)) ci.Visible = false;
    }

    /// <summary>
    /// Demo mode (DemoMode.cs): plays an in-memory character (never saved) in <paramref name="area"/>, with no quest gating,
    /// no intro and no town-portal memory. Replaces the current demo character, if any.
    /// </summary>
    public void DemoSession(SaveData d, string area)
    {
        if (d == null) return;
        _hArea = area; _hAreaUsed = false;
        BeginSession(-1, d, false);
    }

    /// <summary>Saves and returns to the title screen.</summary>
    public void ToMenu()
    {
        Log.Info("game", "returning to the title screen");
        EndHitStop();
        if (Hero != null) SaveNow("menu");
        _resume = null;
        _load = LoadStep.Idle;
        _afterLoad = null;
        _failNote = null;
        Log.Guard("ui", () => Ui?.CloseAll());
        PauseLocks = 0;
        Log.Guard("game", TearDownLevel);
        Log.Guard("quest", () => Quests.Detach());
        if (Hero != null) Hero.ParagonUp -= OnParagonUp;
        Hero = null;
        SharedStash.Detach();
        ClearReturn();
        _seen.Clear();
        _deathPending = false;
        _combat = 0f;
        SetOverlayAlpha(0f);
        ShowMenu();
    }

    public void Quit()
    {
        if (_quitting) return;
        _quitting = true;
        Log.Info("game", "quitting");
        EndHitStop();
        if (Hero != null) SaveNow("quit");
        Log.Guard("cfg", () => Cfg?.Save());
        try { Task.WaitAll(_shotTasks.ToArray(), 8000); } catch (Exception e) { Log.Error("shot", "waiting for screenshots failed", e); }
        DrainShots();
        if (Bot != null && IsInstanceValid(Bot)) Log.Guard("bot", () => Bot.Summary("final"));
        // animation self-check of the whole session (see AnimCheck): an error line when actors slid about without a walk cycle
        if (AnimCheck.HeroMove + AnimCheck.HeroIdle + AnimCheck.MonMove > 1.0 || CharModel.Unanimated > 0)
        {
            if (AnimCheck.Clean) Log.Info("anim", AnimCheck.Report()); else Log.Error("anim", AnimCheck.Report());
        }
        WinKeys.Stop();
        Log.Close();
        GetTree().Quit();
    }

    public override void _ExitTree()
    {
        World.Event -= OnWorldEvent;
        if (Engine.TimeScale != 1.0) Engine.TimeScale = 1.0;
    }

    // ═════════════════════════ areas ═════════════════════════

    /// <summary>
    /// Travels to an area behind the loading screen: "town", a region id (its overworld), a dungeon id or "rift:&lt;tier&gt;".
    /// <paramref name="entry"/> is the area the hero comes from (they appear at the matching entrance), "waypoint", or null.
    /// Quest gating is checked first; a refused or failed load never leaves the game without a level.
    /// </summary>
    public void EnterArea(string areaId, string entry = null) => Travel(areaId, entry, false, null);

    /// <summary>Takes the town portal back to where it was opened (see <see cref="ReturnArea"/>).</summary>
    public void UseReturnPortal()
    {
        if (ReturnArea == null) { Log.Warn("area", "UseReturnPortal: no portal is open"); return; }
        Travel(ReturnArea, "return", true, null);
    }

    private static bool IsKnownArea(string id) =>
        !string.IsNullOrEmpty(id) && (id == ZoneDb.Town || id.StartsWith("rift") || ZoneDb.RegionOf(id) != null);

    private static bool CanEnterSafe(string areaId)
    {
        try { return Quests.CanEnter(areaId); }
        catch (Exception e) { Log.Error("quest", $"CanEnter({areaId}) failed", e); return true; }
    }

    private bool Travel(string areaId, string entry, bool force, Action after)
    {
        if (Hero == null) { Log.Warn("area", $"EnterArea({areaId}) ignored: no hero"); return false; }
        if (_travelGuard || State == GameState.Loading || _load is LoadStep.FadeOut or LoadStep.Hold or LoadStep.Build or LoadStep.Settle)
        {
            Log.Warn("area", $"EnterArea({areaId}) ignored: already travelling to {_loadArea}");
            return false;
        }
        if (!IsKnownArea(areaId))
        {
            Log.Warn("area", $"unknown area '{areaId}', going to town instead");
            areaId = ZoneDb.Town; entry = null;
        }
        var cur = Level != null && IsInstanceValid(Level) ? Level : null;
        if (!force)
        {
            if (areaId == ZoneDb.Town && cur != null && cur.IsTown)
            {
                Log.Guard("hud", () => Hud?.Warn("你已身在殘燈鎮。"));
                return false;
            }
            if (!CanEnterSafe(areaId))
            {
                Log.Info("area", $"entry to {areaId} refused by quest gating");
                Log.Guard("hud", () => { Snd.Ui("ui_error"); Hud?.Warn("灰暮仍封鎖著那條路。"); });
                return false;
            }
        }

        // town-portal memory
        bool fromCombat = cur != null && !cur.IsTown;
        bool returning = false;
        var pl = Player != null && IsInstanceValid(Player) ? Player : null;
        if (areaId == ZoneDb.Town)
        {
            // T (no entry given) or an explicit "portal" leaves a way back; walking out through an exit, a waypoint or the
            // portal of a finished rift (which all name where the hero comes from) does not
            if (fromCombat && pl != null && !pl.Dead && (entry == null || entry == "portal"))
            {
                ReturnArea = cur.AreaId; ReturnPos = pl.Pos2; _returnSeed = _levelSeed;
                _returnState = null;
                try { if (LevelState.Enabled) _returnState = LevelState.Capture(cur, pl, _levelSeed); }       // the way back finds the place as it was left
                catch (Exception e) { Log.Error("save", "capturing the level for the town portal failed", e); }
                Log.Info("area", $"town portal opened from {ReturnArea} at ({ReturnPos.X:0.0}, {ReturnPos.Y:0.0})");
                Log.Guard("audio", () => Snd.Ui("portal_open"));
            }
            else if (fromCombat) ClearReturn();          // walked out, took a waypoint or died: no portal stays open
        }
        else
        {
            returning = ReturnArea != null && areaId == ReturnArea && cur != null && cur.IsTown && (entry == null || entry == "return" || entry == "portal");
            if (!returning) ClearReturn();
        }

        _travelGuard = true;
        try { Ui?.CloseAll(); }
        catch (Exception e) { Log.Error("ui", "CloseAll before travelling failed", e); }
        finally { _travelGuard = false; }

        _loadArea = areaId; _loadEntry = entry; _loadReturn = returning; _afterLoad = after; _failNote = null;
        Log.Info("area", $"travel → {areaId} entry={entry ?? "-"} from={cur?.AreaId ?? "-"} force={force} returning={returning}");
        bool fromMenu = State != GameState.Play;
        SetState(GameState.Loading);
        EndHitStop();
        ShowOverlay(areaId, fromMenu);
        _load = LoadStep.FadeOut;
        return true;
    }

    private void ClearReturn() { ReturnArea = null; ReturnPos = Vector2.Zero; _returnSeed = 0; _returnState = null; }

    private void TickLoading(double real, ulong now)
    {
        switch (_load)
        {
            case LoadStep.FadeOut:
                _ovAlpha = Math.Min(1f, _ovAlpha + (float)(real / 0.22));
                SetOverlayAlpha(_ovAlpha);
                if (_ovAlpha >= 1f) { _load = LoadStep.Hold; _loadFrames = 0; }
                break;
            case LoadStep.Hold:             // let the black frame with the area name reach the screen before the build blocks
                if (++_loadFrames >= 2) _load = LoadStep.Build;
                break;
            case LoadStep.Build:
                RunBuild();
                break;
            case LoadStep.Settle:           // first frames of the new level (shader warm-up) stay hidden; keep the card up long enough to read
                _loadT += real; _loadFrames++;
                if (_loadFrames >= 4 && _loadT >= 0.3 && now - _overlayShownMs >= 1100) BeginFadeIn();
                break;
            case LoadStep.FadeIn:
                _ovAlpha = Math.Max(0f, _ovAlpha - (float)(real / 0.45));
                SetOverlayAlpha(_ovAlpha);
                if (_ovAlpha <= 0f) _load = LoadStep.Idle;
                break;
        }
    }

    private void RunBuild()
    {
        string area = _loadArea, entry = _loadEntry;
        var sw = Stopwatch.StartNew();
        try
        {
            BuildArea(area, entry, _loadReturn);
            Log.Info("area", $"entered {area} ({ZoneDb.AreaName(area)}) in {sw.ElapsedMilliseconds} ms: seed={_levelSeed} monsters={World.Monsters.Count} objects={Interactable.All.Count} monsterLevel={Level?.MonsterLevel ?? 0}");
            _load = LoadStep.Settle; _loadT = 0; _loadFrames = 0;
        }
        catch (Exception e)
        {
            Log.Error("area", $"loading area '{area}' (entry={entry ?? "-"}) failed after {sw.ElapsedMilliseconds} ms", e);
            Log.Guard("area", TearDownLevel);
            if (area != ZoneDb.Town)
            {
                // fall back to town on the next frame rather than crash or strand the hero in the void
                _loadArea = ZoneDb.Town; _loadEntry = null; _loadReturn = false;
                ClearReturn();
                _failNote = "前路崩塌了。燼火將你帶回殘燈鎮。";
                SetOverlayText(ZoneDb.Town);
            }
            else
            {
                Log.Error("area", "the town itself could not be built; returning to the title screen");
                ToMenu();
                Notify("世界無法成形。詳情請見日誌。");
            }
        }
    }

    private void BeginFadeIn()
    {
        string area = Level?.AreaId ?? _loadArea;
        SetState(GameState.Play);
        _load = LoadStep.FadeIn;
        Log.Guard("hud", () => Hud?.AreaTitle(ZoneDb.AreaName(area), AreaSubtitle(area)));
        if (_failNote != null) { string note = _failNote; _failNote = null; Log.Guard("hud", () => Hud?.Warn(note)); }
        var after = _afterLoad; _afterLoad = null;
        if (after != null) Log.Guard("game", after);
        if (_hClock < 0) StartHarnessClock("play");
        if (!_perfOffDone && Arg("perf-off")) { _perfOffDone = true; Log.Guard("test", ApplyPerfOff); }
    }

    private bool _perfOffDone, _preDrawHooked;

    /// <summary>--perf-off=hud,anim,ui: switches parts of the game off to see what they cost in the perf line (measurement only).</summary>
    private void ApplyPerfOff()
    {
        string v = (ArgValue("perf-off") ?? "").ToLowerInvariant();
        if (v.Contains("hud") && Hud != null) { Hud.Visible = false; Hud.ProcessMode = ProcessModeEnum.Disabled; }
        if (v.Contains("ui") && UiLayer != null) { UiLayer.Visible = false; UiLayer.ProcessMode = ProcessModeEnum.Disabled; }
        if (v.Contains("anim")) CharModel.NoAnimation = true;
        if (v.Contains("anim") && WorldRoot != null)
            foreach (var n in WorldRoot.FindChildren("*", "AnimationPlayer", true, false)) if (n is AnimationPlayer ap) ap.Active = false;
        if (v.Contains("world") && WorldRoot != null) WorldRoot.Visible = false;
        Log.Info("test", $"--perf-off: {v}");
    }

    private void BuildArea(string areaId, string entry, bool returning)
    {
        var carry = CaptureCarry();
        EndHitStop();
        var oldPlayer = Player;
        if (oldPlayer != null && IsInstanceValid(oldPlayer))
        {
            oldPlayer.Died -= OnPlayerDied;
            if (oldPlayer.IsInsideTree()) Log.Guard("area", oldPlayer.CancelAction);     // ends a channel and its looping sound
        }
        RememberSeen();

        var resume = _resume; _resume = null;
        if (resume != null && resume.Area != areaId) resume = null;
        int seed = resume != null ? resume.Seed : returning && _returnSeed != 0 ? _returnSeed : SeedFor(areaId);
        // a loaded save or the town portal back: the level is rebuilt as it was (dead monsters, used objects, objective, fog)
        LevelState.Pending = resume ?? (returning && _returnState != null && _returnState.Area == areaId && _returnState.Seed == seed ? _returnState : null);
        Log.Info("area", $"building {areaId} entry={entry ?? "-"} seed={seed} tier={WorldTier} heroLevel={Hero.D.Level}");
        // The new level is built while the old one still stands: Level.Build notes where the hero left the old one (for
        // "return" entries), takes over World.Root and populates itself. Only then is the old level released.
        var level = AshenAbyss.Level.Build(areaId, returning ? "return" : entry, seed);
        LevelState.Pending = null;
        if (level == null) throw new InvalidOperationException("Level.Build returned null");
        if (level.GetParent() == null) WorldRoot.AddChild(level);
        ReleaseWorldExcept(level);
        Level = level;                                    // assigned before anything below can fail, so a failure still frees it
        _levelSeed = seed;
        if (World.Root == null || !IsInstanceValid(World.Root)) World.Root = level;
        if (World.Grid == null) World.Grid = level.Grid;
        RestoreSeen(level, areaId, seed);

        var p = new Player();
        level.AddChild(p);
        p.Init(Hero);
        p.Died += OnPlayerDied;
        Player = p;
        World.P = p;
        ApplyCarry(p, carry, level.IsTown);

        Vector2 at = returning ? ReturnPos : level.EntryPos(entry);
        var grid = level.Grid ?? World.Grid;
        if (grid != null)
        {
            at = grid.NearestWalk(at);
            if (!grid.WalkAt(at)) { Log.Warn("area", $"entry point ({at.X:0.0}, {at.Y:0.0}) is not walkable; using the default entrance"); at = grid.NearestWalk(level.EntryPos(null)); }
        }
        p.Teleport(at);
        if (returning) ClearReturn();
        if (resume != null) Log.Guard("save", () => LevelState.ApplyHero(p, resume, level, level.IsTown));
        // the level updates this every unpaused frame; set it now so the hidden warm-up frames already cut walls round the hero
        RenderingServer.GlobalShaderParameterSet(HeroPosGlobal, p.GlobalPosition);

        Cam.Follow(p);
        Cam.SetZoomTarget(level.IsTown ? 0.85f : 1f, 0f);
        Cam.Snap();
        if (level.Env != null && level.Env.Environment != null) Gfx.TuneEnvironment(level.Env.Environment, Cfg);
        World.FootstepSfx = string.IsNullOrEmpty(level.Theme?.Footstep) ? "footstep_stone" : level.Theme.Footstep;
        StartAreaAudio(level);

        Hero.D.Area = areaId;
        _combat = 0f;
        _deathPending = false;
        try { AreaEntered?.Invoke(areaId); }
        catch (Exception e) { Log.Error("area", "an AreaEntered handler failed", e); }
        World.Emit("area", areaId);
        // after the "area" event: the named layer has placed its named monsters; a resumed save restores the level now
        Log.Guard("levelstate", () => LevelState.OnArrived(level));
        SaveNow("area");
    }

    /// <summary>Frees everything in the world except <paramref name="keep"/> (the level that was just built). The hero node goes with its level.</summary>
    private void ReleaseWorldExcept(Level keep)
    {
        var old = Level;
        Player = null;
        World.P = null;
        Cam?.Follow(null);
        if (WorldRoot != null)
        {
            foreach (var child in WorldRoot.GetChildren())
            {
                if (child == keep) continue;
                WorldRoot.RemoveChild(child);      // leaves the tree now: actors and interactables unregister themselves
                child.QueueFree();
            }
        }
        if (old != null && old != keep && IsInstanceValid(old) && !old.IsQueuedForDeletion())
        {
            old.GetParent()?.RemoveChild(old);
            old.QueueFree();
        }
        Level = null;
        Interactable.All.RemoveAll(i => i == null || !IsInstanceValid(i) || !i.IsInsideTree());
    }

    /// <summary>Removes the whole world: level, hero, monsters, effects (menu, failed loads, switching characters).</summary>
    private void TearDownLevel()
    {
        EndHitStop();
        var pl = Player;
        if (pl != null && IsInstanceValid(pl))
        {
            pl.Died -= OnPlayerDied;
            if (pl.IsInsideTree()) Log.Guard("area", pl.CancelAction);      // ends a channel and its looping sound
        }
        RememberSeen();
        ReleaseWorldExcept(null);
        World.Clear();
    }

    /// <summary>
    /// Level seed from the save seed, the area id and a visit counter. Town and the overworld regions stay the same world
    /// for a character (their counter is recorded but not mixed in); dungeons and rifts are rolled anew on every visit.
    /// </summary>
    private int SeedFor(string areaId)
    {
        var d = Hero.D;
        d.AreaVisits ??= new();
        int visit = d.AreaVisits.GetValueOrDefault(areaId) + 1;
        d.AreaVisits[areaId] = visit;
        bool stable = areaId == ZoneDb.Town || ZoneDb.Region(areaId) != null;
        return MixSeed(d.Seed, areaId, stable ? 0 : visit);
    }

    private static int MixSeed(int saveSeed, string areaId, int visit)
    {
        unchecked
        {
            uint h = 2166136261u;                       // FNV-1a; string.GetHashCode differs between runs
            foreach (char c in areaId) { h ^= c; h *= 16777619u; }
            h ^= (uint)saveSeed; h *= 16777619u;
            h ^= (uint)visit * 0x9E3779B1u; h *= 16777619u;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            int seed = (int)(h & 0x7FFFFFFF);
            return seed == 0 ? 1 : seed;
        }
    }

    private void RememberSeen()
    {
        var lv = Level;
        if (lv == null || !IsInstanceValid(lv) || lv.Seen == null || string.IsNullOrEmpty(lv.AreaId)) return;
        _seen[lv.AreaId] = (_levelSeed, lv.Seen);
    }

    /// <summary>The explored map of an area survives leaving and returning within a session, as long as the layout is the same.</summary>
    private void RestoreSeen(Level level, string areaId, int seed)
    {
        var now = level.Seen;
        if (now == null || !_seen.TryGetValue(areaId, out var old)) return;
        if (old.seed != seed || old.seen == null || old.seen.Length != now.Length || ReferenceEquals(old.seen, now)) return;
        for (int i = 0; i < now.Length; i++) if (old.seen[i]) now[i] = true;      // merged: what the level revealed on arrival stays
    }

    private sealed class Carry
    {
        public float LifeFrac, Resource, EvadeCd, SecondWindCd;
        public int Pips, Potions;
        public readonly List<(string id, float left, float max, int charges, int maxCharges)> Cds = new();
    }

    private Carry CaptureCarry()
    {
        var p = Player;
        if (p == null || !IsInstanceValid(p) || p.Dead || p.Hero != Hero) return null;
        var c = new Carry
        {
            LifeFrac = Mathf.Clamp(p.LifeFrac, 0.05f, 1f), Resource = p.Resource, Pips = p.Pips, Potions = p.Potions,
            EvadeCd = p.EvadeCd, SecondWindCd = p.SecondWindCd,
        };
        foreach (var kv in p.Cds) c.Cds.Add((kv.Key, kv.Value.Left, kv.Value.Max, kv.Value.Charges, kv.Value.MaxCharges));
        return c;
    }

    /// <summary>Life fraction, resource, potions and cooldowns follow the hero from area to area. The hearth of Lastlamp mends and restocks.</summary>
    private void ApplyCarry(Player p, Carry c, bool town)
    {
        if (c == null) return;             // a fresh arrival keeps what Player.Init gave: full life and potions
        foreach (var cd in c.Cds)
            p.Cds[cd.id] = new AshenAbyss.Player.Cd { Left = cd.left, Max = cd.max, Charges = cd.charges, MaxCharges = cd.maxCharges };
        p.EvadeCd = c.EvadeCd;
        p.SecondWindCd = c.SecondWindCd;
        p.Pips = Math.Min(c.Pips, Hero.MaxPips);
        p.Resource = Mathf.Clamp(c.Resource, 0f, Hero.MaxResource);
        if (town)
        {
            p.Life = p.MaxLife;
            p.Potions = Hero.D.PotionCap;
        }
        else
        {
            p.Life = Mathf.Max(1f, p.MaxLife * c.LifeFrac);
            p.Potions = Math.Clamp(c.Potions, 0, Hero.D.PotionCap);
        }
    }

    private void StartAreaAudio(Level level)
    {
        string music, amb;
        if (level.IsTown) { music = "town"; amb = "amb_town"; }
        else
        {
            var r = level.Region ?? ZoneDb.RegionOf(level.AreaId);
            if (r == null && level.IsRift)
            {
                // a rift borrows the look and the monsters of a region; it borrows its music too
                string borrowed = level.Theme?.Id switch
                {
                    "crypt" => "crypt", "icehall" or "icecave" => "frost", "brasshall" or "sandtomb" => "sands", "cathedral" => "sanctum",
                    "forge" or "lavacave" => "abyss", "wetcave" => "mire", null => "abyss", _ => "wastes",
                };
                r = ZoneDb.Region(borrowed);
            }
            music = r?.Music ?? "abyss";
            amb = r?.Amb ?? "amb_abyss";
        }
        Log.Guard("audio", () =>
        {
            if (Audio == null) return;
            Audio.PlayMusic(music);
            Audio.PlayAmbience(amb);
            Audio.SetCombat(0f);
        });
    }

    private static string AreaSubtitle(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) return "";
        if (areaId == ZoneDb.Town) return ZoneDb.TownSubtitle;
        if (areaId.StartsWith("rift"))
        {
            int colon = areaId.IndexOf(':');
            return colon > 0 && int.TryParse(areaId[(colon + 1)..], out int tier) && tier > 0 ? $"第 {tier} 層" : "沒有盡頭的深處";
        }
        var region = ZoneDb.Region(areaId);
        if (region != null) return region.Subtitle ?? "";
        return ZoneDb.RegionOf(areaId)?.Name ?? "";
    }

    // ───────────────────────── loading overlay ─────────────────────────

    private void BuildOverlay()
    {
        _ovLayer = new CanvasLayer { Name = "LoadingLayer", Layer = 90 };
        AddChild(_ovLayer);
        _overlay = new Control { Name = "Loading", MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _ovLayer.AddChild(_overlay);
        _overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var bg = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.AddChild(bg);
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 16);
        _overlay.AddChild(box);
        box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _ovTitle = OverlayLabel(60, new Color(0.94f, 0.87f, 0.72f), true);
        box.AddChild(_ovTitle);
        var rule = new ColorRect
        {
            Color = new Color(1f, 0.5f, 0.15f, 0.6f), CustomMinimumSize = new Vector2(260, 2),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        box.AddChild(rule);
        _ovSub = OverlayLabel(24, Palette.TextDim, false);
        box.AddChild(_ovSub);

        _ovTip = OverlayLabel(22, new Color(0.78f, 0.72f, 0.62f), false);
        _ovTip.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _overlay.AddChild(_ovTip);
        _ovTip.AnchorLeft = 0.18f; _ovTip.AnchorRight = 0.82f; _ovTip.AnchorTop = 1f; _ovTip.AnchorBottom = 1f;
        _ovTip.OffsetLeft = 0; _ovTip.OffsetRight = 0; _ovTip.OffsetTop = -190; _ovTip.OffsetBottom = -96;
    }

    /// <summary>The loading screen follows the text-size setting (its labels are built once, at boot).</summary>
    private void RefreshOverlayFonts()
    {
        void Set(Label l, int size) { if (l != null && IsInstanceValid(l)) l.AddThemeFontSizeOverride("font_size", UiTheme.Fs(size)); }
        Set(_ovTitle, 60); Set(_ovSub, 24); Set(_ovTip, 22);
    }

    private static Label OverlayLabel(int size, Color color, bool title)
    {
        var l = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        int px = size;
        try { px = UiTheme.Fs(size); }          // follows the 介面文字大小 setting; never below the readable floor
        catch (Exception e) { Log.Warn("boot", "UiTheme.Fs unavailable for the loading screen: " + e.Message); }
        l.AddThemeFontSizeOverride("font_size", px);
        l.AddThemeColorOverride("font_color", color);
        try
        {
            Font f = title ? UiTheme.Title : UiTheme.Body;
            if (f != null) l.AddThemeFontOverride("font", f);
        }
        catch (Exception e) { Log.Warn("boot", "UiTheme fonts unavailable for the loading screen: " + e.Message); }
        return l;
    }

    private void ShowOverlay(string areaId, bool instant)
    {
        _overlayShownMs = Godot.Time.GetTicksMsec();
        SetOverlayText(areaId);
        int tip = Rng.R.Next(Tips.Length);
        if (tip == _lastTip) tip = (tip + 1) % Tips.Length;
        _lastTip = tip;
        if (_ovTip != null) _ovTip.Text = CardTip ?? Tips[tip];
        if (instant) _ovAlpha = 1f;
        SetOverlayAlpha(_ovAlpha);
    }

    private void SetOverlayText(string areaId)
    {
        if (_ovTitle != null) _ovTitle.Text = CardTitle ?? ZoneDb.AreaName(areaId);
        if (_ovSub != null) _ovSub.Text = CardSub ?? AreaSubtitle(areaId);
    }

    private void SetOverlayAlpha(float a)
    {
        _ovAlpha = Mathf.Clamp(a, 0f, 1f);
        if (_overlay == null) return;
        _overlay.Modulate = new Color(1f, 1f, 1f, _ovAlpha);
        _overlay.Visible = _ovAlpha > 0.001f;
    }

    // ═════════════════════════ saving ═════════════════════════

    public void SaveNow(string reason)
    {
        if (Hero == null || NoSave) return;          // demo mode writes nothing
        _saveReq = null;
        _autosaveT = 0;
        var sw = Stopwatch.StartNew();
        Log.Guard("save", CaptureResume);
        double capMs = sw.Elapsed.TotalMilliseconds;
        bool ok = SaveSystem.Save(Slot, Hero.D);
        Log.Info("save", $"save reason={reason} slot={Slot} ok={ok} area={Hero.D.Area} playtime={Hero.D.PlayTime:0}s capture={capMs:0.0}ms total={sw.Elapsed.TotalMilliseconds:0.0}ms");
        if (!ok) Log.Guard("hud", () => Hud?.Warn("存檔失敗。詳情請見日誌。"));
    }

    /// <summary>
    /// Puts the current area's state into the save (LevelState): a loaded save resumes in the same layout, at the same
    /// spot, with the same dead monsters, used objects, objective, ground loot and explored map; plus an open town portal
    /// and the explored maps of the town and the overworld regions.
    /// </summary>
    private void CaptureResume()
    {
        // MAP-QUEST: the explored maps of the town and the overworld regions (whose layout never changes for a character)
        // are kept in every save, also while the exact resume is off, so quest pins of explored places survive a reload
        if (!LevelState.Enabled) { Hero.D.Resume = null; Hero.D.Portal = null; KeepStableFog(Hero.D); return; }
        var d = Hero.D;
        var lv = Level != null && IsInstanceValid(Level) ? Level : null;
        var pl = Player != null && IsInstanceValid(Player) ? Player : null;
        var snap = lv != null && lv.AreaId == d.Area ? LevelState.Capture(lv, pl, _levelSeed) : null;
        if (snap != null) d.Resume = snap;
        else if (d.Resume != null && d.Resume.Area != d.Area) d.Resume = null;
        d.Portal = ReturnArea != null && _returnSeed != 0
            ? new PortalSnapshot { Area = ReturnArea, X = ReturnPos.X, Z = ReturnPos.Y, Seed = _returnSeed, State = _returnState }
            : null;
        // explored maps: the town and the regions in d.Fog (MAP-QUEST, as with the resume off); a dungeon or rift keeps
        // its own in its snapshot (d.Resume, d.Portal.State), so no map is written twice
        KeepStableFog(d);
    }

    /// <summary>The explored maps of the town and the overworld regions into the save (seed-checked when read back).</summary>
    private void KeepStableFog(SaveData d)
    {
        RememberSeen();
        d.Fog ??= new();
        d.Fog.Clear();
        foreach (var kv in _seen)
        {
            bool stable = kv.Key == ZoneDb.Town || ZoneDb.Region(kv.Key) != null;
            if (!stable || kv.Value.seen == null) continue;
            d.Fog[kv.Key] = new FogRec { Seed = kv.Value.seed, Len = kv.Value.seen.Length, Bits = LevelState.PackBits(kv.Value.seen) };
        }
    }

    /// <summary>
    /// A save of format 2 or later: wake exactly where the game was left (its area, layout and spot) with the town portal
    /// that was open. False for older saves and for places that cannot be rebuilt (named encounter arenas): those wake
    /// at the overworld entrance or in town, as before.
    /// </summary>
    private bool TakeResume(SaveData d)
    {
        _resume = null;
        if (d.Fog != null)                                  // explored maps (absent in old saves): merged when the same layout is built
            foreach (var kv in d.Fog)
                if (kv.Value?.Bits != null && kv.Value.Len > 0) _seen[kv.Key] = (kv.Value.Seed, LevelState.UnpackBits(kv.Value.Bits, kv.Value.Len));
        if (!LevelState.Enabled) return false;              // feature off: wake the old way
        var s = d.Resume;
        if (s == null || string.IsNullOrEmpty(s.Area) || s.Seed == 0) { Log.Info("save", $"no resume state (format {d.Version}): waking in {d.Area} the old way"); return false; }
        if (s.Area.StartsWith("rift~") || !IsKnownArea(s.Area)) { Log.Info("save", $"resume state for {s.Area} cannot be rebuilt: waking the old way"); return false; }
        if (!LevelState.Sanitize(s, out string why)) { Log.Warn("save", $"resume state unusable ({why}): waking in {d.Area} the old way"); return false; }
        if (why != null) Log.Warn("save", $"resume state of {s.Area}: {why}");
        _resume = s;
        var pt = d.Portal;
        if (s.Area == ZoneDb.Town && pt != null && !string.IsNullOrEmpty(pt.Area) && pt.Seed != 0 && IsKnownArea(pt.Area) && !pt.Area.StartsWith("rift~")
            && float.IsFinite(pt.X) && float.IsFinite(pt.Z))
        {
            ReturnArea = pt.Area; ReturnPos = new Vector2(pt.X, pt.Z); _returnSeed = pt.Seed;
            _returnState = pt.State != null && pt.State.Area == pt.Area && pt.State.Seed == pt.Seed ? pt.State : null;
            if (_returnState != null && !LevelState.Sanitize(_returnState, out string pwhy)) { Log.Warn("save", $"town portal state unusable ({pwhy}): the place behind it is rebuilt afresh"); _returnState = null; }
        }
        Log.Info("save", $"resume: {s.Area} seed={s.Seed} at ({s.X:0.00}, {s.Z:0.00}) dead={s.DeadMons?.Count ?? 0} spent={s.Spent?.Count ?? 0} loot={s.Loot?.Count ?? 0} portal={ReturnArea ?? "-"}");
        return true;
    }

    /// <summary>The shared stash changed (Hero.ToStash / FromStash): character and stash are written within half a second.</summary>
    public void SaveSoon(string reason) { if (Hero != null) RequestSave(reason, 400); }

    /// <summary>Asks for an autosave a little later, so bursts of progress (quest counters, several level-ups) become one write.</summary>
    private void RequestSave(string reason, int delayMs)
    {
        ulong at = Godot.Time.GetTicksMsec() + (ulong)Math.Max(0, delayMs);
        if (_saveReq == null || at < _saveReqAt) { _saveReq = reason; _saveReqAt = at; }
    }

    /// <summary>
    /// Progress worth keeping is written soon after it happens. Quest steps are saved by Quests itself (it calls
    /// SaveNow, which also cancels a request pending here), so one event never costs two writes.
    /// </summary>
    private void OnWorldEvent(string evt, string arg)
    {
        switch (evt)
        {
            case "boss": RequestSave("boss:" + arg, 250); break;
            case "dungeon": case "stronghold": case "miniboss": case "altar": case "waypoint": case "rift":
                RequestSave(evt + ":" + arg, 1500);
                break;
        }
    }

    // ═════════════════════════ hero events ═════════════════════════

    /// <summary>Called by Player when the hero gains a level.</summary>
    public void OnLevelUp(int level)
    {
        Log.Info("game", $"level up → {level}");
        Log.Guard("hud", () =>
        {
            Hud?.Flash(new Color(1f, 0.82f, 0.4f, 0.35f), 0.9f);
            Hud?.Toast($"等級提升：{level}", Palette.Gold);
        });
        World.Emit("levelup", level.ToString(CultureInfo.InvariantCulture));
        RequestSave("levelup", 300);
    }

    /// <summary>Called through Player.Died: death screen; a hardcore character is marked dead and saved at once.</summary>
    public void OnPlayerDied()
    {
        if (Hero == null) return;
        var d = Hero.D;
        Log.Info("game", $"hero died in {Level?.AreaId ?? "?"}: deaths={d.Deaths} hardcore={d.Hardcore} level={d.Level}");
        EndHitStop();
        _deathPending = true; _deathClock = 0; _deathNext = 1.1; _deathTries = 0;
        _combat = 0f;
        Log.Guard("audio", () => Audio?.SetCombat(0f));
        Log.Guard("hud", () => Hud?.Flash(new Color(0.55f, 0.03f, 0.02f, 0.6f), 1.8f));
        Cam?.Shake(0.5f, 0.5f);
        if (d.Hardcore)
        {
            d.Dead = true;                  // written before anything else can happen: no escaping by closing the game
            SaveNow("hardcore-death");
            World.Emit("death", "");
        }
    }

    /// <summary>Revives the hero at the level's checkpoint (town when there is no level). A dead hardcore hero returns to the menu instead.</summary>
    public void Respawn()
    {
        if (Hero == null) return;
        _deathPending = false;
        if (Hero.D.Hardcore && Hero.D.Dead)
        {
            Log.Info("game", "the hardcore character is gone; back to the title screen");
            ToMenu();
            return;
        }
        Log.Guard("ui", () => Ui?.Close("death"));
        EndHitStop();
        var p = Player; var lv = Level;
        if (p == null || !IsInstanceValid(p) || lv == null || !IsInstanceValid(lv))
        {
            Travel(ZoneDb.Town, null, true, null);
            return;
        }
        Vector2 at = lv.Checkpoint;
        var grid = lv.Grid ?? World.Grid;
        if (grid != null)
        {
            at = grid.NearestWalk(at);
            if (!grid.WalkAt(at)) at = grid.NearestWalk(lv.EntryPos(null));
        }
        if (p.Dead)
        {
            p.Revive(at);
            World.Emit("death", "");
            Log.Guard("hud", () => Hud?.Flash(new Color(1f, 0.75f, 0.4f, 0.3f), 0.8f));
            Log.Info("game", $"respawned in {lv.AreaId} at ({at.X:0.0}, {at.Y:0.0})");
        }
        else
        {
            p.Stop();
            p.Teleport(at);                 // not dead: used as "take me back to the checkpoint"
            Log.Info("game", $"returned to the checkpoint of {lv.AreaId}");
        }
        Cam?.Snap();
    }

    // ═════════════════════════ hit-stop ═════════════════════════

    /// <summary>A brief slow-motion punch on heavy hits: time dips to 15% for the given real time, at most once every 0.2 s.</summary>
    public void HitStop(float seconds)
    {
        if (seconds <= 0f || State != GameState.Play || PauseLocks > 0 || _baseScale != 1.0) return;
        ulong now = Godot.Time.GetTicksMsec();
        if (now - _lastHitStopMs < 200) return;
        _lastHitStopMs = now;
        seconds = Mathf.Clamp(seconds, 0.01f, 0.25f);
        _hitStopUntilMs = now + (ulong)(seconds * 1000f);
        Engine.TimeScale = 0.15;
        int token = ++_hitStopToken;
        var timer = GetTree().CreateTimer(seconds, true, false, true);     // always processes, idle frames, ignores time scale
        timer.Timeout += () => { if (token == _hitStopToken) EndHitStop(); };
    }

    private void EndHitStop()
    {
        _hitStopUntilMs = 0;
        _hitStopToken++;
        if (Engine.TimeScale != _baseScale) Engine.TimeScale = _baseScale;
    }

    // ═════════════════════════ screenshots ═════════════════════════

    public void TakeScreenshot() => TakeScreenshot(false);

    /// <summary>
    /// PrtSc / F12, as Settings.ScreenshotMode asks: 0 the frame to the clipboard (default), 1 a PNG file in
    /// Pictures\AshenAbyss, 2 both; with a toast, a shutter sound and a log line. <paramref name="prtsc"/>: the PrtSc key,
    /// which Windows may also have handled (a desktop copy to the clipboard, the Snipping Tool): the frame is taken now
    /// and put on the clipboard a moment later, so the game's picture is the one that stays there.
    /// </summary>
    public void TakeScreenshot(bool prtsc)
    {
        int mode = Math.Clamp(Cfg?.ScreenshotMode ?? 0, 0, 2);
        if (int.TryParse(ArgValue("shotmode"), out int forced)) mode = Math.Clamp(forced, 0, 2);   // test switch: never written to the settings
        bool toFile = mode != 0 || !ScreenClipboard.Supported;
        if (mode != 1 && ScreenClipboard.Supported)
        {
            try
            {
                var img = GetViewport().GetTexture()?.GetImage();
                IntPtr owner = (IntPtr)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
                if (img != null && !img.IsEmpty() && ScreenClipboard.CopyLater(img, owner, prtsc ? 350 : 0))
                {
                    if (!toFile) Log.Guard("audio", () => Snd.Ui("screenshot"));
                    Log.Info("shot", $"screenshot to the clipboard ({(prtsc ? "PrtSc" : "F12")}, mode {mode})");
                }
                else { Log.Warn("shot", "no frame for the clipboard; saving a file instead"); toFile = true; }
            }
            catch (Exception e) { Log.Error("shot", "clipboard screenshot failed", e); toFile = true; }
        }
        if (!toFile) return;
        try
        {
            string dir = ScreenshotDir();
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string path = System.IO.Path.Combine(dir, $"AshenAbyss_{stamp}.png");
            for (int n = 2; System.IO.File.Exists(path) || _shotNames.Contains(path); n++)
                path = System.IO.Path.Combine(dir, $"AshenAbyss_{stamp}_{n}.png");
            Capture(path, true);
        }
        catch (Exception e) { Log.Error("shot", "screenshot failed", e); }
    }

    private static string ScreenshotDir()
    {
        string pics = OS.GetSystemDir(OS.SystemDir.Pictures);
        if (string.IsNullOrEmpty(pics)) pics = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrEmpty(pics)) pics = OS.GetUserDataDir();
        string dir = System.IO.Path.Combine(pics.Replace('/', System.IO.Path.DirectorySeparatorChar), "AshenAbyss");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private void Capture(string path, bool announce)
    {
        try
        {
            path = System.IO.Path.GetFullPath(path);
            var img = GetViewport().GetTexture()?.GetImage();
            if (img == null || img.IsEmpty())
            {
                Log.Warn("shot", $"no frame available for {path}");
                return;
            }
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            _shotNames.Add(path);
            if (announce) Log.Guard("audio", () => Snd.Ui("screenshot"));
            // PNG compression of a 4K frame takes a noticeable fraction of a second: keep it off the game thread
            _shotTasks.RemoveAll(t => t.IsCompleted);
            _shotTasks.Add(Task.Run(() =>
            {
                try
                {
                    Error err = img.SavePng(path);
                    _shotDone.Enqueue((path, err == Error.Ok, err.ToString(), announce));
                }
                catch (Exception e) { _shotDone.Enqueue((path, false, e.Message, announce)); }
            }));
        }
        catch (Exception e) { Log.Error("shot", $"screenshot to {path} failed", e); }
    }

    private void DrainShots()
    {
        while (ScreenClipboard.Done.TryDequeue(out var c))
        {
            if (c.ok) { Log.Info("shot", $"screenshot copied to the clipboard ({c.w}x{c.h})"); Log.Guard("hud", () => Hud?.Toast("已複製畫面到剪貼簿", Palette.Gold)); }
            else { Log.Warn("shot", $"screenshot not copied to the clipboard: {c.err}"); Log.Guard("hud", () => Hud?.Warn("無法複製畫面到剪貼簿。")); }
        }
        while (_shotDone.TryDequeue(out var r))
        {
            if (r.ok)
            {
                Log.Info("shot", $"screenshot saved: {r.path}");
                if (r.announce) { string text = $"畫面已保存：{System.IO.Path.GetFileName(r.path)}（圖片\\AshenAbyss）"; Log.Guard("hud", () => Hud?.Toast(text, Palette.Gold)); }
            }
            else
            {
                Log.Error("shot", $"screenshot not saved: {r.path} ({r.err})");
                if (r.announce) Log.Guard("hud", () => Hud?.Warn("畫面保存失敗。"));
            }
        }
    }

    // ═════════════════════════ test harness ═════════════════════════

    private void SetupHarness()
    {
        _hArea = NullIfEmpty(ArgValue("area"));
        _hUi = NullIfEmpty(ArgValue("ui"));
        _shotPath = NullIfEmpty(ArgValue("shot"));
        _god = Arg("god");
        string at = ArgValue("shot-at");
        if (!string.IsNullOrEmpty(at))
        {
            foreach (var part in at.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double t) && t >= 0) _shotTimes.Add(t);
                else Log.Warn("test", $"--shot-at: cannot read '{part}'");
            }
            _shotTimes.Sort();
        }
        if (_shotPath != null && _shotTimes.Count == 0) _shotTimes.Add(6.0);
        string quit = ArgValue("quit-at");
        if (!string.IsNullOrEmpty(quit))
        {
            if (double.TryParse(quit, NumberStyles.Float, CultureInfo.InvariantCulture, out double q) && q >= 0) _quitAt = q;
            else Log.Warn("test", $"--quit-at: cannot read '{quit}'");
        }
        _dieAt = ArgSeconds("die-at"); _prtscAt = ArgSeconds("prtsc-at"); _f12At = ArgSeconds("f12-at");
        _blurAt = ArgSeconds("blur-at"); _focusAt = ArgSeconds("focus-at");
        _hGear = Arg("gear");
        if (int.TryParse(ArgValue("level"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hl)) _hLevel = hl;
        if (int.TryParse(ArgValue("tier"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ht)) _hTier = ht;
        if (int.TryParse(ArgValue("paragon"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hp)) _hParagon = hp;
        string speed = ArgValue("speed");
        if (!string.IsNullOrEmpty(speed))
        {
            if (double.TryParse(speed, NumberStyles.Float, CultureInfo.InvariantCulture, out double sp) && sp > 0.05 && sp <= 20)
            {
                _baseScale = sp; Engine.TimeScale = sp;
                Log.Info("test", $"--speed: time scale {sp:0.##} (hit-stop disabled)");
            }
            else Log.Warn("test", $"--speed: cannot read '{speed}'");
        }
        if (Arg("ui-tour"))
        {
            _tour = new List<(string, string)>
            {
                ("inventory", null), ("skills", null), ("paragon", null), ("map", null), ("map", "travel"), ("quests", null),
                ("vendor", "merchant"), ("vendor", "smith"), ("vendor", "occultist"), ("vendor", "jeweler"), ("vendor", "stash"),
                ("dialogue", "intro"), ("tier", null), ("rift", null), ("pause", null), ("settings", null), ("death", null),
            };
            string only = ArgValue("ui-tour");
            if (!string.IsNullOrEmpty(only))
            {
                var keep = only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                _tour.RemoveAll(t => Array.IndexOf(keep, t.id) < 0 && Array.IndexOf(keep, t.id + ":" + t.arg) < 0);
            }
            if (string.IsNullOrEmpty(ArgValue("shot-at"))) _shotTimes.Clear();      // the tour takes its own pictures
            Log.Info("test", $"--ui-tour: {_tour.Count} panels, screenshots → {(_shotPath ?? "Pictures\\AshenAbyss")}");
        }
        if (_shotTimes.Count > 0 || _quitAt >= 0)
            Log.Info("test", $"harness: shots at [{string.Join(", ", _shotTimes.Select(t => t.ToString("0.##", CultureInfo.InvariantCulture)))}] s → {_shotPath ?? "Pictures\\AshenAbyss"}; " +
                $"quit at {(_quitAt >= 0 ? _quitAt.ToString("0.##", CultureInfo.InvariantCulture) + " s" : _shotTimes.Count > 0 ? "1.5 s after the last shot" : "never")} (seconds after the menu or the first level is on screen)");
    }

    private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private double ArgSeconds(string name)
    {
        string v = ArgValue(name);
        if (string.IsNullOrEmpty(v)) return -1;
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double t) && t >= 0) return t;
        Log.Warn("test", $"--{name}: cannot read '{v}'");
        return -1;
    }

    /// <summary>
    /// Suites: what the next NewGame / LoadGame should look like (level, gear, world tier, paragon level, god mode and
    /// the area to start in), replacing whatever the command line asked for. Negative numbers / null leave a value alone.
    /// </summary>
    public void HarnessNext(string area, int level, bool gear, int tier = -1, bool god = false, int paragon = -1)
    {
        _hArea = NullIfEmpty(area); _hAreaUsed = false;
        _hLevel = level; _hGear = gear; _hTier = tier; _god = god; _hParagon = paragon;
    }

    /// <summary>Travel for the harness: no quest gating, no town-portal memory.</summary>
    public bool HarnessTravel(string areaId) => Travel(areaId, null, true, null);

    private float _hFov;
    /// <summary>Test harness: a narrower lens for close-up screenshots of characters (0 = the game's own lens).</summary>
    public void HarnessFov(float deg)
    {
        _hFov = deg;
        if (Cam?.Cam != null) Cam.Cam.Fov = deg > 0f ? deg : CameraRig.FovDeg;
    }

    /// <summary>Screenshot to an explicit file (suites and the UI tour); no toast, no sound.</summary>
    public void HarnessShot(string path) { if (!string.IsNullOrEmpty(path)) Capture(path, false); }

    private void TickTour(double real)
    {
        if (_tour == null || State != GameState.Play || Ui == null || _load != LoadStep.Idle) return;
        _tourT += real;
        if (_tourIndex < 0) { if (_tourT < 2.0) return; _tourIndex = 0; _tourPhase = 0; _tourT = 0; }
        if (_tourIndex >= _tour.Count)
        {
            if (_tourPhase == 0) { _tourPhase = 1; Log.Info("test", "ui tour finished"); if (_quitAt < 0) _quitAt = _hClock + 1.5; }
            return;
        }
        var (id, arg) = _tour[_tourIndex];
        string name = arg == null ? id : id + "_" + arg;
        switch (_tourPhase)
        {
            case 0:
                Log.Guard("ui", () => Ui.CloseAll());
                PauseLocks = Math.Max(0, PauseLocks);
                Log.Info("test", $"ui tour: opening {name}");
                Log.Guard("ui", () =>
                {
                    object payload = arg;
                    if (id == "dialogue") payload = DialogueDb.Get(arg ?? "intro");
                    Ui.Open(id, payload);
                });
                _tourPhase = 1; _tourT = 0;
                break;
            case 1:
                if (_tourT < 1.3) break;
                bool open = false;
                try { open = Ui.IsOpen(id); } catch (Exception e) { Log.Error("ui", $"IsOpen({id}) failed", e); }
                if (!open) Log.Warn("test", $"ui tour: panel '{name}' is not open after Ui.Open");
                string basePath = _shotPath ?? System.IO.Path.Combine(ScreenshotDir(), "ui");
                if (basePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) basePath = basePath[..^4];
                Capture($"{basePath}_{name}.png", false);
                _tourPhase = 2; _tourT = 0;
                break;
            case 2:
                if (_tourT < 0.4) break;
                Log.Guard("ui", () => Ui.CloseAll());
                _tourIndex++; _tourPhase = 0; _tourT = 0;
                break;
        }
    }

    /// <summary>--die-at: a blow no hero survives (unless --god), to test the death panel, respawn and hardcore marking.</summary>
    private void HarnessKill()
    {
        var p = Player;
        if (p == null || !IsInstanceValid(p) || p.Dead) { Log.Warn("test", "--die-at: no living hero"); return; }
        Log.Info("test", $"--die-at: lethal blow at {_hClock:0.0} s (life {p.Life:0}/{p.MaxLife:0})");
        p.IFrames = 0f;
        p.Barrier = 0f;
        p.TakeHit(new Hit { Source = null, Amount = p.MaxLife * 50f, El = Element.Shadow });
        Log.Info("test", $"--die-at: hero dead={p.Dead} hardcore={Hero?.D.Hardcore} savedDead={Hero?.D.Dead}");
    }

    private void StartHarnessClock(string what)
    {
        _hClock = 0;
        if (_shotTimes.Count > 0 || _quitAt >= 0) Log.Info("test", $"harness clock started ({what} on screen)");
    }

    private void TickHarness(double real, ulong now)
    {
        if (_quitting) return;
        if (_hClock < 0)
        {
            // never hang an automated run that asked to quit but could not get anything on screen
            if (_quitAt >= 0 && (now - _bootMs) / 1000.0 > _quitAt + 180.0) { Log.Error("test", "nothing reached the screen in time; quitting"); Quit(); }
            return;
        }
        _hClock += real;
        if (_hFov > 0f && Cam?.Cam != null && !Mathf.IsEqualApprox(Cam.Cam.Fov, _hFov)) Cam.Cam.Fov = _hFov;
        if (_tour != null) Log.Guard("test", () => TickTour(real));
        if (_dieAt >= 0 && _hClock >= _dieAt) { _dieAt = -1; Log.Guard("test", HarnessKill); }
        if (_prtscAt >= 0 && _hClock >= _prtscAt)
        {
            _prtscAt = -1;
            Log.Info("test", $"--prtsc-at: PrtSc through {WinKeys.SimulatePrtSc()}");
        }
        if (_blurAt >= 0 && _hClock >= _blurAt) { _blurAt = -1; Log.Info("test", "--blur-at: simulated focus loss (Alt-Tab)"); SetAppFocus(false, "test"); }
        if (_focusAt >= 0 && _hClock >= _focusAt) { _focusAt = -1; Log.Info("test", $"--focus-at: simulated focus return; paused={Paused} pauseMenu={Ui?.IsOpen("pause")}"); SetAppFocus(true, "test"); }
        if (_f12At >= 0 && _hClock >= _f12At)
        {
            _f12At = -1;
            Log.Info("test", "--f12-at: synthetic F12 key event");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.F12, PhysicalKeycode = Key.F12, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.F12, PhysicalKeycode = Key.F12, Pressed = false });
        }
        while (_shotIndex < _shotTimes.Count && _hClock >= _shotTimes[_shotIndex])
        {
            string path = HarnessShotPath(_shotIndex);
            Log.Info("test", $"harness screenshot {_shotIndex + 1}/{_shotTimes.Count} at {_hClock:0.00} s → {path ?? "(default folder)"}");
            if (path != null) Capture(path, false); else TakeScreenshot();
            _shotIndex++;
            if (_shotIndex >= _shotTimes.Count && _quitAt < 0) _quitAt = _hClock + 1.5;
        }
        if (_quitAt >= 0 && _hClock >= _quitAt)      // shots due by now were taken just above; Quit waits for their files
        {
            Log.Info("test", $"harness quit at {_hClock:0.00} s");
            Quit();
        }
    }

    private string HarnessShotPath(int index)
    {
        if (_shotPath == null) return null;
        string p = _shotPath;
        if (!p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) p += ".png";
        if (_shotTimes.Count <= 1) return p;
        string dir = System.IO.Path.GetDirectoryName(p) ?? "";
        return System.IO.Path.Combine(dir, $"{System.IO.Path.GetFileNameWithoutExtension(p)}_{index + 1}.png");
    }

    private void ApplyHarnessToSave(SaveData d)
    {
        if (_hTier >= 0)
        {
            d.Tier = Math.Clamp(_hTier, 0, 3);
            d.MaxTier = Math.Max(d.MaxTier, d.Tier);
            Log.Info("test", $"--tier: world tier {d.Tier} ({ZoneDb.TierNames[d.Tier]})");
        }
        if (int.TryParse(ArgValue("seed"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed) && seed != 0)
        {
            d.Seed = seed;                  // same seed → same overworlds, for runs that are compared with each other
            Log.Info("test", $"--seed: world seed {seed}");
        }
        if (_hLevel > 0)
        {
            int lv = Math.Clamp(_hLevel, 1, Balance.LevelCap);
            if (lv != d.Level) { d.Level = lv; d.Xp = 0; }
            Log.Info("test", $"--level: hero level {d.Level}");
        }
        if (_hParagon >= 0 && d.Level >= Balance.LevelCap)
        {
            d.Paragon = Math.Clamp(_hParagon, 0, Balance.ParagonCap);
            Log.Info("test", $"--paragon: paragon level {d.Paragon}");
        }
    }

    private void ApplyHarnessToHero(Hero h)
    {
        if (_hLevel > 0) Log.Guard("test", () => AutoBuild(h));
        if (_hGear) Log.Guard("test", () => AutoGear(h));
        if (_hParagon > 0 && h.D.Level >= Balance.LevelCap)
            Log.Guard("test", () => { int n = h.AutoAllocateParagon(); Log.Info("test", $"--paragon: {n} board points allocated automatically"); });
        if (_god) Log.Guard("test", () => { EnsureGod(h); Log.Info("test", "--god: the hero cannot die"); });
    }

    private static readonly BuffDef GodBuff = new() { Id = "test_god", Name = "不滅燼火", Deathless = true };      // see Player.PreventDeath: lethal blows are counted, not suffered

    public static void EnsureGod(Hero h)
    {
        foreach (var b in h.Buffs) if (b.Def.Id == GodBuff.Id) { b.Left = 1e9f; return; }
        h.AddBuff(GodBuff, 1e9f);
    }

    /// <summary>Equips a level-appropriate rare item in every slot (--gear).</summary>
    public static void AutoGear(Hero h)
    {
        var d = h.D;
        int power = Math.Clamp(d.Level * 10 + d.Tier * 30, 10, ItemDb.MaxPower);
        ItemKind[] kinds = { ItemKind.Helm, ItemKind.Chest, ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet, ItemKind.Ring, ItemKind.Ring };
        EquipSlot[] slots = { EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Gloves, EquipSlot.Pants, EquipSlot.Boots, EquipSlot.Amulet, EquipSlot.Ring1, EquipSlot.Ring2 };
        var rng = new Random(d.Seed ^ 0x5EED);
        // the hands (SYSTEMS, modular gear): one of the class's styles — two-hand, dual-wield or one-hand + off-hand
        // --hands=2h|dual|off|1h picks the style (when the class has it); otherwise one at random
        var styles = ItemDb.HandStyles(d.Class);
        string want = (I?.ArgValue("hands") ?? "").Trim().ToLowerInvariant();
        string style = styles.Contains(want) ? want : styles[rng.Next(styles.Count)];
        if (want.Length > 0 && want != style) Log.Info("test", $"--hands={want}: {Names.ClassKey(d.Class)} cannot hold that; using {style}");
        var (mh, oh) = ItemDb.RollHands(d.Class, power, style, () => Rarity.Rare, rng);
        foreach (var w in new[] { mh, oh }) if (w != null) w.ReqLevel = Math.Min(w.ReqLevel, d.Level);
        d.Equip[(int)EquipSlot.MainHand] = mh; d.Equip[(int)EquipSlot.OffHand] = oh;
        for (int i = 0; i < kinds.Length; i++)
        {
            var it = ItemDb.Generate(power, Rarity.Rare, d.Class, kinds[i], rng);
            it.ReqLevel = Math.Min(it.ReqLevel, d.Level);
            d.Equip[(int)slots[i]] = it;
        }
        h.Recalc();
        Log.Info("test", $"--gear: rare items of power {power} equipped (main {d.Equip[(int)EquipSlot.MainHand]?.Base}, off {d.Equip[(int)EquipSlot.OffHand]?.Base ?? "-"}); life={h.MaxLife:0} armor={h.Armor:0} weapon={h.WeaponMin:0}-{h.WeaponMax:0}");
    }

    /// <summary>
    /// Spends every free skill point the way a sensible player would: one skill of each category on the bar, ranks spread
    /// evenly over them, enhancements and one specialisation branch, passives, a key passive and a class spec.
    /// Used by --level and by the TestBot as it levels.
    /// </summary>
    public static void AutoBuild(Hero h)
    {
        var d = h.D;
        int lvl = d.Level;
        var unlocked = SkillDb.Of(h.Class).Where(s => s.UnlockLevel <= lvl).OrderBy(s => s.UnlockLevel).ToList();
        var passives = SkillDb.PassivesOf(h.Class).Where(p => p.UnlockLevel <= lvl).OrderBy(p => p.UnlockLevel).ToList();
        var picks = new List<SkillDef>(6);
        void Pick(SkillDef s) { if (s != null && s.Class == h.Class && s.UnlockLevel <= lvl && !picks.Contains(s) && picks.Count < 6) picks.Add(s); }

        foreach (var id in d.Bar) Pick(SkillDb.Get(id));                                  // what is already on the bar stays
        if (!picks.Any(s => s.Cat == SkillCat.Basic))
            Pick(unlocked.FirstOrDefault(s => s.Cat == SkillCat.Basic && h.BaseRank(s.Id) > 0) ?? unlocked.FirstOrDefault(s => s.Cat == SkillCat.Basic));
        foreach (var cat in new[] { SkillCat.Core, SkillCat.Defensive, SkillCat.Mobility, SkillCat.Mastery, SkillCat.Ultimate })
            if (!picks.Any(s => s.Cat == cat))
                Pick(unlocked.Where(s => s.Cat == cat).OrderBy(s => s.Kind == SkillKind.Channel ? 1 : 0).ThenBy(s => s.UnlockLevel).FirstOrDefault());
        foreach (var extra in unlocked.Where(x => x.Cat != SkillCat.Basic)) Pick(extra);  // low levels: fill with whatever else is unlocked
        foreach (var extra in unlocked) Pick(extra);

        static int Weight(SkillDef s) => s.Cat switch { SkillCat.Core => 0, SkillCat.Basic => 1, SkillCat.Ultimate => 2, SkillCat.Mastery => 3, _ => 4 };
        var order = picks.OrderBy(Weight).ToList();
        void Ranks(int upTo) { foreach (var s in order) if (h.BaseRank(s.Id) > 0 && h.BaseRank(s.Id) < upTo) h.Learn(s.Id); }
        void Ups(bool branch)
        {
            foreach (var s in order)
            {
                var u = branch ? s.A : s.Enh;
                if (u == null || s.Enh == null || h.BaseRank(s.Id) <= 0 || h.HasUp(u.Id)) continue;
                if (branch && s.B != null && h.HasUp(s.B.Id)) continue;                  // the player already chose the other branch
                h.TakeUp(s.Id, u.Id);
            }
        }
        void Passives(int upTo) { foreach (var p in passives) if (!p.Key && h.BaseRank(p.Id) < Math.Min(upTo, p.MaxRank)) h.Learn(p.Id); }

        foreach (var s in order) if (h.BaseRank(s.Id) == 0) h.Learn(s.Id);
        Ups(false);
        Ranks(2);
        Ups(true);
        Passives(1);
        Ranks(3);
        if (!passives.Any(p => p.Key && h.BaseRank(p.Id) > 0))
        {
            var key = passives.FirstOrDefault(p => p.Key);
            if (key != null) h.Learn(key.Id);
        }
        Passives(2);
        Ranks(4);
        Passives(3);
        Ranks(5);

        // the action bar: basic on LMB, core on RMB, then defensive, mobility, mastery, ultimate on 1-4
        var known = picks.Where(s => h.BaseRank(s.Id) > 0).ToList();
        var bar = new string[6];
        var basic = known.FirstOrDefault(s => s.Cat == SkillCat.Basic);
        var core = known.Where(s => s.Cat == SkillCat.Core).OrderBy(s => s.Kind == SkillKind.Channel ? 1 : 0).FirstOrDefault();
        bar[0] = basic?.Id;
        bar[1] = core?.Id;
        var rest = known.Where(s => s != basic && s != core)
            .OrderBy(s => s.Cat switch { SkillCat.Defensive => 0, SkillCat.Mobility => 1, SkillCat.Mastery => 2, SkillCat.Ultimate => 3, _ => 4 }).ToList();
        int ri = 0;
        for (int i = 2; i < 6 && ri < rest.Count; i++) bar[i] = rest[ri++].Id;
        if (bar[1] == null && ri < rest.Count) bar[1] = rest[ri++].Id;
        if (bar[0] == null && ri < rest.Count) bar[0] = rest[ri].Id;
        d.Bar = bar;

        if (lvl >= SkillDb.SpecLevel && string.IsNullOrEmpty(d.Spec))
        {
            var spec = SkillDb.SpecsOf(h.Class).FirstOrDefault();
            if (spec != null) h.SetSpec(spec.Id);
        }
        h.Recalc();
        Log.Info("test", $"auto build lv{lvl} {Names.ClassKey(h.Class)}: bar=[{string.Join(", ", bar.Select(b => b ?? "-"))}] spec={d.Spec ?? "-"} points left={h.SkillPoints}");
    }

    private void OpenHarnessUi()
    {
        if (Ui == null || _hUi == null) return;
        string id = _hUi, arg = null;
        int colon = id.IndexOf(':');
        if (colon > 0) { arg = id[(colon + 1)..]; id = id[..colon]; }
        object payload = arg;
        if (id == "dialogue") payload = DialogueDb.Get(arg ?? "intro");
        else if (id == "vendor" && arg == null) payload = "merchant";
        Log.Info("test", $"--ui: opening '{id}'{(arg != null ? $" with '{arg}'" : "")}");
        Ui.Open(id, payload);
    }
}
