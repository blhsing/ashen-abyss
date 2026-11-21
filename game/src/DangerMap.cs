using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

internal enum DangerKind { Circle, Cone, Line, Ring }

/// <summary>One hostile ground warning: its shape, when it lands and how long it stays dangerous after that.</summary>
internal sealed class Danger
{
    public DangerKind Kind;
    public Vector2 C, Dir;                  // centre (circle, ring), apex (cone) or start (line); unit direction
    public float R, Inner, HalfArc, HalfW;  // radius / outer radius / length; ring hole; half cone angle (rad); half line width
    public double Born, Land, Until;        // game time (Game.Time)
    public Node3D Node;                     // the mark; freed before it lands = the move was cancelled
    public bool Boss;                       // laid down while a boss or named leader fights the hero
    public bool Minor;                      // the swing of a plain monster (a small cone): not worth giving up the fight for in a pack
    public bool Threat, Hit, Counted;       // statistics: the hero stood in it before it landed / was struck by it
    public Node Root;                       // the level it belongs to

    public bool Contains(Vector2 p, float pad)
    {
        switch (Kind)
        {
            case DangerKind.Circle:
                return p.DistanceSquaredTo(C) <= (R + pad) * (R + pad);
            case DangerKind.Ring:
            {
                float d = p.DistanceTo(C);
                return d <= R + pad && d >= Inner - pad;
            }
            case DangerKind.Cone:
            {
                Vector2 v = p - C;
                float d = v.Length();
                if (d > R + pad) return false;
                if (d < 0.8f + pad || HalfArc >= Mathf.Pi - 0.01f) return true;          // the apex (the monster's own body)
                float ang = Mathf.Abs(Dir.AngleTo(v));
                if (ang <= HalfArc) return true;
                // distance from the cone's edge ray
                if (ang - HalfArc >= Mathf.Pi * 0.5f) return false;
                return d * Mathf.Sin(ang - HalfArc) <= pad;
            }
            default:
            {
                Vector2 v = p - C;
                float along = v.Dot(Dir);
                if (along < -pad || along > R + pad) return false;
                float side = Mathf.Abs(v.X * Dir.Y - v.Y * Dir.X);
                return side <= HalfW + pad;
            }
        }
    }

    /// <summary>Signed-ish distance to get out: how far the nearest safe ground is from <paramref name="p"/> (0 when outside).</summary>
    public float EscapeDist(Vector2 p)
    {
        if (!Contains(p, 0f)) return 0f;
        switch (Kind)
        {
            case DangerKind.Circle: return R - p.DistanceTo(C);
            case DangerKind.Ring:
            {
                float d = p.DistanceTo(C);
                return Mathf.Min(R - d, d - Inner);
            }
            case DangerKind.Cone:
            {
                Vector2 v = p - C;
                float d = v.Length();
                float ang = Mathf.Abs(Dir.AngleTo(v));
                float side = HalfArc >= Mathf.Pi - 0.01f ? float.MaxValue : d * Mathf.Sin(Mathf.Min(Mathf.Pi * 0.5f, HalfArc - ang + 0.0001f));
                return Mathf.Min(R - d, side);
            }
            default:
            {
                Vector2 v = p - C;
                float side = Mathf.Abs(v.X * Dir.Y - v.Y * Dir.X);
                return HalfW - side;
            }
        }
    }

    public string Describe() => Kind switch
    {
        DangerKind.Circle => $"circle r{R:0.0}",
        DangerKind.Ring => $"ring {Inner:0.0}-{R:0.0}",
        DangerKind.Cone => $"cone r{R:0.0} {Mathf.RadToDeg(HalfArc) * 2f:0}°",
        _ => $"line {R:0.0}x{HalfW * 2f:0.0}",
    };
}

/// <summary>
/// AI pilot: a live register of every hostile ground warning (Fx.Telegraph / TelegraphCone / TelegraphLine /
/// TelegraphRing record themselves here), plus read-only views of hostile shots in flight and hostile ground pools.
/// The pilot asks "where will each of these hit, and when" to pick safe ground. It also keeps the statistics of the
/// telegraphed hits the hero stood in: taken (struck) or dodged (left in time or rolled through with i-frames).
/// Nothing here changes the game: it only reads.
/// </summary>
internal static class DangerMap
{
    public static readonly List<Danger> Live = new();
    private static Danger _pending;          // registered by an Fx.Telegraph* call, waiting for its mark (Bind)
    private static bool _hooked;
    private static double _bossSeen = -100;

