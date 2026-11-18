using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>A moving shot: the hero's bolts, waves and thrown weapons, and monsters' missiles. Travels on the nav grid.</summary>
public partial class Projectile : Node3D
{
    private Player _p;
    private Monster _m;
    private SkillDef _d;
    private float _coef, _mul = 1f, _speed, _left, _radius, _zoneAcc, _height;
    private bool _forceCrit, _wave, _child;
    private Element _el;
    private Vector2 _dir;
    private int _pierce, _bounce, _pierced;
    private readonly HashSet<Actor> _hit = new();
    private StatusApply? _status;
    public float Homing;
    /// <summary>Enemies already struck by another projectile of the same cast; this one passes through them.</summary>
    public HashSet<Actor> Volley;
    public Action<Vector2> OnEnd;
    public Player MinionOwner;
    public float MinionCoef;
    public SkillDef MinionSkill;
    private static readonly List<Actor> Tmp = new();

    private Vector2 Pos2 => new(GlobalPosition.X, GlobalPosition.Z);
    /// <summary>True for the hero's shots (read by the bestiary's shield-bearers and absorbers and by the named layer).</summary>
    public bool FromPlayer => _p != null;

    public static Projectile SpawnPlayer(Player p, SkillDef d, Vector3 origin, Vector2 dir, float coef, float mul, bool forceCrit, bool wave = false, bool child = false)
    {
        var pr = new Projectile
        {
            _p = p, _d = d, _coef = coef, _mul = mul, _forceCrit = forceCrit, _wave = wave, _child = child, _el = d.Element, _dir = dir.Normalized(),
            _speed = wave ? 30f : d.Speed, _left = d.Range > 0 ? d.Range : 18f, _radius = wave ? d.Radius * 0.5f : 0.45f,
            _pierce = wave ? -1 : d.Pierce, _bounce = child ? 0 : d.Bounce, _height = origin.Y,
        };
        World.Root.AddChild(pr);
        pr.GlobalPosition = origin;
        pr.AddChild(Fx.ProjectileVisual(d.Element, wave ? 2 : d.Explode > 0 || d.Coef >= 1.5f ? 1 : 0, wave ? d.Radius : 0.5f, d.Id));
        pr.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Y), 0);
        return pr;
    }

    public static Projectile SpawnMonster(Monster m, Vector3 origin, Vector2 dir, float speed, float range, float mul, Element el, StatusApply? status, float radius = 0.45f)
    {
        var pr = new Projectile { _m = m, _mul = mul, _el = el, _dir = dir.Normalized(), _speed = speed, _left = range, _radius = radius, _status = status, _height = origin.Y };
        World.Root.AddChild(pr);
        pr.GlobalPosition = origin;
        pr.AddChild(Fx.ProjectileVisual(el, radius > 0.7f ? 1 : 0, radius, "monster", true));
        pr.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Y), 0);
        return pr;
    }

    public override void _Process(double delta)
    {
        if (Game.I.Paused) return;
        long t0 = Prof.Now;
        try { Tick((float)delta); }
        catch (Exception e)
        {
            Log.Error("projectile", $"update failed (skill={_d?.Id ?? "-"}, monster={(_m != null && IsInstanceValid(_m) ? _m.Def?.Id : "-")})", e);
            SetProcess(false);
            QueueFree();
        }
        Prof.Shots += Prof.Now - t0;
    }

    private void Tick(float dt)
    {
        if (Homing > 0 && _m != null && World.P != null && !World.P.Dead)
        {
            Vector2 want = (World.P.Pos2 - Pos2).Normalized();
            _dir = _dir.Lerp(want, Mathf.Min(1f, Homing * dt)).Normalized();
            Rotation = new Vector3(0, Mathf.Atan2(_dir.X, _dir.Y), 0);
        }
        float step = _speed * dt;
        Vector2 from = Pos2, to = from + _dir * step;
        var grid = World.Grid;
        if (grid != null && !grid.LineShot(from, to)) { Impact(null, from, true); return; }
        GlobalPosition = new Vector3(to.X, _height, to.Y);
        _left -= step;

        if (_p != null)
        {
            if (Monster.Absorbers.Count > 0 && Monster.AbsorbShot(to)) { End(to, false); return; }     // a Pale Hush mouth swallows it (roster)
            World.InCapsule(Faction.Player, from, to, _radius, Tmp);
            foreach (var t in Tmp.ToArray())
            {
                if (_hit.Contains(t)) continue;
                _hit.Add(t);
                if (Volley != null && !Volley.Add(t)) continue;
                if (!HitPlayerTarget(t)) return;
            }
            if (_wave && _d.Leave != null)
            {
                _zoneAcc += step;
                if (_zoneAcc >= _d.Radius * 1.4f) { _zoneAcc = 0; GroundZone.SpawnPlayer(_p, _d, _d.Leave, to, _d.Leave.Coef, false); }
            }
        }
        else if (_m != null)
        {
            World.InCapsule(MinionOwner != null ? Faction.Player : Faction.Monster, from, to, _radius, Tmp);
            foreach (var t in Tmp.ToArray())
            {
                bool alive = GodotObject.IsInstanceValid(_m);
                if (MinionOwner != null) Combat.PlayerHit(MinionOwner, t, MinionSkill, MinionCoef, _el, new HitOpt { Minion = true, MinionActor = alive ? _m : null, Dir = _dir });
                else if (alive) Combat.MonsterHit(_m, t, _mul, _el, _status);
                Fx.Hit(t.Chest, _el, false);
                End(to, false);
                return;
            }
        }
        if (_left <= 0) End(to, true);
    }

    /// <summary>Returns false when the projectile was consumed.</summary>
    private bool HitPlayerTarget(Actor t)
    {
        Vector2 at = Pos2;
        if (t is Monster mon && mon.BlocksProjectiles) { Fx.Hit(World.To3(at, _height), Element.Physical, false); Snd.At("block", GlobalPosition, 0.6f); End(at, false); return false; }
        if (t is Monster sb && sb.InterceptShot(_dir, at)) { End(at, false); return false; }       // a raised shield (roster: Shieldbearer, Reflect)
        float coef = _coef * (1f + _d.PierceRamp * _pierced);
        if (_d.Explode > 0 && !_wave)
        {
            Explode(at);
            End(at, false);
            return false;
        }
        Combat.PlayerHit(_p, t, _d, coef, _el, new HitOpt { Mul = _mul, ForceCrit = _forceCrit, Dir = _dir });
        if (_d.PipGain > 0 && _hit.Count == 1 && !_child) _p.GainPips(_d.PipGain);
        if (!_wave) { Fx.Hit(t.Chest, _el, false); if (_d.HitSfx != null) Snd.At(_d.HitSfx, t.GlobalPosition, 0.6f); }
        if (_d.SplitCount > 0 && !_child && _d.Explode <= 0 && _pierced == 0) Split(at);
        _pierced++;
        if (_pierce < 0) return true;
        if (_pierce > 0) { _pierce--; return true; }
        if (_bounce > 0)
        {
            var next = World.Nearest(Faction.Player, at, 9f, a => !_hit.Contains(a));
            if (next != null)
            {
                _bounce--; _mul *= _d.BounceMul;
                _dir = (next.Pos2 - at).Normalized(); _left = 12f;
                Rotation = new Vector3(0, Mathf.Atan2(_dir.X, _dir.Y), 0);
                return true;
            }
        }
        End(at, false);
        return false;
    }

    private void Explode(Vector2 at)
    {
        _p.Exec.Burst(at, _d.Explode, _coef, _el, _d, 0, _mul, _forceCrit);
        if (_d.SplitCount > 0 && !_child) Split(at);
    }

    private void Split(Vector2 at)
    {
        float baseAng = Rng.Range(0f, Mathf.Tau);
        for (int i = 0; i < _d.SplitCount; i++)
        {
            Vector2 dir = Vector2.Right.Rotated(baseAng + i * Mathf.Tau / _d.SplitCount);
            if (_d.Explode <= 0) dir = _dir.Rotated(Mathf.DegToRad(-25f + 25f * i));
            var c = SpawnPlayer(_p, _d, World.To3(at + dir * 0.6f, _height), dir, _coef, _mul * _d.SplitMul, false, false, true);
            c._left = 7f;
            foreach (var h in _hit) c._hit.Add(h);
        }
    }

    private void Impact(Actor t, Vector2 at, bool wall)
    {
        if (_p != null && _d.Explode > 0 && !_wave) Explode(at);
        else Fx.Hit(World.To3(at, _height), _el, false);
        End(at, true);
    }

    private void End(Vector2 at, bool expired)
    {
        if (_p != null && expired && !_wave && _d.EndBurst > 0 && !_child) _p.Exec.Burst(at, _d.EndBurstRadius, _d.EndBurst, _el, _d);
        var cb = OnEnd; OnEnd = null;
        cb?.Invoke(at);
        Fx.ReleaseProjectile(this);
    }
}
