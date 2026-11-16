using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// Procedural set dressing for everything the prop library has no model for: graves, ruins, columns, crystals,
/// town houses, stalls, palisades, torches and braziers. Each piece is emitted into the per-sector batch of its
/// texture set (see <see cref="EnvBuild.Batch(string, WallFlags, Color, Vector3)"/>), so a whole graveyard costs a
/// handful of draw calls. Local frame of a piece: +Y up, +Z the way it faces, metres.
/// </summary>
internal static class EnvDress
{
    private static readonly Color W = new(1f, 1f, 1f, 1f);
    private static Color G(float v) => new(v, v, v, 1f);
    private static Vector3 P(float x, float y, float z) => new(x, y, z);
    private static readonly Color Ember = new(1f, 0.42f, 0.1f);

    /// <summary>Fetches the batch for a texture set around the piece and aims it at the piece's frame.</summary>
    private static EnvMesh M(EnvBuild b, string set, Transform3D xf, ref EnvRng r, WallFlags flags = WallFlags.None, Color? tint = null)
    {
        var m = b.Batch(set, flags, tint ?? Colors.White, xf.Origin);
        m.Xf = xf;
        m.UvOff = new Vector2(r.Range(0f, 3f), r.Range(0f, 3f));
        return m;
    }

    private static Basis Tumble(ref EnvRng r, float tilt = 0.5f) =>
        new Basis(Vector3.Up, r.Angle()) * new Basis(Vector3.Right, r.Range(-tilt, tilt)) * new Basis(Vector3.Back, r.Range(-tilt, tilt));

    private static Transform3D At(Transform3D xf, float x, float y, float z, float yaw = 0f) =>
        xf * new Transform3D(new Basis(Vector3.Up, yaw), new Vector3(x, y, z));

    private static float YawOf(Vector3 worldDir) => Mathf.Atan2(worldDir.X, worldDir.Z);

    /// <summary>Builds the dressing kind named by a theme list entry ('@' prefix) at a prop slot.</summary>
    public static void Make(EnvBuild b, string kind, Vector3 pos, float yaw, float scale, float radius, ref EnvRng r)
    {
        var xf = EnvProps.Xf(pos, yaw, scale);
        var t = b.Theme;
        switch (kind)
        {
            case "@headstone": Headstone(b, xf, ref r); break;
            case "@grave_slab": GraveSlab(b, xf, ref r); break;
            case "@grave_cluster": GraveCluster(b, xf, radius, ref r); break;
            case "@sarcophagus": Sarcophagus(b, xf, ref r); break;
            case "@obelisk": Obelisk(b, xf, ref r); break;
            case "@ruin_pillar": RuinPillar(b, xf, ref r); break;
            case "@pillar_square": PillarSquare(b, xf, ref r); break;
            case "@pillar_round": PillarRound(b, xf, ref r); break;
            case "@rubble": Rubble(b, xf, false, ref r); break;
            case "@rubble_big": Rubble(b, xf, true, ref r); break;
            case "@ruin_wall": RuinWall(b, xf, radius, ref r); break;
            case "@arch": Arch(b, xf, ref r); break;
            case "@colonnade": Colonnade(b, xf, ref r); break;
            case "@sand_column": Column(b, xf, r.Range(3.2f, 4.6f), r.Chance(0.4f), ref r); break;
            case "@cath_column": CathColumn(b, xf, ref r); break;
            case "@brass_column": BrassColumn(b, xf, ref r); break;
            case "@ice_crystal": Crystal(b, xf, "ice_pale", WallFlags.Gloss | WallFlags.Lit, IceTint(t), true, ref r); break;
            case "@ice_shard": Crystal(b, xf, "ice_pale", WallFlags.Gloss | WallFlags.Lit, IceTint(t), false, ref r); break;
            case "@ice_cluster": CrystalCluster(b, xf, "ice_pale", WallFlags.Gloss | WallFlags.Lit, IceTint(t), ref r); break;
            case "@obsidian_pillar": Crystal(b, xf, "obsidian", ObsidianFlags(t), ObsidianTint(t), true, ref r); break;
            case "@obsidian_cluster": CrystalCluster(b, xf, "obsidian", ObsidianFlags(t), ObsidianTint(t), ref r); break;
            case "@stalagmite": Stalagmite(b, xf, false, ref r); break;
            case "@stalagmite_cluster": Stalagmite(b, xf, true, ref r); break;
            case "@house":
            {
                float hx = Mathf.Clamp(radius > 1f ? radius * 0.82f : r.Range(2.6f, 3.4f), 2.2f, 4.6f);
                float hz = Mathf.Clamp(radius > 1f ? radius * 0.66f : r.Range(2.1f, 2.7f), 1.9f, 3.8f);
                House(b, EnvProps.Xf(pos, yaw, 1f), hx, hz, Vector3.Back, ref r);
                break;
            }
            case "@stall": Stall(b, xf, ref r); break;
            case "@well": Well(b, xf, ref r); break;
            case "@tent": Tent(b, xf, ref r); break;
            case "@palisade":
            {
                float half = Mathf.Clamp(radius > 0.6f ? radius : r.Range(1.8f, 3f), 1.4f, 4f);
                var dir = xf.Basis.X.Normalized();
                Palisade(b, pos - dir * half, pos + dir * half, ref r);
                break;
            }
            case "@post": Post(b, xf, ref r); break;
            case "@banner": Banner(b, xf, ref r); break;
            case "@altar": Altar(b, xf, ref r); break;
            case "@anvil": Anvil(b, xf, ref r); break;
            case "@torch": Torch(b, xf, ref r); break;
            case "@brazier": Brazier(b, xf, false, ref r); break;
            case "@brazier_brass": Brazier(b, xf, true, ref r); break;
            case "@glowshroom": Glowshroom(b, xf, ref r); break;
            default:
                if (EnvAssets.LogOnce("dress:" + kind)) Log.Warn("env", $"unknown set-dressing kind '{kind}', building rubble instead");
                Rubble(b, xf, false, ref r);
                break;
        }
    }

    private static Color IceTint(LevelTheme t) => new(Mathf.Min(0.55f + t.Accent.R * 0.3f, 1f), Mathf.Min(0.7f + t.Accent.G * 0.25f, 1f), Mathf.Min(0.8f + t.Accent.B * 0.2f, 1f));
    private static WallFlags ObsidianFlags(LevelTheme t) => t.Id == "rift" ? WallFlags.Gloss | WallFlags.Lit : WallFlags.Gloss;
    private static Color ObsidianTint(LevelTheme t) => t.Id == "rift" ? new Color(0.7f + t.Accent.R * 0.3f, 0.6f + t.Accent.G * 0.3f, 1f) : Colors.White;

    // ───────────── graves ─────────────

    public static void Headstone(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var lean = new Transform3D(new Basis(Vector3.Right, r.Range(-0.14f, 0.14f)) * new Basis(Vector3.Back, r.Range(-0.08f, 0.08f)), Vector3.Zero);
        var m = M(b, b.Theme.DressStone, xf * lean, ref r);
        float w = r.Range(0.55f, 0.75f), h = r.Range(0.8f, 1.25f), d = 0.17f;
        switch (r.Int(4))
        {
            case 0:     // stepped head
                m.BoxAt(P(0f, -0.1f, 0f), P(w, h + 0.1f, d), W, 0.6f);
                m.BoxAt(P(0f, h, 0f), P(w * 0.62f, 0.16f, d), W);
                break;
            case 1:     // cross on a block
                m.BoxAt(P(0f, -0.1f, 0f), P(0.56f, 0.24f, 0.36f), W, 0.6f);
                m.BoxAt(P(0f, 0.14f, 0f), P(0.2f, h + 0.2f, 0.16f), W);
                m.BoxAt(P(0f, h * 0.72f, 0f), P(0.7f, 0.19f, 0.17f), W);
                break;
            case 2:     // snapped: a stump and the fallen half
                m.BoxAt(P(0f, -0.1f, 0f), P(w, h * 0.45f, d), W, 0.6f);
                m.BoxT(new Transform3D(new Basis(Vector3.Up, r.Range(-0.5f, 0.5f)) * new Basis(Vector3.Right, 1.45f), P(r.Range(-0.2f, 0.2f), 0.04f, 0.5f)),
                    P(w, h * 0.5f, d), G(0.9f), 1f, true);
                break;
            default:    // plain slab on a footing
                m.BoxAt(P(0f, -0.1f, 0f), P(w + 0.16f, 0.22f, d + 0.16f), W, 0.6f);
                m.BoxAt(P(0f, 0.12f, 0f), P(w, h, d), W);
                break;
        }
    }

