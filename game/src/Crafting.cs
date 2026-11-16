using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AshenAbyss;

/// <summary>Crafting state kept in the save next to the fields declared in Save.cs.</summary>
public sealed partial class SaveData
{
    public HashSet<string> Junk = new();               // uids of bag items the player marked as junk (廢品)
}

/// <summary>A tempering recipe: one themed family of affixes the smith can try to beat into an item.</summary>
public sealed class TemperRecipe
{
    public string Id, Name, Desc;
    public int Class = -1;                 // ClassId, or -1 when every class may use it
    public ItemKind[] Kinds;
    public Stat[] Stats;
    public bool Allows(ItemKind k) => Array.IndexOf(Kinds, k) >= 0;
    public bool ForClass(ClassId c) => Class < 0 || Class == (int)c;
}

/// <summary>What a salvage run yields (or yielded): materials, returned gems and the fate of each legendary power.</summary>
public sealed class SalvageSummary
{
    public int Count, Scrap, Dust, Cores, Gems, Precious;
    public readonly List<string> Powers = new();       // one line per legendary power or unique item
}

/// <summary>
/// Rules and data of the crafting stations that are not already in <see cref="ItemDb"/>: tempering recipes and costs,
/// gem combining, the jeweler's fallback stock, bag ordering and salvage previews. Pure data and arithmetic; the
/// Hero methods in Hero.Crafting.cs validate and apply them.
/// </summary>
public static class Crafting
{
    public const int TemperCharges = 5;                // an item's TemperLeft starts here
    public const int GemsPerCombine = 3;
    public const int MaxGemTier = 3;
    public const string ScrapName = "碎鐵", DustName = "幽塵", CoreName = "燼核";

    private static readonly ItemKind[] AllGear = { ItemKind.Weapon, ItemKind.Helm, ItemKind.Chest, ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet, ItemKind.Ring };
    private static readonly ItemKind[] WeaponOnly = { ItemKind.Weapon };
    private static readonly ItemKind[] OffenceGear = { ItemKind.Weapon, ItemKind.Gloves, ItemKind.Amulet, ItemKind.Ring };
    private static readonly ItemKind[] DefenceGear = { ItemKind.Helm, ItemKind.Chest, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet };
    private static readonly ItemKind[] UtilityGear = { ItemKind.Helm, ItemKind.Chest, ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet, ItemKind.Ring };
    private static readonly ItemKind[] ResourceGear = { ItemKind.Weapon, ItemKind.Helm, ItemKind.Gloves, ItemKind.Amulet, ItemKind.Ring };
    private static readonly ItemKind[] MobilityGear = { ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet };

    // ───────────────────────── tempering ─────────────────────────

