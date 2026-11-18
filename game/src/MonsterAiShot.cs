using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Monster missiles the shared Projectile does not cover: the bomber's lob (an arc that ignores low obstacles and
/// lands on a marked circle), the snare (big, slow, a web patch where it misses) and the returning disc.
/// </summary>
public partial class MonsterShot : Node3D
{
    private enum Kind { Lob, Snare, Disc }
    private Kind _kind;
    private Monster _m;
    private Vector3 _from;
    private Vector2 _land, _dir, _start;
    private float _t, _time, _speed, _range, _radius, _height, _went;
    private bool _back;
    private readonly HashSet<Actor> _hitOut = new(), _hitBack = new();
    private static readonly List<Actor> Tmp = new();

    private Vector2 Pos2 => new(GlobalPosition.X, GlobalPosition.Z);

    private static MonsterShot Make(Monster m, Kind kind, Vector3 origin, Element el, float radius)
    {
        var s = new MonsterShot { _m = m, _kind = kind, _from = origin, _radius = radius, _height = origin.Y };
        World.Root.AddChild(s);
        s.GlobalPosition = origin;
        s.AddChild(Fx.ProjectileVisual(el, radius > 0.7f ? 1 : 0, radius, kind == Kind.Disc ? "disc" : "monster", true));
        return s;
    }

    /// <summary>A lobbed bomb: flies <paramref name="time"/> seconds to a point marked with a circle of <paramref name="radius"/>.</summary>
    public static void Lob(Monster m, Vector3 origin, Vector2 land, float time, float radius)
    {
        if (World.Root == null) return;
        Fx.Telegraph(land, radius, time, Palette.Danger);
        var s = Make(m, Kind.Lob, origin, m.Def.Element, 0.5f);
        s._land = land; s._time = Mathf.Max(0.3f, time); s._start = new Vector2(origin.X, origin.Z);
    }

    /// <summary>The snare: radius 0.8 m at 11 m/s — readable. A hit roots; a miss leaves a sticky patch.</summary>
    public static void Snare(Monster m, Vector3 origin, Vector2 dir)
    {
        if (World.Root == null) return;
        origin.Y = Mathf.Clamp(origin.Y, 0.9f, 1.6f);
        var s = Make(m, Kind.Snare, origin, m.Def.Element == Element.Physical ? Element.Poison : m.Def.Element, 0.8f);
        s._dir = dir.Normalized(); s._speed = 11f; s._range = m.Def.Range + 4f;
        s.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Y), 0);
    }

    /// <summary>The returning disc: out to 12 m and back along the same line, one hit each way (0.6x on the way back).</summary>
    public static void Disc(Monster m, Vector3 origin, Vector2 dir)
    {
        if (World.Root == null) return;
        origin.Y = Mathf.Clamp(origin.Y, 0.9f, 1.5f);
        var s = Make(m, Kind.Disc, origin, Element.Physical, 0.5f);
        s._dir = dir.Normalized(); s._speed = 17f; s._range = 12f;
        s._start = new Vector2(origin.X, origin.Z);
        Vector2 end = s._start + s._dir * s._range;
        if (World.Grid != null) end = World.Grid.ClampLine(s._start, end, true);
        Fx.TelegraphLine(s._start, s._dir, s._start.DistanceTo(end), 0.25f, 2f * s._start.DistanceTo(end) / s._speed, new Color(0.9f, 0.75f, 0.4f));
    }

    public override void _Process(double delta)
    {
        if (Game.I.Paused) return;
        try { Tick((float)delta); }
        catch (Exception e) { Log.Error("monster-ai", $"monster shot {_kind} failed", e); QueueFree(); }
    }

    private bool OwnerOk => _m != null && IsInstanceValid(_m);

    private void Tick(float dt)
    {
        switch (_kind)
        {
            case Kind.Lob:
            {
                _t += dt;
                float k = Mathf.Clamp(_t / _time, 0f, 1f);
                Vector2 q = _start.Lerp(_land, k);
                float y = Mathf.Lerp(_from.Y, 0.3f, k) + Mathf.Sin(k * Mathf.Pi) * 3.2f;
                GlobalPosition = new Vector3(q.X, y, q.Y);
                if (k >= 1f) { if (OwnerOk) _m.BombLanded(_land); QueueFree(); }
                break;
            }
            case Kind.Snare:
            {
                float step = _speed * dt;
                Vector2 from = Pos2, to = from + _dir * step;
                if (World.Grid != null && !World.Grid.LineShot(from, to)) { Missed(from); return; }
                GlobalPosition = new Vector3(to.X, _height, to.Y);
                _went += step;
                World.InCapsule(Faction.Monster, from, to, _radius, Tmp);
                if (Tmp.Count > 0 && OwnerOk)
                {
                    var a = Tmp[0];
                    _m.Strike(a, 0.4f, _m.Def.Ai.SnareRoot, false);
                    Fx.Hit(a.Chest, Element.Poison, false);
                    Snd.At("poison_splash", a.GlobalPosition, 0.6f);
                    AiNote("Snare.hit");
                    QueueFree();
                    return;
                }
                if (_went >= _range) Missed(to);
                break;
            }
            case Kind.Disc:
            {
                float step = _speed * dt;
                Vector2 from = Pos2;
                Vector2 dir = _back ? (OwnerOk && !_m.Dead ? (_m.Pos2 - from).Normalized() : -_dir) : _dir;
                Vector2 to = from + dir * step;
                if (!_back && World.Grid != null && !World.Grid.LineShot(from, to)) { _back = true; return; }
                GlobalPosition = new Vector3(to.X, _height, to.Y);
                RotateY(dt * 20f);
                _went += step;
                World.InCapsule(Faction.Monster, from, to, _radius, Tmp);
                foreach (var a in Tmp.ToArray())
                {
                    var set = _back ? _hitBack : _hitOut;
                    if (!set.Add(a) || !OwnerOk) continue;
                    _m.Strike(a, _back ? 0.6f : 1f, null, false);
                    Fx.Hit(a.Chest, Element.Physical, false);
                    Snd.At("hit_metal", a.GlobalPosition, 0.6f);
                    AiNote(_back ? "Return.hitback" : "Return.hitout");
                }
                if (!_back && _went >= _range) { _back = true; AiNote("Return.turn"); }
                if (_back && (!OwnerOk || _m.Dead || to.DistanceTo(_m.Pos2) < 0.8f || _went > _range * 2.6f)) QueueFree();
                break;
            }
        }
    }

    private void Missed(Vector2 at)
    {
        if (OwnerOk && World.Root != null)
            GroundZone.SpawnMonster(_m, at, 1.6f, 5f, 0f, Element.Poison, St.Slow(0.4f, 1f));
        AiNote("Snare.web");
        QueueFree();
    }

    private static void AiNote(string what)
    {
        Monster.AiSeen.TryGetValue(what, out int n); Monster.AiSeen[what] = n + 1;
        if (n < 3) Log.Info("ai", what);
    }
}

