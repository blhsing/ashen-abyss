using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>The Ember Stair (燼階), eight ember-memories fought in order (BESTIARY.md 4.6). Rungs 1-6 and 8 are remixes of the region bosses' kits; rung 7 is new.</summary>
public static partial class NamedDb
{
    static partial void RegisterPinnacle(Registry r)
    {
        void Remix(string id, string from, Action<BossKit> change)
        {
            var k = BossKits.Get(from).Derive(id, change);
            k.Tempo = 1.15f; k.EnrageAt = 330f;
            BossKits.Register(k);
        }
        Remix("pin_1_gravedigger", "boss_gravedigger", k =>
        {
            var sweep = k.Moves.First(m => m.Id == "shovel_sweep"); sweep.Arc = 200f; sweep.Radius = 8.2f; sweep.Status = St.Knock(13f);
        });
        r.Add("pin_1_gravedigger", () => new PinGravedigger(), "pit burial headstone ?dug");

        Remix("pin_2_rimelord", "boss_rimelord", k => { });
        r.Add("pin_2_rimelord", () => new PinRimelord(), "cold leapsnuff whiteout ?frozen");

        Remix("pin_3_bishop", "boss_bishop", k => { k.WaveAdds = new[] { "skeleton", "skeleton", "skeleton_thrower" }; });
        r.Add("pin_3_bishop", () => new PinBishop(), "urn bearer prison3 ?floor ?revive");

        Remix("pin_4_mire_mother", "boss_mire_mother", k =>
        {
            var pull = k.Moves.First(m => m.Id == "devouring_pull"); pull.MaxRange = 40f; pull.Length = 40f; pull.MinRange = 4f;
            var brood = k.Moves.First(m => m.Id == "brood_call"); brood.Adds = new[] { "rotspawn" }; brood.Count = 1;
            k.WaveAdds = new[] { "rotspawn", "mire_husk", "mire_husk" };
        });
        r.Add("pin_4_mire_mother", () => new PinMireMother(), "hightide lowtide ?soaked");

        Remix("pin_5_sand_tyrant", "boss_sand_tyrant", k => { });
        r.Add("pin_5_sand_tyrant", () => new PinTyrant(), "wedge mirrorswing guards reforge ?doublecross ?reflect");

        Remix("pin_6_grand_hush", "boss_grand_hush", k => { var c = k.Moves.First(m => m.Id == "choir_call"); c.Adds = new[] { "choir" }; c.Phase = 1; c.Cd = 26f; });
        r.Add("pin_6_grand_hush", () => new PinGrandHush(), "rest toll3 bells ?punish ?choirshield");

        BossKits.Register(new BossKit
        {
            Id = "pin_7_firstkeeper", Short = "伊蕾", El = Element.Fire, Tempo = 1.1f, EnrageAt = 330f,
            PhaseLines = new[] { "「你學得很快。可火不是學來的。」", "「看清楚——這是第一盞燈最後的樣子。」" },
            EnrageLine = "「天要亮了。不是為你。」",
            WaveAdds = Array.Empty<string>(), WaveCount = 0,
            Haste = new[] { 1f, 1.15f, 1.3f }, Gcd = new[] { 1.3f, 1f, 0.7f },
            Basic = NK.Swing("keeper_cut", 5f, 100f, Element.Fire, St.Burn(0.3f, 3f), "Sword_Regular_A"),
            Moves = new[]
            {
                // melee mirror
                NK.Slam("m_whirl", 8f, 4.8f, 1.4f, Element.Fire, 1, St.Burn(0.4f, 3f)),
                NK.Charge("m_dash", 9f, 13f, 2.6f, 1.4f, Element.Fire, St.Knock(7f)),
                NK.Leap("m_leap", 11f, 4f, 1.5f, Element.Fire, St.Stun(0.6f)),
                // archer mirror
                NK.Fan("r_volley", 6f, 5, 12f, 18f, 0.8f, Element.Physical, St.Bleed(0.3f, 3f), 2),
                NK.Rain("r_rain", 12f, 3f, 2.2f, 0.9f, Element.Physical),
                NK.Erupt("r_traps", 10f, 3, 2f, 1f, Element.Fire, St.Root(1f)),
                // caster mirror
                NK.Fan("c_bolt", 5f, 1, 0f, 13f, 1.6f, Element.Fire, St.Burn(0.5f, 3f)),
                NK.Rain("c_meteor", 12f, 3.5f, 2.6f, 1.1f, Element.Fire, St.Burn(0.4f, 3f)),
                new BossMoveDef { Id = "c_nova", Kind = BossMoveKind.Slam, Cd = 9f, MaxRange = 7f, Radius = 6f, Wind = 1f, Recover = 0.7f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Fire, Dmg = 1.4f, Shots = 10, Speed = 9f, Reach = 16f },
                NK.Blink("lantern_step", 7f),
            },
        });
        r.Add("pin_7_firstkeeper", () => new PinFirstKeeper(), "mirror brazier snuff");

        Remix("pin_8_osaros", "boss_osaros", k => { k.Tempo = 1.2f; });
        r.Add("pin_8_osaros", () => new PinOsaros(), "dark extinguish echo ?ember");

        // the voices of the bosses that speak an intro (the voice run designs a new boss_* voice when it does not know one)
        foreach (var n in All)
        {
            if (n.Intro == null) continue;
            if (n.Kind == NamedKind.Stronghold) n.IntroVoice = "boss_" + n.Id;
            if (n.Id == "wb_firstbearer") n.IntroVoice = "boss_wb_firstbearer";
            if (n.Id == "pin_7_firstkeeper") n.IntroVoice = "boss_firstkeeper";
        }
    }
}

