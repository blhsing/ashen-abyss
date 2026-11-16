using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Everything the level renderer needs to know about the look of one kind of place: texture sets, sky, fog, light,
/// weather and the prop vocabulary. Entries of the prop lists are either model folder names under
/// res://assets/env/models or, when they start with '@', a procedural set-dressing kind built by <see cref="EnvDress"/>.
/// </summary>
public sealed class LevelTheme
{
    private static readonly string[] None = Array.Empty<string>();

    // ── contract (ARCHITECTURE.md 3.4) ──
    public string Id, Ground, GroundAlt, Path, RoomFloor, ArenaFloor, WallTex, WallTopTex, PitKind = "none" /* lava|water|void|none */, Sky, Weather = "none", Footstep = "footstep_stone";
    public bool Outdoor;
    public float WallHeight = 4.6f, FogDensity = 0.015f, AmbientEnergy = 0.5f, SunEnergy, TorchRange = 11f;
    public Color Ambient = new(0.3f, 0.34f, 0.44f), FogColor = new(0.2f, 0.22f, 0.27f), SunColor = new(0.6f, 0.68f, 0.9f), Accent = new(0.6f, 0.7f, 1f), TorchColor = new(1f, 0.56f, 0.24f);
    public Vector3 SunDir = new Vector3(-0.45f, -0.72f, -0.52f).Normalized();   // the direction the light travels
    public string[] Trees = None, Rocks = None, BigRocks = None, Clutter = None, Breakables = None, Furniture = None, Lights = None, Pillars = None, Structures = None, Foliage = None;

    // ── renderer extras ──
    /// <summary>Outdoor edge treatment: forest | cliff | mixed | walls | lowwall | palisade.</summary>
    public string Border = "forest";
    /// <summary>For "mixed" borders: the share of the edge that is rock rather than tree line.</summary>
    public float CliffShare = 0.3f;
    /// <summary>Rock set (triplanar) of outdoor cliff masses.</summary>
    public string CliffTex = "grey_rock";
    /// <summary>Chance that a border cell of a tree line holds a big rock instead of a tree.</summary>
    public float BorderRocks = 0.12f;
    /// <summary>Indoor walls are irregular cave rock instead of dressed masonry.</summary>
    public bool Organic;
    /// <summary>Linear multiplier on every ground layer after automatic levelling, and how far albedo is pulled to grey.</summary>
    public Color GroundTint = new(1f, 1f, 1f), WallTint = new(1f, 1f, 1f), PropTint = new(0.82f, 0.82f, 0.84f);
    public float Desaturate = 0.4f;
    /// <summary>Target band for the average linear albedo of ground and wall textures (keeps regions in their value range).</summary>
    public float GroundLumMin = 0.055f, GroundLumMax = 0.17f, WallLumMin = 0.04f, WallLumMax = 0.13f;
    /// <summary>Snow / ash / moss / sand settled on up-facing surfaces of rocks, walls and props.</summary>
    public Color TopColor = new(0.4f, 0.4f, 0.4f);
    public float TopAmount;
    /// <summary>Deep water colour, void mist colour or lava glow.</summary>
    public Color PitColor = new(0.02f, 0.03f, 0.04f);
    public float PitGlow;
    public float SkyEnergy = 0.4f, FogHeight = 0.6f, FogHeightDensity = 0.25f, VolumetricDensity = 0.018f;
    public float Exposure = 1.05f, Saturation = 0.88f, Contrast = 1.08f, GlowIntensity = 0.7f;
    public Color WeatherColor = new(0.5f, 0.5f, 0.5f);
    public float WeatherAmount = 1f;
    public Vector2 Wind = new(0.6f, 0.25f);                   // metres per second on the xz plane
    // ── look (lighting pass) ──
    /// <summary>How wet the ground is: puddles and damp patches that darken the albedo and turn glossy (0 dry … 1 soaked).</summary>
    public float Wet = 0.2f;
    /// <summary>Roughness multiplier of the whole ground (polished marble, ice) and of masonry walls.</summary>
    public float GroundGloss = 1f, WallGloss = 1f;
    /// <summary>Ground mist: the slab of volumetric fog hugging the floor. Density is per metre; Top is where it has thinned out.</summary>
    public float MistDensity = 0.03f, MistTop = 2.6f;
    /// <summary>Colour of the mist (scattering albedo). Black = derive from the fog colour.</summary>
    public Color MistColor = new(0f, 0f, 0f);
    /// <summary>Opacity the distance fog reaches far behind the play plane (0 … 1): what the background dissolves into.</summary>
    public float FogFar = 0.6f;
    /// <summary>Energy of the cool back light that follows the hero (0 = off) and its colour (black = the sun colour).</summary>
    /// <summary>Character readability: multiplier on the extra ambient only characters receive, and on their back-light rim.</summary>
    public float ActorFill = 1f, ActorRim = 1f;
    /// <summary>Scales the energy (and a little of the reach) of every fire and lamp of the theme: the town is the best lit place in the game.</summary>
    public float TorchBoost = 1f;
    public float RimEnergy = 1.0f;
    public Color RimColor = new(0f, 0f, 0f);
    /// <summary>Vignette strength and split-toning strength (shadows towards the ambient hue, highlights towards fire).</summary>
    public float Vignette = 0.42f, Grade = 0.05f;
    /// <summary>Elevation of the sun or moon above the horizon in degrees (0 = keep SunDir as written). The light always comes from behind the scene, towards the lens.</summary>
    public float SunElevation = 40f;
    /// <summary>Share of open floor cells that get a piece of small ground clutter (pebbles, twigs, bones), and what to draw it from.</summary>
    public float Scatter = 0.12f;
    public string[] ScatterProps = None;
    /// <summary>Texture sets the procedural dressing builds with.</summary>
    public string DressStone = "big_blocks", DressStoneAlt = "castle_blocks", DressWood = "rough_beam", DressPlank = "old_planks", DressMetal = "rusty_iron", DressRoof = "roof_tiles";

    public LevelTheme Clone() => (LevelTheme)MemberwiseClone();

    public string[] List(string category) => category switch
    {
        "tree" => Trees,
        "rock" => Rocks,
        "rock_big" => BigRocks,
        "clutter" => Clutter,
        "breakable" => Breakables,
        "furniture" => Furniture,
        "light" => Lights,
        "pillar" => Pillars,
        "structure" => Structures,
        "foliage" => Foliage,
        _ => None,
    };
}

public static class LevelThemes
{
    // fire light between roughly 1800 K and 2200 K (declared before All: static initialisers run in textual order)
    // (true black-body colours at these temperatures are almost pure red-orange and turn every stone wall crimson
    // under the ACES curve; these keep the order and the warmth but leave enough green and blue for materials to read)
    private static readonly Color Fire1800 = new(1f, 0.55f, 0.24f), Fire2000 = new(1f, 0.62f, 0.32f), Fire2200 = new(1f, 0.69f, 0.4f);
    private static readonly HashSet<string> Warned = new();
    private static readonly Dictionary<string, LevelTheme> All = Build();

    public static IEnumerable<string> Ids => All.Keys;

    public static LevelTheme Get(string id)
    {
        if (id != null && All.TryGetValue(id, out var t)) return t;
        if (Warned.Add(id ?? "<null>")) Log.Warn("theme", $"unknown level theme '{id}', using 'wastes'");
        return All["wastes"];
    }

