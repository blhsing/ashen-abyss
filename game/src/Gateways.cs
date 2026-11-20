using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// The built gateways of the world: every way out of a level and every way into a dungeon is ONE object, a gate whose
/// rectangular opening is the portal itself (Fx.Veil fills it; there is no round ring anywhere in a gate).
/// A gate is put together from the level's own graded materials (EnvMats.Wall: the theme's stone, timber and metal,
/// with its snow, moss or sand settling on the ledges and the same see-through cut as the walls), in the manner of
/// the region: coursed masonry with a keystone, timber posts with a through beam, braces and iron bands, battered
/// sandstone pylons under a stepped cornice, rimed stone hung with ice, a gabled grave gate, a gothic gate with a
/// pointed gable, clustered basalt columns under an obsidian slab. Each carries an inlay of the portal's own light.
/// Gates are laid out in world axes with the opening across X: the lens looks north from above, so the veil is always
/// seen face-on between the jambs, and the hero walks through it from either side.
/// </summary>
internal static class Gateways
{
    private static readonly Color W = new(1f, 1f, 1f, 1f);
    private static Color G(float v) => new(v, v, v, 1f);
    private static Vector3 P(float x, float y, float z) => new(x, y, z);

    /// <summary>Half the width a gate of this opening takes on the ground, jambs and footings included (metres).</summary>
    public static float HalfSpan(float opening) => opening * 0.5f + 1.0f;

    private sealed class Kit
    {
        public LevelTheme T;
        public EnvRng R;
        public Color Glow;
        public float Wd, H;          // the opening
        public bool Grand;
        public readonly Dictionary<(string Set, int Flags, uint Tint), EnvMesh> Meshes = new();

        public EnvMesh M(string set, WallFlags flags = WallFlags.None, Color? tint = null)
        {
            flags |= WallFlags.Stand;                 // a landmark: indoors the floor cut must not dissolve it to stubs
            var c = tint ?? Colors.White;
            c = new Color(Mathf.Clamp(c.R, 0f, 1f), Mathf.Clamp(c.G, 0f, 1f), Mathf.Clamp(c.B, 0f, 1f), 1f);
            var key = (set ?? "", (int)flags, c.ToRgba32());
            if (!Meshes.TryGetValue(key, out var m)) Meshes[key] = m = new EnvMesh((flags & WallFlags.Tri) == 0);
            m.Xf = Transform3D.Identity;
            m.UvOff = new Vector2(R.Range(0f, 3f), R.Range(0f, 3f));
            return m;
        }

        public string Stone => string.IsNullOrEmpty(T.DressStone) ? "big_blocks" : T.DressStone;
        public string StoneAlt => string.IsNullOrEmpty(T.DressStoneAlt) ? Stone : T.DressStoneAlt;
        public string Wood => string.IsNullOrEmpty(T.DressWood) ? "rough_beam" : T.DressWood;
        public string Metal => string.IsNullOrEmpty(T.DressMetal) ? "rusty_iron" : T.DressMetal;
        /// <summary>Inlay that glows in the portal's colour (runes, seams, glyphs).</summary>
        public EnvMesh Ember => M(Metal, WallFlags.Glow | WallFlags.NoShadow | WallFlags.NoTop, Glow);
    }

    /// <summary>Which manner of gate a theme builds.</summary>
    public static string StyleOf(string themeId) => themeId switch
    {
        "mire" or "cave" or "wetcave" => "timber",
        "sands" or "sandtomb" or "brasshall" => "pylon",
        "frost" or "icehall" or "icecave" => "rimed",
        "barrows" or "crypt" or "graveyard" => "grave",
        "sanctum" or "cathedral" => "gothic",
        "abyss" or "forge" or "lavacave" or "rift" => "basalt",
        _ => "masonry",                 // wastes, town and anything new
    };

