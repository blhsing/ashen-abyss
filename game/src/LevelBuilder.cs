using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AshenAbyss;

/// <summary>
/// Everything visible in a level that is not an actor or an interactable: terrain, walls and cliffs, pits, props,
/// lights, the WorldEnvironment and the weather. The work is split over the Env* helpers; this class is the public
/// face named in the architecture contract.
/// </summary>
public static class LevelBuilder
{
    private static readonly Dictionary<string, Material> Surfaces = new();

    /// <summary>Builds the whole look of <paramref name="level"/> as children of it and stores the WorldEnvironment in level.Env.</summary>
    public static void Build(Level level, LevelData data, LevelTheme theme)
    {
        if (level == null || data == null || data.Grid == null)
        {
            Log.Error("env", "LevelBuilder.Build called without a level or a grid; nothing built");
            return;
        }
        theme ??= LevelThemes.Get("wastes");
        if (!string.IsNullOrEmpty(ThemeOverride)) theme = LevelThemes.Get(ThemeOverride);
        else if (level.IsRift && theme.Id != "rift") theme = RiftVariant(theme);
        Log.Guard("env", () => TrimCaches(theme.Id));
        EnvBuild b;
        try { b = new EnvBuild(level, data, theme); }
        catch (Exception e) { Log.Error("env", $"cannot prepare level build for theme '{theme.Id}'", e); return; }

        Log.Info("env", $"building theme='{theme.Id}' kind={data.Kind} grid={data.Grid.W}x{data.Grid.H} seed={data.Seed} slots={data.Props?.Count ?? 0} quality={b.Quality} apron={b.F.A}");
        Step(b, "globals", EnvAssets.EnsureGlobals);
        Step(b, "theme", () => { EnvMats.Reset(theme); EnvMats.SetWalkMask(b); EnvProps.SetTheme(theme); });
        Step(b, "terrain", () => EnvTerrain.Build(b));
        Step(b, "pits", () => EnvTerrain.BuildPits(b));
        Step(b, "walls", () => EnvWalls.Build(b));
        Step(b, "props", () => EnvProps.PlaceSlots(b));
        Step(b, "filllights", () => EnvProps.FillLights(b));
        Step(b, "scatter", () => EnvProps.ScatterGround(b));
        Step(b, "commit", b.Commit);
        Step(b, "lights", () => EnvAtmosphere.BuildLights(b));
        Step(b, "environment", () => EnvAtmosphere.BuildEnvironment(b));
        Step(b, "weather", () => EnvAtmosphere.BuildWeather(b));
        Log.Info("env", $"built '{theme.Id}' in {b.Clock.ElapsedMilliseconds} ms [{b.Timing}] meshes={b.MeshNodes} tris={b.Triangles} propInstances={b.PropInstances} propBatches={b.PropBatches} lights={b.Lights.Count} flames={b.Flames.Count}");
    }

    private static string _builtTheme;
    /// <summary>Releases the caches of themes left behind (at most two themes resident); see TrimCaches.</summary>
    public static bool TrimEnabled = true;          // on since 0.1.0: a six-region tour peaked at 3.9 GB and fell back to 2.9 GB (4.7 GB and rising without it), 0 errors

    /// <summary>
    /// Every cache of the environment and of the character models only ever grew: each region visited left its
    /// texture sets, prop models, sky and monster models in video memory for good (2.1 GB in town, 4.2 GB after five
    /// regions on a GPU that shares the system's RAM). When a level of another theme is built, everything the caches
    /// hold is handed to a "previous" generation or dropped: what the new level asks for is taken back (or loaded),
    /// the rest is released once the old level's nodes are gone. At most two themes are resident.
    /// </summary>
    private static void TrimCaches(string themeId)
    {
        if (!TrimEnabled || themeId == _builtTheme) return;
        _builtTheme = themeId;
        Surfaces.Clear();
        EnvAssets.Trim();
        EnvProps.Trim();
        EnvAtmosphere.TrimSkies();
        CharModel.TrimCache();
    }

    /// <summary>Test harness: draw every level with this theme id, whatever the level asked for (null = off).</summary>
    public static string ThemeOverride;

    /// <summary>
    /// The endless rift rolls the layout and materials of an ordinary dungeon theme; this lays the rift's own air over
    /// it (violet ambient and mist, pale motes rising, cold violet rim) so the place is unmistakable whatever it rolled.
    /// </summary>
    private static LevelTheme RiftVariant(LevelTheme t)
    {
        var rift = LevelThemes.Get("rift");
        var r = t.Clone();
        r.Ambient = rift.Ambient;
        r.AmbientEnergy = Mathf.Max(t.AmbientEnergy, rift.AmbientEnergy);
        r.FogColor = rift.FogColor;
        r.MistColor = rift.MistColor;
        r.MistDensity = Mathf.Max(t.MistDensity, rift.MistDensity);
        r.SunColor = rift.SunColor;
        r.Accent = rift.Accent;
        r.Weather = "embers";
        r.WeatherColor = rift.WeatherColor;
        r.WeatherAmount = 0.7f;
        if (EnvTerrain.PitKind(t) == "void") { r.PitColor = rift.PitColor; r.PitGlow = rift.PitGlow; }
        r.GroundTint = new Color(t.GroundTint.R * 0.95f, t.GroundTint.G * 0.92f, t.GroundTint.B * 1.1f);
        r.WallTint = new Color(t.WallTint.R * 0.92f, t.WallTint.G * 0.88f, t.WallTint.B * 1.12f);
        r.Saturation = Mathf.Min(t.Saturation, 0.9f);
        r.Grade = 0.09f;
        r.Vignette = 0.5f;
        r.RimColor = new Color(0.72f, 0.62f, 1f);
        r.RimEnergy = 1.3f;
        return r;
    }

