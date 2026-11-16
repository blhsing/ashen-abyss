using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// The NavGrid seen by the renderer: the grid plus an apron of solid cells around it (so the camera never looks off
/// the edge of the world), a 1 m vertex lattice over both, and distance fields on that lattice that drive pit slopes,
/// embankments, wall-top fades and baked contact shadows.
/// Extended cell (cx, cy) is NavGrid cell (cx - A, cy - A); lattice vertex (i, j) sits at world (X0 + i, Z0 + j).
/// </summary>
internal sealed class EnvField
{
    public const int Sector = 12;                    // cells per render sector (24 m)

    public readonly NavGrid G;
    public readonly byte[] Deco;
    public readonly int A, CW, CH, LW, LH, SW, SH;
    public readonly float X0, Z0;
    /// <summary>Metres from a lattice vertex inside solid wall to the nearest open ground (0 outside walls).</summary>
    public readonly float[] WallD;
    /// <summary>Metres from a lattice vertex inside a pit to its rim (0 outside pits).</summary>
    public readonly float[] PitD;
    /// <summary>Metres from a lattice vertex on open ground to the nearest wall (0 on and inside walls).</summary>
    public readonly float[] OpenD;
    /// <summary>Chessboard distance in cells from a wall cell to the nearest non-wall cell (0 for non-wall cells), capped at 250.</summary>
    public readonly byte[] CellWallD;
    /// <summary>Chessboard distance in cells from any cell to the nearest pit cell (0 for pit cells), capped at 250.</summary>
    public readonly byte[] CellPitD;
    public bool AnyPit;

    public EnvField(NavGrid grid, byte[] deco, int apron)
    {
        G = grid; Deco = deco; A = apron;
        CW = grid.W + 2 * apron; CH = grid.H + 2 * apron;
        LW = CW * 2 + 1; LH = CH * 2 + 1;
        SW = (CW + Sector - 1) / Sector; SH = (CH + Sector - 1) / Sector;
        X0 = grid.Origin.X - apron * NavGrid.Cell;
        Z0 = grid.Origin.Y - apron * NavGrid.Cell;

        WallD = Distance(t => t == NavGrid.Wall);
        PitD = Distance(t => t == NavGrid.Pit);
        OpenD = Distance(t => t != NavGrid.Wall);
        CellWallD = CellDistance(t => t != NavGrid.Wall, true);
        CellPitD = CellDistance(t => t == NavGrid.Pit, false);
        for (int i = 0; i < grid.T.Length; i++) if (grid.T[i] == NavGrid.Pit) { AnyPit = true; break; }
    }

    public byte Cell(int cx, int cy) => G.Get(cx - A, cy - A);                    // Wall outside the grid
    public bool IsWall(int cx, int cy) => Cell(cx, cy) == NavGrid.Wall;
    public bool InCells(int cx, int cy) => cx >= 0 && cy >= 0 && cx < CW && cy < CH;
    public bool InGrid(int cx, int cy) => G.In(cx - A, cy - A);

    public byte DecoAt(int cx, int cy)
    {
        int gx = cx - A, gy = cy - A;
        if (!G.In(gx, gy) || Deco == null) return 0;
        int i = gy * G.W + gx;
        return i < Deco.Length ? Deco[i] : (byte)0;
    }

    public int WallDepth(int cx, int cy) => InCells(cx, cy) ? CellWallD[cy * CW + cx] : 250;
    public int PitDistance(int cx, int cy) => InCells(cx, cy) ? CellPitD[cy * CW + cx] : 250;

    public Vector3 CellCenter(int cx, int cy, float y = 0f) => new(X0 + (cx + 0.5f) * NavGrid.Cell, y, Z0 + (cy + 0.5f) * NavGrid.Cell);
    public float LatX(int i) => X0 + i;
    public float LatZ(int j) => Z0 + j;
    public int Lat(int i, int j) => j * LW + i;

    /// <summary>Extended cell under a world position (may be out of range).</summary>
    public Vector2I CellOf(float wx, float wz) => new(Mathf.FloorToInt((wx - X0) / NavGrid.Cell), Mathf.FloorToInt((wz - Z0) / NavGrid.Cell));

