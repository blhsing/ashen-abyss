using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

public sealed class ActiveBuff
{
    public BuffDef Def;
    public float Left;
    public int Stacks = 1;
}

/// <summary>
/// The character sheet: turns the saved character (level, gear, skills, passives, board, buffs) into final stats,
/// and owns every rule about spending points, equipping, and crafting. No engine types — the Player node drives it.
/// </summary>
public sealed partial class Hero
{
    public readonly SaveData D;
    public readonly StatBlock S = new();
    public readonly List<ActiveBuff> Buffs = new();
    public readonly Dictionary<string, float> Aspects = new();   // active legendary powers → value
    public readonly HashSet<string> Uniques = new();
    public readonly HashSet<string> Keystones = new();          // behaviour hooks granted by the paragon board (filled by ApplyParagonStats)
    public float MaxLife, MaxResource, WeaponMin, WeaponMax, Aps, MainMul, Armor;
    public int MaxPips;
    public const int BagSize = 40, StashSize = 80;

    public event Action Changed;                 // stats or inventory changed → UI refresh
    public event Action<int> LevelUp;
    public event Action<int> ParagonUp;
    private readonly Dictionary<string, SkillDef> _eff = new();

    public Hero(SaveData d) { D = d; Recalc(); }

    public ClassId Class => D.Class;
    public ClassSpec Spec => SkillDb.Spec(D.Spec);
    public Item Weapon => D.Equip[(int)EquipSlot.Weapon];

    public static SaveData NewCharacter(string name, ClassId cls, bool hardcore)
    {
        var d = new SaveData { Name = name, Class = cls, Hardcore = hardcore };
        string basic = SkillDb.DefaultBasic(cls);
        d.Skills[basic] = 1;
        d.Bar[0] = basic;
        d.Equip[(int)EquipSlot.Weapon] = ItemDb.Starter(cls);
        d.Gold = 50;
        return d;
    }

    // ───────────────────────── stats ─────────────────────────

    public void Recalc()
    {
        S.Clear(); Aspects.Clear(); Uniques.Clear(); Keystones.Clear(); _eff.Clear();
        var main = Names.MainStat(Class);
        foreach (var st in new[] { Stat.Str, Stat.Int, Stat.Dex, Stat.Wil }) S[st] = 7 + D.Level;
        S[main] += D.Level;
        int altars = D.Flags.Count(f => f.StartsWith("altar_"));      // each 燼火祭壇 found: +1 to every attribute, permanently
        foreach (var st in new[] { Stat.Str, Stat.Int, Stat.Dex, Stat.Wil }) S[st] += altars;
        S[Stat.CritChance] = 5; S[Stat.CritDmg] = 0;

        float armor = 0;
        foreach (var it in D.Equip)
        {
            if (it == null) continue;
            armor += it.Armor * it.BaseMul;
            foreach (var a in it.Affixes) S.Add(a.Stat, it.Eff(a));
            foreach (var g in it.Gems) { var gd = ItemDb.Gem(g); if (gd != null) { var b = ItemDb.GemBonus(gd, it.Kind); S.Add(b.s, b.v); } }
            if (it.Aspect != null)
            {
                var asp = ItemDb.Aspect(it.Aspect);
                if (asp != null)
                {
                    float v = asp.Value(it.AspectRoll) * (it.Kind == ItemKind.Amulet ? 1.5f : it.Kind == ItemKind.Weapon ? 2f : 1f);
                    if (asp.Id == "multishot") v = asp.Value(it.AspectRoll);
                    Aspects[asp.Id] = Aspects.TryGetValue(asp.Id, out var cur) ? MathF.Max(cur, v) : v;
                }
            }
            if (it.Unique != null) Uniques.Add(it.Unique);
        }
        foreach (var p in SkillDb.PassivesOf(Class))
        {
            int r = D.Skills.GetValueOrDefault(p.Id);
            if (r > 0) foreach (var (s, v) in p.PerRank) S.Add(s, v * r);
        }
        if (Spec != null) foreach (var (s, v) in Spec.Stats) S.Add(s, v);
        ApplyParagonStats();
        foreach (var b in Buffs) if (b.Def.Stat != Stat.Count) S.Add(b.Def.Stat, b.Def.Value * b.Stacks);
        if (Uniques.Contains("u_emberheart")) S.Add(Stat.DmgAll, 35);
        S.Add(Stat.Thorns, S[Stat.Thorns] * Asp("thornmail") / 100f);

        // attributes → derived
        armor += S[Stat.Armor] + S[Stat.Str];
        Armor = armor;
        S.Add(Stat.ResAll, S[Stat.Int] * 0.04f);
        S.Add(Stat.Dodge, S[Stat.Dex] * 0.015f);
        S.Add(Stat.CritChance, S[Stat.Dex] * 0.01f);
        S.Add(Stat.Healing, S[Stat.Wil] * 0.08f);
        S.Add(Stat.ResourceGen, S[Stat.Wil] * 0.05f);
        MainMul = 1f + S[main] * 0.0025f;

        S[Stat.Dodge] = MathF.Min(S[Stat.Dodge], 50); S[Stat.DmgReduction] = MathF.Min(S[Stat.DmgReduction], 75);
        S[Stat.Cdr] = MathF.Min(S[Stat.Cdr], 60); S[Stat.MoveSpeed] = MathF.Min(S[Stat.MoveSpeed], 100);
        S[Stat.AttackSpeed] = MathF.Min(S[Stat.AttackSpeed], 100); S[Stat.ResourceCost] = MathF.Min(S[Stat.ResourceCost], 50);
        S[Stat.CritChance] = MathF.Min(S[Stat.CritChance], 75);

        MaxLife = MathF.Round((Balance.BaseLife(D.Level) + S[Stat.MaxLife]) * MathF.Max(0.2f, 1f + S[Stat.LifePct] / 100f));
        MaxResource = 100 + S[Stat.MaxResource];
        var w = Weapon;
        if (w != null) { float up = w.BaseMul; WeaponMin = w.DmgMin * up; WeaponMax = w.DmgMax * up; Aps = w.Aps; }
        else { WeaponMin = 2; WeaponMax = 4; Aps = 1f; }
        MaxPips = Class == ClassId.Hunter ? 3 + (D.Spec == "hu_combo" ? 1 : 0) + (HasKeystone("ks_combo") ? 2 : 0) : Class == ClassId.Pactbinder ? 5 : 0;
        Changed?.Invoke();
    }

