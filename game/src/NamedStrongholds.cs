using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>The seven stronghold bosses (BESTIARY.md 4.2): a BossBrain kit for the fighting in between, a script for the signature mechanics.</summary>
public static partial class NamedDb
{
    static partial void RegisterStrongholds(Registry r)
    {
        BossKits.Register(NK.Kit("sh_wastes", "莫葛", Element.Poison,
            NK.Swing("maul", 4.6f, 100f, Element.Poison, St.Poison(0.3f, 3f), "Zombie_Scratch"),
            NK.Cone("bile", 9f, 7f, 70f, 1.2f, Element.Poison, St.Poison(0.5f, 4f), anim: "Zombie_Scratch"),
            NK.Slam("belly_slam", 11f, 4.5f, 1.4f, Element.Poison, 0, St.Knock(7f)),
            NK.Charge("lumber", 13f, 12f, 3f, 1.3f, Element.Physical, St.Knock(6f))));
        r.Add("sh_wastes", () => new Mog(), "vent rupture tower ember");

        BossKits.Register(NK.Kit("sh_frost", "瓦雷克", Element.Frost,
            NK.Swing("frost_cut", 5.4f, 100f, Element.Frost, St.Chill(0.25f)),
            NK.Cone("castellan_cleave", 8f, 7.4f, 130f, 1.4f, Element.Frost, St.Chill(0.5f, 3f)),
            NK.Charge("gate_rush", 11f, 14f, 3f, 1.3f, Element.Frost, St.Knock(6f)),
            NK.Leap("rampart_leap", 13f, 4f, 1.4f, Element.Frost, St.Chill(0.4f)).At(2)));
        r.Add("sh_frost", () => new Varek(), "banner muster broken ?replant");

        BossKits.Register(NK.Kit("sh_crypt", "柯瓦", Element.Shadow,
            NK.Swing("oath_cut", 5.4f, 100f, Element.Shadow),
            NK.Cone("warden_sweep", 8f, 7f, 140f, 1.4f, Element.Physical, St.Knock(7f)),
            NK.Erupt("grave_spikes", 10f, 2, 2.2f, 1f, Element.Shadow, St.Slow(0.4f, 2f)),
            NK.Charge("warden_rush", 12f, 14f, 3f, 1.3f).At(2)));
        r.Add("sh_crypt", () => new Kova(), "circle raise ?heal");

        BossKits.Register(NK.Kit("sh_mire", "老泥", Element.Poison,
            NK.Swing("stake_swing", 5.8f, 100f, Element.Physical, null, "Sword_Attack"),
            NK.Slam("mud_slam", 9f, 5f, 1.4f, Element.Poison, 1, St.Slow(0.4f, 2f)),
            NK.Erupt("peat_spit", 11f, 2, 2.4f, 0.9f, Element.Poison, St.Poison(0.4f, 3f), 5f, St.Poison(0.6f, 2f))));
        r.Add("sh_mire", () => new OldPeat(), "plate sink sleepers ?replate", n => n.Hp = 24f);

        BossKits.Register(NK.Kit("sh_sands", "第七號", Element.Lightning,
            NK.Swing("scale_smash", 5.6f, 100f, Element.Lightning),
            NK.Cone("market_sweep", 8f, 7.2f, 150f, 1.3f, Element.Lightning, St.Knock(7f)),
            NK.Lines("arc_lines", 10f, 3, 30f, 16f, 2f, 1.1f, Element.Lightning),
            NK.Charge("closing_charge", 12f, 14f, 3.2f, 1.3f, Element.Lightning).At(2)));
        r.Add("sh_sands", () => new Custodian(), "tether shock bell ?earthed");

        BossKits.Register(NK.Kit("sh_sanctum", "葛蘭", Element.Fire,
            NK.Swing("sealer_cut", 5.6f, 100f, Element.Physical),
            NK.Cone("barricade_sweep", 7f, 7.2f, 150f, 1.2f, Element.Physical, St.Knock(11f)),
            NK.Charge("seal_rush", 12f, 13f, 3f, 1.3f, Element.Fire, St.Burn(0.4f, 3f)).At(2)));
        r.Add("sh_sanctum", () => new Gran(), "sweep seal ?encase ?grab ?lit");

        BossKits.Register(NK.Kit("sh_abyss", "烏爾格", Element.Fire,
            NK.Swing("magma_fist", 5.8f, 100f, Element.Fire, St.Burn(0.3f, 3f), "Sword_Attack"),
            NK.Slam("furnace_slam", 9f, 5f, 1.5f, Element.Fire, 1, St.Burn(0.4f, 3f)),
            NK.Erupt("ember_spew", 10f, 2, 2.4f, 1f, Element.Fire, St.Burn(0.4f, 3f), 4f)));
        r.Add("sh_abyss", () => new Urg(), "eat ?overheat ?denied");
    }
}

