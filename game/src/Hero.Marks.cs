using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>Bag marks kept in the save next to the fields declared in Save.cs / Crafting.cs.</summary>
public sealed partial class SaveData
{
    public HashSet<string> Discards = new();           // uids of bag items marked to be thrown away (丟棄); D.Junk holds the 回收 marks

    /// <summary>A copy sharing every list and item (GearEval swaps Equip on it); never saved.</summary>
    public SaveData ShallowCopy() => (SaveData)MemberwiseClone();
}

/// <summary>What a bag item is marked for: recycling at the smith (D.Junk, badge 收) or throwing away (D.Discards, badge 棄).</summary>
public enum BagMark { None, Recycle, Discard }

/// <summary>How an item compares with what the hero wears now.</summary>
public enum GearVerdict { NotGear, Unusable, Worse, Similar, Better }

/// <summary>
/// Bag marks, locks and the upgrade verdict. A locked item (Item.Locked) is never marked and never taken by a bulk
/// action; worn items are never in the bag, so never marked either. Recycle and discard marks exclude each other.
/// </summary>
public sealed partial class Hero
{
    private HashSet<string> DiscardSet => D.Discards ??= new HashSet<string>();

    public BagMark MarkOf(Item it) => it == null ? BagMark.None : IsJunk(it) ? BagMark.Recycle : DiscardSet.Contains(it.Uid) ? BagMark.Discard : BagMark.None;

    /// <summary>Why this item cannot take that mark (null when it can). Gems cannot be recycled (the smith takes gear only).</summary>
    public string WhyCantMark(Item it, BagMark m)
    {
        if (it == null) return "沒有選擇物品";
        if (!InBag(it)) return "只能標記行囊中的物品";
        if (IsProtected(it) && m != BagMark.None) return it.Locked ? "已鎖定的物品不會被標記" : "裝備組中的物品不會被標記（先從裝備組移除）";
        if (m == BagMark.Recycle && !it.IsEquip) return "寶石無法回收（可以標記丟棄）";
        return null;
    }

    /// <summary>Sets the item's mark (None clears it). Returns false when refused.</summary>
    public bool SetMark(Item it, BagMark m, bool notify = true)
    {
        if (WhyCantMark(it, m) != null) return false;
        JunkSet.Remove(it.Uid); DiscardSet.Remove(it.Uid);
        if (m == BagMark.Recycle) JunkSet.Add(it.Uid);
        else if (m == BagMark.Discard) DiscardSet.Add(it.Uid);
        if (notify) Notify();
        return true;
    }

    /// <summary>Marks one item, or clears the mark when it already has this one. Returns the new mark (unchanged when refused).</summary>
    public BagMark ToggleMark(Item it, BagMark m)
    {
        var cur = MarkOf(it);
        var next = cur == m ? BagMark.None : m;
        return SetMark(it, next) ? next : cur;
    }

    /// <summary>Marks every listed bag item that may take the mark (locked ones are skipped). Returns how many now carry it.</summary>
    public int MarkAll(IEnumerable<Item> items, BagMark m)
    {
        int n = 0;
        foreach (var it in items.ToList()) if (MarkOf(it) != m && SetMark(it, m, false)) n++;
        Notify();
        return n;
    }

    /// <summary>Clears the marks of the listed items (all bag items when null). Returns how many were cleared.</summary>
    public int ClearMarks(IEnumerable<Item> items = null)
    {
        int n = 0;
        foreach (var it in (items ?? D.Bag).ToList()) if (MarkOf(it) != BagMark.None) { JunkSet.Remove(it.Uid); DiscardSet.Remove(it.Uid); n++; }
        Notify();
        return n;
    }

    /// <summary>Locks or unlocks a carried, stashed or worn item; locking drops its mark. Returns the new state.</summary>
    public bool ToggleLock(Item it)
    {
        if (it == null) return false;
        it.Locked = !it.Locked;
        if (it.Locked) { JunkSet.Remove(it.Uid); DiscardSet.Remove(it.Uid); }
        Log.Info("inv", $"{(it.Locked ? "locked" : "unlocked")} {it.Name}");
        Notify();
        return it.Locked;
    }

    public List<Item> DiscardItems() => D.Bag.Where(i => DiscardSet.Contains(i.Uid) && !IsProtected(i)).ToList();
    public List<Item> RecycleItems() => JunkItems().Where(i => !IsProtected(i)).ToList();

    /// <summary>Throws away every item marked 丟棄 (gems set in them come back to the bag). Returns the count per rarity.</summary>
    public int[] DiscardMarked()
    {
        var by = new int[5];
        foreach (var it in DiscardItems())
            if (Discard(it)) { by[(int)it.Rarity]++; DiscardSet.Remove(it.Uid); }
        PruneMarks();
        Notify();
        return by;
    }

    /// <summary>Drops marks whose item has left the bag (both kinds).</summary>
    public void PruneMarks()
    {
        PruneJunk();
        var set = DiscardSet;
        if (set.Count == 0) return;
        var keep = new HashSet<string>();
        foreach (var it in D.Bag) if (set.Contains(it.Uid)) keep.Add(it.Uid);
        if (keep.Count != set.Count) set.IntersectWith(keep);
    }

    // ───────────────────────── upgrade verdict ─────────────────────────

