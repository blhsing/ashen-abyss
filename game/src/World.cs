using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// The walkable map of a level: 2 m cells (floor / wall / pit) plus round obstacles (pillars, trees, props).
/// Handles collision resolution, line-of-sight and A* pathfinding for every actor.
/// </summary>
public sealed class NavGrid
{
    public const float Cell = 2f;
    public const byte Floor = 0, Wall = 1, Pit = 2;
    public readonly int W, H;
    public readonly Vector2 Origin;
    public readonly byte[] T;
    private readonly List<(Vector2 p, float r)>[] _obs;
    private AStarGrid2D _astar;

    public NavGrid(int w, int h, Vector2 origin)
    {
        W = w; H = h; Origin = origin;
        T = new byte[w * h];
        _obs = new List<(Vector2, float)>[w * h];
        Array.Fill(T, Wall);
    }

    public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
    public byte Get(int x, int y) => In(x, y) ? T[y * W + x] : Wall;
    public void Set(int x, int y, byte v) { if (In(x, y)) T[y * W + x] = v; }
    public bool Walk(int x, int y) => In(x, y) && T[y * W + x] == Floor;
    public Vector2I CellOf(Vector2 p) => new(Mathf.FloorToInt((p.X - Origin.X) / Cell), Mathf.FloorToInt((p.Y - Origin.Y) / Cell));
    public Vector2 Center(int x, int y) => Origin + new Vector2((x + 0.5f) * Cell, (y + 0.5f) * Cell);
    public Vector2 Center(Vector2I c) => Center(c.X, c.Y);
    public bool WalkAt(Vector2 p) { var c = CellOf(p); return Walk(c.X, c.Y); }
    public Vector2 Size => new(W * Cell, H * Cell);

    public const float SolidRadius = 0.85f;      // props this big fill their cell for path planning
    private bool[] _solid;

    public void AddObstacle(Vector2 p, float r)
    {
        var c = CellOf(p);
        if (!In(c.X, c.Y)) return;
        (_obs[c.Y * W + c.X] ??= new()).Add((p, r));
        if (r >= SolidRadius)
        {
            (_solid ??= new bool[W * H])[c.Y * W + c.X] = true;
            if (_astar != null && T[c.Y * W + c.X] == Floor) _astar.SetPointSolid(c, true);
        }
    }

    /// <summary>True when a big prop fills this cell for path planning (the hero's route goes round it).</summary>
    public bool Solid(int x, int y) => _solid != null && In(x, y) && _solid[y * W + x];

    /// <summary>
    /// Makes a cell walk-through again: every prop obstacle in it stops colliding (the prop itself stays visible).
    /// Used when a prop turned out to wall off part of the level. Returns how many obstacles were removed.
    /// </summary>
    public int ClearProps(int x, int y)
    {
        if (!In(x, y)) return 0;
        int i = y * W + x, n = _obs[i]?.Count ?? 0;
        _obs[i]?.Clear();
        if (_solid != null) _solid[i] = false;
        if (_astar != null && T[i] == Floor) _astar.SetPointSolid(new Vector2I(x, y), false);
        return n;
    }

    /// <summary>True for a walkable cell with no prop standing in it.</summary>
    public bool Clear(int x, int y) => Walk(x, y) && (_obs[y * W + x] == null || _obs[y * W + x].Count == 0);

    public void RemoveObstacle(Vector2 p)
    {
        var c = CellOf(p);
        if (!In(c.X, c.Y)) return;
        int i = c.Y * W + c.X;
        if (_obs[i] == null) return;
        _obs[i].RemoveAll(o => o.p.DistanceSquaredTo(p) < 0.01f);
        if (_solid == null || !_solid[i]) return;
        bool still = false;
        foreach (var o in _obs[i]) if (o.r >= SolidRadius) { still = true; break; }
        _solid[i] = still;
        if (!still && _astar != null && T[i] == Floor) _astar.SetPointSolid(c, false);
    }

