using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>What a quest target asks of the hero; picks the symbol inside the quest pin.</summary>
public enum QuestMarkKind { Npc, Kill, Object, Area, Item }

/// <summary>One quest or mission target in the current level, at its live position.</summary>
public sealed class QuestMark
{
    public QuestMarkKind Kind;
    public bool Main;               // gold (main quest) or silver-blue (side quests, the level's own objective, named hunts)
    public bool Known;              // the fog over its current position has lifted: only then is it shown at its spot
    public Vector2 Pos;
    public string Name = "";        // what the target is ("鐵匠 布蘭", "寂滅圖騰"…)
    public string Text = "";        // the objective it belongs to ("與守燈長老艾爾溫交談"…)
    public object Ref;              // the monster, object, NPC or map marker it follows
    public MapMarker Marker;        // the ordinary map marker at the same thing, if any (drawn under the pin, or left out)
}

/// <summary>
/// Quest and mission targets of the current level, for the area map and the minimap. Rules:
/// · every current step of every active quest (main and side) is resolved to targets in this level: NPCs to talk to,
///   things to destroy, free or light, monsters and bosses to kill (story bosses, marked elites, ember carriers, named
///   hunts), ember shards to pick up, and the exits or dungeon entrances that lead to the area a step wants;
/// · a dungeon's own objective (and its boss) always counts, as a side target when no quest asks for it;
/// · a target is only shown where the fog over its CURRENT position has lifted (Known); moving targets are followed;
///   targets that are done (dead, used, picked up, quest step finished) drop out on the next update.
/// Unexplored targets are not placed; the gold "way of the main quest" rings of the map stay as they were.
/// </summary>
public static class QuestMarks
{
    public static readonly Color MainCol = new(1f, 0.8f, 0.3f);
    public static readonly Color SideCol = new(0.7f, 0.84f, 1f);

    private static readonly List<QuestMark> _list = new();
    private static readonly List<QuestMark> _pool = new();
    private static readonly HashSet<object> _taken = new();
    private static Level _lv;
    private static ulong _at;
    private static int _fails;

    public static Color ColorOf(QuestMark m) => m.Main ? MainCol : SideCol;

    public static string KindName(QuestMarkKind k) => k switch
    {
        QuestMarkKind.Npc => "任務：交談對象",
        QuestMarkKind.Kill => "任務：討伐目標",
        QuestMarkKind.Object => "任務：互動目標",
        QuestMarkKind.Area => "任務：前往之處",
        _ => "任務：拾取之物",
    };

    /// <summary>The targets of the level, refreshed at most every 0.15 s (positions follow moving targets).</summary>
    public static IReadOnlyList<QuestMark> Of(Level lv)
    {
        if (lv == null || !GodotObject.IsInstanceValid(lv)) { Recycle(); _lv = null; return _list; }
        ulong now = Time.GetTicksMsec();
        if (ReferenceEquals(lv, _lv) && now - _at < 150UL) return _list;
        _lv = lv; _at = now;
        Recycle();
        try { Collect(lv); }
        catch (Exception e)
        {
            Recycle();
            if (_fails++ < 3) Log.Error("questmarks", "collecting quest targets failed", e);
        }
        return _list;
    }

    /// <summary>
    /// True for ordinary map markers that the quest layer replaces: the dungeon objectives, which used to be drawn at
    /// their exact spot from the start, exist only as quest pins now (and so only where the fog has lifted).
    /// </summary>
    public static bool Covers(MapMarker m) => m != null && m.Kind == "objective";

    /// <summary>Number of targets of a quest step present (shown or not) in the level; for logs and tests.</summary>
    public static string Describe(Level lv)
    {
        var l = Of(lv);
        int known = 0;
        var sb = new System.Text.StringBuilder();
        foreach (var m in l)
        {
            if (m.Known) known++;
            if (sb.Length < 600) sb.Append($" [{m.Kind}{(m.Main ? "/main" : "/side")}{(m.Known ? "" : "/fog")} {m.Name} ({m.Pos.X:0},{m.Pos.Y:0})]");
        }
        return $"targets={l.Count} shown={known}{sb}";
    }

    private static void Recycle()
    {
        foreach (var m in _list) { m.Ref = null; m.Marker = null; _pool.Add(m); }
        _list.Clear();
        _taken.Clear();
    }

