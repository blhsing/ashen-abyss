using Godot;
using System;
using System.Text;

namespace AshenAbyss;

/// <summary>
/// Panel "rift": the 裂隙石 in town. Explains the endless 深淵裂隙, shows the deepest tier cleared, lets the hero pick a
/// tier from 1 to that record plus one (at most <see cref="MaxTier"/>) with what the choice means for the monsters, and
/// enters it with <c>Game.I.EnterArea("rift:&lt;tier&gt;")</c>. Closed until the campaign is finished.
/// </summary>
public partial class RiftUi : UiPanel
{
    public override string Id => "rift";

    public const int MaxTier = 100;
    /// <summary>Monster life and damage grow by this factor per tier; the level applies the same curve (Level.RiftMul = 1.12^tier).</summary>
    public const float Growth = 1.12f;

    /// <summary>The rift's own multiplier on monster life and damage at a tier.</summary>
    public static float RiftScale(int tier) => MathF.Pow(Growth, Math.Max(1, tier));

    // Design pixels at text scale 1. The window and its two columns grow with the text scale, the window only as far as
    // a 1920×1080 screen allows: the rules column is RulesW wide, the chooser at least ChooserW and takes the rest.
    private const float FrameW = 1440f, FrameH = 840f, MaxFrameW = 1840f, MaxFrameH = 1000f;
    private const float RulesW = 476f, ChooserW = 840f;

    private bool _built, _open, _syncing, _picked;
    private int _tier = 1, _max = 1;
    private PanelContainer _frame;
    private Control _lockBox, _chooser;
    private VBoxContainer _rulesCol;
    private Label _lockWhy, _best, _tierLabel, _tierNote;
    private RichTextLabel _scaling;
    private HSlider _slider;
    private Button _m10, _m1, _p1, _p10, _enter;

    public override void _Ready()
    {
        base._Ready();
        Log.Guard("rift", EnsureBuilt);
    }

    // ───────────────────────── construction ─────────────────────────

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        MouseFilter = MouseFilterEnum.Ignore;        // the panel manager lays the dimmed backdrop underneath
        WorldUiKit.FullRect(this);

        _frame = WorldUiKit.Shell(this, new Vector2(FrameW, FrameH), "深淵裂隙", () => WorldUiKit.ClosePanel(Id), out _, out var body, 0f);
        FitFrame();

        // Two columns: what the rift is on the left, the choice of a tier on the right. One column under the other
        // no longer fits a 1080-line screen at readable sizes; side by side they do, at every text scale. The rules
        // and the figures each sit in a scroller, so text that outgrows the window scrolls instead of pushing the
        // window (and the button that enters the rift) off the screen.
        var cols = WorldUiKit.HBox(16);
        cols.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(cols);

        // ── left: the rules, in a sub-panel ──
        var rulesPane = UiTheme.Frame(false) ?? new PanelContainer();
        rulesPane.SizeFlagsVertical = SizeFlags.ExpandFill;
        cols.AddChild(rulesPane);
        var rulesScroll = UiTheme.MinSize(new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, RulesW, 0f);
        rulesPane.AddChild(rulesScroll);
        _rulesCol = WorldUiKit.VBox(12);
        _rulesCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rulesScroll.AddChild(_rulesCol);
        BuildRules();

        // ── right: closed, or the chooser ──
        var right = UiTheme.MinSize(WorldUiKit.VBox(0), ChooserW, 0f);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        right.SizeFlagsVertical = SizeFlags.ExpandFill;
        cols.AddChild(right);