/// <summary>A full-screen veil over the world, under the HUD: whiteout, the dark with a ring of light round the hero.</summary>
public partial class NamedOverlay : CanvasLayer
{
    private static NamedOverlay _i;
    private ColorRect _rect; private ShaderMaterial _mat;
    private float _strength, _target, _hole;
    private Color _tint = Colors.Black;
    private const string Code = @"shader_type canvas_item;
uniform vec4 tint : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float strength = 0.0;
uniform float hole = 0.0;
uniform vec2 centre = vec2(0.5, 0.5);
uniform float aspect = 1.7777;
void fragment() {
    vec2 p = (UV - centre) * vec2(aspect, 1.0);
    float a = strength;
    if (hole > 0.0) a *= smoothstep(hole, hole + 0.14, length(p));
    COLOR = vec4(tint.rgb, a);
}";

    public static void Set(Color tint, float strength, float hole = 0f)
    {
        var g = Game.I;
        if (g == null) return;
        if (_i == null || !IsInstanceValid(_i)) { _i = new NamedOverlay { Name = "NamedOverlay", Layer = -1 }; g.AddChild(_i); }
        _i._tint = tint; _i._target = strength; _i._hole = hole;
    }
    public static void Clear() { if (_i != null && IsInstanceValid(_i)) _i._target = 0f; }
    public static void Hole(float hole) { if (_i != null && IsInstanceValid(_i)) _i._hole = hole; }

    public override void _Ready()
    {
        _mat = new ShaderMaterial { Shader = new Shader { Code = Code } };
        _rect = new ColorRect { Material = _mat, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White };
        _rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _strength = Mathf.MoveToward(_strength, _target, dt * 1.5f);
        if (World.Root == null || !(Game.I?.State == Game.GameState.Play)) _strength = 0f;
        _rect.Visible = _strength > 0.005f;
        if (!_rect.Visible) return;
        var vp = GetViewport().GetVisibleRect().Size;
        Vector2 c = new(0.5f, 0.5f);
        var cam = Game.I?.Cam?.Cam; var p = World.P;
        if (cam != null && p != null && IsInstanceValid(p) && !cam.IsPositionBehind(p.GlobalPosition))
        {
            var s = cam.UnprojectPosition(p.GlobalPosition + Vector3.Up * 1f);
            c = new Vector2(s.X / Mathf.Max(1f, vp.X), s.Y / Mathf.Max(1f, vp.Y));
        }
        _mat.SetShaderParameter("tint", _tint);
        _mat.SetShaderParameter("strength", _strength);
        _mat.SetShaderParameter("hole", _hole);
        _mat.SetShaderParameter("centre", c);
        _mat.SetShaderParameter("aspect", vp.X / Mathf.Max(1f, vp.Y));
    }
}

/// <summary>Things two or more rungs share.</summary>
public abstract class PinScript : BossScript
{
    protected readonly List<Vector2> Trail = new();
    private float _trailT;
    protected bool HeroActed(ref bool was)
    {
        if (!E.HeroOk) return false;
        bool busy = World.P.Busy; bool rise = busy && !was; was = busy; return rise;
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        _trailT -= dt;
        if (_trailT <= 0f && E.HeroOk) { _trailT = 1f; Trail.Add(E.HeroPos); if (Trail.Count > 6) Trail.RemoveAt(0); }
    }
    protected void Pit(Vector2 at, float r = 1.8f)
    {
        E.Own(NamedProps.Place(NamedProps.Patch(r, new Color(0.06f, 0.05f, 0.05f)), at));
        E.Zone(at, r, 9999f, 0.15f, Element.Physical, St.Root(2f));
    }
    /// <summary>Bone prison on the hero's last three positions at once.</summary>
    protected void PrisonOnTrail(float wind = 1.1f)
    {
        int n = Trail.Count;
        for (int i = Math.Max(0, n - 3); i < n; i++) E.Blast(Trail[i], 1.9f, wind, 1.1f, Element.Shadow, St.Slow(0.3f, 1.5f), null, false, "hit_bone");
    }
    public override void Death() { NamedOverlay.Clear(); }
    public override void Reset() { base.Reset(); NamedOverlay.Clear(); }
}

