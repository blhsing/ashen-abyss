using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AshenAbyss;

/// <summary>
/// Pause-aware delayed callbacks for elite and boss hazards ("the mortar lands in 1.1 s", "the wall sinks after 3.5 s").
/// The clock hangs on SceneTree.ProcessFrame, so an effect still resolves after the monster that started it has died or
/// while it is staggered; it stands still while the game is paused and drops everything that was started in a level
/// that has since been unloaded. Entries may carry an owner so a whole group can be cancelled at once.
/// </summary>
internal static class HazardClock
{
    private struct Item
    {
        public float T, Dur;
        public Action End;
        public Action<float> Tick;
        public object Owner;
        public Node Root;            // the level the entry was started in ...
        public NavGrid Grid;         // ... and its grid: when either changes, the entry is stale
        public bool Dead;
    }

    private static readonly List<Item> Items = new(64);
    private static readonly Predicate<Item> IsDead = it => it.Dead;
    private static SceneTree _tree;
    private static bool _warned;

    public static int Pending => Items.Count;

    /// <summary>Runs <paramref name="end"/> once after <paramref name="delay"/> unpaused seconds.</summary>
    public static void After(float delay, Action end, object owner = null) => Add(delay, null, end, owner);

    /// <summary>Calls <paramref name="tick"/> every unpaused frame with progress 0..1, then <paramref name="end"/>.</summary>
    public static void Run(float duration, Action<float> tick, Action end = null, object owner = null) => Add(duration, tick, end, owner);

    /// <summary>Drops every pending entry that was scheduled with this owner. Safe to call from inside a callback.</summary>
    public static void Cancel(object owner)
    {
        if (owner == null) return;
        for (int i = 0; i < Items.Count; i++)
        {
            var it = Items[i];
            if (it.Dead || !ReferenceEquals(it.Owner, owner)) continue;
            it.Dead = true; it.End = null; it.Tick = null; it.Owner = null;
            Items[i] = it;
        }
    }

    private static void Add(float dur, Action<float> tick, Action end, object owner)
    {
        if (tick == null && end == null) return;
        if (!Hook()) return;
        Items.Add(new Item { Dur = MathF.Max(dur, 0.0001f), Tick = tick, End = end, Owner = owner, Root = World.Root, Grid = World.Grid });
    }

    private static bool Hook()
    {
        if (_tree != null) return true;
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            _tree = tree;
            tree.ProcessFrame += OnFrame;
            Log.Info("hazard", "hazard clock attached to the scene tree");
            return true;
        }
        if (!_warned) { _warned = true; Log.Warn("hazard", "no scene tree available: delayed hazard dropped"); }
        return false;
    }

    private static void OnFrame()
    {
        if (Items.Count == 0) return;
        var game = Game.I;
        bool paused = game == null || game.Paused;
        float dt = paused ? 0f : (float)_tree.Root.GetProcessDeltaTime();      // the same step every _Process sees this frame
        Node root = World.Root;
        NavGrid grid = World.Grid;
        bool purge = false;
        int n = Items.Count;                          // entries added by callbacks this frame start on the next one
        for (int i = 0; i < n; i++)
        {
            var it = Items[i];
            if (it.Dead) { purge = true; continue; }
            if (!ReferenceEquals(it.Root, root) || !ReferenceEquals(it.Grid, grid))      // the level it belonged to is gone
            {
                it.Dead = true; it.End = null; it.Tick = null; it.Owner = null;
                Items[i] = it; purge = true;
                continue;
            }
            if (paused) continue;
            it.T += dt;
            Items[i] = it;
            bool done = it.T >= it.Dur;
            try
            {
                it.Tick?.Invoke(done ? 1f : it.T / it.Dur);
                if (!done) continue;
                var cur = Items[i];                   // re-read: the tick may have cancelled its own owner
                Action end = cur.Dead ? null : cur.End;
                cur.Dead = true; cur.End = null; cur.Tick = null; cur.Owner = null;
                Items[i] = cur; purge = true;
                end?.Invoke();
            }
            catch (Exception e)
            {
                Log.Error("hazard", "delayed effect failed", e);
                var bad = Items[i];
                bad.Dead = true; bad.End = null; bad.Tick = null; bad.Owner = null;
                Items[i] = bad; purge = true;
            }
        }
        if (purge) Items.RemoveAll(IsDead);
    }
}

/// <summary>
/// Behaviour of the sixteen elite affixes. <see cref="Apply"/> sets the static modifiers when the monster spawns,
/// <see cref="Tick"/> runs each affix's timer (stored in Monster.AffixT at the affix's index) while the monster is
/// awake, and the three hooks react to hits taken, attacks landed and death. Everything that can hurt is shown on the
/// ground for at least 0.6 s first, and nothing fires unless the hero is alive and within about 16 m.
/// </summary>
public static class EliteFx
{
    private enum Ax : byte
    {
        None, Molten, Frozen, Shocking, Plagued, Shadow, Waller, Vortex, Chilling,
        Shielded, Armored, Vampiric, Hasted, Summoner, Teleporter, Enraged, Splitting,
    }

