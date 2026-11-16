using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Terrain and pits. The ground is a 1 m lattice over the grid (plus apron), cut into 24 m chunks and drawn with
/// env_ground: per-vertex weights from LevelData.Deco pick between ground / alternate / path / room-or-arena sets.
/// Pits are dug into the same lattice and filled with lava, dark water or drifting void mist.
/// </summary>
internal static class EnvTerrain
{
    public static string PitKind(LevelTheme t) => t.PitKind is "lava" or "water" ? t.PitKind : "void";
    public static float PitDepth(LevelTheme t) => PitKind(t) switch { "lava" => 1.3f, "water" => 1.5f, _ => 9f };
    public static float PitLevel(LevelTheme t) => PitKind(t) switch { "lava" => -0.4f, "water" => -0.34f, _ => -1.5f };

    public static void Build(EnvBuild b)
    {
        var f = b.F;
        var t = b.Theme;
        int lw = f.LW, lh = f.LH, n = lw * lh;
        var hgt = new float[n];
        var wAlt = new float[n];
        var wPath = new float[n];
        var wSpec = new float[n];
        var wShore = new float[n];
        var occ = new float[n];
        var valid = new bool[n];
        bool outdoor = t.Outdoor;
        bool embank = outdoor && !b.SolidWalls;          // ground rises under tree lines and towards cliffs
        float depth = PitDepth(t);
        string kind = PitKind(t);
        float slope = kind == "lava" ? 1.8f : kind == "water" ? 2.4f : 2.2f;
        int seed = b.Seed;

        for (int j = 0; j < lh; j++)
            for (int i = 0; i < lw; i++)
            {
                int k = j * lw + i;
                int open = 0, alt = 0, path = 0, spec = 0, shore = 0;
                for (int dy = -1; dy <= 0; dy++)
                    for (int dx = -1; dx <= 0; dx++)
                    {
                        int sx = i + dx, sy = j + dy;
                        if (sx < 0 || sy < 0) continue;
                        int cx = sx >> 1, cy = sy >> 1;
                        if (f.Cell(cx, cy) != NavGrid.Floor) continue;
                        open++;
                        switch (f.DecoAt(cx, cy))
                        {
                            case 1: path++; break;
                            case 2: alt++; break;
                            case 3: case 4: spec++; break;
                            case 5: shore++; break;
                        }
                    }
                float pd = f.PitD[k], wd = f.WallD[k];
                if (open > 0)
                {
                    float inv = 1f / open;
                    wAlt[k] = alt * inv; wPath[k] = path * inv; wSpec[k] = spec * inv; wShore[k] = shore * inv;
                    valid[k] = true;
                }
                else if (pd > 0f) { wShore[k] = 1f; valid[k] = true; }
                else if (embank) wAlt[k] = 1f;               // leaf litter / scree under the border, untouched by the blur

                float x = f.LatX(i), z = f.LatZ(j);
                float y = 0f;
                if (pd > 0f)
                {
                    float d = pd;
                    if (pd >= 0.99f) d += (EnvNoise.Value(x * 0.31f, z * 0.31f, seed + 11) - 0.5f) * 0.7f;
                    y = -depth * Mathf.SmoothStep(0f, slope, Mathf.Max(d, 0f));
                }
                else if (embank && wd > 0.5f)
                {
                    float nz = EnvNoise.Fbm(x * 0.07f, z * 0.07f, seed + 23);
                    y = Mathf.Min(2.6f, 0.17f * Mathf.Pow(wd - 0.5f, 1.25f)) * (0.3f + 1.4f * nz);
                }
                hgt[k] = y;

                float o;
                if (wd > 0f) o = outdoor ? 0.2f : 0f;
                else
                {
                    o = Mathf.SmoothStep(0f, outdoor ? 3.2f : 2.4f, f.OpenD[k]);
                    if (outdoor) o = 0.45f + 0.55f * o;
                }
                occ[k] = o;
            }

        b.Heights = hgt;

        var tmp = new float[n];
        Blur(wAlt, valid, lw, lh, tmp);
        Blur(wPath, valid, lw, lh, tmp);
        Blur(wSpec, valid, lw, lh, tmp);
        Blur(wShore, valid, lw, lh, tmp);

        var matRoom = EnvMats.Ground(t, t.RoomFloor ?? t.Ground);
        var matArena = EnvMats.Ground(t, t.ArenaFloor ?? t.RoomFloor ?? t.Ground);
        bool skipDeep = b.SolidWalls;
        var idx = new List<int>(4096);
        const int S = EnvField.Sector;
        int chunks = 0;
        for (int sy = 0; sy < f.SH; sy++)
            for (int sx = 0; sx < f.SW; sx++)
            {
                int cx0 = sx * S, cy0 = sy * S, cx1 = Math.Min(cx0 + S, f.CW), cy1 = Math.Min(cy0 + S, f.CH);
                int i0 = cx0 * 2, j0 = cy0 * 2, i1 = cx1 * 2, j1 = cy1 * 2;
                int vw = i1 - i0 + 1, vh = j1 - j0 + 1;
                idx.Clear();
                for (int j = j0; j < j1; j++)
                    for (int i = i0; i < i1; i++)
                    {
                        int k = j * lw + i;
                        if (skipDeep)
                        {
                            float m = Mathf.Min(Mathf.Min(f.WallD[k], f.WallD[k + 1]), Mathf.Min(f.WallD[k + lw], f.WallD[k + lw + 1]));
                            if (m > 1.2f) continue;                  // buried under a capped wall block
                        }
                        int a = (j - j0) * vw + (i - i0), b1 = a + 1, c = a + vw, d = c + 1;
                        idx.Add(a); idx.Add(b1); idx.Add(d);
                        idx.Add(a); idx.Add(d); idx.Add(c);
                    }
                if (idx.Count == 0) continue;

                var verts = new Vector3[vw * vh];
                var norms = new Vector3[vw * vh];
                var cols = new Color[vw * vh];
                var uvs = new Vector2[vw * vh];
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        int k = j * lw + i, v = (j - j0) * vw + (i - i0);
                        verts[v] = new Vector3(f.LatX(i), hgt[k], f.LatZ(j));
                        float hl = hgt[i > 0 ? k - 1 : k], hr = hgt[i < lw - 1 ? k + 1 : k];
                        float hu = hgt[j > 0 ? k - lw : k], hd = hgt[j < lh - 1 ? k + lw : k];
                        norms[v] = new Vector3(hl - hr, 2f, hu - hd).Normalized();
                        cols[v] = new Color(wAlt[k], wPath[k], wSpec[k], wShore[k]);
                        uvs[v] = new Vector2(occ[k], 0f);
                    }

                int rooms = 0, arenas = 0;
                for (int cy = cy0; cy < cy1; cy++)
                    for (int cx = cx0; cx < cx1; cx++)
                    {
                        byte dc = f.DecoAt(cx, cy);
                        if (dc == 3) rooms++; else if (dc == 4) arenas++;
                    }

                var arrays = new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex] = verts;
                arrays[(int)Mesh.ArrayType.Normal] = norms;
                arrays[(int)Mesh.ArrayType.Color] = cols;
                arrays[(int)Mesh.ArrayType.TexUV] = uvs;
                arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
                var mesh = new ArrayMesh();
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                b.AddMesh(b.Terrain, $"Ground_{sx}_{sy}", mesh, arenas > rooms ? matArena : matRoom, false);
                b.Triangles += idx.Count / 3;
                chunks++;
            }
        Log.Debug("env", $"terrain: {chunks} chunks, lattice {lw}x{lh}");
    }

    /// <summary>3×3 box blur over the vertices that own real ground; the rest keep their value and do not bleed in.</summary>
    private static void Blur(float[] w, bool[] valid, int lw, int lh, float[] tmp)
    {
        for (int j = 0; j < lh; j++)
            for (int i = 0; i < lw; i++)
            {
                int k = j * lw + i;
                if (!valid[k]) { tmp[k] = w[k]; continue; }
                float sum = 0f;
                int cnt = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int jj = j + dy;
                    if (jj < 0 || jj >= lh) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int ii = i + dx;
                        if (ii < 0 || ii >= lw) continue;
                        int kk = jj * lw + ii;
                        if (!valid[kk]) continue;
                        sum += w[kk];
                        cnt++;
                    }
                }
                tmp[k] = cnt > 0 ? sum / cnt : w[k];
            }
        Array.Copy(tmp, w, w.Length);
    }

    // ───────────── pits ─────────────

    public static void BuildPits(EnvBuild b)
    {
        var f = b.F;
        var t = b.Theme;
        if (!f.AnyPit) return;
        string kind = PitKind(t);
        if (t.PitKind != kind) Log.Debug("env", $"theme '{t.Id}' has pits but PitKind='{t.PitKind}'; rendering them as void");

        // one flat sheet per sector over every pit cell and its neighbours (the terrain rim hides the overlap)
        var sheets = new Dictionary<int, EnvMesh>();
        int lw = f.LW;
        for (int cy = 0; cy < f.CH; cy++)
            for (int cx = 0; cx < f.CW; cx++)
            {
                if (f.PitDistance(cx, cy) > 1) continue;
                int sector = f.SectorOfCell(cx, cy);
                if (!sheets.TryGetValue(sector, out var m)) { m = new EnvMesh(false); sheets[sector] = m; }
                for (int q = 0; q < 4; q++)
                {
                    int i = cx * 2 + (q & 1), j = cy * 2 + (q >> 1);
                    int v0 = Corner(m, f, i, j), v1 = Corner(m, f, i + 1, j), v2 = Corner(m, f, i + 1, j + 1), v3 = Corner(m, f, i, j + 1);
                    m.Tri(v0, v1, v2);
                    m.Tri(v0, v2, v3);
                }
            }
        if (sheets.Count == 0) return;

        float level = PitLevel(t);
        Material surface = null, mistLow = null, mistHigh = null;
        switch (kind)
        {
            case "lava": surface = LavaMaterial(t); break;
            case "water": surface = WaterMaterial(t); break;
            default:
                // a chasm is dark: two thin veils of the pit colour deep down, lit from below only where the theme says it glows
                mistLow = VoidMaterial(t, 0.85f, 0.02f + t.PitGlow * 0.1f, 3.1f, 0);
                mistHigh = VoidMaterial(t, 0.45f, 0.012f + t.PitGlow * 0.05f, 7.7f, 1);
                break;
        }
        foreach (var (sector, m) in sheets)
        {
            var mesh = m.Build();
            if (mesh == null) continue;
            b.Triangles += m.TriangleCount;
            if (surface != null)
            {
                var mi = b.AddMesh(b.Pits, kind + "_" + sector, mesh, null, false);
                mi.MaterialOverride = surface;
                mi.Position = new Vector3(0f, level, 0f);
            }
            else
            {
                var lo = b.AddMesh(b.Pits, "mist_lo_" + sector, mesh, null, false);
                var hi = b.AddMesh(b.Pits, "mist_hi_" + sector, mesh, null, false);
                if (mistLow != null) { lo.MaterialOverride = mistLow; hi.MaterialOverride = mistHigh; }
                lo.Position = new Vector3(0f, level - 4f, 0f);
                hi.Position = new Vector3(0f, level - 1.2f, 0f);
            }
        }

        // glow thrown on the banks: lava lights its surroundings, void and toxic water carry the region accent
        float energy = kind == "lava" ? 1.7f * Mathf.Max(t.PitGlow, 0.5f) : 0.9f * t.PitGlow;
        if (energy < 0.15f) return;
        var color = kind == "water" ? t.Accent : t.PitColor;
        // One light per block of shore cells. A lava field can have a very long shore, so the block size grows until
        // the whole map needs no more than about a hundred of them; the light manager then keeps only the nearest
        // dozen switched on, so the cost on screen is bounded whatever the map looks like.
        float range = kind == "lava" ? 10.5f : 8f;
        const int MaxGlowLights = 110;
        var spots = new List<Vector3>();
        int step = kind == "lava" ? 4 : 5;
        for (; step <= 12; step++)
        {
            spots.Clear();
            for (int by = 0; by < f.CH; by += step)
                for (int bx = 0; bx < f.CW; bx += step)
                {
                    bool placed = false;
                    for (int cy = by; cy < Math.Min(by + step, f.CH) && !placed; cy++)
                        for (int cx = bx; cx < Math.Min(bx + step, f.CW) && !placed; cx++)
                        {
                            if (f.Cell(cx, cy) != NavGrid.Pit || !FloorNear(f, cx, cy, 2)) continue;
                            spots.Add(f.CellCenter(cx, cy, kind == "lava" ? 0.9f : 0.5f));
                            placed = true;
                        }
                }
            if (spots.Count <= MaxGlowLights) break;
        }
        float spread = step / (kind == "lava" ? 4f : 5f);          // fewer lights: each one carries more
        foreach (var at in spots) b.AddLight(at, color, energy * Mathf.Sqrt(spread), range * Mathf.Min(spread, 1.5f), 0.25f, false);
        Log.Debug("env", $"pits: kind={kind} sheets={sheets.Count} glowLights={spots.Count} (one per {step}x{step} cells)");
    }

    private static int Corner(EnvMesh m, EnvField f, int i, int j)
    {
        float pd = i < f.LW && j < f.LH ? f.PitD[j * f.LW + i] : 0f;
        float bank = 1f - Mathf.SmoothStep(0f, 2.6f, pd);
        return m.Raw(new Vector3(f.LatX(i), 0f, f.LatZ(j)), Vector3.Up, new Color(bank, 0f, 0f, 1f));
    }

    private static bool FloorNear(EnvField f, int cx, int cy, int r)
    {
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (f.Cell(cx + dx, cy + dy) == NavGrid.Floor) return true;
        return false;
    }

    private static Material LavaMaterial(LevelTheme t)
    {
        var shader = EnvAssets.LoadShader("env_lava");
        if (shader == null) return null;
        var m = new ShaderMaterial { Shader = shader, ResourceName = "lava" };
        var em = EnvAssets.Map("lava_molten", "emission");
        m.SetShaderParameter("molten_alb", EnvAssets.Albedo("lava_molten"));
        if (em != null) m.SetShaderParameter("molten_em", em);
        var crustAlb = EnvAssets.Map("lava_crust", "albedo");
        if (crustAlb != null) m.SetShaderParameter("crust_alb", crustAlb);
        var crustEm = EnvAssets.Map("lava_crust", "emission");
        if (crustEm != null) m.SetShaderParameter("crust_em", crustEm);
        m.SetShaderParameter("crust_nrm", EnvAssets.Normal("lava_crust"));
        m.SetShaderParameter("noise_tex", EnvAssets.Noise);
        m.SetShaderParameter("uv_scale", 1f / Mathf.Max(EnvAssets.Info("lava_molten").SizeM, 0.05f));
        m.SetShaderParameter("glow_color", t.PitColor);
        m.SetShaderParameter("energy", 3.4f);
        m.SetShaderParameter("procedural", em == null ? 1f : 0.25f);
        return m;
    }

    private static Material WaterMaterial(LevelTheme t)
    {
        var shader = EnvAssets.LoadShader("env_water");
        if (shader == null) return null;
        var m = new ShaderMaterial { Shader = shader, ResourceName = "water" };
        var deep = t.PitColor;
        m.SetShaderParameter("wave_nrm", EnvAssets.WaveNormal);
        m.SetShaderParameter("noise_tex", EnvAssets.Noise);
        m.SetShaderParameter("deep_color", deep);
        m.SetShaderParameter("shallow_color", new Color(deep.R * 2.6f + 0.02f, deep.G * 2.6f + 0.022f, deep.B * 2.6f + 0.018f));
        m.SetShaderParameter("flow", new Vector2(0.004f + Mathf.Abs(t.Wind.X) * 0.004f, 0.003f + Mathf.Abs(t.Wind.Y) * 0.004f));
        m.SetShaderParameter("glow", t.Accent);
        m.SetShaderParameter("glow_energy", t.PitGlow * 0.14f);
        // dark, heavy water: the back light may lay one broken streak on it, not a field of sparkles
        m.SetShaderParameter("roughness", 0.24f);
        m.SetShaderParameter("wave_strength", 0.3f);
        return m;
    }

    private static Material VoidMaterial(LevelTheme t, float density, float brightness, float seed, int priority)
    {
        var shader = EnvAssets.LoadShader("env_void");
        if (shader == null) return null;
        var m = new ShaderMaterial { Shader = shader, ResourceName = "void_mist", RenderPriority = priority };
        m.SetShaderParameter("noise_tex", EnvAssets.Noise);
        m.SetShaderParameter("color", t.PitColor);
        m.SetShaderParameter("density", density);
        m.SetShaderParameter("brightness", brightness);
        m.SetShaderParameter("seed", seed);
        m.SetShaderParameter("speed", 0.6f + t.Wind.Length() * 0.2f);
        return m;
    }
}
