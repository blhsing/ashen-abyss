using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenAbyss;

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
//  Modular gear on heroes (CONTRACT §4–§7, RENDER).
//
//  A hero is assembled from: the barely-clad base body hero_<class>_base.glb (split into body_<region> meshes), one
//  skinned armour piece per equipped armour slot (bound to the SAME Skeleton3D; the body regions it covers are hidden),
//  and the weapon / off-hand models as rigid meshes on bone attachments (hand_r, hand_l, lowerarm_l for shields).
//
//  Fallback chain, so the game works while the art is still being made:
//    armour look missing  → the nearest look of the same class and slot → no piece (the body shows);
//    weapon model missing → the nearest variant → today's baked weapon (baked hero, class base) → a primitive stand-in;
//    base body missing    → today's baked hero (armour built in): its built-in weapons are hidden and the equipped
//                           weapon models are shown instead.
//  Test switches: --gear-off (old behaviour everywhere), --gear-baked (ignore base bodies), --gear-ph (synthesise
//  placeholder base bodies, armour and jewels from today's heroes when the art is missing), --gear-dir=<folder>
//  (extra folder of .glb files loaded at run time, searched before res://assets/gear).
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The single place that reads SYSTEMS' item fields (Item.Base, Item.Look, EquipSlot names).</summary>
public static class GearAdapter
{
    public static Item MainHand(Hero h) => h?.D?.Equip == null ? null : h.D.Equip[(int)EquipSlot.MainHand];
    public static Item OffHand(Hero h) => h?.D?.Equip == null ? null : h.D.Equip[(int)EquipSlot.OffHand];
    public static Item Slot(Hero h, EquipSlot s) => h?.D?.Equip == null || (int)s >= h.D.Equip.Length ? null : h.D.Equip[(int)s];
    public static string BaseOf(Item it) => it?.Base;
    /// <summary>The model id of an item as worn by (or shown to) a hero of class <paramref name="cls"/>.</summary>
    public static string LookOf(Item it, ClassId cls) => it == null ? null : ItemDb.LookFor(it, cls);
    public static bool IsTwoHand(string baseId) => (ItemDb.BaseOf(baseId)?.Hands ?? 1) == 2;
    public static bool IsOffHandBase(string baseId) => ItemDb.BaseOf(baseId)?.Off ?? false;
    public static IEnumerable<Item> Starter(ClassId c) { yield return ItemDb.Starter(c); var o = ItemDb.StarterOff(c); if (o != null) yield return o; }
    /// <summary>The class's 0.1–0.3 weapon (main, off): the bases today's baked hero models carry as wpn_N / wpn_N_off.</summary>
    public static (string main, string off) Legacy(ClassId c) => (ItemDb.Loadout(c).Legacy, ItemDb.Loadout(c).LegacyOff);
}

public sealed class ArmorPick
{
    public string Slot;          // helm chest gloves pants boots
    public int Tier;             // 1..4
    public Rarity Rarity;
    public string Model;         // explicit model id when the item names one
}

public sealed class WeaponPick
{
    public string Base;
    public int Variant;          // 1..3
    public Rarity Rarity;
    public string Model;
}

/// <summary>What a hero wears and holds, in render terms. Built from a hero, a list of items or by hand (tests).</summary>
public sealed class GearSet
{
    public static readonly string[] ArmorSlots = { "helm", "chest", "gloves", "pants", "boots" };
    private static readonly EquipSlot[] ArmorEquip = { EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Gloves, EquipSlot.Pants, EquipSlot.Boots };

    public ClassId Class;
    public readonly ArmorPick[] Armor = new ArmorPick[5];
    public WeaponPick Main, Off;

    public string Signature
    {
        get
        {
            var sb = new System.Text.StringBuilder(96);
            sb.Append((int)Class);
            foreach (var a in Armor) sb.Append('|').Append(a == null ? "-" : $"{a.Model ?? a.Slot + a.Tier}:{(int)a.Rarity}");
            sb.Append('|').Append(Main == null ? "-" : $"{Main.Model ?? Main.Base + Main.Variant}:{(int)Main.Rarity}");
            sb.Append('|').Append(Off == null ? "-" : $"{Off.Model ?? Off.Base + Off.Variant}:{(int)Off.Rarity}");
            return sb.ToString();
        }
    }

    public override string ToString() => Signature;

    public static GearSet FromHero(Hero h)
    {
        if (h == null) return null;
        var g = new GearSet { Class = h.Class };
        for (int i = 0; i < 5; i++) g.Armor[i] = ArmorOf(GearAdapter.Slot(h, ArmorEquip[i]), h.Class, ArmorSlots[i]);
        g.SetHands(GearAdapter.MainHand(h), GearAdapter.OffHand(h));
        return g;
    }

    public static GearSet FromItems(ClassId c, IEnumerable<Item> items)
    {
        var g = new GearSet { Class = c };
        Item main = null, off = null;
        foreach (var it in items ?? Enumerable.Empty<Item>())
        {
            if (it == null) continue;
            int k = Array.IndexOf(ArmorSlots, SlotKey(it.Kind));
            if (k >= 0) { g.Armor[k] = ArmorOf(it, c, ArmorSlots[k]); continue; }
            if (it.Kind == ItemKind.OffHand) off = it;
            else if (it.Kind == ItemKind.Weapon) { if (main == null) main = it; else off ??= it; }
        }
        g.SetHands(main, off);
        return g;
    }

    /// <summary>A new hero's loadout (no armour: barely clad, the starting weapon).</summary>
    public static GearSet Starter(ClassId c)
    {
        try { return FromItems(c, GearAdapter.Starter(c)); }
        catch (Exception e) { Log.Error("gear", $"starter gear of {c} could not be built", e); return new GearSet { Class = c }; }
    }

    /// <summary>Test loadout: armour tier for every slot (0 = none), weapon bases (null = empty) and variant.</summary>
    public static GearSet Make(ClassId c, int armorTier, string main, string off, int variant = 2, Rarity rarity = Rarity.Rare, int slots = 0x1F)
    {
        var g = new GearSet { Class = c };
        for (int i = 0; i < 5; i++)
            if (armorTier > 0 && (slots & (1 << i)) != 0) g.Armor[i] = new ArmorPick { Slot = ArmorSlots[i], Tier = armorTier, Rarity = rarity };
        if (main != null) g.Main = new WeaponPick { Base = main, Variant = variant, Rarity = rarity };
        if (off != null) g.Off = new WeaponPick { Base = off, Variant = variant, Rarity = rarity };
        return g;
    }

    private void SetHands(Item main, Item off)
    {
        // a one-hand weapon in the off hand alone is shown in the main hand
        if (main == null && off != null && off.Kind == ItemKind.Weapon) { main = off; off = null; }
        Main = WeaponOf(main, Class);
        Off = WeaponOf(off, Class);
        if (Main != null && GearAdapter.IsTwoHand(Main.Base)) Off = null;
    }

    private static string SlotKey(ItemKind k) => k switch
    {
        ItemKind.Helm => "helm", ItemKind.Chest => "chest", ItemKind.Gloves => "gloves", ItemKind.Pants => "pants", ItemKind.Boots => "boots", _ => null
    };

    private static int Num(string look, char tag)
    {
        if (string.IsNullOrEmpty(look)) return 0;
        int i = look.LastIndexOf('_');
        if (i < 0 || i + 2 > look.Length || look[i + 1] != tag) return 0;
        return int.TryParse(look.AsSpan(i + 2), out int n) ? n : 0;
    }

    private static ArmorPick ArmorOf(Item it, ClassId c, string slot)
    {
        if (it == null) return null;
        string look = GearAdapter.LookOf(it, c);
        int t = Num(look, 't');
        if (t <= 0) t = it.Rarity >= Rarity.Legendary ? 4 : it.Rarity == Rarity.Rare ? 3 : it.Rarity == Rarity.Magic ? 2 : 1;
        return new ArmorPick { Slot = slot, Tier = Math.Clamp(t, 1, 4), Rarity = it.Rarity, Model = look != null && look.StartsWith("arm_") ? look : null };
    }

    private static WeaponPick WeaponOf(Item it, ClassId c)
    {
        if (it == null) return null;
        string b = GearAdapter.BaseOf(it);
        string look = GearAdapter.LookOf(it, c);
        if (b == null && look != null && look.StartsWith("wpn_")) { int u = look.LastIndexOf('_'); if (u > 4) b = look[4..u]; }
        if (b == null) b = it.Kind == ItemKind.OffHand ? GearAdapter.Legacy(c).off : GearAdapter.Legacy(c).main;
        if (b == null) return null;
        int v = Num(look, 'v');
        if (v <= 0) v = it.Rarity >= Rarity.Legendary ? 3 : it.Rarity == Rarity.Rare || it.Power >= 250 ? 2 : 1;
        return new WeaponPick { Base = b, Variant = Math.Clamp(v, 1, 3), Rarity = it.Rarity, Model = look != null && look.StartsWith("wpn_") ? look : null };
    }
}

// ───────────────────────────────────────────── stances ─────────────────────────────────────────────

public enum Stance { Unarmed, TwoHandMelee, TwoHandStaff, Ranged, OneHandShield, OneHandCaster, DualWield, Fists, OneHand }

/// <summary>
/// Loadout → stance → clips from the shared humanoid library (anim_lib.glb). The library has no two-hand, bow,
/// dual-wield or off-hand-lantern clips; the closest existing clip stands in (see the gap notes in PROGRESS_RENDER.md).
/// </summary>
public static class Stances
{
    private static readonly HashSet<string> Casters = new() { "focus", "totem", "grimoire", "lantern" };
    private static readonly HashSet<string> CasterMains = new() { "wand", "sceptre" };

    public static Stance Of(GearSet g)
    {
        string m = g?.Main?.Base, o = g?.Off?.Base;
        if (m == null && o == null) return Stance.Unarmed;
        if (m is "bow" or "crossbow") return Stance.Ranged;
        if (m is "staff" or "scythe2") return Stance.TwoHandStaff;
        if (m != null && GearAdapter.IsTwoHand(m)) return Stance.TwoHandMelee;
        if (m == "fist" || o == "fist") return Stance.Fists;
        if (o == "shield") return Stance.OneHandShield;
        if (o != null && Casters.Contains(o)) return Stance.OneHandCaster;
        if (m != null && o != null) return Stance.DualWield;
        return m != null && CasterMains.Contains(m) ? Stance.OneHandCaster : Stance.OneHand;
    }

    /// <summary>The idle a stance stands in (the classes' old idles are the stances of their old weapons).</summary>
    public static string Idle(Stance s) => s switch
    {
        Stance.TwoHandMelee or Stance.DualWield or Stance.OneHand => "Sword_Idle",
        Stance.OneHandShield => "Idle_Shield_Loop",
        Stance.Ranged => "Pistol_Idle_Loop",
        _ => "Idle_Loop",
    };

    /// <summary>True when the left hand is placed on the weapon's second grip (two-hand melee weapons).</summary>
    public static bool UsesSecondHand(Stance s, string mainBase) => s == Stance.TwoHandMelee;

    /// <summary>
    /// The clip a skill asks for, adapted to what the hero holds: no shield bash without a shield, fists instead of
    /// sword cuts when unarmed, a thrown or cast shot instead of the crossbow shot without a ranged weapon.
    /// </summary>
    public static string Remap(Stance s, string clip, GearSet g)
    {
        if (string.IsNullOrEmpty(clip)) return clip;
        bool caster = g?.Main?.Base is "staff" or "wand" or "sceptre" || (g?.Off?.Base != null && Casters.Contains(g.Off.Base));
        switch (s)
        {
            case Stance.Unarmed:
            case Stance.Fists:
                return clip switch
                {
                    "Sword_Regular_A" or "Sword_Regular_Combo" => "Punch_Jab",
                    "Sword_Regular_B" or "Sword_Regular_C" => "Punch_Cross",
                    "Sword_Attack" or "Sword_Heavy_Combo" or "Shield_OneShot" => "Melee_Hook",
                    "Pistol_Shoot" => "OverhandThrow",
                    _ => clip,
                };
            case Stance.Ranged:
                return clip is "Shield_OneShot" ? "Sword_Attack" : clip;
            case Stance.OneHandShield:
                return clip switch { "Punch_Jab" or "Punch_Cross" => "Sword_Regular_A", "Pistol_Shoot" => "OverhandThrow", _ => clip };
            default:
                return clip switch
                {
                    "Shield_OneShot" => s == Stance.DualWield ? "Sword_Regular_B" : "Sword_Attack",
                    "Shield_Dash" => "Sword_Dash",
                    "Punch_Jab" or "Punch_Cross" => s == Stance.DualWield ? "Sword_Regular_B" : "Sword_Regular_A",
                    "Pistol_Shoot" => caster ? "Spell_Simple_Shoot" : "OverhandThrow",
                    _ => clip,
                };
        }
    }
}

