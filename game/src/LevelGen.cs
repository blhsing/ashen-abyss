using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace AshenAbyss;

public enum AreaKind { Town, Overworld, Halls, Cave, Arena }

/// <summary>What to generate. <see cref="Size"/> is cells per side (0 picks the kind's default).</summary>
public sealed class LevelRequest
{
    public string AreaId, ThemeId;
    public AreaKind Kind;
    public int Seed, Size;
    public bool Boss, Stronghold;
    public DungeonGoal Goal;
    public string[] DungeonIds;             // overworld only: [side dungeon 1, side dungeon 2, story dungeon]
}

/// <summary>A point of interest. Always on a walkable cell that can be reached from the start.</summary>
public sealed class Poi
{
    public string Kind, Tag;
    public Vector2 Pos;
    public float Radius, Facing;            // Facing: yaw in radians, 0 looks toward +Z (same convention as Actor.Facing)
    public int Room = -1;                   // index into LevelData.Rooms, -1 when outside every room
}

/// <summary>Where a group of monsters starts. Kind: "pack", "champions", "elite", "objective", "stronghold".</summary>
public sealed class SpawnGroup
{
    public Vector2 Pos;
    public int Count, Room = -1;
    public string Kind;
}

/// <summary>A place for a prop. Blocking slots are already registered as obstacles in the grid.</summary>
public sealed class PropSlot
{
    public Vector2 Pos;
    public string Category;                 // tree, rock, rock_big, clutter, breakable, furniture, light, pillar, structure, foliage
    public float Facing, Scale = 1f, Radius;
    public bool Blocks;
    public string Tag;                      // optional hint for the builder: house, stall, forge, tent, well, bench, camp, ruin, lantern, torch, brazier …
    public Vector2 Half;                    // structures with a rectangular footprint: its world-aligned half extents in metres (zero for round props)
}

public sealed class LevelData
{
    public AreaKind Kind;
    public int Seed;
    public NavGrid Grid;
    public byte[] Deco;                     // per cell: 0 ground, 1 path/road, 2 alternate ground patch, 3 room interior, 4 arena, 5 shore (next to a pit)
    public List<Poi> Pois = new();
    public List<SpawnGroup> Spawns = new();
    public List<PropSlot> Props = new();
    public List<Rect2I> Rooms = new();      // dungeons: [0] = start room … [last] = boss arena (bounding box)
    public int[] RoomMap;                   // per cell: index into Rooms, -1 for corridors, open ground and walls

    public Poi Find(string kind)
    {
        foreach (var p in Pois) if (p.Kind == kind) return p;
        return null;
    }

    public Poi Find(string kind, string tag)
    {
        foreach (var p in Pois) if (p.Kind == kind && p.Tag == tag) return p;
        return null;
    }

    public IEnumerable<Poi> All(string kind)
    {
        foreach (var p in Pois) if (p.Kind == kind) yield return p;
    }

    /// <summary>Where the hero appears by default.</summary>
    public Vector2 Start => Find("start")?.Pos ?? (Grid != null ? Grid.Size * 0.5f : Vector2.Zero);

    public byte DecoAt(Vector2 p)
    {
        if (Grid == null || Deco == null) return 0;
        var c = Grid.CellOf(p);
        return Grid.In(c.X, c.Y) ? Deco[c.Y * Grid.W + c.X] : (byte)0;
    }

    public int RoomAt(Vector2 p)
    {
        if (Grid == null || RoomMap == null) return -1;
        var c = Grid.CellOf(p);
        return Grid.In(c.X, c.Y) ? RoomMap[c.Y * Grid.W + c.X] : -1;
    }
}

/// <summary>
/// Procedural layouts. Everything derives from one <see cref="Random"/> seeded with the request's seed, so the same
/// request always produces the same level. North is -Z (row 0), south is +Z: dungeons run from the start in the
/// south to the boss in the north, the town gate and the overworld start sit at the south edge.
/// </summary>
public static class LevelGen
{
    public const byte DecoGround = 0, DecoPath = 1, DecoAlt = 2, DecoRoom = 3, DecoArena = 4, DecoShore = 5;

    public static LevelData Generate(LevelRequest r)
    {
        r ??= new LevelRequest { AreaId = ZoneDb.Town, ThemeId = "town", Kind = AreaKind.Town };
        var clock = System.Diagnostics.Stopwatch.StartNew();
        LevelData d = null;
        int attempts = 0;
        while (d == null && attempts < 6)
        {
            int attempt = attempts++;
            try { d = new Gen(r, attempt).Run(); }
            catch (GenRejected e) { Log.Warn("gen", $"{r.Kind} '{r.AreaId}' seed {r.Seed}: attempt {attempt} rejected ({e.Message})"); }
            catch (Exception e) { Log.Error("gen", $"{r.Kind} '{r.AreaId}' seed {r.Seed}: attempt {attempt} failed", e); }
        }
        if (d == null)
        {
            Log.Error("gen", $"{r.Kind} '{r.AreaId}' seed {r.Seed}: every attempt failed, falling back to the emergency layout");
            d = new Gen(r, 0).Emergency();
        }
        clock.Stop();
        Log.Info("gen", Summary(r, d, attempts, clock.Elapsed.TotalMilliseconds));
        return d;
    }

    private static string Summary(LevelRequest r, LevelData d, int attempts, double ms)
    {
        int walk = 0;
        foreach (byte t in d.Grid.T) if (t == NavGrid.Floor) walk++;
        var pois = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var p in d.Pois) { pois.TryGetValue(p.Kind ?? "?", out int c); pois[p.Kind ?? "?"] = c + 1; }
        var groups = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int monsters = 0;
        foreach (var s in d.Spawns) { groups.TryGetValue(s.Kind ?? "?", out int c); groups[s.Kind ?? "?"] = c + 1; monsters += s.Count; }
        int blocking = 0;
        foreach (var p in d.Props) if (p.Blocks) blocking++;
        var sb = new StringBuilder(256);
        sb.Append(r.Kind).Append(" '").Append(r.AreaId).Append("' theme=").Append(r.ThemeId).Append(" seed=").Append(r.Seed);
        sb.Append(" size=").Append(d.Grid.W).Append('x').Append(d.Grid.H).Append(" walkable=").Append(walk).Append(" rooms=").Append(d.Rooms.Count);
        sb.Append(" pois[");
        bool first = true;
        foreach (var kv in pois) { if (!first) sb.Append(' '); first = false; sb.Append(kv.Key).Append('=').Append(kv.Value); }
        sb.Append("] spawns=").Append(d.Spawns.Count).Append('[');
        first = true;
        foreach (var kv in groups) { if (!first) sb.Append(' '); first = false; sb.Append(kv.Key).Append('=').Append(kv.Value); }
        sb.Append("] monsters=").Append(monsters).Append(" props=").Append(d.Props.Count).Append(" blocking=").Append(blocking);
        sb.Append(" attempts=").Append(attempts).Append(' ').Append(ms.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append("ms");
        return sb.ToString();
    }

    /// <summary>A layout that broke one of its own guarantees; the generator retries with a derived seed.</summary>
    private sealed class GenRejected : Exception
    {
        public GenRejected(string message) : base(message) { }
    }

    /// <summary>Smooth value noise on a wrapping 64×64 lattice.</summary>
    private sealed class ValueNoise
    {
        private const int N = 64;
        private readonly float[] _v = new float[N * N];

        public ValueNoise(Random r)
        {
            for (int i = 0; i < _v.Length; i++) _v[i] = (float)r.NextDouble();
        }

        private float L(int x, int y) => _v[(y & (N - 1)) * N + (x & (N - 1))];

