using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// A monster or a player minion. One state machine (spawn → idle → chase → wind-up → recover) specialised by role:
/// melee, ranged, caster, exploder, teleporter, bruiser (telegraphed area swings), support and leaper.
/// Elite affixes are layered on by EliteFx; bosses hand control to a BossBrain.
/// </summary>
public partial class Monster : Actor
{
    public MonsterDef Def;
    public MonTier Tier;
    public readonly List<string> Affixes = new();
    public float BaseDamage, XpValue, SpeedMul = 1f, AtkSpeedMul = 1f, DrMul = 1f, DmgMul = 1f;
    public string Title;
    public bool MiniBoss, BlocksProjectiles, NoLoot, Objective;
    public int Pack = -1;
    public readonly float[] AffixT = new float[8];
    // minion side
    public string SummonSkill;
    public float MinionCoef, LifeTime;
    public SkillDef MinionSkill;
    public Action<Vector2> OnExpire;
    public bool Cleave, Taunter;
    private float _enrage, _tauntT;
    // boss side
    public BossBrain Brain;
    public float Stagger, StaggerImmune, StaggerLeft;
    public bool Staggered => StaggerLeft > 0f;

    private enum Mode { Spawn, Idle, Chase, Windup, Recover, Busy, Dead }
    private Mode _st = Mode.Idle;
    private float _t, _cd, _special, _repath, _deadT, _kite, _idleSfx, _flinchIcd;
    private Actor _target;
    private Vector2 _attackPos, _attackDir;
    private readonly List<Vector2> _path = new();
    private int _pathI;
    private bool _aggro, _contactDone;
    private Vector2 _home;
    private Action _busyEnd;
    private Node3D _telegraph;
    private static readonly List<Actor> Tmp = new();
    public bool Aggro => _aggro;
    public override bool IsElite => Tier is MonTier.Elite or MonTier.Champion || MiniBoss;
    public override bool IsBoss => Tier == MonTier.Boss || MiniBoss;
    public override bool Unstoppable => _st == Mode.Spawn || (Tier == MonTier.Minion && Def.Poise > 50);
    public bool Attacking => _st is Mode.Windup or Mode.Busy;

    public static Monster Spawn(MonsterDef def, int level, MonTier tier, Vector2 pos, Faction faction, IEnumerable<string> affixes = null, bool miniBoss = false)
    {
        var m = new Monster { Def = def, Tier = tier, Level = level, Faction = faction, MiniBoss = miniBoss, Name = def.Id };
        if (affixes != null) m.Affixes.AddRange(affixes);
        World.Root.AddChild(m);
        m.GlobalPosition = World.To3(pos);
        m.Setup();
        (faction == Faction.Monster ? World.Monsters : World.Minions).Add(m);
        return m;
    }