    public void BuildPathing()
    {
        _astar = new AStarGrid2D
        {
            Region = new Rect2I(0, 0, W, H),
            CellSize = new Vector2(Cell, Cell),
            DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles,
            DefaultComputeHeuristic = AStarGrid2D.Heuristic.Octile,
            DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Octile,
        };
        _astar.Update();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                bool solid = T[y * W + x] != Floor;
                if (!solid && _obs[y * W + x] != null)
                    foreach (var o in _obs[y * W + x]) if (o.r >= 0.85f) { solid = true; break; }   // big obstacles block the whole cell for pathing
                if (solid) _astar.SetPointSolid(new Vector2I(x, y), true);
            }
    }

    /// <summary>Pushes a circle out of walls, pits and obstacles. If the target is inside a blocked cell the move is refused.</summary>
    public Vector2 ResolveCircle(Vector2 to, float r, Vector2 from)
    {
        Vector2 step = to - from;
        float stepLen = step.Length();
        var c0 = CellOf(to);
        if (!Walk(c0.X, c0.Y))
        {
            // slide along one axis if possible
            var tx = new Vector2(to.X, from.Y); var cx = CellOf(tx);
            var ty = new Vector2(from.X, to.Y); var cy = CellOf(ty);
            if (Walk(cx.X, cx.Y)) to = tx; else if (Walk(cy.X, cy.Y)) to = ty; else return from;
            c0 = CellOf(to);
        }
        for (int pass = 0; pass < 2; pass++)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = c0.X + dx, y = c0.Y + dy;
                    if (!In(x, y) || T[y * W + x] != Floor)
                    {
                        float minX = Origin.X + x * Cell, minY = Origin.Y + y * Cell;
                        float px = Mathf.Clamp(to.X, minX, minX + Cell), py = Mathf.Clamp(to.Y, minY, minY + Cell);
                        var d = to - new Vector2(px, py);
                        float len = d.Length();
                        if (len < r && len > 1e-5f) to += d / len * (r - len);
                    }
                    else if (_obs[y * W + x] is { } list)
                    {
                        foreach (var o in list)
                        {
                            var d = to - o.p;
                            float len = d.Length(), min = r * 0.7f + o.r;
                            if (len >= min || len <= 1e-5f) continue;
                            Vector2 n = d / len;
                            to += n * (min - len);
                            // Walking straight at a round prop used to stop the walker dead: the push-out is radial, and
                            // props and path points both sit on cell centres, so head-on was the usual case. Slide round it.
                            if (pass == 0 && stepLen > 1e-5f && -(step / stepLen).Dot(n) > 0.8f)
                            {
                                var tan = new Vector2(-n.Y, n.X);
                                float side = step.Dot(tan) >= -1e-4f * stepLen ? 1f : -1f;      // keep going round the side it is already on
                                to += tan * side * stepLen * 0.85f;
                            }
                        }
                    }
                }
        }
        return to;
    }

    private bool Trace(Vector2 a, Vector2 b, bool shot, bool props = false)
    {
        float dist = a.DistanceTo(b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / (Cell * 0.45f)));
        var start = CellOf(a);
        for (int i = 1; i <= steps; i++)
        {
            var c = CellOf(a.Lerp(b, i / (float)steps));
            byte t = Get(c.X, c.Y);
            if (t == Wall || (!shot && t == Pit)) return false;
            // a big prop (a tree, a statue, a stall) in the way: not a straight walk — the path goes round it
            if (props && _solid != null && c != start && _solid[c.Y * W + c.X]) return false;
        }
        return true;
    }

    /// <summary>True when an actor can walk straight from a to b (no wall, pit or big prop in between).</summary>
    public bool LineWalk(Vector2 a, Vector2 b) => Trace(a, b, false, true);
    /// <summary>True when a projectile or spell can travel from a to b (pits do not block).</summary>
    public bool LineShot(Vector2 a, Vector2 b) => Trace(a, b, true);

    public Vector2I NearestWalkCell(Vector2I c, int maxR = 8)
    {
        if (Walk(c.X, c.Y)) return c;
        for (int r = 1; r <= maxR; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    if (Walk(c.X + dx, c.Y + dy)) return new Vector2I(c.X + dx, c.Y + dy);
                }
        return c;
    }

    public Vector2 NearestWalk(Vector2 p) { var c = CellOf(p); return Walk(c.X, c.Y) ? p : Center(NearestWalkCell(c)); }

    /// <summary>Furthest point toward <paramref name="to"/> reachable in a straight line (for dashes, leaps and blinks).</summary>
    public Vector2 ClampLine(Vector2 from, Vector2 to, bool overPits)
    {
        float dist = from.DistanceTo(to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.5f));
        Vector2 last = from;
        for (int i = 1; i <= steps; i++)
        {
            var p = from.Lerp(to, i / (float)steps);
            var c = CellOf(p);
            byte t = Get(c.X, c.Y);
            if (t == Wall) break;
            if (t == Floor) last = p; else if (!overPits) break;
        }
        return last;
    }

    private readonly List<Vector2> _path = new();
    /// <summary>A* path as world points (already string-pulled). Empty if unreachable.</summary>
    public List<Vector2> Path(Vector2 from, Vector2 to)
    {
        _path.Clear();
        if (_astar == null) return _path;
        var a = NearestWalkCell(CellOf(from));
        var b = NearestWalkCell(CellOf(to));
        if (!In(a.X, a.Y) || !In(b.X, b.Y)) return _path;
        // standing beside a big prop, or heading for one (a cage, a statue): plan between the free cells next to them
        bool endSolid = _astar.IsPointSolid(b);
        if (_astar.IsPointSolid(a)) a = FreeNeighbour(a, b);
        if (endSolid) b = FreeNeighbour(b, a.X >= 0 ? a : CellOf(from));
        if (a.X < 0 || b.X < 0) { _path.Add(to); return _path; }
        var ids = _astar.GetIdPath(a, b);
        if (ids.Count == 0)
        {
            // no route round the props (one may fill a narrow passage): squeeze straight through if only props are in the way
            if (Trace(from, to, false)) _path.Add(to);
            return _path;
        }
        var pts = new List<Vector2>(ids.Count + 1);
        foreach (var id in ids) pts.Add(Center(id));
        if (endSolid) pts.Add(to);
        else if (WalkAt(to)) pts[^1] = to;
        // string pulling
        Vector2 cur = from;
        int i = 0;
        while (i < pts.Count)
        {
            int far = i;
            for (int j = pts.Count - 1; j > i; j--) if (LineWalk(cur, pts[j])) { far = j; break; }
            _path.Add(pts[far]);
            cur = pts[far];
            i = far + 1;
        }
        return _path;
    }

    private Vector2I FreeNeighbour(Vector2I c, Vector2I toward)
    {
        var best = new Vector2I(-1, -1); int bd = int.MaxValue;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var n = new Vector2I(c.X + dx, c.Y + dy);
                if (!Walk(n.X, n.Y) || _astar.IsPointSolid(n)) continue;
                int d = (n.X - toward.X) * (n.X - toward.X) + (n.Y - toward.Y) * (n.Y - toward.Y);
                if (d < bd) { bd = d; best = n; }
            }
        return best;
    }

    public Vector2 RandomWalkNear(Vector2 p, float rMin, float rMax, Random rng = null)
    {
        rng ??= Rng.R;
        for (int i = 0; i < 24; i++)
        {
            float ang = (float)rng.NextDouble() * Mathf.Tau, d = rMin + (rMax - rMin) * (float)rng.NextDouble();
            var q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * d;
            if (WalkAt(q) && LineWalk(p, q)) return q;
        }
        return p;
    }
}

