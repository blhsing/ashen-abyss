using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// 燼紋盤 — full-screen panel (id "paragon"). The attached boards are drawn with custom drawing on a canvas that
/// can be dragged and zoomed; left click lights a node, right click puts it out. A lit gate shows an empty slot
/// beyond it: clicking that opens the board picker. The right-hand column lists the glyphs (socket, level,
/// tempering attempts earned in rifts). While the hero is below the level cap the panel only says what is needed.
///
/// Text: every size goes through UiTheme.Fs exactly once (the UiTheme factories do it themselves). The canvas draws
/// its labels in screen space, so zooming out never shrinks them below the floor: a label that no longer fits at the
/// current zoom is left out instead (see the "text on the canvas" notes above DrawAll). The top bar, the glyph
/// column and the two centred boxes take their sizes from the text (Relayout).
///
/// Chrome: the boards, their nodes, the plates under text on the canvas and the list entries are drawn from the
/// raster interface art (UiArt: forged sub-panel frames, enamel-and-rim node sprites, plaques, key caps, cards);
/// connectors, glyph-reach rings, auras and the hover / selection rings stay drawn lines. Without the art every one
/// of them falls back to the vector shape or flat box it had before.
/// </summary>
public partial class ParagonUi : UiPanel
{
    public override string Id => "paragon";
    public override bool Fullscreen => true;

    private const float Cell = 46f;                              // one board cell at zoom 1, in UI pixels
    private const int Pitch = PBoard.Size + 2;                   // lattice pitch in cells: two cells of air between boards
    private const float BoardPx = PBoard.Size * Cell;
    private const float SideDesign = 520f;                       // width of the glyph column at 標準 text (see Relayout)
    private const float TopMin = 72f, Edge = 12f;                // least height of the top bar; gap to the screen edge
    private const float ZoomMin = 0.3f, ZoomMax = 1.7f;
    private const string HintText = "左鍵　點亮／選定　　右鍵　熄滅／取下　　拖曳　移動盤面　　滾輪　縮放";
    private const string GatePrompt = "＋ 接續燼紋盤";
    private const string EmptyBoardNote = "　空盤：右鍵點擊盤面空白處可卸除";

    /// <summary>Width of the manager's tooltip card: its text column plus the padding of the card.</summary>
    private static float TipW => UiTheme.TipWidth + 30f;

    private static readonly Color BgCol = new(0.028f, 0.026f, 0.03f, 0.985f);
    private static readonly Color PlateCol = new(0.028f, 0.026f, 0.03f, 0.94f);
    private static readonly Color LinkDark = new(0.2f, 0.19f, 0.18f);
    private static readonly Color LinkNear = new(0.5f, 0.42f, 0.24f);
    private static readonly Color LitRim = new(1f, 0.93f, 0.78f);
    private static readonly Color DarkRim = new(0.3f, 0.29f, 0.27f);
    private static readonly Color CoreDark = new(0.05f, 0.045f, 0.04f);

    // ── raster art (res://assets/ui, see UiArt): looked up once, the node pieces into arrays indexed by PNodeType
    //    (Start, Normal, Magic, Rare, Legendary, Socket, Gate). A null entry means "not installed": the vector
    //    shapes and flat boxes are drawn instead. Nothing below is looked up or built while drawing. ──
    private static bool _artLooked;
    private static readonly UiArt.Piece[] PnRim = new UiArt.Piece[7], PnLit = new UiArt.Piece[7], PnFill = new UiArt.Piece[7];
    private static UiArt.Piece _aBoard, _aPlaque, _fBoard, _aBead, _aLozenge, _aKeycap;
    /// <summary>
    /// Half the logical box of a node sprite, in cells. The pieces are authored in a 48 px box around a shape of
    /// nominal radius 17 (rings, the gate's half side), 19 (socket), 20.5 (rhombus points) or 21.5 (star points);
    /// the box is scaled so that this radius lands where the vector shape of that type always had it.
    /// </summary>
    private static readonly float[] PnHalf = { 0.5647f, 0.2824f, 0.3311f, 0.447f, 0.4894f, 0.4547f, 0.4235f };
    /// <summary>How far the forged shape of a type reaches from its centre, in cells: the hover ring goes just outside it.</summary>
    private static readonly float[] PnRing = { 0.53f, 0.2f, 0.283f, 0.382f, 0.44f, 0.36f, 0.39f };
    /// <summary>The plaques under text on the canvas are held back a little: dim and coloured lines keep their contrast.</summary>
    private static readonly Color PlaqueMod = new(0.7f, 0.68f, 0.68f);
    /// <summary>An enamel bead (the round body piece) of radius r takes a box of half r × this.</summary>
    private const float BeadBox = 24f / 13.5f, LozengeBox = 24f / 15.5f;

    private static void LookArt()
    {
        if (_artLooked) return;
        _artLooked = true;
        if (!UiTheme.Art) return;
        string[] kinds = { "start", "normal", "magic", "rare", "legend", "socket", "gate" };
        for (int i = 0; i < kinds.Length; i++)
        {
            PnRim[i] = UiArt.P("pn_" + kinds[i]);
            PnLit[i] = UiArt.P("pn_" + kinds[i] + "_lit");
            PnFill[i] = UiArt.P("pn_" + kinds[i] + "_fill");
            // a type is drawn from the art only when its three pieces are all there
            if (PnRim[i] == null || PnLit[i] == null || PnFill[i] == null) PnRim[i] = PnLit[i] = PnFill[i] = null;
        }
        _aBead = PnFill[(int)PNodeType.Normal];
        _aLozenge = PnFill[(int)PNodeType.Magic];
        _aBoard = UiArt.P("panel");
        _fBoard = UiArt.Field("soot");
        _aPlaque = UiArt.P("plaque");
        _aKeycap = UiArt.P("keycap");
    }

    // The hint under the boards, with the art: the same words as HintText, each input on a key cap, on a plaque (the
    // boards pass underneath it). Widths are measured once per font size.
    private static readonly string[] HintKeys = { "左鍵", "右鍵", "拖曳", "滾輪" }, HintActs = { "點亮／選定", "熄滅／取下", "移動盤面", "縮放" };
    private const float HintKeyPad = 9f, HintKeyGap = 8f, HintPairGap = 22f, HintPlatePad = 14f, HintLift = 9f;
    private readonly float[] _hintKeyW = new float[HintKeys.Length], _hintActW = new float[HintKeys.Length];
    private int _hintFs = -1;
    private float _hintW;

    /// <summary>
    /// Draws the hint line as key caps on a plaque; <paramref name="top"/> and <paramref name="baseline"/> are those
    /// of its text line. False (nothing drawn) when the pieces are missing or the canvas is too narrow for it: the
    /// caller then draws the plain line.
    /// </summary>
    private bool DrawHintCaps(int fs, float top, float baseline, float canvasW)
    {
        if (_aKeycap == null || _aPlaque == null) return false;
        if (fs != _hintFs)
        {
            _hintFs = fs;
            _hintW = 0f;
            for (int i = 0; i < HintKeys.Length; i++)
            {
                _hintKeyW[i] = _fontUi.GetStringSize(HintKeys[i], HorizontalAlignment.Left, -1f, fs).X;
                _hintActW[i] = _fontUi.GetStringSize(HintActs[i], HorizontalAlignment.Left, -1f, fs).X;
                _hintW += _hintKeyW[i] + 2f * HintKeyPad + HintKeyGap + _hintActW[i] + (i + 1 < HintKeys.Length ? HintPairGap : 0f);
            }
        }
        float lineH = _fontUi.GetHeight(fs);
        var plate = new Rect2(Edge, top - 7f, _hintW + 2f * HintPlatePad, lineH + 14f);
        if (plate.End.X > canvasW - Edge) return false;
        UiArt.Nine(_ci, _aPlaque, plate, PlaqueMod);
        float x = plate.Position.X + HintPlatePad;
        for (int i = 0; i < HintKeys.Length; i++)
        {
            float kw = _hintKeyW[i] + 2f * HintKeyPad;
            UiArt.Nine(_ci, _aKeycap, new Rect2(x, top, kw, lineH), Colors.White);
            DrawString(_fontUi, new Vector2(x + HintKeyPad, baseline), HintKeys[i], HorizontalAlignment.Left, -1f, fs, UiTheme.Text);
            x += kw + HintKeyGap;
            DrawString(_fontUi, new Vector2(x, baseline), HintActs[i], HorizontalAlignment.Left, -1f, fs, UiTheme.Dim);
            x += _hintActW[i] + HintPairGap;
        }
        return true;
    }

    private sealed class GlyphRow
    {
        public GlyphDef G;
        public PanelContainer Panel;
        public RichTextLabel Text;
        public Button SocketBtn, TemperBtn;
    }

    private Hero _hero;
    private bool _built, _dirty = true, _tipDirty, _tipShown, _camInit, _camGlide;
    private readonly Action _onHeroChanged, _drawAction, _tickAction, _inputAction, _buildAction;
    private InputEvent _pendingEvent;
    private ulong _lastErrorMs;
    private float _dt, _time, _zoom = 0.9f;
    private Vector2 _cam, _camTarget;

    // pointer state
    private int _hovBoard = -1, _hovNode = -1, _hovGateBoard = -1, _hovGateDir = -1;
    private int _selSocket = -1;
    private bool _leftDown, _midDown, _dragging, _swallowClick;
    private Vector2 _pressPos;
    private string _previewGlyph;
    private Control _previewPanel;

    // board picker
    private int _pickBoard = -1, _pickDir = -1;

    // transient message under the boards
    private string _flash;
    private float _flashT;
    private Color _flashCol;

    // cached per-board strings (rebuilt when the hero changes, never per frame): kind and count, the same with the
    // note for an empty board, the level in the socket. They hold no sizes; the canvas measures them while drawing.
    private readonly string[] _boardSub = new string[ParagonDb.MaxBoards];
    private readonly string[] _boardSubLong = new string[ParagonDb.MaxBoards];
    private readonly string[] _sockText = new string[ParagonDb.MaxBoards];

    // layout that follows the text size (Relayout)
    private float _sideW = SideDesign, _topH = TopMin, _pickTextW = 880f;
    private readonly List<RichTextLabel> _pickTexts = new();

    // controls
    private Font _fontTitle, _fontUi;
    private StyleBox _boardBox, _rowBox, _rowBoxOn;
    /// <summary>The list entries are forged cards: their content keeps clear of the 9 px moulding.</summary>
    private bool _rowArt;
    /// <summary>What a glyph row's text column is narrower than the glyph column (frame, row padding, scroll bar).</summary>
    private float RowTextTrim => _rowArt ? 102f : 84f;
    private PanelContainer _top, _side, _picker, _lockBox;
    private Label _classLabel, _lvlLabel, _xpLabel, _ptsLabel, _spentLabel, _lockTitle, _lockBody, _pickHint;
    private RichTextLabel _triesText;
    private VBoxContainer _glyphRows, _pickRows;
    private Button _refundBtn, _resetBtn, _homeBtn;
    private readonly List<GlyphRow> _rows = new();

