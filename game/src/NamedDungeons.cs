using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>The fourteen side-dungeon bosses (BESTIARY.md 4.3), replacing the scaled-up placeholder of each side dungeon.</summary>
public static partial class NamedDb
{
    static partial void RegisterDungeons(Registry r)
    {
        BossKits.Register(NK.Kit("db_wastes_d1", "一家", Element.Poison,
            NK.Swing("huddle_swipe", 4.8f, 110f, Element.Physical, null, "Zombie_Scratch"),
            NK.Slam("table_slam", 9f, 4.5f, 1.3f, Element.Physical, 0, St.Knock(6f)),
            NK.Charge("tumble", 12f, 11f, 3.4f, 1.2f, Element.Physical, St.Knock(6f))));
        r.Add("db_wastes_d1", () => new Household(), "shed split ?rehuddle");

        BossKits.Register(NK.Kit("db_wastes_d2", "枯根之心", Element.Fire,
            NK.Swing("splinter", 5f, 140f, Element.Fire), NK.Erupt("ember_sap", 9f, 2, 2.2f, 0.9f, Element.Fire, St.Burn(0.3f, 3f))).Still().Caster(0f, 30f));
        r.Add("db_wastes_d2", () => new DeadRoots(), "erupt ignite ?stumpling", n => n.Hp = 20f);

        BossKits.Register(NK.Kit("db_frost_d1", "歐德", Element.Frost,
            NK.Swing("watch_cut", 5.4f, 100f, Element.Frost, St.Chill(0.25f)),
            NK.Cone("bell_cleave", 8f, 7f, 130f, 1.3f, Element.Frost, St.Chill(0.5f, 3f)),
            NK.Slam("frost_stamp", 11f, 4.5f, 1.3f, Element.Frost, 1, St.Chill(0.5f, 3f))));
        r.Add("db_frost_d1", () => new OdWatch(), "shell thaw ?regrow");

        BossKits.Register(NK.Kit("db_frost_d2", "霜牙女獵首", Element.Frost,
            NK.Swing("rime_claw", 4.6f, 100f, Element.Frost, St.Chill(0.3f), "Zombie_Scratch"),
            NK.Cone("antler_gore", 8f, 6f, 90f, 1.3f, Element.Frost, St.Bleed(0.4f, 3f))));
        r.Add("db_frost_d2", () => new Matriarch(), "leap icicles ?drop");

        BossKits.Register(NK.Kit("db_crypt_d1", "行棺", Element.Shadow,
            NK.Swing("lid_bash", 5.2f, 100f, Element.Physical, null, "Sword_Attack"),
            NK.Slam("sarcophagus_slam", 9f, 5f, 1.4f, Element.Physical, 1, St.Stun(0.5f)),
            NK.Charge("lion_feet_charge", 11f, 14f, 3.6f, 1.4f, Element.Physical, St.Knock(8f))));
        r.Add("db_crypt_d1", () => new Sarcophagus(), "open wraiths ?forced");

        BossKits.Register(NK.Kit("db_crypt_d2", "骨倉總管", Element.Physical,
            NK.Swing("four_arm", 5.4f, 120f, Element.Physical),
            NK.Cone("ledger_combo", 7f, 6.6f, 110f, 1.1f, Element.Physical, null, 1, 2),
            NK.Erupt("bone_spurs", 10f, 2, 2.2f, 1f, Element.Physical, St.Slow(0.4f, 2f))));
        r.Add("db_crypt_d2", () => new Steward(), "restock ?denied");

        BossKits.Register(NK.Kit("db_mire_d1", "溺窟長鱔", Element.Poison,
            NK.Swing("eel_bite", 5f, 90f, Element.Poison, St.Poison(0.3f, 3f), "Zombie_Scratch")).Still());
        r.Add("db_mire_d1", () => new LongEel(), "flood strike ?breach");

        BossKits.Register(NK.Kit("db_mire_d2", "泥釜三嫗", Element.Poison,
            NK.Bolt("cauldron_spit", Element.Poison, 13f, 0.9f, St.Poison(0.3f, 3f))).Caster(5f, 12f));
        r.Add("db_mire_d2", () => new ThreeHags(), "cast sink ?exposed");

        BossKits.Register(NK.Kit("db_sands_d1", "工坊總樞", Element.Lightning,
            NK.Bolt("hub_arc", Element.Lightning, 16f, 0.8f), NK.Slam("overload_pulse", 10f, 4.5f, 1.2f, Element.Lightning, 1)).Still().Caster(0f, 30f));
        r.Add("db_sands_d1", () => new WorkshopHub(), "deliver beam jam overload");

        BossKits.Register(NK.Kit("db_sands_d2", "伊芙塔", Element.Physical,
            NK.Swing("ring_blades", 4.8f, 110f, Element.Physical, St.Bleed(0.3f, 3f), "Sword_Regular_B"),
            NK.Cone("blade_dance", 6f, 6f, 140f, 0.9f, Element.Physical, St.Bleed(0.4f, 3f), 1, 3),
            NK.Charge("veil_dash", 10f, 12f, 2.4f, 1.2f, Element.Physical, St.Bleed(0.4f, 3f))));
        r.Add("db_sands_d2", () => new Iveta(), "doubles ?blind");

        BossKits.Register(NK.Kit("db_sanctum_d1", "艾妲", Element.Shadow,
            NK.Bolt("hush_note", Element.Shadow, 14f, 0.9f),
            NK.Fan("chord", 7f, 5, 14f, 12f, 0.8f, Element.Shadow),
            NK.Homing("held_breath", 12f, 2, 0.9f, Element.Shadow), NK.Blink("step_of_silence", 7f)).Caster(6f, 13f));
        r.Add("db_sanctum_d1", () => new Abbess(), "rest ?punish ?weak");

        BossKits.Register(NK.Kit("db_sanctum_d2", "藏舌人", Element.Physical,
            NK.Swing("clapper_swing", 5.4f, 100f, Element.Physical, St.Stun(0.3f)),
            NK.Cone("rack_sweep", 8f, 6.8f, 140f, 1.2f, Element.Physical, St.Knock(6f))));
        r.Add("db_sanctum_d2", () => new KeeperOfTongues(), "plant slam");

        BossKits.Register(NK.Kit("db_abyss_d1", "大風箱", Element.Fire,
            NK.Swing("tong_hand", 6f, 120f, Element.Fire, St.Burn(0.3f, 3f))).Still());
        r.Add("db_abyss_d1", () => new GreatBellows(), "blow draw ?gantry");

        BossKits.Register(NK.Kit("db_abyss_d2", "半燼騎士", Element.Fire,
            NK.Swing("half_cut", 5.6f, 100f, Element.Fire, St.Burn(0.3f, 3f)),
            NK.Cone("split_cleave", 8f, 7.2f, 130f, 1.4f, Element.Fire, St.Burn(0.4f, 3f)),
            NK.Charge("ember_rush", 11f, 14f, 3f, 1.3f, Element.Fire)));
        r.Add("db_abyss_d2", () => new HalfBurnt(), "fire hush ?firelit");
    }
}