    public static readonly List<TemperRecipe> Recipes = new()
    {
        new() { Id = "weapon", Name = "鋒刃訣", Kinds = WeaponOnly,
            Desc = "把殺意淬進刃口。只有武器承受得住。",
            Stats = new[] { Stat.CritDmg, Stat.DmgVuln, Stat.DmgOverpower, Stat.DmgElite, Stat.DmgCC, Stat.LifeOnHit, Stat.LifeOnKill } },
        new() { Id = "offence", Name = "殺伐訣", Kinds = OffenceGear,
            Desc = "讓每一次出手都更重、更快、更準。",
            Stats = new[] { Stat.DmgAll, Stat.CritChance, Stat.AttackSpeed, Stat.DmgBasic, Stat.DmgCore, Stat.DmgUlt, Stat.DmgDot } },
        new() { Id = "defence", Name = "守禦訣", Kinds = DefenceGear,
            Desc = "灰暮之下，活著回到火邊才算數。",
            Stats = new[] { Stat.MaxLife, Stat.LifePct, Stat.Armor, Stat.ResAll, Stat.DmgReduction, Stat.BarrierPct, Stat.Thorns, Stat.LifeRegen } },
        new() { Id = "utility", Name = "拾遺訣", Kinds = UtilityGear,
            Desc = "旅人的小聰明：多撿一枚錢，多留一口氣。",
            Stats = new[] { Stat.Healing, Stat.LuckyHit, Stat.LifeOnKill, Stat.GoldFind, Stat.MagicFind, Stat.XpGain } },
        new() { Id = "resource", Name = "蓄能訣", Kinds = ResourceGear,
            Desc = "讓體內的餘燼燒得更久，也回得更快。",
            Stats = new[] { Stat.MaxResource, Stat.ResourceGen, Stat.ResourceCost, Stat.Cdr } },
        new() { Id = "mobility", Name = "疾行訣", Kinds = MobilityGear,
            Desc = "站著不動的人，最先被灰埋住。",
            Stats = new[] { Stat.MoveSpeed, Stat.Dodge, Stat.AttackSpeed } },

        new() { Id = "class_emberblade", Name = "燼刃訣", Class = (int)ClassId.Emberblade, Kinds = AllGear,
            Desc = "燼刃衛的傳承：重、燙，而且不退。",
            Stats = new[] { Stat.Str, Stat.DmgPhysical, Stat.DmgFire, Stat.DmgOverpower, Stat.DmgCore, Stat.Thorns } },
        new() { Id = "class_astromancer", Name = "觀星訣", Class = (int)ClassId.Astromancer, Kinds = AllGear,
            Desc = "星術師的傳承：天上已無星辰，杖端仍有。",
            Stats = new[] { Stat.Int, Stat.DmgFire, Stat.DmgFrost, Stat.DmgLightning, Stat.MaxResource, Stat.DmgCore } },
        new() { Id = "class_hunter", Name = "影獵訣", Class = (int)ClassId.Hunter, Kinds = AllGear,
            Desc = "影獵手的傳承：先看見破綻，再讓對方看見你。",
            Stats = new[] { Stat.Dex, Stat.DmgPhysical, Stat.DmgPoison, Stat.DmgShadow, Stat.DmgVuln, Stat.CritChance } },
        new() { Id = "class_soulbinder", Name = "喚魂訣", Class = (int)ClassId.Soulbinder, Kinds = AllGear,
            Desc = "喚魂師的傳承：亡者不索報酬，只要一個名字。",
            Stats = new[] { Stat.Wil, Stat.MinionDmg, Stat.DmgShadow, Stat.DmgDot, Stat.LifeOnKill, Stat.MaxResource } },
        new() { Id = "class_wildsoul", Name = "荒靈訣", Class = (int)ClassId.Wildsoul, Kinds = AllGear,
            Desc = "荒靈行者的傳承：枯林記得每一道爪痕與雷聲。",
            Stats = new[] { Stat.Wil, Stat.DmgPhysical, Stat.DmgLightning, Stat.DmgOverpower, Stat.LifePct, Stat.ResourceGen } },
        new() { Id = "class_forgebody", Name = "鍛身訣", Class = (int)ClassId.Forgebody, Kinds = AllGear,
            Desc = "鍛身者的傳承：爐火熄了，就拿自己當鐵。",
            Stats = new[] { Stat.Dex, Stat.DmgPhysical, Stat.DmgFire, Stat.DmgBasic, Stat.AttackSpeed, Stat.DmgReduction } },
        new() { Id = "class_lampknight", Name = "誓燈訣", Class = (int)ClassId.Lampknight, Kinds = AllGear,
            Desc = "誓燈騎士的傳承：盾後有燈，燈後有人。",
            Stats = new[] { Stat.Str, Stat.DmgFire, Stat.BarrierPct, Stat.Thorns, Stat.Healing, Stat.DmgCC } },
        new() { Id = "class_pactbinder", Name = "縛淵訣", Class = (int)ClassId.Pactbinder, Kinds = AllGear,
            Desc = "縛淵術士的傳承：契約上的每一個字，都要有人償還。",
            Stats = new[] { Stat.Int, Stat.MinionDmg, Stat.DmgShadow, Stat.DmgFire, Stat.DmgDot, Stat.MaxResource } },
    };

    public static TemperRecipe Recipe(string id)
    {
        if (id == null) return null;
        foreach (var r in Recipes) if (r.Id == id) return r;
        return null;
    }

    /// <summary>The recipes a class may use: the six shared categories, then its own.</summary>
    public static List<TemperRecipe> RecipesFor(ClassId cls)
    {
        var l = new List<TemperRecipe>();
        foreach (var r in Recipes) if (r.ForClass(cls)) l.Add(r);
        return l;
    }

    /// <summary>How many tempered affixes an item can hold: rare one, legendary two, everything else none.</summary>
    public static int TemperSlots(Item it) =>
        it == null || !it.IsEquip ? 0 : it.Rarity == Rarity.Rare ? 1 : it.Rarity == Rarity.Legendary ? 2 : 0;

    public static int TemperedCount(Item it)
    {
        int n = 0;
        if (it != null) foreach (var a in it.Affixes) if (a.Tempered) n++;
        return n;
    }

