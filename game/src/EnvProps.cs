using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AshenAbyss;

/// <summary>One mesh node of a prop model with its transform relative to the model root and its original materials.</summary>
internal sealed class PropPart
{
    public Mesh Mesh;
    public Transform3D Local = Transform3D.Identity;
    public Material[] Src;
}

/// <summary>A loaded prop model: the scene for one-off instances plus its mesh parts for MultiMesh batching.</summary>
internal sealed class PropDef
{
    public string Name;
    public PackedScene Scene;
    public readonly List<PropPart> Parts = new();
    public Aabb Bounds;
    public int Tris;
    public bool Natural, Plant;
    public Vector3[] Flames;
    public int Stamp = -1;                      // theme generation its materials were converted for
    public float Height => Bounds.End.Y;
    public float Foot => Mathf.Max(Bounds.Size.X, Bounds.Size.Z) * 0.5f;
}

internal readonly record struct LightKind(float Scale, int Stand, float Flame, float Energy, float Range, bool Shadow);

/// <summary>
/// Prop models of the environment: loading and caching, conversion of their glTF materials to the env_prop shader
/// (theme grading, settled snow or ash, see-through), and the placement of LevelData prop slots.
/// </summary>
internal static class EnvProps
{
    /// <summary>Within this many metres of its own light a light prop answers as if the light stood this far away (env_prop: flame_guard).</summary>
    public const float FlameGuard = 2.2f;
    /// <summary>Energy of a standard torch (OmniLight3D); individual light kinds scale it.</summary>
    public const float TorchEnergy = 3.4f;

    private static readonly Dictionary<string, PropDef> Defs = new();
    private static readonly Dictionary<(ulong, int), Material> Converted = new();
    private static LevelTheme _theme;
    private static int _stamp;
    private static Dictionary<string, Vector3[]> _manifestFlames;

    // flame points (local, metres) of the light props, measured when the models were built
    private static readonly Dictionary<string, Vector3[]> FlamePoints = new()
    {
        ["lamp_post"] = new[] { new Vector3(0f, 3.264f, 0f) },
        ["lantern_wood"] = new[] { new Vector3(0.001f, 0.186f, -0.006f) },
        ["lantern_oil"] = new[] { new Vector3(0f, 0.095f, 0f) },
        ["fire_barrel"] = new[] { new Vector3(0f, 0.757f, 0f) },
        ["fire_pit"] = new[] { new Vector3(0f, 0.3f, 0f) },
        ["candlestick"] = new[] { new Vector3(0f, 0.24f, 0f) },
        ["candle_brass"] = new[] { new Vector3(0f, 0.354f, 0f) },
        ["candelabra_3"] = new[] { new Vector3(0f, 0.389f, 0f), new Vector3(-0.106f, 0.371f, 0f), new Vector3(0.106f, 0.37f, 0f) },
        ["candelabra_5"] = new[]
        {
            new Vector3(0.001f, 0.818f, 0f), new Vector3(0.172f, 0.734f, 0f), new Vector3(-0.172f, 0.734f, 0f), new Vector3(0.088f, 0.739f, 0.149f),
            new Vector3(-0.088f, 0.739f, -0.149f), new Vector3(0.086f, 0.739f, -0.151f), new Vector3(-0.088f, 0.741f, 0.147f),
        },
        ["wall_lantern"] = new[] { new Vector3(0f, 0.674f, 0.614f) },
        ["hanging_lamp"] = new[] { new Vector3(0.001f, -0.318f, 0.002f) },
        ["chandelier_iron"] = new[] { new Vector3(-0.004f, -0.528f, -0.001f) },
    };

    // scale, stand (0 none, 1 stone plinth, 2 wooden post), flame height (m), energy ×, range ×, may cast shadows
    private static readonly Dictionary<string, LightKind> LightKinds = new()
    {
        ["lamp_post"] = new(1.05f, 0, 0.38f, 1f, 1.1f, true),
        ["lantern_wood"] = new(1.9f, 2, 0.2f, 0.75f, 0.85f, true),
        ["lantern_oil"] = new(2.4f, 2, 0.16f, 0.65f, 0.8f, false),
        ["fire_barrel"] = new(1.25f, 0, 0.75f, 1.05f, 1f, true),
        ["fire_pit"] = new(1.2f, 0, 0.95f, 1.15f, 1.1f, true),
        ["candelabra_5"] = new(1.7f, 1, 0.16f, 0.85f, 0.9f, true),
        ["candelabra_3"] = new(2.2f, 1, 0.14f, 0.7f, 0.8f, false),
        ["candle_brass"] = new(2.2f, 1, 0.14f, 0.5f, 0.65f, false),
        ["candlestick"] = new(2.4f, 1, 0.14f, 0.5f, 0.65f, false),
    };

