using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>The authored, qualitative part of one base's guide entry (everything numeric comes from GearBase at runtime).</summary>
public sealed class BaseNotes
{
    public string Style;          // how it plays
    public string Strong, Weak;   // strengths, weaknesses
    public string[] Fits;         // best fit tags: 暴擊流, 控場流, 打王, 清怪, 坦 …
}

/// <summary>
/// 武器指南 data. Hands, speeds, spreads, implicits and the class loadouts are read from ItemDb at runtime, so the guide
/// never drifts from the game; the text here is only the trade-offs in words, one short entry per base, plus notes
/// where a class uses a base in its own way. No damage figures are written here: balance is FIXES' (a relative damage
/// indicator could be read from a table FIXES maintains; there is none yet, so the guide shows none).
/// </summary>
public static class WeaponGuide
{
    public static readonly Dictionary<string, BaseNotes> Notes = new()
    {
        ["sword1"] = new() { Style = "出手俐落的單手劍，速度與穩定兼顧。", Strong = "攻速快、傷害起伏小；內建爆擊傷害，配合爆擊詞綴越堆越強。", Weak = "沒有控場或破甲的特長，單看內建屬性較平凡。", Fits = new[] { "暴擊流", "清怪" } },
        ["axe1"] = new() { Style = "劈砍沉重的單手斧，打的是破綻。", Strong = "內建易傷傷害，對陷入易傷的敵人特別有效；與造成易傷的技能是好搭檔。", Weak = "傷害起伏大，偶爾手感不穩；沒有易傷來源時優勢不明顯。", Fits = new[] { "打王", "易傷流" } },
        ["mace1"] = new() { Style = "以重量取勝的單手錘。", Strong = "內建壓制傷害，壓制觸發時的重擊更痛；適合打硬目標。", Weak = "單手武器中速度最慢之一，命中效果觸發較少。", Fits = new[] { "打王", "壓制流" } },
        ["dagger"] = new() { Style = "最快的刃器，以手數與要害取勝。", Strong = "攻速最快、傷害最穩；內建爆擊機率，命中、幸運一擊與資源產生觸發最頻繁。", Weak = "每擊很輕，單發重擊或靠單次傷害的效果吃虧。", Fits = new[] { "暴擊流", "觸發流", "清怪" } },
        ["wand"] = new() { Style = "輕巧的施法媒介，搭配副手使用。", Strong = "施法快；內建資源產生，讓核心技能更常出手。", Weak = "單手 90% 傷害，需要副手的屬性補回來。", Fits = new[] { "資源循環", "清怪" } },
        ["scythe1"] = new() { Style = "喚魂師的單手鐮刃，刀刀見血。", Strong = "內建擊中恢復生命，續戰力好；攻速中上。", Weak = "傷害起伏大；續航屬性對輸出沒有直接幫助。", Fits = new[] { "續戰", "近戰召喚" } },
        ["flail"] = new() { Style = "甩動鏈球的連枷，最難預測的一擊。", Strong = "內建對受控敵人傷害，搭配暈眩、凍結、擊退的技能很強。", Weak = "傷害起伏最大；沒有控場時優勢消失。", Fits = new[] { "控場流", "坦" } },
        ["fist"] = new() { Style = "成對使用的拳刃，鍛身者的招牌。", Strong = $"攻速最快的武器之一，內建攻擊速度；雙持時兩手輪流、整體再快 {(ItemDb.DualSpeed - 1f) * 100f:0}%。", Weak = "每擊最輕；只有單手 90% 傷害，靠手數與職業加成補足。", Fits = new[] { "連擊", "觸發流", "清怪" } },
        ["sceptre"] = new() { Style = "端莊的權杖，為技能循環而生。", Strong = "內建冷卻時間縮減，終極與冷卻技能更常可用。", Weak = "沒有直接的傷害屬性；單手 90% 傷害。", Fits = new[] { "冷卻循環", "召喚" } },
        ["sword2"] = new() { Style = "大開大闔的雙手巨劍。", Strong = "雙手：詞綴 ×1.5；內建爆擊傷害是同類中最多的；速度在雙手武器中偏快、傷害穩定。", Weak = "不能使用副手；比單手慢。", Fits = new[] { "暴擊流", "清怪", "打王" } },
        ["axe2"] = new() { Style = "破甲的雙手巨斧。", Strong = "雙手：詞綴 ×1.5；大量易傷傷害，配易傷技能是打王利器。", Weak = "較慢、傷害起伏大；不能使用副手。", Fits = new[] { "打王", "易傷流" } },
        ["mace2"] = new() { Style = "最沉重的武器，一錘定音。", Strong = "雙手：詞綴 ×1.5；壓制傷害最高。", Weak = "全遊戲最慢：動作最長、命中效果觸發最少；不能使用副手。", Fits = new[] { "打王", "壓制流", "坦" } },
        ["staff"] = new() { Style = "施法者的雙手法杖。", Strong = "雙手：詞綴 ×1.5；內建核心技能傷害、傷害最穩。", Weak = "不能使用副手，少了副手的生存或施法屬性。", Fits = new[] { "核心技能", "清怪" } },
        ["polearm"] = new() { Style = "長柄刀，專斬強敵。", Strong = "雙手：詞綴 ×1.5；內建對菁英傷害。", Weak = "對雜兵沒有額外優勢；不能使用副手。", Fits = new[] { "打王", "菁英" } },
        ["scythe2"] = new() { Style = "收割靈魂的巨鐮。", Strong = "雙手：詞綴 ×1.5；大量擊中恢復生命，邊打邊回。", Weak = "傷害起伏大；續航屬性不直接加輸出；不能使用副手。", Fits = new[] { "續戰", "近戰召喚", "坦" } },
        ["bow"] = new() { Style = "張弓連射的長弓。", Strong = "雙手：詞綴 ×1.5；比弩快、傷害穩；內建基本技能傷害，連射更痛。", Weak = "每箭比弩輕；不能使用副手。", Fits = new[] { "基本技能", "清怪" } },
        ["crossbow"] = new() { Style = "裝填沉穩的重弩。", Strong = "雙手：詞綴 ×1.5；內建爆擊傷害；每發比長弓重。", Weak = "比長弓慢、起伏大；不能使用副手。", Fits = new[] { "暴擊流", "打王" } },
        ["shield"] = new() { Style = "以盾牌換取生存。", Strong = "內建格擋：被格擋的攻擊傷害減半；額外護甲；可擲出生命、抗性與傷害減免詞綴。", Weak = "不提供輸出；搭配單手武器只有 90% 傷害。", Fits = new[] { "坦", "穩健" } },
        ["focus"] = new() { Style = "聚焦法力的法器。", Strong = "內建傷害加成，補回單手的差距；可擲出元素傷害、爆擊與冷卻詞綴。", Weak = "不提供任何防禦。", Fits = new[] { "施法", "暴擊流" } },
        ["totem"] = new() { Style = "荒靈行者的獸骨圖騰。", Strong = "內建傷害加成；可擲出毒素、物理、僕從與生命恢復詞綴。", Weak = "不提供格擋或護甲。", Fits = new[] { "自然", "召喚" } },
        ["grimoire"] = new() { Style = "契約者的魔典。", Strong = "內建傷害加成；可擲出暗影、持續傷害、僕從與資源詞綴。", Weak = "不提供任何防禦。", Fits = new[] { "持續傷害", "召喚" } },
        ["lantern"] = new() { Style = "誓燈騎士的提燈。", Strong = "內建屏障強度與火焰傷害；可擲出治療、抗性與冷卻詞綴——攻守兼顧。", Weak = "沒有盾牌的格擋與護甲。", Fits = new[] { "聖光", "穩健" } },
    };

