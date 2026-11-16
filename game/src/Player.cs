using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// The hero in the world: mouse/keyboard control, movement, the cast state machine, resources, cooldowns, evade,
/// potions and every hook that gear powers and passives attach to. Skill behaviour itself lives in SkillExec.
/// Controls: LMB move / basic skill / interact, RMB core skill, 1-4 skills, Q potion, Space evade, Shift stand still,
/// WASD direct movement.
/// </summary>
public partial class Player : Actor
{
    public Hero Hero;
    public SkillExec Exec;
    public float Resource;
    public int Pips, Potions;
    public sealed class Cd { public float Left, Max; public int Charges = 1, MaxCharges = 1; }
    public readonly Dictionary<string, Cd> Cds = new();
    public float EvadeCd, EvadeMax = 5f;
    public int Form;                                 // wildsoul: 0 human, 1 bear, 2 wolf
    private readonly CharModel[] _forms = new CharModel[3];
    private float _formScale = 1f;

    private enum Act { Free, Cast, Channel, Dash }
    private Act _act;
    private float _actT, _contactT, _lockT;
    private bool _fired;
    private SkillDef _castDef;
    private int _castSlot = -1;
    private Vector2 _castPoint;
    private Actor _castTarget;
    private float _chanTick, _chanTime;
    // dash / leap / evade movement
    private Vector2 _dashFrom, _dashTo;
    private float _dashDur, _dashArc;
    private Action _dashEnd;
    private Action<Vector2, Vector2> _dashStep;
    private bool _dashIframes;
    // orders
    private readonly List<Vector2> _path = new();
    private int _pathI;
    private bool _hasMove;
    private Interactable _goUse;
    private (int slot, Actor target)? _pending;
    private float _repath;
    // aim
    public Vector2 Aim;
    public Actor Hover;
    public Interactable HoverObj;
    // timers and counters used by powers
    public float InCombat, IFrames, StealthLeft, SkirmishLeft, ShiftLeft, FlowLeft, EmberPause, SecondWindCd, VolleyLeft;
    public int FlowStacks, ResonanceStacks;
    public Element LastElement = Element.Physical;
    public bool FreeNext;
    private bool _ambush;
    private readonly Dictionary<string, int> _nth = new();
    private float _regen, _potionTimer, _burnTick, _stepT;
    private readonly bool[] _press = new bool[6];
    private bool _pressEvade, _pressPotion;
    public const float BaseSpeed = 6.4f;
    public bool InputEnabled = true;
    /// <summary>Statistics for the test harness: skills that reached their contact frame, channel ticks, the last skill that fired.</summary>
    public int Fired, ChanTicks;
    /// <summary>Test harness: how often --god saved the hero from a lethal blow (static: survives area changes).</summary>
    public static int GodSaves;
    public string LastFired;
    /// <summary>Test harness: holds the channelled skill in this bar slot as if its key were down (-1 = real input only).</summary>
    public int ForceHold = -1;
    public event Action Died;

    public bool Invulnerable => IFrames > 0f || Hero.BuffFlag(b => b.Immune) || Dead;
    public override bool Unstoppable => Hero.BuffFlag(b => b.Unstoppable || b.Immune) || IFrames > 0f;
    public bool Stealthed => StealthLeft > 0f;
    public bool Busy => _act != Act.Free;
    public float ResourceFrac => Hero.MaxResource <= 0 ? 0 : Resource / Hero.MaxResource;
    public float LifeFrac => MaxLife <= 0 ? 0 : Life / MaxLife;

    public void Init(Hero hero)
    {
        Hero = hero;
        Faction = Faction.Player;
        DisplayName = hero.D.Name;
        Radius = 0.45f;
        Name = "Player";
        Exec = new SkillExec(this);
        Model = CharModel.Create("hero_" + Names.ClassKey(hero.Class));
        AddChild(Model);
        HeightM = Model.Height;
        if (hero.Class == ClassId.Wildsoul)
        {
            _forms[0] = Model;
            _forms[1] = CharModel.Create("form_bear"); _forms[2] = CharModel.Create("form_wolf");
            for (int i = 1; i < 3; i++) { AddChild(_forms[i]); _forms[i].Visible = false; }
        }
        Model.SetRim(Names.ClassColor(hero.Class) * 0.35f);
        // a small warm light carried by the hero: the ember that keeps them readable in the dark
        var ember = new OmniLight3D { LightColor = new Color(1f, 0.72f, 0.45f), LightEnergy = 1.6f, OmniRange = 9f, OmniAttenuation = 1.4f, Position = new Vector3(0, 2.2f, 0.4f), ShadowEnabled = false };
        ember.LightSpecular = 0.2f;
        AddChild(ember);
        hero.Changed += OnHeroChanged;
        hero.LevelUp += OnLevelUp;
        OnHeroChanged();
        Life = MaxLife;
        Resource = Names.IsBuilder(hero.Class) ? 0 : hero.MaxResource;
        Potions = hero.D.PotionCap;
        World.MonsterKilled += OnKill;
        UpdateWeaponTier();
    }

    public override void _ExitTree()
    {
        if (Hero != null) { Hero.Changed -= OnHeroChanged; Hero.LevelUp -= OnLevelUp; }
        World.MonsterKilled -= OnKill;
    }

