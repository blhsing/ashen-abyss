using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Everything that stands on NavGrid.Wall cells.
/// Solid themes (indoors, or a city of stone outdoors): every wall block is extruded to the theme's wall height, its
/// top is textured along the rim and fades into an unlit black lid deeper inside, and a second black sheet at stub
/// height closes the view when the see-through cut opens a wall between the camera and the hero.
/// Open themes: wall masses become irregular rock (height from noise and the distance to the edge) or tree lines with
/// big rocks, optionally fronted by a palisade or a low masonry wall.
/// All of it is drawn with the env_wall / env_cap / env_prop shaders, so it takes part in the see-through cut.
/// </summary>
internal static class EnvWalls
{
    /// <summary>Metres inside a solid block where its textured top has faded to black and the flat lid takes over.</summary>
    private const float CapFade = 2.3f;
    /// <summary>Height of the black section sheet inside wall blocks; equals cut_floor of the wall shaders.</summary>
    private const float StubY = 0.45f;
    /// <summary>How far the black lid continues beyond the built field, so a zoomed-out camera never sees past it.</summary>
    private const float SkirtM = 48f;
    /// <summary>Models above this triangle count are kept out of the back rows of a tree line.</summary>
    private const int HeavyTris = 6500;

    private static readonly int[] DX = { 1, 0, -1, 0 }, DY = { 0, 1, 0, -1 };
    private static readonly Vector3[] FaceN = { Vector3.Right, Vector3.Back, Vector3.Left, Vector3.Forward };

    private enum Style { Masonry, Cave, Cliff }

    private struct Quad
    {
        public Vector3 A, B, C, D, N;       // ring order; N = the direction the front must face
        public float Ca, Cb, Cc, Cd;        // grey level per corner (multiplies the albedo)
        public int Sector;
        public bool Top;
    }

    public static void Build(EnvBuild b)
    {
        if (b == null || b.F == null || b.Theme == null) return;
        if (b.SolidWalls) Guard("solid walls", () => BuildSolid(b));
        else BuildOutdoor(b);
    }

    private static void Guard(string what, Action a)
    {
        try { a(); }
        catch (Exception e) { Log.Error("env", $"walls: {what} failed", e); }
    }

    private static string Set(string s, string fallback) => string.IsNullOrEmpty(s) ? fallback : s;

    private static bool Glossy(string set) =>
        set != null && (set.StartsWith("ice", StringComparison.Ordinal) || set.StartsWith("obsidian", StringComparison.Ordinal));

    // ═════════════════════════════════════ one solid mass ═════════════════════════════════════

    /// <summary>
    /// A set of cells modelled as one solid: side faces along its boundary (rows of displaced vertices from foot to
    /// rim) and a top sheet on the 1 m lattice. Every vertex is a pure function of its lattice index, so neighbouring
    /// pieces meet without cracks even though they end up in different sector meshes.
    /// </summary>
    private sealed class Mass
    {
        public readonly EnvBuild B;
        public readonly EnvField F;
        public readonly bool[] Solid;               // per extended cell
        public readonly float[] D;                  // per lattice vertex: metres inside the mass (0 on its rim and outside)
        public readonly Style Style;
        public readonly float Height;
        public readonly int Rows, Seed;
        /// <summary>Cliffs: height factor per cell, so small outcrops stay low. Null = 1 everywhere.</summary>
        public float[] CellScale;
        /// <summary>Solid blocks under an open sky are buildings: their whole top is a textured roof instead of fading into the black lid.</summary>
        public bool Roofs;
        public readonly List<Quad> Quads = new(4096);
        private readonly float[] _ground;           // terrain height per lattice vertex, null when the terrain pass failed
        private readonly int _cw, _ch, _lw;

        public Mass(EnvBuild b, bool[] solid, float[] dist, Style style, float height)
        {
            B = b; F = b.F; Solid = solid; D = dist; Style = style; Height = height;
            Rows = style == Style.Cliff ? 5 : Mathf.Clamp(Mathf.RoundToInt(height / 1.2f), 2, 6);
            Seed = b.Seed;
            _cw = F.CW; _ch = F.CH; _lw = F.LW;
            _ground = b.Heights != null && b.Heights.Length == F.LW * F.LH ? b.Heights : null;
        }

        /// <summary>Cells beyond the field count as solid, so no faces are built on the edge of the world.</summary>
        public bool IsSolid(int cx, int cy) => cx < 0 || cy < 0 || cx >= _cw || cy >= _ch || Solid[cy * _cw + cx];

        private bool Sub(int sx, int sy) => sx < 0 || sy < 0 || IsSolid(sx >> 1, sy >> 1);

        private float Ground(int i, int j) => _ground != null ? _ground[j * _lw + i] : 0f;

        /// <summary>Unit direction on the xz plane from a rim vertex out of the mass, and how many of its four sub-squares are solid.</summary>
        private Vector2 Out(int i, int j, out int solidCount)
        {
            bool nw = Sub(i - 1, j - 1), ne = Sub(i, j - 1), sw = Sub(i - 1, j), se = Sub(i, j);
            solidCount = (nw ? 1 : 0) + (ne ? 1 : 0) + (sw ? 1 : 0) + (se ? 1 : 0);
            float ox = 0f, oz = 0f;
            if (!nw) { ox -= 1f; oz -= 1f; }
            if (!ne) { ox += 1f; oz -= 1f; }
            if (!sw) { ox -= 1f; oz += 1f; }
            if (!se) { ox += 1f; oz += 1f; }
            float len = Mathf.Sqrt(ox * ox + oz * oz);
            return len > 0.01f ? new Vector2(ox / len, oz / len) : Vector2.Zero;
        }

