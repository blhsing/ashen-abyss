using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// A persistent area on the ground: burning ground, pools, storms, consecration, hostile hazards and armed traps.
/// Player-owned zones damage monsters; monster-owned zones damage the hero and minions.
/// </summary>
public partial class GroundZone : Node3D
{
    private Player _p;
    private Monster _m;
    private SkillDef _d;
    private ZoneSpec _z;
    private float _coef, _monMul, _left, _tick, _armIn;
    private bool _trap, _statusFromSkill;
    private StatusApply? _monStatus;
    private Action<Vector2> _trigger;
    public Action<Vector2> OnEnd;
    public float PipChance;
    public bool Done { get; private set; }
    private static readonly List<Actor> Tmp = new();
    private Vector2 Pos2 => new(GlobalPosition.X, GlobalPosition.Z);

    public static GroundZone SpawnPlayer(Player p, SkillDef d, ZoneSpec z, Vector2 at, float coefPerTick, bool statusFromSkill)
    {
        var g = new GroundZone { _p = p, _d = d, _z = z, _coef = coefPerTick, _left = z.Dur, _statusFromSkill = statusFromSkill };
        World.Root.AddChild(g);
        g.GlobalPosition = World.To3(at, 0.06f);
        g.AddChild(Fx.ZoneVisual(z.Radius, z.El, false, z.Dur));
        return g;
    }

    public static GroundZone SpawnMonster(Monster m, Vector2 at, float radius, float dur, float mulPerTick, Element el, StatusApply? status = null, float tick = 0.5f)
    {
        var g = new GroundZone { _m = m, _z = new ZoneSpec { Radius = radius, Dur = dur, Tick = tick, El = el }, _monMul = mulPerTick, _left = dur, _monStatus = status };
        World.Root.AddChild(g);
        g.GlobalPosition = World.To3(at, 0.06f);
        g.AddChild(Fx.ZoneVisual(radius, el, true, dur));
        return g;
    }

    public void MakeTrap(float armTime, Action<Vector2> trigger)
    {
        _trap = true; _armIn = armTime; _trigger = trigger;
        foreach (var c in GetChildren()) c.QueueFree();
        AddChild(Fx.TrapVisual(_z.El));
    }

    public void Remove() { Done = true; QueueFree(); }

    public override void _Process(double delta)
    {
        if (Game.I.Paused || Done) return;
        long t0 = Prof.Now;
        try { Tick((float)delta); }
        catch (Exception e)
        {
            Log.Error("zone", $"update failed (skill={_d?.Id ?? "-"}, trap={_trap})", e);
            Done = true;
            QueueFree();
        }
        Prof.Shots += Prof.Now - t0;
    }

    private void Tick(float dt)
    {
        _left -= dt;
        if (_trap)
        {
            if (_armIn > 0) { _armIn -= dt; return; }
            if (World.Nearest(Faction.Player, Pos2, 1.9f) != null)
            {
                Snd.At("trap_trigger", GlobalPosition);
                _trigger?.Invoke(Pos2);
                Remove();
            }
            else if (_left <= 0) Remove();
            return;
        }
        if (_z.Follow && _p != null) GlobalPosition = World.To3(_p.Pos2, 0.06f);
        _tick += dt;
        if (_tick >= _z.Tick)
        {
            _tick -= _z.Tick;
            if (_p != null)
            {
                World.InCircle(Faction.Player, Pos2, _z.Radius, Tmp);
                double now = Game.I.Time;
                foreach (var t in Tmp.ToArray())
                {
                    // overlapping pools of one skill do not multiply: an enemy takes one tick of that skill per interval
                    if (_d != null && !_trap)
                    {
                        t.ZoneTicks ??= new Dictionary<string, double>();
                        if (t.ZoneTicks.TryGetValue(_d.Id, out double last) && now - last < _z.Tick * 0.8f) continue;
                        t.ZoneTicks[_d.Id] = now;
                    }
                    Combat.PlayerHit(_p, t, _d, _coef, _z.El, new HitOpt { NoCrit = _z.Tick < 0.45f && _d == null, NoOnHit = !_statusFromSkill });
                    if (!_statusFromSkill && _z.OnHit.Count > 0) Combat.ApplyStatuses(_p, t, _z.OnHit, _z.El);
                }
                if (PipChance > 0 && Tmp.Count > 0 && Rng.Chance(PipChance)) _p.GainPips(1);
                if (_z.HealPct > 0 && _p.Pos2.DistanceTo(Pos2) <= _z.Radius) _p.Heal(_p.MaxLife * _z.HealPct / 100f);
                if (_d != null && _d.Has("minion_haste"))
                    foreach (var m in World.Minions) if (m.Pos2.DistanceTo(Pos2) <= _z.Radius) m.Enrage(1f);
            }
            else if (_m != null)
            {
                if (GodotObject.IsInstanceValid(_m)) Combat.MonsterArea(_m, Pos2, _z.Radius, _monMul, _z.El, _monStatus);
                else { World.InCircle(Faction.Monster, Pos2, _z.Radius, Tmp); }
            }
        }
        if (_left <= 0)
        {
            Done = true;
            OnEnd?.Invoke(Pos2);
            QueueFree();
        }
    }
}
