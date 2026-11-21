using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// What an encounter's own script overrides. A script is a composition of the mechanic blocks on
/// <see cref="NamedEncounter"/> (life thresholds, timers, summons, telegraphed novas / lines / cones / rings, channels,
/// shields, life floors, props, zones, tethers, leashes); bespoke code goes in the callbacks.
/// </summary>
public abstract class NamedScript
{
    public NamedEncounter E;
    protected Monster M => E.M;
    protected static Player P => World.P;
    protected Level L => E.L;
    protected Vector2 C => E.Center;
    protected float R => E.ArenaR;
    protected Element El => E.Def.Aura;

    /// <summary>Once, when the encounter is created: thresholds, timers and other blocks.</summary>
    public virtual void Build() { }
    /// <summary>At creation and after every reset: props and anything that must be whole when the fight starts.</summary>
    public virtual void Setup() { }
    public virtual void Engage() { }
    /// <summary>Every unpaused frame while the fight is on.</summary>
    public virtual void Tick(float dt) { }
    /// <summary>Every unpaused frame while nobody fights it (patrols, ambushes).</summary>
    public virtual void Idle(float dt) { }
    /// <summary>The named monster took a hit (after it was applied; its life can still be put back here).</summary>
    public virtual void Damaged(Hit h, float amount) { }
    /// <summary>The hero was hit by the named monster or by something of this encounter.</summary>
    public virtual void HeroHit(Hit h, float amount) { }
    /// <summary>An add or a prop of this encounter took a hit.</summary>
    public virtual void OtherDamaged(Monster m, Hit h, float amount) { }
    /// <summary>Any hostile monster died while the fight is on (adds, props, bystanders).</summary>
    public virtual void Killed(Monster m) { }
    public virtual void Phase(int n) { }
    internal virtual void Move(BossMoveDef d) { }
    internal virtual void Dropped(BossMoveDef d, Vector2 at, float radius) { }
    /// <summary>The named monster died. Hazards started here still resolve.</summary>
    public virtual void Death() { }
    /// <summary>The fight ended without a kill: put back what Build's blocks do not know about.</summary>
    public virtual void Reset() { }
    /// <summary>True while the script is acting through a brain-driven boss (the brain then waits).</summary>
    public virtual bool Holding => false;
    /// <summary>Where a self-playing hero should stand right now / what it should hit first (test hints; null = no opinion).</summary>
    public virtual Vector2? BotStand => null;
    public virtual Monster BotTarget => null;
    /// <summary>Test hint: the named monster should not be struck right now (a neutral guide, a phase to sit out).</summary>
    public virtual bool BotPassive => false;

    protected void Mech(string key) => E.Mech(key);
}

/// <summary>A lit place: braziers, burning props. Scripts ask <see cref="NamedEncounter.InFirelight"/>.</summary>
public sealed class LightSpot
{
    public Vector2 Pos; public float Radius = 6f; public bool Lit = true; public Node3D Node; public float Off;      // Off > 0: relights by itself after that many seconds
}

/// <summary>
/// Runs one named encounter: the monster, its script, everything the fight puts on the field and the bookkeeping
/// around it (engage / reset / death, name plate, boss bar, music, intro line, mechanic log, loot, kill record).
/// One node per encounter, child of the level; it outlives the monster by a few seconds to finish what death started.
/// </summary>
public partial class NamedEncounter : Node
{
    public NamedDef Def;
    public Monster M;
    public Level L;
    public NamedScript S;
    public Vector2 Home, Center;
    public float ArenaR = 12f;
    public bool Engaged, Slain, Test;
    public float FightTime, Floor;                 // Floor: the monster cannot be brought below this fraction of its life
    public bool KillAddsOnDeath = true, HalfLoot, NoLoot;
    public readonly HashSet<string> Fired = new();
    public readonly List<Monster> Adds = new(), Props = new();
    public readonly List<LightSpot> Lights = new();
    public float DamageTaken;                      // total damage the named monster took this fight
    public event Action<NamedEncounter> Finished;
    public string Result = "";

    private sealed class Threshold { public float Frac; public bool Done; public Action Do; }
    public sealed class Loop { public float Period, First, T; public bool On = true; public Action Do; public Func<bool> When; }
    private sealed class PropInfo { public Vector2 At; public Action Destroyed; public bool Pinned = true; }

    private readonly List<Threshold> _thresholds = new();
    private readonly List<Loop> _loops = new();
    private readonly List<Node3D> _nodes = new();
    private readonly List<GroundZone> _zones = new();
    private readonly List<Projectile> _shots = new();
    private readonly Dictionary<Monster, PropInfo> _props = new();
    private readonly object _moveTok = new(), _hazTok = new();
    private static readonly List<Actor> Tmp = new();
    private bool _introSaid, _announced, _music, _levelManaged, _bar;
    private float _leash, _afterDeath, _baseDmg, _dr0 = 1f, _speed0 = 1f, _atk0 = 1f, _dmg0 = 1f;
    private bool _blocks0;
    private int _level, _errors;
    private BossBrain _brain;

