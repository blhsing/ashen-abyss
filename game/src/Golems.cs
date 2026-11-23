using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════
// Soulbinder minion kinds and golems (tools/golems/PLAN.md §1, §2, §2a).
//
// ART HANDOFF: every model the book's minions are drawn with is chosen in MinionArt.Table below, and nowhere else.
// The 0.4.0 art is PLACEHOLDER: existing bestiary models recoloured (tint), given an edge glow (rim) and rescaled.
// When the real models arrive, change Model (and Tint / Rim / Scale, usually to white / black / 1) in that table;
// nothing else in the code names a model. Fuse points of the forged iron golem: FuseBones (same table, per model).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>How one minion kind is drawn (see ART HANDOFF above).</summary>
public sealed class MinionArt
{
    public string Def, Kind, Base, Model, Name, Note;
    public Color Tint = Colors.White, Rim = new(0.2f, 0.9f, 0.7f);
    public float Scale = 1f, Radius = 0.5f;
    public string[] Attacks, Idle;            // clips; null = the base definition's
    public string Run, Walk;
    public Element Element = Element.Physical;
    /// <summary>Bones the forged item is fused to: right arm, left arm, torso, head (nearest bones of the stand-in model).</summary>
    public string ArmR = "hand_r", ArmL = "lowerarm_l", Torso = "spine_03", Head = "Head";

    public static readonly MinionArt[] Table =
    {
        // ── 骸骨戰士 warriors: material and prop variants of the skeleton family ──
        new() { Def = "sb_w_raider", Kind = "w_raider", Base = "minion_skeleton", Model = "mon_bone_scrap", Name = "骸骨掠襲者", Scale = 1.05f,
                Tint = new Color(0.86f, 0.80f, 0.72f), Rim = new Color(0.95f, 0.45f, 0.15f) * 0.7f, Attacks = new[] { "Zombie_Scratch", "Punch_Jab" }, Idle = new[] { "Zombie_Idle_Loop" }, Run = "Sprint_Loop",
                Note = "small lopsided scrap skeleton, ember rim: reads 'fast'" },
        new() { Def = "sb_w_guard", Kind = "w_guard", Base = "minion_skeleton", Model = "mon_bone_pallbearer", Name = "骸骨持盾者", Scale = 1.05f,
                Tint = new Color(0.78f, 0.80f, 0.84f), Rim = new Color(0.35f, 0.65f, 1f) * 0.7f, Attacks = new[] { "Shield_OneShot", "Sword_Regular_A" },
                Note = "coffin-lid tower shield, cold blue rim: reads 'tank'" },
        new() { Def = "sb_w_reaper", Kind = "w_reaper", Base = "minion_skeleton", Model = "mon_skeleton", Name = "骸骨收割者", Scale = 1.2f,
                Tint = new Color(0.52f, 0.48f, 0.50f), Rim = new Color(0.75f, 0.2f, 0.9f) * 0.75f, Attacks = new[] { "Sword_Attack", "Sword_Heavy_Combo" },
                Note = "larger soot-dark skeleton, violet rim, heavy swings" },
        // ── 骸骨術士 mages (new) ──
        new() { Def = "sb_m_shadow", Kind = "m_shadow", Base = "minion_thrower", Model = "mon_bone_urnbearer", Name = "骸骨影術士", Element = Element.Shadow,
                Tint = new Color(0.72f, 0.62f, 0.86f), Rim = new Color(0.62f, 0.3f, 1f) * 0.8f, Attacks = new[] { "Spell_Simple_Shoot" },
                Note = "urn-bearer skeleton, violet: shadow bolts" },
        new() { Def = "sb_m_frost", Kind = "m_frost", Base = "minion_thrower", Model = "mon_skeleton", Name = "骸骨霜術士", Element = Element.Frost, Scale = 0.98f,
                Tint = new Color(0.70f, 0.84f, 1.0f), Rim = new Color(0.4f, 0.8f, 1f) * 0.85f, Attacks = new[] { "Spell_Simple_Shoot" },
                Note = "ice-blue skeleton casting: frost bolts" },
        new() { Def = "sb_m_bone", Kind = "m_bone", Base = "minion_thrower", Model = "mon_skeleton_thrower", Name = "骸骨骨術士", Element = Element.Physical,
                Tint = new Color(1.0f, 0.95f, 0.82f), Rim = new Color(1f, 0.85f, 0.5f) * 0.7f, Attacks = new[] { "OverhandThrow" },
                Note = "bone-white spear thrower: bone spears paid with life" },
        // ── 魔像 golems: 2.4–3 m bodies ──
        new() { Def = "sb_golem_bone", Kind = "g_bone", Base = "minion_colossus", Model = "mon_bone_amalgam", Name = "骨魔像", Scale = 1.55f, Radius = 0.42f,
                Tint = new Color(1.05f, 1.0f, 0.9f), Rim = new Color(0.85f, 0.95f, 0.75f) * 0.55f, Attacks = new[] { "Sword_Attack", "Sword_Heavy_Combo" },
                Note = "barrow amalgam, bone-white, pale green edge: tank" },
        new() { Def = "sb_golem_blood", Kind = "g_blood", Base = "minion_colossus", Model = "mon_ash_colossus", Name = "血魔像", Scale = 1.3f, Radius = 0.42f, Element = Element.Shadow,
                Tint = new Color(0.48f, 0.08f, 0.08f), Rim = new Color(0.9f, 0.08f, 0.06f) * 0.5f, Attacks = new[] { "Sword_Attack", "Sword_Heavy_Combo" },
                Note = "drift colossus soaked dark red, wet red edge glow: sustain" },
        new() { Def = "sb_golem_iron", Kind = "g_iron", Base = "minion_colossus", Model = "mon_brass_sentinel", Name = "鐵魔像", Scale = 1.5f, Radius = 0.42f,
                Tint = new Color(0.62f, 0.42f, 0.30f), Rim = new Color(1f, 0.5f, 0.15f) * 0.65f, Attacks = new[] { "Sword_Attack", "Sword_Heavy_Combo" },
                Note = "riveted sentinel in rusty slag, forge-orange seams: bruiser; forged items fuse to hand_r / lowerarm_l / spine_03 / Head" },
    };

