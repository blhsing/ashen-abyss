using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>The twelve AI kinds of BESTIARY.md 5.1 (Charger … Stalker). Numbers in comments are the specification's.</summary>
public partial class Monster
{
    private static readonly List<Actor> AiTmp = new();
    private static readonly Color GrabColor = new(0.85f, 0.35f, 1f), PullColor = new(1f, 0.55f, 0.15f);

    // ═════════════════════════ Charger 衝撞 ═════════════════════════

    /// <summary>6-14 m with a clear straight walkable lane: a 0.8 s locked line, then a 14 m/s run that stuns itself on a wall.</summary>
    private bool TryCharge(float dist)
    {
        if (_aiCd[CdKind] > 0f || Rooted || dist < 6f || dist > 14f) return false;
        var grid = World.Grid;
        Vector2 dir = (_target.Pos2 - Pos2).Normalized();
        float len = A.ChargeLen;
        if (grid != null && !grid.LineWalk(Pos2, _target.Pos2)) return false;
        _aiCd[CdKind] = 7f;
        FaceDir(dir);
        float width = 2f * Radius + 0.8f;
        _telegraph = Fx.TelegraphLine(Pos2, dir, len, width, 0.8f, Palette.Danger, Def.Element);
        Snd.At("telegraph_warn", GlobalPosition, 0.5f);
        Snd.At(Def.Sfx + "_idle", GlobalPosition, 0.8f);
        Model.Play(Def.Idle, 1.6f, 0.1f);
        Note("Charger.windup");
        Channel(0.8f, () => ChargeRun(dir, len), null, null);
        return true;
    }

    private void ChargeRun(Vector2 dir, float len)
    {
        var hit = new HashSet<Actor>();
        Vector2 start = Pos2;
        float speed = A.ChargeSpeed, time = len / speed, went = 0f;
        bool wall = false, connected = false;
        _charging = true;
        Model.Play(Def.Run, 2.4f, 0.05f);
        Note("Charger.charge");
        Channel(time, () => ChargeEnd(wall, connected), dt =>
        {
            if (Stunned) return;
            float step = speed * dt;
            Vector2 before = Pos2;
            Move(dir * step);
            float made = Pos2.DistanceTo(before);
            went += made;
            World.InCapsule(Faction.Monster, before, Pos2, Radius + 0.3f, AiTmp);
            foreach (var a in AiTmp.ToArray())
                if (hit.Add(a)) { Strike(a, 1.5f, St.Knock(8f)); if (A.ChargeStatus.HasValue) ApplyHostile(a, A.ChargeStatus.Value, Def.Element); connected = true; Fx.Hit(a.Chest, Def.Element, true); Snd.At("punch_hit", a.GlobalPosition, 0.8f); }
            if (made < step * 0.4f && went < len - 0.5f)
            {
                wall = true;
                _t = 0f;               // stop now: the Busy timer ends this frame
            }
        }, () => { _charging = false; });
    }

    private void ChargeEnd(bool wall, bool connected)
    {
        _charging = false;
        if (wall)
        {
            // the reward for side-stepping: a stunned, vulnerable charger
            StTime[(int)StatusId.Stun] = Mathf.Max(StTime[(int)StatusId.Stun], 1.2f);
            ApplyStatus(St.Vuln(2f), null);
            Fx.Burst(Chest, Element.Physical, 1.4f, 0.8f);
            Fx.Shake(0.25f, 0.2f);
            Snd.At("hit_metal", GlobalPosition, 0.8f);
            Note("Charger.wall");
            return;
        }
        if (connected) Note("Charger.connect");
        if (connected && Def.Is(MonTrait.DeathBurst))
        {
            // the pack-mule's kegs go off when its charge connects
            Note("DeathBurst.charge");
            TakeHit(new Hit { Source = this, Amount = MaxLife * 10f, NoReact = true });
            return;
        }
        Model.Play(Def.Idle, 1f, 0.2f);
        Busy(0.6f, null);
    }

    // ═════════════════════════ Shieldbearer 持盾 ═════════════════════════

    private void BreakGuard(string why)
    {
        if (_guardBroken) return;
        _guardBroken = true; _guardBrokenUntil = Now + 2.5; _backHits = 0;
        CancelTelegraph();
        Model.Play("Hit_Knockback", 1f, 0.05f, true);
        Snd.At("barrier_break", GlobalPosition, 0.7f);
        Note("Shieldbearer.guardbreak", why);
        if (_st is Mode.Chase or Mode.Idle) { _st = Mode.Recover; _t = 0.8f; }
    }