    private void OnHeroChanged()
    {
        // a change of maximum life keeps the life fraction: gear and buffs neither heal nor hurt
        float frac = MaxLife > 0 ? Mathf.Clamp(Life / MaxLife, 0f, 1f) : 1f;
        if (!Mathf.IsEqualApprox(MaxLife, Hero.MaxLife))
        {
            MaxLife = Hero.MaxLife;
            if (!Dead) Life = Mathf.Max(1f, MaxLife * frac);
        }
        Life = Mathf.Min(Life, MaxLife);
        Resource = Mathf.Min(Resource, Hero.MaxResource);
        Pips = Mathf.Min(Pips, Hero.MaxPips);
        EvadeMax = 5f * (1f - Hero.Asp("ashen_step") / 100f);
        UpdateWeaponTier();
    }

    private void UpdateWeaponTier() => Model?.SetWeaponTier(WeaponTierOf(Hero.Weapon));

    /// <summary>Which weapon mesh (wpn_1/2/3) a weapon shows on the model: legendary 3, rare or power 250+ 2, else (and bare hands) 1.</summary>
    public static int WeaponTierOf(Item w) => w == null ? 1 : w.Rarity >= Rarity.Legendary ? 3 : w.Rarity >= Rarity.Rare || w.Power >= 250 ? 2 : 1;

    private void OnLevelUp(int lvl)
    {
        Life = MaxLife; Potions = Hero.D.PotionCap;
        if (!Names.IsBuilder(Hero.Class)) Resource = Hero.MaxResource;
        Fx.LevelUp(this);
        Snd.At("level_up", GlobalPosition);
        Log.Info("hero", $"level up → {lvl} life={MaxLife} skillpts={Hero.SkillPoints}");
        Game.I.OnLevelUp(lvl);
    }

    // ───────────────────────── input ─────────────────────────

