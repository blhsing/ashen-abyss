using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// Panel "named_ladder": the altar of the Ember Stair (燼階) in town. Eight rungs, each an ember-memory of a boss; a
/// rung opens when the one below fell at the current world tier or higher. Minimal on purpose (built from the shared
/// kit: WorldUiKit / UiTheme), registered on first use so Ui.cs stays untouched.
/// </summary>
public partial class NamedLadderUi : UiPanel
{
    public const string PanelId = "named_ladder";
    public override string Id => PanelId;

    private bool _built;
    private VBoxContainer _list;
    private Label _why;

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

    private void Build()
    {
        if (_built) return;
        _built = true;
        MouseFilter = MouseFilterEnum.Ignore;
        WorldUiKit.FullRect(this);
        WorldUiKit.Shell(this, new Vector2(1180f, 820f), "燼階", () => WorldUiKit.ClosePanel(Id), out _, out var body, 0f);
        body.AddChild(WorldUiKit.Wrapped("深淵記得每一個倒下的首領。八段燼憶依序排成階梯——擊敗一階，下一階才會在此世界階級回應你。", UiTheme.FsBody, UiTheme.Text, 1080f));
        _why = WorldUiKit.Wrapped("", UiTheme.FsBody, Palette.Bad, 1080f);
        body.AddChild(_why);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        _list = WorldUiKit.VBox(10);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);
    }

    public override void OnOpen(object arg)
    {
        Build();
        Refresh();
    }

    public override void Refresh()
    {
        if (_list == null) return;
        WorldUiKit.ClearChildren(_list);
        bool open = NamedAreas.LadderOpen(out string why);
        _why.Text = open ? $"目前世界階級：{ZoneDb.TierNames[Game.I?.WorldTier ?? 0]}" : why;
        _why.AddThemeColorOverride("font_color", open ? UiTheme.Gold : Palette.Bad);
        for (int rung = 1; rung <= 8; rung++)
        {
            var d = NamedDb.RungOf(rung);
            if (d == null) continue;
            var row = WorldUiKit.HBox(16);
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            int best = NamedRecords.BestTier(d.Id);
            bool can = open && NamedAreas.RungOpen(rung);
            var name = UiTheme.Label($"第{"一二三四五六七八"[rung - 1]}階　{d.NameZh}", UiTheme.FsName, can ? UiTheme.Gold : UiTheme.Dim);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(name);
            row.AddChild(UiTheme.Label(best < 0 ? "未攻克" : $"最高攻克：{ZoneDb.TierNames[best]}", UiTheme.FsRow, best < 0 ? UiTheme.Dim : UiTheme.Text));
            int r = rung;
            var b = UiTheme.MinSize(UiTheme.Button(can ? "挑戰" : "封閉", () => Enter(r), UiTheme.FsBody), 180f, 52f);
            b.Disabled = !can;
            row.AddChild(b);
            _list.AddChild(row);
        }
    }

    private void Enter(int rung)
    {
        if (!NamedAreas.LadderOpen(out _) || !NamedAreas.RungOpen(rung)) return;
        WorldUiKit.ClosePanel(Id);
        Game.I?.EnterArea(NamedAreas.RungArea(rung));
    }
}
