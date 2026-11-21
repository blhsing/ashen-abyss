using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AshenAbyss;

/// <summary>
/// Scripted QA suites (--suite=name[:args], hosted by TestBot). Each suite is a coroutine that creates its own
/// characters, drives the real game objects through their public API and writes one "[suite]" log line per check:
///   skills[:class,class…[:level,level…]]   every skill of a class, at each level, per upgrade branch and spec
///   systems                                items, crafting, vendors, stash, skill points, spec, paragon, tiers
///   objects                                chests, shrines, altars, loot of every rarity, gold / globes / materials
///   save                                   save / load round trips, all slots, backup fallback, settings
/// The run ends with "[suite] DONE … pass=N fail=M" and quits the game.
/// </summary>
public sealed partial class TestSuite
{
    private sealed class WaitUntil { public Func<bool> Cond; public double Timeout; public string What; }

    private readonly Stack<IEnumerator> _stack = new();
    private readonly string _name, _arg;
    private float _wait;
    private WaitUntil _until;
    private double _untilLeft;
    private string _section = "start";
    private int _pass, _fail;
    private readonly List<string> _failures = new();
    public bool Finished { get; private set; }

    // damage bookkeeping for the test in progress
    private int _hits, _ownHits, _minionHits, _dotHits, _kills;
    private float _dmg;
    private string _curSkill;

    private static Game G => Game.I;

    private readonly TestBot _bot;
    private Monster _focusTarget;
    private float _focusDmg, _minionDmg, _otherDmg;

    public TestSuite(string spec, TestBot bot = null)
    {
        _bot = bot;
        spec ??= "";
        int colon = spec.IndexOf(':');
        _name = (colon >= 0 ? spec[..colon] : spec).Trim().ToLowerInvariant();
        _arg = colon >= 0 ? spec[(colon + 1)..].Trim() : "";
        World.Damage += OnDamage;
        World.MonsterKilled += OnKilled;
        _stack.Push(Root());
        Log.Info("suite", $"suite '{_name}' arg='{_arg}' starting");
    }

    /// <summary>Stops the suite; coroutines are disposed so their finally blocks (restoring the user's saves) still run.</summary>
    public void Abort()
    {
        World.Damage -= OnDamage;
        World.MonsterKilled -= OnKilled;
        while (_stack.Count > 0)
        {
            var co = _stack.Pop();
            try { (co as IDisposable)?.Dispose(); }
            catch (Exception e) { Log.Error("suite", "cleanup failed", e); }
        }
    }

    private void OnDamage(Actor a, Hit h, float amount)
    {
        if (a is not Monster m || m.Faction != Faction.Monster || amount <= 0f) return;
        _hits++; _dmg += amount;
        if (a == _focusTarget) _focusDmg += amount;
        if (h.Dot) _dotHits++;
        if (h.Minion) { _minionHits++; _minionDmg += amount; }
        if (h.Skill == null && !h.Dot && !h.Minion) _otherDmg += amount;
        if (h.Skill != null && h.Skill.Id == _curSkill) _ownHits++;
    }

    private void OnKilled(Monster m, Hit h) { if (m != null && m.Faction == Faction.Monster) _kills++; }

    // ───────────────────────── scheduler ─────────────────────────

    /// <summary>Advances the suite. dt is game time (scaled), real is wall-clock time.</summary>
    public void Tick(float dt, float real)
    {
        if (Finished) return;
        if (_wait > 0f) { _wait -= dt; return; }
        if (_until != null)
        {
            bool ok;
            try { ok = _until.Cond(); }
            catch (Exception e) { Log.Error("suite", $"wait condition '{_until.What}' failed", e); ok = true; }
            if (!ok)
            {
                _untilLeft -= real;
                if (_untilLeft > 0) return;
                Fail($"timeout waiting for {_until.What}");
            }
            _until = null;
        }
        for (int guard = 0; guard < 200; guard++)
        {
            if (_stack.Count == 0) { Finish(); return; }
            var co = _stack.Peek();
            bool more;
            try { more = co.MoveNext(); }
            catch (Exception e)
            {
                Log.Error("suite", $"step failed in '{_section}'", e);
                Fail($"exception in '{_section}': {e.GetType().Name}: {e.Message}");
                _stack.Pop();
                continue;
            }
            if (!more) { _stack.Pop(); continue; }
            switch (co.Current)
            {
                case null: return;
                case float f: _wait = f; return;
                case double d: _wait = (float)d; return;
                case int i: _wait = i; return;
                case WaitUntil w: _until = w; _untilLeft = w.Timeout; return;
                case IEnumerator sub: _stack.Push(sub); continue;
                default: return;
            }
        }
    }

    private static WaitUntil Until(Func<bool> cond, double timeoutReal, string what) => new() { Cond = cond, Timeout = timeoutReal, What = what };

    private void Pass(string what) { _pass++; Log.Info("suite", $"PASS {what}"); }
    private void Fail(string what) { _fail++; _failures.Add(what); Log.Warn("suite", $"FAIL {what}"); }
    private bool Check(bool ok, string what, string detail = null)
    {
        if (ok) Pass(detail == null ? what : $"{what} — {detail}");
        else Fail(detail == null ? what : $"{what} — {detail}");
        return ok;
    }

    private void Finish()
    {
        if (Finished) return;
        Finished = true;
        Abort();
        Log.Info("suite", $"DONE suite={_name} pass={_pass} fail={_fail} errors={Log.ErrorCount} warnings={Log.WarnCount}");
        Log.Info("suite", AnimCheck.Report());
        for (int i = 0; i < _failures.Count; i++) Log.Info("suite", $"  failure {i + 1}: {_failures[i]}");
        G.Quit();
    }

    private IEnumerator Root()
    {
        yield return Until(() => G.State == Game.GameState.Menu || G.State == Game.GameState.Play, 60, "the menu");
        yield return null;
        switch (_name)
        {
            case "skills": yield return SkillsSuite(); break;
            case "systems": yield return SystemsSuite(); break;
            case "objects": yield return ObjectsSuite(); break;
            case "save": yield return SaveSuite(); break;
            case "dps": yield return DpsSuite(); break;
            case "gear": yield return GearSuite(); break;        // RENDER: --suite=gear[:part] (TestSuite.Gear.cs)
            case "items": yield return ItemsSuite(); break;      // SYSTEMS: --suite=items[:fixture dir] (TestSuite.Items.cs): save migration, hands, loot spread
            case "mapquest": yield return MapQuestSuite(); break; // MAP-QUEST: --suite=mapquest (TestSuite.MapQuest.cs): quest pins on the maps
            case "named": yield return NamedSuite(); break;      // NAMED-HOOK: --suite=named[:filter[:class]] (TestSuite.Named.cs)
            case "census": case "bestiary": case "ai": case "rosterperf": yield return RosterSuite(); break;      // TestSuite.Roster.cs
            case "anims":
                if (_arg.StartsWith("shots")) yield return AnimShots(); else yield return AnimsSuite();
                break;
            case "all":
                yield return SystemsSuite();
                yield return SaveSuite();
                yield return ObjectsSuite();
                yield return SkillsSuite();
                break;
            default: Fail($"unknown suite '{_name}' (skills, systems, objects, save, all)"); break;
        }
    }

    // ───────────────────────── shared helpers ─────────────────────────

    private IEnumerator StartHero(ClassId cls, int level, string area, bool gear = true, bool god = true, int tier = 0, int paragon = -1, int slot = Game.HarnessSlot, bool hardcore = false)
    {
        yield return Until(() => G.State != Game.GameState.Loading, 90, "the previous load");
        G.HarnessNext(area, level, gear, tier, god, paragon);
        G.NewGame(slot, "試煉者", cls, hardcore);
        yield return Until(() => G.State == Game.GameState.Play && G.Hero != null && G.Hero.Class == cls && G.Player != null && GodotObject.IsInstanceValid(G.Player)
            && G.Player.IsInsideTree() && G.Level != null && G.Level.Built, 120, $"{Names.ClassKey(cls)} lv{level} in {area}");
        yield return 0.25f;
    }

    /// <summary>The most open walkable cell of the level (largest square of floor round it), nearest to a point on ties.</summary>
    private static Vector2 FindArena(NavGrid grid, Vector2 near, int maxCells = 6)
    {
        int best = -1; float bestD = float.MaxValue; Vector2 at = near;
        for (int y = 0; y < grid.H; y++)
            for (int x = 0; x < grid.W; x++)
            {
                if (!grid.Walk(x, y)) continue;
                int r = 0;
                if (!grid.Clear(x, y)) continue;
                for (int rr = 1; rr <= maxCells; rr++)
                {
                    bool open = true;
                    for (int dy = -rr; dy <= rr && open; dy++)
                        for (int dx = -rr; dx <= rr; dx++)
                            if (!grid.Clear(x + dx, y + dy)) { open = false; break; }
                    if (!open) break;
                    r = rr;
                }
                var c = grid.Center(x, y);
                float d = c.DistanceSquaredTo(near);
                if (r > best || (r == best && d < bestD)) { best = r; bestD = d; at = c; }
            }
        Log.Info("suite", $"arena at ({at.X:0.0}, {at.Y:0.0}) clearance {best} cells");
        return at;
    }

    private static void ClearField(bool loot = true)
    {
        foreach (var m in World.Monsters.ToArray()) if (GodotObject.IsInstanceValid(m)) m.QueueFree();
        foreach (var m in World.Minions.ToArray()) if (GodotObject.IsInstanceValid(m)) m.QueueFree();
        World.Monsters.Clear(); World.Minions.Clear();
        foreach (var r in World.Remnants) if (GodotObject.IsInstanceValid(r.Fx)) r.Fx.QueueFree();
        World.Remnants.Clear();
        var root = World.Root;
        if (root != null && GodotObject.IsInstanceValid(root))
            foreach (var c in root.GetChildren()) if (c is Projectile or GroundZone) c.QueueFree();
        if (loot)
            foreach (var it in Interactable.All.ToArray())
                if (GodotObject.IsInstanceValid(it) && (it.Item != null || it.AutoRadius > 0f)) it.QueueFree();
    }

    private static Monster Dummy(string defId, int level, Vector2 at, float life, MonTier tier = MonTier.Normal)
    {
        var def = MonsterDb.Get(defId) ?? MonsterDb.Get("cultist");
        var grid = World.Grid;
        var m = Monster.Spawn(def, level, tier, grid != null ? grid.NearestWalk(at) : at, Faction.Monster);
        if (life > 0f) { m.MaxLife = life; m.Life = life; }
        m.NoLoot = true;
        m.ForceAggro();
        return m;
    }

    private static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    // ═════════════════════════ skills ═════════════════════════

