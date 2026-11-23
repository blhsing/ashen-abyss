using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// The minion system shared by every summoner (tools/golems/PLAN.md §4) and the Soulbinder's book minions:
///   - persistence: persistent minions (no lifetime) follow the hero through area changes, town and save/load
///     (SaveData.Minions); the golem keeps its life and its re-form timer;
///   - follow: a leash (teleport past 25 m or after 3 s out of sight), "attack my target" (the hero's last struck enemy);
///   - the golem: one at a time, permanent, re-forms 20 s after dying (Golems.cs);
///   - warriors and mages refill by consuming remnants (殘魂) older than RemnantMinAge, away from the hero's aim (亡者名冊 upgrade 1);
///   - scaling: resistances and the attack-speed affix from the hero; golem life, armour and damage (Golems.cs);
///   - a cap on the minion count, and no shadows for distant or surplus skeletons.
/// One node under Game (it survives area changes); the HUD frame is HudMinions.
/// </summary>
public partial class MinionSystem : Node
{
    public static MinionSystem I { get; private set; }
    public const int MaxMinions = 14;
    public const float LeashRange = 25f, ReformTime = 20f, ReformFast = 8f;
    // counters for the test suite and the logs
    public static int Stuck, Shockwaves, Blocks, ThornHits, Commands, Reforms, Raised, Leashed, Restored, Capped, Redirected;

    public Monster Golem { get; private set; }
    public GolemCtl GolemCtl => Golem?.Ctl as GolemCtl;
    public bool GolemAlive => Golem != null && GodotObject.IsInstanceValid(Golem) && !Golem.Dead;
    /// <summary>Seconds before a dead golem re-forms (0 when alive or not wanted).</summary>
    public float ReformLeft => Save?.GolemDead == true ? Save.GolemReform : 0f;

    private float _slow, _corpseT, _summonGrace;
    private string _golemSig;                    // kind + forge uid the current golem was built for
    private Actor _heroTarget; private double _heroTargetT;
    private readonly Dictionary<ulong, float> _unseen = new();
    private bool _harnessDone;
    private int _raiseAlt;
    private static readonly List<Actor> Tmp = new();

    private static Hero H => Game.I?.Hero;
    private static Player P => World.P != null && GodotObject.IsInstanceValid(World.P) ? World.P : null;
    private static MinionSave Save { get { var h = H; if (h == null) return null; return h.D.Minions ??= new MinionSave(); } }

    /// <summary>Called once from Game._Ready.</summary>
    public static void Install(Game g)
    {
        if (I != null && GodotObject.IsInstanceValid(I)) return;
        var n = new MinionSystem { Name = "MinionSystem" };
        g.AddChild(n);
        I = n;
        g.AreaEntered += a => Log.Guard("minions", () => n.OnAreaEntered(a));
        World.MonsterKilled += (m, h) => Log.Guard("minions", () => n.OnKill(m, h));
        World.Damage += (a, h, amt) => n.CountDamage(a, h, amt);
        World.MonsterKilled += (m, h) => { if (m != null && m.IsBoss && m.Faction == Faction.Monster) Log.Guard("balance", () => n.LogBossBreakdown(m)); };
        World.Damage += (a, h, amt) => { if (h.Source != null && h.Source == World.P && !h.Minion && a is Monster m && m.Faction == Faction.Monster && !m.Dead) { n._heroTarget = a; n._heroTargetT = Game.I.Time; } };
        Hero.BookChanged += line => Log.Guard("minions", () => n.OnBookChanged(line));
        GolemItems.Ensure();
        Log.Guard("hud", () => g.Hud?.AddChild(new HudMinions()));
        Log.Info("minions", "minion system installed");
    }

    // ───────────────────────── frame ─────────────────────────

    public override void _Process(double delta)
    {
        var g = Game.I;
        if (g == null || g.State != Game.GameState.Play || g.Paused || H == null || P == null) return;
        float dt = (float)delta;
        try { Tick(dt); }
        catch (Exception e) { if (_errors++ < 10) Log.Error("minions", "minion system update failed", e); }
    }
    private int _errors;

    private void Tick(float dt)
    {
        var h = H; var p = P;
        if (_summonGrace > 0f) _summonGrace -= dt;
        TickGolem(dt, h, p);
        foreach (var m in World.Minions.ToArray())
            if (m != null && GodotObject.IsInstanceValid(m) && !m.Dead && m.Ctl != null) m.Ctl.Tick(m, dt);

        _slow -= dt;
        if (_slow <= 0f)
        {
            _slow = 0.5f;
            Leash(p, 0.5f);
            Refresh(h, p);
            Snapshot(h);
        }
        _corpseT -= dt;
        if (_corpseT <= 0f)
        {
            _corpseT = 1.2f;
            if (!(Game.I.Level?.IsTown ?? true)) RefillFromRemnant(h, p);
        }
        if (_heroTarget != null && (!GodotObject.IsInstanceValid(_heroTarget) || _heroTarget.Dead || Game.I.Time - _heroTargetT > 6.0)) _heroTarget = null;
    }

    // ───────────────────────── the golem ─────────────────────────