    private static void Add(Level lv, QuestMarkKind kind, bool main, Vector2 pos, string name, string text, object reference, MapMarker marker = null)
    {
        if (reference != null && !_taken.Add(reference))
        {
            // the same thing asked for twice (a main step and the level's own objective): the main quest wins the colour
            if (main) foreach (var o in _list) if (ReferenceEquals(o.Ref, reference)) { o.Main = true; o.Text = text; }
            return;
        }
        QuestMark m;
        if (_pool.Count > 0) { m = _pool[^1]; _pool.RemoveAt(_pool.Count - 1); } else m = new QuestMark();
        m.Kind = kind; m.Main = main; m.Pos = pos; m.Name = name ?? ""; m.Text = text ?? ""; m.Ref = reference;
        m.Marker = marker ?? (reference != null ? lv.MarkerOf(reference) : null);
        m.Known = lv.SeenAt(pos);
        _list.Add(m);
    }

    private static bool Alive(Monster m) => m != null && GodotObject.IsInstanceValid(m) && !m.Dead && m.IsInsideTree();
    private static bool Usable(Interactable it) => it != null && GodotObject.IsInstanceValid(it) && it.CanUse && it.IsInsideTree();

    private static string NpcKey(string id) => id != null && id.StartsWith("npc_", StringComparison.Ordinal) ? id.Substring(4) : id;

    private static void Collect(Level lv)
    {
        bool dungeonAsked = false;
        foreach (var q in Quests.Active())
        {
            int st = Quests.StepIndex(q);
            if (st < 0 || st >= q.Steps.Count) continue;
            var s = q.Steps[st];
            string text = Quests.StepText(q, st);
            bool main = q.Main;
            switch (s.Event)
            {
                case "talk":
                    foreach (var n in lv.Npcs)
                        if (n?.Obj != null && GodotObject.IsInstanceValid(n.Obj) && NpcKey(n.Id) == s.Arg)
                            Add(lv, QuestMarkKind.Npc, main, n.Obj.Pos2, QuestDb.NpcName(s.Arg), text, n.Obj);
                    if (!lv.IsTown) Ways(lv, ZoneDb.Town, main, text);
                    break;
                case "area":
                    if (lv.AreaId != s.Arg) Ways(lv, s.Arg, main, text);
                    break;
                case "stronghold":
                    if (lv.Region?.Id == s.Arg && lv.Dungeon == null && !lv.IsRift)
                    {
                        if (!lv.Liberated)
                        {
                            Add(lv, QuestMarkKind.Kill, main, lv.StrongholdPos, lv.Region.Stronghold, text, lv.StrongholdMarker, lv.StrongholdMarker);                        }
                    }
                    else Ways(lv, s.Area ?? s.Arg, main, text);
                    break;
                case "dungeon":
                    if (lv.Dungeon?.Id == s.Arg) { dungeonAsked = true; DungeonGoal(lv, main, text); }
                    else Ways(lv, s.Arg, main, text);
                    break;
                case "boss":
                    if (Alive(lv.BossMon) && (lv.BossMon.Def?.Id == s.Arg || lv.AreaId == s.Area))
                        Add(lv, QuestMarkKind.Kill, main, lv.BossMon.Pos2, BossName(lv.BossMon), text, lv.BossMon);
                    else if (lv.AreaId != s.Area) Ways(lv, s.Area, main, text);
                    break;
                case "elite":
                    if (InRegion(lv, s.Region)) Elites(lv, main, text);
                    else if (s.Region != null) Ways(lv, s.Region, main, text);
                    break;
                case "altar":
                    if (InRegion(lv, s.Region) && lv.Dungeon == null)
                    {
                        foreach (var it in Interactable.All)
                            if (it.Kind == "altar" && Usable(it)) Add(lv, QuestMarkKind.Object, main, it.Pos2, it.Label, text, it);
                    }
                    else if (s.Region != null) Ways(lv, s.Region, main, text);
                    break;
                case "event":
                    if (InRegion(lv, s.Region) && lv.Dungeon == null)
                    {
                        foreach (var e in lv.Events)
                            if (e != null && !e.Done) Add(lv, QuestMarkKind.Object, main, e.Pos, EventName(lv, e), text, e, EventMarker(lv, e));
                    }
                    else if (s.Region != null) Ways(lv, s.Region, main, text);
                    break;
            }
        }

        // the level's own mission: a dungeon objective nobody asked for, and every boss or named hunt still standing
        if (lv.Dungeon != null && !dungeonAsked) DungeonGoal(lv, false, string.IsNullOrEmpty(lv.ObjectiveText) ? "地城目標" : lv.ObjectiveText);
        var marks = lv.Markers;
        for (int i = 0; i < marks.Count; i++)
        {
            var mk = marks[i];
            if (mk?.Kind != "boss" || mk.Ref is not Monster mon || !Alive(mon)) continue;
            if (_taken.Contains(mon)) continue;
            bool named = NamedHub.Of(mon) != null;
            if (!named && !ReferenceEquals(mon, lv.BossMon)) continue;
            if (ReferenceEquals(mon, lv.BossMon) && lv.Dungeon != null && !lv.ObjectiveDone) continue;    // behind the sealed door: the goal comes first
            Add(lv, QuestMarkKind.Kill, false, mon.Pos2, BossName(mon), named && !ReferenceEquals(mon, lv.BossMon) ? $"討伐：{BossName(mon)}" : $"擊敗{BossName(mon)}", mon, mk);
        }
    }

