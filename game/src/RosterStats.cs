using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// Test measurement for roster balance (bot / AI-pilot / tour runs only): time-to-kill per monster tier and damage
/// the hero takes, per area, from the first hit on a monster to its death. One `[roster] stats` line per area when the
/// next level is built, plus a running `[roster] totals` line, so a campaign with --bestiary can be set against one
/// with --no-bestiary area by area. Installed by <see cref="Populate.Run"/>; does nothing in normal play.
/// </summary>
public static class RosterStats
{
    private sealed class Acc
    {
        public double[] TtkSum = new double[4]; public int[] TtkN = new int[4];
        public double Taken, Secs; public int Kills, Hits;
        public readonly Dictionary<string, double> ByType = new();
        public readonly Dictionary<string, (double sum, int n)> TtkByType = new();
        public void Add(Acc o)
        {
            for (int i = 0; i < 4; i++) { TtkSum[i] += o.TtkSum[i]; TtkN[i] += o.TtkN[i]; }
            Taken += o.Taken; Secs += o.Secs; Kills += o.Kills; Hits += o.Hits;
            foreach (var kv in o.ByType) { ByType.TryGetValue(kv.Key, out var v); ByType[kv.Key] = v + kv.Value; }
            foreach (var kv in o.TtkByType) { TtkByType.TryGetValue(kv.Key, out var v); TtkByType[kv.Key] = (v.sum + kv.Value.sum, v.n + kv.Value.n); }
        }
    }

    private static readonly string[] Tiers = { "normal", "champion", "elite", "boss" };
    private static readonly Dictionary<ulong, double> FirstHit = new();
    private static readonly Dictionary<string, Acc> Totals = new();
    private static Acc _cur;
    private static string _area;
    private static double _since;
    private static bool _installed, _on;

    /// <summary>Called when a level is built: closes the previous area's line and starts the next.</summary>
    public static void OnLevel(Level L)
    {
        var g = Game.I;
        if (g == null) return;
        if (!_installed)
        {
            _installed = true;
            _on = g.Arg("bot") || g.Arg("pilot-test") || g.Arg("tour") || g.Arg("roster-stats");
            if (!_on) return;
            World.Damage += OnDamage;
            World.MonsterKilled += OnKilled;
            if (Engine.GetMainLoop() is SceneTree tree) Tick(tree);
        }
        if (!_on) return;
        Flush();
        FirstHit.Clear();
        _cur = new Acc();
        _area = L == null ? "-" : L.IsRift ? "rift" : L.AreaId;
        _since = g.Time;
    }

    // every 120 s of play: the running line of the current area (a run that quits in its last area still reports it)
    private static void Tick(SceneTree tree)
    {
        tree.CreateTimer(120.0, false).Timeout += () =>
        {
            Log.Guard("roster", () => { if (_cur != null && _area != null) { _cur.Secs = (Game.I?.Time ?? 0) - _since; Log.Info("roster", $"running area={_area} hero=lv{Game.I?.Hero?.D.Level} {Line(_cur)}"); } });
            Log.Guard("roster", Stall);
            Tick(tree);
        };
    }

    // no kill since the last tick while monsters are about: who is near the hero and what are they doing (a bot deadlock)
    private static int _lastKills = -1;
    private static void Stall()
    {
        var p = Game.I?.Player;
        if (_cur == null || p == null || p.Dead) return;
        int k = _cur.Kills;
        bool stalled = k == _lastKills && World.Monsters.Count > 0;
        _lastKills = k;
        if (!stalled) return;
        var near = World.Monsters.Where(m => m != null && GodotObject.IsInstanceValid(m) && !m.Dead).OrderBy(m => m.Pos2.DistanceSquaredTo(p.Pos2)).Take(6)
            .Select(m => $"{m.Def?.Id}({m.Tier}{(m.Objective ? ",marked" : "")}) d={m.Pos2.DistanceTo(p.Pos2):0.0} life={m.Life / Mathf.Max(1f, m.MaxLife) * 100f:0}% walk={(World.Grid != null && World.Grid.WalkAt(m.Pos2) ? "y" : "n")} untarget={m.Untargetable} hidden={m.AiHidden} {m.DebugState} {m.AiState}");
        var marked = World.Monsters.Where(m => m != null && GodotObject.IsInstanceValid(m) && !m.Dead && m.Objective)
            .Select(m => $"{m.Def?.Id} d={m.Pos2.DistanceTo(p.Pos2):0} walk={(World.Grid != null && World.Grid.WalkAt(m.Pos2) ? "y" : "n")} untarget={m.Untargetable} hidden={m.AiHidden}");
        Log.Info("roster", $"stall: no kill in 120 s at ({p.Pos2.X:0.0},{p.Pos2.Y:0.0}) area={_area}; near: {string.Join(" | ", near)}; marked: [{string.Join(", ", marked)}]");
    }