    /// <summary>What an elite needs beyond one timer per affix.</summary>
    private sealed class State
    {
        public Ax[] Kinds = Array.Empty<Ax>();
        public bool Shield, Enraged;
        public float ShieldLeft;
        public Node3D Bubble, Aura;
        public int AuraPulse;
        public List<Monster> Summons;
    }

    private const float ActRange = 16f;      // nothing is cast at a hero farther away than this
    private const float Retry = 0.4f;        // how soon a ready affix looks again when it could not act
    private const int MaxSlots = 7;          // AffixT[7] is Monster's own kiting timer
    private const float ShieldDr = 0.3f;     // the bubble lets 30 % of the damage through
    private const float VampIcd = 0.8f;
    private const int SummonCap = 6;
    private const float CrystalH = 1.9f, PillarH = 2.8f, PillarR = 0.6f;

    private static readonly ConditionalWeakTable<Monster, State> States = new();
    private static readonly HashSet<string> Unknown = new();
    private static readonly Predicate<Monster> Gone = x => x == null || !GodotObject.IsInstanceValid(x) || x.Dead;
    private static readonly Color ChillColor = new(0.6f, 0.9f, 1f);
    private static readonly Color ShieldColor = new(1f, 0.87f, 0.5f);
    private static readonly Color RageColor = new(1f, 0.14f, 0.08f);
    private static bool _damageHooked;

    private static CylinderMesh _crystalMesh, _pillarMesh;
    private static SphereMesh _bubbleMesh;
    private static TorusMesh _auraMesh;
    private static StandardMaterial3D _crystalMat, _pillarMat, _bubbleMat, _auraMat;

    // ───────────────────────── public hooks ─────────────────────────

    /// <summary>Called once from Monster.Setup: static modifiers, first-use delays and persistent visuals.</summary>
    public static void Apply(Monster m)
    {
        if (m == null || m.Affixes.Count == 0 || States.TryGetValue(m, out _)) return;
        var s = new State();
        States.Add(m, s);
        if (m.Faction != Faction.Monster) return;
        try
        {
            if (!_damageHooked) { _damageHooked = true; World.Damage += OnWorldDamage; }
            var kinds = new Ax[Math.Min(m.Affixes.Count, MaxSlots)];
            for (int i = 0; i < kinds.Length; i++)
            {
                Ax k = Parse(m.Affixes[i]);
                kinds[i] = k;
                float first = 0f;
                switch (k)
                {
                    case Ax.None:
                        if (Unknown.Add(m.Affixes[i] ?? "")) Log.Warn("elite", $"unknown affix '{m.Affixes[i]}' on {m.Def?.Id}: ignored");
                        break;
                    case Ax.Molten: first = Rng.Range(2.5f, 4.5f); break;
                    case Ax.Frozen: first = Rng.Range(3.5f, 6f); break;
                    case Ax.Plagued: first = Rng.Range(1f, 2.5f); break;
                    case Ax.Shadow: first = Rng.Range(2.5f, 5f); break;
                    case Ax.Waller: first = Rng.Range(4f, 7f); break;
                    case Ax.Vortex: first = Rng.Range(3f, 6f); break;
                    case Ax.Chilling:
                        first = 0.5f;
                        s.Aura = MakeAura();
                        m.AddChild(s.Aura);
                        break;
                    case Ax.Shielded: first = Rng.Range(3f, 6f); break;
                    case Ax.Armored:
                        m.DrMul *= 0.5f; m.SpeedMul *= 0.82f; m.AtkSpeedMul *= 0.9f;
                        break;
                    case Ax.Hasted:
                        m.SpeedMul *= 1.3f; m.AtkSpeedMul *= 1.3f;
                        break;
                    case Ax.Summoner: first = Rng.Range(3f, 5f); break;
                    case Ax.Teleporter: first = Rng.Range(2f, 4f); break;
                }
                m.AffixT[i] = first;
            }
            s.Kinds = kinds;
            if (m.Affixes.Count > MaxSlots) Log.Warn("elite", $"{m.Def?.Id} has {m.Affixes.Count} affixes; only the first {MaxSlots} are active");
            if (Log.Verbose) Log.Debug("elite", $"{m.Def?.Id} lv{m.Level} {m.Tier} affixes=[{string.Join(",", m.Affixes)}] dr={m.DrMul:0.##} speed={m.SpeedMul:0.##}");
        }
        catch (Exception e)
        {
            Log.Error("elite", $"applying affixes to {m.Def?.Id} failed", e);
        }
    }