public sealed class Remnant
{
    public Vector2 Pos;
    public float Left = 12f;
    public Node3D Fx;
}

/// <summary>Registry of what is alive in the current level, plus the spatial queries combat uses.</summary>
public static class World
{
    public static NavGrid Grid;
    public static Player P;
    public static Node3D Root;                                   // parent for spawned actors, projectiles and effects
    public static readonly List<Monster> Monsters = new();       // hostile
    public static readonly List<Monster> Minions = new();        // fighting for the player
    public static readonly List<Remnant> Remnants = new();
    public static event Action<Actor, Hit, float> Damage;
    public static event Action<Monster, Hit> MonsterKilled;
    /// <summary>Loose game events for quests, objectives, renown and statistics: (event, argument). See ARCHITECTURE.md.</summary>
    public static event Action<string, string> Event;
    public static void Emit(string evt, string arg = "")
    {
        try { Event?.Invoke(evt, arg ?? ""); }
        catch (Exception e) { Log.Error("event", $"handler for {evt}({arg}) failed", e); }
    }
    public static string FootstepSfx = "footstep_stone";

    public static void RaiseDamage(Actor a, Hit h, float amount) => Damage?.Invoke(a, h, amount);
    public static void RaiseKilled(Monster m, Hit h) => MonsterKilled?.Invoke(m, h);

