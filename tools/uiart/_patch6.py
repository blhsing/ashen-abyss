p = r"Y:\game\src\UiTheme.cs"
s = open(p, encoding="utf-8").read()
def rep(a, b, n=1):
    global s
    assert s.count(a) == n, (s.count(a), a[:90])
    s = s.replace(a, b)

# theme glyphs from the art
rep('''        Put("arrow", "OptionButton", Tex(chevron("M3 5l4 4.2L11 5", "#dbd1bd"), 14, 14));
    }''', '''        Put("arrow", "OptionButton", Tex(chevron("M3 5l4 4.2L11 5", "#dbd1bd"), 14, 14));

        if (!Art) return;
        // forged versions from the raster art replace the vector glyphs where a piece exists
        void PutArt(string name, string type, string piece) { var tex = UiArt.Icon(piece); if (tex != null) t.SetIcon(name, type, tex); }
        foreach (string type in new[] { "CheckBox", "PopupMenu" })
        {
            PutArt("unchecked", type, "check_off"); PutArt("checked", type, "check_on");
            PutArt("radio_unchecked", type, "radio_off"); PutArt("radio_checked", type, "radio_on");
        }
        PutArt("unchecked", "CheckButton", "toggle_off"); PutArt("checked", "CheckButton", "toggle_on");
        foreach (string type in new[] { "HSlider", "VSlider" })
        {
            PutArt("grabber", type, "slider_grab"); PutArt("grabber_highlight", type, "slider_grab_hover");
        }
        PutArt("arrow", "OptionButton", "arrow_down");
        PutArt("submenu", "PopupMenu", "arrow_right");
    }''')

# tab tone
rep('''    public static T TabTone<T>(T b, bool active) where T : Godot.Button
    {
        if (_tabOn == null)''', '''    public static T TabTone<T>(T b, bool active) where T : Godot.Button
    {
        if (Art)
        {
            b.AddThemeStyleboxOverride("normal", ArtBox(active ? "tab_on" : "tab", 20f, 8f));
            b.AddThemeStyleboxOverride("hover", ArtBox(active ? "tab_on" : "tab_hover", 20f, 8f));
            b.AddThemeStyleboxOverride("pressed", ArtBox("tab_on", 20f, 8f));
            b.AddThemeStyleboxOverride("hover_pressed", ArtBox("tab_on", 20f, 8f));
        }
        else
        {
        if (_tabOn == null)''')
rep('''        b.AddThemeStyleboxOverride("normal", active ? _tabOn : _tabOff);
        b.AddThemeStyleboxOverride("hover", active ? _tabOnHover : _tabOffHover);
        b.AddThemeStyleboxOverride("pressed", _tabOn);
        b.AddThemeStyleboxOverride("hover_pressed", _tabOnHover);
        Color c = active ? Gold.Lightened(0.22f) : new Color(0.66f, 0.63f, 0.57f);''', '''        b.AddThemeStyleboxOverride("normal", active ? _tabOn : _tabOff);
        b.AddThemeStyleboxOverride("hover", active ? _tabOnHover : _tabOffHover);
        b.AddThemeStyleboxOverride("pressed", _tabOn);
        b.AddThemeStyleboxOverride("hover_pressed", _tabOnHover);
        }
        Color c = active ? Gold.Lightened(0.3f) : new Color(0.66f, 0.63f, 0.57f);''')

# frame
rep('''        p.AddThemeStyleboxOverride("panel", heavy ? HeavyBox : FrameBox);
        if (heavy) p.Draw += () => DrawWindowOrnament(p);
        return p;''', '''        p.AddThemeStyleboxOverride("panel", heavy ? WindowBox : PanelBox);
        if (heavy && !Art) p.Draw += () => DrawWindowOrnament(p);
        return p;
    }

    /// <summary>An inset well (recessed rim, darkest field) to hold a list or a grid of slots.</summary>
    public static PanelContainer Well()
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", WellBox);
        return p;''')

# rule
rep('''        var s = new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore };
        s.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(Gold.Darkened(0.3f), alpha), Thickness = 1 });
        return s;''', '''        var s = new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore };
        if (Art)
        {
            float a = Mathf.Clamp(alpha + 0.2f, 0f, 1f);
            s.AddThemeStyleboxOverride("separator", ArtBox("rule|" + a.ToString("0.00"), () => new UiNine("rule") { Line = true, FrameMod = new Color(1f, 1f, 1f, a) }));
        }
        else s.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(Gold.Darkened(0.3f), alpha), Thickness = 1 });
        return s;''')