    /// <summary>Called every frame by an awake, unstunned monster that has affixes.</summary>
    public static void Tick(Monster m, float dt)
    {
        if (m == null || m.Dead || m.Faction != Faction.Monster) return;
        if (!States.TryGetValue(m, out var s))
        {
            Apply(m);
            if (!States.TryGetValue(m, out s)) return;
        }
        if (s.Kinds.Length == 0 || !m.Aggro) return;      // timers start counting when the pack wakes
        try { TickAffixes(m, s, dt); }
        catch (Exception e)
        {
            Log.Error("elite", $"affix tick failed on {m.Def?.Id} [{string.Join(",", m.Affixes)}]; its affixes are switched off", e);
            s.Kinds = Array.Empty<Ax>();
        }
    }

    /// <summary>Called from Monster.TakeHit for every direct (non-DoT) hit on any monster.</summary>
    public static void OnHitTaken(Monster m, Hit h)
    {
        if (m == null || m.Affixes.Count == 0 || m.Dead || m.Faction != Faction.Monster) return;
        if (!States.TryGetValue(m, out var s)) return;
        int i = Array.IndexOf(s.Kinds, Ax.Shocking);
        if (i < 0 || m.AffixT[i] > 0f) return;
        try
        {
            var p = World.P;
            if (p == null || p.Dead || World.Root == null || m.Pos2.DistanceTo(p.Pos2) > ActRange) return;
            m.AffixT[i] = 1.5f;
            ShockRing(m);
        }
        catch (Exception e) { Log.Error("elite", "shocking affix failed", e); }
    }

    /// <summary>Called when a monster's own melee attack connects.</summary>
    public static void OnAttackHit(Monster m)
    {
        if (m == null || m.Affixes.Count == 0 || m.Dead || m.Faction != Faction.Monster) return;
        if (!States.TryGetValue(m, out var s)) return;
        int i = Array.IndexOf(s.Kinds, Ax.Vampiric);
        if (i >= 0) VampHeal(m, i);
    }

    /// <summary>Called from Monster.Die for hostile monsters.</summary>
    public static void OnDeath(Monster m)
    {
        if (m == null || m.Affixes.Count == 0) return;
        try
        {
            HazardClock.Cancel(m);                         // casts that were still winding up die with the caster
            if (!States.TryGetValue(m, out var s)) return;
            if (s.Shield)
            {
                s.Shield = false;
                m.BlocksProjectiles = false;
                if (m.DrMul > 0f) m.DrMul /= ShieldDr;
            }
            if (s.Bubble != null && GodotObject.IsInstanceValid(s.Bubble)) s.Bubble.Visible = false;
            if (s.Aura != null && GodotObject.IsInstanceValid(s.Aura)) s.Aura.Visible = false;
            var kinds = s.Kinds;
            s.Kinds = Array.Empty<Ax>();
            if (World.Root == null) return;
            for (int i = 0; i < kinds.Length; i++)
            {
                if (kinds[i] == Ax.Molten) DeathBlast(m);
                else if (kinds[i] == Ax.Splitting) Split(m);
            }
        }
        catch (Exception e) { Log.Error("elite", $"affix death effects failed on {m.Def?.Id}", e); }
    }

    // ───────────────────────── frame ─────────────────────────