    private static bool InRegion(Level lv, string region) =>
        region != null && (lv.Region?.Id == region || ZoneDb.RegionOf(lv.AreaId ?? "")?.Id == region) && !lv.IsRift;

    private static string BossName(Monster m)
    {
        var n = NamedHub.Of(m);
        if (n?.Def != null && !string.IsNullOrEmpty(n.Def.NameZh)) return n.Def.NameZh;
        return !string.IsNullOrEmpty(m.Title) && m.Brain == null ? m.Title : m.DisplayName;
    }

    private static MapMarker EventMarker(Level lv, WorldEvent e)
    {
        foreach (var m in lv.Markers) if (m.Kind == "event" && (ReferenceEquals(m.Ref, e) || m.Pos.DistanceSquaredTo(e.Pos) < 1f)) return m;
        return null;
    }

    private static string EventName(Level lv, WorldEvent e)
    {
        var m = EventMarker(lv, e);
        return m != null && !string.IsNullOrEmpty(m.Label) ? m.Label : "區域事件";
    }

    /// <summary>Elites of the region: the nearest ten that are alive.</summary>
    private static void Elites(Level lv, bool main, string text)
    {
        var p = World.P;
        Vector2 hero = p != null && GodotObject.IsInstanceValid(p) ? p.Pos2 : lv.Checkpoint;
        var found = new List<Monster>();
        foreach (var m in World.Monsters)
            if (Alive(m) && m.IsElite && !m.IsBoss && m.Faction == Faction.Monster && lv.SeenAt(m.Pos2)) found.Add(m);
        found.Sort((a, b) => a.Pos2.DistanceSquaredTo(hero).CompareTo(b.Pos2.DistanceSquaredTo(hero)));
        for (int i = 0; i < found.Count && i < 10; i++) Add(lv, QuestMarkKind.Kill, main, found[i].Pos2, found[i].DisplayName, text, found[i]);
    }

    /// <summary>The objective of the dungeon the hero is in; once it is met, its boss.</summary>
    private static void DungeonGoal(Level lv, bool main, string text)
    {
        if (!lv.ObjectiveDone)
            switch (lv.Goal)
            {
                case AshenAbyss.DungeonGoal.SlayElites:
                    foreach (var m in lv.ObjMonsters) if (Alive(m)) Add(lv, QuestMarkKind.Kill, main, m.Pos2, "被標記的菁英", text, m);
                    break;
                case AshenAbyss.DungeonGoal.DestroyIdols:
                case AshenAbyss.DungeonGoal.FreePrisoners:
                    foreach (var mk in lv.Markers)
                        if (mk.Kind == "objective" && mk.Ref is Interactable it && Usable(it))
                            Add(lv, QuestMarkKind.Object, main, it.Pos2, it.Label, text, it, mk);
                    break;
                case AshenAbyss.DungeonGoal.CollectEmbers:
                    if (lv.Embers < lv.EmbersNeeded)
                    {
                        foreach (var it in Interactable.All)
                            if (it.Kind == "ember" && Usable(it)) Add(lv, QuestMarkKind.Item, main, it.Pos2, "燼晶", text, it);
                        foreach (var m in World.Monsters)
                            if (Alive(m) && m.Objective) Add(lv, QuestMarkKind.Kill, main, m.Pos2, $"{m.DisplayName}（攜燼晶）", text, m);
                    }
                    else
                        foreach (var mk in lv.Markers)
                            if (mk.Kind == "objective" && mk.Ref is Interactable it && Usable(it))
                                Add(lv, QuestMarkKind.Object, main, it.Pos2, it.Label, text, it, mk);
                    break;
            }
        else if (Alive(lv.BossMon))
            Add(lv, QuestMarkKind.Kill, main, lv.BossMon.Pos2, BossName(lv.BossMon), text, lv.BossMon);
    }

