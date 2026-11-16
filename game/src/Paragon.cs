using System;
using System.Collections.Generic;
using System.Text;

namespace AshenAbyss;

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════
//  燼紋盤 — the endgame progression that wakes at the level cap.
//
//  A hero attaches up to four square boards (15 × 15 cells). The first one is the class's start board with the
//  燼種 (start node) in its centre; every board has a 燼門 (gate) in the middle of each edge, and an owned gate
//  lets one more board be attached on that side. Nodes are taken one point at a time, always next to an owned
//  node; refunds are free as long as everything owned stays connected to the 燼種.
//
//  This file is data and rules only (no engine nodes): layouts, the boards of every class, glyphs, and the text
//  shown for them. The per-hero state (what is owned, attached and socketed) lives in Hero.Paragon.cs.
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════

public enum PNodeType { Start, Normal, Magic, Rare, Legendary, Socket, Gate }

/// <summary>One cell of a board.</summary>
public sealed class PNode
{
    public int Index, X, Y;                 // Index = Y * PBoard.Size + X
    public PNodeType Type;
    public string Name = "";
    public string Desc;                     // legendary effect / flavour line
    public (Stat s, float v)[] Stats = Array.Empty<(Stat, float)>();
    public (Stat s, float v)[] Bonus = Array.Empty<(Stat, float)>();    // rare: granted only while the threshold is met
    public Stat Req = Stat.Count;           // rare: the attribute the threshold is checked against
    public string Hook;                     // legendary: behaviour hook id honoured by combat code (ks_*), null for pure stats
    public int GateDir = -1;                // gate: 0 north, 1 east, 2 south, 3 west
}

/// <summary>A board definition, built once per class.</summary>
public sealed class PBoard
{
    public const int Size = 15, Mid = 7, CellCount = Size * Size;
    public string Id, Name, Desc, Kind;
    public bool IsStart, Shared;
    public readonly Stat[] Sectors = new Stat[4];           // attribute of the normal nodes in the north / east / south / west wedge
    public readonly PNode[] Cells = new PNode[CellCount];   // null = empty cell
    public readonly List<PNode> Nodes = new();
    public readonly PNode[] Gates = new PNode[4];
    public PNode StartNode, Legendary, Socket;
    public int[] Links = Array.Empty<int>();                // pairs of node indices that touch (for drawing)

    public PNode At(int x, int y) => x < 0 || y < 0 || x >= Size || y >= Size ? null : Cells[y * Size + x];
    public PNode Node(int index) => index < 0 || index >= CellCount ? null : Cells[index];
}

public static class ParagonDb
{
    public const int MaxBoards = 4;
    public const int NormalValue = 5, GateValue = 5;
    public const float RareMul = 2.5f;
    public static readonly (int dx, int dy)[] Dirs = { (0, -1), (1, 0), (0, 1), (-1, 0) };
    public static readonly string[] DirNames = { "北", "東", "南", "西" };
    public static readonly Stat[] Attrs = { Stat.Str, Stat.Int, Stat.Dex, Stat.Wil };
    public static int Opposite(int dir) => (dir + 2) & 3;

    private static readonly Dictionary<ClassId, List<PBoard>> Cache = new();

    /// <summary>Every board a class may use; [0] is its start board.</summary>
    public static IReadOnlyList<PBoard> Boards(ClassId c)
    {
        if (!Cache.TryGetValue(c, out var l)) Cache[c] = l = BuildClass(c);
        return l;
    }

    public static PBoard StartBoard(ClassId c) => Boards(c)[0];

    public static PBoard Board(ClassId c, string id)
    {
        if (id == null) return null;
        var l = Boards(c);
        for (int i = 0; i < l.Count; i++) if (l[i].Id == id) return l[i];
        return null;
    }

    /// <summary>Which wedge of the board a cell lies in: 0 north, 1 east, 2 south, 3 west (diagonals count as north / south).</summary>
    public static int Sector(int x, int y)
    {
        int dx = x - PBoard.Mid, dy = y - PBoard.Mid;
        return Math.Abs(dy) >= Math.Abs(dx) ? (dy <= 0 ? 0 : 2) : (dx > 0 ? 1 : 3);
    }

    /// <summary>The attribute total a rare node's second half needs. It rises with every board attached before this one.</summary>
    public static int RareThreshold(ClassId c, Stat req, int boardOrdinal)
    {
        int o = Math.Max(0, boardOrdinal);
        return req == Names.MainStat(c) ? 260 + 70 * o : 110 + 35 * o;
    }

    /// <summary>What one magic node of a stat grants. Rare nodes grant RareMul times this for each half.</summary>
    public static float MagicValue(Stat s) => s switch
    {
        Stat.Str or Stat.Int or Stat.Dex or Stat.Wil => 7f,
        Stat.MaxLife => 60f, Stat.LifePct => 2f, Stat.Armor => 60f, Stat.ResAll => 2f,
        Stat.ResFire or Stat.ResFrost or Stat.ResLightning or Stat.ResPoison or Stat.ResShadow => 6f,
        Stat.DmgReduction => 1f, Stat.Dodge => 1f, Stat.LifeRegen => 4f, Stat.LifeOnHit => 2f, Stat.LifeOnKill => 6f,
        Stat.Healing => 4f, Stat.Thorns => 40f, Stat.BarrierPct => 6f,
        Stat.DmgAll => 3f,
        Stat.DmgPhysical or Stat.DmgFire or Stat.DmgFrost or Stat.DmgLightning or Stat.DmgPoison or Stat.DmgShadow => 4f,
        Stat.DmgDot => 5f, Stat.DmgElite => 4f, Stat.DmgCC => 5f, Stat.DmgVuln => 5f, Stat.DmgOverpower => 8f,
        Stat.DmgBasic => 6f, Stat.DmgCore => 5f, Stat.DmgUlt => 6f, Stat.MinionDmg => 5f,
        Stat.AttackSpeed => 1.6f, Stat.CritChance => 1f, Stat.CritDmg => 5f, Stat.LuckyHit => 2f,
        Stat.MaxResource => 4f, Stat.ResourceGen => 3f, Stat.ResourceCost => 1.6f, Stat.Cdr => 1.6f,
        Stat.MoveSpeed => 1.6f, Stat.GoldFind => 6f, Stat.MagicFind => 4f, Stat.XpGain => 2f,
        _ => 1f
    };

    // ───────────────────────── layouts ─────────────────────────
    //  . empty   o normal   G gate   S start   L legendary   O glyph socket
    //  A-F rare node of cluster 0-5        a-f magic nodes of that cluster
    //  Every layout is 15 × 15 with a gate in the middle of each edge, so it can be rotated and mirrored freely.

    /// <summary>The start board: a cross from the 燼種 to the four gates, a cluster in every corner.</summary>
    private static readonly string[] TStart =
    {
        ".......G.......",
        ".aaa...o...bbb.",
        ".aAa...o...bBb.",
        "..o....o....o..",
        "..o.aooooob.o..",
        "..ooo..o..ooo..",
        "....o..o..o....",
        "GooooooSooooooG",
        "....o..o..o....",
        "..ooo..o..ooo..",
        "..o.coooood.o..",
        "..o....o....o..",
        ".cCc...o..dDd..",
        ".ccc...o..ddoL.",
        ".......G.......",
    };

    /// <summary>The wheel: the socket sits in the hub of a ring road; the legendary waits in a far corner.</summary>
    private static readonly string[] TWheel =
    {
        ".......G.......",
        ".aAa.d.o...bBb.",
        ".a.a.Ddo...b.b.",
        ".aaa.d.o...bbb.",
        "...ooooooooo...",
        "....o..o..o....",
        "....o..o..o....",
        "GooooooOooooooG",
        "....o..o..o....",
        "....o..o..o....",
        "...ooooooooo...",
        ".ccc...o.e.oo..",
        ".c.c...oeE.oL..",
        ".cCc...o.e.....",
        ".......G.......",
    };

    /// <summary>The pinwheel: four arms spiral into the legendary at the centre; an outer road lets a path pass it by.</summary>
    private static readonly string[] TPinwheel =
    {
        ".......G.......",
        "..a....o....b..",
        ".aAaooooooobBb.",
        "..a.o..O..o.b..",
        "..oooo....ooo..",
        "..o..o.oooo.o..",
        "..o..o.o....o..",
        "Goo..ooLoo..ooG",
        "..o....o.o..o..",
        "..o.oooo.o..o..",
        "..ooo..E.oooo..",
        "..d.o.eee.o.c..",
        ".dDdooooooocCc.",
        "..d....o....c..",
        ".......G.......",
    };

