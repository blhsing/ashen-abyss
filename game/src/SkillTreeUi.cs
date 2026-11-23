using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AshenAbyss;

/// <summary>
/// BBCode text for skills, upgrades, passives and specialisations. Shared by the skill tree and the HUD action bar.
/// Depends only on the data classes and on <see cref="UiTheme.Fs"/> for the size of the heading line, so it is safe
/// to call from anywhere. Nothing is cached: every call builds the text for the text scale in force.
/// The <c>[font_size=N]</c> tags it writes are FINAL (already through UiTheme.Fs): show these strings as they are and
/// never send them through <c>UiTheme.Bb</c>, which would scale the sizes a second time.
/// </summary>
public static class SkillText
{
    /// <summary>DESIGN sizes (px at 1080p) of a tooltip's heading line: a skill's name, and every other title (upgrade, passive, specialisation, slot, tier).</summary>
    public const int NameSize = 30, TitleSize = 28;

    /// <summary>Labels of the six action slots, in <c>SaveData.Bar</c> order.</summary>
    public static readonly string[] SlotKeys = { "左鍵", "右鍵", "1", "2", "3", "4" };

    public static readonly Color NameColor = new(0.98f, 0.93f, 0.82f);
    private static readonly Color NumColor = new(0.99f, 0.95f, 0.84f);
    private static readonly string NumHex = NumColor.ToHtml(false);
    private static readonly Regex NumRx = new(@"\d+(?:\.\d+)?%?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly MatchEvaluator NumEval = m => $"[color=#{NumHex}]{m.Value}[/color]";
    private const string Mark = "\u0001";

    public static string Hex(Color c) => c.ToHtml(false);
    public static string Num(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>
    /// The heading line of a tooltip card. <paramref name="designSize"/> is a DESIGN size: it goes through
    /// <see cref="UiTheme.Fs"/> here, exactly once, and the number written into the tag is final.
    /// </summary>
    public static string Head(string text, int designSize = TitleSize) =>
        $"[font_size={UiTheme.Fs(designSize)}][color=#{Hex(NameColor)}]{text}[/color][/font_size]";

    /// <summary>Wraps every number of a plain description in a brighter colour so the figures read at a glance.</summary>
    public static string Highlight(string text) => string.IsNullOrEmpty(text) ? "" : NumRx.Replace(text, NumEval);

    public static Color CatColor(SkillCat c) => c switch
    {
        SkillCat.Basic => new Color(0.80f, 0.78f, 0.72f),
        SkillCat.Core => new Color(1f, 0.56f, 0.22f),
        SkillCat.Defensive => new Color(0.50f, 0.72f, 0.98f),
        SkillCat.Mobility => new Color(0.40f, 0.88f, 0.70f),
        SkillCat.Mastery => new Color(0.78f, 0.56f, 1f),
        _ => new Color(1f, 0.84f, 0.38f)
    };

    /// <summary>Name of the class's secondary gauge ("" when the class has none).</summary>
    public static string PipName(ClassId c) => c == ClassId.Hunter ? "連擊點" : c == ClassId.Pactbinder ? "契印" : "";
    private static string PipCount(ClassId c, int n) => c == ClassId.Hunter ? $"{n} 點連擊點" : $"{n} 枚契印";

    public static string FormName(int form) => form switch { 1 => "熊靈形", 2 => "狼靈形", 3 => "人形", _ => "" };

    /// <summary>The skill description with the damage figure of <paramref name="rank"/> filled in (and the next rank's when given).</summary>
    public static string Describe(SkillDef eff, int rank, int nextRank = 0)
    {
        if (eff == null) return "";
        string d = (eff.Desc ?? "").Replace("{0}", Mark);
        d = NumRx.Replace(d, NumEval);
        if (d.IndexOf(Mark, StringComparison.Ordinal) < 0) return d;
        string gold = Hex(Palette.Gold);
        int cur = eff.DmgPct(rank);
        string pct = $"[color=#{gold}]{cur}%[/color]", bare = $"[color=#{gold}]{cur}[/color]";
        if (nextRank > rank)
        {
            string good = Hex(Palette.Good);
            int nx = eff.DmgPct(nextRank);
            pct += $"[color=#{good}]（下一級 {nx}%）[/color]";
            bare += $"[color=#{good}]（下一級 {nx}）[/color]";
        }
        return d.Replace(Mark + "%", pct).Replace(Mark, bare);
    }

    /// <summary>Cost or generation, gauge use, cooldown and charges on one BBCode line ("" when the skill is free and instant).</summary>
    public static string CostLine(SkillDef eff, Hero h)
    {
        if (eff == null) return "";
        var parts = new List<string>(5);
        string res = Names.Resource(eff.Class);
        string rc = Hex(Names.ResourceColor(eff.Class).Lerp(Colors.White, 0.3f));
        if (eff.Cost > 0f)
        {
            float c = eff.Cost * (h != null ? 1f - h.S[Stat.ResourceCost] / 100f : 1f);
            parts.Add($"{(eff.Kind == SkillKind.Channel ? "每秒消耗" : "消耗")} [color=#{rc}]{Num(c)} {res}[/color]");
        }
        else if (eff.Cost < 0f)
        {
            float g = -eff.Cost * (h != null ? 1f + h.S[Stat.ResourceGen] / 100f : 1f);
            parts.Add($"產生 [color=#{rc}]{Num(g)} {res}[/color]");
        }
        if (PipName(eff.Class).Length > 0)
        {
            if (eff.PipReq > 0) parts.Add($"需要 {PipCount(eff.Class, eff.PipReq)}");
            if (eff.PipSpend) parts.Add($"消耗所有{PipName(eff.Class)}");
            if (eff.PipGain > 0) parts.Add($"獲得 {PipCount(eff.Class, eff.PipGain)}");
        }
        if (eff.Cooldown > 0f) parts.Add($"冷卻 {Num(eff.Cooldown * (h?.CdrMul ?? 1f))} 秒");
        if (eff.Charges > 1) parts.Add($"充能 {eff.Charges} 次");
        return string.Join("　", parts);
    }

    /// <summary>
    /// Full skill tooltip. <paramref name="eff"/> is the effective definition (<c>Hero.Eff</c>); with
    /// <paramref name="showNext"/> the description also previews the next rank's damage.
    /// </summary>
    public static string Tooltip(SkillDef eff, Hero h, bool showNext = false)
    {
        if (eff == null) return "";
        var sb = new StringBuilder(640);
        string dim = Hex(Palette.TextDim), gold = Hex(Palette.Gold), good = Hex(Palette.Good), bad = Hex(Palette.Bad);
        int baseRank = h?.BaseRank(eff.Id) ?? 0;
        int bonus = h != null ? Math.Max(0, (int)h.S[Stat.SkillRanks]) : 0;
        bool known = baseRank > 0;
        int rank = known ? baseRank + bonus : 1 + bonus;        // unlearned skills preview what the first point would give

        sb.Append(Head(eff.Name, NameSize)).Append('\n');
        sb.Append($"[color=#{Hex(CatColor(eff.Cat))}]{Names.SkillCat(eff.Cat)}技能[/color]　[color=#{Hex(Palette.Element(eff.Element))}]{Names.Element(eff.Element)}[/color]");
        if (eff.Form != 0) sb.Append($"　[color=#{dim}]{FormName(eff.Form)}[/color]");
        sb.Append('\n');

        if (known)
        {
            sb.Append($"等級 {baseRank}/{SkillDef.MaxRank}");
            if (bonus > 0) sb.Append($" [color=#{good}]+{bonus}（裝備）[/color]");
        }
        else sb.Append($"[color=#{dim}]尚未習得[/color]");
        if (h != null && h.D.Level < eff.UnlockLevel) sb.Append($"　[color=#{bad}]需要等級 {eff.UnlockLevel}[/color]");
        sb.Append('\n');

        string cost = CostLine(eff, h);
        if (cost.Length > 0) sb.Append(cost).Append('\n');

        bool canRankUp = known && baseRank < SkillDef.MaxRank;
        sb.Append('\n').Append(Describe(eff, rank, showNext && canRankUp ? rank + 1 : 0));
        if (showNext && canRankUp && (eff.Desc == null || !eff.Desc.Contains("{0}")))
            sb.Append($"\n[color=#{dim}]此技能的效果不隨等級變化。[/color]");

        if (eff.Coef > 0f && eff.Lucky > 0f)
        {
            float coef = eff.Lucky * 100f;
            float real = MathF.Min(100f, coef * (1f + (h != null ? h.S[Stat.LuckyHit] : 0f) / 100f));
            sb.Append($"\n\n[color=#{dim}]幸運一擊係數 {Num(coef)}%");
            if (real > coef + 0.05f) sb.Append($"（目前機率 {Num(real)}%）");
            sb.Append("[/color]");
        }

        if (h != null)
        {
            bool first = true;
            foreach (var u in eff.Ups)
            {
                if (u == null || !h.HasUp(u.Id)) continue;
                sb.Append(first ? "\n\n" : "\n");
                first = false;
                sb.Append($"[color=#{gold}]◆ {u.Name}[/color]　{Highlight(u.Desc)}");
            }
        }
        return sb.ToString();
    }

    /// <summary>Tooltip of one upgrade node (the enhancement or one of the two exclusive branches).</summary>
    public static string UpgradeTooltip(SkillDef s, SkillUp u, Hero h)
    {
        if (s == null || u == null) return "";
        var sb = new StringBuilder(320);
        string dim = Hex(Palette.TextDim), good = Hex(Palette.Good), bad = Hex(Palette.Bad);
        var other = u == s.A ? s.B : u == s.B ? s.A : null;
        sb.Append(Head(u.Name)).Append('\n');
        sb.Append($"[color=#{Hex(CatColor(s.Cat))}]{s.Name}[/color]　[color=#{dim}]{(u == s.Enh ? "強化" : other != null ? $"分支強化，與「{other.Name}」擇一" : "分支強化")}[/color]\n\n");
        sb.Append(Highlight(u.Desc));
        if (h == null) return sb.ToString();
        sb.Append("\n\n");
        bool enh = s.Enh != null && h.HasUp(s.Enh.Id);
        if (h.HasUp(u.Id)) sb.Append($"[color=#{good}]已取得[/color]");
        else if (h.BaseRank(s.Id) < 1) sb.Append($"[color=#{bad}]需先習得「{s.Name}」[/color]");
        else if (u != s.Enh && !enh) sb.Append($"[color=#{bad}]需先取得強化「{s.Enh?.Name}」[/color]");
        else if (other != null && h.HasUp(other.Id)) sb.Append($"[color=#{dim}]目前選擇「{other.Name}」，可隨時切換，不需點數[/color]");
        else if (h.SkillPoints <= 0) sb.Append($"[color=#{bad}]技能點數不足[/color]");
        else sb.Append($"[color=#{dim}]需要 1 點技能點數[/color]");
        return sb.ToString();
    }

    /// <summary>Tooltip of a passive: per-rank stats with the current and next totals, or the rule of a key passive.</summary>
    public static string PassiveTooltip(PassiveDef p, Hero h)
    {
        if (p == null) return "";
        var sb = new StringBuilder(360);
        string dim = Hex(Palette.TextDim), gold = Hex(Palette.Gold), good = Hex(Palette.Good), bad = Hex(Palette.Bad);
        int rank = h?.BaseRank(p.Id) ?? 0;
        bool locked = h != null && h.D.Level < p.UnlockLevel;
        sb.Append(Head(p.Name)).Append('\n');
        if (p.Key)
        {
            sb.Append($"[color=#{gold}]關鍵被動[/color]　[color=#{dim}]二者擇一[/color]\n\n");
            sb.Append(Highlight(p.Desc)).Append("\n\n");
            PassiveDef other = null;
            if (h != null)
                foreach (var o in SkillDb.PassivesOf(p.Class))
                    if (o.Key && o.Id != p.Id && h.BaseRank(o.Id) > 0) { other = o; break; }
            if (rank > 0) sb.Append($"[color=#{good}]已啟用[/color]");
            else if (locked) sb.Append($"[color=#{bad}]需要等級 {p.UnlockLevel}[/color]");
            else if (other != null) sb.Append($"[color=#{dim}]選擇後將取代「{other.Name}」，不需點數[/color]");
            else sb.Append($"[color=#{dim}]尚未啟用[/color]");
            return sb.ToString();
        }

        sb.Append($"[color=#{dim}]被動[/color]　等級 {rank}/{p.MaxRank}\n\n");
        sb.Append($"[color=#{dim}]每級提供[/color]");
        foreach (var (s, v) in p.PerRank) sb.Append("\n◆ ").Append(Highlight(StatInfo.Format(s, v)));
        if (rank > 0)
        {
            sb.Append($"\n\n[color=#{dim}]目前合計[/color]");
            foreach (var (s, v) in p.PerRank) sb.Append($"\n[color=#{good}]{StatInfo.Format(s, v * rank)}[/color]");
        }
        if (rank < p.MaxRank)
        {
            sb.Append($"\n\n[color=#{dim}]下一級合計[/color]");
            foreach (var (s, v) in p.PerRank) sb.Append('\n').Append(StatInfo.Format(s, v * (rank + 1)));
        }
        if (locked) sb.Append($"\n\n[color=#{bad}]需要等級 {p.UnlockLevel}[/color]");
        return sb.ToString();
    }

    /// <summary>Tooltip of a class specialisation.</summary>
    public static string SpecTooltip(ClassSpec sp, Hero h)
    {
        if (sp == null) return "";
        var sb = new StringBuilder(280);
        string dim = Hex(Palette.TextDim), good = Hex(Palette.Good), bad = Hex(Palette.Bad);
        sb.Append(Head(sp.Name)).Append('\n');
        sb.Append($"[color=#{Hex(Names.ClassColor(sp.Class))}]{Names.Class(sp.Class)}[/color]　[color=#{dim}]職業專精[/color]\n\n");
        sb.Append(Highlight(sp.Desc));
        if (h == null) return sb.ToString();
        sb.Append("\n\n");
        if (h.D.Spec == sp.Id) sb.Append($"[color=#{good}]已選擇[/color]");
        else if (h.D.Level < SkillDb.SpecLevel) sb.Append($"[color=#{bad}]需要等級 {SkillDb.SpecLevel}[/color]");
        else sb.Append($"[color=#{dim}]專精可隨時更換，不需任何代價[/color]");
        return sb.ToString();
    }
}

/// <summary>
/// Panel "skills": the skill tree. A vertical trunk of seven clusters (by unlock level) ending in the key-passive
/// capstone; every active skill is an octagonal node with rank pips and three satellites (enhancement, then the
/// exclusive A / B branch), minor passives hang on the other side of the trunk. A side column offers the class
/// specialisations, a bottom strip assigns skills to the six action slots.
/// Everything is drawn by hand on four stacked layers (backdrop, glow, nodes, overlay) so that only the cheap
/// glow layer repaints every frame; the node layer repaints when the hero changes.
/// The layout is computed from text metrics (string widths, line heights at the current UiTheme.TextScale): the tier
/// title column, the trunk position, row heights, cards, the bottom strip. <see cref="OnTextScale"/> redoes it.
/// The chrome comes from the raster interface art (UiArt): the three areas are forged sub-panels on slate, tiers
/// are inset wells, specialisations are cards, nodes wear forged frames by state (locked / available / owned), keys
/// sit on key caps and the action slots are the HUD's skill cells. Connections, halos and the click feedback stay
/// drawn; without the art (or a piece of it) the flat boxes and vector rings of old are drawn instead.
/// </summary>
public partial class SkillTreeUi : UiPanel
{
    public override string Id => "skills";
    public override bool Fullscreen => true;

    // ───────────────────────── model ─────────────────────────

    private enum NK { Hub, Skill, Enh, UpA, UpB, Passive, Key, Spec, Points, Slot, Pick }

    private sealed class Tier
    {
        public int Index, Level, Rows = 1;
        public float Weight = 1f;
        public float Need, Height;              // the height its text and nodes ask for, and the one it gets
        public bool Capstone, Open;
        public string Title, LevelText, LockText;
        public Rect2 Band;
        public TNode Hub;
        public readonly List<TNode> Skills = new(), Passives = new();
    }

    private sealed class TNode
    {
        public NK Kind;
        public Vector2 Pos;
        public float R;
        public bool Box;                        // rectangular hot zone (cards, slots) instead of a round node
        public Rect2 Rect;
        public SkillDef Skill;
        public SkillUp Up;
        public PassiveDef Passive;
        public ClassSpec Spec;
        public Tier Tier;
        public int Index, Side = 1, Max;
        public int Lines;                       // wrapped description lines the node's box was sized for
        public Vector2[] Fill, Line, Link;      // cached polygon, its closed outline and the link to the parent
        public Texture2D Icon;
        public string Label, Sub, SubB, Lock, Glyph, RankText;
        // measured at layout time with the fonts and sizes they are drawn in (see Sz): dropped and redone on a text-scale change
        public float LabelX, TextW, NameW, SubW, SubBW, LockW;
        // state, refreshed from the hero
        public bool Owned, Can, Locked, Swap;
        public int Rank, Bonus;

        public bool Hit(Vector2 p, float tol)
        {
            if (Box) return Rect.HasPoint(p);
            float r = R + tol;
            return p.DistanceSquaredTo(Pos) <= r * r;
        }

        public Vector2 Center => Box ? Rect.Position + Rect.Size * 0.5f : Pos;
    }

    private sealed class Pop
    {
        public Vector2 Pos;
        public float T, Dur, R;
        public Color C;
        public string Text;
    }

    // ───────────────────────── look ─────────────────────────

    private static readonly Color CNodeFill = new(0.075f, 0.068f, 0.064f);
    private static readonly Color CHubFill = new(0.22f, 0.13f, 0.05f);
    private static readonly Color CUpOwned = new(0.34f, 0.23f, 0.07f);
    private static readonly Color CRim = new(0.46f, 0.42f, 0.36f);
    private static readonly Color CRimLocked = new(0.25f, 0.23f, 0.21f);
    private static readonly Color CLinkDim = new(0.2f, 0.185f, 0.17f);
    private static readonly Color CLinkOpen = new(0.42f, 0.36f, 0.28f);
    private static readonly Color CLinkLit = new(0.98f, 0.7f, 0.26f);
    private static readonly Color CAssign = new(0.45f, 0.9f, 0.85f);
    private static readonly Color CLockText = new(0.8f, 0.38f, 0.32f);
    private static readonly Color CIconIdle = new(0.74f, 0.7f, 0.63f, 0.92f);
    private static readonly Color CIconLocked = new(0.42f, 0.4f, 0.38f, 0.5f);
    private static readonly Color CHover = new(1f, 0.95f, 0.82f, 0.92f);
    private static readonly Color CPipEmpty = new(0.4f, 0.37f, 0.32f);
    private const string Sep = " · ";
    private const string BarTitle = "技能配置";
    private const string BarCaption = "左鍵選取　右鍵清空";
    private const string BarHint1 = "先點選欄位，再點選已習得的技能，即可放入該欄位。";
    private const string BarHint2 = "左鍵欄位預設放置基本技能，但任何技能皆可放入。";

    // Text sizes. Every number below is a DESIGN size (px at 1080p, before the player's text-size setting), and so is
    // every "size" parameter and local in this class. Only Sz() turns one into the size handed to a font, and only
    // the helpers that measure or draw (TextWidth, LineH, Asc, Em, Mid, LinesOf, Txt, TxtR, TxtC, TxtLit, Para) call
    // it — so each size goes through UiTheme.Fs exactly once. Never pass Sz(...) or UiTheme.Fs(...) into those helpers.
    // (_hintSize is a design size as well: ZRow or ZCap, whichever the bottom strip has room for.)
    private const int ZBig = 54;        // the available-points figure
    private const int ZTitle = 38;      // panel title
    private const int ZEmpty = 34;      // the notice shown when there is no hero
    private const int ZHead = 28;       // section headings: 職業專精, 技能配置
    private const int ZName = 24;       // tier titles; skill, key-passive and specialisation names; click feedback; glyphs standing in for a missing icon
    private const int ZRow = 22;        // passive names, descriptions on cards, hints, the class line
    private const int ZCap = 20;        // captions: category · element, level requirements, key caps, counters, glyphs on small nodes, "+N" badges
    private const TextServer.LineBreakFlag Brk = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound | TextServer.LineBreakFlag.Adaptive;

    private static readonly string[] HelpKeys = { "左鍵", "右鍵", "分支", "配置", "K / Esc" };
    private static readonly string[] HelpTexts =
    {
        "習得技能、提升等級、取得強化",
        "返還點數，重置不需任何代價",
        "兩項分支強化擇一，可隨時切換",
        "先點下方欄位，再點已習得的技能",
        "關閉技能頁",
    };

    private static readonly Dictionary<string, string> KeyIcons = new()
    {
        ["kp_eb_edge"] = "winged-sword", ["kp_eb_stand"] = "vibrating-shield",
        ["kp_as_overload"] = "thunder-struck", ["kp_as_mastery"] = "frostfire",
        ["kp_hu_finisher"] = "backstab", ["kp_hu_skirmish"] = "dodge",
        ["kp_sb_chorus"] = "minions", ["kp_sb_alone"] = "spectre",
        ["kp_ws_feral"] = "wolverine-claws", ["kp_ws_elder"] = "oak-leaf",
        ["kp_fb_flow"] = "yin-yang", ["kp_fb_vajra"] = "meditation",
        ["kp_lk_retort"] = "shield-echoes", ["kp_lk_weight"] = "scales",
        ["kp_pb_blood"] = "bleeding-heart", ["kp_pb_full"] = "star-swirl",
    };

    private static readonly Dictionary<string, string> SpecIcons = new()
    {
        ["eb_heavy"] = "two-handed-sword", ["eb_swift"] = "quick-slash", ["eb_guard"] = "checked-shield",
        ["as_fire"] = "flame", ["as_frost"] = "snowflake-1", ["as_storm"] = "lightning-helix",
        ["hu_combo"] = "daggers", ["hu_ambush"] = "hood", ["hu_venom"] = "poison-bottle",
        ["sb_legion"] = "skeleton", ["sb_offering"] = "death-skull", ["sb_blood"] = "bleeding-heart",
        ["ws_bear"] = "bear-head", ["ws_wolf"] = "wolf-head", ["ws_raven"] = "raven",
        ["fb_fire"] = "fire-silhouette", ["fb_iron"] = "anvil", ["fb_wind"] = "tornado",
        ["lk_ward"] = "templar-shield", ["lk_judge"] = "gavel", ["lk_guide"] = "lantern-flame",
        ["pb_brute"] = "ogre", ["pb_imps"] = "imp", ["pb_wraith"] = "ghost",
    };

    private static string StatIcon(Stat s) => s switch
    {
        Stat.DmgCore => "sword-brandish", Stat.DmgBasic => "punch", Stat.DmgPhysical => "crossed-swords",
        Stat.DmgFire => "flame", Stat.DmgLightning => "lightning-helix", Stat.DmgPoison => "poison-bottle",
        Stat.DmgShadow => "evil-moon", Stat.DmgDot => "burning-embers", Stat.DmgVuln => "broken-shield",
        Stat.DmgOverpower => "mighty-force", Stat.MinionDmg => "minions",
        Stat.Armor => "armor-vest", Stat.LifePct => "heart-plus", Stat.MaxLife => "heart-plus",
        Stat.LifeOnKill => "heart-drop", Stat.Healing => "healing", Stat.DmgReduction => "shield",
        Stat.ResAll => "magic-shield", Stat.ResFire => "fire-shield", Stat.ResShadow => "evil-moon",
        Stat.BarrierPct => "rosa-shield", Stat.Thorns => "spiked-shield", Stat.Dodge => "dodging",
        Stat.CritChance => "on-target", Stat.CritDmg => "bullseye", Stat.AttackSpeed => "quick-slash",
        Stat.ResourceGen => "energy-breath", Stat.MaxResource => "round-bottom-flask", Stat.Cdr => "hourglass",
        Stat.MoveSpeed => "wingfoot",
        _ => "star-formation"
    };

    // ───────────────────────── state ─────────────────────────

    private Hero _hero;
    private bool _built, _bound, _dirty = true, _tipShown, _anyGlow, _glowWas;
    private ClassId _cls;
    private Vector2 _laidSize = new(-1f, -1f);
    private float _k = 1f, _time, _trunkX, _sepW, _specHeadY, _helpY, _pickX;
    // text-driven layout, all recomputed by Relayout (baselines, column widths, offsets of a label's lines from its node's centre)
    private float _tight, _titleBase, _ruleY, _numW, _specHintY, _specHead, _helpKeyW, _tierTextW, _barHeadY, _barCapY;
    private float _dyTier, _dyTierSub, _dySkillName, _dySkillSub, _dyPassName, _dyPassSub;
    private readonly float[] _helpH = new float[HelpKeys.Length], _helpTw = new float[HelpKeys.Length];
    private int _helpRows, _hintSize = ZCap;
    private int _assign = -1, _points, _errors;
    private Rect2 _leftR, _treeR, _barR;
    private readonly List<Tier> _tiers = new();
    private readonly List<TNode> _nodes = new(), _picks = new();
    private readonly Dictionary<string, TNode> _skillNodes = new();
    private readonly TNode[] _slots = new TNode[6];
    private readonly List<Pop> _pops = new();
    private TNode _pointsNode, _hover;
    private Control _back, _glow, _fg, _over;
    private Button _resetBtn, _closeBtn;
    private Font _fTitle, _fBody, _fUi;
    private StyleBox _sbPanel, _sbBand, _sbBandLocked, _sbCard, _sbCardSel, _sbCardLocked, _sbSlot, _sbBadge;
    private string _ptsText = "0", _spentText = "", _classText = "", _specHint = "", _assignText = "";
    private Color _specHintCol;
    private float _traitY = -1f;                                                  // [fixes] 職業特性 block (LayoutLeft)
    private static string TraitLine(string text, bool on) => (on ? "◆ " : "◇ ") + text + (on ? "" : "（未生效）");
    /// <summary>「拳刃精通　雙持拳刃：武器傷害 +8%」→「拳刃精通 +8%」: the name and the last figure only.</summary>
    private static string Compact(string text)
    {
        int gap = text.IndexOf('　');
        string name = gap > 0 ? text[..gap] : text;
        var m = System.Text.RegularExpressions.Regex.Match(text, @"[+＋][0-9.]+%");
        return m.Success ? $"{name} {m.Value}" : name;
    }
    private readonly List<(string text, bool on)> _traits = new();
    private static Texture2D _glowTex;

    // ── raster chrome (res://assets/ui, see UiArt): looked up once. A piece is null when the art is not installed or
    //    does not carry it; every draw below then falls back to the flat box or vector ring it replaced. ──
    private static bool _artLooked;
    private static UiArt.Piece _aPanel, _aWell, _aCard, _aCardOn, _aKeycap, _aCell, _aCellHover, _aMark, _aMarkHi, _aLine, _aRule, _aPip, _aPipOff;
    private static UiArt.Piece _fArea, _fCard, _fWell, _aHubOff, _aHubOn;
    /// <summary>Node frames by state: 0 locked (also the idle look, at full brightness), 1 available, 2 owned.</summary>
    private static readonly UiArt.Piece[] _aOct = new UiArt.Piece[3], _aRound = new UiArt.Piece[3], _aKey = new UiArt.Piece[3];
    private static readonly Color ArtIdle = new(1f, 1f, 1f), ArtLocked = new(0.6f, 0.58f, 0.58f), ArtBandLocked = new(0.62f, 0.6f, 0.62f);
    private static readonly Color LinkShade = new(0f, 0f, 0f, 0.5f), CardVeil = new(0.93f, 0.66f, 0.3f, 0.055f);
    private static readonly bool[] HelpIsKey = { true, true, false, false, true };     // the others are topics, not keys
    private const float UpGrow = 1.1f;      // the forged ring of an upgrade node against the circle the layout sized
    private Action _onArtSet;
    // layout that makes room for the mouldings (Relayout, LayoutLeft, LayoutBar)
    private float _slotTop, _leftPad, _cardPadX, _cardPadY, _helpGap, _helpPad, _keyPad;

    private static void LookArt()
    {
        if (_artLooked) return;
        _artLooked = true;
        if (!UiTheme.Art) return;
        _aPanel = UiArt.P("panel"); _aWell = UiArt.P("well"); _aCard = UiArt.P("card"); _aCardOn = UiArt.P("card_on");
        _aKeycap = UiArt.P("keycap"); _aCell = UiArt.P("skill_slot"); _aCellHover = UiArt.P("slot_hover");
        _aMark = UiArt.P("slot_rarity"); _aMarkHi = UiArt.P("slot_rarity_hi"); _aLine = UiArt.P("boxline"); _aRule = UiArt.P("rule");
        _aPip = UiArt.P("bullet_gold"); _aPipOff = UiArt.P("bullet");
        _fArea = UiArt.Field("slate"); _fCard = UiArt.Field("soot"); _fWell = UiArt.Field("well");
        _aHubOff = UiArt.P("node_hub_off"); _aHubOn = UiArt.P("node_hub_on");
        string[] state = { "locked", "avail", "owned" };
        for (int i = 0; i < 3; i++)
        {
            _aOct[i] = UiArt.P("node_oct_" + state[i]);
            _aRound[i] = UiArt.P("node_round_" + state[i]);
            _aKey[i] = UiArt.P("node_key_" + state[i]);
        }
    }

    /// <summary>The three areas wear the forged moulding (and the layout leaves it room): the art is installed.</summary>
    private static bool ArtOn => _aPanel != null;

    /// <summary>The frame of a node in its state, or null (flat fallback) when that piece is missing.</summary>
    private static UiArt.Piece NodeArt(UiArt.Piece[] set, TNode n) => set[n.Owned ? 2 : n.Can ? 1 : 0];

    private static Rect2 Square(Vector2 centre, float half) => new(centre.X - half, centre.Y - half, half * 2f, half * 2f);

    // ───────────────────────── life cycle ─────────────────────────

    public SkillTreeUi()
    {
        Name = "SkillTreeUi";
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Ready()
    {
        base._Ready();
        Log.Guard("ui", EnsureBuilt);
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        // the texture set follows the window size (x1 / x2): the cached layers are painted again from the new one
        _onArtSet ??= () => _dirty = true;
        UiArt.SetChanged -= _onArtSet;
        UiArt.SetChanged += _onArtSet;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_onArtSet != null) UiArt.SetChanged -= _onArtSet;
        Unbind();
    }

    public override void OnOpen(object arg)
    {
        base.OnOpen(arg);
        Log.Guard("ui", () =>
        {
            EnsureBuilt();
            Bind();
            _assign = -1;
            _pops.Clear();
            _hover = null;
            ShowBook(arg as string == "book");            // [golem] --ui=skills:book opens the 亡者名冊 tab
            _tipShown = false;          // the manager hides any tooltip when a panel opens
            if (NormalizeBar()) _hero?.Notify();
            _dirty = true;
            if (_hero != null)
                Log.Info("ui", $"skill tree opened: {Names.Class(_hero.Class)} lv{_hero.D.Level} points={_hero.SkillPoints} spec={_hero.D.Spec ?? "-"}");
        });
    }

    public override void OnClose()
    {
        base.OnClose();
        Log.Guard("ui", () =>
        {
            ClearHover();
            _assign = -1;
            _pops.Clear();
            Unbind();
        });
    }

    public override void Refresh()
    {
        base.Refresh();
        _dirty = true;
    }

    /// <summary>
    /// The text-size setting changed. Nothing of the old layout survives: the fonts' metrics, every measured string
    /// width, the tier heights, the trunk position, the cards and the bottom strip were all computed for the old
    /// sizes. They are thrown away and the layout is run again at once when the panel is on screen (so no frame is
    /// drawn with new sizes in old boxes); a closed panel lays itself out when it is next shown. The buttons' font
    /// sizes were already rescaled by the manager; their boxes follow in LayoutLeft. Tooltip text is built on demand
    /// (nothing cached here), and the manager has already hidden the card.
    /// </summary>
    public override void OnTextScale()
    {
        base.OnTextScale();
        Log.Guard("ui", () =>
        {
            _laidSize = new Vector2(-1f, -1f);          // whatever happens below, the next frame on screen lays out again
            _pops.Clear();
            ClearHover();
            _dirty = true;
            if (!_built || _hero == null || !IsInsideTree() || !IsVisibleInTree()) return;
            if (Size.X < 320f || Size.Y < 240f) return;
            Relayout();
            UpdateState();
            RedrawAll();
            Log.Info("ui", $"skill tree laid out again for text scale ×{UiTheme.TextScale:0.00}");
        });
    }

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        try { LookArt(); }
        catch (Exception e) { Fail("art lookup", e); }
        _back = Layer("Backdrop", DrawBack);
        _glow = Layer("Glow", DrawGlow);
        _fg = Layer("Nodes", DrawNodes);
        _over = Layer("Overlay", DrawOver);
        // the node frames and pips are drawn far below their authored size: they carry mipmaps
        _fg.TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        Action reset = () => Log.Guard("ui", OnResetPressed), close = () => Log.Guard("ui", OnClosePressed);
        try
        {
            // design sizes: UiTheme.Button sends them through UiTheme.Fs itself
            _resetBtn = UiTheme.Button("重置全部", reset, UiTheme.FsRow);
            _closeBtn = UiTheme.Button("關閉", close, UiTheme.FsRow);
        }
        catch (Exception e) { Fail("theme buttons", e); }
        _resetBtn ??= PlainButton("重置全部", reset);
        _closeBtn ??= PlainButton("關閉", close);
        _resetBtn.FocusMode = FocusModeEnum.None;
        _closeBtn.FocusMode = FocusModeEnum.None;
        AddChild(_resetBtn);
        AddChild(_closeBtn);
        Log.Guard("ui", BuildBookTab);                     // [golem] 亡者名冊 tab (SkillTreeUi.Book.cs)

        try
        {
            Color panel = UiTheme.Panel, border = UiTheme.Border, gold = UiTheme.Gold;
            _sbPanel = UiTheme.Box(panel, border, 2, 6);
            _sbBand = UiTheme.Box(Fade(panel.Lightened(0.05f), 0.86f), Fade(border, 0.55f), 1, 6);
            _sbBandLocked = UiTheme.Box(Fade(panel.Darkened(0.4f), 0.9f), Fade(border, 0.25f), 1, 6);
            _sbCard = UiTheme.Box(panel.Lightened(0.07f), Fade(border, 0.8f), 1, 5);
            _sbCardSel = UiTheme.Box(panel.Lightened(0.07f).Lerp(gold, 0.1f), gold, 2, 5);
            _sbCardLocked = UiTheme.Box(panel.Darkened(0.3f), Fade(border, 0.4f), 1, 5);
            _sbSlot = UiTheme.Box(new Color(0.05f, 0.045f, 0.042f), border, 2, 4);
            _sbBadge = UiTheme.Box(new Color(0.04f, 0.038f, 0.036f, 0.95f), Fade(Palette.Good, 0.75f), 1, 4);
        }
        catch (Exception e) { Fail("theme boxes", e); }     // Sb() falls back to plain rectangles

        Resized += OnResized;
        MouseExited += ClearHover;
    }

    private static Button PlainButton(string text, Action onPress)
    {
        var b = new Button { Text = text };
        b.Pressed += onPress;
        return b;
    }

    private Control Layer(string name, Action<Control> draw)
    {
        var c = new Control { Name = name, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(c);
        c.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        c.Draw += () => SafeDraw(draw, c);
        return c;
    }

    private void SafeDraw(Action<Control> draw, Control c)
    {
        try { draw(c); }
        catch (Exception e) { Fail("draw", e); }
    }

    private void Fail(string where, Exception e)
    {
        if (_errors++ < 5) Log.Error("ui", $"skill tree {where} failed", e);
    }

    private void OnResized()
    {
        _laidSize = new Vector2(-1f, -1f);
        _dirty = true;
    }

    private void Bind()
    {
        var h = Game.I?.Hero;
        if (_bound && ReferenceEquals(h, _hero)) return;
        Unbind();
        if (!ReferenceEquals(h, _hero))
        {
            _hero = h;
            _tiers.Clear(); _nodes.Clear(); _picks.Clear(); _skillNodes.Clear();
            _hover = null; _pointsNode = null;
            Array.Clear(_slots);
            _laidSize = new Vector2(-1f, -1f);
            _assign = -1;
        }
        if (h != null) { h.Changed += OnHeroChanged; _bound = true; }
        _dirty = true;
    }

    private void Unbind()
    {
        if (_bound && _hero != null) _hero.Changed -= OnHeroChanged;
        _bound = false;
    }

    private void OnHeroChanged() => _dirty = true;

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!_built) return;
        if (!IsVisibleInTree())
        {
            if (_bound) { Unbind(); ClearHover(); _assign = -1; _pops.Clear(); }
            return;
        }
        try
        {
            Bind();
            float dt = (float)delta;
            _time += dt;
            if (_resetBtn != null) _resetBtn.Disabled = _hero == null;
            if (_hero == null)
            {
                if (_dirty) { _dirty = false; RedrawAll(); }
                return;
            }
            var size = Size;
            if (size.X < 320f || size.Y < 240f) return;
            if (size != _laidSize || _hero.Class != _cls || _tiers.Count == 0) Relayout();
            if (_dirty)
            {
                _dirty = false;
                UpdateState();
                RedrawAll();
                RefreshTooltip();
            }
            if (_hover != null)
            {
                var over = GetViewport()?.GuiGetHoveredControl();
                if (over != this || !_hover.Hit(Ui.PointerIn(this), 5f * _k)) ClearHover();
            }
            if (_anyGlow || _glowWas) { _glowWas = _anyGlow; _glow.QueueRedraw(); }
            if (_pops.Count > 0)
            {
                for (int i = _pops.Count - 1; i >= 0; i--)
                {
                    var p = _pops[i];
                    p.T += dt;
                    if (p.T >= p.Dur) _pops.RemoveAt(i);
                }
                _over.QueueRedraw();
            }
        }
        catch (Exception e) { Fail("process", e); }
    }

    private void RedrawAll()
    {
        _back.QueueRedraw(); _glow.QueueRedraw(); _fg.QueueRedraw(); _over.QueueRedraw();
    }

    // ───────────────────────── layout ─────────────────────────

    /// <summary>
    /// A DESIGN size → the size handed to the font: fitted to the panel (<c>_k</c> is 1 on a 1920×1080 canvas) and then
    /// through <see cref="UiTheme.Fs"/> — the player's text scale and the 20 px floor. The one place where this
    /// panel's sizes are converted; everything that measures or draws text calls it exactly once.
    /// </summary>
    private int Sz(int size) => UiTheme.Fs(Mathf.RoundToInt(size * _k));

    // measuring: all of these take DESIGN sizes
    private float LineH(Font f, int size) => f.GetHeight(Sz(size));
    private float Asc(Font f, int size) => f.GetAscent(Sz(size));
    /// <summary>The em square of a size in pixels (a full-width glyph is this wide and about this tall): for boxes that hold a glyph.</summary>
    private float Em(int size) => Sz(size);
    /// <summary>The baseline that centres a line of text on <paramref name="yc"/>.</summary>
    private float Mid(Font f, int size, float yc)
    {
        int px = Sz(size);
        return yc + (f.GetAscent(px) - f.GetDescent(px)) * 0.5f;
    }
    private float TextWidth(Font f, string s, int size) => string.IsNullOrEmpty(s) ? 0f : f.GetStringSize(s, HorizontalAlignment.Left, -1f, Sz(size)).X;
    /// <summary>How many lines <paramref name="s"/> takes when it is wrapped at <paramref name="width"/> (0 for no text).</summary>
    private int LinesOf(Font f, string s, float width, int size)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int px = Sz(size);
        float h = f.GetMultilineStringSize(s, HorizontalAlignment.Left, Mathf.Max(1f, width), px, -1, Brk).Y;
        return Math.Max(1, Mathf.RoundToInt(h / Mathf.Max(1f, f.GetHeight(px))));
    }