    public static MinionArt OfKind(string kind) => Table.FirstOrDefault(a => a.Kind == kind);
    public static MinionArt OfDef(string def) => Table.FirstOrDefault(a => a.Def == def);
}

/// <summary>Monster definitions of the book's minions, built from the table (MonsterDb itself is the bestiary's).</summary>
public static class MinionDefs
{
    private static readonly Dictionary<string, MonsterDef> Built = new();

    public static MonsterDef Get(string id)
    {
        if (id == null) return null;
        if (Built.TryGetValue(id, out var d)) return d;
        var a = MinionArt.OfDef(id);
        if (a == null) return null;
        var b = MonsterDb.Get(a.Base);
        if (b == null) { Log.Warn("golem", $"base definition '{a.Base}' of {id} missing"); return null; }
        d = b.Variant(a.Def, a.Name, a.Tint, m =>
        {
            m.Model = a.Model; m.Scale = a.Scale; m.Family = "minion"; m.Element = a.Element;
            m.Radius = a.Radius / Mathf.Max(0.1f, a.Scale);         // collision radius is Radius × Scale: big bodies keep a door-sized footprint
            if (a.Attacks != null) m.AttackAnims = a.Attacks;
            if (a.Idle != null) m.Idle = a.Idle[0];
            if (a.Run != null) m.Run = a.Run;
            if (a.Walk != null) m.Walk = a.Walk;
            switch (a.Kind)
            {
                case "w_raider": m.Speed = 7f; m.AnimSpeed = 1.35f; m.Cooldown = 1.0f; m.Hp = 0.7f; break;
                case "w_guard": m.Speed = 5.0f; m.Cooldown = 1.5f; m.Hp = 1.6f; m.Poise = 3f; break;
                case "w_reaper": m.Speed = 4.6f; m.AnimSpeed = 0.95f; m.Cooldown = 1.9f; m.Range = 2.6f; break;
                case "m_shadow": m.AI = MonAI.Caster; m.Range = 12f; m.Cooldown = 1.7f; m.Contact = 0.4f; m.ProjSpeed = 17f; m.Hp = 0.7f; break;
                case "m_frost": m.AI = MonAI.Caster; m.Range = 11f; m.Cooldown = 1.9f; m.Contact = 0.4f; m.ProjSpeed = 15f; m.Hp = 0.7f; break;
                case "m_bone": m.Range = 12f; m.Cooldown = 2.1f; m.ProjSpeed = 22f; m.Hp = 0.8f; break;
                case "g_bone": m.Hp = 1.6f; m.Speed = 4.8f; m.Cooldown = 1.8f; m.Range = 2.8f; m.Poise = 80f; m.RiseFromGround = true; break;
                case "g_blood": m.Hp = 1.2f; m.Speed = 4.8f; m.Cooldown = 1.6f; m.Range = 2.8f; m.Poise = 80f; m.RiseFromGround = true; break;
                case "g_iron": m.Hp = 1.3f; m.Speed = 4.6f; m.Cooldown = 2.0f; m.Range = 2.9f; m.Poise = 80f; m.RiseFromGround = true; break;
            }
        });
        Built[id] = d;
        return d;
    }

