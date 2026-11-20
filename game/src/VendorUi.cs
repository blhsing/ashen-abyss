using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AshenAbyss;

/// <summary>
/// Panel "vendor". Opened with the NPC id:
/// "merchant" (交易: buy, sell, buy back · 賭運: gamble per slot), "smith" (拆解 · 精鑄 · 淬火),
/// "occultist" (附魔 · 銘刻), "jeweler" (鑲嵌 · 合成 · 購買) and "stash" (bag ↔ 80-slot stash).
/// Every rule lives in Hero / Crafting / ItemDb / Loot; this class shows costs and reasons, asks before anything
/// precious is destroyed, plays the sound and rebuilds itself whenever the hero reports a change.
/// </summary>
public partial class VendorUi : UiPanel
{
    public override string Id => "vendor";

    /// <summary>The NPC whose panel is open right now (null while closed).</summary>
    public static string ActiveNpc { get; private set; }

    // Design sizes at text scale 1.0. The cell grids (Cell; PickW holds a well with ten cells, or ten priced cells
    // at 標準) keep their size at every text scale; the text columns (RightW, CodexW, ListW) and the price captions
    // (PriceW) grow with it, and so does the frame: 1522 × 940 at 標準, 1732 × 1028 at 特大 — see SizeFrame. The
    // frame never comes closer than CrestRoom to the top of a 1080-line screen (its crest reaches 34 px above it),
    // so at its tallest it stands 35 px from the top and 17 px from the bottom. Whatever can outgrow its pane scrolls.
    private const int FrameH = 940, MaxFrameH = 1028, Cell = 64, GemCell = 52, PickW = 700, PriceW = 66;
    private const int BuyBackSize = 10, GambleLogSize = 10;
    private const float CrestRoom = 35f;

    /// <summary>Text width of the crafting pane right of the gear picker (700 design px, scaled with the text).</summary>
    private static float RightW => UiTheme.Px(700f);
    /// <summary>Room the detail pane's moulding takes left and right of its content together (the theme's sub-panel).</summary>
    private static float PanePad
    {
        get
        {
            var sb = UiTheme.PanelBox;
            return sb == null ? 0f : sb.GetMargin(Side.Left) + sb.GetMargin(Side.Right);
        }
    }
    /// <summary>Outer width of the crafting pane: its text column inside the sub-panel moulding.</summary>
    private static float PaneW => RightW + PanePad;
    /// <summary>Width of the occultist's codex column (440 design px, scaled with the text).</summary>
    private static float CodexW => UiTheme.Px(440f);
    /// <summary>Width of the jeweler's combine list (700 design px, scaled with the text; never wider than PaneW, which sizes the frame).</summary>
    private static float ListW => UiTheme.Px(700f);
    /// <summary>
    /// Frame width: the picker and the crafting pane with their gap (20) — or two picker columns with the divider
    /// between them (50) — plus the frame's padding and 2 px to spare.
    /// </summary>
    private static float FrameW => MathF.Max(PickW + 20f + PaneW, 2f * PickW + 50f) + 2f * UiTheme.WinPadX + 2f;
    /// <summary>Width left for a grid inside a well that fills the picker column.</summary>
    private static float PickInner => PickW - ItemUi.WellPad;

    private sealed class NpcInfo { public string Name, Line; public string[] Tabs; }

    private static readonly Dictionary<string, NpcInfo> Npcs = new()
    {
        ["merchant"] = new NpcInfo { Name = "行商 莉雅", Line = "「路還沒斷的時候，貨就還有。」", Tabs = new[] { "交易", "賭運" } },
        ["smith"] = new NpcInfo { Name = "鐵匠 布蘭", Line = "「鐵不會說謊。火候不到，它就是不肯彎。」", Tabs = new[] { "拆解", "精鑄", "淬火" } },
        ["occultist"] = new NpcInfo { Name = "祕術師 薇菈", Line = "「每一道威能都有名字。記得住，它就留得下來。」", Tabs = new[] { "附魔", "銘刻" } },
        ["jeweler"] = new NpcInfo { Name = "寶石匠 蔻拉", Line = "「石頭裡睡著舊日的光。我只是把它叫醒。」", Tabs = new[] { "鑲嵌", "合成", "購買" } },
        ["stash"] = new NpcInfo { Name = "儲藏箱", Line = "留在火邊的東西，回來時還會在。", Tabs = new[] { "儲藏" } },
    };

    private static readonly ItemKind[] GambleKinds =
        { ItemKind.Weapon, ItemKind.Helm, ItemKind.Chest, ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet, ItemKind.Ring };
    private static readonly string[] TemperSlotNames = { "第一淬位", "第二淬位" };
    private static readonly StyleBox[] PipBoxes = new StyleBox[4];

    // what was sold this session can be bought back until newer sales push it out
    private static readonly List<(Item item, int price)> BuyBack = new();
    private static Hero _buyBackHero;

    private readonly SlotBoard _board = new();
    private readonly Dictionary<ItemKind, (Item item, int price)> _offers = new();   // hidden gamble rolls, one per slot
    private readonly List<Item> _gambleLog = new();
    private readonly HashSet<Item> _picked = new();                                    // salvage selection
    private readonly Dictionary<string, ScrollContainer> _scrolls = new();
    private readonly Dictionary<string, int> _scrollPos = new();
    private List<(Item item, int price)> _ownGems;                                     // jeweler fallback stock

    private bool _built, _open, _dirty;
    private Hero _hero;
    private string _npc = "merchant";
    private int _tab, _restore;
    private PanelContainer _frame;
    private Label _title, _line, _msg;
    private HBoxContainer _tabsBox, _moneyBox;
    private MarginContainer _body;
    private Item _sel;                       // gear chosen in a crafting tab
    private string _recipe, _aspect;
    private int _temperSlot, _affixIdx = -1, _candIdx;
    private Affix[] _cands;                  // enchant candidates waiting for the player's choice
    private Item _candItem;
    private float _msgT, _dt;
    private int _faults, _settle;

    // ───────────────────────── life cycle ─────────────────────────

    public VendorUi()
    {
        Name = "VendorUi";
    }