/// <summary>Helpers for brain-driven bosses: the script may take the body for a while (the brain holds), and times its own specials.</summary>
public abstract class BossScript : NamedScript
{
    protected float T, HoldLeft;
    public override bool Holding => HoldLeft > 0f;
    public override void Engage() { T = 0f; HoldLeft = 0f; }
    public override void Tick(float dt) { T += dt; if (HoldLeft > 0f) HoldLeft -= dt; }
    public override void Reset() { HoldLeft = 0f; }
    /// <summary>The script acts through the body for <paramref name="s"/> seconds: the brain drops what it was doing and waits.</summary>
    protected void Take(float s) { HoldLeft = Mathf.Max(HoldLeft, s); E.Brain?.Suspend(0.05f); }
    protected void Face() { if (E.HeroOk) M.FaceDir(E.HeroPos - M.Pos2); }
    protected Vector2 ToHero => E.DirToHero(M.Pos2);
    protected void Anim(string clip, float speed = 1f) => M.Model?.Play(clip, speed, 0.1f, true);
    protected bool Free => !Holding && !(E.Brain?.Acting ?? false);
}

/// <summary>斷燈哨長・莫葛: vents a poison cloud behind him as he walks; at 66 / 33 % he ruptures (8 m nova, 1.5 s warning) and three husks climb down from the dead beacon tower; dying, he frees the beacon's ember.</summary>
internal sealed class Mog : BossScript
{
    private Vector2 _last; private float _vent;
    public override void Build() => E.AtLife(Rupture, 0.66f, 0.33f);
    private Vector2 Tower => E.L?.Beacon != null && GodotObject.IsInstanceValid(E.L.Beacon) ? E.L.Beacon.Pos2 : E.Center;
    public override void Tick(float dt)
    {
        base.Tick(dt);
        _vent -= dt;
        if (M.Pos2.DistanceTo(_last) > 1.6f && _vent <= 0f)
        {
            _vent = 0.9f;
            Mech("vent");
            E.Zone(M.Pos2 - M.Forward * 1.4f, 2.2f, 6f, 0.1f, Element.Poison, St.Poison(0.35f, 2f));
            _last = M.Pos2;
        }
    }
    private void Rupture()
    {
        Mech("rupture");
        Take(1.8f);
        Anim("Spell_Simple_Enter", 0.7f);
        E.Toast("腹皮撐裂了——退開！");
        E.Blast(M.Pos2, 8f, 1.5f, 1.4f, Element.Poison, St.Poison(0.7f, 4f), () => E.Zone(M.Pos2, 4f, 6f, 0.12f, Element.Poison, St.Poison(0.4f, 2f)));
        Mech("tower");
        for (int i = 0; i < 3; i++) E.SummonAfter(1.2f + i * 0.3f, "husk", E.Near(Tower, 1.5f, 4f));
    }
    public override void Death()
    {
        Mech("ember");
        Fx.Burst(M.Chest, Element.Fire, 4f, 1.5f);
        Fx.Beam(M.Chest, World.To3(Tower, 6f), Element.Fire, 0.5f, 1.2f);
        Game.I?.Hud?.Toast("吞下的餘燼掙脫而出——燈塔重燃", Palette.Ember);
    }
}