// ───────────────────────────────────────────── catalogue ─────────────────────────────────────────────

public sealed class GearEntry
{
    public string Id, Kind, Base, Class, Slot, File, Mount, Grip;
    public Vector3? Grip2;           // Blender weapon-local (+Z up the weapon), as written by ART-W
    public float Length;
    public string[] Hides = Array.Empty<string>();
}

/// <summary>Manifests, model files, fallbacks and the per-model caches (scenes, remapped skins).</summary>
public static class GearCatalog
{
    private const string Root = "res://assets/gear/";
    private static readonly string[] ManifestFiles = { "gear_manifest.json", "gear_manifest_armor.json", "gear_manifest_weapons.json" };
    private static Dictionary<string, GearEntry> _entries;
    private static readonly Dictionary<string, PackedScene> Scenes = new();
    private static readonly LinkedList<string> SceneOrder = new();          // least recently used first
    private const int MaxScenes = 56;          // 40 + 16 for gear sets (pinned models are never evicted anyway)

    /// <summary>
    /// Remembers a loaded model, forgetting the least recently used beyond <see cref="MaxScenes"/> (a vendor's whole stock
    /// rendered as icons must not keep every texture in video memory). Instances in use keep their own references, so
    /// forgetting is always safe: the file is only read again if it is needed again.
    /// </summary>
    private static void Remember(string id, PackedScene ps)
    {
        Scenes[id] = ps;
        SceneOrder.Remove(id);
        SceneOrder.AddLast(id);
        for (var node = SceneOrder.First; node != null && SceneOrder.Count > MaxScenes;)
        {
            var next = node.Next;
            if (!IsPinned(node.Value)) { Scenes.Remove(node.Value); SceneOrder.Remove(node); }
            node = next;
        }
    }
    private static readonly Dictionary<string, Node> RuntimeTemplates = new();   // glb read at run time (not imported yet / --gear-dir)
    private static readonly Dictionary<string, string> PathCache = new();       // id → loadable path, "" = none
    private static readonly Dictionary<string, Skin> Skins = new();
    private static readonly HashSet<string> Warned = new();
    private static bool? _off, _baked, _ph;
    private static string _dir;
    public static string ManifestStamp { get; private set; } = "";

    private static bool Flag(string n) => Game.I != null && Game.I.Arg(n);
    public static bool Off => _off ??= Flag("gear-off");
    public static bool ForceBaked => _baked ??= Flag("gear-baked");
    public static bool Placeholders => _ph ??= Flag("gear-ph");
    private static string ExtraDir => _dir ??= (Game.I?.ArgValue("gear-dir") ?? "").Trim();

    public static IReadOnlyDictionary<string, GearEntry> Entries { get { Ensure(); return _entries; } }
    public static GearEntry Entry(string id) { Ensure(); return id != null && _entries.TryGetValue(id, out var e) ? e : null; }

    /// <summary>Forget everything read (new art imported while the game runs: the test suite calls this).</summary>
    public static void Reload()
    {
        _entries = null; Scenes.Clear(); SceneOrder.Clear(); PathCache.Clear(); Skins.Clear();
        foreach (var n in RuntimeTemplates.Values) if (GodotObject.IsInstanceValid(n)) n.Free();
        RuntimeTemplates.Clear();
    }

    private static void Ensure()
    {
        if (_entries != null) return;
        _entries = new Dictionary<string, GearEntry>(StringComparer.Ordinal);
        var stamp = new System.Text.StringBuilder();
        foreach (var f in ManifestFiles)
        {
            string path = Root + f;
            if (!FileAccess.FileExists(path)) continue;
            string text = FileAccess.GetFileAsString(path);
            stamp.Append(f).Append(':').Append(text.Length).Append(':').Append(text.GetHashCode()).Append(';');
            int n = 0;
            try { foreach (var e in Parse(text)) { _entries[e.Id] = e; n++; } }
            catch (Exception ex) { Log.Error("gear", $"{f} could not be read", ex); }
            Log.Info("gear", $"{f}: {n} entries");
        }
        ManifestStamp = stamp.ToString();
        if (_entries.Count == 0) Log.Info("gear", "no gear manifest yet: model files are found by their contract names");
    }

    /// <summary>A JSON array, an object holding an array ("entries" / "models" / "items"), an object keyed by id, or one object per line.</summary>
    private static IEnumerable<GearEntry> Parse(string text)
    {
        var list = new List<JsonElement>();
        text = text.Trim();
        var opt = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        try
        {
            using var doc = JsonDocument.Parse(text, opt);
            var r = doc.RootElement.Clone();
            if (r.ValueKind == JsonValueKind.Array) list.AddRange(r.EnumerateArray());
            else if (r.ValueKind == JsonValueKind.Object)
            {
                JsonElement arr = default; bool found = false;
                foreach (var k in new[] { "entries", "models", "items", "gear" })
                    if (r.TryGetProperty(k, out arr) && arr.ValueKind == JsonValueKind.Array) { found = true; break; }
                if (found) list.AddRange(arr.EnumerateArray());
                else if (r.TryGetProperty("id", out _)) list.Add(r);
                else foreach (var p in r.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.Object) list.Add(WithId(p.Value, p.Name));
            }
        }
        catch (JsonException)
        {
            foreach (var line in text.Split('\n'))
            {
                string l = line.Trim().TrimEnd(',');
                if (l.Length < 2 || l[0] != '{') continue;
                using var d = JsonDocument.Parse(l, opt);
                list.Add(d.RootElement.Clone());
            }
        }
        foreach (var e in list)
        {
            string S(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var g = new GearEntry
            {
                Id = S("id"), Kind = S("kind"), Base = S("base"), Class = S("class"), Slot = S("slot"), File = S("file"), Mount = S("mount"), Grip = S("grip"),
            };
            if (string.IsNullOrEmpty(g.Id)) continue;
            foreach (var k in new[] { "grip2", "grip2_offset" })
                if (e.TryGetProperty(k, out var a) && a.ValueKind == JsonValueKind.Array && a.GetArrayLength() >= 3)
                { g.Grip2 = new Vector3((float)a[0].GetDouble(), (float)a[1].GetDouble(), (float)a[2].GetDouble()); break; }
            foreach (var k in new[] { "length", "length_m" })
                if (e.TryGetProperty(k, out var a) && a.ValueKind == JsonValueKind.Number) { g.Length = (float)a.GetDouble(); break; }
            foreach (var k in new[] { "hides", "hide_body_regions" })
                if (e.TryGetProperty(k, out var a) && a.ValueKind == JsonValueKind.Array) { g.Hides = a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()).ToArray(); break; }
            yield return g;
        }
    }

    private static JsonElement WithId(JsonElement obj, string id)
    {
        var d = new Dictionary<string, object> { ["id"] = id };
        foreach (var p in obj.EnumerateObject()) d[p.Name] = p.Value;
        return JsonDocument.Parse(JsonSerializer.Serialize(d)).RootElement.Clone();
    }

    private static string Folder(string id) =>
        id.StartsWith("arm_", StringComparison.Ordinal) ? "armor/" : id.StartsWith("jwl_", StringComparison.Ordinal) || id.StartsWith("gem_", StringComparison.Ordinal) ? "jewels/" : "weapons/";