    private static void Step(EnvBuild b, string name, Action action)
    {
        long t0 = b.Clock.ElapsedMilliseconds;
        try { action(); }
        catch (Exception e) { Log.Error("env", $"build step '{name}' failed", e); }
        if (b.Timing.Length > 0) b.Timing.Append(' ');
        b.Timing.Append(name).Append('=').Append(b.Clock.ElapsedMilliseconds - t0);
    }

    /// <summary>
    /// A fresh instance of res://assets/env/models/&lt;name&gt;/&lt;name&gt;.glb (scene cached), or null when the model does not
    /// exist (logged once). Its materials follow the grading of the level that was built last.
    /// </summary>
    public static Node3D Prop(string modelName)
    {
        var def = EnvProps.Def(modelName);
        if (def == null) return null;
        try
        {
            EnvProps.Use(def);
            var inst = def.Scene.Instantiate<Node3D>();
            inst.Name = modelName;
            EnvProps.PrepareInstance(inst);
            return inst;
        }
        catch (Exception e)
        {
            Log.Error("env", $"cannot instantiate prop '{modelName}'", e);
            return null;
        }
    }

    /// <summary>
    /// Cached PBR material from res://assets/env/tex/&lt;set&gt;/ that works on any mesh (object-space triplanar, so no UVs
    /// are needed): one repeat every size_m / uvScale metres. Missing sets give a flat stand-in (logged once).
    /// </summary>
    public static Material Surface(string texSet, float uvScale = 1f)
    {
        texSet ??= "";
        if (uvScale <= 0f) uvScale = 1f;
        string key = texSet + "|" + uvScale.ToString("0.###", CultureInfo.InvariantCulture);
        if (Surfaces.TryGetValue(key, out var cached)) return cached;

        var info = EnvAssets.Info(texSet);
        var mat = new OrmMaterial3D { ResourceName = "env_" + texSet };
        try
        {
            // keep hand-built objects in the same value range as the graded environment
            float level = EnvAssets.LevelScale(texSet, 0.05f, 0.17f);
            float srgb = Mathf.Pow(Mathf.Clamp(level, 0.05f, 1f), 1f / 2.2f);
            var alb = EnvAssets.Map(texSet, "albedo");
            if (alb != null)
            {
                mat.AlbedoTexture = alb;
                mat.AlbedoColor = new Color(srgb, srgb, srgb);
                var nrm = EnvAssets.Map(texSet, "normal");
                if (nrm != null) { mat.NormalEnabled = true; mat.NormalTexture = nrm; }
                var arm = EnvAssets.Map(texSet, "arm");
                if (arm != null) { mat.OrmTexture = arm; mat.AOEnabled = true; }
                else { mat.Roughness = 0.85f; mat.Metallic = 0f; }
                if (info.Emission)
                {
                    var em = EnvAssets.Map(texSet, "emission");
                    if (em != null) { mat.EmissionEnabled = true; mat.EmissionTexture = em; mat.Emission = Colors.White; mat.EmissionEnergyMultiplier = 3f; }
                }
            }
            else
            {
                mat.AlbedoColor = new Color(info.Avg.R * srgb, info.Avg.G * srgb, info.Avg.B * srgb);
                mat.Roughness = 0.85f;
                mat.Metallic = 0f;
            }
            float s = uvScale / Mathf.Max(info.SizeM, 0.05f);
            mat.Uv1Triplanar = true;
            mat.Uv1TriplanarSharpness = 4f;
            mat.Uv1Scale = new Vector3(s, s, s);
        }
        catch (Exception e) { Log.Error("env", $"building surface material '{texSet}' failed", e); }
        Surfaces[key] = mat;
        return mat;
    }
}

/// <summary>What the manifest says about one PBR texture set.</summary>
internal sealed class TexInfo
{
    public string Name;
    public float SizeM = 2f;
    public float Lum = -1f;                     // linear luminance of the average albedo, -1 = unknown
    public Color Avg = new(0.45f, 0.43f, 0.4f); // sRGB
    public bool Emission, Known;
}