    /// <summary>Exits, gates and dungeon entrances of this level that lead to <paramref name="area"/> (or its region).</summary>
    private static void Ways(Level lv, string area, bool main, string text)
    {
        if (string.IsNullOrEmpty(area) || area == lv.AreaId) return;
        string region = area == ZoneDb.Town ? null : ZoneDb.RegionOf(area)?.Id;
        string here = lv.IsTown ? ZoneDb.Town : lv.Region?.Id;
        if (region != null && region == here && lv.Dungeon != null) region = null;       // from a dungeon to another place of the same region: out to the overworld
        foreach (var m in lv.Markers)
        {
            if (m == null || (m.Kind != "exit" && m.Kind != "entrance")) continue;
            object to = m.Ref is Interactable it && GodotObject.IsInstanceValid(it) ? it.Tag : m.Ref;
            string dest = to switch { string s => s, DungeonDef d => d.Id, _ => null };
            bool leads;
            if (lv.IsTown) leads = area != ZoneDb.Town && (dest == null || dest == area || dest == region);     // the town gate opens the road to every region
            else if (dest == null) leads = false;
            else if (dest == area) leads = true;
            else if (lv.Dungeon != null) leads = dest == lv.Region?.Id || dest == ZoneDb.Town && area == ZoneDb.Town;   // out of the dungeon
            else leads = region != null && dest == region;
            if (!leads) continue;
            Add(lv, QuestMarkKind.Area, main, m.Pos, string.IsNullOrEmpty(m.Label) ? ZoneDb.AreaName(area) : m.Label, text, m, m);
        }
    }
}

/// <summary>
/// The quest pin, drawn from primitives like the other map glyphs: a map pin whose head holds the symbol of what the
/// target asks for (a speech bubble, crossed blades, a hand-rune, a banner, a gem). Gold for the main quest,
/// silver-blue for the rest. The tip of the pin points at the target.
/// </summary>
internal static class QuestGlyph
{
    private static readonly Vector2[] P3 = new Vector2[3], P4 = new Vector2[4], P6 = new Vector2[6];

    /// <summary>Head centre of a pin whose tip is at <paramref name="tip"/>, for a head of radius <paramref name="r"/>.</summary>
    public static Vector2 Head(Vector2 tip, float r) => tip + new Vector2(0f, -r * 1.55f);

    /// <summary>Width of the dark outline round a pin of head radius <paramref name="r"/>.</summary>
    private static float Rim(float r) => MathF.Max(2f, r * 0.18f);

    /// <summary>A soft glow of the pin's colour under it, so it reads on fog and on explored ground alike.</summary>
    private static void Glow(CanvasItem ci, Vector2 c, float r, Color col, float alpha)
    {
        ci.DrawCircle(c, r * 1.75f, new Color(col, 0.07f * alpha));
        ci.DrawCircle(c, r * 1.45f, new Color(col, 0.11f * alpha));
        ci.DrawCircle(c, r * 1.22f, new Color(col, 0.16f * alpha));
    }

