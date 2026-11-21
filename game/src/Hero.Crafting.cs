using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// Item handling and crafting rules that sit on top of Hero.cs: junk marks, sorting, bulk selling and salvaging,
/// socket and enchant validation with reasons, gem combining and tempering. Every "WhyCant…" returns a zh-TW reason
/// (null = allowed); every action re-validates, applies, logs and raises <see cref="Changed"/> (through Recalc when
/// the stats can be affected).
/// </summary>
public sealed partial class Hero
{
    // ───────────────────────── ownership ─────────────────────────

    public int BagFree => BagSize - D.Bag.Count;
    public int StashFree => StashSize - D.Stash.Count;
    public bool InBag(Item it) => it != null && D.Bag.Contains(it);
    public bool IsEquipped(Item it) => it != null && Array.IndexOf(D.Equip, it) >= 0;
    /// <summary>True for items the crafters can work on: worn or carried (not stashed).</summary>
    public bool Owns(Item it) => InBag(it) || IsEquipped(it);

    /// <summary>After an item was altered: worn gear changes the stats, carried gear only the display.</summary>
    private void AfterCraft(Item it) { if (IsEquipped(it)) Recalc(); else Notify(); }

    // ───────────────────────── junk marks and sorting ─────────────────────────

    private HashSet<string> JunkSet => D.Junk ??= new HashSet<string>();

    public bool IsJunk(Item it) => it != null && JunkSet.Contains(it.Uid);

    /// <summary>Marks or unmarks a carried item as junk; returns the new state.</summary>
    public bool ToggleJunk(Item it)
    {
        if (!InBag(it) || IsProtected(it)) return false;    // locked (鎖定) or gear-set items take no mark
        bool now = !JunkSet.Remove(it.Uid);
        if (now) JunkSet.Add(it.Uid);
        Notify();
        return now;
    }

    public void ClearJunk(Item it) { if (it != null) JunkSet.Remove(it.Uid); }

    public List<Item> JunkItems()
    {
        var l = new List<Item>();
        var set = JunkSet;
        if (set.Count == 0) return l;
        foreach (var it in D.Bag) if (set.Contains(it.Uid)) l.Add(it);
        return l;
    }

    /// <summary>Drops marks whose item has left the bag (sold, equipped, stashed, salvaged elsewhere).</summary>
    public void PruneJunk()
    {
        var set = JunkSet;
        if (set.Count == 0) return;
        var keep = new HashSet<string>();
        foreach (var it in D.Bag) if (set.Contains(it.Uid)) keep.Add(it.Uid);
        if (keep.Count != set.Count) set.IntersectWith(keep);
    }

    public void SortBag()
    {
        D.Bag.Sort(Crafting.CompareItems);
        PruneJunk();
        Notify();
    }

    public void SortStash()
    {
        D.Stash.Sort(Crafting.CompareItems);
        Notify();
    }

    // ───────────────────────── equip / stash / trade reasons ─────────────────────────

    public string WhyCantUnequip(EquipSlot slot)
    {
        if (D.Equip[(int)slot] == null) return "這個欄位沒有裝備";
        if (BagFull) return "行囊已滿";
        return null;
    }

    public string WhyCantStash(Item it)
    {
        if (!InBag(it)) return "只能存入行囊中的物品";
        if (D.Stash.Count >= StashSize) return "儲藏箱已滿";
        return null;
    }

    public string WhyCantWithdraw(Item it)
    {
        if (it == null || !D.Stash.Contains(it)) return "儲藏箱裡沒有這件物品";
        if (BagFull) return "行囊已滿";
        return null;
    }

    /// <summary>Bag → stash, dropping any junk mark on the way.</summary>
    public bool StashItem(Item it)
    {
        if (WhyCantStash(it) != null) return false;
        JunkSet.Remove(it.Uid);
        return ToStash(it);
    }

    public string WhyCantBuy(int price)
    {
        if (D.Gold < price) return "金幣不足";
        if (BagFull) return "行囊已滿";
        return null;
    }

    public string WhyCantSell(Item it) => InBag(it) ? null : "只能賣出行囊中的物品";

    public bool SellItem(Item it)
    {
        if (WhyCantSell(it) != null) return false;
        JunkSet.Remove(it.Uid);
        Sell(it);
        Log.Info("trade", $"sold {it.Name} ({it.Rarity}, power {it.Power}) for {it.Value}; gold={D.Gold}");
        return true;
    }

