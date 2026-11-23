using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>The four world bosses (BESTIARY.md 4.5): timed world events (see <see cref="WorldBossEvent"/>), each fought in its own field.</summary>
public static partial class NamedDb
{
    static partial void RegisterWorld(Registry r)
    {
        BossKits.Register(NK.Kit("wb_barrowbearer", "葬列", Element.Physical,
            NK.Swing("mandible", 7.5f, 110f, Element.Physical, St.Bleed(0.3f, 3f), "Sword_Attack"),
            NK.Cone("chapel_sweep", 9f, 9f, 150f, 1.4f, Element.Physical, St.Knock(10f), maxRange: 10f),
            NK.Charge("carapace_rush", 13f, 18f, 5f, 1.5f, Element.Physical, St.Knock(9f)),
            NK.Slam("bell_quake", 14f, 6f, 1.4f, Element.Physical, 2, St.Stun(0.5f), 2, 12f)));
        r.Add("wb_barrowbearer", () => new BarrowBearer(), "interment coffins procession ?opened");

        BossKits.Register(NK.Kit("wb_firstbearer", "負山者", Element.Fire,
            NK.Swing("hod_fist", 7.5f, 110f, Element.Physical, St.Knock(6f), "Sword_Attack"),
            NK.Slam("stamp", 11f, 6f, 1.4f, Element.Physical, 1, St.Stun(0.4f), 1, 10f)));
        r.Add("wb_firstbearer", () => new FirstBearer(), "trunks bricks kneel gather ?straps ?carried");

        BossKits.Register(NK.Kit("wb_daylessdisc", "無晝日輪", Element.Lightning,
            NK.Bolt("sun_arc", Element.Lightning, 16f, 0.9f),
            NK.Fan("ring_volley", 8f, 7, 12f, 12f, 0.8f, Element.Lightning)).Still().Caster(0f, 40f));
        r.Add("wb_daylessdisc", () => new DaylessDisc(), "eclipse jar dawn ?discharge ?sheltered");

        BossKits.Register(NK.Kit("wb_breathofsilence", "寂主之息", Element.Shadow,
            NK.Swing("coil_lash", 8f, 120f, Element.Shadow),
            NK.Lines("white_breath", 10f, 3, 30f, 22f, 2.6f, 1.2f, Element.Shadow),
            NK.Charge("ribbon_surge", 14f, 20f, 4f, 1.4f, Element.Shadow)).Caster(4f, 14f));
        r.Add("wb_breathofsilence", () => new BreathOfSilence(), "quiet coil swallow ?brazier ?weakpoint");
    }
}

