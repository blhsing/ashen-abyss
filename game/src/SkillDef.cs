using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

public enum SkillKind { Melee, Proj, Wave, Chain, Nova, Ground, Zone, Dash, Leap, Blink, Buff, Summon, Channel, Trap, Command }
public enum StatusId { Burn, Poison, Bleed, Rot, Chill, Freeze, Stun, Slow, Root, Vulnerable, Weaken, Mark, Hex, Fear, Knockback, Pull, Taunt, Count }

/// <summary>A status a hit applies. Mag: slow/weaken fraction, DoT total weapon-coefficient over Dur, chill amount, or impulse.</summary>
public struct StatusApply
{
    public StatusId Id; public float Dur, Mag, Chance;
    public StatusApply(StatusId id, float dur, float mag = 0, float chance = 1f) { Id = id; Dur = dur; Mag = mag; Chance = chance; }
}

public static class St
{
    public static StatusApply Burn(float coef, float dur = 4f) => new(StatusId.Burn, dur, coef);
    public static StatusApply Poison(float coef, float dur = 4f) => new(StatusId.Poison, dur, coef);
    public static StatusApply Bleed(float coef, float dur = 4f) => new(StatusId.Bleed, dur, coef);
    public static StatusApply Rot(float coef, float dur = 4f) => new(StatusId.Rot, dur, coef);
    public static StatusApply Hex(float dur, float coef) => new(StatusId.Hex, dur, coef);
    public static StatusApply Chill(float amount = 0.3f, float dur = 3f) => new(StatusId.Chill, dur, amount);
    public static StatusApply Freeze(float dur) => new(StatusId.Freeze, dur);
    public static StatusApply Stun(float dur, float chance = 1f) => new(StatusId.Stun, dur, 0, chance);
    public static StatusApply Slow(float mag, float dur) => new(StatusId.Slow, dur, mag);
    public static StatusApply Root(float dur) => new(StatusId.Root, dur);
    public static StatusApply Vuln(float dur) => new(StatusId.Vulnerable, dur);
    public static StatusApply Weaken(float mag, float dur) => new(StatusId.Weaken, dur, mag);
    public static StatusApply Mark(float dur, float mag = 0.25f) => new(StatusId.Mark, dur, mag);
    public static StatusApply Fear(float dur) => new(StatusId.Fear, dur);
    public static StatusApply Knock(float force = 6f) => new(StatusId.Knockback, 0.25f, force);
    public static StatusApply Pull(float force = 5f) => new(StatusId.Pull, 0.25f, force);
    public static StatusApply Taunt(float dur) => new(StatusId.Taunt, dur);
}

/// <summary>A timed stat modifier on the player (or a flag such as unstoppable / stealth).</summary>
public sealed class BuffDef
{
    public string Id, Name;
    public Stat Stat = Stat.Count;        // Stat.Count = no stat, flag-only buff
    public float Value, Dur;
    public int MaxStacks = 1;
    public bool Unstoppable, Immune, Stealth, Aura, Deathless, FreeCast, Ambush, FreePips;
    public float RegenPct, Burn, LifeSteal, GainOnKill, CritBurst, VulnDur;   // effects granted while the buff is active
    public int ExtraProj;
    public BuffDef Clone() => (BuffDef)MemberwiseClone();
}

/// <summary>A persistent ground area: damage ticks, statuses, optional healing for the player standing in it.</summary>
public sealed class ZoneSpec
{
    public float Coef, Dur = 3f, Radius = 3f, Tick = 0.5f, HealPct;
    public Element El = Element.Fire;
    public List<StatusApply> OnHit = new();
    public bool Follow;
    public ZoneSpec Clone() { var z = (ZoneSpec)MemberwiseClone(); z.OnHit = new List<StatusApply>(OnHit); return z; }
}

public sealed class SkillUp
{
    public string Id, Name, Desc;
    public Action<SkillDef> Mod;
}

/// <summary>
/// Declarative definition of an active skill. One executor per SkillKind reads these fields, so most skills and
/// most upgrades need no bespoke code: an upgrade is a small function that mutates a clone of the definition.
/// Ranks 1-5 add 12% damage each; every skill has one enhancement and an exclusive A/B specialisation.
/// </summary>
public sealed class SkillDef
{
    // identity
    public string Id, Name, Desc, Icon;
    public ClassId Class;
    public SkillCat Cat;
    public int UnlockLevel = 1;

