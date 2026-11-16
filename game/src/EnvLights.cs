using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Owns the fixed lights of a level (torches, braziers, lanterns, pit glow). They are plain OmniLight3D children;
/// this node keeps the cost down: twice a second it switches off every light that cannot reach the screen, keeps
/// only the nearest few of the cheap glow lights (lava banks, fungus), hands the few shadow maps to the fires
/// nearest the hero, and every frame it flickers the ones that are on with three summed sines (no noise lookups,
/// no allocations). It also keeps the ranges that depend on the camera zoom in step with it: where the distance
/// fog starts (always behind the hero), how far the sun's shadow cascades and the mist froxels reach.
/// </summary>
public partial class EnvLights : Node3D
{
    private const float ScanEvery = 0.5f;

    /// <summary>The level's environment; fog and mist ranges follow the camera zoom.</summary>
    public Godot.Environment Air;

    private OmniLight3D[] _lights;
    private Vector3[] _pos;
    private float[] _energy, _range, _flicker, _phase, _dist;
    private bool[] _canShadow, _on, _shadow, _want;
    private int[] _active, _picked, _order;
    private int _activeCount, _maxShadows, _maxGlow, _maxFire, _errors;
    private float _scanT = ScanEvery, _clock, _zoomApplied = -1f;
    private Vector3 _fallback, _lastScan;
    private DirectionalLight3D _sun;
    private ulong _heroLit;
    private float _heroEnergy = 9f;
    private OmniLight3D _heroLight;
    private const float HeroLightHeight = 4.2f;

    /// <summary>Lights currently switched on (for the debug log).</summary>
    public int ActiveCount => _activeCount;

    internal void Setup(List<EnvLightSpec> specs, LevelTheme theme, int quality, float energyGain, Vector3 start, DirectionalLight3D sun)
    {
        int n = specs?.Count ?? 0;
        var preset = Gfx.PresetOf(Game.I?.Cfg);
        _sun = sun;
        // energy for about the same light on the ground at the hero's feet as the old low light gave (A), less in B
        _heroEnergy = EnvAtmosphere.Dark ? 13f : (theme != null && theme.Outdoor ? 8.5f : 8f);
        _lights = new OmniLight3D[n];
        _pos = new Vector3[n];
        _energy = new float[n]; _range = new float[n]; _flicker = new float[n]; _phase = new float[n]; _dist = new float[n];
        _canShadow = new bool[n]; _on = new bool[n]; _shadow = new bool[n]; _want = new bool[n];
        _active = new int[n]; _order = new int[n];
        _maxShadows = preset.OmniShadows;
        _maxGlow = quality <= 0 ? 5 : quality == 1 ? 8 : quality == 2 ? 12 : 18;
        _maxFire = quality <= 0 ? 8 : quality == 1 ? 12 : quality == 2 ? 16 : 24;
        _picked = new int[Math.Max(_maxShadows, 1)];
        // Six cube faces of forest per torch per frame is what the frame time used to go on (and with animated
        // characters in reach every shadow map is redrawn every frame): two paraboloid halves are a third of that and,
        // from the height the camera hangs at, look the same. Measured at 極致 with four cube lights: 49 ms in a
        // crypt fight. So paraboloids at every preset; the top preset keeps the penumbrae.
        var mode = OmniLight3D.ShadowMode.DualParaboloid;
        Color torch = theme?.TorchColor ?? new Color(1f, 0.56f, 0.24f);
        float torchRange = theme != null && theme.TorchRange > 1f ? theme.TorchRange : 11f;
        float boost = Mathf.Clamp(theme?.TorchBoost ?? 1f, 0.3f, 3f);

        for (int i = 0; i < n; i++)
        {
            var s = specs[i];
            bool fire = s.Flicker >= 0.6f;
            Color c = s.Color.R + s.Color.G + s.Color.B > 0.02f ? s.Color : torch;
            _pos[i] = s.Pos;
            _energy[i] = Mathf.Max(s.Energy, 0f) * energyGain * boost;
            // With the steep falloff nothing visible is left beyond three quarters of the authored range, and every
            // metre of radius is paid for in every pixel the sphere covers.
            _range[i] = (s.Range > 0.5f ? s.Range : torchRange) * (EnvAtmosphere.Dark ? 0.8f : (fire ? 0.88f : 0.9f)) * (1f + (boost - 1f) * 0.4f);
            _flicker[i] = Mathf.Clamp(s.Flicker, 0f, 1.5f);
            _phase[i] = Mathf.PosMod(s.Pos.X * 12.9898f + s.Pos.Z * 78.233f + s.Pos.Y * 37.719f, Mathf.Tau);
            _canShadow[i] = s.CanShadow && fire;
            var l = new OmniLight3D
            {
                Position = s.Pos, LightColor = c, LightEnergy = _energy[i], OmniRange = _range[i],
                // steeper than the default: pools of light with a hot centre, not flood lighting (B: inverse square)
                OmniAttenuation = EnvAtmosphere.Dark ? (fire ? 2.0f : 2.1f) : (fire ? 1.45f : 1.7f),
                OmniShadowMode = mode, ShadowEnabled = false, ShadowBias = 0.05f, ShadowNormalBias = 1.1f, ShadowBlur = 1.6f, ShadowOpacity = 0.94f,
                LightSize = fire && quality >= 3 ? 0.12f : 0f,
                LightSpecular = fire ? 0.9f : 0.5f, LightVolumetricFogEnergy = fire ? 2.4f : 1.2f,
                LightBakeMode = Light3D.BakeMode.Disabled, Visible = false,
            };
            _lights[i] = l;
            AddChild(l);
        }
        _fallback = start;
        Scan(start, 1f);
    }