    // props that are tiny in the real world get enlarged so they still read from 24 m away
    private static readonly (string prefix, float scale)[] Scales =
    {
        ("candelabra_5", 1.7f), ("candelabra_3", 2.2f), ("candle", 2.2f), ("lantern_oil", 2.4f), ("lantern_wood", 1.9f),
        ("reed", 3.2f), ("fern", 2.1f), ("swamp_plant", 1.7f), ("broadleaf", 1.9f), ("dry_bush", 1.8f), ("dead_bush", 1.6f),
        ("urn", 1.7f), ("jug", 1.7f), ("brass_urn", 1.5f), ("brass_ewer", 1.6f), ("vase", 1.6f), ("clay_pot", 1.9f), ("lidded_pot", 1.3f),
        ("goblets", 1.7f), ("bowl", 1.9f), ("books_row", 1.7f), ("journal", 2f), ("basket", 2.2f), ("brass_basin", 2.2f), ("cauldron", 2.4f),
        ("brass_pan", 1.8f), ("brass_mug", 2.6f), ("brass_vial", 2.6f), ("footstool", 2f), ("rock_small", 2.1f), ("dead_stub", 1.9f),
        ("root_piece", 1.5f), ("branch", 1.5f), ("bust", 2.3f), ("dagger", 1.8f), ("smith_hammer", 2.1f), ("vice", 1.6f), ("saw", 1.5f),
        ("barrel_stave", 1.3f), ("barrel_lid", 1.3f), ("tub", 1.4f), ("bucket", 1.35f), ("statue_king", 1.9f), ("quiver_tree_b", 1.7f),
        ("quiver_tree_a", 1.45f), ("dead_quiver", 1.5f), ("dead_trunk", 1.7f), ("dead_pine", 1.3f), ("crate", 1.4f), ("mace", 1.4f),
        ("war_hammer", 1.4f), ("axe", 1.5f), ("spade", 1.25f), ("pickaxe", 1.25f), ("shard", 1.3f), ("basalt_boulder_b", 1.4f),
        ("roots_flat", 1.3f), ("mirror", 1.5f), ("trough", 1.3f), ("camp_stool", 1.3f), ("stool", 1.25f),
    };

    private static readonly string[] PlantPrefixes = { "fern", "reed", "swamp_plant", "broadleaf", "dry_bush", "dead_bush" };
    private static readonly string[] TreePrefixes = { "dead_tree", "dead_pine", "dead_trunk", "dead_quiver", "quiver_tree" };
    private static readonly string[] NaturalPrefixes =
    {
        "rock", "boulder", "cliff", "spire", "shard", "basalt", "dead_", "fallen_log", "stump", "roots", "root_piece", "branch", "quiver",
    };

    public static LevelTheme Theme => _theme;

    /// <summary>A new level: prop materials are converted again with this theme's grading the next time each model is used.</summary>
    public static void SetTheme(LevelTheme theme)
    {
        _theme = theme;
        _stamp++;
        Converted.Clear();
    }

    private static bool HasPrefix(string name, string[] prefixes)
    {
        foreach (var p in prefixes) if (name.StartsWith(p, StringComparison.Ordinal)) return true;
        return false;
    }

    public static float BaseScale(string model)
    {
        float best = 1.12f;
        int bestLen = 0;
        foreach (var (prefix, scale) in Scales)
            if (prefix.Length > bestLen && model.StartsWith(prefix, StringComparison.Ordinal)) { best = scale; bestLen = prefix.Length; }
        return best;
    }

    public static bool IsPlaceable(string entry) => !string.IsNullOrEmpty(entry) && (entry[0] == '@' || Def(entry) != null);

    // ───────────── loading ─────────────

    private static IEnumerable<Node> Walk(Node n)
    {
        yield return n;
        foreach (var c in n.GetChildren())
            foreach (var d in Walk(c)) yield return d;
    }

    /// <summary>Forget the prop models of the last theme; the ones the next level uses are loaded again on demand.</summary>
    public static void Trim()
    {
        _prevDefs = new Dictionary<string, PropDef>(Defs);     // one theme back stays loaded: town and back costs nothing
        Defs.Clear();
        Converted.Clear();
    }
    private static Dictionary<string, PropDef> _prevDefs = new();

    /// <summary>The model by folder name; null (logged once) when it does not exist.</summary>
    public static PropDef Def(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (Defs.TryGetValue(name, out var cached)) return cached;
        if (_prevDefs.TryGetValue(name, out cached)) { Defs[name] = cached; return cached; }
        string path = $"{EnvAssets.ModelRoot}{name}/{name}.glb";
        PropDef def = null;
        try
        {
            if (ResourceLoader.Exists(path))
            {
                var scene = GD.Load<PackedScene>(path);
                if (scene != null) def = Extract(name, scene);
            }
        }
        catch (Exception e)
        {
            Log.Error("env", $"loading prop '{name}' failed", e);
            def = null;
        }
        if (def == null && EnvAssets.LogOnce("prop:" + name)) Log.Warn("env", $"missing prop model '{name}' ({path})");
        Defs[name] = def;
        return def;
    }

