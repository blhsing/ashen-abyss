using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

public struct HitOpt
{
    public float Mul;
    public bool Minion, NoCrit, ForceCrit, NoOnHit;
    public Vector2 Dir;
    public Actor MinionActor;
}

/// <summary>
/// Damage rules. Player damage = weapon roll × skill coefficient × main-stat multiplier × (1 + all additive bonuses)
/// × fixed multipliers (crit ×1.5, vulnerable ×1.2, overpower ×1.25) × conditional multipliers from powers.
/// Incoming damage is reduced in turn by armour, resistance and damage reduction, which multiply together.
/// </summary>
public static class Combat
{
    /// <summary>Share of a blocked hit that still lands (block chance comes from shields: Stat.Block).</summary>
    public const float BlockedShare = 0.5f;

    /// <summary>--no-overpower (measurement only, --suite=dps): no hit overpowers, to size what overpower adds.</summary>
    private static bool? _noOp;
    private static bool NoOverpowerTest => _noOp ??= Game.I?.Arg("no-overpower") ?? false;

    public static float PlayerHit(Player p, Actor t, SkillDef s, float coef, Element el, HitOpt o = default)
    {
        if (t == null || t.Dead || p == null) return 0f;
        var h = p.Hero; var S = h.S;
        float w = Rng.Range(h.StrikeMin, h.StrikeMax);      // the hand that strikes now (dual-wield alternates, SkillExec.Fire)
        // [fixes 0.4.0] One weapon hit is the weapon's damage per second ÷ its swing speed, which keeps attacks that are
        // repeated as fast as the weapon swings equal on every base. A skill held back by its cooldown, or a minion's
        // blow, comes at its own rate whatever the weapon: with the per-swing hit a slow two-hander made them up to 1.7×
        // stronger (--suite=dps:…:bases). They use the hit at the class's own pace instead (× SpeedMul), the same on every base.
        if (o.Minion || (s != null && s.Cooldown > 0f)) w *= h.SpeedMul;
        float add = S[Stat.DmgAll] + S[StatInfo.DmgOf(el)];
        if (s != null) add += s.Cat switch { SkillCat.Basic => S[Stat.DmgBasic], SkillCat.Core => S[Stat.DmgCore], SkillCat.Ultimate => S[Stat.DmgUlt], _ => 0f };
        bool elite = t.IsElite || t.IsBoss;
        if (elite) add += S[Stat.DmgElite];
        if (t.AnyCc) add += S[Stat.DmgCC];
        bool vuln = t.Has(StatusId.Vulnerable);
        if (vuln) add += S[Stat.DmgVuln];
        if (o.Minion) { add += S[Stat.MinionDmg]; if (t.Has(StatusId.Mark)) add += t.StMag[(int)StatusId.Mark] * 100f; }

        float cc = S[Stat.CritChance];
        if (s != null) { float best = 0; foreach (var kv in s.CritVs) if (t.Has(kv.Key)) best = MathF.Max(best, kv.Value); cc += best; }
        if (h.Class == ClassId.Hunter && h.HasKeystone("ks_combo")) cc += p.Pips * 3f;
        bool crit = !o.NoCrit && (o.ForceCrit || (!o.Minion && p.ConsumeAmbush()) || Rng.Chance(cc / 100f));
        if (crit) add += S[Stat.CritDmg];
        bool op = !o.Minion && !o.NoCrit && !NoOverpowerTest && Rng.Chance(h.D.Spec == "eb_heavy" ? 0.06f : 0.03f);
        if (op) add += 50f + S[Stat.DmgOverpower];

        float m = (o.Mul <= 0f ? 1f : o.Mul) * (crit ? 1.5f : 1f) * (vuln ? 1.2f : 1f) * (op ? 1.25f : 1f);
        if (s != null)
        {
            float best = 0; foreach (var kv in s.DmgVs) if (t.Has(kv.Key)) best = MathF.Max(best, kv.Value);
            m *= 1f + best;
            if (elite && s.EliteDmg > 0f) m *= 1f + s.EliteDmg;
        }
        m *= p.DamageMult(t, s, o.Minion);
        float dmg = w * coef * h.MainMul * (1f + add / 100f) * m;
        var hit = new Hit { Source = o.MinionActor ?? p, Amount = dmg, El = el, Crit = crit, Overpower = op, Skill = s, Dir = o.Dir, Minion = o.Minion };
        float dealt = t.TakeHit(hit);
        if (!o.NoOnHit) p.AfterHit(t, s, hit, dealt, el, o.Minion);
        return dealt;
    }