    /// <summary>After three blocks within 4 s it answers with a 6 m shield dash (0.6 s lane).</summary>
    private bool TryShieldDash(float dist)
    {
        if (_blocks < 3 || Now > _blockWindow || _aiCd[CdKind] > 0f || dist > 7f || Rooted) return false;
        _blocks = 0; _aiCd[CdKind] = 6f;
        Vector2 dir = (_target.Pos2 - Pos2).Normalized();
        FaceDir(dir);
        _telegraph = Fx.TelegraphLine(Pos2, dir, 6f, 2f * Radius + 0.8f, 0.6f, Palette.Danger, Sigil.Blade);
        Model.Play(CharModel.HasClip("Shield_Dash") && Def.Rig == "A" ? "Shield_Dash" : "Attack2", 1f, 0.1f, true);
        Note("Shieldbearer.dash");
        Channel(0.6f, () =>
        {
            var hit = new HashSet<Actor>();
            float went = 0f;
            _charging = true;
            Channel(6f / 12f, () => { _charging = false; Busy(0.4f, null); }, dt =>
            {
                Vector2 before = Pos2;
                Move(dir * 12f * dt);
                went += Pos2.DistanceTo(before);
                World.InCapsule(Faction.Monster, before, Pos2, Radius + 0.3f, AiTmp);
                foreach (var a in AiTmp.ToArray()) if (hit.Add(a)) { Strike(a, 1f, St.Stun(0.5f)); Snd.At("shield_bash", a.GlobalPosition); }
            }, () => _charging = false);
        }, null, null);
        return true;
    }

    // ═════════════════════════ Burrower 潛地 ═════════════════════════

    private bool BurrowChase(float dt, float dist, float reach)
    {
        switch (_bur)
        {
            case 1:
            {
                _burT -= dt;
                Vector2 goal = _target.Pos2;
                if (A.StationaryEmerge)
                {
                    if (!_burSpotSet || _burSpot.DistanceTo(_target.Pos2) > 4f) { _burSpot = World.Grid?.RandomWalkNear(_target.Pos2, 0.5f, 2f) ?? _target.Pos2; _burSpotSet = true; }
                    goal = _burSpot;
                }
                if (_burT <= 0f && Pos2.DistanceTo(goal) <= 1.5f && !Rooted) { Emerge(); return true; }
                MoveToward(goal, dt, 0.3f);
                return true;
            }
            case 2: case 4: return true;
            case 3:
            {
                _burT -= dt;
                if ((_burT <= 0f || dist > 8f) && !Rooted) { Dive(); return true; }
                if (A.StationaryEmerge && dist > reach)
                {
                    FaceDir(_target.Pos2 - Pos2, 1f - Mathf.Exp(-6f * dt));
                    Model.Play(Def.Idle, 1f, 0.25f);
                    return true;
                }
                return false;            // surfaced: it fights as Melee (and its Breath special, below)
            }
        }
        return false;
    }

    private void BurrowRipple(float dt)
    {
        _stalkT -= dt;
        if (_stalkT > 0f) return;
        _stalkT = 0.3f;
        Fx.Dust(GlobalPosition);
    }

    private void Emerge()
    {
        _bur = 2;
        Vector2 at = Pos2;
        _telegraph = Fx.Telegraph(at, 2.2f, 0.7f, Palette.Danger, Def.Element);
        Snd.At("earth_cast", GlobalPosition, 0.8f);
        Note("Burrower.emerge");
        Channel(0.7f, () =>
        {
            _bur = 3; _burT = A.SurfaceTime;
            SetHidden(false);
            Model.Play(Model.Has("Spawn") ? "Spawn" : "LayToIdle", 2.2f, 0.02f, true);
            World.InCircle(Faction.Monster, at, 2.2f, AiTmp);
            foreach (var a in AiTmp.ToArray()) Strike(a, 1.3f * AiFirstMul(), A.EmergeStatus, false);
            Fx.Burst(World.To3(at, 0.2f), Element.Physical, 2.2f, 0.9f);
            Fx.Dust(World.To3(at));
            Fx.Shake(0.2f, 0.2f);
            Snd.At("earth_impact", GlobalPosition);
            if (Def.Is(MonTrait.Extinguish)) Extinguish(at, 2.2f);
            _cd = 0.5f;
        }, null, () => { _bur = 1; _burT = 1f; });
    }

    private void Dive()
    {
        _bur = 4;
        Model.Play("Hit_Knockback", 1.6f, 0.05f, true);
        Fx.Dust(GlobalPosition);
        Snd.At("earth_cast", GlobalPosition, 0.5f);
        Note("Burrower.dive");
        Channel(0.5f, () => { _bur = 1; _burT = 2.5f; _burSpotSet = false; SetHidden(true); }, null, () => { _bur = 3; _burT = 1.5f; });
    }