    private void Setup()
    {
        int wt = Game.I.WorldTier;
        float hpMul = Tier switch { MonTier.Champion => 2.4f, MonTier.Elite => 5.5f, _ => 1f }, dmgMul = Tier switch { MonTier.Champion => 1.2f, MonTier.Elite => 1.35f, _ => 1f };
        float scale = Def.Scale * (Tier switch { MonTier.Champion => 1.1f, MonTier.Elite => 1.2f, _ => 1f });
        // elites and named leaders grow into their full life over the first ten levels (a level-2 hero deals a fifth of a monster's
        // life per second: a ×16 stronghold boss was a three-minute fight that killed the hero several times)
        float young = Mathf.Clamp((Level - 1) / 9f, 0f, 1f);
        if (Tier == MonTier.Elite) hpMul = Mathf.Lerp(3.5f, 5.5f, young);
        if (MiniBoss) { hpMul = Mathf.Lerp(8f, 13f, young); dmgMul = Mathf.Lerp(1.25f, 1.5f, young); scale = Def.Scale * 1.45f; }
        // a bruiser already carries three times the life of a foot soldier: as a champion, elite or named leader the two
        // factors used to multiply (an elite lava brute had 19× a normal monster's life — a full minute at level 50)
        if ((Tier is MonTier.Elite or MonTier.Champion || MiniBoss) && Def.Hp > 1f) hpMul /= Mathf.Sqrt(Def.Hp);
        if (Def.AI == MonAI.Boss) { hpMul *= Mathf.Lerp(0.8f, 1f, young); dmgMul *= Mathf.Lerp(0.75f, 1f, young); }   // the first region boss, met at level 4-6 in starter gear, is a lesson and not a wall
        MaxLife = Mathf.Round(Balance.MonsterHp(Level) * Def.Hp * hpMul * (Faction == Faction.Monster ? Balance.TierHp[wt] : 1f));
        Life = MaxLife;
        BaseDamage = Balance.MonsterDmg(Level) * Def.Dmg * dmgMul * Balance.TierDmg[wt];
        // a mini-boss is worth three elites; its own factor replaces the tier factor (the two used to multiply to 112× a normal kill,
        // more than a region boss)
        XpValue = Balance.MonsterXp(Level) * Def.Xp * (MiniBoss ? Balance.MiniBossXp : Tier switch { MonTier.Champion => 3f, MonTier.Elite => 8f, _ => 1f });
        Radius = Def.Radius * scale;
        DisplayName = Def.Name;
        Model = CharModel.Create(Def.Model);
        AddChild(Model);
        Model.Scale = Vector3.One * scale;
        HeightM = Model.Height * scale;
        if (Def.Tint != Colors.White) Model.SetTint(Def.Tint);
        _home = Pos2;
        Facing = Rng.Range(0f, Mathf.Tau); Rotation = new Vector3(0, Facing, 0);
        _cd = Rng.Range(0.2f, 1.2f); _special = Rng.Range(2f, 5f); _idleSfx = Rng.Range(2f, 12f);
        for (int i = 0; i < AffixT.Length; i++) AffixT[i] = Rng.Range(2f, 5f);

        if (Affixes.Count > 0 || MiniBoss)
        {
            var c = Affixes.Count > 0 ? MonsterDb.Affix(Affixes[0])?.Color ?? Palette.Danger : Palette.Danger;
            Model.SetRim(c * (Tier == MonTier.Elite || MiniBoss ? 1.4f : 0.9f));
            Title = string.Join("・", Affixes.Select(a => MonsterDb.Affix(a)?.Name ?? a));
            EliteFx.Apply(this);
        }
        if (Faction == Faction.Player) { Model.SetRim(new Color(0.2f, 0.9f, 0.7f) * 0.8f); _aggro = true; }
        if (Def.AI == MonAI.Boss && Faction == Faction.Monster) { Tier = MonTier.Boss; Brain = new BossBrain(this); XpValue = Balance.MonsterXp(Level) * Def.Xp; }
        if (Def.RiseFromGround && Model.Has("LayToIdle"))
        {
            _st = Mode.Spawn; _t = Model.Len("LayToIdle") / 1.6f;
            // the dead lie where they fell until their pack notices the hero, then claw their way up; summoned ones rise at once
            if (Faction == Faction.Monster) { _buried = true; Model.Hold("LayToIdle", 0f); }
            else Model.Play("LayToIdle", 1.6f, 0f, true);
        }
        else Model.Play(Def.Idle);
    }

    /// <summary>One line for test logs: what the state machine is doing right now.</summary>
    public string DebugState => $"{_st} aggro={_aggro} target={(_target == null ? "none" : _target == World.P ? "hero" : "other")} cd={_cd:0.0} path={_pathI}/{_path.Count} blocked={_blocked:0.0}s " +
        $"at=({Pos2.X:0.0},{Pos2.Y:0.0}) y={GlobalPosition.Y:0.00} clip={Model?.Current} rooted={Rooted} stunned={Stunned}";

    public void Enrage(float dur) => _enrage = Mathf.Max(_enrage, dur);
    public void ForceAggro() => _aggro = true;

    // ───────────────────────── frame ─────────────────────────

    private static int _tickErrors;

    public override void _Process(double delta)
    {
        if (Game.I.Paused) return;
        long t0 = Prof.Now;
        try { Tick((float)delta); }
        catch (Exception e)
        {
            // one broken monster must not take the frame (or the log) with it
            if (_tickErrors++ < 30) Log.Error("monster", $"update of {Def?.Id ?? "?"} ({_st}, affixes [{string.Join(",", Affixes)}]) failed", e);
            CancelTelegraph();
            if (_st is Mode.Windup or Mode.Busy) { _st = Mode.Chase; _busyEnd = null; _busyTick = null; }
        }
        long dtk = Prof.Now - t0;
        Prof.Monsters += dtk;
        if (_sleep == 0 && _st != Mode.Dead) { Prof.MonstersAwake += dtk; Prof.Awake++; }
    }

