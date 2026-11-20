using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>A town NPC the level keeps turning toward the hero and whose quest marker it refreshes.</summary>
public sealed class NpcRef
{
    public string Id;
    public Interactable Obj;
    public CharModel Model;
    public Node3D QuestMark;
    public float BaseYaw, MarkY;
}

/// <summary>
/// Factories for everything the hero can click in a level: chests, shrines, altars, breakables, beacons, doors,
/// portals, town services and dungeon objectives. Each builds an <see cref="Interactable"/> with its visuals
/// (a prop from the environment kit when it exists, otherwise simple shared meshes), a zh-TW label and its behaviour.
/// </summary>
public static class WorldObjects
{
    // ───────────────────────── shared meshes and materials ─────────────────────────

    private static readonly Dictionary<string, Material> Mats = new();
    private static readonly Dictionary<string, Aabb> BoundsCache = new();
    private static readonly HashSet<string> MissingProps = new();
    private static BoxMesh _box;
    private static CylinderMesh _cyl, _cone;
    private static SphereMesh _sphere, _gem;
    private static TorusMesh _torus;

    private static BoxMesh BoxM => _box ??= new BoxMesh { Size = Vector3.One };
    private static CylinderMesh CylM => _cyl ??= new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 14, Rings = 1 };
    private static CylinderMesh ConeM => _cone ??= new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 1 };
    private static SphereMesh SphereM => _sphere ??= new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 14, Rings = 7 };
    private static SphereMesh GemM => _gem ??= new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 4, Rings = 1 };
    private static TorusMesh TorusM => _torus ??= new TorusMesh { InnerRadius = 0.92f, OuterRadius = 1f, Rings = 40, RingSegments = 6 };

    private static readonly Color Stone = new(0.36f, 0.35f, 0.34f), DarkStone = new(0.15f, 0.14f, 0.16f), Wood = new(0.33f, 0.22f, 0.13f),
        Iron = new(0.2f, 0.2f, 0.22f), Brass = new(0.72f, 0.55f, 0.24f), Warm = new(1f, 0.66f, 0.36f), Hush = new(0.62f, 0.3f, 0.95f);

    public static Material Flat(Color c, float rough = 0.88f, float metal = 0f)
    {
        string key = $"f{c.ToRgba32():x8}_{(int)(rough * 20f)}_{(int)(metal * 20f)}";
        if (!Mats.TryGetValue(key, out var m)) Mats[key] = m = new StandardMaterial3D { AlbedoColor = c, Roughness = rough, Metallic = metal };
        return m;
    }

    public static Material Glow(Color c, float energy = 3f)
    {
        string key = $"g{c.ToRgba32():x8}_{(int)(energy * 10f)}";
        if (!Mats.TryGetValue(key, out var m))
            Mats[key] = m = new StandardMaterial3D
            {
                AlbedoColor = new Color(c.R * 0.35f, c.G * 0.35f, c.B * 0.35f), Roughness = 0.5f,
                EmissionEnabled = true, Emission = new Color(c.R, c.G, c.B), EmissionEnergyMultiplier = energy,
            };
        return m;
    }

    /// <summary>A textured surface from the environment kit, or a flat colour when the set is not there.</summary>
    public static Material Tex(string set, Color fallback, float rough = 0.88f, float metal = 0f)
    {
        string key = "t" + set;
        if (!Mats.TryGetValue(key, out var m))
        {
            try { m = LevelBuilder.Surface(set); }
            catch (Exception e) { Log.Warn("objects", $"surface '{set}' unavailable: {e.Message}"); }
            Mats[key] = m;                  // a null is remembered too: the flat colour stands in
        }
        return m ?? Flat(fallback, rough, metal);
    }

    public static MeshInstance3D Part(Node3D parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 rotDeg = default, bool shadow = true)
    {
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Position = pos, RotationDegrees = rotDeg, Scale = scale };
        mi.CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
        parent.AddChild(mi);
        return mi;
    }

    public static OmniLight3D Light(Node3D parent, Color c, float energy, float range, float y, bool shadow = false)
    {
        var l = new OmniLight3D { LightColor = c, LightEnergy = energy, OmniRange = range, OmniAttenuation = 1.3f, ShadowEnabled = shadow, Position = new Vector3(0, y, 0) };
        l.LightSpecular = 0.3f;
        parent.AddChild(l);
        return l;
    }

    private static void Attach(Node3D parent, Node3D child, Vector3 pos)
    {
        if (child == null) return;
        parent.AddChild(child);
        child.Position = pos;
    }

    // ───────────────────────── props from the environment kit ─────────────────────────

    /// <summary>An instance of a kit prop, or null (logged once) when the model does not exist.</summary>
    public static Node3D Prop(string name)
    {
        if (string.IsNullOrEmpty(name) || MissingProps.Contains(name)) return null;
        Node3D n = null;
        try { n = LevelBuilder.Prop(name); }
        catch (Exception e) { Log.Warn("objects", $"prop '{name}' failed to load: {e.Message}"); }
        if (n == null && MissingProps.Add(name)) Log.Debug("objects", $"prop '{name}' missing, using a built shape");
        return n;
    }

    private static void Accumulate(Node n, Transform3D xf, ref Aabb box, ref bool any)
    {
        if (n is Node3D n3) xf *= n3.Transform;
        if (n is MeshInstance3D mi && mi.Mesh != null)
        {
            var a = xf * mi.GetAabb();
            box = any ? box.Merge(a) : a;
            any = true;
        }
        foreach (var c in n.GetChildren()) Accumulate(c, xf, ref box, ref any);
    }

    /// <summary>A kit prop scaled to a height (and optionally a maximum width), standing on the ground at its holder's origin.</summary>
    public static Node3D PropFit(string name, float height, float maxWidth = 0f)
    {
        var n = Prop(name);
        if (n == null) return null;
        if (!BoundsCache.TryGetValue(name, out var box))
        {
            box = new Aabb(); bool any = false;
            Accumulate(n, Transform3D.Identity, ref box, ref any);
            if (!any) box = new Aabb(new Vector3(-0.5f, 0, -0.5f), Vector3.One);
            BoundsCache[name] = box;
        }
        var holder = new Node3D { Name = name };
        holder.AddChild(n);
        float s = box.Size.Y > 0.01f ? height / box.Size.Y : 1f;
        if (maxWidth > 0f)
        {
            float w = Mathf.Max(box.Size.X, box.Size.Z) * s;
            if (w > maxWidth) s *= maxWidth / w;
        }
        var centre = box.GetCenter();
        n.Position -= new Vector3(centre.X, box.Position.Y, centre.Z);
        holder.Scale = Vector3.One * s;
        return holder;
    }

    private static Node3D PropAny(float height, float maxWidth, params string[] names)
    {
        foreach (var name in names) { var n = PropFit(name, height, maxWidth); if (n != null) return n; }
        return null;
    }

    // ───────────────────────── common plumbing ─────────────────────────

    private static void Toast(string text, Color c) => Game.I?.Hud?.Toast(text, c);
    private static void Warn(string text) { Game.I?.Hud?.Warn(text); Snd.Ui("ui_error"); }
    /// <summary>
    /// Makes an object solid — unless it stands in a passage one cell wide, where it would wall off the rest of the level
    /// (an ember altar set in the corridor before a boss door did exactly that). There it stays walk-through.
    /// </summary>
    private static void Block(Vector2 pos, float r)
    {
        var grid = World.Grid;
        if (grid == null) return;
        var lv = Level.Current;
        if (r >= 0.6f && lv != null && !lv.KeepsConnected(pos))
        {
            Log.Info("level", $"object at ({pos.X:0},{pos.Y:0}) stands in a narrow passage of {lv.AreaId}: it does not block the way");
            return;
        }
        grid.AddObstacle(pos, r);
    }
    private static int LootLevel => Level.Current?.MonsterLevel ?? Game.I?.Hero?.D.Level ?? 1;

    public static Interactable Make(string kind, Vector2 pos, string label, Color color, float useRange = 2.4f, float labelHeight = 1.5f, float facing = 0f)
    {
        var it = new Interactable { Kind = kind, Label = label, LabelColor = color, UseRange = useRange, LabelHeight = labelHeight, Name = kind };
        it.Position = World.To3(pos);
        it.Rotation = new Vector3(0, facing, 0);
        World.Root?.AddChild(it);
        return it;
    }

    /// <summary>Where loot from an opened container should land: a step toward the hero, on walkable ground.</summary>
    private static Vector2 DropSpot(Vector2 from, Player p)
    {
        Vector2 dir = p != null ? p.Pos2 - from : Vector2.Down;
        if (dir.LengthSquared() < 0.01f) dir = Vector2.Down;
        Vector2 at = from + dir.Normalized() * 1.1f;
        var g = World.Grid;
        return g == null || g.WalkAt(at) ? at : g.NearestWalk(from);
    }

    private static Node3D Arch(Material mat, float width, float height, float depth = 0.7f)
    {
        var n = new Node3D { Name = "arch" };
        float half = width * 0.5f;
        Part(n, BoxM, mat, new Vector3(-half, height * 0.5f, 0), new Vector3(0.7f, height, depth));
        Part(n, BoxM, mat, new Vector3(half, height * 0.5f, 0), new Vector3(0.7f, height, depth));
        Part(n, BoxM, mat, new Vector3(0, height + 0.3f, 0), new Vector3(width + 1.3f, 0.6f, depth + 0.15f));
        Part(n, BoxM, mat, new Vector3(0, height + 0.78f, 0), new Vector3(width * 0.6f, 0.36f, depth));
        return n;
    }

    // ── [gfx 0.3, item 11] gateways: the gate is the portal (Gateways.cs builds it, Fx.Veil fills its opening) ──

    /// <summary>
    /// OFF for the 0.3.0 release: the built gateways compile but had not been seen on screen when work was paused, so
    /// every way in / way out is built as in 0.2.0 (the arch with the round swirl). EnvProbe's --gfx-gates switches
    /// them on for a test run; flip the default once all regions, dungeons and the rift stone have been looked at.
    /// </summary>
    public static bool BuiltGateways = false;

    /// <summary>A kit surface laid in world metres (for unit meshes stretched into slabs and monoliths; static things only).</summary>
    private static Material TexW(string set, Color fallback)
    {
        string key = "tw" + set;
        if (!Mats.TryGetValue(key, out var m))
        {
            try { m = LevelBuilder.Surface(set, 1f, true); }
            catch (Exception e) { Log.Warn("objects", $"surface '{set}' unavailable: {e.Message}"); }
            Mats[key] = m;
        }
        return m ?? Flat(fallback);
    }

    /// <summary>Metres from a spot to the nearest wall, pit or blocking prop, looked for up to <paramref name="upTo"/> (0 when the spot itself is blocked).</summary>
    private static float FreeAround(Vector2 at, float upTo)
    {
        var g = World.Grid;
        if (g == null) return upTo;
        if (!g.WalkAt(at)) return 0f;
        float free = upTo;
        for (int i = 0; i < 16 && free > 0.25f; i++)
        {
            var d = Vector2.FromAngle(i * Mathf.Tau / 16f);
            for (float s = 0.25f; s < free; s += 0.25f)
                if (!g.WalkAt(at + d * s)) { free = s; break; }
        }
        return free;
    }

    /// <summary>
    /// Where a portal may stand near <paramref name="pos"/>: the spot within <paramref name="reach"/> metres that keeps
    /// <paramref name="need"/> metres clear of walls and blocking props, or the clearest one there is (level generators
    /// put a way out on the last cell before the rim, where a ring of stones would stand half inside the border).
    /// </summary>
    private static Vector2 PortalSpot(Vector2 pos, float need, out float free, float reach = 3f)
    {
        free = FreeAround(pos, need);
        if (free >= need) return pos;
        Vector2 best = pos;
        float bestScore = free;
        for (float r = 0.5f; r <= reach; r += 0.5f)
            for (int i = 0; i < 12; i++)
            {
                var at = pos + Vector2.FromAngle(i * Mathf.Tau / 12f) * r;
                float f = FreeAround(at, need);
                float score = f - r * 0.06f;                 // nearer is better at equal clearance
                if (score > bestScore + 0.04f) { bestScore = score; best = at; free = f; }
            }
        if (best != pos) Log.Info("objects", $"portal moved {best.DistanceTo(pos):0.0} m off its marker at ({pos.X:0},{pos.Y:0}) to stand clear ({free:0.0} m free)");
        return best;
    }

    /// <summary>
    /// One gateway: the gate built in the manner of the level's region (Gateways.Build) and, filling its rectangular
    /// opening, the veil that is the portal. The jambs are solid. Returns the height of the opening.
    /// </summary>
    private static float Gateway(Interactable it, Vector2 pos, float facing, float free, Color veil, float gain, bool grand)
    {
        // the opening: a hero and a half wide and well over head height; narrower where the place is tight
        float w = free >= 2.3f ? (grand ? 2.7f : 2.5f) : free >= 1.95f ? 2.2f : 1.9f;
        float h = grand ? 3.2f : 3.0f;
        Gateways.Build(it, facing, pos, w, h, veil, grand);
        var fx = Fx.Veil(veil, w - 0.04f, h - 0.02f, gain);
        Attach(it, fx, new Vector3(0, 0.06f, 0));
        fx.Rotation = new Vector3(0, -facing, 0);                        // like the gate: across world X, face-on to the lens
        var grid = World.Grid;
        if (grid != null && free >= 1.6f)
            foreach (float s in new[] { -1f, 1f })
                grid.AddObstacle(pos + new Vector2(s * (w * 0.5f + 0.38f), 0f), 0.36f);      // the jambs; the opening stays free
        return h;
    }

    private static Node3D Bowl(Material stone, Material metal, float radius, float standHeight)
    {
        var n = new Node3D { Name = "bowl" };
        Part(n, CylM, stone, new Vector3(0, 0.12f, 0), new Vector3(radius * 2.3f, 0.24f, radius * 2.3f));
        Part(n, ConeM, stone, new Vector3(0, 0.24f + standHeight * 0.5f, 0), new Vector3(radius * 1.1f, standHeight, radius * 1.1f));
        Part(n, ConeM, metal, new Vector3(0, 0.24f + standHeight + 0.16f, 0), new Vector3(radius * 2f, 0.32f, radius * 2f), new Vector3(180, 0, 0));
        return n;
    }

    // ───────────────────────── chests ─────────────────────────

    private static Node3D ChestShape(int quality, Color accent)
    {
        var n = new Node3D { Name = "chest" };
        var wood = Tex(quality >= 2 ? "dark_planks" : "old_planks", Wood);
        var band = quality >= 2 ? Flat(Brass, 0.4f, 0.9f) : Flat(Iron, 0.55f, 0.8f);
        float w = 1.15f + quality * 0.08f;
        Part(n, BoxM, wood, new Vector3(0, 0.3f, 0), new Vector3(w, 0.6f, 0.72f));
        Part(n, BoxM, band, new Vector3(-w * 0.32f, 0.3f, 0), new Vector3(0.09f, 0.63f, 0.75f));
        Part(n, BoxM, band, new Vector3(w * 0.32f, 0.3f, 0), new Vector3(0.09f, 0.63f, 0.75f));
        var lid = new Node3D { Name = "lid", Position = new Vector3(0, 0.6f, -0.36f) };
        n.AddChild(lid);
        Part(lid, BoxM, wood, new Vector3(0, 0.12f, 0.36f), new Vector3(w, 0.24f, 0.72f));
        Part(lid, BoxM, band, new Vector3(0, 0.12f, 0.36f), new Vector3(w + 0.04f, 0.1f, 0.76f));
        Part(lid, BoxM, quality >= 2 ? Glow(accent, 2f) : band, new Vector3(0, 0.02f, 0.74f), new Vector3(0.16f, 0.2f, 0.06f), default, false);
        return n;
    }

    /// <summary>A chest that opens once. Quality 0 common … 3 boss; quality 2+ carries a beam so rewards read from afar.</summary>
    public static Interactable Chest(Vector2 pos, int quality, float facing = 0f, Action<Vector2> onOpened = null)
    {
        quality = Math.Clamp(quality, 0, 3);
        string label = quality switch { 0 => "舊木箱", 1 => "鐵箍寶箱", 2 => "燼紋寶箱", _ => "燼主遺匣" };
        Color col = quality switch { 0 => Palette.Text, 1 => Palette.Rarity(Rarity.Magic), 2 => Palette.Rarity(Rarity.Rare), _ => Palette.Rarity(Rarity.Legendary) };
        var it = Make("chest", pos, label, col, 2.4f, 1.3f, facing);
        var vis = PropFit("chest", 0.8f + quality * 0.08f, 1.5f) ?? ChestShape(quality, col);
        it.AddChild(vis);
        Node3D beam = null; OmniLight3D glow = null;
        if (quality >= 2)
        {
            beam = Fx.LootBeam(quality >= 3 ? Rarity.Legendary : Rarity.Rare);
            Attach(it, beam, Vector3.Zero);
            glow = Light(it, col, 1.1f, 5f, 1.2f);
        }
        Block(pos, 0.55f);
        it.OnUse = p =>
        {
            it.CanUse = false; it.ShowLabel = false;
            Snd.At("chest_open", it.GlobalPosition);
            if (vis.FindChild("lid", true, false) is Node3D lid)
                it.CreateTween().TweenProperty(lid, "rotation:x", lid.Rotation.X - 1.25f, 0.45f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            if (beam != null && GodotObject.IsInstanceValid(beam)) beam.QueueFree();
            if (glow != null) glow.Visible = false;
            Fx.Dust(it.GlobalPosition + Vector3.Up * 0.4f);
            Vector2 at = DropSpot(it.Pos2, p);
            Log.Guard("loot", () => Loot.OpenChest(at, quality));
            Level.Current?.RemoveMarker(it);
            Log.Info("objects", $"chest opened quality={quality} at ({it.Pos2.X:F0},{it.Pos2.Y:F0})");
            onOpened?.Invoke(at);
        };
        it.Spend = () =>
        {
            it.CanUse = false; it.ShowLabel = false;
            if (vis.FindChild("lid", true, false) is Node3D lid) lid.Rotation = new Vector3(lid.Rotation.X - 1.25f, lid.Rotation.Y, lid.Rotation.Z);
            if (beam != null && GodotObject.IsInstanceValid(beam)) beam.QueueFree();
            if (glow != null) glow.Visible = false;
            Level.Current?.RemoveMarker(it);
        };
        return it;
    }

    // ───────────────────────── shrines ─────────────────────────

    private sealed class ShrineKind
    {
        public string Name, Desc; public Element El; public float Dur; public BuffDef[] Buffs;
    }

    private static readonly ShrineKind[] ShrineKinds =
    {
        new() { Name = "怒焰神龕", Desc = "傷害提高 30%", El = Element.Fire, Dur = 35f,
            Buffs = new[] { new BuffDef { Id = "shrine_damage", Name = "怒焰祝福", Stat = Stat.DmgAll, Value = 30f, Dur = 35f } } },
        new() { Name = "鐵誓神龕", Desc = "受到的傷害降低 25%", El = Element.Physical, Dur = 40f,
            Buffs = new[] { new BuffDef { Id = "shrine_guard", Name = "鐵誓祝福", Stat = Stat.DmgReduction, Value = 25f, Dur = 40f } } },
        new() { Name = "疾風神龕", Desc = "移動與攻擊速度提高", El = Element.Lightning, Dur = 35f,
            Buffs = new[] { new BuffDef { Id = "shrine_haste", Name = "疾風祝福", Stat = Stat.MoveSpeed, Value = 25f, Dur = 35f },
                            new BuffDef { Id = "shrine_haste_atk", Name = "疾風祝福", Stat = Stat.AttackSpeed, Value = 20f, Dur = 35f } } },
        new() { Name = "拾遺神龕", Desc = "金幣與尋寶率大幅提高", El = Element.Poison, Dur = 45f,
            Buffs = new[] { new BuffDef { Id = "shrine_fortune", Name = "拾遺祝福", Stat = Stat.GoldFind, Value = 100f, Dur = 45f },
                            new BuffDef { Id = "shrine_fortune_mf", Name = "拾遺祝福", Stat = Stat.MagicFind, Value = 60f, Dur = 45f } } },
        new() { Name = "湧泉神龕", Desc = "施放技能不消耗資源", El = Element.Frost, Dur = 30f,
            Buffs = new[] { new BuffDef { Id = "shrine_flow", Name = "湧泉祝福", FreeCast = true, Dur = 30f } } },
        new() { Name = "殛罰神龕", Desc = "爆擊時迸發爆裂，爆擊機率提高", El = Element.Shadow, Dur = 30f,
            Buffs = new[] { new BuffDef { Id = "shrine_wrath", Name = "殛罰祝福", Stat = Stat.CritChance, Value = 10f, CritBurst = 1.2f, Dur = 30f } } },
    };
    public const int ShrineCount = 6;

    /// <summary>A shrine granting one of six 30-45 second blessings (kind -1 = random).</summary>
    public static Interactable Shrine(Vector2 pos, int kind = -1)
    {
        if (kind < 0 || kind >= ShrineKinds.Length) kind = Rng.R.Next(ShrineKinds.Length);
        var k = ShrineKinds[kind];
        Color col = Palette.Element(k.El);
        var it = Make("shrine", pos, k.Name, col, 2.5f, 2.3f);
        it.Sub = k.Desc;
        var stone = Tex("gothic_stone", Stone);
        Part(it, CylM, stone, new Vector3(0, 0.15f, 0), new Vector3(1.5f, 0.3f, 1.5f));
        Part(it, ConeM, stone, new Vector3(0, 0.95f, 0), new Vector3(0.62f, 1.3f, 0.62f));
        Part(it, CylM, stone, new Vector3(0, 1.66f, 0), new Vector3(0.8f, 0.12f, 0.8f));
        var gem = Part(it, GemM, Glow(col, 3.5f), new Vector3(0, 2.15f, 0), new Vector3(0.34f, 0.58f, 0.34f), default, false);
        var glow = Light(it, col, 1.5f, 7f, 2.1f);
        Block(pos, 0.6f);
        it.OnUse = p =>
        {
            if (p?.Hero == null) return;
            it.CanUse = false; it.ShowLabel = false;
            foreach (var b in k.Buffs) p.Hero.AddBuff(b, k.Dur);
            Fx.BuffAura(p, k.El, k.Dur, true);
            Fx.Burst(it.GlobalPosition + Vector3.Up * 2f, k.El, 3f, 0.8f);
            Snd.At("shrine", it.GlobalPosition);
            Toast($"{k.Name}：{k.Desc}（{k.Dur:0} 秒）", col);
            gem.Visible = false; glow.Visible = false;
            Level.Current?.RemoveMarker(it);
            Log.Info("objects", $"shrine used: {k.Buffs[0].Id}");
        };
        it.Spend = () => { it.CanUse = false; it.ShowLabel = false; gem.Visible = false; glow.Visible = false; Level.Current?.RemoveMarker(it); };
        return it;
    }

    // ───────────────────────── altars ─────────────────────────

    /// <summary>燼火祭壇: a one-time, permanent +1 to every attribute, remembered in the save as the flag <paramref name="id"/>.</summary>
    public static Interactable Altar(Vector2 pos, string id)
    {
        bool spent = Game.I?.Hero != null && Game.I.Hero.D.Has(id);
        var it = Make("altar", pos, "燼火祭壇", Palette.Ember, 2.7f, 1.9f);
        it.Sub = spent ? "餘燼已冷" : "永久提升全屬性";
        it.Tag = id;
        var stone = Tex("big_blocks", Stone);
        Part(it, BoxM, stone, new Vector3(0, 0.14f, 0), new Vector3(2.1f, 0.28f, 2.1f));
        Part(it, BoxM, stone, new Vector3(0, 0.42f, 0), new Vector3(1.5f, 0.28f, 1.5f));
        Part(it, BoxM, stone, new Vector3(0, 0.86f, 0), new Vector3(0.95f, 0.6f, 0.95f));
        Part(it, ConeM, Flat(Brass, 0.45f, 0.85f), new Vector3(0, 1.3f, 0), new Vector3(1.0f, 0.3f, 1.0f), new Vector3(180, 0, 0));
        Block(pos, 0.75f);
        if (spent)
        {
            it.CanUse = false; it.LabelColor = Palette.TextDim;
            Part(it, SphereM, Glow(new Color(0.5f, 0.16f, 0.05f), 0.6f), new Vector3(0, 1.36f, 0), new Vector3(0.5f, 0.14f, 0.5f), default, false);
            return it;
        }
        var fire = Fx.Fire(0.7f, true);
        Attach(it, fire, new Vector3(0, 1.4f, 0));
        it.OnUse = p =>
        {
            var h = p?.Hero;
            if (h == null) return;
            it.CanUse = false; it.Sub = "餘燼已冷"; it.LabelColor = Palette.TextDim;
            if (h.D.Set(id))
            {
                h.Recalc();
                World.Emit("altar", id);
                Toast("燼火祭壇——全屬性永久 +1", Palette.Ember);
            }
            Fx.Burst(it.GlobalPosition + Vector3.Up * 1.4f, Element.Fire, 3.5f, 1f);
            Fx.Heal(p);
            Snd.At("ember_buff", it.GlobalPosition);
            if (fire != null && GodotObject.IsInstanceValid(fire)) fire.QueueFree();
            Level.Current?.RemoveMarker(it);
            Log.Info("objects", $"altar claimed: {id}");
        };
        return it;
    }

    // ───────────────────────── breakables ─────────────────────────

    private static readonly string[] DefaultBreakables = { "barrel", "barrel_old", "crate_a", "crate_b", "urn_a", "urn_b", "clay_pot" };

    private static Node3D BreakableShape(int shape)
    {
        var n = new Node3D { Name = "breakable" };
        if (shape == 0)
        {
            Part(n, CylM, Tex("barrel_wood", Wood), new Vector3(0, 0.5f, 0), new Vector3(0.78f, 1f, 0.78f));
            Part(n, CylM, Flat(Iron, 0.55f, 0.8f), new Vector3(0, 0.28f, 0), new Vector3(0.82f, 0.06f, 0.82f), default, false);
            Part(n, CylM, Flat(Iron, 0.55f, 0.8f), new Vector3(0, 0.74f, 0), new Vector3(0.82f, 0.06f, 0.82f), default, false);
        }
        else if (shape == 1) Part(n, BoxM, Tex("old_planks", Wood), new Vector3(0, 0.42f, 0), new Vector3(0.84f, 0.84f, 0.84f));
        else
        {
            var clay = Flat(new Color(0.5f, 0.33f, 0.22f), 0.8f);
            Part(n, SphereM, clay, new Vector3(0, 0.36f, 0), new Vector3(0.62f, 0.72f, 0.62f));
            Part(n, CylM, clay, new Vector3(0, 0.78f, 0), new Vector3(0.3f, 0.2f, 0.3f));
        }
        return n;
    }

    /// <summary>A barrel, crate or urn standing on a "breakable" prop slot: breaks on use, sometimes hiding a little loot.</summary>
    public static Interactable Breakable(PropSlot slot, LevelTheme theme, Random rng)
    {
        rng ??= Rng.R;
        var names = theme?.Breakables is { Length: > 0 } tb ? tb : DefaultBreakables;
        string name = names[rng.Next(names.Length)] ?? "barrel";
        int shape = name.Contains("barrel") ? 0 : name.Contains("crate") ? 1 : 2;
        string label = shape == 0 ? "木桶" : shape == 1 ? "木箱" : name.Contains("brass") ? "銅甕" : "陶甕";
        var it = Make("breakable", slot.Pos, label, Palette.TextDim, 2.3f, 1.1f, slot.Facing);
        it.ShowLabel = false;
        float scale = slot.Scale > 0.05f ? slot.Scale : 1f;
        var vis = Prop(name) ?? BreakableShape(shape);
        vis.Scale *= scale;
        it.AddChild(vis);
        bool blocks = slot.Blocks;
        Vector2 at = slot.Pos;
        it.OnUse = p =>
        {
            it.CanUse = false;
            Vector3 gp = it.GlobalPosition;
            Snd.At(shape == 2 ? "break_pot" : "break_wood", gp, 0.8f);
            Fx.Dust(gp + Vector3.Up * 0.3f);
            if (blocks) World.Grid?.RemoveObstacle(at);
            float roll = Rng.F();
            int lvl = LootLevel;
            if (roll < 0.16f) Loot.DropGold(Rng.Range(3, 8) * (1 + lvl / 4), at);
            else if (roll < 0.2f) Loot.DropGlobe(at);
            else if (roll < 0.225f && Game.I?.Hero != null)
            {
                var item = Loot.Roll(lvl, 0, Game.I.Hero.Class);
                if (item != null) Loot.DropItem(item, at);
            }
            if (shape != 2 && World.Root != null)
            {
                var debris = Prop(shape == 0 && Rng.Chance(0.5f) ? "barrel_broken_b" : "barrel_stave");
                if (debris != null)
                {
                    World.Root.AddChild(debris);
                    debris.Position = gp;
                    debris.Rotation = new Vector3(0, Rng.Range(0f, Mathf.Tau), 0);
                    debris.Scale *= scale;
                }
            }
            it.QueueFree();
        };
        it.Spend = () => { it.CanUse = false; if (blocks) World.Grid?.RemoveObstacle(at); it.QueueFree(); };
        return it;
    }

    // ───────────────────────── travel: waypoints, beacons, doors, portals ─────────────────────────

    private static void OpenTravelMap() => Game.I?.Ui?.Open("map", "travel");

    /// <summary>The town's travel stone.</summary>
    public static Interactable Waypoint(Vector2 pos)
    {
        var it = Make("waypoint", pos, "燼塔路標", Palette.Gold, 2.8f, 2.6f);
        it.Sub = "前往已重燃的燼塔";
        var stone = Tex("gothic_stone", Stone);
        Part(it, CylM, stone, new Vector3(0, 0.1f, 0), new Vector3(2.6f, 0.2f, 2.6f));
        Part(it, ConeM, stone, new Vector3(0, 1.2f, 0), new Vector3(0.6f, 2.0f, 0.6f));
        Part(it, GemM, Glow(Palette.Ember, 3f), new Vector3(0, 2.5f, 0), new Vector3(0.3f, 0.5f, 0.3f), default, false);
        Part(it, TorusM, Glow(Palette.Ember, 1.6f), new Vector3(0, 0.22f, 0), new Vector3(1.15f, 1f, 1.15f), default, false);
        Light(it, Warm, 1.4f, 7f, 2.3f);
        Block(pos, 0.5f);
        it.OnUse = _ => { Snd.Ui("ui_open"); OpenTravelMap(); };
        return it;
    }

    /// <summary>A region's 燼塔. Dark until its stronghold is cleared, then it burns for good and serves as the waypoint.</summary>
    public static Interactable Beacon(Vector2 pos, RegionDef region, bool lit)
    {
        var it = Make("beacon", pos, "熄滅的燼塔", Palette.TextDim, 3.8f, 4.2f);
        it.Tag = region;
        var tower = PropFit("fort_tower", 7.5f, 4.4f);
        float top = 7.6f;
        if (tower == null)
        {
            tower = new Node3D { Name = "tower" };
            var stone = Tex("castle_blocks", Stone);
            Part(tower, CylM, stone, new Vector3(0, 0.25f, 0), new Vector3(3.6f, 0.5f, 3.6f));
            Part(tower, ConeM, stone, new Vector3(0, 3.2f, 0), new Vector3(2.5f, 5.6f, 2.5f));
            Part(tower, CylM, stone, new Vector3(0, 6.15f, 0), new Vector3(2.3f, 0.3f, 2.3f));
            Part(tower, ConeM, Flat(Iron, 0.5f, 0.8f), new Vector3(0, 6.55f, 0), new Vector3(1.7f, 0.5f, 1.7f), new Vector3(180, 0, 0));
            top = 6.8f;
        }
        it.AddChild(tower);
        it.LabelHeight = Mathf.Min(top, 4.6f);
        it.SetMeta("top", top);
        Block(pos, 1.5f);
        it.OnUse = p =>
        {
            if (it.HasMeta("lit")) { Snd.Ui("ui_open"); OpenTravelMap(); }
            else Warn(region != null ? $"{region.Stronghold}仍被盤踞，燼塔無法重燃" : "燼塔尚未重燃");
        };
        if (lit) LightBeacon(it, false);
        else it.Sub = region != null ? $"肅清{region.Stronghold}以重燃" : "";
        return it;
    }

    /// <summary>Sets a beacon ablaze: fire at its crown, a far-reaching warm light, a new label; with fanfare when it has just been won.</summary>
    public static void LightBeacon(Interactable beacon, bool fanfare)
    {
        if (beacon == null || !GodotObject.IsInstanceValid(beacon) || beacon.HasMeta("lit")) return;
        beacon.SetMeta("lit", true);
        var region = beacon.Tag as RegionDef;
        float top = beacon.HasMeta("top") ? beacon.GetMeta("top").AsSingle() : 7f;
        beacon.Label = region != null ? $"燼塔・{region.Name}" : "燼塔";
        beacon.Sub = "傳送";
        beacon.LabelColor = Palette.Gold;
        Attach(beacon, Fx.Fire(2.4f, true), new Vector3(0, top, 0));
        var l = Light(beacon, Warm, fanfare ? 0.2f : 3.2f, 26f, top + 1.2f);
        Light(beacon, Warm, 1.6f, 9f, 2.4f);
        if (!fanfare) return;
        beacon.CreateTween().TweenProperty(l, "light_energy", 3.2f, 2.5f).SetTrans(Tween.TransitionType.Sine);
        Vector3 gp = beacon.GlobalPosition;
        Fx.Burst(gp + Vector3.Up * top, Element.Fire, 6f, 1.6f);
        Fx.Ring(gp + Vector3.Up * 0.15f, 14f, Palette.Ember, 1.6f);
        Fx.Shake(0.25f, 0.5f);
        Snd.At("beacon_light", gp + Vector3.Up * 2f, 1f);
    }

    /// <summary>The way into a dungeon. Story dungeons glow like a wound; side dungeons take the region's accent.</summary>
    public static Interactable Entrance(Vector2 pos, DungeonDef d, RegionDef region, float facing = 0f)
    {
        Color col = d.Story ? Palette.Danger : region?.Accent ?? Palette.Ember;
        float free = 3f;
        if (BuiltGateways) pos = PortalSpot(pos, 2.4f, out free, 2.5f);
        var it = Make("entrance", pos, d.Name, d.Story ? Palette.Ember : Palette.Text, 3f, BuiltGateways ? 3.9f : 3.2f, facing);
        bool cleared = Game.I?.Hero != null && Game.I.Hero.D.Has("dg_" + d.Id);
        it.Sub = (d.Story ? "首領巢穴" : "地城") + (cleared ? "（已淨化）" : "");
        it.Tag = d;
        // [gfx 0.3, item 11] The gate is the portal: a built gate of the region with a veil filling its opening. (It was
        // an arch of three boxes with a round swirl hung 1.45 m up inside it, half of it in the lintel.)
        if (BuiltGateways)
        {
            Gateway(it, pos, facing, free, col, 0.62f, true);
            Light(it, col, 0.9f, 7f, 1.8f);
        }
        else
        {
            it.AddChild(Arch(Tex("rough_stone_wall", DarkStone), 2.4f, 2.9f));
            Attach(it, Fx.Portal(col, 1.25f), new Vector3(0, 1.45f, 0));
            Light(it, col, 1.3f, 7f, 1.8f);
        }
        it.OnUse = p =>
        {
            bool open = true;
            try { open = Quests.CanEnter(d.Id); } catch (Exception e) { Log.Error("objects", $"CanEnter({d.Id}) failed", e); }
            if (!open) { Warn("這條路尚未向你敞開"); return; }
            Snd.At("portal_open", it.GlobalPosition);
            Log.Info("objects", $"entering dungeon {d.Id}");
            Game.I.EnterArea(d.Id, Level.Current?.AreaId);
        };
        return it;
    }

    /// <summary>A road or stair leading to another area.</summary>
    public static Interactable Exit(Vector2 pos, string toArea, float facing = 0f)
    {
        string name = ZoneDb.AreaName(toArea);
        float free = 3f;
        if (BuiltGateways) pos = PortalSpot(pos, 2.3f, out free, 2.5f);
        var it = Make("exit", pos, $"前往{name}", Palette.Text, 3f, BuiltGateways ? 3.7f : 3.0f, facing);
        it.Tag = toArea;
        Color col = toArea == ZoneDb.Town ? Warm : ZoneDb.Region(toArea)?.Accent ?? new Color(0.7f, 0.8f, 1f);
        // [gfx 0.3, item 11] The gate is the portal (see Gateway). The way back is the dimmer, colder veil.
        if (BuiltGateways) Gateway(it, pos, facing, free, col.Lerp(new Color(0.55f, 0.68f, 1f), 0.5f), 0.45f, false);
        else
        {
            it.AddChild(Arch(Tex("drystone_wall", Stone), 2.4f, 2.7f));
            Attach(it, Fx.Portal(col, 1.15f), new Vector3(0, 1.35f, 0));
        }
        Light(it, col, 0.9f, 6f, 1.8f);
        it.OnUse = p =>
        {
            bool open = true;
            try { open = Quests.CanEnter(toArea); } catch (Exception e) { Log.Error("objects", $"CanEnter({toArea}) failed", e); }
            if (!open) { Warn("前路未明，先完成眼前的使命"); return; }
            Snd.At("portal_open", it.GlobalPosition);
            Game.I.EnterArea(toArea, Level.Current?.AreaId);
        };
        return it;
    }

    /// <summary>A free-standing portal with any behaviour (boss exits, the rift's way out, the town's return portal).</summary>
    public static Interactable Portal(Vector2 pos, string label, string sub, Color c, Action<Player> onUse)
    {
        var it = Make("portal", pos, label, Palette.Gold, 2.8f, 2.9f);
        it.Sub = sub ?? "";
        Part(it, TorusM, Glow(c, 2f), new Vector3(0, 0.08f, 0), new Vector3(1.3f, 1f, 1.3f), default, false);
        Attach(it, Fx.Portal(c, 1.2f), new Vector3(0, BuiltGateways ? 0.02f : 1.4f, 0));       // [gfx 0.3, item 11] on the ground inside its ring (it hung 1.4 m above it)
        Light(it, c, 1.3f, 7f, 1.6f);
        if (it.IsInsideTree())
        {
            Fx.Burst(it.GlobalPosition + Vector3.Up * 1.2f, Element.Lightning, 2.5f, 0.6f);
            Snd.At("portal_open", it.GlobalPosition);
        }
        it.OnUse = onUse;
        return it;
    }

    /// <summary>The barred gate that seals a boss arena: <paramref name="width"/> metres across, panel along local X.</summary>
    public static Node3D Gate(Vector2 pos, float yaw, float width)
    {
        var n = new Node3D { Name = "bossgate", Position = World.To3(pos), Rotation = new Vector3(0, yaw, 0) };
        var gate = PropFit("iron_gate", 3.6f, width + 0.6f);
        if (gate != null) n.AddChild(gate);
        else
        {
            var iron = Tex("rusty_iron", Iron, 0.6f, 0.8f);
            int bars = Mathf.Max(4, Mathf.RoundToInt(width / 0.4f));
            for (int i = 0; i <= bars; i++)
                Part(n, CylM, iron, new Vector3(-width * 0.5f + width * i / bars, 1.7f, 0), new Vector3(0.11f, 3.4f, 0.11f));
            Part(n, BoxM, iron, new Vector3(0, 0.5f, 0), new Vector3(width + 0.2f, 0.14f, 0.16f));
            Part(n, BoxM, iron, new Vector3(0, 2.9f, 0), new Vector3(width + 0.2f, 0.14f, 0.16f));
        }
        Part(n, GemM, Glow(Hush, 2.5f), new Vector3(0, 2.2f, 0), new Vector3(0.36f, 0.5f, 0.2f), default, false);
        Light(n, Hush, 0.8f, 5f, 2.2f);
        World.Root?.AddChild(n);
        return n;
    }

    // ───────────────────────── town services ─────────────────────────

    public static Interactable Stash(Vector2 pos, float facing = 0f)
    {
        var it = Make("stash", pos, "燼藏箱", Palette.Gold, 2.6f, 1.6f, facing);
        it.Sub = "共用倉庫";
        it.AddChild(PropFit("chest", 1.15f, 2f) ?? ChestShape(2, Palette.Gold));
        Part(it, BoxM, Tex("big_blocks", Stone), new Vector3(0, 0.04f, 0), new Vector3(2.2f, 0.08f, 1.5f));
        Block(pos, 0.7f);
        it.OnUse = _ => Game.I?.Ui?.Open("vendor", "stash");
        return it;
    }

    /// <summary>The great brazier at the heart of 殘燈鎮: the town's warm light, and a place to rest.</summary>
    public static Interactable Brazier(Vector2 pos)
    {
        var it = Make("brazier", pos, "大燼火盆", Palette.Ember, 3.6f, 3.2f);
        it.Sub = "在火旁歇息";
        var bowl = PropFit("fire_pit", 1.1f, 3.2f);
        float fireY = 1.0f;
        if (bowl == null) { bowl = Bowl(Tex("big_blocks", Stone), Flat(Iron, 0.5f, 0.85f), 1.3f, 0.7f); fireY = 1.35f; }
        it.AddChild(bowl);
        Attach(it, Fx.Fire(3.2f, false), new Vector3(0, fireY, 0));
        var l = Light(it, Warm, 4.2f, 30f, 3.4f, true);
        l.OmniAttenuation = 1.1f;
        Light(it, new Color(1f, 0.55f, 0.25f), 2.2f, 10f, 1.6f);
        Block(pos, 1.3f);
        Snd.Loop("town_brazier", "fire_loop", it);
        Level.Current?.Loops.Add("town_brazier");
        it.OnUse = p =>
        {
            if (p == null) return;
            p.Heal(p.MaxLife);
            p.Potions = p.Hero.D.PotionCap;
            Fx.Heal(p);
            Snd.At("ember_buff", it.GlobalPosition, 0.8f);
            Toast("燼火的暖意滲入四肢——生命與藥水已補滿", Palette.Ember);
        };
        return it;
    }

    public static Interactable TierStatue(Vector2 pos, float facing = 0f)
    {
        var it = Make("tierstatue", pos, "界階石像", Palette.Gold, 3f, 3.4f, facing);
        it.Sub = "調整世界階級";
        var statue = PropFit("statue_king", 3.6f, 2.4f);
        if (statue == null)
        {
            statue = new Node3D { Name = "statue" };
            var stone = Tex("worn_marble", Stone);
            Part(statue, BoxM, stone, new Vector3(0, 0.4f, 0), new Vector3(1.4f, 0.8f, 1.4f));
            Part(statue, ConeM, stone, new Vector3(0, 1.8f, 0), new Vector3(0.9f, 2.0f, 0.7f));
            Part(statue, SphereM, stone, new Vector3(0, 3.05f, 0), new Vector3(0.5f, 0.56f, 0.5f));
        }
        it.AddChild(statue);
        Part(it, GemM, Glow(Palette.Danger, 2.5f), new Vector3(0, 0.95f, 0.75f), new Vector3(0.2f, 0.3f, 0.12f), default, false);
        Light(it, new Color(1f, 0.4f, 0.3f), 0.8f, 5f, 1.4f);
        Block(pos, 0.8f);
        it.OnUse = _ => Game.I?.Ui?.Open("tier");
        return it;
    }

    public static Interactable RiftStone(Vector2 pos)
    {
        var it = Make("riftstone", pos, "裂隙石", Hush, 3f, 3.1f);
        it.Sub = "開啟深淵裂隙";
        var obsidian = Tex("obsidian", DarkStone, 0.35f, 0.2f);
        Part(it, CylM, obsidian, new Vector3(0, 0.12f, 0), new Vector3(2.4f, 0.24f, 2.4f));
        Part(it, ConeM, obsidian, new Vector3(-0.2f, 1.4f, 0), new Vector3(0.8f, 2.6f, 0.6f), new Vector3(0, 0, 6));
        Part(it, ConeM, obsidian, new Vector3(0.55f, 0.85f, 0.1f), new Vector3(0.5f, 1.5f, 0.45f), new Vector3(0, 0, -12));
        Part(it, BoxM, Glow(Hush, 3.5f), new Vector3(-0.2f, 1.5f, 0.26f), new Vector3(0.07f, 1.7f, 0.05f), new Vector3(0, 0, 6), false);
        if (BuiltGateways) Attach(it, Fx.Portal(Hush, 0.5f, false), new Vector3(0.62f, 2.3f, 0.1f));
        else Attach(it, Fx.Portal(Hush, 0.5f), new Vector3(0.25f, 2.3f, 0));     // [gfx 0.3, item 11] a vortex held over the lesser shard, clear of both stones (no pool in mid-air)
        Light(it, Hush, 1.2f, 6f, 1.8f);
        Block(pos, 0.8f);
        it.OnUse = _ => Game.I?.Ui?.Open("rift");
        return it;
    }

    public static Interactable TownGate(Vector2 pos, float facing = 0f)
    {
        var it = Make("gate", pos, "鎮門", Palette.Text, 3.4f, 3.6f, facing);
        it.Sub = "啟程前往荒野";
        it.AddChild(PropFit("castle_door", 4.2f, 5f) ?? Arch(Tex("castle_blocks", Stone), 3.2f, 3.4f, 0.9f));
        Light(it, Warm, 1.0f, 7f, 3f);
        it.OnUse = _ => { Snd.Ui("ui_open"); OpenTravelMap(); };
        return it;
    }

    // ───────────────────────── people ─────────────────────────

    private sealed class NpcDef { public string Name, Role, Model, Anim, Hint, Idle; public bool Service; }

    // The clip is part of the model: the elder's lantern is rigid in his right hand and only hangs right in Idle_Lantern_Loop,
    // the smith's hammer rests on his shoulder in Idle_FoldArms_Loop (Idle_Talking_Loop opens that hand), the occultist stands
    // with folded arms. They keep these clips while they talk as well — nothing may switch a named NPC to another idle.
    private static readonly Dictionary<string, NpcDef> NpcDefs = new()
    {
        ["elder"] = new() { Name = "艾爾溫", Role = "守燈長老", Model = "npc_elder", Anim = "Idle_Lantern_Loop", Hint = "交談",
            Idle = "燈還亮著，孩子。只要還有人添燼，灰暮就吞不下我們。" },
        ["smith"] = new() { Name = "布蘭", Role = "鐵匠", Model = "npc_smith", Anim = "Idle_FoldArms_Loop", Hint = "強化・拆解", Service = true },
        ["merchant"] = new() { Name = "莉雅", Role = "行商", Model = "npc_merchant", Anim = "Idle_Talking_Loop", Hint = "買賣", Service = true },
        ["occultist"] = new() { Name = "薇菈", Role = "祕術師", Model = "npc_occultist", Anim = "Idle_FoldArms_Loop", Hint = "附魔・銘刻", Service = true },
        ["jeweler"] = new() { Name = "蔻拉", Role = "寶石匠", Model = "npc_villager_f", Anim = "Idle_Talking_Loop", Hint = "寶石・鑲孔", Service = true },
    };

    private static readonly string[] VillagerNames = { "老葛", "米拉", "托本", "漢娜", "小烏", "賽門", "艾達", "柯林" };
    private static readonly string[] VillagerAnims = { "Idle_Loop", "Idle_FoldArms_Loop", "Idle_Talking_Loop", "Idle_Torch_Loop" };
    private static readonly string[] VillagerLines =
    {
        "昨夜又有一盞燈滅了……願燼火庇佑我們。",
        "灰落得比雪還密。你從外頭回來，可曾見過太陽的樣子？",
        "布蘭的爐子還熱著，那就還不算太糟。",
        "別在鎮外待到入夜。荒原上的東西，聞得到活人的暖意。",
        "持燼者……我祖母說過這個詞。原來不只是傳說。",
        "長老每晚都守著火盆。我從沒見他闔過眼。",
        "教團的人說寂靜才是救贖。可我只想再聽一次孩子們的笑聲。",
        "要是你路過墳塚，替我在東邊那塊無名碑前放一撮灰。",
    };
    private static readonly string[] CampLines =
    {
        "多虧了你，這裡的火又能燒下去了。",
        "燼塔亮起的那一夜，我終於睡了個整覺。",
        "我們會守住這裡。你只管往前走。",
    };
    private static readonly string[] FreedLines =
    {
        "我以為再也見不到火光了……謝謝你。",
        "他們要我們安靜，永遠安靜。我這就回鎮上去。",
        "願燼火照著你的路，持燼者。",
        "還有其他人被關著，求你別丟下他們。",
    };

    /// <summary>Display name of an NPC role, e.g. "鐵匠 布蘭" (used by the map too).</summary>
    public static string NpcTitle(string id) => NpcDefs.TryGetValue(id ?? "", out var d) ? $"{d.Role} {d.Name}" : "鎮民";

    private static (Interactable it, CharModel model) Person(string kind, Vector2 pos, float facing, string modelId, string anim, string label, Color col)
    {
        var it = Make(kind, pos, label, col, 2.6f, 2.1f);
        var model = CharModel.Create(modelId);
        it.AddChild(model);
        model.Rotation = new Vector3(0, facing, 0);
        model.ShowWeapons(false);
        model.Play(anim, 1f, 0f);
        it.LabelHeight = model.Height + 0.25f;
        Block(pos, 0.42f);
        return (it, model);
    }

    /// <summary>
    /// A town NPC. Talking asks the quest system first; a line of dialogue plays and is followed by the NPC's
    /// service panel (smith, merchant, occultist, jeweler). The elder only talks; villagers offer a passing remark.
    /// </summary>
    public static Interactable Npc(Vector2 pos, string id, float facing, Random rng)
    {
        rng ??= Rng.R;
        id = string.IsNullOrEmpty(id) ? "villager" : id;
        var lv = Level.Current;
        if (!NpcDefs.TryGetValue(id, out var def))
        {
            string vName = VillagerNames[rng.Next(VillagerNames.Length)];
            string line = VillagerLines[rng.Next(VillagerLines.Length)];
            var (v, vm) = Person("npc", pos, facing, rng.Next(2) == 0 ? "npc_villager_m" : "npc_villager_f", VillagerAnims[rng.Next(VillagerAnims.Length)], vName, Palette.Text);
            v.Sub = "鎮民"; v.Tag = "villager";
            v.OnUse = p => { p?.Stop(); Game.I?.Hud?.Subtitle(vName, line, 4.5f); };
            lv?.Npcs.Add(new NpcRef { Id = "villager", Obj = v, Model = vm, BaseYaw = facing });
            return v;
        }
        string title = $"{def.Role} {def.Name}";
        var (it, model) = Person("npc", pos, facing, def.Model, def.Anim, title, Palette.Gold);
        it.Sub = def.Hint; it.Tag = id;
        float markY = model.Height + 0.75f;
        var mark = Part(it, GemM, Glow(Palette.Gold, 4f), new Vector3(0, markY, 0), new Vector3(0.26f, 0.44f, 0.26f), default, false);
        mark.Visible = false;
        lv?.Npcs.Add(new NpcRef { Id = id, Obj = it, Model = model, QuestMark = mark, BaseYaw = facing, MarkY = markY });
        it.OnUse = p =>
        {
            p?.Stop();
            DialogueScript s = null;
            try { s = Quests.TalkTo(id); } catch (Exception e) { Log.Error("npc", $"TalkTo({id}) failed", e); }
            World.Emit("talk", id);
            Action service = def.Service ? () => Game.I?.Ui?.Open("vendor", id) : null;
            if (s != null && s.Lines != null && s.Lines.Count > 0)
            {
                if (service != null)
                {
                    var end = s.OnEnd;                       // restored when it runs, so a shared script is never wrapped twice over
                    s.OnEnd = () => { s.OnEnd = end; end?.Invoke(); service(); };
                }
                Game.I?.Ui?.Open("dialogue", s);
            }
            else if (service != null) service();
            else Game.I?.Hud?.Subtitle(title, def.Idle ?? "……", 5f);
            Log.Info("npc", $"talk {id} dialogue={(s != null ? s.Id : "-")}");
        };
        return it;
    }

    /// <summary>A survivor keeping a liberated camp: a little human warmth beside the relit beacon.</summary>
    public static Interactable CampFolk(Vector2 pos, float facing, Random rng)
    {
        rng ??= Rng.R;
        string line = CampLines[rng.Next(CampLines.Length)];
        var (it, model) = Person("npc", pos, facing, rng.Next(2) == 0 ? "npc_villager_m" : "npc_villager_f", VillagerAnims[rng.Next(VillagerAnims.Length)], "守營人", Palette.Text);
        it.Sub = "倖存者"; it.Tag = "villager";
        it.OnUse = p => { p?.Stop(); Game.I?.Hud?.Subtitle("守營人", line, 4.5f); };
        Level.Current?.Npcs.Add(new NpcRef { Id = "villager", Obj = it, Model = model, BaseYaw = facing });
        return it;
    }

    // ───────────────────────── dungeon objectives ─────────────────────────

    /// <summary>寂滅圖騰: toppled by standing beside it through a 1.5 second channel.</summary>
    public static Interactable Idol(Vector2 pos, Action onToppled)
    {
        var it = Make("idol", pos, "寂滅圖騰", Hush, 2.6f, 3.3f);
        it.Sub = "推倒（引導 1.5 秒）";
        var stone = Tex("obsidian_slabs", DarkStone, 0.5f);
        Part(it, CylM, stone, new Vector3(0, 0.1f, 0), new Vector3(1.7f, 0.2f, 1.7f));
        var pivot = new Node3D { Name = "pivot", Position = new Vector3(0, 0.2f, 0) };
        it.AddChild(pivot);
        Part(pivot, ConeM, stone, new Vector3(0, 1.4f, 0), new Vector3(0.8f, 2.8f, 0.8f));
        Part(pivot, BoxM, stone, new Vector3(0, 2.1f, 0), new Vector3(1.5f, 0.22f, 0.3f));
        var eye = Part(pivot, SphereM, Glow(Hush, 4f), new Vector3(0, 2.1f, 0.2f), new Vector3(0.32f, 0.32f, 0.14f), default, false);
        var glow = Light(pivot, Hush, 1.4f, 7f, 2.2f);
        Block(pos, 0.7f);
        it.OnUse = p =>
        {
            Action done = () =>
            {
                if (!GodotObject.IsInstanceValid(it) || !it.CanUse) return;
                it.CanUse = false; it.ShowLabel = false;
                eye.Visible = false; glow.Visible = false;
                it.CreateTween().TweenProperty(pivot, "rotation:z", 1.42f, 0.7f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
                Vector3 gp = it.GlobalPosition;
                Fx.Burst(gp + Vector3.Up * 1.5f, Element.Shadow, 3.5f, 1f);
                Fx.Shake(0.2f, 0.25f);
                Snd.At("earth_impact", gp);
                Level.Current?.RemoveMarker(it);
                Log.Info("objective", "idol toppled");
                onToppled?.Invoke();
            };
            var lv = Level.Current;
            if (lv != null) lv.BeginChannel(it, 1.5f, "推倒圖騰", done); else done();
        };
        it.Spend = () =>
        {
            it.CanUse = false; it.ShowLabel = false;
            eye.Visible = false; glow.Visible = false;
            pivot.Rotation = new Vector3(pivot.Rotation.X, pivot.Rotation.Y, 1.42f);
            Level.Current?.RemoveMarker(it);
        };
        return it;
    }

    /// <summary>A caged survivor. Opening the cage frees them; they thank the hero and slip away.</summary>
    public static Interactable Prisoner(Vector2 pos, Action onFreed, Random rng)
    {
        rng ??= Rng.R;
        var it = Make("prisoner", pos, "被囚的倖存者", Palette.Good, 2.7f, 2.6f);
        it.Sub = "打開牢籠";
        var iron = Tex("rusty_iron", Iron, 0.6f, 0.8f);
        Part(it, BoxM, iron, new Vector3(0, 0.04f, 0), new Vector3(1.8f, 0.08f, 1.8f));
        var cage = new Node3D { Name = "cage" };
        it.AddChild(cage);
        Part(cage, BoxM, iron, new Vector3(0, 2.24f, 0), new Vector3(1.8f, 0.08f, 1.8f));
        for (int i = 0; i < 16; i++)
        {
            int side = i / 4; float t = -0.84f + (i % 4) * 0.42f + (side >= 2 ? 0.42f : 0f);
            Vector3 at = side switch { 0 => new Vector3(t, 1.14f, -0.84f), 1 => new Vector3(0.84f, 1.14f, t), 2 => new Vector3(-t, 1.14f, 0.84f), _ => new Vector3(-0.84f, 1.14f, -t) };
            Part(cage, CylM, iron, at, new Vector3(0.07f, 2.2f, 0.07f), default, i % 4 == 0);
        }
        var model = CharModel.Create(rng.Next(2) == 0 ? "npc_villager_m" : "npc_villager_f");
        it.AddChild(model);
        model.Rotation = new Vector3(0, (float)rng.NextDouble() * Mathf.Tau, 0);
        model.ShowWeapons(false);
        model.Play("Sitting_Idle_Loop", 1f, 0f);
        Block(pos, 0.85f);
        string line = FreedLines[rng.Next(FreedLines.Length)];
        it.OnUse = p =>
        {
            it.CanUse = false; it.ShowLabel = false;
            Vector3 gp = it.GlobalPosition;
            Snd.At("door_open", gp, 0.8f);
            Snd.At("hit_metal", gp, 0.6f);
            var tw = it.CreateTween();
            tw.TweenProperty(cage, "position:y", -2.4f, 0.6f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(cage)) cage.QueueFree(); }));
            World.Grid?.RemoveObstacle(pos);
            if (p != null) model.Rotation = new Vector3(0, Mathf.Atan2(p.Pos2.X - pos.X, p.Pos2.Y - pos.Y), 0);
            model.Play("Idle_Talking_Loop", 1f, 0.3f);
            Game.I?.Hud?.Subtitle("倖存者", line, 4f);
            Level.Current?.RemoveMarker(it);
            Log.Info("objective", "prisoner freed");
            onFreed?.Invoke();
            Action leave = () =>
            {
                if (!GodotObject.IsInstanceValid(it)) return;
                Fx.Teleport(it.GlobalPosition + Vector3.Up, Element.Fire);
                it.QueueFree();
            };
            var lv = Level.Current;
            if (lv != null) lv.After(4f, leave); else leave();
        };
        it.Spend = () => { it.CanUse = false; it.ShowLabel = false; World.Grid?.RemoveObstacle(pos); Level.Current?.RemoveMarker(it); it.QueueFree(); };
        return it;
    }

    /// <summary>The altar by the boss door where collected 燼晶 are offered.</summary>
    public static Interactable EmberAltar(Vector2 pos, Func<bool> ready, Action onOffer)
    {
        var it = Make("emberaltar", pos, "燼晶祭壇", Palette.Ember, 2.8f, 2.0f);
        it.Sub = "獻上燼晶以解開封印";
        var stone = Tex("big_blocks", Stone);
        Part(it, BoxM, stone, new Vector3(0, 0.2f, 0), new Vector3(1.8f, 0.4f, 1.8f));
        Part(it, BoxM, stone, new Vector3(0, 0.75f, 0), new Vector3(1.0f, 0.7f, 1.0f));
        Part(it, ConeM, Flat(Brass, 0.45f, 0.85f), new Vector3(0, 1.26f, 0), new Vector3(1.1f, 0.32f, 1.1f), new Vector3(180, 0, 0));
        Part(it, SphereM, Glow(new Color(0.6f, 0.2f, 0.05f), 0.8f), new Vector3(0, 1.34f, 0), new Vector3(0.55f, 0.14f, 0.55f), default, false);
        Light(it, Palette.Ember, 0.7f, 5f, 1.7f);
        Block(pos, 0.75f);
        it.OnUse = p =>
        {
            if (ready != null && !ready()) { Warn("燼晶還不夠"); return; }
            it.CanUse = false; it.Sub = "燼火已燃";
            Attach(it, Fx.Fire(1.1f, true), new Vector3(0, 1.4f, 0));
            Fx.Burst(it.GlobalPosition + Vector3.Up * 1.4f, Element.Fire, 4f, 1.2f);
            Snd.At("ember_buff", it.GlobalPosition);
            Level.Current?.RemoveMarker(it);
            onOffer?.Invoke();
        };
        it.Spend = () =>
        {
            it.CanUse = false; it.Sub = "燼火已燃";
            Attach(it, Fx.Fire(1.1f, true), new Vector3(0, 1.4f, 0));
            Level.Current?.RemoveMarker(it);
        };
        return it;
    }

    /// <summary>A 燼晶 left by a slain carrier; gathered by walking over it.</summary>
    public static Interactable EmberShard(Vector2 pos, Action onPick)
    {
        var it = Make("ember", pos, "燼晶", Palette.Ember, 2f, 1f);
        it.ShowLabel = false; it.AutoRadius = 2.2f;
        Part(it, GemM, Glow(Palette.Ember, 4f), new Vector3(0, 0.55f, 0), new Vector3(0.3f, 0.52f, 0.3f), new Vector3(12, 0, 8), false);
        Light(it, Palette.Ember, 0.8f, 3.5f, 0.7f);
        it.OnUse = p =>
        {
            Vector3 gp = it.GlobalPosition;
            it.Consume();
            Snd.At("item_pickup", gp, 0.8f);
            Fx.Hit(gp + Vector3.Up * 0.6f, Element.Fire, false);
            onPick?.Invoke();
        };
        return it;
    }

    // ───────────────────────── world event pieces ─────────────────────────

    public const float EventBowlTop = 1.2f;

    public static Interactable DyingBrazier(Vector2 pos, Action<Player> onUse)
    {
        var it = Make("event", pos, "將熄的火盆", Palette.Gold, 2.8f, 2.0f);
        it.Sub = "添燼，喚回火光";
        it.AddChild(Bowl(Tex("big_blocks", Stone), Flat(Iron, 0.5f, 0.85f), 0.7f, 0.6f));
        Block(pos, 0.75f);
        it.OnUse = onUse;
        return it;
    }

    public static Interactable CursedChest(Vector2 pos, Action<Player> onUse)
    {
        var it = Make("event", pos, "受詛的寶箱", Hush, 2.5f, 1.4f);
        it.Sub = "開啟——後果自負";
        it.AddChild(PropFit("chest", 0.95f, 1.6f) ?? ChestShape(2, Hush));
        Part(it, TorusM, Glow(Hush, 2.2f), new Vector3(0, 0.06f, 0), new Vector3(1.5f, 1f, 1.5f), default, false);
        Light(it, Hush, 1.3f, 6f, 1.3f);
        Block(pos, 0.55f);
        it.OnUse = onUse;
        return it;
    }

    /// <summary>The circle a ritual is cast in. The child named "orb" is grown by the event as the ritual nears completion.</summary>
    public static Node3D RitualCircle(Vector2 pos, float radius)
    {
        var n = new Node3D { Name = "ritual", Position = World.To3(pos, 0.05f) };
        Part(n, TorusM, Glow(Hush, 2.4f), Vector3.Zero, new Vector3(radius, 1f, radius), default, false);
        Part(n, TorusM, Glow(Hush, 1.4f), Vector3.Zero, new Vector3(radius * 0.55f, 1f, radius * 0.55f), default, false);
        var orb = Part(n, SphereM, Glow(Hush, 5f), new Vector3(0, 1.6f, 0), Vector3.One * 0.3f, default, false);
        orb.Name = "orb";
        Light(n, Hush, 1.6f, 9f, 1.8f);
        World.Root?.AddChild(n);
        return n;
    }
}