    /// <summary>Class-specific notes on a base (class, base) — how that class in particular uses it.</summary>
    public static readonly Dictionary<(ClassId, string), string> ClassNotes = new()
    {
        [(ClassId.Emberblade, "sword1")] = "雙持時兩手輪流出擊；劍＋斧可同時拿到爆擊傷害與易傷傷害。",
        [(ClassId.Emberblade, "axe1")] = "雙持搭配劍或錘，一手一種內建屬性。",
        [(ClassId.Hunter, "dagger")] = "雙持匕首是最快的配置，連擊點累積最快。",
        [(ClassId.Hunter, "bow")] = "遠程連射的主力；與弩的差別在速度與每發輕重。",
        [(ClassId.Hunter, "crossbow")] = "沿用影獵手原本的弩；每發較重，節奏較慢。",
        [(ClassId.Soulbinder, "shield")] = "喚魂師在僕從身後也需要自保：盾讓你站得更久。",
        [(ClassId.Soulbinder, "focus")] = "偏重自身施法輸出時的選擇。",
        [(ClassId.Wildsoul, "totem")] = "荒靈行者唯一的副手；變身時同樣生效。",
        [(ClassId.Forgebody, "fist")] = "鍛身者的招牌雙持：兩把拳刃輪流出拳。",
        [(ClassId.Forgebody, "mace2")] = "放棄連擊節奏換取沉重單擊。",
        [(ClassId.Lampknight, "shield")] = "持盾衝鋒與盾擊在任何配置都能施放，但只有拿盾才有格擋。",
        [(ClassId.Lampknight, "lantern")] = "以燼光取代盾牌：少了格擋，多了屏障與火焰。",
        [(ClassId.Pactbinder, "grimoire")] = "契約者的傳統配置：魔杖或權杖＋魔典。",
    };