    /// <summary>The diamond: a stepped ring around the legendary, the socket tucked under its northern tip.</summary>
    private static readonly string[] TDiamond =
    {
        ".......G.......",
        ".......o.......",
        "...a...o...b...",
        "..aA..ooo.bBb..",
        "...aaooooob....",
        "....oo.O.oo....",
        "...oo.....oo...",
        "GooooooLooooooG",
        "...oo..e..oo...",
        "....ooeEeoo....",
        "....dooeoocc...",
        "..dDd.ooo..Cc..",
        "...d...o...c...",
        ".......o.......",
        ".......G.......",
    };

    /// <summary>Rotates a layout clockwise <paramref name="rot"/> quarter turns, then mirrors it left-right if asked.</summary>
    private static char[] Transform(string[] t, int rot, bool flip)
    {
        const int n = PBoard.Size;
        var g = new char[n * n];
        Array.Fill(g, '.');
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                char ch = y < t.Length && x < t[y].Length ? t[y][x] : '.';
                int nx = x, ny = y;
                for (int r = 0; r < (rot & 3); r++) { int tx = n - 1 - ny; ny = nx; nx = tx; }
                if (flip) nx = n - 1 - nx;
                g[ny * n + nx] = ch;
            }
        return g;
    }

    // ───────────────────────── board specifications ─────────────────────────

    /// <summary>Placeholder for "the class's own main attribute" in the boards every class shares.</summary>
    private const Stat MainAttr = Stat.Count;

    private sealed class Spec
    {
        public string Id, Name, Desc, Kind = "職業盤";
        public string[] T;
        public int Rot;
        public bool Flip, Start, Shared;
        public Stat[] Sectors;
        public (string name, Stat a, Stat b, Stat req)[] Rares = Array.Empty<(string, Stat, Stat, Stat)>();
        public string LName = "", LDesc = "", LHook;
        public (Stat s, float v)[] LStats = Array.Empty<(Stat, float)>();

        public Spec AsStart() { Start = true; Kind = "起始盤"; return this; }
        public Spec AsShared(string kind) { Shared = true; Kind = kind; return this; }
        public Spec R(params (string, Stat, Stat, Stat)[] rares) { Rares = rares; return this; }
        public Spec L(string name, string desc, string hook, params (Stat, float)[] stats) { LName = name; LDesc = desc; LHook = hook; LStats = stats; return this; }
    }

    private static Spec B(string id, string name, string desc, string[] t, int rot, bool flip, Stat n, Stat e, Stat s, Stat w) =>
        new() { Id = id, Name = name, Desc = desc, T = t, Rot = rot, Flip = flip, Sectors = new[] { n, e, s, w } };

    private static string FuryDesc(string res) => $"{res}不再自然衰減。{res}高於 50 時，造成的傷害提高 15%。";
    private const string ResoDesc = "施放元素與前一次不同的技能時獲得一層共鳴（物理技能不計）：每層使傷害提高 8%，持續 6 秒，最多 3 層。";
    private const string ComboDesc = "連擊點上限 +2。每點連擊點使爆擊機率提高 3%。";
    private const string ArmyDesc = "骸骨僕從上限 +2。你的僕從受到的傷害降低 30%。";
    private const string LawDesc = "受你役使的魔物受到的傷害降低 30%。";
    private const string BulwarkDesc = "生命高於 80% 時，額外獲得 12% 傷害減免。";
    private const string CycleDesc = "擊殺菁英或首領時，所有技能的剩餘冷卻時間縮短 3 秒。";

    private static List<Spec> SpecsFor(ClassId c)
    {
        const Stat Str = Stat.Str, Int = Stat.Int, Dex = Stat.Dex, Wil = Stat.Wil;
        string res = Names.Resource(c);
        var l = new List<Spec>();
        switch (c)
        {
            // ══════════════════ 燼刃衛 ══════════════════
            case ClassId.Emberblade:
                l.Add(B("eb_start", "燼刃之礎", "巨劍、重甲，還有一口嚥不下去的怒火。燼刃衛的燼紋都從這裡刻起。", TStart, 0, false, Str, Str, Wil, Dex).AsStart()
                    .R(("熔鑄", Stat.DmgFire, Stat.DmgOverpower, Str), ("重擊者", Stat.DmgCore, Stat.CritDmg, Str),
                       ("戰痕", Stat.LifeOnHit, Stat.Thorns, Wil), ("鐵幕", Stat.Armor, Stat.LifePct, Dex))
                    .L("巨刃之誓", "劍越沉，誓言越重。", null, (Stat.DmgCore, 20), (Stat.DmgOverpower, 30), (Stat.Str, 25)));
                l.Add(B("eb_fury", "不滅怒焰", "怒火本該隨戰鬥結束而冷卻。這面燼紋不許它冷。", TWheel, 0, false, Str, Wil, Str, Dex)
                    .R(("焚鋒", Stat.DmgFire, Stat.DmgAll, Str), ("燃怒", Stat.ResourceGen, Stat.MaxResource, Wil),
                       ("灼痕", Stat.DmgVuln, Stat.CritDmg, Str), ("怒濤", Stat.AttackSpeed, Stat.DmgBasic, Dex),
                       ("餘燼不冷", Stat.DmgDot, Stat.DmgFire, Wil))
                    .L("不滅怒焰", FuryDesc(res), "ks_fury", (Stat.ResourceGen, 10)));
                l.Add(B("eb_break", "碎甲裂陣", "不繞路，不試探。擋在前面的，一律劈開。", TPinwheel, 0, false, Str, Dex, Str, Wil)
                    .R(("斷鋼", Stat.DmgPhysical, Stat.DmgOverpower, Str), ("千鈞", Stat.DmgCore, Stat.DmgElite, Str),
                       ("破陣", Stat.DmgCC, Stat.DmgAll, Dex), ("血勇", Stat.LifeOnHit, Stat.LifePct, Wil),
                       ("終結", Stat.CritChance, Stat.CritDmg, Dex))
                    .L("崩山", "一劍落下，山也得讓路。", null, (Stat.DmgOverpower, 60), (Stat.DmgPhysical, 25), (Stat.DmgCore, 15)));
                l.Add(B("eb_guard", "鐵誓守衛", "城牆倒了之後，剩下的牆就是穿著甲的人。", TDiamond, 0, false, Str, Wil, Str, Int)
                    .R(("棘甲", Stat.Thorns, Stat.Armor, Str), ("不退", Stat.DmgReduction, Stat.LifePct, Str),
                       ("熔爐之膚", Stat.ResFire, Stat.ResAll, Int), ("戰吼餘響", Stat.Healing, Stat.LifeRegen, Wil),
                       ("鎮守", Stat.BarrierPct, Stat.MaxLife, Wil))
                    .L("荊棘鐵衛", "伸手碰他的人，先流自己的血。", null, (Stat.Thorns, 400), (Stat.DmgReduction, 6), (Stat.Armor, 200)));
                break;

            // ══════════════════ 星術師 ══════════════════
            case ClassId.Astromancer:
                l.Add(B("as_start", "星圖初階", "天上已經沒有星星了。星術師只好把星圖刻進自己的血肉。", TStart, 1, false, Int, Int, Wil, Dex).AsStart()
                    .R(("熾星", Stat.DmgFire, Stat.DmgCore, Int), ("寒星", Stat.DmgFrost, Stat.DmgCC, Int),
                       ("靈泉", Stat.MaxResource, Stat.ResourceGen, Wil), ("雷星", Stat.DmgLightning, Stat.CritChance, Dex))
                    .L("群星低語", "夜空沉默之後，她在自己體內把它們一顆一顆點亮。", null, (Stat.DmgFire, 15), (Stat.DmgFrost, 15), (Stat.DmgLightning, 15), (Stat.MaxResource, 20)));
                l.Add(B("as_reso", "元素共鳴", "火、霜與雷從不相容。讓它們輪流開口，反而成了合唱。", TWheel, 1, false, Int, Dex, Int, Wil)
                    .R(("三相", Stat.DmgFire, Stat.DmgFrost, Int), ("流電", Stat.DmgLightning, Stat.AttackSpeed, Dex),
                       ("星潮", Stat.ResourceGen, Stat.Cdr, Wil), ("聚焦", Stat.CritChance, Stat.CritDmg, Dex),
                       ("星辰餘暉", Stat.DmgAll, Stat.DmgElite, Int))
                    .L("元素共鳴", ResoDesc, "ks_resonance", (Stat.MaxResource, 15)));
                l.Add(B("as_pyre", "燃天之火", "日爐熄了，不代表天空不能再燒一次。", TPinwheel, 1, false, Int, Wil, Int, Dex)
                    .R(("焚野", Stat.DmgFire, Stat.DmgDot, Int), ("隕落", Stat.DmgUlt, Stat.DmgElite, Int),
                       ("灼魂", Stat.DmgVuln, Stat.DmgFire, Wil), ("熾心", Stat.CritDmg, Stat.DmgCore, Dex),
                       ("燃盡", Stat.ResourceCost, Stat.MaxResource, Wil))
                    .L("隕火", "她唸出的每一個字，都曾是一顆墜落的星。", null, (Stat.DmgFire, 35), (Stat.DmgUlt, 30)));
                l.Add(B("as_veil", "霜幕雷庭", "寒氣替她築牆，雷光替她守夜。", TDiamond, 1, false, Int, Dex, Int, Wil)
                    .R(("霜幕", Stat.BarrierPct, Stat.DmgFrost, Int), ("凍土", Stat.DmgCC, Stat.DmgFrost, Wil),
                       ("雷庭", Stat.DmgLightning, Stat.LuckyHit, Dex), ("星盾", Stat.DmgReduction, Stat.ResAll, Int),
                       ("靜電", Stat.MoveSpeed, Stat.Dodge, Dex))
                    .L("凝星之幕", "霜在她四周結成星圖，沒有東西能不留痕跡地穿過。", null, (Stat.BarrierPct, 40), (Stat.DmgFrost, 20), (Stat.DmgLightning, 20)));
                break;

            // ══════════════════ 影獵手 ══════════════════
            case ClassId.Hunter:
                l.Add(B("hu_start", "獵徑", "灰暮之下處處是影子。獵手只需要其中一道。", TStart, 2, false, Dex, Dex, Str, Int).AsStart()
                    .R(("獵殺本能", Stat.CritChance, Stat.CritDmg, Dex), ("神射", Stat.DmgBasic, Stat.AttackSpeed, Dex),
                       ("輕足", Stat.MoveSpeed, Stat.ResourceGen, Str), ("暗影行者", Stat.DmgShadow, Stat.Dodge, Int))
                    .L("一矢穿心", "他出門只帶一支箭。通常夠用。", null, (Stat.CritDmg, 40), (Stat.DmgVuln, 20)));
                l.Add(B("hu_tempo", "致命節奏", "一擊，再一擊。等獵物數清楚拍子，曲子已經結束。", TWheel, 2, false, Dex, Wil, Dex, Str)
                    .R(("連環", Stat.DmgCore, Stat.AttackSpeed, Dex), ("蓄勢", Stat.MaxResource, Stat.ResourceGen, Wil),
                       ("弱點", Stat.DmgVuln, Stat.CritDmg, Dex), ("追獵", Stat.DmgElite, Stat.MoveSpeed, Str),
                       ("收割", Stat.LifeOnKill, Stat.DmgAll, Wil))
                    .L("致命節奏", ComboDesc, "ks_combo", (Stat.DmgCore, 10)));
                l.Add(B("hu_venom", "蝕骨之毒", "刀口很淺。要緊的是刀口上塗的東西。", TPinwheel, 2, false, Dex, Int, Dex, Str)
                    .R(("淬毒", Stat.DmgPoison, Stat.DmgDot, Dex), ("潰爛", Stat.DmgDot, Stat.DmgVuln, Int),
                       ("毒霧", Stat.DmgCC, Stat.DmgPoison, Int), ("耐毒", Stat.ResPoison, Stat.LifePct, Str),
                       ("纏鬥", Stat.LuckyHit, Stat.AttackSpeed, Dex))
                    .L("萬蛇之牙", "毒不急。毒有的是時間。", null, (Stat.DmgPoison, 35), (Stat.DmgDot, 35)));
                l.Add(B("hu_shade", "無光之幕", "燈照不到的地方，才是他真正的家。", TDiamond, 2, false, Dex, Int, Dex, Wil)
                    .R(("潛影", Stat.DmgShadow, Stat.CritChance, Dex), ("虛步", Stat.Dodge, Stat.MoveSpeed, Dex),
                       ("暗襲", Stat.CritDmg, Stat.DmgElite, Int), ("煙幕", Stat.DmgReduction, Stat.Cdr, Wil),
                       ("奪命", Stat.DmgCore, Stat.DmgShadow, Int))
                    .L("影中之影", "許多影子裡，只有一道會自己動。", null, (Stat.Dodge, 8), (Stat.DmgShadow, 30), (Stat.MoveSpeed, 8)));
                break;

            // ══════════════════ 喚魂師 ══════════════════
            case ClassId.Soulbinder:
                l.Add(B("sb_start", "魂契之始", "亡者從不沉默，只是活人不肯聽。喚魂師聽了，並且回了話。", TStart, 3, false, Wil, Wil, Int, Str).AsStart()
                    .R(("骸骨主宰", Stat.MinionDmg, Stat.LifePct, Wil), ("幽語", Stat.DmgShadow, Stat.DmgCore, Wil),
                       ("腐朽", Stat.DmgDot, Stat.DmgShadow, Int), ("魂匣", Stat.MaxResource, Stat.LifeOnKill, Str))
                    .L("千魂低語", "她身後總有腳步聲，數目從來對不上。", null, (Stat.MinionDmg, 25), (Stat.DmgShadow, 20), (Stat.MaxResource, 15)));
                l.Add(B("sb_army", "亡者軍勢", "墓土之下的兵員，比任何一位領主麾下的都多。", TWheel, 3, false, Wil, Str, Wil, Int)
                    .R(("點骸成兵", Stat.MinionDmg, Stat.AttackSpeed, Wil), ("骨牆", Stat.Armor, Stat.LifePct, Str),
                       ("魂飼", Stat.ResourceGen, Stat.LifeOnKill, Wil), ("將令", Stat.MinionDmg, Stat.DmgElite, Int),
                       ("不眠", Stat.Cdr, Stat.MaxResource, Int))
                    .L("亡者軍勢", ArmyDesc, "ks_army", (Stat.MinionDmg, 15)));
                l.Add(B("sb_wither", "凋零詛咒", "不必動手。只要說出那個字，剩下的交給時間。", TPinwheel, 3, false, Wil, Int, Wil, Dex)
                    .R(("蝕魂", Stat.DmgShadow, Stat.DmgDot, Wil), ("枯萎", Stat.DmgCC, Stat.DmgVuln, Int),
                       ("厄言", Stat.DmgCore, Stat.CritChance, Dex), ("奪息", Stat.LifeOnHit, Stat.Healing, Wil),
                       ("晦暗", Stat.DmgElite, Stat.DmgShadow, Int))
                    .L("萬物同朽", "連日爐都會熄滅，何況血肉。", null, (Stat.DmgDot, 40), (Stat.DmgShadow, 25)));
                l.Add(B("sb_blood", "血魂祭儀", "欠亡者的，她一向用自己的血來還。", TDiamond, 3, false, Wil, Str, Wil, Int)
                    .R(("血祭", Stat.LifePct, Stat.LifeRegen, Wil), ("飲魂", Stat.LifeOnKill, Stat.Healing, Wil),
                       ("骨甲", Stat.Armor, Stat.DmgReduction, Str), ("怨靈之盾", Stat.BarrierPct, Stat.ResShadow, Int),
                       ("殉道", Stat.DmgAll, Stat.CritDmg, Str))
                    .L("以血還魂", "傷口還沒合上，魂已經先回來了。", null, (Stat.LifePct, 15), (Stat.Healing, 30), (Stat.LifeOnKill, 40)));
                break;

            // ══════════════════ 荒靈行者 ══════════════════
            case ClassId.Wildsoul:
                l.Add(B("ws_start", "荒林之根", "枯林沒有死，只是把心跳藏得很深。", TStart, 0, true, Wil, Wil, Str, Dex).AsStart()
                    .R(("野性", Stat.DmgCore, Stat.AttackSpeed, Wil), ("風暴之子", Stat.DmgLightning, Stat.DmgPhysical, Wil),
                       ("厚毛", Stat.LifePct, Stat.Armor, Str), ("林息", Stat.ResourceGen, Stat.Healing, Dex))
                    .L("古林之心", "樹根記得每一場雨，也記得每一筆仇。", null, (Stat.LifePct, 10), (Stat.DmgCore, 20), (Stat.ResourceGen, 10)));
                l.Add(B("ws_breath", "不散靈息", "獸不會在獵殺之間忘記自己是獸。", TWheel, 0, true, Wil, Dex, Wil, Str)
                    .R(("吐納", Stat.ResourceGen, Stat.MaxResource, Wil), ("獸性", Stat.DmgAll, Stat.AttackSpeed, Dex),
                       ("撕裂", Stat.DmgDot, Stat.DmgPhysical, Dex), ("蛻形", Stat.MoveSpeed, Stat.Cdr, Wil),
                       ("獵血", Stat.LifeOnHit, Stat.CritChance, Str))
                    .L("不散靈息", FuryDesc(res), "ks_fury", (Stat.ResourceGen, 10)));
                l.Add(B("ws_bear", "熊靈之軀", "熊靈不躲。熊靈只是站著，等對方先倒下。", TDiamond, 0, true, Wil, Str, Wil, Str)
                    .R(("巨掌", Stat.DmgOverpower, Stat.DmgCore, Str), ("厚皮", Stat.Armor, Stat.DmgReduction, Str),
                       ("冬眠", Stat.LifeRegen, Stat.Healing, Wil), ("樹皮", Stat.BarrierPct, Stat.LifePct, Wil),
                       ("震懾", Stat.DmgCC, Stat.Thorns, Str))
                    .L("山岳之軀", "風雪推不動山，刀劍也一樣。", null, (Stat.LifePct, 15), (Stat.Armor, 300), (Stat.DmgOverpower, 40)));
                l.Add(B("ws_storm", "風暴與狼", "先看見閃光，然後才感覺到牙。", TPinwheel, 0, true, Wil, Dex, Wil, Int)
                    .R(("引雷", Stat.DmgLightning, Stat.LuckyHit, Int), ("狼牙", Stat.CritChance, Stat.CritDmg, Dex),
                       ("疾奔", Stat.MoveSpeed, Stat.Dodge, Dex), ("雷雲", Stat.DmgLightning, Stat.DmgCC, Wil),
                       ("狩群", Stat.DmgElite, Stat.DmgVuln, Wil))
                    .L("雷牙", "狼群從不嚎叫著衝鋒。替牠們嚎叫的是雷。", null, (Stat.DmgLightning, 30), (Stat.AttackSpeed, 10), (Stat.CritDmg, 25)));
                break;

            // ══════════════════ 鍛身者 ══════════════════
            case ClassId.Forgebody:
                l.Add(B("fb_start", "爐前百鍊", "日爐死了，鍛打卻沒有停。只是砧上換成了自己。", TStart, 1, true, Dex, Dex, Str, Wil).AsStart()
                    .R(("連拳", Stat.DmgBasic, Stat.AttackSpeed, Dex), ("火脈", Stat.DmgFire, Stat.DmgDot, Dex),
                       ("鐵骨", Stat.Armor, Stat.DmgReduction, Str), ("氣海", Stat.ResourceGen, Stat.MaxResource, Wil))
                    .L("千錘之身", "每一道舊傷，都是一次淬火。", null, (Stat.AttackSpeed, 10), (Stat.DmgAll, 12), (Stat.Armor, 150)));
                l.Add(B("fb_vigor", "長燃氣焰", "爐火一旦生起，就不該讓它矮下去。", TWheel, 1, true, Dex, Wil, Dex, Str)
                    .R(("聚氣", Stat.ResourceGen, Stat.DmgCore, Wil), ("流勢", Stat.AttackSpeed, Stat.CritChance, Dex),
                       ("崩拳", Stat.DmgCore, Stat.DmgOverpower, Str), ("吐焰", Stat.DmgFire, Stat.DmgAll, Dex),
                       ("不息", Stat.Cdr, Stat.MoveSpeed, Wil))
                    .L("長燃氣焰", FuryDesc(res), "ks_fury", (Stat.ResourceGen, 10)));
                l.Add(B("fb_iron", "鐵脈金身", "打在他身上的東西，多半自己先壞。", TDiamond, 1, true, Dex, Str, Str, Wil)
                    .R(("硬功", Stat.Armor, Stat.Thorns, Str), ("鐵膚", Stat.DmgReduction, Stat.LifePct, Str),
                       ("卸勁", Stat.Dodge, Stat.Healing, Dex), ("調息", Stat.LifeRegen, Stat.ResAll, Wil),
                       ("反震", Stat.Thorns, Stat.DmgCC, Str))
                    .L("不壞之軀", "鐵會鏽，石會裂。他還站著。", null, (Stat.DmgReduction, 8), (Stat.Armor, 250), (Stat.Thorns, 250)));
                l.Add(B("fb_gale", "風火連環", "一拳是火星。一百拳，就是野火。", TPinwheel, 1, true, Dex, Int, Dex, Wil)
                    .R(("疾風步", Stat.MoveSpeed, Stat.Dodge, Dex), ("烈掌", Stat.DmgFire, Stat.CritDmg, Dex),
                       ("破綻", Stat.DmgVuln, Stat.CritChance, Dex), ("餘焰", Stat.DmgDot, Stat.DmgFire, Int),
                       ("追擊", Stat.DmgElite, Stat.LuckyHit, Wil))
                    .L("燎原之拳", "風助火勢，火借風行。兩樣他都帶在拳頭上。", null, (Stat.DmgFire, 30), (Stat.CritDmg, 30), (Stat.MoveSpeed, 6)));
                break;

            // ══════════════════ 誓燈騎士 ══════════════════
            case ClassId.Lampknight:
                l.Add(B("lk_start", "守燈之誓", "燈在，人就在。騎士的誓詞只有這一句。", TStart, 2, true, Str, Str, Wil, Int).AsStart()
                    .R(("燼光", Stat.DmgFire, Stat.CritChance, Str), ("盾誓", Stat.Armor, Stat.BarrierPct, Str),
                       ("禱言", Stat.Healing, Stat.LifeRegen, Wil), ("審判", Stat.DmgCore, Stat.DmgElite, Int))
                    .L("長明", "他見過最後一座燼塔熄滅的樣子，所以不打算再看一次。", null, (Stat.LifePct, 10), (Stat.DmgFire, 20), (Stat.Healing, 20)));
                l.Add(B("lk_faith", "不移信念", "信念不是火，風吹不熄；信念是握著火的那隻手。", TWheel, 2, true, Str, Wil, Str, Dex)
                    .R(("虔信", Stat.ResourceGen, Stat.MaxResource, Wil), ("燼焰", Stat.DmgFire, Stat.DmgAll, Str),
                       ("誅邪", Stat.DmgElite, Stat.CritDmg, Str), ("燈影", Stat.Cdr, Stat.DmgUlt, Wil),
                       ("奮擊", Stat.AttackSpeed, Stat.DmgBasic, Dex))
                    .L("不移信念", FuryDesc(res), "ks_fury", (Stat.ResourceGen, 10)));
                l.Add(B("lk_ward", "燼光庇護", "站進他的燈下，連寂靜都繞道而行。", TDiamond, 2, true, Str, Wil, Str, Int)
                    .R(("光幕", Stat.BarrierPct, Stat.LifePct, Str), ("堅盾", Stat.Armor, Stat.DmgReduction, Str),
                       ("引燈", Stat.Healing, Stat.Cdr, Wil), ("淨焰", Stat.ResAll, Stat.ResShadow, Int),
                       ("護民", Stat.MaxLife, Stat.LifeOnKill, Wil))
                    .L("燈下無懼", "盾舉起來的時候，他身後的人終於敢闔眼。", null, (Stat.BarrierPct, 40), (Stat.DmgReduction, 6), (Stat.ResAll, 10)));
                l.Add(B("lk_judge", "審判之焰", "教團說寂靜即是救贖。他的回答是火。", TPinwheel, 2, true, Str, Int, Str, Dex)
                    .R(("定罪", Stat.DmgCC, Stat.DmgVuln, Int), ("烈誓", Stat.DmgFire, Stat.DmgCore, Str),
                       ("懲戒", Stat.Thorns, Stat.DmgOverpower, Str), ("裁決", Stat.CritChance, Stat.CritDmg, Dex),
                       ("餘燼審判", Stat.DmgDot, Stat.DmgFire, Int))
                    .L("焚罪", "罪不必宣讀。火自己認得。", null, (Stat.DmgFire, 30), (Stat.DmgCC, 30), (Stat.CritChance, 5)));
                break;

            // ══════════════════ 縛淵術士 ══════════════════
            default:
                l.Add(B("pb_start", "初契", "第一道契約總是最便宜的。代價要到後來才看得清。", TStart, 3, true, Int, Int, Wil, Dex).AsStart()
                    .R(("咒術研習", Stat.DmgCore, Stat.DmgShadow, Int), ("淵火", Stat.DmgFire, Stat.DmgShadow, Int),
                       ("馭魔", Stat.MinionDmg, Stat.MaxResource, Wil), ("契約護身", Stat.LifePct, Stat.ResFire, Dex))
                    .L("淵語者", "燼魔的語言裡沒有「請」字。他也從來不用。", null, (Stat.DmgShadow, 20), (Stat.DmgFire, 20), (Stat.MinionDmg, 15)));
                l.Add(B("pb_law", "魔契鐵律", "契約寫得夠細，魔物就只是一件比較吵的工具。", TWheel, 3, true, Int, Wil, Int, Str)
                    .R(("役魔", Stat.MinionDmg, Stat.DmgElite, Int), ("血肉之契", Stat.LifePct, Stat.Healing, Wil),
                       ("契印湧現", Stat.ResourceGen, Stat.Cdr, Wil), ("鞭笞", Stat.DmgCore, Stat.DmgFire, Int),
                       ("魔軀", Stat.Armor, Stat.DmgReduction, Str))
                    .L("魔契鐵律", LawDesc, "ks_army", (Stat.MinionDmg, 20)));
                l.Add(B("pb_flame", "淵火焚世", "從淵底借來的火，沒有人打算還。", TPinwheel, 3, true, Int, Wil, Int, Dex)
                    .R(("淵焰", Stat.DmgFire, Stat.DmgDot, Int), ("焦土", Stat.DmgCC, Stat.DmgFire, Wil),
                       ("淵門", Stat.DmgUlt, Stat.Cdr, Int), ("爆燃", Stat.CritChance, Stat.CritDmg, Dex),
                       ("熔痕", Stat.DmgVuln, Stat.DmgElite, Dex))
                    .L("淵火不熄", "它在底下燒了一千年，不差這一個晚上。", null, (Stat.DmgFire, 30), (Stat.DmgDot, 30), (Stat.DmgUlt, 20)));
                l.Add(B("pb_shade", "縛魂暗契", "影與焰各執契約的一端。他站在中間，兩邊都收利息。", TDiamond, 3, true, Int, Dex, Int, Wil)
                    .R(("蝕影", Stat.DmgShadow, Stat.DmgCore, Int), ("縛鏈", Stat.DmgCC, Stat.DmgShadow, Int),
                       ("虛弱詛咒", Stat.DmgVuln, Stat.LuckyHit, Dex), ("裂隙行者", Stat.MoveSpeed, Stat.Dodge, Dex),
                       ("暗契", Stat.MaxResource, Stat.ResourceCost, Wil))
                    .L("淵焰共鳴", ResoDesc, "ks_resonance", (Stat.MaxResource, 15)));
                break;
        }

        // ══════════════════ boards every class shares ══════════════════
        l.Add(B("sh_edge", "裂燼之鋒", "不問出身，不問流派。這面燼紋只教一件事：如何更快地結束。", TWheel, 2, false, MainAttr, Dex, MainAttr, Int).AsShared("通用盤・攻勢")
            .R(("見血", Stat.CritChance, Stat.CritDmg, Dex), ("趁虛", Stat.DmgVuln, Stat.DmgCC, Int),
               ("獵首者", Stat.DmgElite, Stat.DmgAll, MainAttr), ("疾鋒", Stat.AttackSpeed, Stat.DmgBasic, Dex),
               ("機遇", Stat.LuckyHit, Stat.CritDmg, Int))
            .L("玻璃利刃", "捨棄防備，換取純粹的殺傷。", null, (Stat.DmgAll, 25), (Stat.CritDmg, 20), (Stat.LifePct, -10)));
        l.Add(B("sh_wall", "不熄之壁", "活下來的人未必最強，只是最晚倒下。", TDiamond, 0, false, MainAttr, Str, MainAttr, Wil).AsShared("通用盤・守禦")
            .R(("厚甲", Stat.Armor, Stat.DmgReduction, Str), ("長息", Stat.LifePct, Stat.LifeRegen, Wil),
               ("禦災", Stat.ResAll, Stat.BarrierPct, Wil), ("靈巧", Stat.Dodge, Stat.MoveSpeed, MainAttr),
               ("生機", Stat.Healing, Stat.LifeOnKill, Str))
            .L("不動壁壘", BulwarkDesc, "ks_bulwark", (Stat.Armor, 200)));
        l.Add(B("sh_road", "流燼之途", "路還很長。懂得省力的人，才走得到盡頭。", TPinwheel, 2, false, MainAttr, Wil, MainAttr, Dex).AsShared("通用盤・巧途")
            .R(("流轉", Stat.ResourceGen, Stat.MaxResource, Wil), ("省力", Stat.ResourceCost, Stat.Cdr, Wil),
               ("拾荒者", Stat.GoldFind, Stat.MagicFind, Dex), ("見識", Stat.XpGain, Stat.MoveSpeed, Dex),
               ("韌命", Stat.MaxLife, Stat.Healing, MainAttr))
            .L("時光餘燼", CycleDesc, "ks_cycle", (Stat.Cdr, 6)));
        return l;
    }

    // ───────────────────────── building ─────────────────────────

    private static List<PBoard> BuildClass(ClassId c)
    {
        var res = new List<PBoard>();
        List<Spec> specs;
        try { specs = SpecsFor(c); }
        catch (Exception e) { Log.Error("paragon", $"board specs for {c} failed", e); specs = new List<Spec>(); }
        foreach (var sp in specs)
        {
            try
            {
                var b = Build(c, sp);
                if (b.IsStart) res.Insert(0, b); else res.Add(b);
            }
            catch (Exception e) { Log.Error("paragon", $"building board {sp.Id} failed", e); }
        }
        if (res.Count == 0 || !res[0].IsStart)
        {
            Log.Error("paragon", $"class {c} has no usable start board: using a bare one");
            var main = Names.MainStat(c);
            res.Insert(0, Build(c, B("fallback_" + Names.ClassKey(c), "燼種之盤", "一面尚未刻下任何名字的燼紋盤。", TStart, 0, false, main, main, main, main).AsStart()
                .L("餘燼", "灰裡還有一點紅。", null, (Stat.DmgAll, 10))));
        }
        int nodes = 0;
        foreach (var b in res) nodes += b.Nodes.Count;
        Log.Info("paragon", $"boards built for {Names.Class(c)}: {res.Count} boards, {nodes} nodes");
        return res;
    }

    private static Stat Resolve(Stat s, Stat main) => s == MainAttr ? main : s;

    private static (string name, Stat a, Stat b, Stat req) RareOf(Spec sp, int k, Stat main)
    {
        if (k < sp.Rares.Length)
        {
            var r = sp.Rares[k];
            return (r.name, Resolve(r.a, main), Resolve(r.b, main), Resolve(r.req, main));
        }
        return ("無名燼紋", Stat.DmgAll, Stat.LifePct, main);
    }

    private static PBoard Build(ClassId cls, Spec sp)
    {
        const int n = PBoard.Size;
        Stat main = Names.MainStat(cls);
        var b = new PBoard { Id = sp.Id, Name = sp.Name, Desc = sp.Desc, Kind = sp.Kind, IsStart = sp.Start, Shared = sp.Shared };
        for (int i = 0; i < 4; i++) b.Sectors[i] = Resolve(sp.Sectors[i], main);
        char[] grid = Transform(sp.T, sp.Rot, sp.Flip);
        var magicSeen = new int[6];
        int clusters = 0;

        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                char ch = grid[y * n + x];
                if (ch == '.') continue;
                var node = new PNode { Index = y * n + x, X = x, Y = y };
                if (ch == 'S')
                {
                    node.Type = PNodeType.Start; node.Name = "燼種"; node.Desc = "一切燼紋由此蔓延。";
                    b.StartNode = node;
                }
                else if (ch == 'G')
                {
                    node.Type = PNodeType.Gate; node.Name = "燼門";
                    node.GateDir = y == 0 ? 0 : x == n - 1 ? 1 : y == n - 1 ? 2 : 3;
                    node.Stats = new (Stat, float)[] { (Stat.Str, GateValue), (Stat.Int, GateValue), (Stat.Dex, GateValue), (Stat.Wil, GateValue) };
                    if (b.Gates[node.GateDir] != null) Log.Error("paragon", $"board {sp.Id}: two gates on side {node.GateDir}");
                    b.Gates[node.GateDir] = node;
                }
                else if (ch == 'L')
                {
                    node.Type = PNodeType.Legendary; node.Name = sp.LName; node.Desc = sp.LDesc; node.Hook = sp.LHook; node.Stats = sp.LStats;
                    if (b.Legendary != null) Log.Error("paragon", $"board {sp.Id}: more than one legendary node");
                    b.Legendary = node;
                }
                else if (ch == 'O')
                {
                    node.Type = PNodeType.Socket; node.Name = "燼符槽";
                    b.Socket = node;
                }
                else if (ch == 'o')
                {
                    Stat a = b.Sectors[Sector(x, y)];
                    node.Type = PNodeType.Normal; node.Name = StatInfo.Name(a);
                    node.Stats = new (Stat, float)[] { (a, NormalValue) };
                }
                else if (ch >= 'A' && ch <= 'F')
                {
                    int k = ch - 'A';
                    var r = RareOf(sp, k, main);
                    node.Type = PNodeType.Rare; node.Name = r.name; node.Req = r.req;
                    node.Stats = new (Stat, float)[] { (r.a, MagicValue(r.a) * RareMul) };
                    node.Bonus = new (Stat, float)[] { (r.b, MagicValue(r.b) * RareMul) };
                    clusters = Math.Max(clusters, k + 1);
                }
                else if (ch >= 'a' && ch <= 'f')
                {
                    int k = ch - 'a';
                    var r = RareOf(sp, k, main);
                    Stat s = (magicSeen[k]++ & 1) == 0 ? r.a : r.b;
                    node.Type = PNodeType.Magic; node.Name = StatInfo.Name(s);
                    node.Stats = new (Stat, float)[] { (s, MagicValue(s)) };
                }
                else
                {
                    Log.Warn("paragon", $"board {sp.Id}: unknown layout mark '{ch}' at {x},{y}");
                    continue;
                }
                b.Cells[node.Index] = node;
                b.Nodes.Add(node);
            }

        var links = new List<int>(b.Nodes.Count * 2);
        foreach (var node in b.Nodes)
        {
            var right = b.At(node.X + 1, node.Y);
            if (right != null) { links.Add(node.Index); links.Add(right.Index); }
            var down = b.At(node.X, node.Y + 1);
            if (down != null) { links.Add(node.Index); links.Add(down.Index); }
        }
        b.Links = links.ToArray();
        Validate(b, sp, clusters);
        return b;
    }

    /// <summary>Catches layout mistakes at start-up instead of in the middle of somebody's endgame.</summary>
    private static void Validate(PBoard b, Spec sp, int clusters)
    {
        const int n = PBoard.Size;
        (int x, int y)[] gatePos = { (PBoard.Mid, 0), (n - 1, PBoard.Mid), (PBoard.Mid, n - 1), (0, PBoard.Mid) };
        for (int d = 0; d < 4; d++)
        {
            var g = b.Gates[d];
            if (g == null || g.X != gatePos[d].x || g.Y != gatePos[d].y) Log.Error("paragon", $"board {b.Id}: gate {d} missing or misplaced");
        }
        if (b.Legendary == null) Log.Error("paragon", $"board {b.Id}: no legendary node");
        if (b.IsStart && b.StartNode == null) Log.Error("paragon", $"board {b.Id}: start board without a start node");
        if (!b.IsStart && b.StartNode != null) Log.Error("paragon", $"board {b.Id}: start node on a board that is not a start board");
        if (!b.IsStart && b.Socket == null) Log.Error("paragon", $"board {b.Id}: no glyph socket");
        if (clusters > sp.Rares.Length) Log.Error("paragon", $"board {b.Id}: layout has {clusters} clusters but only {sp.Rares.Length} rare definitions");
        if (b.Nodes.Count == 0) return;

        var seen = new bool[PBoard.CellCount];
        var q = new Queue<PNode>();
        var first = b.Nodes[0];
        seen[first.Index] = true; q.Enqueue(first);
        int reached = 1;
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                var o = b.At(cur.X + Dirs[d].dx, cur.Y + Dirs[d].dy);
                if (o == null || seen[o.Index]) continue;
                seen[o.Index] = true; reached++; q.Enqueue(o);
            }
        }
        if (reached != b.Nodes.Count) Log.Error("paragon", $"board {b.Id}: layout is not connected ({reached}/{b.Nodes.Count} nodes reachable)");
    }
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════
//  燼符 — glyphs
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A glyph sits in a board's socket and grows with the points of one attribute allocated around it
/// (radius 3 cells, 4 from glyph level 25, 5 from level 51). A second bonus wakes once enough points are in range.
/// </summary>
public sealed class GlyphDef
{
    public string Id, Name, Flavor;
    public Stat Attr;
    public (Stat s, float per5)[] Scale = Array.Empty<(Stat, float)>();     // granted per 5 attribute points in range, at glyph level 1
    public (Stat s, float v)[] Bonus = Array.Empty<(Stat, float)>();        // granted while Threshold points are in range
    public int Threshold = 40;
}