        private float ScaleAt(int i, int j)
        {
            if (CellScale == null) return 1f;
            float s = 0f;
            for (int q = 0; q < 4; q++)
            {
                int sx = i - 1 + (q & 1), sy = j - 1 + (q >> 1);
                if (sx < 0 || sy < 0) continue;
                int cx = sx >> 1, cy = sy >> 1;
                if (cx >= _cw || cy >= _ch || !Solid[cy * _cw + cx]) continue;
                s = Mathf.Max(s, CellScale[cy * _cw + cx]);
            }
            return s > 0.01f ? s : 1f;
        }

        /// <summary>Height of a rock mass above the terrain: a broad noise field that grows away from the edge, plus bumps.</summary>
        private float RockRise(int i, int j, float x, float z)
        {
            float dd = D[j * _lw + i];
            float baseH = Height * (0.55f + 0.7f * EnvNoise.Fbm(x * 0.09f, z * 0.09f, Seed + 41));
            float grow = 1f + 0.3f * Mathf.SmoothStep(0f, 5f, dd);
            float bumps = (EnvNoise.Fbm(x * 0.31f, z * 0.31f, Seed + 43) - 0.5f) * 1.5f * Mathf.SmoothStep(0f, 1.5f, dd);
            return Mathf.Max(1.2f, (baseH * grow + bumps) * ScaleAt(i, j));
        }

        private float RimRise(int i, int j, float x, float z) => Style switch
        {
            Style.Masonry => Height + (EnvNoise.Value(x * 0.9f, z * 0.9f, Seed + 5) - 0.5f) * 0.12f,
            Style.Cave => Height + (EnvNoise.Fbm(x * 0.19f, z * 0.19f, Seed + 9) - 0.5f) * 1.3f,
            _ => RockRise(i, j, x, z),
        };

        /// <summary>Position of rim vertex (i, j) at one row of the side faces: row 0 is a skirt under the floor, row Rows the rim.</summary>
        public Vector3 Point(int i, int j, int row)
        {
            float x = F.LatX(i), z = F.LatZ(j);
            float y0 = Ground(i, j);
            float t = row / (float)Rows;
            float y = row == 0 ? y0 - (Style == Style.Cliff ? 0.35f : 0.2f) : y0 + RimRise(i, j, x, z) * t;
            var o = Out(i, j, out int n);
            if (o == Vector2.Zero) return new Vector3(x, y, z);

            // displacement along the outward direction: negative = the face recedes into the mass
            float d;
            switch (Style)
            {
                case Style.Masonry:
                    d = row == 0 ? 0f : (EnvNoise.Value3(new Vector3(x, y, z) * 1.3f, Seed + 3) - 0.5f) * 0.07f;
                    break;
                case Style.Cave:
                {
                    float lean = 0.15f + 0.5f * EnvNoise.Value(x * 0.23f, z * 0.23f, Seed + 5);
                    float n3 = EnvNoise.Value3(new Vector3(x, y, z) * 0.55f, Seed + 3) - 0.5f;
                    float corner = n == 1 ? -0.45f : n == 3 ? 0.18f : 0f;         // round the staircase corners of cell-shaped caves
                    d = corner - lean * Mathf.Pow(t, 1.5f) + n3 * (0.25f + 0.5f * Mathf.Sin(Mathf.Pi * t));
                    d = Mathf.Clamp(d, -0.88f, row <= 1 ? 0.1f : 0.22f);
                    break;
                }
                default:
                {
                    float lean = 0.25f + 0.65f * EnvNoise.Value(x * 0.17f, z * 0.17f, Seed + 5);
                    float n3 = EnvNoise.Value3(new Vector3(x, y, z) * 0.42f, Seed + 3) - 0.5f;
                    float corner = n == 1 ? -0.5f : n == 3 ? 0.2f : 0f;
                    float ledge = 0.3f + 0.4f * EnvNoise.Value(x * 0.11f + 9f, z * 0.11f, Seed + 7);
                    d = corner - lean * t + n3 * (0.3f + 0.6f * Mathf.Sin(Mathf.Pi * t)) - (t > ledge ? 0.28f : 0f);
                    d = Mathf.Clamp(d, -0.88f, row <= 1 ? 0.1f : 0.2f);
                    break;
                }
            }
            return new Vector3(x + o.X * d, y, z + o.Y * d);
        }

        /// <summary>Vertex of the top sheet: the rim point on the boundary, otherwise the undisplaced lattice point at its height.</summary>
        public Vector3 TopPoint(int i, int j)
        {
            float dd = D[j * _lw + i];
            if (dd <= 0f) return Point(i, j, Rows);
            float x = F.LatX(i), z = F.LatZ(j);
            float y;
            switch (Style)
            {
                case Style.Masonry: y = Ground(i, j) + Height; break;
                case Style.Cave:
                    y = Ground(i, j) + Height + (EnvNoise.Fbm(x * 0.19f, z * 0.19f, Seed + 9) - 0.5f) * 1.3f * (1f - Mathf.SmoothStep(0.5f, 2f, dd));
                    break;
                default: y = Ground(i, j) + RockRise(i, j, x, z); break;
            }
            return new Vector3(x, y, z);
        }

        /// <summary>Rock deep inside the border sinks into shadow; solid blocks fade to black towards the lid.</summary>
        private float DepthShade(int i, int j) => Mathf.Lerp(1f, 0.4f, Mathf.SmoothStep(5f, 18f, F.WallD[j * _lw + i]));

