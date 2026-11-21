using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>The 28 rare elites (BESTIARY.md 4.1). Each keeps its base creature's own AI and two elite affixes; the script adds its signature mechanics.</summary>
public static partial class NamedDb
{
    static partial void RegisterRares(Registry r)
    {
        // ── 灰燼荒原 ──
        r.Add("rare_wastes_oldhowl", () => new OldHowl(), "howl ?flee");
        r.Add("rare_wastes_nightwatch", () => new NightWatch(), "inhale wail rise ?interrupted");
        r.Add("rare_wastes_ashbride", () => new AshBride(), "pile reform ashfall");
        r.Add("rare_wastes_gallowsfather", () => new GallowsFather(), "noose ?hoist ?cages");
        // ── 霜脊隘口 ──
        r.Add("rare_frost_hamo", () => new Hamo(), "blast muster ?freeze");
        r.Add("rare_frost_whiteshadow", () => new WhiteShadow(), "stealth decoys pounce ?reveal", n => n.Hp = 10f);
        r.Add("rare_frost_lastrun", () => new LastRun(), "charge keg deathburst");
        r.Add("rare_frost_thousandyear", () => new ThousandYear(), "shield pull shatter ?drain");
        // ── 沉眠墓原 ──
        r.Add("rare_crypt_belak", () => new Belak(), "wall open");
        r.Add("rare_crypt_veilwife", () => new VeilWife(), "drop web cocoon ?bite");
        r.Add("rare_crypt_namelessking", () => new NamelessKing(), "slam rise ?backblock");
        r.Add("rare_crypt_hundredtears", () => new HundredTears(), "fear split ghosts", n => n.Hp = 10f);
        // ── 瘴泣沼澤 ──
        r.Add("rare_mire_olddrum", () => new OldDrum(), "tongue ?swallow ?belly");
        r.Add("rare_mire_guidelight", () => new GuideLight(), "lead ?ambush ?turn ?early");
        r.Add("rare_mire_weepingriver", () => new WeepingRiver(), "submerge inhale weepers ?feed");
        r.Add("rare_mire_dafu", () => new Dafu(), "nets ?haul ?sleepers ?renet");
        // ── 鏽砂廢都 ──
        r.Add("rare_sands_ogan", () => new Ogan(), "bash ?reflect ?overheat ?behind");
        r.Add("rare_sands_hundredshell", () => new HundredShell(), "moult rearmour ?denied");
        r.Add("rare_sands_tesa", () => new Tesa(), "strike chain jar ?lastjar");
        r.Add("rare_sands_thirteenth", () => new Thirteenth(), "disc orbit ?drop");
        // ── 寂滅聖城 ──
        r.Add("rare_sanctum_firstcantor", () => new FirstCantor(), "rest halo ?blink ?broken");
        r.Add("rare_sanctum_everquiet", () => new Everquiet(), "toll strip ?fall");
        r.Add("rare_sanctum_kolen", () => new Kolen(), "sprint trail ?ignite ?quench");
        r.Add("rare_sanctum_vesna", () => new Vesna(), "snuff hushzone ?cone");
        // ── 燼淵 ──
        r.Add("rare_abyss_foreman", () => new Foreman(), "bell shock ?standup");
        r.Add("rare_abyss_firstborn", () => new FirstBorn(), "summon home ?eruption");
        r.Add("rare_abyss_whitetide", () => new WhiteTide(), "absorb release walkers");
        r.Add("rare_abyss_slagferry", () => new SlagFerry(), "volley wade ?island");
    }
}

/// <summary>Helpers shared by the rare elites (they all keep their own AI; the script borrows the body for its specials).</summary>
public abstract class RareScript : NamedScript
{
    protected float T;
    public override void Tick(float dt) { T += dt; }
    public override void Engage() { T = 0f; }
    protected bool Busy => M.Attacking;
    protected Vector2 ToHero => E.DirToHero(M.Pos2);
    protected void Face() { if (E.HeroOk) M.FaceDir(E.HeroPos - M.Pos2); }
    protected static StatusApply? S(StatusApply s) => s;
}

// ═════════════════════════ 灰燼荒原 wastes ═════════════════════════

/// <summary>老嚎: the pack-father howls at 75/50/25 % (pack enrages, four hounds run in); dying with four or more hounds alive, the pack flees with the spoils.</summary>
internal sealed class OldHowl : RareScript
{
    private const string Hound = "dusk_hound", HoundAlt = "rime_stalker";
    public override void Build() => E.AtLife(Howl, 0.75f, 0.5f, 0.25f);
    public override void Setup()
    {
        for (int i = 0; i < 3; i++) E.Summon(Hound, E.Near(E.Home, 2f, 4f), HoundAlt, false, 1f).Pack = M.Pack;
    }
    private void Howl()
    {
        Mech("howl");
        E.Cast(M, 0.9f, () =>
        {
            Fx.Ring(M.GlobalPosition + Vector3.Up * 0.3f, 30f, Palette.Element(Element.Fire), 0.8f);
            Snd.At("boss_roar", M.GlobalPosition, 0.9f);
            foreach (var m in E.HostilesIn(M.Pos2, 30f)) { m.Enrage(8f); Fx.BuffAura(m, Element.Fire, 8f, false); }
            E.RunIn(Hound, 4, HoundAlt, m => m.Enrage(8f));
            E.Toast("老嚎長嚎——群犬應聲而至！");
        }, "Spell_Simple_Enter");
    }
    public override void Death()
    {
        int alive = E.AliveAdds();
        if (alive < 4) return;
        Mech("flee");
        E.HalfLoot = true; E.KillAddsOnDeath = false;
        foreach (var a in E.Adds.ToArray())
        {
            if (a == null || !GodotObject.IsInstanceValid(a) || a.Dead) continue;
            a.NoLoot = true;
            Vector2 away = a.Pos2 + (a.Pos2 - E.HeroPos).Normalized() * 30f;
            a.Busy(3f, () => { if (GodotObject.IsInstanceValid(a)) { World.Monsters.Remove(a); a.QueueFree(); } }, dt => { a.Move((away - a.Pos2).Normalized() * 9f * dt); a.Model?.Play(a.Def.Run, 1.2f, 0.1f); });
        }
        Game.I?.Hud?.Toast("群犬叼起戰利品四散奔逃——下次先收拾群犬", Palette.Bad);
    }
}

/// <summary>守夜人・朵恩: a visible 1.2 s inhale, then a 12 m wail (fear 1.5 s) that pulls three husks out of the ground; a stun (or a heavy blow) during the inhale stops it.</summary>
internal sealed class NightWatch : RareScript
{
    private bool _inhaling; private float _hurt;
    public override void Build() => E.Every(9f, Inhale, 4f, () => !Busy && E.HeroDist < 16f);
    private void Inhale()
    {
        Mech("inhale");
        _inhaling = true; _hurt = 0f;
        Face();
        M.Busy(1.25f, null);
        M.Model?.Play("Spell_Simple_Enter", 0.8f, 0.1f, true);
        E.Track(Fx.TelegraphRing(M.Pos2, 0f, 12f, 1.2f, Palette.Danger, M.Def.Element));
        Fx.Glow(M.Chest + Vector3.Up * 0.4f, new Color(0.85f, 0.9f, 1f), 0.8f, 1.2f);
        E.Then(1.2f, Wail);
    }
    public override void Damaged(Hit h, float amount) { if (_inhaling) _hurt += amount; }
    private void Wail()
    {
        _inhaling = false;
        if (M.Stunned || _hurt >= M.MaxLife * 0.08f) { Mech("interrupted"); E.Toast("呼喊被打斷了"); Fx.Burst(M.Chest, Element.Lightning, 2f, 0.6f); return; }
        Mech("wail");
        Snd.At("bell_toll", M.GlobalPosition, 0.9f);
        Fx.Ring(M.GlobalPosition + Vector3.Up * 0.2f, 12f, new Color(0.8f, 0.85f, 1f), 0.6f, Sigil.Arcane);
        E.HitCircle(M.Pos2, 12f, 0.5f, Element.Frost, St.Fear(1.5f));
        Mech("rise");
        for (int i = 0; i < 3; i++) E.SummonAfter(0.8f, "husk", E.Near(M.Pos2, 3f, 9f));
    }
}