    /// <summary>The golem kind the hero should have now (null: none — not a Soulbinder, book locked, line sacrificed).</summary>
    public static string WantedGolem(Hero h)
    {
        if (h == null || h.Class != ClassId.Soulbinder || !h.BookUnlocked) return null;
        return h.BookKindId(ClassBook.Golem);
    }

    private string Signature(Hero h, string kind) => kind == null ? null : kind + "|" + (kind == "g_iron" ? h.D.GolemForge?.Uid ?? "fists" : "");

    private void TickGolem(float dt, Hero h, Player p)
    {
        var s = Save;
        string want = WantedGolem(h);
        bool has = Golem != null && GodotObject.IsInstanceValid(Golem);
        if (has && Golem.Dead) has = false;
        if (want == null)
        {
            if (has) Dismiss("golem not wanted");
            s.GolemDead = false; s.GolemReform = 0f;
            return;
        }
        string sig = Signature(h, want);
        if (has && sig != _golemSig) { Dismiss($"golem changed {_golemSig} → {sig}"); has = false; s.GolemDead = false; s.GolemLife = 1f; }
        if (has) return;
        if (s.GolemDead)
        {
            s.GolemReform -= dt;
            if (s.GolemReform > 0f) return;
            s.GolemDead = false; s.GolemLife = 1f;
            Reforms++;
            if (SpawnGolem(h, p, want, 1f, "re-formed") == null) _summonGrace = 5f;
            return;
        }
        if (_summonGrace > 0f) return;
        if (SpawnGolem(h, p, want, Mathf.Clamp(s.GolemLife, 0.05f, 1f), Golem == null ? "formed" : "restored") == null) _summonGrace = 5f;      // no retry storm
    }

    /// <summary>Removes the golem without a death (kind changed, line sacrificed, the test suite).</summary>
    public void Dismiss(string why)
    {
        if (Golem != null && GodotObject.IsInstanceValid(Golem))
        {
            World.Minions.Remove(Golem);
            Fx.Ash(Golem);
            Golem.QueueFree();
            Log.Info("golem", $"dismissed: {why}");
        }
        Golem = null; _golemSig = null;
    }

    public Monster SpawnGolem(Hero h, Player p, string kind, float lifeFrac, string why)
    {
        var art = MinionArt.OfKind(kind);
        var def = art == null ? null : MinionDefs.Get(art.Def);
        if (def == null) { Log.Warn("golem", $"no definition for golem kind {kind}"); return null; }
        GolemStats forge = null;
        if (kind == "g_iron" && h.D.GolemForge != null)
        {
            forge = GolemForge.Inherit(h.D.GolemForge, h);
            def = GolemForge.StyledDef(def, forge.Style);
        }
        var grid = World.Grid;
        Vector2 pos = grid?.RandomWalkNear(p.Pos2, 1.5f, 3.2f) ?? p.Pos2 + new Vector2(2f, 0f);
        int level = forge != null ? Math.Min(forge.Level, h.D.Level) : h.D.Level;
        var m = Monster.Spawn(def, Math.Max(1, level), MonTier.Minion, pos, Faction.Player);
        m.SummonSkill = "golem";
        m.MinionSkill = h.Eff("sb_golem_cmd") ?? SkillDb.Get("sb_golem_cmd");
        m.MinionCoef = kind switch { "g_bone" => 0.75f, "g_blood" => 0.85f, _ => 1.1f } * (1f + 0.02f * Math.Max(0, h.Rank("sb_golem_cmd") - 1));
        m.Ctl = new GolemCtl { M = m, Kind = kind, Forge = forge };
        m.Taunter = kind == "g_bone";
        ScaleGolem(m, h, p, lifeFrac);
        m.Model?.SetRim(art.Rim);
        if (forge != null) Log.Guard("golem", () => GolemForge.Fuse(m, h.D.GolemForge, art));
        Golem = m; _golemSig = Signature(h, kind);
        Fx.Burst(World.To3(pos, 0.3f), kind == "g_blood" ? Element.Shadow : Element.Physical, 2.4f, 0.8f);
        Fx.Ring(World.To3(pos, 0.15f), 2.6f, art.Rim * 1.4f, 0.8f, Sigil.Arcane);
        Snd.At("summon_raise", m.GlobalPosition, 0.8f);
        Log.Info("golem", $"{why}: {def.Name} ({kind}{(forge != null ? $", forged from {h.D.GolemForge.Name} [{forge.Style}]" : "")}) life={m.Life:0}/{m.MaxLife:0} level={m.Level} model={def.Model} radius={m.Radius:0.00} height={m.HeightM:0.0}");
        return m;
    }

    /// <summary>Golem life from the hero's life, the golem-life affix and the forged item (life fraction kept).</summary>
    private static void ScaleGolem(Monster m, Hero h, Player p, float frac)
    {
        var g = m.Ctl as GolemCtl;
        float k = 0.9f * m.Def.Hp * (1f + h.S[Stat.GolemLife] / 100f) * (1f + (g?.Forge?.LifePct ?? 0f) / 100f) * (h.HasUnique("u_cairn") ? 1.3f : 1f);
        float max = Mathf.Max(50f, p.MaxLife * k);
        frac = Mathf.Clamp(frac, 0.01f, 1f);
        m.MaxLife = Mathf.Round(max); m.Life = m.MaxLife * frac;
        float spd = 1f + h.S[Stat.MinionAtkSpeed] / 100f + (g?.Forge?.AtkSpdPct ?? 0f) / 100f;
        m.AtkSpeedMul = spd;
    }

