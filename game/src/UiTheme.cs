using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// The look of every menu and HUD element: dark forged iron and soot, thin ember-gold trims, parchment text.
/// Builds one Godot Theme (fonts, sizes, frames, button states, scrollbars, sliders, tabs, tooltips) exactly once
/// and offers the small widget factories the panels are assembled from. Everything is created lazily and shared,
/// so calling these helpers from a panel constructor during boot is safe.
/// </summary>
public static class UiTheme
{
    // ───────────────────────── palette ─────────────────────────
    public static readonly Color Bg = new(0.040f, 0.038f, 0.044f, 0.97f);        // soot: backdrops and tooltips
    public static readonly Color Panel = new(0.078f, 0.072f, 0.074f, 0.98f);     // forged iron: window bodies
    public static readonly Color Border = new(0.33f, 0.27f, 0.19f);              // dull bronze edge
    public static readonly Color Gold = new(0.93f, 0.73f, 0.38f);                // ember-gold trim and titles
    public static readonly Color Text = Palette.Text;                            // parchment
    public static readonly Color Dim = Palette.TextDim;
    public static readonly Color Inset = new(0.028f, 0.026f, 0.031f, 0.96f);     // recessed wells: slots, fields
    public static readonly Color Raised = new(0.112f, 0.102f, 0.098f, 0.98f);    // buttons, tabs
    public static readonly Color Ember = Palette.Ember;

    private const string FontDir = "res://assets/fonts/";

    private static bool _fontsReady;
    private static Font _title, _body, _ui, _uiBold, _display, _italic;
    private static Theme _theme;
    private static StyleBoxFlat _frameBox, _heavyBox, _tipBox;
    private static readonly Dictionary<string, UiNine> ArtBoxes = new();
    private static readonly Dictionary<int, StyleBox> SlotBoxes = new();

    // ───────────────────────── text scale ─────────────────────────
    //
    // The interface is laid out for 1920×1080 and stretched to the real screen. Traditional Chinese glyphs are dense,
    // so EVERY font size in the game goes through Fs(): it applies the player's text-size setting and never returns
    // less than MinFont. The widget factories below (Label, Small, Button, Rich …) take DESIGN sizes and call Fs()
    // themselves; code that sets a size directly (AddThemeFontSizeOverride, DrawString, BBCode [font_size=N]) must
    // call Fs() — or Bb() for a finished BBCode string — exactly once.

    /// <summary>Nothing renders below this many design pixels (at 1080p), whatever the text scale.</summary>
    public const int MinFont = 20;
    /// <summary>The house scale in design pixels: captions and counters, list rows, running text, names in tooltips, panel titles, big headings.</summary>
    public const int FsCaption = 20, FsRow = 22, FsBody = 24, FsName = 28, FsTitle = 34, FsHeading = 44;
    /// <summary>Multiplier from Settings.UiTextScale: 0 標準 = 1.0, 1 大 = 1.15, 2 特大 = 1.3. Change it through <see cref="ApplyTextScale"/>.</summary>
    public static float TextScale = 1f;
    /// <summary>The Settings.UiTextScale value in force (0, 1 or 2).</summary>
    public static int TextScaleSetting { get; private set; }
    /// <summary>
    /// Raised after the scale changed and the shared Theme was updated; the argument is the scale that applied before.
    /// Owners of control trees (Ui, Hud, MainMenu) answer with <see cref="Rescale"/> on their tree.
    /// </summary>
    public static event Action<float> TextScaleChanged;

    private static bool _scaleRead, _applying;

    public static float ScaleOf(int setting) => setting <= 0 ? 1f : setting == 1 ? 1.15f : 1.3f;

    /// <summary>A design font size (px at 1080p) → the size to use: scaled by the text-size setting, never below MinFont.</summary>
    public static int Fs(int designSize)
    {
        if (!_scaleRead) ReadSavedScale();
        return Math.Max(MinFont, (int)MathF.Round(designSize * TextScale));
    }

    /// <summary>A design length that has to grow with the text (row heights, label columns, text boxes): scaled, not floored.</summary>
    public static float Px(float design)
    {
        if (!_scaleRead) ReadSavedScale();
        return MathF.Round(design * TextScale);
    }

    /// <summary>Width of the text column of a tooltip card: wider than the old 400 px, and a little wider again with larger text.</summary>
    public static float TipWidth => MathF.Round(500f * (1f + (TextScale - 1f) * 0.6f));

    /// <summary>
    /// Rewrites every <c>[font_size=N]</c> of a BBCode string through <see cref="Fs"/>. Apply it exactly once per
    /// string (a second pass would scale the sizes again): either where the string is built or where it is shown.
    /// </summary>
    public static string Bb(string bbcode) => MapFontTags(bbcode, 1f);

    private const string FontTag = "[font_size=";

    /// <summary>Maps the N of every [font_size=N]: N was written for <paramref name="writtenAt"/> (1 = plain design sizes).</summary>
    private static string MapFontTags(string bb, float writtenAt)
    {
        if (string.IsNullOrEmpty(bb)) return bb ?? "";
        int at = bb.IndexOf(FontTag, StringComparison.Ordinal);
        if (at < 0) return bb;
        var sb = new System.Text.StringBuilder(bb.Length + 8);
        int from = 0;
        while (at >= 0)
        {
            int d0 = at + FontTag.Length, d1 = d0;
            int n = 0;
            while (d1 < bb.Length && bb[d1] >= '0' && bb[d1] <= '9' && d1 - d0 < 4) { n = n * 10 + (bb[d1] - '0'); d1++; }
            sb.Append(bb, from, d0 - from);
            if (d1 > d0) sb.Append(Fs(DesignOf(n, writtenAt)));
            from = d1;
            at = bb.IndexOf(FontTag, from, StringComparison.Ordinal);
        }
        sb.Append(bb, from, bb.Length - from);
        return sb.ToString();
    }

    /// <summary>The design size behind a size that was produced by Fs() while <paramref name="scale"/> applied.</summary>
    private static int DesignOf(int size, float scale) => scale <= 1.001f ? size : (int)MathF.Round(size / scale);

    /// <summary>
    /// The saved setting is read the first time a size is asked for (Game loads its settings before it builds any
    /// interface). Until the settings exist the scale stays at 1 and the question is asked again.
    /// </summary>
    private static void ReadSavedScale()
    {
        try
        {
            var cfg = Game.I?.Cfg;
            if (cfg == null) return;
            _scaleRead = true;
            int setting = Math.Clamp(cfg.UiTextScale, 0, 2);
            // test harness: --textscale=0|1|2 overrides the saved setting for this run only (nothing is written back)
            string forced = Game.I.ArgValue("textscale");
            if (!string.IsNullOrEmpty(forced) && int.TryParse(forced, out int fs))
            {
                setting = Math.Clamp(fs, 0, 2);
                Log.Info("ui", $"--textscale: text scale forced to {setting} for this run");
            }
            if (_theme != null && !Mathf.IsEqualApprox(ScaleOf(setting), TextScale)) { ApplyTextScale(setting); return; }
            TextScaleSetting = setting;
            TextScale = ScaleOf(setting);
            Log.Info("ui", $"text scale from settings: {setting} (×{TextScale:0.00}), minimum font {MinFont} px");
        }
        catch (Exception e)
        {
            _scaleRead = true;
            Log.Warn("ui", "text scale could not be read from the settings, using 1.0: " + e.Message);
        }
    }

