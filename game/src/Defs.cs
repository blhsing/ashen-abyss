using System;
using System.Collections.Generic;

namespace AshenAbyss;

public enum ClassId { Emberblade, Astromancer, Hunter, Soulbinder, Wildsoul, Forgebody, Lampknight, Pactbinder }
public enum Element { Physical, Fire, Frost, Lightning, Poison, Shadow }
public enum Rarity { Common, Magic, Rare, Legendary, Unique }
// Saves store these as integers: new members go at the END so 0.1–0.3 saves keep their meaning.
// ItemKind.Weapon = any wielded weapon (1H or 2H, see Item.Base); ItemKind.OffHand = shield / focus / totem / grimoire / lantern.
public enum ItemKind { Weapon, Helm, Chest, Gloves, Pants, Boots, Amulet, Ring, Gem, OffHand }
// MainHand takes the old Weapon index (0), so an old save's weapon lands in the main hand with nothing to convert.
// OffHand holds an off-hand item or, for dual-wield classes, a second one-hand weapon; a two-hand weapon blocks it.
public enum EquipSlot { MainHand, Helm, Chest, Gloves, Pants, Boots, Amulet, Ring1, Ring2, OffHand }
public enum SkillCat { Basic, Core, Defensive, Mobility, Mastery, Ultimate }
public enum Faction { Player, Monster }

public enum Stat
{
    Str, Int, Dex, Wil,
    MaxLife, LifePct, Armor, ResAll, ResFire, ResFrost, ResLightning, ResPoison, ResShadow,
    DmgReduction, Dodge, LifeRegen, LifeOnHit, LifeOnKill, Healing, Thorns, BarrierPct,
    DmgAll, DmgPhysical, DmgFire, DmgFrost, DmgLightning, DmgPoison, DmgShadow, DmgDot, DmgElite, DmgCC, DmgVuln, DmgOverpower,
    DmgBasic, DmgCore, DmgUlt, MinionDmg,
    AttackSpeed, CritChance, CritDmg, LuckyHit,
    MaxResource, ResourceGen, ResourceCost, Cdr,
    MoveSpeed, GoldFind, MagicFind, XpGain, SkillRanks,
    Block,
    GolemLife, GolemDmg, MageDmg, MinionAtkSpeed, CmdCdr,      // [golem] Soulbinder off-hand affixes (GolemItems.cs)
    Count
}

public static class StatInfo
{
    // name, isPercent
    private static readonly (string name, bool pct)[] Info = new (string, bool)[(int)Stat.Count];

    static StatInfo()
    {
        void S(Stat s, string n, bool pct = true) => Info[(int)s] = (n, pct);
        S(Stat.Str, "力量", false); S(Stat.Int, "智力", false); S(Stat.Dex, "敏捷", false); S(Stat.Wil, "意志", false);
        S(Stat.MaxLife, "生命上限", false); S(Stat.LifePct, "生命上限"); S(Stat.Armor, "護甲", false);
        S(Stat.ResAll, "全元素抗性"); S(Stat.ResFire, "火焰抗性"); S(Stat.ResFrost, "冰霜抗性"); S(Stat.ResLightning, "閃電抗性"); S(Stat.ResPoison, "毒素抗性"); S(Stat.ResShadow, "暗影抗性");
        S(Stat.DmgReduction, "傷害減免"); S(Stat.Dodge, "閃避機率"); S(Stat.LifeRegen, "每秒生命恢復", false);
        S(Stat.LifeOnHit, "擊中恢復生命", false); S(Stat.LifeOnKill, "擊殺恢復生命", false); S(Stat.Healing, "受到治療效果");
        S(Stat.Thorns, "荊棘傷害", false); S(Stat.BarrierPct, "屏障強度");
        S(Stat.DmgAll, "傷害"); S(Stat.DmgPhysical, "物理傷害"); S(Stat.DmgFire, "火焰傷害"); S(Stat.DmgFrost, "冰霜傷害");
        S(Stat.DmgLightning, "閃電傷害"); S(Stat.DmgPoison, "毒素傷害"); S(Stat.DmgShadow, "暗影傷害"); S(Stat.DmgDot, "持續傷害");
        S(Stat.DmgElite, "對菁英傷害"); S(Stat.DmgCC, "對受控敵人傷害"); S(Stat.DmgVuln, "易傷傷害"); S(Stat.DmgOverpower, "壓制傷害");
        S(Stat.DmgBasic, "基本技能傷害"); S(Stat.DmgCore, "核心技能傷害"); S(Stat.DmgUlt, "終極技能傷害"); S(Stat.MinionDmg, "僕從傷害");
        S(Stat.AttackSpeed, "攻擊速度"); S(Stat.CritChance, "爆擊機率"); S(Stat.CritDmg, "爆擊傷害"); S(Stat.LuckyHit, "幸運一擊機率");
        S(Stat.MaxResource, "資源上限", false); S(Stat.ResourceGen, "資源產生"); S(Stat.ResourceCost, "資源消耗減少"); S(Stat.Cdr, "冷卻時間縮減");
        S(Stat.MoveSpeed, "移動速度"); S(Stat.GoldFind, "金幣獲取"); S(Stat.MagicFind, "尋寶率"); S(Stat.XpGain, "經驗值獲取");
        S(Stat.SkillRanks, "所有技能等級", false);
        S(Stat.Block, "格擋機率");
        S(Stat.GolemLife, "魔像生命"); S(Stat.GolemDmg, "魔像傷害"); S(Stat.MageDmg, "骸骨術士傷害"); S(Stat.MinionAtkSpeed, "僕從攻擊速度"); S(Stat.CmdCdr, "魔像號令冷卻縮減");
    }