    public int Level => _level;
    public bool LevelManaged => _levelManaged;
    public BossBrain Brain => M != null && IsInstanceValid(M) ? M.Brain : null;
    public bool Alive => M != null && IsInstanceValid(M) && !M.Dead;
    public float LifeFrac => Alive ? M.Life / Mathf.Max(1f, M.MaxLife) : 0f;
    public bool HeroOk => World.P != null && IsInstanceValid(World.P) && !World.P.Dead;
    public Vector2 HeroPos => World.P != null ? World.P.Pos2 : Center;
    public float HeroDist => Alive && World.P != null ? M.Pos2.DistanceTo(World.P.Pos2) : 999f;

    // ───────────────────────── life cycle ─────────────────────────

    internal void Init(NamedDef def, Monster m, Level level, Vector2 centre, float arenaR, bool test)
    {
        Def = def; M = m; L = level; Home = m.Pos2; Center = centre; ArenaR = arenaR; Test = test;
        _level = m.Level; _baseDmg = m.BaseDamage;
        _dr0 = m.DrMul; _speed0 = m.SpeedMul; _atk0 = m.AtkSpeedMul; _dmg0 = m.DmgMul; _blocks0 = m.BlocksProjectiles;      // what its elite affixes set
        Name = "Named_" + def.Id;
        _levelManaged = level != null && level.BossMon == m;
        S = def.Script?.Invoke() ?? new PlainScript();
        S.E = this;
        try { S.Build(); S.Setup(); }
        catch (Exception e) { Log.Error("named", $"{def.Id}: script set-up failed", e); }
        if (m.Brain != null) AttachBrain(m.Brain);
        Log.Info("named", $"spawned {def.Id} ({def.NameZh}) kind={def.Kind} base={def.Base} model={m.Def.Model} level={m.Level} life={m.MaxLife:F0} dmg={m.BaseDamage:F0} " +
            $"at=({m.Pos2.X:F0},{m.Pos2.Y:F0}) arena=({centre.X:F0},{centre.Y:F0}) r={arenaR:F0} brain={(m.Brain != null ? "kit" : "base-ai")} script={S.GetType().Name} mechs=[{string.Join(",", def.Mechs)}]");
    }

    /// <summary>The level registered the monster as its boss after the encounter was created (dungeons, arenas).</summary>
    internal void MarkLevelManaged() => _levelManaged = L != null && L.BossMon == M;

    internal void AttachBrain(BossBrain b)
    {
        if (b == null || ReferenceEquals(b, _brain) || b.Scripted) return;
        bool replaced = _brain != null;
        _brain = b;
        b.Engaging = () => { if (!Engaged) Engage(); };
        b.Disengaged = () => { if (Engaged) Disengage("the brain let go"); };
        b.PhaseBegan = n => { if (Engaged) Guard(() => S.Phase(n)); };
        b.MoveStarted = d => { if (Engaged) Guard(() => S.Move(d)); };
        b.Dropped = (d, at, r) => { if (Engaged) Guard(() => S.Dropped(d, at, r)); };
        b.Held = () => S.Holding;
        if (!_levelManaged && !Test) b.SetArena(Center, ArenaR);
        if (replaced && Engaged) Disengage("the level reset the fight");       // Level.ResetBossFight made a fresh brain
    }

    public override void _Process(double delta)
    {
        if (Game.I == null || Game.I.Paused) return;
        try { Step((float)delta); }
        catch (Exception e) { if (_errors++ < 12) Log.Error("named", $"{Def?.Id}: update failed", e); }
    }

    private void Step(float dt)
    {
        if (Slain)
        {
            _afterDeath += dt;
            TickLights(dt);
            if (_afterDeath > 12f) { Cleanup(false); QueueFree(); }
            return;
        }
        if (M == null || !IsInstanceValid(M)) { Cleanup(true); QueueFree(); return; }      // removed without a death (level tools, harness)
        bool now = FightIsOn(dt);
        if (now && !Engaged) Engage();
        else if (!now && Engaged) Disengage("out of combat");
        PinProps();
        TickLights(dt);
        if (!Engaged)
        {
            if (!_announced && Def.Kind == NamedKind.Rare && HeroOk && HeroDist < 30f && (World.Grid == null || World.Grid.LineShot(M.Pos2, HeroPos))) Announce();
            Guard(() => S.Idle(dt));
            return;
        }
        FightTime += dt;
        for (int i = 0; i < _thresholds.Count; i++)
        {
            var t = _thresholds[i];
            if (t.Done || LifeFrac > t.Frac) continue;
            t.Done = true;
            Guard(t.Do);
            if (!Alive) return;
        }
        for (int i = 0; i < _loops.Count; i++)
        {
            var l = _loops[i];
            if (!l.On) continue;
            l.T -= dt;
            if (l.T > 0f) continue;
            if (l.When != null && !l.When()) { l.T = 0.5f; continue; }
            l.T = l.Period;
            Guard(l.Do);
        }
        Guard(() => S.Tick(dt));
    }

