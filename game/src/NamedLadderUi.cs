using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// Panel "named_ladder": the altar of the Ember Stair (燼階) in town. Eight rungs, each an ember-memory of a boss; a
/// rung opens when the one below fell at the current world tier or higher. One framed row per rung: the rung and the
/// boss, one line on what the fight asks of the hero, the best tier it fell at and the way in. Built from the shared
/// kit (WorldUiKit / UiTheme), registered on first use so Ui.cs stays untouched. The window grows with the text size
/// (標準 / 大 / 特大) up to what a 1080p screen holds; the rung list scrolls when it no longer fits.
/// </summary>
public partial class NamedLadderUi : UiPanel
{
    public const string PanelId = "named_ladder";
    public override string Id => PanelId;

    private const float FrameW = 1240f, FrameH = 1000f, MaxFrameW = 1780f, MaxFrameH = 1020f;      // all eight rungs fit at 標準; 大 / 特大 scroll the list
    private static readonly string Digits = "一二三四五六七八";

    /// <summary>What each rung asks of the hero, in one line (the full design is in BESTIARY.md 4.6).</summary>
    private static readonly string[] RungHint =
    {
        "墓坑留到戰鬥結束；被活埋時連續攻擊八次脫身。",
        "遠離火盆會凍僵；他會跳向火盆，把火凍熄。",
        "她的命藏在三只骨灰甕裡——先擊倒守甕人，再打碎甕。",
        "漲潮時站上根島；退潮時她承受雙倍傷害。",
        "光中的揮擊會有延遲的鏡影；鏡衛會反彈投射物。",
        "休止小節別施放技能；合唱團在場時他受到的傷害較少。",
        "她用你的職業招式；爭奪中央火盆的光。",
        "黑暗中光圈逐漸縮小；熄滅將臨時走進移動的火圈。",
    };

    private bool _built;
    private PanelContainer _frame;
    private VBoxContainer _list;
    private Label _why, _tier;

    public static void Show()
    {
        var ui = Game.I?.Ui;
        if (ui == null) return;
        if (ui.Find(PanelId) == null) ui.Register(new NamedLadderUi());
        ui.Open(PanelId);
    }

    public override void _Ready()
    {
        base._Ready();
        Log.Guard("named", Build);
    }

    private Vector2 FrameSize => new(MathF.Min(MaxFrameW, UiTheme.Px(FrameW)), MathF.Min(MaxFrameH, UiTheme.Px(FrameH)));
    private float TextW => FrameSize.X - 100f;

    private void Build()
    {
        if (_built) return;
        _built = true;
        MouseFilter = MouseFilterEnum.Ignore;
        WorldUiKit.FullRect(this);
        _frame = WorldUiKit.Shell(this, FrameSize, "燼階", () => WorldUiKit.ClosePanel(Id), out var header, out var body, 0f);
        _tier = UiTheme.Label("", UiTheme.FsRow, UiTheme.Gold);              // the world tier sits in the title row (the list keeps the room for all eight rungs)
        _tier.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(_tier);
        header.MoveChild(_tier, header.GetChildCount() - 2);
        body.AddChild(WorldUiKit.Wrapped("深淵記得每一個倒下的首領。八段燼憶依序排成階梯——擊敗一階，下一階才會在此世界階級回應你。", UiTheme.FsBody, UiTheme.Text, TextW));
        _why = WorldUiKit.Wrapped("", UiTheme.FsBody, Palette.Bad, TextW);
        body.AddChild(_why);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        _list = WorldUiKit.VBox(5);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);
    }

    public override void OnOpen(object arg)
    {
        Build();
        Refresh();
    }

    /// <summary>The text size changed: the window takes its new size and the rows are built again at the new metrics.</summary>
    public override void OnTextScale()
    {
        if (!_built) return;
        Log.Guard("named", () =>
        {
            if (_frame != null && IsInstanceValid(_frame)) WorldUiKit.Center(_frame, FrameSize);
            Refresh();
        });
    }

    public override void Refresh()
    {
        if (_list == null) return;
        WorldUiKit.ClearChildren(_list);
        bool open = NamedAreas.LadderOpen(out string why);
        int tier = Game.I?.WorldTier ?? 0;
        _why.Text = open ? "" : why;
        _why.Visible = !open;
        _tier.Text = open ? $"世界階級：{ZoneDb.TierNames[tier]}（攻克依此階級記錄）　" : "";
        _why.AddThemeColorOverride("font_color", open ? UiTheme.Gold : Palette.Bad);
        for (int rung = 1; rung <= 8; rung++)
        {
            var d = NamedDb.RungOf(rung);
            if (d == null) continue;
            int best = NamedRecords.BestTier(d.Id);
            bool reached = NamedAreas.RungOpen(rung);
            bool can = open && reached;

            var well = UiTheme.Well();
            well.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var pad = WorldUiKit.Margin(14, 3, 14, 3);
            well.AddChild(pad);
            var row = WorldUiKit.HBox(18);
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            pad.AddChild(row);

            // left: the rung, the boss, what the fight asks
            var col = WorldUiKit.VBox(0);
            col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            col.AddChild(UiTheme.Label($"第{Digits[rung - 1]}階　{d.NameZh}", UiTheme.FsName, can ? UiTheme.Gold : UiTheme.Dim));
            col.AddChild(UiTheme.Small(RungHint[rung - 1], UiTheme.FsCaption, can ? UiTheme.Text : UiTheme.Dim));
            row.AddChild(col);

            // right: the record and the way in
            string rec = best >= 0 ? $"最高攻克：{ZoneDb.TierNames[best]}" : reached ? "未攻克" : $"先攻克第{Digits[rung - 2]}階";
            var recL = UiTheme.Label(rec, UiTheme.FsRow, best >= 0 ? UiTheme.Text : UiTheme.Dim);
            recL.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(recL);
            int r = rung;
            var b = UiTheme.MinSize(UiTheme.Button(can ? "挑戰" : "封閉", () => Enter(r), UiTheme.FsBody), 150f, 46f);
            b.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            b.Disabled = !can;
            row.AddChild(b);
            _list.AddChild(well);
        }
    }

    private void Enter(int rung)
    {
        if (!NamedAreas.LadderOpen(out _) || !NamedAreas.RungOpen(rung)) return;
        WorldUiKit.ClosePanel(Id);
        Log.Info("named", $"ladder: rung {rung} chosen at the altar (tier {Game.I?.WorldTier ?? 0})");
        Game.I?.EnterArea(NamedAreas.RungArea(rung));
    }
}
