def patch(path, pairs):
    s = open(path, encoding="utf-8").read()
    for a, b in pairs:
        assert s.count(a) == 1, (path, s.count(a), a[:90])
        s = s.replace(a, b)
    open(path, "w", encoding="utf-8", newline="\n").write(s)

patch(r"Y:\game\src\MainMenu.cs", [
    ('''    private static StyleBoxFlat ClassBox(Color bg, Color border)
    {''', '''    private static StyleBox ClassBox(Color bg, Color border)
    {'''),
])
patch(r"Y:\game\src\SettingsUi.cs", [
    ('''        var sb = MenuKit.Box(new Color(0.115f, 0.105f, 0.095f, 0.98f), MenuKit.Line.Lightened(0.1f), 1, 4, 10, 2);
        sb.BorderWidthBottom = 3;
        p.AddThemeStyleboxOverride("panel", sb);''', '''        if (UiTheme.Art && UiArt.Has("keycap")) p.AddThemeStyleboxOverride("panel", UiTheme.ArtBox("keycap", 11f, 3f));
        else
        {
            var sb = MenuKit.FlatBox(new Color(0.115f, 0.105f, 0.095f, 0.98f), MenuKit.Line.Lightened(0.1f), 1, 4, 10, 2);
            sb.BorderWidthBottom = 3;
            p.AddThemeStyleboxOverride("panel", sb);
        }'''),
])
print("ok")
