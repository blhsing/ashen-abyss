using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════
// 亡者名冊 Roll of the Dead — the generic per-class "book" (tools/golems/PLAN.md §1, §3).
// A book has lines; each line offers kinds; each kind has two upgrade picks (choose one); a line can instead be
// sacrificed (獻祭) for a personal bonus. Only the Soulbinder has a book today; the data and the UI are generic.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>One of a kind's two upgrade picks.</summary>
public sealed class BookUpDef
{
    public string Name, Desc;
}

/// <summary>A kind in a book line (骸骨戰士 掠襲者, 骨魔像 …).</summary>
public sealed class BookKindDef
{
    public string Id, Name, Short, Desc, Icon;
    public string Line;
    public BookUpDef[] Ups = new BookUpDef[2];
}

/// <summary>A line of the book (骸骨戰士, 骸骨術士, 魔像) with its kinds and its sacrifice bonus.</summary>
public sealed class BookLineDef
{
    public string Id, Name, Icon;
    public string SacName, SacDesc;
    public (Stat s, float v)[] SacStats = Array.Empty<(Stat, float)>();
    /// <summary>Bonus to armour (per cent of the armour from gear) when sacrificed; armour is flat, so it is not a stat.</summary>
    public float SacArmorPct;
    public BookKindDef[] Kinds = Array.Empty<BookKindDef>();
}

public sealed class ClassBookDef
{
    public ClassId Class;
    public string Name, Hint;
    public int UnlockLevel = 5;
    public BookLineDef[] Lines = Array.Empty<BookLineDef>();
    /// <summary>Kind picked in each line by default (and for migrated saves), in line order.</summary>
    public string[] Defaults = Array.Empty<string>();
}

/// <summary>What the hero chose in a book. Stored in <see cref="SaveData.Book"/>.</summary>
public sealed class BookData
{
    /// <summary>line id → kind id, or <see cref="ClassBook.Sacrificed"/>.</summary>
    public Dictionary<string, string> Pick = new();
    /// <summary>line id → upgrade index taken for the current kind (0 or 1; -1 none).</summary>
    public Dictionary<string, int> Up = new();
    /// <summary>Changes that cost nothing outside town (granted once to a migrated save).</summary>
    public int FreeChanges;
    /// <summary>True when this book was given to a save from before the book existed (old-save migration).</summary>
    public bool Migrated;
}

/// <summary>A persistent minion as it follows the hero across areas, town and save/load.</summary>
public sealed class MinionEntry
{
    public string Def;        // MonsterDef id
    public string Skill;      // SummonSkill tag ("sb_raise", "sb_raise#mage", "pb_bind", "golem" …)
    public float Life = 1f;   // life fraction
}

/// <summary>The persistent minions of the hero (warrior / mage counts, the golem's state, the Pactbinder's fiend).</summary>
public sealed class MinionSave
{
    public List<MinionEntry> List = new();
    public float GolemLife = 1f;      // fraction; 0 while it re-forms
    public float GolemReform;         // seconds left before a dead golem re-forms
    public bool GolemDead;
}

public sealed partial class SaveData
{
    /// <summary>亡者名冊 choices (null until the class's book is first used; filled by Hero.EnsureBook).</summary>
    public BookData Book;
    /// <summary>The item the iron golem was forged from: a full copy (its essence). Re-forming never costs it again.</summary>
    public Item GolemForge;
    /// <summary>Persistent minions (MinionSystem).</summary>
    public MinionSave Minions;
}

public static class ClassBook
{
    public const string Sacrificed = "sac";
    public const string Warriors = "warriors", Mages = "mages", Golem = "golem";

    public static readonly List<ClassBookDef> Books = Build();

    public static ClassBookDef Of(ClassId c) => Books.FirstOrDefault(b => b.Class == c);
    public static BookLineDef Line(ClassBookDef b, string lineId) => b?.Lines.FirstOrDefault(l => l.Id == lineId);
    public static BookKindDef Kind(string id) => id == null ? null : Books.SelectMany(b => b.Lines).SelectMany(l => l.Kinds).FirstOrDefault(k => k.Id == id);

    /// <summary>Gold for one change outside town (free in town): a small fee that grows with the level.</summary>
    public static int Fee(int level) => 50 + 25 * Math.Max(1, level);

    private static BookKindDef K(string id, string line, string name, string shrt, string icon, string desc, (string n, string d) a, (string n, string d) b) =>
        new() { Id = id, Line = line, Name = name, Short = shrt, Icon = icon, Desc = desc, Ups = new[] { new BookUpDef { Name = a.n, Desc = a.d }, new BookUpDef { Name = b.n, Desc = b.d } } };

