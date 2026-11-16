using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// Graphics quality. Turns <see cref="Settings"/> (quality preset 0..3 = 低/中/高/極致 and render scale) into viewport,
/// shadow and environment state. The 3D scene is rendered below window resolution and upscaled whenever the scale
/// is under 1.0; the automatic scale aims at a number of internal lines per preset (1440 at 高, which is 0.67 on a
/// 4K display).
///
/// Measured on the Radeon 8060S at 4K output: FSR 2 alone costs 9-14 ms of GPU time, more than everything else in
/// the frame, so no preset uses it (the code path stays for the harness: --gfx-up=2). 中, 高 and 極致 resolve
/// temporally at the internal resolution (TAA, 1-3 ms) and upscale with the spatial FSR 1 pass (about 1.5 ms): thin
/// branches and the dithered see-through cut still hold still, and the frame fits in 16.7 ms.
///
/// What each preset buys (see <see cref="Preset"/>):
///   低   1080 lines, FSR 1 + FXAA, one shadow cascade, no fire-light shadows, no SSAO / mist, half the particles.
///   中   1296 lines, TAA + FSR 1, sun shadows 2048, 1 shadow-casting fire, SSAO, thin ground mist (48³ froxels).
///   高   1440 lines, TAA + FSR 1, sun shadows 2048, 2 shadow-casting fires, SSAO, ground mist with light
///        shafts (64³).
///   (The lens only shows ground between 10 m and 27 m away, so one 2048 shadow map over that range already has
///   texels of about 2 cm; cascades are only worth their second scene pass at the top preset.)
///   With the render scale on automatic a governor (see <see cref="Govern"/>) trims the internal resolution, never
///   below 72 % of the figures above, whenever the GPU needs more than 14 ms for the 3D picture.
///   極致 1620 lines, TAA + FSR 1, two cascades at 4096 with penumbrae, 3 fires with soft shadows, high SSAO, SSIL,
///        screen-space reflections on wet floors, SDFGI bounce from lava and fires, 96³ mist, denser dressing and weather.
/// </summary>
public static class Gfx
{
    public const int QualityCount = 4;
    public static readonly string[] QualityNames = { "低", "中", "高", "極致" };
    public static string QualityName(int q) => QualityNames[Math.Clamp(q, 0, QualityCount - 1)];

    /// <summary>Everything a quality level decides. Level code reads the current one through <see cref="P"/>.</summary>
    public sealed class Preset
    {
        public float TargetLines;                   // internal resolution the automatic render scale aims at
        public int Upscale;                         // 0 = FSR 1 + FXAA, 1 = TAA at internal resolution + FSR 1, 2 = FSR 2 (temporal upscaler and anti-aliasing in one)
        public bool Fsr2 => Upscale >= 2;
        // The sun only has to shadow the 27 m (zoomed out: 48 m) of ground the lens shows, so a 2048 atlas already gives
        // texels of one to three centimetres; bigger atlases only cost fill and bandwidth on an integrated GPU.
        public int DirAtlas, PosAtlas, Splits;      // directional shadow atlas, positional atlas, cascades (1, 2 or 4)
        public RenderingServer.ShadowQuality Soft;
        public int OmniShadows;                     // fire lights that may cast shadows at once
        public bool Ssao, Ssil, Ssr, Sdfgi, Mist;   // Mist = volumetric fog (ground mist, halos, shafts)
        public int FogSize, FogDepth;               // froxel grid of the volumetric fog
        public float Lod;                           // mesh LOD threshold in pixels
        public float Particles;                     // multiplier on effect and weather particle counts
        public float Dressing;                      // multiplier on optional set dressing (ground clutter, border trees)
        public int Aniso;                           // 0 = off, 1 = 2×, 2 = 4×, 3 = 8×, 4 = 16×
    }

