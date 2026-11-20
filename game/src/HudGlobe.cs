using Godot;
using System;

namespace AshenAbyss;

/// <summary>Canvas shaders of the HUD, kept as source text so the module needs no files outside its own.</summary>
internal static class HudShaders
{
    // value noise + the forged-iron ring with two ember-gold trims shared by the globes and the minimap
    private const string Common = @"
// arithmetic hash (no sin): the sin-based one broke into flat blocks on some GPUs
float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.x, p.y, p.x) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}
float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}
vec3 iron_ring(vec2 p, float r, float px) {
	float bevel = clamp((r - R) / (1.0 - R), 0.0, 1.0);
	float lit = 0.5 - 0.5 * (p.x * 0.55 + p.y * 0.83);
	vec3 iron = vec3(0.105, 0.098, 0.095) * (0.55 + 0.9 * sin(bevel * 3.14159) * (0.35 + lit));
	iron += vec3(0.02) * vnoise(p * 34.0);
	vec3 gold = vec3(0.88, 0.64, 0.30) * (0.55 + 0.75 * lit);
	float t1 = 1.0 - smoothstep(0.004, 0.004 + px, abs(r - (R + 0.022)));
	float t2 = 1.0 - smoothstep(0.003, 0.003 + px, abs(r - 0.968));
	return mix(iron, gold, max(t1, t2 * 0.75));
}
";

    private const string GlobeCode = @"shader_type canvas_item;
uniform float fill = 1.0;
uniform float ghost = 1.0;
uniform float barrier = 0.0;
uniform float flash = 0.0;
uniform float pulse = 0.0;
uniform float phase = 0.0;
uniform vec4 liquid : source_color = vec4(0.72, 0.06, 0.05, 1.0);
uniform vec4 shell : source_color = vec4(0.78, 0.9, 1.0, 1.0);
uniform float art = 0.0;
const float R = 0.84;
" + Common + @"
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float px = fwidth(r) * 1.25;
	float y = -p.y;
	float t = TIME + phase;
	float z = sqrt(max(0.0, 1.0 - (r / R) * (r / R)));
	float lvl = mix(-R, R, clamp(fill, 0.0, 1.0));
	float calm = smoothstep(0.0, 0.05, fill) * (1.0 - smoothstep(0.97, 1.0, fill));
	float surf = lvl + (sin(p.x * 7.0 + t * 1.9) * 0.016 + sin(p.x * 13.0 - t * 2.7) * 0.008) * calm;
	float liq = (1.0 - smoothstep(surf - 0.012, surf + 0.012, y)) * step(0.002, fill);
	float gl = mix(-R, R, clamp(ghost, 0.0, 1.0));
	float gm = (1.0 - smoothstep(gl - 0.012, gl + 0.012, y)) * (1.0 - liq) * step(0.002, ghost);
	// the noise lattice is kept near the origin (mod) so its cells never lose precision as time runs on
	float t1 = mod(t, 512.0);
	float n = vnoise(vec2(p.x * 3.2 + t1 * 0.12 + 40.0, y * 3.2 - t1 * 0.33 + 300.0)) * 0.55
		+ vnoise(vec2(p.x * 7.5 - t1 * 0.2 + 90.0, y * 7.5 - t1 * 0.55 + 500.0)) * 0.3
		+ vnoise(vec2(p.x * 15.0 + t1 * 0.31 + 20.0, y * 15.0 - t1 * 0.8 + 900.0)) * 0.15;
	vec3 body = mix(liquid.rgb * 0.22, liquid.rgb, clamp(0.3 + 0.7 * z, 0.0, 1.0)) * (0.78 + 0.5 * n);
	body += liquid.rgb * exp(-max(surf - y, 0.0) * 20.0) * 0.5;
	body += vec3(1.0, 0.35, 0.25) * pulse * 0.35 * (0.5 + 0.5 * sin(TIME * 6.0));
	// empty glass keeps a breath of the liquid's colour, strongest toward the rim: the globe always reads as this class's
	vec3 glass = vec3(0.026, 0.024, 0.03) + liquid.rgb * (0.05 * z + 0.11 * smoothstep(0.25, R, r));
	vec3 col = mix(glass, body, liq);
	col = mix(col, vec3(0.92, 0.86, 0.8), gm * 0.5);
	float bl = mix(-R, R, clamp(barrier, 0.0, 1.0));
	float bm = (1.0 - smoothstep(bl - 0.012, bl + 0.012, y)) * step(0.002, barrier);
	float rim = smoothstep(0.3, R, r);
	float weave = 0.5 + 0.5 * sin((p.x + y) * 24.0 + t * 2.0) * sin((p.x - y) * 24.0 - t * 1.5);
	col = mix(col, shell.rgb, bm * clamp(0.14 + 0.55 * rim * rim + 0.12 * weave, 0.0, 1.0));
	col *= mix(1.0, 0.42, smoothstep(0.5, R, r));
	float spec = 1.0 - smoothstep(0.0, 0.36, length(p - vec2(-0.3, -0.36)));
	col += vec3(1.0, 0.96, 0.9) * spec * spec * 0.3;
	col += vec3(1.0, 0.95, 0.88) * flash * 0.55;
	float ring = smoothstep(R - px, R + px, r);
	vec3 outc = mix(col, iron_ring(p, r, px), ring);
	float alpha = 1.0 - smoothstep(1.0 - px * 2.0, 1.0, r);
	// art = 1: only the glass is drawn here, the forged ring is a texture laid over it
	alpha = mix(alpha, 1.0 - smoothstep(R, R + px * 2.0, r), art);
	COLOR = vec4(mix(outc, col, art), alpha);
}
";

    private const string MapCode = @"shader_type canvas_item;