/// <summary>灰燼新娘: reforms three times; each pile lasts 3 s and must be struck, and every reform leaves an ash-fall (weaken, slow).</summary>
internal sealed class AshBride : RareScript
{
    private int _reforms;
    private Monster _pile;
    public override void Setup() { _reforms = 0; E.Floor = 0.01f; }
    public override void Reset() { _pile = null; }
    public override Monster BotTarget => _pile != null && GodotObject.IsInstanceValid(_pile) && !_pile.Dead && _reforms >= 1 ? _pile : null;
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_pile == null && E.Floor > 0f && M.Life <= M.MaxLife * 0.011f) Collapse();
    }
    private void Collapse()
    {
        Mech("pile");
        E.Hide(M, true);
        M.Busy(3.2f, null);
        Vector2 at = M.Pos2;
        _pile = E.Prop("灰堆", at, M.MaxLife * 0.07f, NamedProps.Heap(new Color(0.55f, 0.53f, 0.5f), 1.1f), () =>
        {
            _pile = null;
            E.Toast("灰堆被打散——新娘不再歸來");
            E.KillNamed();
        }, 0.9f, 1f);
        E.Track(Fx.Telegraph(at, 1.4f, 3f, Palette.Danger, M.Def.Element));
        E.After(3f, () =>
        {
            if (_pile == null || !E.Alive) return;
            E.RemoveProp(_pile); _pile = null;
            _reforms++;
            Mech("reform");
            E.Hide(M, false);
            M.Life = M.MaxLife * 0.4f;
            Fx.Burst(M.Chest, Element.Shadow, 3f, 1f);
            Mech("ashfall");
            E.Zone(at, 4f, 8f, 0.05f, Element.Physical, St.Weaken(0.25f, 2f));
            E.Zone(at, 4f, 8f, 0.0f, Element.Physical, St.Slow(0.35f, 1.5f));
            E.Toast(_reforms >= 3 ? "她最後一次從灰中站起" : "灰燼重新聚成了她");
            if (_reforms >= 3) E.Floor = 0f;
        });
    }
}

/// <summary>絞林之父: three noose-lines in a fan (a caught hero is hoisted 2 s, freed by dealing 15 % of his life); set alight, he drops three burning gibbet cages.</summary>
internal sealed class GallowsFather : RareScript
{
    private float _hoistDmg = -1f, _cageCd;
    public override void Build() => E.Every(7f, Nooses, 3f, () => !Busy && E.HeroDist < 14f);
    private void Nooses()
    {
        Mech("noose");
        Face();
        M.Busy(1.1f, null);
        M.Model?.Play("Spell_Simple_Shoot", 0.7f, 0.1f, true);
        Vector2 from = M.Pos2, dir = ToHero;
        bool caught = false;
        E.LaneFan(from, dir, 3, 40f, 13f, 1.6f, 1f, 0.6f, Element.Physical, null, k =>
        {
            float a = Mathf.DegToRad(-20f + 20f * k);
            if (!caught && E.HeroInLane(from, dir.Rotated(a), 13f, 1.6f)) { caught = true; Hoist(); }
        });
    }
    private void Hoist()
    {
        if (!E.HeroOk) return;
        Mech("hoist");
        var p = World.P;
        p.StTime[(int)StatusId.Root] = Mathf.Max(p.StTime[(int)StatusId.Root], 2f);
        p.CcImmune = 0f;
        _hoistDmg = 0f;
        E.Toast("絞索收緊！打他（其生命 15%）掙脫");
        E.Run(2f, k => { if (_hoistDmg >= 0f && E.HeroOk && Mathf.FloorToInt(k * 8f) != Mathf.FloorToInt((k - 0.02f) * 8f)) E.Hit(World.P, 0.12f, Element.Physical); }, () => _hoistDmg = -1f);
    }
    public override void Damaged(Hit h, float amount)
    {
        if (_hoistDmg >= 0f)
        {
            _hoistDmg += amount;
            if (_hoistDmg >= M.MaxLife * 0.15f && E.HeroOk) { World.P.StTime[(int)StatusId.Root] = 0f; _hoistDmg = -1f; E.Toast("繩斷了"); }
        }
        bool fire = h.El == Element.Fire || M.Has(StatusId.Burn);
        if (fire && _cageCd <= 0f)
        {
            _cageCd = 9f;
            Mech("cages");
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = E.Near(E.HeroPos, 0.5f, 4.5f);
                E.Blast(at, 2.2f, 1.6f + i * 0.35f, 0.9f, Element.Fire, St.Burn(0.4f, 3f), () => E.Zone(at, 2f, 5f, 0.12f, Element.Fire, St.Burn(0.3f, 2f)), true);
            }
        }
    }
    public override void Tick(float dt) { base.Tick(dt); if (_cageCd > 0f) _cageCd -= dt; }
}

// ═════════════════════════ 霜脊隘口 frost ═════════════════════════

/// <summary>無主號角・哈莫: a 14 m blast cone that freezes in two ticks; every blast musters four Frostbound in a line across the far end that advance in step.</summary>
internal sealed class Hamo : RareScript
{
    public override void Build() => E.Every(10f, Blast, 3f, () => !Busy && E.HeroDist < 15f);
    private int _blasts;
    private void Blast()
    {
        Mech("blast");
        Face();
        M.Busy(1.6f, null);
        M.Model?.Play("Spell_Simple_Shoot", 0.6f, 0.1f, true);
        Vector2 dir = ToHero, origin = M.Pos2;
        for (int tick = 0; tick < 2; tick++)
        {
            int t = tick;
            E.ConeBlast(dir, 14f, 60f, 1f + t * 0.45f, 0.45f, Element.Frost, St.Chill(0.6f, 3f), hero =>
            {
                if (hero && t == 1 && E.HeroOk && World.P.Has(StatusId.Chill)) { Mech("freeze"); E.Hit(World.P, 0.1f, Element.Frost, St.Freeze(1.0f)); }
            });
        }
        E.Then(1.2f, () =>
        {
            if (++_blasts % 2 == 0) return;
            Mech("muster");
            Vector2 far = origin + dir * 13f, side = new(-dir.Y, dir.X);
            if (E.AliveAdds() > 4) return;
            for (int i = 0; i < 4; i++) E.SummonAfter(0.7f, "frostbound", far + side * (-4.5f + 3f * i), null, m => m.FaceDir(-dir), 0.6f);
        });
    }
}

/// <summary>白影: never fully visible — re-stealths 1.5 s after every pounce, leaving three decoy ripples; the pounce marks (bleed, vulnerable). Area damage or burning ground reveals it for 3 s.</summary>
internal sealed class WhiteShadow : RareScript
{
    private bool _hidden; private float _reveal, _pounce = 2.5f, _angle;
    private readonly List<Node3D> _decoys = new();
    public override void Setup() { _hidden = false; }
    public override void Reset() { E.Hide(M, false); _hidden = false; }
    public override void Engage() { base.Engage(); Stealth(); }
    private void Stealth()
    {
        Mech("stealth");
        _hidden = true;
        E.Hide(M, true);
        Mech("decoys");
        for (int i = 0; i < 3; i++)
        {
            var d = E.Mark(E.Near(M.Pos2, 2f, 6f), 0.9f, new Color(0.85f, 0.92f, 1f), 2.5f);
            _decoys.Add(d);
        }
        Fx.Dust(M.GlobalPosition);
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (!E.HeroOk) return;
        if (_reveal > 0f) { _reveal -= dt; if (_reveal <= 0f && !_hidden) Stealth(); }
        if (!_hidden) return;
        // circles the hero in the blizzard
        _angle += dt * 0.7f;
        Vector2 goal = E.Walk(E.HeroPos + Vector2.Right.Rotated(_angle) * 7f);
        E.Drive(M, goal, dt, 1.2f, 0.5f);
        // anything burning or any of the hero's ground effects on top of it reveals it
        foreach (var z in NamedHub.AllZones())
        {
            var s = NamedHub.ZoneSpecOf(z);
            if (s == null || !(NamedHub.ZoneOfHero(z) || s.El == Element.Fire)) continue;
            if (NamedHub.ZonePos(z).DistanceTo(M.Pos2) <= s.Radius + 1f) { Reveal(); return; }
        }
        _pounce -= dt;
        if (_pounce <= 0f) Pounce();
    }
    public override void OtherDamaged(Monster m, Hit h, float amount) { }
    private void Reveal() { Mech("reveal"); _hidden = false; E.Hide(M, false); _reveal = 3f; E.Toast("白影現形了！"); }
    private void Pounce()
    {
        _pounce = 4f;
        Vector2 at = E.HeroPos;
        E.Track(Fx.Telegraph(at, 1.8f, 0.8f, Palette.Danger, M.Def.Element));
        E.After(0.8f, () =>
        {
            if (!E.Alive) return;
            Mech("pounce");
            _hidden = false; E.Hide(M, false);
            M.Pos2 = E.Walk(at + E.DirToHero(at) * -1.2f);
            M.Model?.Play("Zombie_Scratch", 1.4f, 0.05f, true);
            if (E.HeroIn(at, 2.1f)) { E.Hit(World.P, 1.2f, Element.Physical, St.Bleed(0.6f, 4f)); World.P.ApplyStatus(St.Vuln(4f), M); }
            _reveal = 1.5f;
        });
    }
}

