using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AshenAbyss;

/// <summary>
/// Graphics review harness (created by Game when any --gfx* switch is given; never part of a normal session).
/// It walks a list of areas in one process, takes screenshots at fixed times, measures frame and GPU time, and can
/// render the 3D picture through an off-screen viewport of any size so 4K with FSR is measured and photographed
/// from a small window.
///
///   --gfx-tour=town,wastes,wastes_d1,rift:1   areas to visit in order (label=area names the files; default label = theme id)
///   --gfx-at=5,12            seconds after an area is on screen at which a shot is taken (default 5,12)
///   --gfx-out=Y:\shots\gfx\x output folder (shots are &lt;label&gt;_&lt;n&gt;.png, timings go to perf.txt)
///   --gfx-q=0..3             quality preset for this run only (the saved setting is not touched)
///   --gfx-scale=0.67         render scale for this run only
///   --gfx-res=3840x2160      render the 3D scene at this size off screen and show it scaled into the window
///   --gfx-beauty             also save the off-screen picture at full size (&lt;label&gt;_&lt;n&gt;_full.png, no HUD)
///   --gfx-hud=0              hide the HUD and every panel
///   --gfx-zoom=1.2           camera zoom
///   --gfx-novsync            uncapped frame rate while measuring
///   --gfx-fx                 spawn a gallery of ground zones, telegraphs, bursts and loot beams round the hero before each shot
///                            (--gfx-fx=telec|telecf|telek|telekf|telel|teler|telemix|telering: the ground sigils of every school, see TeleGallery)
///   --gfx-stay               do not quit after the last area
///   a tour stop may carry a placement: label=area@behind:tree:2 stands the hero just north of (behind, seen from the
///   lens) the 3rd nearest prop slot of that category; consecutive stops in the same area do not reload it
///   Options of one stop, joined by '+' after the '@' (they replace the global switch of the same name for that stop):
///     behind:tree:2[:back[:side]] · at:3/5.5/8 (shot times) · fx[:2] · hud:0 · fov:16 · life:0.15 · hug:5 · evade:5 ·
///     run:7.5:30 · bot (the test bot plays) · clips:Roll*1.9/Sword_Dash*1.5[:start] · ui:paragon (opens that panel) ·
///     class:astromancer[:level[:paragon]] · focusbehind:tree:1 (holds the target cut on the spot behind that prop, marked by a ring)
///     (a new hero of that class starts in the stop's area: a whole class sweep in one process)
///   --gfx-burst=6,0.06,8     eight more shots from second 6 on, one every 0.06 s
///   --gfx-evade=6            the hero evades to screen-right at second 6; the pose is logged for 0.9 s
///   --gfx-run=5              the hero runs to screen-right from second 5 on (--gfx-runspeed=30 adds that much move speed %)
///   --gfx-life=0.2           holds the hero's life at this fraction (low-life warning)
///   --gfx-hug=4              at second 4 the hero is put right on top of the nearest boss or giant (body blocking)
///   --gfx-fov=14             a narrower lens for close-ups
///   --gfx-clips=Roll@1.9,Sword_Dash@1.5   clip viewer: from --gfx-clips-at (default 5 s) each clip is played on the standing
///                            hero (turned to screen-right, its own controller held) and shot 0.08 / 0.2 / 0.32 / 0.5 s in
/// </summary>
public partial class EnvProbe : Node
{
    private sealed class Stop { public string Label, Area, Spec; public readonly Dictionary<string, string[]> Opt = new(); }

    private readonly List<Stop> _tour = new();
    private readonly List<double> _at = new();
    private string _out;
    private bool _beauty, _hideHud, _fx, _stay, _noVsync;
    private float _zoom;
    private Vector2I _res;

    private int _index = -1, _shot;
    private double _t = -1, _wait, _sumFrame, _sumGpu, _sumCpu, _worst, _drawSum, _primSum;
    private int _frames, _gpuSamples;
    private bool _entering, _fxDone, _finished, _errorLogged;
    private string _areaNow;
    private ulong _lastTick;

    private SubViewport _sub;
    private Camera3D _subCam;
    private Rid _measureRid;
    private readonly List<Task> _saves = new();

    private static string A(string name) => Game.I?.ArgValue(name);
    private static bool Has(string name) => Game.I != null && Game.I.Arg(name);