    /// <summary>Damage per second of a DoT whose total over <paramref name="dur"/> is <paramref name="coefTotal"/> × weapon damage.</summary>
    public static float DotDps(Player p, float coefTotal, float dur, Element el, bool minion = false)
    {
        var h = p.Hero; var S = h.S;
        // [fixes 0.4.0] a DoT does not stack (the stronger one stays), so its damage per second must not depend on how
        // slowly the weapon swings: the hit at the class's own pace (see PlayerHit)
        float w = (h.WeaponMin + h.WeaponMax) * 0.5f * h.SpeedMul;
        float add = S[Stat.DmgAll] + S[Stat.DmgDot] + S[StatInfo.DmgOf(el)] + (minion ? S[Stat.MinionDmg] : 0f);
        return w * coefTotal * h.MainMul * (1f + add / 100f) / MathF.Max(dur, 0.1f);
    }

    public static Element DotElement(StatusId id, Element fallback) => id switch
    {
        StatusId.Burn => Element.Fire, StatusId.Poison => Element.Poison, StatusId.Bleed => Element.Physical, StatusId.Rot => Element.Shadow, _ => fallback
    };

    /// <summary>Applies a skill's statuses from the player to a target (DoTs are sized from the player's stats).</summary>
    public static void ApplyStatuses(Player p, Actor t, IEnumerable<StatusApply> list, Element el, bool minion = false)
    {
        foreach (var st in list)
        {
            if (st.Id is StatusId.Burn or StatusId.Poison or StatusId.Bleed or StatusId.Rot)
            {
                var de = DotElement(st.Id, el);
                t.ApplyStatus(st, p, DotDps(p, st.Mag, st.Dur, de, minion), de, minion);
            }
            else t.ApplyStatus(st, p);
        }
    }

    /// <summary>A monster (or hostile effect) damaging the player or one of the player's minions.</summary>
    public static float MonsterHit(Monster m, Actor target, float mul, Element el, StatusApply? status = null, bool melee = false)
    {
        if (target == null || target.Dead) return 0f;
        float raw = m.BaseDamage * mul * m.WeakenMul * Rng.Range(0.9f, 1.1f);
        if (target is Player p)
        {
            if (p.Invulnerable) return 0f;
            var h = p.Hero;
            if (Rng.Chance(h.S[Stat.Dodge] / 100f)) { World.RaiseDamage(p, new Hit { Source = m, Amount = 0, El = el }, -1f); return 0f; }
            float red = (1f - Balance.ArmorDr(h.Armor, m.Level)) * (1f - h.Res(el) / 100f) * (1f - h.S[Stat.DmgReduction] / 100f) * p.ExtraDr();
            if (h.S[Stat.Block] > 0f && Rng.Chance(h.S[Stat.Block] / 100f)) red *= BlockedShare;    // a shield (or block affixes) takes half the hit
            float dmg = raw * red;
            float dealt = p.TakeHit(new Hit { Source = m, Amount = dmg, El = el });
            if (status.HasValue && !p.Unstoppable && !p.Dead)
            {
                var st = status.Value;
                if (st.Id is StatusId.Burn or StatusId.Poison or StatusId.Bleed or StatusId.Rot)
                    p.ApplyStatus(st, m, raw * st.Mag * red / MathF.Max(st.Dur, 0.1f), DotElement(st.Id, el));
                else p.ApplyStatus(st, m);
            }
            if (melee && !m.Dead) p.OnStruckMelee(m);
            return dealt;
        }
        float md = raw * 0.45f * (World.P != null && World.P.Hero.HasKeystone("ks_army") ? 0.7f : 1f);
        return target.TakeHit(new Hit { Source = m, Amount = md, El = el });
    }

    /// <summary>Hostile area damage centred on a point (ground telegraphs, explosions, pools).</summary>
    private static readonly List<Actor> Tmp = new();
    public static void MonsterArea(Monster m, Vector2 c, float r, float mul, Element el, StatusApply? status = null)
    {
        World.InCircle(Faction.Monster, c, r, Tmp);
        foreach (var a in Tmp.ToArray()) MonsterHit(m, a, mul, el, status);
    }
}