/// <summary>末班馱隊: walks the supply road; every charge drops a keg that goes off after 2.5 s (freeze 1.5 s), up to six; on death the whole load goes (9 m, 3 s warning).</summary>
internal sealed class LastRun : RareScript
{
    private Vector2 _a, _b; private bool _toB = true; private int _kegs;
    public override void Setup()
    {
        _a = E.Home; _b = E.Near(E.Home, 26f, 36f);
        if (E.L?.Data?.Deco != null)
        {
            // the far end of the road: the path cell farthest from where it stands, within 45 m
            var g = World.Grid; float best = 0f;
            for (int y = 0; y < g.H; y += 2) for (int x = 0; x < g.W; x += 2)
            {
                int i = y * g.W + x; if (i >= E.L.Data.Deco.Length || E.L.Data.Deco[i] != 1 || !g.Clear(x, y)) continue;
                var p = g.Center(x, y); float d = p.DistanceTo(_a);
                if (d > best && d < 45f) { best = d; _b = p; }
            }
        }
    }
    public override void Idle(float dt)
    {
        if (E.Test) return;
        if (E.Drive(M, _toB ? _b : _a, dt, 0.45f, 1.5f)) _toB = !_toB;
    }
    public override void Build() => E.Every(7f, Charge, 2.5f, () => !Busy && E.HeroDist < 20f);
    private void Charge()
    {
        Mech("charge");
        Face();
        Vector2 from = M.Pos2, dir = ToHero;
        float len = Mathf.Clamp(E.HeroDist + 4f, 8f, 16f);
        E.LineBlast(from, dir, len, 2.6f, 0.9f, 1.1f, Element.Frost, St.Knock(7f));
        M.Busy(1.6f, null, dt => { });
        E.Then(0.9f, () =>
        {
            Vector2 to = E.Walk(from + dir * len);
            E.Run(0.55f, k => { if (E.Alive) M.Pos2 = from.Lerp(to, k); }, () =>
            {
                if (_kegs >= 6) return;
                _kegs++;
                Mech("keg");
                Vector2 at = E.Walk(from + dir * (len * 0.5f));
                var keg = NamedProps.Place(NamedProps.Jar(new Color(0.6f, 0.85f, 1f), 0.8f), at);
                E.Own(keg);
                E.Blast(at, 3f, 2.5f, 0.9f, Element.Frost, St.Freeze(1.5f), () => { _kegs--; if (GodotObject.IsInstanceValid(keg)) keg.QueueFree(); });
            });
            M.Model?.Play(M.Def.Run, 1.6f, 0.05f);
        });
    }
    public override void Death()
    {
        Mech("deathburst");
        E.Toast("整車貨物要炸了——快離開！");
        E.Blast(M.Pos2, 9f, 3f, 1.6f, Element.Frost, St.Freeze(1.5f));
    }
}

/// <summary>千年凍燭: six orbiting flickers each absorb one hit and re-freeze after 10 s; a 10 m heat-draw pulls the hero in and drains resource while chilled; breaks into six flickers on death.</summary>
internal sealed class ThousandYear : RareScript
{
    private List<Node3D> _orbs;
    private readonly float[] _down = new float[6];
    public override void Setup()
    {
        if (_orbs == null) _orbs = E.Orbit(M, 6, new Color(0.6f, 0.9f, 1f), 0.32f);
        Array.Clear(_down, 0, 6);
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        for (int i = 0; i < 6; i++) if (_down[i] > 0f) _down[i] -= dt;
        NamedEncounter.SpinOrbs(_orbs, 1.6f, 1.6f, 1.6f, T, i => _down[i] <= 0f);
        if (!E.HeroOk || E.HeroDist > 10f) return;
        E.Blow(M.Pos2 - E.HeroPos, 1.6f, dt);
        if ((int)(T * 2f) != (int)((T - dt) * 2f)) Mech("pull");
        if (World.P.Has(StatusId.Chill)) { E.Drain(0.04f * dt); if ((int)T != (int)(T - dt)) Mech("drain"); }
    }
    public override void Damaged(Hit h, float amount)
    {
        for (int i = 0; i < 6; i++)
        {
            if (_down[i] > 0f) continue;
            _down[i] = 10f;
            M.Life = Mathf.Min(M.MaxLife, M.Life + amount);       // the flicker took it
            Mech("shield");
            Fx.Hit(M.Chest, Element.Frost, false);
            return;
        }
    }
    public override void Death()
    {
        Mech("shatter");
        for (int i = 0; i < 6; i++) E.Summon("cf_flicker", E.Near(M.Pos2, 1f, 3f), "ice_wraith", true, 0.15f);
        E.KillAddsOnDeath = false;
    }
}

// ═════════════════════════ 沉眠墓原 crypt ═════════════════════════

/// <summary>陵衛長・貝拉克: spawns with three pallbearers that lock lids into a shield wall (no frontal damage, blocks projectiles); the wall opens 4 s when a member is staggered or struck from behind.</summary>
internal sealed class Belak : RareScript
{
    private readonly List<Monster> _wall = new();
    private float _open;
    public override void Setup()
    {
        _wall.Clear();
        Vector2 side = new(-M.Forward.Y, M.Forward.X);
        for (int i = 0; i < 3; i++)
        {
            var m = E.Summon("bone_pallbearer", E.Home + side * (i - 1 < 0 ? -2.2f * (1 + i) : 2.2f * i), "skeleton", false, 2.2f);
            m.Pack = M.Pack; _wall.Add(m);
        }
    }
    public override void Engage() { base.Engage(); Mech("wall"); E.Toast("棺蓋相連成牆——繞到背後！"); }
    public override Vector2? BotStand => _open > 0f || !E.Alive ? null : E.Walk(M.Pos2 - M.Forward * 3f);
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_open > 0f) _open -= dt;
        bool closed = _open <= 0f;
        Apply(M, closed);
        foreach (var m in _wall) if (m != null && GodotObject.IsInstanceValid(m) && !m.Dead) Apply(m, closed);
        // the wall moves as one: members keep station beside the captain
        if (closed && E.HeroOk)
        {
            Vector2 side = new(-M.Forward.Y, M.Forward.X);
            for (int i = 0; i < _wall.Count; i++)
            {
                var m = _wall[i];
                if (m == null || !GodotObject.IsInstanceValid(m) || m.Dead || m.Attacking) continue;
                float off = i == 0 ? -2.2f : i == 1 ? 2.2f : -4.4f;
                Vector2 slot = M.Pos2 + side * off;
                if (m.Pos2.DistanceTo(slot) > 2.5f) E.Drive(m, slot, dt, 1.1f, 0.8f);
            }
        }
    }
    private void Apply(Monster m, bool closed)
    {
        bool front = closed && E.HeroOk && NamedEncounter.InFront(m, E.HeroPos, 0.1f);
        m.DrMul = front ? 0.04f : 1f;
        m.BlocksProjectiles = front;
    }
    public override void Damaged(Hit h, float amount) => Check(M, amount);
    public override void OtherDamaged(Monster m, Hit h, float amount) => Check(m, amount);
    private void Check(Monster m, float amount)
    {
        if (_open > 0f || !E.HeroOk) return;
        bool behind = NamedEncounter.Behind(m, E.HeroPos, -0.1f);
        bool stagger = m.Stunned || amount >= m.MaxLife * 0.12f;
        if (!behind && !stagger) return;
        _open = 4f;
        Mech("open");
        E.Toast("盾牆開了！");
        foreach (var x in _wall) if (x != null && GodotObject.IsInstanceValid(x)) Fx.Burst(x.Chest, Element.Lightning, 1.5f, 0.5f);
    }
    public override void Killed(Monster m) { if (_wall.Contains(m) && _open <= 0f) { _open = 4f; Mech("open"); } }
}

