using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Every visual effect in the game. Effects are assembled from a handful of shared resources created once:
/// procedural textures, unit meshes, one ShaderMaterial per fx_* shader (instance uniforms carry what differs)
/// and cached ParticleProcessMaterial presets per element. Nothing here needs a scene or a custom node class:
/// animation is driven from the SceneTree's process_frame signal, so the module stays a plain static class.
///
/// Craft rules followed throughout: gather (under 150 ms) → body → impact (brightest frames + a light flash) →
/// linger (under a second, low opacity); one hue family per element (Palette.Element); hostile ground danger is
/// always Palette.Danger with a hard edge and a fill timer, friendly marks are thinner and dimmer.
///
/// Budgets: about 70 live one-shot emitters (the least valuable are dropped first), 12 pooled flash lights and
/// 10 small persistent lights, none of them casting shadows.
/// </summary>
public static class Fx
{
    // ───────────────────────── budgets ─────────────────────────

    private const int MaxOneShots = 70;
    private const int MaxFlashLights = 12;
    private const int MaxSoftLights = 10;
    private const float CullDistance = 60f;          // effects further than this from the hero are not spawned

    /// <summary>How a particle is drawn: additive dot / additive puff / alpha puff / velocity streak / additive shard / alpha chip.</summary>
    private enum Look { Dot, Puff, Smoke, Streak, Shard, Chip, Count }

    private struct ShotRec { public GpuParticles3D P; public float Left; public int Value; }

    private sealed class FlashSlot { public OmniLight3D L; public float T, Dur, Energy; public bool Busy; }

    /// <summary>One running animation. Pooled; the meaning of the numeric slots is documented at each step function.</summary>
    private sealed class Anim
    {
        public Action<Anim, float> Step;
        public Node3D Node;                 // owner: when it is freed (by us or by anyone else) the animation ends
        public GeometryInstance3D G1, G2;
        public GpuParticles3D P1, P2;
        public OmniLight3D Light;
        public Actor Actor;
        public Element El;
        public float T, Dur, A, B, C, D, Orphan;
        public Vector3 V0, V1;
        public int I;
        public bool Done, Always;           // Always: keeps running while the game is paused (ambient fires, portals)

        public void Clear()
        {
            Step = null; Node = null; G1 = null; G2 = null; P1 = null; P2 = null; Light = null; Actor = null; El = Element.Physical;
            T = Dur = A = B = C = D = Orphan = 0f; V0 = V1 = Vector3.Zero; I = 0; Done = Always = false;
        }
    }

    // ───────────────────────── state ─────────────────────────

    private static volatile bool _ready, _failed;
    private static bool _hooked, _wasPaused;
    private static SceneTree _tree;
    private static float _clock;
    private static int _frame, _errors;
    private static int _mainThread;                  // managed id of the thread that runs OnFrame (0 until the first frame)
    private static readonly List<Anim> _anims = new(128);
    private static readonly List<Anim> _pending = new(16);   // animations registered from another thread (or before the first frame)
    private static readonly Stack<Anim> _animPool = new(64);
    private static readonly List<ShotRec> _shots = new(96);
    private static readonly FlashSlot[] _flash = new FlashSlot[MaxFlashLights];
    private static readonly List<OmniLight3D> _soft = new(16);
    private static readonly Dictionary<int, ParticleProcessMaterial> _pm = new();
    private static readonly Dictionary<ulong, ParticleProcessMaterial> _portalPm = new();
    private static readonly Dictionary<int, Texture2D> _ramps = new();
    private static readonly Dictionary<uint, Texture2D> _colorRamps = new();
    private static readonly Mesh[] _draw = new Mesh[(int)Look.Count];

    private static Texture2D _dot, _noise, _puff, _shard, _streak;
    private static Mesh _quad, _quadXY, _sphere, _cyl, _shaft, _plate, _spike, _flameQuad, _flameFieldQuad;
    private static Material _mRing, _mTele, _mZone, _mSlash, _mFlash, _mBeam, _mPillar, _mWave, _mSwirl, _mShaft, _mShaftHostile, _mIron, _mFlame, _mFlameField;
    private static Texture2D _cShrink, _cShrinkLate, _cGrow, _cPop, _cFlame;
    private static Texture2D _rFlame, _rSmoke, _rSmokeThin, _rDark, _rDust, _rMist, _rHaze, _rAsh, _rChip, _rHeal, _rGold, _rFaint;

    private static readonly StringName PT = "t", PTint = "tint", PGain = "gain", PPower = "power", PWidth = "width", PFade = "fade",
        PMode = "mode", PArc = "arc", PInner = "inner", PSoft = "soft", PEdge = "edge_tint", PHalf = "half_size", PHostile = "hostile",
        PSeed = "seed", PRadius = "radius", PPulse = "pulse", PBob = "bob", PFlow = "flow", PTopFade = "top_fade", PShimmer = "shimmer",
        PUpright = "upright", PNoise = "noise_tex", PKind = "kind", MetaProj = "fx_projectile";

    private static readonly Action<Anim, float> FnT = StepT, FnTele = StepTelegraph, FnZone = StepZone, FnMeteor = StepMeteor,
        FnAura = StepAura, FnPillar = StepPillar, FnBeam = StepBeam, FnFlicker = StepFlicker, FnGhost = StepGhost,
        FnSkyBolt = StepSkyBolt, FnGrow = StepGrow;

    // particle preset ids (first part of the cache key)
    private const int PHit = 1, PBurstA = 2, PBurstB = 3, PBurstC = 4, PTrail = 5, PTrailSparks = 6, PZone = 7, PWave = 8, PAura = 9,
        PDash = 10, PAshFlake = 11, PAshEmber = 12, PDust = 13, PHeal = 14, PLvlSpark = 15, PLvlEmber = 16, PTeleport = 17, PGather = 18,
        PFlame = 19, PFireEmber = 20, PFireSmoke = 21, PSparkle = 22, PZoneBody = 23, PZoneEmber = 24, PZoneSmoke = 25;

    // ═════════════════════════ public API ═════════════════════════

    /// <summary>Soft round dot (white, alpha falling off from the centre). Shared; do not modify.</summary>
    public static Texture2D SoftDot => _dot ??= MakeDot();

    /// <summary>Seamless greyscale noise (256², mip-mapped). Shared; do not modify.</summary>
    public static Texture2D NoiseTex => _noise ??= MakeNoise();

    /// <summary>Builds the shared effect resources now (call from a loading screen) instead of on the first effect.</summary>
    public static void Prewarm() => Ensure();

