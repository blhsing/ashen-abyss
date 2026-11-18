using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AshenAbyss;

/// <summary>Which picture the mouse cursor shows.</summary>
public enum CursorKind
{
    Default,        // in play, nothing special under the pointer: the gauntlet pointer (a click walks there)
    Attack,         // a hostile: the forge-hot blade
    Talk,           // a friendly character: the speech plaque
    PickUp,         // loot on the ground: the reaching gauntlet (also Godot's drag shapes)
    Interact,       // chests, shrines, doors, portals, waypoints, totems…: the gauntlet with the cog
    Forbidden,      // ground or a target that cannot be reached: the cold gauntlet with the broken red ring
    Ui,             // over panels, menus and the HUD: the slim iron pointer
    UiHot,          // Godot's pointing-hand shape (clickable menu entries): the same pointer, lit
    Caret,          // text fields
    Busy,           // loading screens
}

/// <summary>
/// The contextual mouse cursor. The pictures are raster art rendered from 3D models (Y:\tools\cursor) in four sizes;
/// this node loads them, picks the size for the screen, dresses Godot's own cursor shapes (pointing hand, I-beam, drag,
/// forbidden, busy) and every frame decides what the plain arrow should be: over the interface the slim pointer, in
/// play what a click would do (<see cref="Player.HoverKind"/>, decided by the hero's own picking; nothing is picked
/// twice). A change costs one <c>Input.SetCustomMouseCursor</c> call; a frame without a change costs a few comparisons.
///
/// Size: 32 px × max(window scale, display scaling) × the interface text size, snapped to the nearest size in the set
/// (32 / 40 / 48 / 64 / 96), so a 4K screen gets the 64 px cursor instead of a tiny 32 px one.
/// Setting 「滑鼠游標」 (<see cref="Settings.SystemCursor"/>) hands every shape back to the operating system.
/// Switches: --cursor-size=&lt;px&gt; forces a size, --cursor-display=&lt;factor&gt; stands in for the display scaling,
/// --cursor-log writes a "cursor" line per change, --cursor-test runs <see cref="CursorTest"/>.
/// </summary>
public partial class Cursors : Node
{
    public static Cursors I { get; private set; }

    private const string Dir = "res://assets/ui/cursor/";
    private const int KindCount = 10;
    private static readonly string[] FileOf = { "default", "attack", "talk", "pickup", "interact", "forbid", "ui", "ui_hot", "caret", "busy" };

    /// <summary>Godot's own shapes (what Control.MouseDefaultCursorShape asks for) and the picture each one gets.</summary>
    private static readonly (Input.CursorShape shape, CursorKind kind)[] Slots =
    {
        (Input.CursorShape.PointingHand, CursorKind.UiHot), (Input.CursorShape.Ibeam, CursorKind.Caret),
        (Input.CursorShape.Drag, CursorKind.PickUp), (Input.CursorShape.CanDrop, CursorKind.PickUp),
        (Input.CursorShape.Forbidden, CursorKind.Forbidden),
        (Input.CursorShape.Wait, CursorKind.Busy), (Input.CursorShape.Busy, CursorKind.Busy),
    };

    private sealed class Art { public Image Img; public Vector2 Hot; }

    private int[] _sizes = Array.Empty<int>();
    private readonly Dictionary<(int kind, int size), Vector2> _hot = new();
    private readonly Dictionary<int, Art[]> _sets = new();
    private Art[] _set;
    private int _size, _forceSize;
    private bool _ready, _system, _dressed, _log;
    private CursorKind _kind = CursorKind.Ui;
    private bool _shown;                       // the arrow slot carries _kind at _size
    private CursorKind? _forced;
    private float _fitT, _forbidT, _forceDisplay;
    private string _fitWhy = "";
    private Input.CursorShape _shapeSeen = Input.CursorShape.Arrow;

    /// <summary>The picture the arrow carries now.</summary>
    public static CursorKind Current => I != null ? I._kind : CursorKind.Default;
    /// <summary>The size in use (pixels); 0 while the art is not loaded.</summary>
    public static int Size => I?._size ?? 0;
    /// <summary>True when the game draws its own cursors (art loaded, the system-cursor setting off).</summary>
    public static bool Active => I != null && I._ready && !I._system;
    /// <summary>The hotspot of a picture at the size in use (pixels from its top-left corner).</summary>
    public static Vector2 HotspotOf(CursorKind kind) => I?._set?[(int)kind]?.Hot ?? Vector2.Zero;