    /// <summary>Re-forms a dead (or missing) golem at once (魔像號令 when the golem is down). False when there is no golem to have.</summary>
    public bool ReformNow(string why)
    {
        var h = H; var p = P;
        string want = WantedGolem(h);
        if (want == null || p == null) return false;
        if (GolemAlive) return false;
        var s = Save;
        s.GolemDead = false; s.GolemReform = 0f; s.GolemLife = 1f;
        Reforms++;
        return SpawnGolem(h, p, want, 1f, why) != null;
    }

    public static void OnMinionDied(Monster m, Hit h)
    {
        var sys = I;
        if (m.Ctl != null) Log.Guard("golem", () => m.Ctl.OnDeath(m, h));
        if (sys == null || m != sys.Golem) return;
        var s = Save;
        if (s == null) return;
        var hero = H;
        s.GolemDead = true;
        s.GolemReform = hero != null && hero.HasBookUp("g_iron", 1) && (m.Ctl as GolemCtl)?.Kind == "g_iron" ? ReformFast : ReformTime;
        s.GolemLife = 0f;
        sys.Golem = null; sys._golemSig = null;
        Log.Info("golem", $"{m.Def?.Name} fell (killer {(h.Source as Monster)?.Def?.Id ?? "?"}); re-forms in {s.GolemReform:0} s");
        Game.I?.Hud?.Toast($"{m.Def?.Name}崩解了，{s.GolemReform:0} 秒後重新塑成。", new Color(0.8f, 0.75f, 0.7f));
    }

    // ───────────────────────── the command ─────────────────────────

    /// <summary>sb_golem_cmd (SkillExec.FireCommand): the golem's command, its echo by the skeletons, or an immediate re-form.</summary>
    public void Command(SkillDef d, Vector2 aim, float coef, float mul)
    {
        var h = H; var p = P;
        if (h == null || p == null) return;
        if (!GolemAlive)
        {
            if (ReformNow("re-formed by 魔像號令")) { Commands++; Fx.Telegraph(Golem.Pos2, 3f, 0.4f, new Color(0.8f, 0.85f, 0.75f), true, Sigil.Arcane); return; }
            // no golem to command (line sacrificed): the command is a weak burst of bone at the cursor
            p.Exec.Burst(aim, 3f, coef * 0.5f, Element.Physical, d, 0.2f, mul);
            Fx.Telegraph(aim, 3f, 0.2f, new Color(0.85f, 0.85f, 0.8f), true, Sigil.Arcane);
            return;
        }
        var g = GolemCtl;
        g.Command(Golem, d, aim, coef, mul);
        if (d.Has("cmd_echo"))
            foreach (var m in World.Minions.ToArray())
                if (m != Golem && m.Ctl is SkeletonCtl && !m.Dead && m.Pos2.DistanceTo(p.Pos2) < 20f) g.Echo(m, d, coef);
        float horn = h.Asp("sb_war_horn");
        if (horn > 0f)
        {
            foreach (var m in World.Minions) if (m != Golem && !m.Dead) m.Enrage(6f);
            h.AddBuff(new BuffDef { Id = "sb_war_horn_b", Name = "號角", Stat = Stat.MinionDmg, Value = horn }, 6f);
        }
    }

    // ───────────────────────── warriors and mages (亡者名冊) ─────────────────────────

    /// <summary>True when the book decides what sb_raise raises (Soulbinder at the book's level).</summary>
    public static bool BookRaises(Hero h) => h != null && h.Class == ClassId.Soulbinder && h.BookUnlocked;

    public static int WarriorCap(Hero h) => h.LineSacrificed(ClassBook.Warriors) ? 0 : Math.Max(1, h.Eff("sb_raise")?.SummonMax ?? 4);
    public static int MageCap(Hero h) => h.LineSacrificed(ClassBook.Mages) ? 0 : 2 + (h.Rank("sb_raise") >= 3 ? 1 : 0) + (h.D.Spec == "sb_legion" ? 1 : 0) + (h.HasAsp("legion") ? 1 : 0);
    public static int Count(string tag) { int n = 0; foreach (var m in World.Minions) if (!m.Dead && m.SummonSkill == tag) n++; return n; }
    public const string MageTag = "sb_raise#mage";