/// <summary>窖中一家: every 15 % lost one body peels off as a husk and the mass shrinks and speeds up (+8 %); at 25 % the last three split and must all die within 6 s of each other, or they huddle again at 20 %.</summary>
internal sealed class Household : BossScript
{
    private int _shed; private readonly List<Monster> _parts = new();
    private float _window = -1f; private int _cycle; private bool _split;
    public override void Build() => E.AtLife(Shed, 0.85f, 0.70f, 0.55f, 0.40f);
    public override void Setup() { _shed = 0; _parts.Clear(); _window = -1f; _cycle = 0; _split = false; M.SpeedMul = 1f; }
    public override Monster BotTarget => _split && M.Life <= M.MaxLife * 0.011f ? _parts.FirstOrDefault(p => p != null && GodotObject.IsInstanceValid(p) && !p.Dead) : null;
    private void Shed()
    {
        _shed++;
        Mech("shed");
        E.Summon("husk", E.Near(M.Pos2, 1.5f, 3f));
        if (M.Model != null) M.Model.Scale *= 0.93f;
        M.SpeedMul *= 1.08f;
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (!_split && E.LifeFrac <= 0.25f) Split();
        if (!_split) return;
        bool mDown = M.Life <= M.MaxLife * 0.011f;
        int alive = _parts.Count(p => p != null && GodotObject.IsInstanceValid(p) && !p.Dead);
        int down = (mDown ? 1 : 0) + (2 - alive);
        // the six seconds start when the mass is down or nearly so: a bloated part that bursts by itself while the mass is
        // still standing is simply gone (with the roster live they burst on the hero, and the window re-huddled them forever)
        bool early = !mDown && M.Life > M.MaxLife * 0.06f;
        if (down > 0 && _window < 0f && !early) { _window = 6f; E.Toast("三個都得在六秒內倒下！"); }
        if (mDown) { E.Hide(M, alive > 0); Take(0.3f); }
        if (down >= 3) { E.Floor = 0f; E.KillNamed(); return; }
        if (_window < 0f) return;
        _window -= dt;
        if (_window > 0f) return;
        Mech("rehuddle");
        _window = -1f; _cycle++;
        E.Hide(M, false);
        foreach (var p in _parts) if (p != null && GodotObject.IsInstanceValid(p) && !p.Dead) { E.Adds.Remove(p); World.Monsters.Remove(p); p.NoLoot = true; p.QueueFree(); }
        _parts.Clear();
        M.Life = M.MaxLife * 0.2f;
        E.Toast("它們又擠回了一起");
        E.Then(2f, () => { _split = false; Split(); });
    }
    private void Split()
    {
        if (_split) return;
        _split = true;
        Mech("split");
        E.Floor = 0.01f;
        float life = M.Life * 0.6f * MathF.Pow(0.55f, _cycle);
        for (int i = 0; i < 2; i++)
        {
            var p = E.Summon("husk_bloated", E.Near(M.Pos2, 2f, 3.5f), "husk", true, 1f, MonTier.Champion);
            if (p == null) continue;
            p.MaxLife = Mathf.Max(1f, Mathf.Round(life)); p.Life = p.MaxLife;
            _parts.Add(p);
        }
    }
}