    public float Asp(string id) => Aspects.GetValueOrDefault(id);
    public bool HasAsp(string id) => Aspects.ContainsKey(id);
    public bool HasUnique(string id) => Uniques.Contains(id);
    public bool HasKey(string passiveId) => D.Skills.GetValueOrDefault(passiveId) > 0;
    public bool HasKeystone(string ks) => Keystones.Contains(ks);
    /// <summary>Implemented by the paragon module (Hero.Paragon.cs): adds board and glyph stats to S and hook ids to Keystones.</summary>
    partial void ApplyParagonStats();
    public float Res(Element e) { var s = StatInfo.ResOf(e); return s == Stat.Count ? 0 : MathF.Min(70, S[Stat.ResAll] + S[s]); }
    public float AttackSpeedMul => 1f + S[Stat.AttackSpeed] / 100f;
    public float CdrMul => 1f - S[Stat.Cdr] / 100f;
    public float MoveMul => 1f + S[Stat.MoveSpeed] / 100f;

    // ───────────────────────── buffs ─────────────────────────

    public ActiveBuff AddBuff(BuffDef def, float dur)
    {
        var b = Buffs.FirstOrDefault(x => x.Def.Id == def.Id);
        if (b != null) { b.Left = dur; if (b.Stacks < def.MaxStacks) b.Stacks++; b.Def = def; }
        else { b = new ActiveBuff { Def = def, Left = dur }; Buffs.Add(b); }
        Recalc();
        return b;
    }

    public void RemoveBuffs(Predicate<ActiveBuff> p) { if (Buffs.RemoveAll(p) > 0) Recalc(); }
    public bool BuffFlag(Func<BuffDef, bool> f) { foreach (var b in Buffs) if (f(b.Def)) return true; return false; }
    public float BuffMax(Func<BuffDef, float> f) { float m = 0; foreach (var b in Buffs) m = MathF.Max(m, f(b.Def)); return m; }
    public float BuffSum(Func<BuffDef, float> f) { float m = 0; foreach (var b in Buffs) m += f(b.Def); return m; }

