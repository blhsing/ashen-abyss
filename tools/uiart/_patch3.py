p = r"Y:\game\src\UiNine.cs"
s = open(p, encoding="utf-8").read()
old = """    /// <summary>Extra room the frame takes outside the box on every side (design px, may be negative).</summary>
    public float Grow;
"""
new = """    /// <summary>Extra room the frame takes outside the box on every side (design px, may be negative).</summary>
    public float Grow;
    /// <summary>
    /// A lighter frame (and top ornament) for boxes narrower or lower than <see cref="SmallBelow"/>: a confirm dialog
    /// should not carry the corner castings of a full window.
    /// </summary>
    public UiArt.Piece Small, SmallTop;
    public Vector2 SmallBelow;
"""
assert old in s; s = s.replace(old, new)
old = """            if (Frame != null)
            {
                if (Line)"""
new = """            var frame = Frame;
            var top = Top;
            if (Small != null && (rect.Size.X < SmallBelow.X || rect.Size.Y < SmallBelow.Y)) { frame = Small; top = SmallTop; }
            if (frame != null)
            {
                if (Line)"""
assert old in s; s = s.replace(old, new)
old = """                    float h = Frame.BoxSize.Y;
                    UiArt.Nine(ci, Frame, new Rect2(rect.Position.X, rect.Position.Y + MathF.Round((rect.Size.Y - h) * 0.5f), rect.Size.X, h), FrameMod);
                }
                else if (Frame.Kind == UiArt.Kind.Sprite) UiArt.Sprite(ci, Frame, rect, FrameMod);
                else UiArt.Nine(ci, Frame, rect, FrameMod);
            }"""
new = """                    float h = frame.BoxSize.Y;
                    UiArt.Nine(ci, frame, new Rect2(rect.Position.X, rect.Position.Y + MathF.Round((rect.Size.Y - h) * 0.5f), rect.Size.X, h), FrameMod);
                }
                else if (frame.Kind == UiArt.Kind.Sprite) UiArt.Sprite(ci, frame, rect, FrameMod);
                else UiArt.Nine(ci, frame, rect, FrameMod);
            }"""
assert old in s; s = s.replace(old, new)
old = """            if (Top != null && rect.Size.X >= TopMinWidth)
                UiArt.SpriteAt(ci, Top, new Vector2(rect.Position.X + MathF.Round(rect.Size.X * 0.5f), rect.Position.Y), Colors.White);"""
new = """            if (top != null && rect.Size.X >= (ReferenceEquals(top, Top) ? TopMinWidth : top.W * 0.5f + 80f))
                UiArt.SpriteAt(ci, top, new Vector2(rect.Position.X + MathF.Round(rect.Size.X * 0.5f), rect.Position.Y), Colors.White);"""
assert old in s; s = s.replace(old, new)
open(p, "w", encoding="utf-8", newline="\n").write(s)

p = r"Y:\game\src\UiArt.cs"
s = open(p, encoding="utf-8").read()
old = """        public Vector2 Anchor;
        public bool Center;
"""
new = """        public Vector2 Anchor;
        public bool Center;
        /// <summary>Drawn far below its authored size (tree nodes): the texture carries mipmaps.</summary>
        public bool Mip;
"""
assert old in s; s = s.replace(old, new)
old = """                    Tex[i] = LoadTexture((i == 1 ? Dir2 : Dir1) + Name + ".png");
                    if (Tex[i] == null && Tex[1 - i] == null && !Tried[1 - i])
                    {
                        Tried[1 - i] = true;
                        Tex[1 - i] = LoadTexture((i == 1 ? Dir1 : Dir2) + Name + ".png");
                    }"""
new = """                    Tex[i] = LoadTexture((i == 1 ? Dir2 : Dir1) + Name + ".png", Mip);
                    if (Tex[i] == null && Tex[1 - i] == null && !Tried[1 - i])
                    {
                        Tried[1 - i] = true;
                        Tex[1 - i] = LoadTexture((i == 1 ? Dir1 : Dir2) + Name + ".png", Mip);
                    }"""
assert old in s; s = s.replace(old, new)
old = """                    p.Center = d.ContainsKey("center") && d["center"].AsBool();"""
new = """                    p.Center = d.ContainsKey("center") && d["center"].AsBool();
                    p.Mip = d.ContainsKey("mip") && d["mip"].AsBool();"""
assert old in s; s = s.replace(old, new)
old = """    private static Texture2D LoadTexture(string path)
    {
        try
        {
            if (ResourceLoader.Exists(path))
            {
                var t = ResourceLoader.Load<Texture2D>(path);
                if (t != null) return t;
            }
        }"""
new = """    private static Texture2D LoadTexture(string path, bool mip = false)
    {
        try
        {
            if (ResourceLoader.Exists(path))
            {
                var t = ResourceLoader.Load<Texture2D>(path);
                if (t != null && mip)
                {
                    // small node sprites are drawn at a fraction of their size: give them a mip chain
                    var im = t.GetImage();
                    if (im != null && !im.IsEmpty())
                    {
                        if (im.IsCompressed()) im.Decompress();
                        im.GenerateMipmaps();
                        return ImageTexture.CreateFromImage(im);
                    }
                }
                if (t != null) return t;
            }
        }"""
assert old in s; s = s.replace(old, new)
old = """    /// <summary>The seamless panel-field tile with this name ("slate", "soot", "well" …), or null.</summary>"""
new = """    private static readonly Dictionary<string, Texture2D> Icons = new();

    /// <summary>
    /// A piece as a plain texture that reports its DESIGN size (half the x2 pixels), for theme icons and
    /// TextureRects: check boxes, slider grabbers, arrows. Always the x2 pixels, so it stays crisp on large screens.
    /// </summary>
    public static Texture2D Icon(string name)
    {
        if (name == null) return null;
        if (Icons.TryGetValue(name, out var cached)) return cached;
        Texture2D result = null;
        try
        {
            var p = P(name);
            if (p != null)
            {
                var src = LoadTexture(Dir2 + name + ".png") ?? LoadTexture(Dir1 + name + ".png");
                var im = src?.GetImage();
                if (im != null && !im.IsEmpty())
                {
                    if (im.IsCompressed()) im.Decompress();
                    var it = ImageTexture.CreateFromImage(im);
                    it.SetSizeOverride(new Vector2I(Mathf.RoundToInt(p.W * 0.5f), Mathf.RoundToInt(p.H * 0.5f)));
                    result = it;
                }
            }
        }
        catch (Exception e) { Log.Warn("uiart", $"icon '{name}' could not be prepared: {e.Message}"); }
        Icons[name] = result;
        return result;
    }

    /// <summary>The seamless panel-field tile with this name ("slate", "soot", "well" …), or null.</summary>"""
assert old in s; s = s.replace(old, new)
open(p, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