    private bool FightIsOn(float dt)
    {
        var p = World.P;
        if (p == null || !IsInstanceValid(p)) return false;
        var b = M.Brain;
        if (b != null && !b.Scripted) return b.Engaged;
        if (b != null) return Engaged && !p.Dead;                 // a remote-controlled monster: its script decides (see Provoke)
        if (p.Dead) return false;
        bool on = M.Aggro || (Engaged && M.Life < M.MaxLife);
        if (on && M.Pos2.DistanceTo(p.Pos2) > 60f) { _leash += dt; if (_leash > 8f) return false; }
        else _leash = 0f;
        return on;
    }

    /// <summary>Starts the fight from a script (a patrol that was attacked, an ambush that was sprung).</summary>
    public void Provoke() { if (!Engaged && !Slain && Alive) { M.ForceAggro(); Engage(); } }

    private void Announce()
    {
        _announced = true;
        Game.I?.Hud?.Toast($"稀有菁英現身——{Def.NameZh}", Palette.Gold);
        Snd.Ui("event_start");
        L?.Reveal(M.Pos2, 8f);
        Log.Info("named", $"{Def.Id} discovered by the hero");
    }

    private void Engage()
    {
        Engaged = true; FightTime = 0f; DamageTaken = 0f; _leash = 0f;
        if (!_announced && Def.Kind == NamedKind.Rare) Announce();
        var g = Game.I;
        if (!_levelManaged && L != null)
        {
            L.Boss = M; _bar = true;                                 // the big bar at the top of the screen
            if (Def.Boss)
            {
                _music = true;
                g?.Audio?.PlayMusic(Def.Id == "pin_8_osaros" ? "boss_final" : "boss");
                g?.Cam?.SetZoomTarget(Def.Kind is NamedKind.World or NamedKind.Pinnacle ? 1.5f : 1.3f, 1.6f);
                g?.Hud?.AreaTitle(Def.NameZh, Def.Title);
            }
        }
        if (!_introSaid && Def.Intro != null)
        {
            _introSaid = true;
            float t = Math.Clamp(1.8f + Def.Intro.Length * 0.17f, 2.5f, 8f);
            g?.Hud?.Subtitle(Def.IntroSpeaker ?? Def.NameZh, Def.Intro, t);
            g?.Audio?.PlayVoice(Def.Id + "_intro");
        }
        Log.Info("named", $"{Def.Id} engaged: life={M.MaxLife:F0} level={M.Level} heroLevel={g?.Hero?.D.Level ?? 0} tier={g?.WorldTier ?? 0}");
        Guard(S.Engage);
    }

    private void Disengage(string why)
    {
        Engaged = false;
        Log.Info("named", $"{Def.Id} fight reset ({why}) after {FightTime:F0}s at {LifeFrac:P0} life");
        ClearField();
        foreach (var t in _thresholds) t.Done = false;
        foreach (var l in _loops) { l.T = l.First; l.On = true; }
        Floor = 0f;
        if (Alive)
        {
            if (M.Brain == null || M.Brain.Scripted) { M.Life = M.MaxLife; M.Barrier = 0f; M.Dots.Clear(); }
            M.DrMul = _dr0; M.SpeedMul = _speed0; M.AtkSpeedMul = _atk0; M.DmgMul = _dmg0; M.BlocksProjectiles = _blocks0; M.Untargetable = false; M.Visible = true;
            M.BaseDamage = _baseDmg;
        }
        Guard(S.Reset);
        Guard(S.Setup);
        EndPresentation();
    }

    private void EndPresentation()
    {
        var g = Game.I;
        if (_bar && L != null && IsInstanceValid(L) && L.Boss == M) L.Boss = null;
        _bar = false;
        if (_music)
        {
            _music = false;
            string track = (L?.Region ?? L?.SpawnRegion)?.Music;
            if (!string.IsNullOrEmpty(track)) g?.Audio?.PlayMusic(track);
            g?.Cam?.SetZoomTarget(1f, 2f);
        }
    }

    /// <summary>Called by the hub when the named monster dies.</summary>
    internal void OnSlain(Hit h)
    {
        if (Slain) return;
        Slain = true;
        bool wasEngaged = Engaged;
        Engaged = false;
        HazardClock.Cancel(_moveTok);
        HazardClock.Cancel(_hazTok);
        FreeNodes();
        Vector2 at = M.Pos2;
        Guard(S.Death);
        if (KillAddsOnDeath) KillAll(Adds);
        KillAll(Props, true);
        var missing = new List<string>();
        foreach (var k in Def.Mechs) if (!Fired.Contains(k)) missing.Add(k);
        Result = $"killed in {FightTime:F1}s";
        Log.Info("named", $"{Def.Id} slain: fight={FightTime:F1}s engaged={wasEngaged} mechs={Def.Mechs.Length - missing.Count}/{Def.Mechs.Length}{(missing.Count > 0 ? $" missing=[{string.Join(",", missing)}]" : "")} " +
            $"counts=[{string.Join(",", MechCount.Select(kv => $"{kv.Key}×{kv.Value}"))}] " +
            $"heroLevel={Game.I?.Hero?.D.Level ?? 0} tier={Game.I?.WorldTier ?? 0} godSaves={Player.GodSaves}");
        Fx.Burst(World.To3(at, 1f), Def.Aura, 5f, 1.6f);
        Fx.Shake(0.4f, 0.5f);
        Guard(() => NamedHub.Slain(this, at));
        EndPresentation();
        Finished?.Invoke(this);
    }