/// <summary>枯根之心: the stump cannot be hurt while its four roots are underground; roots erupt in marked lines and rings, and a root set alight (fire damage, or a lit Stumpling dying beside it) stays up and burning 8 s — the heart takes damage only while one burns.</summary>
internal sealed class DeadRoots : BossScript
{
    private readonly Monster[] _roots = new Monster[4];
    private readonly float[] _up = new float[4], _burn = new float[4];
    private readonly Vector2[] _at = new Vector2[4];
    public override Monster BotTarget
    {
        get
        {
            if (_burn.Any(b => b > 0f)) return null;
            // a stumpling dying beside a surfaced root lights it (any hero, fire or not): kill the one closest to a root
            for (int i = 0; i < 4; i++)
            {
                var r = _roots[i];
                if (r == null || !GodotObject.IsInstanceValid(r) || r.Dead || _up[i] <= 0f) continue;
                var s = E.NearestAdd(r.Pos2, a => a.Pos2.DistanceTo(r.Pos2) < 3.2f);      // its only adds are stumplings (a reskin id with the roster live)
                if (s != null) return s;
            }
            for (int i = 0; i < 4; i++) if (_roots[i] != null && GodotObject.IsInstanceValid(_roots[i]) && !_roots[i].Dead && _up[i] > 0f) return _roots[i];
            return null;
        }
    }
    public override void Build()
    {
        E.Every(7f, Erupt, 2f);
        E.Every(11f, () => { Mech("stumpling"); for (int i = 0; i < 2; i++) E.SummonAfter(0.8f, "char_stumpling", E.Near(M.Pos2, 3f, 7f), "imp"); }, 5f);
    }
    public override void Setup()
    {
        for (int i = 0; i < 4; i++) { _at[i] = E.OnRing(i * Mathf.Tau / 4f + 0.6f, 5f); _up[i] = 0f; _burn[i] = 0f; _roots[i] = null; }
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        bool burning = false;
        for (int i = 0; i < 4; i++)
        {
            if (_burn[i] > 0f) { _burn[i] -= dt; burning = true; if (_burn[i] <= 0f) Sink(i); continue; }
            if (_up[i] > 0f)
            {
                _up[i] -= dt;
                var r = _roots[i];
                bool lured = E.Adds.Any(a => a != null && GodotObject.IsInstanceValid(a) && !a.Dead && r != null && GodotObject.IsInstanceValid(r) && a.Pos2.DistanceTo(r.Pos2) < 2.2f);
                if (r != null && GodotObject.IsInstanceValid(r) && (lured || r.Has(StatusId.Burn) || NamedHub.FireNear(r.Pos2, 0.6f))) Ignite(i);
                else if (_up[i] <= 0f) Sink(i);
            }
        }
        M.DrMul = burning ? 1f : 0f;
    }
    private void Erupt()
    {
        Mech("erupt");
        int i = Rng.R.Next(4);
        for (int k = 0; k < 4 && (_up[i] > 0f || _burn[i] > 0f); k++) i = (i + 1) % 4;
        if (_up[i] > 0f || _burn[i] > 0f) return;
        Vector2 dir = E.DirToHero(M.Pos2);
        E.LineBlast(M.Pos2, dir, 16f, 2.2f, 1f, 0.7f, Element.Fire, St.Root(0.8f));
        E.RingBlast(_at[i], 0f, 3f, 1.1f, 0.6f, Element.Fire);
        int idx = i;
        E.After(1.1f, () =>
        {
            if (!E.Alive) return;
            _at[idx] = E.Walk(M.Pos2 + dir * 6f);
            _roots[idx] = E.Prop("燼根", _at[idx], M.MaxLife * 0.15f, NamedProps.Column(new Color(0.35f, 0.22f, 0.15f), 0.45f, 2.6f), () => { _roots[idx] = null; _up[idx] = 0f; _burn[idx] = 0f; }, 0.7f, 2.6f);
            _up[idx] = 6f;
        });
    }
    public override void OtherDamaged(Monster m, Hit h, float amount)
    {
        int i = Array.IndexOf(_roots, m);
        if (i >= 0 && (h.El == Element.Fire)) Ignite(i);
    }
    public override void Killed(Monster m)
    {
        if (!E.Adds.Contains(m) && m.Def.Id != "char_stumpling" && m.Def.Id != "imp") return;      // its adds are the stumplings, whatever id the roster gives them
        for (int i = 0; i < 4; i++) if (_roots[i] != null && GodotObject.IsInstanceValid(_roots[i]) && _up[i] > 0f && _roots[i].Pos2.DistanceTo(m.Pos2) < 3.5f) Ignite(i);
    }
    private void Ignite(int i)
    {
        if (_burn[i] > 0f || _roots[i] == null) return;
        Mech("ignite");
        _burn[i] = 8f; _up[i] = 0f;
        var f = Fx.Fire(0.8f, true);
        if (f != null && GodotObject.IsInstanceValid(_roots[i])) { f.Position = new Vector3(0, 1.4f, 0); _roots[i].AddChild(f); }
        E.Toast("根燒起來了——心臟露出破綻！");
    }
    private void Sink(int i)
    {
        _up[i] = 0f; _burn[i] = 0f;
        if (_roots[i] != null) { E.RemoveProp(_roots[i]); _roots[i] = null; }
    }
}

/// <summary>百年哨・歐德: his ice shell regrows completely every 10 s unless he stands in the light of one of the hall's four braziers (lit by the freed prisoners).</summary>
internal sealed class OdWatch : BossScript
{
    private float _regrow = 10f;
    public override void Setup()
    {
        for (int i = 0; i < 4; i++)
        {
            Vector2 at = E.OnRing(i * Mathf.Tau / 4f + Mathf.Pi / 4f, Mathf.Min(E.ArenaR - 2.5f, 8f));
            var node = E.Own(NamedProps.Place(NamedProps.Brazier(true, 1.1f), at));
            E.Light(at, 6f, true, node);
        }
        M.Barrier = M.MaxLife * 0.25f; M.BarrierLeft = 1e6f;
    }
    public override Vector2? BotStand => E.Alive && !E.InFirelight(M.Pos2) && M.Barrier > 0f ? E.NearestLight(M.Pos2, true)?.Pos : null;
    public override void Engage() { base.Engage(); Mech("shell"); E.Toast("冰殼包住了歐德——引他到火盆旁"); }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        bool lit = E.InFirelight(M.Pos2);
        if (lit && M.Barrier > 0f)
        {
            M.Barrier = Mathf.Max(0f, M.Barrier - M.MaxLife * 0.06f * dt);
            if ((int)(T * 2f) != (int)((T - dt) * 2f)) { Mech("thaw"); Fx.Hit(M.Chest, Element.Fire, false); }
        }
        _regrow -= dt;
        if (_regrow > 0f) return;
        _regrow = 10f;
        if (lit) return;
        Mech("regrow");
        M.Barrier = M.MaxLife * 0.25f; M.BarrierLeft = 1e6f;
        Fx.Burst(M.Chest, Element.Frost, 2.5f, 0.8f);
        E.Toast("冰殼重新長滿——把他引到火盆的光裡！");
    }
}

