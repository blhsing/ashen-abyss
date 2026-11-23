using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenAbyss;

/// <summary>
/// --suite=items[:fixture dir] (SYSTEMS, modular gear). Runs in memory only — no save slot is read or written:
/// 1. 0.1–0.3 save migration: every *.json in the fixture folder (default Y:/tools/gear/systems/fixtures, copies of
///    old test saves) is read through SaveSystem.FromJson; nothing may be lost (items, affixes, gems, uids), every weapon
///    gets a base its class can hold, each weapon keeps its damage per second and the hero its damage per second of time.
/// 2. Starting gear, hands rules (2H blocks the off hand, bag-full refusal, dual-wield alternation, class limits).
/// 3. Loot spread over the bases of every class, base speeds and damage per second, tooltips.
/// </summary>
public sealed partial class TestSuite
{
    private IEnumerator ItemsSuite()
    {
        _section = "items: migration";
        string dir = string.IsNullOrEmpty(_arg) ? "Y:/tools/gear/systems/fixtures" : _arg;
        var files = System.IO.Directory.Exists(dir) ? System.IO.Directory.GetFiles(dir, "*.json").OrderBy(f => f).ToArray() : Array.Empty<string>();
        Check(files.Length > 0, $"migration: fixtures found in {dir}", $"{files.Length} files");
        foreach (var f in files) { MigrationCase(f); yield return null; }
        MigrateUniqueCase();
        yield return null;

        _section = "items: starting gear";
        foreach (ClassId c in Enum.GetValues<ClassId>())
        {
            var d = Hero.NewCharacter("試煉者", c, false);
            var h = new Hero(d);
            var g = ItemDb.Loadout(c);
            bool armorless = new[] { EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Gloves, EquipSlot.Pants, EquipSlot.Boots, EquipSlot.Amulet, EquipSlot.Ring1, EquipSlot.Ring2 }.All(s => d.Equip[(int)s] == null);
            bool weapon = h.MainHand != null && h.MainHand.Base == g.Starter && (g.StarterOff == null ? h.OffHand == null : h.OffHand?.Base == g.StarterOff);
            Check(armorless && weapon && h.MainHand.Look != null, $"start: {Names.ClassKey(c)} starts with {g.Starter}{(g.StarterOff != null ? "+" + g.StarterOff : "")} and no armour",
                $"main={h.MainHand?.Base} {h.MainHand?.Look} off={h.OffHand?.Base} dps={(h.WeaponMin + h.WeaponMax) * 0.5f * h.SwingsPerSec:0.0}");
        }
        yield return null;

        _section = "items: hands";
        foreach (ClassId c in Enum.GetValues<ClassId>()) { HandsCase(c); yield return null; }

        _section = "items: loot spread";
        LootSpreadCase();
        yield return null;

        _section = "items: bases";
        BaseTableCase();
        yield return null;

        _section = "items: gear evaluator";
        GearEvalCase();
        yield return null;

        _section = "items: bag marks";
        MarksCase();
        yield return null;

        _section = "items: class compatibility";
        CompatCase();
        yield return null;

        _section = "items: gear sets";
        SetsCase();
        yield return null;
    }