/// <summary>負陵母蟲・葬列: Interment (three marked zones collapse into pits that stay), Coffin-fall (six to ten coffins that open after 10 s unless smashed), the Procession (burrowed, following the party for four tolls, erupting under it).</summary>
internal sealed class BarrowBearer : BossScript
{
    private int _pits;
    public override void Build()
    {
        E.Every(17f, Interment, 6f, () => Free);
        E.Every(24f, Coffins, 12f, () => Free);
        E.Every(40f, Procession, 26f, () => Free);
    }
    public override void Setup() { _pits = 0; }
    /// <summary>Hint: break the coffins near the hero before they open (each holds two or three of the dead).</summary>
    public override Monster BotTarget
    {
        get
        {
            if (!E.HeroOk) return null;
            var c = E.NearestProp(E.HeroPos, p => p.DisplayName == "棺木");
            return c != null && c.Pos2.DistanceTo(E.HeroPos) < 7f ? c : null;
        }
    }
    private void Interment()
    {
        Mech("interment");
        Take(1.2f);
        Anim("Spell_Simple_Enter", 0.7f);
        for (int i = 0; i < 3 && _pits < 9; i++)
        {
            Vector2 at = i == 0 ? E.HeroPos : E.Near(E.Center, 3f, E.ArenaR - 2f);
            _pits++;
            E.Blast(at, 3.6f, 2f, 1.8f, Element.Physical, St.Root(2f), () =>
            {
                E.Own(NamedProps.Place(NamedProps.Patch(3.4f, new Color(0.08f, 0.07f, 0.06f)), at));
                E.Zone(at, 3.3f, 9999f, 0.08f, Element.Physical, St.Slow(0.5f, 1f));
            });
        }
        E.Toast("地面被掏空了！");
    }
    private void Coffins()
    {
        Mech("coffins");
        Take(1.6f);
        Anim("Sword_Attack", 0.6f);
        int n = 6 + Mathf.RoundToInt((1f - E.LifeFrac) * 4f);
        for (int i = 0; i < n; i++)
        {
            Vector2 at = E.Near(E.HeroPos, 1.5f, 9f);
            E.Blast(at, 1.6f, 1.3f + i * 0.08f, 1f, Element.Physical, St.Stun(0.4f), () =>
            {
                if (!E.Alive) return;
                Monster coffin = null;
                coffin = E.Prop("棺木", at, M.MaxLife * 0.008f, NamedProps.Coffin(true), null, 0.7f, 2f);
                E.After(10f, () =>
                {
                    if (coffin == null || !GodotObject.IsInstanceValid(coffin) || coffin.Dead || !E.Alive) return;
                    Mech("opened");
                    E.RemoveProp(coffin);
                    bool low = E.LifeFrac < 0.35f;
                    for (int k = 0; k < (low ? 3 : 2); k++) E.Summon(low ? "sexton_grub" : "skeleton", E.Near(at, 0.5f, 1.8f), low ? "husk" : null, true, low ? 0.4f : 1f);
                });
            }, true);
        }
    }
    private void Procession()
    {
        Mech("procession");
        Take(8.5f);
        E.Hide(M, true);
        Fx.Dust(M.GlobalPosition);
        E.Toast("葬列鑽入地底——鐘聲四響之後，牠會在你腳下破土！");
        var mound = E.Own(NamedProps.Place(NamedProps.Heap(new Color(0.3f, 0.25f, 0.2f), 1.8f), M.Pos2));
        for (int toll = 1; toll <= 4; toll++)
        {
            int t = toll;
            E.Then(t * 1.7f, () =>
            {
                Snd.At("bell_toll", M.GlobalPosition, 0.8f + t * 0.1f);
                if (t < 4) return;
                Vector2 at = E.HeroPos;
                E.Blast(at, 5f, 1f, 2f, Element.Physical, St.Knock(12f), () =>
                {
                    if (!E.Alive) return;
                    M.Pos2 = E.Walk(at); E.Hide(M, false);
                    if (GodotObject.IsInstanceValid(mound)) mound.QueueFree();
                    E.RingBlast(at, 5f, 9f, 0.6f, 1f, Element.Physical);
                });
            });
        }
        E.Run(6.8f, k => { if (E.Alive && E.HeroOk) { M.Pos2 = E.Walk(M.Pos2 + (E.HeroPos - M.Pos2).LimitLength(0.11f)); if (GodotObject.IsInstanceValid(mound)) mound.GlobalPosition = M.GlobalPosition; } });
    }
    public override void Reset() { base.Reset(); E.Hide(M, false); }
}

