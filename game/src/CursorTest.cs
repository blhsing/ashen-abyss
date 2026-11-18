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
///   on the title screen (no --new): the menu background and a menu entry (pointing-hand shape).
/// Use a private slot (--slot=90..99) and --god.
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
            if (g.State == Game.GameState.Play && P != null && IsInstanceValid(P) && g.Level != null) _run = Level();
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
        _point = null; Player.TestMouse = null;
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
        }
        else Skip("empty ground: no free spot on screen");

        // ── ground that cannot be reached ──
        var wall = Unwalkable();
        if (wall is { } w)
        {
            PointAtWorld(() => World.To3(w));
            yield return 0.5f;
            Check($"unreachable ground at ({w.X:0},{w.Y:0})", CursorKind.Forbidden);
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
        var win = GetTree().Root;
        Vector2I size0 = win.Size;
        foreach (var s in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080), new Vector2I(2560, 1440), size0 })
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
        Check(Cursors.Active, "the game's cursors are active");
        HotspotChecks();
        SizeTable();
        PointAtScreen(new Vector2(ViewSize.X * 0.78f, ViewSize.Y * 0.5f));
        yield return 0.6f;
        Check($"title background (shape {Input.GetCurrentCursorShape()})", CursorKind.Ui);
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
