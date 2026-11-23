using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// Runs skills from their declarative definitions: one routine per SkillKind plus the shared riders
/// (statuses, zones, bursts, barriers, summons). Also hosts the timers those skills leave behind.
/// </summary>
public sealed class SkillExec
{
    private readonly Player P;
    private Hero H => P.Hero;
    private static readonly List<Actor> Tmp = new();
    private readonly List<(float t, Action a)> _later = new();
    private readonly List<GroundZone> _traps = new();
    private sealed class Running { public SkillDef D; public float Pulse, Rain; public string Marker; }
    private readonly List<Running> _running = new();
    private int _killDepth, _raiseCount;

    public SkillExec(Player p) { P = p; }

    private int Rank(SkillDef d) => Math.Max(1, H.Rank(d.Id));
    private float Coef(SkillDef d) => d.Dmg(Rank(d));
    /// <summary>[golem] A skill's damage coefficient at the hero's rank (MinionSystem).</summary>
    public float CoefOf(SkillDef d) => Coef(d);
    /// <summary>[golem] Raises a persistent minion of a skill (MinionSystem: book kinds, corpses, area changes).</summary>
    public Monster Raise(string id, SkillDef d, Vector2 near, float coef, string tag = null) => SpawnMinion(id, d, near, coef, 0f, tag);
    private Vector3 Pos3 => P.GlobalPosition;

    /// <summary>The school a skill's ground marks are drawn in: its element; the lamp knight's ember-light is the holy mark.</summary>
    private static Sigil Mark(SkillDef d) => d.Element == Element.Fire && d.Id != null && d.Id.StartsWith("lk_") ? Sigil.Holy : d.Element;

    public void After(float t, Action a) { if (t <= 0) a(); else _later.Add((t, a)); }

    public void Tick(float dt)
    {
        for (int i = _later.Count - 1; i >= 0; i--)
        {
            var (t, a) = _later[i];
            t -= dt;
            if (t <= 0) { _later.RemoveAt(i); Log.Guard("skill", a); }
            else _later[i] = (t, a);
        }
        for (int i = _running.Count - 1; i >= 0; i--)
        {
            var r = _running[i];
            if (!H.Buffs.Any(b => b.Def.Id == r.Marker)) { _running.RemoveAt(i); continue; }
            var d = r.D;
            if (d.PulseCoef > 0)
            {
                r.Pulse += dt;
                if (r.Pulse >= d.Tick) { r.Pulse = 0; Burst(P.Pos2, d.PulseRadius, Coef(d) * d.PulseCoef, d.Element, d, 0, 1, false, true); }
            }
            if (d.Rain > 0)
            {
                r.Rain += dt;
                if (r.Rain >= d.Rain)
                {
                    r.Rain = 0;
                    var t = RandomEnemyNear(P.Pos2, 14f);
                    Vector2 c = t != null ? t.Pos2 + new Vector2(Rng.Range(-1f, 1f), Rng.Range(-1f, 1f)) : P.Pos2 + new Vector2(Rng.Range(-9f, 9f), Rng.Range(-9f, 9f));
                    Fx.Meteor(c, 0.55f, d.Element, d.RainRadius);
                    Burst(c, d.RainRadius, Coef(d) * d.RainCoef, d.Element, d, 0.55f);
                }
            }
        }
        for (int i = World.Remnants.Count - 1; i >= 0; i--)
        {
            var r = World.Remnants[i];
            r.Left -= dt;
            if (r.Left <= 0) { if (GodotObject.IsInstanceValid(r.Fx)) r.Fx.QueueFree(); World.Remnants.RemoveAt(i); }
        }
    }

    private static Actor RandomEnemyNear(Vector2 p, float r)
    {
        World.InCircle(Faction.Player, p, r, Tmp);
        return Tmp.Count == 0 ? null : Tmp[Rng.R.Next(Tmp.Count)];
    }

    // ───────────────────────── entry point ─────────────────────────