    private static void TickAffixes(Monster m, State s, float dt)
    {
        var p = World.P;
        if (p == null || World.Root == null) return;
        Vector2 mp = m.Pos2, pp = p.Pos2;
        float dist = mp.DistanceTo(pp);
        bool heroOk = !p.Dead && !p.Stealthed;
        bool active = heroOk && dist <= ActRange;
        var kinds = s.Kinds;
        for (int i = 0; i < kinds.Length; i++)
        {
            ref float t = ref m.AffixT[i];
            if (t > 0f) t -= dt;
            switch (kinds[i])
            {
                case Ax.Molten:
                    if (t > 0f) break;
                    if (!active) { t = Retry; break; }
                    t = Rng.Range(5f, 6.5f);
                    Mortar(m, pp);
                    break;

                case Ax.Frozen:
                    if (t > 0f) break;
                    if (!active) { t = Retry; break; }
                    t = Rng.Range(7.5f, 9f);
                    Crystals(m, pp);
                    break;

                case Ax.Plagued:
                    if (t > 0f) break;
                    if (!active) { t = Retry; break; }
                    t = 2.5f;
                    Pool(m, mp);
                    break;

                case Ax.Shadow:
                    if (t > 0f) break;
                    if (!active) { t = Retry; break; }
                    t = 5f;
                    Orb(m);
                    break;

                case Ax.Waller:
                    if (t > 0f) break;
                    if (!active || dist > 12f) { t = Retry; break; }
                    t = Rng.Range(10f, 12f);
                    RaiseWall(m, mp, pp);
                    break;

                case Ax.Vortex:
                    if (t > 0f) break;
                    if (!active || dist < 4.5f || dist > 14f || !Los(mp, pp)) { t = Retry; break; }
                    t = Rng.Range(8f, 10f);
                    Tether(m, mp, pp);
                    break;

                case Ax.Chilling:
                    if (t > 0f) break;
                    t = 0.5f;
                    if (!p.Dead && dist <= 5f + p.Radius)
                    {
                        // builds to a heavy chill but never freezes on its own
                        bool build = p.StMag[(int)StatusId.Chill] < 0.5f;
                        p.ApplyStatus(St.Chill(build ? 0.1f : 0f, 1.2f), m);
                    }
                    if (active && ++s.AuraPulse >= 5)
                    {
                        s.AuraPulse = 0;
                        Fx.Ring(m.GlobalPosition + Vector3.Up * 0.15f, 5f, ChillColor, 0.9f, Sigil.Frost);
                    }
                    break;

                case Ax.Shielded:
                    if (s.Shield)
                    {
                        s.ShieldLeft -= dt;
                        if (s.ShieldLeft <= 0f && m.DrMul > 0f) ShieldOff(m, s);
                    }
                    else if (t <= 0f)
                    {
                        if (heroOk && dist <= 24f && m.DrMul > 0f) { t = 10f; ShieldOn(m, s); }
                        else t = Retry;
                    }
                    break;

                case Ax.Summoner:
                    if (t > 0f) break;
                    if (!active) { t = Retry; break; }
                    t = RaiseDead(m, s) ? 7f : 2f;
                    break;

                case Ax.Teleporter:
                    if (t > 0f) break;
                    if (!active || dist <= 6f) { t = Retry; break; }
                    t = Blink(m, pp) ? 6f : 1f;
                    break;

                case Ax.Enraged:
                    if (!s.Enraged)
                    {
                        if (m.Life < m.MaxLife * 0.3f) { EnrageOn(m, s); t = 1.5f; }
                    }
                    else if (t <= 0f)
                    {
                        t = 1.5f;
                        m.Enrage(2f);                          // Monster's own haste: faster feet and swings
                        if (dist <= 30f) Fx.Ring(m.GlobalPosition + Vector3.Up * 0.2f, m.Radius + 1.2f, RageColor, 0.6f);
                    }
                    break;
            }
        }
    }

    // ───────────────────────── hazards ─────────────────────────

    /// <summary>Molten: a lobbed mortar that lands on a marked circle and leaves burning ground.</summary>
    private static void Mortar(Monster m, Vector2 target)
    {
        const float R = 2.6f, Delay = 1.1f;
        Fx.Telegraph(target, R, Delay, Palette.Danger, Sigil.Fire);
        Fx.Meteor(target, Delay, Element.Fire, R);
        Fx.CastGlow(m, Element.Fire);
        Snd.At("fire_cast", m.GlobalPosition, 0.6f);
        HazardClock.After(Delay, () =>
        {
            Fx.Burst(World.To3(target, 0.2f), Element.Fire, R, 0.9f);
            Snd.At("fire_impact", World.To3(target), 0.8f);
            if (!Valid(m)) return;
            Combat.MonsterArea(m, target, R, 1f, Element.Fire, St.Burn(0.4f, 3f));
            GroundZone.SpawnMonster(m, target, R - 0.3f, 4.5f, 0.18f, Element.Fire);
        });
    }

    /// <summary>Molten: the corpse glows for 1.2 s, then bursts.</summary>
    private static void DeathBlast(Monster m)
    {
        const float R = 4.2f, Delay = 1.2f;
        Vector2 at = m.Pos2;
        Fx.Telegraph(at, R, Delay, Palette.Danger, Sigil.Fire);
        Snd.At("telegraph_warn", m.GlobalPosition, 0.7f);
        HazardClock.Run(Delay, _ =>
        {
            // hold the ash-dissolve back so the body is still there (and still the damage source) when it blows
            if (!Valid(m) || m.Model == null) return;
            if (m.Model.DissolveSpeed > 0.5f) m.Model.DissolveSpeed = 0.5f;
            m.Model.Flash(new Color(1f, 0.45f, 0.1f), 0.8f);
        }, () =>
        {
            Fx.Burst(World.To3(at, 0.3f), Element.Fire, R, 1.3f);
            Fx.Shake(0.3f, 0.25f);
            Snd.At("meteor_explode", World.To3(at), 0.9f);
            if (Valid(m)) Combat.MonsterArea(m, at, R, 1.5f, Element.Fire, St.Burn(0.5f, 3f));
        });
    }

