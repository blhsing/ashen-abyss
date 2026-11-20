using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// The isometric follow camera. The rig node sits on the smoothed focus point (the hero, about chest height); the camera
/// hangs behind it at a fixed pitch of 48° with a 32° lens, 15.6 m × <see cref="Zoom"/> away, which puts a 1.8 m hero at
/// about 13.5 % of the frame height at zoom 1 (19 % fully zoomed in, 8.5 % fully out; bosses pull back to about 9 %).
/// It looks along −Z so that W/A/S/D map to screen directions. Mouse wheel zooms while the pointer is not over the UI,
/// shakes are decaying noise, and the view leans a little toward the cursor so ranged heroes see what they aim at.
/// The rig also owns the two things that belong to the lens rather than to a level: a soft vignette under the HUD and
/// a cool, shadowless rim light that follows the focus from behind, so the hero and whatever fights next to them
/// always separate from the ground.
/// </summary>
public partial class CameraRig : Node3D
{
    public const float MinZoom = 0.7f, MaxZoom = 1.6f, PitchDeg = 48f, FovDeg = 32f, BaseDistance = 15.6f, FocusHeight = 1.0f;
    /// <summary>The focus sits this far south of the hero (towards the camera): perspective shows far more ground up-screen than down-screen, and the action bar covers the bottom edge.</summary>
    public const float FocusBias = 0.9f;
    /// <summary>Largest value <see cref="Zoom"/> can take (wheel × scripted pull-back). Level code sizes its culling and shadows for it.</summary>
    public const float ZoomCeiling = 1.8f;

    public Camera3D Cam;
    /// <summary>Current zoom factor (0.7 … 1.6 from the mouse wheel, multiplied by any scripted zoom). Writing it sets the player's preferred zoom.</summary>
    public float Zoom = 1f;
    /// <summary>Lean the view toward the mouse cursor.</summary>
    public bool LookAhead = true;
    public float LookAheadGain = 0.1f, LookAheadMax = 1.8f;
    /// <summary>The smoothed point on the ground plane the camera is centred on.</summary>
    public Vector2 Focus => new(_focus.X, _focus.Z);
    /// <summary>Strength of the vignette (0 = off). Level themes may change it; 0.42 is the default.</summary>
    public float Vignette { get => _vignette; set { _vignette = Mathf.Clamp(value, 0f, 1f); ApplyVignette(); } }
    /// <summary>Colour the vignette darkens towards (the deepest shadow tone of the region).</summary>
    public Color VignetteTint { get => _vignetteTint; set { _vignetteTint = value; ApplyVignette(); } }
    /// <summary>The rim light that follows the focus (colour and energy are set per level by the environment builder).</summary>
    public SpotLight3D Rim;

    private Node3D _target;
    private Vector3 _focus = new(0, FocusHeight, FocusBias);
    private Vector3 _lean;
    private float _user = 1f, _script = 1f, _scriptGoal = 1f, _scriptRate, _zoomNow = 1f, _lastZoom = 1f;
    private float _shakeAmp, _shakeDur, _shakeLeft, _shakeClock;
    private float _vignette = 0.42f;
    private Color _vignetteTint = new(0.01f, 0.012f, 0.02f);
    private ulong _lastUsec;
    private ShaderMaterial _vignetteMat;
    private CanvasLayer _lensLayer;
    private readonly FastNoiseLite _noise = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 1f, FractalType = FastNoiseLite.FractalTypeEnum.None, Seed = 7 };
    private static readonly float PitchSin = Mathf.Sin(Mathf.DegToRad(PitchDeg)), PitchCos = Mathf.Cos(Mathf.DegToRad(PitchDeg));

    private const string LensShader = @"shader_type canvas_item;