    public EnvProbe()
    {
        Name = "EnvProbe";
        ProcessPriority = 2000;                              // after the camera rig
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _EnterTree()
    {
        try { Setup(); }
        catch (Exception e) { Log.Error("gfx", "probe setup failed", e); }
    }

    private void Setup()
    {
        var inv = CultureInfo.InvariantCulture;
        string tour = A("gfx-tour");
        if (!string.IsNullOrWhiteSpace(tour))
            foreach (var part in tour.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int eq = part.IndexOf('=');
                string area = eq > 0 ? part[(eq + 1)..] : part;
                string label = eq > 0 ? part[..eq] : null;
                string spec = null;
                int atSign = area.IndexOf('@');
                if (atSign > 0) { spec = area[(atSign + 1)..]; area = area[..atSign]; }
                var stop = new Stop { Label = label, Area = area, Spec = spec };
                if (spec != null)
                    foreach (var item in spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var ip = item.Split(':');
                        stop.Opt[ip[0]] = ip.Length > 1 ? ip[1..] : Array.Empty<string>();
                    }
                _tour.Add(stop);
            }
        string at = A("gfx-at");
        if (!string.IsNullOrWhiteSpace(at))
            foreach (var part in at.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (double.TryParse(part, NumberStyles.Float, inv, out double v) && v >= 0) _at.Add(v);
        if (_at.Count == 0) { _at.Add(5); _at.Add(12); }
        string burst = A("gfx-burst");
        if (!string.IsNullOrWhiteSpace(burst))
        {
            var bp = burst.Split(',');
            if (bp.Length == 3 && double.TryParse(bp[0], NumberStyles.Float, inv, out double b0) && double.TryParse(bp[1], NumberStyles.Float, inv, out double bd) && int.TryParse(bp[2], out int bn))
                for (int i = 0; i < Math.Clamp(bn, 1, 40); i++) _at.Add(b0 + i * Math.Max(bd, 0.01));
        }
        _evadeAt = double.TryParse(A("gfx-evade"), NumberStyles.Float, inv, out double ev) ? ev : -1;
        _runAt = double.TryParse(A("gfx-run"), NumberStyles.Float, inv, out double rn) ? rn : -1;
        _runSpeed = float.TryParse(A("gfx-runspeed"), NumberStyles.Float, inv, out float rs) ? rs : 0f;
        _hugAt = double.TryParse(A("gfx-hug"), NumberStyles.Float, inv, out double hg) ? hg : -1;
        _lifeFrac = float.TryParse(A("gfx-life"), NumberStyles.Float, inv, out float lf) ? lf : -1f;
        _fov = float.TryParse(A("gfx-fov"), NumberStyles.Float, inv, out float fv) ? fv : 0f;
        _at.Sort();
        _atDef = new List<double>(_at);
        _evadeDef = _evadeAt; _runDef = _runAt; _runSpeedDef = _runSpeed; _hugDef = _hugAt; _lifeDef = _lifeFrac; _fovDef = _fov;
        _out = A("gfx-out");
        if (string.IsNullOrWhiteSpace(_out)) _out = @"Y:\shots\gfx";
        try { System.IO.Directory.CreateDirectory(_out); } catch (Exception e) { Log.Warn("gfx", "cannot create " + _out + ": " + e.Message); }
        _beauty = Has("gfx-beauty");
        _hideHud = A("gfx-hud") == "0";
        _fx = Has("gfx-fx");
        _hideHudDef = _hideHud; _fxDef = _fx; _botDef = Has("bot");
        _stay = Has("gfx-stay");
        _noVsync = Has("gfx-novsync");
        _sweep = Has("gfx-sweep");
        if (!string.IsNullOrWhiteSpace(A("gfx-theme"))) LevelBuilder.ThemeOverride = A("gfx-theme");      // --gfx-theme=rift: draw every stop with this theme
        if (float.TryParse(A("gfx-zoom"), NumberStyles.Float, inv, out float z)) _zoom = z;
        if (int.TryParse(A("gfx-q"), out int q)) Gfx.QualityOverride = Math.Clamp(q, 0, 3);
        if (float.TryParse(A("gfx-scale"), NumberStyles.Float, inv, out float sc)) Gfx.ScaleOverride = sc;
        if (int.TryParse(A("gfx-up"), out int up)) Gfx.UpscaleOverride = Math.Clamp(up, 0, 2);       // --gfx-up=0|1|2: FSR1+FXAA, TAA+FSR1, FSR2
        if (Has("gfx-bright")) EnvAtmosphere.Dark = false;                                            // --gfx-bright: the old, brighter level A (reference only)
        if (Has("gfx-dark")) EnvAtmosphere.Dark = true;                                               // --gfx-dark: the shipped dark level (already the default)
        if (Has("gfx-nocut")) EnvMats.WalkMaskOff = true;                                             // --gfx-nocut: indoor walls keep hiding floor (A/B of the floor cut)
        if (Has("gfx-trim")) LevelBuilder.TrimEnabled = true;                                         // --gfx-trim: release the caches of themes left behind (to be verified)
        _maskMode = Has("gfx-mask");                                                                  // --gfx-mask: also save <shot>_mask.png (hero green, other actors magenta)
        if (Has("gfx-gates")) WorldObjects.BuiltGateways = true;                                      // --gfx-gates: the built gateways with their veil (item 11; off by default until verified)
        if (Has("gfx-nogov")) Gfx.GovernorOff = true;                                                 // --gfx-nogov: keep the render scale fixed
        string res = A("gfx-res");
        if (!string.IsNullOrWhiteSpace(res))
        {
            var wh = res.ToLowerInvariant().Split('x');
            if (wh.Length == 2 && int.TryParse(wh[0], out int w) && int.TryParse(wh[1], out int h) && w >= 320 && h >= 180) _res = new Vector2I(w, h);
            else Log.Warn("gfx", $"--gfx-res: cannot read '{res}'");
        }
        Log.Info("gfx", $"probe: tour=[{string.Join(", ", _tour.Select(s => s.Area))}] at=[{string.Join(", ", _at.Select(v => v.ToString("0.#", inv)))}] out={_out} " +
            $"q={(Gfx.QualityOverride >= 0 ? Gfx.QualityOverride.ToString() : "saved")} res={(_res.X > 0 ? _res.X + "x" + _res.Y : "window")} beauty={_beauty} hud={!_hideHud} fx={_fx}");
    }

    public override void _Ready()
    {
        try
        {
            var root = GetTree().Root;
            if (_res.X > 0) BuildOffscreen(root);
            else _measureRid = root.GetViewportRid();
            RenderingServer.ViewportSetMeasureRenderTime(_measureRid, true);
            Gfx.Apply(Game.I.Cfg, _sub != null ? _sub : root);
        }
        catch (Exception e) { Log.Error("gfx", "probe start failed", e); }
    }

    /// <summary>The 3D world drawn by a second viewport of the requested size (same World3D), shown scaled under the HUD.</summary>
    private void BuildOffscreen(Window root)
    {
        _sub = new SubViewport
        {
            Name = "ProbeView", Size = _res, RenderTargetUpdateMode = SubViewport.UpdateMode.Always, HandleInputLocally = false,
            TransparentBg = false, PositionalShadowAtlasSize = 4096,
        };
        AddChild(_sub);
        _subCam = new Camera3D { Name = "ProbeCam", Current = true };
        _sub.AddChild(_subCam);
        // the lens (vignette) belongs to the picture: draw it inside the off-screen view and switch the window's own one off
        var rig = Game.I.Cam;
        if (rig != null)
        {
            var lens = new CanvasLayer { Name = "Lens", Layer = 0 };
            var rect = rig.NewLensRect();
            if (rect != null)
            {
                lens.AddChild(rect);
                _sub.AddChild(lens);
                rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                rig.LensVisible = false;
            }
        }
        root.Disable3D = true;
        var layer = new CanvasLayer { Name = "ProbeScreen", Layer = -50 };
        var tr = new TextureRect
        {
            Texture = _sub.GetTexture(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };
        layer.AddChild(tr);
        AddChild(layer);
        tr.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _measureRid = _sub.GetViewportRid();
        Gfx.GovernTarget = _sub;
        Log.Info("gfx", $"probe: 3D is rendered off screen at {_res.X}x{_res.Y}");
    }

    public override void _Process(double delta)
    {
        try { Tick(); }
        catch (Exception e)
        {
            if (!_errorLogged) { _errorLogged = true; Log.Error("gfx", "probe update failed", e); }
        }
    }

    private void Tick()
    {
        if (_freeze) Engine.TimeScale = 0.02;          // Game resets the time scale every frame; hold it while a mask is taken
        ulong now = Time.GetTicksUsec();
        double real = _lastTick == 0 ? 0 : (now - _lastTick) / 1e6;
        _lastTick = now;
        var g = Game.I;
        if (g == null || _finished) return;

        if (_subCam != null && g.Cam?.Cam != null && g.Cam.Cam.IsInsideTree())
        {
            var c = g.Cam.Cam;
            _subCam.GlobalTransform = c.GlobalTransform;
            _subCam.Fov = c.Fov; _subCam.Near = c.Near; _subCam.Far = c.Far; _subCam.HOffset = c.HOffset; _subCam.VOffset = c.VOffset;
        }
        if (_hideHud && g.UiLayer != null && g.State == Game.GameState.Play) g.UiLayer.Visible = _entering;   // keep the loading screen
        if (_noVsync && DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0;
        }

        bool playing = g.State == Game.GameState.Play && g.Level != null && IsInstanceValid(g.Level) && g.Player != null && IsInstanceValid(g.Player);
        if (_index < 0)
        {
            if (!playing) return;
            // first stop: the area the game started in when no tour was given
            if (_tour.Count == 0) _tour.Add(new Stop { Area = g.Level.AreaId });
            _index = 0;
            if (g.Level.AreaId == _tour[0].Area && !_tour[0].Opt.ContainsKey("class")) Arrive(g);
            else Travel(g);
            return;
        }
        if (_entering)
        {
            // wait until the game has left Play for the loading screen and come back with the new level
            _wait += real;
            if (playing && !ReferenceEquals(g.Level, _levelBefore) && _wait > 0.3) Arrive(g);
            else if (playing && _wait > 25) { Log.Warn("gfx", $"probe: '{_tour[_index].Area}' did not load (still in {g.Level.AreaId}); taking it as it is"); Arrive(g); }
            return;
        }
        if (!playing) return;

        _t += real;
        if (_zoom > 0.1f && g.Cam != null && Mathf.Abs(g.Cam.Zoom - _zoom) > 0.01f && _t < 1.5) g.Cam.Zoom = _zoom;
        if (_t > 1.5)
        {
            _frames++; _sumFrame += real; if (real > _worst) _worst = real;
            double gpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(_measureRid);
            double cpu = RenderingServer.ViewportGetMeasuredRenderTimeCpu(_measureRid);
            if (gpu > 0.01) { _sumGpu += gpu; _sumCpu += cpu; _gpuSamples++; }
            _drawSum += Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
            _primSum += Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        }
        if (_fx && !_fxDone && _t >= Math.Max(0.5, _at[0] - 1.6)) { _fxDone = true; Log.Guard("gfx", () => Gallery(g)); }
        Log.Guard("gfx", () => Staged(g));
        if (_focusHold is Vector3 fh) RenderingServer.GlobalShaderParameterSet("focus_pos", new Vector4(fh.X, fh.Y, fh.Z, 1f));
        Log.Guard("gfx", () => ClipViewer(g));
        while (_shot < _at.Count && _t >= _at[_shot])
        {
            Shoot(_tour[_index], _shot + 1);
            _shot++;
        }
        if (_shot >= _at.Count && _t >= _at[^1] + 0.7)
        {
            if (_sweep && !_sweepDone) { if (!_sweepLogged) Report(g); _sweepLogged = true; if (!SweepTick(g, real)) return; }
            else Report(g);
            _sweepDone = false; _sweepLogged = false; _sweepStep = -1;
            _index++;
            if (_index >= _tour.Count) { Finish(g); return; }
            Travel(g);
        }
    }

    private void Travel(Game g)
    {
        _areaNow = g.Level?.AreaId;
        _entering = true; _wait = 0;
        string area = _tour[_index].Area;
        _levelBefore = g.Level;
        if (_tour[_index].Opt.TryGetValue("class", out var cls) && cls.Length > 0)
        {
            // a new hero of another class, started in this stop's area (what the UI test layer's "newgame" does)
            int idx = Array.IndexOf(Names.ClassKeys, cls[0].ToLowerInvariant());
            int level = cls.Length > 1 && int.TryParse(cls[1], out int lv) ? lv : 30;
            if (idx >= 0)
            {
                _areaNow = g.Level?.AreaId;
                _entering = true; _wait = 0;
                if (g.UiLayer != null) g.UiLayer.Visible = true;
                g.Ui?.CloseAll();
                Log.Info("gfx", $"probe: new {cls[0]} (level {level}) in {area} ({_index + 1}/{_tour.Count})");
                g.HarnessNext(area, level, true, -1, true, cls.Length > 2 && int.TryParse(cls[2], out int pg) ? pg : -1);
                g.NewGame(g.Slot, "試煉者", (ClassId)idx, false);
                return;
            }
            Log.Warn("gfx", $"probe: unknown class '{cls[0]}'");
        }
        if (g.Level != null && g.Level.AreaId == area) { Arrive(g); return; }           // another view of the area we are in
        Log.Info("gfx", $"probe: travelling to {area} ({_index + 1}/{_tour.Count})");
        if (_hideHud && g.UiLayer != null) g.UiLayer.Visible = true;
        if (!g.HarnessTravel(area)) Log.Warn("gfx", $"probe: travel to {area} was refused");
    }

    private void Arrive(Game g)
    {
        _entering = false;
        _t = 0; _shot = 0; _fxDone = false;
        _frames = 0; _sumFrame = 0; _sumGpu = 0; _sumCpu = 0; _gpuSamples = 0; _worst = 0; _drawSum = 0; _primSum = 0;
        var stop = _tour[_index];
        if (string.IsNullOrEmpty(stop.Label))
        {
            string theme = g.Level?.Theme?.Id ?? stop.Area;
            stop.Label = _tour.Count(s => s != stop && s.Label == theme) > 0 ? theme + "_" + stop.Area.Replace(':', '-') : theme;
        }
        Gfx.Apply(g.Cfg, _sub != null ? _sub : GetTree().Root);
        if (g.Level?.Env?.Environment != null) Gfx.TuneEnvironment(g.Level.Env.Environment, g.Cfg);
        Log.Guard("gfx", () => ApplyStop(g, stop));
        Log.Guard("gfx", () => GoToPoi(g, stop));
        if (stop.Opt.TryGetValue("behind", out var beh)) Log.Guard("gfx", () => Place(g, "behind:" + string.Join(":", beh)));
        _focusHold = null;
        if (stop.Opt.TryGetValue("focusbehind", out var fb) && fb.Length > 0)
            Log.Guard("gfx", () =>
            {
                var data = g.Level?.Data; var pl = g.Player;
                if (data?.Props == null || pl == null) return;
                int nth = fb.Length > 1 && int.TryParse(fb[1], out int k) ? k : 1;
                var here = new Vector2(pl.GlobalPosition.X, pl.GlobalPosition.Z);
                var c = data.Props.Where(s => s != null && s.Category == fb[0]).OrderBy(s => s.Pos.DistanceSquaredTo(here)).ToList();
                if (c.Count == 0) return;
                var slot = c[Math.Clamp(nth, 0, c.Count - 1)];
                var at = slot.Pos + new Vector2(0f, -(Mathf.Max(slot.Radius, 0.4f) + 0.75f));
                _focusHold = new Vector3(at.X, 0f, at.Y);
                Fx.Telegraph(at, 0.7f, 40f, Palette.Element(Element.Lightning), true);
                Log.Info("gfx", $"probe: target cut held at ({at.X:0.0}, {at.Y:0.0}), {at.DistanceTo(here):0.0} m from the hero");
            });
        _evadeDone = false; _hugDone = false; _poseLeft = 0; _runOn = false; _hugTarget = null;
        Log.Info("gfx", $"probe: in {g.Level?.AreaId} (theme {g.Level?.Theme?.Id}) as '{stop.Label}'");
    }

    private void Report(Game g)
    {
        var inv = CultureInfo.InvariantCulture;
        var stop = _tour[_index];
        double frame = _frames > 0 ? _sumFrame / _frames * 1000.0 : 0, fps = frame > 0 ? 1000.0 / frame : 0;
        double gpu = _gpuSamples > 0 ? _sumGpu / _gpuSamples : 0, cpu = _gpuSamples > 0 ? _sumCpu / _gpuSamples : 0;
        string line = string.Format(inv, "{0,-11} fps={1,5:0.0} frame={2,5:0.0}ms worst={3,5:0.0}ms gpu={4,5:0.00}ms cpu={5,5:0.00}ms draws={6,5:0} prims={7,8:0} q={8} internal={9}x{10} scale={11:0.00} monsters={12}",
            stop.Label, fps, frame, _worst * 1000.0, gpu, cpu, _frames > 0 ? _drawSum / _frames : 0, _frames > 0 ? _primSum / _frames : 0,
            Gfx.Quality, Gfx.Internal.X, Gfx.Internal.Y, Gfx.Scale, World.Monsters.Count);
        Log.Info("gfx", "perf " + line);
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(_out, "perf.txt"), line + "\r\n", Encoding.UTF8); }
        catch (Exception e) { Log.Warn("gfx", "cannot write perf.txt: " + e.Message); }
    }

    private void Finish(Game g)
    {
        _finished = true;
        try { Task.WaitAll(_saves.ToArray(), 15000); } catch (Exception e) { Log.Warn("gfx", "waiting for probe shots: " + e.Message); }
        Log.Info("gfx", "probe: tour finished");
        if (_hideHud && g.UiLayer != null) g.UiLayer.Visible = true;
        if (!_stay) g.Quit();
    }

    /// <summary>
    /// --gfx-poi=kind[:tag][,n]: moves the hero to stand just south of the n-th point of interest of that kind
    /// (e.g. npc:smith, brazier, entrance, boss, waypoint), so a shot can be framed on a particular place.
    /// --gfx-xz=x,z moves the hero by that many metres from where the level put them.
    /// </summary>
    private static void GoToPoi(Game g, Stop stop)
    {
        var inv = CultureInfo.InvariantCulture;
        var p = g.Player;
        var data = g.Level?.Data;
        if (p == null || data == null) return;
        Vector2? goal = null;
        string spec = stop != null && stop.Opt.TryGetValue("poi", out var po) && po.Length > 0 ? string.Join(":", po) : A("gfx-poi");
        if (!string.IsNullOrWhiteSpace(spec))
        {
            int nth = 0;
            int comma = spec.IndexOf(',');
            if (comma > 0) { int.TryParse(spec[(comma + 1)..], out nth); spec = spec[..comma]; }
            string kind = spec, tag = null;
            int colon = spec.IndexOf(':');
            if (colon > 0) { kind = spec[..colon]; tag = spec[(colon + 1)..]; }
            var found = data.Pois.Where(q => q != null && q.Kind == kind && (tag == null || q.Tag == tag)).ToList();
            if (found.Count == 0) { Log.Warn("gfx", $"probe: no point of interest '{spec}' in this level"); }
            else goal = found[Math.Clamp(nth, 0, found.Count - 1)].Pos;
        }
        string xz = A("gfx-xz");
        if (!string.IsNullOrWhiteSpace(xz))
        {
            var parts = xz.Split(',');
            if (parts.Length == 2 && float.TryParse(parts[0], NumberStyles.Float, inv, out float dx) && float.TryParse(parts[1], NumberStyles.Float, inv, out float dz))
                goal = (goal ?? new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z)) + new Vector2(dx, dz);
        }
        if (goal == null) return;
        // stand a few metres south of it (towards the lens), on the nearest walkable spot
        var grid = g.Level.Grid;
        Vector2 best = goal.Value;
        var offsets = new List<Vector2> { new Vector2(0f, 3.2f), new Vector2(1.5f, 3f), new Vector2(-1.5f, 3f), new Vector2(0f, 2f), new Vector2(2.5f, 1f), new Vector2(-2.5f, 1f), new Vector2(0f, 4.5f), Vector2.Zero };
        // off:dx:dz — stand there instead (e.g. off:0:-3 = three metres north of it, so the thing itself is in the picture)
        if (stop != null && stop.Opt.TryGetValue("off", out var of) && of.Length >= 2 &&
            float.TryParse(of[0], NumberStyles.Float, inv, out float ox) && float.TryParse(of[1], NumberStyles.Float, inv, out float oz))
        {
            offsets.Insert(0, new Vector2(ox, oz));
            offsets.Insert(1, new Vector2(ox + 1.5f, oz)); offsets.Insert(2, new Vector2(ox - 1.5f, oz)); offsets.Insert(3, new Vector2(ox, oz * 0.6f));
        }
        foreach (var off in offsets)
            if (grid == null || grid.WalkAt(goal.Value + off)) { best = goal.Value + off; break; }
        p.Teleport(best);
        g.Cam?.Snap();
        Log.Info("gfx", $"probe: hero moved to ({best.X:0.0}, {best.Y:0.0})");
    }