    private static Color Fade(Color c, float a) => new(c.R, c.G, c.B, c.A * a);

    private static Vector2[] Ngon(Vector2 c, float r, int sides, float start, bool closed)
    {
        var pts = new Vector2[closed ? sides + 1 : sides];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = start + Mathf.Tau * (i % sides) / sides;
            pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return pts;
    }

    private Texture2D IconOf(SkillDef s)
    {
        try { return Icons.ForSkill(s); }
        catch (Exception e) { Fail("icon", e); return null; }
    }

    private Texture2D IconNamed(string name)
    {
        try { return Icons.Get(name); }
        catch (Exception e) { Fail("icon", e); return null; }
    }

    private static Texture2D GlowTex()
    {
        if (_glowTex != null) return _glowTex;
        var g = new Gradient
        {
            Offsets = new[] { 0f, 0.45f, 1f },
            Colors = new[] { new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0.28f), new Color(1f, 1f, 1f, 0f) },
        };
        _glowTex = new GradientTexture2D
        {
            Gradient = g, Width = 128, Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
        };
        return _glowTex;
    }

    private void Relayout()
    {
        var h = _hero;
        _cls = h.Class;
        _laidSize = Size;
        float w = Size.X, hgt = Size.Y;
        _k = Mathf.Clamp(Mathf.Min(w / 1920f, hgt / 1080f), 0.5f, 3f);
        float k = _k;
        _fTitle = UiTheme.Title ?? ThemeDB.FallbackFont;
        _fBody = UiTheme.Body ?? ThemeDB.FallbackFont;
        _fUi = UiTheme.Ui ?? ThemeDB.FallbackFont;
        _tight = -0.1f * Em(ZCap);          // two stacked lines of a label sit a little closer than two lines of running text

        // the strip at the bottom is as tall as a slot with its key cap underneath; with the forged frames the areas
        // stand a little closer to the screen edge and to each other, and the strip keeps clear of its own moulding
        bool art = ArtOn;
        // (the tree keeps the width it had: at the largest text its labels need every pixel of it)
        float m = (art ? 10f : 16f) * k, leftW = (art ? 424f : 420f) * k, gap = (art ? 8f : 12f) * k;
        _leftPad = (art ? 18f : 20f) * k;
        _slotTop = (art ? 13f : 10f) * k;
        float barH = Mathf.Max(104f * k, _slotTop + 60f * k + LineH(_fUi, ZCap) + (art ? 14f : 6f) * k);
        _leftR = new Rect2(m, m, leftW, hgt - 2f * m);
        float tx = m + leftW + gap;
        _treeR = new Rect2(tx, m, w - tx - m, hgt - 2f * m - barH - gap);
        _barR = new Rect2(tx, hgt - m - barH, w - tx - m, barH);

        if (_hover != null) ClearHover();
        _tiers.Clear(); _nodes.Clear(); _picks.Clear(); _skillNodes.Clear();
        Array.Clear(_slots);
        BuildTiers(h.Class);
        LayoutTree();
        LayoutLeft(h.Class);
        LayoutBar();
        _dirty = true;
        Log.Debug("ui", $"skill tree laid out {w:0}x{hgt:0} k={k:0.00} text×{UiTheme.TextScale:0.00} class={Names.ClassKey(h.Class)} tiers={_tiers.Count} nodes={_nodes.Count} bar={barH:0} trunk={_trunkX:0}");
    }

    private void BuildTiers(ClassId cls)
    {
        var skills = SkillDb.Of(cls).OrderBy(s => s.UnlockLevel).ToList();
        var passives = SkillDb.PassivesOf(cls).OrderBy(p => p.UnlockLevel).ToList();
        int[] lv = SkillDb.ClusterLevels;
        for (int i = 0; i < lv.Length; i++)
        {
            int lo = i == 0 ? int.MinValue : lv[i], hi = i + 1 < lv.Length ? lv[i + 1] : int.MaxValue;
            var t = new Tier { Index = i, Level = lv[i] };
            foreach (var s in skills)
                if (s.UnlockLevel >= lo && s.UnlockLevel < hi)
                    t.Skills.Add(new TNode { Kind = NK.Skill, Skill = s, Tier = t, Label = s.Name, Icon = IconOf(s) });
            foreach (var p in passives)
                if (!p.Key && p.UnlockLevel >= lo && p.UnlockLevel < hi)
                    t.Passives.Add(new TNode
                    {
                        Kind = NK.Passive, Passive = p, Tier = t, Label = p.Name, Sub = p.Desc, Max = p.MaxRank,
                        Lock = $"等級 {p.UnlockLevel} 解鎖",
                        Icon = IconNamed(p.PerRank.Length > 0 ? StatIcon(p.PerRank[0].s) : "star-formation"),
                    });
            t.Rows = Math.Max(1, Math.Max(t.Skills.Count, t.Passives.Count));
            t.Weight = t.Rows;
            var cats = new List<string>(2);
            foreach (var n in t.Skills)
            {
                string c = Names.SkillCat(n.Skill.Cat);
                if (!cats.Contains(c)) cats.Add(c);
            }
            t.Title = cats.Count == 0 ? "被動" : string.Join("・", cats);
            t.LevelText = $"等級 {t.Level}";
            t.LockText = $"等級 {t.Level} 解鎖";
            _tiers.Add(t);
        }

        var keys = passives.Where(p => p.Key).ToList();
        if (keys.Count == 0) return;
        var cap = new Tier { Index = lv.Length, Level = keys.Min(p => p.UnlockLevel), Capstone = true, Title = "關鍵被動" };
        foreach (var p in keys)
            cap.Passives.Add(new TNode
            {
                Kind = NK.Key, Passive = p, Tier = cap, Label = p.Name, Sub = p.Desc, Max = 1,
                Lock = $"等級 {p.UnlockLevel} 解鎖",
                Icon = IconNamed(KeyIcons.TryGetValue(p.Id, out var ic) ? ic : "crown"),
            });
        cap.Rows = Math.Max(1, (keys.Count + 1) / 2);
        cap.Weight = cap.Rows * 0.94f;
        cap.LevelText = $"等級 {cap.Level}";
        cap.LockText = $"等級 {cap.Level} 解鎖";
        _tiers.Add(cap);
    }

    /// <summary>Skills hang on alternating sides of the trunk, tier by tier; a tier's minor passives take the other side.</summary>
    private static int SkillSide(Tier t) => t.Index % 2 == 0 ? 1 : -1;

    /// <summary>
    /// Lays the tree out from its text: the title column is as wide as its widest line, the trunk stands where the
    /// labels of both sides fit, every tier is at least as tall as its lines and nodes need, and nodes that carry a
    /// glyph are at least as large as the glyph. All measurements use the sizes the text is drawn in (see Sz).
    /// </summary>
    private void LayoutTree()
    {
        float k = _k;
        // inside the forged frame the bands start beyond its moulding and corner caps (9 px, caps 15 px) and stand
        // closer to each other, which gives back the height the frame took
        bool art = ArtOn;
        float pad = (art ? 14f : 6f) * k, gapB = (art ? 4f : 6f) * k, slack = 2f * k;
        float bandL = _treeR.Position.X + (art ? 13f : 8f) * k, bandR = _treeR.End.X - (art ? 13f : 8f) * k;

        // ── line metrics: where the lines of each kind of label sit, relative to the centre of their node ──
        float nameH = LineH(_fTitle, ZName), rowH = LineH(_fTitle, ZRow), capH = LineH(_fUi, ZCap), keyDescH = LineH(_fBody, ZCap);
        float top = -(nameH + _tight + capH) * 0.5f;
        _dySkillName = top + Asc(_fTitle, ZName);
        _dySkillSub = top + nameH + _tight + Asc(_fUi, ZCap);
        _dyTier = _dySkillName;             // a tier's title block is set like a skill's label: a name over a caption
        _dyTierSub = _dySkillSub;
        top = -(rowH + _tight + capH) * 0.5f;
        _dyPassName = top + Asc(_fTitle, ZRow);
        _dyPassSub = top + rowH + _tight + Asc(_fUi, ZCap);

        // ── the tier title column is as wide as its widest line ──
        float tw = 0f, glyphW = 0f;
        foreach (var t in _tiers)
        {
            tw = Mathf.Max(tw, TextWidth(_fTitle, t.Title, ZName));
            tw = Mathf.Max(tw, Mathf.Max(TextWidth(_fUi, t.LevelText, ZCap), TextWidth(_fUi, t.LockText, ZCap)));
            glyphW = Mathf.Max(glyphW, TextWidth(_fUi, t.Level.ToString(CultureInfo.InvariantCulture), ZCap));
        }
        _tierTextW = Mathf.Min(tw + slack, (bandR - bandL) * 0.22f);
        float titleW = Mathf.Max(132f * k, _tierTextW + 28f * k);
        float inner = bandR - bandL - titleW;
        float hx = k * Mathf.Clamp(inner * 0.42f / (549f * k), 0.8f, 1.2f);

        // ── nodes that carry a glyph are at least as large as the glyph needs ──
        float upR = Mathf.Max(17f * k, Em(ZCap) * 0.66f + 2.5f * k);            // a full-width glyph inside the circle
        float abOff = Mathf.Max(21f * k, upR + 2f * k);                         // the two branch nodes: above and below the row's axis
        float hubNeed = Mathf.Max(20.4f * k, (glyphW + Em(ZCap) * 0.74f) * 0.5f + 3f * k);     // the level figure inside the diamond
        // the forged diamond has a broad rim (its opening is 0.69 of the frame): the frame grows so the figure stays inside it
        if (_aHubOff != null && _aHubOn != null) hubNeed *= 1.3f;

        // ── measure the labels: skills on one side of the trunk, minor passives on the other ──
        _sepW = TextWidth(_fUi, Sep, ZCap);
        float lockGap = 12f * k, labelGap = upR + 16f * k, passGap = 14f * k, passR0 = 23f * k;
        float edgeL = 6f * k, edgeR = 14f * k;
        float pTextL = 0f, pTextR = 0f;
        foreach (var t in _tiers)
        {
            if (t.Capstone) continue;
            int skillSide = SkillSide(t);
            foreach (var n in t.Skills)
            {
                var s = n.Skill;
                n.NameW = TextWidth(_fTitle, n.Label, ZName);
                n.Sub = Names.SkillCat(s.Cat);
                n.SubW = TextWidth(_fUi, n.Sub, ZCap);
                n.SubB = Names.Element(s.Element);
                n.SubBW = TextWidth(_fUi, n.SubB, ZCap);
                n.Lock = $"等級 {s.UnlockLevel} 解鎖";
                n.LockW = TextWidth(_fUi, n.Lock, ZCap);
            }
            foreach (var n in t.Passives)
            {
                n.NameW = TextWidth(_fTitle, n.Label, ZRow);
                n.SubW = TextWidth(_fUi, n.Sub, ZCap);
                n.LockW = TextWidth(_fUi, n.Lock, ZCap);
                float text = Mathf.Max(n.NameW, Mathf.Max(n.SubW, n.LockW)) + slack;
                if (skillSide > 0) pTextL = Mathf.Max(pTextL, text); else pTextR = Mathf.Max(pTextR, text);
            }
        }

        // What each side of the trunk needs for a given node spacing h: from the trunk to the end of its widest skill
        // label (sl / sr; the level requirement always has its place, shown or not), and with the passives' lines as
        // well (nl / nr; a passive may stand as close to the trunk as 100·h).
        void Needs(float h, out float sl, out float sr, out float nl, out float nr)
        {
            float skL = 0f, skR = 0f;
            foreach (var t in _tiers)
            {
                if (t.Capstone) continue;
                foreach (var n in t.Skills)
                {
                    float need = (140f + (n.Skill.Enh != null ? 78f : 0f) + 64f) * h + labelGap
                        + Mathf.Max(n.NameW, n.SubW + _sepW + n.SubBW + lockGap + n.LockW) + slack;
                    if (SkillSide(t) > 0) skR = Mathf.Max(skR, need); else skL = Mathf.Max(skL, need);
                }
            }
            sl = skL + edgeL;
            sr = skR + edgeR;
            nl = Mathf.Max(sl, pTextL > 0f ? 100f * h + passR0 + passGap + pTextL + edgeL : 0f);
            nr = Mathf.Max(sr, pTextR > 0f ? 100f * h + passR0 + passGap + pTextR + edgeR : 0f);
        }

        // ── the trunk: left of centre (0.42) when the labels allow it, otherwise where both sides fit. When they do
        //    not fit, the nodes first move closer together (hx, never below the 0.8 it always allowed); a passive may
        //    also move in towards the trunk (140 → 100). A passive's line that still does not fit ends in an ellipsis
        //    (its tooltip has the whole text); skill labels stay whole. ──
        float sL, sR, needL, needR;
        Needs(hx, out sL, out sR, out needL, out needR);
        while (needL + needR > inner && hx > 0.8f * k + 0.001f)
        {
            hx = Mathf.Max(0.8f * k, hx - 0.02f * k);
            Needs(hx, out sL, out sR, out needL, out needR);
        }
        float pMin = 100f * hx, pMax = 140f * hx;
        float halfL;
        if (needL + needR <= inner) halfL = Mathf.Clamp(inner * 0.42f, needL, inner - needR);
        else if (sL + sR <= inner) halfL = Mathf.Clamp(inner * needL / (needL + needR), sL, inner - sR);
        else halfL = inner * sL / Mathf.Max(1f, sL + sR);
        float halfR = inner - halfL;
        _trunkX = bandL + titleW + halfL;
        float minX = bandL + titleW, maxX = bandR;

        // ── capstone: a key passive's description wraps inside what its side of the trunk offers ──
        float keyGap = 18f * k;
        int keyLines = 0;
        foreach (var t in _tiers)
        {
            if (!t.Capstone) continue;
            for (int j = 0; j < t.Passives.Count; j++)
            {
                var n = t.Passives[j];
                float room = (j % 2 == 0 ? halfL - edgeL : halfR - edgeR) - 150f * hx - 27f * k - keyGap;
                n.TextW = Mathf.Clamp(room, 60f * k, UiTheme.Px(440f) * k);
                n.NameW = TextWidth(_fTitle, n.Label, ZName);
                n.LockW = TextWidth(_fUi, n.Lock, ZCap);
                n.Lines = Math.Min(3, LinesOf(_fBody, n.Sub, n.TextW, ZCap));
                keyLines = Math.Max(keyLines, n.Lines);
            }
        }

        // ── tier heights: each tier gets at least what its lines and nodes need; the rest is shared by weight ──
        float rowNeed = Mathf.Max(Mathf.Max(nameH, rowH) + _tight + capH, 2f * (abOff + upR)) + 4f * k;
        float capNeed = Mathf.Max(nameH + keyLines * keyDescH, 2f * (hubNeed + 2f * k + capH)) + 10f * k;
        float avail = _treeR.Size.Y - 2f * pad - gapB * (_tiers.Count - 1);
        float total = 0f;
        foreach (var t in _tiers)
        {
            t.Need = (t.Capstone ? capNeed : rowNeed) * t.Rows;
            total += t.Weight;
        }
        float unit = Mathf.Min(avail / Mathf.Max(0.01f, total), 128f * k);
        for (int pass = 0; pass < _tiers.Count; pass++)
        {
            // tiers that need more than their share keep what they need; the others share what is left
            float held = 0f, free = 0f;
            foreach (var t in _tiers)
            {
                if (t.Need > unit * t.Weight) held += t.Need; else free += t.Weight;
            }
            if (free <= 0f) break;
            float u = Mathf.Min((avail - held) / free, 128f * k);
            if (u >= unit - 0.01f) break;
            unit = Mathf.Max(0f, u);
        }
        float used = 0f;
        foreach (var t in _tiers)
        {
            t.Height = Mathf.Max(t.Need, unit * t.Weight);
            used += t.Height;
        }
        // not even the minimum fits (it does on a 1920×1080 canvas at every text scale): shrink all tiers evenly
        float squeeze = used > avail && used > 0f ? Mathf.Max(0.1f, avail / used) : 1f;
        if (squeeze < 0.999f) Log.Debug("ui", $"skill tree tiers squeezed ×{squeeze:0.00}: need {used:0}, have {avail:0}");
        float rowUnit = Mathf.Max(rowNeed, unit) * squeeze;
        used = used * squeeze + gapB * (_tiers.Count - 1);
        float y = _treeR.Position.Y + (_treeR.Size.Y - used) * 0.5f;

        float skillR = Mathf.Min(32f * k, rowUnit * 0.33f);
        float passR = Mathf.Min(23f * k, rowUnit * 0.24f), keyR = Mathf.Min(27f * k, rowUnit * 0.29f);
        float hubR = Mathf.Max(Mathf.Min(17f * k, rowUnit * 0.18f) * 1.2f, hubNeed);

        foreach (var t in _tiers)
        {
            float hT = t.Height * squeeze;
            t.Band = new Rect2(bandL, y, bandR - bandL, hT);
            float cy = y + hT * 0.5f;
            y += hT + gapB;

            var hub = new TNode { Kind = NK.Hub, Tier = t, R = hubR, Pos = new Vector2(_trunkX, cy), Glyph = t.Level.ToString(CultureInfo.InvariantCulture) };
            // under a forged frame the dark fill stops inside the rim, so no edge of it shows around the art
            hub.Fill = Ngon(hub.Pos, hub.R * (_aHubOff != null && _aHubOn != null ? 0.9f : 1f), 4, 0f, false);
            hub.Line = Ngon(hub.Pos, hub.R, 4, 0f, true);
            t.Hub = hub;
            _nodes.Add(hub);

            float pitch = hT / t.Rows;
            if (t.Capstone)
            {
                for (int j = 0; j < t.Passives.Count; j++)
                {
                    int side = j % 2 == 0 ? -1 : 1, line = j / 2;
                    PlaceKey(t.Passives[j], t, side, cy + (line - (t.Rows - 1) * 0.5f) * pitch, hx, keyR);
                }
                continue;
            }
            int skillSide = SkillSide(t);
            for (int i = 0; i < t.Skills.Count; i++)
                PlaceSkill(t.Skills[i], t, skillSide, cy + (i - (t.Skills.Count - 1) * 0.5f) * pitch, hx, skillR, upR, abOff, minX, maxX);
            // the passives of one side share one column: as far out as their widest line allows
            float passRoom = (skillSide > 0 ? halfL - edgeL - pTextL : halfR - edgeR - pTextR) - passR - passGap;
            float passOff = Mathf.Clamp(passRoom, pMin, pMax);
            for (int i = 0; i < t.Passives.Count; i++)
                PlacePassive(t.Passives[i], t, -skillSide, cy + (i - (t.Passives.Count - 1) * 0.5f) * pitch, hx, passOff, passR, minX, maxX);
        }
    }

    private Vector2[] LinkFromHub(Tier t, Vector2 to, int side, float hx)
    {
        var hub = t.Hub.Pos;
        return Mathf.Abs(to.Y - hub.Y) < 1f
            ? new[] { hub, to }
            : new[] { hub, new Vector2(_trunkX + side * 58f * hx, to.Y), to };
    }

    private void PlaceSkill(TNode n, Tier t, int side, float ry, float hx, float skillR, float upR, float abOff, float minX, float maxX)
    {
        float k = _k;
        var s = n.Skill;
        n.Side = side;
        n.R = skillR;
        n.Pos = new Vector2(_trunkX + side * 140f * hx, ry);
        n.Fill = Ngon(n.Pos, skillR * 1.08f * (_aOct[0] != null ? 0.94f : 1f), 8, Mathf.Pi / 8f, false);
        n.Line = Ngon(n.Pos, skillR * 1.08f, 8, Mathf.Pi / 8f, true);
        n.Link = LinkFromHub(t, n.Pos, side, hx);
        _nodes.Add(n);
        _skillNodes[s.Id] = n;

        TNode stem = n;
        if (s.Enh != null) stem = Satellite(n, NK.Enh, s.Enh, new Vector2(n.Pos.X + side * 78f * hx, ry), upR, n);
        float fx = stem.Pos.X + side * 64f * hx;
        if (s.A != null) Satellite(n, NK.UpA, s.A, new Vector2(fx, ry - abOff), upR, stem);
        if (s.B != null) Satellite(n, NK.UpB, s.B, new Vector2(fx, ry + abOff), upR, stem);

        // the label starts beyond the branch nodes; its lines were measured in LayoutTree, which also made the room
        n.LabelX = fx + side * (upR + 16f * k);
        n.TextW = Mathf.Max(60f * k, side > 0 ? maxX - 14f * k - n.LabelX : n.LabelX - minX - 6f * k);
    }

    private TNode Satellite(TNode skill, NK kind, SkillUp up, Vector2 pos, float r, TNode from)
    {
        var u = new TNode
        {
            Kind = kind, Skill = skill.Skill, Up = up, Tier = skill.Tier, Side = skill.Side, Pos = pos, R = r,
            Glyph = string.IsNullOrEmpty(up.Name) ? "?" : up.Name.Substring(0, 1),
            Link = new[] { from.Pos, pos },
        };
        _nodes.Add(u);
        return u;
    }

    private void PlacePassive(TNode n, Tier t, int side, float py, float hx, float off, float r, float minX, float maxX)
    {
        float k = _k;
        n.Side = side;
        n.R = r;
        n.Pos = new Vector2(_trunkX + side * off, py);
        n.Link = LinkFromHub(t, n.Pos, side, hx);
        n.LabelX = n.Pos.X + side * (r + 14f * k);
        n.TextW = Mathf.Max(60f * k, side > 0 ? maxX - 14f * k - n.LabelX : n.LabelX - minX - 6f * k);
        _nodes.Add(n);
    }

    /// <summary>A key passive of the capstone. Its text column (TextW) and line count were set in LayoutTree.</summary>
    private void PlaceKey(TNode n, Tier t, int side, float py, float hx, float r)
    {
        float k = _k;
        n.Side = side;
        n.R = r;
        n.Pos = new Vector2(_trunkX + side * 150f * hx, py);
        n.Link = LinkFromHub(t, n.Pos, side, hx);
        n.LabelX = n.Pos.X + side * (r + 18f * k);
        _nodes.Add(n);
    }

    private void LayoutLeft(ClassId cls)
    {
        float k = _k;
        float x = _leftR.Position.X + _leftPad, w = _leftR.Size.X - 2f * _leftPad, y0 = _leftR.Position.Y;

        // title line, the rule under it and the points card: each as tall as its text (the forged frame's moulding
        // is 9 px: the title starts a little lower so it keeps its air)
        bool art = ArtOn;
        float lead = (art ? 14f : 10f) * k;
        _titleBase = y0 + lead + Asc(_fTitle, ZTitle);
        _ruleY = y0 + lead + 2f * k + LineH(_fTitle, ZTitle);
        _numW = Mathf.Max(104f * k, TextWidth(_fUi, "888", ZBig) + 4f * k);   // the figure's box: three digits always fit
        float caption = LineH(_fTitle, ZName) + _tight + LineH(_fUi, ZCap);
        _pointsNode = new TNode { Kind = NK.Points, Box = true, Rect = new Rect2(x, _ruleY + 12f * k, w, Mathf.Max(92f * k, caption + 20f * k)) };
        _nodes.Add(_pointsNode);

        // heading of the specialisations with its hint line
        float y = _pointsNode.Rect.End.Y + 16f * k;
        _specHeadY = y + Asc(_fTitle, ZHead);
        y += LineH(_fTitle, ZHead) + _tight;
        _specHintY = y + Asc(_fUi, ZCap);
        y += LineH(_fUi, ZCap) + 6f * k;

        // the two buttons are as tall as their text needs (their font size follows the text scale)
        float btnH = 46f * k;
        if (_resetBtn != null) btnH = Mathf.Max(btnH, _resetBtn.GetCombinedMinimumSize().Y);
        if (_closeBtn != null) btnH = Mathf.Max(btnH, _closeBtn.GetCombinedMinimumSize().Y);
        float btnY = _leftR.End.Y - 16f * k - btnH, limit = btnY - 8f * k;
        limit -= LayoutBookTab(cls, x, w, btnY, btnH, k);     // [golem] the book button above reset / close

        // specialisation cards: icon and name on one row, the description wrapped below it across the whole card.
        // Each card is as tall as its own text; only if the three do not fit are the descriptions cut to fewer lines.
        var specs = SkillDb.SpecsOf(cls).ToList();
        // a forged card has a 9 px moulding: its content keeps the padding of a sub-panel
        _cardPadX = (_aCard != null ? 18f : 14f) * k;
        _cardPadY = (_aCard != null ? 14f : 10f) * k;
        float padX = _cardPadX, padY = _cardPadY, descH = LineH(_fBody, ZRow), descW = w - 2f * padX;
        _specHead = Mathf.Max(52f * k, LineH(_fTitle, ZName));
        var lines = new int[specs.Count];
        for (int i = 0; i < specs.Count; i++) lines[i] = LinesOf(_fBody, specs[i].Desc, descW, ZRow);
        int cap = 4;
        for (; cap > 1; cap--)
        {
            float sum = (specs.Count - 1) * 10f * k;
            foreach (int l in lines) sum += 2f * padY + _specHead + (l > 0 ? 4f * k + Math.Min(l, cap) * descH : 0f);
            if (y + sum <= limit) break;
        }
        float cy = y;
        for (int i = 0; i < specs.Count; i++)
        {
            var sp = specs[i];
            int l = Math.Min(lines[i], cap);
            float ch = 2f * padY + _specHead + (l > 0 ? 4f * k + l * descH : 0f);
            if (cy + ch > limit) break;
            _nodes.Add(new TNode
            {
                Kind = NK.Spec, Box = true, Spec = sp, Rect = new Rect2(x, cy, w, ch), Label = sp.Name, Sub = sp.Desc, Lines = l,
                Icon = IconNamed(SpecIcons.TryGetValue(sp.Id, out var ic) ? ic : "star-formation"),
            });
            cy += ch + 10f * k;
        }

        // [fixes] 職業特性: the class's weapon bonuses (read-only, not bought with points), when the column has room
        _traitY = -1f;
        _traits.Clear();
        var th = Game.I?.Hero;
        if (th != null && th.Class == cls)
        {
            if (cls == ClassId.Forgebody)
            {
                _traits.Add(($"鍛體　天生：傷害減免 +{Hero.ForgedBodyDrPct:0}%", true));
                _traits.Add(($"拳刃精通　雙持拳刃：武器傷害 +{Hero.FistMasteryPct(th.D.Level):0}%", th.FistMastery));
            }
            else if (cls == ClassId.Pactbinder)
            {
                float pf = Hero.PactEarlyFrac(th.D.Level);
                // gone from level 8 on (the class intro still names it)
                if (pf > 0f) _traits.Add(($"契約初成　等級 {Hero.PactEarlyEndLevel} 前：傷害 +{Hero.PactEarlyDmgPct * pf:0}%、生命 +{Hero.PactEarlyLifePct * pf:0}%", true));
            }
            else if (cls == ClassId.Lampknight)
            {
                bool shield = th.OffHand?.Base == "shield", lantern = th.OffHand?.Base == "lantern";
                string sv = shield ? $"+{Hero.ShieldOathFor(th.OffHand, th.MainHand):0.#}%" : "依盾的格擋率提高";
                _traits.Add(($"盾誓　單手持盾：武器傷害 {sv}", shield && th.OathPct > 0f));
                if (Hero.LanternOathPct() > 0f) _traits.Add(($"燈誓　單手提燈：武器傷害 +{Hero.LanternOathPct():0}%（火焰）", lantern && th.OathPct > 0f));
            }
        }
        if (_traits.Count > 0)
        {
            // each line wraps to at most two rows in the narrow left column (特大); when that does not fit, the compact
            // form (name and value on one row, 「拳刃精通 +8%」) is tried before the block is left out
            float TraitsH()
            {
                float h = LineH(_fTitle, ZRow) + 2f * k;
                foreach (var (text, on) in _traits) h += Math.Clamp(LinesOf(_fUi, TraitLine(text, on), w, ZCap), 1, 2) * LineH(_fUi, ZCap);
                return h;
            }
            float tH = TraitsH();
            if (cy + 6f * k + tH > limit)
            {
                for (int i = 0; i < _traits.Count; i++) _traits[i] = (Compact(_traits[i].text), _traits[i].on);
                tH = TraitsH();
            }
            if (cy + 6f * k + tH > limit && _traits.Count > 1)
            {
                // last resort: every trait on one row,「◆ 鍛體 +8%　◇ 拳刃精通 +12%」(the first marker comes from TraitLine)
                var sb = new System.Text.StringBuilder(_traits[0].text);
                for (int i = 1; i < _traits.Count; i++) sb.Append(_traits[i].on ? "　◆ " : "　◇ ").Append(_traits[i].text);
                bool on0 = _traits[0].on;
                _traits.Clear();
                _traits.Add((sb.ToString(), on0));
                tH = TraitsH();
            }
            if (cy + 6f * k + tH <= limit) { _traitY = cy + 6f * k; cy = _traitY + tH + 4f * k; }
        }

        // the key legend is a nicety: it shows as many of its rows as the column still has room for (a row wraps
        // to two lines when its text is wider than the column), and not at all when fewer than two would fit
        // (with the art the real keys sit on key caps: the column is wider by a cap's padding and the rows get air)
        _helpY = -1f;
        _helpRows = 0;
        _helpKeyW = 0f;
        _keyPad = _aKeycap != null ? 9f * k : 0f;           // the key caps under the slots
        _helpPad = _aKeycap != null ? 6f * k : 0f;          // those of this legend: tighter, the text beside them needs the room
        _helpGap = _aKeycap != null ? 5f * k : 0f;
        for (int i = 0; i < HelpKeys.Length; i++)
        {
            _helpTw[i] = TextWidth(_fUi, HelpKeys[i], ZCap);
            _helpKeyW = Mathf.Max(_helpKeyW, _helpTw[i] + 2f * _helpPad);
        }
        _helpKeyW += (_aKeycap != null ? 9f : 14f) * k;
        float helpTop = cy + 6f * k, hy = helpTop + LineH(_fTitle, ZRow) + 2f * k + _helpGap;
        for (int i = 0; i < HelpKeys.Length; i++)
        {
            _helpH[i] = Math.Clamp(LinesOf(_fUi, HelpTexts[i], w - _helpKeyW, ZCap), 1, 2) * LineH(_fUi, ZCap);
            if (_helpRows != i || hy + _helpH[i] > limit) continue;
            hy += _helpH[i] + _helpGap;
            _helpRows++;
        }
        if (_helpRows >= 2) _helpY = helpTop; else _helpRows = 0;

        float resetW = Mathf.Floor(w * 0.6f);
        if (_resetBtn != null) { _resetBtn.Position = new Vector2(x, btnY); _resetBtn.Size = new Vector2(resetW, btnH); }
        if (_closeBtn != null) { _closeBtn.Position = new Vector2(x + resetW + 10f * k, btnY); _closeBtn.Size = new Vector2(w - resetW - 10f * k, btnH); }
    }

    private void LayoutBar()
    {
        float k = _k;
        float bx = _barR.Position.X, by = _barR.Position.Y;

        // the heading block (title over its caption) is centred in the strip; the slots start where its text ends
        float headW = Mathf.Max(TextWidth(_fTitle, BarTitle, ZHead), TextWidth(_fUi, BarCaption, ZCap));
        float headH = LineH(_fTitle, ZHead), top = by + (_barR.Size.Y - (headH + _tight + LineH(_fUi, ZCap))) * 0.5f;
        _barHeadY = top + Asc(_fTitle, ZHead);
        _barCapY = top + headH + _tight + Asc(_fUi, ZCap);

        // (a key cap under a slot may be wider than the slot at the largest text: the slots then stand further apart)
        float sz = 60f * k, step = 76f * k, sx = bx + Mathf.Max(196f * k, 18f * k + headW + 24f * k);
        if (_aKeycap != null)
            foreach (string key in SkillText.SlotKeys) step = Mathf.Max(step, TextWidth(_fUi, key, ZCap) + 2f * _keyPad + 8f * k);
        for (int i = 0; i < 6; i++)
        {
            var n = new TNode { Kind = NK.Slot, Box = true, Index = i, Rect = new Rect2(sx + i * step, by + _slotTop, sz, sz), Label = SkillText.SlotKeys[i] };
            n.NameW = TextWidth(_fUi, n.Label, ZCap);
            _slots[i] = n;
            _nodes.Add(n);
        }
        _pickX = sx + 5f * step + sz + 16f * k + 18f * k;

        // the two hint lines are set at description size when both fit beside the slots, at caption size otherwise
        float room = BarRight - _pickX - 2f * k;
        _hintSize =TextWidth(_fBody, BarHint1, ZRow) <= room && TextWidth(_fBody, BarHint2, ZRow) <= room ? ZRow : ZCap;
    }

    /// <summary>How far the text and the offered skills of the strip stay from its top and bottom edge (clear of the moulding).</summary>
    private float BarInset => (ArtOn ? 13f : 8f) * _k;
    /// <summary>Where the strip's text and offered skills end on the right.</summary>
    private float BarRight => _barR.End.X - (ArtOn ? 18f : 12f) * _k;

    /// <summary>Top of the row of skills offered while a slot is being assigned: under the line that says which slot.</summary>
    private float PickTop => _barR.Position.Y + BarInset + 2f * _k + LineH(_fUi, ZRow);

    // ───────────────────────── state ─────────────────────────

    private void UpdateState()
    {
        var h = _hero;
        if (h == null) return;
        int lvl = h.D.Level, pts = h.SkillPoints;
        _points = pts;
        _ptsText = pts.ToString(CultureInfo.InvariantCulture);
        _spentText = $"已分配 {Math.Max(0, h.SkillPointsSpent)} 點";
        _classText = $"{Names.Class(h.Class)}　等級 {lvl}";
        bool specOpen = lvl >= SkillDb.SpecLevel;
        string specId = h.D.Spec;
        bool hasSpec = false;
        foreach (var n in _nodes) if (n.Kind == NK.Spec && n.Spec.Id == specId) { hasSpec = true; break; }
        _specHint = !specOpen ? $"等級 {SkillDb.SpecLevel} 解鎖" : hasSpec ? "可隨時更換，不需任何代價" : "選擇一項專精";
        _specHintCol = !specOpen ? CLockText : hasSpec ? UiTheme.Dim : Palette.Ember;
        if (_assign >= 0) _assignText = $"選擇要放入「{SkillText.SlotKeys[_assign]}」的技能（右鍵取消）";

        bool keyOwned = false;
        foreach (var n in _nodes) if (n.Kind == NK.Key && h.BaseRank(n.Passive.Id) > 0) { keyOwned = true; break; }
        foreach (var t in _tiers) t.Open = lvl >= t.Level;

        bool any = pts > 0 || _assign >= 0;
        var bar = h.D.Bar;
        foreach (var n in _nodes)
        {
            switch (n.Kind)
            {
                case NK.Hub:
                    n.Owned = n.Tier.Open;
                    break;
                case NK.Skill:
                {
                    var s = n.Skill;
                    int r = h.BaseRank(s.Id);
                    n.Rank = r;
                    n.Owned = r > 0;
                    n.Bonus = r > 0 ? Math.Max(0, h.Rank(s.Id) - r) : 0;
                    n.RankText = n.Bonus > 0 ? "+" + n.Bonus.ToString(CultureInfo.InvariantCulture) : null;
                    n.Locked = lvl < s.UnlockLevel;
                    n.Can = h.CanLearn(s);
                    break;
                }
                case NK.Enh:
                case NK.UpA:
                case NK.UpB:
                {
                    var s = n.Skill;
                    var u = n.Up;
                    bool learned = h.BaseRank(s.Id) > 0;
                    bool enh = s.Enh != null && h.HasUp(s.Enh.Id);
                    var other = u == s.A ? s.B : u == s.B ? s.A : null;
                    n.Owned = h.HasUp(u.Id);
                    n.Locked = !learned || (u != s.Enh && !enh);
                    n.Swap = !n.Owned && !n.Locked && other != null && h.HasUp(other.Id);
                    n.Can = !n.Owned && !n.Swap && s.Enh != null && h.CanTakeUp(s, u);
                    break;
                }
                case NK.Passive:
                {
                    var p = n.Passive;
                    int r = h.BaseRank(p.Id);
                    n.Rank = r;
                    n.Owned = r > 0;
                    n.Locked = lvl < p.UnlockLevel;
                    n.Can = pts > 0 && !n.Locked && r < p.MaxRank;
                    break;
                }
                case NK.Key:
                {
                    var p = n.Passive;
                    n.Rank = h.BaseRank(p.Id) > 0 ? 1 : 0;
                    n.Owned = n.Rank > 0;
                    n.Locked = lvl < p.UnlockLevel;
                    n.Swap = !n.Owned && !n.Locked && keyOwned;
                    n.Can = !n.Owned && !n.Locked && !keyOwned && pts > 0;
                    break;
                }
                case NK.Spec:
                    n.Owned = n.Spec.Id == specId;
                    n.Locked = !specOpen;
                    n.Can = specOpen && !hasSpec;
                    break;
                case NK.Slot:
                {
                    string id = bar != null && n.Index < bar.Length ? bar[n.Index] : null;
                    var s = id != null && h.BaseRank(id) > 0 ? SkillDb.Get(id) : null;
                    n.Skill = s;
                    n.Owned = s != null;
                    n.Icon = s == null ? null : _skillNodes.TryGetValue(s.Id, out var sn) ? sn.Icon : IconOf(s);
                    break;
                }
            }
            if (n.Can) any = true;
        }
        _anyGlow = any;

        BuildPicks();
        if (_hover != null) _hover = PickAt(Ui.PointerIn(this));
    }

    private void BuildPicks()
    {
        _picks.Clear();
        if (_assign < 0) return;
        float k = _k;
        // under the instruction line, in whatever height the strip has left
        float y = PickTop, sz = Mathf.Clamp(_barR.End.Y - BarInset - y, 24f * k, 50f * k), x = _pickX, right = BarRight;
        // the left mouse slot offers the basic skill first: that is where it belongs by default
        for (int pass = 0; pass < 2; pass++)
            foreach (var t in _tiers)
                foreach (var sn in t.Skills)
                {
                    if (!sn.Owned) continue;
                    bool basic = sn.Skill.Cat == SkillCat.Basic;
                    bool take = _assign == 0 ? (pass == 0) == basic : pass == 0;
                    if (!take || x + sz > right) continue;
                    _picks.Add(new TNode { Kind = NK.Pick, Box = true, Skill = sn.Skill, Icon = sn.Icon, Owned = true, Rect = new Rect2(x, y, sz, sz) });
                    x += sz + 10f * k;
                }
    }

    /// <summary>Drops bar entries that are no longer learned; an emptied left-mouse slot falls back to the basic skill
    /// unless that skill is already bound elsewhere.</summary>
    private bool NormalizeBar()
    {
        var h = _hero;
        var bar = h?.D.Bar;
        if (bar == null || bar.Length < 6) return false;
        bool changed = false;
        for (int i = 0; i < 6; i++)
            if (bar[i] != null && (SkillDb.Get(bar[i]) == null || h.BaseRank(bar[i]) <= 0)) { bar[i] = null; changed = true; }
        if (bar[0] == null)
        {
            foreach (var s in SkillDb.Of(h.Class))
            {
                if (s.Cat != SkillCat.Basic || h.BaseRank(s.Id) <= 0) continue;
                if (Array.IndexOf(bar, s.Id) < 0) { bar[0] = s.Id; changed = true; }
                break;
            }
        }
        return changed;
    }

    // ───────────────────────── input ─────────────────────────

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);
        if (!_built || _hero == null || _tiers.Count == 0) return;
        try
        {
            if (@event is InputEventMouseMotion mm) SetHover(PickAt(mm.Position));
            else if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex != MouseButton.Left && mb.ButtonIndex != MouseButton.Right) return;
                var n = PickAt(mb.Position);
                SetHover(n);
                OnClick(n, mb.ButtonIndex == MouseButton.Right);
                AcceptEvent();
            }
        }
        catch (Exception e) { Fail("input", e); }
    }

    private TNode PickAt(Vector2 p)
    {
        float tol = 3f * _k;
        for (int i = 0; i < _picks.Count; i++) if (_picks[i].Hit(p, tol)) return _picks[i];
        for (int i = _nodes.Count - 1; i >= 0; i--) if (_nodes[i].Hit(p, tol)) return _nodes[i];
        return null;
    }

    private void SetHover(TNode n)
    {
        if (ReferenceEquals(n, _hover)) return;
        _hover = n;
        _over?.QueueRedraw();
        RefreshTooltip();
    }

    private void ClearHover()
    {
        _hover = null;
        _over?.QueueRedraw();
        if (_tipShown) { _tipShown = false; Game.I?.Ui?.HideTooltip(); }
    }

    private void RefreshTooltip()
    {
        var ui = Game.I?.Ui;
        if (ui == null) return;
        var n = _hover;
        string bb = n == null || _hero == null ? null : TooltipFor(n);
        if (string.IsNullOrEmpty(bb))
        {
            if (_tipShown) { _tipShown = false; ui.HideTooltip(); }
            return;
        }
        // anchored to the node, not the cursor: the card sits beside what it describes instead of on top of it
        if (n.Kind == NK.Slot || n.Kind == NK.Pick)
        {
            // entries of the bottom strip: the card floats above the strip so it never hides the row being picked from
            Vector2 g = GetGlobalTransform() * new Vector2(n.Rect.End.X + 6f * _k, _barR.Position.Y - 10f * _k);
            g.Y -= TipHeight(bb);
            g.Y -= 12f;         // the manager drops the card a little below the point it is given
            ui.ShowTooltip(bb, g);
        }
        else
        {
            // beside the node's box: to its right, or — the cards are wide now — to its left when the screen ends first
            Rect2 a = n.Box
                ? new Rect2(n.Rect.Position.X - 6f * _k, n.Rect.Position.Y, n.Rect.Size.X + 12f * _k, n.Rect.Size.Y)
                : new Rect2(n.Pos.X - n.R - 8f * _k, n.Pos.Y - n.R, 2f * n.R + 16f * _k, 2f * n.R);
            ui.ShowTooltipAt(GetGlobalTransform() * a, bb);
        }
        _tipShown = true;
    }

    /// <summary>
    /// Generous estimate of a tooltip card's height, from the manager's own measures: the text column
    /// (UiTheme.TipWidth), running text at UiTheme.FsBody and the heading line at SkillText.NameSize, all at the
    /// text scale in force.
    /// </summary>
    private static float TipHeight(string bb)
    {
        int body = UiTheme.Fs(UiTheme.FsBody);
        Font f = UiTheme.Body ?? ThemeDB.FallbackFont;
        float lineH = f.GetHeight(body) + 2f;                                   // the card's line_separation
        int perLine = Math.Max(8, (int)(UiTheme.TipWidth / body) - 1) * 2;      // in half-width units, one glyph short of the column
        int lines = 0, units = 0;
        bool tag = false;
        foreach (char ch in bb)
        {
            if (ch == '\n') { lines += Math.Max(1, (units + perLine - 1) / perLine); units = 0; }
            else if (ch == '[') tag = true;
            else if (ch == ']') tag = false;
            else if (!tag) units += ch < 0x2E80 ? 1 : 2;
        }
        lines += Math.Max(1, (units + perLine - 1) / perLine);
        float heading = f.GetHeight(UiTheme.Fs(SkillText.NameSize)) - f.GetHeight(body);   // the first line is set larger
        return 30f + lines * lineH + Mathf.Max(0f, heading);
    }

    private void OnClick(TNode n, bool right)
    {
        var h = _hero;
        if (h == null) return;
        if (_assign >= 0 && (n == null || (right && n.Kind != NK.Slot) || n.Kind is not (NK.Skill or NK.Slot or NK.Pick)))
        {
            _assign = -1;                       // anything but a skill or a slot cancels the assignment
            _dirty = true;
            return;
        }
        if (n == null) return;
        switch (n.Kind)
        {
            case NK.Skill:
                if (_assign >= 0) AssignTo(_assign, n.Skill, n);
                else if (right) RefundSkill(h, n);
                else LearnSkill(h, n);
                break;
            case NK.Enh:
            case NK.UpA:
            case NK.UpB:
                if (right) RefundUp(h, n); else TakeUp(h, n);
                break;
            case NK.Passive:
            case NK.Key:
                if (right) RefundPassive(h, n); else LearnPassive(h, n);
                break;
            case NK.Spec:
                if (!right) ChooseSpec(h, n);
                break;
            case NK.Slot:
                if (right) ClearSlot(h, n);
                else
                {
                    _assign = _assign == n.Index ? -1 : n.Index;
                    _dirty = true;
                    Sfx("ui_click", 0.6f);
                }
                break;
            case NK.Pick:
                if (!right) AssignTo(_assign, n.Skill, n);
                break;
        }
    }

    // ───────────────────────── actions ─────────────────────────

    private static void Sfx(string name, float volume = 1f)
    {
        try { Snd.Ui(name, volume); }
        catch (Exception e) { Log.Warn("ui", $"skill tree sound '{name}' failed: {e.Message}"); }
    }

    private void Ok(TNode n, Color c, string sound = "ui_click", float volume = 0.9f)
    {
        if (_pops.Count < 12) _pops.Add(new Pop { Pos = n.Center, R = n.Box ? 26f * _k : n.R, C = c, Dur = 0.5f });
        Sfx(sound, volume);
    }

    private void Say(TNode n, string text, Color c)
    {
        for (int i = _pops.Count - 1; i >= 0; i--) if (_pops[i].Text != null) _pops.RemoveAt(i);     // one line at a time
        _pops.Add(new Pop { Pos = n.Center, R = n.Box ? n.Rect.Size.Y * 0.5f : n.R, C = c, Dur = 1.7f, Text = text });
    }

    private void Deny(TNode n, string text)
    {
        Say(n, text, Palette.Bad);
        Sfx("ui_error", 0.7f);
    }

    private static bool LastBasic(Hero h, SkillDef s) =>
        s.Cat == SkillCat.Basic && SkillDb.Of(h.Class).Count(x => x.Cat == SkillCat.Basic && h.BaseRank(x.Id) > 0) <= 1;

    private void LearnSkill(Hero h, TNode n)
    {
        var s = n.Skill;
        int before = h.BaseRank(s.Id);
        if (h.Learn(s.Id))
        {
            Log.Info("skills", $"{(before == 0 ? "learned" : "ranked up")} {s.Id} → {before + 1}/{SkillDef.MaxRank}, points left {h.SkillPoints}");
            Ok(n, before == 0 ? UiTheme.Gold : Palette.Ember);
        }
        else Deny(n, h.D.Level < s.UnlockLevel ? $"需要等級 {s.UnlockLevel}" : before >= SkillDef.MaxRank ? "已達最高等級" : "技能點數不足");
    }

    private void RefundSkill(Hero h, TNode n)
    {
        var s = n.Skill;
        int before = h.BaseRank(s.Id);
        if (before <= 0) return;
        if (h.Unlearn(s.Id))
        {
            Log.Info("skills", $"refunded {s.Id} {before} → {before - 1}, points left {h.SkillPoints}");
            if (NormalizeBar()) h.Notify();
            Ok(n, UiTheme.Dim, "ui_click", 0.55f);
        }
        else Deny(n, "必須保留一項基本技能");
    }

    private void TakeUp(Hero h, TNode n)
    {
        var s = n.Skill;
        var u = n.Up;
        if (h.HasUp(u.Id)) return;
        bool swap = n.Swap;
        if (s.Enh != null && h.TakeUp(s.Id, u.Id))
        {
            Log.Info("skills", $"{(swap ? "switched to" : "took")} upgrade {u.Id} ({u.Name}), points left {h.SkillPoints}");
            Ok(n, UiTheme.Gold);
        }
        else Deny(n, h.BaseRank(s.Id) < 1 ? $"需先習得「{s.Name}」" : u != s.Enh && s.Enh != null && !h.HasUp(s.Enh.Id) ? $"需先取得「{s.Enh.Name}」" : "技能點數不足");
    }

    /// <summary>Hero has no single-upgrade refund, so the list is edited here; dropping the enhancement also drops the branch that needs it.</summary>
    private void RefundUp(Hero h, TNode n)
    {
        var s = n.Skill;
        var u = n.Up;
        if (!h.HasUp(u.Id)) return;
        int removed = 0;
        if (u == s.Enh)
        {
            if (s.A != null && h.D.Ups.Remove(s.A.Id)) removed++;
            if (s.B != null && h.D.Ups.Remove(s.B.Id)) removed++;
        }
        if (h.D.Ups.Remove(u.Id)) removed++;
        h.Recalc();
        Log.Info("skills", $"refunded upgrade {u.Id} ({removed} point{(removed == 1 ? "" : "s")}), points left {h.SkillPoints}");
        Ok(n, UiTheme.Dim, "ui_click", 0.55f);
    }

    private void LearnPassive(Hero h, TNode n)
    {
        var p = n.Passive;
        int before = h.BaseRank(p.Id);
        if (p.Key && before > 0) return;
        if (p.Key && h.SkillPoints <= 0 && h.D.Level >= p.UnlockLevel)
        {
            // switching the capstone is free: release the other key passive first so its point pays for this one
            foreach (var o in SkillDb.PassivesOf(h.Class))
                if (o.Key && o.Id != p.Id && h.BaseRank(o.Id) > 0) { h.Unlearn(o.Id); break; }
        }
        if (h.Learn(p.Id))
        {
            Log.Info("skills", $"passive {p.Id} ({p.Name}) → {before + 1}/{p.MaxRank}, points left {h.SkillPoints}");
            Ok(n, p.Key ? UiTheme.Gold : Palette.Ember, p.Key ? "ember_buff" : "ui_click", p.Key ? 0.7f : 0.9f);
        }
        else Deny(n, h.D.Level < p.UnlockLevel ? $"需要等級 {p.UnlockLevel}" : before >= p.MaxRank ? "已達最高等級" : "技能點數不足");
    }

    private void RefundPassive(Hero h, TNode n)
    {
        var p = n.Passive;
        int before = h.BaseRank(p.Id);
        if (before <= 0) return;
        if (h.Unlearn(p.Id))
        {
            Log.Info("skills", $"refunded passive {p.Id} {before} → {before - 1}, points left {h.SkillPoints}");
            Ok(n, UiTheme.Dim, "ui_click", 0.55f);
        }
    }

    private void ChooseSpec(Hero h, TNode n)
    {
        var sp = n.Spec;
        if (h.D.Spec == sp.Id) return;
        string old = h.D.Spec;
        if (h.SetSpec(sp.Id))
        {
            Log.Info("skills", $"specialisation {old ?? "-"} → {sp.Id} ({sp.Name})");
            Ok(n, UiTheme.Gold, "ember_buff", 0.7f);
        }
        else Deny(n, $"等級 {SkillDb.SpecLevel} 解鎖職業專精");
    }

    private void AssignTo(int slot, SkillDef s, TNode from)
    {
        var h = _hero;
        var bar = h?.D.Bar;
        if (s == null || bar == null || slot < 0 || slot >= bar.Length) return;
        if (h.BaseRank(s.Id) <= 0) { Deny(from, "尚未習得此技能"); return; }
        _assign = -1;
        _dirty = true;
        int other = Array.IndexOf(bar, s.Id);
        if (other == slot) return;
        string prev = bar[slot];
        if (other >= 0) bar[other] = prev;      // already on the bar: the two slots trade places
        bar[slot] = s.Id;
        Log.Info("skills", $"bar slot {slot} ({SkillText.SlotKeys[slot]}) ← {s.Id}{(other >= 0 ? $", slot {other} ← {prev ?? "-"}" : "")}");
        var target = slot < _slots.Length && _slots[slot] != null ? _slots[slot] : from;
        Ok(target, CAssign, "equip_weapon", 0.6f);
        h.Notify();
    }

    private void ClearSlot(Hero h, TNode n)
    {
        int i = n.Index;
        var bar = h.D.Bar;
        if (_assign == i) { _assign = -1; _dirty = true; }
        if (bar == null || i >= bar.Length || bar[i] == null) return;
        string old = bar[i];
        bar[i] = null;
        NormalizeBar();
        Log.Info("skills", $"bar slot {i} ({SkillText.SlotKeys[i]}) cleared (was {old}){(bar[i] != null ? $", fell back to {bar[i]}" : "")}");
        if (bar[i] != null) Say(n, "左鍵欄位保留基本技能", UiTheme.Text);
        Sfx("ui_click", 0.55f);
        h.Notify();
    }

    private void OnResetPressed()
    {
        var h = _hero;
        var ui = Game.I?.Ui;
        if (h == null || ui == null) return;
        ClearHover();
        _assign = -1;
        _dirty = true;
        ui.Confirm("確定要重置所有技能、強化與被動嗎？\n技能點數將全數返還，不需任何代價。", () => Log.Guard("ui", () =>
        {
            int spent = h.SkillPointsSpent;
            h.RespecAll();
            Log.Info("skills", $"respec all: {spent} points returned, points now {h.SkillPoints}");
            Sfx("ash_crumble", 0.7f);
            _dirty = true;
        }), "重置", "取消");
    }

    private void OnClosePressed() => Game.I?.Ui?.Close(Id);

    // ───────────────────────── tooltips ─────────────────────────

    /// <summary>
    /// BBCode of the hovered node's card, built on demand (never cached). The heading sizes in it are final — they
    /// went through UiTheme.Fs in SkillText.Head — so the string is shown as it is, without UiTheme.Bb.
    /// </summary>
    private string TooltipFor(TNode n)
    {
        var h = _hero;
        string dim = SkillText.Hex(Palette.TextDim), gold = SkillText.Hex(Palette.Gold), bad = SkillText.Hex(Palette.Bad);
        switch (n.Kind)
        {
            case NK.Skill:
            {
                var s = n.Skill;
                var sb = new StringBuilder(SkillText.Tooltip(h.Eff(s.Id) ?? s, h, true), 900);
                if (_assign >= 0)
                {
                    sb.Append("\n\n").Append(n.Owned
                        ? $"[color=#{SkillText.Hex(CAssign)}]左鍵：放入「{SkillText.SlotKeys[_assign]}」欄位[/color]"
                        : $"[color=#{bad}]尚未習得，無法放入欄位[/color]");
                    return sb.ToString();
                }
                if (!n.Locked)
                {
                    sb.Append("\n\n");
                    if (n.Rank >= SkillDef.MaxRank) sb.Append($"[color=#{dim}]已達最高等級[/color]");
                    else if (h.SkillPoints <= 0) sb.Append($"[color=#{bad}]技能點數不足[/color]");
                    else sb.Append($"[color=#{gold}]左鍵：{(n.Rank == 0 ? "習得" : "提升等級")}（1 點）[/color]");
                }
                if (n.Rank > 1) sb.Append($"\n[color=#{dim}]右鍵：返還 1 級[/color]");
                else if (n.Rank == 1)
                    sb.Append(LastBasic(h, s) ? $"\n[color=#{dim}]必須保留一項基本技能[/color]" : $"\n[color=#{dim}]右鍵：遺忘此技能，返還它與其強化的全部點數[/color]");
                return sb.ToString();
            }
            case NK.Enh:
            case NK.UpA:
            case NK.UpB:
            {
                var sb = new StringBuilder(SkillText.UpgradeTooltip(n.Skill, n.Up, h), 480);
                if (n.Owned)
                    sb.Append(n.Kind == NK.Enh ? $"\n[color=#{dim}]右鍵：返還（分支強化會一併返還）[/color]" : $"\n[color=#{dim}]右鍵：返還[/color]");
                else if (n.Swap) sb.Append($"\n[color=#{gold}]左鍵：改選此分支[/color]");
                else if (n.Can) sb.Append($"\n[color=#{gold}]左鍵：取得[/color]");
                return sb.ToString();
            }
            case NK.Passive:
            case NK.Key:
            {
                var sb = new StringBuilder(SkillText.PassiveTooltip(n.Passive, h), 480);
                if (n.Can) sb.Append($"\n\n[color=#{gold}]左鍵：{(n.Kind == NK.Key ? "啟用" : n.Rank == 0 ? "習得" : "提升等級")}（1 點）[/color]");
                else if (n.Swap) sb.Append($"\n[color=#{gold}]左鍵：改選此關鍵被動[/color]");
                else if (!n.Locked && !n.Owned && n.Kind == NK.Key) sb.Append($"\n[color=#{bad}]技能點數不足[/color]");
                else if (!n.Locked && n.Kind == NK.Passive && n.Rank < n.Max) sb.Append($"\n\n[color=#{bad}]技能點數不足[/color]");
                if (n.Rank > 0) sb.Append($"\n[color=#{dim}]右鍵：返還{(n.Kind == NK.Key ? "" : " 1 級")}[/color]");
                return sb.ToString();
            }
            case NK.Hub:
            {
                var t = n.Tier;
                var sb = new StringBuilder(260);
                sb.Append(SkillText.Head(t.Title)).Append('\n');
                sb.Append(t.Open ? $"[color=#{dim}]等級 {t.Level} 開放[/color]" : $"[color=#{bad}]需要等級 {t.Level}[/color]");
                if (t.Skills.Count > 0) sb.Append("\n\n技能：").Append(string.Join("、", t.Skills.Select(x => x.Label)));
                if (t.Passives.Count > 0) sb.Append(t.Skills.Count > 0 ? "\n" : "\n\n").Append(t.Capstone ? "擇一：" : "被動：").Append(string.Join("、", t.Passives.Select(x => x.Label)));
                return sb.ToString();
            }
            case NK.Spec:
            {
                var sb = new StringBuilder(SkillText.SpecTooltip(n.Spec, h), 400);
                if (!n.Owned && !n.Locked) sb.Append($"\n[color=#{gold}]左鍵：選擇此專精[/color]");
                return sb.ToString();
            }
            case NK.Points:
                return SkillText.Head("技能點數") + "\n" +
                       "每提升一個等級獲得 1 點，部分獎勵也會給予額外的點數。\n\n" +
                       $"可用 [color=#{gold}]{h.SkillPoints}[/color] 點　已分配 {Math.Max(0, h.SkillPointsSpent)} 點\n" +
                       $"[color=#{dim}]返還與重置皆不需任何代價。[/color]";
            case NK.Slot:
            {
                string key = SkillText.SlotKeys[n.Index];
                if (n.Skill == null)
                    return SkillText.Head($"{key}欄位") + $"\n[color=#{dim}]空欄位[/color]\n\n[color=#{gold}]左鍵：選擇要放入的技能[/color]";
                var sb = new StringBuilder(SkillText.Tooltip(h.Eff(n.Skill.Id) ?? n.Skill, h), 800);
                sb.Append($"\n\n[color=#{dim}]{key}欄位[/color]\n[color=#{gold}]左鍵：更換技能[/color]　[color=#{dim}]右鍵：清空[/color]");
                return sb.ToString();
            }
            case NK.Pick:
            {
                var sb = new StringBuilder(SkillText.Tooltip(h.Eff(n.Skill.Id) ?? n.Skill, h), 800);
                if (_assign >= 0) sb.Append($"\n\n[color=#{SkillText.Hex(CAssign)}]左鍵：放入「{SkillText.SlotKeys[_assign]}」欄位[/color]");
                return sb.ToString();
            }
        }
        return null;
    }

    // ───────────────────────── drawing ─────────────────────────

    private static void Sb(Control c, StyleBox sb, Rect2 r)
    {
        if (sb != null) c.DrawStyleBox(sb, r);
        else c.DrawRect(r, new Color(0.1f, 0.09f, 0.08f, 0.92f));
    }

    // Drawing text: "size" is always a DESIGN size, converted here (Sz) and nowhere else. A width cuts the line with
    // an ellipsis when the text is wider — the layout measures first, so that is the fallback, not the rule.

    private void Txt(Control c, Font f, float x, float baseline, string s, int size, Color col, float width = -1f)
    {
        if (!string.IsNullOrEmpty(s)) c.DrawString(f, new Vector2(x, baseline), s, HorizontalAlignment.Left, width, Sz(size), col);
    }

    private void TxtR(Control c, Font f, float right, float baseline, string s, int size, Color col, float width)
    {
        if (!string.IsNullOrEmpty(s)) c.DrawString(f, new Vector2(right - width, baseline), s, HorizontalAlignment.Right, width, Sz(size), col);
    }

    private void TxtC(Control c, Font f, float cx, float baseline, string s, int size, Color col, float width)
    {
        if (!string.IsNullOrEmpty(s)) c.DrawString(f, new Vector2(cx - width * 0.5f, baseline), s, HorizontalAlignment.Center, width, Sz(size), col);
    }

    /// <summary>Centred text over a dark outline, readable on top of anything: the floating feedback of a click.</summary>
    private void TxtLit(Control c, Font f, float cx, float baseline, string s, int size, Color col, float width, Color shade)
    {
        if (string.IsNullOrEmpty(s)) return;
        int px = Sz(size);
        var pos = new Vector2(cx - width * 0.5f, baseline);
        c.DrawStringOutline(f, pos, s, HorizontalAlignment.Center, width, px, Mathf.Max(2, Mathf.RoundToInt(5f * _k)), shade);
        c.DrawString(f, pos, s, HorizontalAlignment.Center, width, px, col);
    }

    /// <summary>A wrapped paragraph of at most <paramref name="maxLines"/> lines; <paramref name="baseline"/> is that of its first line.</summary>
    private void Para(Control c, Font f, float x, float baseline, string s, int size, Color col, float width, int maxLines, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        if (!string.IsNullOrEmpty(s)) c.DrawMultilineString(f, new Vector2(x, baseline), s, align, width, Sz(size), maxLines, col, Brk);
    }

    private static void Halo(Control c, Texture2D tex, Vector2 p, float r, Color col, float alpha)
    {
        if (tex != null) c.DrawTextureRect(tex, new Rect2(p.X - r, p.Y - r, r * 2f, r * 2f), false, new Color(col.R, col.G, col.B, alpha));
    }

    private static void IconAt(Control c, Texture2D tex, Vector2 p, float half, Color col)
    {
        if (tex != null) c.DrawTextureRect(tex, new Rect2(p.X - half, p.Y - half, half * 2f, half * 2f), false, Icons.Tint(tex, col));
    }

    // layer 0: everything opaque that sits under the glow
    private void DrawBack(Control c)
    {
        var bg = UiTheme.Bg;
        c.DrawRect(new Rect2(Vector2.Zero, Size), new Color(bg.R, bg.G, bg.B, 0.97f));
        if (_hero == null || _tiers.Count == 0) return;
        float k = _k;
        // weights: three forged areas (sub-panel moulding on slate) > cards (soot) > inset wells (tier bands, the
        // points box) > cells. Without the art everything is the flat boxes built in EnsureBuilt.
        Area(c, _leftR);
        Area(c, _treeR);
        Area(c, _barR);
        var tex = GlowTex();
        bool art = _aWell != null;
        // an ember haze at the foot of the trunk: the tree grows out of the fire (the flat bands are translucent and
        // lie over it; the forged wells are opaque, so there it glows over them)
        float gr = Mathf.Min(_treeR.Size.Y * 0.62f, _treeR.Size.X * 0.4f);
        var haze = new Rect2(_trunkX - gr, _treeR.End.Y - gr - (ArtOn ? 12f : 2f) * k, gr * 2f, gr);
        if (tex != null && !art) c.DrawTextureRectRegion(tex, haze, new Rect2(0f, 0f, 128f, 64f), new Color(1f, 0.42f, 0.12f, 0.1f));
        foreach (var t in _tiers) Band(c, t);
        if (tex != null && art) c.DrawTextureRectRegion(tex, haze, new Rect2(0f, 0f, 128f, 64f), new Color(1f, 0.42f, 0.12f, 0.085f));
        foreach (var n in _nodes)
        {
            if (n.Kind == NK.Spec) Card(c, n);
            else if (n.Kind == NK.Points) Inset(c, n.Rect);
            else if (n.Kind == NK.Slot) Cell(c, n.Rect);
        }
        foreach (var p in _picks) Cell(c, p.Rect);
    }

    /// <summary>One of the three areas: the sub-panel moulding around a slate field.</summary>
    private void Area(Control c, Rect2 r)
    {
        if (_aPanel == null) { Sb(c, _sbPanel, r); return; }
        var inner = r.Grow(-4f);
        if (_fArea != null) UiArt.Tile(c, _fArea, inner);
        else c.DrawRect(inner, UiTheme.Panel);
        UiArt.Nine(c, _aPanel, r);
    }

    /// <summary>A tier's row: a quiet inset. Open tiers are sunk one step into the slate, locked ones lie deeper and dimmer.</summary>
    private void Band(Control c, Tier t)
    {
        if (_aWell == null) { Sb(c, t.Open ? _sbBand : _sbBandLocked, t.Band); return; }
        var field = t.Open ? _fCard : _fWell;
        Color mod = t.Open ? ArtIdle : ArtBandLocked;
        var inner = t.Band.Grow(-2f);
        if (field != null) UiArt.Tile(c, field, inner, mod);
        else c.DrawRect(inner, t.Open ? UiTheme.Bg : UiTheme.Inset);
        UiArt.Nine(c, _aWell, t.Band, mod);
    }

    /// <summary>An inset well on the darkest field (the points box).</summary>
    private void Inset(Control c, Rect2 r)
    {
        if (_aWell == null) { Sb(c, _sbCard, r); return; }
        var inner = r.Grow(-2f);
        if (_fWell != null) UiArt.Tile(c, _fWell, inner);
        else c.DrawRect(inner, UiTheme.Inset);
        UiArt.Nine(c, _aWell, r);
    }

    /// <summary>A specialisation card: brighter inlay and a warm veil when chosen, held back while locked.</summary>
    private void Card(Control c, TNode n)
    {
        if (_aCard == null) { Sb(c, n.Owned ? _sbCardSel : n.Locked ? _sbCardLocked : _sbCard, n.Rect); return; }
        var inner = n.Rect.Grow(-3f);
        if (_fCard != null) UiArt.Tile(c, _fCard, inner, n.Locked ? ArtBandLocked : ArtIdle);
        else c.DrawRect(inner, UiTheme.Bg);
        if (n.Owned) c.DrawRect(n.Rect.Grow(-7f), CardVeil);
        UiArt.Nine(c, n.Owned && _aCardOn != null ? _aCardOn : _aCard, n.Rect, n.Locked ? ArtLocked : ArtIdle);
    }

    /// <summary>A cell of the bottom strip (action slot, offered skill).</summary>
    private void Cell(Control c, Rect2 r)
    {
        if (_aCell != null) UiArt.Sprite(c, _aCell, r);
        else Sb(c, _sbSlot, r);
    }

    /// <summary>A tinted line around a cell or card (armed slot, offered skill, something to choose): from the art, or a plain rectangle.</summary>
    private static void Mark(Control c, UiArt.Piece p, Rect2 r, Color col, float width)
    {
        if (p == null) c.DrawRect(r, col, false, width);
        else if (p.Kind == UiArt.Kind.Sprite) UiArt.Sprite(c, p, r, col);
        else UiArt.Nine(c, p, r, col);
    }

    // layer 1: the only one repainted every frame — pulsing halos behind whatever a click can change
    private void DrawGlow(Control c)
    {
        if (_hero == null || _tiers.Count == 0) return;
        var tex = GlowTex();
        float k = _k;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 3.4f);
        if (_assign >= 0)
        {
            foreach (var n in _nodes)
                if (n.Kind == NK.Skill && n.Owned) Halo(c, tex, n.Pos, n.R * (2.0f + 0.3f * pulse), CAssign, 0.28f + 0.3f * pulse);
            // the armed slot and the skills on offer wear the cell's own line in the assignment colour (the art's
            // line lies a little inside its box, hence the wider margin there)
            var slot = _assign < _slots.Length ? _slots[_assign] : null;
            var armed = _aMarkHi ?? _aMark;
            if (slot != null) Mark(c, armed, slot.Rect.Grow((armed != null ? 6f + pulse : 2f + 2f * pulse) * k), Fade(CAssign, 0.55f + 0.4f * pulse), 2.5f * k);
            foreach (var p in _picks) Mark(c, _aMark, p.Rect.Grow((_aMark != null ? 5f : 2f) * k), Fade(CAssign, 0.3f + 0.35f * pulse), 2f * k);
            return;
        }
        foreach (var n in _nodes)
        {
            if (!n.Can) continue;
            if (n.Box) Mark(c, _aLine, n.Rect.Grow((_aLine != null ? 2f + 2f * pulse : 1f + 2f * pulse) * k), Fade(Palette.Ember, 0.35f + 0.45f * pulse), 2f * k);
            else Halo(c, tex, n.Pos, n.R * (2.0f + 0.35f * pulse), n.Kind == NK.Skill && n.Rank == 0 ? Palette.Gold : Palette.Ember, 0.3f + 0.34f * pulse);
        }
        if (_points > 0 && _pointsNode != null)
        {
            var pr = _pointsNode.Rect;
            // behind the figure, whose box grows with the text (_numW)
            Halo(c, tex, new Vector2(pr.Position.X + 20f * k + _numW * 0.36f, pr.Position.Y + pr.Size.Y * 0.5f), Mathf.Max(74f * k, _numW * 0.7f), Palette.Ember, 0.16f + 0.14f * pulse);
        }
    }

    // layer 2: links, nodes and every label; repainted only when the hero or the layout changes
    private void DrawNodes(Control c)
    {
        if (_hero == null)
        {
            var f = UiTheme.Title ?? ThemeDB.FallbackFont;
            TxtC(c, f, Size.X * 0.5f, Mid(f, ZEmpty, Size.Y * 0.5f), "尚無持燼者踏上旅途", ZEmpty, UiTheme.Dim, Size.X);
            return;
        }
        if (_tiers.Count == 0 || _fTitle == null) return;
        DrawLeft(c, _hero);
        DrawTree(c);
        DrawBar(c);
    }

    private void DrawLeft(Control c, Hero h)
    {
        float k = _k;
        float x = _leftR.Position.X + _leftPad, w = _leftR.Size.X - 2f * _leftPad;
        // every baseline and box below was placed by LayoutLeft from the text's own metrics
        Txt(c, _fTitle, x, _titleBase, "技能", ZTitle, UiTheme.Gold);
        TxtR(c, _fUi, x + w, _titleBase, _classText, ZRow, Names.ClassColor(h.Class).Lerp(UiTheme.Text, 0.35f), w - TextWidth(_fTitle, "技能", ZTitle) - 14f * k);
        if (_aRule != null) UiArt.Nine(c, _aRule, new Rect2(x, _ruleY - _aRule.BoxSize.Y * 0.5f, w, _aRule.BoxSize.Y));
        else c.DrawLine(new Vector2(x, _ruleY), new Vector2(x + w, _ruleY), Fade(UiTheme.Border, 0.8f), Mathf.Max(1f, k));

        if (_pointsNode != null)
        {
            var pr = _pointsNode.Rect;
            float mid = pr.Position.Y + pr.Size.Y * 0.5f, nx = pr.Position.X + 20f * k, tx = nx + _numW + 8f * k, tw = pr.End.X - 10f * k - tx;
            Txt(c, _fUi, nx, Mid(_fUi, ZBig, mid), _ptsText, ZBig, _points > 0 ? Palette.Gold : UiTheme.Dim, _numW);
            float nameH = LineH(_fTitle, ZName), top = mid - (nameH + _tight + LineH(_fUi, ZCap)) * 0.5f;
            Txt(c, _fTitle, tx, top + Asc(_fTitle, ZName), "可用技能點數", ZName, _points > 0 ? UiTheme.Text : UiTheme.Dim, tw);
            Txt(c, _fUi, tx, top + nameH + _tight + Asc(_fUi, ZCap), _spentText, ZCap, UiTheme.Dim, tw);
        }

        Txt(c, _fTitle, x, _specHeadY, "職業專精", ZHead, UiTheme.Gold, w);
        Txt(c, _fUi, x, _specHintY, _specHint, ZCap, _specHintCol, w);
        foreach (var n in _nodes) if (n.Kind == NK.Spec) DrawSpec(c, n);

        if (_traitY > 0f)
        {
            // [fixes] 職業特性: gold heading; each bonus bright while the worn gear makes it count, dim otherwise
            Txt(c, _fTitle, x, _traitY + Asc(_fTitle, ZRow), "職業特性", ZRow, Fade(UiTheme.Gold, 0.85f));
            float ty = _traitY + LineH(_fTitle, ZRow) + 2f * k;
            foreach (var (text, on) in _traits)
            {
                string line = TraitLine(text, on);
                Para(c, _fUi, x, ty + Asc(_fUi, ZCap), line, ZCap, on ? UiTheme.Text : UiTheme.Dim, w, 2);
                ty += Math.Clamp(LinesOf(_fUi, line, w, ZCap), 1, 2) * LineH(_fUi, ZCap);
            }
        }

        if (_helpY > 0f && _helpRows > 0)
        {
            Txt(c, _fTitle, x, _helpY + Asc(_fTitle, ZRow), "操作", ZRow, Fade(UiTheme.Gold, 0.85f));
            float ly = _helpY + LineH(_fTitle, ZRow) + 2f * k + _helpGap, asc = Asc(_fUi, ZCap), lh = LineH(_fUi, ZCap);
            for (int i = 0; i < _helpRows; i++)
            {
                if (_aKeycap != null && HelpIsKey[i])
                {
                    // a real key sits on a key cap as wide as its name; the topics beside them stay plain text
                    float kw = _helpTw[i] + 2f * _helpPad;
                    UiArt.Nine(c, _aKeycap, new Rect2(x, ly, kw, lh));
                    TxtC(c, _fUi, x + kw * 0.5f, ly + asc, HelpKeys[i], ZCap, UiTheme.Text, kw);
                }
                else Txt(c, _fUi, x + _helpPad, ly + asc, HelpKeys[i], ZCap, UiTheme.Text);
                Para(c, _fUi, x + _helpKeyW, ly + asc, HelpTexts[i], ZCap, UiTheme.Dim, w - _helpKeyW, 2);
                ly += _helpH[i] + _helpGap;
            }
        }
    }

    private void DrawSpec(Control c, TNode n)
    {
        float k = _k;
        var r = n.Rect;
        // head row (icon, name, state tag) of height _specHead, then the description across the card: see LayoutLeft
        float padX = _cardPadX, padY = _cardPadY, ir = 26f * k, hy = r.Position.Y + padY + _specHead * 0.5f;
        var ic = new Vector2(r.Position.X + padX + ir, hy);
        Color cc = Names.ClassColor(n.Spec.Class);
        var ring = _aRound[n.Owned ? 2 : 0];
        c.DrawCircle(ic, ring != null ? ir * 0.93f : ir, n.Owned ? CNodeFill.Lerp(cc, 0.25f) : CNodeFill, true, -1f, true);
        if (ring != null) UiArt.Sprite(c, ring, Square(ic, ir), n.Locked ? ArtLocked : ArtIdle);
        IconAt(c, n.Icon, ic, ir * 0.66f, n.Owned ? cc.Lerp(Colors.White, 0.4f) : n.Locked ? CIconLocked : CIconIdle);
        if (ring == null) c.DrawArc(ic, ir, 0f, Mathf.Tau, 40, n.Owned ? UiTheme.Gold : n.Locked ? CRimLocked : CRim, (n.Owned ? 2.5f : 1.8f) * k, true);

        float tx = ic.X + ir + 14f * k, right = r.End.X - padX;
        string tag = n.Owned ? "已選擇" : n.Locked ? "未解鎖" : null;
        float tagW = tag == null ? 0f : TextWidth(_fUi, tag, ZCap) + 12f * k;
        Txt(c, _fTitle, tx, Mid(_fTitle, ZName, hy), n.Label, ZName, n.Owned ? UiTheme.Gold : n.Locked ? UiTheme.Dim : UiTheme.Text, right - tagW - tx);
        if (tag != null) TxtR(c, _fUi, right, Mid(_fUi, ZCap, hy), tag, ZCap, n.Owned ? UiTheme.Gold : CLockText, tagW);
        Color dc = n.Locked ? Fade(UiTheme.Dim, 0.8f) : n.Owned ? UiTheme.Text : UiTheme.Text.Lerp(UiTheme.Dim, 0.45f);
        if (n.Lines > 0)
            Para(c, _fBody, r.Position.X + padX, r.Position.Y + padY + _specHead + 4f * k + Asc(_fBody, ZRow), n.Sub, ZRow, dc, r.Size.X - 2f * padX, n.Lines);
    }

    private Color LinkColor(TNode n) => n.Owned ? CLinkLit : n.Locked || n.Tier == null || !n.Tier.Open ? CLinkDim : CLinkOpen;

    private void DrawTree(Control c)
    {
        float k = _k;
        if (ArtOn)
        {
            // the connections are content and stay drawn lines; a dark stroke under them sets them into the forged rows
            float shade = 3f * k;
            for (int i = 0; i + 1 < _tiers.Count; i++)
                c.DrawLine(_tiers[i].Hub.Pos, _tiers[i + 1].Hub.Pos, LinkShade, (_tiers[i + 1].Open ? 5f : 4f) * k + shade, true);
            foreach (var n in _nodes)
                if (n.Link != null) c.DrawPolyline(n.Link, LinkShade, (n.Owned ? 3.5f : 2.5f) * k + shade, true);
        }
        for (int i = 0; i + 1 < _tiers.Count; i++)
        {
            bool lit = _tiers[i + 1].Open;
            c.DrawLine(_tiers[i].Hub.Pos, _tiers[i + 1].Hub.Pos, lit ? CLinkLit : CLinkDim, (lit ? 5f : 4f) * k, true);
        }
        foreach (var t in _tiers)
        {
            // title over its level line, centred on the band; the column is as wide as the widest of them (_tierTextW)
            float x = t.Band.Position.X + 14f * k, cy = t.Band.Position.Y + t.Band.Size.Y * 0.5f;
            Txt(c, _fTitle, x, cy + _dyTier, t.Title, ZName, t.Open ? UiTheme.Gold : UiTheme.Dim, _tierTextW);
            Txt(c, _fUi, x, cy + _dyTierSub, t.Open ? t.LevelText : t.LockText, ZCap, t.Open ? UiTheme.Dim : CLockText, _tierTextW);
        }
        foreach (var n in _nodes)
            if (n.Link != null) c.DrawPolyline(n.Link, LinkColor(n), (n.Owned ? 3.5f : 2.5f) * k, true);
        foreach (var n in _nodes)
        {
            switch (n.Kind)
            {
                case NK.Hub: DrawHub(c, n); break;
                case NK.Skill: DrawSkill(c, n); break;
                case NK.Enh: case NK.UpA: case NK.UpB: DrawUp(c, n); break;
                case NK.Passive: DrawPassive(c, n); break;
                case NK.Key: DrawKey(c, n); break;
            }
        }
    }

    private void DrawHub(Control c, TNode n)
    {
        float k = _k;
        bool open = n.Tier.Open;
        c.DrawColoredPolygon(n.Fill, open ? CHubFill : CNodeFill);
        var frame = open ? _aHubOn : _aHubOff;
        if (frame != null) UiArt.Sprite(c, frame, Square(n.Pos, n.R));
        else c.DrawPolyline(n.Line, open ? UiTheme.Gold : CRimLocked, 2f * k, true);
        // the diamond was sized for its figure in LayoutTree (hubNeed)
        TxtC(c, _fUi, n.Pos.X, Mid(_fUi, ZCap, n.Pos.Y), n.Glyph, ZCap, open ? UiTheme.Gold : UiTheme.Dim, n.R * 4f);
        if (n.Tier.Capstone) TxtC(c, _fUi, n.Pos.X, n.Pos.Y + n.R + 2f * k + Asc(_fUi, ZCap), "擇一", ZCap, UiTheme.Dim, UiTheme.Px(120f) * k);
    }

    private void DrawPips(Control c, float cx, float y, int have, int max)
    {
        float k = _k, step = 11f * k;
        float x = cx - (max - 1) * step * 0.5f;
        for (int i = 0; i < max; i++)
        {
            var p = new Vector2(x + i * step, y);
            if (i < have)
            {
                // a rank: a gold stud
                if (_aPip != null) UiArt.Sprite(c, _aPip, Square(p, 5.4f * k));
                else c.DrawCircle(p, 3.6f * k, UiTheme.Gold, true, -1f, true);
            }
            else c.DrawCircle(p, 3.1f * k, CPipEmpty, false, 1.2f * k, true);
        }
    }

    private void DrawSkill(Control c, TNode n)
    {
        float k = _k;
        var s = n.Skill;
        Color el = Palette.Element(s.Element);
        // dark body with the element's tint, the forged octagon around it (its box is the octagon's bounding square:
        // flat sides at the node's radius), the icon on top
        c.DrawColoredPolygon(n.Fill, n.Owned ? CNodeFill.Lerp(el, 0.2f) : CNodeFill);
        var frame = NodeArt(_aOct, n);
        if (frame != null) UiArt.Sprite(c, frame, Square(n.Pos, n.R * 0.998f), n.Locked && !n.Owned ? ArtLocked : ArtIdle);
        Color ic = n.Owned ? el.Lerp(Colors.White, 0.45f) : n.Locked ? CIconLocked : CIconIdle;
        if (n.Icon != null) IconAt(c, n.Icon, n.Pos, n.R * 0.7f, ic);
        else TxtC(c, _fTitle, n.Pos.X, Mid(_fTitle, ZName, n.Pos.Y), n.Label.Substring(0, 1), ZName, ic, n.R * 4f);
        if (frame == null) c.DrawPolyline(n.Line, n.Owned ? UiTheme.Gold : n.Can ? Palette.Ember : n.Locked ? CRimLocked : CRim, (n.Owned ? 3f : 2f) * k, true);

        DrawPips(c, n.Pos.X, n.Pos.Y + n.R + 9f * k, n.Rank, SkillDef.MaxRank);
        if (n.RankText != null)
        {
            // ranks granted by gear: a badge on the node's upper right corner, its box measured from the figure
            // (caption-size text no longer fits beside the pips without leaving the row)
            float bw = TextWidth(_fUi, n.RankText, ZCap) + 10f * k, bh = LineH(_fUi, ZCap) * 0.8f;
            var br = new Rect2(n.Pos.X + n.R * 0.5f, n.Pos.Y - n.R * 1.08f - bh * 0.3f, bw, bh);
            Sb(c, _sbBadge, br);
            TxtC(c, _fUi, br.Position.X + bw * 0.5f, Mid(_fUi, ZCap, br.Position.Y + bh * 0.5f), n.RankText, ZCap, Palette.Good, bw);
        }

        // the label: name over "category · element [level requirement]", both lines measured and given room by LayoutTree
        Color nameCol = n.Owned ? UiTheme.Text : n.Locked ? Fade(UiTheme.Dim, 0.7f) : UiTheme.Text.Lerp(UiTheme.Dim, 0.55f);
        float yN = n.Pos.Y + _dySkillName, yS = n.Pos.Y + _dySkillSub, lockGap = 12f * k;
        if (n.Side > 0) Txt(c, _fTitle, n.LabelX, yN, n.Label, ZName, nameCol, n.TextW);
        else TxtR(c, _fTitle, n.LabelX, yN, n.Label, ZName, nameCol, n.TextW);
        float total = n.SubW + _sepW + n.SubBW + (n.Locked ? lockGap + n.LockW : 0f);
        if (n.Locked && total > n.TextW)
        {
            // no room for both (only on a canvas much narrower than 1920): the requirement matters more
            if (n.Side > 0) Txt(c, _fUi, n.LabelX, yS, n.Lock, ZCap, CLockText, n.TextW);
            else TxtR(c, _fUi, n.LabelX, yS, n.Lock, ZCap, CLockText, n.TextW);
            return;
        }
        float sx = n.Side > 0 ? n.LabelX : n.LabelX - total;
        float a = n.Locked ? 0.5f : n.Owned ? 1f : 0.78f;
        Txt(c, _fUi, sx, yS, n.Sub, ZCap, Fade(SkillText.CatColor(s.Cat), a)); sx += n.SubW;
        Txt(c, _fUi, sx, yS, Sep, ZCap, Fade(UiTheme.Dim, a)); sx += _sepW;
        Txt(c, _fUi, sx, yS, n.SubB, ZCap, Fade(el, a)); sx += n.SubBW;
        if (n.Locked) Txt(c, _fUi, sx + lockGap, yS, n.Lock, ZCap, CLockText);
    }

    private void DrawUp(Control c, TNode n)
    {
        float k = _k;
        var frame = NodeArt(_aRound, n);
        if (frame != null)
        {
            // the forged ring is a little larger than the circle the layout sized for the glyph: its rim is broad,
            // and the glyph keeps its room inside
            float half = n.R * UpGrow;
            c.DrawCircle(n.Pos, half * 0.93f, n.Owned ? CUpOwned : CNodeFill, true, -1f, true);
            UiArt.Sprite(c, frame, Square(n.Pos, half), n.Locked && !n.Owned ? ArtLocked : ArtIdle);
        }
        else
        {
            c.DrawCircle(n.Pos, n.R, n.Owned ? CUpOwned : CNodeFill, true, -1f, true);
            Color rim = n.Owned ? UiTheme.Gold : n.Can ? Palette.Ember : n.Locked ? CRimLocked : CRim;
            c.DrawArc(n.Pos, n.R, 0f, Mathf.Tau, 32, rim, (n.Owned ? 2.5f : 1.8f) * k, true);
        }
        // the circle was sized for a caption-size glyph in LayoutTree (upR)
        Color gc = n.Owned ? SkillText.NameColor : n.Locked ? CRimLocked.Lightened(0.15f) : CRim.Lightened(0.25f);
        TxtC(c, _fTitle, n.Pos.X, Mid(_fTitle, ZCap, n.Pos.Y), n.Glyph, ZCap, gc, n.R * 4f);
    }

    private void DrawPassive(Control c, TNode n)
    {
        float k = _k;
        var frame = NodeArt(_aRound, n);
        c.DrawCircle(n.Pos, frame != null ? n.R * 0.93f : n.R, n.Owned ? CUpOwned.Darkened(0.25f) : CNodeFill, true, -1f, true);
        if (frame != null) UiArt.Sprite(c, frame, Square(n.Pos, n.R), n.Locked && !n.Owned ? ArtLocked : ArtIdle);
        Color ic = n.Owned ? UiTheme.Gold.Lerp(Colors.White, 0.35f) : n.Locked ? CIconLocked : CIconIdle;
        if (n.Icon != null) IconAt(c, n.Icon, n.Pos, n.R * 0.66f, ic);
        else TxtC(c, _fTitle, n.Pos.X, Mid(_fTitle, ZCap, n.Pos.Y), n.Label.Substring(0, 1), ZCap, ic, n.R * 4f);
        if (frame == null) c.DrawArc(n.Pos, n.R, 0f, Mathf.Tau, 40, n.Owned ? UiTheme.Gold : n.Can ? Palette.Ember : n.Locked ? CRimLocked : CRim, (n.Owned ? 2.5f : 1.8f) * k, true);
        DrawPips(c, n.Pos.X, n.Pos.Y + n.R + (frame != null ? 9f : 8f) * k, n.Rank, n.Max);

        // name over the per-rank line (or the level requirement); TextW is the room LayoutTree found for them
        Color nameCol = n.Owned ? UiTheme.Text : n.Locked ? Fade(UiTheme.Dim, 0.7f) : UiTheme.Text.Lerp(UiTheme.Dim, 0.55f);
        float yN = n.Pos.Y + _dyPassName, yS = n.Pos.Y + _dyPassSub;
        string sub = n.Locked ? n.Lock : n.Sub;
        Color subCol = n.Locked ? CLockText : Fade(UiTheme.Dim, n.Owned ? 1f : 0.85f);
        if (n.Side > 0)
        {
            Txt(c, _fTitle, n.LabelX, yN, n.Label, ZRow, nameCol, n.TextW);
            Txt(c, _fUi, n.LabelX, yS, sub, ZCap, subCol, n.TextW);
        }
        else
        {
            TxtR(c, _fTitle, n.LabelX, yN, n.Label, ZRow, nameCol, n.TextW);
            TxtR(c, _fUi, n.LabelX, yS, sub, ZCap, subCol, n.TextW);
        }
    }

    private void DrawKey(Control c, TNode n)
    {
        float k = _k;
        // the capstone's frame carries a second, studded ring outside the node's own circle
        var frame = NodeArt(_aKey, n);
        c.DrawCircle(n.Pos, frame != null ? n.R * 0.93f : n.R, n.Owned ? CUpOwned : CNodeFill, true, -1f, true);
        if (frame != null) UiArt.Sprite(c, frame, Square(n.Pos, n.R), n.Locked && !n.Owned ? ArtLocked : ArtIdle);
        Color ic = n.Owned ? UiTheme.Gold.Lerp(Colors.White, 0.45f) : n.Locked ? CIconLocked : CIconIdle;
        if (n.Icon != null) IconAt(c, n.Icon, n.Pos, n.R * 0.64f, ic);
        else TxtC(c, _fTitle, n.Pos.X, Mid(_fTitle, ZRow, n.Pos.Y), n.Label.Substring(0, 1), ZRow, ic, n.R * 4f);
        if (frame == null)
        {
            Color rim = n.Owned ? UiTheme.Gold : n.Can ? Palette.Ember : n.Locked ? CRimLocked : CRim;
            c.DrawArc(n.Pos, n.R, 0f, Mathf.Tau, 48, rim, (n.Owned ? 3f : 2f) * k, true);
            c.DrawArc(n.Pos, n.R + 5f * k, 0f, Mathf.Tau, 48, Fade(rim, 0.55f), 1.2f * k, true);
        }

        Color nameCol = n.Owned ? UiTheme.Gold : n.Locked ? Fade(UiTheme.Dim, 0.7f) : UiTheme.Text;
        Color descCol = n.Locked ? Fade(UiTheme.Dim, 0.7f) : n.Owned ? UiTheme.Text : UiTheme.Dim;
        // name line (with the level requirement beside it) over the description, which wraps in the column and on
        // the number of lines LayoutTree measured (TextW, Lines); the block is centred on the node
        float nameH = LineH(_fTitle, ZName), top = n.Pos.Y - (nameH + n.Lines * LineH(_fBody, ZCap)) * 0.5f;
        float yN = top + Asc(_fTitle, ZName), yD = top + nameH + Asc(_fBody, ZCap), lockGap = 12f * k;
        if (n.Side > 0)
        {
            Txt(c, _fTitle, n.LabelX, yN, n.Label, ZName, nameCol);
            if (n.Locked) Txt(c, _fUi, n.LabelX + n.NameW + lockGap, yN, n.Lock, ZCap, CLockText);
            if (n.Lines > 0) Para(c, _fBody, n.LabelX, yD, n.Sub, ZCap, descCol, n.TextW, n.Lines);
        }
        else
        {
            TxtR(c, _fTitle, n.LabelX, yN, n.Label, ZName, nameCol, Mathf.Max(n.TextW, n.NameW + 4f * k));
            if (n.Locked) TxtR(c, _fUi, n.LabelX - n.NameW - lockGap, yN, n.Lock, ZCap, CLockText, n.LockW + 4f * k);
            if (n.Lines > 0) Para(c, _fBody, n.LabelX - n.TextW, yD, n.Sub, ZCap, descCol, n.TextW, n.Lines, HorizontalAlignment.Right);
        }
    }

    private void DrawBar(Control c)
    {
        float k = _k;
        float bx = _barR.Position.X, by = _barR.Position.Y;
        // heading block: baselines from LayoutBar, which also moved the slots past its widest line
        Txt(c, _fTitle, bx + 18f * k, _barHeadY, BarTitle, ZHead, UiTheme.Gold);
        Txt(c, _fUi, bx + 18f * k, _barCapY, BarCaption, ZCap, UiTheme.Dim);
        float capAsc = Asc(_fUi, ZCap);
        for (int i = 0; i < _slots.Length; i++)
        {
            var n = _slots[i];
            if (n == null) continue;
            var r = n.Rect;
            if (n.Skill != null)
            {
                Color ic = Palette.Element(n.Skill.Element).Lerp(Colors.White, 0.4f);
                if (n.Icon != null) c.DrawTextureRect(n.Icon, r.Grow(-8f * k), false, Icons.Tint(n.Icon, ic));
                else TxtC(c, _fTitle, r.Position.X + r.Size.X * 0.5f, Mid(_fTitle, ZName, r.Position.Y + r.Size.Y * 0.5f), n.Skill.Name.Substring(0, 1), ZName, ic, r.Size.X * 2f);
            }
            // the key cap sits right under its slot; the strip's height includes this line (see Relayout)
            Color keyCol = _assign == i ? CAssign : n.Skill != null ? UiTheme.Text : UiTheme.Dim;
            float cx = r.Position.X + r.Size.X * 0.5f;
            if (_aKeycap != null)
            {
                // a forged key: as wide as its name, never narrower than it is high
                float kh = LineH(_fUi, ZCap) - 2f * k, kw = Mathf.Max(n.NameW + 2f * _keyPad, kh);
                var kr = new Rect2(cx - kw * 0.5f, r.End.Y + 3f * k, kw, kh);
                UiArt.Nine(c, _aKeycap, kr, n.Skill != null || _assign == i ? ArtIdle : ArtLocked);
                TxtC(c, _fUi, cx, Mid(_fUi, ZCap, kr.Position.Y + kh * 0.5f), n.Label, ZCap, keyCol, kw);
            }
            else TxtC(c, _fUi, cx, r.End.Y + 1f * k + capAsc, n.Label, ZCap, keyCol, r.Size.X + 16f * k);
        }

        float room = BarRight - _pickX;
        if (room < 120f * k) return;
        if (_assign >= 0)
        {
            // one instruction line, the row of offered skills under it (PickTop)
            Txt(c, _fUi, _pickX, by + BarInset - (ArtOn ? 0f : 2f * k) + Asc(_fUi, ZRow), _assignText, ZRow, CAssign, room);
            if (_picks.Count == 0) Txt(c, _fBody, _pickX, Mid(_fBody, ZRow, (PickTop + _barR.End.Y - BarInset) * 0.5f), "尚無可放入的技能。", ZRow, UiTheme.Dim, room);
            foreach (var p in _picks)
            {
                var r = p.Rect;
                Color ic = Palette.Element(p.Skill.Element).Lerp(Colors.White, 0.4f);
                if (p.Icon != null) c.DrawTextureRect(p.Icon, r.Grow(-7f * k), false, Icons.Tint(p.Icon, ic));
                else TxtC(c, _fTitle, r.Position.X + r.Size.X * 0.5f, Mid(_fTitle, ZName, r.Position.Y + r.Size.Y * 0.5f), p.Skill.Name.Substring(0, 1), ZName, ic, r.Size.X * 2f);
            }
        }
        else
        {
            // two hint lines centred in the strip, at the size LayoutBar found room for
            float lh = LineH(_fBody, _hintSize), top = by + (_barR.Size.Y - 2f * lh) * 0.5f, asc = Asc(_fBody, _hintSize);
            Txt(c, _fBody, _pickX, top + asc, BarHint1, _hintSize, UiTheme.Dim, room);
            Txt(c, _fBody, _pickX, top + lh + asc, BarHint2, _hintSize, Fade(UiTheme.Dim, 0.8f), room);
        }
    }

    // layer 3: hover ring and the short-lived feedback of a click
    private void DrawOver(Control c)
    {
        if (_hero == null || _fTitle == null) return;
        float k = _k;
        var hv = _hover;
        if (hv != null && hv.Kind != NK.Points)
        {
            if (hv.Box)
            {
                // cells light their rim (the art's hover line), cards get a bright bevelled line around the moulding
                bool cell = hv.Kind == NK.Slot || hv.Kind == NK.Pick;
                var piece = cell ? _aCellHover : _aLine;
                if (piece != null) Mark(c, piece, hv.Rect.Grow((cell ? 3f : 2.5f) * k), cell ? Colors.White : CHover, 2f * k);
                else c.DrawRect(hv.Rect.Grow(1.5f * k), CHover, false, 2f * k);
            }
            else
            {
                // the white ring stays: it lies just outside the forged frame of whatever node it is on
                float reach = hv.Kind switch
                {
                    NK.Skill => 1.08f,
                    NK.Key => _aKey[0] != null ? 1.19f : 1f,
                    NK.Enh or NK.UpA or NK.UpB => _aRound[0] != null ? UpGrow : 1f,
                    _ => 1f,
                };
                c.DrawArc(hv.Pos, hv.R * reach + 4f * k, 0f, Mathf.Tau, 48, CHover, 2f * k, true);
            }
        }
        if (_pops.Count == 0) return;
        var tex = GlowTex();
        for (int i = 0; i < _pops.Count; i++)
        {
            var p = _pops[i];
            float t = Mathf.Clamp(p.T / p.Dur, 0f, 1f);
            if (p.Text == null)
            {
                float e = 1f - (1f - t) * (1f - t);
                Halo(c, tex, p.Pos, p.R * 2.2f + e * 30f * k, p.C, (1f - t) * 0.5f);
                c.DrawArc(p.Pos, p.R + e * 34f * k, 0f, Mathf.Tau, 48, Fade(p.C, (1f - t) * 0.9f), (3.5f - 2.5f * t) * k, true);
            }
            else
            {
                float a = t < 0.7f ? 1f : (1f - t) / 0.3f;
                // the line's box follows its text, stays inside the panel and never rises out of its top edge
                float w = Mathf.Max(440f * k, TextWidth(_fTitle, p.Text, ZName) + 24f * k);
                float x = Mathf.Clamp(p.Pos.X - w * 0.5f, 6f * k, Mathf.Max(6f * k, Size.X - w - 6f * k));
                float baseline = Mathf.Max(p.Pos.Y - p.R - 12f * k - t * 26f * k, Asc(_fTitle, ZName) + 6f * k);
                TxtLit(c, _fTitle, x + w * 0.5f, baseline, p.Text, ZName, Fade(p.C, a), w, new Color(0f, 0f, 0f, 0.85f * a));
            }
        }
    }
}