/// <summary>首役・負山者: stoking throws (trunks rolled down 30 m lanes, smouldering peat-bricks); the straps of the hod are exposed 6 s whenever he kneels — break both and the beacon-tower falls along a marked line (himself included) and he is staggered; Gathering: a 180° sweep that puts the hero in the hod until the cage door breaks (5 %) or 6 s pass.</summary>
internal sealed class FirstBearer : BossScript
{
    private readonly List<Monster> _straps = new();
    private float _hodBack = -1f; private bool _hod = true;
    private Monster _door; private float _carry;
    public override Monster BotTarget
    {
        get
        {
            if (_door != null && GodotObject.IsInstanceValid(_door) && !_door.Dead) return _door;
            return _straps.FirstOrDefault(s => s != null && GodotObject.IsInstanceValid(s) && !s.Dead);
        }
    }
    public override void Build()
    {
        E.Every(9f, Trunks, 4f, () => Free);
        E.Every(11f, Bricks, 7f, () => Free);
        E.Every(26f, Kneel, 14f, () => Free && _hod);
        E.Every(19f, Gather, 10f, () => Free && E.HeroDist < 10f);
    }
    public override void Setup() { _hod = true; _hodBack = -1f; _straps.Clear(); _door = null; _carry = 0f; }
    private void Trunks()
    {
        Mech("trunks");
        Take(1.1f);
        Anim("OverhandThrow", 0.8f);
        Vector2 h = E.HeroPos;
        for (int i = 0; i < 3; i++)
        {
            Vector2 dir = Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau));
            Vector2 from = E.Walk(h - dir * 15f + dir.Orthogonal() * (i - 1) * 5f);
            E.LineBlast(from, dir, 30f, 3f, 1.4f + i * 0.4f, 1.3f, Element.Physical, St.Knock(9f));
        }
    }
    private void Bricks()
    {
        Mech("bricks");
        Take(1f);
        Anim("OverhandThrow", 1f);
        for (int i = 0; i < 4; i++)
        {
            Vector2 at = E.Near(E.HeroPos, 0f, 6f);
            E.Blast(at, 2.6f, 1.2f + i * 0.2f, 1f, Element.Fire, St.Burn(0.4f, 3f), () => E.Zone(at, 2.4f, 12f, 0.12f, Element.Fire, St.Slow(0.3f, 1f)), true);
        }
    }
    private void Kneel()
    {
        Mech("kneel");
        Take(6f);
        Anim("Spell_Simple_Enter", 0.5f);
        E.Toast("他跪下撿柴——背帶露出來了！");
        Vector2 side = M.Forward.Orthogonal();
        for (int s = -1; s <= 1; s += 2)
        {
            var strap = E.Prop("背帶", E.Walk(M.Pos2 + side * s * 2.2f - M.Forward * 1.2f), M.MaxLife * 0.03f, NamedProps.Stake(NamedProps.Wood, 2.6f, Palette.Ember), StrapBroken, 0.6f, 2.6f);
            _straps.Add(strap);
        }
        E.After(6f, () => { foreach (var s in _straps.ToArray()) E.RemoveProp(s); _straps.Clear(); });
    }
    private void StrapBroken()
    {
        if (_straps.Any(s => s != null && GodotObject.IsInstanceValid(s) && !s.Dead)) return;
        Mech("straps");
        _straps.Clear();
        _hod = false; _hodBack = 40f;
        Vector2 dir = Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau));
        E.Toast("燈塔要倒了——避開那條線！");
        E.LineBlast(E.Walk(M.Pos2 - dir * 3f), dir, 26f, 5f, 2f, 3f, Element.Physical, St.Stun(1f), () => { E.HurtNamed(0.08f); E.Expose(M, 8f); });
    }
    private void Gather()
    {
        Mech("gather");
        Take(1.6f);
        Face();
        Anim("Sword_Attack", 0.7f);
        E.ConeBlast(ToHero, 8f, 180f, 1.2f, 0.8f, Element.Physical, null, hero =>
        {
            if (!hero || !E.HeroOk || World.P.Invulnerable) return;
            Mech("carried");
            _carry = 6f;
            _door = E.Prop("籠門", M.Pos2 + M.Forward * 1.5f, M.MaxLife * 0.05f, NamedProps.Cage(), () => { _door = null; _carry = 0f; if (E.HeroOk) { World.P.StTime[(int)StatusId.Stun] = 0f; World.P.Teleport(E.Walk(M.Pos2 + M.Forward * 3f)); } E.Toast("籠門碎了！"); }, 0.8f, 2.2f);
            E.Unpin(_door);
            E.Toast("被裝進了柴簍！打破籠門！");
        });
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (!_hod) { _hodBack -= dt; if (_hodBack <= 0f) { _hod = true; E.Toast("他又扛起了燈塔"); } }
        if (_carry <= 0f) return;
        _carry -= dt;
        if (!E.HeroOk) { _carry = 0f; return; }
        var p = World.P;
        p.StTime[(int)StatusId.Stun] = Mathf.Max(p.StTime[(int)StatusId.Stun], 0.3f); p.CcImmune = 0f;
        Vector2 hold = E.Walk(M.Pos2 - M.Forward * 0.8f);
        p.Pos2 = hold;
        if (_door != null && GodotObject.IsInstanceValid(_door)) E.MoveProp(_door, hold + M.Forward.Orthogonal() * 1.1f);
        if ((int)(_carry * 2f) != (int)((_carry + dt) * 2f)) E.Hit(p, 0.18f, Element.Fire);
        if (_carry > 0f) return;
        if (_door != null) { E.RemoveProp(_door); _door = null; }
        Vector2 zone = E.Near(E.Center, 6f, E.ArenaR - 2f);
        p.StTime[(int)StatusId.Stun] = 0f;
        p.Teleport(zone);
        E.Blast(zone, 3f, 0.6f, 1.4f, Element.Fire, St.Burn(0.5f, 3f), () => E.Zone(zone, 3f, 6f, 0.15f, Element.Fire));
    }
}