    /// <summary>Where the model of an id can be read from ("" when nowhere). res:// imported → res:// raw glb → --gear-dir.</summary>
    public static string PathOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        if (PathCache.TryGetValue(id, out var p)) return p;
        p = "";
        var cands = new List<string>();
        if (ExtraDir.Length > 0) cands.Add(System.IO.Path.Combine(ExtraDir, id + ".glb"));
        var e = Entry(id);
        if (!string.IsNullOrEmpty(e?.File)) cands.Add(e.File.StartsWith("res://") || e.File.Contains(':') ? e.File : Root + e.File.TrimStart('/'));
        cands.Add(Root + Folder(id) + id + ".glb");
        if (Folder(id) == "jewels/") cands.Add(Root + "weapons/" + id + ".glb");
        foreach (var c in cands)
        {
            // res:// only once imported (a glb whose textures are not imported yet cannot be read cleanly); --gear-dir files are read raw
            if (c.StartsWith("res://")) { if (ResourceLoader.Exists(c)) { p = c; break; } continue; }
            if (FileAccess.FileExists(c)) { p = c; break; }
        }
        PathCache[id] = p;
        return p;
    }

    /// <summary>True when the id can be shown: its file exists, or (--gear-ph) a placeholder can be made for it.</summary>
    public static bool Has(string id) => PathOf(id).Length > 0 || (Placeholders && GearPlaceholders.CanMake(id));
    public static bool HasFile(string id) => PathOf(id).Length > 0;

    /// <summary>A fresh instance of a gear model (meshes and materials shared with every other instance), or null.</summary>
    public static Node3D Instantiate(string id)
    {
        string path = PathOf(id);
        if (path.Length > 0)
        {
            try
            {
                if (path.StartsWith("res://") && ResourceLoader.Exists(path))
                {
                    if (!Scenes.TryGetValue(id, out var ps)) ps = GD.Load<PackedScene>(path);
                    Remember(id, ps);
                    if (ps != null) return ps.Instantiate<Node3D>();
                }
                if (!RuntimeTemplates.TryGetValue(id, out var tpl))
                {
                    tpl = LoadGltf(path);
                    RuntimeTemplates[id] = tpl;
                    if (tpl != null) Log.Info("gear", $"'{id}' read at run time from {path} (not imported)");
                }
                if (tpl != null) return (Node3D)tpl.Duplicate();
            }
            catch (Exception e) { if (Warned.Add("load:" + id)) Log.Error("gear", $"gear model '{id}' ({path}) failed to load", e); }
        }
        return Placeholders ? GearPlaceholders.Make(id) : null;
    }

    // ── gear sets: models every saved set refers to stay loaded, so a swap is instant ──
    private static readonly Dictionary<string, HashSet<string>> Pins = new();       // owner → model ids
    private static bool IsPinned(string id) { foreach (var p in Pins.Values) if (p.Contains(id)) return true; return false; }

    /// <summary>
    /// Keeps every model of these loadouts loaded (threaded, never evicted) and their materials converted, so equipping
    /// any of them — a gear-set swap, several slots at once, in combat — is built in the same frame without a stall.
    /// Call again whenever the sets change (the previous pins of <paramref name="owner"/> are replaced); an empty list unpins.
    /// </summary>
    public static void PreloadSets(string owner, ClassId cls, IEnumerable<IEnumerable<Item>> sets)
    {
        var ids = new HashSet<string>();
        foreach (var set in sets ?? Enumerable.Empty<IEnumerable<Item>>())
        {
            var g = GearSet.FromItems(cls, set);
            foreach (var a in g.Armor) { var id = ResolveArmor(cls, a); if (id != null) ids.Add(id); }
            foreach (var w in new[] { g.Main, g.Off }) { var id = ResolveWeapon(w); if (id != null) ids.Add(id); }
        }
        if (ids.Count == 0) { Pins.Remove(owner); return; }
        Pins[owner] = ids;
        var waiting = new HashSet<string>(ids.Where(id => !Warm(id)));
        if (waiting.Count == 0 || Engine.GetMainLoop() is not SceneTree tree) return;
        Action poll = null;
        poll = () =>
        {
            waiting.RemoveWhere(Warm);
            if (waiting.Count == 0) tree.ProcessFrame -= poll;
        };
        tree.ProcessFrame += poll;
    }

    private static readonly HashSet<string> Warmed = new();

    /// <summary>True once the model is loaded and its materials converted (the conversion is done once per model).</summary>
    private static bool Warm(string id)
    {
        if (!Prefetch(id)) return false;
        if (Warmed.Add(id))
        {
            var n = Instantiate(id);
            if (n != null) { foreach (var mi in Walk(n).OfType<MeshInstance3D>()) CharModel.ConvertForWorld(mi); n.Free(); }
        }
        return true;
    }

    /// <summary>Threaded load of an imported model (icons): true once Instantiate will not block.</summary>
    public static bool Prefetch(string id)
    {
        if (Scenes.ContainsKey(id) || RuntimeTemplates.ContainsKey(id)) return true;
        string path = PathOf(id);
        if (!path.StartsWith("res://") || !ResourceLoader.Exists(path)) return true;
        var st = ResourceLoader.LoadThreadedGetStatus(path);
        if (st == ResourceLoader.ThreadLoadStatus.InvalidResource) { ResourceLoader.LoadThreadedRequest(path, "", true); return false; }
        if (st == ResourceLoader.ThreadLoadStatus.InProgress) return false;
        if (st == ResourceLoader.ThreadLoadStatus.Loaded) Remember(id, ResourceLoader.LoadThreadedGet(path) as PackedScene);
        return true;
    }

    private static Node LoadGltf(string path)
    {
        string abs = path.StartsWith("res://") ? ProjectSettings.GlobalizePath(path) : path;
        var doc = new GltfDocument();
        var st = new GltfState();
        if (doc.AppendFromFile(abs, st) != Error.Ok) return null;
        return doc.GenerateScene(st);
    }

    // ── choosing a model ──

    public static string ArmorId(ClassId c, string slot, int tier) => $"arm_{Names.ClassKey(c)}_{slot}_t{tier}";
    public static string WeaponId(string b, int v) => $"wpn_{b}_v{v}";

    /// <summary>The armour look to show: the asked one, else the nearest tier of the same class and slot, else null.</summary>
    public static string ResolveArmor(ClassId c, ArmorPick a)
    {
        if (a == null) return null;
        if (a.Model != null && a.Model.StartsWith($"arm_{Names.ClassKey(c)}_") && Has(a.Model)) return a.Model;
        foreach (int t in Nearest(a.Tier, 4)) { string id = ArmorId(c, a.Slot, t); if (Has(id)) return id; }
        return null;
    }

    /// <summary>The weapon model to show: the asked one, else the nearest variant of the base, else null.</summary>
    public static string ResolveWeapon(WeaponPick w)
    {
        if (w == null) return null;
        if (w.Model != null && w.Model.StartsWith("wpn_") && HasFile(w.Model)) return w.Model;
        foreach (int v in Nearest(w.Variant, 3)) { string id = WeaponId(w.Base, v); if (HasFile(id)) return id; }
        return null;
    }

    private static IEnumerable<int> Nearest(int want, int max)
    {
        want = Math.Clamp(want, 1, max);
        yield return want;
        for (int d = 1; d < max; d++)
        {
            if (want - d >= 1) yield return want - d;
            if (want + d <= max) yield return want + d;
        }
    }

    /// <summary>The model id of an item for icons and ground loot (resolved to something that exists), or null.</summary>
    public static string ModelIdFor(Item it, ClassId cls)
    {
        if (it == null) return null;
        string look = GearAdapter.LookOf(it, cls);
        if (it.Kind is ItemKind.Weapon or ItemKind.OffHand)
        {
            string b = GearAdapter.BaseOf(it);
            if (look != null && HasFile(look)) return look;
            if (b != null)
            {
                int v = 2;
                if (look != null && look.Length > 2 && look[^2] == 'v') int.TryParse(look[^1..], out v);
                foreach (int k in Nearest(v, 3)) { string id = WeaponId(b, k); if (HasFile(id)) return id; }
            }
            return Placeholders && b != null ? "ph_" + WeaponId(b, Math.Clamp(it.Rarity >= Rarity.Legendary ? 3 : it.Rarity == Rarity.Rare ? 2 : 1, 1, 3)) : null;
        }
        if (it.Kind is ItemKind.Ring or ItemKind.Amulet or ItemKind.Gem)
        {
            if (look != null && Has(look)) return look;
            if (it.Kind != ItemKind.Gem)
            {
                string pre = it.Kind == ItemKind.Ring ? "jwl_ring_v" : "jwl_amulet_v";
                foreach (int k in Nearest(it.Rarity >= Rarity.Legendary ? 3 : it.Rarity == Rarity.Rare ? 2 : 1, 3)) if (Has(pre + k)) return pre + k;
            }
            return null;
        }
        if (look != null && look.StartsWith("arm_"))
        {
            int u = look.LastIndexOf("_t", StringComparison.Ordinal);
            int s0 = look.IndexOf('_', 4);
            if (u > s0 && s0 > 0 && int.TryParse(look[(u + 2)..], out int t))
            {
                string slot = look[(s0 + 1)..u];
                string id = ResolveArmor(cls, new ArmorPick { Slot = slot, Tier = t, Model = look });
                if (id != null) return id;
                // a piece made for another body still shows what the item looks like
                foreach (ClassId c in Enum.GetValues<ClassId>()) { id = ResolveArmor(c, new ArmorPick { Slot = slot, Tier = t }); if (id != null && HasFile(id)) return id; }
            }
        }
        return null;
    }

    // ── per-file facts ──

    /// <summary>A value the model file carries in its glTF extras (node metadata), from any of its nodes; null when absent.</summary>
    public static Variant? Meta(Node inst, string key)
    {
        foreach (var n in Walk(inst))
        {
            if (n.HasMeta(key)) return n.GetMeta(key);
            if (!n.HasMeta("extras")) continue;
            var v = n.GetMeta("extras");
            if (v.VariantType == Variant.Type.Dictionary && v.AsGodotDictionary().ContainsKey(key)) return v.AsGodotDictionary()[key];
        }
        return null;
    }

    public static string MountFor(string id, string baseId, bool main, Node inst = null)
    {
        string m = Entry(id)?.Mount;
        if (string.IsNullOrEmpty(m) && inst != null && Meta(inst, "mount") is Variant mv && mv.VariantType == Variant.Type.String) m = mv.AsString();
        // a model made for the right hand held in the left (dual wield) goes to the left
        if (m == "hand_r" && !main && !GearAdapter.IsOffHandBase(baseId)) m = "hand_l";
        if (!string.IsNullOrEmpty(m)) return m;
        if (baseId == "shield") return "forearm_l";
        if (baseId == "bow") return "hand_l";
        if (GearAdapter.IsOffHandBase(baseId)) return "hand_l";
        return main ? "hand_r" : "hand_l";
    }

    /// <summary>The second-hand grip in weapon space (contract axes: +Z up the weapon), from the manifest, the file, or a guess per base.</summary>
    public static Vector3 Grip2For(string id, string baseId, Node inst = null)
    {
        var e = Entry(id);
        if (e?.Grip2 is Vector3 b) return b;
        foreach (var key in new[] { "grip2_offset", "grip2" })
            if (inst != null && Meta(inst, key) is Variant v)
            {
                float[] f = v.VariantType switch
                {
                    Variant.Type.Array => v.AsGodotArray().Select(x => (float)x.AsDouble()).ToArray(),
                    Variant.Type.PackedFloat32Array => v.AsFloat32Array(),
                    Variant.Type.PackedFloat64Array => v.AsFloat64Array().Select(x => (float)x).ToArray(),
                    _ => null,
                };
                if (f != null && f.Length >= 3) return new Vector3(f[0], f[1], f[2]);
            }
        return baseId switch { "sword2" => new Vector3(0, 0, -0.15f), "staff" => new Vector3(0, 0, 0.32f), _ => new Vector3(0, 0, -0.34f) };
    }

    /// <summary>The body regions an armour piece hides: manifest "hides", else the file's hide_body_regions metadata, else the slot's skin.</summary>
    public static string[] HidesFor(string id, Node inst, string slot)
    {
        var e = Entry(id);
        if (e != null && e.Hides.Length > 0) return e.Hides;
        foreach (var n in Walk(inst))
        {
            foreach (var key in new[] { "hide_body_regions", "extras" })
            {
                if (!n.HasMeta(key)) continue;
                var v = n.GetMeta(key);
                if (key == "extras" && v.VariantType == Variant.Type.Dictionary)
                {
                    var d = v.AsGodotDictionary();
                    if (!d.ContainsKey("hide_body_regions")) continue;
                    v = d["hide_body_regions"];
                }
                if (v.VariantType == Variant.Type.String) return v.AsString().Split(',', ' ', ';').Where(s => s.Length > 0).ToArray();
                if (v.VariantType is Variant.Type.Array or Variant.Type.PackedStringArray) return v.AsStringArray();
            }
        }
        return DefaultHides(slot);
    }

    public static string[] DefaultHides(string slot) => slot switch
    {
        "chest" => new[] { "torso", "upper_arms", "wrap_chest" }, "gloves" => new[] { "hands" }, "pants" => new[] { "hips", "thighs", "wrap_loin" }, "boots" => new[] { "feet" },
        _ => Array.Empty<string>(),
    };

    /// <summary>The armour file's skin bound by bone name onto the hero's skeleton (cached per file and mesh).</summary>
    public static Skin RemapSkin(string key, Skin src, Skeleton3D from, Skeleton3D to)
    {
        if (src == null || to == null) return src;
        if (Skins.TryGetValue(key, out var s)) return s;
        s = (Skin)src.Duplicate();
        int missing = 0;
        for (int i = 0; i < s.GetBindCount(); i++)
        {
            string name = s.GetBindName(i).ToString();
            if (string.IsNullOrEmpty(name) && from != null)
            {
                int b = s.GetBindBone(i);
                if (b >= 0 && b < from.GetBoneCount()) name = from.GetBoneName(b);
            }
            if (string.IsNullOrEmpty(name)) continue;
            int t = to.FindBone(name);
            if (t < 0) { missing++; continue; }
            s.SetBindName(i, name);
            s.SetBindBone(i, t);
        }
        if (missing > 0 && Warned.Add("skin:" + key)) Log.Warn("gear", $"'{key}': {missing} bones of its skin are not on the hero skeleton");
        Skins[key] = s;
        return s;
    }

    internal static IEnumerable<Node> Walk(Node n)
    {
        if (n == null) yield break;
        yield return n;
        foreach (var c in n.GetChildren()) foreach (var d in Walk(c)) yield return d;
    }

    /// <summary>
    /// A loot item's model lying on the ground (a wrapper the loot animation may turn and scale), or null when the item
    /// has no model file (the caller keeps its loot mesh). Materials are the characters' (graded with the level).
    /// A model not loaded yet is read on the loader thread: meanwhile the wrapper shows <paramref name="fallback"/> (the
    /// old loot mesh), and the model takes its place as soon as it is there, so a shower of loot never stalls a frame.
    /// <paramref name="modelScale"/> multiplies the model against the wrapper's own scale (the loot animation's).
    /// </summary>
    public static Node3D GroundModel(Item it, Node3D fallback = null, float modelScale = 1f)
    {
        if (it == null || Off) return null;
        string id;
        try { id = ModelIdFor(it, Game.I?.Hero?.Class ?? ClassId.Emberblade); }
        catch (Exception e) { Log.Error("gear", "ground model lookup failed", e); return null; }
        if (id == null) return null;
        var wrap = new Node3D { Name = "LootModel" };
        if (Prefetch(id))
        {
            if (!FillGround(wrap, id, modelScale)) { wrap.Free(); return null; }
            if (fallback != null && GodotObject.IsInstanceValid(fallback) && fallback.GetParent() == null) fallback.Free();     // never shown: do not leave it orphaned
            return wrap;
        }
        if (fallback != null) wrap.AddChild(fallback);
        if (Engine.GetMainLoop() is not SceneTree tree) return wrap;
        Action poll = null;
        poll = () =>
        {
            if (!GodotObject.IsInstanceValid(wrap)) { tree.ProcessFrame -= poll; return; }
            if (!Prefetch(id)) return;
            tree.ProcessFrame -= poll;
            if (FillGround(wrap, id, modelScale) && fallback != null && GodotObject.IsInstanceValid(fallback)) { wrap.RemoveChild(fallback); fallback.QueueFree(); }
        };
        tree.ProcessFrame += poll;
        return wrap;
    }

    private static bool FillGround(Node3D wrap, string id, float modelScale)
    {
        var model = Instantiate(id);
        if (model == null) return false;
        // wrap (turned and scaled by the loot animation) → fit (size) → lay (lying pose) → model
        var fit = new Node3D { Name = "Fit" };
        var lay = new Node3D { Name = "Lay" };
        wrap.AddChild(fit);
        fit.AddChild(lay);
        lay.AddChild(model);
        bool armor = id.Contains("arm_");
        // armour lies on its back; a weapon, off-hand or jewel rests on its flattest side (its thinnest axis to the sky)
        if (armor) lay.Basis = new Basis(Vector3.Right, -Mathf.Pi / 2f);
        else
        {
            var own = LocalBounds(model, lay).Size;
            Basis rest = own.Y <= own.X && own.Y <= own.Z ? Basis.Identity
                : own.Z <= own.X ? new Basis(Vector3.Right, -Mathf.Pi / 2f) : new Basis(Vector3.Back, Mathf.Pi / 2f);
            lay.Basis = new Basis(Vector3.Up, 0.35f) * rest;
        }
        var box = LocalBounds(model, lay);
        lay.Position = new Vector3(-box.GetCenter().X, -box.Position.Y + 0.015f, -box.GetCenter().Z);
        // an armour piece in its bind pose is a body's width: shrink it to a pile that reads as loot; a ring grows to be seen
        float span = Mathf.Max(box.Size.X, box.Size.Z), k = modelScale;
        if (span > 0.001f && (span > 0.75f || span < 0.16f)) k *= Mathf.Clamp(span, 0.16f, 0.75f) / span;
        fit.Scale = Vector3.One * k;
        foreach (var mi in Walk(model).OfType<MeshInstance3D>())
        {
            CharModel.ConvertForWorld(mi);
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            if (armor) mi.SetInstanceShaderParameter("tint", new Vector3(0.68f, 0.68f, 0.68f));     // pale plate under a loot beam: a little less exposure
        }
        foreach (var ap in Walk(model).OfType<AnimationPlayer>().ToList()) ap.QueueFree();
        return true;
    }

    /// <summary>Bounds of every mesh under <paramref name="n"/> in the space of <paramref name="space"/> (nodes need not be in the tree).</summary>
    public static Aabb LocalBounds(Node n, Node3D space)
    {
        var box = new Aabb(); bool first = true;
        foreach (var mi in Walk(n).OfType<VisualInstance3D>())
        {
            var a = mi.GetAabb();
            var xf = Transform3D.Identity;
            for (Node p = mi; p != null && p != space; p = p.GetParent()) if (p is Node3D p3) xf = p3.Transform * xf;
            if (space != null) xf = new Transform3D(space.Basis, Vector3.Zero) * xf;      // the space's turn, not its offset
            for (int i = 0; i < 8; i++)
            {
                var q = xf * a.GetEndpoint(i);
                if (first) { box = new Aabb(q, Vector3.Zero); first = false; } else box = box.Expand(q);
            }
        }
        return box;
    }
}

