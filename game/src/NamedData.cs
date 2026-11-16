using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenAbyss;

public enum NamedKind { Rare, Stronghold, Dungeon, Region, World, Pinnacle }

/// <summary>
/// One named encounter. Identity (name, base creature, region, where it is met, look, the designer's mechanics list)
/// comes from the <c>named</c> section of data/bestiary.json; presentation and tuning (title, scale, tint, life and
/// damage factors, loot tier, arena theme, placement rule, the spoken intro) are filled in by <see cref="NamedDb"/>.
/// </summary>
public sealed class NamedDef
{
    public string Id, NameZh, NameEn, Base, Where, Look;
    public string[] Regions = Array.Empty<string>();     // region ids it belongs to (world bosses: two)
    public string[] MechText = Array.Empty<string>();    // the design's mechanics list, verbatim
    public NamedKind Kind;
    // presentation
    public string Title = "";                            // second line of the name plate
    public float Scale = 1.5f;                           // final model scale
    public Color Tint = Colors.White, Rim = new(1f, 0.55f, 0.2f);
    public Element Aura = Element.Fire;                  // colour of the attached glow / aura effects
    public string Intro, IntroSpeaker, IntroVoice;       // one spoken line (voice id = Id + "_intro"); null = silent
    // tuning: life in "ordinary monster lives of its level", damage and experience as multiples of an ordinary monster
    public float Hp = 13f, Dmg = 1.35f, Xp = 12f;
    public int LevelBonus;                               // on top of the area's monster level
    public int Affixes;                                  // ordinary elite affixes rolled on top (rares: 2)
    public int LootTier = 1;                             // 1 rare elite, 2 stronghold / dungeon, 3 world, 4 pinnacle
    public string StandIn;                               // MonsterDb id used while the base type has no definition
    public string Theme = "crypt";                       // arena theme for the test arena / world-boss field / ladder rung
    public string Spot = "open";                         // rare elites: where in the overworld they are put
    public int Rung;                                     // pinnacle: 1..8
    public string DungeonId;                             // dungeon bosses
    public Func<NamedScript> Script;                     // null = no script yet (the monster still spawns, named and scaled)
    public string[] Mechs = Array.Empty<string>();       // keys the script reports through Mech(); the suite expects each at least once
    public Action<MonsterDef> Shape;                     // last-minute changes to the cloned definition

    public string Region => Regions.Length > 0 ? Regions[0] : null;
    public bool Boss => Kind != NamedKind.Rare;
}

/// <summary>Kill records of named encounters, kept in the save (plain fields; older saves load with empty tables).</summary>
public sealed partial class SaveData
{
    /// <summary>Named id → kills. Pinnacle bosses also count under "id@t&lt;tier&gt;".</summary>
    public Dictionary<string, int> NamedKills = new();
    /// <summary>World boss id → index of the last interval whose cache was claimed.</summary>
    public Dictionary<string, int> NamedCache = new();
}

/// <summary>What the quest log / map may show about named encounters. Read-only views over the save.</summary>
public static class NamedRecords
{
    private static SaveData D => Game.I?.Hero?.D;

    public static int Kills(string id) { var d = D; if (d == null || id == null) return 0; d.NamedKills ??= new(); return d.NamedKills.GetValueOrDefault(id); }
    public static int KillsAtTier(string id, int tier) => Kills($"{id}@t{tier}");
    /// <summary>Highest world tier at which this (pinnacle) boss fell, or -1.</summary>
    public static int BestTier(string id) { for (int t = 3; t >= 0; t--) if (KillsAtTier(id, t) > 0) return t; return -1; }

    internal static void Count(NamedDef def, int tier)
    {
        var d = D; if (d == null || def == null) return;
        d.NamedKills ??= new();
        d.NamedKills[def.Id] = d.NamedKills.GetValueOrDefault(def.Id) + 1;
        if (def.Kind == NamedKind.Pinnacle) { string k = $"{def.Id}@t{tier}"; d.NamedKills[k] = d.NamedKills.GetValueOrDefault(k) + 1; }
    }