    public int SectorOfCell(int cx, int cy) =>
        Mathf.Clamp(cy / Sector, 0, SH - 1) * SW + Mathf.Clamp(cx / Sector, 0, SW - 1);

    public int SectorOf(Vector3 world)
    {
        var c = CellOf(world.X, world.Z);
        return SectorOfCell(Mathf.Clamp(c.X, 0, CW - 1), Mathf.Clamp(c.Y, 0, CH - 1));
    }

    /// <summary>Type of the cell that owns lattice sub-square (sx, sy) (1 m squares; two per cell and axis).</summary>
    private byte Sub(int sx, int sy) => sx < 0 || sy < 0 ? NavGrid.Wall : Cell(sx >> 1, sy >> 1);

    /// <summary>
    /// Chamfer distance transform on the lattice: 0 at vertices that touch at least one cell failing the test,
    /// otherwise the distance in metres to the nearest such vertex.
    /// </summary>
    private float[] Distance(Func<byte, bool> inside)
    {
        const float Far = 1e6f, D1 = 1f, D2 = 1.41421f;
        var d = new float[LW * LH];
        for (int j = 0; j < LH; j++)
            for (int i = 0; i < LW; i++)
            {
                bool all = inside(Sub(i - 1, j - 1)) && inside(Sub(i, j - 1)) && inside(Sub(i - 1, j)) && inside(Sub(i, j));
                d[j * LW + i] = all ? Far : 0f;
            }
        for (int j = 0; j < LH; j++)
            for (int i = 0; i < LW; i++)
            {
                int k = j * LW + i;
                float v = d[k];
                if (v == 0f) continue;
                if (i > 0) v = Mathf.Min(v, d[k - 1] + D1);
                if (j > 0)
                {
                    v = Mathf.Min(v, d[k - LW] + D1);
                    if (i > 0) v = Mathf.Min(v, d[k - LW - 1] + D2);
                    if (i < LW - 1) v = Mathf.Min(v, d[k - LW + 1] + D2);
                }
                d[k] = v;
            }
        for (int j = LH - 1; j >= 0; j--)
            for (int i = LW - 1; i >= 0; i--)
            {
                int k = j * LW + i;
                float v = d[k];
                if (v == 0f) continue;
                if (i < LW - 1) v = Mathf.Min(v, d[k + 1] + D1);
                if (j < LH - 1)
                {
                    v = Mathf.Min(v, d[k + LW] + D1);
                    if (i < LW - 1) v = Mathf.Min(v, d[k + LW + 1] + D2);
                    if (i > 0) v = Mathf.Min(v, d[k + LW - 1] + D2);
                }
                d[k] = v;
            }
        for (int k = 0; k < d.Length; k++) if (d[k] > 1000f) d[k] = 1000f;       // nothing of the other kind anywhere
        return d;
    }

    /// <summary>
    /// Chessboard distance per extended cell to the nearest cell passing <paramref name="target"/> (0 on targets).
    /// With <paramref name="onlyOthers"/> = true the result is meaningful for the non-target cells only, which is what
    /// "how deep inside the wall is this cell" needs.
    /// </summary>
    private byte[] CellDistance(Func<byte, bool> target, bool onlyOthers)
    {
        var d = new int[CW * CH];
        for (int y = 0; y < CH; y++)
            for (int x = 0; x < CW; x++)
                d[y * CW + x] = target(Cell(x, y)) ? 0 : 100000;
        for (int y = 0; y < CH; y++)
            for (int x = 0; x < CW; x++)
            {
                int k = y * CW + x, v = d[k];
                if (v == 0) continue;
                if (x > 0) v = Math.Min(v, d[k - 1] + 1);
                if (y > 0)
                {
                    v = Math.Min(v, d[k - CW] + 1);
                    if (x > 0) v = Math.Min(v, d[k - CW - 1] + 1);
                    if (x < CW - 1) v = Math.Min(v, d[k - CW + 1] + 1);
                }
                d[k] = v;
            }
        var r = new byte[CW * CH];
        for (int y = CH - 1; y >= 0; y--)
            for (int x = CW - 1; x >= 0; x--)
            {
                int k = y * CW + x, v = d[k];
                if (v != 0)
                {
                    if (x < CW - 1) v = Math.Min(v, d[k + 1] + 1);
                    if (y < CH - 1)
                    {
                        v = Math.Min(v, d[k + CW] + 1);
                        if (x < CW - 1) v = Math.Min(v, d[k + CW + 1] + 1);
                        if (x > 0) v = Math.Min(v, d[k + CW - 1] + 1);
                    }
                    d[k] = v;
                }
                r[k] = (byte)Math.Min(v, 250);
            }
        return r;
    }

