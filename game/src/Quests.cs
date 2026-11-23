using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// One objective of a quest. A step is finished by a World event (or, for "talk", by speaking to the NPC):
/// talk(npc) · area(areaId) · stronghold(regionId) · dungeon(dungeonId) · boss(bossDefId) are remembered as flags, so a
/// step that was already done before its quest reached it completes at once; elite / altar / event are counted
/// inside <see cref="Region"/> until <see cref="Count"/> is reached.
/// </summary>
public sealed class QuestStep
{
    public string Text;            // shown in the tracker and the quest log
    public string Event;           // talk, area, stronghold, dungeon, boss, elite, altar, event
    public string Arg;             // npc id, area id, region id, dungeon id or boss def id the step waits for
    public string Region;          // counted steps: only progress made in this region counts
    public int Count = 1;          // counted steps: how many are needed
    public string Area;            // where the map should point while this step is current
    public string Dialogue;        // talk steps: the script the NPC speaks (DialogueDb id)
    public string AltBoss;         // area / dungeon steps on the way to a boss: also done once that boss is dead
}

public sealed class QuestDef
{
    public string Id, Name, Giver, GiverId, Desc, Region;     // Giver is the display name, GiverId the npc id
    public bool Main;
    public int Act;
    public List<QuestStep> Steps = new();
    public string[] Requires = Array.Empty<string>();         // quests that must be finished before this one starts
    public string Remind;                                     // main quests: what the elder says while it is under way (bark id)
    // rewards, granted when the last step is done
    public float XpFrac;                                      // experience as a fraction of the hero's current level
    public int Gold, SkillPts, PotionCap;
}

/// <summary>The campaign (one main quest per act) and two side quests for each of the seven regions.</summary>
public static class QuestDb
{
    public const string Elder = "elder", Smith = "smith", Merchant = "merchant", Occultist = "occultist", Jeweler = "jeweler";
    public const string FirstQuest = "main_wastes", FinalQuest = "main_abyss";

    public static readonly string[] ActNames = { "", "第一幕", "第二幕", "第三幕", "第四幕", "第五幕", "第六幕", "第七幕" };

    public static string NpcName(string npcId) => npcId switch
    {
        Elder => "守燈長老 艾爾溫", Smith => "鐵匠 布蘭", Merchant => "行商 莉雅", Occultist => "祕術師 薇菈", Jeweler => "琢匠 歐丹",
        "villager" or "villager_m" or "villager_f" => "鎮民",
        _ => npcId ?? "",
    };

    public static string NpcShort(string npcId) => npcId switch
    {
        Elder => "艾爾溫", Smith => "布蘭", Merchant => "莉雅", Occultist => "薇菈", Jeweler => "歐丹",
        _ => NpcName(npcId),
    };

    public static readonly List<QuestDef> All = Build();
    private static readonly Dictionary<string, QuestDef> ById = All.ToDictionary(q => q.Id);
    public static QuestDef Get(string id) => id != null && ById.TryGetValue(id, out var q) ? q : null;

    private static QuestStep Talk(string npc, string text, string dialogue) =>
        new() { Event = "talk", Arg = npc, Text = text, Dialogue = dialogue, Area = ZoneDb.Town };
    private static QuestStep Go(string area, string text, string altBoss = null) =>
        new() { Event = "area", Arg = area, Text = text, Area = area, AltBoss = altBoss };
    private static QuestStep Hold(string region, string text) =>
        new() { Event = "stronghold", Arg = region, Text = text, Area = region };
    private static QuestStep Clear(string dungeon, string text, string altBoss = null) =>
        new() { Event = "dungeon", Arg = dungeon, Text = text, Area = dungeon, AltBoss = altBoss };
    private static QuestStep Slay(string boss, string area, string text) =>
        new() { Event = "boss", Arg = boss, Text = text, Area = area };
    private static QuestStep Tally(string evt, string region, int count, string text) =>
        new() { Event = evt, Region = region, Count = count, Text = text, Area = region };