    // ═════════════════════════ Bomber 投彈 ═════════════════════════

    private void BomberResolve(Vector3 hand)
    {
        if (_bombMelee)
        {
            World.InCone(Faction.Monster, Pos2, _attackDir, Def.Range * 0.3f + Radius + 1.6f, 110f, AiTmp);
            foreach (var a in AiTmp.ToArray()) Strike(a, AiFirstMul());
            Fx.Burst(World.To3(Pos2 + _attackDir * 1.6f, 0.2f), Def.Element, 2f, 0.6f);
            Snd.At("swing_heavy", GlobalPosition);
            Note("Bomber.melee");
            return;
        }
        Vector2 land = _target.Pos2;
        hand.Y = Mathf.Clamp(hand.Y, 0.9f, 2.6f);
        MonsterShot.Lob(this, hand, land, 0.9f, A.BombRadius);
        Snd.At(Def.Sfx + "_attack", GlobalPosition, 0.7f);
        Note("Bomber.throw");
    }

    /// <summary>The lob lands: area 1.0x + OnHit, then a ground zone (three per bomber at most).</summary>
    internal void BombLanded(Vector2 at)
    {
        World.InCircle(Faction.Monster, at, A.BombRadius, AiTmp);
        foreach (var a in AiTmp.ToArray()) Strike(a, AiFirstMul(), A.BombDirect, false);
        Fx.Burst(World.To3(at, 0.3f), Def.Element, A.BombRadius, 1f);
        Snd.At(Def.Element == Element.Fire ? "fire_impact" : Def.Element == Element.Poison ? "poison_splash" : "earth_impact", World.To3(at), 0.9f);
        if (A.BombZone != null) AddZone(at, A.BombZone, 3);
        Note("Bomber.land");
    }

    // ═════════════════════════ Healer 療癒 ═════════════════════════

    private static bool IsConstruct(MonsterDef d) => d.Family is "engine" or "effigy" or "bell" || d.Id is "brass_sentinel" || (d.RoleText ?? "").Contains("construct");

    private bool TryHeal()
    {
        if (_aiCd[CdKind] > 0f) return false;
        Monster best = null; float bestPct = 0.7f;
        foreach (var m in World.Monsters)
        {
            if (m == this || m.Dead || !m._aggro || m.IsBoss || m.AiHidden || m.MaxLife <= 0f) continue;
            if (A.HealFilter == "construct" ? !IsConstruct(m.Def) : A.HealFilter != null && m.Def.Family != A.HealFilter) continue;
            float pct = m.Life / m.MaxLife;
            if (pct >= bestPct || m.Pos2.DistanceSquaredTo(Pos2) > 144f) continue;
            if (World.Grid != null && !World.Grid.LineShot(Pos2, m.Pos2)) continue;
            bestPct = pct; best = m;
        }
        if (best == null) { _aiCd[CdKind] = 1f; return false; }
        _aiCd[CdKind] = 8f;
        var ally = best;
        float rate = A.HealPct * (ally.IsElite ? 0.5f : 1f);
        Model.Play(A.CastAnim, 0.6f, 0.15f);
        FaceDir(ally.Pos2 - Pos2);
        var ring = MonsterFx.GroundRing(ally, ally.Radius * 2.2f + 0.4f, new Color(0.5f, 1f, 0.55f, 0.35f));
        float beamT = 0f, healed = 0f;
        Note("Healer.channel", ally.Def.Id);
        void Cleanup() { if (IsInstanceValid(ring)) ring.QueueFree(); }
        Channel(3f, () => { Cleanup(); Note("Healer.done", $"{healed:0} life"); }, dt =>
        {
            if (!IsInstanceValid(ally) || ally.Dead || (World.Grid != null && !World.Grid.LineShot(Pos2, ally.Pos2))) { CancelChannel("lost ally"); return; }
            float h = ally.MaxLife * rate * dt;
            ally.Heal(h); healed += h;
            beamT -= dt;
            if (beamT <= 0f) { beamT = 0.25f; Fx.Beam(Chest, ally.Chest, Element.Poison, 0.12f, 0.3f); }
        }, Cleanup);
        return true;
    }

    // ═════════════════════════ Reviver 喚起 ═════════════════════════

    private sealed class Remains
    {
        public MonsterDef Def; public Vector2 Pos; public int Level; public double Until; public bool Used; public Node3D Prop; public Node Root; public int Pack;
    }
    private static readonly List<Remains> Graves = new();

