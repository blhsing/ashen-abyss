using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// More mechanic blocks of <see cref="NamedEncounter"/>: taking control of a monster (rares keep their own AI; a
/// script borrows the body for a cast, a walk or a hide), exposure windows, directional rules, forced kills, and a
/// few shapes every second encounter needs (fan of lanes, a moving shield of orbs, an aura).
/// </summary>
public partial class NamedEncounter
{
    // ───────────────────────── control of a body ─────────────────────────

    /// <summary>The script holds the monster this frame: its own AI (or brain) does nothing until released.</summary>
    public void Hold(Monster m, float seconds = 0.2f)
    {
        if (m == null || !IsInstanceValid(m) || m.Dead) return;
        if (m.Brain != null && !m.Brain.Scripted) { m.Brain.Suspend(seconds); return; }
        if (!m.Attacking || seconds > 0.25f) m.Busy(seconds, null);
    }

    /// <summary>Walks a monster toward a point this frame (on the grid; it plays its run clip). True when it has arrived.</summary>
    public bool Drive(Monster m, Vector2 to, float dt, float speedMul = 1f, float stop = 1.2f)
    {
        if (m == null || !IsInstanceValid(m) || m.Dead) return true;
        Vector2 d = to - m.Pos2;
        float dist = d.Length();
        if (dist <= stop) return true;
        if (m.Brain != null && !m.Brain.Scripted) { m.Brain.Suspend(0.25f); m.BossMove(to, dt, stop); return false; }
        m.Busy(0.25f, null);
        float speed = m.Def.Speed * m.SpeedMul * m.SlowMul * speedMul;
        Vector2 step = d / dist * Mathf.Min(dist, speed * dt);
        m.Move(step);
        m.FaceDir(d, 1f - Mathf.Exp(-10f * dt));
        m.Model?.Play(speed > 4.5f ? m.Def.Run : m.Def.Walk, 1f, 0.2f);
        return false;
    }

    /// <summary>Puts a monster on a short cast: it stands, faces the hero and plays a clip for <paramref name="wind"/> seconds, then <paramref name="then"/> runs.</summary>
    public void Cast(Monster m, float wind, Action then, string anim = "Spell_Simple_Shoot")
    {
        if (m == null || !IsInstanceValid(m) || m.Dead) return;
        if (HeroOk) m.FaceDir(HeroPos - m.Pos2);
        if (m.Brain != null && !m.Brain.Scripted) m.Brain.Suspend(wind + 0.2f);
        else m.Busy(wind, null);
        m.Model?.Play(anim, Mathf.Clamp(0.5f / Mathf.Max(0.3f, wind), 0.4f, 1.6f), 0.1f, true);
        Then(wind, then);
    }

    /// <summary>Hidden and untouchable (burrowed, ascended, cloaked) or back.</summary>
    public void Hide(Monster m, bool hidden)
    {
        if (m == null || !IsInstanceValid(m)) return;
        m.Visible = !hidden;
        m.Untargetable = hidden;
    }

    /// <summary>A stagger window: the monster stands helpless and takes more damage (the engine's own stagger, so the HUD and the brain agree).</summary>
    public void Expose(Monster m, float seconds, bool toast = true)
    {
        if (m == null || !IsInstanceValid(m) || m.Dead) return;
        m.Stagger = 100f; m.StaggerLeft = Mathf.Max(m.StaggerLeft, seconds); m.StaggerImmune = 0f;
        m.Brain?.Interrupt();
        m.ApplyStatus(St.Vuln(seconds), null);
        Fx.Burst(m.Chest, Element.Lightning, 3f, 1f);
        Snd.At("stagger_break", m.GlobalPosition);
        if (toast && ReferenceEquals(m, M)) Game.I?.Hud?.Toast($"{Def.NameZh}露出破綻！", Palette.Gold);
    }

    /// <summary>A hard stun that ignores the short immunity after crowd control (used by the encounter on its own monster).</summary>
    public static void StunMon(Monster m, float seconds)
    {
        if (m == null || !IsInstanceValid(m) || m.Dead) return;
        m.StTime[(int)StatusId.Stun] = Mathf.Max(m.StTime[(int)StatusId.Stun], seconds);
        m.Brain?.Interrupt();
    }

    /// <summary>Ends the named monster now, as if the hero had struck the last blow (a broken reform, a crushed core).</summary>
    public void KillNamed()
    {
        if (!Alive) return;
        Floor = 0f;
        M.Untargetable = false; M.Visible = true;
        M.DrMul = 1f;
        M.TakeHit(new Hit { Amount = M.Life + M.Barrier + 1f, Source = World.P, El = Element.Physical, NoReact = true });
    }

    /// <summary>Takes a share of the named monster's life (a self-inflicted burst, a falling tower).</summary>
    public void HurtNamed(float frac, Element el = Element.Physical)
    {
        if (!Alive) return;
        M.TakeHit(new Hit { Amount = M.MaxLife * frac, Source = World.P, El = el, NoReact = true });
    }

    public void HealNamed(float frac)
    {
        if (!Alive) return;
        M.Life = Mathf.Min(M.MaxLife, M.Life + M.MaxLife * frac);
        Fx.Heal(M);
    }

    // ───────────────────────── directions ─────────────────────────

    /// <summary>True when <paramref name="from"/> lies behind the monster (outside its front half-cone of <paramref name="cosFront"/>).</summary>
    public static bool Behind(Monster m, Vector2 from, float cosFront = -0.15f)
    {
        Vector2 d = from - m.Pos2;
        if (d.LengthSquared() < 0.01f) return false;
        return m.Forward.Dot(d.Normalized()) < cosFront;
    }