public static class GlyphDb
{
    public const int MaxLevel = 100, TriesPerClear = 3, MaxTries = 30;
    /// <summary>燼紋等級 at which the n-th glyph of a class's order wakes.</summary>
    public static readonly int[] UnlockLevels = { 1, 3, 6, 10, 15, 20, 26, 32, 40, 48, 58, 70 };

    public static readonly List<GlyphDef> All = new()
    {
        G("g_steel", "燼符：斷鋼", Stat.Str, "刻痕很深，像是有人拿它試過劍。", new[] { (Stat.DmgPhysical, 1.0f) }, new[] { (Stat.DmgOverpower, 12f) }),
        G("g_kiln", "燼符：爐心", Stat.Str, "握得久了，掌心會想起日爐的溫度。", new[] { (Stat.DmgFire, 1.0f) }, new[] { (Stat.DmgCore, 8f) }),
        G("g_bastion", "燼符：堅壘", Stat.Str, "殘燈鎮的城牆裡，砌著許多這樣的石片。", new[] { (Stat.Armor, 20f) }, new[] { (Stat.DmgReduction, 3f) }),
        G("g_orbit", "燼符：星軌", Stat.Int, "上頭的星圖，對不上任何一片還存在的夜空。", new[] { (Stat.DmgFrost, 0.7f), (Stat.DmgLightning, 0.7f) }, new[] { (Stat.CritDmg, 10f) }),
        G("g_tide", "燼符：靈潮", Stat.Int, "貼近耳邊，能聽見很遠很遠的潮聲。", new[] { (Stat.MaxResource, 0.8f), (Stat.ResourceGen, 0.5f) }, new[] { (Stat.Cdr, 4f) }),
        G("g_thrall", "燼符：役使", Stat.Int, "被束縛的東西，都認得這道紋。", new[] { (Stat.MinionDmg, 1.2f) }, new[] { (Stat.DmgShadow, 8f) }),
        G("g_eye", "燼符：獵眼", Stat.Dex, "盯著它看的人，總覺得自己才是被看的那個。", new[] { (Stat.CritDmg, 1.2f) }, new[] { (Stat.CritChance, 3f) }),
        G("g_gale", "燼符：疾風", Stat.Dex, "輕得不像石頭，擱在桌上會自己挪動。", new[] { (Stat.AttackSpeed, 0.3f) }, new[] { (Stat.MoveSpeed, 5f) }),
        G("g_venom", "燼符：蝕毒", Stat.Dex, "別用沒戴手套的手碰它。", new[] { (Stat.DmgPoison, 0.8f), (Stat.DmgDot, 0.8f) }, new[] { (Stat.DmgVuln, 10f) }),
        G("g_vigil", "燼符：守魂", Stat.Wil, "守燈人把它縫在衣襟內側，貼著心口。", new[] { (Stat.LifePct, 0.5f) }, new[] { (Stat.Healing, 10f) }),
        G("g_surge", "燼符：靈湧", Stat.Wil, "紋路會隨著持有者的呼吸明滅。", new[] { (Stat.DmgCore, 0.8f) }, new[] { (Stat.ResourceGen, 6f) }),
        G("g_reaper", "燼符：獵首", Stat.Wil, "每一道刻痕，都是一個再也不會出聲的名字。", new[] { (Stat.DmgElite, 1.0f) }, new[] { (Stat.DmgCC, 8f) }),
    };