    /// <summary>A normal monster of a family some reviver of this level raises leaves a bone heap for 12 s.</summary>
    private void RecordRemains()
    {
        if (Tier != MonTier.Normal || NoRevive || Faction != Faction.Monster || Def.Role == "prop" || Def.Family is "boss" or "minion" or "prop") return;
        bool wanted = false;
        foreach (var m in World.Monsters)
            if (m.Def.AI == MonAI.Reviver && !m.Dead && (m.A.ReviveFamilies == null ? m.Def.Family == Def.Family : Array.IndexOf(m.A.ReviveFamilies, Def.Family) >= 0)) { wanted = true; break; }
        if (!wanted) return;
        PruneGraves();
        var r = new Remains { Def = Def, Pos = Pos2, Level = Level, Until = Now + 13.5, Root = World.Root, Pack = Pack };
        r.Prop = MonsterFx.BoneHeap(Pos2, Def.Element);
        Graves.Add(r);
    }

    private static void PruneGraves()
    {
        double now = Now;
        for (int i = Graves.Count - 1; i >= 0; i--)
        {
            var g = Graves[i];
            if (g.Used || g.Until < now || g.Root != World.Root)
            {
                if (g.Prop != null && GodotObject.IsInstanceValid(g.Prop)) g.Prop.QueueFree();
                Graves.RemoveAt(i);
            }
        }
    }

    private bool TryRevive()
    {
        if (_aiCd[CdKind] > 0f) return false;
        PruneGraves();
        Remains best = null; float bd = 14f * 14f;
        foreach (var g in Graves)
        {
            if (g.Used || (A.ReviveFamilies == null ? g.Def.Family != Def.Family : Array.IndexOf(A.ReviveFamilies, g.Def.Family) < 0)) continue;
            float d = g.Pos.DistanceSquaredTo(Pos2);
            if (d < bd) { bd = d; best = g; }
        }
        if (best == null) { _aiCd[CdKind] = 1f; return false; }
        _aiCd[CdKind] = 9f;
        var grave = best;
        grave.Used = true;
        FaceDir(grave.Pos - Pos2);
        Model.Play(A.CastAnim, 0.8f, 0.15f);
        var tele = Fx.Telegraph(grave.Pos, 1.3f, 1.5f, new Color(0.5f, 1f, 0.6f), true, Sigil.Shadow);
        float beamT = 0f;
        Note("Reviver.cast", grave.Def.Id);
        Channel(1.5f, () =>
        {
            if (grave.Prop != null && IsInstanceValid(grave.Prop)) grave.Prop.QueueFree();
            var m = Monster.Spawn(grave.Def, grave.Level, MonTier.Normal, grave.Pos, Faction.Monster);
            m.Life = m.MaxLife * 0.6f; m.NoLoot = true; m.XpValue *= 0.25f; m.NoRevive = true; m.Pack = Pack; m._aggro = true;
            if (m.Model.Has("LayToIdle")) { m.Model.Play("LayToIdle", 1.6f, 0f, true); m.Busy(m.Model.Len("LayToIdle") / 1.6f, null); }
            Fx.Burst(World.To3(grave.Pos, 0.3f), Element.Poison, 1.4f, 0.7f);
            Snd.At("summon_raise", World.To3(grave.Pos), 0.9f);
            Note("Reviver.raised", grave.Def.Id);
        }, dt =>
        {
            beamT -= dt;
            if (beamT <= 0f) { beamT = 0.25f; Fx.Beam(Chest, World.To3(grave.Pos, 0.4f), Element.Poison, 0.12f, 0.3f); }
        }, () => { grave.Used = false; if (IsInstanceValid(tele)) tele.QueueFree(); });
        return true;
    }

    // ═════════════════════════ Spawner 孕母 ═════════════════════════

    private bool TrySpawnYoung()
    {
        if (_aiCd[CdKind] > 0f) return false;
        _young.RemoveAll(m => m == null || !IsInstanceValid(m) || m.Dead);
        if (_young.Count >= A.SpawnCap) { _aiCd[CdKind] = 1.5f; return false; }
        _aiCd[CdKind] = A.SpawnEvery;
        var def = LocalDef(A.EggSac ? "shroud_eggsac" : A.SpawnDef);
        if (def == null) return false;
        int n = A.EggSac ? 1 : Math.Min(A.SpawnCount, A.SpawnCap - _young.Count);
        var spots = new List<Vector2>();
        for (int i = 0; i < n; i++)
        {
            Vector2 at = World.Grid?.RandomWalkNear(Pos2, 1.2f, 2.5f) ?? Pos2;
            spots.Add(at);
            Fx.Telegraph(at, 0.7f, 0.8f, new Color(0.8f, 0.8f, 0.6f), true, Sigil.Arcane);
        }
        Model.Play(Model.Has("Attack2") ? "Attack2" : A.CastAnim, 1f, 0.1f, true);
        Note(A.EggSac ? "Spawner.egg" : "Spawner.spawn", def.Id);
        Channel(0.8f, () =>
        {
            foreach (var at in spots)
            {
                var m = Summon(def, at, 0.2f);
                if (m == null) continue;
                if (A.EggSac)
                {
                    m.MaxLife = m.Life = MaxLife * 0.2f;
                    m._owner = this;
                }
                _young.Add(m);
                Fx.Dust(World.To3(at));
            }
        }, null, null);
        return true;
    }