/// <summary>霜牙女獵首: leaps between the cave's ice pillars instead of walking, every landing shaking icicles over a marked area; shattering the pillar she is perched on drops her, stunned 3 s.</summary>
internal sealed class Matriarch : BossScript
{
    private readonly List<Monster> _pillars = new();
    private Monster _perch; private float _next = 3f; private bool _grounded;
    public override Monster BotTarget => _perch != null && GodotObject.IsInstanceValid(_perch) && !_perch.Dead ? _perch : null;
    public override void Setup()
    {
        _pillars.Clear(); _perch = null; _grounded = false;
        for (int i = 0; i < 5; i++)
            _pillars.Add(E.Prop("冰柱", E.OnRing(i * Mathf.Tau / 5f, Mathf.Min(E.ArenaR - 2.5f, 8.5f)), M.MaxLife * 0.06f, NamedProps.Column(NamedProps.Ice, 0.7f, 3.4f, true), null, 0.9f, 3.4f));
    }
    public override void Engage() { base.Engage(); _next = 1.5f; }
    public override void Killed(Monster m)
    {
        if (!_pillars.Remove(m)) return;
        if (m == _perch)
        {
            Mech("drop");
            _perch = null;
            E.Expose(M, 3f);
            E.Toast("冰柱崩塌——她摔下來了！");
            _next = 4f;
        }
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        _pillars.RemoveAll(p => p == null || !GodotObject.IsInstanceValid(p) || p.Dead);
        if (_pillars.Count == 0) { _grounded = true; return; }
        if (_perch != null) { Take(0.3f); M.Pos2 = _perch.Pos2 + new Vector2(0.01f, 0f); if (E.HeroOk) M.FaceDir(E.HeroPos - M.Pos2, 0.2f); }
        _next -= dt;
        if (_next > 0f || M.Staggered) return;
        _next = 5.5f;
        Leap();
    }
    private void Leap()
    {
        var to = _pillars.Where(p => p != _perch).OrderBy(p => p.Pos2.DistanceTo(E.HeroPos) + Rng.Range(0f, 6f)).FirstOrDefault();
        if (to == null) return;
        Mech("leap");
        Take(1.2f);
        Vector2 from = M.Pos2, land = to.Pos2;
        _perch = null;
        M.Model?.Play("NinjaJump_Idle_Loop", 1f, 0.05f);
        E.Run(0.9f, k => { if (E.Alive) { var q = from.Lerp(land, k); M.GlobalPosition = new Vector3(q.X, Mathf.Sin(k * Mathf.Pi) * 3f + k * 2.4f, q.Y); } }, () =>
        {
            if (!E.Alive) return;
            _perch = to;
            M.GlobalPosition = new Vector3(land.X, 0f, land.Y);
            Mech("icicles");
            for (int i = 0; i < 5; i++) E.Blast(E.Near(E.HeroPos, 0f, 5f), 1.8f, 1.1f + i * 0.2f, 0.8f, Element.Frost, St.Chill(0.4f, 2f), null, true);
            // and she strikes from the pillar at whoever stands below
            if (E.HeroOk && E.HeroDist < 7f) E.ConeBlast(ToHero, 6f, 70f, 0.8f, 1.1f, Element.Frost, St.Bleed(0.3f, 3f));
        });
    }
    public override void Reset() { base.Reset(); _perch = null; if (E.Alive) M.GlobalPosition = new Vector3(M.GlobalPosition.X, 0f, M.GlobalPosition.Z); }
}

/// <summary>行棺: lid closed it takes 90 % less damage and slams and charges; every 14 s the lid slides open, two wraiths come out and the occupant is exposed 5 s. Killing its wraiths quickly opens the lid again sooner.</summary>
internal sealed class Sarcophagus : BossScript
{
    private float _lid = 10f, _open; private readonly List<Monster> _wraiths = new();
    /// <summary>Hint: while the lid is shut (it takes a tenth), kill the wraiths it let out — the last one forces the lid.</summary>
    public override Monster BotTarget => _open <= 0f ? _wraiths.FirstOrDefault(w => w != null && GodotObject.IsInstanceValid(w) && !w.Dead && !w.Untargetable) : null;
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_open > 0f) { _open -= dt; M.DrMul = 1.2f; if (_open <= 0f) { E.Toast("棺蓋闔上了"); } return; }
        M.DrMul = 0.1f;
        _lid -= dt;
        if (_lid <= 0f) Open();
    }
    private void Open()
    {
        _lid = 10f; _open = 6f;
        Mech("open");
        Take(0.8f);
        Anim("Spell_Simple_Enter", 0.8f);
        E.Toast("棺蓋滑開——裡面的東西露出來了！");
        Mech("wraiths");
        _wraiths.Clear();
        for (int i = 0; i < 2 && E.AliveAdds() < 4; i++) { var w = E.Summon("wraith", E.Near(M.Pos2, 1.5f, 3f), null, true, 0.7f); if (w != null) _wraiths.Add(w); }
    }
    public override void Killed(Monster m)
    {
        if (!_wraiths.Remove(m) || _wraiths.Count > 0 || _open > 0f) return;
        Mech("forced");
        _lid = Mathf.Min(_lid, 2f);
    }
}

/// <summary>骨倉總管: at 70 / 40 / 10 % it walks to the nearest bone-stack and rebuilds — heals 12 % and gains one more swing per combo; a stack destroyed beforehand is a heal denied.</summary>
internal sealed class Steward : BossScript
{
    private Monster _goal; private bool _going; private int _extra;
    public override Monster BotTarget => _going && _goal != null && GodotObject.IsInstanceValid(_goal) && !_goal.Dead ? _goal : null;
    public override void Build() => E.AtLife(() => { _goal = E.NearestProp(M.Pos2); if (_goal == null) { Mech("denied"); return; } _going = true; E.Toast("總管走向骨堆——先拆了它！"); }, 0.7f, 0.4f, 0.1f);
    public override void Setup()
    {
        _extra = 0; _going = false;
        for (int i = 0; i < 3; i++)
            E.Prop("骨堆", E.OnRing(i * Mathf.Tau / 3f + 0.5f, Mathf.Min(E.ArenaR - 2f, 8f)), M.MaxLife * 0.05f, NamedProps.Heap(NamedProps.Bone, 1.2f), null, 0.9f, 1.4f);
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (!_going) return;
        if (_goal == null || !GodotObject.IsInstanceValid(_goal) || _goal.Dead) { _going = false; Mech("denied"); E.Toast("骨堆沒了——治療落空"); return; }
        Take(0.3f);
        if (!E.Drive(M, _goal.Pos2, dt, 1f, 1.8f)) return;
        _going = false;
        Mech("restock");
        Take(1.5f);
        Anim("Spell_Simple_Enter", 0.7f);
        E.RemoveProp(_goal); _goal = null;
        E.HealNamed(0.12f);
        _extra++;
        M.AtkSpeedMul = 1f + 0.12f * _extra;
        var combo = BossKits.Get(E.Def.Id).Moves.FirstOrDefault(m => m.Id == "ledger_combo");
        if (combo != null) combo.Count = 2 + _extra;
    }
    public override void Reset()
    {
        base.Reset();
        var combo = BossKits.Get(E.Def.Id).Moves.FirstOrDefault(m => m.Id == "ledger_combo");
        if (combo != null) combo.Count = 2;
        M.AtkSpeedMul = 1f;
    }
}