// ───────────────────────────────────────────── grips (charlib conventions) ─────────────────────────────────────────────

/// <summary>
/// Weapon frames on the hand and forearm bones, ported from tools/blender/heroes/charlib.py (grip, staff_grip) and
/// kit_twn.py (shield_frame). The frames are in the bone's own space (the glTF export keeps Blender's bone axes) and
/// take a weapon in the contract's axes: +Z up the weapon, +X the edge, +Y the palm normal — ART-W exports with
/// export_yup=False, so a weapon file's own axes are exactly these. The primitive stand-ins are built Y-up and wrapped in G2B.
/// </summary>
public static class GearGrips
{
    private static readonly Vector3 GripCR = new(-0.031f, 0.103f, -0.004f), GripCL = new(0.031f, 0.103f, -0.004f);
    private static readonly Vector3 GripAR = new(-0.04f, 0.17f, 0.99f), GripAL = new(0.04f, 0.17f, 0.99f);
    /// <summary>Y-up model space (+Y up the weapon) → contract weapon space (+Z up the weapon).</summary>
    public static readonly Transform3D G2B = new(new Basis(new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, -1, 0)), Vector3.Zero);

    private static Transform3D Rot(Vector3 axis, float deg) => new(new Basis(axis, Mathf.DegToRad(deg)), Vector3.Zero);

    /// <summary>charlib.grip(side, tilt, roll, slide, push) in the hand bone's space.</summary>
    public static Transform3D Hand(bool right, float tilt = 0f, float roll = 0f, float slide = 0f, float push = 0f)
    {
        Vector3 z = (right ? GripAR : GripAL).Normalized();
        Vector3 x = (Vector3.Up - z * Vector3.Up.Dot(z)).Normalized();
        Vector3 y = z.Cross(x);
        var L = new Transform3D(new Basis(x, y, z), right ? GripCR : GripCL);
        L = L * Rot(Vector3.Up, -tilt) * Rot(Vector3.Back, roll) * new Transform3D(Basis.Identity, new Vector3(push, 0, slide));
        return L;
    }

    /// <summary>kit_cst.staff_grip: more upright (tilt) and leaning away from the body (lean).</summary>
    public static Transform3D Staff(bool right, float tilt, float lean)
    {
        Vector3 z = (right ? GripAR : GripAL).Normalized();
        Vector3 x = (Vector3.Up - z * Vector3.Up.Dot(z)).Normalized();
        var L = new Transform3D(new Basis(x, z.Cross(x), z), right ? GripCR : GripCL) * Rot(Vector3.Up, -tilt) * Rot(Vector3.Right, right ? lean : -lean);
        return L;
    }

    /// <summary>
    /// kit_twn.shield_frame("l") moved onto the forearm: shield +Y (Blender) = face out of the back of the hand, +Z = up
    /// (thumb side), X along the forearm; origin <paramref name="back"/> behind the wrist, <paramref name="lift"/> off the arm.
    /// </summary>
    public static Transform3D Forearm(Skeleton3D sk, float back = 0.10f, float lift = 0.055f)
    {
        Vector3 along = Vector3.Up;
        Vector3 thumb = GripAL.Normalized(); thumb = (thumb - along * thumb.Dot(along)).Normalized();
        Vector3 dorsal = new Vector3(-1, 0, 0); dorsal = (dorsal - thumb * dorsal.Dot(thumb)).Normalized();
        Vector3 x = dorsal.Cross(thumb);
        var M = new Transform3D(new Basis(x, dorsal, thumb), -along * back + dorsal * lift);
        int h = sk?.FindBone("hand_l") ?? -1;
        var handRest = h >= 0 ? sk.GetBoneRest(h) : Transform3D.Identity;     // hand_l in lowerarm_l's space
        return handRest * M;
    }

    /// <summary>The frame of a base on its mount bone.</summary>
    public static Transform3D For(string baseId, string mount, Skeleton3D sk)
    {
        if (mount == "forearm_l") return Forearm(sk);
        bool right = mount != "hand_l";
        return baseId switch
        {
            "staff" => Staff(right, 46f, 30f),
            "scythe2" => Staff(right, 25f, 18f),
            "dagger" when !right => Hand(false) * new Transform3D(new Basis(Vector3.Right, Mathf.Pi), new Vector3(0, 0, -0.012f)),   // reverse grip (the hunter's off-hand dagger)
            _ => Hand(right),
        };
    }
}

// ───────────────────────────────────────────── assembly on CharModel ─────────────────────────────────────────────

public partial class CharModel
{
    private static readonly string[] HeroIds = Names.ClassKeys.Select(k => "hero_" + k).ToArray();

    /// <summary>True for the eight hero bodies (the models gear is put on).</summary>
    public bool IsHeroBody => ModelId != null && Array.IndexOf(HeroIds, ModelId) >= 0;
    /// <summary>True when this hero is assembled from a base body and armour pieces (false: today's baked hero).</summary>
    public bool Modular { get; private set; }
    /// <summary>True when the hero shows its equipment through <see cref="SetGear"/> (false: --gear-off, not a hero).</summary>
    public bool GearManaged => IsHeroBody && !GearCatalog.Off && Skeleton != null;
    public GearSet Gear { get; private set; }
    public Stance Stance { get; private set; } = Stance.Unarmed;
    /// <summary>The idle the current loadout stands in.</summary>
    public string StanceIdle => Stances.Idle(Stance);
    public string GearSignature => _gearSig ?? "";
    /// <summary>Raised after the equipment shown changed (meshes added or removed).</summary>
    public event Action GearChanged;
    /// <summary>For logs and the test suite: how the last loadout was built ("arm_x_chest_t3", "wpn_sword1_v2 (placeholder)", ...).</summary>
    public string GearReport { get; private set; } = "";

    private bool _phBase;
    private string _gearSig;
    private readonly List<Node> _gearNodes = new();
    private readonly List<MeshInstance3D> _gearMeshes = new(), _gearWeapons = new(), _body = new();
    private readonly Dictionary<MeshInstance3D, int> _hiddenBy = new();
    private GearGripIK _ik;
    /// <summary>The left-hand grip IK of the current loadout (null without a two-hand melee weapon); diagnostics for the tests.</summary>
    private static bool _ikArgRead;
    /// <summary>Names of the body meshes the worn armour hides (diagnostics).</summary>
    public string HiddenBody => string.Join(",", _hiddenBy.Keys.Where(IsInstanceValid).Select(m => m.Name.ToString()));
    public GearGripIK GripIK => _ik != null && IsInstanceValid(_ik) ? _ik : null;
    private Color _glowColor = Colors.Black;

    /// <summary>Material conversion for gear shown outside a character (ground loot).</summary>
    internal static void ConvertForWorld(MeshInstance3D mi) => ConvertMaterials(mi);

    /// <summary>The base body to build a hero from, or null for today's baked hero (sets <see cref="Modular"/>).</summary>
    private PackedScene ResolveModular()
    {
        if (!IsHeroBody || GearCatalog.Off || GearCatalog.ForceBaked) return null;
        string id = ModelId + "_base";
        if (ResourceLoader.Exists($"res://assets/chars/{id}.glb"))
        {
            var s = LoadScene(id);
            if (s != null) { FileId = id; Modular = true; return s; }
        }
        if (GearCatalog.Placeholders)
        {
            var s = LoadScene(ModelId);
            if (s != null) { FileId = ModelId; Modular = true; _phBase = true; return s; }
        }
        return null;
    }

    /// <summary>After the base body is instanced: placeholder split (--gear-ph) and the list of body region meshes.</summary>
    private void PrepareModular()
    {
        if (!Modular) return;
        if (_phBase) GearPlaceholders.SplitBody(_inst, Skeleton, ModelId);
        foreach (var mi in Walk(_inst).OfType<MeshInstance3D>().ToList())
        {
            if (mi.Name.ToString().StartsWith("wpn_", StringComparison.OrdinalIgnoreCase)) { mi.GetParent().RemoveChild(mi); mi.Free(); continue; }
            _body.Add(mi);
            mi.LodBias = 100f;          // body regions stay on LOD 0: coarser LODs open hairline cracks at the region borders (REGIONS.md)
        }
    }