    // ───────────── staged moments (graphics pass 0.3) ─────────────

    private double _evadeAt = -1, _hugAt = -1, _runAt = -1, _poseLeft;
    private float _lifeFrac = -1f, _fov, _runSpeed;
    private bool _evadeDone, _hugDone, _runOn;
    private Monster _hugTarget; private int _hugLog;

    // what the global switches said: a stop without its own option falls back to these
    private List<double> _atDef = new();
    private double _evadeDef = -1, _runDef = -1, _hugDef = -1;
    private float _runSpeedDef, _lifeDef = -1f, _fovDef;
    private bool _hideHudDef, _fxDef, _botDef;
    private Level _levelBefore;
    private string _clipSpec;
    private double _clipAt = -1;
    private static string _fxArg;
    private string[] _uiOpen;
    private Vector3? _focusHold;
    private bool _uiDone;

    /// <summary>Loads the options of a stop (see the header) into the fields the probe works with.</summary>
    private void ApplyStop(Game g, Stop stop)
    {
        var inv = CultureInfo.InvariantCulture;
        var o = stop.Opt;
        double D(string k, double def, int i = 0) => o.TryGetValue(k, out var a) && a.Length > i && double.TryParse(a[i], NumberStyles.Float, inv, out double v) ? v : def;
        _at.Clear();
        if (o.TryGetValue("at", out var at) && at.Length > 0)
            foreach (var part in at[0].Split('/', StringSplitOptions.RemoveEmptyEntries))
                if (double.TryParse(part, NumberStyles.Float, inv, out double v) && v >= 0) _at.Add(v);
        if (_at.Count == 0) _at.AddRange(_atDef);
        _at.Sort();
        _fx = o.ContainsKey("fx") || _fxDef;
        _fxArg = o.TryGetValue("fx", out var fa) && fa.Length > 0 ? fa[0] : A("gfx-fx");
        _hideHud = o.TryGetValue("hud", out var hu) && hu.Length > 0 ? hu[0] == "0" : _hideHudDef;
        if (!_hideHud && g.UiLayer != null) g.UiLayer.Visible = true;
        _evadeAt = D("evade", _evadeDef); _runAt = D("run", _runDef); _runSpeed = (float)D("run", _runSpeedDef, 1);
        _hugAt = D("hug", _hugDef); _lifeFrac = (float)D("life", _lifeDef); _fov = (float)D("fov", _fovDef);
        g.HarnessFov(_fov > 1f ? _fov : 0f);
        bool bot = o.ContainsKey("bot") || _botDef;
        if (bot && (g.Bot == null || !IsInstanceValid(g.Bot)))
        {
            g.Bot = new TestBot { Name = "TestBot" };
            g.AddChild(g.Bot);
        }
        if (g.Bot != null && IsInstanceValid(g.Bot))
        {
            g.Bot.SetProcess(bot);
            if (!bot)
            {
                // a bot stopped in mid-fight may leave a skill key down: let every key it uses go
                foreach (var key in new[] { Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Q, Key.Space, Key.Shift })
                    Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
                var pl = g.Player;
                if (pl != null && IsInstanceValid(pl)) pl.ForceHold = -1;
            }
        }
        Player.TestMouse = null; Player.TestLeft = false; Player.TestRight = false; Player.TestShift = false;
        _clips = null; _clipIndex = -1;
        _clipSpec = o.TryGetValue("clips", out var cl) && cl.Length > 0 ? cl[0].Replace('/', ',').Replace('*', '@') : null;
        _clipAt = D("clips", -1, 1);
        g.Ui?.CloseAll();
        _uiOpen = o.TryGetValue("ui", out var uo) && uo.Length > 0 ? uo : null;
        _uiDone = false;
    }

