using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AshenAbyss;

/// <summary>
/// Icon atlas-on-demand. The game-icons.net SVGs under res://assets/icons/gi are deliberately not imported by Godot
/// (the folder carries a .gdignore), so they are rasterised at runtime: file bytes → Image.LoadSvgFromBuffer → a
/// normalised white glyph on transparent (so every caller tints with Modulate) → cached ImageTexture with mipmaps.
/// A bare name such as "fireball" is resolved through res://assets/icons/index.txt (first */name.svg wins); anything
/// that cannot be found becomes a drawn placeholder (rounded square with the first character) and is logged once.
/// </summary>
public static class Icons
{
    /// <summary>Edge length in pixels of every rasterised glyph.</summary>
    public const int Px = 96;
    private const string ResRoot = "res://assets/icons/";

    private static Dictionary<string, string> _index;
    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static readonly Dictionary<char, Texture2D> Placeholders = new();
    private static readonly HashSet<string> Warned = new();
    private static string _diskRoot;

    // ───────────────────────── mapping tables ─────────────────────────

    /// <summary>Item icon keys (Item.Icon) → game-icons names, ordered from humble to grand; picked by item power.</summary>
    private static readonly Dictionary<string, string[]> ItemIcons = new()
    {
        ["helm"] = new[] { "barbute", "visored-helm", "crested-helmet" },
        ["chest"] = new[] { "leather-armor", "scale-mail", "breastplate" },
        ["gloves"] = new[] { "gloves", "gauntlet" },
        ["pants"] = new[] { "trousers", "leg-armor", "armored-pants" },
        ["boots"] = new[] { "leather-boot", "boots", "metal-boot" },
        ["amulet"] = new[] { "primitive-necklace", "gem-pendant", "gem-necklace" },
        ["ring"] = new[] { "ring", "diamond-ring", "skull-ring" },
        ["greatsword"] = new[] { "broadsword", "two-handed-sword", "rune-sword" },
        ["staff"] = new[] { "bo", "wizard-staff", "crystal-wand" },
        ["crossbow"] = new[] { "crossbow" },
        ["scythe"] = new[] { "scythe", "reaper-scythe" },
        ["club"] = new[] { "wood-club", "spiked-mace", "flanged-mace" },
        ["gauntlet"] = new[] { "brass-knuckles", "mailed-fist", "punch-blast" },
        ["swordshield"] = new[] { "battle-gear" },
        ["grimoire"] = new[] { "spell-book", "secret-book", "evil-book" },
        ["gem_ruby"] = new[] { "cut-diamond" },
        ["gem_sapphire"] = new[] { "saphir" },
        ["gem_topaz"] = new[] { "topaz" },
        ["gem_emerald"] = new[] { "emerald" },
        ["gem_amethyst"] = new[] { "amethyst" },
        ["gem_skull"] = new[] { "skull-crack" },
        ["weapon"] = new[] { "crossed-swords" },
        ["gem"] = new[] { "gems" },
    };

    /// <summary>
    /// Friendly names → game-icons names. Consulted only when the bare name is not in the index itself, so a real
    /// icon name always wins. Every one of the 64 skill icons named in SkillsData.cs exists in the index as written,
    /// so this table only carries the semantic shorthands the interface uses.
    /// </summary>
    private static readonly Dictionary<string, string> Alias = new()
    {
        ["potion"] = "heart-bottle", ["evade"] = "dodging", ["gold"] = "two-coins", ["scrap"] = "ore", ["dust"] = "powder",
        ["core"] = "fire-gem", ["stash"] = "chest", ["waypoint"] = "fire-bowl", ["quest"] = "scroll-unfurled", ["map"] = "treasure-map",
        ["paragon"] = "star-formation", ["inventory"] = "knapsack", ["settings"] = "cog", ["lock"] = "padlock", ["boss"] = "crowned-skull",
        ["elite"] = "horned-skull", ["smith"] = "anvil", ["merchant"] = "shop", ["occultist"] = "crystal-ball", ["jeweler"] = "gem-chain",
        ["elder"] = "lantern-flame", ["villager"] = "person", ["rift"] = "magic-portal", ["riftstone"] = "magic-portal", ["altar"] = "fire-shrine",
        ["shrine"] = "rune-stone", ["entrance"] = "dungeon-gate", ["exit"] = "exit-door", ["bossdoor"] = "wooden-door", ["stronghold"] = "stone-tower",
        ["event"] = "polar-star", ["objective"] = "position-marker", ["tier"] = "laurel-crown", ["tierstatue"] = "laurel-crown",
        ["brazier"] = "campfire", ["life"] = "hearts", ["npc"] = "conversation", ["idol"] = "totem-head", ["talk"] = "conversation",
    };