    private void Tick(float dt)
    {
        if (_st == Mode.Dead) { TickDead(dt); return; }
        var p = World.P;
        if (p == null) return;
        float dp = Pos2.DistanceTo(p.Pos2);
        if (Faction == Faction.Monster && dp > 46f && !IsBoss)           // asleep far from the hero: no AI, no animation work
        {
            int far = dp < 60f ? 1 : 2;
            if (_sleep != far) { _sleep = far; Model.Visible = far == 1; Model.SetAsleep(true); }
            return;
        }
        if (_sleep != 0) { _sleep = 0; Model.Visible = true; Model.SetAsleep(false); }
        TickStatuses(dt);
        if (Dead) return;
        if (_enrage > 0) _enrage -= dt;
        if (LifeTime > 0) { LifeTime -= dt; if (LifeTime <= 0) { Expire(); return; } }
        if (StaggerImmune > 0) StaggerImmune -= dt;
        if (StaggerLeft > 0)
        {
            StaggerLeft -= dt;
            Model.Play("Hit_Knockback", 0.25f, 0.2f);
            if (StaggerLeft <= 0) { Stagger = 0; StaggerImmune = 14f; Model.Play(Def.Idle); }
            return;
        }
        if (Stunned) { Model.Pause(Has(StatusId.Freeze)); if (!Has(StatusId.Freeze)) Model.Play("Hit_Head", 0.6f, 0.1f); CancelTelegraph(); if (_st is Mode.Windup) _st = Mode.Chase; return; }
        if (!_buried) Model.Pause(false);
        if (Affixes.Count > 0) EliteFx.Tick(this, dt);

        if (Brain != null) { Brain.Tick(dt); return; }

        switch (_st)
        {
            case Mode.Spawn:
                if (_buried)
                {
                    if (!_aggro && dp < 13.5f && !p.Dead && !p.Stealthed && (World.Grid == null || World.Grid.LineShot(Pos2, p.Pos2))) WakePack();
                    if (!_aggro) break;
                    _buried = false;
                    Model.Play("LayToIdle", 1.6f, 0f, true);
                    Snd.At(Def.Sfx + "_idle", GlobalPosition, 0.5f);
                }
                _t -= dt;
                if (_t <= 0) _st = _aggro ? Mode.Chase : Mode.Idle;
                break;
            case Mode.Idle: TickIdle(dt, dp); break;
            case Mode.Chase: TickChase(dt); break;
            case Mode.Windup: TickWindup(dt); break;
            case Mode.Recover: _t -= dt; if (_t <= 0) _st = Mode.Chase; break;
            case Mode.Busy: _t -= dt; if (_t <= 0) { _st = Mode.Chase; var e = _busyEnd; _busyEnd = null; e?.Invoke(); } break;
        }
    }

    private void TickIdle(float dt, float distToPlayer)
    {
        Model.Play(Def.Idle, 1f, 0.3f);
        _idleSfx -= dt;
        if (_idleSfx <= 0 && distToPlayer < 22f) { _idleSfx = Rng.Range(6f, 16f); Snd.At(Def.Sfx + "_idle", GlobalPosition, 0.5f); }
        if (_aggro) { _st = Mode.Chase; return; }
        var p = World.P;
        if (distToPlayer < 13.5f && !p.Dead && !p.Stealthed && (World.Grid == null || World.Grid.LineShot(Pos2, p.Pos2))) WakePack();
        else if (distToPlayer < 6f && !p.Dead && !p.Stealthed) WakePack();
    }

    public void WakePack()
    {
        if (_aggro) return;
        _aggro = true;
        if (Pack >= 0) foreach (var m in World.Monsters) if (m.Pack == Pack && !m._aggro) { m._aggro = true; }
        if (IsElite) Snd.At("elite_spawn", GlobalPosition, 0.7f);
    }

    private Actor PickTarget()
    {
        if (Faction == Faction.Monster)
        {
            if (TauntBy != null && !TauntBy.Dead && IsInstanceValid(TauntBy)) return TauntBy;
            var p = World.P;
            Actor best = p.Dead || p.Stealthed ? null : p;
            float bd = best == null ? 1e9f : Pos2.DistanceTo(p.Pos2) * 0.8f;
            foreach (var m in World.Minions)
            {
                if (m.Dead) continue;
                float d = Pos2.DistanceTo(m.Pos2) * (m.Taunter ? 0.5f : 1f);
                if (d < bd) { bd = d; best = m; }
            }
            return best;
        }
        // minion: enemies near the owner first
        var owner = World.P;
        Actor t = World.Nearest(Faction.Player, Pos2, 13f, a => a.Pos2.DistanceTo(owner.Pos2) < 18f);
        return t;
    }