    /// <summary>Theme of an area id: "town", a region id (its overworld), a dungeon id or "rift:&lt;tier&gt;".</summary>
    public static LevelTheme ForArea(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) return Get("wastes");
        if (areaId == ZoneDb.Town) return Get("town");
        if (areaId.StartsWith("rift", StringComparison.Ordinal)) return Get("rift");
        var dungeon = ZoneDb.Dungeon(areaId);
        if (dungeon != null) return Get(dungeon.Theme);
        var region = ZoneDb.Region(areaId);
        if (region != null) return Get(region.Theme);
        if (All.ContainsKey(areaId)) return All[areaId];
        return Get(areaId);
    }

    private static Color C(float r, float g, float b) => new(r, g, b);
    private static Vector3 Dir(float x, float y, float z) => new Vector3(x, y, z).Normalized();
    private static string[] L(params string[] s) => s;

    private static Dictionary<string, LevelTheme> Build()
    {
        var d = new Dictionary<string, LevelTheme>();
        void A(LevelTheme t) => d[t.Id] = t;

        // ───────────────────────── 殘燈鎮: cobbles, timber and plaster, warm lanterns ─────────────────────────
        A(new LevelTheme
        {
            Id = "town", TorchBoost = 1.9f, Vignette = 0.45f, Scatter = 0.04f, RimEnergy = 0.8f, Grade = 0.06f, Wet = 0.3f, MistColor = C(0.9f, 0.7f, 0.5f), MistTop = 2.2f, MistDensity = 0.02f, FogFar = 0.55f, SunElevation = 38f, Outdoor = true, Border = "palisade",
            Ground = "packed_dirt", GroundAlt = "dead_grass", Path = "cobble_street", RoomFloor = "flagstone", ArenaFloor = "flagstone",
            WallTex = "rough_stone_wall", WallTopTex = "old_planks", CliffTex = "grey_rock", PitKind = "water", Sky = "dusk", Weather = "ash", Footstep = "footstep_dirt",
            WallHeight = 3.2f, FogDensity = 0.006f, AmbientEnergy = 0.644f, SunEnergy = 0.386f, TorchRange = 12.5f,
            Ambient = C(0.3f, 0.33f, 0.42f), FogColor = C(0.2f, 0.15f, 0.12f), SunColor = C(0.55f, 0.62f, 0.82f), Accent = C(1f, 0.72f, 0.36f), TorchColor = Fire2200,
            SunDir = Dir(-0.42f, -0.74f, -0.52f),
            GroundTint = C(0.95f, 0.92f, 0.9f), Desaturate = 0.35f, TopColor = C(0.36f, 0.35f, 0.34f), TopAmount = 0.1f,
            PitColor = C(0.012f, 0.016f, 0.022f), SkyEnergy = 0.45f, FogHeight = 0.3f, FogHeightDensity = 0.12f, VolumetricDensity = 0.012f,
            Saturation = 0.95f, WeatherColor = C(0.46f, 0.44f, 0.42f), WeatherAmount = 0.3f, Wind = new Vector2(0.5f, 0.2f),
            DressStone = "rough_stone_wall", DressStoneAlt = "castle_blocks", DressWood = "rough_beam", DressPlank = "old_planks", DressRoof = "roof_tiles",
            Trees = L("dead_tree_b", "dead_pine_a", "dead_pine_b", "dead_trunk"),
            Rocks = L("rock_small_a", "rock_small_b", "rock_g", "rock_h"),
            BigRocks = L("boulder_a", "rock_i", "rock_j"),
            Clutter = L("barrel_broken_a", "barrel_lid", "bucket", "basket_a", "basket_b", "broom", "spade", "axe", "tub", "saw", "bowl_a", "trough_a", "barrel_stave"),
            Breakables = L("barrel", "barrel_old", "crate_a", "crate_b", "clay_pot", "jug"),
            Furniture = L("bench", "table_trestle", "picnic_table", "chair", "stool_a", "trough_b", "cupboard", "ladder_a", "sign_board", "camp_stool"),
            Lights = L("lamp_post", "lamp_post", "lantern_wood", "fire_barrel"),
            Pillars = L("@post", "@well"),
            Structures = L("@house", "@house", "@house", "@stall", "@well", "@tent"),
            Foliage = L("dry_bush_a", "dry_bush_b", "dead_bush_a", "dead_bush_c"),
        });

        // ───────────────────────── 灰燼荒原: ash-grey dead forest, cold dusk, drifting ash ─────────────────────────
        A(new LevelTheme
        {
            Id = "wastes", GroundLumMax = 0.13f, Scatter = 0.16f, Wet = 0.12f, MistTop = 2.2f, MistDensity = 0.035f, FogFar = 0.7f, SunElevation = 34f, Outdoor = true, Border = "mixed", CliffShare = 0.22f,
            Ground = "ash_ground", GroundAlt = "dead_leaves", Path = "gravel_path", RoomFloor = "packed_dirt", ArenaFloor = "burnt_soil",
            WallTex = "grey_rock", WallTopTex = "grey_rock", CliffTex = "grey_rock", PitKind = "water", Sky = "dusk", Weather = "ash", Footstep = "footstep_dirt",
            WallHeight = 4f, FogDensity = 0.011f, AmbientEnergy = 0.616f, SunEnergy = 0.678f, TorchRange = 11f,
            Ambient = C(0.3f, 0.34f, 0.43f), FogColor = C(0.22f, 0.26f, 0.34f), SunColor = C(0.7f, 0.75f, 0.88f), Accent = C(0.55f, 0.7f, 1f), TorchColor = Fire2000,
            SunDir = Dir(-0.5f, -0.66f, -0.56f),
            GroundTint = C(0.96f, 0.97f, 1f), Desaturate = 0.62f, TopColor = C(0.34f, 0.335f, 0.33f), TopAmount = 0.5f, PropTint = C(0.72f, 0.73f, 0.76f),
            PitColor = C(0.01f, 0.014f, 0.02f), SkyEnergy = 0.4f, FogHeight = 0.8f, FogHeightDensity = 0.3f, VolumetricDensity = 0.02f,
            Saturation = 0.8f, WeatherColor = C(0.44f, 0.44f, 0.45f), WeatherAmount = 1f, Wind = new Vector2(0.9f, 0.35f),
            DressStone = "drystone_wall", DressStoneAlt = "mossy_stone", DressWood = "rough_beam", DressPlank = "old_planks",
            Trees = L("dead_tree_a", "dead_tree_b", "dead_tree_big", "dead_pine_a", "dead_pine_b", "dead_pine_c", "dead_trunk", "dead_quiver"),
            Rocks = L("rock_a", "rock_c", "rock_d", "rock_g", "rock_h", "boulder_d", "boulder_e"),
            BigRocks = L("boulder_b", "boulder_c", "cliff_b", "rock_cluster", "boulder_a"),
            Clutter = L("branch_a", "branch_b", "branch_c", "root_piece_a", "dead_stub_a", "dead_stub_b", "rock_small_a", "rock_small_b", "@headstone", "@rubble", "spade"),
            Breakables = L("barrel_old", "crate_a", "urn_a", "jug", "clay_pot"),
            Furniture = L("@grave_slab", "@headstone", "fallen_log_a", "stump_a", "stump_b", "camp_stool"),
            Lights = L("@torch", "@torch", "fire_pit", "lantern_wood"),
            Pillars = L("dead_trunk", "@ruin_pillar", "@obelisk"),
            Structures = L("@ruin_wall", "@grave_cluster", "@ruin_wall", "fallen_log_b", "roots_big", "@tent"),
            Foliage = L("dead_bush_a", "dead_bush_b", "dead_bush_c", "dry_bush_a", "dry_bush_c"),
        });

        // ───────────────────────── 霜脊隘口: snow and ice, blizzard, pale blue ─────────────────────────
        A(new LevelTheme
        {
            Id = "frost", Scatter = 0.08f, Wet = 0.04f, MistTop = 3f, MistDensity = 0.03f, FogFar = 0.85f, SunElevation = 30f, Outdoor = true, Border = "mixed", CliffShare = 0.7f,
            Ground = "snow_fresh", GroundAlt = "snow_patchy", Path = "snow_trampled", RoomFloor = "flagstone", ArenaFloor = "ice",
            WallTex = "dark_cliff", WallTopTex = "snow_fresh", CliffTex = "dark_cliff", PitKind = "void", Sky = "snow_overcast", Weather = "snow", Footstep = "footstep_dirt",
            WallHeight = 4.5f, FogDensity = 0.017f, AmbientEnergy = 0.284f, SunEnergy = 0.199f, TorchRange = 11.5f,
            Ambient = C(0.42f, 0.5f, 0.66f), FogColor = C(0.5f, 0.6f, 0.75f), SunColor = C(0.82f, 0.88f, 1f), Accent = C(0.5f, 0.85f, 1f), TorchColor = Fire2000,
            SunDir = Dir(-0.56f, -0.6f, -0.57f),
            GroundTint = C(0.86f, 0.93f, 1.04f), Desaturate = 0.3f, GroundLumMin = 0.16f, GroundLumMax = 0.3f, WallLumMin = 0.05f, WallLumMax = 0.14f,
            TopColor = C(0.62f, 0.68f, 0.76f), TopAmount = 0.85f, PropTint = C(0.74f, 0.8f, 0.88f),
            PitColor = C(0.42f, 0.6f, 0.8f), PitGlow = 0.5f, SkyEnergy = 0.35f, FogHeight = 1f, FogHeightDensity = 0.22f, VolumetricDensity = 0.024f,
            Exposure = 0.98f, Saturation = 0.8f, WeatherColor = C(0.86f, 0.9f, 0.96f), WeatherAmount = 1f, Wind = new Vector2(4.2f, 1.6f),
            DressStone = "castle_blocks", DressStoneAlt = "rough_stone_wall", DressWood = "log_wall", DressPlank = "old_planks",
            Trees = L("dead_pine_a", "dead_pine_b", "dead_pine_c", "dead_tree_b", "dead_trunk"),
            Rocks = L("rock_a", "rock_b", "rock_e", "rock_f", "boulder_a", "rock_i"),
            BigRocks = L("cliff_a", "cliff_b", "boulder_b", "boulder_c", "cliff_d"),
            Clutter = L("branch_a", "branch_c", "rock_small_a", "rock_small_b", "barrel_broken_b", "shield", "sword", "@ice_shard"),
            Breakables = L("barrel", "barrel_old", "crate_a", "crate_b"),
            Furniture = L("camp_stool", "bench", "table_trestle", "fallen_log_a", "stump_a"),
            Lights = L("fire_barrel", "@torch", "fire_pit"),
            Pillars = L("@ice_crystal", "@ruin_pillar", "dead_trunk"),
            Structures = L("@ruin_wall", "@ice_cluster", "@palisade", "@tent", "@ruin_wall"),
            Foliage = L("dead_bush_a", "dead_bush_b", "dry_bush_b", "dry_bush_d"),
        });

        // ───────────────────────── 沉眠墓原: grey grass and burial stone, green-tinged fog ─────────────────────────
        A(new LevelTheme
        {
            Id = "barrows", Grade = 0.04f, Scatter = 0.14f, Wet = 0.3f, MistColor = C(0.55f, 0.8f, 0.6f), MistTop = 2.4f, MistDensity = 0.04f, FogFar = 0.75f, SunElevation = 36f, Outdoor = true, Border = "mixed", CliffShare = 0.15f,
            Ground = "dead_grass", GroundAlt = "forest_soil", Path = "flagstone", RoomFloor = "slate_floor", ArenaFloor = "cracked_tiles",
            WallTex = "mossy_stone", WallTopTex = "mossy_stone", CliffTex = "mossy_rock", PitKind = "void", Sky = "night", Weather = "dust", Footstep = "footstep_dirt",
            WallHeight = 4f, FogDensity = 0.014f, AmbientEnergy = 0.494f, SunEnergy = 0.57f, TorchRange = 11f,
            Ambient = C(0.3f, 0.34f, 0.33f), FogColor = C(0.13f, 0.21f, 0.17f), SunColor = C(0.74f, 0.8f, 0.82f), Accent = C(0.5f, 0.9f, 0.55f), TorchColor = Fire2200,
            SunDir = Dir(0.48f, -0.7f, -0.53f),
            GroundTint = C(1f, 1f, 0.98f), Desaturate = 0.75f, GroundLumMin = 0.05f, GroundLumMax = 0.13f,
            TopColor = C(0.2f, 0.25f, 0.13f), TopAmount = 0.35f, PropTint = C(0.8f, 0.8f, 0.78f),
            PitColor = C(0.3f, 0.75f, 0.42f), PitGlow = 0.3f, SkyEnergy = 0.5f, FogHeight = 1f, FogHeightDensity = 0.4f, VolumetricDensity = 0.024f,
            Saturation = 0.86f, WeatherColor = C(0.5f, 0.66f, 0.5f), WeatherAmount = 0.7f, Wind = new Vector2(0.3f, 0.2f),
            DressStone = "big_blocks", DressStoneAlt = "mossy_stone", DressWood = "rough_beam", DressPlank = "dark_planks",
            Trees = L("dead_tree_big", "dead_tree_a", "dead_trunk", "dead_pine_c", "dead_tree_b"),
            Rocks = L("rock_g", "rock_h", "rock_i", "rock_j", "rock_a"),
            BigRocks = L("boulder_a", "rock_cluster", "boulder_c", "rock_d"),
            Clutter = L("@headstone", "@headstone", "@rubble", "rock_small_a", "branch_b", "dead_stub_a", "shield", "spade"),
            Breakables = L("urn_a", "urn_b", "jug", "barrel_old", "vase_blue"),
            Furniture = L("@sarcophagus", "@grave_slab", "@headstone", "@grave_slab"),
            Lights = L("@brazier", "@torch", "candelabra_5"),
            Pillars = L("@obelisk", "@ruin_pillar", "statue_king"),
            Structures = L("@ruin_wall", "@grave_cluster", "@arch", "@grave_cluster", "statue_king", "iron_gate"),
            Foliage = L("fern_a", "fern_b", "fern_c", "dead_bush_b", "dead_bush_c"),
        });

        // ───────────────────────── 瘴泣沼澤: wet mud, moss, murky water, toxic spores ─────────────────────────
        A(new LevelTheme
        {
            Id = "mire", Scatter = 0.1f, Wet = 0.55f, MistColor = C(0.75f, 0.85f, 0.45f), MistTop = 3.2f, MistDensity = 0.055f, FogFar = 0.92f, SunElevation = 50f, Outdoor = true, Border = "forest",
            Ground = "swamp_floor", GroundAlt = "mossy_ground", Path = "mossy_planks", RoomFloor = "wet_mud", ArenaFloor = "mud_stones",
            WallTex = "mossy_rock", WallTopTex = "moss", CliffTex = "mossy_rock", PitKind = "water", Sky = "swamp_fog", Weather = "spores", Footstep = "footstep_dirt",
            WallHeight = 4f, FogDensity = 0.021f, AmbientEnergy = 0.628f, SunEnergy = 0.598f, TorchRange = 10.5f,
            Ambient = C(0.28f, 0.34f, 0.24f), FogColor = C(0.24f, 0.29f, 0.13f), SunColor = C(0.74f, 0.78f, 0.6f), Accent = C(0.75f, 0.9f, 0.3f), TorchColor = Fire2000,
            SunDir = Dir(-0.36f, -0.8f, 0.48f),
            GroundTint = C(0.92f, 1f, 0.84f), Desaturate = 0.42f, GroundLumMin = 0.045f, GroundLumMax = 0.12f,
            TopColor = C(0.16f, 0.22f, 0.07f), TopAmount = 0.5f, PropTint = C(0.74f, 0.82f, 0.68f),
            PitColor = C(0.022f, 0.034f, 0.012f), PitGlow = 0.35f, SkyEnergy = 0.3f, FogHeight = 1.4f, FogHeightDensity = 0.55f, VolumetricDensity = 0.034f,
            Saturation = 0.76f, WeatherColor = C(0.75f, 0.92f, 0.25f), WeatherAmount = 1f, Wind = new Vector2(0.25f, 0.15f),
            DressStone = "mossy_stone", DressStoneAlt = "old_brick", DressWood = "rough_beam", DressPlank = "mossy_planks", DressRoof = "thatch",
            Trees = L("dead_tree_a", "dead_tree_b", "dead_tree_big", "dead_trunk", "dead_quiver"),
            Rocks = L("rock_a", "rock_b", "rock_c", "rock_d", "rock_e", "rock_f"),
            BigRocks = L("boulder_a", "rock_b", "rock_e", "roots_big"),
            Clutter = L("roots_flat_a", "roots_flat_b", "root_piece_a", "root_piece_b", "branch_a", "branch_b", "stump_a", "stump_b"),
            Breakables = L("barrel_old", "crate_b", "clay_pot", "jug"),
            Furniture = L("fallen_log_a", "trough_a", "tub", "bucket", "stump_b", "pier_deck"),
            Lights = L("lantern_wood", "@torch", "@glowshroom", "@glowshroom"),
            Pillars = L("dead_trunk", "pier_poles", "dead_quiver"),
            Structures = L("pier_section", "roots_big", "fallen_log_b", "@ruin_wall", "pier_frame"),
            Foliage = L("reed_a", "reed_b", "fern_a", "fern_b", "fern_d", "swamp_plant_a", "swamp_plant_b", "swamp_plant_c", "broadleaf_a", "broadleaf_b"),
        });

        // ───────────────────────── 鏽砂廢都: dunes and sandstone ruins, brass, dusty violet dusk ─────────────────────────
        A(new LevelTheme
        {
            Id = "sands", Scatter = 0.14f, Wet = 0f, MistColor = C(0.85f, 0.68f, 0.55f), MistTop = 3f, MistDensity = 0.03f, FogFar = 0.78f, SunElevation = 24f, Outdoor = true, Border = "mixed", CliffShare = 0.5f,
            Ground = "sand_dunes", GroundAlt = "cracked_earth", Path = "sandstone_blocks", RoomFloor = "mosaic_tiles", ArenaFloor = "ornate_tiles",
            WallTex = "sandstone_rough", WallTopTex = "sandstone_blocks", CliffTex = "sandstone_worn", PitKind = "void", Sky = "desert_dusk", Weather = "sand", Footstep = "footstep_dirt",
            WallHeight = 4.5f, FogDensity = 0.015f, AmbientEnergy = 0.412f, SunEnergy = 0.412f, TorchRange = 11.5f,
            Ambient = C(0.38f, 0.33f, 0.5f), FogColor = C(0.38f, 0.28f, 0.34f), SunColor = C(1f, 0.8f, 0.62f), Accent = C(0.8f, 0.6f, 1f), TorchColor = Fire2200,
            SunDir = Dir(-0.68f, -0.46f, -0.57f),
            GroundTint = C(1f, 0.93f, 0.9f), Desaturate = 0.5f, GroundLumMin = 0.09f, GroundLumMax = 0.2f, WallLumMin = 0.07f, WallLumMax = 0.17f,
            TopColor = C(0.36f, 0.29f, 0.21f), TopAmount = 0.4f, PropTint = C(0.86f, 0.8f, 0.78f),
            PitColor = C(0.56f, 0.4f, 0.72f), PitGlow = 0.35f, SkyEnergy = 0.4f, FogHeight = 0.8f, FogHeightDensity = 0.3f, VolumetricDensity = 0.022f,
            Saturation = 0.82f, WeatherColor = C(0.62f, 0.5f, 0.4f), WeatherAmount = 1f, Wind = new Vector2(7.5f, 2.2f),
            DressStone = "sandstone_blocks", DressStoneAlt = "sandstone_rough", DressWood = "rough_beam", DressPlank = "old_planks", DressMetal = "tarnished_bronze", DressRoof = "terracotta_tiles",
            Trees = L("quiver_tree_a", "quiver_tree_b", "dead_quiver", "dead_trunk"),
            Rocks = L("boulder_d", "boulder_e", "rock_small_a", "rock_small_b", "rock_cluster"),
            BigRocks = L("cliff_c", "boulder_b", "boulder_c", "cliff_a"),
            Clutter = L("brass_pan", "brass_vial", "basket_b", "dead_stub_a", "dead_stub_b", "@rubble", "bowl_a", "brass_mug"),
            Breakables = L("urn_a", "urn_b", "brass_urn", "brass_ewer", "clay_pot", "lidded_pot", "jug"),
            Furniture = L("brass_basin", "cauldron", "table_low", "camp_stool", "drawer_chest"),
            Lights = L("@brazier_brass", "@torch", "@brazier_brass"),
            Pillars = L("@sand_column", "@sand_column", "@obelisk"),
            Structures = L("@ruin_wall", "@arch", "@colonnade", "statue_king", "@ruin_wall", "@tent"),
            Foliage = L("dry_bush_a", "dry_bush_b", "dry_bush_c", "dry_bush_d", "dead_bush_a"),
        });

        // ───────────────────────── 寂滅聖城: marble streets, gothic stone, candles, cold white ─────────────────────────
        A(new LevelTheme
        {
            Id = "sanctum", Scatter = 0.06f, GroundGloss = 0.75f, Wet = 0.3f, MistDensity = 0.02f, FogFar = 0.6f, SunElevation = 46f, Outdoor = true, Border = "walls",
            Ground = "flagstone", GroundAlt = "cracked_tiles", Path = "marble_floor", RoomFloor = "marble_checker", ArenaFloor = "ornate_tiles",
            WallTex = "gothic_stone", WallTopTex = "roof_tiles", CliffTex = "gothic_stone", PitKind = "void", Sky = "night", Weather = "ash", Footstep = "footstep_stone",
            WallHeight = 6.5f, FogDensity = 0.011f, AmbientEnergy = 0.326f, SunEnergy = 0.408f, TorchRange = 11.5f,
            Ambient = C(0.38f, 0.42f, 0.52f), FogColor = C(0.28f, 0.32f, 0.4f), SunColor = C(0.85f, 0.9f, 1f), Accent = C(0.95f, 0.95f, 1f), TorchColor = Fire2200,
            SunDir = Dir(0.4f, -0.74f, -0.54f),
            GroundTint = C(0.94f, 0.97f, 1.04f), Desaturate = 0.6f, GroundLumMin = 0.07f, GroundLumMax = 0.15f, WallLumMin = 0.05f, WallLumMax = 0.13f,
            TopColor = C(0.4f, 0.4f, 0.42f), TopAmount = 0.08f, PropTint = C(0.8f, 0.82f, 0.88f),
            PitColor = C(0.8f, 0.86f, 1f), PitGlow = 0.6f, SkyEnergy = 0.5f, FogHeight = 0.5f, FogHeightDensity = 0.2f, VolumetricDensity = 0.018f,
            Saturation = 0.72f, WeatherColor = C(0.7f, 0.72f, 0.78f), WeatherAmount = 0.4f, Wind = new Vector2(0.3f, 0.5f),
            DressStone = "big_blocks", DressStoneAlt = "gothic_stone", DressWood = "dark_panel_wood", DressPlank = "dark_planks", DressMetal = "aged_gold", DressRoof = "roof_tiles",
            Trees = L("dead_tree_b", "dead_trunk"),
            Rocks = L("@rubble", "rock_small_a", "rock_small_b"),
            BigRocks = L("@rubble_big", "statue_king"),
            Clutter = L("books_row", "goblets", "journal", "@rubble", "broom", "barrel_lid"),
            Breakables = L("vase_blue", "urn_a", "urn_b", "brass_urn", "crate_a", "barrel"),
            Furniture = L("bench", "table_gothic", "console_gothic", "armchair_a", "cabinet_gothic", "bookshelf", "settee"),
            Lights = L("lamp_post", "candelabra_5", "@brazier", "lamp_post"),
            Pillars = L("@cath_column", "statue_king", "@obelisk"),
            Structures = L("@arch", "@colonnade", "iron_gate", "statue_king", "@banner", "@altar"),
            Foliage = L("dead_bush_c", "dry_bush_d"),
        });

        // ───────────────────────── 燼淵: basalt, lava, obsidian spires, falling embers ─────────────────────────
        A(new LevelTheme
        {
            Id = "abyss", Scatter = 0.16f, Wet = 0.1f, MistColor = C(1f, 0.6f, 0.38f), MistTop = 2.5f, MistDensity = 0.05f, FogFar = 0.8f, SunElevation = 55f, Outdoor = true, Border = "cliff", CliffShare = 1f,
            Ground = "basalt", GroundAlt = "scorched_cracked", Path = "obsidian_slabs", RoomFloor = "slate_floor", ArenaFloor = "obsidian_slabs",
            WallTex = "basalt_layered", WallTopTex = "basalt", CliffTex = "basalt_layered", PitKind = "lava", Sky = "night_dark", Weather = "embers", Footstep = "footstep_stone",
            WallHeight = 5f, FogDensity = 0.014f, AmbientEnergy = 1.688f, SunEnergy = 1.35f, TorchRange = 11f,
            Ambient = C(0.26f, 0.23f, 0.32f), FogColor = C(0.24f, 0.1f, 0.06f), SunColor = C(0.55f, 0.5f, 0.72f), Accent = C(1f, 0.5f, 0.15f), TorchColor = Fire1800,
            SunDir = Dir(-0.3f, -0.85f, -0.43f),
            GroundTint = C(1.02f, 0.98f, 0.98f), Desaturate = 0.5f, GroundLumMin = 0.06f, GroundLumMax = 0.13f, WallLumMin = 0.05f, WallLumMax = 0.11f,
            TopColor = C(0.12f, 0.115f, 0.115f), TopAmount = 0.3f, PropTint = C(0.7f, 0.66f, 0.68f),
            PitColor = C(1f, 0.36f, 0.08f), PitGlow = 1f, SkyEnergy = 0.3f, FogHeight = 0.5f, FogHeightDensity = 0.3f, VolumetricDensity = 0.022f,
            Saturation = 0.94f, GlowIntensity = 0.85f, WeatherColor = C(1f, 0.45f, 0.1f), WeatherAmount = 1f, Wind = new Vector2(0.4f, 0.2f),
            DressStone = "basalt_layered", DressStoneAlt = "slate_floor", DressWood = "rough_beam", DressPlank = "dark_planks", DressMetal = "iron_plate",
            Trees = L("spire_a", "spire_b", "dead_trunk"),
            Rocks = L("basalt_boulder_a", "basalt_boulder_b", "shard_a", "spire_c"),
            BigRocks = L("cliff_d", "spire_a", "spire_b", "basalt_boulder_a"),
            Clutter = L("shard_a", "@rubble", "pickaxe", "war_hammer", "mace", "rock_small_a"),
            Breakables = L("urn_a", "jug", "barrel_old", "crate_b"),
            Furniture = L("@sarcophagus", "@anvil", "bust_bull"),
            Lights = L("@brazier", "@torch", "@brazier"),
            Pillars = L("spire_a", "@obsidian_pillar", "@ruin_pillar"),
            Structures = L("@ruin_wall", "@arch", "@obsidian_cluster", "pier_frame"),
            Foliage = L("shard_a", "dead_bush_b"),
        });

        // ═════════════════════════ dungeons ═════════════════════════

        // crypt: stone halls, pillars, torches, sarcophagi
        A(new LevelTheme
        {
            Id = "crypt", Scatter = 0.12f, Wet = 0.35f, MistColor = C(0.6f, 0.8f, 0.65f), MistDensity = 0.04f,
            Ground = "crypt_cobble", GroundAlt = "slate_floor", Path = "flagstone", RoomFloor = "flagstone", ArenaFloor = "cracked_tiles",
            WallTex = "castle_blocks", WallTopTex = "big_blocks", PitKind = "void", Sky = "cave", Weather = "dust", Footstep = "footstep_stone",
            WallHeight = 4.6f, FogDensity = 0.019f, AmbientEnergy = 0.5f, SunEnergy = 0.1f, TorchRange = 11f,
            Ambient = C(0.2f, 0.28f, 0.25f), FogColor = C(0.1f, 0.17f, 0.13f), SunColor = C(0.5f, 0.66f, 0.62f), Accent = C(0.5f, 0.9f, 0.55f), TorchColor = Fire2000,
            GroundTint = C(0.96f, 1f, 0.96f), Desaturate = 0.5f, TopAmount = 0f, PropTint = C(0.78f, 0.82f, 0.78f),
            PitColor = C(0.3f, 0.8f, 0.45f), PitGlow = 0.5f, SkyEnergy = 0.2f, FogHeight = 0.5f, FogHeightDensity = 0.35f, VolumetricDensity = 0.026f,
            Saturation = 0.86f, WeatherColor = C(0.5f, 0.56f, 0.5f), WeatherAmount = 0.6f, Wind = new Vector2(0.12f, 0.08f),
            DressStone = "big_blocks", DressStoneAlt = "castle_blocks", DressWood = "rough_beam", DressPlank = "dark_planks",
            Trees = L("dead_trunk"),
            Rocks = L("@rubble", "rock_small_a", "rock_small_b"),
            BigRocks = L("@rubble_big", "rock_i"),
            Clutter = L("@rubble", "books_row", "shield", "sword", "mace", "goblets", "journal", "barrel_broken_a", "barrel_lid", "bowl_b"),
            Breakables = L("urn_a", "urn_b", "jug", "barrel_old", "vase_blue", "crate_a"),
            Furniture = L("@sarcophagus", "@sarcophagus", "table_small", "shelf_small", "bookshelf", "stool_b", "@altar"),
            Lights = L("@brazier", "candelabra_5", "@torch"),
            Pillars = L("@pillar_square", "@pillar_round", "statue_king"),
            Structures = L("@sarcophagus", "@arch", "@rubble_big", "statue_king", "iron_gate"),
            Foliage = L("fern_a", "fern_d"),
        });

        // cave: root-choked earth and grey rock
        A(new LevelTheme
        {
            Id = "cave", Scatter = 0.18f, Wet = 0.45f, MistDensity = 0.035f, Organic = true,
            Ground = "forest_soil", GroundAlt = "root_ground", Path = "packed_dirt", RoomFloor = "root_ground", ArenaFloor = "burnt_soil",
            WallTex = "grey_rock", WallTopTex = "grey_rock", PitKind = "void", Sky = "cave", Weather = "dust", Footstep = "footstep_dirt",
            WallHeight = 4.8f, FogDensity = 0.021f, AmbientEnergy = 0.72f, SunEnergy = 0.16f, TorchRange = 12f,
            Ambient = C(0.22f, 0.25f, 0.32f), FogColor = C(0.1f, 0.12f, 0.16f), SunColor = C(0.5f, 0.56f, 0.7f), Accent = C(0.55f, 0.7f, 1f), TorchColor = Fire2000,
            GroundTint = C(1f, 0.98f, 0.96f), Desaturate = 0.5f, GroundLumMin = 0.045f, GroundLumMax = 0.12f, TopAmount = 0f,
            PitColor = C(0.3f, 0.42f, 0.62f), PitGlow = 0.3f, SkyEnergy = 0.15f, FogHeight = 0.5f, FogHeightDensity = 0.3f, VolumetricDensity = 0.026f,
            Saturation = 0.84f, WeatherColor = C(0.5f, 0.5f, 0.52f), WeatherAmount = 0.5f, Wind = new Vector2(0.1f, 0.06f),
            DressStone = "grey_rock", DressStoneAlt = "drystone_wall", DressWood = "rough_beam", DressPlank = "old_planks",
            Trees = L("dead_trunk", "roots_big"),
            Rocks = L("rock_g", "rock_h", "rock_i", "rock_j", "rock_small_a"),
            BigRocks = L("boulder_a", "rock_d", "rock_i"),
            Clutter = L("root_piece_a", "root_piece_b", "branch_b", "roots_flat_a", "rock_small_b", "pickaxe"),
            Breakables = L("barrel_old", "crate_a", "clay_pot"),
            Furniture = L("fallen_log_a", "stump_a", "camp_stool"),
            Lights = L("@torch", "lantern_wood", "fire_pit"),
            Pillars = L("@stalagmite", "dead_trunk"),
            Structures = L("roots_big", "@stalagmite_cluster", "@rubble_big"),
            Foliage = L("fern_a", "fern_b", "fern_c", "fern_d"),
        });

        // graveyard: the gravedigger's walled burial ground under a cold moon
        A(new LevelTheme
        {
            Id = "graveyard", Scatter = 0.14f, Wet = 0.4f, MistTop = 1.8f, MistDensity = 0.065f, FogFar = 0.75f, SunElevation = 38f, Outdoor = true, Border = "lowwall",
            Ground = "dark_mud", GroundAlt = "dead_leaves", Path = "gravel_path", RoomFloor = "forest_soil", ArenaFloor = "burnt_soil",
            WallTex = "mossy_stone", WallTopTex = "mossy_stone", CliffTex = "grey_rock", PitKind = "void", Sky = "night", Weather = "ash", Footstep = "footstep_dirt",
            WallHeight = 2.6f, FogDensity = 0.016f, AmbientEnergy = 0.694f, SunEnergy = 0.868f, TorchRange = 11f,
            Ambient = C(0.25f, 0.3f, 0.42f), FogColor = C(0.18f, 0.23f, 0.32f), SunColor = C(0.7f, 0.77f, 0.94f), Accent = C(0.55f, 0.7f, 1f), TorchColor = Fire2000,
            SunDir = Dir(0.52f, -0.62f, -0.58f),
            GroundTint = C(0.96f, 0.98f, 1.02f), Desaturate = 0.6f, GroundLumMin = 0.04f, GroundLumMax = 0.11f,
            TopColor = C(0.2f, 0.22f, 0.14f), TopAmount = 0.3f, PropTint = C(0.72f, 0.74f, 0.8f),
            PitColor = C(0.4f, 0.55f, 0.85f), PitGlow = 0.35f, SkyEnergy = 0.5f, FogHeight = 1.1f, FogHeightDensity = 0.5f, VolumetricDensity = 0.028f,
            Saturation = 0.8f, WeatherColor = C(0.42f, 0.43f, 0.46f), WeatherAmount = 0.6f, Wind = new Vector2(0.5f, 0.3f),
            DressStone = "big_blocks", DressStoneAlt = "mossy_stone", DressWood = "rough_beam", DressPlank = "dark_planks",
            Trees = L("dead_tree_big", "dead_tree_a", "dead_tree_b", "dead_trunk", "dead_pine_c"),
            Rocks = L("rock_g", "rock_h", "rock_j", "@headstone"),
            BigRocks = L("boulder_a", "rock_cluster", "@grave_cluster"),
            Clutter = L("@headstone", "@headstone", "@grave_slab", "@rubble", "spade", "pickaxe", "branch_a", "dead_stub_b"),
            Breakables = L("urn_a", "urn_b", "barrel_old", "crate_a"),
            Furniture = L("@grave_slab", "@headstone", "@sarcophagus", "@grave_slab"),
            Lights = L("@torch", "lantern_wood", "@brazier"),
            Pillars = L("@obelisk", "statue_king", "@ruin_pillar"),
            Structures = L("@grave_cluster", "@grave_cluster", "@ruin_wall", "statue_king", "iron_gate", "@arch"),
            Foliage = L("dead_bush_a", "dead_bush_b", "dead_bush_c", "fern_c"),
        });

        // icehall: a garrison swallowed by frost
        A(new LevelTheme
        {
            Id = "icehall", Scatter = 0.1f, GroundGloss = 0.75f, Wet = 0.2f, MistColor = C(0.7f, 0.85f, 1f), MistDensity = 0.03f,
            Ground = "flagstone", GroundAlt = "snow_patchy", Path = "slate_floor", RoomFloor = "flagstone", ArenaFloor = "ice",
            WallTex = "castle_blocks", WallTopTex = "castle_blocks", PitKind = "void", Sky = "cave", Weather = "snow", Footstep = "footstep_stone",
            WallHeight = 4.8f, FogDensity = 0.017f, AmbientEnergy = 0.84f, SunEnergy = 0.18f, TorchRange = 11f,
            Ambient = C(0.28f, 0.38f, 0.55f), FogColor = C(0.18f, 0.28f, 0.42f), SunColor = C(0.6f, 0.76f, 1f), Accent = C(0.5f, 0.85f, 1f), TorchColor = Fire2000,
            GroundTint = C(0.88f, 0.96f, 1.08f), Desaturate = 0.5f, GroundLumMin = 0.07f, GroundLumMax = 0.2f,
            TopColor = C(0.58f, 0.65f, 0.75f), TopAmount = 0.4f, PropTint = C(0.74f, 0.8f, 0.9f),
            PitColor = C(0.4f, 0.62f, 0.86f), PitGlow = 0.3f, SkyEnergy = 0.2f, FogHeight = 0.5f, FogHeightDensity = 0.3f, VolumetricDensity = 0.024f,
            Saturation = 0.84f, WeatherColor = C(0.84f, 0.9f, 0.98f), WeatherAmount = 0.3f, Wind = new Vector2(0.5f, 0.3f),
            DressStone = "big_blocks", DressStoneAlt = "castle_blocks", DressWood = "log_wall", DressPlank = "old_planks",
            Rocks = L("rock_small_a", "@ice_shard", "@rubble"),
            BigRocks = L("@ice_cluster", "@rubble_big"),
            Clutter = L("shield", "sword", "barrel_broken_a", "@ice_shard", "@rubble", "bowl_a"),
            Breakables = L("barrel", "crate_a", "crate_b", "barrel_old"),
            Furniture = L("table_trestle", "bench", "chair", "hutch", "stool_b"),
            Lights = L("fire_barrel", "@brazier", "@torch"),
            Pillars = L("@pillar_square", "@ice_crystal"),
            Structures = L("@ice_cluster", "@ruin_wall", "@arch"),
            Foliage = L("@ice_shard"),
        });

        // icecave: blue glacier ice
        A(new LevelTheme
        {
            Id = "icecave", Scatter = 0.1f, GroundGloss = 0.6f, Wet = 0.15f, MistColor = C(0.65f, 0.85f, 1f), MistDensity = 0.04f, Organic = true,
            Ground = "snow_patchy", GroundAlt = "ice", Path = "snow_trampled", RoomFloor = "ice", ArenaFloor = "ice",
            WallTex = "ice_pale", WallTopTex = "snow_fresh", PitKind = "water", Sky = "cave", Weather = "snow", Footstep = "footstep_dirt",
            WallHeight = 5f, FogDensity = 0.019f, AmbientEnergy = 0.7f, SunEnergy = 0.168f, TorchRange = 11f,
            Ambient = C(0.26f, 0.42f, 0.6f), FogColor = C(0.14f, 0.28f, 0.44f), SunColor = C(0.6f, 0.8f, 1f), Accent = C(0.5f, 0.85f, 1f), TorchColor = Fire2000,
            GroundTint = C(0.84f, 0.95f, 1.1f), Desaturate = 0.3f, GroundLumMin = 0.1f, GroundLumMax = 0.24f, WallLumMin = 0.08f, WallLumMax = 0.18f,
            WallTint = C(0.8f, 0.95f, 1.15f), TopColor = C(0.6f, 0.68f, 0.78f), TopAmount = 0.7f, PropTint = C(0.72f, 0.82f, 0.94f),
            PitColor = C(0.01f, 0.03f, 0.05f), PitGlow = 0.15f, SkyEnergy = 0.25f, FogHeight = 0.5f, FogHeightDensity = 0.3f, VolumetricDensity = 0.026f,
            Saturation = 0.9f, WeatherColor = C(0.84f, 0.92f, 1f), WeatherAmount = 0.25f, Wind = new Vector2(0.3f, 0.2f),
            DressStone = "mountain_rock", DressStoneAlt = "dark_cliff", DressWood = "rough_beam", DressPlank = "old_planks",
            Rocks = L("rock_a", "rock_e", "@ice_shard"),
            BigRocks = L("boulder_a", "@ice_cluster"),
            Clutter = L("@ice_shard", "rock_small_a", "branch_c"),
            Breakables = L("barrel_old", "crate_a"),
            Furniture = L("fallen_log_a", "camp_stool"),
            Lights = L("@torch", "fire_pit", "lantern_wood"),
            Pillars = L("@ice_crystal", "@stalagmite"),
            Structures = L("@ice_cluster", "@stalagmite_cluster"),
            Foliage = L("@ice_shard"),
        });

        // wetcave: drowned grottoes under the mire
        A(new LevelTheme
        {
            Id = "wetcave", Scatter = 0.14f, Wet = 0.85f, MistColor = C(0.7f, 0.85f, 0.4f), MistTop = 3f, MistDensity = 0.06f, Organic = true,
            Ground = "wet_mud", GroundAlt = "mud_stones", Path = "mossy_planks", RoomFloor = "swamp_floor", ArenaFloor = "mud_stones",
            WallTex = "mossy_rock", WallTopTex = "moss", PitKind = "water", Sky = "cave", Weather = "spores", Footstep = "footstep_dirt",
            WallHeight = 4.6f, FogDensity = 0.023f, AmbientEnergy = 0.72f, SunEnergy = 0.14f, TorchRange = 10.5f,
            Ambient = C(0.24f, 0.32f, 0.2f), FogColor = C(0.13f, 0.19f, 0.08f), SunColor = C(0.6f, 0.72f, 0.46f), Accent = C(0.75f, 0.9f, 0.3f), TorchColor = Fire2000,
            GroundTint = C(0.94f, 1f, 0.86f), Desaturate = 0.42f, GroundLumMin = 0.04f, GroundLumMax = 0.11f,
            TopColor = C(0.15f, 0.21f, 0.07f), TopAmount = 0.55f, PropTint = C(0.74f, 0.82f, 0.68f),
            PitColor = C(0.02f, 0.034f, 0.01f), PitGlow = 0.6f, SkyEnergy = 0.15f, FogHeight = 0.8f, FogHeightDensity = 0.5f, VolumetricDensity = 0.034f,
            Saturation = 0.9f, WeatherColor = C(0.75f, 0.92f, 0.25f), WeatherAmount = 0.8f, Wind = new Vector2(0.1f, 0.08f),
            DressStone = "mossy_stone", DressStoneAlt = "old_brick", DressWood = "rough_beam", DressPlank = "mossy_planks",
            Trees = L("dead_trunk", "roots_big"),
            Rocks = L("rock_a", "rock_c", "rock_e", "rock_small_a"),
            BigRocks = L("boulder_a", "rock_b", "roots_big"),
            Clutter = L("roots_flat_a", "roots_flat_b", "root_piece_a", "root_piece_b", "branch_a", "stump_a"),
            Breakables = L("barrel_old", "crate_b", "clay_pot", "jug"),
            Furniture = L("fallen_log_a", "tub", "bucket", "pier_deck"),
            Lights = L("@glowshroom", "lantern_wood", "@torch", "@glowshroom"),
            Pillars = L("@stalagmite", "dead_trunk", "pier_poles"),
            Structures = L("roots_big", "pier_section", "@stalagmite_cluster"),
            Foliage = L("reed_a", "reed_b", "fern_a", "fern_b", "swamp_plant_a", "swamp_plant_b", "broadleaf_a"),
        });

        // brasshall: workshops and throne rooms of the brass dynasty
        A(new LevelTheme
        {
            Id = "brasshall", Scatter = 0.08f, GroundGloss = 0.8f, Wet = 0.04f, MistColor = C(0.9f, 0.7f, 0.5f), MistDensity = 0.025f,
            Ground = "sandstone_blocks", GroundAlt = "sand_fine", Path = "mosaic_tiles", RoomFloor = "terracotta_tiles", ArenaFloor = "ornate_tiles",
            WallTex = "sandstone_blocks", WallTopTex = "sandstone_worn", PitKind = "void", Sky = "cave", Weather = "dust", Footstep = "footstep_stone",
            WallHeight = 5f, FogDensity = 0.016f, AmbientEnergy = 0.44f, SunEnergy = 0.1f, TorchRange = 11.5f,
            Ambient = C(0.34f, 0.29f, 0.42f), FogColor = C(0.2f, 0.15f, 0.22f), SunColor = C(0.76f, 0.62f, 0.86f), Accent = C(0.8f, 0.6f, 1f), TorchColor = Fire2200,
            GroundTint = C(1f, 0.94f, 0.92f), Desaturate = 0.36f, GroundLumMin = 0.07f, GroundLumMax = 0.17f, WallLumMin = 0.06f, WallLumMax = 0.15f,
            TopColor = C(0.36f, 0.29f, 0.21f), TopAmount = 0.2f, PropTint = C(0.88f, 0.82f, 0.8f),
            PitColor = C(0.6f, 0.42f, 0.8f), PitGlow = 0.45f, SkyEnergy = 0.2f, FogHeight = 0.4f, FogHeightDensity = 0.25f, VolumetricDensity = 0.022f,
            Saturation = 0.92f, WeatherColor = C(0.7f, 0.58f, 0.4f), WeatherAmount = 0.6f, Wind = new Vector2(0.15f, 0.1f),
            DressStone = "sandstone_blocks", DressStoneAlt = "sandstone_rough", DressWood = "rough_beam", DressPlank = "old_planks", DressMetal = "tarnished_bronze",
            Rocks = L("@rubble", "rock_small_a", "boulder_e"),
            BigRocks = L("@rubble_big", "boulder_d"),
            Clutter = L("brass_pan", "brass_mug", "brass_vial", "goblets", "@rubble", "bowl_a", "vice", "smith_hammer"),
            Breakables = L("brass_urn", "brass_ewer", "urn_a", "urn_b", "lidded_pot", "clay_pot"),
            Furniture = L("brass_basin", "cauldron", "table_low", "drawer_chest", "throne", "@anvil"),
            Lights = L("@brazier_brass", "candelabra_5", "@brazier_brass"),
            Pillars = L("@brass_column", "@sand_column", "@brass_column"),
            Structures = L("@arch", "@colonnade", "statue_king", "bust_lion", "@banner"),
            Foliage = L("dry_bush_c", "dry_bush_d"),
        });

        // sandtomb: burial passages choked with sand
        A(new LevelTheme
        {
            Id = "sandtomb", Scatter = 0.14f, Wet = 0f, MistColor = C(0.85f, 0.7f, 0.5f), MistDensity = 0.04f,
            Ground = "sand_fine", GroundAlt = "cracked_earth", Path = "sandstone_blocks", RoomFloor = "mosaic_tiles", ArenaFloor = "terracotta_tiles",
            WallTex = "sandstone_rough", WallTopTex = "sandstone_blocks", PitKind = "void", Sky = "cave", Weather = "dust", Footstep = "footstep_dirt",
            WallHeight = 4.6f, FogDensity = 0.019f, AmbientEnergy = 0.56f, SunEnergy = 0.14f, TorchRange = 11f,
            Ambient = C(0.32f, 0.27f, 0.36f), FogColor = C(0.2f, 0.15f, 0.16f), SunColor = C(0.76f, 0.62f, 0.8f), Accent = C(0.8f, 0.6f, 1f), TorchColor = Fire2000,
            GroundTint = C(1f, 0.93f, 0.88f), Desaturate = 0.4f, GroundLumMin = 0.07f, GroundLumMax = 0.16f, WallLumMin = 0.055f, WallLumMax = 0.14f,
            TopColor = C(0.36f, 0.29f, 0.21f), TopAmount = 0.45f, PropTint = C(0.86f, 0.8f, 0.76f),
            PitColor = C(0.5f, 0.36f, 0.66f), PitGlow = 0.35f, SkyEnergy = 0.15f, FogHeight = 0.4f, FogHeightDensity = 0.3f, VolumetricDensity = 0.026f,
            Saturation = 0.88f, WeatherColor = C(0.66f, 0.54f, 0.4f), WeatherAmount = 0.8f, Wind = new Vector2(0.3f, 0.12f),
            DressStone = "sandstone_blocks", DressStoneAlt = "sandstone_worn", DressWood = "rough_beam", DressPlank = "old_planks", DressMetal = "corroded_bronze",
            Rocks = L("@rubble", "boulder_e", "rock_small_b"),
            BigRocks = L("@rubble_big", "boulder_d"),
            Clutter = L("@rubble", "basket_b", "brass_vial", "bowl_b", "dead_stub_a", "dagger"),
            Breakables = L("urn_a", "urn_b", "jug", "clay_pot", "lidded_pot", "brass_urn"),
            Furniture = L("@sarcophagus", "@sarcophagus", "table_low", "@altar"),
            Lights = L("@torch", "@brazier_brass", "@torch"),
            Pillars = L("@sand_column", "@obelisk", "@pillar_square"),
            Structures = L("@sarcophagus", "@arch", "@rubble_big", "statue_king"),
            Foliage = L("dry_bush_d"),
        });

        // cathedral: marble nave, candles, velvet
        A(new LevelTheme
        {
            Id = "cathedral", Scatter = 0.05f, GroundGloss = 0.6f, Wet = 0.1f, MistDensity = 0.02f,
            Ground = "marble_floor", GroundAlt = "cracked_tiles", Path = "red_carpet", RoomFloor = "marble_checker", ArenaFloor = "ornate_tiles",
            WallTex = "gothic_stone", WallTopTex = "gothic_stone", PitKind = "void", Sky = "cave", Weather = "dust", Footstep = "footstep_stone",
            WallHeight = 6.4f, FogDensity = 0.013f, AmbientEnergy = 0.5f, SunEnergy = 0.14f, TorchRange = 12f,
            Ambient = C(0.34f, 0.37f, 0.47f), FogColor = C(0.2f, 0.23f, 0.3f), SunColor = C(0.78f, 0.84f, 1f), Accent = C(0.95f, 0.95f, 1f), TorchColor = Fire2200,
            GroundTint = C(0.95f, 0.97f, 1.03f), Desaturate = 0.4f, GroundLumMin = 0.07f, GroundLumMax = 0.13f, WallLumMin = 0.05f, WallLumMax = 0.12f,
            TopAmount = 0f, PropTint = C(0.82f, 0.83f, 0.88f),
            PitColor = C(0.82f, 0.88f, 1f), PitGlow = 0.7f, SkyEnergy = 0.25f, FogHeight = 0.4f, FogHeightDensity = 0.2f, VolumetricDensity = 0.02f,
            Saturation = 0.82f, WeatherColor = C(0.62f, 0.62f, 0.66f), WeatherAmount = 0.5f, Wind = new Vector2(0.08f, 0.06f),
            DressStone = "big_blocks", DressStoneAlt = "gothic_stone", DressWood = "dark_panel_wood", DressPlank = "varnished_wood", DressMetal = "aged_gold",
            Rocks = L("@rubble", "rock_small_a"),
            BigRocks = L("@rubble_big", "statue_king"),
            Clutter = L("books_row", "journal", "goblets", "@rubble", "broom"),
            Breakables = L("vase_blue", "urn_a", "brass_urn", "brass_ewer", "crate_a"),
            Furniture = L("bench", "bench", "table_gothic", "console_gothic", "throne", "bookshelf", "library_cabinet", "armchair_b"),
            Lights = L("candelabra_5", "@brazier", "candelabra_5", "lamp_post"),
            Pillars = L("@cath_column", "@cath_column", "statue_king"),
            Structures = L("@arch", "@altar", "statue_king", "@banner", "@colonnade"),
            Foliage = L("dead_bush_c"),
        });

        // forge: the dead Sun-Forge, iron and soot
        A(new LevelTheme
        {
            Id = "forge", Scatter = 0.12f, Wet = 0.05f, MistColor = C(1f, 0.66f, 0.42f), MistDensity = 0.04f,
            Ground = "slate_floor", GroundAlt = "scorched_cracked", Path = "iron_plate", RoomFloor = "obsidian_slabs", ArenaFloor = "obsidian_slabs",
            WallTex = "gothic_stone", WallTopTex = "basalt", PitKind = "lava", Sky = "cave", Weather = "embers", Footstep = "footstep_stone",
            WallHeight = 5.2f, FogDensity = 0.015f, AmbientEnergy = 0.7f, SunEnergy = 0.14f, TorchRange = 11f,
            Ambient = C(0.26f, 0.22f, 0.3f), FogColor = C(0.2f, 0.1f, 0.06f), SunColor = C(0.62f, 0.52f, 0.7f), Accent = C(1f, 0.5f, 0.15f), TorchColor = Fire1800,
            GroundTint = C(1.02f, 0.97f, 0.96f), Desaturate = 0.5f, GroundLumMin = 0.04f, GroundLumMax = 0.1f, WallLumMin = 0.035f, WallLumMax = 0.09f,
            TopColor = C(0.1f, 0.1f, 0.1f), TopAmount = 0.2f, PropTint = C(0.72f, 0.68f, 0.68f),
            PitColor = C(1f, 0.36f, 0.08f), PitGlow = 1f, SkyEnergy = 0.15f, FogHeight = 0.4f, FogHeightDensity = 0.3f, VolumetricDensity = 0.024f,
            Saturation = 0.94f, GlowIntensity = 0.85f, WeatherColor = C(1f, 0.45f, 0.1f), WeatherAmount = 0.8f, Wind = new Vector2(0.2f, 0.1f),
            DressStone = "big_blocks", DressStoneAlt = "basalt_layered", DressWood = "rough_beam", DressPlank = "dark_planks", DressMetal = "iron_plate",
            Rocks = L("basalt_boulder_b", "shard_a", "@rubble"),
            BigRocks = L("basalt_boulder_a", "@rubble_big"),
            Clutter = L("smith_hammer", "pickaxe", "war_hammer", "mace", "vice", "@rubble", "barrel_stave"),
            Breakables = L("barrel_old", "crate_b", "urn_a", "jug"),
            Furniture = L("@anvil", "@anvil", "table_small", "stool_b", "bust_bull"),
            Lights = L("@brazier", "@brazier", "@torch"),
            Pillars = L("@pillar_square", "@obsidian_pillar"),
            Structures = L("@arch", "@ruin_wall", "@obsidian_cluster", "pier_frame"),
            Foliage = L("shard_a"),
        });

        // lavacave: the obsidian rift
        A(new LevelTheme
        {
            Id = "lavacave", Scatter = 0.16f, Wet = 0.05f, MistColor = C(1f, 0.6f, 0.38f), MistDensity = 0.05f, Organic = true,
            Ground = "basalt", GroundAlt = "scorched_cracked", Path = "slate_floor", RoomFloor = "basalt_layered", ArenaFloor = "obsidian_slabs",
            WallTex = "obsidian", WallTopTex = "basalt", PitKind = "lava", Sky = "cave", Weather = "embers", Footstep = "footstep_stone",
            WallHeight = 5.2f, FogDensity = 0.017f, AmbientEnergy = 0.68f, SunEnergy = 0.12f, TorchRange = 10.5f,
            Ambient = C(0.25f, 0.2f, 0.3f), FogColor = C(0.2f, 0.09f, 0.05f), SunColor = C(0.6f, 0.5f, 0.7f), Accent = C(1f, 0.5f, 0.15f), TorchColor = Fire1800,
            GroundTint = C(1.02f, 0.97f, 0.96f), Desaturate = 0.5f, GroundLumMin = 0.035f, GroundLumMax = 0.09f, WallLumMin = 0.03f, WallLumMax = 0.08f,
            TopColor = C(0.1f, 0.1f, 0.1f), TopAmount = 0.2f, PropTint = C(0.7f, 0.66f, 0.68f),
            PitColor = C(1f, 0.34f, 0.07f), PitGlow = 1f, SkyEnergy = 0.12f, FogHeight = 0.4f, FogHeightDensity = 0.35f, VolumetricDensity = 0.026f,
            Saturation = 0.94f, GlowIntensity = 0.85f, WeatherColor = C(1f, 0.42f, 0.08f), WeatherAmount = 1f, Wind = new Vector2(0.2f, 0.1f),
            DressStone = "basalt_layered", DressStoneAlt = "slate_floor", DressWood = "rough_beam", DressPlank = "dark_planks", DressMetal = "iron_plate",
            Trees = L("spire_a", "spire_b"),
            Rocks = L("basalt_boulder_a", "basalt_boulder_b", "shard_a", "spire_c"),
            BigRocks = L("spire_a", "spire_b", "basalt_boulder_a"),
            Clutter = L("shard_a", "@rubble", "pickaxe", "rock_small_a"),
            Breakables = L("barrel_old", "crate_b", "urn_a"),
            Furniture = L("@anvil", "bust_bull"),
            Lights = L("@torch", "@brazier", "@torch"),
            Pillars = L("@obsidian_pillar", "spire_a", "@stalagmite"),
            Structures = L("@obsidian_cluster", "@stalagmite_cluster", "@rubble_big"),
            Foliage = L("shard_a"),
        });

        // rift: the endless abyssal rift, a place that is not quite a place
        A(new LevelTheme
        {
            Id = "rift", Scatter = 0.1f, GroundGloss = 0.7f, Wet = 0.2f, MistColor = C(0.7f, 0.6f, 1f), MistDensity = 0.05f, Organic = true,
            Ground = "basalt", GroundAlt = "obsidian_slabs", Path = "dark_marble", RoomFloor = "dark_marble", ArenaFloor = "obsidian_slabs",
            WallTex = "obsidian", WallTopTex = "basalt", PitKind = "void", Sky = "cave", Weather = "embers", Footstep = "footstep_stone",
            WallHeight = 5f, FogDensity = 0.019f, AmbientEnergy = 0.62f, SunEnergy = 0.1f, TorchRange = 11f,
            Ambient = C(0.28f, 0.22f, 0.46f), FogColor = C(0.13f, 0.09f, 0.27f), SunColor = C(0.7f, 0.6f, 1f), Accent = C(0.82f, 0.74f, 1f), TorchColor = Fire2000,
            GroundTint = C(0.96f, 0.94f, 1.08f), Desaturate = 0.45f, GroundLumMin = 0.04f, GroundLumMax = 0.1f, WallLumMin = 0.03f, WallLumMax = 0.085f,
            WallTint = C(0.9f, 0.86f, 1.2f), TopAmount = 0f, PropTint = C(0.72f, 0.7f, 0.86f),
            PitColor = C(0.8f, 0.76f, 1f), PitGlow = 0.9f, SkyEnergy = 0.15f, FogHeight = 0.5f, FogHeightDensity = 0.4f, VolumetricDensity = 0.03f,
            Saturation = 0.92f, GlowIntensity = 0.85f, WeatherColor = C(0.8f, 0.72f, 1f), WeatherAmount = 0.7f, Wind = new Vector2(0.12f, 0.1f),
            DressStone = "dark_marble", DressStoneAlt = "basalt_layered", DressWood = "rough_beam", DressPlank = "dark_planks", DressMetal = "iron_plate",
            Trees = L("spire_a", "spire_b"),
            Rocks = L("shard_a", "spire_c", "basalt_boulder_b", "@rubble"),
            BigRocks = L("spire_a", "basalt_boulder_a", "@rubble_big"),
            Clutter = L("shard_a", "@rubble", "rock_small_b"),
            Breakables = L("urn_a", "urn_b", "barrel_old", "crate_a"),
            Furniture = L("@sarcophagus", "@altar"),
            Lights = L("@brazier", "@torch", "@brazier"),
            Pillars = L("@obsidian_pillar", "@obelisk", "@pillar_square"),
            Structures = L("@obsidian_cluster", "@arch", "@ruin_wall"),
            Foliage = L("shard_a"),
        });

        return d;
    }
}
