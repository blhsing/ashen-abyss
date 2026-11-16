p = r"Y:\game\src\UiTheme.cs"
s = open(p, encoding="utf-8").read()
def rep(a, b, n=1):
    global s
    assert s.count(a) == n, (s.count(a), a[:90])
    s = s.replace(a, b)

# ── fields ──
rep("    private static StyleBoxFlat _frameBox, _heavyBox, _tipBox;\n", "    private static StyleBoxFlat _frameBox, _heavyBox, _tipBox;\n    private static readonly Dictionary<string, UiNine> ArtBoxes = new();\n")
rep("    private static readonly Dictionary<int, StyleBoxFlat> SlotBoxes = new();", "    private static readonly Dictionary<int, StyleBox> SlotBoxes = new();")

# ── Box / frames ──
old = s[s.index("    /// <summary>A flat box with a border: the building block of every frame in the game.</summary>"):s.index("    // ───────────────────────── theme ─────────────────────────")]
new = open(r"Y:\tools\uiart\_theme_boxes.txt", encoding="utf-8").read()
s = s.replace(old, new)

# ── theme: buttons ──
old = s[s.index("        // ── buttons ──"):s.index('        foreach (string type in new[] { "Button", "OptionButton", "MenuButton" })')]
new = '''        // ── buttons ──
        StyleBox bNormal, bHover, bPressed, bDisabled, bFocus;
        if (Art)
        {
            bNormal = ArtBox("btn", 18f, 8f); bHover = ArtBox("btn_hover", 18f, 8f); bPressed = ArtBox("btn_pressed", 18f, 8f);
            bDisabled = ArtBox("btn_disabled", 18f, 8f); bFocus = ArtBox("btn_focus", 0f, 0f);
        }
        else
        {
            var fn = Flat(Raised, Border, 1, 3, 18f, 8f); fn.BorderWidthBottom = 2;
            var fh = Flat(new Color(0.165f, 0.138f, 0.112f, 0.98f), Gold, 1, 3, 18f, 8f); fh.BorderWidthBottom = 2;
            var fp = Flat(new Color(0.062f, 0.055f, 0.052f, 1f), Ember.Darkened(0.2f), 1, 3, 18f, 8f); fp.BorderWidthTop = 2;
            var ff = Flat(new Color(0f, 0f, 0f, 0f), new Color(Gold, 0.55f), 1, 3);
            ff.DrawCenter = false;
            bNormal = fn; bHover = fh; bPressed = fp; bFocus = ff;
            bDisabled = Flat(new Color(0.07f, 0.068f, 0.07f, 0.85f), new Color(0.2f, 0.18f, 0.16f), 1, 3, 18f, 8f);
        }
'''
s = s.replace(old, new)

# ── frames in the theme ──
rep('''        t.SetStylebox("panel", "Panel", FrameBox);
        t.SetStylebox("panel", "PanelContainer", FrameBox);
        t.SetStylebox("panel", "PopupPanel", TipBox);
        t.SetStylebox("panel", "TooltipPanel", TipBox);''', '''        t.SetStylebox("panel", "Panel", PanelBox);
        t.SetStylebox("panel", "PanelContainer", PanelBox);
        t.SetStylebox("panel", "PopupPanel", Art ? ArtBox("panel", 14f, 12f, "soot", 3f) : TipFlat);
        t.SetStylebox("panel", "TooltipPanel", Art ? ArtBox("panel", 14f, 10f, "soot", 3f) : TipFlat);''')

# ── text fields ──
rep('''        var eNormal = Flat(Inset, Border, 1, 3, 12f, 8f);
        var eFocus = Flat(Inset, Gold, 1, 3, 12f, 8f);
        var eRead = Flat(new Color(0.05f, 0.048f, 0.05f, 0.9f), new Color(0.2f, 0.18f, 0.16f), 1, 3, 12f, 8f);''',
'''        StyleBox eNormal = Art ? ArtBox("input", 14f, 9f) : Flat(Inset, Border, 1, 3, 12f, 8f);
        StyleBox eFocus = Art ? ArtBox("input_focus", 14f, 9f) : Flat(Inset, Gold, 1, 3, 12f, 8f);
        StyleBox eRead = Art ? ArtBox("input_read", () => new UiNine("input", null, 0f, 14f, 9f) { FrameMod = new Color(0.72f, 0.7f, 0.68f) })
            : Flat(new Color(0.05f, 0.048f, 0.05f, 0.9f), new Color(0.2f, 0.18f, 0.16f), 1, 3, 12f, 8f);''')

# ── scrolling ──
old = s[s.index("        var track = Flat(new Color(0f, 0f, 0f, 0.35f)"):s.index('        t.SetStylebox("panel", "ScrollContainer", empty);')]
new = '''        var track = Flat(new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0f), 0, 4, 4f, 4f);
        var grab = Flat(Border, Border, 0, 4);
        var grabHi = Flat(Gold.Darkened(0.15f), Gold, 0, 4);
        var grabOn = Flat(Gold, Gold, 0, 4);
        foreach (string type in new[] { "VScrollBar", "HScrollBar" })
        {
            // the forged track and grabber are vertical pieces; the rare horizontal bar keeps the flat look
            bool art = Art && type == "VScrollBar";
            t.SetStylebox("scroll", type, art ? ArtBox("scroll_track", 6f, 4f) : track);
            t.SetStylebox("scroll_focus", type, art ? ArtBox("scroll_track", 6f, 4f) : track);
            t.SetStylebox("grabber", type, art ? ArtBox("scroll_grab", 0f, 0f) : grab);
            t.SetStylebox("grabber_highlight", type, art ? ArtBox("scroll_grab_hover", 0f, 0f) : grabHi);
            t.SetStylebox("grabber_pressed", type, art ? ArtBox("scroll_grab_hover", 0f, 0f) : grabOn);
        }
'''
s = s.replace(old, new)