/// <summary>溺窟長鱔: the cellar floods in three stages; the eel travels and strikes only through water — the hero on a shrinking dry island forces it to breach onto land, slow and exposed 4 s; in water it is untargetable between strikes.</summary>
internal sealed class LongEel : BossScript
{
    private readonly List<Vector2> _islands = new();
    private float _islandR = 5.5f, _next = 3f; private int _stage;
    private readonly List<Node3D> _rings = new();
    public override Vector2? BotStand => E.Alive && _islands.Count > 0 && M.Untargetable && E.Fired.Contains("strike") ? _islands.OrderBy(i => i.DistanceTo(E.HeroPos)).First() : null;
    public override void Build() => E.AtLife(Flood, 0.75f, 0.5f, 0.25f);
    public override void Setup()
    {
        _islands.Clear(); _rings.Clear(); _islandR = 5.5f; _stage = 0;
        for (int i = 0; i < 3; i++) _islands.Add(E.OnRing(i * Mathf.Tau / 3f + 0.3f, Mathf.Min(E.ArenaR - 4f, 7.5f)));
    }
    public override void Engage()
    {
        base.Engage();
        foreach (var i in _islands) _rings.Add(E.Mark(i, _islandR, new Color(0.8f, 0.75f, 0.5f)));
        Flood();
        Submerge();
    }
    private bool OnIsland(Vector2 p) => _islands.Any(i => i.DistanceTo(p) <= _islandR);
    private void Flood()
    {
        _stage++;
        Mech("flood");
        _islandR = Mathf.Max(2.6f, 5.5f - 1f * (_stage - 1));
        foreach (var r in _rings) if (GodotObject.IsInstanceValid(r)) r.Scale = Vector3.One * (_islandR / 5.5f);
        E.Toast(_stage == 1 ? "地窟開始進水" : "水位又漲了——乾地越來越小");
    }
    private void Submerge() { E.Hide(M, true); M.Busy(0f, null); }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        Take(0.3f);
        if (E.HeroOk && !OnIsland(E.HeroPos) && (int)(T * 2f) != (int)((T - dt) * 2f)) E.Hit(World.P, 0.02f, Element.Poison, St.Slow(0.25f, 0.7f));
        _next -= dt;
        if (_next > 0f || !E.HeroOk) return;
        _next = 4.5f;
        if (OnIsland(E.HeroPos)) Breach(); else Strike();
    }
    private void Strike()
    {
        Mech("strike");
        Vector2 at = E.HeroPos;
        E.Blast(at, 2.6f, 1.1f, 1.2f, Element.Poison, St.Poison(0.4f, 3f), () =>
        {
            if (!E.Alive) return;
            M.Pos2 = E.Walk(at); E.Hide(M, false);
            E.After(1.3f, () => { if (E.Alive && !M.Staggered) Submerge(); });
        });
    }
    private void Breach()
    {
        Mech("breach");
        Vector2 at = E.HeroPos, from = E.Walk(at + Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau)) * 8f);
        E.LineBlast(from, at - from, from.DistanceTo(at) + 2f, 2.6f, 1.2f, 1.3f, Element.Poison, St.Knock(6f), () =>
        {
            if (!E.Alive) return;
            M.Pos2 = E.Walk(at - (at - from).Normalized() * 1.5f); E.Hide(M, false);
            E.Expose(M, 4f);
            E.Toast("長鱔擱淺在乾地上！");
            E.After(4.2f, () => { if (E.Alive) Submerge(); });
        });
    }
    public override void Reset() { base.Reset(); E.Hide(M, false); }
}