/// <summary>垂紗婆: drops, strikes and re-ascends every 6 s, each drop leaving a web (root on entry, 10 s); every third drop cocoons the hero — she bites the cocooned for triple damage.</summary>
internal sealed class VeilWife : RareScript
{
    private int _drops; private bool _up; private Monster _cocoon;
    public override void Engage() { base.Engage(); _drops = 0; Ascend(); }
    public override void Reset() { E.Hide(M, false); _up = false; }
    public override Monster BotTarget => _cocoon != null && GodotObject.IsInstanceValid(_cocoon) && !_cocoon.Dead ? _cocoon : null;
    private void Ascend()
    {
        _up = true;
        E.Hide(M, true);
        M.Busy(1e6f, null);
        E.Then(2.6f, Drop);
    }
    private void Drop()
    {
        if (!E.Alive || !E.HeroOk) { E.Then(1f, Drop); return; }
        _drops++;
        Vector2 at = E.HeroPos;
        E.Track(Fx.Telegraph(at, 2.2f, 1f, Palette.Danger, M.Def.Element));
        E.Then(1f, () =>
        {
            Mech("drop");
            M.Pos2 = E.Walk(at);
            E.Hide(M, false); _up = false;
            M.Busy(2.4f, null);
            M.Model?.Play("Zombie_Scratch", 1.3f, 0.05f, true);
            bool hit = E.HeroIn(at, 2.4f);
            E.HitCircle(at, 2.2f, 1f, Element.Poison, St.Poison(0.4f, 3f));
            Mech("web");
            var web = NamedProps.Place(NamedProps.Patch(2.4f, new Color(0.85f, 0.85f, 0.9f)), at);
            E.Own(web);
            E.Zone(at, 2.4f, 10f, 0.02f, Element.Poison, St.Root(1f));
            E.After(10f, () => { if (GodotObject.IsInstanceValid(web)) web.QueueFree(); });
            if (_drops % 3 == 0 && hit && E.HeroOk) Cocoon();
            E.Then(2.4f, () => { if (E.Alive) Ascend(); });
        });
    }
    private void Cocoon()
    {
        Mech("cocoon");
        var p = World.P;
        p.StTime[(int)StatusId.Root] = Mathf.Max(p.StTime[(int)StatusId.Root], 2.5f);
        p.CcImmune = 0f;
        _cocoon = E.Prop("絲繭", p.Pos2 + new Vector2(0.6f, 0f), Mathf.Max(30f, M.MaxLife * 0.06f), NamedProps.Heap(new Color(0.9f, 0.9f, 0.92f), 0.9f), () =>
        {
            if (E.HeroOk) World.P.StTime[(int)StatusId.Root] = 0f;
            _cocoon = null;
        }, 0.7f, 1.6f);
        E.Toast("被裹進絲繭了——打破它！");
        E.After(1.3f, () =>
        {
            if (_cocoon == null || !E.Alive || !E.HeroOk) return;
            Mech("bite");
            E.Hit(World.P, 3f, Element.Poison, St.Poison(0.5f, 4f));
            Fx.Hit(World.P.Chest, Element.Poison, true);
        });
        E.After(2.5f, () => { if (_cocoon != null) { E.RemoveProp(_cocoon); _cocoon = null; } });
    }
}

/// <summary>無名王臥像: the slab makes him immune from behind and he cannot be staggered; the lid-slam sends three crushing lines and leaves him vulnerable for 2 s.</summary>
internal sealed class NamelessKing : RareScript
{
    private float _vuln;
    public override void Build() => E.Every(10f, Slam, 4f, () => !Busy && E.HeroDist < 13f);
    public override Vector2? BotStand => E.Alive ? E.Walk(M.Pos2 + M.Forward * 2.5f) : null;
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_vuln > 0f) { _vuln -= dt; M.DrMul = 1.5f; return; }
        bool back = E.HeroOk && NamedEncounter.Behind(M, E.HeroPos, -0.2f);
        M.DrMul = back ? 0f : 1f;
        M.StTime[(int)StatusId.Stun] = 0f; M.StTime[(int)StatusId.Freeze] = 0f;
    }
    public override void Damaged(Hit h, float amount) { if (amount <= 0.5f && E.HeroOk && NamedEncounter.Behind(M, E.HeroPos, -0.2f)) Mech("backblock"); }
    private void Slam()
    {
        Mech("slam");
        Face();
        M.Busy(1.2f + 2f, null);
        M.Model?.Play("Sword_Attack", 0.6f, 0.1f, true);
        E.LaneFan(M.Pos2, ToHero, 3, 36f, 14f, 2.2f, 1.15f, 1.3f, Element.Physical, St.Stun(0.6f));
        E.Then(1.2f, () =>
        {
            Mech("rise");
            _vuln = 2f;
            M.Model?.Play("Hit_Knockback", 0.5f, 0.1f, true);
            E.Toast("無名王倒地——趁現在！");
        });
    }
}

/// <summary>百泣結: a 5 m fear aura (1 s every 4 s) unless the hero stands in firelight; splits into three mourners at 50 %, each of which releases two lantern-ghosts when it dies.</summary>
internal sealed class HundredTears : RareScript
{
    private readonly HashSet<Monster> _mourners = new();
    public override void Build()
    {
        E.Every(4f, Aura, 2f);
        E.AtLife(Split, 0.5f);
    }
    public override void Setup() { _mourners.Clear(); }
    private void Aura()
    {
        if (!E.HeroOk) return;
        foreach (var src in _mourners.Where(m => m != null && GodotObject.IsInstanceValid(m) && !m.Dead).Append(M))
        {
            if (src.Pos2.DistanceTo(E.HeroPos) > 5f) continue;
            if (E.InFirelight(E.HeroPos)) { Fx.Glow(World.P.Chest, Palette.Ember, 0.6f, 0.4f); return; }
            Mech("fear");
            Fx.Ring(src.GlobalPosition + Vector3.Up * 0.2f, 5f, new Color(0.7f, 0.85f, 1f), 0.4f);
            E.Hit(World.P, 0.15f, Element.Shadow, St.Fear(1f));
            return;
        }
    }
    private void Split()
    {
        Mech("split");
        E.Toast("哭聲分成了三股");
        for (int i = 0; i < 2; i++)
        {
            var m = E.Summon("wraith_mourner", E.Near(M.Pos2, 2f, 4f), "wraith", true, 3f, MonTier.Champion);
            _mourners.Add(m);
        }
        Fx.Burst(M.Chest, Element.Shadow, 4f, 1f);
    }
    public override void Killed(Monster m) { if (_mourners.Remove(m)) Ghosts(m.Pos2); }
    public override void Death() => Ghosts(M.Pos2);
    private void Ghosts(Vector2 at)
    {
        Mech("ghosts");
        for (int i = 0; i < 2; i++) E.Summon("wraith_lantern", E.Near(at, 1f, 2.5f), "ice_wraith", true, 0.6f);
        E.KillAddsOnDeath = false;
    }
}

// ═════════════════════════ 瘴泣沼澤 mire ═════════════════════════

/// <summary>老鼓腹: a tongue-pull that swallows (2 s inside, poison, cannot act, then spat 8 m); fire ignites the belly gas — it bursts for 20 % of its own life and is stunned 3 s.</summary>
internal sealed class OldDrum : RareScript
{
    private float _bellyCd;
    public override void Build() => E.Every(8f, Tongue, 3f, () => !Busy && E.HeroDist < 12f);
    public override void Tick(float dt) { base.Tick(dt); if (_bellyCd > 0f) _bellyCd -= dt; }
    private void Tongue()
    {
        Mech("tongue");
        Face();
        M.Busy(1.3f, null);
        M.Model?.Play("Melee_Hook", 0.8f, 0.1f, true);
        Vector2 from = M.Pos2, dir = ToHero;
        E.LineBlast(from, dir, 12f, 1.4f, 0.9f, 0.3f, Element.Poison, null, () => { if (E.HeroInLane(from, dir, 12f, 1.4f)) Swallow(); });
    }
    private void Swallow()
    {
        if (!E.HeroOk || World.P.Invulnerable) return;
        Mech("swallow");
        var p = World.P;
        E.Drag(M.Pos2, 0.4f);
        E.After(0.35f, () =>
        {
            if (!E.HeroOk) return;
            p.StTime[(int)StatusId.Stun] = Mathf.Max(p.StTime[(int)StatusId.Stun], 2f); p.CcImmune = 0f;
            p.Model.Visible = false;
            E.Hit(p, 0.4f, Element.Poison, St.Poison(0.9f, 3f));
            E.Toast("被吞下去了！");
        });
        E.After(2.35f, () =>
        {
            if (GodotObject.IsInstanceValid(p)) p.Model.Visible = true;
            if (!E.HeroOk) return;
            Vector2 d = E.Alive ? M.Forward : Vector2.Down;
            E.Fling(d, Mathf.Sqrt(56f * 8f));
            Fx.Burst(p.Chest, Element.Poison, 2f, 0.6f);
        });
    }
    public override void Reset() { if (E.HeroOk) World.P.Model.Visible = true; }
    public override void Damaged(Hit h, float amount)
    {
        if (_bellyCd > 0f || !(h.El == Element.Fire || M.Has(StatusId.Burn))) return;
        _bellyCd = 16f;
        Mech("belly");
        E.Toast("腹中瘴氣被點燃了！");
        Fx.Burst(M.Chest, Element.Fire, 4f, 1.4f);
        E.HitCircle(M.Pos2, 4f, 0.8f, Element.Fire);
        E.After(0.01f, () => { E.HurtNamed(0.15f, Element.Fire); NamedEncounter.StunMon(M, 3f); });
    }
}