    private float MoveSpeed => Def.Speed * SpeedMul * SlowMul * (_enrage > 0 ? 1.2f : 1f);
    private float AtkSpeed => Def.AnimSpeed * AtkSpeedMul * (_enrage > 0 ? 1.3f : 1f) * Mathf.Lerp(1f, 0.6f, 1f - SlowMul);

    private void TickChase(float dt)
    {
        _cd -= dt; _special -= dt; _kite -= dt;
        if (_target == null || _target.Dead || !IsInstanceValid(_target) || (_target is Player pl && pl.Stealthed)) _target = PickTarget();
        else if ((_repath -= dt) <= 0f) { _repath = 0.5f; _target = PickTarget(); _path.Clear(); }

        if (_target == null)
        {
            if (Faction == Faction.Player) FollowOwner(dt);
            else { Model.Play(Def.Idle, 1f, 0.3f); if (World.P.Dead) _aggro = false; if (!_aggro) _st = Mode.Idle; }
            return;
        }
        if (Faction == Faction.Player && Pos2.DistanceTo(World.P.Pos2) > 24f) { Pos2 = World.Grid?.RandomWalkNear(World.P.Pos2, 1.5f, 3f) ?? World.P.Pos2; return; }

        Vector2 to = _target.Pos2 - Pos2;
        float dist = to.Length();
        float reach = Def.Range + (IsRanged ? 0f : Radius + _target.Radius - 0.5f);
        bool los = !IsRanged || World.Grid == null || World.Grid.LineShot(Pos2, _target.Pos2);

        if (TrySpecial(dist, los)) return;

        if (dist <= reach && los)
        {
            // kiting: turn and run (it used to back away while still turned to its target — a forward run played backward)
            if (IsRanged && dist < 4.5f && _kite <= 0 && !Rooted && Def.AI != MonAI.Support && Retreat(dt, to)) return;
            FaceDir(to, 1f - Mathf.Exp(-10f * dt));
            if (_cd <= 0 && !Has(StatusId.Fear)) { StartAttack(); return; }
            Model.Play(Def.Idle, 1f, 0.2f);
            Separate(dt);
            return;
        }
        if (Has(StatusId.Fear)) { MoveDir(-to.Normalized(), dt); return; }
        MoveToward(_target.Pos2, dt, IsRanged && los ? reach * 0.8f : 0f);     // without a line of fire, keep walking until there is one
    }

    private bool IsRanged => Def.AI is MonAI.Ranged or MonAI.Caster or MonAI.Teleporter or MonAI.Support;

    /// <summary>Runs from a target that came too close, for up to 1.6 s. False when there is nowhere to go.</summary>
    private bool Retreat(float dt, Vector2 toTarget)
    {
        Vector2 away = -toTarget.Normalized();
        Vector2 want = Pos2 + away * 3f;
        if (World.Grid != null && !World.Grid.WalkAt(want)) { _kite = 3f; return false; }
        MoveDir(away, dt);
        AffixT[7] += dt; if (AffixT[7] > 1.6f) { AffixT[7] = 0; _kite = 3f; }
        return true;
    }

    private void FollowOwner(float dt)
    {
        var o = World.P;
        float d = Pos2.DistanceTo(o.Pos2);
        if (d > 24f) { Pos2 = World.Grid?.RandomWalkNear(o.Pos2, 1.5f, 3f) ?? o.Pos2; return; }
        if (d > 4.5f) MoveToward(o.Pos2, dt, 3f);
        else { Model.Play(Def.Idle, 1f, 0.3f); Separate(dt); }
    }

    private void MoveToward(Vector2 goal, float dt, float stopAt)
    {
        if (Rooted) { Model.Play(Def.Idle, 1f, 0.2f); return; }
        var grid = World.Grid;
        Vector2 dir;
        if (grid == null || grid.LineWalk(Pos2, goal)) { dir = goal - Pos2; _path.Clear(); }
        else
        {
            if (_path.Count == 0 || _pathI >= _path.Count) { _path.Clear(); _path.AddRange(grid.Path(Pos2, goal)); _pathI = 0; }
            while (_pathI < _path.Count && Pos2.DistanceTo(_path[_pathI]) < 0.6f) _pathI++;
            if (_pathI >= _path.Count) { Model.Play(Def.Idle, 1f, 0.2f); return; }
            dir = _path[_pathI] - Pos2;
        }
        // "close enough" is measured to the goal itself: the next waypoint of a detour is usually nearer than the stop
        // distance, and an archer who had to walk round a tree used to stand there for ever
        if (Pos2.DistanceTo(goal) <= stopAt || dir.LengthSquared() < 1e-4f) { Model.Play(Def.Idle, 1f, 0.2f); return; }
        MoveDir(dir.Normalized(), dt);
    }

