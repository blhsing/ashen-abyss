using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AshenAbyss;

/// <summary>
/// Exact save resume tests (save-restore agent), one private slot (--slot, default 92):
///   --gptest=saveall --gpcases=name:class:area[:mode],…  each case is played into the slot, saved, and the slot file copied
///       to test_out/case_&lt;name&gt;.json with its expectations in exp_&lt;name&gt;.json. Modes: (none) progress and save;
///       portal = then T to town; boss = then a boss fight is started; dead = then the hero dies; named = then a named
///       encounter is fought; event = a world event finished and another one started; hc = a hardcore hero;
///       hcdead = a hardcore hero that dies (must stay unloadable).
///   --gptest=verifyall --gpcases=name:mode[:src[:bak]],…  a new process: each case file (or one derived from case src) is
///       put into the slot and loaded, then checked. Modes: exact (the default checks), portal, boss, dead, named, event,
///       old (Resume/Portal/Fog removed, format 1), real03 (a real 0.3 save), trunc (cut in half, .bak = case bak),
///       badtypes (Resume of the wrong JSON type), badpos (unusable spot), badrecs (broken records), hcdead.
/// </summary>
public partial class GameplayTest
{
    private static int TSlot => int.TryParse(G.ArgValue("slot"), out int s) && s >= 90 && s <= 99 ? s : 92;
    private static string SlotFile(string ext = "json") => ProjectSettings.GlobalizePath($"user://saves/slot{TSlot}.{ext}");
    private static string CaseFile(string name, string ext = "json") => $"{OutDir}/case_{name}.{ext}";
    private static readonly JsonSerializerOptions ExOpt = new() { IncludeFields = true, WriteIndented = true };

    private Expect ReadEx() { try { return JsonSerializer.Deserialize<Expect>(File.ReadAllText(ExpectPath), ExOpt); } catch (Exception) { return null; } }
    private void WriteEx(Expect ex) => File.WriteAllText(ExpectPath, JsonSerializer.Serialize(ex, ExOpt));

    private static IEnumerator<float> NewHeroEx(int slot, string key, string area, bool hardcore)
    {
        int idx = Math.Max(0, Array.IndexOf(Names.ClassKeys, key));
        G.NewGame(slot, "試煉者", (ClassId)idx, hardcore);
        var w = WaitPlay(); while (w.MoveNext()) yield return w.Current;
        area = area.Replace('=', ':');                                       // "rift=1" in --gpcases (":" separates fields)
        if (area.StartsWith("rift")) G.Hero.D.Set(Quests.FlagCampaign);            // rifts open after the campaign
        var g = Go(area); while (g.MoveNext()) yield return g.Current;
        Log.Info(Cat, $"hero {key} in slot {slot}, area {G.Level.AreaId}, hardcore={G.Hero.D.Hardcore}");
    }