/// <summary>泥釜三嫗: one life, three bodies — only the hag farthest from the hero casts (empowered volleys, rain); the nearest sinks and re-emerges on the far side. Two kept close together fix the caster and expose her.</summary>
internal sealed class ThreeHags : BossScript
{
    private readonly List<Monster> _sisters = new();
    private float _cast = 3f, _sinkCd;
    public override void Setup()
    {
        _sisters.Clear();
        for (int i = 0; i < 2; i++)
        {
            var s = E.Summon("mire_hag", E.Near(E.Home, 4f, 7f), null, false, 50f);
            if (s == null) continue;
            s.DisplayName = E.Def.NameZh; s.Title = i == 0 ? "簍帽" : "鰻籠帽";
            s.Model?.SetRim(E.Def.Rim * 1.1f);
            _sisters.Add(s);
        }
    }
    private IEnumerable<Monster> All() => _sisters.Where(s => s != null && GodotObject.IsInstanceValid(s) && !s.Dead).Append(M);
    public override void OtherDamaged(Monster m, Hit h, float amount)
    {
        if (!_sisters.Contains(m) || !E.Alive) return;
        m.Life = m.MaxLife;                                         // one life: it is all hers
        float mul = Caster() == m && Exposed() ? 1.5f : 1f;
        M.TakeHit(new Hit { Amount = amount * mul, Source = h.Source, El = h.El, Dot = h.Dot, NoReact = true, Skill = h.Skill });
    }
    public override void Damaged(Hit h, float amount) { if (Caster() == M && Exposed() && !h.NoReact) M.Life -= amount * 0.5f; }
    private Monster Caster() => All().OrderByDescending(s => s.Pos2.DistanceTo(E.HeroPos)).First();
    private bool Exposed()
    {
        var c = Caster(); var others = All().Where(s => s != c).ToList();
        return others.Count == 2 && others[0].Pos2.DistanceTo(others[1].Pos2) < 4f;
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        foreach (var s in _sisters) if (s != null && GodotObject.IsInstanceValid(s) && !s.Dead) { s.Busy(0.3f, null); if (E.HeroOk) s.FaceDir(E.HeroPos - s.Pos2, 0.2f); }
        if (_sinkCd > 0f) _sinkCd -= dt;
        if (!E.HeroOk) return;
        var near = All().OrderBy(s => s.Pos2.DistanceTo(E.HeroPos)).First();
        if (_sinkCd <= 0f && near.Pos2.DistanceTo(E.HeroPos) < 3.5f && !(Exposed()))
        {
            _sinkCd = 4f;
            Mech("sink");
            Fx.Dust(near.GlobalPosition);
            near.Pos2 = E.Walk(E.Center + (E.Center - E.HeroPos).Normalized() * Mathf.Min(E.ArenaR - 2f, 9f) + Vector2.Right.Rotated(Rng.Range(0f, 6f)) * 2f);
            Fx.Dust(near.GlobalPosition);
        }
        _cast -= dt;
        if (_cast > 0f) return;
        _cast = 3.2f;
        var c = Caster();
        bool exposed = Exposed();
        if (exposed) { Mech("exposed"); c.ApplyStatus(St.Vuln(3.2f), null); Fx.Glow(c.Chest, Colors.White, 0.6f, 0.6f); }
        Mech("cast");
        c.Model?.Play("Spell_Simple_Shoot", 1f, 0.1f, true);
        Vector2 d = E.DirToHero(c.Pos2);
        if (Rng.Chance(0.5f))
            for (int i = -2; i <= 2; i++) E.Shoot(c.Pos2, d.Rotated(Mathf.DegToRad(i * 12f)), 12f, 22f, 0.8f, Element.Poison, St.Poison(0.3f, 3f));
        else
            for (int i = 0; i < 4; i++) E.Blast(E.Near(E.HeroPos, 0f, 3.5f), 2f, 1.1f + i * 0.2f, 0.8f, Element.Poison, St.Poison(0.4f, 3f), null, true);
    }
}

/// <summary>工坊總樞: every 18 s each running conveyor delivers a construct (key-winder, then brass-hermits, then a sentinel); destroying a line's drive-gear jams it; all three jammed, the hub overloads and opens its core for 8 s. Its heliostat arm sweeps a beam across the floor throughout.</summary>
internal sealed class WorkshopHub : BossScript
{
    private readonly Monster[] _gears = new Monster[3];
    private readonly Vector2[] _line = new Vector2[3];
    private float _deliver = 6f, _core, _beamA; private int _wave;
    public override Monster BotTarget => _core > 0f ? null : _gears.FirstOrDefault(g => g != null && GodotObject.IsInstanceValid(g) && !g.Dead);
    public override void Setup() { for (int i = 0; i < 3; i++) Gear(i); _wave = 0; _core = 0f; }
    private void Gear(int i)
    {
        _line[i] = E.OnRing(i * Mathf.Tau / 3f + 0.5f, Mathf.Min(E.ArenaR - 2.5f, 9f));
        _gears[i] = E.Prop("傳動齒輪", _line[i], M.MaxLife * 0.06f, NamedProps.Gear(new Color(1f, 0.75f, 0.35f)), () => Jam(i), 0.9f, 1.6f);
    }
    private bool Running(int i) => _gears[i] != null && GodotObject.IsInstanceValid(_gears[i]) && !_gears[i].Dead;
    private void Jam(int i)
    {
        Mech("jam");
        _gears[i] = null;
        E.Toast($"輸送線卡住了（{3 - Enumerable.Range(0, 3).Count(Running)}/3）");
        if (Enumerable.Range(0, 3).Any(Running)) return;
        Mech("overload");
        _core = 8f;
        E.Expose(M, 8f);
        E.Toast("總樞過載——核心打開了！");
        E.After(8.2f, () => { if (E.Alive) for (int k = 0; k < 3; k++) if (!Running(k)) Gear(k); });
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_core > 0f) { _core -= dt; M.DrMul = 1.5f; } else M.DrMul = 0.12f;
        _deliver -= dt;
        if (_deliver <= 0f)
        {
            _deliver = 18f;
            string kind = (_wave++ % 3) switch { 0 => "key_winder", 1 => "brass_hermit", _ => "brass_sentinel" };
            for (int i = 0; i < 3; i++)
            {
                if (!Running(i) || E.AliveAdds() >= 8) continue;
                Mech("deliver");
                int n = kind == "brass_hermit" ? 2 : 1;
                for (int k = 0; k < n; k++) E.SummonAfter(0.8f, kind, E.Near(_line[i], 1f, 2.5f), kind == "brass_sentinel" ? null : "sand_husk", null, kind == "brass_sentinel" ? 0.6f : 1f);
            }
        }
        // the heliostat arm: a beam that sweeps the floor
        float was = _beamA;
        _beamA += dt * 0.55f;
        if ((int)(was / 0.9f) != (int)(_beamA / 0.9f))
        {
            Mech("beam");
            E.LineBlast(M.Pos2, Vector2.Right.Rotated(_beamA + 0.5f), E.ArenaR + 2f, 1.6f, 0.9f, 0.7f, Element.Fire, St.Burn(0.3f, 2f));
        }
    }
}