/// <summary>凍骨關守將・瓦雷克: plants the garrison banner (8 % of his life): while it stands Frostbound within 15 m cannot be staggered or slowed and a rank of four rises every 22 s, never more than six banner-mustered soldiers alive (the user's "cap and slow", 2026-11-22); breaking it stops the musters and leaves him vulnerable 6 s; he plants it again after 25 s.</summary>
internal sealed class Varek : BossScript
{
    private const float MusterEvery = 22f;
    private const int MusterCap = 6;
    private Monster _banner; private float _replant = -1f, _muster;
    private readonly List<Monster> _ranks = new();
    private int _rising;
    public override Monster BotTarget => _banner != null && GodotObject.IsInstanceValid(_banner) && !_banner.Dead ? _banner : null;
    public override void Engage() { base.Engage(); Plant(); }
    public override void Reset() { base.Reset(); _banner = null; _replant = -1f; _ranks.Clear(); _rising = 0; }
    private void Plant()
    {
        Mech(_replant > -0.5f && T > 1f ? "replant" : "banner");
        Take(1.2f);
        Anim("Sword_Attack", 0.8f);
        Vector2 at = E.Walk(M.Pos2 - M.Forward * 2.5f);
        _banner = E.Prop("守備軍旗", at, M.MaxLife * 0.08f, NamedProps.Banner(new Color(0.55f, 0.75f, 1f)), Broken, 0.6f, 3.2f);
        _muster = 4f; _replant = -1f;
        E.Toast("軍旗插下——寒霜守軍不退！");
    }
    private void Broken()
    {
        _banner = null;
        Mech("broken");
        E.Expose(M, 6f);
        _replant = 25f;
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_replant > 0f) { _replant -= dt; if (_replant <= 0f) Plant(); }
        if (_banner == null || !GodotObject.IsInstanceValid(_banner) || _banner.Dead) return;
        foreach (var m in E.HostilesIn(_banner.Pos2, 15f))
        {
            if (m.Def.Family != "frost" && m.Def.Id != "frostbound") continue;
            m.StTime[(int)StatusId.Slow] = 0f; m.StTime[(int)StatusId.Chill] = 0f; m.Stagger = 0f;
        }
        // the user's decision (2026-11-22, "cap and slow"): a rank of four every 22 s, topped up only to six
        // banner-mustered soldiers alive (the ones still rising count) — no muster at all while six stand
        _muster -= dt;
        if (_muster <= 0f)
        {
            _muster = MusterEvery;
            _ranks.RemoveAll(m => m == null || !GodotObject.IsInstanceValid(m) || m.Dead);
            int room = Math.Min(4, MusterCap - _ranks.Count - _rising);
            if (room <= 0) return;
            Mech("muster");
            Vector2 side = ToHero.Orthogonal();
            for (int i = 0; i < room; i++)
            {
                _rising++;
                E.SummonAfter(0.8f, "frostbound", _banner.Pos2 + side * (-4.5f + 3f * i) + ToHero * 2f, null, m => { _rising = Math.Max(0, _rising - 1); _ranks.Add(m); }, 0.7f);
            }
            E.After(1.2f, () => _rising = 0);                 // a summon that found no room never reports back
        }
    }
}

/// <summary>叛誓守陵人・柯瓦: drives his sword into the ground for a 7 m desecration circle (12 s) from which skeletons climb; every skeleton that dies inside heals him 3 %.</summary>
internal sealed class Kova : BossScript
{
    private Vector2 _c; private float _left;
    public override void Build() => E.Every(18f, Circle, 3f, () => Free);
    private void Circle()
    {
        Mech("circle");
        Take(1.4f);
        Anim("Sword_Attack", 0.6f);
        _c = M.Pos2; _left = 12f;
        E.Mark(_c, 7f, new Color(0.45f, 0.95f, 0.5f), 12f);
        E.Zone(_c, 7f, 12f, 0.04f, Element.Shadow, St.Slow(0.15f, 1f));
        E.Toast("墓土翻動——把骸骨引出圈外再打！");
        Raise(3);
    }
    private void Raise(int n)
    {
        Mech("raise");
        for (int i = 0; i < n; i++) E.SummonAfter(0.9f, "skeleton", E.Near(_c, 1.5f, 6f));
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_left <= 0f) return;
        float was = _left; _left -= dt;
        if ((int)(was / 4f) != (int)(_left / 4f) && _left > 0f) Raise(1);
    }
    public override void Killed(Monster m)
    {
        if (_left <= 0f || !E.Alive || m.Pos2.DistanceTo(_c) > 7f || m.Def.Family != "bone") return;
        Mech("heal");
        E.HealNamed(0.03f);
        Fx.Beam(World.To3(m.Pos2, 1f), M.Chest, Element.Shadow, 0.15f, 0.3f);
    }
}