    // cost / timing
    public float Cost;                 // > 0 spends resource, < 0 generates
    public float Cooldown;
    public int Charges = 1;
    public int PipGain;                // secondary gauge (combo points / sigils) gained on hit, once per cast
    public bool PipSpend;              // consumes all pips: +30% damage each
    public int PipReq;                 // pips required and consumed
    public string Anim = "Sword_Regular_A";
    public string[] AnimAlt;           // cycled instead of Anim when set
    public float AnimSpeed = 1f, Contact = 0.28f, Lock = 0.45f;
    public string Sfx = "swing_light", HitSfx;

    // behaviour
    public SkillKind Kind;
    public Element Element = Element.Physical;
    public float Coef, RankScale = 0.12f;
    public float Range, Radius, Arc = 130f;
    public int Hits = 1; public float HitInterval = 0.12f;
    public int Count = 1; public float Spread = 12f, Speed = 28f;
    public int Pierce, Bounce;         // Pierce -1 = unlimited
    public float BounceMul = 0.7f, ChainRamp, PierceRamp, ExtraChance;
    public float Explode;              // explosion radius on projectile impact
    public float Delay, Duration, Tick = 0.3f;
    public int Form;                   // wildsoul: 0 any, 1 bear, 2 wolf, 3 human (reverts)
    public float Lucky = 0.3f;

    // riders
    public List<StatusApply> OnHit = new();
    public List<BuffDef> Buffs = new();
    public float Barrier, BarrierDur = 6f, HealPct, LifeSteal, HealOnHit, GainOnHit, GainOnKill;
    public ZoneSpec Leave;             // zone left at the impact / landing / along the path
    public float EndBurst, EndBurstRadius = 4f;   // burst when a channel / buff / zone / dash / summon ends
    public float OriginBurst, OriginDelay = 1f;   // delayed burst left at the starting point (dash / blink)
    public float CastBurst, CastBurstRadius = 6f; // burst around the caster at cast time (buff / summon kinds)
    public float PulseCoef, PulseRadius;          // periodic pulses around the caster while a buff lasts (uses Tick)
    public float Rain, RainCoef, RainRadius = 3f; // while a buff lasts, strike a random nearby enemy every Rain seconds
    public float EndHeal, GainOnCrit, RemnantChance, BarrierOnHit, MinionHpMul = 1f, PipChance;
    public int SpawnRemnants, PipOnCrit;
    public bool ResetAllCds;
    public int SplitCount; public float SplitMul = 0.5f;       // child projectiles after an explosion
    public int ExtraStrikes; public float ExtraMul = 0.35f;     // follow-up ground strikes around the first
    public Dictionary<StatusId, float> CritVs = new(), DmgVs = new();
    public float EliteDmg;
    public bool ResetOnKill, RemnantOnKill;
    public string Summon; public int SummonMax = 1; public float SummonDur;
    public int Nth; public Action<SkillDef> NthMod;             // every Nth cast is modified
    public HashSet<string> Flags = new();

    public SkillUp Enh, A, B;

    public const int MaxRank = 5;
    public float Dmg(int rank) => Coef * (1f + RankScale * (Math.Max(rank, 1) - 1));
    public int DmgPct(int rank) => (int)MathF.Round(Dmg(rank) * 100f);
    public IEnumerable<SkillUp> Ups { get { yield return Enh; yield return A; yield return B; } }
    public bool Has(string flag) => Flags.Contains(flag);

    public SkillDef Clone()
    {
        var c = (SkillDef)MemberwiseClone();
        c.OnHit = new List<StatusApply>(OnHit);
        c.Buffs = Buffs.Select(b => b.Clone()).ToList();
        c.Leave = Leave?.Clone();
        c.CritVs = new Dictionary<StatusId, float>(CritVs);
        c.DmgVs = new Dictionary<StatusId, float>(DmgVs);
        c.Flags = new HashSet<string>(Flags);
        return c;
    }