    private static PropDef Extract(string name, PackedScene scene)
    {
        var inst = scene.Instantiate();
        var def = new PropDef { Name = name, Scene = scene, Plant = HasPrefix(name, PlantPrefixes) };
        def.Natural = def.Plant || HasPrefix(name, NaturalPrefixes);
        try
        {
            bool first = true;
            foreach (var node in Walk(inst))
            {
                if (node is not MeshInstance3D mi || mi.Mesh == null) continue;
                var local = Transform3D.Identity;
                for (Node c = mi; c != null && c != inst; c = c.GetParent())
                    if (c is Node3D n3) local = n3.Transform * local;
                int surfaces = mi.Mesh.GetSurfaceCount();
                var part = new PropPart { Mesh = mi.Mesh, Local = local, Src = new Material[surfaces] };
                for (int s = 0; s < surfaces; s++)
                {
                    part.Src[s] = mi.GetActiveMaterial(s);
                    if (mi.Mesh is ArrayMesh am)
                    {
                        int il = am.SurfaceGetArrayIndexLen(s);
                        def.Tris += (il > 0 ? il : am.SurfaceGetArrayLen(s)) / 3;
                    }
                }
                var box = local * mi.Mesh.GetAabb();
                def.Bounds = first ? box : def.Bounds.Merge(box);
                first = false;
                def.Parts.Add(part);
            }
        }
        finally { inst.Free(); }
        if (def.Parts.Count == 0)
        {
            Log.Warn("env", $"prop '{name}' contains no meshes");
            return null;
        }
        def.Flames = FlamesFor(name, def);
        return def;
    }

    private static Vector3[] FlamesFor(string name, PropDef def)
    {
        if (FlamePoints.TryGetValue(name, out var f)) return f;
        LoadManifestFlames();
        if (_manifestFlames.TryGetValue(name, out f)) return f;
        return null;
    }

    /// <summary>Flame points of light props the built-in table does not know, from the model manifest when it exists.</summary>
    private static void LoadManifestFlames()
    {
        if (_manifestFlames != null) return;
        _manifestFlames = new Dictionary<string, Vector3[]>();
        string path = EnvAssets.ModelRoot + "manifest.json";
        try
        {
            if (!Godot.FileAccess.FileExists(path)) return;
            using var fa = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (fa == null) return;
            using var doc = JsonDocument.Parse(fa.GetAsText());
            if (!doc.RootElement.TryGetProperty("props", out var props) || props.ValueKind != JsonValueKind.Array) return;
            foreach (var p in props.EnumerateArray())
            {
                if (!p.TryGetProperty("name", out var nm) || nm.ValueKind != JsonValueKind.String) continue;
                if (!p.TryGetProperty("light", out var light) || light.ValueKind != JsonValueKind.Object) continue;
                if (!light.TryGetProperty("flame_positions", out var fl) || fl.ValueKind != JsonValueKind.Array) continue;
                var pts = new List<Vector3>();
                foreach (var e in fl.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.Array && e.GetArrayLength() >= 3)
                        pts.Add(new Vector3((float)e[0].GetDouble(), (float)e[1].GetDouble(), (float)e[2].GetDouble()));
                if (pts.Count > 0) _manifestFlames[nm.GetString()] = pts.ToArray();
            }
            Log.Info("env", $"model manifest: flame points for {_manifestFlames.Count} light props");
        }
        catch (Exception e) { Log.Error("env", "reading the model manifest failed", e); }
    }

    // ───────────── materials ─────────────

    /// <summary>Puts the current theme's converted materials on the model's meshes (once per level).</summary>
    public static void Use(PropDef def)
    {
        if (def == null || _theme == null || def.Stamp == _stamp) return;
        def.Stamp = _stamp;
        try
        {
            foreach (var part in def.Parts)
                for (int s = 0; s < part.Src.Length; s++)
                {
                    var src = part.Src[s];
                    if (src != null) part.Mesh.SurfaceSetMaterial(s, Convert(src, def));
                }
        }
        catch (Exception e) { Log.Error("env", $"converting materials of prop '{def.Name}' failed", e); }
    }