    private static readonly Preset[] Presets =
    {
        new() { TargetLines = 1080f, Upscale = 0, DirAtlas = 2048, PosAtlas = 2048, Splits = 1, Soft = RenderingServer.ShadowQuality.SoftVeryLow, OmniShadows = 0,
                Ssao = false, Ssil = false, Ssr = false, Sdfgi = false, Mist = false, FogSize = 48, FogDepth = 32, Lod = 6f, Particles = 0.5f, Dressing = 0.6f, Aniso = 1 },
        new() { TargetLines = 1296f, Upscale = 1, DirAtlas = 2048, PosAtlas = 2048, Splits = 1, Soft = RenderingServer.ShadowQuality.SoftLow, OmniShadows = 1,
                Ssao = true, Ssil = false, Ssr = false, Sdfgi = false, Mist = true, FogSize = 48, FogDepth = 48, Lod = 4f, Particles = 0.75f, Dressing = 0.8f, Aniso = 2 },
        new() { TargetLines = 1440f, Upscale = 1, DirAtlas = 2048, PosAtlas = 2048, Splits = 1, Soft = RenderingServer.ShadowQuality.SoftLow, OmniShadows = 2,
                Ssao = true, Ssil = false, Ssr = false, Sdfgi = false, Mist = true, FogSize = 64, FogDepth = 64, Lod = 2.5f, Particles = 1f, Dressing = 1f, Aniso = 2 },
        new() { TargetLines = 1620f, Upscale = 1, DirAtlas = 4096, PosAtlas = 4096, Splits = 2, Soft = RenderingServer.ShadowQuality.SoftMedium, OmniShadows = 3,
                Ssao = true, Ssil = true, Ssr = true, Sdfgi = true, Mist = true, FogSize = 80, FogDepth = 80, Lod = 1.5f, Particles = 1.3f, Dressing = 1.25f, Aniso = 3 },
    };

    /// <summary>Quality preset in use after the last <see cref="Apply"/>.</summary>
    public static int Quality { get; private set; } = 2;
    /// <summary>The preset in use.</summary>
    public static Preset P => Presets[Math.Clamp(Quality, 0, QualityCount - 1)];
    /// <summary>3D render scale in use after the last <see cref="Apply"/> (1 = native).</summary>
    public static float Scale { get; private set; } = 1f;
    /// <summary>Internal 3D resolution after scaling.</summary>
    public static Vector2I Internal { get; private set; }
    public static bool Upscaling => Scale < 0.99f;

    /// <summary>Test harness: a quality level that replaces the saved setting without touching it (−1 = none).</summary>
    public static int QualityOverride = -1;
    /// <summary>Test harness: a render scale that replaces the saved setting without touching it (0 = none).</summary>
    public static float ScaleOverride;
    /// <summary>Test harness: an upscaler (0, 1, 2 as in <see cref="Preset.Upscale"/>) that replaces the one of the preset (−1 = none).</summary>
    public static int UpscaleOverride = -1;

    private static int _globalQ = -1;
    private static string _lastSig;

    // ── automatic render scale: the governor ──
    /// <summary>GPU milliseconds per frame above which the internal resolution is lowered, and below which it may rise again.</summary>
    public const float GovernHigh = 14.0f, GovernLow = 10.2f;
    /// <summary>The governor never takes the scale below this share of what the preset asked for (and never below 0.5 of the output).</summary>
    public const float GovernFloor = 0.72f;
    /// <summary>Factor the governor currently applies on top of the automatic scale (1 = none).</summary>
    public static float Dynamic { get; private set; } = 1f;
    /// <summary>Test harness: switch the governor off (fixed scale for measurements).</summary>
    public static bool GovernorOff;
    /// <summary>Test harness: the viewport that draws the 3D picture when it is not the window itself.</summary>
    public static Viewport GovernTarget;
    private static Rid _govRid;
    private static double _govT, _govSum, _govQuiet, _govWarm = 4.0;
    private static int _govN;

