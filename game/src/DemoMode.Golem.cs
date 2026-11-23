using System;
using System.Linq;

namespace AshenAbyss;

/// <summary>展示模式 with golems: every Soulbinder scene shows the next golem kind in turn, the iron one forged from a legendary maul.</summary>
public partial class DemoMode
{
    private static int _golemTurn;

    private void DemoGolem(Hero h)
    {
        if (h.Class != ClassId.Soulbinder || h.D.Level < 5) return;
        h.EnsureBook();
        string[] kinds = { "g_bone", "g_blood", "g_iron" };
        string kind = kinds[_golemTurn++ % kinds.Length];
        string[] warriors = { "w_raider", "w_guard", "w_reaper" }, mages = { "m_shadow", "m_frost", "m_bone" };
        h.SetBook(ClassBook.Warriors, warriors[_rng.Next(3)], _rng.Next(2), true, out _);
        h.SetBook(ClassBook.Mages, mages[_rng.Next(3)], _rng.Next(2), true, out _);
        h.SetBook(ClassBook.Golem, kind, _rng.Next(2), true, out _);
        if (kind == "g_iron")
        {
            string b = new[] { "mace2", "axe2", "polearm", "shield" }[_rng.Next(4)];
            var it = ItemDb.Generate(Math.Clamp(h.D.Level * 10 + 40, 10, ItemDb.MaxPower), Rarity.Legendary, b == "shield" ? ClassId.Soulbinder : ClassId.Emberblade, b == "shield" ? ItemKind.OffHand : ItemKind.Weapon, _rng, b);
            h.D.Bag.Add(it);
            GolemForge.Forge(h, it, out _);
        }
        if (h.D.Level >= 7 && h.BaseRank("sb_golem_cmd") <= 0)
        {
            h.D.Skills["sb_golem_cmd"] = 1;
            if (!h.D.Bar.Contains("sb_golem_cmd")) { int i = Array.FindIndex(h.D.Bar, 2, x => x == null); h.D.Bar[i < 0 ? 5 : i] = "sb_golem_cmd"; }
        }
        h.Recalc();
        Log.Info("demo", $"golem scene: {kind}{(h.D.GolemForge != null && kind == "g_iron" ? $" forged from {h.D.GolemForge.Name}" : "")}, book [{string.Join(", ", h.D.Book.Pick.Values)}]");
    }
}
