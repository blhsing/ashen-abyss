using Godot;
using System;
using MoveK = AshenAbyss.BossMoveKind;

namespace AshenAbyss;

/// <summary>
/// Short-hand for the boss kits of the named encounters (stronghold, dungeon, world and pinnacle bosses all run on
/// <see cref="BossBrain"/>). Each factory returns one <see cref="BossMoveDef"/> of the brain's move library with the
/// usual telegraph and recovery; the kit says how a boss fights between its signature mechanics, which live in its
/// <see cref="NamedScript"/>.
/// </summary>
internal static class NK
{
    public static BossKit Kit(string id, string shortName, Element el, BossMoveDef basic, params BossMoveDef[] moves) => new()
    {
        Id = id, Short = shortName, El = el, Basic = basic, Moves = moves, NoRoar = true, EnrageAt = 300f,
        Haste = new[] { 1f, 1.1f, 1.22f }, Gcd = new[] { 1.7f, 1.35f, 1.0f },
        PhaseLines = Array.Empty<string>(), EnrageLine = "",
    };

    public static BossKit Caster(this BossKit k, float keepMin = 6f, float keepMax = 12f) { k.Ranged = true; k.KeepMin = keepMin; k.KeepMax = keepMax; return k; }
    public static BossKit Still(this BossKit k) { k.Immobile = true; return k; }
    public static BossKit Roars(this BossKit k, string[] lines = null, string[] adds = null) { k.NoRoar = false; k.PhaseLines = lines ?? Array.Empty<string>(); k.WaveAdds = adds; return k; }

    public static BossMoveDef Swing(string id, float radius = 5.2f, float arc = 100f, Element el = Element.Physical, StatusApply? st = null, string anim = "Sword_Regular_A") =>
        new() { Id = id, Kind = MoveK.Cone, Radius = radius, Arc = arc, Wind = 0.7f, Recover = 0.45f, Anim = anim, Contact = 0.4f, El = el, Status = st };

    public static BossMoveDef Bolt(string id, Element el, float speed = 14f, float dmg = 0.9f, StatusApply? st = null) =>
        new() { Id = id, Kind = MoveK.Fan, Speed = speed, Reach = 22f, Wind = 0.6f, Recover = 0.4f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = el, Dmg = dmg, Status = st };

    public static BossMoveDef Cone(string id, float cd, float radius, float arc, float dmg, Element el = Element.Physical, StatusApply? st = null, int phase = 1, int count = 1, float maxRange = 8.5f, string anim = "Sword_Attack") =>
        new() { Id = id, Kind = MoveK.Cone, Cd = cd, Phase = phase, MaxRange = maxRange, Radius = radius, Arc = arc, Count = count, Wind = 0.95f, Interval = 0.7f, Recover = 0.8f, Anim = anim, Contact = 0.55f, Dmg = dmg, El = el, Status = st };

    public static BossMoveDef Slam(string id, float cd, float radius, float dmg, Element el = Element.Physical, int rings = 0, StatusApply? st = null, int phase = 1, float maxRange = 10f) =>
        new() { Id = id, Kind = MoveK.Slam, Cd = cd, Phase = phase, MaxRange = maxRange, Radius = radius, Rings = rings, RingWidth = 3f, RingDmg = dmg * 0.7f, Wind = 1f, Recover = 0.8f, Anim = "Sword_Attack", Contact = 0.55f, Dmg = dmg, El = el, Status = st, HitSfx = "ground_slam" };

    public static BossMoveDef Charge(string id, float cd, float len, float width, float dmg, Element el = Element.Physical, StatusApply? st = null, int phase = 1) =>
        new() { Id = id, Kind = MoveK.Charge, Cd = cd, Phase = phase, MinRange = 5f, MaxRange = len + 4f, Length = len, Width = width, Wind = 0.9f, Recover = 0.9f, Anim = "Sword_Dash", Contact = 0.3f, Dmg = dmg, El = el, Status = st };

    public static BossMoveDef Lines(string id, float cd, int count, float spread, float len, float width, float dmg, Element el, StatusApply? st = null, int phase = 1) =>
        new() { Id = id, Kind = MoveK.Line, Cd = cd, Phase = phase, MinRange = 2f, MaxRange = len, Count = count, Spread = spread, Length = len, Width = width, Wind = 0.9f, Recover = 0.6f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, Dmg = dmg, El = el, Status = st };

    public static BossMoveDef Erupt(string id, float cd, int count, float radius, float dmg, Element el, StatusApply? st = null, float zoneDur = 0f, StatusApply? zoneSt = null, int phase = 1) =>
        new() { Id = id, Kind = MoveK.Eruptions, Cd = cd, Phase = phase, MaxRange = 24f, Count = count, PerPhase = 1f, Interval = 0.55f, Radius = radius, Wind = 0.95f, Recover = 0.5f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, Dmg = dmg, El = el, Status = st, ZoneDur = zoneDur, ZoneDmg = 0.12f, ZoneStatus = zoneSt };

