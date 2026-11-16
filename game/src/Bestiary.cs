using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AshenAbyss;

/// <summary>One regular type of data/bestiary.json, as written there (BESTIARY.md section 2).</summary>
public sealed class BestiaryType
{
    public string Id, NameZh, NameEn, Family, Role, Ai, AiKind, Rig, RigNote, BodyPlan, Build, VariantOf, Model, Element, Status, Anims, Silhouette;
    public string[] AiTraits = Array.Empty<string>(), Regions = Array.Empty<string>();
    public float Hp = 1f, Dmg = 1f, Speed = 4f, Scale = 1f, SpawnWeight = 1f;
    public int Stage;
}

/// <summary>A regional reskin (BESTIARY.md 3.9): the base type's model and behaviour with another element, status and look.</summary>
public sealed class BestiaryReskin { public string Id, Base, Region, NameZh, Element, Status, State, Look; }

public sealed class BestiaryFamily
{
    public string Id, NameZh, NameEn, Concept;
    public string[] Regions = Array.Empty<string>(), SignatureIn = Array.Empty<string>(), Types = Array.Empty<string>();
}

public sealed class BestiaryRegion
{
    public string Id; public int Act, Level;
    public readonly List<(string id, float w, bool signature, string[] types)> Families = new();
    public readonly List<(string id, string baseId, string family, float w)> Spawns = new();
    public readonly List<(string id, string[] families, string boss)> Dungeons = new();
}

/// <summary>
/// The designed roster (data/bestiary.json: 129 regular types, 28 regional reskins, 25 families, 7 regions) made live.
/// Loaded once, it turns every type and reskin into a <see cref="MonsterDef"/> inside <see cref="MonsterDb"/> (definitions
/// written by hand keep their tuned numbers and only gain family, traits, regions and weights), fills the region spawn
/// tables and dungeon family lists of <see cref="ZoneDb"/>, and says which character model each type really gets:
/// its own file, or a stand-in chosen from the data while that file has not been produced.
/// For other modules: <see cref="Type"/> (definition of any type or reskin id), <see cref="Info"/> (the raw record),
/// <see cref="Spawn"/> (one monster of a type at a position), <see cref="Named"/> (the "named" section, unparsed).
/// </summary>
public static class Bestiary
{
    public const string Path = "res://data/bestiary.json";
    public static readonly List<BestiaryType> Types = new();
    public static readonly List<BestiaryReskin> Reskins = new();
    public static readonly List<BestiaryFamily> Families = new();
    public static readonly List<BestiaryRegion> Regions = new();
    private static readonly Dictionary<string, BestiaryType> TypeById = new(), TypeByModel = new();
    private static readonly Dictionary<string, BestiaryReskin> ReskinById = new();
    private static readonly Dictionary<(string, string), string> ReskinOf = new();      // (base id, region) → reskin id
    private static readonly Dictionary<string, BestiaryFamily> FamilyById = new();
    private static readonly Dictionary<string, BestiaryRegion> RegionById = new();
    private static readonly List<string> Problems = new();
    private static JsonDocument _doc;
    private static bool _tried, _booted;
    private static int _merged, _added, _reskinsAdded, _reskinsMerged;

    /// <summary>
    /// PARKED (2026-11-15, see Y:\ROSTER_NOTES.md): while this is false the bestiary changes nothing in the game — no new
    /// definitions in MonsterDb, the built-in region spawn lists stay, CharModel resolves models as before. The data can
    /// still be read (Info, Named, Family, Region). Set to true only together with the new AI (MonsterAi*.cs) and the
    /// family-based Populate, which are not written yet.
    /// </summary>
    public static readonly bool Live = false;

    public static bool Loaded { get; private set; }
    /// <summary>The whole document (ValueKind Undefined when the file could not be read).</summary>
    public static JsonElement Root { get; private set; }
    /// <summary>The "named" section: rare_elites, stronghold_bosses, dungeon_bosses, region_bosses, world_bosses, pinnacle_bosses.</summary>
    public static JsonElement Named { get; private set; }

    // ───────────────────────── lookups (public API) ─────────────────────────

    /// <summary>The monster definition of a regular type or a regional reskin (also every other id MonsterDb knows). Null when unknown.</summary>
    public static MonsterDef Type(string id) => MonsterDb.Get(id);
    /// <summary>The bestiary record of a regular type; for a reskin id, the record of its base type. Null when unknown.</summary>
    public static BestiaryType Info(string id)
    {
        EnsureLoaded();
        if (id == null) return null;
        if (TypeById.TryGetValue(id, out var t)) return t;
        return ReskinById.TryGetValue(id, out var r) && TypeById.TryGetValue(r.Base, out t) ? t : null;
    }
    public static BestiaryReskin ReskinInfo(string id) { EnsureLoaded(); return id != null && ReskinById.TryGetValue(id, out var r) ? r : null; }
    public static BestiaryFamily Family(string id) { EnsureLoaded(); return id != null && FamilyById.TryGetValue(id, out var f) ? f : null; }
    public static BestiaryRegion Region(string id) { EnsureLoaded(); return id != null && RegionById.TryGetValue(id, out var r) ? r : null; }
    public static bool IsType(string id) { EnsureLoaded(); return id != null && TypeById.ContainsKey(id); }
    public static bool IsReskin(string id) { EnsureLoaded(); return id != null && ReskinById.ContainsKey(id); }

    /// <summary>The id that stands for a type in a region: its regional reskin when one exists, otherwise the id itself.</summary>
    public static string Regional(string typeId, string regionId)
    {
        EnsureLoaded();
        if (typeId == null || regionId == null) return typeId;
        return ReskinOf.TryGetValue((typeId, regionId), out var r) && MonsterDb.Get(r) != null ? r : typeId;
    }

    /// <summary>The definition a summoned or called type takes in the level being played (reskinned where the region has one).</summary>
    public static MonsterDef Local(string typeId)
    {
        var lv = Level.Current;
        string region = lv?.Region?.Id ?? lv?.SpawnRegion?.Id;
        return MonsterDb.Get(Regional(typeId, region)) ?? MonsterDb.Get(typeId);
    }

