using Godot;
using System;
using System.Text;

namespace AshenAbyss;

/// <summary>
/// Panel "tier": the four world tiers with what each one changes (monster life, damage and level, experience, gold),
/// which of them the hero has unlocked and what opens the rest. A tier can only be chosen in 殘燈鎮, at the tier statue.
/// </summary>
public partial class WorldTierUi : UiPanel
{
    public override string Id => "tier";

    private static readonly Color[] TierColors =
    {
        new(0.78f, 0.82f, 0.74f), new(0.96f, 0.80f, 0.42f), new(0.76f, 0.52f, 1f), new(1f, 0.36f, 0.14f),
    };

    // Design pixels at text scale 1. The window grows with the text scale, as far as a 1920×1080 screen allows; the
    // four cards share its width. CardText is the least width of a card's text column (it scales with the text),
    // DescH the height kept for the description so that the figures start level on every card (three lines).
    private const float FrameW = 1680f, FrameH = 900f, MaxFrameW = 1840f, MaxFrameH = 1044f;
    private const float CardText = 280f, DescH = 104f;

    private bool _built, _open;
    private PanelContainer _frame;
    private HBoxContainer _cards;
    private Label _note;

    public override void _Ready()
    {
        base._Ready();
        Log.Guard("tier", EnsureBuilt);
    }

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        MouseFilter = MouseFilterEnum.Ignore;        // the panel manager lays the dimmed backdrop underneath
        WorldUiKit.FullRect(this);