        private float TopShade(int i, int j) =>
            Style == Style.Cliff ? DepthShade(i, j)
            : Roofs ? Mathf.Lerp(0.97f, 0.62f, Mathf.SmoothStep(2f, 10f, D[j * _lw + i]))
            : 0.42f * (1f - Mathf.SmoothStep(0.15f, CapFade, D[j * _lw + i]));

        private float SideShade(int i, int j, int row)
        {
            float c = row == 0 ? 0.45f : row == 1 && Rows >= 4 ? 0.85f : 1f;      // baked contact shadow at the foot
            if (Style == Style.Cliff) c *= DepthShade(i, j);
            else if (row == Rows) c *= Roofs ? 0.97f : 0.8f;
            return c;
        }

        public void BuildSides()
        {
            for (int cy = 0; cy < _ch; cy++)
                for (int cx = 0; cx < _cw; cx++)
                {
                    if (!Solid[cy * _cw + cx]) continue;
                    int sector = F.SectorOfCell(cx, cy);
                    for (int d = 0; d < 4; d++)
                    {
                        if (IsSolid(cx + DX[d], cy + DY[d])) continue;
                        int i0 = cx * 2 + (d == 0 ? 2 : 0), j0 = cy * 2 + (d == 1 ? 2 : 0);
                        int di = d == 1 || d == 3 ? 1 : 0, dj = 1 - di;
                        for (int s = 0; s < 2; s++)
                        {
                            int ia = i0 + di * s, ja = j0 + dj * s, ib = ia + di, jb = ja + dj;
                            Vector3 pa = Point(ia, ja, 0), pb = Point(ib, jb, 0);
                            float ca = SideShade(ia, ja, 0), cb = SideShade(ib, jb, 0);
                            for (int r = 1; r <= Rows; r++)
                            {
                                Vector3 qa = Point(ia, ja, r), qb = Point(ib, jb, r);
                                float ea = SideShade(ia, ja, r), eb = SideShade(ib, jb, r);
                                Quads.Add(new Quad { A = pa, B = pb, C = qb, D = qa, N = FaceN[d], Ca = ca, Cb = cb, Cc = eb, Cd = ea, Sector = sector });
                                pa = qa; pb = qb; ca = ea; cb = eb;
                            }
                        }
                    }
                }
        }

        /// <summary>The top sheet. With <paramref name="fade"/> the part that has gone black is left to the flat lid.</summary>
        public void BuildTop(bool fade)
        {
            for (int cy = 0; cy < _ch; cy++)
                for (int cx = 0; cx < _cw; cx++)
                {
                    if (!Solid[cy * _cw + cx]) continue;
                    int sector = F.SectorOfCell(cx, cy);
                    for (int q = 0; q < 4; q++)
                    {
                        int i = cx * 2 + (q & 1), j = cy * 2 + (q >> 1);
                        if (fade)
                        {
                            int k = j * _lw + i;
                            float m = Mathf.Min(Mathf.Min(D[k], D[k + 1]), Mathf.Min(D[k + _lw], D[k + _lw + 1]));
                            if (m >= CapFade) continue;
                        }
                        Quads.Add(new Quad
                        {
                            A = TopPoint(i, j), B = TopPoint(i + 1, j), C = TopPoint(i + 1, j + 1), D = TopPoint(i, j + 1), N = Vector3.Up,
                            Ca = TopShade(i, j), Cb = TopShade(i + 1, j), Cc = TopShade(i + 1, j + 1), Cd = TopShade(i, j + 1),
                            Sector = sector, Top = true,
                        });
                    }
                }
        }

        private static long Key(Vector3 p)
        {
            long x = Mathf.RoundToInt(p.X * 32f) & 0x1FFFFF, y = Mathf.RoundToInt(p.Y * 32f) & 0x1FFFFF, z = Mathf.RoundToInt(p.Z * 32f) & 0x1FFFFF;
            return (x << 42) | (y << 21) | z;
        }

        private static void Accumulate(Dictionary<long, Vector3> acc, Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            var g = (b - a).Cross(c - a);                    // length = 2 × area, so big faces weigh more
            if (g.Dot(facing) < 0f) g = -g;
            long ka = Key(a), kb = Key(b), kc = Key(c);
            acc.TryGetValue(ka, out var s); acc[ka] = s + g;
            acc.TryGetValue(kb, out s); acc[kb] = s + g;
            acc.TryGetValue(kc, out s); acc[kc] = s + g;
        }

        private static Vector3 Welded(Dictionary<long, Vector3> acc, Vector3 p, Vector3 fallback) =>
            acc.TryGetValue(Key(p), out var s) && s.LengthSquared() > 1e-10f ? s.Normalized() : fallback;

        private static Color G(float v) => new(v, v, v, 1f);

