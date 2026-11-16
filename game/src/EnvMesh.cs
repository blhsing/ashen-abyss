using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Growable triangle soup used by the level renderer for everything it models in code (walls, caps, cliffs, pit
/// surfaces and procedural set dressing). Geometry is described in a local frame and pushed through <see cref="Xf"/>,
/// so a whole map sector of same-material pieces ends up in one world-space mesh and one draw call.
/// With <c>uv = true</c> vertices carry metre-scaled box-projected UVs and tangents (architecture);
/// without, the material is expected to map by world position (triplanar rock) and normals can be welded smooth.
/// Winding is fixed automatically from the supplied normals (Godot front faces are clockwise).
/// </summary>
internal sealed class EnvMesh
{
    public readonly List<Vector3> V = new(), N = new();
    public readonly List<Vector2> UV = new();
    public readonly List<Color> C = new();
    public readonly List<float> T = new();
    public readonly List<int> I = new();
    public readonly bool HasUv;
    /// <summary>Local → world transform applied to everything emitted (rotation + uniform scale + translation).</summary>
    public Transform3D Xf = Transform3D.Identity;
    /// <summary>Added to generated UVs so copies of the same piece do not show the same texels.</summary>
    public Vector2 UvOff;
    /// <summary>Weld normals of coincident vertices when the mesh is built (organic rock).</summary>
    public bool Smooth;

    public EnvMesh(bool uv) { HasUv = uv; }

    public bool Empty => I.Count == 0;
    public int TriangleCount => I.Count / 3;

    private float XfScale => Xf.Basis.X.Length();

    /// <summary>Adds one vertex given in the local frame. Tangent is ignored for meshes without UVs.</summary>
    public int Vert(Vector3 p, Vector3 n, Vector2 uv, Vector3 tangent, Color col)
    {
        V.Add(Xf * p);
        N.Add((Xf.Basis * n).Normalized());
        C.Add(col);
        if (HasUv)
        {
            UV.Add(uv * XfScale + UvOff);
            var t = (Xf.Basis * tangent).Normalized();
            T.Add(t.X); T.Add(t.Y); T.Add(t.Z); T.Add(1f);
        }
        return V.Count - 1;
    }

    /// <summary>Adds a vertex that is already in world space (terrain-like meshes, no UVs).</summary>
    public int Raw(Vector3 worldPos, Vector3 normal, Color col)
    {
        V.Add(worldPos);
        N.Add(normal);
        C.Add(col);
        if (HasUv) { UV.Add(new Vector2(worldPos.X, worldPos.Z)); T.Add(1f); T.Add(0f); T.Add(0f); T.Add(1f); }
        return V.Count - 1;
    }

    /// <summary>Adds a triangle between existing vertices, flipping it if needed so its front faces along the vertex normals.</summary>
    public void Tri(int a, int b, int c)
    {
        var g = (V[b] - V[a]).Cross(V[c] - V[a]);
        if (g.Dot(N[a] + N[b] + N[c]) > 0f) { I.Add(a); I.Add(c); I.Add(b); }
        else { I.Add(a); I.Add(b); I.Add(c); }
    }

    /// <summary>Adds a triangle whose front must face <paramref name="worldDir"/> (for smooth meshes whose normals are recomputed later).</summary>
    public void TriFacing(int a, int b, int c, Vector3 worldDir)
    {
        var g = (V[b] - V[a]).Cross(V[c] - V[a]);
        if (g.Dot(worldDir) > 0f) { I.Add(a); I.Add(c); I.Add(b); }
        else { I.Add(a); I.Add(b); I.Add(c); }
    }

    /// <summary>Tangent frame of the box projection: u runs horizontally along the face, v runs down it (or along +Z on flat tops).</summary>
    private static void Frame(Vector3 n, out Vector3 t, out Vector3 bt)
    {
        t = Mathf.Abs(n.Y) > 0.999f ? Vector3.Right : Vector3.Up.Cross(n).Normalized();
        bt = n.Cross(t);
    }

    private static Vector2 Project(Vector3 p, Vector3 t, Vector3 bt) => new(p.Dot(t), -p.Dot(bt));

    /// <summary>Quad a-b-c-d (ring order, local frame) with face normal n and one colour per corner.</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color ca, Color cb, Color cc, Color cd)
    {
        Frame(n, out var t, out var bt);
        int i0 = Vert(a, n, Project(a, t, bt), t, ca);
        int i1 = Vert(b, n, Project(b, t, bt), t, cb);
        int i2 = Vert(c, n, Project(c, t, bt), t, cc);
        int i3 = Vert(d, n, Project(d, t, bt), t, cd);
        Tri(i0, i1, i2);
        Tri(i0, i2, i3);
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color col) => Quad(a, b, c, d, n, col, col, col, col);

    public void Tri3(Vector3 a, Vector3 b, Vector3 c, Vector3 n, Color col)
    {
        Frame(n, out var t, out var bt);
        int i0 = Vert(a, n, Project(a, t, bt), t, col);
        int i1 = Vert(b, n, Project(b, t, bt), t, col);
        int i2 = Vert(c, n, Project(c, t, bt), t, col);
        Tri(i0, i1, i2);
    }

    /// <summary>Axis-aligned box in the local frame. <paramref name="foot"/> darkens the bottom edge of the sides (contact shadow).</summary>
    public void Box(Vector3 min, Vector3 max, Color col, float foot = 1f, bool top = true, bool bottom = false)
    {
        var lo = new Color(col.R * foot, col.G * foot, col.B * foot, col.A);
        float x0 = min.X, y0 = min.Y, z0 = min.Z, x1 = max.X, y1 = max.Y, z1 = max.Z;
        // +X, -X, +Z, -Z (corner order: bottom, bottom, top, top)
        Quad(new(x1, y0, z0), new(x1, y0, z1), new(x1, y1, z1), new(x1, y1, z0), Vector3.Right, lo, lo, col, col);
        Quad(new(x0, y0, z1), new(x0, y0, z0), new(x0, y1, z0), new(x0, y1, z1), Vector3.Left, lo, lo, col, col);
        Quad(new(x1, y0, z1), new(x0, y0, z1), new(x0, y1, z1), new(x1, y1, z1), Vector3.Back, lo, lo, col, col);
        Quad(new(x0, y0, z0), new(x1, y0, z0), new(x1, y1, z0), new(x0, y1, z0), Vector3.Forward, lo, lo, col, col);
        if (top) Quad(new(x0, y1, z0), new(x1, y1, z0), new(x1, y1, z1), new(x0, y1, z1), Vector3.Up, col);
        if (bottom) Quad(new(x0, y0, z0), new(x0, y0, z1), new(x1, y0, z1), new(x1, y0, z0), Vector3.Down, lo);
    }

    /// <summary>Box given by the centre of its base and its size.</summary>
    public void BoxAt(Vector3 baseCenter, Vector3 size, Color col, float foot = 1f, bool top = true, bool bottom = false) =>
        Box(new Vector3(baseCenter.X - size.X * 0.5f, baseCenter.Y, baseCenter.Z - size.Z * 0.5f),
            new Vector3(baseCenter.X + size.X * 0.5f, baseCenter.Y + size.Y, baseCenter.Z + size.Z * 0.5f), col, foot, top, bottom);

    /// <summary>Box under an extra local transform (leaning beams, tumbled blocks).</summary>
    public void BoxT(Transform3D local, Vector3 size, Color col, float foot = 1f, bool bottom = false)
    {
        var saved = Xf;
        Xf = saved * local;
        Box(new Vector3(-size.X * 0.5f, 0f, -size.Z * 0.5f), new Vector3(size.X * 0.5f, size.Y, size.Z * 0.5f), col, foot, true, bottom);
        Xf = saved;
    }

    /// <summary>Upright frustum (cylinder, tapered shaft, cone when r1 is tiny) with smooth sides and an optional flat top.</summary>
    public void Cyl(Vector3 baseCenter, float r0, float r1, float h, int sides, Color col, bool cap = true, float foot = 1f, bool capBottom = false)
    {
        if (capBottom && r0 > 0.03f)
        {
            var dn = new Color(col.R * foot, col.G * foot, col.B * foot, col.A);
            int cb = Vert(baseCenter, Vector3.Down, new Vector2(baseCenter.X, baseCenter.Z), Vector3.Right, dn);
            int rb = V.Count;
            for (int i = 0; i <= sides; i++)
            {
                float ang = i / (float)sides * Mathf.Tau;
                var p = baseCenter + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r0;
                Vert(p, Vector3.Down, new Vector2(p.X, p.Z), Vector3.Right, dn);
            }
            for (int i = 0; i < sides; i++) Tri(cb, rb + i, rb + i + 1);
        }
        var lo = new Color(col.R * foot, col.G * foot, col.B * foot, col.A);
        float rAvg = (r0 + r1) * 0.5f;
        int first = V.Count;
        for (int i = 0; i <= sides; i++)
        {
            float ang = i / (float)sides * Mathf.Tau;
            float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            var dir = new Vector3(cs, 0f, sn);
            var n = (dir * h + Vector3.Up * (r0 - r1)).Normalized();
            var tan = new Vector3(sn, 0f, -cs);
            float u = -ang * rAvg;
            Vert(baseCenter + dir * r0, n, new Vector2(u, -baseCenter.Y), tan, lo);
            Vert(baseCenter + dir * r1 + Vector3.Up * h, n, new Vector2(u, -(baseCenter.Y + h)), tan, col);
        }
        for (int i = 0; i < sides; i++)
        {
            int b0 = first + i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
            Tri(b0, b1, t1);
            Tri(b0, t1, t0);
        }
        if (cap && r1 > 0.03f)
        {
            var top = baseCenter + Vector3.Up * h;
            int c = Vert(top, Vector3.Up, new Vector2(top.X, top.Z), Vector3.Right, col);
            int ring = V.Count;
            for (int i = 0; i <= sides; i++)
            {
                float ang = i / (float)sides * Mathf.Tau;
                var p = top + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r1;
                Vert(p, Vector3.Up, new Vector2(p.X, p.Z), Vector3.Right, col);
            }
            for (int i = 0; i < sides; i++) Tri(c, ring + i, ring + i + 1);
        }
    }

    /// <summary>Flat-faced frustum (obelisks, crystals, stakes, octagonal bases): one quad per side, optional flat top.</summary>
    public void Prism(Vector3 baseCenter, float r0, float r1, float h, int sides, Color col, bool cap = true, float foot = 1f, float twist = 0f)
    {
        var lo = new Color(col.R * foot, col.G * foot, col.B * foot, col.A);
        var top = baseCenter + Vector3.Up * h;
        for (int i = 0; i < sides; i++)
        {
            float a0 = twist + i / (float)sides * Mathf.Tau, a1 = twist + (i + 1) / (float)sides * Mathf.Tau;
            var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 b0 = baseCenter + d0 * r0, b1 = baseCenter + d1 * r0, t0 = top + d0 * r1, t1 = top + d1 * r1;
            var mid = (d0 + d1).Normalized();
            var n = (mid * h + Vector3.Up * (r0 - r1)).Normalized();
            Quad(b0, b1, t1, t0, n, lo, lo, col, col);
        }
        if (cap && r1 > 0.03f)
        {
            for (int i = 1; i < sides - 1; i++)
            {
                float a0 = twist, a1 = twist + i / (float)sides * Mathf.Tau, a2 = twist + (i + 1) / (float)sides * Mathf.Tau;
                Tri3(top + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r1,
                     top + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r1,
                     top + new Vector3(Mathf.Cos(a2), 0f, Mathf.Sin(a2)) * r1, Vector3.Up, col);
            }
        }
    }

    /// <summary>
    /// Gable roof over a rectangle centred on the local origin, ridge along local Z. Only the two slopes are built
    /// (the camera never sees the underside); the gable triangles belong to the wall material.
    /// </summary>
    public void Gable(float halfW, float halfD, float eaveY, float ridgeY, float overhang, Color col)
    {
        float rise = ridgeY - eaveY;
        float slope = rise / Mathf.Max(halfW, 0.01f);
        float ex = halfW + overhang, ey = eaveY - overhang * slope, ez = halfD + overhang;
        var nl = new Vector3(-rise, halfW, 0f).Normalized();
        var nr = new Vector3(rise, halfW, 0f).Normalized();
        var dark = new Color(col.R * 0.8f, col.G * 0.8f, col.B * 0.8f, col.A);
        Quad(new(-ex, ey, ez), new(-ex, ey, -ez), new(0f, ridgeY, -ez), new(0f, ridgeY, ez), nl, dark, dark, col, col);
        Quad(new(ex, ey, -ez), new(ex, ey, ez), new(0f, ridgeY, ez), new(0f, ridgeY, -ez), nr, dark, dark, col, col);
    }

    /// <summary>Recomputes normals so vertices sharing a position share an area-weighted normal (rounded rock).</summary>
    private void WeldNormals()
    {
        var acc = new Dictionary<long, Vector3>(V.Count);
        static long Key(Vector3 p)
        {
            long x = Mathf.RoundToInt(p.X * 50f) & 0x1FFFFF, y = Mathf.RoundToInt(p.Y * 50f) & 0x1FFFFF, z = Mathf.RoundToInt(p.Z * 50f) & 0x1FFFFF;
            return (x << 42) | (y << 21) | z;
        }
        for (int i = 0; i + 2 < I.Count; i += 3)
        {
            Vector3 a = V[I[i]], b = V[I[i + 1]], c = V[I[i + 2]];
            var fn = (c - a).Cross(b - a);               // clockwise front → this points out of the face, length = 2 × area
            for (int k = 0; k < 3; k++)
            {
                long key = Key(V[I[i + k]]);
                acc.TryGetValue(key, out var sum);
                acc[key] = sum + fn;
            }
        }
        for (int i = 0; i < V.Count; i++)
        {
            if (acc.TryGetValue(Key(V[i]), out var sum) && sum.LengthSquared() > 1e-10f) N[i] = sum.Normalized();
        }
    }

    /// <summary>Uploads the mesh. Returns null when nothing was emitted.</summary>
    public ArrayMesh Build()
    {
        if (I.Count == 0) return null;
        if (Smooth) WeldNormals();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = V.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = N.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = C.ToArray();
        if (HasUv)
        {
            arrays[(int)Mesh.ArrayType.TexUV] = UV.ToArray();
            arrays[(int)Mesh.ArrayType.Tangent] = T.ToArray();
        }
        arrays[(int)Mesh.ArrayType.Index] = I.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}

/// <summary>Small deterministic generator (xorshift) so level dressing is identical for the same seed without allocating a Random per prop.</summary>
internal struct EnvRng
{
    private uint _s;

    public EnvRng(int seed)
    {
        _s = (uint)seed * 747796405u + 2891336453u;
        if (_s == 0) _s = 0x9E3779B9u;
        Next(); Next();
    }

    public static EnvRng For(int seed, int a, int b = 0) => new(unchecked(seed * 73856093 ^ a * 19349663 ^ b * 83492791));

    public uint Next()
    {
        _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
        return _s;
    }

    /// <summary>Uniform in [0, 1).</summary>
    public float F() => (Next() >> 8) * (1f / 16777216f);
    public float Range(float a, float b) => a + (b - a) * F();
    public int Int(int n) => n <= 0 ? 0 : (int)(Next() % (uint)n);
    public bool Chance(float p) => F() < p;
    public float Angle() => F() * Mathf.Tau;
    public T Pick<T>(T[] list) => list[Int(list.Length)];
}

/// <summary>Hash-based value noise in world space (deterministic, no engine objects) for heights and placement masks.</summary>
internal static class EnvNoise
{
    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) * (1f / 16777215f);
        }
    }

    /// <summary>Smooth value noise in [0, 1].</summary>
    public static float Value(float x, float y, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// <summary>Three octaves of value noise in [0, 1].</summary>
    public static float Fbm(float x, float y, int seed) =>
        Value(x, y, seed) * 0.57f + Value(x * 2.07f + 13.1f, y * 2.07f + 7.7f, seed + 17) * 0.29f + Value(x * 4.3f + 3.3f, y * 4.3f + 29.9f, seed + 31) * 0.14f;

    /// <summary>Cheap 3D-ish noise in [0, 1] (two rotated 2D lookups) used to displace cave and cliff walls.</summary>
    public static float Value3(Vector3 p, int seed) =>
        Value(p.X + p.Y * 0.61f, p.Z - p.Y * 0.47f, seed) * 0.6f + Value(p.X * 2.3f - p.Y * 0.9f + 5.2f, p.Z * 2.3f + p.Y * 1.1f + 1.3f, seed + 7) * 0.4f;
}