    public static void GraveSlab(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var m = M(b, b.Theme.DressStone, xf, ref r);
        m.BoxAt(P(0f, -0.08f, 0f), P(1.05f, 0.26f, 2.15f), G(0.9f), 0.6f);
        m.BoxT(new Transform3D(new Basis(Vector3.Up, r.Range(-0.06f, 0.06f)), P(r.Range(-0.04f, 0.04f), 0.18f, 0f)), P(0.88f, 0.14f, 1.95f), W);
        if (r.Chance(0.6f)) Headstone(b, At(xf, 0f, 0f, -1.25f), ref r);
    }

    public static void GraveCluster(EnvBuild b, Transform3D xf, float radius, ref EnvRng r)
    {
        float ext = Mathf.Clamp(radius > 0.5f ? radius : 3.2f, 2.2f, 6f);
        int cols = Mathf.Clamp((int)(ext * 2f / 1.7f), 2, 5), rows = Mathf.Clamp((int)(ext * 2f / 3f), 1, 3);
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
            {
                if (r.Chance(0.18f)) continue;
                float x = (col - (cols - 1) * 0.5f) * 1.7f + r.Range(-0.15f, 0.15f);
                float z = (row - (rows - 1) * 0.5f) * 3f + r.Range(-0.2f, 0.2f);
                var g = At(xf, x, 0f, z, r.Range(-0.12f, 0.12f));
                if (r.Chance(0.55f)) GraveSlab(b, g, ref r);
                else Headstone(b, At(g, 0f, 0f, -0.9f), ref r);
            }
    }

    public static void Sarcophagus(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var m = M(b, b.Theme.DressStone, xf, ref r);
        m.BoxAt(P(0f, -0.05f, 0f), P(1.45f, 0.27f, 2.65f), G(0.9f), 0.6f);
        m.BoxAt(P(0f, 0.22f, 0f), P(1.1f, 0.66f, 2.25f), W, 0.8f);
        var lid = M(b, "dark_marble", xf, ref r, WallFlags.Gloss);
        bool ajar = r.Chance(0.35f);
        var basis = new Basis(Vector3.Up, ajar ? r.Range(0.12f, 0.3f) : 0f);
        float shift = ajar ? 0.22f : 0f;
        lid.BoxT(new Transform3D(basis, P(shift, 0.88f, 0f)), P(1.24f, 0.17f, 2.4f), W);
        lid.BoxT(new Transform3D(basis, P(shift, 1.05f, 0f)), P(0.8f, 0.07f, 1.9f), W);
    }

    // ───────────── ruins and stonework ─────────────

    public static void Obelisk(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var m = M(b, b.Theme.DressStone, xf, ref r);
        float h = r.Range(3f, 4.4f);
        m.BoxAt(P(0f, -0.05f, 0f), P(1.25f, 0.4f, 1.25f), G(0.9f), 0.6f);
        m.BoxAt(P(0f, 0.35f, 0f), P(0.95f, 0.25f, 0.95f), W);
        m.Prism(P(0f, 0.6f, 0f), 0.52f, 0.33f, h, 4, W, false, 0.85f, Mathf.Pi / 4f);
        m.Prism(P(0f, 0.6f + h, 0f), 0.33f, 0.02f, 0.5f, 4, W, false, 1f, Mathf.Pi / 4f);
    }

    public static void RuinPillar(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var m = M(b, b.Theme.DressStone, xf, ref r);
        float h = r.Range(1.3f, 3f);
        m.BoxAt(P(0f, -0.05f, 0f), P(1.15f, 0.38f, 1.15f), G(0.9f), 0.6f);
        m.BoxAt(P(0f, 0.33f, 0f), P(0.82f, h, 0.82f), W, 0.85f);
        m.BoxT(new Transform3D(new Basis(Vector3.Up, r.Angle()) * new Basis(Vector3.Right, r.Range(0.15f, 0.4f)), P(r.Range(-0.1f, 0.1f), 0.25f + h, r.Range(-0.1f, 0.1f))),
            P(0.6f, 0.4f, 0.55f), G(0.95f), 1f, true);
        int n = 2 + r.Int(3);
        for (int i = 0; i < n; i++)
        {
            float a = r.Angle(), d = r.Range(0.9f, 1.7f);
            var size = P(r.Range(0.35f, 0.75f), r.Range(0.25f, 0.5f), r.Range(0.35f, 0.75f));
            m.BoxT(new Transform3D(Tumble(ref r, 0.4f), P(Mathf.Cos(a) * d, -0.06f, Mathf.Sin(a) * d)), size, G(r.Range(0.8f, 1f)), 0.7f, true);
        }
    }

    private static float PillarHeight(LevelTheme t) => t.Outdoor ? Mathf.Min(t.WallHeight + 0.15f, 5f) : t.WallHeight + 0.15f;

    public static void PillarSquare(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        float hgt = PillarHeight(b.Theme);
        var m = M(b, b.Theme.DressStone, xf, ref r);
        m.BoxAt(P(0f, 0f, 0f), P(1.3f, 0.45f, 1.3f), G(0.9f), 0.6f);
        m.BoxAt(P(0f, 0.45f, 0f), P(0.9f, hgt - 0.9f, 0.9f), W, 0.9f);
        m.BoxAt(P(0f, hgt - 0.45f, 0f), P(1.25f, 0.45f, 1.25f), W);
    }

    public static void PillarRound(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        float hgt = PillarHeight(b.Theme);
        var m = M(b, b.Theme.DressStone, xf, ref r);
        m.BoxAt(P(0f, 0f, 0f), P(1.3f, 0.4f, 1.3f), G(0.9f), 0.6f);
        m.Cyl(P(0f, 0.4f, 0f), 0.54f, 0.46f, 0.2f, 14, W, false);
        m.Cyl(P(0f, 0.6f, 0f), 0.43f, 0.39f, hgt - 1.32f, 14, W, false, 0.9f);
        m.Cyl(P(0f, hgt - 0.72f, 0f), 0.39f, 0.62f, 0.34f, 14, W, false);
        m.BoxAt(P(0f, hgt - 0.38f, 0f), P(1.3f, 0.38f, 1.3f), W);
    }

    public static void Rubble(EnvBuild b, Transform3D xf, bool big, ref EnvRng r)
    {
        var t = b.Theme;
        var m = M(b, r.Chance(0.5f) ? t.DressStone : t.DressStoneAlt, xf, ref r);
        int n = big ? 8 + r.Int(6) : 4 + r.Int(4);
        float spread = big ? 1.5f : 0.6f, smin = big ? 0.35f : 0.16f, smax = big ? 1f : 0.5f;
        for (int i = 0; i < n; i++)
        {
            float a = r.Angle(), d = spread * Mathf.Sqrt(r.F());
            var size = P(r.Range(smin, smax), r.Range(smin, smax) * 0.7f, r.Range(smin, smax));
            m.BoxT(new Transform3D(Tumble(ref r, 0.45f), P(Mathf.Cos(a) * d, -size.Y * 0.25f, Mathf.Sin(a) * d)), size, G(r.Range(0.8f, 1f)), 0.7f, true);
        }
    }

    /// <summary>A drift of pebbles and chips of the local stone: three to eight small tumbled blocks, half sunk.</summary>
    public static void Pebbles(EnvBuild b, Transform3D xf, string set, ref EnvRng r)
    {
        var m = M(b, string.IsNullOrEmpty(set) ? b.Theme.DressStone : set, xf, ref r);
        int n = 3 + r.Int(6);
        float spread = r.Range(0.35f, 0.9f);
        for (int i = 0; i < n; i++)
        {
            float a = r.Angle(), d = spread * Mathf.Sqrt(r.F());
            float s = i == 0 ? r.Range(0.16f, 0.3f) : r.Range(0.05f, 0.17f);
            var size = P(s * r.Range(0.8f, 1.5f), s * r.Range(0.45f, 0.8f), s * r.Range(0.8f, 1.5f));
            m.BoxT(new Transform3D(Tumble(ref r, 0.5f), P(Mathf.Cos(a) * d, -size.Y * 0.35f, Mathf.Sin(a) * d)), size, G(r.Range(0.7f, 1f)), 0.75f, true);
        }
    }