    private void SetsCase()
    {
        var rng = new Random(41);
        // emberblade: two-hand ↔ dual-wield
        var d = Hero.NewCharacter("組", ClassId.Emberblade, false); d.Level = 30;
        var h = new Hero(d);
        Item W(ClassId c, string b, ItemKind k = ItemKind.Weapon) { var it = ItemDb.Generate(300, Rarity.Rare, c, k, rng, b); it.ReqLevel = 1; return it; }
        var great = W(ClassId.Emberblade, "sword2"); var axe = W(ClassId.Emberblade, "axe1"); var sword = W(ClassId.Emberblade, "sword1");
        var helm = W(ClassId.Emberblade, null, ItemKind.Helm);
        d.Bag.Clear(); d.Equip[(int)EquipSlot.MainHand] = great; d.Equip[(int)EquipSlot.OffHand] = null; d.Equip[(int)EquipSlot.Helm] = helm; h.Recalc();
        h.SaveSet(0);
        d.Bag.Add(axe); d.Bag.Add(sword);
        h.Equip(axe, EquipSlot.MainHand); h.Equip(sword, EquipSlot.OffHand);
        string bar1 = d.Bar[1]; d.Bar[1] = null;
        h.SaveSet(1);
        int total = d.Bag.Count + d.Equip.Count(x => x != null);
        var r0 = h.LoadSet(0, false);
        bool twoOk = r0.Ok && h.MainHand == great && h.OffHand == null && d.Bag.Contains(axe) && d.Bag.Contains(sword) && h.OffHandBlocked;
        var r1 = h.LoadSet(1, false);
        bool dualOk = r1.Ok && h.MainHand == axe && h.OffHand == sword && h.DualWielding && d.Bag.Contains(great);
        int total2 = d.Bag.Count + d.Equip.Count(x => x != null);
        Check(twoOk && dualOk && total == total2 && d.SetActive == 1 && d.SetPrev == 0, "sets: two-hand ↔ dual-wield swap both ways, nothing duplicated or lost",
            $"items {total}→{total2} main={h.MainHand?.Base} off={h.OffHand?.Base}");
        // protection: set items take no mark and no bulk action
        Check(h.InSet(great) && h.WhyCantMark(great, BagMark.Recycle) != null && !h.ToggleJunk(great) && h.MarkAll(new[] { great }, BagMark.Discard) == 0 && h.RecycleItems().Count == 0,
            "sets: items in a set are protected from marks and bulk actions", h.WhyCantMark(great, BagMark.Recycle));
        // full bag: the swap needs room for what comes off → refused, nothing moves
        while (d.Bag.Count < Hero.BagSize) d.Bag.Add(ItemDb.Generate(50, Rarity.Common, ClassId.Emberblade, ItemKind.Ring, rng));
        d.Bag.Remove(great); d.Bag.Insert(0, great);
        var rf = h.LoadSet(0, false);
        Check(!rf.Ok && rf.Why != null && rf.Why.Contains("行囊") && h.MainHand == axe && h.OffHand == sword, "sets: with a full bag the swap is refused, nothing dropped", rf.Why);
        d.Bag.RemoveAll(x => x.Kind == ItemKind.Ring);
        // missing: the great sword sold → the set loads the rest and lists it; in the stash away from town → missing, in town → pulled out
        d.Bag.Remove(great); d.Stash.Add(great);
        var rm = h.LoadSet(0, false);
        Check(rm.Ok && rm.Missing.Contains(great.Name) && h.MainHand == axe, "sets: an item in the stash away from town is listed as missing (⚠), the slot keeps its item", string.Join("、", rm.Missing));
        var rs = h.LoadSet(0, true);
        Check(rs.Ok && rs.FromStash == 1 && h.MainHand == great && !d.Stash.Contains(great), "sets: in town the stash counts and the item is pulled out", $"from stash {rs.FromStash}");

        var rg = h.LoadSet(1, false);
        // save and reload: the sets come back and still load
        var back = SaveSystem.FromJson(SaveSystem.ToJson(d));
        var hb = new Hero(back);
        var rb = hb.LoadSet(0, false);
        Check(back.GearSets.Count >= 2 && hb.SetName(1) == "裝備組 2" && rb.Ok && hb.MainHand?.Uid == great.Uid && hb.OffHand == null, "sets: sets survive saving and loading",
            $"sets={back.GearSets.Count} main={hb.MainHand?.Base}");
        Check(SaveSystem.FromJson(System.IO.File.Exists("Y:/tools/gear/systems/fixtures/old_hunter_l13.json") ? System.IO.File.ReadAllText("Y:/tools/gear/systems/fixtures/old_hunter_l13.json") : "{}")?.GearSets != null,
            "sets: an old save loads with (empty) gear sets");

        // lamp knight: two-hand ↔ one-hand + shield, and the skill bar goes with the set
        var dl = Hero.NewCharacter("燈", ClassId.Lampknight, false); dl.Level = 30;
        var hl = new Hero(dl);
        var flail = W(ClassId.Lampknight, "flail"); var shield = W(ClassId.Lampknight, "shield", ItemKind.OffHand); var pole = W(ClassId.Lampknight, "polearm");
        dl.Bag.Clear(); dl.Equip[(int)EquipSlot.MainHand] = flail; dl.Equip[(int)EquipSlot.OffHand] = shield; hl.Recalc();
        dl.Bar[2] = null; hl.SaveSet(0);
        dl.Bag.Add(pole); hl.Equip(pole);
        dl.Bar[2] = dl.Bar[0]; hl.SaveSet(1);
        var a = hl.LoadSet(0, false);
        bool offOk = a.Ok && hl.MainHand == flail && hl.OffHand == shield && dl.Bag.Contains(pole) && dl.Bar[2] == null;
        var b = hl.LoadSet(1, false);
        bool twoOk2 = b.Ok && hl.MainHand == pole && hl.OffHand == null && dl.Bag.Contains(flail) && dl.Bag.Contains(shield) && dl.Bar[2] == dl.Bar[0];
        Check(offOk && twoOk2, "sets: one-hand + shield ↔ two-hand, and the skill bar follows the set", $"main={hl.MainHand?.Base} off={hl.OffHand?.Base} bar2={dl.Bar[2]}");
        // a preview of a set for the 裝備組 tab
        var pv = hl.PreviewSet(0);
        Check(pv.MainHand == flail && pv.OffHand == shield && pv.S[Stat.Block] > 0f && hl.MainHand == pole, "sets: the preview wears the set without touching the hero", $"block {pv.S[Stat.Block]:0.#}%");
    }