    // ── fluent builders (used only by SkillDb) ──
    public SkillDef Res(float cost, float cd = 0, int charges = 1) { Cost = cost; Cooldown = cd; Charges = charges; return this; }
    public SkillDef Cast(string anim, float speed, float contact, float lockT, string sfx, string hitSfx = null)
    { Anim = anim; AnimSpeed = speed; Contact = contact; Lock = lockT; Sfx = sfx; HitSfx = hitSfx; return this; }
    public SkillDef Alt(params string[] anims) { AnimAlt = anims; return this; }
    public SkillDef El(Element e) { Element = e; return this; }
    public SkillDef Melee(float coef, float radius, float arc = 130f, int hits = 1) { Kind = SkillKind.Melee; Coef = coef; Radius = radius; Arc = arc; Hits = hits; return this; }
    public SkillDef Proj(float coef, float range, int count = 1, int pierce = 0, float speed = 28f) { Kind = SkillKind.Proj; Coef = coef; Range = range; Count = count; Pierce = pierce; Speed = speed; return this; }
    public SkillDef Wave(float coef, float range, float width) { Kind = SkillKind.Wave; Coef = coef; Range = range; Radius = width; return this; }
    public SkillDef Chain(float coef, float range, int jumps) { Kind = SkillKind.Chain; Coef = coef; Range = range; Count = jumps; return this; }
    public SkillDef Nova(float coef, float radius, int hits = 1) { Kind = SkillKind.Nova; Coef = coef; Radius = radius; Hits = hits; return this; }
    public SkillDef Ground(float coef, float range, float radius, float delay) { Kind = SkillKind.Ground; Coef = coef; Range = range; Radius = radius; Delay = delay; return this; }
    public SkillDef Zone(float coefPerTick, float range, float radius, float dur, float tick = 0.5f) { Kind = SkillKind.Zone; Coef = coefPerTick; Range = range; Radius = radius; Duration = dur; Tick = tick; return this; }
    public SkillDef Dash(float coef, float range, float width) { Kind = SkillKind.Dash; Coef = coef; Range = range; Radius = width; return this; }
    public SkillDef Leap(float coef, float range, float radius) { Kind = SkillKind.Leap; Coef = coef; Range = range; Radius = radius; return this; }
    public SkillDef Blink(float coef, float range, float radius) { Kind = SkillKind.Blink; Coef = coef; Range = range; Radius = radius; return this; }
    public SkillDef Buff(float dur) { Kind = SkillKind.Buff; Duration = dur; return this; }
    public SkillDef Chan(float coefPerTick, float radius, float tick = 0.3f) { Kind = SkillKind.Channel; Coef = coefPerTick; Radius = radius; Tick = tick; return this; }
    public SkillDef Trap(float coef, float range, float radius) { Kind = SkillKind.Trap; Coef = coef; Range = range; Radius = radius; return this; }
    public SkillDef Sum(string id, int max, float dur, float coef, float range = 8f) { Kind = SkillKind.Summon; Summon = id; SummonMax = max; SummonDur = dur; Coef = coef; Range = range; return this; }
    public SkillDef Cmd(float coef, float range, float radius) { Kind = SkillKind.Command; Coef = coef; Range = range; Radius = radius; return this; }
    public SkillDef Hit(params StatusApply[] s) { OnHit.AddRange(s); return this; }
    public SkillDef With(Action<SkillDef> f) { f(this); return this; }
    public SkillDef B_(Stat s, float v, float dur, string name = null, int stacks = 1)
    { Buffs.Add(new BuffDef { Id = Id + "_" + s, Name = name ?? Name, Stat = s, Value = v, Dur = dur, MaxStacks = stacks }); return this; }
    public SkillDef Flag(params string[] f) { foreach (var x in f) Flags.Add(x); return this; }
    public SkillDef Ups_(SkillUp e, SkillUp a, SkillUp b)
    { Enh = e; A = a; B = b; e.Id = Id + "_e"; a.Id = Id + "_a"; b.Id = Id + "_b"; return this; }
}

/// <summary>The class-only mechanic: one of three specialisations chosen at level 15.</summary>
public sealed class ClassSpec
{
    public string Id, Name, Desc;
    public ClassId Class;
    public (Stat s, float v)[] Stats = Array.Empty<(Stat, float)>();
    public Action<SkillDef> Mod;       // applied to every skill of the class
}
