using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>One gear set (裝備組): the items of all ten worn slots by uid, plus the skill bar and the specialisation.</summary>
public sealed class GearSetData
{
    public string Name = "";
    public string[] Uids = new string[SaveSystem.EquipSlots];       // by EquipSlot; null = the slot is empty in this set
    public string[] ItemNames = new string[SaveSystem.EquipSlots];  // for the ⚠ list once an item is gone
    public string[] Bar;                                            // skill bar (LMB, RMB, 1–4); null = not stored
    public string Spec;                                             // class specialisation; null = not stored
    public bool Empty => Uids == null || Uids.All(u => u == null);
}

/// <summary>Gear sets kept in the save next to the fields declared in Save.cs (old saves load with none).</summary>
public sealed partial class SaveData
{
    public List<GearSetData> GearSets = new();
    public int SetActive = -1, SetPrev = -1;                         // the last two loaded (E swaps between them)
}

/// <summary>What loading a set did.</summary>
public sealed class SetLoadResult
{
    public bool Ok;
    public string Why;                          // refused: the reason (bag full …)
    public readonly List<string> Missing = new();  // items of the set that were not found (sold, salvaged, in the stash away from town)
    public int FromStash, Moved;
}

/// <summary>
/// Gear sets: six per hero, saved by item uid (nothing is duplicated). Loading equips every item of the set that is
/// worn, carried, or — in town — in the stash (pulled out on the way); what is missing is reported and its slot keeps
/// what it holds. The items that come off go to the bag: with no room the load is refused, never dropped.
/// Skill bar and specialisation go with the set (respec is free in this game, so switching them is too); skill ranks and
/// upgrades are not part of a set. Items in any set count as locked (IsProtected) for every bulk action.
/// </summary>
public sealed partial class Hero
{
    public const int SetCount = 6;

    public GearSetData SetAt(int i)
    {
        D.GearSets ??= new();
        while (D.GearSets.Count < SetCount) D.GearSets.Add(new GearSetData());
        var s = D.GearSets[Math.Clamp(i, 0, SetCount - 1)];
        if (s.Uids == null || s.Uids.Length != SaveSystem.EquipSlots) { var u = new string[SaveSystem.EquipSlots]; if (s.Uids != null) Array.Copy(s.Uids, u, Math.Min(u.Length, s.Uids.Length)); s.Uids = u; }
        if (s.ItemNames == null || s.ItemNames.Length != SaveSystem.EquipSlots) { var n = new string[SaveSystem.EquipSlots]; if (s.ItemNames != null) Array.Copy(s.ItemNames, n, Math.Min(n.Length, s.ItemNames.Length)); s.ItemNames = n; }
        return s;
    }

    public string SetName(int i) { var s = SetAt(i); return string.IsNullOrWhiteSpace(s.Name) ? $"裝備組 {i + 1}" : s.Name; }

    /// <summary>True when the item belongs to any saved set.</summary>
    public bool InSet(Item it)
    {
        if (it == null || D.GearSets == null) return false;
        foreach (var s in D.GearSets) if (s?.Uids != null && Array.IndexOf(s.Uids, it.Uid) >= 0) return true;
        return false;
    }

    /// <summary>Never taken by a bulk action (mark, recycle, discard, sell, salvage by bots): locked by the player or part of a gear set.</summary>
    public bool IsProtected(Item it) => it != null && (it.Locked || InSet(it));

    /// <summary>Indexes of the sets an item belongs to (for its badge and tooltip).</summary>
    public List<int> SetsOf(Item it)
    {
        var l = new List<int>();
        if (it == null) return l;
        for (int i = 0; i < SetCount; i++) if (Array.IndexOf(SetAt(i).Uids, it.Uid) >= 0) l.Add(i);
        return l;
    }

    /// <summary>Saves what is worn now (and the skill bar and specialisation) into set <paramref name="i"/>.</summary>
    public void SaveSet(int i)
    {
        var s = SetAt(i);
        for (int k = 0; k < SaveSystem.EquipSlots; k++) { var it = D.Equip[k]; s.Uids[k] = it?.Uid; s.ItemNames[k] = it?.Name; }
        s.Bar = (string[])D.Bar.Clone();
        s.Spec = D.Spec;
        foreach (var it in D.Equip) if (it != null) { JunkSet.Remove(it.Uid); DiscardSet.Remove(it.Uid); }
        D.SetActive = i;
        Log.Info("sets", $"saved set {i + 1} '{SetName(i)}': {string.Join(",", s.Uids.Select(u => u ?? "-"))}");
        PreloadSetModels();
        Notify();
    }

    public void ClearSet(int i)
    {
        var s = SetAt(i);
        s.Uids = new string[SaveSystem.EquipSlots]; s.ItemNames = new string[SaveSystem.EquipSlots]; s.Bar = null; s.Spec = null;
        if (D.SetActive == i) D.SetActive = -1;
        if (D.SetPrev == i) D.SetPrev = -1;
        PreloadSetModels();
        Notify();
    }

    public void RenameSet(int i, string name)
    {
        name = (name ?? "").Trim();
        if (name.Length > 12) name = name[..12];
        SetAt(i).Name = name;
        PreloadSetModels();
        Notify();
    }

    /// <summary>Where the item of this uid is now: worn (slot), in the bag, in the stash — or null.</summary>
    private Item FindOwned(string uid, bool stash, out int worn, out bool inStash)
    {
        worn = -1; inStash = false;
        if (uid == null) return null;
        for (int k = 0; k < D.Equip.Length; k++) if (D.Equip[k]?.Uid == uid) { worn = k; return D.Equip[k]; }
        foreach (var it in D.Bag) if (it.Uid == uid) return it;
        if (stash) foreach (var it in D.Stash) if (it.Uid == uid) { inStash = true; return it; }
        return null;
    }