    private static GlyphDef G(string id, string name, Stat attr, string flavor, (Stat, float)[] scale, (Stat, float)[] bonus) =>
        new() { Id = id, Name = name, Attr = attr, Flavor = flavor, Scale = scale, Bonus = bonus };

    private static readonly Dictionary<ClassId, GlyphDef[]> Orders = new();

    public static GlyphDef Get(string id)
    {
        if (id == null) return null;
        for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
        return null;
    }

    /// <summary>The order in which a class wakes its glyphs: its own attribute first, glyphs it cannot use last.</summary>
    public static IReadOnlyList<GlyphDef> Order(ClassId c)
    {
        if (Orders.TryGetValue(c, out var o)) return o;
        Stat main = Names.MainStat(c);
        bool minions = c is ClassId.Soulbinder or ClassId.Pactbinder or ClassId.Wildsoul;
        var keyed = new List<(int key, GlyphDef g)>();
        for (int i = 0; i < All.Count; i++)
        {
            var g = All[i];
            int key = (g.Attr == main ? 0 : 100) + (g.Id == "g_thrall" && !minions ? 1000 : 0) + i;
            keyed.Add((key, g));
        }
        keyed.Sort((a, b) => a.key.CompareTo(b.key));
        o = new GlyphDef[keyed.Count];
        for (int i = 0; i < o.Length; i++) o[i] = keyed[i].g;
        Orders[c] = o;
        return o;
    }