    /// <summary>A stretch of broken wall along local X: a stepped silhouette that crumbles towards one end.</summary>
    public static void RuinWall(EnvBuild b, Transform3D xf, float radius, ref EnvRng r)
    {
        float len = Mathf.Clamp(radius > 0.6f ? radius * 2f : r.Range(3.5f, 6.5f), 3f, 8f);
        int segs = Mathf.Max(3, Mathf.RoundToInt(len));
        float segW = len / segs, thick = 0.7f, hMax = r.Range(2.4f, 3.4f);
        bool fromLeft = r.Chance(0.5f);
        var m = M(b, b.Theme.DressStoneAlt, xf, ref r);
        m.BoxAt(P(0f, -0.1f, 0f), P(len + 0.3f, 0.35f, thick + 0.3f), G(0.85f), 0.6f);
        float h = hMax;
        for (int i = 0; i < segs; i++)
        {
            int k = fromLeft ? i : segs - 1 - i;
            float x = -len * 0.5f + (k + 0.5f) * segW;
            if (i > 0) h = Mathf.Max(0.35f, h - r.Range(0f, hMax * 0.42f));
            if (h > 2.3f && i > 0 && r.Chance(0.25f))
            {
                // a window opening survives in this bay
                m.BoxAt(P(x, 0.25f, 0f), P(segW, 0.9f, thick), W, 0.85f);
                m.BoxAt(P(x, 2f, 0f), P(segW, h - 1.75f, thick), W);
            }
            else m.BoxAt(P(x, 0.25f, 0f), P(segW, h, thick), W, 0.85f);
        }
        int fallen = 3 + r.Int(4);
        for (int i = 0; i < fallen; i++)
        {
            var size = P(r.Range(0.3f, 0.7f), r.Range(0.22f, 0.45f), r.Range(0.3f, 0.7f));
            float side = r.Chance(0.5f) ? 1f : -1f;
            m.BoxT(new Transform3D(Tumble(ref r, 0.4f), P(r.Range(-len * 0.5f, len * 0.5f), -0.05f, side * r.Range(0.7f, 1.7f))), size, G(r.Range(0.8f, 1f)), 0.7f, true);
        }
    }

    /// <summary>Two piers carrying a round arch of voussoirs across local X; sometimes half of it has fallen.</summary>
    public static void Arch(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var m = M(b, b.Theme.DressStone, xf, ref r);
        float span = r.Range(3.2f, 4.2f), rad = span * 0.5f, pierH = r.Range(2.4f, 3.2f), depth = 0.9f;
        bool broken = r.Chance(0.3f);
        float rightH = broken ? pierH * r.Range(0.4f, 0.75f) : pierH;
        m.BoxAt(P(-rad - 0.45f, -0.05f, 0f), P(0.95f, pierH + 0.05f, depth), W, 0.6f);
        m.BoxAt(P(rad + 0.45f, -0.05f, 0f), P(0.95f, rightH + 0.05f, depth), W, 0.6f);
        m.BoxAt(P(-rad - 0.45f, pierH, 0f), P(1.1f, 0.18f, depth + 0.12f), W);
        if (!broken) m.BoxAt(P(rad + 0.45f, pierH, 0f), P(1.1f, 0.18f, depth + 0.12f), W);
        const int n = 9;
        int upto = broken ? 4 + r.Int(2) : n;
        for (int i = 0; i < upto; i++)
        {
            float a = Mathf.Pi - (i + 0.5f) / n * Mathf.Pi;                    // from the left pier over to the right
            var c = P(Mathf.Cos(a) * rad, pierH + 0.18f + Mathf.Sin(a) * rad, 0f);
            m.BoxT(new Transform3D(new Basis(Vector3.Back, a - Mathf.Pi * 0.5f), c), P(Mathf.Pi * rad / n * 1.12f, 0.5f, depth), W, 1f, true);
        }
        if (broken)
        {
            for (int i = 0; i < 4; i++)
            {
                var size = P(r.Range(0.4f, 0.7f), r.Range(0.35f, 0.5f), r.Range(0.5f, 0.85f));
                m.BoxT(new Transform3D(Tumble(ref r, 0.5f), P(r.Range(0f, rad + 0.8f), -0.05f, r.Range(-1.2f, 1.2f))), size, G(r.Range(0.8f, 1f)), 0.7f, true);
            }
        }
    }

    /// <summary>Drum column on a plinth. Broken ones lose their capital and leave drums on the ground. Returns the height of its top.</summary>
    public static float Column(EnvBuild b, Transform3D xf, float height, bool broken, ref EnvRng r)
    {
        var m = M(b, b.Theme.DressStone, xf, ref r);
        m.BoxAt(P(0f, -0.05f, 0f), P(1.35f, 0.4f, 1.35f), G(0.9f), 0.6f);
        m.Cyl(P(0f, 0.35f, 0f), 0.62f, 0.54f, 0.22f, 16, W, false);
        float y = 0.57f, rad = 0.5f;
        float shaft = broken ? height * r.Range(0.3f, 0.6f) : height;
        int drums = Mathf.Max(2, Mathf.RoundToInt(shaft / 1.1f));
        float dh = shaft / drums;
        for (int d = 0; d < drums; d++)
        {
            float r1 = rad - 0.012f;
            var off = P(r.Range(-0.025f, 0.025f), 0f, r.Range(-0.025f, 0.025f));
            m.Cyl(P(0f, y, 0f) + off, rad, r1, dh - 0.02f, 16, G(r.Range(0.9f, 1f)), d == drums - 1, d == 0 ? 0.85f : 1f);
            y += dh;
            rad = r1;
        }
        if (!broken)
        {
            m.Cyl(P(0f, y, 0f), rad, rad + 0.26f, 0.38f, 16, W, false);
            y += 0.38f;
            m.BoxAt(P(0f, y, 0f), P(1.45f, 0.26f, 1.45f), W);
            return y + 0.26f;
        }
        int fallen = 1 + r.Int(2);
        for (int i = 0; i < fallen; i++)
        {
            float a = r.Angle(), d = r.Range(1.2f, 2.1f);
            m.Xf = xf * new Transform3D(new Basis(Vector3.Up, r.Angle()) * new Basis(Vector3.Right, Mathf.Pi * 0.5f), P(Mathf.Cos(a) * d, 0.44f, Mathf.Sin(a) * d));
            m.Cyl(P(0f, -0.5f, 0f), 0.47f, 0.46f, r.Range(0.8f, 1.1f), 14, G(0.92f), true, 1f, true);
        }
        m.Xf = xf;
        return 0f;
    }

    public static void Colonnade(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        int n = 3 + r.Int(2);
        const float gap = 2.7f;
        float height = r.Range(3.2f, 4f);
        var tops = new float[n];
        for (int i = 0; i < n; i++)
            tops[i] = Column(b, At(xf, (i - (n - 1) * 0.5f) * gap, 0f, 0f), height, r.Chance(0.25f), ref r);
        var m = M(b, b.Theme.DressStone, xf, ref r);
        for (int i = 0; i < n - 1; i++)
            if (tops[i] > 1f && tops[i + 1] > 1f && r.Chance(0.75f))
                m.BoxAt(P((i + 0.5f - (n - 1) * 0.5f) * gap, tops[i], 0f), P(gap + 1f, 0.45f, 1f), W);
    }

