using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace AshenAbyss;

/// <summary>
/// Panel "quests". Tab 任務: the campaign and the commissions (active and finished) with what has been done, the step
/// in hand and the rewards. Tab 名望: one page per region with its four renown tiers, their rewards, the progress
/// toward the next one and what is still left to do there.
/// Optional argument: "renown", a region id (opens its renown page) or a quest id (selects that quest).
/// </summary>
public partial class QuestLogUi : UiPanel
{
    public override string Id => "quests";

    // Design pixels at text scale 1. The list column and the window grow with the text scale (the window only as far as
    // a 1920×1080 screen allows); the detail column takes what is left and wraps its text to that width.
    private const float ListW = 470f, DetailW = 930f;
    private const float FrameW = 1540f, FrameH = 880f, MaxFrameW = 1840f, MaxFrameH = 1000f;
    // the renown bar: caption size, track thickness, how far a notch sticks out, and the gap between notch and caption
    private const int BarFont = UiTheme.FsCaption;
    private const float BarTrack = 16f, BarNotch = 5f, BarGap = 3f;

    private bool _built, _open, _subscribed, _dirty;
    private int _tab;
    private string _questId, _regionId;
    private PanelContainer _frame;
    private Button _tabQuests, _tabRenown;
    private Control _questPage, _renownPage;
    private VBoxContainer _questList, _questDetail, _regionList, _regionDetail;
    private int _barPoints, _errors;
    private Color _barAccent = new(1f, 0.6f, 0.2f);
    // the raster pieces of the renown bar, looked up once (null without the art)
    private static bool _artLooked;
    private static UiArt.Piece _pTrough, _pStud, _pStudGold;

    private static void LookArt()
    {
        if (_artLooked) return;
        _artLooked = true;
        _pTrough = WorldUiKit.Piece("input"); _pStud = WorldUiKit.Piece("bullet"); _pStudGold = WorldUiKit.Piece("bullet_gold");
    }

    public override void _Ready()
    {
        base._Ready();
        Log.Guard("questlog", EnsureBuilt);
    }

    // ───────────────────────── construction ─────────────────────────

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        MouseFilter = MouseFilterEnum.Ignore;        // the panel manager lays the dimmed backdrop underneath
        WorldUiKit.FullRect(this);

        _frame = WorldUiKit.Shell(this, new Vector2(FrameW, FrameH), "任務日誌", () => WorldUiKit.ClosePanel(Id), out var header, out var body, 0f);
        FitFrame();
        _tabQuests = UiTheme.MinSize(UiTheme.Button("任務", () => SetTab(0), UiTheme.FsRow), 120f, 0f);
        _tabRenown = UiTheme.MinSize(UiTheme.Button("名望", () => SetTab(1), UiTheme.FsRow), 120f, 0f);
        header.AddChild(_tabQuests); header.MoveChild(_tabQuests, 1);
        header.AddChild(_tabRenown); header.MoveChild(_tabRenown, 2);