    /// <summary>
    /// Spawns one monster of a type or reskin id at a position, at the given level and rank (affixes optional; miniBoss
    /// makes a named leader). The level being built or played must exist (World.Root). Returns null for an unknown id.
    /// </summary>
    public static Monster Spawn(string typeId, Vector2 pos, int level, MonTier tier = MonTier.Normal, IEnumerable<string> affixes = null, bool miniBoss = false, int pack = -1)
    {
        var def = MonsterDb.Get(typeId);
        if (def == null) { Log.Warn("bestiary", $"spawn of unknown type '{typeId}'"); return null; }
        if (World.Root == null) { Log.Warn("bestiary", $"spawn of '{typeId}' without a level"); return null; }
        var m = Monster.Spawn(def, Math.Max(1, level), tier, World.Grid?.NearestWalk(pos) ?? pos, Faction.Monster, affixes, miniBoss);
        if (pack >= 0) m.Pack = pack;
        return m;
    }

    // ───────────────────────── loading ─────────────────────────

    public static bool EnsureLoaded()
    {
        if (_tried) return Loaded;
        _tried = true;
        try
        {
            if (!FileAccess.FileExists(Path)) { Log.Error("bestiary", $"{Path} is missing: only the built-in monster definitions are available"); return false; }
            string text = FileAccess.GetFileAsString(Path);
            _doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            Root = _doc.RootElement;
            if (Root.TryGetProperty("named", out var named)) Named = named;
            foreach (var e in Arr(Root, "families"))
            {
                var f = new BestiaryFamily
                {
                    Id = S(e, "id"), NameZh = S(e, "name_zh"), NameEn = S(e, "name_en"), Concept = S(e, "concept"),
                    Regions = SA(e, "regions"), SignatureIn = SA(e, "signature_in"), Types = SA(e, "types"),
                };
                if (string.IsNullOrEmpty(f.Id)) { Problems.Add("a family without an id"); continue; }
                Families.Add(f); FamilyById[f.Id] = f;
            }
            foreach (var e in Arr(Root, "types"))
            {
                var t = new BestiaryType
                {
                    Id = S(e, "id"), NameZh = S(e, "name_zh"), NameEn = S(e, "name_en"), Family = S(e, "family"), Role = S(e, "role") ?? "melee",
                    Ai = S(e, "ai") ?? "Melee", AiKind = S(e, "ai_kind") ?? "Melee", AiTraits = SA(e, "ai_traits"), Rig = S(e, "rig") ?? "A",
                    RigNote = S(e, "rig_note") ?? "", BodyPlan = S(e, "body_plan") ?? "humanoid biped", Build = S(e, "build") ?? "", VariantOf = S(e, "variant_of"),
                    Model = S(e, "model"), Element = S(e, "element") ?? "physical", Status = S(e, "status") ?? "-", Anims = S(e, "anims") ?? "",
                    Silhouette = S(e, "silhouette") ?? "", Regions = SA(e, "regions"),
                    Hp = F(e, "hp", 1f), Dmg = F(e, "dmg", 1f), Speed = F(e, "speed", 4f), Scale = F(e, "scale", 1f), SpawnWeight = F(e, "spawn_weight", 1f), Stage = (int)F(e, "stage", 0f),
                };
                if (string.IsNullOrEmpty(t.Id)) { Problems.Add("a type without an id"); continue; }
                if (TypeById.ContainsKey(t.Id)) { Problems.Add($"type '{t.Id}' is listed twice"); continue; }
                if (string.IsNullOrEmpty(t.NameZh)) Problems.Add($"type '{t.Id}' has no name_zh");
                if (string.IsNullOrEmpty(t.Model)) { Problems.Add($"type '{t.Id}' names no model"); t.Model = "mon_" + t.Id; }
                if (string.IsNullOrEmpty(t.Family) || !FamilyById.ContainsKey(t.Family)) Problems.Add($"type '{t.Id}' belongs to unknown family '{t.Family}'");
                if (t.Hp <= 0f || t.Dmg <= 0f || t.Scale <= 0f || t.Speed < 0f) Problems.Add($"type '{t.Id}' has a non-positive hp, dmg or scale");
                if (t.SpawnWeight <= 0f) Problems.Add($"type '{t.Id}' has spawn_weight {t.SpawnWeight}");
                Types.Add(t); TypeById[t.Id] = t;
                if (!TypeByModel.ContainsKey(t.Model)) TypeByModel[t.Model] = t;
            }
            foreach (var e in Arr(Root, "reskins"))
            {
                var r = new BestiaryReskin
                {
                    Id = S(e, "id"), Base = S(e, "base"), Region = S(e, "region"), NameZh = S(e, "name_zh"), Element = S(e, "element") ?? "physical",
                    Status = S(e, "status") ?? "-", State = S(e, "state") ?? "", Look = S(e, "look") ?? "",
                };
                if (string.IsNullOrEmpty(r.Id)) { Problems.Add("a reskin without an id"); continue; }
                if (r.Base == null || !TypeById.ContainsKey(r.Base)) { Problems.Add($"reskin '{r.Id}' has unknown base '{r.Base}'"); continue; }
                if (TypeById.ContainsKey(r.Id) || ReskinById.ContainsKey(r.Id)) { Problems.Add($"reskin id '{r.Id}' is already taken"); continue; }
                Reskins.Add(r); ReskinById[r.Id] = r;
                if (!string.IsNullOrEmpty(r.Region)) ReskinOf[(r.Base, r.Region)] = r.Id;
            }
            foreach (var e in Arr(Root, "regions"))
            {
                var r = new BestiaryRegion { Id = S(e, "id"), Act = (int)F(e, "act", 1f), Level = (int)F(e, "level", 1f) };
                if (string.IsNullOrEmpty(r.Id)) { Problems.Add("a region without an id"); continue; }
                foreach (var f in Arr(e, "families"))
                {
                    string fid = S(f, "id"); float w = F(f, "weight", 0f);
                    if (fid == null || !FamilyById.ContainsKey(fid)) { Problems.Add($"region '{r.Id}' lists unknown family '{fid}'"); continue; }
                    if (w <= 0f) { Problems.Add($"region '{r.Id}': family '{fid}' has weight {w}"); continue; }
                    var types = SA(f, "types").Where(id =>
                    {
                        bool ok = TypeById.ContainsKey(id) || ReskinById.ContainsKey(id);
                        if (!ok) Problems.Add($"region '{r.Id}', family '{fid}': unknown type '{id}'");
                        return ok;
                    }).ToArray();
                    r.Families.Add((fid, w, B(f, "signature"), types));
                }
                foreach (var s in Arr(e, "spawns"))
                {
                    string id = S(s, "id"); float w = F(s, "w", 0f);
                    if (id == null || !(TypeById.ContainsKey(id) || ReskinById.ContainsKey(id))) { Problems.Add($"region '{r.Id}' spawn list: unknown type '{id}'"); continue; }
                    if (w <= 0f) { Problems.Add($"region '{r.Id}' spawn list: '{id}' has weight {w}"); continue; }
                    r.Spawns.Add((id, S(s, "base") ?? id, S(s, "family") ?? "", w));
                }
                foreach (var d in Arr(e, "dungeons"))
                {
                    string did = S(d, "id");
                    var fams = SA(d, "families").Where(f =>
                    {
                        bool ok = FamilyById.ContainsKey(f);
                        if (!ok) Problems.Add($"dungeon '{did}' lists unknown family '{f}'");
                        return ok;
                    }).ToArray();
                    if (did != null) r.Dungeons.Add((did, fams, S(d, "boss")));
                }
                Regions.Add(r); RegionById[r.Id] = r;
            }
            Loaded = Types.Count > 0;
            if (!Loaded) Log.Error("bestiary", $"{Path} holds no types");
        }
        catch (Exception e)
        {
            Loaded = false;
            Log.Error("bestiary", $"{Path} could not be read; only the built-in monster definitions are available", e);
        }
        return Loaded;
    }

