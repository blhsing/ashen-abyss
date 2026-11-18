using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AshenAbyss;

/// <summary>
/// Registry of live named encounters and the glue to the rest of the game: it boots itself (module initializer) onto
/// <see cref="World.Event"/> / <see cref="World.Damage"/> / <see cref="World.MonsterKilled"/>, spawns named monsters
/// from their definitions, routes hits and deaths to their encounters, pays out their loot and keeps the kill records.
/// Where an encounter needs to look at the hero's shots or ground effects it reads them here (reflection over the
/// private fields of <see cref="Projectile"/> / <see cref="GroundZone"/>, so their files stay untouched).
/// </summary>
public static class NamedHub
{
    private static readonly Dictionary<Monster, NamedEncounter> Owner = new();      // named monsters, their adds and props → encounter
    public static readonly List<NamedEncounter> Live = new();
    private static bool _booted;

    // ───────────────────────── boot ─────────────────────────

#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Boot()
#pragma warning restore CA2255
    {
        if (_booted) return;
        _booted = true;
        World.Event += OnEvent;
        World.Damage += OnDamage;
        World.MonsterKilled += OnKilled;
    }

    private static void OnEvent(string evt, string arg)
    {
        if (evt != "area") return;
        try { NamedDb.Ensure(); NamedAreas.OnArea(Level.Current, arg); }
        catch (Exception e) { Log.Error("named", $"area hook for '{arg}' failed", e); }
    }

    private static void OnDamage(Actor a, Hit h, float amount)
    {
        if (Owner.Count == 0 || amount < 0f) return;
        try
        {
            if (a is Monster m && Owner.TryGetValue(m, out var e) && e != null && ReferenceEquals(e.M, m)) e.OnDamaged(h, amount);
            else if (a is Monster prop && Owner.TryGetValue(prop, out var pe) && pe != null) pe.OnPropDamaged(prop, h, amount);
            else if (a is Player && h.Source is Monster src && Owner.TryGetValue(src, out var he) && he != null) he.OnHeroHit(h, amount);
        }
        catch (Exception ex) { Log.Error("named", "damage hook failed", ex); }
    }

    private static void OnKilled(Monster m, Hit h)
    {
        if (m == null || Live.Count == 0) return;
        try
        {
            if (Owner.TryGetValue(m, out var e) && e != null && GodotObject.IsInstanceValid(e) && ReferenceEquals(e.M, m)) { e.OnSlain(h); return; }
            foreach (var x in Live.ToArray()) if (x != null && GodotObject.IsInstanceValid(x)) x.OnKilled(m);
        }
        catch (Exception ex) { Log.Error("named", "kill hook failed", ex); }
    }

    /// <summary>Called by every new BossBrain: a named encounter re-attaches its script to a fresh brain. No-op for region bosses.</summary>
    internal static void BrainCreated(Monster m, BossBrain brain)
    {
        if (m == null || Owner.Count == 0) return;
        if (Owner.TryGetValue(m, out var e) && e != null && GodotObject.IsInstanceValid(e) && ReferenceEquals(e.M, m)) e.AttachBrain(brain);
    }

    internal static void Link(Monster m, NamedEncounter e) { if (m != null && e != null) Owner[m] = e; }
    internal static void Unlink(Monster m) { if (m != null) Owner.Remove(m); }
    public static NamedEncounter Of(Monster m) => m != null && Owner.TryGetValue(m, out var e) ? e : null;
    public static NamedEncounter Find(string id)
    {
        for (int i = Live.Count - 1; i >= 0; i--) { var e = Live[i]; if (e != null && GodotObject.IsInstanceValid(e) && e.Def?.Id == id) return e; }
        return null;
    }

    internal static void Forget(NamedEncounter e)
    {
        if (e == null) return;
        Live.Remove(e);
        if (Owner.Count == 0) return;
        var gone = new List<Monster>();
        foreach (var kv in Owner) if (ReferenceEquals(kv.Value, e)) gone.Add(kv.Key);
        foreach (var m in gone) Owner.Remove(m);
    }