    public override void _Process(double delta)
    {
        try
        {
            float dt = (float)delta;
            float zoom = 1f;
            var cam = Game.I?.Cam;
            if (cam != null && IsInstanceValid(cam)) zoom = Mathf.Clamp(cam.Zoom, 0.6f, CameraRig.ZoomCeiling);
            if (Mathf.Abs(zoom - _zoomApplied) > 0.01f) ApplyZoom(zoom);
            TuneHeroLight();
            if (_heroLight != null)
            {
                Vector3 hp = HeroPos();
                _heroLight.GlobalPosition = new Vector3(hp.X, HeroLightHeight, hp.Z + 1.3f);
                // a carried flame breathes a little
                _heroLight.LightEnergy = _heroEnergy * (1f + 0.05f * Mathf.Sin(_clock * 7.3f) + 0.035f * Mathf.Sin(_clock * 12.1f + 1.7f));
            }
            if (_lights == null || _lights.Length == 0) return;

            _clock += dt;
            _scanT += dt;
            Vector3 hero = HeroPos();
            // rescan at once after a teleport, otherwise the lights at the destination pop in half a second late
            if (_scanT >= ScanEvery || hero.DistanceSquaredTo(_lastScan) > 25f) Scan(hero, zoom);

            float t = _clock;
            for (int n = 0; n < _activeCount; n++)
            {
                int i = _active[n];
                float f = _flicker[i];
                if (f <= 0.001f) continue;
                float ph = _phase[i];
                float w = f >= 0.6f
                    ? 0.11f * Mathf.Sin(t * 9.7f + ph) + 0.075f * Mathf.Sin(t * 15.3f + ph * 1.7f) + 0.05f * Mathf.Sin(t * 23.9f + ph * 0.61f)
                    : 0.5f * Mathf.Sin(t * 1.3f + ph);         // glowing pits and fungus breathe slowly
                _lights[i].LightEnergy = _energy[i] * (1f + w * f);
            }
        }
        catch (Exception e)
        {
            if (_errors++ < 3) Log.Error("env", "light manager update failed", e);
        }
    }

    /// <summary>
    /// The ember the hero carries (an OmniLight3D the Player node creates for itself) is part of the lighting of
    /// every level: it is what keeps the hero readable wherever they stand. Its strength is balanced here against
    /// the sun and the fires of the theme, once per hero instance.
    /// </summary>
    private void TuneHeroLight()
    {
        var p = World.P;
        if (p == null || !IsInstanceValid(p) || !p.IsInsideTree()) return;
        ulong id = p.GetInstanceId();
        if (id == _heroLit) return;
        _heroLit = id;
        // The light the Player node carries sits a metre from the hero's head: by distance fall-off it lit the hero
        // several times more than the ground, which made the figure look self-lit. It is switched off; the light
        // manager keeps one of its own instead (see _Process), hung 4.2 m up and a little towards the lens, where
        // the hero's body and the ground at its feet are about equally far from it.
        int n = p.GetChildCount();
        for (int i = 0; i < n; i++)
            if (p.GetChild(i) is OmniLight3D l) { l.Visible = false; break; }
        if (_heroLight == null)
        {
            bool dark = EnvAtmosphere.Dark;
            _heroLight = new OmniLight3D
            {
                Name = "HeroFire", LightColor = new Color(1f, 0.72f, 0.45f), LightEnergy = _heroEnergy,
                OmniRange = dark ? 7.6f : 9f, OmniAttenuation = dark ? 1.9f : 1.6f, ShadowEnabled = false,
                LightSpecular = 0.35f, LightVolumetricFogEnergy = 1.2f, LightBakeMode = Light3D.BakeMode.Disabled,
            };
            AddChild(_heroLight);
        }
    }

