using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// The hero in the world: mouse/keyboard control, movement, the cast state machine, resources, cooldowns, evade,
/// potions and every hook that gear powers and passives attach to. Skill behaviour itself lives in SkillExec.
/// Controls: LMB move / basic skill / interact, RMB core skill, 1-4 skills, Q potion, Space evade, Shift stand still,
/// WASD direct movement. Clicking an enemy out of reach runs up to it (A*) and strikes; ranged skills stop at their range
/// with a clear line of sight; holding the button keeps attacking it (see Order / TickChase).
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
    private float _repath;
    // attack order: an enemy was clicked out of reach → run up to it (A*, re-planned as it moves), then strike
    private int _chSlot = -1;
    private Actor _chTarget;
    private float _chRepath, _chStuck, _chBest, _chStart, _chT, _chLimit;
    private int _chTried;                // un-sticking steps tried since the hero last got anywhere
    private Vector2 _sideDir;
    private readonly List<Vector2I> _avoid = new();   // cells whose small props pinned the hero during this run-up
    private Vector2 _chGoal, _chLast;
    // the enemy the button that is held down keeps attacking after the first blow, even when the cursor slips off it
    private int _keepSlot = -1;
    private Actor _keepTarget;
    private Actor _unreachable;          // the last chase gave up on this one (cursor shows "cannot reach")
    private float _unreachableT;
    /// <summary>Chase statistics for the test harness: started, struck on arrival, cancelled (by reason).</summary>
    public int ChaseStarts, ChaseHits;
    public string LastChaseEnd = "";
    /// <summary>Test harness: whether the target was in sight (a clear shot) when the last run-up ended in a blow.</summary>
    public bool LastStrikeSight;
    /// <summary>Test harness: the run-up state in one line.</summary>
    public string ChaseDebug => $"target={(_chTarget as Monster)?.Def?.Id ?? "-"} tried={_chTried} limit={_chLimit:0} keep={(_keepTarget as Monster)?.Def?.Id ?? "-"} act={_act} move={_hasMove} path={_pathI}/{_path.Count} stuck={_chStuck:0.0} t={_chT:0.0} best={_chBest:0.0} unreach={(_unreachable as Monster)?.Def?.Id ?? "-"}";
    /// <summary>The enemy the hero is running at or attacking right now (null when none). For the HUD and cursor.</summary>
    public Actor AttackTarget => ValidTarget(_chTarget) ? _chTarget : ValidTarget(_keepTarget) && Held(_keepSlot) ? _keepTarget : null;
    /// <summary>What a left click would do right now (None while the UI has the mouse). Set in UpdateAim; read by the cursor.</summary>
    public enum HoverAct { None, Move, Attack, Talk, PickUp, Interact, Unreachable }
    public HoverAct HoverKind;
    // test harness: a simulated mouse (screen position + buttons) used instead of the real one while set
    public static Vector2? TestMouse;
    public static bool TestLeft, TestRight, TestShift;
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

    private bool Held(int slot) => slot >= 0 && (slot == ForceHold || InputEnabled && !Game.I.UiBlocksMouse && slot switch
    {
        0 => Input.IsMouseButtonPressed(MouseButton.Left) || TestMouse != null && TestLeft,
        1 => Input.IsMouseButtonPressed(MouseButton.Right) || TestMouse != null && TestRight,
        2 => Input.IsKeyPressed(Key.Key1), 3 => Input.IsKeyPressed(Key.Key2), 4 => Input.IsKeyPressed(Key.Key3), _ => Input.IsKeyPressed(Key.Key4)
    });

    /// <summary>Shift: attack in place (no running up), cast where the cursor is.</summary>
    private static bool ShiftHeld => Input.IsKeyPressed(Key.Shift) || TestMouse != null && TestShift;

    /// <summary>Test harness: a click of a mouse button (0 left, 1 right) at the simulated cursor, as if it had been pressed now.</summary>
    public void TestClick(int slot) { if (slot >= 0 && slot < 6) _press[slot] = true; }

    private static bool ValidTarget(Actor t) => t != null && IsInstanceValid(t) && !t.Dead && !t.Untargetable && t.IsInsideTree() && !(t is Monster m && m.Stealthed);

    private void UpdateAim()
    {
        var cam = GetViewport().GetCamera3D();
        if (cam == null) return;
        var mp = TestMouse ?? GetViewport().GetMousePosition();
        Vector3 o = cam.ProjectRayOrigin(mp), d = cam.ProjectRayNormal(mp);
        if (Mathf.Abs(d.Y) > 1e-4f) { float t = -o.Y / d.Y; if (t > 0) Aim = World.To2(o + d * t); }
        Hover = null; HoverObj = null;
        HoverKind = HoverAct.None;
        if (Game.I.UiBlocksMouse) return;
        // enemies are picked a little above the ground so tall monsters are easy to click
        float t2 = Mathf.Abs(d.Y) > 1e-4f ? (1.0f - o.Y) / d.Y : 0;
        Vector2 aimHigh = World.To2(o + d * t2);
        float best = 1e9f;
        foreach (var m in World.Monsters)
        {
            if (m.Dead || m.Untargetable || m.Stealthed) continue;
            float dist = Mathf.Min(m.Pos2.DistanceTo(Aim), m.Pos2.DistanceTo(aimHigh)) - m.Radius;
            if (dist < 1.1f && dist < best) { best = dist; Hover = m; }
        }
        if (Hover == null) HoverObj = Interactable.Pick(Aim, 1.6f);
        if (Hover != null) HoverKind = ReferenceEquals(Hover, _unreachable) ? HoverAct.Unreachable : HoverAct.Attack;
        else if (HoverObj != null) HoverKind = HoverObj.Kind switch { "npc" or "prisoner" => HoverAct.Talk, "loot" => HoverAct.PickUp, _ => HoverAct.Interact };
        else HoverKind = World.Grid != null && !World.Grid.WalkAt(Aim) ? HoverAct.Unreachable : HoverAct.Move;
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
        if (stunned) { Model.Play("Hit_Chest", 0.5f); EndChase("interrupted"); ClearPress(); return; }

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
        if (_unreachableT > 0f && (_unreachableT -= dt) <= 0f) _unreachable = null;
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
        bool shift = ShiftHeld;
        // skills: a fresh press, or a held button (the left one only over an enemy, or while it keeps attacking one)
        if (_act == Act.Free)
        {
            for (int slot = 5; slot >= 0; slot--)
            {
                bool press = _press[slot], held = !press && Held(slot);
                if (!press && !held) continue;
                Actor target = Hover;
                // holding the button after a blow keeps attacking that enemy, even when the cursor slips off it
                if (target == null && held && slot == _keepSlot && ValidTarget(_keepTarget)) target = _keepTarget;
                if (slot == 0 && !shift)
                {
                    if (press && Hover == null && HoverObj != null) { GoUse(HoverObj); break; }
                    if (target == null) continue;          // plain ground click or drag = move (below)
                }
                // held over the enemy this button is already running at: let the run-up go on
                if (held && slot == _chSlot && ReferenceEquals(target, _chTarget)) break;
                int r = Order(slot, Aim, target, press, shift);
                if (r == 1) { _hasMove = false; _goUse = null; return; }
                if (r == 2) break;
            }
        }
        // movement
        Vector2 wish = Vector2.Zero;
        if (InputEnabled)
        {
            if (Input.IsKeyPressed(Key.W)) wish.Y -= 1; if (Input.IsKeyPressed(Key.S)) wish.Y += 1;
            if (Input.IsKeyPressed(Key.A)) wish.X -= 1; if (Input.IsKeyPressed(Key.D)) wish.X += 1;
        }
        if (wish != Vector2.Zero) { _hasMove = false; _goUse = null; EndChase("wasd"); _keepTarget = null; }
        else if ((_press[0] || Held(0)) && Hover == null && HoverObj == null && !shift && !Game.I.UiBlocksMouse)
        {
            // a fresh click on the ground cancels the attack order; dragging the held button keeps attacking the same enemy
            bool attacking = !_press[0] && (_chSlot == 0 && _chTarget != null || _keepSlot == 0 && ValidTarget(_keepTarget));
            if (!attacking)
            {
                if (_chTarget != null) EndChase("ground click");
                _keepTarget = null;
                if (Aim.DistanceTo(Pos2) > 0.6f) SetMove(Aim, false);
            }
        }

        if (_act == Act.Free && _chTarget != null && TickChase(dt)) return;
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
            // a waypoint on the way counts as passed a little sooner than the last one (a cell centre may hold a small prop)
            while (_pathI < _path.Count && Pos2.DistanceTo(_path[_pathI]) < (_pathI < _path.Count - 1 ? 0.7f : 0.35f)) _pathI++;
            if (_pathI >= _path.Count) _hasMove = false;
            else vel = (_path[_pathI] - Pos2).Normalized() * MoveSpeed;
        }

        if (vel != Vector2.Zero)
        {
            var moved = Move(vel * dt);
            if (_act != Act.Channel) FaceDir(vel, 1f - Mathf.Exp(-18f * dt));
            if (moved.Length() < MoveSpeed * dt * 0.15f && _hasMove)
            {
                // pushed back by a prop standing on the waypoint itself: take the next one
                if (_pathI < _path.Count - 1 && Pos2.DistanceTo(_path[_pathI]) < 1.6f) _pathI++;
                else { _repath -= dt; if (_repath <= -0.3f) _hasMove = false; }
            }
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

    /// <summary>
    /// [gfx 0.3, item 5] The clip an evade plays and its rate. Everyone rolls, timed so the tumble is complete when the
    /// 0.32 s dash ends; the astromancer and the soulbinder, whose robes are a rigid bell, lunge forward instead
    /// (Shield_Dash: the body stays upright and leans into the step).
    /// </summary>
    public static (string clip, float rate) EvadeAnimOf(ClassId c) =>
        c is ClassId.Astromancer or ClassId.Soulbinder ? ("Shield_Dash", 1.6f) : ("Roll", 2.5f);

    public void SetMove(Vector2 target, bool keepPending)
    {
        var grid = World.Grid;
        if (!keepPending) EndChase("move");
        _path.Clear(); _pathI = 0;
        if (grid == null || grid.LineWalkWide(Pos2, grid.NearestWalk(target), Radius)) _path.Add(grid?.NearestWalk(target) ?? target);
        else _path.AddRange(grid.Path(Pos2, target, Radius));
        _hasMove = _path.Count > 0;
        _repath = 0.25f;
    }

    public void GoUse(Interactable obj) { EndChase("use"); _keepTarget = null; _goUse = obj; SetMove(obj.Pos2, false); }
    public void Stop() { EndChase("stop"); _keepTarget = null; _hasMove = false; _goUse = null; }

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
    private void WarnOnce(string why)
    {
        if (string.IsNullOrEmpty(why) || Game.I.Time - _warnT <= 0.8f) return;
        _warnT = (float)Game.I.Time; Game.I.Hud?.Warn(why); Snd.Ui("ui_error");
    }

    /// <summary>
    /// Casts the skill in a bar slot (used by the bot and the test suites). A melee skill aimed at an enemy out of reach
    /// runs up to it first (Shift: strike in place).
    /// </summary>
    public bool TryCast(int slot, Vector2 point, Actor target, bool fresh)
    {
        string id = Hero.D.Bar[slot];
        var d = Hero.Eff(id);
        if (d == null || Hero.Rank(id) <= 0) return false;
        string why = WhyCant(d);
        if (why != null) { if (fresh) WarnOnce(why); return false; }
        bool melee = d.Kind == SkillKind.Melee || (d.Kind == SkillKind.Nova && d.Radius < 4.5f && d.Coef > 0);
        if (target != null && !IsInstanceValid(target)) target = null;
        if (melee && target != null && !ShiftHeld && Pos2.DistanceTo(target.Pos2) > d.Radius * 0.8f + target.Radius)
        {
            StartChase(slot, target);
            return true;
        }
        EndChase(null);
        BeginCast(d, slot, point, target);
        return true;
    }

    /// <summary>
    /// A mouse/keyboard order: the skill in <paramref name="slot"/> at the cursor or at the enemy under it. A skill that
    /// needs its target in reach (melee, projectiles, waves, chains) runs up to an enemy out of reach or out of sight
    /// first; ground-targeted, movement and self skills are cast at once. Returns 0 refused, 1 cast begun, 2 running up.
    /// </summary>
    private int Order(int slot, Vector2 point, Actor target, bool fresh, bool shift)
    {
        string id = Hero.D.Bar[slot];
        var d = Hero.Eff(id);
        if (d == null || Hero.Rank(id) <= 0) return 0;
        string why = WhyCant(d);
        if (why != null) { if (fresh) WarnOnce(why); return 0; }
        if (!ValidTarget(target)) target = null;
        if (target != null && !shift && ReachOf(d, out _) > 0f && !InReach(d, target))
        {
            if (!fresh && ReferenceEquals(target, _unreachable)) return 0;   // given up on a moment ago: no retry every frame while held
            StartChase(slot, target);
            return _chTarget != null ? 2 : 0;
        }
        EndChase(null);
        if (target != null) { _keepSlot = slot; _keepTarget = target; }
        BeginCast(d, slot, point, target);
        return 1;
    }

    /// <summary>
    /// How close (metres, plus the target's radius) the hero must stand to use a skill on an enemy, and whether it needs
    /// a clear line of sight. 0 = used from anywhere (ground-targeted, dashes, leaps, blinks, buffs, summons, channels).
    /// </summary>
    public static float ReachOf(SkillDef d, out bool sight)
    {
        sight = false;
        switch (d.Kind)
        {
            case SkillKind.Melee: return d.Radius * 0.8f;
            case SkillKind.Nova: return d.Radius < 4.5f && d.Coef > 0 ? d.Radius * 0.8f : 0f;
            case SkillKind.Proj: sight = true; return (d.Range > 0 ? d.Range : 18f) * 0.85f;
            case SkillKind.Wave: sight = true; return (d.Range > 0 ? d.Range : 9f) * 0.85f;
            case SkillKind.Chain: sight = true; return (d.Range > 0 ? d.Range : 12f) * 0.9f;
            default: return 0f;
        }
    }

    /// <summary>True when the skill can be used on the target from where the hero stands.</summary>
    public bool InReach(SkillDef d, Actor t)
    {
        float r = ReachOf(d, out bool sight);
        if (r <= 0f) return true;
        if (Pos2.DistanceTo(t.Pos2) > r + t.Radius) return false;
        return !sight || World.Grid == null || World.Grid.LineShot(Pos2, t.Pos2);
    }

    private void StartChase(int slot, Actor t)
    {
        bool same = _chSlot == slot && ReferenceEquals(_chTarget, t);
        _goUse = null;
        if (same) return;
        _chSlot = slot; _chTarget = t;
        _avoid.Clear();
        _chBest = _chStart = Pos2.DistanceTo(t.Pos2); _chStuck = 0f; _chT = 0f; _chLast = Pos2; _chLimit = 12f; _chTried = 0;
        ChaseStarts++;
        if (!PlanChase(t)) { GiveUp(t, "no path"); return; }
        if (Log.Verbose) Log.Debug("hero", $"run-up to {(t as Monster)?.Def?.Id ?? t.Name} ({_chBest:0.0} m) for slot {slot}");
    }

    /// <summary>A* route to the target (straight when the way is clear). False when there is no way there.</summary>
    private bool PlanChase(Actor t, bool forceAStar = false)
    {
        _chRepath = 0.3f; _chGoal = t.Pos2;
        var grid = World.Grid;
        _path.Clear(); _pathI = 0;
        if (grid == null || !forceAStar && _avoid.Count == 0 && grid.LineWalkWide(Pos2, t.Pos2, Radius)) _path.Add(t.Pos2);
        else
        {
            var route = _avoid.Count > 0 ? grid.PathAvoiding(Pos2, t.Pos2, Radius, _avoid) : grid.Path(Pos2, t.Pos2, Radius);
            if (route.Count == 0 && _avoid.Count > 0) route = grid.Path(Pos2, t.Pos2, Radius);
            if (route.Count == 0) { _hasMove = false; return false; }
            _path.AddRange(route);
        }
        _hasMove = true; _repath = 0.25f;
        // time allowed for the whole route: a detour round a cliff may take a while, but not for ever
        float len = 0f; Vector2 cur = Pos2; foreach (var q in _path) { len += cur.DistanceTo(q); cur = q; }
        if (_chT <= 0f) _chLimit = Mathf.Max(12f, 4f + len / Mathf.Max(2f, MoveSpeed) * 1.6f);      // set once, from the first route
        return true;
    }

    /// <summary>One frame of a run-up: strike once in reach, re-plan as the target moves (at most ~3 times a second), give up when stuck.</summary>
    private bool TickChase(float dt)
    {
        var t = _chTarget;
        if (!ValidTarget(t)) { EndChase(t != null && IsInstanceValid(t) && t.Dead ? "target died" : "target gone"); _keepTarget = null; return false; }
        string id = Hero.D.Bar[_chSlot];
        var d = Hero.Eff(id);
        if (d == null || Hero.Rank(id) <= 0) { EndChase("skill gone"); return false; }
        if (InReach(d, t))
        {
            _hasMove = false;
            string why = WhyCant(d);
            if (why != null) { WarnOnce(why); EndChase("cannot cast: " + why); return false; }
            int slot = _chSlot;
            EndChase(null);
            LastStrikeSight = World.Grid == null || World.Grid.LineShot(Pos2, t.Pos2);
            ChaseHits++; LastChaseEnd = "struck";
            _keepSlot = slot; _keepTarget = t;
            BeginCast(d, slot, t.Pos2, t);
            return true;
        }
        float dist = Pos2.DistanceTo(t.Pos2);
        // progress: the hero must get somewhere (0.5 m every 1.5 s: not pinned against a wall, a pit edge or a crowd), and
        // a detour round a wall may lead away for a while but not for ever (the route time ×1.6 + 4 s, at least 12 s)
        _chT += dt;
        if (dist < _chBest) _chBest = dist;
        if (Pos2.DistanceTo(_chLast) > 0.5f) { _chLast = Pos2; _chStuck = 0f; _chTried = 0; }
        else if (!(Rooted && !Unstoppable))
        {
            _chStuck += dt;
            // pinned (a wall corner, a rock, a body in the way): a melee blow that reaches already lands from here;
            // otherwise take the cell-by-cell route, then a step aside, before giving up
            if (_chStuck > 0.3f && d.Kind == SkillKind.Melee && dist <= d.Radius + t.Radius - 0.1f)
            {
                string why = WhyCant(d);
                if (why == null)
                {
                    int slot = _chSlot;
                    EndChase(null);
                    LastStrikeSight = World.Grid == null || World.Grid.LineShot(Pos2, t.Pos2);
                    ChaseHits++; LastChaseEnd = "struck";
                    _keepSlot = slot; _keepTarget = t;
                    BeginCast(d, slot, t.Pos2, t);
                    return true;
                }
            }
            if (_chStuck > 0.35f && _chTried == 0) NoteProps();
            if (_chStuck > 0.35f && _chTried == 0) { _chTried = 1; if (!PlanChase(t, true)) { GiveUp(t, "no path"); return false; } }
            else if (_chStuck > 0.7f && _chTried == 1) { _chTried = 2; SideStep(t); }
            else if (_chStuck > 1.1f && _chTried == 2) { _chTried = 3; SideStep(t); }
            if (_chStuck > 1.5f) { GiveUp(t, "stuck"); return false; }
        }
        if (_chT > _chLimit && _chBest > _chStart - 0.5f) { GiveUp(t, "no progress"); return false; }
        _chRepath -= dt;
        if (_chRepath <= 0f && (!_hasMove || t.Pos2.DistanceTo(_chGoal) > 0.75f) && !PlanChase(t, _chTried > 0)) GiveUp(t, "no path");
        return false;
    }

    /// <summary>Pinned: the cells round the hero that hold a (small) prop are kept out of the next routes of this run-up.</summary>
    private void NoteProps()
    {
        var g = World.Grid;
        if (g == null) return;
        var c = g.CellOf(Pos2);
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var q = new Vector2I(c.X + dx, c.Y + dy);
                if (g.Walk(q.X, q.Y) && !g.Clear(q.X, q.Y) && !_avoid.Contains(q)) _avoid.Add(q);
            }
    }

    /// <summary>Un-sticking: a short step in the free direction that leads most toward the target, then the route again.</summary>
    private void SideStep(Actor t)
    {
        var grid = World.Grid;
        if (grid == null) return;
        // toward the next point of the route (not the target itself: on a detour that may be the wrong way)
        Vector2 aim = _pathI < _path.Count ? _path[_pathI] : t.Pos2;
        Vector2 toT = (aim - Pos2).Normalized(), best = Pos2; float bs = -1e9f;
        Vector2 bestDir = Vector2.Zero;
        for (int k = 0; k < 16; k++)
        {
            Vector2 dir = Vector2.Right.Rotated(k * Mathf.Tau / 16f);
            if (_chTried >= 3 && dir.Dot(_sideDir) > 0.85f) continue;     // the second try goes another way
            // walk it in small steps, as the body really would (props and walls push it about)
            Vector2 q = Pos2;
            for (int s2 = 0; s2 < 8; s2++) q = grid.ResolveCircle(q + dir * 0.2f, Radius, q);
            float moved = q.DistanceTo(Pos2);
            if (moved < 0.6f) continue;
            float s = moved + dir.Dot(toT) * 0.8f;
            if (s > bs) { bs = s; best = q; bestDir = dir; }
        }
        if (bs <= -1e8f) return;
        _sideDir = bestDir;
        _path.Clear(); _pathI = 0;
        _path.Add(best);
        var route = grid.Path(best, t.Pos2, Radius);
        if (route.Count > 0) _path.AddRange(route); else _path.Add(t.Pos2);
        _hasMove = true; _chRepath = 0.6f; _repath = 0.25f;
    }

    private void GiveUp(Actor t, string why)
    {
        EndChase(why);
        _keepTarget = null;
        _unreachable = t; _unreachableT = 2.5f;
        WarnOnce("無法抵達");
        var g = World.Grid;
        string around = "";
        if (g != null && why == "stuck")
        {
            var c = g.CellOf(Pos2);
            for (int dy = -1; dy <= 1; dy++) { for (int dx = -1; dx <= 1; dx++) around += g.Get(c.X + dx, c.Y + dy) + (g.Solid(c.X + dx, c.Y + dy) ? "s" : "") + (g.Clear(c.X + dx, c.Y + dy) ? "" : "o"); around += "/"; }
            around = $" at ({Pos2.X:0.00},{Pos2.Y:0.00}) cell={c} next={(_pathI < _path.Count ? _path[_pathI].ToString("0.0") : "-")} path={_pathI}/{_path.Count} cells[{around}]";
        }
        Log.Info("hero", $"run-up to {(t as Monster)?.Def?.Id ?? t?.Name ?? "?"} given up: {why}{around}");
    }

    /// <summary>Ends the run-up (null reason = silently, a new order replaces it). The hero stops where he is.</summary>
    private void EndChase(string why)
    {
        if (_chTarget == null) return;
        if (why != null) LastChaseEnd = why;
        _chTarget = null; _chSlot = -1;
        _hasMove = false;
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
        EndChase(null);
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
        _hasMove = false; EndChase(null);
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
        _hasMove = false; EndChase("teleport");
    }

    private void TryEvade()
    {
        if (EvadeCd > 0 || Rooted && !Unstoppable) return;
        EndChase("evade"); _keepTarget = null;
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
        // [gfx 0.3, item 5] The Roll clip is 1.47 s long and turns the body over between 0.2 s and 0.8 s of it. At rate 1.9 the
        // 0.32 s dash ended 0.61 s into the clip, with the hero upside down in mid-tumble, and the blend back to standing
        // swung the body like a plank (the "45° lean" seen on the robed astromancer). At 2.5 the tumble is over (0.8 s) as
        // the dash ends, so the hero comes out of it on their feet. The two heroes in long robes do not tumble at all (see
        // EvadeAnimOf): their skirt is one rigid bell, and a bell turning head over heels is the plank the eye saw.
        var (evadeClip, evadeRate) = Form != 0 ? ("Roll", 2.5f) : EvadeAnimOf(Hero.Class);
        ActiveModel.Play(evadeClip, evadeRate, 0.05f, true);
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
