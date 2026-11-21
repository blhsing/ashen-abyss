using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// --suite=mapquest (MAP-QUEST, slot 90): quest pins on the area map and the minimap (QuestMarks.cs). Town NPC targets
/// at the start; a region's stronghold and elite-hunt targets before and after their spot is explored; a dungeon's
/// objective; live following and removal; the explored map kept across leaving, a save and a reload (and an old save
/// without explored data still loading). Windowed with --shot=&lt;base.png&gt; it photographs the HUD and the map.
/// </summary>
public sealed partial class TestSuite
{
    private const int MapQuestSlot = 90;

    private static List<QuestMark> QMarks() => QuestMarks.Of(G.Level).ToList();

    private IEnumerator MapShot(string name, bool map, bool hover = false)
    {
        if (map)
        {
            MapUi.TestHover = hover;
            Log.Guard("ui", () => G.Ui.Open("map"));
            yield return Until(() => G.Ui.IsOpen("map"), 5, "the map");
            yield return 0.8f;
        }
        G.Cam?.Snap();
        yield return null; yield return null;
        G.HarnessShot($"{ShotBase()}_{name}.png");
        yield return 0.3f;
        if (map) { Log.Guard("ui", () => G.Ui.CloseAll()); MapUi.TestHover = false; yield return 0.3f; }
    }

    private static void SetQuest(string id, int stage)
    {
        var d = G.Hero.D;
        d.Quests[id] = stage;
        Quests.Attach(G.Hero);          // re-reads the state (as a load does)
    }

    private IEnumerator MapQuestSuite()
    {
        string savedShot = G.ArgValue("shot");
        if (string.IsNullOrEmpty(savedShot)) Log.Info("suite", "mapquest: no --shot given; pictures go to Y:\\shots\\qa\\anim_*.png");

        // ── 1. town at the start: the elder (main, gold) and a side-quest giver (silver) are pinned ──
        _section = "mapquest: town";
        yield return StartHero(ClassId.Lampknight, 12, "town", gear: true, god: true, slot: MapQuestSlot);
        yield return 4.5f;
        SetQuest("side_wastes_cellar", 1);                     // "回殘燈鎮找莉雅"
        yield return 0.3f;
        var t = QMarks();
        Log.Info("suite", "mapquest town: " + QuestMarks.Describe(G.Level));
        Check(t.Any(m => m.Kind == QuestMarkKind.Npc && m.Main && m.Known && m.Name.Contains("艾爾溫")), "town: the elder is pinned as the main quest's NPC");
        Check(t.Any(m => m.Kind == QuestMarkKind.Npc && !m.Main && m.Known && m.Name.Contains("莉雅")), "town: the merchant is pinned as a side quest's NPC");
        Check(G.Level.Markers.All(m => !QuestMarks.Covers(m) || m.Kind == "objective"), "town: no ordinary marker is hidden by the quest layer");
        // a hero far from the elder still sees the elder's pin on the rim of the minimap
        var elder = t.FirstOrDefault(m => m.Kind == QuestMarkKind.Npc && m.Main);
        if (elder != null)
        {
            // the marked place of the town farthest from the elder (the gate, a far corner)
            var far = G.Level.Markers.Select(m => m.Pos).OrderByDescending(p => p.DistanceSquaredTo(elder.Pos)).First();
            far = G.Level.StandNear(far, elder.Pos, 3f);
            G.Player.Teleport(far);
            yield return 0.6f;
            Check(QMarks().Any(m => m.Main && m.Known && m.Kind == QuestMarkKind.Npc), "town: the elder stays pinned while the hero is far away", $"distance {far.DistanceTo(elder.Pos):0} m");
            yield return MapShot("town_hud_far", false);
            G.Player.Teleport(G.Level.StandNear(elder.Pos, elder.Pos + new Vector2(0, 6), 5f));
            yield return 0.6f;
        }
        yield return MapShot("town_hud", false);
        yield return MapShot("town_map", true, hover: true);

        // ── 2. a region: the stronghold to take (main) and elites to hunt (side), before and after exploring ──
        _section = "mapquest: region";
        SetQuest("main_wastes", 2);                             // "奪回斷燈哨站…"
        SetQuest("side_wastes_cellar", 0);                     // "肅清荒棄地窖" (an entrance to find)
        SetQuest("side_wastes_elites", 0);                     // "在灰燼荒原擊殺菁英怪物"
        G.EnterArea("wastes");
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level.Built && G.Level.AreaId == "wastes" && G.Player != null && G.Player.IsInsideTree(), 120, "the wastes");
        yield return 4.5f;
        var lv = G.Level;
        Log.Info("suite", "mapquest wastes (arrival): " + QuestMarks.Describe(lv));
        var hold = QMarks().FirstOrDefault(m => m.Kind == QuestMarkKind.Kill && m.Main);
        Check(hold != null, "region: the stronghold is a main-quest kill target", hold == null ? "none" : $"at ({hold.Pos.X:0},{hold.Pos.Y:0})");
        bool holdFoggy = hold != null && !lv.SeenAt(lv.StrongholdPos);
        Check(hold == null || hold.Known == lv.SeenAt(hold.Pos), "region: a target is shown exactly when its spot is explored", $"known={hold?.Known} seen={holdFoggy == false}");
        Check(QMarks().Where(m => !m.Known).All(m => !lv.SeenAt(m.Pos)) && QMarks().Where(m => m.Known).All(m => lv.SeenAt(m.Pos)), "region: no pin stands in unexplored fog");
        yield return MapShot("region_before_map", true);
        yield return MapShot("region_before_hud", false);

