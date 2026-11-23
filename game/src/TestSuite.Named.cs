using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// --suite=named[:filter[:class[:tier]]] — every named encounter in its test arena ("rift~arena:&lt;id&gt;") against a
/// geared hero of the right level, played by the bot's own rotation (the encounter's script may point the bot at a
/// prop to break or a place to stand, as a player would read the fight). One line per encounter: spawned, every
/// mechanic that fired (required and optional), killed or not, the fight's game time against its window.
/// filter: all (default) | rare | stronghold | dungeon | world | pinnacle | id[,id…] | id prefix.
/// </summary>
public sealed partial class TestSuite
{
    private static readonly Dictionary<NamedKind, (float lo, float hi)> NamedWindow = new()
    {
        [NamedKind.Rare] = (20f, 45f), [NamedKind.Stronghold] = (45f, 90f), [NamedKind.Dungeon] = (45f, 90f),
        [NamedKind.World] = (120f, 240f), [NamedKind.Pinnacle] = (120f, 300f),
    };

    /// <summary>The level a hero meets this encounter at in normal play.</summary>
    public static int NamedHeroLevel(NamedDef d)
    {
        if (d.Kind == NamedKind.Pinnacle) return 50;
        int reg = d.Regions.Length > 0 ? d.Regions.Max(r => ZoneDb.Region(r)?.Level ?? 1) : 40;
        int lv = d.Kind switch { NamedKind.World => reg + 6, NamedKind.Rare => reg + 4, _ => reg + 5 };
        return Math.Clamp(Math.Max(lv, 6), 1, Balance.LevelCap);
    }