    public static BossMoveDef Fan(string id, float cd, int count, float spread, float speed, float dmg, Element el, StatusApply? st = null, int waves = 1, int phase = 1) =>
        new() { Id = id, Kind = MoveK.Fan, Cd = cd, Phase = phase, MinRange = 3f, MaxRange = 22f, Count = count, PerPhase = 1f, Waves = waves, Spread = spread, Speed = speed, Reach = 22f, Wind = 0.8f, Interval = 0.7f, Recover = 0.5f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, Dmg = dmg, El = el, Status = st };

    public static BossMoveDef Rain(string id, float cd, float dur, float radius, float dmg, Element el, StatusApply? st = null, int phase = 1, float zoneDur = 0f) =>
        new() { Id = id, Kind = MoveK.Rain, Cd = cd, Phase = phase, Duration = dur, Interval = 0.45f, PerPhase = 0.5f, Radius = radius, Wind = 1f, Recover = 0.6f, Anim = "Spell_Simple_Shoot", Dmg = dmg, El = el, Status = st, Meteor = true, ZoneDur = zoneDur, ZoneDmg = 0.12f };

    public static BossMoveDef Homing(string id, float cd, int count, float dmg, Element el, StatusApply? st = null, int phase = 1) =>
        new() { Id = id, Kind = MoveK.Homing, Cd = cd, Phase = phase, Count = count, PerPhase = 1f, Speed = 5.4f, Turn = 2.2f, Reach = 26f, ShotRadius = 0.6f, Wind = 0.8f, Recover = 0.5f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, Dmg = dmg, El = el, Status = st };

    public static BossMoveDef Spiral(string id, float cd, int arms, float dmg, Element el, int phase = 1) =>
        new() { Id = id, Kind = MoveK.Spiral, Cd = cd, Phase = phase, Duration = 3f, Count = arms, Interval = 0.17f, Rotate = 13f, Speed = 7f, Reach = 18f, Wind = 0.9f, Recover = 0.6f, Dmg = dmg, El = el };

    public static BossMoveDef Donut(string id, float cd, float inner, float dmg, Element el, string warn, int phase = 1, float wind = 1.6f) =>
        new() { Id = id, Kind = MoveK.Donut, Cd = cd, Phase = phase, MaxRange = 16f, Inner = inner, Radius = 0f, Wind = wind, Recover = 1f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, Dmg = dmg, El = el, Warn = warn, HitSfx = "nova" };

    public static BossMoveDef Leap(string id, float cd, float radius, float dmg, Element el = Element.Physical, StatusApply? st = null, int phase = 1) =>
        new() { Id = id, Kind = MoveK.Leap, Cd = cd, Phase = phase, MinRange = 6f, MaxRange = 20f, Radius = radius, Wind = 1f, Recover = 0.9f, Dmg = dmg, El = el, Status = st, HitSfx = "leap_land" };

    public static BossMoveDef Summon(string id, float cd, int count, params string[] adds) =>
        new() { Id = id, Kind = MoveK.Summon, Cd = cd, First = cd * 0.5f, Count = count, PerPhase = 1f, Wind = 1f, Recover = 0.5f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, Adds = adds };

    public static BossMoveDef Blink(string id, float cd, float maxRange = 6f) =>
        new() { Id = id, Kind = MoveK.Teleport, Cd = cd, First = 2f, MaxRange = maxRange, Weight = 3f, Recover = 0.3f, El = Element.Shadow, Away = true };

    public static BossMoveDef Pull(string id, float cd, float len, float radius, float dmg, Element el, StatusApply? st = null) =>
        new() { Id = id, Kind = MoveK.PullSlam, Cd = cd, First = 6f, MinRange = 5f, MaxRange = len - 1f, Length = len, Width = 1.8f, Radius = radius, Wind = 1.2f, Recover = 1f, Anim = "Melee_Hook", Contact = 0.35f, Dmg = dmg, El = el, Status = st, HitSfx = "ground_slam" };

    public static BossMoveDef Cross(string id, float cd, int count, float len, float dmg, Element el, bool atHero = false, int phase = 1) =>
        new() { Id = id, Kind = MoveK.Cross, Cd = cd, Phase = phase, MaxRange = 20f, Count = count, WavesPerPhase = 1f, Rotate = 45f, Length = len, Width = 2.2f, AtHero = atHero, Wind = 1f, Interval = 0.75f, Recover = 0.7f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, Dmg = dmg, El = el };

    public static BossMoveDef Prison(string id, float cd, int count, float dmg, Element el, StatusApply? st = null) =>
        new() { Id = id, Kind = MoveK.Prison, Cd = cd, MaxRange = 22f, Count = count, Length = 5f, Radius = 1.6f, Wind = 1f, Interval = 0.7f, Recover = 0.6f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, Dmg = dmg, El = el, Status = st };

    public static BossMoveDef At(this BossMoveDef d, int phase) { d.Phase = phase; return d; }
    public static BossMoveDef First(this BossMoveDef d, float first) { d.First = first; return d; }
}