    private static IEnumerable<JsonElement> Arr(JsonElement e, string key)
    {
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var a) && a.ValueKind == JsonValueKind.Array)
            foreach (var x in a.EnumerateArray()) yield return x;
    }
    private static string S(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static float F(JsonElement e, string key, float def) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : def;
    private static bool B(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;
    private static string[] SA(JsonElement e, string key) => Arr(e, key).Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()).ToArray();

    // ───────────────────────── definitions ─────────────────────────

    /// <summary>Called by MonsterDb while it builds its list: adds every bestiary type and reskin that is not written by hand, and enriches those that are.</summary>
    public static void Merge(List<MonsterDef> list)
    {
        // every built-in definition gets its place in a pack, bestiary or not (minions and bosses included: harmless)
        if (!Live || !EnsureLoaded()) { foreach (var d in list) d.Slot = SlotOf(d); return; }
        var by = new Dictionary<string, MonsterDef>();
        foreach (var d in list) by[d.Id] = d;
        foreach (var t in Types)
        {
            bool builtIn = by.TryGetValue(t.Id, out var d);
            if (builtIn) { Enrich(d, t); _merged++; }
            else { d = Build(t); list.Add(d); by[d.Id] = d; _added++; }
            Log.Guard("bestiary", () => MonsterAiTune.Apply(d, t, builtIn));
            d.Slot = SlotOf(d);
        }
        foreach (var r in Reskins)
        {
            var baseDef = by[r.Base]; var t = TypeById[r.Base];
            if (by.TryGetValue(r.Id, out var d))
            {
                // written by hand (cloned from its base before the bestiary was merged): tuned numbers stay, the rest follows the base
                d.Traits = baseDef.Traits; d.Ai = baseDef.Ai.Clone(); d.Model = baseDef.Model;
                CopyRecord(d, baseDef);
                if (!string.IsNullOrEmpty(r.NameZh) && d.Name != r.NameZh) Log.Debug("bestiary", $"reskin {r.Id}: built-in name '{d.Name}' kept (bestiary says '{r.NameZh}')");
                _reskinsMerged++;
            }
            else
            {
                d = baseDef.Variant(r.Id, string.IsNullOrEmpty(r.NameZh) ? baseDef.Name : r.NameZh, TintOf(r.Region, r.Element), m =>
                {
                    m.Element = ElementOf(r.Element, m.Element);
                    var sts = ParseStatuses(r.Status);
                    m.OnHit = sts.Count > 0 ? sts[0] : null;
                    m.OnHit2 = sts.Count > 1 ? sts[1] : null;
                });
                list.Add(d); by[d.Id] = d; _reskinsAdded++;
            }
            d.BaseId = r.Base; d.RegionId = r.Region; d.Look = r.Look; d.InBestiary = true;
            d.Regions = string.IsNullOrEmpty(r.Region) ? Array.Empty<string>() : new[] { r.Region };
            Log.Guard("bestiary", () => MonsterAiTune.ApplyReskin(d, baseDef, r));
            d.Slot = SlotOf(d);
        }
        foreach (var d in list) if (!d.InBestiary) d.Slot = SlotOf(d);
        foreach (var extra in MonsterAiTune.HelperDefs(by)) { list.Add(extra); by[extra.Id] = extra; }
    }

    private static void CopyRecord(MonsterDef d, MonsterDef from)
    {
        d.NameEn = from.NameEn; d.Rig = from.Rig; d.BodyPlan = from.BodyPlan; d.RoleText = from.RoleText; d.Family = from.Family;
        d.SpawnWeight = from.SpawnWeight; d.Stage = from.Stage;
    }

    private static void Enrich(MonsterDef d, BestiaryType t)
    {
        d.NameEn = t.NameEn ?? ""; d.Rig = t.Rig; d.BodyPlan = t.BodyPlan; d.RoleText = t.Role; d.Regions = t.Regions; d.SpawnWeight = t.SpawnWeight; d.Stage = t.Stage;
        d.Look = t.Silhouette; d.InBestiary = true;
        if (d.Family != t.Family) { Log.Warn("bestiary", $"type {t.Id}: built-in family '{d.Family}' replaced by the bestiary's '{t.Family}'"); d.Family = t.Family; }
        if (d.Model != t.Model) { Log.Info("bestiary", $"type {t.Id}: model '{d.Model}' → '{t.Model}' (bestiary)"); d.Model = t.Model; }
        d.Traits |= TraitsOf(t);
        if (KindOf(t.AiKind, out var kind) && kind != d.AI) Log.Warn("bestiary", $"type {t.Id}: built-in AI {d.AI} kept (bestiary says {t.AiKind})");
    }

    private static readonly HashSet<string> MeleeClips = new()
    {
        "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C", "Sword_Regular_Combo", "Sword_Attack", "Sword_Heavy_Combo", "Melee_Hook", "Punch_Cross", "Punch_Jab",
        "Zombie_Scratch", "Shield_OneShot",
    };
    private static readonly HashSet<string> RangedClips = new() { "OverhandThrow", "Spell_Simple_Shoot", "Pistol_Shoot", "Interact" };

    /// <summary>A definition for a type the code does not write by hand, from the record and a template for its AI kind.</summary>
    private static MonsterDef Build(BestiaryType t)
    {
        if (!KindOf(t.AiKind, out var kind)) { Problems.Add($"type '{t.Id}' has unknown AI kind '{t.AiKind}' (treated as Melee)"); kind = MonAI.Melee; }
        string role = RoleClass(t.Role);
        bool giant = t.Scale >= 1.75f, heavy = role is "bruiser" or "giant" or "mother" || kind == MonAI.Brute;
        bool swarmer = role == "swarmer";
        var d = new MonsterDef
        {
            Id = t.Id, Name = string.IsNullOrEmpty(t.NameZh) ? t.Id : t.NameZh, NameEn = t.NameEn ?? "", Model = t.Model, Family = t.Family, Sfx = SfxOf(t.Family, heavy),
            Role = role == "giant" || role == "mother" ? "bruiser" : role, RoleText = t.Role, AI = kind, Scale = t.Scale, Hp = t.Hp, Dmg = t.Dmg, Speed = t.Speed,
            Element = ElementOf(t.Element, Element.Physical), Rig = t.Rig, BodyPlan = t.BodyPlan, Regions = t.Regions, SpawnWeight = t.SpawnWeight, Stage = t.Stage,
            Look = t.Silhouette, InBestiary = true, Traits = TraitsOf(t),
            Radius = swarmer ? 0.42f : giant ? 0.62f : heavy ? 0.55f : 0.5f,
            Poise = swarmer ? 0.6f : giant ? 8f : heavy ? 5f : 1f,
            Xp = MathF.Round(Math.Clamp(0.55f + 0.65f * t.Hp, 0.6f, 4.2f) + (role is "caster" or "support" ? 0.2f : 0f), 1),
            Spectral = t.BodyPlan.StartsWith("floating"),
        };
        // timing and reach by kind (the hand-written definitions of the same kinds are the pattern)
        switch (kind)
        {
            case MonAI.Ranged: d.Range = 12f; d.Contact = 0.5f; d.Cooldown = 2.4f; d.ProjSpeed = 17f; d.AnimSpeed = 1.1f; break;
            case MonAI.Caster: case MonAI.Teleporter: d.Range = 12f; d.Contact = 0.4f; d.Cooldown = 2.5f; d.ProjSpeed = 12f; break;
            case MonAI.Support: d.Range = 11f; d.Contact = 0.4f; d.Cooldown = 2.8f; d.ProjSpeed = 11f; break;
            case MonAI.Bomber: d.Range = 10f; d.Contact = 0.5f; d.Cooldown = 3.5f; break;
            case MonAI.Healer: case MonAI.Reviver: d.Range = 2.2f; d.Contact = 0.36f; d.Cooldown = 1.9f; break;
            case MonAI.Spawner: d.Range = 2.6f + (t.Scale - 1f); d.Contact = 0.6f; d.Cooldown = 2.4f; d.AnimSpeed = 0.9f; break;
            case MonAI.Caller: d.Range = d.Is(MonTrait.Eruption) ? 11f : 2.2f; d.Contact = 0.4f; d.Cooldown = d.Is(MonTrait.Eruption) ? 2.6f : 1.8f; break;
            case MonAI.Brute: d.Range = 2.6f + (t.Scale - 1f) * 1.3f; d.Contact = 0.65f; d.Cooldown = 2.5f; d.AnimSpeed = 0.85f; break;
            case MonAI.Exploder: d.Range = 2.2f; d.Contact = 0.6f; d.Cooldown = 2.2f; break;
            case MonAI.Turret: d.Range = 16f; d.Contact = 1.2f; d.Cooldown = 4f; d.Poise = 99f; break;
            case MonAI.Swarm: d.Range = 0.6f; d.Contact = 0.3f; d.Cooldown = 0.5f; d.Radius = 0.9f; d.Poise = 99f; break;
            case MonAI.Flyer: d.Range = 2.2f; d.Contact = 0.3f; d.Cooldown = 4.5f; d.AnimSpeed = 1.2f; break;
            case MonAI.Shieldbearer: d.Range = 2.3f; d.Contact = 0.3f; d.Cooldown = 1.8f; d.Poise = 3f; break;
            case MonAI.Charger: d.Range = 2.3f + (t.Scale - 1f); d.Contact = 0.4f; d.Cooldown = 1.8f; d.Poise = MathF.Max(d.Poise, 2.5f); break;
            case MonAI.Stalker: case MonAI.Leaper: d.Range = 2.1f; d.Contact = 0.32f; d.Cooldown = 1.3f; d.AnimSpeed = 1.3f; break;
            case MonAI.Burrower: d.Range = 2.3f + (t.Scale - 1f); d.Contact = 0.4f; d.Cooldown = 1.5f; break;
            default:
                d.Range = swarmer ? 1.9f : 2.3f + MathF.Max(0f, t.Scale - 1f); d.Contact = swarmer ? 0.34f : 0.38f;
                d.Cooldown = swarmer ? 1.15f : 1.6f; d.AnimSpeed = swarmer ? 1.4f : 1.05f;
                break;
        }
        if (d.Is(MonTrait.Breath) && kind is MonAI.Caster or MonAI.Ranged) d.Range = d.Ai.BreathLen * 0.7f;
        SetClips(d, t, kind);
        return d;
    }

    private static readonly Regex Word = new(@"[A-Za-z][A-Za-z0-9_]*", RegexOptions.Compiled);

    private static void SetClips(MonsterDef d, BestiaryType t, MonAI kind)
    {
        bool rangedBasic = kind is MonAI.Ranged or MonAI.Caster or MonAI.Teleporter or MonAI.Support or MonAI.Bomber || (kind == MonAI.Caller && d.Is(MonTrait.Eruption));
        if (t.Rig != "A")
        {
            // creatures on their own rigs: the standard clip set (CharModel maps the names onto a humanoid stand-in too)
            bool both = kind is MonAI.Melee or MonAI.Leaper or MonAI.Brute or MonAI.Burrower or MonAI.Stalker && !d.Is(MonTrait.Hook | MonTrait.Pulse | MonTrait.Breath | MonTrait.Grab);
            d.AttackAnims = both ? new[] { "Attack1", "Attack2" } : new[] { "Attack1" };
            d.Ai.CastAnim = "Attack2"; d.Ai.MeleeAnim = rangedBasic ? "Attack2" : "Attack1";
            return;
        }
        var melee = new List<string>(); var ranged = new List<string>();
        string[] parts = (t.Anims ?? "").Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            string seg = parts[i], low = seg.ToLowerInvariant();
            foreach (Match m in Word.Matches(seg))
            {
                string w = m.Value;
                if (!CharModel.HasClip(w)) continue;
                string before = seg[..m.Index].TrimEnd().ToLowerInvariant();
                if (before.EndsWith("idle")) { d.Idle = w; continue; }
                if (before.EndsWith("run")) { d.Run = w; continue; }
                if (before.EndsWith("walk")) { d.Walk = w; continue; }
                if (before.EndsWith("guard pose") || before.EndsWith("rises with") || before.EndsWith("with")) continue;
                if (MeleeClips.Contains(w)) { if (!melee.Contains(w)) melee.Add(w); }
                else if (RangedClips.Contains(w)) { if (!ranged.Contains(w)) ranged.Add(w); }
            }
            if (low.Contains("zombie idle and walk")) { d.Idle = "Zombie_Idle_Loop"; d.Walk = "Zombie_Walk_Fwd_Loop"; d.Run = "Zombie_Walk_Fwd_Loop"; }
        }
        if (!CharModel.IsLocomotion(d.Walk)) d.Walk = "Walk_Loop";
        if (!CharModel.IsLocomotion(d.Run)) d.Run = "Jog_Fwd_Loop";
        if ((t.Anims ?? "").Contains("rises with LayToIdle") && !d.Is(MonTrait.Dormant)) d.RiseFromGround = true;
        string defMelee = d.Idle.StartsWith("Zombie") ? "Zombie_Scratch" : kind == MonAI.Brute ? "Sword_Attack" : "Sword_Regular_A";
        string defRanged = kind is MonAI.Ranged or MonAI.Bomber ? "OverhandThrow" : "Spell_Simple_Shoot";
        // "Interact" is a quiet two-handed gesture: fine for casts and mends, not as a thrown or shot attack
        var shots = ranged.Where(c => c != "Interact").ToList();
        if (rangedBasic) d.AttackAnims = shots.Count > 0 ? shots.ToArray() : new[] { defRanged };
        else d.AttackAnims = melee.Count > 0 ? melee.ToArray() : new[] { defMelee };
        d.Ai.MeleeAnim = melee.Count > 0 ? melee[0] : null;
        d.Ai.CastAnim = ranged.Count > 0 ? (ranged.Contains("Spell_Simple_Shoot") ? "Spell_Simple_Shoot" : ranged[0]) : "Spell_Simple_Shoot";
    }

    public static bool KindOf(string name, out MonAI kind) => Enum.TryParse(name ?? "", false, out kind) && kind != MonAI.Boss;

    private static MonTrait TraitsOf(BestiaryType t)
    {
        MonTrait res = MonTrait.None;
        foreach (var name in t.AiTraits)
        {
            if (Enum.TryParse(name, false, out MonTrait tr)) res |= tr;
            else Problems.Add($"type '{t.Id}' has unknown trait '{name}'");
        }
        return res;
    }

    /// <summary>The mode written after Dormant in the "ai" text: buried, statue or ceiling.</summary>
    public static string DormantModeOf(BestiaryType t)
    {
        var m = Regex.Match(t?.Ai ?? "", @"Dormant\((\w+)\)");
        return m.Success ? m.Groups[1].Value.ToLowerInvariant() : "buried";
    }

    /// <summary>The first word of the role text, folded to the classes packs are built from.</summary>
    public static string RoleClass(string role)
    {
        string r = (role ?? "melee").ToLowerInvariant();
        if (r.StartsWith("swarmer")) return "swarmer";
        if (r.StartsWith("melee") || r.StartsWith("shield") || r.StartsWith("charger")) return "melee";
        if (r.StartsWith("ranged")) return "ranged";
        if (r.StartsWith("caster")) return "caster";
        if (r.StartsWith("support")) return "support";
        if (r.StartsWith("bruiser") || r.StartsWith("exploder")) return "bruiser";
        if (r.StartsWith("giant")) return "giant";
        if (r.StartsWith("mother")) return "mother";
        if (r.StartsWith("flyer")) return "flyer";
        if (r.StartsWith("turret")) return "turret";
        if (r.StartsWith("swarm")) return "swarm";
        if (r.StartsWith("ambusher")) return "ambusher";
        if (r.StartsWith("burrower")) return "burrower";
        if (r.StartsWith("stealth")) return "stalker";
        return "special";
    }

    public static PackSlot SlotOf(MonsterDef d)
    {
        if (d.AI == MonAI.Swarm) return PackSlot.Cloud;
        if (d.AI == MonAI.Turret || d.Is(MonTrait.Dormant)) return PackSlot.Placed;
        if (d.Giant || d.Family == "hauler") return PackSlot.Giant;
        if (d.AI is MonAI.Healer or MonAI.Reviver or MonAI.Spawner or MonAI.Caller or MonAI.Support || d.Role == "support") return PackSlot.Special;
        if (d.AI is MonAI.Ranged or MonAI.Caster or MonAI.Teleporter or MonAI.Bomber or MonAI.Flyer or MonAI.Stalker or MonAI.Burrower) return PackSlot.Back;
        if (d.AI is MonAI.Exploder or MonAI.Brute || d.Role == "bruiser") return PackSlot.Heavy;
        return d.Role == "swarmer" ? PackSlot.Swarmer : PackSlot.Front;
    }

    private static string SfxOf(string family, bool heavy) => family switch
    {
        "bone" or "frost" => heavy ? "brute" : "skeleton",
        "husk" or "wax" => heavy ? "rotspawn" : "husk",
        "cult" or "reaver" => heavy ? "brute" : "cultist",
        "wraith" or "coldfire" or "ash" => heavy ? "brute" : "wraith",
        "dusk" or "sexton" or "shroud" => heavy ? "brute" : "stalker",
        "hauler" or "forge" => "brute",
        "char" or "lamp" or "lurk" => "rotspawn",
        "mire" => heavy ? "rotspawn" : "hag",
        "effigy" or "rust" or "engine" or "bell" => "construct",
        "sands" => heavy ? "construct" : "revenant",
        "fiend" => heavy ? "brute" : "fiend",
        "hush" => heavy ? "brute" : "choir",
        _ => "husk",
    };

    public static Element ElementOf(string name, Element fallback) => (name ?? "").ToLowerInvariant() switch
    {
        "physical" => Element.Physical, "fire" => Element.Fire, "frost" => Element.Frost, "lightning" => Element.Lightning,
        "poison" => Element.Poison, "shadow" => Element.Shadow, _ => fallback,
    };

    private static Color TintOf(string region, string element) => region switch
    {
        "frost" => new Color(0.62f, 0.84f, 1f), "mire" => new Color(0.6f, 0.8f, 0.5f), "sands" => new Color(1f, 0.86f, 0.58f),
        "abyss" => new Color(1f, 0.6f, 0.4f), "crypt" => new Color(0.82f, 0.86f, 0.8f), "sanctum" => new Color(0.95f, 0.95f, 1f),
        _ => ElementOf(element, Element.Physical) switch
        {
            Element.Frost => new Color(0.62f, 0.84f, 1f), Element.Poison => new Color(0.6f, 0.8f, 0.5f), Element.Fire => new Color(1f, 0.6f, 0.4f),
            Element.Lightning => new Color(1f, 0.86f, 0.58f), Element.Shadow => new Color(0.75f, 0.65f, 0.95f), _ => Colors.White,
        },
    };

    // ───────────────────────── status text ─────────────────────────

    private static readonly Regex Clause = new(
        @"^(chill|burn|poison|bleed|slow|weaken|stun|root|fear|vulnerable|freeze|knockback)\b\s*([\d.]+)?\s*(?:(?:over|for)\s*([\d.]+)\s*s)?", RegexOptions.Compiled);
    private static readonly string[] Qualified =
    {
        "waking", "pounce", "burst", "lit:", "(charge", "aura", "inside", "per tick", "4th tick", "centre", "direct hit", "second hit", "while held",
        "wail", "within", "opening strike", "ground", "thrown", "chains", "heals", "dims",
    };

    /// <summary>
    /// The statuses a type's ordinary hits apply, read from the bestiary's status text ("chill 0.3", "burn 0.3 over 3 s",
    /// "slow 0.3 for 2 s", "stun 0.5 s (30% chance)", "knockback" …). Clauses that belong to one behaviour (the waking strike,
    /// the death burst, the aura, the hook's pull …) are left out: MonsterAiData.cs gives those to the behaviour.
    /// </summary>
    public static List<StatusApply> ParseStatuses(string text, bool includeQualified = false)
    {
        var res = new List<StatusApply>();
        if (string.IsNullOrWhiteSpace(text)) return res;
        foreach (var raw in text.Split(';'))
        {
            string c = raw.Trim().ToLowerInvariant();
            if (c.Length == 0 || c == "-" || c.StartsWith("none")) continue;
            if (!includeQualified && Qualified.Any(q => c.Contains(q))) continue;
            var m = Clause.Match(c);
            if (!m.Success) continue;
            float a = m.Groups[2].Success ? float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0f;
            float b = m.Groups[3].Success ? float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 0f;
            float chance = 1f;
            var ch = Regex.Match(c, @"(\d+)% chance");
            if (ch.Success) chance = int.Parse(ch.Groups[1].Value, CultureInfo.InvariantCulture) / 100f;
            StatusApply? st = m.Groups[1].Value switch
            {
                "chill" => St.Chill(a > 0 ? a : 0.3f),
                "burn" => St.Burn(a > 0 ? a : 0.3f, b > 0 ? b : 3f),
                "poison" => St.Poison(a > 0 ? a : 0.3f, b > 0 ? b : 3f),
                "bleed" => St.Bleed(a > 0 ? a : 0.3f, b > 0 ? b : 3f),
                "slow" => St.Slow(a > 0 ? a : 0.3f, b > 0 ? b : 2f),
                "weaken" => St.Weaken(a > 0 ? a : 0.2f, b > 0 ? b : 3f),
                "stun" => St.Stun(a > 0 ? a : 0.5f, chance),
                "root" => St.Root(a > 0 ? a : 1f),
                "fear" => new StatusApply(StatusId.Fear, a > 0 ? a : 1f, 0, chance),
                "vulnerable" => St.Vuln(a > 0 ? a : 3f),
                "freeze" => St.Freeze(a > 0 ? a : 1f),
                "knockback" => St.Knock(6f),
                _ => null,
            };
            if (st.HasValue) res.Add(st.Value);
        }
        return res;
    }

    // ───────────────────────── regions ─────────────────────────

    /// <summary>Called by ZoneDb's static constructor: the spawn tables and dungeon family lists of the design replace the built-in ones.</summary>
    public static void ApplyRegions(List<RegionDef> regions)
    {
        if (!Live || !EnsureLoaded()) return;
        foreach (var reg in regions)
        {
            if (!RegionById.TryGetValue(reg.Id, out var br)) { Problems.Add($"region '{reg.Id}' is not in the bestiary: its built-in spawn list stays"); continue; }
            if (br.Spawns.Count > 0) reg.Spawns = br.Spawns.Select(s => (s.id, s.w)).ToArray();
            if (br.Families.Count > 0) reg.Families = br.Families.ToArray();
            foreach (var d in reg.Dungeons)
            {
                var bd = br.Dungeons.FirstOrDefault(x => x.id == d.Id);
                if (bd.id != null && bd.families.Length > 0) d.Families = bd.families;
                else Problems.Add($"dungeon '{d.Id}' has no family list in the bestiary: it uses its region's mix");
            }
        }
    }

    // ───────────────────────── models and stand-ins ─────────────────────────

    public static bool ModelExists(string modelId) => modelId != null && ResourceLoader.Exists($"res://assets/chars/{modelId}.glb");
    public static BestiaryType TypeOfModel(string modelId)
    {
        if (!Live) return null;         // parked: CharModel resolves models exactly as before
        EnsureLoaded();
        return modelId != null && TypeByModel.TryGetValue(modelId, out var t) ? t : null;
    }

    private static readonly Dictionary<string, string[]> PlanGenerics = new()
    {
        ["humanoid biped"] = new[] { "mon_cultist", "mon_skeleton", "mon_husk" },
        ["oversized bruiser"] = new[] { "mon_brute", "mon_knight", "mon_rotspawn" },
        ["giant / construct"] = new[] { "mon_brute", "mon_brass_sentinel", "mon_knight" },
        ["quadruped beast"] = new[] { "form_wolf", "form_bear", "mon_rime_stalker", "mon_fiend" },
        ["arthropod / crawler"] = new[] { "mon_rime_stalker", "form_wolf", "mon_husk" },
        ["serpentine"] = new[] { "mon_wraith", "mon_husk" },
        ["flyer"] = new[] { "mon_imp", "mon_wraith", "mon_fiend" },
        ["floating / incorporeal"] = new[] { "mon_wraith", "mon_choir" },
        ["swarm / turret / siege"] = new[] { "mon_brass_sentinel" },
    };

    /// <summary>
    /// What a monster model that is not on disk is drawn with, best first: the model of the type it is a variant of (and
    /// that type's own base), then the built member of its family with the closest role, then any built creature of the
    /// same rig and body plan, then a generic for the body plan. Clouds and emplacements fall back to a primitive
    /// ("prim_swarm", "prim_turret") rather than to a person. Empty for models the bestiary does not know.
    /// </summary>
    public static List<string> StandInsFor(string modelId)
    {
        var res = new List<string>();
        var t = TypeOfModel(modelId);
        if (t == null) return res;
        void Add(string m) { if (m != null && m != modelId && !res.Contains(m)) res.Add(m); }
        // 1. the variant chain
        var seen = new HashSet<string> { t.Id };
        for (var b = t; b != null && b.VariantOf != null && TypeById.TryGetValue(b.VariantOf, out var nb) && seen.Add(nb.Id); b = nb) Add(nb.Model);
        // immobile and bodiless things should not turn into people or beasts while they wait for their model
        if (t.AiKind == "Swarm") { Add("prim_swarm"); return res; }
        if (t.AiKind == "Turret") { Add("prim_turret"); return res; }
        // 2. the family's built member with the closest role
        string rc = RoleClass(t.Role);
        foreach (var o in Types.Where(o => o.Family == t.Family && o.Id != t.Id && ModelExists(o.Model)).OrderBy(o => Distance(t, o, rc))) Add(o.Model);
        // 3. any built creature of the same rig and body plan (nothing to offer for the humanoid rig: step 4 covers it)
        if (t.Rig != "A")
            foreach (var o in Types.Where(o => o.Rig == t.Rig && o.Id != t.Id && ModelExists(o.Model)).OrderBy(o => Distance(t, o, rc))) Add(o.Model);
        // 4. a generic for the body plan
        if (PlanGenerics.TryGetValue(t.BodyPlan, out var gen))
        {
            if (t.BodyPlan == "quadruped beast" && t.Scale >= 1.15f) Add("form_bear");
            foreach (var g in gen) Add(g);
        }
        Add("mon_skeleton");
        return res;
    }

    private static float Distance(BestiaryType t, BestiaryType o, string roleClass)
    {
        float d = MathF.Abs(t.Scale - o.Scale) * 2f;
        if (o.Rig != t.Rig) d += 8f;
        if (o.BodyPlan != t.BodyPlan) d += 3f;
        if (RoleClass(o.Role) != roleClass) d += 2f;
        if (o.AiKind != t.AiKind) d += 1f;
        return d;
    }

    /// <summary>The model file a type is drawn with right now: its own, or the first stand-in that exists.</summary>
    public static string ResolvedModel(string modelId, out bool standIn)
    {
        standIn = false;
        if (ModelExists(modelId)) return modelId;
        standIn = true;
        foreach (var s in StandInsFor(modelId)) if (s.StartsWith("prim_") || ModelExists(s)) return s;
        return "mon_skeleton";
    }

    // ───────────────────────── boot check ─────────────────────────

    public static int TypeCount => Types.Count;
    public static int ReskinCount => Reskins.Count;
    public static int StandInCount { get; private set; }
    public static readonly List<string> StandInTypes = new();

    /// <summary>
    /// Run once when the game starts: loads the data (through MonsterDb and ZoneDb), checks it against the definitions and
    /// the models on disk, and writes one summary line (plus one debug line per type with --verbose).
    /// </summary>
    public static void Boot()
    {
        if (_booted || !Live) return;
        _booted = true;
        int defs = MonsterDb.All.Count; _ = ZoneDb.Regions.Count;
        if (!Loaded) { Log.Warn("bestiary", $"not loaded: {defs} built-in definitions only"); return; }
        // every type and reskin has a definition; every type is reachable in some region
        foreach (var t in Types) if (MonsterDb.Get(t.Id) == null) Problems.Add($"type '{t.Id}' has no definition");
        foreach (var r in Reskins) if (MonsterDb.Get(r.Id) == null) Problems.Add($"reskin '{r.Id}' has no definition");
        var reachable = new HashSet<string>();
        foreach (var reg in ZoneDb.Regions)
        {
            if (reg.Families == null) { Problems.Add($"region '{reg.Id}' has no family table"); continue; }
            float total = reg.Families.Sum(f => f.w);
            if (MathF.Abs(total - 100f) > 0.6f) Problems.Add($"region '{reg.Id}': family weights add up to {total:0.#}, not 100");
            foreach (var f in reg.Families)
                foreach (var id in f.types)
                {
                    var d = MonsterDb.Get(id);
                    if (d == null) { Problems.Add($"region '{reg.Id}': '{id}' has no definition"); continue; }
                    if (d.Family != f.id) Problems.Add($"region '{reg.Id}': '{id}' is listed under family '{f.id}' but belongs to '{d.Family}'");
                    reachable.Add(d.BaseId ?? d.Id); reachable.Add(d.Id);
                }
            foreach (var (id, w) in reg.Spawns) if (MonsterDb.Get(id) == null) Problems.Add($"region '{reg.Id}' spawn list: '{id}' has no definition");
            foreach (var dg in reg.Dungeons)
                if (dg.Families != null)
                    foreach (var fid in dg.Families)
                        if (!reg.Families.Any(f => f.id == fid)) Problems.Add($"dungeon '{dg.Id}' draws family '{fid}', which its region '{reg.Id}' does not have");
        }
        foreach (var t in Types) if (!reachable.Contains(t.Id)) Problems.Add($"type '{t.Id}' is in no region's family table");
        foreach (var r in Reskins) if (!reachable.Contains(r.Id)) Problems.Add($"reskin '{r.Id}' is in no region's family table");
        foreach (var d in MonsterDb.All)
        {
            if (!d.InBestiary) continue;
            foreach (var id in new[] { d.Ai.SpawnDef, d.Ai.CallDef, d.Ai.SplitDef }.Concat(d.Ai.EscortDefs ?? Array.Empty<string>()))
                if (id != null && MonsterDb.Get(id) == null) Problems.Add($"type '{d.Id}' summons unknown type '{id}'");
        }

        // models: the type's own file, or a stand-in from the data
        int real = 0, prim = 0; StandInCount = 0; StandInTypes.Clear();
        var byRig = new SortedDictionary<string, (int real, int total)>();
        foreach (var t in Types)
        {
            string got = ResolvedModel(t.Model, out bool standIn);
            byRig.TryGetValue(t.Rig, out var c);
            byRig[t.Rig] = (c.real + (standIn ? 0 : 1), c.total + 1);
            if (standIn) { StandInCount++; StandInTypes.Add($"{t.Id}→{got}"); if (got.StartsWith("prim_")) prim++; } else real++;
            var d = MonsterDb.Get(t.Id);
            if (Log.Verbose && d != null)
                Log.Debug("bestiary", $"{t.Id} [{t.Family}] {d.AI}{(d.Traits != MonTrait.None ? " + " + d.Traits : "")} slot={d.Slot} rig {t.Rig} model {t.Model}{(standIn ? " → stand-in " + got : "")} " +
                    $"hp×{d.Hp:0.##} dmg×{d.Dmg:0.##} speed {d.Speed:0.#} scale {d.Scale:0.##} el {d.Element} attacks [{string.Join(",", d.AttackAnims)}] idle {d.Idle} walk {d.Walk} run {d.Run} regions [{string.Join(",", t.Regions)}] w={t.SpawnWeight:0.##}");
        }
        if (Log.Verbose)
            foreach (var r in Reskins)
            {
                var d = MonsterDb.Get(r.Id);
                if (d != null) Log.Debug("bestiary", $"reskin {r.Id} = {r.Base} in {r.Region}: '{d.Name}' el {d.Element} onHit {StatusText(d.OnHit)}{(d.OnHit2.HasValue ? " + " + StatusText(d.OnHit2) : "")} tint ({d.Tint.R:0.##},{d.Tint.G:0.##},{d.Tint.B:0.##}) traits {d.Traits}");
            }
        Log.Info("bestiary", $"loaded {Types.Count} types ({_merged} merged into built-in definitions, {_added} new), {Reskins.Count} reskins ({_reskinsMerged} built-in, {_reskinsAdded} new), " +
            $"{Families.Count} families, {Regions.Count} regions; models: {real} real, {StandInCount} stand-ins ({prim} primitive) — by rig " +
            string.Join(" ", byRig.Select(kv => $"{kv.Key}:{kv.Value.real}/{kv.Value.total}")) + $"; definitions total {MonsterDb.All.Count}; problems {Problems.Count}");
        if (StandInCount > 0) Log.Info("bestiary", "stand-ins: " + string.Join(" ", StandInTypes));
        foreach (var p in Problems.Take(40)) Log.Warn("bestiary", p);
        if (Problems.Count > 40) Log.Warn("bestiary", $"… and {Problems.Count - 40} more problems");
    }

    public static IReadOnlyList<string> DataProblems => Problems;

    public static string StatusText(StatusApply? s) =>
        !s.HasValue ? "-" : $"{s.Value.Id}({s.Value.Mag:0.##},{s.Value.Dur:0.#}s{(s.Value.Chance < 1f ? $",{s.Value.Chance * 100:0}%" : "")})";
}