    // ───────────────────────── spawning ─────────────────────────

    /// <summary>The level a named monster of this kind is met at in an area of the given monster level.</summary>
    public static int LevelFor(NamedDef n, int areaLevel) => Math.Clamp(areaLevel + n.LevelBonus, 1, Balance.LevelCap + 8);

    /// <summary>
    /// Spawns a named monster and its encounter. <paramref name="levelBoss"/>: the level registers it as its boss (dungeon,
    /// arena) before the encounter starts, so the level runs the arena, the music and the reset. Returns the encounter.
    /// </summary>
    public static NamedEncounter Spawn(NamedDef n, Level L, Vector2 at, int level, Vector2 centre, float arenaR, bool levelBoss = false, bool test = false, int pack = -1)
    {
        if (n == null || L == null || World.Root == null) return null;
        var baseDef = NamedDb.BaseDef(n, out bool standIn);
        var d = baseDef.Variant(n.Id, n.NameZh, n.Tint == Colors.White ? baseDef.Tint : n.Tint);
        d.RiseFromGround = false;
        d.InBestiary = false;
        if (n.Boss)
        {
            d.AI = MonAI.Boss; d.Role = "boss";
            d.Scale = n.Scale;
            d.Poise = Mathf.Max(d.Poise, 8f);
            if (d.Radius < 0.6f) d.Radius = 0.6f;
        }
        else d.Scale = n.Scale / 1.2f;                   // an elite's model grows by a fifth on top of its definition
        n.Shape?.Invoke(d);
        var kitReady = n.Boss && BossKits.Has(n.Id);
        if (n.Boss && !kitReady) Log.Warn("named", $"{n.Id}: no boss kit registered, the generic one is used");

        IEnumerable<string> affixes = n.Affixes > 0 ? Populate.PickAffixes(n.Affixes, L.Rand ?? new Random()) : null;
        if (affixes != null) affixes = FilterAffixes(new List<string>(affixes), n.Affixes, L.Rand ?? new Random());
        var m = Monster.Spawn(d, level, n.Boss ? MonTier.Normal : MonTier.Elite, World.Grid != null ? World.Grid.NearestWalk(at) : at, Faction.Monster, affixes);
        if (pack >= 0) m.Pack = pack;
        int wt = Game.I?.WorldTier ?? 0;
        // the first ten levels: a hero in starter gear meets them (the same easing Monster.Setup gives elites and region bosses)
        float young = Mathf.Clamp((level - 1) / 9f, 0f, 1f);
        m.MaxLife = Mathf.Round(Balance.MonsterHp(level) * n.Hp * Balance.TierHp[wt] * Mathf.Lerp(0.8f, 1f, young));
        m.Life = m.MaxLife;
        m.BaseDamage = Balance.MonsterDmg(level) * n.Dmg * Balance.TierDmg[wt] * Mathf.Lerp(0.7f, 1f, young);
        m.XpValue = Balance.MonsterXp(level) * n.Xp;
        m.NoLoot = true;                                 // named loot is paid out by the hub (Slain)
        m.DisplayName = n.NameZh;
        string affixTitle = m.Title;
        m.Title = n.Kind == NamedKind.Rare ? (string.IsNullOrEmpty(affixTitle) ? n.Title : $"{n.Title}　{affixTitle}") : n.Title;
        if (n.Boss) m.Tier = MonTier.Boss;
        m.Model?.SetRim(n.Rim * (n.Boss ? 1.1f : 1.35f));
        if (levelBoss) { L.HasArena = true; L.ArenaPos = centre; L.ArenaRadius = arenaR; L.SetBoss(m, false); }

        var e = new NamedEncounter();
        L.AddChild(e);
        Link(m, e);
        Live.Add(e);
        e.Init(n, m, L, centre, arenaR, test);
        if (standIn) Log.Info("named", $"{n.Id}: base type '{n.Base}' is not in the roster yet; stand-in '{baseDef.Id}' (model {baseDef.Model})");
        return e;
    }

