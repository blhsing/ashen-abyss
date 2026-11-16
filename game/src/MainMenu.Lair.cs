using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// The title backdrop: a close-up of 寂主・奧薩洛斯's face lurking in the dark, rendered into its own SubViewport. His
/// face and crown fill the right of the frame, seen from a little below and turned toward the menu so the breach in his
/// left temple shows; his glowing parts (the emission textures of the emis_ materials: eyes, breach, cracks, the furnace
/// behind his teeth, candle wicks) are the brightest things on screen, a narrow ember light from below gives his face its
/// form, a cold rim from behind and haze separate his outline from the black, and embers, ash and smoke drift in front.
/// The camera leans in and out by a few centimetres. The brazier stage (<see cref="BuildBackdrop"/>) is only rendered while
/// character creation shows the hero; the two views cross-fade.
///
/// Nothing here knows the model's proportions: the framing and every light are placed at run time from the head bone
/// and the model's bounds (<see cref="MeasureLord"/>), so a rebuilt boss_osaros.glb is framed the same way. All the
/// numbers of the look are in the "look" block below.
/// </summary>
public partial class MainMenu
{
    // ── the look ─────────────────────────────────────────────────────────────────────────────────────────────────
    // Model: the boss's fight scale, his turn toward the menu (degrees; 0 faces the lens) and the idle's speed; the stare:
    // degrees his chin is lowered on the neck and on the head (on top of the idle, see LairStare), so he glares up at the
    // lens from under his brow.
    private const string LordModel = "boss_osaros";
    private const float LordScale = 2.4f, LordYaw = -4f, LordIdleRate = 0.55f, StareNeck = 7.2f, StareHead = 10.8f;
    // the eyes for the stare: a small ember core high in each socket (model metres, his space: x left/right, y up, z forward),
    // so they read as eyes rolled up at the lens with the brow's edge cutting across their tops; 0 = off
    private const float IrisSize = 0f;
    private static readonly Vector3[] EyeCore = { new(0.036f, 1.704f, 0.115f), new(-0.036f, 1.704f, 0.115f) };
    private static readonly Vector3 IrisRaise = new(0f, 0.006f, 0.004f);
    private static readonly Color IrisColor = new(1f, 0.55f, 0.18f);
    private const float IrisEnergy = 6f;
    // Framing: a tight close-up of his face, placed from his eyes (a point between them riding on the head bone, so the
    // bow and the breathing never push them out of frame). The frame is FaceSpan head units tall; his eyes sit EyeFromTop
    // of it below the top edge and FrameX across. The lens is CamBelow head units below his eye line (it looks up under
    // the brow) and CamSide to his right (frame-height units, negative = toward the menu); the push-in is PushIn × the
    // distance over PushPeriod s.
    private const float LairFov = 30f, FrameX = 0.64f, FaceSpan = 0.20f, EyeFromTop = 0.42f, CamBelow = 0.10f, CamSide = 0f;
    private static readonly Vector3 EyeMid = new(0f, 1.708f, 0.118f);
    private float _stareNeck = StareNeck, _stareHead = StareHead, _camBelow = CamBelow, _iris = IrisSize;
    private const float PushIn = 0.04f, PushPeriod = 64f;
    // Lights, positioned in head units (u = his head bone's height above his feet, 3.66 m today) from the head bone, world axes
    // (x to the right of the frame, y up, z toward the lens); each aims at the head bone plus Aim. Flicker is the share
    // of the light that follows the fire's flicker.
    private readonly struct LairLight
    {
        public readonly string Name; public readonly bool Omni, Shadow; public readonly Vector3 At, Aim; public readonly Color Color;
        public readonly float Energy, Angle, Range, Flicker, Spec;
        public LairLight(string name, bool omni, Vector3 at, Vector3 aim, Color c, float energy, float angle, float range, float flicker, bool shadow = false, float spec = 0.7f)
        { Name = name; Omni = omni; At = at; Aim = aim; Color = c; Energy = energy; Angle = angle; Range = range; Flicker = flicker; Shadow = shadow; Spec = spec; }
    }
    private static readonly LairLight[] LairRig =
    {
        // the fire below, out of frame: a warm glow that fades up his collar
        new("Glow", true, new Vector3(-0.11f, -0.60f, 0.45f), Vector3.Zero, new Color(1f, 0.40f, 0.12f), 0.2f, 0f, 0.80f, 1f),
        // the key: a narrow ember light from low on the menu's side, raking up across his face (brow, cheekbone, teeth
        // catch it, the orbits and hollows stay black); the one shadowed light
        new("Under", false, new Vector3(-0.14f, -0.62f, 0.28f), new Vector3(0.0f, 0.12f, 0f), new Color(1f, 0.50f, 0.22f), 1.4f, 10f, 1.9f, 0.6f, true),
        // ember rim from low behind on the menu's side: the edges of crown, skull and pauldron toward the logo
        new("Ember", false, new Vector3(-0.50f, -0.20f, -1.10f), new Vector3(0f, 0.24f, 0f), new Color(1f, 0.34f, 0.08f), 3f, 18f, 2.5f, 0.4f),
        // the cold from behind on the far side: a rim down his skull and cheek that cuts him from the dark
        new("ColdFace", false, new Vector3(0.62f, 0.22f, -0.75f), new Vector3(0f, 0.10f, 0f), new Color(0.45f, 0.58f, 0.92f), 14f, 14f, 2.2f, 0f, false, 0.25f),
        // the cold from high behind: a rim on the crown and the spires
        new("ColdA", false, new Vector3(0.68f, 0.84f, -1.09f), new Vector3(0f, 0.19f, 0f), new Color(0.42f, 0.55f, 0.88f), 4f, 28f, 3.3f, 0f, false, 0.25f),
        new("ColdB", false, new Vector3(-0.33f, 1.04f, -0.93f), new Vector3(0f, 0.25f, 0f), new Color(0.40f, 0.52f, 0.85f), 2.5f, 26f, 3.3f, 0f, false, 0.25f),
    };
    // his glowing parts here (the fight's own material stays as it is): energy of their emission texture, how much they breathe
    private const float LordGlowEnergy = 3.0f, LordGlowBreath = 0.12f;
    // the cold haze far behind him (head units from the head bone; size in head units)
    private static readonly Vector3 HazeAt = new(0.16f, 0.46f, -1.36f);
    private static readonly Vector2 HazeSize = new(3.0f, 2.45f);
    private static readonly Color HazeColor = new(0.12f, 0.16f, 0.26f);
    // the hall behind him, black against the haze: columns as (x, z, width) in head units from the head bone
    private static readonly Vector3[] LairColumns = { new(-0.95f, -1.15f, 0.20f), new(0.85f, -1.20f, 0.17f), new(1.65f, -1.05f, 0.22f) };
    // the current model (v2), measured: head bone 3.66 m above his feet at fight scale, crown tip 5.14 m (used if he cannot be measured)
    private const float FallbackHead = 3.66f, FallbackTop = 5.14f;
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private SubViewport _lairVp;
    private TextureRect _lairRect;
    private Node3D _lair;
    private Camera3D _lairCam;
    private Godot.Environment _lairEnv;
    private CharModel _lord;
    private Node3D _lordEyes;
    private Node3D[] _eyeCores = Array.Empty<Node3D>();
    private Vector3 _eyeAt;
    private bool _eyeSet;
    private Light3D[] _lairLights = Array.Empty<Light3D>();
    private Vector3[] _lairLightAt = Array.Empty<Vector3>();
    private StandardMaterial3D _lairHazeMat;
    private float _lairFade = 1f;
    private readonly List<ShaderMaterial> _lordGlow = new();
    private Vector3 _lordHead = new(0f, FallbackHead, 0f);
    private float _lordTop = FallbackTop, _lordU = FallbackHead;