        _frame = WorldUiKit.Shell(this, new Vector2(FrameW, FrameH), "世界階級", () => WorldUiKit.ClosePanel(Id), out _, out var body, 0f);
        FitFrame();
        body.AddChild(WorldUiKit.Wrapped(
            "世界階級決定灰暮有多深。階級越高，魔物越強悍、等級越高，倒下時留下的經驗、金幣與寶物也越豐厚。",
            UiTheme.FsBody, UiTheme.Text, 1400));
        _cards = WorldUiKit.HBox(16);
        _cards.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(_cards);
        _note = WorldUiKit.Wrapped("", UiTheme.FsRow, UiTheme.Dim, 1400);
        _note.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(_note);
    }

    /// <summary>The window grows with the text scale, up to what a 1920×1080 screen holds; the cards scroll inside it if they must.</summary>
    private void FitFrame()
    {
        if (_frame == null) return;
        _frame.CustomMinimumSize = new Vector2(MathF.Min(MaxFrameW, UiTheme.Px(FrameW)), MathF.Min(MaxFrameH, UiTheme.Px(FrameH)));
    }

    /// <summary>The text size changed: the window takes its new size (the cards are rebuilt by Refresh).</summary>
    public override void OnTextScale()
    {
        if (_built) Log.Guard("tier", FitFrame);
    }

    public override void OnOpen(object arg)
    {
        EnsureBuilt();
        WorldUiKit.FullRect(this);
        _open = true;
        Refresh();
        var d = Game.I?.Hero?.D;
        Log.Info("tier", $"opened tier={d?.Tier ?? 0} max={d?.MaxTier ?? 0} town={InTown()}");
    }

    public override void OnClose() => _open = false;

    private static bool InTown()
    {
        var g = Game.I;
        var lv = g?.Level;
        return lv != null ? lv.IsTown : (g?.Hero?.D.Area ?? "") == ZoneDb.Town;
    }

    private static int TierCount => Math.Min(ZoneDb.TierNames.Length, Math.Min(Balance.TierHp.Length, Balance.TierLevelBonus.Length));

    /// <summary>Why a tier is still closed. The rules live in the story module: the campaign opens 夢魘, a deep rift opens 煉獄.</summary>
    private static string LockReason(int tier, SaveData d)
    {
        bool campaign = false;
        try { campaign = Quests.CampaignDone; }
        catch (Exception e) { Log.Warn("tier", "campaign state unavailable: " + e.Message); }
        switch (tier)
        {
            case 2:
                return "終結寂主・奧薩洛斯，走完主線之後開啟。";
            case 3:
                return campaign
                    ? $"通關深淵裂隙第 {Quests.RiftForTopTier} 層之後開啟。\n你的最高紀錄：{(d.RiftBest > 0 ? $"第 {d.RiftBest} 層" : "尚無")}"
                    : $"走完主線、再通關深淵裂隙第 {Quests.RiftForTopTier} 層之後開啟。";
            default:
                return "踏上旅途之後開啟。";
        }
    }

    public override void Refresh()
    {
        if (!_built || !_open) return;
        Log.Guard("tier", Rebuild);
    }

    private void Rebuild()
    {
        WorldUiKit.ClearChildren(_cards);
        var d = Game.I?.Hero?.D;
        if (d == null)
        {
            _note.Text = "沒有進行中的旅程。";
            return;
        }
        int count = TierCount;
        int cur = Math.Clamp(d.Tier, 0, count - 1), max = Math.Clamp(d.MaxTier, 0, count - 1);
        bool town = InTown();
        for (int i = 0; i < count; i++) _cards.AddChild(Card(i, d, cur, max, town));
        _note.Text = town
            ? "在此選定的階級，會自你下一次踏出殘燈鎮起生效。"
            : "世界階級只能在殘燈鎮的階位石像前更改。";
        _note.AddThemeColorOverride("font_color", town ? UiTheme.Dim : Palette.Bad);
    }

    private Control Card(int tier, SaveData d, int cur, int max, bool town)
    {
        bool current = tier == cur, unlocked = tier <= max;
        Color tc = TierColors[Math.Min(tier, TierColors.Length - 1)];
        if (!unlocked) tc = tc.Lerp(new Color(0.4f, 0.4f, 0.4f), 0.6f);

        // the card: the forged card frame, with the gold inlay and a warm floor for the tier in force and held back
        // for a tier that is still closed (the frame's own padding is 18 × 16)
        var card = new PanelContainer();
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        card.SizeFlagsVertical = SizeFlags.ExpandFill;
        card.AddThemeStyleboxOverride("panel", WorldUiKit.CardBox(current ? 1 : unlocked ? 0 : 2));

        var margin = WorldUiKit.Margin(2, 0, 2, 2);
        card.AddChild(margin);
        var col = WorldUiKit.VBox(8);
        margin.AddChild(col);

        // Design sizes: numeral 48, tier name 36, description and figures 22, the state tag and the reason a tier is
        // closed 20. The numeral stands beside the name: one heading row instead of two leaves the card's height to
        // its text.
        var head = WorldUiKit.HBox(14);
        head.Alignment = BoxContainer.AlignmentMode.Center;
        col.AddChild(head);
        var numeral = UiTheme.Label(WorldUiKit.Roman[Math.Min(tier, WorldUiKit.Roman.Length - 1)], 48, tc, true);
        numeral.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(numeral);
        var name = UiTheme.Label(ZoneDb.TierNames[tier], 36, unlocked ? UiTheme.Text : UiTheme.Dim, true);
        name.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(name);
        var state = UiTheme.Label(current ? "◆ 目前的階級 ◆" : unlocked ? "已開啟" : "尚未開啟", UiTheme.FsCaption, current ? UiTheme.Gold : unlocked ? Palette.Good : Palette.Bad);
        state.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(state);
        col.AddChild(WorldUiKit.Rule(0.5f));

        // The description and the figures take the room between the heading and the button. They scroll inside the
        // card should the largest text size leave them too little of it; the reason a tier is closed and the button
        // stay in view underneath.
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        col.AddChild(scroll);
        var text = WorldUiKit.VBox(8);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(text);

        string desc = tier < ZoneDb.TierDesc.Length ? ZoneDb.TierDesc[tier] : "";
        var descLabel = UiTheme.MinSize(WorldUiKit.Wrapped(desc, UiTheme.FsRow, unlocked ? UiTheme.Text : UiTheme.Dim, CardText), CardText, DescH);
        descLabel.VerticalAlignment = VerticalAlignment.Top;
        text.AddChild(descLabel);
        text.AddChild(WorldUiKit.Rich(Modifiers(tier, d), CardText, UiTheme.FsRow));

        if (!unlocked)
        {
            var why = WorldUiKit.Wrapped(LockReason(tier, d), UiTheme.FsCaption, Palette.Bad, CardText);
            why.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(why);
        }
        int pick = tier;
        var btn = UiTheme.Button(current ? "目前的階級" : unlocked ? "選擇此階級" : "尚未開啟", () => Select(pick), UiTheme.FsRow);
        btn.Disabled = current || !unlocked || !town;
        if (unlocked && !current && !town) btn.TooltipText = "只能在殘燈鎮更改";
        col.AddChild(btn);
        return card;
    }

    private static string Mult(float v) => v <= 1.0001f ? "基準" : $"×{v:0.##}";
    private static string Plus(float v) => v <= 1.0001f ? "基準" : $"+{(v - 1f) * 100f:0}%";

    private static string Modifiers(int tier, SaveData d)
    {
        string danger = WorldUiKit.Hex(new Color(1f, 0.55f, 0.42f)), good = WorldUiKit.Hex(Palette.Good), dim = WorldUiKit.Hex(UiTheme.Dim);
        int bonus = Balance.TierLevelBonus[tier];
        var sb = new StringBuilder(320);
        sb.Append($"[color=#{dim}]魔物生命[/color]　[color=#{danger}]{Mult(Balance.TierHp[tier])}[/color]\n");
        sb.Append($"[color=#{dim}]魔物傷害[/color]　[color=#{danger}]{Mult(Balance.TierDmg[tier])}[/color]\n");
        sb.Append($"[color=#{dim}]魔物等級[/color]　[color=#{danger}]{(bonus > 0 ? "+" + bonus : "基準")}[/color]\n");
        sb.Append($"[color=#{dim}]經驗值　[/color]　[color=#{good}]{Plus(Balance.TierXp[tier])}[/color]\n");
        sb.Append($"[color=#{dim}]金幣　　[/color]　[color=#{good}]{Plus(Balance.TierGold[tier])}[/color]\n");
        // what that means for this hero: monsters match the hero's level, plus the tier's bonus
        int low = Balance.AreaLevel(1, d.Level, tier, false);
        sb.Append($"[color=#{dim}]野外魔物[/color]　[color=#{danger}]至少 {low} 級[/color]");
        return sb.ToString();
    }

    private void Select(int tier)
    {
        Log.Guard("tier", () =>
        {
            var g = Game.I;
            var d = g?.Hero?.D;
            if (d == null) return;
            int count = TierCount;
            if (tier == d.Tier) return;
            if (tier < 0 || tier >= count || tier > d.MaxTier)
            {
                WorldUiKit.Sfx("ui_error");
                Log.Warn("tier", $"refused tier {tier}: max is {d.MaxTier}");
                return;
            }
            if (!InTown())
            {
                WorldUiKit.Sfx("ui_error");
                g.Hud?.Warn("世界階級只能在殘燈鎮更改。");
                return;
            }
            int old = d.Tier;
            d.Tier = tier;
            Log.Info("tier", $"world tier {old} -> {tier} ({ZoneDb.TierNames[tier]})");
            WorldUiKit.Sfx("world_tier");
            g.Hud?.Toast($"世界階級：{ZoneDb.TierNames[tier]}", TierColors[Math.Min(tier, TierColors.Length - 1)]);
            g.SaveNow("world tier");
            Refresh();
        });
    }
}