/// <summary>墓道劍舞・伊芙塔: at 75 / 50 / 25 % she bursts into sand and three doubles step out — they die in one hit but burst into blinding sand; only the real one's rings sound and eyes glow.</summary>
internal sealed class Iveta : BossScript
{
    private readonly List<Monster> _doubles = new();
    public override void Build() => E.AtLife(Doubles, 0.75f, 0.5f, 0.25f);
    private void Doubles()
    {
        Mech("doubles");
        Take(0.8f);
        Fx.Burst(M.Chest, Element.Physical, 3.5f, 1.2f);
        Fx.Dust(M.GlobalPosition);
        var spots = new List<Vector2> { M.Pos2 };
        for (int i = 0; i < 3; i++) spots.Add(E.Near(M.Pos2, 3f, 6f));
        spots = spots.OrderBy(_ => Rng.F()).ToList();
        M.Pos2 = E.Walk(spots[0]);
        for (int i = 1; i < 4; i++)
        {
            var d = E.Summon("sand_revenant", spots[i], null, true, 0.02f);
            if (d == null) continue;
            d.DisplayName = E.Def.NameZh; d.Title = "";
            if (d.Model != null) d.Model.Scale = M.Model?.Scale ?? d.Model.Scale;
            d.Model?.SetTint(E.Def.Tint);
            _doubles.Add(d);
        }
        Snd.At("bell_toll", M.GlobalPosition, 0.35f);
        M.Model?.SetRim(new Color(1f, 0.85f, 0.4f) * 2f);
    }
    public override void Killed(Monster m)
    {
        if (!_doubles.Remove(m)) return;
        Mech("blind");
        Fx.Burst(World.To3(m.Pos2, 1f), Element.Physical, 3f, 1f);
        if (E.HeroIn(m.Pos2, 3f)) E.Hit(World.P, 0.1f, Element.Physical, St.Weaken(0.3f, 3f));
    }
}

/// <summary>止息院長・艾妲: every 12 s a two-second rest (a ring closing on the hero) — any skill used during it is answered by a heavy shadow strike; a rest with nothing cast leaves her taking 50 % more damage for 4 s.</summary>
internal sealed class Abbess : BossScript
{
    private float _rest = -1f, _weak; private bool _cast, _wasBusy;
    public override void Build() => E.Every(12f, Rest, 4f);
    private void Rest()
    {
        Mech("rest");
        Take(2.1f);
        Anim("Spell_Simple_Enter", 0.6f);
        _rest = 2f; _cast = false; _wasBusy = World.P?.Busy ?? false;
        E.Track(Fx.TelegraphRing(E.HeroPos, 0f, 2.6f, 2f, new Color(0.85f, 0.85f, 1f), Sigil.Arcane));
        E.Warn("休止——此刻別施放任何技能");
        Snd.At("bell_toll", M.GlobalPosition, 0.6f);
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_weak > 0f) { _weak -= dt; M.DrMul = 1.5f; if (_weak <= 0f) M.DrMul = 1f; }
        if (_rest < 0f || !E.HeroOk) return;
        bool busy = World.P.Busy;
        if (busy && !_wasBusy && !_cast) { _cast = true; Mech("punish"); E.Blast(E.HeroPos, 2.4f, 0.6f, 2f, Element.Shadow, St.Stun(0.6f)); }
        _wasBusy = busy;
        _rest -= dt;
        if (_rest >= 0f) return;
        if (!_cast) { Mech("weak"); _weak = 4f; E.Toast("休止守住了——院長露出破綻"); }
    }
}

/// <summary>藏舌人: throws clappers that stick upright in the floor (up to five); his slam makes every planted clapper ring a stun at once. Clappers can be knocked down.</summary>
internal sealed class KeeperOfTongues : BossScript
{
    public override void Build()
    {
        E.Every(5.5f, Plant, 2f, () => Free && E.AliveProps() < 5);
        E.Every(11f, Slam, 8f, () => Free);
    }
    /// <summary>Hint: knock down a clapper whose ring would reach the hero (within 3.6 m) before his slam makes it ring (two or more planted).</summary>
    public override Monster BotTarget
    {
        get
        {
            if (!E.HeroOk || E.AliveProps() < 2) return null;
            var c = E.NearestProp(E.HeroPos);
            return c != null && c.Pos2.DistanceTo(E.HeroPos) < 3.6f ? c : null;      // only one whose ring would reach the hero
        }
    }
    private void Plant()
    {
        Mech("plant");
        Take(0.9f);
        Face();
        Anim("OverhandThrow", 1f);
        Vector2 at = E.Near(E.HeroPos, 2f, 5f);
        E.Blast(at, 1f, 0.8f, 0.6f, Element.Physical, null, () =>
            E.Prop("鐘舌", at, M.MaxLife * 0.012f, NamedProps.Stake(new Color(0.75f, 0.65f, 0.4f), 1.8f, new Color(1f, 0.85f, 0.5f)), null, 0.45f, 1.8f));
    }
    private void Slam()
    {
        Mech("slam");
        Take(1.4f);
        Anim("Sword_Attack", 0.7f);
        E.Blast(M.Pos2, 4f, 1.1f, 1.2f, Element.Physical, St.Stun(0.6f));
        foreach (var c in E.Props.ToArray()) if (c != null && GodotObject.IsInstanceValid(c) && !c.Dead) E.RingBlast(c.Pos2, 0f, 3.6f, 1.1f, 0.6f, Element.Physical, St.Stun(1.2f));
        Snd.At("bell_toll", M.GlobalPosition, 0.9f);
    }
}