    /// <summary>Sells every listed item that is in the bag; returns how many were sold and the gold earned.</summary>
    public (int count, long gold) SellMany(IEnumerable<Item> items)
    {
        int n = 0; long gold = 0;
        foreach (var it in items.ToList())
        {
            int v = it?.Value ?? 0;
            if (!SellItem(it)) continue;
            n++; gold += v;
        }
        return (n, gold);
    }

    // ───────────────────────── salvage ─────────────────────────

    /// <summary>Why an item cannot be thrown away right now (null = it can). Its socketed gems come back to the bag.</summary>
    public string WhyCantDiscard(Item it)
    {
        if (it == null) return "沒有選擇物品";
        if (IsEquipped(it)) return "請先卸下這件裝備";
        if (!D.Bag.Contains(it)) return "只能丟棄行囊中的物品";
        if (D.Bag.Count - 1 + it.Gems.Count > BagSize) return "行囊空間不足，無法收回鑲嵌的寶石";
        return null;
    }

    /// <summary>Takes an item out of the bag for good (the caller may drop it on the ground). Socketed gems stay in the bag.</summary>
    public bool Discard(Item it)
    {
        if (WhyCantDiscard(it) != null) return false;
        int at = D.Bag.IndexOf(it);
        D.Bag.RemoveAt(at);
        JunkSet.Remove(it.Uid);
        foreach (var g in it.Gems) D.Bag.Add(ItemDb.MakeGem(g));
        int gems = it.Gems.Count;
        it.Gems.Clear();
        Log.Info("inv", $"discarded {it.Name} ({it.Rarity}, power {it.Power}){(gems > 0 ? $"; {gems} gem(s) back to the bag" : "")}");
        Notify();
        return true;
    }

    public string WhyCantSalvage(Item it)
    {
        if (it == null) return "沒有選擇物品";
        if (!it.IsEquip) return "寶石無法拆解";
        if (IsEquipped(it)) return "請先卸下這件裝備";
        if (!D.Bag.Contains(it)) return "只能拆解行囊中的裝備";
        if (D.Bag.Count - 1 + it.Gems.Count > BagSize) return "行囊空間不足，無法收回鑲嵌的寶石";
        return null;
    }

    /// <summary>
    /// Salvages every listed item that may be salvaged. Materials are added, set gems return to the bag and
    /// legendary powers enter the codex (see <see cref="Salvage"/>). <c>Powers</c> lists the names of powers that were new or improved.
    /// </summary>
    public SalvageSummary SalvageMany(IEnumerable<Item> items)
    {
        var res = new SalvageSummary();
        foreach (var it in items.ToList())
        {
            if (WhyCantSalvage(it) != null) continue;
            bool improves = it.Aspect != null && (!D.Codex.TryGetValue(it.Aspect, out float best) || it.AspectRoll > best);
            int gems = it.Gems.Count;
            var y = Salvage(it);
            JunkSet.Remove(it.Uid);
            D.Enchants.Remove(it.Uid);
            res.Count++; res.Scrap += y.scrap; res.Dust += y.dust; res.Cores += y.cores; res.Gems += gems;
            if (it.Rarity >= Rarity.Legendary) res.Precious++;
            if (improves)
            {
                var asp = ItemDb.Aspect(it.Aspect);
                if (asp != null) res.Powers.Add(asp.Name);
            }
            Log.Info("craft", $"salvaged {it.Name} ({it.Rarity}, power {it.Power}) → scrap {y.scrap} dust {y.dust} cores {y.cores}{(improves ? $" codex+={it.Aspect}@{it.AspectRoll:0.00}" : "")}");
        }
        return res;
    }

    // ───────────────────────── enchanting (occultist) ─────────────────────────

    /// <summary>Why this item (and, when <paramref name="index"/> ≥ 0, this affix) cannot be enchanted right now.</summary>
    public string WhyCantEnchant(Item it, int index = -1, bool checkCost = true)
    {
        if (!Owns(it)) return "找不到這件裝備";
        if (!it.IsEquip) return "寶石無法附魔";
        if (it.Rarity == Rarity.Unique) return "獨特裝備的詞綴無法重鑄";
        if (it.Affixes.Count == 0) return "這件裝備沒有可重鑄的詞綴";
        if (it.Affixes.All(a => a.Tempered)) return "這件裝備只有淬火詞綴，請回鐵匠處重淬";
        if (index >= 0)
        {
            if (index >= it.Affixes.Count) return "找不到這條詞綴";
            if (it.Affixes[index].Tempered) return "淬火詞綴無法附魔，請回鐵匠處重淬";
        }
        if (!checkCost) return null;
        if (D.Gold < EnchantCost(it)) return "金幣不足";
        if (D.Dust < ItemDb.EnchantDustCost) return "幽塵不足";
        return null;
    }