    public static string Name(Stat s) => Info[(int)s].name;
    public static bool IsPct(Stat s) => Info[(int)s].pct;

    public static string Format(Stat s, float v)
    {
        string num = IsPct(s) ? $"{v:0.#}%" : $"{MathF.Round(v):0}";
        return $"+{num} {Name(s)}";
    }

    public static Stat DmgOf(Element e) => e switch
    {
        Element.Fire => Stat.DmgFire, Element.Frost => Stat.DmgFrost, Element.Lightning => Stat.DmgLightning, Element.Poison => Stat.DmgPoison,
        Element.Shadow => Stat.DmgShadow, _ => Stat.DmgPhysical
    };

    public static Stat ResOf(Element e) => e switch
    {
        Element.Fire => Stat.ResFire, Element.Frost => Stat.ResFrost, Element.Lightning => Stat.ResLightning, Element.Poison => Stat.ResPoison,
        Element.Shadow => Stat.ResShadow, _ => Stat.Count
    };
}

/// <summary>A flat bag of stat values. Sources (base, gear, talents, buffs) are summed into one of these.</summary>
public sealed class StatBlock
{
    public readonly float[] V = new float[(int)Stat.Count];
    public float this[Stat s] { get => V[(int)s]; set => V[(int)s] = value; }
    public void Add(Stat s, float v) => V[(int)s] += v;
    public void AddAll(StatBlock o, float mul = 1f) { for (int i = 0; i < V.Length; i++) V[i] += o.V[i] * mul; }
    public void Clear() => Array.Clear(V);
}

public static class Names
{
    public const int ClassCount = 8;
    private static readonly string[] ClassNames = { "燼刃衛", "星術師", "影獵手", "喚魂師", "荒靈行者", "鍛身者", "誓燈騎士", "縛淵術士" };
    public static readonly string[] ClassKeys = { "emberblade", "astromancer", "hunter", "soulbinder", "wildsoul", "forgebody", "lampknight", "pactbinder" };
    private static readonly string[] ClassDescs =
    {
        "身披重甲的近戰守衛，以巨劍與怒焰撕裂敵陣。攻擊累積怒焰，再以毀滅性的重擊宣洩。",
        "操縱火焰、冰霜與閃電的施法者。身軀脆弱，卻能在遠處降下星辰之怒。",
        "遊走於陰影的獵手，弓弩與短刃並用。迅捷、致命，以連擊點強化殺招。",
        "與亡魂締約的喚魂師。汲取殘魂喚起骸骨僕從，以暗影與詛咒凋零眾敵。",
        "枯林深處的蛻形者。借熊靈之軀硬撼強敵，以狼靈之爪撕裂獵物，並號令大地與風暴。",
        $"在熄滅的日爐中鍛鍊肉身的武者。雙手套上拳刃，以拳、掌與氣焰擊碎一切；必要時也掄起巨錘與巨斧。「鍛體」使其受到的傷害降低 {Hero.ForgedBodyDrPct:0}%；雙持拳刃時，「拳刃精通」提高武器傷害。",
        "守護燼塔的持盾騎士。以信念驅動劍與盾，燃起燼光庇護自己，並審判寂滅的信徒。單手持盾時，「盾誓」依盾的格擋率提高武器傷害。",
        "與燼魔立約的術士。以咒力施法累積契印，再以契印號令被縛的魔物代為殺戮。「契約初成」：等級 8 前傷害與生命提高。",
    };
    private static readonly string[] ResourceNames = { "怒焰", "靈能", "精力", "魂能", "靈息", "氣焰", "信念", "咒力" };
    private static readonly Stat[] MainStats = { Stat.Str, Stat.Int, Stat.Dex, Stat.Wil, Stat.Wil, Stat.Dex, Stat.Str, Stat.Int };
    /// <summary>True for classes whose resource is built by attacking and decays out of combat (starts empty).</summary>
    private static readonly bool[] Builder = { true, false, false, false, true, true, true, false };
    private static readonly float[] ResourceRegen = { 0f, 8f, 12f, 5f, 0f, 0f, 0f, 9f };
    private static readonly Godot.Color[] ClassColors =
    {
        new(1f, 0.5f, 0.15f), new(0.4f, 0.7f, 1f), new(0.25f, 0.85f, 0.7f), new(0.7f, 0.9f, 0.6f),
        new(0.55f, 0.85f, 0.4f), new(1f, 0.38f, 0.12f), new(1f, 0.85f, 0.45f), new(0.9f, 0.25f, 0.45f),
    };
    private static readonly Godot.Color[] ResourceColors =
    {
        new(0.95f, 0.4f, 0.08f), new(0.2f, 0.45f, 1f), new(0.95f, 0.8f, 0.2f), new(0.35f, 0.85f, 0.65f),
        new(0.45f, 0.75f, 0.3f), new(1f, 0.55f, 0.15f), new(1f, 0.9f, 0.55f), new(0.65f, 0.2f, 0.85f),
    };