    private void MoveDir(Vector2 dir, float dt)
    {
        float sp = MoveSpeed;
        // neighbours nudge a walker sideways, never backward: the shove is capped well below its own pace
        Vector2 sep = SeparationPush() * 2.2f;
        float cap = sp * 0.55f;
        if (sep.LengthSquared() > cap * cap) sep = sep.Normalized() * cap;
        // turning round (a kiting archer, a feared monster): pivot first, then run — no running backward
        float align = Forward.Dot(dir);
        if (align < 0.2f) { sp *= 0.3f; FaceDir(dir, 1f - Mathf.Exp(-16f * dt)); }
        Vector2 moved = Move((dir * sp + sep) * dt);
        FaceDir(dir, 1f - Mathf.Exp(-9f * dt));
        float made = dt > 1e-5f ? moved.Length() / dt : sp;            // what the step really covered (sliding along a wall still counts)
        if (made < sp * 0.25f)
        {
            // held up by the crowd or a wall: wait instead of running on the spot
            _blocked += dt;
            if (_blocked > 0.25f) { Model.Play(Def.Idle, 1f, 0.25f); return; }
        }
        else _blocked = 0f;
        Model.Locomote(sp, 0.2f, Def.Walk, Def.Run);           // the gait that fits the speed, feet matched to the ground
        AnimCheck.Moving(Model, _animProbe, dt, false, Def.Id);
    }

    private Vector2 SeparationPush()
    {
        long t0 = Prof.Now;
        Vector2 push = SeparationPushCore();
        Prof.Separation += Prof.Now - t0;
        return push;
    }

    private Vector2 SeparationPushCore()
    {
        Vector2 push = Vector2.Zero;
        var list = Faction == Faction.Monster ? World.Monsters : World.Minions;
        foreach (var o in list)
        {
            if (o == this || o.Dead) continue;
            Vector2 d = Pos2 - o.Pos2;
            float min = Radius + o.Radius + 0.15f, len = d.Length();
            if (len < min && len > 0.001f) push += d / len * (min - len) / min;
        }
        var p = World.P;
        if (Faction == Faction.Monster && p != null)
        {
            Vector2 d = Pos2 - p.Pos2; float min = Radius + p.Radius, len = d.Length();
            if (len < min && len > 0.001f) push += d / len * (min - len) / min * 2f;
        }
        return push;
    }

    private void Separate(float dt)
    {
        // a gentle drift apart while standing: fast shoves would read as sliding in an idle pose
        var push = SeparationPush() * 2.5f;
        if (push.LengthSquared() > 1.1f * 1.1f) push = push.Normalized() * 1.1f;
        if (push.LengthSquared() > 0.0025f) Move(push * dt);
    }

    // ───────────────────────── role specials ─────────────────────────