    /// <summary>sb_raise cast (SkillExec.FireSummon): raises a warrior or a mage at once, or (both lines full) heals and enrages them all.</summary>
    public bool RaiseCast(SkillExec ex, SkillDef d, Vector2 aim, float coef)
    {
        var h = H;
        if (!BookRaises(h)) return false;
        int w = Count("sb_raise"), m = Count(MageTag), wc = WarriorCap(h), mc = MageCap(h);
        bool wantMage = (_raiseAlt++ % 2 == 1 && m < mc) || w >= wc;
        if ((!wantMage && w < wc) || m < mc)
        {
            // the nearest remnant within reach of the cast is consumed and the minion rises there; none: it rises from nothing
            Vector2 at = aim;
            Remnant best = null; float bd = Mathf.Max(4f, d.Range);
            foreach (var r in World.Remnants) { float dd = r.Pos.DistanceTo(aim); if (dd < bd) { bd = dd; best = r; } }
            if (best != null)
            {
                World.Remnants.Remove(best);
                if (GodotObject.IsInstanceValid(best.Fx)) best.Fx.QueueFree();
                at = best.Pos; RemnantsUsed++;
                Fx.Burst(World.To3(at, 0.4f), Element.Shadow, 1.2f, 0.5f);
            }
            RaiseOne(ex, h, d, at, coef, !(!wantMage && w < wc));
            return true;
        }
        foreach (var x in World.Minions) { x.Heal(x.MaxLife); Fx.Heal(x); }
        h.AddBuff(new BuffDef { Id = "minion_frenzy", Name = "亡者之怒", Stat = Stat.MinionDmg, Value = 30 }, 6f);
        foreach (var x in World.Minions) if (x.SummonSkill is "sb_raise" or MageTag) x.Enrage(6f);
        return true;
    }

    // ── remnants (殘魂): the one shared leftover of the Soulbinder's kills; 魂爆 detonates them, the army consumes them ──
    public const float RemnantMinAge = 3.5f, RemnantAimGuard = 4f, RemnantReach = 18f;
    private const float RemnantLife = 12f;           // Remnant.Left starts here (World.cs)

    /// <summary>Remnants the army may take: older than RemnantMinAge, away from the hero's aim (kept for 魂爆), near the hero.</summary>
    public static Remnant FreeRemnant(Player p)
    {
        Remnant best = null; float bd = RemnantReach;
        foreach (var r in World.Remnants)
        {
            if (RemnantLife - r.Left < RemnantMinAge || r.Pos.DistanceTo(p.Aim) < RemnantAimGuard) continue;
            float d = r.Pos.DistanceTo(p.Pos2);
            if (d < bd) { bd = d; best = r; }
        }
        return best;
    }

    /// <summary>One warrior or mage below its cap rises from a free remnant (a full or sacrificed army takes nothing).</summary>
    private void RefillFromRemnant(Hero h, Player p)
    {
        if (!BookRaises(h) || h.BaseRank("sb_raise") <= 0 || World.Minions.Count >= MaxMinions) return;
        int w = Count("sb_raise"), m = Count(MageTag), wc = WarriorCap(h), mc = MageCap(h);
        if (w >= wc && m >= mc) return;
        var r = FreeRemnant(p);
        if (r == null) return;
        ConsumeRemnant(r, p.Pos2, to => { if (RaiseFree(to, null)) RemnantsUsed++; });
    }

