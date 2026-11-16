p = r"Y:\game\src\HudActionSlot.cs"
s = open(p, encoding="utf-8").read()
def rep(a, b, n=1):
    global s
    assert s.count(a) == n, (s.count(a), a[:90])
    s = s.replace(a, b)

rep("    private static StyleBoxFlat _boxNormal, _boxHover, _boxUlt, _boxEmpty, _boxLack;", "    private static StyleBox _boxNormal, _boxHover, _boxUlt, _boxEmpty, _boxLack;")
rep("    private StyleBoxFlat _style;", "    private StyleBox _style;")
rep("    private void SetStyle(StyleBoxFlat s)", "    private void SetStyle(StyleBox s)")
rep('''            TextureProgress = SweepTexture((int)size - 4), MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(2f, 2f), Visible = false,
        };
        AddChild(_sweep);

        _flash = new ColorRect { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(2f, 2f), Size = new Vector2(size - 4f, size - 4f), Visible = false };''',
'''            TextureProgress = SweepTexture((int)(size - inset * 2f)), MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(inset, inset), Visible = false,
        };
        AddChild(_sweep);

        _flash = new ColorRect { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(inset, inset), Size = new Vector2(size - inset * 2f, size - inset * 2f), Visible = false };''')
rep('''        float pad = Mathf.Round(size * 0.11f);
        _icon = new TextureRect''', '''        float pad = Mathf.Round(size * 0.11f);
        // the forged cell has a rim of about a tenth of its size: sweep and flash stay on the floor inside it
        float inset = UiTheme.Art ? MathF.Round(size * 0.09f) : 2f;
        _icon = new TextureRect''')
rep('''        if (_boxNormal != null) return;
        StyleBoxFlat make(Color bg, Color border, int px)''', '''        if (_boxNormal != null) return;
        for (int i = 0; i < Tenths.Length; i++) Tenths[i] = (i / 10) + "." + (i % 10);
        for (int i = 0; i < Whole.Length; i++) Whole[i] = i.ToString();
        if (UiTheme.Art)
        {
            // one forged cell; states are overlays: gold rim (hover), gold line (ultimate), dull red line (cannot cast)
            var cell = UiArt.P("skill_slot");
            _boxNormal = new UiNine { Frame = cell };
            _boxHover = new UiNine { Frame = cell, Over1 = UiArt.P("slot_hover") };
            _boxUlt = new UiNine { Frame = cell, Over1 = UiArt.P("slot_rarity"), Over1Mod = new Color(UiTheme.Gold, 0.85f) };
            _boxEmpty = new UiNine { Frame = cell, FrameMod = new Color(0.72f, 0.7f, 0.7f, 0.92f) };
            _boxLack = new UiNine { Frame = cell, Over1 = UiArt.P("skill_slot_off"), Over1Mod = new Color(0.62f, 0.16f, 0.12f), Back = new Color(0.3f, 0.03f, 0.02f, 0.28f), BackInset = 6f, BackOver = true };
            return;
        }
        StyleBoxFlat make(Color bg, Color border, int px)''')
rep('''        _boxLack = make(new Color(0.05f, 0.022f, 0.022f, 0.94f), new Color(0.42f, 0.13f, 0.1f), 2);
        for (int i = 0; i < Tenths.Length; i++) Tenths[i] = (i / 10) + "." + (i % 10);
        for (int i = 0; i < Whole.Length; i++) Whole[i] = i.ToString();''', '''        _boxLack = make(new Color(0.05f, 0.022f, 0.022f, 0.94f), new Color(0.42f, 0.13f, 0.1f), 2);''')
open(p, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
