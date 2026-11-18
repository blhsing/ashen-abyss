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
        if (_bot == null) { Fail("named: no bot to fight"); yield break; }

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
            yield return StartHero(cls, level, NamedAreas.ArenaOf(d.Id), true, true, tier, paragon, slot: 91);
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
                $"hero lv{level}{(paragon > 0 ? $"+p{paragon}" : "")} tier{tier} godSaves={saves} named life={e.M?.MaxLife ?? 0:0} hp={F(d.Hp)}";
            rows.Add(row);
            Log.Info("suite", "named " + row);
            Check(e.Slain, $"named {d.Id}: killed", $"{F(fight)}s");
            Check(missing.Count == 0, $"named {d.Id}: every mechanic fired", missing.Count > 0 ? "missing " + string.Join(",", missing) : $"{d.Mechs.Length} mechanics");
            if (e.Slain) Check(inWin, $"named {d.Id}: kill time within {lo:0}-{hi:0}s", $"{F(fight)}s");
            _bot.Stats($"named {d.Id}");
        }
        Log.Info("suite", "named: summary");
        foreach (var r in rows) Log.Info("suite", "  " + r);
    }
}