    public void Fire(SkillDef d, Vector2 point, Actor target, float mul, bool forceCrit)
    {
        // the target may have been freed between the wind-up and the contact frame (area change, a boss fight reset, a despawn)
        if (target != null && (!GodotObject.IsInstanceValid(target) || target.Dead)) target = null;
        Vector2 pos = P.Pos2;
        Vector2 dir = (target != null ? target.Pos2 : point) - pos;
        dir = dir.LengthSquared() < 0.01f ? P.Forward : dir.Normalized();
        Vector2 aim = pos + dir * Mathf.Min(pos.DistanceTo(point), d.Range > 0 ? d.Range : 999f);
        float coef = Coef(d);
        // dual-wield: each attack swings the other hand (multi-hit swings alternate hit by hit); one weapon: always the main hand
        int hand0 = d.Coef > 0 ? H.NextStrike() : H.StrikeHand;

        switch (d.Kind)
        {
            case SkillKind.Melee:
                for (int i = 0; i < d.Hits; i++)
                {
                    int n = i, hand = (hand0 + i) % 2;
                    After(i * d.HitInterval / H.AttackSpeedMul, () => { H.StrikeHand = H.DualWielding ? hand : 0; MeleeHit(d, P.Forward, coef, mul, forceCrit, n == 0); });
                }
                break;
            case SkillKind.Nova:
                for (int i = 0; i < d.Hits; i++)
                    After(i * 0.16f, () => { Burst(P.Pos2, d.Radius, coef, d.Element, d, 0, mul, forceCrit); if (d.Has("spin")) Fx.Slash(P, d.Radius, 360, d.Element); });
                if (d.Leave != null) SpawnZone(pos, d.Leave, d);
                break;
            case SkillKind.Proj: FireProjectiles(d, dir, coef, mul, forceCrit); break;
            case SkillKind.Wave: FireWave(d, dir, coef, mul, forceCrit, false); break;
            case SkillKind.Chain:
            {
                var first = target != null && !target.Dead ? target : World.Nearest(Faction.Player, aim, 5f) ?? World.Nearest(Faction.Player, pos + dir * 5f, 7f);
                if (first != null && first.Pos2.DistanceTo(pos) <= d.Range + 2f)
                {
                    Fx.Beam(P.ActiveModel.BonePos("hand_r"), first.Chest, d.Element);
                    ChainFrom(first, coef * mul, d.Count, d.Element, d);
                    if (d.PipGain > 0) P.GainPips(d.PipGain);
                }
                else Fx.Beam(P.ActiveModel.BonePos("hand_r"), World.To3(pos + dir * 6f, 1.2f), d.Element);
                break;
            }
            case SkillKind.Ground: FireGround(d, aim, coef, mul, forceCrit); break;
            case SkillKind.Zone:
            {
                var z = new ZoneSpec { Coef = d.Has("per_second") ? d.Tick : 1f, Dur = d.Duration, Radius = d.Radius, Tick = d.Tick, El = d.Element, Follow = d.Has("follow") };
                z.OnHit.AddRange(d.OnHit);
                // "per second" zones tick every d.Tick seconds: each tick carries that share of the listed coefficient
                var zone = GroundZone.SpawnPlayer(P, d, z, aim, coef * z.Coef * mul, true);
                zone.PipChance = d.PipChance;
                if (d.EndBurst > 0) zone.OnEnd = c => { Fx.Meteor(c, 0.3f, d.Element, d.EndBurstRadius); Burst(c, d.EndBurstRadius, d.EndBurst, d.Element, d, 0.3f); };
                Fx.CastGlow(P, d.Element);
                break;
            }
            case SkillKind.Dash: FireDash(d, dir, coef, mul); break;
            case SkillKind.Leap: FireLeap(d, point, coef, mul); break;
            case SkillKind.Blink: FireBlink(d, point, coef, mul); break;
            case SkillKind.Buff: FireBuff(d, coef); break;
            case SkillKind.Summon: FireSummon(d, aim, coef); break;
            case SkillKind.Trap: FireTrap(d, aim, coef, mul); break;
            case SkillKind.Command: FireCommand(d, aim, coef, mul); break;
        }

        // riders shared by every kind
        if (d.Kind != SkillKind.Buff)
        {
            foreach (var b in d.Buffs) AddBuff(b, d);
            if (d.Barrier > 0) { P.AddBarrier(P.MaxLife * d.Barrier * P.BarrierMul, d.BarrierDur); Fx.Ring(Pos3, 1.6f, new Color(0.7f, 0.85f, 1f), 0.4f, Sigil.Arcane); }
            if (d.HealPct > 0 && !d.Has("remnants")) P.Heal(P.MaxLife * d.HealPct / 100f);
            if (d.CastBurst > 0 && d.Kind == SkillKind.Summon) Burst(pos, d.CastBurstRadius, d.CastBurst, d.Element, d);
        }
        if (d.Id == "hu_dash" && H.HasAsp("shadow_volley")) P.VolleyLeft = 4f;
    }