        public float At(float x, float y)
        {
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = L(x0, y0), b = L(x0 + 1, y0), c = L(x0, y0 + 1), d = L(x0 + 1, y0 + 1);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        public float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += At(x, y) * amp;
                norm += amp;
                amp *= 0.5f;
                x = x * 2f + 17.3f;
                y = y * 2f + 9.1f;
            }
            return sum / norm;
        }
    }

    private sealed class Obstacle
    {
        public Vector2 P;
        public float R;
        public PropSlot Owner;
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════════════════════════
    private sealed class Gen
    {
        private const byte Floor = NavGrid.Floor, Wall = NavGrid.Wall, Pit = NavGrid.Pit;
        private const float Cell = NavGrid.Cell;
        private const float Impassable = 1e9f;
        private const float SolidRadius = 0.85f;                 // NavGrid.BuildPathing closes a cell for obstacles this big
        private static readonly int[] DX4 = { 1, -1, 0, 0 }, DY4 = { 0, 0, 1, -1 };
        private static readonly int[] RingX = { -1, 0, 1, 1, 1, 0, -1, -1 }, RingY = { -1, -1, -1, 0, 1, 1, 1, 0 };   // clockwise from NW, odd = orthogonal

        private readonly LevelRequest _q;
        private readonly LevelData D = new();
        private readonly List<Obstacle> _obs = new();
        private readonly bool[] _ring = new bool[8];
        private Random R;
        private int W, H, B;                                      // B = thickness of the untouchable rim
        private NavGrid G;
        private byte[] T, Deco;
        private int[] RoomOf, _queue;
        private bool[] Keep;                                      // no blocking props, no pits
        private bool[] Clear;                                     // no props at all
        private bool[] Occ;                                       // holds a blocking prop
        private bool[] Halo;                                      // too close to a blocking prop for another one
        private bool[] Solid;                                     // closed for pathing by a big obstacle
        private bool[] NoSpawn, Prot, Corr;
        private int _occCount, _occCap = int.MaxValue;
        private string _pit = "none";
        private bool _outdoor, _organic, _lenient;
        private Vector2I _start;

        public Gen(LevelRequest q, int attempt)
        {
            _q = q;
            R = new Random(attempt == 0 ? q.Seed : unchecked(q.Seed * 31 + attempt * 7919));
            bool open = q.Kind == AreaKind.Overworld || q.Kind == AreaKind.Town;
            _outdoor = open;
            try
            {
                var theme = LevelThemes.Get(q.ThemeId);
                if (theme != null)
                {
                    _pit = string.IsNullOrEmpty(theme.PitKind) ? "none" : theme.PitKind;
                    _outdoor = theme.Outdoor || open;
                }
            }
            catch (Exception e)
            {
                if (attempt == 0) Log.Warn("gen", $"theme '{q.ThemeId}' could not be read ({e.GetType().Name}); generating without pits");
            }
            _organic = _outdoor || q.Kind == AreaKind.Cave || (q.ThemeId ?? "").Contains("cave");
        }

        public LevelData Run()
        {
            switch (_q.Kind)
            {
                case AreaKind.Town: GenTown(); break;
                case AreaKind.Overworld:
                    if (IsField) GenCave(true); else GenOverworld();
                    break;
                case AreaKind.Halls: GenHalls(); break;
                case AreaKind.Cave: GenCave(false); break;
                default: GenArena(); break;
            }
            Finish();
            return D;
        }

        // ───────────────────────────────────────────── basics ─────────────────────────────────────────────

        private void Init(int w, int h, int border)
        {
            W = w; H = h; B = border;
            G = new NavGrid(w, h, Vector2.Zero);
            T = G.T;
            int n = w * h;
            Deco = new byte[n];
            RoomOf = new int[n];
            Array.Fill(RoomOf, -1);
            Keep = new bool[n]; Clear = new bool[n]; Occ = new bool[n]; Halo = new bool[n];
            Solid = new bool[n]; NoSpawn = new bool[n]; Prot = new bool[n]; Corr = new bool[n];
            _queue = new int[n];
            _obs.Clear();
            _occCount = 0;
            D.Kind = _q.Kind; D.Seed = _q.Seed; D.Grid = G; D.Deco = Deco; D.RoomMap = RoomOf;
            D.Pois.Clear(); D.Spawns.Clear(); D.Props.Clear(); D.Rooms.Clear();
        }

        private int I(int x, int y) => y * W + x;
        private bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        private bool Inner(int x, int y) => x >= B && y >= B && x < W - B && y < H - B;
        private int Edge(int x, int y) => Math.Min(Math.Min(x, y), Math.Min(W - 1 - x, H - 1 - y));
        private bool IsFloor(int x, int y) => In(x, y) && T[y * W + x] == Floor;
        private bool IsWall(int x, int y) => !In(x, y) || T[y * W + x] == Wall;
        private float Rf() => (float)R.NextDouble();
        private float Rf(float a, float b) => a + (b - a) * (float)R.NextDouble();
        private int Ri(int a, int bInclusive) => bInclusive <= a ? a : R.Next(a, bInclusive + 1);
        private bool Chance(float p) => R.NextDouble() < p;
        private float RandFacing() => Rf(-MathF.PI, MathF.PI);
        private static float Dist(float x0, float y0, float x1, float y1) => MathF.Sqrt((x0 - x1) * (x0 - x1) + (y0 - y1) * (y0 - y1));
        private static float FaceOf(float dx, float dy) => MathF.Atan2(dx, dy);            // yaw whose forward (sin, cos) points along (dx, dy)
        private Vector2 Jit(int x, int y, float j) => G.Center(x, y) + new Vector2(Rf(-j, j), Rf(-j, j));
        /// <summary>World position of a point given in cell coordinates (cell centres sit on integers).</summary>
        private Vector2 Wp(float cx, float cy) => G.Origin + new Vector2((cx + 0.5f) * Cell, (cy + 0.5f) * Cell);

        private void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = R.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Opens a cell unless it lies in the rim or in a protected zone.</summary>
        private void Carve(int x, int y)
        {
            if (Inner(x, y) && !Prot[y * W + x]) T[y * W + x] = Floor;
        }

        /// <summary>Opens a cell even inside a protected zone (the boss door and its passage).</summary>
        private void CarveRaw(int x, int y)
        {
            if (Inner(x, y)) T[y * W + x] = Floor;
        }

        private void CarveDisc(float cx, float cy, float r)
        {
            int x0 = (int)MathF.Floor(cx - r), x1 = (int)MathF.Ceiling(cx + r), y0 = (int)MathF.Floor(cy - r), y1 = (int)MathF.Ceiling(cy + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Carve(x, y);
        }

        private void MarkDisc(float cx, float cy, float r, bool[] a, bool[] b = null)
        {
            int x0 = (int)MathF.Floor(cx - r), x1 = (int)MathF.Ceiling(cx + r), y0 = (int)MathF.Floor(cy - r), y1 = (int)MathF.Ceiling(cy + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!In(x, y) || (x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                    a[y * W + x] = true;
                    if (b != null) b[y * W + x] = true;
                }
        }

        private int CountFloor()
        {
            int n = 0;
            foreach (byte t in T) if (t == Floor) n++;
            return n;
        }

        private int Walls8(int x, int y)
        {
            int n = 0;
            for (int k = 0; k < 8; k++) if (IsWall(x + RingX[k], y + RingY[k])) n++;
            return n;
        }

        /// <summary>Sum of the directions pointing away from the walls beside a cell. False when there is no wall or they cancel out.</summary>
        private bool WallNormal(int x, int y, out int nx, out int ny, out int walls)
        {
            nx = 0; ny = 0; walls = 0;
            if (IsWall(x - 1, y)) { nx++; walls++; }
            if (IsWall(x + 1, y)) { nx--; walls++; }
            if (IsWall(x, y - 1)) { ny++; walls++; }
            if (IsWall(x, y + 1)) { ny--; walls++; }
            return walls > 0 && (nx != 0 || ny != 0);
        }

        /// <summary>4-connected fill over floor. With <paramref name="pathing"/> it also stops at cells closed by big obstacles.</summary>
        private bool[] Flood(int from, bool pathing, out int count)
        {
            var seen = new bool[W * H];
            count = 0;
            if (from < 0 || from >= T.Length || T[from] != Floor || (pathing && Solid[from])) return seen;
            int head = 0, tail = 0;
            _queue[tail++] = from;
            seen[from] = true;
            while (head < tail)
            {
                int c = _queue[head++];
                count++;
                int cx = c % W, cy = c / W;
                for (int k = 0; k < 4; k++)
                {
                    int x = cx + DX4[k], y = cy + DY4[k];
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    int i = y * W + x;
                    if (seen[i] || T[i] != Floor || (pathing && Solid[i])) continue;
                    seen[i] = true;
                    _queue[tail++] = i;
                }
            }
            return seen;
        }

        /// <summary>All cells of one type connected to <paramref name="seed"/>; marks them in <paramref name="seen"/>.</summary>
        private List<int> Component(int seed, byte type, bool[] seen)
        {
            var list = new List<int>();
            if (seen[seed] || T[seed] != type) return list;
            seen[seed] = true;
            list.Add(seed);
            for (int head = 0; head < list.Count; head++)
            {
                int c = list[head], cx = c % W, cy = c / W;
                for (int k = 0; k < 4; k++)
                {
                    int x = cx + DX4[k], y = cy + DY4[k];
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    int i = y * W + x;
                    if (seen[i] || T[i] != type) continue;
                    seen[i] = true;
                    list.Add(i);
                }
            }
            return list;
        }

        /// <summary>Breadth-first step distance from every source cell through the cells that pass; -1 where it never arrives.</summary>
        private int[] Bfs(Func<int, bool> source, Func<int, bool> pass)
        {
            int n = W * H;
            var dist = new int[n];
            Array.Fill(dist, -1);
            int head = 0, tail = 0;
            for (int i = 0; i < n; i++)
                if (source(i)) { dist[i] = 0; _queue[tail++] = i; }
            while (head < tail)
            {
                int c = _queue[head++], cx = c % W, cy = c / W;
                for (int k = 0; k < 4; k++)
                {
                    int x = cx + DX4[k], y = cy + DY4[k];
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    int i = y * W + x;
                    if (dist[i] >= 0 || !pass(i)) continue;
                    dist[i] = dist[c] + 1;
                    _queue[tail++] = i;
                }
            }
            return dist;
        }

        /// <summary>Chebyshev distance of every floor cell to the nearest cell that is not floor (walls, pits, outside).</summary>
        private int[] OpenDist()
        {
            var d = new int[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (T[i] != Floor) { d[i] = 0; continue; }
                    int m = int.MaxValue;
                    m = Math.Min(m, x > 0 ? d[i - 1] : 0);
                    m = Math.Min(m, y > 0 ? d[i - W] : 0);
                    m = Math.Min(m, x > 0 && y > 0 ? d[i - W - 1] : 0);
                    m = Math.Min(m, x < W - 1 && y > 0 ? d[i - W + 1] : 0);
                    d[i] = m + 1;
                }
            for (int y = H - 1; y >= 0; y--)
                for (int x = W - 1; x >= 0; x--)
                {
                    int i = y * W + x;
                    if (T[i] != Floor) continue;
                    int m = d[i] - 1;
                    m = Math.Min(m, x < W - 1 ? d[i + 1] : 0);
                    m = Math.Min(m, y < H - 1 ? d[i + W] : 0);
                    m = Math.Min(m, x < W - 1 && y < H - 1 ? d[i + W + 1] : 0);
                    m = Math.Min(m, x > 0 && y < H - 1 ? d[i + W - 1] : 0);
                    d[i] = m + 1;
                }
            return d;
        }

        /// <summary>
        /// Cheapest 4-connected route from a cell to <paramref name="target"/> (A*) or, when target is negative, to the first
        /// cell that satisfies <paramref name="goal"/> (Dijkstra). Costs are per entered cell and at least 1.
        /// </summary>
        private List<int> Route(int from, int target, Func<int, bool> goal, float[] cost)
        {
            int n = W * H;
            var dist = new float[n];
            Array.Fill(dist, float.MaxValue);
            var prev = new int[n];
            var closed = new bool[n];
            var open = new PriorityQueue<int, float>();
            int tx = target >= 0 ? target % W : 0, ty = target >= 0 ? target / W : 0;
            dist[from] = 0f;
            prev[from] = -1;
            open.Enqueue(from, 0f);
            while (open.TryDequeue(out int cur, out _))
            {
                if (closed[cur]) continue;
                closed[cur] = true;
                if (cur == target || (target < 0 && goal != null && goal(cur)))
                {
                    var path = new List<int>();
                    for (int c = cur; c >= 0; c = prev[c]) path.Add(c);
                    path.Reverse();
                    return path;
                }
                int cx = cur % W, cy = cur / W;
                for (int k = 0; k < 4; k++)
                {
                    int x = cx + DX4[k], y = cy + DY4[k];
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    int i = y * W + x;
                    if (closed[i] || cost[i] >= Impassable) continue;
                    float nd = dist[cur] + cost[i];
                    if (nd >= dist[i]) continue;
                    dist[i] = nd;
                    prev[i] = cur;
                    open.Enqueue(i, target >= 0 ? nd + Math.Abs(x - tx) + Math.Abs(y - ty) : nd);
                }
            }
            return null;
        }

        /// <summary>Movement cost per cell for routing: open ground is cheap, rock costs extra, the rim, pits and protected rock are closed.</summary>
        private float[] CostField(ValueNoise noise, float wallCost, float noiseAmp)
        {
            var cost = new float[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!Inner(x, y) || T[i] == Pit || (Prot[i] && T[i] != Floor)) { cost[i] = Impassable; continue; }
                    cost[i] = (T[i] == Floor ? 1f : wallCost) + noise.At(x * 0.21f + 40f, y * 0.21f + 40f) * noiseAmp;
                }
            return cost;
        }

        /// <summary>Cellular smoothing inside a rectangle (inclusive); everything outside counts as rock.</summary>
        private void SmoothCells(int passes, int x0, int y0, int x1, int y1, bool cave = false)
        {
            var tmp = new byte[T.Length];
            for (int p = 0; p < passes; p++)
            {
                Array.Copy(T, tmp, T.Length);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int near = 0, wide = 0;
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                bool rock = xx < x0 || yy < y0 || xx > x1 || yy > y1 || tmp[yy * W + xx] != Floor;
                                if (!rock) continue;
                                wide++;
                                if (dx >= -1 && dx <= 1 && dy >= -1 && dy <= 1) near++;
                            }
                        // caves also grow rock in the middle of wide empty spaces during the first passes, which breaks them into chambers
                        T[y * W + x] = near >= 5 || (cave && p < 3 && wide <= 2) ? Wall : Floor;
                    }
            }
        }

        /// <summary>Joins every floor region of at least <paramref name="minSize"/> cells to the start region with a tunnel; smaller ones are filled in.</summary>
        private void ConnectRegions(int from, int minSize, float[] cost, bool wide)
        {
            var ignore = new bool[T.Length];
            for (int guard = 0; guard < 600; guard++)
            {
                var reach = Flood(from, false, out _);
                int seed = -1;
                for (int i = 0; i < T.Length; i++)
                    if (T[i] == Floor && !reach[i] && !ignore[i]) { seed = i; break; }
                if (seed < 0) return;
                var comp = Component(seed, Floor, new bool[T.Length]);
                bool guarded = false;
                foreach (int c in comp) if (Prot[c]) { guarded = true; break; }
                List<int> leg = comp.Count >= minSize || guarded ? Route(seed, -1, i => reach[i], cost) : null;
                if (leg == null)
                {
                    foreach (int c in comp) { if (Prot[c]) ignore[c] = true; else T[c] = Wall; }
                    continue;
                }
                foreach (int c in leg)
                {
                    int x = c % W, y = c / W;
                    Carve(x, y);
                    if (wide) { Carve(x + 1, y); Carve(x, y + 1); }
                    else if (Chance(0.45f)) { if (Chance(0.5f)) Carve(x + 1, y); else Carve(x, y + 1); }
                }
            }
        }

        /// <summary>Turns floor that cannot be reached from the start into rock (or pit when it borders one).</summary>
        private void SealUnreachable()
        {
            var reach = Flood(I(_start.X, _start.Y), false, out _);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (T[i] != Floor || reach[i]) continue;
                    bool byPit = false;
                    for (int k = 0; k < 4; k++)
                        if (In(x + DX4[k], y + DY4[k]) && T[I(x + DX4[k], y + DY4[k])] == Pit) { byPit = true; break; }
                    T[i] = byPit ? Pit : Wall;
                    Deco[i] = DecoGround;
                }
        }

        private List<Vector2> SmoothPath(List<int> cells, int step, int iterations)
        {
            var pts = new List<Vector2>();
            for (int k = 0; k < cells.Count; k += step) pts.Add(new Vector2(cells[k] % W, cells[k] / W));
            if ((cells.Count - 1) % step != 0) pts.Add(new Vector2(cells[^1] % W, cells[^1] / W));
            for (int it = 0; it < iterations && pts.Count > 2; it++)
            {
                var q = new List<Vector2>(pts.Count * 2) { pts[0] };
                for (int k = 0; k + 1 < pts.Count; k++)
                {
                    Vector2 a = pts[k], b = pts[k + 1];
                    q.Add(a + (b - a) * 0.25f);
                    q.Add(a + (b - a) * 0.75f);
                }
                q.Add(pts[^1]);
                pts = q;
            }
            return pts;
        }

        /// <summary>Walks a polyline (cell coordinates) and opens floor / paints deco / reserves cells within the given radii.</summary>
        private void Stamp(List<Vector2> pts, float floorR, float decoR, byte deco, float keepR)
        {
            int r = (int)MathF.Ceiling(Math.Max(floorR, Math.Max(decoR, keepR)));
            for (int k = 0; k < pts.Count; k++)
            {
                Vector2 a = pts[k], b = k + 1 < pts.Count ? pts[k + 1] : pts[k];
                int steps = Math.Max(1, (int)MathF.Ceiling(a.DistanceTo(b) / 0.5f));
                for (int s = 0; s < steps; s++)
                {
                    var p = a + (b - a) * (s / (float)steps);
                    int px = (int)MathF.Round(p.X), py = (int)MathF.Round(p.Y);
                    for (int y = py - r; y <= py + r; y++)
                        for (int x = px - r; x <= px + r; x++)
                        {
                            if (!Inner(x, y)) continue;
                            float dx = x - p.X, dy = y - p.Y, d2 = dx * dx + dy * dy;
                            int i = y * W + x;
                            if (floorR > 0f && d2 <= floorR * floorR && !Prot[i]) T[i] = Floor;
                            if (decoR > 0f && d2 <= decoR * decoR && T[i] == Floor) Deco[i] = deco;
                            if (keepR > 0f && d2 <= keepR * keepR) Keep[i] = true;
                        }
                }
            }
        }

        private void StampLine(int x0, int y0, int x1, int y1, float floorR, float decoR, byte deco, float keepR) =>
            Stamp(new List<Vector2> { new(x0, y0), new(x1, y1) }, floorR, decoR, deco, keepR);

        // ───────────────────────────────────────────── placement ─────────────────────────────────────────────

        private Poi AddPoi(string kind, int x, int y, string tag = null, float radius = 0f, float facing = 0f, float clear = 2.6f)
        {
            var p = new Poi { Kind = kind, Tag = tag, Pos = G.Center(x, y), Radius = radius, Facing = facing, Room = In(x, y) ? RoomOf[I(x, y)] : -1 };
            D.Pois.Add(p);
            Reserve(x, y, clear);
            return p;
        }

        /// <summary>Keeps a spot and its surroundings free of props.</summary>
        private void Reserve(int x, int y, float radius)
        {
            if (radius > 0f) MarkDisc(x, y, radius, Keep, Clear);
            if (!In(x, y)) return;
            int i = I(x, y);
            Keep[i] = true; Clear[i] = true; NoSpawn[i] = true;
        }

        private bool FarFromPois(int x, int y, float cells, string kind)
        {
            var p = G.Center(x, y);
            float d2 = cells * Cell * cells * Cell;
            foreach (var poi in D.Pois)
                if ((kind == null || poi.Kind == kind) && poi.Pos.DistanceSquaredTo(p) < d2) return false;
            return true;
        }

        private PropSlot Prop(string cat, Vector2 pos, float radius, float facing, float scale = 1f, string tag = null)
        {
            var p = new PropSlot { Category = cat, Pos = pos, Radius = radius, Facing = facing, Scale = scale, Blocks = false, Tag = tag };
            D.Props.Add(p);
            return p;
        }

        /// <summary>
        /// True when closing cell (x, y) cannot split the walkable area: all open orthogonal neighbours stay linked through
        /// the ring of eight cells around it.
        /// </summary>
        private bool SafeToBlock(int x, int y)
        {
            int free = 0, firstRock = -1;
            for (int k = 0; k < 8; k++)
            {
                int xx = x + RingX[k], yy = y + RingY[k];
                _ring[k] = IsFloor(xx, yy) && !Occ[yy * W + xx];
                if (_ring[k]) free++; else if (firstRock < 0) firstRock = k;
            }
            if (free == 8 || free == 0) return true;
            int runs = 0;
            bool inRun = false, orth = false;
            for (int s = 1; s <= 8; s++)
            {
                int k = (firstRock + s) & 7;
                if (_ring[k])
                {
                    if (!inRun) { inRun = true; orth = false; }
                    if ((k & 1) == 1) orth = true;
                }
                else
                {
                    if (inRun && orth) runs++;
                    inRun = false;
                }
            }
            return runs <= 1;
        }

        /// <summary>
        /// Places a prop that blocks movement, if the cell allows it: on floor, outside reserved cells, not next to another
        /// blocker, and never where it could close a passage. Big ones (radius over 1 m) need open ground all around.
        /// </summary>
        private bool PlaceBlocker(int x, int y, Vector2 pos, string cat, float radius, float scale, float facing, string tag = null, bool ignoreKeep = false)
        {
            if (_occCount >= _occCap || !Inner(x, y)) return false;
            int i = I(x, y);
            if (T[i] != Floor || Halo[i] || Clear[i] || (Keep[i] && !ignoreKeep)) return false;
            bool big = radius > 1f;
            if (big)
            {
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (!IsFloor(x + dx, y + dy)) return false;
                        int j = I(x + dx, y + dy);
                        if (Occ[j]) return false;
                        if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && (Clear[j] || (Keep[j] && !ignoreKeep))) return false;
                    }
            }
            else if (!SafeToBlock(x, y)) return false;
            var slot = new PropSlot { Category = cat, Pos = pos, Radius = radius, Facing = facing, Scale = scale, Blocks = true, Tag = tag };
            D.Props.Add(slot);
            Block(slot, pos, radius, big ? 2 : 1);
            return true;
        }

        /// <summary>Registers one obstacle circle for a blocking prop and reserves the cells around it.</summary>
        private void Block(PropSlot owner, Vector2 pos, float radius, int halo)
        {
            var c = G.CellOf(pos);
            if (!In(c.X, c.Y)) return;
            int i = I(c.X, c.Y);
            _obs.Add(new Obstacle { P = pos, R = radius, Owner = owner });
            if (!Occ[i]) { Occ[i] = true; _occCount++; }
            if (radius >= SolidRadius) Solid[i] = true;
            for (int dy = -halo; dy <= halo; dy++)
                for (int dx = -halo; dx <= halo; dx++)
                    if (In(c.X + dx, c.Y + dy)) Halo[I(c.X + dx, c.Y + dy)] = true;
        }

        /// <summary>Wall-side lights: one on every stretch of straight wall, at least <paramref name="spacingM"/> apart, facing into the open.</summary>
        private void WallLights(float spacingM, string tag, bool[] only = null)
        {
            var placed = new List<Vector2>();
            float min2 = spacingM * spacingM;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (T[y * W + x] != Floor || (only != null && !only[y * W + x])) continue;
                    if (!WallNormal(x, y, out int nx, out int ny, out int walls) || walls != 1) continue;
                    var pos = G.Center(x, y) - new Vector2(nx, ny) * 0.8f;
                    bool ok = true;
                    foreach (var q in placed)
                        if (q.DistanceSquaredTo(pos) < min2) { ok = false; break; }
                    if (!ok) continue;
                    placed.Add(pos);
                    Prop("light", pos, 0.25f, FaceOf(nx, ny), 1f, tag);
                }
        }

        private void AddGroup(string kind, int x, int y, int count) =>
            D.Spawns.Add(new SpawnGroup { Pos = G.Center(x, y), Count = count, Kind = kind, Room = RoomOf[I(x, y)] });

        private float MinDist(int cell, List<int> others)
        {
            float best = float.MaxValue;
            int x = cell % W, y = cell / W;
            foreach (int o in others)
            {
                float d = Dist(x, y, o % W, o / W);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Elite and champion groups first, spread as far apart as possible, then ordinary packs wherever no other group is
        /// within <paramref name="spacing"/> cells (dart throwing over the shuffled candidates).
        /// </summary>
        private void PlaceSpawns(List<int> primary, List<int> secondary, float spacing, int elites, int champs)
        {
            Shuffle(primary);
            if (secondary != null) Shuffle(secondary);
            var taken = new List<int>();
            foreach (var s in D.Spawns)
            {
                var c = G.CellOf(s.Pos);
                if (In(c.X, c.Y)) taken.Add(I(c.X, c.Y));
            }
            var special = new List<int>();
            int e = 0, ch = 0;
            while (e < elites || ch < champs)
            {
                bool elite = e < elites && (ch >= champs || e <= ch);
                if (elite) e++; else ch++;
                int best = -1;
                float bestScore = -1f;
                for (int t = 0; t < 40 && primary.Count > 0; t++)
                {
                    int cand = primary[R.Next(primary.Count)];
                    if (MinDist(cand, taken) < spacing) continue;
                    float score = Math.Min(MinDist(cand, special), Dist(cand % W, cand / W, _start.X, _start.Y) * 0.6f);
                    if (score > bestScore) { bestScore = score; best = cand; }
                }
                int count = elite ? Ri(3, 5) : Ri(2, 4);
                if (best < 0) continue;
                special.Add(best);
                taken.Add(best);
                AddGroup(elite ? "elite" : "champions", best % W, best / W, count);
            }
            for (int pass = 0; pass < 2; pass++)
            {
                var list = pass == 0 ? primary : secondary;
                if (list == null) continue;
                foreach (int cand in list)
                {
                    if (MinDist(cand, taken) < spacing) continue;
                    taken.Add(cand);
                    AddGroup("pack", cand % W, cand / W, Ri(3, 7));
                }
            }
        }

        /// <summary>Picks up to <paramref name="count"/> entries of the pool that lie far from <paramref name="from"/> and from each other.</summary>
        private List<int> SpreadPick(List<int> pool, int count, Func<int, Vector2I> at, Vector2I from)
        {
            var res = new List<int>();
            var left = new List<int>(pool);
            while (res.Count < count && left.Count > 0)
            {
                int best = 0;
                float bestScore = -1f;
                for (int k = 0; k < left.Count; k++)
                {
                    var p = at(left[k]);
                    float s = Dist(p.X, p.Y, from.X, from.Y) * 0.5f;
                    foreach (int c in res)
                    {
                        var q = at(c);
                        s = Math.Min(s, Dist(p.X, p.Y, q.X, q.Y));
                    }
                    s += Rf() * 2f;
                    if (s > bestScore) { bestScore = s; best = k; }
                }
                res.Add(left[best]);
                left.RemoveAt(best);
            }
            return res;
        }

        /// <summary>Tries to sink a rough pool of pit cells around a spot; refused when it would touch reserved ground or cut anything off.</summary>
        private bool TryPit(int cx, int cy, float rad, ValueNoise noise, ref int reachCount)
        {
            var blob = new List<int>();
            int r = (int)MathF.Ceiling(rad * 1.3f) + 2;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    float d = Dist(x, y, cx, cy);
                    if (d > rad * 1.3f + 1.2f) continue;
                    if (!Inner(x, y)) return false;
                    int i = y * W + x;
                    if (Keep[i] || Prot[i] || Deco[i] == DecoPath) return false;
                    if (T[i] == Floor && d <= rad * (0.7f + 0.6f * noise.At(x * 0.37f + 5f, y * 0.37f + 9f))) blob.Add(i);
                }
            if (blob.Count < 4) return false;
            foreach (int i in blob) T[i] = Pit;
            Flood(I(_start.X, _start.Y), false, out int now);
            if (now != reachCount - blob.Count)
            {
                foreach (int i in blob) T[i] = Floor;
                return false;
            }
            reachCount = now;
            return true;
        }

        /// <summary>Ground next to a pit becomes shore.</summary>
        private void MarkShores()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (T[i] != Floor || (Deco[i] != DecoGround && Deco[i] != DecoAlt)) continue;
                    for (int k = 0; k < 8; k++)
                    {
                        int xx = x + RingX[k], yy = y + RingY[k];
                        if (In(xx, yy) && T[yy * W + xx] == Pit) { Deco[i] = DecoShore; break; }
                    }
                }
        }

        private void AltPatches(ValueNoise noise, float threshold)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (T[i] == Floor && Deco[i] == DecoGround && noise.Fbm(x * 0.09f + 31f, y * 0.09f + 77f, 2) > threshold) Deco[i] = DecoAlt;
                }
        }

        private string ParentArea()
        {
            try { return ZoneDb.RegionOf(_q.AreaId)?.Id ?? ZoneDb.Town; }
            catch (Exception) { return ZoneDb.Town; }
        }

        // ═════════════════════════════════════════════ Overworld ═════════════════════════════════════════════

        private void GenOverworld()
        {
            int n = _q.Size > 0 ? Math.Clamp(_q.Size, 96, 128) : 110;
            Init(n, n, 6);
            var noise = new ValueNoise(R);
            int cells = W * H;

            // 1. organic terrain: layered value noise that thickens into an impassable rim, then cellular smoothing
            var hgt = new float[cells];
            var sorted = new List<float>(cells);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int e = Edge(x, y);
                    float v = noise.Fbm(x * 0.06f, y * 0.06f, 3);
                    if (e < B + 10) v += (1f - (e - B) / 10f) * 0.3f;
                    hgt[y * W + x] = v;
                    if (e >= B) sorted.Add(v);
                }
            sorted.Sort();
            float thr = sorted[(int)(sorted.Count * 0.52f)];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    T[y * W + x] = Edge(x, y) >= B && hgt[y * W + x] <= thr ? Floor : Wall;
            SmoothCells(3, B, B, W - B - 1, H - B - 1);

            // 2. anchors: start in the south, story dungeon far in the north, a zigzag of road bends between them
            int sx = W / 2 + Ri(-W / 6, W / 6);
            _start = new Vector2I(sx, H - B - 4);
            int nx = W / 2 + Ri(-W / 5, W / 5);
            var north = new Vector2I(nx, B + 1);
            var northFront = new Vector2I(nx, B + 3);
            float bend = Chance(0.5f) ? 1f : -1f;
            float[] ts = { 0.2f, 0.42f, 0.66f, 0.85f };
            float[] amps = { Rf(0.12f, 0.22f), Rf(0.12f, 0.22f), Rf(0.04f, 0.12f), Rf(0.1f, 0.2f) };
            var ctrl = new List<Vector2I>();
            for (int k = 0; k < ts.Length; k++)
            {
                float bx = sx + (nx - sx) * ts[k] + bend * amps[k] * W;
                bend = -bend;
                int cy = (int)MathF.Round(_start.Y + (northFront.Y - _start.Y) * ts[k]) + Ri(-2, 2);
                ctrl.Add(new Vector2I(Math.Clamp((int)MathF.Round(bx), B + 14, W - B - 15), cy));
            }
            var hold = ctrl[2];                                    // the stronghold sits two thirds of the way along the road

            bool westSouth = Chance(0.5f);
            int ySouth = (int)MathF.Round(_start.Y + (northFront.Y - _start.Y) * Rf(0.28f, 0.42f));
            int yNorth = (int)MathF.Round(_start.Y + (northFront.Y - _start.Y) * Rf(0.56f, 0.8f));
            var sideA = new Vector2I(westSouth ? B + 1 : W - B - 2, ySouth);
            var sideB = new Vector2I(westSouth ? W - B - 2 : B + 1, yNorth);
            int dirA = westSouth ? 1 : -1, dirB = -dirA;           // the direction each entrance looks (out of the cliff)

            CarveDisc(_start.X, _start.Y, 3.2f);
            CarveDisc(hold.X, hold.Y, 8.7f);                       // ≈ 17 m stronghold clearing
            CarveDisc(north.X, north.Y + 2, 2.6f);
            CarveDisc(sideA.X + dirA * 2, sideA.Y, 2.6f);
            CarveDisc(sideB.X + dirB * 2, sideB.Y, 2.6f);

            // 3. roads: cheapest routes over a noisy cost field, smoothed and stamped into the terrain
            var cost = CostField(noise, 4f, 3f);
            var stops = new List<Vector2I> { _start };
            stops.AddRange(ctrl);
            stops.Add(northFront);
            var main = new List<int>();
            for (int k = 0; k + 1 < stops.Count; k++)
            {
                var leg = Route(I(stops[k].X, stops[k].Y), I(stops[k + 1].X, stops[k + 1].Y), null, cost);
                if (leg == null) throw new GenRejected("main road could not be routed");
                if (k > 0) leg.RemoveAt(0);
                main.AddRange(leg);
            }
            var mainPts = SmoothPath(main, 5, 2);
            Stamp(mainPts, 1.75f, 0.95f, DecoPath, 1.3f);
            StampLine(northFront.X, northFront.Y, north.X, north.Y, 0f, 0.95f, DecoPath, 1.3f);   // no carving here: the cliff behind the door stays whole

            var sides = new (Vector2I cell, int dir)[] { (sideA, dirA), (sideB, dirB) };
            foreach (var s in sides)
            {
                int fx = s.cell.X + s.dir * 2, fy = s.cell.Y;
                var leg = Route(I(fx, fy), -1, i => Deco[i] == DecoPath, cost);
                if (leg == null) throw new GenRejected("branch path could not be routed");
                Stamp(SmoothPath(leg, 4, 2), 1.35f, 0.6f, DecoPath, 1f);
                StampLine(fx, fy, s.cell.X, s.cell.Y, 0f, 0.6f, DecoPath, 1f);
            }
            var roadDist = Bfs(i => Deco[i] == DecoPath, i => true);

            // 4. event clearings (≈ 9 m) a little off the roads, each with a trail leading to it
            int wantEvents = Ri(2, 3);
            var events = new List<Vector2I>();
            for (int t = 0; t < 900 && events.Count < wantEvents; t++)
            {
                int x = Ri(B + 9, W - B - 10), y = Ri(B + 9, H - B - 10);
                int rd = roadDist[I(x, y)];
                if (rd < 8 || rd > (t < 600 ? 22 : 40)) continue;
                if (Dist(x, y, _start.X, _start.Y) < 18f || Dist(x, y, hold.X, hold.Y) < 20f) continue;
                if (Dist(x, y, north.X, north.Y) < 11f || Dist(x, y, sideA.X, sideA.Y) < 11f || Dist(x, y, sideB.X, sideB.Y) < 11f) continue;
                bool ok = true;
                foreach (var ev in events)
                    if (Dist(x, y, ev.X, ev.Y) < 24f) { ok = false; break; }
                if (!ok) continue;
                if (t < 500)
                {
                    int open = 0, all = 0;
                    for (int dy = -4; dy <= 4; dy++)
                        for (int dx = -4; dx <= 4; dx++) { all++; if (IsFloor(x + dx, y + dy)) open++; }
                    if (open < all * 0.55f) continue;
                }
                events.Add(new Vector2I(x, y));
            }
            foreach (var ev in events)
            {
                CarveDisc(ev.X, ev.Y, 4.7f);
                var leg = Route(I(ev.X, ev.Y), -1, i => Deco[i] == DecoPath, cost);
                if (leg != null) Stamp(SmoothPath(leg, 4, 1), 1.2f, 0f, DecoGround, 0f);
            }

            // 5. one connected land: join the larger stray regions, fill the crumbs
            cost = CostField(noise, 4f, 3f);
            ConnectRegions(I(_start.X, _start.Y), 30, cost, true);

            // 6. rock islands: specks vanish, some of the others sink into lakes / lava / chasms
            int wantLakes = _pit == "none" ? 0 : Ri(3, 6), lakes = 0;
            var seenRock = new bool[cells];
            for (int i = 0; i < cells; i++)
            {
                if (seenRock[i] || T[i] != Wall) continue;
                var comp = Component(i, Wall, seenRock);
                bool rim = false;
                foreach (int c in comp)
                    if (Edge(c % W, c / W) < B) { rim = true; break; }
                if (rim) continue;
                if (comp.Count <= 2) { foreach (int c in comp) T[c] = Floor; continue; }
                bool roll = Chance(0.6f);
                if (lakes < wantLakes && roll && comp.Count >= 5 && comp.Count <= 110)
                {
                    foreach (int c in comp) T[c] = Pit;
                    lakes++;
                }
            }
            MarkDisc(_start.X, _start.Y, 4f, Keep);
            MarkDisc(hold.X, hold.Y, 8.7f, Keep);
            foreach (var ev in events) MarkDisc(ev.X, ev.Y, 4.7f, Keep);
            Flood(I(_start.X, _start.Y), false, out int reachCount);
            for (int t = 0; t < 400 && lakes < wantLakes; t++)
            {
                int x = Ri(B + 6, W - B - 7), y = Ri(B + 6, H - B - 7);
                float rad = Rf(1.8f, 3.4f);
                int rr = (int)MathF.Ceiling(rad * 1.3f) + 1;
                bool open = true;
                for (int dy = -rr; dy <= rr && open; dy++)
                    for (int dx = -rr; dx <= rr; dx++)
                        if (!IsFloor(x + dx, y + dy)) { open = false; break; }
                if (open && TryPit(x, y, rad, noise, ref reachCount)) lakes++;
            }

            // 7. landmarks
            AddPoi("start", _start.X, _start.Y, null, 0f, MathF.PI);
            AddPoi("exit", _start.X, _start.Y + 3, ZoneDb.Town, 0f, 0f);       // the southernmost cell of the arrival clearing, rim behind it
            string[] ids = DungeonIds();
            if (ids[2] != null) AddPoi("entrance", north.X, north.Y, ids[2], 0f, FaceOf(0, 1));
            if (ids[0] != null) AddPoi("entrance", sideA.X, sideA.Y, ids[0], 0f, FaceOf(dirA, 0));
            if (ids[1] != null) AddPoi("entrance", sideB.X, sideB.Y, ids[1], 0f, FaceOf(dirB, 0));
            Reserve(north.X, north.Y + 1, 2.6f);
            Reserve(sideA.X + dirA, sideA.Y, 2.6f);
            Reserve(sideB.X + dirB, sideB.Y, 2.6f);
            AddPoi("stronghold", hold.X, hold.Y, null, 17f, 0f, 1.2f);
            // the beacon stands beside the centre, on whichever side lies farthest off the road
            var way = new Vector2I(hold.X + 3, hold.Y);
            int wayBest = -1;
            for (int k = 0; k < 8; k++)
            {
                int wx = hold.X + RingX[k] * 3, wy = hold.Y + RingY[k] * 3;
                int rd = roadDist[I(wx, wy)] * 2 + (k & 1);         // orthogonal sides win ties
                if (rd > wayBest) { wayBest = rd; way = new Vector2I(wx, wy); }
            }
            AddPoi("waypoint", way.X, way.Y, null, 0f, FaceOf(hold.X - way.X, hold.Y - way.Y));
            foreach (var ev in events) AddPoi("event", ev.X, ev.Y, null, 9f, 0f, 1.2f);

            // 8. three altars hidden in dead-end nooks dug into the cliffs
            var depth = new int[cells];
            Array.Fill(depth, -1);
            var par = new int[cells];
            int qh = 0, qt = 0;
            for (int i = 0; i < cells; i++)
                if (T[i] == Floor) { depth[i] = 0; par[i] = i; _queue[qt++] = i; }
            while (qh < qt)
            {
                int c = _queue[qh++], cx = c % W, cy = c / W;
                if (depth[c] >= 7) continue;
                for (int k = 0; k < 4; k++)
                {
                    int x = cx + DX4[k], y = cy + DY4[k];
                    if (!Inner(x, y)) continue;
                    int i = y * W + x;
                    if (depth[i] >= 0 || T[i] != Wall) continue;
                    depth[i] = depth[c] + 1;
                    par[i] = c;
                    _queue[qt++] = i;
                }
            }
            var altars = new List<Vector2I>();
            for (int pass = 0; pass < 4 && altars.Count < 3; pass++)
            {
                int wantDepth = pass < 2 ? 6 : 5;
                float spacing = pass % 2 == 0 ? 26f : 15f;
                var cands = new List<(float score, int cell)>();
                for (int i = 0; i < cells; i++)
                {
                    if (depth[i] != wantDepth || Edge(i % W, i / W) < B + 3) continue;
                    int root = i;
                    while (depth[root] > 0) root = par[root];
                    int rx = root % W, ry = root / W;
                    if (Keep[root] || roadDist[root] < 7 || Dist(rx, ry, _start.X, _start.Y) < 20f || Dist(rx, ry, hold.X, hold.Y) < 14f) continue;
                    cands.Add((roadDist[root] + Rf() * 6f, i));
                }
                cands.Sort((a, b) => a.score != b.score ? b.score.CompareTo(a.score) : a.cell.CompareTo(b.cell));
                foreach (var c in cands)
                {
                    if (altars.Count >= 3) break;
                    int x = c.cell % W, y = c.cell / W;
                    bool ok = true;
                    foreach (var a in altars)
                        if (Dist(x, y, a.X, a.Y) < spacing) { ok = false; break; }
                    if (!ok) continue;
                    altars.Add(new Vector2I(x, y));
                    for (int cc = c.cell; depth[cc] > 0; cc = par[cc]) { T[cc] = Floor; NoSpawn[cc] = true; }
                    CarveDisc(x, y, 2f);
                    MarkDisc(x, y, 2.5f, NoSpawn);
                }
            }
            if (altars.Count < 3)
            {
                // no rock deep enough: fall back to the quietest corners of the open land
                var far = new List<(int d, int cell)>();
                for (int i = 0; i < cells; i++)
                    if (T[i] == Floor && !Keep[i] && roadDist[i] >= 6) far.Add((roadDist[i], i));
                far.Sort((a, b) => a.d != b.d ? b.d.CompareTo(a.d) : a.cell.CompareTo(b.cell));
                foreach (var f in far)
                {
                    if (altars.Count >= 3) break;
                    int x = f.cell % W, y = f.cell / W;
                    if (Dist(x, y, _start.X, _start.Y) < 20f) continue;
                    bool ok = true;
                    foreach (var a in altars)
                        if (Dist(x, y, a.X, a.Y) < 18f) { ok = false; break; }
                    if (ok) altars.Add(new Vector2I(x, y));
                }
            }
            for (int k = 0; k < altars.Count; k++)
                AddPoi("altar", altars[k].X, altars[k].Y, $"{_q.AreaId}_altar{k + 1}", 0f, RandFacing());

            // 9. chests against the cliffs away from the roads, shrines within sight of them
            var spots = new List<int>();
            for (int i = 0; i < cells; i++)
            {
                if (T[i] != Floor || Keep[i] || roadDist[i] < 5) continue;
                int x = i % W, y = i / W;
                if (Dist(x, y, _start.X, _start.Y) < 18f || Walls8(x, y) < 3) continue;
                spots.Add(i);
            }
            Shuffle(spots);
            int wantChests = Ri(4, 6), chests = 0;
            for (int pass = 0; pass < 2 && chests < wantChests; pass++)
                foreach (int i in spots)
                {
                    if (chests >= wantChests) break;
                    int x = i % W, y = i / W;
                    if (Clear[i] || !FarFromPois(x, y, pass == 0 ? 20f : 11f, "chest") || !FarFromPois(x, y, 7f, null)) continue;
                    WallNormal(x, y, out int wx, out int wy, out _);
                    AddPoi("chest", x, y, null, 0f, wx != 0 || wy != 0 ? FaceOf(wx, wy) : RandFacing());
                    chests++;
                }
            spots.Clear();
            for (int i = 0; i < cells; i++)
            {
                if (T[i] != Floor || Keep[i] || roadDist[i] < 3 || roadDist[i] > 6) continue;
                if (Dist(i % W, i / W, _start.X, _start.Y) >= 22f) spots.Add(i);
            }
            Shuffle(spots);
            int wantShrines = Ri(2, 3), shrines = 0;
            for (int pass = 0; pass < 2 && shrines < wantShrines; pass++)
                foreach (int i in spots)
                {
                    if (shrines >= wantShrines) break;
                    int x = i % W, y = i / W;
                    if (Clear[i] || !FarFromPois(x, y, pass == 0 ? 28f : 15f, "shrine") || !FarFromPois(x, y, 8f, null)) continue;
                    AddPoi("shrine", x, y, null, 0f, RandFacing());
                    shrines++;
                }

            // 10. ground variety
            AltPatches(noise, 0.6f);
            MarkShores();

            // 11. the stronghold camp: structures on a ring, defenders between them, fires at the edge
            MarkDisc(_start.X, _start.Y, 11f, NoSpawn);            // nothing within 22 m of the arrival point
            MarkDisc(hold.X, hold.Y, 10.7f, NoSpawn);
            foreach (var ev in events) MarkDisc(ev.X, ev.Y, 5.7f, NoSpawn);
            MarkDisc(north.X, north.Y, 4f, NoSpawn);
            MarkDisc(sideA.X, sideA.Y, 4f, NoSpawn);
            MarkDisc(sideB.X, sideB.Y, 4f, NoSpawn);
            _occCap = (int)(CountFloor() * 0.28f);
            float a0 = Rf(0f, MathF.PI * 2f);
            for (int k = 0; k < 5; k++)
            {
                float a = a0 + k * (MathF.PI * 2f / 5f);
                int x = hold.X + (int)MathF.Round(MathF.Sin(a) * 5.5f), y = hold.Y - (int)MathF.Round(MathF.Cos(a) * 5.5f);
                if (roadDist[I(x, y)] < 3 || Dist(x, y, way.X, way.Y) < 3.5f) continue;
                PlaceBlocker(x, y, G.Center(x, y), "structure", 1.7f, 1f, FaceOf(hold.X - x, hold.Y - y), "camp", true);
            }
            int holdGroups = Ri(5, 6);
            for (int k = 0; k < holdGroups; k++)
            {
                float a = a0 + (k + 0.5f) * (MathF.PI * 2f / 5f), rr = k < 5 ? Rf(3.5f, 6.5f) : 1.5f;
                int x = hold.X + (int)MathF.Round(MathF.Sin(a) * rr), y = hold.Y - (int)MathF.Round(MathF.Cos(a) * rr);
                int count = Ri(4, 7);
                if (!_q.Stronghold) continue;                      // a relit beacon: same land, no garrison
                if (!IsFloor(x, y) || Occ[I(x, y)] || Clear[I(x, y)]) { x = hold.X; y = hold.Y + (k % 2 == 0 ? 2 : -2); }
                AddGroup("stronghold", x, y, count);
            }
            for (int k = 0; k < 4; k++)
            {
                float a = a0 + MathF.PI / 4f + k * (MathF.PI / 2f);
                float lx = hold.X + MathF.Sin(a) * 7.4f, ly = hold.Y - MathF.Cos(a) * 7.4f;
                if (IsFloor((int)MathF.Round(lx), (int)MathF.Round(ly))) Prop("light", Wp(lx, ly), 0.3f, FaceOf(hold.X - lx, hold.Y - ly), 1f, "brazier");
            }
            foreach (var ev in events)
            {
                float a = Rf(0f, MathF.PI * 2f);
                for (int k = 0; k < 2; k++)
                {
                    float aa = a + k * MathF.PI + Rf(-0.4f, 0.4f);
                    int x = ev.X + (int)MathF.Round(MathF.Sin(aa) * 3.4f), y = ev.Y - (int)MathF.Round(MathF.Cos(aa) * 3.4f);
                    PlaceBlocker(x, y, G.Center(x, y), "structure", 0.95f, 1f, FaceOf(ev.X - x, ev.Y - y), "ruin", true);
                }
                MarkDisc(ev.X, ev.Y, 4.7f, Keep);
            }

            // 12. lanterns along the main road, torches at the doors
            RoadLanterns(mainPts, 15f, 9f, hold, 9.5f);
            DoorTorches(north.X, north.Y, 0, 1);
            DoorTorches(sideA.X, sideA.Y, dirA, 0);
            DoorTorches(sideB.X, sideB.Y, dirB, 0);
            DoorTorches(_start.X, _start.Y + 3, 0, -1);

            // 13. dead forest, rocks and undergrowth: thick along the cliffs, groves and scattered trunks elsewhere
            var open2 = OpenDist();
            var order = new List<int>();
            for (int i = 0; i < cells; i++)
                if (T[i] == Floor) order.Add(i);
            Shuffle(order);
            foreach (int i in order)
            {
                if (Clear[i]) continue;
                int x = i % W, y = i / W;
                bool path = Deco[i] == DecoPath;
                float grove = noise.Fbm(x * 0.11f + 90f, y * 0.11f + 50f, 2);
                float pTree = open2[i] == 1 ? 0.45f : open2[i] == 2 ? 0.25f : grove > 0.6f ? 0.2f : 0.03f;
                float roll = Rf();
                if (roll < pTree) PlaceBlocker(x, y, Jit(x, y, 0.45f), "tree", Rf(0.4f, 0.55f), Rf(0.85f, 1.35f), RandFacing());
                else if (roll < pTree + 0.02f) PlaceBlocker(x, y, Jit(x, y, 0.4f), "rock", Rf(0.5f, 0.75f), Rf(0.8f, 1.3f), RandFacing());
                else if (roll < pTree + 0.032f) PlaceBlocker(x, y, G.Center(x, y), "rock_big", Rf(1.2f, 1.6f), Rf(0.9f, 1.3f), RandFacing());
                else if (roll < pTree + 0.14f) { if (!path) Prop("foliage", Jit(x, y, 0.8f), 0.5f, RandFacing(), Rf(0.8f, 1.4f)); }
                else if (roll < pTree + 0.165f) { if (!path) Prop("clutter", Jit(x, y, 0.7f), 0.4f, RandFacing(), Rf(0.8f, 1.2f)); }
            }

            // 14. monsters: about one group every 16 m of walkable land
            var reach = Flood(I(_start.X, _start.Y), true, out _);
            var cand = new List<int>();
            for (int i = 0; i < cells; i++)
                if (reach[i] && !Occ[i] && !NoSpawn[i] && !Clear[i]) cand.Add(i);
            PlaceSpawns(cand, null, 8f, Ri(6, 8), Ri(4, 6));
        }

        /// <summary>The three dungeon ids of an overworld: [side 1, side 2, story]; missing ones come from the region definition.</summary>
        private string[] DungeonIds()
        {
            var ids = new string[3];
            var src = _q.DungeonIds;
            if (src != null)
                for (int k = 0; k < 3 && k < src.Length; k++) ids[k] = string.IsNullOrEmpty(src[k]) ? null : src[k];
            if (ids[0] != null && ids[1] != null && ids[2] != null) return ids;
            try
            {
                var region = ZoneDb.Region(_q.AreaId);
                if (region?.Dungeons != null)
                {
                    int side = 0;
                    foreach (var d in region.Dungeons)
                    {
                        if (d == null || Array.IndexOf(ids, d.Id) >= 0) continue;
                        if (d.Story) ids[2] ??= d.Id;
                        else
                        {
                            while (side < 2 && ids[side] != null) side++;
                            if (side < 2) ids[side] = d.Id;
                        }
                    }
                }
            }
            catch (Exception e) { Log.Warn("gen", $"no dungeon list for '{_q.AreaId}' ({e.GetType().Name})"); }
            return ids;
        }

        /// <summary>
        /// Lantern slots beside a road (polyline in cell coordinates): one every <paramref name="every"/> cells, on alternating
        /// sides, skipping anything within <paramref name="avoidRadius"/> cells of <paramref name="avoid"/>.
        /// </summary>
        private void RoadLanterns(List<Vector2> pts, float every, float walked, Vector2I avoid = default, float avoidRadius = 0f)
        {
            int side = 1;
            for (int k = 0; k + 1 < pts.Count; k++)
            {
                Vector2 a = pts[k], b = pts[k + 1];
                float len = a.DistanceTo(b);
                walked += len;
                if (walked < every || len < 0.01f) continue;
                walked = 0f;
                var dir = (b - a) / len;
                float lx = a.X - dir.Y * 1.7f * side, ly = a.Y + dir.X * 1.7f * side;
                side = -side;
                int cx = (int)MathF.Round(lx), cy = (int)MathF.Round(ly);
                if (!IsFloor(cx, cy) || Clear[I(cx, cy)] || Occ[I(cx, cy)]) continue;
                if (avoidRadius > 0f && Dist(lx, ly, avoid.X, avoid.Y) < avoidRadius) continue;
                Prop("light", Wp(lx, ly), 0.25f, FaceOf(a.X - lx, a.Y - ly), 1f, "lantern");
            }
        }

        /// <summary>Two lights flanking a doorway that looks along (dx, dy).</summary>
        private void DoorTorches(int x, int y, int dx, int dy)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                int tx = x + dx - dy * 2 * s, ty = y + dy + dx * 2 * s;
                if (IsFloor(tx, ty)) Prop("light", G.Center(tx, ty), 0.25f, FaceOf(dx, dy), 1f, "torch");
            }
        }

        // ═════════════════════════════════════════════ dungeons: shared ═════════════════════════════════════════════

        /// <summary>Carves the boss chamber around (cx, cy): a rough disc for organic places, an octagonal hall otherwise.</summary>
        private List<int> CarveArena(int cx, int cy, int ra, bool organic, ValueNoise noise)
        {
            var list = new List<int>();
            for (int y = cy - ra - 1; y <= cy + ra + 1; y++)
                for (int x = cx - ra - 1; x <= cx + ra + 1; x++)
                {
                    if (!Inner(x, y)) continue;
                    bool inside;
                    if (organic) inside = Dist(x, y, cx, cy) <= ra + (noise.At(x * 0.33f + 3f, y * 0.33f + 11f) - 0.5f);   // ra ± 0.5 cells
                    else
                    {
                        int dx = Math.Abs(x - cx), dy = Math.Abs(y - cy);
                        inside = dx <= ra && dy <= ra && dx + dy <= ra * 2 - ra / 2;
                    }
                    if (!inside) continue;
                    int i = y * W + x;
                    T[i] = Floor;
                    Deco[i] = DecoArena;
                    list.Add(i);
                }
            return list;
        }

        /// <summary>The dungeon definition behind the requested area, if it is one.</summary>
        private DungeonDef ThisDungeon()
        {
            try { return ZoneDb.Dungeon(_q.AreaId); }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// An overworld request that asks for a boss (or names a dungeon) is an outdoor dungeon field: open land with a road
        /// from the arrival point to a walled boss glade, filled by the dungeon content rules instead of the region ones.
        /// </summary>
        private bool IsField => _q.Kind == AreaKind.Overworld && (_q.Boss || ThisDungeon() != null);

        /// <summary>Half-size of the boss arena in cells: story bosses get the larger one.</summary>
        private int ArenaCells() => ThisDungeon()?.Story == true ? 8 : 7;

        /// <summary>Open floor radius (m) guaranteed around the centre of an arena carved by <see cref="CarveArena"/>.</summary>
        private static float ArenaRadius(int ra, bool organic)
        {
            // organic: every cell whose centre is within ra - 0.5 cells is floor; a point can sit up to half a cell diagonal from its cell centre
            float cells = organic ? ra - 0.5f - 0.71f : Math.Min(ra + 0.5f, (ra * 2 - ra / 2) * 0.7071f);
            return MathF.Floor(cells * Cell * 2f) / 2f;
        }

        /// <summary>
        /// Opens the only way into the boss chamber on its south side: a door cell and a threshold one cell wide, then a
        /// passage two cells wide down to the edge of the protected band. Returns the door cell.
        /// </summary>
        private Vector2I OpenDoor(int cx, int cy, int ra, int doorX, int protY)
        {
            int yb = cy;
            for (int y = cy; y <= cy + ra + 1; y++)
            {
                if (!IsFloor(doorX, y)) break;
                yb = y;
            }
            int door = yb + 1;
            while (door < protY - 1 && (IsFloor(doorX - 1, door) || IsFloor(doorX + 1, door)))
            {
                CarveRaw(doorX, door);                             // the chamber bulges beside this cell: still part of the chamber
                Deco[I(doorX, door)] = DecoArena;
                door++;
            }
            CarveRaw(doorX, door);
            CarveRaw(doorX, door + 1);
            for (int y = door + 2; y <= protY; y++) { CarveRaw(doorX, y); CarveRaw(doorX + 1, y); }
            return new Vector2I(doorX, door);
        }

        private Rect2I Bounds(List<int> cells)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (int c in cells)
            {
                int x = c % W, y = c / W;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
            return new Rect2I(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        private Vector2I RoomCentre(int room)
        {
            var rc = D.Rooms[room];
            return new Vector2I(rc.Position.X + rc.Size.X / 2, rc.Position.Y + rc.Size.Y / 2);
        }

        /// <summary>Start, way out, boss door and boss: the same in every dungeon kind.</summary>
        private void DungeonCorePois(Vector2I exit, Vector2I door, Vector2I arena, float arenaRadius, int arenaRoom)
        {
            AddPoi("start", _start.X, _start.Y, null, 0f, MathF.PI);
            AddPoi("exit", exit.X, exit.Y, ParentArea(), 0f, 0f);
            var d = AddPoi("bossdoor", door.X, door.Y, null, 0f, MathF.PI, 2.6f);
            d.Room = arenaRoom;
            Reserve(door.X, door.Y + 1, 2.6f);
            AddPoi("boss", arena.X, arena.Y, null, arenaRadius, 0f, 1.2f);
        }

        /// <summary>Goal content: four objective spots in distinct rooms, plus the ember altar by the boss door when embers are collected.</summary>
        private void PlaceObjectives(List<int> rooms, Vector2I altarCell)
        {
            var goal = _q.Goal;
            if (goal == DungeonGoal.None) return;
            var picked = SpreadPick(rooms, 4, RoomCentre, _start);
            foreach (int room in picked)
            {
                var c = RoomCentre(room);
                if (goal == DungeonGoal.DestroyIdols || goal == DungeonGoal.FreePrisoners)
                    AddPoi("objective", c.X, c.Y, goal == DungeonGoal.DestroyIdols ? "idol" : "prisoner", 0f, RandFacing()).Room = room;
                else
                {
                    AddGroup("objective", c.X, c.Y, Ri(3, 5));
                    D.Spawns[^1].Room = room;                       // chambers may overlap: the group belongs to the one it was picked for
                    Reserve(c.X, c.Y, 1.6f);
                }
            }
            if (goal != DungeonGoal.CollectEmbers) return;
            if (!IsFloor(altarCell.X, altarCell.Y) || Clear[I(altarCell.X, altarCell.Y)])
            {
                // the preferred spot is taken or solid: the nearest free floor cell around it
                int best = -1;
                float bd = float.MaxValue;
                for (int y = altarCell.Y - 3; y <= altarCell.Y + 3; y++)
                    for (int x = altarCell.X - 3; x <= altarCell.X + 3; x++)
                    {
                        if (!IsFloor(x, y) || Clear[I(x, y)] || Prot[I(x, y)]) continue;
                        float d = Dist(x, y, altarCell.X, altarCell.Y);
                        if (d < bd) { bd = d; best = I(x, y); }
                    }
                if (best < 0) return;
                altarCell = new Vector2I(best % W, best / W);
            }
            AddPoi("objective", altarCell.X, altarCell.Y, "altar", 0f, 0f);
        }

        // ═════════════════════════════════════════════ Halls ═════════════════════════════════════════════

        private static int Gap(Rect2I a, Rect2I b)
        {
            int ax1 = a.Position.X + a.Size.X - 1, ay1 = a.Position.Y + a.Size.Y - 1, bx1 = b.Position.X + b.Size.X - 1, by1 = b.Position.Y + b.Size.Y - 1;
            int gx = Math.Max(0, Math.Max(a.Position.X - bx1, b.Position.X - ax1) - 1);
            int gy = Math.Max(0, Math.Max(a.Position.Y - by1, b.Position.Y - ay1) - 1);
            return gx + gy;
        }

        private static bool Overlaps(Rect2I a, Rect2I b, int margin) =>
            a.Position.X - margin < b.Position.X + b.Size.X && b.Position.X - margin < a.Position.X + a.Size.X &&
            a.Position.Y - margin < b.Position.Y + b.Size.Y && b.Position.Y - margin < a.Position.Y + a.Size.Y;

        private static int Hops(List<int>[] adj, int from, int to)
        {
            var dist = new int[adj.Length];
            Array.Fill(dist, -1);
            var queue = new List<int> { from };
            dist[from] = 0;
            for (int head = 0; head < queue.Count; head++)
            {
                int c = queue[head];
                if (c == to) return dist[c];
                foreach (int nb in adj[c])
                    if (dist[nb] < 0) { dist[nb] = dist[c] + 1; queue.Add(nb); }
            }
            return int.MaxValue;
        }

        private void CorridorRect(int x0, int y0, int w, int h)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                {
                    if (!Inner(x, y)) continue;
                    int i = y * W + x;
                    if (Prot[i] || T[i] == Floor) continue;
                    T[i] = Floor;
                    Corr[i] = true;
                }
        }

        /// <summary>Joins two rooms: one straight run when they face each other, otherwise an L.</summary>
        private void Corridor(Rect2I a, Rect2I b, int width)
        {
            int ax0 = a.Position.X, ay0 = a.Position.Y, ax1 = ax0 + a.Size.X - 1, ay1 = ay0 + a.Size.Y - 1;
            int bx0 = b.Position.X, by0 = b.Position.Y, bx1 = bx0 + b.Size.X - 1, by1 = by0 + b.Size.Y - 1;
            int ox0 = Math.Max(ax0, bx0) + 1, ox1 = Math.Min(ax1, bx1) - width;
            int oy0 = Math.Max(ay0, by0) + 1, oy1 = Math.Min(ay1, by1) - width;
            if (ox1 >= ox0)
            {
                int x = Ri(ox0, ox1), ya = (ay0 + ay1) / 2, yb = (by0 + by1) / 2;
                CorridorRect(x, Math.Min(ya, yb), width, Math.Abs(ya - yb) + 1);
            }
            else if (oy1 >= oy0)
            {
                int y = Ri(oy0, oy1), xa = (ax0 + ax1) / 2, xb = (bx0 + bx1) / 2;
                CorridorRect(Math.Min(xa, xb), y, Math.Abs(xa - xb) + 1, width);
            }
            else
            {
                int pax = Ri(ax0 + 1, ax1 - width), pay = Ri(ay0 + 1, ay1 - width);
                int pbx = Ri(bx0 + 1, bx1 - width), pby = Ri(by0 + 1, by1 - width);
                if (Chance(0.5f))
                {
                    CorridorRect(Math.Min(pax, pbx), pay, Math.Abs(pax - pbx) + width, width);
                    CorridorRect(pbx, Math.Min(pay, pby), width, Math.Abs(pay - pby) + width);
                }
                else
                {
                    CorridorRect(pax, Math.Min(pay, pby), width, Math.Abs(pay - pby) + width);
                    CorridorRect(Math.Min(pax, pbx), pby, Math.Abs(pax - pbx) + width, width);
                }
            }
        }

        /// <summary>Evenly spaced, centred positions between lo and hi (inclusive).</summary>
        private static List<int> Slots(int lo, int hi)
        {
            var list = new List<int>();
            int len = hi - lo;
            if (len < 0) return list;
            int step = len % 3 == 0 ? 3 : len % 4 == 0 ? 4 : 3;
            int count = len / step + 1, off = (len - (count - 1) * step) / 2;
            for (int k = 0; k < count; k++) list.Add(lo + off + k * step);
            return list;
        }

        private void GenHalls()
        {
            int n = _q.Size > 0 ? Math.Clamp(_q.Size, 56, 90) : 60 + R.Next(21);
            Init(n, n, 3);
            var noise = new ValueNoise(R);

            // the boss arena against the north wall, sealed off behind two cells of masonry
            int ra = ArenaCells();
            int acx = Ri(B + ra + 2, W - B - ra - 3), acy = B + ra;
            var arenaCells = CarveArena(acx, acy, ra, false, noise);
            int protY = acy + ra + 2;
            for (int y = 0; y <= protY; y++)
                for (int x = 0; x < W; x++) Prot[I(x, y)] = true;
            var door = OpenDoor(acx, acy, ra, acx + Ri(-2, 2), protY);

            // rooms: start at the south end, an antechamber under the boss door, the rest scattered between
            int minX = B, maxX = W - B - 1, minY = protY + 1, maxY = H - B - 1;
            int sw = Ri(6, 8), sh = Ri(5, 6);
            var startRoom = new Rect2I(Ri(minX + 1, maxX - sw), maxY - sh + 1, sw, sh);
            int aw = Ri(7, 9), ah = Ri(5, 6);
            var ante = new Rect2I(Math.Clamp(door.X - aw / 2 + Ri(-1, 1), minX, maxX - aw + 1), minY, aw, ah);
            int want = Math.Clamp(9 + (n - 56) * 5 / 34 + Ri(0, 2), 9, 14);
            int wMax = 9 + (n - 56) / 12, hMax = 8 + (n - 56) / 14;
            var others = new List<Rect2I>();
            for (int t = 0; t < 2000 && others.Count < want - 3; t++)
            {
                int shrink = t / 500;
                int w = Ri(5, Math.Max(6, wMax - shrink * 2)), h = Ri(5, Math.Max(5, hMax - shrink * 2));
                var rc = new Rect2I(Ri(minX, maxX - w + 1), Ri(minY, maxY - h + 1), w, h);
                if (Overlaps(rc, startRoom, 3) || Overlaps(rc, ante, 3)) continue;
                bool ok = true;
                foreach (var o in others)
                    if (Overlaps(rc, o, 3)) { ok = false; break; }
                if (ok) others.Add(rc);
            }
            if (others.Count < 6) throw new GenRejected($"only {others.Count + 3} rooms fit");
            int scx = startRoom.Position.X + startRoom.Size.X / 2, scy = startRoom.Position.Y + startRoom.Size.Y / 2;
            others.Sort((a, b) =>
            {
                float da = Dist(a.Position.X + a.Size.X * 0.5f, a.Position.Y + a.Size.Y * 0.5f, scx, scy);
                float db = Dist(b.Position.X + b.Size.X * 0.5f, b.Position.Y + b.Size.Y * 0.5f, scx, scy);
                if (da != db) return da.CompareTo(db);
                return a.Position.Y != b.Position.Y ? a.Position.Y.CompareTo(b.Position.Y) : a.Position.X.CompareTo(b.Position.X);
            });
            D.Rooms.Add(startRoom);
            D.Rooms.AddRange(others);
            D.Rooms.Add(ante);
            int linked = D.Rooms.Count, anteRoom = linked - 1, arenaRoom = linked;
            D.Rooms.Add(Bounds(arenaCells));
            for (int r = 0; r < linked; r++)
            {
                var rc = D.Rooms[r];
                for (int y = rc.Position.Y; y < rc.Position.Y + rc.Size.Y; y++)
                    for (int x = rc.Position.X; x < rc.Position.X + rc.Size.X; x++)
                    {
                        Carve(x, y);
                        if (!IsFloor(x, y)) continue;
                        Deco[I(x, y)] = DecoRoom;
                        RoomOf[I(x, y)] = r;
                    }
            }
            foreach (int c in arenaCells) RoomOf[c] = arenaRoom;

            // corridors: a spanning tree over every room but the arena, plus a couple of loops
            var adj = new List<int>[linked];
            for (int r = 0; r < linked; r++) adj[r] = new List<int>();
            var inTree = new bool[linked];
            inTree[0] = true;
            var edges = new List<(int a, int b)>();
            for (int added = 1; added < linked; added++)
            {
                int ba = -1, bb = -1, bd = int.MaxValue;
                for (int a = 0; a < linked; a++)
                {
                    if (!inTree[a]) continue;
                    for (int b = 0; b < linked; b++)
                    {
                        if (inTree[b]) continue;
                        int d = Gap(D.Rooms[a], D.Rooms[b]);
                        if (d < bd) { bd = d; ba = a; bb = b; }
                    }
                }
                inTree[bb] = true;
                edges.Add((ba, bb));
                adj[ba].Add(bb);
                adj[bb].Add(ba);
            }
            var leaf = new bool[linked];
            for (int r = 0; r < linked; r++) leaf[r] = adj[r].Count == 1;
            int loops = n >= 72 ? 3 : 2;
            var extra = new List<(int d, int a, int b)>();
            for (int a = 0; a < linked; a++)
                for (int b = a + 1; b < linked; b++)
                {
                    if (adj[a].Contains(b)) continue;
                    int d = Gap(D.Rooms[a], D.Rooms[b]);
                    if (d <= n / 3) extra.Add((d, a, b));
                }
            extra.Sort((p, q) => p.d != q.d ? p.d.CompareTo(q.d) : p.a != q.a ? p.a.CompareTo(q.a) : p.b.CompareTo(q.b));
            foreach (var c in extra)
            {
                if (loops == 0) break;
                if (Hops(adj, c.a, c.b) < 3) continue;
                edges.Add((c.a, c.b));
                adj[c.a].Add(c.b);
                adj[c.b].Add(c.a);
                loops--;
            }
            foreach (var e in edges) Corridor(D.Rooms[e.a], D.Rooms[e.b], Ri(2, 3));

            // corridors and the room cells in front of their mouths stay clear
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!Corr[I(x, y)]) continue;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (In(x + dx, y + dy)) Keep[I(x + dx, y + dy)] = true;
                }
            foreach (int c in arenaCells) Keep[c] = true;

            // points of interest
            _start = new Vector2I(scx, scy);
            DungeonCorePois(new Vector2I(scx, startRoom.Position.Y + startRoom.Size.Y - 1), door, new Vector2I(acx, acy), ArenaRadius(ra, false), arenaRoom);
            var pool = new List<int>();
            for (int r = 1; r < anteRoom; r++) pool.Add(r);
            int altarX = door.X + (door.X - ante.Position.X >= ante.Size.X / 2 ? -2 : 2);
            PlaceObjectives(pool, new Vector2I(Math.Clamp(altarX, ante.Position.X, ante.Position.X + ante.Size.X - 1), ante.Position.Y + 1));

            var free = new List<int>();                             // rooms without an objective in the middle
            foreach (int r in pool)
            {
                var c = RoomCentre(r);
                if (!Clear[I(c.X, c.Y)]) free.Add(r);
            }
            Shuffle(free);
            int shrineRoom = free.Count > 0 ? free[^1] : anteRoom;
            if (free.Count > 0) free.RemoveAt(free.Count - 1);
            var sc = RoomCentre(shrineRoom);
            if (Clear[I(sc.X, sc.Y)]) sc = new Vector2I(sc.X, sc.Y + 1);
            AddPoi("shrine", sc.X, sc.Y, null, 0f, RandFacing());

            var chestRooms = new List<int>();
            foreach (int r in free) if (leaf[r]) chestRooms.Add(r);        // dead-end rooms first
            foreach (int r in free) if (!leaf[r]) chestRooms.Add(r);
            var rest = new List<int>(pool);
            Shuffle(rest);
            foreach (int r in rest) if (!chestRooms.Contains(r) && r != shrineRoom) chestRooms.Add(r);
            int wantChests = Ri(2, 3), chests = 0;
            foreach (int r in chestRooms)
            {
                if (chests >= wantChests) break;
                var rc = D.Rooms[r];
                var edge = new List<int>();
                for (int y = rc.Position.Y; y < rc.Position.Y + rc.Size.Y; y++)
                    for (int x = rc.Position.X; x < rc.Position.X + rc.Size.X; x++)
                    {
                        int i = I(x, y);
                        if (T[i] == Floor && !Keep[i] && !Clear[i] && WallNormal(x, y, out _, out _, out _)) edge.Add(i);
                    }
                if (edge.Count == 0) continue;
                int pick = edge[R.Next(edge.Count)];
                WallNormal(pick % W, pick / W, out int wx, out int wy, out _);
                AddPoi("chest", pick % W, pick / W, null, 0f, FaceOf(wx, wy));
                chests++;
            }

            // furnishing
            _occCap = (int)(CountFloor() * 0.28f);
            for (int r = 0; r < linked; r++)
            {
                var rc = D.Rooms[r];
                int x0 = rc.Position.X, y0 = rc.Position.Y, x1 = x0 + rc.Size.X - 1, y1 = y0 + rc.Size.Y - 1;
                if (r != 0 && rc.Size.X >= 7 && rc.Size.Y >= 7)
                {
                    var xs = Slots(x0 + (rc.Size.X >= 9 ? 2 : 1), x1 - (rc.Size.X >= 9 ? 2 : 1));
                    var ys = Slots(y0 + (rc.Size.Y >= 9 ? 2 : 1), y1 - (rc.Size.Y >= 9 ? 2 : 1));
                    int mode = Ri(0, 2);                            // 0 ring, 1 full grid, 2 two rows
                    foreach (int y in ys)
                        foreach (int x in xs)
                        {
                            bool rimX = x == xs[0] || x == xs[^1], rimY = y == ys[0] || y == ys[^1];
                            if ((mode == 0 && !rimX && !rimY) || (mode == 2 && !rimY)) continue;
                            PlaceBlocker(x, y, G.Center(x, y), "pillar", 0.95f, 1f, 0f);
                        }
                }
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int i = I(x, y);
                        if (T[i] != Floor || Clear[i]) continue;
                        float roll = Rf();
                        if (WallNormal(x, y, out int wx, out int wy, out _))
                        {
                            var nrm = new Vector2(wx, wy).Normalized();
                            if (_outdoor && roll < 0.05f) PlaceBlocker(x, y, Jit(x, y, 0.3f), "tree", Rf(0.4f, 0.55f), Rf(0.85f, 1.3f), RandFacing());
                            else if (roll < 0.14f) PlaceBlocker(x, y, G.Center(x, y) - nrm * 0.3f, "furniture", 0.7f, 1f, FaceOf(wx, wy));
                            else if (roll < 0.26f)
                            {
                                if (Keep[i]) continue;
                                int count = Ri(1, 3);
                                for (int k = 0; k < count; k++) Prop("breakable", Jit(x, y, 0.55f) - nrm * 0.2f, 0.4f, RandFacing(), Rf(0.85f, 1.15f));
                            }
                            else if (roll < 0.38f) Prop("clutter", Jit(x, y, 0.6f), 0.4f, RandFacing(), Rf(0.8f, 1.2f));
                        }
                        else if (roll < 0.03f) Prop("clutter", Jit(x, y, 0.7f), 0.4f, RandFacing(), Rf(0.8f, 1.2f));
                        else if (_outdoor && roll < 0.12f) Prop("foliage", Jit(x, y, 0.8f), 0.5f, RandFacing(), Rf(0.8f, 1.3f));
                    }
            }
            for (int i = 0; i < T.Length; i++)
                if (Corr[i] && !Clear[i] && Chance(0.035f)) Prop("clutter", Jit(i % W, i / W, 0.6f), 0.4f, RandFacing(), Rf(0.8f, 1.2f));
            WallLights(9f, _outdoor ? "brazier" : "torch");

            // monsters: rooms first, long corridors get what is left
            MarkDisc(_start.X, _start.Y, 9f, NoSpawn);
            var reach = Flood(I(_start.X, _start.Y), true, out _);
            var inRooms = new List<int>();
            var inCorridors = new List<int>();
            for (int i = 0; i < T.Length; i++)
            {
                if (!reach[i] || Occ[i] || NoSpawn[i] || Clear[i] || Prot[i]) continue;
                int room = RoomOf[i];
                if (room == 0 || room == anteRoom || room == arenaRoom) continue;
                if (room > 0) inRooms.Add(i); else if (Corr[i]) inCorridors.Add(i);
            }
            int specials = Math.Max(1, inRooms.Count / 100);       // small dungeons hold fewer elite and champion groups
            int elites = Math.Min(_q.Goal == DungeonGoal.SlayElites ? Ri(1, 2) : Ri(2, 3), specials), champs = Math.Min(Ri(2, 3), specials);
            PlaceSpawns(inRooms, inCorridors, 6f, elites, champs);
        }

        // ═════════════════════════════════════════════ Cave ═════════════════════════════════════════════

        /// <summary>Caverns; with <paramref name="field"/> the same journey laid out in the open (an outdoor dungeon such as a graveyard).</summary>
        private void GenCave(bool field)
        {
            int n = _q.Size > 0 ? Math.Clamp(_q.Size, field ? 48 : 56, 90) : field ? 60 : 62 + R.Next(15);
            Init(n, n, field ? 5 : 3);
            var noise = new ValueNoise(R);

            // the boss cavern (or glade) in the north, walled off by untouched rock; its narrow mouth is the door
            int ra = ArenaCells();
            int acx = Ri(B + ra + 4, W - B - ra - 5), acy = B + ra + 1;
            var arenaCells = CarveArena(acx, acy, ra, true, noise);
            int protY = acy + ra + 3;
            for (int y = 0; y <= protY; y++)
                for (int x = 0; x < W; x++) Prot[I(x, y)] = true;
            var door = OpenDoor(acx, acy, ra, acx + Ri(-2, 2), protY);

            // below it: cellular caverns, or open land shaped by value noise for an outdoor field
            int rx0 = B, rx1 = W - B - 1, ry0 = protY + 1, ry1 = H - B - 1;
            _start = new Vector2I(Ri(rx0 + 6, rx1 - 6), ry1 - 3);
            float fill = field ? 0.36f : 0.46f;
            for (int round = 0; ; round++)
            {
                if (field)
                {
                    var hgt = new float[T.Length];
                    var sorted = new List<float>();
                    for (int y = ry0; y <= ry1; y++)
                        for (int x = rx0; x <= rx1; x++)
                        {
                            int e = Math.Min(Math.Min(x - rx0, rx1 - x), Math.Min(y - ry0, ry1 - y));
                            float v = noise.Fbm(x * 0.085f + round * 13.7f, y * 0.085f, 3) + (e < 4 ? (1f - e / 4f) * 0.25f : 0f);
                            hgt[I(x, y)] = v;
                            sorted.Add(v);
                        }
                    sorted.Sort();
                    float thr = sorted[Math.Clamp((int)(sorted.Count * (1f - fill)), 0, sorted.Count - 1)];
                    for (int y = ry0; y <= ry1; y++)
                        for (int x = rx0; x <= rx1; x++) T[I(x, y)] = hgt[I(x, y)] <= thr ? Floor : Wall;
                    SmoothCells(2, rx0, ry0, rx1, ry1);
                }
                else
                {
                    for (int y = ry0; y <= ry1; y++)
                        for (int x = rx0; x <= rx1; x++) T[I(x, y)] = Rf() < fill ? Wall : Floor;
                    SmoothCells(5, rx0, ry0, rx1, ry1, true);
                }
                CarveDisc(_start.X, _start.Y, 3f);
                CarveDisc(door.X + 0.5f, protY + 2.2f, 2.2f);
                ConnectRegions(I(_start.X, _start.Y), 14, CostField(noise, 2.5f, 3f), field);
                Flood(I(_start.X, _start.Y), false, out int open);
                if (open >= (rx1 - rx0 + 1) * (ry1 - ry0 + 1) * 0.36f || round >= 3) break;
                fill -= 0.03f;
            }
            if (!Flood(I(_start.X, _start.Y), false, out _)[I(acx, acy)]) throw new GenRejected("boss cavern is cut off");

            // an outdoor field gets a proper road from the arrival point to the mouth of the boss glade
            List<int> trail = null;
            if (field)
            {
                trail = Route(I(_start.X, _start.Y), I(door.X, protY + 1), null, CostField(noise, 5f, 3f));
                if (trail == null) throw new GenRejected("no road from the start to the boss door");
                Stamp(SmoothPath(trail, 4, 2), 1.5f, 0.9f, DecoPath, 1.2f);
                for (int y = door.Y; y <= protY; y++)
                    for (int x = door.X; x <= door.X + 1; x++)
                        if (IsFloor(x, y)) { Deco[I(x, y)] = DecoPath; Keep[I(x, y)] = true; }
            }

            // pits: pools, lava or chasms in the wider caverns
            MarkDisc(_start.X, _start.Y, 4f, Keep);
            MarkDisc(door.X, protY + 2, 3.2f, Keep);
            if (_pit != "none")
            {
                int wantPits = Math.Min(Ri(3, 6), Math.Max(1, (rx1 - rx0 + 1) * (ry1 - ry0 + 1) / 350)), pits = 0;
                Flood(I(_start.X, _start.Y), false, out int reachCount);
                var openNow = OpenDist();
                for (int t = 0; t < 500 && pits < wantPits; t++)
                {
                    int x = Ri(rx0 + 2, rx1 - 2), y = Ri(ry0 + 2, ry1 - 2);
                    int room = openNow[I(x, y)];
                    if (room < 3) continue;
                    float rad = Math.Min(room - 1.3f, Rf(1.5f, 3.4f));
                    if (!TryPit(x, y, rad, noise, ref reachCount)) continue;
                    pits++;
                    openNow = OpenDist();
                }
            }
            SealUnreachable();

            // chambers: the widest spots, well apart; they stand in for rooms
            var openDist = OpenDist();
            var centres = new List<int> { I(_start.X, _start.Y) };
            for (int spacing = 9; spacing >= 5 && centres.Count < 6; spacing -= 2)
            {
                var byWidth = new List<int>();
                for (int y = ry0; y <= ry1; y++)
                    for (int x = rx0; x <= rx1; x++)
                        if (openDist[I(x, y)] >= 2) byWidth.Add(I(x, y));
                byWidth.Sort((a, b) => openDist[a] != openDist[b] ? openDist[b].CompareTo(openDist[a]) : a.CompareTo(b));
                foreach (int c in byWidth)
                {
                    if (centres.Count >= 14) break;
                    bool ok = true;
                    foreach (int o in centres)
                        if (Math.Max(Math.Abs(c % W - o % W), Math.Abs(c / W - o / W)) < spacing) { ok = false; break; }
                    if (ok) centres.Add(c);
                }
            }
            foreach (int c in centres)
            {
                int half = Math.Max(1, openDist[c] - 1);
                D.Rooms.Add(new Rect2I(c % W - half, c / W - half, half * 2 + 1, half * 2 + 1));
            }
            int arenaRoom = D.Rooms.Count;
            D.Rooms.Add(Bounds(arenaCells));
            for (int r = 0; r < arenaRoom; r++)
            {
                var rc = D.Rooms[r];
                for (int y = rc.Position.Y; y < rc.Position.Y + rc.Size.Y; y++)
                    for (int x = rc.Position.X; x < rc.Position.X + rc.Size.X; x++)
                        if (IsFloor(x, y) && RoomOf[I(x, y)] < 0) RoomOf[I(x, y)] = r;
            }
            foreach (int c in arenaCells) { RoomOf[c] = arenaRoom; Keep[c] = true; }

            // caves: a worn trail from the entrance to the boss door, kept to the middle of the passages
            if (!field)
            {
                var trailCost = new float[T.Length];
                for (int i = 0; i < T.Length; i++) trailCost[i] = T[i] == Floor ? 1f + 4f / Math.Max(1, openDist[i]) : Impassable;
                trail = Route(I(_start.X, _start.Y), I(door.X, door.Y), null, trailCost);
                if (trail == null) throw new GenRejected("no way from the start to the boss door");
                foreach (int c in trail)
                {
                    Deco[c] = DecoPath;
                    Keep[c] = true;
                }
            }
            var trailDist = Bfs(i => Deco[i] == DecoPath, i => T[i] == Floor);

            // points of interest
            DungeonCorePois(new Vector2I(_start.X, _start.Y + 3), door, new Vector2I(acx, acy), ArenaRadius(ra, true), arenaRoom);
            var pool = new List<int>();
            for (int r = 1; r < arenaRoom; r++) pool.Add(r);
            int altarX = door.X + (IsFloor(door.X + 2, protY + 2) ? 2 : -2);
            PlaceObjectives(pool, new Vector2I(altarX, protY + 2));

            var free = new List<int>();
            foreach (int r in pool)
            {
                var c = RoomCentre(r);
                if (!Clear[I(c.X, c.Y)]) free.Add(r);
            }
            Shuffle(free);
            if (free.Count > 0)
            {
                var c = RoomCentre(free[^1]);
                free.RemoveAt(free.Count - 1);
                AddPoi("shrine", c.X, c.Y, null, 0f, RandFacing());
            }
            else
            {
                // no spare chamber: beside the trail, halfway in
                int mid = trail[trail.Count / 2], best = mid;
                for (int k = 0; k < 4; k++)
                {
                    int x = mid % W + DX4[k] * 2, y = mid / W + DY4[k] * 2;
                    if (IsFloor(x, y) && !Clear[I(x, y)] && Deco[I(x, y)] != DecoPath) { best = I(x, y); break; }
                }
                AddPoi("shrine", best % W, best / W, null, 0f, RandFacing());
            }
            // chests in the corners farthest from the trail
            var nooks = new List<int>();
            for (int i = 0; i < T.Length; i++)
                if (T[i] == Floor && !Keep[i] && !Prot[i] && trailDist[i] >= 7 && Walls8(i % W, i / W) >= 3) nooks.Add(i);
            nooks.Sort((a, b) => trailDist[a] != trailDist[b] ? trailDist[b].CompareTo(trailDist[a]) : a.CompareTo(b));
            int wantChests = Ri(2, 3), chests = 0;
            foreach (int i in nooks)
            {
                if (chests >= wantChests) break;
                int x = i % W, y = i / W;
                if (Clear[i] || !FarFromPois(x, y, 12f, "chest") || !FarFromPois(x, y, 4f, null)) continue;
                WallNormal(x, y, out int wx, out int wy, out _);
                AddPoi("chest", x, y, null, 0f, wx != 0 || wy != 0 ? FaceOf(wx, wy) : RandFacing());
                chests++;
            }
            foreach (int r in free)
            {
                if (chests >= wantChests) break;
                var c = RoomCentre(r);
                if (Clear[I(c.X, c.Y)]) continue;
                AddPoi("chest", c.X, c.Y, null, 0f, RandFacing());
                chests++;
            }

            // ground variety
            AltPatches(noise, 0.58f);
            MarkShores();

            // rock, rubble and growth
            _occCap = (int)(CountFloor() * 0.28f);
            var order = new List<int>();
            for (int i = 0; i < T.Length; i++)
                if (T[i] == Floor && !Prot[i]) order.Add(i);
            Shuffle(order);
            foreach (int i in order)
            {
                if (Clear[i]) continue;
                int x = i % W, y = i / W;
                bool path = Deco[i] == DecoPath;
                float roll = Rf();
                int room = openDist[i];
                if (field)
                {
                    float pTree = room == 1 ? 0.4f : room == 2 ? 0.2f : 0.04f;
                    if (roll < pTree) PlaceBlocker(x, y, Jit(x, y, 0.45f), "tree", Rf(0.4f, 0.55f), Rf(0.85f, 1.35f), RandFacing());
                    else if (roll < pTree + 0.03f) PlaceBlocker(x, y, Jit(x, y, 0.4f), "rock", Rf(0.5f, 0.75f), Rf(0.8f, 1.3f), RandFacing());
                    else if (roll < pTree + 0.05f) { if (room >= 3) PlaceBlocker(x, y, G.Center(x, y), "rock_big", Rf(1.2f, 1.6f), Rf(0.9f, 1.3f), RandFacing()); }
                    else if (roll < pTree + 0.17f) { if (!path) Prop("foliage", Jit(x, y, 0.8f), 0.5f, RandFacing(), Rf(0.8f, 1.4f)); }
                    else if (roll < pTree + 0.22f) { if (!path) Prop("clutter", Jit(x, y, 0.7f), 0.4f, RandFacing(), Rf(0.8f, 1.2f)); }
                    else if (roll < pTree + 0.25f) { if (room == 1 && !Keep[i]) Prop("breakable", Jit(x, y, 0.5f), 0.4f, RandFacing(), Rf(0.85f, 1.15f)); }
                }
                else if (room >= 3 && roll < 0.035f) PlaceBlocker(x, y, G.Center(x, y), "rock_big", Rf(1.1f, 1.5f), Rf(0.9f, 1.3f), RandFacing());
                else if (room == 1 && roll < 0.12f) PlaceBlocker(x, y, Jit(x, y, 0.35f), "rock", Rf(0.5f, 0.75f), Rf(0.8f, 1.3f), RandFacing());
                else if (room == 1 && roll < 0.17f) { if (!Keep[i]) Prop("breakable", Jit(x, y, 0.5f), 0.4f, RandFacing(), Rf(0.85f, 1.15f)); }
                else if (roll < 0.24f) { if (!path) Prop("clutter", Jit(x, y, 0.7f), 0.4f, RandFacing(), Rf(0.8f, 1.2f)); }
                else if (roll < 0.31f) { if (!path) Prop("foliage", Jit(x, y, 0.8f), 0.5f, RandFacing(), Rf(0.8f, 1.4f)); }
            }
            if (field)
            {
                // fires round the boss glade, lanterns along the road, torches at the way out
                WallLights(10f, "brazier", Prot);
                RoadLanterns(SmoothPath(trail, 4, 2), 13f, 4f);
                DoorTorches(_start.X, _start.Y + 3, 0, -1);
            }
            else WallLights(10f, "torch");

            // monsters in the open caverns (a small field keeps them a little closer to the arrival point)
            MarkDisc(_start.X, _start.Y, field && n < 64 ? 7.5f : 9f, NoSpawn);
            MarkDisc(door.X, protY + 2, 3.2f, NoSpawn);
            var reach = Flood(I(_start.X, _start.Y), true, out _);
            var wideSpots = new List<int>();
            var narrow = new List<int>();
            for (int i = 0; i < T.Length; i++)
            {
                if (!reach[i] || Occ[i] || NoSpawn[i] || Clear[i] || Prot[i]) continue;
                if (openDist[i] >= 2) wideSpots.Add(i); else narrow.Add(i);
            }
            int specials = Math.Max(1, wideSpots.Count / 160);     // small places hold fewer elite and champion groups
            int elites = Math.Min(_q.Goal == DungeonGoal.SlayElites ? Ri(1, 2) : Ri(2, 3), specials), champs = Math.Min(Ri(2, 3), specials);
            PlaceSpawns(wideSpots, narrow, field ? 6f : 6.5f, elites, champs);
        }

        // ═════════════════════════════════════════════ Arena ═════════════════════════════════════════════

        private void GenArena()
        {
            int n = _q.Size > 0 ? Math.Clamp(_q.Size, 36, 56) : 40;
            Init(n, n, _outdoor ? 4 : 3);
            var noise = new ValueNoise(R);

            int ra = n >= 38 ? 8 : 7;
            int acx = W / 2 + Ri(-2, 2), acy = B + ra + 1;
            var arenaCells = CarveArena(acx, acy, ra, _organic, noise);
            int protY = acy + ra + 3;
            for (int y = 0; y <= protY; y++)
                for (int x = 0; x < W; x++) Prot[I(x, y)] = true;
            var door = OpenDoor(acx, acy, ra, acx + Ri(-2, 2), protY);

            // a short winding approach from the south
            _start = new Vector2I(Math.Clamp(door.X + Ri(-8, 8), B + 4, W - B - 5), H - B - 4);
            CarveDisc(_start.X, _start.Y, 3f);
            int midY = (_start.Y + protY + 1) / 2;
            int midX = Math.Clamp((_start.X + door.X) / 2 + Ri(-4, 4), B + 3, W - B - 4);
            var approach = new List<Vector2> { new(_start.X, _start.Y), new(midX, midY), new(door.X + 0.5f, protY + 1) };
            for (int it = 0; it < 2; it++)
            {
                var q = new List<Vector2>(approach.Count * 2) { approach[0] };
                for (int k = 0; k + 1 < approach.Count; k++)
                {
                    q.Add(approach[k] + (approach[k + 1] - approach[k]) * 0.25f);
                    q.Add(approach[k] + (approach[k + 1] - approach[k]) * 0.75f);
                }
                q.Add(approach[^1]);
                approach = q;
            }
            Stamp(approach, 1.6f, 0.8f, DecoPath, 0.9f);
            for (int y = door.Y; y <= protY; y++)
                for (int x = door.X; x <= door.X + 1; x++)
                    if (IsFloor(x, y)) { Deco[I(x, y)] = DecoPath; Keep[I(x, y)] = true; }
            if (!Flood(I(_start.X, _start.Y), false, out _)[I(acx, acy)]) throw new GenRejected("arena is cut off from the approach");

            // rim pits: lava, water or void biting into the edge of the arena, never into its heart
            foreach (int c in arenaCells) Keep[c] = true;
            float arenaRadius = ArenaRadius(ra, _organic);
            if (_pit != "none")
            {
                float pitFrom = Math.Max(6.3f, ra - 1.2f);          // cells from the centre that always stay floor (≥ 11 m of open ground)
                arenaRadius = Math.Min(arenaRadius, MathF.Floor((pitFrom - 0.71f) * Cell * 2f) / 2f);
                int pockets = Ri(3, 5);
                float a0 = Rf(0f, MathF.PI * 2f);
                for (int k = 0; k < pockets; k++)
                {
                    float a = a0 + k * (MathF.PI * 2f / pockets) + Rf(-0.25f, 0.25f);
                    if (MathF.Cos(a) < -0.6f) continue;             // leave the door side alone (south = -cos)
                    float px = acx + MathF.Sin(a) * (ra - 0.9f), py = acy - MathF.Cos(a) * (ra - 0.9f);
                    foreach (int c in arenaCells)
                    {
                        int x = c % W, y = c / W;
                        if (Dist(x, y, px, py) <= 1.35f && Dist(x, y, acx, acy) >= pitFrom && Math.Abs(x - door.X) > 2) T[c] = Pit;
                    }
                }
                SealUnreachable();
                MarkShores();
            }

            D.Rooms.Add(new Rect2I(_start.X - 2, _start.Y - 2, 5, 5));
            D.Rooms.Add(Bounds(arenaCells));
            for (int y = _start.Y - 2; y <= _start.Y + 2; y++)
                for (int x = _start.X - 2; x <= _start.X + 2; x++)
                    if (IsFloor(x, y)) RoomOf[I(x, y)] = 0;
            foreach (int c in arenaCells) RoomOf[c] = 1;

            DungeonCorePois(new Vector2I(_start.X, _start.Y + 3), door, new Vector2I(acx, acy), arenaRadius, 1);

            // dressing along the approach
            _occCap = (int)(CountFloor() * 0.2f);
            for (int y = protY + 1; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = I(x, y);
                    if (T[i] != Floor || Clear[i]) continue;
                    float roll = Rf();
                    if (WallNormal(x, y, out _, out _, out _))
                    {
                        if (roll < 0.14f) PlaceBlocker(x, y, Jit(x, y, 0.3f), _organic ? "rock" : "furniture", Rf(0.5f, 0.7f), Rf(0.8f, 1.2f), RandFacing());
                        else if (roll < 0.3f) Prop("clutter", Jit(x, y, 0.6f), 0.4f, RandFacing(), Rf(0.8f, 1.2f));
                        else if (_organic && roll < 0.42f) Prop("foliage", Jit(x, y, 0.7f), 0.5f, RandFacing(), Rf(0.8f, 1.3f));
                    }
                    else if (roll < 0.04f && Deco[i] != DecoPath) Prop("clutter", Jit(x, y, 0.7f), 0.4f, RandFacing(), Rf(0.8f, 1.2f));
                }
            foreach (int c in arenaCells)
                if (T[c] == Floor && !Clear[c] && Dist(c % W, c / W, acx, acy) > 6.4f && Chance(0.07f))
                    Prop("clutter", Jit(c % W, c / W, 0.6f), 0.4f, RandFacing(), Rf(0.8f, 1.2f));
            WallLights(9f, _organic ? "brazier" : "torch");
        }

        // ═════════════════════════════════════════════ Town ═════════════════════════════════════════════

        private const int PlazaX = 24, PlazaY = 24;

        private void PaveRect(int x0, int y0, int w, int h)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                {
                    if (!Inner(x, y)) continue;
                    int i = I(x, y);
                    T[i] = Floor;
                    Deco[i] = DecoPath;
                    Keep[i] = true;
                }
        }

        private void PaveDisc(float cx, float cy, float r)
        {
            for (int y = (int)MathF.Floor(cy - r); y <= (int)MathF.Ceiling(cy + r); y++)
                for (int x = (int)MathF.Floor(cx - r); x <= (int)MathF.Ceiling(cx + r); x++)
                {
                    if (!IsFloor(x, y) || (x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                    Deco[I(x, y)] = DecoPath;
                    Keep[I(x, y)] = true;
                }
        }

        private float ToPlaza(int x, int y) => FaceOf(PlazaX - x, PlazaY - y);

        /// <summary>A round structure (stall, forge, tent, well, bench) placed by hand.</summary>
        private PropSlot TownStructure(Vector2 pos, string cat, string tag, float radius, float facing)
        {
            var slot = new PropSlot { Category = cat, Pos = pos, Radius = radius, Facing = facing, Blocks = true, Tag = tag };
            D.Props.Add(slot);
            Block(slot, pos, radius, 1);
            return slot;
        }

        /// <summary>An NPC station on the plaza ring with its stall behind it.</summary>
        private void TownStation(string npc, int x, int y, string stallTag, float stallRadius)
        {
            AddPoi("npc", x, y, npc, 0f, ToPlaza(x, y));
            if (stallTag == null) return;
            float dx = x - PlazaX, dy = y - PlazaY, len = MathF.Sqrt(dx * dx + dy * dy);
            var pos = G.Center(x, y) + new Vector2(dx / len, dy / len) * 3.2f;
            TownStructure(pos, "structure", stallTag, stallRadius, ToPlaza(x, y));
            Prop("light", pos + new Vector2(-dy / len, dx / len) * (stallRadius + 0.5f), 0.25f, ToPlaza(x, y), 1f, "lantern");
        }

        /// <summary>A house: a rectangular footprint that blocks, a lantern by the door and some stores against the wall.</summary>
        private void TownHouse(int x0, int y0, int w, int h, int dx, int dy, string tag)
        {
            var centre = G.Origin + new Vector2((x0 + w * 0.5f) * Cell, (y0 + h * 0.5f) * Cell);
            var slot = new PropSlot
            {
                Category = "structure", Tag = tag, Pos = centre, Facing = FaceOf(dx, dy), Blocks = true,
                Radius = Math.Min(w, h) * Cell * 0.5f, Half = new Vector2(w * Cell * 0.5f, h * Cell * 0.5f),
            };
            D.Props.Add(slot);
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                {
                    if (!IsFloor(x, y)) continue;
                    int i = I(x, y);
                    _obs.Add(new Obstacle { P = G.Center(x, y), R = 1.15f, Owner = slot });
                    if (!Occ[i]) { Occ[i] = true; _occCount++; }
                    Solid[i] = true;
                    Keep[i] = true; Clear[i] = true;
                }
            for (int y = y0 - 1; y <= y0 + h; y++)
                for (int x = x0 - 1; x <= x0 + w; x++)
                    if (In(x, y)) Halo[I(x, y)] = true;
            // the doorstep: just outside the wall that faces (dx, dy)
            float reach = (dx != 0 ? w : h) * Cell * 0.5f + 0.7f;
            var step = centre + new Vector2(dx, dy) * reach;
            var along = new Vector2(-dy, dx);
            var lamp = step + along * 1.6f;
            if (G.WalkAt(lamp)) Prop("light", lamp, 0.25f, FaceOf(dx, dy), 1f, "lantern");
            var stores = step - along * 1.8f + new Vector2(dx, dy) * 0.2f;
            if (G.WalkAt(stores))
            {
                Prop("clutter", stores, 0.5f, RandFacing(), Rf(0.9f, 1.15f));
                if (Chance(0.6f)) Prop("clutter", stores - along * 0.9f, 0.4f, RandFacing(), Rf(0.8f, 1.1f));
            }
        }

        /// <summary>殘燈鎮: hand-built and identical for every seed.</summary>
        private void GenTown()
        {
            R = new Random(0x51A7E);
            Init(50, 50, 4);

            // ground inside the wall: a square with cut corners, 35 cells across
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int dx = Math.Abs(x - PlazaX), dy = Math.Abs(y - PlazaY);
                    if (dx <= 17 && dy <= 17 && dx + dy <= 27) T[I(x, y)] = Floor;
                }

            // paving: the plaza round the great brazier, four streets, the gate passage through the south wall
            PaveDisc(PlazaX, PlazaY, 7.6f);
            PaveRect(23, 31, 3, 14);                               // main street and gate passage, rows 31..44
            PaveRect(23, 8, 3, 10);                                // north street
            PaveRect(8, 23, 10, 3);                                // west street
            PaveRect(31, 23, 10, 3);                               // east street
            PaveDisc(24, 9, 2.6f);
            PaveDisc(39, 24, 2.6f);
            PaveDisc(9, 24, 2.6f);

            // points of interest
            _start = new Vector2I(24, 39);
            AddPoi("start", 24, 39, null, 0f, MathF.PI);
            AddPoi("exit", 24, 44, "worldmap", 0f, 0f, 1.5f);
            AddPoi("brazier", PlazaX, PlazaY, null, 2.2f, 0f, 2.6f);
            TownStation("elder", 27, 18, null, 0f);
            TownStation("smith", 30, 21, "forge", 1.5f);
            TownStation("merchant", 18, 21, "stall", 1.3f);
            TownStation("occultist", 18, 27, "tent", 1.5f);
            TownStation("jeweler", 30, 27, "stall", 1.3f);
            TownStation("villager", 21, 18, null, 0f);
            TownStation("villager", 22, 37, null, 0f);
            TownStation("villager", 11, 22, null, 0f);
            AddPoi("stash", 21, 30, null, 0f, ToPlaza(21, 30));
            AddPoi("waypoint", 27, 30, null, 0f, ToPlaza(27, 30));
            AddPoi("tierstatue", 24, 9, null, 0f, FaceOf(0, 1));
            AddPoi("riftstone", 39, 24, null, 0f, FaceOf(-1, 0));

            // houses (footprint, the side their door faces)
            TownHouse(15, 9, 4, 3, 0, 1, "house");
            TownHouse(10, 12, 3, 3, 1, 0, "house");
            TownHouse(9, 17, 3, 4, 1, 0, "house");
            TownHouse(15, 14, 4, 3, 0, 1, "house");
            TownHouse(28, 12, 5, 4, 0, 1, "hall");                 // the lantern keepers' hall, behind the elder
            TownHouse(34, 16, 4, 3, 0, 1, "house");
            TownHouse(35, 10, 3, 3, -1, 0, "house");
            TownHouse(35, 29, 4, 3, 0, -1, "house");
            TownHouse(28, 34, 4, 3, -1, 0, "house");
            TownHouse(34, 34, 3, 3, -1, 0, "house");
            TownHouse(9, 29, 4, 4, 1, 0, "inn");
            TownHouse(17, 34, 4, 3, 1, 0, "house");
            TownHouse(11, 35, 3, 3, 1, 0, "house");

            // the well at the end of the west street, benches round the fire
            TownStructure(G.Center(9, 24), "structure", "well", 1f, FaceOf(1, 0));
            for (int k = 0; k < 4; k++)
            {
                float a = MathF.PI / 4f + k * (MathF.PI / 2f);
                var pos = G.Center(PlazaX, PlazaY) + new Vector2(MathF.Sin(a), -MathF.Cos(a)) * 8.4f;
                TownStructure(pos, "furniture", "bench", 0.6f, FaceOf(-MathF.Sin(a), MathF.Cos(a)));
            }

            // lanterns: street mouths, street sides, the gate and the far ends
            int[] lamps =
            {
                22, 16, 26, 16, 22, 32, 26, 32, 16, 22, 16, 26, 32, 22, 32, 26,      // where the streets meet the plaza
                22, 12, 26, 12, 12, 22, 12, 26, 36, 22, 36, 26, 22, 36, 26, 36,      // along the streets
                22, 41, 26, 41,                                                      // inside the gate
                21, 8, 27, 8, 39, 21, 39, 27, 9, 21, 9, 27,                          // statue, rift stone, well
            };
            for (int k = 0; k + 1 < lamps.Length; k += 2)
            {
                int x = lamps[k], y = lamps[k + 1];
                if (IsFloor(x, y) && !Occ[I(x, y)]) Prop("light", G.Center(x, y), 0.25f, ToPlaza(x, y), 1f, "lantern");
            }

            // a few dead trees by the wall, then weeds and litter off the paving
            _occCap = (int)(CountFloor() * 0.3f);
            int[] trees = { 13, 10, 39, 14, 38, 33, 14, 39, 31, 9, 8, 27, 40, 28, 20, 40 };
            for (int k = 0; k + 1 < trees.Length; k += 2)
                PlaceBlocker(trees[k], trees[k + 1], Jit(trees[k], trees[k + 1], 0.3f), "tree", 0.5f, Rf(0.9f, 1.3f), RandFacing());
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = I(x, y);
                    if (T[i] != Floor || Keep[i] || Clear[i] || Occ[i]) continue;
                    float roll = Rf();
                    bool byWall = Walls8(x, y) > 0;
                    if (roll < (byWall ? 0.22f : 0.07f)) Prop("foliage", Jit(x, y, 0.8f), 0.5f, RandFacing(), Rf(0.8f, 1.3f));
                    else if (roll < (byWall ? 0.3f : 0.1f)) Prop("clutter", Jit(x, y, 0.7f), 0.4f, RandFacing(), Rf(0.8f, 1.2f));
                }
        }

        // ═════════════════════════════════════════════ last resort ═════════════════════════════════════════════

        /// <summary>A plain walled yard with the essential points of interest; only used when every attempt failed.</summary>
        public LevelData Emergency()
        {
            R = new Random(1);
            _lenient = true;
            Init(34, 34, 3);
            for (int y = 6; y <= 27; y++)
                for (int x = 6; x <= 27; x++) { T[I(x, y)] = Floor; Deco[I(x, y)] = DecoRoom; }
            D.Rooms.Add(new Rect2I(6, 6, 22, 22));
            for (int y = 6; y <= 27; y++)
                for (int x = 6; x <= 27; x++) RoomOf[I(x, y)] = 0;
            _start = new Vector2I(17, 25);
            AddPoi("start", 17, 25, null, 0f, MathF.PI);
            if (_q.Kind == AreaKind.Town)
            {
                AddPoi("exit", 17, 27, "worldmap", 0f, 0f, 1.2f);
                AddPoi("brazier", 17, 16, null, 2.2f);
                string[] npcs = { "elder", "smith", "merchant", "occultist", "jeweler", "villager", "villager", "villager" };
                for (int k = 0; k < npcs.Length; k++) AddPoi("npc", 8 + k * 2 + (k >= 4 ? 3 : 0), 10, npcs[k], 0f, 0f, 0.8f);
                AddPoi("stash", 10, 21); AddPoi("waypoint", 24, 21); AddPoi("tierstatue", 10, 16); AddPoi("riftstone", 24, 16);
            }
            else
            {
                bool region = _q.Kind == AreaKind.Overworld && !IsField;
                AddPoi("exit", 17, 27, region ? ZoneDb.Town : ParentArea(), 0f, 0f, 1.2f);
                if (region)
                {
                    string[] ids = DungeonIds();
                    if (ids[0] != null) AddPoi("entrance", 7, 16, ids[0], 0f, FaceOf(1, 0));
                    if (ids[1] != null) AddPoi("entrance", 26, 16, ids[1], 0f, FaceOf(-1, 0));
                    if (ids[2] != null) AddPoi("entrance", 17, 7, ids[2], 0f, FaceOf(0, 1));
                    AddPoi("stronghold", 17, 15, null, 12f);
                    AddPoi("waypoint", 20, 15);
                }
                else
                {
                    AddPoi("bossdoor", 17, 21, null, 0f, MathF.PI);
                    AddPoi("boss", 17, 13, null, 13f);
                }
            }
            try { Finish(); }
            catch (Exception e)
            {
                Log.Error("gen", "emergency layout failed validation", e);
                G.BuildPathing();
            }
            return D;
        }

        // ═════════════════════════════════════════════ validation ═════════════════════════════════════════════

        /// <summary>Moves a position onto the nearest cell that is reachable and free. Returns true when it had to move.</summary>
        private bool Settle(ref Vector2 pos, bool[] reach)
        {
            var c = G.CellOf(pos);
            if (In(c.X, c.Y) && reach[I(c.X, c.Y)] && !Occ[I(c.X, c.Y)]) return false;
            int cx = Math.Clamp(c.X, 0, W - 1), cy = Math.Clamp(c.Y, 0, H - 1);
            for (int r = 1; r < Math.Max(W, H); r++)
            {
                int best = -1;
                float bd = float.MaxValue;
                for (int y = cy - r; y <= cy + r; y++)
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if ((Math.Abs(x - cx) != r && Math.Abs(y - cy) != r) || !In(x, y)) continue;
                        int i = y * W + x;
                        if (!reach[i] || Occ[i]) continue;
                        float d = Dist(x, y, cx, cy);
                        if (d < bd) { bd = d; best = i; }
                    }
                if (best < 0) continue;
                pos = G.Center(best % W, best / W);
                return true;
            }
            throw new GenRejected("no reachable cell to settle on");
        }

        /// <summary>Removes blocking props whose circle reaches into a disc that must stay open.</summary>
        private int ClearBlockers(Vector2 centre, float radius)
        {
            int removed = 0;
            for (int k = D.Props.Count - 1; k >= 0; k--)
            {
                var p = D.Props[k];
                if (!p.Blocks) continue;
                bool hit = false;
                foreach (var o in _obs)
                    if (o.Owner == p && o.P.DistanceTo(centre) < radius + o.R) { hit = true; break; }
                if (!hit) continue;
                D.Props.RemoveAt(k);
                _obs.RemoveAll(o => o.Owner == p);
                removed++;
            }
            if (removed == 0) return 0;
            Array.Clear(Occ, 0, Occ.Length);
            Array.Clear(Solid, 0, Solid.Length);
            _occCount = 0;
            foreach (var o in _obs)
            {
                var c = G.CellOf(o.P);
                if (!In(c.X, c.Y)) continue;
                int i = I(c.X, c.Y);
                if (!Occ[i]) { Occ[i] = true; _occCount++; }
                if (o.R >= SolidRadius) Solid[i] = true;
            }
            return removed;
        }

        /// <summary>
        /// Checks the guarantees of the contract, repairs what can be repaired, registers the obstacles and builds pathing.
        /// </summary>
        private void Finish()
        {
            var start = D.Find("start") ?? throw new GenRejected("layout has no start");
            var sc = G.CellOf(start.Pos);
            if (!IsFloor(sc.X, sc.Y)) throw new GenRejected("start is not on floor");
            _start = sc;
            int repaired = ClearBlockers(start.Pos, 4f);

            // boss arena: at least 11 m of open floor all around its centre, entered through the door only
            var boss = D.Find("boss");
            var door = D.Find("bossdoor");
            if (boss != null)
            {
                var bc = G.CellOf(boss.Pos);
                for (int dy = -6; dy <= 6; dy++)
                    for (int dx = -6; dx <= 6; dx++)
                        if (dx * dx + dy * dy <= 38 && !IsFloor(bc.X + dx, bc.Y + dy)) throw new GenRejected("boss arena is smaller than 11 m");
                repaired += ClearBlockers(boss.Pos, 11f);
                if (door != null && !_lenient)
                {
                    var dc = G.CellOf(door.Pos);
                    if (!IsFloor(dc.X, dc.Y)) throw new GenRejected("boss door is not on floor");
                    int di = I(dc.X, dc.Y);
                    T[di] = Wall;
                    bool sealedOff = !Flood(I(sc.X, sc.Y), false, out _)[I(bc.X, bc.Y)];
                    T[di] = Floor;
                    if (!sealedOff) throw new GenRejected("boss arena has more than one way in");
                }
            }

            // everything must stand on a free cell that can be walked to from the start
            var reach = Flood(I(sc.X, sc.Y), true, out int reachable);
            if (reachable < 40) throw new GenRejected("walkable area is too small");
            int moved = 0;
            foreach (var p in D.Pois)
            {
                var pos = p.Pos;
                if (!Settle(ref pos, reach)) continue;
                p.Pos = pos;
                var c = G.CellOf(pos);
                p.Room = RoomOf[I(c.X, c.Y)];
                moved++;
            }
            foreach (var s in D.Spawns)
            {
                var pos = s.Pos;
                if (!Settle(ref pos, reach)) continue;
                s.Pos = pos;
                var c = G.CellOf(pos);
                s.Room = RoomOf[I(c.X, c.Y)];
                moved++;
            }
            if (boss != null && !_lenient && !reach[I(G.CellOf(boss.Pos).X, G.CellOf(boss.Pos).Y)]) throw new GenRejected("boss cannot be reached");
            if (moved > 0 || repaired > 0) Log.Warn("gen", $"{_q.Kind} '{_q.AreaId}' seed {_q.Seed}: moved {moved} spots onto reachable cells, removed {repaired} misplaced props");

            foreach (var o in _obs) G.AddObstacle(o.P, o.R);
            G.BuildPathing();
        }
    }
}