    private void CompatCase()
    {
        var rng = new Random(23);
        var wand = ItemDb.Generate(200, Rarity.Rare, ClassId.Astromancer, ItemKind.Weapon, rng, "wand");
        var d = Hero.NewCharacter("相", ClassId.Emberblade, false); d.Level = 30;
        var h = new Hero(d);
        d.Bag.Add(wand); wand.ReqLevel = 1;
        string tip = ItemText.Tooltip(wand, null, 30, false, null, h);
        // 可用職業 lists only the classes that can use it (wand: 星術師、喚魂師、縛淵術士), never a name split across lines
        string usable = string.Concat(tip.Split('\n').SkipWhile(l => !l.Contains("可用職業")).Take(3));
        bool onlyUsers = Enum.GetValues<ClassId>().All(c => usable.Contains(Names.Class(c)) == ItemDb.UsableBy(wand, c));
        bool whole = tip.Split('\n').All(l => Enum.GetValues<ClassId>().All(c => { var n = Names.Class(c); return l.Contains(n) || !(l.EndsWith(n[..2]) || l.TrimStart('　').StartsWith(n[2..] + "[")); }));
        Check(tip.Contains("你的職業無法使用") && tip.Contains("可用職業") && onlyUsers && !tip.Contains("[s]"),
            "compat: an unusable weapon's tooltip carries the red banner and lists only the classes that can use it", usable);
        // every base for every hero class: the 可用職業 lines break only at 、 (no line ends inside a class name)
        int splits = 0;
        foreach (var gb in ItemDb.GearBases)
            foreach (ClassId hc in Enum.GetValues<ClassId>())
            {
                var hh = new Hero(Hero.NewCharacter("斷", hc, false));
                var t = ItemText.Tooltip(ItemDb.Generate(100, Rarity.Common, hc, gb.Off ? ItemKind.OffHand : ItemKind.Weapon, new Random(1), gb.Id), null, 30, false, null, hh);
                var lines = t.Split('\n').SkipWhile(l => !l.Contains("可用職業")).TakeWhile(l => l.Contains("可用職業") || l.StartsWith("　")).ToList();
                foreach (var l in lines) { var plain = System.Text.RegularExpressions.Regex.Replace(l, @"\[[^\]]*\]", ""); if (plain.Length > 19 || !(plain.EndsWith("、") || l == lines[^1])) splits++; }
            }
        Check(splits == 0 && whole, "compat: 可用職業 lines wrap only at 、 and stay within a card line", $"bad lines {splits}");
        string why = h.WhyCantEquip(wand);
        Check(why == $"{Names.Class(ClassId.Emberblade)}無法裝備魔杖" && !h.Equip(wand), "compat: equipping it is refused with 「燼刃衛無法裝備魔杖」", why);
        var axe = ItemDb.Generate(200, Rarity.Rare, ClassId.Emberblade, ItemKind.Weapon, rng, "axe1");
        string tip2 = ItemText.Tooltip(axe, null, 30, false, null, h);
        Check(!tip2.Contains("你的職業無法使用") && tip2.Contains("主手／可雙持") && ItemDb.HowHeld(axe, ClassId.Lampknight) == null && ItemDb.HowHeld(axe, ClassId.Wildsoul) == "主手",
            "compat: a usable one-hand axe says how the class holds it (燼刃衛 主手／可雙持, 荒靈行者 主手)");
        int ok = 0;
        foreach (ClassId c in Enum.GetValues<ClassId>())
        {
            var lines = ItemDb.LoadoutLines(c);
            var g = ItemDb.Loadout(c);
            bool good = lines.Length == 3 && lines[0].StartsWith("雙手：") && lines[1].StartsWith("單手：") && lines[2].StartsWith("副手：")
                && g.TwoHand.All(b => lines[0].Contains(ItemDb.BaseOf(b).Name)) && g.OffHand.All(b => lines[2].Contains(ItemDb.BaseOf(b).Name)) && lines[1].Contains("可雙持") == g.CanDual;
            if (good) ok++; else Log.Warn("suite", $"compat: loadout lines {Names.ClassKey(c)}: {string.Join(" / ", lines)}");
            Log.Info("suite", $"loadout {Names.ClassKey(c)}: {string.Join(" / ", lines)}");
        }
        Check(ok == Names.ClassCount, "compat: every class's 可用武器 lines come from its loadout table");
        // loot: about one weapon in ten is forged for another class
        int weapons = 0, foreign = 0;
        for (int i = 0; i < 4000; i++)
        {
            var it = Loot.Roll(30, 1, ClassId.Lampknight);
            if (it == null || it.Kind is not (ItemKind.Weapon or ItemKind.OffHand)) continue;
            weapons++; if (!ItemDb.UsableBy(it, ClassId.Lampknight)) foreign++;
        }
        float share = foreign / (float)Math.Max(1, weapons);
        Check(share > 0.05f && share < 0.16f, "compat: about one weapon drop in ten is another class's (marked ⊘)", $"{foreign}/{weapons} = {share * 100f:0.0}%");
        int alienStock = 0;
        for (int i = 0; i < 6; i++) { Loot.ResetVendors(); alienStock += Loot.VendorStock("merchant", h).Count(e => !ItemDb.UsableBy(e.item, h.Class)); }
        Check(alienStock >= 6, "compat: the merchant always racks one or two other classes' weapons", $"{alienStock} over 6 shelves");
    }

    // ───────────────────────── 4. GearEval ─────────────────────────

    private static readonly JsonSerializerOptions CloneOpt = new() { IncludeFields = true };
    private static Item CloneItem(Item it) { var c = JsonSerializer.Deserialize<Item>(JsonSerializer.Serialize(it, CloneOpt), CloneOpt); c.Uid = Guid.NewGuid().ToString("N")[..12]; return c; }

    private static Item Plain(ItemKind k, int power, ClassId c, string baseId, params (Stat s, float v)[] affixes)
    {
        var it = ItemDb.Generate(power, Rarity.Rare, c, k, new Random(3), baseId);
        it.Affixes.Clear();
        foreach (var (s, v) in affixes) it.Affixes.Add(new Affix { Stat = s, Value = v });
        it.Aspect = null; it.ReqLevel = 1; it.Sockets = 0; it.Gems.Clear();
        return it;
    }

