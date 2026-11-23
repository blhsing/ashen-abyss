using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace AshenAbyss;

/// <summary>
/// --gptest=stash (with --slot=92 --stash-keep): the shared stash. Three old-format saves with their own stashes are
/// migrated one after another (one with a uid that collides); items move from hero A to hero B through the stash; a
/// hardcore hero has its own stash; a damaged stash file falls back to its backup; gear sets pull from the shared stash
/// (only the items they reference); the total number of items never changes. Uses only slot 92 and its test stash files.
/// </summary>
public partial class GameplayTest
{
    private static string SaveDir => ProjectSettings.GlobalizePath("user://saves");
    private static string StashFileOf(bool hc) => SharedStash.PathFor(TSlot, hc);
    private static string CharCopy(string k) => $"{OutDir}/stash_char_{k}.json";

    private static JsonObject ReadJson(string path) { try { return JsonNode.Parse(File.ReadAllText(path)) as JsonObject; } catch (Exception) { return null; } }
    private static int Arr(JsonObject o, string key) => o?[key] is JsonArray a ? a.Count(x => x != null) : 0;
    private static int StashCount(bool hc) => Arr(ReadJson(StashFileOf(hc)), "Items");
    /// <summary>Items a character file holds (bag, worn, its own stash if any).</summary>
    private static int CharItems(string path) { var o = ReadJson(path); return Arr(o, "Bag") + Arr(o, "Equip") + Arr(o, "Stash"); }

    private IEnumerator<float> LoadSlotFrom(string file)
    {
        File.Copy(file, SlotFile(), true);
        if (File.Exists(SlotFile("bak"))) File.Delete(SlotFile("bak"));
        G.LoadGame(TSlot);
        var w = WaitPlay(); while (w.MoveNext()) yield return w.Current;
    }

    private void KeepChar(string k) { G.SaveNow("test"); File.Copy(SlotFile(), CharCopy(k), true); }

