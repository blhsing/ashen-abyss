using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// Panel "guide" — 武器指南. A class selector, the class's bases grouped by stance (雙手 / 單手＋副手 / 雙持) with the
/// general rules first, and the detail of one base: hands, speed, spread, built-in stat (all from ItemDb), its play
/// style, strengths, weaknesses and best fit (WeaponGuide.Notes), class notes and bonuses, the plain-words comparison
/// with the alternatives, and — for the hero's own class — the equipped alternative beside it.
/// Opened with G (on a hovered weapon or off-hand: focused on that base), from the 可用武器 section of the sheet and
/// from the class introduction. Keys: ↑/↓ entries, ←/→ class, G or Esc closes.
/// Open arg: a <see cref="WeaponGuideUi.Focus"/>, an Item (its base, the hero's class) or a ClassId.
/// </summary>
public partial class WeaponGuideUi : UiPanel
{
    public sealed class Focus { public ClassId Class; public string Base; }

    public override string Id => "guide";

    private const float FrameW = 1440f, FrameH = 860f, MaxFrameW = 1860f, MaxFrameH = 1020f, ListW = 380f;
    private bool _built, _open;
    private PanelContainer _frame;
    private HFlowContainer _classes;
    private VBoxContainer _list, _detail;
    private ScrollContainer _listScroll, _detailScroll;
    private ClassId _cls;
    private string _base;                     // null = the general rules
    private readonly List<string> _entries = new();   // null first (rules), then the bases in list order

    /// <summary>For tests: what is shown now.</summary>
    public ClassId ShownClass => _cls;
    public string ShownBase => _base;
    public IReadOnlyList<string> ShownEntries => _entries;

    public override void _Ready()
    {
        base._Ready();
        Log.Guard("guide", EnsureBuilt);
    }

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        MouseFilter = MouseFilterEnum.Ignore;
        WorldUiKit.FullRect(this);
        _frame = WorldUiKit.Shell(this, new Vector2(FrameW, FrameH), "武器指南", () => WorldUiKit.ClosePanel(Id), out var header, out var body, 0f);
        FitFrame();
        _classes = new HFlowContainer();
        _classes.AddThemeConstantOverride("h_separation", 6);
        _classes.AddThemeConstantOverride("v_separation", 4);
        body.AddChild(_classes);

        var row = WorldUiKit.HBox(16);
        row.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(row);
        var well = UiTheme.Well() ?? new PanelContainer();
        well.SizeFlagsVertical = SizeFlags.ExpandFill;
        row.AddChild(well);
        _listScroll = UiTheme.MinSize(new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, ListW, 0f);
        well.AddChild(_listScroll);
        _list = WorldUiKit.VBox(4);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _listScroll.AddChild(_list);