    /// <summary>
    /// Class bonuses tied to a base (FIXES maintains the passives). The name is looked up among the class's passives at
    /// runtime: when it exists its own description is shown; until then the authored line below stands in.
    /// </summary>
    public static readonly List<(ClassId cls, string baseId, string name, string fallback)> Bonuses = new()
    {
        (ClassId.Forgebody, "fist", "拳刃精通", $"雙手皆持拳刃時，武器傷害 +{Hero.FistMasteryBase:0}%。"),
        (ClassId.Lampknight, "shield", "盾誓", $"單手武器配盾時，盾的格擋率每 1%，武器傷害 +{Hero.ShieldOathPerBlockPct:0.#}%。"),
        // 燈誓 is off (Hero.LanternOathBasePct = 0): the lantern's own fire and barrier already put it level with mace2
    };

    public static IEnumerable<(string name, string desc)> BonusesFor(ClassId c, string baseId)
    {
        foreach (var b in Bonuses)
        {
            if (b.cls != c || b.baseId != baseId) continue;
            var p = SkillDb.PassivesOf(c).FirstOrDefault(x => x.Name == b.name);
            yield return (b.name, p?.Desc ?? b.fallback);
        }
    }

    /// <summary>The class's bases grouped by stance: 雙手, then 單手＋副手 (one-hand bases and off-hands) or 單手／雙持.</summary>
    public static List<(string title, List<string> bases)> Groups(ClassId c)
    {
        var g = ItemDb.Loadout(c);
        var l = new List<(string, List<string>)>();
        if (g.TwoHand.Length > 0) l.Add(("雙手", g.TwoHand.ToList()));
        if (g.OffHand.Length > 0) l.Add(("單手＋副手", g.MainHand.Concat(g.OffHand).ToList()));
        else if (g.CanDual) l.Add(("雙持", g.MainHand.ToList()));
        else if (g.MainHand.Length > 0) l.Add(("單手", g.MainHand.ToList()));
        return l;
    }

    /// <summary>Every base the class can use (each once).</summary>
    public static List<string> BasesOf(ClassId c) => Groups(c).SelectMany(x => x.bases).Distinct().ToList();

    /// <summary>The bases this one competes with for the same hand(s) in this class: same hand count, or the other off-hands.</summary>
    public static List<string> Alternatives(ClassId c, string baseId)
    {
        var b = ItemDb.BaseOf(baseId);
        if (b == null) return new List<string>();
        return BasesOf(c).Where(x => x != baseId && ItemDb.BaseOf(x) is { } o && o.Off == b.Off && (b.Off || o.Hands == b.Hands)).ToList();
    }

    /// <summary>The same base made at a power, for icons and the implicit values shown (common, no affixes).</summary>
    public static Item Sample(string baseId, int power, ClassId c)
    {
        var b = ItemDb.BaseOf(baseId);
        if (b == null) return null;
        var it = ItemDb.Generate(power, Rarity.Common, c, b.Off ? ItemKind.OffHand : ItemKind.Weapon, new Random(baseId.GetHashCode() ^ power), baseId);
        it.ReqLevel = 1;
        return it;
    }

    private static string Pct(float f) => $"{MathF.Abs(f) * 100f:0}%";