    /// <summary>燼紋等級 a class needs for a glyph; int.MaxValue for an unknown id.</summary>
    public static int UnlockLevel(ClassId c, string id)
    {
        var o = Order(c);
        for (int i = 0; i < o.Count; i++)
            if (o[i].Id == id) return i < UnlockLevels.Length ? UnlockLevels[i] : UnlockLevels[^1] + 12 * (i - UnlockLevels.Length + 1);
        return int.MaxValue;
    }

    public static int Radius(int level) => level >= 51 ? 5 : level >= 25 ? 4 : 3;
    public static float LevelMul(int level) => 1f + 0.02f * (Math.Clamp(level, 1, MaxLevel) - 1);

    /// <summary>A cell at offset (dx, dy) from the socket counts when it lies inside the rounded circle of that radius.</summary>
    public static bool InRadius(int dx, int dy, int radius) => dx * dx + dy * dy <= (radius + 0.5f) * (radius + 0.5f);

    /// <summary>
    /// Chance that one tempering attempt earned in a rift of <paramref name="tier"/> raises a glyph of <paramref name="level"/>:
    /// certain while the rift is at least as deep as the glyph's level, 10% less per level beyond that, never under 5%.
    /// </summary>
    public static float UpgradeChance(int tier, int level) =>
        level >= MaxLevel || tier <= 0 ? 0f : Math.Clamp(1f - 0.1f * (level - tier), 0.05f, 1f);
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════
//  Text (BBCode) for tooltips and lists
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════

public static class ParagonText
{
    public static readonly Godot.Color SocketColor = new(0.55f, 0.92f, 0.85f);