    /// <summary>The worn set of slots the set would give (missing items keep the slot's current item), and the bookkeeping.</summary>
    public Item[] SetLayout(int i, bool stash, List<string> missing = null)
    {
        var s = SetAt(i);
        var e = (Item[])D.Equip.Clone();
        var kept = new bool[e.Length];
        for (int k = 0; k < e.Length; k++)
        {
            string uid = s.Uids[k];
            if (uid == null) { e[k] = null; continue; }
            var it = FindOwned(uid, stash, out _, out _);
            if (it == null) { missing?.Add(s.ItemNames[k] ?? "?"); kept[k] = true; continue; }       // keep what the slot holds
            e[k] = it;
        }
        // an item may now sit in two slots (a ring kept in Ring1 that the set puts in Ring2): it stays only where the set wants it
        for (int k = 0; k < e.Length; k++)
        {
            if (!kept[k] || e[k] == null) continue;
            for (int j = 0; j < e.Length; j++) if (j != k && e[j] == e[k]) { e[k] = null; break; }
        }
        // hands stay legal: a two-hand weapon clears the off hand (the set's own choice wins over a kept item)
        int mh = (int)EquipSlot.MainHand, oh = (int)EquipSlot.OffHand;
        if (e[mh] != null && e[mh].TwoHand && e[oh] != null) { if (kept[mh]) e[mh] = null; else e[oh] = null; }
        return e;
    }

    /// <summary>Loads set <paramref name="i"/> (see the class remarks). <paramref name="stash"/>: the stash counts (in town).</summary>
    public SetLoadResult LoadSet(int i, bool stash)
    {
        var r = new SetLoadResult();
        var s = SetAt(i);
        if (s.Empty) { r.Why = $"「{SetName(i)}」尚未儲存裝備"; return r; }
        var e = SetLayout(i, stash, r.Missing);
        // what leaves the body goes to the bag; what comes from the bag frees its cell
        var outgoing = D.Equip.Where(x => x != null && Array.IndexOf(e, x) < 0).ToList();
        var fromBag = e.Where(x => x != null && D.Bag.Contains(x)).Distinct().ToList();
        var fromStash = e.Where(x => x != null && D.Stash.Contains(x)).Distinct().ToList();
        int after = D.Bag.Count - fromBag.Count + outgoing.Count;
        if (after > BagSize) { r.Why = $"行囊空間不足：換下的裝備需要 {after - BagSize} 格空位"; return r; }
        foreach (var x in fromBag) D.Bag.Remove(x);
        foreach (var x in fromStash) { D.Stash.Remove(x); r.FromStash++; }
        for (int k = 0; k < e.Length; k++) { if (D.Equip[k] != e[k]) r.Moved++; D.Equip[k] = e[k]; }
        foreach (var x in outgoing) D.Bag.Add(x);
        foreach (var x in e) if (x != null) { JunkSet.Remove(x.Uid); DiscardSet.Remove(x.Uid); }
        // the skill bar (only skills still learned) and the specialisation (free to switch, as respec is)
        if (s.Bar != null && s.Bar.Length == 6)
            for (int b = 0; b < 6; b++)
                D.Bar[b] = s.Bar[b] == null ? null : BaseRank(s.Bar[b]) > 0 ? s.Bar[b] : D.Bar[b];      // a skill unlearned since keeps today's binding
        if (s.Spec != null && s.Spec != D.Spec && D.Level >= SkillDb.SpecLevel && SkillDb.SpecsOf(Class).Any(x => x.Id == s.Spec)) D.Spec = s.Spec;
        if (D.SetActive != i) { D.SetPrev = D.SetActive; D.SetActive = i; }
        r.Ok = true;
        Recalc();
        Log.Info("sets", $"loaded set {i + 1} '{SetName(i)}': moved {r.Moved}, from stash {r.FromStash}, missing {r.Missing.Count}; main={MainHand?.Base} off={OffHand?.Base}");
        return r;
    }

    /// <summary>Items of a set that cannot be found right now (stash counts only when <paramref name="stash"/>).</summary>
    public List<string> MissingOf(int i, bool stash)
    {
        var l = new List<string>();
        var s = SetAt(i);
        for (int k = 0; k < s.Uids.Length; k++) if (s.Uids[k] != null && FindOwned(s.Uids[k], stash, out _, out _) == null) l.Add(s.ItemNames[k] ?? "?");
        return l;
    }

    /// <summary>A copy of the hero (no buffs) wearing set <paramref name="i"/> as LoadSet would put it on (stash counted).</summary>
    /// <summary>RENDER: keeps the models of every saved set loaded and pinned (GearCatalog.PreloadSets), so a swap is built in one frame.</summary>
    public void PreloadSetModels()
    {
        try
        {
            var sets = new List<IEnumerable<Item>>();
            for (int k = 0; k < SetCount; k++) { var s = SetAt(k); if (!s.Empty) sets.Add(SetLayout(k, true)); }
            GearCatalog.PreloadSets("hero-sets", Class, sets);
        }
        catch (Exception e) { Log.Error("sets", "preloading the set models failed", e); }
    }

    public Hero PreviewSet(int i)
    {
        var d = D.ShallowCopy();
        d.Equip = SetLayout(i, true);
        return new Hero(d);
    }
}