        // ── closed: the stone's answer on a card that is held back like a closed tier ──
        var lockBox = WorldUiKit.VBox(14);
        lockBox.SizeFlagsVertical = SizeFlags.ExpandFill;
        lockBox.Alignment = BoxContainer.AlignmentMode.Center;
        right.AddChild(lockBox);
        _lockBox = lockBox;
        var seal = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        seal.AddThemeStyleboxOverride("panel", WorldUiKit.CardBox(2));
        lockBox.AddChild(seal);
        var sealMargin = WorldUiKit.Margin(26, 18, 26, 22);
        seal.AddChild(sealMargin);
        var sealCol = WorldUiKit.VBox(10);
        sealMargin.AddChild(sealCol);
        var lockTitle = WorldUiKit.Wrapped("裂隙石冰冷而沉默", 36, UiTheme.Dim, ChooserW - 240f, true);
        lockTitle.HorizontalAlignment = HorizontalAlignment.Center;
        sealCol.AddChild(lockTitle);
        var gem = UiTheme.Art && UiArt.Has("rule_gem") ? UiArt.Icon("rule_gem") : null;
        if (gem != null)
            sealCol.AddChild(new TextureRect
            {
                Texture = gem, StretchMode = TextureRect.StretchModeEnum.KeepCentered, MouseFilter = MouseFilterEnum.Ignore,
                Modulate = new Color(0.8f, 0.78f, 0.78f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            });
        _lockWhy = WorldUiKit.Wrapped("", UiTheme.FsBody, Palette.Bad, ChooserW - 240f);
        _lockWhy.HorizontalAlignment = HorizontalAlignment.Center;
        sealCol.AddChild(_lockWhy);

        // ── open ──
        var chooser = WorldUiKit.VBox(14);
        chooser.SizeFlagsVertical = SizeFlags.ExpandFill;
        right.AddChild(chooser);
        _chooser = chooser;

        _best = UiTheme.Label("", UiTheme.FsBody, UiTheme.Gold);
        _best.HorizontalAlignment = HorizontalAlignment.Center;
        chooser.AddChild(_best);

        // the tier selector: the chosen tier reads in an inset well between the four step buttons
        var row = WorldUiKit.HBox(14);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        chooser.AddChild(row);
        _m10 = StepButton("－10", -10); row.AddChild(_m10);
        _m1 = StepButton("－1", -1); row.AddChild(_m1);
        _tierLabel = UiTheme.MinSize(UiTheme.Label("", 52, UiTheme.Gold, true), 320f, 0f);
        _tierLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _tierLabel.VerticalAlignment = VerticalAlignment.Center;
        var readout = new PanelContainer();
        readout.AddThemeStyleboxOverride("panel", UiTheme.Art ? UiTheme.ArtBox("well", 12f, 2f, "well", 2f) : UiTheme.WellBox);
        readout.AddChild(_tierLabel);
        row.AddChild(readout);
        _p1 = StepButton("＋1", 1); row.AddChild(_p1);
        _p10 = StepButton("＋10", 10); row.AddChild(_p10);

        _slider = new HSlider { MinValue = 1, MaxValue = 2, Step = 1, Value = 1, CustomMinimumSize = new Vector2(760, 30), FocusMode = FocusModeEnum.None };
        _slider.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _slider.ValueChanged += OnSlider;
        chooser.AddChild(_slider);

        _tierNote = WorldUiKit.Wrapped("", UiTheme.FsRow, UiTheme.Dim, ChooserW - 80f);
        _tierNote.HorizontalAlignment = HorizontalAlignment.Center;
        chooser.AddChild(_tierNote);

        // What the tier means for the monsters (running text, 24). The card is as wide as the column and as tall as
        // its text; its scroller takes the room down to the button, so a card too tall for it scrolls. The card's
        // moulding casts a shadow: it stands in a little room of its own, so the scroller does not cut that off.
        var scalingScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        chooser.AddChild(scalingScroll);
        var cardRoom = WorldUiKit.Margin(8, 6, 8, 10);
        cardRoom.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scalingScroll.AddChild(cardRoom);
        var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", WorldUiKit.CardBox(0));
        cardRoom.AddChild(card);
        _scaling = WorldUiKit.Rich("", ChooserW - 110f);
        card.AddChild(_scaling);

        _enter = UiTheme.MinSize(UiTheme.Button("踏入裂隙", Enter, UiTheme.FsName), 360f, 64f);
        _enter.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        chooser.AddChild(_enter);
    }

    /// <summary>The window grows with the text scale, up to what a 1920×1080 screen holds; its columns scroll inside it if they must.</summary>
    private void FitFrame()
    {
        if (_frame == null) return;
        _frame.CustomMinimumSize = new Vector2(MathF.Min(MaxFrameW, UiTheme.Px(FrameW)), MathF.Min(MaxFrameH, UiTheme.Px(FrameH)));
    }

    /// <summary>
    /// The text size changed: the window takes its new size and the rules are laid out again (their studs are sized
    /// from the text); everything else follows through its containers.
    /// </summary>
    public override void OnTextScale()
    {
        if (_built) Log.Guard("rift", () => { FitFrame(); BuildRules(); });
    }