uniform sampler2D map_tex : filter_linear, repeat_disable;
uniform vec2 center = vec2(0.5, 0.5);
uniform vec2 span = vec2(0.2, 0.2);
uniform float has_map = 0.0;
uniform float art = 0.0;
const float R = 0.9;
" + Common + @"
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float px = fwidth(r) * 1.25;
	vec2 muv = center + (p / R) * span;
	float inb = step(0.0, muv.x) * step(muv.x, 1.0) * step(0.0, muv.y) * step(muv.y, 1.0) * has_map;
	vec4 m = texture(map_tex, clamp(muv, vec2(0.0, 0.0), vec2(1.0, 1.0))) * inb;
	float fl = smoothstep(0.42, 0.58, m.r);
	float edge = smoothstep(0.12, 0.45, m.r) * (1.0 - smoothstep(0.5, 0.8, m.r)) * mix(0.4, 1.0, m.a);
	float road = smoothstep(0.35, 0.65, m.g) * fl;
	float pit = smoothstep(0.4, 0.6, m.b);
	vec3 col = vec3(0.026, 0.025, 0.03);
	col = mix(col, vec3(0.10, 0.05, 0.035), pit * 0.9);
	col = mix(col, vec3(0.29, 0.265, 0.225), fl * 0.85);
	col = mix(col, vec3(0.43, 0.39, 0.32), road * 0.7);
	col = mix(col, vec3(0.76, 0.69, 0.56), edge * 0.9);
	col *= mix(1.0, 0.5, smoothstep(0.55, R, r));
	float ring = smoothstep(R - px, R + px, r);
	vec3 outc = mix(col, iron_ring(p, r, px), ring);
	float inner = 1.0 - smoothstep(R - px, R + px, r);
	float alpha = (1.0 - smoothstep(1.0 - px * 2.0, 1.0, r)) * mix(1.0, 0.84, inner);
	alpha = mix(alpha, (1.0 - smoothstep(R, R + px * 2.0, r)) * 0.86, art);
	COLOR = vec4(mix(outc, col, art), alpha);
}
";

    private const string VignetteCode = @"shader_type canvas_item;
// [gfx 0.3] The warning lives in the outer band of the frame only: the middle 56 % x 50 % of the picture is never
// touched, the tint rises towards the edge and is capped, so a heavy blow or low life can never hide the fight.
uniform float strength = 0.0;
uniform vec4 tint : source_color = vec4(0.5, 0.0, 0.0, 1.0);
void fragment() {
	vec2 q = abs(UV - vec2(0.5, 0.5)) * 2.0;
	float d = length(max(q - vec2(0.56, 0.5), vec2(0.0)) / vec2(0.44, 0.5));
	float v = smoothstep(0.0, 1.0, d);
	COLOR = vec4(tint.rgb, clamp(v * v * strength, 0.0, 0.55));
}
";

    private static Shader _globe, _map, _vignette;
    public static Shader Globe => _globe ??= new Shader { Code = GlobeCode };
    public static Shader Map => _map ??= new Shader { Code = MapCode };
    public static Shader Vignette => _vignette ??= new Shader { Code = VignetteCode };
}