    /// <summary>The definition of a summon: the book's kinds first, then the bestiary.</summary>
    public static MonsterDef Resolve(string id) => Get(id) ?? MonsterDb.Get(id);
}

/// <summary>Per-minion behaviour hooks (set on Monster.Ctl by MinionSystem).</summary>
public abstract class MinionCtl
{
    public Monster M;
    /// <summary>The minion's attack lands now. True when handled (the built-in minion attack is skipped).</summary>
    public virtual bool Attack(Monster m, Actor target, Vector3 hand) => false;
    /// <summary>Damage about to be taken (after the hostile's own scaling); returns what is left.</summary>
    public virtual float Incoming(Monster m, Hit h, float amount) => amount;
    public virtual void Tick(Monster m, float dt) { }
    public virtual void OnDeath(Monster m, Hit h) { }
}

// ───────────────────────────────────────────── warriors and mages ─────────────────────────────────────────────

public sealed class SkeletonCtl : MinionCtl
{
    public string Kind;
    private float _iceT;
    private static readonly List<Actor> Tmp = new();

    public override bool Attack(Monster m, Actor target, Vector3 hand)
    {
        var p = World.P; var h = p?.Hero;
        if (p == null || target == null || !GodotObject.IsInstanceValid(target) || target.Dead) return true;
        float mageMul = 1f + (h.S[Stat.MageDmg]) / 100f;
        var opt = new HitOpt { Minion = true, MinionActor = m, Dir = (target.Pos2 - m.Pos2).Normalized() };
        switch (Kind)
        {
            case "m_shadow": case "m_frost": case "m_bone":
            {
                hand.Y = Mathf.Clamp(hand.Y, 0.9f, 1.8f);
                float coef = m.MinionCoef; int n = 1;
                Element el = m.Def.Element;
                if (Kind == "m_shadow" && h.HasBookUp("m_shadow", 1)) { n = 2; coef *= 0.65f; }
                if (Kind == "m_bone")
                {
                    bool burn = h.HasBookUp("m_bone", 0);
                    float pay = m.MaxLife * (burn ? 0.10f : 0.05f);
                    if (m.Life > pay + 1f) { m.Life -= pay; coef *= burn ? 1.9f : 1.6f; }
                }
                opt.Mul = mageMul;
                for (int i = 0; i < n; i++)
                {
                    Vector2 dir = opt.Dir.Rotated((i - (n - 1) * 0.5f) * 0.18f);
                    var pr = Projectile.SpawnMonster(m, hand, dir, m.Def.ProjSpeed, 18f, 0, el, null);
                    pr.MinionOwner = p; pr.MinionCoef = coef * mageMul; pr.MinionSkill = m.MinionSkill;
                    pr.OnMinionHit = t => OnBoltHit(m, t);
                }
                Snd.At(Kind == "m_frost" ? "frost_cast" : Kind == "m_bone" ? "knife_throw" : "shadow_cast", m.GlobalPosition, 0.45f);
                return true;
            }
            case "w_reaper":
            {
                float r = m.Def.Range + 0.5f + (h.HasBookUp("w_reaper", 0) ? 1.2f : 0f);
                World.InCircle(Faction.Player, m.Pos2 + opt.Dir * (m.Def.Range * 0.6f), r, Tmp);
                foreach (var a in Tmp.ToArray())
                {
                    var o = opt; o.Mul = 1.5f * (h.HasBookUp("w_reaper", 1) && a.Life < a.MaxLife * 0.3f ? 1.4f : 1f);
                    Combat.PlayerHit(p, a, m.MinionSkill, m.MinionCoef, m.Def.Element, o);
                }
                Fx.Burst(World.To3(m.Pos2 + opt.Dir * (m.Def.Range * 0.6f), 0.2f), Element.Physical, r, 0.45f);
                Snd.At("swing_heavy", m.GlobalPosition, 0.5f);
                return true;
            }
            default:     // raider, guard
            {
                float reach = m.Def.Range + m.Radius + target.Radius + 0.6f;
                if (m.Pos2.DistanceTo(target.Pos2) > reach) return true;
                Combat.PlayerHit(p, target, m.MinionSkill, m.MinionCoef, m.Def.Element, opt);
                Fx.Hit(target.Chest, m.Def.Element, false);
                if (Kind == "w_raider" && h.HasBookUp("w_raider", 0) && !target.Dead)
                    target.ApplyStatus(St.Bleed(0.4f, 4), p, Combat.DotDps(p, 0.4f * m.MinionCoef, 4f, Element.Physical, true), Element.Physical, true);
                if (Kind == "w_guard" && !target.Dead) MinionSystem.Taunt(target, m, 3f, false);
                return true;
            }
        }
    }