        /// <summary>
        /// Sends the quads to the per-sector batches. Rock gets normals welded over the whole mass first (not per
        /// batch), so there is no lighting seam where a cliff crosses a sector border; masonry keeps flat faces and
        /// gets metre-scaled UVs (u along the face, v down it; tops map by world xz).
        /// </summary>
        public void Emit(string sideSet, WallFlags sideFlags, string topSet, WallFlags topFlags)
        {
            Dictionary<long, Vector3> weld = null;
            if (Style != Style.Masonry)
            {
                weld = new Dictionary<long, Vector3>(Quads.Count * 2);
                for (int n = 0; n < Quads.Count; n++)
                {
                    var q = Quads[n];
                    Accumulate(weld, q.A, q.B, q.C, q.N);
                    Accumulate(weld, q.A, q.C, q.D, q.N);
                }
            }

            EnvMesh mesh = null;
            int lastSector = -1;
            bool lastTop = false;
            for (int n = 0; n < Quads.Count; n++)
            {
                var q = Quads[n];
                if (mesh == null || q.Sector != lastSector || q.Top != lastTop)
                {
                    mesh = B.Batch(q.Top ? topSet : sideSet, q.Top ? topFlags : sideFlags, Colors.White, q.Sector);
                    lastSector = q.Sector;
                    lastTop = q.Top;
                }
                Vector3 na = q.N, nb = q.N, nc = q.N, nd = q.N;
                if (weld != null)
                {
                    na = Welded(weld, q.A, q.N); nb = Welded(weld, q.B, q.N); nc = Welded(weld, q.C, q.N); nd = Welded(weld, q.D, q.N);
                }
                int i0, i1, i2, i3;
                if (mesh.HasUv && !q.Top)
                {
                    var tan = Vector3.Up.Cross(q.N).Normalized();
                    i0 = mesh.Vert(q.A, na, new Vector2(q.A.Dot(tan), -q.A.Y), tan, G(q.Ca));
                    i1 = mesh.Vert(q.B, nb, new Vector2(q.B.Dot(tan), -q.B.Y), tan, G(q.Cb));
                    i2 = mesh.Vert(q.C, nc, new Vector2(q.C.Dot(tan), -q.C.Y), tan, G(q.Cc));
                    i3 = mesh.Vert(q.D, nd, new Vector2(q.D.Dot(tan), -q.D.Y), tan, G(q.Cd));
                }
                else
                {
                    i0 = mesh.Raw(q.A, na, G(q.Ca));
                    i1 = mesh.Raw(q.B, nb, G(q.Cb));
                    i2 = mesh.Raw(q.C, nc, G(q.Cc));
                    i3 = mesh.Raw(q.D, nd, G(q.Cd));
                }
                mesh.TriFacing(i0, i1, i2, q.N);
                mesh.TriFacing(i0, i2, i3, q.N);
            }
        }
    }

    // ═════════════════════════════════════ solid themes ═════════════════════════════════════

    private static void BuildSolid(EnvBuild b)
    {
        var f = b.F;
        var t = b.Theme;
        var solid = new bool[f.CW * f.CH];
        int count = 0;
        for (int cy = 0; cy < f.CH; cy++)
            for (int cx = 0; cx < f.CW; cx++)
                if (f.IsWall(cx, cy)) { solid[cy * f.CW + cx] = true; count++; }
        if (count == 0) return;

        string wall = Set(t.WallTex, "castle_blocks"), top = Set(t.WallTopTex, wall);
        var mass = new Mass(b, solid, f.WallD, t.Organic ? Style.Cave : Style.Masonry, Mathf.Clamp(t.WallHeight, 1.6f, 9f)) { Roofs = t.Outdoor };
        mass.BuildSides();
        mass.BuildTop(!mass.Roofs);
        if (t.Organic)
        {
            var flags = Glossy(wall) ? WallFlags.Tri | WallFlags.Gloss : WallFlags.Tri;
            mass.Emit(wall, flags, top, WallFlags.Tri);
        }
        else mass.Emit(wall, WallFlags.None, top, WallFlags.None);
        Guard("wall lids", () => BuildLids(b, mass));
        Log.Debug("env", $"walls: solid style={mass.Style} cells={count} height={mass.Height:0.0} rows={mass.Rows} quads={mass.Quads.Count}");
    }

    /// <summary>
    /// The unlit black sheets of a solid theme: the lid at wall height over everything deeper than the textured rim
    /// (continued far beyond the field), and the section sheet at stub height that shows when a wall is cut open.
    /// Both are merged into long strips; neither needs sectors.
    /// </summary>
    private static void BuildLids(EnvBuild b, Mass mass)
    {
        var f = b.F;
        var d = mass.D;
        int lw = f.LW, lh = f.LH;
        float h = mass.Height;
        var lid = new EnvMesh(false);
        var stub = new EnvMesh(false);
        for (int j = 0; j < lh - 1; j++)
        {
            int runLid = -1, runStub = -1;
            for (int i = 0; i < lw; i++)                   // the last column only closes open runs
            {
                float m = -1f;
                if (i < lw - 1)
                {
                    int k = j * lw + i;
                    m = Mathf.Min(Mathf.Min(d[k], d[k + 1]), Mathf.Min(d[k + lw], d[k + lw + 1]));
                }
                if (m >= CapFade && !mass.Roofs) { if (runLid < 0) runLid = i; }
                else if (runLid >= 0) { Flat(lid, f.LatX(runLid), f.LatX(i), f.LatZ(j), f.LatZ(j + 1), h); runLid = -1; }
                if (m >= 0.99f) { if (runStub < 0) runStub = i; }
                else if (runStub >= 0) { Flat(stub, f.LatX(runStub), f.LatX(i), f.LatZ(j), f.LatZ(j + 1), StubY); runStub = -1; }
            }
        }
        float x0 = f.X0, z0 = f.Z0, x1 = f.X0 + f.CW * NavGrid.Cell, z1 = f.Z0 + f.CH * NavGrid.Cell;
        Flat(lid, x0 - SkirtM, x1 + SkirtM, z0 - SkirtM, z0, h);
        Flat(lid, x0 - SkirtM, x1 + SkirtM, z1, z1 + SkirtM, h);
        Flat(lid, x0 - SkirtM, x0, z0, z1, h);
        Flat(lid, x1, x1 + SkirtM, z0, z1, h);

        var mat = EnvMats.Cap(new Color(0f, 0f, 0f));
        // under a sun the lid has to throw the shadow of the block it closes; indoors nothing is above the torches
        var lidMesh = lid.Build();
        if (lidMesh != null) { b.AddMesh(b.Walls, "Lid", lidMesh, mat, b.Theme.Outdoor); b.Triangles += lid.TriangleCount; }
        var stubMesh = stub.Build();
        if (stubMesh != null) { b.AddMesh(b.Walls, "Section", stubMesh, mat, false); b.Triangles += stub.TriangleCount; }
    }