    private static List<QuestDef> Build()
    {
        var l = new List<QuestDef>();

        void Story(string id, int act, string region, string name, string remind, string desc, float xp, int gold, int potion, string[] requires, params QuestStep[] steps)
        {
            var q = new QuestDef
            {
                Id = id, Main = true, Act = act, Region = region, Name = name, Remind = remind, Desc = desc,
                GiverId = Elder, Giver = NpcName(Elder), XpFrac = xp, Gold = gold, SkillPts = 1, PotionCap = potion,
                Requires = requires ?? Array.Empty<string>(),
            };
            q.Steps.AddRange(steps);
            l.Add(q);
        }

        void Side(string id, string region, string giver, string name, string desc, int gold, QuestStep goal)
        {
            var q = new QuestDef
            {
                Id = id, Region = region, Act = ZoneDb.Region(region)?.Act ?? 0, Name = name, Desc = desc,
                GiverId = giver, Giver = NpcName(giver), XpFrac = 0.35f, Gold = gold,
            };
            q.Steps.Add(goal);
            q.Steps.Add(Talk(giver, $"回殘燈鎮找{NpcShort(giver)}", id + "_done"));
            l.Add(q);
        }

        // ───────────────────────── main story ─────────────────────────

        Story("main_wastes", 1, "wastes", "將熄的火盆", "elder_remind_act1",
            "殘燈鎮的大火盆正在黯淡。守燈長老艾爾溫說，東邊斷燈哨站的燼塔被人掐熄了——動手的，是鎮上從前的守墓人葛魯姆。他正把自己埋下的人一個個挖出來，交給寂滅教團。",
            1.0f, 150, 1, null,
            Talk(Elder, "與守燈長老艾爾溫交談", "elder_first"),
            Go("wastes", "出鎮，前往灰燼荒原", "boss_gravedigger"),
            Hold("wastes", "奪回斷燈哨站，重燃荒原的燼塔"),
            Go("wastes_boss", "找到掘墓者的墓園", "boss_gravedigger"),
            Slay("boss_gravedigger", "wastes_boss", "讓掘墓者・葛魯姆停手"),
            Talk(Elder, "回殘燈鎮，向艾爾溫回報", "elder_act1_return"));

        Story("main_frost", 2, "frost", "不換哨的人", "elder_remind_keys",
            "霜之燼鑰在霜骸領主赫爾蒙手中。他凍死在哨位上，卻至今不肯離開那道關。一千個冬天，沒有人來換他的哨。",
            0.75f, 400, 0, new[] { "main_wastes" },
            Go("frost", "前往霜脊隘口", "boss_rimelord"),
            Hold("frost", "攻下凍骨關，重燃隘口的燼塔"),
            Go("frost_boss", "進入赫爾蒙的冰廳", "boss_rimelord"),
            Clear("frost_boss", "斬除冰廳中被標記的霜縛菁英", "boss_rimelord"),
            Slay("boss_rimelord", "frost_boss", "擊敗霜骸領主・赫爾蒙，取得霜之燼鑰"),
            Talk(Elder, "將霜之燼鑰帶回給艾爾溫", "elder_frost_return"));

        Story("main_crypt", 3, "crypt", "骨頭的佈道", "elder_remind_keys",
            "骨之燼鑰供在腐骨主教瑪爾薩的祭壇上。她對著沉眠墓原的遺骨講道，再把聽眾一一喚起。她說，骨頭不會痛。",
            0.75f, 400, 0, new[] { "main_wastes" },
            Go("crypt", "前往沉眠墓原", "boss_bishop"),
            Hold("crypt", "奪回守陵人營地，重燃墓原的燼塔"),
            Go("crypt_boss", "進入腐骨聖所", "boss_bishop"),
            Clear("crypt_boss", "斬除聖所中被標記的護教菁英", "boss_bishop"),
            Slay("boss_bishop", "crypt_boss", "擊敗腐骨主教・瑪爾薩，取得骨之燼鑰"),
            Talk(Elder, "將骨之燼鑰帶回給艾爾溫", "elder_crypt_return"));

        Story("main_mire", 4, "mire", "沼母的搖籃", "elder_remind_keys",
            "沼之燼鑰在沼母艾絲珂手裡。瘴泣沼澤裡的東西都喊她母親，而她不許任何一個孩子死透。",
            0.75f, 400, 0, new[] { "main_wastes" },
            Go("mire", "前往瘴泣沼澤", "boss_mire_mother"),
            Hold("mire", "奪回沉木寨，重燃沼澤的燼塔"),
            Go("mire_boss", "進入沼母之庭", "boss_mire_mother"),
            Clear("mire_boss", "在沼母之庭收集燼晶，獻於祭壇", "boss_mire_mother"),
            Slay("boss_mire_mother", "mire_boss", "擊敗沼母・艾絲珂，取得沼之燼鑰"),
            Talk(Elder, "將沼之燼鑰帶回給艾爾溫", "elder_mire_return"));

        Story("main_sands", 5, "sands", "黃銅的太陽", "elder_remind_act5",
            "三把燼鑰能打開鏽砂廢都的黃銅爐門。門後，末代君王伊薩克守著世上最後一份日爐殘圖——他曾想照著它，用黃銅再鑄一個太陽。",
            0.6f, 900, 1, new[] { "main_frost", "main_crypt", "main_mire" },
            Talk(Elder, "與艾爾溫商議三把燼鑰的用處", "elder_act5_brief"),
            Go("sands", "前往鏽砂廢都", "boss_sand_tyrant"),
            Hold("sands", "奪下斷柱商站，重燃廢都的燼塔"),
            Go("sands_boss", "以三把燼鑰開啟爐門，進入暴君王座", "boss_sand_tyrant"),
            Clear("sands_boss", "收集燼晶，喚醒王座前的祭壇", "boss_sand_tyrant"),
            Slay("boss_sand_tyrant", "sands_boss", "擊敗鏽砂暴君・伊薩克，取得日爐殘圖"),
            Talk(Elder, "把日爐殘圖帶回殘燈鎮", "elder_act5_return"));

        Story("main_sanctum", 6, "sanctum", "只響一聲的鐘", "elder_remind_act6",
            "寂滅聖城的鐘樓亮了燈。大導師賽倫要敲響喚醒寂主的鐘。那口鐘只需要響一聲。",
            0.6f, 1200, 0, new[] { "main_sands" },
            Go("sanctum", "前往寂滅聖城", "boss_grand_hush"),
            Hold("sanctum", "攻下燭匠街壘，重燃聖城的燼塔"),
            Go("sanctum_boss", "進入無聲大聖堂", "boss_grand_hush"),
            Clear("sanctum_boss", "摧毀大聖堂中的寂滅圖騰", "boss_grand_hush"),
            Slay("boss_grand_hush", "sanctum_boss", "阻止寂滅大導師・賽倫敲鐘"),
            Talk(Elder, "回殘燈鎮，把鐘聲的事告訴艾爾溫", "elder_act6_return"));

        Story("main_abyss", 7, "abyss", "最後一根柴", "elder_remind_act7",
            "鐘響之後，日爐底下裂開了燼淵，寂主奧薩洛斯在那裡翻了身。日爐殘圖說，爐只能從爐心點燃；爐芯的材料，是持燼者。",
            1.0f, 2000, 0, new[] { "main_sanctum" },
            Go("abyss", "降入燼淵", "boss_osaros"),
            Hold("abyss", "守住餘火前哨，點燃最後一座燼塔"),
            Go("abyss_boss", "抵達日爐之心", "boss_osaros"),
            Clear("abyss_boss", "解救被囚於日爐之心的持燼者", "boss_osaros"),
            Slay("boss_osaros", "abyss_boss", "終結寂主・奧薩洛斯"),
            Talk(Elder, "回到殘燈鎮的火邊", "elder_epilogue"));

        // ───────────────────────── side quests (start on the first visit to their region) ─────────────────────────

        Side("side_wastes_elites", "wastes", Smith, "哨站欠下的命",
            "布蘭的徒弟死在斷燈哨站。殺他的東西還在荒原上走動。布蘭不要頭顱，只要一個數目。",
            120, Tally("elite", "wastes", 6, "在灰燼荒原擊殺菁英怪物"));
        Side("side_wastes_cellar", "wastes", Merchant, "地窖裡的貨",
            "莉雅在荒棄地窖裡藏過一批貨。如今地窖裡多了些不付錢的住客。",
            120, Clear("wastes_d1", "肅清荒棄地窖"));

        Side("side_frost_altars", "frost", Occultist, "雪下的火",
            "薇菈說，隘口的積雪底下埋著古老的燼火祭壇，一千年沒人添過柴。她想知道它們是否還燒著。",
            300, Tally("altar", "frost", 2, "在霜脊隘口尋找燼火祭壇"));
        Side("side_frost_post", "frost", Elder, "凍在哨站裡的人",
            "艾爾溫的冊子上，有幾個名字最後的去向是冰封哨站。他想知道，該不該把它們劃掉。",
            300, Clear("frost_d1", "解救冰封哨站中的倖存者"));

        Side("side_crypt_tomb", "crypt", Jeweler, "怕吵的人",
            "歐丹的妻子葬在無名陵寢。教團在那裡立了圖騰，日夜低誦。她生前最怕吵。",
            300, Clear("crypt_d1", "摧毀無名陵寢中的寂滅圖騰"));
        Side("side_crypt_elites", "crypt", Smith, "打歪了心的鐵",
            "教團護教者的鎧甲用的是好鐵，只是打歪了心。布蘭想把它們熔了重打。",
            300, Tally("elite", "crypt", 8, "在沉眠墓原擊殺菁英怪物"));

        Side("side_mire_events", "mire", Merchant, "沼路上的求救聲",
            "莉雅的同行走沼路進貨，三隊人只回來半隊。沼裡出事的時候，總得有人搭把手。",
            300, Tally("event", "mire", 2, "在瘴泣沼澤完成區域事件"));
        Side("side_mire_lair", "mire", Occultist, "沼巫的藥櫃",
            "沼巫的方子裡有毒，也有藥。薇菈想趁巢穴空著，把能救人的那一半抄下來。",
            300, Clear("mire_d2", "摧毀沼巫巢穴中的寂滅圖騰"));

        Side("side_sands_workshop", "sands", Smith, "師父的師父",
            "布蘭這一脈的手藝，傳自鏽砂廢都的黃銅工坊。那裡的爐子，至今還在替一個死去的王打造守衛。",
            600, Clear("sands_d1", "摧毀黃銅工坊中的寂滅圖騰"));
        Side("side_sands_altars", "sands", Jeweler, "沙裡的暖石",
            "歐丹說，真正的好石頭都在火邊長大。廢都的沙底下還有燒著的祭壇——替他去摸一摸。",
            600, Tally("altar", "sands", 2, "在鏽砂廢都尋找燼火祭壇"));

        Side("side_sanctum_cloister", "sanctum", Merchant, "修院裡的啞者",
            "聖城的靜默修院收容「自願噤聲的人」。莉雅有個妹妹，三年前進去之後，再沒寄過信。",
            750, Clear("sanctum_d1", "解救靜默修院中的倖存者"));
        Side("side_sanctum_elites", "sanctum", Occultist, "同一個匠人",
            "薇菈要十張聖城菁英的面具，不肯說用途。她只說，她認得做面具的那個匠人。",
            750, Tally("elite", "sanctum", 10, "在寂滅聖城擊殺菁英怪物"));

        Side("side_abyss_forge", "abyss", Smith, "舊爐膛",
            "熔爐遺構是日爐最老的一段爐膛。每個打鐵的人，都想知道那口爐子從前有多熱。",
            900, Clear("abyss_d1", "摧毀熔爐遺構中的寂滅圖騰"));
        Side("side_abyss_events", "abyss", Elder, "替他們守夜",
            "餘火前哨的人已經很久沒合過眼。燼淵裡出事的時候，替他們擋一擋。",
            900, Tally("event", "abyss", 3, "在燼淵完成區域事件"));

        return l;
    }
}

