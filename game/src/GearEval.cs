using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AshenAbyss;

/// <summary>The judgement of one item against what the hero wears: verdict, offence and defence change (fractions), notes.</summary>
public sealed class GearJudgement
{
    public GearVerdict Verdict;
    public float Off, Def;              // relative change of the two scores: +0.082 = +8.2%
    public float Total;                 // weighted (GearEval weighting)
    public bool Special;                // a legendary power or unique effect the numbers cannot value (含特殊效果)
    public int NeedLevel;               // > 0: the hero cannot wear it yet (level requirement)
}

/// <summary>
/// The one gear evaluator (升級 / 相近 / 較差) used by the bag filter, its presets, tooltips, the AI pilot and TestBot.
/// It simulates the swap on a copy of the hero (a two-hand weapon replaces main + off hand, a one-hand weapon or an
/// off-hand the slot it goes to, a ring the weaker ring — exactly what Hero.Equip would do), recomputes every stat with
/// the game's own Hero.Recalc, and scores two numbers before and after:
/// offence = expected damage per second of the primary attack (weapon hit × swings per second × main attribute ×
/// damage bonuses × crit and overpower, cooldowns, skill ranks); defence = effective life against a typical hit at
/// the hero's level (life through armour, resistances, damage reduction, block and dodge).
/// Verdict: 升級 when one score rises over 2% and the other falls no more than 2%, or the weighted total rises over 3%;
/// 較差 when both fall, or the total falls over 3%; else 相近. Temporary buffs are left out of both sides.
/// </summary>
public static class GearEval
{
    public const float Step = 0.02f, TotalStep = 0.03f;
    private static readonly Dictionary<string, GearJudgement> Cache = new();

    /// <summary>Weighting chosen in the settings (裝備比較): 0 平衡 50/50, 1 偏重攻擊 75/25, 2 偏重防禦 25/75.</summary>
    public static int Weighting => Math.Clamp(Game.I?.Cfg?.GearWeight ?? 0, 0, 2);
    public static (float off, float def) Weights(int w) => w == 1 ? (0.75f, 0.25f) : w == 2 ? (0.25f, 0.75f) : (0.5f, 0.5f);

    /// <summary>Forget cached judgements (the weighting changed).</summary>
    public static void Invalidate() => Cache.Clear();

    // ───────────────────────── the two scores ─────────────────────────

    /// <summary>Expected damage per second of the hero's primary attack (no target conditions beyond averages).</summary>
    public static float Offense(Hero h)
    {
        var S = h.S;
        float w = (h.WeaponMin + h.WeaponMax) * 0.5f * h.SwingsPerSec;
        var basic = SkillDb.Get(SkillDb.DefaultBasic(h.Class));
        var el = basic?.Element ?? Element.Physical;
        bool minions = h.Class is ClassId.Soulbinder or ClassId.Pactbinder or ClassId.Wildsoul;
        float add = S[Stat.DmgAll] + S[StatInfo.DmgOf(el)] + 0.5f * (S[Stat.DmgBasic] + S[Stat.DmgCore]) + 0.15f * S[Stat.DmgUlt]
                    + 0.25f * (S[Stat.DmgVuln] + S[Stat.DmgElite] + S[Stat.DmgCC]) + 0.3f * S[Stat.DmgDot] + (minions ? 0.5f * S[Stat.MinionDmg] : 0f);
        if (h.Class == ClassId.Soulbinder) add += MinionTerms(h);         // [golem] golem, mage and minion-speed affixes of a minion build
        float crit = Math.Clamp(S[Stat.CritChance] / 100f, 0f, 1f);
        float normal = 1f + add / 100f;
        float critical = 1.5f * (1f + (add + S[Stat.CritDmg]) / 100f);
        float hit = (1f - crit) * normal + crit * critical;
        float op = 0.03f * (1.25f * (1f + (add + 50f + S[Stat.DmgOverpower]) / 100f) - normal);      // 3% overpower chance, its extra
        float cdr = 1f + 0.3f * S[Stat.Cdr] / 100f;
        float ranks = 1f + 0.05f * S[Stat.SkillRanks];
        float res = 1f + 0.002f * (S[Stat.ResourceGen] + S[Stat.ResourceCost] * 2f) + 0.001f * S[Stat.MaxResource] + 0.002f * S[Stat.LuckyHit];
        return w * h.MainMul * (hit + op) * cdr * ranks * res;
    }