    /// <summary>The rare hunt of a region: its four rare elites with how often each was killed.</summary>
    public static List<(NamedDef def, int kills)> RareHunt(string regionId) =>
        NamedDb.All.Where(n => n.Kind == NamedKind.Rare && n.Region == regionId).Select(n => (n, Kills(n.Id))).ToList();

    /// <summary>"3/28" style tally of distinct rare elites killed.</summary>
    public static (int done, int total) RareTally(string regionId = null)
    {
        int done = 0, total = 0;
        foreach (var n in NamedDb.All)
        {
            if (n.Kind != NamedKind.Rare || (regionId != null && n.Region != regionId)) continue;
            total++; if (Kills(n.Id) > 0) done++;
        }
        return (done, total);
    }

    /// <summary>Ready-made lines for a quest-log page: one per region, plus world bosses and the ladder.</summary>
    public static List<string> Lines()
    {
        var res = new List<string>();
        foreach (var r in ZoneDb.Regions)
        {
            var (done, total) = RareTally(r.Id);
            var names = RareHunt(r.Id).Select(x => x.kills > 0 ? $"{x.def.NameZh}×{x.kills}" : "？？？");
            res.Add($"{r.Name}　稀有獵殺 {done}/{total}：{string.Join("、", names)}");
        }
        foreach (var n in NamedDb.All.Where(n => n.Kind == NamedKind.World)) res.Add($"世界首領　{n.NameZh}　擊殺 {Kills(n.Id)}");
        foreach (var n in NamedDb.All.Where(n => n.Kind == NamedKind.Pinnacle).OrderBy(n => n.Rung))
        {
            int best = BestTier(n.Id);
            res.Add($"燼階第 {n.Rung} 階　{n.NameZh}　{(best < 0 ? "未攻克" : $"最高攻克：{ZoneDb.TierNames[best]}")}");
        }
        return res;
    }
}

/// <summary>
/// The table of named encounters: bestiary.json supplies identity, this class supplies everything the game needs to
/// stage them. Encounter scripts register themselves through the partial methods at the bottom, one per file.
/// </summary>
public static partial class NamedDb
{
    public static readonly List<NamedDef> All = new();
    private static readonly Dictionary<string, NamedDef> ById = new();
    private static bool _loaded;

    public static NamedDef Get(string id) { Ensure(); return id != null && ById.TryGetValue(id, out var d) ? d : null; }
    public static IEnumerable<NamedDef> Of(NamedKind k) { Ensure(); return All.Where(n => n.Kind == k); }
    public static NamedDef StrongholdOf(string regionId) { Ensure(); return All.FirstOrDefault(n => n.Kind == NamedKind.Stronghold && n.Region == regionId); }
    public static NamedDef DungeonBossOf(string dungeonId) { Ensure(); return All.FirstOrDefault(n => n.Kind == NamedKind.Dungeon && n.DungeonId == dungeonId); }
    public static NamedDef RungOf(int rung) { Ensure(); return All.FirstOrDefault(n => n.Kind == NamedKind.Pinnacle && n.Rung == rung); }

    public static void Ensure()
    {
        if (_loaded) return;
        _loaded = true;
        try { Load(); }
        catch (Exception e) { Log.Error("named", "loading the named section of data/bestiary.json failed", e); }
        try { Stage(); }
        catch (Exception e) { Log.Error("named", "staging the named encounters failed", e); }
        var reg = new Registry();
        try { RegisterRares(reg); } catch (Exception e) { Log.Error("named", "registering the rare elites failed", e); }
        try { RegisterStrongholds(reg); } catch (Exception e) { Log.Error("named", "registering the stronghold bosses failed", e); }
        try { RegisterDungeons(reg); } catch (Exception e) { Log.Error("named", "registering the dungeon bosses failed", e); }
        try { RegisterWorld(reg); } catch (Exception e) { Log.Error("named", "registering the world bosses failed", e); }
        try { RegisterPinnacle(reg); } catch (Exception e) { Log.Error("named", "registering the pinnacle bosses failed", e); }
        int scripted = All.Count(n => n.Script != null);
        Log.Info("named", $"layer ready: {All.Count} definitions ({string.Join(", ", Enum.GetValues<NamedKind>().Select(k => $"{k} {All.Count(n => n.Kind == k)}"))}), scripted={scripted}");
    }