/// <summary>沉木寨主・老泥: wears the stockade — four plank plates, each absorbing one heavy hit; tears up planks to plate himself again; sinks into the mud and rises under the hero (4.5 m), leaving two peat-sleepers behind.</summary>
internal sealed class OldPeat : BossScript
{
    private int _plates;
    public override void Setup() { _plates = 4; }
    public override void Build()
    {
        E.Every(15f, Sink, 7f, () => Free && E.HeroOk);
        E.Every(22f, Replate, 22f, () => Free && _plates < 3);
    }
    public override void Damaged(Hit h, float amount)
    {
        if (_plates <= 0 || amount < M.MaxLife * 0.012f || h.Dot) return;
        _plates--;
        M.Life = Mathf.Min(M.MaxLife, M.Life + amount * 0.85f);
        Mech("plate");
        Fx.Dust(M.Chest);
        E.Own(NamedProps.Place(NamedProps.Slab(NamedProps.Wood, 1.2f, 0.25f, 0.3f), E.Near(M.Pos2, 1f, 2f), Rng.Range(0f, 3f)));
        if (_plates == 0) E.Toast("木甲全數剝落！");
    }
    private void Replate()
    {
        Mech("replate");
        Take(1.6f);
        Anim("Spell_Simple_Enter", 0.7f);
        E.Then(1.5f, () => { _plates = Math.Min(4, _plates + 2); E.Toast("老泥扯下棧板重新披甲"); });
    }
    private void Sink()
    {
        Mech("sink");
        Take(3.4f);
        Vector2 from = M.Pos2;
        Mech("sleepers");
        for (int i = 0; i < 2; i++) E.SummonAfter(1f, "peat_sleeper", E.Near(from, 1f, 2.5f), "mire_husk");
        E.Hide(M, true);
        Fx.Dust(M.GlobalPosition);
        E.Then(1.4f, () =>
        {
            Vector2 at = E.HeroPos;
            E.Blast(at, 4.5f, 1.2f, 1.5f, Element.Poison, St.Knock(9f), () => { if (E.Alive) { M.Pos2 = E.Walk(at); E.Hide(M, false); Fx.Dust(M.GlobalPosition); } });
        });
    }
    public override void Reset() { base.Reset(); E.Hide(M, false); }
}