    private IEnumerator NamedSuite()
    {
        NamedDb.Ensure();
        var parts = _arg.Split(':', StringSplitOptions.TrimEntries);
        string filter = parts.Length > 0 && parts[0].Length > 0 ? parts[0].ToLowerInvariant() : "all";
        var cls = ClassId.Emberblade;
        if (parts.Length > 1 && parts[1].Length > 0) { int ci = Array.IndexOf(Names.ClassKeys, parts[1].ToLowerInvariant()); if (ci >= 0) cls = (ClassId)ci; }
        int tierArg = parts.Length > 2 && int.TryParse(parts[2], out int t) ? t : -1;
        bool god = !(parts.Length > 3 && parts[3].Equals("nogod", StringComparison.OrdinalIgnoreCase));      // :nogod — the hero can die (deaths are counted in the row), drinks potions
        // :legendary — end-game gear: the --gear set re-rolled as legendary items of the top power (the ladder is for strong builds)
        bool legendary = parts.Length > 4 && parts[4].Equals("legendary", StringComparison.OrdinalIgnoreCase);
        if (_bot == null) { Fail("named: no bot to fight"); yield break; }
        if (filter == "ladder") { yield return NamedLadder(cls, god); yield break; }

        var list = NamedDb.All.Where(n => n.Kind != NamedKind.Region).ToList();
        list = filter switch
        {
            "all" => list,
            "rare" => list.Where(n => n.Kind == NamedKind.Rare).ToList(),
            "stronghold" => list.Where(n => n.Kind == NamedKind.Stronghold).ToList(),
            "dungeon" => list.Where(n => n.Kind == NamedKind.Dungeon).ToList(),
            "world" => list.Where(n => n.Kind == NamedKind.World).ToList(),
            "pinnacle" => list.Where(n => n.Kind == NamedKind.Pinnacle).ToList(),
            _ => list.Where(n => filter.Split(',').Any(f => n.Id == f || n.Id.StartsWith(f, StringComparison.Ordinal))).ToList(),
        };
        if (list.Count == 0) { Fail($"named: nothing matches '{filter}'"); yield break; }
        Log.Info("suite", $"named: {list.Count} encounters, class {Names.ClassKey(cls)}");
        var rows = new List<string>();

        foreach (var d in list)
        {
            _section = $"named {d.Id}";
            int level = NamedHeroLevel(d);
            int tier = tierArg >= 0 ? tierArg : d.Kind == NamedKind.Pinnacle ? 2 : 0;
            int paragon = d.Kind == NamedKind.Pinnacle ? 50 : -1;                  // the ladder is after the campaign: a hero there has paragon levels
            yield return StartHero(cls, level, NamedAreas.ArenaOf(d.Id), true, god, tier, paragon, slot: 91);
            if (legendary) EndGameGear(G.Hero, G.Player);
            int deaths = 0, potions = 0; bool wasDead = false; double potionAt = -10, deadAt = 0;
            NamedEncounter e = null;
            yield return Until(() => (e = NamedHub.Find(d.Id)) != null && e.M != null && GodotObject.IsInstanceValid(e.M), 30, $"{d.Id} to spawn");
            if (e == null) { Fail($"named {d.Id}: did not spawn"); rows.Add($"{d.Id,-28} NOT SPAWNED"); continue; }
            Pass($"named {d.Id}: spawned ({e.M.Def.Model}, life {e.M.MaxLife:0}, level {e.M.Level})");
            int saves0 = Player.GodSaves;
            var (lo, hi) = NamedWindow[d.Kind];
            double t0 = G.Time, limit = hi * 2.6;
            var p = G.Player;
            p.Teleport(World.Grid.NearestWalk(e.Center + (G.Level.Checkpoint - e.Center).Normalized() * Mathf.Min(9f, e.ArenaR - 1f)));
            _bot.ResetStats();
            while (!e.Slain && G.Time - t0 < limit && GodotObject.IsInstanceValid(e))
            {
                p = G.Player;
                if (p == null || !GodotObject.IsInstanceValid(p)) { yield return 0.2f; continue; }
                if (p.Dead)
                {
                    if (!wasDead) { wasDead = true; deadAt = G.Time; deaths++; Log.Info("suite", $"named {d.Id}: the hero died ({deaths}) at {G.Time - t0:0}s, boss life {e.LifeFrac:P0}"); }
                    else if (G.Time - deadAt > 3.0) { deadAt = G.Time + 1e6; G.Respawn(); }        // the death screen's 「復活」: back at the checkpoint, the fight starts over
                    yield return 0.2f; continue;
                }
                wasDead = false;
                if (!god && p.LifeFrac < 0.45f && p.Potions > 0 && G.Time - potionAt > 1.5) { potionAt = G.Time; potions++; DrinkPotion(p); }      // what any player does
                Monster target = null;
                Vector2? stand = null;
                bool passive = false;
                try { target = e.S?.BotTarget; stand = e.S?.BotStand; passive = e.S?.BotPassive ?? false; } catch (Exception) { }
                if (target == null || !GodotObject.IsInstanceValid(target) || target.Dead || target.Untargetable)
                {
                    target = e.Alive && !e.M.Untargetable && !passive ? e.M : null;
                    if (target == null)
                    {
                        float bd = float.MaxValue;
                        foreach (var m in World.Monsters) { if (m == null || m.Dead || m.Untargetable || !GodotObject.IsInstanceValid(m) || m.Def.Id == "named_prop" || (passive && m == e.M)) continue; float d2 = m.Pos2.DistanceSquaredTo(p.Pos2); if (d2 < bd && d2 < 30f * 30f) { bd = d2; target = m; } }
                    }
                }
                if (stand != null && p.Pos2.DistanceTo(stand.Value) > 2.4f) p.SetMove(World.Grid.NearestWalk(stand.Value), false);
                else if (target != null) _bot.FightStep(target);
                else if (e.Alive && !passive) p.SetMove(World.Grid.NearestWalk(e.M.Pos2), false);
                yield return 0.12f;
            }
            _bot.FightStop();
            if (!GodotObject.IsInstanceValid(e)) { Fail($"named {d.Id}: the encounter vanished"); rows.Add($"{d.Id,-28} VANISHED"); continue; }
            float fight = e.Slain ? e.FightTime : (float)(G.Time - t0);
            var missing = d.Mechs.Where(k => !e.Fired.Contains(k)).ToList();
            var opt = d.MechsOpt.Where(k => e.Fired.Contains(k)).ToList();
            int saves = Player.GodSaves - saves0;
            bool inWin = e.Slain && fight >= lo && fight <= hi;
            string row = $"{d.Id,-28} {(e.Slain ? "killed" : $"ALIVE {e.LifeFrac:P0}"),-11} {F(fight),6}s window {lo:0}-{hi:0} {(inWin ? "ok" : e.Slain ? (fight < lo ? "FAST" : "SLOW") : "-")} " +
                $"mechs {d.Mechs.Length - missing.Count}/{d.Mechs.Length}{(missing.Count > 0 ? " missing=[" + string.Join(",", missing) + "]" : "")} optional=[{string.Join(",", opt)}]/{d.MechsOpt.Length} " +
                $"hero lv{level}{(paragon > 0 ? $"+p{paragon}" : "")}{(legendary ? " legendary" : "")} tier{tier} {(god ? $"godSaves={saves}" : $"nogod deaths={deaths} potions={potions}")} named life={e.M?.MaxLife ?? 0:0} hp={F(d.Hp)}" +
                $" stand-in={(NamedDb.BaseDef(d, out bool si) != null && si ? "yes" : "no")}";
            rows.Add(row);
            Log.Info("suite", "named " + row);
            Check(e.Slain, $"named {d.Id}: killed", $"{F(fight)}s");
            if (!god) Check(deaths == 0, $"named {d.Id}: no god mode, no death", $"deaths {deaths}, potions {potions}");
            Check(missing.Count == 0, $"named {d.Id}: every mechanic fired", missing.Count > 0 ? "missing " + string.Join(",", missing) : $"{d.Mechs.Length} mechanics");
            if (e.Slain) Check(inWin, $"named {d.Id}: kill time within {lo:0}-{hi:0}s", $"{F(fight)}s");
            _bot.Stats($"named {d.Id}");
        }
        Log.Info("suite", "named: summary");
        foreach (var r in rows) Log.Info("suite", "  " + r);
    }