    /// <summary>Advances buff timers; returns the buffs that just expired.</summary>
    public List<ActiveBuff> TickBuffs(float dt)
    {
        List<ActiveBuff> ended = null;
        for (int i = Buffs.Count - 1; i >= 0; i--)
        {
            var b = Buffs[i];
            if (b.Def.Aura) continue;
            b.Left -= dt;
            if (b.Left <= 0) { (ended ??= new()).Add(b); Buffs.RemoveAt(i); }
        }
        if (ended != null) Recalc();
        return ended;
    }

    // ───────────────────────── skills ─────────────────────────

    public int BaseRank(string id) => D.Skills.GetValueOrDefault(id);
    public int Rank(string id) { int r = BaseRank(id); return r <= 0 ? 0 : r + (int)S[Stat.SkillRanks]; }
    public bool HasUp(string upId) => D.Ups.Contains(upId);
    public int SkillPointsSpent => D.Skills.Values.Sum() + D.Ups.Count - 1;      // the starting basic skill is free
    public int SkillPoints => D.Level - 1 + D.BonusSkillPts - SkillPointsSpent;

    /// <summary>The skill definition with the spec, taken upgrades and gear powers applied.</summary>
    public SkillDef Eff(string id)
    {
        if (id == null) return null;
        if (_eff.TryGetValue(id, out var e)) return e;
        var b = SkillDb.Get(id);
        if (b == null) return null;
        e = b.Clone();
        Spec?.Mod?.Invoke(e);
        foreach (var u in b.Ups) if (u != null && HasUp(u.Id)) u.Mod?.Invoke(e);
        if (HasAsp("wide_arc"))
        {
            float m = 1f + Asp("wide_arc") / 100f;
            e.Radius *= m; e.Explode *= m; if (e.Leave != null) e.Leave.Radius *= m;
        }
        if (HasAsp("multishot") && e.Kind == SkillKind.Proj) { e.Count += 2; e.Coef *= 1f - Asp("multishot") / 100f; if (e.Spread < 10) e.Spread = 10; }
        if (id == "sb_raise") e.SummonMax += (HasAsp("legion") ? 2 : 0) + (HasKeystone("ks_army") ? 2 : 0);
        if (id == "as_meteor" && HasAsp("twin_meteor")) { e.ExtraStrikes += 1; e.Flags.Add("twin"); }
        if (id == "as_chain" && HasUnique("u_stargazer")) e.Count += 3;
        if (id == "eb_whirl" && HasAsp("cyclone_pull") && e.OnHit.All(x => x.Id != StatusId.Pull)) e.OnHit.Add(St.Pull(3));
        if (HasKey("kp_ws_elder") && e.Form == 3) { e.Cooldown *= 0.75f; e.Coef *= 1.1f; }
        _eff[id] = e;
        return e;
    }

    public bool CanLearn(SkillDef s) => SkillPoints > 0 && D.Level >= s.UnlockLevel && BaseRank(s.Id) < SkillDef.MaxRank;

    public bool Learn(string id)
    {
        var s = SkillDb.Get(id);
        if (s != null)
        {
            if (!CanLearn(s)) return false;
            D.Skills[id] = BaseRank(id) + 1;
            if (BaseRank(id) == 1) AutoBind(id);
            Recalc(); return true;
        }
        var p = SkillDb.Passive(id);
        if (p == null || SkillPoints <= 0 || D.Level < p.UnlockLevel || BaseRank(id) >= p.MaxRank) return false;
        if (p.Key) foreach (var o in SkillDb.PassivesOf(Class).Where(o => o.Key && o.Id != id)) D.Skills.Remove(o.Id);
        D.Skills[id] = BaseRank(id) + 1;
        Recalc(); return true;
    }

    public bool CanTakeUp(SkillDef s, SkillUp u)
    {
        if (SkillPoints <= 0 || BaseRank(s.Id) < 1 || HasUp(u.Id)) return false;
        if (u == s.Enh) return true;
        return HasUp(s.Enh.Id);     // A/B need the enhancement; picking one replaces the other for free
    }

    public bool TakeUp(string skillId, string upId)
    {
        var s = SkillDb.Get(skillId);
        var u = s?.Ups.FirstOrDefault(x => x.Id == upId);
        if (u == null) return false;
        var other = u == s.A ? s.B : u == s.B ? s.A : null;
        if (other != null && HasUp(other.Id) && HasUp(s.Enh.Id)) { D.Ups.Remove(other.Id); D.Ups.Add(u.Id); Recalc(); return true; }
        if (!CanTakeUp(s, u)) return false;
        D.Ups.Add(u.Id);
        Recalc(); return true;
    }