    /// <summary>The left column: what the rift is, then its rules, one block each under a gold stud.</summary>
    private void BuildRules()
    {
        if (_rulesCol == null) return;
        WorldUiKit.ClearChildren(_rulesCol);
        _rulesCol.AddChild(WorldUiKit.Wrapped(
            "日爐之下，寂靜仍自裂縫滲出。裂隙沒有盡頭，也從不重複同一副模樣——每深入一層，其中的魔物便更強一分。",
            UiTheme.FsBody, UiTheme.Text, RulesW - 40f));
        _rulesCol.AddChild(WorldUiKit.Rule(0.5f));

        int minutes = (int)MathF.Round(Level.RiftTime / 60f);
        // one block per rule (running text, 24): each wraps by itself in the column and keeps a gap to the next
        string[] rules =
        {
            $"踏入之後有 {minutes} 分鐘。擊殺魔物累積燼火，引出裂隙守衛並將其擊敗，即為通關。",
            $"每次通關可得 {GlyphDb.TriesPerClear} 次燼符淬鍊機會；裂隙越深，淬鍊高階燼符越有把握。",
            $"通關一層，才能挑戰下一層，最深可達第 {MaxTier} 層。",
            $"通關第 {Quests.RiftForTopTier} 層，世界階級「{WorldUiKit.TierName(3)}」隨之開啟。",
            "時限一到，裂隙便開始崩解：此後即使擊敗守衛也不算通關，戰利品所剩無幾。",
        };
        bool stud = UiTheme.Art && UiArt.Has("bullet_gold");
        string gold = WorldUiKit.Hex(UiTheme.Gold);
        foreach (string rule in rules)
        {
            if (!stud)
            {
                _rulesCol.AddChild(WorldUiKit.Rich($"[color=#{gold}]◆[/color] {rule}", RulesW - 40f));
                continue;
            }
            var line = WorldUiKit.HBox(6);
            line.AddChild(WorldUiKit.Mark("bullet_gold", "◆", UiTheme.Gold, UiTheme.FsBody, 26f, 1.3f));
            var text = WorldUiKit.Rich(rule, RulesW - 76f);
            text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            line.AddChild(text);
            _rulesCol.AddChild(line);
        }
    }

    private Button StepButton(string text, int delta)
    {
        var b = UiTheme.MinSize(UiTheme.Button(text, () => SetTier(_tier + delta), UiTheme.FsBody), 92f, 56f);
        b.FocusMode = FocusModeEnum.None;
        b.SizeFlagsVertical = SizeFlags.ShrinkCenter;       // beside the tall readout the buttons keep their own height
        return b;
    }

    // ───────────────────────── panel life cycle ─────────────────────────

    public override void OnOpen(object arg)
    {
        EnsureBuilt();
        WorldUiKit.FullRect(this);
        _open = true;
        var d = Game.I?.Hero?.D;
        int max = MaxAllowed(d);
        if (arg is int want && want >= 1) { _tier = want; _picked = true; }
        else if (!_picked || _tier > max) _tier = max;          // by default, the next tier to conquer
        Refresh();
        Log.Info("rift", $"opened best={d?.RiftBest ?? 0} max={max} tier={_tier} open={Unlocked()}");
    }

    public override void OnClose() => _open = false;

    private static bool Unlocked() => Game.I?.Hero != null && WorldUiKit.CanEnter("rift:1");

    private static int MaxAllowed(SaveData d) => d == null ? 1 : Math.Clamp(d.RiftBest + 1, 1, MaxTier);

    public override void Refresh()
    {
        if (!_built || !_open) return;
        Log.Guard("rift", () =>
        {
            var d = Game.I?.Hero?.D;
            bool open = d != null && Unlocked();
            _lockBox.Visible = !open;
            _chooser.Visible = open;
            if (!open)
            {
                string why = d == null ? "沒有進行中的旅程" : WorldUiKit.WhyLocked("rift:1");
                _lockWhy.Text = (string.IsNullOrEmpty(why) ? "終結寂主之後，深淵裂隙才會開啟" : why) + "。";
                return;
            }
            _max = MaxAllowed(d);
            _best.Text = d.RiftBest > 0 ? $"最深紀錄：第 {d.RiftBest} 層" : "你還不曾走完任何一層裂隙";
            _syncing = true;
            _slider.Visible = _max > 1;
            _slider.MaxValue = Math.Max(2, _max);
            _syncing = false;
            SetTier(_tier);
        });
    }

    // ───────────────────────── choosing ─────────────────────────

    private void OnSlider(double value)
    {
        if (_syncing) return;
        SetTier((int)Math.Round(value));
    }

