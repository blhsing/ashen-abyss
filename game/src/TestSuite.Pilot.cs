using Godot;
using System;
using System.Collections;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// --suite=pilot[:save json]: the AI pilot's gear and storage rules.
/// Gear (the user's report, 2026-11-22, slot 4 emberblade lv5: the pilot swapped 「鐵淚庇護・鐵製巨劍」 for a plain sword and
/// then a magic one, and 「亡歌餘燼・皮手套」 for a magic pair): per slot only the best candidate is worn, no churn, a rare
/// is not traded for a lower rarity without a 10 % gain, a legendary / locked piece needs 15 %.
/// Storage (user rules): only locked and gear-set pieces are the player's; the stash is shared across saves and the
/// pilot never takes anything out of it; with a full stash it salvages from the bag only (lowest value first, a
/// legendary's power recorded in the codex by the salvage); when only protected pieces are left it hands back.
/// </summary>
public partial class TestSuite
{
    private IEnumerator PilotSuite()
    {
        _section = "pilot: gear";
        string file = !string.IsNullOrEmpty(_arg) ? _arg : "Y:/tools/pilot/fixtures/user_slot4_20261122.json";
        if (!System.IO.File.Exists(file)) { Fail($"pilot: no save copy at {file}"); yield break; }
        var d = SaveSystem.FromJson(System.IO.File.ReadAllText(file));
        var h = new Hero(d);
        var rareSword = d.Bag.Concat(d.Equip).FirstOrDefault(i => i != null && i.Name == "鐵淚庇護・鐵製巨劍");
        var rareGloves = d.Bag.Concat(d.Equip).FirstOrDefault(i => i != null && i.Name == "亡歌餘燼・皮手套");
        Check(rareSword != null && rareGloves != null, "pilot: the reported items are in the save copy", $"{rareSword?.Name} / {rareGloves?.Name}");
        if (rareSword == null || rareGloves == null) yield break;
        if (!h.IsEquipped(rareSword)) h.Equip(rareSword);
        if (!h.IsEquipped(rareGloves)) h.Equip(rareGloves);
        var pilot = new AiPilot();
        Item best = null; float bestT = float.MinValue;
        foreach (var it in d.Bag.Where(i => i != null && i.IsEquip && i.Base == "sword2"))
        {
            var j = GearEval.Judge(h, it);
            Log.Info("suite", $"pilot: candidate {it.Name} ({it.Rarity}, power {it.Power}) vs {h.Compared(it)?.Name}: {j.Verdict} total={j.Total * 100f:0.#}%");
            if (j.Verdict == GearVerdict.Better && j.Total > bestT) { bestT = j.Total; best = it; }
        }
        pilot.PickEquips(h);
        string after1 = $"{h.MainHand?.Name}|{h.D.Equip[(int)EquipSlot.Gloves]?.Name}";
        pilot.PickEquips(h);
        string after2 = $"{h.MainHand?.Name}|{h.D.Equip[(int)EquipSlot.Gloves]?.Name}";
        Check(best == null || h.MainHand == best, "pilot: the best sword in the bag is worn, in one step", $"{h.MainHand?.Name} (best {best?.Name})");
        Check(h.MainHand == null || h.MainHand.Rarity != Rarity.Common, "pilot: no plain sword over the rare one", h.MainHand?.Name);
        Check(after1 == after2, "pilot: a second pass makes no further swap", after1);

        _section = "pilot: prized pieces";
        var h2 = new Hero(SaveSystem.FromJson(SaveSystem.ToJson(d)));
        var leg = ItemDb.Generate(Math.Max(10, d.Level * 10), Rarity.Legendary, h2.Class, ItemKind.Gloves, null, "gloves");
        h2.D.Bag.Add(leg); h2.Equip(leg);
        var near = ItemDb.Generate(Math.Max(10, d.Level * 10) + 2, Rarity.Rare, h2.Class, ItemKind.Gloves, null, "gloves");
        h2.D.Bag.Add(near);
        float t = GearEval.Judge(h2, near).Total;
        new AiPilot().PickEquips(h2);
        bool kept = h2.D.Equip[(int)EquipSlot.Gloves] == leg;
        Check(kept || t >= 0.15f, "pilot: a legendary is only replaced for a 15 % gain", $"candidate {t * 100f:0.#}% kept={kept}");
        yield return null;

        _section = "pilot: storage";
        // a) full bag, empty stash: keepers to the stash, junk gone, nothing locked touched
        {
            var hs = Fresh(d, out var locked);
            Fill(hs, Hero.BagSize - hs.D.Bag.Count, i => i % 5 == 0 ? Rarity.Legendary : i % 2 == 0 ? Rarity.Rare : Rarity.Magic);
            var r = new AiPilot().OverflowStep(hs);
            Check(r != OverflowResult.StorageFull && hs.D.Bag.Count < Hero.BagSize * 4 / 5 && (hs.D.Bag.Contains(locked) || hs.D.Stash.Contains(locked))
                  && hs.D.Stash.Any(i => i.Rarity >= Rarity.Legendary),
                "storage: a full bag empties (legendaries and the locked piece stashed, junk gone, nothing locked sold)",
                $"{r} bag {hs.D.Bag.Count} stash {hs.D.Stash.Count}");
        }
        // b) full bag of locked pieces, full stash: nothing goes, control is handed back, the stash untouched
        {
            var hs = Fresh(d, out _);
            hs.D.Bag.Clear();
            for (int i = 0; i < Hero.BagSize; i++) { var it = Gen(hs, Rarity.Rare, i); it.Locked = true; hs.D.Bag.Add(it); }
            for (int i = 0; i < Hero.StashSize; i++) hs.D.Stash.Add(Gen(hs, Rarity.Magic, i));
            var stashBefore = string.Join(",", hs.D.Stash.Select(x => x.Uid));
            var r = new AiPilot().OverflowStep(hs);
            Check(r == OverflowResult.StorageFull && hs.D.Bag.Count == Hero.BagSize && string.Join(",", hs.D.Stash.Select(x => x.Uid)) == stashBefore,
                "storage: only locked pieces left and the stash full → hand back, nothing sold, the shared stash untouched",
                $"{r} bag {hs.D.Bag.Count}");
        }
        // c) full stash, bag of found legendaries: the lowest-value ones are salvaged from the bag, powers into the codex
        {
            var hs = Fresh(d, out _);
            hs.D.Bag.Clear();
            for (int i = 0; i < Hero.StashSize; i++) hs.D.Stash.Add(Gen(hs, Rarity.Magic, i));
            for (int i = 0; i < Hero.BagSize; i++) hs.D.Bag.Add(Gen(hs, Rarity.Legendary, i));
            int lowest = hs.D.Bag.Min(x => x.Power), codex0 = hs.D.Codex.Count;
            var stashBefore = string.Join(",", hs.D.Stash.Select(x => x.Uid));
            var r = new AiPilot().OverflowStep(hs);
            Check(r != OverflowResult.StorageFull && hs.D.Bag.Count < Hero.BagSize * 4 / 5 && hs.D.Bag.All(x => x.Power >= lowest)
                  && string.Join(",", hs.D.Stash.Select(x => x.Uid)) == stashBefore && hs.D.Codex.Count >= codex0,
                "storage: a full stash → found legendaries salvaged from the bag (lowest first), the stash untouched, powers kept in the codex",
                $"{r} bag {hs.D.Bag.Count} codex {codex0}→{hs.D.Codex.Count}");
        }
        yield return null;
    }

    private static Hero Fresh(SaveData src, out Item locked)
    {
        var hs = new Hero(SaveSystem.FromJson(SaveSystem.ToJson(src)));
        hs.D.Stash.Clear();
        locked = Gen(hs, Rarity.Rare, 999); locked.Locked = true;
        hs.D.Bag.Add(locked);
        return hs;
    }

    private static Item Gen(Hero h, Rarity r, int i)
    {
        var kinds = new[] { ItemKind.Helm, ItemKind.Chest, ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots, ItemKind.Ring, ItemKind.Amulet };
        var k = kinds[i % kinds.Length];
        return ItemDb.Generate(5 + (i * 7) % 40, r, h.Class, k, null, null);
    }

    private static void Fill(Hero h, int n, Func<int, Rarity> rar)
    {
        for (int i = 0; i < n; i++) h.D.Bag.Add(Gen(h, rar(i), i));
    }
}