    private bool TrySpecial(float dist, bool los)
    {
        if (_special > 0 || Faction != Faction.Monster) return false;
        switch (Def.AI)
        {
            case MonAI.Teleporter when dist < 5f:
            {
                Vector2 to = World.Grid?.RandomWalkNear(_target.Pos2, 8f, 11f) ?? Pos2;
                Fx.Teleport(Chest, Def.Element); Pos2 = to; Fx.Teleport(Chest, Def.Element);
                Snd.At("teleport", GlobalPosition, 0.6f);
                _special = 6f; _cd = 0.6f;
                return true;
            }
            case MonAI.Leaper when dist > 5f && dist < 12f && los:
            {
                _special = Rng.Range(5f, 7f);
                Vector2 land = _target.Pos2;
                _telegraph = Fx.Telegraph(land, 2.4f, 0.75f, Palette.Danger);
                FaceDir(land - Pos2);
                Model.Play("NinjaJump_Start", 1.5f, 0.1f, true);
                Vector2 from = Pos2;
                float t0 = 0;
                bool air = false;
                Busy(0.75f, () =>
                {
                    Pos2 = World.Grid?.NearestWalk(land) ?? land;
                    var gp = GlobalPosition; GlobalPosition = new Vector3(gp.X, 0, gp.Z);
                    Model.Play("NinjaJump_Land", 1.6f, 0.05f, true);
                    Combat.MonsterArea(this, land, 2.4f, 1.3f, Def.Element, Def.OnHit);
                    Fx.Burst(World.To3(land, 0.2f), Def.Element, 2.4f, 0.7f);
                    Snd.At(Def.Sfx + "_attack", GlobalPosition);
                    _cd = 0.8f;
                }, dtt =>
                {
                    t0 += dtt / 0.75f;
                    // the take-off clip is shorter than the flight: hold the airborne pose instead of dropping back to an idle in mid-air
                    if (t0 > 0.8f && !air) { air = true; Model.Play("NinjaJump_Idle_Loop", 1f, 0.08f); }
                    if (t0 > 0.35f) { float k = Mathf.Clamp((t0 - 0.35f) / 0.65f, 0, 1); var q = from.Lerp(land, k); GlobalPosition = new Vector3(q.X, Mathf.Sin(k * Mathf.Pi) * 2.2f, q.Y); }
                });
                return true;
            }
            case MonAI.Support:
            {
                Monster ally = null; float best = 14f;
                foreach (var m in World.Monsters)
                {
                    if (m == this || m.Dead || !m._aggro || m.Barrier > 0) continue;
                    float d = m.Pos2.DistanceTo(Pos2);
                    if (d < best) { best = d; ally = m; }
                }
                if (ally == null) { _special = 2f; return false; }
                _special = 7f;
                Model.Play("Spell_Simple_Shoot", 1.2f, 0.1f, true);
                var a = ally;
                Busy(0.5f, () =>
                {
                    if (!IsInstanceValid(a) || a.Dead) return;
                    a.AddBarrier(a.MaxLife * 0.35f, 8f);
                    Fx.Beam(Chest, a.Chest, Element.Frost, 0.2f, 0.4f);
                    Fx.Ring(a.GlobalPosition + Vector3.Up * 0.2f, a.Radius * 2.5f, new Color(0.9f, 0.95f, 1f), 0.5f);
                    Snd.At("barrier_up", a.GlobalPosition, 0.6f);
                });
                return true;
            }
        }
        return false;
    }

    private Action<float> _busyTick;
    private int _sleep;          // 0 awake, 1 dormant but drawn, 2 dormant and hidden
    private bool _buried;        // lying in the ground, waiting for its pack to wake
    private readonly AnimCheck.Probe _animProbe = new();
    private float _blocked;
    public void Busy(float time, Action end, Action<float> tick = null) { _st = Mode.Busy; _t = time; _busyEnd = end; _busyTick = tick; SetPhysicsProcess(tick != null); }

    /// <summary>Pact fiend: leap to a point on the owner's command, then run the callback.</summary>
    public void CommandLeap(Vector2 to, float time, Action onLand)
    {
        Vector2 from = Pos2; float k = 0;
        Model.Play("NinjaJump_Idle_Loop", 1f, 0.05f);
        FaceDir(to - from);
        Busy(time, () =>
        {
            Pos2 = World.Grid?.NearestWalk(to) ?? to;
            var gp = GlobalPosition; GlobalPosition = new Vector3(gp.X, 0, gp.Z);
            Model.Play("NinjaJump_Land", 1.6f, 0.05f, true);
            onLand?.Invoke();
        }, dtt => { k = Mathf.Min(1f, k + dtt / time); var q = from.Lerp(to, k); GlobalPosition = new Vector3(q.X, Mathf.Sin(k * Mathf.Pi) * 2.4f, q.Y); });
    }

    // ───────────────────────── attacks ─────────────────────────

    private void StartAttack()
    {
        _st = Mode.Windup; _contactDone = false;
        string anim = Def.AttackAnims[Rng.R.Next(Def.AttackAnims.Length)];
        float sp = AtkSpeed;
        _t = Def.Contact / Mathf.Max(0.2f, sp / Def.AnimSpeed);
        _attackPos = _target.Pos2; _attackDir = (_target.Pos2 - Pos2).Normalized();
        FaceDir(_attackDir);
        _cd = Def.Cooldown / (AtkSpeedMul * (_enrage > 0 ? 1.3f : 1f));
        if (Faction == Faction.Monster)
            switch (Def.AI)
            {
                case MonAI.Brute: _telegraph = Fx.TelegraphCone(this, Def.Range + Radius + 0.6f, 110f, _t, Palette.Danger); break;
                case MonAI.Exploder: _t = 0.95f; _telegraph = Fx.Telegraph(Pos2, 3.6f, _t, Palette.Danger); Model.Flash(new Color(0.8f, 1f, 0.3f), 1.5f); break;
            }
        Model.PlayAttack(anim, sp, _t, 0.1f);          // the swing meets the moment the blow resolves
    }