    public static void Clear()
    {
        Monsters.Clear(); Minions.Clear();
        foreach (var r in Remnants) if (GodotObject.IsInstanceValid(r.Fx)) r.Fx.QueueFree();
        Remnants.Clear();
        Grid = null; Root = null;
    }

    private static bool Ok(Actor a) => a != null && !a.Dead && !a.Untargetable && GodotObject.IsInstanceValid(a);

    /// <summary>Fills <paramref name="res"/> with living enemies of <paramref name="attacker"/> matching the test.</summary>
    private static void Collect(Faction attacker, List<Actor> res, Func<Actor, bool> test)
    {
        res.Clear();
        if (attacker == Faction.Player) { foreach (var m in Monsters) if (Ok(m) && test(m)) res.Add(m); }
        else
        {
            if (Ok(P) && test(P)) res.Add(P);
            foreach (var m in Minions) if (Ok(m) && test(m)) res.Add(m);
        }
    }

    public static void InCircle(Faction attacker, Vector2 c, float r, List<Actor> res) =>
        Collect(attacker, res, a => a.Pos2.DistanceSquaredTo(c) <= (r + a.Radius) * (r + a.Radius));

    public static void InCone(Faction attacker, Vector2 origin, Vector2 dir, float r, float arcDeg, List<Actor> res)
    {
        float cos = Mathf.Cos(Mathf.DegToRad(arcDeg * 0.5f));
        dir = dir.Normalized();
        Collect(attacker, res, a =>
        {
            var d = a.Pos2 - origin;
            float len = d.Length();
            if (len > r + a.Radius) return false;
            if (len < a.Radius + 0.6f || arcDeg >= 359f) return true;      // overlapping the attacker always counts
            return d.Dot(dir) / len >= cos - a.Radius / Mathf.Max(len, 0.5f) * 0.6f;
        });
    }

    public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = ab.LengthSquared() < 1e-6f ? 0f : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    public static void InCapsule(Faction attacker, Vector2 a, Vector2 b, float halfWidth, List<Actor> res) =>
        Collect(attacker, res, x => DistToSegment(x.Pos2, a, b) <= halfWidth + x.Radius);

    public static Actor Nearest(Faction attacker, Vector2 p, float maxR, Func<Actor, bool> filter = null)
    {
        Actor best = null; float bd = maxR * maxR;
        void Test(Actor a)
        {
            if (!Ok(a) || (filter != null && !filter(a))) return;
            float d = a.Pos2.DistanceSquaredTo(p);
            if (d < bd) { bd = d; best = a; }
        }
        if (attacker == Faction.Player) foreach (var m in Monsters) Test(m);
        else { Test(P); foreach (var m in Minions) Test(m); }
        return best;
    }

    public static int CountMonstersNear(Vector2 p, float r)
    {
        int n = 0;
        foreach (var m in Monsters) if (Ok(m) && m.Pos2.DistanceSquaredTo(p) <= r * r) n++;
        return n;
    }

    public static Vector3 To3(Vector2 p, float y = 0f) => new(p.X, y, p.Y);
    public static Vector2 To2(Vector3 p) => new(p.X, p.Z);
}