    private IEnumerator SkillsSuite()
    {
        var classes = new List<ClassId>();
        var levels = new List<int> { 1, 12, 25, 50 };
        string area = "town";
        var parts = _name == "skills" ? _arg.Split(':', StringSplitOptions.TrimEntries) : Array.Empty<string>();
        if (parts.Length > 0 && parts[0].Length > 0 && parts[0] != "all")
            foreach (var key in parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int idx = Array.IndexOf(Names.ClassKeys, key.ToLowerInvariant());
                if (idx >= 0) classes.Add((ClassId)idx); else Fail($"skills: unknown class '{key}'");
            }
        if (classes.Count == 0) for (int i = 0; i < Names.ClassCount; i++) classes.Add((ClassId)i);
        if (parts.Length > 1 && parts[1].Length > 0)
        {
            levels.Clear();
            foreach (var l in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (int.TryParse(l, out int lv)) levels.Add(Math.Clamp(lv, 1, Balance.LevelCap));
        }
        if (parts.Length > 2 && parts[2].Length > 0) area = parts[2];

        foreach (var cls in classes)
            foreach (int level in levels)
            {
                _section = $"skills {Names.ClassKey(cls)} lv{level}";
                yield return StartHero(cls, level, area);
                if (G.Hero == null || G.Hero.Class != cls) { Fail($"{_section}: hero not created"); continue; }
                yield return SkillPass(cls, level);
            }
    }

    private IEnumerator SkillPass(ClassId cls, int level)
    {
        var h = G.Hero; var p = G.Player;
        string tag = $"{Names.ClassKey(cls)} lv{level}";
        Log.Info("suite", $"skills {tag}: life={h.MaxLife:0} armor={h.Armor:0} weapon={h.WeaponMin:0}-{h.WeaponMax:0} resource={h.MaxResource:0} pips={h.MaxPips} bar=[{string.Join(",", h.D.Bar.Select(b => b ?? "-"))}]");
        ClearField();
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint);
        int fails0 = _fail, tests = 0;
        var all = SkillDb.Of(cls).OrderBy(s => s.UnlockLevel).ToList();
        var unlocked = all.Where(s => s.UnlockLevel <= level).ToList();
        if (level >= Balance.LevelCap) Check(unlocked.Count == all.Count && all.Count == 8, $"skills {tag}: all {all.Count} skills unlocked at the level cap (expected 8)");
        var specs = SkillDb.SpecsOf(cls).Select(s => s.Id).ToList();
        string defaultSpec = level >= SkillDb.SpecLevel ? h.D.Spec : null;
        bool full = level >= Balance.LevelCap;

        foreach (var s in unlocked)
        {
            // at the cap: no spec × (base, A, B), then every spec with one branch; below it: what the level allows
            var runs = new List<(string spec, string variant, int rank)>();
            if (full)
            {
                runs.Add((null, "base", 1)); runs.Add((null, "A", 5)); runs.Add((null, "B", 5));
                for (int i = 0; i < specs.Count; i++) runs.Add((specs[i], i % 2 == 0 ? "A" : "B", 3));
            }
            else runs.Add((defaultSpec, level >= 12 ? "A" : "base", Math.Clamp(level / 8 + 1, 1, 5)));
            foreach (var run in runs)
            {
                tests++;
                _section = $"skill {tag} {s.Id} {run.variant} spec={run.spec ?? "-"}";
                yield return SkillTest(p, h, s, run.spec, run.variant, run.rank, arena, tag);
            }
        }
        ClearField();
        Log.Info("suite", $"skills {tag}: {unlocked.Count} skills, {tests} tests, new failures={_fail - fails0}");
    }

    private static void Configure(Hero h, SkillDef s, string spec, string variant, int rank)
    {
        var d = h.D;
        string basic = SkillDb.DefaultBasic(h.Class);
        d.Skills.Clear(); d.Ups.Clear();
        d.Skills[basic] = 1;
        d.Skills[s.Id] = Math.Clamp(rank, 1, SkillDef.MaxRank);
        if (variant != "base")
        {
            if (s.Enh != null) d.Ups.Add(s.Enh.Id);
            var u = variant == "A" ? s.A : s.B;
            if (u != null) d.Ups.Add(u.Id);
        }
        // the pact fiend is what Command skills order about: keep its summoning skill known
        if (s.Kind == SkillKind.Command)
            foreach (var o in SkillDb.Of(h.Class)) if (o.Kind == SkillKind.Summon && o.Summon != null && o.Summon.StartsWith("pact")) d.Skills[o.Id] = 1;
        d.Spec = spec;
        d.Bar = new string[6];
        d.Bar[0] = basic; d.Bar[2] = s.Id;
        h.Buffs.RemoveAll(b => b.Def.Id != "test_god");
        h.Recalc();
        if (G.God) Game.EnsureGod(h);
    }

    private IEnumerator SkillTest(Player p, Hero h, SkillDef baseDef, string spec, string variant, int rank, Vector2 arena, string tag)
    {
        const int slot = 2;
        string id = baseDef.Id;
        Configure(h, baseDef, spec, variant, rank);
        var d = h.Eff(id);
        if (d == null) { Fail($"skill {tag} {id}: no effective definition"); yield break; }

        ClearField();
        p.ForceHold = -1;
        p.CancelAction();
        p.SetForm(0);
        p.Teleport(arena);
        p.FaceDir(Vector2.Right);
        p.Cds.Clear();
        p.StealthLeft = 0f; p.FreeNext = false;
        yield return null;

        int mlvl = Math.Max(1, h.D.Level);
        var d1 = Dummy("cultist", mlvl, arena + new Vector2(2.2f, 0f), 5e7f);
        var d2 = Dummy("cultist", mlvl, arena + new Vector2(3.0f, 1.3f), 5e7f);
        Dummy("cultist", mlvl, arena + new Vector2(3.6f, -1.4f), 0f);
        Dummy("husk", mlvl, arena + new Vector2(4.4f, 0.6f), 0f);
        Dummy("husk", mlvl, arena + new Vector2(4.8f, -0.4f), 0f);
        yield return null;

        // a command needs its fiend first
        if (d.Kind == SkillKind.Command)
        {
            var sum = SkillDb.Of(h.Class).FirstOrDefault(o => o.Kind == SkillKind.Summon && o.Summon != null && o.Summon.StartsWith("pact"));
            if (sum != null)
            {
                h.D.Bar[3] = sum.Id; h.Recalc();
                p.Resource = h.MaxResource; p.Pips = h.MaxPips;
                p.TryCast(3, arena + new Vector2(1.5f, 1.5f), null, false);
                yield return Until(() => !p.Busy, 4, "fiend summon");
                yield return 0.4f;
            }
        }

        _hits = _ownHits = _minionHits = _dotHits = _kills = 0; _dmg = 0f; _curSkill = id;
        int err0 = Log.ErrorCount, fired0 = p.Fired, chan0 = p.ChanTicks;
        var buffsBefore = new HashSet<string>(h.Buffs.Select(b => b.Def.Id));
        bool generator = d.Cost < 0f;
        bool usesPips = d.PipSpend || d.PipReq > 0;
        p.Resource = generator ? 0f : h.MaxResource;
        p.Pips = usesPips ? h.MaxPips : 0;
        float res0 = p.Resource; int pips0 = p.Pips;
        float res1 = res0; int pips1 = pips0;
        string cdNote = "-";
        Vector2 start = p.Pos2;
        float moved = 0f;
        int casts = 0, accepted = 0, formSeen = p.Form, minionsMax = 0;
        string refuse = null;
        const int wantCasts = 3;
        var gainedIds = new HashSet<string>();

        for (int c = 0; c < wantCasts; c++)
        {
            yield return Until(() => !p.Busy, 4, $"{id} hero free");
            if (p.Dead) { Fail($"skill {tag} {id}: the hero died during the test"); break; }
            Actor target = World.Nearest(Faction.Player, p.Pos2, 40f);
            if (target == null) target = Dummy("cultist", mlvl, p.Pos2 + new Vector2(2.2f, 0f), 5e7f);
            if (target.Pos2.DistanceTo(p.Pos2) > 9f) p.Teleport(World.Grid.NearestWalk(target.Pos2 - new Vector2(2.4f, 0f)));
            Vector2 point = target.Pos2;
            Vector2 before = p.Pos2;
            casts++;
            int f0 = p.Fired, t0 = p.ChanTicks;
            string why = p.WhyCant(d);
            if (d.Kind == SkillKind.Channel)
            {
                p.ForceHold = slot;
                bool ok = p.TryCast(slot, point, target, true);
                if (ok) accepted++; else refuse ??= why ?? "TryCast returned false";
                yield return 1.8f;
                p.ForceHold = -1;
                yield return 0.25f;
            }
            else
            {
                bool ok = p.TryCast(slot, point, target, true);
                if (ok) accepted++; else refuse ??= why ?? "TryCast returned false";
                yield return Until(() => p.Fired > f0 || p.Dead, 3, $"{id} to fire");
                yield return Until(() => !p.Busy, 4, $"{id} to finish");
            }
            moved = Mathf.Max(moved, p.Pos2.DistanceTo(before));
            foreach (var b in h.Buffs) if (!buffsBefore.Contains(b.Def.Id)) gainedIds.Add(b.Def.Id);     // short buffs may be gone by the end
            if (p.Form != 0) formSeen = p.Form;
            minionsMax = Math.Max(minionsMax, World.Minions.Count);
            if (c == 0)
            {
                res1 = p.Resource; pips1 = p.Pips;
                if (p.Cds.TryGetValue(id, out var cd)) cdNote = $"{cd.Charges}/{cd.MaxCharges} left {F(cd.Left)}s of {F(cd.Max)}s";
            }
            // refill so three casts fit in the window (the first cast showed the real cost and cooldown)
            p.Resource = generator ? p.Resource : h.MaxResource;
            if (usesPips) p.Pips = h.MaxPips;
            if (p.Cds.TryGetValue(id, out var cdr)) { cdr.Charges = cdr.MaxCharges; cdr.Left = 0f; }
        }

        float tail = 0.9f + d.Delay + (d.ExtraStrikes > 0 ? 1.0f : 0f);
        if (d.Kind == SkillKind.Summon) tail = 5.5f;
        else if (d.Kind == SkillKind.Trap) tail = Mathf.Max(tail, 2.2f);
        else if (d.Kind == SkillKind.Zone || d.Leave != null || d.PulseCoef > 0f || d.Rain > 0f) tail = Mathf.Max(tail, Mathf.Min(3.2f, Mathf.Max(d.Duration, d.Leave?.Dur ?? 0f)));
        if (d.OriginBurst > 0f) tail = Mathf.Max(tail, d.OriginDelay + 0.6f);
        if (d.OnHit.Any(o => o.Id is StatusId.Burn or StatusId.Poison or StatusId.Bleed or StatusId.Rot)) tail = Mathf.Max(tail, 1.4f);
        yield return tail;
        minionsMax = Math.Max(minionsMax, World.Minions.Count);
        int minionHitsNow = _minionHits;

        // buffs the skill left behind must run out (auras stay until replaced)
        var gained = h.Buffs.Where(b => !buffsBefore.Contains(b.Def.Id) && b.Def.Id != "test_god").ToList();
        string buffNote = gained.Count == 0 ? (gainedIds.Count == 0 ? "-" : string.Join(",", gainedIds) + "(ended)") : string.Join(",", gained.Select(b => $"{b.Def.Id}{(b.Def.Aura ? "(aura)" : $":{F(b.Left)}s")}"));
        string expired = "-";
        if (gained.Any(b => !b.Def.Aura))
        {
            foreach (var b in gained) if (!b.Def.Aura) b.Left = Mathf.Min(b.Left, 0.15f);
            yield return 0.5f;
            var stuck = gained.Where(b => !b.Def.Aura && h.Buffs.Contains(b)).Select(b => b.Def.Id).ToList();
            expired = stuck.Count == 0 ? "ok" : "STUCK " + string.Join(",", stuck);
        }

        int fired = p.Fired - fired0, ticks = p.ChanTicks - chan0, errs = Log.ErrorCount - err0;
        var problems = new List<string>();
        if (errs > 0) problems.Add($"{errs} error(s) logged");
        if (accepted == 0) problems.Add($"never castable ({refuse})");
        if (d.Kind == SkillKind.Channel) { if (ticks == 0) problems.Add("channel never ticked"); }
        else if (fired == 0) problems.Add("never reached its contact frame");
        bool direct = d.Coef > 0f && d.Kind is not (SkillKind.Buff or SkillKind.Summon);
        bool indirect = d.Kind == SkillKind.Buff && (d.PulseCoef > 0f || d.CastBurst > 0f || d.Rain > 0f || (d.Leave != null && d.Leave.Coef > 0f));
        if (direct && _ownHits == 0) problems.Add(_hits == 0 ? "dealt no damage" : "no hit carried its skill id");
        if (indirect && _hits == 0) problems.Add("its damaging part never hit");
        if (d.Kind == SkillKind.Summon)
        {
            if (minionsMax == 0) problems.Add("no minion spawned");
            else if (d.Coef > 0f && minionHitsNow == 0) problems.Add("minions never dealt damage");
        }
        if (d.Kind is SkillKind.Dash or SkillKind.Leap or SkillKind.Blink && moved < 1f) problems.Add($"did not move the hero ({F(moved)} m)");
        if (h.Class == ClassId.Wildsoul && d.Form is 1 or 2 && formSeen != d.Form) problems.Add($"form {formSeen}, expected {d.Form}");
        if (d.Kind == SkillKind.Buff && d.Duration > 0f && !gainedIds.Contains(id + "_t") && !gained.Any(b => b.Def.Id == id + "_t") && !buffsBefore.Contains(id + "_t")) problems.Add("no buff marker after casting");
        if (expired.StartsWith("STUCK")) problems.Add("buffs did not expire: " + expired);
        if (accepted > 0 && fired > 0)
        {
            bool refunds = d.GainOnHit > 0f || d.GainOnCrit > 0f || d.GainOnKill > 0f;       // hits that pay the cost back hide it from a before/after reading
            if (d.Cost > 0f && !(res1 < res0 - 0.01f) && !d.Channel() && !refunds) problems.Add($"cost {F(d.Cost)} not paid ({F(res0)}→{F(res1)})");
            if (generator && !(res1 > res0 + 0.01f) && _hits > 0) problems.Add($"generated nothing ({F(res0)}→{F(res1)})");
            if (d.PipSpend && pips1 >= pips0 && pips0 > 0) problems.Add($"pips not spent ({pips0}→{pips1})");
            if (d.PipReq > 0 && pips1 > pips0 - d.PipReq && !h.BuffFlag(b => b.FreePips)) problems.Add($"pip requirement not consumed ({pips0}→{pips1})");
            if (d.PipGain > 0 && !usesPips && h.MaxPips > 0 && p.Pips <= 0 && _hits > 0) problems.Add("gained no pips");
            if (d.Cooldown > 0f && cdNote == "-") problems.Add("no cooldown entry");
        }

        string line = $"skill {tag} {id} [{d.Kind}/{d.Cat}] {variant} r{rank} spec={spec ?? "-"} | casts={accepted}/{casts} fired={fired} ticks={ticks} hits={_hits} own={_ownHits} minionHits={minionHitsNow} dot={_dotHits} " +
            $"dmg={_dmg:0} kills={_kills} | res {F(res0)}→{F(res1)} (cost {F(d.Cost)}) pips {pips0}→{pips1} cd={cdNote} | minions={minionsMax} form={formSeen} moved={F(moved)} buffs={buffNote} expired={expired}";
        if (problems.Count == 0) Pass(line);
        else Fail(line + " | " + string.Join("; ", problems));
        _curSkill = null;
    }