    /// <summary>Refunds one rank (or the whole skill with its upgrades at rank 1). Respec is free, as in the genre today.</summary>
    public bool Unlearn(string id)
    {
        int r = BaseRank(id);
        if (r <= 0) return false;
        var s = SkillDb.Get(id);
        if (s != null && r == 1)
        {
            if (s.Cat == SkillCat.Basic && SkillDb.Of(Class).Count(x => x.Cat == SkillCat.Basic && BaseRank(x.Id) > 0) <= 1) return false;
            foreach (var u in s.Ups) D.Ups.Remove(u.Id);
            D.Skills.Remove(id);
            for (int i = 0; i < 6; i++) if (D.Bar[i] == id) D.Bar[i] = null;
        }
        else if (r == 1) D.Skills.Remove(id);
        else D.Skills[id] = r - 1;
        Recalc(); return true;
    }

    public void RespecAll()
    {
        string basic = SkillDb.DefaultBasic(Class);
        D.Skills.Clear(); D.Ups.Clear();
        D.Skills[basic] = 1;
        D.Bar = new string[6]; D.Bar[0] = basic;
        Recalc();
    }

    private void AutoBind(string id)
    {
        if (D.Bar.Contains(id)) return;
        var s = SkillDb.Get(id);
        int pref = s.Cat == SkillCat.Core ? 1 : -1;
        if (pref == 1 && D.Bar[1] == null) { D.Bar[1] = id; return; }
        for (int i = 2; i < 6; i++) if (D.Bar[i] == null) { D.Bar[i] = id; return; }
        if (D.Bar[1] == null) D.Bar[1] = id;
    }

    public bool SetSpec(string id)
    {
        if (D.Level < SkillDb.SpecLevel || SkillDb.SpecsOf(Class).All(s => s.Id != id)) return false;
        D.Spec = id; Recalc(); return true;
    }

    // ───────────────────────── paragon board ─────────────────────────

    // (allocation, glyphs and board data live in Hero.Paragon.cs / Paragon.cs)

    // ───────────────────────── experience ─────────────────────────

    public void AddXp(float amount)
    {
        long xp = (long)MathF.Round(amount * (1f + S[Stat.XpGain] / 100f));
        if (D.Level < Balance.LevelCap)
        {
            D.Xp += xp;
            while (D.Level < Balance.LevelCap && D.Xp >= Balance.XpToNext(D.Level))
            {
                D.Xp -= Balance.XpToNext(D.Level);
                D.Level++;
                Recalc();
                LevelUp?.Invoke(D.Level);
            }
            if (D.Level >= Balance.LevelCap) { D.ParagonXp += D.Xp; D.Xp = 0; }
        }
        else D.ParagonXp += xp;
        while (D.Level >= Balance.LevelCap && D.Paragon < Balance.ParagonCap && D.ParagonXp >= Balance.ParagonXp(D.Paragon))
        {
            D.ParagonXp -= Balance.ParagonXp(D.Paragon);
            D.Paragon++;
            ParagonUp?.Invoke(D.Paragon);
        }
    }

    public float XpFraction => D.Level < Balance.LevelCap ? D.Xp / (float)Balance.XpToNext(D.Level)
        : D.Paragon < Balance.ParagonCap ? D.ParagonXp / (float)Balance.ParagonXp(D.Paragon) : 1f;

    // ───────────────────────── inventory ─────────────────────────

    public bool BagFull => D.Bag.Count >= BagSize;
    public bool AddItem(Item it) { if (BagFull) return false; D.Bag.Add(it); Changed?.Invoke(); return true; }
    public void RemoveItem(Item it) { D.Bag.Remove(it); Changed?.Invoke(); }

    public static EquipSlot SlotFor(ItemKind k) => k switch
    {
        ItemKind.Weapon => EquipSlot.Weapon, ItemKind.Helm => EquipSlot.Helm, ItemKind.Chest => EquipSlot.Chest, ItemKind.Gloves => EquipSlot.Gloves,
        ItemKind.Pants => EquipSlot.Pants, ItemKind.Boots => EquipSlot.Boots, ItemKind.Amulet => EquipSlot.Amulet, _ => EquipSlot.Ring1
    };