/// <summary>不眠掘墓者・葛魯姆: every grave-hands eruption leaves an open pit for the fight (root 2 s, hurts) and a wider shovel sweep knocks the hero toward them; at 60 % and 30 % he buries the hero (dig out with eight attacks) while husks rise; headstone-rain stones stay standing as projectile walls.</summary>
internal sealed class PinGravedigger : PinScript
{
    private float _buried = -1f; private int _digs; private bool _was; private Node3D _mound;
    private int _pits;
    public override void Build() => E.AtLife(Burial, 0.6f, 0.3f);
    public override void Setup() { _pits = 0; _buried = -1f; }
    internal override void Dropped(BossMoveDef d, Vector2 at, float radius)
    {
        if (d.Id == "grave_hands" && _pits < 14) { _pits++; Mech("pit"); Pit(at, 1.7f); }
        if (d.Id == "headstone_rain" && E.AliveProps() < 10)
        {
            Mech("headstone");
            var s = E.Prop("墓碑", at, M.MaxLife * 0.02f, NamedProps.Slab(NamedProps.Stone, 1.2f, 1.8f, 0.35f), null, 0.7f, 1.8f);
            if (s != null) s.BlocksProjectiles = true;
        }
    }
    private void Burial()
    {
        if (!E.HeroOk) return;
        Mech("burial");
        var p = World.P;
        _buried = 8f; _digs = 0; _was = p.Busy;
        p.Untargetable = true;
        p.StTime[(int)StatusId.Root] = Mathf.Max(p.StTime[(int)StatusId.Root], 8f); p.CcImmune = 0f;
        _mound = E.Own(NamedProps.Place(NamedProps.Heap(new Color(0.3f, 0.25f, 0.2f), 1.4f), p.Pos2));
        E.Toast("被活埋了！連續攻擊八次挖出來");
        for (int i = 0; i < 4; i++) E.SummonAfter(1f + i * 0.4f, "husk", E.Near(p.Pos2, 3f, 7f));
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_buried < 0f) return;
        _buried -= dt;
        if (HeroActed(ref _was)) { _digs++; Fx.Dust(World.P.GlobalPosition); }
        if (_digs >= 8 || _buried < 0f || !E.HeroOk)
        {
            if (_digs >= 8) Mech("dug");
            _buried = -1f;
            if (E.HeroOk) { World.P.Untargetable = false; World.P.StTime[(int)StatusId.Root] = 0f; }
            if (_mound != null && GodotObject.IsInstanceValid(_mound)) _mound.QueueFree();
        }
    }
    public override void Reset() { base.Reset(); if (World.P != null && GodotObject.IsInstanceValid(World.P)) World.P.Untargetable = false; }
    public override void Death() { base.Death(); if (World.P != null && GodotObject.IsInstanceValid(World.P)) World.P.Untargetable = false; }
}

/// <summary>永冬・赫爾蒙: a cold meter fills whenever the hero is more than 6 m from one of three braziers (full: frozen 3 s); his glacier leap targets the braziers and snuffs them 15 s; Whiteout replaces the blizzard ring.</summary>
internal sealed class PinRimelord : PinScript
{
    private float _cold, _leap = 10f, _white = -1f, _whiteCd = 6f; private bool _warned;
    public override Vector2? BotStand => E.HeroOk && _cold > 0.35f ? E.NearestLight(E.HeroPos, true)?.Pos : null;
    public override void Setup()
    {
        for (int i = 0; i < 3; i++)
        {
            Vector2 at = E.OnRing(i * Mathf.Tau / 3f + 0.4f, Mathf.Min(E.ArenaR - 3f, 9f));
            var n = E.Own(NamedProps.Place(NamedProps.Brazier(true, 1.2f), at));
            E.Light(at, 6f, true, n);
        }
        var b = E.Brain;
        if (b != null) b.Veto = d => d.Id == "glacier_leap" || d.Id == "blizzard";
        _cold = 0f;
    }
    public override void Engage() { base.Engage(); var b = E.Brain; if (b != null) b.Veto = d => d.Id == "glacier_leap" || d.Id == "blizzard"; }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (!E.HeroOk) return;
        bool warm = E.Lights.Any(l => l.Lit && l.Pos.DistanceTo(E.HeroPos) <= 6f);
        _cold = Mathf.Clamp(_cold + (warm ? -dt / 3f : dt / 9f), 0f, 1f);
        if (_cold > 0.5f && !_warned) { _warned = true; Mech("cold"); E.Warn("寒意入骨——靠近火盆！"); }
        if (_cold < 0.3f) _warned = false;
        if (_cold >= 1f) { _cold = 0f; Mech("frozen"); E.Hit(World.P, 0.4f, Element.Frost, St.Freeze(3f)); World.P.CcImmune = 0f; }
        if ((int)(T * 2) != (int)((T - dt) * 2) && _cold > 0.1f) World.P.ApplyStatus(St.Chill(_cold * 0.6f, 0.6f), M);
        _leap -= dt;
        if (_leap <= 0f && Free) { _leap = 14f; LeapAtBrazier(); }
        if (_white >= 0f) { _white -= dt; if (_white < 0f) NamedOverlay.Clear(); }
        else if (E.Brain != null && E.Brain.Phase >= 2) { _whiteCd -= dt; if (_whiteCd <= 0f) { _whiteCd = 24f; Whiteout(); } }
    }
    private void LeapAtBrazier()
    {
        var l = E.Lights.Where(x => x.Lit).OrderBy(x => x.Pos.DistanceTo(E.HeroPos)).FirstOrDefault();
        if (l == null) return;
        Mech("leapsnuff");
        Take(1.8f);
        Vector2 from = M.Pos2, to = E.Walk(l.Pos + (M.Pos2 - l.Pos).Normalized() * 1.2f);
        M.Model?.Play("NinjaJump_Idle_Loop", 1f, 0.05f);
        E.Track(Fx.Telegraph(l.Pos, 4.2f, 1f, Palette.Danger, M.Def.Element));
        E.Run(1f, k => { if (E.Alive) { var q = from.Lerp(to, k); M.GlobalPosition = new Vector3(q.X, Mathf.Sin(k * Mathf.Pi) * 3f, q.Y); } }, () =>
        {
            if (!E.Alive) return;
            M.GlobalPosition = new Vector3(to.X, 0f, to.Y);
            E.HitCircle(l.Pos, 4.2f, 1.6f, Element.Frost, St.Chill(0.5f, 3f));
            Fx.Burst(World.To3(l.Pos, 0.3f), Element.Frost, 4f, 1f);
            E.SetLit(l, false, 15f);
            E.Toast("火盆被凍熄了");
        });
    }
    private void Whiteout()
    {
        Mech("whiteout");
        _white = 6f;
        NamedOverlay.Set(new Color(0.94f, 0.96f, 1f), 0.7f);
        E.Warn("白茫——只看得見火光與危險的徵兆");
    }
}