    /// <summary>Clustered gothic pier rising to the full height of the nave.</summary>
    public static void CathColumn(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        float hgt = Mathf.Min(t.WallHeight + 0.3f, 7.5f);
        var m = M(b, t.DressStoneAlt, xf, ref r);
        m.Prism(P(0f, 0f, 0f), 1.02f, 1.02f, 0.55f, 8, G(0.9f), true, 0.6f, Mathf.Pi / 8f);
        m.Prism(P(0f, 0.55f, 0f), 0.86f, 0.74f, 0.3f, 8, W, true, 1f, Mathf.Pi / 8f);
        m.Cyl(P(0f, 0.85f, 0f), 0.5f, 0.47f, hgt - 1.75f, 14, W, false, 0.9f);
        for (int k = 0; k < 4; k++)
        {
            float a = Mathf.Pi / 4f + k * Mathf.Pi / 2f;
            m.Cyl(P(Mathf.Cos(a) * 0.5f, 0.85f, Mathf.Sin(a) * 0.5f), 0.17f, 0.16f, hgt - 1.75f, 8, W, false, 0.9f);
        }
        m.Cyl(P(0f, hgt - 0.9f, 0f), 0.62f, 1f, 0.5f, 14, W, false);
        m.Prism(P(0f, hgt - 0.4f, 0f), 1.08f, 1.08f, 0.4f, 8, W, true, 1f, Mathf.Pi / 8f);
    }

    public static void BrassColumn(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        float hgt = Mathf.Min(t.WallHeight + 0.15f, 6f);
        var m = M(b, t.DressStone, xf, ref r);
        m.BoxAt(P(0f, 0f, 0f), P(1.3f, 0.5f, 1.3f), G(0.9f), 0.6f);
        m.BoxAt(P(0f, hgt - 0.4f, 0f), P(1.3f, 0.4f, 1.3f), W);
        var br = M(b, "tarnished_bronze", xf, ref r, WallFlags.NoTop);
        br.Cyl(P(0f, 0.5f, 0f), 0.44f, 0.44f, hgt - 0.9f, 16, W, false, 0.85f);
        for (int i = 0; i < 4; i++)
        {
            float y = 0.5f + (hgt - 0.9f) * (i + 0.5f) / 4f;
            br.Cyl(P(0f, y - 0.07f, 0f), 0.52f, 0.52f, 0.14f, 16, W, true, 1f, true);
        }
    }

    // ───────────── crystals and cave growths ─────────────

    public static void Crystal(EnvBuild b, Transform3D xf, string set, WallFlags flags, Color tint, bool big, ref EnvRng r)
    {
        var m = M(b, set, xf, ref r, flags, tint);
        int n = big ? 4 + r.Int(3) : 2 + r.Int(2);
        float baseH = big ? r.Range(2.2f, 3.6f) : r.Range(0.5f, 1f);
        float baseR = big ? r.Range(0.34f, 0.5f) : r.Range(0.1f, 0.17f);
        for (int i = 0; i < n; i++)
        {
            float k = i == 0 ? 1f : r.Range(0.35f, 0.75f);
            float a = r.Angle();
            float lean = i == 0 ? r.Range(0f, 0.12f) : r.Range(0.25f, 0.6f);
            float dist = i == 0 ? 0f : baseR * r.Range(0.8f, 1.6f);
            m.Xf = xf * new Transform3D(new Basis(Vector3.Up, a) * new Basis(Vector3.Right, lean), P(Mathf.Sin(a) * dist, -0.1f, Mathf.Cos(a) * dist));
            float h = baseH * k, rr = baseR * (0.55f + 0.45f * k), twist = r.Angle();
            var shade = G(r.Range(0.85f, 1f));
            m.Prism(Vector3.Zero, rr, rr * 0.82f, h, 6, shade, false, 0.75f, twist);
            m.Prism(P(0f, h, 0f), rr * 0.82f, 0.02f, rr * 1.7f, 6, shade, false, 1f, twist);
        }
        m.Xf = xf;
    }

    public static void CrystalCluster(EnvBuild b, Transform3D xf, string set, WallFlags flags, Color tint, ref EnvRng r)
    {
        int big = 2 + r.Int(2);
        for (int i = 0; i < big; i++)
        {
            float a = r.Angle(), d = i == 0 ? 0f : r.Range(0.8f, 1.6f);
            Crystal(b, At(xf, Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d, r.Angle()), set, flags, tint, true, ref r);
        }
        int small = 3 + r.Int(3);
        for (int i = 0; i < small; i++)
        {
            float a = r.Angle(), d = r.Range(1f, 2.3f);
            Crystal(b, At(xf, Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d, r.Angle()), set, flags, tint, false, ref r);
        }
    }

    public static void Stalagmite(EnvBuild b, Transform3D xf, bool cluster, ref EnvRng r)
    {
        var t = b.Theme;
        var flags = WallFlags.Tri;
        if (t.WallTex != null && t.WallTex.StartsWith("ice", StringComparison.Ordinal)) flags |= WallFlags.Gloss;
        var m = M(b, t.WallTex, xf, ref r, flags);
        int n = cluster ? 4 + r.Int(4) : 1 + r.Int(3);
        for (int i = 0; i < n; i++)
        {
            float k = i == 0 ? 1f : r.Range(0.3f, 0.7f);
            float a = r.Angle(), dist = i == 0 ? 0f : r.Range(0.5f, cluster ? 1.8f : 0.9f);
            float h = (cluster ? r.Range(2f, 3.6f) : r.Range(1.6f, 3f)) * k;
            float rad = h * r.Range(0.2f, 0.28f);
            var foot = P(Mathf.Cos(a) * dist, -0.1f, Mathf.Sin(a) * dist);
            m.Cyl(foot, rad, rad * 0.55f, h * 0.45f, 9, G(0.95f), false, 0.6f);
            m.Cyl(foot + P(0f, h * 0.45f, 0f), rad * 0.55f, 0.03f, h * 0.55f, 9, W, false);
        }
    }

    // ───────────── town ─────────────

