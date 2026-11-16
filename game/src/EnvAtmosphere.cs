using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AshenAbyss;

/// <summary>
/// Light and air of a level: the fixed lights and their manager, the sun / moon (or a dim indoor fill), the
/// WorldEnvironment (sky, ambient, fog, glow, tonemap, grading), the ground mist and the weather particles.
///
/// The look aimed at is painterly darkness: most of the frame well below mid-grey in a cool ambient, warm pools of
/// fire light standing out of it, and the region's hue carried by the air rather than painted over everything:
///   · no fog at all between the lens and the play plane (a veil there flattens the whole picture);
///   · distance fog that only starts behind the hero's plane, so the top of the frame dissolves into the region hue;
///   · height fog under the floor, so pits and chasms fill with it;
///   · a slab of volumetric mist in the first metres above the ground that catches fire light and moon shafts;
///   · the sun or moon always stands behind the scene (it shines towards the lens): shadows fall forward, wet
///     ground glints, and everything gets a lit edge.
/// </summary>
internal static class EnvAtmosphere
{
    // ── calibration ──
    // Themes give light in engine units against albedos levelled to roughly 0.04 … 0.17 (linear). These factors bring
    // an open floor under ambient light alone to about 7-10 % sRGB, moon-lit ground to about 20 %, and the ground
    // 2 m from a torch to about 60 %, with the ACES curve at an exposure close to 1 so emissive flames, lava and
    // skill effects keep the brightness they were authored with against a glow threshold of 1.0.
    // The user chose the dark level (15 Nov): B as it stands for every enclosed theme, and B with the moon and the
    // ambient raised a little for everything under the sky, so open ground, tree shadows and unlit enemies can be
    // made out in moonlight (frame median about 7-10 % sRGB, p75 under 20 %) while fire and spells still dominate.
    // Level A is kept only as a reference for tests (EnvProbe: --gfx-bright).
    //   A  "as now"   unlit floor about 12-17 % sRGB, moonlit ground about 30 %, wide pools of fire light;
    //   B  "darker"   unlit ground 3-6 %, moonlit 7-11 %, unlit indoor floor 2-4 %, tight warm pools that fall off
    //                 with the square of the distance, fog well below the lit ground, mist lit only near fires.
    //                 (The ACES curve has a hard toe: scene-linear 0.004 comes out at 0.0006, i.e. 0.7 % sRGB. Ground
    //                 that should read at 5 % has to reach the tonemapper at about 0.013, three times what a straight
    //                 conversion suggests; the B gains are set for that.)
    public static bool Dark = true;
    public static float ExposureBase => Dark ? 1.0f : 1.16f;
    public static float AmbientGainOutdoor => Dark ? 1.55f : 1.2f;
    public static float AmbientGainIndoor => Dark ? 0.6f : 3.3f;
    public static float SunGain => Dark ? 0.95f : 3.9f;
    public static float FillGain => Dark ? 0.55f : 4.5f;
    public static float TorchGain => Dark ? 1.9f : 2.9f;
    public static float FogEnergy => Dark ? 0.035f : 0.55f;
    public static float SkyGain => Dark ? 0.03f : 0.22f;
    /// <summary>
    /// Factor that brings character albedo (raw 2K textures, mostly far lighter than the levelled environment) into
    /// the value band of the ground they stand on. Found by measurement (hero / monster / nearby ground luma).
    /// </summary>
    public const float ActorAlbedo = 0.8f;
    public const float MistScale = 1f, PitFogDensity = 0.55f;

    private sealed class SkyInfo
    {
        public float Mean = 0.5f;
        public Vector3 Dir;
        public bool HasDir;
    }

    private static Dictionary<string, SkyInfo> _skyInfo;
    private static readonly Dictionary<string, Sky> Skies = new();
    public static void TrimSkies() => Skies.Clear();
    private static Shader _mistShader;