    private static string Hex(Godot.Color c) => c.ToHtml(false);

    public static Godot.Color AttrColor(Stat s) => s switch
    {
        Stat.Str => new Godot.Color(0.92f, 0.42f, 0.3f),
        Stat.Int => new Godot.Color(0.42f, 0.64f, 1f),
        Stat.Dex => new Godot.Color(0.5f, 0.86f, 0.45f),
        Stat.Wil => new Godot.Color(0.76f, 0.56f, 0.96f),
        _ => Palette.Text
    };

    public static Godot.Color TypeColor(PNodeType t) => t switch
    {
        PNodeType.Magic => Palette.Rarity(Rarity.Magic),
        PNodeType.Rare => Palette.Rarity(Rarity.Rare),
        PNodeType.Legendary => Palette.Rarity(Rarity.Legendary),
        PNodeType.Socket => SocketColor,
        PNodeType.Gate => Palette.Gold,
        PNodeType.Start => Palette.Ember,
        _ => Palette.Text
    };

    public static string TypeName(PNodeType t) => t switch
    {
        PNodeType.Start => "燼種", PNodeType.Normal => "尋常燼紋", PNodeType.Magic => "魔法燼紋", PNodeType.Rare => "稀有燼紋",
        PNodeType.Legendary => "傳奇燼紋", PNodeType.Socket => "燼符槽", _ => "燼門"
    };