    private void ReleaseYoung()
    {
        var def = LocalDef(A.SpawnDef);
        if (def == null || A.SpawnOnDeath <= 0) return;
        for (int i = 0; i < A.SpawnOnDeath; i++) Summon(def, World.Grid?.RandomWalkNear(Pos2, 0.6f, 2f) ?? Pos2, 0.2f);
        Fx.Burst(Chest, Element.Poison, 2f, 0.8f);
        Note("Spawner.release", $"{A.SpawnOnDeath}×{def.Id}");
    }

    private void EggSacTick(float dt)
    {
        _burT -= dt;
        if (_burT > 0f || Dead) return;
        var def = LocalDef("shroud_ling");
        var mother = _owner;
        for (int i = 0; i < 3; i++)
        {
            var m = Summon(def, World.Grid?.RandomWalkNear(Pos2, 0.4f, 1.4f) ?? Pos2, 0.2f);
            if (m != null && mother != null && IsInstanceValid(mother)) { mother._young.Add(m); m.Pack = mother.Pack; }
        }
        Fx.Burst(Chest, Element.Poison, 1.4f, 0.8f);
        Snd.At("poison_splash", GlobalPosition, 0.8f);
        Note("Spawner.hatch");
        NoLoot = true; XpValue = 0f;
        TakeHit(new Hit { Source = this, Amount = MaxLife * 10f, NoReact = true });
    }

    // ═════════════════════════ Caller 喚援 ═════════════════════════

    private bool TryCall()
    {
        if (_aiCd[CdKind] > 0f) return false;
        _called.RemoveAll(m => m == null || !IsInstanceValid(m) || m.Dead);
        bool escorts = Def.Is(MonTrait.Escort) && _called.Count > 0;
        if (!escorts && _called.Count >= A.CallCap) { _aiCd[CdKind] = 2f; return false; }
        _aiCd[CdKind] = 12f;
        var def = LocalDef(A.CallDef);
        if (def == null) return false;
        Model.Play(A.CastAnim, 0.9f, 0.1f, true);
        var ring = MonsterFx.GroundRing(this, 1.6f, new Color(1f, 0.45f, 0.2f, 0.35f));
        Snd.At(Def.Family == "reaver" ? "war_cry" : Def.Family == "fiend" ? "fiend_summon" : "beast_roar", GlobalPosition, 0.9f);
        Note("Caller.cast", escorts ? "escorts enraged" : def.Id);
        void Cleanup() { if (IsInstanceValid(ring)) ring.QueueFree(); }
        Channel(1.2f, () =>
        {
            Cleanup();
            if (A.CallWail.HasValue)
            {
                World.InCircle(Faction.Monster, Pos2, A.CallWailRadius, AiTmp);
                foreach (var a in AiTmp.ToArray()) ApplyHostile(a, A.CallWail.Value, Def.Element);
                Fx.Ring(GlobalPosition + Vector3.Up * 0.2f, A.CallWailRadius, new Color(0.7f, 0.75f, 0.9f), 0.5f, Sigil.Arcane);
            }
            if (escorts) { foreach (var m in _called) m.Enrage(8f); Note("Escort.enrage"); return; }
            int n = Math.Min(A.CallCount, A.CallCap - _called.Count);
            var hero = World.P;
            var self = this;
            AshenAbyss.Level.Current?.After(1f, () =>
            {
                if (!IsInstanceValid(self) || self.Dead || hero == null) return;
                for (int i = 0; i < n; i++)
                {
                    Vector2 at;
                    if (def.RiseFromGround) at = World.Grid?.RandomWalkNear(hero.Pos2, 4f, 8f) ?? hero.Pos2;
                    else at = World.Grid?.RandomWalkNear(hero.Pos2, 16f, 22f) ?? self.Pos2;       // they run in from off-screen
                    var m = self.Summon(def, at, 0.25f);
                    if (m == null) continue;
                    self._called.Add(m);
                    if (def.RiseFromGround) Fx.Dust(World.To3(at));
                }
                self.Note("Caller.arrive", $"{n}×{def.Id}");
            });
        }, null, Cleanup);
        return true;
    }