    private static List<ClassBookDef> Build()
    {
        var l = new List<ClassBookDef>();
        l.Add(new ClassBookDef
        {
            Class = ClassId.Soulbinder, Name = "亡者名冊",
            Hint = "每一條亡者線擇一種類並擇一強化，或將整條線獻祭以換取自身的力量；三條線盡數獻祭時，另得傷害減免 +10%。骸骨戰士與術士吸收較舊的殘魂來補員（你瞄準處附近的殘魂會留給魂爆）。在殘燈鎮變更免費，其他地方需支付金幣。",
            Defaults = new[] { "w_raider", "m_shadow", "g_bone" },
            Lines = new[]
            {
                new BookLineDef
                {
                    Id = Warriors, Name = "骸骨戰士", Icon = "skill/sb_raise",
                    SacName = "獻祭骸骨戰士", SacDesc = "不再喚起骸骨戰士（殘魂不再被戰士吸收，全數留給魂爆）。你造成的傷害 +10%。", SacStats = new[] { (Stat.DmgAll, 10f) },
                    Kinds = new[]
                    {
                        K("w_raider", Warriors, "掠襲者", "掠襲", "skill/eb_slash", "迅捷而脆弱：移動與攻擊速度 +30%，生命 −30%。",
                          ("鋒鏈", "攻擊使敵人流血，4 秒內造成 40% 的額外傷害。"), ("群狼", "骸骨戰士上限 +1。")),
                        K("w_guard", Warriors, "持盾者", "持盾", "skill/lk_bash", "以棺蓋為盾：生命 +60%，受到的傷害 −25%，攻擊會嘲諷敵人。",
                          ("骨壁", "受到的傷害再降低 15%。"), ("反擊", "受到近戰攻擊時，反彈 30% 的傷害。")),
                        K("w_reaper", Warriors, "收割者", "收割", "skill/eb_cleave", "緩慢而沉重：攻擊會橫掃周圍，傷害 +50%，攻擊速度 −20%。",
                          ("寬刃", "橫掃範圍擴大 40%。"), ("處刑", "對生命低於 30% 的敵人傷害 +40%。")),
                    },
                },
                new BookLineDef
                {
                    Id = Mages, Name = "骸骨術士", Icon = "skill/sb_bolt",
                    SacName = "獻祭骸骨術士", SacDesc = "不再喚起骸骨術士（殘魂不再被術士吸收，全數留給魂爆）。資源產生 +15%，爆擊機率 +3%。", SacStats = new[] { (Stat.ResourceGen, 15f), (Stat.CritChance, 3f) },
                    Kinds = new[]
                    {
                        K("m_shadow", Mages, "影術士", "影術", "skill/sb_bolt", "從遠處射出蝕魂之彈，造成暗影傷害。",
                          ("影蝕", "命中使敵人受到的僕從傷害提高 25%，持續 4 秒。"), ("雙矢", "每次施法射出 2 枚彈，各造成 65% 傷害。")),
                        K("m_frost", Mages, "霜術士", "霜術", "skill/as_frostlance", "射出寒霜之矢，使敵人緩速 30%。",
                          ("凍結", "命中使敵人冰緩，累積後凍結。"), ("冰甲", "命中時你獲得相當於生命上限 2% 的屏障（每 3 秒一次）。")),
                        K("m_bone", Mages, "骨術士", "骨術", "skill/sb_spear", "以自身生命為代價擲出骨矛：每次消耗 5% 生命，傷害 +60%。",
                          ("髓燃", "骨矛傷害再提高 30%，但每次消耗 10% 生命。"), ("回髓", "骨矛擊殺敵人時恢復 20% 生命。")),
                    },
                },
                new BookLineDef
                {
                    Id = Golem, Name = "魔像", Icon = "skill/sb_colossus",
                    SacName = "獻祭魔像", SacDesc = "不再塑造魔像。生命上限 +15%，護甲 +15%，傷害減免 +6%。", SacStats = new[] { (Stat.LifePct, 15f), (Stat.DmgReduction, 6f) }, SacArmorPct = 15f,
                    Kinds = new[]
                    {
                        K("g_bone", Golem, "骨魔像", "骨像", "skill/sb_colossus", "坦克：攻擊會嘲諷周圍的敵人，並以荊棘反傷近戰攻擊者。號令「骨牢令」：嘲諷 8 公尺內所有敵人 5 秒，期間魔像受到的傷害降低 50%。",
                          ("碎骨反擊", "荊棘傷害加倍，嘲諷範圍擴大 50%。"), ("骨甲共享", "魔像存活時，你受到的傷害降低 10%。")),
                        K("g_blood", Golem, "血魔像", "血像", "skill/eb_rupture", "續戰：代你承受 15% 的傷害，並以自身的攻擊恢復生命。號令「血潮令」：吸取 6 公尺內敵人的生命 3 秒，造成暗影傷害並治療你與魔像。",
                          ("輸血", "你的生命低於 40% 時，魔像將自身 15% 的生命輸給你（每 8 秒一次）。"), ("血爆", "魔像死亡時爆裂，對周圍造成 300% 武器傷害的暗影傷害。")),
                        K("g_iron", Golem, "鐵魔像", "鐵像", "skill/eb_leap", "重擊：每第 4 次攻擊放出震波。可用金屬裝備鑄造，繼承其詞綴與外觀。號令「鐵擊令」：躍向目標位置重擊，使 4 公尺內的敵人暈眩 1.5 秒。",
                          ("熔鐵", "震波與鐵擊令留下燃燒的裂隙 3 秒。"), ("重鑄", "死亡後 8 秒即重新塑成（原為 20 秒）。")),
                    },
                },
            },
        });
        return l;
    }
}