    /// <summary>
    /// Timber-and-plaster house with a pitched roof: stone footing, plastered walls in a timber frame, gables,
    /// tiled or thatched roof, chimney, door and warm windows. <paramref name="doorDir"/> is given in the frame of xf.
    /// </summary>
    public static void House(EnvBuild b, Transform3D xf, float halfX, float halfZ, Vector3 doorDir, ref EnvRng r, int kind = 0)
    {
        var t = b.Theme;
        if (halfX > halfZ)
        {
            // the ridge runs along the long side: turn the frame so that side is local Z
            var turn = new Basis(Vector3.Up, Mathf.Pi * 0.5f);
            xf = xf * new Transform3D(turn, Vector3.Zero);
            doorDir = turn.Inverse() * doorDir;
            (halfX, halfZ) = (halfZ, halfX);
        }
        float hw = halfX, hd = halfZ;
        // kind: 0 cottage, 1 the keepers' hall (dressed stone, tall, always lit), 2 the inn (tall, every window lit)
        float baseH = r.Range(0.55f, 0.85f), wallH = kind == 1 ? r.Range(3.7f, 4.1f) : kind == 2 ? r.Range(3.3f, 3.6f) : r.Range(2.7f, 3.3f);
        float ridgeH = wallH + hw * r.Range(0.8f, 1.05f);
        bool thatch = kind == 0 && r.Chance(0.4f);
        const float proud = 0.05f;

        var m = M(b, t.DressStone, xf, ref r);
        m.Box(P(-hw - 0.07f, -0.1f, -hd - 0.07f), P(hw + 0.07f, baseH, hd + 0.07f), W, 0.6f);

        m = M(b, kind == 1 ? t.DressStoneAlt : "timber_plaster", xf, ref r);
        m.Box(P(-hw, baseH, -hd), P(hw, wallH, hd), W, 0.9f, false);
        m.Tri3(P(-hw, wallH, hd), P(hw, wallH, hd), P(0f, ridgeH, hd), Vector3.Back, W);
        m.Tri3(P(hw, wallH, -hd), P(-hw, wallH, -hd), P(0f, ridgeH, -hd), Vector3.Forward, W);

        m = M(b, t.DressWood, xf, ref r, WallFlags.None, new Color(0.62f, 0.55f, 0.5f));
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                m.BoxAt(P(sx * (hw + proud - 0.1f), baseH, sz * (hd + proud - 0.1f)), P(0.2f, wallH - baseH, 0.2f), W, 1f, false);
        for (int level = 0; level < 2; level++)
        {
            float y = level == 0 ? baseH : wallH - 0.17f;
            m.BoxAt(P(0f, y, hd + proud - 0.06f), P(hw * 2f + 0.1f, 0.17f, 0.12f), W);
            m.BoxAt(P(0f, y, -hd - proud + 0.06f), P(hw * 2f + 0.1f, 0.17f, 0.12f), W);
            m.BoxAt(P(hw + proud - 0.06f, y, 0f), P(0.12f, 0.17f, hd * 2f + 0.1f), W);
            m.BoxAt(P(-hw - proud + 0.06f, y, 0f), P(0.12f, 0.17f, hd * 2f + 0.1f), W);
        }
        int studs = Mathf.Max(1, Mathf.RoundToInt(hd * 2f / 1.6f) - 1);
        for (int i = 1; i <= studs; i++)
        {
            float z = -hd + i * (hd * 2f / (studs + 1));
            m.BoxAt(P(hw + proud - 0.06f, baseH, z), P(0.12f, wallH - baseH, 0.14f), W, 1f, false);
            m.BoxAt(P(-hw - proud + 0.06f, baseH, z), P(0.12f, wallH - baseH, 0.14f), W, 1f, false);
        }
        for (int sz = -1; sz <= 1; sz += 2)
        {
            float z = sz * (hd + proud - 0.06f);
            m.BoxAt(P(0f, baseH, z), P(0.16f, ridgeH - baseH - 0.15f, 0.12f), W, 1f, false);                                  // king post
            m.BoxAt(P(0f, wallH + (ridgeH - wallH) * 0.45f, z), P(hw * 1.1f, 0.14f, 0.12f), W);                              // collar beam
            float braceLen = (wallH - baseH) / Mathf.Cos(0.6f) * 0.96f;
            m.BoxT(new Transform3D(new Basis(Vector3.Back, 0.6f), P(hw * 0.86f, baseH, z)), P(0.13f, braceLen, 0.11f), W);
            m.BoxT(new Transform3D(new Basis(Vector3.Back, -0.6f), P(-hw * 0.86f, baseH, z)), P(0.13f, braceLen, 0.11f), W);
        }

        m = M(b, thatch ? "thatch" : t.DressRoof, xf, ref r, thatch ? WallFlags.Dull : WallFlags.None);
        m.Gable(hw + 0.02f, hd, wallH - 0.02f, ridgeH + 0.03f, 0.5f, W);

        m = M(b, t.DressWood, xf, ref r, WallFlags.None, new Color(0.5f, 0.45f, 0.42f));
        m.BoxAt(P(0f, ridgeH - 0.05f, 0f), P(0.2f, 0.16f, hd * 2f + 1f), W);

        if (r.Chance(0.75f))
        {
            m = M(b, "old_brick", xf, ref r);
            float cx = hw * r.Range(0.35f, 0.6f) * (r.Chance(0.5f) ? 1f : -1f), cz = hd * r.Range(-0.5f, 0.5f);
            m.BoxAt(P(cx, wallH, cz), P(0.62f, ridgeH - wallH + 0.75f, 0.62f), W);
            m.BoxAt(P(cx, ridgeH + 0.75f, cz), P(0.78f, 0.12f, 0.78f), G(0.8f));
        }

        // door on the wall that faces doorDir
        bool doorOnX = Mathf.Abs(doorDir.X) > Mathf.Abs(doorDir.Z);
        float ds = (doorOnX ? doorDir.X : doorDir.Z) < 0f ? -1f : 1f;
        m = M(b, "dark_planks", xf, ref r);
        Panel(m, hw, hd, doorOnX, ds, 0f, baseH * 0.2f, 1.05f, 2f, 0.09f, W);
        m = M(b, t.DressWood, xf, ref r, WallFlags.None, new Color(0.55f, 0.5f, 0.46f));
        Panel(m, hw, hd, doorOnX, ds, -0.62f, baseH * 0.2f, 0.15f, 2.15f, 0.13f, W);
        Panel(m, hw, hd, doorOnX, ds, 0.62f, baseH * 0.2f, 0.15f, 2.15f, 0.13f, W);
        Panel(m, hw, hd, doorOnX, ds, 0f, baseH * 0.2f + 2f, 1.4f, 0.16f, 0.13f, W);
        m = M(b, t.DressStone, xf, ref r);
        if (doorOnX) m.BoxAt(P(ds * (hw + 0.45f), -0.05f, 0f), P(0.7f, 0.2f, 1.5f), G(0.9f), 0.7f);
        else m.BoxAt(P(0f, -0.05f, ds * (hd + 0.45f)), P(1.5f, 0.2f, 0.7f), G(0.9f), 0.7f);

        // windows: eave sides and gable ends, most of them lit from inside
        int perSide = hd > 2.6f ? 2 : 1;
        var frame = new Color(0.4f, 0.36f, 0.33f);
        for (int side = 0; side < 4; side++)
        {
            bool onX = side < 2;
            float sign = side % 2 == 0 ? 1f : -1f;
            int count = onX ? perSide : 1;
            for (int i = 0; i < count; i++)
            {
                float along = onX ? (count == 1 ? 0f : (i == 0 ? -hd * 0.45f : hd * 0.45f)) : (r.Chance(0.5f) ? 0.9f : -0.9f) * Mathf.Min(1f, hw / 2.4f);
                if (onX == doorOnX && sign == ds && Mathf.Abs(along) < 1.25f) continue;        // the door is here
                bool lit = r.Chance(kind == 0 ? 0.62f : 0.94f);
                m = M(b, t.DressWood, xf, ref r, WallFlags.None, frame);
                Panel(m, hw, hd, onX, sign, along, 1.36f, 0.84f, 0.96f, 0.06f, W);
                m = M(b, "dark_planks", xf, ref r, lit ? WallFlags.Glow | WallFlags.NoShadow : WallFlags.NoShadow, lit ? t.TorchColor : new Color(0.2f, 0.2f, 0.22f));
                Panel(m, hw, hd, onX, sign, along, 1.46f, 0.64f, 0.76f, 0.085f, W);
                m = M(b, t.DressWood, xf, ref r, WallFlags.None, frame);
                Panel(m, hw, hd, onX, sign, along, 1.46f, 0.07f, 0.76f, 0.11f, W);
                Panel(m, hw, hd, onX, sign, along, 1.8f, 0.64f, 0.07f, 0.11f, W);
            }
        }

        // a lantern by the door (the hall and the inn carry one on each side)
        int lanterns = kind != 0 ? 2 : r.Chance(0.7f) ? 1 : 0;
        for (int li = 0; li < lanterns; li++)
        {
            float sideOff = li == 0 ? 0.95f : -0.95f;
            var outward = doorOnX ? new Vector3(ds, 0f, 0f) : new Vector3(0f, 0f, ds);
            var mount = doorOnX ? P(ds * (hw + 0.06f), 2.05f, sideOff) : P(sideOff, 2.05f, ds * (hd + 0.06f));
            float yaw = YawOf(xf.Basis * outward);
            var wp = xf * mount;
            var lxf = EnvProps.Xf(wp, yaw, 1f);
            Vector3 flame = lxf * new Vector3(0f, 0.674f, 0.614f);
            if (EnvProps.Place(b, "wall_lantern", wp, yaw, 1f, false)) b.AddFlame(flame - Vector3.Up * 0.05f, 0.3f, 1f, r.F());
            if (li == 0) b.AddLight(flame + (xf.Basis * outward).Normalized() * 0.45f + Vector3.Up * 0.1f, t.TorchColor, EnvProps.TorchEnergy * (kind != 0 ? 0.85f : 0.65f), t.TorchRange * 0.75f, 1f, false);
        }
        if (kind == 1)
        {
            // the keepers' colours hang beside the door
            var outward = doorOnX ? new Vector3(ds, 0f, 0f) : new Vector3(0f, 0f, ds);
            var at = doorOnX ? P(ds * (hw + 0.7f), 0f, -2.1f) : P(-2.1f, 0f, ds * (hd + 0.7f));
            Banner(b, xf * new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(outward.X, outward.Z)), at), ref r);
        }
    }

    /// <summary>Thin panel standing proud of one wall of a box-shaped building (door leaves, window frames, glass).</summary>
    private static void Panel(EnvMesh m, float hw, float hd, bool onX, float sign, float along, float y, float w, float h, float thick, Color c)
    {
        if (onX) m.BoxAt(P(sign * (hw + thick * 0.5f), y, along), P(thick, h, w), c, 1f, true, true);
        else m.BoxAt(P(along, y, sign * (hd + thick * 0.5f)), P(w, h, thick), c, 1f, true, true);
    }

    private static Color Cloth(ref EnvRng r) => r.Int(4) switch
    {
        0 => new Color(0.82f, 0.36f, 0.28f),
        1 => new Color(0.84f, 0.7f, 0.42f),
        2 => new Color(0.46f, 0.56f, 0.7f),
        _ => new Color(0.74f, 0.7f, 0.62f),
    };

    /// <summary>
    /// The smithy: an open shed on four posts with a tiled roof, a stone hearth whose coals glow, a chimney, an anvil
    /// on its block and a quenching trough. Local +Z is the open front. About 3.2 m square.
    /// </summary>
    public static void Forge(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        const float hw = 1.55f, hd = 1.45f, eave = 2.7f, ridge = 3.5f;
        var m = M(b, t.DressWood, xf, ref r);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                m.BoxAt(P(sx * hw, -0.05f, sz * hd), P(0.2f, eave + 0.05f, 0.2f), W, 0.7f);
        m.BoxAt(P(0f, eave - 0.14f, hd), P(hw * 2f + 0.3f, 0.16f, 0.16f), W);
        m.BoxAt(P(0f, eave - 0.14f, -hd), P(hw * 2f + 0.3f, 0.16f, 0.16f), W);
        m = M(b, t.DressRoof == "thatch" ? "roof_tiles" : t.DressRoof, xf * new Transform3D(new Basis(Vector3.Up, Mathf.Pi * 0.5f), Vector3.Zero), ref r);
        m.Gable(hd + 0.1f, hw + 0.1f, eave, ridge, 0.35f, W);
        // back wall of rough planks, low side wall
        m = M(b, t.DressPlank, xf, ref r);
        m.BoxAt(P(0f, -0.05f, -hd + 0.02f), P(hw * 2f - 0.2f, 2.1f, 0.07f), G(0.85f), 0.7f);
        m.BoxAt(P(-hw + 0.02f, -0.05f, -0.3f), P(0.07f, 1.1f, hd * 1.3f), G(0.85f), 0.7f);
        // hearth and chimney
        m = M(b, t.DressStone, xf, ref r);
        m.BoxAt(P(0.55f, -0.05f, -hd + 0.6f), P(1.5f, 0.95f, 1.0f), W, 0.6f);
        m.BoxAt(P(0.55f, 0.9f, -hd + 0.25f), P(1.5f, 0.5f, 0.3f), W);
        m = M(b, "old_brick", xf, ref r);
        m.BoxAt(P(0.55f, 1.4f, -hd + 0.28f), P(0.7f, ridge - 0.5f, 0.5f), W);
        m.BoxAt(P(0.55f, ridge + 0.9f, -hd + 0.28f), P(0.86f, 0.12f, 0.66f), G(0.8f));
        m = M(b, "iron_plate", xf, ref r, WallFlags.Glow | WallFlags.NoShadow, Ember);
        m.BoxAt(P(0.55f, 0.9f, -hd + 0.72f), P(1.1f, 0.06f, 0.6f), W);
        Anvil(b, At(xf, -0.45f, 0f, 0.25f, 0.5f), ref r);
        float s = xf.Basis.X.Length();
        EnvProps.Place(b, "trough_a", xf * P(-0.95f, 0f, -0.7f), YawOf(xf.Basis.X), EnvProps.BaseScale("trough_a") * s * 0.8f, true);
        EnvProps.Place(b, "smith_hammer", xf * P(0.1f, 0.96f, -hd + 0.95f), r.Angle(), EnvProps.BaseScale("smith_hammer") * s, false);
        var fp = xf * P(0.55f, 0.98f, -hd + 0.72f);
        b.AddFlame(fp, 0.42f * s, 1.5f, r.F());
        b.AddFlame(fp + xf.Basis.X * 0.3f, 0.3f * s, 1.2f, r.F());
        b.AddLight(fp + Vector3.Up * 0.5f + xf.Basis.Z.Normalized() * 0.5f, t.TorchColor, EnvProps.TorchEnergy * 1.2f, t.TorchRange * 0.95f, 1f, true);
    }

    public static void Stall(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        const float w = 2.8f, d = 1.7f, hBack = 2.5f, hFront = 2f;
        var m = M(b, t.DressWood, xf, ref r);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            m.BoxAt(P(sx * w * 0.5f, -0.05f, -d * 0.5f), P(0.12f, hBack + 0.05f, 0.12f), W, 0.7f);
            m.BoxAt(P(sx * w * 0.5f, -0.05f, d * 0.5f), P(0.12f, hFront + 0.05f, 0.12f), W, 0.7f);
        }
        m = M(b, t.DressPlank, xf, ref r);
        m.BoxAt(P(0f, -0.05f, d * 0.5f - 0.3f), P(w - 0.1f, 1f, 0.6f), W, 0.7f);
        m.BoxAt(P(0f, 0.95f, d * 0.5f - 0.3f), P(w + 0.1f, 0.06f, 0.75f), W);
        m.BoxAt(P(0f, -0.05f, -d * 0.5f + 0.1f), P(w - 0.2f, 1.5f, 0.06f), G(0.85f), 0.7f);

        float run = d + 0.6f, drop = hBack - hFront + 0.1f, len = Mathf.Sqrt(run * run + drop * drop);
        m = M(b, "canvas", xf, ref r, WallFlags.Dull | WallFlags.NoTop, Cloth(ref r));
        m.BoxT(new Transform3D(new Basis(Vector3.Right, Mathf.Atan2(drop, run)), P(0f, hBack - drop * 0.5f + 0.04f, -d * 0.5f + run * 0.5f - 0.1f)),
            P(w + 0.5f, 0.05f, len), W, 1f, true);

        string[] goods = { "basket_a", "clay_pot", "bowl_a", "jug", "basket_b", "brass_pan" };
        int n = 2 + r.Int(2);
        float s = xf.Basis.X.Length();
        for (int i = 0; i < n; i++)
        {
            string g = goods[r.Int(goods.Length)];
            var wp = xf * P((i - (n - 1) * 0.5f) * 0.8f + r.Range(-0.1f, 0.1f), 1.01f, d * 0.5f - 0.3f + r.Range(-0.1f, 0.1f));
            EnvProps.Place(b, g, wp, r.Angle(), EnvProps.BaseScale(g) * s * 0.85f, false);
        }
    }

    public static void Well(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        var m = M(b, t.DressStone, xf, ref r);
        m.Cyl(P(0f, -0.1f, 0f), 1.05f, 1f, 0.95f, 14, W, true, 0.6f);
        m.Cyl(P(0f, 0.84f, 0f), 0.7f, 0.7f, 0.02f, 14, G(0.03f), true);                   // the dark shaft
        m = M(b, t.DressWood, xf, ref r);
        m.BoxAt(P(1f, 0.4f, 0f), P(0.16f, 2.2f, 0.16f), W);
        m.BoxAt(P(-1f, 0.4f, 0f), P(0.16f, 2.2f, 0.16f), W);
        m.BoxAt(P(0f, 2.3f, 0f), P(2.5f, 0.14f, 0.14f), W);
        var roofXf = xf * new Transform3D(new Basis(Vector3.Up, Mathf.Pi * 0.5f), Vector3.Zero);
        bool thatch = t.DressRoof == "thatch" || r.Chance(0.5f);
        m = M(b, thatch ? "thatch" : t.DressRoof, roofXf, ref r, thatch ? WallFlags.Dull : WallFlags.None);
        m.Gable(0.85f, 1.4f, 2.55f, 3.15f, 0.15f, W);
        float s = xf.Basis.X.Length();
        EnvProps.Place(b, "bucket", xf * P(0.55f, 0.86f, 0.62f), r.Angle(), EnvProps.BaseScale("bucket") * s, false);
    }

    public static void Tent(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        const float hw = 1.25f, hd = 1.6f, h = 2f;
        var m = M(b, "canvas", xf, ref r, WallFlags.Dull, Cloth(ref r));
        m.Gable(hw, hd, 0f, h, 0f, W);
        m.Tri3(P(hw, 0f, -hd), P(-hw, 0f, -hd), P(0f, h, -hd), Vector3.Forward, G(0.85f));
        m.Tri3(P(-hw, 0f, hd), P(-hw * 0.25f, 0f, hd), P(0f, h, hd), Vector3.Back, G(0.8f));
        m.Tri3(P(hw * 0.25f, 0f, hd), P(hw, 0f, hd), P(0f, h, hd), Vector3.Back, G(0.8f));
        // seams and a patch, so the pitched cloth reads as a tent and not as a board
        var seam = M(b, "burlap", xf, ref r, WallFlags.Dull | WallFlags.NoShadow);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int k = -1; k <= 1; k++)
            {
                float z = k * hd * 0.62f, slope = Mathf.Atan2(h, hw);
                seam.BoxT(new Transform3D(new Basis(Vector3.Back, sx * -slope), P(sx * hw * 0.5f, h * 0.5f + 0.02f, z)),
                    P(Mathf.Sqrt(hw * hw + h * h) * 0.96f, 0.02f, 0.07f), G(0.7f), 1f, true);
            }
        m = M(b, b.Theme.DressWood, xf, ref r);
        m.BoxAt(P(0f, h - 0.03f, 0f), P(0.08f, 0.08f, hd * 2f + 0.5f), W);
        m.BoxAt(P(0f, 0f, hd + 0.05f), P(0.08f, h + 0.25f, 0.08f), W, 0.7f);
        m.BoxAt(P(0f, 0f, -hd - 0.05f), P(0.08f, h + 0.25f, 0.08f), W, 0.7f);
        m = M(b, "burlap", xf, ref r, WallFlags.Dull);
        m.BoxT(new Transform3D(new Basis(Vector3.Up, r.Range(-0.4f, 0.4f)), P(hw + 0.9f, -0.02f, r.Range(-0.5f, 0.5f))), P(0.7f, 0.14f, 1.8f), W, 0.8f);
    }

    /// <summary>A run of sharpened stakes between two world points on the ground.</summary>
    public static void Palisade(EnvBuild b, Vector3 from, Vector3 to, ref EnvRng r)
    {
        var dir = to - from;
        float len = dir.Length();
        if (len < 0.2f) return;
        var xf = new Transform3D(new Basis(Vector3.Up, YawOf(dir)), from);        // local +Z runs along the fence
        var m = M(b, "bark", xf, ref r);
        int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.36f));
        for (int i = 0; i < n; i++)
        {
            float z = (i + 0.5f) * len / n;
            float rad = r.Range(0.15f, 0.19f), h = r.Range(2.6f, 3.3f);
            var p = P(r.Range(-0.05f, 0.05f), -0.2f, z);
            m.Prism(p, rad, rad * 0.92f, h, 6, G(r.Range(0.8f, 1f)), false, 0.6f, r.Angle());
            m.Prism(p + P(0f, h, 0f), rad * 0.92f, 0.02f, 0.4f, 6, W, false);
        }
        m = M(b, b.Theme.DressPlank, xf, ref r);
        m.Box(P(-0.27f, 1.7f, 0f), P(-0.17f, 1.92f, len), W, 1f, true, true);
        m.Box(P(0.17f, 0.9f, 0f), P(0.27f, 1.1f, len), W, 1f, true, true);
    }

    public static void Post(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        var m = M(b, t.DressWood, xf, ref r);
        m.BoxAt(P(0f, -0.1f, 0f), P(0.2f, 2.8f, 0.2f), W, 0.6f);
        m.BoxAt(P(0.35f, 2.4f, 0f), P(0.9f, 0.1f, 0.1f), W);
        float s = xf.Basis.X.Length();
        var lp = xf * P(0.7f, 1.55f, 0f);
        if (EnvProps.Place(b, "lantern_wood", lp, r.Angle(), 1.6f * s, false)) b.AddFlame(lp + Vector3.Up * (0.25f * s), 0.3f * s, 1f, r.F());
        b.AddLight(lp + Vector3.Up * 0.5f, t.TorchColor, EnvProps.TorchEnergy * 0.7f, t.TorchRange * 0.8f, 1f, false);
    }

    public static void Banner(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        var m = M(b, t.DressStone, xf, ref r);
        m.BoxAt(P(0f, -0.05f, 0f), P(0.6f, 0.35f, 0.6f), G(0.9f), 0.6f);
        m = M(b, t.DressWood, xf, ref r);
        m.Cyl(P(0f, 0.3f, 0f), 0.07f, 0.06f, 4.3f, 8, W, true);
        m.BoxAt(P(0f, 4.25f, 0f), P(1.3f, 0.09f, 0.09f), W);
        bool velvet = t.Id is "sanctum" or "cathedral";
        var tint = velvet ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.25f + t.Accent.R * 0.35f, 0.22f + t.Accent.G * 0.3f, 0.22f + t.Accent.B * 0.3f);
        m = M(b, velvet ? "red_velvet" : "canvas", xf, ref r, WallFlags.Dull | WallFlags.NoTop, tint);
        m.Box(P(-0.55f, 1.5f, -0.025f), P(0.55f, 4.2f, 0.025f), W, 0.85f, true, true);
    }

    public static void Altar(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        var m = M(b, t.DressStone, xf, ref r);
        m.BoxAt(P(0f, -0.05f, 0f), P(2.9f, 0.25f, 1.9f), G(0.9f), 0.6f);
        m.BoxAt(P(0f, 0.2f, 0f), P(2.4f, 0.18f, 1.4f), W);
        m.BoxAt(P(0f, 0.38f, 0f), P(1.9f, 0.85f, 0.95f), W, 0.85f);
        m = M(b, "dark_marble", xf, ref r, WallFlags.Gloss);
        m.BoxAt(P(0f, 1.23f, 0f), P(2.1f, 0.12f, 1.1f), W);
        m = M(b, "red_velvet", xf, ref r, WallFlags.Dull | WallFlags.NoTop);
        m.BoxAt(P(0f, 1.35f, 0f), P(0.7f, 0.02f, 1.14f), W);
        m.Box(P(-0.35f, 0.75f, 0.55f), P(0.35f, 1.36f, 0.58f), W, 0.9f, true, true);

        float s = xf.Basis.X.Length();
        float yaw = YawOf(xf.Basis.Z);
        var centre = Vector3.Zero;
        int lit = 0;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            var wp = xf * P(sx * 0.75f, 1.35f, 0f);
            var def = EnvProps.Def("candelabra_3");
            if (def == null) continue;
            var cx = EnvProps.Xf(wp, yaw, 2f * s);
            b.AddProp(def, cx, false);
            if (def.Flames != null)
                foreach (var f in def.Flames)
                {
                    var fp = cx * f;
                    b.AddFlame(fp, 0.26f * s, 1f, r.F());
                    centre += fp;
                    lit++;
                }
        }
        var lightPos = lit > 0 ? centre / lit + Vector3.Up * 0.3f : xf * P(0f, 2.2f, 0f);
        b.AddLight(lightPos, t.TorchColor, EnvProps.TorchEnergy * 0.7f, t.TorchRange * 0.8f, 1f, false);
    }

    public static void Anvil(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        var m = M(b, "bark", xf, ref r);
        m.Cyl(P(0f, -0.05f, 0f), 0.36f, 0.33f, 0.6f, 10, W, true, 0.6f);
        m = M(b, "iron_plate", xf, ref r, WallFlags.NoTop);
        m.BoxAt(P(0f, 0.55f, 0f), P(0.3f, 0.12f, 0.5f), W);
        m.BoxAt(P(0f, 0.67f, 0f), P(0.2f, 0.16f, 0.34f), W);
        m.BoxAt(P(0f, 0.83f, 0f), P(0.3f, 0.13f, 0.8f), W);
        m.Xf = xf * new Transform3D(new Basis(Vector3.Right, Mathf.Pi * 0.5f), P(0f, 0.9f, 0.4f));
        m.Prism(Vector3.Zero, 0.1f, 0.02f, 0.36f, 6, W, false);
        m.Xf = xf;
    }

    // ───────────── lights ─────────────

    /// <summary>Stone plinth or wooden post that lifts a small light prop to eye level. Returns the height of its top.</summary>
    public static float Stand(EnvBuild b, Vector3 pos, float yaw, bool wood, ref EnvRng r)
    {
        var t = b.Theme;
        var xf = EnvProps.Xf(pos, yaw, 1f);
        if (wood)
        {
            var m = M(b, t.DressWood, xf, ref r);
            m.BoxAt(P(0f, -0.1f, 0f), P(0.22f, 1.3f, 0.22f), W, 0.6f);
            m.BoxAt(P(0f, 1.2f, 0f), P(0.4f, 0.06f, 0.4f), W);
            return 1.26f;
        }
        var s = M(b, t.DressStone, xf, ref r);
        s.BoxAt(P(0f, -0.05f, 0f), P(0.74f, 0.2f, 0.74f), G(0.9f), 0.6f);
        s.BoxAt(P(0f, 0.15f, 0f), P(0.5f, 0.75f, 0.5f), W, 0.9f);
        s.BoxAt(P(0f, 0.9f, 0f), P(0.64f, 0.1f, 0.64f), W);
        return 1f;
    }

    public static void Torch(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        var m = M(b, t.DressStone, xf, ref r);
        m.BoxAt(P(0f, -0.05f, 0f), P(0.5f, 0.3f, 0.5f), G(0.9f), 0.6f);
        m = M(b, t.DressWood, xf, ref r);
        m.Cyl(P(0f, 0.25f, 0f), 0.075f, 0.06f, 1.95f, 7, W, false, 0.8f);
        m = M(b, t.DressMetal, xf, ref r, WallFlags.NoTop);
        m.Prism(P(0f, 2.15f, 0f), 0.09f, 0.17f, 0.24f, 6, W, false);
        m = M(b, t.DressMetal, xf, ref r, WallFlags.Glow | WallFlags.NoShadow, Ember);
        m.Prism(P(0f, 2.36f, 0f), 0.15f, 0.15f, 0.03f, 6, W, true);
        float s = xf.Basis.X.Length();
        var fp = xf * P(0f, 2.36f, 0f);
        b.AddFlame(fp, 0.62f * s, 1f, r.F());
        b.AddLight(fp + Vector3.Up * (0.32f * s), t.TorchColor, EnvProps.TorchEnergy, t.TorchRange, 1f, true);
    }

    public static void Brazier(EnvBuild b, Transform3D xf, bool brass, ref EnvRng r)
    {
        var t = b.Theme;
        string metal = brass ? "tarnished_bronze" : t.DressMetal;
        var m = M(b, t.DressStone, xf, ref r);
        m.BoxAt(P(0f, -0.05f, 0f), P(0.95f, 0.5f, 0.95f), G(0.9f), 0.6f);
        m.BoxAt(P(0f, 0.45f, 0f), P(0.7f, 0.12f, 0.7f), W);
        m = M(b, metal, xf, ref r, WallFlags.NoTop);
        m.Cyl(P(0f, 0.57f, 0f), 0.12f, 0.1f, 0.4f, 8, W, false);
        m.Cyl(P(0f, 0.97f, 0f), 0.2f, 0.56f, 0.36f, 12, W, false);
        m.Cyl(P(0f, 1.33f, 0f), 0.56f, 0.58f, 0.06f, 12, W, false);
        m = M(b, metal, xf, ref r, WallFlags.Glow | WallFlags.NoShadow, Ember);
        m.Cyl(P(0f, 1.3f, 0f), 0.52f, 0.52f, 0.05f, 12, W, true);
        float s = xf.Basis.X.Length();
        var fp = xf * P(0f, 1.36f, 0f);
        b.AddFlame(fp, 1f * s, 1.35f, r.F());
        b.AddFlame(fp + xf.Basis.X * 0.16f, 0.7f * s, 1.1f, r.F());
        b.AddLight(fp + Vector3.Up * (0.55f * s), t.TorchColor, EnvProps.TorchEnergy * 1.25f, t.TorchRange * 1.15f, 1f, true);
    }

    /// <summary>A huddle of candles burnt down to different heights in a pool of old wax.</summary>
    public static void Candles(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        int n = 3 + r.Int(4);
        var wax = M(b, "canvas", xf, ref r, WallFlags.NoTop | WallFlags.NoShadow, new Color(0.86f, 0.78f, 0.6f));
        wax.Cyl(P(0f, -0.01f, 0f), 0.46f, 0.38f, 0.03f, 10, G(0.75f), true);
        Vector3 sum = Vector3.Zero;
        for (int i = 0; i < n; i++)
        {
            float a = r.Angle(), d = i == 0 ? 0f : r.Range(0.13f, 0.4f), h = r.Range(0.12f, 0.44f), rad = r.Range(0.035f, 0.055f);
            var foot = P(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            wax.Cyl(foot, rad * 1.15f, rad, h, 7, W, true);
            var fp = xf * (foot + P(0f, h + 0.02f, 0f));
            b.AddFlame(fp, 0.2f, 0.9f, r.F());
            sum += fp;
        }
        b.AddLight(sum / n + Vector3.Up * 0.45f, t.TorchColor, EnvProps.TorchEnergy * 0.36f, t.TorchRange * 0.6f, 1f, false);
    }

    /// <summary>Rocks with something still burning between them: the glow of the lava field carried inland.</summary>
    public static void EmberRocks(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        Rubble(b, xf, false, ref r);
        var hot = new Color(Mathf.Min(t.PitColor.R, 1f), Mathf.Min(t.PitColor.G, 1f), Mathf.Min(t.PitColor.B, 1f));
        var m = M(b, t.DressStone, xf, ref r, WallFlags.Glow | WallFlags.NoShadow, hot);
        int n = 3 + r.Int(3);
        for (int i = 0; i < n; i++)
        {
            float a = r.Angle(), d = r.Range(0f, 0.45f);
            var size = P(r.Range(0.12f, 0.3f), r.Range(0.06f, 0.14f), r.Range(0.12f, 0.3f));
            m.BoxT(new Transform3D(Tumble(ref r, 0.3f), P(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d)), size, G(r.Range(0.6f, 1f)), 1f, true);
        }
        b.AddLight(xf * P(0f, 0.7f, 0f), hot, 1.5f, 6.5f, 0.25f, false);
    }

    /// <summary>A stand of ice that holds a cold light of its own.</summary>
    public static void LitCrystal(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        Crystal(b, xf * new Transform3D(Basis.Identity.Scaled(Vector3.One * 0.6f), Vector3.Zero), "ice_pale", WallFlags.Gloss | WallFlags.Lit, IceTint(t), true, ref r);
        Crystal(b, At(xf, 0.5f, 0f, 0.2f, 1.3f), "ice_pale", WallFlags.Gloss | WallFlags.Lit, IceTint(t), false, ref r);
        b.AddLight(xf * P(0f, 1.1f, 0f), t.Accent, 1.3f, 6.5f, 0.25f, false);
    }

    /// <summary>A clump of fungus whose caps glow in the region's accent colour: light without fire.</summary>
    public static void Glowshroom(EnvBuild b, Transform3D xf, ref EnvRng r)
    {
        var t = b.Theme;
        int n = 4 + r.Int(4);
        var stem = M(b, "bark", xf, ref r, WallFlags.NoTop, new Color(0.9f, 0.9f, 0.8f));
        var replay = r;                                   // the caps repeat the stems' random walk
        for (int i = 0; i < n; i++)
        {
            float a = r.Angle(), d = i == 0 ? 0f : r.Range(0.25f, 0.8f), h = r.Range(0.3f, 0.85f);
            r.F();
            stem.Cyl(P(Mathf.Cos(a) * d, -0.05f, Mathf.Sin(a) * d), 0.045f + h * 0.05f, 0.035f, h + 0.05f, 6, W, false);
        }
        var after = r;
        var cap = M(b, "bark", xf, ref after, WallFlags.Glow | WallFlags.NoShadow, t.Accent);
        for (int i = 0; i < n; i++)
        {
            float a = replay.Angle(), d = i == 0 ? 0f : replay.Range(0.25f, 0.8f), h = replay.Range(0.3f, 0.85f);
            float rad = 0.16f + replay.F() * 0.2f;
            var c = P(Mathf.Cos(a) * d, h - 0.03f, Mathf.Sin(a) * d);
            cap.Cyl(c, rad, rad * 0.6f, rad * 0.22f, 9, G(0.75f), false);
            cap.Cyl(c + P(0f, rad * 0.22f, 0f), rad * 0.6f, 0.03f, rad * 0.3f, 9, W, false);
        }
        r = after;
        b.AddLight(xf * P(0f, 0.95f, 0f), t.Accent, 1.15f, 6.5f, 0.25f, false);
    }
}