    private void AddBuff(BuffDef b, SkillDef d)
    {
        float dur = b.Aura ? 1e9f : b.Dur > 0 ? b.Dur : d.Duration;
        if (b.Aura) H.RemoveBuffs(x => x.Def.Aura && !d.Buffs.Any(y => y.Id == x.Def.Id));
        H.AddBuff(b, dur);
        if (b.Stealth) P.BeginStealth(dur);
    }

    // ───────────────────────── melee / bursts ─────────────────────────

    private void MeleeHit(SkillDef d, Vector2 dir, float coef, float mul, bool forceCrit, bool first)
    {
        World.InCone(Faction.Player, P.Pos2, dir, d.Radius, d.Arc, Tmp);
        Fx.Slash(P, d.Radius, d.Arc, d.Element);
        if (Tmp.Count == 0) return;
        var list = Tmp.ToArray();
        foreach (var t in list)
            Combat.PlayerHit(P, t, d, coef, d.Element, new HitOpt { Mul = mul, ForceCrit = forceCrit, Dir = (t.Pos2 - P.Pos2).Normalized() });
        if (first && d.PipGain > 0) P.GainPips(d.PipGain);
        Snd.At(d.HitSfx ?? "hit_flesh", list[0].GlobalPosition, 0.8f);
        Fx.Hit(list[0].Chest, d.Element, false);
        if (d.Coef >= 1.2f) Fx.Shake(0.22f, 0.15f);
        Fx.HitStop(d.Coef >= 1.2f ? 0.07f : 0.035f);
    }

    /// <summary>Area damage from the hero around a point, optionally after a delay. Returns nothing; kills are reported through OnKill.</summary>
    public void Burst(Vector2 c, float r, float coef, Element el, SkillDef s, float delay = 0, float mul = 1, bool forceCrit = false, bool quiet = false)
    {
        After(delay, () =>
        {
            World.InCircle(Faction.Player, c, r, Tmp);
            if (!quiet) { Fx.Burst(World.To3(c, 0.2f), el, r, Mathf.Clamp(coef, 0.4f, 2.5f)); if (s?.HitSfx != null) Snd.At(s.HitSfx, World.To3(c), 0.9f); }
            if (Tmp.Count == 0) return;
            foreach (var t in Tmp.ToArray())
            {
                var dir = t.Pos2 - c;
                if (coef > 0) Combat.PlayerHit(P, t, s, coef, el, new HitOpt { Mul = mul, ForceCrit = forceCrit, Dir = dir.LengthSquared() > 0.01f ? dir.Normalized() : Vector2.Zero });
                else if (s != null) Combat.ApplyStatuses(P, t, s.OnHit, el);
            }
            if (s != null && s.PipGain > 0 && s.Kind != SkillKind.Buff) P.GainPips(s.PipGain);
            if (!quiet && coef >= 1.5f) Fx.Shake(Mathf.Clamp(coef * 0.15f, 0.2f, 0.6f), 0.25f);
        });
    }

    public void ChainFrom(Actor first, float coef, int jumps, Element el, SkillDef s)
    {
        var hitSet = new HashSet<Actor>();
        Actor cur = first;
        float c = coef;
        for (int i = 0; i < jumps && cur != null; i++)
        {
            var target = cur;
            float cc = c;
            float delay = i * 0.07f;
            hitSet.Add(target);
            After(delay, () =>
            {
                if (target.Dead || !GodotObject.IsInstanceValid(target)) return;
                Combat.PlayerHit(P, target, s, cc, el);
                Fx.Hit(target.Chest, el, false);
                if (s != null && H.HasUnique("u_stargazer") && s.Id == "as_chain" && Rng.Chance(0.25f)) { Fx.Meteor(target.Pos2, 0.2f, Element.Lightning, 2.5f); Burst(target.Pos2, 2.5f, 0.8f, Element.Lightning, null, 0.2f); }
            });
            var next = World.Nearest(Faction.Player, target.Pos2, 8f, a => !hitSet.Contains(a));
            if (next != null) { var a = target; var b = next; After(delay + 0.05f, () => { if (GodotObject.IsInstanceValid(a) && GodotObject.IsInstanceValid(b)) Fx.Beam(a.Chest, b.Chest, el); }); }
            cur = next;
            if (s != null && s.ChainRamp > 0) c *= 1f + s.ChainRamp;
        }
        Snd.At(s?.HitSfx ?? "lightning_zap", first.GlobalPosition, 0.7f);
    }