    public string WhyCantEquip(Item it)
    {
        if (!it.IsEquip) return "無法裝備";
        if (it.ReqLevel > D.Level) return $"需要等級 {it.ReqLevel}";
        if (it.Kind == ItemKind.Weapon && it.WeaponClass >= 0 && it.WeaponClass != (int)Class) return $"僅限{Names.Class((ClassId)it.WeaponClass)}使用";
        return null;
    }

    /// <summary>The equipped item an inventory item would be compared with / replace.</summary>
    public Item Compared(Item it)
    {
        if (!it.IsEquip) return null;
        if (it.Kind == ItemKind.Ring)
        {
            var r1 = D.Equip[(int)EquipSlot.Ring1]; var r2 = D.Equip[(int)EquipSlot.Ring2];
            if (r1 == null || r2 == null) return null;
            return r1.Power <= r2.Power ? r1 : r2;
        }
        return D.Equip[(int)SlotFor(it.Kind)];
    }

    public bool Equip(Item it)
    {
        if (WhyCantEquip(it) != null || !D.Bag.Contains(it)) return false;
        EquipSlot slot = SlotFor(it.Kind);
        if (it.Kind == ItemKind.Ring)
        {
            var r1 = D.Equip[(int)EquipSlot.Ring1]; var r2 = D.Equip[(int)EquipSlot.Ring2];
            slot = r1 == null ? EquipSlot.Ring1 : r2 == null ? EquipSlot.Ring2 : (r1.Power <= r2.Power ? EquipSlot.Ring1 : EquipSlot.Ring2);
        }
        var old = D.Equip[(int)slot];
        int idx = D.Bag.IndexOf(it);
        D.Bag.RemoveAt(idx);
        if (old != null) D.Bag.Insert(idx, old);
        D.Equip[(int)slot] = it;
        Recalc(); return true;
    }

    public bool Unequip(EquipSlot slot)
    {
        var it = D.Equip[(int)slot];
        if (it == null || BagFull) return false;
        D.Equip[(int)slot] = null; D.Bag.Add(it);
        Recalc(); return true;
    }

    public bool ToStash(Item it) { if (D.Stash.Count >= StashSize || !D.Bag.Remove(it)) return false; D.Stash.Add(it); Changed?.Invoke(); return true; }
    public bool FromStash(Item it) { if (BagFull || !D.Stash.Remove(it)) return false; D.Bag.Add(it); Changed?.Invoke(); return true; }

    public void Sell(Item it) { if (D.Bag.Remove(it)) { D.Gold += it.Value; Changed?.Invoke(); } }
    public bool Buy(Item it, int price) { if (D.Gold < price || BagFull) return false; D.Gold -= price; D.Bag.Add(it); Changed?.Invoke(); return true; }

    // ───────────────────────── crafting ─────────────────────────

    /// <summary>Salvage: materials back, and a legendary's power is recorded permanently in the codex (best roll kept).</summary>
    public (int scrap, int dust, int cores) Salvage(Item it)
    {
        if (!D.Bag.Remove(it) || !it.IsEquip) return default;
        var y = ItemDb.SalvageYield(it);
        D.Scrap += y.scrap; D.Dust += y.dust; D.Cores += y.cores;
        if (it.Aspect != null && (!D.Codex.TryGetValue(it.Aspect, out var best) || it.AspectRoll > best)) D.Codex[it.Aspect] = it.AspectRoll;
        foreach (var g in it.Gems) if (!BagFull) D.Bag.Add(ItemDb.MakeGem(g));
        Changed?.Invoke();
        return y;
    }

    public string WhyCantUpgrade(Item it)
    {
        if (it.Upgrade >= ItemDb.MaxUpgrade) return "已達最高強化等級";
        if (D.Gold < ItemDb.UpgradeGoldCost(it)) return "金幣不足";
        if (D.Scrap < ItemDb.UpgradeScrapCost(it)) return "碎鐵不足";
        if (D.Dust < ItemDb.UpgradeDustCost(it)) return "幽塵不足";
        if (D.Cores < ItemDb.UpgradeCoreCost(it)) return "燼核不足";
        return null;
    }

