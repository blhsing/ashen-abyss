def patch(path, pairs):
    s = open(path, encoding="utf-8").read()
    for a, b in pairs:
        assert s.count(a) == 1, (path, s.count(a), a[:90])
        s = s.replace(a, b)
    open(path, "w", encoding="utf-8", newline="\n").write(s)

patch(r"Y:\game\src\CreditsData.cs", [
    # Box: art line frame when the raster art is installed
    ('''    public static StyleBoxFlat Box(Color bg, Color border, int borderPx = 1, int radius = 2, float padH = 0f, float padV = 0f)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border, AntiAliasing = radius > 0 };''',
     '''    /// <summary>
    /// A bordered box. With the raster interface art it is a bevelled line frame tinted with the border colour over
    /// the flat background (see UiTheme.Box); without it, or for round boxes, the flat box of old.
    /// </summary>
    public static StyleBox Box(Color bg, Color border, int borderPx = 1, int radius = 2, float padH = 0f, float padV = 0f)
    {
        if (UiTheme.Art && radius <= 5 && borderPx > 0 && border.A > 0.02f && UiArt.Has("boxline"))
        {
            var n = new UiNine("boxline") { Back = bg, BackInset = 1f, FrameMod = border };
            n.ContentMarginLeft = padH; n.ContentMarginRight = padH; n.ContentMarginTop = padV; n.ContentMarginBottom = padV;
            return n;
        }
        return FlatBox(bg, border, borderPx, radius, padH, padV);
    }

    public static StyleBoxFlat FlatBox(Color bg, Color border, int borderPx = 1, int radius = 2, float padH = 0f, float padV = 0f)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border, AntiAliasing = radius > 0 };'''),
    ('''    public static StyleBoxFlat FocusBox()
    {
        var s = new StyleBoxFlat { DrawCenter = false, BorderColor = Gold, AntiAliasing = true };''',
     '''    public static StyleBox FocusBox()
    {
        if (UiTheme.Art) return UiTheme.ArtBox("menu_focus", () => new UiNine("btn_focus") { Grow = 2f });
        var s = new StyleBoxFlat { DrawCenter = false, BorderColor = Gold, AntiAliasing = true };'''),
    ('''            var sb = Box(PanelBg, heavy ? Palette.Ember.Darkened(0.45f) : Line, heavy ? 2 : 1, 3);
            sb.ShadowColor = new Color(0, 0, 0, 0.55f);''', '''            var sb = FlatBox(PanelBg, heavy ? Palette.Ember.Darkened(0.45f) : Line, heavy ? 2 : 1, 3);
            sb.ShadowColor = new Color(0, 0, 0, 0.55f);'''),
    ('''        var p = new PanelContainer();
        var sb = Box(new Color(GlassBg.R, GlassBg.G, GlassBg.B, alpha), border ?? new Color(Line.R, Line.G, Line.B, 0.75f), 1, 3);
        sb.ShadowColor = new Color(0, 0, 0, 0.45f);
        sb.ShadowSize = 14;
        p.AddThemeStyleboxOverride("panel", sb);
        return p;''', '''        var p = new PanelContainer();
        if (UiTheme.Art)
        {
            // the sub-panel moulding round a translucent soot field: the backdrop still glows through
            float a = Mathf.Clamp(alpha + 0.06f, 0f, 1f);
            p.AddThemeStyleboxOverride("panel", UiTheme.ArtBox("glass|" + a.ToString("0.00"), () => new UiNine("panel", "soot", 3f) { FieldMod = new Color(1f, 1f, 1f, a) }));
            return p;
        }
        var sb = FlatBox(new Color(GlassBg.R, GlassBg.G, GlassBg.B, alpha), border ?? new Color(Line.R, Line.G, Line.B, 0.75f), 1, 3);
        sb.ShadowColor = new Color(0, 0, 0, 0.45f);
        sb.ShadowSize = 14;
        p.AddThemeStyleboxOverride("panel", sb);
        return p;'''),
    # Btn
    ('''        b.AddThemeStyleboxOverride("normal", Box(RowBg, edge, 1, 2, 18, 6));
        b.AddThemeStyleboxOverride("hover", Box(new Color(0.125f, 0.105f, 0.088f, 0.96f), hot, 1, 2, 18, 6));
        b.AddThemeStyleboxOverride("pressed", Box(pressBg, hot.Lightened(0.15f), 1, 2, 18, 6));
        b.AddThemeStyleboxOverride("disabled", Box(new Color(0.05f, 0.048f, 0.046f, 0.72f), new Color(0.19f, 0.18f, 0.17f), 1, 2, 18, 6));
        b.AddThemeStyleboxOverride("focus", FocusBox());''', '''        if (UiTheme.Art)
        {
            // forged plates; the tone warms (primary) or reddens (danger) the metal a little, the lettering carries the rest
            Color mod = tone == MenuTone.Primary ? new Color(1f, 0.94f, 0.84f) : tone == MenuTone.Danger ? new Color(1f, 0.76f, 0.72f) : Colors.White;
            string k = "|" + tone;
            b.AddThemeStyleboxOverride("normal", UiTheme.ArtBox("mbtn" + k, () => new UiNine("btn", null, 0f, 18f, 6f) { FrameMod = mod }));
            b.AddThemeStyleboxOverride("hover", UiTheme.ArtBox("mbtn_hover" + k, () => new UiNine("btn_hover", null, 0f, 18f, 6f) { FrameMod = mod }));
            b.AddThemeStyleboxOverride("pressed", UiTheme.ArtBox("mbtn_pressed" + k, () => new UiNine("btn_pressed", null, 0f, 18f, 6f) { FrameMod = mod }));
            b.AddThemeStyleboxOverride("hover_pressed", UiTheme.ArtBox("mbtn_pressed" + k, () => new UiNine("btn_pressed", null, 0f, 18f, 6f) { FrameMod = mod }));
            b.AddThemeStyleboxOverride("disabled", UiTheme.ArtBox("btn_disabled", 18f, 6f));
            b.AddThemeStyleboxOverride("focus", UiTheme.ArtBox("btn_focus", 0f, 0f));
        }
        else
        {
            b.AddThemeStyleboxOverride("normal", Box(RowBg, edge, 1, 2, 18, 6));
            b.AddThemeStyleboxOverride("hover", Box(new Color(0.125f, 0.105f, 0.088f, 0.96f), hot, 1, 2, 18, 6));
            b.AddThemeStyleboxOverride("pressed", Box(pressBg, hot.Lightened(0.15f), 1, 2, 18, 6));
            b.AddThemeStyleboxOverride("disabled", Box(new Color(0.05f, 0.048f, 0.046f, 0.72f), new Color(0.19f, 0.18f, 0.17f), 1, 2, 18, 6));
            b.AddThemeStyleboxOverride("focus", FocusBox());
        }'''),
    # Entry: plates
    ('''        var empty = new StyleBoxEmpty { ContentMarginLeft = 42, ContentMarginRight = 16, ContentMarginTop = 4, ContentMarginBottom = 4 };
        foreach (var st in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" }) b.AddThemeStyleboxOverride(st, empty);
        if (_title != null) b.AddThemeFontOverride("font", _title);
        b.AddThemeFontSizeOverride("font_size", size);
        b.AddThemeColorOverride("font_color", Ash);''', '''        bool plates = UiTheme.Art && UiArt.Has("menu_plate");
        var empty = new StyleBoxEmpty { ContentMarginLeft = plates ? 60 : 42, ContentMarginRight = plates ? 56 : 16, ContentMarginTop = 4, ContentMarginBottom = 4 };
        foreach (var st in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" }) b.AddThemeStyleboxOverride(st, empty);
        if (plates)
        {
            // forged entry plates: a quiet one at rest, the lit one (ember stones, bright hairline) under the pointer or the focus
            var idle = UiTheme.ArtBox("entry_idle", () => new UiNine("menu_plate") { FrameMod = new Color(1f, 1f, 1f, 0.62f) }.Margins(60f, 4f, 56f, 4f));
            var hotBox = UiTheme.ArtBox("entry_hot", () => new UiNine("menu_plate_hover").Margins(60f, 4f, 56f, 4f));
            b.AddThemeStyleboxOverride("normal", idle);
            b.AddThemeStyleboxOverride("disabled", UiTheme.ArtBox("entry_off", () => new UiNine("menu_plate") { FrameMod = new Color(0.7f, 0.7f, 0.7f, 0.4f) }.Margins(60f, 4f, 56f, 4f)));
            b.AddThemeStyleboxOverride("hover", hotBox);
            b.AddThemeStyleboxOverride("pressed", hotBox);
            b.AddThemeStyleboxOverride("hover_pressed", hotBox);
            b.AddThemeStyleboxOverride("focus", hotBox);
        }
        if (_title != null) b.AddThemeFontOverride("font", _title);
        b.AddThemeFontSizeOverride("font_size", size);
        b.AddThemeColorOverride("font_color", Ash);'''),
    ('''        b.Draw += () =>
        {
            if (b.Disabled || !(b.HasFocus() || b.IsHovered())) return;
            float cy = b.Size.Y * 0.5f + 1f, cx = 18f, r = size * 0.21f;''', '''        b.Draw += () =>
        {
            if (plates || b.Disabled || !(b.HasFocus() || b.IsHovered())) return;
            float cy = b.Size.Y * 0.5f + 1f, cx = 18f, r = size * 0.21f;'''),
    # Check: forged tick box
    ('''        b.Draw += () =>
        {
            float s = size + 2f, y = (b.Size.Y - s) * 0.5f;
            var r = new Rect2(6f, y, s, s);
            b.DrawRect(r, new Color(0.05f, 0.045f, 0.04f, 0.92f), true);''', '''        UiArt.Piece boxOff = UiTheme.Art ? UiArt.P("check_off") : null, boxOn = UiTheme.Art ? UiArt.P("check_on") : null;
        b.Draw += () =>
        {
            float s = size + 2f, y = (b.Size.Y - s) * 0.5f;
            var r = new Rect2(6f, y, s, s);
            if (boxOff != null && boxOn != null)
            {
                UiArt.Sprite(b, b.ButtonPressed ? boxOn : boxOff, r.Grow(2f), b.IsHovered() ? Colors.White : new Color(0.92f, 0.9f, 0.88f));
                return;
            }
            b.DrawRect(r, new Color(0.05f, 0.045f, 0.04f, 0.92f), true);'''),
    # Slider
    ('''        var track = Box(new Color(0.09f, 0.082f, 0.076f), Line.Darkened(0.2f), 1, 3);
        track.ContentMarginTop = 4; track.ContentMarginBottom = 4;
        s.AddThemeStyleboxOverride("slider", track);
        s.AddThemeStyleboxOverride("grabber_area", Box(Palette.Ember.Darkened(0.35f), Palette.Ember.Darkened(0.35f), 0, 3));
        s.AddThemeStyleboxOverride("grabber_area_highlight", Box(Palette.Ember.Darkened(0.1f), Palette.Ember.Darkened(0.1f), 0, 3));''', '''        if (UiTheme.Art && UiArt.Has("slider_track"))
        {
            // forged groove, lit fill and a rhombic knob: the theme's own slider art
            s.AddThemeStyleboxOverride("slider", UiTheme.ArtBox("slider_track", 0f, 5f));
            s.AddThemeStyleboxOverride("grabber_area", UiTheme.ArtBox("slider_fill", 0f, 5f));
            s.AddThemeStyleboxOverride("grabber_area_highlight", UiTheme.ArtBox("slider_fill", 0f, 5f));
            var g0 = UiArt.Icon("slider_grab"); var g1 = UiArt.Icon("slider_grab_hover");
            if (g0 != null && g1 != null)
            {
                s.AddThemeIconOverride("grabber", g0);
                s.AddThemeIconOverride("grabber_highlight", g1);
                s.AddThemeIconOverride("grabber_disabled", g0);
                return s;
            }
        }
        var track = FlatBox(new Color(0.09f, 0.082f, 0.076f), Line.Darkened(0.2f), 1, 3);
        track.ContentMarginTop = 4; track.ContentMarginBottom = 4;
        s.AddThemeStyleboxOverride("slider", track);
        s.AddThemeStyleboxOverride("grabber_area", Box(Palette.Ember.Darkened(0.35f), Palette.Ember.Darkened(0.35f), 0, 3));
        s.AddThemeStyleboxOverride("grabber_area_highlight", Box(Palette.Ember.Darkened(0.1f), Palette.Ember.Darkened(0.1f), 0, 3));'''),
])
print("ok")