    private void CancelTelegraph() { if (_telegraph != null && IsInstanceValid(_telegraph)) _telegraph.QueueFree(); _telegraph = null; }

    private void TickWindup(float dt)
    {
        _t -= dt;
        if (_t > 0) return;
        _telegraph = null;
        _st = Mode.Recover; _t = 0.35f / AtkSpeedMul;
        if (_target == null || !IsInstanceValid(_target)) return;
        Log.Guard("monster", ResolveAttack);
    }

    private void ResolveAttack()
    {
        Vector3 hand = Model.BonePos("hand_r");
        if (Faction == Faction.Player) { MinionAttack(hand); return; }
        switch (Def.AI)
        {
            case MonAI.Ranged: case MonAI.Caster: case MonAI.Teleporter: case MonAI.Support:
            {
                Vector2 dir = (_target.Pos2 - Pos2).Normalized();
                hand.Y = Mathf.Clamp(hand.Y, 0.9f, 1.8f);
                Projectile.SpawnMonster(this, hand, dir, Def.ProjSpeed, Def.Range + 8f, 1f, Def.Element, Def.OnHit);
                Snd.At(Def.Sfx + "_attack", GlobalPosition, 0.7f);
                break;
            }
            case MonAI.Brute:
            {
                World.InCone(Faction.Monster, Pos2, _attackDir, Def.Range + Radius + 0.6f, 110f, Tmp);
                foreach (var a in Tmp.ToArray()) Combat.MonsterHit(this, a, 1f, Def.Element, Def.OnHit, true);
                Fx.Burst(World.To3(Pos2 + _attackDir * (Def.Range * 0.6f), 0.2f), Def.Element, 2.2f, 0.6f);
                Fx.Shake(0.2f, 0.2f);
                Snd.At(Def.Sfx + "_attack", GlobalPosition);
                break;
            }
            case MonAI.Exploder:
                Combat.MonsterArea(this, Pos2, 3.6f, 1f, Def.Element, St.Poison(0.5f, 4));
                Fx.Burst(Chest, Element.Poison, 3.6f, 1.2f);
                Snd.At("bloat_explode", GlobalPosition);
                NoLoot = false;
                TakeHit(new Hit { Source = this, Amount = MaxLife * 10f, NoReact = true });
                break;
            default:
            {
                float reach = Def.Range + Radius + _target.Radius + 0.5f;
                Vector2 to = _target.Pos2 - Pos2;
                if (to.Length() <= reach && to.Normalized().Dot(Forward) > 0.2f)
                {
                    Combat.MonsterHit(this, _target, 1f, Def.Element, Def.OnHit, true);
                    Fx.Hit(_target.Chest, Def.Element, false);
                    Snd.At("hit_flesh", _target.GlobalPosition, 0.6f);
                    EliteFx.OnAttackHit(this);
                }
                else Snd.At("swing_light", GlobalPosition, 0.4f);
                break;
            }
        }
    }

    private void MinionAttack(Vector3 hand)
    {
        var p = World.P;
        var opt = new HitOpt { Minion = true, MinionActor = this, Dir = (_target.Pos2 - Pos2).Normalized() };
        if (IsRanged)
        {
            hand.Y = Mathf.Clamp(hand.Y, 0.9f, 1.8f);
            var pr = Projectile.SpawnMonster(this, hand, opt.Dir, Def.ProjSpeed, 18f, 0, Def.Element, null);
            pr.MinionOwner = p; pr.MinionCoef = MinionCoef; pr.MinionSkill = MinionSkill;
            return;
        }
        float reach = Def.Range + Radius + _target.Radius + 0.6f;
        if (Cleave)
        {
            World.InCircle(Faction.Player, Pos2 + opt.Dir * (Def.Range * 0.6f), Def.Range + 0.5f, Tmp);
            foreach (var a in Tmp.ToArray()) Combat.PlayerHit(p, a, MinionSkill, MinionCoef, Def.Element, opt);
            Fx.Burst(World.To3(Pos2 + opt.Dir * (Def.Range * 0.6f), 0.2f), Def.Element, 2f, 0.4f);
        }
        else if (Pos2.DistanceTo(_target.Pos2) <= reach)
        {
            Combat.PlayerHit(p, _target, MinionSkill, MinionCoef, Def.Element, opt);
            Fx.Hit(_target.Chest, Def.Element, false);
        }
        if (Taunter && (_tauntT -= 1f) <= 0) { _tauntT = 3; _target.ApplyStatus(St.Taunt(4), this); }
    }