    /// <summary>Populate hands a caller its escorts (the ash-handler's hounds); calling replaces them only when all are dead.</summary>
    public void AdoptEscorts(IEnumerable<Monster> escorts) { int n = 0; foreach (var m in escorts) if (m != null) { _called.Add(m); m._owner = this; n++; } if (n > 0) Note("Escort.adopt", $"{n} escorts"); }

    // ═════════════════════════ Swarm 蟲群 ═════════════════════════

    private Actor SwarmTarget()
    {
        if (A.Attract != "fire") return _target;
        // flame-moths prefer what burns
        Actor best = null; float bd = 14f * 14f;
        var p = World.P;
        if (p != null && !p.Dead && p.Has(StatusId.Burn)) { best = p; bd = p.Pos2.DistanceSquaredTo(Pos2); }
        foreach (var m in World.Minions) { if (m.Dead || !m.Has(StatusId.Burn)) continue; float d = m.Pos2.DistanceSquaredTo(Pos2); if (d < bd) { bd = d; best = m; } }
        return best ?? _target;
    }

    private bool SwarmChase(float dt)
    {
        var t = SwarmTarget();
        if (t != _target && t != null) { _target = t; Note("Swarm.attract"); }
        float d = Pos2.DistanceTo(_target.Pos2);
        if (d > 0.4f) MoveToward(_target.Pos2, dt, 0.2f);
        else Model.Play(Def.Idle, 1f, 0.3f);
        return true;
    }

    private void SwarmTick(float dt, double now)
    {
        float frac = MaxLife > 0f ? Mathf.Clamp(Life / MaxLife, 0f, 1f) : 1f;
        float r = Mathf.Lerp(A.SwarmRadius * 0.5f, A.SwarmRadius, frac);
        Radius = r;
        if (Model != null)
        {
            if (Model.IsPrimitive) Model.RotateY(dt * 2.4f);
            Model.Scale = Vector3.One * Def.Scale * (0.55f + 0.45f * frac);
        }
        if (!_aggro || Stunned || now < _swarmNext) return;
        _swarmNext = now + 0.5;
        World.InCircle(Faction.Monster, Pos2, r, AiTmp);
        if (AiTmp.Count > 0) Note("Swarm.bite");
        foreach (var a in AiTmp.ToArray()) Strike(a, 0.25f, null, false);
    }
    private double _swarmNext;

    // ═════════════════════════ Turret 砲座 ═════════════════════════

    private bool TurretChase(float dt, float dist, bool sight)
    {
        if (_target == null) return true;
        Vector2 to = _target.Pos2 - Pos2;
        // it turns at TurnRate (60°/s): a hero who circles it fast stays out of its aim
        float delta = Mathf.Wrap(Mathf.Atan2(to.X, to.Y) - Facing, -Mathf.Pi, Mathf.Pi);
        float max = Mathf.DegToRad(A.TurnRate) * dt;
        Facing += Mathf.Clamp(delta, -max, max);
        Rotation = new Vector3(0, Facing, 0);
        bool aligned = Mathf.Abs(delta) < Mathf.DegToRad(12f);
        if (dist < 4f && _aiCd[CdKind2] <= 0f && sight) { TurretClose(); return true; }
        if (dist <= Def.Range && sight && _cd <= 0f && !Has(StatusId.Fear) && aligned) { StartAttack(); return true; }
        Model.Play(Def.Idle, 1f, 0.3f);
        return true;
    }

    private void TurretFire()
    {
        Vector2 from = Pos2, dir = _attackDir;
        Vector2 end = from + dir * Def.Range;
        if (World.Grid != null) end = World.Grid.ClampLine(from, end, true);
        World.InCapsule(Faction.Monster, from, end, 0.8f, AiTmp);
        foreach (var a in AiTmp.ToArray()) Strike(a, AiFirstMul(), null, false);
        Fx.Beam(World.To3(from, 1.4f), World.To3(end, 1.1f), Def.Element == Element.Physical ? Element.Fire : Def.Element, 0.35f, 0.3f);
        Snd.At(Def.Element == Element.Poison ? "poison_cast" : "fire_cast", GlobalPosition, 0.8f);
        _shutterOpenUntil = Now + 1.5;
        Note("Turret.fire");
    }

