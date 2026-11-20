using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// The bestiary's behaviours (BESTIARY.md section 5) on top of Monster's state machine: the twelve new AI kinds
/// (MonsterAiKinds.cs) and the 23 traits (MonsterAiTraits.cs). This file holds the shared state, the hooks Monster.cs
/// calls (setup, the per-frame tick, idle, chase, attack start / resolve, damage in / out, death, statuses) and the
/// helpers every behaviour uses: <see cref="Strike"/> (a hit with the type's statuses), channels that crowd control
/// breaks, cooldowns, and the counters the AI test suite reads (<see cref="AiSeen"/>).
/// Monsters of the built-in kinds without traits skip all of it (one bool test per frame).
/// </summary>
public partial class Monster
{
    // ───────────────────────── state ─────────────────────────

    private bool _ai;                     // this definition has a new kind, a trait or a helper role: the hooks below do something
    private MonAiData A => Def.Ai;
    /// <summary>Model not drawn: burrowed, buried, roosting on the ceiling.</summary>
    public bool AiHidden { get; private set; }
    /// <summary>Stealthed stalker: auto-targeting and minions ignore it (area damage still lands).</summary>
    public bool Stealthed { get; private set; }
    private Action _busyCancel;           // the channel in progress (heal, revive, call, breath, charge …) and what undoes it
    private bool _busyCc;                 // that channel ends on stun / freeze / fear
    private readonly float[] _aiCd = new float[8];
    private const int CdKind = 0, CdHook = 1, CdGrab = 2, CdBreath = 3, CdPulse = 4, CdAbsorb = 5, CdEmpower = 6, CdKind2 = 7;
    private double _tick05, _tick10;      // next half-second / one-second housekeeping (game time)
    private float _firstMul = 1f;         // the waking strike of a dormant ambusher hits harder
    private StatusApply? _firstStatus;
    // allies' buffs (set by auras and masks of other monsters; read through timestamps so allies need no tick)
    private double _auraHasteUntil, _auraWindUntil, _maskUntil;
    private bool _maskRaised;
    private Node3D _maskFx;
    // phases of the kinds and traits
    private bool _phase, _swarm, _turret, _flyer, _charging, _dormant, _shellUp, _lit, _pile, _reformed, _guardBroken;
    private int _bur;                     // burrower: 0 not one, 1 under, 2 emerging, 3 surfaced, 4 diving
    private float _burT;
    private Vector2 _burSpot; private bool _burSpotSet;
    private double _guardBrokenUntil, _revealUntil, _lastHit, _litTick;
    private int _blocks, _backHits; private double _blockWindow;
    private float _stalkT;
    private bool _stealthOn;
    private float _grabTaken;
    private Player _grabbed;
    private float _shellMax;
    private readonly List<GroundZone> _zones = new();
    private readonly List<(GroundZone z, bool inside, int entries)> _watch = new();
    private readonly List<Monster> _young = new(), _called = new();
    private Node3D _auraRing, _litFx, _dormantMark;
    private Monster _owner;               // the mother, caller or splitter this one came from
    public bool NoRevive, NoReform;
    private double _keepUntil;            // a dead monster's node stays (invisible) while its zones and bursts still need it
    private double _absorbWant;

    public bool AiNoSeparation => _phase || _swarm || _flyer || _charging || _bur == 1 || AiHidden;
    private bool AiNoFlinch => _shellUp || (_guardUp && !_guardBroken) || Now < _maskUntil || _swarm || _turret || _dormant || (_bur != 0 && _bur != 3) || _pile || _grabbed != null;
    private bool AiUnstoppable => _pile || (_dormant && AiHidden);
    private float AiBaseSpeed => _bur == 1 ? A.BurrowSpeed : Def.Speed;
    private float AiSpeedMul => (_lit ? 1.4f : 1f) * (_shellUp ? 0.8f : 1f) * (Now < _auraHasteUntil ? 1.25f : 1f);
    private float AiAtkMul => (_lit ? 1.2f : 1f) * (Now < _auraHasteUntil ? 1.25f : 1f) * (Now < _auraWindUntil ? 1.2f : 1f);
    private bool AiKeepsBody => Now < _keepUntil;
    private bool _guardUp => Def.AI == MonAI.Shieldbearer && _st is Mode.Chase or Mode.Idle && !_guardBroken;
    private float ExplodeRadius => Def.Id == "husk_bloated" || Def.BaseId == "husk_bloated" ? 3.6f : Def.Is(MonTrait.Trail) ? 3.2f : 3.6f;
    private static double Now => Game.I?.Time ?? 0;