    // ───────────────────────── data ─────────────────────────

    private static readonly (string key, NamedKind kind)[] Sections =
    {
        ("rare_elites", NamedKind.Rare), ("stronghold_bosses", NamedKind.Stronghold), ("dungeon_bosses", NamedKind.Dungeon),
        ("region_bosses", NamedKind.Region), ("world_bosses", NamedKind.World), ("pinnacle_bosses", NamedKind.Pinnacle),
    };

    private static void Load()
    {
        const string path = "res://data/bestiary.json";
        if (!FileAccess.FileExists(path)) { Log.Error("named", $"{path} is missing: no named encounters"); return; }
        string text = FileAccess.GetFileAsString(path);
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("named", out var named)) { Log.Error("named", "bestiary.json has no 'named' section"); return; }
        foreach (var (key, kind) in Sections)
        {
            if (!named.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) { Log.Warn("named", $"named.{key} is missing"); continue; }
            foreach (var e in arr.EnumerateArray())
            {
                string S(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
                var d = new NamedDef { Id = S("id"), NameZh = S("name_zh"), NameEn = S("name_en"), Base = S("base"), Where = S("where"), Look = S("look"), Kind = kind };
                if (string.IsNullOrEmpty(d.Id) || ById.ContainsKey(d.Id)) continue;
                string region = S("region");
                d.Regions = ZoneDb.Regions.Select(r => r.Id).Where(id => region.Contains(id, StringComparison.Ordinal)).ToArray();
                if (e.TryGetProperty("mechanics", out var ms) && ms.ValueKind == JsonValueKind.Array)
                    d.MechText = ms.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
                All.Add(d); ById[d.Id] = d;
            }
        }
    }

    // ───────────────────────── staging ─────────────────────────

    private static readonly Dictionary<string, string> StandIns = new()
    {
        ["dusk_hound"] = "rime_stalker", ["husk_wailer"] = "husk", ["ash_weeper"] = "cult_caster", ["char_hangtree"] = "rotspawn", ["rime_horn"] = "frostbound",
        ["dusk_lynx"] = "rime_stalker", ["rime_packbeast"] = "frost_knight", ["cf_spirit"] = "ice_wraith", ["bone_pallbearer"] = "knight", ["shroud_dropper"] = "rime_stalker",
        ["effigy_knight"] = "knight", ["wraith_mourner"] = "wraith", ["bog_toad"] = "rotspawn", ["lamp_bearer"] = "mire_hag", ["lurk_elder"] = "rotspawn",
        ["bog_netter"] = "mire_thrower", ["dune_mirror"] = "brass_sentinel", ["rust_stinger"] = "sand_revenant", ["dune_stormjar"] = "sand_caster", ["dune_disc"] = "sand_thrower",
        ["bell_hanging"] = "brass_sentinel", ["wax_wick"] = "husk", ["cult_snuffer"] = "cultist", ["forge_hammer"] = "brute", ["fiend_caller"] = "cult_caster",
        ["hush_mouth"] = "wraith", ["hauler_stoker"] = "brute", ["husk_huddle"] = "husk_bloated", ["char_root"] = "rotspawn", ["rime_sentry"] = "frost_knight",
        ["bone_amalgam"] = "knight", ["lurk_eel"] = "rotspawn", ["engine_heliostat"] = "brass_sentinel", ["bell_clapper"] = "knight", ["forge_bellows"] = "brute",
        ["sexton_matron"] = "rotspawn", ["hauler_hod"] = "brute", ["hush_serpent"] = "wraith",
    };

    /// <summary>
    /// The definition a named encounter is cloned from: its base type when the roster has it, the nearest existing
    /// creature otherwise (never blocks on a missing type or model — CharModel has its own model stand-ins).
    /// </summary>
    public static MonsterDef BaseDef(NamedDef n, out bool standIn)
    {
        standIn = false;
        var d = MonsterDb.Get(n.Base);
        if (d != null) return d;
        standIn = true;
        d = MonsterDb.Get(n.StandIn) ?? (StandIns.TryGetValue(n.Base ?? "", out var alt) ? MonsterDb.Get(alt) : null);
        return d ?? MonsterDb.Get("knight") ?? MonsterDb.All[0];
    }