        _questPage = Page(body, false, out _questList, out _questDetail);
        _renownPage = Page(body, true, out _regionList, out _regionDetail);
        SetTab(0);
    }

    /// <summary>The window grows with the text scale, up to what a 1920×1080 screen holds; both columns scroll inside it.</summary>
    private void FitFrame()
    {
        if (_frame == null) return;
        _frame.CustomMinimumSize = new Vector2(MathF.Min(MaxFrameW, UiTheme.Px(FrameW)), MathF.Min(MaxFrameH, UiTheme.Px(FrameH)));
    }

    /// <summary>The text size changed: the window takes its new size (the lists and details are rebuilt by Refresh).</summary>
    public override void OnTextScale()
    {
        if (_built) Log.Guard("questlog", FitFrame);
    }

    /// <summary>
    /// A list on the left and a detail column on the right, each in its own scroller. The quests are rows in an
    /// inset well; the regions (<paramref name="cards"/>) are cards standing on the window's own field. The detail
    /// column is a sub-panel.
    /// </summary>
    private static Control Page(VBoxContainer body, bool cards, out VBoxContainer list, out VBoxContainer detail)
    {
        var row = WorldUiKit.HBox(16);
        row.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(row);

        var left = UiTheme.MinSize(new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, ListW, 0f);
        list = WorldUiKit.VBox(cards ? 8 : 4);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (cards)
        {
            // a little room round the cards, so the scroller does not cut the shadow their mouldings cast
            row.AddChild(left);
            var around = WorldUiKit.Margin(6, 4, 8, 6);
            around.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            left.AddChild(around);
            around.AddChild(list);
        }
        else
        {
            var well = UiTheme.Well() ?? new PanelContainer();
            well.SizeFlagsVertical = SizeFlags.ExpandFill;
            row.AddChild(well);
            well.AddChild(left);
            left.AddChild(list);
        }

        var pane = UiTheme.Frame(false) ?? new PanelContainer();
        pane.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pane.SizeFlagsVertical = SizeFlags.ExpandFill;
        row.AddChild(pane);
        var right = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        pane.AddChild(right);
        // (a margin on the right keeps the captions clear of the scroll bar)
        var room = WorldUiKit.Margin(0, 0, 14, 0);
        room.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        right.AddChild(room);
        detail = WorldUiKit.VBox(10);
        detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        room.AddChild(detail);
        return row;
    }

    private void SetTab(int tab)
    {
        _tab = tab;
        _questPage.Visible = tab == 0;
        _renownPage.Visible = tab == 1;
        UiTheme.TabTone(_tabQuests, tab == 0);
        UiTheme.TabTone(_tabRenown, tab == 1);
    }

    // ───────────────────────── panel life cycle ─────────────────────────

    public override void OnOpen(object arg)
    {
        EnsureBuilt();
        WorldUiKit.FullRect(this);
        _open = true;
        _dirty = false;
        if (!_subscribed)
        {
            _subscribed = true;
            try { Quests.Changed += OnStoryChanged; }
            catch (Exception e) { Log.Warn("questlog", "cannot listen to quest changes: " + e.Message); }
        }

        int tab = 0;
        if (arg is string a && a.Length > 0)
        {
            if (a == "renown") tab = 1;
            else if (ZoneDb.Region(a) != null) { tab = 1; _regionId = a; }
            else if (QuestDb.Get(a) != null) _questId = a;
        }
        // the page of the region the hero stands in is the one they most likely want
        var here = Game.I?.Level?.Region;
        if (_regionId == null && here != null) _regionId = here.Id;
        if (_questId == null || !IsListed(QuestDb.Get(_questId))) _questId = DefaultQuest(here);
        SetTab(tab);
        Refresh();
        Log.Info("questlog", $"opened tab={(tab == 0 ? "quests" : "renown")} quest={_questId ?? "-"} region={_regionId ?? "-"}");
    }

    public override void OnClose()
    {
        _open = false;
        if (!_subscribed) return;
        _subscribed = false;
        try { Quests.Changed -= OnStoryChanged; }
        catch (Exception e) { Log.Warn("questlog", "cannot stop listening to quest changes: " + e.Message); }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_open) Log.Guard("questlog", OnClose);
    }

    private void OnStoryChanged() => _dirty = true;

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!_open || !_dirty) return;
        _dirty = false;
        Refresh();
    }

    public override void Refresh()
    {
        if (!_built || !_open) return;
        Log.Guard("questlog", () =>
        {
            BuildQuestList();
            BuildQuestDetail();
            BuildRegionList();
            BuildRegionDetail();
        });
    }

    // ───────────────────────── shared pieces ─────────────────────────

    /// <summary>True when <paramref name="piece"/> of the raster art can stand in for a text mark (◆ ● ○ ·).</summary>
    private static bool HasMark(string piece) => piece != null && UiTheme.Art && UiArt.Has(piece);

    /// <summary>
    /// One entry of a list column: a row plate (quests) or a card (regions), the chosen one lit. <paramref name="mark"/>
    /// names a small piece shown in front of the text (bullet, bullet_gold); the caller leaves the old text mark out
    /// of <paramref name="text"/> when <see cref="HasMark"/> says the piece exists.
    /// </summary>
    private static Button ListButton(string text, bool selected, Color col, Action onPress, bool card = false, string mark = null, bool faded = false)
    {
        var b = UiTheme.Button(text, onPress, UiTheme.FsRow);
        b.Alignment = HorizontalAlignment.Left;
        // a row is as wide as the list column; a name too long for it ends in an ellipsis and shows in full on hover
        b.ClipText = true;
        b.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        bool marked = HasMark(mark);
        if (WorldUiKit.Measure(WorldUiKit.BodyFont, text, UiTheme.FsRow).X > UiTheme.Px(ListW) - (card ? 84f : 78f) - (marked ? UiTheme.Px(28f) : 0f)) b.TooltipText = text;
        UiTheme.MinSize(b, 0f, card ? 62f : 48f);
        b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        b.FocusMode = FocusModeEnum.None;
        b.AddThemeColorOverride("font_color", selected ? UiTheme.Gold : col);
        if (selected)
        {
            b.AddThemeColorOverride("font_hover_color", UiTheme.Gold.Lightened(0.2f));
            b.AddThemeColorOverride("font_pressed_color", UiTheme.Gold);
        }
        if (card) WorldUiKit.CardTone(b, selected, faded);
        else WorldUiKit.RowTone(b, selected);
        if (marked)
        {
            // the mark is a small forged stud; it grows with the text like the glyph it replaces
            b.Icon = UiArt.Icon(mark);
            b.ExpandIcon = true;
            b.AddThemeConstantOverride("icon_max_width", (int)UiTheme.Px(24f));
            b.AddThemeConstantOverride("h_separation", (int)UiTheme.Px(8f));
            Color tint = faded ? new Color(0.62f, 0.6f, 0.58f) : Colors.White;
            b.AddThemeColorOverride("icon_normal_color", tint);
            b.AddThemeColorOverride("icon_hover_color", faded ? tint.Lightened(0.2f) : tint);
            b.AddThemeColorOverride("icon_pressed_color", tint);
            b.AddThemeColorOverride("icon_hover_pressed_color", tint);
            b.AddThemeColorOverride("icon_focus_color", tint);
        }
        return b;
    }

    /// <summary>A section of a detail page: a little air, then the heading over its forged line.</summary>
    private static Control Section(string title)
    {
        var box = WorldUiKit.VBox(0);
        box.MouseFilter = MouseFilterEnum.Ignore;
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 6f), MouseFilter = MouseFilterEnum.Ignore });
        box.AddChild(WorldUiKit.Heading(title, UiTheme.FsName));
        return box;
    }

    /// <summary>
    /// One line of a detail page: a mark, the text (wrapping in the room that is left) and an optional caption on the
    /// right. <paramref name="size"/> is a DESIGN size for the mark and the text; the caption is always caption-sized.
    /// The mark and caption columns grow with the text scale. With the art the mark is the small piece
    /// <paramref name="piece"/> (a stud, a set or an empty socket); <paramref name="mark"/> is its text form without.
    /// </summary>
    private static Control Row(string piece, string mark, Color markCol, string text, Color textCol, int size = UiTheme.FsRow, string right = null, Color? rightCol = null)
    {
        var row = WorldUiKit.HBox(10);
        if (HasMark(piece)) row.AddChild(WorldUiKit.Mark(piece, mark, markCol, size, 28f, piece.StartsWith("bullet", StringComparison.Ordinal) ? 1.3f : 1f));
        else
        {
            var m = UiTheme.MinSize(UiTheme.Label(mark, size, markCol), 28f, 0f);
            m.HorizontalAlignment = HorizontalAlignment.Center;
            m.VerticalAlignment = VerticalAlignment.Top;
            row.AddChild(m);
        }
        var t = WorldUiKit.Wrapped(text, size, textCol, 300f);
        t.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(t);
        if (!string.IsNullOrEmpty(right))
        {
            // the caption is smaller than the text: it is centred on the text's first line (rows are rebuilt by
            // Refresh after a text-scale change, so the measured line height is always the current one)
            var r = UiTheme.Label(right, UiTheme.FsCaption, rightCol ?? UiTheme.Dim);
            r.CustomMinimumSize = new Vector2(UiTheme.Px(150f), MathF.Ceiling(WorldUiKit.LineHeight(WorldUiKit.BodyFont, size)));
            r.HorizontalAlignment = HorizontalAlignment.Right;
            r.VerticalAlignment = VerticalAlignment.Center;
            r.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            row.AddChild(r);
        }
        return row;
    }

    private static string RegionName(string regionId) => ZoneDb.Region(regionId ?? "")?.Name ?? "";

    private static string PlaceName(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) return "";
        string name = ZoneDb.AreaName(areaId);
        var r = ZoneDb.RegionOf(areaId);
        return r != null && r.Id != areaId ? r.Name + "・" + name : name;
    }

    private static int CountFlags(SaveData d, string prefix)
    {
        int n = 0;
        if (d?.Flags == null) return 0;
        foreach (var f in d.Flags) if (f != null && f.StartsWith(prefix, StringComparison.Ordinal)) n++;
        return n;
    }

    // ───────────────────────── quests ─────────────────────────

    private static bool IsListed(QuestDef q)
    {
        if (q == null) return false;
        try { return Quests.StepIndex(q) != Quests.NotStarted; }
        catch (Exception) { return false; }
    }

    /// <summary>The main quest set in the region the hero stands in, else the earliest one under way, else anything listed.</summary>
    private static string DefaultQuest(RegionDef here)
    {
        try
        {
            QuestDef first = null, side = null;
            foreach (var q in Quests.Active())
            {
                if (q.Main)
                {
                    if (here != null && q.Region == here.Id) return q.Id;
                    first ??= q;
                }
                else side ??= q;
            }
            if (first != null) return first.Id;
            if (side != null) return side.Id;
            QuestDef last = null;
            foreach (var q in Quests.Completed()) last = q;
            return last?.Id;
        }
        catch (Exception e)
        {
            Log.Warn("questlog", "quest list unavailable: " + e.Message);
            return null;
        }
    }

    private void SelectQuest(string id)
    {
        _questId = id;
        _dirty = true;          // rebuilt on the next frame, once the button that was pressed has finished its click
    }

    private void BuildQuestList()
    {
        WorldUiKit.ClearChildren(_questList);
        List<QuestDef> mains = new(), sides = new(), done = new();
        try
        {
            foreach (var q in Quests.Active()) (q.Main ? mains : sides).Add(q);
            foreach (var q in Quests.Completed()) done.Add(q);
        }
        catch (Exception e)
        {
            if (_errors++ < 3) Log.Error("questlog", "reading the quest lists failed", e);
        }
        if (mains.Count + sides.Count + done.Count == 0)
        {
            _questId = null;
            _questList.AddChild(WorldUiKit.Wrapped("日誌還是空白的。", UiTheme.FsRow, UiTheme.Dim, ListW - 40f));
            return;
        }
        if (!IsListed(QuestDb.Get(_questId))) _questId = DefaultQuest(Game.I?.Level?.Region);

        QuestSection("主線", mains, "◆", UiTheme.Text);
        QuestSection("委託", sides, "◇", UiTheme.Text);
        QuestSection("已完成", done, "●", UiTheme.Dim);
    }

    private void QuestSection(string title, List<QuestDef> list, string mark, Color col)
    {
        if (list.Count == 0) return;
        if (_questList.GetChildCount() > 0) _questList.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8), MouseFilter = MouseFilterEnum.Ignore });
        _questList.AddChild(WorldUiKit.Heading($"{title}（{list.Count}）", UiTheme.FsRow, new Color(0.74f, 0.7f, 0.62f)));
        bool finished = mark == "●";
        foreach (var q in list)
        {
            string id = q.Id;
            // main quests wear the gold stud, commissions the plain one (finished ones the same, held back); without
            // the art the old text marks stand in front of the name
            string stud = q.Main ? "bullet_gold" : "bullet";
            string lead = HasMark(stud) ? "" : mark + " ";
            string text = q.Main ? $"{lead}{q.Name}" : $"{lead}{q.Name}　〔{RegionName(q.Region)}〕";
            _questList.AddChild(ListButton(text, id == _questId, col, () => SelectQuest(id), false, stud, finished));
        }
    }

    private void BuildQuestDetail()
    {
        WorldUiKit.ClearChildren(_questDetail);
        var q = QuestDb.Get(_questId);
        if (q == null)
        {
            _questDetail.AddChild(WorldUiKit.Wrapped("還沒有任何任務。\n到殘燈鎮的大火盆旁，找守燈長老艾爾溫談談。", UiTheme.FsBody, UiTheme.Dim, DetailW - 40f));
            return;
        }
        int st = Quests.StepIndex(q);
        bool done = st == Quests.Done;

        // design sizes: quest name 36, section headings 28, description / reward / step in hand 24, the line under
        // the name and finished steps 22, captions 20
        _questDetail.AddChild(WorldUiKit.Wrapped(q.Name, 36, done ? UiTheme.Text : UiTheme.Gold, DetailW - 40f, true));
        var sub = new StringBuilder(64);
        sub.Append(Quests.ActLabel(q));
        string region = RegionName(q.Region);
        if (region.Length > 0) sub.Append("　·　").Append(region);
        if (!string.IsNullOrEmpty(q.Giver)) sub.Append(q.Main ? "　·　" : "　·　委託人：").Append(q.Giver);
        if (done) sub.Append("　·　已完成");
        _questDetail.AddChild(WorldUiKit.Wrapped(sub.ToString(), UiTheme.FsRow, done ? Palette.Good : UiTheme.Dim, DetailW - 40f));
        if (!string.IsNullOrEmpty(q.Desc)) _questDetail.AddChild(WorldUiKit.Wrapped(q.Desc, UiTheme.FsBody, UiTheme.Text, DetailW - 40f));

        // the step in hand wears the gold stud; a finished step is an ember set in its socket
        _questDetail.AddChild(Section("目標"));
        int shown = done ? q.Steps.Count : Math.Min(st + 1, q.Steps.Count);
        for (int i = 0; i < shown; i++)
        {
            bool current = !done && i == st;
            string text = Quests.StepText(q, i);
            if (current)
            {
                string place = PlaceName(q.Steps[i].Area);
                _questDetail.AddChild(Row("bullet_gold", "◆", UiTheme.Gold, text, UiTheme.Text, UiTheme.FsBody, place, UiTheme.Dim));
            }
            else _questDetail.AddChild(Row("radio_on", "●", Palette.Good.Darkened(0.25f), text, UiTheme.Dim));
        }
        if (!done && shown < q.Steps.Count) _questDetail.AddChild(Row(null, "", UiTheme.Dim, "……之後的事，走到了才知道。", new Color(UiTheme.Dim, 0.7f), UiTheme.FsCaption));

        _questDetail.AddChild(Section(done ? "已領取的獎勵" : "獎勵"));
        string reward;
        if (done)
        {
            // experience was scaled to the hero's level on the day; only the fixed parts are still known
            var parts = new List<string>(4);
            if (q.XpFrac > 0f) parts.Add("經驗");
            if (q.Gold > 0) parts.Add($"金幣 {q.Gold:N0}");
            if (q.SkillPts > 0) parts.Add($"技能點 +{q.SkillPts}");
            if (q.PotionCap > 0) parts.Add($"藥水上限 +{q.PotionCap}");
            if (!q.Main) parts.Add($"名望 +{Quests.RenownSideQuest}");
            reward = string.Join("　", parts);
        }
        else reward = Quests.RewardText(q);
        _questDetail.AddChild(WorldUiKit.Wrapped(string.IsNullOrEmpty(reward) ? "沒有額外的獎勵。" : reward, UiTheme.FsBody, done ? UiTheme.Dim : Palette.Good, DetailW - 40f));
    }

    // ───────────────────────── renown ─────────────────────────

    private void SelectRegion(string id)
    {
        _regionId = id;
        _dirty = true;
    }

    private static string TierName(int tier) => Quests.RenownTierNames[Math.Clamp(tier, 0, Quests.RenownTierNames.Length - 1)];

    private void BuildRegionList()
    {
        WorldUiKit.ClearChildren(_regionList);
        if (ZoneDb.Region(_regionId ?? "") == null) _regionId = ZoneDb.Regions.Count > 0 ? ZoneDb.Regions[0].Id : null;
        _regionList.AddChild(WorldUiKit.Heading("瑟蘭諸地", UiTheme.FsRow, new Color(0.74f, 0.7f, 0.62f)));
        foreach (var r in ZoneDb.Regions)
        {
            string id = r.Id;
            bool open = WorldUiKit.CanEnter(id);
            int tier = WorldUiKit.RenownTier(id), pts = WorldUiKit.RenownPoints(id);
            string text = open ? $"{r.Name}　{TierName(tier)}　{pts} 點" : $"{r.Name}　〔尚未開放〕";
            // one card per region: the chosen one lit, the closed ones held back
            _regionList.AddChild(ListButton(text, id == _regionId, open ? UiTheme.Text : UiTheme.Dim, () => SelectRegion(id), true, null, !open));
        }
        _regionList.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10), MouseFilter = MouseFilterEnum.Ignore });
        _regionList.AddChild(WorldUiKit.Wrapped(
            "每片土地都記得誰為它點過火。解放據點、討伐首領、淨空地城、尋得祭壇、平息事件與完成委託，都會累積那片土地的名望。",
            UiTheme.FsCaption, UiTheme.Dim, ListW - 40f));
    }

    private void BuildRegionDetail()
    {
        WorldUiKit.ClearChildren(_regionDetail);
        var r = ZoneDb.Region(_regionId ?? "");
        if (r == null) return;
        var d = Game.I?.Hero?.D;
        int tier = WorldUiKit.RenownTier(r.Id), pts = WorldUiKit.RenownPoints(r.Id);
        var th = Quests.RenownThresholds;
        bool open = WorldUiKit.CanEnter(r.Id);

        // design sizes: region name 36, section headings 28, the renown line 24, the line under the name and the rows 22,
        // captions (points, states, the bar's labels) 20
        _regionDetail.AddChild(WorldUiKit.Wrapped(r.Name, 36, r.Accent.Lerp(Colors.White, 0.2f), DetailW - 40f, true));
        _regionDetail.AddChild(WorldUiKit.Wrapped($"{r.Subtitle}　·　{WorldUiKit.ActLabel(r)}　·　建議等級 {r.Level}", UiTheme.FsRow, UiTheme.Dim, DetailW - 40f));
        if (!open)
        {
            string why = WorldUiKit.WhyLocked(r.Id);
            _regionDetail.AddChild(WorldUiKit.Wrapped("尚未開放" + (string.IsNullOrEmpty(why) ? "。" : "——" + why + "。"), UiTheme.FsRow, Palette.Bad, DetailW - 40f));
        }

        string next = tier >= th.Length ? "已達最高階。" : $"距離「{TierName(tier + 1)}」還差 {Math.Max(0, th[tier] - pts)} 點。";
        _regionDetail.AddChild(WorldUiKit.Wrapped($"名望「{TierName(tier)}」　第 {tier}/{th.Length} 階　·　{pts} 點　　{next}", UiTheme.FsBody, tier > 0 ? UiTheme.Gold : UiTheme.Text, DetailW - 40f));

        _barPoints = pts;
        _barAccent = r.Accent;
        // as tall as its two caption lines and the track need at the current text size (rebuilt with the page)
        var bar = new Control { CustomMinimumSize = new Vector2(DetailW - 60f, BarHeight()), MouseFilter = MouseFilterEnum.Ignore };
        bar.Draw += () => DrawRenownBar(bar);
        _regionDetail.AddChild(bar);

        // what is earned or done is an ember set in its socket; what is not yet, an empty socket
        _regionDetail.AddChild(Section("名望獎勵"));
        for (int t = 1; t <= th.Length; t++)
        {
            bool got = tier >= t;
            string reward = Quests.RenownRewardText(t);
            string state = got ? "已領取" : $"需 {th[t - 1]} 點";
            _regionDetail.AddChild(Row(got ? "radio_on" : "radio_off", got ? "●" : "○", got ? Palette.Good : UiTheme.Dim,
                $"{WorldUiKit.Roman[Math.Min(t - 1, WorldUiKit.Roman.Length - 1)]}　{TierName(t)}　——　{reward}",
                got ? UiTheme.Text : new Color(0.78f, 0.74f, 0.66f), UiTheme.FsRow, state, got ? Palette.Good : UiTheme.Dim));
        }

        _regionDetail.AddChild(Section("這片土地上的事"));
        bool hold = WorldUiKit.StrongholdFree(d, r);
        _regionDetail.AddChild(Check(hold, $"解放據點「{r.Stronghold}」，重燃燼塔", Quests.RenownStronghold));
        if (r.Dungeons != null)
            foreach (var dg in r.Dungeons)
            {
                if (dg.Story) continue;
                _regionDetail.AddChild(Check(WorldUiKit.DungeonCleared(d, dg), $"淨空地城「{dg.Name}」", Quests.RenownDungeon));
            }
        string boss = MonsterDb.Get(r.Boss)?.Name ?? "首領";
        DungeonDef lair = null;
        if (r.Dungeons != null) foreach (var dg in r.Dungeons) if (dg.Story) lair = dg;
        _regionDetail.AddChild(Check(WorldUiKit.BossSlain(d, r), lair != null ? $"在「{lair.Name}」討伐{boss}" : $"討伐{boss}", Quests.RenownBoss));
        foreach (var q in QuestDb.All)
        {
            if (q.Main || q.Region != r.Id) continue;
            int st = Quests.StepIndex(q);
            if (st == Quests.NotStarted) _regionDetail.AddChild(Check(false, "尚未聽聞的委託", Quests.RenownSideQuest));
            else _regionDetail.AddChild(Check(st == Quests.Done, $"委託「{q.Name}」", Quests.RenownSideQuest));
        }
        int altars = CountFlags(d, "q_alt_" + r.Id + "|"), events = Math.Min(Quests.RenownEventCap, CountFlags(d, "q_ev_" + r.Id + "_"));
        _regionDetail.AddChild(Row("bullet", "·", UiTheme.Dim, $"已尋得燼火祭壇 {altars} 座", UiTheme.Text, UiTheme.FsRow, $"每座 +{Quests.RenownAltar}", UiTheme.Dim));
        _regionDetail.AddChild(Row("bullet", "·", UiTheme.Dim, $"已平息區域事件 {events}/{Quests.RenownEventCap} 次", UiTheme.Text, UiTheme.FsRow, $"每次 +{Quests.RenownEvent}", UiTheme.Dim));
    }

    private static Control Check(bool done, string text, int points) =>
        Row(done ? "radio_on" : "radio_off", done ? "●" : "○", done ? Palette.Good : UiTheme.Dim, text, done ? UiTheme.Dim : UiTheme.Text, UiTheme.FsRow, $"名望 +{points}", done ? Palette.Good.Darkened(0.2f) : UiTheme.Gold);

    /// <summary>
    /// Height of the renown bar at the current text size: a line of tier names, the track with its notches, a line of
    /// thresholds. <see cref="DrawRenownBar"/> lays the same pieces out from the same measurements.
    /// </summary>
    private static float BarHeight() =>
        MathF.Ceiling(2f * WorldUiKit.LineHeight(WorldUiKit.SmallFont, BarFont) + BarTrack + 2f * (BarNotch + BarGap));

    /// <summary>The road from 陌路 to 傳頌: a filled track with one notch per tier, its name above and its threshold below.</summary>
    private void DrawRenownBar(Control bar)
    {
        try
        {
            var th = Quests.RenownThresholds;
            if (th == null || th.Length == 0) return;
            var font = WorldUiKit.SmallFont;
            // measured, not tuned: the names stand on the first line, the track follows below it, the numbers below that
            float lineH = WorldUiKit.LineHeight(font, BarFont), ascent = WorldUiKit.Ascent(font, BarFont);
            float w = bar.Size.X, left = 8f, right = w - 48f, h = BarTrack;
            float y = MathF.Round(lineH + BarGap + BarNotch);
            float nameBase = MathF.Round(ascent), numBase = MathF.Round(y + h + BarNotch + BarGap + ascent);
            float max = th[^1];
            float span = right - left;
            float frac = Math.Clamp(_barPoints / max, 0f, 1f);
            Color fill = _barAccent.Lerp(UiTheme.Gold, 0.5f);
            LookArt();
            if (_pTrough != null)
            {
                // a forged trough; the fill keeps the region's colour and lies inside its rim
                UiArt.Nine(bar, _pTrough, new Rect2(left - 3f, y - 2f, span + 6f, h + 4f));
                if (frac > 0f) bar.DrawRect(new Rect2(left + 1f, y + 2f, (span - 2f) * frac, h - 4f), fill);
            }
            else
            {
                bar.DrawRect(new Rect2(left, y, span, h), new Color(0.04f, 0.036f, 0.032f));
                if (frac > 0f) bar.DrawRect(new Rect2(left + 1f, y + 1f, (span - 2f) * frac, h - 2f), fill);
                bar.DrawRect(new Rect2(left, y, span, h), new Color(UiTheme.Border, 0.95f), false, 1f);
            }
            for (int i = 0; i < th.Length; i++)
            {
                float x = left + span * (th[i] / max);
                bool got = _barPoints >= th[i];
                var col = got ? UiTheme.Gold : UiTheme.Dim;
                // each tier is a stud riveted to the trough: gold once reached (a notch line without the art)
                var stud = got ? _pStudGold ?? _pStud : _pStud;
                if (_pTrough != null && stud != null)
                    UiArt.SpriteCentered(bar, stud, new Vector2(MathF.Round(x), MathF.Round(y + h * 0.5f)), got ? Colors.White : new Color(0.7f, 0.68f, 0.68f), 1.25f);
                else bar.DrawLine(new Vector2(x, y - BarNotch), new Vector2(x, y + h + BarNotch), col, 2f);
                bool last = i == th.Length - 1;
                BarCaption(bar, font, TierName(i + 1), x, nameBase, col, last, w);
                BarCaption(bar, font, th[i].ToString(), x, numBase, col, last, w);
            }
        }
        catch (Exception e)
        {
            if (_errors++ < 3) Log.Error("questlog", "drawing the renown bar failed", e);
        }
    }

    /// <summary>
    /// One caption of a notch at <paramref name="x"/>, centred on it. The last notch sits at the right end of the
    /// track: its captions end a little past it instead. Either way the caption is kept inside the bar's width.
    /// </summary>
    private static void BarCaption(Control bar, Font font, string text, float x, float baseline, Color col, bool last, float w)
    {
        float tw = WorldUiKit.Measure(font, text, BarFont).X;
        float tx = last ? MathF.Min(x + 44f, w) - tw : x - tw * 0.5f;
        tx = Math.Clamp(tx, 0f, MathF.Max(0f, w - tw));
        WorldUiKit.Text(bar, font, new Vector2(MathF.Round(tx), baseline), text, BarFont, col);
    }
}