    // ───────────────────────── projectiles / waves ─────────────────────────

    private void FireProjectiles(SkillDef d, Vector2 dir, float coef, float mul, bool forceCrit)
    {
        int count = d.Count + (int)H.BuffMax(b => b.ExtraProj);
        if (d.ExtraChance > 0 && Rng.Chance(d.ExtraChance)) count++;
        float volleyMul = 0;
        if (d.Id == "hu_shot" && P.VolleyLeft > 0 && H.HasAsp("shadow_volley")) { volleyMul = H.Asp("shadow_volley") / 100f; }
        Vector3 origin = P.ActiveModel.BonePos("hand_r");
        origin.Y = Mathf.Clamp(origin.Y, 0.9f, 1.6f);
        float spread = count > 1 ? Mathf.Max(d.Spread, 8f) : 0f;
        // one volley, one hit per enemy: extra projectiles widen the attack, they do not stack on a target at point-blank range
        var volley = count > 1 || volleyMul > 0 ? new HashSet<Actor>() : null;
        for (int i = 0; i < count; i++)
        {
            float ang = count == 1 ? 0 : Mathf.DegToRad(-spread * (count - 1) / 2f + spread * i);
            Projectile.SpawnPlayer(P, d, origin, dir.Rotated(ang), coef, mul, forceCrit).Volley = volley;
        }
        if (volleyMul > 0)
            for (int i = -1; i <= 1; i += 2)
                Projectile.SpawnPlayer(P, d, origin, dir.Rotated(Mathf.DegToRad(9f * i)), coef, mul * volleyMul, false).Volley = volley;
        Fx.CastGlow(P, d.Element);
    }

    private void FireWave(SkillDef d, Vector2 dir, float coef, float mul, bool forceCrit, bool isReturn)
    {
        Vector3 origin = World.To3(P.Pos2 + dir * 1.0f, 0.6f);
        var pr = Projectile.SpawnPlayer(P, d, origin, dir, coef, mul, forceCrit, wave: true);
        pr.OnEnd = end =>
        {
            if (d.EndBurst > 0) Burst(end, d.EndBurstRadius, d.EndBurst, d.Element, d);
            if (!isReturn && d.Id == "eb_cleave" && H.HasUnique("u_regret"))
            {
                var back = Projectile.SpawnPlayer(P, d, World.To3(end, 0.6f), -dir, coef, mul * 0.6f, false, wave: true);
                back.OnEnd = null;
            }
        };
        Fx.Shake(0.25f, 0.2f);
    }

    // ───────────────────────── ground targeted ─────────────────────────

    private void FireGround(SkillDef d, Vector2 aim, float coef, float mul, bool forceCrit)
    {
        if (d.Has("remnants")) { DetonateRemnants(d, aim, coef, mul); return; }
        float r = d.Radius;
        if (d.Delay > 0.2f) { Fx.Telegraph(aim, r, d.Delay, Palette.Element(d.Element), true, Mark(d)); Fx.Meteor(aim, d.Delay, d.Element, r); }
        else if (coef <= 0) Fx.Ring(World.To3(aim, 0.15f), r, Palette.Element(d.Element), 0.6f, Mark(d));
        Burst(aim, r, coef, d.Element, d, d.Delay, mul, forceCrit);
        if (d.Leave != null) After(d.Delay, () => SpawnZone(aim, d.Leave, d));
        for (int i = 0; i < d.ExtraStrikes; i++)
        {
            bool twin = d.Has("twin") && i == d.ExtraStrikes - 1;
            float em = twin ? H.Asp("twin_meteor") / 100f : d.ExtraMul;
            Vector2 c = d.Has("same_spot") ? aim : aim + new Vector2(Rng.Range(-1f, 1f), Rng.Range(-1f, 1f)).Normalized() * Rng.Range(r * 0.7f, r * 1.3f);
            float delay = d.Delay + 0.25f + i * 0.2f;
            float rr = twin ? r : r * 0.7f;
            After(delay - 0.5f, () => { Fx.Meteor(c, 0.5f, d.Element, rr); });
            Burst(c, rr, coef * em, d.Element, d, delay, mul);
        }
        Fx.CastGlow(P, d.Element);
    }