/// <summary>Shared textures, shaders and manifest data of the environment renderer. Missing files are logged once and replaced by neutral stand-ins.</summary>
internal static class EnvAssets
{
    public const string TexRoot = "res://assets/env/tex/", ModelRoot = "res://assets/env/models/", SkyRoot = "res://assets/env/sky/";

    private static Dictionary<string, TexInfo> _info;
    private static readonly Dictionary<string, Texture2D> Maps = new();
    private static Dictionary<string, Texture2D> _prevMaps = new();

    /// <summary>The maps of the theme before this one stay for one more level (town and back costs nothing); older ones go.</summary>
    public static void Trim()
    {
        _prevMaps = new Dictionary<string, Texture2D>(Maps);
        Maps.Clear();
    }
    private static readonly Dictionary<string, Texture2D> Solids = new();
    private static readonly Dictionary<string, Shader> Shaders = new();
    private static readonly HashSet<string> Logged = new();
    private static Texture2D _noise, _wave, _dot, _flatNormal, _defaultArm;

    public static bool LogOnce(string key) => Logged.Add(key);

    // ───────────── manifest ─────────────

    private static void LoadManifest()
    {
        _info = new Dictionary<string, TexInfo>();
        string path = TexRoot + "manifest.json";
        try
        {
            if (!Godot.FileAccess.FileExists(path)) { Log.Warn("env", $"texture manifest missing ({path}); assuming 2 m tiles"); return; }
            using var fa = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (fa == null) { Log.Warn("env", $"texture manifest unreadable ({path})"); return; }
            using var doc = JsonDocument.Parse(fa.GetAsText());
            if (!doc.RootElement.TryGetProperty("sets", out var sets) || sets.ValueKind != JsonValueKind.Array) return;
            foreach (var s in sets.EnumerateArray())
            {
                if (!s.TryGetProperty("name", out var nm) || nm.ValueKind != JsonValueKind.String) continue;
                var ti = new TexInfo { Name = nm.GetString(), Known = true };
                if (s.TryGetProperty("size_m", out var sz))
                {
                    if (sz.ValueKind == JsonValueKind.Array && sz.GetArrayLength() > 0 && sz[0].ValueKind == JsonValueKind.Number) ti.SizeM = (float)sz[0].GetDouble();
                    else if (sz.ValueKind == JsonValueKind.Number) ti.SizeM = (float)sz.GetDouble();
                }
                if (ti.SizeM < 0.05f) ti.SizeM = 2f;
                if (s.TryGetProperty("avg_color_rgb", out var rgb) && rgb.ValueKind == JsonValueKind.Array && rgb.GetArrayLength() >= 3)
                {
                    ti.Avg = new Color((float)rgb[0].GetDouble() / 255f, (float)rgb[1].GetDouble() / 255f, (float)rgb[2].GetDouble() / 255f);
                    ti.Lum = 0.2126f * ToLinear(ti.Avg.R) + 0.7152f * ToLinear(ti.Avg.G) + 0.0722f * ToLinear(ti.Avg.B);
                }
                if (s.TryGetProperty("maps", out var maps) && maps.ValueKind == JsonValueKind.Array)
                    foreach (var m in maps.EnumerateArray()) if (m.ValueKind == JsonValueKind.String && m.GetString() == "emission") ti.Emission = true;
                _info[ti.Name] = ti;
            }
            Log.Info("env", $"texture manifest: {_info.Count} sets");
        }
        catch (Exception e) { Log.Error("env", "reading the texture manifest failed", e); }
    }

    public static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

    public static TexInfo Info(string set)
    {
        if (_info == null) LoadManifest();
        set ??= "";
        if (_info.TryGetValue(set, out var ti)) return ti;
        ti = new TexInfo { Name = set };
        _info[set] = ti;
        if (set.Length > 0 && _info.Count > 1 && LogOnce("info:" + set)) Log.Warn("env", $"texture set '{set}' is not in the manifest");
        return ti;
    }

    /// <summary>
    /// Multiplier that brings a set's average albedo into [min, max] (linear), so a pale texture cannot blow out a dark
    /// region and a near-black one still reads under torch light.
    /// </summary>
    public static float LevelScale(string set, float min, float max)
    {
        float lum = Info(set).Lum;
        if (lum <= 0.0001f) return 1f;
        if (lum < min) return Mathf.Min(min / lum, 4f);
        if (lum > max) return Mathf.Max(max / lum, 0.3f);
        return 1f;
    }

    // ───────────── textures ─────────────

    /// <summary>One map (albedo, normal, arm, height, emission) of a set, or null when it is not there.</summary>
    public static Texture2D Map(string set, string map)
    {
        string path = TexRoot + set + "/" + map + ".jpg";
        if (Maps.TryGetValue(path, out var t)) return t;
        if (_prevMaps.TryGetValue(path, out t)) { Maps[path] = t; return t; }
        if (!string.IsNullOrEmpty(set))
        {
            try
            {
                if (ResourceLoader.Exists(path)) t = GD.Load<Texture2D>(path);
            }
            catch (Exception e) { Log.Error("env", $"loading {path} failed", e); t = null; }
            if (t == null && map != "emission" && LogOnce("set:" + set))
                Log.Warn("env", $"texture set '{set}' is missing or not imported yet ({path}); using flat stand-ins");
        }
        Maps[path] = t;
        return t;
    }