        // explore the stronghold: walk the hero there (god mode), then back to the start
        Vector2 start = G.Player.Pos2;
        if (hold != null)
        {
            G.Player.Teleport(lv.StandNear(lv.StrongholdPos, start, lv.StrongholdRadius + 6f));
            yield return 1.2f;
            Check(QMarks().Any(m => m.Kind == QuestMarkKind.Kill && m.Main && m.Known), "region: after exploring, the stronghold is pinned");
            int elites = QMarks().Count(m => m.Kind == QuestMarkKind.Kill && !m.Main && m.Known);
            Log.Info("suite", "mapquest wastes (at the stronghold): " + QuestMarks.Describe(lv));
            yield return MapShot("region_explored_near_hud", false);
            G.Player.Teleport(start);
            yield return 1.0f;
            Check(QMarks().Any(m => m.Kind == QuestMarkKind.Kill && m.Main && m.Known), "region: the stronghold stays pinned far away", $"distance {start.DistanceTo(lv.StrongholdPos):0} m, elites seen {elites}");
            yield return MapShot("region_after_hud_edge", false);
            yield return MapShot("region_after_map", true, hover: true);
        }

        // live: an elite hunt target follows its monster and disappears when it dies
        _section = "mapquest: live";
        var elite = World.Monsters.FirstOrDefault(m => GodotObject.IsInstanceValid(m) && !m.Dead && m.IsElite && !m.IsBoss);
        if (elite != null)
        {
            var spot = G.Player.Pos2 + new Vector2(6f, 0f);
            spot = lv.Grid.NearestWalk(spot);
            elite.Pos2 = spot;
            yield return 0.5f;
            var em = QMarks().FirstOrDefault(m => ReferenceEquals(m.Ref, elite));
            Check(em != null && em.Known && em.Pos.DistanceTo(elite.Pos2) < 1.5f, "live: the elite's pin follows it", em == null ? "no pin" : $"pin ({em.Pos.X:0.0},{em.Pos.Y:0.0}) monster ({elite.Pos2.X:0.0},{elite.Pos2.Y:0.0})");
            yield return MapShot("live_elite_hud", false);
            elite.TakeHit(new Hit { Source = G.Player, Amount = elite.MaxLife * 100f, El = Element.Physical });
            yield return 0.5f;
            Check(!QMarks().Any(m => ReferenceEquals(m.Ref, elite)), "live: the pin is gone when the elite dies");
        }
        else Fail("live: no elite in the wastes to follow");

        // the stronghold falls: its target goes
        foreach (var m in lv.StrongholdMobs.ToList()) if (GodotObject.IsInstanceValid(m) && !m.Dead) m.TakeHit(new Hit { Source = G.Player, Amount = m.MaxLife * 100f, El = Element.Physical });
        yield return Until(() => lv.Liberated, 10, "the stronghold liberated");
        yield return 0.6f;
        Check(!QMarks().Any(m => m.Kind == QuestMarkKind.Kill && m.Main && m.Name == lv.Region.Stronghold), "live: the stronghold pin is gone once it is liberated", QuestMarks.Describe(lv));