/// <summary>An expanding ring on the ground (Pulse): visible as it travels; standing in the gap or rolling through it is safe.</summary>
public partial class PulseRing : Node3D
{
    private Monster _m;
    private float _r, _max, _speed, _width, _mul;
    private StatusApply? _status;
    private MeshInstance3D _mesh;
    private TorusMesh _torus;
    private readonly HashSet<Actor> _hit = new();
    private static readonly List<Actor> Tmp = new();
    private static StandardMaterial3D _mat;

    public static void Start(Monster m, Vector2 at, float max, float speed, float width, float mul, StatusApply? status)
    {
        if (World.Root == null) return;
        var p = new PulseRing { _m = m, _max = max, _speed = speed, _width = width, _mul = mul, _status = status, _r = 0.3f };
        World.Root.AddChild(p);
        p.GlobalPosition = World.To3(at, 0.12f);
        _mat ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.95f, 0.75f, 0.55f, 0.45f), CullMode = BaseMaterial3D.CullModeEnum.Disabled, ResourceName = "pulse_ring",
        };
        p._torus = new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.3f + width, Rings = 48, RingSegments = 6 };
        p._mesh = new MeshInstance3D { Mesh = p._torus, MaterialOverride = _mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = new Vector3(1f, 0.06f, 1f) };
        p.AddChild(p._mesh);
        Snd.At("nova", p.GlobalPosition, 0.6f);
    }

    public override void _Process(double delta)
    {
        if (Game.I.Paused) return;
        try
        {
            float dt = (float)delta;
            _r += _speed * dt;
            float inner = Mathf.Max(0.05f, _r - _width * 0.5f), outer = _r + _width * 0.5f;
            _torus.InnerRadius = inner; _torus.OuterRadius = outer;
            if (_m != null && IsInstanceValid(_m))
            {
                var c = new Vector2(GlobalPosition.X, GlobalPosition.Z);
                World.InCircle(Faction.Monster, c, outer, Tmp);
                foreach (var a in Tmp.ToArray())
                {
                    float d = a.Pos2.DistanceTo(c);
                    if (d < inner - a.Radius || !_hit.Add(a)) continue;
                    _m.Strike(a, _mul, _status, false);
                    Fx.Hit(a.Chest, _m.Def.Element, false);
                }
            }
            if (_r >= _max || _m == null || !IsInstanceValid(_m)) QueueFree();
        }
        catch (Exception e) { Log.Error("monster-ai", "pulse ring failed", e); QueueFree(); }
    }
}