    // ───────────────────────── counters for the AI suite and the logs ─────────────────────────

    /// <summary>How often each behaviour event happened since the game started ("Charger.charge", "Hook.pull" …).</summary>
    public static readonly Dictionary<string, int> AiSeen = new();
    private static readonly Dictionary<string, int> AiLogged = new();

    /// <summary>Counts a behaviour event; the first three of each event and type are logged as "[ai] …".</summary>
    private void Note(string what, string detail = null)
    {
        AiSeen.TryGetValue(what, out int n); AiSeen[what] = n + 1;
        string key = what + "|" + Def.Id;
        AiLogged.TryGetValue(key, out int l);
        if (l >= 3) return;
        AiLogged[key] = l + 1;
        Log.Info("ai", $"{what} {Def.Id}{(detail != null ? " " + detail : "")} at ({Pos2.X:0.0},{Pos2.Y:0.0}) life {Life:0}/{MaxLife:0}");
    }

    // ───────────────────────── setup ─────────────────────────

    // ── the faint glow some creature files ask for (rigs_bf_meta.json "light"), at most a few at once ──
    private OmniLight3D _glowLight;
    private static readonly List<OmniLight3D> GlowLights = new();
    private const int MaxGlowLights = 8;

    private void GlowSetup()
    {
        if (Model == null || Faction != Faction.Monster) return;
        var li = CharModel.CreatureLight(Model.FileId);
        if (li == null) return;
        GlowLights.RemoveAll(l => l == null || !IsInstanceValid(l) || l.IsQueuedForDeletion());
        if (GlowLights.Count >= MaxGlowLights) return;
        var (c, energy, range, height) = li.Value;
        float s = Model.Scale.X;
        // a child of the model: it hides with it (asleep far away, burrowed, roosting) and its height scales with it
        _glowLight = new OmniLight3D { LightColor = c, LightEnergy = energy * 0.8f, OmniRange = range * s, OmniAttenuation = 1.6f, ShadowEnabled = false, LightSpecular = 0.1f, Position = new Vector3(0, height, 0), Name = "glow" };
        Model.AddChild(_glowLight);
        GlowLights.Add(_glowLight);
    }

    private void GlowOff() { if (_glowLight != null && IsInstanceValid(_glowLight)) { GlowLights.Remove(_glowLight); _glowLight.QueueFree(); } _glowLight = null; }

    private void AiSetup()
    {
        Log.Guard("monster-ai", GlowSetup);
        var d = Def;
        _ai = d.Traits != MonTrait.None || d.AI > MonAI.Boss || d.Id == "shroud_eggsac";
        if (!_ai || Faction != Faction.Monster) { _ai = _ai && Faction == Faction.Monster; return; }
        for (int i = 0; i < _aiCd.Length; i++) _aiCd[i] = Rng.Range(1.5f, 4f);
        _aiCd[CdKind] = Rng.Range(2f, 5f);           // a charger's first use 2-5 s after aggro (and the others' first special)
        _aiCd[CdHook] = Rng.Range(0.4f, 1.6f);       // a hook is the gap-closer: thrown soon, while the hero is still out of reach
        _phase = d.Is(MonTrait.Phasing);
        switch (d.AI)
        {
            case MonAI.Swarm:
                _swarm = true;
                Radius = A.SwarmRadius;
                break;
            case MonAI.Turret:
                _turret = true;
                break;
            case MonAI.Flyer:
                _flyer = true;
                break;
            case MonAI.Burrower:
                _bur = 1; _burT = 0.5f;
                SetHidden(true);
                break;
            case MonAI.Stalker:
                SetStealth(true);
                break;
        }
        if (d.Is(MonTrait.Shell) && Tier != MonTier.Minion)
        {
            _shellMax = MaxLife * A.ShellMul;
            AddBarrier(_shellMax, 1e7f);
            _shellUp = true;
            Model.SetRim(new Color(0.55f, 0.8f, 1f) * 0.5f);
        }
        if (d.Is(MonTrait.Aura)) _auraRing = MonsterFx.GroundRing(this, A.AuraRadius, A.AuraEffect is "chill" or "weaken" ? new Color(1f, 0.3f, 0.15f, 0.22f) : new Color(1f, 0.8f, 0.4f, 0.18f));
        if (d.Is(MonTrait.Dormant)) DormantSetup();
        if (d.Id == "shroud_eggsac") { _burT = 4f; _aggro = true; }
    }