/// <summary>空棺・瑪爾薩: she keeps her life in three urns and cannot fall below 66 / 33 / 1 % until the matching urn is broken; each urn is guarded by an urn-bearer who revives her adds; bone prison closes on the hero's last three positions.</summary>
internal sealed class PinBishop : PinScript
{
    private readonly Monster[] _urns = new Monster[3];
    private readonly Monster[] _bearers = new Monster[3];
    private readonly List<Vector2> _graves = new();
    private int _broken; private float _revive = 8f;
    public override Monster BotTarget
    {
        get
        {
            if (E.Floor <= 0f || M.Life > M.MaxLife * (E.Floor + 0.02f)) return null;
            var u = _urns.FirstOrDefault(x => x != null && GodotObject.IsInstanceValid(x) && !x.Dead);
            var b = u != null ? _bearers[Array.IndexOf(_urns, u)] : null;
            return b != null && GodotObject.IsInstanceValid(b) && !b.Dead && b.Pos2.DistanceTo(u.Pos2) < 5f ? b : u;
        }
    }
    public override void Setup()
    {
        _broken = 0; _graves.Clear();
        for (int i = 0; i < 3; i++)
        {
            int k = i;
            Vector2 at = E.OnRing(i * Mathf.Tau / 3f + 0.2f, Mathf.Min(E.ArenaR - 2.5f, 9f));
            _urns[i] = E.Prop($"骨灰甕・{"一二三"[i]}", at, M.MaxLife * 0.04f, NamedProps.Urn(new Color(0.75f, 1f, 0.8f)), () => Broken(k), 0.6f, 1.4f);
            _bearers[i] = E.Summon("urn_bearer", E.Near(at, 1f, 2f), "cult_caster", false, 2f, MonTier.Champion);
        }
        E.Floor = 0.66f;
    }
    private void Broken(int k)
    {
        Mech("urn");
        _urns[k] = null; _broken++;
        E.Floor = _broken switch { 1 => 0.33f, 2 => 0.01f, _ => 0f };
        E.Toast(_broken >= 3 ? "最後的甕碎了——她不再有退路" : "一只骨灰甕碎了");
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (E.Floor > 0f && M.Life <= M.MaxLife * (E.Floor + 0.005f) && (int)(T * 0.5f) != (int)((T - dt) * 0.5f)) Mech("floor");
        foreach (var b in _bearers) if (b != null && GodotObject.IsInstanceValid(b) && !b.Dead && !b.Aggro) { int i = Array.IndexOf(_bearers, b); if (_urns[i] != null && GodotObject.IsInstanceValid(_urns[i])) E.Drive(b, _urns[i].Pos2, dt, 1f, 2f); }
        _revive -= dt;
        if (_revive > 0f) return;
        _revive = 8f;
        bool any = _bearers.Any(b => b != null && GodotObject.IsInstanceValid(b) && !b.Dead);
        if (!any) return;
        Mech("bearer");
        if (_graves.Count == 0) return;
        Mech("revive");
        foreach (var g in _graves.Take(2).ToList()) { _graves.Remove(g); E.SummonAfter(0.8f, "skeleton", g); }
    }
    public override void Killed(Monster m) { if (m.Def.Family == "bone" && _graves.Count < 8) _graves.Add(m.Pos2); }
    internal override void Move(BossMoveDef d) { if (d.Id == "bone_prison") { Mech("prison3"); E.After(0.3f, () => PrisonOnTrail()); } }
}