    /// <summary>Elite affixes that would fight a named monster's own design are swapped for others.</summary>
    private static IEnumerable<string> FilterAffixes(List<string> list, int n, Random rng)
    {
        string[] bad = { "splitting", "teleporter", "summoner", "waller" };
        list.RemoveAll(a => Array.IndexOf(bad, a) >= 0);
        var pool = new List<string>();
        foreach (var a in MonsterDb.Affixes) if (Array.IndexOf(bad, a.Id) < 0 && !list.Contains(a.Id)) pool.Add(a.Id);
        while (list.Count < n && pool.Count > 0) { int i = rng.Next(pool.Count); list.Add(pool[i]); pool.RemoveAt(i); }
        return list;
    }

    // ───────────────────────── death: loot, records ─────────────────────────

    /// <summary>The named monster of an encounter died: loot by tier, kill record, announcements.</summary>
    internal static void Slain(NamedEncounter e, Vector2 at)
    {
        var def = e.Def; var L = e.L;
        int tier = Game.I?.WorldTier ?? 0;
        NamedRecords.Count(def, tier);
        bool first = NamedRecords.Kills(def.Id) == 1;
        if (!e.NoLoot && L != null && GodotObject.IsInstanceValid(L) && L.Grid != null)
        {
            Vector2 spot = L.Grid.NearestWalk(at);
            Vector2 Near(float r) => L.Grid.RandomWalkNear(spot, r * 0.6f, r, L.Rand);
            int lv = e.Level;
            switch (def.LootTier)
            {
                case 1:
                    if (e.HalfLoot) { Loot.OpenChest(spot, 1); Loot.DropGold((int)(12f * lv), Near(1.6f)); }
                    else { Loot.OpenChest(spot, 2); Loot.DropGold((int)(25f * lv), Near(1.6f)); Loot.DropMaterials(3, 2, 0, Near(1.6f)); }
                    break;
                case 2:
                    Loot.OpenChest(spot, 2); Loot.OpenChest(Near(2f), first ? 3 : 2);
                    Loot.DropGold((int)(40f * lv), Near(1.8f)); Loot.DropMaterials(5, 3, 1, Near(1.8f));
                    break;
                case 3:
                    L.RewardChest(Near(2.6f), 3);
                    Loot.OpenChest(spot, 3); Loot.OpenChest(Near(2f), 2);
                    Loot.DropGold((int)(80f * lv), Near(2f)); Loot.DropMaterials(10, 6, 2, Near(2f));
                    break;
                case 4:
                    L.RewardChest(Near(2.6f), 3);
                    Loot.OpenChest(spot, 3); Loot.OpenChest(Near(2f), 3);
                    if (first || tier >= 2) Loot.OpenChest(Near(2.2f), 3);
                    Loot.DropGold((int)(100f * lv), Near(2f)); Loot.DropMaterials(12, 8, 3, Near(2f));
                    break;
            }
        }
        string label = def.Kind switch
        {
            NamedKind.Rare => "稀有菁英", NamedKind.Stronghold => "據點首領", NamedKind.Dungeon => "地城首領",
            NamedKind.World => "世界首領", NamedKind.Pinnacle => "燼階首領", _ => "首領",
        };
        Game.I?.Hud?.Toast($"{label}「{def.NameZh}」已被擊敗{(first ? "（首次）" : "")}", Palette.Gold);
        World.Emit("named", def.Id);
        if (def.Kind == NamedKind.World) WorldBossEvent.Claimed(def);
        if (L != null && GodotObject.IsInstanceValid(L)) NamedAreas.AfterKill(e, at);
        if (!e.Test) Game.I?.SaveNow("named");
    }

    // ───────────────────────── the hero's shots and ground effects ─────────────────────────

