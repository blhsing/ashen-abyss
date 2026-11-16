using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// The raster interface art (res://assets/ui): forged frames, plates, slots and ornaments rendered from 3D models by
/// Y:\tools\uiart. Two texture sets are shipped — x2 (two texture pixels per design pixel, for screens above 1080p)
/// and x1 (exactly half) — and the set that matches the window is used, so the art is sampled close to 1:1 and no
/// mipmaps are needed. The manifest (manifest.json, written by slice.py) carries every piece's size, nine-slice
/// margins and shadow padding in x2 texture pixels.
///
/// Drawing never allocates: pieces are looked up once and kept by the callers, and the draw helpers only issue
/// RenderingServer commands. Rectangles are always given in design pixels and name the LOGICAL box of a piece; its
/// shadow / glow padding is drawn outside that box.
/// </summary>
public static class UiArt
{
    public enum Kind { Sprite, Nine, H3 }

    public sealed class Piece
    {
        public string Name;
        public Kind Kind;
        /// <summary>Texture size, nine-slice margins (including Pad), outer padding and anchor in x2 texture pixels.</summary>
        public float W, H, ML, MT, MR, MB, Pad;
        public Vector2 Anchor;
        public bool Center;
        /// <summary>Drawn far below its authored size (tree nodes): the texture carries mipmaps.</summary>
        public bool Mip;
        internal readonly Texture2D[] Tex = new Texture2D[2];
        internal readonly bool[] Tried = new bool[2];

        /// <summary>Size of the logical box in design pixels (texture minus padding).</summary>
        public Vector2 BoxSize => new((W - 2f * Pad) * 0.5f, (H - 2f * Pad) * 0.5f);
        /// <summary>Thickness of the nine-slice margins inside the logical box, in design pixels.</summary>
        public float InL => (ML - Pad) * 0.5f;
        public float InT => (MT - Pad) * 0.5f;
        public float InR => (MR - Pad) * 0.5f;
        public float InB => (MB - Pad) * 0.5f;

        public Texture2D Texture
        {
            get
            {
                int i = _set;
                if (Tex[i] == null && !Tried[i])
                {
                    Tried[i] = true;
                    Tex[i] = LoadTexture((i == 1 ? Dir2 : Dir1) + Name + ".png", Mip);
                    if (Tex[i] == null && Tex[1 - i] == null && !Tried[1 - i])
                    {
                        Tried[1 - i] = true;
                        Tex[1 - i] = LoadTexture((i == 1 ? Dir1 : Dir2) + Name + ".png", Mip);
                    }
                }
                return Tex[i];
            }
        }
    }

    private const string Root = "res://assets/ui/";
    private const string Dir1 = Root + "x1/", Dir2 = Root + "x2/";

    private static readonly Dictionary<string, Piece> Pieces = new();
    private static readonly HashSet<string> Missing = new();
    private static bool _loaded;
    private static int _set = 1;                 // 0 = x1, 1 = x2
    /// <summary>Texture pixels per x2 pixel (1 or 0.5) and design pixels per texture pixel (0.5 or 1) of the set in use.</summary>
    private static float _k = 1f, _s = 0.5f;