    // statistics since the last Reset (the pilot test harness reads them)
    public static int Threats, Taken, Dodged, BossThreats, BossTaken, BossDodged, OtherHits;
    public static bool LogHits;           // the pilot test harness logs every marked hit
    private static int _hitLogs;
    public static double TakenDmg, OtherDmg;

    public static double Now => Game.I?.Time ?? 0;
    /// <summary>Lines stay dangerous a little after they land: a charge still has to run down its lane.</summary>
    public const float LineLinger = 0.55f;

    public static void Reset()
    {
        Threats = Taken = Dodged = BossThreats = BossTaken = BossDodged = OtherHits = 0;
        TakenDmg = OtherDmg = 0;
    }

    private static Danger New(DangerKind k, float delay)
    {
        if (Game.I == null || World.Root == null) return null;
        Hook();
        double now = Now;
        var d = new Danger { Kind = k, Born = now, Land = now + Mathf.Max(delay, 0.05f), Root = World.Root, Boss = BossFight() };
        d.Until = d.Land + 0.05;
        if (Live.Count > 400) Live.RemoveAt(0);
        Live.Add(d);
        _pending = d;
        return d;
    }

    public static void Circle(Vector2 c, float r, float delay)
    {
        try { var d = New(DangerKind.Circle, delay); if (d != null) { d.C = c; d.R = Mathf.Max(r, 0.3f); } }
        catch (Exception e) { Log.Error("danger", "circle not recorded", e); }
    }

    public static void Cone(Vector2 apex, Vector2 dir, float range, float arcDeg, float delay, Actor owner)
    {
        try
        {
            var d = New(DangerKind.Cone, delay);
            if (d == null) return;
            d.C = apex; d.Dir = dir.LengthSquared() < 1e-6f ? new Vector2(0f, 1f) : dir.Normalized();
            d.R = Mathf.Max(range, 0.5f);
            d.HalfArc = arcDeg >= 359f ? Mathf.Pi : Mathf.DegToRad(Mathf.Clamp(arcDeg, 5f, 358f) * 0.5f);
            if (owner is Monster m && m.Brain != null) d.Boss = true;
            else if (owner is Monster mm && !mm.IsElite && !mm.MiniBoss && d.R <= 5f && (mm.Def == null || mm.Def.Dmg <= 1.15f)) d.Minor = true;      // heavy hitters (knights, brutes) are always dodged
        }
        catch (Exception e) { Log.Error("danger", "cone not recorded", e); }
    }

    public static void Line(Vector2 from, Vector2 dir, float length, float width, float delay)
    {
        try
        {
            var d = New(DangerKind.Line, delay);
            if (d == null) return;
            d.C = from; d.Dir = dir.LengthSquared() < 1e-6f ? new Vector2(0f, 1f) : dir.Normalized();
            d.R = Mathf.Max(length, 0.5f); d.HalfW = Mathf.Max(width, 0.3f) * 0.5f;
            d.Until = d.Land + LineLinger;
        }
        catch (Exception e) { Log.Error("danger", "line not recorded", e); }
    }

    public static void Ring(Vector2 c, float inner, float outer, float delay)
    {
        try
        {
            var d = New(DangerKind.Ring, delay);
            if (d == null) return;
            d.C = c; d.R = Mathf.Max(outer, 0.5f); d.Inner = Mathf.Clamp(inner, 0f, d.R);
        }
        catch (Exception e) { Log.Error("danger", "ring not recorded", e); }
    }

    /// <summary>Called with the mark an Fx.Telegraph* call returns: ties it to the danger that call registered.</summary>
    public static Node3D Bind(Node3D node)
    {
        if (_pending != null) { _pending.Node = node; _pending = null; }
        return node;
    }