    // ───────────────────────── per frame ─────────────────────────

    private void AiTickAlways(float dt)
    {
        if (!_ai) return;
        double now = Now;
        if (_aggro) for (int i = 0; i < _aiCd.Length; i++) _aiCd[i] -= dt;
        if (Has(StatusId.Fear) && _busyCancel != null && _busyCc) CancelChannel("fear");
        if (Def.Id == "shroud_eggsac") { EggSacTick(dt); return; }
        if (_swarm) SwarmTick(dt, now);
        if (_bur == 1 && _aggro) BurrowRipple(dt);
        if (Def.AI == MonAI.Stalker) StalkerVisibility(dt, now);
        if (_glowLight != null) _glowLight.Visible = !Stealthed && !AiHidden && !_dormant;
        if (Def.AI == MonAI.Shieldbearer && _guardBroken && now >= _guardBrokenUntil) { _guardBroken = false; _backHits = 0; }
        if (_grabbed != null) GrabHold(dt);
        if (now >= _tick05)
        {
            _tick05 = now + 0.5;
            if (Def.Is(MonTrait.Aura) && _aggro && !Stunned) AuraPulse(now);
            if (Def.Is(MonTrait.Flammable)) FlammableTick(now);
            if (_zones.Count > 0 || _watch.Count > 0) ZonesTick();
            if (_dormant && AiHidden) DormantHint();
        }
    }

    private void AiPostTick(float dt, float facing0)
    {
        if (!_ai || _st is Mode.Busy or Mode.Dead) return;
        float rate = Def.AI == MonAI.Turret ? A.TurnRate : Def.AI == MonAI.Shieldbearer ? 180f : 0f;
        if (rate <= 0f) return;
        float max = Mathf.DegToRad(rate) * dt;
        float delta = Mathf.Wrap(Facing - facing0, -Mathf.Pi, Mathf.Pi);
        if (Mathf.Abs(delta) <= max) return;
        Facing = facing0 + Mathf.Sign(delta) * max;
        Rotation = new Vector3(0, Facing, 0);
    }

    private void AiOnStunned()
    {
        if (!_ai) return;
        if (_busyCancel != null && _busyCc) CancelChannel(Has(StatusId.Freeze) ? "freeze" : "stun");
        if (_grabbed != null) GrabRelease("stun");
    }

    /// <summary>Idle frame: dormant ambushers wait for their trigger instead of waking at sight. True when handled.</summary>
    private bool AiIdle(float dt, float distToPlayer)
    {
        if (!_ai) return false;
        if (_dormant) { DormantIdle(distToPlayer); return true; }
        if (_turret && !_aggro) { Model.Play(Def.Idle, 1f, 0.3f); var p = World.P; if (distToPlayer < Def.Range && !p.Dead && !p.Stealthed && (World.Grid?.LineShot(Pos2, p.Pos2) ?? true)) WakePack(); return true; }
        return false;
    }

    // ───────────────────────── chase ─────────────────────────

