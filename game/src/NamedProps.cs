using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// The things named encounters stand on the field: the passive "monster" that makes a prop attackable, and small
/// looks built from shared primitive meshes (banner, pylon, crystal, urn, coffin, brazier, jar, bell, mirror …).
/// Self-contained on purpose: no new model files, nothing from the effect or environment kits beyond their public calls.
/// </summary>
public static class NamedProps
{
    private static readonly Dictionary<int, MonsterDef> Defs = new();

    /// <summary>The definition behind every destructible prop: it never moves or attacks; its character model is hidden.</summary>
    public static MonsterDef Def(float radius)
    {
        int key = Mathf.RoundToInt(radius * 10f);
        if (Defs.TryGetValue(key, out var d)) return d;
        d = new MonsterDef
        {
            Id = "named_prop", Name = "", Model = "mon_husk", Family = "prop", Sfx = "construct", Role = "prop", AI = MonAI.Melee,
            Hp = 1f, Dmg = 0f, Xp = 0f, Speed = 0f, Range = 0.1f, Radius = radius, Poise = 99f, Cooldown = 99f, Scale = 1f,
        };
        Defs[key] = d;
        return d;
    }

    // ───────────────────────── meshes and materials ─────────────────────────

    private static BoxMesh _box; private static CylinderMesh _cyl, _cone; private static SphereMesh _sphere, _gem; private static TorusMesh _torus;
    public static BoxMesh Box => _box ??= new BoxMesh { Size = Vector3.One };
    public static CylinderMesh Cyl => _cyl ??= new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 14, Rings = 1 };
    public static CylinderMesh Cone => _cone ??= new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 1 };
    public static SphereMesh Sphere => _sphere ??= new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 14, Rings = 7 };
    public static SphereMesh Gem => _gem ??= new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 4, Rings = 1 };
    public static TorusMesh Torus => _torus ??= new TorusMesh { InnerRadius = 0.9f, OuterRadius = 1f, Rings = 40, RingSegments = 6 };

    public static readonly Color Stone = new(0.36f, 0.35f, 0.34f), Dark = new(0.14f, 0.13f, 0.15f), Wood = new(0.33f, 0.22f, 0.13f), Iron = new(0.2f, 0.2f, 0.22f),
        Brass = new(0.72f, 0.55f, 0.24f), Bone = new(0.8f, 0.76f, 0.66f), Ice = new(0.6f, 0.85f, 1f), Warm = new(1f, 0.62f, 0.3f), Hush = new(0.62f, 0.3f, 0.95f),
        Pale = new(0.92f, 0.94f, 1f), Bog = new(0.4f, 0.55f, 0.25f), Storm = new(0.75f, 0.8f, 1f);

    private static Material Mat(Color c, float rough = 0.85f, float metal = 0f) => WorldObjects.Flat(c, rough, metal);
    private static Material Lit(Color c, float e = 3f) => WorldObjects.Glow(c, e);
    private static MeshInstance3D P(Node3D n, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 rot = default, bool shadow = true) =>
        WorldObjects.Part(n, mesh, mat, pos, scale, rot, shadow);
    private static Node3D N(string name) => new() { Name = name };

    // ───────────────────────── looks ─────────────────────────

    /// <summary>A standard on a pole (garrison banner, chandlers' colours).</summary>
    public static Node3D Banner(Color cloth)
    {
        var n = N("banner");
        P(n, Cyl, Mat(Wood), new Vector3(0, 1.6f, 0), new Vector3(0.12f, 3.2f, 0.12f));
        P(n, Box, Mat(cloth, 0.95f), new Vector3(0.5f, 2.4f, 0), new Vector3(0.9f, 1.3f, 0.05f));
        P(n, Gem, Lit(cloth, 2f), new Vector3(0, 3.3f, 0), Vector3.One * 0.28f, default, false);
        WorldObjects.Light(n, cloth, 1.1f, 6f, 2.6f);
        return n;
    }

    /// <summary>A storm pylon / lightning rod: a metal post with a glowing head.</summary>
    public static Node3D Pylon(Color glow, float height = 3.2f)
    {
        var n = N("pylon");
        P(n, Cyl, Mat(Brass, 0.45f, 0.85f), new Vector3(0, height * 0.5f, 0), new Vector3(0.34f, height, 0.34f));
        P(n, Cyl, Mat(Iron, 0.5f, 0.8f), new Vector3(0, 0.12f, 0), new Vector3(1.0f, 0.24f, 1.0f));
        P(n, Sphere, Lit(glow, 4f), new Vector3(0, height + 0.25f, 0), Vector3.One * 0.55f, default, false);
        WorldObjects.Light(n, glow, 1.6f, 8f, height);
        return n;
    }

    /// <summary>A shard of ember crystal / ice / void, pointing up.</summary>
    public static Node3D Crystal(Color c, float height = 1.6f)
    {
        var n = N("crystal");
        P(n, Gem, Lit(c, 2.6f), new Vector3(0, height * 0.5f, 0), new Vector3(height * 0.42f, height, height * 0.42f), default, false);
        P(n, Gem, Lit(c, 1.6f), new Vector3(0.35f, height * 0.28f, 0.1f), new Vector3(height * 0.22f, height * 0.5f, height * 0.22f), new Vector3(0, 0, -22), false);
        WorldObjects.Light(n, c, 1.2f, 6f, height * 0.7f);
        return n;
    }

    public static Node3D Urn(Color glow)
    {
        var n = N("urn");
        P(n, Sphere, Mat(Dark, 0.5f), new Vector3(0, 0.7f, 0), new Vector3(1.0f, 1.3f, 1.0f));
        P(n, Cyl, Mat(Dark, 0.5f), new Vector3(0, 1.45f, 0), new Vector3(0.45f, 0.3f, 0.45f));
        P(n, Sphere, Lit(glow, 3f), new Vector3(0, 1.75f, 0), Vector3.One * 0.36f, default, false);
        WorldObjects.Light(n, glow, 1f, 5f, 1.8f);
        return n;
    }

    public static Node3D Coffin(bool upright = true)
    {
        var n = N("coffin");
        if (upright) P(n, Box, Mat(Wood), new Vector3(0, 1.0f, 0), new Vector3(0.8f, 2.0f, 0.45f), new Vector3(8, 0, 0));
        else P(n, Box, Mat(Wood), new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.5f, 2.0f));
        P(n, Box, Mat(Iron, 0.6f, 0.7f), new Vector3(0, upright ? 1.0f : 0.56f, upright ? 0.24f : 0), upright ? new Vector3(0.84f, 0.1f, 0.06f) : new Vector3(0.84f, 0.04f, 0.1f));
        return n;
    }

    /// <summary>A fire bowl on a stand. The child "fire" holds flame and light; <see cref="SetFire"/> switches it.</summary>
    public static Node3D Brazier(bool lit, float size = 1f, Color? flame = null)
    {
        var n = N("brazier");
        P(n, Cyl, Mat(Stone), new Vector3(0, 0.12f, 0), new Vector3(1.5f, 0.24f, 1.5f) * size);
        P(n, Cyl, Mat(Iron, 0.5f, 0.85f), new Vector3(0, 0.6f * size, 0), new Vector3(0.3f, 1.0f, 0.3f) * size);
        P(n, Cone, Mat(Iron, 0.5f, 0.85f), new Vector3(0, 1.2f * size, 0), new Vector3(1.3f, 0.4f, 1.3f) * size, new Vector3(180, 0, 0));
        var fire = N("fire");
        fire.Position = new Vector3(0, 1.35f * size, 0);
        n.AddChild(fire);
        var f = Fx.Fire(1.1f * size, false);
        if (f != null) fire.AddChild(f);
        WorldObjects.Light(fire, flame ?? Warm, 2.4f, 12f * size, 0.6f);
        fire.Visible = lit;
        return n;
    }

    public static void SetFire(Node3D brazier, bool lit)
    {
        if (brazier == null || !GodotObject.IsInstanceValid(brazier)) return;
        var fire = brazier.GetNodeOrNull<Node3D>("fire");
        if (fire == null || fire.Visible == lit) return;
        fire.Visible = lit;
        if (brazier.IsInsideTree())
        {
            if (lit) { Snd.At("beacon_light", brazier.GlobalPosition, 0.7f); Fx.Burst(brazier.GlobalPosition + Vector3.Up * 1.3f, Element.Fire, 2f, 0.6f); }
            else Fx.Dust(brazier.GlobalPosition + Vector3.Up * 1.2f);
        }
    }

    /// <summary>A storm jar / keg / hearth-pot: a squat vessel with a glowing band.</summary>
    public static Node3D Jar(Color glow, float size = 1f)
    {
        var n = N("jar");
        P(n, Sphere, Mat(Brass, 0.5f, 0.7f), new Vector3(0, 0.6f * size, 0), new Vector3(0.9f, 1.1f, 0.9f) * size);
        P(n, Torus, Lit(glow, 3f), new Vector3(0, 0.6f * size, 0), new Vector3(0.5f, 1f, 0.5f) * size, default, false);
        WorldObjects.Light(n, glow, 1f, 5f, 0.9f * size);
        return n;
    }

    /// <summary>A stack of bones / planks / a drive-gear housing: a low heap.</summary>
    public static Node3D Heap(Color c, float size = 1f)
    {
        var n = N("heap");
        P(n, Box, Mat(c), new Vector3(0, 0.3f * size, 0), new Vector3(1.3f, 0.6f, 1.0f) * size, new Vector3(0, 18, 0));
        P(n, Box, Mat(c * 0.85f), new Vector3(0.1f, 0.8f * size, 0), new Vector3(1.0f, 0.5f, 0.8f) * size, new Vector3(0, -12, 0));
        P(n, Box, Mat(c * 1.1f), new Vector3(-0.1f, 1.2f * size, 0.05f), new Vector3(0.7f, 0.4f, 0.6f) * size, new Vector3(0, 30, 0));
        return n;
    }

    public static Node3D Gear(Color glow)
    {
        var n = N("gear");
        P(n, Cyl, Mat(Brass, 0.45f, 0.85f), new Vector3(0, 0.9f, 0), new Vector3(1.6f, 0.3f, 1.6f), new Vector3(90, 0, 0));
        P(n, Box, Mat(Iron, 0.5f, 0.8f), new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.9f, 0.5f));
        P(n, Sphere, Lit(glow, 3f), new Vector3(0, 0.9f, 0.2f), Vector3.One * 0.4f, default, false);
        return n;
    }

    /// <summary>A bell clapper / stake planted upright in the floor.</summary>
    public static Node3D Stake(Color c, float height = 2f, Color? glow = null)
    {
        var n = N("stake");
        P(n, Cyl, Mat(c, 0.5f, 0.6f), new Vector3(0, height * 0.5f, 0), new Vector3(0.22f, height, 0.22f));
        P(n, Sphere, glow.HasValue ? Lit(glow.Value, 2.5f) : Mat(c, 0.4f, 0.8f), new Vector3(0, height, 0), Vector3.One * 0.5f, default, false);
        return n;
    }

    /// <summary>The snuffer cone planted mouth-down.</summary>
    public static Node3D Snuffer(Color glow)
    {
        var n = N("snuffer");
        P(n, Cone, Mat(Brass, 0.4f, 0.9f), new Vector3(0, 0.7f, 0), new Vector3(1.1f, 1.4f, 1.1f));
        P(n, Torus, Lit(glow, 2.2f), new Vector3(0, 0.06f, 0), new Vector3(0.7f, 1f, 0.7f), default, false);
        return n;
    }

    /// <summary>A slab standing on edge: mirror segment, headstone, ice pillar, plank plate.</summary>
    public static Node3D Slab(Color c, float width, float height, float thick = 0.3f, bool shiny = false)
    {
        var n = N("slab");
        P(n, Box, Mat(c, shiny ? 0.15f : 0.85f, shiny ? 0.9f : 0f), new Vector3(0, height * 0.5f, 0), new Vector3(width, height, thick));
        return n;
    }

    public static Node3D Column(Color c, float radius, float height, bool glow = false)
    {
        var n = N("column");
        P(n, Cyl, glow ? Lit(c, 1.4f) : Mat(c, 0.6f), new Vector3(0, height * 0.5f, 0), new Vector3(radius * 2f, height, radius * 2f));
        return n;
    }

    /// <summary>A hovering mote: frozen flicker, storm-jar spark, ember.</summary>
    public static Node3D Orb(Color c, float size = 0.5f, float y = 1.2f)
    {
        var n = N("orb");
        P(n, Sphere, Lit(c, 4f), new Vector3(0, y, 0), Vector3.One * size, default, false);
        WorldObjects.Light(n, c, 0.9f, 4f, y);
        return n;
    }

    /// <summary>A flat disc on the ground: pit, shadow wedge marker, mud patch, web.</summary>
    public static Node3D Patch(float radius, Color c, bool glow = false)
    {
        var n = N("patch");
        P(n, Cyl, glow ? Lit(c, 1.2f) : Mat(c, 1f), new Vector3(0, 0.03f, 0), new Vector3(radius * 2f, 0.05f, radius * 2f), default, false);
        return n;
    }

    /// <summary>A glowing ring on the ground (halo, hush zone, tide line, safe circle).</summary>
    public static Node3D Ring(float radius, Color c, float energy = 2.2f)
    {
        var n = N("ring");
        P(n, Torus, Lit(c, energy), new Vector3(0, 0.07f, 0), new Vector3(radius, 1f, radius), default, false);
        return n;
    }

    public static Node3D Cage()
    {
        var n = N("cage");
        var iron = Mat(Iron, 0.55f, 0.8f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.Tau / 6f;
            P(n, Cyl, iron, new Vector3(Mathf.Cos(a) * 0.7f, 1.1f, Mathf.Sin(a) * 0.7f), new Vector3(0.08f, 2.2f, 0.08f));
        }
        P(n, Cyl, iron, new Vector3(0, 2.2f, 0), new Vector3(1.6f, 0.1f, 1.6f));
        return n;
    }

    /// <summary>Puts a free-standing visual into the level at a spot (not attackable). The caller owns the node.</summary>
    public static Node3D Place(Node3D visual, Vector2 at, float yaw = 0f)
    {
        if (visual == null || World.Root == null) return visual;
        visual.Position = World.To3(at);
        visual.Rotation = new Vector3(0, yaw, 0);
        World.Root.AddChild(visual);
        return visual;
    }
}