    /// <summary>A boss or named leader is fighting nearby (its marks count as the big ones).</summary>
    public static bool BossFight()
    {
        var p = World.P;
        if (p == null || !GodotObject.IsInstanceValid(p)) return false;
        if (Now - _bossSeen < 1.0) return true;
        var list = World.Monsters;
        Vector2 me = p.Pos2;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || m.Brain == null || !GodotObject.IsInstanceValid(m)) continue;
            if (m.Aggro && m.Pos2.DistanceSquaredTo(me) < 30f * 30f) { _bossSeen = Now; return true; }
        }
        return false;
    }

    /// <summary>The mark of this danger was taken away before it landed (the monster died or was staggered).</summary>
    public static bool Cancelled(Danger d, double now)
    {
        if (now >= d.Land) return false;
        var n = d.Node;
        return n != null && (!GodotObject.IsInstanceValid(n) || n.IsQueuedForDeletion());
    }

    /// <summary>Per frame (the pilot calls it): drops what is over, counts what the hero stood in and whether it struck.</summary>
    public static void Tick(Vector2 hero, float pad)
    {
        double now = Now;
        var root = World.Root;
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            var d = Live[i];
            bool gone = d.Root != root || Cancelled(d, now);
            if (!gone && now <= d.Until && d.Contains(hero, pad)) d.Threat = true;
            if (gone || now > d.Until + 0.35)
            {
                if (!gone && d.Threat && !d.Counted)
                {
                    d.Counted = true;
                    Threats++; if (d.Boss) BossThreats++;
                    if (d.Hit) { Taken++; if (d.Boss) BossTaken++; }
                    else { Dodged++; if (d.Boss) BossDodged++; }
                }
                Live.RemoveAt(i);
            }
        }
    }

    private static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        World.Damage += OnDamage;
    }

    private static void OnDamage(Actor a, Hit h, float amount)
    {
        if (amount <= 0f || a is not Player p || !GodotObject.IsInstanceValid(p)) return;
        try
        {
            double now = Now;
            Vector2 at = p.Pos2;
            bool tele = false;
            for (int i = 0; i < Live.Count; i++)
            {
                var d = Live[i];
                if (now < d.Land - 0.08 || now > d.Until + 0.12 || !d.Contains(at, p.Radius + 0.35f)) continue;
                if (!d.Hit && LogHits && _hitLogs++ < 400) Log.Info("danger", $"marked hit {amount:0} ({amount / Mathf.Max(1f, p.MaxLife) * 100f:0}% life) by {d.Describe()}{(d.Boss ? " [boss]" : "")}: warned {d.Land - d.Born:0.00}s ahead, hero {d.EscapeDist(at):0.0} m inside, roll cd {p.EvadeCd:0.0}s, busy={p.Busy}{(p.Stunned ? " FROZEN/STUNNED" : p.Rooted ? " ROOTED" : "")}");
                d.Hit = true; d.Threat = true; tele = true;
            }
            if (tele) TakenDmg += amount;
            else { OtherHits++; OtherDmg += amount; }
            Remember(h, amount, tele);
        }
        catch (Exception e) { Log.Error("danger", "hit not attributed", e); }
    }


    // ── recent hits on the hero: damage intake for the potion and retreat decisions, and the death report ──
    private struct Recent { public double T; public float Amount; public string Who; public bool Tele; }
    private static readonly List<Recent> _recent = new(64);

    private static void Remember(Hit h, float amount, bool tele)
    {
        string who = h.Source is Monster m && GodotObject.IsInstanceValid(m) ? (m.Def?.Id ?? "?") + (m.Brain != null ? "(boss)" : m.IsElite ? "(elite)" : "") : h.Dot ? "(dot)" : "(other)";
        _recent.Add(new Recent { T = Now, Amount = amount, Who = who + (h.Skill != null ? ":" + h.Skill.Id : ""), Tele = tele });
        if (_recent.Count > 60) _recent.RemoveAt(0);
    }

    /// <summary>Damage the hero took in the last <paramref name="secs"/> seconds.</summary>
    public static float Intake(double secs)
    {
        double now = Now; float s = 0f;
        for (int i = _recent.Count - 1; i >= 0 && now - _recent[i].T <= secs; i--) s += _recent[i].Amount;
        return s;
    }

    /// <summary>The last seconds before a death: who hit for how much (marks flagged).</summary>
    public static string DeathReport(double secs = 6)
    {
        double now = Now;
        var by = new Dictionary<string, (int n, float dmg, int tele)>();
        foreach (var r in _recent)
        {
            if (now - r.T > secs) continue;
            var v = by.GetValueOrDefault(r.Who);
            by[r.Who] = (v.n + 1, v.dmg + r.Amount, v.tele + (r.Tele ? 1 : 0));
        }
        var parts = new List<string>();
        foreach (var kv in by) parts.Add($"{kv.Key} x{kv.Value.n} {kv.Value.dmg:0}{(kv.Value.tele > 0 ? $" ({kv.Value.tele} marked)" : "")}");
        parts.Sort();
        return string.Join(", ", parts);
    }
    public static string Stats() =>
        $"telegraphed threats={Threats} dodged={Dodged} taken={Taken} (boss {BossThreats}/{BossDodged}/{BossTaken}) dmgTelegraphed={TakenDmg:0} otherHits={OtherHits} dmgOther={OtherDmg:0}";
}