    /// <summary>
    /// Pays for one enchant attempt and returns the candidates to choose from (two when the pools allow, never the
    /// same stat twice). Null when refused. The caller commits one with <see cref="CommitEnchant"/> or keeps the original.
    /// </summary>
    public Affix[] BeginEnchant(Item it, int index)
    {
        if (WhyCantEnchant(it, index) != null) return null;
        int cost = EnchantCost(it);
        var rolled = EnchantRoll(it, index);
        if (rolled == null) return null;
        var a = rolled.Length > 0 ? rolled[0] : null;
        var b = rolled.Length > 1 ? rolled[1] : null;
        for (int tries = 0; tries < 8 && a != null && b != null && b.Stat == a.Stat; tries++)
            b = ItemDb.RerollAffix(it, index, Class);
        if (a != null && b != null && b.Stat == a.Stat) b = null;
        var list = new List<Affix>(2);
        if (a != null) list.Add(a);
        if (b != null) list.Add(b);
        Log.Info("craft", $"enchant roll on {it.Name} affix {index} ({it.Affixes[index].Stat}) cost {cost} gold → {string.Join(" | ", list.Select(x => $"{x.Stat} {x.Value}"))}");
        return list.ToArray();
    }

    /// <summary>Replaces the affix with the chosen candidate. Masterworking milestone boosts stay with the slot.</summary>
    public void CommitEnchant(Item it, int index, Affix pick)
    {
        if (it == null || pick == null || index < 0 || index >= it.Affixes.Count) return;
        var old = it.Affixes[index];
        pick.Bonus = old.Bonus;
        pick.Tempered = false;
        Log.Info("craft", $"enchant applied on {it.Name}: {old.Stat} {old.Value} → {pick.Stat} {pick.Value}");
        ApplyEnchant(it, index, pick);
    }

    // ───────────────────────── sockets and gems (jeweler) ─────────────────────────

    public const int SocketDustCost = 5;

    public static int MaxSockets(ItemKind k) =>
        k is ItemKind.Weapon or ItemKind.Chest ? 2 : k is ItemKind.Gloves or ItemKind.Boots or ItemKind.Gem ? 0 : 1;

    public string WhyCantAddSocket(Item it)
    {
        if (!Owns(it)) return "找不到這件裝備";
        if (!it.IsEquip) return "寶石上無法開鑿插槽";
        int max = MaxSockets(it.Kind);
        if (max == 0) return $"{Names.Kind(it.Kind)}無法開鑿插槽";
        if (it.Sockets >= max) return "插槽數量已達上限";
        if (D.Gold < SocketGoldCost) return "金幣不足";
        if (D.Dust < SocketDustCost) return "幽塵不足";
        return null;
    }

    public string WhyCantSocket(Item gem, Item target)
    {
        if (gem == null || gem.Kind != ItemKind.Gem || !D.Bag.Contains(gem)) return "請先從行囊中選擇一顆寶石";
        if (target == null || !target.IsEquip) return "寶石只能鑲嵌在裝備上";
        if (!Owns(target)) return "找不到這件裝備";
        if (target.Sockets <= 0) return "這件裝備沒有插槽";
        if (target.Gems.Count >= target.Sockets) return "這件裝備沒有空插槽";
        return null;
    }

    public string WhyCantUnsocket(Item target)
    {
        if (!Owns(target)) return "找不到這件裝備";
        if (target.Gems.Count == 0) return "這件裝備沒有鑲嵌寶石";
        if (D.Bag.Count + target.Gems.Count > BagSize) return "行囊空間不足";
        return null;
    }

    public int GemCount(string gemId)
    {
        int n = 0;
        foreach (var it in D.Bag) if (it.Kind == ItemKind.Gem && it.GemId == gemId) n++;
        return n;
    }

    public string WhyCantCombineGems(string gemId)
    {
        var g = ItemDb.Gem(gemId);
        if (g == null) return "未知的寶石";
        if (Crafting.NextGem(gemId) == null) return "這已是最高階的寶石";
        if (GemCount(gemId) < Crafting.GemsPerCombine) return $"需要 {Crafting.GemsPerCombine} 顆{g.Name}";
        if (D.Gold < Crafting.GemCombineGold(g)) return "金幣不足";
        if (D.Dust < Crafting.GemCombineDust(g)) return "幽塵不足";
        return null;
    }