    /// <summary>Like StatInfo.Format, but also reads well for penalties.</summary>
    public static string Fmt(Stat s, float v)
    {
        if (v >= 0) return StatInfo.Format(s, v);
        string num = StatInfo.IsPct(s) ? $"{-v:0.#}%" : $"{MathF.Round(-v):0}";
        return $"-{num} {StatInfo.Name(s)}";
    }

    /// <summary>Glyph values are small per step, so they keep two decimals instead of rounding.</summary>
    public static string FmtFine(Stat s, float v) => StatInfo.IsPct(s) ? $"+{v:0.##}% {StatInfo.Name(s)}" : $"+{v:0.#} {StatInfo.Name(s)}";

    // ───────────────────────── nodes ─────────────────────────

    public static string NodeTooltip(Hero h, int board, PNode n)
    {
        var sb = new StringBuilder(512);
        var boards = h.ParagonBoards;
        var st = board >= 0 && board < boards.Count ? boards[board] : null;
        string dim = Hex(Palette.TextDim), gold = Hex(Palette.Gold), good = Hex(Palette.Good), bad = Hex(Palette.Bad);
        var col = n.Type == PNodeType.Normal && n.Stats.Length > 0 ? AttrColor(n.Stats[0].s) : TypeColor(n.Type);

        sb.Append($"[font_size=26][color=#{Hex(col)}]{n.Name}[/color][/font_size]\n");
        sb.Append($"[color=#{dim}]{TypeName(n.Type)}{(st != null ? "　" + st.Def.Name : "")}[/color]\n\n");

        if (n.Type == PNodeType.Legendary && !string.IsNullOrEmpty(n.Desc))
        {
            if (n.Hook != null) sb.Append($"[color=#{Hex(Palette.Rarity(Rarity.Legendary))}]✹ {n.Desc}[/color]\n");
            else sb.Append($"[i][color=#{dim}]「{n.Desc}」[/color][/i]\n");
        }
        foreach (var (s, v) in n.Stats) sb.Append($"◆ {Fmt(s, v)}\n");

        if (n.Type == PNodeType.Rare && n.Bonus.Length > 0)
        {
            int need = h.ParagonRareThreshold(board, n);
            float have = h.ParagonAttr(n.Req);
            bool met = have >= need;
            foreach (var (s, v) in n.Bonus) sb.Append($"[color=#{(met ? good : dim)}]◇ {Fmt(s, v)}[/color]\n");
            sb.Append($"[color=#{(met ? good : bad)}]　需要{StatInfo.Name(n.Req)} {need}（目前 {have:0}）[/color]\n");
        }
        else if (n.Type == PNodeType.Gate)
        {
            sb.Append($"\n[color=#{dim}]點亮後，可在門外接續一面新的燼紋盤（盤面合計最多 {ParagonDb.MaxBoards} 面）。[/color]\n");
        }
        else if (n.Type == PNodeType.Start)
        {
            sb.Append($"[color=#{dim}]{n.Desc}[/color]\n");
        }
        else if (n.Type == PNodeType.Socket)
        {
            string gid = h.SocketedGlyph(board);
            var g = GlyphDb.Get(gid);
            if (g == null) sb.Append($"[color=#{dim}]尚未嵌入燼符。點亮此槽後，從右側的清單選一枚燼符嵌入。[/color]\n");
            else
            {
                var ev = h.EvalGlyph(board, gid);
                sb.Append($"[color=#{Hex(AttrColor(g.Attr))}]{g.Name}[/color]　等級 {ev.Level}\n");
                GlyphLines(sb, g, ev.Level, ev.Points, true);
            }
        }

        sb.Append('\n');
        bool owned = h.ParagonOwned(board, n.Index);
        if (n.Type == PNodeType.Start) sb.Append($"[color=#{dim}]燼種永遠燃著，無法熄滅。[/color]");
        else if (!h.ParagonUnlocked) sb.Append($"[color=#{dim}]{h.WhyCantTakeParagon(board, n.Index)}[/color]");
        else if (owned)
        {
            if (n.Type == PNodeType.Socket && h.SocketedGlyph(board) != null) sb.Append($"[color=#{gold}]右鍵：取下燼符[/color]");
            else
            {
                string why = h.WhyCantRefundParagon(board, n.Index);
                sb.Append(why == null ? $"[color=#{dim}]已點亮　右鍵：熄滅並取回 1 點[/color]" : $"[color=#{dim}]已點亮　{why}[/color]");
                if (n.Type == PNodeType.Socket) sb.Append($"\n[color=#{gold}]左鍵：選定此槽，再從清單嵌入燼符[/color]");
            }
        }
        else
        {
            string why = h.WhyCantTakeParagon(board, n.Index);
            sb.Append(why == null ? $"[color=#{gold}]左鍵：點亮（消耗 1 點燼紋點）[/color]" : $"[color=#{bad}]{why}[/color]");
        }
        return sb.ToString();
    }