    /// <summary>Per-stop placement: behind:&lt;category&gt;[:n[:back[:side]]] puts the hero just north of the n-th nearest prop slot of that category (back / side in metres).</summary>
    private static void Place(Game g, string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return;
        var p = g.Player; var data = g.Level?.Data; var grid = g.Level?.Grid;
        if (p == null || data?.Props == null) return;
        var parts = spec.Split(':');
        if (parts[0] != "behind" || parts.Length < 2) { Log.Warn("gfx", $"probe: unknown placement '{spec}'"); return; }
        int nth = parts.Length > 2 && int.TryParse(parts[2], out int k) ? k : 0;
        float back = parts.Length > 3 && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float bk) ? bk : 0.75f;
        float side = parts.Length > 4 && float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float sd) ? sd : 0f;
        var here = new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z);
        var cands = data.Props.Where(s => s != null && s.Category == parts[1] && s.Tag is not ("house" or "hall" or "inn" or "forge" or "stall" or "tent"))
            .Select(s => (s, at: s.Pos + new Vector2(side, -(Mathf.Max(s.Radius, 0.4f) + back))))
            .Where(c => grid == null || grid.WalkAt(c.at))
            .OrderBy(c => c.s.Pos.DistanceSquaredTo(here)).ToList();
        if (cands.Count == 0) { Log.Warn("gfx", $"probe: no '{parts[1]}' slot to stand behind"); return; }
        var pick = cands[Math.Clamp(nth, 0, cands.Count - 1)];
        p.Teleport(pick.at);
        g.Cam?.Snap();
        Log.Info("gfx", $"probe: hero behind {parts[1]} #{nth} at ({pick.s.Pos.X:0.0}, {pick.s.Pos.Y:0.0}) r={pick.s.Radius:0.00} scale={pick.s.Scale:0.00}; standing at ({pick.at.X:0.0}, {pick.at.Y:0.0})");
    }

    private static int Bone(Skeleton3D sk, params string[] words)
    {
        for (int i = 0; i < sk.GetBoneCount(); i++)
        {
            string n = sk.GetBoneName(i).ToLowerInvariant();
            foreach (var w in words) if (n == w) return i;
        }
        for (int i = 0; i < sk.GetBoneCount(); i++)
        {
            string n = sk.GetBoneName(i).ToLowerInvariant();
            foreach (var w in words) if (n.Contains(w)) return i;
        }
        return -1;
    }

    /// <summary>The evade, the run, the low-life hold and the walk into a boss, each at its second of the stop.</summary>
    private void Staged(Game g)
    {
        var p = g.Player;
        if (p == null) return;
        var inv = CultureInfo.InvariantCulture;
        if (_lifeFrac >= 0f && !p.Dead) p.Life = Mathf.Max(1f, p.MaxLife * _lifeFrac);
        if (_uiOpen != null && !_uiDone && _t >= 1.5)
        {
            _uiDone = true;
            g.Ui?.Open(_uiOpen[0], _uiOpen.Length > 1 ? _uiOpen[1] : null);
        }
        var vp = GetTree().Root.GetVisibleRect().Size;
        if (_runAt >= 0 && _t >= _runAt && !_runOn)
        {
            _runOn = true;
            if (_runSpeed > 0f) g.Hero.AddBuff(new BuffDef { Id = "probe_run", Name = "probe", Stat = Stat.MoveSpeed, Value = _runSpeed }, 30f);
            Player.TestMouse = new Vector2(vp.X * 0.93f, vp.Y * 0.45f);
            Player.TestLeft = true;
            _poseLeft = 1.2;
            Log.Info("gfx", $"probe: run to screen-right (move speed +{_runSpeed:0}%)");
        }
        if (_evadeAt >= 0 && _t >= _evadeAt && !_evadeDone)
        {
            _evadeDone = true;
            Player.TestLeft = false;
            Player.TestMouse = new Vector2(vp.X * 0.93f, vp.Y * 0.45f);
            p.QueueEvade();
            _poseLeft = 0.9;
            Log.Info("gfx", "probe: evade to screen-right");
        }
        if (_hugAt >= 0 && _t >= _hugAt && !_hugDone)
        {
            _hugDone = true;
            Monster big = null; float best = 1e9f;
            foreach (var m in World.Monsters)
            {
                if (m == null || !IsInstanceValid(m) || m.Dead || m.Radius < 0.85f) continue;
                float d = m.Pos2.DistanceTo(p.Pos2);
                if (d < best) { best = d; big = m; }
            }
            if (big == null) Log.Warn("gfx", "probe: no boss or giant to walk into");
            else
            {
                if (big.Life >= big.MaxLife - 0.5f) big.Life = big.MaxLife - 2f;       // a boss behind its sealed door sleeps (no processing) until it is provoked
                p.Teleport(big.Pos2 + new Vector2(0.35f, 0.5f));
                g.Cam?.Snap();
                _hugTarget = big; _hugLog = 0;
                Log.Info("gfx", $"probe: hero put on top of {big.Def?.Id} (radius {big.Radius:0.00}, hero radius {p.Radius:0.00})");
            }
        }
        if (_hugTarget != null && IsInstanceValid(_hugTarget) && _hugLog < 6 && _t >= _hugAt + 0.25 * (_hugLog + 1))
        {
            _hugLog++;
            Log.Info("gfx", string.Format(inv, "probe: hug +{0:0.00}s distance={1:0.00} m (bodies touch at {2:0.00})", _t - _hugAt, _hugTarget.Pos2.DistanceTo(p.Pos2), _hugTarget.Radius + p.Radius));
        }
        if (_poseLeft > 0)
        {
            _poseLeft -= GetProcessDeltaTime();
            var cm = p.ActiveModel; var sk = cm?.Skeleton;
            if (sk != null)
            {
                int head = Bone(sk, "head"), hips = Bone(sk, "pelvis", "hips", "root");
                if (head >= 0 && hips >= 0)
                {
                    var a = sk.GlobalTransform * sk.GetBoneGlobalPose(hips).Origin; var b = sk.GlobalTransform * sk.GetBoneGlobalPose(head).Origin;
                    var up = b - a;
                    float tilt = up.LengthSquared() > 1e-6f ? Mathf.RadToDeg(up.Normalized().AngleTo(Vector3.Up)) : 0f;
                    Log.Info("gfx", string.Format(inv, "pose t={0:0.000} clip={1} at={2:0.00}s tilt={3:0}deg hipsY={4:0.00} headY={5:0.00} busy={6}", _t, cm.Current, cm.Anim != null ? cm.Anim.CurrentAnimationPosition : 0, tilt, a.Y, b.Y, p.Busy));
                }
            }
        }
    }

    // ───────────── clip viewer ─────────────

    private List<(string clip, float rate)> _clips;
    private int _clipIndex = -1, _clipShot;
    private double _clipT0;
    private static readonly double[] ClipShots = { 0.08, 0.2, 0.32, 0.5 };

    private void ClipViewer(Game g)
    {
        var p = g.Player;
        if (p == null) return;
        if (_clips == null)
        {
            _clips = new List<(string, float)>();
            string spec = _clipSpec ?? A("gfx-clips");
            if (!string.IsNullOrWhiteSpace(spec))
                foreach (var part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    int at = part.IndexOf('@');
                    float rate = at > 0 && float.TryParse(part[(at + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : 1f;
                    _clips.Add((at > 0 ? part[..at] : part, rate));
                }
        }
        if (_clips.Count == 0) return;
        double start = _clipAt >= 0 ? _clipAt : double.TryParse(A("gfx-clips-at"), NumberStyles.Float, CultureInfo.InvariantCulture, out double s0) ? s0 : 5.0;
        if (_t < start) return;
        var cm = p.ActiveModel;
        if (cm == null) return;
        if (_clipIndex < 0 || _t - _clipT0 > 0.75)
        {
            _clipIndex++;
            if (_clipIndex >= _clips.Count)
            {
                if (_clipIndex == _clips.Count) { p.SetProcess(true); Log.Info("gfx", "probe: clip viewer done"); }
                _clipIndex = _clips.Count + 1; _clipT0 = _t;
                return;
            }
            p.SetProcess(false);                              // its controller would put the idle clip back every frame
            p.Rotation = new Vector3(0f, Mathf.Pi * 0.5f, 0f);
            _clipT0 = _t; _clipShot = 0;
            cm.Play(_clips[_clipIndex].clip, _clips[_clipIndex].rate, 0.05f, true);
            Log.Info("gfx", $"probe: clip {_clips[_clipIndex].clip} at rate {_clips[_clipIndex].rate:0.##}, length {(cm.Anim != null && cm.Anim.HasAnimation(_clips[_clipIndex].clip) ? cm.Anim.GetAnimation(_clips[_clipIndex].clip).Length : 0):0.00} s");
            return;
        }
        if (_clipIndex < _clips.Count && _clipShot < ClipShots.Length && _t - _clipT0 >= ClipShots[_clipShot])
        {
            _clipShot++;
            var stop = new Stop { Label = $"clip_{_clips[_clipIndex].clip}_r{_clips[_clipIndex].rate:0.##}".Replace('.', 'p') };
            Shoot(stop, _clipShot);
        }
    }

    // ───────────── cost sweep ─────────────

    private bool _sweep, _sweepDone, _sweepLogged;
    private int _sweepStep = -1, _sweepFrames;
    private double _sweepT, _sweepGpu, _sweepLast;
    private readonly List<(Light3D light, bool shadow)> _sweepLights = new();

    private static IEnumerable<Node> Walk(Node n)
    {
        yield return n;
        foreach (var c in n.GetChildren())
            foreach (var d in Walk(c)) yield return d;
    }

    /// <summary>
    /// --gfx-sweep: after the shots of an area, switches the expensive features off one after the other (cumulative)
    /// and logs the GPU time of each state, so the cost of every feature is known. Returns true when it has finished.
    /// </summary>
    private bool SweepTick(Game g, double real)
    {
        var env = g.Level?.Env?.Environment;
        Viewport vp = _sub != null ? _sub : GetTree().Root;
        string[] names = { "all on", "-taa", "-fsr (bilinear)", "-mist", "-ssil", "-ssao", "-glow", "-omni shadows", "-sun shadows", "-particles+weather", "-sun+omni lights", "-half resolution" };
        if (_sweepStep < 0)
        {
            _sweepStep = 0; _sweepT = 0; _sweepGpu = 0; _sweepFrames = 0; _sweepLast = 0;
            _sweepLights.Clear();
            foreach (var n in Walk(g.Level)) if (n is Light3D l) _sweepLights.Add((l, l.ShadowEnabled));
            return false;
        }
        _sweepT += real;
        if (_sweepT > 1.0)
        {
            double gpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(_measureRid);
            if (gpu > 0.01) { _sweepGpu += gpu; _sweepFrames++; }
        }
        if (_sweepT < 3.2) return false;
        double avg = _sweepFrames > 0 ? _sweepGpu / _sweepFrames : 0;
        string line = string.Format(CultureInfo.InvariantCulture, "sweep {0,-11} {1,-20} gpu={2,6:0.00}ms  saved={3,6:0.00}ms", _tour[_index].Label, names[_sweepStep], avg, _sweepStep == 0 ? 0 : _sweepLast - avg);
        Log.Info("gfx", line);
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(_out, "perf.txt"), line + "\r\n", Encoding.UTF8); } catch { }
        _sweepLast = avg;
        _sweepStep++; _sweepT = 0; _sweepGpu = 0; _sweepFrames = 0;
        switch (_sweepStep)
        {
            case 1: vp.UseTaa = false; break;
            case 2: vp.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear; break;
            case 3: if (env != null) env.VolumetricFogEnabled = false; break;
            case 4: if (env != null) env.SsilEnabled = false; break;
            case 5: if (env != null) env.SsaoEnabled = false; break;
            case 6: if (env != null) env.GlowEnabled = false; break;
            case 7: foreach (var (l, _) in _sweepLights) if (IsInstanceValid(l) && l is OmniLight3D) l.ShadowEnabled = false; break;
            case 8: foreach (var (l, _) in _sweepLights) if (IsInstanceValid(l) && l is DirectionalLight3D) l.ShadowEnabled = false; break;
            case 9: foreach (var n in Walk(g.Level)) if (n is GpuParticles3D p) p.Visible = false; break;
            case 10: foreach (var (l, _) in _sweepLights) if (IsInstanceValid(l)) l.Visible = false; break;
            case 11: vp.Scaling3DScale = Mathf.Max(vp.Scaling3DScale * 0.5f, 0.25f); break;
            default:
                _sweepDone = true;
                return true;                 // the next area builds everything afresh; Gfx.Apply runs again on arrival
        }
        return false;
    }

    // ───────────── screenshots ─────────────

    private bool _maskMode, _freeze;
    private static StandardMaterial3D _maskHero, _maskOther;

    /// <summary>
    /// Actor mask for measurements: right after a shot the same view is drawn again with the hero flat green and every
    /// other character flat magenta (time almost stopped, temporal AA off), so a script can compare the brightness of
    /// figures with the ground around them.
    /// </summary>
    private async System.Threading.Tasks.Task ShootMask(string basePath)
    {
        var g = Game.I;
        if (g?.Level == null) return;
        static StandardMaterial3D Flat(Color c) => new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = c, DisableFog = true, DisableAmbientLight = true,
        };
        _maskHero ??= Flat(new Color(0f, 1f, 0f));
        _maskOther ??= Flat(new Color(1f, 0f, 1f));
        var models = new List<CharModel>();
        foreach (var node in Walk(g.Level)) if (node is CharModel cm) models.Add(cm);
        var root = GetTree().Root;
        bool taa = root.UseTaa;
        var heroModel = g.Player != null && IsInstanceValid(g.Player) ? g.Player.Model : null;
        try
        {
            _freeze = true;
            Engine.TimeScale = 0.02;
            root.UseTaa = false;
            foreach (var cm in models) if (IsInstanceValid(cm)) cm.SetMask(cm == heroModel ? _maskHero : _maskOther);
            for (int i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var img = root.GetTexture()?.GetImage();
            if (img != null && !img.IsEmpty()) Save(img, basePath + "_mask.png");
        }
        finally
        {
            foreach (var cm in models) if (IsInstanceValid(cm)) cm.SetMask(null);
            root.UseTaa = taa;
            _freeze = false;
            Engine.TimeScale = 1.0;
        }
    }

    private async void Shoot(Stop stop, int n)
    {
        try
        {
            string basePath = System.IO.Path.Combine(_out, $"{stop.Label}_{n}");
            if (_maskMode) { _freeze = true; Engine.TimeScale = 0.02; }      // hold still from the shot to its mask
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var img = GetTree().Root.GetTexture()?.GetImage();
            if (img != null && !img.IsEmpty()) Save(img, basePath + ".png");
            if (_maskMode) await ShootMask(basePath);
            if (_sub != null && _beauty)
            {
                var full = _sub.GetTexture()?.GetImage();
                if (full != null && !full.IsEmpty()) Save(full, basePath + "_full.png");
            }
        }
        catch (Exception e) { Log.Error("gfx", "probe screenshot failed", e); }
    }

    private void Save(Image img, string path)
    {
        _saves.RemoveAll(t => t.IsCompleted);
        _saves.Add(Task.Run(() =>
        {
            try
            {
                var err = img.SavePng(path);
                if (err != Error.Ok) Log.Error("gfx", $"probe shot not saved: {path} ({err})");
                else Log.Info("gfx", $"probe shot: {path}");
            }
            catch (Exception e) { Log.Error("gfx", $"probe shot failed: {path}", e); }
        }));
    }

    // ───────────── effect gallery ─────────────

    /// <summary>
    /// The ground sigils laid out for review, all filling together so one shot shows them at the same moment (the
    /// gallery appears 1.6 s before the first shot, which finds the marks half filled):
    ///   telec / telecf   circles of the eight schools, hostile / friendly      telek / telekf   cones
    ///   telel            lines      teler   rings (donuts)      telering   the expanding rings of Fx.Ring
    ///   telemix          a crowded floor: every shape, hostile and friendly, large and small
    ///   telestress       48 large marks over the whole view for 14 s (fill-rate measurement)
    /// Order of the schools: fire, frost, lightning, poison / shadow, blade, holy, arcane.
    /// </summary>
    private static void TeleGallery(Vector2 c, string what)
    {
        Sigil[] kinds = { Sigil.Fire, Sigil.Frost, Sigil.Lightning, Sigil.Poison, Sigil.Shadow, Sigil.Blade, Sigil.Holy, Sigil.Arcane };
        Color[] cols =
        {
            Palette.Element(Element.Fire), Palette.Element(Element.Frost), Palette.Element(Element.Lightning), Palette.Element(Element.Poison),
            Palette.Element(Element.Shadow), Palette.Element(Element.Physical), new Color(1f, 0.86f, 0.5f), new Color(0.7f, 0.6f, 1f),
        };
        const float fill = 3.2f;
        bool friendly = what.EndsWith('f');
        Vector2 up = new(0f, -1f);                                                    // up the screen
        Vector2 Cell(int i) => c + new Vector2(-6.3f + (i % 4) * 4.2f, i < 4 ? -7.1f : -2.7f);
        Color Tint(int i) => friendly ? cols[i] : Palette.Danger;
        switch (what.TrimEnd('f'))
        {
            case "telec":
                for (int i = 0; i < 8; i++) Fx.Telegraph(Cell(i), 1.95f, fill, Tint(i), friendly, kinds[i]);
                break;
            case "telek":
                for (int i = 0; i < 8; i++) Fx.TelegraphCone(Cell(i) - up * 1.7f, up, 3.1f, 84f, fill, Tint(i), friendly, kinds[i]);
                break;
            case "telel":
                for (int i = 0; i < 8; i++) Fx.TelegraphLine(c + new Vector2(-7.35f + i * 2.1f, -0.6f), up, 8.2f, 1.8f, fill, Palette.Danger, kinds[i]);
                break;
            case "teler":
                for (int i = 0; i < 8; i++) Fx.TelegraphRing(Cell(i), 0.85f, 1.95f, fill, Palette.Danger, kinds[i]);
                break;
            case "telering":
                for (int i = 0; i < 8; i++) Fx.Ring(World.To3(Cell(i), 0.1f), 1.9f, cols[i], 4.6f, kinds[i]);
                break;
            case "telestress":
                // 48 large, long-lived marks over the whole view: the worst case for the fill rate (read gpu ms in perf.txt)
                for (int i = 0; i < 48; i++)
                {
                    Vector2 at = c + new Vector2(-7.5f + (i % 8) * 2.15f, -9f + (i / 8) * 2.1f);
                    switch (i % 4)
                    {
                        case 0: Fx.Telegraph(at, 3.2f, 14f, Palette.Danger, kinds[i % 8]); break;
                        case 1: Fx.TelegraphCone(at, up.Rotated(i), 5f, 100f, 14f, Palette.Danger, false, kinds[i % 8]); break;
                        case 2: Fx.TelegraphLine(at, up.Rotated(i * 0.7f), 7f, 2.2f, 14f, Palette.Danger, kinds[i % 8]); break;
                        default: Fx.TelegraphRing(at, 1.4f, 3.4f, 14f, Palette.Danger, kinds[i % 8]); break;
                    }
                }
                break;
            default:
                // a crowded floor: what a boss room looks like when everything goes off at once
                Fx.TelegraphRing(c + new Vector2(0f, -4.6f), 2.2f, 5.4f, fill, Palette.Danger, Sigil.Frost);
                Fx.TelegraphCone(c + new Vector2(-7.4f, -1.2f), new Vector2(1f, -0.35f), 5.2f, 80f, fill * 0.9f, Palette.Danger, false, Sigil.Fire);
                Fx.TelegraphCone(c + new Vector2(7.6f, -8.2f), new Vector2(-1f, 0.5f), 4.6f, 110f, fill * 1.2f, Palette.Danger, false, Sigil.Blade);
                Fx.TelegraphLine(c + new Vector2(-7.6f, -9.4f), new Vector2(1f, 0.16f), 9f, 2.2f, fill * 1.1f, Palette.Danger, Sigil.Lightning);
                Fx.TelegraphLine(c + new Vector2(6.9f, 1.6f), new Vector2(-0.3f, -1f), 6.5f, 1.6f, fill * 0.8f, Palette.Danger, Sigil.Shadow);
                Fx.Telegraph(c + new Vector2(5.3f, -3.4f), 1.6f, fill * 1.3f, Palette.Danger, Sigil.Poison);
                Fx.Telegraph(c + new Vector2(-4.9f, -6.3f), 1.25f, fill * 0.7f, Palette.Danger, Sigil.Shadow);
                Fx.Telegraph(c + new Vector2(2.9f, 1.3f), 1.1f, fill, Palette.Danger);
                Fx.Telegraph(c + new Vector2(-3.4f, 0.9f), 0.8f, fill * 1.4f, Palette.Danger, Sigil.Arcane);
                Fx.Telegraph(c + new Vector2(-2.3f, -2.9f), 1.7f, fill, cols[2], true, Sigil.Lightning);
                Fx.Telegraph(c + new Vector2(2.3f, -8.6f), 1.5f, fill * 1.2f, cols[6], true, Sigil.Holy);
                Fx.TelegraphCone(c, up, 3.2f, 90f, fill, cols[0], true, Sigil.Fire);
                break;
        }
    }

    /// <summary>Lays out one of every ground effect round the hero so a single screenshot reviews them all.</summary>
    private static void Gallery(Game g)
    {
        var p = g.Player;
        var root = World.Root;
        if (p == null || root == null) return;
        Vector2 c = new(p.GlobalPosition.X, p.GlobalPosition.Z);
        string what = _fxArg ?? A("gfx-fx");
        if (what != null && what.StartsWith("tele")) { TeleGallery(c, what); return; }
        Element[] els = { Element.Fire, Element.Frost, Element.Lightning, Element.Poison, Element.Shadow, Element.Physical };

        void Zone(Vector2 at, float r, Element el, bool hostile)
        {
            var n = new Node3D { Name = "ProbeZone" };
            root.AddChild(n);
            n.GlobalPosition = new Vector3(at.X, 0.06f, at.Y);
            n.AddChild(Fx.ZoneVisual(r, el, hostile, 9f));
        }

        // friendly zones in a row up-screen, hostile ones beside the hero
        for (int i = 0; i < els.Length; i++) Zone(c + new Vector2(-8.75f + i * 3.5f, -6.2f), 1.6f, els[i], false);
        Zone(c + new Vector2(-6.2f, -1.2f), 2.3f, Element.Fire, true);
        Zone(c + new Vector2(6.2f, -1.2f), 2.3f, Element.Poison, true);
        Zone(c + new Vector2(-6.4f, 3.2f), 1.9f, Element.Frost, true);
        Zone(c + new Vector2(6.4f, 3.2f), 1.9f, Element.Shadow, true);

        // telegraphs at different fill levels
        Fx.Telegraph(c + new Vector2(-2.6f, -2.6f), 1.5f, 2.6f, Palette.Danger);
        Fx.TelegraphRing(c + new Vector2(2.6f, -2.6f), 0.8f, 1.7f, 3.4f, Palette.Danger);
        Fx.TelegraphLine(c + new Vector2(-3.5f, 2.4f), new Vector2(1f, 0.25f), 6.5f, 1.3f, 2.2f, Palette.Danger);
        Fx.Telegraph(c + new Vector2(0f, 3.6f), 1.2f, 4f, Palette.Element(Element.Frost), true);
        Fx.TelegraphCone(p, 4f, 70f, 3f, Palette.Danger);

        // loot beams by rarity and the big moments
        var rar = (Rarity[])Enum.GetValues(typeof(Rarity));
        for (int i = 0; i < rar.Length; i++)
        {
            var beam = Fx.LootBeam(rar[i]);
            if (beam == null) continue;
            root.AddChild(beam);
            beam.GlobalPosition = new Vector3(c.X - 4.5f + i * 1.8f, 0.05f, c.Y - 3.9f);
        }
        Fx.Burst(new Vector3(c.X + 3.4f, 0.8f, c.Y + 1.2f), Element.Fire, 2.2f, 1.2f);
        Fx.Burst(new Vector3(c.X - 3.4f, 0.8f, c.Y + 0.8f), Element.Lightning, 1.8f, 1f);
        Fx.Hit(new Vector3(c.X + 1.2f, 1.2f, c.Y + 0.6f), Element.Physical, true);
        Fx.Meteor(c + new Vector2(4.6f, -4.2f), 1.5f, Element.Fire, 2f);

        // shots hanging in the air: the hero's (near row) and the enemy's (far row), small and big alternating
        for (int i = 0; i < els.Length; i++)
            for (int h = 0; h < 2; h++)
            {
                var holder = new Node3D { Name = "ProbeShot" };
                root.AddChild(holder);
                holder.GlobalPosition = new Vector3(c.X - 3.75f + i * 1.5f, 1.1f, c.Y + 2.2f - h * 1.3f);
                holder.Rotation = new Vector3(0f, Mathf.Pi * 0.5f, 0f);
                holder.AddChild(Fx.ProjectileVisual(els[i], i % 2, 0.4f, "", h == 1));
            }
        if ((_fxArg ?? A("gfx-fx")) == "2") { Fx.LevelUp(p); Fx.Heal(p); }
    }
}