    /// <summary>The --gear set again, as legendary items of the top power: what a strong post-campaign build wears on the ladder.</summary>
    private static void EndGameGear(Hero h, Player p)
    {
        if (h == null) return;
        var d = h.D;
        int power = ItemDb.MaxPower;
        var rng = new Random(d.Seed ^ 0x1E6E);
        var styles = ItemDb.HandStyles(d.Class);
        string style = styles.Contains("2h") ? "2h" : styles[0];
        var (mh, oh) = ItemDb.RollHands(d.Class, power, style, () => Rarity.Legendary, rng);
        foreach (var w in new[] { mh, oh }) if (w != null) w.ReqLevel = Math.Min(w.ReqLevel, d.Level);
        d.Equip[(int)EquipSlot.MainHand] = mh; d.Equip[(int)EquipSlot.OffHand] = oh;
        ItemKind[] kinds = { ItemKind.Helm, ItemKind.Chest, ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet, ItemKind.Ring, ItemKind.Ring };
        EquipSlot[] slots = { EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Gloves, EquipSlot.Pants, EquipSlot.Boots, EquipSlot.Amulet, EquipSlot.Ring1, EquipSlot.Ring2 };
        for (int i = 0; i < kinds.Length; i++)
        {
            var it = ItemDb.Generate(power, Rarity.Legendary, d.Class, kinds[i], rng);
            it.ReqLevel = Math.Min(it.ReqLevel, d.Level);
            d.Equip[(int)slots[i]] = it;
        }
        h.Recalc();
        if (p != null && GodotObject.IsInstanceValid(p)) p.Heal(h.MaxLife * 10f);
        Log.Info("suite", $"named: end-game gear — legendary items of power {power} ({style}); life={h.MaxLife:0} armor={h.Armor:0} weapon={h.WeaponMin:0}-{h.WeaponMax:0}");
    }