    // ───────────────────────── glyphs ─────────────────────────

    /// <summary>The rules of a glyph at a level; with <paramref name="points"/> ≥ 0 also what it grants right now.</summary>
    public static void GlyphLines(StringBuilder sb, GlyphDef g, int level, float points, bool showNow)
    {
        string dim = Hex(Palette.TextDim), good = Hex(Palette.Good);
        string attr = StatInfo.Name(g.Attr);
        level = Math.Clamp(level, 1, GlyphDb.MaxLevel);
        int radius = GlyphDb.Radius(level);
        float mul = GlyphDb.LevelMul(level);
        sb.Append($"[color=#{dim}]半徑 {radius} 格內，每點亮 5 點{attr}：[/color]\n");
        foreach (var (s, per5) in g.Scale) sb.Append($"◆ {FmtFine(s, per5 * mul)}\n");
        bool met = showNow && points >= g.Threshold;
        sb.Append($"[color=#{dim}]範圍內{attr}達 {g.Threshold} 點時：[/color]\n");
        foreach (var (s, v) in g.Bonus) sb.Append($"[color=#{(met ? good : dim)}]◇ {Fmt(s, v)}[/color]\n");
        if (showNow)
        {
            sb.Append($"\n範圍內{attr}：[color=#{Hex(AttrColor(g.Attr))}]{points:0}[/color] 點\n");
            foreach (var (s, per5) in g.Scale) sb.Append($"[color=#{good}]　→ {FmtFine(s, per5 * mul * points / 5f)}[/color]\n");
        }
    }

    public static string GlyphTooltip(Hero h, GlyphDef g)
    {
        var sb = new StringBuilder(512);
        string dim = Hex(Palette.TextDim), gold = Hex(Palette.Gold);
        sb.Append($"[font_size=26][color=#{Hex(AttrColor(g.Attr))}]{g.Name}[/color][/font_size]\n");
        if (!h.GlyphUnlocked(g.Id))
        {
            sb.Append($"[color=#{dim}]沉眠中　燼紋等級 {h.GlyphUnlockLevel(g.Id)} 時甦醒[/color]\n\n");
            GlyphLines(sb, g, 1, 0, false);
            sb.Append($"\n[i][color=#{dim}]「{g.Flavor}」[/color][/i]");
            return sb.ToString();
        }
        int level = h.GlyphLevel(g.Id);
        int at = h.GlyphBoard(g.Id);
        sb.Append($"[color=#{dim}]等級 {level}／{GlyphDb.MaxLevel}　{(at >= 0 ? "嵌於「" + h.ParagonBoards[at].Def.Name + "」" : "尚未嵌入")}[/color]\n\n");
        if (at >= 0) GlyphLines(sb, g, level, h.EvalGlyph(at, g.Id).Points, true);
        else GlyphLines(sb, g, level, 0, false);
        if (level < 25) sb.Append($"\n[color=#{dim}]等級 25 時，半徑擴為 4 格。[/color]");
        else if (level < 51) sb.Append($"\n[color=#{dim}]等級 51 時，半徑擴為 5 格。[/color]");
        if (level < GlyphDb.MaxLevel)
        {
            sb.Append($"\n[color=#{dim}]每提升一級，效果增加 2%。[/color]");
            if (h.GlyphTries > 0) sb.Append($"\n[color=#{gold}]淬鍊成功率 {h.GlyphUpgradeChance(g.Id) * 100f:0}%（裂隙第 {h.GlyphTryTierFor(g.Id)} 層的機會）[/color]");
            else sb.Append($"\n[color=#{dim}]通關深淵裂隙可獲得淬鍊機會。[/color]");
        }
        sb.Append($"\n\n[i][color=#{dim}]「{g.Flavor}」[/color][/i]");
        return sb.ToString();
    }

    /// <summary>The compact text of one row in the glyph list.</summary>
    public static string GlyphRow(Hero h, GlyphDef g)
    {
        var sb = new StringBuilder(256);
        string dim = Hex(Palette.TextDim);
        string attr = StatInfo.Name(g.Attr);
        bool unlocked = h.GlyphUnlocked(g.Id);
        int level = Math.Max(1, h.GlyphLevel(g.Id));
        float mul = GlyphDb.LevelMul(level);
        if (!unlocked)
        {
            sb.Append($"[color=#{dim}][font_size=20]{g.Name}[/font_size]　燼紋等級 {h.GlyphUnlockLevel(g.Id)} 時甦醒\n");
            sb.Append($"每 5 點{attr}：{ScaleText(g, 1f)}[/color]");
            return sb.ToString();
        }
        int at = h.GlyphBoard(g.Id);
        sb.Append($"[font_size=20][color=#{Hex(AttrColor(g.Attr))}]{g.Name}[/color][/font_size]　等級 {level}　[color=#{dim}]半徑 {GlyphDb.Radius(level)}[/color]\n");
        sb.Append($"每 5 點{attr}：{ScaleText(g, mul)}\n");
        if (at >= 0)
        {
            var ev = h.EvalGlyph(at, g.Id);
            sb.Append($"[color=#{Hex(Palette.Gold)}]嵌於「{h.ParagonBoards[at].Def.Name}」[/color]　範圍內{attr} {ev.Points:0} 點");
            if (ev.Bonus) sb.Append($"　[color=#{Hex(Palette.Good)}]額外效果已生效[/color]");
        }
        else sb.Append($"[color=#{dim}]尚未嵌入[/color]");
        return sb.ToString();
    }

    private static string ScaleText(GlyphDef g, float mul)
    {
        var sb = new StringBuilder(48);
        for (int i = 0; i < g.Scale.Length; i++)
        {
            if (i > 0) sb.Append('、');
            sb.Append(FmtFine(g.Scale[i].s, g.Scale[i].per5 * mul));
        }
        return sb.ToString();
    }

    // ───────────────────────── boards ─────────────────────────

    /// <summary>What a board offers, for the picker shown at an open gate. <paramref name="ordinal"/> = how many boards are attached before it.</summary>
    public static string BoardSummary(Hero h, PBoard b, int ordinal)
    {
        var sb = new StringBuilder(512);
        string dim = Hex(Palette.TextDim), legend = Hex(Palette.Rarity(Rarity.Legendary)), rare = Hex(Palette.Rarity(Rarity.Rare));
        sb.Append($"[color=#{dim}]{b.Kind}[/color]　[i]{b.Desc}[/i]\n");
        var lg = b.Legendary;
        if (lg != null)
        {
            sb.Append($"[color=#{legend}]✹ {lg.Name}[/color]　");
            if (lg.Hook != null) sb.Append(lg.Desc).Append('　');
            for (int i = 0; i < lg.Stats.Length; i++) { if (i > 0) sb.Append('、'); sb.Append(Fmt(lg.Stats[i].s, lg.Stats[i].v)); }
            sb.Append('\n');
        }
        sb.Append($"[color=#{rare}]稀有燼紋[/color]　");
        bool first = true;
        foreach (var n in b.Nodes)
        {
            if (n.Type != PNodeType.Rare) continue;
            if (!first) sb.Append('、');
            first = false;
            sb.Append(n.Name).Append('（').Append(StatInfo.Name(n.Stats[0].s));
            if (n.Bonus.Length > 0) sb.Append('／').Append(StatInfo.Name(n.Bonus[0].s));
            sb.Append('）');
        }
        sb.Append('\n');
        sb.Append($"[color=#{dim}]盤面屬性[/color]　");
        for (int d = 0; d < 4; d++)
        {
            if (d > 0) sb.Append('　');
            sb.Append($"{ParagonDb.DirNames[d]} [color=#{Hex(AttrColor(b.Sectors[d]))}]{StatInfo.Name(b.Sectors[d])}[/color]");
        }
        Stat main = Names.MainStat(h.Class);
        sb.Append($"\n[color=#{dim}]作為第 {ordinal + 1} 面盤，稀有燼紋的門檻為：{StatInfo.Name(main)} {ParagonDb.RareThreshold(h.Class, main, ordinal)}，其餘屬性 {ParagonDb.RareThreshold(h.Class, Stat.Count, ordinal)}[/color]");
        return sb.ToString();
    }
}