    private static Material Convert(Material src, PropDef def)
    {
        int cls = def.Plant ? 2 : def.Natural ? 1 : 0;
        var key = (src.GetInstanceId(), cls);
        if (Converted.TryGetValue(key, out var conv)) return conv;
        conv = src;
        // glass and other blended surfaces keep their imported material: they must stay in the transparent pass
        if (src is BaseMaterial3D b && b.Transparency != BaseMaterial3D.TransparencyEnum.Alpha)
        {
            bool twoSided = b.CullMode == BaseMaterial3D.CullModeEnum.Disabled;
            var shader = EnvAssets.PropShader(twoSided, def.Plant);
            if (shader != null)
            {
                var t = _theme;
                var m = new ShaderMaterial { Shader = shader, ResourceName = b.ResourceName };
                m.SetShaderParameter("albedo", b.AlbedoColor);
                if (b.AlbedoTexture != null) m.SetShaderParameter("albedo_tex", b.AlbedoTexture);
                if (b.NormalEnabled && b.NormalTexture != null)
                {
                    m.SetShaderParameter("normal_tex", b.NormalTexture);
                    m.SetShaderParameter("normal_scale", b.NormalScale);
                }
                m.SetShaderParameter("roughness", b.Roughness);
                m.SetShaderParameter("metallic", b.Metallic);
                var orm = b.RoughnessTexture ?? b.MetallicTexture ?? b.AOTexture;
                if (orm != null) m.SetShaderParameter("orm_tex", orm);
                m.SetShaderParameter("ao_amount", b.AOEnabled && b.AOTexture != null ? 1f : 0f);
                if (b.EmissionEnabled)
                {
                    var e = b.Emission;
                    if (b.EmissionTexture != null)
                    {
                        m.SetShaderParameter("emission_tex", b.EmissionTexture);
                        if (e.R + e.G + e.B > 2.9f) e = Colors.Black;      // white factor × texture: the texture carries the colour
                    }
                    m.SetShaderParameter("emission", new Color(e.R, e.G, e.B));
                    m.SetShaderParameter("emission_energy", Mathf.Clamp(b.EmissionEnergyMultiplier, 1f, 4f));
                }
                if (b.Transparency == BaseMaterial3D.TransparencyEnum.AlphaScissor) m.SetShaderParameter("alpha_cut", b.AlphaScissorThreshold);
                else if (b.Transparency != BaseMaterial3D.TransparencyEnum.Disabled) m.SetShaderParameter("alpha_cut", 0.4f);
                m.SetShaderParameter("noise_tex", EnvAssets.Noise);
                m.SetShaderParameter("tint", new Vector3(t.PropTint.R, t.PropTint.G, t.PropTint.B));
                m.SetShaderParameter("desaturate", t.Desaturate * 0.7f);
                m.SetShaderParameter("top_color", t.TopColor);
                m.SetShaderParameter("top_amount", def.Natural ? t.TopAmount : t.TopAmount * 0.4f);
                m.SetShaderParameter("sway", def.Plant ? 0.035f : 0f);
                // the see-through cut only opens props that can hide a figure: the shader multiplies this height by the
                // instance scale (the mesh node's own scale is taken out here, it is part of the instance transform)
                float nodeScale = def.Parts.Count > 0 ? def.Parts[0].Local.Basis.Y.Length() : 1f;
                m.SetShaderParameter("prop_height", def.Height / Mathf.Max(nodeScale, 1e-4f));
                // trees: the wide see-through disc opens the crown only; the trunk stands (it used to dissolve into a
                // grainy column whenever the hero walked past) and is cut just where it covers a figure
                if (HasPrefix(def.Name, TreePrefixes))
                    m.SetShaderParameter("disc_from", def.Name.Contains("quiver", StringComparison.Ordinal) ? 0.55f : def.Name.StartsWith("dead_trunk", StringComparison.Ordinal) ? 0.6f : 0.3f);
                // props were never levelled like the ground and walls: near-black stone and iron is lifted to the foot of
                // the theme's wall band, and rock under the sky gets the sky's share of the ambient and a faint sheen
                m.SetShaderParameter("lum_floor", t.WallLumMin * EnvMats.PropLumFloor);
                if (def.Natural && !def.Plant)
                {
                    m.SetShaderParameter("sky_fill", EnvMats.SkyFill(t));
                    m.SetShaderParameter("sky_rim", EnvMats.SkyRim(t));
                }
                if (def.Flames != null && def.Flames.Length > 0)
                {
                    // where PlaceLight hangs this prop's own light: the middle of its flames, a quarter metre (world) higher
                    var at = Vector3.Zero;
                    int nf = Math.Min(def.Flames.Length, 7);
                    for (int i = 0; i < nf; i++) at += def.Flames[i];
                    at /= nf;
                    float ks = LightKinds.TryGetValue(def.Name, out var lk) ? lk.Scale : BaseScale(def.Name);
                    at += Vector3.Up * (0.25f / Mathf.Max(ks, 0.1f));
                    if (def.Parts.Count > 0) at = def.Parts[0].Local.AffineInverse() * at;
                    m.SetShaderParameter("flame_local", at);
                    m.SetShaderParameter("flame_guard", FlameGuard);
                }
                conv = m;
            }
        }
        else if (src is BaseMaterial3D glass && def.Flames != null)
        {
            // Lantern glass: the lamp's own light stands a hand away from the panes and burnt them to white pixels (the
            // blended panes also take no part in anti-aliasing). They carry a thin warm film of the flame's colour
            // instead and take no light; the flame card and its halo inside do the glowing.
            var tc = _theme.TorchColor;
            conv = new StandardMaterial3D
            {
                ResourceName = glass.ResourceName + "_warm", ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                AlbedoColor = new Color(Mathf.Min(tc.R * 0.92f, 1f), Mathf.Min(tc.G * 0.66f, 1f), Mathf.Min(tc.B * 0.4f, 1f), 0.26f),
            };
        }
        Converted[key] = conv;
        return conv;
    }

    public static void PrepareInstance(Node3D inst)
    {
        foreach (var node in Walk(inst))
            if (node is MeshInstance3D mi) mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
    }

    // ───────────── placement ─────────────

    public static Transform3D Xf(Vector3 pos, float yaw, float scale) =>
        new(new Basis(Vector3.Up, yaw).Scaled(new Vector3(scale, scale, scale)), pos);

    /// <summary>Queues one instance of a model. Returns false when the model does not exist.</summary>
    public static bool Place(EnvBuild b, string model, Vector3 pos, float yaw, float scale, bool shadow)
    {
        var def = Def(model);
        if (def == null) return false;
        b.AddProp(def, Xf(pos, yaw, scale), shadow);
        return true;
    }

