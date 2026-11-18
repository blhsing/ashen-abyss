    // ───────────────────────── rosters ─────────────────────────

    /// <summary>One family as it lives in a level: its weight and its types sorted by their place in a pack.</summary>
    private sealed class FamPool
    {
        public string Id; public float W; public bool Signature;
        public readonly List<(MonsterDef d, float w)> Core = new(), Back = new(), Heavy = new(), Special = new(), Giant = new(), Placed = new(), Cloud = new(), Leaders = new();
        public bool HasLine => Core.Count > 0;
        public bool HasPressure => Back.Count > 0 || Placed.Count > 0;
        public bool IsGuest => Id == "hauler";             // the Fuel-bearers never make a pack of their own
        public void Add(MonsterDef d, float w)
        {
            if (d == null || w <= 0f) return;
            (d.Slot switch
            {
                PackSlot.Back => Back, PackSlot.Heavy => Heavy, PackSlot.Special => Special, PackSlot.Giant => Giant,
                PackSlot.Placed => Placed, PackSlot.Cloud => Cloud, _ => Core,
            }).Add((d, w));
            if (d.CanLead && d.Slot is not (PackSlot.Placed or PackSlot.Cloud or PackSlot.Giant)) Leaders.Add((d, w));
        }
    }

    private sealed class Roster
    {
        public readonly List<(MonsterDef d, float w)> All = new(), Core = new(), Ranged = new(), Bruiser = new(), Leaders = new();
        /// <summary>The bestiary's families in this level (null: the region has no family table — the flat list above is used).</summary>
        public List<FamPool> Fams;
        public string Note = "";
    }

    private static Roster _roster;
    private static Level _rosterOf;
    private static readonly Dictionary<string, MonsterDef> SturdyDefs = new();

    /// <summary>
    /// A level's monsters. With the bestiary: the families of the region (or of the dungeon, or two or three drawn for a
    /// rift), each with its types sorted into pack slots, plus the flat lists the fallback uses. Without: the region's flat
    /// spawn list sorted by role into core / ranged / bruiser.
    /// </summary>
    private static Roster RosterOf(Level L)
    {
        if (_roster != null && _rosterOf == L) return _roster;
        var reg = L?.SpawnRegion;
        var r = new Roster();
        if (reg?.Spawns != null)
            foreach (var (id, w) in reg.Spawns)
            {
                var d = MonsterDb.Get(id);
                if (d == null) { Log.Warn("populate", $"region {reg.Id} lists unknown monster '{id}'"); continue; }
                if (d.Slot is PackSlot.Placed or PackSlot.Cloud) continue;     // never rolled into a walking pack
                r.All.Add((d, w));
                if (d.Role is "ranged" or "caster" or "support") r.Ranged.Add((d, w));
                else if (d.Role == "bruiser") r.Bruiser.Add((d, w));
                else r.Core.Add((d, w));
                if (d.CanLead && d.Slot != PackSlot.Giant) r.Leaders.Add((d, w));
            }
        if (r.All.Count == 0) r.All.Add((MonsterDb.Get("husk") ?? MonsterDb.All[0], 1f));
        if (r.Core.Count == 0) r.Core.AddRange(r.All);
        if (r.Leaders.Count == 0) foreach (var e in r.All) r.Leaders.Add((Sturdy(e.d), e.w));
        if (Bestiary.Live && reg?.Families != null && L != null)
            try { r.Fams = FamiliesOf(L, reg); }
            catch (Exception e) { Log.Error("populate", $"family roster of {L.AreaId} failed; the flat spawn list is used", e); r.Fams = null; }
        if (r.Fams != null && r.Fams.Count == 0) r.Fams = null;
        if (r.Fams != null) Log.Info("populate", $"roster {L.AreaId}: {string.Join(" ", r.Fams.Select(f => $"{f.Id}:{f.W:0.#}({f.Core.Count + f.Back.Count + f.Heavy.Count + f.Special.Count + f.Giant.Count + f.Placed.Count + f.Cloud.Count})"))}{r.Note}");
        _roster = r; _rosterOf = L;
        return r;
    }

    private static FamPool PoolOf(string fid, float w, bool sig, IEnumerable<string> typeIds)
    {
        var f = new FamPool { Id = fid, W = w, Signature = sig };
        foreach (var id in typeIds)
        {
            var d = MonsterDb.Get(id);
            if (d == null) continue;
            f.Add(d, MathF.Max(0.05f, d.SpawnWeight));
        }
        return f;
    }

    private static List<FamPool> FamiliesOf(Level L, RegionDef reg)
    {
        var res = new List<FamPool>();
        if (L.IsRift)
        {
            // a rift draws two or three families from the whole roster: one line family, one pressure family and, from
            // tier 5, a giant or a mother as a guest (BESTIARY.md 3.8); each type in the colours of the rift's region
            var rng = L.Rand;
            var all = Bestiary.Families.Where(f => f.Types.Length > 0 && f.Id != "hauler")
                .Select(f => PoolOf(f.Id, 1f, false, f.Types.Select(t => Bestiary.Regional(t, reg.Id)))).Where(p => p.Core.Count + p.Back.Count > 0).ToList();
            var line = all.Where(p => p.HasLine && p.Core.Any(c => c.d.Slot == PackSlot.Swarmer) && p.Core.Any(c => c.d.Slot == PackSlot.Front)).ToList();
            if (line.Count == 0) line = all.Where(p => p.HasLine).ToList();
            var first = line[rng.Next(line.Count)];
            first.W = 60f; res.Add(first);
            var press = all.Where(p => p != first && p.HasPressure).ToList();
            if (press.Count > 0) { var second = press[rng.Next(press.Count)]; second.W = 40f; res.Add(second); }
            if (L.RiftTier >= 5)
            {
                var guests = Bestiary.Families.Where(f => f.Types.Any(t => { var d = MonsterDb.Get(t); return d != null && (d.Giant || d.AI == MonAI.Spawner); }))
                    .Select(f => f.Id).Where(id => res.All(p => p.Id != id)).ToList();
                if (guests.Count > 0)
                {
                    var gf = Bestiary.Family(guests[rng.Next(guests.Count)]);
                    var guest = PoolOf(gf.Id, 0f, false, gf.Types.Where(t => { var d = MonsterDb.Get(t); return d != null && (d.Giant || d.AI == MonAI.Spawner); }).Select(t => Bestiary.Regional(t, reg.Id)));
                    res.Add(guest);
                }
            }
            return res;
        }
        string[] only = L.Dungeon?.Families;
        foreach (var (id, w, sig, types) in reg.Families)
        {
            if (only != null && Array.IndexOf(only, id) < 0) continue;
            res.Add(PoolOf(id, w, sig, types));
        }
        return res;
    }

    private static MonsterDef Pick(List<(MonsterDef d, float w)> list, Random rng)
    {
        float total = 0f;
        foreach (var e in list) total += e.w;
        float x = (float)rng.NextDouble() * total;
        foreach (var e in list) { x -= e.w; if (x <= 0f) return e.d; }
        return list[^1].d;
    }

    private static FamPool PickFam(List<FamPool> fams, Random rng, Func<FamPool, bool> ok, FamPool not = null)
    {
        float total = 0f;
        foreach (var f in fams) if (f != not && ok(f)) total += f.W;
        if (total <= 0f) return null;
        float x = (float)rng.NextDouble() * total;
        foreach (var f in fams) { if (f == not || !ok(f)) continue; x -= f.W; if (x <= 0f) return f; }
        foreach (var f in fams) if (f != not && ok(f)) return f;
        return null;
    }

    /// <summary>
    /// A monster fit to lead: one that bursts on its first attack would end its own fight, so as a champion, elite or
    /// mini-boss it becomes a heavy hitter with telegraphed swings instead (same id, so kill counters still match).
    /// </summary>
    private static MonsterDef Sturdy(MonsterDef d)
    {
        if (d == null || d.AI != MonAI.Exploder) return d;
        if (!SturdyDefs.TryGetValue(d.Id, out var s))
            SturdyDefs[d.Id] = s = d.Variant(d.Id, d.Name, d.Tint, v =>
            {
                v.AI = MonAI.Brute; v.Range = 3f; v.Contact = 0.65f; v.Cooldown = 2.3f; v.Dmg = 1.6f; v.Speed = MathF.Max(v.Speed, 3.4f);
                v.Traits &= ~(MonTrait.Trail | MonTrait.DeathBurst);
            });
        return s;
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }

    /// <summary>Elite affixes, each from a different group (hazard / control / defensive / behavioural).</summary>
    public static List<string> PickAffixes(int n, Random rng)
    {
        var groups = MonsterDb.Affixes.GroupBy(a => a.Group).Select(g => g.ToList()).ToList();
        Shuffle(groups, rng);
        var res = new List<string>(n);
        for (int i = 0; i < n && i < groups.Count; i++) res.Add(groups[i][rng.Next(groups[i].Count)].Id);
        return res;
    }

    // ───────────────────────── spawning ─────────────────────────

    /// <summary>Every monster Populate places goes through here (the census counts them).</summary>
    public static Monster SpawnOne(Level L, MonsterDef def, MonTier tier, Vector2 pos, int pack, IEnumerable<string> affixes = null, bool mini = false)
    {
        var m = Monster.Spawn(def, L.MonsterLevel, tier, L.Grid.NearestWalk(pos), Faction.Monster, affixes, mini);
        m.Pack = pack;
        return m;
    }

    private static Vector2 Scatter(Level L, Vector2 centre, float rMin, float rMax) => L.Grid.RandomWalkNear(centre, rMin, rMax, L.Rand);

    /// <summary>The pack-size caps of the first levels: a fresh hero with a starter weapon cannot hold eight husks at once.</summary>
    private static int PackCap(Level L) => L.MonsterLevel <= 2 ? 4 : L.MonsterLevel <= 4 ? 6 : L.MonsterLevel <= 8 ? 9 : 14;

    /// <summary>
    /// A pack: mostly one or two kinds of swarmer or melee, zero to two ranged or casters behind them and sometimes a
    /// bruiser. Everyone shares a pack id so the whole group wakes together. With the bestiary the pack is drawn from
    /// one family by the region's family weights (its back line or special now and then from a second family), may
    /// carry one guest giant, a swarm cloud, and ambushers or emplacements placed round it.
    /// </summary>
    public static List<Monster> Pack(Level L, Vector2 pos, int count, bool objective = false, List<Monster> into = null)
    {
        var ro = RosterOf(L);
        if (ro.Fams != null) return FamilyPack(L, ro, pos, count, objective, into);
        var rng = L.Rand;
        int cap = PackCap(L);
        int n = Math.Clamp(count, 1, cap), pack = L.NextPack();
        int ranged = 0, bruiser = 0;
        if (ro.Ranged.Count > 0)
        {
            double x = rng.NextDouble();
            ranged = n >= 5 ? (x < 0.3 ? 0 : x < 0.75 ? 1 : 2) : n >= 3 ? (x < 0.55 ? 0 : 1) : 0;
        }
        if (ro.Bruiser.Count > 0 && n - ranged >= 3 && rng.NextDouble() < 0.28) bruiser = 1;
        int core = n - ranged - bruiser;
        var a = Pick(ro.Core, rng);
        var b = rng.NextDouble() < 0.3 ? Pick(ro.Core, rng) : a;
        var list = into ?? new List<Monster>(n);
        void Add(Monster m) { m.Objective = objective; list.Add(m); }
        for (int i = 0; i < core; i++) Add(SpawnOne(L, i % 3 == 2 ? b : a, MonTier.Normal, Scatter(L, pos, 0.4f, 1.6f + n * 0.3f), pack));
        for (int i = 0; i < ranged; i++) Add(SpawnOne(L, Pick(ro.Ranged, rng), MonTier.Normal, Scatter(L, pos, 2.5f, 4.5f), pack));
        if (bruiser > 0) Add(SpawnOne(L, Pick(ro.Bruiser, rng), MonTier.Normal, Scatter(L, pos, 0.5f, 2.2f), pack));
        return list;
    }

    private static List<Monster> FamilyPack(Level L, Roster ro, Vector2 pos, int count, bool objective, List<Monster> into)
    {
        var rng = L.Rand; var fams = ro.Fams;
        int cap = PackCap(L);
        int n = Math.Clamp(count, 1, cap), pack = L.NextPack();
        var list = into ?? new List<Monster>(n + 3);
        var f = PickFam(fams, rng, p => !p.IsGuest && (p.HasLine || p.Back.Count > 0 || p.Heavy.Count > 0));
        if (f == null) { ro.Fams = null; return Pack(L, pos, count, objective, into); }
        // a second family for the back line and the special now and then (packs of two or three families)
        var partner = rng.NextDouble() < 0.35 ? PickFam(fams, rng, p => !p.IsGuest && (p.Back.Count > 0 || p.Special.Count > 0), f) : null;
        var backFrom = f.Back.Count > 0 && (partner == null || partner.Back.Count == 0 || rng.NextDouble() < 0.5) ? f : partner?.Back.Count > 0 ? partner : null;
        var specialFrom = f.Special.Count > 0 && (partner == null || partner.Special.Count == 0 || rng.NextDouble() < 0.6) ? f : partner?.Special.Count > 0 ? partner : null;
        int left = n;
        void Add(Monster m, bool obj = true) { if (m == null) return; m.Objective = objective && obj; list.Add(m); }
        Monster Put(MonsterDef d, float rMin, float rMax, int packId = -1)
        {
            var m = SpawnOne(L, d, MonTier.Normal, Scatter(L, pos, rMin, rMax), packId < 0 ? pack : packId);
            Add(m);
            // the ash-handler's hounds come with him and count toward the pack
            if (d.Is(MonTrait.Escort) && d.Ai.EscortDefs != null)
            {
                var esc = new List<Monster>();
                foreach (var e in d.Ai.EscortDefs)
                {
                    var ed = Bestiary.Local(e) ?? MonsterDb.Get(e);
                    if (ed == null) continue;
                    var em = SpawnOne(L, ed, MonTier.Normal, L.Grid.RandomWalkNear(m.Pos2, 0.8f, 2f, rng), m.Pack);
                    Add(em); esc.Add(em); left--;
                }
                m.AdoptEscorts(esc);
            }
            return m;
        }
        // one guest giant at most (a Fuel-bearer in someone else's pack, or the family's own colossus); not before level 4
        if (L.MonsterLevel >= 4 && left >= 3)
        {
            var hauler = fams.FirstOrDefault(p => p.IsGuest && p.Giant.Count > 0);
            MonsterDef giant = null;
            if (hauler != null && rng.NextDouble() < Math.Clamp(hauler.W / 100.0 * 2.2, 0.03, 0.2)) giant = Pick(hauler.Giant, rng);
            else if (f.Giant.Count > 0 && rng.NextDouble() < 0.18) giant = Pick(f.Giant, rng);
            if (giant != null) { Put(giant, 0.6f, 2.4f); left -= 2; }       // a giant takes two places of the pack
        }
        // a swarm cloud drifts with the pack
        if (f.Cloud.Count > 0 && left >= 2 && rng.NextDouble() < 0.55) { Put(Pick(f.Cloud, rng), 0.5f, 2f); left--; }
        // one special (healer, reviver, mother, caller, aura or shield support)
        if (specialFrom != null && left >= 3 && rng.NextDouble() < (specialFrom == f ? 0.4 : 0.3)) { Put(Pick(specialFrom.Special, rng), 2.5f, 4.5f); left--; }
        // the back line
        int back = 0;
        if (backFrom != null && left >= 3)
        {
            double x = rng.NextDouble();
            back = left >= 5 ? (x < 0.3 ? 0 : x < 0.75 ? 1 : 2) : (x < 0.55 ? 0 : 1);
            if (!f.HasLine) back = Math.Max(back, Math.Min(left, 2));
        }
        for (int i = 0; i < back && left > 0; i++, left--) Put(Pick(backFrom.Back, rng), 2.5f, 4.5f);
        // a heavy
        if (f.Heavy.Count > 0 && left >= 3 && rng.NextDouble() < 0.28) { Put(Pick(f.Heavy, rng), 0.5f, 2.2f); left--; }
        // the core: one or two kinds of swarmer and line melee
        var core = f.HasLine ? f.Core : f.Back.Count > 0 ? f.Back : f.Heavy;
        if (left > 0 && core.Count > 0)
        {
            var a = Pick(core, rng);
            var b = rng.NextDouble() < 0.35 ? Pick(core, rng) : a;
            for (int i = 0; i < left; i++) Put(i % 3 == 2 ? b : a, 0.4f, 1.6f + n * 0.3f);
            left = 0;
        }
        // ambushers and emplacements are placed round the pack by the layout, in their own group: they wake on their own trigger
        var placedFrom = f.Placed.Count > 0 ? f : partner?.Placed.Count > 0 ? partner : null;
        if (placedFrom != null && rng.NextDouble() < 0.6)
        {
            int ambush = L.NextPack();
            int k = 1 + (n >= 5 && rng.NextDouble() < 0.5 ? 1 : 0) + (n >= 8 && rng.NextDouble() < 0.4 ? 1 : 0);
            var d = Pick(placedFrom.Placed, rng);
            for (int i = 0; i < k; i++)
            {
                Vector2 at = PlacedSpot(L, pos, d);
                var m = SpawnOne(L, d, MonTier.Normal, at, ambush);
                if (d.AI == MonAI.Turret) m.FaceDir(pos - m.Pos2);
                Add(m, false);
            }
        }
        return list;
    }

    /// <summary>Where the layout puts an ambusher or an emplacement: statues and turrets against a wall, the buried and the roosting on the path near the pack.</summary>
    private static Vector2 PlacedSpot(Level L, Vector2 pos, MonsterDef d)
    {
        Vector2 at = Scatter(L, pos, 3f, 7f);
        bool wall = d.AI == MonAI.Turret || d.Ai.DormantMode == "statue";
        return wall ? NookNear(L, at, 2) : at;
    }

    /// <summary>A leader from the level's families (champions, elites, mini-bosses): never an exploder, swarm, turret, mother, support or giant.</summary>
    private static MonsterDef LeaderDef(Level L, Roster ro, out FamPool fam)
    {
        fam = null;
        if (ro.Fams != null)
        {
            fam = PickFam(ro.Fams, L.Rand, p => p.Leaders.Count > 0);
            if (fam != null) return Pick(fam.Leaders, L.Rand);
        }
        return Sturdy(Pick(ro.Leaders, L.Rand));
    }

    /// <summary>One champion with a single affix, joining an existing pack (or alone when pack is below zero).</summary>
    public static Monster Champion(Level L, Vector2 pos, int pack = -1)
    {
        var ro = RosterOf(L);
        return SpawnOne(L, LeaderDef(L, ro, out _), MonTier.Champion, Scatter(L, pos, 0.5f, 2.5f), pack < 0 ? L.NextPack() : pack, PickAffixes(1, L.Rand));
    }

    /// <summary>Three to five champions of one kind sharing one affix.</summary>
    public static List<Monster> Champions(Level L, Vector2 pos, int count, List<Monster> into = null)
    {
        var ro = RosterOf(L); var rng = L.Rand;
        int n = Math.Clamp(count, 3, 5), pack = L.NextPack();
        var def = LeaderDef(L, ro, out _);
        var affix = PickAffixes(1, rng);
        var list = into ?? new List<Monster>(n);
        for (int i = 0; i < n; i++) list.Add(SpawnOne(L, def, MonTier.Champion, Scatter(L, pos, 0.5f, 3f), pack, affix));
        return list;
    }

    /// <summary>An elite leader (two affixes, three from world tier 2) with two to four ordinary followers. Returns the leader.</summary>
    public static Monster Elite(Level L, Vector2 pos, int count, bool objective, List<Monster> into = null)
    {
        var ro = RosterOf(L); var rng = L.Rand;
        int pack = L.NextPack();
        int affixes = (Game.I?.WorldTier ?? 0) >= 2 ? 3 : 2;
        var leader = SpawnOne(L, LeaderDef(L, ro, out var fam), MonTier.Elite, pos, pack, PickAffixes(affixes, rng));
        leader.Objective = objective;
        into?.Add(leader);
        var follower = fam != null && fam.Core.Count > 0 ? Pick(fam.Core, rng) : Pick(ro.Core, rng);
        int minions = Math.Clamp(count - 1, 2, 4);
        for (int i = 0; i < minions; i++)
        {
            var m = SpawnOne(L, follower, MonTier.Normal, Scatter(L, pos, 1.2f, 3.4f), pack);
            into?.Add(m);
        }
        return leader;
    }

    /// <summary>A stronghold's garrison: a denser pack with one champion among it.</summary>
    public static List<Monster> StrongholdPack(Level L, Vector2 pos, int count, List<Monster> into = null)
    {
        var list = into ?? new List<Monster>();
        int before = list.Count;
        Pack(L, pos, count + 2, false, list);
        int pack = list.Count > before ? list[before].Pack : -1;
        list.Add(Champion(L, pos, pack));
        return list;
    }

    /// <summary>A named mini-boss built from an ordinary monster.</summary>
    public static Monster MiniBoss(Level L, MonsterDef def, Vector2 pos, int affixes, string title)
    {
        def = Sturdy(def ?? LeaderDef(L, RosterOf(L), out _));
        var m = SpawnOne(L, def, MonTier.Elite, pos, L.NextPack(), PickAffixes(affixes, L.Rand), true);
        if (!string.IsNullOrEmpty(title)) m.Title = title;
        return m;
    }

    /// <summary>The region's spell-slinger, for rituals.</summary>
    public static MonsterDef CasterDef(Level L)
    {
        var ro = RosterOf(L);
        if (ro.Fams != null)
        {
            var casters = ro.Fams.SelectMany(f => f.Back).Where(e => e.d.AI == MonAI.Caster && !e.d.Is(MonTrait.Breath)).ToList();
            if (casters.Count > 0) return Pick(casters, L.Rand);
        }
        foreach (var e in ro.Ranged) if (e.d.Role == "caster") return e.d;
        return MonsterDb.Get("cult_caster") ?? Pick(ro.Leaders, L.Rand);
    }

    /// <summary>A walking ordinary monster of the level, for anybody who needs adds (boss fights, named encounters): no bombs, clouds, turrets or giants.</summary>
    public static MonsterDef AddDef(Level L, Random rng = null)
    {
        if (L == null) return MonsterDb.Get("skeleton");
        var ro = RosterOf(L);
        rng ??= L.Rand;
        if (ro.Fams != null)
        {
            var f = PickFam(ro.Fams, rng, p => p.Core.Count > 0);
            if (f != null)
            {
                var ok = f.Core.Where(e => e.d.AI is not (MonAI.Exploder or MonAI.Burrower) && !e.d.Is(MonTrait.Dormant)).ToList();
                if (ok.Count > 0) return Pick(ok, rng);
            }
        }
        var flat = ro.Core.Where(e => e.d.AI != MonAI.Exploder).ToList();
        return flat.Count > 0 ? Pick(flat, rng) : MonsterDb.Get("skeleton");
    }

    private static void SpawnGroupOf(Level L, SpawnGroup g)
    {
        switch (g.Kind)
        {
            case "champions": Champions(L, g.Pos, g.Count); break;
            case "elite": Elite(L, g.Pos, g.Count, false); break;
            case "stronghold": StrongholdPack(L, g.Pos, g.Count); break;
            default: Pack(L, g.Pos, g.Count); break;
        }
    }