    private static void Flat(EnvMesh m, float x0, float x1, float z0, float z1, float y)
    {
        var c = new Color(1f, 1f, 1f, 1f);
        int a = m.Raw(new Vector3(x0, y, z0), Vector3.Up, c), b = m.Raw(new Vector3(x1, y, z0), Vector3.Up, c);
        int d = m.Raw(new Vector3(x1, y, z1), Vector3.Up, c), e = m.Raw(new Vector3(x0, y, z1), Vector3.Up, c);
        m.Tri(a, b, d);
        m.Tri(a, d, e);
    }

    // ═════════════════════════════════════ open themes ═════════════════════════════════════

    private static void BuildOutdoor(EnvBuild b)
    {
        var f = b.F;
        var t = b.Theme;
        string border = Set(t.Border, "forest");
        var rock = new bool[f.CW * f.CH];
        float[] scale = null;
        int rockCells = 0;
        Guard("classifying wall masses", () => rockCells = Classify(b, border, rock, out scale));

        if (rockCells > 0)
            Guard("cliffs", () =>
            {
                string set = Set(t.CliffTex, Set(t.WallTex, "grey_rock"));
                var mass = new Mass(b, rock, Distance(f, rock), Style.Cliff, Mathf.Clamp(t.WallHeight, 2.5f, 7f)) { CellScale = scale };
                mass.BuildSides();
                mass.BuildTop(false);
                var flags = Glossy(set) ? WallFlags.Tri | WallFlags.Gloss : WallFlags.Tri;
                mass.Emit(set, flags, set, flags);
                Log.Debug("env", $"walls: cliffs cells={rockCells} quads={mass.Quads.Count} tex={set}");
            });

        if (border is "palisade" or "lowwall") Guard("fence", () => BuildFence(b, rock, border == "palisade"));
        Guard("tree lines", () => PlantBorder(b, rock));
    }

    /// <summary>
    /// Decides which wall cells are rock. Free-standing clumps always are; the rest follows the theme's border kind
    /// (cliff = all, mixed = a noise mask covering about CliffShare of the edge, otherwise none: trees stand there).
    /// </summary>
    private static int Classify(EnvBuild b, string border, bool[] rock, out float[] scale)
    {
        var f = b.F;
        var t = b.Theme;
        scale = null;
        float share = border == "cliff" ? 1f : border == "mixed" ? Mathf.Clamp(t.CliffShare, 0f, 1f) : 0f;
        float thr = 0.5f + (share - 0.5f) * 0.42f;           // Fbm sits around 0.5 with a spread of roughly 0.13
        if (share > 0.001f)
            for (int cy = 0; cy < f.CH; cy++)
                for (int cx = 0; cx < f.CW; cx++)
                {
                    if (!f.IsWall(cx, cy)) continue;
                    if (share >= 0.999f) { rock[cy * f.CW + cx] = true; continue; }
                    var c = f.CellCenter(cx, cy);
                    rock[cy * f.CW + cx] = EnvNoise.Fbm(c.X * 0.035f, c.Z * 0.035f, b.Seed + 71) < thr;
                }

        foreach (var isl in f.Islands())
        {
            if (isl.Cells.Count > 90) continue;                // a wood in the middle of the map follows the border rule
            scale ??= NewScale(rock.Length);
            float s = Mathf.Clamp(0.35f + Mathf.Sqrt(isl.Cells.Count) / 6f, 0.45f, 1f);
            foreach (var c in isl.Cells)
            {
                int k = c.Y * f.CW + c.X;
                rock[k] = true;
                scale[k] = s;
            }
        }
        int n = 0;
        for (int k = 0; k < rock.Length; k++) if (rock[k]) n++;
        return n;
    }

    private static float[] NewScale(int n)
    {
        var s = new float[n];
        Array.Fill(s, 1f);
        return s;
    }

    /// <summary>Chamfer distance on the lattice into a cell mask: 0 on and outside its boundary (same convention as EnvField.WallD).</summary>
    private static float[] Distance(EnvField f, bool[] solid)
    {
        const float Far = 1e6f, D1 = 1f, D2 = 1.41421f;
        int lw = f.LW, lh = f.LH, cw = f.CW, ch = f.CH;
        var d = new float[lw * lh];
        bool Sub(int sx, int sy)
        {
            if (sx < 0 || sy < 0) return true;
            int cx = sx >> 1, cy = sy >> 1;
            return cx >= cw || cy >= ch || solid[cy * cw + cx];
        }
        for (int j = 0; j < lh; j++)
            for (int i = 0; i < lw; i++)
                d[j * lw + i] = Sub(i - 1, j - 1) && Sub(i, j - 1) && Sub(i - 1, j) && Sub(i, j) ? Far : 0f;
        for (int j = 0; j < lh; j++)
            for (int i = 0; i < lw; i++)
            {
                int k = j * lw + i;
                float v = d[k];
                if (v == 0f) continue;
                if (i > 0) v = Mathf.Min(v, d[k - 1] + D1);
                if (j > 0)
                {
                    v = Mathf.Min(v, d[k - lw] + D1);
                    if (i > 0) v = Mathf.Min(v, d[k - lw - 1] + D2);
                    if (i < lw - 1) v = Mathf.Min(v, d[k - lw + 1] + D2);
                }
                d[k] = v;
            }
        for (int j = lh - 1; j >= 0; j--)
            for (int i = lw - 1; i >= 0; i--)
            {
                int k = j * lw + i;
                float v = d[k];
                if (v == 0f) continue;
                if (i < lw - 1) v = Mathf.Min(v, d[k + 1] + D1);
                if (j < lh - 1)
                {
                    v = Mathf.Min(v, d[k + lw] + D1);
                    if (i < lw - 1) v = Mathf.Min(v, d[k + lw + 1] + D2);
                    if (i > 0) v = Mathf.Min(v, d[k + lw - 1] + D2);
                }
                d[k] = v;
            }
        for (int k = 0; k < d.Length; k++) if (d[k] > 1000f) d[k] = 1000f;
        return d;
    }