/// <summary>
/// A life or resource globe: glass sphere in a forged-iron ring, liquid level with a slow swell, a pale band for
/// what was just lost, a barrier sheen, the numbers on hover, and pips (combo points / sigils) in an arc above it.
/// </summary>
public partial class HudGlobe : Control
{
    public const float Diameter = 172f;
    public const float PipArea = 30f;

    private static readonly StringName PFill = "fill", PGhost = "ghost", PBarrier = "barrier", PFlash = "flash",
        PPulse = "pulse", PPhase = "phase", PLiquid = "liquid", PShell = "shell";
    private static readonly Vector2[] Diamond = new Vector2[4];

    private readonly ColorRect _rect;
    private readonly ShaderMaterial _mat;
    private readonly Label _read;
    private float _fill = -1f, _ghost = -1f, _ghostHold, _barrier = -1f, _flash, _pulse = -1f;
    private float _sentFill = -9f, _sentGhost = -9f, _sentBarrier = -9f, _sentFlash = -9f;
    private int _pips, _maxPips;
    private Color _pipColor = Colors.White;
    private bool _roundPips;
    private string _readText = "";
    private readonly bool _rimArt;

    /// <summary>True while the pointer rests on the glass: the owner then feeds the numeric readout.</summary>
    public bool Hovered { get; private set; }

    public HudGlobe()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(Diameter, Diameter + PipArea);
        Size = CustomMinimumSize;

        _mat = new ShaderMaterial { Shader = HudShaders.Globe };
        _rect = new ColorRect
        {
            Material = _mat, Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(0f, PipArea), Size = new Vector2(Diameter, Diameter),
        };
        AddChild(_rect);

        if (UiTheme.Art && UiArt.Has("globe_rim"))
        {
            // the forged ring with its crown and cradle lies over the glass; the shader then draws the glass only
            _mat.SetShaderParameter("art", 1f);
            var piece = UiArt.P("globe_rim");
            var rim = new Control { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(0f, PipArea), Size = new Vector2(Diameter, Diameter) };
            rim.Draw += () => UiArt.Sprite(rim, piece, new Rect2(0f, 0f, Diameter, Diameter));
            AddChild(rim);
            _rimArt = true;
        }

        // the numbers shown while the pointer rests on the glass: list-row size, up to three short lines
        _read = UiTheme.Outlined(UiTheme.Small("", UiTheme.FsRow, UiTheme.Text, true), 6);
        _read.AddThemeConstantOverride("line_spacing", -4);
        _read.AutowrapMode = TextServer.AutowrapMode.WordSmart;       // "12345 / 12345" breaks at the space when it is wider than the glass
        _read.HorizontalAlignment = HorizontalAlignment.Center;
        _read.VerticalAlignment = VerticalAlignment.Center;
        _read.Position = new Vector2(0f, PipArea);
        _read.Size = new Vector2(Diameter, Diameter);
        _read.Visible = false;
        AddChild(_read);