    /// <summary>
    /// Switches the text size (Settings.UiTextScale: 0 標準, 1 大, 2 特大): updates <see cref="TextScale"/>, rewrites the
    /// font sizes of the shared Theme resource in place (everything that reads the theme follows by itself) and raises
    /// <see cref="TextScaleChanged"/> so the panel manager, the HUD and the title screen rescale what they built.
    /// </summary>
    public static void ApplyTextScale(int setting)
    {
        setting = Math.Clamp(setting, 0, 2);
        _scaleRead = true;
        float old = TextScale, now = ScaleOf(setting);
        TextScaleSetting = setting;
        if (Mathf.IsEqualApprox(old, now) || _applying) return;
        _applying = true;
        try
        {
            TextScale = now;
            if (_theme != null)
            {
                try { FillSizes(_theme); }
                catch (Exception e) { Log.Error("ui", "theme font sizes could not be rebuilt", e); }
            }
            Log.Info("ui", $"text scale {old:0.00} -> {now:0.00} (setting {setting})");
            var handlers = TextScaleChanged;
            if (handlers != null)
                foreach (Delegate d in handlers.GetInvocationList())
                {
                    try { ((Action<float>)d)(old); }
                    catch (Exception e) { Log.Error("ui", "a text-scale listener failed", e); }
                }
        }
        finally { _applying = false; }
    }

    private static readonly StringName MetaMin = "ui_min";
    private static readonly StringName MetaFixed = "ui_fixed";

    /// <summary>
    /// Marks a control whose font size its owner manages by hand (a logo, a display numeral): <see cref="Rescale"/>
    /// leaves its font-size overrides alone when the text scale changes.
    /// </summary>
    public static T FixedSize<T>(T c) where T : Control
    {
        c.SetMeta(MetaFixed, true);
        return c;
    }
    private static readonly StringName[] SizeItems =
    {
        "font_size", "normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size", "mono_font_size",
        "title_button_font_size", "font_separator_size",
    };

    /// <summary>
    /// Gives a control a minimum size in design pixels that grows with the text scale, and keeps it in step when the
    /// scale changes later (through <see cref="Rescale"/>). Pass 0 for an axis that should not be constrained.
    /// </summary>
    public static T MinSize<T>(T c, float designW, float designH) where T : Control
    {
        c.SetMeta(MetaMin, new Vector2(designW, designH));
        c.CustomMinimumSize = new Vector2(Px(designW), Px(designH));
        return c;
    }

    /// <summary>
    /// Brings a whole control tree (in the scene or not) in step with the current text scale: font-size overrides,
    /// [font_size] tags of BBCode already put into RichTextLabels and <see cref="MinSize"/> boxes are recomputed from
    /// their design values, and custom-drawn controls are asked to redraw. <paramref name="oldScale"/> is the scale
    /// the tree was built with (the argument of <see cref="TextScaleChanged"/>). Only for trees whose sizes all came
    /// from Fs().
    /// </summary>
    public static void Rescale(Node root, float oldScale)
    {
        if (root == null || !GodotObject.IsInstanceValid(root)) return;
        try { RescaleNode(root, oldScale); }
        catch (Exception e) { Log.Error("ui", "rescaling a control tree failed", e); }
    }

    /// <summary>Asks every canvas item under <paramref name="root"/> to redraw (after the art's texture set changed).</summary>
    public static void RedrawAll(Node root)
    {
        if (root == null || !GodotObject.IsInstanceValid(root)) return;
        if (root is CanvasItem ci) ci.QueueRedraw();
        int count = root.GetChildCount();
        for (int i = 0; i < count; i++) RedrawAll(root.GetChild(i));
    }

    private static void RescaleNode(Node n, float oldScale)
    {
        if (n is Control c)
        {
            bool fixedSize = c.HasMeta(MetaFixed);
            for (int i = 0; i < SizeItems.Length && !fixedSize; i++)
            {
                if (!c.HasThemeFontSizeOverride(SizeItems[i])) continue;
                int cur = c.GetThemeFontSize(SizeItems[i]);
                int want = Fs(DesignOf(cur, oldScale));
                if (want != cur) c.AddThemeFontSizeOverride(SizeItems[i], want);
            }
            if (c.HasMeta(MetaMin))
            {
                Vector2 d = c.GetMeta(MetaMin).AsVector2();
                c.CustomMinimumSize = new Vector2(Px(d.X), Px(d.Y));
            }
            if (c is RichTextLabel rich && rich.BbcodeEnabled)
            {
                string text = rich.Text;
                if (!string.IsNullOrEmpty(text) && text.Contains(FontTag, StringComparison.Ordinal))
                {
                    string mapped = MapFontTags(text, oldScale);
                    if (mapped != text) rich.Text = mapped;
                }
            }
        }
        if (n is CanvasItem ci) ci.QueueRedraw();
        int count = n.GetChildCount();
        for (int i = 0; i < count; i++) RescaleNode(n.GetChild(i), oldScale);
    }

    // ───────────────────────── fonts ─────────────────────────

    /// <summary>Noto Serif TC Bold — headings and names.</summary>
    public static Font Title { get { EnsureFonts(); return _title; } }
    /// <summary>Noto Serif TC Medium — running text.</summary>
    public static Font Body { get { EnsureFonts(); return _body; } }
    /// <summary>Noto Sans TC Medium — numbers and small labels.</summary>
    public static Font Ui { get { EnsureFonts(); return _ui; } }
    /// <summary>Noto Sans TC Bold — damage numbers, counters, key caps.</summary>
    public static Font UiBold { get { EnsureFonts(); return _uiBold; } }
    /// <summary>Cinzel for Latin display lettering, falling back to the serif title face for Chinese.</summary>
    public static Font Display { get { EnsureFonts(); return _display; } }

    public static Theme Theme
    {
        get
        {
            if (_theme == null) _theme = BuildTheme();
            return _theme;
        }
    }

    private static Font LoadFont(string file)
    {
        string path = FontDir + file;
        try
        {
            if (ResourceLoader.Exists(path))
            {
                var f = ResourceLoader.Load<Font>(path);
                if (f != null)
                {
                    // CJK text must come from the plain rasteriser: a distance field blurs dense strokes at these sizes,
                    // and the engine already rasterises dynamic fonts at the stretched (2× on 4K) size by itself
                    if (f is FontFile imported && imported.MultichannelSignedDistanceField)
                    {
                        Log.Warn("ui", $"font {file} was imported as MSDF; switching it back to plain rasterisation");
                        imported.MultichannelSignedDistanceField = false;
                    }
                    return f;
                }
            }
        }
        catch (Exception e) { Log.Warn("ui", $"font {file}: import unavailable ({e.Message})"); }
        try
        {
            // not imported (yet): read the font file directly
            if (Godot.FileAccess.FileExists(path))
            {
                var ff = new FontFile();
                if (ff.LoadDynamicFont(path) == Error.Ok)
                {
                    ff.Antialiasing = TextServer.FontAntialiasing.Gray;
                    ff.GenerateMipmaps = false;
                    ff.MultichannelSignedDistanceField = false;
                    ff.AllowSystemFallback = true;
                    ff.SubpixelPositioning = TextServer.SubpixelPositioning.Auto;
                    return ff;
                }
            }
        }
        catch (Exception e) { Log.Warn("ui", $"font {file}: dynamic load failed ({e.Message})"); }
        Log.Warn("ui", $"font {file} missing, falling back");
        return null;
    }

    /// <summary>
    /// The machine's own Traditional Chinese face (Microsoft JhengHei on Windows): the last link of every fallback
    /// chain, and the stand-in when one of our font files cannot be loaded.
    /// </summary>
    private static Font SystemCjk(bool bold)
    {
        try
        {
            return new SystemFont
            {
                FontNames = new[] { "Microsoft JhengHei", "Microsoft JhengHei UI", "PMingLiU", "Noto Sans CJK TC", "PingFang TC", "sans-serif" },
                FontWeight = bold ? 700 : 400,
                Antialiasing = TextServer.FontAntialiasing.Gray,
                GenerateMipmaps = false,
                MultichannelSignedDistanceField = false,
                AllowSystemFallback = true,
                SubpixelPositioning = TextServer.SubpixelPositioning.Auto,
            };
        }
        catch (Exception e)
        {
            Log.Warn("ui", "system font 'Microsoft JhengHei' is unavailable: " + e.Message);
            return null;
        }
    }