    /// <summary>
    /// Shows exactly this equipment: armour pieces on the base body (or nothing on a baked hero), weapons in the hands,
    /// the stance's idle. Cheap when the loadout did not change (same signature).
    /// </summary>
    public void SetGear(GearSet g)
    {
        if (g == null || !GearManaged) return;
        string sig = g.Signature + (Modular ? "|m" : "|b");
        if (sig == _gearSig) { _pendingGear = null; return; }
        // the first loadout is built at once (the hero must not appear naked); later changes wait for their files on the
        // loader thread and keep showing the old pieces meanwhile, so equipping never stalls a frame
        if (_gearSig != null && !GearReady(g)) { _pendingGear = g; SetProcess(true); return; }
        _pendingGear = null;
        ApplyGear(g, sig);
    }

    private GearSet _pendingGear;
    /// <summary>True while a loadout waits for its model files (it is applied as soon as they are loaded).</summary>
    public bool GearPending => _pendingGear != null;

    /// <summary>True when every model of the loadout can be instanced without blocking (starts threaded loads otherwise).</summary>
    private bool GearReady(GearSet g)
    {
        bool ok = true;
        if (Modular) foreach (var a in g.Armor) { string id = GearCatalog.ResolveArmor(g.Class, a); if (id != null && !GearCatalog.Prefetch(id)) ok = false; }
        foreach (var w in new[] { g.Main, g.Off }) { string id = GearCatalog.ResolveWeapon(w); if (id != null && !GearCatalog.Prefetch(id)) ok = false; }
        return ok;
    }

    /// <summary>Called every frame while a loadout waits for its files (CharModel._Process).</summary>
    private void TickGear()
    {
        if (_prebuild.Count > 0 && _pendingGear == null) TickPrebuild();
        if (_pendingGear == null || !GearReady(_pendingGear)) return;
        var g = _pendingGear;
        _pendingGear = null;
        ApplyGear(g, g.Signature + (Modular ? "|m" : "|b"));
    }

    /// <summary>How many times any hero model was rebuilt (the gear-set test checks one rebuild per swap).</summary>
    public static int GearRebuilds;

    private void ApplyGear(GearSet g, string sig)
    {
        GearRebuilds++;
        ulong t0 = Time.GetTicksUsec();
        float vis = _visibility;
        if (vis < 0.99f) SetVisibility(1f);
        ClearGear();
        Gear = g; _gearSig = sig;
        var report = new List<string>();
        var fresh = new List<MeshInstance3D>();
        try
        {
            if (Modular)
                foreach (var a in g.Armor)
                {
                    if (a == null) continue;
                    string id = GearCatalog.ResolveArmor(g.Class, a);
                    if (id == null) { report.Add($"{a.Slot}:none"); continue; }
                    if (AttachArmor(id, a, fresh)) report.Add(id + (GearCatalog.HasFile(id) ? "" : " (placeholder)"));
                }
            Stance = Stances.Of(g);
            AttachWeapon(g.Main, true, g, fresh, report);
            AttachWeapon(g.Off, false, g, fresh, report);
        }
        catch (Exception e) { Log.Error("gear", $"assembling {ModelId} [{sig}] failed", e); }
        _meshes.AddRange(fresh);           // materials, shadows and LOD were set once when the piece was built (FinishPiece)
        _gearMeshes.AddRange(fresh);
        _glowColor = g.Main != null && g.Main.Rarity >= Rarity.Legendary ? Palette.Rarity(g.Main.Rarity) : Colors.Black;
        // the instance state of the model (tint, rim, frost, dissolve) carries over to the new pieces
        foreach (var mi in fresh)
        {
            mi.SetInstanceShaderParameter(PTint, new Vector3(_tint.R, _tint.G, _tint.B));
            mi.SetInstanceShaderParameter(PFrozen, _frozen);
            mi.SetInstanceShaderParameter(PDissolve, _dissolve);
        }
        SetRim(_rim);
        _glow = null;
        if (vis < 0.99f) SetVisibility(vis);
        GearReport = $"{(Modular ? (_phBase ? "modular(placeholder body)" : "modular") : "baked")} stance={Stance} [{string.Join(", ", report)}]";
        Log.Info("gear", $"{ModelId}: {GearReport} in {(Time.GetTicksUsec() - t0) / 1000.0:0.0} ms");
        GearChanged?.Invoke();
    }

    private void ClearGear()
    {
        foreach (var mi in _gearMeshes) _meshes.Remove(mi);
        _gearMeshes.Clear(); _gearWeapons.Clear();
        foreach (var n in _gearNodes)
            if (IsInstanceValid(n)) { n.GetParent()?.RemoveChild(n); n.QueueFree(); }
        _gearNodes.Clear();
        // pooled pieces are only hidden: the next loadout (a gear-set swap) shows them again without building anything
        foreach (var p in _shown) foreach (var r in p.Roots) if (IsInstanceValid(r)) r.Visible = false;
        _shown.Clear();
        _ik = null;
        foreach (var mi in _body) if (IsInstanceValid(mi)) mi.Visible = true;
        _hiddenBy.Clear();
        foreach (var w in _weapons) w.Visible = false;
    }

    // ── pooled pieces: every armour piece and weapon built for this model stays under its skeleton, hidden when not worn ──
    private sealed class Piece
    {
        public readonly List<Node3D> Roots = new();
        public readonly List<MeshInstance3D> Meshes = new();
        public string[] Hides = Array.Empty<string>();
        public bool Weapon;
        public string Mount, What, Id;
        public Transform3D Grip;
        public Node Model;
        public ulong Used;
    }
    private readonly Dictionary<string, Piece> _pieces = new();
    private readonly List<Piece> _shown = new();
    private readonly Queue<(string key, Func<Piece> build)> _prebuild = new();
    private ulong _pieceClock;
    private const int MaxPieces = 40;            // three or four full sets of seven pieces, and what is worn
    /// <summary>True while the model has gear work to do over the next frames (a loadout waiting for files, pieces being pre-built).</summary>
    private bool GearBusy => _pendingGear != null || _prebuild.Count > 0;

    private static string ArmorKey(string id) => "arm:" + id;
    private static string WeaponKey(WeaponPick w, string id, bool main) => $"wpn:{id ?? "prim_" + w.Base + w.Variant}@{GearCatalog.MountFor(id, w.Base, main)}";

    private Piece BuildArmorPiece(string id, string slot)
    {
        var inst = GearCatalog.Instantiate(id);
        if (inst == null) return null;
        var srcSk = FindSkeleton(inst);
        var piece = new Piece { Id = id, What = id, Hides = GearCatalog.HidesFor(id, inst, slot) };
        int n = 0;
        foreach (var mi in Walk(inst).OfType<MeshInstance3D>().ToList())
        {
            if (mi.Mesh == null) continue;
            BoneAttachment3D ba = null;
            var rel = Transform3D.Identity;
            for (Node p = mi; p != null && p != inst; p = p.GetParent())
            {
                if (p is BoneAttachment3D b) { ba = b; break; }
                if (p is Node3D p3) rel = p3.Transform * rel;
            }
            string key = id + "/" + mi.Name;
            mi.GetParent().RemoveChild(mi);
            mi.Owner = null;
            foreach (var c in mi.GetChildren().ToList()) { mi.RemoveChild(c); c.QueueFree(); }
            mi.Name = "gear_" + id + "_" + n;
            if (mi.Skin != null)
            {
                Skeleton.AddChild(mi);
                mi.Transform = Transform3D.Identity;
                mi.Skeleton = new NodePath("..");
                mi.Skin = GearCatalog.RemapSkin(key, mi.Skin, srcSk, Skeleton);
                piece.Roots.Add(mi);
            }
            else if (ba != null && Skeleton.FindBone(ba.BoneName) >= 0)
            {
                var att = new BoneAttachment3D { BoneName = ba.BoneName, Name = "gear_att_" + ba.BoneName };
                Skeleton.AddChild(att);
                att.AddChild(mi);
                mi.Transform = rel;
                piece.Roots.Add(att);
            }
            else
            {
                Skeleton.AddChild(mi);
                mi.Transform = rel;
                piece.Roots.Add(mi);
            }
            piece.Meshes.Add(mi);
            n++;
        }
        inst.Free();
        if (n == 0) return null;
        FinishPiece(piece);
        return piece;
    }

    private Piece BuildWeaponPiece(WeaponPick w, string id, bool main)
    {
        Node3D model = id != null ? GearCatalog.Instantiate(id) : null;
        string mount = GearCatalog.MountFor(id, w.Base, main);
        string what = id;
        if (model == null) { model = GearPlaceholders.Weapon(w.Base, w.Variant); what = $"{w.Base} v{w.Variant} (primitive)"; }
        if (model == null) return null;
        string bone = mount == "forearm_l" ? "lowerarm_l" : mount;
        if (Skeleton.FindBone(bone) < 0) { Log.Warn("gear", $"{ModelId} has no bone '{bone}' for {what}"); model.Free(); return null; }
        var att = new BoneAttachment3D { Name = "Gear_" + (id ?? w.Base) + "_" + mount, BoneName = bone };
        Skeleton.AddChild(att);
        var grip = GearGrips.For(w.Base, mount, Skeleton);
        var holder = new Node3D { Name = "Grip", Transform = grip };
        att.AddChild(holder);
        holder.AddChild(model);
        foreach (var ap in Walk(model).OfType<AnimationPlayer>().ToList()) ap.QueueFree();
        var piece = new Piece { Id = id, Weapon = true, Mount = mount, What = what, Grip = grip, Model = model };
        piece.Roots.Add(att);
        int k = 0;
        foreach (var mi in Walk(model).OfType<MeshInstance3D>())
        {
            mi.Name = $"gear_{id ?? "prim_" + w.Base}_{mount}_{k++}";
            piece.Meshes.Add(mi);
        }
        FinishPiece(piece);
        return piece;
    }

    /// <summary>A new piece: materials converted once, hidden until worn.</summary>
    private void FinishPiece(Piece p)
    {
        foreach (var mi in p.Meshes)
        {
            ConvertMaterials(mi);
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            mi.ExtraCullMargin = 1.5f;
            if (Modular) mi.LodBias = 100f;
        }
        foreach (var r in p.Roots) r.Visible = false;
    }

    private Piece GetPiece(string key, Func<Piece> build)
    {
        if (!_pieces.TryGetValue(key, out var p) || p.Roots.Any(r => !IsInstanceValid(r)))
        {
            p = build();
            if (p == null) return null;
            _pieces[key] = p;
            TrimPieces();
        }
        p.Used = ++_pieceClock;
        return p;
    }

    private void TrimPieces()
    {
        while (_pieces.Count > MaxPieces)
        {
            var old = _pieces.Where(kv => !_shown.Contains(kv.Value)).OrderBy(kv => kv.Value.Used).FirstOrDefault();
            if (old.Value == null) return;
            foreach (var r in old.Value.Roots) if (IsInstanceValid(r)) { r.GetParent()?.RemoveChild(r); r.QueueFree(); }
            _pieces.Remove(old.Key);
        }
    }

    private void ShowPiece(Piece p, List<MeshInstance3D> fresh)
    {
        foreach (var r in p.Roots) r.Visible = true;
        fresh.AddRange(p.Meshes);
        if (p.Weapon) _gearWeapons.AddRange(p.Meshes);
        foreach (var h in p.Hides) HideRegion(h);
        _shown.Add(p);
    }

    /// <summary>
    /// Builds (hidden, one piece per frame, once its file is loaded) every armour piece and weapon of these loadouts, so
    /// that switching to any of them only shows and hides nodes. Player calls it with the hero's saved gear sets.
    /// </summary>
    public void PrebuildGear(IEnumerable<GearSet> sets)
    {
        if (!GearManaged || Skeleton == null) return;
        foreach (var g in sets ?? Enumerable.Empty<GearSet>())
        {
            if (g == null) continue;
            if (Modular)
                foreach (var a in g.Armor)
                {
                    string id = GearCatalog.ResolveArmor(g.Class, a);
                    if (id == null || _pieces.ContainsKey(ArmorKey(id))) continue;
                    string slot = a.Slot;
                    _prebuild.Enqueue((ArmorKey(id), () => BuildArmorPiece(id, slot)));
                }
            foreach (var (w, main) in new[] { (g.Main, true), (g.Off, false) })
            {
                if (w == null) continue;
                string id = GearCatalog.ResolveWeapon(w);
                if (id == null && !Modular && _weapons.Count > 0) continue;         // baked hero: its own weapon meshes, nothing to build
                string key = WeaponKey(w, id, main);
                if (!_pieces.ContainsKey(key)) _prebuild.Enqueue((key, () => BuildWeaponPiece(w, id, main)));
            }
        }
        if (_prebuild.Count > 0) SetProcess(true);
    }