    private void OnBoltHit(Monster m, Actor t)
    {
        var p = World.P; var h = p?.Hero;
        if (h == null || t == null || t.Dead) return;
        switch (Kind)
        {
            case "m_shadow": if (h.HasBookUp("m_shadow", 0)) t.ApplyStatus(St.Mark(4), p); break;
            case "m_frost":
                t.ApplyStatus(St.Slow(0.3f, 2f), p);
                if (h.HasBookUp("m_frost", 0)) t.ApplyStatus(St.Chill(0.3f), p);
                if (h.HasBookUp("m_frost", 1) && Game.I.Time - _iceT > 3.0) { _iceT = (float)Game.I.Time; p.AddBarrier(p.MaxLife * 0.02f * p.BarrierMul, 4f); }
                break;
            case "m_bone":
                if (h.HasBookUp("m_bone", 1) && t.Dead && GodotObject.IsInstanceValid(m) && !m.Dead) m.Heal(m.MaxLife * 0.2f);
                break;
        }
    }

    public override float Incoming(Monster m, Hit h, float amount)
    {
        var hero = World.P?.Hero;
        if (hero == null) return amount;
        if (Kind == "w_guard")
        {
            amount *= 0.75f * (hero.HasBookUp("w_guard", 0) ? 0.85f : 1f);
            if (hero.HasBookUp("w_guard", 1) && !h.Dot && h.Source is Monster src && src.Faction == Faction.Monster && !src.Dead && src.Pos2.DistanceTo(m.Pos2) < 4.5f)
                src.TakeHit(new Hit { Source = m, Amount = amount * 0.3f, El = Element.Physical, Minion = true, NoReact = true });
        }
        return amount;
    }
}

// ───────────────────────────────────────────── golems ─────────────────────────────────────────────

/// <summary>Attack style of the iron golem, from the weapon class of the item it was forged from (§2a).</summary>
public enum GolemStyle { Fists, TwoHand, OneHand, Polearm, Shield }

/// <summary>Golem stats inherited from a forged item, at minion scale (GolemForge.Inherit).</summary>
public sealed class GolemStats
{
    public float DmgPct, AtkSpdPct, LifePct, DrPct, ResPct, CritPct, BlockPct, Thorns, LifeOnHit, GeneralPct, WeaponPct;
    public float ArmorFlat;
    public int Level;
    public GolemStyle Style = GolemStyle.Fists;
    public Element Element = Element.Physical;
    /// <summary>Legendary power or unique effect the golem uses (null when none, or when it cannot use it).</summary>
    public string Power; public float PowerValue; public string PowerName; public bool PowerUnusable;
    public readonly List<string> Lines = new();
    public float TotalDmgMul => (1f + (DmgPct + GeneralPct + WeaponPct) / 100f);
}

public sealed class GolemCtl : MinionCtl
{
    public string Kind;              // g_bone, g_blood, g_iron
    public GolemStats Forge;         // iron golem forged from an item (null: plain slag fists)
    public int Hits;
    public float CmdDr, CmdDrT;      // 骨牢令: damage taken −50 % while it lasts
    private float _thornT, _transT, _drainT, _drainTick, _undyingT = -999f;
    private Vector2 _lastPos; private float _stuckT;
    private static readonly List<Actor> Tmp = new();

    public GolemStyle Style => Forge?.Style ?? GolemStyle.Fists;
    private static Player P => World.P;
    private static Hero H => World.P?.Hero;

    public float DamageMul
    {
        get
        {
            var h = H; if (h == null) return 1f;
            float m = MinionSystem.SoulMinionMul * (1f + h.S[Stat.GolemDmg] / 100f + (h.D.Spec == "sb_legion" ? 0.25f : 0f) + GolemAsp("legion") / 100f);
            if (Forge != null) m *= Forge.TotalDmgMul;
            return m;
        }
    }

    /// <summary>A legendary power the golem has: worn by the hero, or carried by the item it was forged from.</summary>
    public float GolemAsp(string id)
    {
        float v = H?.Asp(id) ?? 0f;
        if (Forge?.Power == id && !Forge.PowerUnusable) v = Mathf.Max(v, Forge.PowerValue);
        return v;
    }
    public bool GolemUnique(string id) => (H?.HasUnique(id) ?? false) || (Forge?.Power == id && !Forge.PowerUnusable);