    /// <summary>The kinds' and traits' own decisions while chasing. True when this frame is spent.</summary>
    private bool AiChase(float dt, float dist, bool los, float reach)
    {
        if (!_ai) return false;
        if (Def.Role == "prop") { Model.Play(Def.Idle, 1f, 0.3f); return true; }
        bool sight = los && (IsRanged || World.Grid == null || World.Grid.LineShot(Pos2, _target.Pos2));
        switch (Def.AI)
        {
            case MonAI.Swarm: return SwarmChase(dt);
            case MonAI.Turret: return TurretChase(dt, dist, sight);
            case MonAI.Flyer: return FlyerChase(dt, dist, sight);
            case MonAI.Burrower: if (BurrowChase(dt, dist, reach)) return true; break;
            case MonAI.Stalker: if (StalkerChase(dt, dist)) return true; break;
            case MonAI.Charger: if (TryCharge(dist)) return true; break;
            case MonAI.Shieldbearer: if (TryShieldDash(dist)) return true; break;
            case MonAI.Healer: if (TryHeal()) return true; if (KeepAway(dt, dist, 9f, 12f)) return true; break;
            case MonAI.Reviver: if (TryRevive()) return true; if (KeepAway(dt, dist, 9f, 12f)) return true; break;
            case MonAI.Spawner: if (TrySpawnYoung()) return true; if (Def.Speed > 0.1f && KeepAway(dt, dist, 8f, 10f)) return true; break;
            case MonAI.Caller: if (TryCall()) return true; if (!IsRanged && KeepAway(dt, dist, 8f, 10f)) return true; break;
        }
        if (_bur is 1 or 2 or 4) return true;
        // traits that are specials
        if (Def.Is(MonTrait.Grab) && TryGrab(dist, reach)) return true;
        if (Def.Is(MonTrait.Hook) && TryHook(dist, sight)) return true;
        if (Def.Is(MonTrait.Breath) && TryBreath(dist, sight)) return true;
        if (Def.Is(MonTrait.Eruption) && TryErupt(dist, sight)) return true;
        if (Def.Is(MonTrait.Absorb) && TryAbsorb(dist)) return true;
        if (Def.Is(MonTrait.Empower) && TryEmpower()) return true;
        if (Def.Is(MonTrait.Pulse) && Def.AI != MonAI.Brute && TryPulse(dist)) return true;
        // a breather of the Caster / Ranged kinds has no missile: between breaths it holds its distance
        if (Def.Is(MonTrait.Breath) && Def.AI is MonAI.Caster or MonAI.Ranged && dist <= reach + 0.5f && sight)
        {
            if (dist < 3.5f && !Rooted && KeepAway(dt, dist, 3.5f, A.BreathLen)) return true;
            FaceDir(_target.Pos2 - Pos2, 1f - Mathf.Exp(-8f * dt));
            Model.Play(Def.Idle, 1f, 0.25f);
            Separate(dt);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Keeps a distance band from the target (healers, revivers, callers, mobile mothers). Inside 3 m the monster
    /// is cornered and fights with its melee clip (returns false so the ordinary attack runs).
    /// </summary>
    private bool KeepAway(float dt, float dist, float min, float max)
    {
        if (dist < 3f) return false;
        Vector2 to = _target.Pos2 - Pos2;
        if (dist < min && !Rooted)
        {
            Vector2 away = -to.Normalized();
            if (World.Grid == null || World.Grid.WalkAt(Pos2 + away * 2.5f)) { MoveDir(away, dt); return true; }
        }
        else if (dist > max) { MoveToward(_target.Pos2, dt, max - 0.5f); return true; }
        FaceDir(to, 1f - Mathf.Exp(-8f * dt));
        Model.Play(Def.Idle, 1f, 0.25f);
        Separate(dt);
        return true;
    }

    // ───────────────────────── channels ─────────────────────────

    /// <summary>A Busy spell that crowd control (and death) can break: <paramref name="cancel"/> undoes what was set up.</summary>
    private void Channel(float time, Action end, Action<float> tick, Action cancel, bool cancelOnCc = true)
    {
        Busy(time, end, tick);
        _busyCancel = cancel ?? (() => { });
        _busyCc = cancelOnCc;
    }

    private void CancelChannel(string why)
    {
        if (_st != Mode.Busy || _busyCancel == null) return;
        var c = _busyCancel;
        _busyCancel = null; _busyEnd = null; _busyTick = null;
        _st = Mode.Chase;
        CancelTelegraph();
        try { c(); }
        catch (Exception e) { Log.Error("monster-ai", $"cancelling the channel of {Def.Id} failed", e); }
        Note("channel.broken", why);
    }

    // ───────────────────────── attacks ─────────────────────────

    /// <summary>Wind-up of an ordinary attack: some kinds swap the clip, set their own telegraph or timing.</summary>
    private string AiStartAttack(string anim)
    {
        if (!_ai) return anim;
        switch (Def.AI)
        {
            case MonAI.Shieldbearer:
                if (Def.Rig == "A" && CharModel.HasClip("Shield_OneShot")) anim = "Shield_OneShot";
                break;
            case MonAI.Turret:
                _t = 1.2f;
                _attackDir = Forward;                    // it fires where it faces: the turn rate lets the hero out-run the aim
                _attackPos = Pos2 + _attackDir * Def.Range;
                _telegraph = Fx.TelegraphLine(Pos2, _attackDir, Def.Range, 1.6f, _t, Palette.Danger);
                Snd.At("telegraph_warn", GlobalPosition, 0.4f);
                _shutterOpenUntil = Now + 1.2 + 1.5;
                break;
            case MonAI.Bomber:
                _bombMelee = A.BombMelee && _target != null && Pos2.DistanceTo(_target.Pos2) < 4f;
                if (_bombMelee)
                {
                    _telegraph = Fx.TelegraphCone(this, Def.Range * 0.3f + Radius + 1.6f, 110f, Mathf.Max(_t, 0.6f), Palette.Danger);
                    _t = Mathf.Max(_t, 0.6f);
                    anim = A.MeleeAnim ?? (Def.Rig == "A" ? "Sword_Attack" : "Attack1");
                }
                break;
        }
        if (_lit) Model.Flash(Palette.Ember, 0.4f);
        return anim;
    }

    private bool _bombMelee;
    private double _shutterOpenUntil;

    private bool AiResolveAttack(Vector3 hand)
    {
        if (!_ai) return false;
        switch (Def.AI)
        {
            case MonAI.Bomber: BomberResolve(hand); return true;
            case MonAI.Turret: TurretFire(); return true;
            case MonAI.Shieldbearer:
            {
                float reach = Def.Range + Radius + _target.Radius + 0.5f;
                Vector2 to = _target.Pos2 - Pos2;
                if (to.Length() <= reach && to.Normalized().Dot(Forward) > 0.2f)
                {
                    Strike(_target, AiFirstMul(), Def.OnHit.HasValue && Def.OnHit.Value.Id == StatusId.Knockback ? null : St.Knock(6f));
                    Fx.Hit(_target.Chest, Def.Element, false);
                    Snd.At("shield_bash", _target.GlobalPosition, 0.7f);
                    Note("Shieldbearer.bash");
                }
                else Snd.At("swing_light", GlobalPosition, 0.4f);
                return true;
            }
        }
        if (Def.Is(MonTrait.Extinguish)) Extinguish(Pos2 + Forward * 1.5f, 2.5f);
        return false;
    }

    /// <summary>The multiplier of the next blow: a dormant ambusher's waking strike, used once.</summary>
    private float AiFirstMul()
    {
        float m = _firstMul;
        _firstMul = 1f;
        return m;
    }

    /// <summary>An ordinary missile, or the trait's version of it (snare, returning disc); trails leave ground where it ends.</summary>
    private void AiShoot(Vector3 hand, Vector2 dir)
    {
        if (_ai && Def.Is(MonTrait.Snare)) { MonsterShot.Snare(this, hand, dir); Note("Snare.throw"); return; }
        if (_ai && Def.Is(MonTrait.Return)) { MonsterShot.Disc(this, hand, dir); Note("Return.throw"); return; }
        var pr = Projectile.SpawnMonster(this, hand, dir, Def.ProjSpeed, Def.Range + 8f, AiFirstMul(), Def.Element, Def.OnHit);
        if (_ai && Def.Is(MonTrait.Trail) && A.Trail != null)
        {
            var self = this;
            pr.OnEnd = at => { if (IsInstanceValid(self)) self.AiTrailAt(at); };
        }
    }

    /// <summary>After a bruiser's slam: wax pools, the bell's pulse, the snuffing of fire.</summary>
    private void AiAfterSlam(Vector2 at)
    {
        if (!_ai) return;
        if (Def.Is(MonTrait.Trail)) AiTrailAt(at);
        if (Def.Is(MonTrait.Extinguish)) Extinguish(at, 3f);
        if (Def.Is(MonTrait.Pulse) && _aiCd[CdPulse] <= 0f) StartPulse();
    }

    // ───────────────────────── hits on the hero ─────────────────────────

    /// <summary>
    /// One blow of this monster on one hostile: damage through Combat.MonsterHit with the type's status, its second
    /// status, a lit monster's burn, an extra status the behaviour adds, and Drain. Returns the damage dealt.
    /// </summary>
    public float Strike(Actor t, float mul, StatusApply? extra = null, bool melee = true, Element? element = null)
    {
        if (t == null || t.Dead || !IsInstanceValid(t)) return 0f;
        Element el = element ?? Def.Element;
        StatusApply? first = _lit && A.LitBurn.HasValue ? A.LitBurn : Def.OnHit;
        float dealt = Combat.MonsterHit(this, t, mul, el, first, melee);
        if (dealt <= 0f || t.Dead) return dealt;
        if (_lit && Def.OnHit.HasValue) ApplyHostile(t, Def.OnHit.Value, el);
        if (Def.OnHit2.HasValue) ApplyHostile(t, Def.OnHit2.Value, el);
        if (extra.HasValue) ApplyHostile(t, extra.Value, el);
        if (_ai && Def.Is(MonTrait.Drain) && t is Player p) DrainHero(p);
        return dealt;
    }

    /// <summary>A status from this monster on a hostile, with damage-over-time scaled like Combat.MonsterHit does it.</summary>
    public void ApplyHostile(Actor t, StatusApply st, Element el)
    {
        if (t == null || t.Dead) return;
        if (t is Player p)
        {
            if (p.Unstoppable && st.Id is not (StatusId.Burn or StatusId.Poison or StatusId.Bleed or StatusId.Rot or StatusId.Weaken or StatusId.Vulnerable)) return;
            if (st.Id is StatusId.Burn or StatusId.Poison or StatusId.Bleed or StatusId.Rot)
            {
                var h = p.Hero;
                Element de = Combat.DotElement(st.Id, el);
                float red = (1f - Balance.ArmorDr(h.Armor, Level)) * (1f - h.Res(de) / 100f) * (1f - h.S[Stat.DmgReduction] / 100f) * p.ExtraDr();
                float raw = BaseDamage * WeakenMul;
                p.ApplyStatus(st, this, raw * st.Mag * red / MathF.Max(st.Dur, 0.1f), de);
            }
            else p.ApplyStatus(st, this);
            return;
        }
        if (st.Id is StatusId.Burn or StatusId.Poison or StatusId.Bleed or StatusId.Rot) return;    // minions: the hit already hurt
        t.ApplyStatus(st, this);
    }

    // ───────────────────────── damage taken ─────────────────────────

    private static bool IsZoneHit(Hit h) => h.Dir == Vector2.Zero && h.Skill != null && h.Skill.Kind is SkillKind.Zone or SkillKind.Ground or SkillKind.Trap;
    private static bool IsAreaHit(Hit h) => h.Skill != null && (h.Skill.Kind is SkillKind.Zone or SkillKind.Ground or SkillKind.Trap or SkillKind.Nova or SkillKind.Wave or SkillKind.Leap
        || h.Skill.Explode > 0f || (h.Skill.Kind == SkillKind.Melee && h.Skill.Arc >= 150f));

    /// <summary>Where a hit came from, as a direction from this monster (zero when unknown).</summary>
    private Vector2 IncomingFrom(Hit h)
    {
        if (h.Dir != Vector2.Zero) return -h.Dir.Normalized();
        if (h.Source != null && IsInstanceValid(h.Source) && h.Source != this) { var d = h.Source.Pos2 - Pos2; return d.LengthSquared() > 1e-4f ? d.Normalized() : Vector2.Zero; }
        return Vector2.Zero;
    }

    private bool InFront(Vector2 from, float arcDeg) => from != Vector2.Zero && from.Dot(Forward) >= Mathf.Cos(Mathf.DegToRad(arcDeg * 0.5f));

    /// <summary>Adjusts an incoming hit (guard, front armour, shell, cloud, shutter, mask, statue). False drops it.</summary>
    private bool AiModifyHit(ref Hit h)
    {
        if (!_ai && Now >= _maskUntil) return true;
        if (_pile) { h.Amount = MathF.Max(h.Amount, Life + 1f); return true; }
        if (Now < _maskUntil) { h.Amount *= 0.6f; h.NoReact = true; }
        if (!_ai) return true;
        if (_flyer && IsZoneHit(h)) return false;                 // a flyer is above the burning ground (its height is in the model)
        if (_dormant && A.DormantMode == "statue") h.Amount *= 0.2f;
        if (_swarm) h.Amount *= h.Dot ? 1.5f : IsAreaHit(h) ? 1f : 0.35f;
        if (_turret && A.Shutter && Now >= _shutterOpenUntil && _st is not Mode.Windup) h.Amount *= 0.3f;
        if (!h.Dot && !IsZoneHit(h))
        {
            var from = IncomingFrom(h);
            if (Def.AI == MonAI.Shieldbearer)
            {
                if (_guardUp && InFront(from, A.GuardArc))
                {
                    h.Amount *= A.GuardDr; h.NoReact = true;
                    if (Now > _blockWindow) _blocks = 0;
                    _blocks++; _blockWindow = Now + 4.0;
                    Snd.At("shield_block", GlobalPosition, 0.5f);
                    Note("Shieldbearer.block");
                }
                else if (!_guardBroken && from != Vector2.Zero && !InFront(from, 180f))
                {
                    // hits from behind count whatever it is doing (a bash in progress included)
                    if (++_backHits >= 3) BreakGuard("three hits from behind");
                }
                if (_guardBroken) h.Amount *= 1.25f;
            }
            if (Def.Is(MonTrait.FrontArmour) && InFront(from, A.ArmourArc)) { h.Amount *= 0.5f; Note("FrontArmour.deflect"); }
        }
        if (_shellUp && Barrier > 0f && h.El == Element.Fire && !h.Dot)
        {
            float r = h.Amount;
            h.Amount = 3f * r <= Barrier ? 3f * r : Barrier + (r - Barrier / 3f);
            Note("Shell.fire");
        }
        return true;
    }

    private void AiAfterHit(Hit h, float dealt)
    {
        if (!_ai || Dead) return;
        double now = Now;
        if (!h.Dot) _lastHit = now;
        if (Def.AI == MonAI.Stalker && !h.Dot && h.Amount > 0f) Reveal(3f);
        if (_dormant && A.DormantMode == "statue" && h.Amount > 0f) Wake("struck");
        if (_grabbed != null) { _grabTaken += h.Amount; if (_grabTaken >= MaxLife * 0.08f) GrabRelease("struck"); }
        if (Def.Is(MonTrait.Flammable) && !_lit && h.El == Element.Fire && h.Amount > 0f) Ignite("fire damage");
        if (Def.Is(MonTrait.Absorb) && h.Skill != null && h.Skill.Kind == SkillKind.Proj) _absorbWant = now;
        // a heavy blow is a flinch for the casters' channels (heal, revive, call)
        if (_busyCancel != null && _busyCc && !h.Dot && !h.NoReact && h.Amount > MaxLife * 0.05f * Def.Poise && !IsElite && Def.AI is MonAI.Healer or MonAI.Reviver or MonAI.Caller)
            CancelChannel("flinch");
    }

    protected override void OnBarrierEnd()
    {
        if (!_shellUp || Barrier > 0f) return;
        _shellUp = false;
        Model.SetRim(BaseRim());
        Fx.Burst(Chest, Element.Frost, 2.4f, 1.2f);
        Snd.At("frost_shatter", GlobalPosition, 0.9f);
        Note("Shell.broken");
        if (!Dead && Brain == null)
        {
            CancelTelegraph();
            Model.Play("Hit_Knockback", 1f, 0.05f, true);
            Busy(1f, null);
        }
    }

    protected override bool PreventDeath(Hit h)
    {
        if (Faction != Faction.Monster) return false;
        if (Now < _maskUntil && !_maskRaised)
        {
            _maskRaised = true; _maskUntil = 0;
            Life = MaxLife * 0.4f;
            MaskVisual(false);
            Fx.Burst(Chest, Element.Lightning, 1.6f, 0.9f);
            Snd.At("barrier_break", GlobalPosition, 0.8f);
            Note("Empower.raise");
            return true;
        }
        if (_ai && Def.Is(MonTrait.Reform) && !_reformed && !NoReform && !_pile && !Has(StatusId.Freeze) && !InFrostZone())
        {
            BecomePile();
            return true;
        }
        return false;
    }

    public override void ApplyStatus(StatusApply s, Actor source, float dotDps = 0f, Element dotEl = Element.Fire, bool minion = false)
    {
        if (_ai && Faction == Faction.Monster && !AiAcceptStatus(s, source)) return;
        base.ApplyStatus(s, source, dotDps, dotEl, minion);
    }

    private bool AiAcceptStatus(StatusApply s, Actor source)
    {
        if (_pile || (AiHidden && _dormant)) return false;
        bool displace = s.Id is StatusId.Knockback or StatusId.Pull;
        if (_swarm && (displace || s.Id is StatusId.Root or StatusId.Bleed)) return false;
        if ((_turret || _shellUp || _charging || _bur != 3 && _bur != 0) && displace) return false;
        if (Def.AI == MonAI.Shieldbearer && _guardUp && source != null && IsInstanceValid(source) && s.Id is StatusId.Stun or StatusId.Freeze or StatusId.Knockback)
        {
            var from = source.Pos2 - Pos2;
            if (from.LengthSquared() > 1e-4f && InFront(from.Normalized(), A.GuardArc))
            {
                BreakGuard(s.Id.ToString().ToLowerInvariant());
                if (s.Id == StatusId.Knockback) return false;     // the shield takes the shove
            }
        }
        if (_lit && s.Id is StatusId.Chill or StatusId.Freeze) PutOut("chill");
        return true;
    }

    // ───────────────────────── death ─────────────────────────

    private void AiOnDeath(Hit h)
    {
        GlowOff();
        if (!_ai && Now >= _maskUntil) { RecordRemains(); return; }
        bool expiry = h.Source == null && h.Amount <= 0f;
        if (_busyCancel != null) { var c = _busyCancel; _busyCancel = null; try { c(); } catch (Exception e) { Log.Error("monster-ai", "channel cleanup at death failed", e); } }
        if (_grabbed != null) GrabRelease("death");
        if (AiHidden) SetHidden(false);
        if (Stealthed || _stealthOn) { _stealthOn = false; Stealthed = false; Model.SetVisibility(1f); }
        if (_auraRing != null && IsInstanceValid(_auraRing)) _auraRing.Visible = false;
        if (_maskFx != null && IsInstanceValid(_maskFx)) _maskFx.Visible = false;
        if (_litFx != null && IsInstanceValid(_litFx)) _litFx.QueueFree();
        Absorbers.Remove(this);
        if (!_ai) { RecordRemains(); return; }
        if (Def.Id == "shroud_eggsac") return;
        if (Def.Is(MonTrait.SplitOnDeath) && !expiry) SplitApart();
        if (Def.Is(MonTrait.DeathBurst) && !expiry) DeathBurst();
        if (Def.AI == MonAI.Spawner && !expiry) ReleaseYoung();
        if (_lit && !expiry) LitDeathPatch();
        if (Def.Id == "reaver_cagebearer" && !expiry && !NoLoot) DropStolenEmber();
        RecordRemains();
    }

    // ───────────────────────── small helpers ─────────────────────────

    /// <summary>The rim the monster had at spawn (its elite affix colour, or none).</summary>
    private Color BaseRim()
    {
        if (Affixes.Count == 0 && !MiniBoss) return Colors.Black;
        var c = Affixes.Count > 0 ? MonsterDb.Affix(Affixes[0])?.Color ?? Palette.Danger : Palette.Danger;
        return c * (Tier == MonTier.Elite || MiniBoss ? 1.4f : 0.9f);
    }

    private void SetHidden(bool hidden)
    {
        AiHidden = hidden;
        Untargetable = hidden;
        if (Model != null && _sleep == 0) Model.Visible = !hidden;
    }

    /// <summary>The definition a behaviour summons, in the region's own colours where it has a reskin.</summary>
    private MonsterDef LocalDef(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var def = Bestiary.Local(id) ?? MonsterDb.Get(id);
        if (def == null) Log.Warn("monster-ai", $"{Def.Id} summons unknown type '{id}'");
        return def;
    }

    /// <summary>A monster this one brings into the fight: already aggro'd, in its pack, no loot, a share of the experience.</summary>
    private Monster Summon(MonsterDef def, Vector2 at, float xpShare, MonTier tier = MonTier.Normal)
    {
        if (def == null || World.Root == null) return null;
        var m = Monster.Spawn(def, Level, tier, World.Grid?.NearestWalk(at) ?? at, Faction.Monster);
        m.Pack = Pack; m.NoLoot = true; m.XpValue *= xpShare; m._owner = this;
        m._aggro = true;
        return m;
    }

    /// <summary>Wakes this monster at once (used by the harness and by behaviours that hand a monster to the fight).</summary>
    public void AiWakeNow() { if (_dormant) Wake("forced"); _aggro = true; }

    /// <summary>For the harness: which behaviour state this monster is in.</summary>
    public string AiState => !_ai ? "-" : $"kind={Def.AI} traits={Def.Traits} st={_st} hidden={AiHidden} stealth={Stealthed} bur={_bur} dormant={_dormant} shell={_shellUp} lit={_lit} pile={_pile} guard={(_guardBroken ? "broken" : _guardUp ? "up" : "-")}";
}