    /// <summary>Fills every prop slot of the level except the breakables (those are interactables built elsewhere).</summary>
    public static void PlaceSlots(EnvBuild b)
    {
        var slots = b.Data.Props;
        if (slots == null) return;
        var t = b.Theme;
        int placed = 0, empty = 0;
        for (int k = 0; k < slots.Count; k++)
        {
            var s = slots[k];
            if (s == null || s.Category == "breakable") continue;
            // a tagged slot names what the generator reserved the footprint for: build exactly that
            if (!string.IsNullOrEmpty(s.Tag))
            {
                var tr = EnvRng.For(b.Seed, k, 7919);
                bool done = false;
                try { done = PlaceTagged(b, s, ref tr); }
                catch (Exception ex)
                {
                    if (EnvAssets.LogOnce("tag:" + s.Tag)) Log.Error("env", $"placing tagged slot '{s.Tag}' ({s.Category}) failed", ex);
                }
                if (done) { placed++; continue; }
            }
            var list = t.List(s.Category ?? "");
            if (list.Length == 0) list = Generic(s.Category);
            if (list.Length == 0) { empty++; continue; }
            var r = EnvRng.For(b.Seed, k, 7919);
            // pick deterministically; if that entry's model is missing walk on through the list
            int start = r.Int(list.Length);
            string entry = null;
            for (int n = 0; n < list.Length; n++)
            {
                string e = list[(start + n) % list.Length];
                if (IsPlaceable(e)) { entry = e; break; }
            }
            if (entry == null) { empty++; continue; }
            try
            {
                PlaceSlot(b, s, entry, ref r);
                placed++;
            }
            catch (Exception ex)
            {
                if (EnvAssets.LogOnce("slot:" + entry)) Log.Error("env", $"placing '{entry}' ({s.Category}) failed", ex);
            }
        }
        Log.Debug("env", $"prop slots: {placed} placed, {empty} without a usable entry");
    }

    /// <summary>First entry of a theme list that names one of the wanted kinds and can be placed; null when there is none.</summary>
    private static string FirstOf(string[] list, params string[] wanted)
    {
        foreach (var w in wanted)
            foreach (var e in list)
                if (e == w && IsPlaceable(e)) return e;
        return null;
    }