    /// <summary>
    /// Builds the gate under <paramref name="parent"/> (whose yaw <paramref name="facing"/> is cancelled) for an opening
    /// of <paramref name="width"/> × <paramref name="height"/> metres standing on the parent's origin.
    /// </summary>
    public static Node3D Build(Node3D parent, float facing, Vector2 at, float width, float height, Color glow, bool grand)
    {
        var lv = Level.Current;
        var t = lv?.Theme ?? LevelThemes.Get("wastes");
        var k = new Kit
        {
            T = t, Glow = glow, Wd = width, H = height, Grand = grand,
            R = EnvRng.For(lv?.Data?.Seed ?? 1, Mathf.RoundToInt(at.X * 8f), Mathf.RoundToInt(at.Y * 8f)),
        };
        string style = StyleOf(t.Id);
        try
        {
            switch (style)
            {
                case "timber": Timber(k); break;
                case "pylon": Pylon(k); break;
                case "rimed": Masonry(k, true, false); Icicles(k); break;
                case "grave": Masonry(k, false, true); break;
                case "gothic": Gothic(k); break;
                case "basalt": Basalt(k); break;
                default: Masonry(k, true, false); break;
            }
        }
        catch (Exception e) { Log.Error("objects", $"building the {style} gateway failed", e); }

        var holder = new Node3D { Name = "gateway_" + style, Rotation = new Vector3(0, -facing, 0) };
        foreach (var (key, mesh) in k.Meshes)
        {
            var am = mesh.Build();
            if (am == null) continue;
            var flags = (WallFlags)key.Flags;
            am.SurfaceSetMaterial(0, EnvMats.Wall(t, key.Set, flags, new Color(key.Tint)));
            holder.AddChild(new MeshInstance3D
            {
                Name = key.Set, Mesh = am,
                CastShadow = (flags & (WallFlags.NoShadow | WallFlags.Glow)) == 0 ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        parent.AddChild(holder);
        return holder;
    }

    // ───────────── pieces ─────────────

    /// <summary>A pier of coursed blocks: alternating widths, each course its own shade, a dark joint between them.</summary>
    private static void Pier(Kit k, string set, float cx, float y0, float width, float depth, float height, int courses)
    {
        var m = k.M(set);
        float ch = height / courses;
        for (int i = 0; i < courses; i++)
        {
            float wv = width * (i % 2 == 0 ? 1f : 0.92f) + k.R.Range(-0.02f, 0.02f);
            float dv = depth * (i % 2 == 0 ? 0.94f : 1f);
            m.BoxAt(P(cx + k.R.Range(-0.02f, 0.02f), y0 + i * ch, k.R.Range(-0.015f, 0.015f)), P(wv, ch - 0.03f, dv), G(k.R.Range(0.84f, 1f)), i == 0 ? 0.6f : 0.92f);
        }
    }

    /// <summary>A line of glowing segments down a face (z = the face it lies on).</summary>
    private static void Seam(Kit k, float x, float y0, float y1, float z, int parts, float w = 0.06f)
    {
        var e = k.Ember;
        float len = (y1 - y0) / parts;
        for (int i = 0; i < parts; i++)
            e.BoxAt(P(x + (i % 2 == 0 ? 0f : 0.035f), y0 + i * len + 0.05f, z), P(w, len - 0.14f, 0.03f), W, 1f, true, true);
    }

    /// <summary>A few blocks that have come down beside the gate (never in the opening or in front of it).</summary>
    private static void Rubble(Kit k, string set, float xOut)
    {
        var m = k.M(set);
        float side = k.R.Chance(0.5f) ? -1f : 1f;
        int n = 2 + k.R.Int(2);
        for (int i = 0; i < n; i++)
        {
            var size = P(k.R.Range(0.3f, 0.52f), k.R.Range(0.22f, 0.36f), k.R.Range(0.3f, 0.5f));
            var basis = new Basis(Vector3.Up, k.R.Angle()) * new Basis(Vector3.Right, k.R.Range(-0.3f, 0.3f));
            m.BoxT(new Transform3D(basis, P(side * (xOut + k.R.Range(0.25f, 0.95f)), -0.04f, k.R.Range(-0.75f, 0.2f))), size, G(k.R.Range(0.78f, 0.95f)), 0.7f, true);
        }
    }

    // ───────────── coursed masonry (wastes, town; rimed in the frost; gabled over the graves) ─────────────

    private static void Masonry(Kit k, bool keystone, bool gable)
    {
        float w = k.Wd, h = k.H, jw = 0.74f, d = 0.86f, xj = w * 0.5f + jw * 0.5f;
        string set = k.Stone, alt = k.StoneAlt;
        k.M(alt).BoxAt(P(0f, -0.05f, 0f), P(w + 2f * jw + 0.4f, 0.11f, d + 0.6f), G(0.9f), 0.7f);                 // threshold slab
        var m = k.M(set);
        foreach (float s in new[] { -1f, 1f })
        {
            m.BoxAt(P(s * xj, 0.03f, 0f), P(jw + 0.24f, 0.3f, d + 0.24f), W, 0.6f);                                // plinth
            Pier(k, set, s * xj, 0.33f, jw, d, h - 0.33f - 0.17f, 5);
            m = k.M(set);
            m.BoxAt(P(s * xj, h - 0.17f, 0f), P(jw + 0.2f, 0.17f, d + 0.2f), W);                                   // impost
            // a buttress stone leaning against the outside of each pier
            m.BoxT(new Transform3D(new Basis(Vector3.Back, s * 0.2f), P(s * (xj + jw * 0.5f + 0.34f), -0.05f, k.R.Range(-0.1f, 0.1f))),
                P(0.5f, k.R.Range(1.15f, 1.5f), d * 0.78f), G(0.9f), 0.6f, true);
            Seam(k, s * xj, 0.75f, h - 0.5f, d * 0.5f + 0.012f, 3);
        }
        float span = w + 2f * jw + 0.36f;
        m.BoxAt(P(0f, h, 0f), P(span, 0.56f, d + 0.06f), G(0.96f));                                                // lintel
        if (gable)
        {
            // a stepped gable with a grave cross: the lintel of a tomb
            m.BoxAt(P(0f, h + 0.56f, 0f), P(span + 0.2f, 0.12f, d + 0.26f), W);
            m.BoxAt(P(0f, h + 0.68f, 0f), P(span * 0.66f, 0.3f, d * 0.82f), G(0.94f));
            m.BoxAt(P(0f, h + 0.98f, 0f), P(span * 0.36f, 0.28f, d * 0.7f), G(0.98f));
            m.BoxAt(P(0f, h + 1.26f, 0f), P(0.16f, 0.62f, 0.16f), W);
            m.BoxAt(P(0f, h + 1.6f, 0f), P(0.5f, 0.13f, 0.15f), W);
            var e = k.Ember;                                                                                      // the cross repeated in light on the gable
            e.BoxAt(P(0f, h + 0.74f, d * 0.41f + 0.012f), P(0.06f, 0.44f, 0.03f), W, 1f, true, true);
            e.BoxAt(P(0f, h + 0.98f, d * 0.41f + 0.012f), P(0.26f, 0.06f, 0.03f), W, 1f, true, true);
            foreach (float s in new[] { -1f, 1f })                                                                 // an urn on either shoulder
            {
                m.Cyl(P(s * (xj + 0.1f), h + 0.68f, 0f), 0.13f, 0.21f, 0.28f, 8, G(0.95f));
                m.Cyl(P(s * (xj + 0.1f), h + 0.96f, 0f), 0.21f, 0.1f, 0.18f, 8, W);
            }
            // two headstones propped against the gate
            var a2 = k.M(alt);
            a2.BoxT(new Transform3D(new Basis(Vector3.Up, 0.3f) * new Basis(Vector3.Right, -0.16f), P(-(xj + 1.15f), -0.05f, 0.25f)), P(0.62f, 1.05f, 0.15f), G(0.9f), 0.6f, true);
            a2.BoxT(new Transform3D(new Basis(Vector3.Up, -0.4f) * new Basis(Vector3.Right, -0.22f), P(xj + 1.2f, -0.05f, 0.1f)), P(0.55f, 0.85f, 0.14f), G(0.85f), 0.6f, true);
        }
        else
        {
            m.BoxAt(P(0f, h + 0.56f, 0f), P(span + 0.26f, 0.13f, d + 0.3f), W);                                    // cornice
            if (k.Grand)
                foreach (float s in new[] { -1f, 1f })
                {
                    m.BoxAt(P(s * xj, h + 0.69f, 0f), P(0.44f, 0.36f, 0.44f), G(0.95f));
                    m.Prism(P(s * xj, h + 1.05f, 0f), 0.3f, 0.03f, 0.4f, 4, W, true, 1f, Mathf.Pi * 0.25f);
                }
        }
        if (keystone)
        {
            m.BoxAt(P(0f, h - 0.09f, 0f), P(0.5f, 0.84f, d + 0.26f), W, 1f, true, true);
            var e = k.Ember;
            float z = (d + 0.26f) * 0.5f + 0.012f;
            e.BoxAt(P(0f, h + 0.08f, z), P(0.07f, 0.46f, 0.03f), W, 1f, true, true);
            e.BoxAt(P(0f, h + 0.36f, z), P(0.26f, 0.06f, 0.03f), W, 1f, true, true);
            e.BoxAt(P(0f, h + 0.14f, z), P(0.18f, 0.06f, 0.03f), W, 1f, true, true);
        }
        Rubble(k, set, xj + jw * 0.5f + 0.5f);
    }

    /// <summary>Ice hanging from the lintel and the imposts of a rimed gate (the snow on its ledges comes with the theme's material).</summary>
    private static void Icicles(Kit k)
    {
        var ice = k.M("ice", WallFlags.Tri | WallFlags.Lit | WallFlags.NoShadow | WallFlags.Gloss | WallFlags.NoTop, new Color(0.5f, 0.72f, 0.95f));
        float w = k.Wd, h = k.H, span = w + 2f * 0.74f + 0.5f;
        int n = 9 + k.R.Int(4);
        for (int i = 0; i < n; i++)
        {
            float x = Mathf.Lerp(-span * 0.5f, span * 0.5f, (i + k.R.Range(0.2f, 0.8f)) / n);
            bool overOpening = Mathf.Abs(x) < w * 0.5f;
            float len = k.R.Range(0.18f, overOpening ? 0.42f : 0.75f);
            float z = (k.R.Chance(0.75f) ? 1f : -1f) * 0.48f;
            ice.Cyl(P(x, h + 0.56f - len, z), 0.01f, k.R.Range(0.035f, 0.06f), len, 5, W, false);
        }
    }

    // ───────────── timber (mire, root caves) ─────────────

    private static void Timber(Kit k)
    {
        float w = k.Wd, h = k.H, xj = w * 0.5f + 0.24f;
        var stone = k.M(k.Stone);
        var wood = k.M(k.Wood);
        var iron = k.M(k.Metal, WallFlags.NoTop);
        k.M(k.T.DressPlank ?? k.Wood).BoxAt(P(0f, -0.05f, 0f), P(w + 0.2f, 0.1f, 1.5f), G(0.85f), 0.7f);            // a boarded threshold
        foreach (float s in new[] { -1f, 1f })
        {
            stone.BoxAt(P(s * xj, -0.05f, 0f), P(0.82f, 0.32f, 0.82f), G(0.9f), 0.6f);                             // footing stone
            wood.Cyl(P(s * xj, 0.22f, 0f), 0.25f, 0.2f, h + 0.72f, 8, W, true, 0.6f);                              // post, standing proud of the beam
            foreach (float y in new[] { 0.62f, 1.7f, h - 0.3f })
                iron.BoxAt(P(s * xj, y, 0f), P(0.54f, 0.08f, 0.54f), G(0.9f));                                     // iron bands
            // corner brace into the beam, and the peg that pins the joint
            wood.BoxT(new Transform3D(new Basis(Vector3.Back, s * 0.8f), P(s * (w * 0.5f - 0.01f), h - 0.66f, 0f)), P(0.15f, 0.86f, 0.17f), G(0.9f), 1f, true);
            wood.BoxAt(P(s * xj, h + 0.12f, 0f), P(0.07f, 0.07f, 0.62f), G(0.6f), 1f, true, true);
            // a shorter stake outside, what is left of the fence this gate stood in
            wood.Cyl(P(s * (xj + 0.78f), -0.05f, k.R.Range(-0.12f, 0.2f)), 0.14f, 0.07f, k.R.Range(1.4f, 1.95f), 6, G(0.88f), true, 0.6f);
            iron.BoxAt(P(s * (xj + 0.4f), 1.05f, 0f), P(0.86f, 0.07f, 0.1f), G(0.85f));                            // the strap that ties it to the post
        }
        wood.BoxAt(P(0f, h, 0f), P(w + 2f * 0.24f + 1.25f, 0.36f, 0.34f), W, 1f, true, true);                      // the through beam, tenons standing out past the posts
        wood.BoxAt(P(0f, h + 0.5f, 0f), P(w + 0.2f, 0.2f, 0.26f), G(0.93f), 1f, true, true);                       // the tie above it
        wood.BoxAt(P(0f, h + 0.36f, 0f), P(0.2f, 0.14f, 0.3f), G(0.85f));                                          // king block between the two
        // an iron plate on the beam with the glyph of the way cut through it
        float zf = 0.34f * 0.5f + 0.022f;
        iron.BoxAt(P(0f, h + 0.02f, zf - 0.01f), P(0.56f, 0.32f, 0.04f), G(0.95f), 1f, true, true);
        var e = k.Ember;
        e.BoxAt(P(0f, h + 0.06f, zf + 0.012f), P(0.07f, 0.24f, 0.02f), W, 1f, true, true);
        e.BoxAt(P(-0.14f, h + 0.1f, zf + 0.012f), P(0.06f, 0.14f, 0.02f), W, 1f, true, true);
        e.BoxAt(P(0.14f, h + 0.1f, zf + 0.012f), P(0.06f, 0.14f, 0.02f), W, 1f, true, true);
        foreach (float s in new[] { -1f, 1f }) Seam(k, s * xj, 0.8f, 1.55f, 0.25f, 1, 0.05f);                      // a brand burnt into each post
    }

    // ───────────── battered sandstone pylons (sands, tombs, the brass halls) ─────────────

    private static void Pylon(Kit k)
    {
        float w = k.Wd, h = k.H, jw = 0.8f, d = 0.95f, xj = w * 0.5f + jw * 0.5f;
        string set = k.Stone, alt = k.StoneAlt;
        k.M(alt).BoxAt(P(0f, -0.05f, 0f), P(w + 2f * jw + 0.9f, 0.12f, d + 0.9f), G(0.9f), 0.7f);                  // paved threshold
        var m = k.M(set);
        float third = h / 3f;
        foreach (float s in new[] { -1f, 1f })
        {
            // battered: every drum steps in towards the top, the inner face stays plumb with the opening
            for (int i = 0; i < 3; i++)
            {
                float extra = (2 - i) * 0.16f;
                m.BoxAt(P(s * (xj + extra * 0.5f), 0.07f + i * third, 0f), P(jw + extra, third - (i == 2 ? 0.07f : 0.03f), d + extra), G(1f - i * 0.03f), i == 0 ? 0.6f : 0.94f);
            }
            Seam(k, s * xj, 0.6f, h - 0.4f, d * 0.5f + 0.012f, 4, 0.07f);
            // a small obelisk before each pylon, outside the width of the opening
            float ox = s * (xj + jw * 0.5f + 0.95f);
            m.BoxAt(P(ox, -0.05f, 0.55f), P(0.56f, 0.2f, 0.56f), G(0.9f), 0.6f);
            m.Prism(P(ox, 0.15f, 0.55f), 0.3f, 0.17f, 1.75f, 4, W, true, 0.9f, Mathf.Pi * 0.25f);
            m.Prism(P(ox, 1.9f, 0.55f), 0.17f, 0.015f, 0.3f, 4, W, true, 1f, Mathf.Pi * 0.25f);
        }
        float span = w + 2f * jw + 0.1f;
        m.BoxAt(P(0f, h, 0f), P(span, 0.5f, d), G(0.97f));                                                         // lintel
        m.BoxAt(P(0f, h + 0.5f, 0f), P(span + 0.24f, 0.12f, d + 0.2f), W);                                         // the cornice flares in three steps
        m.BoxAt(P(0f, h + 0.62f, 0f), P(span + 0.5f, 0.12f, d + 0.4f), G(0.97f));
        m.BoxAt(P(0f, h + 0.74f, 0f), P(span + 0.78f, 0.1f, d + 0.58f), W);
        var brass = k.M(k.Metal, WallFlags.NoTop);
        brass.BoxAt(P(0f, h + 0.39f, d * 0.5f + 0.012f), P(span - 0.1f, 0.07f, 0.03f), W, 1f, true, true);           // a band of metal under the cornice
        brass.BoxAt(P(0f, h + 0.04f, d * 0.5f + 0.012f), P(0.82f, 0.3f, 0.03f), G(0.9f), 1f, true, true);          // cartouche
        var e = k.Ember;
        e.BoxAt(P(0f, h + 0.1f, d * 0.5f + 0.03f), P(0.56f, 0.06f, 0.02f), W, 1f, true, true);
        e.BoxAt(P(-0.2f, h + 0.18f, d * 0.5f + 0.03f), P(0.07f, 0.1f, 0.02f), W, 1f, true, true);
        e.BoxAt(P(0.2f, h + 0.18f, d * 0.5f + 0.03f), P(0.07f, 0.1f, 0.02f), W, 1f, true, true);
        Rubble(k, set, xj + jw * 0.5f + 1.3f);
    }

    // ───────────── gothic (the holy city and its cathedral) ─────────────

    private static void Gothic(Kit k)
    {
        float w = k.Wd, h = k.H, d = 0.74f, xj = w * 0.5f + 0.27f;
        string set = k.Stone;
        var m = k.M(set);
        var gold = k.M(k.Metal, WallFlags.NoTop | WallFlags.Gloss);
        k.M(k.StoneAlt).BoxAt(P(0f, -0.05f, 0f), P(w + 2.1f, 0.11f, d + 0.8f), G(0.92f), 0.7f);
        foreach (float s in new[] { -1f, 1f })
        {
            m.BoxAt(P(s * xj, 0.03f, 0f), P(0.86f, 0.3f, d + 0.3f), W, 0.6f);                                      // moulded base, in two steps
            m.BoxAt(P(s * xj, 0.33f, 0f), P(0.72f, 0.12f, d + 0.16f), G(0.96f));
            m.BoxAt(P(s * xj, 0.45f, 0f), P(0.5f, h - 0.45f - 0.28f, d), G(0.95f), 0.95f);                         // the core of the jamb
            foreach (float e2 in new[] { -1f, 1f })                                                                // engaged shafts on its four corners
                foreach (float f in new[] { -1f, 1f })
                    m.Cyl(P(s * xj + e2 * 0.24f, 0.45f, f * (d * 0.5f - 0.02f)), 0.1f, 0.1f, h - 0.45f - 0.28f, 8, W, false, 0.9f);
            m.BoxAt(P(s * xj, h - 0.28f, 0f), P(0.8f, 0.12f, d + 0.24f), W);                                       // capital
            m.BoxAt(P(s * xj, h - 0.16f, 0f), P(0.92f, 0.16f, d + 0.34f), G(0.97f));
            gold.BoxAt(P(s * xj, h - 0.34f, 0f), P(0.76f, 0.06f, d + 0.2f), W);                                    // a gilt ring under it
            m.Prism(P(s * xj, h + 0.42f, 0f), 0.26f, 0.2f, 0.5f, 4, G(0.96f), true, 1f, Mathf.Pi * 0.25f);         // pinnacle
            m.Prism(P(s * xj, h + 0.92f, 0f), 0.2f, 0.02f, 0.7f, 4, W, true, 1f, Mathf.Pi * 0.25f);
            Seam(k, s * xj, 0.8f, h - 0.6f, d * 0.5f + 0.012f, 3, 0.05f);
        }
        float span = w + 2f * 0.5f + 0.5f;
        m.BoxAt(P(0f, h, 0f), P(span, 0.42f, d + 0.1f), G(0.97f));                                                 // the flat head of the opening
        // the pointed gable above it: two raking stones meeting at the apex, a field of stone behind them
        float half = w * 0.5f + 0.2f, ang = 1.02f, len = half / Mathf.Sin(ang), rise = len * Mathf.Cos(ang);
        foreach (float s in new[] { -1f, 1f })
            m.BoxT(new Transform3D(new Basis(Vector3.Back, s * ang), P(s * half, h + 0.42f, 0f)), P(0.24f, len + 0.08f, d * 0.92f), W, 1f, true);
        m.BoxAt(P(0f, h + 0.42f, 0f), P(half * 1.2f, rise * 0.5f, d * 0.6f), G(0.9f));
        m.BoxAt(P(0f, h + 0.42f + rise * 0.5f, 0f), P(half * 0.55f, rise * 0.3f, d * 0.6f), G(0.9f));
        gold.Prism(P(0f, h + 0.42f + rise, 0f), 0.13f, 0.02f, 0.62f, 4, W, true, 1f, Mathf.Pi * 0.25f);            // gilt finial
        // the lamp of the order in the gable: a gilt lozenge with the portal's light at its heart
        float zf = d * 0.3f + 0.012f;
        gold.BoxT(new Transform3D(new Basis(Vector3.Back, Mathf.Pi * 0.25f), P(0.26f, h + 0.5f, zf)), P(0.36f, 0.36f, 0.04f), W, 1f, true);
        k.Ember.BoxT(new Transform3D(new Basis(Vector3.Back, Mathf.Pi * 0.25f), P(0.13f, h + 0.63f, zf + 0.03f)), P(0.18f, 0.18f, 0.03f), W, 1f, true);
    }

    // ───────────── basalt and obsidian (the abyss, the forge, the lava caves, the rift) ─────────────

    private static void Basalt(Kit k)
    {
        float w = k.Wd, h = k.H;
        string set = k.Stone;
        var m = k.M(set, WallFlags.Tri);
        var glass = k.M("obsidian_slabs", WallFlags.Gloss | WallFlags.NoTop);
        k.M(k.StoneAlt).BoxAt(P(0f, -0.05f, 0f), P(w + 2.6f, 0.1f, 1.7f), G(0.85f), 0.7f);
        foreach (float s in new[] { -1f, 1f })
        {
            // a cluster of six-sided columns of unequal height; the innermost one is the jamb
            float x0 = s * (w * 0.5f + 0.33f);
            m.Prism(P(x0, -0.05f, 0f), 0.36f, 0.34f, h + 0.42f, 6, W, true, 0.6f, 0.2f);
            m.Prism(P(x0 + s * 0.52f, -0.05f, 0.3f), 0.31f, 0.29f, h * k.R.Range(0.66f, 0.8f), 6, G(0.92f), true, 0.6f, 0.7f);
            m.Prism(P(x0 + s * 0.5f, -0.05f, -0.33f), 0.33f, 0.31f, h + k.R.Range(0.75f, 1.15f), 6, G(0.96f), true, 0.6f, 0.45f);
            m.Prism(P(x0 + s * 1.02f, -0.05f, 0.04f), 0.28f, 0.26f, k.R.Range(1.0f, 1.5f), 6, G(0.88f), true, 0.6f, 0.1f);
            m.Prism(P(x0 + s * 0.98f, -0.05f, -0.5f), 0.26f, 0.24f, k.R.Range(1.9f, 2.5f), 6, G(0.9f), true, 0.6f, 0.9f);
            // ember in the joint between the columns, climbing in broken runs
            Seam(k, x0 + s * 0.02f, 0.5f, h - 0.3f, 0.335f, 4, 0.055f);
            Seam(k, x0 + s * 0.3f, 0.3f, h * 0.55f, 0.47f, 2, 0.045f);
        }
        float span = w + 1.5f;
        glass.BoxAt(P(0f, h, 0f), P(span, 0.52f, 0.82f), G(0.95f), 1f, true, true);                                // the slab laid across
        var top = k.M(set, WallFlags.Tri);
        top.Prism(P(-0.62f, h + 0.52f, 0.02f), 0.24f, 0.22f, 0.34f, 6, G(0.95f), true, 1f, 0.3f);                  // a crown of stubs
        top.Prism(P(0.02f, h + 0.52f, -0.05f), 0.27f, 0.25f, 0.62f, 6, W, true, 1f, 0.6f);
        top.Prism(P(0.64f, h + 0.52f, 0.04f), 0.22f, 0.2f, 0.42f, 6, G(0.92f), true, 1f, 0.1f);
        var e = k.Ember;                                                                                          // a crack of fire across the face of the slab
        float zf = 0.41f + 0.012f;
        e.BoxAt(P(-0.5f, h + 0.3f, zf), P(0.7f, 0.05f, 0.03f), W, 1f, true, true);
        e.BoxAt(P(-0.13f, h + 0.18f, zf), P(0.05f, 0.16f, 0.03f), W, 1f, true, true);
        e.BoxAt(P(0.22f, h + 0.16f, zf), P(0.72f, 0.05f, 0.03f), W, 1f, true, true);
        e.BoxAt(P(0.58f, h + 0.2f, zf), P(0.05f, 0.2f, 0.03f), W, 1f, true, true);
    }
}