    public static void Pin(CanvasItem ci, QuestMarkKind kind, Color col, Vector2 tip, float r, float alpha = 1f)
    {
        var c = Head(tip, r);
        float o = Rim(r);
        var shade = new Color(0.02f, 0.015f, 0.012f, 0.92f * alpha);
        var fill = new Color(col, alpha);
        var dark = new Color(0.07f, 0.05f, 0.035f, alpha);
        Glow(ci, c, r, col, alpha);
        // outline (shade), then the gold or silver body, then the dark socket the symbol sits in
        P3[0] = tip + new Vector2(0f, o * 1.2f); P3[1] = c + new Vector2(-r * 0.92f - o, r * 0.42f); P3[2] = c + new Vector2(r * 0.92f + o, r * 0.42f);
        ci.DrawColoredPolygon(P3, shade);
        ci.DrawCircle(c, r + o, shade);
        P3[0] = tip; P3[1] = c + new Vector2(-r * 0.86f, r * 0.45f); P3[2] = c + new Vector2(r * 0.86f, r * 0.45f);
        ci.DrawColoredPolygon(P3, fill);
        ci.DrawCircle(c, r, fill);
        ci.DrawCircle(c, r * 0.74f, dark);
        Symbol(ci, kind, c, r * 0.6f, fill, dark);
        ci.DrawArc(c, r, -2.6f, -0.6f, 12, new Color(1f, 1f, 1f, 0.35f * alpha), MathF.Max(1f, r * 0.12f), true);   // a glint on the rim
    }

    /// <summary>The symbol alone, for the legend and the hover ring.</summary>
    public static void Symbol(CanvasItem ci, QuestMarkKind kind, Vector2 c, float s, Color col, Color dark)
    {
        float w = MathF.Max(1.4f, s * 0.26f);
        switch (kind)
        {
            case QuestMarkKind.Npc:                 // a speech bubble with three dots
                ci.DrawCircle(c + new Vector2(0f, -s * 0.12f), s * 0.78f, col);
                P3[0] = c + new Vector2(-s * 0.45f, s * 0.4f); P3[1] = c + new Vector2(-s * 0.05f, s * 0.5f); P3[2] = c + new Vector2(-s * 0.7f, s * 0.95f);
                ci.DrawColoredPolygon(P3, col);
                for (int i = -1; i <= 1; i++) ci.DrawCircle(c + new Vector2(i * s * 0.34f, -s * 0.12f), MathF.Max(1f, s * 0.12f), dark);
                break;
            case QuestMarkKind.Kill:                // two crossed blades with guards
                ci.DrawLine(c + new Vector2(-s * 0.85f, -s * 0.85f), c + new Vector2(s * 0.6f, s * 0.6f), col, w, true);
                ci.DrawLine(c + new Vector2(s * 0.85f, -s * 0.85f), c + new Vector2(-s * 0.6f, s * 0.6f), col, w, true);
                ci.DrawLine(c + new Vector2(s * 0.25f, s * 0.75f), c + new Vector2(s * 0.75f, s * 0.25f), col, w, true);
                ci.DrawLine(c + new Vector2(-s * 0.25f, s * 0.75f), c + new Vector2(-s * 0.75f, s * 0.25f), col, w, true);
                ci.DrawCircle(c + new Vector2(s * 0.82f, s * 0.82f), w * 0.7f, col);
                ci.DrawCircle(c + new Vector2(-s * 0.82f, s * 0.82f), w * 0.7f, col);
                break;
            case QuestMarkKind.Object:              // a rune-stone with a spark: something to touch, free, light or break
                P4[0] = c + new Vector2(0f, -s); P4[1] = c + new Vector2(s * 0.75f, 0f); P4[2] = c + new Vector2(0f, s); P4[3] = c + new Vector2(-s * 0.75f, 0f);
                ci.DrawColoredPolygon(P4, col);
                P4[0] = c + new Vector2(0f, -s * 0.5f); P4[1] = c + new Vector2(s * 0.36f, 0f); P4[2] = c + new Vector2(0f, s * 0.5f); P4[3] = c + new Vector2(-s * 0.36f, 0f);
                ci.DrawColoredPolygon(P4, dark);
                ci.DrawCircle(c, MathF.Max(1f, s * 0.16f), col);
                break;
            case QuestMarkKind.Area:                // a banner on a pole
                ci.DrawLine(c + new Vector2(-s * 0.55f, -s * 0.9f), c + new Vector2(-s * 0.55f, s * 0.95f), col, w, true);
                P4[0] = c + new Vector2(-s * 0.5f, -s * 0.9f); P4[1] = c + new Vector2(s * 0.85f, -s * 0.62f);
                P4[2] = c + new Vector2(s * 0.45f, -s * 0.25f); P4[3] = c + new Vector2(-s * 0.5f, -s * 0.05f);
                ci.DrawColoredPolygon(P4, col);
                break;
            default:                                // a cut gem: something to pick up
                for (int i = 0; i < 6; i++)
                {
                    float a = Mathf.Pi / 6f + i * Mathf.Tau / 6f;
                    P6[i] = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * s * 0.85f;
                }
                ci.DrawColoredPolygon(P6, col);
                ci.DrawLine(P6[4], P6[1], dark, MathF.Max(1f, s * 0.12f), true);
                break;
        }
    }