    /// <summary>The style key written by the art pipeline ("A" …); empty when no art is installed.</summary>
    public static string Style { get; private set; } = "";
    /// <summary>True when the manifest was found: the theme then dresses the interface with the raster art.</summary>
    public static bool Ready { get { Load(); return Pieces.Count > 0; } }
    /// <summary>Raised after the texture set changed (window resized across the 1080p line): redraw what is cached.</summary>
    public static event Action SetChanged;

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            string path = Root + "manifest.json";
            if (!FileAccess.FileExists(path)) { Log.Warn("uiart", "no interface art manifest (res://assets/ui/manifest.json): flat frames are used"); return; }
            var parsed = Json.ParseString(FileAccess.GetFileAsString(path));
            if (parsed.VariantType != Variant.Type.Dictionary) { Log.Warn("uiart", "manifest.json is not a dictionary"); return; }
            var man = parsed.AsGodotDictionary();
            Style = man.ContainsKey("style") ? man["style"].AsString() : "?";
            if (man.ContainsKey("pieces"))
                foreach (var kv in man["pieces"].AsGodotDictionary())
                {
                    var d = kv.Value.AsGodotDictionary();
                    var p = new Piece { Name = kv.Key.AsString() };
                    string kind = d.ContainsKey("kind") ? d["kind"].AsString() : "sprite";
                    p.Kind = kind == "nine" ? Kind.Nine : kind == "h3" ? Kind.H3 : Kind.Sprite;
                    p.W = F(d, "w"); p.H = F(d, "h"); p.Pad = F(d, "pad");
                    if (d.ContainsKey("m"))
                    {
                        var m = d["m"].AsGodotArray();
                        if (m.Count == 4) { p.ML = m[0].AsSingle(); p.MT = m[1].AsSingle(); p.MR = m[2].AsSingle(); p.MB = m[3].AsSingle(); }
                    }
                    if (d.ContainsKey("anchor"))
                    {
                        var a = d["anchor"].AsGodotArray();
                        if (a.Count == 2) p.Anchor = new Vector2(a[0].AsSingle(), a[1].AsSingle());
                    }
                    p.Center = d.ContainsKey("center") && d["center"].AsBool();
                    p.Mip = d.ContainsKey("mip") && d["mip"].AsBool();
                    Pieces[p.Name] = p;
                }
            if (man.ContainsKey("tiles"))
                foreach (var kv in man["tiles"].AsGodotDictionary())
                {
                    var d = kv.Value.AsGodotDictionary();
                    string name = "field_" + kv.Key.AsString();
                    Pieces[name] = new Piece { Name = name, Kind = Kind.Sprite, W = F(d, "w"), H = F(d, "h") };
                }
            Log.Info("uiart", $"interface art: style {Style}, {Pieces.Count} pieces");
        }
        catch (Exception e) { Log.Error("uiart", "the interface art manifest could not be read; flat frames are used", e); Pieces.Clear(); }
    }

    private static float F(Godot.Collections.Dictionary d, string key) => d.ContainsKey(key) ? d[key].AsSingle() : 0f;

    private static Texture2D LoadTexture(string path, bool mip = false)
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
        }
        catch (Exception e) { Log.Warn("uiart", $"{path}: import unavailable ({e.Message})"); }
        try
        {
            // not imported yet (a development run straight after the art was regenerated): read the PNG itself
            if (FileAccess.FileExists(path))
            {
                var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
                if (img != null && !img.IsEmpty()) return ImageTexture.CreateFromImage(img);
            }
        }
        catch (Exception e) { Log.Warn("uiart", $"{path}: direct load failed ({e.Message})"); }
        Log.Warn("uiart", $"texture missing: {path}");
        return null;
    }

    /// <summary>The piece with this name, or null (logged once) when the art set does not contain it.</summary>
    public static Piece P(string name)
    {
        Load();
        if (name != null && Pieces.TryGetValue(name, out var p)) return p;
        if (name != null && Pieces.Count > 0 && Missing.Add(name)) Log.Warn("uiart", $"piece '{name}' is not in the manifest");
        return null;
    }

    public static bool Has(string name) { Load(); return name != null && Pieces.ContainsKey(name); }

    private static readonly Dictionary<string, Texture2D> Icons = new();

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

    /// <summary>The seamless panel-field tile with this name ("slate", "soot", "well" …), or null.</summary>
    public static Piece Field(string name) => P("field_" + name);

    /// <summary>
    /// Chooses the texture set for the current window: x1 while the interface is drawn at up to one screen pixel per
    /// design pixel (1080p and smaller), x2 above. Call it when the window size changes.
    /// </summary>
    public static void FitWindow(Vector2 windowSize)
    {
        float scale = Mathf.Min(windowSize.X / 1920f, windowSize.Y / 1080f);
        int want = scale > 1.04f ? 1 : 0;
        if (want == _set) return;
        _set = want;
        _k = want == 1 ? 1f : 0.5f;
        _s = want == 1 ? 0.5f : 1f;
        Log.Info("uiart", $"texture set x{(want == 1 ? 2 : 1)} for a {windowSize.X:0}x{windowSize.Y:0} window (scale {scale:0.00})");
        try { SetChanged?.Invoke(); } catch (Exception e) { Log.Error("uiart", "a texture-set listener failed", e); }
    }

    // ───────────────────────── drawing (design pixels in, no allocations) ─────────────────────────

    /// <summary>Nine-slice (or three-slice) frame around the logical box <paramref name="box"/>; edges tile to fit.</summary>
    public static void Nine(Rid ci, Piece p, Rect2 box, Color mod)
    {
        if (p == null) return;
        var tex = p.Texture;
        if (tex == null) return;
        float k = _k, s = _s;
        float pad = p.Pad * k;
        float ml = p.ML * k, mt = p.MT * k, mr = p.MR * k, mb = p.MB * k;
        // a box smaller than the frame's own corners: shrink the whole frame instead of letting corners overlap
        float needW = (ml + mr - 2f * pad) * s, needH = (mt + mb - 2f * pad) * s;
        float f = 1f;
        if (needW > box.Size.X && needW > 0f) f = box.Size.X / needW;
        if (needH > box.Size.Y && needH > 0f) f = Mathf.Min(f, box.Size.Y / needH);
        if (f <= 0.01f) return;
        s *= f;
        var dst = new Rect2(0f, 0f, box.Size.X / s + 2f * pad, box.Size.Y / s + 2f * pad);
        RenderingServer.CanvasItemAddSetTransform(ci, new Transform2D(s, 0f, 0f, s, box.Position.X - pad * s, box.Position.Y - pad * s));
        RenderingServer.CanvasItemAddNinePatch(ci, dst, new Rect2(0f, 0f, p.W * k, p.H * k), tex.GetRid(), new Vector2(ml, mt), new Vector2(mr, mb),
            RenderingServer.NinePatchAxisMode.TileFit, p.Kind == Kind.H3 ? RenderingServer.NinePatchAxisMode.Stretch : RenderingServer.NinePatchAxisMode.TileFit,
            p.Center || p.Kind == Kind.H3, mod);
        RenderingServer.CanvasItemAddSetTransform(ci, Transform2D.Identity);
    }

    /// <summary>Fills <paramref name="rect"/> with a seamless tile at its native density (the pattern starts at the rect's corner).</summary>
    public static void Tile(Rid ci, Piece tile, Rect2 rect, Color mod)
    {
        if (tile == null || rect.Size.X <= 0f || rect.Size.Y <= 0f) return;
        var tex = tile.Texture;
        if (tex == null) return;
        float s = _s;
        RenderingServer.CanvasItemAddSetTransform(ci, new Transform2D(s, 0f, 0f, s, rect.Position.X, rect.Position.Y));
        RenderingServer.CanvasItemAddTextureRect(ci, new Rect2(0f, 0f, rect.Size.X / s, rect.Size.Y / s), tex.GetRid(), true, mod);
        RenderingServer.CanvasItemAddSetTransform(ci, Transform2D.Identity);
    }

    /// <summary>A fixed piece stretched so that its logical box covers <paramref name="box"/>.</summary>
    public static void Sprite(Rid ci, Piece p, Rect2 box, Color mod)
    {
        if (p == null) return;
        var tex = p.Texture;
        if (tex == null) return;
        float lw = p.W - 2f * p.Pad, lh = p.H - 2f * p.Pad;
        if (lw <= 0f || lh <= 0f) return;
        float sx = box.Size.X / lw, sy = box.Size.Y / lh;
        var dst = new Rect2(box.Position.X - p.Pad * sx, box.Position.Y - p.Pad * sy, p.W * sx, p.H * sy);
        RenderingServer.CanvasItemAddTextureRect(ci, dst, tex.GetRid(), false, mod);
    }

    /// <summary>A fixed piece at its native size (times <paramref name="scale"/>) with its anchor point on <paramref name="at"/>.</summary>
    public static void SpriteAt(Rid ci, Piece p, Vector2 at, Color mod, float scale = 1f)
    {
        if (p == null) return;
        var tex = p.Texture;
        if (tex == null) return;
        float s = 0.5f * scale;
        var dst = new Rect2(at.X - p.Anchor.X * s, at.Y - p.Anchor.Y * s, p.W * s, p.H * s);
        RenderingServer.CanvasItemAddTextureRect(ci, dst, tex.GetRid(), false, mod);
    }

    /// <summary>A fixed piece at its native size, centred on <paramref name="centre"/>.</summary>
    public static void SpriteCentered(Rid ci, Piece p, Vector2 centre, Color mod, float scale = 1f)
    {
        if (p == null) return;
        var tex = p.Texture;
        if (tex == null) return;
        float s = 0.5f * scale;
        var dst = new Rect2(centre.X - p.W * s * 0.5f, centre.Y - p.H * s * 0.5f, p.W * s, p.H * s);
        RenderingServer.CanvasItemAddTextureRect(ci, dst, tex.GetRid(), false, mod);
    }

    // convenience overloads for custom-drawn controls (call from _Draw)
    public static void Nine(CanvasItem c, Piece p, Rect2 box) => Nine(c.GetCanvasItem(), p, box, Colors.White);
    public static void Nine(CanvasItem c, Piece p, Rect2 box, Color mod) => Nine(c.GetCanvasItem(), p, box, mod);
    public static void Tile(CanvasItem c, Piece tile, Rect2 rect) => Tile(c.GetCanvasItem(), tile, rect, Colors.White);
    public static void Tile(CanvasItem c, Piece tile, Rect2 rect, Color mod) => Tile(c.GetCanvasItem(), tile, rect, mod);
    public static void Sprite(CanvasItem c, Piece p, Rect2 box) => Sprite(c.GetCanvasItem(), p, box, Colors.White);
    public static void Sprite(CanvasItem c, Piece p, Rect2 box, Color mod) => Sprite(c.GetCanvasItem(), p, box, mod);
    public static void SpriteAt(CanvasItem c, Piece p, Vector2 at) => SpriteAt(c.GetCanvasItem(), p, at, Colors.White);
    public static void SpriteAt(CanvasItem c, Piece p, Vector2 at, Color mod, float scale = 1f) => SpriteAt(c.GetCanvasItem(), p, at, mod, scale);
    public static void SpriteCentered(CanvasItem c, Piece p, Vector2 centre) => SpriteCentered(c.GetCanvasItem(), p, centre, Colors.White);
    public static void SpriteCentered(CanvasItem c, Piece p, Vector2 centre, Color mod, float scale = 1f) => SpriteCentered(c.GetCanvasItem(), p, centre, mod, scale);
}