    public ParagonUi()
    {
        _onHeroChanged = () => _dirty = true;
        _drawAction = DrawAll;
        _tickAction = Tick;
        _inputAction = HandleInput;
        _buildAction = Build;
    }

    // ───────────────────────── lifecycle ─────────────────────────

    public override void _Ready()
    {
        base._Ready();
        Log.Guard("paragon-ui", _buildAction);
    }

    /// <summary>The manager takes a closed panel out of the tree: stop listening, but remember the hero and the view.</summary>
    public override void _ExitTree()
    {
        base._ExitTree();
        Listen(false);
    }

    public override void OnOpen(object arg)
    {
        base.OnOpen(arg);
        Log.Guard("paragon-ui", () =>
        {
            Build();
            var gh = Game.I?.Hero;
            if (!ReferenceEquals(gh, _hero)) Bind(gh); else Listen(true);
            _leftDown = _midDown = _dragging = _swallowClick = false;
            _flashT = 0f;
            _dirty = true;
            if (_hero != null)
                Log.Info("ui", $"paragon opened: {Names.Class(_hero.Class)} lv{_hero.D.Level} 燼紋{_hero.D.Paragon} points={_hero.ParagonPoints} boards={_hero.ParagonBoards.Count} unlocked={_hero.ParagonUnlocked}");
            else Log.Info("ui", "paragon opened without a hero");
        });
    }

    public override void OnClose()
    {
        base.OnClose();
        Log.Guard("paragon-ui", () =>
        {
            ClosePicker();
            ClearHover();
            _previewGlyph = null; _previewPanel = null;
            _leftDown = _midDown = _dragging = false;
            Game.I?.Ui?.HideTooltip();
            _tipShown = false;
        });
    }

    public override void Refresh()
    {
        base.Refresh();
        _dirty = true;
    }

    /// <summary>
    /// The text-size setting changed. The manager has already rescaled the font sizes of the controls; what this
    /// panel works out by hand follows here: the height of the top bar, the widths of the glyph column and the two
    /// centred boxes and the wrap widths inside them (Relayout), and every BBCode string that carries sizes (the
    /// glyph rows and the note above them are rewritten by the refresh this asks for). The manager dropped its
    /// tooltip card, so the hover text is fetched again at the new sizes. The canvas keeps no metrics: it measures
    /// its labels while drawing.
    /// </summary>
    public override void OnTextScale()
    {
        base.OnTextScale();
        if (!_built) return;
        Log.Guard("paragon-ui", () =>
        {
            _tipShown = false;
            _tipDirty = true;
            _dirty = true;
            if (IsInsideTree()) Relayout();     // a closed panel waits outside the tree: its next refresh does it
            QueueRedraw();
        });
    }

    public override void _Process(double delta)
    {
        if (!_built || !IsVisibleInTree()) return;
        _dt = (float)delta;
        Safe(_tickAction, "frame");
    }