    /// <summary>--gptest=saveall: every case into the one test slot, each copied out after its save.</summary>
    private IEnumerator<float> SaveAll()
    {
        var cases = (G.ArgValue("gpcases") ?? "dun:emberblade:wastes_d2").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Split(':')).ToList();
        foreach (var a in cases)
        {
            string name = a[0], cls = a[1], area = a[2], mode = a.Length > 3 ? a[3] : "";
            bool hc = mode is "hc" or "hcdead";
            Log.Info(Cat, $"=== save case {name}: {cls} in {area} mode={(mode == "" ? "exact" : mode)}");
            var n = NewHeroEx(TSlot, cls, area, hc); while (n.MoveNext()) yield return n.Current;
            _expect = $"{OutDir}/exp_{name}.json";
            IEnumerator<float> run = mode switch
            {
                "portal" => PortalRun(), "boss" => BossRun(), "dead" or "hcdead" => DeadRun(), "named" => NamedRun(), "event" => EventRun(),
                _ => SaveRun(),
            };
            while (run.MoveNext()) yield return run.Current;
            var ex = ReadEx();
            if (ex != null) { ex.Mode = mode == "" || mode == "hc" ? ex.Mode : mode; WriteEx(ex); }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (G.Hero != null && !(mode == "hcdead")) G.SaveNow("test");
            double ms = sw.Elapsed.TotalMilliseconds;
            File.Copy(SlotFile(), CaseFile(name), true);
            long bytes = new FileInfo(CaseFile(name)).Length;
            Log.Info(Cat, $"case {name} saved in {G.Level?.AreaId}: {bytes} bytes, save {ms:0.0} ms → {CaseFile(name)}");
            yield return 0.5f;
        }
    }

    // ───────────────────────── extra save modes ─────────────────────────

    /// <summary>Progress, then the boss fight starts: door open, the hero in the arena, the boss hurt and fighting back.</summary>
    private IEnumerator<float> BossRun()
    {
        var s = SaveRun(); while (s.MoveNext()) yield return s.Current;
        var lv = G.Level; var p = P;
        var boss = lv.BossMon;
        if (boss == null || !IsInstanceValid(boss)) { Check(false, "boss: no boss in this level"); yield break; }
        if (lv.DoorSealed) lv.OpenBossDoor(true);
        yield return 0.3f;
        p.Teleport(lv.StandNear(boss.Pos2, lv.Checkpoint, 3.5f)); G.Cam?.Snap();
        yield return 0.5f;
        boss.TakeHit(new Hit { Source = p, Amount = boss.MaxLife * 0.3f, El = Element.Physical, NoReact = true });
        yield return 2.0f;
        Check(lv.BossEngaged && IsInstanceValid(boss) && !boss.Dead, $"boss: fight on (engaged={lv.BossEngaged}, boss life {(IsInstanceValid(boss) ? boss.Life / boss.MaxLife : 0f):P0})");
        var ex = ReadEx();
        var cp = lv.Checkpoint;
        ex.X = cp.X; ex.Z = cp.Y; ex.NoFacing = true; ex.PosTol = 1.0f; ex.LifeFrac = -1f;
        WriteEx(ex);
        Log.Info(Cat, $"boss state: {boss.Def.Id} life={boss.Life:0}/{boss.MaxLife:0} hero=({p.Pos2.X:0.0},{p.Pos2.Y:0.0}) checkpoint=({cp.X:0.0},{cp.Y:0.0})");
    }

    /// <summary>Progress, then the hero dies (quitting with the death screen up).</summary>
    private IEnumerator<float> DeadRun()
    {
        var s = SaveRun(); while (s.MoveNext()) yield return s.Current;
        var p = P; var lv = G.Level;
        var ex = ReadEx();
        var cp = lv.Checkpoint;
        ex.X = cp.X; ex.Z = cp.Y; ex.NoFacing = true; ex.PosTol = 1.0f; ex.LifeFrac = 1f;
        WriteEx(ex);
        p.Life = 0f; p.Dead = true; p.Die(new Hit { Source = null, Amount = p.MaxLife * 50f, El = Element.Shadow });
        yield return 0.6f;
        Check(p.Dead, $"dead: hero died (hardcore={G.Hero.D.Hardcore}, marked dead={G.Hero.D.Dead})");
    }

    /// <summary>Progress, then a named encounter of this area is fought (hurt, engaged).</summary>
    private IEnumerator<float> NamedRun()
    {
        var s = SaveRun(); while (s.MoveNext()) yield return s.Current;
        var p = P; var lv = G.Level;
        var e = NamedHub.Live.Where(x => IsInstanceValid(x) && x.Alive && !x.LevelManaged && x.L == lv).OrderBy(x => x.M.Pos2.DistanceTo(p.Pos2)).FirstOrDefault();
        if (e == null) { Log.Info(Cat, "named: no named encounter in this area (SKIP)"); yield break; }
        p.Teleport(lv.StandNear(e.M.Pos2, p.Pos2, 3f)); G.Cam?.Snap();
        yield return 0.6f;
        e.M.TakeHit(new Hit { Source = p, Amount = e.M.MaxLife * 0.25f, El = Element.Physical, NoReact = true });
        yield return 2.5f;
        Check(e.Engaged && e.Alive, $"named: {e.Def.Id} fight on (engaged={e.Engaged}, life {e.LifeFrac:P0})");
        var ex = ReadEx();
        ex.Named = e.Def.Id; ex.NamedX = e.Center.X; ex.NamedZ = e.Center.Y; ex.NamedR = e.ArenaR;
        ex.NoFacing = true; ex.PosTol = 9999f; ex.LifeFrac = -1f;          // the hero is moved out of the arena: checked in VerifyMore
        WriteEx(ex);
    }

    /// <summary>Progress, then one world event is finished and another one started.</summary>
    private IEnumerator<float> EventRun()
    {
        var s = SaveRun(); while (s.MoveNext()) yield return s.Current;
        var lv = G.Level; var p = P;
        var ex = ReadEx();
        Vector2 back = p.Pos2; float face = p.Facing;
        for (int i = 0; i < lv.Events.Count && (ex.EventDone < 0 || ex.EventMid < 0); i++)
        {
            var ev = lv.Events[i];
            if (ev.Done || ev.Active || ev is WorldBossEvent) continue;
            if (ex.EventDone < 0) { ev.RestoreDone(); ex.EventDone = i; continue; }          // what a finished event leaves behind
            // start this one: its object (brazier, cursed chest) or walking up to it (ritual)
            p.Teleport(lv.Grid.RandomWalkNear(ev.Pos, 2f, 3f)); G.Cam?.Snap();
            yield return 0.4f;
            var obj = Interactable.All.FirstOrDefault(o => IsInstanceValid(o) && o.CanUse && o.Pos2.DistanceTo(ev.Pos) < 1.6f);
            obj?.Use(p);
            yield return 1.2f;
            if (ev.Active) ex.EventMid = i;
            Log.Info(Cat, $"event {i} {ev.GetType().Name}: active={ev.Active}");
        }
        p.Teleport(back); p.FaceDir(new Vector2(Mathf.Sin(face), Mathf.Cos(face))); G.Cam?.Snap();
        yield return 0.3f;
        ex.X = p.Pos2.X; ex.Z = p.Pos2.Y; ex.Facing = p.Facing; ex.LifeFrac = p.LifeFrac;
        ex.Fog = FogCount(lv);
        Check(ex.EventDone >= 0, $"event: finished event #{ex.EventDone}, started event #{ex.EventMid} (of {lv.Events.Count})");
        WriteEx(ex);
    }

    // ───────────────────────── verify ─────────────────────────

    /// <summary>--gptest=verifyall: puts each case (or a derived file) into the slot, loads it and checks it.</summary>
    private IEnumerator<float> VerifyAll()
    {
        var cases = (G.ArgValue("gpcases") ?? "dun").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Split(':')).ToList();
        foreach (var a in cases)
        {
            string name = a[0], mode = a.Length > 1 ? a[1] : "", src = a.Length > 2 ? a[2] : name, bak = a.Length > 3 ? a[3] : null;
            Log.Info(Cat, $"=== verify case {name}: mode={(mode == "" ? "exact" : mode)} from case {src}{(bak != null ? $", backup {bak}" : "")}");
            if (!File.Exists(CaseFile(src))) { Check(false, $"{name}: case file {CaseFile(src)} missing"); continue; }
            string why = null;
            try { why = Prepare(mode, src, bak); }
            catch (Exception e) { Log.Error(Cat, $"{name}: preparing the slot failed", e); continue; }
            _expect = $"{OutDir}/exp_{(mode == "trunc" ? bak : src)}.json";
            var before = G.Hero;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            G.LoadGame(TSlot);
            if (mode == "hcdead")
            {
                yield return 0.5f;
                var d = SaveSystem.Load(TSlot);
                Check(ReferenceEquals(G.Hero, before) && d != null && d.Hardcore && d.Dead, $"{name}: a dead hardcore hero cannot be loaded (hero kept={ReferenceEquals(G.Hero, before)}, saved hardcore={d?.Hardcore} dead={d?.Dead})");
                Log.Guard(Cat, () => G.Ui?.CloseAll());
                continue;
            }
            var w = WaitPlay(); while (w.MoveNext()) yield return w.Current;
            Log.Info(Cat, $"loaded case {name} into {G.Level.AreaId} in {sw.ElapsedMilliseconds} ms (including the test wait){(why != null ? $"; slot prepared: {why}" : "")}");
            IEnumerator<float> v = mode switch
            {
                "portal" => PortalBack(),
                "old" or "badtypes" or "badpos" or "real03" => OldWay(name, src, mode),
                _ => Verify(),
            };
            if (mode == "badrecs") { var ex = ReadEx(); if (ex != null) { ex.NoFog = true; ex.LifeFrac = -1f; _exOverride = ex; } }
            while (v.MoveNext()) yield return v.Current;
            _exOverride = null;
        }
    }

    private Expect _exOverride;
    private string _prepText;

    /// <summary>Writes the slot files for one case; returns a note on what was changed.</summary>
    private string Prepare(string mode, string src, string bak)
    {
        string text = File.ReadAllText(CaseFile(src));
        if (File.Exists(SlotFile("bak"))) File.Delete(SlotFile("bak"));
        string note = null;
        switch (mode)
        {
            case "old":
            {
                var o = JsonNode.Parse(text).AsObject();
                o.Remove("Resume"); o.Remove("Portal"); o.Remove("Fog"); o["Version"] = 1;
                text = o.ToJsonString(); note = "Resume, Portal and Fog removed, Version 1";
                break;
            }
            case "trunc":
                File.Copy(CaseFile(bak), SlotFile("bak"), true);
                text = text[..(text.Length / 2)]; note = $"main file cut to {text.Length} chars, .bak = case {bak}";
                break;
            case "badtypes":
            {
                var o = JsonNode.Parse(text).AsObject();
                o["Resume"]!["DeadMons"] = "oops"; o["Resume"]!["X"] = "far away";
                text = o.ToJsonString(); note = "Resume.DeadMons and Resume.X of the wrong JSON type";
                break;
            }
            case "badpos":
            {
                var o = JsonNode.Parse(text).AsObject();
                o["Resume"]!["X"] = 1e9; o["Resume"]!["Z"] = -1e9;
                text = o.ToJsonString(); note = "Resume.X/Z = ±1e9";
                break;
            }
            case "badrecs":
            {
                var o = JsonNode.Parse(text).AsObject();
                var r = o["Resume"]!.AsObject();
                r["DeadMons"]!.AsArray().Add(new JsonObject { ["I"] = 99999, ["D"] = "no_such_monster", ["X"] = 1.0, ["Z"] = 1.0 });
                r["DeadMons"]!.AsArray().Add(null);
                r["Cds"]!.AsArray().Add(new JsonObject { ["Id"] = null, ["Left"] = 3.0 });
                r["Gold"]!.AsArray().Add(new JsonObject { ["A"] = -5, ["X"] = 1.0, ["Z"] = 1.0 });
                r["Loot"]!.AsArray().Add(new JsonObject { ["It"] = null, ["X"] = 1.0, ["Z"] = 1.0 });
                r["Hurt"]!.AsArray().Add(new JsonObject { ["I"] = -3, ["D"] = "x", ["X"] = 2.0, ["Z"] = 2.0, ["F"] = 7.0 });
                r["Chests"]!.AsArray().Add(new JsonObject { ["K"] = "not a number", ["X"] = 1e7, ["Z"] = 0.0 });
                r["EventsDone"]!.AsArray().Add(424242);
                r["Fog"] = "@@not base64@@"; r["LifeFrac"] = -4.0; r["RiftTimeLeft"] = -50.0;
                text = o.ToJsonString(); note = "broken records added (unknown monster, null entries, negative gold, bad fog, odd values)";
                break;
            }
        }
        File.WriteAllText(SlotFile(), text);
        _prepText = text;
        return note;
    }

    /// <summary>Saves without a usable resume state: the hero wakes the old way (town, the overworld, or outside the dungeon).</summary>
    private IEnumerator<float> OldWay(string name, string src, string mode)
    {
        yield return 1.0f;
        var lv = G.Level; var p = P;
        string saved = null; int fmt = 0;
        int equipWas = 0;
        try { var o = JsonNode.Parse(_prepText ?? File.ReadAllText(CaseFile(src))).AsObject(); saved = (string)o["Area"]; fmt = (int?)o["Version"] ?? 0; equipWas = (o["Equip"] as JsonArray)?.Count ?? 0; } catch (Exception) { }
        if (saved == null) try { saved = (string)JsonNode.Parse(File.ReadAllText(CaseFile(src)))["Area"]; } catch (Exception) { }
        string expect = saved == null || saved == ZoneDb.Town || saved.StartsWith("rift") ? ZoneDb.Town
            : ZoneDb.Region(saved) != null ? saved : ZoneDb.RegionOf(saved)?.Id ?? ZoneDb.Town;
        Check(lv.AreaId == expect, $"{name}: saved in {saved} (format {fmt}), woke the old way in {lv.AreaId} (expected {expect}) at ({p.Pos2.X:0.0},{p.Pos2.Y:0.0})");
        Check(G.Hero.D.Version == SaveSystem.FormatVersion && G.Hero.D.Equip.Length == SaveSystem.EquipSlots && G.Hero.D.GearSets != null, $"{name}: written back as format {G.Hero.D.Version}, equipment slots {equipWas} → {G.Hero.D.Equip.Length}, gear sets list present");
        var g = GearCheck(name, null); while (g.MoveNext()) yield return g.Current;
    }

    /// <summary>The hero's worn gear is what the save says, and the model shows it.</summary>
    private IEnumerator<float> GearCheck(string name, Expect ex)
    {
        var h = G.Hero; var p = P;
        string sig = GearSet.FromHero(h).Signature;
        float waited = 0f;
        while (p.Model != null && p.Model.GearManaged && (p.Model.Gear == null || p.Model.Gear.Signature != sig) && waited < 6f) { yield return 0.25f; waited += 0.25f; }
        string shown = p.Model?.Gear?.Signature;
        bool managed = p.Model != null && p.Model.GearManaged;
        Check(!managed || shown == sig, $"{name}: model shows the worn gear ({(managed ? shown == sig ? "same loadout" : $"shows '{shown}' worn '{sig}'" : "gear not modular here")}; equip slots {h.D.Equip.Length})");
        if (ex?.GearSig != null)
        {
            var uids = h.D.Equip.Select(i => i?.Uid ?? "-").ToList();
            string sets = string.Join("|", (h.D.GearSets ?? new()).Select(s => s?.Uids == null ? "" : string.Join(",", s.Uids.Select(u => u ?? "-"))));
            Check(sig == ex.GearSig && uids.SequenceEqual(ex.EquipUids) && sets == ex.SetUids,
                $"{name}: gear as saved (loadout {(sig == ex.GearSig ? "same" : $"'{sig}' vs '{ex.GearSig}'")}, worn uids {(uids.SequenceEqual(ex.EquipUids) ? "same" : "DIFFER")}, gear sets {(sets == ex.SetUids ? "same" : "DIFFER")})");
        }
    }

    /// <summary>The checks added for 0.4.0, run at the end of <see cref="Verify"/>.</summary>
    private IEnumerator<float> VerifyMore(Expect ex)
    {
        ex = _exOverride ?? ex;
        var lv = G.Level; var p = P; var h = G.Hero;
        string tag = $"verify[{ex.Mode ?? "exact"}]";
        // wounded monsters
        if (ex.HurtPos.Count > 0)
        {
            int ok = 0;
            for (int i = 0; i < ex.HurtPos.Count; i++)
            {
                var q = ex.HurtPos[i]; var at = new Vector2(q[0], q[1]); string def = i < ex.HurtDef.Count ? ex.HurtDef[i] : null;
                var m = Living.Where(x => x.Def?.Id == def && x.Pos2.DistanceTo(at) < 12f).OrderBy(x => Mathf.Abs(x.Life / x.MaxLife - q[2])).ThenBy(x => x.Pos2.DistanceTo(at)).FirstOrDefault();
                float f = m != null ? m.Life / m.MaxLife : -1f;
                if (m != null && m.Pos2.DistanceTo(at) < 12f && f >= q[2] - 0.02f && f <= q[2] + 0.12f) ok++;      // living monsters wake at their spawn spot; it may have wandered before the save
                else Log.Info(Cat, $"  wounded {def} at ({at.X:0.0},{at.Y:0.0}) saved {q[2]:P0}: nearest {(m == null ? "none" : $"{m.Pos2.DistanceTo(at):0.0} m away at {f:P0}")}");
            }
            Check(ok == ex.HurtPos.Count, $"{tag}: {ok}/{ex.HurtPos.Count} wounded monsters keep their wounds");
        }
        // the hero's life
        if (ex.LifeFrac >= 0f && !lv.IsTown) Check(Mathf.Abs(LevelState.ResumedLife - ex.LifeFrac) < 0.02f, $"{tag}: hero life on waking {LevelState.ResumedLife:P0} (saved {ex.LifeFrac:P0}; now {p.LifeFrac:P0})");
        // gear and gear sets
        { var g = GearCheck(tag, ex); while (g.MoveNext()) yield return g.Current; }
        Check(h.D.Hardcore == ex.Hardcore && !h.D.Dead, $"{tag}: hardcore={h.D.Hardcore} (saved {ex.Hardcore}), alive");
        // rift
        if (ex.RiftTime >= 0f)
            Check(lv.IsRift && Mathf.Abs(lv.RiftPoints - ex.RiftPoints) < 0.01f && lv.RiftTimeLeft <= ex.RiftTime + 0.01f && lv.RiftTimeLeft >= ex.RiftTime - 20f,
                $"{tag}: rift points {lv.RiftPoints:0.0} (saved {ex.RiftPoints:0.0}), time left {lv.RiftTimeLeft:0.0} s (saved {ex.RiftTime:0.0} s), progress {lv.RiftProgress:P0}");
        switch (ex.Mode)
        {
            case "boss":
            {
                var b = lv.BossMon;
                bool whole = b != null && IsInstanceValid(b) && !b.Dead && b.Life >= b.MaxLife - 0.5f;
                Check(whole && !lv.BossEngaged && !lv.DoorSealed && b.Pos2.DistanceTo(p.Pos2) > lv.ArenaRadius * 0.5f,
                    $"{tag}: boss back whole ({(b != null && IsInstanceValid(b) ? $"{b.Life / b.MaxLife:P0}" : "missing")}), fight off={!lv.BossEngaged}, door {(lv.DoorSealed ? "sealed" : "open")}, hero {(b != null && IsInstanceValid(b) ? b.Pos2.DistanceTo(p.Pos2) : 0f):0.0} m from it at the checkpoint");
                break;
            }
            case "dead":
                Check(!p.Dead && p.LifeFrac > 0.99f, $"{tag}: saved while dead, woke alive at the checkpoint with full life ({p.LifeFrac:P0})");
                break;
            case "named":
                if (ex.Named != null)
                {
                    var e = NamedHub.Live.FirstOrDefault(x => IsInstanceValid(x) && x.Def?.Id == ex.Named && x.L == lv);
                    float d = p.Pos2.DistanceTo(new Vector2(ex.NamedX, ex.NamedZ));
                    Check(e != null && e.Alive && e.LifeFrac > 0.99f && !e.Engaged && d > ex.NamedR, $"{tag}: named {ex.Named} back whole ({(e != null ? $"{e.LifeFrac:P0}, engaged={e.Engaged}" : "missing")}), hero {d:0.0} m from its arena centre (radius {ex.NamedR:0})");
                }
                break;
            case "event":
                if (ex.EventDone >= 0) Check(ex.EventDone < lv.Events.Count && lv.Events[ex.EventDone].Done, $"{tag}: finished event #{ex.EventDone} stays finished");
                if (ex.EventMid >= 0)
                {
                    var ev = ex.EventMid < lv.Events.Count ? lv.Events[ex.EventMid] : null;
                    Check(ev != null && !ev.Active && !ev.Done, $"{tag}: event #{ex.EventMid} that was under way starts afresh (active={ev?.Active}, done={ev?.Done})");
                }
                break;
        }
    }
}