    /// <summary>Takes a remnant off the ground; its wisp flies to <paramref name="to"/> (the hero's side), where <paramref name="arrive"/> runs.</summary>
    public static void ConsumeRemnant(Remnant r, Vector2 to, Action<Vector2> arrive)
    {
        World.Remnants.Remove(r);
        var grid = World.Grid;
        Vector2 spot = grid?.RandomWalkNear(to, 1.2f, 2.8f) ?? to;
        var fx = r.Fx;
        if (fx != null && GodotObject.IsInstanceValid(fx) && fx.IsInsideTree())
        {
            var tw = fx.CreateTween();
            tw.TweenProperty(fx, "global_position", World.To3(spot, fx.GlobalPosition.Y), 0.45f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tw.TweenCallback(Callable.From(() => { Log.Guard("minions", () => arrive(spot)); if (GodotObject.IsInstanceValid(fx)) fx.QueueFree(); }));
        }
        else Log.Guard("minions", () => arrive(spot));
    }
    public static int RemnantsUsed;

    /// <summary>A free raise from a remnant (or a 血穫 kill). <paramref name="mage"/>: null = whichever line has room (warriors first).</summary>
    public bool RaiseFree(Vector2 at, bool? mage)
    {
        var h = H; var p = P;
        if (p == null || !BookRaises(h) || h.BaseRank("sb_raise") <= 0) return false;
        if (World.Minions.Count >= MaxMinions) return false;
        var d = h.Eff("sb_raise");
        int w = Count("sb_raise"), m = Count(MageTag), wc = WarriorCap(h), mc = MageCap(h);
        bool asMage = mage ?? (w >= wc);
        if (asMage && m >= mc) asMage = false;
        if (!asMage && w >= wc) { if (m >= mc) return false; asMage = true; }
        var mon = RaiseOne(p.Exec, h, d, at, p.Exec.CoefOf(d), asMage);
        if (mon != null) Raised++;
        return mon != null;
    }

    private Monster RaiseOne(SkillExec ex, Hero h, SkillDef d, Vector2 near, float coef, bool mage)
    {
        string line = mage ? ClassBook.Mages : ClassBook.Warriors;
        var kind = h.BookKind(line);
        if (kind == null) return null;
        string def = MinionArt.OfKind(kind.Id)?.Def ?? (mage ? "minion_thrower" : "minion_skeleton");
        if (!mage && d.Has("thrower_alt") && (++_throwAlt % 2 == 0)) def = "minion_thrower";
        return ex.Raise(def, d, near, coef * (mage ? 0.8f : 1f), mage ? MageTag : null);
    }
    private int _throwAlt;

    /// <summary>Every minion SkillExec spawns comes through here: book kinds get their behaviour and look, the count cap holds.</summary>
    public static void OnSpawned(Monster m, SkillDef d)
    {
        var h = H;
        if (m == null || h == null) return;
        var art = MinionArt.OfDef(m.Def.Id);
        if (art != null)
        {
            m.Ctl = new SkeletonCtl { M = m, Kind = art.Kind };
            m.Model?.SetRim(art.Rim);
            if (art.Kind == "w_guard") m.Taunter = true;
            if (art.Kind == "w_raider") { m.SpeedMul = 1.3f; m.AtkSpeedMul = 1.3f; }
            if (art.Kind == "w_reaper") { m.AtkSpeedMul = 0.8f; m.Cleave = true; }
        }
        m.AtkSpeedMul *= 1f + h.S[Stat.MinionAtkSpeed] / 100f;
        if (m.SummonSkill is "sb_raise" or MageTag) BaseCoef[m.GetInstanceId()] = m.MinionCoef;
        // the cap: the oldest skeleton (never the golem) crumbles
        if (World.Minions.Count > MaxMinions)
        {
            var old = World.Minions.FirstOrDefault(x => x != m && !x.IsGolem && !x.Dead);
            if (old != null) { Capped++; World.Minions.Remove(old); Fx.Ash(old); old.QueueFree(); }
        }
    }

    // ───────────────────────── combat hooks ─────────────────────────

    /// <summary>The hero's last struck enemy, for minions near enough ("attack my target"); null in town or when none.</summary>
    public static Monster TargetFor(Monster m)
    {
        var s = I; var p = P;
        if (s == null || p == null || s._heroTarget is not Monster t) return null;
        if (t.Dead || !GodotObject.IsInstanceValid(t) || t.Untargetable) return null;
        if (t.Pos2.DistanceTo(p.Pos2) > 16f || t.Pos2.DistanceTo(m.Pos2) > 22f) return null;
        return t;
    }

    /// <summary>Damage a player minion takes: resistances follow the hero's, then the kind's own rules.</summary>
    public static float Incoming(Monster m, Hit h)
    {
        var hero = H;
        float a = h.Amount;
        if (hero != null && !(m.Ctl is GolemCtl)) a *= 1f - Mathf.Clamp(hero.Res(h.El), 0f, 70f) / 200f;
        if (m.Ctl != null) a = m.Ctl.Incoming(m, h, a);
        return a;
    }

    /// <summary>
    /// Damage to the hero shared with the golem (Player.TakeHit): 血魔像 takes 15 % (25 % with 血魂), 魔像之心 20 %,
    /// 骨甲共享 takes 10 % off. Returns what the hero still takes.
    /// </summary>
    public static float RedirectHero(Player p, Hit h)
    {
        var s = I; var hero = p?.Hero;
        if (s == null || hero == null || h.Amount <= 0f || h.Source == p || !s.GolemAlive) return h.Amount;
        var g = s.GolemCtl;
        float share = 0f, cut = 0f;
        if (g.Kind == "g_blood") share += hero.D.Spec == "sb_blood" ? 0.25f : 0.15f;
        if (hero.HasKey("kp_sb_golemheart")) share += 0.2f;
        if (g.Kind == "g_bone" && hero.HasBookUp("g_bone", 1)) cut = 0.1f;
        if (share <= 0f && cut <= 0f) return h.Amount;
        float give = h.Amount * share;
        if (give > 0f)
        {
            s.Golem.TakeHit(new Hit { Source = h.Source, Amount = give, El = h.El, Dot = true, NoReact = true });
            Redirected++;
        }
        return (h.Amount - give) * (1f - cut);
    }

    private void OnKill(Monster m, Hit h)
    {
        var p = P;
        if (p == null || m == null || m.Faction != Faction.Monster) return;
        if (m.Pos2.DistanceTo(p.Pos2) > 18f) return;
        // ember_burst carried by a forged golem: its kills burst too
        var g = GolemCtl;
        if (g != null && h.Source == Golem && g.Forge?.Power == "ember_burst" && !g.Forge.PowerUnusable)
            p.Exec.Burst(m.Pos2, 3.2f, g.Forge.PowerValue / 100f, Element.Fire, null, 0.1f);
        if (g != null && h.Source == Golem && g.Forge?.Power == "soul_harvest" && !g.Forge.PowerUnusable) p.GainResource(g.Forge.PowerValue);
        if (g != null && h.Source == Golem && g.Forge?.Power == "u_snuffer" && Rng.Chance(0.15f))
        {
            World.InCircle(Faction.Player, m.Pos2, 5f, Tmp);
            foreach (var t in Tmp.ToArray()) t.ApplyStatus(St.Freeze(2), p);
        }
        if (g != null && h.Source == Golem && g.Forge?.Power == "u_reaper") { p.Heal(p.MaxLife * 0.03f); p.GainResource(5); }
    }

    // ───────────────────────── follow, scaling, persistence ─────────────────────────

    private void Leash(Player p, float dt)
    {
        var grid = World.Grid;
        foreach (var m in World.Minions.ToArray())
        {
            if (m == null || !GodotObject.IsInstanceValid(m) || m.Dead || m.Attacking) continue;
            float d = m.Pos2.DistanceTo(p.Pos2);
            ulong id = m.GetInstanceId();
            bool seen = d < 8f || grid == null || grid.LineShot(m.Pos2, p.Pos2);
            float unseen = seen ? 0f : _unseen.GetValueOrDefault(id) + dt;
            _unseen[id] = unseen;
            if (d > LeashRange || unseen > 3f)
            {
                m.Pos2 = grid?.RandomWalkNear(p.Pos2, 1.5f, 3.5f) ?? p.Pos2;
                m.SetTarget(null);
                _unseen[id] = 0f;
                Leashed++;
                Fx.Teleport(m.Chest, Element.Shadow);
            }
        }
        if (_unseen.Count > 64) _unseen.Clear();
    }

    private void Refresh(Hero h, Player p)
    {
        // a golem follows the hero's life and the golem affixes as they change (its life fraction is kept)
        if (GolemAlive)
        {
            float frac = Golem.Life / Mathf.Max(1f, Golem.MaxLife);
            ScaleGolem(Golem, h, p, frac);
            Golem.Life = Mathf.Min(Golem.MaxLife, Golem.MaxLife * frac);
        }
        // army size: each skeleton hits a little softer the more of them there are (balance 2026-11-23)
        int army = 0;
        foreach (var m in World.Minions) if (!m.Dead && m.SummonSkill is "sb_raise" or MageTag) army++;
        float per = ArmyFactor(army);
        foreach (var m in World.Minions)
            if (!m.Dead && BaseCoef.TryGetValue(m.GetInstanceId(), out var bc)) m.MinionCoef = bc * per * SoulMinionMul;
        if (BaseCoef.Count > 64) { var live = World.Minions.Select(x => x.GetInstanceId()).ToHashSet(); foreach (var k in BaseCoef.Keys.ToList()) if (!live.Contains(k)) BaseCoef.Remove(k); }
        // cheaper far skeletons: no shadows beyond the nearest six or past 14 m
        var near = World.Minions.Where(m => !m.Dead && !m.IsGolem && m.Model != null).OrderBy(m => m.Pos2.DistanceSquaredTo(p.Pos2)).ToList();
        for (int i = 0; i < near.Count; i++)
        {
            bool shadow = i < 6 && near[i].Pos2.DistanceTo(p.Pos2) < 14f;
            SetShadows(near[i].Model, shadow);
        }
    }

    private static readonly Dictionary<ulong, bool> ShadowState = new();
    private static void SetShadows(CharModel model, bool on)
    {
        ulong id = model.GetInstanceId();
        if (ShadowState.TryGetValue(id, out var was) && was == on) return;
        ShadowState[id] = on;
        if (ShadowState.Count > 200) ShadowState.Clear();
        foreach (var mi in model.FindChildren("*", "MeshInstance3D", true, false))
            if (mi is MeshInstance3D m3) m3.CastShadow = on ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
    }

    /// <summary>Writes the persistent minions into the save (every half second of play).</summary>
    private void Snapshot(Hero h)
    {
        var s = Save;
        if (s == null || _restoring) return;
        s.List.Clear();
        foreach (var m in World.Minions)
        {
            if (m == null || !GodotObject.IsInstanceValid(m) || m.Dead || m.LifeTime > 0f || m.SummonSkill == null || m.IsGolem) continue;
            s.List.Add(new MinionEntry { Def = m.Def.Id, Skill = m.SummonSkill, Life = Mathf.Clamp(m.Life / Mathf.Max(1f, m.MaxLife), 0.05f, 1f) });
        }
        if (GolemAlive) { s.GolemLife = Mathf.Clamp(Golem.Life / Mathf.Max(1f, Golem.MaxLife), 0.05f, 1f); s.GolemDead = false; }
    }

    private bool _restoring;

    /// <summary>A new area (or the town, or a loaded save): the persistent minions are raised again at the hero's side.</summary>
    private void OnAreaEntered(string area)
    {
        var h = H; var p = P;
        if (h == null || p == null) return;
        if (!_harnessDone) { _harnessDone = true; ApplyHarness(h); }
        h.EnsureBook();
        ResetBreakdown();
        Golem = null; _golemSig = null; _unseen.Clear(); _heroTarget = null;
        var s = Save;
        _restoring = true;
        try
        {
            int n = 0;
            foreach (var e in s.List.ToList())
            {
                string skillId = e.Skill?.Split('#')[0];
                var d = h.Eff(skillId);
                if (d == null || h.BaseRank(skillId) <= 0) continue;
                if (e.Skill == "sb_raise" && h.Class == ClassId.Soulbinder && BookRaises(h) && (h.LineSacrificed(ClassBook.Warriors) || (MinionArt.OfDef(e.Def) != null && MinionArt.OfDef(e.Def).Kind != h.BookKindId(ClassBook.Warriors)))) continue;
                if (e.Skill == MageTag && (!BookRaises(h) || h.LineSacrificed(ClassBook.Mages) || MinionArt.OfDef(e.Def)?.Kind != h.BookKindId(ClassBook.Mages))) continue;
                if (World.Minions.Count >= MaxMinions) break;
                float coef = p.Exec.CoefOf(d) * (e.Skill.EndsWith("#imp") ? 0.6f : e.Skill == MageTag ? 0.8f : 1f);
                var m = p.Exec.Raise(e.Def, d, p.Pos2, coef, e.Skill == skillId ? null : e.Skill);
                if (m == null) continue;
                m.Life = m.MaxLife * Mathf.Clamp(e.Life, 0.05f, 1f);
                n++;
            }
            string want = WantedGolem(h);
            if (want != null && !s.GolemDead) { SpawnGolem(h, p, want, Mathf.Clamp(s.GolemLife, 0.05f, 1f), "followed into " + area); n++; }
            Restored += n;
            if (n > 0 || s.GolemDead) Log.Info("minions", $"area {area}: {n} minion(s) restored at the hero's side ({string.Join(", ", World.Minions.Select(m => m.Def.Id))}){(s.GolemDead ? $"; golem re-forms in {s.GolemReform:0} s" : "")}");
        }
        finally { _restoring = false; }
        _summonGrace = 0.3f;
    }

    private void OnBookChanged(string line)
    {
        var h = H;
        if (h == null || P == null) return;
        // the minions of a changed line crumble; the next cast or corpse raises the new kind
        string tag = line == ClassBook.Warriors ? "sb_raise" : line == ClassBook.Mages ? MageTag : null;
        if (tag != null)
            foreach (var m in World.Minions.ToArray()) if (m.SummonSkill == tag && !m.Dead) { World.Minions.Remove(m); Fx.Ash(m); m.QueueFree(); }
        if (line == ClassBook.Golem) { Dismiss("book changed"); var s = Save; s.GolemDead = false; s.GolemLife = 1f; }
    }

    /// <summary>
    /// Test switches (the "golem stance" of the test bots): --golem=bone|blood|iron|none, --book=w_guard,m_frost,g_iron,
    /// --sacrifice=all|warriors,mages,golem, --forge (forges a fitting metal item from the bag, or rolls one).
    /// </summary>
    private void ApplyHarness(Hero h)
    {
        var g = Game.I;
        if (h.Class != ClassId.Soulbinder || g == null) return;
        string golem = g.ArgValue("golem"), book = g.ArgValue("book"), sac = g.ArgValue("sacrifice");
        bool forge = g.Arg("forge");
        if (golem == null && book == null && sac == null && !forge) return;
        h.EnsureBook();
        if (book != null) foreach (var k in book.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) { var kd = ClassBook.Kind(k); if (kd != null) h.SetBook(kd.Line, kd.Id, 0, true, out _); }
        if (golem != null)
        {
            string id = golem.ToLowerInvariant() switch { "bone" => "g_bone", "blood" => "g_blood", "iron" => "g_iron", "none" => ClassBook.Sacrificed, _ => null };
            if (id != null) h.SetBook(ClassBook.Golem, id, id == ClassBook.Sacrificed ? -1 : 0, true, out _);
        }
        if (sac != null)
            foreach (var l in (sac == "all" ? new[] { ClassBook.Warriors, ClassBook.Mages, ClassBook.Golem } : sac.Split(',', StringSplitOptions.TrimEntries)))
                h.SetBook(l, ClassBook.Sacrificed, -1, true, out _);
        if (sac == "all" && h.D.Level >= SkillDb.KeyPassiveLevel)
        {
            foreach (var kp in SkillDb.PassivesOf(h.Class).Where(x => x.Key)) h.D.Skills.Remove(kp.Id);
            h.D.Skills["kp_sb_alone"] = 1; h.D.Spec = h.D.Level >= SkillDb.SpecLevel ? "sb_offering" : h.D.Spec; h.Recalc();
        }
        if (sac == "all")
        {
            // a lone soul bars no summons: 喚起骸骨 raises nothing, the command has no golem, the colossus would end 孤魂
            var swap = new Dictionary<string, string> { ["sb_raise"] = "sb_spear", ["sb_golem_cmd"] = "sb_blight", ["sb_colossus"] = "sb_explode" };
            for (int i = 0; i < h.D.Bar.Length; i++)
            {
                if (h.D.Bar[i] == null || !swap.TryGetValue(h.D.Bar[i], out var to)) continue;
                var sd = SkillDb.Get(to);
                if (sd == null || sd.UnlockLevel > h.D.Level || h.D.Bar.Contains(to)) { h.D.Bar[i] = null; continue; }
                int rank = Math.Max(1, h.BaseRank(h.D.Bar[i]));
                h.D.Skills.Remove(h.D.Bar[i]);
                h.D.Skills[to] = Math.Max(h.BaseRank(to), rank);
                h.D.Bar[i] = to;
            }
            h.Recalc();
        }
        if (golem != null && golem != "none" && h.BaseRank("sb_golem_cmd") <= 0 && h.D.Level >= 7)
        {
            h.D.Skills["sb_golem_cmd"] = 1;
            if (!h.D.Bar.Contains("sb_golem_cmd")) { int slot = Array.FindIndex(h.D.Bar, 2, b => b == null || b == "sb_blight"); if (slot < 0) slot = 5; h.D.Bar[slot] = "sb_golem_cmd"; }
            h.Recalc();
        }
        if (forge && h.BookKindId(ClassBook.Golem) == "g_iron")
        {
            var it = GolemForge.Candidates(h, true).OrderByDescending(x => GolemForge.Score(x, h)).FirstOrDefault();
            if (it == null) { it = ItemDb.Generate(Math.Clamp(h.D.Level * 10, 10, ItemDb.MaxPower), Rarity.Rare, ClassId.Emberblade, ItemKind.Weapon, new Random(h.D.Seed), "axe2"); h.D.Bag.Add(it); }
            GolemForge.Forge(h, it, out _);
        }
        Log.Info("test", $"golem stance: book=[{string.Join(", ", h.D.Book.Pick.Select(kv => kv.Key + "=" + kv.Value))}] forge={h.D.GolemForge?.Name ?? "-"} bar=[{string.Join(",", h.D.Bar.Select(b => b ?? "-"))}]");
    }

    /// <summary>Damage factor of each Soulbinder skeleton for an army of <paramref name="n"/> (1 alone, 0.75 at six).</summary>
    public const float ArmyStep = 0.067f;
    /// <summary>Every Soulbinder minion's damage (golem and skeletons) against the hero's own scaling (balance 2026-11-23: bosses died 2-4x faster than for other classes).</summary>
    public const float SoulMinionMul = 0.35f;
    public static float ArmyFactor(int n) => 1f / (1f + ArmyStep * Mathf.Max(0, n - 1));
    private static readonly Dictionary<ulong, float> BaseCoef = new();

    // ── taunt rules (balance 2026-11-23: a boss that only ever faced the golem never touched the hero) ──
    public const float BossTauntDur = 3f, BossTauntImmune = 15f;
    private static readonly Dictionary<ulong, double> BossTauntAt = new();
    public static int BossTaunts, BossTauntsRefused;

    /// <summary>
    /// A minion's taunt. Ordinary monsters and elites take it in full. Bosses (and named leaders) ignore passive taunts (golem hits,
    /// 持盾者, the command's echo); the golem's command (骨牢令) holds a boss for 3 s, then the boss shrugs off taunts for 15 s.
    /// </summary>
    public static void Taunt(Actor a, Monster by, float dur, bool command)
    {
        if (a == null || a.Dead) return;
        if (!a.IsBoss) { a.ApplyStatus(St.Taunt(dur), by); return; }
        if (!command) return;
        ulong id = a.GetInstanceId();
        double now = Game.I?.Time ?? 0;
        if (BossTauntAt.TryGetValue(id, out var t) && now - t < BossTauntDur + BossTauntImmune) { BossTauntsRefused++; return; }
        if (BossTauntAt.Count > 32) BossTauntAt.Clear();
        BossTauntAt[id] = now;
        a.ApplyStatus(St.Taunt(Mathf.Min(dur, BossTauntDur)), by);
        BossTaunts++;
    }

    // ── balance log: who dealt the damage of a boss fight (golem, each skeleton kind, the colossus, the hero's own skills) ──
    private readonly Dictionary<string, float> _dmgBy = new();
    private float _heroTaken, _golemTaken; private int _heroHits;

    private void CountDamage(Actor a, Hit h, float amt)
    {
        if (amt <= 0f) return;
        if (a is Player) { _heroTaken += amt; _heroHits++; return; }
        if (a is Monster mm && mm.Faction == Faction.Player) { if (mm == Golem) _golemTaken += amt; return; }
        if (a is not Monster m || m.Faction != Faction.Monster || World.P == null) return;
        string key = h.Source is Monster src && src.Faction == Faction.Player
            ? (src == Golem ? "golem" : MinionArt.OfDef(src.Def.Id)?.Kind ?? src.Def.Id)
            : h.Source == World.P ? (h.Skill?.Id ?? (h.Dot ? "dot" : "hero-other")) : h.Minion ? "minion-dot" : "other";
        _dmgBy[key] = _dmgBy.GetValueOrDefault(key) + amt;
    }

    private void LogBossBreakdown(Monster boss)
    {
        float total = _dmgBy.Values.Sum();
        if (total <= 0f) return;
        string parts = string.Join(" ", _dmgBy.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value / total:P0}"));
        Log.Info("balance", $"{boss.Def?.Id} down: damage since the area began {total:0}: {parts} | hero took {_heroTaken:0} in {_heroHits} hits, golem took {_golemTaken:0}; boss taunts {BossTaunts} (refused {BossTauntsRefused}); army factor now {ArmyFactor(World.Minions.Count(x => x.SummonSkill is "sb_raise" or MageTag)):0.00}");
    }

    private void ResetBreakdown() { _dmgBy.Clear(); _heroTaken = _golemTaken = 0f; _heroHits = 0; }

    /// <summary>For the test suite: every remnant on the ground, gone.</summary>
    public void ClearCorpses() { foreach (var r in World.Remnants) if (GodotObject.IsInstanceValid(r.Fx)) r.Fx.QueueFree(); World.Remnants.Clear(); }
    public void ForgetHeroTarget() => _heroTarget = null;
}