    /// <summary>
    /// Slots that carry a tag (house, hall, inn, forge, stall, tent, well, bench, lantern, torch, brazier, camp, ruin)
    /// get the matching piece instead of a random pick from the category list; houses are sized from the slot's
    /// rectangular footprint, so what blocks the hero is what the eye sees. Returns false for tags it does not know.
    /// </summary>
    private static bool PlaceTagged(EnvBuild b, PropSlot s, ref EnvRng r)
    {
        var t = b.Theme;
        var pos = new Vector3(s.Pos.X, 0f, s.Pos.Y);
        float scale = Mathf.Clamp(s.Scale <= 0.01f ? 1f : s.Scale, 0.3f, 4f);
        var facing = new Vector3(Mathf.Sin(s.Facing), 0f, Mathf.Cos(s.Facing));
        switch (s.Tag)
        {
            case "house":
            case "hall":
            case "inn":
            {
                // the footprint is world-aligned: build in the world frame and put the door on the side the slot faces
                float hx, hz;
                if (s.Half.X > 0.5f && s.Half.Y > 0.5f) { hx = s.Half.X - 0.3f; hz = s.Half.Y - 0.3f; }
                else { hx = Mathf.Max(s.Radius * 0.8f, 2.2f); hz = Mathf.Max(s.Radius * 0.66f, 1.9f); }
                int kind = s.Tag == "hall" ? 1 : s.Tag == "inn" ? 2 : 0;
                EnvDress.House(b, Xf(pos, 0f, 1f), hx, hz, facing, ref r, kind);
                return true;
            }
            case "forge": EnvDress.Forge(b, Xf(pos, s.Facing, 1f), ref r); return true;
            case "stall": EnvDress.Stall(b, Xf(pos, s.Facing, scale), ref r); return true;
            case "tent": EnvDress.Tent(b, Xf(pos, s.Facing, Mathf.Clamp(s.Radius > 0.6f ? s.Radius / 1.5f : scale, 0.7f, 1.5f)), ref r); return true;
            case "well": EnvDress.Well(b, Xf(pos, s.Facing, Mathf.Clamp(s.Radius > 0.5f ? s.Radius / 1.05f : scale, 0.7f, 1.4f)), ref r); return true;
            case "ruin": EnvDress.Make(b, "@ruin_wall", pos, s.Facing, scale, s.Radius, ref r); return true;
            case "camp":
            {
                PlaceLight(b, Def("fire_pit") != null ? "fire_pit" : "@brazier", pos, s.Facing, scale, ref r);
                for (int i = 0; i < 3; i++)
                {
                    float a = r.Angle(), d = Mathf.Max(s.Radius, 1.3f) + r.Range(0.1f, 0.5f);
                    Place(b, "camp_stool", pos + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), a, BaseScale("camp_stool"), false);
                }
                return true;
            }
            case "bench":
            {
                var def = Def("bench");
                if (def == null) return false;
                float sc = BaseScale("bench") * scale;
                if (s.Radius > 0.3f && def.Foot * sc > s.Radius * 1.9f) sc = s.Radius * 1.9f / def.Foot;
                b.AddProp(def, Xf(pos, s.Facing, sc), true);
                return true;
            }
            case "lantern":
            {
                string e = FirstOf(t.Lights, "lamp_post", "lantern_wood", "candelabra_5", "@brazier_brass", "@brazier", "@torch") ?? "@torch";
                PlaceLight(b, e, pos, s.Facing, scale, ref r);
                return true;
            }
            case "torch":
                PlaceLight(b, "@torch", pos, s.Facing, scale, ref r);
                return true;
            case "brazier":
                PlaceLight(b, FirstOf(t.Lights, "@brazier_brass", "@brazier") ?? "@brazier", pos, s.Facing, scale, ref r);
                return true;
        }
        if (EnvAssets.LogOnce("tagunknown:" + s.Tag)) Log.Debug("env", $"prop slot tag '{s.Tag}' has no special dressing; using the category list '{s.Category}'");
        return false;
    }

    // ───────────── extra lights ─────────────

    /// <summary>
    /// Pools of light where the generator left long dark stretches: along the walls of every indoor level (and among
    /// the graves of the two burial grounds), wherever no light stands within about 12 m, a small piece that suits
    /// the place is set down with a weak shadowless light: candles guttering on the flagstones, glowing fungus in the
    /// wet caves, lit ice in the frozen ones, embers between the rocks where lava runs, a lantern on a post in the
    /// root caves. They are decoration only (nothing blocks), and the light manager still caps what burns at once.
    /// </summary>
    public static void FillLights(EnvBuild b)
    {
        var t = b.Theme;
        var f = b.F;
        bool burial = t.Id is "graveyard" or "barrows";
        if (t.Outdoor && !burial) return;
        float spacing = burial ? 13f : 12f;
        if (b.Quality <= 0) spacing *= 1.35f;

        var lights = new List<Vector2>(b.Lights.Count + 64);
        foreach (var l in b.Lights) lights.Add(new Vector2(l.Pos.X, l.Pos.Z));

        var taken = new List<(Vector2 at, float r)>();
        if (b.Data.Props != null) foreach (var s in b.Data.Props) if (s != null) taken.Add((s.Pos, Mathf.Max(s.Radius, Mathf.Max(s.Half.X, s.Half.Y)) + 0.9f));
        if (b.Data.Pois != null) foreach (var p in b.Data.Pois) if (p != null) taken.Add((p.Pos, Mathf.Clamp(p.Radius, 0f, 5f) + 2.5f));

        // candidate cells: open floor one step from a wall, visited in a fixed scrambled order
        var cells = new List<int>();
        for (int cy = 0; cy < f.CH; cy++)
            for (int cx = 0; cx < f.CW; cx++)
            {
                if (f.Cell(cx, cy) != NavGrid.Floor || !f.InGrid(cx, cy) || f.PitDistance(cx, cy) <= 1) continue;
                var c = f.CellCenter(cx, cy);
                float open = f.Sample(f.OpenD, c.X, c.Z);
                if (open < 0.6f || open > 1.9f) continue;
                cells.Add(cy * f.CW + cx);
            }
        int seed = b.Seed;
        cells.Sort((x, y) => EnvRng.For(seed, x, 3301).Next().CompareTo(EnvRng.For(seed, y, 3301).Next()));

        int placed = 0;
        float min2 = spacing * spacing;
        foreach (int k in cells)
        {
            var c = f.CellCenter(k % f.CW, k / f.CW);
            var at = new Vector2(c.X, c.Z);
            bool ok = true;
            foreach (var l in lights) if (l.DistanceSquaredTo(at) < min2) { ok = false; break; }
            if (!ok) continue;
            foreach (var (p, rad) in taken) if (p.DistanceSquaredTo(at) < rad * rad) { ok = false; break; }
            if (!ok) continue;

            var r = EnvRng.For(seed, k, 3307);
            var pos = new Vector3(c.X + r.Range(-0.5f, 0.5f), 0f, c.Z + r.Range(-0.5f, 0.5f));
            var xf = Xf(pos, r.Angle(), 1f);
            string pit = EnvTerrain.PitKind(t);
            if (t.Organic && t.Id is "wetcave") EnvDress.Glowshroom(b, xf, ref r);
            else if (t.Id is "icecave" or "icehall" && (t.Organic || r.Chance(0.5f))) EnvDress.LitCrystal(b, xf, ref r);
            else if (t.PitKind == "lava" && (t.Organic || r.Chance(0.6f))) EnvDress.EmberRocks(b, xf, ref r);
            else if (t.Organic) PlaceLight(b, Def("lantern_wood") != null ? "lantern_wood" : "@torch", pos, r.Angle(), 1f, ref r);
            else EnvDress.Candles(b, xf, ref r);
            lights.Add(at);
            b.Reserved.Add((at, 1.3f));
            placed++;
        }
        Log.Debug("env", $"fill lights: {placed} added to {b.Lights.Count - placed} (spacing {spacing:0.#} m, {cells.Count} candidate cells)");
    }

    // ───────────── ground scatter ─────────────

    /// <summary>
    /// Small things lying about that no prop slot asked for: pebbles and chips of the local stone, twigs, stubs, tufts.
    /// Purely visual (nothing here blocks or casts a long shadow), placed by a hash of the cell so a level always
    /// looks the same, gathered into drifts by low-frequency noise and thicker along walls where debris collects.
    /// Paths, arenas, shores and the surroundings of every prop slot and point of interest stay clear.
    /// </summary>
    public static void ScatterGround(EnvBuild b)
    {
        var t = b.Theme;
        var f = b.F;
        float share = Mathf.Clamp(t.Scatter, 0f, 0.6f) * Gfx.PresetOf(Game.I?.Cfg).Dressing;
        if (share <= 0.004f) return;

        // what may be scattered: the list of the theme, or the small natural pieces of its clutter / rock / foliage lists
        var models = new List<PropDef>();
        void Consider(string[] list, bool explicitList)
        {
            if (list == null) return;
            foreach (var e in list)
            {
                if (string.IsNullOrEmpty(e) || e[0] == '@') continue;
                var def = Def(e);
                if (def == null || models.Contains(def)) continue;
                if (!explicitList && (def.Tris > 4600 || def.Height * BaseScale(e) > 1.0f || def.Foot * BaseScale(e) > 1.3f)) continue;
                models.Add(def);
            }
        }
        if (t.ScatterProps != null && t.ScatterProps.Length > 0) Consider(t.ScatterProps, true);
        else { Consider(t.Clutter, false); Consider(t.Rocks, false); Consider(t.Foliage, false); }

        // cells to keep clear
        var keep = new bool[f.CW * f.CH];
        void Clear(Vector2 at, float radius)
        {
            var c0 = f.CellOf(at.X - radius, at.Y - radius);
            var c1 = f.CellOf(at.X + radius, at.Y + radius);
            for (int cy = c0.Y; cy <= c1.Y; cy++)
                for (int cx = c0.X; cx <= c1.X; cx++)
                    if (f.InCells(cx, cy)) keep[cy * f.CW + cx] = true;
        }
        if (b.Data.Props != null) foreach (var s in b.Data.Props) if (s != null) Clear(s.Pos, Mathf.Max(s.Radius, Mathf.Max(s.Half.X, s.Half.Y)) + 0.8f);
        if (b.Data.Pois != null) foreach (var p in b.Data.Pois) if (p != null) Clear(p.Pos, Mathf.Clamp(p.Radius, 0f, 6f) + 2.2f);
        foreach (var (at, rad) in b.Reserved) Clear(at, rad);

        string stone = t.Outdoor && !b.SolidWalls ? (string.IsNullOrEmpty(t.CliffTex) ? t.DressStone : t.CliffTex) : t.DressStone;
        int pebbles = 0, props = 0;
        for (int cy = 0; cy < f.CH; cy++)
            for (int cx = 0; cx < f.CW; cx++)
            {
                int k = cy * f.CW + cx;
                if (keep[k] || f.Cell(cx, cy) != NavGrid.Floor || !f.InGrid(cx, cy)) continue;
                byte deco = f.DecoAt(cx, cy);
                if (deco == 4 || deco == 5) continue;
                var r = EnvRng.For(b.Seed, k, 5501);
                var c = f.CellCenter(cx, cy);
                float drift = Mathf.SmoothStep(0.38f, 0.7f, EnvNoise.Fbm(c.X * 0.055f, c.Z * 0.055f, b.Seed + 91));
                float p = share * (0.25f + 1.9f * drift);
                if (deco == 1) p *= 0.2f; else if (deco == 3) p *= 0.6f;
                float open = f.Sample(f.OpenD, c.X, c.Z);
                if (open < 2.2f) p *= 2.2f;                      // against the walls
                if (!r.Chance(p)) continue;
                var pos = new Vector3(c.X + r.Range(-0.85f, 0.85f), 0f, c.Z + r.Range(-0.85f, 0.85f));
                if (models.Count == 0 || r.Chance(0.5f))
                {
                    EnvDress.Pebbles(b, Xf(pos, r.Angle(), 1f), stone, ref r);
                    pebbles++;
                }
                else
                {
                    var def = models[r.Int(models.Count)];
                    float sc = BaseScale(def.Name) * r.Range(0.6f, 1.05f);
                    float sink = def.Natural && !def.Plant ? 0.05f * sc : 0f;
                    b.AddProp(def, Xf(pos - Vector3.Up * sink, r.Angle(), sc), false);
                    props++;
                }
            }
        Log.Debug("env", $"scatter: {pebbles} pebble drifts, {props} small props from {models.Count} models (share {share:0.00})");
    }

    private static string[] Generic(string category) => category switch
    {
        "tree" => new[] { "dead_trunk", "dead_tree_b" },
        "rock" => new[] { "rock_g", "rock_small_a", "@rubble" },
        "rock_big" => new[] { "boulder_a", "@rubble_big" },
        "clutter" => new[] { "@rubble", "rock_small_b" },
        "furniture" => new[] { "table_small", "bench" },
        "light" => new[] { "@torch" },
        "pillar" => new[] { "@pillar_square" },
        "structure" => new[] { "@ruin_wall" },
        "foliage" => new[] { "dead_bush_b" },
        _ => Array.Empty<string>(),
    };

    private static void PlaceSlot(EnvBuild b, PropSlot s, string entry, ref EnvRng r)
    {
        var pos = new Vector3(s.Pos.X, 0f, s.Pos.Y);
        string cat = s.Category ?? "";
        bool oriented = cat is "furniture" or "structure" or "light" or "pillar";
        float yaw = oriented ? s.Facing + r.Range(-0.07f, 0.07f) : r.Angle();
        float scale = Mathf.Clamp(s.Scale <= 0.01f ? 1f : s.Scale, 0.3f, 4f);

        if (cat == "light") { PlaceLight(b, entry, pos, yaw, scale, ref r); return; }
        if (entry[0] == '@') { EnvDress.Make(b, entry, pos, yaw, scale, s.Radius, ref r); return; }

        var def = Def(entry);
        if (def == null) return;
        float sc = scale * BaseScale(entry) * r.Range(0.9f, 1.14f);
        switch (cat)
        {
            case "rock":
            case "rock_big":
            case "pillar":
                // fit the footprint to the collision radius the generator reserved
                if (s.Radius > 0.25f && def.Foot > 0.05f)
                    sc = Mathf.Clamp(s.Radius * (cat == "rock_big" ? 1.3f : 1.15f) / def.Foot, 0.35f, 4.5f) * r.Range(0.94f, 1.08f) * Mathf.Sqrt(scale);
                else if (cat == "rock_big") sc *= 1.5f;
                // a slender model (a trunk, a spire) fitted to a wide footprint would tower over the camera: cap its height
                // and how far it may be blown up beyond its natural size
                {
                    float maxH = cat == "pillar" ? 5f : cat == "rock_big" ? 4.6f : 2.6f;
                    float natural = BaseScale(entry);
                    sc = Mathf.Min(sc, natural * 2.4f);
                    if (def.Height * sc > maxH) sc = maxH / def.Height;
                }
                break;
            case "tree":
                sc *= r.Range(1f, 1.3f);
                if (def.Height * sc > 9.5f) sc = 9.5f / def.Height;
                break;
            case "furniture":
            case "structure":
                // never let an oversized model swallow the room it stands in
                if (s.Radius > 0.6f && def.Foot * sc > s.Radius * 2.2f) sc = s.Radius * 2.2f / def.Foot;
                break;
        }
        float sink = def.Natural && !def.Plant ? 0.04f * sc : 0f;
        bool shadow = !def.Plant && (cat is not ("foliage" or "clutter") || def.Height * sc > 1.2f);       // plants sway, and a swaying shadow caster keeps every shadow map near it dirty
        b.AddProp(def, Xf(pos - Vector3.Up * sink, yaw, sc), shadow);
    }

    /// <summary>A light slot: lantern / torch / brazier prop, its flames and a warm omni light handed to the light manager.</summary>
    public static void PlaceLight(EnvBuild b, string entry, Vector3 pos, float yaw, float scale, ref EnvRng r)
    {
        if (entry[0] == '@') { EnvDress.Make(b, entry, pos, yaw, scale, 0f, ref r); return; }
        var def = Def(entry);
        if (def == null) { EnvDress.Make(b, "@torch", pos, yaw, scale, 0f, ref r); return; }
        var t = b.Theme;
        if (!LightKinds.TryGetValue(entry, out var kind)) kind = new LightKind(BaseScale(entry), def.Height < 1.2f ? 1 : 0, 0.3f, 0.8f, 0.9f, false);
        float sc = scale * kind.Scale;
        float stand = 0f;
        if (kind.Stand != 0 && def.Height * sc < 1.4f) stand = EnvDress.Stand(b, pos, yaw, kind.Stand == 2, ref r);
        var xf = Xf(pos + Vector3.Up * stand, yaw, sc);
        b.AddProp(def, xf, true);

        var flames = def.Flames;
        Vector3 centre;
        if (flames != null && flames.Length > 0)
        {
            centre = Vector3.Zero;
            int n = Math.Min(flames.Length, 7);
            for (int i = 0; i < n; i++)
            {
                var wp = xf * flames[i];
                centre += wp;
                // a flame smaller than a hand is a pixel from the camera: let lanterns and candles carry a readable glow
                b.AddFlame(wp - Vector3.Up * (0.02f * sc), Mathf.Max(kind.Flame * sc, n > 2 ? 0.2f : 0.3f), 1f, r.F());
            }
            centre /= n;
        }
        else
        {
            centre = xf * new Vector3(0f, def.Height, 0f);
            b.AddFlame(centre, Mathf.Max(kind.Flame * sc, 0.3f), 1f, r.F());
        }
        b.AddLight(centre + Vector3.Up * 0.25f, t.TorchColor, TorchEnergy * kind.Energy, t.TorchRange * kind.Range, 1f, kind.Shadow);
    }
}