    private static string CategoryIcon(SkillCat c) => c switch
    {
        SkillCat.Basic => "crossed-swords", SkillCat.Core => "spiky-explosion", SkillCat.Defensive => "shield",
        SkillCat.Mobility => "sprint", SkillCat.Mastery => "star-formation", _ => "laurel-crown"
    };

    // ───────────────────────── public API ─────────────────────────

    // ───────────────────────── raster icons (res://assets/icons/r/<category>/<id>.png) ─────────────────────────

    private const string RasterRoot = "res://assets/icons/r/";
    private static readonly Dictionary<string, Texture2D> RasterCache = new();
    private static readonly HashSet<Texture2D> RasterSet = new();

    /// <summary>
    /// Old glyph names (and the interface's shorthands) → raster icon ids. Lets every existing call site
    /// (Icons.Get("fire-bowl"), the stat / key-passive / specialisation tables of the panels) show raster art
    /// without being touched; the typed helpers below (ForPassive, ForSpec, ForMarker …) are more exact.
    /// </summary>
    private static readonly Dictionary<string, string> Legacy = new()
    {
        // HUD, currency, menu objects
        ["heart-bottle"] = "misc/potion", ["potion"] = "misc/potion", ["dodging"] = "hud/evade", ["evade"] = "hud/evade",
        ["two-coins"] = "misc/gold", ["gold"] = "misc/gold", ["ore"] = "misc/scrap", ["scrap"] = "misc/scrap",
        ["powder"] = "misc/dust", ["dust"] = "misc/dust", ["fire-gem"] = "misc/core", ["core"] = "misc/core",
        ["knapsack"] = "misc/inventory", ["inventory"] = "misc/inventory", ["cog"] = "misc/settings", ["settings"] = "misc/settings",
        ["padlock"] = "misc/lock", ["lock"] = "misc/lock", ["treasure-map"] = "misc/map", ["map"] = "misc/map",
        ["paragon"] = "misc/paragon", ["key"] = "misc/key",
        // afflictions and flag buffs (HudBuffs)
        ["knockout"] = "status/stun", ["ice-cube"] = "status/freeze", ["root-tip"] = "status/root", ["terror"] = "status/fear",
        ["flame"] = "status/burn", ["small-fire"] = "stat/burning", ["poison-bottle"] = "status/poison", ["bleeding-wound"] = "status/bleed",
        ["skull-crossed-bones"] = "status/rot", ["snowflake-1"] = "status/chill", ["snail"] = "status/slow",
        ["broken-shield"] = "status/vulnerable", ["broken-bone"] = "status/weaken", ["invisible"] = "status/stealth",
        ["eye-shield"] = "buff/immune", ["crowned-heart"] = "buff/deathless", ["strong"] = "buff/unstoppable",
        ["magic-swirl"] = "buff/freecast", ["bleeding-heart"] = "buff/lifesteal",
        // stats (generic buffs, passive nodes, skill category fallbacks)
        ["crossed-swords"] = "stat/dmg", ["sword-brandish"] = "stat/core", ["spiky-explosion"] = "stat/core", ["punch"] = "stat/basic",
        ["burning-embers"] = "stat/dot", ["mighty-force"] = "stat/overpower", ["minions"] = "stat/minion",
        ["snowflake-2"] = "stat/frost", ["lightning-frequency"] = "stat/lightning", ["lightning-helix"] = "stat/lightning",
        ["cursed-star"] = "stat/shadow", ["evil-moon"] = "stat/shadow", ["quick-slash"] = "stat/attackspeed",
        ["wingfoot"] = "stat/movespeed", ["sprint"] = "stat/movespeed", ["shield"] = "stat/dr", ["armor-vest"] = "stat/armor",
        ["bolt-shield"] = "stat/barrier", ["rosa-shield"] = "stat/barrier", ["magic-shield"] = "stat/resall", ["fire-shield"] = "stat/resfire",
        ["spiked-shield"] = "stat/thorns", ["on-target"] = "stat/critchance", ["bullseye"] = "stat/critdmg",
        ["hearts"] = "stat/life", ["heart-plus"] = "stat/life", ["life"] = "stat/life", ["heart-drop"] = "stat/lifeonkill",
        ["healing"] = "stat/healing", ["hourglass"] = "stat/cdr", ["embrassed-energy"] = "stat/resource", ["energy-breath"] = "stat/resource",
        ["round-bottom-flask"] = "stat/maxresource", ["upgrade"] = "stat/generic", ["star-formation"] = "stat/generic", ["crown"] = "stat/generic",
        // key passives
        ["winged-sword"] = "keypassive/kp_eb_edge", ["vibrating-shield"] = "keypassive/kp_eb_stand", ["thunder-struck"] = "keypassive/kp_as_overload",
        ["frostfire"] = "keypassive/kp_as_mastery", ["backstab"] = "keypassive/kp_hu_finisher", ["dodge"] = "keypassive/kp_hu_skirmish",
        ["spectre"] = "keypassive/kp_sb_alone", ["wolverine-claws"] = "keypassive/kp_ws_feral", ["oak-leaf"] = "keypassive/kp_ws_elder",
        ["yin-yang"] = "keypassive/kp_fb_flow", ["meditation"] = "keypassive/kp_fb_vajra", ["shield-echoes"] = "keypassive/kp_lk_retort",
        ["scales"] = "keypassive/kp_lk_weight", ["star-swirl"] = "keypassive/kp_pb_full",
        // specialisations
        ["two-handed-sword"] = "spec/eb_heavy", ["checked-shield"] = "spec/eb_guard", ["daggers"] = "spec/hu_combo", ["hood"] = "spec/hu_ambush",
        ["skeleton"] = "spec/sb_legion", ["death-skull"] = "spec/sb_offering", ["bear-head"] = "spec/ws_bear", ["wolf-head"] = "spec/ws_wolf",
        ["raven"] = "spec/ws_raven", ["fire-silhouette"] = "spec/fb_fire", ["tornado"] = "spec/fb_wind", ["templar-shield"] = "spec/lk_ward",
        ["gavel"] = "spec/lk_judge", ["ogre"] = "spec/pb_brute", ["imp"] = "spec/pb_imps", ["ghost"] = "spec/pb_wraith",
        // map and minimap markers, town services
        ["fire-bowl"] = "marker/waypoint", ["waypoint"] = "marker/waypoint", ["exit-door"] = "marker/exit", ["exit"] = "marker/exit",
        ["dungeon-gate"] = "marker/entrance", ["entrance"] = "marker/entrance", ["crowned-skull"] = "marker/boss", ["boss"] = "marker/boss",
        ["wooden-door"] = "marker/bossdoor", ["bossdoor"] = "marker/bossdoor", ["stone-tower"] = "marker/stronghold", ["stronghold"] = "marker/stronghold",
        ["fire-shrine"] = "marker/altar", ["altar"] = "marker/altar", ["locked-chest"] = "marker/chest", ["rune-stone"] = "marker/shrine", ["shrine"] = "marker/shrine",
        ["polar-star"] = "marker/event", ["event"] = "marker/event", ["position-marker"] = "marker/objective", ["objective"] = "marker/objective",
        ["scroll-unfurled"] = "marker/quest", ["quest"] = "marker/quest", ["conversation"] = "marker/npc", ["npc"] = "marker/npc", ["talk"] = "marker/npc",
        ["lantern-flame"] = "marker/elder", ["elder"] = "marker/elder", ["anvil"] = "marker/smith", ["smith"] = "marker/smith",
        ["shop"] = "marker/merchant", ["merchant"] = "marker/merchant", ["crystal-ball"] = "marker/occultist", ["occultist"] = "marker/occultist",
        ["gem-chain"] = "marker/jeweler", ["jeweler"] = "marker/jeweler", ["person"] = "marker/villager", ["villager"] = "marker/villager",
        ["chest"] = "marker/stash", ["stash"] = "marker/stash", ["campfire"] = "marker/brazier", ["brazier"] = "marker/brazier",
        ["laurel-crown"] = "marker/tierstatue", ["tier"] = "marker/tierstatue", ["tierstatue"] = "marker/tierstatue",
        ["magic-portal"] = "marker/portal", ["portal"] = "marker/portal", ["rift"] = "marker/riftstone", ["riftstone"] = "marker/riftstone",
        ["prisoner"] = "marker/prisoner", ["totem-head"] = "marker/idol", ["idol"] = "marker/idol", ["horned-skull"] = "marker/elite", ["elite"] = "marker/elite",
        ["beacon"] = "marker/beacon", ["start"] = "marker/start", ["hero"] = "marker/hero",
    };