/// <summary>大風箱: 5 s of BLOW (the hero pushed toward the slag channels at the edge, burning patches fanned to double size) and 5 s of DRAW (pulled toward the nozzle and its tong-hand guards). Only the thrall on top can be hurt — during DRAW, or from the two side gantries.</summary>
internal sealed class GreatBellows : BossScript
{
    private bool _draw; private float _phase = 5f, _fan;
    private Vector2[] _gantry = new Vector2[2];
    public override Vector2? BotStand => E.Alive && !_draw ? _gantry.OrderBy(g => g.DistanceTo(E.HeroPos)).First() : null;
    public override void Setup()
    {
        Vector2 side = M.Forward.Orthogonal();
        _gantry[0] = E.Walk(M.Pos2 + side * 4.5f); _gantry[1] = E.Walk(M.Pos2 - side * 4.5f);
        foreach (var g in _gantry) E.Own(NamedProps.Place(NamedProps.Slab(NamedProps.Iron, 2.4f, 0.3f, 2.4f), g));
        for (int i = 0; i < 8; i++) E.Zone(E.OnRing(i * Mathf.Tau / 8f, E.ArenaR - 0.5f), 2f, 9999f, 0.2f, Element.Fire, St.Burn(0.5f, 2f));
    }
    public override void Engage()
    {
        base.Engage();
        Setup();
    }
    private bool OnGantry => E.HeroOk && _gantry.Any(g => g.DistanceTo(E.HeroPos) < 2.2f);
    public override void Tick(float dt)
    {
        base.Tick(dt);
        _phase -= dt;
        if (_phase <= 0f)
        {
            _phase = 5f; _draw = !_draw;
            Mech(_draw ? "draw" : "blow");
            E.Toast(_draw ? "風箱吸氣——爐僕現在打得到！" : "風箱鼓風——小心被吹進熔渣槽");
            if (_draw && E.HeroOk) { Vector2 d = M.Forward; E.ConeBlast(ToHero, 5f, 120f, 1.5f, 1.2f, Element.Fire, St.Burn(0.4f, 3f)); }
        }
        bool gantry = OnGantry;
        if (gantry && (int)(T * 2f) != (int)((T - dt) * 2f)) Mech("gantry");
        M.DrMul = _draw || gantry ? 1f : 0f;
        if (!E.HeroOk) return;
        Vector2 away = E.HeroPos - M.Pos2;
        if (_draw) E.Blow(-away, 2.8f, dt);
        else if (!gantry) E.Blow(away, 3.4f, dt);
        if (!_draw)
        {
            _fan -= dt;
            if (_fan <= 0f)
            {
                _fan = 1.2f;
                foreach (var z in NamedHub.AllZones().ToArray())
                {
                    var s = NamedHub.ZoneSpecOf(z);
                    if (s == null || s.El != Element.Fire || s.Radius > 6f || NamedHub.ZonePos(z).DistanceTo(M.Pos2) > E.ArenaR + 3f || s.Dur > 999f) continue;
                    if (NamedHub.ZoneOfMonster(z)) s.Radius = Mathf.Min(s.Radius * 1.25f, 5f);
                }
            }
        }
    }
}

/// <summary>半燼騎士: changes stance every 15 s. FIRE: his swings leave burning ground and he takes full damage. HUSH: puts out burning ground, drains resource in an aura and takes 60 % less damage unless the hero stands in firelight (his leftover flames or the hero's fire).</summary>
internal sealed class HalfBurnt : BossScript
{
    private bool _hush; private float _stance = 15f, _trail;
    private readonly List<GroundZone> _flames = new();
    public override void Setup() { _hush = false; _stance = 15f; M.Model?.SetRim(Palette.Ember * 1.3f); }
    /// <summary>Hint: in HUSH, fight from the edge of one of his leftover flames (lit, not standing in the fire).</summary>
    public override Vector2? BotStand
    {
        get
        {
            if (!_hush || !E.Alive || !E.HeroOk || E.InFirelight(E.HeroPos)) return null;
            GroundZone best = null; float bd = 12f;
            foreach (var z in _flames)
            {
                if (z == null || !GodotObject.IsInstanceValid(z) || z.Done) continue;
                float d = NamedHub.ZonePos(z).DistanceTo(E.HeroPos);
                if (d < bd) { bd = d; best = z; }
            }
            if (best == null) return null;
            Vector2 c = NamedHub.ZonePos(best);
            if (c.DistanceTo(E.HeroPos) < 2.9f) return null;                 // lit already
            return E.Walk(c + (M.Pos2 - c).Normalized() * 2.6f);
        }
    }
    public override void Engage() { base.Engage(); Mech("fire"); }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        _stance -= dt;
        if (_stance <= 0f)
        {
            _stance = 15f; _hush = !_hush;
            Mech(_hush ? "hush" : "fire");
            M.Model?.SetRim(_hush ? new Color(0.9f, 0.9f, 1f) * 1.4f : Palette.Ember * 1.3f);
            E.Toast(_hush ? "寂之半身亮起——站進火光裡才傷得了他" : "燼之半身燃起");
            if (_hush)
            {
                NamedHub.SnuffHeroZones(M.Pos2, 30f, Element.Fire);
                var keep = _flames.Where(z => z != null && GodotObject.IsInstanceValid(z) && !z.Done).ToList();
                E.After(4f, () => { foreach (var z in keep) if (GodotObject.IsInstanceValid(z) && !z.Done) z.Remove(); });
            }
        }
        if (!_hush)
        {
            M.DrMul = 1f;
            _trail -= dt;
            if (_trail <= 0f && E.HeroDist < 7f && (E.Brain?.Acting ?? false))
            {
                _trail = 1.6f;
                var z = E.Zone(E.Walk(M.Pos2 + M.Forward * 2.5f), 2f, 9f, 0.12f, Element.Fire, St.Burn(0.4f, 2f));
                if (z != null) _flames.Add(z);
            }
            return;
        }
        bool lit = E.HeroOk && (E.InFirelight(E.HeroPos) || _flames.Any(z => z != null && GodotObject.IsInstanceValid(z) && !z.Done && NamedHub.ZonePos(z).DistanceTo(E.HeroPos) < 3f));
        M.DrMul = lit ? 1f : 0.4f;
        if (lit && (int)(T * 2f) != (int)((T - dt) * 2f)) Mech("firelit");
        if (E.HeroDist < 8f) E.Drain(0.03f * dt);
    }
}
