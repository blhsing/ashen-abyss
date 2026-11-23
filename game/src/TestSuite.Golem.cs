using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// --suite=golem[:part] (GOLEM, tools/golems/PLAN.md): parts book, kinds, golems, boss, forge, persist, migrate, perf, shots
/// (default: all but shots and perf). Uses save slot 95 (--slot is ignored: the suite picks it).
/// </summary>
public sealed partial class TestSuite
{
    private const int GolemSlot = 95;

    private IEnumerator GolemSuite()
    {
        var parts = string.IsNullOrEmpty(_arg) ? new[] { "book", "kinds", "golems", "boss", "forge", "persist", "migrate" } : _arg.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            _section = "golem " + part;
            Log.Info("suite", $"── golem part: {part} ──");
            switch (part)
            {
                case "book": yield return GBook(); break;
                case "kinds": yield return GKinds(); break;
                case "remnants": yield return GHero(20, "town", "g_bone"); yield return GRemnants(); break;
                case "golems": yield return GGolems(); break;
                case "boss": yield return GBoss(); break;
                case "forge": yield return GForge(); break;
                case "persist": yield return GPersist(); break;
                case "migrate": yield return GMigrate(); break;
                case "perf": yield return GPerf(); break;
                case "shots": yield return GShots(); break;
                default: Fail($"golem: unknown part '{part}'"); break;
            }
        }
        Log.Info("suite", $"golem counters: stuck={MinionSystem.Stuck} shockwaves={MinionSystem.Shockwaves} blocks={MinionSystem.Blocks} thorns={MinionSystem.ThornHits} commands={MinionSystem.Commands} reforms={MinionSystem.Reforms} raised={MinionSystem.Raised} leashed={MinionSystem.Leashed} restored={MinionSystem.Restored} capped={MinionSystem.Capped} redirected={MinionSystem.Redirected}");
    }

    private static MinionSystem Ms => MinionSystem.I;

    private IEnumerator GHero(int level, string area, string golem = "g_bone")
    {
        yield return StartHero(ClassId.Soulbinder, level, area, gear: true, god: true, slot: GolemSlot);
        var h = G.Hero;
        h.EnsureBook();
        if (golem != null) h.SetBook(ClassBook.Golem, golem, -1, true, out _);
        if (h.BaseRank("sb_raise") <= 0) h.D.Skills["sb_raise"] = 1;
        if (level >= 7 && h.BaseRank("sb_golem_cmd") <= 0) h.D.Skills["sb_golem_cmd"] = 1;
        h.Recalc();
        yield return Until(() => golem == null || Ms.GolemAlive, 10, "the golem to form");
    }

    private void Cast(string id, Vector2 at, Actor target = null)
    {
        var p = G.Player; var h = G.Hero;
        var d = h.Eff(id);
        if (p.Cds.TryGetValue(id, out var cd)) { cd.Charges = cd.MaxCharges; cd.Left = 0f; }
        p.Resource = h.MaxResource;
        p.Exec.Fire(d, at, target, 1f, false);
    }

    // ───────────────────────── book ─────────────────────────

    private IEnumerator GBook()
    {
        yield return StartHero(ClassId.Soulbinder, 4, "town", gear: true, god: true, slot: GolemSlot);
        var h = G.Hero;
        bool set4 = h.SetBook(ClassBook.Golem, "g_iron", 0, true, out var why4);
        Check(!h.BookUnlocked && !set4, "book: locked below level 5", why4);
        Check(MinionSystem.WantedGolem(h) == null && !Ms.GolemAlive, "book: no golem below level 5");
        yield return GHero(12, "town", null);
        h = G.Hero;
        var bd = h.EnsureBook();
        Check(h.BookKindId(ClassBook.Warriors) == "w_raider" && h.BookKindId(ClassBook.Mages) == "m_shadow" && h.BookKindId(ClassBook.Golem) == "g_bone", "book: default 掠襲者 / 影術士 / 骨魔像",
            string.Join(",", bd.Pick.Select(kv => kv.Key + "=" + kv.Value)));
        Check(!bd.Migrated, "book: a new character is not a migrated save");
        // every kind and both upgrade picks
        int ok = 0, total = 0;
        foreach (var line in h.Book.Lines)
            foreach (var k in line.Kinds)
                for (int up = -1; up <= 1; up++)
                {
                    total++;
                    if (h.SetBook(line.Id, k.Id, up, true, out _) && h.BookKindId(line.Id) == k.Id && h.BookUp(line.Id) == up && (up < 0 || h.HasBookUp(k.Id, up))) ok++;
                }
        Check(ok == total, $"book: every kind with each upgrade pick can be chosen ({ok}/{total})");
        // sacrifice bonuses
        foreach (var l in h.Book.Lines) h.SetBook(l.Id, l.Kinds[0].Id, -1, true, out _);
        float dmg0 = h.S[Stat.DmgAll], regen0 = h.S[Stat.ResourceGen], life0 = h.MaxLife, armor0 = h.Armor;
        h.SetBook(ClassBook.Warriors, ClassBook.Sacrificed, -1, true, out _);
        Check(Mathf.IsEqualApprox(h.S[Stat.DmgAll] - dmg0, 10f), "book: sacrificing the warriors gives +10% damage", $"{dmg0:0.#} → {h.S[Stat.DmgAll]:0.#}");
        h.SetBook(ClassBook.Mages, ClassBook.Sacrificed, -1, true, out _);
        Check(Mathf.IsEqualApprox(h.S[Stat.ResourceGen] - regen0, 15f), "book: sacrificing the mages gives +15% resource regen", $"{regen0:0.#} → {h.S[Stat.ResourceGen]:0.#}");
        h.SetBook(ClassBook.Golem, ClassBook.Sacrificed, -1, true, out _);
        Check(h.MaxLife > life0 * 1.1f && h.Armor > armor0, "book: sacrificing the golem gives +15% life and armour", $"life {life0:0} → {h.MaxLife:0}, armour {armor0:0} → {h.Armor:0}");
        Check(h.SacrificedCount == 3, "book: all three lines sacrificed");
        h.D.Spec = "sb_offering"; h.Recalc();
        Check(Mathf.IsEqualApprox(h.S[Stat.DmgAll] - dmg0, 15f + Hero.AllSacrificedDmg), "book: 獻祭 specialisation raises the sacrifice bonuses by 50% (and all three lines add 三線盡獻)", $"+{h.S[Stat.DmgAll] - dmg0:0.#}% damage");
        h.D.Spec = null; h.Recalc();
        yield return 0.6f;
        Check(!Ms.GolemAlive, "book: a sacrificed golem line has no golem");
        var p = G.Player;
        Cast("sb_raise", p.Pos2 + new Vector2(2, 0));
        yield return 0.6f;
        Check(MinionSystem.Count("sb_raise") == 0 && MinionSystem.Count(MinionSystem.MageTag) == 0, "book: with warriors and mages sacrificed sb_raise raises nothing", $"{World.Minions.Count} minion(s)");
        // the town is free; elsewhere a fee (and one free change for a migrated save)
        long gold = h.D.Gold;
        h.SetBook(ClassBook.Golem, "g_blood", -1, true, out _);
        Check(h.D.Gold == gold, "book: a change in town costs nothing");
        G.EnterArea("wastes");
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && !G.Level.IsTown, 120, "the wastes");
        yield return 0.5f;
        h = G.Hero;
        h.D.Gold = 10;
        Check(!h.SetBook(ClassBook.Golem, "g_iron", -1, false, out var whyFee) && h.BookKindId(ClassBook.Golem) == "g_blood", "book: outside town a change needs the fee", whyFee);
        h.D.Gold = 100000;
        int fee = ClassBook.Fee(h.D.Level);
        Check(h.SetBook(ClassBook.Golem, "g_iron", -1, false, out _) && h.D.Gold == 100000 - fee, $"book: outside town the fee ({fee} gold) is paid");
        h.D.Book.FreeChanges = 1;
        Check(h.SetBook(ClassBook.Golem, "g_bone", -1, false, out _) && h.D.Gold == 100000 - fee && h.D.Book.FreeChanges == 0, "book: a free change is used before any fee");
        foreach (var l in h.Book.Lines) h.SetBook(l.Id, l.Kinds[0].Id, -1, true, out _);
    }

    // ───────────────────────── warriors and mages ─────────────────────────

    private IEnumerator GKinds()
    {
        yield return GHero(20, "town", "g_bone");
        var h = G.Hero; var p = G.Player;
        ClearField(); Ms.Dismiss("suite"); yield return 0.4f;
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint);
        p.Teleport(arena);
        foreach (var (w, m) in new[] { ("w_raider", "m_shadow"), ("w_guard", "m_frost"), ("w_reaper", "m_bone") })
        {
            h.SetBook(ClassBook.Warriors, w, 0, true, out _); h.SetBook(ClassBook.Mages, m, 1, true, out _);
            yield return 0.3f;
            for (int i = 0; i < 4; i++) { Cast("sb_raise", p.Pos2 + new Vector2(2, 0)); yield return 0.25f; }
            yield return 0.5f;
            var war = World.Minions.Where(x => x.SummonSkill == "sb_raise").ToList();
            var mag = World.Minions.Where(x => x.SummonSkill == MinionSystem.MageTag).ToList();
            string wd = MinionArt.OfKind(w).Def, md = MinionArt.OfKind(m).Def;
            Check(war.Count >= 2 && war.All(x => x.Def.Id == wd || x.Def.Id == "minion_thrower") && mag.Count >= 1 && mag.All(x => x.Def.Id == md),
                $"kinds: {w} + {m}: raises alternate warriors and mages of the chosen kinds", $"{war.Count} × {string.Join("/", war.Select(x => x.Def.Id).Distinct())}, {mag.Count} × {string.Join("/", mag.Select(x => x.Def.Id).Distinct())}, models {string.Join("/", war.Concat(mag).Select(x => x.Def.Model).Distinct())}");
            Check(war.Concat(mag).Where(x => x.Def.Id != "minion_thrower").All(x => x.Ctl is SkeletonCtl), $"kinds: {w} + {m}: each has its behaviour (the 擲矛手 upgrade's throwers stay plain)");
            // fight a dummy: the kind's rules run without errors
            int err0 = Log.ErrorCount;
            var dm = Dummy("cultist", 20, p.Pos2 + new Vector2(5, 0), 4e5f);
            int mh0 = _minionHits;
            yield return 4f;
            Check(_minionHits > mh0 && Log.ErrorCount == err0, $"kinds: {w} + {m} fight", $"minion hits {_minionHits - mh0}");
            if (m == "m_frost") Check(dm.Has(StatusId.Slow) || dm.Has(StatusId.Chill) || _minionHits > mh0, "kinds: frost mages slow their target", $"slow={dm.Has(StatusId.Slow)} chill={dm.Has(StatusId.Chill)}");
            if (w == "w_guard") Check(war.Any(x => x.Taunter), "kinds: 持盾者 taunt");
            ClearField(); Ms.Dismiss("suite");
            yield return 0.3f;
        }
        // caps, and the corpse refill
        h.SetBook(ClassBook.Warriors, "w_raider", 1, true, out _);       // 群狼: +1 cap
        int wc = MinionSystem.WarriorCap(h), mc = MinionSystem.MageCap(h);
        Check(wc == h.Eff("sb_raise").SummonMax && wc >= 5, $"kinds: 群狼 raises the warrior cap to {wc}");
        for (int i = 0; i < wc + mc + 3; i++) { Cast("sb_raise", p.Pos2 + new Vector2(2, 0)); yield return 0.15f; }
        yield return 0.4f;
        Check(MinionSystem.Count("sb_raise") == wc && MinionSystem.Count(MinionSystem.MageTag) == mc, $"kinds: casts fill both caps and no more ({MinionSystem.Count("sb_raise")}/{wc} warriors, {MinionSystem.Count(MinionSystem.MageTag)}/{mc} mages)");
        ClearField(); Ms.Dismiss("suite"); Ms.ClearCorpses();
        yield return 0.3f;
        yield return GRemnants();
    }

    // ───────────────────────── remnants: one resource for 魂爆 and the army ─────────────────────────

    private static void AgeRemnants(float age) { foreach (var r in World.Remnants) r.Left = 12f - age; }

    private IEnumerator GRemnants()
    {
        G.EnterArea("wastes");
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && !G.Level.IsTown, 120, "the wastes");
        yield return 1f;
        var p = G.Player; var h = G.Hero;
        foreach (var l in h.Book.Lines) h.SetBook(l.Id, l.Kinds[0].Id, -1, true, out _);
        if (h.BaseRank("sb_explode") <= 0) { h.D.Skills["sb_explode"] = 1; h.Recalc(); }
        ClearField(); Ms.ClearCorpses();
        yield return 0.3f;
        Vector2 aim = p.Aim;
        Vector2 side = p.Pos2 + (p.Pos2 - aim).Normalized() * 5f;           // away from the aim point
        if (World.Grid != null) side = World.Grid.NearestWalk(side);
        Log.Info("suite", $"remnants: hero ({p.Pos2.X:0.0},{p.Pos2.Y:0.0}) aim ({aim.X:0.0},{aim.Y:0.0}) side ({side.X:0.0},{side.Y:0.0})");

        // kills leave remnants (75 %), and no separate corpse list exists any more
        int k0 = World.Remnants.Count;
        for (int i = 0; i < 12; i++) { var v = Dummy("cultist", 1, side + new Vector2(i * 0.3f, 0.5f), 1f); v.TakeHit(new Hit { Source = p, Amount = 99f }); }
        yield return 0.2f;
        int made = World.Remnants.Count - k0;
        Check(made >= 5 && made <= 12, $"remnants: 12 kills left {made} remnants (chance {SkillExec.RemnantChance:P0})");
        // fresh ones are left alone for a while (the player may want them for 魂爆)
        int used0 = MinionSystem.RemnantsUsed, n0 = World.Remnants.Count;
        yield return 2f;
        Check(MinionSystem.RemnantsUsed == used0 && World.Remnants.Count == n0, "remnants: fresh remnants (under 3.5 s) are not consumed");
        // older ones refill the army, with a wisp flying to the hero's side
        AgeRemnants(5f);
        yield return 4f;
        int used = MinionSystem.RemnantsUsed - used0;
        Check(used >= 2 && World.Remnants.Count < n0 && MinionSystem.Count("sb_raise") + MinionSystem.Count(MinionSystem.MageTag) >= 2, $"remnants: the army refills by consuming remnants ({used} consumed, {World.Remnants.Count} left)");
        // the aim zone is protected
        ClearField(); Ms.ClearCorpses();
        Player.TestMouse = p.GetViewport().GetVisibleRect().Size * 0.5f;      // the aim on the hero's own (walkable) ground, whatever the layout
        Vector2 aimWas = p.Aim;
        yield return Until(() => { bool still = p.Aim.DistanceTo(aimWas) < 0.05f; aimWas = p.Aim; return still; }, 6, "the aim point to settle");
        yield return 0.5f;
        aim = p.Aim;
        Vector2 az = World.Grid?.NearestWalk(aim) ?? aim;          // the aim may fall on a wall: the walkable cell next to it
        float guard = MinionSystem.RemnantAimGuard - az.DistanceTo(aim) - 0.2f;
        for (int i = 0; i < 4; i++) p.Exec.SpawnRemnant(az + Vector2.Right.Rotated(i * 1.5f) * Mathf.Clamp(guard, 0f, 1f));
        AgeRemnants(6f);
        used0 = MinionSystem.RemnantsUsed;
        yield return 3.5f;
        Player.TestMouse = null;
        Check(MinionSystem.RemnantsUsed == used0 && World.Remnants.Count == 4, $"remnants: those within {MinionSystem.RemnantAimGuard} m of the aim are kept for 魂爆", $"{World.Remnants.Count} left, aim moved {p.Aim.DistanceTo(aim):0.00} m");
        // 魂爆 still detonates them
        int hits0 = _hits;
        var dm = Dummy("brute", 25, az, 5e5f);
        int before0 = World.Remnants.Count;
        Cast("sb_explode", az);
        yield return 0.8f;
        Check(World.Remnants.Count == 0 && _hits > hits0, "remnants: 魂爆 detonates them", $"{before0} → {World.Remnants.Count} remnants, {_hits - hits0} hits, dummy {dm.Pos2.DistanceTo(az):0.0} m from them");
        // a sacrifice build leaves every remnant alone
        ClearField(); Ms.ClearCorpses(); yield return 0.3f;
        h.SetBook(ClassBook.Warriors, ClassBook.Sacrificed, -1, true, out _); h.SetBook(ClassBook.Mages, ClassBook.Sacrificed, -1, true, out _);
        for (int i = 0; i < 5; i++) p.Exec.SpawnRemnant(side + new Vector2(i * 0.5f, 0));
        AgeRemnants(6f);
        used0 = MinionSystem.RemnantsUsed;
        yield return 4f;
        Check(MinionSystem.RemnantsUsed == used0 && World.Remnants.Count == 5, "remnants: with warriors and mages sacrificed nothing is consumed (all for 魂爆)");
        // a full army takes nothing either
        foreach (var l in h.Book.Lines) h.SetBook(l.Id, l.Kinds[0].Id, -1, true, out _);
        var d = h.Eff("sb_raise");
        for (int i = 0; i < MinionSystem.WarriorCap(h); i++) p.Exec.Raise(MinionArt.OfKind("w_raider").Def, d, p.Pos2, 0.3f);
        for (int i = 0; i < MinionSystem.MageCap(h); i++) p.Exec.Raise(MinionArt.OfKind("m_shadow").Def, d, p.Pos2, 0.3f, MinionSystem.MageTag);
        used0 = MinionSystem.RemnantsUsed;
        yield return 3f;
        Check(MinionSystem.RemnantsUsed == used0, "remnants: a full army consumes nothing");
        // the cast raises at the nearest remnant in reach
        ClearField(); Ms.ClearCorpses(); yield return 0.3f;
        Vector2 spot = p.Pos2 + new Vector2(3f, 2f);
        if (World.Grid != null) spot = World.Grid.NearestWalk(spot);
        p.Exec.SpawnRemnant(spot);
        used0 = MinionSystem.RemnantsUsed;
        Cast("sb_raise", spot + new Vector2(1f, 0f));
        yield return 0.4f;
        var risen = World.Minions.FirstOrDefault(x => x.SummonSkill == "sb_raise");
        Check(MinionSystem.RemnantsUsed == used0 + 1 && World.Remnants.Count == 0 && risen != null && risen.Pos2.DistanceTo(spot) < 3f, "remnants: 喚起骸骨 raises at the nearest remnant and consumes it");
        // the cap of 24
        ClearField(); Ms.ClearCorpses(); yield return 0.2f;
        for (int i = 0; i < 30; i++) p.Exec.SpawnRemnant(side + new Vector2(i % 6, i / 6) * 0.4f);
        Check(World.Remnants.Count == 24, $"remnants: at most 24 on the ground ({World.Remnants.Count})");
        // the golem re-forms without them
        h.SetBook(ClassBook.Golem, "g_bone", -1, true, out _);
        yield return Until(() => Ms.GolemAlive, 6, "the golem");
        int before = World.Remnants.Count;
        Ms.Golem.TakeHit(new Hit { Amount = 1e9f }); yield return 0.3f;
        h.D.Minions.GolemReform = 0.2f;
        yield return Until(() => Ms.GolemAlive, 4, "the re-form");
        Check(World.Remnants.Count == before, "remnants: re-forming the golem consumes none");
        Ms.ClearCorpses();
    }

    // ───────────────────────── golems ─────────────────────────

    private IEnumerator GGolems()
    {
        yield return GHero(25, "town", "g_bone");
        var h = G.Hero; var p = G.Player;
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint);
        p.Teleport(arena);
        foreach (var kind in new[] { "g_bone", "g_blood", "g_iron" })
        {
            ClearField();
            h.D.GolemForge = null;
            h.SetBook(ClassBook.Golem, kind, -1, true, out _);
            yield return Until(() => Ms.GolemAlive && (Ms.GolemCtl?.Kind == kind), 6, $"the {kind} golem");
            var gm = Ms.Golem;
            if (gm == null) { Fail($"golems: {kind} did not form"); continue; }
            var art = MinionArt.OfKind(kind);
            Check(gm.Def.Model == art.Model && gm.LifeTime <= 0f, $"golems: {kind} formed, permanent, drawn as {art.Model}", $"life {gm.MaxLife:0} (hero {p.MaxLife:0}) height {gm.HeightM:0.0} m radius {gm.Radius:0.00}");
            Check(gm.HeightM >= 2.2f && gm.HeightM <= 3.6f, $"golems: {kind} is a large body (2.4–3 m)", $"{gm.HeightM:0.00} m");
            Check(gm.MaxLife > p.MaxLife * 0.6f, $"golems: {kind} life scales with the hero", $"{gm.MaxLife:0} vs {p.MaxLife:0}");
            // golem life affix
            float life0 = gm.MaxLife;
            h.AddBuff(new BuffDef { Id = "suite_golem_life", Name = "x", Stat = Stat.GolemLife, Value = 50 }, 30f);
            yield return 0.7f;
            Check(gm.MaxLife > life0 * 1.4f, $"golems: {kind} follows the golem-life affix", $"{life0:0} → {gm.MaxLife:0}");
            h.RemoveBuffs(b => b.Def.Id == "suite_golem_life");
            // passives against a dummy that fights back
            int sh0 = MinionSystem.Shockwaves, th0 = MinionSystem.ThornHits, rd0 = MinionSystem.Redirected;
            var dm = Dummy("brute", 25, gm.Pos2 + new Vector2(3, 0), 6e5f);
            float gl0 = gm.Life;
            yield return kind == "g_iron" ? 13f : 9f;
            if (!GodotObject.IsInstanceValid(gm) || gm.Dead) { Fail($"golems: {kind} died against one dummy"); continue; }
            switch (kind)
            {
                case "g_bone":
                    Check(dm.TauntBy == gm || dm.Has(StatusId.Taunt), "golems: 骨魔像 taunts what it hits");
                    Check(MinionSystem.ThornHits > th0, "golems: 骨魔像 thorns strike back", $"{MinionSystem.ThornHits - th0} thorn hits");
                    break;
                case "g_blood":
                    for (int k = 0; k < 4; k++) Combat.MonsterHit(dm, p, 3f, Element.Physical);
                    Check(MinionSystem.Redirected > rd0, "golems: 血魔像 takes a share of the hero's damage", $"{MinionSystem.Redirected - rd0} shared hits");
                    break;
                case "g_iron":
                    Check(MinionSystem.Shockwaves > sh0, "golems: 鐵魔像 sends a shockwave every 4th hit", $"{MinionSystem.Shockwaves - sh0} shockwaves, {Ms.GolemCtl.Hits} hits");
                    break;
            }
            // the command
            int c0 = MinionSystem.Commands;
            gm.Heal(gm.MaxLife);
            float hl0 = p.Life; p.Life = p.MaxLife * 0.5f;
            Vector2 aim = gm.Pos2 + new Vector2(4, 1);
            Cast("sb_golem_cmd", aim);
            yield return 1.6f;
            Check(MinionSystem.Commands > c0, $"golems: 魔像號令 commands the {kind}");
            switch (kind)
            {
                case "g_bone": Check(dm.TauntBy == gm && Ms.GolemCtl.CmdDr > 0f, "golems: 骨牢令 taunts and hardens the golem"); break;
                case "g_blood": Check(p.Life > p.MaxLife * 0.5f, "golems: 血潮令 heals the hero", $"{p.Life / p.MaxLife:P0}"); break;
                case "g_iron": Check(gm.Pos2.DistanceTo(aim) < 3.5f, "golems: 鐵擊令 leaps to the cursor", $"{gm.Pos2.DistanceTo(aim):0.0} m from the aim"); break;
            }
            // death and re-forming
            gm.TakeHit(new Hit { Source = dm, Amount = gm.MaxLife * 50f });
            yield return 0.3f;
            Check(!Ms.GolemAlive && Ms.ReformLeft > 15f, $"golems: a fallen {kind} re-forms after 20 s", $"{Ms.ReformLeft:0.0} s");
            h.D.Minions.GolemReform = 0.4f;
            yield return Until(() => Ms.GolemAlive, 4, "the re-form");
            Check(Ms.GolemAlive && Ms.Golem.Pos2.DistanceTo(p.Pos2) < 5f, $"golems: {kind} re-formed at the hero's side");
            Ms.Golem.TakeHit(new Hit { Source = dm, Amount = 1e9f });
            yield return 0.3f;
            Cast("sb_golem_cmd", p.Pos2);
            yield return 0.4f;
            Check(Ms.GolemAlive, $"golems: 魔像號令 re-forms a dead {kind} at once");
            if (kind == "g_iron")
            {
                h.SetBook(ClassBook.Golem, "g_iron", 1, true, out _);       // 重鑄
                yield return Until(() => Ms.GolemAlive, 4, "the iron golem");
                Ms.Golem.TakeHit(new Hit { Source = dm, Amount = 1e9f });
                yield return 0.3f;
                Check(Ms.ReformLeft > 6f && Ms.ReformLeft <= 8f, "golems: 重鑄 re-forms in 8 s", $"{Ms.ReformLeft:0.0} s");
                h.D.Minions.GolemReform = 0.2f;
                yield return Until(() => Ms.GolemAlive, 4, "the iron golem");
            }
            p.Life = hl0;
        }
        // keystone 魔像之心 and 骨甲共享
        ClearField();
        h.SetBook(ClassBook.Golem, "g_bone", 1, true, out _);
        yield return Until(() => Ms.GolemAlive, 6, "the bone golem");
        foreach (var kp in SkillDb.PassivesOf(h.Class).Where(x => x.Key)) h.D.Skills.Remove(kp.Id);
        h.D.Skills["kp_sb_golemheart"] = 1; h.Recalc();
        var hit = new Hit { Source = Ms.Golem, Amount = 1000f };
        hit.Source = Dummy("cultist", 25, p.Pos2 + new Vector2(8, 0), 1e6f);
        float left = MinionSystem.RedirectHero(p, hit);
        Check(Mathf.Abs(left - 1000f * 0.8f * 0.9f) < 1f, "golems: 魔像之心 (20% to the golem) and 骨甲共享 (−10%)", $"1000 → {left:0}");
        float res0 = p.Resource = 0f;
        yield return 4f;
        Check(p.Resource > res0, "golems: 魔像之心 golem hits restore resource", $"{p.Resource:0}");
        h.D.Skills.Remove("kp_sb_golemheart"); h.Recalc();
    }

    private IEnumerator GBoss()
    {
        yield return GHero(25, "town", "g_bone");
        var h = G.Hero; var p = G.Player;
        ClearField();
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint);
        p.Teleport(arena);
        yield return Until(() => Ms.GolemAlive, 6, "the bone golem");
        var bossDef = MonsterDb.All.FirstOrDefault(d => d.AI == MonAI.Boss);
        var boss = Dummy(bossDef?.Id ?? "brute", 25, arena + new Vector2(6, 0), 2e6f, MonTier.Boss);
        yield return 2f;
        Ms.Golem.Pos2 = arena + new Vector2(4, 0);
        Cast("sb_golem_cmd", boss.Pos2);
        yield return 0.6f;
        Check(boss.Brain != null && boss.TauntBy == Ms.Golem, $"boss: {bossDef?.Id} respects 骨牢令 (its target is the golem)", $"brain={boss.Brain != null} taunt={(boss.TauntBy == Ms.Golem ? "golem" : boss.TauntBy?.Name ?? "-")}");
        float gl = Ms.Golem.Life, pl = p.Life;
        yield return 4f;
        Log.Info("suite", $"boss: after 4 s golem life {gl:0} → {(Ms.GolemAlive ? Ms.Golem.Life : 0):0}, hero {pl:0} → {p.Life:0}");
        Pass("boss: a boss fight against the bone golem ran");
        ClearField();
    }

    // ───────────────────────── forging ─────────────────────────

    private static Item MakeItem(ItemKind k, string baseId, int power, Rarity r = Rarity.Rare, string look = null)
    {
        var it = ItemDb.Generate(power, r, k == ItemKind.OffHand ? ClassId.Soulbinder : ClassId.Emberblade, k, new Random(power * 7 + (int)k), baseId);
        if (look != null) it.Look = look;
        it.ReqLevel = 1;
        return it;
    }

    private IEnumerator GForge()
    {
        yield return GHero(30, "town", "g_iron");
        var h = G.Hero; var p = G.Player;
        ClearField();
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint);
        p.Teleport(arena);
        h.D.GolemForge = null; h.D.Bag.Clear();
        yield return Until(() => Ms.GolemAlive, 6, "the iron golem");
        Check(Ms.GolemCtl.Forge == null && Ms.GolemCtl.Style == GolemStyle.Fists, "forge: never forged = plain slag fists");
        var mace = MakeItem(ItemKind.Weapon, "mace2", 300);
        mace.Affixes.Add(new Affix { Stat = Stat.DmgAll, Value = 20 });
        var shield = MakeItem(ItemKind.OffHand, "shield", 300);
        var sword = MakeItem(ItemKind.Weapon, "sword1", 300);
        var pole = MakeItem(ItemKind.Weapon, "polearm", 300);
        var chest = MakeItem(ItemKind.Chest, null, 300, Rarity.Rare, "arm_emberblade_chest_t2");
        var helm = MakeItem(ItemKind.Helm, null, 300, Rarity.Rare, "arm_lampknight_helm_t2");
        var cloth = MakeItem(ItemKind.Chest, null, 300, Rarity.Rare, "arm_soulbinder_chest_t1");
        var staff = MakeItem(ItemKind.Weapon, "staff", 300);
        var ring = MakeItem(ItemKind.Ring, null, 300);
        var locked = MakeItem(ItemKind.Weapon, "axe2", 300); locked.Locked = true;
        var inSet = MakeItem(ItemKind.Weapon, "sword2", 300);
        var legend = MakeItem(ItemKind.Weapon, "axe1", 300, Rarity.Legendary); legend.Aspect = "executioner"; legend.AspectRoll = 0.5f;
        var unusable = MakeItem(ItemKind.Weapon, "dagger", 300, Rarity.Legendary); unusable.Aspect = "multishot"; unusable.AspectRoll = 0.5f;
        foreach (var it in new[] { mace, shield, sword, pole, chest, helm, cloth, staff, ring, locked, inSet, legend, unusable }) h.D.Bag.Add(it);
        h.D.GearSets ??= new();
        var set = new GearSetData { Name = "測試組" }; set.Uids[0] = inSet.Uid; h.D.GearSets.Add(set);
        var worn = h.D.Equip[(int)EquipSlot.MainHand];

        // exclusions
        Check(GolemForge.Refusal(h, cloth) != null, "forge: cloth armour is refused", GolemForge.Refusal(h, cloth));
        Check(GolemForge.Refusal(h, staff) != null, "forge: a wooden staff is refused", GolemForge.Refusal(h, staff));
        Check(GolemForge.Refusal(h, ring) != null, "forge: rings are refused", GolemForge.Refusal(h, ring));
        Check(GolemForge.Refusal(h, locked) != null && !GolemForge.Forge(h, locked, out _) && h.D.Bag.Contains(locked), "forge: a locked item is refused until unlocked", GolemForge.Refusal(h, locked));
        Check(GolemForge.Refusal(h, inSet) != null && !GolemForge.Forge(h, inSet, out _), "forge: an item of a gear set is refused", GolemForge.Refusal(h, inSet));
        Check(worn == null || (GolemForge.Refusal(h, worn) != null && !GolemForge.Forge(h, worn, out _) && h.D.Equip[(int)EquipSlot.MainHand] == worn), "forge: the worn weapon is refused", worn == null ? "-" : GolemForge.Refusal(h, worn));
        var cands = GolemForge.Candidates(h);
        Check(cands.Contains(mace) && cands.Contains(shield) && cands.Contains(chest) && cands.Contains(helm) && !cands.Contains(cloth) && !cands.Contains(locked) && !cands.Contains(inSet),
            $"forge: the picker lists metal weapons, shields and armour only ({cands.Count}: {string.Join(", ", cands.Select(c => c.Base ?? c.Kind.ToString()))})");
        Check(GolemForge.Candidates(h, true).Count == 0, "forge: salvage-only (AI pilot) lists nothing before items are marked as junk");
        var prev = GolemForge.Preview(h, mace);
        Check(prev.Count >= 3 && prev[0].StartsWith("鑄造後"), "forge: the confirmation shows a stat preview", string.Join(" | ", prev));
        Check(GolemForge.Verdict(h, mace)?.StartsWith("鑄造後：魔像攻擊") == true, "forge: metal items carry a verdict line", GolemForge.Verdict(h, mace));

        // forging a two-hand weapon: consumed, inherited, fused, its style
        int bag0 = h.D.Bag.Count;
        Check(GolemForge.Forge(h, mace, out var w1) && !h.D.Bag.Contains(mace) && h.D.Bag.Count == bag0 - 1, "forge: the item is consumed", w1);
        Check(h.D.GolemForge != null && h.D.GolemForge.Uid == mace.Uid && !ReferenceEquals(h.D.GolemForge, mace), "forge: a full copy is kept as the golem's essence");
        yield return Until(() => Ms.GolemAlive && Ms.GolemCtl.Forge != null, 6, "the forged golem");
        var g = Ms.GolemCtl;
        Check(g.Forge.Style == GolemStyle.TwoHand && g.Forge.DmgPct >= 10f && g.Forge.WeaponPct > 0f, "forge: inheritance (2H style, affix damage, weapon damage)", string.Join("; ", g.Forge.Lines));
        bool fused = Ms.Golem.Model.Skeleton.GetChildren().Any(c => c.Name.ToString().StartsWith("GolemFuse_"));
        Check(fused, "forge: the item's model is fused onto the golem", GolemForge.FuseReport);
        // attacks follow the weapon class
        Check(Ms.Golem.Def.AttackAnims.Contains("Sword_Heavy_Combo") && Ms.Golem.Def.Cooldown >= 2.4f, "forge: a 2H weapon gives slow heavy slams");
        // re-forming never costs the item again
        string uid = h.D.GolemForge.Uid; int bag1 = h.D.Bag.Count;
        Ms.Golem.TakeHit(new Hit { Amount = 1e9f });
        yield return 0.3f;
        h.D.Minions.GolemReform = 0.3f;
        yield return Until(() => Ms.GolemAlive, 4, "the re-form");
        Check(Ms.GolemAlive && Ms.GolemCtl.Forge != null && h.D.GolemForge.Uid == uid && h.D.Bag.Count == bag1, "forge: the golem re-forms from the stored essence (nothing lost twice)");
        // other styles
        foreach (var (it, style, what) in new[] { (shield, GolemStyle.Shield, "a shield adds block and a bash"), (sword, GolemStyle.OneHand, "a 1H weapon swings faster"), (pole, GolemStyle.Polearm, "a polearm sweeps") })
        {
            GolemForge.Forge(h, it, out _);
            yield return Until(() => Ms.GolemAlive && Ms.GolemCtl.Forge?.Style == style, 6, $"the {style} golem");
            Check(Ms.GolemAlive && Ms.GolemCtl.Style == style, $"forge: {what}", $"{it.Base}: {string.Join("; ", Ms.GolemCtl.Forge.Lines.Take(3))}");
            if (style == GolemStyle.Shield) Check(Ms.GolemCtl.Forge.BlockPct >= 10f, "forge: shield block inherited", $"{Ms.GolemCtl.Forge.BlockPct:0.#}%");
            var dm = Dummy("brute", 30, Ms.Golem.Pos2 + new Vector2(2.5f, 0), 5e5f);
            int e0 = Log.ErrorCount;
            yield return 5f;
            Check(Log.ErrorCount == e0 && Ms.GolemCtl.Hits > 0, $"forge: the {style} golem fights", $"{Ms.GolemCtl.Hits} hits, blocks {MinionSystem.Blocks}, shockwaves/bashes {MinionSystem.Shockwaves}");
            ClearField(); h.D.Minions.GolemDead = false;
            yield return 0.3f;
        }
        // armour plates
        GolemForge.Forge(h, chest, out _);
        yield return Until(() => Ms.GolemAlive && Ms.GolemCtl.Forge != null && h.D.GolemForge.Uid == chest.Uid, 6, "the plated golem");
        Check(Ms.GolemCtl.Forge.ArmorFlat > 0f && GolemForge.FuseReport.Contains("torso"), "forge: chest armour becomes torso plates", GolemForge.FuseReport);
        GolemForge.Forge(h, helm, out _);
        yield return Until(() => Ms.GolemAlive && Ms.GolemCtl.Forge != null && h.D.GolemForge.Uid == helm.Uid, 6, "the crowned golem");
        Check(GolemForge.FuseReport.Contains("head"), "forge: a helm becomes a head crown", GolemForge.FuseReport);
        // legendary powers
        var a = GolemForge.Inherit(legend, h); var b = GolemForge.Inherit(unusable, h);
        Check(a.Power == "executioner" && !a.PowerUnusable && b.PowerUnusable && b.Lines.Any(l => l.Contains("魔像無法使用")), "forge: usable powers apply, others are greyed as 魔像無法使用");
        // an old forge keeps working if its material no longer counts
        h.D.GolemForge = GolemForge.Copy(staff);
        yield return Until(() => Ms.GolemAlive && Ms.GolemCtl.Forge != null && h.D.Equip != null && Ms.Golem != null && h.D.GolemForge.Uid == staff.Uid, 6, "the old forge");
        Check(Ms.GolemAlive && Ms.GolemCtl.Forge != null, "forge: an old forge keeps working when its material tag changes");
        h.D.GearSets.Remove(set);
        // the AI pilot's rule: only items marked as junk, unless allowed
        h.D.Junk ??= new(); h.D.Junk.Add(sword.Uid);
        Log.Info("suite", $"forge: salvage-only candidates after marking: {GolemForge.Candidates(h, true).Count}");
        // the item tooltip's forge line and the bag filter 可鑄造魔像
        var tipItem = MakeItem(ItemKind.Weapon, "axe2", 320);
        h.D.Bag.Add(tipItem);
        string tip = ItemText.Tooltip(tipItem, null, h.D.Level, true, null, h);
        Check(tip.Contains("鑄造後：魔像攻擊") && tip.Contains("護甲"), "forge: the tooltip of a metal item carries the forge line", GolemForge.Verdict(h, tipItem));
        string clothTip = ItemText.Tooltip(cloth, null, h.D.Level, true, null, h);
        Check(!clothTip.Contains("鑄造後"), "forge: cloth armour has no forge line");
        var bf = new BagFilter(); bf.ApplyPreset("forgeable");
        var picked = bf.Pick(h);
        Check(bf.Active && picked.Count > 0 && picked.All(x => GolemForge.Refusal(h, x) == null) && !picked.Contains(cloth) && !picked.Contains(locked),
            $"forge: the bag filter 可鑄造魔像 shows exactly the forgeable items ({picked.Count})");
        Check(BagFilter.Presets.Any(x => x.id == "forgeable"), "forge: 可鑄造魔像 is a quick filter (shown to Soulbinders)");
        // the book page's picker and confirmation (UI path)
        h.D.Bag.Add(MakeItem(ItemKind.Weapon, "axe2", 320));
        G.Ui.Open("skills", "book");
        yield return 0.5f;
        var tree = G.Ui.GetNodeOrNull<SkillTreeUi>("SkillTreeUi") ?? FindTree();
        var page = tree?.Book;
        Check(page != null && page.Visible, "forge: the 亡者名冊 tab opens in the skill screen");
        if (page != null)
        {
            page.OpenPicker();
            yield return 0.3f;
            Check(page.PickerOpen, "forge: the picker opens");
            var it = GolemForge.Candidates(h).First();
            int n0 = h.D.Bag.Count;
            page.Confirm(it);
            yield return 0.3f;
            Check(page.ConfirmOpen && h.D.Bag.Contains(it), "forge: the confirmation waits before anything is consumed");
            page.DoForge();
            yield return 0.3f;
            Check(!h.D.Bag.Contains(it) && h.D.GolemForge.Uid == it.Uid && h.D.Bag.Count == n0 - 1, "forge: confirming consumes the item");
        }
        G.Ui.Close("skills");
        yield return 0.3f;
    }

    private SkillTreeUi FindTree()
    {
        foreach (var n in G.Ui.FindChildren("*", "", true, false)) if (n is SkillTreeUi t) return t;
        return null;
    }

    // ───────────────────────── persistence ─────────────────────────

    private string Army() => string.Join(",", World.Minions.Where(m => !m.Dead).Select(m => m.Def.Id).OrderBy(x => x));

    private IEnumerator GPersist()
    {
        yield return GHero(25, "town", "g_blood");
        var h = G.Hero; var p = G.Player;
        h.SetBook(ClassBook.Warriors, "w_guard", 0, true, out _); h.SetBook(ClassBook.Mages, "m_frost", 0, true, out _);
        ClearField(); yield return 0.3f;
        for (int i = 0; i < 6; i++) { Cast("sb_raise", p.Pos2 + new Vector2(2, 0)); yield return 0.2f; }
        yield return Until(() => Ms.GolemAlive, 6, "the blood golem");
        Ms.Golem.Life = Ms.Golem.MaxLife * 0.6f;
        yield return 0.8f;
        string army = Army();
        int count = World.Minions.Count;
        Log.Info("suite", $"persist: army before travel: {army}");
        foreach (var dest in new[] { "wastes", "town" })
        {
            G.EnterArea(dest);
            yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level.AreaId == dest, 120, dest);
            yield return 1f;
            Check(Army() == army, $"persist: the army follows into {dest}", $"{Army()}");
            Check(Ms.GolemAlive && Mathf.Abs(Ms.Golem.Life / Ms.Golem.MaxLife - 0.6f) < 0.05f, $"persist: the golem keeps its life into {dest}", Ms.GolemAlive ? $"{Ms.Golem.Life / Ms.Golem.MaxLife:P0}" : "no golem");
            Check(World.Minions.All(m => m.Pos2.DistanceTo(G.Player.Pos2) < 15f), $"persist: in {dest} the army stands at the hero's side");
        }
        // save and load
        G.SaveNow("suite");
        yield return 0.3f;
        G.LoadGame(GolemSlot);
        yield return Until(() => G.State == Game.GameState.Play && G.Hero != null && G.Player != null && G.Level != null && G.Level.Built, 120, "the loaded save");
        yield return 1.2f;
        Check(Army() == army, "persist: the army comes back with a loaded save", Army());
        Check(Ms.GolemAlive && Mathf.Abs(Ms.Golem.Life / Ms.Golem.MaxLife - 0.6f) < 0.06f, "persist: the golem's life comes back with a loaded save");
        // a dead golem stays dead (with its timer) across a change of area
        Ms.Golem.TakeHit(new Hit { Amount = 1e9f });
        yield return 0.3f;
        float left = Ms.ReformLeft;
        G.EnterArea("wastes");
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level.AreaId == "wastes", 120, "wastes");
        yield return 0.3f;
        Check(!Ms.GolemAlive && Ms.ReformLeft > 0f && Ms.ReformLeft <= left, "persist: a dead golem keeps re-forming after a change of area", $"{left:0.0} → {Ms.ReformLeft:0.0} s");
        // the pactbinder's fiend is persistent too
        yield return StartHero(ClassId.Pactbinder, 12, "town", gear: true, god: true, slot: GolemSlot);
        p = G.Player; h = G.Hero;
        if (h.BaseRank("pb_bind") <= 0) { h.D.Skills["pb_bind"] = 1; h.Recalc(); }
        ClearField(); yield return 0.2f;
        Cast("pb_bind", p.Pos2 + new Vector2(2, 0));
        yield return 1f;
        string fiends = Army();
        G.EnterArea("wastes");
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level.AreaId == "wastes", 120, "wastes");
        yield return 1f;
        Check(fiends.Length > 0 && Army() == fiends, "persist: the Pactbinder's fiend follows too", $"{fiends} → {Army()}");
        Check(!G.Hud.GetNodeOrNull<HudMinions>("HudMinions")?.Visible == false, "persist: the minion frame shows for the Pactbinder");
    }

    // ───────────────────────── old saves ─────────────────────────

    private IEnumerator GMigrate()
    {
        yield return null;
        // a 0.3 Soulbinder: no book in the file at all
        var old = Hero.NewCharacter("舊喚魂師", ClassId.Soulbinder, false);
        old.Book = null; old.Level = 22; old.Xp = 12345; old.Skills["sb_raise"] = 3; old.Skills["sb_bolt"] = 2;
        string json = SaveSystem.ToJson(old).Replace("\"Book\":null,", "").Replace(",\"Book\":null", "").Replace(",\"GolemForge\":null", "").Replace(",\"Minions\":null", "");
        Check(!json.Contains("\"Book\""), "migrate: the fixture has no book (as a 0.3 save)");
        var d = SaveSystem.FromJson(json);
        var h = new Hero(d);
        var bd = h.EnsureBook();
        Check(bd != null && bd.Migrated && bd.FreeChanges == 1, "migrate: an old Soulbinder save gets the book and one free change");
        Check(h.BookKindId(ClassBook.Warriors) == "w_raider" && h.BookKindId(ClassBook.Mages) == "m_shadow" && h.BookKindId(ClassBook.Golem) == "g_bone", "migrate: the default book (掠襲者 / 影術士 / 骨魔像)");
        long gold = d.Gold;
        Check(h.SetBook(ClassBook.Golem, "g_iron", 0, false, out _) && d.Gold == gold && bd.FreeChanges == 0, "migrate: the free change works outside town");
        // the book survives a round trip
        var back = SaveSystem.FromJson(SaveSystem.ToJson(d));
        Check(back.Book?.Pick.GetValueOrDefault(ClassBook.Golem) == "g_iron" && back.Book.Migrated, "migrate: the book is saved and loaded");
        // a forged essence survives a round trip
        d.GolemForge = MakeItem(ItemKind.Weapon, "mace2", 200);
        back = SaveSystem.FromJson(SaveSystem.ToJson(d));
        Check(back.GolemForge != null && back.GolemForge.Uid == d.GolemForge.Uid && back.GolemForge.Affixes.Count == d.GolemForge.Affixes.Count, "migrate: the golem's essence is saved as a full item copy");
        // other classes get no book
        var eb = new Hero(Hero.NewCharacter("x", ClassId.Emberblade, false));
        Check(eb.EnsureBook() == null && eb.Book == null, "migrate: other classes have no book");
        // a fixture file, if one is given (tools/golems/fixtures/*.json)
        string fx = ProjectSettings.GlobalizePath("res://../tools/golems/fixtures");
        if (System.IO.Directory.Exists(fx))
            foreach (var f in System.IO.Directory.GetFiles(fx, "*.json"))
            {
                var fd = SaveSystem.FromJson(System.IO.File.ReadAllText(f));
                if (fd == null) { Fail($"migrate: {System.IO.Path.GetFileName(f)} unreadable"); continue; }
                var fh = new Hero(fd);
                var fb = fh.EnsureBook();
                Check(fd.Class != ClassId.Soulbinder || (fb != null && (fb.Migrated || fd.Level <= 1)), $"migrate: fixture {System.IO.Path.GetFileName(f)} ({Names.Class(fd.Class)} lv{fd.Level})");
            }
    }

    // ───────────────────────── performance ─────────────────────────

    private IEnumerator GPerf()
    {
        yield return StartHero(ClassId.Soulbinder, 30, "wastes", gear: true, god: true, slot: GolemSlot);
        var h = G.Hero; var p = G.Player;
        h.SetBook(ClassBook.Golem, "g_iron", 0, true, out _);
        if (h.BaseRank("sb_raise") <= 0) h.D.Skills["sb_raise"] = 1;
        h.D.Skills["sb_colossus"] = Math.Max(1, h.BaseRank("sb_colossus"));
        h.Recalc();
        ClearField();
        Vector2 arena = FindArena(World.Grid, p.Pos2, 8);
        p.Teleport(arena);
        yield return Until(() => Ms.GolemAlive, 6, "the iron golem");
        var d = h.Eff("sb_raise");
        foreach (var k in new[] { "sb_w_raider", "sb_w_guard", "sb_w_reaper", "sb_w_raider" }) p.Exec.Raise(k, d, p.Pos2, p.Exec.CoefOf(d));
        foreach (var k in new[] { "sb_m_shadow", "sb_m_frost", "sb_m_bone", "sb_m_shadow" }) p.Exec.Raise(k, d, p.Pos2, p.Exec.CoefOf(d) * 0.8f, MinionSystem.MageTag);
        Cast("sb_colossus", p.Pos2 + new Vector2(2, 0));
        yield return 1f;
        int minions = World.Minions.Count;
        Check(minions >= 10, $"perf: 4 warriors, 4 mages, the golem and the colossus ({minions} minions: {Army()})");
        // a dense fight: forty monsters around
        string[] pool = { "cultist", "skeleton", "husk", "fiend", "skeleton_thrower", "brute", "cult_caster", "knight" };
        for (int i = 0; i < 40; i++)
        {
            float ang = i * Mathf.Tau / 40f;
            var m = Dummy(pool[i % pool.Length], 30, arena + Vector2.Right.Rotated(ang) * (6f + (i % 3) * 2f), 0f);
            m.MaxLife *= 6f; m.Life = m.MaxLife;
        }
        int frames = 0; double worst = 0, sum = 0; ulong last = Time.GetTicksUsec();
        int err0 = Log.ErrorCount, minMinions = 99;
        double t0 = G.Time;
        while (G.Time - t0 < 20.0)
        {
            yield return null;
            ulong now = Time.GetTicksUsec();
            double ms = (now - last) / 1000.0; last = now;
            if (frames > 5) { worst = Math.Max(worst, ms); sum += ms; }
            frames++;
            // the army stays at full strength for the whole measurement (the perf case is 4 + 4 + golem + colossus in a dense fight)
            foreach (var m in World.Minions) if (!m.Dead) m.Life = m.MaxLife;
            if (frames % 30 == 0)
            {
                int wN = MinionSystem.Count("sb_raise"), mN = MinionSystem.Count(MinionSystem.MageTag);
                for (; wN < 4; wN++) p.Exec.Raise("sb_w_raider", d, p.Pos2, p.Exec.CoefOf(d));
                for (; mN < 4; mN++) p.Exec.Raise("sb_m_shadow", d, p.Pos2, p.Exec.CoefOf(d) * 0.8f, MinionSystem.MageTag);
                if (!World.Minions.Any(x => x.Def.Id == "minion_colossus" && !x.Dead)) Cast("sb_colossus", p.Pos2 + new Vector2(2, 0));
                minMinions = Math.Min(minMinions, World.Minions.Count(x => !x.Dead));
            }
            if (World.Monsters.Count < 25) for (int i = 0; i < 10; i++) { var m = Dummy(pool[i % pool.Length], 30, arena + Vector2.Right.Rotated(i) * 9f, 0f); m.MaxLife *= 6f; m.Life = m.MaxLife; }
        }
        double avg = sum / Math.Max(1, frames - 6);
        Log.Info("suite", $"perf: {frames} frames in 20 s game time, average {avg:0.0} ms ({1000.0 / avg:0} fps), worst {worst:0.0} ms, minions {World.Minions.Count} (never fewer than {minMinions}), monsters {World.Monsters.Count}, shadows off for far skeletons");
        Check(avg < 33.4, $"perf: dense minion fight averages 30 fps or better ({1000.0 / avg:0} fps)");
        Check(Log.ErrorCount == err0, "perf: no errors in the dense fight");
        ClearField();
    }

    // ───────────────────────── pictures ─────────────────────────

    private IEnumerator GShots()
    {
        string dir = "Y:\\shots\\golems\\";
        yield return GHero(30, "town", "g_bone");
        var h = G.Hero; var p = G.Player;
        ClearField();
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint);
        p.Teleport(arena);
        yield return 4.5f;
        foreach (var kind in new[] { "g_bone", "g_blood", "g_iron" })
        {
            h.D.GolemForge = null;
            h.SetBook(ClassBook.Golem, kind, 0, true, out _);
            yield return Until(() => Ms.GolemAlive && Ms.GolemCtl.Kind == kind, 6, kind);
            yield return 2.5f;
            Ms.Golem.Pos2 = p.Pos2 + new Vector2(1.6f, 0.9f); Ms.Golem.FaceDir(new Vector2(-0.2f, 1f)); p.Teleport(p.Pos2 + new Vector2(-1.2f, 0f)); p.FaceDir(new Vector2(0.3f, 1f));
            yield return 0.6f;
            G.HarnessFov(26f); G.Cam?.Snap(); yield return null; yield return null;
            G.HarnessShot($"{dir}golem_{kind}.png"); yield return null;
        }
        // forged variants
        h.SetBook(ClassBook.Golem, "g_iron", 0, true, out _);
        foreach (var (it, tag) in new[] { (MakeItem(ItemKind.Weapon, "mace2", 420, Rarity.Legendary), "forged_2h"), (MakeItem(ItemKind.OffHand, "shield", 420, Rarity.Rare), "forged_shield"), (MakeItem(ItemKind.Chest, null, 420, Rarity.Rare, "arm_emberblade_chest_t3"), "forged_chest") })
        {
            h.D.Bag.Add(it); GolemForge.Forge(h, it, out _);
            yield return Until(() => Ms.GolemAlive && Ms.GolemCtl.Forge != null && h.D.GolemForge.Uid == it.Uid, 6, tag);
            yield return 2.5f;
            Ms.Golem.Pos2 = p.Pos2 + new Vector2(1.6f, 0.9f); Ms.Golem.FaceDir(new Vector2(-0.2f, 1f)); p.Teleport(p.Pos2 + new Vector2(-1.2f, 0f)); p.FaceDir(new Vector2(0.3f, 1f));
            yield return 0.6f;
            G.HarnessFov(24f); G.Cam?.Snap(); yield return null; yield return null;
            G.HarnessShot($"{dir}golem_{tag}.png"); yield return null;
        }
        // the army at gameplay distance, with the HUD frame
        G.HarnessFov(0f);
        h.SetBook(ClassBook.Warriors, "w_guard", 0, true, out _); h.SetBook(ClassBook.Mages, "m_frost", 0, true, out _);
        var d = h.Eff("sb_raise");
        foreach (var k in new[] { "sb_w_raider", "sb_w_guard", "sb_w_reaper" }) p.Exec.Raise(k, d, p.Pos2, 0.3f);
        foreach (var k in new[] { "sb_m_shadow", "sb_m_frost", "sb_m_bone" }) p.Exec.Raise(k, d, p.Pos2, 0.3f, MinionSystem.MageTag);
        yield return 3f;
        int i = 0;
        foreach (var m in World.Minions.Where(x => !x.IsGolem)) { m.Pos2 = p.Pos2 + new Vector2(-3f + i * 1.3f, 2.2f); m.FaceDir(new Vector2(0, 1)); i++; }
        G.Cam?.Snap(); yield return 0.5f;
        G.HarnessShot($"{dir}army_gameplay.png"); yield return null;
        G.HarnessFov(10f); G.Cam?.Snap(); yield return 0.3f;
        G.HarnessShot($"{dir}army_close.png"); yield return null;
        G.HarnessFov(0f);
        // a fight in the wastes
        G.EnterArea("wastes");
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level.AreaId == "wastes", 120, "wastes");
        yield return 4f;
        p = G.Player;
        for (int k = 0; k < 8; k++) Dummy(k % 2 == 0 ? "cultist" : "skeleton", 30, p.Pos2 + Vector2.Right.Rotated(k * 0.8f) * 7f, 0f).MaxLife *= 4f;
        yield return 3f;
        Cast("sb_golem_cmd", p.Pos2 + new Vector2(5, 0));
        yield return 0.35f;
        G.HarnessShot($"{dir}fight_command.png"); yield return null;
        yield return 2f;
        G.HarnessShot($"{dir}fight_wastes.png"); yield return null;
        // the book page, the picker and the confirmation
        p.Teleport(p.Pos2);
        h.D.Bag.Add(MakeItem(ItemKind.Weapon, "axe2", 420, Rarity.Legendary));
        h.D.Bag.Add(MakeItem(ItemKind.Helm, null, 420, Rarity.Rare, "arm_lampknight_helm_t3"));
        G.Ui.Open("skills", "book");
        yield return 0.8f;
        G.HarnessShot($"{dir}ui_book.png"); yield return null;
        var page = FindTree()?.Book;
        if (page != null)
        {
            page.OpenPicker(); yield return 0.5f;
            G.HarnessShot($"{dir}ui_forge_picker.png"); yield return null;
            page.Confirm(GolemForge.Candidates(h).First()); yield return 0.5f;
            G.HarnessShot($"{dir}ui_forge_confirm.png"); yield return null;
            page.ClosePopups();
        }
        G.Ui.Close("skills");
        // the bag with the 可鑄造魔像 filter on (forge badges), then the tooltip of a forgeable item
        h.D.Bag.Add(MakeItem(ItemKind.Chest, null, 400, Rarity.Rare, "arm_soulbinder_chest_t1"));
        h.D.Bag.Add(MakeItem(ItemKind.Weapon, "staff", 400));
        h.D.Bag.Add(MakeItem(ItemKind.OffHand, "shield", 400));
        yield return 0.3f;
        G.Ui.Open("inventory");
        yield return 0.6f;
        InventoryUi inv = null;
        foreach (var n in G.Ui.FindChildren("*", "", true, false)) if (n is InventoryUi iu) { inv = iu; break; }
        if (inv != null)
        {
            var fl = typeof(InventoryUi).GetField("_filter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var on = typeof(InventoryUi).GetField("_filterOn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dirty = typeof(InventoryUi).GetField("_dirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            (fl?.GetValue(inv) as BagFilter)?.ApplyPreset("forgeable");
            on?.SetValue(inv, true); dirty?.SetValue(inv, true);
            inv.Refresh();
            yield return 0.8f;
            var badges = inv.FindChildren("ForgeBadge", "", true, false).OfType<Control>().Where(c => c.IsVisibleInTree()).ToList();
            Check(badges.Count > 0, $"shots: the 可鑄造魔像 filter marks {badges.Count} bag item(s) with the forge badge");
            G.HarnessShot($"{dir}ui_bag_forgeable.png"); yield return null;
            if (badges.Count > 0 && badges[0].GetParent() is Control cell)
            {
                var forgeItem = GolemForge.Candidates(h).FirstOrDefault(x => x.Base == "axe2") ?? GolemForge.Candidates(h).First();
                G.Ui.ShowTooltipAt(cell.GetGlobalRect(), ItemText.Tooltip(forgeItem, null, h.D.Level, true, null, h), null, cell);
                yield return 0.6f;
                G.HarnessShot($"{dir}ui_tooltip_forge.png"); yield return null;
            }
        }
        G.Ui.Close("inventory");
        Pass("shots: golem pictures taken");
    }
}