    /// <summary>One pre-built piece per frame (a piece whose file is still loading waits its turn).</summary>
    private void TickPrebuild()
    {
        int n = _prebuild.Count;
        while (n-- > 0)
        {
            var (key, build) = _prebuild.Dequeue();
            if (_pieces.ContainsKey(key)) continue;
            string id = key.StartsWith("arm:") ? key[4..] : !key.StartsWith("wpn:prim_") ? key[4..key.IndexOf('@')] : null;
            if (id != null && !GearCatalog.Prefetch(id)) { _prebuild.Enqueue((key, build)); continue; }
            GetPiece(key, build);
            PrebuiltPieces++;
            return;
        }
    }

    /// <summary>How many pieces were pre-built hidden (the gear-set test reports it).</summary>
    public static int PrebuiltPieces;

    private bool AttachArmor(string id, ArmorPick pick, List<MeshInstance3D> fresh)
    {
        var p = GetPiece(ArmorKey(id), () => BuildArmorPiece(id, pick.Slot));
        if (p == null) return false;
        ShowPiece(p, fresh);
        return true;
    }

    private void HideRegion(string region)
    {
        string r = (region ?? "").Trim().ToLowerInvariant();
        if (r.StartsWith("body_")) r = r[5..];
        string[] names = r switch
        {
            "head_hair" or "hair" => new[] { "hair" },
            "arms" => new[] { "upper_arms", "forearms" },
            "legs" => new[] { "thighs", "shins" },
            _ => new[] { r },
        };
        foreach (var mi in _body)
        {
            string n = mi.Name.ToString().ToLowerInvariant();
            if (n.StartsWith("body_")) n = n[5..];
            foreach (var want in names)
                if (want.Length > 0 && n.StartsWith(want, StringComparison.Ordinal))
                {
                    mi.Visible = false;
                    _hiddenBy[mi] = _hiddenBy.GetValueOrDefault(mi) + 1;
                }
        }
    }

    private void AttachWeapon(WeaponPick w, bool main, GearSet g, List<MeshInstance3D> fresh, List<string> report)
    {
        if (w == null) return;
        string id = GearCatalog.ResolveWeapon(w);
        if (id == null && !Modular && ShowBaked(w, main, g.Class)) { report.Add($"baked {w.Base} v{w.Variant}"); if (main) SetupIK(w, null, "hand_r", g); return; }
        var p = GetPiece(WeaponKey(w, id, main), () => BuildWeaponPiece(w, id, main));
        if (p == null) return;
        ShowPiece(p, fresh);
        report.Add($"{p.What}@{p.Mount}");
        if (main) SetupIK(w, id, p.Mount, g, p.Grip, p.Model);
    }

    /// <summary>Baked hero, no model file: the class's own weapon mesh of that variant (wpn_N, wpn_N_off) when the base is the one it carries.</summary>
    private bool ShowBaked(WeaponPick w, bool main, ClassId c)
    {
        if (_weapons.Count == 0) return false;
        var (lm, lo) = GearAdapter.Legacy(c);
        if (main ? w.Base != lm : w.Base != lo) return false;
        string suffix = main ? "" : "_off";
        bool any = false;
        for (int v = w.Variant; v >= 1 && !any; v--)
            foreach (var mesh in _weapons)
                if (mesh.Name.ToString() == $"wpn_{v}{suffix}") { mesh.Visible = true; any = true; }
        return any;
    }

    private void SetupIK(WeaponPick w, string id, string mount, GearSet g, Transform3D? grip = null, Node model = null)
    {
        if (!_ikArgRead) { _ikArgRead = true; if (int.TryParse(Game.I?.ArgValue("gear-ik") ?? "", out int m)) GearGripIK.Mode = Math.Clamp(m, 0, 2); }
        if (!Stances.UsesSecondHand(Stance, w.Base) || mount != "hand_r" || Skeleton == null) return;
        int up = Skeleton.FindBone("upperarm_l"), lo = Skeleton.FindBone("lowerarm_l"), hl = Skeleton.FindBone("hand_l"), hr = Skeleton.FindBone("hand_r");
        if (up < 0 || lo < 0 || hl < 0 || hr < 0) return;
        _ik = new GearGripIK
        {
            Name = "GearGripIK", Upper = up, Lower = lo, HandL = hl, HandR = hr,
            WeaponInHand = grip ?? GearGrips.For(w.Base, mount, Skeleton),
            Grip2 = GearCatalog.Grip2For(id, w.Base, model),
            GripL = GearGrips.Hand(false),
            Pelvis = Skeleton.FindBone("pelvis"), Neck = Skeleton.FindBone("neck_01"),
        };
        Skeleton.AddChild(_ik);
        _gearNodes.Add(_ik);
    }

    /// <summary>Hides (or shows again) the gear weapons as well as baked ones (town people, the lair lord).</summary>
    private void ShowGearWeapons(bool show) { foreach (var w in _gearWeapons) if (IsInstanceValid(w)) w.Visible = show; }

    /// <summary>A legendary or unique main-hand weapon keeps a faint edge of its rarity (where the old tier-3 blade glowed).</summary>
    private void ApplyWeaponGlow()
    {
        if (_gearWeapons.Count == 0 || _glowColor == Colors.Black) return;
        float k = 0.55f;
        var c = new Vector3(Mathf.Max(_rim.R * 0.25f, _glowColor.R * k), Mathf.Max(_rim.G * 0.25f, _glowColor.G * k), Mathf.Max(_rim.B * 0.25f, _glowColor.B * k));
        foreach (var mi in _gearWeapons) if (IsInstanceValid(mi)) mi.SetInstanceShaderParameter(PRim, c);
    }
}

// ───────────────────────────────────────────── placeholders ─────────────────────────────────────────────

/// <summary>
/// Stand-ins while the art is being made. Weapons: small primitive models of every base (always available: the last
/// step of the weapon fallback). With --gear-ph also base bodies, armour pieces and jewels, cut from today's heroes
/// by bone region, so the whole assembly can be tested before ART-A / ART-W deliver (never shipped as art).
/// </summary>
public static class GearPlaceholders
{
    private static readonly Dictionary<string, Material> Mats = new();
    private static readonly Dictionary<string, Mesh> WeaponMeshes = new();

    private static Material Mat(string kind)
    {
        if (Mats.TryGetValue(kind, out var m)) return m;
        StandardMaterial3D s = kind switch
        {
            "iron" => new() { AlbedoColor = new Color(0.3f, 0.3f, 0.31f), Metallic = 0.75f, Roughness = 0.6f },
            "steel" => new() { AlbedoColor = new Color(0.42f, 0.43f, 0.45f), Metallic = 0.8f, Roughness = 0.5f },
            "gilt" => new() { AlbedoColor = new Color(0.5f, 0.38f, 0.18f), Metallic = 0.8f, Roughness = 0.5f },
            "wood" => new() { AlbedoColor = new Color(0.26f, 0.17f, 0.1f), Roughness = 0.8f },
            "leather" => new() { AlbedoColor = new Color(0.2f, 0.13f, 0.08f), Roughness = 0.75f },
            "bone" => new() { AlbedoColor = new Color(0.62f, 0.58f, 0.48f), Roughness = 0.7f },
            "cloth" => new() { AlbedoColor = new Color(0.35f, 0.3f, 0.24f), Roughness = 0.9f },
            "linen" => new() { AlbedoColor = new Color(0.58f, 0.53f, 0.44f), Roughness = 0.92f },
            "skin" => new() { AlbedoColor = new Color(0.6f, 0.44f, 0.35f), Roughness = 0.6f },
            "ember" => new() { AlbedoColor = new Color(1f, 0.5f, 0.15f), EmissionEnabled = true, Emission = new Color(1f, 0.45f, 0.12f), EmissionEnergyMultiplier = 1.6f, Roughness = 0.3f },
            "frost" => new() { AlbedoColor = new Color(0.5f, 0.75f, 1f), EmissionEnabled = true, Emission = new Color(0.4f, 0.7f, 1f), EmissionEnergyMultiplier = 1.4f, Roughness = 0.2f },
            _ => new() { AlbedoColor = new Color(0.5f, 0.5f, 0.5f), Roughness = 0.6f },
        };
        s.ResourceName = kind is "ember" or "frost" ? "emis_ph_" + kind : (kind == "skin" ? "skin_ph" : "ph_" + kind);
        Mats[kind] = s;
        return s;
    }