/// <summary>滿潮・艾絲珂: the tide rises and falls on a 30 s cycle — at high tide only the root-islands are dry and standing in water stacks poison and slow; at low tide her roots are exposed and she takes double damage. The devouring pull runs along the water from any range; her brood are elder leeches.</summary>
internal sealed class PinMireMother : PinScript
{
    private float _tide = 0f; private bool _high;
    private readonly List<Vector2> _islands = new();
    private readonly List<Node3D> _water = new();
    public override Vector2? BotStand => _high && E.HeroOk && !OnIsland(E.HeroPos) ? _islands.OrderBy(i => i.DistanceTo(E.HeroPos)).FirstOrDefault() : null;
    public override void Setup()
    {
        _islands.Clear();
        for (int i = 0; i < 4; i++)
        {
            Vector2 at = E.OnRing(i * Mathf.Tau / 4f + 0.5f, Mathf.Min(E.ArenaR - 3f, 8.5f));
            _islands.Add(at);
            E.Own(NamedProps.Place(NamedProps.Ring(3f, new Color(0.6f, 0.75f, 0.35f), 1.4f), at));
        }
        _tide = 0f; _high = false;
    }
    private bool OnIsland(Vector2 p) => _islands.Any(i => i.DistanceTo(p) <= 3f);
    public override void Tick(float dt)
    {
        base.Tick(dt);
        _tide -= dt;
        if (_tide <= 0f)
        {
            _tide = 15f; _high = !_high;
            Mech(_high ? "hightide" : "lowtide");
            E.Toast(_high ? "潮水漲了——只剩根島是乾的" : "潮水退了——她的根露出來了！");
            foreach (var w in _water) if (GodotObject.IsInstanceValid(w)) w.QueueFree();
            _water.Clear();
            if (_high) _water.Add(E.Own(NamedProps.Place(NamedProps.Patch(E.ArenaR + 2f, new Color(0.12f, 0.2f, 0.12f, 1f)), E.Center)));
        }
        M.DrMul = _high ? 1f : 2f;
        if (_high && E.HeroOk && !OnIsland(E.HeroPos) && (int)T != (int)(T - dt)) { Mech("soaked"); E.Hit(World.P, 0.12f, Element.Poison, St.Poison(0.4f, 3f)); World.P.ApplyStatus(St.Slow(0.3f, 1.2f), M); }
    }
}

/// <summary>日蝕王・伊薩克: a wedge of shadow crosses the dais — in light every glaive strike is followed by a delayed mirror-image of the same swing, in shadow his storm crosses strike twice as often; two mirror-guards reflect projectiles until killed, and a mask-maker re-forges them at 50 %.</summary>
internal sealed class PinTyrant : PinScript
{
    private float _angle, _cone; private Monster _pointer;
    private readonly List<Monster> _guards = new();
    private BossMoveDef _cross;
    public override void Build() => E.AtLife(() => { Mech("reforge"); E.Toast("面具匠重鑄了鏡衛"); Guards(); }, 0.5f);
    public override void Setup()
    {
        _pointer = E.Prop("", E.Center, 1e9f, null, null, 0.1f, 0.1f); _pointer.Untargetable = true;
        _cross = BossKits.Get(E.Def.Id).Moves.FirstOrDefault(m => m.Id == "storm_cross");
    }
    public override void Engage() { base.Engage(); Guards(); }
    private void Guards()
    {
        _guards.RemoveAll(g => g == null || !GodotObject.IsInstanceValid(g) || g.Dead);
        while (_guards.Count < 2)
        {
            Mech("guards");
            var g = E.Summon("dune_mirror", E.Near(M.Pos2, 3f, 5f), "brass_sentinel", true, 1.5f, MonTier.Champion);
            if (g == null) break;
            g.Title = "鏡衛"; _guards.Add(g);
        }
    }
    private bool InShadow(Vector2 p)
    {
        Vector2 d = p - E.Center;
        float a = Mathf.Wrap(Mathf.Atan2(d.Y, d.X) - _angle, -Mathf.Pi, Mathf.Pi);
        return Mathf.Abs(a) <= Mathf.Pi / 3f;
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        _angle += dt * 0.3f;
        if (_pointer != null && GodotObject.IsInstanceValid(_pointer))
        {
            _pointer.Facing = Mathf.Pi * 0.5f - _angle; _pointer.Rotation = new Vector3(0, _pointer.Facing, 0);
            _cone -= dt;
            if (_cone <= 0f) { _cone = 0.6f; Mech("wedge"); E.Track(Fx.TelegraphCone(_pointer, E.ArenaR + 5f, 120f, 0.6f, new Color(0.12f, 0.1f, 0.25f, 0.55f))); }
        }
        foreach (var g in _guards)
        {
            if (g == null || !GodotObject.IsInstanceValid(g) || g.Dead) continue;
            foreach (var s in NamedHub.AllShots().ToArray())
            {
                if (!NamedHub.ShotOfHero(s) || NamedHub.ShotPos(s).DistanceTo(g.Pos2) > 2.2f) continue;
                NamedHub.Kill(s); Mech("reflect");
                E.Shoot(g.Pos2, E.DirToHero(g.Pos2), 17f, 22f, 0.9f, Element.Lightning);
            }
        }
    }
    internal override void Move(BossMoveDef d)
    {
        bool shadow = InShadow(M.Pos2);
        if ((d.Id == "glaive_sweep" || d.Id == "glaive_cut") && !shadow)
        {
            Vector2 dir = M.Forward; float r = d.Radius, arc = d.Arc;
            E.After(1.1f, () => { if (!E.Alive) return; Mech("mirrorswing"); M.FaceDir(dir); E.ConeBlast(dir, r, arc, 0.7f, d.Dmg * 0.8f, Element.Lightning); });
        }
        if (d.Id == "storm_cross" && shadow && _cross != null) { Mech("doublecross"); E.After(2.4f, () => E.Brain?.Force(_cross)); }
    }
}