    private static System.Reflection.MethodInfo _potion;
    /// <summary>The potion key (the player's own potion use; the suite has no key presses).</summary>
    private static void DrinkPotion(Player p)
    {
        _potion ??= typeof(Player).GetMethod("UsePotion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        try { _potion?.Invoke(p, null); } catch (Exception ex) { Log.Warn("suite", $"potion: {ex.Message}"); }
    }

    /// <summary>
    /// --suite=named:ladder — the Ember Stair as a player climbs it: the altar stands in town, its panel opens with rung 1
    /// open and rung 2 closed, the altar's 「挑戰」 takes the hero to rung 1, the bot kills the boss, the kill is recorded at
    /// the world tier, rung 2 opens, and the rung's own portal 「登上燼階第二階」 leads to rung 2 where its boss stands.
    /// Run with --named-live=ladder until the ladder is live. The fight itself is the bot's rotation (god mode unless :nogod).
    /// </summary>
    private IEnumerator NamedLadder(ClassId cls, bool god)
    {
        _section = "named ladder";
        yield return StartHero(cls, 50, ZoneDb.Town, true, god, 2, 50, slot: 91);
        yield return 1.0f;
        var altar = G.Level.Markers.FirstOrDefault(k => k.Label == "燼階" && k.Ref is Interactable it && it.Kind == "named_ladder")?.Ref as Interactable;
        if (!Check(altar != null, "ladder: the altar of the Ember Stair stands in town", NamedAreas.LiveLadder ? "live" : "not live (pass --named-live=ladder)")) yield break;
        Check(NamedAreas.LadderOpen(out string why), "ladder: open at world tier 2", why);
        Check(NamedAreas.RungOpen(1) && !NamedAreas.RungOpen(2), "ladder: rung 1 open, rung 2 closed on a fresh hero");
        G.Player.Teleport(World.Grid.NearestWalk(altar.Pos2 + new Vector2(1.5f, 0f)));
        altar.Use(G.Player);
        yield return 0.5f;
        Check(G.Ui.IsOpen(NamedLadderUi.PanelId), "ladder: the altar opens its panel");
        // the panel's 「挑戰」 on rung 1 (the same call the button makes)
        var panel = G.Ui.Find(NamedLadderUi.PanelId) as NamedLadderUi;
        var enter = typeof(NamedLadderUi).GetMethod("Enter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        enter?.Invoke(panel, new object[] { 2 });
        yield return 0.5f;
        Check(G.Level.AreaId == ZoneDb.Town && G.State == Game.GameState.Play, "ladder: a closed rung cannot be entered", G.Level.AreaId);
        G.Ui.Open(NamedLadderUi.PanelId);
        yield return 0.3f;
        enter?.Invoke(panel, new object[] { 1 });
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level.Built && G.Level.AreaId == NamedAreas.RungArea(1), 60, "rung 1");
        Check(G.Level.AreaId == NamedAreas.RungArea(1), "ladder: 「挑戰」 takes the hero to rung 1", G.Level?.AreaId);
        var d1 = NamedDb.RungOf(1);
        NamedEncounter e = null;
        yield return Until(() => (e = NamedHub.Find(d1.Id)) != null && e.M != null && GodotObject.IsInstanceValid(e.M), 30, "rung 1 boss");
        if (!Check(e != null, "ladder: rung 1 boss spawned", d1.Id)) yield break;
        double t0 = G.Time;
        _bot.ResetStats();
        while (!e.Slain && G.Time - t0 < 900 && GodotObject.IsInstanceValid(e))
        {
            var p = G.Player;
            if (p != null && GodotObject.IsInstanceValid(p) && e.Alive)
            {
                Vector2? stand = null; Monster target = null;
                try { stand = e.S?.BotStand; target = e.S?.BotTarget; } catch (Exception) { }
                if (target == null || !GodotObject.IsInstanceValid(target) || target.Dead || target.Untargetable) target = e.M.Untargetable ? null : e.M;
                if (stand != null && p.Pos2.DistanceTo(stand.Value) > 2.4f) p.SetMove(World.Grid.NearestWalk(stand.Value), false);
                else if (target != null) _bot.FightStep(target);
            }
            yield return 0.12f;
        }
        _bot.FightStop();
        if (!Check(e != null && GodotObject.IsInstanceValid(e) && e.Slain, "ladder: rung 1 boss killed", $"{G.Time - t0:0}s")) yield break;
        Check(NamedRecords.BestTier(d1.Id) == 2, "ladder: the kill is recorded at world tier 2", $"best tier {NamedRecords.BestTier(d1.Id)}");
        Check(NamedAreas.RungOpen(2), "ladder: rung 2 opens after rung 1 fell");
        yield return 1.5f;
        var up = G.Level.Markers.FirstOrDefault(k => k.Kind == "exit" && k.Label != null && k.Label.StartsWith("登上燼階"))?.Ref as Interactable;
        if (!Check(up != null, "ladder: the way up stands where the boss fell", up?.Label)) yield break;
        Check(G.Level.Markers.Any(k => k.Kind == "exit" && k.Label != null && k.Label.StartsWith("返回")), "ladder: the way back to town stands too");
        up.Use(G.Player);
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level.Built && G.Level.AreaId == NamedAreas.RungArea(2), 60, "rung 2");
        Check(G.Level.AreaId == NamedAreas.RungArea(2), "ladder: the portal leads to rung 2", G.Level?.AreaId);
        var d2 = NamedDb.RungOf(2);
        NamedEncounter e2 = null;
        yield return Until(() => (e2 = NamedHub.Find(d2.Id)) != null && e2.M != null && GodotObject.IsInstanceValid(e2.M), 30, "rung 2 boss");
        Check(e2 != null, "ladder: rung 2 boss stands", d2.Id);
        Log.Info("suite", $"named ladder: route done, records {string.Join(", ", NamedRecords.Lines().Where(l => l.StartsWith("燼階")))}");
    }
}