    private void BuildLair()
    {
        var world = new World3D();
        _lairVp = new SubViewport
        {
            Name = "Lair", World3D = world, TransparentBg = false, HandleInputLocally = false, GuiDisableInput = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Size = _vpSize, UseDebanding = true,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Fxaa, Msaa3D = Viewport.Msaa.Disabled, PositionalShadowAtlasSize = 2048,
        };
        AddChild(_lairVp);
        _lair = new Node3D { Name = "LairScene" };
        _lairVp.AddChild(_lair);

        _lairEnv = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.004f, 0.004f, 0.006f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.30f, 0.36f, 0.52f), AmbientLightEnergy = 0.012f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            TonemapMode = Godot.Environment.ToneMapper.Filmic, TonemapExposure = 1.0f, TonemapWhite = 6f,
            GlowEnabled = true, GlowIntensity = 0.85f, GlowStrength = 1.1f, GlowBloom = 0.04f, GlowHdrThreshold = 1.0f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen,
            FogEnabled = false, VolumetricFogEnabled = false,      // depth comes from sprites: a lit fog volume veiled him in grey
            AdjustmentEnabled = true, AdjustmentContrast = 1.08f, AdjustmentSaturation = 1.05f,
        };
        world.Environment = _lairEnv;

        BuildLord();
        MeasureLord();
        float u = _lordU;

        _lairLights = new Light3D[LairRig.Length];
        _lairLightAt = new Vector3[LairRig.Length];
        for (int i = 0; i < LairRig.Length; i++)
        {
            var d = LairRig[i];
            var at = _lordHead + d.At * u;
            Light3D l;
            if (d.Omni) l = new OmniLight3D { OmniRange = d.Range * u, OmniAttenuation = 1.6f, Position = at };
            else
            {
                var aim = _lordHead + d.Aim * u;
                l = new SpotLight3D
                {
                    SpotRange = d.Range * u, SpotAngle = d.Angle, SpotAttenuation = 0.9f, SpotAngleAttenuation = 0.8f,
                    Transform = new Transform3D(Basis.LookingAt((aim - at).Normalized(), Vector3.Up), at),
                };
            }
            l.Name = d.Name; l.LightColor = d.Color; l.LightEnergy = d.Energy; l.LightSpecular = d.Spec; l.LightVolumetricFogEnergy = 0f;
            l.ShadowEnabled = d.Shadow; l.ShadowBlur = 1.2f;
            _lair.AddChild(l);
            _lairLights[i] = l;
            _lairLightAt[i] = at;
        }

        _lairHazeMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoTexture = MenuKit.SoftDot, AlbedoColor = HazeColor,
            DisableReceiveShadows = true, DisableFog = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _lair.AddChild(new MeshInstance3D
        {
            Name = "Haze", Mesh = new QuadMesh { Size = HazeSize * u }, MaterialOverride = _lairHazeMat,
            Position = _lordHead + HazeAt * u, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        var black = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.006f, 0.006f, 0.008f) };
        var column = new BoxMesh { Size = Vector3.One };
        foreach (var c in LairColumns)
            _lair.AddChild(new MeshInstance3D
            {
                Name = "Column", Mesh = column, MaterialOverride = black, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(_lordHead.X + c.X * u, _lordHead.Y, _lordHead.Z + c.Y * u), Scale = new Vector3(c.Z * u, 4f * u, c.Z * u),
            });

        BuildLairAir(u);

        _lairCam = new Camera3D { Name = "Camera", Fov = LairFov, Near = 0.2f, Far = 80f, Current = true };
        _lair.AddChild(_lairCam);

        _lairRect = new TextureRect
        {
            Name = "LairView", Texture = _lairVp.GetTexture(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore,
        };
        _lairRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_lairRect);
        UpdateLair(0f);
    }

    private void BuildLord()
    {
        CharModel m = null;
        try { m = CharModel.Create(LordModel); }
        catch (Exception e) { Log.Error("menu", "the title's boss model failed", e); }
        if (m == null || m.GetChildCount() == 0) { m?.QueueFree(); Log.Warn("menu", "title backdrop without its boss model"); return; }
        _lord = m;
        m.Scale = Vector3.One * LordScale;
        m.RotationDegrees = new Vector3(0f, LordYaw, 0f);
        _lair.AddChild(m);
        m.ShowWeapons(false);
        if (m.Has("Idle_Loop")) m.Play("Idle_Loop", LordIdleRate, 0f);   // slow, heavy breathing
        // the stare: chin down on top of the idle
        int head = m.BoneIndex("head");
        if (m.Skeleton != null && head >= 0)
        {
            m.Skeleton.AddChild(new LairStare { Name = "Stare", Head = head, Neck = m.Skeleton.GetBoneParent(head), HeadDeg = _stareHead, NeckDeg = _stareNeck });
            AddEyes(m.Skeleton, head);
        }
        else Log.Warn("menu", "title backdrop: no head bone, no stare");
        // his glowing parts burn brighter here than in the fight: the title gets its own copies of those materials
        foreach (var mi in FindMeshes(m))
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                if (mi.GetSurfaceOverrideMaterial(s) is not ShaderMaterial sm || !sm.ResourceName.StartsWith("emis", StringComparison.Ordinal)) continue;
                // v2's emis_ materials carry an emission TEXTURE that is black on most of their faces: only the energy is
                // raised here. The flat "emission" colour is added over the whole surface by char.gdshader, so it stays as
                // the model set it (black when a texture is present).
                var own = (ShaderMaterial)sm.Duplicate();
                own.SetShaderParameter("emission_energy", LordGlowEnergy);
                own.SetShaderParameter("pulse", 0f);
                mi.SetSurfaceOverrideMaterial(s, own);
                _lordGlow.Add(own);
            }
    }

    /// <summary>
    /// The point between his eyes that the camera frames, riding on the head bone, and (when IrisSize > 0) the stare's eye
    /// cores: two small glowing beads high in the sockets.
    /// </summary>
    private void AddEyes(Skeleton3D sk, int head)
    {
        var ba = new BoneAttachment3D { Name = "Eyes", BoneIdx = head };
        sk.AddChild(ba);
        var inv = sk.GetBoneGlobalRest(head).AffineInverse();
        _lordEyes = new Node3D { Name = "EyeMid", Position = inv * EyeMid };
        ba.AddChild(_lordEyes);
        _eyeCores = new Node3D[EyeCore.Length];
        for (int i = 0; i < EyeCore.Length; i++) { _eyeCores[i] = new Node3D { Name = "EyeCore" + i, Position = inv * EyeCore[i] }; ba.AddChild(_eyeCores[i]); }
        if (_iris <= 0f) return;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = Colors.Black, EmissionEnabled = true, Emission = IrisColor, EmissionEnergyMultiplier = IrisEnergy,
            DisableReceiveShadows = true, Roughness = 1f,
        };
        var mesh = new SphereMesh { Radius = _iris, Height = _iris * 1.7f, RadialSegments = 12, Rings = 6, Material = mat };
        foreach (var p in EyeCore)
            ba.AddChild(new MeshInstance3D { Mesh = mesh, Position = inv * (p + IrisRaise), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>
    /// Where his head is and how tall he stands, from the model itself: the head bone in his idle (its first frame is
    /// applied here) and the top of his visible meshes. Everything in the title's composition is placed from these.
    /// </summary>
    private void MeasureLord()
    {
        var m = _lord;
        if (m == null) { Log.Info("menu", "title backdrop: no model, fallback framing"); return; }
        try { m.Anim?.Advance(0); } catch (Exception) { }
        Vector3 head = m.BonePos("head");
        float top = float.MinValue, feet = m.GlobalPosition.Y;
        foreach (var mi in FindMeshes(m))
        {
            if (!mi.IsVisibleInTree()) continue;
            var box = mi.GlobalTransform * mi.GetAabb();
            top = Mathf.Max(top, box.End.Y);
        }
        bool ok = m.BoneIndex("head") >= 0 && head.Y - feet > 0.5f && top > head.Y;
        if (ok) { _lordHead = head; _lordTop = top; }
        else
        {
            float h = m.Height * LordScale;
            _lordHead = m.GlobalPosition + Vector3.Up * h * 0.70f;
            _lordTop = feet + h;
        }
        _lordU = Mathf.Max(0.5f, _lordHead.Y - feet);
        Log.Info("menu", $"title backdrop: {m.FileId}{(m.IsStandIn ? " (stand-in)" : "")} head {_lordHead.X:0.00},{_lordHead.Y:0.00},{_lordHead.Z:0.00} " +
            $"top {_lordTop:0.00} unit {_lordU:0.00}{(ok ? "" : " (estimated)")}, {_lordGlow.Count} glowing surfaces");
    }

    private static IEnumerable<MeshInstance3D> FindMeshes(Node n)
    {
        if (n is MeshInstance3D mi && mi.Mesh != null) yield return mi;
        foreach (var c in n.GetChildren()) foreach (var d in FindMeshes(c)) yield return d;
    }

    private void LairEmit(string name, int amount, double lifetime, ParticleProcessMaterial pm, Material sprite, Vector3 at, float u, double preprocess)
    {
        _lair.AddChild(new GpuParticles3D
        {
            Name = name, Amount = amount, Lifetime = lifetime, Preprocess = preprocess, ProcessMaterial = pm,
            Position = _lordHead + at * u,
            DrawPass1 = new QuadMesh { Size = Vector2.One, Material = sprite }, VisibilityAabb = new Aabb(new Vector3(-2, -1.5f, -2) * u, new Vector3(4, 4, 4) * u),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>Embers, ash and smoke: placed, sized and moving in head units, like the lights.</summary>
    private void BuildLairAir(float u)
    {
        Vector3 V(float x, float y, float z) => new Vector3(x, y, z) * u;
        var emberRamp = Ramp((0f, new Color(1f, 0.9f, 0.6f, 0f)), (0.1f, new Color(1f, 0.72f, 0.32f, 1f)), (0.65f, new Color(1f, 0.36f, 0.07f, 0.85f)), (1f, new Color(0.5f, 0.08f, 0f, 0f)));
        // embers rising from the fire below, drifting between him and the lens
        LairEmit("Embers", 80, 9.0, new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = V(0.65f, 0.08f, 0.45f),
            Direction = new Vector3(-0.2f, 1f, 0.05f), Spread = 30f, InitialVelocityMin = 0.05f * u, InitialVelocityMax = 0.12f * u, Gravity = V(-0.006f, 0.012f, 0f),
            ScaleMin = 0.005f * u, ScaleMax = 0.012f * u, ColorRamp = emberRamp,
            TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.6f, TurbulenceNoiseScale = 2.5f, TurbulenceInfluenceMin = 0.02f, TurbulenceInfluenceMax = 0.07f,
        }, Sprite(true, new Color(2.8f, 1.5f, 0.6f)), new Vector3(0.05f, -0.42f, 0.40f), u, 9.0);
        // ash falling slowly through the light
        LairEmit("Ash", 90, 14.0, new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = V(1.36f, 0.08f, 0.82f),
            Direction = Vector3.Down, Spread = 30f, InitialVelocityMin = 0.04f * u, InitialVelocityMax = 0.095f * u, Gravity = V(0.014f, -0.016f, 0.005f),
            ScaleMin = 0.004f * u, ScaleMax = 0.0095f * u,
            ColorRamp = Ramp((0f, new Color(1, 1, 1, 0f)), (0.12f, new Color(1, 1, 1, 0.55f)), (0.85f, new Color(1, 1, 1, 0.45f)), (1f, new Color(1, 1, 1, 0f))),
            TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.4f, TurbulenceNoiseScale = 4f,
        }, Sprite(false, new Color(0.45f, 0.42f, 0.42f)), new Vector3(0.14f, 0.74f, 0.22f), u, 14.0);
        // smoke banks rolling in front of his chest: the lower body is lost in it
        LairEmit("Smoke", 10, 18.0, new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = V(1.09f, 0.14f, 0.27f),
            Direction = new Vector3(-1f, 0.15f, 0f), Spread = 20f, InitialVelocityMin = 0.016f * u, InitialVelocityMax = 0.044f * u, Gravity = Vector3.Zero,
            ScaleMin = 0.82f * u, ScaleMax = 1.36f * u, AngleMin = -180f, AngleMax = 180f,
            ColorRamp = Ramp((0f, new Color(1, 1, 1, 0f)), (0.3f, new Color(1, 1, 1, 0.16f)), (0.7f, new Color(1, 1, 1, 0.16f)), (1f, new Color(1, 1, 1, 0f))),
        }, Sprite(false, new Color(0.10f, 0.075f, 0.065f)), new Vector3(0.16f, -0.40f, 0.33f), u, 18.0);
    }

    /// <summary>Per frame: the cross-fade with the creation stage, the flicker of the ember light, the camera's breath.</summary>
    private void UpdateLair(float dt)
    {
        if (_lairVp == null) return;
        // gone within about half a second when creation starts, back as quickly when it ends (the stage camera's own
        // swoop takes longer and goes on underneath)
        _lairFade = Mathf.MoveToward(_lairFade, _camBlendTarget > 0f ? 0f : 1f, dt * 1.8f);
        float a = _lairFade * _lairFade * (3f - 2f * _lairFade);
        if (_lairRect != null) _lairRect.Modulate = new Color(1, 1, 1, a);
        if (_lairFade <= 0.001f || _lairCam == null) return;

        float t = _t, u = _lordU;
        // the fire below breathes and gutters: slow swells, a quicker shiver, now and then a dip
        float f = 1f + 0.16f * Mathf.Sin(t * 1.3f) * Mathf.Sin(t * 0.47f + 1.1f) + 0.06f * Mathf.Sin(t * 7.9f) * Mathf.Sin(t * 3.3f)
                  + 0.035f * Mathf.Sin(t * 17.3f + 0.7f) - 0.12f * Mathf.Max(0f, Mathf.Sin(t * 0.31f + 2f) - 0.85f) / 0.15f;
        for (int i = 0; i < _lairLights.Length; i++)
        {
            var d = LairRig[i];
            if (d.Flicker <= 0f) continue;
            _lairLights[i].LightEnergy = d.Energy * (1f - d.Flicker + d.Flicker * f);
            if (d.Omni) _lairLights[i].Position = _lairLightAt[i] + new Vector3(0.011f * Mathf.Sin(t * 2.1f), 0.014f * Mathf.Sin(t * 3.7f), 0f) * u;
        }
        float glow = LordGlowEnergy * (1f - LordGlowBreath + LordGlowBreath * Mathf.Sin(t * 0.8f));
        foreach (var g in _lordGlow) g.SetShaderParameter("emission_energy", glow);

        // the frame: his face, from the eyes (followed softly, so the breathing still shows against the frame)
        var eyeNow = _lordEyes != null && IsInstanceValid(_lordEyes) ? _lordEyes.GlobalPosition : _lordHead + new Vector3(0f, 0.13f * u, 0.12f * u);
        _eyeAt = _eyeSet && _t > 1.5f ? _eyeAt.Lerp(eyeNow, Mathf.Min(1f, dt * 1.5f)) : eyeNow;   // snapped while the pose settles
        _eyeSet = true;
        float span = FaceSpan * u;
        float fov = Mathf.DegToRad(LairFov);
        float dist = span * 0.5f / Mathf.Tan(fov * 0.5f);
        // it leans in and out by a few centimetres over a minute, and drifts a hair sideways
        float push = 0.5f - 0.5f * Mathf.Cos(t * Mathf.Tau / PushPeriod);
        dist *= 1f - PushIn * push;
        var at = new Vector3(_eyeAt.X, _eyeAt.Y - (0.5f - EyeFromTop) * span * (1f - PushIn * push), _eyeAt.Z);
        var pos = new Vector3(_eyeAt.X + CamSide * span + 0.01f * u * Mathf.Sin(t * 0.07f),
                              _eyeAt.Y - _camBelow * u + 0.006f * u * Mathf.Sin(t * 0.09f + 1f), _eyeAt.Z + dist);
        var dir = at - pos;
        _lairCam.Transform = new Transform3D(Basis.LookingAt(dir.Normalized(), Vector3.Up), pos);
        float aspect = _vpSize.Y > 0 ? _vpSize.X / (float)_vpSize.Y : 16f / 9f;
        float visW = 2f * dir.Length() * Mathf.Tan(fov * 0.5f) * aspect;
        _lairCam.HOffset = -(FrameX - 0.5f) * visW;
    }

    private float _fpsT, _fpsWorst, _gpuSum, _cpuSum;
    private int _fpsFrames, _gpuN;
    private bool _fpsDone;

    /// <summary>One line in the log per run: the title screen's frame rate between 3 and 8 s after it opened.</summary>
    private void MeasureTitleFps(float dt)
    {
        if (_fpsDone || _page != Page.Title || _lairVp == null) return;
        if (_t < 3f) return;
        var rid = _lairVp.GetViewportRid();
        if (_fpsFrames == 0) RenderingServer.ViewportSetMeasureRenderTime(rid, true);
        else
        {
            // what the backdrop itself costs (its own viewport's render time), apart from whatever caps the frame rate
            _gpuSum += (float)RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid);
            _cpuSum += (float)RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid);
            _gpuN++;
        }
        _fpsT += dt; _fpsFrames++; _fpsWorst = Mathf.Max(_fpsWorst, dt);
        if (_fpsT < 5f) return;
        _fpsDone = true;
        RenderingServer.ViewportSetMeasureRenderTime(rid, false);
        int n = Math.Max(1, _gpuN);
        Log.Info("perf", $"title screen: fps={_fpsFrames / _fpsT:0} frame={_fpsT / _fpsFrames * 1000f:0.0}ms worst={_fpsWorst * 1000f:0.0}ms " +
            $"backdrop gpu={_gpuSum / n:0.00}ms cpu={_cpuSum / n:0.00}ms " +
            $"draws={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0} prims={Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame):0} " +
            $"backdrop={_vpSize.X}x{_vpSize.Y}x{_lairVp?.Scaling3DScale ?? 0f:0.00} q={_quality}");
    }

    /// <summary>Which of the two backdrops render: never both for long, nothing while the screen is hidden.</summary>
    private void GateBackdrops(bool vis)
    {
        bool stage = vis && (_lairFade < 0.999f || _lairVp == null);
        bool lair = vis && _lairFade > 0.001f;
        if (_vp != null)
        {
            var want = stage ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
            if (_vp.RenderTargetUpdateMode != want) _vp.RenderTargetUpdateMode = want;
            if (_vpRect != null) _vpRect.Visible = stage;
        }
        if (_lairVp != null)
        {
            var want = lair ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
            if (_lairVp.RenderTargetUpdateMode != want) _lairVp.RenderTargetUpdateMode = want;
            if (_lairRect != null) _lairRect.Visible = lair;
        }
    }

    private void TuneLair(Vector2I size, float scale, int q)
    {
        if (_lairVp == null) return;
        _lairVp.Size = size;
        _lairVp.Scaling3DMode = scale < 0.99f ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
        _lairVp.Scaling3DScale = scale;
        _lairVp.FsrSharpness = 0.3f;
        _lairVp.Msaa3D = q >= 3 ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Disabled;
        _lairVp.PositionalShadowAtlasSize = q >= 2 ? 4096 : 2048;
        // the one shadowed light: only from quality 1 up
        for (int i = 0; i < _lairLights.Length; i++) _lairLights[i].ShadowEnabled = LairRig[i].Shadow && q >= 1;
    }
}