/// <summary>引路燈人: neutral at first — it walks ahead through the fog, past three clusters of dormant false lamp-posts that wake as it passes; at the end of the walk it turns with a spore aura. Struck early, it turns at once but drops only half.</summary>
internal sealed class GuideLight : RareScript
{
    private readonly List<Vector2> _route = new();
    private int _leg; private bool _hostile, _walking; private float _legT;
    private float _aura;
    public override void Setup()
    {
        _route.Clear(); _leg = 0; _hostile = false; _walking = false;
        Vector2 dir = Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau));
        for (int i = 1; i <= 4; i++) _route.Add(E.Test ? E.OnRing(i * 1.4f, E.ArenaR * 0.65f) : E.Walk(E.Home + dir.Rotated(Rng.Range(-0.4f, 0.4f)) * (9f * i)));
        M.Faction = Faction.Monster;
        M.Untargetable = false;
    }
    public override bool BotPassive => !_hostile;
    public override Vector2? BotStand => !_hostile && E.Alive ? E.Walk(M.Pos2 - E.DirToHero(M.Pos2) * -5f) : null;
    public override void Idle(float dt)
    {
        if (_hostile) return;
        M.Busy(0.3f, null);
        if (!E.HeroOk) return;
        if (!_walking) { if (E.HeroDist < 12f) { _walking = true; Mech("lead"); E.Toast("霧中有盞燈在前方引路……"); } else return; }
        if (E.HeroDist > 14f) { M.Model?.Play(M.Def.Idle, 1f, 0.2f); return; }       // it waits for the hero
        if (_leg >= _route.Count) { Turn(false); return; }
        _legT += dt;
        if (E.Drive(M, _route[_leg], dt, 0.55f, 1f) || _legT > 12f)
        {
            if (_leg < 3)
            {
                Mech("ambush");
                for (int i = 0; i < 2; i++) E.SummonAfter(0.8f, "lamp_post", E.Near(_route[_leg], 2f, 5f), "mire_hag");
            }
            _leg++; _legT = 0f;
        }
    }
    public override void Damaged(Hit h, float amount) { if (!_hostile) { Mech("early"); E.HalfLoot = true; E.Toast("燈人被你驚醒——伏兵還沒走完，戰利品減半"); Turn(true); } }
    private void Turn(bool early)
    {
        if (_hostile) return;
        _hostile = true;
        if (!early) Mech("turn");
        M.Busy(0.05f, null);
        E.Provoke();
        Fx.Burst(M.Chest, Element.Poison, 3f, 1f);
        if (!early) E.Toast("燈光轉成了孢子的綠——它從來就不是引路人");
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (!_hostile) { Turn(false); return; }
        _aura -= dt;
        if (_aura <= 0f) { _aura = 1f; if (E.HeroOk && E.HeroDist < 4.5f) E.Hit(World.P, 0.18f, Element.Poison, St.Poison(0.3f, 2f)); }
    }
}

/// <summary>泣河老蛭: submerged except to feed; the inhale reaches 14 m and drags the hero into its strike; sheds six weepers per 20 % life lost, and each weeper that feeds on the hero and returns heals it 3 %.</summary>
internal sealed class WeepingRiver : RareScript
{
    private bool _down; private float _feed = 3f;
    private readonly HashSet<Monster> _fed = new();
    public override void Build() => E.AtLife(Shed, 0.8f, 0.6f, 0.4f, 0.2f);
    public override void Engage() { base.Engage(); Submerge(); }
    public override void Reset() { E.Hide(M, false); _down = false; }
    private void Submerge() { Mech("submerge"); _down = true; E.Hide(M, true); M.Busy(1e6f, null); _feed = 2.5f; }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_down) { _feed -= dt; if (_feed <= 0f) Inhale(); }
        foreach (var w in E.Adds.ToArray())
        {
            if (w == null || !GodotObject.IsInstanceValid(w) || w.Dead) continue;
            if (_fed.Contains(w) && w.Pos2.DistanceTo(M.Pos2) < 2.5f) { _fed.Remove(w); E.HealNamed(0.03f); Mech("feed"); w.NoLoot = true; World.Monsters.Remove(w); w.QueueFree(); }
            else if (_fed.Contains(w)) E.Drive(w, M.Pos2, dt, 1.2f, 1f);
        }
    }
    public override void HeroHit(Hit h, float amount) { if (h.Source is Monster w && E.Adds.Contains(w)) _fed.Add(w); }
    private void Inhale()
    {
        _feed = 999f;
        Mech("inhale");
        if (!E.HeroOk) { E.Then(1f, Submerge); return; }
        M.Pos2 = E.Walk(E.HeroPos + E.DirToHero(M.Pos2) * -9f);
        E.Hide(M, false); _down = false;
        Face();
        M.Model?.Play("Spell_Simple_Enter", 0.7f, 0.1f, true);
        E.Toast("河水倒灌進牠的口中——別被吸過去！");
        E.Run(1.6f, k => { if (E.HeroOk && E.HeroDist < 14f) E.Blow(M.Pos2 - E.HeroPos, 4.2f, 1f / 60f); });
        E.Then(1.6f, () =>
        {
            E.ConeBlast(ToHero, 5f, 120f, 0.6f, 1.4f, Element.Poison, St.Poison(0.5f, 3f));
            E.Then(3.8f, () => { if (E.Alive) Submerge(); });
        });
        M.Busy(5.4f, null);
    }
    private void Shed()
    {
        Mech("weepers");
        for (int i = 0; i < 6; i++) E.SummonAfter(0.6f, "lurk_weeper", E.Near(M.Pos2, 2f, 6f), "mire_husk", null, 0.6f);
    }
}

/// <summary>網婆・妲芙: three nets in a fan; a netted hero is hauled 6 m toward the nearest mud patch where two peat-sleepers rise; she re-nets the moment a root ends unless in melee.</summary>
internal sealed class Dafu : RareScript
{
    private readonly List<Vector2> _mud = new();
    private float _rootLeft = -1f, _cd;
    public override void Setup()
    {
        _mud.Clear();
        for (int i = 0; i < 3; i++)
        {
            Vector2 at = E.Near(E.Home, 6f, 11f);
            _mud.Add(at);
            E.Own(NamedProps.Place(NamedProps.Patch(2.2f, new Color(0.22f, 0.2f, 0.12f)), at));
        }
    }
    public override void Build() => E.Every(7f, Nets, 2.5f, () => !Busy && E.HeroDist is > 3.2f and < 14f);
    private void Nets()
    {
        Mech("nets");
        Face();
        M.Busy(1.1f, null);
        M.Model?.Play("OverhandThrow", 0.9f, 0.1f, true);
        Vector2 from = M.Pos2, dir = ToHero;
        bool caught = false;
        E.LaneFan(from, dir, 3, 36f, 13f, 1.8f, 0.9f, 0.35f, Element.Poison, null, k =>
        {
            float a = Mathf.DegToRad(-18f + 18f * k);
            if (!caught && E.HeroInLane(from, dir.Rotated(a), 13f, 1.8f)) { caught = true; Netted(); }
        });
    }
    private void Netted()
    {
        if (!E.HeroOk) return;
        var p = World.P;
        p.StTime[(int)StatusId.Root] = Mathf.Max(p.StTime[(int)StatusId.Root], 1.6f); p.CcImmune = 0f;
        _rootLeft = 1.6f;
        Vector2 mud = _mud.OrderBy(m => m.DistanceTo(p.Pos2)).FirstOrDefault();
        if (_mud.Count == 0) return;
        Mech("haul");
        Vector2 to = p.Pos2 + (mud - p.Pos2).LimitLength(6f);
        E.Drag(to, 0f);
        if (p.Pos2.DistanceTo(mud) < 8f)
        {
            Mech("sleepers");
            for (int i = 0; i < 2; i++) E.SummonAfter(0.9f, "peat_sleeper", E.Near(mud, 0.5f, 2f), "mire_husk");
        }
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_cd > 0f) _cd -= dt;
        if (_rootLeft < 0f) return;
        _rootLeft -= dt;
        if (_rootLeft > 0f) return;
        _rootLeft = -1f;
        if (E.HeroDist > 3.2f && _cd <= 0f && !Busy) { _cd = 4f; Mech("renet"); Nets(); }
    }
}

// ═════════════════════════ 鏽砂廢都 sands ═════════════════════════