    public override void _Ready()
    {
        base._Ready();
        Log.Guard("ui", EnsureBuilt);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_open) ActiveNpc = null;
        _open = false;
        Hook(null);
        _board.Dispose();
    }

    public override void OnOpen(object arg)
    {
        EnsureBuilt();
        string npc = arg as string;
        if (string.IsNullOrEmpty(npc)) npc = "merchant";
        else if (!Npcs.ContainsKey(npc))
        {
            Log.Warn("vendor", $"unknown vendor id '{npc}', opening the merchant instead");
            npc = "merchant";
        }
        _npc = npc;
        ActiveNpc = npc;
        _open = true;
        _tab = 0;
        ResetWork();
        _offers.Clear();
        _gambleLog.Clear();
        _ownGems = null;
        _scrolls.Clear(); _scrollPos.Clear(); _restore = 0;
        _faults = 0;
        _msgT = 0f;
        if (_msg != null) _msg.Text = "";
        Hook(Game.I?.Hero);
        if (!ReferenceEquals(_buyBackHero, _hero)) { BuyBack.Clear(); _buyBackHero = _hero; }
        ItemMarks.Track(_hero);
        _hero?.PruneJunk();
        // this panel shows the bag itself and needs the room the inventory would take
        if (Game.I?.Ui != null && Game.I.Ui.IsOpen("inventory"))
            Callable.From(() => Game.I?.Ui?.Close("inventory")).CallDeferred();
        Log.Info("vendor", $"opened {npc}");
        Log.Guard("ui", Rebuild);
    }

    public override void OnClose()
    {
        if (_cands != null) Log.Info("craft", "enchant choice left open: original affix kept");
        if (_open) Log.Info("vendor", $"closed {_npc}");
        _open = false;
        ActiveNpc = null;
        ResetWork();
        _board.Clear();
        Hook(null);
    }

    public override void Refresh() => _dirty = true;

    private void ResetWork()
    {
        _sel = null; _picked.Clear();
        _recipe = null; _aspect = null;
        _temperSlot = 0; _affixIdx = -1;
        _cands = null; _candItem = null; _candIdx = 0;
    }

    private void Hook(Hero h)
    {
        if (ReferenceEquals(h, _hero)) return;
        if (_hero != null) _hero.Changed -= OnHeroChanged;
        _hero = h;
        if (_hero != null) _hero.Changed += OnHeroChanged;
    }

    private void OnHeroChanged() => _dirty = true;

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!_built) return;
        _dt = (float)delta;
        try { Tick(); }
        catch (Exception e)
        {
            // never let a fault escape the frame, and never let it repeat sixty times a second in the log
            _dirty = false;
            if (++_faults <= 5) Log.Error("ui", $"vendor panel ({_npc}, tab {_tab}) frame failed", e);
        }
    }

    private void Tick()
    {
        if (!_open) return;
        var h = Game.I?.Hero;
        if (!ReferenceEquals(h, _hero)) { Hook(h); ResetWork(); _dirty = true; }
        if (_msgT > 0f)
        {
            _msgT -= _dt;
            if (_msgT <= 0f) _msg.Text = "";
        }
        if (_dirty) Rebuild();
        if (_settle > 0) { _settle--; return; }     // rebuilt cells get their place at the end of the frame: hover resumes on the next
        if (_restore > 0) RestoreScroll();
        _board.Poll(this);
    }

    // ───────────────────────── frame ─────────────────────────

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _frame = UiTheme.Frame(true);
        _frame.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_frame);
        _frame.AnchorLeft = 0.5f; _frame.AnchorRight = 0.5f; _frame.AnchorTop = 0.5f; _frame.AnchorBottom = 0.5f;
        _frame.GrowHorizontal = GrowDirection.Both;
        _frame.GrowVertical = GrowDirection.Both;
        SizeFrame();

        // the heavy frame brings its own padding (UiTheme.WinPadX × WinPadY): at 標準 1458 × 884 remain for the content
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        _frame.AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 18);
        _title = UiTheme.Label("", UiTheme.FsTitle, UiTheme.Gold, true);
        head.AddChild(_title);
        _line = UiTheme.Trimmed(UiTheme.Label("", UiTheme.FsRow, UiTheme.Dim), false);
        _line.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _line.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(_line);
        var close = ItemUi.Btn("關閉", () => Game.I?.Ui?.Close("vendor"), UiTheme.FsRow);
        close.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(close);
        root.AddChild(head);

        _tabsBox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 8) };
        root.AddChild(_tabsBox);
        root.AddChild(UiTheme.Rule());

        _body = new MarginContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(_body);

        root.AddChild(UiTheme.Rule());
        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", 24);
        _moneyBox = new HBoxContainer();
        foot.AddChild(_moneyBox);
        // one line beside the purse: a message too long for it ends in an ellipsis and shows in full on hover
        _msg = UiTheme.Trimmed(UiTheme.Label("", UiTheme.FsRow, UiTheme.Text));
        _msg.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _msg.HorizontalAlignment = HorizontalAlignment.Right;
        _msg.VerticalAlignment = VerticalAlignment.Center;
        foot.AddChild(_msg);
        root.AddChild(foot);
    }

    /// <summary>
    /// The frame follows the text scale: as wide as the picker plus the crafting column (which grows with the text),
    /// and taller with larger text up to what a 1080-line screen holds under the crest. Content that needs more
    /// than this scrolls inside its pane; should a tab still ask for more, the frame grows evenly up and down.
    /// </summary>
    private void SizeFrame()
    {
        if (_frame == null || !IsInstanceValid(_frame)) return;
        float halfW = MathF.Round(FrameW * 0.5f);
        float h = MathF.Round(MathF.Min(UiTheme.Px(FrameH), MaxFrameH));
        // centred on the screen — but the crest needs its room above the frame, so a frame at its tallest sits low
        float top = MathF.Max(-MathF.Round(h * 0.5f), CrestRoom - 540f);
        _frame.OffsetLeft = -halfW; _frame.OffsetRight = halfW;
        _frame.OffsetTop = top; _frame.OffsetBottom = top + h;
    }

    /// <summary>The text size changed: the frame takes its new size; the content is rebuilt by the Refresh that follows.</summary>
    public override void OnTextScale()
    {
        SizeFrame();
        _dirty = true;
    }

    // ───────────────────────── rebuild ─────────────────────────

    private void Rebuild()
    {
        _dirty = false;
        if (!_built) return;
        _settle = 1;
        _board.Clear();
        if (_restore == 0) SaveScroll(); else _scrolls.Clear();
        ItemUi.Clear(_tabsBox);
        ItemUi.Clear(_body);
        ItemUi.Clear(_moneyBox);

        var info = Npcs[_npc];
        _title.Text = info.Name;
        _line.Text = info.Line;
        var h = _hero;
        if (h == null) return;

        if (_sel != null && !h.Owns(_sel)) _sel = null;
        _picked.RemoveWhere(i => !h.InBag(i));
        if (_cands != null && (_candItem == null || !h.Owns(_candItem) || _candIdx < 0 || _candIdx >= _candItem.Affixes.Count))
        {
            _cands = null; _candItem = null;
        }
        _tab = Math.Clamp(_tab, 0, info.Tabs.Length - 1);
        if (info.Tabs.Length > 1) _tabsBox.AddChild(ItemUi.Tabs(info.Tabs, _tab, PickTab, UiTheme.FsBody));
        _moneyBox.AddChild(ItemUi.Money(_board, h.D));

        Control content = _npc switch
        {
            "smith" => _tab == 0 ? SalvageTab(h) : _tab == 1 ? MasterworkTab(h) : TemperTab(h),
            "occultist" => _tab == 0 ? EnchantTab(h) : ImprintTab(h),
            "jeweler" => _tab == 0 ? SocketTab(h) : _tab == 1 ? CombineTab(h) : GemShopTab(h),
            "stash" => StashTab(h),
            _ => _tab == 0 ? TradeTab(h) : GambleTab(h),
        };
        _body.AddChild(content);
        _restore = 2;
    }

    private void PickTab(int i)
    {
        if (i == _tab || Busy()) return;
        _tab = i;
        _affixIdx = -1;
        _picked.Clear();
        _dirty = true;
    }

    private void SaveScroll()
    {
        foreach (var kv in _scrolls)
            if (GodotObject.IsInstanceValid(kv.Value)) _scrollPos[kv.Key] = kv.Value.ScrollVertical;
        _scrolls.Clear();
    }

    /// <summary>Puts the lists back where they were once the rebuilt content has been laid out.</summary>
    private void RestoreScroll()
    {
        _restore--;
        foreach (var kv in _scrolls)
            if (_scrollPos.TryGetValue(kv.Key, out int y) && GodotObject.IsInstanceValid(kv.Value)) kv.Value.ScrollVertical = y;
    }

    private ScrollContainer Scroller(string key, Control content, Vector2 minSize)
    {
        var sc = ItemUi.Scroll(content, minSize);
        _scrolls[$"{_npc}/{_tab}/{key}"] = sc;
        return sc;
    }

    /// <summary>
    /// A scrolling pane that takes whatever height its column has left, so the fixed parts around it (headings,
    /// costs, the action button) always stay on screen. <paramref name="width"/> is in final pixels;
    /// <paramref name="minDesignH"/> is the least height it keeps, in design pixels (scaled with the text).
    /// </summary>
    private ScrollContainer FlexScroller(string key, Control content, float width, float minDesignH)
    {
        var sc = Scroller(key, content, new Vector2(width, UiTheme.Px(minDesignH)));
        sc.SizeFlagsVertical = SizeFlags.ExpandFill;
        return sc;
    }

    // ───────────────────────── small helpers ─────────────────────────

    private void Say(string text, Color? c = null)
    {
        if (_msg == null) return;
        _msg.Text = text;
        _msg.AddThemeColorOverride("font_color", c ?? UiTheme.Text);
        _msgT = 6f;
    }

    private void Fail(string why)
    {
        Say(why, Palette.Bad);
        Snd.Ui("ui_error");
    }

    private void Confirm(string text, Action yes, string yesText)
    {
        var ui = Game.I?.Ui;
        if (ui == null) { Log.Guard("ui", yes); return; }
        ui.Confirm(text, () => Log.Guard("ui", yes), yesText, "取消");
    }

    /// <summary>True (with a reminder) while an enchant result is waiting for the player's choice.</summary>
    private bool Busy()
    {
        if (_cands == null) return false;
        Fail("請先決定這次附魔要留下哪一條詞綴。");
        return true;
    }

    private static bool Precious(Item it) => it != null && it.Rarity >= Rarity.Legendary;

    private static HBoxContainer Columns()
    {
        var r = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };      // columns start at the frame's left edge, under the title
        r.AddThemeConstantOverride("separation", 20);
        return r;
    }

    private static VBoxContainer Column(float width = 0f, bool expand = false)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        if (width > 0f) v.CustomMinimumSize = new Vector2(width, 0);
        if (expand) v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return v;
    }

    /// <summary>
    /// The detail pane of a crafting tab: a sub-panel (thin moulding over the darker field) that takes the width the
    /// picker leaves and the whole height of the body. Returns the column to fill.
    /// </summary>
    private static VBoxContainer Pane(HBoxContainer row)
    {
        var frame = UiTheme.Frame(false);
        frame.MouseFilter = MouseFilterEnum.Pass;
        frame.CustomMinimumSize = new Vector2(PaneW, 0);
        frame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var v = Column();
        frame.AddChild(v);
        row.AddChild(frame);
        return v;
    }

    /// <summary>
    /// A quiet plate under one line of a list (an offer, a recipe); the bare line without the row piece.
    /// <paramref name="held"/> holds the plate back for a line that cannot be used now, so its dim lettering
    /// keeps its contrast.
    /// </summary>
    private static Control RowPlate(Control line, bool held = false)
    {
        if (!UiTheme.Art || !UiArt.Has("row")) return line;
        var p = new PanelContainer { MouseFilter = MouseFilterEnum.Pass };
        p.AddThemeStyleboxOverride("panel", held
            ? UiTheme.ArtBox("items_row_held", () => new UiNine("row", null, 0f, 8f, 3f) { FrameMod = new Color(0.62f, 0.62f, 0.64f, 0.55f) })
            : UiTheme.ArtBox("row", 8f, 3f));
        p.AddChild(line);
        return p;
    }

    /// <summary>A section heading (running-text size, title face, gold) over the forged header line.</summary>
    private static Label Section(string text) => ItemUi.Underlined(UiTheme.Label(text, UiTheme.FsBody, UiTheme.Gold, true));

    /// <summary>An explanation at row size that wraps inside <paramref name="width"/> (final pixels).</summary>
    private static Label Note(string text, float width, Color? c = null)
    {
        var l = UiTheme.Label(text, UiTheme.FsRow, c ?? UiTheme.Dim);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(width, 0);
        return l;
    }

    /// <summary>The chosen item's name at name size; too long for its row, it ends in an ellipsis and shows in full on hover.</summary>
    private static Label NameLabel(Item it)
    {
        var l = UiTheme.Trimmed(UiTheme.Label(it.Upgrade > 0 ? $"{it.Name} +{it.Upgrade}" : it.Name, UiTheme.FsName, Palette.Rarity(it.Rarity), true));
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return l;
    }

    /// <summary>A button with the reason it is disabled written beside it (the reason wraps in the rest of the row).</summary>
    private static Control ActionRow(Button b, string why)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        b.Disabled = why != null;
        UiTheme.MinSize(b, 180, 52);
        b.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(b);
        if (why != null)
        {
            var l = UiTheme.Wrapped(UiTheme.Label(why, UiTheme.FsRow, Palette.Bad));
            l.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(l);
        }
        return row;
    }

    private static HBoxContainer ToolRow(params Button[] buttons)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        foreach (var b in buttons) row.AddChild(b);
        return row;
    }

    /// <summary>
    /// The chosen item's card in a pane that scrolls and takes the height its column has left (at least
    /// <paramref name="minDesignH"/> design px). The card text comes from ItemText.Tooltip with its sizes final.
    /// </summary>
    private Control ItemBox(Hero h, Item it, float minDesignH)
    {
        var rich = UiTheme.Rich(ItemText.Tooltip(it, null, h.D.Level, false), RightW - 24 - ItemUi.WellPad);
        return FlexWell("item:" + it.Uid, rich, RightW, minDesignH, out _);
    }

    /// <summary>
    /// A scrolling pane (see <see cref="FlexScroller"/>) sunk into a well. <paramref name="width"/> is the outer
    /// width in final pixels; the content has that minus the well's rim (ItemUi.WellPad).
    /// </summary>
    private PanelContainer FlexWell(string key, Control content, float width, float minDesignH, out ScrollContainer scroll)
    {
        scroll = FlexScroller(key, content, width - ItemUi.WellPad, minDesignH);
        var well = ItemUi.Well(scroll);
        well.SizeFlagsVertical = SizeFlags.ExpandFill;
        return well;
    }

    private static string PriceLine(Hero h, int price, string hint) =>
        ItemUi.Col(h.D.Gold >= price ? Palette.Gold : Palette.Bad, $"價格 {Crafting.Num(price)} 金幣") + "\n" + ItemUi.Dim(hint);

    private static Control KindIcon(Texture2D tex, int size)
    {
        var p = ItemUi.Empty(size);
        if (tex == null) return p;
        var t = new TextureRect
        {
            Texture = tex, Modulate = Icons.Tint(tex, new Color(UiTheme.Text, 0.85f)), MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        p.AddChild(t);
        t.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 6);
        return p;
    }

    // ───────────────────────── gear picker (worn + carried) ─────────────────────────

    /// <summary>Worn gear and the bag as one clickable list. Items with a <paramref name="whyNot"/> reason are dimmed and explain themselves when clicked.</summary>
    private Control GearPicker(Hero h, Func<Item, string> whyNot, Action<Item> onPick)
    {
        var v = Column(PickW);
        v.AddChild(Section("身上的裝備"));
        var worn = ItemUi.Grid(9);
        for (int i = 0; i < h.D.Equip.Length; i++)
        {
            var it = h.D.Equip[i];
            if (it != null) { worn.AddChild(PickCell(it, whyNot, onPick)); continue; }
            var slot = (EquipSlot)i;
            worn.AddChild(ItemUi.Empty(Cell, Icons.ForKind(ItemUi.KindOf(slot), slot == EquipSlot.Weapon ? (int)h.Class : -1)));
        }
        v.AddChild(ItemUi.Well(worn));
        v.AddChild(Section($"行囊（{h.D.Bag.Count} / {Hero.BagSize}）"));
        var bag = ItemUi.Grid(10);
        for (int i = 0; i < Hero.BagSize; i++)
            bag.AddChild(i < h.D.Bag.Count ? PickCell(h.D.Bag[i], whyNot, onPick) : ItemUi.Empty(Cell));
        v.AddChild(ItemUi.Well(bag));
        return v;
    }

    private Control PickCell(Item it, Func<Item, string> whyNot, Action<Item> onPick)
    {
        string why = whyNot?.Invoke(it);
        void Click(Item x)
        {
            if (why != null) Fail(why); else onPick(x);
        }
        var view = ItemUi.Slot(_board, it, Cell, Click, Click, null, false);
        if (why != null) ItemUi.Dimmed(view.Node);
        if (ReferenceEquals(it, _sel)) ItemUi.Outline(view.Node, UiTheme.Gold, 3);
        return view.Node;
    }

    private void Select(Item it)
    {
        if (Busy()) return;
        if (!ReferenceEquals(_sel, it)) { _affixIdx = -1; _temperSlot = 0; }
        _sel = it;
        _dirty = true;
        Snd.Ui("ui_click");
    }

    private Control BagView(Hero h)
    {
        var v = Column(PickW);
        v.AddChild(Section($"行囊（{h.D.Bag.Count} / {Hero.BagSize}）"));
        v.AddChild(ItemUi.Well(ItemUi.BagGrid(_board, h, Cell, null, null)));
        return v;
    }

    // ═════════════════════════ merchant ═════════════════════════

    private static List<(Item item, int price)> SafeStock(string npc, Hero h)
    {
        try { return Loot.VendorStock(npc, h) ?? new List<(Item item, int price)>(); }
        catch (Exception e)
        {
            Log.Error("vendor", $"stock of {npc} unavailable", e);
            return new List<(Item item, int price)>();
        }
    }

    /// <summary>
    /// How many priced cells fit side by side in a well of the picker column. A priced cell is as wide as the cell
    /// or its caption (PriceW design px, which grows with the text), whichever is wider: at 標準 that is ten with
    /// 2 px gaps (the cells themselves then stand 4 px apart, as in the bag) and nine with 6 px, at 特大 seven — the
    /// grids simply take another row.
    /// </summary>
    private static int PriceColumns(int most, int sep)
    {
        float cell = MathF.Max(Cell, UiTheme.Px(PriceW));
        return Math.Clamp((int)((PickInner + sep) / (cell + sep)), 4, most);
    }

    /// <summary>The priced cells of a shop on their shelf: a well as wide as the column, filled from the left.</summary>
    private Control StockGrid(Hero h, List<(Item item, int price)> stock)
    {
        var grid = ItemUi.Grid(PriceColumns(9, 6), 6);
        foreach (var e in stock)
        {
            var entry = e;
            if (entry.item == null) continue;
            var view = ItemUi.Slot(_board, entry.item, Cell, x => BuyStock(entry), x => BuyStock(entry), x => PriceLine(h, entry.price, "左鍵：購買"));
            grid.AddChild(ItemUi.Captioned(view.Node, Crafting.Num(entry.price), h.D.Gold >= entry.price ? Palette.Gold : Palette.Bad, PriceW));
        }
        return ItemUi.Well(grid, true, false);
    }

    private Control TradeTab(Hero h)
    {
        var row = Columns();
        var left = Column(PickW);
        left.AddChild(Section("莉雅的貨物"));
        var stock = SafeStock("merchant", h);
        if (stock.Count == 0) left.AddChild(Note("貨架空了。等你下次回到鎮上，她會帶來新的東西。", PickW));
        else left.AddChild(StockGrid(h, stock));

        left.AddChild(Section("購回"));
        var back = ItemUi.Grid(PriceColumns(BuyBackSize, 2), 2);
        back.AddThemeConstantOverride("v_separation", 4);
        for (int i = 0; i < BuyBackSize; i++)
        {
            if (i >= BuyBack.Count) { back.AddChild(ItemUi.Captioned(ItemUi.Empty(Cell), "", UiTheme.Dim, PriceW)); continue; }
            var entry = BuyBack[i];
            var view = ItemUi.Slot(_board, entry.item, Cell, x => BuyBackItem(entry.item), x => BuyBackItem(entry.item), x => PriceLine(h, entry.price, "左鍵：以賣出的價格購回"));
            back.AddChild(ItemUi.Captioned(view.Node, Crafting.Num(entry.price), h.D.Gold >= entry.price ? Palette.Gold : Palette.Bad, PriceW));
        }
        left.AddChild(ItemUi.Well(back, true, false));
        left.AddChild(Note("賣掉的東西會暫時留在這裡，直到被更新的貨擠掉。", PickW));
        row.AddChild(left);
        row.AddChild(ItemUi.VRule());
        row.AddChild(SellColumn(h));
        return row;
    }

    private VBoxContainer SellColumn(Hero h)
    {
        var right = Column(PickW);
        right.AddChild(Section($"行囊（{h.D.Bag.Count} / {Hero.BagSize}）"));
        right.AddChild(ItemUi.Well(ItemUi.BagGrid(_board, h, Cell, SellClick, SellClick, x => ItemUi.Dim("左鍵或右鍵：賣出　Shift＋左鍵：標記廢品"))));
        int junk = h.JunkItems().Count;
        var sellJunk = ItemUi.Btn(junk > 0 ? $"賣出所有廢品（{junk}）" : "賣出所有廢品", SellJunk, UiTheme.FsRow);
        sellJunk.Disabled = junk == 0;
        right.AddChild(ToolRow(sellJunk, ItemUi.Btn("整理", SortBag, UiTheme.FsRow)));
        right.AddChild(Note("左鍵或右鍵：賣出　Shift＋左鍵：標記廢品", PickW));
        return right;
    }

    private void SortBag() => _hero?.SortBag();

    private void BuyStock((Item item, int price) e)
    {
        var h = _hero;
        if (h == null || e.item == null) return;
        string why = h.WhyCantBuy(e.price);
        if (why != null) { Fail(why); return; }
        if (!h.Buy(e.item, e.price)) { Fail("這筆交易沒有談成。"); return; }
        Snd.Ui("coins");
        Say($"買下「{e.item.Name}」，花費 {Crafting.Num(e.price)} 金幣。", Palette.Gold);
        Log.Info("trade", $"bought {e.item.Name} ({e.item.Rarity}, power {e.item.Power}) from {_npc} for {e.price}; gold={h.D.Gold}");
    }

    private void BuyBackItem(Item it)
    {
        var h = _hero;
        if (h == null) return;
        int i = BuyBack.FindIndex(e => ReferenceEquals(e.item, it));
        if (i < 0) return;
        var entry = BuyBack[i];
        string why = h.WhyCantBuy(entry.price);
        if (why != null) { Fail(why); return; }
        if (!h.Buy(entry.item, entry.price)) { Fail("這筆交易沒有談成。"); return; }
        BuyBack.RemoveAt(i);
        ItemMarks.See(it);
        Snd.Ui("coins");
        Say($"購回「{it.Name}」，花費 {Crafting.Num(entry.price)} 金幣。", Palette.Gold);
        Log.Info("trade", $"bought back {it.Name} for {entry.price}; gold={h.D.Gold}");
        _dirty = true;
    }

    private void SellClick(Item it)
    {
        var h = _hero;
        if (h == null || !h.InBag(it)) return;
        if (Input.IsKeyPressed(Key.Shift))
        {
            bool junk = h.ToggleJunk(it);
            Snd.Ui("ui_click");
            Say(junk ? $"已將「{it.Name}」標記為廢品。" : $"已取消「{it.Name}」的廢品標記。");
            return;
        }
        if (Precious(it) || it.Gems.Count > 0)
        {
            string gems = it.Gems.Count > 0 ? "\n鑲嵌在上面的寶石會一併賣掉。" : "";
            Confirm($"確定要賣出「{it.Name}」嗎？{gems}", () => SellNow(it, true), "賣出");
        }
        else SellNow(it, true);
    }

    private bool SellNow(Item it, bool announce)
    {
        var h = _hero;
        if (h == null || it == null) return false;
        int value = it.Value;
        if (!h.SellItem(it))
        {
            if (announce) Fail(h.WhyCantSell(it) ?? "無法賣出這件物品。");
            return false;
        }
        BuyBack.Insert(0, (it, value));
        while (BuyBack.Count > BuyBackSize) BuyBack.RemoveAt(BuyBack.Count - 1);
        if (announce)
        {
            Snd.Ui("coins");
            Say($"賣出「{it.Name}」，得到 {Crafting.Num(value)} 金幣。", Palette.Gold);
        }
        _dirty = true;
        return true;
    }

    private void SellJunk()
    {
        var h = _hero;
        if (h == null) return;
        var junk = h.JunkItems();
        if (junk.Count == 0) { Fail("沒有標記為廢品的物品。"); return; }
        int precious = junk.Count(Precious);
        void Go()
        {
            int n = 0; long gold = 0;
            foreach (var it in junk)
            {
                int value = it.Value;
                if (SellNow(it, false)) { n++; gold += value; }
            }
            if (n == 0) { Fail("沒有賣出任何物品。"); return; }
            Snd.Ui("coins");
            Say($"賣出 {n} 件廢品，得到 {Crafting.Num(gold)} 金幣。", Palette.Gold);
        }
        if (precious > 0) Confirm($"廢品之中有 {precious} 件傳奇或獨特裝備。\n確定要全部賣出嗎？", Go, "賣出");
        else Go();
    }

    // ───────────────────────── gambling ─────────────────────────

    /// <summary>The hidden piece waiting behind a slot. Loot rolls it and quotes the price; nothing is paid until the hero buys it.</summary>
    private (Item item, int price) Offer(ItemKind k, Hero h)
    {
        if (_offers.TryGetValue(k, out var o) && o.item != null) return o;
        try { o = Loot.Gamble(k, h); }
        catch (Exception e) { Log.Error("vendor", $"gamble offer for {k} failed", e); o = default; }
        _offers[k] = o;
        return o;
    }

    private Control GambleTab(Hero h)
    {
        var row = Columns();
        var left = Column(PickW);
        left.AddChild(Section("賭運"));
        left.AddChild(Note("莉雅的貨箱底下壓著幾件來路不明的東西。先付錢，再看貨——多半平庸，偶爾驚人。", PickW));
        // eight offers in a well, each on its plate and as tall as its button: with the note above they fill the
        // column at the largest text size, so the rows sit close together (what was just won lies under the bag,
        // where there is room). The name and price columns grow with the text.
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 2);
        foreach (var kind in GambleKinds)
        {
            var k = kind;
            var offer = Offer(k, h);
            bool weapon = k == ItemKind.Weapon;
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 14);
            var icon = KindIcon(Icons.ForKind(k, weapon ? (int)h.Class : -1), 44);
            icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            line.AddChild(icon);
            var name = UiTheme.Label(weapon ? ItemDb.WeaponTypeNames[(int)h.Class] : Names.Kind(k), UiTheme.FsRow, UiTheme.Text);
            UiTheme.MinSize(name, 150, 0);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;      // the price and the button keep to the right end of the plate
            name.VerticalAlignment = VerticalAlignment.Center;
            line.AddChild(name);
            bool afford = offer.price > 0 && h.D.Gold >= offer.price;
            var price = UiTheme.Label(offer.item != null ? $"{Crafting.Num(offer.price)} 金幣" : "今日無貨", UiTheme.FsRow, afford ? Palette.Gold : Palette.Bad);
            UiTheme.MinSize(price, 180, 0);
            price.HorizontalAlignment = HorizontalAlignment.Right;
            price.VerticalAlignment = VerticalAlignment.Center;
            line.AddChild(price);
            var roll = ItemUi.Btn("一擲", () => DoGamble(k), UiTheme.FsRow);
            UiTheme.MinSize(roll, 110, 0);
            roll.Disabled = offer.item == null;
            line.AddChild(roll);
            list.AddChild(RowPlate(line, offer.item == null));
        }
        left.AddChild(ItemUi.Well(list));
        row.AddChild(left);
        row.AddChild(ItemUi.VRule());
        var right = SellColumn(h);
        right.AddChild(Section("方才所得"));
        var got = ItemUi.Grid(GambleLogSize);
        for (int i = 0; i < GambleLogSize; i++)
        {
            if (i < _gambleLog.Count && h.Owns(_gambleLog[i])) got.AddChild(ItemUi.Slot(_board, _gambleLog[i], Cell, null, null).Node);
            else got.AddChild(ItemUi.Empty(Cell));
        }
        right.AddChild(ItemUi.Well(got));
        row.AddChild(right);
        return row;
    }

    private void DoGamble(ItemKind k)
    {
        var h = _hero;
        if (h == null) return;
        var o = Offer(k, h);
        if (o.item == null) { Fail("這一類東西，她今天沒有。"); return; }
        string why = h.WhyCantBuy(o.price);
        if (why != null) { Fail(why); return; }
        if (!h.Buy(o.item, o.price)) { Fail("這筆交易沒有談成。"); return; }
        _offers.Remove(k);
        _gambleLog.Remove(o.item);
        _gambleLog.Insert(0, o.item);
        while (_gambleLog.Count > GambleLogSize) _gambleLog.RemoveAt(_gambleLog.Count - 1);
        Snd.Ui("coins");
        if (o.item.Rarity == Rarity.Unique) Snd.Ui("drop_unique");
        else if (o.item.Rarity == Rarity.Legendary) Snd.Ui("drop_legendary");
        else if (o.item.Rarity == Rarity.Rare) Snd.Ui("drop_rare");
        Say($"擲得{Names.Rarity(o.item.Rarity)}物品：「{o.item.Name}」", Palette.Rarity(o.item.Rarity));
        Log.Info("trade", $"gambled on {k} for {o.price}: {o.item.Rarity} {o.item.Name} (power {o.item.Power}); gold={h.D.Gold}");
        _dirty = true;
    }

    // ═════════════════════════ stash ═════════════════════════

    private Control StashTab(Hero h)
    {
        var row = Columns();
        var left = Column(PickW);
        left.AddChild(Section($"行囊（{h.D.Bag.Count} / {Hero.BagSize}）"));
        left.AddChild(ItemUi.Well(ItemUi.BagGrid(_board, h, Cell, Deposit, Deposit, x => ItemUi.Dim("點擊：存入儲藏箱"))));
        left.AddChild(ToolRow(ItemUi.Btn("整理行囊", SortBag, UiTheme.FsRow)));
        left.AddChild(Note("點擊行囊中的物品存入，點擊儲藏箱中的物品取回。\n儲藏箱留在殘燈鎮的火邊；放進去的東西，不會跟著你走進灰暮。", PickW));
        row.AddChild(left);
        row.AddChild(ItemUi.VRule());

        var right = Column(PickW);
        right.AddChild(Section($"儲藏箱（{h.D.Stash.Count} / {Hero.StashSize}）"));
        var grid = ItemUi.Grid(10);
        for (int i = 0; i < Hero.StashSize; i++)
        {
            if (i < h.D.Stash.Count) grid.AddChild(ItemUi.Slot(_board, h.D.Stash[i], Cell, Withdraw, Withdraw, x => ItemUi.Dim("點擊：取回行囊")).Node);
            else grid.AddChild(ItemUi.Empty(Cell));
        }
        right.AddChild(ItemUi.Well(grid));
        right.AddChild(ToolRow(ItemUi.Btn("整理儲藏箱", SortStash, UiTheme.FsRow)));
        row.AddChild(right);
        return row;
    }

    private void SortStash() => _hero?.SortStash();

    private void Deposit(Item it)
    {
        var h = _hero;
        if (h == null) return;
        string why = h.WhyCantStash(it);
        if (why != null) { Fail(why); return; }
        if (!h.StashItem(it)) { Fail("無法存入。"); return; }
        Snd.Ui("item_pickup");
        Log.Debug("inv", $"stashed {it.Name}");
    }

    private void Withdraw(Item it)
    {
        var h = _hero;
        if (h == null) return;
        string why = h.WhyCantWithdraw(it);
        if (why != null) { Fail(why); return; }
        if (!h.FromStash(it)) { Fail("無法取回。"); return; }
        ItemMarks.See(it);
        Snd.Ui("item_pickup");
        Log.Debug("inv", $"withdrew {it.Name}");
    }

    // ═════════════════════════ smith: salvage ═════════════════════════

    private Control SalvageTab(Hero h)
    {
        var row = Columns();
        var left = Column(PickW);
        left.AddChild(Section($"行囊（{h.D.Bag.Count} / {Hero.BagSize}）"));
        left.AddChild(ItemUi.Well(ItemUi.BagGrid(_board, h, Cell, SalvageClick, SalvageClick, SalvageHint, DecorateSalvage, false)));
        left.AddChild(UiTheme.Label("快速選取", UiTheme.FsCaption, UiTheme.Dim));
        left.AddChild(ToolRow(
            ItemUi.Btn("廢品", () => PickWhere(h.IsJunk), UiTheme.FsRow),
            ItemUi.Btn("普通", () => PickWhere(i => i.Rarity == Rarity.Common), UiTheme.FsRow),
            ItemUi.Btn("魔法", () => PickWhere(i => i.Rarity == Rarity.Magic), UiTheme.FsRow),
            ItemUi.Btn("稀有", () => PickWhere(i => i.Rarity == Rarity.Rare), UiTheme.FsRow),
            ItemUi.Btn("清除選取", ClearPicks, UiTheme.FsRow)));
        left.AddChild(Note("點擊裝備以選取或取消。Shift＋左鍵：標記廢品。\n穿在身上的裝備需先卸下才能拆解。", PickW));
        row.AddChild(left);

        var right = Pane(row);
        var list = h.D.Bag.Where(_picked.Contains).ToList();
        var pre = Crafting.PreviewSalvage(list, h.D);
        var sb = new StringBuilder();
        if (pre.Count == 0)
        {
            sb.Append(ItemUi.Dim("尚未選取任何裝備。\n\n拆解會毀去裝備，換回碎鐵、幽塵與燼核。\n傳奇裝備的威能會被收進威能典籍——同一道威能只保留數值最好的一次——之後可以請祕術師薇菈銘刻到別的裝備上。"));
        }
        else
        {
            // a heading inside the running text: the scaled size is written into the tag here, once
            sb.Append($"[font_size={UiTheme.Fs(UiTheme.FsName)}]已選取 {pre.Count} 件[/font_size]\n\n");
            sb.Append(ItemUi.Dim("將會得到") + "\n");
            if (pre.Scrap > 0) sb.Append(ItemUi.Col(ItemUi.ScrapColor, $"{Crafting.ScrapName} ×{pre.Scrap}") + ItemUi.Dim($"　（持有 {Crafting.Num(h.D.Scrap)}）") + "\n");
            if (pre.Dust > 0) sb.Append(ItemUi.Col(ItemUi.DustColor, $"{Crafting.DustName} ×{pre.Dust}") + ItemUi.Dim($"　（持有 {Crafting.Num(h.D.Dust)}）") + "\n");
            if (pre.Cores > 0) sb.Append(ItemUi.Col(ItemUi.CoreColor, $"{Crafting.CoreName} ×{pre.Cores}") + ItemUi.Dim($"　（持有 {Crafting.Num(h.D.Cores)}）") + "\n");
            if (pre.Gems > 0) sb.Append($"鑲嵌的 {pre.Gems} 顆寶石會退回行囊\n");
            if (pre.Powers.Count > 0)
            {
                sb.Append("\n" + ItemUi.Dim("威能典籍") + "\n");
                foreach (var line in pre.Powers) sb.Append(line + "\n");
            }
            if (pre.Precious > 0) sb.Append("\n" + ItemUi.Col(Palette.Bad, $"其中 {pre.Precious} 件是傳奇或獨特裝備；動手之前會再問你一次。"));
        }
        right.AddChild(Section("拆解"));
        right.AddChild(FlexScroller("salvage", UiTheme.Rich(sb.ToString(), RightW - 24), RightW, 240));
        right.AddChild(ActionRow(ItemUi.Btn(pre.Count > 0 ? $"拆解 {pre.Count} 件" : "拆解", DoSalvage, UiTheme.FsBody), pre.Count == 0 ? "尚未選取裝備" : null));
        return row;
    }

    private void DecorateSalvage(Item it, SlotView v)
    {
        if (_picked.Contains(it)) ItemUi.Outline(v.Node, Palette.Ember, 3);
        else if (!it.IsEquip) ItemUi.Dimmed(v.Node, 0.5f);
    }

    private string SalvageHint(Item it)
    {
        if (!it.IsEquip) return ItemUi.Dim("寶石無法拆解");
        var y = ItemDb.SalvageYield(it);
        return ItemUi.Dim($"拆解可得：{Crafting.Mats(y.scrap, y.dust, y.cores)}") + "\n" +
               ItemUi.Dim(_picked.Contains(it) ? "左鍵：取消選取" : "左鍵：選取　Shift＋左鍵：標記廢品");
    }

    private void SalvageClick(Item it)
    {
        var h = _hero;
        if (h == null || !h.InBag(it)) return;
        if (Input.IsKeyPressed(Key.Shift))
        {
            h.ToggleJunk(it);
            Snd.Ui("ui_click");
            return;
        }
        if (!_picked.Remove(it))
        {
            string why = h.WhyCantSalvage(it);
            if (why != null) { Fail(why); return; }
            _picked.Add(it);
        }
        Snd.Ui("ui_click");
        _dirty = true;
    }

    private void PickWhere(Func<Item, bool> match)
    {
        var h = _hero;
        if (h == null) return;
        int n = 0;
        foreach (var it in h.D.Bag)
            if (match(it) && h.WhyCantSalvage(it) == null && _picked.Add(it)) n++;
        if (n == 0) Say("沒有更多符合的裝備。");
        _dirty = true;
    }

    private void ClearPicks()
    {
        _picked.Clear();
        _dirty = true;
    }

    private void DoSalvage()
    {
        var h = _hero;
        if (h == null) return;
        var list = h.D.Bag.Where(_picked.Contains).ToList();
        if (list.Count == 0) { Fail("尚未選取要拆解的裝備。"); return; }
        void Go()
        {
            var r = h.SalvageMany(list);
            _picked.Clear();
            _dirty = true;
            if (r.Count == 0) { Fail("沒有拆解任何物品。"); return; }
            Snd.Ui("salvage");
            string powers = r.Powers.Count > 0 ? $"　威能典籍收錄：{string.Join("、", r.Powers)}" : "";
            Say($"拆解了 {r.Count} 件裝備：{Crafting.Mats(r.Scrap, r.Dust, r.Cores)}{powers}", Palette.Good);
        }
        var precious = list.Where(Precious).ToList();
        if (precious.Count > 0)
        {
            string names = string.Join("、", precious.Take(6).Select(i => $"「{i.Name}」")) + (precious.Count > 6 ? "……" : "");
            Confirm($"即將拆解傳奇或獨特裝備：\n{names}\n拆解之後無法復原。確定嗎？", Go, "拆解");
        }
        else Go();
    }

    // ═════════════════════════ smith: masterworking ═════════════════════════

    private Control MasterworkTab(Hero h)
    {
        var row = Columns();
        row.AddChild(GearPicker(h, it => it.IsEquip ? null : "寶石無法精鑄。", Select));
        var right = Pane(row);
        var item = _sel;
        if (item == null || !item.IsEquip)
        {
            right.AddChild(Section("精鑄"));
            right.AddChild(Note("選擇一件裝備。\n\n每一級精鑄使所有詞綴的數值提高 3%，武器傷害與護甲提高 2.5%，最高 12 級。\n第 4、8、12 級是里程碑：隨機一條詞綴額外提升 25%。\n\n起初只需金幣與碎鐵；第 5 級起需要幽塵，第 9 級起需要燼核。", RightW));
            return row;
        }

        // the item card scrolls in the height left over by the rank, effect and cost lines, which always show
        right.AddChild(ItemBox(h, item, 150));
        right.AddChild(RankPips(item));
        var sb = new StringBuilder();
        sb.Append($"精鑄等級 {ItemUi.Col(UiTheme.Gold, item.Upgrade.ToString())} / {ItemDb.MaxUpgrade}\n");
        bool maxed = item.Upgrade >= ItemDb.MaxUpgrade;
        if (!maxed)
        {
            int next = item.Upgrade + 1;
            sb.Append($"第 {next} 級：所有詞綴數值 +3%，武器傷害與護甲 +2.5%\n");
            if (Crafting.IsMilestone(next)) sb.Append(ItemUi.Col(Palette.Ember, "里程碑：隨機一條詞綴額外提升 25%") + "\n");
            else sb.Append(ItemUi.Dim($"下一個里程碑在第 {(next / 4 + 1) * 4} 級：隨機一條詞綴額外提升 25%") + "\n");
        }
        else sb.Append(ItemUi.Col(Palette.Ember, "這件裝備已精鑄到極限。") + "\n");
        foreach (var a in item.Affixes)
        {
            int boosts = Crafting.Boosts(a);
            if (boosts > 0) sb.Append(ItemUi.Col(Palette.Ember, $"◆ {Crafting.AffixText(item, a)}　里程碑 ×{boosts}") + "\n");
        }
        right.AddChild(UiTheme.Rich(sb.ToString().TrimEnd('\n'), RightW));
        // the cost is a block of its own (as in the other crafting panes): the column's gap parts it from the lines above
        if (!maxed) right.AddChild(UiTheme.Rich(ItemUi.Costs(h.D, ItemDb.UpgradeGoldCost(item), ItemDb.UpgradeScrapCost(item), ItemDb.UpgradeDustCost(item), ItemDb.UpgradeCoreCost(item)), RightW));
        right.AddChild(ActionRow(ItemUi.Btn("精鑄", DoUpgrade, UiTheme.FsBody), h.WhyCantUpgrade(item)));
        return row;
    }

    /// <summary>
    /// The twelve masterwork ranks as a row of forged sockets: an ember stone in every rank reached, and the three
    /// milestones (4, 8, 12) as larger diamond mounts that hold a gold stone. Without the art: the flat pips.
    /// </summary>
    private static Control RankPips(Item it)
    {
        if (ItemUi.SocketArt)
        {
            const float small = 26f, big = 36f, gap = 4f;
            int ranks = ItemDb.MaxUpgrade, reached = it.Upgrade, miles = 0;
            for (int r = 1; r <= ranks; r++) if (Crafting.IsMilestone(r)) miles++;
            var strip = new Control
            {
                CustomMinimumSize = new Vector2(miles * big + (ranks - miles) * small + (ranks - 1) * gap, big),
                MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            };
            strip.Draw += () =>
            {
                float x = 0f;
                for (int r = 1; r <= ranks; r++)
                {
                    bool mile = Crafting.IsMilestone(r);
                    float s = mile ? big : small;
                    ItemUi.DrawSocket(strip, new Rect2(x, (big - s) * 0.5f, s, s), r <= reached, mile ? Palette.Gold : Palette.Ember, mile);
                    x += s + gap;
                }
            };
            return strip;
        }
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        for (int r = 1; r <= ItemDb.MaxUpgrade; r++)
        {
            bool mile = Crafting.IsMilestone(r), on = r <= it.Upgrade;
            int i = (on ? 1 : 0) + (mile ? 2 : 0);
            PipBoxes[i] ??= UiTheme.Box(on ? (mile ? Palette.Gold : Palette.Ember) : new Color(0.08f, 0.075f, 0.07f),
                mile ? Palette.Gold : new Color(0.36f, 0.31f, 0.26f), mile ? 2 : 1, 3);
            var p = new Panel
            {
                CustomMinimumSize = new Vector2(mile ? 34 : 24, mile ? 20 : 12),
                SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore,
            };
            p.AddThemeStyleboxOverride("panel", PipBoxes[i]);
            box.AddChild(p);
        }
        return box;
    }

    private void DoUpgrade()
    {
        var h = _hero; var it = _sel;
        if (h == null || it == null) return;
        string why = h.WhyCantUpgrade(it);
        if (why != null) { Fail(why); return; }
        int gold = ItemDb.UpgradeGoldCost(it);
        var before = new float[it.Affixes.Count];
        for (int i = 0; i < before.Length; i++) before[i] = it.Affixes[i].Bonus;
        if (!h.Upgrade(it)) { Fail("精鑄沒有成功。"); return; }
        Snd.Ui("anvil");
        Affix boosted = null;
        for (int i = 0; i < before.Length && i < it.Affixes.Count; i++)
            if (it.Affixes[i].Bonus > before[i] + 0.001f) boosted = it.Affixes[i];
        if (boosted != null) Say($"里程碑！「{StatInfo.Name(boosted.Stat)}」額外提升 25%（精鑄 +{it.Upgrade}）", Palette.Ember);
        else Say($"精鑄成功：「{it.Name}」+{it.Upgrade}", Palette.Good);
        Log.Info("craft", $"masterworked {it.Name} to rank {it.Upgrade} for {gold} gold{(boosted != null ? $", milestone boosted {boosted.Stat}" : "")}");
        _dirty = true;
    }

    // ═════════════════════════ smith: tempering ═════════════════════════

    private static string TemperFit(Item it) =>
        !it.IsEquip ? "寶石無法淬火。"
        : Crafting.TemperSlots(it) > 0 ? null
        : it.Rarity == Rarity.Unique ? "獨特裝備已成定局，無法淬火。" : "只有稀有或傳奇裝備承受得住淬火。";

    private Control TemperTab(Hero h)
    {
        var row = Columns();
        row.AddChild(GearPicker(h, TemperFit, Select));
        var right = Pane(row);
        var item = _sel;
        if (item == null || Crafting.TemperSlots(item) == 0)
        {
            right.AddChild(Section("淬火"));
            right.AddChild(Note("選擇一件稀有或傳奇裝備。\n\n淬火把一條額外的詞綴打進裝備：你挑選配方——也就是詞綴的類別——結果在該類別中隨機決定，數值隨物品強度而定。\n稀有裝備承受得住一道淬火詞綴，傳奇裝備兩道。\n每件裝備只有 5 次淬火機會。對同一淬位再次淬火，會取代原本的結果。", RightW));
            return row;
        }

        int slots = Crafting.TemperSlots(item), filled = Crafting.TemperedCount(item);
        _temperSlot = Math.Clamp(_temperSlot, 0, Math.Min(slots - 1, filled));
        var recipes = Crafting.RecipesFor(h.Class);
        var cur = Crafting.Recipe(_recipe);
        if (cur == null || !cur.ForClass(h.Class) || !cur.Allows(item.Kind))
        {
            cur = recipes.FirstOrDefault(r => r.Allows(item.Kind));
            _recipe = cur?.Id;
        }

        var head = new HBoxContainer();
        head.AddChild(NameLabel(item));
        var charges = UiTheme.Label($"淬火次數 {Math.Max(0, item.TemperLeft)} / {Crafting.TemperCharges}", UiTheme.FsRow, item.TemperLeft > 0 ? UiTheme.Text : Palette.Bad);
        charges.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(charges);
        right.AddChild(head);

        for (int s = 0; s < slots && s < TemperSlotNames.Length; s++)
        {
            int slot = s;
            int idx = Crafting.TemperedIndex(item, s);
            string what = idx >= 0 ? Crafting.AffixText(item, item.Affixes[idx]) : s <= filled ? "尚未淬火" : "需先淬鍊前一淬位";
            var b = ItemUi.Btn($"{TemperSlotNames[s]}　{what}", () => PickTemperSlot(slot), UiTheme.FsRow);
            b.Alignment = HorizontalAlignment.Left;
            b.Disabled = s > filled;
            ItemUi.Option(b, s == _temperSlot);
            right.AddChild(b);
        }

        right.AddChild(Section("配方"));
        // the recipes as compact chips that share one row: seven three-glyph names fit the pane at every text size,
        // which leaves the height of a second row to the list of results below (more or longer names would wrap)
        var chips = new HFlowContainer();
        chips.AddThemeConstantOverride("h_separation", 6);
        chips.AddThemeConstantOverride("v_separation", 6);
        foreach (var r in recipes)
        {
            var rec = r;
            var b = ItemUi.Btn(rec.Name, () => PickRecipe(rec), UiTheme.FsRow);
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            ItemUi.Option(b, rec.Id == _recipe, 12f);
            if (!rec.Allows(item.Kind)) b.Modulate = new Color(1, 1, 1, 0.45f);
            chips.AddChild(b);
        }
        right.AddChild(chips);

        var sb = new StringBuilder();
        if (cur != null)
        {
            sb.Append(ItemUi.Col(UiTheme.Gold, cur.Name) + "　" + ItemUi.Dim(cur.Desc) + "\n");
            sb.Append(ItemUi.Dim("可能淬得（隨機其一）") + "\n");
            var open = Crafting.TemperCandidates(cur, item, Crafting.TemperedIndex(item, _temperSlot));
            foreach (var stat in cur.Stats)
            {
                var (lo, hi) = Crafting.TemperRange(stat, item);
                string range = StatInfo.IsPct(stat) ? $"+{lo:0.#}–{hi:0.#}% {StatInfo.Name(stat)}" : $"+{lo:0}–{hi:0} {StatInfo.Name(stat)}";
                sb.Append(open.Contains(stat) ? $"◆ {range}\n" : ItemUi.Dim($"◇ {range}（裝備上已有）") + "\n");
            }
        }
        else sb.Append(ItemUi.Col(Palette.Bad, "沒有適用於這類裝備的配方。") + "\n");
        // the list of possible results scrolls, in its well, in the height that is left; the cost stays in view above the button
        right.AddChild(FlexWell("recipe:" + _recipe, UiTheme.Rich(sb.ToString().TrimEnd('\n'), RightW - 24 - ItemUi.WellPad), RightW, 80, out _));
        right.AddChild(UiTheme.Rich(ItemUi.Costs(h.D, Crafting.TemperGoldCost(item), Crafting.TemperScrapCost(item), Crafting.TemperDustCost(item), Crafting.TemperCoreCost(item, _temperSlot)), RightW));

        bool again = Crafting.TemperedIndex(item, _temperSlot) >= 0;
        right.AddChild(ActionRow(ItemUi.Btn(again ? "重新淬火" : "淬火", DoTemper, UiTheme.FsBody), h.WhyCantTemper(item, _recipe, _temperSlot)));
        return row;
    }

    private void PickTemperSlot(int slot)
    {
        _temperSlot = slot;
        _dirty = true;
    }

    private void PickRecipe(TemperRecipe rec)
    {
        var it = _sel;
        if (it != null && !rec.Allows(it.Kind)) { Fail($"「{rec.Name}」無法用於{Names.Kind(it.Kind)}。"); return; }
        _recipe = rec.Id;
        _dirty = true;
    }

    private void DoTemper()
    {
        var h = _hero; var it = _sel;
        if (h == null || it == null) return;
        string why = h.WhyCantTemper(it, _recipe, _temperSlot);
        if (why != null) { Fail(why); return; }
        var a = h.Temper(it, _recipe, _temperSlot);
        if (a == null) { Fail("這一次沒有淬成。"); return; }
        Snd.Ui("anvil");
        Say($"淬得：{Crafting.AffixText(it, a)}{(a.Greater ? "（卓越）" : "")}　剩餘 {Math.Max(0, it.TemperLeft)} 次", a.Greater ? Palette.Ember : Palette.Good);
        _dirty = true;
    }

    // ═════════════════════════ occultist: enchant ═════════════════════════

    private Control EnchantTab(Hero h)
    {
        var row = Columns();
        row.AddChild(GearPicker(h, it => h.WhyCantEnchant(it, -1, false), Select));
        var right = Pane(row);

        if (_cands != null && _candItem != null)
        {
            EnchantChoice(right);
            return row;
        }

        var item = _sel;
        if (item == null || h.WhyCantEnchant(item, -1, false) != null)
        {
            right.AddChild(Section("附魔"));
            right.AddChild(Note("選擇一件裝備，再選一條要重鑄的詞綴。\n\n薇菈會抽走這條詞綴，換上兩道新的可能，由你擇一，或是保留原樣。\n同一件裝備每附魔一次，費用提高五成。\n淬火詞綴不在此列；獨特裝備無法附魔。", RightW));
            return row;
        }
        if (_affixIdx >= item.Affixes.Count || (_affixIdx >= 0 && item.Affixes[_affixIdx].Tempered)) _affixIdx = -1;

        right.AddChild(NameLabel(item));
        right.AddChild(UiTheme.Label("選擇要重鑄的詞綴", UiTheme.FsRow, UiTheme.Dim));
        // the affix list scrolls in the height that is left, so the cost and the button stay in view however
        // many affixes the item carries and whatever the text size
        var affixes = new VBoxContainer();
        affixes.AddThemeConstantOverride("separation", 4);
        for (int i = 0; i < item.Affixes.Count; i++)
        {
            int idx = i;
            var a = item.Affixes[i];
            var b = ItemUi.Btn((a.Tempered ? "淬火　" : "◆ ") + Crafting.AffixText(item, a), () => PickAffix(idx), UiTheme.FsRow);
            b.Alignment = HorizontalAlignment.Left;
            b.Disabled = a.Tempered;
            ItemUi.Option(b, i == _affixIdx);
            affixes.AddChild(b);
        }
        right.AddChild(FlexWell("affixes:" + item.Uid, affixes, RightW, 130, out _));
        int times = h.D.Enchants.GetValueOrDefault(item.Uid);
        var sb = new StringBuilder();
        sb.Append(ItemUi.Costs(h.D, h.EnchantCost(item), 0, ItemDb.EnchantDustCost) + "\n");
        sb.Append(ItemUi.Dim(times > 0 ? $"這件裝備已附魔 {times} 次。" : "這件裝備還沒有附魔過。"));
        right.AddChild(UiTheme.Rich(sb.ToString(), RightW));
        string why = _affixIdx < 0 ? "請先選擇一條詞綴" : h.WhyCantEnchant(item, _affixIdx);
        right.AddChild(ActionRow(ItemUi.Btn("重鑄詞綴", DoEnchant, UiTheme.FsBody), why));
        return row;
    }

    private void EnchantChoice(VBoxContainer right)
    {
        var item = _candItem;
        var old = item.Affixes[_candIdx];
        right.AddChild(Section("附魔：擇一"));
        right.AddChild(NameLabel(item));
        right.AddChild(Note("祕術已成，費用已付。選擇要留下的詞綴：", RightW, UiTheme.Text));
        var keep = ItemUi.Btn($"保留原樣　{Crafting.AffixText(item, old)}", KeepEnchant, UiTheme.FsRow);
        keep.Alignment = HorizontalAlignment.Left;
        right.AddChild(keep);
        foreach (var c in _cands)
        {
            var cand = c;
            // shown as it will count once it sits in this slot (masterworking and milestone boosts carry over)
            float v = cand.Stat == Stat.SkillRanks ? cand.Value : cand.Value * (item.AffixMul + old.Bonus);
            var b = ItemUi.Btn($"換成　{StatInfo.Format(cand.Stat, v)}{(cand.Greater ? "（卓越）" : "")}", () => TakeEnchant(cand), UiTheme.FsRow);
            b.Alignment = HorizontalAlignment.Left;
            ItemUi.Tone(b, true);
            right.AddChild(b);
        }
        right.AddChild(Note("若就此離開，原本的詞綴會保留。", RightW));
    }

    private void PickAffix(int idx)
    {
        _affixIdx = idx;
        _dirty = true;
    }

    private void DoEnchant()
    {
        var h = _hero; var it = _sel;
        if (h == null || it == null) return;
        string why = _affixIdx < 0 ? "請先選擇一條詞綴。" : h.WhyCantEnchant(it, _affixIdx);
        if (why != null) { Fail(why); return; }
        var cands = h.BeginEnchant(it, _affixIdx);
        if (cands == null) { Fail("祕術沒有回應。"); return; }
        Snd.Ui("enchant");
        if (cands.Length == 0)
        {
            Say("沒有別的詞綴可以換上；原本的詞綴保留。", Palette.Bad);
            _dirty = true;
            return;
        }
        _cands = cands; _candItem = it; _candIdx = _affixIdx;
        Say("選擇要留下的詞綴。");
        _dirty = true;
    }

    private void TakeEnchant(Affix cand)
    {
        var h = _hero; var it = _candItem;
        if (h == null || it == null || _cands == null) return;
        int idx = _candIdx;
        _cands = null; _candItem = null; _affixIdx = -1;
        if (!h.Owns(it) || idx < 0 || idx >= it.Affixes.Count) { Fail("這件裝備已不在你身上。"); _dirty = true; return; }
        h.CommitEnchant(it, idx, cand);
        Snd.Ui("enchant");
        Say($"詞綴已換成：{Crafting.AffixText(it, it.Affixes[idx])}", Palette.Good);
        _dirty = true;
    }

    private void KeepEnchant()
    {
        _cands = null; _candItem = null; _affixIdx = -1;
        Say("保留了原本的詞綴。");
        Log.Info("craft", "enchant: original affix kept");
        _dirty = true;
    }

    // ═════════════════════════ occultist: imprint ═════════════════════════

    private static string ImprintFit(Item it, AspectDef asp)
    {
        if (!it.IsEquip) return "寶石無法銘刻。";
        if (it.Rarity != Rarity.Rare && it.Rarity != Rarity.Legendary) return "只能銘刻於稀有或傳奇裝備。";
        if (asp != null && Array.IndexOf(asp.Kinds, it.Kind) < 0) return $"「{asp.Name}」無法銘刻於{Names.Kind(it.Kind)}。";
        return null;
    }

    private Control ImprintTab(Hero h)
    {
        var row = Columns();
        var left = Column(CodexW);
        // what the hero has recorded comes first (the order within each half stays as it is in the book)
        var usable = ItemDb.Aspects.Where(a => a.Class < 0 || a.Class == (int)h.Class).OrderBy(a => h.D.Codex.ContainsKey(a.Id) ? 0 : 1).ToList();
        int known = usable.Count(a => h.D.Codex.ContainsKey(a.Id));
        left.AddChild(Section($"威能典籍（{known} / {usable.Count}）"));
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 3);
        foreach (var a in usable)
        {
            var asp = a;
            bool has = h.D.Codex.TryGetValue(asp.Id, out float best);
            var b = ItemUi.Btn(has ? $"✹ {asp.Name}　{asp.Value(best):0}" : $"{asp.Name}　未收錄", () => PickAspect(asp.Id), UiTheme.FsRow);
            b.Alignment = HorizontalAlignment.Left;
            ItemUi.Option(b, asp.Id == _aspect);
            if (!has) b.Modulate = new Color(1, 1, 1, 0.5f);
            list.AddChild(b);
        }
        // the codex list, in its well, takes the height left above the description pane, and both scroll
        left.AddChild(FlexWell("codex", list, CodexW, 180, out _));

        var cur = ItemDb.Aspect(_aspect);
        var sb = new StringBuilder();
        if (cur == null)
        {
            sb.Append(ItemUi.Dim("從典籍中選擇一道威能。\n拆解傳奇裝備時，它的威能會被收錄；同一道威能只保留數值最好的一次。"));
        }
        else
        {
            bool has = h.D.Codex.TryGetValue(cur.Id, out float best);
            sb.Append(ItemUi.Col(Palette.Rarity(Rarity.Legendary), cur.Name) + "\n");
            sb.Append(has ? string.Format(cur.Desc, $"{cur.Value(best):0}") : string.Format(cur.Desc, $"{cur.Value(0f):0}–{cur.Value(1f):0}"));
            sb.Append("\n" + ItemUi.Dim("可銘刻於：" + string.Join("、", cur.Kinds.Select(k => Names.Kind(k)))));
            bool scales = cur.Id != "multishot" && (Array.IndexOf(cur.Kinds, ItemKind.Weapon) >= 0 || Array.IndexOf(cur.Kinds, ItemKind.Amulet) >= 0);
            if (scales) sb.Append("\n" + ItemUi.Dim("刻在護符上，數值為 1.5 倍；刻在武器上，為 2 倍。"));
            if (!has) sb.Append("\n" + ItemUi.Col(Palette.Bad, "尚未收錄：拆解帶有此威能的傳奇裝備即可取得。"));
        }
        // what the chosen power does: a sub-panel of fixed height under the list (its text scrolls)
        var about = UiTheme.Frame(false);
        about.MouseFilter = MouseFilterEnum.Pass;
        float aboutW = CodexW - PanePad;
        float aboutH = UiTheme.Px(230) - UiTheme.PanelBox.GetMargin(Side.Top) - UiTheme.PanelBox.GetMargin(Side.Bottom);
        about.AddChild(Scroller("aspect:" + _aspect, UiTheme.Rich(sb.ToString(), aboutW - 24), new Vector2(aboutW, aboutH)));
        left.AddChild(about);
        row.AddChild(left);
        row.AddChild(ItemUi.VRule());

        var right = Column(PickW);
        right.AddChild(GearPicker(h, it => ImprintFit(it, cur), Select));
        var item = _sel;
        var act = new StringBuilder();
        string why;
        if (cur == null) why = "請先從典籍中選擇一道威能";
        else if (item == null) why = "請選擇要銘刻的裝備";
        else
        {
            why = ImprintFit(item, cur) ?? h.WhyCantImprint(item, cur.Id);
            act.Append($"將 {ItemUi.Col(Palette.Rarity(Rarity.Legendary), cur.Name)} 銘刻於 {ItemUi.Col(Palette.Rarity(item.Rarity), item.Name)}\n");
            if (item.Aspect != null && item.Aspect != cur.Id)
                act.Append(ItemUi.Col(Palette.Bad, $"原有的威能「{ItemDb.Aspect(item.Aspect)?.Name ?? item.Aspect}」會被永久抹去。") + "\n");
            else if (item.Aspect == cur.Id)
                act.Append(ItemUi.Dim("這件裝備已有此威能；重新銘刻會改用典籍中的數值。") + "\n");
            if (item.Rarity == Rarity.Rare) act.Append(ItemUi.Dim("稀有裝備將升為傳奇，並多出一道淬火位。") + "\n");
            act.Append(ItemUi.Costs(h.D, ItemDb.ImprintGoldCost(item), 0, 0, 1));
        }
        // what the imprint will do, in the height the gear picker leaves (it scrolls when the lines need more);
        // the pane is there even while it is empty, so the button keeps its place at the bottom of the column
        right.AddChild(FlexScroller("imprint", UiTheme.Rich(act.ToString(), PickW - 24), PickW, 100));
        right.AddChild(ActionRow(ItemUi.Btn("銘刻", DoImprint, UiTheme.FsBody), why));
        row.AddChild(right);
        return row;
    }

    private void PickAspect(string id)
    {
        _aspect = id;
        _dirty = true;
    }

    private void DoImprint()
    {
        var h = _hero; var it = _sel;
        var asp = ItemDb.Aspect(_aspect);
        if (h == null) return;
        if (asp == null) { Fail("請先從典籍中選擇一道威能。"); return; }
        if (it == null) { Fail("請選擇要銘刻的裝備。"); return; }
        string why = ImprintFit(it, asp) ?? h.WhyCantImprint(it, asp.Id);
        if (why != null) { Fail(why); return; }
        void Go()
        {
            if (!h.Owns(it)) { Fail("這件裝備已不在你身上。"); return; }
            if (!h.Imprint(it, asp.Id)) { Fail(h.WhyCantImprint(it, asp.Id) ?? "銘刻沒有成功。"); return; }
            Snd.Ui("enchant");
            Say($"「{asp.Name}」已銘刻於「{it.Name}」。", Palette.Rarity(Rarity.Legendary));
            Log.Info("craft", $"imprinted {asp.Id}@{it.AspectRoll:0.00} on {it.Name} (power {it.Power}); gold={h.D.Gold} cores={h.D.Cores}");
            _dirty = true;
        }
        if (it.Aspect != null && it.Aspect != asp.Id)
            Confirm($"「{it.Name}」原有的威能「{ItemDb.Aspect(it.Aspect)?.Name ?? it.Aspect}」會被永久抹去，\n換成「{asp.Name}」。確定要銘刻嗎？", Go, "銘刻");
        else Go();
    }

    // ═════════════════════════ jeweler ═════════════════════════

    private static string GemEffect(Item gem, Item target)
    {
        var g = ItemDb.Gem(gem.GemId);
        if (g == null || target == null) return null;
        var b = ItemDb.GemBonus(g, target.Kind);
        return ItemUi.Col(g.Color, $"嵌入{Names.Kind(target.Kind)}：{StatInfo.Format(b.s, b.v)}");
    }

    private Control SocketTab(Hero h)
    {
        var row = Columns();
        row.AddChild(GearPicker(h, null, SocketPick));
        var right = Pane(row);
        var item = _sel;
        if (item == null || !item.IsEquip)
        {
            right.AddChild(Section("鑲嵌"));
            right.AddChild(Note("選擇一件裝備。\n\n武器與胸甲最多兩個插槽；頭盔、護腿、護符與戒指一個；手套與靴子無法開鑿。\n同一顆寶石嵌在武器、防具或飾品上，效果各不相同。\n取下寶石不需要費用，寶石也不會損壞。", RightW));
            return row;
        }

        int max = Hero.MaxSockets(item.Kind);
        var head = new HBoxContainer();
        head.AddChild(NameLabel(item));
        var count = UiTheme.Label($"插槽 {item.Sockets} / {max}", UiTheme.FsRow, UiTheme.Text);
        count.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(count);
        right.AddChild(head);

        if (item.Sockets > 0)
        {
            var sockets = new HBoxContainer();
            sockets.AddThemeConstantOverride("separation", 8);
            for (int i = 0; i < item.Sockets; i++)
            {
                var def = i < item.Gems.Count ? ItemDb.Gem(item.Gems[i]) : null;
                if (def != null)
                {
                    var set = ItemDb.MakeGem(def.Id);
                    sockets.AddChild(ItemUi.Slot(_board, set, Cell, null, null, x => GemEffect(x, item), false).Node);
                }
                else
                {
                    var empty = ItemUi.EmptySocket(Cell);
                    _board.Add(new SlotView { Node = empty, Text = "空插槽\n" + ItemUi.Dim("點擊下方的寶石即可嵌入。"), Compare = false, Glow = false });
                    sockets.AddChild(empty);
                }
            }
            right.AddChild(sockets);
        }
        else right.AddChild(Note(max > 0 ? "這件裝備還沒有插槽。" : $"{Names.Kind(item.Kind)}無法開鑿插槽。", RightW));

        if (item.Sockets < max) right.AddChild(UiTheme.Rich(ItemUi.Costs(h.D, Hero.SocketGoldCost, 0, Hero.SocketDustCost), RightW));
        right.AddChild(ActionRow(ItemUi.Btn("開鑿插槽", DoAddSocket, UiTheme.FsRow), h.WhyCantAddSocket(item)));
        right.AddChild(ActionRow(ItemUi.Btn("取下所有寶石", DoUnsocket, UiTheme.FsRow), h.WhyCantUnsocket(item)));

        right.AddChild(Section("行囊中的寶石"));
        var gems = h.D.Bag.Where(i => i.Kind == ItemKind.Gem).ToList();
        if (gems.Count == 0) right.AddChild(Note("行囊裡沒有寶石。", RightW));
        else
        {
            bool free = item.Gems.Count < item.Sockets;
            // a bag full of gems is four rows: the grid scrolls in the height left under the buttons (the cells
            // know their scroll area, so one that is scrolled out of sight cannot answer the pointer)
            var grid = ItemUi.Grid(10);
            var well = FlexWell("gems", grid, RightW, Cell + 8, out var scroll);
            foreach (var gem in gems)
            {
                var view = ItemUi.Slot(_board, gem, Cell, SocketGem, SocketGem, x => (GemEffect(x, item) ?? "") + "\n" + ItemUi.Dim("點擊：嵌入所選的裝備"), false, scroll);
                if (!free) ItemUi.Dimmed(view.Node, 0.45f);
                grid.AddChild(view.Node);
            }
            right.AddChild(well);
        }
        return row;
    }

    private void SocketPick(Item it)
    {
        if (it.Kind != ItemKind.Gem) { Select(it); return; }
        if (_sel == null) { Fail("請先選擇要鑲嵌的裝備。"); return; }
        SocketGem(it);
    }

    private void SocketGem(Item gem)
    {
        var h = _hero; var target = _sel;
        if (h == null) return;
        string why = h.WhyCantSocket(gem, target);
        if (why != null) { Fail(why); return; }
        if (!h.Socket(gem, target)) { Fail("無法鑲嵌。"); return; }
        Snd.Ui("gem_socket");
        Say($"「{gem.Name}」已嵌入「{target.Name}」。", Palette.Good);
        Log.Info("craft", $"socketed {gem.GemId} into {target.Name} ({target.Gems.Count}/{target.Sockets})");
        _dirty = true;
    }

    private void DoAddSocket()
    {
        var h = _hero; var it = _sel;
        if (h == null || it == null) return;
        string why = h.WhyCantAddSocket(it);
        if (why != null) { Fail(why); return; }
        if (!h.AddSocket(it)) { Fail("開鑿沒有成功。"); return; }
        Snd.Ui("anvil");
        Say($"在「{it.Name}」上開鑿了一個插槽。", Palette.Good);
        Log.Info("craft", $"socket added to {it.Name} ({it.Sockets}); gold={h.D.Gold} dust={h.D.Dust}");
        _dirty = true;
    }

    private void DoUnsocket()
    {
        var h = _hero; var it = _sel;
        if (h == null || it == null) return;
        string why = h.WhyCantUnsocket(it);
        if (why != null) { Fail(why); return; }
        int n = it.Gems.Count;
        if (!h.Unsocket(it)) { Fail("無法取下寶石。"); return; }
        Snd.Ui("gem_socket");
        Say($"從「{it.Name}」取下了 {n} 顆寶石。");
        Log.Info("craft", $"unsocketed {n} gems from {it.Name}");
        _dirty = true;
    }

    private Control CombineTab(Hero h)
    {
        // the name and cost columns of a row grow with the text, and the list column with them (ListW)
        float listW = ListW;
        var row = Columns();
        var left = Column(listW);
        left.AddChild(Section("合成"));
        left.AddChild(Note($"{Crafting.GemsPerCombine} 顆同種、同階的寶石，可以熔成更高一階的一顆。只計算行囊中尚未鑲嵌的寶石。", listW));
        // every recipe on its plate, in a well. A line holds two cells, two text columns (198 and 180 design px,
        // scaled with the text: the longest gem name with its count, the dearest cost), the arrow and the button:
        // 642 px at 標準, inside the 652 the plate, the well and the scroll bar leave of the list column
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 2);
        var well = FlexWell("combine", list, listW, 240, out var scroll);
        foreach (var g in ItemDb.Gems)
        {
            var next = Crafting.NextGem(g.Id);
            if (next == null) continue;
            string id = g.Id;
            int have = h.GemCount(id);
            int gold = Crafting.GemCombineGold(g), dust = Crafting.GemCombineDust(g);
            string why = h.WhyCantCombineGems(id);
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 10);
            var from = ItemUi.Slot(_board, ItemDb.MakeGem(id), GemCell, null, null, null, false, scroll).Node;
            from.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            line.AddChild(from);
            var count = UiTheme.Trimmed(UiTheme.Label($"{g.Name} ×{have}", UiTheme.FsRow, have >= Crafting.GemsPerCombine ? UiTheme.Text : UiTheme.Dim), false);
            UiTheme.MinSize(count, 198, 0);
            count.VerticalAlignment = VerticalAlignment.Center;
            line.AddChild(count);
            var arrow = UiTheme.Label("→", UiTheme.FsRow, UiTheme.Dim);
            arrow.VerticalAlignment = VerticalAlignment.Center;
            line.AddChild(arrow);
            var to = ItemUi.Slot(_board, ItemDb.MakeGem(next.Id), GemCell, null, null, null, false, scroll).Node;
            to.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            line.AddChild(to);
            bool afford = h.D.Gold >= gold && h.D.Dust >= dust;
            var cost = UiTheme.Label($"{Crafting.Num(gold)} 金幣" + (dust > 0 ? $"　{Crafting.DustName} {dust}" : ""), UiTheme.FsRow, afford ? Palette.Gold : Palette.Bad);
            UiTheme.MinSize(cost, 180, 0);
            cost.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            cost.HorizontalAlignment = HorizontalAlignment.Right;
            cost.VerticalAlignment = VerticalAlignment.Center;
            line.AddChild(cost);
            var b = ItemUi.Btn("合成", () => DoCombine(id), UiTheme.FsRow);
            UiTheme.MinSize(b, 88, 0);
            b.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            b.Disabled = why != null;
            line.AddChild(b);
            list.AddChild(RowPlate(line, why != null));
        }
        left.AddChild(well);
        row.AddChild(left);
        row.AddChild(ItemUi.VRule());
        row.AddChild(BagView(h));
        return row;
    }

    private void DoCombine(string gemId)
    {
        var h = _hero;
        if (h == null) return;
        string why = h.WhyCantCombineGems(gemId);
        if (why != null) { Fail(why); return; }
        var made = h.CombineGems(gemId);
        if (made == null) { Fail("合成沒有成功。"); return; }
        Snd.Ui("gem_socket");
        Say($"熔成了「{made.Name}」。", Palette.Good);
        _dirty = true;
    }

    private List<(Item item, int price)> GemStock(Hero h)
    {
        var list = SafeStock("jeweler", h).Where(e => e.item != null && e.item.Kind == ItemKind.Gem).ToList();
        if (list.Count > 0) return list;
        // nothing on offer from the loot tables: the jeweler falls back to a standing stock of basic gems
        _ownGems ??= Crafting.GemStock(h.D.Level);
        for (int i = 0; i < _ownGems.Count; i++)
        {
            var e = _ownGems[i];
            if (h.D.Bag.Contains(e.item) || h.D.Stash.Contains(e.item)) _ownGems[i] = (ItemDb.MakeGem(e.item.GemId), e.price);
        }
        return _ownGems;
    }

    private Control GemShopTab(Hero h)
    {
        var row = Columns();
        var left = Column(PickW);
        left.AddChild(Section("寶石"));
        var stock = GemStock(h);
        if (stock.Count == 0) left.AddChild(Note("今天沒有寶石可賣。", PickW));
        else left.AddChild(StockGrid(h, stock));
        left.AddChild(Note("嵌在武器上偏向殺伐，嵌在防具上偏向存活，嵌在飾品上則是抗性。\n較高階的寶石，可以在「合成」中以三顆低階寶石熔成。", PickW));
        row.AddChild(left);
        row.AddChild(ItemUi.VRule());
        row.AddChild(BagView(h));
        return row;
    }
}