    private void Cleanup(bool killField)
    {
        HazardClock.Cancel(_moveTok);
        HazardClock.Cancel(_hazTok);
        FreeNodes();
        if (killField) { KillAll(Adds); KillAll(Props, true); }
        foreach (var z in _zones) if (z != null && IsInstanceValid(z) && !z.Done) z.Remove();
        _zones.Clear();
        foreach (var l in Lights) if (l.Node != null && IsInstanceValid(l.Node)) l.Node.QueueFree();
        Lights.Clear();
        EndPresentation();
        NamedHub.Forget(this);
    }

    public override void _ExitTree() { HazardClock.Cancel(_moveTok); HazardClock.Cancel(_hazTok); NamedHub.Forget(this); }

    /// <summary>Everything the fight put on the field goes: pending hazards, telegraphs, zones, shots, adds and props.</summary>
    private void ClearField()
    {
        HazardClock.Cancel(_moveTok);
        HazardClock.Cancel(_hazTok);
        FreeNodes();
        foreach (var z in _zones) if (z != null && IsInstanceValid(z) && !z.Done) z.Remove();
        _zones.Clear();
        foreach (var s in _shots) if (s != null && IsInstanceValid(s) && !s.IsQueuedForDeletion()) Fx.ReleaseProjectile(s);
        _shots.Clear();
        KillAll(Adds);
        KillAll(Props, true);
        foreach (var l in Lights) if (l.Node != null && IsInstanceValid(l.Node)) l.Node.QueueFree();
        Lights.Clear();
    }

    private void FreeNodes()
    {
        foreach (var n in _nodes) if (n != null && IsInstanceValid(n) && !n.IsQueuedForDeletion()) n.QueueFree();
        _nodes.Clear();
    }

    private void KillAll(List<Monster> list, bool silent = false)
    {
        if (list.Count == 0) return;
        var arr = list.ToArray();
        list.Clear();
        foreach (var a in arr)
        {
            if (a == null || !IsInstanceValid(a)) continue;
            _props.Remove(a);
            NamedHub.Unlink(a);
            if (a.Dead) continue;
            a.NoLoot = true; a.XpValue = 0f;
            if (silent) { World.Monsters.Remove(a); a.QueueFree(); continue; }
            a.Life = 0f; a.Dead = true;
            a.Die(new Hit());
        }
    }

    private void Guard(Action a)
    {
        if (a == null) return;
        try { a(); }
        catch (Exception e) { if (_errors++ < 12) Log.Error("named", $"{Def?.Id}: script step failed", e); }
    }

    // ───────────────────────── events from the hub ─────────────────────────

    internal void OnDamaged(Hit h, float amount)
    {
        if (Slain || !Alive) return;
        DamageTaken += amount;
        if (Floor > 0f)
        {
            float min = M.MaxLife * Floor;
            if (M.Life < min) M.Life = min;                  // the blow has landed, death has not been decided yet
        }
        Guard(() => S.Damaged(h, amount));
    }

    internal void OnHeroHit(Hit h, float amount) { if (Engaged) Guard(() => S.HeroHit(h, amount)); }

    /// <summary>A prop or an add of this encounter took a hit (after it was applied).</summary>
    internal void OnPropDamaged(Monster m, Hit h, float amount) { if (!Slain) Guard(() => S.OtherDamaged(m, h, amount)); }

    internal void OnKilled(Monster m)
    {
        if (_props.TryGetValue(m, out var info))
        {
            _props.Remove(m); Props.Remove(m);
            NamedHub.Unlink(m);
            if (!Slain) Guard(info.Destroyed);
        }
        else Adds.Remove(m);
        if (Engaged) Guard(() => S.Killed(m));
    }

    // ───────────────────────── blocks: bookkeeping ─────────────────────────

    /// <summary>Reports that a mechanic fired (one log line per trigger; the test suite expects every key of the definition).</summary>
    public void Mech(string key)
    {
        int n = MechCount.TryGetValue(key, out int had) ? had + 1 : 1;
        MechCount[key] = n;
        if (Fired.Add(key) || (n % 10 == 0 && n <= 50))
            Log.Info("named", $"{Def.Id} mech {key}{(n > 1 ? $" ×{n}" : "")} t={FightTime:F1}s life={LifeFrac:P0}");
    }

    /// <summary>How often each mechanic fired since the encounter was created.</summary>
    public readonly Dictionary<string, int> MechCount = new();

    /// <summary>Runs once when the monster's life falls to each fraction (re-armed when the fight resets).</summary>
    public void AtLife(Action a, params float[] fracs)
    {
        foreach (float f in fracs) _thresholds.Add(new Threshold { Frac = f, Do = a });
        _thresholds.Sort((x, y) => y.Frac.CompareTo(x.Frac));
    }