/// <summary>終止式・賽倫: the score — four measures loop on a visible staff; on rest measures any skill used is answered by a shadow strike, on toll measures his bell-toll rings come in triplets. Two Hung Bells drift and extinguish ground effects; the choir he calls shields him, not themselves.</summary>
internal sealed class PinGrandHush : PinScript
{
    private int _measure; private float _mt = 4f; private bool _was, _struck;
    private List<Node3D> _staff;
    private readonly List<Monster> _bells = new();
    private BossMoveDef _toll;
    public override void Setup()
    {
        _toll = BossKits.Get(E.Def.Id).Moves.FirstOrDefault(m => m.Id == "bell_toll");
        if (_staff == null)
        {
            _staff = new List<Node3D>();
            for (int i = 0; i < 4; i++) { var o = NamedProps.Orb(new Color(0.9f, 0.9f, 1f), 0.4f, 0f); M.AddChild(o); o.Position = new Vector3(-1.5f + i, 4.6f, 0f); _staff.Add(o); }
        }
        _bells.Clear();
        for (int i = 0; i < 2; i++)
        {
            var b = E.Prop("懸鐘", E.OnRing(i * Mathf.Pi + 1f, 7f), 1e9f, NamedProps.Column(new Color(0.75f, 0.65f, 0.4f), 0.9f, 1.4f), null, 0.8f, 3f);
            b.Untargetable = true; E.Unpin(b); _bells.Add(b);
        }
        _measure = 0; _mt = 4f;
    }
    private bool Rest => _measure % 2 == 0;
    public override Monster BotTarget => E.HostilesIn(M.Pos2, 30f).FirstOrDefault(m => m.Def.Id == "choir");
    public override void Tick(float dt)
    {
        base.Tick(dt);
        _mt -= dt;
        if (_mt <= 0f)
        {
            _mt = 4f; _measure = (_measure + 1) % 4; _struck = false;
            Mech(Rest ? "rest" : "toll3");
            E.Warn(Rest ? "休止小節——別施放技能" : "鐘鳴小節");
            if (!Rest && _toll != null) E.Brain?.Force(_toll);
        }
        if (_staff != null) for (int i = 0; i < 4; i++) if (GodotObject.IsInstanceValid(_staff[i])) _staff[i].Scale = Vector3.One * (i == _measure ? 1.6f : 0.7f);
        if (Rest && HeroActed(ref _was) && !_struck) { _struck = true; Mech("punish"); E.Blast(E.HeroPos, 2.4f, 0.7f, 1.2f, Element.Shadow, St.Stun(0.6f)); }
        else if (!Rest) HeroActed(ref _was);
        // the hung bells drift through the nave and put out what burns
        for (int i = 0; i < _bells.Count; i++)
        {
            var b = _bells[i];
            if (b == null || !GodotObject.IsInstanceValid(b)) continue;
            Vector2 to = E.Center + Vector2.Right.Rotated(T * 0.15f + i * Mathf.Pi) * Mathf.Min(E.ArenaR - 3f, 8f);
            E.MoveProp(b, E.Walk(b.Pos2 + (to - b.Pos2).LimitLength(2f * dt)));
            if (NamedHub.SnuffHeroZones(b.Pos2, 3.5f) > 0) Mech("bells");
            if ((int)T != (int)(T - dt)) Mech("bells");
        }
        int choir = E.AliveAdds(a => a.Def.Id == "choir") + (E.Brain?.AddCount ?? 0) * 0;
        foreach (var m in E.HostilesIn(M.Pos2, 30f)) if (m.Def.Id == "choir") choir++;
        bool shielded = choir > 0;
        M.DrMul = shielded ? 0.7f : 1f;
        if (shielded && (int)T != (int)(T - dt)) { Mech("choirshield"); Fx.Beam(M.Chest, M.Chest + Vector3.Up, Element.Shadow, 0.1f, 0.2f); }
    }
    internal override void Move(BossMoveDef d)
    {
        if (d.Id != "bell_toll" || Rest) return;
        Mech("toll3");
        E.After(1.2f, () => { if (E.Alive) E.Waves(M.Pos2, 3, 3.6f, 3.2f, 0.7f, new[] { 0.6f }, 1f, Element.Shadow); });
        E.After(2.4f, () => { if (E.Alive) E.Waves(M.Pos2, 3, 3.6f, 3.2f, 0.7f, new[] { 0.6f }, 1f, Element.Shadow); });
    }
}