    /// <summary>Frozen: three crystals grow around the hero and burst after 1.6 s, freezing what they catch.</summary>
    private static void Crystals(Monster m, Vector2 heroPos)
    {
        const float R = 2.1f, Delay = 1.6f;
        var grid = World.Grid;
        float a0 = Rng.Range(0f, Mathf.Tau);
        Fx.CastGlow(m, Element.Frost);
        Snd.At("frost_cast", m.GlobalPosition, 0.6f);
        for (int k = 0; k < 3; k++)
        {
            Vector2 at = heroPos + Vector2.Right.Rotated(a0 + k * Mathf.Tau / 3f) * (k == 0 ? 0.6f : Rng.Range(2f, 3.2f));
            if (grid != null && !grid.WalkAt(at)) at = heroPos;
            float fuse = Delay + k * 0.15f;
            Fx.Telegraph(at, R, fuse, Palette.Danger, Sigil.Frost);
            Node3D crystal = MakeSpike(CrystalMesh, CrystalMat, CrystalH, at);
            HazardClock.Run(fuse, g =>
            {
                if (!GodotObject.IsInstanceValid(crystal)) return;
                float w = 0.55f + 0.45f * g;
                crystal.Scale = new Vector3(w, Mathf.Max(0.02f, g), w);
            }, () =>
            {
                if (GodotObject.IsInstanceValid(crystal)) crystal.QueueFree();
                Fx.Burst(World.To3(at, 0.3f), Element.Frost, R, 0.8f);
                Snd.At("frost_shatter", World.To3(at), 0.7f);
                if (Valid(m)) Combat.MonsterArea(m, at, R, 0.8f, Element.Frost, St.Freeze(1.2f));
            });
        }
    }

    /// <summary>Shocking: a ring of slow sparks, half a heartbeat after the blow that provoked it.</summary>
    private static void ShockRing(Monster m)
    {
        const float Warn = 0.6f;
        const int Sparks = 8;
        Fx.Telegraph(m.Pos2, m.Radius + 1.4f, Warn, Palette.Danger, Sigil.Lightning);
        HazardClock.After(Warn, () =>
        {
            if (!Alive(m)) return;
            var g = m.GlobalPosition;
            var origin = new Vector3(g.X, 1f, g.Z);
            float a0 = Rng.Range(0f, Mathf.Tau);
            for (int k = 0; k < Sparks; k++)
                Projectile.SpawnMonster(m, origin, Vector2.Right.Rotated(a0 + k * Mathf.Tau / Sparks), 5.5f, 9f, 0.4f, Element.Lightning, null, 0.35f);
            Fx.Burst(m.Chest, Element.Lightning, 1.8f, 0.5f);
            Snd.At("lightning_zap", g, 0.7f);
        }, m);
    }

    /// <summary>Plagued: the ground under the elite sours into a poison pool.</summary>
    private static void Pool(Monster m, Vector2 at)
    {
        const float R = 2.2f, Warn = 0.7f;
        Fx.Telegraph(at, R, Warn, Palette.Danger, Sigil.Poison);
        HazardClock.After(Warn, () =>
        {
            if (!Valid(m)) return;
            GroundZone.SpawnMonster(m, at, R, 6f, 0.16f, Element.Poison, St.Poison(1.5f, 3f));
            Snd.At("poison_splash", World.To3(at), 0.5f);
        });
    }

    /// <summary>Shadow: a slow orb that curves after the hero.</summary>
    private static void Orb(Monster m)
    {
        const float Warn = 0.6f;
        Fx.Telegraph(m.Pos2, m.Radius + 1f, Warn, Palette.Danger, Sigil.Shadow);
        Fx.CastGlow(m, Element.Shadow);
        HazardClock.After(Warn, () =>
        {
            if (!Alive(m)) return;
            var p = World.P;
            if (p == null || p.Dead) return;
            Vector2 dir = p.Pos2 - m.Pos2;
            if (dir.LengthSquared() < 0.01f) dir = m.Forward;
            var g = m.GlobalPosition;
            var orb = Projectile.SpawnMonster(m, new Vector3(g.X, 1.3f, g.Z), dir, 5.2f, 28f, 1.1f, Element.Shadow, null, 0.6f);
            orb.Homing = 2.4f;
            Snd.At("shadow_cast", g, 0.7f);
        }, m);
    }