# ── sliders ──
old = s[s.index("        var sTrack = Flat(new Color(0f, 0f, 0f, 0.6f), Border, 1, 3, 0f, 3f);"):s.index("        // ── progress ──")]
new = '''        StyleBox sTrack = Art ? ArtBox("slider_track", 0f, 5f) : Flat(new Color(0f, 0f, 0f, 0.6f), Border, 1, 3, 0f, 3f);
        StyleBox sFill = Art ? ArtBox("slider_fill", 0f, 5f) : Flat(Gold.Darkened(0.25f), Gold.Darkened(0.25f), 0, 3, 0f, 3f);
        StyleBox sFillHi = Art ? sFill : Flat(Gold, Gold, 0, 3, 0f, 3f);
        foreach (string type in new[] { "HSlider", "VSlider" })
        {
            t.SetStylebox("slider", type, sTrack);
            t.SetStylebox("grabber_area", type, sFill);
            t.SetStylebox("grabber_area_highlight", type, sFillHi);
        }

'''
s = s.replace(old, new)

# ── progress ──
rep('''        t.SetStylebox("background", "ProgressBar", Flat(new Color(0f, 0f, 0f, 0.6f), Border, 1, 2, 2f, 2f));''',
    '''        t.SetStylebox("background", "ProgressBar", Art ? ArtBox("input", 3f, 3f) : Flat(new Color(0f, 0f, 0f, 0.6f), Border, 1, 2, 2f, 2f));''')

# ── tabs ──
rep('''        var tabOn = Flat(Panel, Gold.Darkened(0.2f), 1, 3, 20f, 9f); tabOn.BorderWidthBottom = 0; tabOn.BorderWidthTop = 2;
        var tabOff = Flat(new Color(0.05f, 0.047f, 0.05f, 0.92f), new Color(Border, 0.6f), 1, 3, 20f, 9f); tabOff.BorderWidthBottom = 0;
        var tabHover = Flat(new Color(0.1f, 0.09f, 0.085f, 0.95f), Border.Lightened(0.15f), 1, 3, 20f, 9f); tabHover.BorderWidthBottom = 0;''',
'''        StyleBox tabOn, tabOff, tabHover;
        if (Art) { tabOn = ArtBox("tab_on", 20f, 9f); tabOff = ArtBox("tab", 20f, 9f); tabHover = ArtBox("tab_hover", 20f, 9f); }
        else
        {
            var fOn = Flat(Panel, Gold.Darkened(0.2f), 1, 3, 20f, 9f); fOn.BorderWidthBottom = 0; fOn.BorderWidthTop = 2;
            var fOff = Flat(new Color(0.05f, 0.047f, 0.05f, 0.92f), new Color(Border, 0.6f), 1, 3, 20f, 9f); fOff.BorderWidthBottom = 0;
            var fHover = Flat(new Color(0.1f, 0.09f, 0.085f, 0.95f), Border.Lightened(0.15f), 1, 3, 20f, 9f); fHover.BorderWidthBottom = 0;
            tabOn = fOn; tabOff = fOff; tabHover = fHover;
        }''')
rep('''        t.SetStylebox("panel", "TabContainer", Flat(Panel, Border, 1, 3, 14f, 12f));''', '''        t.SetStylebox("panel", "TabContainer", Art ? PanelBox : Flat(Panel, Border, 1, 3, 14f, 12f));''')

# ── popups and lists ──
rep('''        var menu = Flat(new Color(0.045f, 0.042f, 0.048f, 0.99f), Border, 1, 3, 6f, 6f);
        menu.ShadowColor = new Color(0f, 0f, 0f, 0.55f); menu.ShadowSize = 10;''',
'''        StyleBox menu;
        if (Art) menu = ArtBox("panel", 12f, 12f, "soot", 3f);
        else
        {
            var fm = Flat(new Color(0.045f, 0.042f, 0.048f, 0.99f), Border, 1, 3, 6f, 6f);
            fm.ShadowColor = new Color(0f, 0f, 0f, 0.55f); fm.ShadowSize = 10;
            menu = fm;
        }''')
rep('''            t.SetStylebox("panel", type, Flat(Inset, Border, 1, 3, 6f, 6f));''', '''            t.SetStylebox("panel", type, Art ? WellBox : Flat(Inset, Border, 1, 3, 6f, 6f));''')

# ── separators ──
rep('''        t.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = new Color(Border, 0.85f), Thickness = 1 });''',
    '''        t.SetStylebox("separator", "HSeparator", Art ? ArtBox("rule_line", () => new UiNine("rule") { Line = true, FrameMod = new Color(1f, 1f, 1f, 0.8f) })
            : new StyleBoxLine { Color = new Color(Border, 0.85f), Thickness = 1 });''')
open(p, "w", encoding="utf-8", newline="\n").write(s)

p = r"Y:\game\src\UiNine.cs"
s = open(p, encoding="utf-8").read()
if "BackInset" not in s:
    rep("    public Color Back = new(0f, 0f, 0f, 0f);\n", "    public Color Back = new(0f, 0f, 0f, 0f);\n    /// <summary>How far the flat colour stays inside the box (design px).</summary>\n    public float BackInset;\n")
    rep("            if (Back.A > 0f) RenderingServer.CanvasItemAddRect(ci, rect, Back);", "            if (Back.A > 0f) RenderingServer.CanvasItemAddRect(ci, BackInset != 0f ? rect.Grow(-BackInset) : rect, Back);")
    open(p, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