/// <summary>
/// Campaign state machine, side quests, regional renown and act gating. Listens to <see cref="World.Event"/> and keeps
/// everything in the save: D.Quests (quest id → step index, <see cref="Done"/> when finished; absent = not started),
/// D.Flags ("q_…" facts: areas seen, strongholds, dungeons, bosses, altars, renown tiers, step counters) and
/// D.Renown (region id → points).
///
/// What it expects from the rest of the game:
/// · World.Emit("area", areaId) on every level load · ("stronghold", regionId) when a stronghold is liberated ·
///   ("dungeon", dungeonId) when a dungeon's objective is completed · ("boss", bossDefId) / ("elite", defId) on kills ·
///   ("altar", altarId) the first time an altar is used · ("event", regionId) when a region event is won ·
///   ("rift", tier) when a rift is cleared · ("waypoint", areaId) · ("levelup", level).
/// · An NPC that is clicked calls <see cref="TalkTo"/> with its id (elder, smith, merchant, occultist, jeweler,
///   villager): a returned script is opened in the dialogue panel; null means "open the service" (vendors) or nothing
///   (villagers) — their greeting or murmur has already been shown as a subtitle. <see cref="HasQuestFor"/> drives the
///   marker above the NPC. The "talk" event itself is ignored here, so emitting it as well is harmless.
/// </summary>
public static class Quests
{
    public const int Done = -1, NotStarted = -2;
    public const int RenownStronghold = 100, RenownBoss = 150, RenownDungeon = 60, RenownAltar = 20, RenownEvent = 30, RenownSideQuest = 40;
    public const int RenownEventCap = 10;            // events that still earn renown in one region
    public const int MaxPotionCap = 9;
    public const int RiftForTopTier = 20;
    public static readonly int[] RenownThresholds = { 100, 250, 450, 700 };
    public static readonly string[] RenownTierNames = { "陌路", "初識", "受敬", "倚重", "傳頌" };
    public const string FlagCampaign = "q_campaign";

    // Flags written by this class. Other modules read some of them, so their shape is fixed:
    //   q_sh_<regionId> stronghold liberated · q_dg_<dungeonId> dungeon cleared · q_boss_<bossDefId> story boss slain ·
    //   q_alt_<regionId>|<area>:<altarId> altar found · q_ev_<regionId>_<n> region event that earned renown (n = 1…cap) ·
    //   q_rn_<regionId>_<tier> renown tier granted · q_area_<areaId> visited. Step counters are "q_n_<questId>=<value>".
    private const string FArea = "q_area_", FHold = "q_sh_", FDungeon = "q_dg_", FBoss = "q_boss_", FAltar = "q_alt_", FEvent = "q_ev_", FRenown = "q_rn_", FCount = "q_n_";
    private const string FlagEnding = "q_ending";
    private const int TrackerMax = 8;

    /// <summary>What the hero carries away from each story boss, announced on the first kill.</summary>
    private static readonly Dictionary<string, string> Spoils = new()
    {
        ["boss_gravedigger"] = "取得：刻著三把鑰匙的教團鑰印",
        ["boss_rimelord"] = "取得：霜之燼鑰",
        ["boss_bishop"] = "取得：骨之燼鑰",
        ["boss_mire_mother"] = "取得：沼之燼鑰",
        ["boss_sand_tyrant"] = "取得：日爐殘圖",
        ["boss_grand_hush"] = "鐘響了。只有一聲。",
        ["boss_osaros"] = "寂主已逝。爐心在等你。",
    };

    /// <summary>Raised after quest, renown or tracker state changed (never per frame).</summary>
    public static event Action Changed;

    private static Hero _h;
    private static string _area = ZoneDb.Town;
    private static RegionDef _region;
    private static int _depth;
    private static bool _save, _notify, _silent;
    private static string _saveReason;
    private static readonly List<string> _tracker = new();
    private static readonly IReadOnlyList<string> NoLines = Array.Empty<string>();
    private static bool _trackerDirty = true;
    private static readonly List<QuestDef> _started = new(), _updated = new();
    private static bool _endingPending;
    private static int _endingTries, _endingRequests;
    private static string _greetNpc;
    private static long _greetAt;
    private static readonly HashSet<string> _warned = new();

    // ───────────────────────── lifecycle ─────────────────────────

    /// <summary>Binds the quest system to a loaded character. Safe to call again for another hero.</summary>
    public static void Attach(Hero h)
    {
        Detach();
        if (h == null) return;
        _h = h;
        var d = h.D;
        d.Quests ??= new(); d.Flags ??= new(); d.Renown ??= new(); d.Waypoints ??= new() { ZoneDb.Town };
        _area = string.IsNullOrEmpty(d.Area) ? ZoneDb.Town : d.Area;
        _region = FindRegion(_area);
        World.Event += OnEvent;

        _silent = true;
        Begin();
        try
        {
            if (d.MaxTier < 1) d.MaxTier = 1;
            if (d.Has(FBoss + FinalBoss)) d.Set(FlagCampaign);
            SyncTiers();
            Evaluate();
        }
        catch (Exception e) { Log.Error("quest", "attach failed", e); }
        finally { End(); _silent = false; }

        StoryVoice.Attach(h);
        Log.Info("quest", $"attached: {d.Name} area={_area} {Summary()}");
    }

    public static void Detach()
    {
        World.Event -= OnEvent;
        StoryVoice.Detach();
        if (_h != null) Log.Info("quest", "detached");
        _h = null; _region = null; _area = ZoneDb.Town;
        _depth = 0; _save = _notify = _silent = false; _saveReason = null;
        _started.Clear(); _updated.Clear(); _tracker.Clear(); _trackerDirty = true;
        _endingPending = false; _endingTries = 0;
        _greetNpc = null;
    }

    // ───────────────────────── queries ─────────────────────────