    /// <summary>
    /// The raster icon "category/id" (a PNG under res://assets/icons/r), or null when there is no such file.
    /// Imported textures are used when Godot has imported them; otherwise the PNG is read directly.
    /// </summary>
    public static Texture2D Raster(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (RasterCache.TryGetValue(id, out var t)) return t;
        t = null;
        string path = RasterRoot + id + ".png";
        try
        {
            if (ResourceLoader.Exists(path)) t = ResourceLoader.Load<Texture2D>(path);
            if (t == null)
            {
                // the icons folder carries a .gdignore (not imported): read the PNG itself, from res:// in a dev run
                // or from <exe folder>\assets\icons in an installed build, exactly like the vector set
                byte[] b = ReadBytes("r/" + id + ".png");
                if (b != null && b.Length > 0)
                {
                    var img = new Image();
                    if (img.LoadPngFromBuffer(b) == Error.Ok && !img.IsEmpty())
                    {
                        if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);
                        img.GenerateMipmaps();
                        t = ImageTexture.CreateFromImage(img);
                    }
                }
            }
        }
        catch (Exception e) { Log.Error("icons", $"raster icon '{id}' failed to load", e); t = null; }
        RasterCache[id] = t;
        if (t != null) RasterSet.Add(t);
        return t;
    }

    /// <summary>True for full-colour raster art (do not colourise it); false for a white vector glyph meant to be tinted.</summary>
    public static bool IsRaster(Texture2D t) => t != null && RasterSet.Contains(t);

    /// <summary>
    /// The Modulate to use for an icon: a vector glyph takes <paramref name="wanted"/> as it is; raster art keeps its
    /// own colours and only follows the brightness and alpha of the wanted colour (so dimmed / locked states still dim).
    /// </summary>
    public static Color Tint(Texture2D t, Color wanted)
    {
        if (!IsRaster(t)) return wanted;
        float v = MathF.Max(wanted.R, MathF.Max(wanted.G, wanted.B));
        float k = v >= 0.6f ? 1f : v / 0.6f;
        return new Color(k, k, k, wanted.A);
    }

    private static Texture2D RasterOrWarn(string id, string what)
    {
        var t = Raster(id);
        if (t == null && id != null && Warned.Add("r:" + id)) Log.Warn("icons", $"no raster icon '{id}' for {what}: vector fallback");
        return t;
    }

    private static string ItemRasterId(Item it)
    {
        if (it.Kind == ItemKind.Gem)
        {
            string g = it.GemId ?? "";
            int n = g.Length;
            return n >= 2 && char.IsDigit(g[n - 1]) ? "item/gem_" + g.Substring(0, n - 1) + "_" + g[n - 1] : null;
        }
        if (!string.IsNullOrEmpty(it.Unique)) return "item/" + it.Unique;
        int tier = Math.Clamp(it.Power / 125, 0, 4) + 1;          // same five base tiers as ItemDb's base names
        if (it.Kind == ItemKind.Weapon)
        {
            string type = it.WeaponClass >= 0 && it.WeaponClass < ItemDb.WeaponIcons.Length ? ItemDb.WeaponIcons[it.WeaponClass] : it.Icon;
            return string.IsNullOrEmpty(type) ? null : $"item/wpn_{type}_{tier}";
        }
        string key = KindKey(it.Kind, -1);
        return key == "gem" || key == "weapon" ? null : $"item/{key}_{tier}";
    }

    /// <summary>Minor or key passive of the skill tree (falls back to the icon of its first stat).</summary>
    public static Texture2D ForPassive(PassiveDef p)
    {
        if (p == null) return Placeholder("?");
        return Raster((p.Key ? "keypassive/" : "passive/") + p.Id) ?? Get("star-formation");
    }

    /// <summary>Class specialisation by id (eb_heavy …).</summary>
    public static Texture2D ForSpec(string specId) => Raster("spec/" + specId) ?? Get("star-formation");

    /// <summary>Affliction shown on the hero.</summary>
    public static Texture2D ForStatus(StatusId id) => Raster("status/" + (id == StatusId.Vulnerable ? "vulnerable" : id.ToString().ToLowerInvariant())) ?? Get("upgrade");

    /// <summary>Map / minimap marker kind (waypoint, boss, smith …); null when the kind has no token.</summary>
    public static Texture2D ForMarker(string kind) => string.IsNullOrEmpty(kind) ? null : Raster("marker/" + kind);

    /// <summary>Elite affix (molten, frozen …); null when unknown.</summary>
    public static Texture2D ForAffix(string affixId) => string.IsNullOrEmpty(affixId) ? null : Raster("affix/" + affixId);

    /// <summary>Class crest (512 px medallion); null when missing.</summary>
    public static Texture2D ForClass(ClassId c) => Raster("crest/" + Names.ClassKey(c));

    /// <summary>The game's emblem (512 px); null when missing.</summary>
    public static Texture2D TitleEmblem => Raster("crest/title");

    /// <summary>Paragon glyph stone (g_steel …); null when missing.</summary>
    public static Texture2D ForGlyph(string glyphId) => string.IsNullOrEmpty(glyphId) ? null : Raster("paragon/" + glyphId);

    /// <summary>Paragon node token by type; null when missing.</summary>
    public static Texture2D ForParagonNode(PNodeType t) => Raster("paragon/node_" + t.ToString().ToLowerInvariant());

    /// <summary>Icon of a buff that is not tied to a skill: shrine blessings by id, otherwise null (caller keeps its own choice).</summary>
    public static Texture2D ForBuff(BuffDef d)
    {
        string id = d?.Id;
        if (string.IsNullOrEmpty(id) || !id.StartsWith("shrine_", StringComparison.Ordinal)) return null;
        string kind = id.Substring(7);
        int cut = kind.IndexOf('_');
        if (cut > 0) kind = kind.Substring(0, cut);
        return Raster("shrine/" + kind);
    }

    // ───────────────────────── names ─────────────────────────

    /// <summary>The icon with this name: a raster id ("skill/eb_slash"), an old glyph name or shorthand, or "author/name". Never null.</summary>
    public static Texture2D Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Placeholder("?");
        if (Cache.TryGetValue(name, out var t)) return t;
        t = null;
        try
        {
            if (name.IndexOf('/') > 0 && !name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) t = Raster(name);
            if (t == null && Legacy.TryGetValue(name, out var rid)) t = Raster(rid);
        }
        catch (Exception e) { Log.Error("icons", $"raster lookup of '{name}' failed", e); t = null; }
        if (t != null) { Cache[name] = t; return t; }
        if (Warned.Add("v:" + name)) Log.Warn("icons", $"icon '{name}' has no raster art: vector fallback");
        try { t = Load(name); }
        catch (Exception e) { Log.Error("icons", $"loading icon '{name}' failed", e); t = null; }
        if (t == null)
        {
            if (Warned.Add(name)) Log.Warn("icons", $"icon '{name}' not found, using a placeholder");
            t = Placeholder(name);
        }
        Cache[name] = t;
        return t;
    }

    /// <summary>True when the name resolves to a real SVG (directly, through an alias or as a path).</summary>
    public static bool Has(string name) => !string.IsNullOrWhiteSpace(name) && Resolve(name) != null;

    public static Texture2D ForItem(Item it)
    {
        if (it == null) return Placeholder("?");
        try
        {
            string rid = ItemRasterId(it);
            var r = rid == null ? null : RasterOrWarn(rid, "item " + it.Name);
            if (r != null) return r;
        }
        catch (Exception e) { Log.Error("icons", "raster item icon failed", e); }
        string key = it.Icon;
        if (string.IsNullOrEmpty(key) || (!ItemIcons.ContainsKey(key) && !Has(key)))
            key = it.Kind == ItemKind.Gem ? "gem" : KindKey(it.Kind, it.WeaponClass);
        if (ItemIcons.TryGetValue(key, out var names))
        {
            int tier;
            if (names.Length == 1) tier = 0;
            else if (it.Unique != null) tier = names.Length - 1;
            else tier = Math.Clamp(it.Power * names.Length / (ItemDb.MaxPower + 1), 0, names.Length - 1);
            return Get(names[tier]);
        }
        return Get(key);       // an item carrying a literal game-icons name
    }

    public static Texture2D ForSkill(SkillDef s)
    {
        if (s == null) return Placeholder("?");
        var r = RasterOrWarn("skill/" + s.Id, "skill " + s.Name);
        if (r != null) return r;
        if (!string.IsNullOrEmpty(s.Icon) && Has(s.Icon)) return Get(s.Icon);
        if (!string.IsNullOrEmpty(s.Icon) && Warned.Add("skill:" + s.Id))
            Log.Warn("icons", $"skill {s.Id} names icon '{s.Icon}' which is not in the index; using the {s.Cat} fallback");
        return Get(CategoryIcon(s.Cat));
    }

    public static Texture2D ForKind(ItemKind k, int weaponClass = -1)
    {
        string key = KindKey(k, weaponClass);
        // the ghost of an empty equipment slot: the plain second tier of that kind
        var r = Raster(k == ItemKind.Weapon ? (key == "weapon" ? "item/wpn_greatsword_2" : $"item/wpn_{key}_2") : k == ItemKind.Gem ? "item/gem_ruby_2" : $"item/{key}_2");
        if (r != null) return r;
        if (!ItemIcons.TryGetValue(key, out var names)) return Get(key);
        return Get(names[Math.Min(1, names.Length - 1)]);
    }

    private static string KindKey(ItemKind k, int weaponClass) => k switch
    {
        ItemKind.Weapon => weaponClass >= 0 && weaponClass < ItemDb.WeaponIcons.Length ? ItemDb.WeaponIcons[weaponClass] : "weapon",
        ItemKind.Helm => "helm", ItemKind.Chest => "chest", ItemKind.Gloves => "gloves", ItemKind.Pants => "pants",
        ItemKind.Boots => "boots", ItemKind.Amulet => "amulet", ItemKind.Ring => "ring", _ => "gem"
    };

    /// <summary>
    /// Rasterises SVG source text as it is (colours kept) into a texture that occupies width × height interface
    /// pixels but carries <paramref name="oversample"/> times the detail, so small theme glyphs stay sharp at 4K.
    /// Returns null if the SVG cannot be rendered.
    /// </summary>
    public static ImageTexture FromSvg(string svg, int width, int height, float oversample = 2f)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(svg);
            SvgSize(bytes, out float vw, out _);
            var img = new Image();
            if (img.LoadSvgFromBuffer(bytes, width * oversample / vw) != Error.Ok || img.IsEmpty()) return null;
            var tex = ImageTexture.CreateFromImage(img);
            tex.SetSizeOverride(new Vector2I(width, height));
            return tex;
        }
        catch (Exception e)
        {
            Log.Error("icons", "inline svg failed", e);
            return null;
        }
    }

    // ───────────────────────── index and files ─────────────────────────

    private static void EnsureIndex()
    {
        if (_index != null) return;
        _index = new Dictionary<string, string>(4600, StringComparer.Ordinal);
        string text = ReadText("index.txt");
        if (string.IsNullOrEmpty(text))
        {
            Log.Warn("icons", "assets/icons/index.txt not found: every icon will be a placeholder");
            return;
        }
        int names = 0;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim().TrimStart('﻿').Replace('\\', '/');
            if (line.Length < 5 || !line.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) continue;
            int slash = line.LastIndexOf('/');
            string name = line.Substring(slash + 1, line.Length - slash - 5);
            if (_index.TryAdd(name, line)) names++;                      // first match of */name.svg wins
            if (slash > 0)
            {
                int prev = line.LastIndexOf('/', slash - 1);
                _index.TryAdd(line.Substring(prev + 1, line.Length - prev - 5), line);   // "author/name"
            }
        }
        Log.Info("icons", $"icon index loaded: {names} names");
    }

    /// <summary>Path relative to the icons folder ("gi/lorc/fireball.svg"), or null.</summary>
    private static string Resolve(string name)
    {
        EnsureIndex();
        if (name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            string rel = name.Replace('\\', '/');
            if (rel.StartsWith(ResRoot, StringComparison.OrdinalIgnoreCase)) rel = rel.Substring(ResRoot.Length);
            return rel;
        }
        if (_index.TryGetValue(name, out var path)) return path;
        if (Alias.TryGetValue(name, out var real) && _index.TryGetValue(real, out path)) return path;
        return null;
    }

    private static string DiskRoot
    {
        get
        {
            if (_diskRoot != null) return _diskRoot;
            try
            {
                string exe = OS.GetExecutablePath();
                string dir = string.IsNullOrEmpty(exe) ? "." : System.IO.Path.GetDirectoryName(exe) ?? ".";
                _diskRoot = System.IO.Path.Combine(dir, "assets", "icons");
            }
            catch { _diskRoot = System.IO.Path.Combine(".", "assets", "icons"); }
            return _diskRoot;
        }
    }

    private static byte[] ReadBytes(string rel)
    {
        try
        {
            string res = ResRoot + rel;
            if (Godot.FileAccess.FileExists(res))
            {
                byte[] b = Godot.FileAccess.GetFileAsBytes(res);
                if (b != null && b.Length > 0) return b;
            }
        }
        catch (Exception e) { Log.Debug("icons", $"res read of {rel} failed: {e.Message}"); }
        try
        {
            string disk = System.IO.Path.Combine(DiskRoot, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(disk)) return System.IO.File.ReadAllBytes(disk);
        }
        catch (Exception e) { Log.Debug("icons", $"disk read of {rel} failed: {e.Message}"); }
        return null;
    }

    private static string ReadText(string rel)
    {
        byte[] b = ReadBytes(rel);
        return b == null ? null : Encoding.UTF8.GetString(b);
    }

    // ───────────────────────── rasterising ─────────────────────────

    private static Texture2D Load(string name)
    {
        string rel = Resolve(name);
        if (rel == null) return null;
        byte[] svg = ReadBytes(rel);
        if (svg == null || svg.Length == 0) return null;
        Image img = Rasterize(svg, Px);
        if (img == null) return null;
        return Normalize(img);
    }

    /// <summary>Intrinsic size of an SVG from its root tag (width/height attributes, else the viewBox, else 512).</summary>
    private static void SvgSize(byte[] svg, out float w, out float h)
    {
        w = h = 512f;
        string head = Encoding.ASCII.GetString(svg, 0, Math.Min(svg.Length, 800));
        int a = head.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        if (a < 0) return;
        int b = head.IndexOf('>', a);
        string tag = b > a ? head.Substring(a, b - a) : head.Substring(a);
        float aw = Attr(tag, " width=\""), ah = Attr(tag, " height=\"");
        if (aw > 0f && ah > 0f) { w = aw; h = ah; return; }
        int v = tag.IndexOf("viewBox=\"", StringComparison.OrdinalIgnoreCase);
        if (v < 0) return;
        int s = v + 9, e = tag.IndexOf('"', s);
        if (e <= s) return;
        string[] parts = tag.Substring(s, e - s).Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float vw)
            && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float vh) && vw > 0f && vh > 0f)
        { w = vw; h = vh; }
    }

    private static float Attr(string tag, string key)
    {
        int i = tag.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return 0f;
        int s = i + key.Length, e = s;
        while (e < tag.Length && (char.IsDigit(tag[e]) || tag[e] == '.')) e++;
        if (e == s) return 0f;
        if (e < tag.Length && tag[e] == '%') return 0f;
        return float.TryParse(tag.AsSpan(s, e - s), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }

    /// <summary>SVG bytes → square RGBA8 image of px × px (letterboxed if the artwork is not square).</summary>
    private static Image Rasterize(byte[] svg, int px)
    {
        SvgSize(svg, out float w, out float h);
        var img = new Image();
        Error err = img.LoadSvgFromBuffer(svg, px / MathF.Max(w, h));
        if (err != Error.Ok || img.IsEmpty()) return null;
        if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);
        int iw = img.GetWidth(), ih = img.GetHeight();
        if (iw == px && ih == px) return img;
        if (iw == ih) { img.Resize(px, px, Image.Interpolation.Lanczos); return img; }
        float k = (float)px / Math.Max(iw, ih);
        int nw = Math.Clamp((int)MathF.Round(iw * k), 1, px), nh = Math.Clamp((int)MathF.Round(ih * k), 1, px);
        img.Resize(nw, nh, Image.Interpolation.Lanczos);
        var canvas = Image.CreateFromData(px, px, false, Image.Format.Rgba8, new byte[px * px * 4]);
        canvas.BlitRect(img, new Rect2I(0, 0, nw, nh), new Vector2I((px - nw) / 2, (px - nh) / 2));
        return canvas;
    }

    /// <summary>
    /// Turns any of the source styles (white glyph on transparent, white glyph on a black plate, dark glyph on
    /// transparent) into a pure white glyph whose alpha is the shape. Light pixels are the glyph whenever the image
    /// has a meaningful amount of them; otherwise whatever is opaque is the glyph.
    /// </summary>
    private static Texture2D Normalize(Image img)
    {
        int w = img.GetWidth(), h = img.GetHeight(), n = w * h;
        byte[] src = img.GetData();
        if (src == null || src.Length < n * 4) return ImageTexture.CreateFromImage(img);
        int bright = 0, dark = 0;
        for (int i = 0; i < n; i++)
        {
            int o = i * 4;
            if (src[o + 3] < 128) continue;
            int lum = (src[o] * 77 + src[o + 1] * 150 + src[o + 2] * 29) >> 8;
            if (lum >= 128) bright++; else dark++;
        }
        bool lightGlyph = bright > 0 && bright * 50 >= bright + dark;
        var dst = new byte[n * 4];
        for (int i = 0; i < n; i++)
        {
            int o = i * 4;
            int a = src[o + 3];
            int v = a;
            if (lightGlyph)
            {
                int lum = (src[o] * 77 + src[o + 1] * 150 + src[o + 2] * 29) >> 8;
                v = lum * a / 255;
            }
            dst[o] = 255; dst[o + 1] = 255; dst[o + 2] = 255; dst[o + 3] = (byte)v;
        }
        var outImg = Image.CreateFromData(w, h, false, Image.Format.Rgba8, dst);
        outImg.GenerateMipmaps();
        return ImageTexture.CreateFromImage(outImg);
    }

    // ───────────────────────── placeholder ─────────────────────────

    // 5×7 dot-matrix capitals and digits for the placeholder tile (rows top to bottom, '1' = lit)
    private static readonly Dictionary<char, string> Dots = new()
    {
        ['A'] = "01110 10001 10001 11111 10001 10001 10001", ['B'] = "11110 10001 10001 11110 10001 10001 11110",
        ['C'] = "01110 10001 10000 10000 10000 10001 01110", ['D'] = "11110 10001 10001 10001 10001 10001 11110",
        ['E'] = "11111 10000 10000 11110 10000 10000 11111", ['F'] = "11111 10000 10000 11110 10000 10000 10000",
        ['G'] = "01110 10001 10000 10111 10001 10001 01111", ['H'] = "10001 10001 10001 11111 10001 10001 10001",
        ['I'] = "01110 00100 00100 00100 00100 00100 01110", ['J'] = "00111 00010 00010 00010 00010 10010 01100",
        ['K'] = "10001 10010 10100 11000 10100 10010 10001", ['L'] = "10000 10000 10000 10000 10000 10000 11111",
        ['M'] = "10001 11011 10101 10101 10001 10001 10001", ['N'] = "10001 11001 10101 10011 10001 10001 10001",
        ['O'] = "01110 10001 10001 10001 10001 10001 01110", ['P'] = "11110 10001 10001 11110 10000 10000 10000",
        ['Q'] = "01110 10001 10001 10001 10101 10010 01101", ['R'] = "11110 10001 10001 11110 10100 10010 10001",
        ['S'] = "01111 10000 10000 01110 00001 00001 11110", ['T'] = "11111 00100 00100 00100 00100 00100 00100",
        ['U'] = "10001 10001 10001 10001 10001 10001 01110", ['V'] = "10001 10001 10001 10001 10001 01010 00100",
        ['W'] = "10001 10001 10001 10101 10101 11011 10001", ['X'] = "10001 10001 01010 00100 01010 10001 10001",
        ['Y'] = "10001 10001 01010 00100 00100 00100 00100", ['Z'] = "11111 00001 00010 00100 01000 10000 11111",
        ['0'] = "01110 10001 10011 10101 11001 10001 01110", ['1'] = "00100 01100 00100 00100 00100 00100 01110",
        ['2'] = "01110 10001 00001 00010 00100 01000 11111", ['3'] = "11110 00001 00001 01110 00001 00001 11110",
        ['4'] = "00010 00110 01010 10010 11111 00010 00010", ['5'] = "11111 10000 11110 00001 00001 10001 01110",
        ['6'] = "00110 01000 10000 11110 10001 10001 01110", ['7'] = "11111 00001 00010 00100 01000 01000 01000",
        ['8'] = "01110 10001 10001 01110 10001 10001 01110", ['9'] = "01110 10001 10001 01111 00001 00010 01100",
        ['?'] = "01110 10001 00001 00010 00100 00000 00100",
    };

    /// <summary>A drawn stand-in: rounded square outline with the first letter or digit of the name.</summary>
    private static Texture2D Placeholder(string name)
    {
        char c = '?';
        if (name != null)
            foreach (char ch in name)
            {
                char up = char.ToUpperInvariant(ch);
                if (Dots.ContainsKey(up)) { c = up; break; }
            }
        if (Placeholders.TryGetValue(c, out var cached)) return cached;

        Texture2D tex = null;
        try
        {
            var sb = new StringBuilder(1200);
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 128 128\">");
            sb.Append("<rect x=\"9\" y=\"9\" width=\"110\" height=\"110\" rx=\"20\" fill=\"none\" stroke=\"#fff\" stroke-width=\"7\"/>");
            string[] rows = Dots[c].Split(' ');
            const int cell = 10;
            int ox = 64 - 5 * cell / 2, oy = 64 - 7 * cell / 2;
            for (int r = 0; r < rows.Length; r++)
                for (int col = 0; col < rows[r].Length; col++)
                    if (rows[r][col] == '1')
                        sb.Append("<rect x=\"").Append(ox + col * cell).Append("\" y=\"").Append(oy + r * cell)
                          .Append("\" width=\"").Append(cell).Append("\" height=\"").Append(cell).Append("\" fill=\"#fff\"/>");
            sb.Append("</svg>");
            Image img = Rasterize(Encoding.UTF8.GetBytes(sb.ToString()), Px);
            if (img != null) tex = Normalize(img);
        }
        catch (Exception e) { Log.Error("icons", "placeholder svg failed", e); }
        tex ??= PlainTile();
        Placeholders[c] = tex;
        return tex;
    }

    /// <summary>Last resort when even SVG rasterising is unavailable: a bordered square built by hand.</summary>
    private static Texture2D PlainTile()
    {
        var data = new byte[Px * Px * 4];
        for (int y = 0; y < Px; y++)
            for (int x = 0; x < Px; x++)
            {
                int edge = Math.Min(Math.Min(x, y), Math.Min(Px - 1 - x, Px - 1 - y));
                byte a = edge is >= 8 and < 14 ? (byte)255 : (byte)0;
                int o = (y * Px + x) * 4;
                data[o] = 255; data[o + 1] = 255; data[o + 2] = 255; data[o + 3] = a;
            }
        return ImageTexture.CreateFromImage(Image.CreateFromData(Px, Px, false, Image.Format.Rgba8, data));
    }
}