    // ═════════════════════════ damage output per class and level ═════════════════════════

    /// <summary>
    /// --suite=dps[:classes[:levels]]: the bot's own rotation against one target dummy for 30 s, then against a pack of
    /// six for 20 s, with the auto build and rare gear of that level. Reports damage per second next to what the
    /// monster curve assumes (Balance.ExpectedDps) and the time-to-kill that follows for normal, elite and boss life,
    /// plus how many ordinary monster hits the hero can take.
    /// </summary>
    private IEnumerator DpsSuite()
    {
        var classes = new List<ClassId>();
        var levels = new List<int> { 5, 15, 25, 35, 50 };
        var parts = _arg.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length > 0 && parts[0].Length > 0 && parts[0] != "all")
            foreach (var key in parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int idx = Array.IndexOf(Names.ClassKeys, key.ToLowerInvariant());
                if (idx >= 0) classes.Add((ClassId)idx);
            }
        if (classes.Count == 0) for (int i = 0; i < Names.ClassCount; i++) classes.Add((ClassId)i);
        if (parts.Length > 1 && parts[1].Length > 0)
        {
            levels.Clear();
            foreach (var l in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (int.TryParse(l, out int lv)) levels.Add(Math.Clamp(lv, 1, Balance.LevelCap));
        }
        if (_bot == null) { Fail("dps: no bot to drive the rotation"); yield break; }

        foreach (int level in levels)
        {
            var ratios = new List<float>();
            foreach (var cls in classes)
            {
                _section = $"dps {Names.ClassKey(cls)} lv{level}";
                yield return StartHero(cls, level, "town");
                var h = G.Hero; var p = G.Player;
                if (h == null || h.Class != cls) { Fail($"{_section}: hero not created"); continue; }
                ClearField();
                Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint);
                float[] dps = new float[2];
                string share = "";
                for (int pass = 0; pass < 2; pass++)
                {
                    ClearField();
                    p.CancelAction(); p.SetForm(0); p.Teleport(arena); p.Cds.Clear();
                    h.Buffs.RemoveAll(b => b.Def.Id != "test_god"); h.Recalc();
                    p.Resource = Names.IsBuilder(cls) ? 0f : h.MaxResource; p.Pips = 0;
                    yield return null;
                    var targets = new List<Monster>();
                    int n = pass == 0 ? 1 : 6;
                    for (int i = 0; i < n; i++)
                        targets.Add(Dummy("cultist", level, arena + (n == 1 ? new Vector2(2.4f, 0f) : Vector2.Right.Rotated(i * Mathf.Tau / n) * 3f), 5e8f));
                    _focusTarget = targets[0]; _focusDmg = 0f; _dmg = 0f; _hits = 0; _minionDmg = 0f; _otherDmg = 0f;
                    if (pass == 0) _bot.ResetStats();
                    double t0 = G.Time, window = pass == 0 ? 30.0 : 20.0;
                    while (G.Time - t0 < window)
                    {
                        Monster t = null; float bd = float.MaxValue;
                        foreach (var m in targets) { if (m == null || !GodotObject.IsInstanceValid(m) || m.Dead) continue; float d2 = m.Pos2.DistanceSquaredTo(p.Pos2); if (d2 < bd) { bd = d2; t = m; } }
                        if (t == null) break;
                        _bot.FightStep(pass == 0 ? targets[0] : t);
                        yield return 0.12f;
                    }
                    _bot.FightStop();
                    float secs = (float)Math.Max(1.0, G.Time - t0);
                    dps[pass] = (pass == 0 ? _focusDmg : _dmg) / secs;
                    if (pass == 0) _bot.Stats($"dps-single {Names.ClassKey(cls)} lv{level}");
                    if (pass == 0) share = $"minions {(_dmg > 0 ? _minionDmg / _dmg : 0):P0}, thorns/other {(_dmg > 0 ? _otherDmg / _dmg : 0):P0}";
                    _focusTarget = null;
                }
                ClearField();
                float expected = Balance.ExpectedDps(level), hp = Balance.MonsterHp(level);
                float raw = Balance.MonsterDmg(level);
                float mit = (1f - Balance.ArmorDr(h.Armor, level)) * (1f - h.Res(Element.Physical) / 100f) * (1f - h.S[Stat.DmgReduction] / 100f);
                float hit = raw * mit;
                ratios.Add(dps[0] / expected);
                Log.Info("suite", $"dps {Names.ClassKey(cls)} lv{level}: single {dps[0]:0}/s = {dps[0] / expected:0.00}× expected ({expected:0}); pack of six {dps[1]:0}/s total ({dps[1] / 6f:0}/s each) | " +
                    $"monster life {hp:0}: ttk normal {hp / Math.Max(1f, dps[0]):0.0}s, in a pack {hp * 6f / Math.Max(1f, dps[1]):0.0}s for six, elite(×5.5) {hp * 5.5f / Math.Max(1f, dps[0]):0.0}s, boss(×45) {hp * 45f / Math.Max(1f, dps[0]):0}s | " +
                    $"of the single-target damage: {share} | hero life {h.MaxLife:0} armor {h.Armor:0}: a normal hit {raw:0} lands for {hit:0} ({hit / h.MaxLife:P0} of life, {h.MaxLife / Math.Max(1f, hit):0.0} hits to die) | bar=[{string.Join(",", h.D.Bar.Select(b => b ?? "-"))}]");
            }
            if (ratios.Count > 0)
            {
                ratios.Sort();
                Log.Info("suite", $"dps lv{level}: single-target damage vs Balance.ExpectedDps — min {ratios[0]:0.00}× median {ratios[ratios.Count / 2]:0.00}× max {ratios[^1]:0.00}× over {ratios.Count} classes");
            }
        }
    }

    // ═════════════════════════ systems ═════════════════════════

    private IEnumerator SystemsSuite()
    {
        _section = "systems: items";
        yield return StartHero(ClassId.Emberblade, 30, "town");
        var h = G.Hero; var d = h.D;
        var rng = new Random(12345);

        // ── town services: every person and fixture opens what it should ──
        _section = "systems: town services";
        {
            string[] panels = { "dialogue", "vendor", "map", "tier", "rift", "inventory", "quests", "skills", "paragon" };
            var opened = new List<string>();
            int svcBad = 0;
            foreach (var it in Interactable.All.ToArray())
            {
                if (it == null || !GodotObject.IsInstanceValid(it) || !it.CanUse || it.Item != null || it.AutoRadius > 0f) continue;
                string kind = it.Kind ?? "";
                if (kind is "breakable" or "chest" or "gate") continue;
                string who = kind == "npc" ? "npc:" + (it.Tag as string ?? "?") : kind;
                int errs = Log.ErrorCount;
                Log.Guard("ui", () => G.Ui?.CloseAll());
                G.Player.Teleport(World.Grid.NearestWalk(it.Pos2 + new Vector2(0, 1.6f)));
                yield return null;
                it.Use(G.Player);
                yield return 0.35f;
                string open = string.Join("+", panels.Where(id => G.Ui != null && G.Ui.IsOpen(id)));
                opened.Add($"{who}→{(open.Length > 0 ? open : "(nothing)")}");
                bool expectPanel = kind is "stash" or "tierstatue" or "waypoint" || kind == "npc" && (it.Tag as string) is "elder" or "smith" or "merchant" or "occultist" or "jeweler";
                if (Log.ErrorCount != errs || expectPanel && open.Length == 0) { svcBad++; Log.Warn("suite", $"town service {who}: panel='{open}' errors +{Log.ErrorCount - errs}"); }
                Log.Guard("ui", () => G.Ui?.CloseAll());
                yield return 0.1f;
            }
            G.PauseLocks = 0;
            Check(svcBad == 0 && opened.Count >= 8, "town: every person and fixture opens its panel", string.Join(" ", opened));
        }

        // ── item generation: every rarity × kind × class ──
        int made = 0, bad = 0;
        var perRarity = new int[5];
        foreach (Rarity r in Enum.GetValues<Rarity>())
            for (int ci = 0; ci < Names.ClassCount; ci++)
                foreach (ItemKind k in Enum.GetValues<ItemKind>())
                {
                    if (k == ItemKind.Gem) continue;
                    foreach (int power in new[] { 10, 150, 400, ItemDb.MaxPower })
                    {
                        Item it = null;
                        try { it = ItemDb.Generate(power, r, (ClassId)ci, k, rng); }
                        catch (Exception e) { bad++; if (bad <= 5) Log.Error("suite", $"Generate({power},{r},{(ClassId)ci},{k}) threw", e); continue; }
                        made++;
                        string why = null;
                        if (it == null) why = "null";
                        else if (string.IsNullOrEmpty(it.Name)) why = "no name";
                        // an off-hand asked for a class without off-hands comes back as a weapon (modular gear)
                        else if (it.Kind != k && !(k == ItemKind.OffHand && it.Kind == ItemKind.Weapon && ItemDb.OffBasesOf((ClassId)ci).Length == 0)) why = $"kind {it.Kind}";
                        else if (it.Kind is ItemKind.Weapon or ItemKind.OffHand && (it.Gb == null || it.Look == null)) why = $"no base/look ({it.Base}, {it.Look})";
                        else if (k == ItemKind.Weapon && (it.DmgMax <= 0 || it.DmgMin > it.DmgMax)) why = $"weapon damage {it.DmgMin}-{it.DmgMax}";
                        else if (it.Rarity >= Rarity.Magic && it.Affixes.Count == 0) why = "no affixes";
                        else if (it.Rarity == Rarity.Legendary && it.Aspect == null) why = "legendary without a power";
                        else if (it.Rarity == Rarity.Unique && it.Unique == null) why = "unique without an identity";
                        else if (it.Affixes.Any(a => a.Value == 0f || float.IsNaN(a.Value))) why = "zero-valued affix";
                        else if (it.Affixes.GroupBy(a => a.Stat).Any(g2 => g2.Count() > 1)) why = "duplicate affix stat";
                        if (it != null) perRarity[(int)it.Rarity]++;
                        if (why == null && it != null)
                        {
                            try { if (string.IsNullOrEmpty(ItemText.Tooltip(it, null, 50))) why = "empty tooltip"; }
                            catch (Exception e) { why = "tooltip threw " + e.GetType().Name + ": " + e.Message; }
                        }
                        if (why != null) { bad++; if (bad <= 12) Log.Warn("suite", $"bad item: power={power} asked={r} class={(ClassId)ci} kind={k} got={it?.Rarity} '{it?.Name}': {why}"); }
                    }
                }
        Check(bad == 0, $"items: {made} generated across rarity × class × kind × power", $"bad={bad} got rarity counts C/M/R/L/U={string.Join("/", perRarity)}");
        yield return null;

        // ── drop tables ──
        _section = "systems: loot rolls";
        foreach (int q in new[] { 0, 1, 2, 3 })
        {
            var counts = new int[5];
            for (int i = 0; i < 2000; i++) { var it = Loot.Roll(30, q, h.Class); if (it != null && it.IsEquip) counts[(int)it.Rarity]++; }
            Log.Info("suite", $"loot roll level 30 quality {q} ×2000: common={counts[0]} magic={counts[1]} rare={counts[2]} legendary={counts[3]} unique={counts[4]}");
            Check(counts.Sum() > 1500, $"loot: quality {q} rolls produce equipment");
        }
        yield return null;

        // ── equip / unequip ──
        _section = "systems: equip";
        d.Bag.Clear();
        d.Gold = 5_000_000; d.Scrap = 5000; d.Dust = 5000; d.Cores = 500;
        // (modular gear) a two-hand base of the class, so it lands in the main hand; a wand no emberblade can hold
        var sword = ItemDb.Generate(300, Rarity.Rare, h.Class, ItemKind.Weapon, rng, ItemDb.Loadout(h.Class).TwoHand[0]); sword.ReqLevel = 1;
        var staff = ItemDb.Generate(300, Rarity.Rare, ClassId.Astromancer, ItemKind.Weapon, rng, "wand"); staff.ReqLevel = 1;
        var high = ItemDb.Generate(600, Rarity.Rare, h.Class, ItemKind.Helm, rng); high.ReqLevel = 50;
        h.AddItem(sword); h.AddItem(staff); h.AddItem(high);
        float wBefore = h.WeaponMax;
        int bagExpect = 3 + (h.OffHand != null ? 1 : 0);        // a two-hand weapon also sends the off hand's piece to the bag
        Check(h.Equip(sword) && h.Weapon == sword && d.Bag.Count == bagExpect && h.OffHand == null, "equip: weapon swaps with the old one in the bag", $"weaponMax {wBefore:0}→{h.WeaponMax:0} bag={d.Bag.Count}");
        Check(h.WhyCantEquip(staff) != null && !h.Equip(staff), "equip: another class's weapon is refused", h.WhyCantEquip(staff));
        Check(h.WhyCantEquip(high) != null, "equip: level requirement is enforced", h.WhyCantEquip(high));
        var ring1 = ItemDb.Generate(200, Rarity.Rare, h.Class, ItemKind.Ring, rng); ring1.ReqLevel = 1;
        var ring2 = ItemDb.Generate(250, Rarity.Rare, h.Class, ItemKind.Ring, rng); ring2.ReqLevel = 1;
        d.Equip[(int)EquipSlot.Ring1] = null; d.Equip[(int)EquipSlot.Ring2] = null;
        h.AddItem(ring1); h.AddItem(ring2);
        Check(h.Equip(ring1) && h.Equip(ring2) && d.Equip[(int)EquipSlot.Ring1] == ring1 && d.Equip[(int)EquipSlot.Ring2] == ring2, "equip: two rings fill both ring slots");
        int bagN = d.Bag.Count;
        Check(h.Unequip(EquipSlot.Ring2) && d.Bag.Count == bagN + 1 && d.Equip[(int)EquipSlot.Ring2] == null, "equip: unequip returns the ring to the bag");
        yield return null;

        // ── salvage → codex ──
        _section = "systems: salvage";
        var leg = ItemDb.Generate(300, Rarity.Legendary, h.Class, ItemKind.Chest, rng);
        h.AddItem(leg);
        int mats0 = d.Scrap + d.Dust + d.Cores; string aspect = leg.Aspect;
        d.Codex.Remove(aspect ?? "");
        var sum = h.SalvageMany(new[] { leg });
        Check(sum.Count == 1 && d.Scrap + d.Dust + d.Cores > mats0 && aspect != null && d.Codex.ContainsKey(aspect) && !d.Bag.Contains(leg),
            "salvage: a legendary yields materials and records its power in the codex", $"aspect={aspect} scrap+{sum.Scrap} dust+{sum.Dust} cores+{sum.Cores} powers=[{string.Join(",", sum.Powers)}]");
        Check(h.WhyCantSalvage(h.Weapon) != null, "salvage: worn gear is refused", h.WhyCantSalvage(h.Weapon));
        yield return null;

        // ── masterworking ──
        _section = "systems: masterwork";
        var mw = ItemDb.Generate(300, Rarity.Legendary, h.Class, ItemKind.Weapon, rng); mw.ReqLevel = 1;
        h.AddItem(mw); h.Equip(mw);
        float dmg0 = h.WeaponMax; int ups = 0;
        while (h.Upgrade(mw)) { ups++; if (ups > 40) break; }
        int boosts = mw.Affixes.Sum(a => Crafting.Boosts(a));
        Check(ups == ItemDb.MaxUpgrade && mw.Upgrade == ItemDb.MaxUpgrade && h.WeaponMax > dmg0 && boosts == 3, "masterwork: twelve ranks, three milestone boosts, weapon damage grows",
            $"ups={ups} boosts={boosts} weaponMax {dmg0:0}→{h.WeaponMax:0} then refused: {h.WhyCantUpgrade(mw)}");
        yield return null;

        // ── tempering ──
        _section = "systems: temper";
        var recipes = Crafting.RecipesFor(h.Class);
        Check(recipes.Count > 0, $"temper: {recipes.Count} recipes for {Names.ClassKey(h.Class)}");
        int tempered = 0, refused = 0;
        foreach (ItemKind k in new[] { ItemKind.Weapon, ItemKind.Chest, ItemKind.Ring, ItemKind.Boots })
        {
            foreach (var rar in new[] { Rarity.Rare, Rarity.Legendary })
            {
                var it = ItemDb.Generate(300, rar, h.Class, k, rng);
                h.AddItem(it);
                int slots = Crafting.TemperSlots(it);
                for (int sl = 0; sl < slots; sl++)
                {
                    var r = recipes.FirstOrDefault(x => x.Allows(k) && h.WhyCantTemper(it, x.Id, sl) == null);
                    if (r == null) { refused++; Log.Warn("suite", $"temper: no recipe fits {k} {rar} slot {sl}: {string.Join(" | ", recipes.Select(x => x.Id + "=" + h.WhyCantTemper(it, x.Id, sl)))}"); continue; }
                    int n0 = it.Affixes.Count, left0 = it.TemperLeft;
                    var a = h.Temper(it, r.Id, sl);
                    if (a != null && a.Tempered && it.Affixes.Contains(a) && it.TemperLeft == left0 - 1 && it.Affixes.Count == n0 + 1) tempered++; else refused++;
                    // tempering the same slot again replaces, never adds
                    var again = h.Temper(it, r.Id, sl);
                    if (again != null && it.Affixes.Count != n0 + 1) { refused++; Log.Warn("suite", $"temper: re-tempering slot {sl} changed the affix count to {it.Affixes.Count}"); }
                }
                Check(h.WhyCantTemper(it, recipes[0].Id, slots) != null, $"temper: {rar} {k} refuses slot {slots}", h.WhyCantTemper(it, recipes[0].Id, slots));
                var any = recipes.FirstOrDefault(x => x.Allows(k));
                while (any != null && it.TemperLeft > 0 && h.Temper(it, any.Id, 0) != null) { }
                if (any != null) Check(it.TemperLeft == 0 && h.WhyCantTemper(it, any.Id, 0) != null, $"temper: {rar} {k} runs out of charges", h.WhyCantTemper(it, any.Id, 0));
                d.Bag.Remove(it);
            }
        }
        Check(tempered >= 10 && refused == 0, "temper: rare takes one affix, legendary two, charges are spent", $"tempered={tempered} refused={refused}");
        var uniq = ItemDb.Generate(300, Rarity.Unique, h.Class, ItemKind.Weapon, rng);
        h.AddItem(uniq);
        Check(h.WhyCantTemper(uniq, recipes[0].Id, 0) != null && h.WhyCantEnchant(uniq) != null, "temper/enchant: uniques are refused", h.WhyCantTemper(uniq, recipes[0].Id, 0));
        d.Bag.Remove(uniq);
        yield return null;

        // ── enchanting ──
        _section = "systems: enchant";
        var en = ItemDb.Generate(300, Rarity.Rare, h.Class, ItemKind.Gloves, rng);
        h.AddItem(en);
        int cost1 = h.EnchantCost(en); long gold0 = d.Gold;
        var cands = h.BeginEnchant(en, 0);
        bool rolled = cands != null && cands.Length >= 1;
        var oldStat = en.Affixes[0].Stat;
        if (rolled) h.CommitEnchant(en, 0, cands[0]);
        Check(rolled && d.Gold == gold0 - cost1 && h.EnchantCost(en) > cost1 && en.Affixes[0] == cands[0], "enchant: pays, offers candidates, applies the pick, next one costs more",
            $"cost {cost1}→{h.EnchantCost(en)} {oldStat}→{en.Affixes[0].Stat} candidates={cands?.Length ?? 0}");
        yield return null;

        // ── imprinting ──
        _section = "systems: imprint";
        var asp = ItemDb.Aspects.FirstOrDefault(a => a.Class < 0 || a.Class == (int)h.Class);
        var target = asp == null ? null : ItemDb.Generate(300, Rarity.Rare, h.Class, asp.Kinds[0], rng);
        if (target != null)
        {
            h.AddItem(target);
            d.Codex.Remove(asp.Id);
            Check(h.WhyCantImprint(target, asp.Id) != null, "imprint: a power that is not in the codex is refused", h.WhyCantImprint(target, asp.Id));
            d.Codex[asp.Id] = 0.8f;
            bool ok = h.Imprint(target, asp.Id);
            Check(ok && target.Rarity == Rarity.Legendary && target.Aspect == asp.Id && Mathf.IsEqualApprox(target.AspectRoll, 0.8f), "imprint: a rare becomes legendary with the codex power", $"'{target.Name}' aspect={target.Aspect}");
            target.ReqLevel = 1;
            h.Equip(target);
            Check(h.HasAsp(asp.Id), "imprint: the imprinted power is active when worn", $"{asp.Id}={h.Asp(asp.Id)}");
        }
        else Fail("imprint: no aspect available to test with");
        yield return null;

        // ── sockets and gems ──
        _section = "systems: gems";
        var sock = ItemDb.Generate(300, Rarity.Rare, h.Class, ItemKind.Chest, rng); sock.ReqLevel = 1; sock.Sockets = 0;
        h.AddItem(sock); h.Equip(sock);
        int added = 0; while (h.WhyCantAddSocket(sock) == null && h.AddSocket(sock)) { added++; if (added > 5) break; }
        Check(added == Hero.MaxSockets(ItemKind.Chest) && sock.Sockets == added, "sockets: a chest takes two sockets, then refuses", $"added={added} then: {h.WhyCantAddSocket(sock)}");
        var boots = ItemDb.Generate(300, Rarity.Rare, h.Class, ItemKind.Boots, rng); h.AddItem(boots);
        Check(h.WhyCantAddSocket(boots) != null, "sockets: boots cannot be socketed", h.WhyCantAddSocket(boots));
        d.Bag.Remove(boots);
        var gdef = ItemDb.Gems.First(x => x.Tier == 1);
        for (int i = 0; i < 4; i++) h.AddItem(ItemDb.MakeGem(gdef.Id));
        var gem = d.Bag.First(x => x.Kind == ItemKind.Gem);
        var bonus = ItemDb.GemBonus(gdef, ItemKind.Chest);
        float statBefore = h.S[bonus.s];
        bool socketed = h.WhyCantSocket(gem, sock) == null && h.Socket(gem, sock);
        Check(socketed && sock.Gems.Count == 1 && h.S[bonus.s] > statBefore, "gems: socketing a gem adds its bonus", $"{gdef.Id} on chest: {bonus.s} {statBefore:0.#}→{h.S[bonus.s]:0.#}");
        int gemsInBag = h.GemCount(gdef.Id);
        var fused = h.CombineGems(gdef.Id);
        Check(gemsInBag == 3 && fused != null && h.GemCount(gdef.Id) == 0 && ItemDb.Gem(fused.GemId)?.Tier == 2, "gems: three gems combine into the next tier", $"had {gemsInBag} → {fused?.GemId}");
        Check(h.Unsocket(sock) && sock.Gems.Count == 0 && h.GemCount(gdef.Id) == 1 && Mathf.IsEqualApprox(h.S[bonus.s], statBefore), "gems: unsocketing returns the gem and removes the bonus");
        yield return null;

        // ── stash ──
        _section = "systems: stash";
        d.Stash.Clear();
        var st = ItemDb.Generate(100, Rarity.Magic, h.Class, ItemKind.Helm, rng); h.AddItem(st);
        Check(h.StashItem(st) && d.Stash.Contains(st) && !d.Bag.Contains(st), "stash: bag → stash");
        Check(h.FromStash(st) && d.Bag.Contains(st) && !d.Stash.Contains(st), "stash: stash → bag");
        for (int i = 0; i < Hero.StashSize; i++) d.Stash.Add(ItemDb.Generate(50, Rarity.Common, h.Class, ItemKind.Ring, rng));
        Check(h.WhyCantStash(st) != null && !h.StashItem(st), "stash: a full stash refuses", h.WhyCantStash(st));
        d.Stash.Clear();
        yield return null;

        // ── vendors ──
        _section = "systems: vendors";
        d.Bag.Clear();
        foreach (var npc in new[] { "merchant", "smith", "occultist", "jeweler" })
        {
            List<(Item item, int price)> stock = null;
            try { stock = Loot.VendorStock(npc, h); }
            catch (Exception e) { Log.Error("suite", $"VendorStock({npc}) threw", e); }
            int n = stock?.Count ?? -1;
            if (n > 0)
            {
                var (item, price) = stock[0];
                long g0 = d.Gold; int b0 = d.Bag.Count;
                bool bought = h.Buy(item, price);
                Check(bought && d.Gold == g0 - price && d.Bag.Count == b0 + 1 && price > 0, $"vendor {npc}: {n} items, buying the first one", $"'{item.Name}' {item.Rarity} for {price}");
                long g1 = d.Gold;
                Check(h.SellItem(item) && d.Gold == g1 + item.Value && item.Value > 0 && item.Value < price, $"vendor {npc}: selling it back pays less than it cost", $"sell {item.Value} < buy {price}");
            }
            else Log.Info("suite", $"vendor {npc}: stock={n} (a pure service vendor has none)");
        }
        d.Gold = 10;
        var poor = Loot.VendorStock("merchant", h);
        if (poor != null && poor.Count > 0) Check(!h.Buy(poor[0].item, poor[0].price) && h.WhyCantBuy(poor[0].price) != null, "vendor: no gold, no sale", h.WhyCantBuy(poor[0].price));
        d.Gold = 5_000_000;
        int gambled = 0; var gr = new int[5];
        foreach (ItemKind k in Enum.GetValues<ItemKind>())
        {
            if (k == ItemKind.Gem || (k == ItemKind.OffHand && ItemDb.OffBasesOf(h.Class).Length == 0)) continue;     // (modular gear) a class without off-hands is never offered one
            for (int i = 0; i < 12; i++)
            {
                long g0 = d.Gold; int price = Loot.GamblePrice(k, h);
                var (item, paid) = Loot.Gamble(k, h);
                if (item == null) { Log.Warn("suite", $"gamble {k}: nothing returned (price {price})"); continue; }
                if (item.Kind != k) Fail($"gamble {k}: got a {item.Kind}");
                bool bought = h.Buy(item, paid);          // Gamble only quotes; the caller pays
                if (!bought || d.Gold != g0 - paid || paid != price || paid <= 0) Fail($"gamble {k}: bought={bought} gold {g0}→{d.Gold} but price {price}, paid {paid}");
                gambled++; gr[(int)item.Rarity]++;
                d.Bag.Remove(item);
            }
        }
        int kindsWanted = Enum.GetValues<ItemKind>().Count(k => k != ItemKind.Gem && !(k == ItemKind.OffHand && ItemDb.OffBasesOf(h.Class).Length == 0));
        Check(gambled == kindsWanted * 12, "gamble: every equipment kind can be gambled for", $"{gambled} items, rarities C/M/R/L/U={string.Join("/", gr)}");
        yield return null;

        // ── bulk sell / junk ──
        _section = "systems: junk";
        d.Bag.Clear();
        for (int i = 0; i < 10; i++) h.AddItem(ItemDb.Generate(100, Rarity.Common, h.Class, null, rng));
        foreach (var it in d.Bag.Take(6).ToList()) h.ToggleJunk(it);
        var junk = h.JunkItems();
        var sold = h.SellMany(junk);
        Check(junk.Count == 6 && sold.count == 6 && d.Bag.Count == 4 && h.JunkItems().Count == 0, "junk: marked items sell in one go", $"sold {sold.count} for {sold.gold}");
        h.SortBag();
        for (int i = d.Bag.Count; i < Hero.BagSize; i++) h.AddItem(ItemDb.Generate(100, Rarity.Common, h.Class, null, rng));
        Check(h.BagFull && !h.AddItem(ItemDb.Generate(100, Rarity.Common, h.Class, null, rng)) && d.Bag.Count == Hero.BagSize, "bag: a full bag refuses more items");
        d.Bag.Clear(); h.Notify();
        yield return null;

        // ── skill points, upgrades, respec, spec ──
        _section = "systems: skills";
        foreach (ClassId cls in Enum.GetValues<ClassId>())
        {
            yield return StartHero(cls, 50, "town", gear: false);
            h = G.Hero; d = h.D;
            string tag = Names.ClassKey(cls);
            var skills = SkillDb.Of(cls).ToList(); var passives = SkillDb.PassivesOf(cls).ToList(); var specs = SkillDb.SpecsOf(cls).ToList();
            int cats = skills.Select(s => s.Cat).Distinct().Count();
            Check(skills.Count == 8 && cats == 6 && specs.Count == 3 && passives.Count(x => x.Key) >= 1, $"skill data {tag}: 8 skills in 6 categories, 3 specs, key passives",
                $"skills={skills.Count} cats={cats} passives={passives.Count} keys={passives.Count(x => x.Key)} specs={specs.Count} unlocks=[{string.Join(",", skills.Select(s => s.UnlockLevel))}]");
            foreach (var s in skills)
            {
                var problems = new List<string>();
                if (string.IsNullOrEmpty(s.Name) || string.IsNullOrEmpty(s.Desc)) problems.Add("no name/description");
                if (s.Enh == null || s.A == null || s.B == null) problems.Add("missing upgrades");
                else foreach (var u in s.Ups) if (string.IsNullOrEmpty(u.Name) || string.IsNullOrEmpty(u.Desc) || u.Mod == null) problems.Add($"upgrade {u.Id} incomplete");
                if (string.IsNullOrEmpty(s.Icon)) problems.Add("no icon");
                if (problems.Count > 0) Fail($"skill data {tag} {s.Id}: {string.Join("; ", problems)}");
            }
            int total = d.Level - 1 + d.BonusSkillPts;
            h.RespecAll();
            Check(h.SkillPoints == total && d.Skills.Count == 1 && d.Bar.Count(b => b != null) == 1, $"respec {tag}: every point returns, the basic skill stays", $"points={h.SkillPoints}/{total}");
            int learned = 0;
            foreach (var s in skills) while (h.Learn(s.Id)) learned++;
            bool allMax = skills.All(s => h.BaseRank(s.Id) == SkillDef.MaxRank || h.SkillPoints == 0);
            Check(allMax && h.SkillPoints >= 0 && h.SkillPoints == total - h.SkillPointsSpent, $"learn {tag}: ranks up to {SkillDef.MaxRank}, points never negative", $"learned={learned} left={h.SkillPoints} bar=[{string.Join(",", d.Bar.Select(b => b ?? "-"))}]");
            h.RespecAll();
            var first = skills.First(s => s.Cat == SkillCat.Core);
            h.Learn(first.Id);
            Check(!h.TakeUp(first.Id, first.A.Id), $"upgrades {tag}: a branch needs the enhancement first");
            bool e1 = h.TakeUp(first.Id, first.Enh.Id), a1 = h.TakeUp(first.Id, first.A.Id);
            int ptsAfterA = h.SkillPoints;
            bool b1 = h.TakeUp(first.Id, first.B.Id);
            Check(e1 && a1 && b1 && h.HasUp(first.B.Id) && !h.HasUp(first.A.Id) && h.SkillPoints == ptsAfterA, $"upgrades {tag}: enhancement, branch A, then B replaces A for free");
            Check(h.Unlearn(first.Id) && h.BaseRank(first.Id) == 0 && !h.HasUp(first.Enh.Id) && !d.Bar.Contains(first.Id), $"unlearn {tag}: the skill, its upgrades and its bar slot are cleared");
            Check(!h.Unlearn(SkillDb.DefaultBasic(cls)), $"unlearn {tag}: the last basic skill cannot be unlearned");
            var keys = passives.Where(x => x.Key).ToList();
            if (keys.Count >= 2)
            {
                bool k0 = h.Learn(keys[0].Id), k1 = h.Learn(keys[1].Id);
                Check(k0 && k1 && !h.HasKey(keys[0].Id) && h.HasKey(keys[1].Id), $"key passive {tag}: choosing another replaces the first");
            }
            int passiveRanks = 0;
            foreach (var pv in passives.Where(x => !x.Key)) while (h.Learn(pv.Id)) passiveRanks++;
            Check(passiveRanks > 0 && h.SkillPoints >= 0, $"passives {tag}: ranks can be bought", $"ranks={passiveRanks} left={h.SkillPoints}");
            foreach (var sp in specs)
            {
                bool ok = h.SetSpec(sp.Id);
                var eff = skills.Select(s => h.Eff(s.Id)).ToList();
                Check(ok && d.Spec == sp.Id && eff.All(x => x != null), $"spec {tag}: {sp.Id} ({sp.Name}) applies");
            }
            d.Level = 14; d.Spec = null; h.Recalc();
            Check(!h.SetSpec(specs[0].Id), $"spec {tag}: refused below level {SkillDb.SpecLevel}");
            d.Level = 50; h.RespecAll();
            Game.AutoBuild(h);
            Check(h.SkillPoints >= 0 && d.Bar.Count(b => b != null) == 6 && d.Bar.Distinct().Count() == 6, $"auto build {tag}: six different skills on the bar", $"bar=[{string.Join(",", d.Bar.Select(b => b ?? "-"))}] left={h.SkillPoints}");
        }

        // ── paragon ──
        _section = "systems: paragon";
        foreach (ClassId cls in new[] { ClassId.Emberblade, ClassId.Astromancer, ClassId.Pactbinder })
        {
            yield return StartHero(cls, 50, "town", gear: true);
            h = G.Hero; d = h.D;
            string tag = Names.ClassKey(cls);
            Check(h.ParagonUnlocked && h.ParagonPoints == 0, $"paragon {tag}: unlocked at the cap with no points yet", $"earned={h.ParagonEarned}");
            float life0 = h.MaxLife, mm0 = h.MainMul;
            d.Paragon = 60; h.InvalidateParagon(); h.Recalc();
            int pts = h.ParagonPoints;
            int spent = h.AutoAllocateParagon();
            Check(pts == 60 && spent > 0 && h.ParagonSpent == spent && h.ParagonPoints == pts - spent, $"paragon {tag}: automatic allocation spends board points", $"points={pts} spent={spent} left={h.ParagonPoints} life {life0:0}→{h.MaxLife:0} mainMul {mm0:0.000}→{h.MainMul:0.000} keystones=[{string.Join(",", h.Keystones)}]");
            Check(h.MainMul > mm0 || h.MaxLife > life0 || h.S[Stat.DmgAll] > 0, $"paragon {tag}: allocated nodes change the stats");
            var choices = h.ParagonBoardChoices();
            int attached = 0;
            for (int dir = 0; dir < 4 && attached == 0 && choices.Count > 0; dir++)
                if (h.ParagonGateOpen(0, dir) && h.WhyCantAttachParagonBoard(0, dir, choices[0].Id) == null && h.AttachParagonBoard(0, dir, choices[0].Id)) attached++;
            Log.Info("suite", $"paragon {tag}: board choices={choices.Count} attached={attached} (gates open: {string.Join(",", Enumerable.Range(0, 4).Select(i => h.ParagonGateOpen(0, i)))})");
            var glyphs = h.ParagonGlyphs;
            int sockets = 0, upgraded = 0;
            if (glyphs.Count > 0)
            {
                foreach (var gl in glyphs) { d.Glyphs ??= new(); if (!d.Glyphs.ContainsKey(gl.Id)) d.Glyphs[gl.Id] = 1; }
                h.InvalidateParagon(); h.Recalc();
                for (int b = 0; b < 3; b++)
                {
                    string why = h.WhyCantSocketGlyph(b, glyphs[0].Id);
                    if (why == null && h.SocketGlyph(b, glyphs[0].Id)) { sockets++; break; }
                }
                h.AddGlyphTries(10, 6);
                int lvl0 = h.GlyphLevel(glyphs[0].Id);
                for (int i = 0; i < 6; i++) { if (h.WhyCantUpgradeGlyph(glyphs[0].Id) != null) break; h.UpgradeGlyph(glyphs[0].Id); }
                upgraded = h.GlyphLevel(glyphs[0].Id) - lvl0;
            }
            Log.Info("suite", $"paragon {tag}: glyphs={glyphs.Count} socketed={sockets} glyph level +{upgraded} tries left={h.GlyphTries}");
            Check(glyphs.Count > 0 && upgraded >= 0, $"paragon {tag}: glyph API usable");
            h.RefundAllParagon();
            Check(h.ParagonSpent == 0 && h.ParagonPoints == h.ParagonEarned, $"paragon {tag}: refunding everything returns all points", $"points={h.ParagonPoints}/{h.ParagonEarned}");
            h.AddXp(Balance.ParagonXp(d.Paragon) + 10);
            Check(d.Paragon == 61, $"paragon {tag}: experience past the cap raises the paragon level", $"paragon={d.Paragon}");
        }

        // ── world tiers ──
        _section = "systems: tiers";
        float hp0 = 0, dm0 = 0;
        for (int tier = 0; tier <= 3; tier++)
        {
            yield return StartHero(ClassId.Hunter, 50, "town", gear: true, god: true, tier: tier);
            var m = Dummy("skeleton", 50 + Balance.TierLevelBonus[tier], G.Player.Pos2 + new Vector2(4, 0), 0f);
            if (tier == 0) { hp0 = m.MaxLife; dm0 = m.BaseDamage; }
            Check(G.WorldTier == tier && (tier == 0 || (m.MaxLife > hp0 && m.BaseDamage > dm0)), $"world tier {tier} ({ZoneDb.TierNames[tier]}): monsters scale", $"skeleton life={m.MaxLife:0} dmg={m.BaseDamage:0.0} (tier 0: {hp0:0} / {dm0:0.0})");
            ClearField();
        }
    }

    // ═════════════════════════ world objects and loot ═════════════════════════

    private IEnumerator ObjectsSuite()
    {
        _section = "objects";
        string area = _name == "objects" && _arg.Length > 0 ? _arg : "wastes";
        yield return StartHero(ClassId.Lampknight, 20, area, gear: true, god: false);
        var h = G.Hero; var d = h.D; var p = G.Player;
        ClearField();
        Vector2 c = FindArena(World.Grid, G.Level.Checkpoint);
        p.Teleport(c);
        G.Cam?.Snap();
        yield return 0.3f;
        int err0 = Log.ErrorCount;

        // loot of every rarity, laid out in a row
        d.Bag.Clear();
        var drops = new List<Interactable>();
        var rng = new Random(7);
        int col = 0;
        foreach (Rarity r in Enum.GetValues<Rarity>())
        {
            var it = ItemDb.Generate(200, r, h.Class, r == Rarity.Unique ? ItemKind.Weapon : ItemKind.Helm, rng);
            var drop = Loot.DropItem(it, c + new Vector2(-4f + col++ * 2f, 3f));
            if (drop != null) drops.Add(drop);
        }
        var gemDrop = Loot.DropItem(ItemDb.RandomGem(20, rng), c + new Vector2(-4f + col * 2f, 3f));
        if (gemDrop != null) drops.Add(gemDrop);
        yield return 1.6f;
        Check(drops.Count == 6 && drops.All(x => GodotObject.IsInstanceValid(x) && x.Item != null && x.CanUse), "loot: one drop per rarity plus a gem lies on the ground",
            string.Join(", ", drops.Where(GodotObject.IsInstanceValid).Select(x => $"{x.Item.Rarity}:'{x.Label}'")));
        string shot = G.ArgValue("shot");
        if (!string.IsNullOrEmpty(shot)) { G.HarnessShot(shot.Replace(".png", "") + "_loot.png"); yield return 0.3f; }
        int picked = 0;
        foreach (var drop in drops)
        {
            if (!GodotObject.IsInstanceValid(drop)) continue;
            int b0 = d.Bag.Count;
            p.GoUse(drop);
            yield return Until(() => !GodotObject.IsInstanceValid(drop) || !drop.CanUse || d.Bag.Count > b0, 8, "loot pickup");
            if (d.Bag.Count > b0) picked++;
        }
        Check(picked == drops.Count, "loot: walking to a drop and using it puts it in the bag", $"picked {picked}/{drops.Count}, bag={d.Bag.Count}");

        // gold, globes, materials: walk over them
        long gold0 = d.Gold; int scrap0 = d.Scrap, dust0 = d.Dust, cores0 = d.Cores;
        p.Teleport(c);
        yield return 0.2f;
        Loot.DropGold(250, c + new Vector2(2.5f, -2f));
        Loot.DropMaterials(3, 2, 1, c + new Vector2(-2.5f, -2f));
        p.Life = p.MaxLife * 0.3f;
        Loot.DropGlobe(c + new Vector2(0f, -3f));
        yield return 1.2f;
        float lifeBefore = p.LifeFrac;
        p.SetMove(c + new Vector2(2.5f, -2f), false);
        yield return 1.5f;
        p.SetMove(c + new Vector2(0f, -3f), false);
        yield return 1.5f;
        p.SetMove(c + new Vector2(-2.5f, -2f), false);
        yield return 2.0f;
        Check(d.Gold > gold0, "loot: gold is picked up by walking near it", $"gold {gold0}→{d.Gold}");
        Check(d.Scrap > scrap0 && d.Dust > dust0 && d.Cores > cores0, "loot: materials are picked up", $"scrap {scrap0}→{d.Scrap} dust {dust0}→{d.Dust} cores {cores0}→{d.Cores}");
        Check(p.LifeFrac > lifeBefore + 0.05f, "loot: a health globe heals", $"life {lifeBefore:P0}→{p.LifeFrac:P0}");

        // chests of every quality
        p.Teleport(c);
        for (int q = 0; q <= 3; q++)
        {
            int objs0 = Interactable.All.Count;
            var chest = WorldObjects.Chest(c + new Vector2(3f, 0f), q);
            yield return 0.2f;
            p.GoUse(chest);
            yield return Until(() => !GodotObject.IsInstanceValid(chest) || !chest.CanUse, 6, $"chest {q} to open");
            yield return 1.5f;
            int spawned = Interactable.All.Count(x => GodotObject.IsInstanceValid(x) && (x.Item != null || x.AutoRadius > 0f));
            Check(!GodotObject.IsInstanceValid(chest) || !chest.CanUse, $"chest quality {q}: opens and spills loot", $"loose drops now={spawned} (objects {objs0}→{Interactable.All.Count})");
            if (GodotObject.IsInstanceValid(chest)) chest.QueueFree();
            ClearField();
            p.Teleport(c);
            yield return 0.1f;
        }

        // shrines
        for (int k = 0; k < WorldObjects.ShrineCount; k++)
        {
            var before = new HashSet<string>(h.Buffs.Select(b => b.Def.Id));
            var shrine = WorldObjects.Shrine(c + new Vector2(0f, 3f), k);
            string label = shrine.Label;
            yield return 0.1f;
            shrine.Use(p);
            yield return 0.4f;
            var got = h.Buffs.Where(b => !before.Contains(b.Def.Id)).Select(b => $"{b.Def.Id}:{b.Left:0}s").ToList();
            Check(!GodotObject.IsInstanceValid(shrine) || !shrine.CanUse, $"shrine {k} '{label}': usable once", got.Count > 0 ? "buffs " + string.Join(",", got) : "no new buff (instant effect)");
            if (GodotObject.IsInstanceValid(shrine)) shrine.QueueFree();
            ClearField(false);
        }

        // altar: +1 to every attribute, once
        float str0 = h.S[Stat.Str];
        string altarId = "altar_qa_" + area + "_0";
        d.Flags.RemoveAll(f => f.Contains("altar_qa_"));
        h.Recalc(); str0 = h.S[Stat.Str];
        var altar = WorldObjects.Altar(c + new Vector2(-3f, 0f), altarId);
        yield return 0.1f;
        altar.Use(p);
        yield return 0.5f;
        Check(h.S[Stat.Str] >= str0 + 1f && d.Flags.Any(f => f.Contains(altarId)), "altar: permanently raises the attributes and is remembered", $"str {str0:0}→{h.S[Stat.Str]:0} canUseAgain={(GodotObject.IsInstanceValid(altar) && altar.CanUse)}");
        var again = WorldObjects.Altar(c + new Vector2(-3f, 2f), altarId);
        Check(!again.CanUse, "altar: one that was already used is spent on the next visit");
        again.QueueFree();
        if (GodotObject.IsInstanceValid(altar)) altar.QueueFree();

        // breakables placed by the level
        var breakables = Interactable.All.Where(x => GodotObject.IsInstanceValid(x) && x.CanUse && (x.Kind ?? "").Contains("break", StringComparison.OrdinalIgnoreCase)).ToList();
        if (breakables.Count > 0)
        {
            var b = breakables.OrderBy(x => x.Pos2.DistanceSquaredTo(p.Pos2)).First();
            p.Teleport(World.Grid.NearestWalk(b.Pos2 + new Vector2(1.2f, 0f)));
            yield return 0.1f;
            b.Use(p);
            yield return 0.6f;
            Check(!GodotObject.IsInstanceValid(b) || !b.CanUse, $"breakable '{b.Kind}': breaks when used", $"{breakables.Count} in the level");
        }
        else Log.Info("suite", "objects: this level has no breakables");

        var kinds = Interactable.All.Where(GodotObject.IsInstanceValid).GroupBy(x => x.Kind ?? "").Select(g2 => $"{g2.Key}×{g2.Count()}");
        Log.Info("suite", $"objects: interactable kinds in {area}: {string.Join(" ", kinds)}");
        Check(Log.ErrorCount == err0, "objects: no errors logged while using world objects", $"errors +{Log.ErrorCount - err0}");
    }

    // ═════════════════════════ save / load ═════════════════════════

    private static readonly System.Text.Json.JsonSerializerOptions JsonOpt = new() { IncludeFields = true };

    private static string Canon(SaveData d)
    {
        string at = d.SavedAt; d.SavedAt = "";
        string s = System.Text.Json.JsonSerializer.Serialize(d, JsonOpt);
        d.SavedAt = at;
        return s;
    }

    private static void RestoreSaves(string dir, string backup)
    {
        foreach (var f in System.IO.Directory.GetFiles(dir))
        {
            string n = System.IO.Path.GetFileName(f);
            if (n != "slot0.json" && n != "slot0.bak") System.IO.File.Delete(f);
        }
        foreach (var f in System.IO.Directory.GetFiles(backup)) System.IO.File.Copy(f, System.IO.Path.Combine(dir, System.IO.Path.GetFileName(f)), true);
        System.IO.Directory.Delete(backup, true);
    }

    private IEnumerator SaveSuite()
    {
        _section = "save";
        string dir = ProjectSettings.GlobalizePath("user://saves");
        string backup = System.IO.Path.Combine(ProjectSettings.GlobalizePath("user://"), "saves_qa_backup");
        // the user's own slots are set aside and put back at the end
        System.IO.Directory.CreateDirectory(dir);
        // a backup left by a run that was killed holds the user's real slots: put them back before doing anything else
        if (System.IO.Directory.Exists(backup))
        {
            Log.Warn("suite", "save: found the backup of an interrupted run; restoring it first");
            RestoreSaves(dir, backup);
        }
        int lastSlot0 = G.Cfg.LastSlot;
        System.IO.Directory.CreateDirectory(backup);
        foreach (var f in System.IO.Directory.GetFiles(dir)) System.IO.File.Copy(f, System.IO.Path.Combine(backup, System.IO.Path.GetFileName(f)), true);
        Log.Info("suite", $"save: {System.IO.Directory.GetFiles(backup).Length} existing save files set aside in {backup}");
        var settingsBefore = System.IO.File.Exists(ProjectSettings.GlobalizePath("user://settings.json")) ? System.IO.File.ReadAllText(ProjectSettings.GlobalizePath("user://settings.json")) : null;
        try
        {
            var rng = new Random(99);
            // every slot, every class: write, read, compare field by field through JSON
            for (int slot = 1; slot <= SaveSystem.SlotCount; slot++)
            {
                var cls = (ClassId)((slot - 1) % Names.ClassCount);
                var d = Hero.NewCharacter($"存檔{slot}", cls, slot == 8);
                d.Level = 5 * slot; d.Gold = 1000 * slot; d.Scrap = slot; d.Dust = slot * 2; d.Cores = slot * 3; d.Tier = slot % 4; d.MaxTier = 3; d.Area = slot % 2 == 0 ? "frost" : "wastes_d1";
                d.Quests["main_wastes"] = slot % 5; d.Flags.Add("q_sh_wastes"); d.Waypoints.Add("wastes"); d.Renown["wastes"] = 40 * slot; d.Codex["executioner"] = 0.5f; d.Paragon = slot; d.Deaths = slot;
                var hero = new Hero(d);
                Game.AutoBuild(hero); Game.AutoGear(hero);
                for (int i = 0; i < 12; i++) d.Bag.Add(ItemDb.Generate(100 + i * 20, (Rarity)(i % 5), cls, null, rng));
                d.Bag.Add(ItemDb.RandomGem(30, rng));
                d.Stash.Add(ItemDb.Generate(300, Rarity.Legendary, cls, null, rng));
                string before = Canon(d);
                bool saved = SaveSystem.Save(slot, d);
                var back = SaveSystem.Load(slot);
                string after = back == null ? "" : Canon(back);
                Check(saved && back != null && before == after, $"save slot {slot}: {Names.ClassKey(cls)} lv{d.Level} round-trips exactly", $"bytes={before.Length} equal={before == after}");
                if (back != null && before != after)
                {
                    int at = 0; while (at < before.Length && at < after.Length && before[at] == after[at]) at++;
                    Log.Warn("suite", $"save slot {slot} first difference at {at}: saved '…{before.Substring(Math.Max(0, at - 60), Math.Min(120, before.Length - Math.Max(0, at - 60)))}' loaded '…{after.Substring(Math.Max(0, at - 60), Math.Min(120, after.Length - Math.Max(0, at - 60)))}'");
                }
                if (back != null)
                {
                    var h2 = new Hero(back);
                    Check(Mathf.IsEqualApprox(h2.MaxLife, hero.MaxLife) && Mathf.IsEqualApprox(h2.Armor, hero.Armor) && Mathf.IsEqualApprox(h2.WeaponMax, hero.WeaponMax) && h2.SkillPoints == hero.SkillPoints,
                        $"save slot {slot}: the loaded hero has the same stats", $"life {hero.MaxLife:0}/{h2.MaxLife:0} armor {hero.Armor:0}/{h2.Armor:0} weapon {hero.WeaponMax:0}/{h2.WeaponMax:0}");
                }
            }
            var list = SaveSystem.List();
            Check(list.Count == SaveSystem.SlotCount && list.All(s => s.Exists && !s.Corrupt) && list[7].Hardcore && list[2].Level == 15 && list[2].Name == "存檔3", "save: the slot list shows all eight characters",
                string.Join(" | ", list.Select(s => $"{s.Slot}:{s.Name} lv{s.Level} {s.Area}{(s.Hardcore ? " HC" : "")}")));
            yield return null;

            // backup fallback
            var d3 = SaveSystem.Load(3);
            d3.Level = 16; SaveSystem.Save(3, d3);            // slot3.bak now holds level 15, slot3.json level 16
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "slot3.json"), "{ this is not json");
            int errsBefore = Log.ErrorCount;
            var fromBak = SaveSystem.Load(3);
            Check(fromBak != null && fromBak.Level == 15 && fromBak.Name == "存檔3", "save: a damaged slot falls back to its .bak", $"level={fromBak?.Level} (errors logged +{Log.ErrorCount - errsBefore}, expected 1)");
            var l3 = SaveSystem.List()[2];
            Check(l3.Exists && !l3.Corrupt && l3.Level == 15, "save: the slot list reads the backup too");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "slot3.bak"), "");
            var none = SaveSystem.Load(3);
            var l3b = SaveSystem.List()[2];
            Check(none == null && l3b.Exists && l3b.Corrupt, "save: with both files damaged the slot is reported corrupt, not loaded");
            G.LoadGame(3);
            yield return 0.2f;
            Check(G.State != Game.GameState.Loading && (G.Hero == null || G.Slot != 3), "save: loading a corrupt slot is refused without a crash", $"state={G.State}");
            Log.Guard("ui", () => G.Ui?.CloseAll());
            // truncated / partial JSON that still parses: fields missing
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "slot3.json"), "{\"Name\":\"殘缺\",\"Class\":2,\"Level\":999,\"Bar\":null,\"Equip\":[null],\"Bag\":[null],\"Skills\":null}");
            var partial = SaveSystem.Load(3);
            bool heroOk = false;
            try { if (partial != null) { var hp = new Hero(partial); heroOk = hp.MaxLife > 0; } } catch (Exception e) { Log.Error("suite", "hero from a partial save threw", e); }
            Check(partial != null && partial.Level == Balance.LevelCap && partial.Bar.Length == 6 && partial.Equip.Length == SaveSystem.EquipSlots && partial.Bag.Count == 0 && heroOk, "save: a hand-edited save with missing fields is repaired", $"level={partial?.Level}");
            SaveSystem.Delete(3);
            Check(!SaveSystem.List()[2].Exists, "save: deleting a slot removes it");
            yield return null;

            // hardcore: death marks the slot at once and the slot cannot be loaded again
            _section = "save: hardcore";
            yield return StartHero(ClassId.Hunter, 10, "wastes", gear: true, god: false, slot: 8, hardcore: true);
            var hc = G.Hero; var p = G.Player;
            ClearField();
            yield return 0.2f;
            p.IFrames = 0f; p.Barrier = 0f;
            p.TakeHit(new Hit { Source = null, Amount = p.MaxLife * 50f, El = Element.Shadow });
            yield return null;
            var onDisk = SaveSystem.Load(8);
            Check(p.Dead && hc.D.Dead && onDisk != null && onDisk.Dead && onDisk.Hardcore, "hardcore: death is written to the slot immediately", $"dead={p.Dead} savedDead={onDisk?.Dead}");
            yield return Until(() => G.Ui != null && G.Ui.IsOpen("death"), 8, "the death panel");
            Check(G.Ui != null && G.Ui.IsOpen("death"), "hardcore: the death panel opens");
            G.Respawn();
            yield return Until(() => G.State == Game.GameState.Menu, 10, "the menu after a hardcore death");
            Check(G.State == Game.GameState.Menu && G.Hero == null, "hardcore: 'respawn' returns to the title screen", $"state={G.State}");
            G.LoadGame(8);
            yield return 0.3f;
            Check(G.Hero == null && G.State == Game.GameState.Menu, "hardcore: the dead character can no longer be loaded");
            Check(SaveSystem.List()[7].Dead, "hardcore: the slot list marks the character dead");
            Log.Guard("ui", () => G.Ui?.CloseAll());
            yield return null;

            // normal death: death panel, respawn at the checkpoint
            _section = "save: death";
            yield return StartHero(ClassId.Soulbinder, 10, "wastes", gear: true, god: false, slot: 7);
            p = G.Player; var hn = G.Hero;
            ClearField();
            int deaths0 = hn.D.Deaths;
            p.Teleport(World.Grid.RandomWalkNear(G.Level.Checkpoint, 8f, 14f));
            yield return 0.2f;
            p.TakeHit(new Hit { Source = null, Amount = p.MaxLife * 50f, El = Element.Fire });
            yield return Until(() => G.Ui != null && G.Ui.IsOpen("death"), 8, "the death panel");
            Check(p.Dead && hn.D.Deaths == deaths0 + 1 && G.Ui.IsOpen("death"), "death: the hero dies, the counter rises and the death panel opens", $"deaths={hn.D.Deaths}");
            string shot = G.ArgValue("shot");
            if (!string.IsNullOrEmpty(shot)) { yield return 1.0f; G.HarnessShot(shot.Replace(".png", "") + "_death.png"); yield return 0.3f; }
            G.Respawn();
            yield return 0.3f;
            Check(!p.Dead && p.LifeFrac > 0.99f && p.Pos2.DistanceTo(G.Level.Checkpoint) < 3f && !G.Ui.IsOpen("death") && !G.Paused, "death: respawn revives at the checkpoint with full life and the game runs again",
                $"dead={p.Dead} life={p.LifeFrac:P0} dist={p.Pos2.DistanceTo(G.Level.Checkpoint):0.0} paused={G.Paused} locks={G.PauseLocks}");

            // play → save → reload in the same process: level, gear, quests, area
            _section = "save: session";
            hn.AddXp(Balance.XpToNext(hn.D.Level) + 5);
            hn.D.Gold += 777;
            string weapon = hn.Weapon?.Uid; int level = hn.D.Level; long gold = hn.D.Gold;
            G.SaveNow("suite");
            G.ToMenu();
            yield return Until(() => G.State == Game.GameState.Menu, 10, "the menu");
            G.HarnessNext(null, -1, false, -1, false);
            G.LoadGame(7);
            yield return Until(() => G.State == Game.GameState.Play && G.Hero != null && G.Level != null && G.Level.Built, 120, "the loaded game");
            var hl = G.Hero;
            Check(hl != null && hl.D.Level == level && hl.D.Gold == gold && hl.Weapon?.Uid == weapon && G.Level.AreaId == "wastes" && hl.D.Deaths == deaths0 + 1,
                "load: level, gold, gear, deaths and area come back", $"lv{hl?.D.Level} gold={hl?.D.Gold} area={G.Level?.AreaId} quests=[{string.Join(",", hl?.D.Quests.Select(kv => kv.Key + "@" + kv.Value) ?? Array.Empty<string>())}]");

            // settings
            _section = "save: settings";
            var cfg = G.Cfg;
            int q0 = cfg.Quality; float m0 = cfg.Music; bool dn0 = cfg.DamageNumbers; int ts0 = cfg.UiTextScale;
            cfg.Quality = (q0 + 1) % 4; cfg.Music = 0.33f; cfg.DamageNumbers = !dn0; cfg.UiTextScale = (ts0 + 1) % 3;
            cfg.Save();
            var re = Settings.Load();
            Check(re.Quality == cfg.Quality && Mathf.IsEqualApprox(re.Music, 0.33f) && re.DamageNumbers == cfg.DamageNumbers && re.UiTextScale == cfg.UiTextScale, "settings: saved values are read back");
            cfg.Quality = q0; cfg.Music = m0; cfg.DamageNumbers = dn0; cfg.UiTextScale = ts0;
            cfg.Save();

            // leave the test character before the slots are restored, so quitting cannot write it back into a user slot
            G.ToMenu();
            yield return Until(() => G.State == Game.GameState.Menu && G.Hero == null, 10, "the menu at the end of the save suite");
        }
        finally
        {
            // put the user's slots and settings back exactly as they were
            try
            {
                G.Cfg.LastSlot = lastSlot0;
                RestoreSaves(dir, backup);
                if (settingsBefore != null) System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("user://settings.json"), settingsBefore);
                Log.Info("suite", "save: the user's save files and settings were restored");
            }
            catch (Exception e) { Log.Error("suite", "restoring the user's saves failed", e); }
        }
    }
}