    private static void Chain(Font font, params Font[] fallbacks)
    {
        if (font == null) return;
        var list = new Godot.Collections.Array<Font>();
        foreach (var f in fallbacks)
            if (f != null && !ReferenceEquals(f, font) && !list.Contains(f)) list.Add(f);
        if (list.Count > 0) font.Fallbacks = list;
    }

    private static void EnsureFonts()
    {
        if (_fontsReady) return;
        _fontsReady = true;
        Font serif = null, serifBold = null, sans = null, sansBold = null, cinzel = null;
        try
        {
            serif = LoadFont("NotoSerifTC-Medium.otf") ?? LoadFont("NotoSerifTC-Regular.otf");
            serifBold = LoadFont("NotoSerifTC-Bold.otf");
            sans = LoadFont("NotoSansTC-Medium.otf") ?? LoadFont("NotoSansTC-Regular.otf");
            sansBold = LoadFont("NotoSansTC-Bold.otf");
            cinzel = LoadFont("Cinzel-Variable.ttf");
        }
        catch (Exception e) { Log.Error("ui", "font loading failed", e); }

        Font sys = SystemCjk(false), sysBold = SystemCjk(true);
        if (serif == null || serifBold == null || sans == null || sansBold == null)
        {
            string missing = string.Join(", ", new[] { serif == null ? "NotoSerifTC-Medium" : null, serifBold == null ? "NotoSerifTC-Bold" : null,
                sans == null ? "NotoSansTC-Medium" : null, sansBold == null ? "NotoSansTC-Bold" : null }.Where(s => s != null));
            Log.Warn("ui", $"font files not loaded ({missing}); the system font 'Microsoft JhengHei' stands in" + (sys == null ? " — and it is unavailable too, using the engine fallback" : ""));
        }

        Font fallback = ThemeDB.FallbackFont;
        _body = serif ?? sans ?? sys ?? fallback;
        _title = serifBold ?? serif ?? sansBold ?? sysBold ?? _body;
        _ui = sans ?? sys ?? _body;
        _uiBold = sansBold ?? sans ?? sysBold ?? _ui;

        try
        {
            // serif → sans → system, sans → system (never the other way: no cycles), so Latin, digits, punctuation and
            // every CJK glyph the serif face lacks still render
            Chain(sans, sys);
            Chain(sansBold, sysBold ?? sys);
            Chain(serif, sans, sys);
            Chain(serifBold, sansBold ?? sans, sysBold ?? sys);
            _italic = new FontVariation { BaseFont = _body, VariationTransform = new Transform2D(1f, 0.2f, 0f, 1f, 0f, 0f) };
            if (cinzel != null)
            {
                var disp = new FontVariation { BaseFont = cinzel, VariationEmbolden = 0.4f };
                disp.Fallbacks = new Godot.Collections.Array<Font> { _title };
                _display = disp;
            }
        }
        catch (Exception e) { Log.Error("ui", "font setup failed", e); }
        _italic ??= _body;
        _display ??= _title;
        Log.Info("ui", $"fonts ready: serif={(serif != null)} serifBold={(serifBold != null)} sans={(sans != null)} sansBold={(sansBold != null)} cinzel={(cinzel != null)} system={(sys != null)} (plain rasterised, no MSDF; oversampling follows the stretched viewport)");
    }

    // ───────────────────────── style boxes ─────────────────────────