    private IEnumerator<float> StashTest()
    {
        yield return 1.0f;
        // a clean start: the boot hero lets go of its stash, the test stash files go
        SharedStash.Detach();
        foreach (bool hc in new[] { false, true }) foreach (var ext in new[] { "", ".bak", ".tmp" }) { var f = StashFileOf(hc) + ext; if (File.Exists(f)) File.Delete(f); }
        var rng = new Random(4242);

        // ── 1. three old (0.3-format) saves with their own stashes ──
        var specs = new[] { ("A", "甲", ClassId.Emberblade, 5), ("B", "乙", ClassId.Hunter, 90), ("C", "丙", ClassId.Lampknight, 3) };
        string collide = null;
        foreach (var (k, name, cls, n) in specs)
        {
            var d = Hero.NewCharacter(name, cls, false);
            for (int i = 0; i < n; i++) d.Stash.Add(ItemDb.Generate(60 + i, i % 3 == 0 ? Rarity.Legendary : Rarity.Rare, cls, null, rng));
            if (k == "A") collide = d.Stash[0].Uid;
            if (k == "C")
            {
                d.Stash[0].Uid = collide;                                   // the same uid as one of A's items
                d.GearSets ??= new();
                var set = new GearSetData { Name = "舊組" }; set.Uids[0] = collide; set.ItemNames[0] = d.Stash[0].Name;
                d.GearSets.Add(set);
            }
            SaveSystem.Save(TSlot, d);                                       // not attached: written with its own "Stash"
            var o = ReadJson(SlotFile());
            o["Version"] = 1; o.Remove("StashShared"); o.Remove("Cid"); o.Remove("GearSets"); o.Remove("Fog");
            if (k == "C") o["GearSets"] = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(d.GearSets, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }));
            File.WriteAllText($"{OutDir}/stash_old_{k}.json", o.ToJsonString());
            Log.Info(Cat, $"old save {k} ({name}, {Names.ClassKey(cls)}): {n} item(s) in its own stash, format 1");
        }
        int total = 0;
        foreach (var (k, name, cls, n) in specs)
        {
            var l = LoadSlotFrom($"{OutDir}/stash_old_{k}.json"); while (l.MoveNext()) yield return l.Current;
            total += n;
            var st = G.Hero.D.Stash;
            int distinct = st.Select(i => i.Uid).Distinct().Count();
            var perTab = Enumerable.Range(0, SharedStash.Tabs).Select(t => SharedStash.CountIn(st, t)).ToArray();
            KeepChar(k);
            var co = ReadJson(CharCopy(k));
            bool charClean = co?["StashShared"]?.GetValue<bool>() == true && Arr(co, "Stash") == 0;
            Check(st.Count == total && distinct == total && perTab.All(c => c <= SharedStash.TabSize) && StashCount(false) == total && charClean,
                $"stash: old save {k} migrated: shared stash {st.Count} (expected {total}), distinct uids {distinct}, tabs [{string.Join(",", perTab)}], file {StashCount(false)}, character file marked and without its own stash={charClean}");
            if (k == "C")
            {
                var mine = st.Where(i => i.StashBy == G.Hero.D.Cid).ToList();
                string setUid = G.Hero.D.GearSets.FirstOrDefault()?.Uids[0];
                Check(mine.Count == 3 && setUid != collide && mine.Any(i => i.Uid == setUid) && st.Any(i => i.Uid == collide),
                    $"stash: colliding uid re-issued ({collide} → {setUid}); C's gear set follows its item; A's item keeps {collide}");
            }
        }
        // loading a migrated character again never migrates twice
        { var l = LoadSlotFrom(CharCopy("A")); while (l.MoveNext()) yield return l.Current; }
        Check(G.Hero.D.Stash.Count == total, $"stash: reloading A: still {G.Hero.D.Stash.Count} item(s) (no second migration)");
        KeepChar("A");
        int Everything() => CharItems(CharCopy("A")) + CharItems(CharCopy("B")) + CharItems(CharCopy("C")) + StashCount(false) + StashCount(true);
        int all0 = Everything();
        Log.Info(Cat, $"stash: items in all characters + stashes = {all0}");

        // ── 2. from hero A to hero B through the stash ──
        var a = G.Hero;
        var giveA = a.D.Bag.FirstOrDefault() ?? a.D.Equip.FirstOrDefault(x => x != null);
        if (a.D.Bag.Count == 0 && giveA != null) { a.Unequip(EquipSlot.MainHand); giveA = a.D.Bag.FirstOrDefault(); }
        var takeA = a.D.Stash.First(i => i.StashBy != a.D.Cid);             // one of B's or C's items
        bool okA = giveA != null && a.StashItem(giveA) && a.FromStash(takeA);
        string give = giveA?.Uid, take = takeA.Uid;
        Check(okA && a.D.Stash.Any(i => i.Uid == give) && a.D.Bag.Any(i => i.Uid == take), $"stash: A deposits {giveA?.Name} and takes {takeA.Name}");
        yield return 0.8f;                                                   // the soon-save (Game.SaveSoon) writes both
        KeepChar("A");
        { var l = LoadSlotFrom(CharCopy("B")); while (l.MoveNext()) yield return l.Current; }
        var b = G.Hero;
        var gotB = b.D.Stash.FirstOrDefault(i => i.Uid == give);
        Check(gotB != null && !b.D.Stash.Any(i => i.Uid == take) && b.FromStash(gotB) && b.D.Bag.Contains(gotB),
            $"stash: B sees A's deposit, not the item A took, and takes it ({gotB?.Name})");
        KeepChar("B");
        { var l = LoadSlotFrom(CharCopy("A")); while (l.MoveNext()) yield return l.Current; }
        Check(G.Hero.D.Bag.Any(i => i.Uid == take) && !G.Hero.D.Stash.Any(i => i.Uid == give), "stash: back in A: the taken item is in A's bag, the deposit is gone (B has it)");
        KeepChar("A");
        int all1 = Everything();
        Check(all1 == all0, $"stash: no item lost or doubled across characters and stash ({all1} / {all0})");

        // ── 3. hardcore heroes have their own stash ──
        int normalBefore = StashCount(false);
        G.NewGame(TSlot, "丁", ClassId.Hunter, true);
        { var w = WaitPlay(); while (w.MoveNext()) yield return w.Current; }
        var hcH = G.Hero;
        bool emptyHc = hcH.D.Stash.Count == 0;
        int hcStart = hcH.D.Bag.Count + hcH.D.Equip.Count(x => x != null) + hcH.D.Stash.Count;
        var z = hcH.D.Bag.FirstOrDefault() ?? (hcH.Unequip(EquipSlot.MainHand) ? hcH.D.Bag.FirstOrDefault() : null);
        bool dep = z != null && hcH.StashItem(z);
        KeepChar("HC");
        Check(emptyHc && dep && StashCount(true) == 1 && SharedStash.Hardcore && StashCount(false) == normalBefore,
            $"stash: hardcore hero starts with an empty hardcore stash (empty={emptyHc}), its deposit goes to {Path.GetFileName(StashFileOf(true))} ({StashCount(true)}), the normal stash keeps {StashCount(false)}");
        { var l = LoadSlotFrom(CharCopy("A")); while (l.MoveNext()) yield return l.Current; }
        Check(!G.Hero.D.Stash.Any(i => i.Uid == z?.Uid) && !SharedStash.Hardcore, "stash: a normal hero never sees the hardcore deposit");

        // ── 4. a damaged stash file: the backup is used, and the next save keeps that backup ──
        KeepChar("A");
        SharedStash.Write(force: true);                                      // the backup = the newest stash (a second write of the same state)
        string main = StashFileOf(false), bak = main + ".bak";
        int bakCount = Arr(ReadJson(bak), "Items");
        File.WriteAllText(main, "{\"Ver\":1,\"Tabs\":4,\"Items\":[{\"Uid\":\"broken");
        { var l = LoadSlotFrom(CharCopy("B")); while (l.MoveNext()) yield return l.Current; }
        int got = G.Hero.D.Stash.Count;
        G.SaveNow("test");
        Check(bakCount > 0 && got == bakCount && ReadJson(main) != null && Arr(ReadJson(bak), "Items") == bakCount,
            $"stash: damaged stash file → backup used ({got} item(s), backup had {bakCount}); after saving the main file is whole again and the backup was not overwritten by the damaged file");
        KeepChar("B");

        // ── 5. gear sets pull from the shared stash, only what they reference ──
        { var l = LoadSlotFrom(CharCopy("A")); while (l.MoveNext()) yield return l.Current; }
        var h = G.Hero;
        int made = 0;
        if (h.D.Equip.All(x => x == null))                                // A gave its only piece away above: wear a new helm
        {
            var helm = ItemDb.Generate(10, Rarity.Rare, h.Class, ItemKind.Helm, rng); helm.ReqLevel = 1; h.D.Bag.Add(helm); made++;
            string why = h.WhyCantEquip(helm); if (why != null || !h.Equip(helm)) Log.Info(Cat, $"stash: test helm not worn: {why}");
        }
        int wk = Array.FindIndex(h.D.Equip, x => x != null);
        var worn = wk >= 0 ? h.D.Equip[wk] : null; var wslot = (EquipSlot)Math.Max(0, wk);
        if (worn != null)
        {
            h.SaveSet(1);
            bool moved = h.Unequip(wslot) && h.StashItem(worn);
            var r = h.LoadSet(1, true);
            Check(moved && r.Ok && r.FromStash >= 1 && h.D.Equip[wk] == worn && !h.D.Stash.Contains(worn), $"stash: A's gear set 2 pulls its {wslot} piece back out of the shared stash (from stash {r.FromStash})");
            // put it back in the stash, then B's sets must not take it
            h.Unequip(wslot); h.StashItem(worn);
            KeepChar("A");
            { var l = LoadSlotFrom(CharCopy("B")); while (l.MoveNext()) yield return l.Current; }
            var hb = G.Hero;
            hb.SaveSet(0);
            int before = hb.D.Stash.Count;
            var rb = hb.LoadSet(0, true);
            Check(hb.D.Stash.Any(i => i.Uid == worn.Uid) && hb.D.Stash.Count == before && rb.FromStash == 0, $"stash: B's gear set leaves A's piece in the stash (stash {before} → {hb.D.Stash.Count}, from stash {rb.FromStash})");
            KeepChar("B");
        }
        else { Check(false, "stash: A wears nothing for the gear-set case"); KeepChar("A"); }

        // ── 6. nothing lost overall ──
        int normal = CharItems(CharCopy("A")) + CharItems(CharCopy("B")) + CharItems(CharCopy("C")) + StashCount(false);
        int hcNow = CharItems(CharCopy("HC")) + StashCount(true);
        Check(normal == all0 + made && hcNow == hcStart, $"stash: nothing lost or doubled: normal characters + normal stash {normal} (start {all0} + {made} made by the test), hardcore character + hardcore stash {hcNow} (start {hcStart})");
        Log.Info(Cat, $"stash: file {Path.GetFileName(StashFileOf(false))} {new FileInfo(StashFileOf(false)).Length} bytes, {StashCount(false)} items in {SharedStash.Tabs} tabs");
    }
}