    private static int TierOf(Monster m)
    {
        if (m.Tier == MonTier.Boss || m.Brain != null) return 3;
        if (m.Tier == MonTier.Champion) return 1;
        if (m.Tier == MonTier.Elite || m.MiniBoss) return 2;
        return 0;
    }

    private static void OnDamage(Actor a, Hit h, float amount)
    {
        if (_cur == null || a == null || amount <= 0f) return;
        if (a is Player)
        {
            _cur.Taken += amount; _cur.Hits++;
            string id = h.Source is Monster sm ? sm.Def?.Id ?? "?" : h.Dot ? "dot" : "other";
            _cur.ByType.TryGetValue(id, out var v); _cur.ByType[id] = v + amount;
            return;
        }
        if (a is Monster m && m.Faction == Faction.Monster && h.Source != null && h.Source.Faction != Faction.Monster)
            FirstHit.TryAdd(m.GetInstanceId(), Game.I?.Time ?? 0);
    }

    private static void OnKilled(Monster m, Hit h)
    {
        if (_cur == null || m == null) return;
        _cur.Kills++;
        if (!FirstHit.Remove(m.GetInstanceId(), out double t0)) return;
        if (m.Def?.Family is "minion" or "prop") return;
        int tier = TierOf(m);
        _cur.TtkSum[tier] += (Game.I?.Time ?? 0) - t0; _cur.TtkN[tier]++;
        if (tier <= 1 && m.Def != null) { string key = tier == 1 ? m.Def.Id + "*" : m.Def.Id; _cur.TtkByType.TryGetValue(key, out var v); _cur.TtkByType[key] = (v.sum + (Game.I?.Time ?? 0) - t0, v.n + 1); }
    }

    private static string Line(Acc a)
    {
        double mins = Math.Max(0.05, a.Secs / 60.0);
        float life = Game.I?.Player != null ? Mathf.Max(1f, Game.I.Player.MaxLife) : 1f;
        var ttk = string.Join(" ", Enumerable.Range(0, 4).Select(i => $"{Tiers[i]}={(a.TtkN[i] > 0 ? (a.TtkSum[i] / a.TtkN[i]).ToString("0.0") : "-")}s(n={a.TtkN[i]})"));
        var slow = string.Join(",", a.TtkByType.Where(kv => kv.Value.n >= (kv.Key.EndsWith("*") ? 1 : 3)).OrderByDescending(kv => kv.Value.sum / kv.Value.n).Take(5).Select(kv => $"{kv.Key}:{kv.Value.sum / kv.Value.n:0.0}s/{kv.Value.n}"));
        var top = string.Join(",", a.ByType.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key}:{kv.Value:0}"));
        return $"ttk {ttk} | taken {a.Taken / mins:0}/min ({a.Taken / mins / life:0.00} life bars/min, {a.Hits} hits) kills={a.Kills} in {a.Secs:0}s | worst [{top}] slowest [{slow}]";
    }

    private static void Flush()
    {
        if (_cur == null || _area == null) return;
        _cur.Secs = (Game.I?.Time ?? 0) - _since;
        if (_cur.Kills == 0 && _cur.Hits == 0) return;
        string roster = Bestiary.Live ? "bestiary" : "old";
        Log.Info("roster", $"stats area={_area} roster={roster} hero=lv{Game.I?.Hero?.D.Level} {Line(_cur)}");
        string key = _area.Contains("_boss") ? "boss lairs" : _area;
        if (!Totals.TryGetValue(key, out var tot)) Totals[key] = tot = new Acc();
        tot.Add(_cur);
        var all = new Acc();
        foreach (var t in Totals.Values) all.Add(t);
        Log.Info("roster", $"totals roster={roster} {Line(all)}");
    }
}