    /// <summary>Everything that has to reach exactly as far as the lens sees.</summary>
    private void ApplyZoom(float zoom)
    {
        _zoomApplied = zoom;
        float far = CameraRig.FarGround(zoom);
        if (_sun != null && IsInstanceValid(_sun)) _sun.DirectionalShadowMaxDistance = far + 6f;
        if (Air != null)
        {
            // the hero's plane stays clear: the distance fog starts a few metres behind the focus and thickens beyond the frame
            float begin = CameraRig.BaseDistance * zoom + 4f + 2f * zoom;
            Air.FogDepthBegin = begin;
            Air.FogDepthEnd = begin + 40f + 12f * zoom;
            Air.VolumetricFogLength = far + 6f;
        }
    }

    private Vector3 HeroPos()
    {
        var p = World.P;
        if (p != null && IsInstanceValid(p) && p.IsInsideTree()) return p.GlobalPosition;
        var cam = Game.I?.Cam;
        if (cam != null && IsInstanceValid(cam) && cam.IsInsideTree()) return cam.GlobalPosition;
        return _fallback;
    }

    /// <summary>Distance culling, the caps on glow and fire lights, and the shadow hand-out around a point.</summary>
    private void Scan(Vector3 hero, float zoom)
    {
        _scanT = 0f;
        _lastScan = hero;
        float view = CameraRig.ViewReach(zoom) + 1.5f;         // farthest ground the camera shows, measured from the hero
        int cand = 0;
        for (int i = 0; i < _lights.Length; i++)
        {
            float dx = _pos[i].X - hero.X, dz = _pos[i].Z - hero.Z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            float reach = view + _range[i] * 0.8f + (_on[i] ? 3f : 0f);   // a little hysteresis so lights on the rim do not blink
            _want[i] = d < reach;
            _dist[i] = d - (_on[i] ? 1.5f : 0f);
            if (_want[i]) _order[cand++] = i;
        }
        // nearest first; then the budgets: glow lights and fires are counted separately
        Array.Sort(_order, 0, cand, Comparer<int>.Create((a, b) => _dist[a].CompareTo(_dist[b])));
        int glow = 0, fire = 0, count = 0;
        for (int k = 0; k < cand; k++)
        {
            int i = _order[k];
            bool isFire = _flicker[i] >= 0.6f;
            if (isFire ? fire >= _maxFire : glow >= _maxGlow) { _want[i] = false; continue; }
            if (isFire) fire++; else glow++;
        }
        for (int i = 0; i < _lights.Length; i++)
        {
            bool want = _want[i];
            if (want != _on[i])
            {
                _on[i] = want;
                _lights[i].Visible = want;
                if (!want && _shadow[i]) { _shadow[i] = false; _lights[i].ShadowEnabled = false; }
            }
            if (want) _active[count++] = i;
        }
        _activeCount = count;
        if (_maxShadows <= 0) return;

        // the nearest shadow-capable fires on screen get the shadow maps; current holders are favoured by 1.5 m
        int picked = 0;
        for (int k = 0; k < cand && picked < _maxShadows; k++)
        {
            int i = _order[k];
            if (!_want[i] || !_canShadow[i] || _dist[i] > view) continue;
            _picked[picked++] = i;
        }
        for (int n = 0; n < count; n++)
        {
            int i = _active[n];
            if (!_canShadow[i]) continue;
            bool want = false;
            for (int k = 0; k < picked; k++) if (_picked[k] == i) { want = true; break; }
            if (want != _shadow[i])
            {
                _shadow[i] = want;
                _lights[i].ShadowEnabled = want;
            }
        }
    }
}