    private static string FinalBoss
    {
        get
        {
            RegionDef last = null;
            foreach (var r in ZoneDb.Regions) if (last == null || r.Act > last.Act) last = r;
            return last?.Boss ?? "boss_osaros";
        }
    }

    private static RegionDef FindRegion(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) return null;
        var regions = ZoneDb.Regions;
        for (int i = 0; i < regions.Count; i++)
        {
            var r = regions[i];
            if (r.Id == areaId) return r;
            var ds = r.Dungeons;
            if (ds == null) continue;
            for (int j = 0; j < ds.Length; j++) if (ds[j].Id == areaId) return r;
        }
        return null;
    }

    private static RegionDef RegionOfBoss(string bossId)
    {
        foreach (var r in ZoneDb.Regions) if (r.Boss == bossId) return r;
        return null;
    }

    private static int Stage(QuestDef q)
    {
        if (_h == null || q == null || !_h.D.Quests.TryGetValue(q.Id, out int st)) return NotStarted;
        if (st == Done || st >= q.Steps.Count) return Done;
        return st < 0 ? NotStarted : st;
    }

    public static bool IsActive(QuestDef q) => Stage(q) >= 0;
    public static bool IsDone(QuestDef q) => Stage(q) == Done;
    /// <summary>Index of the current step; <see cref="Done"/> (-1) when finished, <see cref="NotStarted"/> (-2) before it begins.</summary>
    public static int StepIndex(QuestDef q) => Stage(q);
    public static QuestStep CurrentStep(QuestDef q) { int st = Stage(q); return st >= 0 ? q.Steps[st] : null; }
    public static bool CampaignDone => _h != null && _h.D.Has(FlagCampaign);
    /// <summary>How many of the three 燼鑰 the hero holds (0-3).</summary>
    public static int Keys
    {
        get
        {
            if (_h == null) return 0;
            int n = 0;
            foreach (var r in ZoneDb.Regions) if (r.Act == 2 && _h.D.Has(FBoss + r.Boss)) n++;
            return n;
        }
    }

    public static IEnumerable<QuestDef> Active()
    {
        if (_h == null) yield break;
        foreach (var q in QuestDb.All) if (Stage(q) >= 0) yield return q;
    }

    public static IEnumerable<QuestDef> Completed()
    {
        if (_h == null) yield break;
        foreach (var q in QuestDb.All) if (Stage(q) == Done) yield return q;
    }

    /// <summary>Progress of a counted step (elites slain, altars found, events finished).</summary>
    public static int Progress(QuestDef q, QuestStep s)
    {
        if (_h == null || q == null || s == null) return 0;
        if (s.Event == "altar") return CountFlags(FAltar + s.Region + "|");
        return Counter(FCount + q.Id);
    }

    /// <summary>Step text with its counter, e.g. "在灰燼荒原擊殺菁英怪物（3/6）".</summary>
    public static string StepText(QuestDef q, int index)
    {
        if (q == null || index < 0 || index >= q.Steps.Count) return "";
        var s = q.Steps[index];
        if (s.Count <= 1) return s.Text;
        int st = Stage(q);
        int n = st == Done || (st >= 0 && index < st) ? s.Count : st == index ? Math.Min(Progress(q, s), s.Count) : 0;
        return $"{s.Text}（{n}/{s.Count}）";
    }

    public static string ActLabel(QuestDef q) =>
        q == null ? "" : q.Main ? QuestDb.ActNames[Math.Clamp(q.Act, 0, QuestDb.ActNames.Length - 1)] : "委託";

    /// <summary>Experience a quest would grant right now (scales with the hero's level).</summary>
    public static long XpReward(QuestDef q) => q == null ? 0 : XpFor(q.XpFrac);

    public static string RewardText(QuestDef q)
    {
        if (q == null) return "";
        var parts = new List<string>(4);
        long xp = XpReward(q);
        if (xp > 0) parts.Add($"經驗 {xp:N0}");
        if (q.Gold > 0) parts.Add($"金幣 {q.Gold:N0}");
        if (q.SkillPts > 0) parts.Add($"技能點 +{q.SkillPts}");
        if (q.PotionCap > 0) parts.Add($"藥水上限 +{q.PotionCap}");
        if (!q.Main) parts.Add($"名望 +{RenownSideQuest}");
        return string.Join("　", parts);
    }

    /// <summary>Accepts "elder" as well as "npc_elder" (the model name) for the town's people.</summary>
    private static string NpcKey(string npcId) =>
        npcId != null && npcId.StartsWith("npc_", StringComparison.Ordinal) ? npcId.Substring(4) : npcId;

    public static bool HasQuestFor(string npcId)
    {
        if (_h == null || string.IsNullOrEmpty(npcId)) return false;
        npcId = NpcKey(npcId);
        var all = QuestDb.All;
        for (int i = 0; i < all.Count; i++)
        {
            int st = Stage(all[i]);
            if (st < 0) continue;
            var s = all[i].Steps[st];
            if (s.Event == "talk" && s.Arg == npcId) return true;
        }
        return false;
    }

    /// <summary>The main quest the tracker and the map follow: the one set in the region the hero stands in, else the earliest act.</summary>
    private static QuestDef FocusMain()
    {
        QuestDef first = null;
        var all = QuestDb.All;
        for (int i = 0; i < all.Count; i++)
        {
            var q = all[i];
            if (!q.Main || Stage(q) < 0) continue;
            if (_region != null && q.Region == _region.Id) return q;
            first ??= q;
        }
        return first;
    }

    /// <summary>Area id the map should mark: "town", a region or a dungeon. Null when the campaign has nothing pending.</summary>
    public static string NextObjectiveArea()
    {
        if (_h == null) return null;
        var q = FocusMain();
        if (q == null) return null;
        var s = q.Steps[Stage(q)];
        return s.Area ?? (s.Event == "talk" ? ZoneDb.Town : q.Region);
    }

    public static IReadOnlyList<string> Tracker()
    {
        if (_h == null) return NoLines;
        if (_trackerDirty) RebuildTracker();
        return _tracker;
    }

    private static void RebuildTracker()
    {
        _tracker.Clear();
        _trackerDirty = false;
        var all = QuestDb.All;
        int mains = 0;
        foreach (var q in all) if (q.Main && Stage(q) >= 0) mains++;
        var focus = FocusMain();
        if (focus != null)
        {
            bool here = _region != null && focus.Region == _region.Id;
            if (mains == 1 || here)
            {
                _tracker.Add($"◆ {ActLabel(focus)}　{focus.Name}");
                _tracker.Add("　" + StepText(focus, Stage(focus)));
            }
            else
            {
                foreach (var q in all)
                    if (q.Main && Stage(q) >= 0) _tracker.Add($"◆ {q.Name}：{StepText(q, Stage(q))}");
            }
        }
        else if (_h.D.Has(FlagCampaign)) _tracker.Add("◆ 日爐已燃。深淵裂隙仍在滲出寂靜。");

        bool inTown = _area == ZoneDb.Town;
        foreach (var q in all)
        {
            if (q.Main) continue;
            int st = Stage(q);
            if (st < 0) continue;
            var s = q.Steps[st];
            bool show = (_region != null && q.Region == _region.Id) || (inTown && s.Event == "talk");
            if (!show) continue;
            if (_tracker.Count >= TrackerMax) break;
            _tracker.Add($"◇ {q.Name}：{StepText(q, st)}");
        }
    }

    // ───────────────────────── act gating ─────────────────────────

    private static int PrevAct(int act)
    {
        int best = 0;
        foreach (var r in ZoneDb.Regions) if (r.Act < act && r.Act > best) best = r.Act;
        return best;
    }

    /// <summary>A region opens when every story boss of the act before it is dead (wastes → frost / crypt / mire → sands → sanctum → abyss).</summary>
    public static bool RegionOpen(string regionId)
    {
        var r = FindRegion(regionId);
        if (r == null || _h == null) return true;
        int prev = PrevAct(r.Act);
        if (prev <= 0) return true;
        var d = _h.D;
        foreach (var o in ZoneDb.Regions) if (o.Act == prev && !d.Has(FBoss + o.Boss)) return false;
        return true;
    }

    /// <summary>Act gating for travel. Dungeons follow their region; rifts need the finished campaign.</summary>
    public static bool CanEnter(string areaId)
    {
        if (string.IsNullOrEmpty(areaId) || areaId == ZoneDb.Town || _h == null) return true;
        if (NamedAreas.IsNamedArea(areaId)) return true;      // NAMED-HOOK: world-boss fields and the Ember Stair gate themselves
        if (areaId.StartsWith("rift", StringComparison.Ordinal)) return _h.D.Has(FlagCampaign);
        var r = FindRegion(areaId);
        if (r == null)
        {
            if (_warned.Add("area:" + areaId)) Log.Warn("quest", $"CanEnter: unknown area '{areaId}', allowing");
            return true;
        }
        return RegionOpen(r.Id);
    }

    /// <summary>Why an area is still closed, for the map tooltip ("" when it is open).</summary>
    public static string WhyLocked(string areaId)
    {
        if (CanEnter(areaId)) return "";
        if (areaId.StartsWith("rift", StringComparison.Ordinal)) return "終結寂主之後，深淵裂隙才會開啟";
        var r = FindRegion(areaId);
        return r == null ? "尚未開放" : r.Act switch
        {
            2 => "需先讓掘墓者・葛魯姆停手",
            5 => $"需集齊三把燼鑰（{Keys}/3）",
            6 => "需先取得日爐殘圖",
            7 => "需先踏過寂滅聖城的鐘聲",
            _ => "尚未開放",
        };
    }

    // ───────────────────────── renown ─────────────────────────

    public static int RenownPoints(string regionId) =>
        _h == null || regionId == null ? 0 : _h.D.Renown.GetValueOrDefault(regionId);

    public static int RenownTier(string regionId)
    {
        int pts = RenownPoints(regionId), t = 0;
        while (t < RenownThresholds.Length && pts >= RenownThresholds[t]) t++;
        return t;
    }

    /// <summary>燼火祭壇 found in a region so far.</summary>
    public static int AltarsFound(string regionId) => _h == null || regionId == null ? 0 : CountFlags(FAltar + regionId + "|");
    /// <summary>Region events that earned renown there so far (at most <see cref="RenownEventCap"/>).</summary>
    public static int EventsDone(string regionId) => _h == null || regionId == null ? 0 : CountFlags(FEvent + regionId + "_");

    /// <summary>Points needed for the next tier, or -1 at the top tier.</summary>
    public static int RenownNext(string regionId)
    {
        int t = RenownTier(regionId);
        return t >= RenownThresholds.Length ? -1 : RenownThresholds[t];
    }

    /// <summary>What a renown tier (1-4) grants, for the map / quest log.</summary>
    public static string RenownRewardText(int tier) => tier switch
    {
        1 => "金幣與經驗",
        2 => "技能點 +1",
        3 => "藥水上限 +1",
        4 => "燼紋點數 +4",
        _ => "",
    };

    private static int ActScale(RegionDef r) => r.Act <= 1 ? 1 : r.Act <= 4 ? 2 : r.Act - 1;

    private static void AddRenown(RegionDef r, int pts, string why)
    {
        if (r == null || pts <= 0 || _h == null) return;
        var d = _h.D;
        int before = d.Renown.GetValueOrDefault(r.Id), after = before + pts;
        d.Renown[r.Id] = after;
        Log.Info("renown", $"{r.Id} +{pts} ({why}) → {after}");
        Toast($"{r.Name}　名望 +{pts}（{why}）", Palette.Ember);
        for (int t = 1; t <= RenownThresholds.Length; t++)
            if (after >= RenownThresholds[t - 1] && d.Set(FRenown + r.Id + "_" + t)) GrantRenownTier(r, t);
        MarkSave("renown");
    }

    private static void GrantRenownTier(RegionDef r, int tier)
    {
        var d = _h.D;
        int gold = 120 * ActScale(r);
        float xpFrac = 0f;
        string what;
        switch (tier)
        {
            case 1:
                xpFrac = 0.3f;
                what = $"金幣 {gold:N0}、經驗 {XpFor(xpFrac):N0}";
                break;
            case 2:
                d.BonusSkillPts += 1;
                what = $"技能點 +1、金幣 {gold:N0}";
                break;
            case 3:
                if (d.PotionCap < MaxPotionCap) { d.PotionCap++; what = $"藥水上限 +1、金幣 {gold:N0}"; }
                else { gold *= 3; what = $"金幣 {gold:N0}"; }
                break;
            default:
                d.BonusParagonPts += 4;
                gold *= 2;
                what = $"燼紋點數 +4、金幣 {gold:N0}";
                break;
        }
        d.Gold += gold;
        string name = RenownTierNames[Math.Clamp(tier, 0, RenownTierNames.Length - 1)];
        Log.Info("renown", $"{r.Id} tier {tier} granted: {what} (skillpts={d.BonusSkillPts} potions={d.PotionCap} paragonpts={d.BonusParagonPts})");
        Toast($"{r.Name}名望「{name}」：{what}", Palette.Gold);
        if (xpFrac > 0f) GiveXp(xpFrac);
        _h.Notify();
    }

    // ───────────────────────── rewards ─────────────────────────

    private static long XpFor(float frac)
    {
        if (_h == null || frac <= 0f) return 0;
        var d = _h.D;
        long need = d.Level < Balance.LevelCap ? Balance.XpToNext(d.Level) : Balance.ParagonXp(d.Paragon);
        return Math.Max(1L, (long)MathF.Round(need * frac));
    }

    private static void GiveXp(float frac)
    {
        long xp = XpFor(frac);
        if (xp > 0) _h.AddXp(xp);       // may level the hero up, which re-enters OnEvent("levelup"): harmless
    }

    private static void Complete(QuestDef q)
    {
        var d = _h.D;
        long xp = XpReward(q);
        if (q.Gold > 0) d.Gold += q.Gold;
        if (q.SkillPts > 0) d.BonusSkillPts += q.SkillPts;
        int potions = 0;
        if (q.PotionCap > 0)
        {
            potions = Math.Min(MaxPotionCap, d.PotionCap + q.PotionCap) - d.PotionCap;
            if (potions > 0) d.PotionCap += potions;
        }
        Log.Info("quest", $"{q.Id} complete: xp={xp} gold={q.Gold} skillpts=+{q.SkillPts} potions=+{potions} (cap {d.PotionCap})");

        if (!_silent)
        {
            var parts = new List<string>(4);
            if (xp > 0) parts.Add($"經驗 {xp:N0}");
            if (q.Gold > 0) parts.Add($"金幣 {q.Gold:N0}");
            if (q.SkillPts > 0) parts.Add($"技能點 +{q.SkillPts}");
            if (potions > 0) parts.Add($"藥水上限 +{potions}");
            Toast((q.Main ? "任務完成：" : "委託完成：") + q.Name, Palette.Gold);
            if (parts.Count > 0) Toast("獎勵：" + string.Join("　", parts), Palette.Good);
        }
        if (!q.Main) AddRenown(FindRegion(q.Region), RenownSideQuest, "委託");
        _h.Notify();
        if (xp > 0) _h.AddXp(xp);
    }

    // ───────────────────────── state machine ─────────────────────────

    private static void Begin() => _depth++;

    private static void End()
    {
        if (_depth > 0) _depth--;
        if (_depth > 0) return;
        if (_h == null) { _save = _notify = false; _saveReason = null; _started.Clear(); _updated.Clear(); return; }
        FlushNotices();
        bool save = _save, notify = _notify || _save;
        string reason = _saveReason ?? "quest";
        _save = _notify = false; _saveReason = null;
        if (!notify) return;
        _trackerDirty = true;
        RaiseChanged();
        if (!save || _silent) return;
        try { Game.I?.SaveNow(reason); }
        catch (Exception e) { Log.Error("quest", "autosave failed", e); }
    }

    private static void MarkSave(string reason) { _save = true; _saveReason ??= reason; }

    private static void RaiseChanged()
    {
        var c = Changed;
        if (c == null) return;
        foreach (var handler in c.GetInvocationList())
        {
            try { ((Action)handler)(); }
            catch (Exception e) { Log.Error("quest", "Changed handler failed", e); }
        }
    }

    // [fixes] quest toasts raised by a conversation (TalkTo) or while one is on screen wait until it closes, so
    // 「目標更新：…」 is not shown before the line that gives the objective has been read
    private static bool _holdToasts;
    private static readonly List<(string text, Color c)> _heldToasts = new();

    /// <summary>Shows the quest toasts held back while a dialogue was up (DialogueUi calls this when it closes).</summary>
    public static void ReleaseHeldToasts()
    {
        if (_heldToasts.Count == 0) return;
        var held = _heldToasts.ToArray();
        _heldToasts.Clear();
        Log.Info("quest", $"{held.Length} toast(s) shown after the dialogue");
        foreach (var (t, c) in held) Toast(t, c);
    }

    private static void Toast(string text, Color c)
    {
        if (_silent || string.IsNullOrEmpty(text)) return;
        if (_holdToasts || DialogueUi.Talking) { _heldToasts.Add((text, c)); return; }
        try { Game.I?.Hud?.Toast(text, c); }
        catch (Exception e) { Log.Error("quest", "toast failed", e); }
    }

    private static void FlushNotices()
    {
        if (!_silent)
        {
            string mains = null, sides = null;
            foreach (var q in _started)
            {
                if (Stage(q) < 0) continue;      // started and finished in the same breath
                if (q.Main) mains = mains == null ? q.Name : mains + "、" + q.Name;
                else sides = sides == null ? q.Name : sides + "、" + q.Name;
            }
            if (mains != null) Toast("新任務：" + mains, Palette.Gold);
            if (sides != null) Toast("新委託：" + sides, Palette.Text);
            foreach (var q in _updated)
            {
                int st = Stage(q);
                if (st < 0 || _started.Contains(q)) continue;
                if (q.Main) Toast("目標更新：" + StepText(q, st), Palette.Text);
                else if (q.Steps[st].Event == "talk") Toast($"委託達成：{q.Name}——回鎮上找{QuestDb.NpcShort(q.GiverId)}", Palette.Gold);
            }
        }
        _started.Clear();
        _updated.Clear();
    }

    private static bool CanStart(QuestDef q)
    {
        var d = _h.D;
        if (!q.Main) return q.Region != null && d.Has(FArea + q.Region);
        foreach (var req in q.Requires)
        {
            var r = QuestDb.Get(req);
            if (r != null && Stage(r) != Done) return false;
        }
        return true;
    }

    private static bool Satisfied(QuestDef q, QuestStep s)
    {
        var d = _h.D;
        switch (s.Event)
        {
            case "talk": return false;                          // only TalkTo finishes these
            case "area": return d.Has(FArea + s.Arg) || (s.AltBoss != null && d.Has(FBoss + s.AltBoss));
            case "stronghold": return d.Has(FHold + s.Arg);
            case "dungeon": return d.Has(FDungeon + s.Arg) || (s.AltBoss != null && d.Has(FBoss + s.AltBoss));
            case "boss": return d.Has(FBoss + s.Arg);
            default: return Progress(q, s) >= s.Count;
        }
    }

    /// <summary>Finishes the current step of an active quest. Returns the new stage.</summary>
    private static int Advance(QuestDef q)
    {
        var d = _h.D;
        int st = Stage(q);
        if (st < 0) return st;
        var was = q.Steps[st];
        if (was.Count > 1) SetCounter(FCount + q.Id, 0);
        MarkSave("quest");
        if (!_updated.Contains(q)) _updated.Add(q);
        if (++st >= q.Steps.Count)
        {
            d.Quests[q.Id] = Done;
            Log.Info("quest", $"{q.Id} step {st - 1} done ({was.Event}:{was.Arg ?? was.Region}) → finished");
            Complete(q);
            return Done;
        }
        d.Quests[q.Id] = st;
        Log.Info("quest", $"{q.Id} step {st - 1} done ({was.Event}:{was.Arg ?? was.Region}) → step {st}/{q.Steps.Count - 1}: {q.Steps[st].Text}");
        return st;
    }

    /// <summary>Starts every quest whose conditions are met and walks active quests past steps that are already satisfied.</summary>
    private static void Evaluate()
    {
        if (_h == null) return;
        var d = _h.D;
        bool progressed;
        int guard = 0;
        do
        {
            progressed = false;
            foreach (var q in QuestDb.All)
            {
                int st = Stage(q);
                if (st == Done) continue;
                if (st == NotStarted)
                {
                    if (!CanStart(q)) continue;
                    d.Quests[q.Id] = st = 0;
                    _started.Add(q);
                    MarkSave("quest");
                    progressed = true;
                    Log.Info("quest", $"{q.Id} started ({q.Name}): {q.Steps[0].Text}");
                }
                while (st >= 0 && Satisfied(q, q.Steps[st]))
                {
                    st = Advance(q);
                    progressed = true;
                    if (_h == null) return;
                }
            }
        } while (progressed && ++guard < 32);
    }

    private static int CountFlags(string prefix)
    {
        int n = 0;
        var flags = _h.D.Flags;
        for (int i = 0; i < flags.Count; i++)
            if (flags[i] != null && flags[i].StartsWith(prefix, StringComparison.Ordinal)) n++;
        return n;
    }

    /// <summary>Reads a counter kept in the flag list as "name=value" (0 when absent).</summary>
    private static int Counter(string name)
    {
        var flags = _h.D.Flags;
        for (int i = 0; i < flags.Count; i++)
        {
            string f = flags[i];
            if (f == null || f.Length <= name.Length + 1 || f[name.Length] != '=' || !f.StartsWith(name, StringComparison.Ordinal)) continue;
            if (int.TryParse(f.AsSpan(name.Length + 1), out int v)) return v;
        }
        return 0;
    }

    private static void SetCounter(string name, int value)
    {
        var flags = _h.D.Flags;
        for (int i = flags.Count - 1; i >= 0; i--)
        {
            string f = flags[i];
            if (f != null && f.Length > name.Length && f[name.Length] == '=' && f.StartsWith(name, StringComparison.Ordinal)) flags.RemoveAt(i);
        }
        if (value > 0) flags.Add(name + "=" + value);
    }

    /// <summary>Adds one to every active counted step of this kind in the region. Returns true when something moved.</summary>
    private static bool CountStep(string evt, string regionId)
    {
        bool any = false;
        foreach (var q in QuestDb.All)
        {
            int st = Stage(q);
            if (st < 0) continue;
            var s = q.Steps[st];
            if (s.Event != evt || s.Region != regionId) continue;
            int n = Counter(FCount + q.Id);
            if (n >= s.Count) continue;
            SetCounter(FCount + q.Id, ++n);
            any = true;
            _notify = true;
            Log.Debug("quest", $"{q.Id} {evt} {n}/{s.Count}");
        }
        return any;
    }

    private static void AddWaypoint(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) return;
        var d = _h.D;
        if (d.Waypoints.Contains(areaId)) return;
        d.Waypoints.Add(areaId);
        Log.Info("quest", $"waypoint unlocked: {areaId}");
        MarkSave("waypoint");
    }

    private static void SyncTiers()
    {
        var d = _h.D;
        int want = d.RiftBest >= RiftForTopTier ? 3 : d.Has(FlagCampaign) ? 2 : 1;
        want = Math.Min(want, ZoneDb.TierNames.Length - 1);
        if (d.MaxTier >= want) return;
        int from = d.MaxTier;
        d.MaxTier = want;
        Log.Info("quest", $"world tier cap {from} → {want}");
        for (int t = Math.Max(from + 1, 2); t <= want; t++) Toast($"世界階級「{ZoneDb.TierNames[t]}」已開啟", Palette.Ember);
        MarkSave("tier");
    }

    private static void FinishCampaign()
    {
        var d = _h.D;
        if (!d.Set(FlagCampaign)) return;
        Log.Info("quest", $"campaign finished: {d.Name} lv{d.Level} playtime={d.PlayTime:F0}s deaths={d.Deaths}");
        SyncTiers();
        Toast("深淵裂隙已開啟", Palette.Ember);
        MarkSave("campaign");
        if (!_silent && !_endingPending && !d.Has(FlagEnding))
        {
            _endingRequests = DialogueDb.Requests("ending");
            _endingTries = 0;
            _endingPending = After(7.0, TryEnding);
        }
    }

    // ───────────────────────── world events ─────────────────────────

    private static void OnEvent(string evt, string arg)
    {
        if (_h == null) return;
        switch (evt)
        {
            case "area": case "stronghold": case "boss": case "dungeon": case "altar": case "event": case "elite":
            case "rift": case "waypoint": case "levelup":
                break;
            default:
                return;         // kill, miniboss, talk, death, objective …: nothing to track, keep the hot path free
        }
        Begin();
        try
        {
            if (Handle(evt, arg ?? "")) Evaluate();
        }
        catch (Exception e) { Log.Error("quest", $"handling {evt}({arg}) failed", e); }
        finally { End(); }
    }

    /// <summary>Records what happened. Returns true when quest steps should be re-checked.</summary>
    private static bool Handle(string evt, string arg)
    {
        var d = _h.D;
        switch (evt)
        {
            case "area":
            {
                if (arg.Length == 0) return false;
                _area = arg;
                _region = FindRegion(arg);
                StoryVoice.ForgetBoss();
                bool fresh = d.Set(FArea + arg);
                if (_region != null && d.Set(FArea + _region.Id)) fresh = true;
                if (fresh) Log.Info("quest", $"first visit: {arg}");
                _notify = true;
                return true;
            }
            case "stronghold":
            {
                var r = FindRegion(arg) ?? _region;
                if (r == null) { Log.Warn("quest", $"stronghold event without a region: '{arg}'"); return false; }
                if (!d.Set(FHold + r.Id)) return false;
                Log.Info("quest", $"stronghold liberated: {r.Id} ({r.Stronghold})");
                AddWaypoint(r.Id);
                AddRenown(r, RenownStronghold, r.Stronghold);
                MarkSave("stronghold");
                return true;
            }
            case "boss":
            {
                string id = arg;
                var r = RegionOfBoss(id);
                if (r == null && !id.StartsWith("boss_", StringComparison.Ordinal)) { id = "boss_" + id; r = RegionOfBoss(id); }
                if (r == null) return false;                    // not a story boss
                StoryVoice.BossDied(id);                        // last words, every time
                if (!d.Set(FBoss + id)) return false;           // killed before
                Log.Info("quest", $"story boss slain: {id} region={r.Id} keys={Keys}");
                if (Spoils.TryGetValue(id, out var spoil)) Toast(r.Act == 2 ? $"{spoil}（{Keys}/3）" : spoil, Palette.Gold);
                AddRenown(r, RenownBoss, MonsterDb.Get(id)?.Name ?? r.Name);
                if (id == FinalBoss) FinishCampaign();
                MarkSave("boss");
                return true;
            }
            case "dungeon":
            {
                var r = FindRegion(arg);
                DungeonDef dd = null;
                if (r?.Dungeons != null) foreach (var x in r.Dungeons) if (x.Id == arg) { dd = x; break; }
                if (dd == null) return false;                   // rifts and anything that is not a campaign dungeon
                if (!d.Set(FDungeon + dd.Id)) return false;
                Log.Info("quest", $"dungeon cleared for the first time: {dd.Id} ({dd.Name}) story={dd.Story}");
                if (!dd.Story) AddRenown(r, RenownDungeon, dd.Name);
                MarkSave("dungeon");
                return true;
            }
            case "altar":
            {
                var r = _region;
                if (r == null) return false;
                string key = FAltar + r.Id + "|" + _area + ":" + (arg.Length > 0 ? arg : "n" + CountFlags(FAltar + r.Id + "|"));
                if (!d.Set(key)) return false;
                Log.Info("quest", $"altar found: {arg} in {_area} ({CountFlags(FAltar + r.Id + "|")} in {r.Id})");
                AddRenown(r, RenownAltar, "燼火祭壇");
                _notify = true;
                return true;
            }
            case "event":
            {
                var r = FindRegion(arg) ?? _region;
                if (r == null) return false;
                // one flag per paid event, q_ev_<regionId>_<n>: the quest log counts them
                int done = CountFlags(FEvent + r.Id + "_");
                bool paid = done < RenownEventCap && d.Set(FEvent + r.Id + "_" + (done + 1));
                Log.Info("quest", $"region event finished in {r.Id} (#{done + 1}, renown {(paid ? "granted" : "capped")})");
                if (paid) AddRenown(r, RenownEvent, "區域事件");
                CountStep("event", r.Id);
                MarkSave("event");
                return true;
            }
            case "elite":
                return _region != null && CountStep("elite", _region.Id);
            case "rift":
            {
                if (int.TryParse(arg, out int tier) && tier > d.RiftBest)
                {
                    Log.Info("quest", $"rift best {d.RiftBest} → {tier}");
                    d.RiftBest = tier;
                    MarkSave("rift");
                }
                SyncTiers();
                return false;
            }
            case "waypoint":
                AddWaypoint(arg);
                return false;
            case "levelup":
                StoryVoice.LevelUp(arg);
                return false;
        }
        return false;
    }

    // ───────────────────────── talking ─────────────────────────

    /// <summary>
    /// What this NPC has to say right now. Finishes every "talk" step waiting for them (all pending conversations are
    /// joined into one script, rewards are granted at once). Returns null when there is nothing but small talk for a
    /// vendor (the caller then opens the service; the greeting is shown as a subtitle).
    /// </summary>
    public static DialogueScript TalkTo(string npcId)
    {
        if (_h == null || string.IsNullOrEmpty(npcId)) return null;
        npcId = NpcKey(npcId);
        DialogueScript result = null;
        Begin();
        try
        {
            var d = _h.D;
            List<DialogueLine> lines = null;
            string id = null;
            bool spoke;
            int guard = 0;
            do
            {
                spoke = false;
                foreach (var q in QuestDb.All)
                {
                    int st = Stage(q);
                    if (st < 0) continue;
                    var s = q.Steps[st];
                    if (s.Event != "talk" || s.Arg != npcId) continue;
                    lines ??= new List<DialogueLine>();
                    if (q.Id == QuestDb.FinalQuest && st == q.Steps.Count - 1 && d.Set(FlagEnding))
                    {
                        // the narrated ending was never shown at the forge: tell it now, before the elder speaks
                        var ending = DialogueDb.Fetch("ending");
                        if (ending != null) lines.AddRange(ending.Lines);
                        Log.Info("quest", "ending narration joined to the epilogue");
                    }
                    var script = DialogueDb.Fetch(s.Dialogue);
                    if (script != null) lines.AddRange(script.Lines);
                    id ??= s.Dialogue;
                    Log.Info("quest", $"talk {npcId}: {q.Id} step {st} ({s.Dialogue})");
                    Advance(q);
                    spoke = true;
                    if (_h == null) return null;
                }
                if (spoke) Evaluate();
            } while (spoke && ++guard < 16);

            result = lines != null && lines.Count > 0 ? new DialogueScript { Id = id ?? npcId, Lines = lines } : SmallTalk(npcId);
        }
        catch (Exception e) { Log.Error("quest", $"TalkTo({npcId}) failed", e); }
        finally
        {
            _holdToasts = result != null;        // the caller opens this script next: its toasts wait for it to close
            try { End(); } finally { _holdToasts = false; }
            // safety: when no dialogue opened after all, show them at the end of the frame
            if (result != null) Callable.From(() => { if (!DialogueUi.Talking) ReleaseHeldToasts(); }).CallDeferred();
        }
        return result;
    }

    private static DialogueScript SmallTalk(string npcId)
    {
        switch (npcId)
        {
            case QuestDb.Elder:
            {
                var s = new DialogueScript { Id = "elder_idle" };
                var focus = FocusMain();
                var remind = DialogueDb.Line(focus != null ? focus.Remind : CampaignDone ? "elder_remind_after" : null);
                if (remind != null) s.Lines.Add(remind);
                var idle = DialogueDb.Line(DialogueDb.NextIn("elder_idle"));
                if (idle != null) s.Lines.Add(idle);
                return s.Lines.Count > 0 ? s : null;
            }
            case QuestDb.Smith: case QuestDb.Merchant: case QuestDb.Occultist: case QuestDb.Jeweler:
                Greet(npcId, DialogueDb.NextIn(npcId));
                return null;
            case "villager": case "villager_m": case "villager_f":
            {
                bool dawn = CampaignDone && Rng.Chance(0.4f);
                string key = npcId == "villager"
                    ? DialogueDb.NextIn(dawn ? "villager_dawn" : "villager")
                    : dawn ? "dawn_" + npcId : DialogueDb.NextIn(npcId);
                Greet(npcId, key);
                return null;
            }
        }
        return null;
    }

    /// <summary>A vendor's greeting or a villager's murmur as a subtitle with voice; throttled so repeated clicks do not stutter.</summary>
    private static void Greet(string npcId, string barkId)
    {
        if (barkId == null) return;
        long now = System.Environment.TickCount64;
        if (_greetNpc == npcId && now - _greetAt < 5000) return;
        _greetNpc = npcId; _greetAt = now;
        DialogueDb.Say(barkId);
    }

    // ───────────────────────── narrated ending ─────────────────────────
    // (the intro is played by Game when a new character first reaches town)

    private static bool After(double seconds, Action a)
    {
        var g = Game.I;
        if (g == null || !GodotObject.IsInstanceValid(g) || !g.IsInsideTree()) return false;
        var hero = _h;
        var timer = g.GetTree().CreateTimer(seconds);
        timer.Timeout += () => { if (hero != null && ReferenceEquals(_h, hero)) Log.Guard("quest", a); };
        return true;
    }

    private static bool CanNarrate(Game g) =>
        g.State == Game.GameState.Play && g.Ui != null && !g.Ui.AnyOpen && (World.P == null || !World.P.Dead);

    /// <summary>
    /// A few seconds after the last boss falls the narrated ending opens in the dialogue panel. If that cannot happen
    /// (another panel is open, the hero died with the boss, an unattended test run) it waits and retries; if it never
    /// gets its moment, the narration is told together with the elder's epilogue instead (see <see cref="TalkTo"/>).
    /// </summary>
    private static void TryEnding()
    {
        _endingPending = false;
        var g = Game.I;
        var d = _h?.D;
        if (g == null || d == null || d.Has(FlagEnding)) return;
        if (DialogueDb.Requests("ending") > _endingRequests)        // another module fetched and showed it meanwhile
        {
            d.Set(FlagEnding);
            return;
        }
        if (g.Arg("bot")) return;                                   // never put a modal panel in front of the test bot
        if (!CanNarrate(g))
        {
            if (++_endingTries <= 12) _endingPending = After(2.5, TryEnding);
            return;
        }
        d.Set(FlagEnding);
        Log.Info("quest", "playing the narrated ending");
        g.Ui.Open("dialogue", DialogueDb.Fetch("ending"));
        try { g.SaveNow("ending"); }
        catch (Exception e) { Log.Error("quest", "autosave failed", e); }
    }

    // ───────────────────────── diagnostics ─────────────────────────

    /// <summary>One-line state for the log: active quests with their step, finished count, renown per region.</summary>
    public static string Summary()
    {
        if (_h == null) return "(no hero)";
        var d = _h.D;
        var active = new List<string>();
        int done = 0;
        foreach (var q in QuestDb.All)
        {
            int st = Stage(q);
            if (st == Done) done++;
            else if (st >= 0) active.Add($"{q.Id}@{st}");
        }
        var renown = d.Renown.Count == 0 ? "-" : string.Join(",", d.Renown.Select(kv => $"{kv.Key}:{kv.Value}"));
        return $"active=[{string.Join(" ", active)}] done={done}/{QuestDb.All.Count} keys={Keys} campaign={d.Has(FlagCampaign)} maxTier={d.MaxTier} renown={renown}";
    }
}