    public override void _Draw()
    {
        base._Draw();
        if (!_built) return;
        Safe(_drawAction, "drawing");
    }

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);
        if (!_built) return;
        _pendingEvent = @event;
        Safe(_inputAction, "input");
        _pendingEvent = null;
    }

    /// <summary>Esc closes the board picker first; only the next Esc reaches the manager and closes the panel.</summary>
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        base._UnhandledKeyInput(@event);
        if (!_built || _picker == null || !_picker.Visible) return;
        if (@event is InputEventKey k && k.Pressed && !k.Echo && (k.Keycode == Key.Escape || k.PhysicalKeycode == Key.Escape))
        {
            ClosePicker();
            GetViewport()?.SetInputAsHandled();
        }
    }

    /// <summary>Runs a per-frame callback; a fault is logged at most once every five seconds so it cannot flood the log.</summary>
    private void Safe(Action a, string what)
    {
        try { a(); }
        catch (Exception e)
        {
            ulong now = Time.GetTicksMsec();
            if (_lastErrorMs != 0 && now - _lastErrorMs < 5000UL) return;
            _lastErrorMs = Math.Max(1UL, now);
            Log.Error("paragon-ui", $"{what} failed (repeats are muted for 5 s)", e);
        }
    }

    private void Listen(bool on)
    {
        if (_hero == null) return;
        _hero.Changed -= _onHeroChanged;
        if (on) _hero.Changed += _onHeroChanged;
    }

    // ───────────────────────── construction ─────────────────────────

    private void Build()
    {
        if (_built) return;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _fontTitle = UiTheme.Title ?? UiTheme.Body ?? ThemeDB.FallbackFont;
        _fontUi = UiTheme.Ui ?? UiTheme.Body ?? ThemeDB.FallbackFont;
        try { LookArt(); }
        catch (Exception e) { Log.Error("paragon-ui", "looking up the interface art failed; the flat shapes are used", e); }
        // the node sprites are drawn far below their authored size: they carry mipmaps (nothing else drawn here does,
        // fonts included, so the text stays as crisp as before)
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        _boardBox = UiTheme.Box(new Color(0.07f, 0.064f, 0.058f, 0.94f), new Color(0.33f, 0.27f, 0.18f), 2, 10);
        // entries of the glyph list and of the board picker: forged cards (brighter inlay and a warm veil while the
        // glyph is socketed); bevelled line boxes without those pieces
        _rowArt = UiTheme.Art && UiArt.Has("card") && UiArt.Has("card_on");
        if (_rowArt)
        {
            _rowBox = UiTheme.ArtBox("paragon_entry", () => new UiNine("card"));
            _rowBoxOn = UiTheme.ArtBox("paragon_entry_on", () => new UiNine("card_on") { Back = new Color(0.93f, 0.66f, 0.3f, 0.06f), BackInset = 7f });
        }
        else
        {
            _rowBox = UiTheme.Box(new Color(0.085f, 0.08f, 0.075f, 0.9f), UiTheme.Border, 1, 4);
            _rowBoxOn = UiTheme.Box(new Color(0.13f, 0.105f, 0.07f, 0.95f), UiTheme.Gold, 1, 4);
        }

        // ── top bar ──
        _top = UiTheme.Frame();
        AddChild(_top);
        // (offsets and sizes of the four overlays are set by Relayout: they follow the text size)
        _top.SetAnchorsPreset(LayoutPreset.TopWide);
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 22);
        _top.AddChild(bar);
        bar.AddChild(BarLabel("燼紋盤", UiTheme.FsTitle, UiTheme.Gold, true));
        // the class name takes the room that is left: it is the first thing to give way (ellipsis, full text on
        // hover) should the bar ever get too tight for the largest text
        _classLabel = UiTheme.Trimmed(BarLabel("", UiTheme.FsRow, UiTheme.Dim));
        _classLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bar.AddChild(_classLabel);
        _lvlLabel = BarLabel("", UiTheme.FsRow);
        bar.AddChild(_lvlLabel);
        _xpLabel = BarLabel("", UiTheme.FsCaption, UiTheme.Dim);
        bar.AddChild(_xpLabel);
        _ptsLabel = BarLabel("", UiTheme.FsName, UiTheme.Gold);
        bar.AddChild(_ptsLabel);
        _spentLabel = BarLabel("", UiTheme.FsCaption, UiTheme.Dim);
        bar.AddChild(_spentLabel);
        _homeBtn = UiTheme.Button("回到燼種", () => CenterOnStart(true), UiTheme.FsRow);
        bar.AddChild(_homeBtn);
        _refundBtn = UiTheme.Button("熄滅全部", AskRefundAll, UiTheme.FsRow);
        bar.AddChild(_refundBtn);
        _resetBtn = UiTheme.Button("重置盤面", AskReset, UiTheme.FsRow);
        bar.AddChild(_resetBtn);
        bar.AddChild(UiTheme.Button("關閉", () => Game.I?.Ui?.Close("paragon"), UiTheme.FsRow));

        // ── glyph column ──
        _side = UiTheme.Frame();
        AddChild(_side);
        _side.SetAnchorsPreset(LayoutPreset.RightWide);
        var sv = new VBoxContainer();
        sv.AddThemeConstantOverride("separation", 8);
        _side.AddChild(sv);
        sv.AddChild(UiTheme.Label("燼符", UiTheme.FsName, UiTheme.Gold, true));
        _triesText = UiTheme.Rich("", _sideW - 48f, UiTheme.FsRow);
        _triesText.MouseFilter = MouseFilterEnum.Ignore;
        sv.AddChild(_triesText);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        sv.AddChild(scroll);
        _glyphRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _glyphRows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_glyphRows);

        // ── board picker (hidden until a gate slot is clicked) ──
        _picker = UiTheme.Frame(true);
        AddChild(_picker);
        _picker.SetAnchorsPreset(LayoutPreset.Center);
        _picker.GrowHorizontal = GrowDirection.Both; _picker.GrowVertical = GrowDirection.Both;
        _picker.Visible = false;
        var pv = new VBoxContainer();
        pv.AddThemeConstantOverride("separation", 10);
        _picker.AddChild(pv);
        pv.AddChild(UiTheme.Label("選擇要接續的燼紋盤", UiTheme.FsTitle, UiTheme.Gold, true));
        _pickHint = UiTheme.Wrapped(UiTheme.Label("", UiTheme.FsRow, UiTheme.Dim));
        pv.AddChild(_pickHint);
        var ps = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        pv.AddChild(ps);
        _pickRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pickRows.AddThemeConstantOverride("separation", 8);
        ps.AddChild(_pickRows);
        var cancel = UiTheme.Button("暫不接續", ClosePicker, UiTheme.FsRow);
        cancel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        pv.AddChild(cancel);

        // ── message shown while the system sleeps ──
        // (the box grows around its centre when the wrapped text needs more lines than the least height allows)
        _lockBox = UiTheme.Frame(true);
        AddChild(_lockBox);
        _lockBox.SetAnchorsPreset(LayoutPreset.Center);
        _lockBox.GrowHorizontal = GrowDirection.Both; _lockBox.GrowVertical = GrowDirection.Both;
        _lockBox.Visible = false;
        var lv = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        lv.AddThemeConstantOverride("separation", 14);
        _lockBox.AddChild(lv);
        _lockTitle = UiTheme.Label("", 36, UiTheme.Gold, true);
        _lockTitle.HorizontalAlignment = HorizontalAlignment.Center;
        lv.AddChild(_lockTitle);
        _lockBody = UiTheme.Label("", UiTheme.FsBody);
        _lockBody.HorizontalAlignment = HorizontalAlignment.Center;
        _lockBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        lv.AddChild(_lockBody);

        // containers let unhandled mouse events fall through to their parent by default; these overlays must not
        // leak clicks, drags or the wheel onto the board canvas underneath
        _top.MouseFilter = MouseFilterEnum.Stop;
        _side.MouseFilter = MouseFilterEnum.Stop;
        _picker.MouseFilter = MouseFilterEnum.Stop;
        _lockBox.MouseFilter = MouseFilterEnum.Stop;

        _built = true;
        _dirty = true;
        Relayout();
        Log.Info("ui", "paragon panel built");
    }

    /// <summary>A label of the top bar: centred on the height of the bar, whatever its own size.</summary>
    private static Label BarLabel(string text, int size, Color? color = null, bool title = false)
    {
        var l = UiTheme.Label(text, size, color, title);
        l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return l;
    }

    /// <summary>
    /// Everything whose size follows the text. The top bar is as high as its labels and buttons need; the glyph
    /// column and the two centred boxes widen with the text-size setting (the columns by less than the text grows:
    /// lines wrap instead, and the canvas keeps its room), and the text blocks inside them wrap at the new widths.
    /// Runs when the panel is built and at every refresh, so a changed setting is picked up as well.
    /// </summary>
    private void Relayout()
    {
        if (_top == null || _side == null || _picker == null || _lockBox == null) return;
        Vector2 full = Size.X > 1f && Size.Y > 1f ? Size : new Vector2(1920f, 1080f);
        float grow = 1f + (UiTheme.TextScale - 1f) * 0.6f;

        _topH = Mathf.Max(TopMin, Mathf.Ceil(_top.GetCombinedMinimumSize().Y));
        _top.OffsetLeft = Edge; _top.OffsetRight = -Edge; _top.OffsetTop = Edge; _top.OffsetBottom = Edge + _topH;

        _sideW = Mathf.Round(SideDesign * grow);                 // 520 / 567 / 614
        _side.OffsetLeft = -(_sideW + Edge); _side.OffsetRight = -Edge; _side.OffsetTop = _topH + Edge * 2f; _side.OffsetBottom = -Edge;
        SetMinWidth(_triesText, _sideW - 48f);
        foreach (var r in _rows) SetMinWidth(r.Text, _sideW - RowTextTrim);

        // board picker: 1100 × 880 at 標準, never larger than the screen
        float pw = Mathf.Floor(Mathf.Min(full.X - 80f, UiTheme.Px(1100f)) * 0.5f), ph = Mathf.Floor(Mathf.Min(full.Y - 60f, UiTheme.Px(880f)) * 0.5f);
        _picker.OffsetLeft = -pw; _picker.OffsetRight = pw; _picker.OffsetTop = -ph; _picker.OffsetBottom = ph;
        _pickTextW = pw * 2f - 120f;                             // frame, row padding and the scroll bar taken off
        foreach (var t in _pickTexts)
            if (IsInstanceValid(t)) SetMinWidth(t, _pickTextW);

        // message box: 720 wide at 標準, at least 220 high
        float lw = Mathf.Floor(Mathf.Min(full.X - 80f, UiTheme.Px(720f)) * 0.5f), lh = Mathf.Floor(UiTheme.Px(220f) * 0.5f);
        _lockBox.OffsetLeft = -lw; _lockBox.OffsetRight = lw; _lockBox.OffsetTop = -lh; _lockBox.OffsetBottom = lh;
        SetMinWidth(_lockBody, lw * 2f - 80f);
    }

    private static void SetMinWidth(Control c, float width)
    {
        if (c == null) return;
        var want = new Vector2(Mathf.Max(0f, Mathf.Round(width)), c.CustomMinimumSize.Y);
        if (c.CustomMinimumSize != want) c.CustomMinimumSize = want;
    }

    private void Bind(Hero h)
    {
        Listen(false);
        _hero = h;
        Listen(true);
        _selSocket = -1; _previewGlyph = null; _previewPanel = null;
        ClearHover();
        ClosePicker();
        BuildGlyphRows();
        _camInit = false;
        _dirty = true;
    }

    private void BuildGlyphRows()
    {
        foreach (var r in _rows)
        {
            _glyphRows.RemoveChild(r.Panel);
            r.Panel.QueueFree();
        }
        _rows.Clear();
        if (_hero == null) return;
        foreach (var g in _hero.ParagonGlyphs)
        {
            var row = new GlyphRow { G = g };
            var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Pass };
            panel.AddThemeStyleboxOverride("panel", _rowBox);
            var pad = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            pad.AddThemeConstantOverride("margin_left", _rowArt ? 18 : 10); pad.AddThemeConstantOverride("margin_right", _rowArt ? 18 : 10);
            pad.AddThemeConstantOverride("margin_top", _rowArt ? 13 : 6); pad.AddThemeConstantOverride("margin_bottom", _rowArt ? 16 : 8);
            panel.AddChild(pad);
            var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            v.AddThemeConstantOverride("separation", 4);
            pad.AddChild(v);
            row.Text = UiTheme.Rich("", _sideW - RowTextTrim, UiTheme.FsRow);     // the column is wider than this: the text wraps at the real width
            row.Text.MouseFilter = MouseFilterEnum.Ignore;
            v.AddChild(row.Text);
            var hb = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            hb.AddThemeConstantOverride("separation", 8);
            v.AddChild(hb);
            string id = g.Id;
            row.SocketBtn = UiTheme.Button("嵌入", () => OnSocketPressed(id), UiTheme.FsCaption);
            row.SocketBtn.MouseFilter = MouseFilterEnum.Pass;
            hb.AddChild(row.SocketBtn);
            row.TemperBtn = UiTheme.Button("淬鍊", () => OnTemperPressed(id), UiTheme.FsCaption);
            row.TemperBtn.MouseFilter = MouseFilterEnum.Pass;
            hb.AddChild(row.TemperBtn);
            panel.MouseEntered += () => OnGlyphHover(id, panel);
            panel.MouseExited += () => OnGlyphUnhover(id);
            row.Panel = panel;
            _glyphRows.AddChild(panel);
            _rows.Add(row);
        }
    }

    // ───────────────────────── per-frame ─────────────────────────

    private void Tick()
    {
        _time += _dt;
        var gh = Game.I?.Hero;
        if (gh != _hero) Bind(gh);
        if (!_camInit && _hero != null && Size.X > 1f)
        {
            _camInit = true;
            CenterOnStart(false);
        }
        if (_dirty) RefreshUi();
        if (_camGlide)
        {
            _cam = _cam.Lerp(_camTarget, 1f - Mathf.Exp(-10f * _dt));
            if (_cam.DistanceSquaredTo(_camTarget) < 1f) { _cam = _camTarget; _camGlide = false; }
            _tipDirty = true;
        }
        if (_flashT > 0f) _flashT -= _dt;

        // a glyph row keeps its preview only while the pointer is really over it
        if (_previewGlyph != null && (_previewPanel == null || !IsInstanceValid(_previewPanel) || !_previewPanel.GetGlobalRect().HasPoint(Ui.PointerPos(this))))
            EndGlyphPreview();

        UpdateHover(false);
        QueueRedraw();
    }

    private void RefreshUi()
    {
        _dirty = false;
        _tipDirty = true;
        var h = _hero;
        bool has = h != null;
        bool broken = has && (h.ParagonBroken || h.ParagonBoards.Count == 0);
        bool locked = has && !h.ParagonUnlocked;
        bool usable = has && !broken && !locked;

        _side.Visible = usable;
        _refundBtn.Visible = usable; _resetBtn.Visible = usable; _homeBtn.Visible = usable;
        _ptsLabel.Visible = usable; _spentLabel.Visible = usable; _xpLabel.Visible = usable; _lvlLabel.Visible = has && !broken;
        _lockBox.Visible = !usable;
        if (!usable) ClosePicker();
        Relayout();

        if (!has)
        {
            _classLabel.Text = "";
            _lockTitle.Text = "燼紋盤";
            _lockBody.Text = "還沒有持燼者踏上旅途。";
            return;
        }
        _classLabel.Text = Names.Class(h.Class);
        if (broken)
        {
            _lockTitle.Text = "燼紋盤無法讀取";
            _lockBody.Text = "燼紋的紀錄出了差錯，這次遊戲期間無法使用。\n詳細情形已寫入記錄檔。";
            return;
        }
        _lvlLabel.Text = $"燼紋等級 {h.D.Paragon}／{Balance.ParagonCap}";
        if (locked)
        {
            _lvlLabel.Text = $"等級 {h.D.Level}／{h.ParagonUnlockLevel}";
            _lockTitle.Text = "燼紋盤尚未甦醒";
            _lockBody.Text = $"持燼者須達到等級 {h.ParagonUnlockLevel}，體內的燼火才足以刻下燼紋。\n目前等級：{h.D.Level}";
        }
        else
        {
            _xpLabel.Text = h.D.Paragon < Balance.ParagonCap ? $"距下一級 {h.XpFraction * 100f:0}%" : "已達上限";
            _ptsLabel.Text = $"可用燼紋點 {h.ParagonPoints}";
            _spentLabel.Text = $"已點亮 {h.ParagonSpent}";
        }

        var boards = h.ParagonBoards;
        if (_selSocket >= boards.Count || (_selSocket >= 0 && !SocketLit(boards[_selSocket]))) _selSocket = -1;
        for (int b = 0; b < _boardSub.Length; b++)
        {
            if (b >= boards.Count) { _boardSub[b] = null; _boardSubLong[b] = null; _sockText[b] = null; continue; }
            var st = boards[b];
            int total = st.Def.Nodes.Count - (st.Def.StartNode != null ? 1 : 0);
            _boardSub[b] = $"{st.Def.Kind}　{st.OwnedCount}／{total}";
            _boardSubLong[b] = b > 0 && st.OwnedCount == 0 ? _boardSub[b] + EmptyBoardNote : null;
            _sockText[b] = st.Glyph != null ? st.GlyphLevel.ToString() : null;
        }
        if (!usable) return;

        // glyph column: the rule under the counter is a caption, one step below the rows
        string gold = UiTheme.Gold.ToHtml(false), dim = UiTheme.Dim.ToHtml(false);
        int tries = h.GlyphTries;
        _triesText.Text = (tries > 0
                ? $"淬鍊機會 [color=#{gold}]{tries}[/color] 次（最深：裂隙第 {h.GlyphTryTier} 層）"
                : $"淬鍊機會 0 次　[color=#{dim}]通關深淵裂隙可獲得[/color]")
            + $"\n[font_size={UiTheme.Fs(UiTheme.FsCaption)}][color=#{dim}]裂隙層數不低於燼符等級時必定成功；燼符每高出一級，成功率降低 10%。[/color][/font_size]";
        int explicitTarget = _selSocket;
        int autoTarget = TargetSocket();
        foreach (var r in _rows)
        {
            string id = r.G.Id;
            bool awake = h.GlyphUnlocked(id);
            int at = awake ? h.GlyphBoard(id) : -1;
            r.Text.Text = RowBb(ParagonText.GlyphRow(h, r.G));
            r.Panel.AddThemeStyleboxOverride("panel", at >= 0 ? _rowBoxOn : _rowBox);
            r.SocketBtn.Visible = awake; r.TemperBtn.Visible = awake;
            if (!awake) continue;

            if (explicitTarget >= 0 && explicitTarget != at) { r.SocketBtn.Text = at >= 0 ? "移至選定的槽" : "嵌入選定的槽"; r.SocketBtn.Disabled = false; }
            else if (at >= 0) { r.SocketBtn.Text = "取下"; r.SocketBtn.Disabled = false; }
            else { r.SocketBtn.Text = "嵌入"; r.SocketBtn.Disabled = autoTarget < 0; }

            int lvl = h.GlyphLevel(id);
            if (lvl >= GlyphDb.MaxLevel) { r.TemperBtn.Text = "已臻極致"; r.TemperBtn.Disabled = true; }
            else if (tries <= 0) { r.TemperBtn.Text = "淬鍊"; r.TemperBtn.Disabled = true; }
            else { r.TemperBtn.Text = $"淬鍊　{h.GlyphUpgradeChance(id) * 100f:0}%"; r.TemperBtn.Disabled = false; }
        }
        if (_previewGlyph != null) ShowGlyphTip();
    }

    // ───────────────────────── texts from Paragon.cs ─────────────────────────
    //
    // The builders in ParagonText write plain design sizes into their BBCode: [font_size=26] for the name line of a
    // tooltip and [font_size=20] for the name in a glyph row, both tuned for the old, smaller running text. Every
    // such string goes through UiTheme.Bb exactly once, here, where the panel fetches it (never again where it is
    // shown). Before that its name line is lifted to the house scale, so the name still stands above the text
    // around it (tooltip body 24, row text 22).

    /// <summary>A node or glyph tooltip from ParagonText, ready for Ui.ShowTooltip.</summary>
    private static string TipBb(string bb) => UiTheme.Bb(bb?.Replace("[font_size=26]", $"[font_size={UiTheme.FsName}]"));

    /// <summary>The text of a glyph row from ParagonText, ready for its RichTextLabel.</summary>
    private static string RowBb(string bb) => UiTheme.Bb(bb?.Replace("[font_size=20]", "[font_size=26]"));

    // ───────────────────────── camera ─────────────────────────

    private Vector2 ViewCenter
    {
        get
        {
            bool side = _side != null && _side.Visible;
            float w = Size.X - (side ? _sideW + Edge * 2f : 0f);
            float top = _topH + Edge * 2f;
            return new Vector2(w * 0.5f, top + (Size.Y - top) * 0.5f);
        }
    }

    private Vector2 ToScreen(Vector2 world) => (world - _cam) * _zoom + ViewCenter;
    private Vector2 ToWorld(Vector2 screen) => (screen - ViewCenter) / _zoom + _cam;
    private static Vector2 BoardOrigin(int bx, int by) => new(bx * Pitch * Cell, by * Pitch * Cell);
    private static Vector2 NodeWorld(ParagonBoardState st, PNode n) => BoardOrigin(st.X, st.Y) + new Vector2((n.X + 0.5f) * Cell, (n.Y + 0.5f) * Cell);
    private static Rect2 BoardWorldRect(int bx, int by) => new(BoardOrigin(bx, by), new Vector2(BoardPx, BoardPx));
    private static bool SocketLit(ParagonBoardState st) => st.Def.Socket != null && st.Owned[st.Def.Socket.Index];

    private void CenterOnStart(bool glide)
    {
        var h = _hero;
        if (h == null) return;
        var boards = h.ParagonBoards;
        if (boards.Count == 0) return;
        var st = boards[0];
        var target = st.Def.StartNode != null ? NodeWorld(st, st.Def.StartNode) : BoardOrigin(0, 0) + new Vector2(BoardPx, BoardPx) * 0.5f;
        GlideTo(target, glide);
    }

    private void GlideTo(Vector2 world, bool glide)
    {
        _camTarget = world;
        if (glide) _camGlide = true;
        else { _cam = world; _camGlide = false; }
        _tipDirty = true;
    }

    private void ZoomAt(Vector2 screen, float factor)
    {
        var before = ToWorld(screen);
        _zoom = Mathf.Clamp(_zoom * factor, ZoomMin, ZoomMax);
        _cam += before - ToWorld(screen);
        _camGlide = false;
        ClampCam();
        _tipDirty = true;
    }

    /// <summary>Keeps the view centre within the attached boards plus the ring of empty slots around them.</summary>
    private void ClampCam()
    {
        var h = _hero;
        if (h == null) return;
        var boards = h.ParagonBoards;
        if (boards.Count == 0) return;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int b = 0; b < boards.Count; b++)
        {
            var o = BoardOrigin(boards[b].X, boards[b].Y);
            minX = Mathf.Min(minX, o.X); minY = Mathf.Min(minY, o.Y);
            maxX = Mathf.Max(maxX, o.X + BoardPx); maxY = Mathf.Max(maxY, o.Y + BoardPx);
        }
        float slack = Pitch * Cell * 0.6f;
        _cam = new Vector2(Mathf.Clamp(_cam.X, minX - slack, maxX + slack), Mathf.Clamp(_cam.Y, minY - slack, maxY + slack));
    }

    // ───────────────────────── pointer ─────────────────────────

    /// <summary>The pointer position on the canvas, false when it is over one of the overlays or outside the panel.</summary>
    private bool CanvasMouse(out Vector2 mp)
    {
        mp = Ui.PointerIn(this);
        if (mp.X < 0f || mp.Y < 0f || mp.X > Size.X || mp.Y > Size.Y) return false;
        var vp = GetViewport();
        return vp != null && vp.GuiGetHoveredControl() == this;
    }

    private void ClearHover()
    {
        _hovBoard = _hovNode = _hovGateBoard = _hovGateDir = -1;
    }

    private void UpdateHover(bool force)
    {
        int hb = -1, hn = -1, gb = -1, gd = -1;
        var h = _hero;
        if (h != null && h.ParagonUnlocked && !_dragging && !_picker.Visible && CanvasMouse(out Vector2 mp))
        {
            var w = ToWorld(mp);
            var boards = h.ParagonBoards;
            for (int b = 0; b < boards.Count && hb < 0; b++)
            {
                var st = boards[b];
                var local = w - BoardOrigin(st.X, st.Y);
                int cx = Mathf.FloorToInt(local.X / Cell), cy = Mathf.FloorToInt(local.Y / Cell);
                var n = st.Def.At(cx, cy);
                if (n == null) continue;
                var c = new Vector2((cx + 0.5f) * Cell, (cy + 0.5f) * Cell);
                if (local.DistanceSquaredTo(c) <= Cell * Cell * 0.25f) { hb = b; hn = n.Index; }
            }
            for (int b = 0; hb < 0 && gb < 0 && b < boards.Count; b++)
                for (int d = 0; d < 4; d++)
                {
                    if (!h.ParagonGateOpen(b, d)) continue;
                    var (dx, dy) = ParagonDb.Dirs[d];
                    if (!BoardWorldRect(boards[b].X + dx, boards[b].Y + dy).HasPoint(w)) continue;
                    gb = b; gd = d;
                    break;
                }
        }
        bool same = hb == _hovBoard && hn == _hovNode && gb == _hovGateBoard && gd == _hovGateDir;
        if (same && !force && !_tipDirty) return;
        _hovBoard = hb; _hovNode = hn; _hovGateBoard = gb; _hovGateDir = gd;
        _tipDirty = false;
        ShowHoverTip();
    }

    private void ShowHoverTip()
    {
        var h = _hero;
        var ui = Game.I?.Ui;
        if (h == null || ui == null) return;
        if (_previewGlyph != null) return;          // the glyph list owns the tooltip while a row is hovered
        if (_hovBoard >= 0)
        {
            var boards = h.ParagonBoards;
            var st = _hovBoard < boards.Count ? boards[_hovBoard] : null;
            var n = st?.Def.Node(_hovNode);
            if (n == null) { HideTip(); return; }
            // the manager puts the card 14 px right of the point, or 10 px left of it when it would leave the screen:
            // hand it the node's right edge, or its left edge when the card is going to flip, so the node stays visible
            var gp = GlobalPosition + ToScreen(NodeWorld(st, n));
            float r = Cell * _zoom * 0.5f;
            bool flips = gp.X - r + 12f + TipW > GetViewportRect().Size.X - 8f;
            ui.ShowTooltip(TipBb(ParagonText.NodeTooltip(h, _hovBoard, n)), new Vector2(flips ? gp.X - r - 2f : gp.X + r - 2f, gp.Y - r - 12f));
            _tipShown = true;
        }
        else if (_hovGateBoard >= 0)
        {
            string gold = UiTheme.Gold.ToHtml(false), dim = UiTheme.Dim.ToHtml(false);
            int left = ParagonDb.MaxBoards - h.ParagonBoards.Count;
            ui.ShowTooltip($"[font_size={UiTheme.Fs(UiTheme.FsName)}][color=#{gold}]燼門之外[/color][/font_size]\n[color=#{dim}]這道燼門已經點亮，門外還空著。[/color]\n\n左鍵：選擇要在此接續的燼紋盤\n[color=#{dim}]還能再接續 {left} 面。[/color]",
                GlobalPosition + Ui.PointerIn(this) + new Vector2(18f, 12f));
            _tipShown = true;
        }
        else HideTip();
    }

    private void HideTip()
    {
        if (!_tipShown) return;
        _tipShown = false;
        Game.I?.Ui?.HideTooltip();
    }

    private void HandleInput()
    {
        var e = _pendingEvent;
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown)
            {
                if (mb.Pressed && _hero != null && !_picker.Visible) ZoomAt(mb.Position, mb.ButtonIndex == MouseButton.WheelUp ? 1.12f : 1f / 1.12f);
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _leftDown = true; _dragging = false; _pressPos = mb.Position;
                    _swallowClick = _picker.Visible;
                    if (_picker.Visible) ClosePicker();
                }
                else
                {
                    bool click = _leftDown && !_dragging && !_swallowClick;
                    _leftDown = false; _dragging = false; _swallowClick = false;
                    if (click) LeftClick();
                }
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Middle)
            {
                _midDown = mb.Pressed;
                if (mb.Pressed) _pressPos = mb.Position; else _dragging = false;
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                if (mb.Pressed) RightClick();
                AcceptEvent();
            }
        }
        else if (e is InputEventMouseMotion mm && (_leftDown || _midDown) && _hero != null)
        {
            if (!_dragging && (_midDown || mm.Position.DistanceTo(_pressPos) > 6f))
            {
                _dragging = true;
                ClearHover();
                HideTip();
            }
            if (_dragging)
            {
                _cam -= mm.Relative / _zoom;
                _camGlide = false;
                ClampCam();
            }
        }
    }

    private void LeftClick()
    {
        var h = _hero;
        if (h == null || !h.ParagonUnlocked) return;
        UpdateHover(true);
        if (_hovGateBoard >= 0) { OpenPicker(_hovGateBoard, _hovGateDir); return; }
        var boards = h.ParagonBoards;
        if (_hovBoard < 0 || _hovBoard >= boards.Count) return;
        int b = _hovBoard, idx = _hovNode;
        var st = boards[b];
        var n = st.Def.Node(idx);
        if (n == null) return;

        if (st.Owned[idx])
        {
            if (n.Type == PNodeType.Socket)
            {
                _selSocket = _selSocket == b ? -1 : b;
                _dirty = true;
                Snd.Ui("ui_click");
            }
            else if (n.Type == PNodeType.Gate && h.ParagonGateOpen(b, n.GateDir)) OpenPicker(b, n.GateDir);
            return;
        }

        string why = h.WhyCantTakeParagon(b, idx);
        if (why != null)
        {
            Flash(why, Palette.Bad);
            Snd.Ui("ui_error");
            return;
        }
        if (!h.TakeParagon(b, idx)) return;
        Snd.Ui(n.Type == PNodeType.Legendary ? "ember_buff" : "ui_click");
        if (n.Type == PNodeType.Legendary) Flash($"傳奇燼紋「{n.Name}」已點亮", Palette.Rarity(Rarity.Legendary));
        else if (n.Type == PNodeType.Socket)
        {
            _selSocket = b;
            Flash("燼符槽已點亮：從右側清單選一枚燼符嵌入", ParagonText.SocketColor);
        }
        else if (n.Type == PNodeType.Gate && h.ParagonGateOpen(b, n.GateDir))
        {
            // reaching a gate is the moment to choose what lies beyond it; declining leaves the slot open for later
            Flash("燼門已開：選擇要接續的燼紋盤，或稍後再點擊門外的空位", Palette.Gold);
            OpenPicker(b, n.GateDir);
        }
        _dirty = true;
    }

    private void RightClick()
    {
        var h = _hero;
        if (h == null || !h.ParagonUnlocked) return;
        if (_picker.Visible) { ClosePicker(); return; }
        UpdateHover(true);
        var boards = h.ParagonBoards;
        if (_hovBoard >= 0 && _hovBoard < boards.Count)
        {
            int b = _hovBoard, idx = _hovNode;
            var st = boards[b];
            var n = st.Def.Node(idx);
            if (n == null || !st.Owned[idx] || n.Type == PNodeType.Start) return;
            if (n.Type == PNodeType.Socket && h.SocketedGlyph(b) != null)
            {
                if (h.UnsocketGlyph(b)) Snd.Ui("equip_armor");
                return;
            }
            string why = h.WhyCantRefundParagon(b, idx);
            if (why != null)
            {
                Flash(why, Palette.Bad);
                Snd.Ui("ui_error");
                return;
            }
            if (h.RefundParagon(b, idx)) Snd.Ui("ui_click");
            return;
        }

        // empty space of an attached board with nothing lit: offer to take the board off again
        if (!CanvasMouse(out Vector2 mp)) return;
        var w = ToWorld(mp);
        for (int b = 1; b < boards.Count; b++)
        {
            var st = boards[b];
            if (!BoardWorldRect(st.X, st.Y).HasPoint(w)) continue;
            if (st.OwnedCount > 0) return;
            string why = h.WhyCantDetachParagonBoard(b);
            if (why != null) { Flash(why, Palette.Bad); return; }
            string id = st.Def.Id, name = st.Def.Name;
            Action detach = () =>
            {
                var hh = _hero;
                if (hh == null) return;
                var bs = hh.ParagonBoards;
                for (int i = 1; i < bs.Count; i++)
                {
                    if (bs[i].Def.Id != id) continue;
                    if (hh.DetachParagonBoard(i)) Flash($"已卸除「{name}」", Palette.Text);
                    else Flash(hh.WhyCantDetachParagonBoard(i) ?? "無法卸除", Palette.Bad);
                    return;
                }
            };
            var ui = Game.I?.Ui;
            if (ui != null) ui.Confirm($"卸除燼紋盤「{name}」？\n之後可以在任何一道點亮的燼門外重新接續。", detach);
            else detach();
            return;
        }
    }

    private void Flash(string text, Color c)
    {
        _flash = text; _flashCol = c; _flashT = 3.2f;
    }

    private void AskRefundAll()
    {
        var h = _hero;
        if (h == null || !h.ParagonUnlocked) return;
        Action act = () =>
        {
            _hero?.RefundAllParagon();
            _selSocket = -1;
            Flash("所有燼紋已熄滅，燼紋點全數返還", Palette.Text);
        };
        var ui = Game.I?.Ui;
        if (ui != null) ui.Confirm("熄滅所有已點亮的燼紋？\n燼紋點全數返還，燼符會被取下；已接續的燼紋盤與燼符等級不變。", act);
        else act();
    }

    private void AskReset()
    {
        var h = _hero;
        if (h == null || !h.ParagonUnlocked) return;
        Action act = () =>
        {
            _hero?.ResetParagon();
            _selSocket = -1;
            CenterOnStart(true);
            Flash("盤面已重置：只留下起始盤", Palette.Text);
        };
        var ui = Game.I?.Ui;
        if (ui != null) ui.Confirm("重置整個燼紋盤？\n所有燼紋熄滅、接續的燼紋盤全部卸除，燼紋點全數返還。燼符等級不受影響。", act);
        else act();
    }

    // ───────────────────────── glyph column ─────────────────────────

    /// <summary>Where "嵌入" puts a glyph: the socket selected on the canvas, otherwise the first lit socket that is still empty.</summary>
    private int TargetSocket()
    {
        var h = _hero;
        if (h == null) return -1;
        var boards = h.ParagonBoards;
        if (_selSocket >= 0 && _selSocket < boards.Count && SocketLit(boards[_selSocket])) return _selSocket;
        for (int b = 0; b < boards.Count; b++)
            if (SocketLit(boards[b]) && h.SocketedGlyph(b) == null) return b;
        return -1;
    }

    private void OnSocketPressed(string glyphId)
    {
        var h = _hero;
        if (h == null) return;
        int at = h.GlyphBoard(glyphId);
        var boards = h.ParagonBoards;
        bool hasSel = _selSocket >= 0 && _selSocket < boards.Count && SocketLit(boards[_selSocket]);
        if (at >= 0 && (!hasSel || _selSocket == at))
        {
            if (h.UnsocketGlyph(at)) Snd.Ui("equip_armor");
            return;
        }
        int target = hasSel ? _selSocket : TargetSocket();
        if (target < 0)
        {
            Flash("先在盤面上點亮一個燼符槽", Palette.Bad);
            Snd.Ui("ui_error");
            return;
        }
        string why = h.WhyCantSocketGlyph(target, glyphId);
        if (why != null)
        {
            Flash(why, Palette.Bad);
            Snd.Ui("ui_error");
            return;
        }
        if (!h.SocketGlyph(target, glyphId)) return;
        Snd.Ui("equip_armor");
        var st = h.ParagonBoards[target];
        Flash($"{GlyphDb.Get(glyphId)?.Name} 已嵌入「{st.Def.Name}」", ParagonText.SocketColor);
        if (st.Def.Socket != null) GlideTo(NodeWorld(st, st.Def.Socket), true);
        _selSocket = -1;
        _dirty = true;
    }

    private void OnTemperPressed(string glyphId)
    {
        var h = _hero;
        if (h == null) return;
        var g = GlyphDb.Get(glyphId);
        int res = h.UpgradeGlyph(glyphId);
        if (res > 0)
        {
            Snd.Ui("anvil");
            Flash($"{g?.Name} 升至等級 {h.GlyphLevel(glyphId)}", Palette.Good);
        }
        else if (res == 0) Flash("淬鍊失敗：燼符沒有回應", Palette.Bad);
        else
        {
            Snd.Ui("ui_error");
            Flash(h.WhyCantUpgradeGlyph(glyphId) ?? "無法淬鍊", Palette.Bad);
        }
        _dirty = true;
    }

    private void OnGlyphHover(string glyphId, Control panel)
    {
        if (_hero == null) return;
        _previewGlyph = glyphId; _previewPanel = panel;
        ShowGlyphTip();
    }

    private void OnGlyphUnhover(string glyphId)
    {
        if (_previewGlyph != glyphId) return;
        // moving onto one of the row's own buttons is not leaving the row
        if (_previewPanel != null && IsInstanceValid(_previewPanel) && _previewPanel.GetGlobalRect().HasPoint(Ui.PointerPos(this))) return;
        EndGlyphPreview();
    }

    private void EndGlyphPreview()
    {
        _previewGlyph = null; _previewPanel = null;
        HideTip();
        _tipDirty = true;
    }

    private void ShowGlyphTip()
    {
        var h = _hero;
        var ui = Game.I?.Ui;
        var g = GlyphDb.Get(_previewGlyph);
        if (h == null || ui == null || g == null || _previewPanel == null || !IsInstanceValid(_previewPanel)) return;
        // left of the column, level with the row, so the card never covers the row's buttons
        var at = _previewPanel.GlobalPosition;
        ui.ShowTooltip(TipBb(ParagonText.GlyphTooltip(h, g)), new Vector2(at.X - TipW - 50f, at.Y - 12f));
        _tipShown = true;
    }

    // ───────────────────────── board picker ─────────────────────────

    private void OpenPicker(int board, int dir)
    {
        var h = _hero;
        if (h == null || !h.ParagonGateOpen(board, dir)) return;
        _pickBoard = board; _pickDir = dir;
        foreach (Node c in _pickRows.GetChildren())
        {
            _pickRows.RemoveChild(c);
            c.QueueFree();
        }
        _pickTexts.Clear();
        Relayout();
        var boards = h.ParagonBoards;
        int ordinal = boards.Count;
        _pickHint.Text = $"接在「{boards[board].Def.Name}」{ParagonDb.DirNames[dir]}側的燼門之外。接續之後，仍可在盤上沒有任何燼紋時卸除。";
        foreach (var def in h.ParagonBoardChoices())
        {
            var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Pass };
            panel.AddThemeStyleboxOverride("panel", _rowBox);
            var pad = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            pad.AddThemeConstantOverride("margin_left", _rowArt ? 18 : 12); pad.AddThemeConstantOverride("margin_right", _rowArt ? 18 : 12);
            pad.AddThemeConstantOverride("margin_top", _rowArt ? 14 : 8); pad.AddThemeConstantOverride("margin_bottom", _rowArt ? 16 : 10);
            panel.AddChild(pad);
            var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            v.AddThemeConstantOverride("separation", 4);
            pad.AddChild(v);
            var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            v.AddChild(head);
            var name = UiTheme.Label(def.Name, UiTheme.FsName, UiTheme.Gold, true);
            name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            head.AddChild(name);
            head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
            string id = def.Id;
            var pick = UiTheme.Button("接續此盤", () => PickBoard(id), UiTheme.FsRow);
            pick.MouseFilter = MouseFilterEnum.Pass;
            head.AddChild(pick);
            // the summary carries no sizes of its own today; Bb keeps it right should Paragon.cs ever add one
            var text = UiTheme.Rich(UiTheme.Bb(ParagonText.BoardSummary(h, def, ordinal)), _pickTextW, UiTheme.FsRow);
            text.MouseFilter = MouseFilterEnum.Ignore;
            v.AddChild(text);
            _pickTexts.Add(text);
            _pickRows.AddChild(panel);
        }
        _picker.Visible = true;
        ClearHover();
        HideTip();
        Snd.Ui("ui_click");
        Log.Info("ui", $"paragon board picker opened at board {board} side {dir}");
    }

    private void ClosePicker()
    {
        _pickBoard = -1; _pickDir = -1;
        if (_picker != null && _picker.Visible) _picker.Visible = false;
    }

    private void PickBoard(string boardId)
    {
        var h = _hero;
        int board = _pickBoard, dir = _pickDir;
        ClosePicker();
        if (h == null || board < 0) return;
        string why = h.WhyCantAttachParagonBoard(board, dir, boardId);
        if (why != null)
        {
            Flash(why, Palette.Bad);
            Snd.Ui("ui_error");
            return;
        }
        if (!h.AttachParagonBoard(board, dir, boardId)) return;
        Snd.Ui("bell_toll");
        var boards = h.ParagonBoards;
        for (int i = 0; i < boards.Count; i++)
        {
            if (boards[i].Def.Id != boardId) continue;
            GlideTo(BoardOrigin(boards[i].X, boards[i].Y) + new Vector2(BoardPx, BoardPx) * 0.5f, true);
            Flash($"「{boards[i].Def.Name}」已接續：先點亮相鄰的燼門", Palette.Gold);
            break;
        }
        _dirty = true;
    }

    // ───────────────────────── drawing ─────────────────────────

    // ── text on the canvas ──
    //
    // The boards are drawn at the zoom, their labels are not: every DrawString below works in screen space with a
    // size from UiTheme.Fs, so the zoom only chooses between a few DESIGN sizes and nothing reaches the screen under
    // the floor (20 px × the text-size setting). Where a label of that size has no room at the current zoom it is
    // left out rather than shrunk - the tooltips carry the same facts - and it comes back when the player zooms in:
    //   board name            always (zoomed out it is centred on the air above its board, see DrawBoardTitle)
    //   kind / count / note   beside the name while the line fits (measured)
    //   "＋ 接續燼紋盤"         while it fits the empty slot, at the floor size if the larger one is too wide
    //   level in a socket     while it finds a free spot beside the socket (see DrawSocketLevel)
    // All positions come from Font metrics at the scaled size, none from constants tuned for one size.

    /// <summary>Design size of the board names at a zoom (old 30 / 24 / 18 / 14).</summary>
    private static int TitleSize(float zoom) => zoom >= 1.2f ? 34 : zoom >= 0.8f ? 28 : zoom >= 0.55f ? 22 : 20;
    /// <summary>Design size of the kind-and-count line beside a board name (old 22 / 16 / 12 / 12).</summary>
    private static int SubSize(float zoom) => zoom >= 1.2f ? 24 : 20;

    /// <summary>This control's canvas item, fetched once per draw for the art calls.</summary>
    private Rid _ci;

    private void DrawAll()
    {
        var size = Size;
        _ci = GetCanvasItem();
        DrawRect(new Rect2(Vector2.Zero, size), BgCol);
        var h = _hero;
        if (h == null || h.ParagonBroken) return;
        var boards = h.ParagonBoards;
        if (boards.Count == 0) return;

        bool locked = !h.ParagonUnlocked;
        float z = _zoom, cell = Cell * z;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 4.2f);
        var view = new Rect2(-cell, -cell, size.X + cell * 2f, size.Y + cell * 2f);
        int fs = UiTheme.Fs(TitleSize(z)), fsSub = UiTheme.Fs(SubSize(z));
        float lw = Mathf.Max(1.5f, 3.2f * z);

        // ── board backdrops (the names follow at the end, above everything else on the canvas) ──
        for (int b = 0; b < boards.Count; b++)
        {
            var st = boards[b];
            var o = ToScreen(BoardOrigin(st.X, st.Y));
            var rect = new Rect2(o.X - cell * 0.5f, o.Y - cell * 0.5f, BoardPx * z + cell, BoardPx * z + cell);
            if (!rect.Intersects(view)) continue;
            if (_aBoard != null)
            {
                // a forged sub-panel around a soot field; the moulding keeps its thickness at every zoom
                var inner = rect.Grow(-3f);
                if (_fBoard != null) UiArt.Tile(_ci, _fBoard, inner, Colors.White);
                else DrawRect(inner, new Color(0.07f, 0.064f, 0.058f, 0.94f));
                UiArt.Nine(_ci, _aBoard, rect, Colors.White);
            }
            else if (_boardBox != null) DrawStyleBox(_boardBox, rect);
            else DrawRect(rect, new Color(0.07f, 0.064f, 0.058f, 0.94f));
        }

        // the prompt of an empty slot: a step above the board names, the floor size when that is wider than the
        // slot, and left out when even the floor size does not fit
        int fsGate = UiTheme.Fs(TitleSize(z) + 4);
        float slotW = BoardPx * z;
        float gateW = _fontTitle.GetStringSize(GatePrompt, HorizontalAlignment.Left, -1f, fsGate).X;
        if (gateW > slotW - 16f)
        {
            fsGate = UiTheme.Fs(UiTheme.MinFont);
            gateW = _fontTitle.GetStringSize(GatePrompt, HorizontalAlignment.Left, -1f, fsGate).X;
        }
        bool gateFits = gateW <= slotW - 16f;
        float gateDrop = (_fontTitle.GetAscent(fsGate) - _fontTitle.GetDescent(fsGate)) * 0.5f;     // baseline below the middle of the slot

        // ── empty slots beyond lit gates ──
        if (!locked)
            for (int b = 0; b < boards.Count; b++)
                for (int d = 0; d < 4; d++)
                {
                    if (!h.ParagonGateOpen(b, d)) continue;
                    var st = boards[b];
                    var (dx, dy) = ParagonDb.Dirs[d];
                    var o = ToScreen(BoardOrigin(st.X + dx, st.Y + dy));
                    var rect = new Rect2(o.X, o.Y, BoardPx * z, BoardPx * z);
                    var gate = ToScreen(NodeWorld(st, st.Def.Gates[d]));
                    bool hot = _hovGateBoard == b && _hovGateDir == d;
                    var c = new Color(Palette.Gold, hot ? 0.95f : 0.35f + 0.3f * pulse);
                    // The board-sized ghost frame is drawn only while the pointer is in the slot. At rest it read as a stray
                    // empty panel (cut off by the top bar whenever the slot lay above the board, its prompt out of sight in
                    // the middle of it). What always shows is the stub from the lit gate and a small plaque carrying the
                    // prompt right where the new board would begin, clear of this board's frame and title line.
                    var dir = new Vector2(dx, dy);
                    float plateW = gateW + 28f, plateH = _fontTitle.GetAscent(fsGate) + _fontTitle.GetDescent(fsGate) + 10f;
                    float plateHalf = (dy != 0 ? plateH : plateW) * 0.5f;
                    var centre = gate + dir * (cell * 2.7f + plateHalf);
                    var plate = new Rect2(centre.X - plateW * 0.5f, centre.Y - plateH * 0.5f, plateW, plateH);
                    DrawLine(gate, gateFits ? centre - dir * (plateHalf + 4f) : gate + dir * cell * 1.5f, c, lw);
                    if (hot && rect.Intersects(view))
                    {
                        if (_aBoard != null)
                        {
                            // under the pointer: the outline of the board that may be attached here
                            var ghost = rect.Grow(cell * 0.5f);
                            DrawRect(ghost.Grow(-5f), new Color(Palette.Gold, 0.05f));
                            UiArt.Nine(_ci, _aBoard, ghost, new Color(1f, 0.92f, 0.74f, 0.7f));
                        }
                        else
                        {
                            DrawRect(rect, new Color(Palette.Gold, 0.07f));
                            var p0 = rect.Position; var p1 = new Vector2(rect.End.X, p0.Y); var p2 = rect.End; var p3 = new Vector2(p0.X, rect.End.Y);
                            DrawDashedLine(p0, p1, c, 2f, 12f); DrawDashedLine(p1, p2, c, 2f, 12f);
                            DrawDashedLine(p2, p3, c, 2f, 12f); DrawDashedLine(p3, p0, c, 2f, 12f);
                        }
                    }
                    if (!gateFits || !plate.Grow(8f).Intersects(view)) continue;        // zoomed out too far for readable text: the stub and the tooltip remain
                    if (_aPlaque != null) UiArt.Nine(_ci, _aPlaque, plate.Grow(5f), PlaqueMod);
                    else DrawRect(plate, PlateCol);
                    DrawString(_fontTitle, new Vector2(plate.Position.X, centre.Y + gateDrop), GatePrompt, HorizontalAlignment.Center, plateW, fsGate,
                        new Color(Palette.Gold, hot ? 0.98f : 0.66f + 0.3f * pulse));      // on its plaque the prompt stays readable through the whole pulse
                }

        // ── links between facing gates of neighbouring boards ──
        for (int b = 0; b < boards.Count; b++)
        {
            var st = boards[b];
            for (int d = 1; d <= 2; d++)
            {
                var (dx, dy) = ParagonDb.Dirs[d];
                int nb = h.ParagonBoardIndexAt(st.X + dx, st.Y + dy);
                if (nb < 0) continue;
                var ga = st.Def.Gates[d];
                var gb = boards[nb].Def.Gates[ParagonDb.Opposite(d)];
                if (ga == null || gb == null) continue;
                bool oa = st.Owned[ga.Index], ob = boards[nb].Owned[gb.Index];
                DrawLine(ToScreen(NodeWorld(st, ga)), ToScreen(NodeWorld(boards[nb], gb)), oa && ob ? Palette.Ember : oa || ob ? LinkNear : LinkDark, oa && ob ? lw * 1.3f : lw);
            }
        }

        // ── per board: links, glyph reach, nodes ──
        for (int b = 0; b < boards.Count; b++)
        {
            var st = boards[b];
            var o = ToScreen(BoardOrigin(st.X, st.Y));
            if (!new Rect2(o.X, o.Y, BoardPx * z, BoardPx * z).Intersects(view)) continue;
            var links = st.Def.Links;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i + 1 < links.Length; i += 2)
                {
                    int ia = links[i], ib = links[i + 1];
                    bool oa = st.Owned[ia], ob = st.Owned[ib];
                    bool lit = oa && ob;
                    if (lit != (pass == 1)) continue;
                    var pa = CellPos(o, ia, cell); var pb = CellPos(o, ib, cell);
                    if (!view.HasPoint(pa) && !view.HasPoint(pb)) continue;
                    DrawLine(pa, pb, lit ? Palette.Ember : oa || ob ? LinkNear : LinkDark, lit ? lw * 1.3f : lw);
                }

            // glyph reach: the socketed glyph, or the one whose row is hovered if it would land here
            Stat ovAttr = Stat.Count;
            int ovRadius = 0;
            var sock = st.Def.Socket;
            if (sock != null)
            {
                bool preview = false;
                if (_previewGlyph != null && !locked)
                {
                    int at = h.GlyphBoard(_previewGlyph);
                    int target = at >= 0 && _selSocket < 0 ? at : TargetSocket();
                    if (target == b && h.GlyphUnlocked(_previewGlyph))
                    {
                        var pg = GlyphDb.Get(_previewGlyph);
                        if (pg != null) { ovAttr = pg.Attr; ovRadius = GlyphDb.Radius(h.GlyphLevel(_previewGlyph)); preview = true; }
                    }
                }
                if (!preview && st.Glyph != null) { ovAttr = st.Glyph.Attr; ovRadius = st.GlyphRadius; }
                else if (!preview && (_selSocket == b || (_hovBoard == b && _hovNode == sock.Index))) ovRadius = GlyphDb.Radius(1);
                if (ovRadius > 0)
                {
                    var sp = CellPos(o, sock.Index, cell);
                    float rr = (ovRadius + 0.5f) * cell;
                    var oc = ovAttr == Stat.Count ? ParagonText.SocketColor : ParagonText.AttrColor(ovAttr);
                    DrawCircle(sp, rr, new Color(oc, preview ? 0.06f + 0.05f * pulse : 0.065f));
                    DrawArc(sp, rr, 0f, Mathf.Tau, 72, new Color(oc, preview ? 0.5f + 0.4f * pulse : 0.5f), 2f, true);
                }
            }

            var nodes = st.Def.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                var p = CellPos(o, n.Index, cell);
                if (!view.HasPoint(p)) continue;
                DrawNode(h, st, b, n, p, cell, pulse, locked);
                if (ovRadius > 0 && ovAttr != Stat.Count && n != sock && GlyphDb.InRadius(n.X - sock.X, n.Y - sock.Y, ovRadius) && Gives(n, ovAttr))
                    DrawArc(p, cell * 0.36f, 0f, Mathf.Tau, 20, new Color(ParagonText.AttrColor(ovAttr), st.Owned[n.Index] ? 0.9f : 0.3f), 1.5f, true);
            }
        }

        // ── board names: last, because zoomed out a name reaches over the frames of two boards ──
        float tAsc = _fontTitle.GetAscent(fs), tDesc = _fontTitle.GetDescent(fs);
        for (int b = 0; b < boards.Count; b++)
        {
            var st = boards[b];
            var o = ToScreen(BoardOrigin(st.X, st.Y));
            var rect = new Rect2(o.X - cell * 0.5f, o.Y - cell * 0.5f, BoardPx * z + cell, BoardPx * z + cell);
            if (!rect.Grow(tAsc + tDesc + 8f).Intersects(view)) continue;
            DrawBoardTitle(h, b, st, rect, cell, fs, fsSub, tAsc, tDesc);
        }

        if (locked) DrawRect(new Rect2(Vector2.Zero, size), new Color(0f, 0f, 0f, 0.66f));

        // ── hint and transient message: stacked from the bottom edge by their measured line heights ──
        bool sideOn = _side != null && _side.Visible;
        float canvasW = size.X - (sideOn ? _sideW + Edge * 2f : 0f);
        int fsHint = UiTheme.Fs(UiTheme.FsCaption), fsFlash = UiTheme.Fs(UiTheme.FsName);
        bool caps = _aKeycap != null && _aPlaque != null;
        float hintBase = size.Y - Edge - (caps ? HintLift : 0f) - _fontUi.GetDescent(fsHint);
        float hintTop = hintBase - _fontUi.GetAscent(fsHint);
        if (!locked && !(caps && DrawHintCaps(fsHint, hintTop, hintBase, canvasW)))
            DrawString(_fontUi, new Vector2(Edge + 8f, hintBase), HintText, HorizontalAlignment.Left, Mathf.Max(1f, canvasW - Edge - 16f), fsHint, UiTheme.Dim);
        if (caps) hintTop -= 12f;       // the message above stands clear of the hint's plaque
        if (_flashT > 0f && _flash != null)
        {
            // one centred line above the hint; a message wider than the canvas wraps and grows upward instead of
            // being cut off or running under the glyph column
            float a = Mathf.Clamp(_flashT / 0.6f, 0f, 1f);
            float fw = Mathf.Max(1f, canvasW - 48f);
            Vector2 fsz = _fontTitle.GetMultilineStringSize(_flash, HorizontalAlignment.Center, fw, fsFlash, 3);
            float block = fsz.Y;
            var fp = new Vector2(24f, hintTop - 10f - block + _fontTitle.GetAscent(fsFlash));
            // a plate of soot behind the line: it is read over lit nodes, connectors and board titles
            float bw = Mathf.Min(fw, fsz.X) + 44f;
            var band = new Rect2(24f + (fw - bw) * 0.5f, hintTop - 10f - block - 5f, bw, block + 10f);
            if (_aPlaque != null) UiArt.Nine(_ci, _aPlaque, band.Grow(3f), new Color(PlaqueMod, a));
            else
            {
                DrawRect(band, new Color(0.02f, 0.018f, 0.02f, 0.82f * a));
                DrawRect(new Rect2(band.Position, new Vector2(bw, 1f)), new Color(_flashCol, 0.45f * a));
                DrawRect(new Rect2(band.Position.X, band.End.Y - 1f, bw, 1f), new Color(_flashCol, 0.45f * a));
            }
            DrawMultilineString(_fontTitle, fp + new Vector2(1.5f, 1.5f), _flash, HorizontalAlignment.Center, fw, fsFlash, 3, new Color(0f, 0f, 0f, 0.85f * a));
            DrawMultilineString(_fontTitle, fp, _flash, HorizontalAlignment.Center, fw, fsFlash, 3, new Color(_flashCol, a));
        }
    }

    /// <summary>
    /// The name of a board with its kind and count (and the note of an empty board) beside it, as far as the line
    /// fits: up to the right edge of the board, or on into the air when no board is attached to the right. The
    /// line stands just above its board. Zoomed out, the air between two boards is lower than the text: then the
    /// line is centred on that gap and gets a plate, so it keeps its full size and still stays between the outermost
    /// nodes of the two boards.
    /// </summary>
    private void DrawBoardTitle(Hero h, int b, ParagonBoardState st, Rect2 rect, float cell, int fs, int fsSub, float asc, float desc)
    {
        const float gap = 14f;
        string name = st.Def.Name;
        float left = rect.Position.X + 8f;
        float room = rect.Size.X - 16f;
        if (h.ParagonBoardIndexAt(st.X + 1, st.Y) < 0) room += Pitch * cell;
        float tw = _fontTitle.GetStringSize(name, HorizontalAlignment.Left, -1f, fs).X;
        string sub = null;
        float sw = 0f;
        if (_boardSubLong[b] != null)
        {
            sw = _fontUi.GetStringSize(_boardSubLong[b], HorizontalAlignment.Left, -1f, fsSub).X;
            if (tw + gap + sw <= room) sub = _boardSubLong[b];
        }
        if (sub == null && _boardSub[b] != null)
        {
            sw = _fontUi.GetStringSize(_boardSub[b], HorizontalAlignment.Left, -1f, fsSub).X;
            if (tw + gap + sw <= room) sub = _boardSub[b];
        }

        float top = rect.Position.Y;
        float baseY = Mathf.Max(top - 8f, top - cell * 0.5f + (asc - desc) * 0.5f);
        if (baseY - asc < top - cell)
        {
            // the line box is higher than the gap: hide the two frame edges behind the glyphs (the plate hugs the
            // ideographs, which use about three quarters of the ascent and hardly any of the descent)
            float lineW = sub != null ? tw + gap + sw : tw;
            var plate = new Rect2(left - 6f, baseY - asc * 0.84f, lineW + 12f, asc * 0.84f + desc * 0.5f);
            if (_aPlaque != null) UiArt.Nine(_ci, _aPlaque, plate.Grow(5f), PlaqueMod);      // its rim lies outside the glyphs
            else DrawRect(plate, PlateCol);
        }
        DrawString(_fontTitle, new Vector2(left, baseY), name, HorizontalAlignment.Left, -1f, fs, UiTheme.Gold);
        if (sub != null) DrawString(_fontUi, new Vector2(left + tw + gap, baseY), sub, HorizontalAlignment.Left, -1f, fsSub, UiTheme.Dim);
    }

    private static Vector2 CellPos(Vector2 originScreen, int index, float cell) =>
        new(originScreen.X + (index % PBoard.Size + 0.5f) * cell, originScreen.Y + (index / PBoard.Size + 0.5f) * cell);

    private static bool Gives(PNode n, Stat attr)
    {
        var ss = n.Stats;
        for (int k = 0; k < ss.Length; k++) if (ss[k].s == attr) return true;
        return false;
    }

    private void DrawNode(Hero h, ParagonBoardState st, int b, PNode n, Vector2 p, float cell, float pulse, bool locked)
    {
        bool owned = st.Owned[n.Index];
        bool reach = !owned && !locked && h.ParagonReachable(b, n.Index);
        bool hover = _hovBoard == b && _hovNode == n.Index;
        Color baseCol = n.Type == PNodeType.Normal && n.Stats.Length > 0 ? ParagonText.AttrColor(n.Stats[0].s) : ParagonText.TypeColor(n.Type);
        Color fill = owned ? baseCol : reach ? baseCol.Darkened(0.52f) : baseCol.Darkened(0.8f);
        int t = (int)n.Type;
        var body = (uint)t < (uint)PnFill.Length ? PnFill[t] : null;
        if (body == null) { DrawNodeFlat(h, st, b, n, p, cell, pulse, owned, reach, hover, baseCol, fill); return; }

        // Forged node: the enamel body tinted with the node's colour (lit: full colour, within reach: darkened by
        // half, out of reach: almost black - the fills the vector shapes had), then the rim over it: iron, gold once
        // lit. Sprites leave the draw transform alone, so nothing has to be restored between them.
        var ci = _ci;
        float half = PnHalf[t] * cell;
        var box = new Rect2(p.X - half, p.Y - half, half * 2f, half * 2f);
        bool lit = owned || n.Type == PNodeType.Start;       // the 燼種 always burns
        switch (n.Type)
        {
            case PNodeType.Start:
                Aura(p, cell * 0.95f, new Color(Palette.Ember, 0.28f + 0.22f * pulse));
                UiArt.Sprite(ci, body, box, baseCol.Darkened(0.35f));
                break;
            case PNodeType.Legendary:
                if (owned) Aura(p, cell * 1.0f, new Color(baseCol, 0.26f + 0.18f * pulse));
                UiArt.Sprite(ci, body, box, fill);
                break;
            case PNodeType.Socket:
                // a socket is a dark well: its colour is in what it holds
                UiArt.Sprite(ci, body, box, fill.Darkened(0.62f));
                break;
            default:
                UiArt.Sprite(ci, body, box, fill);
                break;
        }
        UiArt.Sprite(ci, lit ? PnLit[t] : PnRim[t], box, Colors.White);
        // within reach: the gold of the lit rim breathes over the iron
        if (reach) UiArt.Sprite(ci, PnLit[t], box, new Color(1f, 1f, 1f, 0.16f + 0.6f * pulse));

        switch (n.Type)
        {
            case PNodeType.Start:
                Bead(ci, p, cell * 0.22f, new Color(1f, 0.82f, 0.5f));
                break;
            case PNodeType.Rare:
                // (the art's rare rim is the plain rhombus: the core that tells it from a magic node is set in here)
                Lozenge(ci, p, cell * 0.21f, CoreDark);
                Lozenge(ci, p, cell * 0.115f, fill);
                if (owned)      // a pip tells whether the second half is awake
                    DrawCircle(new Vector2(p.X, p.Y - cell * 0.3726f), Mathf.Max(2.5f, cell * 0.075f), h.ParagonRareActive(b, n.Index) ? Palette.Good : new Color(0.45f, 0.2f, 0.18f));
                break;
            case PNodeType.Legendary:
                Bead(ci, p, cell * 0.118f, owned ? LitRim : fill);
                break;
            case PNodeType.Socket:
            {
                float sr = cell * 0.36f;
                if (st.Glyph != null)
                {
                    Bead(ci, p, sr * 0.6f, ParagonText.AttrColor(st.Glyph.Attr));
                    if (_sockText[b] != null) DrawSocketLevel(st, n, p, cell, sr, _sockText[b]);
                }
                else Bead(ci, p, sr * 0.32f, owned ? new Color(baseCol, 0.5f + 0.4f * pulse) : fill);
                if (_selSocket == b) DrawArc(p, sr + 6f + 2f * pulse, 0f, Mathf.Tau, 32, new Color(LitRim, 0.6f + 0.4f * pulse), 2f, true);
                break;
            }
        }

        if (hover) DrawArc(p, PnRing[t] * cell + 5f, 0f, Mathf.Tau, 28, Colors.White, 2f, true);
    }

    private static Texture2D _glowTex;

    /// <summary>A soft round glow (built once): the breathing aura behind the 燼種 and a lit legendary node.</summary>
    private static Texture2D GlowTex()
    {
        if (_glowTex != null) return _glowTex;
        var g = new Gradient
        {
            Offsets = new[] { 0f, 0.5f, 1f },
            Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.42f), new Color(1f, 1f, 1f, 0f) },
        };
        _glowTex = new GradientTexture2D
        {
            Gradient = g, Width = 96, Height = 96,
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
        };
        return _glowTex;
    }

    /// <summary>The aura of a node: a soft glow of radius <paramref name="r"/> around <paramref name="p"/>.</summary>
    private void Aura(Vector2 p, float r, Color c)
    {
        var tex = GlowTex();
        if (tex != null) DrawTextureRect(tex, new Rect2(p.X - r, p.Y - r, r * 2f, r * 2f), false, c);
        else DrawCircle(p, r * 0.7f, new Color(c, c.A * 0.5f));
    }

    /// <summary>A round enamel bead of radius <paramref name="r"/> (glyph, ember core); a flat disc without the art.</summary>
    private void Bead(Rid ci, Vector2 p, float r, Color c)
    {
        if (_aBead == null) { DrawCircle(p, r, c); return; }
        float half = r * BeadBox;
        UiArt.Sprite(ci, _aBead, new Rect2(p.X - half, p.Y - half, half * 2f, half * 2f), c);
    }

    /// <summary>A small enamel rhombus whose points lie <paramref name="r"/> from the centre.</summary>
    private void Lozenge(Rid ci, Vector2 p, float r, Color c)
    {
        if (_aLozenge == null) { DrawCircle(p, r * 0.75f, c); return; }
        float half = r * LozengeBox;
        UiArt.Sprite(ci, _aLozenge, new Rect2(p.X - half, p.Y - half, half * 2f, half * 2f), c);
    }

    /// <summary>The vector shapes: what a node looks like while the art (or the pieces of its type) is not installed.</summary>
    private void DrawNodeFlat(Hero h, ParagonBoardState st, int b, PNode n, Vector2 p, float cell, float pulse, bool owned, bool reach, bool hover, Color baseCol, Color fill)
    {
        Color rim = owned ? LitRim : reach ? new Color(baseCol, 0.5f + 0.5f * pulse) : DarkRim;
        float rimW = owned ? Mathf.Max(1.5f, 2.4f * _zoom) : reach ? 2f : 1f;
        float r;

        switch (n.Type)
        {
            case PNodeType.Normal:
                r = cell * 0.2f;
                DrawCircle(p, r, fill);
                DrawArc(p, r, 0f, Mathf.Tau, 16, rim, rimW, true);
                break;

            case PNodeType.Magic:
                r = cell * 0.2f;
                DrawSetTransform(p, Mathf.Pi * 0.25f);
                DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), fill);
                DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), rim, false, rimW);
                DrawSetTransform(Vector2.Zero);
                r *= 1.35f;
                break;

            case PNodeType.Rare:
                r = cell * 0.27f;
                DrawSetTransform(p, Mathf.Pi * 0.25f);
                DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), fill);
                DrawRect(new Rect2(-r * 0.55f, -r * 0.55f, r * 1.1f, r * 1.1f), CoreDark);
                DrawRect(new Rect2(-r * 0.3f, -r * 0.3f, r * 0.6f, r * 0.6f), fill);
                DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), rim, false, rimW);
                DrawSetTransform(Vector2.Zero);
                r *= 1.38f;
                if (owned)      // a pip tells whether the second half is awake
                    DrawCircle(new Vector2(p.X, p.Y - r), Mathf.Max(2.5f, cell * 0.075f), h.ParagonRareActive(b, n.Index) ? Palette.Good : new Color(0.45f, 0.2f, 0.18f));
                break;

            case PNodeType.Legendary:
                r = cell * 0.31f;
                if (owned) DrawCircle(p, r * 2.1f, new Color(baseCol, 0.1f + 0.08f * pulse));
                DrawRect(new Rect2(p.X - r, p.Y - r, r * 2f, r * 2f), fill);
                DrawRect(new Rect2(p.X - r, p.Y - r, r * 2f, r * 2f), rim, false, rimW);
                DrawSetTransform(p, Mathf.Pi * 0.25f);
                DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), fill);
                DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), rim, false, rimW);
                DrawSetTransform(Vector2.Zero);
                DrawCircle(p, r * 0.62f, CoreDark);
                DrawCircle(p, r * 0.38f, owned ? LitRim : fill);
                r *= 1.42f;
                break;

            case PNodeType.Socket:
                r = cell * 0.36f;
                DrawCircle(p, r, CoreDark);
                DrawArc(p, r, 0f, Mathf.Tau, 28, owned ? baseCol : fill, Mathf.Max(2f, cell * 0.08f), true);
                if (st.Glyph != null)
                {
                    DrawCircle(p, r * 0.58f, ParagonText.AttrColor(st.Glyph.Attr));
                    if (_sockText[b] != null) DrawSocketLevel(st, n, p, cell, r, _sockText[b]);
                }
                else DrawCircle(p, r * 0.3f, owned ? new Color(baseCol, 0.5f + 0.4f * pulse) : fill);
                if (reach) DrawArc(p, r + 3f, 0f, Mathf.Tau, 28, rim, 2f, true);
                if (_selSocket == b) DrawArc(p, r + 6f + 2f * pulse, 0f, Mathf.Tau, 32, new Color(LitRim, 0.6f + 0.4f * pulse), 2f, true);
                break;

            case PNodeType.Gate:
                r = cell * 0.3f;
                DrawRect(new Rect2(p.X - r, p.Y - r, r * 2f, r * 2f), fill);
                DrawRect(new Rect2(p.X - r * 0.5f, p.Y - r * 0.55f, r, r * 1.55f), CoreDark);
                DrawRect(new Rect2(p.X - r, p.Y - r, r * 2f, r * 2f), rim, false, rimW);
                r *= 1.3f;
                break;

            default:    // the 燼種
                r = cell * 0.4f;
                DrawCircle(p, r * 1.5f, new Color(Palette.Ember, 0.1f + 0.1f * pulse));
                DrawCircle(p, r, baseCol.Darkened(0.35f));
                DrawCircle(p, r * 0.55f, new Color(1f, 0.82f, 0.5f));
                DrawArc(p, r, 0f, Mathf.Tau, 28, LitRim, rimW, true);
                break;
        }

        if (hover) DrawArc(p, r + 5f, 0f, Mathf.Tau, 28, Colors.White, 2f, true);
    }

    /// <summary>
    /// The level of the socketed glyph, beside its socket, at the smallest allowed size whatever the zoom. It is
    /// drawn only where it has room: under the socket, else above it, else in a free corner beside it - first
    /// keeping clear of the glyph-reach rings around the neighbouring nodes as well, then (corners only) of the
    /// nodes themselves. With no free spot, or once the socket is smaller than the digits (zoomed out), it is left
    /// out: the socket's tooltip always tells the level.
    /// </summary>
    private void DrawSocketLevel(ParagonBoardState st, PNode sock, Vector2 p, float cell, float r, string text)
    {
        const float pad = 3f;
        int fs = UiTheme.Fs(UiTheme.MinFont);
        float hgt = fs * 0.74f + pad * 2f;          // digits only: no descenders, about three quarters of an em high
        if (hgt > cell * 0.9f) return;
        float w = _fontUi.GetStringSize(text, HorizontalAlignment.Left, -1f, fs).X + pad * 2f;
        float ring = r + cell * 0.04f + 4f;         // outside the socket's own ring and its hover ring
        float d = ring * 0.7071f;
        for (int pass = 0; pass < 2; pass++)
            for (int k = pass == 0 ? 0 : 2; k < 6; k++)
            {
                Vector2 at = k switch               // top-left corner of the number's box
                {
                    0 => new Vector2(p.X - w * 0.5f, p.Y + ring),
                    1 => new Vector2(p.X - w * 0.5f, p.Y - ring - hgt),
                    2 => new Vector2(p.X + d, p.Y + d),
                    3 => new Vector2(p.X - d - w, p.Y + d),
                    4 => new Vector2(p.X + d, p.Y - d - hgt),
                    _ => new Vector2(p.X - d - w, p.Y - d - hgt),
                };
                if (!BoxClear(st, sock, p, cell, new Rect2(at, new Vector2(w, hgt)), pass == 0)) continue;
                var bp = new Vector2(at.X + pad, at.Y + hgt - pad);
                DrawStringOutline(_fontUi, bp, text, HorizontalAlignment.Left, -1f, fs, 4, new Color(0f, 0f, 0f, 0.8f));
                DrawString(_fontUi, bp, text, HorizontalAlignment.Left, -1f, fs, LitRim);
                return;
            }
    }

    /// <summary>
    /// True when a label box (screen pixels) beside the socket at <paramref name="p"/> touches none of the nodes
    /// around it; with <paramref name="rings"/> it also keeps off the glyph-reach rings drawn around them.
    /// </summary>
    private static bool BoxClear(ParagonBoardState st, PNode sock, Vector2 p, float cell, Rect2 box, bool rings)
    {
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var n = st.Def.At(sock.X + dx, sock.Y + dy);
                if (n == null) continue;
                float reach = NodeReach(n.Type);
                if (rings && reach < 0.37f) reach = 0.37f;
                reach = reach * cell + 1.5f;
                float cx = p.X + dx * cell, cy = p.Y + dy * cell;
                float ex = cx - Mathf.Clamp(cx, box.Position.X, box.End.X), ey = cy - Mathf.Clamp(cy, box.Position.Y, box.End.Y);
                if (ex * ex + ey * ey < reach * reach) return false;
            }
        return true;
    }

    /// <summary>How far the shape DrawNode draws for a node type reaches from the centre of its cell, in cells.</summary>
    private static float NodeReach(PNodeType t) => t switch
    {
        PNodeType.Normal => 0.2f,
        PNodeType.Magic => 0.29f,           // the square stands on a corner: half its diagonal
        PNodeType.Rare => 0.45f,            // with the pip above it
        PNodeType.Legendary => 0.44f,
        PNodeType.Socket => 0.4f,
        PNodeType.Gate => 0.42f,
        _ => PnFill[(int)PNodeType.Start] != null ? 0.53f : 0.4f,      // the 燼種 (the forged one carries rays)
    };
}