    /// <summary>
    /// Called once a frame while playing (by the camera rig). With the render scale on automatic it watches the GPU
    /// time of the 3D viewport and trims the internal resolution in small steps when a scene does not fit a 60 Hz
    /// frame, then gives it back when there is room again. A fixed scale chosen in the settings is never touched.
    /// </summary>
    public static void Govern(double realDelta, Viewport vp, Settings s)
    {
        vp = GovernTarget != null && GodotObject.IsInstanceValid(GovernTarget) ? GovernTarget : vp;
        if (vp == null || s == null) return;
        try
        {
            bool auto = !GovernorOff && s.RenderScale < 0.05f && ScaleOverride < 0.05f;
            if (!auto)
            {
                if (Dynamic != 1f) { Dynamic = 1f; Apply(s, vp); }
                return;
            }
            var rid = vp.GetViewportRid();
            if (rid != _govRid)
            {
                _govRid = rid;
                RenderingServer.ViewportSetMeasureRenderTime(rid, true);
                _govT = 0; _govSum = 0; _govN = 0; _govWarm = 4.0;
                return;
            }
            // a hitch (a level just built, shaders and pipelines compiling, a stall) is not the cost of the scene:
            // drop the samples and wait a few seconds before judging again
            if (realDelta > 0.2) { _govT = 0; _govSum = 0; _govN = 0; _govWarm = 3.0; return; }
            if (_govWarm > 0) { _govWarm -= realDelta; return; }
            double gpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid);
            if (gpu <= 0.01) return;
            _govSum += gpu; _govN++; _govT += realDelta; _govQuiet += realDelta;
            if (_govT < 1.0 || _govN < 20) return;
            double avg = _govSum / _govN;
            _govT = 0; _govSum = 0; _govN = 0;

            float want = Dynamic;
            if (avg > GovernHigh) want = Dynamic * (avg > GovernHigh * 1.3 ? 0.86f : 0.93f);
            else if (avg < GovernLow && Dynamic < 1f && _govQuiet >= 6.0) want = Mathf.Min(1f, Dynamic * 1.07f);
            want = Mathf.Clamp(want, GovernFloor, 1f);
            if (Mathf.Abs(want - Dynamic) < 0.004f) return;
            float before = Scale;
            Dynamic = want;
            _govQuiet = 0;
            Apply(s, vp);
            Log.Debug("gfx", $"governor: gpu {avg:0.0} ms → render scale {before:0.00} → {Scale:0.00}");
        }
        catch (Exception e) { Log.Error("gfx", "render scale governor failed", e); GovernorOff = true; }
    }

    /// <summary>The quality level a settings object resolves to (honours the harness override).</summary>
    public static int QualityOf(Settings s) => Math.Clamp(QualityOverride >= 0 ? QualityOverride : s?.Quality ?? 2, 0, QualityCount - 1);

    /// <summary>The preset a settings object resolves to.</summary>
    public static Preset PresetOf(Settings s) => Presets[QualityOf(s)];

    /// <summary>Render scale that brings the internal resolution to the current preset's target (1440 lines at 高: 1.0 up to 1440p, 0.67 at 4K), never below 0.5.</summary>
    public static float AutoScale(Vector2I screen) => AutoScale(screen, P.TargetLines);

    private static float AutoScale(Vector2I screen, float lines)
    {
        if (screen.Y <= 0) return 1f;
        float s = lines / screen.Y;
        return Mathf.Clamp(Mathf.Round(s * 100f) / 100f, 0.5f, 1f);
    }

    /// <summary>The scale a given settings object resolves to on a window of the given pixel size.</summary>
    public static float ResolveScale(Settings s, Vector2I windowPx)
    {
        if (ScaleOverride >= 0.05f) return Mathf.Clamp(ScaleOverride, 0.4f, 1f);
        if (s == null) return 1f;
        if (s.RenderScale >= 0.05f) return Mathf.Clamp(s.RenderScale, 0.5f, 1f);
        return AutoScale(windowPx, PresetOf(s).TargetLines);
    }

    /// <summary>Applies render scale (FSR below 1.0), anti-aliasing, shadow atlases and the global quality of screen-space effects.</summary>
    public static void Apply(Settings s, Viewport vp)
    {
        if (s == null || vp == null) return;
        try
        {
            int q = QualityOf(s);
            var p = Presets[q];
            Vector2I px = vp is Window w ? w.Size : vp is SubViewport sv ? sv.Size : DisplayServer.WindowGetSize();
            if (px.X < 16 || px.Y < 16) px = DisplayServer.WindowGetSize();
            float scale = ResolveScale(s, px);
            if (Dynamic < 1f && s.RenderScale < 0.05f && ScaleOverride < 0.05f) scale = Mathf.Max(scale * Dynamic, 0.5f);
            bool upscale = scale < 0.99f;

            // ── per-viewport state ──
            // Thin geometry (dead branches, chains, the dithered see-through cut) needs a temporal resolve to hold still:
            // TAA at the internal resolution (中, 高, and every preset at native resolution), or FSR 2 which upscales and
            // resolves in one pass (極致). 低 trades it for speed (FSR 1 + FXAA).
            int up = UpscaleOverride >= 0 ? Math.Clamp(UpscaleOverride, 0, 2) : p.Upscale;
            bool fsr2 = upscale && up >= 2;
            vp.Scaling3DMode = !upscale ? Viewport.Scaling3DModeEnum.Bilinear : fsr2 ? Viewport.Scaling3DModeEnum.Fsr2 : Viewport.Scaling3DModeEnum.Fsr;
            vp.Scaling3DScale = upscale ? scale : 1f;
            vp.FsrSharpness = fsr2 ? 0.35f : 0.2f;
            vp.TextureMipmapBias = 0f;                               // the upscalers add their own negative bias
            vp.Msaa3D = Viewport.Msaa.Disabled;
            vp.UseTaa = !fsr2 && up >= 1;
            vp.ScreenSpaceAA = fsr2 || up >= 1 ? Viewport.ScreenSpaceAAEnum.Disabled : Viewport.ScreenSpaceAAEnum.Fxaa;
            vp.UseDebanding = true;                                  // dark gradients (fog, vignettes) band badly without it
            vp.MeshLodThreshold = p.Lod;
            vp.AnisotropicFilteringLevel = (Viewport.AnisotropicFiltering)Math.Clamp(p.Aniso, 0, 4);
            if (vp.PositionalShadowAtlasSize != p.PosAtlas) vp.PositionalShadowAtlasSize = p.PosAtlas;
            vp.PositionalShadowAtlas16Bits = q < 3;
            // the nearest fire gets a whole quadrant, the next three share one: sharp contact shadows where the eye is
            vp.SetPositionalShadowAtlasQuadrantSubdiv(0, Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv1);
            vp.SetPositionalShadowAtlasQuadrantSubdiv(1, Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv4);
            vp.SetPositionalShadowAtlasQuadrantSubdiv(2, Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv4);
            vp.SetPositionalShadowAtlasQuadrantSubdiv(3, Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv16);

            // ── renderer-wide state (only when the preset changed: resizing atlases reallocates them) ──
            if (_globalQ != q)
            {
                _globalQ = q;
                RenderingServer.DirectionalShadowAtlasSetSize(p.DirAtlas, q < 3);
                RenderingServer.DirectionalSoftShadowFilterSetQuality(p.Soft);
                RenderingServer.PositionalSoftShadowFilterSetQuality(p.Soft);
                RenderingServer.EnvironmentSetSsaoQuality(
                    q >= 3 ? RenderingServer.EnvironmentSsaoQuality.High : q == 2 ? RenderingServer.EnvironmentSsaoQuality.Low : RenderingServer.EnvironmentSsaoQuality.VeryLow,
                    true, 0.5f, q >= 3 ? 2 : 1, 50f, 300f);
                RenderingServer.EnvironmentSetSsilQuality(
                    q >= 3 ? RenderingServer.EnvironmentSsilQuality.Medium : RenderingServer.EnvironmentSsilQuality.Low,
                    true, 0.5f, q >= 3 ? 4 : 2, 50f, 300f);
                RenderingServer.EnvironmentSetVolumetricFogVolumeSize(p.FogSize, p.FogDepth);
                RenderingServer.EnvironmentSetVolumetricFogFilterActive(true);
                RenderingServer.EnvironmentGlowSetUseBicubicUpscale(q >= 3);
                RenderingServer.GISetUseHalfResolution(true);
                RenderingServer.EnvironmentSetSdfgiRayCount(RenderingServer.EnvironmentSdfgiRayCount.Count16);
                RenderingServer.EnvironmentSetSdfgiFramesToConverge(RenderingServer.EnvironmentSdfgiFramesToConverge.In20Frames);
                RenderingServer.EnvironmentSetSdfgiFramesToUpdateLight(RenderingServer.EnvironmentSdfgiFramesToUpdateLight.In4Frames);
            }

            Quality = q;
            Scale = upscale ? scale : 1f;
            Internal = new Vector2I(Mathf.RoundToInt(px.X * Scale), Mathf.RoundToInt(px.Y * Scale));
            string sig = $"quality={q}({QualityNames[q]}){(QualityOverride >= 0 ? "(override)" : "")} target={px.X}x{px.Y} scale={Scale:0.00}{(s.RenderScale < 0.05f && ScaleOverride < 0.05f ? "(auto)" : "")} " +
                         $"internal={Internal.X}x{Internal.Y} upscaler={(upscale ? (fsr2 ? "FSR2" : "FSR1") : "none")} taa={vp.UseTaa} ssaa={vp.ScreenSpaceAA} " +
                         $"shadows=dir{p.DirAtlas}x{p.Splits}/pos{p.PosAtlas} soft={p.Soft} omniShadows={p.OmniShadows} mist={(p.Mist ? p.FogSize + "^2x" + p.FogDepth : "off")} " +
                         $"ssao={p.Ssao} ssil={p.Ssil} ssr={p.Ssr} sdfgi={p.Sdfgi}";
            if (sig != _lastSig) { _lastSig = sig; Log.Info("gfx", sig); }
        }
        catch (Exception e) { Log.Error("gfx", "applying graphics settings failed", e); }
    }

    /// <summary>
    /// Switches an environment's expensive features by quality: glow always, SSAO and ground mist from 中, SSIL from 高,
    /// reflections and SDFGI only at 極致. Safe to call repeatedly: it only switches features and fills in parameters
    /// that are still at the engine defaults, it never rescales what a level theme has set.
    /// </summary>
    public static void TuneEnvironment(Godot.Environment env, Settings s)
    {
        if (env == null) return;
        try
        {
            int q = QualityOf(s);
            var p = Presets[q];

            // 亮度 (0.8 … 1.3, default 1 = the dark look the game is graded for): an exposure factor on top of what the
            // level set, squared so the slider's short travel covers about a stop either way
            float bright = Mathf.Clamp(s?.Brightness ?? 1f, 0.8f, 1.3f);
            if (!env.HasMeta("exposure_base")) env.SetMeta("exposure_base", env.TonemapExposure);
            env.TonemapExposure = env.GetMeta("exposure_base").AsSingle() * bright * bright;

            // glow: always on — embers, skills and loot beams depend on it
            if (!env.GlowEnabled)
            {
                env.GlowEnabled = true;
                env.GlowIntensity = 0.8f;
                env.GlowStrength = 1.0f;
                env.GlowBloom = 0f;
                env.GlowHdrThreshold = 1.0f;
                env.GlowHdrScale = 2.0f;
                env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen;
            }

            env.SsaoEnabled = p.Ssao;
            if (p.Ssao && env.SsaoLightAffect <= 0.001f) env.SsaoLightAffect = 0.25f;   // scenes are lit by local fires, so AO must touch direct light a little
            env.SsilEnabled = p.Ssil;

            // volumetric fog: the level builder fills a slab of mist near the ground (FogVolume); the global density stays 0
            env.VolumetricFogEnabled = p.Mist;
            if (p.Mist)
            {
                env.VolumetricFogTemporalReprojectionEnabled = true;
                env.VolumetricFogTemporalReprojectionAmount = 0.9f;
            }

            // reflections on wet stone, polished marble, ice and water
            env.SsrEnabled = p.Ssr;
            if (p.Ssr)
            {
                env.SsrMaxSteps = 56;
                env.SsrFadeIn = 0.2f;
                env.SsrFadeOut = 2.5f;
                env.SsrDepthTolerance = 0.3f;
            }

            // dynamic global illumination only at 極致
            bool wasSdfgi = env.SdfgiEnabled;
            env.SdfgiEnabled = p.Sdfgi;
            if (p.Sdfgi && !wasSdfgi)
            {
                env.SdfgiCascades = 4;
                env.SdfgiMinCellSize = 0.4f;
                env.SdfgiUseOcclusion = true;
                env.SdfgiReadSkyLight = true;
                env.SdfgiBounceFeedback = 0.4f;
                env.SdfgiEnergy = 1.0f;
                env.SdfgiYScale = Godot.Environment.SdfgiyScale.Scale75Percent;
            }
        }
        catch (Exception e) { Log.Error("gfx", "tuning the environment failed", e); }
    }
}