    public override void Tick(Monster m, float dt)
    {
        if (CmdDrT > 0f) { CmdDrT -= dt; if (CmdDrT <= 0f) CmdDr = 0f; }
        var p = P; var h = H;
        if (p == null || h == null || p.Dead) return;
        // 輸血: the blood golem pours its life into a dying hero
        if (Kind == "g_blood" && h.HasBookUp("g_blood", 0) && p.LifeFrac < 0.4f && Game.I.Time - _transT > 8.0 && m.Life > m.MaxLife * 0.3f)
        {
            _transT = (float)Game.I.Time;
            float give = m.MaxLife * 0.15f;
            m.Life -= give; p.Heal(give); Fx.Heal(p);
            Fx.Beam(m.Chest, p.Chest, Element.Shadow, 0.3f, 0.5f);
            Snd.At("barrier_up", p.GlobalPosition, 0.5f);
        }
        // 血潮令 running
        if (_drainT > 0f)
        {
            _drainT -= dt; _drainTick -= dt;
            if (_drainTick <= 0f) { _drainTick = 0.5f; DrainPulse(m); }
        }
        // a big body stuck behind a doorway or another body: it passes through to the hero's side
        float far = m.Pos2.DistanceTo(p.Pos2);
        if (far > 6f && m.Pos2.DistanceTo(_lastPos) < 0.05f && !m.Attacking && !m.Rooted) { _stuckT += dt; }
        else _stuckT = 0f;
        _lastPos = m.Pos2;
        if (_stuckT > 1.6f)
        {
            _stuckT = 0f;
            Vector2 to = World.Grid?.RandomWalkNear(p.Pos2, 1.5f, 3f) ?? p.Pos2;
            Log.Info("golem", $"{m.Def.Id} stuck {far:0.0} m from the hero: passes through to ({to.X:0.0}, {to.Y:0.0})");
            m.Pos2 = to;
            MinionSystem.Stuck++;
        }
    }

    // ── attacks ──

    public override bool Attack(Monster m, Actor target, Vector3 hand)
    {
        var p = P; var h = H;
        if (p == null || target == null || !GodotObject.IsInstanceValid(target) || target.Dead) return true;
        Hits++;
        var dir = (target.Pos2 - m.Pos2).Normalized();
        var opt = new HitOpt { Minion = true, MinionActor = m, Dir = dir, Mul = DamageMul };
        float coef = m.MinionCoef;
        Element el = Forge?.Element ?? m.Def.Element;
        bool fourth = Hits % 4 == 0;
        float reach = m.Def.Range + m.Radius + target.Radius + 0.8f;
        float dealt = 0f;

        if (Kind == "g_iron" && Style == GolemStyle.Polearm)
        {
            World.InCone(Faction.Player, m.Pos2, dir, reach + 0.8f, 170f, Tmp);
            foreach (var a in Tmp.ToArray()) dealt += Hit(p, a, coef * 0.85f, el, opt);
            Fx.Slash(m, reach + 0.8f, 170f, el);
        }
        else if (Kind == "g_iron" && Style == GolemStyle.TwoHand)
        {
            World.InCircle(Faction.Player, m.Pos2 + dir * (m.Def.Range * 0.6f), 2.2f, Tmp);
            foreach (var a in Tmp.ToArray()) dealt += Hit(p, a, coef * 1.35f, el, opt);
            Fx.Burst(World.To3(m.Pos2 + dir * (m.Def.Range * 0.6f), 0.2f), el, 2.2f, 0.6f);
            Fx.Shake(0.12f, 0.15f);
        }
        else
        {
            if (m.Pos2.DistanceTo(target.Pos2) <= reach)
            {
                float c = Kind == "g_iron" && Style == GolemStyle.OneHand ? coef * 0.7f : coef;
                dealt += Hit(p, target, c, el, opt);
                Fx.Hit(target.Chest, el, false);
            }
        }
        Snd.At("hit_flesh", m.GlobalPosition, 0.55f);

        switch (Kind)
        {
            case "g_bone":
            {
                float r = 4f * (h.HasBookUp("g_bone", 0) ? 1.5f : 1f);
                World.InCircle(Faction.Player, m.Pos2, r, Tmp);
                foreach (var a in Tmp) MinionSystem.Taunt(a, m, 3f, false);
                break;
            }
            case "g_blood":
                if (dealt > 0f) m.Heal(dealt * 0.25f * (h.D.Spec == "sb_blood" ? 2f : 1f));
                break;
            case "g_iron":
                if (fourth)
                {
                    if (Style == GolemStyle.Shield) ShieldBash(m, target, coef, opt);
                    else Shockwave(m, coef, el);
                }
                else if (Style == GolemStyle.OneHand && !target.Dead && m.Pos2.DistanceTo(target.Pos2) <= reach)
                {
                    var t = target; var o = opt;
                    p.Exec.After(0.22f, () => { if (GodotObject.IsInstanceValid(m) && !m.Dead && GodotObject.IsInstanceValid(t) && !t.Dead) Hit(P, t, coef * 0.7f, el, o); });
                }
                break;
        }
        if (h.HasKey("kp_sb_golemheart")) p.GainResource(2f);
        if (Forge != null && Forge.LifeOnHit > 0f && dealt > 0f) m.Heal(Forge.LifeOnHit);
        return true;
    }