    private void GearEvalCase()
    {
        // a level-30 emberblade in a full rare kit
        var d = Hero.NewCharacter("評", ClassId.Emberblade, false); d.Level = 30;
        var h = new Hero(d);
        Game.AutoGear(h);
        var w = Plain(ItemKind.Weapon, 300, ClassId.Emberblade, "sword2", (Stat.Str, 40));
        d.Equip[(int)EquipSlot.MainHand] = w; d.Equip[(int)EquipSlot.OffHand] = null;
        h.Recalc();

        // 1. the same weapon with a strictly higher roll
        var better = CloneItem(w); better.DmgMin *= 1.2f; better.DmgMax *= 1.2f; better.Affixes[0].Value = 55;
        d.Bag.Add(better);
        var jb = GearEval.Judge(h, better);
        Check(jb.Verdict == GearVerdict.Better && jb.Off > 0.05f, "gear eval: a strictly higher roll is 升級", $"{GearEval.Line(jb)}");
        var same = CloneItem(w);
        var js = GearEval.Judge(h, same);
        Check(js.Verdict == GearVerdict.Similar && MathF.Abs(js.Off) < 0.001f && MathF.Abs(js.Def) < 0.001f, "gear eval: an identical copy is 相近 at ±0%", GearEval.Line(js));
        var worse = CloneItem(w); worse.DmgMin *= 0.7f; worse.DmgMax *= 0.7f;
        Check(GearEval.Judge(h, worse).Verdict == GearVerdict.Worse, "gear eval: a weaker copy is 較差", GearEval.Line(GearEval.Judge(h, worse)));
        string tip = ItemText.Tooltip(better, h.Compared(better), 30, true, null, h);
        Check(tip.Contains("升級：攻擊 +") && tip.Contains("防禦"), "gear eval: the tooltip carries the verdict line", tip.Split('\n').FirstOrDefault(l => l.Contains("攻擊 ")));
        d.Bag.Remove(better);

        // 2. crit for armour on a ring: offence falls, defence rises by about as much → 相近
        var ringA = Plain(ItemKind.Ring, 300, ClassId.Emberblade, null, (Stat.CritChance, 9f));
        var filler = Plain(ItemKind.Ring, 400, ClassId.Emberblade, null, (Stat.GoldFind, 10f));
        d.Equip[(int)EquipSlot.Ring1] = ringA; d.Equip[(int)EquipSlot.Ring2] = filler; h.Recalc();
        var before = GearEval.Baseline(h);
        float o0 = GearEval.Offense(before), d0 = GearEval.Defense(before);
        (float off, float def) Try(float armor)
        {
            var rb = Plain(ItemKind.Ring, 300, ClassId.Emberblade, null, (Stat.Armor, armor));
            var s = GearEval.Simulate(h, rb);
            return (GearEval.Offense(s) / o0 - 1f, GearEval.Defense(s) / d0 - 1f);
        }
        float lo = 1f, hi = 4000f;
        for (int i = 0; i < 30; i++) { float mid = (lo + hi) * 0.5f; var (of, de) = Try(mid); if (de < -of) lo = mid; else hi = mid; }
        var ringB = Plain(ItemKind.Ring, 300, ClassId.Emberblade, null, (Stat.Armor, MathF.Round(hi)));
        var jr = GearEval.Judge(h, ringB);
        Check(jr.Verdict == GearVerdict.Similar && jr.Off < 0f && jr.Def > 0f, "gear eval: swapping crit for armour is 相近", $"{GearEval.Line(jr)} (armour {MathF.Round(hi)} for 9% crit; replaces {h.Compared(ringB)?.Affixes.FirstOrDefault()?.Stat})");

        // 3. a two-hand weapon against main hand + off hand (lamp knight, sword and shield)
        var dl = Hero.NewCharacter("盾", ClassId.Lampknight, false); dl.Level = 30;
        var hl = new Hero(dl);
        var sword = Plain(ItemKind.Weapon, 300, ClassId.Lampknight, "sword1");
        var shield = Plain(ItemKind.OffHand, 300, ClassId.Lampknight, "shield", (Stat.MaxLife, 200f));
        dl.Equip[(int)EquipSlot.MainHand] = sword; dl.Equip[(int)EquipSlot.OffHand] = shield; hl.Recalc();
        var great = Plain(ItemKind.Weapon, 300, ClassId.Lampknight, "sword2");
        var sim = GearEval.Simulate(hl, great);
        var b0 = GearEval.Baseline(hl);
        float off2 = GearEval.Offense(sim) / GearEval.Offense(b0) - 1f, def2 = GearEval.Defense(sim) / GearEval.Defense(b0) - 1f;
        var jg = GearEval.Judge(hl, great);
        var (wo, wd) = GearEval.Weights(GearEval.Weighting);
        Check(sim.MainHand == great && sim.OffHand == null && def2 < 0f && MathF.Abs(jg.Def - def2) < 1e-4f && jg.Verdict == GearEval.Verdict(off2, def2, wo * off2 + wd * def2)
              && hl.MainHand == sword && hl.OffHand == shield,
            "gear eval: a two-hand weapon is weighed against main hand + shield together (the real hero untouched)", $"{GearEval.Line(jg)} block {hl.S[Stat.Block]:0.#}% → {sim.S[Stat.Block]:0.#}%");
        var foreign = Plain(ItemKind.Weapon, 300, ClassId.Astromancer, "staff");
        Check(GearEval.Judge(hl, foreign).Verdict == GearVerdict.Unusable && GearEval.Line(GearEval.Judge(hl, foreign)).Contains("無法使用"), "gear eval: a base the class cannot hold is 無法使用");
        var leg = ItemDb.Generate(300, Rarity.Legendary, ClassId.Lampknight, ItemKind.Helm, new Random(9)); leg.ReqLevel = 1;
        Check(GearEval.Judge(hl, leg).Special && GearEval.Line(GearEval.Judge(hl, leg)).Contains("含特殊效果"), "gear eval: a legendary power is noted as 含特殊效果");
        // the weighting changes the total only
        var cfg = Game.I?.Cfg;
        if (cfg != null)
        {
            int keep = cfg.GearWeight;
            cfg.GearWeight = 1; GearEval.Invalidate(); float tOff = GearEval.Judge(h, ringB).Total;
            cfg.GearWeight = 2; GearEval.Invalidate(); float tDef = GearEval.Judge(h, ringB).Total;
            cfg.GearWeight = keep; GearEval.Invalidate();
            Check(tOff < 0f && tDef > 0f, "gear eval: 偏重攻擊 / 偏重防禦 tip the crit-for-armour swap each way", $"total {tOff * 100f:0.0}% / {tDef * 100f:0.0}%");
        }
    }

    // ───────────────────────── 5. bag marks, locks, presets ─────────────────────────