/// <summary>無晝日輪: a rotating 90° wedge of shadow (light burns everywhere else, speeding up and reversing); four storm-jars arm in turn (break the armed one within 10 s — out of turn it goes off at once); False Dawn every 60 s: shelter behind a fallen mirror-segment or be flashed and stunned 3 s.</summary>
internal sealed class DaylessDisc : BossScript
{
    private float _angle, _spin = 0.35f, _dir = 1f, _burn, _cone;
    private Monster _pointer;
    private readonly List<Monster> _jars = new(), _mirrors = new();
    private readonly Dictionary<Monster, int> _dawns = new();
    private int _armed = -1; private float _armT, _jarsBack = -1f;
    private float _dawn = 30f, _charge = -1f;
    public override Monster BotTarget => _armed >= 0 && _armed < _jars.Count && _jars[_armed] != null && GodotObject.IsInstanceValid(_jars[_armed]) && !_jars[_armed].Dead ? _jars[_armed] : null;
    public override Vector2? BotStand
    {
        get
        {
            if (_charge >= 0f) { var m = Shelter(); return m != null ? E.Walk(m.Pos2 + (m.Pos2 - M.Pos2).Normalized() * 1.4f) : null; }
            return E.HeroOk && !InShadow(E.HeroPos) ? E.Walk(M.Pos2 + Vector2.Right.Rotated(_angle) * 7f) : null;
        }
    }
    public override void Setup()
    {
        _jars.Clear(); _mirrors.Clear(); _dawns.Clear(); _armed = -1; _dawn = 30f; _charge = -1f;
        _pointer = E.Prop("", M.Pos2, 1e9f, null, null, 0.1f, 0.1f);
        _pointer.Untargetable = true; _pointer.Visible = true;
        Jars();
        for (int i = 0; i < 4; i++)
        {
            var m = E.Prop("鏡片殘骸", E.OnRing(i * Mathf.Tau / 4f + Mathf.Pi / 4f, Mathf.Min(E.ArenaR - 3f, 10f)), 1e9f, NamedProps.Slab(new Color(0.85f, 0.8f, 0.6f), 2.2f, 2.4f, 0.25f, true), null, 0.8f, 2.4f);
            m.Untargetable = true;
            _mirrors.Add(m); _dawns[m] = 0;
        }
    }
    private void Jars()
    {
        _jars.Clear();
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            var j = E.Prop($"雷甕・{"一二三四"[i]}", E.OnRing(i * Mathf.Tau / 4f, 4.5f), M.MaxLife * 0.03f, NamedProps.Jar(new Color(0.75f, 0.8f, 1f), 1f), () => JarBroken(k), 0.7f, 1.6f);
            E.Unpin(j);
            _jars.Add(j);
        }
        _armed = 0; _armT = 10f;
    }
    private void JarBroken(int k)
    {
        if (k != _armed) { Mech("discharge"); E.Toast("雷甕提前爆裂！"); Discharge(); }
        else if (_jars.Count > 0) E.Toast("雷甕碎了");
        _jars[k] = null;
        NextJar();
    }
    private void NextJar()
    {
        for (int i = 0; i < _jars.Count; i++) if (_jars[i] != null && GodotObject.IsInstanceValid(_jars[i]) && !_jars[i].Dead) { _armed = i; _armT = 10f; Mech("jar"); _jars[i].Title = "充能中"; return; }
        _armed = -1; _jarsBack = 20f;
    }
    private void Discharge()
    {
        Fx.Shake(0.4f, 0.4f);
        E.HitCircle(E.Center, E.ArenaR + 4f, 1.4f, Element.Lightning, St.Stun(0.6f));
        if (E.HeroOk) Fx.Beam(M.Chest, World.P.Chest, Element.Lightning, 0.3f, 0.3f);
    }
    private bool InShadow(Vector2 p)
    {
        Vector2 d = p - M.Pos2;
        if (d.LengthSquared() < 4f) return true;
        float a = Mathf.Wrap(Mathf.Atan2(d.Y, d.X) - _angle, -Mathf.Pi, Mathf.Pi);
        return Mathf.Abs(a) <= Mathf.Pi / 4f;
    }
    private Monster Shelter() => _mirrors.Where(m => m != null && GodotObject.IsInstanceValid(m) && !m.Dead).OrderBy(m => m.Pos2.DistanceTo(E.HeroPos)).FirstOrDefault();
    public override void Tick(float dt)
    {
        base.Tick(dt);
        // ── eclipse sweep ──
        _spin = Mathf.Min(0.9f, 0.35f + T * 0.004f);
        if ((int)(T / 25f) != (int)((T - dt) / 25f)) _dir = -_dir;
        _angle += _spin * _dir * dt;
        if (_pointer != null && GodotObject.IsInstanceValid(_pointer))
        {
            _pointer.Pos2 = M.Pos2;
            _pointer.Facing = Mathf.Pi * 0.5f - _angle; _pointer.Rotation = new Vector3(0, _pointer.Facing, 0);
            _cone -= dt;
            if (_cone <= 0f) { _cone = 0.5f; E.Track(Fx.TelegraphCone(_pointer, E.ArenaR + 4f, 90f, 0.5f, new Color(0.15f, 0.12f, 0.3f, 0.6f))); }
        }
        _burn -= dt;
        if (_burn <= 0f)
        {
            _burn = 1f;
            Mech("eclipse");
            if (E.HeroOk && !InShadow(E.HeroPos) && _charge < 0f) E.Hit(World.P, 0.22f, Element.Fire, St.Burn(0.15f, 1f));
        }
        // ── storm jars ──
        for (int i = 0; i < _jars.Count; i++)
        {
            var j = _jars[i];
            if (j == null || !GodotObject.IsInstanceValid(j) || j.Dead) continue;
            E.MoveProp(j, M.Pos2 + Vector2.Right.Rotated(T * 0.5f + i * Mathf.Tau / 4f) * 4.5f);
        }
        if (_armed >= 0)
        {
            _armT -= dt;
            if (_armT <= 0f) { Mech("discharge"); E.Toast("雷甕放電了！"); Discharge(); _armT = 10f; }
        }
        else if (_jarsBack > 0f) { _jarsBack -= dt; if (_jarsBack <= 0f) Jars(); }
        // ── false dawn ──
        if (_charge >= 0f)
        {
            _charge -= dt;
            if (_charge < 0f) Flash();
            return;
        }
        _dawn -= dt;
        if (_dawn <= 0f) { _dawn = 60f; Dawn(); }
    }
    private void Dawn()
    {
        Mech("dawn");
        _charge = 6f;
        E.Warn("偽曙將臨——躲到鏡片殘骸背後！");
        E.Track(Fx.TelegraphRing(M.Pos2, 0f, E.ArenaR + 4f, 6f, new Color(1f, 0.95f, 0.7f)));
        Snd.At("boss_roar", M.GlobalPosition, 0.8f);
    }
    private void Flash()
    {
        Fx.LightFlash(M.Chest + Vector3.Up * 4f, new Color(1f, 0.95f, 0.75f), 12f, 60f, 0.5f);
        foreach (var m in _mirrors.ToArray())
        {
            if (m == null || !GodotObject.IsInstanceValid(m)) continue;
            if (++_dawns[m] >= 2) { E.RemoveProp(m); _mirrors.Remove(m); }
        }
        if (!E.HeroOk) return;
        var p = World.P.Pos2;
        bool safe = false;
        foreach (var m in _mirrors.Concat(_dawns.Keys).Distinct())
        {
            if (m == null || !GodotObject.IsInstanceValid(m)) continue;
            Vector2 toM = (m.Pos2 - M.Pos2).Normalized();
            if (p.DistanceTo(m.Pos2) < 2.6f && (p - m.Pos2).Dot(toM) > 0.1f) { safe = true; break; }
        }
        if (safe) { Mech("sheltered"); return; }
        E.Hit(World.P, 2f, Element.Fire, St.Stun(3f));
        E.Toast("被偽曙的光吞沒了");
    }
}