/// <summary>商站看守・第七號: arcs tether it to two storm-pylons; while a pylon stands its barrier returns 30 % of melee damage as shock — destroy them to earth it. Closing bell: three arena-wide shock rings with safe gaps, faster below 30 %.</summary>
internal sealed class Custodian : BossScript
{
    private readonly List<Monster> _pylons = new();
    private float _beam, _bell = 8f;
    public override Monster BotTarget => E.Fired.Contains("shock") ? _pylons.FirstOrDefault(p => p != null && GodotObject.IsInstanceValid(p) && !p.Dead) : null;
    public override void Setup()
    {
        _pylons.Clear();
        for (int i = 0; i < 2; i++)
        {
            Vector2 at = E.OnRing(i * Mathf.Pi + 0.8f, Mathf.Min(E.ArenaR - 2f, 9f));
            _pylons.Add(E.Prop("雷樁", at, M.MaxLife * 0.07f, NamedProps.Pylon(new Color(0.75f, 0.85f, 1f)), () =>
            {
                if (_pylons.All(p => p == null || !GodotObject.IsInstanceValid(p) || p.Dead)) { Mech("earthed"); E.Toast("雷樁全毀——看守接地了"); E.Expose(M, 4f); }
            }, 0.6f, 3.2f));
        }
    }
    private bool Tethered => _pylons.Any(p => p != null && GodotObject.IsInstanceValid(p) && !p.Dead);
    public override void Tick(float dt)
    {
        base.Tick(dt);
        _beam -= dt;
        if (_beam <= 0f && Tethered)
        {
            _beam = 0.5f;
            Mech("tether");
            foreach (var p in _pylons) if (p != null && GodotObject.IsInstanceValid(p) && !p.Dead) Fx.Beam(World.To3(p.Pos2, 3.3f), M.Chest, Element.Lightning, 0.12f, 0.45f);
        }
        _bell -= dt;
        if (_bell <= 0f && Free)
        {
            _bell = E.LifeFrac < 0.3f ? 9f : 15f;
            Bell();
        }
    }
    public override void Damaged(Hit h, float amount)
    {
        if (!Tethered || h.Dot || !E.HeroOk || E.HeroDist > 4f || amount <= 0f) return;
        Mech("shock");
        float mul = Mathf.Clamp(amount * 0.3f / Mathf.Max(1f, M.BaseDamage), 0.05f, 2.5f);
        E.Hit(World.P, mul, Element.Lightning);
        Fx.Beam(M.Chest, World.P.Chest, Element.Lightning, 0.1f, 0.15f);
    }
    private void Bell()
    {
        Mech("bell");
        Take(1.2f);
        Snd.At("bell_toll", M.GlobalPosition, 1f);
        E.Toast("打烊鐘響——站到雷環的縫隙裡！");
        float r = E.ArenaR + 2f;
        for (int k = 0; k < 3; k++)
        {
            float inner = k * r / 3f, outer = inner + r / 3f * 0.55f;
            E.RingBlast(M.Pos2, inner, outer, 1.4f + k * 0.5f, 1.2f, Element.Lightning, St.Stun(0.5f));
        }
    }
}

/// <summary>封街騎士長・葛蘭: the barricade's edge is lined with dormant Wax-sealed — his sweeps knock the hero toward them and any touched wakes and grabs (root 1 s); his cauldron of wax encases a hero it hits directly (rooted until 10 % of the hero's life is dealt to the shell); fire near the edge lights the sealed ones.</summary>
internal sealed class Gran : BossScript
{
    private readonly List<Monster> _sealed = new();
    private Monster _shell;
    public override Monster BotTarget => _shell != null && GodotObject.IsInstanceValid(_shell) && !_shell.Dead ? _shell : null;
    public override void Build()
    {
        E.Every(8f, Sweep, 3f, () => Free && E.HeroDist < 8f);
        E.Every(12f, Seal, 6f, () => Free && E.HeroOk);
    }
    public override void Setup()
    {
        _sealed.Clear();
        int n = 8;
        for (int i = 0; i < n; i++)
        {
            var m = E.Summon("wax_sealed", E.OnRing(i * Mathf.Tau / n, Mathf.Max(6f, E.ArenaR - 1.5f)), "husk", false, 1f);
            if (m == null) continue;
            m.Model?.SetTint(new Color(1.15f, 1.1f, 0.95f));
            _sealed.Add(m);
        }
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        foreach (var w in _sealed)
        {
            if (w == null || !GodotObject.IsInstanceValid(w) || w.Dead || w.Aggro) continue;
            w.Busy(0.3f, null);
            if (E.HeroOk && E.HeroPos.DistanceTo(w.Pos2) < 1.8f)
            {
                Mech("grab");
                w.ForceAggro(); w.Busy(0.01f, null);
                World.P.StTime[(int)StatusId.Root] = Mathf.Max(World.P.StTime[(int)StatusId.Root], 1f);
                E.Toast("封蠟者抓住了你！");
            }
            else if (NamedHub.FireNear(w.Pos2, 0.5f) || w.Has(StatusId.Burn))
            {
                Mech("lit");
                w.ForceAggro(); w.Busy(0.01f, null); w.Enrage(30f);
                w.Model?.SetRim(Palette.Ember * 1.6f);
                Fx.BuffAura(w, Element.Fire, 30f, false);
            }
        }
    }
    private void Sweep()
    {
        Mech("sweep");
        Take(1.3f);
        Face();
        Anim("Sword_Attack", 0.8f);
        var near = _sealed.Where(w => w != null && GodotObject.IsInstanceValid(w) && !w.Dead && !w.Aggro).OrderBy(w => w.Pos2.DistanceTo(E.HeroPos)).FirstOrDefault();
        E.ConeBlast(ToHero, 6.5f, 150f, 1f, 1.1f, Element.Physical, null, hero =>
        {
            if (!hero || !E.HeroOk) return;
            Vector2 dir = near != null ? near.Pos2 - E.HeroPos : E.HeroPos - M.Pos2;
            E.Fling(dir, 13f);
        });
    }
    private void Seal()
    {
        Mech("seal");
        Take(1.2f);
        Face();
        Anim("OverhandThrow", 0.9f);
        Vector2 at = E.HeroPos;
        E.Blast(at, 1.7f, 1.2f, 0.8f, Element.Fire, null, () =>
        {
            E.Zone(at, 2.2f, 5f, 0.05f, Element.Fire, St.Slow(0.4f, 1f));
            if (!E.HeroIn(at, 1.8f)) return;
            Mech("encase");
            var p = World.P;
            p.StTime[(int)StatusId.Root] = Mathf.Max(p.StTime[(int)StatusId.Root], 8f); p.CcImmune = 0f;
            _shell = E.Prop("蠟殼", p.Pos2 + new Vector2(0.5f, 0f), E.HeroLifeFrac(0.10f), NamedProps.Heap(new Color(0.95f, 0.92f, 0.82f), 0.9f), () =>
            {
                if (E.HeroOk) World.P.StTime[(int)StatusId.Root] = 0f;
                _shell = null;
            }, 0.7f, 1.6f);
            E.Toast("被封進蠟殼——打碎它！");
            E.After(8f, () => { if (_shell != null) { E.RemoveProp(_shell); _shell = null; } });
        }, true);
    }
}