/// <summary>初代守燈人・伊蕾: a mirror match — three moves taken from the hero's own class at boss scale; whoever stands in the central brazier's light regenerates and deals 30 % more, and she fights for it; at 40 % she snuffs it and the last phase is fought in the dark.</summary>
internal sealed class PinFirstKeeper : PinScript
{
    private LightSpot _fire; private string _set = "m_"; private bool _dark;
    public override void Build() => E.AtLife(() => { Mech("snuff"); _dark = true; if (_fire != null) E.SetLit(_fire, false); E.Toast("伊蕾親手捻熄了火盆"); NamedOverlay.Set(Colors.Black, 0.82f, 0.32f); }, 0.4f);
    public override void Setup()
    {
        var node = E.Own(NamedProps.Place(NamedProps.Brazier(true, 1.6f, new Color(1f, 0.7f, 0.35f)), E.Center));
        _fire = E.Light(E.Center, 5f, true, node);
        _dark = false;
        var cls = Game.I?.Hero?.Class ?? ClassId.Emberblade;
        string key = Names.ClassKey(cls);
        _set = key is "hunter" ? "r_" : key is "astromancer" or "soulbinder" or "pactbinder" ? "c_" : "m_";
        Veto();
    }
    private void Veto() { var b = E.Brain; if (b != null) b.Veto = d => d.Id.Length > 2 && d.Id[1] == '_' && !d.Id.StartsWith(_set); }
    public override void Engage() { base.Engage(); Veto(); Mech("mirror"); E.Toast($"她用的是你的招式（{Names.ClassKey(Game.I?.Hero?.Class ?? ClassId.Emberblade)}）"); }
    internal override void Move(BossMoveDef d) { if (d.Id.StartsWith(_set)) Mech("mirror"); }
    public override Vector2? BotStand => _fire != null && _fire.Lit && E.HeroOk && E.HeroPos.DistanceTo(_fire.Pos) > 4f ? _fire.Pos : null;
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_fire == null) return;
        bool heroLit = _fire.Lit && E.HeroOk && E.HeroPos.DistanceTo(_fire.Pos) <= _fire.Radius;
        bool herLit = _fire.Lit && M.Pos2.DistanceTo(_fire.Pos) <= _fire.Radius;
        if ((heroLit || herLit) && (int)T != (int)(T - dt)) Mech("brazier");
        M.DrMul = heroLit ? 1.3f : 1f;
        M.DmgMul = herLit ? 1.3f : 1f;
        if (heroLit) World.P.Heal(World.P.MaxLife * 0.015f * dt);
        if (herLit) M.Life = Mathf.Min(M.MaxLife, M.Life + M.MaxLife * 0.004f * dt);
        // she fights for it: when the hero holds the light and she does not, she walks in
        if (heroLit && !herLit && Free && (int)(T / 6f) != (int)((T - dt) / 6f)) Take(1.5f);
        if (Holding && heroLit && !herLit) E.Drive(M, _fire.Pos, dt, 1.2f, 2f);
        if (_dark) NamedOverlay.Hole(0.32f);
    }
}