    /// <summary>Runs every <paramref name="period"/> seconds of the fight (first after <paramref name="first"/>), while <paramref name="when"/> holds.</summary>
    public Loop Every(float period, Action a, float first = -1f, Func<bool> when = null)
    {
        var l = new Loop { Period = period, First = first < 0f ? period : first, Do = a, When = when };
        l.T = l.First;
        _loops.Add(l);
        return l;
    }

    /// <summary>A hazard already shown on the ground: resolves unless the fight resets or the level is left.</summary>
    public void After(float delay, Action a) => HazardClock.After(delay, () => Guard(a), _hazTok);
    /// <summary>A step of something the monster is doing: also dropped by <see cref="Interrupt"/>.</summary>
    public void Then(float delay, Action a) => HazardClock.After(delay, () => Guard(a), _moveTok);
    public void Run(float duration, Action<float> tick, Action end = null) => HazardClock.Run(duration, k => Guard(() => tick(k)), end == null ? null : () => Guard(end), _hazTok);
    public void Interrupt() { HazardClock.Cancel(_moveTok); }
    public Node3D Track(Node3D n) { if (n != null) _nodes.Add(n); return n; }
    public T Own<T>(T n) where T : Node3D { if (n != null) _nodes.Add(n); return n; }
    public void Toast(string text) => Game.I?.Hud?.Toast($"{Def.NameZh}：{text}", Palette.Danger);
    public void Warn(string text) => Game.I?.Hud?.Warn(text);

    // ───────────────────────── blocks: damage shapes ─────────────────────────

    /// <summary>One hostile hit on an actor; works even after the named monster's node is gone (death bursts).</summary>
    public float Hit(Actor a, float mul, Element el, StatusApply? st = null, bool melee = false)
    {
        if (a == null || a.Dead || !IsInstanceValid(a)) return 0f;
        if (M != null && IsInstanceValid(M)) return Combat.MonsterHit(M, a, mul, el, st, melee);
        float raw = _baseDmg * mul * Rng.Range(0.9f, 1.1f);
        if (a is Player p)
        {
            if (p.Invulnerable) return 0f;
            var h = p.Hero;
            float red = (1f - Balance.ArmorDr(h.Armor, _level)) * (1f - h.Res(el) / 100f) * (1f - h.S[Stat.DmgReduction] / 100f) * p.ExtraDr();
            return p.TakeHit(new Hit { Amount = raw * red, El = el });
        }
        return a.TakeHit(new Hit { Amount = raw * 0.45f, El = el });
    }

    public void HitCircle(Vector2 c, float r, float mul, Element el, StatusApply? st = null)
    {
        World.InCircle(Faction.Monster, c, r, Tmp);
        foreach (var a in Tmp.ToArray()) Hit(a, mul, el, st);
    }

    public void HitRing(Vector2 c, float inner, float outer, float mul, Element el, StatusApply? st = null)
    {
        World.InCircle(Faction.Monster, c, outer, Tmp);
        foreach (var a in Tmp.ToArray())
        {
            float d = a.Pos2.DistanceTo(c);
            if (d >= inner && d <= outer + a.Radius) Hit(a, mul, el, st);
        }
    }

    public void HitLine(Vector2 a, Vector2 b, float width, float mul, Element el, StatusApply? st = null)
    {
        World.InCapsule(Faction.Monster, a, b, Mathf.Max(0.3f, width * 0.5f - 0.15f), Tmp);
        foreach (var x in Tmp.ToArray()) Hit(x, mul, el, st);
    }

    public bool HitCone(Vector2 origin, Vector2 dir, float range, float arc, float mul, Element el, StatusApply? st = null)
    {
        World.InCone(Faction.Monster, origin, dir, range, arc, Tmp);
        bool hero = false;
        foreach (var x in Tmp.ToArray()) { if (x is Player) hero = true; Hit(x, mul, el, st); }
        return hero;
    }

    /// <summary>A marked circle that goes off after <paramref name="wind"/> seconds (never shown for less than 0.6 s).</summary>
    public void Blast(Vector2 at, float r, float wind, float mul, Element el, StatusApply? st = null, Action then = null, bool meteor = false, string sfx = null)
    {
        wind = Mathf.Max(0.6f, wind);
        Track(Fx.Telegraph(at, r, wind, Palette.Danger, el));
        if (meteor) Fx.Meteor(at, wind, el, r);
        After(wind, () =>
        {
            HitCircle(at, r, mul, el, st);
            Fx.Burst(World.To3(at, 0.2f), el, r, 0.9f);
            Snd.At(sfx ?? ImpactSfx(el), World.To3(at), 0.7f);
            then?.Invoke();
        });
    }

    /// <summary>A marked ring (safe inside <paramref name="inner"/> and beyond <paramref name="outer"/>).</summary>
    public void RingBlast(Vector2 c, float inner, float outer, float wind, float mul, Element el, StatusApply? st = null, Action then = null)
    {
        wind = Mathf.Max(0.6f, wind);
        Track(Fx.TelegraphRing(c, inner, outer, wind, Palette.Danger, el));
        After(wind, () =>
        {
            HitRing(c, inner, outer, mul, el, st);
            Fx.Ring(World.To3(c, 0.25f), outer, Palette.Element(el), 0.45f, el);
            Snd.At(ImpactSfx(el), World.To3(c), 0.6f);
            then?.Invoke();
        });
    }