/// <summary>餘火吞食者・烏爾格: six ember-crystals in the yard — every 12 s he lumbers to the nearest and eats it (+8 % damage, larger, heals 5 %); after four he overheats (10 m eruption) and stands staggered 6 s.</summary>
internal sealed class Urg : BossScript
{
    private int _eaten; private Monster _goal; private bool _going;
    public override void Setup()
    {
        _eaten = 0; _going = false; _goal = null;
        for (int i = 0; i < 6; i++)
            E.Prop("燼晶", E.OnRing(i * Mathf.Tau / 6f + 0.3f, Mathf.Min(E.ArenaR - 2f, 8f)), M.MaxLife * 0.035f, NamedProps.Crystal(new Color(1f, 0.55f, 0.2f)), null, 0.6f, 1.6f);
    }
    public override void Reset() { base.Reset(); M.Model?.SetTint(Colors.White); if (M.Model != null) M.Model.Scale = Vector3.One * E.Def.Scale; }
    public override Monster BotTarget => _going && _goal != null && GodotObject.IsInstanceValid(_goal) && !_goal.Dead && _goal.Pos2.DistanceTo(E.HeroPos) < 8f ? _goal : null;
    public override void Build() => E.Every(12f, () =>
    {
        _goal = E.NearestProp(M.Pos2);
        if (_goal == null) { Mech("denied"); return; }
        _going = true;
        E.Toast("烏爾格盯上了一顆燼晶！");
    }, 6f, () => Free);
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (!_going) return;
        if (_goal == null || !GodotObject.IsInstanceValid(_goal) || _goal.Dead) { _going = false; Mech("denied"); return; }
        Take(0.3f);
        if (!E.Drive(M, _goal.Pos2, dt, 1f, 2f)) return;
        _going = false;
        Mech("eat");
        E.RemoveProp(_goal); _goal = null;
        _eaten++;
        M.DmgMul += 0.08f;
        if (M.Model != null) M.Model.Scale *= 1.05f;
        E.HealNamed(0.05f);
        Fx.Burst(M.Chest, Element.Fire, 3f, 1f);
        if (_eaten < 4) return;
        Mech("overheat");
        _eaten = 0;
        Take(1.8f);
        E.Toast("吃太多了——牠要爆了！");
        E.Blast(M.Pos2, 10f, 1.6f, 1.6f, Element.Fire, St.Burn(0.6f, 3f), () => E.Expose(M, 6f));
    }
}