    /// <summary>Index inside <c>it.Affixes</c> of the affix sitting in tempering slot <paramref name="slot"/>, or -1 when that slot is empty.</summary>
    public static int TemperedIndex(Item it, int slot)
    {
        if (it == null || slot < 0) return -1;
        int n = 0;
        for (int i = 0; i < it.Affixes.Count; i++)
        {
            if (!it.Affixes[i].Tempered) continue;
            if (n == slot) return i;
            n++;
        }
        return -1;
    }

    private static Dictionary<Stat, ItemDb.Pool> _pools;
    /// <summary>The affix pool entry that defines a stat's value range (tempering ignores the pool's item-kind limits).</summary>
    public static ItemDb.Pool PoolOf(Stat s)
    {
        if (_pools == null)
        {
            _pools = new Dictionary<Stat, ItemDb.Pool>();
            foreach (var p in ItemDb.Pools) _pools.TryAdd(p.S, p);
        }
        return _pools.TryGetValue(s, out var pool) ? pool : null;
    }

    /// <summary>Lowest and highest value a tempered roll of this stat can take on this item, as shown after masterworking.</summary>
    public static (float lo, float hi) TemperRange(Stat s, Item it)
    {
        var p = PoolOf(s);
        if (p == null || it == null) return (0, 0);
        float span = p.Lo + (p.Hi - p.Lo) * (it.Power / (float)ItemDb.MaxPower);
        float mul = s == Stat.SkillRanks ? 1f : it.AffixMul;
        return (RoundStat(s, span * 0.72f) * mul, RoundStat(s, span) * mul);
    }

    private static float RoundStat(Stat s, float v) => StatInfo.IsPct(s) ? MathF.Round(v * 10f) / 10f : MathF.Max(1, MathF.Round(v));

    /// <summary>Stats of the recipe the item can still receive. The affix at <paramref name="replaceIndex"/> is about to be replaced, so its stat is free again.</summary>
    public static List<Stat> TemperCandidates(TemperRecipe r, Item it, int replaceIndex)
    {
        var l = new List<Stat>();
        if (r == null || it == null) return l;
        foreach (var s in r.Stats)
        {
            if (PoolOf(s) == null) continue;
            bool taken = false;
            for (int i = 0; i < it.Affixes.Count; i++)
                if (i != replaceIndex && it.Affixes[i].Stat == s) { taken = true; break; }
            if (!taken) l.Add(s);
        }
        return l;
    }

    /// <summary>Rolls one tempered affix from the recipe: a random stat of the category, valued by the item's power.</summary>
    public static Affix RollTemper(TemperRecipe r, Item it, int replaceIndex, Random rng = null)
    {
        rng ??= Rng.R;
        var cands = TemperCandidates(r, it, replaceIndex);
        if (cands.Count == 0) return null;
        var pool = PoolOf(cands[rng.Next(cands.Count)]);
        var a = ItemDb.RollAffix(pool, it, rng);
        a.Tempered = true;
        return a;
    }

    public static int TemperGoldCost(Item it) => (int)((70 + it.Power * 2.4f) * (it.Rarity >= Rarity.Legendary ? 1.6f : 1f));
    public static int TemperScrapCost(Item it) => 3 + it.Power / 100;
    public static int TemperDustCost(Item it) => 1 + it.Power / 200;
    /// <summary>The second tempering slot of a legendary also eats an ember core per attempt.</summary>
    public static int TemperCoreCost(Item it, int slot) => slot >= 1 ? 1 : 0;

    // ───────────────────────── masterworking helpers ─────────────────────────

    public static bool IsMilestone(int rank) => rank > 0 && rank % 4 == 0;
    /// <summary>How many 25% milestone boosts an affix carries.</summary>
    public static int Boosts(Affix a) => a == null ? 0 : (int)MathF.Round(a.Bonus / 0.25f);

    // ───────────────────────── gems ─────────────────────────

    /// <summary>The gem of the same kind one tier up, or null at the top tier.</summary>
    public static GemDef NextGem(string gemId)
    {
        var g = ItemDb.Gem(gemId);
        if (g == null || g.Tier >= MaxGemTier) return null;
        foreach (var x in ItemDb.Gems) if (x.Icon == g.Icon && x.Tier == g.Tier + 1) return x;
        return null;
    }

    public static int GemCombineGold(GemDef g) => 120 * g.Tier * g.Tier;
    public static int GemCombineDust(GemDef g) => g.Tier >= 2 ? 3 : 0;
    public static int GemPrice(GemDef g) => 220 * g.Tier * g.Tier;