    private static readonly Dictionary<string, string> RegionTheme = new()
    {
        ["wastes"] = "graveyard", ["frost"] = "icecave", ["crypt"] = "crypt", ["mire"] = "wetcave", ["sands"] = "sandtomb", ["sanctum"] = "cathedral", ["abyss"] = "lavacave",
    };
    private static readonly Dictionary<string, Element> RegionEl = new()
    {
        ["wastes"] = Element.Fire, ["frost"] = Element.Frost, ["crypt"] = Element.Shadow, ["mire"] = Element.Poison, ["sands"] = Element.Lightning, ["sanctum"] = Element.Shadow, ["abyss"] = Element.Fire,
    };

    private static void Stage()
    {
        foreach (var n in All)
        {
            string reg = n.Region;
            if (reg != null)
            {
                n.Theme = RegionTheme.GetValueOrDefault(reg, "crypt");
                n.Aura = RegionEl.GetValueOrDefault(reg, Element.Fire);
            }
            switch (n.Kind)
            {
                case NamedKind.Rare:
                    n.Hp = 13f; n.Dmg = 1.35f; n.Xp = 12f; n.Affixes = 2; n.LootTier = 1; n.Scale = 1.5f; n.Title = "稀有菁英";
                    n.Rim = new Color(1f, 0.78f, 0.3f);
                    break;
                case NamedKind.Stronghold:
                    n.Hp = 27f; n.Dmg = 1.5f; n.Xp = Balance.MiniBossXp; n.LootTier = 2; n.Scale = 1.7f;
                    n.Title = $"{ZoneDb.Region(reg)?.Stronghold ?? "據點"}之主";
                    n.IntroSpeaker = n.NameZh; n.IntroVoice = n.Id;
                    break;
                case NamedKind.Dungeon:
                    n.DungeonId = n.Id.StartsWith("db_", StringComparison.Ordinal) ? n.Id[3..] : null;
                    n.Hp = 27f; n.Dmg = 1.5f; n.Xp = Balance.MiniBossXp; n.LootTier = 2; n.Scale = 1.7f; n.LevelBonus = 0;
                    var dg = ZoneDb.Dungeon(n.DungeonId);
                    n.Title = $"{dg?.Name ?? "地城"}之主";
                    if (dg != null) n.Theme = dg.Theme;
                    break;
                case NamedKind.World:
                    n.Hp = 72f; n.Dmg = 1.9f; n.Xp = 60f; n.LootTier = 3; n.Scale = 3.6f; n.LevelBonus = 1; n.Title = "世界首領";
                    n.IntroSpeaker = "旁白"; n.IntroVoice = "narrator";
                    break;
                case NamedKind.Pinnacle:
                {
                    n.Rung = int.TryParse(n.Id.AsSpan(4, 1), out int r) ? r : 0;
                    var b = MonsterDb.Get(n.Base);
                    n.Hp = (b?.Hp ?? 60f) * 1.15f + 12f; n.Dmg = (b?.Dmg ?? 2.2f) * 1.2f; n.Xp = (b?.Xp ?? 60f) * 1.2f;
                    n.LootTier = 4; n.Scale = (b?.Scale ?? 2f) * 1.08f; n.LevelBonus = 2; n.Title = $"燼階・第{"一二三四五六七八"[Math.Clamp(n.Rung - 1, 0, 7)]}階";
                    n.Rim = new Color(1f, 0.9f, 0.6f);
                    n.IntroSpeaker = n.NameZh; n.IntroVoice = b != null ? n.Base : n.Id;
                    break;
                }
                case NamedKind.Region:
                    n.Title = ""; n.LootTier = 0;
                    break;
            }
        }

        // ── rare elites: epithet, look (scale / tint), where in the overworld ──
        Rare("rare_wastes_oldhowl", "枯林道的群父", 1.9f, new Color(0.75f, 0.72f, 0.7f), "road");
        Rare("rare_wastes_nightwatch", "仍在鳴鐘示警", 1.45f, new Color(0.7f, 0.8f, 0.9f), "camp_north");
        Rare("rare_wastes_ashbride", "焚毀禮拜堂的新娘", 1.5f, new Color(0.85f, 0.85f, 0.85f), "open");
        Rare("rare_wastes_gallowsfather", "絞架林的老樹", 1.9f, new Color(0.55f, 0.45f, 0.4f), "grove");
        Rare("rare_frost_hamo", "號聲無人應答", 1.55f, new Color(0.7f, 0.9f, 1f), "ledge");
        Rare("rare_frost_whiteshadow", "風雪中的眼", 1.6f, new Color(1.25f, 1.25f, 1.3f), "ledge");
        Rare("rare_frost_lastrun", "永不抵達的補給", 1.7f, new Color(0.75f, 0.85f, 1f), "road");
        Rare("rare_frost_thousandyear", "山巔熄去的烽火", 1.9f, new Color(0.6f, 0.95f, 1.2f), "far");
        Rare("rare_crypt_belak", "送葬大道的盾牆", 1.5f, new Color(0.8f, 0.8f, 0.75f), "road");
        Rare("rare_crypt_veilwife", "懸繭穹廳之主", 1.7f, new Color(0.9f, 0.9f, 0.95f), "nook");
        Rare("rare_crypt_namelessking", "被鑿去面容的王", 1.75f, new Color(0.7f, 0.7f, 0.72f), "far");
        Rare("rare_crypt_hundredtears", "萬人塚的哭聲", 1.9f, new Color(0.75f, 0.9f, 1f), "open");
        Rare("rare_mire_olddrum", "沉沒爐廳的獨眼", 1.8f, new Color(0.6f, 0.75f, 0.45f), "open");
        Rare("rare_mire_guidelight", "霧中領路的燈", 1.35f, new Color(0.9f, 1f, 0.7f), "road");
        Rare("rare_mire_weepingriver", "長棧道下的深流", 2.0f, new Color(0.95f, 0.92f, 0.85f), "shore");
        Rare("rare_mire_dafu", "溺村曬網場的主人", 1.6f, new Color(0.65f, 0.8f, 0.6f), "open");
        Rare("rare_sands_ogan", "閱兵階上的鏡盾", 1.6f, new Color(1.2f, 1.05f, 0.6f), "road");
        Rare("rare_sands_hundredshell", "軍械沙丘的拾甲者", 1.9f, new Color(0.95f, 0.75f, 0.45f), "open");
        Rare("rare_sands_tesa", "引雷庭的長者", 1.5f, new Color(0.9f, 0.9f, 1.15f), "open");
        Rare("rare_sands_thirteenth", "無頂觀星臺的擲輪者", 1.55f, new Color(1.15f, 0.95f, 0.55f), "far");
        Rare("rare_sanctum_firstcantor", "遊行廣場的指揮", 1.5f, new Color(1.1f, 1.1f, 1.2f), "open");
        Rare("rare_sanctum_everquiet", "墜落鐘樓的裂鐘", 2.2f, new Color(0.85f, 0.8f, 0.6f), "open");
        Rare("rare_sanctum_kolen", "燭匠區不滅的燭", 1.45f, new Color(1.25f, 0.85f, 0.45f), "road");
        Rare("rare_sanctum_vesna", "審判階上的白面", 1.45f, new Color(1.15f, 1.15f, 1.15f), "camp_north");
        Rare("rare_abyss_foreman", "停擺軋場的哨聲", 1.8f, new Color(0.9f, 0.6f, 0.45f), "open");
        Rare("rare_abyss_firstborn", "渣階上的白金之焰", 1.5f, new Color(1.3f, 1.2f, 0.9f), "far");
        Rare("rare_abyss_whitetide", "虛空之上的白環", 1.8f, new Color(1.3f, 1.3f, 1.35f), "shore");
        Rare("rare_abyss_slagferry", "渣河裡的撐篙人", 2.1f, new Color(1f, 0.6f, 0.4f), "shore");

        // ── spoken intros (data/vo_lines.json carries the same ids for the voice run) ──
        Say("sh_wastes", "火……在我肚子裡。誰也別想吹熄它。");
        Say("sh_frost", "軍旗還立著。關，就還沒有破。");
        Say("sh_crypt", "鑰匙是我交出去的。這道門，也由我守到最後。");
        Say("sh_mire", "木頭……都是我的。寨子……也是我的。");
        Say("sh_sands", "商站已打烊。逾時滯留者，依規清場。");
        Say("sh_sanctum", "這條街封了。蠟乾之前，誰也不許過。");
        Say("sh_abyss", "餓……還有火嗎？把你的，也給我。");
        Say("wb_barrowbearer", "地在動。牠背著一整座陵寢，來替你送葬。");
        Say("wb_firstbearer", "柴……還沒送到。讓路。", "首役・負山者", "wb_firstbearer");
        Say("wb_daylessdisc", "黃銅的太陽睜開了眼。它記得怎麼亮，卻忘了為誰而亮。");
        Say("wb_breathofsilence", "聽——什麼都沒有。那，就是牠的呼吸。");
        Say("pin_1_gravedigger", "又是你。躺好——這一次，我把土壓實。");
        Say("pin_2_rimelord", "冬天沒有結束。我也沒有。");
        Say("pin_3_bishop", "我的棺是空的。你的，很快就不是了。");
        Say("pin_4_mire_mother", "潮漲了。這一回，連你的火一起淹掉。");
        Say("pin_5_sand_tyrant", "抬頭。那輪死去的太陽，正看著你我。");
        Say("pin_6_grand_hush", "最後一個小節。休止符，由你來填。");
        Say("pin_7_firstkeeper", "原來是你接了我的燈。來，讓我看看它還亮不亮。");
        Say("pin_8_osaros", "我醒了。這一次，沒有夢替你擋著。");

        // ── looks and arenas of the bosses ──
        Look("sh_wastes", 1.75f, new Color(1f, 0.8f, 0.6f), Element.Poison);
        Look("sh_frost", 1.75f, new Color(0.6f, 0.85f, 1.1f), Element.Frost);
        Look("sh_crypt", 1.7f, new Color(0.7f, 0.75f, 0.7f), Element.Shadow);
        Look("sh_mire", 2.1f, new Color(0.6f, 0.7f, 0.45f), Element.Poison);
        Look("sh_sands", 2.0f, new Color(1.1f, 0.95f, 0.6f), Element.Lightning);
        Look("sh_sanctum", 1.75f, new Color(1.2f, 1.2f, 1.15f), Element.Fire);
        Look("sh_abyss", 2.2f, new Color(1.2f, 0.7f, 0.4f), Element.Fire);
        Look("db_wastes_d1", 2.0f, new Color(0.85f, 0.8f, 0.7f), Element.Poison);
        Look("db_wastes_d2", 2.3f, new Color(0.6f, 0.45f, 0.35f), Element.Fire);
        Look("db_frost_d1", 1.9f, new Color(0.65f, 0.9f, 1.2f), Element.Frost);
        Look("db_frost_d2", 1.8f, new Color(0.75f, 0.9f, 1.1f), Element.Frost);
        Look("db_crypt_d1", 2.1f, new Color(0.6f, 0.6f, 0.62f), Element.Shadow);
        Look("db_crypt_d2", 2.0f, new Color(0.9f, 0.88f, 0.8f), Element.Physical);
        Look("db_mire_d1", 2.3f, new Color(0.9f, 0.9f, 0.8f), Element.Poison);
        Look("db_mire_d2", 1.6f, new Color(0.7f, 0.85f, 0.55f), Element.Poison);
        Look("db_sands_d1", 2.6f, new Color(1.15f, 0.95f, 0.5f), Element.Lightning);
        Look("db_sands_d2", 1.6f, new Color(1.1f, 0.95f, 0.75f), Element.Physical);
        Look("db_sanctum_d1", 1.7f, new Color(1.1f, 1.1f, 1.25f), Element.Shadow);
        Look("db_sanctum_d2", 1.9f, new Color(0.85f, 0.8f, 0.65f), Element.Physical);
        Look("db_abyss_d1", 2.8f, new Color(0.8f, 0.55f, 0.45f), Element.Fire);
        Look("db_abyss_d2", 1.85f, new Color(1.1f, 0.8f, 0.75f), Element.Fire);
        Look("wb_barrowbearer", 3.6f, new Color(0.55f, 0.5f, 0.45f), Element.Physical, "graveyard");
        Look("wb_firstbearer", 3.4f, new Color(0.8f, 0.7f, 0.6f), Element.Fire, "icecave");
        Look("wb_daylessdisc", 3.8f, new Color(1.3f, 1.1f, 0.55f), Element.Lightning, "brasshall");
        Look("wb_breathofsilence", 4.0f, new Color(1.4f, 1.4f, 1.45f), Element.Shadow, "lavacave");
        Look("pin_1_gravedigger", 0f, new Color(0.8f, 0.85f, 1f), Element.Shadow, "graveyard");
        Look("pin_2_rimelord", 0f, new Color(0.8f, 0.95f, 1.25f), Element.Frost, "icehall");
        Look("pin_3_bishop", 0f, new Color(1f, 0.95f, 0.85f), Element.Shadow, "crypt");
        Look("pin_4_mire_mother", 0f, new Color(0.7f, 1f, 0.75f), Element.Poison, "wetcave");
        Look("pin_5_sand_tyrant", 0f, new Color(1.3f, 1.25f, 1.1f), Element.Lightning, "brasshall");
        Look("pin_6_grand_hush", 0f, new Color(1.35f, 1.35f, 1.35f), Element.Shadow, "cathedral");
        Look("pin_7_firstkeeper", 1.5f, new Color(1.25f, 1.05f, 0.8f), Element.Fire, "graveyard");
        Look("pin_8_osaros", 0f, new Color(1.2f, 1.15f, 1.3f), Element.Shadow, "forge");
        var keeper = Get("pin_7_firstkeeper");
        if (keeper != null) { keeper.StandIn = "knight"; keeper.Hp = 78f; keeper.Dmg = 2.6f; keeper.Xp = 80f; keeper.IntroVoice = "pin_7_firstkeeper"; keeper.Shape = d => { d.Model = "hero_lampknight"; d.Idle = "Idle_Shield_Loop"; d.Element = Element.Fire; }; }
    }