    public bool Upgrade(Item it)
    {
        if (WhyCantUpgrade(it) != null) return false;
        D.Gold -= ItemDb.UpgradeGoldCost(it); D.Scrap -= ItemDb.UpgradeScrapCost(it); D.Dust -= ItemDb.UpgradeDustCost(it); D.Cores -= ItemDb.UpgradeCoreCost(it);
        it.Upgrade++; it.Value = ItemDb.SellValue(it);
        if (it.Upgrade % 4 == 0)
        {
            var pick = it.Affixes.Where(a => a.Stat != Stat.SkillRanks).ToList();
            if (pick.Count > 0) Rng.Pick(pick).Bonus += 0.25f;      // masterworking milestone: one affix +25%
        }
        Recalc(); return true;
    }

    public int EnchantCost(Item it) => ItemDb.EnchantGoldCost(it, D.Enchants.GetValueOrDefault(it.Uid));
    public bool CanEnchant(Item it) => it.Affixes.Count > 0 && it.Rarity != Rarity.Unique && D.Gold >= EnchantCost(it) && D.Dust >= ItemDb.EnchantDustCost;

    /// <summary>Pays for a reroll and returns two candidate affixes; the caller applies one or keeps the old one.</summary>
    public Affix[] EnchantRoll(Item it, int index)
    {
        if (!CanEnchant(it) || index < 0 || index >= it.Affixes.Count) return null;
        D.Gold -= EnchantCost(it); D.Dust -= ItemDb.EnchantDustCost;
        D.Enchants[it.Uid] = D.Enchants.GetValueOrDefault(it.Uid) + 1;
        Changed?.Invoke();
        return new[] { ItemDb.RerollAffix(it, index, Class), ItemDb.RerollAffix(it, index, Class) };
    }

    public void ApplyEnchant(Item it, int index, Affix a) { if (a != null) { it.Affixes[index] = a; Recalc(); } }

    public string WhyCantImprint(Item it, string aspectId)
    {
        var asp = ItemDb.Aspect(aspectId);
        if (asp == null || !D.Codex.ContainsKey(aspectId)) return "尚未取得此威能";
        if (it.Rarity != Rarity.Rare && it.Rarity != Rarity.Legendary) return "只能銘刻於稀有或傳奇裝備";
        if (!asp.Kinds.Contains(it.Kind)) return "此威能無法銘刻於這類裝備";
        if (asp.Class >= 0 && asp.Class != (int)Class) return "職業不符";
        if (D.Gold < ItemDb.ImprintGoldCost(it)) return "金幣不足";
        if (D.Cores < 1) return "燼核不足";
        return null;
    }

    public bool Imprint(Item it, string aspectId)
    {
        if (WhyCantImprint(it, aspectId) != null) return false;
        D.Gold -= ItemDb.ImprintGoldCost(it); D.Cores -= 1;
        var asp = ItemDb.Aspect(aspectId);
        string baseName = it.Name.Contains('・') ? it.Name[(it.Name.IndexOf('・') + 1)..] : it.Name.Contains('之') && it.Rarity == Rarity.Legendary ? it.Name[(it.Name.IndexOf('之') + 1)..] : it.Name;
        if (it.Rarity == Rarity.Rare && it.Affixes.Count < 4) { /* legendary keeps the rare's three affixes */ }
        it.Rarity = Rarity.Legendary; it.Aspect = aspectId; it.AspectRoll = D.Codex[aspectId];
        it.Name = asp.Name + "之" + baseName; it.Value = ItemDb.SellValue(it);
        Recalc(); return true;
    }

    public bool Socket(Item gem, Item target)
    {
        if (gem.Kind != ItemKind.Gem || !target.IsEquip || target.Gems.Count >= target.Sockets || !D.Bag.Remove(gem)) return false;
        target.Gems.Add(gem.GemId);
        Recalc(); return true;
    }

    public bool Unsocket(Item target)
    {
        if (target.Gems.Count == 0 || D.Bag.Count + target.Gems.Count > BagSize) return false;
        foreach (var g in target.Gems) D.Bag.Add(ItemDb.MakeGem(g));
        target.Gems.Clear();
        Recalc(); return true;
    }

    public const int SocketGoldCost = 600;
    public bool AddSocket(Item it)
    {
        int max = it.Kind is ItemKind.Weapon or ItemKind.Chest ? 2 : it.Kind is ItemKind.Gloves or ItemKind.Boots ? 0 : 1;
        if (it.Sockets >= max || D.Gold < SocketGoldCost || D.Dust < 5) return false;
        D.Gold -= SocketGoldCost; D.Dust -= 5; it.Sockets++;
        Changed?.Invoke(); return true;
    }

    public void Notify() => Changed?.Invoke();
}
