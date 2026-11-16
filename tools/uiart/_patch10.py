def patch(path, pairs):
    s = open(path, encoding="utf-8").read()
    for a, b in pairs:
        assert s.count(a) == 1, (path, s.count(a), a[:90])
        s = s.replace(a, b)
    open(path, "w", encoding="utf-8", newline="\n").write(s)

# ── pieces: boss frame sized for the 20 px bar, a frame for the 12 px target bar ──
patch(r"Y:\tools\uiart\pieces.py", [
    ("@piece('boss_frame', 'h3', ml=150, mr=150, px=128, h=104, pad=20, blend=12, shadow=(8, 2, 4, 0.6))",
     "@piece('boss_frame', 'h3', ml=150, mr=150, px=128, h=120, pad=20, blend=12, shadow=(8, 2, 4, 0.6))"),
    ("        k.solid(k.rhombus(x, cy, 30, 30), [(0, 0), (0, 12), (3, 15), (7, 15), (9, 13)], 'plate', cap=True, name=\"end\")\n        S.jewel(k, x, cy, 9.5, 9.5, 13.0)",
     "        k.solid(k.rhombus(x, cy, 34, 38), [(0, 0), (0, 12), (3, 15), (7, 15), (9, 13)], 'plate', cap=True, name=\"end\")\n        S.jewel(k, x, cy, 10.5, 12.5, 13.0)"),
    ("@piece('boss_stagger', 'h3',", '''@piece('target_frame', 'h3', ml=36, mr=36, px=64, h=56, pad=6, blend=6, shadow=(4, 1, 2, 0.5))
def target_frame(k, S, m):
    # health-bar frame of the enemy under the pointer (12 px bar in the hole)
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    out = spear(x0, x1, cy, (y1 - y0) * 0.5, 16)
    k.solid(out, [(0, 0), (0, 4.5), (1.8, 6.3), (5, 6.3), (6.5, 4.5), (8, 3.8), (8.6, 3.8), (10, 1.2)], 'bar', cap=False, name="rim")
    for x in (x0 + 15, x1 - 15):
        k.solid(k.rhombus(x, cy, 10, 12), [(0, 2), (0, 5.5), (1.6, 7.1), (3.6, 7.1)], 'trim', cap=True, name="cap")
    k.catcher()


@piece('boss_stagger', 'h3','''),
])

# ── boss / target frame ──
patch(r"Y:\game\src\HudBossBar.cs", [
    ('''        var bar = new Rect2(x0, y, barW, barH);
        Framed(bar.Grow(3f), a);
        var inner = bar;''', '''        var bar = new Rect2(x0, y, barW, barH);
        var frameArt = _boss ? BossArt : TargetArt;
        if (frameArt == null) Framed(bar.Grow(3f), a);
        var inner = bar;'''),
    ('''        y += barH + 9f;

        if (!_boss) return;''', '''        // the forged frame lies over the ends of the bar: spear tips with ember stones for a boss, a light frame otherwise
        if (frameArt != null)
            UiArt.Nine(this, frameArt, _boss ? new Rect2(bar.Position.X - 34f, bar.Position.Y - 10f, bar.Size.X + 68f, bar.Size.Y + 20f)
                : new Rect2(bar.Position.X - 9f, bar.Position.Y - 5f, bar.Size.X + 18f, bar.Size.Y + 10f), new Color(1f, 1f, 1f, a));
        y += barH + (frameArt != null && _boss ? 14f : 9f);

        if (!_boss) return;'''),
    ('''        DrawRect(sb.Grow(1.5f), new Color(0f, 0f, 0f, 0.7f * a));
        DrawRect(sb, new Color(0.1f, 0.09f, 0.08f, 0.95f * a));''', '''        if (StaggerArt == null) DrawRect(sb.Grow(1.5f), new Color(0f, 0f, 0f, 0.7f * a));
        DrawRect(sb, new Color(0.1f, 0.09f, 0.08f, 0.95f * a));'''),
    ('''            DrawRect(new Rect2(sb.Position, sb.Size.X * _stagger, sb.Size.Y), new Color(UiTheme.Gold, 0.95f * a));
            DrawRect(new Rect2(sb.Position, sb.Size.X * _stagger, 2f), new Color(1f, 0.95f, 0.8f, 0.8f * a));
        }
    }''', '''            DrawRect(new Rect2(sb.Position, sb.Size.X * _stagger, sb.Size.Y), new Color(UiTheme.Gold, 0.95f * a));
            DrawRect(new Rect2(sb.Position, sb.Size.X * _stagger, 2f), new Color(1f, 0.95f, 0.8f, 0.8f * a));
        }
        if (StaggerArt != null) UiArt.Nine(this, StaggerArt, new Rect2(sb.Position.X - 6f, sb.Position.Y - 3f, sb.Size.X + 12f, sb.Size.Y + 6f), new Color(1f, 1f, 1f, a));
    }

    private static UiArt.Piece _bossArt, _targetArt, _staggerArt;
    private static bool _artLooked;
    private static void LookArt()
    {
        if (_artLooked) return;
        _artLooked = true;
        if (!UiTheme.Art) return;
        _bossArt = UiArt.P("boss_frame"); _targetArt = UiArt.P("target_frame"); _staggerArt = UiArt.P("boss_stagger");
    }
    private static UiArt.Piece BossArt { get { LookArt(); return _bossArt; } }
    private static UiArt.Piece TargetArt { get { LookArt(); return _targetArt; } }
    private static UiArt.Piece StaggerArt { get { LookArt(); return _staggerArt; } }'''),
])