    /// <summary>Bilinear sample of a lattice field at a world position.</summary>
    public float Sample(float[] field, float wx, float wz)
    {
        float fx = Mathf.Clamp(wx - X0, 0f, LW - 1.001f), fz = Mathf.Clamp(wz - Z0, 0f, LH - 1.001f);
        int i = (int)fx, j = (int)fz;
        float tx = fx - i, tz = fz - j;
        int k = j * LW + i;
        return Mathf.Lerp(Mathf.Lerp(field[k], field[k + 1], tx), Mathf.Lerp(field[k + LW], field[k + LW + 1], tx), tz);
    }

    /// <summary>Wall cells that are not connected to the outside of the map (free-standing blocks: houses, rock clumps).</summary>
    public List<WallIsland> Islands()
    {
        var mark = new int[CW * CH];                 // 0 unvisited, -1 outer wall, > 0 island id
        var stack = new Stack<int>();
        void Flood(int start, int id, WallIsland isl)
        {
            stack.Push(start);
            mark[start] = id;
            while (stack.Count > 0)
            {
                int k = stack.Pop();
                int x = k % CW, y = k / CW;
                if (isl != null)
                {
                    isl.Cells.Add(new Vector2I(x, y));
                    isl.MinX = Math.Min(isl.MinX, x); isl.MaxX = Math.Max(isl.MaxX, x);
                    isl.MinY = Math.Min(isl.MinY, y); isl.MaxY = Math.Max(isl.MaxY, y);
                }
                for (int n = 0; n < 4; n++)
                {
                    int nx = x + (n == 0 ? 1 : n == 1 ? -1 : 0), ny = y + (n == 2 ? 1 : n == 3 ? -1 : 0);
                    if (!InCells(nx, ny)) continue;
                    int nk = ny * CW + nx;
                    if (mark[nk] != 0 || !IsWall(nx, ny)) continue;
                    mark[nk] = id;
                    stack.Push(nk);
                }
            }
        }
        for (int x = 0; x < CW; x++)
        {
            if (mark[x] == 0 && IsWall(x, 0)) Flood(x, -1, null);
            int b = (CH - 1) * CW + x;
            if (mark[b] == 0 && IsWall(x, CH - 1)) Flood(b, -1, null);
        }
        for (int y = 0; y < CH; y++)
        {
            int l = y * CW, r = y * CW + CW - 1;
            if (mark[l] == 0 && IsWall(0, y)) Flood(l, -1, null);
            if (mark[r] == 0 && IsWall(CW - 1, y)) Flood(r, -1, null);
        }
        var list = new List<WallIsland>();
        for (int y = 0; y < CH; y++)
            for (int x = 0; x < CW; x++)
            {
                int k = y * CW + x;
                if (mark[k] != 0 || !IsWall(x, y)) continue;
                var isl = new WallIsland { Id = list.Count + 1, MinX = x, MaxX = x, MinY = y, MaxY = y };
                Flood(k, isl.Id, isl);
                list.Add(isl);
            }
        return list;
    }
}

internal sealed class WallIsland
{
    public int Id, MinX, MaxX, MinY, MaxY;
    public readonly List<Vector2I> Cells = new();
    public int W => MaxX - MinX + 1;
    public int H => MaxY - MinY + 1;
    /// <summary>How much of the bounding rectangle is filled (1 = a clean rectangle).</summary>
    public float Fill => Cells.Count / (float)(W * H);
}