    /// <summary>[golem] What the Soulbinder's minion affixes add to the damage score (zero for a line that is sacrificed).</summary>
    public static float MinionTerms(Hero h)
    {
        var S = h.S;
        bool golem = !h.LineSacrificed(ClassBook.Golem), mages = !h.LineSacrificed(ClassBook.Mages), any = golem || mages || !h.LineSacrificed(ClassBook.Warriors);
        return (golem ? 0.35f * S[Stat.GolemDmg] + 0.1f * S[Stat.CmdCdr] + 0.05f * S[Stat.GolemLife] : 0f) + (mages ? 0.25f * S[Stat.MageDmg] : 0f) + (any ? 0.4f * S[Stat.MinionAtkSpeed] : 0f);
    }

    /// <summary>Effective life against a typical hit at the hero's level: half physical, half spread over the five elements.</summary>
    public static float Defense(Hero h)
    {
        var S = h.S;
        float armor = 1f - Balance.ArmorDr(h.Armor, h.D.Level);
        float res = (h.Res(Element.Fire) + h.Res(Element.Frost) + h.Res(Element.Lightning) + h.Res(Element.Poison) + h.Res(Element.Shadow)) / 5f;
        float resTaken = 0.5f + 0.5f * (1f - res / 100f);
        float dr = 1f - S[Stat.DmgReduction] / 100f;
        float block = 1f - S[Stat.Block] / 100f * (1f - Combat.BlockedShare);
        float dodge = 1f - S[Stat.Dodge] / 100f;
        float life = h.MaxLife * (1f + 0.2f * S[Stat.BarrierPct] / 100f) + 8f * S[Stat.LifeRegen] + 4f * S[Stat.LifeOnHit] + S[Stat.LifeOnKill];
        float heal = 1f + 0.2f * S[Stat.Healing] / 100f;
        return life * heal / MathF.Max(0.02f, armor * resTaken * dr * block * dodge);
    }

    // ───────────────────────── the swap ─────────────────────────

    /// <summary>A copy of the hero (no buffs) wearing what it wears now.</summary>
    public static Hero Baseline(Hero h)
    {
        var d = h.D.ShallowCopy();
        d.Equip = (Item[])h.D.Equip.Clone();
        return new Hero(d);
    }

    /// <summary>A copy of the hero (no buffs) with <paramref name="it"/> put on as Hero.Equip would (whatever it takes off is gone).</summary>
    public static Hero Simulate(Hero h, Item it)
    {
        var d = h.D.ShallowCopy();
        var e = (Item[])h.D.Equip.Clone();
        int at = Array.IndexOf(e, it);
        if (at >= 0) e[at] = null;
        var slot = h.TargetSlot(it);
        e[(int)slot] = it;
        if (slot == EquipSlot.MainHand && it.TwoHand) e[(int)EquipSlot.OffHand] = null;
        if (slot == EquipSlot.OffHand && e[(int)EquipSlot.MainHand] is { } m && m.TwoHand) e[(int)EquipSlot.MainHand] = null;
        d.Equip = e;
        return new Hero(d);
    }

    /// <summary>Everything about an item that changes its stats (enchanting and tempering replace affixes in place).</summary>
    private static string ItemSig(Item e)
    {
        if (e == null) return "-";
        float a = 0f;
        foreach (var x in e.Affixes) a += (int)x.Stat * 31f + x.Value * 7f + x.Bonus;
        return $"{e.Uid}.{e.Upgrade}.{e.Gems.Count}.{e.DmgMax:0.##}.{a:0.###}";
    }

    private static string Sig(Hero h, Item it)
    {
        var sb = new System.Text.StringBuilder(200);
        sb.Append(ItemSig(it)).Append('|');
        foreach (var e in h.D.Equip) sb.Append(ItemSig(e)).Append(',');
        sb.Append(h.D.Level).Append('/').Append(h.D.Paragon).Append('/').Append(h.D.Talents.Count).Append('/').Append(h.D.Skills.Values.Sum())
          .Append('/').Append(h.D.Spec).Append('/').Append(Weighting);
        return sb.ToString();
    }