render_mode blend_mix, unshaded;
uniform vec3 tint = vec3(0.01, 0.012, 0.02);
uniform float strength = 0.42;
uniform float inner = 0.42;
uniform float outer = 1.08;
void fragment() {
	vec2 p = (UV - 0.5) * vec2(1.0, 0.82);
	float d = length(p) * 2.0;
	float v = smoothstep(inner, outer, d);
	float n = fract(sin(dot(FRAGCOORD.xy + fract(TIME) * 61.0, vec2(12.9898, 78.233))) * 43758.5453);
	COLOR = vec4(tint, clamp(v * v * strength + (n - 0.5) * 0.012, 0.0, 1.0));
}";

    /// <summary>Metres of ground the camera shows from the hero to the farthest corner of the frame, at a zoom factor.</summary>
    public static float ViewReach(float zoom)
    {
        // camera height and the ray through the top edge of a 16:9 frame (pitch − fov/2 below the horizon), widened to the corner
        float dist = BaseDistance * zoom;
        float h = PitchSin * dist + FocusHeight;
        float top = Mathf.DegToRad(PitchDeg - FovDeg * 0.5f);
        float ahead = h / Mathf.Tan(top) - PitchCos * dist;             // up-screen from the focus
        float halfW = (h / Mathf.Sin(top)) * Mathf.Tan(Mathf.DegToRad(FovDeg * 0.5f)) * (16f / 9f);
        return Mathf.Sqrt(ahead * ahead + halfW * halfW) + FocusBias * zoom;
    }

    /// <summary>Distance from the camera to the farthest ground it shows at a zoom factor (for shadow and fog ranges).</summary>
    public static float FarGround(float zoom)
    {
        float dist = BaseDistance * zoom;
        float h = PitchSin * dist + FocusHeight;
        float top = Mathf.DegToRad(PitchDeg - FovDeg * 0.5f);
        float slant = h / Mathf.Sin(top);
        float halfW = slant * Mathf.Tan(Mathf.DegToRad(FovDeg * 0.5f)) * (16f / 9f);
        return Mathf.Sqrt(slant * slant + halfW * halfW);
    }

    public CameraRig()
    {
        Name = "CameraRig";
        ProcessPriority = 1000;          // after every actor has moved this frame
        Cam = new Camera3D { Name = "Camera", Fov = FovDeg, Near = 0.4f, Far = 220f, Current = true };
        AddChild(Cam);

        // Back light: north of the focus and above it, aimed back at it. No shadows and a tight cone, so it only rims
        // what stands near the hero and lays a cool specular streak on wet ground towards the lens.
        Rim = new SpotLight3D
        {
            Name = "Rim", LightColor = new Color(0.62f, 0.74f, 1f), LightEnergy = 0f, SpotRange = 16f, SpotAngle = 34f, SpotAngleAttenuation = 1.4f,
            SpotAttenuation = 0.8f, ShadowEnabled = false, LightSpecular = 0.12f, LightVolumetricFogEnergy = 0f,
            LightBakeMode = Light3D.BakeMode.Disabled, Visible = false,
        };
        AddChild(Rim);
        var rimPos = new Vector3(-2.2f, 6.2f, -7.2f - FocusBias);
        var rimAim = new Vector3(0f, 0.4f, -FocusBias);
        Rim.Transform = new Transform3D(Basis.LookingAt((rimAim - rimPos).Normalized(), Vector3.Up), rimPos);
        Place();
    }

    // The audio listener is not here: AudioMan keeps its own just above the hero, which is what positional sound is tuned for.
    public override void _Ready()
    {
        Cam.MakeCurrent();
        try { BuildLens(); }
        catch (Exception e) { Log.Error("camera", "building the vignette failed", e); }
    }

    /// <summary>
    /// One full-screen rect under the HUD (UiLayer is layer 1) and over the 3D picture: the vignette plus a breath of
    /// grain that keeps dark gradients from banding.
    /// </summary>
    private void BuildLens()
    {
        _vignetteMat = new ShaderMaterial { Shader = new Shader { Code = LensShader } };
        _lensLayer = new CanvasLayer { Name = "Lens", Layer = 0 };
        var rect = new ColorRect { Name = "Vignette", Color = Colors.White, MouseFilter = Control.MouseFilterEnum.Ignore, Material = _vignetteMat };
        _lensLayer.AddChild(rect);
        AddChild(_lensLayer);
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ApplyVignette();
    }

    /// <summary>Another full-screen rect with the same lens material, for a viewport that draws the 3D picture on the camera's behalf (test harness).</summary>
    public ColorRect NewLensRect() =>
        _vignetteMat == null ? null : new ColorRect { Name = "Vignette", Color = Colors.White, MouseFilter = Control.MouseFilterEnum.Ignore, Material = _vignetteMat };

    /// <summary>Shows or hides the rig's own lens layer.</summary>
    public bool LensVisible
    {
        get => _lensLayer != null && _lensLayer.Visible;
        set { if (_lensLayer != null) _lensLayer.Visible = value; }
    }

    private void ApplyVignette()
    {
        if (_vignetteMat == null) return;
        _vignetteMat.SetShaderParameter("strength", _vignette);
        _vignetteMat.SetShaderParameter("tint", new Vector3(_vignetteTint.R, _vignetteTint.G, _vignetteTint.B));
    }

    /// <summary>Sets the back light for a level: energy 0 switches it off.</summary>
    public void SetRim(Color color, float energy)
    {
        if (Rim == null) return;
        Rim.LightColor = color;
        Rim.LightEnergy = Mathf.Max(energy, 0f);
        Rim.Visible = energy > 0.01f;
    }

    // ───────────────────────── API ─────────────────────────

    public void Follow(Node3D target) => _target = target;

    /// <summary>Jumps straight to the target without easing (area changes, respawns).</summary>
    public void Snap()
    {
        if (TargetPoint(out var want)) _focus = want;
        _lean = Vector3.Zero;
        _script = _scriptGoal;
        _zoomNow = Mathf.Clamp(_user * _script, 0.6f, ZoomCeiling);
        Zoom = _lastZoom = _zoomNow;
        Place();
    }

    /// <summary>Starts a decaying noise shake. Amount is roughly metres of sway at the focus point (0.15 light … 0.6 heavy).</summary>
    public void Shake(float amount, float seconds)
    {
        if (amount <= 0f || seconds <= 0f) return;
        var cfg = Game.I?.Cfg;
        if (cfg != null && !cfg.ScreenShake) return;
        amount = Mathf.Min(amount, 1.2f) * 0.7f;        // the lens is closer than the effects were authored for
        float current = _shakeLeft > 0f && _shakeDur > 0f ? _shakeAmp * (_shakeLeft / _shakeDur) * (_shakeLeft / _shakeDur) : 0f;
        if (amount >= current) { _shakeAmp = amount; _shakeDur = seconds; _shakeLeft = seconds; }
    }

    /// <summary>Scripted zoom on top of the player's own wheel zoom: 1 = neutral, 1.5 pulls back for bosses, 0.85 closes in for town.</summary>
    public void SetZoomTarget(float zoom, float seconds)
    {
        _scriptGoal = Mathf.Clamp(zoom, 0.5f, 2f);
        if (seconds <= 0.01f) { _script = _scriptGoal; _scriptRate = 0f; }
        else _scriptRate = Mathf.Abs(_scriptGoal - _script) / seconds;
    }

    // ───────────────────────── input ─────────────────────────

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventMouseButton mb || !mb.Pressed) return;
        if (mb.ButtonIndex != MouseButton.WheelUp && mb.ButtonIndex != MouseButton.WheelDown) return;
        var g = Game.I;
        if (g == null || g.State != Game.GameState.Play || g.UiBlocksMouse) return;
        float step = 0.08f * (mb.Factor > 0f ? Mathf.Clamp(mb.Factor, 0.25f, 3f) : 1f);
        _user = Mathf.Clamp(_user + (mb.ButtonIndex == MouseButton.WheelUp ? -step : step), MinZoom, MaxZoom);
    }

    // ───────────────────────── frame ─────────────────────────

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        try
        {
            // an outside write to Zoom is taken as the player's preferred zoom
            if (!Mathf.IsEqualApprox(Zoom, _lastZoom)) _user = Mathf.Clamp(Zoom / Mathf.Max(0.2f, _script), MinZoom, MaxZoom);
            if (!Mathf.IsEqualApprox(_script, _scriptGoal)) _script = Mathf.MoveToward(_script, _scriptGoal, Mathf.Max(_scriptRate, 0.05f) * dt);
            float wantZoom = Mathf.Clamp(_user * _script, 0.6f, ZoomCeiling);
            _zoomNow = Mathf.Lerp(_zoomNow, wantZoom, 1f - Mathf.Exp(-9f * dt));
            Zoom = _lastZoom = _zoomNow;

            if (TargetPoint(out var want))
            {
                float gap = _focus.DistanceTo(want);
                if (gap > 22f) _focus = want;                                  // travelled across the map: cut, do not pan
                else
                {
                    _focus = _focus.Lerp(want, 1f - Mathf.Exp(-10f * dt));
                    gap = _focus.DistanceTo(want);
                    if (gap > 6f) _focus = want + (_focus - want) / gap * 6f;   // never let the hero slide off screen after a blink
                }
            }

            Vector3 lean = Vector3.Zero;
            if (LookAhead && CanLean() && CursorOnGround(out var cursor))
            {
                var off = new Vector3(cursor.X - _focus.X, 0f, cursor.Y - _focus.Z) * LookAheadGain;
                float len = off.Length();
                if (len > LookAheadMax) off *= LookAheadMax / len;
                lean = off;
            }
            _lean = _lean.Lerp(lean, 1f - Mathf.Exp(-4f * dt));

            Place();
            ApplyShake(dt);
            UpdateFocusCut(dt);

            // the automatic render scale follows what the lens is asked to draw
            ulong now = Time.GetTicksUsec();
            double real = _lastUsec == 0 ? 0.0 : (now - _lastUsec) / 1e6;
            _lastUsec = now;
            var game = Game.I;
            if (game != null && game.State == Game.GameState.Play) Gfx.Govern(real, GetViewport(), game.Cfg);
        }
        catch (Exception e) { Log.Error("camera", "camera update failed", e); }
    }

    private static readonly StringName FocusGlobal = "focus_pos";      // [shader_globals] in project.godot
    private Vector3 _cutAt;
    private float _cutW;

    /// <summary>
    /// The enemy under the cursor or being attacked is cut free of tall scenery the way the hero is: its position and
    /// a 0..1 weight go to the global shader uniform focus_pos (read by env_prop and env_wall). The weight eases in
    /// and out, and the point glides between neighbouring targets instead of jumping.
    /// </summary>
    private void UpdateFocusCut(float dt)
    {
        Actor want = null;
        var g = Game.I;
        var p = g != null && g.State == Game.GameState.Play ? g.Player : null;
        if (p != null && IsInstanceValid(p) && p.IsInsideTree() && !p.Dead) want = p.AttackTarget ?? p.Hover;
        if (want != null && (!IsInstanceValid(want) || !want.IsInsideTree() || want.Dead)) want = null;
        if (want != null)
        {
            var at = want.GlobalPosition;
            if (_cutW < 0.02f || at.DistanceSquaredTo(_cutAt) > 36f) _cutAt = at;
            else _cutAt = _cutAt.Lerp(at, 1f - Mathf.Exp(-14f * dt));
        }
        float w = Mathf.MoveToward(_cutW, want != null ? 1f : 0f, dt * (want != null ? 5f : 2.5f));
        if (w <= 0f && _cutW <= 0f) return;
        _cutW = w;
        RenderingServer.GlobalShaderParameterSet(FocusGlobal, new Vector4(_cutAt.X, _cutAt.Y, _cutAt.Z, _cutW));
    }

    private bool TargetPoint(out Vector3 p)
    {
        p = default;
        if (_target == null) return false;
        if (!IsInstanceValid(_target)) { _target = null; return false; }
        if (!_target.IsInsideTree()) return false;
        var g = _target.GlobalPosition;
        p = new Vector3(g.X, FocusHeight, g.Z + FocusBias * _zoomNow);      // the ground plane, so jumps and leaps do not bob the view
        return true;
    }

    private static bool CanLean()
    {
        var g = Game.I;
        return g != null && g.State == Game.GameState.Play && g.PauseLocks == 0 && !g.UiBlocksMouse && DisplayServer.WindowIsFocused();
    }

    private bool CursorOnGround(out Vector2 hit)
    {
        hit = default;
        var vp = GetViewport();
        if (vp == null || !Cam.IsInsideTree()) return false;
        Vector2 mp = vp.GetMousePosition();
        if (!vp.GetVisibleRect().HasPoint(mp)) return false;
        Vector3 o = Cam.ProjectRayOrigin(mp), d = Cam.ProjectRayNormal(mp);
        if (d.Y > -1e-3f) return false;
        float t = -o.Y / d.Y;
        if (t <= 0f || t > 400f) return false;
        hit = new Vector2(o.X + d.X * t, o.Z + d.Z * t);
        return true;
    }

    private void Place()
    {
        Position = _focus + _lean;
        float dist = BaseDistance * _zoomNow;
        Cam.Position = new Vector3(0f, PitchSin * dist, PitchCos * dist);
        Cam.RotationDegrees = new Vector3(-PitchDeg, 0f, 0f);
    }

    private void ApplyShake(float dt)
    {
        if (_shakeLeft <= 0f)
        {
            if (Cam.HOffset != 0f || Cam.VOffset != 0f) { Cam.HOffset = 0f; Cam.VOffset = 0f; }
            return;
        }
        // real time: a shake must die down at the same pace during hit-stop slow motion
        float real = Engine.TimeScale > 0.01 ? dt / (float)Engine.TimeScale : dt;
        _shakeLeft -= real;
        _shakeClock += real;
        if (_shakeLeft <= 0f) { _shakeLeft = 0f; Cam.HOffset = 0f; Cam.VOffset = 0f; return; }
        float k = _shakeLeft / _shakeDur;
        float amp = _shakeAmp * k * k * Mathf.Lerp(0.8f, 1.2f, Mathf.InverseLerp(0.6f, ZoomCeiling, _zoomNow));
        float t = _shakeClock * 26f;
        Cam.HOffset = _noise.GetNoise2D(t, 11.3f) * amp;
        Cam.VOffset = _noise.GetNoise2D(47.9f, t) * amp;
        Cam.RotationDegrees = new Vector3(-PitchDeg, 0f, _noise.GetNoise2D(t, 93.1f) * amp * 1.6f);
    }
}