    private void DetonateRemnants(SkillDef d, Vector2 aim, float coef, float mul)
    {
        var near = World.Remnants.Where(r => r.Pos.DistanceTo(aim) <= 12f).ToList();
        if (near.Count == 0) { Burst(aim, d.Radius * 0.8f, coef * 0.35f, d.Element, d, 0, mul); return; }
        int i = 0;
        foreach (var r in near)
        {
            World.Remnants.Remove(r);
            if (GodotObject.IsInstanceValid(r.Fx)) r.Fx.QueueFree();
            var c = r.Pos;
            float delay = i++ * 0.06f;
            Burst(c, d.Radius, coef, d.Element, d, delay, mul);
            if (d.HealPct > 0) P.Heal(P.MaxLife * d.HealPct / 100f);
            if (d.Leave != null) After(delay, () => SpawnZone(c, d.Leave, d));
        }
    }

    /// <summary>[golem] Chance that a Soulbinder kill leaves a remnant (殘魂): one resource for 魂爆 and the army (MinionSystem).</summary>
    public const float RemnantChance = 0.65f;

    public void SpawnRemnant(Vector2 p)
    {
        if (World.Remnants.Count >= 24) return;
        World.Remnants.Add(new Remnant { Pos = p, Fx = Fx.RemnantWisp(p) });
    }

    private GroundZone SpawnZone(Vector2 c, ZoneSpec z, SkillDef d)
    {
        float coef = Coef(d) * z.Coef;
        if (d.Kind != SkillKind.Zone && z.Coef < 1f && !d.Has("per_second")) coef = z.Coef;     // "Leave" zones carry their own per-tick coefficient
        if (d.Has("leave_at_self")) coef = Coef(d) * z.Tick;
        return GroundZone.SpawnPlayer(P, d, z, c, coef, false);
    }

    // ───────────────────────── movement skills ─────────────────────────

    private void FireDash(SkillDef d, Vector2 dir, float coef, float mul)
    {
        Vector2 from = P.Pos2;
        Vector2 to = World.Grid?.ClampLine(from, from + dir * d.Range, false) ?? from + dir * d.Range;
        var hit = new HashSet<Actor>();
        float dur = Mathf.Max(0.12f, from.DistanceTo(to) / 34f);
        Fx.DashTrail(P, d.Element, dur);
        P.StartDash(to, dur, 0f, true, (a, b) =>
        {
            if (coef <= 0) return;
            World.InCapsule(Faction.Player, a, b, d.Radius, Tmp);
            foreach (var t in Tmp.ToArray())
                if (hit.Add(t)) { Combat.PlayerHit(P, t, d, coef, d.Element, new HitOpt { Mul = mul, Dir = dir }); Fx.Hit(t.Chest, d.Element, false); }
        }, () =>
        {
            if (hit.Count > 0) Snd.At(d.HitSfx ?? "hit_flesh", Pos3, 0.8f);
            if (d.EndBurst > 0) Burst(P.Pos2, d.EndBurstRadius, d.EndBurst, d.Element, d);
            if (d.Leave != null) for (int i = 0; i <= 2; i++) SpawnZone(from.Lerp(to, i / 2f), d.Leave, d);
        });
        if (d.OriginBurst > 0) { Fx.Afterimage(from, d.Element, d.OriginDelay); Burst(from, 3.5f, d.OriginBurst, d.Element, d, d.OriginDelay); }
    }

    private void FireLeap(SkillDef d, Vector2 point, float coef, float mul)
    {
        Vector2 from = P.Pos2;
        Vector2 dir = point - from;
        float dist = Mathf.Min(dir.Length(), d.Range);
        dir = dir.LengthSquared() < 0.01f ? P.Forward : dir.Normalized();
        Vector2 to = World.Grid?.ClampLine(from, from + dir * dist, true) ?? from + dir * dist;
        float dur = 0.32f + from.DistanceTo(to) * 0.018f;
        P.ActiveModel.Play("NinjaJump_Idle_Loop", 1f, 0.1f);
        Fx.Telegraph(to, d.Radius, dur, Palette.Element(d.Element), true, Mark(d));
        P.StartDash(to, dur, 2.6f, true, null, () =>
        {
            P.ActiveModel.Play("NinjaJump_Land", 1.6f, 0.05f, true);
            Burst(P.Pos2, d.Radius, coef, d.Element, d, 0, mul);
            Fx.Shake(0.45f, 0.3f);
            Snd.At(d.HitSfx ?? "leap_land", Pos3);
            if (d.Leave != null) SpawnZone(P.Pos2, d.Leave, d);
        });
    }