    /// <summary>Explosion / nova: shock ring, particles per element and a light flash.</summary>
    public static void Burst(Vector3 pos, Element el, float radius, float intensity = 1f)
    {
        try
        {
            if (!Ensure() || Far(pos)) return;
            radius = Mathf.Clamp(radius, 0.6f, 14f);
            intensity = Mathf.Clamp(intensity, 0.3f, 2.5f);
            float punch = Mathf.Sqrt(intensity);
            Color c = Palette.Element(el);
            Vector3 ground = new(pos.X, 0.1f, pos.Z);
            Vector3 core = new(pos.X, Mathf.Max(pos.Y, 0.35f), pos.Z);

            // impact: a white-hot core for a few frames, a light flash, and the shock ring racing to the edge of the area
            FlashFx(null, core, Vector3.One, radius * 0.16f, radius * (0.32f + 0.08f * punch), Hot(c, 0.45f), 0.14f + 0.012f * radius, 0.85f + 0.3f * punch, 3f);
            FlashLight(core + Vector3.Up * 0.5f, LightTint(c), 2.4f + 3f * punch, radius * 2f + 3f, 0.32f);
            RingFx(null, ground, radius, c, 0.3f + radius * 0.035f, 1.1f + 0.6f * punch, 0.13f);
            // body and linger
            BurstParticles(core, el, radius, intensity);
            if (el == Element.Lightning && radius >= 1.5f)
            {
                for (int i = 0; i < 3; i++)
                {
                    float ang = Rng.Range(0f, Mathf.Tau), d = radius * Rng.Range(0.6f, 0.95f);
                    BeamCore(core + Vector3.Up * 0.4f, ground + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d), c, 0.22f, 0.16f, true, 1.3f);
                }
            }
        }
        catch (Exception e) { Fail(nameof(Burst), e); }
    }

    /// <summary>Small impact where a blow or shot lands.</summary>
    public static void Hit(Vector3 pos, Element el, bool crit)
    {
        try
        {
            if (!Ensure() || Far(pos)) return;
            Color c = Palette.Element(el);
            var pm = HitPm(el, crit, out var look, out float life);
            OneShot(null, pos, pm, look, crit ? 14 : 7, life, 0.95f, crit ? 2 : 1, 4f);
            float s = crit ? 0.3f : 0.2f;
            FlashFx(null, pos, Vector3.One, s, s * 1.8f, Hot(c, 0.5f), crit ? 0.14f : 0.1f, crit ? 1.3f : 1.0f, 3f);
            if (crit) FlashLight(pos, LightTint(c), 2.4f, 4.5f, 0.18f);
        }
        catch (Exception e) { Fail(nameof(Hit), e); }
    }

    /// <summary>Melee swipe at chest height, facing the actor's Facing. arcDeg ≥ 359 is a full spin.</summary>
    public static void Slash(Actor a, float radius, float arcDeg, Element el)
    {
        try
        {
            if (!Live(a) || !Ensure()) return;
            radius = Mathf.Max(radius, 0.6f);
            bool full = arcDeg >= 359f;
            bool plain = el == Element.Physical;
            Color c = plain ? new Color(0.82f, 0.72f, 0.58f) : Palette.Element(el).Lerp(Colors.White, 0.08f);
            var q = MeshNode(_quad, _mSlash);
            q.Scale = new Vector3(radius, 1f, radius);
            Set(q, PT, 0f);
            Set(q, PArc, full ? 3.2f : Mathf.DegToRad(Mathf.Clamp(arcDeg, 20f, 358f) * 0.5f));
            Set(q, PTint, c);
            Set(q, PGain, plain ? 0.8f : 1.05f);
            Vector3 off = new(0f, Mathf.Clamp(a.HeightM * 0.55f, 0.6f, 2.4f), 0f);
            Vector3 at = a.GlobalPosition + off;
            if (!Spawn(q, at, a.Facing)) { q.Free(); return; }
            var an = NewAnim(FnT, q, full ? 0.22f : 0.16f);
            an.G1 = q; an.D = 1f; an.Actor = a; an.V1 = off;
            if (!plain && !full && !Far(at))
            {
                // a few flecks of the element thrown off the blade
                var pm = HitPm(el, false, out var look, out float life);
                Vector2 f = a.Forward;
                OneShot(null, at + new Vector3(f.X, 0f, f.Y) * (radius * 0.6f), pm, look, 6, life, 0.9f, 0, 4f);
            }
        }
        catch (Exception e) { Fail(nameof(Slash), e); }
    }

    /// <summary>A plain expanding ring on the ground.</summary>
    public static void Ring(Vector3 pos, float radius, Color c, float seconds)
    {
        try
        {
            if (!Ensure() || Far(pos)) return;
            RingFx(null, new Vector3(pos.X, Mathf.Max(pos.Y, 0.08f), pos.Z), Mathf.Max(radius, 0.3f), c, Mathf.Max(seconds, 0.1f), 0.85f, 0.09f);
        }
        catch (Exception e) { Fail(nameof(Ring), e); }
    }

    /// <summary>A beam between two points; jagged and flickering for lightning, a straight soft stream otherwise.</summary>
    public static void Beam(Vector3 from, Vector3 to, Element el, float width = 0.25f, float seconds = 0.2f)
    {
        try
        {
            if (!Ensure() || Far(to)) return;
            Color c = Palette.Element(el);
            bool jag = el == Element.Lightning;
            seconds = Mathf.Max(seconds, 0.06f);
            BeamCore(from, to, c, Mathf.Max(width, 0.05f) * (jag ? 1.7f : 1.25f), seconds, jag, jag ? 1.9f : 1.3f);
            // impact at the far end
            FlashFx(null, to, Vector3.One, 0.22f, 0.5f, Hot(c), 0.12f, 1.8f, 1.6f);
            FlashLight(to, LightTint(c), 2.2f, 5f, Mathf.Min(seconds + 0.08f, 0.4f));
        }
        catch (Exception e) { Fail(nameof(Beam), e); }
    }

    /// <summary>Circle on the ground that fills over <paramref name="delay"/>, then frees itself. Hostile unless friendly is set.</summary>
    public static Node3D Telegraph(Vector2 pos, float radius, float delay, Color c, bool friendly = false)
    {
        try
        {
            if (Ensure())
            {
                radius = Mathf.Max(radius, 0.3f);
                return Tele(new Vector3(pos.X, friendly ? 0.07f : 0.09f, pos.Y), 0f, new Vector2(radius, radius), 0f, 1f, 0f, delay, c, friendly);
            }
        }
        catch (Exception e) { Fail(nameof(Telegraph), e); }
        return StubInWorld();
    }

    /// <summary>Cone in front of an actor (hostile unless the actor fights for the player).</summary>
    public static Node3D TelegraphCone(Actor a, float range, float arcDeg, float delay, Color c)
    {
        try
        {
            if (Live(a) && Ensure())
            {
                bool friendly = a.Faction == Faction.Player;
                range = Mathf.Max(range, 0.5f);
                float arc = arcDeg >= 359f ? 3.2f : Mathf.DegToRad(Mathf.Clamp(arcDeg, 5f, 358f) * 0.5f);
                Vector3 p = a.GlobalPosition;
                return Tele(new Vector3(p.X, friendly ? 0.07f : 0.09f, p.Z), a.Facing, new Vector2(range, range), 1f, arc, 0f, delay, c, friendly);
            }
        }
        catch (Exception e) { Fail(nameof(TelegraphCone), e); }
        return StubInWorld();
    }

    /// <summary>Hostile rectangle starting at <paramref name="from"/>: <paramref name="length"/> long, <paramref name="width"/> wide in total.</summary>
    public static Node3D TelegraphLine(Vector2 from, Vector2 dir, float length, float width, float delay, Color c)
    {
        try
        {
            if (Ensure())
            {
                dir = dir.LengthSquared() < 1e-6f ? new Vector2(0f, 1f) : dir.Normalized();
                length = Mathf.Max(length, 0.5f); width = Mathf.Max(width, 0.3f);
                Vector2 mid = from + dir * (length * 0.5f);
                return Tele(new Vector3(mid.X, 0.09f, mid.Y), Mathf.Atan2(dir.X, dir.Y), new Vector2(width * 0.5f, length * 0.5f), 2f, 1f, 0f, delay, c, false);
            }
        }
        catch (Exception e) { Fail(nameof(TelegraphLine), e); }
        return StubInWorld();
    }

    /// <summary>Hostile donut: safe inside <paramref name="inner"/>, dangerous out to <paramref name="outer"/>.</summary>
    public static Node3D TelegraphRing(Vector2 pos, float inner, float outer, float delay, Color c)
    {
        try
        {
            if (Ensure())
            {
                outer = Mathf.Max(outer, 0.5f);
                float frac = Mathf.Clamp(inner / outer, 0f, 0.97f);
                return Tele(new Vector3(pos.X, 0.09f, pos.Y), 0f, new Vector2(outer, outer), 3f, 1f, frac, delay, c, false);
            }
        }
        catch (Exception e) { Fail(nameof(TelegraphRing), e); }
        return StubInWorld();
    }

    /// <summary>A body falling along a slanted path with a trail; it touches the ground exactly after <paramref name="delay"/>.</summary>
    public static void Meteor(Vector2 target, float delay, Element el, float radius)
    {
        try
        {
            if (!Ensure()) return;
            delay = Mathf.Max(delay, 0.05f);
            radius = Mathf.Clamp(radius, 0.8f, 12f);
            Color c = Palette.Element(el);
            Vector3 end = new(target.X, 0.3f, target.Y);
            if (Far(end)) return;
            if (el == Element.Lightning) { SkyBolt(end, delay, radius, c); return; }

            float h = Mathf.Clamp(5f + delay * 16f, 7f, 20f);
            Vector3 start = end + new Vector3(-0.42f, 1f, -0.3f) * h;
            float size = Mathf.Clamp(radius * 0.19f, 0.26f, 0.95f);
            var root = new Node3D { Name = "FxMeteor" };
            if (el == Element.Physical)
            {
                // a heavy bolt dropping out of the dusk, point first
                root.Basis = Basis.LookingAt(-(end - start).Normalized(), Vector3.Up);
                float len = 1.6f + size, thick = 0.05f + size * 0.05f;
                var shaft = MeshNode(_shaft, _mShaft);
                shaft.Scale = new Vector3(thick, len, thick);
                shaft.RotationDegrees = new Vector3(90f, 0f, 0f);
                root.AddChild(shaft);
                var streak = Orb(new Vector3(thick * 2.6f, thick * 2.6f, len * 1.4f), new Color(0.9f, 0.86f, 0.76f), 0.7f, 2.2f);
                streak.Position = new Vector3(0f, 0f, -len * 0.9f);
                root.AddChild(streak);
            }
            else
            {
                root.AddChild(Orb(Vector3.One * (size * 1.7f), c, 1.3f, 2.4f));
                root.AddChild(Orb(Vector3.One * (size * 0.75f), Hot(c, 0.8f), 2.4f, 1.3f));
            }
            var trail = TrailEmitter(el, true, Mathf.Clamp(size / 0.3f, 1.2f, 3f));
            root.AddChild(trail);
            if (!Spawn(root, start)) { root.Free(); return; }
            var a = NewAnim(FnMeteor, root, delay);
            a.V0 = start; a.V1 = end; a.El = el; a.A = radius; a.P1 = trail;
        }
        catch (Exception e) { Fail(nameof(Meteor), e); }
    }

    /// <summary>
    /// The look of a shot; the caller parents it under the Projectile (local +Z is the direction of travel).
    /// size 0 small, 1 big (carries a small light), 2 ground wave as wide as <paramref name="radius"/>.
    /// </summary>
    public static Node3D ProjectileVisual(Element el, int size, float radius, string id, bool hostile = false)
    {
        var root = new Node3D { Name = "FxProjectile" };
        try
        {
            if (!Ensure()) return root;
            root.SetMeta(MetaProj, true);
            id ??= "";
            Color c = Palette.Element(el);
            if (size >= 2) BuildWave(root, el, c, radius, hostile);
            else if (el == Element.Physical || id.Contains("hu_") || id.Contains("sb_spear")) BuildShaft(root, el, c, size, radius, id, hostile);
            else BuildOrb(root, el, c, size, radius, hostile);
        }
        catch (Exception e) { Fail(nameof(ProjectileVisual), e); }
        return root;
    }

    /// <summary>Ends a projectile: its trail is left behind in the world to fade out naturally, then the projectile is freed.</summary>
    public static void ReleaseProjectile(Projectile p)
    {
        if (p == null || !GodotObject.IsInstanceValid(p)) return;
        try
        {
            if (_ready && p.IsInsideTree())
            {
                for (int i = p.GetChildCount() - 1; i >= 0; i--)
                {
                    if (p.GetChild(i) is not Node3D v || !v.HasMeta(MetaProj)) continue;
                    for (int j = v.GetChildCount() - 1; j >= 0; j--)
                        if (v.GetChild(j) is GpuParticles3D gp) ReleaseTrail(gp);
                }
            }
        }
        catch (Exception e) { Fail(nameof(ReleaseProjectile), e); }
        p.QueueFree();
    }

    /// <summary>
    /// The look of a persistent ground area; the caller parents it under the zone node (at ground level).
    /// Fades in, fades out over the last half second of <paramref name="seconds"/>; hostile ones are edged in Palette.Danger.
    /// </summary>
    public static Node3D ZoneVisual(float radius, Element el, bool hostile, float seconds)
    {
        var root = new Node3D { Name = "FxZone" };
        try
        {
            if (!Ensure()) return root;
            radius = Mathf.Clamp(radius, 0.5f, 30f);
            seconds = Mathf.Max(seconds, 0.3f);
            Color c = el == Element.Physical ? new Color(0.78f, 0.7f, 0.56f) : Palette.Element(el);
            float area = radius * radius;

            // the floor layer: scorch / rime / pool with its glowing veins, ragged for friendly areas, a hard danger rim for hostile ones
            var disc = MeshNode(_quad, _mZone);
            disc.Scale = new Vector3(radius, 1f, radius);
            disc.Position = new Vector3(0f, 0.02f, 0f);
            Set(disc, PTint, c);
            Set(disc, PEdge, hostile ? Palette.Danger : c);
            Set(disc, PFade, 0f);
            Set(disc, PHostile, hostile ? 1f : 0f);
            Set(disc, PSeed, Rng.F());
            Set(disc, PRadius, radius);
            Set(disc, PKind, (float)ZoneKind(el));
            if (hostile) disc.SortingOffset = 0.5f;
            root.AddChild(disc);

            // the volume standing on it
            float life;
            GpuParticles3D parts;
            MultiMeshInstance3D flameField = null;
            switch (el)
            {
                case Element.Fire:
                {
                    // a field of flames standing on the scorch (the same flame cards the torches burn with), a few loose
                    // tongues licking between them, embers climbing out, thin smoke above
                    var field = FlameField(radius, Mathf.Clamp((int)(6f + area * 2.2f), 8, 48));
                    if (field != null) { root.AddChild(field); flameField = field; }
                    var flames = Loop(ZoneBodyPm(el, radius, out life), Look.Puff, Mathf.Clamp((int)(4f + area * 1.0f), 5, 28), life, radius + 3f);
                    flames.Position = new Vector3(0f, 0.1f, 0f);
                    root.AddChild(flames);
                    parts = flames;
                    var embers = Loop(ZoneEmberPm(radius), Look.Dot, Mathf.Clamp((int)(4f + area * 1.1f), 5, 30), 1.7f, radius + 4f);
                    embers.Position = new Vector3(0f, 0.3f, 0f);
                    root.AddChild(embers);
                    var smoke = Loop(ZoneSmokePm(el, radius), Look.Smoke, Mathf.Clamp((int)(3f + area * 0.5f), 4, 14), 2.6f, radius + 5f);
                    smoke.Position = new Vector3(0f, 1.1f, 0f);
                    root.AddChild(smoke);
                    break;
                }
                case Element.Frost:
                case Element.Poison:
                case Element.Shadow:
                {
                    // a low bank of mist / fumes / darkness, and the motes of the element rising through it
                    var bank = Loop(ZoneBodyPm(el, radius, out life), el == Element.Shadow ? Look.Smoke : Look.Puff, Mathf.Clamp((int)(4f + area * 1.0f), 5, 28), life, radius + 3f);
                    bank.Position = new Vector3(0f, 0.25f, 0f);
                    root.AddChild(bank);
                    var motes = Loop(ZonePm(el, radius, out var look, out float moteLife), look, Mathf.Clamp((int)(4f + area * 0.9f), 6, 40), moteLife, radius + 3f);
                    motes.Position = new Vector3(0f, 0.12f, 0f);
                    root.AddChild(motes);
                    parts = bank;
                    life = Mathf.Max(life, moteLife);
                    break;
                }
                default:
                {
                    // sparks jumping off charged ground (lightning), or a hail of bolts from above (physical)
                    var pm = ZonePm(el, radius, out var look, out life);
                    parts = Loop(pm, look, Mathf.Clamp((int)(4f + area * 0.9f), 6, 40), life, radius + 3f);
                    if (el == Element.Physical)
                    {
                        // the hail starts 5.5 m up and slants: hang the emitter where the bolts will come down inside the circle
                        parts.Position = new Vector3(0.82f, 5.5f, 0.55f);
                        parts.VisibilityAabb = new Aabb(-radius - 3f, -7f, -radius - 3f, radius * 2f + 6f, 9f, radius * 2f + 6f);
                    }
                    else parts.Position = new Vector3(0f, 0.12f, 0f);
                    root.AddChild(parts);
                    break;
                }
            }

            float energy = el == Element.Fire ? 2.4f : el == Element.Shadow ? 0.6f : 1.3f;
            var light = SoftLight(hostile ? c.Lerp(Palette.Danger, 0.3f) : LightTint(c), 0f, radius * 1.7f + 3f);
            if (light != null) { light.Position = new Vector3(0f, 1.2f, 0f); root.AddChild(light); }

            var a = NewAnim(FnZone, root, seconds);
            a.G1 = disc; a.P1 = parts; a.Light = light; a.A = energy; a.B = radius; a.El = el; a.C = Rng.Range(0.2f, 0.5f);
            a.V0 = new Vector3(life, Rng.Range(0f, 40f), 0f);
            a.G2 = flameField;
            if (flameField != null) flameField.SetInstanceShaderParameter(PFade, 0f);
        }
        catch (Exception e) { Fail(nameof(ZoneVisual), e); }
        return root;
    }

    /// <summary>
    /// Flames standing on a disc of ground: one MultiMesh of camera-facing flame cards (env_flame), denser and taller
    /// toward the middle. Its instance uniform "fade" brings the whole field in and out.
    /// </summary>
    private static MultiMeshInstance3D FlameField(float radius, int count, float hMin = 0f, float hMax = 0f)
    {
        if (_flameQuad == null || _mFlame is not ShaderMaterial) return null;
        bool fixedHeights = hMax > 0.01f;
        count = Math.Max(3, (int)(count * (fixedHeights ? 1f : Density)));
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = count > 4 ? _flameFieldQuad : _flameQuad, InstanceCount = count };
        for (int i = 0; i < count; i++)
        {
            float ang = Rng.Range(0f, Mathf.Tau), d = radius * 0.9f * Mathf.Sqrt(Rng.F());
            float k = 1f - d / Mathf.Max(radius, 0.1f);
            float h = fixedHeights ? Mathf.Lerp(hMin, hMax, k * Rng.Range(0.5f, 1f))
                : Mathf.Lerp(0.55f, 1.5f, k * Rng.Range(0.5f, 1f)) * Mathf.Clamp(0.8f + radius * 0.1f, 0.85f, 1.6f);
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(Mathf.Cos(ang) * d, 0.03f, Mathf.Sin(ang) * d)));
            // a clustered fire overlaps its own cards: share the brightness between them
            float share = fixedHeights ? Mathf.Clamp(1.1f / Mathf.Sqrt(count), 0.26f, 1f) : 0f;
            mm.SetInstanceCustomData(i, new Color(h, Rng.F(), Rng.Range(1.1f, 1.65f), share));
        }
        var node = new MultiMeshInstance3D
        {
            Name = "Flames", Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 3f,
        };
        NoGi(node);
        return node;
    }

    private static int ZoneKind(Element el) => el switch
    {
        Element.Fire => 0, Element.Frost => 1, Element.Lightning => 2, Element.Poison => 3, Element.Shadow => 4, _ => 5,
    };

    /// <summary>An armed trap lying on the ground; the caller parents it under the trap node.</summary>
    public static Node3D TrapVisual(Element el)
    {
        var root = new Node3D { Name = "FxTrap" };
        try
        {
            if (!Ensure()) return root;
            Color c = el == Element.Physical ? Palette.Ember : Palette.Element(el);
            var plate = MeshNode(_plate, _mIron);
            plate.Position = new Vector3(0f, 0.03f, 0f);
            root.AddChild(plate);
            for (int i = 0; i < 5; i++)
            {
                float ang = i * Mathf.Tau / 5f;
                var sp = MeshNode(_spike, _mIron);
                sp.Position = new Vector3(Mathf.Cos(ang) * 0.26f, 0.18f, Mathf.Sin(ang) * 0.26f);
                sp.Rotation = new Vector3(Mathf.Sin(ang) * 0.35f, 0f, -Mathf.Cos(ang) * 0.35f);      // lean outward
                root.AddChild(sp);
            }
            // a small heart of light that breathes while the trap waits
            var glow = Orb(Vector3.One * 0.13f, c, 1.4f, 1.6f);
            glow.Position = new Vector3(0f, 0.16f, 0f);
            Set(glow, PPulse, 1f);
            root.AddChild(glow);
            // a thin, quiet ring where it will spring
            var q = MeshNode(_quad, _mTele);
            q.Scale = new Vector3(1.9f, 1f, 1.9f);
            q.Position = new Vector3(0f, 0.02f, 0f);
            Set(q, PT, 0f); Set(q, PTint, c); Set(q, PEdge, c); Set(q, PMode, 3f); Set(q, PArc, 1f); Set(q, PInner, 0.9f);
            Set(q, PSoft, 1f); Set(q, PFade, 0.55f); Set(q, PHalf, new Vector2(1.9f, 1.9f));
            root.AddChild(q);
        }
        catch (Exception e) { Fail(nameof(TrapVisual), e); }
        return root;
    }

    /// <summary>The signature death: embers and ash lifting from the body while the corpse dissolves.</summary>
    public static void Ash(Actor a)
    {
        try
        {
            if (!Live(a) || !Ensure()) return;
            Vector3 pos = a.GlobalPosition;
            if (Far(pos)) return;
            bool boss = a.IsBoss;
            float h = Mathf.Max(a.HeightM, 0.8f);
            float r = Mathf.Clamp(Mathf.Max(a.Radius * 1.3f, h * 0.38f), 0.4f, 3.4f);
            int rb = Mathf.Clamp(Mathf.RoundToInt(r * 4f), 2, 14);
            int value = boss || a.IsElite ? 3 : 2;
            Vector3 at = new(pos.X, Mathf.Max(pos.Y, 0f) + 0.32f, pos.Z);
            float life = boss ? 2.6f : 1.5f, explosive = boss ? 0.15f : 0.4f;

            // ash: grey flakes peeling off the whole body and drifting away on the wind
            OneShot(null, at, AshFlakePm(rb), Look.Chip, Mathf.Clamp((int)(14f + r * 22f), 16, 72), life, explosive, value, r + 4f);
            // embers: the last of the fire leaving the flesh
            OneShot(null, at, AshEmberPm(rb), Look.Dot, Mathf.Clamp((int)(10f + r * 16f), 12, 52), life * 0.8f, explosive, value, r + 4f);
            // a low glow where the body burns from the inside
            float burn = boss ? 2.4f : 1.1f;
            var glow = Orb(new Vector3(r * 1.1f, 0.28f, r * 1.1f), Palette.Ember, 0.9f, 1.5f);
            if (Spawn(glow, new Vector3(pos.X, Mathf.Max(pos.Y, 0f) + 0.2f, pos.Z)))
            {
                var an = NewAnim(FnT, glow, burn);
                an.G1 = glow; an.C = 0.3f; an.D = 1f;
            }
            else glow.Free();
            if (value == 3) FlashLight(at + Vector3.Up * 0.6f, Palette.Ember, boss ? 3.2f : 1.8f, r * 2f + 3.5f, burn);
        }
        catch (Exception e) { Fail(nameof(Ash), e); }
    }

    /// <summary>A puff of dust at ground level (rolls, landings).</summary>
    public static void Dust(Vector3 pos)
    {
        try
        {
            if (!Ensure() || Far(pos)) return;
            OneShot(null, new Vector3(pos.X, Mathf.Max(pos.Y, 0f) + 0.15f, pos.Z), DustPm(), Look.Smoke, 8, 0.55f, 0.9f, 0, 4f);
        }
        catch (Exception e) { Fail(nameof(Dust), e); }
    }

    /// <summary>Warm green motes rising around a healed actor.</summary>
    public static void Heal(Actor a)
    {
        try
        {
            if (!Live(a) || !Ensure()) return;
            Vector3 pos = a.GlobalPosition;
            if (Far(pos)) return;
            Color good = Palette.Good;
            OneShot(a, new Vector3(0f, 0.15f, 0f), HealPm(), Look.Dot, 16, 0.9f, 0.5f, 1, 4f);
            RingFx(a, new Vector3(0f, 0.08f, 0f), Mathf.Max(a.Radius * 2.6f, 1.3f), good, 0.5f, 0.9f, 0.14f);
            FlashLight(pos + Vector3.Up * 1.1f, good.Lerp(Colors.White, 0.3f), 1.3f, 4.5f, 0.5f);
        }
        catch (Exception e) { Fail(nameof(Heal), e); }
    }

    /// <summary>Level up: a pillar of ember light, rings across the ground and a fountain of sparks.</summary>
    public static void LevelUp(Actor a)
    {
        try
        {
            if (!Live(a) || !Ensure()) return;
            Color ember = new(1f, 0.62f, 0.2f);
            const float height = 10f, width = 0.8f;
            // gather → pillar
            var pil = Pillar(ember, width * 0.15f, height, 1.7f, 1.6f, 0.7f, 0.5f);
            Set(pil, PT, 0.6f);
            a.AddChild(pil);
            var an = NewAnim(FnPillar, pil, 1.4f);
            an.G1 = pil; an.A = width; an.B = height;
            // impact: rings and light
            RingFx(a, new Vector3(0f, 0.1f, 0f), 5.5f, Palette.Gold, 0.75f, 1.6f, 0.14f);
            RingFx(a, new Vector3(0f, 0.12f, 0f), 2.6f, ember, 0.45f, 1.8f, 0.2f);
            FlashFx(a, new Vector3(0f, 1.1f, 0f), Vector3.One, 0.5f, 1.6f, Hot(ember, 0.7f), 0.22f, 2.4f, 1.5f);
            FlashLight(a.GlobalPosition + Vector3.Up * 1.4f, new Color(1f, 0.78f, 0.45f), 6f, 12f, 1f);
            // body and linger: sparks thrown up, embers circling as they rise
            OneShot(a, new Vector3(0f, 0.3f, 0f), LevelSparkPm(), Look.Streak, 48, 1.2f, 0.75f, 3, 8f);
            OneShot(a, new Vector3(0f, 0.2f, 0f), LevelEmberPm(), Look.Dot, 30, 1.6f, 0.3f, 3, 8f);
        }
        catch (Exception e) { Fail(nameof(LevelUp), e); }
    }

    /// <summary>Vanish / appear flash at a blink's origin or destination.</summary>
    public static void Teleport(Vector3 pos, Element el)
    {
        try
        {
            if (!Ensure() || Far(pos)) return;
            Color c = Palette.Element(el);
            FlashFx(null, pos, new Vector3(0.4f, 1.6f, 0.4f), 1.3f, 0.5f, Hot(c), 0.22f, 2f, 1.5f);
            OneShot(null, pos, TeleportPm(el), Look.Streak, 16, 0.4f, 0.9f, 2, 5f);
            RingFx(null, new Vector3(pos.X, 0.09f, pos.Z), 1.5f, c, 0.35f, 1.3f, 0.16f);
            FlashLight(pos, LightTint(c), 2.5f, 6f, 0.3f);
        }
        catch (Exception e) { Fail(nameof(Teleport), e); }
    }

    /// <summary>Motes circling an actor for the length of a buff. big = ultimate: denser, with a ring of light on the ground.</summary>
    public static void BuffAura(Actor a, Element el, float seconds, bool big)
    {
        try
        {
            if (!Live(a) || !Ensure()) return;
            seconds = Mathf.Max(seconds, 0.5f);
            Color c = el == Element.Physical ? new Color(0.95f, 0.88f, 0.72f) : Palette.Element(el);
            // recasting replaces the aura of the same element instead of stacking emitters
            for (int i = 0; i < _anims.Count; i++)
            {
                var o = _anims[i];
                if (o.Step == FnAura && o.B == 1f && o.Actor == a && o.El == el && o.T < o.Dur) o.Dur = o.T;
            }

            var root = new Node3D { Name = "FxAura" };
            var parts = Loop(AuraPm(el, big), Look.Dot, big ? 26 : 12, 1.1f, 5f);
            parts.Position = new Vector3(0f, 0.15f, 0f);
            root.AddChild(parts);
            MeshInstance3D ring = null;
            OmniLight3D light = null;
            if (big)
            {
                ring = MeshNode(_quad, _mZone);
                ring.Scale = new Vector3(1.8f, 1f, 1.8f);
                ring.Position = new Vector3(0f, 0.07f, 0f);
                Set(ring, PTint, c); Set(ring, PEdge, c); Set(ring, PFade, 0f); Set(ring, PHostile, 0f); Set(ring, PSeed, Rng.F()); Set(ring, PRadius, 1.8f); Set(ring, PKind, 6f);
                root.AddChild(ring);
                light = SoftLight(LightTint(c), 0f, 6f);
                if (light != null) { light.Position = new Vector3(0f, 1.4f, 0f); root.AddChild(light); }
            }
            a.AddChild(root);
            var an = NewAnim(FnAura, root, seconds);
            an.P1 = parts; an.G1 = ring; an.Light = light; an.Actor = a; an.El = el; an.A = 1.2f; an.B = 1f; an.C = 1.2f;

            // the moment it takes hold
            RingFx(a, new Vector3(0f, 0.1f, 0f), big ? 4.2f : 1.7f, c, big ? 0.55f : 0.4f, big ? 1.7f : 1.1f, 0.15f);
            if (big)
            {
                FlashFx(a, new Vector3(0f, a.HeightM * 0.55f, 0f), Vector3.One, 0.4f, 1.5f, Hot(c), 0.2f, 2.2f, 1.6f);
                FlashLight(a.GlobalPosition + Vector3.Up * 1.3f, LightTint(c), 4.5f, 9f, 0.45f);
            }
        }
        catch (Exception e) { Fail(nameof(BuffAura), e); }
    }

    /// <summary>The gather beat of a cast: a brief glow drawing in at the casting hand.</summary>
    public static void CastGlow(Actor a, Element el)
    {
        try
        {
            if (!Live(a) || !Ensure()) return;
            Vector3 hand = HandPos(a);
            if (Far(hand)) return;
            Color c = Palette.Element(el);
            FlashFx(null, hand, Vector3.One, 0.36f, 0.12f, Hot(c, 0.45f), 0.16f, 1.6f, 1.7f);
            OneShot(null, hand, GatherPm(el), Look.Dot, 6, 0.28f, 0.8f, 0, 3f);
        }
        catch (Exception e) { Fail(nameof(CastGlow), e); }
    }

    /// <summary>Particles shed along the path of a dashing actor.</summary>
    public static void DashTrail(Actor a, Element el, float seconds)
    {
        try
        {
            if (!Live(a) || !Ensure()) return;
            seconds = Mathf.Clamp(seconds, 0.08f, 3f);
            var pm = DashPm(el, out var look, out float life, out int amount);
            var root = new Node3D { Name = "FxDash" };
            var parts = Loop(pm, look, amount, life, 16f);
            parts.Position = new Vector3(0f, el == Element.Physical ? 0.2f : 0.95f, 0f);
            root.AddChild(parts);
            a.AddChild(root);
            var an = NewAnim(FnAura, root, seconds);
            an.P1 = parts; an.C = life + 0.1f;
        }
        catch (Exception e) { Fail(nameof(DashTrail), e); }
    }

    /// <summary>A ghost left standing where a delayed burst will go off after <paramref name="seconds"/>.</summary>
    public static void Afterimage(Vector2 pos, Element el, float seconds)
    {
        try
        {
            if (!Ensure()) return;
            seconds = Mathf.Clamp(seconds, 0.1f, 20f);
            Vector3 at = new(pos.X, 0.95f, pos.Y);
            if (Far(at)) return;
            Color c = Palette.Element(el);
            var ghost = Orb(new Vector3(0.42f, 0.95f, 0.42f), c, 1f, 1.3f);
            Set(ghost, PT, 1f);
            Set(ghost, PPulse, 0.8f);
            if (!Spawn(ghost, at)) { ghost.Free(); return; }
            var an = NewAnim(FnGhost, ghost, seconds);
            an.G1 = ghost; an.V0 = new Vector3(0.42f, 0.95f, 0.42f);
            // a quiet timer at its feet
            Tele(new Vector3(pos.X, 0.07f, pos.Y), 0f, new Vector2(1.1f, 1.1f), 0f, 1f, 0f, seconds, c, true);
        }
        catch (Exception e) { Fail(nameof(Afterimage), e); }
    }

    /// <summary>A floating soul wisp. Lives until the caller frees it.</summary>
    public static Node3D RemnantWisp(Vector2 pos)
    {
        try
        {
            if (Ensure())
            {
                var root = new Node3D { Name = "FxWisp" };
                Color soul = new(0.62f, 0.95f, 0.78f);
                var halo = Orb(Vector3.One * 0.44f, new Color(0.62f, 0.5f, 0.95f), 0.55f, 3f);
                var orb = Orb(Vector3.One * 0.17f, soul, 1.7f, 1.5f);
                Set(halo, PBob, 0.12f); Set(halo, PPulse, 0.6f);
                Set(orb, PBob, 0.12f); Set(orb, PPulse, 1f);
                root.AddChild(halo); root.AddChild(orb);
                Spawn(root, new Vector3(pos.X, 1f, pos.Y));
                return root;
            }
        }
        catch (Exception e) { Fail(nameof(RemnantWisp), e); }
        return StubInWorld();
    }

    /// <summary>
    /// The beam above an item on the ground, in the rarity colour; the caller parents it under the loot node at ground level.
    /// Legendary and unique beams are taller, haloed and shed sparkles.
    /// </summary>
    public static Node3D LootBeam(Rarity r)
    {
        var root = new Node3D { Name = "FxLootBeam" };
        try
        {
            if (!Ensure()) return root;
            Color c = Palette.Rarity(r);
            float h, w, gain;
            switch (r)
            {
                case Rarity.Magic: h = 2.2f; w = 0.08f; gain = 0.9f; break;
                case Rarity.Rare: h = 3.0f; w = 0.1f; gain = 1.1f; break;
                case Rarity.Legendary: h = 5f; w = 0.13f; gain = 1.3f; break;
                case Rarity.Unique: h = 5.6f; w = 0.14f; gain = 1.35f; break;
                default: h = 1.2f; w = 0.06f; gain = 0.45f; break;
            }
            bool grand = r >= Rarity.Legendary;
            root.AddChild(Pillar(c, w, h, gain, 2.2f, 0.75f, grand ? 0.55f : 0.3f));
            if (grand) root.AddChild(Pillar(c, w * 3.2f, h * 0.8f, gain * 0.28f, 3f, 0.9f, 0.6f));

            // a pool of light at its foot
            float foot = grand ? 0.8f : 0.45f;
            var pool = MeshNode(_quad, _mZone);
            pool.Scale = new Vector3(foot, 1f, foot);
            pool.Position = new Vector3(0f, 0.05f, 0f);
            Set(pool, PTint, c); Set(pool, PEdge, c); Set(pool, PFade, Mathf.Min(1f, gain * 0.55f)); Set(pool, PHostile, 0f); Set(pool, PSeed, Rng.F()); Set(pool, PRadius, foot); Set(pool, PKind, 6f);
            root.AddChild(pool);

            if (grand)
            {
                var sparkle = Loop(SparklePm(r, c, h), Look.Dot, 10, 1.2f, 3f);
                sparkle.Position = new Vector3(0f, h * 0.4f, 0f);
                sparkle.Preprocess = 1.2f;
                root.AddChild(sparkle);
                var light = SoftLight(LightTint(c), 1.1f, 4.5f);
                if (light != null) { light.Position = new Vector3(0f, 0.9f, 0f); root.AddChild(light); }
            }
        }
        catch (Exception e) { Fail(nameof(LootBeam), e); }
        return root;
    }

    /// <summary>
    /// A looping fire for braziers, torches and campfires: flames, embers, smoke (from size 0.5) and, when
    /// <paramref name="light"/> is set, a flickering shadowless OmniLight. <paramref name="size"/> is roughly the flame width in metres.
    /// </summary>
    public static Node3D Fire(float size, bool light)
    {
        var root = new Node3D { Name = "FxFire" };
        try
        {
            if (!Ensure()) return root;
            int sb = Mathf.Clamp(Mathf.RoundToInt(size * 10f), 2, 40);
            float s = sb * 0.1f;

            // the body of the fire: a small cluster of flame cards (crisp tongues, a hot core, a halo); a few loose
            // puffs lick round them. Big fires get more and taller cards, never a brighter ball.
            // (a great fire is many tongues side by side, not one tongue four metres tall)
            var cards = FlameField(Mathf.Min(s * 0.3f, 1.1f), Mathf.Clamp((int)(2.5f + s * 3f), 3, 14), Mathf.Min(s * 0.75f, 1.1f), Mathf.Min(s * 1.45f, 2.3f));
            if (cards != null) root.AddChild(cards);
            var flames = Loop(FlamePm(sb), Look.Puff, Mathf.Clamp((int)(4f + s * 3f), 4, 12), 0.65f, s * 1.5f + 1f);
            flames.Position = new Vector3(0f, s * 0.15f, 0f);
            flames.Preprocess = 0.65f;
            root.AddChild(flames);

            var embers = Loop(FireEmberPm(sb), Look.Dot, Mathf.Clamp((int)(4f + s * 5f), 4, 14), 1.5f, s * 2f + 2f);
            embers.Position = new Vector3(0f, s * 0.3f, 0f);
            embers.Preprocess = 1.5f;
            root.AddChild(embers);

            if (s >= 0.5f)
            {
                var smoke = Loop(FireSmokePm(sb), Look.Smoke, 6, 2.2f, s * 2f + 2f);
                smoke.Position = new Vector3(0f, s * 0.9f, 0f);
                smoke.Preprocess = 2.2f;
                root.AddChild(smoke);
            }

            var glow = Orb(Vector3.One * Mathf.Min(s * 0.42f, 1.1f), new Color(1f, 0.5f, 0.16f), 0.5f / (1f + s * 0.4f), 2.4f);
            glow.Position = new Vector3(0f, s * 0.25f, 0f);
            Set(glow, PPulse, 0.7f);
            root.AddChild(glow);

            if (light)
            {
                float energy = 1.6f + 1.5f * Mathf.Sqrt(s);
                var l = new OmniLight3D
                {
                    LightColor = new Color(1f, 0.6f, 0.28f), LightEnergy = energy, OmniRange = 4.5f + s * 5.5f, OmniAttenuation = 1.3f,
                    ShadowEnabled = false, LightSpecular = 0.3f, DistanceFadeEnabled = true, DistanceFadeBegin = 55f, DistanceFadeLength = 10f,
                    Position = new Vector3(0f, s * 0.6f + 0.25f, 0f),
                };
                root.AddChild(l);
                var a = NewAnim(FnFlicker, root, 1f);
                a.Light = l; a.A = energy; a.B = Rng.Range(0f, 40f); a.I = _anims.Count & 1; a.Always = true;
            }
        }
        catch (Exception e) { Fail(nameof(Fire), e); }
        return root;
    }

    /// <summary>
    /// A standing portal: a swirling disc that turns to face the camera, motes spiralling up around it, a glow on
    /// the ground and a light. The caller parents it at ground level; <paramref name="radius"/> is half its width.
    /// </summary>
    public static Node3D Portal(Color c, float radius)
    {
        var root = new Node3D { Name = "FxPortal" };
        try
        {
            if (!Ensure()) return root;
            radius = Mathf.Clamp(radius, 0.4f, 6f);
            float tall = radius * 1.25f;

            var disc = MeshNode(_quadXY, _mSwirl);
            Vector3 full = new(radius, tall, 1f);
            disc.Scale = full * 0.05f;
            disc.Position = new Vector3(0f, tall + 0.12f, 0f);
            disc.ExtraCullMargin = radius * 2f;      // turned toward the camera in the shader, so cull generously
            // in the palette of the world: a pale blue-white core that keeps the caller's hue, well under the fires
            c = c.Lerp(new Color(0.6f, 0.72f, 1f), 0.3f);
            Set(disc, PTint, c); Set(disc, PGain, 0.6f); Set(disc, PFade, 1f); Set(disc, PUpright, 1f);
            root.AddChild(disc);
            var grow = NewAnim(FnGrow, disc, 0.5f);
            grow.V0 = full; grow.Always = true;

            var pool = MeshNode(_quad, _mZone);
            pool.Scale = new Vector3(radius * 1.15f, 1f, radius * 1.15f);
            pool.Position = new Vector3(0f, 0.05f, 0f);
            Set(pool, PTint, c); Set(pool, PEdge, c); Set(pool, PFade, 0.42f); Set(pool, PHostile, 0f); Set(pool, PSeed, Rng.F()); Set(pool, PRadius, radius * 1.15f); Set(pool, PKind, 6f);
            root.AddChild(pool);

            var motes = Loop(PortalPm(c, radius), Look.Dot, Mathf.Clamp((int)(10f + radius * 8f), 10, 30), 1.3f, radius + 3f);
            motes.Position = new Vector3(0f, 0.1f, 0f);
            motes.Preprocess = 1.3f;
            root.AddChild(motes);

            root.AddChild(new OmniLight3D
            {
                LightColor = LightTint(c), LightEnergy = 1.6f, OmniRange = radius * 4f + 3f, OmniAttenuation = 1.3f, ShadowEnabled = false,
                LightSpecular = 0.25f, DistanceFadeEnabled = true, DistanceFadeBegin = 55f, DistanceFadeLength = 10f,
                Position = new Vector3(0f, tall, 0f),
            });
        }
        catch (Exception e) { Fail(nameof(Portal), e); }
        return root;
    }

    /// <summary>Camera shake; respects the screen-shake setting and is safe before the camera exists.</summary>
    public static void Shake(float amount, float seconds)
    {
        try
        {
            if (amount <= 0f || seconds <= 0f) return;
            var g = Game.I;
            if (g == null || !GodotObject.IsInstanceValid(g)) return;
            if (g.Cfg != null && !g.Cfg.ScreenShake) return;
            var cam = g.Cam;
            if (cam == null || !GodotObject.IsInstanceValid(cam)) return;
            cam.Shake(Mathf.Min(amount, 1.5f), Mathf.Min(seconds, 1.5f));
        }
        catch (Exception e) { Fail(nameof(Shake), e); }
    }

    /// <summary>A brief slow-motion punch on heavy hits; safe before the game exists.</summary>
    public static void HitStop(float seconds)
    {
        try
        {
            if (seconds <= 0f) return;
            var g = Game.I;
            if (g == null || !GodotObject.IsInstanceValid(g)) return;
            g.HitStop(Mathf.Min(seconds, 0.25f));
        }
        catch (Exception e) { Fail(nameof(HitStop), e); }
    }

    /// <summary>A short flash of light without shadows (pooled, at most 12 alive). Extra to the contract, for bosses and events.</summary>
    public static void LightFlash(Vector3 pos, Color c, float energy, float range, float seconds)
    {
        try { if (Ensure() && !Far(pos)) FlashLight(pos, c, energy, range, seconds); }
        catch (Exception e) { Fail(nameof(LightFlash), e); }
    }

    /// <summary>A soft ball of light that swells and fades. Extra to the contract, for bosses and events.</summary>
    public static void Glow(Vector3 pos, Color c, float size, float seconds)
    {
        try
        {
            if (!Ensure() || Far(pos)) return;
            size = Mathf.Max(size, 0.05f);
            FlashFx(null, pos, Vector3.One, size * 0.5f, size, c, Mathf.Max(seconds, 0.05f), 1.8f, 1.6f);
        }
        catch (Exception e) { Fail(nameof(Glow), e); }
    }

    // ═════════════════════════ building blocks ═════════════════════════

    // At the dark level a core that is half white reads as a lamp: effects keep their element colour to the centre.
    private static Color Hot(Color c, float w = 0.6f) => c.Lerp(Colors.White, w * 0.55f);
    private static Color LightTint(Color c) => c.Lerp(Colors.White, 0.25f);
    private static Color Mul(Color c, float k, float a) => new(c.R * k, c.G * k, c.B * k, a);
    private static bool Live(Node3D n) => n != null && GodotObject.IsInstanceValid(n) && n.IsInsideTree();

    private static void Set(GeometryInstance3D g, StringName p, float v) => g.SetInstanceShaderParameter(p, v);
    private static void Set(GeometryInstance3D g, StringName p, Color v) => g.SetInstanceShaderParameter(p, v);
    private static void Set(GeometryInstance3D g, StringName p, Vector2 v) => g.SetInstanceShaderParameter(p, v);

    /// <summary>Particle density by the graphics quality setting (0 低 … 3 極致).</summary>
    private static float Density => Mathf.Min(Gfx.P.Particles, 1.15f);

    private static void Fail(string where, Exception e)
    {
        _errors++;
        if (_errors <= 12 || _errors % 200 == 0) Log.Error("fx", $"{where} failed (#{_errors})", e);
    }

    private static bool Far(Vector3 pos)
    {
        var p = World.P;
        if (p == null || !GodotObject.IsInstanceValid(p) || !p.IsInsideTree()) return false;
        Vector3 d = p.GlobalPosition - pos;
        return d.X * d.X + d.Z * d.Z > CullDistance * CullDistance;
    }

    /// <summary>Where free-standing effects live: the level, so they vanish with it.</summary>
    private static Node Parent()
    {
        var r = World.Root;
        if (r != null && GodotObject.IsInstanceValid(r) && r.IsInsideTree()) return r;
        var g = Game.I;
        if (g != null && GodotObject.IsInstanceValid(g))
        {
            var w = g.WorldRoot;
            if (w != null && GodotObject.IsInstanceValid(w) && w.IsInsideTree()) return w;
        }
        _tree ??= Engine.GetMainLoop() as SceneTree;
        return _tree?.Root;
    }

    /// <summary>Adds a node to the world at a world position. Returns false (node untouched) when there is no world to add it to.</summary>
    private static bool Spawn(Node3D n, Vector3 pos, float yaw = 0f)
    {
        var parent = Parent();
        if (parent == null) return false;
        if (parent is Node3D p3) pos = p3.ToLocal(pos);
        n.Position = pos;
        if (yaw != 0f) n.Rotation = new Vector3(0f, yaw, 0f);
        parent.AddChild(n);
        return true;
    }

    /// <summary>A harmless empty node for callers that expect one when effects are unavailable.</summary>
    private static Node3D StubInWorld()
    {
        var n = new Node3D { Name = "FxNone" };
        try { Spawn(n, Vector3.Zero); } catch (Exception e) { Fail(nameof(StubInWorld), e); }
        return n;
    }

    private static Camera3D Cam()
    {
        var g = Game.I;
        if (g != null && GodotObject.IsInstanceValid(g))
        {
            var rig = g.Cam;
            if (rig != null && GodotObject.IsInstanceValid(rig))
            {
                var c = rig.Cam;
                if (c != null && GodotObject.IsInstanceValid(c) && c.IsInsideTree()) return c;
            }
        }
        var v = _tree?.Root?.GetCamera3D();
        return v != null && GodotObject.IsInstanceValid(v) && v.IsInsideTree() ? v : null;
    }

    private static void NoGi(GeometryInstance3D g) => g.GIMode = GeometryInstance3D.GIModeEnum.Disabled;

    private static MeshInstance3D MeshNode(Mesh mesh, Material mat)
    {
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        NoGi(mi);
        return mi;
    }

    /// <summary>A glowing ball / ellipsoid (fx_flash on the unit sphere).</summary>
    private static MeshInstance3D Orb(Vector3 scale, Color c, float gain, float power)
    {
        var m = MeshNode(_sphere, _mFlash);
        m.Scale = scale;
        Set(m, PT, 0f); Set(m, PTint, c); Set(m, PGain, gain); Set(m, PPower, power);
        return m;
    }

    /// <summary>A column of light standing on its parent's origin (fx_pillar on the unit cylinder).</summary>
    private static MeshInstance3D Pillar(Color c, float radius, float height, float gain, float power, float topFade, float shimmer)
    {
        var p = MeshNode(_cyl, _mPillar);
        p.Scale = new Vector3(radius, height, radius);
        p.Position = new Vector3(0f, height * 0.5f, 0f);
        Set(p, PT, 0f); Set(p, PTint, c); Set(p, PGain, gain); Set(p, PPower, power); Set(p, PTopFade, topFade); Set(p, PShimmer, shimmer);
        return p;
    }

    /// <summary>
    /// Registers an animation. A level may be assembled away from the main thread (Fire, Portal): such registrations
    /// wait in a small locked list and join the running set on the next frame.
    /// </summary>
    private static Anim NewAnim(Action<Anim, float> step, Node3D node, float dur)
    {
        bool main = System.Environment.CurrentManagedThreadId == _mainThread;
        var a = main && _animPool.Count > 0 ? _animPool.Pop() : new Anim();
        a.Step = step; a.Node = node; a.Dur = Mathf.Max(dur, 0.01f);
        if (main) _anims.Add(a);
        else lock (_pending) _pending.Add(a);
        return a;
    }

    private static bool TryPm(int key, out ParticleProcessMaterial m) { lock (_pm) return _pm.TryGetValue(key, out m); }
    private static ParticleProcessMaterial KeepPm(int key, ParticleProcessMaterial m) { lock (_pm) _pm[key] = m; return m; }

    /// <summary>Expanding ring. With an owner, <paramref name="pos"/> is local to it and the ring travels with it.</summary>
    private static void RingFx(Node3D owner, Vector3 pos, float radius, Color c, float seconds, float gain, float width)
    {
        var q = MeshNode(_quad, _mRing);
        q.Scale = new Vector3(radius, 1f, radius);
        Set(q, PT, 0f); Set(q, PTint, c); Set(q, PGain, gain * 0.75f); Set(q, PWidth, width);
        if (owner != null) { q.Position = pos; owner.AddChild(q); }
        else if (!Spawn(q, pos)) { q.Free(); return; }
        var a = NewAnim(FnT, q, seconds);
        a.G1 = q; a.D = 1f;
    }

    /// <summary>A ball of light going from scale s0 to s1 while it fades. With an owner, <paramref name="pos"/> is local to it.</summary>
    private static void FlashFx(Node3D owner, Vector3 pos, Vector3 shape, float s0, float s1, Color c, float seconds, float gain, float power)
    {
        var s = Orb(shape * s0, c, gain * 0.6f, power);
        if (owner != null) { s.Position = pos; owner.AddChild(s); }
        else if (!Spawn(s, pos)) { s.Free(); return; }
        var a = NewAnim(FnT, s, seconds);
        a.G1 = s; a.A = s0; a.B = s1; a.V0 = shape; a.D = 1f; a.I = 1;
    }

    // ───────────────────────── lights ─────────────────────────

    /// <summary>Brief shadowless light from a pool of twelve; when all are busy the dimmest gives way to a clearly brighter request.</summary>
    private static void FlashLight(Vector3 pos, Color c, float energy, float range, float seconds)
    {
        // A flash is a pulse on the ground around the impact, never a floodlight: against the dark level the old
        // energies (up to 7 for an ultimate) lit hero, floor and walls to white in a close-up.
        energy = Mathf.Min(energy * 0.45f, 3.2f);
        if (energy <= 0.05f) return;
        var parent = Parent();
        if (parent == null) return;
        FlashSlot pick = null;
        float weakest = float.MaxValue;
        for (int i = 0; i < _flash.Length; i++)
        {
            var s = _flash[i] ??= new FlashSlot();
            bool ok = s.L != null && GodotObject.IsInstanceValid(s.L) && s.L.IsInsideTree() && s.L.GetParent() == parent;
            if (!ok)
            {
                if (s.L != null && GodotObject.IsInstanceValid(s.L)) s.L.QueueFree();
                s.L = null; s.Busy = false;
            }
            float k = s.Busy ? 1f - s.T / s.Dur : 0f;
            float cur = s.Busy ? s.Energy * k * k : -1f;
            if (cur < weakest) { weakest = cur; pick = s; }
        }
        if (pick == null || weakest > energy * 0.6f) return;
        if (pick.L == null)
        {
            pick.L = new OmniLight3D { ShadowEnabled = false, OmniAttenuation = 1.3f, LightSpecular = 0.25f };
            parent.AddChild(pick.L);
        }
        var l = pick.L;
        l.GlobalPosition = pos;
        l.LightColor = c; l.LightEnergy = energy; l.OmniRange = Mathf.Max(range, 1f); l.Visible = true;
        pick.T = 0f; pick.Dur = Mathf.Max(seconds, 0.05f); pick.Energy = energy; pick.Busy = true;
    }

    private static void TickLights(float dt)
    {
        for (int i = 0; i < _flash.Length; i++)
        {
            var s = _flash[i];
            if (s == null || !s.Busy) continue;
            if (s.L == null || !GodotObject.IsInstanceValid(s.L)) { s.L = null; s.Busy = false; continue; }
            s.T += dt;
            float k = 1f - s.T / s.Dur;
            if (k <= 0f) { s.Busy = false; s.L.Visible = false; }
            else s.L.LightEnergy = s.Energy * k * k;
        }
    }

    /// <summary>A small persistent shadowless light (projectile heads, zones, auras, grand loot); null when ten already burn.</summary>
    private static OmniLight3D SoftLight(Color c, float energy, float range)
    {
        for (int i = _soft.Count - 1; i >= 0; i--)
            if (!GodotObject.IsInstanceValid(_soft[i]) || _soft[i].IsQueuedForDeletion()) _soft.RemoveAt(i);
        if (_soft.Count >= MaxSoftLights) return null;
        var l = new OmniLight3D { LightColor = c, LightEnergy = energy, OmniRange = range, OmniAttenuation = 1.4f, ShadowEnabled = false, LightSpecular = 0.2f };
        _soft.Add(l);
        return l;
    }

    // ───────────────────────── emitters ─────────────────────────

    /// <summary>Frees room for one more one-shot emitter of the given value; false when everything alive is worth more.</summary>
    private static bool MakeRoom(int value)
    {
        if (_shots.Count < MaxOneShots) return true;
        int worst = -1, worstVal = int.MaxValue;
        float worstLeft = float.MaxValue;
        for (int i = 0; i < _shots.Count; i++)
        {
            var s = _shots[i];
            if (s.Value < worstVal || (s.Value == worstVal && s.Left < worstLeft)) { worst = i; worstVal = s.Value; worstLeft = s.Left; }
        }
        if (worst < 0 || worstVal > value) return false;
        var p = _shots[worst].P;
        if (p != null && GodotObject.IsInstanceValid(p)) p.QueueFree();
        _shots[worst] = _shots[^1];
        _shots.RemoveAt(_shots.Count - 1);
        return true;
    }

    /// <summary>
    /// One burst of particles that frees itself. With an owner, <paramref name="pos"/> is local to it and the emitter
    /// rides along (particles stay in world space). value: 0 decorative … 3 never worth dropping.
    /// </summary>
    private static GpuParticles3D OneShot(Node3D owner, Vector3 pos, ParticleProcessMaterial pm, Look look, int amount, float life, float explosive, int value, float reach)
    {
        if (pm == null || !MakeRoom(value)) return null;
        var p = new GpuParticles3D
        {
            Emitting = false, OneShot = true, ProcessMaterial = pm, DrawPass1 = _draw[(int)look],
            Amount = Math.Max(2, (int)(amount * Density)), Lifetime = life, Explosiveness = explosive, LocalCoords = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(-reach, -2f, -reach, reach * 2f, reach + 4f, reach * 2f),
        };
        if (look == Look.Streak) p.TransformAlign = GpuParticles3D.TransformAlignEnum.ZBillboardYToVelocity;
        NoGi(p);
        if (owner != null) { p.Position = pos; owner.AddChild(p); }
        else if (!Spawn(p, pos)) { p.Free(); return null; }
        if (_wasPaused) p.SpeedScale = 0f;
        p.Emitting = true;
        _shots.Add(new ShotRec { P = p, Left = life * (2f - explosive) + 0.2f, Value = value });
        return p;
    }

    /// <summary>A continuous emitter (world-space particles); starts when its owner enters the tree.</summary>
    private static GpuParticles3D Loop(ParticleProcessMaterial pm, Look look, int amount, float life, float reach)
    {
        var p = new GpuParticles3D
        {
            ProcessMaterial = pm, DrawPass1 = _draw[(int)look], Amount = Math.Max(1, (int)(amount * Density)), Lifetime = life,
            LocalCoords = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(-reach, -3f, -reach, reach * 2f, reach + 6f, reach * 2f),
        };
        if (look == Look.Streak) p.TransformAlign = GpuParticles3D.TransformAlignEnum.ZBillboardYToVelocity;
        NoGi(p);
        return p;
    }

    private static GpuParticles3D TrailEmitter(Element el, bool big, float scale)
    {
        var pm = TrailPm(el, big, out var look, out float life, out int amount);
        var p = Loop(pm, look, amount, life, 22f);
        if (scale != 1f) p.Scale = Vector3.One * scale;
        return p;
    }

    /// <summary>Leaves a trail emitter behind in the world: it stops emitting and is freed once its last particle has faded.</summary>
    private static void ReleaseTrail(GpuParticles3D p)
    {
        if (p == null || !GodotObject.IsInstanceValid(p) || !p.IsInsideTree()) return;
        var parent = Parent();
        if (parent == null) return;
        p.Emitting = false;
        if (p.GetParent() != parent) p.Reparent(parent, true);
        _shots.Add(new ShotRec { P = p, Left = (float)p.Lifetime + 0.15f, Value = 0 });
    }

    private static void TickShots(float dt)
    {
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            var s = _shots[i];
            bool gone = s.P == null || !GodotObject.IsInstanceValid(s.P);
            if (!gone)
            {
                s.Left -= dt;
                if (s.Left <= 0f) { s.P.QueueFree(); gone = true; }
            }
            if (gone) { _shots[i] = _shots[^1]; _shots.RemoveAt(_shots.Count - 1); }
            else _shots[i] = s;
        }
    }

    /// <summary>Freezes / resumes the one-shot emitters with the game, so a paused explosion is still there on resume.</summary>
    private static void HoldShots(bool hold)
    {
        for (int i = 0; i < _shots.Count; i++)
        {
            var p = _shots[i].P;
            if (p != null && GodotObject.IsInstanceValid(p)) p.SpeedScale = hold ? 0f : 1f;
        }
    }

    // ───────────────────────── composite pieces ─────────────────────────

    private static void BurstParticles(Vector3 at, Element el, float radius, float intensity)
    {
        int rb = Mathf.Clamp(Mathf.RoundToInt(radius), 1, 12);
        float k = 0.6f + 0.4f * intensity;
        var a = BurstPmA(el, rb, out var lookA, out float lifeA, out int nA);
        OneShot(null, at, a, lookA, (int)(nA * k), lifeA, 0.95f, 2, radius + 4f);
        var b = BurstPmB(el, rb, out var lookB, out float lifeB, out int nB);
        if (b != null) OneShot(null, at, b, lookB, (int)(nB * k), lifeB, 0.85f, 0, radius + 4f);
        if (el == Element.Fire) OneShot(null, at, BurstPmC(rb), Look.Dot, (int)((8 + rb * 2) * k), 1f, 0.8f, 1, radius + 4f);
    }

    /// <summary>The mesh strip of a beam, relative to its start. jagged = lightning: random kinks, rebuilt a few times while it lives.</summary>
    private static void BuildStrip(ImmediateMesh im, Vector3 delta, Vector3 side, float width, bool jagged)
    {
        im.ClearSurfaces();
        float len = delta.Length();
        if (len < 0.02f) return;
        Vector3 dir = delta / len;
        Vector3 depth = dir.Cross(side);
        int segs = jagged ? Mathf.Clamp((int)(len / 0.6f) + 3, 5, 20) : 2;
        float amp = jagged ? Mathf.Min(0.75f, 0.12f + len * 0.06f) : 0f;
        float hw = width * 0.5f;
        im.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (int i = 0; i <= segs; i++)
        {
            float k = i / (float)segs;
            Vector3 p = delta * k;
            if (jagged && i > 0 && i < segs)
            {
                float env = Mathf.Sin(k * Mathf.Pi) * 0.7f + 0.3f;
                p += side * (Rng.Range(-1f, 1f) * amp * env) + depth * (Rng.Range(-1f, 1f) * amp * 0.4f * env);
            }
            im.SurfaceSetUV(new Vector2(0f, k));
            im.SurfaceAddVertex(p - side * hw);
            im.SurfaceSetUV(new Vector2(1f, k));
            im.SurfaceAddVertex(p + side * hw);
        }
        im.SurfaceEnd();
    }

    /// <summary>The axis across a beam that makes its strip face the camera.</summary>
    private static Vector3 SideFor(Vector3 from, Vector3 delta)
    {
        Vector3 view = Vector3.Up;
        var cam = Cam();
        if (cam != null) view = cam.GlobalPosition - (from + delta * 0.5f);
        Vector3 side = delta.Cross(view);
        if (side.LengthSquared() < 1e-6f) side = delta.Cross(Vector3.Right);
        if (side.LengthSquared() < 1e-6f) return Vector3.Forward;
        return side.Normalized();
    }

    private static void BeamCore(Vector3 from, Vector3 to, Color c, float width, float seconds, bool jagged, float gain)
    {
        Vector3 delta = to - from;
        if (delta.LengthSquared() < 0.0004f) return;
        Vector3 side = SideFor(from, delta);
        var im = new ImmediateMesh();
        BuildStrip(im, delta, side, width, jagged);
        var mi = MeshNode(im, _mBeam);
        Set(mi, PT, 0f); Set(mi, PTint, c); Set(mi, PGain, gain); Set(mi, PFlow, jagged ? 0f : 1f);
        if (!Spawn(mi, from)) { mi.Free(); return; }
        var a = NewAnim(FnBeam, mi, seconds);
        a.G1 = mi; a.V0 = delta; a.V1 = side; a.A = width; a.I = jagged ? 1 : 0; a.C = 0.045f;
    }

    private static Node3D Tele(Vector3 pos, float yaw, Vector2 half, float mode, float arc, float inner, float delay, Color c, bool friendly)
    {
        var q = MeshNode(_quad, _mTele);
        q.Scale = new Vector3(half.X, 1f, half.Y);
        Color fill = friendly ? c : Palette.Danger.Lerp(c, 0.2f);
        Color edge = friendly ? c.Lerp(Colors.White, 0.25f) : Palette.Danger;
        Set(q, PT, 0f); Set(q, PTint, fill); Set(q, PEdge, edge); Set(q, PMode, mode); Set(q, PArc, arc); Set(q, PInner, inner);
        Set(q, PSoft, friendly ? 1f : 0f); Set(q, PFade, 0f); Set(q, PHalf, half);
        if (!friendly) q.SortingOffset = 1f;         // warnings are drawn over everything else on the ground
        if (!Spawn(q, pos, yaw)) return q;
        var a = NewAnim(FnTele, q, Mathf.Max(delay, 0.05f));
        a.G1 = q;
        return q;
    }

    /// <summary>Lightning does not fall as a body: a thin line of light gathers over the target, then the bolt strikes.</summary>
    private static void SkyBolt(Vector3 ground, float delay, float radius, Color c)
    {
        const float height = 15f;
        var pil = Pillar(c, 0.1f, height, 1.2f, 1.5f, 0.35f, 0.6f);
        Set(pil, PT, 0.8f);
        if (!Spawn(pil, new Vector3(ground.X, height * 0.5f, ground.Z))) { pil.Free(); return; }
        var a = NewAnim(FnSkyBolt, pil, delay);
        a.G1 = pil; a.V0 = ground; a.A = radius;
    }

    private static void BuildOrb(Node3D root, Element el, Color c, int size, float radius, bool hostile)
    {
        bool big = size >= 1;
        float s = big ? 0.34f : 0.2f;
        if (hostile) s = Mathf.Max(s * 1.1f, Mathf.Min(radius, 1.6f) * 0.5f);
        Vector3 shape = el switch
        {
            Element.Frost => new Vector3(0.5f, 0.5f, 2.6f),          // a lance of ice
            Element.Lightning => new Vector3(0.7f, 0.7f, 1.9f),
            Element.Poison => new Vector3(1f, 0.9f, 1.15f),
            _ => new Vector3(1f, 1f, 1.3f),
        };
        if (hostile) root.AddChild(Orb(Vector3.One * (s * 2.3f), Palette.Danger, 0.85f, 3.6f));   // hostile shots wear a red halo
        root.AddChild(Orb(shape * (s * 1.5f), c, 1.5f, 2.6f));
        var core = Orb(shape * (s * 0.6f), Hot(c, 0.8f), 2.2f, 1.3f);
        if (el == Element.Lightning || el == Element.Shadow) Set(core, PPulse, 1f);
        root.AddChild(core);

        var trail = TrailEmitter(el, big, hostile ? Mathf.Clamp(s / 0.22f, 1f, 2.2f) : 1f);
        trail.Position = new Vector3(0f, 0f, -s * 0.6f);
        root.AddChild(trail);
        if (big && el == Element.Fire)
        {
            var sparks = Loop(TrailSparksPm(), Look.Streak, 10, 0.4f, 22f);
            sparks.Position = new Vector3(0f, 0f, -s * 0.4f);
            root.AddChild(sparks);
        }
        if (big)
        {
            var l = SoftLight(LightTint(c), 1.5f, 5.5f);
            if (l != null) root.AddChild(l);
        }
    }

    /// <summary>Crossbow bolts, javelins, bone spears: a thin solid shaft with a faint streak behind it.</summary>
    private static void BuildShaft(Node3D root, Element el, Color c, int size, float radius, string id, bool hostile)
    {
        bool spear = id.Contains("sb_spear"), bolt = id.Contains("hu_");
        float len = spear ? 1.7f : bolt ? (size >= 1 ? 1.2f : 0.9f) : (size >= 1 ? 1.6f : 1.25f);
        float thick = spear ? 0.055f : bolt ? 0.028f : 0.04f;
        if (hostile && radius > 0.7f) { len *= 1.3f; thick *= 1.5f; }

        var shaft = MeshNode(_shaft, hostile ? _mShaftHostile : _mShaft);
        shaft.Scale = new Vector3(thick, len, thick);
        shaft.RotationDegrees = new Vector3(90f, 0f, 0f);            // the cylinder's +Y (its point) becomes +Z, the way it flies
        root.AddChild(shaft);

        Color streak = hostile ? Palette.Danger.Lerp(c, 0.5f) : el == Element.Physical ? new Color(0.9f, 0.86f, 0.76f) : c;
        var tail = Orb(new Vector3(thick * 2.6f, thick * 2.6f, len * 0.9f), streak, hostile ? 0.9f : 0.6f, 2.2f);
        tail.Position = new Vector3(0f, 0f, -len * 0.55f);
        root.AddChild(tail);
        var tip = Orb(Vector3.One * (thick * 2.4f), Hot(streak, 0.6f), 1.4f, 1.5f);
        tip.Position = new Vector3(0f, 0f, len * 0.5f);
        root.AddChild(tip);

        var trail = TrailEmitter(el, false, 1f);
        trail.Position = new Vector3(0f, 0f, -len * 0.4f);
        root.AddChild(trail);
        if (size >= 1 && !bolt)
        {
            var l = SoftLight(LightTint(streak), 0.9f, 4f);
            if (l != null) root.AddChild(l);
        }
    }

    /// <summary>A wave front hugging the ground, as wide as <paramref name="radius"/>, shedding particles along its path.</summary>
    private static void BuildWave(Node3D root, Element el, Color c, float radius, bool hostile)
    {
        float half = Mathf.Max(radius * 0.5f, 0.6f);
        float depth = Mathf.Max(radius * 0.6f, 1f);
        Color tint = el == Element.Physical ? new Color(0.85f, 0.72f, 0.5f) : c;
        if (hostile) tint = Palette.Danger.Lerp(tint, 0.45f);

        var q = MeshNode(_quad, _mWave);
        q.Scale = new Vector3(half * 1.25f, 1f, depth);
        Set(q, PTint, tint); Set(q, PGain, 1.5f); Set(q, PFade, 1f); Set(q, PSeed, Rng.F());
        root.AddChild(q);

        var pm = WavePm(el, half, out var look, out float life, out int amount);
        var parts = Loop(pm, look, amount, life, 24f);
        parts.Position = new Vector3(0f, 0f, depth * 0.45f);
        root.AddChild(parts);

        // the shot itself may fly at any height: settle the wave on the ground once it is in the world
        root.Ready += () =>
        {
            try
            {
                if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;
                float lift = root.GlobalPosition.Y;
                if (GodotObject.IsInstanceValid(q)) q.Position = new Vector3(0f, 0.09f - lift, 0f);
                if (GodotObject.IsInstanceValid(parts)) parts.Position = new Vector3(0f, 0.25f - lift, depth * 0.45f);
            }
            catch (Exception e) { Fail("wave placement", e); }
        };
    }

    private static Vector3 HandPos(Actor a)
    {
        CharModel m = a is Player p ? p.ActiveModel : a.Model;
        if (m != null && GodotObject.IsInstanceValid(m) && m.IsInsideTree()) return m.BonePos("DEF-hand.R");
        return a.Chest;
    }

    // ═════════════════════════ animation ═════════════════════════

    private static void OnFrame()
    {
        if (!_ready) return;
        try
        {
            var root = _tree?.Root;
            if (root == null) return;
            float dt = (float)root.GetProcessDeltaTime();
            if (dt <= 0f) return;
            if (dt > 0.1f) dt = 0.1f;
            _clock += dt;
            if (_clock > 1024f) _clock -= 1024f;
            _frame++;
            _mainThread = System.Environment.CurrentManagedThreadId;
            if (_pending.Count > 0)
            {
                lock (_pending) { _anims.AddRange(_pending); _pending.Clear(); }
            }
            // Effects stand still with the fight (a pausing panel is open during play). Outside play (title screen,
            // loading) there is no fight to stay in step with, so they run freely.
            var g = Game.I;
            bool paused = g != null && GodotObject.IsInstanceValid(g) && g.State == Game.GameState.Play && g.PauseLocks > 0;
            if (paused != _wasPaused) { _wasPaused = paused; HoldShots(paused); }
            TickAnims(dt, paused);
            if (paused) return;
            TickLights(dt);
            TickShots(dt);
        }
        catch (Exception e) { Fail("frame update", e); }
    }

    private static void TickAnims(float dt, bool paused)
    {
        for (int i = _anims.Count - 1; i >= 0; i--)
        {
            var a = _anims[i];
            var n = a.Node;
            bool alive = !a.Done && n != null && GodotObject.IsInstanceValid(n) && !n.IsQueuedForDeletion();
            if (alive)
            {
                if (!n.IsInsideTree())
                {
                    // handed to a caller that has not parented it yet: wait (never free what someone else may hold)
                    a.Orphan += dt;
                    if (a.Orphan > 90f)
                    {
                        if (a.Step == FnGrow) n.Scale = a.V0;      // never leave a forgotten portal collapsed
                        alive = false;
                    }
                }
                else if (!paused || a.Always)
                {
                    a.Orphan = 0f;
                    a.T += dt;
                    try { a.Step(a, dt); }
                    catch (Exception e)
                    {
                        Fail("effect step", e);
                        a.Done = true;
                        if (GodotObject.IsInstanceValid(n)) n.QueueFree();
                    }
                    alive = !a.Done;
                }
            }
            if (alive) continue;
            int last = _anims.Count - 1;
            _anims[i] = _anims[last];
            _anims.RemoveAt(last);
            a.Clear();
            if (_animPool.Count < 256) _animPool.Push(a);
        }
    }

    /// <summary>
    /// Generic one-shot: instance uniform "t" runs C → D over Dur, then the node is freed.
    /// B &gt; 0: scale runs V0·A → V0·B (I = 1 eases out). Actor set: the node follows it at offset V1.
    /// </summary>
    private static void StepT(Anim a, float dt)
    {
        float k = Mathf.Clamp(a.T / a.Dur, 0f, 1f);
        a.G1.SetInstanceShaderParameter(PT, Mathf.Lerp(a.C, a.D, k));
        if (a.B > 0f)
        {
            float e = a.I == 1 ? 1f - (1f - k) * (1f - k) : k;
            a.Node.Scale = a.V0 * Mathf.Lerp(a.A, a.B, e);
        }
        if (a.Actor != null && GodotObject.IsInstanceValid(a.Actor) && a.Actor.IsInsideTree()) a.Node.GlobalPosition = a.Actor.GlobalPosition + a.V1;
        if (k >= 1f) { a.Node.QueueFree(); a.Done = true; }
    }

    /// <summary>Ground warning: fades in fast, fills over Dur, flashes out in 0.14 s.</summary>
    private static void StepTelegraph(Anim a, float dt)
    {
        const float tailOut = 0.14f;
        float fade = Mathf.Min(1f, a.T / 0.1f);
        if (a.T > a.Dur) fade = Mathf.Max(0f, 1f - (a.T - a.Dur) / tailOut);
        a.G1.SetInstanceShaderParameter(PT, Mathf.Min(a.T / a.Dur, 1f));
        a.G1.SetInstanceShaderParameter(PFade, fade);
        if (a.T >= a.Dur + tailOut) { a.Node.QueueFree(); a.Done = true; }
    }

    /// <summary>
    /// Ground area. A light energy, B radius, C storm timer, D last fade written, I 1 once emission has stopped,
    /// V0.X particle lifetime. Fades in over 0.25 s and out over the last 0.45 s of Dur.
    /// </summary>
    private static void StepZone(Anim a, float dt)
    {
        float f = Mathf.Min(1f, a.T / 0.25f) * Mathf.Clamp((a.Dur - a.T) / 0.45f, 0f, 1f);
        if (Mathf.Abs(f - a.D) > 0.004f || (f <= 0f && a.D > 0f))
        {
            a.D = f;
            a.G1.SetInstanceShaderParameter(PFade, f);
            if (a.G2 != null) a.G2.SetInstanceShaderParameter(PFade, f);
        }
        if (a.Light != null)
        {
            // fire light never holds still
            float flick = a.El == Element.Fire ? 1f + 0.14f * Mathf.Sin(a.T * 11f + a.V0.Y) + 0.09f * Mathf.Sin(a.T * 17.3f + a.V0.Y * 1.7f) : 1f;
            a.Light.LightEnergy = a.A * f * flick;
        }
        if (a.I == 0 && a.T >= Mathf.Max(a.Dur - a.V0.X * 0.8f, a.Dur * 0.5f))
        {
            // let what is in the air burn out before the floor fades
            a.I = 1;
            int n = a.Node.GetChildCount();
            for (int i = 0; i < n; i++)
                if (a.Node.GetChild(i) is GpuParticles3D gp) gp.Emitting = false;
        }
        if (a.El == Element.Lightning && a.I == 0)
        {
            // a storm: thin bolts keep finding the ground inside the circle
            a.C -= dt;
            if (a.C <= 0f)
            {
                a.C = Rng.Range(0.35f, 0.7f);
                float ang = Rng.Range(0f, Mathf.Tau), d = a.B * Mathf.Sqrt(Rng.F()) * 0.9f;
                Vector3 at = a.Node.GlobalPosition + new Vector3(Mathf.Cos(ang) * d, 0.1f, Mathf.Sin(ang) * d);
                if (!Far(at))
                {
                    BeamCore(at + new Vector3(Rng.Range(-1f, 1f), 9f, Rng.Range(-1f, 1f)), at, Palette.Element(Element.Lightning), 0.3f, 0.16f, true, 1.5f);
                    FlashFx(null, at, Vector3.One, 0.2f, 0.45f, Colors.White, 0.1f, 1.4f, 1.6f);
                }
            }
        }
        if (a.T >= a.Dur + 1.6f) { a.Node.QueueFree(); a.Done = true; }
    }

    /// <summary>Falling body: V0 start, V1 landing point, A radius, P1 trail. Gathers speed and lands exactly at Dur.</summary>
    private static void StepMeteor(Anim a, float dt)
    {
        float k = Mathf.Clamp(a.T / a.Dur, 0f, 1f);
        float e = k * (0.55f + 0.45f * k);
        a.Node.GlobalPosition = a.V0.Lerp(a.V1, e);
        if (k < 1f) return;
        ReleaseTrail(a.P1);
        Color c = Palette.Element(a.El);
        FlashFx(null, a.V1, Vector3.One, a.A * 0.22f, a.A * 0.5f, Hot(c, 0.45f), 0.16f, 1.8f, 2.2f);
        FlashLight(a.V1 + Vector3.Up * 0.8f, LightTint(c), 3.5f, a.A * 2f + 4f, 0.3f);
        if (a.El == Element.Physical) OneShot(null, a.V1, DustPm(), Look.Smoke, 8, 0.55f, 0.9f, 0, 4f);
        a.Node.QueueFree();
        a.Done = true;
    }

    /// <summary>
    /// Emitters that run for Dur on an owner (auras, dash trails), then stop and are freed after C more seconds.
    /// G1 (ground ring) and Light fade in and out with it; A light energy; B = 1 marks a buff aura; ends early if the Actor dies.
    /// </summary>
    private static void StepAura(Anim a, float dt)
    {
        if (a.Actor != null && a.T < a.Dur && (!GodotObject.IsInstanceValid(a.Actor) || a.Actor.Dead)) a.Dur = a.T;
        if (a.G1 != null || a.Light != null)
        {
            float f = Mathf.Min(1f, a.T / 0.25f) * Mathf.Clamp((a.Dur - a.T) / 0.4f, 0f, 1f);
            if (Mathf.Abs(f - a.D) > 0.004f || (f <= 0f && a.D > 0f))
            {
                a.D = f;
                if (a.G1 != null) a.G1.SetInstanceShaderParameter(PFade, f);
                if (a.Light != null) a.Light.LightEnergy = a.A * f;
            }
        }
        if (a.I == 0 && a.T >= a.Dur)
        {
            a.I = 1;
            if (a.P1 != null) a.P1.Emitting = false;
            if (a.P2 != null) a.P2.Emitting = false;
        }
        if (a.T >= a.Dur + a.C) { a.Node.QueueFree(); a.Done = true; }
    }

    /// <summary>Level-up pillar: A full radius, B height. Snaps open in 0.12 s, holds, then narrows and fades.</summary>
    private static void StepPillar(Anim a, float dt)
    {
        float k = Mathf.Clamp(a.T / a.Dur, 0f, 1f);
        float gather = Mathf.Min(1f, a.T / 0.12f);
        float r = a.A * Mathf.Lerp(0.15f, 1f, gather) * Mathf.Lerp(1f, 0.45f, k);
        a.Node.Scale = new Vector3(r, a.B, r);
        float t = a.T < 0.12f ? Mathf.Lerp(0.6f, 0f, gather) : Mathf.Clamp((k - 0.35f) / 0.65f, 0f, 1f);
        a.G1.SetInstanceShaderParameter(PT, t * t);
        if (k >= 1f) { a.Node.QueueFree(); a.Done = true; }
    }

    /// <summary>Beam: V0 span, V1 side axis, A width, I 1 = lightning (re-kinked every 45 ms), C next re-kink time.</summary>
    private static void StepBeam(Anim a, float dt)
    {
        float k = a.T / a.Dur;
        a.G1.SetInstanceShaderParameter(PT, Mathf.Min(k, 1f));
        if (k >= 1f) { a.Node.QueueFree(); a.Done = true; return; }
        if (a.I == 1 && a.T >= a.C && k < 0.8f)
        {
            a.C = a.T + 0.045f;
            if (a.Node is MeshInstance3D mi && mi.Mesh is ImmediateMesh im) BuildStrip(im, a.V0, a.V1, a.A, true);
        }
    }

    /// <summary>Fire light: A base energy, B phase, I frame parity (each light is updated every other frame).</summary>
    private static void StepFlicker(Anim a, float dt)
    {
        if (((_frame + a.I) & 1) != 0) return;
        var l = a.Light;
        if (l == null || !GodotObject.IsInstanceValid(l)) { a.Done = true; return; }
        float t = _clock;
        float n = 0.5f * MathF.Sin(t * 11.3f + a.B) + 0.3f * MathF.Sin(t * 23.7f + a.B * 1.7f) + 0.2f * MathF.Sin(t * 5.1f + a.B * 0.6f);
        l.LightEnergy = a.A * (0.88f + 0.12f * n);
    }

    /// <summary>Afterimage: V0 shape. Appears, grows clearer as its moment nears, then bursts outward and is gone.</summary>
    private static void StepGhost(Anim a, float dt)
    {
        const float pop = 0.12f;
        if (a.T < a.Dur)
        {
            float k = a.T / a.Dur;
            float t = Mathf.Max(1f - a.T / 0.1f, Mathf.Lerp(0.7f, 0.3f, k));
            a.G1.SetInstanceShaderParameter(PT, t);
            return;
        }
        float o = Mathf.Clamp((a.T - a.Dur) / pop, 0f, 1f);
        a.G1.SetInstanceShaderParameter(PT, Mathf.Lerp(0.1f, 1f, o));
        a.Node.Scale = a.V0 * (1f + 0.5f * o);
        if (o >= 1f) { a.Node.QueueFree(); a.Done = true; }
    }

    /// <summary>Sky bolt: the gathering line brightens over Dur, then the bolt strikes V0 (A = radius).</summary>
    private static void StepSkyBolt(Anim a, float dt)
    {
        float k = Mathf.Clamp(a.T / a.Dur, 0f, 1f);
        a.G1.SetInstanceShaderParameter(PT, Mathf.Lerp(0.8f, 0.3f, k * k));
        if (k < 1f) return;
        Color c = Palette.Element(Element.Lightning);
        Vector3 top = a.V0 + new Vector3(Rng.Range(-1.5f, 1.5f), 15f, Rng.Range(-1.5f, 1.5f));
        BeamCore(top, a.V0, c, 0.5f + a.A * 0.06f, 0.24f, true, 2.2f);
        BeamCore(top, a.V0 + new Vector3(Rng.Range(-0.6f, 0.6f), 0f, Rng.Range(-0.6f, 0.6f)), c, 0.25f, 0.18f, true, 1.4f);
        FlashFx(null, a.V0, Vector3.One, a.A * 0.25f, a.A * 0.6f, Hot(c), 0.14f, 2.4f, 1.5f);
        FlashLight(a.V0 + Vector3.Up, LightTint(c), 4f, a.A * 2f + 5f, 0.25f);
        a.Node.QueueFree();
        a.Done = true;
    }

    /// <summary>Portal opening: scale swells to V0 with a slight overshoot; the node stays.</summary>
    private static void StepGrow(Anim a, float dt)
    {
        float k = Mathf.Clamp(a.T / a.Dur, 0f, 1f);
        float u = k - 1f;
        float s = 1f + 2.2f * u * u * u + 1.2f * u * u;
        a.Node.Scale = a.V0 * Mathf.Max(s, 0.05f);
        if (k >= 1f) a.Done = true;
    }

    // ═════════════════════════ particle presets ═════════════════════════

    private static int PmKey(int preset, int a, int b) => preset * 1_000_000 + a * 1000 + b;

    private static ParticleProcessMaterial Base(Vector3 dir, float spread, float v0, float v1, Vector3 gravity, float s0, float s1, Texture2D ramp, Texture2D scaleCurve)
    {
        var m = new ParticleProcessMaterial
        {
            Direction = dir, Spread = spread, InitialVelocityMin = v0, InitialVelocityMax = v1, Gravity = gravity,
            ScaleMin = s0, ScaleMax = s1, LifetimeRandomness = 0.3f,
        };
        if (ramp != null) m.ColorRamp = ramp;
        if (scaleCurve != null) m.ScaleCurve = scaleCurve;
        return m;
    }

    private static void Sphere(ParticleProcessMaterial m, float r)
    {
        m.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        m.EmissionSphereRadius = r;
    }

    private static void Box(ParticleProcessMaterial m, Vector3 extents)
    {
        m.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        m.EmissionBoxExtents = extents;
    }

    /// <summary>A flat ring (inner = 0: a disc) lying on the ground.</summary>
    private static void RingShape(ParticleProcessMaterial m, float radius, float inner, float height)
    {
        m.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
        m.EmissionRingAxis = Vector3.Up;
        m.EmissionRingRadius = radius;
        m.EmissionRingInnerRadius = inner;
        m.EmissionRingHeight = height;
    }

    private static void Spin(ParticleProcessMaterial m, float degPerSec)
    {
        m.AngleMin = 0f; m.AngleMax = 360f;
        m.AngularVelocityMin = -degPerSec; m.AngularVelocityMax = degPerSec;
    }

    private static void Damp(ParticleProcessMaterial m, float a, float b) { m.DampingMin = a; m.DampingMax = b; }

    private static void Turb(ParticleProcessMaterial m, float strength, float scale)
    {
        m.TurbulenceEnabled = true;
        m.TurbulenceNoiseStrength = strength;
        m.TurbulenceNoiseScale = scale;
        m.TurbulenceInfluenceMin = 0.05f;
        m.TurbulenceInfluenceMax = 0.18f;
    }

    private static readonly Color Bright = new(1.5f, 1.5f, 1.5f, 1f);
    private static readonly Vector3 Back = new(0f, 0f, -1f);
    /// <summary>
    /// Direction for presets that spread along the ground: the spread fans out around the direction in the plane it
    /// shares with the vertical, and Flatness squeezes the other angle, so a horizontal direction with Spread 180
    /// and high Flatness gives a flat disc of directions (a vertical direction would give an upright fan instead).
    /// </summary>
    private static readonly Vector3 Flat = new(0f, 0f, 1f);

    /// <summary>Sparks of a single hit. Physical: pale sparks; fire: sparks that fall; frost: shards; lightning: fast sparks; poison: droplets; shadow: violet motes.</summary>
    private static ParticleProcessMaterial HitPm(Element el, bool crit, out Look look, out float life)
    {
        switch (el)
        {
            case Element.Fire: look = Look.Streak; life = 0.3f; break;
            case Element.Frost: look = Look.Shard; life = 0.35f; break;
            case Element.Lightning: look = Look.Streak; life = 0.16f; break;
            case Element.Poison: look = Look.Dot; life = 0.4f; break;
            case Element.Shadow: look = Look.Dot; life = 0.45f; break;
            default: look = Look.Streak; life = 0.22f; break;
        }
        int key = PmKey(PHit, (int)el, crit ? 1 : 0);
        if (TryPm(key, out var m)) return m;
        float k = crit ? 1.35f : 1f;
        switch (el)
        {
            case Element.Fire:
                m = Base(Vector3.Up, 110f, 3.5f * k, 8f * k, new Vector3(0f, -11f, 0f), 0.2f * k, 0.42f * k, Ramp(el), _cShrink);
                Sphere(m, 0.12f); Damp(m, 3f, 6f); break;
            case Element.Frost:
                m = Base(Vector3.Up, 120f, 2.5f * k, 6f * k, new Vector3(0f, -8f, 0f), 0.1f * k, 0.24f * k, Ramp(el), _cShrink);
                Sphere(m, 0.15f); Spin(m, 420f); break;
            case Element.Lightning:
                m = Base(Vector3.Up, 180f, 7f * k, 14f * k, Vector3.Zero, 0.22f * k, 0.5f * k, Ramp(el), _cShrink);
                Sphere(m, 0.1f); Damp(m, 28f, 40f); break;
            case Element.Poison:
                m = Base(Vector3.Up, 95f, 2f * k, 5f * k, new Vector3(0f, -10f, 0f), 0.07f * k, 0.16f * k, Ramp(el), _cShrink);
                Sphere(m, 0.15f); break;
            case Element.Shadow:
                m = Base(Vector3.Up, 180f, 1.5f * k, 4f * k, new Vector3(0f, 0.8f, 0f), 0.08f * k, 0.2f * k, Ramp(el), _cShrink);
                Sphere(m, 0.2f); Damp(m, 5f, 8f); break;
            default:
                m = Base(Vector3.Up, 120f, 4f * k, 9f * k, new Vector3(0f, -13f, 0f), 0.16f * k, 0.36f * k, Ramp(el), _cShrink);
                Sphere(m, 0.1f); Damp(m, 4f, 8f); break;
        }
        m.Color = Bright;
        return KeepPm(key, m);
    }

    /// <summary>Body of an explosion: what is thrown outward. rb = radius in metres.</summary>
    private static ParticleProcessMaterial BurstPmA(Element el, int rb, out Look look, out float life, out int amount)
    {
        switch (el)
        {
            case Element.Fire: look = Look.Streak; life = 0.55f; amount = 10 + rb * 3; break;
            case Element.Frost: look = Look.Shard; life = 0.6f; amount = 10 + rb * 3; break;
            case Element.Lightning: look = Look.Streak; life = 0.28f; amount = 12 + rb * 3; break;
            case Element.Poison: look = Look.Dot; life = 0.7f; amount = 12 + rb * 3; break;
            case Element.Shadow: look = Look.Dot; life = 0.8f; amount = 10 + rb * 2; break;
            default: look = Look.Chip; life = 0.7f; amount = 8 + rb * 2; break;
        }
        amount = Math.Min(amount, 44);
        int key = PmKey(PBurstA, (int)el, rb);
        if (TryPm(key, out var m)) return m;
        float R = rb;
        switch (el)
        {
            case Element.Fire:          // sparks thrown out, falling back
                m = Base(Vector3.Up, 80f, R * 1.5f, R * 2.6f, new Vector3(0f, -9f, 0f), 0.24f, 0.5f, Ramp(el), _cShrink);
                Sphere(m, R * 0.2f); Damp(m, R * 2f, R * 3f); m.Color = Bright; break;
            case Element.Frost:         // tumbling shards of ice
                m = Base(Vector3.Up, 85f, R * 1.3f, R * 2.3f, new Vector3(0f, -7f, 0f), 0.14f, 0.32f, Ramp(el), _cShrink);
                Sphere(m, R * 0.25f); Damp(m, R * 1.5f, R * 2.5f); Spin(m, 360f); m.Color = Bright; break;
            case Element.Lightning:     // fast sparks skittering outward over the upper half-sphere
                m = Base(Vector3.Up, 88f, R * 3f, R * 5.5f, Vector3.Zero, 0.3f, 0.7f, Ramp(el), _cShrink);
                Sphere(m, R * 0.15f); Damp(m, R * 9f, R * 13f); m.Color = Bright; break;
            case Element.Poison:        // droplets lobbed up and out
                m = Base(Vector3.Up, 70f, R * 1.2f, R * 2.1f, new Vector3(0f, -11f, 0f), 0.08f, 0.19f, Ramp(el), _cShrink);
                Sphere(m, R * 0.25f); m.Color = Bright; break;
            case Element.Shadow:        // violet motes flung out, then drawn back toward the dark
                m = Base(Vector3.Up, 90f, R * 1.2f, R * 2f, new Vector3(0f, 0.6f, 0f), 0.08f, 0.2f, Ramp(el), _cShrink);
                Sphere(m, R * 0.3f); m.RadialAccelMin = -R * 3.5f; m.RadialAccelMax = -R * 2f; Damp(m, 1f, 2f); m.Color = Bright; break;
            default:                    // debris
                m = Base(Vector3.Up, 70f, R * 1.2f, R * 2.2f, new Vector3(0f, -14f, 0f), 0.08f, 0.2f, _rChip, _cShrinkLate);
                Sphere(m, R * 0.3f); Spin(m, 540f); break;
        }
        return KeepPm(key, m);
    }

    /// <summary>Linger of an explosion: smoke, mist, haze or dust at low opacity. Null for lightning (its linger is arcs).</summary>
    private static ParticleProcessMaterial BurstPmB(Element el, int rb, out Look look, out float life, out int amount)
    {
        switch (el)
        {
            case Element.Fire: look = Look.Smoke; life = 0.95f; amount = 5 + rb / 2; break;
            case Element.Frost: look = Look.Puff; life = 0.9f; amount = 6 + rb; break;
            case Element.Lightning: look = Look.Dot; life = 0f; amount = 0; return null;
            case Element.Poison: look = Look.Puff; life = 1f; amount = 6 + rb; break;
            case Element.Shadow: look = Look.Smoke; life = 0.9f; amount = 8 + rb; break;
            default: look = Look.Smoke; life = 0.8f; amount = 8 + rb; break;
        }
        amount = Math.Min(amount, 18);
        int key = PmKey(PBurstB, (int)el, rb);
        if (TryPm(key, out var m)) return m;
        float R = rb;
        float s0 = 0.7f + R * 0.12f, s1 = 1.3f + R * 0.2f;
        switch (el)
        {
            case Element.Fire:
                m = Base(Vector3.Up, 40f, 0.4f, 1.1f, new Vector3(0f, 0.5f, 0f), s0, s1, _rSmoke, _cGrow);
                Sphere(m, R * 0.45f); Spin(m, 30f); break;
            case Element.Frost:
                m = Base(Flat, 180f, R * 0.5f, R, Vector3.Zero, s0 + 0.2f, s1 + 0.3f, _rMist, _cGrow);
                m.Flatness = 0.92f; Sphere(m, R * 0.4f); Damp(m, R * 0.8f, R * 1.2f); Spin(m, 25f); break;
            case Element.Poison:
                m = Base(Flat, 180f, R * 0.4f, R * 0.9f, new Vector3(0f, 0.2f, 0f), s0 + 0.2f, s1 + 0.3f, _rHaze, _cGrow);
                m.Flatness = 0.9f; Sphere(m, R * 0.4f); Damp(m, R * 0.7f, R * 1.1f); Spin(m, 25f); break;
            case Element.Shadow:
                m = Base(Flat, 180f, R * 0.4f, R * 0.9f, new Vector3(0f, 0.4f, 0f), s0, s1, _rDark, _cGrow);
                m.Flatness = 0.8f; Sphere(m, R * 0.4f); Damp(m, R * 0.7f, R * 1.1f); Spin(m, 30f); break;
            default:
                m = Base(Flat, 180f, R * 0.7f, R * 1.3f, new Vector3(0f, 0.3f, 0f), s0, s1, _rDust, _cGrow);
                m.Flatness = 0.9f; Sphere(m, R * 0.35f); Damp(m, R * 1.2f, R * 1.8f); Spin(m, 30f); break;
        }
        return KeepPm(key, m);
    }

    /// <summary>Fire only: embers drifting up out of the blast.</summary>
    private static ParticleProcessMaterial BurstPmC(int rb)
    {
        int key = PmKey(PBurstC, 0, rb);
        if (TryPm(key, out var m)) return m;
        m = Base(Vector3.Up, 35f, 0.7f, 2f, new Vector3(0f, 0.9f, 0f), 0.05f, 0.12f, Ramp(Element.Fire), _cShrink);
        Sphere(m, rb * 0.6f); Turb(m, 0.6f, 4f); m.Color = Bright;
        return KeepPm(key, m);
    }

    /// <summary>Trail shed by a flying shot. The emitter's -Z points back along the flight path.</summary>
    private static ParticleProcessMaterial TrailPm(Element el, bool big, out Look look, out float life, out int amount)
    {
        switch (el)
        {
            case Element.Fire: look = Look.Puff; life = 0.3f; amount = big ? 36 : 24; break;
            case Element.Frost: look = Look.Shard; life = 0.28f; amount = big ? 26 : 16; break;
            case Element.Lightning: look = Look.Streak; life = 0.16f; amount = big ? 22 : 14; break;
            case Element.Poison: look = Look.Dot; life = 0.35f; amount = big ? 26 : 16; break;
            case Element.Shadow: look = Look.Dot; life = 0.38f; amount = big ? 28 : 18; break;
            default: look = Look.Dot; life = 0.16f; amount = 8; break;
        }
        int key = PmKey(PTrail, (int)el, big ? 1 : 0);
        if (TryPm(key, out var m)) return m;
        float s = big ? 1.6f : 1f;
        switch (el)
        {
            case Element.Fire:          // licks of flame, lifting
                m = Base(Back, 25f, 0.5f, 2f, new Vector3(0f, 1.2f, 0f), 0.22f * s, 0.42f * s, _rFlame, _cShrink);
                Sphere(m, 0.06f * s); Spin(m, 60f); break;
            case Element.Frost:         // slivers of ice, falling
                m = Base(Back, 35f, 0.5f, 2.5f, new Vector3(0f, -2.5f, 0f), 0.08f * s, 0.18f * s, Ramp(el), _cShrink);
                Sphere(m, 0.08f * s); Spin(m, 300f); m.Color = Bright; break;
            case Element.Lightning:     // sparks spat sideways
                m = Base(Back, 180f, 2f, 6f, Vector3.Zero, 0.14f * s, 0.32f * s, Ramp(el), _cShrink);
                Sphere(m, 0.05f * s); Damp(m, 14f, 20f); m.Color = Bright; break;
            case Element.Poison:        // dripping venom
                m = Base(Back, 30f, 0.3f, 1.5f, new Vector3(0f, -5f, 0f), 0.06f * s, 0.14f * s, Ramp(el), _cShrink);
                Sphere(m, 0.08f * s); m.Color = Bright; break;
            case Element.Shadow:        // violet motes left hanging in the air
                m = Base(Back, 40f, 0.3f, 1.4f, new Vector3(0f, 0.5f, 0f), 0.07f * s, 0.17f * s, Ramp(el), _cShrink);
                Sphere(m, 0.1f * s); Damp(m, 1f, 2f); m.Color = Bright; break;
            default:                    // the faint wake of a bolt
                m = Base(Back, 20f, 0.2f, 1f, Vector3.Zero, 0.04f, 0.09f, _rFaint, _cShrink);
                Sphere(m, 0.03f); break;
        }
        return KeepPm(key, m);
    }

    /// <summary>Big fire shots also drop sparks that fall away behind them.</summary>
    private static ParticleProcessMaterial TrailSparksPm()
    {
        int key = PmKey(PTrailSparks, 0, 0);
        if (TryPm(key, out var m)) return m;
        m = Base(Back, 50f, 1.5f, 4f, new Vector3(0f, -9f, 0f), 0.14f, 0.3f, Ramp(Element.Fire), _cShrink);
        Sphere(m, 0.1f); m.Color = Bright;
        return KeepPm(key, m);
    }

    /// <summary>What rises from a ground area (for physical zones: what hails down on it).</summary>
    private static ParticleProcessMaterial ZonePm(Element el, float radius, out Look look, out float life)
    {
        switch (el)
        {
            case Element.Fire: look = Look.Puff; life = 0.8f; break;
            case Element.Frost: look = Look.Dot; life = 1.3f; break;
            case Element.Lightning: look = Look.Streak; life = 0.28f; break;
            case Element.Poison: look = Look.Dot; life = 1.3f; break;
            case Element.Shadow: look = Look.Dot; life = 1.2f; break;
            default: look = Look.Streak; life = 0.25f; break;
        }
        int rb = Mathf.Clamp(Mathf.RoundToInt(radius * 2f), 1, 60);
        int key = PmKey(PZone, (int)el, rb);
        if (TryPm(key, out var m)) return m;
        float R = rb * 0.5f;
        switch (el)
        {
            case Element.Fire:          // tongues of flame
                m = Base(Vector3.Up, 10f, 0.7f, 1.7f, new Vector3(0f, 0.8f, 0f), 0.35f, 0.7f, _rFlame, _cPop);
                RingShape(m, R * 0.92f, 0f, 0.05f); Spin(m, 40f); break;
            case Element.Frost:         // motes of rime drifting up
                m = Base(Vector3.Up, 20f, 0.3f, 0.9f, Vector3.Zero, 0.05f, 0.12f, RampSoft(el), _cPop);
                RingShape(m, R * 0.95f, 0f, 0.05f); m.Color = Bright; break;
            case Element.Lightning:     // sparks jumping off the ground
                m = Base(Vector3.Up, 25f, 3f, 7f, new Vector3(0f, -6f, 0f), 0.25f, 0.55f, Ramp(el), _cShrink);
                RingShape(m, R * 0.95f, 0f, 0.05f); m.Color = Bright; break;
            case Element.Poison:        // bubbles of venom
                m = Base(Vector3.Up, 12f, 0.25f, 0.8f, new Vector3(0f, 0.3f, 0f), 0.07f, 0.17f, RampSoft(el), _cPop);
                RingShape(m, R * 0.95f, 0f, 0.05f); m.Color = Bright; break;
            case Element.Shadow:        // violet motes
                m = Base(Vector3.Up, 15f, 0.4f, 1.1f, new Vector3(0f, 0.4f, 0f), 0.06f, 0.15f, RampSoft(el), _cPop);
                RingShape(m, R * 0.95f, 0f, 0.05f); m.Color = Bright; break;
            default:                    // a hail of bolts from above (the emitter hangs 5.5 m up, offset against the slant)
                m = Base(new Vector3(-0.15f, -1f, -0.1f).Normalized(), 2f, 20f, 26f, Vector3.Zero, 0.5f, 0.9f, _rFaint, null);
                Box(m, new Vector3(R * 0.75f, 0.1f, R * 0.75f)); m.Color = Bright; break;
        }
        return KeepPm(key, m);
    }

    /// <summary>The body of a ground area: tongues of flame, a bank of freezing mist, venom fumes or crawling darkness.</summary>
    private static ParticleProcessMaterial ZoneBodyPm(Element el, float radius, out float life)
    {
        life = el switch { Element.Fire => 0.95f, Element.Frost => 2.4f, Element.Poison => 2.6f, _ => 2.0f };
        int rb = Mathf.Clamp(Mathf.RoundToInt(radius * 2f), 1, 60);
        int key = PmKey(PZoneBody, (int)el, rb);
        if (TryPm(key, out var m)) return m;
        float R = rb * 0.5f;
        switch (el)
        {
            case Element.Fire:
                m = Base(Vector3.Up, 9f, 0.9f, 2.3f, new Vector3(0f, 1.3f, 0f), 0.55f, 1.15f, _rFlame, _cFlame);
                RingShape(m, R * 0.88f, 0f, 0.05f); Spin(m, 45f); break;
            case Element.Frost:
                m = Base(Vector3.Up, 60f, 0.05f, 0.3f, new Vector3(0f, 0.05f, 0f), 1.1f, 2.0f, _rMist, _cGrow);
                RingShape(m, R * 0.85f, 0f, 0.05f); Spin(m, 14f); break;
            case Element.Poison:
                m = Base(Vector3.Up, 40f, 0.1f, 0.45f, new Vector3(0f, 0.12f, 0f), 1.0f, 1.9f, _rHaze, _cGrow);
                RingShape(m, R * 0.85f, 0f, 0.05f); Spin(m, 18f); break;
            default:
                m = Base(Vector3.Up, 30f, 0.25f, 0.8f, new Vector3(0f, 0.3f, 0f), 0.9f, 1.7f, _rDark, _cGrow);
                RingShape(m, R * 0.85f, 0f, 0.05f); Spin(m, 25f); Turb(m, 0.6f, 2.5f); break;
        }
        return KeepPm(key, m);
    }

    /// <summary>Embers climbing out of burning ground.</summary>
    private static ParticleProcessMaterial ZoneEmberPm(float radius)
    {
        int rb = Mathf.Clamp(Mathf.RoundToInt(radius * 2f), 1, 60);
        int key = PmKey(PZoneEmber, 0, rb);
        if (TryPm(key, out var m)) return m;
        m = Base(Vector3.Up, 20f, 1.2f, 3.2f, new Vector3(0f, 0.6f, 0f), 0.035f, 0.085f, Ramp(Element.Fire), _cShrink);
        RingShape(m, rb * 0.5f * 0.9f, 0f, 0.05f); Turb(m, 1.1f, 2.6f); m.Color = new Color(1.9f, 1.9f, 1.9f, 1f);
        return KeepPm(key, m);
    }

    /// <summary>Thin smoke drifting off a burning area.</summary>
    private static ParticleProcessMaterial ZoneSmokePm(Element el, float radius)
    {
        int rb = Mathf.Clamp(Mathf.RoundToInt(radius * 2f), 1, 60);
        int key = PmKey(PZoneSmoke, (int)el, rb);
        if (TryPm(key, out var m)) return m;
        m = Base(Vector3.Up, 14f, 0.7f, 1.4f, new Vector3(0.15f, 0.35f, 0.05f), 1.1f, 2.1f, _rSmoke, _cGrow);
        RingShape(m, rb * 0.5f * 0.75f, 0f, 0.05f); Spin(m, 18f);
        return KeepPm(key, m);
    }

    /// <summary>What a ground wave throws up along its front. half = half the width of the front.</summary>
    private static ParticleProcessMaterial WavePm(Element el, float half, out Look look, out float life, out int amount)
    {
        int wb = Mathf.Clamp(Mathf.RoundToInt(half * 4f), 2, 40);
        float w = wb * 0.25f;
        switch (el)
        {
            case Element.Fire: look = Look.Puff; life = 0.45f; break;
            case Element.Physical: look = Look.Smoke; life = 0.6f; break;
            case Element.Frost: look = Look.Shard; life = 0.5f; break;
            default: look = Look.Dot; life = 0.5f; break;
        }
        amount = Mathf.Clamp((int)(14f + w * 8f), 16, 40);
        if (el == Element.Physical) amount = Mathf.Clamp(amount / 2, 10, 20);
        int key = PmKey(PWave, (int)el, wb);
        if (TryPm(key, out var m)) return m;
        switch (el)
        {
            case Element.Fire:
                m = Base(Vector3.Up, 20f, 1.2f, 3f, new Vector3(0f, 1.5f, 0f), 0.4f, 0.8f, _rFlame, _cShrink);
                Spin(m, 60f); break;
            case Element.Physical:
                m = Base(Vector3.Up, 45f, 0.8f, 2.2f, new Vector3(0f, -1.5f, 0f), 0.5f, 1f, _rDust, _cGrow);
                Spin(m, 30f); break;
            case Element.Frost:
                m = Base(Vector3.Up, 30f, 1f, 3f, new Vector3(0f, -4f, 0f), 0.1f, 0.24f, Ramp(el), _cShrink);
                Spin(m, 300f); m.Color = Bright; break;
            default:
                m = Base(Vector3.Up, 30f, 1f, 3f, new Vector3(0f, -2f, 0f), 0.08f, 0.2f, Ramp(el), _cShrink);
                m.Color = Bright; break;
        }
        Box(m, new Vector3(w, 0.08f, 0.25f));
        return KeepPm(key, m);
    }

    /// <summary>Motes circling up around a buffed actor.</summary>
    private static ParticleProcessMaterial AuraPm(Element el, bool big)
    {
        int key = PmKey(PAura, (int)el, big ? 1 : 0);
        if (TryPm(key, out var m)) return m;
        float r = big ? 0.95f : 0.75f;
        Texture2D ramp = el == Element.Physical ? ColorRamp(new Color(0.95f, 0.88f, 0.72f)) : RampSoft(el);
        // tangential acceleration turns around the gravity axis, so a little lift is needed for the motes to circle
        m = Base(Vector3.Up, 8f, big ? 1f : 0.7f, big ? 2.2f : 1.5f, new Vector3(0f, 0.4f, 0f), big ? 0.07f : 0.05f, big ? 0.17f : 0.12f, ramp, _cPop);
        RingShape(m, r, r * 0.75f, 0.3f);
        m.TangentialAccelMin = 2f; m.TangentialAccelMax = 4f;
        m.Color = Bright;
        return KeepPm(key, m);
    }

    /// <summary>What a dashing actor sheds: dust at the feet for physical dashes, the element's motes otherwise.</summary>
    private static ParticleProcessMaterial DashPm(Element el, out Look look, out float life, out int amount)
    {
        switch (el)
        {
            case Element.Physical: look = Look.Smoke; life = 0.45f; amount = 14; break;
            case Element.Frost: look = Look.Shard; life = 0.4f; amount = 28; break;
            case Element.Lightning: look = Look.Streak; life = 0.25f; amount = 26; break;
            case Element.Fire: look = Look.Puff; life = 0.4f; amount = 26; break;
            default: look = Look.Dot; life = 0.45f; amount = 30; break;
        }
        int key = PmKey(PDash, (int)el, 0);
        if (TryPm(key, out var m)) return m;
        switch (el)
        {
            case Element.Physical:
                m = Base(Vector3.Up, 60f, 0.3f, 1.2f, new Vector3(0f, 0.4f, 0f), 0.4f, 0.8f, _rDust, _cGrow);
                Box(m, new Vector3(0.25f, 0.1f, 0.25f)); Spin(m, 30f); break;
            case Element.Fire:
                m = Base(Vector3.Up, 40f, 0.3f, 1.4f, new Vector3(0f, 1.2f, 0f), 0.28f, 0.55f, _rFlame, _cShrink);
                Box(m, new Vector3(0.28f, 0.7f, 0.28f)); Spin(m, 60f); break;
            case Element.Frost:
                m = Base(Vector3.Up, 180f, 0.3f, 1.6f, new Vector3(0f, -2f, 0f), 0.08f, 0.2f, Ramp(el), _cShrink);
                Box(m, new Vector3(0.28f, 0.7f, 0.28f)); Spin(m, 300f); m.Color = Bright; break;
            case Element.Lightning:
                m = Base(Vector3.Up, 180f, 2f, 5f, Vector3.Zero, 0.16f, 0.36f, Ramp(el), _cShrink);
                Box(m, new Vector3(0.28f, 0.7f, 0.28f)); Damp(m, 12f, 18f); m.Color = Bright; break;
            default:
                m = Base(Vector3.Up, 180f, 0.3f, 1.4f, new Vector3(0f, 0.4f, 0f), 0.08f, 0.2f, Ramp(el), _cShrink);
                Box(m, new Vector3(0.28f, 0.7f, 0.28f)); Damp(m, 1f, 2f); m.Color = Bright; break;
        }
        return KeepPm(key, m);
    }

    /// <summary>Ash flakes peeling off a corpse (rb = body radius in quarter metres).</summary>
    private static ParticleProcessMaterial AshFlakePm(int rb)
    {
        int key = PmKey(PAshFlake, 0, rb);
        if (TryPm(key, out var m)) return m;
        float r = rb * 0.25f;
        m = Base(new Vector3(0.25f, 1f, 0.1f).Normalized(), 35f, 0.5f, 1.7f, new Vector3(0.5f, 0.45f, 0.15f), 0.05f, 0.12f, _rAsh, _cShrinkLate);
        Box(m, new Vector3(r, 0.25f, r)); Spin(m, 260f); Turb(m, 1.2f, 3f);
        return KeepPm(key, m);
    }

    /// <summary>Embers leaving a corpse.</summary>
    private static ParticleProcessMaterial AshEmberPm(int rb)
    {
        int key = PmKey(PAshEmber, 0, rb);
        if (TryPm(key, out var m)) return m;
        float r = rb * 0.25f;
        m = Base(Vector3.Up, 25f, 0.8f, 2.2f, new Vector3(0.2f, 0.7f, 0f), 0.04f, 0.1f, Ramp(Element.Fire), _cShrink);
        Box(m, new Vector3(r, 0.25f, r)); Turb(m, 0.8f, 3f); m.Color = new Color(1.7f, 1.7f, 1.7f, 1f);
        return KeepPm(key, m);
    }

    private static ParticleProcessMaterial DustPm()
    {
        int key = PmKey(PDust, 0, 0);
        if (TryPm(key, out var m)) return m;
        m = Base(Flat, 180f, 1f, 2.6f, new Vector3(0f, 0.3f, 0f), 0.4f, 0.8f, _rDust, _cGrow);
        m.Flatness = 0.85f; Sphere(m, 0.25f); Damp(m, 3f, 5f); Spin(m, 30f);
        return KeepPm(key, m);
    }

    private static ParticleProcessMaterial HealPm()
    {
        int key = PmKey(PHeal, 0, 0);
        if (TryPm(key, out var m)) return m;
        m = Base(Vector3.Up, 6f, 1.2f, 2.4f, Vector3.Zero, 0.06f, 0.14f, _rHeal, _cPop);
        RingShape(m, 0.7f, 0.4f, 0.2f); m.Color = Bright;
        return KeepPm(key, m);
    }

    private static ParticleProcessMaterial LevelSparkPm()
    {
        int key = PmKey(PLvlSpark, 0, 0);
        if (TryPm(key, out var m)) return m;
        m = Base(Vector3.Up, 20f, 5f, 10f, new Vector3(0f, -7f, 0f), 0.25f, 0.5f, _rGold, _cShrink);
        Sphere(m, 0.4f); m.Color = new Color(1.7f, 1.7f, 1.7f, 1f);
        return KeepPm(key, m);
    }

    private static ParticleProcessMaterial LevelEmberPm()
    {
        int key = PmKey(PLvlEmber, 0, 0);
        if (TryPm(key, out var m)) return m;
        m = Base(Vector3.Up, 12f, 1f, 3.2f, new Vector3(0f, 0.4f, 0f), 0.06f, 0.14f, _rGold, _cPop);
        RingShape(m, 1f, 0.2f, 0.3f); Turb(m, 0.6f, 3f);
        m.TangentialAccelMin = 1.5f; m.TangentialAccelMax = 3f;
        m.Color = new Color(1.7f, 1.7f, 1.7f, 1f);
        return KeepPm(key, m);
    }

    private static ParticleProcessMaterial TeleportPm(Element el)
    {
        int key = PmKey(PTeleport, (int)el, 0);
        if (TryPm(key, out var m)) return m;
        m = Base(Vector3.Up, 10f, 3f, 8f, Vector3.Zero, 0.3f, 0.6f, Ramp(el), _cShrink);
        Sphere(m, 0.45f); Damp(m, 5f, 8f); m.Color = Bright;
        return KeepPm(key, m);
    }

    /// <summary>Motes drawn in toward the casting hand.</summary>
    private static ParticleProcessMaterial GatherPm(Element el)
    {
        int key = PmKey(PGather, (int)el, 0);
        if (TryPm(key, out var m)) return m;
        m = Base(Vector3.Up, 180f, 0f, 0.2f, Vector3.Zero, 0.05f, 0.11f, Ramp(el), _cPop);
        m.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.SphereSurface;
        m.EmissionSphereRadius = 0.5f;
        m.RadialAccelMin = -14f; m.RadialAccelMax = -9f;
        m.Color = Bright;
        return KeepPm(key, m);
    }

    /// <summary>Flames of a looping fire (sb = size in tenths of a metre).</summary>
    private static ParticleProcessMaterial FlamePm(int sb)
    {
        int key = PmKey(PFlame, 0, sb);
        if (TryPm(key, out var m)) return m;
        float s = sb * 0.1f, q = Mathf.Sqrt(s);
        m = Base(Vector3.Up, 12f, 1f * q, 2f * q, new Vector3(0f, 1f, 0f), Mathf.Min(s * 0.5f, 1.1f), Mathf.Min(s * 0.85f, 1.7f), _rFlame, _cFlame);
        Sphere(m, s * 0.26f); Spin(m, 50f);
        m.Color = new Color(1f, 1f, 1f, 1f / (1f + s * 1.3f));       // additive puffs pile up: the bigger the fire, the thinner each one
        return KeepPm(key, m);
    }

    private static ParticleProcessMaterial FireEmberPm(int sb)
    {
        int key = PmKey(PFireEmber, 0, sb);
        if (TryPm(key, out var m)) return m;
        float s = sb * 0.1f, q = Mathf.Sqrt(s), k = 0.7f + 0.3f * s;
        m = Base(Vector3.Up, 18f, 1.2f * q, 2.8f * q, new Vector3(0f, 0.5f, 0f), 0.03f * k, 0.07f * k, Ramp(Element.Fire), _cShrink);
        Sphere(m, s * 0.28f); Turb(m, 0.9f, 3f); m.Color = new Color(1.8f, 1.8f, 1.8f, 1f);
        return KeepPm(key, m);
    }

    private static ParticleProcessMaterial FireSmokePm(int sb)
    {
        int key = PmKey(PFireSmoke, 0, sb);
        if (TryPm(key, out var m)) return m;
        float s = sb * 0.1f;
        m = Base(Vector3.Up, 10f, 0.8f, 1.5f, new Vector3(0f, 0.3f, 0f), s * 0.8f, s * 1.4f, _rSmokeThin, _cGrow);
        Sphere(m, s * 0.25f); Spin(m, 20f);
        return KeepPm(key, m);
    }

    /// <summary>Sparkles hanging in a grand loot beam of height h.</summary>
    private static ParticleProcessMaterial SparklePm(Rarity r, Color c, float h)
    {
        int key = PmKey(PSparkle, (int)r, 0);
        if (TryPm(key, out var m)) return m;
        m = Base(Vector3.Up, 180f, 0.05f, 0.3f, new Vector3(0f, 0.15f, 0f), 0.04f, 0.1f, ColorRamp(c), _cPop);
        Box(m, new Vector3(0.3f, h * 0.4f, 0.3f)); m.Color = Bright;
        return KeepPm(key, m);
    }

    /// <summary>Motes spiralling up around a portal's foot.</summary>
    private static ParticleProcessMaterial PortalPm(Color c, float radius)
    {
        int rb = Mathf.Clamp(Mathf.RoundToInt(radius * 4f), 1, 40);
        ulong key = ((ulong)c.ToRgba32() << 8) | (uint)rb;
        ParticleProcessMaterial m;
        lock (_portalPm) { if (_portalPm.TryGetValue(key, out m)) return m; }
        float R = rb * 0.25f;
        m = Base(Vector3.Up, 8f, 0.8f, 2.1f, new Vector3(0f, 0.3f, 0f), 0.05f, 0.12f, ColorRamp(c), _cPop);
        RingShape(m, R * 0.95f, R * 0.7f, 0.1f);
        m.TangentialAccelMin = 2f; m.TangentialAccelMax = 4.5f;
        m.Color = Bright;
        lock (_portalPm) _portalPm[key] = m;
        return m;
    }

    // ───────────────────────── colour ramps and curves ─────────────────────────

    private static Texture2D Grad(params (float o, Color c)[] pts)
    {
        var offs = new float[pts.Length];
        var cols = new Color[pts.Length];
        for (int i = 0; i < pts.Length; i++) { offs[i] = pts[i].o; cols[i] = pts[i].c; }
        var g = new Gradient();
        g.Offsets = offs;
        g.Colors = cols;
        return new GradientTexture1D { Gradient = g, Width = 64 };
    }

    private static Texture2D CurveTex(params (float x, float y)[] pts)
    {
        var c = new Curve();
        foreach (var p in pts) c.AddPoint(new Vector2(p.x, p.y));
        return new CurveTexture { Curve = c, Width = 64 };
    }

    /// <summary>Hot → element colour → dark, fading out: for particles born bright (sparks, shards, droplets).</summary>
    private static Texture2D Ramp(Element el)
    {
        int key = (int)el;
        Texture2D t;
        lock (_ramps) { if (_ramps.TryGetValue(key, out t)) return t; }
        Color c = Palette.Element(el), hot = c.Lerp(Colors.White, 0.7f);
        t = Grad((0f, hot), (0.3f, c), (0.75f, Mul(c, 0.6f, 0.6f)), (1f, Mul(c, 0.25f, 0f)));
        lock (_ramps) _ramps[key] = t;
        return t;
    }

    /// <summary>Fades in, glows in the element colour, fades out: for ambient motes.</summary>
    private static Texture2D RampSoft(Element el)
    {
        int key = 100 + (int)el;
        Texture2D t;
        lock (_ramps) { if (_ramps.TryGetValue(key, out t)) return t; }
        Color c = Palette.Element(el), hot = c.Lerp(Colors.White, 0.6f);
        t = Grad((0f, Mul(c, 1f, 0f)), (0.2f, Mul(hot, 1f, 0.9f)), (0.6f, Mul(c, 1f, 0.8f)), (1f, Mul(c, 0.4f, 0f)));
        lock (_ramps) _ramps[key] = t;
        return t;
    }

    /// <summary>Like <see cref="RampSoft"/> for an arbitrary colour (rarities, portals).</summary>
    private static Texture2D ColorRamp(Color c)
    {
        uint key = c.ToRgba32();
        Texture2D t;
        lock (_colorRamps) { if (_colorRamps.TryGetValue(key, out t)) return t; }
        Color hot = c.Lerp(Colors.White, 0.6f);
        t = Grad((0f, Mul(c, 1f, 0f)), (0.2f, Mul(hot, 1f, 1f)), (0.65f, Mul(c, 1f, 0.8f)), (1f, Mul(c, 0.4f, 0f)));
        lock (_colorRamps) _colorRamps[key] = t;
        return t;
    }

    // ═════════════════════════ shared resources ═════════════════════════

    private static readonly object BuildLock = new();

    private static bool Ensure()
    {
        if (_ready) return true;
        if (_failed) return false;
        lock (BuildLock)
        {
            if (_ready) return true;
            if (_failed) return false;
            try
            {
                Build();
                _ready = true;
                Log.Info("fx", $"effect resources ready (budget: {MaxOneShots} one-shot emitters, {MaxFlashLights} flash lights, {MaxSoftLights} soft lights)");
            }
            catch (Exception e)
            {
                _failed = true;
                Log.Error("fx", "building the shared effect resources failed; effects are disabled", e);
            }
        }
        return _ready;
    }

    private static void Build()
    {
        _tree = Engine.GetMainLoop() as SceneTree;
        Texture2D dot = SoftDot, noise = NoiseTex;
        _puff = MakePuff();
        _shard = MakeShard();
        _streak = MakeStreak();

        // unit meshes, scaled per instance
        _quad = new PlaneMesh { Size = new Vector2(2f, 2f), Orientation = PlaneMesh.OrientationEnum.Y };   // ground quad: local XZ in -1..1
        _quadXY = new QuadMesh { Size = new Vector2(2f, 2f) };                   // upright quad: local XY in -1..1
        _sphere = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 };
        _cyl = new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 1f, RadialSegments = 14, Rings = 1, CapTop = false, CapBottom = false };
        _shaft = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 1f, Height = 1f, RadialSegments = 6, Rings = 1 };
        _plate = new CylinderMesh { TopRadius = 0.4f, BottomRadius = 0.46f, Height = 0.06f, RadialSegments = 10, Rings = 1 };
        _spike = new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.05f, Height = 0.26f, RadialSegments = 5, Rings = 1 };

        // one material per shader; instance uniforms carry everything that differs
        _mRing = ShaderMat("fx_ring", noise);
        _mTele = ShaderMat("fx_telegraph", null);
        _mZone = ShaderMat("fx_zone", noise);
        _mSlash = ShaderMat("fx_slash", noise);
        _mFlash = ShaderMat("fx_flash", null);
        _mBeam = ShaderMat("fx_beam", noise);
        _mPillar = ShaderMat("fx_pillar", noise);
        _mWave = ShaderMat("fx_wave", noise);
        _mSwirl = ShaderMat("fx_swirl", noise);
        _mFlame = ShaderMat("env_flame", noise);
        _flameQuad = new QuadMesh { Size = Vector2.One, Material = _mFlame };
        // many flames side by side (burning ground, great braziers): each card dimmer and with little halo, or they add up to a white sheet
        _mFlameField = ShaderMat("env_flame", noise);
        if (_mFlameField is ShaderMaterial ff)
        {
            ff.SetShaderParameter("energy", 1.8f); ff.SetShaderParameter("halo", 0.04f);
            ff.SetShaderParameter("span", 1.5f); ff.SetShaderParameter("base", 0.1f);
        }
        _flameFieldQuad = new QuadMesh { Size = Vector2.One, Material = _mFlameField };
        _mShaft = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.86f, 0.83f, 0.74f) };
        _mShaftHostile = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.78f, 0.5f, 0.42f) };
        _mIron = new StandardMaterial3D { AlbedoColor = new Color(0.17f, 0.16f, 0.15f), Metallic = 0.7f, Roughness = 0.45f };

        // particle draw passes
        _draw[(int)Look.Dot] = DrawQuad(dot, true, true, 1f, 1f);
        _draw[(int)Look.Puff] = DrawQuad(_puff, true, true, 1f, 1f);
        _draw[(int)Look.Smoke] = DrawQuad(_puff, false, true, 1f, 1f);
        _draw[(int)Look.Streak] = DrawQuad(_streak, true, false, 0.14f, 1f);
        _draw[(int)Look.Shard] = DrawQuad(_shard, true, true, 0.5f, 1f);
        _draw[(int)Look.Chip] = DrawQuad(_shard, false, true, 0.7f, 1f);

        // size over life
        _cShrink = CurveTex((0f, 1f), (0.6f, 0.7f), (1f, 0f));
        _cShrinkLate = CurveTex((0f, 1f), (0.75f, 0.9f), (1f, 0f));
        _cGrow = CurveTex((0f, 0.45f), (1f, 1f));
        _cPop = CurveTex((0f, 0.2f), (0.2f, 1f), (1f, 0.1f));
        _cFlame = CurveTex((0f, 0.55f), (0.25f, 1f), (1f, 0.15f));

        // colour over life
        _rFlame = Grad((0f, new Color(1f, 0.92f, 0.6f, 0f)), (0.1f, new Color(1f, 0.8f, 0.35f, 0.85f)), (0.45f, new Color(1f, 0.42f, 0.08f, 0.7f)),
            (0.8f, new Color(0.75f, 0.14f, 0.03f, 0.3f)), (1f, new Color(0.3f, 0.05f, 0.01f, 0f)));
        _rSmoke = Grad((0f, new Color(0.1f, 0.09f, 0.08f, 0f)), (0.2f, new Color(0.1f, 0.09f, 0.08f, 0.3f)), (1f, new Color(0.06f, 0.06f, 0.06f, 0f)));
        _rSmokeThin = Grad((0f, new Color(0.08f, 0.075f, 0.07f, 0f)), (0.25f, new Color(0.08f, 0.075f, 0.07f, 0.2f)), (1f, new Color(0.05f, 0.05f, 0.05f, 0f)));
        _rDark = Grad((0f, new Color(0.08f, 0.03f, 0.12f, 0f)), (0.2f, new Color(0.08f, 0.03f, 0.12f, 0.5f)), (1f, new Color(0.03f, 0.01f, 0.05f, 0f)));
        _rDust = Grad((0f, new Color(0.52f, 0.47f, 0.4f, 0f)), (0.15f, new Color(0.52f, 0.47f, 0.4f, 0.32f)), (1f, new Color(0.4f, 0.37f, 0.33f, 0f)));
        _rMist = Grad((0f, new Color(0.6f, 0.85f, 1f, 0f)), (0.2f, new Color(0.6f, 0.85f, 1f, 0.2f)), (1f, new Color(0.4f, 0.6f, 0.9f, 0f)));
        _rHaze = Grad((0f, new Color(0.5f, 0.85f, 0.2f, 0f)), (0.2f, new Color(0.5f, 0.85f, 0.2f, 0.2f)), (1f, new Color(0.2f, 0.45f, 0.08f, 0f)));
        _rAsh = Grad((0f, new Color(0.58f, 0.55f, 0.52f, 0f)), (0.1f, new Color(0.58f, 0.55f, 0.52f, 0.9f)), (0.6f, new Color(0.4f, 0.38f, 0.36f, 0.7f)),
            (1f, new Color(0.28f, 0.27f, 0.26f, 0f)));
        _rChip = Grad((0f, new Color(0.32f, 0.29f, 0.26f, 1f)), (0.7f, new Color(0.25f, 0.23f, 0.21f, 1f)), (1f, new Color(0.2f, 0.19f, 0.18f, 0f)));
        _rHeal = Grad((0f, new Color(0.95f, 1f, 0.75f, 0f)), (0.15f, new Color(0.8f, 1f, 0.6f, 1f)), (0.6f, new Color(0.45f, 0.9f, 0.4f, 0.8f)),
            (1f, new Color(0.2f, 0.6f, 0.2f, 0f)));
        _rGold = Grad((0f, new Color(1f, 0.97f, 0.8f, 1f)), (0.3f, new Color(1f, 0.75f, 0.3f, 1f)), (0.75f, new Color(1f, 0.45f, 0.1f, 0.6f)),
            (1f, new Color(0.5f, 0.15f, 0.03f, 0f)));
        _rFaint = Grad((0f, new Color(0.9f, 0.88f, 0.82f, 0.5f)), (1f, new Color(0.6f, 0.58f, 0.55f, 0f)));

        if (_tree != null && !_hooked)
        {
            _tree.ProcessFrame += OnFrame;
            _hooked = true;
        }
        else if (_tree == null) Log.Warn("fx", "no SceneTree yet: effects will not animate until one exists");
    }

    private static Material ShaderMat(string name, Texture2D noise)
    {
        string path = $"res://shaders/{name}.gdshader";
        Shader sh = ResourceLoader.Exists(path) ? GD.Load<Shader>(path) : null;
        if (sh == null)
        {
            Log.Error("fx", $"shader {path} is missing; using a plain additive stand-in");
            return new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                AlbedoColor = new Color(1f, 0.6f, 0.3f, 0.3f),
            };
        }
        var m = new ShaderMaterial { Shader = sh };
        if (noise != null) m.SetShaderParameter(PNoise, noise);
        return m;
    }

    /// <summary>The quad one particle is drawn with. Billboards turn to the camera; the others are aligned to their velocity by the emitter.</summary>
    private static Mesh DrawQuad(Texture2D tex, bool additive, bool billboard, float w, float h)
    {
        var m = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoTexture = tex,
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            DisableReceiveShadows = true,
        };
        if (billboard)
        {
            m.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
            m.BillboardKeepScale = true;
        }
        return new QuadMesh { Size = new Vector2(w, h), Material = m };
    }

    private static Texture2D MakeDot()
    {
        var g = new Gradient();
        g.Offsets = new[] { 0f, 0.22f, 0.55f, 1f };
        g.Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.72f), new Color(1f, 1f, 1f, 0.2f), new Color(1f, 1f, 1f, 0f) };
        return new GradientTexture2D
        {
            Gradient = g, Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
        };
    }

    private static Texture2D MakeNoise()
    {
        var fn = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.018f, Seed = 7,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 4,
        };
        try
        {
            // generated right away (a NoiseTexture2D would fill in a few frames later and effects would start blank)
            Image img = fn.GetSeamlessImage(256, 256);
            if (img != null && !img.IsEmpty())
            {
                img.GenerateMipmaps();
                return ImageTexture.CreateFromImage(img);
            }
        }
        catch (Exception e) { Log.Warn("fx", "immediate noise image failed, using a NoiseTexture2D: " + e.Message); }
        return new NoiseTexture2D { Noise = fn, Width = 256, Height = 256, Seamless = true, GenerateMipmaps = true };
    }

    private static Texture2D FromAlpha(int w, int h, Func<float, float, int, int, float> alpha)
    {
        var data = new byte[w * h * 4];
        int i = 0;
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h * 2f - 1f;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                data[i++] = 255; data[i++] = 255; data[i++] = 255;
                data[i++] = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha(u, v, x, y) * 255f), 0, 255);
            }
        }
        var img = Image.CreateFromData(w, h, false, Image.Format.Rgba8, data);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>A wispy cloud: radial falloff broken up by noise. Used additively (flames, mist) and alpha-blended (smoke, dust).</summary>
    private static Texture2D MakePuff()
    {
        var fn = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.035f, FractalOctaves = 3, Seed = 21 };
        return FromAlpha(96, 96, (u, v, x, y) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float fall = Mathf.Clamp(1f - r, 0f, 1f);
            fall = fall * fall * (3f - 2f * fall);
            float n = fn.GetNoise2D(x, y) * 0.5f + 0.5f;
            return fall * (0.45f + 0.9f * n) * 1.25f - 0.04f;
        });
    }

    /// <summary>A hard-edged sliver: ice shards, ash flakes, debris.</summary>
    private static Texture2D MakeShard()
    {
        return FromAlpha(32, 64, (u, v, x, y) =>
        {
            float d = 1f - (Mathf.Abs(u) * 1.05f + Mathf.Abs(v + u * 0.25f));
            return Mathf.Clamp(d * 5f, 0f, 1f) * (0.6f + 0.4f * (1f - Mathf.Abs(u)));
        });
    }

    /// <summary>A spark: bright head at the top (the direction of travel), tail fading behind.</summary>
    private static Texture2D MakeStreak()
    {
        return FromAlpha(16, 64, (u, v, x, y) =>
        {
            float across = 1f - Mathf.Abs(u);
            float k = v * 0.5f + 0.5f;                              // 0 at the head end, 1 at the tail end
            float along = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
            return across * across * Mathf.Pow(Mathf.Clamp(along, 0f, 1f), 1.5f);
        });
    }
}