    /// <summary>Attack2 when the hero is within 4 m: flash (stun cone), spore cloud or ember fan; 0.8 s telegraph, 8 s cooldown.</summary>
    private void TurretClose()
    {
        _aiCd[CdKind2] = 8f;
        _shutterOpenUntil = Now + 0.8 + 1.5;
        string kind = A.Attack2;
        Node3D tele = kind == "cloud" ? Fx.Telegraph(Pos2, 3f, 0.8f, Palette.Danger, Def.Element) : Fx.TelegraphCone(this, 5f, 90f, 0.8f, Palette.Danger, Def.Element);
        _telegraph = tele;
        Model.Play(Model.Has("Attack2") ? "Attack2" : A.CastAnim, 1f, 0.1f, true);
        Note("Turret.close", kind);
        Channel(0.8f, () =>
        {
            switch (kind)
            {
                case "cloud":
                    AddZone(Pos2, new MonZone { Radius = 3f, Dur = 4f, El = Element.Poison, Status = St.Poison(0.3f, 3f), Mul = 0.12f }, 3);
                    Fx.Burst(Chest, Element.Poison, 3f, 1f);
                    break;
                case "flash":
                    World.InCone(Faction.Monster, Pos2, Forward, 5f, 90f, AiTmp);
                    foreach (var a in AiTmp.ToArray()) Strike(a, 0.6f, St.Stun(1f), false);
                    Fx.LightFlash(Chest, new Color(1f, 0.9f, 0.6f), 6f, 8f, 0.25f);
                    break;
                default:    // ember fan
                    for (int i = -2; i <= 2; i++)
                        Projectile.SpawnMonster(this, Chest, Forward.Rotated(Mathf.DegToRad(i * 14f)), 15f, 9f, 0.5f, Element.Fire, A.LitBurn ?? Def.OnHit);
                    break;
            }
        }, null, null);
    }

    // ═════════════════════════ Flyer 飛行 ═════════════════════════

    private float _orbitSign = 1f;

    private bool FlyerChase(float dt, float dist, bool sight)
    {
        if (_aiCd[CdKind] <= 0f && dist >= 5f && dist <= 12f && sight) { Swoop(); return true; }
        Vector2 from = Pos2 - _target.Pos2;
        if (from.LengthSquared() < 0.01f) from = Vector2.Right;
        if (Rng.Chance(dt * 0.15f)) _orbitSign = -_orbitSign;
        Vector2 goal = _target.Pos2 + from.Normalized().Rotated(0.7f * _orbitSign) * A.OrbitDist;
        if (World.Grid != null && !World.Grid.WalkAt(goal)) { _orbitSign = -_orbitSign; goal = _target.Pos2 + from.Normalized().Rotated(0.7f * _orbitSign) * A.OrbitDist; }
        MoveToward(goal, dt, 0.4f);
        return true;
    }

    private void Swoop()
    {
        _aiCd[CdKind] = 4.5f;
        Vector2 dir = (_target.Pos2 - Pos2).Normalized();
        float len = Mathf.Clamp(Pos2.DistanceTo(_target.Pos2) + 3f, 6f, 15f);
        FaceDir(dir);
        _telegraph = Fx.TelegraphLine(Pos2, dir, len, 1.6f, 0.6f, Palette.Danger, Def.Element);
        Model.Play(Def.Idle, 1.6f, 0.1f);
        Snd.At(Def.Sfx + "_idle", GlobalPosition, 0.7f);
        Note("Flyer.windup");
        Channel(0.6f, () =>
        {
            var hit = new HashSet<Actor>();
            Vector2 start = Pos2;
            _charging = true;
            Model.Play(Model.Has("Attack1") ? "Attack1" : Def.AttackAnims[0], 1.4f, 0.05f, true);
            Note("Flyer.swoop");
            Channel(len / 16f, () =>
            {
                _charging = false;
                if (Def.Is(MonTrait.Trail) && A.Trail != null)
                    for (int i = 1; i <= 3; i++) AiTrailAt(start.Lerp(Pos2, i / 4f));
                if (Def.Is(MonTrait.Extinguish)) Extinguish(start.Lerp(Pos2, 0.5f), start.DistanceTo(Pos2) * 0.5f + 1f);
                Model.Play(Def.Idle, 1f, 0.2f);
                Busy(0.5f, null);            // hangs low for half a second: the moment to hit it
            }, dt =>
            {
                Vector2 before = Pos2;
                Move(dir * 16f * dt);
                World.InCapsule(Faction.Monster, before, Pos2, Radius + 0.5f, AiTmp);
                foreach (var a in AiTmp.ToArray()) if (hit.Add(a)) { Strike(a, AiFirstMul(), null, true); Fx.Hit(a.Chest, Def.Element, false); }
            }, () => _charging = false);
        }, null, null);
    }

    // ═════════════════════════ Stalker 潛獵 ═════════════════════════

    private double _pounceAt;