    private void FireBlink(SkillDef d, Vector2 point, float coef, float mul)
    {
        Vector2 from = P.Pos2;
        Vector2 dir = point - from;
        float dist = Mathf.Min(dir.Length(), d.Range);
        dir = dir.LengthSquared() < 0.01f ? P.Forward : dir.Normalized();
        Vector2 want = from + dir * dist;
        var grid = World.Grid;
        Vector2 to = grid == null || grid.WalkAt(want) ? want : grid.ClampLine(from, want, true);
        Fx.Teleport(World.To3(from, 1f), d.Element);
        if (!d.Has("no_origin_nova") && coef > 0) Burst(from, d.Radius, coef, d.Element, d, 0, mul);
        if (d.OriginBurst > 0) { Fx.Afterimage(from, d.Element, d.OriginDelay); Burst(from, d.Radius, coef * d.OriginBurst, d.Element, d, d.OriginDelay, mul); }
        P.Teleport(to);
        Fx.Teleport(World.To3(to, 1f), d.Element);
        if (d.Has("fiend_follow"))
            foreach (var m in World.Minions.Where(m => m.Def.Id.StartsWith("pact"))) { m.Pos2 = grid?.RandomWalkNear(to, 1.5f, 3f) ?? to; m.Heal(m.MaxLife * 0.3f); }
    }

    // ───────────────────────── buffs ─────────────────────────

    private void FireBuff(SkillDef d, float coef)
    {
        string marker = d.Id + "_t";
        if (d.Duration > 0)
        {
            H.AddBuff(new BuffDef { Id = marker, Name = d.Name }, d.Duration);
            _running.RemoveAll(r => r.Marker == marker);
            if (d.PulseCoef > 0 || d.Rain > 0) _running.Add(new Running { D = d, Marker = marker });
        }
        foreach (var b in d.Buffs) AddBuff(b, d);
        if (d.Barrier > 0) P.AddBarrier(P.MaxLife * d.Barrier * P.BarrierMul, d.BarrierDur > 0 ? Mathf.Max(d.BarrierDur, d.Duration) : d.Duration);
        if (d.HealPct > 0) { P.Heal(P.MaxLife * d.HealPct / 100f); Fx.Heal(P); }
        if (d.PipGain > 0) P.GainPips(d.PipGain);
        if (d.CastBurst > 0) Burst(P.Pos2, d.CastBurstRadius, coef > 0 ? coef * d.CastBurst : d.CastBurst, d.Element, d);
        else if (d.OnHit.Count > 0)
        {
            World.InCircle(Faction.Player, P.Pos2, d.CastBurstRadius, Tmp);
            foreach (var t in Tmp.ToArray()) Combat.ApplyStatuses(P, t, d.OnHit, d.Element);
            Fx.Ring(Pos3 + Vector3.Up * 0.2f, d.CastBurstRadius, Palette.Element(d.Element), 0.5f, Mark(d));
        }
        if (d.Leave != null) SpawnZone(P.Pos2, d.Leave, d);
        for (int i = 0; i < d.SpawnRemnants; i++) SpawnRemnant(P.Pos2 + Vector2.Right.Rotated(i * Mathf.Tau / d.SpawnRemnants) * 2.5f);
        if (d.ResetAllCds) foreach (var kv in P.Cds) if (kv.Key != d.Id) { kv.Value.Charges = kv.Value.MaxCharges; kv.Value.Left = 0; }
        if (d.Has("fiend_taunt"))
            foreach (var m in World.Minions.Where(m => m.Def.Id.StartsWith("pact")))
            { World.InCircle(Faction.Player, m.Pos2, 9f, Tmp); foreach (var t in Tmp) t.ApplyStatus(St.Taunt(5), m); }
        if (d.Has("giant")) P.ApplyFormScale();
        Fx.BuffAura(P, d.Element, Mathf.Max(d.Duration, 1f), d.Cat == SkillCat.Ultimate);
    }

    public void OnBuffEnd(ActiveBuff b)
    {
        string id = b.Def.Id;
        if (!id.EndsWith("_t")) return;
        var d = H.Eff(id[..^2]);
        if (d == null) return;
        if (d.EndBurst > 0 && d.Kind == SkillKind.Buff) Burst(P.Pos2, d.EndBurstRadius, d.EndBurst, d.Element, d);
        if (d.EndHeal > 0) { P.Heal(P.MaxLife * d.EndHeal / 100f); Fx.Heal(P); }
        if (d.Has("giant")) P.ApplyFormScale();
        Snd.At("buff_expire", Pos3, 0.5f);
    }

