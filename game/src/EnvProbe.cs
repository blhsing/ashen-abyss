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
///   --gfx-stay               do not quit after the last area
/// </summary>
public partial class EnvProbe : Node
{
    private sealed class Stop { public string Label, Area; }

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
                _tour.Add(new Stop { Label = label, Area = area });
            }
        string at = A("gfx-at");
        if (!string.IsNullOrWhiteSpace(at))
            foreach (var part in at.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (double.TryParse(part, NumberStyles.Float, inv, out double v) && v >= 0) _at.Add(v);
        if (_at.Count == 0) { _at.Add(5); _at.Add(12); }
        _at.Sort();
        _out = A("gfx-out");
        if (string.IsNullOrWhiteSpace(_out)) _out = @"Y:\shots\gfx";
        try { System.IO.Directory.CreateDirectory(_out); } catch (Exception e) { Log.Warn("gfx", "cannot create " + _out + ": " + e.Message); }
        _beauty = Has("gfx-beauty");
        _hideHud = A("gfx-hud") == "0";
        _fx = Has("gfx-fx");
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
            if (g.Level.AreaId == _tour[0].Area) Arrive(g);
            else Travel(g);
            return;
        }
        if (_entering)
        {
            // wait until the game has left Play for the loading screen and come back with the new level
            _wait += real;
            if (playing && g.Level.AreaId != _areaNow && _wait > 0.3) Arrive(g);
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
        Log.Guard("gfx", () => GoToPoi(g));
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
    private static void GoToPoi(Game g)
    {
        var inv = CultureInfo.InvariantCulture;
        var p = g.Player;
        var data = g.Level?.Data;
        if (p == null || data == null) return;
        Vector2? goal = null;
        string spec = A("gfx-poi");
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
        foreach (var off in new[] { new Vector2(0f, 3.2f), new Vector2(1.5f, 3f), new Vector2(-1.5f, 3f), new Vector2(0f, 2f), new Vector2(2.5f, 1f), new Vector2(-2.5f, 1f), new Vector2(0f, 4.5f), Vector2.Zero })
            if (grid == null || grid.WalkAt(goal.Value + off)) { best = goal.Value + off; break; }
        p.Teleport(best);
        g.Cam?.Snap();
        Log.Info("gfx", $"probe: hero moved to ({best.X:0.0}, {best.Y:0.0})");
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

    /// <summary>Lays out one of every ground effect round the hero so a single screenshot reviews them all.</summary>
    private static void Gallery(Game g)
    {
        var p = g.Player;
        var root = World.Root;
        if (p == null || root == null) return;
        Vector2 c = new(p.GlobalPosition.X, p.GlobalPosition.Z);
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
        if (A("gfx-fx") == "2") { Fx.LevelUp(p); Fx.Heal(p); }
    }
}