    private void SetTier(int tier)
    {
        if (!_built) return;
        var d = Game.I?.Hero?.D;
        if (d == null) return;
        if (tier != _tier) _picked = true;
        _tier = Math.Clamp(tier, 1, _max);
        _tierLabel.Text = $"第 {_tier} 層";
        _syncing = true;
        _slider.Value = _tier;
        _syncing = false;
        _m10.Disabled = _m1.Disabled = _tier <= 1;
        _p10.Disabled = _p1.Disabled = _tier >= _max;

        if (_tier > d.RiftBest) _tierNote.Text = _tier >= MaxTier ? "裂隙的最深處。通關即刷新你的紀錄。" : "尚未通關的一層。通關即刷新紀錄，並開啟下一層。";
        else _tierNote.Text = "已通關的一層，可以反覆進入。";
        _tierNote.AddThemeColorOverride("font_color", _tier > d.RiftBest ? UiTheme.Gold : UiTheme.Dim);
        _scaling.Text = ScalingText(_tier, d);
    }

    private static string Times(float v) => v < 100f ? $"×{v:0.##}" : v < 1e6f ? $"×{v:N0}" : $"×{v:0.##E+0}";

    private static string ScalingText(int tier, SaveData d)
    {
        var g = Game.I;
        int wt = Math.Clamp(g?.WorldTier ?? 0, 0, Balance.TierLevelBonus.Length - 1);
        int level = Balance.AreaLevel(1, d.Level, wt, true);         // as Level.Build does for a rift
        float rift = RiftScale(tier);
        float hp = rift * Balance.TierHp[wt], dmg = rift * Balance.TierDmg[wt];
        string danger = WorldUiKit.Hex(new Color(1f, 0.55f, 0.42f)), good = WorldUiKit.Hex(Palette.Good), dim = WorldUiKit.Hex(UiTheme.Dim), gold = WorldUiKit.Hex(UiTheme.Gold);
        int minutes = (int)MathF.Round(Level.RiftTime / 60f);

        var sb = new StringBuilder(512);
        sb.Append($"[color=#{dim}]魔物等級[/color]　　[color=#{danger}]{level}[/color]\n");
        sb.Append($"[color=#{dim}]魔物生命[/color]　　[color=#{danger}]{Times(hp)}[/color]　[color=#{dim}]（裂隙 {Times(rift)}，世界階級「{WorldUiKit.TierName(wt)}」{Times(Balance.TierHp[wt])}）[/color]\n");
        sb.Append($"[color=#{dim}]魔物傷害[/color]　　[color=#{danger}]{Times(dmg)}[/color]　[color=#{dim}]（裂隙 {Times(rift)}，世界階級 {Times(Balance.TierDmg[wt])}）[/color]\n");
        if (tier < MaxTier) sb.Append($"[color=#{dim}]再深一層[/color]　　[color=#{danger}]生命與傷害再 ×{Growth:0.##}[/color]\n");
        sb.Append($"[color=#{dim}]時限[/color]　　　　{minutes} 分鐘\n");
        sb.Append($"[color=#{dim}]通關獎勵[/color]　　[color=#{good}]守衛的寶箱、金幣與鍛造材料——層數越深越豐厚[/color]\n");
        sb.Append($"　　　　　　[color=#{good}]燼符淬鍊機會 ×{GlyphDb.TriesPerClear}[/color]　[color=#{dim}]（{tier} 級以下的燼符必定淬鍊成功）[/color]");
        if (tier >= Quests.RiftForTopTier && d.MaxTier < ZoneDb.TierNames.Length - 1)
            sb.Append($"\n[color=#{gold}]通關此層，世界階級「{WorldUiKit.TierName(ZoneDb.TierNames.Length - 1)}」隨之開啟。[/color]");
        return sb.ToString();
    }

    private void Enter()
    {
        var g = Game.I;
        var d = g?.Hero?.D;
        if (d == null) return;
        if (!Unlocked())
        {
            WorldUiKit.Sfx("ui_error");
            Refresh();
            return;
        }
        int tier = Math.Clamp(_tier, 1, MaxAllowed(d));
        string from = g.Level != null && IsInstanceValid(g.Level) ? g.Level.AreaId : ZoneDb.Town;
        Log.Info("rift", $"entering rift tier {tier} (best {d.RiftBest}, world tier {g.WorldTier}) from {from}");
        WorldUiKit.Sfx("rift_open");
        WorldUiKit.ClosePanel(Id);
        g.EnterArea("rift:" + tier, from);
    }
}