    public void OnBarrierEnd() { }

    // ───────────────────────── channel ─────────────────────────

    public void ChannelStart(SkillDef d) => Snd.Loop("chan", d.Sfx, P);

    public void ChannelTick(SkillDef d, float time)
    {
        float coef = Coef(d);
        if (d.Has("ramp")) coef *= 1f + Mathf.Min(0.5f, 0.1f * Mathf.Floor(time));
        World.InCircle(Faction.Player, P.Pos2, d.Radius, Tmp);
        Fx.Slash(P, d.Radius, 360, d.Element);
        if (Tmp.Count == 0) return;
        foreach (var t in Tmp.ToArray()) Combat.PlayerHit(P, t, d, coef, d.Element, new HitOpt { Dir = (t.Pos2 - P.Pos2).Normalized() });
        Snd.At(d.HitSfx ?? "hit_flesh", Pos3, 0.5f);
    }

    public void ChannelEnd(SkillDef d, float time)
    {
        Snd.StopLoop("chan");
        if (d.EndBurst > 0 && time > 0.3f) Burst(P.Pos2, d.EndBurstRadius, d.EndBurst, Element.Fire, d);
    }

    // ───────────────────────── summons / traps / commands ─────────────────────────

    private string PactFiendId() => H.D.Spec switch { "pb_brute" => "pact_brute", "pb_imps" => "pact_imp", "pb_wraith" => "pact_wraith", _ => "pact_fiend" };

    private void FireSummon(SkillDef d, Vector2 aim, float coef)
    {
        if (d.Id == "sb_raise" && MinionSystem.I != null && MinionSystem.I.RaiseCast(this, d, aim, coef)) return;     // [golem] 亡者名冊: warriors and mages
        string id = d.Summon == "pact_fiend" ? PactFiendId() : d.Summon;
        int max = d.SummonMax * (id == "pact_imp" ? 3 : 1);
        var mine = World.Minions.Where(m => m.SummonSkill == d.Id && !m.Dead).ToList();
        if (mine.Count >= max)
        {
            foreach (var m in World.Minions) { m.Heal(m.MaxLife); Fx.Heal(m); }
            H.AddBuff(new BuffDef { Id = "minion_frenzy", Name = "亡者之怒", Stat = Stat.MinionDmg, Value = 30 }, 6f);
            foreach (var m in mine) m.Enrage(6f);
            return;
        }
        int toSpawn = id == "pact_imp" ? max - mine.Count : 1;
        for (int i = 0; i < toSpawn; i++)
        {
            string sid = id;
            if (d.Has("thrower_alt") && (++_raiseCount % 2 == 0)) sid = "minion_thrower";
            SpawnMinion(sid, d, aim, coef, d.SummonDur);
        }
        if (d.Has("imps2")) for (int i = 0; i < 2; i++) if (World.Minions.Count(m => m.SummonSkill == d.Id + "#imp") < 2) SpawnMinion("pact_imp", d, aim, coef * 0.6f, 0, d.Id + "#imp");
        if (d.Has("rally")) foreach (var m in World.Minions) { m.Heal(m.MaxLife); Fx.Heal(m); }
        if (d.Has("rally")) MinionSystem.I?.ReformNow("re-formed by the colossus's 號令");          // [golem] the colossus's rally also restores the golem
    }

    private Monster SpawnMinion(string id, SkillDef d, Vector2 near, float coef, float life, string tag = null)
    {
        var def = MinionDefs.Resolve(id);          // [golem] the book's kinds, then the bestiary
        if (def == null) { Log.Warn("skill", $"unknown summon '{id}'"); return null; }
        Vector2 pos = World.Grid?.RandomWalkNear(near, 0.5f, 2.5f) ?? near;
        var m = Monster.Spawn(def, H.D.Level, MonTier.Minion, pos, Faction.Player);
        m.SummonSkill = tag ?? d.Id;
        m.MinionCoef = coef;
        m.MinionSkill = d;
        m.MaxLife = P.MaxLife * 0.45f * def.Hp * d.MinionHpMul; m.Life = m.MaxLife;
        m.LifeTime = life;
        m.Cleave = d.Has("cleave") || def.AI == MonAI.Brute;
        m.Taunter = d.Has("taunt") || id == "pact_brute";
        if (d.EndBurst > 0) m.OnExpire = c => Burst(c, d.EndBurstRadius, d.EndBurst, d.Element, d);
        Log.Guard("minions", () => MinionSystem.OnSpawned(m, d));          // [golem] book kinds, scaling, the count cap
        Fx.Burst(World.To3(pos, 0.3f), id.StartsWith("pact") ? Element.Fire : Element.Shadow, 1.6f, 0.5f);
        return m;
    }