    private static readonly FieldInfo ZoneP = typeof(GroundZone).GetField("_p", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo ZoneM = typeof(GroundZone).GetField("_m", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo ZoneZ = typeof(GroundZone).GetField("_z", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo ShotP = typeof(Projectile).GetField("_p", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo ShotDir = typeof(Projectile).GetField("_dir", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly List<GroundZone> Zones = new();
    private static readonly List<Projectile> Shots = new();
    private static double _zonesAt = -1, _shotsAt = -1;

    /// <summary>Ground effects in the level (refreshed at most ten times a second).</summary>
    public static List<GroundZone> AllZones()
    {
        double now = Game.I?.Time ?? 0;
        if (Math.Abs(now - _zonesAt) < 0.1) { Zones.RemoveAll(z => z == null || !GodotObject.IsInstanceValid(z) || z.Done); return Zones; }
        _zonesAt = now;
        Zones.Clear();
        var root = World.Root;
        if (root != null && GodotObject.IsInstanceValid(root))
            foreach (var c in root.GetChildren()) if (c is GroundZone z && !z.Done && !z.IsQueuedForDeletion()) Zones.Add(z);
        return Zones;
    }

    public static bool ZoneOfHero(GroundZone z) => z != null && ZoneP?.GetValue(z) != null;
    public static bool ZoneOfMonster(GroundZone z) => z != null && ZoneM?.GetValue(z) != null;
    public static ZoneSpec ZoneSpecOf(GroundZone z) => z == null ? null : ZoneZ?.GetValue(z) as ZoneSpec;
    public static Vector2 ZonePos(GroundZone z) => new(z.GlobalPosition.X, z.GlobalPosition.Z);

    /// <summary>True when burning ground (the hero's or anyone's) lies within <paramref name="r"/> of a point.</summary>
    internal static bool FireNear(Vector2 p, float r)
    {
        foreach (var z in AllZones())
        {
            var s = ZoneSpecOf(z);
            if (s == null || s.El != Element.Fire) continue;
            if (ZonePos(z).DistanceTo(p) <= s.Radius + r) return true;
        }
        return false;
    }

    /// <summary>Removes the hero's ground effects inside a circle (null circle: all of them); optionally only one element. Returns the count.</summary>
    public static int SnuffHeroZones(Vector2? c, float r, Element? only = null)
    {
        int n = 0;
        foreach (var z in AllZones().ToArray())
        {
            if (!ZoneOfHero(z)) continue;
            var s = ZoneSpecOf(z);
            if (only != null && (s == null || s.El != only.Value)) continue;
            if (c != null && ZonePos(z).DistanceTo(c.Value) > r + (s?.Radius ?? 0f)) continue;
            z.Remove(); n++;
        }
        return n;
    }

    /// <summary>Projectiles in flight (refreshed at most twenty times a second).</summary>
    public static List<Projectile> AllShots()
    {
        double now = Game.I?.Time ?? 0;
        if (Math.Abs(now - _shotsAt) < 0.05) { Shots.RemoveAll(s => s == null || !GodotObject.IsInstanceValid(s) || s.IsQueuedForDeletion() || !s.Visible); return Shots; }
        _shotsAt = now;
        Shots.Clear();
        var root = World.Root;
        if (root != null && GodotObject.IsInstanceValid(root))
            foreach (var c in root.GetChildren()) if (c is Projectile s && !s.IsQueuedForDeletion() && s.Visible) Shots.Add(s);
        return Shots;
    }

    public static bool ShotOfHero(Projectile s) => s != null && ShotP?.GetValue(s) != null;
    public static Vector2 ShotPos(Projectile s) => new(s.GlobalPosition.X, s.GlobalPosition.Z);
    public static Vector2 ShotDirOf(Projectile s) => ShotDir?.GetValue(s) is Vector2 v ? v : Vector2.Zero;
    public static void SetShotDir(Projectile s, Vector2 d) { if (s != null && ShotDir != null && d.LengthSquared() > 1e-4f) ShotDir.SetValue(s, d.Normalized()); }
    public static void Kill(Projectile s) { if (s != null && GodotObject.IsInstanceValid(s) && !s.IsQueuedForDeletion()) Fx.ReleaseProjectile(s); }
}