    private static void Rare(string id, string title, float scale, Color tint, string spot)
    {
        if (!ById.TryGetValue(id, out var n)) return;
        n.Title = title; n.Scale = scale; n.Tint = tint; n.Spot = spot;
    }

    private static void Look(string id, float scale, Color tint, Element aura, string theme = null)
    {
        if (!ById.TryGetValue(id, out var n)) return;
        if (scale > 0f) n.Scale = scale;
        n.Tint = tint; n.Aura = aura;
        if (theme != null) n.Theme = theme;
    }

    private static void Say(string id, string text, string speaker = null, string voice = null)
    {
        if (!ById.TryGetValue(id, out var n)) return;
        n.Intro = text;
        if (speaker != null) n.IntroSpeaker = speaker;
        if (voice != null) n.IntroVoice = voice;
    }

    // ───────────────────────── script registration ─────────────────────────

    /// <summary>Binds a script (and the mechanic keys it must report) to an encounter; may also adjust its tuning.</summary>
    public sealed class Registry
    {
        public void Add(string id, Func<NamedScript> script, string mechs, Action<NamedDef> tune = null)
        {
            if (!ById.TryGetValue(id, out var n)) { Log.Warn("named", $"script for unknown encounter '{id}'"); return; }
            n.Script = script;
            n.Mechs = string.IsNullOrWhiteSpace(mechs) ? Array.Empty<string>() : mechs.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            tune?.Invoke(n);
        }
    }

    static partial void RegisterRares(Registry r);
    static partial void RegisterStrongholds(Registry r);
    static partial void RegisterDungeons(Registry r);
    static partial void RegisterWorld(Registry r);
    static partial void RegisterPinnacle(Registry r);
}