    private void FireTrap(SkillDef d, Vector2 aim, float coef, float mul)
    {
        _traps.RemoveAll(t => !GodotObject.IsInstanceValid(t) || t.Done);
        while (_traps.Count >= d.SummonMax) { _traps[0].Remove(); _traps.RemoveAt(0); }
        var z = new ZoneSpec { Coef = 1f, Dur = 30f, Radius = d.Radius, El = d.Element };
        var trap = GroundZone.SpawnPlayer(P, d, z, aim, coef * mul, true);
        trap.MakeTrap(0.8f, c =>
        {
            Burst(c, d.Radius, coef, d.Element, d, 0, mul);
            if (d.Leave != null) SpawnZone(c, d.Leave, d);
        });
        _traps.Add(trap);
    }

    private void FireCommand(SkillDef d, Vector2 aim, float coef, float mul)
    {
        if (d.Has("golem_cmd")) { MinionSystem.I?.Command(d, aim, coef, mul); return; }      // [golem] 魔像號令 (Golems.cs)
        var fiend = World.Minions.FirstOrDefault(m => m.Def.Id.StartsWith("pact") && !m.Dead);
        if (fiend == null) { Burst(aim, d.Radius * 0.8f, coef * 0.4f, d.Element, d, 0.15f, mul); return; }
        Fx.Telegraph(aim, d.Radius, 0.35f, Palette.Element(d.Element), true, Mark(d));
        fiend.CommandLeap(aim, 0.35f, () =>
        {
            Burst(aim, d.Radius, coef, d.Element, d, 0, mul);
            Fx.Shake(0.4f, 0.25f);
            if (d.Leave != null) SpawnZone(aim, d.Leave, d);
        });
    }

    // ───────────────────────── hooks ─────────────────────────

    public void OnEvadeEnd(Vector2 from, Vector2 to)
    {
        if (H.HasAsp("ashen_step")) GroundZone.SpawnPlayer(P, null, new ZoneSpec { Coef = 1f, Dur = 3f, Radius = 2f }, from, 0.25f, false);
        if (H.HasUnique("u_shovel"))
        {
            World.InCapsule(Faction.Player, from, to, 1.6f, Tmp);
            foreach (var t in Tmp.ToArray()) { Combat.PlayerHit(P, t, null, 1.5f, Element.Physical); t.ApplyStatus(St.Slow(0.4f, 3), P); }
            Fx.Burst(World.To3(from.Lerp(to, 0.5f), 0.2f), Element.Physical, 2.5f, 0.6f);
        }
    }

    public void OnKill(Monster m, Hit h)
    {
        if (_killDepth > 2) return;
        _killDepth++;
        try
        {
            var s = h.Skill;
            Vector2 at = m.Pos2;
            if (s != null && s.ResetOnKill && P.Cds.TryGetValue(s.Id, out var cd)) { cd.Charges = cd.MaxCharges; cd.Left = 0; }
            if (H.Class == ClassId.Soulbinder && ((s != null && s.RemnantOnKill) || Rng.Chance(RemnantChance))) SpawnRemnant(at);
            if (H.HasAsp("ember_burst")) Burst(at, 3.2f, H.Asp("ember_burst") / 100f, Element.Fire, null, 0.1f);
            if (H.HasUnique("u_snuffer") && Rng.Chance(0.15f))
            {
                World.InCircle(Faction.Player, at, 5f, Tmp);
                foreach (var t in Tmp.ToArray()) t.ApplyStatus(St.Freeze(2), P);
                Fx.Burst(World.To3(at, 0.3f), Element.Frost, 5f, 0.6f);
            }
            if (m.HexCoef > 0 && m.Has(StatusId.Hex)) Burst(at, 3.5f, m.HexCoef, Element.Shadow, null, 0.1f);
            foreach (var b in H.Buffs)
            {
                if (!b.Def.Id.EndsWith("_t")) continue;
                var d = H.Eff(b.Def.Id[..^2]);
                if (d != null && d.Has("extend_on_kill"))
                {
                    string pre = d.Id + "_";
                    foreach (var o in H.Buffs) if (o.Def.Id.StartsWith(pre)) o.Left += 1f;
                }
            }
        }
        finally { _killDepth--; }
    }
}