    private sealed class Kit
    {
        private readonly Dictionary<string, SurfaceTool> _st = new();
        public Kit Add(string mat, Mesh prim, float x, float y, float z, float rx = 0, float ry = 0, float rz = 0, float sx = 1, float sy = 1, float sz = 1)
        {
            if (!_st.TryGetValue(mat, out var st)) { st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles); _st[mat] = st; }
            var b = Basis.FromEuler(new Vector3(Mathf.DegToRad(rx), Mathf.DegToRad(ry), Mathf.DegToRad(rz))).Scaled(new Vector3(sx, sy, sz));
            st.AppendFrom(prim, 0, new Transform3D(b, new Vector3(x, y, z)));
            return this;
        }
        public ArrayMesh Build()
        {
            ArrayMesh m = null;
            foreach (var kv in _st) { kv.Value.SetMaterial(Mat(kv.Key)); kv.Value.GenerateNormals(); m = kv.Value.Commit(m); }
            return m;
        }
    }

    private static BoxMesh Box(float x, float y, float z) => new() { Size = new Vector3(x, y, z) };
    private static CylinderMesh Cyl(float r, float h, int seg = 10, float top = -1) => new() { TopRadius = top < 0 ? r : top, BottomRadius = r, Height = h, RadialSegments = seg, Rings = 1 };
    private static SphereMesh Ball(float r, int seg = 12, int rings = 6) => new() { Radius = r, Height = r * 2f, RadialSegments = seg, Rings = rings };
    private static PrismMesh Tip(float w, float h, float d) => new() { Size = new Vector3(w, h, d) };
    private static TorusMesh Hoop(float a, float b) => new() { InnerRadius = a, OuterRadius = b, Rings = 20, RingSegments = 8 };

    /// <summary>A primitive model of a weapon base in contract weapon space (+Z up the weapon, +X the edge, origin at the grip).</summary>
    public static Node3D Weapon(string b, int v)
    {
        if (string.IsNullOrEmpty(b)) return null;
        string key = b + v;
        if (!WeaponMeshes.TryGetValue(key, out var mesh))
        {
            try { mesh = BuildWeapon(b, Math.Clamp(v, 1, 3)); }
            catch (Exception e) { Log.Error("gear", $"primitive weapon '{b}' failed", e); mesh = null; }
            WeaponMeshes[key] = mesh;
        }
        if (mesh == null) return null;
        var root = new Node3D { Name = "prim_" + b };
        root.AddChild(new MeshInstance3D { Name = "mesh", Mesh = mesh, Transform = GearGrips.G2B });      // built Y-up, shown in the contract's Z-up weapon axes
        return root;
    }

    private static ArrayMesh BuildWeapon(string b, int v)
    {
        var k = new Kit();
        string metal = v == 1 ? "iron" : "steel", trim = v >= 2 ? "gilt" : "iron", glow = v == 3 ? "ember" : null;
        void Blade(float len, float w, float y0, float th = 0.008f)
        {
            k.Add(metal, Box(w, len, th), 0, y0 + len / 2f, 0);
            k.Add(metal, Tip(w, w * 1.4f, th), 0, y0 + len + w * 0.7f, 0);
            if (glow != null) k.Add(glow, Box(w * 0.18f, len * 0.8f, th * 1.3f), 0, y0 + len * 0.45f, 0);
        }
        void Haft(float y0, float y1, float r = 0.017f, string m = "wood") => k.Add(m, Cyl(r, y1 - y0, 8), 0, (y0 + y1) / 2f, 0);
        void Grip(float len = 0.13f) { k.Add("leather", Cyl(0.016f, len, 8), 0, 0, 0); k.Add(trim, Ball(0.024f, 8, 4), 0, -len / 2f - 0.015f, 0); }
        switch (b)
        {
            case "sword1": Grip(); k.Add(trim, Box(0.17f, 0.022f, 0.03f), 0, 0.075f, 0); Blade(0.68f, 0.045f, 0.085f); break;
            case "sword2": Grip(0.32f); k.Add(trim, Box(0.28f, 0.03f, 0.035f), 0, 0.17f, 0); Blade(1.0f, 0.06f, 0.185f, 0.01f); break;
            case "dagger": Grip(0.1f); k.Add(trim, Box(0.09f, 0.018f, 0.022f), 0, 0.058f, 0); Blade(0.22f, 0.032f, 0.067f, 0.006f); break;
            case "axe1": Haft(-0.12f, 0.55f); k.Add(metal, Box(0.15f, 0.13f, 0.012f), 0.08f, 0.47f, 0); k.Add(metal, Box(0.035f, 0.17f, 0.014f), 0.16f, 0.47f, 0); break;
            case "axe2":
                Haft(-0.5f, 0.75f, 0.02f); k.Add(metal, Box(0.24f, 0.2f, 0.014f), 0.0f, 0.62f, 0); k.Add(metal, Box(0.04f, 0.28f, 0.016f), 0.13f, 0.62f, 0);
                k.Add(metal, Box(0.04f, 0.28f, 0.016f), -0.13f, 0.62f, 0); if (glow != null) k.Add(glow, Box(0.2f, 0.02f, 0.018f), 0, 0.62f, 0); break;
            case "mace1": Haft(-0.12f, 0.45f, 0.016f, metal == "iron" ? "wood" : "iron"); k.Add(metal, Ball(0.065f), 0, 0.5f, 0);
                for (int i = 0; i < 6; i++) k.Add(trim, Box(0.012f, 0.1f, 0.05f), 0, 0.5f, 0, 0, i * 30f); break;
            case "mace2": Haft(-0.5f, 0.6f, 0.02f); k.Add(metal, Cyl(0.09f, 0.22f, 8), 0, 0.68f, 0); k.Add(trim, Cyl(0.1f, 0.03f, 8), 0, 0.6f, 0); k.Add(trim, Cyl(0.1f, 0.03f, 8), 0, 0.76f, 0);
                if (glow != null) k.Add(glow, Ball(0.04f), 0, 0.82f, 0); break;
            case "wand": Haft(-0.1f, 0.3f, 0.011f); k.Add(trim, Cyl(0.02f, 0.04f, 8), 0, 0.3f, 0); k.Add(glow ?? "frost", Ball(0.025f), 0, 0.34f, 0); break;
            case "sceptre": Haft(-0.1f, 0.38f, 0.014f, trim); k.Add(trim, Hoop(0.035f, 0.05f), 0, 0.45f, 0, 90); k.Add(glow ?? "ember", Ball(0.03f), 0, 0.45f, 0); break;
            case "scythe1": Haft(-0.12f, 0.5f); k.Add(metal, Box(0.3f, 0.04f, 0.008f), 0.15f, 0.5f, 0, 0, 0, -12f); k.Add(metal, Tip(0.04f, 0.08f, 0.008f), 0.32f, 0.45f, 0, 0, 0, 200f); break;
            case "scythe2": Haft(-0.6f, 1.15f, 0.019f); k.Add(metal, Box(0.62f, 0.07f, 0.01f), -0.3f, 1.1f, 0, 0, 0, 14f); k.Add(metal, Tip(0.07f, 0.14f, 0.01f), -0.64f, 1.01f, 0, 0, 0, 150f);
                if (glow != null) k.Add(glow, Box(0.5f, 0.012f, 0.012f), -0.28f, 1.12f, 0, 0, 0, 14f); break;
            case "flail": Haft(-0.1f, 0.32f, 0.016f); for (int i = 0; i < 5; i++) k.Add("iron", Hoop(0.008f, 0.014f), 0.02f * i, 0.36f + 0.03f * i, 0, 0, 0, i % 2 * 90f);
                k.Add(metal, Ball(0.06f), 0.12f, 0.52f, 0); break;
            case "fist": k.Add("leather", Cyl(0.017f, 0.1f, 8), 0, 0, 0); k.Add(metal, Box(0.04f, 0.13f, 0.09f), 0.055f, 0, 0);
                for (int i = 0; i < 3; i++) k.Add(trim, Tip(0.03f, 0.05f, 0.02f), 0.095f, -0.04f + 0.04f * i, 0, 0, 0, -90f); break;
            case "staff": Haft(-0.6f, 1.15f, 0.018f); k.Add(trim, Hoop(0.05f, 0.07f), 0, 1.2f, 0, 90); k.Add(glow ?? "frost", Ball(0.045f), 0, 1.2f, 0); break;
            case "polearm": Haft(-0.7f, 1.2f, 0.018f); k.Add(metal, Box(0.1f, 0.38f, 0.01f), 0.03f, 1.38f, 0); k.Add(metal, Tip(0.1f, 0.12f, 0.01f), 0.03f, 1.63f, 0); break;
            case "bow":
                k.Add("leather", Cyl(0.02f, 0.12f, 8), 0, 0, 0);
                k.Add("wood", Cyl(0.014f, 0.62f, 8, 0.008f), -0.06f, 0.36f, 0, 0, 0, 14f); k.Add("wood", Cyl(0.008f, 0.62f, 8, 0.014f), -0.06f, -0.36f, 0, 0, 0, -14f);
                k.Add("cloth", Cyl(0.0025f, 1.36f, 4), -0.135f, 0, 0); break;
            case "crossbow":
                k.Add("wood", Box(0.5f, 0.05f, 0.05f), 0.16f, 0.07f, 0); k.Add("wood", Box(0.04f, 0.12f, 0.035f), 0, 0, 0);
                k.Add(metal, Box(0.05f, 0.03f, 0.56f), 0.36f, 0.09f, 0, 0, 0, 0); k.Add("cloth", Cyl(0.002f, 0.5f, 4), 0.27f, 0.1f, 0, 90f); break;
            case "shield":
                k.Add(v == 3 ? metal : "wood", Box(0.42f, 0.56f, 0.022f), 0, 0, -0.03f); k.Add(trim, Box(0.45f, 0.028f, 0.03f), 0, 0.28f, -0.03f); k.Add(trim, Box(0.45f, 0.028f, 0.03f), 0, -0.28f, -0.03f);
                k.Add(trim, Box(0.028f, 0.56f, 0.03f), 0.21f, 0, -0.03f); k.Add(trim, Box(0.028f, 0.56f, 0.03f), -0.21f, 0, -0.03f);
                k.Add(trim, Ball(0.05f, 10, 5), 0, 0, -0.05f, 0, 0, 0, 1, 1, 0.6f); k.Add("leather", Box(0.12f, 0.04f, 0.03f), 0, 0, 0); break;
            case "focus": k.Add(trim, Cyl(0.03f, 0.06f, 8, 0.05f), 0, 0.05f, 0); k.Add(glow ?? "frost", Ball(0.06f), 0, 0.13f, 0); break;
            case "totem": Haft(-0.08f, 0.26f, 0.016f); k.Add("bone", Ball(0.06f, 10, 5), 0, 0.33f, 0, 0, 0, 0, 1, 1.2f, 0.9f); k.Add("cloth", Box(0.02f, 0.12f, 0.005f), 0.05f, 0.24f, 0); break;
            case "grimoire": k.Add("leather", Box(0.2f, 0.26f, 0.06f), 0.06f, 0.02f, 0); k.Add(trim, Box(0.21f, 0.02f, 0.065f), 0.06f, 0.14f, 0); k.Add("linen", Box(0.18f, 0.24f, 0.05f), 0.065f, 0.02f, 0);
                if (glow != null) k.Add(glow, Box(0.05f, 0.05f, 0.064f), 0.06f, 0.03f, 0); break;
            case "lantern": k.Add("iron", Hoop(0.03f, 0.04f), 0, 0.03f, 0, 90); k.Add("iron", Box(0.1f, 0.15f, 0.1f), 0, -0.1f, 0); k.Add("ember", Ball(0.035f), 0, -0.1f, 0); break;
            default: Grip(); Blade(0.5f, 0.04f, 0.08f); break;
        }
        return k.Build();
    }

    // ── jewels and gems (--gear-ph) ──

    public static bool CanMake(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (id.StartsWith("ph_wpn_") || id.StartsWith("jwl_") || id.StartsWith("gem_")) return true;
        if (id.StartsWith("arm_"))
        {
            var p = id.Split('_');
            return p.Length == 4 && Array.IndexOf(Names.ClassKeys, p[1]) >= 0 && Array.IndexOf(GearSet.ArmorSlots, p[2]) >= 0;
        }
        return false;
    }

    public static Node3D Make(string id)
    {
        if (!CanMake(id)) return null;
        try
        {
            if (id.StartsWith("ph_wpn_"))
            {
                string rest = id[7..]; int u = rest.LastIndexOf("_v", StringComparison.Ordinal);
                return u > 0 && int.TryParse(rest[(u + 2)..], out int v) ? Weapon(rest[..u], v) : null;
            }
            if (id.StartsWith("arm_")) return Armor(id);
            var k = new Kit();
            int t = id.Length > 0 && char.IsDigit(id[^1]) ? id[^1] - '0' : 2;
            string metal = t >= 3 ? "gilt" : t == 2 ? "steel" : "iron";
            if (id.StartsWith("jwl_ring")) { k.Add(metal, Hoop(0.009f, 0.013f), 0, 0, 0, 90); k.Add(t >= 2 ? "ember" : "frost", Ball(0.006f), 0, 0.013f, 0); }
            else if (id.StartsWith("jwl_amulet")) { k.Add("leather", Hoop(0.05f, 0.054f), 0, 0.05f, 0, 90); k.Add(metal, Cyl(0.022f, 0.008f, 10), 0, 0, 0, 90); k.Add(t >= 2 ? "ember" : "frost", Ball(0.01f), 0, 0, 0.006f); }
            else k.Add(id.Contains("sapph") || id.Contains("frost") ? "frost" : "ember", new SphereMesh { Radius = 0.014f, Height = 0.03f, RadialSegments = 6, Rings = 2 }, 0, 0, 0);
            var root = new Node3D { Name = "ph_" + id };
            root.AddChild(new MeshInstance3D { Name = "mesh", Mesh = k.Build(), Transform = GearGrips.G2B });
            return root;
        }
        catch (Exception e) { Log.Error("gear", $"placeholder '{id}' failed", e); return null; }
    }

    // ── bodies and armour cut from today's heroes (--gear-ph) ──

    /// <summary>Which body region a bone belongs to (the regions of CONTRACT §4 / REGIONS.md).</summary>
    public static string RegionOfBone(string bone)
    {
        string n = (bone ?? "").ToLowerInvariant();
        if (n.StartsWith("head") || n.StartsWith("neck")) return "head";
        if (n.StartsWith("upperarm")) return "upper_arms";
        if (n.StartsWith("lowerarm")) return "forearms";
        if (n.StartsWith("hand") || n.StartsWith("index") || n.StartsWith("middle") || n.StartsWith("pinky") || n.StartsWith("ring") || n.StartsWith("thumb")) return "hands";
        if (n.StartsWith("thigh") || n.StartsWith("pelvis")) return "thighs";
        if (n.StartsWith("calf")) return "shins";
        if (n.StartsWith("foot") || n.StartsWith("ball")) return "feet";
        return "torso";
    }

    private static readonly Dictionary<string, string[]> SlotRegions = new()
    {
        ["helm"] = new[] { "head" }, ["chest"] = new[] { "torso", "upper_arms" }, ["gloves"] = new[] { "forearms", "hands" },
        ["pants"] = new[] { "thighs" }, ["boots"] = new[] { "shins", "feet" },
    };

    private static bool IsSkinMat(Material m) { string n = (m?.ResourceName ?? "").ToLowerInvariant(); return n.StartsWith("skin") || n.StartsWith("hair") || n.StartsWith("emis"); }

    private sealed class Cut { public Dictionary<string, (ArrayMesh mesh, bool skinOnly)> ByRegion = new(); public Skin Skin; }
    private static readonly Dictionary<string, List<Cut>> Cuts = new();       // hero file → per source mesh, region meshes

    /// <summary>The baked hero cut by region, once per file: a list over its meshes of region → (mesh, material).</summary>
    private static List<Cut> CutsOf(string heroId, Node inst, Skeleton3D sk)
    {
        if (Cuts.TryGetValue(heroId, out var list)) return list;
        list = new List<Cut>();
        foreach (var mi in GearCatalog.Walk(inst).OfType<MeshInstance3D>())
        {
            if (mi.Mesh is not ArrayMesh am || mi.Name.ToString().StartsWith("wpn_")) continue;
            var cut = new Cut { Skin = mi.Skin };
            var regionOfBind = new string[mi.Skin?.GetBindCount() ?? sk.GetBoneCount()];
            for (int i = 0; i < regionOfBind.Length; i++)
            {
                string name = mi.Skin != null ? mi.Skin.GetBindName(i).ToString() : sk.GetBoneName(i);
                if (string.IsNullOrEmpty(name) && mi.Skin != null) { int bb = mi.Skin.GetBindBone(i); name = bb >= 0 && bb < sk.GetBoneCount() ? sk.GetBoneName(bb) : ""; }
                regionOfBind[i] = RegionOfBone(name);
            }
            var builders = new Dictionary<string, List<(Godot.Collections.Array arr, Material mat, Mesh.ArrayFormat flags)>>();
            for (int s = 0; s < am.GetSurfaceCount(); s++)
            {
                var arr = am.SurfaceGetArrays(s);
                var mat = mi.GetSurfaceOverrideMaterial(s) ?? am.SurfaceGetMaterial(s);
                var fmt = am.SurfaceGetFormat(s);
                foreach (var (region, sub) in SplitSurface(arr, regionOfBind, (fmt & Mesh.ArrayFormat.FlagUse8BoneWeights) != 0 ? 8 : 4))
                {
                    if (!builders.TryGetValue(region, out var l)) builders[region] = l = new();
                    l.Add((sub, mat, fmt & Mesh.ArrayFormat.FlagUse8BoneWeights));
                }
            }
            foreach (var kv in builders)
            {
                var m = new ArrayMesh();
                bool skinOnly = true;
                foreach (var (arr, mat, flags) in kv.Value)
                {
                    m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arr, null, null, flags);
                    m.SurfaceSetMaterial(m.GetSurfaceCount() - 1, mat);
                    if (!IsSkinMat(mat)) skinOnly = false;
                }
                cut.ByRegion[kv.Key] = (m, skinOnly);
            }
            list.Add(cut);
        }
        Cuts[heroId] = list;
        return list;
    }

    /// <summary>One surface split by the region of each triangle's dominant bone.</summary>
    private static IEnumerable<(string region, Godot.Collections.Array arr)> SplitSurface(Godot.Collections.Array arr, string[] regionOfBind, int per)
    {
        var vtx = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        if (vtx == null || vtx.Length == 0) yield break;
        var bv = arr[(int)Mesh.ArrayType.Bones]; var wv = arr[(int)Mesh.ArrayType.Weights];
        if (bv.VariantType == Variant.Type.Nil || wv.VariantType == Variant.Type.Nil) yield break;
        int[] bones = bv.VariantType == Variant.Type.PackedInt32Array ? bv.AsInt32Array() : Array.ConvertAll(bv.AsFloat32Array(), f => (int)f);
        float[] w = wv.VariantType == Variant.Type.PackedFloat64Array ? Array.ConvertAll(wv.AsFloat64Array(), d => (float)d) : wv.AsFloat32Array();
        per = bones.Length / vtx.Length;
        var idxV = arr[(int)Mesh.ArrayType.Index];
        int[] idx = idxV.VariantType == Variant.Type.Nil ? Enumerable.Range(0, vtx.Length).ToArray() : idxV.AsInt32Array();
        var groups = new Dictionary<string, List<int>>();
        var score = new Dictionary<string, float>();
        for (int t = 0; t + 2 < idx.Length; t += 3)
        {
            score.Clear();
            for (int c = 0; c < 3; c++)
            {
                int v = idx[t + c];
                for (int j = 0; j < per; j++)
                {
                    int b = bones[v * per + j];
                    if (b < 0 || b >= regionOfBind.Length) continue;
                    string r = regionOfBind[b];
                    score[r] = score.GetValueOrDefault(r) + w[v * per + j];
                }
            }
            string best = "torso"; float bs = -1f;
            foreach (var kv in score) if (kv.Value > bs) { bs = kv.Value; best = kv.Key; }
            if (!groups.TryGetValue(best, out var g)) groups[best] = g = new List<int>();
            g.Add(idx[t]); g.Add(idx[t + 1]); g.Add(idx[t + 2]);
        }
        foreach (var kv in groups)
        {
            var map = new Dictionary<int, int>();
            var order = new List<int>();
            var ni = new int[kv.Value.Count];
            for (int i = 0; i < ni.Length; i++)
            {
                int o = kv.Value[i];
                if (!map.TryGetValue(o, out int n)) { n = order.Count; map[o] = n; order.Add(o); }
                ni[i] = n;
            }
            var outArr = new Godot.Collections.Array();
            outArr.Resize((int)Mesh.ArrayType.Max);
            for (int a = 0; a < (int)Mesh.ArrayType.Max; a++)
            {
                if (a == (int)Mesh.ArrayType.Index) { outArr[a] = ni; continue; }
                var src = arr[a];
                outArr[a] = src.VariantType switch
                {
                    Variant.Type.PackedVector3Array => Pick(src.AsVector3Array(), order, 1),
                    Variant.Type.PackedVector2Array => Pick(src.AsVector2Array(), order, 1),
                    Variant.Type.PackedFloat32Array => Pick(src.AsFloat32Array(), order, src.AsFloat32Array().Length / vtx.Length),
                    Variant.Type.PackedFloat64Array => Pick(src.AsFloat64Array(), order, src.AsFloat64Array().Length / vtx.Length),
                    Variant.Type.PackedInt32Array => Pick(src.AsInt32Array(), order, src.AsInt32Array().Length / vtx.Length),
                    Variant.Type.PackedColorArray => Pick(src.AsColorArray(), order, 1),
                    _ => default,
                };
            }
            yield return (kv.Key, outArr);
        }
    }

    private static T[] Pick<T>(T[] src, List<int> order, int per)
    {
        if (src == null || per <= 0) return src;
        var o = new T[order.Count * per];
        for (int i = 0; i < order.Count; i++) Array.Copy(src, order[i] * per, o, i * per, per);
        return o;
    }

    /// <summary>
    /// --gear-ph base body: today's hero cut into body_&lt;region&gt; meshes; whatever is not skin or hair is shown in
    /// plain linen (a stand-in for the barely-clad body ART-A makes).
    /// </summary>
    public static void SplitBody(Node3D inst, Skeleton3D sk, string heroId)
    {
        if (inst == null || sk == null) return;
        var cuts = CutsOf(heroId, inst, sk);
        foreach (var mi in GearCatalog.Walk(inst).OfType<MeshInstance3D>().ToList())
        {
            if (mi.Name.ToString().StartsWith("wpn_")) continue;
            mi.GetParent().RemoveChild(mi); mi.Free();
        }
        int k = 0;
        // a head that has a face of its own loses its helmet; a fully helmed one keeps the shape as bare skin
        bool face = cuts.Any(c => c.ByRegion.TryGetValue("head", out var h) && Enumerable.Range(0, h.mesh.GetSurfaceCount()).Any(s => IsSkinMat(h.mesh.SurfaceGetMaterial(s))));
        foreach (var cut in cuts)
            foreach (var kv in cut.ByRegion)
            {
                var mi = new MeshInstance3D { Name = $"body_{kv.Key}_{k++}", Mesh = kv.Value.mesh, Skin = cut.Skin };
                sk.AddChild(mi);
                mi.Skeleton = new NodePath("..");
                for (int s = 0; s < kv.Value.mesh.GetSurfaceCount(); s++)
                {
                    var m = kv.Value.mesh.SurfaceGetMaterial(s);
                    if (IsSkinMat(m)) continue;
                    mi.SetSurfaceOverrideMaterial(s, kv.Key == "head" && face ? Hidden() : Mat(kv.Key is "thighs" or "torso" ? "linen" : "skin"));
                }
            }
    }

    /// <summary>--gear-ph armour piece: the hero's own non-skin parts of the slot's regions, tinted by tier (t4 = as made).</summary>
    private static Node3D Armor(string id)
    {
        var p = id.Split('_');
        string cls = p[1], slot = p[2];
        int tier = p[3].Length > 1 && char.IsDigit(p[3][1]) ? p[3][1] - '0' : 4;
        string heroId = "hero_" + cls;
        string path = $"res://assets/chars/{heroId}.glb";
        if (!ResourceLoader.Exists(path)) return null;
        if (!Cuts.ContainsKey(heroId))
        {
            var tmp = GD.Load<PackedScene>(path).Instantiate<Node3D>();
            var sk = GearCatalog.Walk(tmp).OfType<Skeleton3D>().FirstOrDefault();
            if (sk != null) CutsOf(heroId, tmp, sk);
            tmp.Free();
        }
        if (!Cuts.TryGetValue(heroId, out var cuts)) return null;
        var root = new Node3D { Name = "ph_" + id };
        int n = 0;
        foreach (var cut in cuts)
            foreach (var region in SlotRegions[slot])
            {
                if (!cut.ByRegion.TryGetValue(region, out var part) || part.skinOnly) continue;
                var mi = new MeshInstance3D { Name = $"piece{n++}", Mesh = part.mesh, Skin = cut.Skin };
                root.AddChild(mi);
                for (int s = 0; s < part.mesh.GetSurfaceCount(); s++)
                {
                    var m = part.mesh.SurfaceGetMaterial(s);
                    if (IsSkinMat(m)) { mi.SetSurfaceOverrideMaterial(s, Hidden()); continue; }
                    if (tier < 4) mi.SetSurfaceOverrideMaterial(s, Tinted(m, tier));
                }
            }
        if (n == 0) { root.Free(); return null; }
        root.SetMeta("hide_body_regions", string.Join(",", slot switch
        {
            "helm" => new[] { "hair" }, "chest" => new[] { "torso", "upper_arms" }, "gloves" => new[] { "hands", "forearms" },
            "pants" => new[] { "thighs" }, _ => new[] { "feet", "shins" },
        }));
        return root;
    }

    private static Material _hidden;
    private static Material Hidden() => _hidden ??= new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlbedoColor = new Color(0, 0, 0, 0), ResourceName = "ph_hidden" };

    private static readonly Dictionary<(ulong, int), Material> TintCache = new();
    private static Material Tinted(Material m, int tier)
    {
        if (m is not StandardMaterial3D s) return m;
        if (TintCache.TryGetValue((s.GetInstanceId(), tier), out var c)) return c;
        var d = (StandardMaterial3D)s.Duplicate();
        d.AlbedoColor = s.AlbedoColor * (tier switch { 1 => new Color(0.62f, 0.5f, 0.38f), 2 => new Color(0.72f, 0.74f, 0.78f), _ => new Color(1.05f, 0.92f, 0.7f) });
        if (tier == 1) d.Roughness = Mathf.Min(1f, s.Roughness + 0.25f);
        TintCache[(s.GetInstanceId(), tier)] = d;
        return d;
    }
}