    // ───────────── palisade / low wall ─────────────

    /// <summary>
    /// A fence between open ground and the tree line. It runs from the middle of each boundary edge to the middle of
    /// the next one, which keeps straight runs straight and turns the staircase of a diagonal edge into a clean line.
    /// The line sits 0.4 m inside the wall cells; gaps are left at exits and entrances.
    /// </summary>
    private static void BuildFence(EnvBuild b, bool[] rock, bool palisade)
    {
        var f = b.F;
        var t = b.Theme;
        var gaps = new List<Vector2>();
        if (b.Data.Pois != null)
            foreach (var p in b.Data.Pois)
                if (p != null && p.Kind is "exit" or "entrance" or "bossdoor") gaps.Add(p.Pos);

        bool IsFloor(int cx, int cy) => f.InCells(cx, cy) && f.Cell(cx, cy) == NavGrid.Floor;
        bool Edge(int cx, int cy, int d)
        {
            if (!IsFloor(cx, cy)) return false;
            int nx = cx + DX[d], ny = cy + DY[d];
            return f.InCells(nx, ny) && f.IsWall(nx, ny) && !rock[ny * f.CW + nx];
        }
        Vector3 Mid(int cx, int cy, int d) => f.CellCenter(cx, cy) + new Vector3(DX[d], 0f, DY[d]) * 1.4f;
        bool InGap(Vector3 p)
        {
            foreach (var g in gaps)
            {
                float dx = p.X - g.X, dz = p.Z - g.Y;
                if (dx * dx + dz * dz < 2.5f * 2.5f) return true;
            }
            return false;
        }

        string wallSet = Set(t.WallTex, "drystone_wall"), topSet = Set(t.WallTopTex, wallSet);
        float height = Mathf.Clamp(t.WallHeight, 1.2f, 4f);
        int segments = 0;
        for (int cy = 0; cy < f.CH; cy++)
            for (int cx = 0; cx < f.CW; cx++)
                for (int d = 0; d < 4; d++)
                {
                    if (!Edge(cx, cy, d)) continue;
                    // walk on with the open side on the left: turn towards it, go straight, or wrap round the wall corner
                    int td = (d + 1) & 3;
                    int ax = cx + DX[td], ay = cy + DY[td];
                    int bx = ax + DX[d], by = ay + DY[d];
                    int ncx, ncy, nd;
                    if (!IsFloor(ax, ay)) { ncx = cx; ncy = cy; nd = td; }
                    else if (f.IsWall(bx, by)) { ncx = ax; ncy = ay; nd = d; }
                    else { ncx = bx; ncy = by; nd = (td + 2) & 3; }

                    var from = Mid(cx, cy, d);
                    var r = EnvRng.For(b.Seed, cy * f.CW + cx, 601 + d);
                    if (!palisade && !InGap(from)) Pier(b, from, d, wallSet, topSet, height, ref r);
                    if (!Edge(ncx, ncy, nd)) continue;                 // the fence ends here (water, rock, edge of the map)
                    var to = Mid(ncx, ncy, nd);
                    if (InGap((from + to) * 0.5f)) continue;
                    if (palisade) EnvDress.Palisade(b, from, to, ref r);
                    else WallRun(b, from, to, wallSet, topSet, height, ref r);
                    segments++;
                }
        Log.Debug("env", $"walls: {(palisade ? "palisade" : "low wall")} segments={segments} gaps={gaps.Count}");
    }