    /// <summary>Waller: a ring of stone spikes around the hero with one opening, which faces the elite.</summary>
    private static void RaiseWall(Monster m, Vector2 elitePos, Vector2 centre)
    {
        const float R = 3.7f, Warn = 0.8f, Up = 3.5f;
        const int Slots = 14;
        var grid = World.Grid;
        if (grid == null) return;
        Vector2 toElite = elitePos - centre;
        float gap = toElite.LengthSquared() > 0.01f ? Mathf.Atan2(toElite.Y, toElite.X) : Rng.Range(0f, Mathf.Tau);
        Fx.TelegraphRing(centre, R - 0.7f, R + 0.7f, Warn, Palette.Danger, Sigil.Blade);
        Fx.CastGlow(m, Element.Physical);
        Snd.At("earth_cast", World.To3(centre), 0.7f);
        HazardClock.After(Warn, () =>
        {
            var spots = new List<Vector2>(Slots);
            var spikes = new List<Node3D>(Slots);
            for (int k = 1; k < Slots - 1; k++)            // slots 0 and 13 straddle the gap and stay open
            {
                float a = gap + (k + 0.5f) * Mathf.Tau / Slots;
                Vector2 at = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R;
                if (!grid.WalkAt(at)) continue;
                grid.AddObstacle(at, PillarR);
                spots.Add(at);
                spikes.Add(MakeSpike(PillarMesh, PillarMat, PillarH, at));
            }
            if (spots.Count == 0) return;
            Snd.At("earth_impact", World.To3(centre), 0.8f);
            Fx.Shake(0.15f, 0.2f);
            HazardClock.Run(0.28f, g => SetHeight(spikes, g));
            HazardClock.After(Up, () =>
            {
                for (int k = 0; k < spots.Count; k++) grid.RemoveObstacle(spots[k]);
                HazardClock.Run(0.35f, g => SetHeight(spikes, 1f - g), () =>
                {
                    for (int k = 0; k < spikes.Count; k++)
                        if (GodotObject.IsInstanceValid(spikes[k])) spikes[k].QueueFree();
                });
            });
        }, m);
    }

    /// <summary>Vortex: a marked lane toward the hero; whoever still stands in it when it fills is dragged to the elite.</summary>
    private static void Tether(Monster m, Vector2 from, Vector2 heroPos)
    {
        const float Width = 1.6f, Warn = 0.7f;
        Vector2 dir = heroPos - from;
        float len = dir.Length();
        if (len < 0.1f) return;
        dir /= len;
        float reach = Mathf.Min(len + 2.5f, 15f);
        Fx.TelegraphLine(from, dir, reach, Width, Warn, Palette.Danger, Sigil.Shadow);
        Fx.CastGlow(m, Element.Shadow);
        Snd.At("wind_cast", m.GlobalPosition, 0.7f);
        HazardClock.After(Warn, () =>
        {
            if (!Alive(m) || m.Stunned) return;
            var hero = World.P;
            if (hero == null || hero.Dead || hero.Invulnerable) return;
            Vector2 a = m.Pos2, b = a + dir * reach;
            if (World.DistToSegment(hero.Pos2, a, b) > Width * 0.5f + hero.Radius) return;      // stepped out of the lane
            float pull = hero.Pos2.DistanceTo(a) - (m.Radius + hero.Radius + 0.9f);
            if (pull <= 0.3f) return;
            Fx.Beam(m.Chest, hero.Chest, Element.Shadow, 0.3f, 0.35f);
            Snd.At("chain", hero.GlobalPosition, 0.8f);
            hero.ApplyStatus(St.Pull(Mathf.Sqrt(56f * pull)), m);     // knock-back velocity decays at 28 m/s²
        }, m);
    }