/// <summary>醒寂・奧薩洛斯: the dark — a ring of light round the hero shrinks while he lives and grows with every stagger; Extinguish twice a phase with a moving safe zone; phase 3 echoes one signature move from each of the six rungs below, in order; his hush drains resource, refilled only by the embers his fallen knights drop.</summary>
internal sealed class PinOsaros : PinScript
{
    private float _hole = 0.5f, _ext = 6f, _echo = 4f; private int _extN, _phase = 1, _echoI; private bool _stag;
    private float _wedge = -1f, _wedgeA; private Monster _pointer; private float _cone;
    public override void Setup()
    {
        _hole = 0.5f; _ext = 6f; _extN = 0; _phase = 1; _echoI = 0;
        var b = E.Brain; if (b != null) b.Veto = d => d.Id == "extinguish";
        _pointer = E.Prop("", E.Center, 1e9f, null, null, 0.1f, 0.1f); _pointer.Untargetable = true;
    }
    public override void Engage() { base.Engage(); var b = E.Brain; if (b != null) b.Veto = d => d.Id == "extinguish"; Mech("dark"); NamedOverlay.Set(Colors.Black, 0.9f, _hole); }
    public override void Phase(int n) { _phase = n; _extN = 0; _ext = 4f; }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        _hole = Mathf.Max(0.13f, _hole - dt * 0.0035f);
        bool st = M.Staggered;
        if (st && !_stag) { _hole = Mathf.Min(0.5f, _hole + 0.12f); Fx.LightFlash(M.Chest, Palette.Ember, 4f, 20f, 0.4f); }
        _stag = st;
        NamedOverlay.Hole(_hole);
        if ((int)(T / 10f) != (int)((T - dt) / 10f)) Mech("dark");
        if (_phase >= 2 && E.HeroOk) E.Drain(0.015f * dt);
        _ext -= dt;
        if (_ext <= 0f && _extN < 2 && _phase >= 1) { _extN++; _ext = 20f; Extinguish(); }
        if (_phase >= 3) { _echo -= dt; if (_echo <= 0f && Free) { _echo = 7f; Echo(); } }
        if (_wedge >= 0f)
        {
            _wedge -= dt; _wedgeA += dt * 0.5f;
            if (_pointer != null && GodotObject.IsInstanceValid(_pointer))
            {
                _pointer.Pos2 = M.Pos2; _pointer.Facing = Mathf.Pi * 0.5f - _wedgeA; _pointer.Rotation = new Vector3(0, _pointer.Facing, 0);
                _cone -= dt; if (_cone <= 0f) { _cone = 0.5f; E.Track(Fx.TelegraphCone(_pointer, E.ArenaR + 4f, 90f, 0.5f, new Color(0.15f, 0.12f, 0.3f, 0.6f))); }
            }
            if ((int)(_wedge) != (int)(_wedge + dt) && E.HeroOk)
            {
                Vector2 d = E.HeroPos - M.Pos2; float a = Mathf.Wrap(Mathf.Atan2(d.Y, d.X) - _wedgeA, -Mathf.Pi, Mathf.Pi);
                if (Mathf.Abs(a) > Mathf.Pi / 4f) E.Hit(World.P, 0.3f, Element.Fire);
            }
        }
    }
    private void Extinguish()
    {
        Mech("extinguish");
        Take(4f);
        Anim("Spell_Simple_Shoot", 0.5f);
        Vector2 safe = E.Near(E.Center, 4f, Mathf.Min(E.ArenaR - 3f, 9f));
        Vector2 drift = Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau));
        var ring = E.Mark(safe, 4.5f, Palette.Ember, 4f);
        E.Warn("熄滅將臨——走進移動的火圈！");
        E.Run(3.5f, k => { safe = E.Walk(safe + drift * (1.2f / 60f)); if (GodotObject.IsInstanceValid(ring)) ring.GlobalPosition = World.To3(safe); }, () =>
        {
            if (!E.Alive || !E.HeroOk) return;
            Fx.Shake(0.5f, 0.5f);
            if (E.HeroPos.DistanceTo(safe) > 4.5f) E.Hit(World.P, 2.6f, Element.Shadow);
            foreach (var mi in World.Minions.ToArray()) if (mi != null && GodotObject.IsInstanceValid(mi) && mi.Pos2.DistanceTo(safe) > 4.5f) E.Hit(mi, 2.6f, Element.Shadow);
        });
    }
    private void Echo()
    {
        int k = _echoI++ % 6;
        Mech("echo");
        Log.Info("named", $"pin_8_osaros echo {k + 1}/6");
        switch (k)
        {
            case 0: E.Toast("回響：掘墓者"); for (int i = 0; i < 4; i++) { Vector2 at = E.Near(E.HeroPos, 0f, 4f); E.Blast(at, 1.9f, 1f + i * 0.3f, 1f, Element.Shadow, St.Slow(0.4f, 2f), () => Pit(at, 1.6f)); } break;
            case 1: E.Toast("回響：霜骸領主"); E.Waves(M.Pos2, 3, 2f, 3f, 1f, new[] { 0.7f }, 1f, Element.Frost, St.Chill(0.6f, 3f)); break;
            case 2: E.Toast("回響：腐骨主教"); PrisonOnTrail(1f); break;
            case 3: E.Toast("回響：沼母"); E.LineBlast(M.Pos2, ToHero, 30f, 1.8f, 1.2f, 0.6f, Element.Poison, null, () => { if (E.HeroOk && E.HeroInLane(M.Pos2, ToHero, 30f, 1.8f)) { E.Drag(M.Pos2, 2f); E.After(0.6f, () => E.Blast(M.Pos2, 4.6f, 0.6f, 1.8f, Element.Poison)); } }); break;
            case 4: E.Toast("回響：鏽砂暴君——站進陰影"); _wedge = 7f; _wedgeA = Mathf.Atan2(E.HeroPos.Y - M.Pos2.Y, E.HeroPos.X - M.Pos2.X) + 1.2f; break;
            case 5: E.Toast("回響：寂滅大導師"); for (int t = 0; t < 3; t++) { int tt = t; E.After(tt * 1.2f, () => { if (E.Alive) E.Waves(M.Pos2, 3, 3.6f, 3.2f, 0.7f, new[] { 0.6f }, 1f, Element.Shadow); }); } break;
        }
    }
    public override void Killed(Monster m)
    {
        if (m.Def.Id != "knight" || m == M) return;
        Vector2 at = World.Grid?.NearestWalk(m.Pos2) ?? m.Pos2;
        Mech("ember");
        WorldObjects.EmberShard(at, () => { if (E.HeroOk) World.P.Resource = Mathf.Min(World.P.Hero.MaxResource, World.P.Resource + World.P.Hero.MaxResource * 0.35f); });
    }
}