# item slot boxes
old = s[s.index("    private static StyleBoxFlat SlotBox(int rarity, bool hover, bool bad)"):s.index("    /// <summary>The colour an item's glyph is drawn in")]
new = '''    /// <summary>The shared box of an inventory cell: rarity -1 = empty, otherwise (int)Rarity; hover and "cannot be worn" states.</summary>
    public static StyleBox SlotBox(int rarity, bool hover, bool bad)
    {
        int key = (rarity + 1) * 4 + (hover ? 1 : 0) + (bad ? 2 : 0);
        if (SlotBoxes.TryGetValue(key, out var s)) return s;
        if (Art)
        {
            // forged cell + a tinted rarity line (with corner flares from legendary up) + a gold rim while hovered
            var n = new UiNine { Frame = UiArt.P("slot") };
            if (rarity > (int)Rarity.Common)
            {
                n.Over1 = UiArt.P(rarity >= (int)Rarity.Legendary ? "slot_rarity_hi" : "slot_rarity");
                Color rc0 = Palette.Rarity((Rarity)rarity);
                n.Over1Mod = hover ? rc0.Lightened(0.2f) : rc0;
            }
            else if (rarity == (int)Rarity.Common)
            {
                n.Over1 = UiArt.P("slot_rarity");
                n.Over1Mod = new Color(0.62f, 0.6f, 0.56f, hover ? 0.8f : 0.5f);
            }
            if (hover) n.Over2 = UiArt.P("slot_hover");
            if (bad) { n.Back = new Color(0.45f, 0.04f, 0.03f, 0.32f); n.BackInset = 4f; n.BackOver = true; }
            SlotBoxes[key] = n;
            return n;
        }
        Color rc = rarity < 0 ? Border.Darkened(0.45f)
            : rarity == (int)Rarity.Common ? new Color(0.4f, 0.385f, 0.36f) : Palette.Rarity((Rarity)rarity);
        Color bg = rarity <= (int)Rarity.Common ? Inset : Inset.Lerp(new Color(rc, 1f), 0.11f);
        if (bad) bg = bg.Lerp(new Color(0.45f, 0.04f, 0.03f, 1f), 0.3f);
        if (hover) { bg = bg.Lightened(0.07f); rc = rarity < 0 ? Border : rc.Lightened(0.25f); }
        else if (rarity > (int)Rarity.Common) rc = rc.Darkened(0.2f);
        s = Flat(bg, rc, rarity > (int)Rarity.Common || hover ? 2 : 1, 3);
        SlotBoxes[key] = s;
        return s;
    }

'''
s = s.replace(old, new)
rep("        StyleBoxFlat normal = SlotBox(rarity, false, bad), lit = SlotBox(rarity, true, bad);", "        StyleBox normal = SlotBox(rarity, false, bad), lit = SlotBox(rarity, true, bad);")
open(p, "w", encoding="utf-8", newline="\n").write(s)

p = r"Y:\game\src\UiNine.cs"
s = open(p, encoding="utf-8").read()
if "BackOver" not in s:
    rep("    public float BackInset;\n", "    public float BackInset;\n    /// <summary>Lay the flat colour over the frame's floor instead of under it (a tint veil inside a slot).</summary>\n    public bool BackOver;\n")
    rep("            if (Back.A > 0f) RenderingServer.CanvasItemAddRect(ci, BackInset != 0f ? rect.Grow(-BackInset) : rect, Back);",
        "            if (Back.A > 0f && !BackOver) RenderingServer.CanvasItemAddRect(ci, BackInset != 0f ? rect.Grow(-BackInset) : rect, Back);")
    rep("            if (Over1 != null) Layer(ci, Over1, rect, Over1Mod);", "            if (Back.A > 0f && BackOver) RenderingServer.CanvasItemAddRect(ci, BackInset != 0f ? rect.Grow(-BackInset) : rect, Back);\n            if (Over1 != null) Layer(ci, Over1, rect, Over1Mod);")
    open(p, "w", encoding="utf-8", newline="\n").write(s)

# Ui.cs: tooltip cards on the art, texture set follows the window
p = r"Y:\game\src\Ui.cs"
s = open(p, encoding="utf-8").read()
rep('''        var dimmer = (StyleBoxFlat)UiTheme.TipBox.Duplicate();
        dimmer.BorderColor = UiTheme.Border.Darkened(0.25f);
        dimmer.BgColor = new Color(0.026f, 0.025f, 0.03f, 0.97f);
        _tip2.AddThemeStyleboxOverride("panel", dimmer);''', '''        _tip2.AddThemeStyleboxOverride("panel", UiTheme.TipBoxDim);''')
rep('''            Color c = ReferenceEquals(card, _tip2) ? _tip2Accent : _tipAccent;
            card.DrawRect(new Rect2(1f, 1f, card.Size.X - 2f, 3f), new Color(c, 0.9f));
            card.DrawRect(new Rect2(1f, 4f, card.Size.X - 2f, 1f), new Color(0f, 0f, 0f, 0.5f));''', '''            Color c = ReferenceEquals(card, _tip2) ? _tip2Accent : _tipAccent;
            UiTheme.DrawTipBand(card, c);''')
rep('''        try { _script = UiScript.FromArgs(); } catch (Exception e) { Log.Error("uitest", "interface script setup failed", e); }
    }''', '''        try { _script = UiScript.FromArgs(); } catch (Exception e) { Log.Error("uitest", "interface script setup failed", e); }
        // the raster art comes in two densities: follow the window across the 1080p line
        try
        {
            var win = GetTree()?.Root;
            if (win != null)
            {
                UiArt.FitWindow(win.Size);
                win.SizeChanged += () => { if (IsInstanceValid(win)) UiArt.FitWindow(win.Size); };
                UiArt.SetChanged += () => { if (IsInstanceValid(win)) UiTheme.RedrawAll(win); };
            }
        }
        catch (Exception e) { Log.Error("ui", "could not follow the window size for the interface art", e); }
    }''')
open(p, "w", encoding="utf-8", newline="\n").write(s)

p = r"Y:\game\src\UiTheme.cs"
s = open(p, encoding="utf-8").read()
rep('''    private static void RescaleNode(Node n, float oldScale)''', '''    /// <summary>Asks every canvas item under <paramref name="root"/> to redraw (after the art's texture set changed).</summary>
    public static void RedrawAll(Node root)
    {
        if (root == null || !GodotObject.IsInstanceValid(root)) return;
        if (root is CanvasItem ci) ci.QueueRedraw();
        int count = root.GetChildCount();
        for (int i = 0; i < count; i++) RedrawAll(root.GetChild(i));
    }

    private static void RescaleNode(Node n, float oldScale)''')
open(p, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