    /// <summary>
    /// Rings rolling outward from a point, one after the other, each <paramref name="width"/> wide: the gaps in time
    /// (<paramref name="gaps"/>, cycled) are what the hero steps through. <paramref name="each"/> runs as every ring lands.
    /// </summary>
    public void Waves(Vector2 c, int count, float firstInner, float width, float firstDelay, float[] gaps, float mul, Element el, StatusApply? st = null, Action<int, float, float> each = null)
    {
        float t = Mathf.Max(0.6f, firstDelay);
        for (int k = 0; k < count; k++)
        {
            int kk = k;
            float inner = firstInner + k * width, outer = inner + width, at = t;
            After(Mathf.Max(0f, at - 0.7f), () => Track(Fx.TelegraphRing(c, inner, outer, 0.7f, Palette.Danger, el)));
            After(at, () =>
            {
                HitRing(c, inner, outer, mul, el, st);
                Fx.Ring(World.To3(c, 0.25f), outer, Palette.Element(el), 0.45f, el);
                Snd.At(ImpactSfx(el), World.To3(c), 0.5f);
                each?.Invoke(kk, inner, outer);
            });
            t += gaps != null && gaps.Length > 0 ? gaps[k % gaps.Length] : 0.55f;
        }
    }

    /// <summary>A marked lane that strikes after <paramref name="wind"/> seconds.</summary>
    public void LineBlast(Vector2 from, Vector2 dir, float len, float width, float wind, float mul, Element el, StatusApply? st = null, Action then = null)
    {
        wind = Mathf.Max(0.6f, wind);
        dir = dir.LengthSquared() < 1e-4f ? Vector2.Down : dir.Normalized();
        Track(Fx.TelegraphLine(from, dir, len, width, wind, Palette.Danger, el));
        After(wind, () =>
        {
            Vector2 end = from + dir * len;
            HitLine(from, end, width, mul, el, st);
            Fx.Beam(World.To3(from, 1.1f), World.To3(end, 1.1f), el, width * 0.5f, 0.3f);
            Snd.At(ImpactSfx(el), World.To3(from + dir * (len * 0.5f)), 0.6f);
            then?.Invoke();
        });
    }

    /// <summary>A cone from the monster along its facing (the monster is turned to <paramref name="dir"/> first).</summary>
    public void ConeBlast(Vector2 dir, float range, float arc, float wind, float mul, Element el, StatusApply? st = null, Action<bool> then = null)
    {
        if (!Alive) return;
        wind = Mathf.Max(0.6f, wind);
        M.FaceDir(dir);
        Vector2 origin = M.Pos2, d = M.Forward;
        Track(Fx.TelegraphCone(M, range, arc, wind, Palette.Danger, el));
        After(wind, () =>
        {
            bool hero = HitCone(origin, d, range, arc, mul, el, st);
            if (Alive) Fx.Slash(M, range, arc, el);
            Snd.At(ImpactSfx(el), World.To3(origin), 0.7f);
            then?.Invoke(hero);
        });
    }

    public GroundZone Zone(Vector2 at, float r, float dur, float mulPerTick, Element el, StatusApply? st = null)
    {
        if (!Alive || World.Root == null) return null;
        var z = GroundZone.SpawnMonster(M, at, r, dur, mulPerTick, el, st);
        _zones.Add(z);
        if (_zones.Count > 40) _zones.RemoveAll(x => x == null || !IsInstanceValid(x) || x.Done);
        return z;
    }

    public Projectile Shoot(Vector2 from, Vector2 dir, float speed, float reach, float mul, Element el, StatusApply? st = null, float radius = 0.45f, float homing = 0f)
    {
        if (!Alive) return null;
        var shot = Projectile.SpawnMonster(M, World.To3(from, 1.3f), dir, speed, reach, mul, el, st, radius);
        if (homing > 0f) shot.Homing = homing;
        _shots.Add(shot);
        if (_shots.Count > 60) _shots.RemoveAll(x => x == null || !IsInstanceValid(x));
        return shot;
    }

    public static string ImpactSfx(Element el) => el switch
    {
        Element.Fire => "fire_impact", Element.Frost => "frost_impact", Element.Lightning => "lightning_zap",
        Element.Poison => "poison_splash", Element.Shadow => "shadow_impact", _ => "earth_impact",
    };

    // ───────────────────────── blocks: positions ─────────────────────────