    /// <summary>What the jeweler sells when the loot tables offer no gems: every chipped gem, and flawless ones for seasoned bearers.</summary>
    public static List<(Item item, int price)> GemStock(int level)
    {
        var l = new List<(Item, int)>();
        int maxTier = level >= 30 ? 2 : 1;
        foreach (var g in ItemDb.Gems)
            if (g.Tier <= maxTier) l.Add((ItemDb.MakeGem(g.Id), GemPrice(g)));
        return l;
    }

    // ───────────────────────── bag order ─────────────────────────

    /// <summary>Bag order: gear by slot, best rarity and power first; gems last, grouped by kind.</summary>
    public static int CompareItems(Item a, Item b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        bool ga = a.Kind == ItemKind.Gem, gb = b.Kind == ItemKind.Gem;
        if (ga != gb) return ga ? 1 : -1;
        int c;
        if (ga)
        {
            c = string.CompareOrdinal(a.Icon, b.Icon); if (c != 0) return c;
            c = b.Power.CompareTo(a.Power); if (c != 0) return c;
            return string.CompareOrdinal(a.Uid, b.Uid);
        }
        c = ((int)a.Kind).CompareTo((int)b.Kind); if (c != 0) return c;
        c = ((int)b.Rarity).CompareTo((int)a.Rarity); if (c != 0) return c;
        c = b.Power.CompareTo(a.Power); if (c != 0) return c;
        c = string.CompareOrdinal(a.Name, b.Name); if (c != 0) return c;
        return string.CompareOrdinal(a.Uid, b.Uid);
    }

    // ───────────────────────── salvage preview ─────────────────────────

    private static string Hex(Godot.Color c) => c.ToHtml(false);

    /// <summary>What salvaging these items would give, without touching anything. Power lines are BBCode.</summary>
    public static SalvageSummary PreviewSalvage(IEnumerable<Item> items, SaveData d)
    {
        var s = new SalvageSummary();
        Dictionary<string, float> best = null;         // best roll per power so far: the codex, then earlier items of this batch
        foreach (var it in items)
        {
            if (it == null || !it.IsEquip) continue;
            var y = ItemDb.SalvageYield(it);
            s.Count++; s.Scrap += y.scrap; s.Dust += y.dust; s.Cores += y.cores; s.Gems += it.Gems.Count;
            if (it.Rarity >= Rarity.Legendary) s.Precious++;
            if (it.Unique != null)
            {
                s.Powers.Add($"[color=#{Hex(Palette.Rarity(Rarity.Unique))}]✹ {it.Name}[/color]　[color=#{Hex(Palette.TextDim)}]獨特之物不留威能，只餘材料[/color]");
                continue;
            }
            if (it.Aspect == null) continue;
            var asp = ItemDb.Aspect(it.Aspect);
            if (asp == null) continue;
            best ??= new Dictionary<string, float>();
            bool known = best.TryGetValue(it.Aspect, out float old) || (d != null && d.Codex.TryGetValue(it.Aspect, out old));
            string name = $"[color=#{Hex(Palette.Rarity(Rarity.Legendary))}]✹ {asp.Name}（{asp.Value(it.AspectRoll):0}）[/color]";
            if (!known)
                s.Powers.Add($"{name}　[color=#{Hex(Palette.Good)}]將記入威能典籍[/color]");
            else if (it.AspectRoll > old)
                s.Powers.Add($"{name}　[color=#{Hex(Palette.Good)}]將取代典籍中的紀錄（{asp.Value(old):0}）[/color]");
            else
                s.Powers.Add($"{name}　[color=#{Hex(Palette.TextDim)}]典籍已有同等或更佳的紀錄（{asp.Value(old):0}）[/color]");
            if (!known || it.AspectRoll > old) best[it.Aspect] = it.AspectRoll;
        }
        return s;
    }

    // ───────────────────────── text ─────────────────────────

    public static string Num(long v) => v.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>"碎鐵 ×4、幽塵 ×1" — plain text, empty parts left out.</summary>
    public static string Mats(int scrap, int dust, int cores)
    {
        var parts = new List<string>(3);
        if (scrap > 0) parts.Add($"{ScrapName} ×{scrap}");
        if (dust > 0) parts.Add($"{DustName} ×{dust}");
        if (cores > 0) parts.Add($"{CoreName} ×{cores}");
        return parts.Count == 0 ? "沒有材料" : string.Join("、", parts);
    }

    /// <summary>"+12.5% 爆擊傷害" for an affix as it currently counts on its item.</summary>
    public static string AffixText(Item it, Affix a) => a == null ? "" : StatInfo.Format(a.Stat, it == null ? a.Value : it.Eff(a));
}