    private const string MistShaderCode = @"shader_type fog;
// Ground mist: a slab of fog in the first metres above the floor (and all the way down into pits), broken into
// slow drifting banks by two octaves of world-space noise.
uniform sampler2D noise_tex : repeat_enable, filter_linear;
uniform vec3 color : source_color = vec3(0.5);
uniform vec3 glow : source_color = vec3(0.0);
uniform float density = 0.03;
uniform float top = 2.6;
uniform vec2 wind = vec2(0.5, 0.2);
void fog() {
	vec3 w = WORLD_POSITION;
	float k = 1.0 - smoothstep(top * 0.2, top, w.y);
	vec2 p = w.xz * 0.031 + wind * TIME * 0.012;
	float n1 = texture(noise_tex, p).r;
	float n2 = texture(noise_tex, p * 2.9 - wind * TIME * 0.02 + vec2(w.y * 0.09, 0.37)).r;
	float banks = smoothstep(0.3, 0.75, n1 * 0.7 + n2 * 0.45);
	float deep = smoothstep(0.0, -2.5, w.y);
	DENSITY = density * k * mix(0.18, 1.7, banks) * (1.0 + deep * 2.5);
	ALBEDO = color;
	EMISSION = glow * deep;
}";

    // ═════════════════════════════════════ lights ═════════════════════════════════════

    public static void BuildLights(EnvBuild b)
    {
        if (b == null || b.Root == null) return;
        var t = b.Theme;
        var node = new EnvLights { Name = "Lights" };
        b.Root.AddChild(node);
        DirectionalLight3D sun = null;
        try { sun = MakeSun(b); node.AddChild(sun); }
        catch (Exception e) { Log.Error("env", "building the sun / fill light failed", e); }

        Vector3 start = Vector3.Zero;
        try
        {
            var poi = b.Data?.Find("start");
            if (poi != null) start = new Vector3(poi.Pos.X, 0f, poi.Pos.Y);
            else if (b.Grid != null) start = new Vector3(b.Grid.Origin.X + b.Grid.W * NavGrid.Cell * 0.5f, 0f, b.Grid.Origin.Y + b.Grid.H * NavGrid.Cell * 0.5f);
        }
        catch (Exception e) { Log.Error("env", "no start position for the light manager", e); }
        node.Setup(b.Lights, t, b.Quality, TorchGain, start, t.Outdoor ? sun : null);
        b.LightNode = node;
        Log.Debug("env", $"lights: {b.Lights.Count} omni ({node.ActiveCount} on at the start), {(t.Outdoor ? "sun with shadows" : "indoor fill")}");
    }