    private float Hit(Player p, Actor a, float coef, Element el, HitOpt opt)
    {
        if (a == null || a.Dead) return 0f;
        var o = opt;
        if (Forge != null && Forge.CritPct > 0f && Rng.Chance(Forge.CritPct / 100f)) o.ForceCrit = true;
        if (GolemAsp("executioner") > 0f && a.Life < a.MaxLife * 0.3f) o.Mul *= 1f + GolemAsp("executioner") / 100f;
        if (GolemAsp("chill_touch") > 0f) { if (a.Has(StatusId.Chill) || a.Has(StatusId.Freeze)) o.Mul *= 1f + GolemAsp("chill_touch") / 100f; a.ApplyStatus(St.Chill(0.15f), p); }
        float d = Combat.PlayerHit(p, a, M.MinionSkill, coef, el, o);
        if (d > 0f && GolemAsp("vuln_mark") > 0f && o.ForceCrit) a.ApplyStatus(St.Vuln(GolemAsp("vuln_mark")), p);
        if (d > 0f && GolemAsp("storm_echo") > 0f && Rng.Chance(GolemAsp("storm_echo") / 100f * 0.5f))
        {
            var t2 = World.Nearest(Faction.Player, a.Pos2, 8f, x => x != a);
            if (t2 != null) { Fx.Beam(a.Chest, t2.Chest, Element.Lightning, 0.2f, 0.25f); Combat.PlayerHit(p, t2, M.MinionSkill, coef * 0.6f, Element.Lightning, opt); }
        }
        return d;
    }

    private void Shockwave(Monster m, float coef, Element el)
    {
        var p = P;
        float r = 4f;
        bool crucible = GolemUnique("u_crucible");
        World.InCircle(Faction.Player, m.Pos2, r, Tmp);
        var opt = new HitOpt { Minion = true, MinionActor = m, Mul = DamageMul * (crucible ? 2.2f : 1f) };
        foreach (var a in Tmp.ToArray())
        {
            Hit(p, a, coef * 0.8f, crucible ? Element.Fire : el, opt);
            if (!a.Dead) a.ApplyStatus(St.Knock(5f), m);
            if (crucible && !a.Dead) Combat.ApplyStatuses(p, a, new[] { St.Burn(0.6f, 3) }, Element.Fire, true);
        }
        Fx.Burst(World.To3(m.Pos2, 0.2f), crucible ? Element.Fire : Element.Physical, r, 1f);
        Fx.Ring(m.GlobalPosition + Vector3.Up * 0.15f, r, crucible ? Palette.Element(Element.Fire) : new Color(0.95f, 0.6f, 0.3f), 0.5f, Sigil.Blade);
        Fx.Shake(0.25f, 0.25f);
        Snd.At("earth_impact", m.GlobalPosition, 0.8f);
        if (H.HasBookUp("g_iron", 0) || GolemAsp("sb_iron_fissure") > 0f) Fissure(m.Pos2, m);
        MinionSystem.Shockwaves++;
    }

    private void ShieldBash(Monster m, Actor target, float coef, HitOpt opt)
    {
        if (target == null || target.Dead) return;
        Hit(P, target, coef * 1.2f, Element.Physical, opt);
        if (!target.Dead) { target.ApplyStatus(St.Stun(1f), m); target.ApplyStatus(St.Knock(7f), m); }
        Fx.Burst(target.Chest, Element.Physical, 1.6f, 0.7f);
        Snd.At("shield_bash", m.GlobalPosition, 0.8f);
        MinionSystem.Shockwaves++;
    }

    /// <summary>熔鐵 / 鐵裂: a burning fissure left by the shockwave and the slam.</summary>
    public void Fissure(Vector2 at, Monster m)
    {
        var p = P;
        float coef = 0.25f + GolemAsp("sb_iron_fissure") / 100f;
        GroundZone.SpawnPlayer(p, M.MinionSkill, new ZoneSpec { Coef = 1f, Dur = 3f, Radius = 3f, El = Element.Fire }, at, coef * DamageMul, false);
    }

    // ── damage intake (golem scaling with the hero, §4) ──

