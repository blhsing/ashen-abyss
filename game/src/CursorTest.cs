using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// Test driver of the contextual mouse cursor (--cursor-test, added by <see cref="Cursors"/>). A screenshot does not
/// contain the operating system's cursor, so the proof is in the log: it holds a simulated pointer (Player.TestMouse
/// for the hero's picking + synthetic mouse-motion events for the interface) over one thing after another and writes
/// "cursortest PASS/FAIL &lt;what&gt;: want X got Y" with the size and hotspot in use.
///   in a level (--new=… --area=…): hotspots and the size table, empty ground, unreachable ground, loot, a chest, a
///     shrine, a portal, a waypoint, a totem, an NPC (objects it places itself around the hero), every other kind of
///     object the level holds, a monster, a panel and a button on it, the HUD, the system-cursor setting, the text
///     scale, the window size, then a journey (--cursor-then=&lt;area&gt;, default town) for the loading cursor.
///   on the title screen (no --new): the menu background, a menu entry (pointing-hand shape), the class screen's
///     turn-the-hero area (drag shape) and the name field (I-beam).
///   --cursor-test=chase (in a level with monsters): the click-to-attack run-up. The enemy that was clicked out of
///     reach shows the blade while the hero runs at it; the pointer leaving it with the button up / with the button
///     held; an enemy no path leads to (the red ring after the run-up gave up, the blade again 2.5 s later).
///     Monsters are frozen and one is moved to open ground for this: the cursor is what is checked, not the fight.
///   --cursor-expect=&lt;px|off&gt; (with --cursor-drop): the fallback. Pictures missing at the wanted size -> the nearest
///     complete size is used (px); a picture missing at every size -> the feature is off, the system cursor stays (off).
/// Use the cursor stream's own slot (--slot=94) and --god.
/// </summary>
public partial class CursorTest : Node
{
    private const string Cat = "cursortest";
    private IEnumerator<float> _run;
    private double _wait = 1.5;
    private Func<Vector2?> _point;          // where the simulated pointer is held (design px), re-evaluated every frame
    private int _pass, _fail, _skip;
    private bool _sawBusy, _watchBusy;
    private readonly List<Vector2> _used = new();
    private static Game G => Game.I;
    private static Player P => G?.Player;

    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; Log.Info(Cat, "cursor test armed"); }

    public override void _Process(double delta)
    {
        var g = G;
        if (g == null) return;
        if (_watchBusy && g.State == Game.GameState.Loading && Cursors.Current == CursorKind.Busy) _sawBusy = true;
        HoldPointer();
        if (_run == null)
        {
            string mode = (g.ArgValue("cursor-test") ?? "").Trim().ToLowerInvariant();
            if (g.State == Game.GameState.Play && P != null && IsInstanceValid(P) && g.Level != null) _run = mode == "chase" ? Chase() : Level();
            else if (g.State == Game.GameState.Menu && !g.Arg("new") && !g.Arg("load")) _run = Menu();
            else return;
        }
        if (g.State == Game.GameState.Loading) return;       // a loading screen in between: wait
        _wait -= delta;
        if (_wait > 0) return;
        bool more;
        try { more = _run.MoveNext(); }
        catch (Exception e) { Log.Error(Cat, "test step failed", e); more = false; _fail++; }
        if (more) { _wait = _run.Current; return; }
        Log.Info(Cat, $"done pass={_pass} fail={_fail} skip={_skip}");
        _run = Idle();
        _point = null; Player.TestMouse = null; Player.TestLeft = Player.TestRight = Player.TestShift = false;
        g.Quit();
    }

    private static IEnumerator<float> Idle() { while (true) yield return 1f; }

    // ───────────────────────── simulated pointer ─────────────────────────

    private void HoldPointer()
    {
        if (_point == null) return;
        Vector2? at;
        try { at = _point(); } catch { at = null; }
        if (at is not { } d) return;
        Player.TestMouse = d;
        Vector2 win = GetViewport().GetFinalTransform() * d;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = win, GlobalPosition = win });
    }

    private static Vector2 Screen(Vector3 world) => G.Cam.Cam.UnprojectPosition(world);
    private static Vector2 ViewSize => G.GetViewport().GetVisibleRect().Size;

    /// <summary>On screen and clear of the HUD's corners (minimap and tracker on the right, bars at the bottom).</summary>
    private static bool ScreenOk(Vector3 world)
    {
        var cam = G.Cam.Cam;
        if (cam.IsPositionBehind(world)) return false;
        Vector2 s = Screen(world), v = ViewSize;
        return s.X > v.X * 0.10f && s.X < v.X * 0.74f && s.Y > v.Y * 0.14f && s.Y < v.Y * 0.70f;
    }

    private void PointAtWorld(Func<Vector3> where) => _point = () => Screen(where());
    private void PointAtScreen(Vector2 design) => _point = () => design;

    private void Check(string what, CursorKind want)
    {
        var got = Cursors.Current;
        bool ok = got == want;
        if (ok) _pass++; else _fail++;
        var p = P;
        string hover = p != null && IsInstanceValid(p) ? $"hoverKind={p.HoverKind} overUi={G.Ui?.MouseOverUi} anyOpen={G.Ui?.AnyOpen}" : "";
        Log.Info(Cat, $"{(ok ? "PASS" : "FAIL")} {what}: want {want} got {got} | {Cursors.Describe()} {hover}");
    }

    private void Check(bool ok, string what)
    {
        if (ok) _pass++; else _fail++;
        Log.Info(Cat, $"{(ok ? "PASS" : "FAIL")} {what}");
    }

    private void Skip(string what) { _skip++; Log.Info(Cat, $"SKIP {what}"); }

    /// <summary>
    /// --cursor-shots=&lt;folder&gt;: a screenshot of this moment plus a "SHOT" log line (where the pointer is in window
    /// pixels, which picture and hotspot the cursor has). A screenshot never contains the cursor itself;
    /// Y:\tools\cursor\ingame.py pastes the picture in at that place afterwards, to judge it at its real size in the game.
    /// </summary>
    private void Shot(string name)
    {
        string dir = G.ArgValue("cursor-shots");
        if (string.IsNullOrWhiteSpace(dir) || _point == null) return;
        Vector2? at;
        try { at = _point(); } catch { at = null; }
        if (at is not { } d) return;
        Vector2 win = GetViewport().GetFinalTransform() * d;
        // the picture on screen: the arrow's, or the one of the shape a control asked for
        CursorKind kind = Input.GetCurrentCursorShape() switch
        {
            Input.CursorShape.PointingHand => CursorKind.UiHot, Input.CursorShape.Ibeam => CursorKind.Caret,
            Input.CursorShape.Drag or Input.CursorShape.CanDrop => CursorKind.PickUp, Input.CursorShape.Forbidden => CursorKind.Forbidden,
            Input.CursorShape.Wait or Input.CursorShape.Busy => CursorKind.Busy, _ => Cursors.Current,
        };
        var hot = Cursors.HotspotOf(kind);
        string path = System.IO.Path.Combine(dir.Trim(), name + ".png");
        Log.Info(Cat, $"SHOT {path} pointer={win.X:0},{win.Y:0} picture={Cursors.FileName(kind)}_{Cursors.Size} hotspot={hot.X:0},{hot.Y:0}");
        G.HarnessShot(path);
    }

    // ───────────────────────── places ─────────────────────────

    private static bool Clear(Vector2 at, float fromMonsters, float fromObjects)
    {
        foreach (var m in World.Monsters) if (m != null && IsInstanceValid(m) && !m.Dead && m.Pos2.DistanceTo(at) < fromMonsters + m.Radius) return false;
        foreach (var i in Interactable.All) if (i != null && IsInstanceValid(i) && i.Pos2.DistanceTo(at) < fromObjects) return false;
        return true;
    }

    /// <summary>A walkable spot near the hero that is on screen, away from monsters, objects and spots already used.</summary>
    private Vector2? FreeSpot(float minD, float maxD, float apart = 3.6f)
    {
        var p = P; var grid = World.Grid;
        for (float d = minD; d <= maxD; d += 0.8f)
            for (int a = 0; a < 16; a++)
            {
                float ang = a * Mathf.Tau / 16f + d;
                Vector2 at = p.Pos2 + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * d;
                if (grid != null && (!grid.WalkAt(at) || !grid.LineWalk(p.Pos2, at))) continue;
                if (!ScreenOk(World.To3(at)) || !Clear(at, 3.5f, 2.6f)) continue;
                bool far = true;
                foreach (var u in _used) if (u.DistanceTo(at) < apart) { far = false; break; }
                if (!far) continue;
                return at;
            }
        return null;
    }

    /// <summary>A cell that cannot be walked on, on screen, with nothing near it that would be picked instead.</summary>
    private static Vector2? Unwalkable()
    {
        var p = P; var grid = World.Grid;
        if (grid == null) return null;
        var c0 = grid.CellOf(p.Pos2);
        for (int r = 1; r <= 12; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    int x = c0.X + dx, y = c0.Y + dy;
                    if (grid.Walk(x, y)) continue;
                    Vector2 at = grid.Center(x, y);
                    if (!ScreenOk(World.To3(at)) || !Clear(at, 3.5f, 2.6f)) continue;
                    return at;
                }
        return null;
    }

    /// <summary>Puts the hero two or three cells from the nearest unwalkable ground (away from monsters). False when the level has none.</summary>
    private static bool StandBesideWall()
    {
        var p = P; var grid = World.Grid;
        if (grid == null) return false;
        var c0 = grid.CellOf(p.Pos2);
        for (int r = 3; r <= 70; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    int x = c0.X + dx, y = c0.Y + dy;
                    if (!grid.In(x, y) || grid.Walk(x, y) || !Clear(grid.Center(x, y), 3.5f, 2.6f)) continue;
                    for (int k = 0; k < 16; k++)
                    {
                        int sx = x + Mathf.RoundToInt(Mathf.Cos(k * Mathf.Tau / 16f) * 2.6f), sy = y + Mathf.RoundToInt(Mathf.Sin(k * Mathf.Tau / 16f) * 2.6f);
                        if (!grid.Walk(sx, sy) || !Clear(grid.Center(sx, sy), 9f, 2.6f)) continue;
                        p.Stop(); p.Teleport(grid.Center(sx, sy)); G.Cam?.Snap();
                        return true;
                    }
                }
        return false;
    }

    private static IEnumerable<Monster> Living => World.Monsters.Where(m => m != null && IsInstanceValid(m) && !m.Dead && !m.Untargetable && !m.Stealthed && m.IsInsideTree());

    // ───────────────────────── checks that need no pointer ─────────────────────────

    private void HotspotChecks()
    {
        int bad = 0, n = 0;
        foreach (int size in Cursors.Sizes)
        {
            var parts = new List<string>();
            foreach (CursorKind k in Enum.GetValues(typeof(CursorKind)))
            {
                var img = Cursors.ImageOf(k, size, out Vector2 hot);
                n++;
                if (img == null) { bad++; parts.Add($"{k}=MISSING"); continue; }
                int hx = (int)hot.X, hy = (int)hot.Y;
                bool inside = hx >= 0 && hy >= 0 && hx < img.GetWidth() && hy < img.GetHeight();
                // the hotspot must sit on the picture (the tip of the talon, blade or tail; the middle of the caret and the crest)
                float a = 0f;
                if (inside)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int x = hx + dx, y = hy + dy;
                            if (x >= 0 && y >= 0 && x < img.GetWidth() && y < img.GetHeight()) a = Mathf.Max(a, img.GetPixel(x, y).A);
                        }
                bool ok = inside && img.GetWidth() == size && img.GetHeight() == size && a > 0.5f && img.GetFormat() == Image.Format.Rgba8 && !img.HasMipmaps();
                if (!ok) bad++;
                parts.Add($"{k}={hx},{hy}{(ok ? "" : "(!)")}");
            }
            Log.Info(Cat, $"hotspots at {size} px: {string.Join(" ", parts)}");
        }
        Check(bad == 0 && n > 0, $"art: {n} pictures are the stated size, RGBA8 without mipmaps, hotspot on the picture ({bad} bad)");
        ImportCheck();
    }

    /// <summary>
    /// A development run only (the PNGs lie beside the project): what the game loaded must be the PNG on disk — the same
    /// alpha everywhere and the same colour wherever the picture is visible. An import older than the art, or a lossy
    /// one, would show here. (Godot's importer rewrites the colour of all-but-invisible pixels next to the picture,
    /// "fix alpha border": colour differences are therefore reported with the highest alpha they occur at.)
    /// </summary>
    private void ImportCheck()
    {
        const int Faint = 20;                // alpha (of 255) below which a pixel's colour is left to the importer
        int compared = 0, stale = 0, imported = 0, touched = 0, worstAlpha = 0;
        long faintPixels = 0;
        var names = new List<string>();
        foreach (int size in Cursors.Sizes)
            foreach (CursorKind k in Enum.GetValues(typeof(CursorKind)))
            {
                string res = $"res://assets/ui/cursor/{Cursors.FileName(k)}_{size}.png";
                string path = ProjectSettings.GlobalizePath(res);
                var img = Cursors.ImageOf(k, size, out _);
                if (img == null || !System.IO.File.Exists(path)) continue;
                if (ResourceLoader.Exists(res)) imported++;
                var disk = Image.LoadFromFile(path);
                if (disk == null || disk.IsEmpty()) continue;
                if (disk.GetFormat() != Image.Format.Rgba8) disk.Convert(Image.Format.Rgba8);
                compared++;
                bool same = disk.GetWidth() == img.GetWidth() && disk.GetHeight() == img.GetHeight();
                int faint = 0, top = 0;
                if (same)
                {
                    byte[] a = img.GetData(), b = disk.GetData();
                    same = a.Length == b.Length;
                    for (int i = 0; same && i + 3 < a.Length; i += 4)
                    {
                        if (a[i + 3] != b[i + 3]) { same = false; break; }
                        if (a[i + 3] == 0 || a[i] == b[i] && a[i + 1] == b[i + 1] && a[i + 2] == b[i + 2]) continue;
                        faint++; if (a[i + 3] > top) top = a[i + 3];          // a visible pixel whose colour differs
                    }
                    if (top >= Faint) same = false;
                }
                if (top > worstAlpha) worstAlpha = top;
                if (!same) { stale++; if (names.Count < 8) names.Add($"{Cursors.FileName(k)}_{size}(a{top})"); }
                else if (faint > 0) { touched++; faintPixels += faint; }
            }
        if (compared == 0) { Skip("import: no PNGs beside the project to compare with (an installed build)"); return; }
        Check(stale == 0, $"import: {compared} loaded pictures ({imported} through Godot's import) have the alpha of the PNGs on disk and their colour wherever alpha >= {Faint}/255; {stale} differ{(stale > 0 ? ": " + string.Join(", ", names) : "")}");
        Log.Info(Cat, $"  import detail: in {touched} pictures the importer recoloured {faintPixels} nearly invisible border pixels (highest alpha among them {worstAlpha}/255)");
    }

    private void SizeTable()
    {
        var sizes = Cursors.Sizes;
        (Vector2 win, float disp, float text, int want, string name)[] rows =
        {
            (new Vector2(1920, 1080), 1f, 1f, 32, "1080p 100 %"), (new Vector2(1600, 900), 1f, 1f, 32, "1600x900 window 100 %"),
            (new Vector2(1920, 1080), 1.25f, 1f, 40, "1080p laptop 125 %"), (new Vector2(2560, 1440), 1f, 1f, 40, "1440p 100 %"),
            (new Vector2(3840, 2160), 1f, 1f, 64, "4K 100 %"), (new Vector2(3840, 2160), 1.25f, 1f, 64, "4K 125 %"),
            (new Vector2(3840, 2160), 1.5f, 1.3f, 96, "4K 150 % text 特大"), (new Vector2(5120, 2880), 2f, 1f, 96, "5K 200 %"),
            (new Vector2(1600, 900), 1.5f, 1f, 48, "1600x900 window on a 150 % display"), (new Vector2(2560, 1600), 1.5f, 1f, 48, "2560x1600 laptop 150 %"),
            (new Vector2(1920, 1080), 1f, 1.15f, 40, "1080p text 大"), (new Vector2(1920, 1080), 1f, 1.3f, 40, "1080p text 特大"),
            (new Vector2(1280, 720), 1f, 1f, 32, "720p"),
        };
        int bad = 0;
        foreach (var r in rows)
        {
            int got = Cursors.SizeFor(r.win, r.disp, r.text, sizes, out float target);
            if (got != r.want) bad++;
            Log.Info(Cat, $"size table: {r.name} -> wants {target:0.#} px -> {got} px (expected {r.want}){(got == r.want ? "" : " (!)")}");
        }
        Check(bad == 0, $"size table: {rows.Length} screens pick the expected size");
    }

    /// <summary>
    /// The fallback (--cursor-drop + --cursor-expect): never a half set. Returns true when the run was a fallback run
    /// (the art checks that assume a whole set are skipped then).
    /// </summary>
    private bool FallbackCheck()
    {
        var g = G;
        string drop = g.ArgValue("cursor-drop"), expect = (g.ArgValue("cursor-expect") ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(drop)) return false;
        Log.Info(Cat, $"fallback: pictures treated as missing = {drop}; expected = {(expect.Length > 0 ? expect : "(not given)")}; now: {Cursors.Describe()}");
        if (expect == "off")
        {
            Check(!Cursors.Active, $"fallback: a picture is missing at every size -> the game's cursors are off, the system cursor stays ({Cursors.Describe()})");
            return true;
        }
        int size = Cursors.Size;
        int whole = 0;
        foreach (CursorKind k in Enum.GetValues(typeof(CursorKind))) if (Cursors.ImageOf(k, size, out _) != null) whole++;
        int kinds = Enum.GetValues(typeof(CursorKind)).Length;
        Check(Cursors.Active && whole == kinds, $"fallback: the size in use ({size} px) has all {kinds} pictures ({whole} found), the game's cursors stay on");
        if (int.TryParse(expect, out int want))
        {
            int wanted = Cursors.SizeFor(GetTree().Root.Size, Cursors.DisplayScale(), UiTheme.TextScale, Cursors.Sizes, out float target);
            Check(size == want && wanted != want, $"fallback: this window wants {target:0.#} px -> {wanted} px, which is incomplete -> {size} px is used (expected {want})");
        }
        return true;
    }

    private int ExpectedSizeNow()
    {
        Vector2 ws = GetTree().Root.Size;
        float ds = Cursors.DisplayScale();
        return Cursors.SizeFor(ws, ds, UiTheme.TextScale, Cursors.Sizes, out _);
    }

    // ───────────────────────── the run in a level ─────────────────────────

    private IEnumerator<float> Level()
    {
        var g = G; var p = P;
        Log.Info(Cat, $"level {g.Level.AreaId}: {Cursors.Describe()}");
        if (FallbackCheck())
        {
            // a fallback run: with the game's cursors still on, one thing in the world must still switch the picture
            if (Cursors.Active)
            {
                var fs = FreeSpot(3f, 7f);
                if (fs is { } f0) { PointAtWorld(() => World.To3(f0)); yield return 0.5f; Check("fallback: empty ground", CursorKind.Default); }
            }
            _point = null; Player.TestMouse = null;
            yield break;
        }
        Check(Cursors.Active, "the game's cursors are active");
        HotspotChecks();
        SizeTable();
        Check(Cursors.Size == ExpectedSizeNow(), $"size in use {Cursors.Size} px = the size for this window ({ExpectedSizeNow()} px)");
        yield return 0.3f;

        // ── empty ground ──
        var spot = FreeSpot(3f, 7f);
        if (spot is { } gs)
        {
            PointAtWorld(() => World.To3(gs));
            yield return 0.5f;
            Check("empty ground", CursorKind.Default);
            Shot("ground");
        }
        else Skip("empty ground: no free spot on screen");

        // ── ground that cannot be reached ──
        var wall = Unwalkable();
        if (wall == null && StandBesideWall())
        {
            // none in view from where the hero woke: he now stands beside the nearest unwalkable ground
            yield return 0.8f;
            wall = Unwalkable();
            spot = FreeSpot(3f, 7f);
        }
        if (wall is { } w)
        {
            PointAtWorld(() => World.To3(w));
            yield return 0.5f;
            Check($"unreachable ground at ({w.X:0},{w.Y:0})", CursorKind.Forbidden);
            Shot("ground_unreachable");
            if (spot is { } back)
            {
                PointAtWorld(() => World.To3(back));
                yield return 0.4f;
                Check("back on walkable ground", CursorKind.Default);
            }
        }
        else Skip("unreachable ground: no unwalkable cell on screen");

        // ── things the test places around the hero ──
        var rng = new Random(7);
        var made = new List<(string what, CursorKind want, Func<Vector2, Interactable> make)>
        {
            ("loot on the ground", CursorKind.PickUp, at => Loot.DropItem(ItemDb.Generate(Math.Clamp(p.Hero.D.Level * 10, 10, ItemDb.MaxPower), Rarity.Rare, p.Hero.Class, ItemKind.Weapon), at)),
            ("a chest", CursorKind.Interact, at => WorldObjects.Chest(at, 1)),
            ("a shrine", CursorKind.Interact, at => WorldObjects.Shrine(at)),
            ("a portal", CursorKind.Interact, at => WorldObjects.Portal(at, "測試傳送門", "", Palette.Gold, _ => { })),
            ("a waypoint", CursorKind.Interact, at => WorldObjects.Waypoint(at)),
            ("a totem", CursorKind.Interact, at => WorldObjects.Idol(at, () => { })),
            ("a town gate (door)", CursorKind.Interact, at => WorldObjects.TownGate(at)),
            ("an NPC", CursorKind.Talk, at => WorldObjects.CampFolk(at, 0f, rng)),
        };
        foreach (var (what, want, make) in made)
        {
            var at = FreeSpot(3.2f, 9f);
            if (at is not { } pos) { Skip($"{what}: no free spot on screen"); continue; }
            Interactable obj = null;
            try { obj = make(pos); } catch (Exception e) { Log.Error(Cat, $"placing {what} failed", e); }
            if (obj == null) { Skip($"{what}: could not be placed"); continue; }
            _used.Add(pos);
            yield return 0.9f;                      // loot lands, models appear
            var o = obj;
            PointAtWorld(() => IsInstanceValid(o) ? new Vector3(o.GlobalPosition.X, 0f, o.GlobalPosition.Z) : World.To3(pos));
            yield return 0.5f;
            Check($"{what} (kind '{obj.Kind}')", want);
            Shot("object_" + obj.Kind);
            if (obj.Kind != "loot" && IsInstanceValid(obj)) { obj.QueueFree(); _used.Remove(pos); yield return 0.1f; }
        }
        // the label of the loot (the HUD owns that click): the same picture as the item itself
        var lootObj = Interactable.All.FirstOrDefault(i => i.Kind == "loot" && i.CanUse);
        if (lootObj != null)
        {
            var lo = lootObj;
            bool onLabel = false;
            for (int up = 0; up <= 150 && !onLabel; up += 5)
            {
                int u = up;
                _point = () => IsInstanceValid(lo) ? Screen(lo.GlobalPosition) - new Vector2(0f, u) : (Vector2?)null;
                yield return 0.07f;
                onLabel = g.Hud?.PointerObj == lo;
            }
            if (onLabel)
            {
                yield return 0.3f;
                Log.Info(Cat, $"loot label: pointerObj={g.Hud?.PointerObj?.Kind ?? "-"} overUi={g.Ui?.MouseOverUi} hoveredControl={(GetViewport().GuiGetHoveredControl() != null)}");
                Check("the label of the loot (the HUD owns the click)", CursorKind.PickUp);
                Shot("loot_label");
            }
            else Skip("loot label: the pointer never came to rest on it");
            if (IsInstanceValid(lo)) lo.QueueFree();
        }

        // ── every other kind of object this level holds (first of each kind near enough to stand by) ──
        var seen = new HashSet<string> { "loot", "gold", "globe" };
        foreach (var obj in Interactable.All.ToArray())
        {
            if (obj == null || !IsInstanceValid(obj) || !obj.CanUse || obj.AutoRadius > 0 || !seen.Add(obj.Kind)) continue;
            if (_used.Any(u => u.DistanceTo(obj.Pos2) < 0.5f)) continue;         // one of mine: already checked
            if (!ScreenOk(obj.GlobalPosition) || !Clear(obj.Pos2, 3f, -1f)) continue;
            var o = obj;
            PointAtWorld(() => IsInstanceValid(o) ? new Vector3(o.GlobalPosition.X, 0f, o.GlobalPosition.Z) : Vector3.Zero);
            yield return 0.45f;
            Check($"level object kind '{obj.Kind}' ({obj.Label})", obj.Kind is "npc" or "prisoner" ? CursorKind.Talk : CursorKind.Interact);
        }

        // ── the interface ──
        PointAtScreen(new Vector2(ViewSize.X * 0.5f, ViewSize.Y - 60f));
        yield return 0.5f;
        Check("the action bar (HUD)", CursorKind.Ui);
        Shot("hud");
        g.Ui.Open("inventory");
        yield return 0.8f;
        var panel = g.Ui.Find("inventory");
        if (panel != null && panel.IsVisibleInTree())
        {
            Rect2 r = panel.GetGlobalRect();
            PointAtScreen(r.Position + r.Size * new Vector2(0.5f, 0.5f));
            yield return 0.4f;
            Check("a panel (inventory)", CursorKind.Ui);
            var btn = FirstButton(panel);
            if (btn != null)
            {
                PointAtScreen(btn.GetGlobalRect().GetCenter());
                yield return 0.4f;
                Check($"a button on the panel ('{(btn as Button)?.Text}', shape {Input.GetCurrentCursorShape()})", CursorKind.Ui);
                Shot("panel_button");
            }
            else Skip("a button on the panel: none found");
            // behind an open panel the world does not answer: still the interface pointer
            PointAtScreen(new Vector2(ViewSize.X * 0.12f, ViewSize.Y * 0.4f));
            yield return 0.4f;
            Check("the world behind an open panel", CursorKind.Ui);
        }
        else Skip("panel: the inventory did not open");
        g.Ui.CloseAll();
        yield return 0.5f;

        // ── a hostile ──
        Monster mon = Living.Where(m => !m.IsBoss).OrderBy(m => m.Pos2.DistanceTo(p.Pos2)).FirstOrDefault();
        if (mon != null)
        {
            if (!ScreenOk(mon.GlobalPosition))
            {
                // walk the camera there: stand a few metres off, on ground the hero can stand on
                var grid = World.Grid;
                for (int a = 0; a < 12; a++)
                {
                    Vector2 at = mon.Pos2 + new Vector2(Mathf.Cos(a * Mathf.Tau / 12f), Mathf.Sin(a * Mathf.Tau / 12f)) * 5.5f;
                    if (grid != null && !grid.WalkAt(at)) continue;
                    p.Teleport(at);
                    break;
                }
                yield return 1.6f;
            }
            // the nearest one now (the pack has woken up and moves)
            for (int tries = 0; tries < 4; tries++)
            {
                mon = Living.Where(m => !m.IsBoss && ScreenOk(m.GlobalPosition)).OrderBy(m => m.Pos2.DistanceTo(p.Pos2)).FirstOrDefault();
                if (mon != null) break;
                yield return 0.6f;
            }
            if (mon != null)
            {
                var m = mon;
                PointAtWorld(() => IsInstanceValid(m) ? m.GlobalPosition + Vector3.Up * 1.0f : Vector3.Zero);
                yield return 0.5f;
                Check($"a hostile ({mon.DisplayName})", CursorKind.Attack);
                Shot("hostile");
            }
            else Skip("a hostile: none on screen after walking there");
        }
        else Skip("a hostile: this level has no monsters");

        // ── the setting: the system cursor ──
        bool was = g.Cfg.SystemCursor;
        g.Cfg.SystemCursor = true;
        yield return 0.3f;
        Check(!Cursors.Active, $"setting 滑鼠游標=系統: the game's cursors are off ({Cursors.Describe()})");
        g.Cfg.SystemCursor = was;
        yield return 0.3f;
        Check(Cursors.Active == !was, $"setting back: {Cursors.Describe()}");

        // ── size follows the text scale and the window ──
        _point = null; Player.TestMouse = null;
        int ts0 = UiTheme.TextScaleSetting;
        for (int ts = 0; ts <= 2; ts++)
        {
            UiTheme.ApplyTextScale(ts);
            yield return 0.4f;
            Check(Cursors.Size == ExpectedSizeNow(), $"text scale {ts} (x{UiTheme.TextScale:0.00}): size {Cursors.Size} px, expected {ExpectedSizeNow()} px");
        }
        UiTheme.ApplyTextScale(ts0);
        yield return 0.3f;
        // resizing the test window covers the user's desktop for a moment: only when asked for (--cursor-resize)
        var win = GetTree().Root;
        Vector2I size0 = win.Size;
        if (!g.Arg("cursor-resize")) Log.Info(Cat, "window-size steps not run in this pass (--cursor-resize runs them)");
        else foreach (var s in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080), new Vector2I(2560, 1440), size0 })
        {
            win.Size = s;
            yield return 0.7f;
            Vector2I real = win.Size;
            Check(Cursors.Size == ExpectedSizeNow(), $"window asked {s.X}x{s.Y} got {real.X}x{real.Y}: size {Cursors.Size} px, expected {ExpectedSizeNow()} px");
        }

        // ── a journey: the busy cursor while the loading screen is up ──
        string then = g.ArgValue("cursor-then");
        if (string.IsNullOrWhiteSpace(then)) then = g.Level.AreaId == ZoneDb.Town ? "" : ZoneDb.Town;
        if (then.Length > 0)
        {
            _watchBusy = true; _sawBusy = false;
            g.Ui.CloseAll();
            bool going = g.HarnessTravel(then.Trim());
            Log.Info(Cat, $"journey to {then}: started={going}");
            yield return 2.5f;                      // the run is held while the loading screen is up
            _watchBusy = false;
            Check(_sawBusy, $"loading screen on the way to {then}: the busy cursor was shown");
            yield return 1.0f;
            // whatever stands in the new area: every kind once
            p = P;
            var seen2 = new HashSet<string> { "loot", "gold", "globe" };
            foreach (var obj in Interactable.All.OrderBy(i => i.Pos2.DistanceTo(p.Pos2)).ToArray())
            {
                if (obj == null || !IsInstanceValid(obj) || !obj.CanUse || obj.AutoRadius > 0 || seen2.Contains(obj.Kind)) continue;
                if (!ScreenOk(obj.GlobalPosition))
                {
                    if (obj.Pos2.DistanceTo(p.Pos2) > 60f) continue;
                    var grid = World.Grid; bool moved = false;
                    for (int a = 0; a < 12 && !moved; a++)
                    {
                        Vector2 at = obj.Pos2 + new Vector2(Mathf.Cos(a * Mathf.Tau / 12f), Mathf.Sin(a * Mathf.Tau / 12f)) * 4.5f;
                        if (grid != null && !grid.WalkAt(at)) continue;
                        p.Teleport(at); moved = true;
                    }
                    if (!moved) continue;
                    yield return 1.5f;
                    if (!IsInstanceValid(obj) || !ScreenOk(obj.GlobalPosition)) continue;
                }
                seen2.Add(obj.Kind);
                var o = obj;
                PointAtWorld(() => IsInstanceValid(o) ? new Vector3(o.GlobalPosition.X, 0f, o.GlobalPosition.Z) : Vector3.Zero);
                yield return 0.5f;
                Check($"{then}: object kind '{obj.Kind}' ({obj.Label})", obj.Kind is "npc" or "prisoner" ? CursorKind.Talk : CursorKind.Interact);
            }
        }
        _point = null; Player.TestMouse = null;
        yield return 0.3f;
    }

    // ───────────────────────── the click-to-attack run-up ─────────────────────────

    private static void Freeze()
    {
        foreach (var m in Living) if (m.ProcessMode != ProcessModeEnum.Disabled) m.ProcessMode = ProcessModeEnum.Disabled;
    }

    private static bool ClearOf(Vector2 at, float fromMonsters, float fromObjects, Monster except)
    {
        foreach (var m in World.Monsters) if (m != null && m != except && IsInstanceValid(m) && !m.Dead && m.Pos2.DistanceTo(at) < fromMonsters + m.Radius) return false;
        foreach (var i in Interactable.All) if (i != null && IsInstanceValid(i) && i.Pos2.DistanceTo(at) < fromObjects) return false;
        return true;
    }

    private static float Now => Time.GetTicksMsec() / 1000f;

    private IEnumerator<float> Chase()
    {
        var g = G; var p = P; var grid = World.Grid;
        Log.Info(Cat, $"chase test in {g.Level.AreaId}: {Cursors.Describe()}");
        Check(Cursors.Active, "the game's cursors are active");
        if (grid == null) { Skip("chase: this level has no grid"); yield break; }
        Freeze();
        Monster t = Living.Where(m => !m.IsBoss && m != g.Level?.BossMon).OrderBy(m => m.Pos2.DistanceTo(p.Pos2)).FirstOrDefault();
        if (t == null) { Skip("chase: this level has no monsters"); yield break; }
        t.MaxLife *= 200f; t.Life = t.MaxLife;                 // lives through the test

        // ── a stage on open ground: the hero at A, the enemy at B (9.5 m away, a straight walk), C = open ground beside A ──
        // The camera rests on the hero, so what is on screen relative to him here is on screen wherever he stands.
        bool Seen(Vector2 offset, float up) => ScreenOk(World.To3(p.Pos2 + offset) + Vector3.Up * up);
        Vector2 A = default, B = default, C = default;
        bool staged = false;
        var c0 = grid.CellOf(p.Pos2);
        float[] fars = { 9.5f, 8f, 6.5f };
        for (int r = 0; r <= 30 && !staged; r++)
            for (int dy = -r; dy <= r && !staged; dy++)
                for (int dx = -r; dx <= r && !staged; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    if (!grid.Walk(c0.X + dx, c0.Y + dy)) continue;
                    Vector2 a = grid.Center(c0.X + dx, c0.Y + dy);
                    if (!ClearOf(a, 9f, 3.5f, t)) continue;
                    for (int fi = 0; fi < fars.Length && !staged; fi++)
                        for (int k = 0; k < 8 && !staged; k++)
                        {
                            Vector2 dir = new Vector2(Mathf.Cos(k * Mathf.Tau / 8f), Mathf.Sin(k * Mathf.Tau / 8f)), side = new Vector2(-dir.Y, dir.X);
                            Vector2 offB = dir * fars[fi], offC = side * 4.5f - dir * 1.5f;
                            if (!Seen(offB, 1f) || !Seen(offC, 0f)) continue;
                            Vector2 b = a + offB, c = a + offC;
                            if (!grid.WalkAt(b) || !grid.LineWalk(a, b) || !ClearOf(b, 9f, 3.5f, t)) continue;
                            if (!grid.WalkAt(c) || !grid.LineWalk(a, c) || !ClearOf(c, 7f, 3.5f, t)) continue;
                            A = a; B = b; C = c; staged = true;
                        }
                }
        if (!staged) { Skip("chase: no open stage (hero, enemy 6.5-9.5 m away in a straight walk, open ground beside) found within 60 m"); yield break; }
        p.Stop(); p.Teleport(A); t.Pos2 = B; g.Cam?.Snap();
        yield return 0.6f;
        if (!ScreenOk(World.To3(B) + Vector3.Up) || !ScreenOk(World.To3(C)))
            Log.Info(Cat, $"  note: after the move the enemy / the open ground are not where expected on screen (enemy {Screen(World.To3(B))}, ground {Screen(World.To3(C))}); the pointer follows them anyway");
        Log.Info(Cat, $"stage: hero ({A.X:0.0},{A.Y:0.0}) enemy {t.DisplayName} ({B.X:0.0},{B.Y:0.0}) {A.DistanceTo(B):0.0} m, open ground ({C.X:0.0},{C.Y:0.0}); monsters frozen");
        Func<Vector3> onEnemy = () => IsInstanceValid(t) ? t.GlobalPosition + Vector3.Up * 1.0f : World.To3(B);
        Func<Vector3> onGround = () => World.To3(C);

        // ── 1. the enemy out of reach, before any click ──
        PointAtWorld(onEnemy);
        yield return 0.45f;
        Check($"an enemy {p.Pos2.DistanceTo(t.Pos2):0.0} m away (out of reach), before the click", CursorKind.Attack);
        Shot("chase_before_click");

        // ── 2. the click: the hero runs up; the pointer stays on the enemy ──
        int s0 = p.ChaseStarts, h0 = p.ChaseHits;
        { float w0 = Now; while (p.Busy && Now - w0 < 2.5f) yield return 0.03f; }      // a click during a swing would be dropped
        p.TestClick(0);
        int n = 0, bad = 0; bool ran = false; float t0 = Now;
        while (Now - t0 < 6f && p.ChaseHits == h0)
        {
            yield return 0.02f;
            if (ReferenceEquals(p.AttackTarget, t)) { ran = true; n++; if (Cursors.Current != CursorKind.Attack) bad++; if (n == 8) Shot("chase_running"); }
            else if (ran) break;
        }
        Check(p.ChaseStarts > s0 && ran && n >= 5 && bad == 0 && p.ChaseHits > h0,
            $"click on it: the run-up started ({p.ChaseStarts - s0}), the blade on {n - bad} of {n} samples while the hero ran at it ({Now - t0:0.00} s), ended '{p.LastChaseEnd}' at {p.Pos2.DistanceTo(t.Pos2):0.0} m [pointer ON the target: the blade comes from the hover, hoverKind={p.HoverKind}]");
        yield return 0.5f;
        Check("after the blow, the pointer still on the enemy", CursorKind.Attack);

        // ── 3. a click, then the pointer leaves for open ground with the button UP: the run-up goes on, a click there would walk ──
        Freeze();
        p.Stop(); p.Teleport(A); g.Cam?.Snap();
        PointAtWorld(onEnemy);
        yield return 0.5f;
        s0 = p.ChaseStarts; h0 = p.ChaseHits;
        { float w0 = Now; while (p.Busy && Now - w0 < 2.5f) yield return 0.03f; }      // a click during a swing would be dropped
        p.TestClick(0);
        yield return 0.12f;
        bool running = ReferenceEquals(p.AttackTarget, t);
        PointAtWorld(onGround);
        yield return 0.18f;
        bool still = ReferenceEquals(p.AttackTarget, t);
        var kUp = Cursors.Current;
        Check(running && still && kUp == CursorKind.Default,
            $"run-up in progress (target kept: {still}), button up, pointer moved to open ground: {kUp} (want Default: a click there would walk and cancel) hoverKind={p.HoverKind}");
        PointAtWorld(onEnemy);
        yield return 0.12f;
        Check("pointer back on the enemy the hero is running at", CursorKind.Attack);
        t0 = Now;
        while (Now - t0 < 5f && p.ChaseHits == h0 && ReferenceEquals(p.AttackTarget, t)) yield return 0.05f;
        Log.Info(Cat, $"  run-up 2 ended '{p.LastChaseEnd}' hits+{p.ChaseHits - h0}");
        yield return 0.5f;

        // ── 4. the button HELD: press on the enemy, the pointer slips off during the run-up and stays off after the blow ──
        Freeze();
        p.Stop(); p.Teleport(A); g.Cam?.Snap();
        PointAtWorld(onEnemy);
        yield return 0.5f;
        s0 = p.ChaseStarts; h0 = p.ChaseHits;
        int f0 = p.Fired;
        { float w0 = Now; while (p.Busy && Now - w0 < 2.5f) yield return 0.03f; }      // a click during a swing would be dropped
        Player.TestLeft = true;
        p.TestClick(0);
        yield return 0.12f;
        PointAtWorld(onGround);
        n = 0; bad = 0; ran = false; t0 = Now;
        while (Now - t0 < 6f && p.ChaseHits == h0)
        {
            yield return 0.02f;
            if (ReferenceEquals(p.AttackTarget, t)) { ran = true; if (p.Hover == null) { n++; if (Cursors.Current != CursorKind.Attack) bad++; if (n == 8) Shot("held_pointer_on_ground"); } }
            else if (ran) break;
        }
        Check(p.ChaseStarts > s0 && ran && n >= 5 && bad == 0,
            $"button held, the pointer slipped onto open ground during the run-up: the blade on {n - bad} of {n} samples (hoverKind={p.HoverKind}), ended '{p.LastChaseEnd}' [pointer OFF the target: the blade comes from the held-button rule; the button is the harness flag Player.TestLeft, not a real mouse button]");
        // the held button keeps striking although the pointer is on the ground
        n = 0; bad = 0; t0 = Now;
        while (Now - t0 < 2.4f)
        {
            yield return 0.03f;
            if (ReferenceEquals(p.AttackTarget, t) && p.Hover == null) { n++; if (Cursors.Current != CursorKind.Attack) bad++; }
        }
        Check(n >= 10 && bad == 0 && p.Fired - f0 >= 2,
            $"button still held after the blow, pointer on open ground: {p.Fired - f0} blows so far, the blade on {n - bad} of {n} samples");
        Player.TestLeft = false;
        yield return 0.45f;
        Check($"button released, pointer on open ground (attack target now: {(p.AttackTarget == null ? "none" : "kept")})", CursorKind.Default);
        PointAtWorld(onEnemy);
        yield return 0.3f;
        Check("pointer back on the enemy", CursorKind.Attack);

        // ── 5. an enemy no path leads to: deep inside unwalkable ground ──
        Freeze();
        p.Stop(); g.Cam?.Snap();
        yield return 0.4f;                      // the camera rests on the hero again (Seen measures from him)
        bool walled = false; Vector2 W = default, S = default;
        var h = grid.CellOf(A);
        for (int r = 2; r <= 45 && !walled; r++)
            for (int dy = -r; dy <= r && !walled; dy++)
                for (int dx = -r; dx <= r && !walled; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    int x = h.X + dx, y = h.Y + dy;
                    if (!grid.In(x, y) || grid.Walk(x, y)) continue;
                    bool deep = true;
                    for (int ay = -1; ay <= 1 && deep; ay++) for (int ax = -1; ax <= 1 && deep; ax++) if (grid.Walk(x + ax, y + ay)) deep = false;
                    if (!deep) continue;
                    // somewhere to stand three to five cells away, with the enemy on screen from there
                    for (int rr = 3; rr <= 5 && !walled; rr++)
                        for (int k = 0; k < 16 && !walled; k++)
                        {
                            int sx = x + Mathf.RoundToInt(Mathf.Cos(k * Mathf.Tau / 16f) * rr), sy = y + Mathf.RoundToInt(Mathf.Sin(k * Mathf.Tau / 16f) * rr);
                            if (!grid.Walk(sx, sy)) continue;
                            Vector2 s = grid.Center(sx, sy), w = grid.Center(x, y);
                            if (!Seen(w - s, 1f) || !ClearOf(s, 8f, 3.5f, t) || !ClearOf(w, 6f, 3.5f, t)) continue;
                            W = w; S = s; walled = true;
                        }
                }
        if (!walled) Skip("an enemy no path leads to: no 3x3 block of unwalkable cells with a place to stand 3-5 cells off (enemy on screen) within 90 m");
        else
        {
            p.Teleport(S); t.Pos2 = W; g.Cam?.Snap();
            yield return 0.6f;
            Log.Info(Cat, $"stage: enemy moved into unwalkable ground ({W.X:0.0},{W.Y:0.0}), hero ({S.X:0.0},{S.Y:0.0}) {S.DistanceTo(W):0.0} m away");
            PointAtWorld(onEnemy);
            yield return 0.45f;
            Check("an enemy no path leads to, before the click (the game cannot know yet)", CursorKind.Attack);
            s0 = p.ChaseStarts;
            { float w0 = Now; while (p.Busy && Now - w0 < 2.5f) yield return 0.03f; }      // a click during a swing would be dropped
            p.TestClick(0);
            t0 = Now; ran = false; n = 0; bad = 0;
            yield return 0.05f;
            while (Now - t0 < 14f)
            {
                yield return 0.03f;
                if (ReferenceEquals(p.AttackTarget, t)) { ran = true; n++; if (Cursors.Current != CursorKind.Attack) bad++; }
                else if (p.ChaseStarts > s0) break;
            }
            float gave = Now - t0;
            yield return 0.12f;
            var kind = Cursors.Current; var hk = p.HoverKind;
            Check(p.ChaseStarts > s0 && p.AttackTarget == null && kind == CursorKind.Forbidden && hk == Player.HoverAct.Unreachable,
                $"the run-up gave up after {gave:0.0} s ('{p.LastChaseEnd}'{(ran ? $", the blade on {n - bad} of {n} samples while trying" : ", at once")}): pointer on that enemy -> {kind} (hoverKind={hk})");
            Shot("hostile_unreachable");
            PointAtWorld(() => World.To3(S + (S - W).Normalized() * 2.5f));
            yield return 0.3f;
            Log.Info(Cat, $"  meanwhile the ground beside the hero: {Cursors.Current} (hoverKind={p.HoverKind})");
            PointAtWorld(onEnemy);
            yield return 3.0f;
            Check("3 s later the blade again on that enemy (a new click may try again)", CursorKind.Attack);
        }
        Player.TestLeft = false;
        _point = null; Player.TestMouse = null;
        yield return 0.3f;
    }

    private static BaseButton FirstButton(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is BaseButton b && b.IsVisibleInTree() && !b.Disabled && b.Size.X > 20f && b.MouseFilter != Control.MouseFilterEnum.Ignore) return b;
            var deep = FirstButton(c);
            if (deep != null) return deep;
        }
        return null;
    }

    // ───────────────────────── the run on the title screen ─────────────────────────

    private IEnumerator<float> Menu()
    {
        var g = G;
        yield return 1.5f;
        Log.Info(Cat, $"title screen: {Cursors.Describe()}");
        if (FallbackCheck())
        {
            if (Cursors.Active)
            {
                PointAtScreen(new Vector2(ViewSize.X * 0.78f, ViewSize.Y * 0.5f));
                yield return 0.6f;
                Check($"fallback: title background (shape {Input.GetCurrentCursorShape()})", CursorKind.Ui);
            }
            _point = null;
            yield break;
        }
        Check(Cursors.Active, "the game's cursors are active");
        HotspotChecks();
        SizeTable();
        Check(Cursors.Size == ExpectedSizeNow(), $"size in use {Cursors.Size} px = the size for this window ({ExpectedSizeNow()} px)");
        PointAtScreen(new Vector2(ViewSize.X * 0.78f, ViewSize.Y * 0.5f));
        yield return 0.6f;
        Check($"title background (shape {Input.GetCurrentCursorShape()})", CursorKind.Ui);
        Shot("title_background");
        BaseButton hand = null;
        foreach (var n in g.UiLayer.GetChildren())
        {
            hand = FirstWithShape(n, Control.CursorShape.PointingHand);
            if (hand != null) break;
        }
        if (hand != null)
        {
            PointAtScreen(hand.GetGlobalRect().GetCenter());
            yield return 0.6f;
            var shape = Input.GetCurrentCursorShape();
            Check(shape == Input.CursorShape.PointingHand, $"a menu entry ('{(hand as Button)?.Text}') asks for the pointing hand: shape {shape} -> the lit pointer (UiHot, hotspot {Cursors.HotspotOf(CursorKind.UiHot).X:0},{Cursors.HotspotOf(CursorKind.UiHot).Y:0})");
            Check("the arrow slot stays the interface pointer", CursorKind.Ui);
            Shot("title_menu_entry");
        }
        else Skip("a menu entry with the pointing-hand shape: none found");
        MainMenu menu = null;
        foreach (var n in g.UiLayer.GetChildren()) if (n is MainMenu mm) menu = mm;
        if (menu != null)
        {
            menu.Open("class");
            yield return 1.6f;
            var drag = FirstControl(menu, c => c.MouseDefaultCursorShape == Control.CursorShape.Drag && c.MouseFilter == Control.MouseFilterEnum.Stop);
            if (drag != null)
            {
                Rect2 r = drag.GetGlobalRect();
                PointAtScreen(r.Position + r.Size * new Vector2(0.5f, 0.35f));
                yield return 0.6f;
                var shape = Input.GetCurrentCursorShape();
                Check(shape == Input.CursorShape.Drag, $"class screen, the turn-the-hero area asks for the drag shape: shape {shape} -> the reaching gauntlet (PickUp, hotspot {Cursors.HotspotOf(CursorKind.PickUp).X:0},{Cursors.HotspotOf(CursorKind.PickUp).Y:0})");
                Shot("class_turn_area");
            }
            else Skip("class screen: no drag area found");
            menu.Open("identity");
            yield return 1.6f;
            var edit = FirstControl(menu, c => c is LineEdit);
            if (edit != null)
            {
                PointAtScreen(edit.GetGlobalRect().GetCenter());
                yield return 0.6f;
                var shape = Input.GetCurrentCursorShape();
                Check(shape == Input.CursorShape.Ibeam, $"name field asks for the I-beam: shape {shape} -> the iron caret (hotspot {Cursors.HotspotOf(CursorKind.Caret).X:0},{Cursors.HotspotOf(CursorKind.Caret).Y:0})");
                Shot("name_field");
            }
            else Skip("name screen: no text field found");
            menu.Open("");
            yield return 0.8f;
        }
        _point = null;
        yield return 0.3f;
    }

    private static Control FirstControl(Node n, Func<Control, bool> want)
    {
        if (n is Control c && c.IsVisibleInTree() && c.Size.X > 20f && c.Size.Y > 10f && want(c)) return c;
        foreach (var ch in n.GetChildren())
        {
            var deep = FirstControl(ch, want);
            if (deep != null) return deep;
        }
        return null;
    }

    private static BaseButton FirstWithShape(Node n, Control.CursorShape shape)
    {
        if (n is BaseButton b && b.IsVisibleInTree() && !b.Disabled && b.MouseDefaultCursorShape == shape && b.Size.X > 20f) return b;
        foreach (var c in n.GetChildren())
        {
            var deep = FirstWithShape(c, shape);
            if (deep != null) return deep;
        }
        return null;
    }
}