/// <summary>正午之盾・歐岡: reflects every projectile at full damage; after three reflections the mirror overheats and flashes (60° cone, stun 1.5 s). Open from behind and for 3 s after a shield-bash that misses.</summary>
internal sealed class Ogan : RareScript
{
    private int _reflected; private float _open;
    public override void Build() => E.Every(6f, Bash, 2.5f, () => !Busy && E.HeroDist < 5.5f);
    public override Vector2? BotStand => _open > 0f || !E.Alive ? null : E.Walk(M.Pos2 - M.Forward * 2.6f);
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_open > 0f) _open -= dt;
        bool back = E.HeroOk && NamedEncounter.Behind(M, E.HeroPos, -0.1f);
        if (back) { if ((int)(T * 2) != (int)((T - dt) * 2)) Mech("behind"); }
        M.DrMul = _open > 0f || back ? 1f : 0.15f;
        // the mirror: hero shots coming at its face are turned back
        foreach (var s in NamedHub.AllShots().ToArray())
        {
            if (!NamedHub.ShotOfHero(s)) continue;
            Vector2 sp = NamedHub.ShotPos(s);
            if (sp.DistanceTo(M.Pos2) > 2.4f || !NamedEncounter.InFront(M, sp, 0.2f) || _open > 0f) continue;
            NamedHub.Kill(s);
            Mech("reflect");
            E.Shoot(M.Pos2 + M.Forward, E.DirToHero(M.Pos2), 18f, 22f, 1.0f, Element.Lightning, null, 0.45f);
            Fx.LightFlash(M.Chest, new Color(1f, 0.9f, 0.6f), 2f, 6f, 0.15f);
            if (++_reflected >= 3) { _reflected = 0; Overheat(); }
        }
    }
    private void Overheat()
    {
        Mech("overheat");
        E.Toast("鏡面過熱——閃光！");
        E.ConeBlast(ToHero, 12f, 60f, 1f, 0.6f, Element.Lightning, St.Stun(1.5f));
    }
    private void Bash()
    {
        Mech("bash");
        Face();
        M.Busy(1.3f, null);
        M.Model?.Play("Sword_Attack", 0.9f, 0.1f, true);
        E.ConeBlast(ToHero, 4f, 70f, 0.8f, 1.2f, Element.Physical, St.Knock(7f), hero => { if (!hero) { _open = 3f; E.Toast("撲空了——盾後露出空門"); } });
    }
}

/// <summary>百殼老蠍: moults at 66 % and 33 % — the brass stays as an obstacle, it turns fast and soft (+40 % damage taken) and runs for the nearest brass prop to re-armour; destroyed props deny it.</summary>
internal sealed class HundredShell : RareScript
{
    private bool _soft; private Monster _goal;
    public override void Build() => E.AtLife(Moult, 0.66f, 0.33f);
    public override void Setup()
    {
        for (int i = 0; i < 3; i++)
            E.Prop("黃銅殘骸", E.Near(E.Home, 7f, 12f), Mathf.Max(40f, M.MaxLife * 0.06f), NamedProps.Gear(new Color(1f, 0.7f, 0.3f)), () => { }, 0.9f, 1.4f);
    }
    public override Monster BotTarget => _soft && _goal != null && GodotObject.IsInstanceValid(_goal) && !_goal.Dead && _goal.Pos2.DistanceTo(E.HeroPos) < 9f ? _goal : null;
    private void Moult()
    {
        Mech("moult");
        E.Toast("百殼脫下了黃銅——牠要去找新的殼！");
        E.Own(NamedProps.Place(NamedProps.Heap(new Color(0.7f, 0.5f, 0.25f), 1.3f), M.Pos2));
        _soft = true;
        M.DrMul = 1.4f; M.SpeedMul = 1.6f;
        _goal = E.NearestProp(M.Pos2);
        if (_goal == null) { Mech("denied"); E.After(6f, () => { if (E.Alive && _soft) { _soft = false; M.DrMul = 1f; M.SpeedMul = 1f; } }); }
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (!_soft) return;
        if (_goal == null || !GodotObject.IsInstanceValid(_goal) || _goal.Dead)
        {
            _goal = E.NearestProp(M.Pos2);
            if (_goal == null) { Mech("denied"); _soft = false; M.SpeedMul = 1.25f; return; }
        }
        if (E.Drive(M, _goal.Pos2, dt, 1.6f, 1.6f))
        {
            Mech("rearmour");
            E.RemoveProp(_goal); _goal = null;
            _soft = false; M.DrMul = 1f; M.SpeedMul = 1f;
            M.AddBarrier(M.MaxLife * 0.08f, 12f);
            Fx.Burst(M.Chest, Element.Lightning, 2.5f, 0.8f);
            E.Toast("牠披上了新殼");
        }
    }
}

/// <summary>雷甕長老・泰薩: called strikes chain to every brass object within 6 m of the impact (props, sentinels, his jars); each jar lost at 66 / 33 / 0 % discharges a nova, the last with a 3 s warning.</summary>
internal sealed class Tesa : RareScript
{
    private readonly List<Vector2> _rods = new();
    public override void Build()
    {
        E.Every(5.5f, Strike, 2f, () => E.HeroOk && E.HeroDist < 22f);
        E.AtLife(() => Jar(false), 0.66f, 0.33f);
    }
    public override void Setup()
    {
        _rods.Clear();
        for (int i = 0; i < 4; i++)
        {
            Vector2 at = E.OnRing(i * Mathf.Tau / 4f + 0.4f, 7f);
            _rods.Add(at);
            E.Own(NamedProps.Place(NamedProps.Pylon(new Color(0.75f, 0.8f, 1f), 2.6f), at));
        }
    }
    private void Strike()
    {
        Mech("strike");
        Vector2 at = E.Near(E.HeroPos, 0f, 2f);
        E.Blast(at, 2.4f, 1.1f, 0.9f, Element.Lightning, null, () =>
        {
            var brass = _rods.Where(r => r.DistanceTo(at) <= 6f).ToList();
            foreach (var m in E.HostilesIn(at, 6f)) if (m.Def.Family == "sands" || m.Def.Family == "rust" || m.Def.Family == "engine") brass.Add(m.Pos2);
            if (E.Alive && M.Pos2.DistanceTo(at) <= 6f) brass.Add(M.Pos2);
            if (brass.Count == 0) return;
            Mech("chain");
            foreach (var b in brass)
            {
                Fx.Beam(World.To3(at, 1.2f), World.To3(b, 1.6f), Element.Lightning, 0.2f, 0.25f);
                E.HitCircle(b, 2.6f, 0.6f, Element.Lightning);
            }
        }, false, "storm_bolt");
    }
    private void Jar(bool last)
    {
        Mech(last ? "lastjar" : "jar");
        if (last) { E.Toast("最後一只雷甕要炸了！"); E.Blast(M.Pos2, 8f, 3f, 1.6f, Element.Lightning, St.Stun(0.8f)); }
        else E.RingBlast(M.Pos2, 0f, 6f, 1f, 1.1f, Element.Lightning);
    }
    public override void Death() => Jar(true);
}

/// <summary>第十三輪: thrown discs do not return — each stays on a wide orbit round him as a moving blade, up to six; staggering him drops every disc.</summary>
internal sealed class Thirteenth : RareScript
{
    private readonly List<(Node3D n, float r, float a)> _discs = new();
    private float _hitCd;
    public override void Build() => E.Every(4.5f, Throw, 2f, () => !Busy && E.HeroDist < 18f);
    public override void Reset() { Clear(); }
    private void Clear() { foreach (var d in _discs) if (GodotObject.IsInstanceValid(d.n)) d.n.QueueFree(); _discs.Clear(); }
    private void Throw()
    {
        Mech("disc");
        Face();
        M.Busy(0.9f, null);
        M.Model?.Play("OverhandThrow", 1f, 0.1f, true);
        E.Shoot(M.Pos2, ToHero, 15f, 16f, 0.9f, Element.Physical);
        if (_discs.Count >= 6) return;
        float r = 4f + _discs.Count * 1.6f;
        var n = NamedProps.Place(NamedProps.Ring(0.7f, new Color(1f, 0.8f, 0.4f), 2.6f), M.Pos2);
        E.Own(n);
        _discs.Add((n, r, Rng.Range(0f, Mathf.Tau)));
        Mech("orbit");
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_hitCd > 0f) _hitCd -= dt;
        for (int i = 0; i < _discs.Count; i++)
        {
            var d = _discs[i];
            if (!GodotObject.IsInstanceValid(d.n)) continue;
            float a = d.a + T * (0.9f - i * 0.08f) * (i % 2 == 0 ? 1f : -1f);
            Vector2 at = M.Pos2 + Vector2.Right.Rotated(a) * d.r;
            d.n.GlobalPosition = World.To3(at, 1.1f);
            d.n.Rotation = new Vector3(Mathf.Pi * 0.5f, a * 3f, 0f);
            if (_hitCd <= 0f && E.HeroIn(at, 1.1f)) { _hitCd = 0.6f; E.Hit(World.P, 0.6f, Element.Physical, St.Bleed(0.3f, 3f)); }
        }
        if ((M.Staggered || M.Stunned) && _discs.Count > 0)
        {
            Mech("drop");
            E.Toast("輪盤全數落地");
            foreach (var d in _discs) if (GodotObject.IsInstanceValid(d.n)) Fx.Dust(d.n.GlobalPosition);
            Clear();
        }
    }
    public override void Damaged(Hit h, float amount)
    {
        if (amount >= M.MaxLife * 0.07f && _discs.Count > 0) NamedEncounter.StunMon(M, 0.8f);    // an elite does not stagger: a heavy blow stands in for it
    }
}

// ═════════════════════════ 寂滅聖城 sanctum ═════════════════════════