    /// <summary>How far an edge pin reaches from its centre toward its target (the arrow and its outline).</summary>
    public static float EdgeReach(float r) => r + MathF.Max(6f, r * 0.55f) + Rim(r) * 1.5f;

    /// <summary>
    /// A target off the edge of the view: a pin head with an arrow pointing where it lies. <paramref name="pulse"/> (0-1,
    /// negative for none) rings it gently: the main quest. <paramref name="count"/> &gt; 1 puts a "×n" badge on it when
    /// several targets in that direction were folded into this one.
    /// </summary>
    public static void Edge(CanvasItem ci, QuestMarkKind kind, Color col, Vector2 at, Vector2 dir, float r, float alpha = 0.95f, float pulse = -1f, int count = 1)
    {
        dir = dir.LengthSquared() < 1e-6f ? new Vector2(0, -1) : dir.Normalized();
        var side = new Vector2(-dir.Y, dir.X);
        var fill = new Color(col, alpha);
        float o = Rim(r);
        var shade = new Color(0.02f, 0.015f, 0.012f, 0.92f * alpha);
        if (pulse >= 0f) ci.DrawArc(at, r * (1.25f + 0.45f * pulse), 0f, Mathf.Tau, 28, new Color(col, 0.7f * (1f - pulse)), MathF.Max(1.5f, r * 0.16f), true);
        Glow(ci, at, r, col, alpha);
        Vector2 tip = at + dir * (r + MathF.Max(6f, r * 0.55f));
        P3[0] = tip + dir * o * 1.3f; P3[1] = at + dir * (r * 0.8f) + side * (r * 0.7f + o); P3[2] = at + dir * (r * 0.8f) - side * (r * 0.7f + o);
        ci.DrawColoredPolygon(P3, shade);
        P3[0] = tip; P3[1] = at + dir * (r * 0.8f) + side * r * 0.6f; P3[2] = at + dir * (r * 0.8f) - side * r * 0.6f;
        ci.DrawColoredPolygon(P3, fill);
        ci.DrawCircle(at, r + o, shade);
        ci.DrawCircle(at, r, fill);
        var dark = new Color(0.07f, 0.05f, 0.035f, alpha);
        ci.DrawCircle(at, r * 0.74f, dark);
        Symbol(ci, kind, at, r * 0.58f, fill, dark);
        if (count > 1) Badge(ci, at - dir * (r + o + 14f), count, col);         // on the inner side, clear of the pin
    }

    /// <summary>A "×n" count on a dark plate (readable size: the 20 px caption floor).</summary>
    public static void Badge(CanvasItem ci, Vector2 centre, int n, Color col)
    {
        var font = WorldUiKit.SmallFont;
        string s = "×" + n;
        int fs = UiTheme.FsCaption;
        var size = WorldUiKit.Measure(font, s, fs);
        float lh = WorldUiKit.LineHeight(font, fs);
        float w = MathF.Max(size.X + 10f, lh * 0.9f), h = lh * 0.82f;
        var box = new Rect2(MathF.Round(centre.X - w * 0.5f), MathF.Round(centre.Y - h * 0.5f), MathF.Round(w), MathF.Round(h));
        ci.DrawRect(box.Grow(1.5f), new Color(0.02f, 0.015f, 0.012f, 0.95f));
        ci.DrawRect(box, new Color(0.09f, 0.07f, 0.05f, 0.95f));
        ci.DrawRect(box, new Color(col, 0.85f), false, 1.2f);
        float baseY = box.Position.Y + (h - lh) * 0.5f + WorldUiKit.Ascent(font, fs);
        WorldUiKit.Text(ci, font, new Vector2(box.Position.X, MathF.Round(baseY)), s, fs, col, HorizontalAlignment.Center, box.Size.X, 3);
    }
}