    // ───────────────────────── damage ─────────────────────────

    public override float TakeHit(Hit h)
    {
        if (Dead) return 0;
        if (Faction == Faction.Monster) { h.Amount *= DrMul * (Staggered ? 1.25f : 1f); }
        float d = base.TakeHit(h);
        if (Dead) return d;
        if (!_aggro && Faction == Faction.Monster) WakePack();
        if (!h.Dot)
        {
            Model.Flash(Colors.White, h.Crit ? 1f : 0.55f);
            EliteFx.OnHitTaken(this, h);
            _flinchIcd -= 0.3f;
            if (!h.NoReact && Brain == null && (_st == Mode.Chase || _st == Mode.Idle) && _flinchIcd <= 0 && h.Amount > MaxLife * 0.05f * Def.Poise && !IsElite)
            {
                _flinchIcd = 1.2f;
                Model.Play(Rng.Chance(0.5f) ? "Hit_Chest" : "Hit_Head", 1.6f, 0.05f, true);
                _st = Mode.Recover; _t = 0.22f;
                if (h.Dir != Vector2.Zero && Def.Poise < 2f) Knock = h.Dir * 3.5f;
            }
        }
        Brain?.OnDamaged();
        return d;
    }

    protected override void OnStagger(float amount)
    {
        if (!IsBoss || StaggerImmune > 0 || Staggered) return;
        Stagger = Mathf.Min(100f, Stagger + amount);
        if (Stagger >= 100f)
        {
            StaggerLeft = 5f;
            CancelTelegraph();
            Brain?.Interrupt();
            ApplyStatus(St.Vuln(5.5f), null);
            Snd.At("stagger_break", GlobalPosition);
            Fx.Burst(Chest, Element.Lightning, 3f, 1f);
            Game.I.Hud?.Toast("踉蹌！", Palette.Gold);
        }
    }

    private void Expire()
    {
        OnExpire?.Invoke(Pos2);
        NoLoot = true;
        Dead = true;
        Die(new Hit());
    }

    public override void Die(Hit h)
    {
        _st = Mode.Dead; _deadT = 0;
        CancelTelegraph();
        Untargetable = true;
        (Faction == Faction.Monster ? World.Monsters : World.Minions).Remove(this);
        Model.Pause(false);
        Model.Play(Model.Has("Death01") ? "Death01" : "Hit_Knockback", 1.5f, 0.08f, true);
        if (h.Dir != Vector2.Zero) Knock = h.Dir * (IsBoss ? 0 : 5f);
        Snd.At(IsBoss && Brain != null ? "boss_death" : Def.Sfx + "_death", GlobalPosition, 0.8f);
        if (Faction == Faction.Monster)
        {
            EliteFx.OnDeath(this);
            Log.Guard("loot", () => Loot.OnMonsterDeath(this));
            World.RaiseKilled(this, h);
        }
        Brain?.OnDeath();
    }

    private void TickDead(float dt)
    {
        _deadT += dt;
        if (Knock.LengthSquared() > 0.01f) { Move(Knock * dt); Knock = Knock.MoveToward(Vector2.Zero, 20f * dt); }
        float wait = IsBoss ? 1.6f : 0.55f;
        if (_deadT > wait && _deadT - dt <= wait)
        {
            Model.DissolveSpeed = IsBoss ? 0.45f : 1.1f;
            Model.Dissolve();
            Fx.Ash(this);
            Snd.At("ash_crumble", GlobalPosition, 0.5f);
        }
        if (Model.Dissolved || _deadT > 6f) QueueFree();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) { World.Monsters.Remove(this); World.Minions.Remove(this); }
    }

    // Busy state needs a per-frame callback (leaps); run it from _PhysicsProcess-free path
    public override void _PhysicsProcess(double delta)
    {
        if (_st == Mode.Busy && _busyTick != null)
        {
            if (Game.I.Paused) return;
            try { _busyTick((float)delta); }
            catch (Exception e) { if (_tickErrors++ < 30) Log.Error("monster", $"busy step of {Def?.Id ?? "?"} failed", e); _busyTick = null; }
        }
        else SetPhysicsProcess(false);          // only the leaps of the Busy state need this callback
    }

    // API for BossBrain
    public void BossMove(Vector2 goal, float dt, float stopAt) => MoveToward(goal, dt, stopAt);
    public void BossIdle() => Model.Play(Def.Idle, 1f, 0.25f);
    public float BossSpeedMul { set => SpeedMul = value; }
}