    private void MarksCase()
    {
        var rng = new Random(17);
        var d = Hero.NewCharacter("標", ClassId.Lampknight, false); d.Level = 20;
        var h = new Hero(d);
        Item Add(Rarity r, int power, ItemKind k, string b = null) { var it = ItemDb.Generate(power, r, ClassId.Lampknight, k, rng, b); it.ReqLevel = 1; d.Bag.Add(it); return it; }
        var lowC = Add(Rarity.Common, 100, ItemKind.Helm);
        var lowM = Add(Rarity.Magic, 120, ItemKind.Boots);
        var highM = Add(Rarity.Magic, 260, ItemKind.Boots);
        var rareLow = Add(Rarity.Rare, 100, ItemKind.Gloves);
        var bow = ItemDb.Generate(150, Rarity.Common, ClassId.Hunter, ItemKind.Weapon, rng, "bow"); d.Bag.Add(bow);
        var legend = Add(Rarity.Legendary, 100, ItemKind.Ring);
        var gem = ItemDb.MakeGem("ruby1"); d.Bag.Add(gem);
        var locked = Add(Rarity.Common, 90, ItemKind.Pants);
        h.ToggleLock(locked);

        var f = new BagFilter();
        f.ApplyPreset("lowcommon");
        var low = f.Pick(h);
        Check(low.Contains(lowC) && low.Contains(lowM) && low.Contains(bow) && low.Contains(locked) && !low.Contains(highM) && !low.Contains(rareLow) && !low.Contains(gem) && low.Count == 4,
            "presets: 低於目前等級的普通與魔法 picks plain low pieces", string.Join(", ", low.Select(i => i.Name)));
        f.ApplyPreset("otherclass");
        Check(f.Pick(h).SequenceEqual(new[] { bow }), "presets: 非本職業 picks the bow of a lamp knight", string.Join(", ", f.Pick(h).Select(i => i.Name)));
        f.ApplyPreset("nonupgrade");
        var nonUp = f.Pick(h);
        Check(!nonUp.Contains(gem) && nonUp.All(i => h.Judge(i) != GearVerdict.Better), "presets: 全部非升級 holds no upgrade and no gem",
            string.Join(", ", nonUp.Select(i => $"{i.Name}={GearEval.VerdictName(h.Judge(i))}")));
        // a combined filter: rarity AND kind AND affix
        var f2 = new BagFilter(); f2.Rarities[(int)Rarity.Magic] = true; f2.Kind = Array.FindIndex(BagFilter.Kinds, k => k.kind == ItemKind.Boots);
        Check(f2.Pick(h).Count == 2 && f2.Pick(h).All(i => i.Kind == ItemKind.Boots && i.Rarity == Rarity.Magic), "filter: rarity and kind combine");
        var stat = highM.Affixes[0].Stat;
        var f3 = new BagFilter { Affix = stat };
        Check(f3.Pick(h).Contains(highM) && f3.Pick(h).All(i => i.Affixes.Any(a => a.Stat == stat) || i.Implicits.Any(a => a.Stat == stat)), $"filter: has affix {StatInfo.Name(stat)}");

        // bulk marks skip locked items; gems cannot be recycled; marks exclude each other
        f.ApplyPreset("lowcommon");
        int n = h.MarkAll(f.Pick(h), BagMark.Recycle);
        Check(n == 3 && h.MarkOf(lowC) == BagMark.Recycle && h.MarkOf(locked) == BagMark.None, "marks: bulk 回收 marks the shown items but never a locked one", $"marked {n}");
        Check(h.WhyCantMark(gem, BagMark.Recycle) != null && h.SetMark(gem, BagMark.Discard) && h.MarkOf(gem) == BagMark.Discard, "marks: a gem can be marked 丟棄 but not 回收");
        h.SetMark(lowC, BagMark.Discard);
        Check(h.MarkOf(lowC) == BagMark.Discard && !h.IsJunk(lowC), "marks: 回收 and 丟棄 exclude each other");
        Check(!h.SetMark(locked, BagMark.Discard) && !h.ToggleJunk(locked), "marks: a locked item refuses every mark", h.WhyCantMark(locked, BagMark.Discard));

        // persistence through a save round trip (and an old save loads with empty marks)
        var back = SaveSystem.FromJson(SaveSystem.ToJson(d));
        var hb = new Hero(back);
        var lc = back.Bag.First(i => i.Uid == lowC.Uid); var lm = back.Bag.First(i => i.Uid == lowM.Uid); var lk = back.Bag.First(i => i.Uid == locked.Uid);
        Check(hb.MarkOf(lc) == BagMark.Discard && hb.MarkOf(lm) == BagMark.Recycle && lk.Locked && !back.Bag.First(i => i.Uid == highM.Uid).Locked,
            "marks: 回收 / 丟棄 marks and locks survive saving and loading");
        string old = System.IO.Directory.Exists("Y:/tools/gear/systems/fixtures") ? System.IO.Directory.GetFiles("Y:/tools/gear/systems/fixtures", "*.json").FirstOrDefault() : null;
        if (old != null) { var od = SaveSystem.FromJson(System.IO.File.ReadAllText(old)); Check(od.Discards != null && od.Bag.All(i => !i.Locked), "marks: an old save loads with no marks and no locks"); }

        // discard the marked: counts by rarity, locked items stay
        int bagBefore = d.Bag.Count;
        var by = h.DiscardMarked();
        Check(by.Sum() == 2 && !d.Bag.Contains(lowC) && !d.Bag.Contains(gem) && d.Bag.Contains(locked) && d.Bag.Count == bagBefore - 2 && h.DiscardItems().Count == 0,
            "marks: 丟棄 throws away exactly the marked items", $"by rarity {string.Join("/", by)}");
        var rec = h.RecycleItems();
        var sum = h.SalvageMany(rec);
        Check(sum.Count == rec.Count && rec.Count == 2 && d.Bag.Contains(locked) && h.RecycleItems().Count == 0, "marks: 回收 salvages exactly the marked items", $"recycled {sum.Count}: {Crafting.Mats(sum.Scrap, sum.Dust, sum.Cores)}");
    }

    // ───────────────────────── 1. migration ─────────────────────────

    private sealed class OldWeapon { public string Uid; public float Min, Max, Aps; public int Upgrade, WeaponClass; public bool Worn; }