# ── enemy bars and loot labels ──
patch(r"Y:\game\src\HudWorld.cs", [
    ('''        if (!hostile) return;
        if (m.Has(StatusId.Vulnerable)) DrawRect(r.Grow(1.5f), new Color(0.88f, 0.55f, 1f, 0.95f), false, 1f);
        else if (elite) DrawRect(r.Grow(1.5f), new Color(UiTheme.Gold, 0.8f), false, 1f);''', '''        if (!hostile) return;
        var barArt = BarArt;
        if (barArt != null && (elite || hot || m.Has(StatusId.Vulnerable)))
        {
            // a small forged frame for the bars that matter: gold for the marked, violet while vulnerable
            Color tint = m.Has(StatusId.Vulnerable) ? new Color(0.9f, 0.62f, 1f) : elite ? new Color(1f, 0.9f, 0.7f) : Colors.White;
            UiArt.Nine(this, barArt, new Rect2(r.Position.X - 4f, r.Position.Y - 2.5f, r.Size.X + 8f, r.Size.Y + 5f), tint);
        }
        else if (m.Has(StatusId.Vulnerable)) DrawRect(r.Grow(1.5f), new Color(0.88f, 0.55f, 1f, 0.95f), false, 1f);
        else if (elite) DrawRect(r.Grow(1.5f), new Color(UiTheme.Gold, 0.8f), false, 1f);'''),
    ('''            DrawRect(r, l.Hot ? new Color(0.17f, 0.13f, 0.07f, 0.94f) : new Color(0.02f, 0.02f, 0.025f, l.Loot ? 0.74f : 0.62f));
            if (l.Hot) DrawRect(r, new Color(UiTheme.Gold, 0.95f), false, 1f);
            else if (l.Fine) DrawRect(r, new Color(l.Col, 0.55f), false, 1f);''', '''            var hotArt = LabelHotArt;
            if (l.Hot && hotArt != null) UiArt.Nine(this, hotArt, r.Grow(2f), Colors.White);
            else
            {
                DrawRect(r, l.Hot ? new Color(0.17f, 0.13f, 0.07f, 0.94f) : new Color(0.02f, 0.02f, 0.025f, l.Loot ? 0.74f : 0.62f));
                var lineArt = LabelLineArt;
                if (l.Hot) DrawRect(r, new Color(UiTheme.Gold, 0.95f), false, 1f);
                else if (l.Fine && lineArt != null) UiArt.Nine(this, lineArt, r, new Color(l.Col, 0.8f));
                else if (l.Fine) DrawRect(r, new Color(l.Col, 0.55f), false, 1f);
            }'''),
    ('''    private void DrawBar(Monster m, Vector2 sp, bool elite, bool hot, bool hostile)
    {''', '''    private static UiArt.Piece _barArt, _labelHotArt, _labelLineArt;
    private static bool _artLooked;
    private static void LookArt()
    {
        if (_artLooked) return;
        _artLooked = true;
        if (!UiTheme.Art) return;
        _barArt = UiArt.P("plate_bar"); _labelHotArt = UiArt.P("plaque"); _labelLineArt = UiArt.P("boxline");
    }
    private static UiArt.Piece BarArt { get { LookArt(); return _barArt; } }
    private static UiArt.Piece LabelHotArt { get { LookArt(); return _labelHotArt; } }
    private static UiArt.Piece LabelLineArt { get { LookArt(); return _labelLineArt; } }

    private void DrawBar(Monster m, Vector2 sp, bool elite, bool hot, bool hostile)
    {'''),
])

# ── buff icons ──
patch(r"Y:\game\src\HudBuffs.cs", [
    ('''            Color frame = en.Bad ? new Color(0.85f, 0.25f, 0.2f, hot ? 1f : 0.85f) : new Color(UiTheme.Gold, hot ? 1f : 0.6f);
            DrawRect(r, frame, false, hot ? 2f : 1f);''', '''            Color frame = en.Bad ? new Color(0.85f, 0.25f, 0.2f, hot ? 1f : 0.85f) : new Color(UiTheme.Gold, hot ? 1f : 0.6f);
            if (_frameArt != null)
                UiArt.Sprite(this, _frameArt, r.Grow(2f), en.Bad ? new Color(1f, 0.5f, 0.45f) : hot ? Colors.White : new Color(0.88f, 0.86f, 0.84f));
            else DrawRect(r, frame, false, hot ? 2f : 1f);'''),
    ('''    public override void _Draw()
    {
        if (_n <= 0) return;''', '''    private readonly UiArt.Piece _frameArt = UiTheme.Art ? UiArt.P("buff_frame") : null;

    public override void _Draw()
    {
        if (_n <= 0) return;'''),
])