        var pane = UiTheme.Frame(false) ?? new PanelContainer();
        pane.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pane.SizeFlagsVertical = SizeFlags.ExpandFill;
        row.AddChild(pane);
        _detailScroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        pane.AddChild(_detailScroll);
        var room = WorldUiKit.Margin(0, 0, 14, 0);
        room.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _detailScroll.AddChild(room);
        _detail = WorldUiKit.VBox(10);
        _detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        room.AddChild(_detail);
        body.AddChild(UiTheme.Label("↑／↓：選擇　←／→：切換職業　G 或 Esc：關閉", UiTheme.FsCaption, UiTheme.Dim));
    }

    /// <summary>The window grows with the text scale, up to what a 1920×1080 screen holds (and never past the screen).</summary>
    private void FitFrame()
    {
        if (_frame == null) return;
        var vp = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
        float w = MathF.Min(MathF.Min(MaxFrameW, UiTheme.Px(FrameW)), vp.X - 40f);
        float h = MathF.Min(MathF.Min(MaxFrameH, UiTheme.Px(FrameH)), vp.Y - 60f);
        _frame.CustomMinimumSize = new Vector2(w, h);
    }

    public override void OnTextScale() { if (_built) Log.Guard("guide", FitFrame); }

    public override void OnOpen(object arg)
    {
        EnsureBuilt();
        WorldUiKit.FullRect(this);
        _open = true;
        FitFrame();
        var h = Game.I?.Hero;
        _cls = h?.Class ?? ClassId.Emberblade;
        _base = null;
        switch (arg)
        {
            case Focus f: _cls = f.Class; _base = f.Base; break;
            case Item it when ItemDb.BaseOf(it.Base) != null: _base = it.Base; if (h == null || !ItemDb.UsableBy(it, h.Class)) _cls = FirstClassFor(it.Base, _cls); break;
            case ClassId c: _cls = c; break;
            case string s when s.Length > 0:        // "lampknight:polearm", "polearm" or "lampknight" (UI scripts)
            {
                var parts = s.Split(':');
                int ci = Array.IndexOf(Names.ClassKeys, parts[0]);
                if (ci >= 0) _cls = (ClassId)ci;
                string b = ci >= 0 ? (parts.Length > 1 ? parts[1] : null) : parts[0];
                if (ItemDb.BaseOf(b) != null) { _base = b; if (ci < 0) _cls = FirstClassFor(b, _cls); }
                break;
            }
        }
        if (_base != null && !WeaponGuide.BasesOf(_cls).Contains(_base)) _base = null;
        Refresh();
        Log.Info("guide", $"opened class={Names.ClassKey(_cls)} base={_base ?? "rules"}");
    }

    /// <summary>A class that can use this base (the current one if it can), for a base the hero's class cannot hold.</summary>
    private static ClassId FirstClassFor(string baseId, ClassId prefer)
    {
        if (WeaponGuide.BasesOf(prefer).Contains(baseId)) return prefer;
        for (int c = 0; c < Names.ClassCount; c++) if (WeaponGuide.BasesOf((ClassId)c).Contains(baseId)) return (ClassId)c;
        return prefer;
    }

    public override void OnClose() { _open = false; }

    public override void Refresh()
    {
        if (!_built || !_open) return;
        Log.Guard("guide", () => { BuildClasses(); BuildList(); BuildDetail(); });
    }

    private void Select(string baseId) { _base = baseId; Snd.Ui("ui_click"); Refresh(); _detailScroll.ScrollVertical = 0; }
    private void SelectClass(ClassId c) { if (c == _cls) return; _cls = c; if (_base != null && !WeaponGuide.BasesOf(c).Contains(_base)) _base = null; Snd.Ui("ui_click"); Refresh(); }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (!_open || e is not InputEventKey k || !k.Pressed || k.Echo) return;
        if (Game.I?.Ui is { } ui && (ui.ModalOpen || !ui.IsOpen(Id))) return;
        Key key = k.Keycode != Key.None ? k.Keycode : k.PhysicalKeycode;
        int at = _entries.IndexOf(_base);
        switch (key)
        {
            case Key.Up: if (at > 0) Select(_entries[at - 1]); break;
            case Key.Down: if (at < _entries.Count - 1) Select(_entries[at + 1]); break;
            case Key.Left: SelectClass((ClassId)(((int)_cls + Names.ClassCount - 1) % Names.ClassCount)); break;
            case Key.Right: SelectClass((ClassId)(((int)_cls + 1) % Names.ClassCount)); break;
            case Key.G: WorldUiKit.ClosePanel(Id); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    // ───────────────────────── building ─────────────────────────

    private void BuildClasses()
    {
        ItemUi.Clear(_classes);
        var mine = Game.I?.Hero?.Class;
        _classes.AddChild(UiTheme.Label("職業", UiTheme.FsRow, UiTheme.Dim));
        for (int i = 0; i < Names.ClassCount; i++)
        {
            var c = (ClassId)i;
            var b = UiTheme.Button(Names.Class(c) + (mine == c ? "（你）" : ""), () => SelectClass(c), UiTheme.FsRow);
            b.FocusMode = FocusModeEnum.None;
            UiTheme.TabTone(b, c == _cls);
            _classes.AddChild(b);
        }
    }

    private int Power => Math.Clamp((Game.I?.Hero?.D.Level ?? 30) * 10, 10, ItemDb.MaxPower);

    private Texture2D IconOf(string baseId)
    {
        var s = WeaponGuide.Sample(baseId, Power, _cls);
        if (s == null) return null;
        try { return ItemIcon3D.Get(s, 128) ?? Icons.ForItem(s); } catch (Exception) { return Icons.ForItem(s); }
    }

    private void BuildList()
    {
        ItemUi.Clear(_list);
        _entries.Clear();
        _entries.Add(null);
        _list.AddChild(Entry(null, "通用規則", "雙手、單手、副手、雙持、格擋"));
        foreach (var (title, bases) in WeaponGuide.Groups(_cls))
        {
            var head = UiTheme.Label(title, UiTheme.FsBody, UiTheme.Gold, true);
            _list.AddChild(ItemUi.Underlined(head));
            foreach (var b in bases)
            {
                var gb = ItemDb.BaseOf(b);
                _entries.Add(b);
                _list.AddChild(Entry(b, gb.Name, gb.Off ? "副手" : $"{gb.HandsName}・每秒攻擊 {gb.Aps:0.00}"));
            }
        }
    }

    private Control Entry(string baseId, string name, string sub)
    {
        bool on = baseId == _base;
        var btn = new Button { FocusMode = FocusModeEnum.None, Flat = !on, CustomMinimumSize = new Vector2(0, 58), MouseDefaultCursorShape = CursorShape.PointingHand };
        btn.Pressed += () => Log.Guard("guide", () => Select(baseId));
        var row = WorldUiKit.HBox(10);
        row.MouseFilter = MouseFilterEnum.Ignore;
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 4);
        btn.AddChild(row);
        if (baseId != null)
        {
            var icon = new TextureRect { Texture = IconOf(baseId), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(48, 48), MouseFilter = MouseFilterEnum.Ignore };
            row.AddChild(icon);
        }
        var col = WorldUiKit.VBox(0);
        col.MouseFilter = MouseFilterEnum.Ignore;
        var n = UiTheme.Label(name, UiTheme.FsRow, on ? UiTheme.Gold : UiTheme.Text); n.MouseFilter = MouseFilterEnum.Ignore;
        var s = UiTheme.Label(sub, UiTheme.FsCaption, UiTheme.Dim); s.MouseFilter = MouseFilterEnum.Ignore;
        col.AddChild(n); col.AddChild(s);
        row.AddChild(col);
        // the button's own height follows its content at every text size
        // (a line box is about 1.45 × its font size; two lines plus the row's 4 px insets and some air)
        btn.CustomMinimumSize = new Vector2(0, MathF.Max(62f, 1.45f * (UiTheme.Fs(UiTheme.FsRow) + UiTheme.Fs(UiTheme.FsCaption)) + 14f));
        return btn;
    }

    private Label Para(string text, int size = UiTheme.FsRow, Color? c = null)
    {
        var l = UiTheme.Label(text, size, c ?? UiTheme.Text);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return l;
    }

    private void Head(string text) => _detail.AddChild(ItemUi.Underlined(UiTheme.Label(text, UiTheme.FsBody, UiTheme.Gold, true)));

    private void BuildDetail()
    {
        ItemUi.Clear(_detail);
        if (_base == null) { BuildRules(); return; }
        var gb = ItemDb.BaseOf(_base);
        var notes = WeaponGuide.Notes.GetValueOrDefault(_base);
        var hero = Game.I?.Hero;

        // title: icon, name, hands
        var top = WorldUiKit.HBox(16);
        top.AddChild(new TextureRect { Texture = IconOf(_base), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(112, 112) });
        var tc = WorldUiKit.VBox(4);
        tc.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        tc.AddChild(UiTheme.Label(gb.Name, UiTheme.FsTitle, UiTheme.Gold, true));
        string how = gb.Off ? "副手" : gb.Hands == 2 ? "雙手武器" : ItemDb.Allows(_cls, _base, EquipSlot.OffHand) ? "單手（可雙持）" : "單手";
        tc.AddChild(UiTheme.Label($"{Names.Class(_cls)}・{how}", UiTheme.FsRow, UiTheme.Text));
        if (notes != null) tc.AddChild(Para(notes.Style, UiTheme.FsRow, UiTheme.Dim));
        top.AddChild(tc);
        // beside it: the hero's equipped alternative (same hand), when this is the hero's class
        var worn = hero != null && hero.Class == _cls ? WeaponGuide.EquippedAlternative(hero, _base) : null;
        if (worn != null) top.AddChild(WornCard(worn));
        _detail.AddChild(top);

        Head("數值（取自遊戲資料）");
        var sample = WeaponGuide.Sample(_base, Power, _cls);
        if (!gb.Off)
        {
            _detail.AddChild(Para($"每秒攻擊 {gb.Aps:0.00}　　每擊傷害起伏 ±{gb.Spread * 100f:0}%　　{gb.HandsName}"));
            if (gb.Hands == 2) _detail.AddChild(Para($"詞綴強度 ×{ItemDb.TwoHandAffixMul:0.#}，不能使用副手", UiTheme.FsCaption, UiTheme.Dim));
            else _detail.AddChild(Para($"傷害為雙手的 {ItemDb.OneHandDps * 100f:0}%" + (ItemDb.Allows(_cls, _base, EquipSlot.OffHand) ? $"；雙持時攻速 +{(ItemDb.DualSpeed - 1f) * 100f:0}%" : "；另一手可拿副手"), UiTheme.FsCaption, UiTheme.Dim));
        }
        else if (sample != null && sample.Armor > 0) _detail.AddChild(Para($"護甲 {sample.Armor}（物品強度 {Power}）"));
        foreach (var (s, lo, hi) in gb.Implicit)
        {
            string fmt(float v) => StatInfo.IsPct(s) ? $"{v:0.#}%" : $"{v:0}";
            float now = sample?.Implicits.FirstOrDefault(a => a.Stat == s)?.Value ?? lo;
            _detail.AddChild(Para($"◇ 內建 {StatInfo.Name(s)}：{fmt(lo)}–{fmt(hi)}（隨物品強度成長；強度 {Power} 約 {fmt(now)}）"));
        }

        if (notes != null)
        {
            Head("優勢與劣勢");
            _detail.AddChild(Para("＋ " + notes.Strong, UiTheme.FsRow, Palette.Good));
            _detail.AddChild(Para("－ " + notes.Weak, UiTheme.FsRow, Palette.Bad));
            _detail.AddChild(Para("適合：" + string.Join("・", notes.Fits), UiTheme.FsRow, UiTheme.Gold));
        }
        string cn = WeaponGuide.ClassNotes.GetValueOrDefault((_cls, _base));
        var bonuses = WeaponGuide.BonusesFor(_cls, _base).ToList();
        if (cn != null || bonuses.Count > 0)
        {
            Head($"{Names.Class(_cls)}");
            if (cn != null) _detail.AddChild(Para(cn));
            foreach (var (name, desc) in bonuses) _detail.AddChild(Para($"職業加成「{name}」：{desc}", UiTheme.FsRow, Palette.Ember));
        }

        var alts = WeaponGuide.Alternatives(_cls, _base);
        if (alts.Count > 0)
        {
            Head(gb.Off ? "與其他副手比較" : gb.Hands == 2 ? "與其他雙手武器比較" : "與其他單手武器比較");
            foreach (var a in alts) _detail.AddChild(Para($"• 對{ItemDb.BaseOf(a).Name}：{WeaponGuide.Compare(_base, a)}"));
        }
        // the other stance in one line (two-hand against the pair, and back)
        var g = ItemDb.Loadout(_cls);
        if (!gb.Off && g.TwoHand.Length > 0 && (g.MainHand.Length > 0))
            _detail.AddChild(Para(gb.Hands == 2
                ? $"改用單手：少了詞綴 ×{ItemDb.TwoHandAffixMul:0.#}，換來{(g.OffHand.Length > 0 ? "副手的屬性（" + string.Join("、", g.OffHand.Select(o => ItemDb.BaseOf(o).Name)) + "）" : g.CanDual ? "雙持的兩把武器詞綴與 +" + ((ItemDb.DualSpeed - 1f) * 100f).ToString("0") + "% 攻速" : "較快的出手")}。"
                : $"改用雙手（{string.Join("、", g.TwoHand.Select(t => ItemDb.BaseOf(t).Name))}）：詞綴 ×{ItemDb.TwoHandAffixMul:0.#}、傷害為全額，但放棄{(g.OffHand.Length > 0 ? "副手" : "雙持")}。",
                UiTheme.FsRow, UiTheme.Dim));
        if (worn != null && worn.Base != _base && ItemDb.BaseOf(worn.Base) != null)
        {
            Head("與目前裝備比較");
            _detail.AddChild(Para($"對你的{ItemDb.BaseOf(worn.Base).Name}：{WeaponGuide.Compare(_base, worn.Base)}"));
        }
    }

    private Control WornCard(Item worn)
    {
        var card = UiTheme.Well() ?? new PanelContainer();
        var v = WorldUiKit.VBox(4);
        card.AddChild(v);
        v.AddChild(UiTheme.Label("目前裝備", UiTheme.FsCaption, UiTheme.Dim));
        var r = WorldUiKit.HBox(8);
        r.AddChild(new TextureRect { Texture = (ItemIcon3D.Get(worn, 128) ?? Icons.ForItem(worn)), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(64, 64) });
        var c = WorldUiKit.VBox(2);
        c.AddChild(UiTheme.Label(worn.Name, UiTheme.FsRow, Palette.Rarity(worn.Rarity)));
        var wb = worn.Gb;
        if (wb != null) c.AddChild(UiTheme.Label(wb.Off ? $"{wb.Name}・副手" : $"{wb.Name}・{wb.HandsName}・每秒攻擊 {wb.Aps:0.00}", UiTheme.FsCaption, UiTheme.Dim));
        r.AddChild(c);
        v.AddChild(r);
        UiTheme.MinSize(card, 360f, 0f);
        return card;
    }

    private void BuildRules()
    {
        Head("通用規則");
        foreach (var line in WeaponGuide.Rules()) _detail.AddChild(Para("• " + line));
        Head($"{Names.Class(_cls)}可用的武器");
        foreach (var line in ItemDb.LoadoutLines(_cls)) _detail.AddChild(Para(line));
        _detail.AddChild(Para("在左側選擇一種武器，查看它的玩法、優劣與替代選擇。在物品欄、儲藏箱或商人處，指向武器或副手按 G 也能直接開啟它的說明。", UiTheme.FsCaption, UiTheme.Dim));
    }
}