/// <summary>首席領唱: every 8 s a rest gives all allies within 14 m her barrier; barriers fail only while the hero stands inside her drifting 3 m halo. Blinks 10 m when struck in melee (6 s cooldown).</summary>
internal sealed class FirstCantor : RareScript
{
    private Vector2 _halo; private Node3D _ring; private float _blink;
    public override void Build() => E.Every(8f, Rest, 1.5f);
    public override void Engage() { base.Engage(); _halo = E.Near(M.Pos2, 3f, 6f); _ring = E.Mark(_halo, 3f, new Color(0.95f, 0.95f, 1f)); }
    public override Vector2? BotStand => E.Alive && E.HeroOk && E.HeroPos.DistanceTo(_halo) > 2.2f && (M.Barrier > 0f) ? _halo : null;
    private void Rest()
    {
        Mech("rest");
        Snd.At("bell_toll", M.GlobalPosition, 0.6f);
        foreach (var m in E.HostilesIn(M.Pos2, 14f)) { m.AddBarrier(m.MaxLife * 0.18f, 8f); Fx.BuffAura(m, Element.Shadow, 1f, false); }
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_blink > 0f) _blink -= dt;
        if (_ring == null || !E.HeroOk) return;
        // the halo drifts away from the hero
        Vector2 away = (_halo - E.HeroPos);
        if (away.Length() < 7f) _halo = E.Walk(_halo + away.Normalized() * 0.9f * dt);
        if (_halo.DistanceTo(M.Pos2) > 9f) _halo = E.Walk(_halo + (M.Pos2 - _halo).Normalized() * 2f * dt);
        if (GodotObject.IsInstanceValid(_ring)) _ring.GlobalPosition = World.To3(_halo);
        Mech("halo");
        if (E.HeroPos.DistanceTo(_halo) <= 3f)
        {
            bool any = false;
            foreach (var m in E.HostilesIn(M.Pos2, 30f)) if (m.Barrier > 0f) { m.Barrier = 0f; m.BarrierLeft = 0f; any = true; }
            if (any) { Mech("broken"); Fx.Ring(World.To3(_halo, 0.2f), 3f, Colors.White, 0.4f); }
        }
    }
    public override void Damaged(Hit h, float amount)
    {
        if (_blink > 0f || !E.HeroOk || E.HeroDist > 3.5f) return;
        _blink = 6f;
        Mech("blink");
        Vector2 to = E.Walk(M.Pos2 + E.DirToHero(M.Pos2) * -10f);
        Fx.Teleport(M.GlobalPosition, Element.Shadow);
        M.Pos2 = to;
        Fx.Teleport(M.GlobalPosition, Element.Shadow);
    }
}

/// <summary>大鐘「永寂」: tolls three expanding rings with different gaps, each deleting the hero's ground effects and stripping summons' boons; hits on the hanging legs build stagger — staggered, it falls and rings, stunning its own allies 3 s.</summary>
internal sealed class Everquiet : RareScript
{
    private float _legs;
    public override void Build() => E.Every(9f, Toll, 3f, () => E.HeroOk && E.HeroDist < 20f);
    private void Toll()
    {
        Mech("toll");
        M.Busy(1.4f, null);
        Snd.At("bell_toll", M.GlobalPosition, 1f);
        E.Waves(M.Pos2, 3, 0f, 4f, 1.1f, new[] { 0.5f, 1.1f }, 0.9f, Element.Shadow, St.Stun(0.4f), (k, inner, outer) =>
        {
            int n = NamedHub.SnuffHeroZones(M.Pos2, outer);
            foreach (var mi in E.MinionsIn(M.Pos2, outer)) { mi.DmgMul = 1f; mi.SpeedMul = 1f; mi.AtkSpeedMul = 1f; mi.Barrier = 0f; }
            if (n > 0 || k == 2) Mech("strip");
        });
    }
    public override void Damaged(Hit h, float amount)
    {
        if (!E.HeroOk || !NamedEncounter.Behind(M, E.HeroPos, 0.3f) && E.HeroDist > 2.6f) return;   // the legs hang under the lip: close or from the side
        _legs += amount;
        if (_legs < M.MaxLife * 0.12f) return;
        _legs = 0f;
        Mech("fall");
        E.Toast("巨鐘墜地轟鳴——連牠的同伴都被震暈了");
        Snd.At("bell_toll", M.GlobalPosition, 1.2f);
        NamedEncounter.StunMon(M, 3f);
        M.ApplyStatus(St.Vuln(3f), null);
        foreach (var m in E.HostilesIn(M.Pos2, 12f)) if (m != M) NamedEncounter.StunMon(m, 3f);
        E.HitCircle(M.Pos2, 4f, 0.8f, Element.Physical, St.Stun(0.6f));
    }
}

/// <summary>不熄者・柯倫: never detonates — sprints in long arcs leaving burning wax (6 s), lighting every Wax-sealed he brushes; only chill or freeze slows him, and freezing him puts the trail out.</summary>
internal sealed class Kolen : RareScript
{
    private float _sprint = 2f, _drop; private Vector2 _goal; private bool _running;
    public override void Tick(float dt)
    {
        base.Tick(dt);
        bool cold = M.Has(StatusId.Chill) || M.Has(StatusId.Freeze);
        if (M.Has(StatusId.Freeze) && _running) { _running = false; Mech("quench"); E.Toast("凍住了——蠟火熄滅"); }
        M.SpeedMul = cold ? 0.6f : 1f;
        if (!_running) { _sprint -= dt; if (_sprint <= 0f && !Busy) Sprint(); return; }
        _drop -= dt;
        if (_drop <= 0f)
        {
            _drop = 0.35f;
            E.Zone(M.Pos2, 1.4f, 6f, 0.12f, Element.Fire, St.Burn(0.3f, 2f));
            Mech("trail");
            foreach (var m in E.HostilesIn(M.Pos2, 2f))
                if (m != M && (m.Def.Family == "wax" || m.Def.Id.StartsWith("wax_")) && !m.Has(StatusId.Burn)) { Mech("ignite"); m.Enrage(10f); m.Model?.SetRim(Palette.Ember * 1.6f); Fx.BuffAura(m, Element.Fire, 10f, false); }
        }
        if (E.Drive(M, _goal, dt, cold ? 1.2f : 2.2f, 1f)) { _running = false; _sprint = 2.2f; }
    }
    private void Sprint()
    {
        Mech("sprint");
        _running = true;
        Vector2 h = E.HeroOk ? E.HeroPos : E.Center;
        Vector2 side = (h - M.Pos2).Orthogonal().Normalized() * (Rng.Chance(0.5f) ? 1f : -1f);
        _goal = E.Walk(h + side * 6f + (h - M.Pos2).Normalized() * 5f);
    }
}

/// <summary>審默官・薇絲娜: every snuffer hit strips a buff and drains 25 % resource; she plants the cone as a 5 m hush zone (10 s: no resource regeneration, fire goes out) and fights barehanded and faster while it stands; the cone can be broken.</summary>
internal sealed class Vesna : RareScript
{
    private Monster _cone; private Vector2 _coneAt; private float _lastRes = -1f;
    public override void Build() => E.Every(14f, Plant, 4f, () => !Busy && _cone == null);
    public override Monster BotTarget => _cone != null && GodotObject.IsInstanceValid(_cone) && !_cone.Dead && E.HeroPos.DistanceTo(_coneAt) < 6f ? _cone : null;
    public override void HeroHit(Hit h, float amount)
    {
        if (h.Source != M || _cone != null) return;
        Mech("snuff");
        E.StripBuff(); E.Drain(0.25f);
        Fx.Burst(World.P.Chest, Element.Shadow, 1.2f, 0.4f);
    }
    private void Plant()
    {
        Mech("hushzone");
        _coneAt = E.Walk(M.Pos2 + M.Forward * 1.2f);
        E.Toast("捻熄之錐立起——圈內無法回復能量");
        _cone = E.Prop("捻熄之錐", _coneAt, Mathf.Max(40f, M.MaxLife * 0.08f), NamedProps.Snuffer(new Color(0.8f, 0.8f, 0.9f)), () => { Mech("cone"); End(); E.Toast("錐被擊倒了"); }, 0.6f, 2.4f);
        E.Mark(_coneAt, 5f, NamedProps.Hush, 10f);
        M.SpeedMul = 1.35f; M.AtkSpeedMul = 1.35f;
        E.After(10f, End);
    }
    private void End()
    {
        if (_cone != null) { E.RemoveProp(_cone); _cone = null; }
        M.SpeedMul = 1f; M.AtkSpeedMul = 1f; _lastRes = -1f;
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_cone == null || !E.HeroOk) return;
        var p = World.P;
        if (p.Pos2.DistanceTo(_coneAt) <= 5f)
        {
            if (_lastRes >= 0f && p.Resource > _lastRes) p.Resource = _lastRes;
            _lastRes = p.Resource;
            NamedHub.SnuffHeroZones(_coneAt, 5f, Element.Fire);
            if (p.Has(StatusId.Burn)) { }
        }
        else _lastRes = -1f;
    }
}