# ── notices: title rule, toast veils, subtitle band ──
patch(r"Y:\game\src\HudNotices.cs", [
    ('''            Flourish(cx, rule, 260f * Mathf.Min(1f, _titleT / 0.8f), new Color(UiTheme.Gold, 0.85f * a));''',
     '''            if (RuleArt != null) UiArt.SpriteAt(this, RuleArt, new Vector2(cx, rule + 3f), new Color(1f, 1f, 1f, a * Mathf.Min(1f, _titleT / 0.8f)), 1f);
            else Flourish(cx, rule, 260f * Mathf.Min(1f, _titleT / 0.8f), new Color(UiTheme.Gold, 0.85f * a));'''),
    ('''            float step = MathF.Round(body.GetHeight(px) * 0.92f);
            float y = Mathf.Max(ToastTop, titleBottom) + body.GetAscent(px);
            for (int i = 0; i < _toasts.Count; i++)
            {
                var t = _toasts[i];
                float a = Fade(t.T, ToastLife, 0.14f, 0.6f);
                float slide = (1f - Mathf.Min(1f, t.T / 0.16f)) * 8f;''', '''            float lineH = body.GetHeight(px);
            var veil = VeilArt;
            float step = veil != null ? MathF.Round(lineH + 6f) : MathF.Round(lineH * 0.92f);
            float y = Mathf.Max(ToastTop, titleBottom) + body.GetAscent(px);
            for (int i = 0; i < _toasts.Count; i++)
            {
                var t = _toasts[i];
                float a = Fade(t.T, ToastLife, 0.14f, 0.6f);
                float slide = (1f - Mathf.Min(1f, t.T / 0.16f)) * 8f;
                if (veil != null)
                {
                    // a translucent plaque behind the line: readable over fire and snow alike (width measured once per text)
                    if (t.W <= 0f || t.Px != px)
                    {
                        t.W = body.GetStringSize(t.Shown, HorizontalAlignment.Left, -1f, px).X; t.Px = px;
                        _toasts[i] = t;
                    }
                    UiArt.Nine(this, veil, new Rect2(MathF.Round(cx - t.W * 0.5f - 26f), MathF.Round(y + slide - body.GetAscent(px)), MathF.Round(t.W + 52f), MathF.Round(lineH + 2f)), new Color(1f, 1f, 1f, a));
                }'''),
    ('''        public string Text, Shown;''', '''        public string Text, Shown;
        public float W;         // measured width of Shown at Px (0 = measure again)
        public int Px;'''),
    ('''t.Shown = text + "　×" + t.Count; t.Col = c;''', '''t.Shown = text + "　×" + t.Count; t.Col = c; t.W = 0f;'''),
    ('''                DrawRect(band, new Color(0.02f, 0.02f, 0.025f, 0.66f * a));
                DrawRect(new Rect2(band.Position, w, 1f), new Color(UiTheme.Gold, 0.35f * a));
                DrawRect(new Rect2(band.Position.X, band.End.Y - 1f, w, 1f), new Color(UiTheme.Gold, 0.35f * a));''', '''                if (VeilArt != null) UiArt.Nine(this, VeilArt, band, new Color(1f, 1f, 1f, a));
                else
                {
                    DrawRect(band, new Color(0.02f, 0.02f, 0.025f, 0.66f * a));
                    DrawRect(new Rect2(band.Position, w, 1f), new Color(UiTheme.Gold, 0.35f * a));
                    DrawRect(new Rect2(band.Position.X, band.End.Y - 1f, w, 1f), new Color(UiTheme.Gold, 0.35f * a));
                }'''),
    ('''    public override void _Draw()
    {
        if (_idle) return;''', '''    private static UiArt.Piece _ruleArt, _veilArt;
    private static bool _artLooked;
    private static void LookArt()
    {
        if (_artLooked) return;
        _artLooked = true;
        if (!UiTheme.Art) return;
        _ruleArt = UiArt.P("title_rule"); _veilArt = UiArt.P("tracker");
    }
    private static UiArt.Piece RuleArt { get { LookArt(); return _ruleArt; } }
    private static UiArt.Piece VeilArt { get { LookArt(); return _veilArt; } }

    public override void _Draw()
    {
        if (_idle) return;'''),
])

# ── quest tracker backing ──
patch(r"Y:\game\src\Hud.cs", [
    ('''        _tracker.Visible = false;
        corner.AddChild(_tracker);''', '''        _tracker.Visible = false;
        if (UiTheme.Art)
            _tracker.AddThemeStyleboxOverride("normal", UiTheme.ArtBox("tracker", () => new UiNine("tracker").Margins(12f, 8f, 14f, 9f)));
        corner.AddChild(_tracker);'''),
])
print("ok")