        // ── 3. save, reload: the explored part of the region (and its pins) is kept ──
        _section = "mapquest: save";
        int seenBefore = lv.Seen.Count(b => b);
        var knownBefore = QMarks().Where(m => m.Known && m.Kind == QuestMarkKind.Area).Select(m => m.Name).OrderBy(n => n).ToList();
        G.SaveNow("mapquest");
        var disk = SaveSystem.Load(MapQuestSlot);
        Check(disk?.Fog != null && disk.Fog.ContainsKey("wastes"), "save: the explored map of the region is written", $"areas={string.Join(",", disk?.Fog?.Keys ?? Enumerable.Empty<string>())}");
        G.LoadGame(MapQuestSlot);
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level.Built && G.Player != null && G.Player.IsInsideTree() && !ReferenceEquals(G.Level, lv), 120, "the reloaded save");
        yield return 2.0f;
        lv = G.Level;
        int seenAfter = lv.Seen.Count(b => b);
        Check(lv.AreaId == "wastes" && seenAfter >= seenBefore, "reload: the explored map came back", $"area={lv.AreaId} explored cells {seenAfter} (saved {seenBefore})");
        Log.Info("suite", "mapquest wastes (reloaded): " + QuestMarks.Describe(lv));
        var knownAfter = QMarks().Where(m => m.Known && m.Kind == QuestMarkKind.Area).Select(m => m.Name).OrderBy(n => n).ToList();
        Check(knownBefore.All(knownAfter.Contains), "reload: the pinned places of the explored part are still pinned", $"before [{string.Join(",", knownBefore)}] after [{string.Join(",", knownAfter)}]");
        yield return MapShot("reload_map", true);

        // leave to town and come back: kept within the session too
        G.EnterArea(ZoneDb.Town);
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level.Built && G.Level.AreaId == ZoneDb.Town, 120, "town");
        yield return 1.0f;
        G.EnterArea("wastes");
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level.Built && G.Level.AreaId == "wastes", 120, "the wastes again");
        yield return 1.5f;
        Check(G.Level.Seen.Count(b => b) >= seenBefore, "re-entry: the explored map is kept", $"{G.Level.Seen.Count(b => b)} cells");

        // an old save without explored data still loads
        var old = SaveSystem.Load(MapQuestSlot);
        if (old != null) { old.Fog = null; SaveSystem.Save(MapQuestSlot, old); }
        var back = SaveSystem.Load(MapQuestSlot);
        Check(back != null && back.Fog != null && back.Fog.Count == 0, "old save: a save without explored data loads (empty map)");

        // ── 4. a dungeon objective ──
        _section = "mapquest: dungeon";
        G.EnterArea("wastes_d1");
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level.Built && G.Level.AreaId == "wastes_d1" && G.Player != null && G.Player.IsInsideTree(), 120, "the cellar");
        yield return 4.5f;
        lv = G.Level;
        var dq = QMarks();
        Log.Info("suite", $"mapquest wastes_d1 goal={lv.Goal}: " + QuestMarks.Describe(lv));
        var goals = dq.Where(m => m.Kind != QuestMarkKind.Area).ToList();
        Check(lv.Goal == DungeonGoal.None || goals.Count > 0, "dungeon: the objective's targets are known to the quest layer", $"{goals.Count} targets");
        Check(goals.All(m => m.Known == lv.SeenAt(m.Pos)), "dungeon: only explored objective targets are shown");
        Check(goals.Count == 0 || goals.All(m => m.Main == false), "dungeon: a side-quest dungeon's targets are silver");
        yield return MapShot("dungeon_before_map", true);
        var tgt = goals.OrderBy(m => m.Pos.DistanceSquaredTo(G.Player.Pos2)).FirstOrDefault();
        if (tgt != null)
        {
            G.Player.Teleport(lv.Grid.NearestWalk(lv.StandNear(tgt.Pos, G.Player.Pos2, 4f)));
            yield return 1.2f;
            Check(QMarks().Any(m => m.Known && m.Kind != QuestMarkKind.Area), "dungeon: an objective target is pinned once its room is explored");
            yield return MapShot("dungeon_after_hud", false);
            yield return MapShot("dungeon_after_map", true, hover: true);
        }
        Check(lv.Markers.Where(m => m.Kind == "objective").All(QuestMarks.Covers), "dungeon: the old objective markers are left to the quest pins");
    }
}