/// <summary>Small persistent visuals of the behaviours: aura rings, the reviver's bone heaps, dormant marks, the brass mask.</summary>
public static class MonsterFx
{
    private static readonly Dictionary<Color, StandardMaterial3D> Mats = new();

    private static StandardMaterial3D Mat(Color c, bool unshaded)
    {
        if (Mats.TryGetValue(c, out var m)) return m;
        m = new StandardMaterial3D
        {
            AlbedoColor = c, Roughness = 0.9f, ResourceName = "monfx",
            ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            Transparency = c.A < 0.99f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        };
        Mats[c] = m;
        return m;
    }

    private static MeshInstance3D Part(Node3D parent, Mesh mesh, Color c, Vector3 pos, Vector3 scale, bool unshaded = false)
    {
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = Mat(c, unshaded), Position = pos, Scale = scale, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        parent.AddChild(mi);
        return mi;
    }

    /// <summary>A thin ring on the ground around an actor (aura radius, heal target, call).</summary>
    public static Node3D GroundRing(Actor a, float radius, Color c)
    {
        var n = new Node3D { Name = "aura_ring" };
        a.AddChild(n);
        n.Position = new Vector3(0, 0.05f, 0);
        // the actor's own scale must not grow the ring
        float s = a.Scale.X > 0.01f ? 1f / a.Scale.X : 1f;
        Part(n, new TorusMesh { InnerRadius = Mathf.Max(0.05f, radius - 0.07f), OuterRadius = radius, Rings = 64, RingSegments = 4 }, c, Vector3.Zero, new Vector3(s, 0.05f, s), true);
        return n;
    }

    /// <summary>A small heap of bones where a monster fell (the reviver's target).</summary>
    public static Node3D BoneHeap(Vector2 at, Element el)
    {
        if (World.Root == null) return null;
        var n = new Node3D { Name = "remains" };
        World.Root.AddChild(n);
        n.GlobalPosition = World.To3(at, 0f);
        var bone = new Color(0.66f, 0.62f, 0.53f);
        var rng = new Random((int)(at.X * 31 + at.Y * 17));
        var cyl = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.04f, Height = 0.42f, RadialSegments = 6 };
        for (int i = 0; i < 6; i++)
        {
            var mi = Part(n, cyl, bone, new Vector3((float)rng.NextDouble() * 0.6f - 0.3f, 0.04f, (float)rng.NextDouble() * 0.6f - 0.3f), Vector3.One);
            mi.Rotation = new Vector3(Mathf.Pi / 2f, (float)rng.NextDouble() * Mathf.Tau, 0);
        }
        Part(n, new SphereMesh { Radius = 0.11f, Height = 0.2f, RadialSegments = 8, Rings = 5 }, bone, new Vector3(0.05f, 0.09f, 0.02f), Vector3.One);
        return n;
    }

    /// <summary>The faint shadow under a ceiling ambusher.</summary>
    public static Node3D Shadow(Actor a, float r)
    {
        var n = new Node3D { Name = "dormant_shadow" };
        a.AddChild(n);
        float s = a.Scale.X > 0.01f ? 1f / a.Scale.X : 1f;
        Part(n, new CylinderMesh { TopRadius = r, BottomRadius = r, Height = 0.01f, RadialSegments = 20 }, new Color(0f, 0f, 0f, 0.32f), new Vector3(0, 0.04f, 0), new Vector3(s, 1f, s), true);
        return n;
    }

    /// <summary>The faint mound over a buried ambusher (wet and dark in the mire).</summary>
    public static Node3D Mound(Actor a, float r, bool wet)
    {
        var n = new Node3D { Name = "dormant_mound" };
        a.AddChild(n);
        float s = a.Scale.X > 0.01f ? 1f / a.Scale.X : 1f;
        Part(n, new SphereMesh { Radius = r, Height = r * 0.5f, RadialSegments = 14, Rings = 6 }, wet ? new Color(0.13f, 0.14f, 0.1f) : new Color(0.2f, 0.18f, 0.16f), new Vector3(0, 0f, 0), new Vector3(s, s * 0.45f, s));
        return n;
    }

    /// <summary>The Brass Mask of an empowered ally, at its face.</summary>
    public static Node3D Mask(Monster m)
    {
        var n = new Node3D { Name = "brass_mask" };
        m.AddChild(n);
        float s = m.Scale.X > 0.01f ? 1f / m.Scale.X : 1f;
        float h = m.HeightM * 0.88f;
        Part(n, new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 12, Rings = 6 }, new Color(0.66f, 0.5f, 0.24f), new Vector3(0, h * s, 0.17f), new Vector3(s, s * 1.15f, s * 0.35f));
        return n;
    }
}