    private static Texture2D Solid(Color c)
    {
        string key = c.ToHtml(true);
        if (Solids.TryGetValue(key, out var t)) return t;
        var img = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        img.Fill(c);
        t = ImageTexture.CreateFromImage(img);
        Solids[key] = t;
        return t;
    }

    public static Texture2D Albedo(string set) => Map(set, "albedo") ?? Solid(Info(set).Avg);
    public static Texture2D Normal(string set) => Map(set, "normal") ?? (_flatNormal ??= Solid(new Color(0.5f, 0.5f, 1f)));
    public static Texture2D Arm(string set) => Map(set, "arm") ?? (_defaultArm ??= Solid(new Color(1f, 0.85f, 0f)));

    /// <summary>Seamless grey fractal noise, sampled in world space by every environment shader.</summary>
    public static Texture2D Noise
    {
        get
        {
            if (_noise == null)
            {
                var fn = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.014f, FractalOctaves = 5, Seed = 1107 };
                _noise = new NoiseTexture2D { Noise = fn, Width = 256, Height = 256, Seamless = true, GenerateMipmaps = true };
            }
            return _noise;
        }
    }

    /// <summary>Seamless ripple normal map for water.</summary>
    public static Texture2D WaveNormal
    {
        get
        {
            if (_wave == null)
            {
                var fn = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.03f, FractalOctaves = 3, Seed = 4421 };
                _wave = new NoiseTexture2D { Noise = fn, Width = 256, Height = 256, Seamless = true, AsNormalMap = true, BumpStrength = 5f, GenerateMipmaps = true };
            }
            return _wave;
        }
    }

    /// <summary>Soft round sprite for weather particles.</summary>
    public static Texture2D SoftDot
    {
        get
        {
            if (_dot == null)
            {
                var g = new Gradient
                {
                    Offsets = new[] { 0f, 0.45f, 1f },
                    Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0f) },
                };
                _dot = new GradientTexture2D
                {
                    Gradient = g, Width = 32, Height = 32,
                    Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
                };
            }
            return _dot;
        }
    }

    // ───────────── shaders and globals ─────────────

    /// <summary>res://shaders/&lt;name&gt;.gdshader, cached. Null (logged once) when the file is missing.</summary>
    public static Shader LoadShader(string name)
    {
        if (Shaders.TryGetValue(name, out var s)) return s;
        string path = $"res://shaders/{name}.gdshader";
        try
        {
            if (ResourceLoader.Exists(path)) s = GD.Load<Shader>(path);
        }
        catch (Exception e) { Log.Error("env", $"loading shader {path} failed", e); s = null; }
        if (s == null) Log.Error("env", $"shader {path} is missing");
        Shaders[name] = s;
        return s;
    }

    /// <summary>
    /// The prop shader in one of four builds: single or double sided, with or without the wind sway. Only plants get
    /// the sway: its vertex code reads TIME, which makes the renderer treat every mesh using it as animated and
    /// redraw the shadow maps of all lights in reach each frame.
    /// </summary>
    public static Shader PropShader(bool doubleSided, bool sway)
    {
        string key = "env_prop#" + (doubleSided ? "2" : "1") + (sway ? "s" : "");
        if (Shaders.TryGetValue(key, out var s)) return s;
        var src = LoadShader("env_prop");
        if (src != null)
        {
            try
            {
                string code = src.Code;
                if (!sway)
                {
                    int a = code.IndexOf("// SWAY_BEGIN", StringComparison.Ordinal), b = code.IndexOf("// SWAY_END", StringComparison.Ordinal);
                    if (a >= 0 && b > a) code = code.Remove(a, b - a);
                    else Log.Warn("env", "env_prop.gdshader has no SWAY markers; static props will keep the animated vertex code");
                }
                if (doubleSided) code = code.Replace("render_mode cull_back", "render_mode cull_disabled");
                s = new Shader { Code = code };
            }
            catch (Exception e) { Log.Error("env", "deriving a variant of env_prop failed", e); s = src; }
        }
        Shaders[key] = s;
        return s;
    }

    /// <summary>Variant of a shader with back-face culling switched off (foliage cards), built from the same source.</summary>
    public static Shader ShaderDoubleSided(string name)
    {
        string key = name + "#2";
        if (Shaders.TryGetValue(key, out var s)) return s;
        var src = LoadShader(name);
        if (src != null)
        {
            try { s = new Shader { Code = src.Code.Replace("render_mode cull_back", "render_mode cull_disabled") }; }
            catch (Exception e) { Log.Error("env", $"deriving the double-sided variant of {name} failed", e); s = src; }
        }
        Shaders[key] = s;
        return s;
    }

    /// <summary>
    /// The occluder shaders read the global uniform hero_pos (set every frame by Level). It is normally declared in
    /// project.godot; if it is not, it is registered here so the shaders still compile.
    /// </summary>
    public static void EnsureGlobals()
    {
        // hero_pos is declared in project.godot ([shader_globals]); nothing to register at runtime.
    }
}