    private static StyleBoxFlat Flat(Color bg, Color border, int borderPx = 1, int radius = 3, float padX = -1f, float padY = -1f)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border, AntiAliasing = true };
        s.SetBorderWidthAll(borderPx);
        s.SetCornerRadiusAll(radius);
        if (padX >= 0f) { s.ContentMarginLeft = padX; s.ContentMarginRight = padX; }
        if (padY >= 0f) { s.ContentMarginTop = padY; s.ContentMarginBottom = padY; }
        return s;
    }

    // ── raster art (res://assets/ui, see UiArt): every frame below prefers it and falls back to the flat boxes ──

    /// <summary>True when the raster interface art is installed: frames, buttons and slots are drawn from it.</summary>
    public static bool Art => UiArt.Ready;

    /// <summary>A shared art-backed style box, built once per key.</summary>
    public static UiNine ArtBox(string key, Func<UiNine> make)
    {
        if (!ArtBoxes.TryGetValue(key, out var b)) ArtBoxes[key] = b = make();
        return b;
    }

    /// <summary>A shared art-backed style box for one frame piece with content margins (design px).</summary>
    public static UiNine ArtBox(string frame, float padX, float padY, string field = null, float fieldInset = 0f)
    {
        return ArtBox($"{frame}|{field}|{padX}|{padY}|{fieldInset}", () => new UiNine(frame, field, fieldInset, padX, padY));
    }

    /// <summary>
    /// A box with a border. With the raster art a square-cornered box becomes a bevelled line frame tinted with the
    /// border colour over the flat background; round boxes (radius above 5: dots, pills) stay flat.
    /// </summary>
    public static StyleBox Box(Color bg, Color border, int borderPx = 2, int radius = 4)
    {
        if (Art && radius <= 5 && borderPx > 0 && border.A > 0.02f && UiArt.Has("boxline"))
        {
            var n = new UiNine("boxline") { Back = bg, BackInset = 1f, FrameMod = border };
            n.ContentMarginLeft = n.ContentMarginRight = n.ContentMarginTop = n.ContentMarginBottom = borderPx;
            return n;
        }
        return Flat(bg, border, borderPx, radius);
    }

    private static StyleBoxFlat FrameFlat
    {
        get
        {
            if (_frameBox != null) return _frameBox;
            var s = Flat(Panel, Border, 1, 3, 14f, 12f);
            s.ShadowColor = new Color(0f, 0f, 0f, 0.5f);
            s.ShadowSize = 10;
            s.ShadowOffset = new Vector2(0f, 3f);
            return _frameBox = s;
        }
    }

    private static StyleBoxFlat HeavyFlat
    {
        get
        {
            if (_heavyBox != null) return _heavyBox;
            var s = Flat(Panel, Gold.Darkened(0.32f), 2, 4, 24f, 20f);
            s.BorderWidthTop = 3;
            s.ShadowColor = new Color(0f, 0f, 0f, 0.62f);
            s.ShadowSize = 26;
            s.ShadowOffset = new Vector2(0f, 6f);
            return _heavyBox = s;
        }
    }

    private static StyleBoxFlat TipFlat
    {
        get
        {
            if (_tipBox != null) return _tipBox;
            var s = Flat(new Color(0.03f, 0.028f, 0.033f, 0.97f), Border.Lightened(0.08f), 1, 3, 14f, 11f);
            s.ShadowColor = new Color(0f, 0f, 0f, 0.6f);
            s.ShadowSize = 12;
            s.ShadowOffset = new Vector2(0f, 4f);
            return _tipBox = s;
        }
    }

    /// <summary>Content margins of the heavy window frame (design px): the moulding is 17 px, the rest is air.</summary>
    public const float WinPadX = 32f, WinPadY = 28f;

    /// <summary>
    /// The window: heavy forged moulding with cast corners and the Sun-Forge crest on a slate field. Boxes below
    /// 620 x 400 (dialogs, small cards) get the compact moulding and the small crest by themselves.
    /// </summary>
    public static StyleBox WindowBox => !Art ? HeavyFlat : ArtBox("window", () =>
        new UiNine("window", "slate", 5f, WinPadX, WinPadY)
        {
            Top = UiArt.P("crest"), TopMinWidth = 620f,
            Small = UiArt.P("modal"), SmallTop = UiArt.P("crest_small"), SmallBelow = new Vector2(620f, 400f),
        });

    /// <summary>The window moulding without the crest: full-screen panels whose top edge meets the screen edge.</summary>
    public static StyleBox WindowPlainBox => !Art ? HeavyFlat : ArtBox("window_plain", () =>
        new UiNine("window", "slate", 5f, WinPadX, WinPadY) { Small = UiArt.P("modal"), SmallBelow = new Vector2(620f, 400f) });

    /// <summary>The sub-panel: thin moulding with capped corners over a darker field (cards inside windows).</summary>
    public static StyleBox PanelBox => !Art ? FrameFlat : ArtBox("panel", () => new UiNine("panel", "soot", 3f, 18f, 16f));

    /// <summary>An inset well for lists and grids: a recessed rim over the darkest field.</summary>
    public static StyleBox WellBox => !Art ? Flat(Inset, Border, 1, 3, 8f, 8f) : ArtBox("well", () => new UiNine("well", "well", 2f, 10f, 10f));

    /// <summary>The box tooltips and floating cards sit in (the rarity band is drawn by the card itself, see <see cref="DrawTipBand"/>).</summary>
    public static StyleBox TipBox => !Art ? TipFlat : ArtBox("tooltip", () =>
        new UiNine("tooltip", "soot", 3f).Margins(18f, 30f, 18f, 15f));

    /// <summary>The comparison card: the same frame, held back a little.</summary>
    public static StyleBox TipBoxDim
    {
        get
        {
            if (Art) return ArtBox("tooltip_dim", () => new UiNine("tooltip", "soot", 3f) { FrameMod = new Color(0.78f, 0.76f, 0.74f), FieldMod = new Color(0.86f, 0.86f, 0.88f) }.Margins(18f, 30f, 18f, 15f));
            var dimmer = (StyleBoxFlat)TipFlat.Duplicate();
            dimmer.BorderColor = Border.Darkened(0.25f);
            dimmer.BgColor = new Color(0.026f, 0.025f, 0.03f, 0.97f);
            return dimmer;
        }
    }

    private static UiArt.Piece _tipBand;

    /// <summary>
    /// The tinted band of a tooltip card (rarity, element or gold at a glance): an enamel strip in the trough under
    /// the card's top moulding. Call from the card's draw callback.
    /// </summary>
    public static void DrawTipBand(Control card, Color c)
    {
        if (!Art)
        {
            card.DrawRect(new Rect2(1f, 1f, card.Size.X - 2f, 3f), new Color(c, 0.9f));
            card.DrawRect(new Rect2(1f, 4f, card.Size.X - 2f, 1f), new Color(0f, 0f, 0f, 0.5f));
            return;
        }
        _tipBand ??= UiArt.P("tooltip_band");
        if (_tipBand == null) return;
        // the trough starts 9 px under the card's top edge and runs between the side mouldings
        UiArt.Nine(card.GetCanvasItem(), _tipBand, new Rect2(11f, 9f, card.Size.X - 22f, _tipBand.BoxSize.Y), new Color(c.R, c.G, c.B, 1f));
    }

    /// <summary>A quiet dark plate with a hairline for toasts, prompts and small notices over the 3D scene.</summary>
    public static StyleBox PlaqueBox => !Art ? Flat(new Color(0.03f, 0.028f, 0.033f, 0.9f), Border, 1, 3, 14f, 8f) : ArtBox("plaque", 18f, 8f);

    // ───────────────────────── theme ─────────────────────────

    private static Theme BuildTheme()
    {
        var t = new Godot.Theme();
        try { Fill(t); Log.Info("ui", "theme built"); }
        catch (Exception e) { Log.Error("ui", "theme build failed, engine defaults remain for the rest", e); }
        return t;
    }

    private static void Fill(Theme t)
    {
        t.DefaultFont = Body;
        FillSizes(t);

        Color shadow = new(0f, 0f, 0f, 0.55f);
        Color goldBright = Gold.Lightened(0.25f);
        Color disabled = new(0.42f, 0.4f, 0.37f);
        var empty = new StyleBoxEmpty();

        // ── labels ──
        t.SetColor("font_color", "Label", Text);
        t.SetColor("font_shadow_color", "Label", shadow);
        t.SetConstant("shadow_offset_x", "Label", 1);
        t.SetConstant("shadow_offset_y", "Label", 1);
        // Noto Serif / Sans TC already carry about 44 % of leading in their line box: larger text needs no extra gap
        t.SetConstant("line_spacing", "Label", 1);

        // ── buttons ──
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
        foreach (string type in new[] { "Button", "OptionButton", "MenuButton" })
        {
            t.SetStylebox("normal", type, bNormal);
            t.SetStylebox("hover", type, bHover);
            t.SetStylebox("pressed", type, bPressed);
            t.SetStylebox("hover_pressed", type, bPressed);
            t.SetStylebox("disabled", type, bDisabled);
            t.SetStylebox("focus", type, bFocus);
            t.SetColor("font_color", type, Text);
            t.SetColor("font_hover_color", type, goldBright);
            t.SetColor("font_pressed_color", type, Gold);
            t.SetColor("font_hover_pressed_color", type, Gold);
            t.SetColor("font_focus_color", type, Text);
            t.SetColor("font_disabled_color", type, disabled);
            t.SetColor("icon_normal_color", type, Text);
            t.SetColor("icon_hover_color", type, goldBright);
            t.SetColor("icon_disabled_color", type, disabled);
            t.SetConstant("h_separation", type, 8);
        }
        t.SetConstant("arrow_margin", "OptionButton", 10);

        // ── check boxes and toggles ──
        var cPad = new StyleBoxEmpty { ContentMarginLeft = 4f, ContentMarginRight = 6f, ContentMarginTop = 4f, ContentMarginBottom = 4f };
        foreach (string type in new[] { "CheckBox", "CheckButton" })
        {
            foreach (string st in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" }) t.SetStylebox(st, type, cPad);
            t.SetColor("font_color", type, Text);
            t.SetColor("font_hover_color", type, goldBright);
            t.SetColor("font_pressed_color", type, Text);
            t.SetColor("font_hover_pressed_color", type, goldBright);
            t.SetColor("font_focus_color", type, Text);
            t.SetColor("font_disabled_color", type, disabled);
            t.SetConstant("h_separation", type, 10);
        }
        ThemeGlyphs(t);

        // ── frames ──
        t.SetStylebox("panel", "Panel", PanelBox);
        t.SetStylebox("panel", "PanelContainer", PanelBox);
        t.SetStylebox("panel", "PopupPanel", Art ? ArtBox("panel", 14f, 12f, "soot", 3f) : TipFlat);
        t.SetStylebox("panel", "TooltipPanel", Art ? ArtBox("panel", 14f, 10f, "soot", 3f) : TipFlat);
        t.SetColor("font_color", "TooltipLabel", Text);
        t.SetColor("font_shadow_color", "TooltipLabel", shadow);

        // ── rich text ──
        t.SetFont("normal_font", "RichTextLabel", Body);
        t.SetFont("bold_font", "RichTextLabel", Title);
        t.SetFont("italics_font", "RichTextLabel", _italic);
        t.SetFont("bold_italics_font", "RichTextLabel", _italic);
        t.SetFont("mono_font", "RichTextLabel", Ui);
        t.SetColor("default_color", "RichTextLabel", Text);
        t.SetColor("font_shadow_color", "RichTextLabel", new Color(0f, 0f, 0f, 0.4f));
        t.SetConstant("shadow_offset_x", "RichTextLabel", 1);
        t.SetConstant("shadow_offset_y", "RichTextLabel", 1);
        t.SetConstant("line_separation", "RichTextLabel", 2);
        t.SetStylebox("normal", "RichTextLabel", empty);
        t.SetStylebox("focus", "RichTextLabel", empty);

        // ── text fields ──
        StyleBox eNormal = Art ? ArtBox("input", 14f, 9f) : Flat(Inset, Border, 1, 3, 12f, 8f);
        StyleBox eFocus = Art ? ArtBox("input_focus", 14f, 9f) : Flat(Inset, Gold, 1, 3, 12f, 8f);
        StyleBox eRead = Art ? ArtBox("input_read", () => new UiNine("input", null, 0f, 14f, 9f) { FrameMod = new Color(0.72f, 0.7f, 0.68f) })
            : Flat(new Color(0.05f, 0.048f, 0.05f, 0.9f), new Color(0.2f, 0.18f, 0.16f), 1, 3, 12f, 8f);
        foreach (string type in new[] { "LineEdit", "TextEdit" })
        {
            t.SetStylebox("normal", type, eNormal);
            t.SetStylebox("focus", type, eFocus);
            t.SetStylebox("read_only", type, eRead);
            t.SetColor("font_color", type, Text);
            t.SetColor("font_selected_color", type, Colors.White);
            t.SetColor("font_placeholder_color", type, new Color(Dim, 0.7f));
            t.SetColor("font_uneditable_color", type, Dim);
            t.SetColor("caret_color", type, Gold);
            t.SetColor("selection_color", type, new Color(Ember, 0.38f));
        }

        // ── scrolling ──
        var track = Flat(new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0f), 0, 4, 4f, 4f);
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
        t.SetStylebox("panel", "ScrollContainer", empty);
        t.SetStylebox("focus", "ScrollContainer", empty);

        // ── sliders ──
        StyleBox sTrack = Art ? ArtBox("slider_track", 0f, 5f) : Flat(new Color(0f, 0f, 0f, 0.6f), Border, 1, 3, 0f, 3f);
        StyleBox sFill = Art ? ArtBox("slider_fill", 0f, 5f) : Flat(Gold.Darkened(0.25f), Gold.Darkened(0.25f), 0, 3, 0f, 3f);
        StyleBox sFillHi = Art ? sFill : Flat(Gold, Gold, 0, 3, 0f, 3f);
        foreach (string type in new[] { "HSlider", "VSlider" })
        {
            t.SetStylebox("slider", type, sTrack);
            t.SetStylebox("grabber_area", type, sFill);
            t.SetStylebox("grabber_area_highlight", type, sFillHi);
        }

        // ── progress ──
        t.SetStylebox("background", "ProgressBar", Art ? ArtBox("input", 3f, 3f) : Flat(new Color(0f, 0f, 0f, 0.6f), Border, 1, 2, 2f, 2f));
        t.SetStylebox("fill", "ProgressBar", Flat(Gold.Darkened(0.2f), Gold, 0, 1));
        t.SetColor("font_color", "ProgressBar", Text);

        // ── tabs ──
        StyleBox tabOn, tabOff, tabHover;
        if (Art) { tabOn = ArtBox("tab_on", 20f, 9f); tabOff = ArtBox("tab", 20f, 9f); tabHover = ArtBox("tab_hover", 20f, 9f); }
        else
        {
            var fOn = Flat(Panel, Gold.Darkened(0.2f), 1, 3, 20f, 9f); fOn.BorderWidthBottom = 0; fOn.BorderWidthTop = 2;
            var fOff = Flat(new Color(0.05f, 0.047f, 0.05f, 0.92f), new Color(Border, 0.6f), 1, 3, 20f, 9f); fOff.BorderWidthBottom = 0;
            var fHover = Flat(new Color(0.1f, 0.09f, 0.085f, 0.95f), Border.Lightened(0.15f), 1, 3, 20f, 9f); fHover.BorderWidthBottom = 0;
            tabOn = fOn; tabOff = fOff; tabHover = fHover;
        }
        foreach (string type in new[] { "TabContainer", "TabBar" })
        {
            t.SetStylebox("tab_selected", type, tabOn);
            t.SetStylebox("tab_unselected", type, tabOff);
            t.SetStylebox("tab_hovered", type, tabHover);
            t.SetStylebox("tab_disabled", type, tabOff);
            t.SetStylebox("tab_focus", type, empty);
            t.SetColor("font_selected_color", type, Gold);
            t.SetColor("font_unselected_color", type, Dim);
            t.SetColor("font_hovered_color", type, Text);
            t.SetColor("font_disabled_color", type, disabled);
        }
        t.SetStylebox("panel", "TabContainer", Art ? PanelBox : Flat(Panel, Border, 1, 3, 14f, 12f));
        t.SetStylebox("tabbar_background", "TabContainer", empty);

        // ── popups and lists ──
        StyleBox menu;
        if (Art) menu = ArtBox("panel", 12f, 12f, "soot", 3f);
        else
        {
            var fm = Flat(new Color(0.045f, 0.042f, 0.048f, 0.99f), Border, 1, 3, 6f, 6f);
            fm.ShadowColor = new Color(0f, 0f, 0f, 0.55f); fm.ShadowSize = 10;
            menu = fm;
        }
        var rowHover = Flat(new Color(Gold, 0.16f), new Color(0f, 0f, 0f, 0f), 0, 2);
        var rowSel = Flat(new Color(Gold, 0.24f), new Color(Gold, 0.5f), 1, 2);
        var line = new StyleBoxLine { Color = new Color(Border, 0.8f), Thickness = 1, GrowBegin = -4f, GrowEnd = -4f };
        t.SetStylebox("panel", "PopupMenu", menu);
        t.SetStylebox("hover", "PopupMenu", rowHover);
        t.SetStylebox("separator", "PopupMenu", line);
        t.SetColor("font_color", "PopupMenu", Text);
        t.SetColor("font_hover_color", "PopupMenu", goldBright);
        t.SetColor("font_disabled_color", "PopupMenu", disabled);
        t.SetColor("font_separator_color", "PopupMenu", Dim);
        t.SetConstant("v_separation", "PopupMenu", 8);
        t.SetConstant("h_separation", "PopupMenu", 8);
        t.SetConstant("item_start_padding", "PopupMenu", 10);
        t.SetConstant("item_end_padding", "PopupMenu", 10);

        foreach (string type in new[] { "ItemList", "Tree" })
        {
            t.SetStylebox("panel", type, Art ? WellBox : Flat(Inset, Border, 1, 3, 6f, 6f));
            t.SetStylebox("focus", type, empty);
            t.SetStylebox("selected", type, rowSel);
            t.SetStylebox("selected_focus", type, rowSel);
            t.SetStylebox("hovered", type, rowHover);
            t.SetStylebox("cursor", type, empty);
            t.SetStylebox("cursor_unfocused", type, empty);
            t.SetColor("font_color", type, Text);
            t.SetColor("font_selected_color", type, goldBright);
            t.SetColor("font_hovered_color", type, Text);
            t.SetColor("guide_color", type, new Color(Border, 0.25f));
        }

        // ── separators ──
        t.SetStylebox("separator", "HSeparator", Art ? ArtBox("rule_line", () => new UiNine("rule") { Line = true, FrameMod = new Color(1f, 1f, 1f, 0.8f) })
            : new StyleBoxLine { Color = new Color(Border, 0.85f), Thickness = 1 });
        t.SetStylebox("separator", "VSeparator", new StyleBoxLine { Color = new Color(Border, 0.85f), Thickness = 1, Vertical = true });
        t.SetConstant("separation", "HSeparator", 10);
        t.SetConstant("separation", "VSeparator", 10);
        t.SetConstant("separation", "HBoxContainer", 8);
        t.SetConstant("separation", "VBoxContainer", 8);
        t.SetConstant("h_separation", "GridContainer", 6);
        t.SetConstant("v_separation", "GridContainer", 6);
    }

    /// <summary>
    /// Every font size of the theme, through Fs(). Called when the theme is built and again whenever the text scale
    /// changes: the Theme resource is updated in place, so every control that reads it follows at once.
    /// </summary>
    private static void FillSizes(Theme t)
    {
        t.DefaultFontSize = Fs(FsBody);
        foreach (string type in new[] { "Button", "OptionButton", "MenuButton", "CheckBox", "CheckButton", "LinkButton" })
            t.SetFontSize("font_size", type, Fs(FsRow));
        t.SetFontSize("font_size", "Label", Fs(FsBody));
        t.SetFontSize("font_size", "TooltipLabel", Fs(FsRow));
        foreach (string fs in new[] { "normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size", "mono_font_size" })
            t.SetFontSize(fs, "RichTextLabel", Fs(FsBody));
        t.SetFontSize("font_size", "LineEdit", Fs(FsBody));
        t.SetFontSize("font_size", "TextEdit", Fs(FsBody));
        t.SetFontSize("font_size", "ProgressBar", Fs(FsCaption));
        t.SetFontSize("font_size", "TabContainer", Fs(FsRow));
        t.SetFontSize("font_size", "TabBar", Fs(FsRow));
        t.SetFontSize("font_size", "PopupMenu", Fs(FsRow));
        t.SetFontSize("font_separator_size", "PopupMenu", Fs(FsCaption));
        t.SetFontSize("font_size", "ItemList", Fs(FsRow));
        t.SetFontSize("font_size", "Tree", Fs(FsRow));
        t.SetFontSize("title_button_font_size", "Tree", Fs(FsRow));
        t.SetFontSize("font_size", "HeaderSmall", Fs(FsRow));
        t.SetFontSize("font_size", "HeaderMedium", Fs(FsName));
        t.SetFontSize("font_size", "HeaderLarge", Fs(FsTitle));
        t.SetFontSize("title_font_size", "Window", Fs(FsRow));
    }

    /// <summary>Small vector glyphs for check boxes, toggles, slider handles and menu arrows.</summary>
    private static void ThemeGlyphs(Theme t)
    {
        const string head = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 ";
        string box(string stroke, string extra) => head + "20 20'><rect x='1.6' y='1.6' width='16.8' height='16.8' rx='2.6' fill='#0b0a0c' stroke='" + stroke + "' stroke-width='1.4'/>" + extra + "</svg>";
        string tick(string c) => "<path d='M5 10.4l3.4 3.4 6.8-7.6' fill='none' stroke='" + c + "' stroke-width='2.4' stroke-linecap='round' stroke-linejoin='round'/>";
        string ring(string stroke, string extra) => head + "20 20'><circle cx='10' cy='10' r='8.2' fill='#0b0a0c' stroke='" + stroke + "' stroke-width='1.4'/>" + extra + "</svg>";
        string dot(string c) => "<circle cx='10' cy='10' r='4.2' fill='" + c + "'/>";
        string toggle(bool on, bool live) => head + "40 20'><rect x='1' y='2' width='38' height='16' rx='8' fill='" + (on && live ? "#56330f" : "#0b0a0c")
            + "' stroke='" + (on && live ? "#f0bd63" : live ? "#6a573c" : "#3a342c") + "' stroke-width='1.4'/><circle cx='" + (on ? "30" : "10") + "' cy='10' r='5.6' fill='"
            + (!live ? "#57504a" : on ? "#f0bd63" : "#8a7a62") + "'/></svg>";
        string knob(string fill, string stroke) => head + "18 18'><circle cx='9' cy='9' r='7' fill='" + fill + "' stroke='" + stroke + "' stroke-width='2'/><circle cx='9' cy='9' r='2.5' fill='" + stroke + "'/></svg>";
        string chevron(string d, string c) => head + "14 14'><path d='" + d + "' fill='none' stroke='" + c + "' stroke-width='1.9' stroke-linecap='round' stroke-linejoin='round'/></svg>";

        ImageTexture Tex(string svg, int w, int h) => Icons.FromSvg(svg.Replace('\'', '"'), w, h);
        void Put(string name, string type, ImageTexture tex) { if (tex != null) t.SetIcon(name, type, tex); }

        var boxOff = Tex(box("#6a573c", ""), 20, 20);
        var boxOn = Tex(box("#c79a4f", tick("#f0bd63")), 20, 20);
        var boxOffDim = Tex(box("#3a342c", ""), 20, 20);
        var boxOnDim = Tex(box("#3a342c", tick("#6d655a")), 20, 20);
        var radio = Tex(ring("#6a573c", ""), 20, 20);
        var radioOn = Tex(ring("#c79a4f", dot("#f0bd63")), 20, 20);
        var radioDim = Tex(ring("#3a342c", ""), 20, 20);
        var radioOnDim = Tex(ring("#3a342c", dot("#6d655a")), 20, 20);

        foreach (string type in new[] { "CheckBox", "PopupMenu" })
        {
            Put("unchecked", type, boxOff); Put("checked", type, boxOn);
            Put("unchecked_disabled", type, boxOffDim); Put("checked_disabled", type, boxOnDim);
            Put("radio_unchecked", type, radio); Put("radio_checked", type, radioOn);
            Put("radio_unchecked_disabled", type, radioDim); Put("radio_checked_disabled", type, radioOnDim);
        }
        Put("submenu", "PopupMenu", Tex(chevron("M5 3l4.2 4L5 11", "#dbd1bd"), 14, 14));

        Put("unchecked", "CheckButton", Tex(toggle(false, true), 40, 20));
        Put("checked", "CheckButton", Tex(toggle(true, true), 40, 20));
        Put("unchecked_disabled", "CheckButton", Tex(toggle(false, false), 40, 20));
        Put("checked_disabled", "CheckButton", Tex(toggle(true, false), 40, 20));

        var grabber = Tex(knob("#1a1614", "#d9a856"), 18, 18);
        var grabberHi = Tex(knob("#2c2118", "#ffd27a"), 18, 18);
        var grabberOff = Tex(knob("#141212", "#57504a"), 18, 18);
        foreach (string type in new[] { "HSlider", "VSlider" })
        {
            Put("grabber", type, grabber); Put("grabber_highlight", type, grabberHi); Put("grabber_disabled", type, grabberOff);
        }
        Put("arrow", "OptionButton", Tex(chevron("M3 5l4 4.2L11 5", "#dbd1bd"), 14, 14));

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
    }

    // ───────────────────────── widgets ─────────────────────────

    public static string Hex(Color c) => c.ToHtml(false);

    /// <summary>A serif label. <paramref name="size"/> is a DESIGN size: it goes through <see cref="Fs"/> here.</summary>
    public static Label Label(string text, int size = FsBody, Color? color = null, bool title = false)
    {
        var l = new Godot.Label { Text = text ?? "", MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontOverride("font", title ? Title : Body);
        l.AddThemeFontSizeOverride("font_size", Fs(size));
        l.AddThemeColorOverride("font_color", color ?? (title ? Gold : Text));
        return l;
    }

    /// <summary>A sans-serif label for numbers, key caps and captions. <paramref name="size"/> is a DESIGN size (floor: MinFont).</summary>
    public static Label Small(string text, int size = FsCaption, Color? color = null, bool bold = false)
    {
        var l = new Godot.Label { Text = text ?? "", MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontOverride("font", bold ? UiBold : Ui);
        l.AddThemeFontSizeOverride("font_size", Fs(size));
        l.AddThemeColorOverride("font_color", color ?? Dim);
        return l;
    }

    /// <summary>
    /// Keeps a one-line label inside the width it is given: what does not fit is cut with an ellipsis, and (unless
    /// <paramref name="hint"/> is false) hovering shows the whole text.
    /// </summary>
    public static T Trimmed<T>(T l, bool hint = true) where T : Godot.Label
    {
        l.ClipText = true;
        l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        if (hint)
        {
            if (l.MouseFilter == Control.MouseFilterEnum.Ignore) l.MouseFilter = Control.MouseFilterEnum.Pass;
            l.MouseEntered += () => { if (GodotObject.IsInstanceValid(l)) l.TooltipText = l.Text; };
        }
        return l;
    }

    /// <summary>Lets a label wrap inside the width its container gives it (or inside <paramref name="designWidth"/> when given).</summary>
    public static T Wrapped<T>(T l, float designWidth = 0f) where T : Godot.Label
    {
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (designWidth > 0f) l.CustomMinimumSize = new Vector2(designWidth, l.CustomMinimumSize.Y);
        else l.SizeFlagsHorizontal |= Control.SizeFlags.ExpandFill;
        return l;
    }

    /// <summary>Adds a dark outline so text stays readable over the 3D scene.</summary>
    public static T Outlined<T>(T c, int px = 5) where T : Control
    {
        c.AddThemeConstantOverride("outline_size", px);
        c.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        return c;
    }

    /// <summary>A themed command button. <paramref name="size"/> is a DESIGN size: it goes through <see cref="Fs"/> here.</summary>
    public static Button Button(string text, Action onPress, int size = FsRow)
    {
        var b = new Godot.Button { Text = text ?? "" };
        b.AddThemeFontSizeOverride("font_size", Fs(size));
        b.Pressed += () =>
        {
            Snd.Ui("ui_click");
            if (onPress != null) Log.Guard("ui", onPress);
        };
        return b;
    }

    /// <summary>
    /// A BBCode text block that wraps at <paramref name="width"/> and grows downward. Its base size comes from the
    /// theme (running text); pass a DESIGN <paramref name="size"/> for another one. The BBCode itself is shown as
    /// given: [font_size] tags in it must already have been through <see cref="Fs"/> / <see cref="Bb"/>.
    /// </summary>
    public static RichTextLabel Rich(string bbcode, float width, int size = 0)
    {
        var r = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false, SelectionEnabled = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(Mathf.Max(0f, width), 0f),
        };
        if (size > 0) RichSize(r, size);
        r.Text = bbcode ?? "";
        return r;
    }

    /// <summary>Sets every base font size of a RichTextLabel (normal, bold, italics, mono) to one DESIGN size.</summary>
    public static T RichSize<T>(T r, int size) where T : RichTextLabel
    {
        int px = Fs(size);
        r.AddThemeFontSizeOverride("normal_font_size", px);
        r.AddThemeFontSizeOverride("bold_font_size", px);
        r.AddThemeFontSizeOverride("italics_font_size", px);
        r.AddThemeFontSizeOverride("bold_italics_font_size", px);
        r.AddThemeFontSizeOverride("mono_font_size", px);
        return r;
    }

    private static StyleBoxFlat _tabOn, _tabOnHover, _tabOff, _tabOffHover;

    /// <summary>
    /// Dresses a button as the chosen or an idle option of a tab row. The chosen tab carries a warm ember face with
    /// an ember-gold frame and bright lettering; idle tabs are dark iron with dim lettering that lights up on hover.
    /// One look for every tab row in the game (vendors, character sheet, map, quest log).
    /// </summary>
    public static T TabTone<T>(T b, bool active) where T : Godot.Button
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
        if (_tabOn == null)
        {
            _tabOn = Flat(new Color(0.215f, 0.135f, 0.055f, 0.98f), Gold.Darkened(0.08f), 1, 3, 20f, 8f);
            _tabOn.BorderWidthBottom = 2;
            _tabOnHover = (StyleBoxFlat)_tabOn.Duplicate();
            _tabOnHover.BgColor = new Color(0.26f, 0.165f, 0.07f, 0.98f);
            _tabOnHover.BorderColor = Gold.Lightened(0.1f);
            _tabOff = Flat(new Color(0.058f, 0.054f, 0.056f, 0.96f), new Color(Border, 0.75f), 1, 3, 20f, 8f);
            _tabOff.BorderWidthBottom = 2;
            _tabOffHover = (StyleBoxFlat)_tabOff.Duplicate();
            _tabOffHover.BgColor = new Color(0.115f, 0.1f, 0.09f, 0.98f);
            _tabOffHover.BorderColor = Gold.Darkened(0.25f);
        }
        b.AddThemeStyleboxOverride("normal", active ? _tabOn : _tabOff);
        b.AddThemeStyleboxOverride("hover", active ? _tabOnHover : _tabOffHover);
        b.AddThemeStyleboxOverride("pressed", _tabOn);
        b.AddThemeStyleboxOverride("hover_pressed", _tabOnHover);
        }
        Color c = active ? Gold.Lightened(0.3f) : new Color(0.66f, 0.63f, 0.57f);
        b.AddThemeColorOverride("font_color", c);
        b.AddThemeColorOverride("font_focus_color", c);
        b.AddThemeColorOverride("font_hover_color", active ? Gold.Lightened(0.35f) : Text);
        b.AddThemeColorOverride("font_pressed_color", Gold.Lightened(0.22f));
        b.AddThemeColorOverride("font_hover_pressed_color", Gold.Lightened(0.35f));
        if (b.Modulate != Colors.White) b.Modulate = Colors.White;
        return b;
    }

    /// <summary>Dark iron-and-ember panel. Heavy frames are windows; light ones are cards inside them.</summary>
    public static PanelContainer Frame(bool heavy = false)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", heavy ? WindowBox : PanelBox);
        if (heavy && !Art) p.Draw += () => DrawWindowOrnament(p);
        return p;
    }

    /// <summary>An inset well (recessed rim, darkest field) to hold a list or a grid of slots.</summary>
    public static PanelContainer Well()
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", WellBox);
        return p;
    }

    private static readonly Vector2[] OrnDiamond = new Vector2[4];
    private static readonly Color OrnHair = new(0.93f, 0.73f, 0.38f, 0.13f), OrnBright = new(0.96f, 0.78f, 0.44f, 0.9f), OrnDark = new(0.03f, 0.028f, 0.03f, 1f);

    /// <summary>
    /// The one ornament every window shares: a faint inner hairline, forged brackets in the four corners and a small
    /// ember diamond set into the middle of the top edge. Drawn over the frame's box and under its content, inside
    /// the padding the heavy frame brings (24 x 20), so it never touches what a panel puts in the frame.
    /// </summary>
    private static void DrawWindowOrnament(Control p)
    {
        Vector2 s = p.Size;
        if (s.X < 220f || s.Y < 140f) return;
        const float inset = 7f, arm = 26f, thick = 2f;
        float x0 = inset, y0 = inset, x1 = s.X - inset, y1 = s.Y - inset;
        // inner hairline (four thin bars: crisp at any stretch factor)
        p.DrawRect(new Rect2(x0 + arm, y0, x1 - x0 - arm * 2f, 1f), OrnHair);
        p.DrawRect(new Rect2(x0 + arm, y1 - 1f, x1 - x0 - arm * 2f, 1f), OrnHair);
        p.DrawRect(new Rect2(x0, y0 + arm, 1f, y1 - y0 - arm * 2f), OrnHair);
        p.DrawRect(new Rect2(x1 - 1f, y0 + arm, 1f, y1 - y0 - arm * 2f), OrnHair);
        // corner brackets
        p.DrawRect(new Rect2(x0, y0, arm, thick), OrnBright); p.DrawRect(new Rect2(x0, y0, thick, arm), OrnBright);
        p.DrawRect(new Rect2(x1 - arm, y0, arm, thick), OrnBright); p.DrawRect(new Rect2(x1 - thick, y0, thick, arm), OrnBright);
        p.DrawRect(new Rect2(x0, y1 - thick, arm, thick), OrnBright); p.DrawRect(new Rect2(x0, y1 - arm, thick, arm), OrnBright);
        p.DrawRect(new Rect2(x1 - arm, y1 - thick, arm, thick), OrnBright); p.DrawRect(new Rect2(x1 - thick, y1 - arm, thick, arm), OrnBright);
        // the ember set into the top edge
        float cx = MathF.Round(s.X * 0.5f), cy = 1.5f;
        Diamond(p, cx, cy, 8f, OrnDark);
        Diamond(p, cx, cy, 6f, OrnBright);
        Diamond(p, cx, cy, 2.6f, new Color(1f, 0.5f, 0.15f));
    }

    private static void Diamond(Control p, float cx, float cy, float r, Color c)
    {
        OrnDiamond[0] = new Vector2(cx, cy - r); OrnDiamond[1] = new Vector2(cx + r, cy);
        OrnDiamond[2] = new Vector2(cx, cy + r); OrnDiamond[3] = new Vector2(cx - r, cy);
        p.DrawColoredPolygon(OrnDiamond, c);
    }

    /// <summary>A thin ember-gold rule to separate sections.</summary>
    public static Control Rule(float alpha = 0.7f)
    {
        var s = new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore };
        if (Art)
        {
            float a = Mathf.Clamp(alpha + 0.2f, 0f, 1f);
            s.AddThemeStyleboxOverride("separator", ArtBox("rule|" + a.ToString("0.00"), () => new UiNine("rule") { Line = true, FrameMod = new Color(1f, 1f, 1f, a) }));
        }
        else s.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(Gold.Darkened(0.3f), alpha), Thickness = 1 });
        return s;
    }

    /// <summary>A tinted glyph from the icon set.</summary>
    public static TextureRect Icon(string name, float size, Color? tint = null)
    {
        // raster icons keep their own colours (Icons.Tint passes only brightness and alpha); vector glyphs take the tint
        var tex = Icons.Get(name);
        return new TextureRect
        {
            Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(size, size), Size = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = Icons.Tint(tex, tint ?? Text), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };
    }

    /// <summary>Shows a tooltip beside <paramref name="c"/> while the pointer rests on it.</summary>
    public static void Tip(Control c, Func<string> bbcode, bool above = false)
    {
        if (c == null || bbcode == null) return;
        if (c.MouseFilter == Control.MouseFilterEnum.Ignore) c.MouseFilter = Control.MouseFilterEnum.Pass;
        c.MouseEntered += () =>
        {
            var ui = Game.I?.Ui;
            if (ui == null) return;
            string bb = null;
            try { bb = bbcode(); } catch (Exception e) { Log.Error("ui", "tooltip text failed", e); }
            if (string.IsNullOrEmpty(bb)) return;
            if (above) ui.ShowTooltipAbove(c.GetGlobalRect(), bb, c);
            else ui.ShowTooltipAt(c.GetGlobalRect(), bb, null, c);
        };
        c.MouseExited += () => Game.I?.Ui?.HideTooltipOf(c);
        c.TreeExiting += () => Game.I?.Ui?.HideTooltipOf(c);
    }

    // ───────────────────────── item slot ─────────────────────────

    /// <summary>The shared box of an inventory cell: rarity -1 = empty, otherwise (int)Rarity; hover and "cannot be worn" states.</summary>
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

    /// <summary>The colour an item's glyph is drawn in: parchment for plain gear, its rarity or gem colour otherwise.</summary>
    public static Color ItemTint(Item it)
    {
        if (it == null) return Dim;
        if (it.Kind == ItemKind.Gem)
        {
            var g = ItemDb.Gem(it.GemId);
            return g != null ? g.Color.Lightened(0.25f) : Text;
        }
        return it.Rarity == Rarity.Common ? new Color(0.78f, 0.75f, 0.69f) : Text.Lerp(Palette.Rarity(it.Rarity), 0.6f);
    }

    /// <summary>
    /// A square inventory cell: rarity-coloured frame, tinted glyph, masterwork badge and socket pips. Hovering shows
    /// a tooltip through the Ui manager — the caller's <paramref name="tooltip"/> text, or by default the item
    /// card with the equipped piece beside it for comparison. Left and right clicks go to the callbacks.
    /// A null item gives an empty recessed cell.
    /// </summary>
    public static Control ItemSlot(Item it, int size, Action<Item> onLeft, Action<Item> onRight, Func<Item, string> tooltip)
    {
        size = Math.Max(16, size);
        var root = new Control
        {
            CustomMinimumSize = new Vector2(size, size), Size = new Vector2(size, size),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        var hero = Game.I?.Hero;
        int rarity = it == null ? -1 : (int)it.Rarity;
        bool bad = false;
        if (it != null && it.IsEquip && hero != null)
        {
            try { bad = hero.WhyCantEquip(it) != null; } catch { bad = false; }
        }
        StyleBox normal = SlotBox(rarity, false, bad), lit = SlotBox(rarity, true, bad);

        var frame = new Godot.Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        frame.AddThemeStyleboxOverride("panel", normal);
        root.AddChild(frame);

        if (it != null)
        {
            int pad = Math.Max(3, size / 9);
            // raster art is not stained by the rarity colour (the slot's rarity rim carries it); vector glyphs still are
            var tex = Icons.ForItem(it);
            var icon = new TextureRect
            {
                Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = Icons.Tint(tex, ItemTint(it)), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            };
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, pad);
            root.AddChild(icon);

            if (it.Upgrade > 0)
            {
                // the masterwork badge is a counter: caption size, never smaller, whatever the cell size. It hugs the
                // top-right corner with a heavy outline so it stays legible over the glyph.
                var badge = Outlined(Small("+" + it.Upgrade, FsCaption, Gold, true), 5);
                badge.HorizontalAlignment = HorizontalAlignment.Right;
                badge.VerticalAlignment = VerticalAlignment.Top;
                badge.AddThemeConstantOverride("line_spacing", 0);
                badge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize);
                badge.OffsetLeft = 0f; badge.OffsetTop = -3f; badge.OffsetRight = -4f; badge.OffsetBottom = 0f;
                badge.ClipText = true;
                root.AddChild(badge);
            }
            if (it.Sockets > 0)
            {
                // one pip per socket: filled pips carry the gem's colour
                var pips = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                pips.AddThemeConstantOverride("separation", 2);
                for (int i = 0; i < it.Sockets; i++)
                {
                    GemDef g = i < it.Gems.Count ? ItemDb.Gem(it.Gems[i]) : null;
                    var pip = new ColorRect
                    {
                        Color = g != null ? g.Color : new Color(0.25f, 0.23f, 0.21f, 0.9f),
                        CustomMinimumSize = new Vector2(Math.Max(4, size / 11), Math.Max(4, size / 11)), MouseFilter = Control.MouseFilterEnum.Ignore,
                    };
                    pips.AddChild(pip);
                }
                pips.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.Minsize, 4);
                root.AddChild(pips);
            }
        }

        root.MouseEntered += () =>
        {
            frame.AddThemeStyleboxOverride("panel", lit);
            if (it == null) return;
            var ui = Game.I?.Ui;
            if (ui == null) return;
            string bb = null, cmp = null;
            try
            {
                if (tooltip != null) bb = tooltip(it);
                else
                {
                    var h = Game.I?.Hero;
                    Item worn = null;
                    if (h != null && it.IsEquip && Array.IndexOf(h.D.Equip, it) < 0) worn = h.Compared(it);
                    bb = ItemText.Tooltip(it, worn, h?.D.Level ?? 99);
                    if (worn != null) cmp = $"[color=#{Hex(Dim)}]目前裝備[/color]\n" + ItemText.Tooltip(worn, null, h?.D.Level ?? 99, false);
                }
            }
            catch (Exception e) { Log.Error("ui", "item tooltip failed", e); }
            if (!string.IsNullOrEmpty(bb)) ui.ShowTooltipAt(root.GetGlobalRect(), bb, cmp, root);
        };
        root.MouseExited += () =>
        {
            frame.AddThemeStyleboxOverride("panel", normal);
            Game.I?.Ui?.HideTooltipOf(root);
        };
        root.TreeExiting += () => Game.I?.Ui?.HideTooltipOf(root);
        root.GuiInput += e =>
        {
            if (e is not InputEventMouseButton mb || !mb.Pressed) return;
            if (mb.ButtonIndex == MouseButton.Left && onLeft != null)
            {
                root.AcceptEvent();
                Log.Guard("ui", () => onLeft(it));
            }
            else if (mb.ButtonIndex == MouseButton.Right && onRight != null)
            {
                root.AcceptEvent();
                Log.Guard("ui", () => onRight(it));
            }
        };
        return root;
    }
}