    public Vector2 Walk(Vector2 p) => World.Grid != null ? World.Grid.NearestWalk(p) : p;
    public bool CanStand(Vector2 p) => World.Grid == null || World.Grid.WalkAt(p);
    public Vector2 Near(Vector2 c, float rMin, float rMax) => World.Grid != null ? World.Grid.RandomWalkNear(c, rMin, rMax) : c + Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau)) * Rng.Range(rMin, rMax);
    /// <summary>A point of the arena on a ring round its centre (angle in radians), pulled onto walkable ground.</summary>
    public Vector2 OnRing(float angle, float radius) => Walk(Center + Vector2.Right.Rotated(angle) * radius);
    /// <summary>Where reinforcements run in from: a walkable spot well away from the hero, out of the fight.</summary>
    public Vector2 OffScreen(float dist = 20f)
    {
        var grid = World.Grid;
        Vector2 hero = HeroPos, best = Walk(Center);
        float bestD = -1f;
        for (int i = 0; i < 16; i++)
        {
            Vector2 q = hero + Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau)) * Rng.Range(dist * 0.8f, dist * 1.15f);
            if (grid != null && !grid.WalkAt(q)) continue;
            float d = q.DistanceTo(hero);
            if (d > bestD) { bestD = d; best = q; }
        }
        return best;
    }
    public Vector2 DirToHero(Vector2 from) { Vector2 d = HeroPos - from; return d.LengthSquared() < 1e-4f ? Vector2.Down : d.Normalized(); }

    // ───────────────────────── blocks: adds and props ─────────────────────────

    /// <summary>The definition an add is spawned from: the roster's type, or a stand-in while it does not exist.</summary>
    public static MonsterDef AddDef(string id, string fallback = null)
    {
        MonsterDef d = null;
        try { d = Bestiary.Live ? Bestiary.Local(id) : null; } catch (Exception) { }
        return d ?? MonsterDb.Get(id) ?? MonsterDb.Get(fallback) ?? MonsterDb.Get("husk") ?? MonsterDb.All[0];
    }

    /// <summary>Spawns one add now. It gives nothing (a named monster's servants are not a harvest) and belongs to the fight.</summary>
    public Monster Summon(string defId, Vector2 at, string fallback = null, bool aggro = true, float life = 1f, MonTier tier = MonTier.Normal)
    {
        if (World.Root == null) return null;
        var def = AddDef(defId, fallback);
        var m = Monster.Spawn(def, _level, tier, Walk(at), Faction.Monster);
        m.NoLoot = true; m.XpValue = 0f;
        if (life != 1f) { m.MaxLife = Mathf.Max(1f, Mathf.Round(m.MaxLife * life)); m.Life = m.MaxLife; }
        if (aggro) m.ForceAggro();
        Adds.Add(m);
        NamedHub.Link(m, this);
        Brain?.Adopt(m);
        Fx.Dust(World.To3(m.Pos2));
        return m;
    }

    /// <summary>Adds that appear on marked spots after a warning (risen from the ground, dropped, stepped out).</summary>
    public void SummonAfter(float delay, string defId, Vector2 at, string fallback = null, Action<Monster> then = null, float life = 1f)
    {
        at = Walk(at);
        Track(Fx.Telegraph(at, 1.1f, Mathf.Max(0.6f, delay), Palette.Danger));
        After(Mathf.Max(0.6f, delay), () =>
        {
            var m = Summon(defId, at, fallback, true, life);
            if (m != null) { Fx.Burst(World.To3(at, 0.3f), Def.Aura, 1.2f, 0.4f); then?.Invoke(m); }
        });
    }

    /// <summary>Adds that run in from off-screen.</summary>
    public void RunIn(string defId, int n, string fallback = null, Action<Monster> then = null)
    {
        for (int i = 0; i < n; i++)
        {
            var m = Summon(defId, Near(OffScreen(), 0f, 3f), fallback);
            if (m != null) then?.Invoke(m);
        }
        Snd.At("elite_spawn", World.To3(HeroPos), 0.6f);
    }

    public int AliveAdds(Func<Monster, bool> filter = null)
    {
        int n = 0;
        for (int i = Adds.Count - 1; i >= 0; i--)
        {
            var a = Adds[i];
            if (a == null || !IsInstanceValid(a) || a.Dead) { Adds.RemoveAt(i); continue; }
            if (filter == null || filter(a)) n++;
        }
        return n;
    }

    /// <summary>
    /// Something on the field that the hero's side can attack and destroy (a banner, a pylon, an urn): a passive monster
    /// with its own primitive look. <paramref name="life"/> is absolute. <paramref name="destroyed"/> runs when it is broken
    /// by the hero (not when the fight clears it away).
    /// </summary>
    public Monster Prop(string name, Vector2 at, float life, Node3D visual, Action destroyed = null, float radius = 0.7f, float height = 2f)
    {
        if (World.Root == null) return null;
        at = Walk(at);
        var m = Monster.Spawn(NamedProps.Def(radius), _level, MonTier.Normal, at, Faction.Monster);
        m.DisplayName = name; m.Title = null;
        m.MaxLife = Mathf.Max(1f, Mathf.Round(life)); m.Life = m.MaxLife;
        m.NoLoot = true; m.XpValue = 0f;
        m.Radius = radius; m.HeightM = height;
        if (m.Model != null) m.Model.Scale = Vector3.One * 0.001f;       // the character model is only there for the code that expects one
        if (visual != null) m.AddChild(visual);
        m.ForceAggro();
        m.Busy(1e9f, null);                                               // it never acts
        m.Rotation = Vector3.Zero;
        Props.Add(m);
        _props[m] = new PropInfo { At = at, Destroyed = destroyed };
        NamedHub.Link(m, this);
        return m;
    }

    /// <summary>Lets a prop be carried about by the script instead of being held on its spot.</summary>
    public void Unpin(Monster prop) { if (prop != null && _props.TryGetValue(prop, out var i)) i.Pinned = false; }
    public void MoveProp(Monster prop, Vector2 to) { if (prop != null && IsInstanceValid(prop)) { prop.Pos2 = to; if (_props.TryGetValue(prop, out var i)) i.At = to; } }
    public void RemoveProp(Monster prop)
    {
        if (prop == null) return;
        _props.Remove(prop); Props.Remove(prop); NamedHub.Unlink(prop);
        if (IsInstanceValid(prop) && !prop.Dead) { World.Monsters.Remove(prop); prop.QueueFree(); }
    }

    private void PinProps()
    {
        if (_props.Count == 0) return;
        foreach (var kv in _props)
        {
            var m = kv.Key;
            if (m == null || !IsInstanceValid(m) || m.Dead) continue;
            m.Knock = Vector2.Zero;
            if (kv.Value.Pinned && m.Pos2.DistanceSquaredTo(kv.Value.At) > 0.0004f) m.Pos2 = kv.Value.At;
            if (!m.Attacking) m.Busy(1e9f, null);                         // a stun or a reset may have dropped it out of its trance
        }
    }

    // ───────────────────────── blocks: the hero ─────────────────────────

    public bool HeroIn(Vector2 c, float r) => HeroOk && World.P.Pos2.DistanceTo(c) <= r;

    /// <summary>Shoves or drags the hero (an impulse that decays like any knock-back). Dodging and unstoppable heroes shrug it off.</summary>
    public void Fling(Vector2 dir, float force)
    {
        var p = World.P;
        if (!HeroOk || p.Unstoppable || p.Invulnerable || dir.LengthSquared() < 1e-4f) return;
        p.Knock = dir.Normalized() * force;
    }

    /// <summary>Drags the hero toward a point so that they arrive about there (impulse sized to the distance).</summary>
    public void Drag(Vector2 to, float stopShort = 1.5f)
    {
        var p = World.P;
        if (!HeroOk) return;
        float dist = p.Pos2.DistanceTo(to) - stopShort;
        if (dist > 0.3f) Fling(to - p.Pos2, Mathf.Sqrt(56f * dist));       // knock-back velocity decays at 28 m/s²
    }

    /// <summary>A steady wind on the hero this frame.</summary>
    public void Blow(Vector2 dir, float speed, float dt)
    {
        var p = World.P;
        if (!HeroOk || p.Unstoppable || dir.LengthSquared() < 1e-4f) return;
        p.Move(dir.Normalized() * speed * dt);
    }

    public void Drain(float fracOfMax)
    {
        var p = World.P;
        if (!HeroOk) return;
        p.Resource = Mathf.Max(0f, p.Resource - p.Hero.MaxResource * fracOfMax);
    }

    /// <summary>Removes one timed buff from the hero (never auras or the harness's own). True when something was taken.</summary>
    public bool StripBuff()
    {
        var p = World.P;
        if (!HeroOk) return false;
        var buffs = p.Hero.Buffs;
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            var b = buffs[i];
            if (b.Def.Aura || b.Def.Id == "test_god" || b.Def.Deathless) continue;
            buffs.RemoveAt(i);
            p.Hero.Recalc();
            return true;
        }
        return false;
    }

    public void Status(StatusApply st) { if (HeroOk && Alive) World.P.ApplyStatus(st, M); }

    // ───────────────────────── blocks: light and fire ─────────────────────────

    public LightSpot Light(Vector2 at, float radius, bool lit, Node3D node = null)
    {
        var l = new LightSpot { Pos = at, Radius = radius, Lit = lit, Node = node };
        Lights.Add(l);
        return l;
    }

    private void TickLights(float dt)
    {
        for (int i = 0; i < Lights.Count; i++)
        {
            var l = Lights[i];
            if (l.Off <= 0f) continue;
            l.Off -= dt;
            if (l.Off <= 0f) SetLit(l, true);
        }
    }

    public void SetLit(LightSpot l, bool lit, float relightAfter = 0f)
    {
        l.Lit = lit; l.Off = lit ? 0f : relightAfter;
        if (l.Node != null && IsInstanceValid(l.Node)) NamedProps.SetFire(l.Node, lit);
    }

    /// <summary>True in the light of a lit brazier of this fight, or on / beside burning ground (anyone's).</summary>
    public bool InFirelight(Vector2 p)
    {
        foreach (var l in Lights) if (l.Lit && l.Pos.DistanceTo(p) <= l.Radius) return true;
        return NamedHub.FireNear(p, 1.2f);
    }

    public LightSpot NearestLight(Vector2 p, bool litOnly)
    {
        LightSpot best = null; float bd = float.MaxValue;
        foreach (var l in Lights)
        {
            if (litOnly && !l.Lit) continue;
            float d = l.Pos.DistanceSquaredTo(p);
            if (d < bd) { bd = d; best = l; }
        }
        return best;
    }

    private sealed class PlainScript : NamedScript { }
}