[Flags]
internal enum WallFlags
{
    None = 0,
    Tri = 1,          // world-space triplanar mapping, mesh has no UVs
    Smooth = 2,       // weld normals (organic rock)
    NoTop = 4,        // no snow / ash / moss cover
    Glow = 8,         // emissive: the batch tint is the emission colour
    Gloss = 16,       // polished / icy
    NoShadow = 32,    // does not cast shadows
    Dull = 64,        // extra rough (cloth, thatch)
    Lit = 128,        // keeps its texture but glows faintly in the batch tint (ice, rift crystal)
}

/// <summary>Theme-graded shader materials. Rebuilt for every level because tints and cover depend on the theme.</summary>
internal static class EnvMats
{
    private static readonly Dictionary<(string, int, uint), ShaderMaterial> Walls = new();
    private static readonly Dictionary<string, ShaderMaterial> Grounds = new();
    private static readonly Dictionary<uint, ShaderMaterial> Caps = new();
    private static readonly StandardMaterial3D Fallback = new() { AlbedoColor = new Color(0.2f, 0.2f, 0.22f), Roughness = 0.9f };

    public static void Reset(LevelTheme theme)
    {
        Walls.Clear();
        Grounds.Clear();
        Caps.Clear();
        _walkMask = null;
    }

    /// <summary>Test harness: build no mask, so the floor cut is off.</summary>
    public static bool WalkMaskOff;
    private static Texture2D _walkMask;
    private static Vector4 _walkRect;

    /// <summary>
    /// Indoors the wall and lid shaders dissolve whatever stands between the lens and walkable floor (floor_cut in
    /// env_wall.gdshader). This is the map they read: one texel per nav cell, white where the cell is not solid, with
    /// a solid ring around the grid so nothing outside it counts as floor. Outdoors there is no mask and no cut:
    /// cliffs, tree lines and roofed houses keep their height.
    /// </summary>
    public static void SetWalkMask(EnvBuild b)
    {
        _walkMask = null;
        var f = b?.F;
        if (WalkMaskOff || f == null || b.Theme == null || b.Theme.Outdoor || !b.SolidWalls) return;
        int w = f.CW + 2, h = f.CH + 2;
        var px = new byte[w * h];
        for (int cy = 0; cy < f.CH; cy++)
            for (int cx = 0; cx < f.CW; cx++)
                if (!f.IsWall(cx, cy)) px[(cy + 1) * w + cx + 1] = 255;
        var img = Image.CreateFromData(w, h, false, Image.Format.L8, px);
        _walkMask = ImageTexture.CreateFromImage(img);
        float cell = NavGrid.Cell;
        _walkRect = new Vector4(f.X0 - cell, f.Z0 - cell, 1f / (w * cell), 1f / (h * cell));
    }

    private static void ApplyWalkMask(ShaderMaterial m)
    {
        if (_walkMask == null) return;
        m.SetShaderParameter("walk_mask", _walkMask);
        m.SetShaderParameter("walk_rect", _walkRect);
    }

    private static Vector3 V(Color c) => new(c.R, c.G, c.B);

    private static readonly string[] LaidWords =
    {
        "cobble", "flagstone", "tiles", "marble", "planks", "blocks", "slabs", "brick", "carpet", "checker", "mosaic", "plate", "slate", "panel", "wood",
    };

