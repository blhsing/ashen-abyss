using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// Soulbinder minion and golem items (tools/golems/PLAN.md §5), added to ItemDb's tables once at boot (MinionSystem.Install):
/// off-hand affixes on the Soulbinder's shield and focus (golem life and damage, mage damage, minion attack speed, command
/// cooldown; ItemDb.Weight keeps them off other classes), six legendary powers and two uniques built on golems.
/// </summary>
public static class GolemItems
{
    private static bool _done;
    public static readonly Stat[] OffhandStats = { Stat.GolemLife, Stat.GolemDmg, Stat.MageDmg, Stat.MinionAtkSpeed, Stat.CmdCdr };
    private static readonly ItemKind[] Off = { ItemKind.OffHand };
    private static readonly ItemKind[] Offense = { ItemKind.Weapon, ItemKind.Gloves, ItemKind.Ring, ItemKind.Amulet, ItemKind.OffHand };
    private static readonly ItemKind[] Defense = { ItemKind.Helm, ItemKind.Chest, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet, ItemKind.OffHand };
    private static readonly ItemKind[] AnyKind = { ItemKind.Weapon, ItemKind.Helm, ItemKind.Chest, ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet, ItemKind.Ring, ItemKind.OffHand };

    public static readonly string[] PowerIds = { "sb_bone_shards", "sb_iron_fissure", "sb_blood_harvest", "sb_war_horn", "sb_undying", "sb_sacrament" };
    public static readonly string[] UniqueIds = { "u_cairn", "u_crucible" };

    public static void Ensure()
    {
        if (_done) return;
        _done = true;
        try { Register(); }
        catch (Exception e) { Log.Error("golem", "registering the golem items failed", e); }
    }

    private static void Register()
    {
        // ── off-hand affixes (only the Soulbinder's shield and focus roll them: ItemDb.Weight) ──
        if (ItemDb.Pools.All(p => p.S != Stat.GolemLife))
        {
            ItemDb.Pools.Add(new ItemDb.Pool(Stat.GolemLife, 4, 40, Off, 0.9f));
            ItemDb.Pools.Add(new ItemDb.Pool(Stat.GolemDmg, 5, 45, Off, 0.9f));
            ItemDb.Pools.Add(new ItemDb.Pool(Stat.MageDmg, 5, 45, Off, 0.8f));
            ItemDb.Pools.Add(new ItemDb.Pool(Stat.MinionAtkSpeed, 2, 14, Off, 0.7f));
            ItemDb.Pools.Add(new ItemDb.Pool(Stat.CmdCdr, 3, 18, Off, 0.6f));
        }
        foreach (var id in new[] { "shield", "focus" })
        {
            var b = ItemDb.BaseOf(id);
            if (b?.Affixes != null && !b.Affixes.Contains(Stat.GolemLife)) b.Affixes = b.Affixes.Concat(OffhandStats).ToArray();
        }

        // ── legendary powers (Soulbinder) ──
        void A(string id, string name, string desc, float lo, float hi, ItemKind[] kinds)
        {
            if (ItemDb.Aspects.Any(a => a.Id == id)) return;
            ItemDb.Aspects.Add(new AspectDef { Id = id, Name = name, Desc = desc, Lo = lo, Hi = hi, Kinds = kinds, Class = (int)ClassId.Soulbinder });
        }
        A("sb_bone_shards", "碎骨之刺", "骨魔像的荊棘觸發時迸出骨片，對周圍敵人造成 {0}% 武器傷害。", 40, 90, Offense);
        A("sb_iron_fissure", "鐵裂", "鐵魔像的震波與「鐵擊令」留下熔鐵裂隙 3 秒，每秒造成 {0}% 武器傷害。", 20, 45, Offense);
        A("sb_blood_harvest", "血穫", "「血潮令」每擊殺一名敵人便喚起一名骸骨僕從，並恢復魔像 {0}% 生命。", 5, 12, Offense);
        A("sb_war_horn", "亡者號角", "魔像號令同時號令所有骸骨僕從：使其狂暴，並使僕從傷害提高 {0}%，持續 6 秒。", 15, 35, Offense);
        A("sb_undying", "不朽泥身", "魔像受到致命傷害時立即重組並恢復 {0}% 生命（每 60 秒一次）。", 30, 70, Defense);
        A("sb_sacrament", "獻祭聖約", "亡者名冊中已獻祭之亡者線的獎勵提高 {0}%。", 50, 100, AnyKind);

        // ── uniques (Soulbinder) ──
        if (ItemDb.Uniques.All(u => u.Id != "u_cairn"))
            ItemDb.Uniques.Add(new UniqueDef
            {
                Id = "u_cairn", Name = "萬骸碑盾", Kind = ItemKind.OffHand, Class = (int)ClassId.Soulbinder, Base = "shield", Icon = "swordshield",
                Desc = "魔像生命提高 30%。骨魔像的荊棘觸發時，你獲得相當於生命上限 2% 的屏障。", Flavor = "碑上刻滿名字。每一個名字，都還在替你擋著。",
                Affixes = new[] { (Stat.GolemLife, 20f, 40f), (Stat.Wil, 40f, 70f), (Stat.Armor, 200f, 480f), (Stat.ResAll, 8f, 15f) },
            });
        if (ItemDb.Uniques.All(u => u.Id != "u_crucible"))
            ItemDb.Uniques.Add(new UniqueDef
            {
                Id = "u_crucible", Name = "熔魂坩堝", Kind = ItemKind.OffHand, Class = (int)ClassId.Soulbinder, Base = "focus", Icon = "staff",
                Desc = "鐵魔像每第 4 擊的震波化為熔流：傷害提高 120% 並使敵人燃燒。魔像號令的冷卻時間縮短 3 秒。", Flavor = "日爐熄滅之前，最後鑄成的不是劍，是一副會走路的身軀。",
                Affixes = new[] { (Stat.GolemDmg, 25f, 45f), (Stat.Wil, 40f, 70f), (Stat.MinionAtkSpeed, 6f, 12f), (Stat.DmgFire, 20f, 40f) },
            });
        Log.Info("golem", $"golem items registered: {OffhandStats.Length} off-hand affixes, {PowerIds.Length} legendary powers, {UniqueIds.Length} uniques");
    }
}