    /// <summary>Shows this picture on the arrow right now. The frame's own context decision replaces it next frame
    /// unless it is pinned with <see cref="Force"/>.</summary>
    public static void Set(CursorKind kind) => I?.Show(kind, "set");
    /// <summary>Pins a picture (a targeting mode, an invalid placement…) until <see cref="Release"/>.</summary>
    public static void Force(CursorKind kind) { if (I != null) I._forced = kind; }
    public static void Release() { if (I != null) I._forced = null; }

    // ───────────────────────── life cycle ─────────────────────────

    public override void _EnterTree()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
        UiTheme.TextScaleChanged -= OnTextScale;
        UiTheme.TextScaleChanged += OnTextScale;
    }

    public override void _ExitTree()
    {
        UiTheme.TextScaleChanged -= OnTextScale;
        if (I == this) I = null;
    }

    public override void _Ready()
    {
        try
        {
            var g = Game.I;
            _log = g != null && (g.Arg("cursor-log") || g.Arg("cursor-test")) || Log.Verbose;
            if (g != null && int.TryParse(g.ArgValue("cursor-size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fs) && fs > 0) _forceSize = fs;
            if (g != null && float.TryParse(g.ArgValue("cursor-display"), NumberStyles.Float, CultureInfo.InvariantCulture, out float fd) && fd > 0f) _forceDisplay = Mathf.Clamp(fd, 0.5f, 4f);
            LoadManifest();
            var win = GetTree()?.Root;
            if (win != null) win.SizeChanged += () => { if (IsInstanceValid(this)) Fit("window resized"); };
            if (_ready)
            {
                _system = g?.Cfg != null && g.Cfg.SystemCursor;
                Fit("start");
                if (_ready && _system) Log.Info("cursor", "the system cursor is used (setting 滑鼠游標 = 系統)");
            }
            if (g != null && g.Arg("cursor-test")) AddChild(new CursorTest { Name = "CursorTest" });
        }
        catch (Exception e) { Log.Error("cursor", "cursor setup failed; the system cursor stays", e); _ready = false; }
    }

    private void OnTextScale(float oldScale) => Fit("text scale");

    // ───────────────────────── art ─────────────────────────

    private void LoadManifest()
    {
        string path = Dir + "cursors.json";
        if (!FileAccess.FileExists(path)) { Log.Warn("cursor", "no cursor art (res://assets/ui/cursor/cursors.json): the system cursor is used"); return; }
        var parsed = Json.ParseString(FileAccess.GetFileAsString(path));
        if (parsed.VariantType != Variant.Type.Dictionary) { Log.Warn("cursor", "cursors.json is not readable: the system cursor is used"); return; }
        var root = parsed.AsGodotDictionary();
        var sizes = new List<int>();
        foreach (var v in root["sizes"].AsGodotArray()) sizes.Add(v.AsInt32());
        sizes.Sort();
        var kinds = root["kinds"].AsGodotDictionary();
        for (int k = 0; k < KindCount; k++)
        {
            if (!kinds.ContainsKey(FileOf[k])) { Log.Warn("cursor", $"cursors.json has no entry '{FileOf[k]}'"); continue; }
            var hot = kinds[FileOf[k]].AsGodotDictionary()["hot"].AsGodotDictionary();
            foreach (int s in sizes)
            {
                string key = s.ToString(CultureInfo.InvariantCulture);
                if (!hot.ContainsKey(key)) continue;
                var xy = hot[key].AsGodotArray();
                _hot[(k, s)] = new Vector2(xy[0].AsSingle(), xy[1].AsSingle());
            }
        }
        _sizes = sizes.ToArray();
        _ready = _sizes.Length > 0 && _hot.Count > 0;
        if (_ready) Log.Info("cursor", $"cursor art: {kinds.Count} pictures, sizes {string.Join("/", _sizes)} px");
    }

    private Art[] SetFor(int size)
    {
        if (_sets.TryGetValue(size, out var have)) return have;
        var set = new Art[KindCount];
        int missing = 0;
        for (int k = 0; k < KindCount; k++)
        {
            var img = LoadImage($"{Dir}{FileOf[k]}_{size}.png");
            if (img == null || !_hot.TryGetValue((k, size), out var hot)) { missing++; continue; }
            // the hotspot has to lie inside the picture or the engine refuses the cursor
            hot = new Vector2(Mathf.Clamp(hot.X, 0, img.GetWidth() - 1), Mathf.Clamp(hot.Y, 0, img.GetHeight() - 1));
            set[k] = new Art { Img = img, Hot = hot };
        }
        if (missing > 0) Log.Warn("cursor", $"{missing} of {KindCount} cursor pictures are missing at {size} px");
        _sets[size] = set;
        return set;
    }

    private readonly Dictionary<int, int> _usable = new();

    /// <summary>The size to use when <paramref name="size"/> is wanted: itself when all its pictures loaded, otherwise the
    /// nearest size that is complete (logged once), 0 when none is.</summary>
    private int Usable(int size)
    {
        if (_usable.TryGetValue(size, out int have)) return have;
        int pick = 0;
        if (Complete(SetFor(size))) pick = size;
        else
        {
            float bd = float.MaxValue;
            foreach (int s in _sizes)
            {
                if (s == size || !Complete(SetFor(s))) continue;
                float d = Mathf.Abs(Mathf.Log((float)s / size));
                if (d < bd) { bd = d; pick = s; }
            }
            if (pick != 0) Log.Warn("cursor", $"the {size} px cursors are incomplete: the {pick} px set is used instead");
        }
        _usable[size] = pick;
        return pick;
    }

    private static bool Complete(Art[] set)
    {
        if (set == null) return false;
        foreach (var a in set) if (a == null) return false;
        return true;
    }

    private static Image LoadImage(string path)
    {
        try
        {
            if (ResourceLoader.Exists(path))
            {
                var im = ResourceLoader.Load<Texture2D>(path)?.GetImage();
                if (im != null && !im.IsEmpty())
                {
                    if (im.IsCompressed()) im.Decompress();
                    if (im.GetFormat() != Image.Format.Rgba8) im.Convert(Image.Format.Rgba8);
                    return im;
                }
            }
        }
        catch (Exception e) { Log.Warn("cursor", $"{path}: import unavailable ({e.Message})"); }
        try
        {
            // not imported yet (a development run straight after the art was regenerated): read the PNG itself
            if (FileAccess.FileExists(path))
            {
                var im = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
                if (im != null && !im.IsEmpty()) { if (im.GetFormat() != Image.Format.Rgba8) im.Convert(Image.Format.Rgba8); return im; }
            }
        }
        catch (Exception e) { Log.Warn("cursor", $"{path}: direct load failed ({e.Message})"); }
        return null;
    }

    // ───────────────────────── size ─────────────────────────

    /// <summary>The cursor size (px) for a window, a display scaling factor and a text scale: 32 px at 1080p / 100 % /
    /// standard text, snapped to the nearest size of the set by ratio (a tie goes to the larger one).</summary>
    public static int SizeFor(Vector2 window, float displayScale, float textScale, IReadOnlyList<int> sizes, out float target)
    {
        float win = Mathf.Min(window.X / 1920f, window.Y / 1080f);
        target = 32f * Mathf.Max(win, displayScale) * Mathf.Max(0.5f, textScale);
        return Nearest(target, sizes);
    }

    private static int Nearest(float target, IReadOnlyList<int> sizes)
    {
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < sizes.Count; i++)
        {
            float d = Mathf.Abs(Mathf.Log(sizes[i] / Mathf.Max(1f, target)));
            if (d < bd - 1e-4f || Mathf.Abs(d - bd) <= 1e-4f && sizes[i] > best) { bd = d; best = sizes[i]; }
        }
        return best;
    }

    /// <summary>The display's scaling factor (1 = 100 %, 1.25 = 125 %…); --cursor-display=&lt;factor&gt; overrides it for tests.</summary>
    public static float DisplayScale()
    {
        if (I != null && I._forceDisplay > 0f) return I._forceDisplay;
        try
        {
            int dpi = DisplayServer.ScreenGetDpi(DisplayServer.WindowGetCurrentScreen());
            return dpi > 0 ? Mathf.Clamp(dpi / 96f, 1f, 4f) : 1f;
        }
        catch { return 1f; }
    }

    private void Fit(string why)
    {
        if (!_ready) return;
        var win = GetTree()?.Root;
        if (win == null) return;
        Vector2 ws = win.Size;
        float ds = DisplayScale(), ts = UiTheme.TextScale, target;
        int size;
        if (_forceSize > 0) { target = _forceSize; size = Nearest(_forceSize, _sizes); }
        else size = SizeFor(ws, ds, ts, _sizes, out target);
        string desc = $"window {ws.X:0}x{ws.Y:0} display x{ds:0.00} text x{ts:0.00}{(_forceSize > 0 ? " forced" : "")} -> wants {target:0.#} px";
        size = Usable(size);
        if (size == 0)
        {
            // never a half set: without one complete size the feature is off and the system cursor stays
            Log.Warn("cursor", "no size of the cursor set has every picture: the system cursor is used");
            _ready = false; _set = null; _system = true;
            Dress();
            return;
        }
        if (size == _size && _set != null) { _fitWhy = desc; return; }
        _size = size;
        _set = SetFor(size);
        _fitWhy = desc;
        Log.Info("cursor", $"size {size} px ({why}: {desc})");
        _dressed = false; _shown = false;
    }

    // ───────────────────────── applying ─────────────────────────

    /// <summary>Gives Godot's own shapes their pictures (or hands every shape back to the system).</summary>
    private void Dress()
    {
        _dressed = true;
        try
        {
            if (_system)
            {
                Input.SetCustomMouseCursor(null, Input.CursorShape.Arrow);
                foreach (var (shape, _) in Slots) Input.SetCustomMouseCursor(null, shape);
                _shown = false;
                return;
            }
            foreach (var (shape, kind) in Slots)
            {
                var a = _set?[(int)kind];
                if (a != null) Input.SetCustomMouseCursor(a.Img, shape, a.Hot);
            }
        }
        catch (Exception e) { Log.Error("cursor", "dressing the cursor shapes failed", e); }
    }

    private void Show(CursorKind kind, string why)
    {
        if (!_ready || _system) { _kind = kind; return; }
        if (_shown && kind == _kind) return;
        var a = _set?[(int)kind] ?? _set?[(int)CursorKind.Default];
        if (a == null) return;
        try { Input.SetCustomMouseCursor(a.Img, Input.CursorShape.Arrow, a.Hot); }
        catch (Exception e) { Log.Error("cursor", "setting the cursor failed", e); _ready = false; return; }
        if (_log) Log.Info("cursor", $"{(_shown ? _kind.ToString() : "-")} -> {kind} ({Detail(why)}) size={_size} hotspot={a.Hot.X:0},{a.Hot.Y:0}");
        _kind = kind; _shown = true;
    }

    // ───────────────────────── frame ─────────────────────────

    public override void _Process(double delta)
    {
        if (!_ready) return;
        try
        {
            var g = Game.I;
            // the setting (applies at once, both ways)
            bool sys = g?.Cfg != null && g.Cfg.SystemCursor;
            if (sys != _system)
            {
                _system = sys; _dressed = false; _shown = false;
                Log.Info("cursor", sys ? "setting: the system cursor is used" : "setting: the game's cursors are used");
            }
            // a window dragged to another display changes the scaling without changing its size
            _fitT -= (float)delta;
            if (_fitT <= 0f) { _fitT = 1.5f; Fit("display"); }
            if (!_dressed) Dress();
            if (_system) return;
            var kind = Context(g, (float)delta, out string why);
            if (!_shown || kind != _kind) Show(kind, why);
            if (_log)
            {
                // the shape a control asked for (the engine switches to that slot's picture by itself)
                var shape = Input.GetCurrentCursorShape();
                if (shape != _shapeSeen)
                {
                    _shapeSeen = shape;
                    string pic = shape == Input.CursorShape.Arrow ? _kind.ToString() : "system";
                    foreach (var (s, k) in Slots) if (s == shape) pic = k.ToString();
                    Log.Info("cursor", $"shape {shape} (picture {pic})");
                }
            }
        }
        catch (Exception e) { UiSafe.Report("cursor", e); }
    }

    /// <summary>What the arrow should be this frame. <paramref name="why"/> is a constant (no allocation per frame).</summary>
    private CursorKind Context(Game g, float dt, out string why)
    {
        if (_forced is { } f) { why = "forced"; return f; }
        if (g == null) { why = "no game"; return CursorKind.Ui; }
        if (g.State == Game.GameState.Loading || g.State == Game.GameState.Boot) { why = "loading"; return CursorKind.Busy; }
        if (g.State != Game.GameState.Play) { why = "menu"; return CursorKind.Ui; }
        var ui = g.Ui;
        if (ui != null)
        {
            if (ui.AnyOpen) { why = "panel open"; return CursorKind.Ui; }
            if (ui.MouseOverUi)
            {
                // a loot label or an object's prompt belongs to the thing it names: the click walks there and uses it
                var lab = g.Hud?.PointerObj;
                if (lab != null && IsInstanceValid(lab) && GetViewport().GuiGetHoveredControl() == null) { why = "label"; return KindOf(lab); }
                why = "over interface"; return CursorKind.Ui;
            }
        }
        var p = g.Player;
        if (p == null || !IsInstanceValid(p)) { why = "no hero"; return CursorKind.Default; }
        if (p.Dead) { why = "hero dead"; return CursorKind.Default; }
        if (g.Paused || !p.InputEnabled) { why = "paused"; return CursorKind.Default; }     // the hover is not updated while paused
        switch (p.HoverKind)
        {
            case Player.HoverAct.Attack: _forbidT = 0; why = "hostile"; return CursorKind.Attack;
            case Player.HoverAct.Talk: _forbidT = 0; why = "npc"; return CursorKind.Talk;
            case Player.HoverAct.PickUp: _forbidT = 0; why = "loot"; return CursorKind.PickUp;
            case Player.HoverAct.Interact: _forbidT = 0; why = "object"; return CursorKind.Interact;
            case Player.HoverAct.Unreachable:
                if (p.Hover != null) { _forbidT = 0; why = "hostile out of reach"; return CursorKind.Forbidden; }
                // ground that cannot be walked on: wait a moment, so sweeping the pointer across a wall does not flicker
                _forbidT += dt;
                if (_forbidT >= 0.08f || _kind == CursorKind.Forbidden) { why = "unreachable ground"; return CursorKind.Forbidden; }
                why = "ground"; return _kind == CursorKind.Ui || _kind == CursorKind.Busy ? CursorKind.Default : _kind;
            default: _forbidT = 0; why = "ground"; return CursorKind.Default;
        }
    }

    /// <summary>The picture for an object's label (the same reading Player.HoverKind gives the object itself).</summary>
    private static CursorKind KindOf(Interactable o) => o.Kind switch { "npc" or "prisoner" => CursorKind.Talk, "loot" => CursorKind.PickUp, _ => CursorKind.Interact };

    /// <summary>The reason with names, for the log line only.</summary>
    private static string Detail(string why)
    {
        try
        {
            var g = Game.I; var p = g?.Player;
            if (p == null || !IsInstanceValid(p)) return why;
            switch (why)
            {
                case "hostile": case "hostile out of reach": return p.Hover != null && IsInstanceValid(p.Hover) ? $"{why}: {p.Hover.DisplayName}" : why;
                case "npc": case "loot": case "object": return p.HoverObj != null && IsInstanceValid(p.HoverObj) ? $"{why}: {p.HoverObj.Kind} '{p.HoverObj.Label}'" : why;
                case "label": { var o = g.Hud?.PointerObj; return o != null && IsInstanceValid(o) ? $"label of {o.Kind} '{o.Label}'" : why; }
                case "ground": case "unreachable ground": return $"{why} ({p.Aim.X:0.0},{p.Aim.Y:0.0})";
            }
        }
        catch { /* the log line must never break the frame */ }
        return why;
    }

    /// <summary>One line for the test harness: the state of the cursor right now.</summary>
    public static string Describe()
    {
        var c = I;
        if (c == null) return "no cursor node";
        if (!c._ready) return "no cursor art (system cursor)";
        if (c._system) return "system cursor (setting)";
        var h = HotspotOf(c._kind);
        return $"kind={c._kind} size={c._size} hotspot={h.X:0},{h.Y:0} shape={Input.GetCurrentCursorShape()} [{c._fitWhy}]";
    }

    /// <summary>Test harness: the sizes of the set, the picture of a kind at a size (null when missing) and its hotspot.</summary>
    public static IReadOnlyList<int> Sizes => I?._sizes ?? Array.Empty<int>();
    public static Image ImageOf(CursorKind kind, int size, out Vector2 hot)
    {
        hot = Vector2.Zero;
        var a = I?.SetFor(size)?[(int)kind];
        if (a == null) return null;
        hot = a.Hot;
        return a.Img;
    }
}