    private static void ShieldOn(Monster m, State s)
    {
        s.Shield = true; s.ShieldLeft = 4f;
        m.BlocksProjectiles = true;
        m.DrMul *= ShieldDr;
        if (s.Bubble == null || !GodotObject.IsInstanceValid(s.Bubble))
        {
            float w = m.Radius + 0.9f, h = Mathf.Max(1.2f, m.HeightM * 0.62f);
            s.Bubble = new MeshInstance3D
            {
                Mesh = BubbleMesh, MaterialOverride = BubbleMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0f, m.HeightM * 0.5f, 0f), Scale = new Vector3(w, h, w),
            };
            m.AddChild(s.Bubble);
        }
        s.Bubble.Visible = true;
        Snd.At("barrier_up", m.GlobalPosition, 0.7f);
        Fx.Ring(m.GlobalPosition + Vector3.Up * 0.2f, m.Radius + 1.6f, ShieldColor, 0.5f);
    }

    private static void ShieldOff(Monster m, State s)
    {
        s.Shield = false; s.ShieldLeft = 0f;
        m.BlocksProjectiles = false;
        m.DrMul /= ShieldDr;
        if (s.Bubble != null && GodotObject.IsInstanceValid(s.Bubble)) s.Bubble.Visible = false;
        Snd.At("barrier_break", m.GlobalPosition, 0.6f);
    }

    private static void VampHeal(Monster m, int slot)
    {
        if (m.AffixT[slot] > 0f || m.Dead) return;
        m.AffixT[slot] = VampIcd;
        if (m.Life >= m.MaxLife) return;
        // A share of an ordinary monster's life per bite, whatever the biter's own life is: 5 % of max life was 3 500 a hit on
        // a mini-boss (×16 life) — more than a hero of that level deals between two bites, so the fight could not be won.
        int wt = Math.Clamp(Game.I?.WorldTier ?? 0, 0, Balance.TierHp.Length - 1);
        float heal = Balance.MonsterHp(m.Level) * Balance.TierHp[wt] * 0.18f;
        m.Heal(Mathf.Min(heal, m.MaxLife * 0.05f));
        Fx.Heal(m);
    }

    /// <summary>Vampiric elites that fight at range never reach Monster's melee hook, so their hits are seen here.</summary>
    private static void OnWorldDamage(Actor victim, Hit h, float amount)
    {
        try
        {
            if (amount <= 0f || h.Dot || victim == null || victim.Faction != Faction.Player) return;
            if (h.Source is not Monster m || m.Affixes.Count == 0 || m.Dead || m.Faction != Faction.Monster) return;
            if (!GodotObject.IsInstanceValid(m) || !States.TryGetValue(m, out var s)) return;
            int i = Array.IndexOf(s.Kinds, Ax.Vampiric);
            if (i >= 0) VampHeal(m, i);
        }
        catch (Exception e) { Log.Error("elite", "vampiric affix failed", e); }
    }

    /// <summary>Summoner: two skeletons claw out of the ground. Returns false when the six-skeleton limit is reached.</summary>
    private static bool RaiseDead(Monster m, State s)
    {
        var def = MonsterDb.Get("skeleton");
        if (def == null) return false;
        s.Summons ??= new List<Monster>(SummonCap);
        s.Summons.RemoveAll(Gone);
        int n = Math.Min(2, SummonCap - s.Summons.Count);
        if (n <= 0) return false;
        const float Warn = 0.8f;
        var grid = World.Grid;
        var list = s.Summons;
        Fx.CastGlow(m, Element.Shadow);
        Snd.At("summon_raise", m.GlobalPosition, 0.7f);
        for (int k = 0; k < n; k++)
        {
            Vector2 at = grid != null
                ? grid.RandomWalkNear(m.Pos2, 1.6f, 3.6f)
                : m.Pos2 + Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau)) * 2.5f;
            Fx.Telegraph(at, 0.9f, Warn, Palette.Danger, Sigil.Shadow);
            HazardClock.After(Warn, () =>
            {
                if (!Alive(m)) return;
                list.RemoveAll(Gone);
                if (list.Count >= SummonCap) return;
                var add = Monster.Spawn(def, m.Level, MonTier.Normal, at, Faction.Monster);
                add.NoLoot = true; add.XpValue = 0f; add.Pack = m.Pack;
                add.ForceAggro();
                list.Add(add);
                Fx.Dust(World.To3(at));
            }, m);
        }
        return true;
    }

    /// <summary>Teleporter: marks a spot beside the hero, then appears on it.</summary>
    private static bool Blink(Monster m, Vector2 heroPos)
    {
        const float Warn = 0.6f;
        var grid = World.Grid;
        Vector2 to = grid != null
            ? grid.RandomWalkNear(heroPos, 1.8f, 3.2f)
            : heroPos + Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau)) * 2.4f;
        if (to.DistanceSquaredTo(heroPos) < 1f) return false;      // no free spot next to the hero right now
        Fx.Telegraph(to, m.Radius + 0.7f, Warn, Palette.Danger);
        HazardClock.After(Warn, () =>
        {
            if (!Alive(m) || m.Stunned) return;
            Fx.Teleport(m.Chest, Element.Shadow);
            m.Pos2 = to;
            Fx.Teleport(m.Chest, Element.Shadow);
            Snd.At("teleport", m.GlobalPosition, 0.6f);
        }, m);
        return true;
    }

    private static void EnrageOn(Monster m, State s)
    {
        s.Enraged = true;
        m.BaseDamage *= 1.35f;
        m.Enrage(2f);
        m.Model?.SetRim(RageColor * 2.2f);
        Fx.Burst(m.Chest, Element.Fire, 2.4f, 0.8f);
        Snd.At("war_cry", m.GlobalPosition, 0.8f);
        if (Log.Verbose) Log.Debug("elite", $"{m.Def?.Id} enraged at {m.Life / Mathf.Max(1f, m.MaxLife):P0} life");
    }

    /// <summary>Splitting: two ordinary copies crawl out of the remains. They carry no affixes, so they cannot split again.</summary>
    private static void Split(Monster m)
    {
        var def = m.Def;
        if (def == null) return;
        int level = m.Level, pack = m.Pack;
        Vector2 at = m.Pos2;
        var rim = (MonsterDb.Affix("splitting")?.Color ?? new Color(0.9f, 0.6f, 0.9f)) * 0.6f;
        Fx.Burst(m.Chest, Element.Shadow, 2f, 0.6f);
        HazardClock.After(0.45f, () =>
        {
            var grid = World.Grid;
            for (int k = 0; k < 2; k++)
            {
                Vector2 pos = grid != null
                    ? grid.RandomWalkNear(at, 0.8f, 1.9f)
                    : at + Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau)) * 1.2f;
                var copy = Monster.Spawn(def, level, MonTier.Normal, pos, Faction.Monster);
                copy.NoLoot = true; copy.XpValue = 0f; copy.Pack = pack;
                copy.ForceAggro();
                copy.Radius *= 0.85f;
                if (copy.Model != null) { copy.Model.Scale *= 0.85f; copy.Model.SetRim(rim); }
                Fx.Dust(World.To3(pos));
            }
            Snd.At("body_fall", World.To3(at), 0.6f);
            if (Log.Verbose) Log.Debug("elite", $"{def.Id} split into two at ({at.X:F0},{at.Y:F0})");
        });
    }

    // ───────────────────────── helpers and visuals ─────────────────────────

    private static bool Valid(Monster m) => m != null && GodotObject.IsInstanceValid(m);
    private static bool Alive(Monster m) => Valid(m) && !m.Dead;
    private static bool Los(Vector2 a, Vector2 b) => World.Grid == null || World.Grid.LineShot(a, b);

    private static Ax Parse(string id) => id switch
    {
        "molten" => Ax.Molten, "frozen" => Ax.Frozen, "shocking" => Ax.Shocking, "plagued" => Ax.Plagued,
        "shadow" => Ax.Shadow, "waller" => Ax.Waller, "vortex" => Ax.Vortex, "chilling" => Ax.Chilling,
        "shielded" => Ax.Shielded, "armored" => Ax.Armored, "vampiric" => Ax.Vampiric, "hasted" => Ax.Hasted,
        "summoner" => Ax.Summoner, "teleporter" => Ax.Teleporter, "enraged" => Ax.Enraged, "splitting" => Ax.Splitting,
        _ => Ax.None,
    };

    private static void SetHeight(List<Node3D> nodes, float k)
    {
        float y = Mathf.Clamp(k, 0.02f, 1f);
        for (int i = 0; i < nodes.Count; i++)
            if (GodotObject.IsInstanceValid(nodes[i])) nodes[i].Scale = new Vector3(1f, y, 1f);
    }

    /// <summary>A spike standing on the ground at a point, flat until something scales it up. Mesh and material are shared.</summary>
    private static Node3D MakeSpike(Mesh mesh, Material mat, float height, Vector2 at)
    {
        var root = new Node3D();
        root.AddChild(new MeshInstance3D
        {
            Mesh = mesh, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0f, height * 0.5f, 0f),
        });
        World.Root.AddChild(root);
        root.GlobalPosition = World.To3(at);
        root.Rotation = new Vector3(0f, Rng.Range(0f, Mathf.Tau), 0f);
        root.Scale = new Vector3(1f, 0.02f, 1f);
        return root;
    }

    private static Node3D MakeAura() => new MeshInstance3D
    {
        Mesh = AuraMesh, MaterialOverride = AuraMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        Position = new Vector3(0f, 0.12f, 0f), Scale = new Vector3(1f, 0.3f, 1f),
    };

    private static Mesh CrystalMesh => _crystalMesh ??= new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.42f, Height = CrystalH, RadialSegments = 5, Rings = 1 };
    private static Mesh PillarMesh => _pillarMesh ??= new CylinderMesh { TopRadius = 0.14f, BottomRadius = PillarR + 0.05f, Height = PillarH, RadialSegments = 6, Rings = 1 };
    private static Mesh BubbleMesh => _bubbleMesh ??= new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 24, Rings = 12 };
    private static Mesh AuraMesh => _auraMesh ??= new TorusMesh { InnerRadius = 4.84f, OuterRadius = 5f, Rings = 56, RingSegments = 6 };

    private static Material CrystalMat => _crystalMat ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(0.55f, 0.85f, 1f, 0.85f), Roughness = 0.15f, Metallic = 0.1f,
        EmissionEnabled = true, Emission = new Color(0.3f, 0.65f, 1f), EmissionEnergyMultiplier = 1.6f,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
    };

    private static Material PillarMat => _pillarMat ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(0.4f, 0.38f, 0.35f), Roughness = 0.95f, Metallic = 0f,
    };

    private static Material BubbleMat => _bubbleMat ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(1f, 0.85f, 0.45f, 0.22f),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    private static Material AuraMat => _auraMat ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(0.55f, 0.85f, 1f, 0.55f),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
    };
}