    /// <summary>
    /// Judges an item for this hero (cached until the gear, level, board, skills or weighting change). Worn items and
    /// gems are NotGear; a weapon or off-hand the class cannot hold (or another class's unique) is Unusable.
    /// </summary>
    public static GearJudgement Judge(Hero h, Item it)
    {
        if (h == null || it == null || !it.IsEquip || h.IsEquipped(it)) return new GearJudgement { Verdict = GearVerdict.NotGear };
        if (!h.UsableByClass(it)) return new GearJudgement { Verdict = GearVerdict.Unusable, Special = it.Aspect != null || it.Unique != null };
        string key = Sig(h, it);
        if (Cache.TryGetValue(key, out var j)) return j;
        if (Cache.Count > 4000) Cache.Clear();
        j = new GearJudgement { Special = it.Aspect != null || it.Unique != null, NeedLevel = it.ReqLevel > h.D.Level ? it.ReqLevel : 0 };
        try
        {
            var before = Baseline(h);
            var after = Simulate(h, it);
            float o0 = Offense(before), d0 = Defense(before), o1 = Offense(after), d1 = Defense(after);
            j.Off = o0 > 0f ? o1 / o0 - 1f : 0f;
            j.Def = d0 > 0f ? d1 / d0 - 1f : 0f;
            var (wo, wd) = Weights(Weighting);
            j.Total = wo * j.Off + wd * j.Def;
            j.Verdict = Verdict(j.Off, j.Def, j.Total);
        }
        catch (Exception e)
        {
            Log.Error("gear", $"judging {it.Name} failed", e);
            j.Verdict = GearVerdict.Similar;
        }
        Cache[key] = j;
        return j;
    }

    /// <summary>The verdict rule on its own (offence and defence change as fractions, weighted total).</summary>
    public static GearVerdict Verdict(float off, float def, float total)
    {
        if ((off > Step && def >= -Step) || (def > Step && off >= -Step) || total > TotalStep) return GearVerdict.Better;
        if ((off < -0.001f && def < -0.001f) || total < -TotalStep) return GearVerdict.Worse;
        return GearVerdict.Similar;
    }

    public static string VerdictName(GearVerdict v) => v switch
    {
        GearVerdict.Better => "升級", GearVerdict.Worse => "較差", GearVerdict.Similar => "相近", GearVerdict.Unusable => "無法使用", _ => ""
    };

    public static Godot.Color VerdictColor(GearVerdict v) => v switch
    {
        GearVerdict.Better => Palette.Good, GearVerdict.Worse => Palette.Bad, GearVerdict.Unusable => Palette.Bad, _ => Palette.TextDim
    };

    private static string Pct(float f)
    {
        float p = f * 100f;
        string s = Math.Abs(p) < 0.05f ? "0" : p.ToString("0.0", CultureInfo.InvariantCulture);
        return (p > 0.05f ? "+" : p < -0.05f ? "−" : "±") + s.TrimStart('-') + "%";
    }

    /// <summary>One line for the tooltip, e.g. 「升級：攻擊 +8.2%　防禦 −1.1%」 (BBCode colour), plus the notes; empty for NotGear.</summary>
    public static string Line(GearJudgement j)
    {
        if (j == null || j.Verdict == GearVerdict.NotGear) return "";
        string hex = VerdictColor(j.Verdict).ToHtml(false);
        string s = j.Verdict == GearVerdict.Unusable
            ? $"[color=#{hex}]無法使用：此職業無法裝備[/color]"
            : $"[color=#{hex}]{VerdictName(j.Verdict)}：攻擊 {Pct(j.Off)}　防禦 {Pct(j.Def)}[/color]";
        string dim = Palette.TextDim.ToHtml(false);
        if (j.Special) s += $"　[color=#{dim}]含特殊效果[/color]";
        if (j.NeedLevel > 0) s += $"　[color=#{dim}]（需要等級 {j.NeedLevel}）[/color]";
        return s;
    }

    /// <summary>For bots and the AI pilot: true when wearing it is a 升級 (and it can be worn now).</summary>
    public static bool IsUpgrade(Hero h, Item it) => it != null && h.WhyCantEquip(it) == null && Judge(h, it).Verdict == GearVerdict.Better;
}