    private static void WallRun(EnvBuild b, Vector3 from, Vector3 to, string wallSet, string topSet, float height, ref EnvRng r)
    {
        var dir = to - from;
        float len = dir.Length();
        if (len < 0.2f) return;
        var xf = new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(dir.X, dir.Z)), from);       // local +Z runs along the wall
        var mid = (from + to) * 0.5f;
        float h = height * r.Range(0.9f, 1f);
        var m = b.Batch(wallSet, WallFlags.None, Colors.White, mid);
        m.Xf = xf;
        m.UvOff = new Vector2(r.Range(0f, 3f), r.Range(0f, 3f));
        m.Box(new Vector3(-0.28f, -0.25f, 0f), new Vector3(0.28f, h, len), new Color(1f, 1f, 1f, 1f), 0.55f, false);
        m.Xf = Transform3D.Identity;
        m = b.Batch(topSet, WallFlags.None, Colors.White, mid);
        m.Xf = xf;
        m.UvOff = new Vector2(r.Range(0f, 3f), r.Range(0f, 3f));
        m.Box(new Vector3(-0.38f, h, 0f), new Vector3(0.38f, h + 0.14f, len), new Color(1f, 1f, 1f, 1f), 1f, true, true);
        m.Xf = Transform3D.Identity;
    }

    private static void Pier(EnvBuild b, Vector3 at, int d, string wallSet, string topSet, float height, ref EnvRng r)
    {
        var xf = new Transform3D(new Basis(Vector3.Up, d * Mathf.Pi * 0.5f + r.Range(-0.06f, 0.06f)), at);
        float h = height + r.Range(0.12f, 0.3f);
        var m = b.Batch(wallSet, WallFlags.None, Colors.White, at);
        m.Xf = xf;
        m.UvOff = new Vector2(r.Range(0f, 3f), r.Range(0f, 3f));
        m.BoxAt(new Vector3(0f, -0.25f, 0f), new Vector3(0.74f, h + 0.25f, 0.74f), new Color(1f, 1f, 1f, 1f), 0.55f, false);
        m.Xf = Transform3D.Identity;
        m = b.Batch(topSet, WallFlags.None, Colors.White, at);
        m.Xf = xf;
        m.BoxAt(new Vector3(0f, h, 0f), new Vector3(0.9f, 0.16f, 0.9f), new Color(1f, 1f, 1f, 1f), 1f, true, true);
        m.Xf = Transform3D.Identity;
    }

    // ───────────── tree lines and big rocks ─────────────

    private sealed class Pick
    {
        public string Name;
        public PropDef Def;
        public float Any, Light;        // weights: anywhere / in the back rows
    }

    /// <summary>The loadable models of a theme list ('@' dressing kinds and missing files are skipped; missing ones are logged once by EnvProps).</summary>
    private static List<Pick> Usable(string[] list)
    {
        var res = new List<Pick>();
        if (list == null) return res;
        foreach (var e in list)
        {
            if (string.IsNullOrEmpty(e) || e[0] == '@') continue;
            bool dup = false;
            foreach (var p in res) if (p.Name == e) { dup = true; break; }
            if (dup) continue;
            var def = EnvProps.Def(e);
            if (def == null) continue;
            bool heavy = def.Tris > HeavyTris;
            res.Add(new Pick { Name = e, Def = def, Any = heavy ? 0.4f : 1f, Light = heavy ? 0f : 1f });
        }
        return res;
    }

    private static Pick Cheapest(List<Pick> list)
    {
        Pick best = null;
        foreach (var p in list) if (best == null || p.Def.Tris < best.Def.Tris) best = p;
        return best;
    }

    /// <summary>Expected triangles of one pick from the list (the cheapest model when no entry carries weight).</summary>
    private static float AverageTris(List<Pick> list, bool lightOnly)
    {
        float w = 0f, sum = 0f;
        foreach (var p in list)
        {
            float pw = lightOnly ? p.Light : p.Any;
            w += pw;
            sum += pw * p.Def.Tris;
        }
        if (w > 0.0001f) return Mathf.Max(sum / w, 1f);
        var cheapest = Cheapest(list);
        return cheapest != null ? Mathf.Max(cheapest.Def.Tris, 1) : 1f;
    }

    private static Pick Choose(List<Pick> list, ref EnvRng r, bool lightOnly)
    {
        float total = 0f;
        for (int i = 0; i < list.Count; i++) total += lightOnly ? list[i].Light : list[i].Any;
        float x = r.F() * total;
        if (total <= 0.0001f) return Cheapest(list);
        for (int i = 0; i < list.Count; i++)
        {
            x -= lightOnly ? list[i].Light : list[i].Any;
            if (x <= 0f) return list[i];
        }
        return list[list.Count - 1];
    }

    /// <summary>
    /// Trees (and now and then a big rock) on the wall cells that are not rock: dense along the inside edge, thinning
    /// out towards the edge of the map. The models are heavy, so every 24 m sector has a triangle budget; the rows
    /// next to the playfield are served first and the rows behind them only use the light models.
    /// </summary>
    private static void PlantBorder(EnvBuild b, bool[] rock)
    {
        var f = b.F;
        var t = b.Theme;
        var treeList = Usable(t.Trees);
        var rockList = Usable(t.BigRocks);
        if (treeList.Count == 0 && rockList.Count == 0)
        {
            if (EnvAssets.LogOnce("border:" + t.Id)) Log.Warn("env", $"theme '{t.Id}' has no loadable tree or big-rock model for its border; the edge stays bare");
            return;
        }
        int q = b.Quality;
        int frontBudget = q switch { 0 => 60_000, 1 => 85_000, 2 => 115_000, _ => 170_000 };      // per 24 m sector; the sun shadow pass draws them a second time
        int backBudget = frontBudget / 2;
        int totalBudget = frontBudget + backBudget;
        int hardCap = totalBudget + totalBudget * 4 / 10;
        float thin = q == 0 ? 0.7f : 1f;
        int sectors = f.SW * f.SH;
        var used = new int[sectors];
        var cheapTree = Cheapest(treeList);
        var cheapRock = Cheapest(rockList);
        int trees = 0, rocks = 0, dropped = 0;

        const int MaxDepth = 12;
        static float Density(int depth) => depth switch { 1 => 0.8f, 2 => 0.6f, 3 => 0.42f, 4 => 0.28f, 5 => 0.22f, <= 8 => 0.12f, _ => 0.06f };

        // Fit every sector into its budget before planting: first by leaving the heavy models out, then by thinning
        // evenly. (Spending the budget in scan order would leave one end of a tree line dense and the other bare.)
        var mix = new float[sectors];           // share of front-row picks that may be heavy models
        var keepFront = new float[sectors];
        var keepBack = new float[sectors];
        if (cheapTree != null)
        {
            var wantFront = new float[sectors];
            var wantBack = new float[sectors];
            for (int cy = 0; cy < f.CH; cy++)
                for (int cx = 0; cx < f.CW; cx++)
                {
                    int k = cy * f.CW + cx;
                    int depth = f.CellWallD[k];
                    if (depth < 1 || depth > MaxDepth || rock[k] || !f.IsWall(cx, cy)) continue;
                    (depth <= 2 ? wantFront : wantBack)[f.SectorOfCell(cx, cy)] += Density(depth) * thin;
                }
            float avgAny = AverageTris(treeList, false), avgLight = AverageTris(treeList, true);
            for (int s = 0; s < sectors; s++)
            {
                mix[s] = 1f; keepFront[s] = 1f; keepBack[s] = 1f;
                float wf = wantFront[s], wb = wantBack[s];
                if (wf * avgAny > frontBudget)
                {
                    if (wf * avgLight <= frontBudget && avgAny > avgLight + 1f)
                        mix[s] = Mathf.Clamp((frontBudget / wf - avgLight) / (avgAny - avgLight), 0f, 1f);
                    else
                    {
                        mix[s] = 0f;
                        keepFront[s] = Mathf.Clamp(frontBudget / (wf * avgLight), 0.15f, 1f);
                    }
                }
                if (wb * avgLight > backBudget) keepBack[s] = Mathf.Clamp(backBudget / (wb * avgLight), 0.1f, 1f);
            }
        }

        for (int depth = 1; depth <= MaxDepth; depth++)
        {
            float dens = thin * Density(depth);
            bool front = depth <= 2;
            for (int cy = 0; cy < f.CH; cy++)
                for (int cx = 0; cx < f.CW; cx++)
                {
                    int k = cy * f.CW + cx;
                    if (f.CellWallD[k] != depth || rock[k] || !f.IsWall(cx, cy)) continue;
                    var r = EnvRng.For(b.Seed, k, 4177);
                    int sector = f.SectorOfCell(cx, cy);
                    var c = f.CellCenter(cx, cy);
                    float jit = depth == 1 ? 0.4f : 0.8f;          // trunks of the first row stay clear of the walkable cells
                    float x = c.X + r.Range(-jit, jit), z = c.Z + r.Range(-jit, jit);

                    if (cheapRock != null && front && r.Chance(depth == 1 ? t.BorderRocks : t.BorderRocks * 0.5f))
                    {
                        var rk = Choose(rockList, ref r, mix[sector] < 0.5f);
                        if (used[sector] + rk.Def.Tris > hardCap) rk = cheapRock;
                        if (used[sector] + rk.Def.Tris > hardCap) { dropped++; continue; }
                        // fit the footprint into the cell (first row) or let it spill a little (second row)
                        float radius = depth == 1 ? r.Range(0.9f, 1.15f) : r.Range(1.1f, 1.7f);
                        float rs = Mathf.Clamp(radius / Mathf.Max(rk.Def.Foot, 0.05f), 0.35f, 4.5f);
                        if (EnvProps.Place(b, rk.Name, new Vector3(x, b.GroundY(x, z) - 0.06f * rs, z), r.Angle(), rs, true))
                        {
                            used[sector] += rk.Def.Tris;
                            rocks++;
                        }
                        continue;
                    }

                    if (cheapTree == null || !r.Chance(dens * (front ? keepFront[sector] : keepBack[sector]))) continue;
                    var pick = Choose(treeList, ref r, !front || r.F() >= mix[sector]);
                    if (used[sector] + pick.Def.Tris > hardCap) pick = cheapTree;
                    if (used[sector] + pick.Def.Tris > hardCap) { dropped++; continue; }
                    // a stepped canopy: modest trees next to the path, taller ones behind them
                    float sc = EnvProps.BaseScale(pick.Name) * (depth == 1 ? r.Range(0.95f, 1.2f) : r.Range(1.1f, 1.55f));
                    // the lens hangs about 12 m up: nothing in a tree line may reach it
                    float tallest = depth == 1 ? 8.5f : 10f;
                    if (pick.Def.Height * sc > tallest) sc = tallest / pick.Def.Height;
                    if (EnvProps.Place(b, pick.Name, new Vector3(x, b.GroundY(x, z) - 0.1f * sc, z), r.Angle(), sc, front))
                    {
                        used[sector] += pick.Def.Tris;
                        trees++;
                    }
                }
        }

        // lumps at the foot of the cliffs: boulders half buried in the rock face
        if (cheapRock != null)
            for (int cy = 0; cy < f.CH; cy++)
                for (int cx = 0; cx < f.CW; cx++)
                {
                    int k = cy * f.CW + cx;
                    if (!rock[k] || f.CellWallD[k] != 1) continue;
                    var r = EnvRng.For(b.Seed, k, 9241);
                    if (!r.Chance(0.07f)) continue;
                    int sector = f.SectorOfCell(cx, cy);
                    var rk = Choose(rockList, ref r, true);
                    if (used[sector] + rk.Def.Tris > hardCap) continue;
                    var c = f.CellCenter(cx, cy);
                    float rs = Mathf.Clamp(r.Range(0.85f, 1.05f) / Mathf.Max(rk.Def.Foot, 0.05f), 0.35f, 4.5f);
                    if (EnvProps.Place(b, rk.Name, new Vector3(c.X, b.GroundY(c.X, c.Z) - 0.05f, c.Z), r.Angle(), rs, true))
                    {
                        used[sector] += rk.Def.Tris;
                        rocks++;
                    }
                }
        Log.Debug("env", $"walls: border trees={trees} rocks={rocks} dropped={dropped} (sector budget {frontBudget}+{backBudget} tris, hard cap {hardCap})");
    }
}