    private void MigrationCase(string file)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(file);
        string json = System.IO.File.ReadAllText(file);
        // what the old save holds, read straight from the JSON (no game code in between)
        var oldItems = new Dictionary<string, string>();          // uid → affixes + gems signature
        var oldWeapons = new List<OldWeapon>();
        int oldEquipLen = 0;
        ClassId cls;
        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;
            cls = (ClassId)root.GetProperty("Class").GetInt32();
            void Read(JsonElement it, bool worn)
            {
                if (it.ValueKind != JsonValueKind.Object) return;
                string uid = it.GetProperty("Uid").GetString();
                oldItems[uid] = Sig(it.GetProperty("Affixes"), it.TryGetProperty("Gems", out var gm) ? gm : default, it.GetProperty("Name").GetString());
                if (it.GetProperty("Kind").GetInt32() == 0)
                    oldWeapons.Add(new OldWeapon { Uid = uid, Min = it.GetProperty("DmgMin").GetSingle(), Max = it.GetProperty("DmgMax").GetSingle(), Aps = it.GetProperty("Aps").GetSingle(),
                        Upgrade = it.GetProperty("Upgrade").GetInt32(), WeaponClass = it.GetProperty("WeaponClass").GetInt32(), Worn = worn });
            }
            var eq = root.GetProperty("Equip");
            oldEquipLen = eq.GetArrayLength();
            int i = 0;
            foreach (var it in eq.EnumerateArray()) { Read(it, i == 0); i++; }
            foreach (var it in root.GetProperty("Bag").EnumerateArray()) Read(it, false);
            foreach (var it in root.GetProperty("Stash").EnumerateArray()) Read(it, false);
        }
        var worn0 = oldWeapons.FirstOrDefault(w => w.Worn);
        float oldHeroRate = worn0 == null ? 3f : (worn0.Min + worn0.Max) * 0.5f * (1f + 0.025f * worn0.Upgrade);   // old: one hit per animation beat

        SaveData d = null;
        try { d = SaveSystem.FromJson(json); }
        catch (Exception e) { Log.Error("suite", $"migration {name}: load threw", e); }
        if (!Check(d != null, $"migration {name}: the old save loads")) return;
        var all = d.Bag.Concat(d.Stash).Concat(d.Equip.Where(x => x != null)).ToList();
        var byUid = all.GroupBy(x => x.Uid).ToDictionary(g => g.Key, g => g.First());

        // nothing lost: every old item is still there with the same affixes, gems and (except a base rename) name
        int missing = 0, changed = 0;
        foreach (var kv in oldItems)
        {
            if (!byUid.TryGetValue(kv.Key, out var it)) { missing++; continue; }
            string now = string.Join(";", it.Affixes.Select(a => $"{(int)a.Stat}:{a.Value:0.###}:{a.Tempered}:{a.Bonus:0.##}")) + "|" + string.Join(",", it.Gems);
            if (kv.Value.Split('#')[0] != now) { changed++; Log.Warn("suite", $"migration {name}: {it.Name} affixes {kv.Value} → {now}"); }
        }
        Check(missing == 0 && changed == 0 && d.Equip.Length == SaveSystem.EquipSlots && oldEquipLen == 9,
            $"migration {name} ({Names.ClassKey(cls)}): all {oldItems.Count} items kept with their affixes and gems", $"missing={missing} changed={changed} equip {oldEquipLen}→{d.Equip.Length}");

        // every weapon: a base the class may hold (or its unique's base), the same damage per second as before
        int badBase = 0, badDps = 0;
        foreach (var w in oldWeapons)
        {
            var it = byUid.GetValueOrDefault(w.Uid);
            if (it == null) continue;
            var owner = w.WeaponClass >= 0 ? (ClassId)w.WeaponClass : cls;
            if (it.Gb == null || it.Gb.Off || (!ItemDb.Allows(owner, it.Base, EquipSlot.MainHand) && it.Unique == null)) { badBase++; Log.Warn("suite", $"migration {name}: {it.Name} got base '{it.Base}'"); }
            // the old game swung at the class's pace (RefAps; genuine old saves store exactly that as Aps)
            float before = (w.Min + w.Max) * 0.5f * ItemDb.Loadout(owner).RefAps, after = (it.DmgMin + it.DmgMax) * 0.5f * it.Aps;
            if (MathF.Abs(w.Aps - ItemDb.Loadout(owner).RefAps) > 0.001f) Log.Info("suite", $"migration {name}: {it.Name} stored Aps {w.Aps} ≠ class pace {ItemDb.Loadout(owner).RefAps} (hand-made fixture)");
            if (MathF.Abs(after - before) > before * 0.0005f + 0.01f) { badDps++; Log.Warn("suite", $"migration {name}: {it.Name} dps {before:0.00} → {after:0.00}"); }
        }
        Check(badBase == 0 && badDps == 0, $"migration {name}: {oldWeapons.Count} weapons → class bases, damage per second unchanged",
            string.Join(" ", oldWeapons.Select(w => byUid.GetValueOrDefault(w.Uid)?.Base ?? "?")));

        // the hero: the worn weapon is in the main hand and deals at least the damage per second of time it did
        Hero h = null;
        try { h = new Hero(d); } catch (Exception e) { Log.Error("suite", $"migration {name}: hero threw", e); }
        if (h != null)
        {
            float newRate = (h.WeaponMin + h.WeaponMax) * 0.5f * h.SpeedMul;
            bool mainOk = worn0 == null || h.MainHand?.Uid == worn0.Uid;
            Check(mainOk && newRate >= oldHeroRate * 0.995f && d.Equip.All(x => x == null || x.Look != null),
                $"migration {name}: old Weapon slot → MainHand, hero damage per second kept, every worn piece has a look",
                $"main={h.MainHand?.Base} {h.MainHand?.Look} off={h.OffHand?.Base} {h.OffHand?.Look} rate {oldHeroRate:0.0} → {newRate:0.0} (speed ×{h.SpeedMul:0.00})");
        }

        // idempotent: saving and loading again changes nothing
        var again = SaveSystem.FromJson(SaveSystem.ToJson(d));
        var all2 = again.Bag.Concat(again.Stash).Concat(again.Equip.Where(x => x != null)).ToList();
        bool same = all2.Count == all.Count && all2.All(x => byUid.TryGetValue(x.Uid, out var y) && y.Base == x.Base && y.Look == x.Look && MathF.Abs(y.DmgMax - x.DmgMax) < 0.001f);
        Check(same, $"migration {name}: a second load changes nothing", $"items {all.Count} → {all2.Count}");
    }

    private static string Sig(JsonElement affixes, JsonElement gems, string name)
    {
        var a = affixes.ValueKind == JsonValueKind.Array ? affixes.EnumerateArray().Select(x =>
            $"{x.GetProperty("Stat").GetInt32()}:{x.GetProperty("Value").GetSingle():0.###}:{(x.TryGetProperty("Tempered", out var t) && t.GetBoolean())}:{(x.TryGetProperty("Bonus", out var b) ? b.GetSingle() : 0f):0.##}") : Enumerable.Empty<string>();
        var g = gems.ValueKind == JsonValueKind.Array ? gems.EnumerateArray().Select(x => x.GetString()) : Enumerable.Empty<string>();
        return string.Join(";", a) + "|" + string.Join(",", g) + "#" + name;
    }

    private void MigrateUniqueCase()
    {
        var it = new Item { Kind = ItemKind.Weapon, Rarity = Rarity.Unique, Unique = "u_reaper", WeaponClass = (int)ClassId.Soulbinder, Aps = 1.0f, DmgMin = 50, DmgMax = 70, Power = 300, Name = "寂夜收割者" };
        float dps0 = (it.DmgMin + it.DmgMax) * 0.5f * it.Aps;
        ItemDb.Migrate(it, ClassId.Soulbinder);
        Check(it.Base == "scythe2" && it.WeaponClass == (int)ClassId.Soulbinder && it.Look == "wpn_scythe2_v3" && MathF.Abs((it.DmgMin + it.DmgMax) * 0.5f * it.Aps - dps0) < 0.01f && it.Name == "寂夜收割者",
            "migration: a class unique keeps its class lock and lands on its own base", $"base={it.Base} look={it.Look} class={it.WeaponClass}");
        foreach (var u in ItemDb.Uniques.Where(u => u.Kind == ItemKind.Weapon))
        {
            var g = ItemDb.Generate(300, Rarity.Unique, (ClassId)u.Class, ItemKind.Weapon, new Random(1));
            Check(g.Unique != null && ItemDb.Allows((ClassId)u.Class, g.Base, EquipSlot.MainHand), $"uniques: {u.Id} rolls on a base {Names.ClassKey((ClassId)u.Class)} can hold", $"{g.Unique} base={g.Base}");
        }
    }

    // ───────────────────────── 2. hands ─────────────────────────

    private void HandsCase(ClassId c)
    {
        var rng = new Random(31 + (int)c);
        var g = ItemDb.Loadout(c);
        string key = Names.ClassKey(c);
        var d = Hero.NewCharacter("手", c, false);
        d.Level = 50;
        var h = new Hero(d);
        Item Make(string b, ItemKind k = ItemKind.Weapon) { var it = ItemDb.Generate(300, Rarity.Rare, c, k, rng, b); it.ReqLevel = 1; return it; }

        // a base of another class's loadout is refused
        var foreign = ItemDb.GearBases.FirstOrDefault(b => !b.Off && !ItemDb.Allows(c, b.Id, EquipSlot.MainHand) && !ItemDb.Allows(c, b.Id, EquipSlot.OffHand));
        if (foreign != null)
        {
            var f = Make(foreign.Id); h.AddItem(f);
            Check(h.WhyCantEquip(f) != null && !h.Equip(f), $"hands {key}: a {foreign.Id} is refused", h.WhyCantEquip(f));
            d.Bag.Remove(f);
        }

        // one-hand + off-hand, then a two-hand weapon takes the off-hand piece to the bag
        if (g.OffHand.Length > 0 && g.TwoHand.Length > 0)
        {
            var one = Make(g.MainHand[0]); var off = Make(g.OffHand[0], ItemKind.OffHand); var two = Make(g.TwoHand[0]);
            d.Bag.Clear(); h.AddItem(one); h.AddItem(off); h.AddItem(two);
            bool a = h.Equip(one) && h.Equip(off) && h.MainHand == one && h.OffHand == off && !h.OffHandBlocked;
            string tip = ItemText.Tooltip(two, h.Compared(two), 50, true, h.ComparedExtra(two));
            bool b = h.Equip(two) && h.MainHand == two && h.OffHand == null && d.Bag.Contains(off) && d.Bag.Contains(one) && h.OffHandBlocked;
            Check(a && b && tip.Contains("同時卸下") && tip.Contains("雙手武器"), $"hands {key}: a two-hand weapon moves the off-hand piece to the bag (and the tooltip says so)",
                $"main={h.MainHand?.Base} off={h.OffHand?.Base} bag={d.Bag.Count}");
            // and an off-hand piece moves the two-hand weapon out
            bool c2 = h.Equip(off) && h.OffHand == off && h.MainHand == null && d.Bag.Contains(two);
            Check(c2, $"hands {key}: an off-hand piece takes the two-hand weapon off", $"main={h.MainHand?.Base} off={h.OffHand?.Base}");
            // bag full: the two-hand weapon is refused while both hands are full
            h.Equip(one);
            while (d.Bag.Count < Hero.BagSize) d.Bag.Add(ItemDb.Generate(50, Rarity.Common, c, ItemKind.Ring, rng));
            string why = h.WhyCantEquip(two);
            bool refused = why != null && !h.Equip(two) && h.MainHand == one && h.OffHand == off;
            Check(refused && why.Contains("行囊已滿"), $"hands {key}: with a full bag the two-hand weapon is refused, nothing moves", why);
            if (off.Gb?.Id == "shield") Check(off.BlockChance > 0 && h.S[Stat.Block] >= off.BlockChance - 0.01f && ItemText.Tooltip(off).Contains("格擋"), $"hands {key}: the shield gives block", $"block {h.S[Stat.Block]:0.#}%");
            d.Bag.Clear();
        }

        // dual-wield: two one-hand weapons, hands alternate, speed from both
        if (g.CanDual)
        {
            var w1 = Make(g.Dual[0]); var w2 = Make(g.Dual[g.Dual.Length - 1]);
            d.Equip[(int)EquipSlot.MainHand] = null; d.Equip[(int)EquipSlot.OffHand] = null;
            d.Bag.Clear(); h.AddItem(w1); h.AddItem(w2);
            bool eq = h.Equip(w1) && h.Equip(w2) && h.MainHand == w1 && h.OffHand == w2 && h.DualWielding;
            float aps = (w1.Aps + w2.Aps) * 0.5f * ItemDb.DualSpeed;
            int s1 = h.NextStrike(); float r1 = h.StrikeMax; int s2 = h.NextStrike(); float r2 = h.StrikeMax;
            bool alt = s1 != s2 && MathF.Abs(r1 - r2) > 0.001f == MathF.Abs(w1.DmgMax - w2.DmgMax) > 0.001f;
            Check(eq && alt && MathF.Abs(h.Aps - aps) < 0.001f, $"hands {key}: dual-wield alternates hands, speed from both weapons",
                $"aps {h.Aps:0.00} (expected {aps:0.00}) strikes {s1},{s2} speed ×{h.SpeedMul:0.00}");
        }
        else if (g.MainHand.Length > 0)
        {
            // a class that cannot dual-wield may not put a weapon in the off hand
            var w1 = Make(g.MainHand[0]); h.AddItem(w1);
            Check(h.WhyCantEquip(w1, EquipSlot.OffHand) != null, $"hands {key}: no weapon in the off hand", h.WhyCantEquip(w1, EquipSlot.OffHand));
            d.Bag.Remove(w1);
        }

        // every allowed base can be worn and gives a sane weapon line
        int ok = 0, bad = 0;
        foreach (var b in g.TwoHand.Concat(g.MainHand))
        {
            var w = Make(b); d.Bag.Clear(); h.AddItem(w);
            d.Equip[(int)EquipSlot.OffHand] = null;
            if (h.Equip(w, EquipSlot.MainHand) && h.WeaponMax > h.WeaponMin && h.SpeedMul > 0.6f && ItemText.Tooltip(w).Contains("每秒攻擊")) ok++;
            else { bad++; Log.Warn("suite", $"hands {key}: {b} → equip {h.MainHand?.Base} range {h.WeaponMin:0}-{h.WeaponMax:0}"); }
        }
        Check(bad == 0, $"hands {key}: every base of the loadout can be wielded ({ok})");
    }

    // ───────────────────────── 3. loot ─────────────────────────

    private void LootSpreadCase()
    {
        foreach (ClassId c in Enum.GetValues<ClassId>())
        {
            var g = ItemDb.Loadout(c);
            var counts = new Dictionary<string, int>();
            int weapons = 0, offs = 0, foreign = 0, n = 3000;
            for (int i = 0; i < n; i++)
            {
                var it = Loot.Roll(30, 1, c);
                if (it == null || it.Kind is not (ItemKind.Weapon or ItemKind.OffHand)) continue;
                if (it.Kind == ItemKind.Weapon) weapons++; else offs++;
                counts[it.Base ?? "?"] = counts.GetValueOrDefault(it.Base ?? "?") + 1;
                bool fits = it.Kind == ItemKind.OffHand ? ItemDb.Allows(c, it.Base, EquipSlot.OffHand) : ItemDb.Allows(c, it.Base, EquipSlot.MainHand);
                if (!fits) foreign++;
            }
            var all = g.TwoHand.Concat(g.MainHand).Concat(g.OffHand).Distinct().ToList();
            int missing = all.Count(b => counts.GetValueOrDefault(b) < Math.Max(3, (weapons + offs) / (all.Count * 6)));
            string table = string.Join(" ", counts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}"));
            Log.Info("suite", $"loot spread {Names.ClassKey(c)} ({n} rolls, lv30 champion): weapons={weapons} offhands={offs} | {table}");
            float fs = foreign / (float)Math.Max(1, weapons + offs);
            Check(missing == 0 && fs <= Loot.ForeignShare * 1.6f, $"loot: {Names.ClassKey(c)} drops spread over all {all.Count} of its bases (other classes' bases {fs * 100f:0}%)", $"missing={missing} foreign={foreign} {table}");
        }
    }

    private void BaseTableCase()
    {
        // per class, each base at power 300: damage per second of time with the base alone (one-hand: 90% by design)
        foreach (ClassId c in Enum.GetValues<ClassId>())
        {
            var g = ItemDb.Loadout(c);
            var rows = new List<string>();
            float lo = float.MaxValue, hi = 0f;
            foreach (var b in g.TwoHand.Concat(g.MainHand))
            {
                var d = Hero.NewCharacter("表", c, false); d.Level = 30;
                var w = ItemDb.Generate(300, Rarity.Common, c, ItemKind.Weapon, new Random(5), b); w.DmgMin = w.DmgMax = MathF.Round(ItemDb.WeaponAvg(300) * (w.TwoHand ? 1f : ItemDb.OneHandDps) / w.Aps);
                d.Equip[(int)EquipSlot.MainHand] = w; d.Equip[(int)EquipSlot.OffHand] = null;
                var h = new Hero(d);
                float rate = (h.WeaponMin + h.WeaponMax) * 0.5f * h.SwingsPerSec / h.StatSpeedMul / (w.TwoHand ? 1f : ItemDb.OneHandDps);      // base implicits (a fist's speed) left out
                lo = MathF.Min(lo, rate); hi = MathF.Max(hi, rate);
                rows.Add($"{b}:{w.Aps:0.00}aps hit{w.DmgMin:0} ×{h.SpeedMul:0.00} dps{(h.WeaponMin + h.WeaponMax) * 0.5f * h.SwingsPerSec:0}");
            }
            Log.Info("suite", $"bases {Names.ClassKey(c)}: {string.Join(" | ", rows)}");
            Check(hi <= lo * 1.02f, $"bases {Names.ClassKey(c)}: every base gives the same damage per second (2H 100%, 1H 90%)", $"{lo:0.0}–{hi:0.0}");
        }
    }
}