    /// <summary>升級 / 相近 / 較差 / 無法使用 by the shared evaluator (GearEval: simulated swap, offence and defence); gems and worn items NotGear.</summary>
    public GearVerdict Judge(Item it) => GearEval.Judge(this, it).Verdict;

    /// <summary>False for a weapon or off-hand this class cannot hold at all, or another class's unique (level is not considered).</summary>
    public bool UsableByClass(Item it) => ItemDb.UsableBy(it, Class);
}

/// <summary>
/// The bag filter (行囊篩選): every set criterion must hold; within the rarity row any ticked rarity matches. An empty
/// filter matches everything. The three presets are plain settings of the same fields.
/// </summary>
public sealed class BagFilter
{
    public readonly bool[] Rarities = new bool[5];
    public int Kind = -1;               // index into Kinds, -1 any
    public int Usable;                  // 0 any, 1 usable by my class, 2 not usable by my class
    public int Verdict;                 // 0 any, 1 upgrade, 2 not an upgrade, 3 worse, 4 similar
    public int Below;                   // 0 any, 1 under my level, 2.. power under PowerSteps[Below-2]
    public Stat Affix = Stat.Count;     // Stat.Count = any
    public bool NewOnly;
    public string Preset;               // the preset these settings came from (null when hand-made)
    public bool Forgeable;              // [golem] 可鑄造魔像: metal items the iron golem can be forged from now (GolemForge.Refusal)

    public static readonly (string name, ItemKind kind)[] Kinds =
    {
        ("主手", ItemKind.Weapon), ("副手", ItemKind.OffHand), ("頭盔", ItemKind.Helm), ("胸甲", ItemKind.Chest), ("手套", ItemKind.Gloves),
        ("護腿", ItemKind.Pants), ("靴子", ItemKind.Boots), ("護符", ItemKind.Amulet), ("戒指", ItemKind.Ring), ("寶石", ItemKind.Gem),
    };
    public static readonly string[] UsableNames = { "職業：不限", "本職業可用", "非本職業" };
    public static readonly string[] VerdictNames = { "比較：不限", "升級", "非升級", "較差", "相近" };
    public static readonly int[] PowerSteps = { 100, 200, 300, 400, 500 };
    public static string BelowName(int i) => i == 0 ? "強度：不限" : i == 1 ? "低於目前等級" : $"強度低於 {PowerSteps[i - 2]}";
    public static int BelowCount => 2 + PowerSteps.Length;

    public bool Active => Rarities.Any(x => x) || Kind >= 0 || Usable != 0 || Verdict != 0 || Below != 0 || Affix != Stat.Count || NewOnly || Forgeable;

    public void Clear()
    {
        Array.Clear(Rarities); Kind = -1; Usable = 0; Verdict = 0; Below = 0; Affix = Stat.Count; NewOnly = false; Forgeable = false; Preset = null;
    }

    public bool Matches(Hero h, Item it, Func<Item, bool> isNew = null)
    {
        if (it == null) return false;
        if (Rarities.Any(x => x) && !Rarities[Math.Clamp((int)it.Rarity, 0, 4)]) return false;
        if (Kind >= 0 && it.Kind != Kinds[Kind].kind) return false;
        if (Usable == 1 && !h.UsableByClass(it)) return false;
        if (Usable == 2 && (h.UsableByClass(it) || !it.IsEquip)) return false;
        if (Verdict != 0)
        {
            var v = h.Judge(it);
            if (v == GearVerdict.NotGear) return false;
            bool ok = Verdict switch { 1 => v == GearVerdict.Better, 2 => v != GearVerdict.Better, 3 => v is GearVerdict.Worse or GearVerdict.Unusable, _ => v == GearVerdict.Similar };
            if (!ok) return false;
        }
        if (Below != 0 && !it.IsEquip) return false;          // a gem's power is its tier: level and power filters are for gear
        if (Below == 1 && it.Power >= h.D.Level * 10) return false;
        if (Below >= 2 && it.Power >= PowerSteps[Below - 2]) return false;
        if (Affix != Stat.Count && it.Affixes.All(a => a.Stat != Affix) && (it.Implicits == null || it.Implicits.All(a => a.Stat != Affix))) return false;
        if (NewOnly && (isNew == null || !isNew(it))) return false;
        if (Forgeable && GolemForge.Refusal(h, it) != null) return false;
        return true;
    }

    /// <summary>Applies a preset: "nonupgrade" 全部非升級, "lowcommon" 低於目前等級的普通與魔法, "otherclass" 非本職業.</summary>
    public void ApplyPreset(string id)
    {
        Clear();
        switch (id)
        {
            case "nonupgrade": Verdict = 2; break;
            case "lowcommon": Rarities[(int)Rarity.Common] = true; Rarities[(int)Rarity.Magic] = true; Below = 1; break;
            case "otherclass": Usable = 2; break;
            case "forgeable": Forgeable = true; break;          // [golem] Soulbinder only (InventoryUi shows it for that class)
            default: return;
        }
        Preset = id;
    }

    public static readonly (string id, string name)[] Presets = { ("nonupgrade", "全部非升級"), ("lowcommon", "低於目前等級的普通與魔法"), ("otherclass", "非本職業"), ("forgeable", "可鑄造魔像") };

    /// <summary>The bag items the filter shows (locked ones included; the bulk actions skip them).</summary>
    public List<Item> Pick(Hero h, Func<Item, bool> isNew = null) => h.D.Bag.Where(i => Matches(h, i, isNew)).ToList();
}