    /// <summary>Fuses three loose gems of one kind and tier into one of the next tier. Returns the new gem, or null when refused.</summary>
    public Item CombineGems(string gemId)
    {
        if (WhyCantCombineGems(gemId) != null) return null;
        var g = ItemDb.Gem(gemId);
        var next = Crafting.NextGem(gemId);
        D.Gold -= Crafting.GemCombineGold(g);
        D.Dust -= Crafting.GemCombineDust(g);
        int at = -1;
        for (int n = 0; n < Crafting.GemsPerCombine; n++)
        {
            int i = D.Bag.FindIndex(x => x.Kind == ItemKind.Gem && x.GemId == gemId);
            if (i < 0) break;
            if (at < 0) at = i;
            JunkSet.Remove(D.Bag[i].Uid);
            D.Bag.RemoveAt(i);
        }
        var made = ItemDb.MakeGem(next.Id);
        D.Bag.Insert(Math.Clamp(at, 0, D.Bag.Count), made);
        Log.Info("craft", $"combined {Crafting.GemsPerCombine}× {g.Id} → {next.Id}; gold={D.Gold} dust={D.Dust}");
        Notify();
        return made;
    }

    // ───────────────────────── tempering (blacksmith) ─────────────────────────

    /// <summary>
    /// Why the item cannot be tempered with this recipe in this slot (0 or 1). A rare holds one tempered affix, a
    /// legendary two; each attempt spends one of the item's charges plus gold and materials.
    /// </summary>
    public string WhyCantTemper(Item it, string recipeId, int slot)
    {
        if (!Owns(it)) return "找不到這件裝備";
        if (!it.IsEquip) return "寶石無法淬火";
        int slots = Crafting.TemperSlots(it);
        if (slots == 0) return it.Rarity == Rarity.Unique ? "獨特裝備已成定局，無法淬火" : "只有稀有或傳奇裝備承受得住淬火";
        if (slot < 0 || slot >= slots) return it.Rarity == Rarity.Rare ? "稀有裝備只能承受一道淬火詞綴" : "傳奇裝備最多承受兩道淬火詞綴";
        if (slot > Crafting.TemperedCount(it)) return "請先淬鍊前一道淬火位";
        if (it.TemperLeft <= 0) return "這件裝備的淬火次數已經耗盡";
        var r = Crafting.Recipe(recipeId);
        if (r == null) return "請先選擇一種淬火配方";
        if (!r.ForClass(Class)) return $"「{r.Name}」僅傳授給{Names.Class((ClassId)r.Class)}";
        if (!r.Allows(it.Kind)) return $"「{r.Name}」無法用於{Names.Kind(it.Kind)}";
        if (Crafting.TemperCandidates(r, it, Crafting.TemperedIndex(it, slot)).Count == 0) return "這件裝備已具備此配方的所有詞綴";
        if (D.Gold < Crafting.TemperGoldCost(it)) return "金幣不足";
        if (D.Scrap < Crafting.TemperScrapCost(it)) return "碎鐵不足";
        if (D.Dust < Crafting.TemperDustCost(it)) return "幽塵不足";
        if (D.Cores < Crafting.TemperCoreCost(it, slot)) return "燼核不足";
        return null;
    }

    /// <summary>
    /// One tempering attempt: rolls a random affix of the recipe's category, sized by the item's power, and puts it in
    /// the slot (replacing what was tempered there before). Returns the new affix, or null when refused.
    /// </summary>
    public Affix Temper(Item it, string recipeId, int slot)
    {
        if (WhyCantTemper(it, recipeId, slot) != null) return null;
        var r = Crafting.Recipe(recipeId);
        int idx = Crafting.TemperedIndex(it, slot);
        var a = Crafting.RollTemper(r, it, idx);
        if (a == null) return null;
        int gold = Crafting.TemperGoldCost(it), scrap = Crafting.TemperScrapCost(it), dust = Crafting.TemperDustCost(it), cores = Crafting.TemperCoreCost(it, slot);
        D.Gold -= gold; D.Scrap -= scrap; D.Dust -= dust; D.Cores -= cores;
        it.TemperLeft--;
        string was = "empty";
        if (idx >= 0)
        {
            var old = it.Affixes[idx];
            was = $"{old.Stat} {old.Value}";
            a.Bonus = old.Bonus;                 // masterworking boosts belong to the slot, not to the roll
            it.Affixes[idx] = a;
        }
        else it.Affixes.Add(a);
        Log.Info("craft", $"tempered {it.Name} slot {slot} with {r.Id}: {was} → {a.Stat} {a.Value}{(a.Greater ? " (greater)" : "")}; charges left {it.TemperLeft}; paid gold {gold} scrap {scrap} dust {dust} cores {cores}");
        AfterCraft(it);
        return a;
    }
}
