def patch(path, pairs):
    s = open(path, encoding="utf-8").read()
    for a, b in pairs:
        assert s.count(a) == 1, (path, s.count(a), a[:90])
        s = s.replace(a, b)
    open(path, "w", encoding="utf-8", newline="\n").write(s)

# ── globe + minimap shaders: with the raster ring the procedural iron ring is switched off ──
patch(r"Y:\game\src\HudGlobe.cs", [
    ('''uniform vec4 shell : source_color = vec4(0.78, 0.9, 1.0, 1.0);
const float R = 0.84;''', '''uniform vec4 shell : source_color = vec4(0.78, 0.9, 1.0, 1.0);
uniform float art = 0.0;
const float R = 0.84;'''),
    ('''	vec3 outc = mix(col, iron_ring(p, r, px), ring);
	float alpha = 1.0 - smoothstep(1.0 - px * 2.0, 1.0, r);
	COLOR = vec4(outc, alpha);
}
";

    private const string MapCode''', '''	vec3 outc = mix(col, iron_ring(p, r, px), ring);
	float alpha = 1.0 - smoothstep(1.0 - px * 2.0, 1.0, r);
	// art = 1: only the glass is drawn here, the forged ring is a texture laid over it
	alpha = mix(alpha, 1.0 - smoothstep(R, R + px * 2.0, r), art);
	COLOR = vec4(mix(outc, col, art), alpha);
}
";

    private const string MapCode'''),
    ('''uniform float has_map = 0.0;
const float R = 0.9;''', '''uniform float has_map = 0.0;
uniform float art = 0.0;
const float R = 0.9;'''),
    ('''	float alpha = (1.0 - smoothstep(1.0 - px * 2.0, 1.0, r)) * mix(1.0, 0.84, inner);
	COLOR = vec4(outc, alpha);''', '''	float alpha = (1.0 - smoothstep(1.0 - px * 2.0, 1.0, r)) * mix(1.0, 0.84, inner);
	alpha = mix(alpha, (1.0 - smoothstep(R, R + px * 2.0, r)) * 0.86, art);
	COLOR = vec4(mix(outc, col, art), alpha);'''),
    ('''        AddChild(_rect);

        // the numbers shown while the pointer rests on the glass''', '''        AddChild(_rect);

        if (UiTheme.Art && UiArt.Has("globe_rim"))
        {
            // the forged ring with its crown and cradle lies over the glass; the shader then draws the glass only
            _mat.SetShaderParameter("art", 1f);
            var piece = UiArt.P("globe_rim");
            var rim = new Control { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(0f, PipArea), Size = new Vector2(Diameter, Diameter) };
            rim.Draw += () => UiArt.Sprite(rim, piece, new Rect2(0f, 0f, Diameter, Diameter));
            AddChild(rim);
            _rimArt = true;
        }

        // the numbers shown while the pointer rests on the glass'''),
    ('''    private string _readText = "";
''', '''    private string _readText = "";
    private readonly bool _rimArt;
'''),
    ('''        float radius = Diameter * 0.5f + 14f;''', '''        float radius = Diameter * 0.5f + (_rimArt ? 23f : 14f);      // clear of the ring's crown'''),
])

patch(r"Y:\game\src\HudMinimap.cs", [
    ('''        _over.Draw += DrawOverlay;
        AddChild(_over);
    }''', '''        _over.Draw += DrawOverlay;
        AddChild(_over);
        if (UiTheme.Art && UiArt.Has("minimap_ring"))
        {
            _mat.SetShaderParameter("art", 1f);
            _ring = UiArt.P("minimap_ring");
            var ring = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(Diameter, Diameter) };
            ring.Draw += () => UiArt.Sprite(ring, _ring, new Rect2(0f, 0f, Diameter, Diameter));
            AddChild(ring);
        }
    }

    private readonly UiArt.Piece _ring;'''),
])

# ── bottom plate and experience bar ──
patch(r"Y:\game\src\HudBar.cs", [
    ('''    public override void _Draw()
    {
        Rect2 Plate = _plate, XpRect = _xpRect;
        DrawStyleBox(_plateBox, Plate);
        // ember-gold hairlines: the thin trim that runs through the whole interface
        DrawRect(new Rect2(Plate.Position.X + 40f, Plate.Position.Y + 3f, Plate.Size.X - 80f, 1f), new Color(UiTheme.Gold, 0.3f));
        DrawRect(new Rect2(Plate.Position.X + 40f, Plate.End.Y - 3f, Plate.Size.X - 80f, 1f), new Color(UiTheme.Gold, 0.12f));

        // experience
        DrawRect(XpRect.Grow(2.5f), new Color(0f, 0f, 0f, 0.8f));
        DrawRect(XpRect.Grow(1.5f), new Color(UiTheme.Border, 0.9f));
        DrawRect(XpRect, new Color(0.05f, 0.046f, 0.045f));''', '''    public override void _Draw()
    {
        Rect2 Plate = _plate, XpRect = _xpRect;
        if (_plateArt != null)
        {
            // forged base plate (its ends disappear behind the globes) and a trough frame round the experience bar
            UiArt.Nine(this, _plateArt, Plate);
            DrawRect(XpRect.Grow(1f), new Color(0.03f, 0.028f, 0.03f));
        }
        else
        {
            DrawStyleBox(_plateBox, Plate);
            // ember-gold hairlines: the thin trim that runs through the whole interface
            DrawRect(new Rect2(Plate.Position.X + 40f, Plate.Position.Y + 3f, Plate.Size.X - 80f, 1f), new Color(UiTheme.Gold, 0.3f));
            DrawRect(new Rect2(Plate.Position.X + 40f, Plate.End.Y - 3f, Plate.Size.X - 80f, 1f), new Color(UiTheme.Gold, 0.12f));

            // experience
            DrawRect(XpRect.Grow(2.5f), new Color(0f, 0f, 0f, 0.8f));
            DrawRect(XpRect.Grow(1.5f), new Color(UiTheme.Border, 0.9f));
            DrawRect(XpRect, new Color(0.05f, 0.046f, 0.045f));
        }'''),
    ('''        for (int i = 1; i < 10; i++)
            DrawRect(new Rect2(XpRect.Position.X + XpRect.Size.X * i / 10f, XpRect.Position.Y, 1f, XpRect.Size.Y), new Color(0f, 0f, 0f, 0.55f));
''', '''        for (int i = 1; i < 10; i++)
            DrawRect(new Rect2(XpRect.Position.X + XpRect.Size.X * i / 10f, XpRect.Position.Y, 1f, XpRect.Size.Y), new Color(0f, 0f, 0f, 0.55f));
        if (_xpArt != null) UiArt.Nine(this, _xpArt, new Rect2(XpRect.Position.X - 14f, XpRect.Position.Y - 3f, XpRect.Size.X + 28f, XpRect.Size.Y + 6f));
'''),
    ('''        _plateBox.SetCornerRadiusAll(5);
''', '''        _plateBox.SetCornerRadiusAll(5);
        if (UiTheme.Art)
        {
            _plateArt = UiArt.P("hud_plate");
            _xpArt = UiArt.P("xp_frame");
        }
'''),
    ('''    private readonly StyleBoxFlat _plateBox;
''', '''    private readonly StyleBoxFlat _plateBox;
    private readonly UiArt.Piece _plateArt, _xpArt;
'''),
])
print("ok")