/// <summary>寂主之息: the Quiet (silence zones spread from its coils: no resource regeneration, fire goes out; four braziers can be relit, 2 s, to burn a zone back for 20 s); Coil (a ring that tightens over 10 s — break the glowing segment, 6 %, or be crushed); Swallowed sound (4 s inhale of every projectile, ground effect and summon attack, then a white cone sized by what it ate).</summary>
internal sealed class BreathOfSilence : BossScript
{
    private readonly List<(Vector2 at, float left, Node3D n)> _quiet = new();
    private readonly List<LightSpot> _braziers = new();
    private float _chan; private LightSpot _chanOn;
    private float _coil = -1f; private Vector2 _coilC; private Monster _weak; private readonly List<Node3D> _coilPosts = new();
    private float _swallow = -1f; private int _eaten; private float _lastRes = -1f;
    public override Monster BotTarget => _weak != null && GodotObject.IsInstanceValid(_weak) && !_weak.Dead ? _weak : null;
    public override Vector2? BotStand
    {
        get
        {
            if (_swallow >= 0f) return E.Walk(M.Pos2 - M.Forward * 4f);
            if (_chanOn != null) return _chanOn.Pos;
            var unlit = _braziers.Where(b => !b.Lit).OrderBy(b => b.Pos.DistanceTo(E.HeroPos)).FirstOrDefault();
            return unlit != null && InQuiet(E.HeroPos) ? unlit.Pos : null;
        }
    }
    public override void Build()
    {
        E.Every(9f, Quiet, 3f);
        E.Every(30f, Coil, 16f, () => Free && _coil < 0f);
        E.Every(34f, Swallow, 24f, () => Free && _coil < 0f);
    }
    public override void Setup()
    {
        _quiet.Clear(); _braziers.Clear(); _coil = -1f; _swallow = -1f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 at = E.OnRing(i * Mathf.Tau / 4f + Mathf.Pi / 4f, Mathf.Min(E.ArenaR - 2f, 11f));
            var n = E.Own(NamedProps.Place(NamedProps.Brazier(false, 1.3f), at));
            _braziers.Add(E.Light(at, 8f, false, n));
        }
    }
    private bool InQuiet(Vector2 p) => _quiet.Any(q => q.at.DistanceTo(p) <= 5f);
    private void Quiet()
    {
        Mech("quiet");
        Vector2 at = E.Near(M.Pos2, 2f, 9f);
        if (_braziers.Any(b => b.Lit && b.Pos.DistanceTo(at) < 8f)) return;      // burnt back
        var n = E.Mark(at, 5f, NamedProps.Hush, 20f);
        _quiet.Add((at, 20f, n));
    }
    private void Coil()
    {
        Mech("coil");
        _coil = 10f; _coilC = E.HeroPos;
        E.Toast("白蚺盤成一圈——打斷發光的那一節！");
        _coilPosts.Clear();
        for (int i = 0; i < 14; i++) _coilPosts.Add(E.Own(NamedProps.Place(NamedProps.Column(NamedProps.Pale, 0.6f, 1.6f), _coilC + Vector2.Right.Rotated(i * Mathf.Tau / 14f) * 9f)));
        float a = Rng.Range(0f, Mathf.Tau);
        _weak = E.Prop("發光的鱗節", E.Walk(_coilC + Vector2.Right.Rotated(a) * 9f), M.MaxLife * 0.06f, NamedProps.Column(new Color(1f, 0.95f, 0.85f), 0.75f, 1.9f, true), () =>
        {
            Mech("weakpoint");
            _weak = null; _coil = -1f; ClearCoil();
            E.Expose(M, 6f);
            E.Toast("鱗節碎裂——盤繞散開了！");
        }, 0.9f, 1.9f);
        E.Unpin(_weak);
    }
    private void ClearCoil() { foreach (var n in _coilPosts) if (GodotObject.IsInstanceValid(n)) n.QueueFree(); _coilPosts.Clear(); if (_weak != null) { E.RemoveProp(_weak); _weak = null; } }
    private void Swallow()
    {
        Mech("swallow");
        Take(4.6f);
        Anim("Spell_Simple_Enter", 0.5f);
        _swallow = 4f; _eaten = 0;
        E.Toast("萬籟被牠吸入——停手，或繞到牠背後");
        E.Mark(M.Pos2, 3f, Colors.White, 4f);
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        // the Quiet: no regeneration, fire goes out
        for (int i = _quiet.Count - 1; i >= 0; i--)
        {
            var q = _quiet[i]; q.left -= dt; _quiet[i] = q;
            bool burnt = _braziers.Any(b => b.Lit && b.Pos.DistanceTo(q.at) < 8f);
            if (q.left <= 0f || burnt) { if (GodotObject.IsInstanceValid(q.n)) q.n.QueueFree(); _quiet.RemoveAt(i); }
            else NamedHub.SnuffHeroZones(q.at, 5f, Element.Fire);
        }
        if (E.HeroOk)
        {
            var p = World.P;
            if (InQuiet(p.Pos2)) { if (_lastRes >= 0f && p.Resource > _lastRes) p.Resource = _lastRes; _lastRes = p.Resource; } else _lastRes = -1f;
            // relighting a brazier: stand at it for 2 s
            var b = _braziers.FirstOrDefault(x => !x.Lit && x.Pos.DistanceTo(p.Pos2) < 2.2f);
            if (b != null && !p.Busy) { if (_chanOn != b) { _chanOn = b; _chan = 0f; } _chan += dt; if (_chan >= 2f) { Mech("brazier"); E.SetLit(b, true); E.After(20f, () => E.SetLit(b, false)); _chanOn = null; E.Toast("火盆重燃——寂靜退去"); } }
            else { _chanOn = null; _chan = 0f; }
        }
        // the coil
        if (_coil >= 0f)
        {
            _coil -= dt;
            float r = Mathf.Lerp(2.2f, 9f, Mathf.Clamp(_coil / 10f, 0f, 1f));
            for (int i = 0; i < _coilPosts.Count; i++) if (GodotObject.IsInstanceValid(_coilPosts[i])) _coilPosts[i].GlobalPosition = World.To3(_coilC + Vector2.Right.Rotated(i * Mathf.Tau / _coilPosts.Count + T * 0.2f) * r);
            if (_weak != null && GodotObject.IsInstanceValid(_weak)) E.MoveProp(_weak, E.Walk(_coilC + Vector2.Right.Rotated(T * 0.2f) * r));
            if (E.HeroOk && E.HeroPos.DistanceTo(_coilC) > r - 0.5f) World.P.Pos2 = E.Walk(_coilC + (E.HeroPos - _coilC).LimitLength(r - 0.6f));
            if (_coil < 0f) { ClearCoil(); if (E.HeroOk && E.HeroPos.DistanceTo(_coilC) < 4f) { E.Hit(World.P, 2.6f, Element.Shadow, St.Stun(1f)); E.Toast("被絞緊了"); } }
        }
        // swallowed sound
        if (_swallow >= 0f)
        {
            _swallow -= dt;
            foreach (var s in NamedHub.AllShots().ToArray())
            {
                Vector2 sp = NamedHub.ShotPos(s);
                if (sp.DistanceTo(M.Pos2) < 2.5f) { NamedHub.Kill(s); _eaten++; } else NamedHub.SetShotDir(s, M.Pos2 - sp);
            }
            _eaten += NamedHub.SnuffHeroZones(null, 0f);
            foreach (var mi in World.Minions.ToArray()) if (mi != null && GodotObject.IsInstanceValid(mi) && !mi.Dead && mi.Pos2.DistanceTo(M.Pos2) < 6f && mi.Attacking) { _eaten++; mi.Busy(0.6f, null); }
            if (_swallow < 0f)
            {
                float k = Mathf.Clamp(1f + _eaten * 0.15f, 1f, 3f);
                E.ConeBlast(ToHero, 10f + 4f * k, Mathf.Min(150f, 60f + 25f * k), 0.8f, 1.1f * k, Element.Shadow, St.Stun(0.6f));
                Log.Info("named", $"wb_breathofsilence exhaled what it ate: {_eaten} (×{k:0.0})");
            }
        }
    }
    public override void Reset() { base.Reset(); ClearCoil(); }
}