    /// <summary>
    /// One base against another in plain words, from the data: speed (and so the weight of a hit), damage spread,
    /// hands, the built-in stat. E.g. 「比巨錘快 19%，每擊較輕；傷害較穩定」.
    /// </summary>
    public static string Compare(string a, string b)
    {
        var x = ItemDb.BaseOf(a); var y = ItemDb.BaseOf(b);
        if (x == null || y == null) return "";
        var parts = new List<string>();
        if (!x.Off && !y.Off)
        {
            float d = x.Aps / y.Aps - 1f;
            if (d > 0.02f) parts.Add($"比{y.Name}快 {Pct(d)}，每擊較輕；命中效果觸發較頻繁");
            else if (d < -0.02f) parts.Add($"比{y.Name}慢 {Pct(d)}，每擊較重；動作較長");
            else parts.Add($"與{y.Name}速度相同");
            if (x.Spread > y.Spread + 0.04f) parts.Add("傷害起伏較大");
            else if (x.Spread < y.Spread - 0.04f) parts.Add("傷害較穩定");
            if (x.Hands != y.Hands) parts.Add(x.Hands == 2 ? "雙手：詞綴 ×1.5，但不能用副手" : "單手：可搭配副手或雙持");
        }
        else parts.Add($"與{y.Name}相比");
        var ix = x.Implicit.FirstOrDefault(); var iy = y.Implicit.FirstOrDefault();
        if (x.Implicit.Length > 0 && y.Implicit.Length > 0 && ix.s != iy.s) parts.Add($"內建{StatInfo.Name(ix.s)}（{y.Name}：{StatInfo.Name(iy.s)}）");
        else if (x.Implicit.Length > 0 && y.Implicit.Length > 0) parts.Add($"同樣內建{StatInfo.Name(ix.s)}，數值{(ix.hi > iy.hi + 0.01f ? "較高" : ix.hi < iy.hi - 0.01f ? "較低" : "相同")}");
        if (x.Off && y.Off && (x.Id == "shield") != (y.Id == "shield")) parts.Add(x.Id == "shield" ? "有格擋與護甲，但不加輸出" : "加輸出，但沒有格擋");
        return string.Join("；", parts) + "。";
    }

    /// <summary>The worn item the hero would compare this base with (the main hand for a weapon, the off hand for an off-hand).</summary>
    public static Item EquippedAlternative(Hero h, string baseId)
    {
        var b = ItemDb.BaseOf(baseId);
        if (h == null || b == null) return null;
        return b.Off ? h.OffHand ?? (h.OffHandBlocked ? h.MainHand : null) : h.MainHand ?? (h.OffHand?.Kind == ItemKind.Weapon ? h.OffHand : null);
    }

    /// <summary>The general rules section (numbers from ItemDb / Combat constants).</summary>
    public static string[] Rules() => new[]
    {
        $"雙手武器：佔用兩手，詞綴強度 ×{ItemDb.TwoHandAffixMul:0.#}；不能使用副手。裝上雙手武器時，副手會收回行囊。",
        $"單手武器：傷害為雙手的 {ItemDb.OneHandDps * 100f:0}%，另一隻手可以拿副手，或（可雙持的職業）再拿一把單手武器。",
        $"雙持：兩手輪流攻擊，每把武器保有自己的傷害；攻擊速度取兩把的平均，再快 {(ItemDb.DualSpeed - 1f) * 100f:0}%。兩把武器的詞綴都會生效，但副手武器的基礎屬性效果減半。",
        "副手：盾、法器、圖騰、魔典、燈各有自己的詞綴類別與內建屬性；只能裝在副手。",
        $"格擋：盾牌內建格擋機率，被格擋的攻擊只受 {Combat.BlockedShare * 100f:0}% 傷害；格擋機率上限 50%。",
        "攻擊速度：每把武器有自己的速度。速度快的武器出手多、每擊較輕，命中觸發的效果（擊中回復、幸運一擊、資源產生）更頻繁；慢的武器每擊較重、動作較長。單看武器本身，各武器的每秒傷害大致相同，差別在手感、內建屬性與雙手的詞綴加成（職業加成另計）。",
        "傷害起伏：每擊傷害在一個範圍內浮動；起伏小的武器表現穩定，起伏大的偶有高峰。",
        "內建屬性（◇）：每種武器天生的屬性，隨物品強度成長，不能附魔或淬火。",
    };
}