    /// <summary>
    /// The direction the sun or moon shines in. Whatever bearing a theme gives, the light is turned so that it comes
    /// from behind the scene towards the camera (which always looks north along −Z), at the theme's elevation.
    /// </summary>
    public static Vector3 SunDir(LevelTheme t)
    {
        var d = t.SunDir;
        if (d.LengthSquared() < 1e-4f) d = new Vector3(-0.45f, -0.72f, 0.4f);
        d = d.Normalized();
        float side = Mathf.Abs(d.X) < 0.25f ? (d.X < 0f ? -0.25f : 0.25f) : d.X;
        var flat = new Vector2(side, Mathf.Abs(d.Z) * 0.75f + 0.18f).Normalized();        // always some +Z: towards the lens
        float elev = Mathf.DegToRad(t.SunElevation > 1f ? Mathf.Clamp(t.SunElevation, 18f, 80f) : Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(-d.Y, 0.3f, 0.98f))));
        return new Vector3(flat.X * Mathf.Cos(elev), -Mathf.Sin(elev), flat.Y * Mathf.Cos(elev)).Normalized();
    }

    /// <summary>Outdoors the sun or moon with cascaded shadows; indoors a very dim shadowless fill so unlit rooms are not pure black.</summary>
    private static DirectionalLight3D MakeSun(EnvBuild b)
    {
        var t = b.Theme;
        var p = Gfx.PresetOf(Game.I?.Cfg);
        Vector3 dir = SunDir(t);
        var xf = new Transform3D(Basis.LookingAt(dir, Mathf.Abs(dir.Y) > 0.98f ? Vector3.Forward : Vector3.Up), Vector3.Zero);
        if (!t.Outdoor)
        {
            // straight down, so floors get a breath of it and wall faces none: rooms keep their depth
            var down = new Transform3D(Basis.LookingAt(new Vector3(0.12f, -1f, 0.2f).Normalized(), Vector3.Forward), Vector3.Zero);
            return new DirectionalLight3D
            {
                Name = "Fill", Transform = down, LightColor = t.SunColor, LightEnergy = Mathf.Max(t.SunEnergy, 0.02f) * FillGain,
                ShadowEnabled = false, LightSpecular = 0f, LightVolumetricFogEnergy = 0.35f,
                SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly, LightBakeMode = Light3D.BakeMode.Disabled,
            };
        }
        float far = CameraRig.FarGround(1f) + 6f;
        var sun = new DirectionalLight3D
        {
            Name = "Sun", Transform = xf, LightColor = t.SunColor, LightEnergy = Mathf.Max(t.SunEnergy, 0f) * SunGain,
            LightSpecular = 0.3f, LightVolumetricFogEnergy = Dark ? 0.4f : 1.6f,
            SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly, LightBakeMode = Light3D.BakeMode.Disabled,
            ShadowEnabled = true, ShadowBias = 0.05f, ShadowNormalBias = 1.4f, ShadowBlur = 1.1f, ShadowOpacity = 0.96f,
            // The camera sees ground between about 10 m and 27 m away (48 m fully zoomed out) and nothing nearer; the
            // light manager keeps the range matched to the zoom, so the cascades are spent on what is on screen.
            DirectionalShadowMaxDistance = far,
            DirectionalShadowFadeStart = 0.9f,
            DirectionalShadowPancakeSize = 30f,
            DirectionalShadowBlendSplits = p.Splits >= 2,
            LightAngularDistance = b.Quality >= 3 ? 0.7f : 0f,       // penumbra search costs a blocker pass per pixel: top preset only
        };
        if (p.Splits >= 4)
        {
            sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
            sun.DirectionalShadowSplit1 = 0.3f;
            sun.DirectionalShadowSplit2 = 0.5f;
            sun.DirectionalShadowSplit3 = 0.72f;
        }
        else if (p.Splits >= 2)
        {
            sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits;
            sun.DirectionalShadowSplit1 = 0.6f;
        }
        else sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal;
        return sun;
    }

    // ═════════════════════════════════════ environment ═════════════════════════════════════

    public static void BuildEnvironment(EnvBuild b)
    {
        if (b == null || b.Level == null) return;
        var env = new Godot.Environment();
        try { Configure(b, env); }
        catch (Exception e) { Log.Error("env", "configuring the environment failed; keeping what was set so far", e); }
        try { Gfx.TuneEnvironment(env, Game.I?.Cfg); }
        catch (Exception e) { Log.Error("env", "quality tuning of the environment failed", e); }

        var old = b.Level.Env;
        if (old != null && GodotObject.IsInstanceValid(old)) old.QueueFree();
        var we = new WorldEnvironment { Name = "WorldEnvironment", Environment = env };
        (b.Root ?? (Node)b.Level).AddChild(we);
        b.Level.Env = we;
        if (b.LightNode != null) b.LightNode.Air = env;

        try { BuildMist(b); }
        catch (Exception e) { Log.Error("env", "building the ground mist failed", e); }
        try { ApplyLens(b); }
        catch (Exception e) { Log.Error("env", "setting the lens for the level failed", e); }
    }

    /// <summary>Vignette and the back light that follows the hero belong to the camera rig; every level sets their colour.</summary>
    private static void ApplyLens(EnvBuild b)
    {
        var t = b.Theme;
        var rig = Game.I?.Cam;
        if (rig == null || !GodotObject.IsInstanceValid(rig)) return;
        Color fog = t.FogColor;
        rig.VignetteTint = new Color(fog.R * 0.05f, fog.G * 0.05f, fog.B * 0.06f);
        rig.Vignette = t.Vignette;
        Color rim = t.RimColor.R + t.RimColor.G + t.RimColor.B > 0.02f ? t.RimColor : t.SunColor.Lerp(new Color(0.75f, 0.85f, 1f), 0.5f);
        rig.SetRim(rim, 0f);          // no light follows the lens: what lights a figure is what lights the ground under it

        // characters share the grading of the level: theme tint, levelling into the environment's albedo band, some
        // of its desaturation; and a barely visible cool edge so a figure in shadow keeps a silhouette
        var pt = t.PropTint;
        CharModel.SetSceneGrade(new Color(pt.R, pt.G, pt.B) * (ActorAlbedo * Mathf.Max(t.ActorFill, 0f)), t.Desaturate * 0.45f, rim * (0.05f * t.ActorRim));
    }

    private static Color MistColor(LevelTheme t)
    {
        if (t.MistColor.R + t.MistColor.G + t.MistColor.B > 0.02f) return t.MistColor;
        Color f = t.FogColor;
        float m = Mathf.Max(f.R, Mathf.Max(f.G, f.B));
        if (m < 0.01f) return new Color(0.6f, 0.6f, 0.6f);
        // scattering albedo: the hue of the region's air, bright enough to pick up whatever light there is
        return new Color(f.R / m, f.G / m, f.B / m).Lerp(Colors.White, 0.25f) * 0.85f;
    }

    /// <summary>The ground mist: one world-sized fog volume whose shader keeps the density near the floor.</summary>
    private static void BuildMist(EnvBuild b)
    {
        var t = b.Theme;
        if (t.MistDensity <= 0.0005f || b.Root == null) return;
        _mistShader ??= new Shader { Code = MistShaderCode };
        var mat = new ShaderMaterial { Shader = _mistShader };
        mat.SetShaderParameter("noise_tex", EnvAssets.Noise);
        mat.SetShaderParameter("color", MistColor(t));
        mat.SetShaderParameter("density", t.MistDensity * MistScale);
        mat.SetShaderParameter("top", Mathf.Clamp(t.MistTop, 0.6f, 8f));
        mat.SetShaderParameter("wind", new Vector2(t.Wind.X, t.Wind.Y));
        // glowing pits (lava, the rift) light the mist that hangs over them from inside
        float glow = EnvTerrain.PitKind(t) == "lava" ? 0.5f * t.PitGlow : EnvTerrain.PitKind(t) == "void" ? 0.12f * t.PitGlow : 0f;
        mat.SetShaderParameter("glow", new Color(t.PitColor.R * glow, t.PitColor.G * glow, t.PitColor.B * glow));
        var fv = new FogVolume { Name = "Mist", Shape = RenderingServer.FogVolumeShape.World, Material = mat };
        b.Root.AddChild(fv);
    }

    private static void Configure(EnvBuild b, Godot.Environment env)
    {
        var t = b.Theme;
        Color fog = t.FogColor;

        // ── background, ambient, reflections ──
        Sky sky = t.Outdoor ? LoadSky(t.Sky) : null;
        string background;
        if (sky != null)
        {
            var info = SkyData(t.Sky);
            float mean = Mathf.Clamp(info?.Mean ?? 0.5f, 0.05f, 4f);
            env.BackgroundMode = Godot.Environment.BGMode.Sky;
            env.Sky = sky;
            // the HDRs differ fourfold in brightness: bring them all to the same dim level, then let the theme scale it
            env.BackgroundEnergyMultiplier = Mathf.Clamp(t.SkyEnergy, 0f, 2f) * SkyGain / mean;
            if (info != null && info.HasDir)
            {
                // turn the panorama so its bright side lies where the sun or moon stands
                Vector3 toLight = -SunDir(t);
                float yaw = Mathf.Atan2(toLight.X, toLight.Z) - Mathf.Atan2(info.Dir.X, info.Dir.Z);
                env.SkyRotation = new Vector3(0f, yaw, 0f);
            }
            env.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
            env.AmbientLightSkyContribution = Dark ? 0f : 0.15f;
            env.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
            background = $"sky '{t.Sky}' x{env.BackgroundEnergyMultiplier:0.00}";
        }
        else
        {
            // never on screen in practice (the lid and the terrain apron cover everything); also what polished metal reflects
            env.BackgroundMode = Godot.Environment.BGMode.Color;
            env.BackgroundColor = new Color(fog.R * 0.05f, fog.G * 0.05f, fog.B * 0.05f);       // also what still water mirrors: keep it dark
            env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            env.ReflectedLightSource = Godot.Environment.ReflectionSource.Bg;
            background = "flat colour";
        }
        // the ambient carries the hue of the region, but thinly: a saturated fill paints the whole frame one colour
        float ambLum = t.Ambient.R * 0.3f + t.Ambient.G * 0.59f + t.Ambient.B * 0.11f;
        env.AmbientLightColor = t.Ambient.Lerp(new Color(ambLum, ambLum, ambLum), 0.3f);
        env.AmbientLightEnergy = Mathf.Max(t.AmbientEnergy, 0f) * (t.Outdoor ? AmbientGainOutdoor : AmbientGainIndoor);

        // ── fog: nothing in front of the play plane, the region hue behind it and under it ──
        float begin = CameraRig.BaseDistance + 5f;
        env.FogEnabled = true;
        env.FogMode = Godot.Environment.FogModeEnum.Depth;
        env.FogDepthBegin = begin;
        env.FogDepthEnd = begin + 46f;
        env.FogDepthCurve = 0.85f;
        env.FogDensity = Mathf.Clamp(t.FogFar, 0f, 1f);
        env.FogLightColor = fog;
        env.FogLightEnergy = FogEnergy;
        env.FogSunScatter = 0f;
        env.FogAerialPerspective = 0f;
        env.FogSkyAffect = 1f;
        env.FogHeight = -0.35f;                               // only what lies below the floor: pit walls sink into it
        // No height fog: Godot has one fog colour for everything, and at the brightness the far background needs it
        // turned every pit into a flat bright slab. Pit sides fade to their deep colour in the ground shader instead.
        env.FogHeightDensity = 0f;

        // volumetric fog (switched on by Gfx from 中): the global density is zero, the mist volume supplies it
        env.VolumetricFogDensity = 0f;
        env.VolumetricFogAlbedo = Colors.White;
        env.VolumetricFogEmission = Colors.Black;
        env.VolumetricFogEmissionEnergy = 0f;
        env.VolumetricFogAnisotropy = 0.55f;                  // forward scattering: the back light blooms in the mist towards the lens
        env.VolumetricFogLength = CameraRig.FarGround(1f) + 6f;
        env.VolumetricFogDetailSpread = 0.9f;                 // nothing is near the camera: spend the slices where the ground is
        env.VolumetricFogAmbientInject = Dark ? 0f : 0.35f;      // B: the mist shows only where a fire lights it
        env.VolumetricFogSkyAffect = 0f;
        env.VolumetricFogGIInject = 1f;

        // ── glow: emissives only (flames, lava, embers, skill effects) ──
        env.GlowEnabled = true;
        env.GlowIntensity = Mathf.Clamp(t.GlowIntensity, 0f, 2f);
        env.GlowStrength = 1f;
        env.GlowBloom = 0f;
        env.GlowHdrThreshold = 1.05f;
        env.GlowHdrScale = 2f;
        env.GlowHdrLuminanceCap = 12f;
        env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen;
        env.SetGlowLevel(0, 0f);
        env.SetGlowLevel(1, 0.2f);
        env.SetGlowLevel(2, 0.6f);
        env.SetGlowLevel(3, 0.45f);
        env.SetGlowLevel(4, 0.5f);
        env.SetGlowLevel(5, 0.22f);
        env.SetGlowLevel(6, 0f);

        // ── tonemap and grading ──
        env.TonemapMode = Godot.Environment.ToneMapper.Aces;
        env.TonemapExposure = ExposureBase * Mathf.Clamp(t.Exposure, 0.4f, 2.5f);
        env.SetMeta("exposure_base", env.TonemapExposure);        // Gfx applies the player's 亮度 setting on top of this
        env.TonemapWhite = 7f;                             // fire-lit stone rolls off softly instead of clipping
        env.AdjustmentEnabled = true;
        env.AdjustmentBrightness = 1f;
        // (contrast pivots on 50 % display value: at 1.08 it takes four points off everything, which is the whole of a
        // 4 % shadow. The dark level keeps its blacks through the tone curve alone.)
        env.AdjustmentContrast = Dark ? 1f : Mathf.Clamp(t.Contrast, 0.8f, 1.4f);
        env.AdjustmentSaturation = Mathf.Clamp(t.Saturation, 0.3f, 1.4f);
        if (t.Grade > 0.001f) env.AdjustmentColorCorrection = GradeRamp(t);

        // ── screen-space shading (switched by Gfx according to the quality preset) ──
        env.SsaoRadius = 1.1f;
        env.SsaoIntensity = 2.6f;
        env.SsaoPower = 1.6f;
        env.SsaoDetail = 0.5f;
        env.SsaoHorizon = 0.06f;
        env.SsaoSharpness = 0.98f;
        env.SsaoLightAffect = 0.35f;                       // the scene is lit by local fires: AO has to bite into direct light a little
        env.SsaoAOChannelAffect = 0.3f;
        env.SsilRadius = 4f;
        env.SsilIntensity = 1.1f;
        env.SsilSharpness = 0.98f;
        env.SsilNormalRejection = 1f;

        Log.Info("env", $"environment '{t.Id}': {background}, ambient x{env.AmbientLightEnergy:0.00}, far fog {env.FogDensity:0.00} from {env.FogDepthBegin:0} m, mist {t.MistDensity:0.000}/m to {t.MistTop:0.0} m, " +
            $"exposure {env.TonemapExposure:0.00}, saturation {env.AdjustmentSaturation:0.00}, contrast {env.AdjustmentContrast:0.00}, grade {t.Grade:0.00}");
    }

    /// <summary>
    /// Split toning as a 1-D colour-correction ramp: shadows lean to the hue of the region's ambient light, highlights
    /// to the fire colour. Black and white stay where they are.
    /// </summary>
    private static Texture2D GradeRamp(LevelTheme t)
    {
        static Vector3 Hue(Color c)
        {
            float m = (c.R + c.G + c.B) / 3f;
            var v = new Vector3(c.R - m, c.G - m, c.B - m);
            float len = v.Length();
            return len > 1e-4f ? v / len : Vector3.Zero;
        }
        float s = Mathf.Clamp(t.Grade, 0f, 0.2f);
        Vector3 lo = Hue(t.Ambient) * s, hi = Hue(t.TorchColor) * (s * 0.6f);
        Color At(float v, Vector3 shift, float w) => new(Mathf.Clamp(v + shift.X * w, 0f, 1f), Mathf.Clamp(v + shift.Y * w, 0f, 1f), Mathf.Clamp(v + shift.Z * w, 0f, 1f), 1f);
        var g = new Gradient
        {
            InterpolationMode = Gradient.InterpolationModeEnum.Cubic,
            Offsets = new[] { 0f, 0.12f, 0.3f, 0.55f, 0.8f, 1f },
            Colors = new[] { new Color(0f, 0f, 0f, 1f), At(0.12f, lo, 0.8f), At(0.3f, lo, 1f), At(0.55f, lo * 0.25f + hi * 0.4f, 1f), At(0.8f, hi, 1f), new Color(1f, 1f, 1f, 1f) },
        };
        return new GradientTexture1D { Gradient = g, Width = 256 };
    }

    private static void LoadSkyManifest()
    {
        _skyInfo = new Dictionary<string, SkyInfo>();
        string path = EnvAssets.SkyRoot + "manifest.json";
        try
        {
            if (!Godot.FileAccess.FileExists(path)) { Log.Warn("env", $"sky manifest missing ({path}); assuming a mean luminance of 0.5"); return; }
            using var fa = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (fa == null) { Log.Warn("env", $"sky manifest unreadable ({path})"); return; }
            using var doc = JsonDocument.Parse(fa.GetAsText());
            if (!doc.RootElement.TryGetProperty("skies", out var skies) || skies.ValueKind != JsonValueKind.Array) return;
            foreach (var s in skies.EnumerateArray())
            {
                if (!s.TryGetProperty("name", out var nm) || nm.ValueKind != JsonValueKind.String) continue;
                var si = new SkyInfo();
                if (s.TryGetProperty("mean_luminance", out var ml) && ml.ValueKind == JsonValueKind.Number) si.Mean = (float)ml.GetDouble();
                if (s.TryGetProperty("light_dir_godot", out var ld) && ld.ValueKind == JsonValueKind.Array && ld.GetArrayLength() >= 3)
                {
                    si.Dir = new Vector3((float)ld[0].GetDouble(), (float)ld[1].GetDouble(), (float)ld[2].GetDouble());
                    si.HasDir = si.Dir.X * si.Dir.X + si.Dir.Z * si.Dir.Z > 0.01f;      // straight overhead has no bearing
                }
                _skyInfo[nm.GetString()] = si;
            }
            Log.Info("env", $"sky manifest: {_skyInfo.Count} skies");
        }
        catch (Exception e) { Log.Error("env", "reading the sky manifest failed", e); }
    }

    private static SkyInfo SkyData(string name)
    {
        if (_skyInfo == null) LoadSkyManifest();
        return name != null && _skyInfo.TryGetValue(name, out var s) ? s : null;
    }

    /// <summary>A shared Sky for res://assets/env/sky/&lt;name&gt;.hdr, or null (logged once) when there is no such file.</summary>
    private static Sky LoadSky(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (Skies.TryGetValue(name, out var cached)) return cached;
        string path = EnvAssets.SkyRoot + name + ".hdr";
        Sky sky = null;
        try
        {
            Texture2D tex = null;
            if (ResourceLoader.Exists(path)) tex = GD.Load<Texture2D>(path);
            else if (Godot.FileAccess.FileExists(path))
            {
                // present but not imported yet (first run from loose files)
                var img = Image.LoadFromFile(path);
                if (img != null && !img.IsEmpty()) tex = ImageTexture.CreateFromImage(img);
            }
            if (tex != null)
            {
                var mat = new PanoramaSkyMaterial { Panorama = tex, Filter = true };
                sky = new Sky { SkyMaterial = mat, RadianceSize = Sky.RadianceSizeEnum.Size64, ProcessMode = Sky.ProcessModeEnum.Incremental };
            }
        }
        catch (Exception e)
        {
            Log.Error("env", $"loading sky {path} failed", e);
            sky = null;
        }
        if (sky == null && EnvAssets.LogOnce("sky:" + name)) Log.Warn("env", $"sky '{name}' is missing ({path}); using a flat dark background");
        Skies[name] = sky;
        return sky;
    }

    // ═════════════════════════════════════ weather ═════════════════════════════════════

    public static void BuildWeather(EnvBuild b)
    {
        if (b == null || b.Root == null) return;
        var t = b.Theme;
        string kind = (t.Weather ?? "none").Trim().ToLowerInvariant();
        if (kind.Length == 0 || kind == "none" || t.WeatherAmount <= 0.01f) return;

        Vector2 w = t.Wind;
        float wind = w.Length();
        int amount;
        float life, sizeMin, sizeMax, speedMin, speedMax, spread, turbulence, alpha, bright, centreY;
        Vector3 vel, gravity, box;
        bool additive, streak = false;
        // The box is the slab of air the lens looks through around the hero (about 21 m wide and 15 m deep at zoom 1,
        // more when pulled back): big enough for the zoomed-out view, dense enough for the close one.
        switch (kind)
        {
            case "ash":         // grey flakes sinking through the wind
                amount = 380; life = 9f; sizeMin = 0.04f; sizeMax = 0.1f; additive = false; alpha = 0.7f; bright = 0.9f;
                vel = new Vector3(w.X, -0.55f, w.Y); speedMin = 0.6f; speedMax = 1.2f; spread = 25f;
                gravity = new Vector3(0f, -0.12f, 0f); turbulence = 0.7f; box = new Vector3(19f, 4f, 13f); centreY = 5f;
                break;
            case "snow":        // a blizzard in the pass, a slow fall in the frozen halls
            {
                float fall = 0.9f + Mathf.Min(wind, 4f) * 0.33f;
                amount = 560; life = Mathf.Clamp(9f / fall, 3.5f, 8f); additive = false; alpha = 0.9f; bright = 1f;
                streak = wind > 3f;
                sizeMin = streak ? 0.1f : 0.04f; sizeMax = streak ? 0.24f : 0.09f;
                vel = new Vector3(w.X, -fall, w.Y); speedMin = 0.8f; speedMax = 1.2f; spread = streak ? 9f : 18f;
                gravity = new Vector3(0f, -0.3f, 0f); turbulence = streak ? 0.5f : 0.35f; box = new Vector3(19f, 4f, 13f); centreY = 5.5f;
                break;
            }
            case "embers":      // sparks rising off the hot ground
                amount = 240; life = 5f; sizeMin = 0.03f; sizeMax = 0.07f; additive = true; alpha = 1f; bright = 2.2f;
                vel = new Vector3(w.X * 0.6f, 0.55f, w.Y * 0.6f); speedMin = 0.5f; speedMax = 1.4f; spread = 40f;
                gravity = new Vector3(0f, 0.25f, 0f); turbulence = 1.1f; box = new Vector3(18f, 2f, 12f); centreY = 2.2f;
                break;
            case "sand":        // streaks of grit blown flat over the dunes
                if (wind < 1f) { w = new Vector2(4f, 1.2f); wind = w.Length(); }
                amount = 460; life = 2.4f; sizeMin = 0.16f; sizeMax = 0.34f; additive = false; alpha = 0.42f; bright = 1f; streak = true;
                vel = new Vector3(w.X, -0.2f, w.Y); speedMin = 0.8f; speedMax = 1.2f; spread = 7f;
                gravity = Vector3.Zero; turbulence = 0.5f; box = new Vector3(21f, 2f, 14f); centreY = 2.1f;
                break;
            case "spores":      // glowing motes adrift over the mire
                amount = 280; life = 8f; sizeMin = 0.035f; sizeMax = 0.085f; additive = true; alpha = 0.85f; bright = 1.6f;
                vel = new Vector3(w.X, 0.1f, w.Y); speedMin = 0.3f; speedMax = 1.1f; spread = 180f;
                gravity = new Vector3(0f, 0.03f, 0f); turbulence = 0.9f; box = new Vector3(18f, 2.2f, 12f); centreY = 2.4f;
                break;
            default:            // "dust": motes hanging in still air, lit where they cross a fire's light
                if (kind != "dust" && EnvAssets.LogOnce("weather:" + kind)) Log.Warn("env", $"unknown weather '{kind}' in theme '{t.Id}'; using dust motes");
                amount = 200; life = 10f; sizeMin = 0.025f; sizeMax = 0.06f; additive = true; alpha = 0.6f; bright = 0.9f;
                vel = new Vector3(w.X, -0.03f, w.Y); speedMin = 0.3f; speedMax = 1.2f; spread = 180f;
                gravity = new Vector3(0f, -0.01f, 0f); turbulence = 0.5f; box = new Vector3(18f, 2.2f, 12f); centreY = 2.4f;
                break;
        }

        float quality = Gfx.PresetOf(Game.I?.Cfg).Particles;
        amount = Mathf.Clamp(Mathf.RoundToInt(amount * Mathf.Clamp(t.WeatherAmount, 0f, 1.5f) * quality), 0, 800);
        if (amount < 8) return;

        float speed = Mathf.Max(vel.Length(), 0.12f);
        Vector3 dir = vel.LengthSquared() > 1e-5f ? vel.Normalized() : Vector3.Down;
        Color c = t.WeatherColor;

        Texture2D dot = null;
        try { dot = Fx.SoftDot; }
        catch (Exception e) { Log.Warn("env", "Fx.SoftDot unavailable, using the environment's own dot: " + e.Message); }
        dot ??= EnvAssets.SoftDot;

        var ramp = new Gradient
        {
            Offsets = new[] { 0f, 0.12f, 0.72f, 1f },
            Colors = new[] { new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0f) },
        };
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = box,
            Direction = dir, Spread = spread, InitialVelocityMin = speed * speedMin, InitialVelocityMax = speed * speedMax,
            Gravity = gravity, ScaleMin = sizeMin, ScaleMax = sizeMax, LifetimeRandomness = 0.35f,
            Color = new Color(c.R, c.G, c.B, alpha), ColorRamp = new GradientTexture1D { Gradient = ramp, Width = 64 },
        };
        if (turbulence > 0f)
        {
            pm.TurbulenceEnabled = true;
            pm.TurbulenceNoiseStrength = turbulence;
            pm.TurbulenceNoiseScale = 3.5f;
            pm.TurbulenceInfluenceMin = 0.05f;
            pm.TurbulenceInfluenceMax = 0.18f;
        }

        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoTexture = dot, AlbedoColor = new Color(bright, bright, bright),
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, DisableReceiveShadows = true, DisableFog = true,
            // flakes right in front of the lens would be blurred discs the size of a head: let them fade out as they come close
            ProximityFadeEnabled = false, DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha, DistanceFadeMinDistance = 3f, DistanceFadeMaxDistance = 7f,
        };
        if (!streak)
        {
            mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
            mat.BillboardKeepScale = true;
        }
        var quad = new QuadMesh { Size = streak ? new Vector2(0.3f, 1f) : Vector2.One, Material = mat };

        var p = new GpuParticles3D
        {
            Name = "Particles", ProcessMaterial = pm, DrawPass1 = quad, Amount = amount, Lifetime = life,
            Preprocess = Mathf.Min(life, 4f), LocalCoords = false, FixedFps = 30, Interpolate = true,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, GIMode = GeometryInstance3D.GIModeEnum.Disabled,
            VisibilityAabb = new Aabb(-box.X - 16f, -box.Y - 14f, -box.Z - 16f, (box.X + 16f) * 2f, (box.Y + 14f) * 2f, (box.Z + 16f) * 2f),
        };
        if (streak) p.TransformAlign = GpuParticles3D.TransformAlignEnum.ZBillboardYToVelocity;

        // emit upwind of the view so the particles are crossing it for most of their life; the view centre lies a little north of the hero
        float lead = life * 0.4f;
        var follow = new EnvFollow
        {
            Name = "Weather",
            Offset = new Vector3(Mathf.Clamp(-w.X * lead, -10f, 10f), centreY, -2.5f + Mathf.Clamp(-w.Y * lead, -8f, 8f)),
        };
        follow.AddChild(p);
        b.Root.AddChild(follow);
        Log.Debug("env", $"weather: {kind} x{amount} life={life:0.0}s wind=({w.X:0.0},{w.Y:0.0}) {(additive ? "additive" : "alpha")}{(streak ? " streaks" : "")}");
    }
}