    public override float Incoming(Monster m, Hit hit, float amount)
    {
        var h = H; var p = P;
        if (h == null) return amount;
        int lvl = (hit.Source as Monster)?.Level ?? h.D.Level;
        float armor = h.Armor * 0.6f + (Forge?.ArmorFlat ?? 0f);
        float red = (1f - 0.6f * Balance.ArmorDr(armor, lvl)) * (1f - Mathf.Clamp(h.Res(hit.El) * 0.6f + (Forge?.ResPct ?? 0f), 0f, 70f) / 100f) * (1f - Mathf.Clamp(Forge?.DrPct ?? 0f, 0f, 50f) / 100f);
        if (CmdDr > 0f) red *= 1f - CmdDr;
        if (Kind == "g_bone") red *= 0.85f;
        if (GolemAsp("iron_will") > 0f && m.Life < m.MaxLife * 0.35f) red *= 1f - GolemAsp("iron_will") / 100f;
        float block = (Forge?.BlockPct ?? 0f) + (Style == GolemStyle.Shield ? 15f : 0f);
        if (block > 0f && !hit.Dot && Rng.Chance(Mathf.Min(60f, block) / 100f)) { red *= Combat.BlockedShare; MinionSystem.Blocks++; }
        amount *= red;
        // thorns: the bone golem's, the item's, the hero's (thornmail spreads it)
        if (!hit.Dot && hit.Source is Monster src && src.Faction == Faction.Monster && !src.Dead && src.Pos2.DistanceTo(m.Pos2) < 5f && Game.I.Time - _thornT > 0.25)
        {
            float thorns = (Kind == "g_bone" ? 0.35f * (h.HasBookUp("g_bone", 0) ? 2f : 1f) : 0f);
            float flat = (Forge?.Thorns ?? 0f) + h.S[Stat.Thorns] * 0.5f;
            if (thorns > 0f || flat > 0f)
            {
                _thornT = (float)Game.I.Time;
                var o = new HitOpt { Minion = true, MinionActor = m, NoOnHit = true, NoCrit = true, Mul = DamageMul };
                if (thorns > 0f) Combat.PlayerHit(p, src, M.MinionSkill, thorns, Element.Physical, o);
                if (flat > 0f && !src.Dead) src.TakeHit(new Hit { Source = m, Amount = flat, El = Element.Physical, Minion = true, NoReact = true });
                MinionSystem.ThornHits++;
                float shards = GolemAsp("sb_bone_shards");
                if (shards > 0f)
                {
                    World.InCircle(Faction.Player, m.Pos2, 4f, Tmp);
                    foreach (var a in Tmp.ToArray()) Combat.PlayerHit(p, a, M.MinionSkill, shards / 100f, Element.Physical, o);
                    Fx.Burst(m.Chest, Element.Physical, 4f, 0.5f);
                }
                if (GolemUnique("u_cairn") && Kind == "g_bone") p.AddBarrier(p.MaxLife * 0.02f * p.BarrierMul, 4f);
            }
        }
        // second wind / 不朽泥身: refuse a killing blow once in a while
        if (amount >= m.Life)
        {
            float undying = GolemAsp("sb_undying");
            if (undying > 0f && Game.I.Time - _undyingT > 60.0)
            {
                _undyingT = (float)Game.I.Time;
                m.Life = m.MaxLife * Mathf.Clamp(undying, 10f, 100f) / 100f;
                Fx.Burst(m.Chest, Element.Shadow, 2.5f, 0.8f);
                Snd.At("summon_raise", m.GlobalPosition, 0.7f);
                Log.Info("golem", "不朽泥身: the golem refused death");
                return 0f;
            }
        }
        return amount;
    }

    public override void OnDeath(Monster m, Hit h)
    {
        var p = P; var hero = H;
        if (p == null || hero == null) return;
        if (Kind == "g_blood" && hero.HasBookUp("g_blood", 1))
        {
            p.Exec.Burst(m.Pos2, 5f, 3f, Element.Shadow, m.MinionSkill);
            Fx.Burst(m.Chest, Element.Shadow, 5f, 1.4f);
        }
    }

    // ── the command (sb_golem_cmd) ──