    private void SetStealth(bool on)
    {
        _stealthOn = on;
        Stealthed = on;
        if (on) Note("Stalker.stealth");
    }

    private void Reveal(float seconds)
    {
        _revealUntil = Math.Max(_revealUntil, Now + seconds);
        if (Stealthed) { Stealthed = false; Note("Stalker.revealed"); }
    }

    private void StalkerVisibility(float dt, double now)
    {
        bool hide = _stealthOn && now >= _revealUntil && _st is not (Mode.Windup or Mode.Busy);
        Stealthed = hide;
        float want = hide ? 0.12f : 1f;
        float v = Mathf.MoveToward(Model.Visibility, want, dt * 3f);
        if (hide) v = Mathf.Max(0.12f, v + Mathf.Sin((float)now * 9f) * 0.015f);      // a faint shimmer
        Model.SetVisibility(v);
        // re-stealth 8 s after a pounce, once it has not been hit for 2 s
        if (!_stealthOn && now > _pounceAt + 8.0 && now > _lastHit + 2.0) SetStealth(true);
    }

    private bool StalkerChase(float dt, float dist)
    {
        if (!_stealthOn || !Stealthed) return false;           // visible: fights as Melee
        var p = _target;
        _stalkT += dt;
        Vector2 heroFwd = p.Forward;
        Vector2 fromHero = (Pos2 - p.Pos2);
        bool outsideFront = fromHero.LengthSquared() > 0.01f && fromHero.Normalized().Dot(heroFwd) < Mathf.Cos(Mathf.DegToRad(60f));
        if (dist >= 5f && dist <= 10f && (outsideFront || _stalkT > 4f) && (World.Grid == null || World.Grid.LineShot(Pos2, p.Pos2)) && !Rooted) { Pounce(); return true; }
        Vector2 behind = p.Pos2 - heroFwd * A.StalkDist;
        if (World.Grid != null && !World.Grid.WalkAt(behind)) behind = p.Pos2 + (fromHero.LengthSquared() > 0.01f ? fromHero.Normalized() : Vector2.Right) * A.StalkDist;
        MoveToward(behind, dt, 0.6f);
        return true;
    }

    private void Pounce()
    {
        _stalkT = 0f; _pounceAt = Now;
        SetStealth(false); Stealthed = false;
        Vector2 land = _target.Pos2;
        Note("Stalker.pounce");
        Leap(land, 0.6f, 2.2f, 1.5f, A.PounceStatus, "Stalker.land");
    }

    /// <summary>A leap onto a point (stalkers' pounce, the ceiling drop of a dormant flyer): telegraph, flight, area landing.</summary>
    private void Leap(Vector2 land, float time, float radius, float mul, StatusApply? status, string what)
    {
        _telegraph = Fx.Telegraph(land, radius, time, Palette.Danger, Def.Element);
        FaceDir(land - Pos2);
        bool rig = Def.Rig == "A" || !Model.OwnAnimations;
        Model.Play(rig && Model.Has("NinjaJump_Start") ? "NinjaJump_Start" : "Attack2", 1.5f, 0.1f, true);
        Vector2 from = Pos2;
        float k = 0f;
        bool air = false;
        Channel(time, () =>
        {
            Pos2 = World.Grid?.NearestWalk(land) ?? land;
            var gp = GlobalPosition; GlobalPosition = new Vector3(gp.X, 0, gp.Z);
            if (rig && Model.Has("NinjaJump_Land")) Model.Play("NinjaJump_Land", 1.6f, 0.05f, true);
            World.InCircle(Faction.Monster, land, radius, AiTmp);
            foreach (var a in AiTmp.ToArray()) Strike(a, mul * AiFirstMul(), status, false);
            Fx.Burst(World.To3(land, 0.2f), Def.Element, radius, 0.7f);
            Snd.At("leap_land", GlobalPosition, 0.8f);
            Note(what);
            _cd = 0.8f;
        }, dt =>
        {
            k += dt / time;
            if (rig && k > 0.8f && !air && Model.Has("NinjaJump_Idle_Loop")) { air = true; Model.Play("NinjaJump_Idle_Loop", 1f, 0.08f); }
            if (k > 0.3f) { float f = Mathf.Clamp((k - 0.3f) / 0.7f, 0, 1); var q = from.Lerp(land, f); GlobalPosition = new Vector3(q.X, Mathf.Sin(f * Mathf.Pi) * 2f, q.Y); }
        }, () => { var gp = GlobalPosition; GlobalPosition = new Vector3(gp.X, 0, gp.Z); });
        _busyCc = false;                 // a leap in flight lands even if it is stunned on the way
    }
}