    public override void _UnhandledInput(InputEvent e)
    {
        if (!InputEnabled || Dead) return;
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left) _press[0] = true;
            else if (mb.ButtonIndex == MouseButton.Right) _press[1] = true;
        }
        else if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            switch (k.Keycode)
            {
                case Key.Key1: _press[2] = true; break;
                case Key.Key2: _press[3] = true; break;
                case Key.Key3: _press[4] = true; break;
                case Key.Key4: _press[5] = true; break;
                case Key.Space: _pressEvade = true; break;
                case Key.Q: _pressPotion = true; break;
            }
        }
    }

    private bool Held(int slot) => slot == ForceHold || InputEnabled && !Game.I.UiBlocksMouse && slot switch
    {
        0 => Input.IsMouseButtonPressed(MouseButton.Left), 1 => Input.IsMouseButtonPressed(MouseButton.Right),
        2 => Input.IsKeyPressed(Key.Key1), 3 => Input.IsKeyPressed(Key.Key2), 4 => Input.IsKeyPressed(Key.Key3), _ => Input.IsKeyPressed(Key.Key4)
    };

    private void UpdateAim()
    {
        var cam = GetViewport().GetCamera3D();
        if (cam == null) return;
        var mp = GetViewport().GetMousePosition();
        Vector3 o = cam.ProjectRayOrigin(mp), d = cam.ProjectRayNormal(mp);
        if (Mathf.Abs(d.Y) > 1e-4f) { float t = -o.Y / d.Y; if (t > 0) Aim = World.To2(o + d * t); }
        Hover = null; HoverObj = null;
        if (Game.I.UiBlocksMouse) return;
        // enemies are picked a little above the ground so tall monsters are easy to click
        float t2 = Mathf.Abs(d.Y) > 1e-4f ? (1.0f - o.Y) / d.Y : 0;
        Vector2 aimHigh = World.To2(o + d * t2);
        float best = 1e9f;
        foreach (var m in World.Monsters)
        {
            if (m.Dead || m.Untargetable) continue;
            float dist = Mathf.Min(m.Pos2.DistanceTo(Aim), m.Pos2.DistanceTo(aimHigh)) - m.Radius;
            if (dist < 1.1f && dist < best) { best = dist; Hover = m; }
        }
        if (Hover == null) HoverObj = Interactable.Pick(Aim, 1.6f);
    }

    // ───────────────────────── frame ─────────────────────────

    private int _tickErrors;

    public override void _Process(double delta)
    {
        if (Game.I.Paused) return;
        if (Dead) return;
        long t0 = Prof.Now;
        try { Tick((float)delta); }
        catch (Exception e)
        {
            // never let an exception escape the frame: log it, drop the action in progress and carry on
            if (_tickErrors++ < 30) Log.Error("hero", $"update failed (act={_act}, skill={_castDef?.Id ?? "-"})", e);
            _act = Act.Free; _castDef = null; _dashEnd = null; _dashStep = null;
            ClearPress();
        }
        Prof.Player += Prof.Now - t0;
    }

    private void Tick(float dt)
    {
        TickStatuses(dt);
        TickTimers(dt);
        UpdateAim();

        bool stunned = Stunned && !Unstoppable;
        if (stunned) { Model.Play("Hit_Chest", 0.5f); ClearPress(); return; }

        if (_pressPotion) { _pressPotion = false; UsePotion(); }
        if (_pressEvade) { _pressEvade = false; TryEvade(); }

        switch (_act)
        {
            case Act.Dash: TickDash(dt); break;
            case Act.Cast: TickCast(dt); break;
            case Act.Channel: TickChannel(dt); break;
        }
        if (_act == Act.Free || _act == Act.Channel) HandleOrders(dt);
        ClearPress();
    }

    private void ClearPress() { for (int i = 0; i < 6; i++) _press[i] = false; _pressEvade = false; _pressPotion = false; }

    private void TickTimers(float dt)
    {
        var ended = Hero.TickBuffs(dt);
        if (ended != null) foreach (var b in ended) Exec.OnBuffEnd(b);
        foreach (var cd in Cds.Values)
        {
            if (cd.Charges >= cd.MaxCharges) continue;
            cd.Left -= dt;
            if (cd.Left <= 0f) { cd.Charges++; cd.Left = cd.Charges < cd.MaxCharges ? cd.Max : 0f; }
        }
        if (EvadeCd > 0) EvadeCd -= dt;
        if (IFrames > 0) IFrames -= dt;
        if (InCombat > 0) InCombat -= dt;
        if (SkirmishLeft > 0) SkirmishLeft -= dt;
        if (ShiftLeft > 0) ShiftLeft -= dt;
        if (VolleyLeft > 0) VolleyLeft -= dt;
        if (EmberPause > 0) EmberPause -= dt;
        if (SecondWindCd > 0) SecondWindCd -= dt;
        if (FlowLeft > 0) { FlowLeft -= dt; if (FlowLeft <= 0) FlowStacks = 0; }
        if (StealthLeft > 0) { StealthLeft -= dt; if (StealthLeft <= 0) EndStealth(); }

        // resource: builders decay out of combat, pools regenerate
        var cls = Hero.Class;
        if (Names.IsBuilder(cls))
        {
            if (InCombat <= 0 && !Hero.HasKeystone("ks_fury")) Resource = Mathf.Max(0, Resource - 4f * dt);
        }
        else Resource = Mathf.Min(Hero.MaxResource, Resource + Names.Regen(cls) * (1f + Hero.S[Stat.ResourceGen] / 100f) * dt);
        if (Hero.D.Spec == "ws_raven") Resource = Mathf.Min(Hero.MaxResource, Resource + 3f * dt);

        _regen += dt;
        if (_regen >= 0.5f)
        {
            _regen = 0;
            float heal = Hero.S[Stat.LifeRegen] * 0.5f + Hero.BuffSum(b => b.RegenPct) * MaxLife * 0.005f;
            if (heal > 0) Heal(heal);
            if (Hero.HasUnique("u_emberheart") && EmberPause <= 0) Life = Mathf.Max(1, Life - MaxLife * 0.005f);
        }
        _potionTimer += dt;
        if (Potions < Hero.D.PotionCap && _potionTimer >= 30f) { Potions++; _potionTimer = 0; }
        else if (Potions >= Hero.D.PotionCap) _potionTimer = 0;
        Exec.Tick(dt);
    }

    // ───────────────────────── movement / orders ─────────────────────────

    private float MoveSpeed => BaseSpeed * Hero.MoveMul * SlowMul * (Form == 2 ? 1.15f : 1f) * (_act == Act.Channel ? 0.8f : 1f);

    private void HandleOrders(float dt)
    {
        bool shift = Input.IsKeyPressed(Key.Shift);
        // skills
        if (_act == Act.Free)
        {
            for (int slot = 5; slot >= 0; slot--)
            {
                bool want = _press[slot] || (Held(slot) && (slot != 0 || Hover != null || shift));
                if (!want) continue;
                if (slot == 0 && !shift)
                {
                    if (_press[0] && HoverObj != null) { GoUse(HoverObj); break; }
                    if (Hover == null) continue;       // plain ground click = move
                }
                if (TryCast(slot, Aim, Hover, _press[slot])) { _hasMove = false; _goUse = null; return; }
            }
        }
        // movement
        Vector2 wish = Vector2.Zero;
        if (InputEnabled)
        {
            if (Input.IsKeyPressed(Key.W)) wish.Y -= 1; if (Input.IsKeyPressed(Key.S)) wish.Y += 1;
            if (Input.IsKeyPressed(Key.A)) wish.X -= 1; if (Input.IsKeyPressed(Key.D)) wish.X += 1;
        }
        if (wish != Vector2.Zero) { _hasMove = false; _goUse = null; _pending = null; }
        else if (Held(0) && Hover == null && HoverObj == null && !shift && !Game.I.UiBlocksMouse && _pending == null)
        {
            if (Aim.DistanceTo(Pos2) > 0.6f) SetMove(Aim, false);
        }

        if (_pending is { } pc)
        {
            if (pc.target == null || !IsInstanceValid(pc.target) || pc.target.Dead) _pending = null;
            else
            {
                var def = Hero.Eff(Hero.D.Bar[pc.slot]);
                float reach = (def?.Radius ?? 2.5f) * 0.8f + pc.target.Radius;
                if (def == null) _pending = null;
                else if (Pos2.DistanceTo(pc.target.Pos2) <= reach) { _pending = null; _hasMove = false; if (TryCast(pc.slot, pc.target.Pos2, pc.target, true)) return; }
                else { _repath -= dt; if (_repath <= 0) { SetMove(pc.target.Pos2, true); _repath = 0.25f; } }
            }
        }
        if (_goUse != null)
        {
            if (!IsInstanceValid(_goUse) || !_goUse.CanUse) _goUse = null;
            else if (Pos2.DistanceTo(_goUse.Pos2) <= _goUse.UseRange) { var u = _goUse; _goUse = null; _hasMove = false; FaceDir(u.Pos2 - Pos2); u.Use(this); }
        }

        Vector2 vel = Vector2.Zero;
        if (Rooted && !Unstoppable) { _hasMove = false; }
        else if (wish != Vector2.Zero) vel = wish.Normalized() * MoveSpeed;
        else if (_hasMove)
        {
            while (_pathI < _path.Count && Pos2.DistanceTo(_path[_pathI]) < 0.35f) _pathI++;
            if (_pathI >= _path.Count) _hasMove = false;
            else vel = (_path[_pathI] - Pos2).Normalized() * MoveSpeed;
        }

        if (vel != Vector2.Zero)
        {
            var moved = Move(vel * dt);
            if (_act != Act.Channel) FaceDir(vel, 1f - Mathf.Exp(-18f * dt));
            if (moved.Length() < MoveSpeed * dt * 0.15f && _hasMove) { _repath -= dt; if (_repath <= -0.3f) _hasMove = false; }
            if (_act == Act.Free)
            {
                // the clip whose stride fits the speed (walk when slowed, jog, sprint when hasted or in wolf form), feet matched to the ground
                if (moved.Length() > MoveSpeed * dt * 0.3f)
                {
                    _blockedT = 0f;
                    ActiveModel.Locomote(MoveSpeed, 0.12f, "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop");
                    AnimCheck.Moving(ActiveModel, _animProbe, dt, true, "hero");
                }
                else if ((_blockedT += dt) > 0.2f) ActiveModel.Play(IdleAnim, 1f, 0.2f);      // pushing against a wall: stand, do not run on the spot
                else ActiveModel.Locomote(MoveSpeed, 0.12f, "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop");
            }
            _stepT -= dt * MoveSpeed / BaseSpeed;
            if (_stepT <= 0) { _stepT = 0.32f; Snd.At(World.FootstepSfx, GlobalPosition, 0.35f); }
        }
        else if (_act == Act.Free)
        {
            ActiveModel.Play(IdleAnim, 1f, 0.2f);
            AnimCheck.HeroStanding(ActiveModel, dt);
        }
    }

    private readonly AnimCheck.Probe _animProbe = new();
    private float _blockedT;
    /// <summary>Test harness: evade on the next frame, as if Space had been pressed.</summary>
    public void QueueEvade() => _pressEvade = true;

    private string IdleAnim => IdleAnimOf(Hero.Class, Form);

    /// <summary>The idle clip a class stands in; a shape-shifted form (form != 0) idles like a beast.</summary>
    public static string IdleAnimOf(ClassId c, int form = 0) => form != 0 ? "Zombie_Idle_Loop" : c switch
    {
        ClassId.Emberblade or ClassId.Wildsoul => "Sword_Idle", ClassId.Lampknight => "Idle_Shield_Loop",
        ClassId.Hunter => "Pistol_Idle_Loop", _ => "Idle_Loop"
    };

    public CharModel ActiveModel => Form != 0 && _forms[Form] != null ? _forms[Form] : Model;

    public void SetMove(Vector2 target, bool keepPending)
    {
        var grid = World.Grid;
        if (!keepPending) _pending = null;
        _path.Clear(); _pathI = 0;
        if (grid == null || grid.LineWalk(Pos2, target)) _path.Add(grid?.NearestWalk(target) ?? target);
        else _path.AddRange(grid.Path(Pos2, target));
        _hasMove = _path.Count > 0;
        _repath = 0.25f;
    }

    public void GoUse(Interactable obj) { _goUse = obj; _pending = null; SetMove(obj.Pos2, false); }
    public void Stop() { _hasMove = false; _goUse = null; _pending = null; }

    // ───────────────────────── casting ─────────────────────────

    public Cd CdOf(SkillDef d)
    {
        if (!Cds.TryGetValue(d.Id, out var cd)) Cds[d.Id] = cd = new Cd();
        cd.MaxCharges = Math.Max(1, d.Charges);
        cd.Max = d.Cooldown * Hero.CdrMul;
        if (cd.Charges > cd.MaxCharges) cd.Charges = cd.MaxCharges;
        return cd;
    }

    public float CostOf(SkillDef d)
    {
        if (d.Cost <= 0) return 0;
        if (FreeNext || Hero.BuffFlag(b => b.FreeCast)) return 0;
        return d.Cost * (1f - Hero.S[Stat.ResourceCost] / 100f);
    }

    /// <summary>Null when the skill can be cast right now, otherwise a short reason for the HUD.</summary>
    public string WhyCant(SkillDef d)
    {
        if (d == null) return "";
        var cd = CdOf(d);
        if (d.Cooldown > 0 && cd.Charges <= 0) return "冷卻中";
        if (!d.Channel() && Resource < CostOf(d) - 0.01f) return Names.Resource(Hero.Class) + "不足";
        if (d.Channel() && Resource < 4f) return Names.Resource(Hero.Class) + "不足";
        if (d.PipReq > 0 && Pips < d.PipReq && !Hero.BuffFlag(b => b.FreePips)) return "契印不足";
        return null;
    }

    private float _warnT;
    public bool TryCast(int slot, Vector2 point, Actor target, bool fresh)
    {
        string id = Hero.D.Bar[slot];
        var d = Hero.Eff(id);
        if (d == null || Hero.Rank(id) <= 0) return false;
        string why = WhyCant(d);
        if (why != null)
        {
            if (fresh && why.Length > 0 && Game.I.Time - _warnT > 0.8f) { _warnT = (float)Game.I.Time; Game.I.Hud?.Warn(why); Snd.Ui("ui_error"); }
            return false;
        }
        // melee skills walk into reach when an enemy was clicked
        bool melee = d.Kind == SkillKind.Melee || (d.Kind == SkillKind.Nova && d.Radius < 4.5f && d.Coef > 0);
        if (target != null && !IsInstanceValid(target)) target = null;
        if (melee && target != null && !Input.IsKeyPressed(Key.Shift))
        {
            float reach = d.Radius * 0.8f + target.Radius;
            if (Pos2.DistanceTo(target.Pos2) > reach) { _pending = (slot, target); SetMove(target.Pos2, true); return true; }
        }
        BeginCast(d, slot, point, target);
        return true;
    }

    private void BeginCast(SkillDef d, int slot, Vector2 point, Actor target)
    {
        // every Nth cast variants
        if (d.Nth > 0)
        {
            int n = _nth.GetValueOrDefault(d.Id) + 1;
            if (n >= d.Nth) { n = 0; var c = d.Clone(); d.NthMod?.Invoke(c); c.Flags.Add("nth"); d = c; }
            _nth[d.Id] = n;
        }
        if (Hero.HasUnique("u_owlbeak") && d.Id == "hu_shot")
        {
            int n = _nth.GetValueOrDefault("owl") + 1;
            if (n >= 4) { n = 0; d = d.Clone(); d.Pierce = -1; d.Flags.Add("force_crit"); }
            _nth["owl"] = n;
        }
        if (target != null && !IsInstanceValid(target)) target = null;
        _castDef = d; _castSlot = slot; _castPoint = point; _castTarget = target;
        Vector2 dir = (target != null ? target.Pos2 : point) - Pos2;
        if (dir.LengthSquared() > 0.01f) FaceDir(dir);
        if (d.Form != 0) SetForm(d.Form == 3 ? 0 : d.Form);
        if (Stealthed && d.Coef > 0) { _ambush = Hero.BuffFlag(b => b.Ambush); EndStealth(); }

        float asp = d.Cat is SkillCat.Basic or SkillCat.Core or SkillCat.Mastery ? Hero.AttackSpeedMul : 1f + (Hero.AttackSpeedMul - 1f) * 0.5f;
        if (Form == 2) asp *= 1.15f;
        if (d.Has("fast_above50") && Resource > 50) asp *= 1.25f;
        if (HasStacks("quick_blood")) asp *= 1f;
        asp *= Mathf.Lerp(1f, 0.6f, 1f - SlowMul);

        if (d.Kind == SkillKind.Channel)
        {
            _act = Act.Channel; _chanTick = 0; _chanTime = 0;
            Exec.ChannelStart(d);
            return;
        }
        _act = Act.Cast; _actT = 0; _fired = false;
        _contactT = d.Contact / asp; _lockT = Mathf.Max(d.Lock / asp, _contactT + 0.02f);
        string anim = d.Anim;
        if (d.AnimAlt != null) { int i = _nth.GetValueOrDefault(d.Id + "#a"); anim = d.AnimAlt[i % d.AnimAlt.Length]; _nth[d.Id + "#a"] = i + 1; }
        if (Form == 1 && d.Form == 0) anim = "Melee_Hook"; else if (Form == 2 && d.Form == 0) anim = "Zombie_Scratch";
        ActiveModel.PlayAttack(anim, d.AnimSpeed * asp, _contactT, 0.06f);        // the swing is lined up with the contact frame
        if (d.Sfx != null && d.Kind != SkillKind.Leap) Snd.At(d.Sfx, GlobalPosition, 0.8f);
        InCombat = Mathf.Max(InCombat, 4f);
    }

    private void TickCast(float dt)
    {
        _actT += dt;
        if (!_fired && _actT >= _contactT)
        {
            _fired = true;
            var d = _castDef;
            // pay
            float cost = CostOf(d);
            if (d.Cost > 0) { Resource = Mathf.Max(0, Resource - cost); if (FreeNext && cost == 0) FreeNext = false; }
            else if (d.Cost < 0) GainResource(-d.Cost);
            if (d.Cooldown > 0) { var cd = CdOf(d); if (cd.Charges == cd.MaxCharges) cd.Left = cd.Max; cd.Charges--; }
            float pipMul = 1f;
            int spent = 0;
            if (d.PipSpend && Pips > 0) { spent = Pips; pipMul = 1f + 0.3f * Pips; Pips = 0; }
            if (d.PipReq > 0 && !Hero.BuffFlag(b => b.FreePips)) Pips = Math.Max(0, Pips - d.PipReq);
            bool forceCrit = d.Has("force_crit") || (spent >= 3 && Hero.HasKey("kp_hu_finisher"));
            NoteElement(d.Element);
            Fired++; LastFired = d.Id;
            Log.Guard("skill", () => Exec.Fire(d, _castPoint, _castTarget, pipMul, forceCrit));
        }
        if (_actT >= _lockT && _act == Act.Cast) { _act = Act.Free; _castDef = null; }
    }

    private void TickChannel(float dt)
    {
        var d = _castDef;
        bool held = _castSlot >= 0 && Held(_castSlot);
        float costPerSec = CostOf(d);
        if (!held || Resource < costPerSec * dt || d == null) { EndChannel(); return; }
        Resource -= costPerSec * dt;
        _chanTime += dt; _chanTick += dt;
        InCombat = 4f;
        float asp = Hero.AttackSpeedMul;
        float tick = d.Tick / asp;
        Rotation = new Vector3(0, Rotation.Y + dt * 14f, 0);       // spin
        ActiveModel.Play(d.Anim, 0.6f, 0.1f);
        if (_chanTick >= tick) { _chanTick -= tick; ChanTicks++; LastFired = d.Id; Exec.ChannelTick(d, _chanTime); }
    }

    private void EndChannel()
    {
        if (_castDef != null) Exec.ChannelEnd(_castDef, _chanTime);
        Rotation = new Vector3(0, Facing, 0);
        _act = Act.Free; _castDef = null;
    }

    public void CancelAction()
    {
        if (_act == Act.Channel) EndChannel();
        _act = Act.Free; _castDef = null;
        var p = GlobalPosition; GlobalPosition = new Vector3(p.X, 0, p.Z);
    }

    private void NoteElement(Element e)
    {
        if (!Hero.HasKeystone("ks_resonance") || e == Element.Physical) return;
        if (e != LastElement) Hero.AddBuff(new BuffDef { Id = "resonance", Name = "元素共鳴", Stat = Stat.DmgAll, Value = 8, MaxStacks = 3 }, 6f);
        LastElement = e;
    }

    // ───────────────────────── dash / leap / evade ─────────────────────────

    /// <summary>Moves the hero along a line over <paramref name="dur"/> seconds (dash, leap with arc, evade).</summary>
    public void StartDash(Vector2 to, float dur, float arc, bool iframes, Action<Vector2, Vector2> step, Action end)
    {
        _dashFrom = Pos2; _dashTo = to; _dashDur = Mathf.Max(0.05f, dur); _dashArc = arc; _dashIframes = iframes; _dashStep = step; _dashEnd = end;
        _act = Act.Dash; _actT = 0;
        if (iframes) IFrames = Mathf.Max(IFrames, dur + 0.05f);
        _hasMove = false; _pending = null;
    }

    private void TickDash(float dt)
    {
        Vector2 prev = Pos2;
        _actT += dt;
        float t = Mathf.Clamp(_actT / _dashDur, 0f, 1f);
        float e = 1f - (1f - t) * (1f - t);
        Vector2 p = _dashFrom.Lerp(_dashTo, e);
        GlobalPosition = new Vector3(p.X, _dashArc * Mathf.Sin(t * Mathf.Pi), p.Y);
        _dashStep?.Invoke(prev, p);
        if (t >= 1f)
        {
            GlobalPosition = new Vector3(p.X, 0, p.Y);
            _act = Act.Free;
            var end = _dashEnd; _dashEnd = null; _dashStep = null;
            end?.Invoke();
        }
    }

    public void Teleport(Vector2 to)
    {
        GlobalPosition = new Vector3(to.X, 0, to.Y);
        _hasMove = false; _pending = null;
    }

    private void TryEvade()
    {
        if (EvadeCd > 0 || Rooted && !Unstoppable) return;
        if (_act == Act.Channel) EndChannel();
        Vector2 dir = Aim - Pos2;
        Vector2 wish = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W)) wish.Y -= 1; if (Input.IsKeyPressed(Key.S)) wish.Y += 1;
        if (Input.IsKeyPressed(Key.A)) wish.X -= 1; if (Input.IsKeyPressed(Key.D)) wish.X += 1;
        if (wish != Vector2.Zero) dir = wish;
        if (dir.LengthSquared() < 0.01f) dir = Forward;
        dir = dir.Normalized();
        Vector2 from = Pos2;
        Vector2 to = World.Grid?.ClampLine(Pos2, Pos2 + dir * 5.5f, false) ?? Pos2 + dir * 5.5f;
        EvadeCd = EvadeMax;
        FaceDir(dir);
        ClearCc();
        ActiveModel.Play("Roll", 1.9f, 0.05f, true);
        Snd.At("evade", GlobalPosition, 0.7f);
        Fx.Dust(GlobalPosition);
        StartDash(to, 0.32f, 0f, true, null, () => Exec.OnEvadeEnd(from, to));
        SkirmishLeft = 4f;
        if (Hero.HasAsp("frost_shell")) { AddBarrier(MaxLife * Hero.Asp("frost_shell") / 100f * BarrierMul, 4f); Fx.Ring(GlobalPosition, 1.5f, Palette.Element(Element.Frost), 0.4f); }
    }

    public float BarrierMul => 1f + Hero.S[Stat.BarrierPct] / 100f;

    private void UsePotion()
    {
        if (Potions <= 0 || Life >= MaxLife) { if (Potions <= 0) Game.I.Hud?.Warn("沒有藥水了"); return; }
        Potions--;
        Heal(MaxLife * 0.35f);
        Snd.At("potion", GlobalPosition);
        Fx.Heal(this);
        if (Hero.HasAsp("potion_surge"))
        {
            Hero.AddBuff(new BuffDef { Id = "potion_surge", Name = "靈藥奔流", Stat = Stat.MoveSpeed, Value = Hero.Asp("potion_surge"), Unstoppable = true }, 5f);
            ClearCc();
        }
    }

    public override void Heal(float amount)
    {
        base.Heal(amount * (1f + Hero.S[Stat.Healing] / 100f));
    }

    // ───────────────────────── forms / stealth ─────────────────────────

    public void SetForm(int form)
    {
        if (Hero.Class != ClassId.Wildsoul || form == Form) return;
        Form = form;
        for (int i = 0; i < 3; i++) if (_forms[i] != null) _forms[i].Visible = i == form;
        ShiftLeft = 4f;
        Fx.Burst(Chest, Element.Poison, 1.6f, 0.5f);
        Snd.At("shapeshift", GlobalPosition, 0.7f);
        ApplyFormScale();
    }

    public void ApplyFormScale()
    {
        float s = Hero.Buffs.Any(b => b.Def.Id.StartsWith("ws_primal")) ? 1.3f : 1f;
        foreach (var f in _forms) if (f != null) f.Scale = Vector3.One * s;
    }

    public void BeginStealth(float dur)
    {
        StealthLeft = dur;
        Model.SetTint(new Color(0.35f, 0.4f, 0.5f));
    }

    private void EndStealth()
    {
        StealthLeft = 0;
        Model.SetTint(Colors.White);
        Hero.RemoveBuffs(b => b.Def.Stealth && !b.Def.Ambush);
    }

    public bool ConsumeAmbush() { if (!_ambush) return false; _ambush = false; Hero.RemoveBuffs(b => b.Def.Stealth); return true; }

    // ───────────────────────── combat hooks ─────────────────────────

    public void GainResource(float amount)
    {
        Resource = Mathf.Min(Hero.MaxResource, Resource + amount * (1f + Hero.S[Stat.ResourceGen] / 100f));
    }

    public void GainPips(int n) { if (Hero.MaxPips > 0) Pips = Math.Min(Hero.MaxPips, Pips + n); }

    private bool HasStacks(string id) => Hero.Buffs.Any(b => b.Def.Id == id);

    /// <summary>Conditional multipliers from legendary powers, key passives and forms.</summary>
    public float DamageMult(Actor t, SkillDef s, bool minion)
    {
        var h = Hero;
        float m = 1f;
        if (h.HasAsp("executioner") && t.Life < t.MaxLife * 0.3f) m *= 1f + h.Asp("executioner") / 100f;
        if (h.HasAsp("overflow") && ResourceFrac > 0.8f) m *= 1f + h.Asp("overflow") / 100f;
        if (h.HasAsp("chill_touch") && (t.Has(StatusId.Chill) || t.Has(StatusId.Freeze))) m *= 1f + h.Asp("chill_touch") / 100f;
        if (s != null && s.Id == "eb_whirl" && h.HasAsp("cyclone_pull")) m *= 1f + h.Asp("cyclone_pull") / 100f;
        if (minion && h.HasAsp("legion")) m *= 1f + h.Asp("legion") / 100f;
        if (h.HasKeystone("ks_fury") && Resource > 50) m *= 1.15f;
        if (h.HasKey("kp_eb_edge") && s != null && s.Cat == SkillCat.Core && Resource > 75) m *= 1.3f;
        if (h.HasKey("kp_as_mastery") && (t.Has(StatusId.Burn) || t.Has(StatusId.Chill) || t.Has(StatusId.Freeze))) m *= 1.2f;
        if (h.HasKey("kp_hu_skirmish") && SkirmishLeft > 0) m *= 1.2f;
        if (h.HasKey("kp_sb_chorus")) m *= 1f + 0.04f * World.Minions.Count;
        if (h.HasKey("kp_sb_alone") && World.Minions.Count == 0) m *= 1.3f;
        if (h.HasKey("kp_ws_feral") && ShiftLeft > 0) m *= 1.2f;
        if (h.HasKey("kp_fb_flow") && !minion) m *= 1f + 0.03f * FlowStacks;
        if (h.HasKey("kp_lk_weight")) m *= 1f + 0.02f * Mathf.Floor(Resource / 10f);
        if (h.HasKey("kp_pb_full") && Pips >= h.MaxPips && h.MaxPips > 0) m *= 1.25f;
        return m;
    }

    /// <summary>Extra damage-reduction multiplier from conditional sources (1 = none).</summary>
    public float ExtraDr()
    {
        var h = Hero;
        float m = 1f;
        if (h.HasAsp("iron_will") && Life < MaxLife * 0.35f) m *= 1f - h.Asp("iron_will") / 100f;
        if (h.HasKeystone("ks_bulwark") && Life > MaxLife * 0.8f) m *= 0.88f;
        if (h.HasKey("kp_eb_stand")) m *= 1f - 0.03f * Mathf.Floor((1f - LifeFrac) * 10f);
        if (Form == 1) m *= 0.8f;
        return m;
    }

    private float _luckyIcd, _retortIcd;
    /// <summary>Everything that happens after one of the hero's hits lands.</summary>
    public void AfterHit(Actor t, SkillDef s, Hit hit, float dealt, Element el, bool minion)
    {
        var h = Hero;
        InCombat = 5f;
        if (s != null)
        {
            if (s.OnHit.Count > 0) Combat.ApplyStatuses(this, t, s.OnHit, el, minion);
            if (s.HealOnHit > 0) Heal(MaxLife * s.HealOnHit / 100f);
            if (s.GainOnHit > 0) GainResource(s.GainOnHit);
            if (s.BarrierOnHit > 0) AddBarrier(MaxLife * s.BarrierOnHit * BarrierMul, 5f);
            if (hit.Crit) { if (s.GainOnCrit > 0) GainResource(s.GainOnCrit); if (s.PipOnCrit > 0) GainPips(s.PipOnCrit); }
            if (s.RemnantChance > 0 && Rng.Chance(s.RemnantChance)) Exec.SpawnRemnant(t.Pos2);
            if (s.Cat == SkillCat.Basic && !minion)
            {
                if (h.HasAsp("quick_blood")) h.AddBuff(new BuffDef { Id = "quick_blood", Name = "沸血", Stat = Stat.AttackSpeed, Value = h.Asp("quick_blood"), MaxStacks = 5 }, 4f);
                if (h.HasKey("kp_fb_flow")) { FlowStacks = Math.Min(10, FlowStacks + 1); FlowLeft = 3f; }
            }
        }
        if (minion)
        {
            if (h.HasKey("kp_pb_blood")) Heal(dealt * 0.1f / (1f + h.S[Stat.Healing] / 100f));
            return;
        }
        float burn = h.BuffMax(b => b.Burn);
        if (burn > 0 && !hit.Dot) t.ApplyStatus(St.Burn(burn, 4), this, Combat.DotDps(this, burn, 4, Element.Fire), Element.Fire);
        float vd = h.BuffMax(b => b.VulnDur);
        if (vd > 0) t.ApplyStatus(St.Vuln(vd), this);
        if (h.HasAsp("chill_touch") && !hit.Dot) t.ApplyStatus(St.Chill(0.15f), this);
        if (hit.Crit)
        {
            if (h.HasAsp("vuln_mark")) t.ApplyStatus(St.Vuln(h.Asp("vuln_mark")), this);
            float cb = h.BuffMax(b => b.CritBurst);
            if (cb > 0) Exec.Burst(t.Pos2, 3f, cb, s != null && s.Id.StartsWith("ws") ? Element.Lightning : Element.Shadow, null, 0.2f);
            if (h.HasKey("kp_as_overload") && Rng.Chance(0.15f)) FreeNext = true;
        }
        if (h.S[Stat.LifeOnHit] > 0 && !hit.Dot) Heal(h.S[Stat.LifeOnHit]);
        float ls = h.BuffMax(b => b.LifeSteal);
        if (ls > 0) Heal(dealt * ls / 100f / (1f + h.S[Stat.Healing] / 100f));
        if (t.Has(StatusId.Stun) && h.HasKey("kp_fb_vajra") && _retortIcd <= 0) { AddBarrier(MaxLife * 0.05f * BarrierMul, 5f); }

        // lucky hit
        if (s != null && !hit.Dot && Rng.Chance(s.Lucky * (1f + h.S[Stat.LuckyHit] / 100f)))
        {
            if (h.HasAsp("storm_echo") && Rng.Chance(h.Asp("storm_echo") / 100f)) Exec.ChainFrom(t, 0.5f, 4, Element.Lightning, null);
            if (h.HasUnique("u_candle") && Life < MaxLife * 0.5f && Rng.Chance(0.4f)) { Heal(MaxLife * 0.06f); Fx.Heal(this); }
        }
    }

    private void OnKill(Monster m, Hit h)
    {
        if (m.Faction != Faction.Monster) return;
        var hero = Hero;
        if (hero.S[Stat.LifeOnKill] > 0) Heal(hero.S[Stat.LifeOnKill]);
        if (hero.HasAsp("soul_harvest")) GainResource(hero.Asp("soul_harvest"));
        if (hero.HasUnique("u_emberheart")) EmberPause = 3f;
        float gk = hero.BuffSum(b => b.GainOnKill);
        if (gk > 0) GainResource(gk);
        if (h.Minion && hero.HasUnique("u_reaper")) { Heal(MaxLife * 0.03f); GainResource(5); }
        if ((m.IsElite || m.IsBoss) && hero.HasKeystone("ks_cycle")) foreach (var cd in Cds.Values) cd.Left = Mathf.Max(0, cd.Left - 3f);
        Exec.OnKill(m, h);
    }

    /// <summary>A monster's melee blow landed on the hero: thorns and retaliation.</summary>
    public void OnStruckMelee(Monster m)
    {
        var h = Hero;
        if (h.S[Stat.Thorns] > 0)
            m.TakeHit(new Hit { Source = this, Amount = h.S[Stat.Thorns] * h.MainMul, El = Element.Physical, NoReact = true });
        if (h.HasKey("kp_lk_retort") && Rng.Chance(0.2f)) Exec.Burst(m.Pos2, 3f, 1.5f, Element.Fire, null);
    }

    protected override bool PreventDeath(Hit h)
    {
        if (Hero.BuffFlag(b => b.Id == "test_god"))
        {
            // harness --god: a death that is counted but not suffered, so damage intake stays honest (no hidden mitigation)
            GodSaves++;
            Life = MaxLife; Potions = Hero.D.PotionCap; IFrames = 1.5f; Dots.Clear();
            return true;
        }
        if (Hero.BuffFlag(b => b.Deathless)) { Life = 1; return true; }
        if (Hero.HasAsp("second_wind") && SecondWindCd <= 0)
        {
            SecondWindCd = 120f;
            Life = MaxLife * Hero.Asp("second_wind") / 100f;
            IFrames = 1.5f;
            Fx.Burst(Chest, Element.Fire, 4f, 1f);
            Game.I.Hud?.Toast("再起！", Palette.Ember);
            return true;
        }
        return false;
    }

    public override float TakeHit(Hit h)
    {
        if (Invulnerable && h.Source != this) return 0;
        float d = base.TakeHit(h);
        if (d > 0)
        {
            InCombat = 5f;
            if (!h.Dot)
            {
                Model.Flash(new Color(1f, 0.2f, 0.15f), 0.6f);
                if (d > MaxLife * 0.08f) { Fx.Shake(Mathf.Clamp(d / MaxLife * 2.5f, 0.15f, 0.6f), 0.25f); Snd.At("player_hurt", GlobalPosition, 0.8f); }
            }
        }
        return d;
    }

    protected override void OnBarrierEnd() => Exec.OnBarrierEnd();

    public override void Die(Hit h)
    {
        Log.Info("hero", $"died: killer={(h.Source as Monster)?.Def?.Id ?? "?"} amount={h.Amount:F0} level={Hero.D.Level} area={Hero.D.Area}");
        CancelAction();
        Hero.D.Deaths++;
        Hero.Buffs.RemoveAll(b => !b.Def.Aura);
        ActiveModel.Play("Death01", 1f, 0.1f, true);
        Snd.At("player_death", GlobalPosition);
        Died?.Invoke();
    }

    public void Revive(Vector2 at)
    {
        Dead = false; Life = MaxLife; Barrier = 0; Dots.Clear(); ClearCc();
        Resource = Names.IsBuilder(Hero.Class) ? 0 : Hero.MaxResource;
        Potions = Hero.D.PotionCap;
        Teleport(at);
        IFrames = 2f;
        _act = Act.Free;
        ActiveModel.Play("Idle_Loop", 1f, 0.1f, true);
        Hero.Recalc();
    }
}

internal static class SkillDefExt
{
    public static bool Channel(this SkillDef d) => d.Kind == SkillKind.Channel;
}