// ═════════════════════════ 燼淵 abyss ═════════════════════════

/// <summary>末班工頭: rings the shift-bell — every Forge-thrall within 20 m enrages 10 s, and any that dies while he lives stands back up once at 40 %; his hammer-blows send telegraphed shock-lines along the floor.</summary>
internal sealed class Foreman : RareScript
{
    private readonly HashSet<string> _risen = new();
    public override void Setup()
    {
        _risen.Clear();
        for (int i = 0; i < 3; i++) E.Summon("forge_thrall", E.Near(E.Home, 3f, 6f), "fiend", false, 1f).Pack = M.Pack;
    }
    public override void Build()
    {
        E.Every(12f, Bell, 2f);
        E.Every(6f, Shock, 4f, () => !Busy && E.HeroDist < 14f);
    }
    private void Bell()
    {
        Mech("bell");
        Snd.At("bell_toll", M.GlobalPosition, 0.9f);
        Fx.Ring(M.GlobalPosition + Vector3.Up * 0.3f, 20f, Palette.Ember, 0.6f, Sigil.Arcane);
        foreach (var m in E.HostilesIn(M.Pos2, 20f)) if (m != M) { m.Enrage(10f); Fx.BuffAura(m, Element.Fire, 10f, false); }
        if (E.AliveAdds() < 2) for (int i = 0; i < 2; i++) E.SummonAfter(0.8f, "forge_thrall", E.Near(M.Pos2, 3f, 7f), "fiend");
    }
    private void Shock()
    {
        Mech("shock");
        Face();
        M.Busy(1.3f, null);
        M.Model?.Play("Sword_Attack", 0.7f, 0.1f, true);
        Vector2 d = ToHero;
        E.LaneFan(M.Pos2, d, 2, 90f, 16f, 1.8f, 1.1f, 1f, Element.Fire, St.Stun(0.5f));
        E.LineBlast(M.Pos2, d, 16f, 1.8f, 1.1f, 1f, Element.Fire, St.Stun(0.5f));
    }
    public override void Killed(Monster m)
    {
        if (!E.Alive || m == null || m.Def.Id == "named_prop" || ReferenceEquals(m, M)) return;
        string key = m.Def.Id + "@" + m.GetInstanceId();
        if (m.Def.Family != "forge" && m.Def.Id != "forge_thrall" && m.Def.Id != "fiend") return;
        if (_risen.Contains(key) || m.Pos2.DistanceTo(M.Pos2) > 20f) return;
        Mech("standup");
        Vector2 at = m.Pos2; string id = m.Def.Id;
        E.After(1.2f, () =>
        {
            if (!E.Alive) return;
            var r = E.Summon(id, at, "fiend", true, 0.4f);
            if (r != null) _risen.Add(r.Def.Id + "@" + r.GetInstanceId());
        });
    }
}

/// <summary>長子火: three cinderlings every 8 s (cap 9); at 50 % it calls them home — each that reaches it heals 4 % and adds a ring to its next eruption.</summary>
internal sealed class FirstBorn : RareScript
{
    private bool _home; private int _rings; private float _homeT;
    public override void Setup() { _home = false; _homeT = 0f; _rings = 0; }
    public override void Build()
    {
        E.Every(8f, () => { if (E.AliveAdds() >= 9) return; Mech("summon"); for (int i = 0; i < 3; i++) E.SummonAfter(0.7f, "cinderling", E.Near(M.Pos2, 2f, 5f), "imp", null, 0.5f); }, 1.5f);
        E.AtLife(() => { _home = true; _homeT = 8f; Mech("home"); E.Toast("長子火召回火種——在半路截住它們！"); E.After(7f, Erupt); }, 0.5f);
        E.Every(14f, Erupt, 18f, () => _home && _homeT <= 0f);
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (!_home || _homeT <= 0f) return;
        _homeT -= dt;
        foreach (var a in E.Adds.ToArray())
        {
            if (a == null || !GodotObject.IsInstanceValid(a) || a.Dead) continue;
            if (a.Has(StatusId.Freeze)) continue;
            if (E.Drive(a, M.Pos2, dt, 1.1f, 1.4f)) { _rings++; E.HealNamed(0.04f); a.NoLoot = true; World.Monsters.Remove(a); a.QueueFree(); }
        }
    }
    private void Erupt()
    {
        if (!E.Alive) return;
        Mech("eruption");
        int n = 1 + _rings; _rings = 0;
        E.Waves(M.Pos2, Mathf.Min(n, 6), 2f, 3f, 1.2f, new[] { 0.6f }, 0.9f, Element.Fire, St.Burn(0.4f, 3f));
    }
}

/// <summary>白潮: a 9 m absorb field for 4 s pulls every projectile into it and releases a white ring scaled by the count (cap ×3); rib-walkers pour out while it inhales, two per second.</summary>
internal sealed class WhiteTide : RareScript
{
    private float _absorb; private int _eaten; private float _walkers;
    public override void Build() => E.Every(11f, () => { Mech("absorb"); _absorb = 4f; _eaten = 0; _walkers = 0f; E.Mark(M.Pos2, 9f, new Color(0.95f, 0.95f, 1f), 4f); E.Toast("所有的聲音都被吸了進去"); }, 3f);
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_absorb <= 0f) return;
        _absorb -= dt;
        Hold();
        foreach (var s in NamedHub.AllShots().ToArray())
        {
            Vector2 sp = NamedHub.ShotPos(s);
            if (sp.DistanceTo(M.Pos2) > 9f) { NamedHub.SetShotDir(s, M.Pos2 - sp); continue; }
            NamedHub.Kill(s); _eaten++;
        }
        _walkers -= dt;
        if (_walkers <= 0f) { _walkers = 0.5f; Mech("walkers"); if (E.AliveAdds() < 10) E.Summon("hush_ribwalker", E.Near(M.Pos2, 1f, 2.5f), "wraith", true, 0.3f); }
        if (_absorb <= 0f)
        {
            Mech("release");
            float mul = Mathf.Clamp(1f + _eaten * 0.25f, 1f, 3f);
            E.RingBlast(M.Pos2, 0f, 9f, 0.8f, 0.8f * mul, Element.Shadow);
        }
    }
    private void Hold() => M.Busy(0.2f, null);
}

/// <summary>渣河擺渡人: stands in the lava out of melee reach and flings five-circle slag volleys; every 20 s he wades ashore, scoops the hero onto a marked island ringed with fire, and stays ashore, vulnerable, for 8 s.</summary>
internal sealed class SlagFerry : RareScript
{
    private Vector2 _river; private float _ashore;
    public override void Setup() { _river = E.Home; }
    public override void Build()
    {
        E.Every(5f, Volley, 2f, () => _ashore <= 0f && E.HeroOk);
        E.Every(20f, Wade, 12f);
    }
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_ashore > 0f) { _ashore -= dt; if (_ashore <= 0f) { M.DrMul = 1f; } return; }
        // in the channel: no melee reaches him there
        M.DrMul = E.HeroDist < 4f ? 0.5f : 1f;
        E.Drive(M, _river, dt, 1f, 1f);
        M.Busy(0.2f, null);
        if (E.HeroOk) M.FaceDir(E.HeroPos - M.Pos2, 0.2f);
    }
    private void Volley()
    {
        Mech("volley");
        M.Model?.Play("OverhandThrow", 1f, 0.1f, true);
        for (int i = 0; i < 5; i++) E.Blast(E.Near(E.HeroPos, 0f, 4.5f), 1.8f, 1.1f + i * 0.15f, 0.7f, Element.Fire, St.Burn(0.3f, 2f), null, true);
    }
    private void Wade()
    {
        if (!E.HeroOk) return;
        Mech("wade");
        Vector2 bank = E.Walk(E.HeroPos + E.DirToHero(E.HeroPos) * 0f);
        E.Track(Fx.Telegraph(E.HeroPos, 2f, 1.4f, Palette.Danger, M.Def.Element));
        M.Busy(1.4f, null);
        E.Then(1.4f, () =>
        {
            if (!E.Alive) return;
            M.Pos2 = E.Walk(E.HeroPos + (M.Pos2 - E.HeroPos).Normalized() * 1.8f);
            if (E.HeroIn(M.Pos2, 3.5f))
            {
                Mech("island");
                Vector2 island = E.Near(E.Center, 6f, 10f);
                E.Drag(island, 0f);
                E.After(0.8f, () => { for (int i = 0; i < 6; i++) E.Zone(island + Vector2.Right.Rotated(i * Mathf.Tau / 6f) * 3.6f, 1.3f, 6f, 0.15f, Element.Fire, St.Burn(0.4f, 2f)); });
                E.Mark(island, 3f, Palette.Ember, 6f);
            }
            _ashore = 8f; M.DrMul = 1.3f;
            E.Toast("擺渡人上岸了——他現在打得到！");
        });
    }
}