    public static bool InFront(Monster m, Vector2 from, float cosFront = 0.2f)
    {
        Vector2 d = from - m.Pos2;
        if (d.LengthSquared() < 0.01f) return true;
        return m.Forward.Dot(d.Normalized()) > cosFront;
    }

    // ───────────────────────── shapes ─────────────────────────

    /// <summary><paramref name="n"/> marked lanes from a point, fanned round a direction.</summary>
    public void LaneFan(Vector2 from, Vector2 dir, int n, float spreadDeg, float len, float width, float wind, float mul, Element el, StatusApply? st = null, Action<int> each = null)
    {
        dir = dir.LengthSquared() < 1e-4f ? Vector2.Down : dir.Normalized();
        for (int i = 0; i < n; i++)
        {
            int k = i;
            float a = n == 1 ? 0f : Mathf.DegToRad(-spreadDeg * 0.5f + spreadDeg * i / (n - 1));
            LineBlast(from, dir.Rotated(a), len, width, wind, mul, el, st, () => each?.Invoke(k));
        }
    }

    /// <summary>True when the hero stood inside a marked lane (call when the lane goes off).</summary>
    public bool HeroInLane(Vector2 from, Vector2 dir, float len, float width)
    {
        if (!HeroOk) return false;
        dir = dir.Normalized();
        Vector2 rel = HeroPos - from;
        float along = rel.Dot(dir);
        if (along < -0.5f || along > len + 0.5f) return false;
        return Mathf.Abs(rel.Dot(new Vector2(-dir.Y, dir.X))) <= width * 0.5f + World.P.Radius;
    }

    /// <summary>A circle of small glowing orbs round a monster (shield flickers, jars, discs); returns the nodes to steer.</summary>
    public List<Node3D> Orbit(Monster m, int n, Color c, float size = 0.35f)
    {
        var list = new List<Node3D>();
        if (m == null) return list;
        for (int i = 0; i < n; i++)
        {
            var o = NamedProps.Orb(c, size, 0f);
            m.AddChild(o);
            list.Add(o);
        }
        return list;
    }

    /// <summary>Lays orbs out on a ring of <paramref name="radius"/> (monster-local), rotating with time.</summary>
    public static void SpinOrbs(List<Node3D> orbs, float radius, float y, float speed, float t, Func<int, bool> visible = null)
    {
        int n = orbs.Count;
        for (int i = 0; i < n; i++)
        {
            var o = orbs[i];
            if (o == null || !IsInstanceValid(o)) continue;
            float a = t * speed + i * Mathf.Tau / n;
            o.Position = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
            o.Visible = visible == null || visible(i);
        }
    }

    /// <summary>Hits the hero within <paramref name="r"/> of a point with a status (no damage roll worth speaking of).</summary>
    public bool StatusIn(Vector2 c, float r, StatusApply st, float mul = 0.05f, Element el = Element.Shadow)
    {
        if (!HeroIn(c, r)) return false;
        Hit(World.P, mul, el, st);
        return true;
    }

    /// <summary>Nearest living add (optionally matching), or null.</summary>
    public Monster NearestAdd(Vector2 p, Func<Monster, bool> filter = null)
    {
        Monster best = null; float bd = float.MaxValue;
        foreach (var a in Adds)
        {
            if (a == null || !IsInstanceValid(a) || a.Dead || (filter != null && !filter(a))) continue;
            float d = a.Pos2.DistanceSquaredTo(p);
            if (d < bd) { bd = d; best = a; }
        }
        return best;
    }

    public Monster NearestProp(Vector2 p, Func<Monster, bool> filter = null)
    {
        Monster best = null; float bd = float.MaxValue;
        foreach (var a in Props)
        {
            if (a == null || !IsInstanceValid(a) || a.Dead || (filter != null && !filter(a))) continue;
            float d = a.Pos2.DistanceSquaredTo(p);
            if (d < bd) { bd = d; best = a; }
        }
        return best;
    }

    public int AliveProps(Func<Monster, bool> filter = null)
    {
        int n = 0;
        foreach (var a in Props) if (a != null && IsInstanceValid(a) && !a.Dead && (filter == null || filter(a))) n++;
        return n;
    }

    /// <summary>Fraction of the hero's maximum life, for things the hero must break by damage ("10 % of the hero's life").</summary>
    public float HeroLifeFrac(float f) => HeroOk ? World.P.MaxLife * f : 100f;

    /// <summary>Hero minions inside a circle.</summary>
    public IEnumerable<Monster> MinionsIn(Vector2 c, float r)
    {
        foreach (var m in World.Minions.ToArray()) if (m != null && IsInstanceValid(m) && !m.Dead && m.Pos2.DistanceTo(c) <= r) yield return m;
    }

    /// <summary>All living hostile monsters within a radius (adds, props excluded).</summary>
    public IEnumerable<Monster> HostilesIn(Vector2 c, float r)
    {
        foreach (var m in World.Monsters.ToArray())
            if (m != null && IsInstanceValid(m) && !m.Dead && m.Def.Id != "named_prop" && m.Pos2.DistanceTo(c) <= r) yield return m;
    }

    /// <summary>A shaft of light on the ground showing a safe or lit place (a visual only).</summary>
    public Node3D Mark(Vector2 at, float radius, Color c, float seconds = 0f)
    {
        var n = NamedProps.Place(NamedProps.Ring(radius, c, 2f), at);
        Own(n);
        if (seconds > 0f) After(seconds, () => { if (IsInstanceValid(n)) n.QueueFree(); });
        return n;
    }
}