    public void Command(Monster m, SkillDef d, Vector2 aim, float coef, float mul)
    {
        var p = P; var h = H;
        float r = d.Radius;         // 8 m, ×1.4 with 號令擴散
        Color c = Kind switch { "g_bone" => new Color(0.92f, 0.9f, 0.8f), "g_blood" => new Color(0.85f, 0.1f, 0.1f), _ => new Color(1f, 0.55f, 0.2f) };
        switch (Kind)
        {
            case "g_bone":
            {
                Fx.Telegraph(m.Pos2, r, 0.3f, c, true, Sigil.Arcane);
                World.InCircle(Faction.Player, m.Pos2, r, Tmp);
                foreach (var a in Tmp.ToArray()) MinionSystem.Taunt(a, m, 5f, true);
                CmdDr = 0.5f; CmdDrT = 5f;
                p.Exec.Burst(m.Pos2, Mathf.Min(r, 5f), coef * 0.6f * mul, Element.Physical, d, 0.3f);
                Fx.BuffAura(m, Element.Physical, 5f, true);
                Snd.At("boss_roar", m.GlobalPosition, 0.45f);
                m.Model?.Play("Spell_Simple_Enter", 1.2f, 0.1f, true);
                break;
            }
            case "g_blood":
            {
                float rr = 6f * (r / 8f);
                Fx.Telegraph(m.Pos2, rr, 0.3f, c, true, Sigil.Shadow);
                _drainT = 3f; _drainTick = 0f; _drainCoef = coef * 0.25f * mul; _drainR = rr; _drainSkill = d;
                Fx.BuffAura(m, Element.Shadow, 3f, true);
                Snd.At("shadow_cast", m.GlobalPosition, 0.8f);
                m.Model?.Play("Spell_Simple_Idle_Loop", 1f, 0.1f, true);
                break;
            }
            default:
            {
                Vector2 to = World.Grid?.NearestWalk(aim) ?? aim;
                float rr = 4f * (r / 8f);
                Fx.Telegraph(to, rr, 0.45f, c, true, Sigil.Blade);
                var dd = d; float cc = coef * mul;
                m.CommandLeap(to, 0.45f, () =>
                {
                    if (!GodotObject.IsInstanceValid(m) || m.Dead) return;
                    World.InCircle(Faction.Player, to, rr, Tmp);
                    foreach (var a in Tmp.ToArray()) a.ApplyStatus(St.Stun(1.5f), m);
                    p.Exec.Burst(to, rr, cc, Element.Physical, dd);
                    Fx.Shake(0.45f, 0.3f);
                    Fx.Burst(World.To3(to, 0.2f), Element.Physical, rr, 1.2f);
                    Snd.At("earth_impact", World.To3(to), 1f);
                    if (h.HasBookUp("g_iron", 0) || GolemAsp("sb_iron_fissure") > 0f) Fissure(to, m);
                });
                break;
            }
        }
        MinionSystem.Commands++;
        Log.Info("golem", $"command {Kind} at ({aim.X:0.0}, {aim.Y:0.0}) radius {r:0.0}");
    }

    private float _drainCoef, _drainR; private SkillDef _drainSkill;

    private void DrainPulse(Monster m)
    {
        var p = P; var h = H;
        if (p == null || m.Dead) { _drainT = 0f; return; }
        World.InCircle(Faction.Player, m.Pos2, _drainR, Tmp);
        float total = 0f; int kills = 0;
        foreach (var a in Tmp.ToArray())
        {
            float d = Combat.PlayerHit(p, a, _drainSkill, _drainCoef, Element.Shadow, new HitOpt { Dir = (a.Pos2 - m.Pos2).Normalized() });
            total += d;
            if (a.Dead) kills++;
            Fx.Beam(a.Chest, m.Chest, Element.Shadow, 0.15f, 0.3f);
        }
        if (total > 0f)
        {
            float heal = total * 0.4f * (h.D.Spec == "sb_blood" ? 1.5f : 1f);
            p.Heal(heal); m.Heal(heal);
        }
        float harvest = GolemAsp("sb_blood_harvest");
        if (kills > 0 && harvest > 0f)
        {
            m.Heal(m.MaxLife * harvest / 100f * kills);
            for (int i = 0; i < kills; i++) MinionSystem.I?.RaiseFree(m.Pos2, false);
        }
    }

    /// <summary>The golem command's weak echo by one skeleton (共鳴, 30 %).</summary>
    public void Echo(Monster s, SkillDef d, float coef)
    {
        var p = P;
        switch (Kind)
        {
            case "g_bone":
                World.InCircle(Faction.Player, s.Pos2, 3f, Tmp);
                foreach (var a in Tmp) MinionSystem.Taunt(a, s, 2f, false);
                break;
            case "g_blood":
                p.Heal(p.MaxLife * 0.01f);
                break;
            default:
                p.Exec.Burst(s.Pos2, 2f, coef * 0.3f, Element.Physical, d, 0.1f);
                break;
        }
        Fx.Ring(s.GlobalPosition + Vector3.Up * 0.15f, 2f, new Color(0.7f, 0.9f, 0.8f), 0.4f, Sigil.Arcane);
    }
}