    /// <summary>Paving, boards and other man-made floors: their pattern has one true scale.</summary>
    public static bool Laid(string set)
    {
        if (string.IsNullOrEmpty(set)) return false;
        foreach (var w in LaidWords) if (set.Contains(w, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>Terrain material: ground / alternate / path plus one special floor (room or arena).</summary>
    public static Material Ground(LevelTheme t, string special)
    {
        special ??= t.Ground ?? "";              // a theme without any ground set must not throw on the dictionary key
        if (Grounds.TryGetValue(special, out var cached)) return cached;
        var shader = EnvAssets.LoadShader("env_ground");
        if (shader == null) return Fallback;
        var m = new ShaderMaterial { Shader = shader, ResourceName = "ground_" + special };
        string[] sets = { t.Ground, t.GroundAlt ?? t.Ground, t.Path ?? t.Ground, special };
        var scale = new float[4];
        var macro = new float[4];
        for (int i = 0; i < 4; i++)
        {
            string set = string.IsNullOrEmpty(sets[i]) ? t.Ground : sets[i];
            m.SetShaderParameter("alb" + i, EnvAssets.Albedo(set));
            m.SetShaderParameter("nrm" + i, EnvAssets.Normal(set));
            m.SetShaderParameter("arm" + i, EnvAssets.Arm(set));
            scale[i] = 1f / Mathf.Max(EnvAssets.Info(set).SizeM, 0.05f);
            macro[i] = Laid(set) ? 0f : 0.85f;       // only loose ground may be shown at two scales; paving would look broken
            // Indoors the floor sets of one theme are kept within a factor of 1.6 of each other: at the dark level the
            // tone curve stretches differences, a corridor of dark cobbles came out near black next to a flagstone room
            // and every figure on it looked lit from inside (measured 1.5-2.4 x its ground against 0.8-1.1 on flagstone).
            float lumMin = t.Outdoor ? t.GroundLumMin : Mathf.Max(t.GroundLumMin, t.GroundLumMax * 0.62f);
            float lv = EnvAssets.LevelScale(set, lumMin, t.GroundLumMax);
            m.SetShaderParameter("tint" + i, V(t.GroundTint) * lv);
        }
        m.SetShaderParameter("uv_scale", new Vector4(scale[0], scale[1], scale[2], scale[3]));
        m.SetShaderParameter("macro", new Vector4(macro[0], macro[1], macro[2], macro[3]));
        m.SetShaderParameter("noise_tex", EnvAssets.Noise);
        m.SetShaderParameter("desaturate", t.Desaturate);
        m.SetShaderParameter("wet", Mathf.Clamp(t.Wet, 0f, 1f));
        m.SetShaderParameter("rough_mul", Mathf.Clamp(t.GroundGloss, 0.15f, 1.5f));
        // a breath of the region's two hues drifting across the map: cool ambient in the hollows, warm on the rises
        var cool = t.Ambient; var warm = t.TorchColor;
        m.SetShaderParameter("drift", new Vector3(warm.R - cool.R, warm.G - cool.G, warm.B - cool.B) * 0.05f);
        switch (t.PitKind)
        {
            case "lava":
                m.SetShaderParameter("shore_tint", new Vector3(0.36f, 0.3f, 0.3f));
                m.SetShaderParameter("shore_rough", 1.05f);
                m.SetShaderParameter("shore_glow", t.PitColor);
                m.SetShaderParameter("shore_glow_energy", 2.4f * Mathf.Max(t.PitGlow, 0.3f));
                m.SetShaderParameter("deep_color", new Color(0.03f, 0.012f, 0.006f));
                m.SetShaderParameter("deep_fade", 1.6f);
                break;
            case "water":
                m.SetShaderParameter("shore_tint", new Vector3(0.5f, 0.5f, 0.48f));
                m.SetShaderParameter("shore_rough", 0.4f);
                m.SetShaderParameter("deep_color", t.PitColor);
                m.SetShaderParameter("deep_fade", 1.4f);
                break;
            default:
                m.SetShaderParameter("shore_tint", new Vector3(0.62f, 0.62f, 0.64f));
                m.SetShaderParameter("shore_rough", 1f);
                m.SetShaderParameter("deep_color", new Color(t.PitColor.R * 0.05f, t.PitColor.G * 0.05f, t.PitColor.B * 0.05f));
                m.SetShaderParameter("deep_fade", 4.5f);
                break;
        }
        Grounds[special] = m;
        return m;
    }

    /// <summary>Wall / cliff / dressing material for one texture set.</summary>
    public static Material Wall(LevelTheme t, string set, WallFlags flags, Color tint)
    {
        var key = (set ?? "", (int)flags, tint.ToRgba32());
        if (Walls.TryGetValue(key, out var cached)) return cached;
        var shader = EnvAssets.LoadShader("env_wall");
        if (shader == null) return Fallback;
        var m = new ShaderMaterial { Shader = shader, ResourceName = "wall_" + set };
        bool glow = (flags & WallFlags.Glow) != 0;
        m.SetShaderParameter("albedo_tex", EnvAssets.Albedo(set));
        m.SetShaderParameter("normal_tex", EnvAssets.Normal(set));
        m.SetShaderParameter("arm_tex", EnvAssets.Arm(set));
        m.SetShaderParameter("noise_tex", EnvAssets.Noise);
        m.SetShaderParameter("uv_scale", 1f / Mathf.Max(EnvAssets.Info(set).SizeM, 0.05f));
        m.SetShaderParameter("triplanar", (flags & WallFlags.Tri) != 0);
        float lv = EnvAssets.LevelScale(set, t.WallLumMin, t.WallLumMax);
        var mul = V(t.WallTint) * lv;
        if (glow) mul *= 0.35f;
        else mul = new Vector3(mul.X * tint.R, mul.Y * tint.G, mul.Z * tint.B);
        m.SetShaderParameter("tint", mul);
        m.SetShaderParameter("desaturate", t.Desaturate * 0.85f);
        bool top = (flags & (WallFlags.NoTop | WallFlags.Glow)) == 0;
        m.SetShaderParameter("top_color", t.TopColor);
        m.SetShaderParameter("top_amount", top ? t.TopAmount : 0f);
        if ((flags & WallFlags.Gloss) != 0) { m.SetShaderParameter("rough_mul", 0.45f); m.SetShaderParameter("normal_strength", 0.8f); }
        else if (!Mathf.IsEqualApprox(t.WallGloss, 1f)) m.SetShaderParameter("rough_mul", Mathf.Clamp(t.WallGloss, 0.3f, 1.5f));
        if ((flags & WallFlags.Dull) != 0) { m.SetShaderParameter("rough_add", 0.25f); m.SetShaderParameter("metal_mul", 0f); }
        if (glow)
        {
            m.SetShaderParameter("emission", new Color(tint.R, tint.G, tint.B));
            m.SetShaderParameter("emission_energy", 2.6f);
        }
        else if ((flags & WallFlags.Lit) != 0)
        {
            m.SetShaderParameter("emission", new Color(tint.R, tint.G, tint.B));
            m.SetShaderParameter("emission_energy", 0.4f);
        }
        ApplyWalkMask(m);
        Walls[key] = m;
        return m;
    }

    /// <summary>Unlit out-of-bounds filler in a flat colour.</summary>
    public static Material Cap(Color c)
    {
        uint key = c.ToRgba32();
        if (Caps.TryGetValue(key, out var cached)) return cached;
        var shader = EnvAssets.LoadShader("env_cap");
        if (shader == null) return Fallback;
        var m = new ShaderMaterial { Shader = shader, ResourceName = "cap" };
        m.SetShaderParameter("color", new Color(c.R, c.G, c.B));
        ApplyWalkMask(m);
        Caps[key] = m;
        return m;
    }
}

internal readonly record struct BatchKey(string Set, int Flags, uint Tint, int Sector);
internal readonly record struct PropKey(PropDef Def, int Sector, bool Shadow);

internal struct EnvLightSpec
{
    public Vector3 Pos;
    public Color Color;
    public float Energy, Range, Flicker;     // Flicker: 1 = fire, 0.25 = slow glow
    public bool CanShadow;
}

internal struct EnvFlame
{
    public Vector3 Pos;
    public float Height, Width, Phase;
}

/// <summary>State of one level build: the field, the batches being filled and the nodes they end up under.</summary>
internal sealed class EnvBuild
{
    public readonly Level Level;
    public readonly LevelData Data;
    public readonly LevelTheme Theme;
    public readonly NavGrid Grid;
    public readonly EnvField F;
    public readonly int Seed, Quality;
    /// <summary>Walls are solid blocks with capped tops everywhere (indoors, or a city of stone outdoors).</summary>
    public readonly bool SolidWalls;
    public readonly Node3D Root, Terrain, Walls, Pits, Props;
    public readonly Stopwatch Clock = Stopwatch.StartNew();
    public readonly StringBuilder Timing = new();
    public readonly List<EnvLightSpec> Lights = new();
    public readonly List<EnvFlame> Flames = new();
    public int Triangles, MeshNodes, PropInstances, PropBatches;
    /// <summary>Spots taken by dressing the builder added on its own (extra lights): later passes keep clear of them.</summary>
    public readonly List<(Vector2 At, float Radius)> Reserved = new();
    /// <summary>The light manager of the level (set by the lights pass; the environment pass hands it the fog to steer).</summary>
    public EnvLights LightNode;
    /// <summary>Terrain height per lattice vertex (set by the terrain pass); 0 on walkable ground.</summary>
    public float[] Heights;

    private readonly Dictionary<BatchKey, EnvMesh> _batches = new();
    private readonly Dictionary<PropKey, List<Transform3D>> _props = new();

    public EnvBuild(Level level, LevelData data, LevelTheme theme)
    {
        Level = level; Data = data; Theme = theme; Grid = data.Grid;
        Seed = data.Seed;
        Quality = Gfx.QualityOf(Game.I?.Cfg);
        SolidWalls = !theme.Outdoor || theme.Border == "walls";
        F = new EnvField(Grid, data.Deco, SolidWalls ? 3 : 12);
        Root = new Node3D { Name = "Env" };
        Terrain = new Node3D { Name = "Terrain" };
        Walls = new Node3D { Name = "Walls" };
        Pits = new Node3D { Name = "Pits" };
        Props = new Node3D { Name = "Props" };
        level.AddChild(Root);
        Root.AddChild(Terrain);
        Root.AddChild(Walls);
        Root.AddChild(Pits);
        Root.AddChild(Props);
    }

    /// <summary>The mesh that collects geometry of one material in the map sector around <paramref name="world"/>.</summary>
    public EnvMesh Batch(string set, WallFlags flags, Color tint, Vector3 world) => Batch(set, flags, tint, F.SectorOf(world));

    public EnvMesh Batch(string set, WallFlags flags, Color tint, int sector)
    {
        // a component above 1 would overflow into the next channel of the packed key (and come back as another colour)
        tint = new Color(Mathf.Clamp(tint.R, 0f, 1f), Mathf.Clamp(tint.G, 0f, 1f), Mathf.Clamp(tint.B, 0f, 1f), 1f);
        var key = new BatchKey(set ?? "", (int)flags, tint.ToRgba32(), sector);
        if (!_batches.TryGetValue(key, out var m))
        {
            m = new EnvMesh((flags & WallFlags.Tri) == 0) { Smooth = (flags & WallFlags.Smooth) != 0 };
            _batches[key] = m;
        }
        m.Xf = Transform3D.Identity;
        m.UvOff = Vector2.Zero;
        return m;
    }

    public EnvMesh Batch(string set, Vector3 world) => Batch(set, WallFlags.None, Colors.White, world);

    /// <summary>Height of the terrain surface at a world position (embankments under tree lines, pit slopes).</summary>
    public float GroundY(float wx, float wz) => Heights != null ? F.Sample(Heights, wx, wz) : 0f;

    /// <summary>Queues one instance of a model; instances are merged per model and sector into MultiMeshes.</summary>
    public void AddProp(PropDef def, Transform3D xf, bool shadow)
    {
        if (def == null) return;
        var key = new PropKey(def, F.SectorOf(xf.Origin), shadow);
        if (!_props.TryGetValue(key, out var list)) { list = new List<Transform3D>(); _props[key] = list; }
        list.Add(xf);
    }

    public void AddLight(Vector3 pos, Color color, float energy, float range, float flicker, bool canShadow) =>
        Lights.Add(new EnvLightSpec { Pos = pos, Color = color, Energy = energy, Range = range, Flicker = flicker, CanShadow = canShadow });

    public void AddFlame(Vector3 pos, float height, float width, float phase) =>
        Flames.Add(new EnvFlame { Pos = pos, Height = height, Width = width, Phase = phase });

    public MeshInstance3D AddMesh(Node3D parent, string name, Mesh mesh, Material mat, bool shadow)
    {
        if (mesh == null) return null;
        if (mat != null && mesh is ArrayMesh am) am.SurfaceSetMaterial(0, mat);
        var mi = new MeshInstance3D
        {
            Name = name, Mesh = mesh,
            CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(mi);
        MeshNodes++;
        return mi;
    }

    /// <summary>Turns the collected batches, prop instances and flames into nodes.</summary>
    public void Commit()
    {
        foreach (var (key, m) in _batches)
        {
            if (m.Empty) continue;
            var flags = (WallFlags)key.Flags;
            var mesh = m.Build();
            if (mesh == null) continue;
            var mat = EnvMats.Wall(Theme, key.Set, flags, new Color(key.Tint));
            bool shadow = (flags & (WallFlags.NoShadow | WallFlags.Glow)) == 0;
            AddMesh(Walls, key.Set + "_" + key.Sector, mesh, mat, shadow);
            Triangles += m.TriangleCount;
        }
        _batches.Clear();

        foreach (var (key, list) in _props)
        {
            var def = key.Def;
            if (def == null || list.Count == 0) continue;
            EnvProps.Use(def);
            foreach (var part in def.Parts)
            {
                var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = part.Mesh, InstanceCount = list.Count };
                for (int i = 0; i < list.Count; i++) mm.SetInstanceTransform(i, list[i] * part.Local);
                var node = new MultiMeshInstance3D
                {
                    Name = def.Name + "_" + key.Sector, Multimesh = mm,
                    CastShadow = key.Shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                    ExtraCullMargin = 0.5f,
                };
                Props.AddChild(node);
                PropBatches++;
            }
            PropInstances += list.Count;
            Triangles += def.Tris * list.Count;
        }
        _props.Clear();

        CommitFlames();
    }

    private void CommitFlames()
    {
        if (Flames.Count == 0) return;
        var shader = EnvAssets.LoadShader("env_flame");
        if (shader == null) return;
        var mat = new ShaderMaterial { Shader = shader, ResourceName = "flame" };
        mat.SetShaderParameter("noise_tex", EnvAssets.Noise);
        var tc = Theme.TorchColor;
        mat.SetShaderParameter("color_edge", new Color(tc.R, tc.G * 0.62f, tc.B * 0.3f));
        mat.SetShaderParameter("color_core", new Color(1f, 0.6f + tc.G * 0.36f, 0.24f + tc.B * 0.8f));
        var quad = new QuadMesh { Size = Vector2.One };
        quad.Material = mat;

        var bySector = new Dictionary<int, List<EnvFlame>>();
        foreach (var fl in Flames)
        {
            int s = F.SectorOf(fl.Pos);
            if (!bySector.TryGetValue(s, out var l)) { l = new List<EnvFlame>(); bySector[s] = l; }
            l.Add(fl);
        }
        foreach (var (sector, l) in bySector)
        {
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = quad, InstanceCount = l.Count };
            for (int i = 0; i < l.Count; i++)
            {
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, l[i].Pos));
                mm.SetInstanceCustomData(i, new Color(l[i].Height, l[i].Phase, l[i].Width, 0f));
            }
            Props.AddChild(new MultiMeshInstance3D
            {
                Name = "Flames_" + sector, Multimesh = mm,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 2.5f,
            });
        }
    }
}