        MouseEntered += () => { Hovered = true; _read.Visible = true; };
        MouseExited += () => { Hovered = false; _read.Visible = false; };
    }

    /// <summary>Only the glass itself reacts to the pointer, not the corners of its square.</summary>
    public override bool _HasPoint(Vector2 point)
    {
        var c = new Vector2(Diameter * 0.5f, PipArea + Diameter * 0.5f);
        return point.DistanceSquaredTo(c) <= Diameter * Diameter * 0.25f;
    }

    public void Setup(Color liquid, Color shell, float phase)
    {
        _mat.SetShaderParameter(PLiquid, liquid);
        _mat.SetShaderParameter(PShell, shell);
        _mat.SetShaderParameter(PPhase, phase);
        _fill = -1f; _ghost = -1f; _sentFill = _sentGhost = _sentBarrier = _sentFlash = -9f; _pulse = -1f;
    }

    /// <summary>Feeds the current fractions (0..1). Losses leave a pale band that drains after a short hold.</summary>
    public void SetValues(float fill, float barrier, float dt)
    {
        fill = Mathf.Clamp(fill, 0f, 1f);
        barrier = Mathf.Clamp(barrier, 0f, 1f);
        if (_fill < 0f) { _fill = fill; _ghost = fill; }
        if (fill < _fill - 0.004f) { _ghostHold = 0.35f; if (fill < _fill - 0.03f) _flash = Mathf.Max(_flash, 0.5f); }
        _fill = fill;
        if (_ghost < _fill) _ghost = _fill;
        else if (_ghost > _fill)
        {
            if (_ghostHold > 0f) _ghostHold -= dt;
            else _ghost = Mathf.Max(_fill, _ghost - dt * 0.55f);
        }
        if (_flash > 0f) _flash = Mathf.Max(0f, _flash - dt * 2.2f);
        _barrier = barrier;

        if (Mathf.Abs(_fill - _sentFill) > 0.0008f) { _sentFill = _fill; _mat.SetShaderParameter(PFill, _fill); }
        if (Mathf.Abs(_ghost - _sentGhost) > 0.0008f) { _sentGhost = _ghost; _mat.SetShaderParameter(PGhost, _ghost); }
        if (Mathf.Abs(_barrier - _sentBarrier) > 0.002f) { _sentBarrier = _barrier; _mat.SetShaderParameter(PBarrier, _barrier); }
        if (Mathf.Abs(_flash - _sentFlash) > 0.004f) { _sentFlash = _flash; _mat.SetShaderParameter(PFlash, _flash); }
    }

    /// <summary>0..1: how strongly the liquid throbs (low life warning).</summary>
    public void SetPulse(float pulse)
    {
        if (Mathf.Abs(pulse - _pulse) < 0.02f) return;
        _pulse = pulse;
        _mat.SetShaderParameter(PPulse, pulse);
    }

    public void SetPips(int pips, int max, Color color, bool round)
    {
        if (pips == _pips && max == _maxPips && round == _roundPips && color == _pipColor) return;
        _pips = pips; _maxPips = max; _pipColor = color; _roundPips = round;
        QueueRedraw();
    }

    public void SetReadout(string text)
    {
        if (text == _readText) return;
        _readText = text ?? "";
        _read.Text = _readText;
    }

    public override void _Draw()
    {
        if (_maxPips <= 0) return;
        var c = new Vector2(Diameter * 0.5f, PipArea + Diameter * 0.5f);
        float radius = Diameter * 0.5f + (_rimArt ? 23f : 14f);      // clear of the ring's crown
        const float step = 0.25f;                                   // radians between pips
        float start = -Mathf.Pi * 0.5f - step * (_maxPips - 1) * 0.5f;
        var dark = new Color(0.03f, 0.03f, 0.035f, 0.9f);
        var rim = new Color(UiTheme.Border, 0.95f);
        for (int i = 0; i < _maxPips; i++)
        {
            float a = start + step * i;
            var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            bool on = i < _pips;
            if (_roundPips)
            {
                DrawCircle(p, 10f, on ? new Color(_pipColor, 0.35f) : rim);
                DrawCircle(p, 8f, on ? _pipColor : dark);
                if (on) DrawCircle(p - new Vector2(2f, 2.2f), 2.4f, new Color(1f, 1f, 1f, 0.55f));
            }
            else
            {
                Rhomb(p, 11f, on ? new Color(_pipColor, 0.4f) : rim);
                Rhomb(p, 8.4f, on ? _pipColor : dark);
                if (on) Rhomb(p - new Vector2(0f, 2.4f), 2.8f, new Color(1f, 1f, 1f, 0.5f));
            }
        }
    }

    private void Rhomb(Vector2 p, float r, Color col)
    {
        Diamond[0] = new Vector2(p.X, p.Y - r * 1.15f);
        Diamond[1] = new Vector2(p.X + r * 0.85f, p.Y);
        Diamond[2] = new Vector2(p.X, p.Y + r * 1.15f);
        Diamond[3] = new Vector2(p.X - r * 0.85f, p.Y);
        DrawColoredPolygon(Diamond, col);
    }
}