    public static string Class(ClassId c) => ClassNames[(int)c];
    public static string ClassKey(ClassId c) => ClassKeys[(int)c];
    public static string ClassDesc(ClassId c) => ClassDescs[(int)c];
    public static string Resource(ClassId c) => ResourceNames[(int)c];
    public static Stat MainStat(ClassId c) => MainStats[(int)c];
    public static bool IsBuilder(ClassId c) => Builder[(int)c];
    public static float Regen(ClassId c) => ResourceRegen[(int)c];
    public static Godot.Color ClassColor(ClassId c) => ClassColors[(int)c];
    public static Godot.Color ResourceColor(ClassId c) => ResourceColors[(int)c];

    public static string Element(Element e) => e switch
    {
        AshenAbyss.Element.Fire => "火焰", AshenAbyss.Element.Frost => "冰霜", AshenAbyss.Element.Lightning => "閃電",
        AshenAbyss.Element.Poison => "毒素", AshenAbyss.Element.Shadow => "暗影", _ => "物理"
    };

    public static string Rarity(Rarity r) => r switch
    {
        AshenAbyss.Rarity.Magic => "魔法", AshenAbyss.Rarity.Rare => "稀有", AshenAbyss.Rarity.Legendary => "傳奇",
        AshenAbyss.Rarity.Unique => "獨特", _ => "普通"
    };

    public static string Kind(ItemKind k) => k switch
    {
        ItemKind.Weapon => "武器", ItemKind.Helm => "頭盔", ItemKind.Chest => "胸甲", ItemKind.Gloves => "手套",
        ItemKind.Pants => "護腿", ItemKind.Boots => "靴子", ItemKind.Amulet => "護符", ItemKind.Ring => "戒指", ItemKind.OffHand => "副手", _ => "寶石"
    };

    public static string SkillCat(SkillCat c) => c switch
    {
        AshenAbyss.SkillCat.Basic => "基本", AshenAbyss.SkillCat.Core => "核心", AshenAbyss.SkillCat.Defensive => "防禦",
        AshenAbyss.SkillCat.Mobility => "機動", AshenAbyss.SkillCat.Mastery => "專精", _ => "終極"
    };
}

/// <summary>Colours shared by loot beams, labels, tooltips and damage text so rarity/element read without words.</summary>
public static class Palette
{
    public static Godot.Color Rarity(Rarity r) => r switch
    {
        AshenAbyss.Rarity.Magic => new Godot.Color(0.42f, 0.62f, 1f),
        AshenAbyss.Rarity.Rare => new Godot.Color(1f, 0.86f, 0.3f),
        AshenAbyss.Rarity.Legendary => new Godot.Color(1f, 0.52f, 0.12f),
        AshenAbyss.Rarity.Unique => new Godot.Color(0.86f, 0.72f, 0.45f),
        _ => new Godot.Color(0.82f, 0.82f, 0.8f)
    };

    public static Godot.Color Element(Element e) => e switch
    {
        AshenAbyss.Element.Fire => new Godot.Color(1f, 0.45f, 0.1f),
        AshenAbyss.Element.Frost => new Godot.Color(0.45f, 0.8f, 1f),
        AshenAbyss.Element.Lightning => new Godot.Color(0.75f, 0.7f, 1f),
        AshenAbyss.Element.Poison => new Godot.Color(0.55f, 0.9f, 0.2f),
        AshenAbyss.Element.Shadow => new Godot.Color(0.6f, 0.25f, 0.9f),
        _ => new Godot.Color(0.9f, 0.88f, 0.82f)
    };

    public static readonly Godot.Color Gold = new(1f, 0.83f, 0.35f);
    public static readonly Godot.Color Ember = new(1f, 0.5f, 0.15f);
    public static readonly Godot.Color Text = new(0.86f, 0.82f, 0.74f);
    public static readonly Godot.Color TextDim = new(0.58f, 0.55f, 0.5f);
    public static readonly Godot.Color Good = new(0.45f, 0.9f, 0.4f);
    public static readonly Godot.Color Bad = new(0.95f, 0.3f, 0.25f);
    public static readonly Godot.Color Danger = new(1f, 0.22f, 0.08f);
}

public static class Rng
{
    public static readonly Random R = new();
    public static float F() => (float)R.NextDouble();
    public static float Range(float a, float b) => a + (b - a) * (float)R.NextDouble();
    public static int Range(int a, int bInclusive) => R.Next(a, bInclusive + 1);
    public static bool Chance(float p) => R.NextDouble() < p;
    public static T Pick<T>(IList<T> l) => l[R.Next(l.Count)];
    public static T Pick<T>(IList<T> l, Random r) => l[r.Next(l.Count)];
}
