using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace AshenAbyss;

/// <summary>Small helpers shared by the ui-world panels (map, dialogue, quest log, world tier, rift).</summary>
internal static class WorldUiKit
{
    public static Font TitleFont => UiTheme.Title ?? ThemeDB.FallbackFont;
    public static Font BodyFont => UiTheme.Body ?? ThemeDB.FallbackFont;
    public static Font SmallFont => UiTheme.Ui ?? ThemeDB.FallbackFont;

    public static string Hex(Color c) => c.ToHtml(false);
    public static string Col(string text, Color c) => $"[color=#{c.ToHtml(false)}]{text}[/color]";

    private static readonly string[] ZhDigits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
    public static string Zh(int n) => n >= 0 && n <= 10 ? ZhDigits[n] : n.ToString();
    public static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI" };

    /// <summary>The three regions that follow the wastes may be taken in any order, so they share one label.</summary>
    public static string ActLabel(RegionDef r) => r == null ? "" : r.Act == 2 ? "第二～四幕" : $"第{Zh(r.Act)}幕";

    public static string TierName(int tier) => ZoneDb.TierNames[Math.Clamp(tier, 0, ZoneDb.TierNames.Length - 1)];

    // ── story state, read defensively: the quest module may not be attached yet (boot, menu) ──

    private static bool _questWarned, _sndWarned, _tipWarned;

    private static void QuestFail(string what, Exception e)
    {
        if (_questWarned) return;
        _questWarned = true;
        Log.Error("ui-world", $"Quests.{what} failed; falling back to safe defaults", e);
    }

    public static bool CanEnter(string areaId)
    {
        try { return Quests.CanEnter(areaId); }
        catch (Exception e) { QuestFail("CanEnter", e); return areaId == ZoneDb.Town; }
    }

    public static int RenownTier(string regionId)
    {
        try { return Math.Clamp(Quests.RenownTier(regionId), 0, 4); }
        catch (Exception e) { QuestFail("RenownTier", e); return 0; }
    }

    public static int RenownPoints(string regionId)
    {
        try { return Math.Max(0, Quests.RenownPoints(regionId)); }
        catch (Exception e) { QuestFail("RenownPoints", e); return 0; }
    }

    public static string NextObjectiveArea()
    {
        try { return Quests.NextObjectiveArea(); }
        catch (Exception e) { QuestFail("NextObjectiveArea", e); return null; }
    }

    /// <summary>Why a region or the rift is still closed ("" when it is open).</summary>
    public static string WhyLocked(string areaId)
    {
        try { return Quests.WhyLocked(areaId) ?? ""; }
        catch (Exception e) { QuestFail("WhyLocked", e); return ""; }
    }

    // World facts live in the save as flags. The level code writes "sh_<region>" / "dg_<dungeon>", the story code keeps
    // its own "q_sh_<region>" / "q_dg_<dungeon>" / "q_boss_<bossDefId>"; either one counts.
    public static bool StrongholdFree(SaveData d, RegionDef r) => d != null && r != null && (d.Has("sh_" + r.Id) || d.Has("q_sh_" + r.Id));
    public static bool DungeonCleared(SaveData d, DungeonDef dg) => d != null && dg != null && (d.Has("dg_" + dg.Id) || d.Has("q_dg_" + dg.Id));

    public static int DungeonsCleared(SaveData d, RegionDef r)
    {
        int n = 0;
        if (r?.Dungeons != null) foreach (var dg in r.Dungeons) if (DungeonCleared(d, dg)) n++;
        return n;
    }

    public static bool BossSlain(SaveData d, RegionDef r) =>
        d != null && r != null && r.Boss != null && (d.Has("q_boss_" + r.Boss) || d.Has(r.Boss) || d.Has("boss_" + r.Id));

    public static void Sfx(string name, float volume = 1f)
    {
        try { Snd.Ui(name, volume); }
        catch (Exception e)
        {
            if (_sndWarned) return;
            _sndWarned = true;
            Log.Warn("ui-world", $"Snd.Ui({name}) failed: {e.Message}");
        }
    }

    public static void Tip(string bbcode, Vector2 screenPos)
    {
        try { Game.I?.Ui?.ShowTooltip(bbcode, screenPos); }
        catch (Exception e)
        {
            if (_tipWarned) return;
            _tipWarned = true;
            Log.Warn("ui-world", "ShowTooltip failed: " + e.Message);
        }
    }

    public static void TipHide()
    {
        try { Game.I?.Ui?.HideTooltip(); }
        catch (Exception) { /* the tooltip layer is gone with the UI: nothing to hide */ }
    }

    public static void ClosePanel(string id)
    {
        try { Game.I?.Ui?.Close(id); }
        catch (Exception e) { Log.Error("ui-world", $"closing panel {id} failed", e); }
    }

    // ── layout ──

    public static void FullRect(Control c) => c.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

    public static void Center(Control c, Vector2 size)
    {
        c.AnchorLeft = 0.5f; c.AnchorRight = 0.5f; c.AnchorTop = 0.5f; c.AnchorBottom = 0.5f;
        c.OffsetLeft = -size.X * 0.5f; c.OffsetRight = size.X * 0.5f;
        c.OffsetTop = -size.Y * 0.5f; c.OffsetBottom = size.Y * 0.5f;
        c.GrowHorizontal = Control.GrowDirection.Both; c.GrowVertical = Control.GrowDirection.Both;
        c.CustomMinimumSize = size;
    }

    public static VBoxContainer VBox(int separation)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", separation);
        return v;
    }

    public static HBoxContainer HBox(int separation)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", separation);
        return h;
    }

    public static MarginContainer Margin(int left, int top, int right, int bottom)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", left);
        m.AddThemeConstantOverride("margin_top", top);
        m.AddThemeConstantOverride("margin_right", right);
        m.AddThemeConstantOverride("margin_bottom", bottom);
        return m;
    }

    public static Control Spacer(bool vertical = false)
    {
        var s = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        if (vertical) s.SizeFlagsVertical = Control.SizeFlags.ExpandFill; else s.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return s;
    }

    /// <summary>A separator between sections: the forged rule (a hairline without the art).</summary>
    public static Control Rule(float alpha = 0.75f) => UiTheme.Rule(alpha);

    // ── raster chrome (res://assets/ui, see UiArt): the pieces the ui-world panels share. Every helper falls back to
    //    the old flat look when the art, or the one piece it needs, is not installed. Style boxes are built once. ──

    /// <summary>The piece with this name, or null without the art (never logs: a missing piece is an expected case).</summary>
    public static UiArt.Piece Piece(string name) => UiTheme.Art && UiArt.Has(name) ? UiArt.P(name) : null;

    private static readonly StyleBox[] CardBoxes = new StyleBox[4], RowBoxes = new StyleBox[3];
    private static StyleBox _capBox, _plateBox, _headLine;

    private static StyleBox Padded(StyleBox sb, float padX, float padY)
    {
        if (sb == null) return new StyleBoxEmpty();
        sb.ContentMarginLeft = padX; sb.ContentMarginRight = padX; sb.ContentMarginTop = padY; sb.ContentMarginBottom = padY;
        return sb;
    }

    /// <summary>
    /// A card inside a window (a world tier, a region, a block of figures): the light forged frame over the soot
    /// field. <paramref name="state"/>: 0 plain, 1 chosen (gold inlay and a warm floor), 2 closed (held back),
    /// 3 under the pointer.
    /// </summary>
    public static StyleBox CardBox(int state)
    {
        state = Math.Clamp(state, 0, 3);
        if (CardBoxes[state] != null) return CardBoxes[state];
        StyleBox sb;
        if (UiTheme.Art && UiArt.Has("card"))
        {
            string on = UiArt.Has("card_on") ? "card_on" : "card";
            sb = state switch
            {
                1 => UiTheme.ArtBox("world_card_on", () => new UiNine(on, "soot", 3f, 18f, 16f) { Back = new Color(0.62f, 0.36f, 0.1f, 0.11f), BackInset = 9f, BackOver = true }),
                2 => UiTheme.ArtBox("world_card_dim", () => new UiNine("card", "soot", 3f, 18f, 16f) { FrameMod = new Color(0.6f, 0.59f, 0.6f), FieldMod = new Color(0.82f, 0.82f, 0.84f) }),
                3 => UiTheme.ArtBox("world_card_hover", () => new UiNine("card", "soot", 3f, 18f, 16f) { FrameMod = new Color(1.3f, 1.22f, 1.06f), Back = new Color(0.62f, 0.42f, 0.16f, 0.07f), BackInset = 9f, BackOver = true }),
                _ => UiTheme.ArtBox("world_card", () => new UiNine("card", "soot", 3f, 18f, 16f)),
            };
        }
        else
        {
            sb = state switch
            {
                1 => Padded(UiTheme.Box(new Color(0.11f, 0.085f, 0.06f, 0.97f), UiTheme.Gold, 3, 6), 18f, 16f),
                2 => Padded(UiTheme.Box(new Color(0.06f, 0.055f, 0.05f, 0.95f), new Color(0.25f, 0.24f, 0.23f), 2, 6), 18f, 16f),
                3 => Padded(UiTheme.Box(new Color(0.085f, 0.075f, 0.065f, 0.96f), UiTheme.Gold.Darkened(0.25f), 2, 6), 18f, 16f),
                _ => Padded(UiTheme.Box(new Color(0.06f, 0.055f, 0.05f, 0.95f), new Color(UiTheme.Border, 0.9f), 2, 6), 18f, 16f),
            };
        }
        return CardBoxes[state] = sb;
    }

    /// <summary>
    /// A list row: 0 at rest, 1 under the pointer, 2 chosen. The row plates (row / row_hover / row_on) are used when
    /// they are installed; until then the tab plates stand in, and without any art a tinted line box does.
    /// </summary>
    public static StyleBox RowBox(int state)
    {
        state = Math.Clamp(state, 0, 2);
        if (RowBoxes[state] != null) return RowBoxes[state];
        string fam = !UiTheme.Art ? null
            : UiArt.Has("row") && UiArt.Has("row_hover") && UiArt.Has("row_on") ? "row"
            : UiArt.Has("tab") && UiArt.Has("tab_hover") && UiArt.Has("tab_on") ? "tab" : null;
        StyleBox sb;
        if (fam != null)
        {
            string piece = state == 2 ? fam + "_on" : state == 1 ? fam + "_hover" : fam;
            sb = UiTheme.ArtBox("world_row|" + piece, () => new UiNine(piece).Margins(18f, 8f, 18f, 8f));
        }
        else
        {
            sb = Padded(state == 2 ? UiTheme.Box(new Color(0.17f, 0.135f, 0.095f, 0.98f), UiTheme.Gold, 1, 3)
                : state == 1 ? UiTheme.Box(new Color(0.125f, 0.11f, 0.095f, 0.98f), UiTheme.Gold.Darkened(0.3f), 1, 3)
                : UiTheme.Box(new Color(0.07f, 0.065f, 0.064f, 0.96f), new Color(UiTheme.Border, 0.8f), 1, 3), 18f, 8f);
        }
        return RowBoxes[state] = sb;
    }

    /// <summary>Dresses a button as a list row (see <see cref="RowBox"/>); the chosen row keeps its plate under the pointer.</summary>
    public static T RowTone<T>(T b, bool chosen) where T : Button
    {
        b.AddThemeStyleboxOverride("normal", RowBox(chosen ? 2 : 0));
        b.AddThemeStyleboxOverride("hover", RowBox(chosen ? 2 : 1));
        b.AddThemeStyleboxOverride("pressed", RowBox(2));
        b.AddThemeStyleboxOverride("hover_pressed", RowBox(2));
        return b;
    }

    /// <summary>Dresses a button as a selectable card (see <see cref="CardBox"/>).</summary>
    public static T CardTone<T>(T b, bool chosen, bool closed = false) where T : Button
    {
        b.AddThemeStyleboxOverride("normal", CardBox(chosen ? 1 : closed ? 2 : 0));
        b.AddThemeStyleboxOverride("hover", CardBox(chosen ? 1 : 3));
        b.AddThemeStyleboxOverride("pressed", CardBox(1));
        b.AddThemeStyleboxOverride("hover_pressed", CardBox(1));
        return b;
    }

    /// <summary>The cap of a key in a hint line: the forged key cap with the key's name in the small face.</summary>
    public static Control KeyCap(string text, int size = UiTheme.FsCaption)
    {
        var p = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (_capBox == null)
        {
            if (UiTheme.Art && UiArt.Has("keycap")) _capBox = UiTheme.ArtBox("keycap", 11f, 1f);
            else
            {
                var flat = new StyleBoxFlat { BgColor = new Color(0.115f, 0.105f, 0.095f, 0.98f), BorderColor = UiTheme.Border.Lightened(0.1f) };
                flat.SetBorderWidthAll(1); flat.BorderWidthBottom = 3; flat.SetCornerRadiusAll(4);
                _capBox = Padded(flat, 10f, 2f);
            }
        }
        p.AddThemeStyleboxOverride("panel", _capBox);
        var l = UiTheme.Small(text, size, new Color(0.95f, 0.92f, 0.85f));
        l.HorizontalAlignment = HorizontalAlignment.Center;
        l.VerticalAlignment = VerticalAlignment.Center;
        p.AddChild(l);
        return p;
    }

    /// <summary>
    /// A hint line whose keys sit in key caps. <paramref name="parts"/> alternate between plain words and key names,
    /// starting with words; an empty part is left out.
    /// </summary>
    public static HBoxContainer HintRow(Color textCol, params string[] parts)
    {
        var row = HBox(7);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            if ((i & 1) == 1) { row.AddChild(KeyCap(parts[i])); continue; }
            var l = UiTheme.Label(parts[i], UiTheme.FsCaption, textCol);
            l.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(l);
        }
        return row;
    }

    /// <summary>
    /// A section heading with the forged header line under it (a plain gold heading without the art).
    /// <paramref name="size"/> is a DESIGN size. Hide or show the returned box, not the label.
    /// </summary>
    public static VBoxContainer Heading(string text, int size, Color? color = null)
    {
        var box = VBox(0);
        box.MouseFilter = Control.MouseFilterEnum.Ignore;
        box.AddChild(UiTheme.Label(text, size, color ?? UiTheme.Gold, true));
        if (UiTheme.Art && UiArt.Has("header_line"))
        {
            _headLine ??= UiTheme.ArtBox("world_header_line", () => new UiNine("header_line") { Line = true });
            var line = new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore };
            line.AddThemeStyleboxOverride("separator", _headLine);
            line.AddThemeConstantOverride("separation", 10);
            box.AddChild(line);
        }
        return box;
    }

    /// <summary>
    /// The mark in front of a line of a list: a small piece (bullet, bullet_gold, radio_on …) centred on the first
    /// text line, in a column <paramref name="column"/> design pixels wide that grows with the text scale. Without the
    /// art it is the old glyph (<paramref name="glyph"/>) in <paramref name="glyphCol"/>.
    /// <paramref name="lineSize"/> is the DESIGN size of the text the mark stands beside; <paramref name="scale"/>
    /// enlarges the piece (the studs are small beside running text).
    /// </summary>
    public static Control Mark(string piece, string glyph, Color glyphCol, int lineSize, float column = 28f, float scale = 1f)
    {
        var tex = UiTheme.Art && UiArt.Has(piece) ? UiArt.Icon(piece) : null;
        if (tex == null)
        {
            var m = UiTheme.MinSize(UiTheme.Label(glyph, lineSize, glyphCol), column, 0f);
            m.HorizontalAlignment = HorizontalAlignment.Center;
            m.VerticalAlignment = VerticalAlignment.Top;
            return m;
        }
        float w = UiTheme.Px(column), lineH = MathF.Ceiling(LineHeight(BodyFont, lineSize));
        var holder = new Control { CustomMinimumSize = new Vector2(w, lineH), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin, MouseFilter = Control.MouseFilterEnum.Ignore };
        Vector2 ts = (tex.GetSize() * UiTheme.TextScale * scale).Round();
        holder.AddChild(new TextureRect
        {
            Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            Position = new Vector2(MathF.Round((w - ts.X) * 0.5f), MathF.Round((lineH - ts.Y) * 0.5f) + 1f), Size = ts,
        });
        return holder;
    }

    /// <summary>The name plate (pointed ends, two studs): a box for one line of a name, as tall as that line.</summary>
    public static StyleBox NamePlateBox()
    {
        if (_plateBox != null) return _plateBox;
        if (UiTheme.Art && UiArt.Has("nameplate")) _plateBox = UiTheme.ArtBox("world_nameplate", () => new UiNine("nameplate").Margins(36f, 1f, 36f, 3f));
        else _plateBox = UiTheme.PanelBox;
        return _plateBox;
    }

    /// <summary>A wrapping label. <paramref name="size"/> is a DESIGN size (UiTheme.Label sends it through UiTheme.Fs).</summary>
    public static Label Wrapped(string text, int size, Color color, float width, bool title = false)
    {
        var l = UiTheme.Label(text, size, color, title);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(width, 0);
        return l;
    }

    /// <summary>
    /// A BBCode block at the theme's running-text size, or at the DESIGN <paramref name="size"/> when one is given.
    /// [font_size] tags inside the BBCode must already carry UiTheme.Fs sizes.
    /// </summary>
    public static RichTextLabel Rich(string bbcode, float width, int size = 0)
    {
        var r = UiTheme.Rich(bbcode, width, size);
        r.BbcodeEnabled = true;
        r.FitContent = true;
        r.ScrollActive = false;
        r.MouseFilter = Control.MouseFilterEnum.Ignore;
        return r;
    }

    public static void ClearChildren(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            n.RemoveChild(c);
            c.QueueFree();
        }
    }

    /// <summary>
    /// The standard window of this module: a centred heavy frame, a title row with a close button, a rule and a body
    /// column. The panel manager already dims the game behind every panel, so <paramref name="dim"/> is normally 0.
    /// </summary>
    public static PanelContainer Shell(Control root, Vector2 size, string title, Action onClose, out HBoxContainer header, out VBoxContainer body, float dim)
    {
        if (dim > 0f)
        {
            var shade = new ColorRect { Color = new Color(0f, 0f, 0f, dim), MouseFilter = Control.MouseFilterEnum.Stop };
            root.AddChild(shade);
            FullRect(shade);
        }
        var frame = UiTheme.Frame(true) ?? new PanelContainer();
        frame.MouseFilter = Control.MouseFilterEnum.Stop;
        root.AddChild(frame);
        Center(frame, size);
        // a window that the largest text size grows to nearly the height of the screen has no room for the crest
        if (UiTheme.Art) frame.ItemRectChanged += () => FitCrest(frame);
        var margin = Margin(4, 0, 4, 2);            // the heavy frame brings its own padding
        frame.AddChild(margin);
        var col = VBox(12);
        margin.AddChild(col);
        header = HBox(16);
        col.AddChild(header);
        header.AddChild(UiTheme.Label(title, UiTheme.FsTitle, UiTheme.Gold, true));
        header.AddChild(Spacer());
        if (onClose != null) header.AddChild(UiTheme.Button("關閉", onClose, UiTheme.FsRow));
        col.AddChild(Rule());
        body = VBox(12);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(body);
        return frame;
    }

    private static readonly StringName MetaPlain = "world_plain";

    /// <summary>
    /// The crest of the heavy window frame rises 34 px above the window. A window standing closer than 36 px
    /// to the top of the screen wears the same moulding without the crest (both boxes have the same padding).
    /// </summary>
    private static void FitCrest(PanelContainer frame)
    {
        if (frame == null || !GodotObject.IsInstanceValid(frame) || frame.Size.Y < 8f) return;
        bool plain = frame.Position.Y < 35.5f;
        bool was = frame.HasMeta(MetaPlain) && frame.GetMeta(MetaPlain).AsBool();
        if (plain == was) return;
        frame.SetMeta(MetaPlain, plain);
        frame.AddThemeStyleboxOverride("panel", plain ? UiTheme.WindowPlainBox : UiTheme.WindowBox);
    }

    // ── immediate drawing ──

    /// <summary>
    /// Draws one line of text. <paramref name="size"/> is a DESIGN size: it goes through UiTheme.Fs here (text scale,
    /// 20 px floor), so callers pass plain design sizes and measure with <see cref="Measure"/> / <see cref="LineHeight"/>.
    /// </summary>
    public static void Text(CanvasItem ci, Font font, Vector2 pos, string text, int size, Color col,
        HorizontalAlignment align = HorizontalAlignment.Left, float width = -1f, int outline = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        int px = UiTheme.Fs(size);
        if (outline > 0) ci.DrawStringOutline(font, pos, text, align, width, px, outline, new Color(0f, 0f, 0f, 0.85f * col.A));
        ci.DrawString(font, pos, text, align, width, px, col);
    }

    /// <summary>The pixel size of one line of <paramref name="text"/> at the DESIGN <paramref name="size"/> (as <see cref="Text"/> draws it).</summary>
    public static Vector2 Measure(Font font, string text, int size) =>
        string.IsNullOrEmpty(text) || font == null ? Vector2.Zero : font.GetStringSize(text, HorizontalAlignment.Left, -1f, UiTheme.Fs(size));

    /// <summary>Line height of <paramref name="font"/> at the DESIGN <paramref name="size"/>: use it to step baselines instead of constants.</summary>
    public static float LineHeight(Font font, int size) => font == null ? UiTheme.Fs(size) * 1.4f : font.GetHeight(UiTheme.Fs(size));

    /// <summary>Ascent of <paramref name="font"/> at the DESIGN <paramref name="size"/>: baseline = top of the line + ascent.</summary>
    public static float Ascent(Font font, int size) => font == null ? UiTheme.Fs(size) : font.GetAscent(UiTheme.Fs(size));

    public static float Hash01(int x, int y, int seed = 0)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    public static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}

/// <summary>How one kind of map marker looks and what the legend calls it.</summary>
internal sealed class MapMarkStyle
{
    public string Name;
    public Color Col;
    public int Shape, Order;
    public float Size = 1f;
    public bool Major;      // labelled on the map without hovering
    public bool Always;     // what the hero is after right now (objectives, quest targets): drawn with a pulse
}

/// <summary>Marker glyphs drawn from primitives, so the map and its legend never depend on icon files.</summary>
internal static class MapGlyphs
{
    public const int Dot = 0, Diamond = 1, TriUp = 2, Square = 3, Star = 4, Ring = 5, Door = 6, Skull = 7, Tower = 8,
        Exclaim = 9, Flame = 10, TriDown = 11, Swirl = 12, Column = 13, Person = 14;

    private static readonly Dictionary<string, MapMarkStyle> Styles = new()
    {
        ["quest"] = new MapMarkStyle { Name = "任務目標", Col = new Color(1f, 0.83f, 0.35f), Shape = Exclaim, Order = 0, Size = 1.25f, Major = true, Always = true },
        ["objective"] = new MapMarkStyle { Name = "地城目標", Col = new Color(1f, 0.86f, 0.3f), Shape = Diamond, Order = 1, Size = 1.1f, Always = true },
        ["boss"] = new MapMarkStyle { Name = "首領", Col = new Color(1f, 0.25f, 0.12f), Shape = Skull, Order = 2, Size = 1.4f, Major = true },
        ["bossdoor"] = new MapMarkStyle { Name = "首領之門", Col = new Color(0.95f, 0.38f, 0.3f), Shape = Door, Order = 3, Size = 1.15f, Major = true },
        ["stronghold"] = new MapMarkStyle { Name = "燼塔據點", Col = new Color(1f, 0.52f, 0.2f), Shape = Tower, Order = 4, Size = 1.35f, Major = true },
        ["waypoint"] = new MapMarkStyle { Name = "燼塔（傳送點）", Col = new Color(0.45f, 0.82f, 1f), Shape = Flame, Order = 5, Size = 1.3f, Major = true },
        ["entrance"] = new MapMarkStyle { Name = "地城入口", Col = new Color(0.98f, 0.64f, 0.3f), Shape = Door, Order = 6, Size = 1.15f, Major = true },
        ["exit"] = new MapMarkStyle { Name = "出口", Col = new Color(0.88f, 0.85f, 0.74f), Shape = TriDown, Order = 7, Size = 1.1f, Major = true },
        ["portal"] = new MapMarkStyle { Name = "傳送門", Col = new Color(0.5f, 0.75f, 1f), Shape = Swirl, Order = 8, Size = 1.2f, Major = true },
        ["event"] = new MapMarkStyle { Name = "區域事件", Col = new Color(1f, 0.66f, 0.26f), Shape = Exclaim, Order = 9 },
        ["altar"] = new MapMarkStyle { Name = "燼火祭壇", Col = new Color(1f, 0.8f, 0.42f), Shape = TriUp, Order = 10 },
        ["shrine"] = new MapMarkStyle { Name = "神龕", Col = new Color(0.55f, 0.92f, 0.96f), Shape = Star, Order = 11 },
        ["chest"] = new MapMarkStyle { Name = "寶箱", Col = new Color(0.96f, 0.8f, 0.36f), Shape = Square, Order = 12, Size = 0.9f },
        ["idol"] = new MapMarkStyle { Name = "寂滅圖騰", Col = new Color(1f, 0.5f, 0.4f), Shape = Column, Order = 13 },
        ["prisoner"] = new MapMarkStyle { Name = "被囚的倖存者", Col = new Color(0.8f, 0.95f, 0.75f), Shape = Person, Order = 14 },
        ["elite"] = new MapMarkStyle { Name = "被標記的菁英", Col = new Color(1f, 0.6f, 0.25f), Shape = Skull, Order = 15 },
        ["elder"] = new MapMarkStyle { Name = "守燈長老", Col = new Color(1f, 0.86f, 0.5f), Shape = Person, Order = 20, Size = 1.1f, Major = true },
        ["smith"] = new MapMarkStyle { Name = "鐵匠", Col = new Color(0.92f, 0.8f, 0.66f), Shape = Person, Order = 21, Major = true },
        ["merchant"] = new MapMarkStyle { Name = "行商", Col = new Color(1f, 0.86f, 0.45f), Shape = Person, Order = 22, Major = true },
        ["occultist"] = new MapMarkStyle { Name = "祕術師", Col = new Color(0.8f, 0.6f, 1f), Shape = Person, Order = 23, Major = true },
        ["jeweler"] = new MapMarkStyle { Name = "琢匠", Col = new Color(0.6f, 0.9f, 1f), Shape = Person, Order = 24, Major = true },
        ["villager"] = new MapMarkStyle { Name = "鎮民", Col = new Color(0.7f, 0.76f, 0.66f), Shape = Dot, Order = 25, Size = 0.9f },
        ["npc"] = new MapMarkStyle { Name = "鎮民", Col = new Color(0.62f, 0.9f, 0.56f), Shape = Person, Order = 26, Major = true },
        ["stash"] = new MapMarkStyle { Name = "倉庫", Col = new Color(0.82f, 0.72f, 0.52f), Shape = Square, Order = 27, Major = true },
        ["brazier"] = new MapMarkStyle { Name = "大火盆", Col = new Color(1f, 0.6f, 0.2f), Shape = Flame, Order = 28 },
        ["tierstatue"] = new MapMarkStyle { Name = "階位石像", Col = new Color(0.82f, 0.68f, 1f), Shape = Column, Order = 29, Major = true },
        ["riftstone"] = new MapMarkStyle { Name = "裂隙石", Col = new Color(0.72f, 0.42f, 1f), Shape = Swirl, Order = 30, Size = 1.2f, Major = true },
        ["beacon"] = new MapMarkStyle { Name = "燼塔", Col = new Color(1f, 0.7f, 0.3f), Shape = Flame, Order = 31, Size = 1.3f, Major = true },
        ["start"] = new MapMarkStyle { Name = "來處", Col = new Color(0.72f, 0.72f, 0.66f), Shape = Ring, Order = 32 },
    };

    private static readonly MapMarkStyle Unknown = new() { Name = "標記", Col = new Color(0.75f, 0.73f, 0.68f), Shape = Dot, Order = 99 };
    private static readonly char[] KindSeparators = { ':', '_', '.' };

    /// <summary>Style of a marker kind. Compound kinds such as "npc:smith" or "npc_smith" resolve to their most specific part.</summary>
    public static MapMarkStyle Style(string kind)
    {
        if (string.IsNullOrEmpty(kind)) return Unknown;
        if (Styles.TryGetValue(kind, out var s)) return s;
        int cut = kind.IndexOfAny(KindSeparators);
        if (cut <= 0 || !(Styles.TryGetValue(kind.Substring(cut + 1), out s) || Styles.TryGetValue(kind.Substring(0, cut), out s))) s = Unknown;
        Styles[kind] = s;           // remember the answer, also when there is none
        return s;
    }

    private static readonly Vector2[] P3 = new Vector2[3], P4 = new Vector2[4], P6 = new Vector2[6];

    /// <summary>A marker on a dark disc so it reads on any terrain.</summary>
    public static void Draw(CanvasItem ci, MapMarkStyle st, Vector2 p, float r, float alpha = 1f)
    {
        ci.DrawCircle(p, r + 3f, new Color(0.02f, 0.015f, 0.012f, 0.8f * alpha));
        Shape(ci, st.Shape, p, r, new Color(st.Col, alpha));
    }

    public static void Shape(CanvasItem ci, int shape, Vector2 p, float r, Color c)
    {
        var dark = new Color(0.03f, 0.025f, 0.02f, c.A);
        switch (shape)
        {
            case Diamond:
                P4[0] = p + new Vector2(0, -r); P4[1] = p + new Vector2(r * 0.8f, 0); P4[2] = p + new Vector2(0, r); P4[3] = p + new Vector2(-r * 0.8f, 0);
                ci.DrawColoredPolygon(P4, c);
                break;
            case TriUp:
                P3[0] = p + new Vector2(0, -r); P3[1] = p + new Vector2(r * 0.9f, r * 0.72f); P3[2] = p + new Vector2(-r * 0.9f, r * 0.72f);
                ci.DrawColoredPolygon(P3, c);
                break;
            case TriDown:
                P3[0] = p + new Vector2(0, r); P3[1] = p + new Vector2(-r * 0.9f, -r * 0.72f); P3[2] = p + new Vector2(r * 0.9f, -r * 0.72f);
                ci.DrawColoredPolygon(P3, c);
                break;
            case Square:
                ci.DrawRect(new Rect2(p.X - r * 0.72f, p.Y - r * 0.72f, r * 1.44f, r * 1.44f), c);
                ci.DrawRect(new Rect2(p.X - r * 0.72f, p.Y - r * 0.12f, r * 1.44f, r * 0.24f), dark);
                break;
            case Star:
                P4[0] = p + new Vector2(0, -r); P4[1] = p + new Vector2(r * 0.3f, 0); P4[2] = p + new Vector2(0, r); P4[3] = p + new Vector2(-r * 0.3f, 0);
                ci.DrawColoredPolygon(P4, c);
                P4[0] = p + new Vector2(-r, 0); P4[1] = p + new Vector2(0, -r * 0.3f); P4[2] = p + new Vector2(r, 0); P4[3] = p + new Vector2(0, r * 0.3f);
                ci.DrawColoredPolygon(P4, c);
                break;
            case Ring:
                ci.DrawArc(p, r * 0.72f, 0f, Mathf.Tau, 20, c, MathF.Max(1.5f, r * 0.28f), true);
                break;
            case Door:
                ci.DrawCircle(p + new Vector2(0, -r * 0.2f), r * 0.68f, c);
                ci.DrawRect(new Rect2(p.X - r * 0.68f, p.Y - r * 0.2f, r * 1.36f, r * 1.1f), c);
                ci.DrawRect(new Rect2(p.X - r * 0.28f, p.Y - r * 0.05f, r * 0.56f, r * 0.95f), dark);
                break;
            case Skull:
                ci.DrawCircle(p + new Vector2(0, -r * 0.15f), r * 0.8f, c);
                ci.DrawRect(new Rect2(p.X - r * 0.42f, p.Y + r * 0.3f, r * 0.84f, r * 0.55f), c);
                ci.DrawCircle(p + new Vector2(-r * 0.32f, -r * 0.18f), r * 0.2f, dark);
                ci.DrawCircle(p + new Vector2(r * 0.32f, -r * 0.18f), r * 0.2f, dark);
                break;
            case Tower:
                ci.DrawRect(new Rect2(p.X - r * 0.5f, p.Y - r * 0.45f, r, r * 1.4f), c);
                ci.DrawRect(new Rect2(p.X - r * 0.8f, p.Y - r * 0.55f, r * 1.6f, r * 0.32f), c);
                ci.DrawRect(new Rect2(p.X - r * 0.8f, p.Y - r * 0.98f, r * 0.4f, r * 0.5f), c);
                ci.DrawRect(new Rect2(p.X - r * 0.2f, p.Y - r * 0.98f, r * 0.4f, r * 0.5f), c);
                ci.DrawRect(new Rect2(p.X + r * 0.4f, p.Y - r * 0.98f, r * 0.4f, r * 0.5f), c);
                break;
            case Exclaim:
                ci.DrawArc(p, r * 0.92f, 0f, Mathf.Tau, 20, c, MathF.Max(1.4f, r * 0.2f), true);
                ci.DrawRect(new Rect2(p.X - r * 0.15f, p.Y - r * 0.58f, r * 0.3f, r * 0.72f), c);
                ci.DrawCircle(p + new Vector2(0, r * 0.45f), r * 0.17f, c);
                break;
            case Flame:
                P6[0] = p + new Vector2(0, -r); P6[1] = p + new Vector2(r * 0.62f, -r * 0.05f); P6[2] = p + new Vector2(r * 0.5f, r * 0.62f);
                P6[3] = p + new Vector2(0, r * 0.95f); P6[4] = p + new Vector2(-r * 0.5f, r * 0.62f); P6[5] = p + new Vector2(-r * 0.62f, -r * 0.05f);
                ci.DrawColoredPolygon(P6, c);
                ci.DrawCircle(p + new Vector2(0, r * 0.35f), r * 0.26f, new Color(1f, 1f, 1f, 0.7f * c.A));
                break;
            case Swirl:
                ci.DrawArc(p, r * 0.9f, 0.4f, 4.6f, 16, c, MathF.Max(1.5f, r * 0.24f), true);
                ci.DrawArc(p, r * 0.45f, 3.5f, 7.7f, 12, c, MathF.Max(1.5f, r * 0.24f), true);
                break;
            case Column:
                ci.DrawRect(new Rect2(p.X - r * 0.3f, p.Y - r * 0.62f, r * 0.6f, r * 1.32f), c);
                ci.DrawRect(new Rect2(p.X - r * 0.62f, p.Y - r * 0.92f, r * 1.24f, r * 0.3f), c);
                ci.DrawRect(new Rect2(p.X - r * 0.62f, p.Y + r * 0.66f, r * 1.24f, r * 0.28f), c);
                break;
            case Person:
                ci.DrawCircle(p + new Vector2(0, -r * 0.5f), r * 0.38f, c);
                P3[0] = p + new Vector2(0, -r * 0.2f); P3[1] = p + new Vector2(r * 0.7f, r * 0.9f); P3[2] = p + new Vector2(-r * 0.7f, r * 0.9f);
                ci.DrawColoredPolygon(P3, c);
                break;
            default:
                ci.DrawCircle(p, r * 0.55f, c);
                break;
        }
    }
}

/// <summary>
/// The painted base of the world map of 瑟蘭: a parchment-and-ash continent generated once from value noise around the
/// travel routes, tinted per region, plus the scattered ink decorations (dead trees, peaks, barrows, reeds, dunes, spires).
/// Everything is laid out in a 1600 × 800 design space that the panel scales to fit.
/// </summary>
internal static class WorldMapArt
{
    public const float DW = 1600f, DH = 800f;
    private const float Coast = 0.30f;
    private const int TW = 1200, TH = 600, LW = 201, LH = 101;

    public sealed class Site
    {
        public string Id;
        public Vector2 P;
        public Color Tint;
        public float Sigma, Strength;
        public bool LabelAbove;
    }

    public sealed class Route
    {
        public Site A, B;
        public Vector2[] Pts;
    }

    public struct Deco
    {
        public byte Kind;
        public Vector2 P;
        public float S;
    }

    public const byte DeadTree = 0, Grave = 1, Peak = 2, Barrow = 3, Menhir = 4, Reeds = 5, Dune = 6, Pillar = 7, Spire = 8, Crag = 9, Hut = 10;

    public static readonly Site[] Sites =
    {
        new() { Id = "town", P = new Vector2(850, 655), Tint = new Color(0.50f, 0.37f, 0.20f), Sigma = 72f, Strength = 0.45f },
        new() { Id = "wastes", P = new Vector2(640, 495), Tint = new Color(0.33f, 0.33f, 0.35f), Sigma = 125f, Strength = 0.58f },
        new() { Id = "frost", P = new Vector2(500, 190), Tint = new Color(0.64f, 0.70f, 0.74f), Sigma = 135f, Strength = 0.72f, LabelAbove = true },
        new() { Id = "crypt", P = new Vector2(300, 420), Tint = new Color(0.26f, 0.33f, 0.25f), Sigma = 125f, Strength = 0.62f },
        new() { Id = "mire", P = new Vector2(1110, 610), Tint = new Color(0.25f, 0.29f, 0.14f), Sigma = 125f, Strength = 0.66f },
        new() { Id = "sands", P = new Vector2(1360, 400), Tint = new Color(0.58f, 0.43f, 0.22f), Sigma = 130f, Strength = 0.66f },
        new() { Id = "sanctum", P = new Vector2(980, 250), Tint = new Color(0.62f, 0.61f, 0.59f), Sigma = 112f, Strength = 0.56f },
        new() { Id = "abyss", P = new Vector2(1340, 115), Tint = new Color(0.09f, 0.065f, 0.065f), Sigma = 140f, Strength = 0.9f },
    };

    public static Site Find(string id)
    {
        foreach (var s in Sites) if (s.Id == id) return s;
        return null;
    }

    public static readonly Route[] Routes =
    {
        MakeRoute("town", "wastes", 22f), MakeRoute("wastes", "crypt", 24f), MakeRoute("wastes", "frost", 30f),
        MakeRoute("wastes", "mire", -34f), MakeRoute("mire", "sands", -30f), MakeRoute("frost", "sanctum", -36f),
        MakeRoute("sands", "sanctum", 28f), MakeRoute("sanctum", "abyss", -24f),
    };

    private static Route MakeRoute(string a, string b, float bend)
    {
        var r = new Route { A = Find(a), B = Find(b), Pts = new Vector2[34] };
        Vector2 pa = r.A.P, pb = r.B.P, d = pb - pa;
        var ctrl = (pa + pb) * 0.5f + new Vector2(-d.Y, d.X).Normalized() * bend * 2f;     // the curve bows by `bend` at its middle
        for (int i = 0; i < r.Pts.Length; i++)
        {
            float t = i / (float)(r.Pts.Length - 1), u = 1f - t;
            r.Pts[i] = pa * (u * u) + ctrl * (2f * u * t) + pb * (t * t);
        }
        return r;
    }

    // land that is not on a road: capes and hinterland that give the continent its outline
    private static readonly (Vector2 a, Vector2 b)[] Hinterland =
    {
        (new Vector2(180, 330), new Vector2(300, 420)), (new Vector2(300, 420), new Vector2(260, 560)),
        (new Vector2(640, 495), new Vector2(600, 640)), (new Vector2(850, 655), new Vector2(1000, 705)),
        (new Vector2(1110, 610), new Vector2(1250, 690)), (new Vector2(1360, 400), new Vector2(1470, 300)),
        (new Vector2(500, 190), new Vector2(380, 130)), (new Vector2(980, 250), new Vector2(900, 130)),
        (new Vector2(1340, 115), new Vector2(1480, 165)),
    };

    /// <summary>The slowly varying layers of the painting, sampled on a coarse lattice.</summary>
    private sealed class Fields
    {
        public float[] Stain, R, G, B, A, Abyss, Frost;
    }

    private const int Idle = 0, Painting = 1, Painted = 2, Done = 3, Failed = -1;
    private static int _state;                  // written by the painting task, read on the main thread
    private static byte[] _pixels;
    private static ImageTexture _tex;
    private static float[] _spine;
    public static readonly List<Deco> Decor = new();

    /// <summary>True once the painted texture exists (or can never exist): nothing more will change.</summary>
    public static bool Settled
    {
        get { int s = System.Threading.Volatile.Read(ref _state); return s == Done || s == Failed; }
    }

    /// <summary>The painted map, or null while it is still being painted in the background.</summary>
    public static Texture2D Texture
    {
        get { Ensure(); return _tex; }
    }

    /// <summary>
    /// Starts the painting on first use and collects it when it is finished. Call from the main thread; the per-pixel
    /// work (about a million noise samples) runs on a worker so opening the map never hitches.
    /// </summary>
    public static void Ensure()
    {
        int state = System.Threading.Volatile.Read(ref _state);
        if (state == Painted)
        {
            var buf = _pixels;
            _pixels = null;
            try
            {
                _tex = ImageTexture.CreateFromImage(Image.CreateFromData(TW, TH, false, Image.Format.Rgb8, buf));
                System.Threading.Volatile.Write(ref _state, Done);
            }
            catch (Exception e)
            {
                System.Threading.Volatile.Write(ref _state, Failed);
                Log.Error("map", "uploading the world map failed", e);
            }
            return;
        }
        if (state != Idle) return;
        try
        {
            var fields = BakeFields();
            BuildDecor();
            System.Threading.Volatile.Write(ref _state, Painting);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    _pixels = Paint(fields);
                    System.Threading.Volatile.Write(ref _state, Painted);
                    Log.Info("map", $"world map painted in {sw.ElapsedMilliseconds} ms ({TW}x{TH}, {Decor.Count} decorations)");
                }
                catch (Exception e)
                {
                    System.Threading.Volatile.Write(ref _state, Failed);
                    Log.Error("map", "painting the world map failed", e);
                }
            });
        }
        catch (Exception e)
        {
            System.Threading.Volatile.Write(ref _state, Failed);
            Log.Error("map", "preparing the world map failed", e);
        }
    }

    private static float VNoise(float x, float y, int seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float a = WorldUiKit.Hash01(xi, yi, seed), b = WorldUiKit.Hash01(xi + 1, yi, seed);
        float c = WorldUiKit.Hash01(xi, yi + 1, seed), d = WorldUiKit.Hash01(xi + 1, yi + 1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    private static float Fbm(float x, float y, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += VNoise(x, y, seed + i * 17) * amp;
            norm += amp;
            x *= 2.03f; y *= 2.03f; amp *= 0.5f;
        }
        return sum / norm;
    }

    private static float LandNoise(float u, float v) => (Fbm(u * 0.0062f, v * 0.0062f, 4, 11) - 0.5f) * 1.5f;

    private static float SpineAt(Vector2 p)
    {
        float gx = Math.Clamp(p.X / DW, 0f, 1f) * (LW - 1), gy = Math.Clamp(p.Y / DH, 0f, 1f) * (LH - 1);
        int i0 = Math.Min((int)gx, LW - 2), j0 = Math.Min((int)gy, LH - 2);
        float fx = gx - i0, fy = gy - j0;
        int k = j0 * LW + i0;
        return _spine[k] * (1 - fx) * (1 - fy) + _spine[k + 1] * fx * (1 - fy) + _spine[k + LW] * (1 - fx) * fy + _spine[k + LW + 1] * fx * fy;
    }

    public static bool IsLand(Vector2 p, float margin = 0f) =>
        _spine != null && p.X > 0 && p.Y > 0 && p.X < DW && p.Y < DH && SpineAt(p) + LandNoise(p.X, p.Y) > Coast + margin;

    /// <summary>Low-frequency fields on a coarse lattice (fast, main thread); the per-pixel pass only interpolates them.</summary>
    private static Fields BakeFields()
    {
        int n = LW * LH;
        var spineField = new float[n];
        float[] stain = new float[n], tr = new float[n], tg = new float[n], tb = new float[n], ta = new float[n], abyss = new float[n], frost = new float[n];
        Site sAbyss = Find("abyss"), sFrost = Find("frost");

        for (int j = 0; j < LH; j++)
            for (int i = 0; i < LW; i++)
            {
                var p = new Vector2(i * DW / (LW - 1), j * DH / (LH - 1));
                float dist = float.MaxValue;
                foreach (var r in Routes) dist = MathF.Min(dist, World.DistToSegment(p, r.A.P, r.B.P));
                foreach (var (a, b) in Hinterland) dist = MathF.Min(dist, World.DistToSegment(p, a, b) + 20f);
                int k = j * LW + i;
                spineField[k] = Math.Clamp(1f - dist / 200f, -0.6f, 1f);
                stain[k] = Fbm(p.X * 0.0035f + 7f, p.Y * 0.0035f + 3f, 3, 23);
                float r0 = 0f, g0 = 0f, b0 = 0f, a0 = 0f;
                foreach (var s in Sites)
                {
                    float w = MathF.Exp(-p.DistanceSquaredTo(s.P) / (2f * s.Sigma * s.Sigma)) * s.Strength;
                    r0 = r0 * (1f - w) + s.Tint.R * w; g0 = g0 * (1f - w) + s.Tint.G * w; b0 = b0 * (1f - w) + s.Tint.B * w;
                    a0 = a0 + w - a0 * w;
                }
                tr[k] = r0; tg[k] = g0; tb[k] = b0; ta[k] = a0;
                abyss[k] = MathF.Exp(-p.DistanceSquaredTo(sAbyss.P) / (2f * 150f * 150f));
                frost[k] = MathF.Exp(-p.DistanceSquaredTo(sFrost.P) / (2f * 140f * 140f));
            }
        _spine = spineField;            // published whole: the worker and IsLand only ever read it
        return new Fields { Stain = stain, R = tr, G = tg, B = tb, A = ta, Abyss = abyss, Frost = frost };
    }

    /// <summary>The per-pixel pass: coast, parchment, regional tints, snow, ember veins, sea ripples and burnt edges. Runs on a worker thread.</summary>
    private static byte[] Paint(Fields f)
    {
        float[] stain = f.Stain, tr = f.R, tg = f.G, tb = f.B, ta = f.A, abyss = f.Abyss, frost = f.Frost;
        var buf = new byte[TW * TH * 3];
        for (int y = 0; y < TH; y++)
        {
            float v = (y + 0.5f) / TH * DH, gy = v / DH * (LH - 1);
            int j0 = Math.Min((int)gy, LH - 2);
            float fy = gy - j0;
            for (int x = 0; x < TW; x++)
            {
                float u = (x + 0.5f) / TW * DW, gx = u / DW * (LW - 1);
                int i0 = Math.Min((int)gx, LW - 2);
                float fx = gx - i0;
                int k = j0 * LW + i0;
                float w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
                float spine = _spine[k] * w00 + _spine[k + 1] * w10 + _spine[k + LW] * w01 + _spine[k + LW + 1] * w11;
                float st = stain[k] * w00 + stain[k + 1] * w10 + stain[k + LW] * w01 + stain[k + LW + 1] * w11;
                float h = spine + LandNoise(u, v);
                float grain = VNoise(u * 0.42f, v * 0.42f, 5);
                float r, g, b;
                if (h > Coast)
                {
                    float a = ta[k] * w00 + ta[k + 1] * w10 + ta[k + LW] * w01 + ta[k + LW + 1] * w11;
                    float shade = 0.80f + 0.40f * st + (grain - 0.5f) * 0.08f;
                    r = (0.41f * (1f - a) + tr[k] * w00 + tr[k + 1] * w10 + tr[k + LW] * w01 + tr[k + LW + 1] * w11) * shade;
                    g = (0.35f * (1f - a) + tg[k] * w00 + tg[k + 1] * w10 + tg[k + LW] * w01 + tg[k + LW + 1] * w11) * shade;
                    b = (0.265f * (1f - a) + tb[k] * w00 + tb[k + 1] * w10 + tb[k + LW] * w01 + tb[k + LW + 1] * w11) * shade;

                    float fr = frost[k] * w00 + frost[k + 1] * w10 + frost[k + LW] * w01 + frost[k + LW + 1] * w11;
                    if (fr > 0.08f && grain > 0.6f)                                       // wind-blown snow
                    {
                        float sn = (grain - 0.6f) * 2.5f * fr * 0.5f;
                        r += (0.9f - r) * sn; g += (0.93f - g) * sn; b += (0.96f - b) * sn;
                    }
                    float ab = abyss[k] * w00 + abyss[k + 1] * w10 + abyss[k + LW] * w01 + abyss[k + LW + 1] * w11;
                    if (ab > 0.06f)                                                       // ember veins in the basalt
                    {
                        float ridge = MathF.Abs(VNoise(u * 0.035f, v * 0.035f, 91) - 0.5f);
                        if (ridge < 0.035f)
                        {
                            float gl = Math.Clamp((1f - ridge / 0.035f) * ab * 1.5f, 0f, 1f);
                            r += (1f - r) * gl; g += (0.42f - g) * gl; b += (0.08f - b) * gl;
                        }
                    }
                    float t = h - Coast;
                    if (t < 0.022f) { float q = t / 0.022f; q *= q; r = 0.06f + (r - 0.06f) * q; g = 0.048f + (g - 0.048f) * q; b = 0.04f + (b - 0.04f) * q; }
                    else if (t < 0.14f) { float q = 0.88f + 0.12f * (t - 0.022f) / 0.118f; r *= q; g *= q; b *= q; }
                }
                else
                {
                    float m = 0.72f + 0.56f * st + (grain - 0.5f) * 0.05f;
                    r = 0.082f * m; g = 0.078f * m; b = 0.088f * m;
                    float s = Coast - h, lift = 0f;
                    if (s < 0.03f) lift = 0.035f * (1f - s / 0.03f);
                    if (MathF.Abs(s - 0.07f) < 0.008f) lift += 0.05f;
                    else if (MathF.Abs(s - 0.15f) < 0.008f) lift += 0.035f;
                    else if (MathF.Abs(s - 0.25f) < 0.008f) lift += 0.022f;
                    r += lift; g += lift; b += lift * 1.1f;
                }
                // burnt, uneven edges
                float edge = MathF.Min(MathF.Min(u, DW - u), MathF.Min(v, DH - v)) / 130f + (st - 0.5f) * 0.5f;
                float vig = 0.2f + 0.8f * WorldUiKit.Smooth(edge);
                int o = (y * TW + x) * 3;
                buf[o] = (byte)(Math.Clamp(r * vig, 0f, 1f) * 255f);
                buf[o + 1] = (byte)(Math.Clamp(g * vig, 0f, 1f) * 255f);
                buf[o + 2] = (byte)(Math.Clamp(b * vig, 0f, 1f) * 255f);
            }
        }
        return buf;
    }

    private static void BuildDecor()
    {
        Decor.Clear();
        var rng = new Random(90417);
        Scatter(rng, "town", Hut, 5, 46f, 84f, 20f);
        Scatter(rng, "wastes", DeadTree, 22, 52f, 160f, 22f);
        Scatter(rng, "wastes", Grave, 7, 52f, 130f, 20f);
        Scatter(rng, "frost", Peak, 20, 50f, 180f, 27f);
        Scatter(rng, "crypt", Barrow, 13, 52f, 160f, 28f);
        Scatter(rng, "crypt", Menhir, 7, 52f, 140f, 20f);
        Scatter(rng, "mire", Reeds, 20, 52f, 160f, 24f);
        Scatter(rng, "sands", Dune, 16, 52f, 165f, 28f);
        Scatter(rng, "sands", Pillar, 5, 52f, 130f, 24f);
        Scatter(rng, "sanctum", Spire, 10, 46f, 110f, 22f);
        Scatter(rng, "sanctum", Peak, 6, 120f, 190f, 27f);
        Scatter(rng, "abyss", Crag, 14, 48f, 150f, 22f);
        Decor.Sort((a, b) => a.P.Y.CompareTo(b.P.Y));
    }

    private static void Scatter(Random rng, string site, byte kind, int count, float rMin, float rMax, float spacing)
    {
        var s = Find(site);
        int placed = 0;
        for (int tries = 0; tries < count * 16 && placed < count; tries++)
        {
            float ang = (float)rng.NextDouble() * MathF.Tau, d = rMin + (rMax - rMin) * (float)rng.NextDouble();
            var p = s.P + new Vector2(MathF.Cos(ang), MathF.Sin(ang) * 0.85f) * d;
            if (p.X < 40 || p.Y < 40 || p.X > DW - 40 || p.Y > DH - 40) continue;
            if (!IsLand(p, 0.06f) || Blocked(p)) continue;
            bool crowded = false;
            foreach (var o in Decor) if (o.P.DistanceSquaredTo(p) < spacing * spacing) { crowded = true; break; }
            if (crowded) continue;
            Decor.Add(new Deco { Kind = kind, P = p, S = 0.8f + 0.5f * (float)rng.NextDouble() });
            placed++;
        }
    }

    /// <summary>
    /// Keeps decorations off the node medallions, their label blocks and the roads. The label blocks are written at
    /// readable screen sizes (28 / 20 design px, see MapUi.LabelsOf), so they take about 124 × 150 map units a side.
    /// </summary>
    private static bool Blocked(Vector2 p)
    {
        foreach (var s in Sites)
        {
            if (p.DistanceSquaredTo(s.P) < 50f * 50f) return true;
            if (MathF.Abs(p.X - s.P.X) < 124f)
            {
                if (!s.LabelAbove && p.Y > s.P.Y + 20f && p.Y < s.P.Y + 150f) return true;
                if (s.LabelAbove && p.Y < s.P.Y - 20f && p.Y > s.P.Y - 150f) return true;
            }
        }
        foreach (var r in Routes)
            foreach (var q in r.Pts)
                if (q.DistanceSquaredTo(p) < 17f * 17f) return true;
        return p.X < 350f && p.Y < 176f;        // the title cartouche
    }
}

/// <summary>
/// Panel "map". Tab 區域 draws the current area from the nav grid under the fog of war, with markers, a legend and the
/// dungeon objective; tab 世界 is the painted map of 瑟蘭 with one node per region. Opened with the argument "travel"
/// (a waypoint or the town gate) the world tab becomes the travel screen.
/// </summary>
public partial class MapUi : UiPanel
{
    public override string Id => "map";
    public override bool Fullscreen => true;

    private const int SS = 4;                       // baked texels per nav cell
    private const float MaxScale = 22f;             // pixels per metre at full zoom

    // ── text sizes, in DESIGN pixels (1080p). Every one of them reaches the screen through UiTheme.Fs exactly once:
    //    inside UiTheme.Label / Small / Button, WorldUiKit.Text / Measure / LineHeight / Ascent, or in a BBCode tag.
    //    Text on the two maps is written in screen space at these sizes: the zoom of the area map and the fit of the
    //    world map move the labels, they never scale them. ──
    private const int FsTab = 24;                   // the two tab buttons and the banner beside them
    private const int FsPlace = 30;                 // the area name of the side column, the place name of a node tooltip
    private const int FsHead = 26;                  // side column section headings (目標, 任務追蹤, 圖例)
    private const int FsMark = UiTheme.FsCaption;   // marker names on the area map (20)
    private const int FsMarkHover = UiTheme.FsRow;  // the name of the marker under the pointer (22)
    private const int FsLegend = UiTheme.FsCaption; // legend entries of both tabs (20)
    private const int FsNode = 28, FsNodeTown = 30; // region names on the world map
    private const int FsNodeLine = UiTheme.FsCaption;   // the two small lines of a world map node (act / level, renown)
    private const int FsSea = 28;                   // names of the seas
    private const int FsLand = 56, FsLandSub = UiTheme.FsRow;   // the cartouche: 瑟蘭 and its subtitle
    private const int FsCompass = UiTheme.FsRow;    // 北
    /// <summary>Design width of the text in the side column of the area tab; it grows with the text scale (UiTheme.MinSize).</summary>
    private const float SideW = 400f;

    private sealed class NodeView
    {
        public WorldMapArt.Site Site;
        public RegionDef R;
        public string Id, Name, Line2, Line3, Reason, Entry;
        public bool Locked, Travel, Stronghold, Boss, Waypoint, Current, Quest;
        public int Dungeons, RenownTier, RenownPts;
        public bool[] DungeonDone = new bool[3];
        public Color Accent;
        // measured widths of the three label lines at the current text scale (LabelsOf fills them, OnTextScale drops them)
        public bool Measured;
        public float NameW, Line2W, Line3W;
    }

    /// <summary>Where the label block of a world map node sits on the canvas: laid out from the font metrics, in screen pixels.</summary>
    private struct NodeLabels
    {
        public float Top, Bottom, HalfW;                        // the whole block, for hit-testing
        public float NameBase, Line2Base, PipsY, Line3Base;     // baselines of the lines, centre of the row of pips
        public float PlateH, PlateHalfW;                        // the forged plate the name stands on (0 without the art)
    }

    /// <summary>A marker name waiting for a free spot on the area map.</summary>
    private struct MarkLabel
    {
        public MapMarker M;
        public MapMarkStyle St;
        public Vector2 P;
        public float R;
        public int I;
    }

    private bool _built, _open, _subscribed, _travel, _gate, _needFit, _dragging, _dirty, _worldPainted;
    private int _tab, _drawErrors;
    private float _t, _bannerT;

    private Button _tabAreaBtn, _tabWorldBtn;
    private Label _banner, _hint, _areaName, _areaSub, _areaInfo, _objText;
    private RichTextLabel _regionInfo, _trackText;
    private Control _areaPage, _worldPage, _areaCanvas, _areaOverlay, _areaFx, _areaRim, _objBar, _legend, _worldCanvas, _worldNodes, _worldFx, _worldRim;
    private Control _objHead, _trackHead, _legendHead, _hintKeys;

    // The raster pieces drawn by hand on the two maps, looked up once (null without the art: the flat drawing stays).
    private static bool _artLooked;
    private static UiArt.Piece _pWell, _pWellField, _pLine, _pPlate, _pHeadLine, _pTrack, _pFill, _pSocket,
        _pNodeOpen, _pNodeLocked, _pNodeLit, _pNodeHub;

    private static void LookArt()
    {
        if (_artLooked) return;
        _artLooked = true;
        if (!UiTheme.Art) return;
        _pWell = WorldUiKit.Piece("well"); _pWellField = WorldUiKit.Piece("field_well"); _pLine = WorldUiKit.Piece("boxline");
        _pPlate = WorldUiKit.Piece("nameplate"); _pHeadLine = WorldUiKit.Piece("header_line");
        _pTrack = WorldUiKit.Piece("slider_track"); _pFill = WorldUiKit.Piece("slider_fill"); _pSocket = WorldUiKit.Piece("socket");
        _pNodeOpen = WorldUiKit.Piece("node_round_avail"); _pNodeLocked = WorldUiKit.Piece("node_round_locked");
        _pNodeLit = WorldUiKit.Piece("node_round_owned"); _pNodeHub = WorldUiKit.Piece("node_key_owned");
    }

    // area tab
    private Level _lv;
    private NavGrid _grid;
    private ImageTexture _areaTex;
    private int _texW, _texH;
    private byte[] _vis;
    private Vector2 _focus, _right = new(1, 0), _fwd = new(0, -1);
    private float _scale = 6f, _fitScale = 3f, _objProgress, _explored;
    private bool _objDone;
    private MapMarker _hoverMarker;
    private readonly List<MapMarkStyle> _legendStyles = new();
    private readonly List<MapMarker> _goals = new();     // exits and entrances that lead toward the main quest
    // quest pins (QuestMarks.cs): the kinds shown in the legend, and the pin under the pointer
    private readonly List<(QuestMarkKind kind, bool main)> _legendQuest = new();
    private sealed class HoverQ { public object Ref; public QuestMarkKind Kind; public bool Main; public string Name, Text; public Vector2 Pos; }
    private HoverQ _hoverQuest;
    private static readonly Vector2[] T3 = new Vector2[3];
    // marker names of one overlay redraw: the candidates in order of importance, and the screen space already spoken for
    private readonly List<MarkLabel> _labelQueue = new();
    private readonly List<Rect2> _glyphBoxes = new(), _labelBoxes = new();
    private static readonly Comparison<MarkLabel> LabelOrder = (a, b) =>
    {
        if (a.St.Always != b.St.Always) return a.St.Always ? -1 : 1;        // what the hero is after comes first
        int byKind = a.St.Order.CompareTo(b.St.Order);
        return byKind != 0 ? byKind : a.I.CompareTo(b.I);                   // stable: panning must not shuffle equals
    };

    // world tab
    private readonly List<NodeView> _nodes = new();
    private NodeView _hoverNode;
    private Vector2 _wOrg;
    private float _wFit;

    public override void _Ready()
    {
        base._Ready();
        Log.Guard("map", EnsureBuilt);
    }

    // ───────────────────────── construction ─────────────────────────

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        MouseFilter = MouseFilterEnum.Ignore;        // the panel manager lays the dimmed backdrop underneath
        WorldUiKit.FullRect(this);

        var frame = UiTheme.Frame(true) ?? new PanelContainer();
        AddChild(frame);
        frame.SetAnchorsPreset(LayoutPreset.FullRect);
        // (the crest's longest ray rises 34 px above the frame: 38 px keeps its tip clear of the top of the screen)
        frame.OffsetLeft = 56; frame.OffsetTop = 38; frame.OffsetRight = -56; frame.OffsetBottom = -32;

        var margin = WorldUiKit.Margin(2, 0, 2, 0);
        frame.AddChild(margin);
        var col = WorldUiKit.VBox(10);
        margin.AddChild(col);

        var header = WorldUiKit.HBox(14);
        col.AddChild(header);
        header.AddChild(UiTheme.Label("地圖", UiTheme.FsTitle, UiTheme.Gold, true));
        header.AddChild(new Control { CustomMinimumSize = new Vector2(18, 0), MouseFilter = MouseFilterEnum.Ignore });
        _tabAreaBtn = UiTheme.MinSize(UiTheme.Button("區域", () => SetTab(0), FsTab), 120f, 0f);     // the tabs widen with the text
        _tabWorldBtn = UiTheme.MinSize(UiTheme.Button("世界", () => SetTab(1), FsTab), 120f, 0f);
        header.AddChild(_tabAreaBtn);
        header.AddChild(_tabWorldBtn);
        // one line between the tabs and the close button: what does not fit is cut with an ellipsis, never spilled
        _banner = UiTheme.Trimmed(UiTheme.Label("", FsTab, UiTheme.Gold, true), false);
        _banner.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        _banner.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(_banner);
        header.AddChild(UiTheme.Button("關閉", () => WorldUiKit.ClosePanel(Id), UiTheme.FsRow));
        col.AddChild(WorldUiKit.Rule());

        // (not clipped: the side column's moulding casts its shadow a little past the page; the two map panes and
        // the scroller clip what they hold themselves)
        var pages = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
        col.AddChild(pages);

        BuildAreaPage(pages);
        BuildWorldPage(pages);

        // The hint line. The area tab names three controls, shown as key caps with what each one does; the world
        // tab speaks in a sentence (the longest is 40 glyphs: about 1040 px at 特大, well inside the frame). Both
        // stand in one row of a fixed height, so switching tabs never moves the map.
        var hints = UiTheme.MinSize(WorldUiKit.HBox(0), 0f, 32f);
        hints.Alignment = BoxContainer.AlignmentMode.Center;
        hints.MouseFilter = MouseFilterEnum.Ignore;
        col.AddChild(hints);
        var keys = WorldUiKit.HBox(34);
        keys.MouseFilter = MouseFilterEnum.Ignore;
        keys.AddChild(WorldUiKit.HintRow(UiTheme.Dim, "", "拖曳", "平移"));
        keys.AddChild(WorldUiKit.HintRow(UiTheme.Dim, "", "滾輪", "縮放"));
        keys.AddChild(WorldUiKit.HintRow(UiTheme.Dim, "", "空白鍵", "回到角色身邊"));
        hints.AddChild(keys);
        _hintKeys = keys;
        _hint = UiTheme.Label("", UiTheme.FsCaption, UiTheme.Dim);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.VerticalAlignment = VerticalAlignment.Center;
        hints.AddChild(_hint);
        SetTab(0);
    }

    private static Control Layer(Control parent, bool input)
    {
        var c = new Control { MouseFilter = input ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore, ClipContents = true };
        parent.AddChild(c);
        WorldUiKit.FullRect(c);
        return c;
    }

    private void BuildAreaPage(Control pages)
    {
        var row = WorldUiKit.HBox(16);
        pages.AddChild(row);
        WorldUiKit.FullRect(row);
        _areaPage = row;

        _areaCanvas = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true,
            MouseFilter = MouseFilterEnum.Stop, TextureFilter = TextureFilterEnum.Linear, MouseDefaultCursorShape = CursorShape.Drag,
        };
        row.AddChild(_areaCanvas);
        _areaOverlay = Layer(_areaCanvas, false);
        _areaFx = Layer(_areaCanvas, false);
        _areaRim = Layer(_areaCanvas, false);       // the rim of the well the map lies in: over the map and everything on it
        _areaCanvas.Draw += DrawAreaBase;
        _areaOverlay.Draw += DrawAreaOverlay;
        _areaFx.Draw += DrawAreaFx;
        _areaRim.Draw += DrawAreaRim;
        _areaCanvas.GuiInput += OnAreaInput;
        _areaCanvas.Resized += OnAreaResized;
        _areaCanvas.MouseExited += OnAreaMouseExited;

        // The side column is one sub-panel: the place, then what there is to do, the quest tracker and the legend,
        // each section under its own heading. The width of its text follows the text scale (400 / 460 / 520 px), and
        // whatever does not fit the height of the pane scrolls inside the moulding.
        var sidePanel = UiTheme.Frame(false) ?? new PanelContainer();
        sidePanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        sidePanel.MouseFilter = MouseFilterEnum.Pass;
        row.AddChild(sidePanel);
        var scroll = UiTheme.MinSize(new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        }, SideW + 24f, 0f);
        sidePanel.AddChild(scroll);
        var side = UiTheme.MinSize(WorldUiKit.VBox(5), SideW + 8f, 0f);
        side.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(side);

        _areaName = SideText(FsPlace, UiTheme.Gold, true);
        side.AddChild(_areaName);
        _areaSub = SideText(UiTheme.FsRow, UiTheme.Dim);
        side.AddChild(_areaSub);
        _areaInfo = SideText(UiTheme.FsRow, UiTheme.Text);
        side.AddChild(_areaInfo);
        side.AddChild(WorldUiKit.Rule(0.5f));

        var objBox = WorldUiKit.VBox(6);
        objBox.MouseFilter = MouseFilterEnum.Ignore;
        objBox.AddChild(WorldUiKit.Heading("目標", FsHead));
        _objHead = objBox;
        side.AddChild(objBox);
        _objText = SideText(UiTheme.FsBody, UiTheme.Text);
        side.AddChild(_objText);
        _objBar = UiTheme.MinSize(new Control { MouseFilter = MouseFilterEnum.Ignore }, 0f, 24f);      // the trough, and a little air under it
        _objBar.Draw += DrawObjectiveBar;
        side.AddChild(_objBar);

        // the region's checklist and the quest tracker are lists: row size
        _regionInfo = UiTheme.MinSize(WorldUiKit.Rich("", SideW, UiTheme.FsRow), SideW, 0f);
        side.AddChild(_regionInfo);

        var trackBox = WorldUiKit.VBox(6);
        trackBox.MouseFilter = MouseFilterEnum.Ignore;
        trackBox.AddChild(SideGap());
        trackBox.AddChild(WorldUiKit.Heading("任務追蹤", FsHead));
        _trackHead = trackBox;
        side.AddChild(trackBox);
        _trackText = UiTheme.MinSize(WorldUiKit.Rich("", SideW, UiTheme.FsRow), SideW, 0f);
        side.AddChild(_trackText);

        var legendBox = WorldUiKit.VBox(6);
        legendBox.MouseFilter = MouseFilterEnum.Ignore;
        legendBox.AddChild(SideGap());
        legendBox.AddChild(WorldUiKit.Heading("圖例", FsHead));
        _legendHead = legendBox;
        side.AddChild(legendBox);
        _legend = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _legend.Draw += DrawLegend;
        side.AddChild(_legend);
        SizeLegend();
    }

    /// <summary>The air above a section heading of the side column.</summary>
    private static Control SideGap() => new() { CustomMinimumSize = new Vector2(0f, 6f), MouseFilter = MouseFilterEnum.Ignore };

    /// <summary>A wrapping label of the side column; <paramref name="size"/> is a DESIGN size.</summary>
    private static Label SideText(int size, Color color, bool title = false) =>
        UiTheme.MinSize(WorldUiKit.Wrapped("", size, color, SideW, title), SideW, 0f);

    /// <summary>One legend row of the area tab: the line of text (its box carries air of its own), never less than the glyphs need.</summary>
    private static float LegendRow => MathF.Max(26f, MathF.Ceiling(WorldUiKit.LineHeight(WorldUiKit.SmallFont, FsLegend) * 0.95f));

    /// <summary>The legend is drawn by hand, so its box is sized by hand too: from the row height at the current text scale.</summary>
    private void SizeLegend()
    {
        if (_legend == null) return;
        int rows = _legendStyles.Count + 2 + (_goals.Count > 0 ? 1 : 0)        // + the hero and the fog
            + (_legendQuest.Count > 0 ? _legendQuest.Count + 1 : 0);             // + the quest pins and their colour key
        _legend.CustomMinimumSize = new Vector2(UiTheme.Px(SideW), LegendRow * rows + 4f);
    }

    private void BuildWorldPage(Control pages)
    {
        var page = WorldUiKit.VBox(6);
        page.MouseFilter = MouseFilterEnum.Ignore;
        pages.AddChild(page);
        WorldUiKit.FullRect(page);
        _worldPage = page;

        _worldCanvas = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true,
            MouseFilter = MouseFilterEnum.Stop, TextureFilter = TextureFilterEnum.Linear,
        };
        page.AddChild(_worldCanvas);
        _worldNodes = Layer(_worldCanvas, false);
        _worldFx = Layer(_worldCanvas, false);
        _worldRim = Layer(_worldCanvas, false);     // the rim of the well the chart lies in: over the chart and its nodes
        _worldCanvas.Draw += DrawWorldBase;
        _worldNodes.Draw += DrawWorldNodes;
        _worldFx.Draw += DrawWorldFx;
        _worldRim.Draw += DrawWorldRim;
        _worldCanvas.GuiInput += OnWorldInput;
        _worldCanvas.Resized += OnWorldResized;
        _worldCanvas.MouseExited += OnWorldMouseExited;

        // The key to the node symbols. It used to be a box painted into the south-east corner of the map; at readable
        // sizes that box would cover the label blocks of the regions around it, so it is a strip of real labels under
        // the map now: one row at 1920 wide, and it wraps by itself when a row is not enough.
        var key = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        key.AddThemeConstantOverride("h_separation", 20);
        key.AddThemeConstantOverride("v_separation", 0);
        page.AddChild(key);
        WorldKey(key, "據點已解放", (ci, c, r) => MapGlyphs.Shape(ci, MapGlyphs.Tower, c, r, new Color(1f, 0.55f, 0.2f)));
        WorldKey(key, "地城已淨空（共三座）", (ci, c, r) => MapGlyphs.Shape(ci, MapGlyphs.Diamond, c, r, new Color(0.8f, 0.75f, 0.6f)));
        WorldKey(key, "首領尚存；劃去即已伏誅", (ci, c, r) => MapGlyphs.Shape(ci, MapGlyphs.Skull, c, r, Palette.Danger));
        WorldKey(key, "燼塔已點燃，可傳送", (ci, c, r) => MapGlyphs.Shape(ci, MapGlyphs.Flame, c, r * 1.08f, new Color(0.45f, 0.82f, 1f)));
        WorldKey(key, "外環：名望階級", (ci, c, r) =>
        {
            ci.DrawArc(c, r, -Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, 10, UiTheme.Gold, MathF.Max(2f, r * 0.4f), true);
            ci.DrawArc(c, r, Mathf.Pi * 0.5f, Mathf.Pi * 1.5f, 10, new Color(0.3f, 0.28f, 0.26f), MathF.Max(2f, r * 0.4f), true);
        });
        WorldKey(key, "主線的下一步", (ci, c, r) => MapGlyphs.Shape(ci, MapGlyphs.Exclaim, c, r * 1.08f, UiTheme.Gold));
        WorldKey(key, "你的所在", (ci, c, r) => MapGlyphs.Shape(ci, MapGlyphs.TriDown, c, r, new Color(1f, 0.9f, 0.6f)));
    }

    /// <summary>One entry of the world map key: a glyph that grows with the text, and its caption.</summary>
    private void WorldKey(Control row, string text, Action<CanvasItem, Vector2, float> draw)
    {
        var cell = WorldUiKit.HBox(6);
        cell.MouseFilter = MouseFilterEnum.Ignore;
        var glyph = UiTheme.MinSize(new Control { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter }, 22f, 22f);
        glyph.Draw += () =>
        {
            try
            {
                var s = glyph.Size;
                draw(glyph, s * 0.5f, MathF.Min(s.X, s.Y) * 0.34f);
            }
            catch (Exception e) { DrawFail(e); }
        };
        cell.AddChild(glyph);
        var caption = UiTheme.Small(text, FsLegend, new Color(0.86f, 0.82f, 0.74f));
        caption.VerticalAlignment = VerticalAlignment.Center;
        cell.AddChild(caption);
        row.AddChild(cell);
    }

    // ───────────────────────── panel life cycle ─────────────────────────

    public override void OnOpen(object arg)
    {
        EnsureBuilt();
        WorldUiKit.FullRect(this);
        string a = arg as string ?? "";
        var lv = CurrentLevel();
        _travel = a.StartsWith("travel", StringComparison.Ordinal) || a == "gate" || a == "waypoint";
        _gate = _travel && (a.Contains("gate") || a.Contains("town") || (lv != null && lv.IsTown));
        _open = true;
        _t = 0f; _bannerT = 0f;
        _dragging = false; _hoverMarker = null; _hoverNode = null; _dirty = false;
        if (!_subscribed)
        {
            _subscribed = true;
            try { Quests.Changed += OnStoryChanged; }
            catch (Exception e) { Log.Warn("map", "cannot listen to quest changes: " + e.Message); }
        }
        BindLevel(lv);
        ReadCameraBasis();
        Refresh();
        _needFit = true;
        SetTab(_travel || _grid == null ? 1 : 0);
        Log.Info("map", $"opened area={lv?.AreaId ?? "-"} travel={_travel} gate={_gate} tab={_tab}");
    }

    public override void OnClose()
    {
        _open = false;
        _dragging = false;
        _hoverMarker = null; _hoverNode = null;
        if (_subscribed)
        {
            _subscribed = false;
            try { Quests.Changed -= OnStoryChanged; }
            catch (Exception e) { Log.Warn("map", "cannot stop listening to quest changes: " + e.Message); }
        }
        BindLevel(null);
        WorldUiKit.TipHide();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_open) Log.Guard("map", OnClose);
    }

    private static Level CurrentLevel()
    {
        var lv = Game.I?.Level;
        return lv != null && IsInstanceValid(lv) ? lv : null;
    }

    private void BindLevel(Level lv)
    {
        if (ReferenceEquals(lv, _lv)) return;
        if (_lv != null) _lv.Changed -= OnLevelChanged;
        _lv = lv;
        if (_lv != null) _lv.Changed += OnLevelChanged;
    }

    private void OnLevelChanged() => _dirty = true;
    private void OnStoryChanged() => _dirty = true;

    public override void Refresh()
    {
        if (!_built || !_open) return;
        Log.Guard("map", () =>
        {
            var lv = CurrentLevel();
            if (!ReferenceEquals(lv, _lv)) { BindLevel(lv); _needFit = true; }
            BakeArea();
            RefreshAreaInfo();
            RefreshWorld();
            UpdateChrome();
            RedrawAll();
        });
    }

    /// <summary>
    /// The text-size setting changed. The manager has already rescaled the labels, the BBCode and the UiTheme.MinSize
    /// boxes of this panel (the side column and the tab buttons among them); what is laid out by hand here follows:
    /// the measured label widths of the world map nodes are dropped, the hand-drawn legend gets its new height, and
    /// the area map is fitted again because the side column has changed width. Everything drawn with WorldUiKit.Text
    /// picks the new size up by itself on the next redraw.
    /// </summary>
    public override void OnTextScale()
    {
        if (!_built) return;
        Log.Guard("map", () =>
        {
            foreach (var n in _nodes) n.Measured = false;
            SizeLegend();
            _hoverMarker = null;
            if (_hoverNode != null) { _hoverNode = null; WorldUiKit.TipHide(); }
            _needFit = true;
            RedrawAll();
        });
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!_open) return;
        try
        {
            float dt = (float)delta;
            _t += dt;
            if (_dirty) { _dirty = false; Refresh(); }
            if (_bannerT > 0f)
            {
                _bannerT -= dt;
                if (_bannerT <= 0f) UpdateChrome();
            }
            if (_tab == 0)
            {
                if (_needFit && _areaCanvas.Size.X > 16f && _areaCanvas.Size.Y > 16f) FitArea();
                _areaFx.QueueRedraw();
            }
            else _worldFx.QueueRedraw();
            if (!_worldPainted)
            {
                // the world map is painted on a worker thread; collect it when it is done and show it
                WorldMapArt.Ensure();
                if (WorldMapArt.Settled) { _worldPainted = true; _worldCanvas.QueueRedraw(); }
            }
        }
        catch (Exception e) { DrawFail(e); }
    }

    public override void _Input(InputEvent e)
    {
        if (_open && IsVisibleInTree() && _tab == 0 && e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Space)
        {
            CenterOnHero();
            GetViewport()?.SetInputAsHandled();
            return;
        }
        base._Input(e);
    }

    private void DrawFail(Exception e)
    {
        if (_drawErrors++ < 3) Log.Error("map", "map update failed", e);
    }

    private void RedrawAll()
    {
        _areaCanvas.QueueRedraw(); _areaOverlay.QueueRedraw(); _areaFx.QueueRedraw(); _areaRim.QueueRedraw();
        _legend.QueueRedraw(); _objBar.QueueRedraw();
        _worldCanvas.QueueRedraw(); _worldNodes.QueueRedraw(); _worldFx.QueueRedraw(); _worldRim.QueueRedraw();
    }

    private void SetTab(int tab)
    {
        _tab = tab;
        _areaPage.Visible = tab == 0;
        _worldPage.Visible = tab == 1;
        UiTheme.TabTone(_tabAreaBtn, tab == 0);
        UiTheme.TabTone(_tabWorldBtn, tab == 1);
        _hoverMarker = null;
        if (_hoverNode != null) { _hoverNode = null; WorldUiKit.TipHide(); }
        UpdateChrome();
        RedrawAll();
    }

    private void UpdateChrome()
    {
        if (_banner == null || _hint == null) return;
        if (_bannerT > 0f) return;                       // a travel refusal is still on display
        _banner.AddThemeColorOverride("font_color", UiTheme.Gold);
        _banner.Text = _travel ? (_tab == 1 ? "選擇目的地" : "切換到「世界」以選擇目的地") : "";
        // the area tab's hint (拖曳：平移　滾輪：縮放　空白鍵：回到角色身邊) is the row of key caps built in EnsureBuilt
        if (_hintKeys != null) _hintKeys.Visible = _tab == 0;
        _hint.Visible = _tab != 0;
        if (_tab == 0) _hint.Text = "";
        else if (_travel) _hint.Text = _gate ? "點擊發亮的地點即可啟程——從殘燈鎮出發，能前往所有你已能踏足的土地。" : "點擊發亮的地點即可啟程——只有燼塔已重燃之處才能抵達。";
        else _hint.Text = "將游標移到地點上可檢視詳情。要啟程，請使用殘燈鎮的燼塔路標，或各地已重燃的燼塔。";
    }

    private void Refuse(string reason)
    {
        WorldUiKit.Sfx("ui_error");
        _banner.Text = reason;
        _banner.AddThemeColorOverride("font_color", Palette.Bad);
        _bannerT = 2.6f;
    }

    // ───────────────────────── area tab: data ─────────────────────────

    private void ReadCameraBasis()
    {
        _right = new Vector2(1, 0); _fwd = new Vector2(0, -1);
        var cam = Game.I?.Cam?.Cam;
        if (cam == null || !IsInstanceValid(cam) || !cam.IsInsideTree()) return;
        var b = cam.GlobalTransform.Basis;
        var f = new Vector2(-b.Z.X, -b.Z.Z);                                  // the camera looks along its −Z
        if (f.LengthSquared() < 1e-4f) f = new Vector2(b.Y.X, b.Y.Z);         // looking straight down: use its up vector
        if (f.LengthSquared() < 1e-4f) return;
        _fwd = f.Normalized();
        _right = new Vector2(-_fwd.Y, _fwd.X);
    }

    private static bool TryHero(out Vector2 pos, out Vector2 forward)
    {
        pos = default; forward = new Vector2(0, 1);
        var p = Game.I?.Player ?? World.P;
        if (p == null || !IsInstanceValid(p) || !p.IsInsideTree()) return false;
        pos = p.Pos2; forward = p.Forward;
        return true;
    }

    /// <summary>Paints the explored part of the nav grid into a texture: SS × SS texels per cell.</summary>
    private void BakeArea()
    {
        _areaTex = null; _grid = null; _explored = 0f;
        _legendStyles.Clear();
        _goals.Clear();
        var lv = _lv;
        if (lv == null) return;
        var g = lv.Grid ?? lv.Data?.Grid;
        if (g == null || g.W <= 0 || g.H <= 0 || g.T == null) return;
        _grid = g;
        int w = g.W, h = g.H, n = w * h;
        var cells = g.T;
        var seen = lv.Seen;
        bool allSeen = seen == null || seen.Length < n;
        var deco = lv.Data?.Deco;
        if (deco != null && deco.Length < n) deco = null;
        if (_vis == null || _vis.Length < n) _vis = new byte[n];

        bool SeenOpen(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            int i = y * w + x;
            return cells[i] != NavGrid.Wall && (allSeen || seen[i]);
        }

        int floorAll = 0, floorSeen = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                byte v = 0;
                if (cells[i] != NavGrid.Wall)
                {
                    bool s = allSeen || seen[i];
                    if (cells[i] == NavGrid.Floor) { floorAll++; if (s) floorSeen++; }
                    if (s) v = 2;
                    else if (SeenOpen(x - 1, y) || SeenOpen(x + 1, y) || SeenOpen(x, y - 1) || SeenOpen(x, y + 1)) v = 1;   // soft edge of the fog
                }
                else if (SeenOpen(x - 1, y) || SeenOpen(x + 1, y) || SeenOpen(x, y - 1) || SeenOpen(x, y + 1)
                    || SeenOpen(x - 1, y - 1) || SeenOpen(x + 1, y - 1) || SeenOpen(x - 1, y + 1) || SeenOpen(x + 1, y + 1)) v = 2;   // only the rim of solid rock is inked
                _vis[i] = v;
            }
        _explored = floorAll > 0 ? floorSeen * 100f / floorAll : 0f;

        var theme = lv.Theme;
        Color accent = theme?.Accent ?? lv.Region?.Accent ?? new Color(0.8f, 0.7f, 0.5f);
        string pitKind = theme?.PitKind ?? "none";
        Color cPit = pitKind == "lava" ? new Color(0.80f, 0.31f, 0.08f) : pitKind == "water" ? new Color(0.15f, 0.25f, 0.33f) : new Color(0.035f, 0.03f, 0.055f);
        Color cGround = new Color(0.40f, 0.36f, 0.30f).Lerp(accent, 0.10f);
        Color cAlt = new Color(0.36f, 0.33f, 0.28f).Lerp(accent, 0.14f);
        Color cRoad = new Color(0.62f, 0.55f, 0.43f);
        Color cRoom = new Color(0.47f, 0.43f, 0.38f).Lerp(accent, 0.06f);
        Color cArena = new Color(0.55f, 0.36f, 0.28f);
        Color cShore = new Color(0.33f, 0.31f, 0.28f);
        Color cWall = new Color(0.13f, 0.115f, 0.10f);

        int tw = w * SS, th = h * SS;
        var buf = new byte[tw * th * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                byte v = _vis[i];
                if (v == 0) continue;
                byte t = cells[i];
                Color c;
                if (t == NavGrid.Wall) c = cWall;
                else if (t == NavGrid.Pit) c = cPit;
                else
                    switch (deco != null ? deco[i] : (byte)0)
                    {
                        case 1: c = cRoad; break;
                        case 2: c = cAlt; break;
                        case 3: c = cRoom; break;
                        case 4: c = cArena; break;
                        case 5: c = cShore; break;
                        default: c = cGround; break;
                    }
                float jitter = 0.93f + 0.14f * WorldUiKit.Hash01(x, y, 3);
                byte alpha = (byte)(v == 1 ? 92 : t == NavGrid.Wall ? 235 : 245);
                bool floor = t == NavGrid.Floor;
                bool eL = floor && g.Get(x - 1, y) != NavGrid.Floor, eR = floor && g.Get(x + 1, y) != NavGrid.Floor;
                bool eT = floor && g.Get(x, y - 1) != NavGrid.Floor, eB = floor && g.Get(x, y + 1) != NavGrid.Floor;
                for (int py = 0; py < SS; py++)
                    for (int px = 0; px < SS; px++)
                    {
                        float k = jitter;
                        if (floor && ((px == 0 && eL) || (px == SS - 1 && eR) || (py == 0 && eT) || (py == SS - 1 && eB))) k *= 0.5f;   // inked outline
                        else if (t == NavGrid.Wall && (px == 0 || py == 0)) k *= 1.5f;                                                // a little relief on the rim
                        int o = ((y * SS + py) * tw + x * SS + px) * 4;
                        buf[o] = (byte)(Math.Clamp(c.R * k, 0f, 1f) * 255f);
                        buf[o + 1] = (byte)(Math.Clamp(c.G * k, 0f, 1f) * 255f);
                        buf[o + 2] = (byte)(Math.Clamp(c.B * k, 0f, 1f) * 255f);
                        buf[o + 3] = alpha;
                    }
            }
        _areaTex = ImageTexture.CreateFromImage(Image.CreateFromData(tw, th, false, Image.Format.Rgba8, buf));
        _texW = tw; _texH = th;

        var marks = lv.Markers;
        if (marks != null)
        {
            FindGoals(lv, marks);
            foreach (var m in marks)
            {
                if (!Shown(m)) continue;
                var st = MapGlyphs.Style(m.Kind);
                if (!_legendStyles.Contains(st)) _legendStyles.Add(st);
            }
        }
        _legendStyles.Sort((a, b) => a.Order.CompareTo(b.Order));
        BakeQuestLegend(lv);
    }

    /// <summary>Marks the exits and dungeon entrances of this area that lead to where the main quest continues.</summary>
    private void FindGoals(Level lv, List<MapMarker> marks)
    {
        string quest = WorldUiKit.NextObjectiveArea();
        if (string.IsNullOrEmpty(quest) || quest == lv.AreaId) return;
        string region = quest == ZoneDb.Town ? null : ZoneDb.RegionOf(quest)?.Id;
        string questName = ZoneDb.AreaName(quest), regionName = region != null ? ZoneDb.Region(region)?.Name : null;

        bool Leads(object target)
        {
            if (target is string s) return s == quest || (region != null && s == region);
            if (target is DungeonDef dg) return dg.Id == quest;
            return false;
        }

        foreach (var m in marks)
        {
            if (m == null || !m.Visible || (m.Kind != "exit" && m.Kind != "entrance")) continue;
            bool goal = Leads(m.Ref) || (m.Ref is Interactable it && IsInstanceValid(it) && Leads(it.Tag));
            if (!goal && !string.IsNullOrEmpty(m.Label))
                goal = m.Label == questName || m.Label == quest || (regionName != null && m.Label == regionName)
                    || m.Label.EndsWith(questName, StringComparison.Ordinal) || (regionName != null && m.Label.EndsWith(regionName, StringComparison.Ordinal));
            if (goal) _goals.Add(m);
        }
    }

    /// <summary>
    /// The level decides what the hero knows about: a marker hidden until seen has Visible = false until the fog over it
    /// lifts (Level.AddMarker), and every other marker is common knowledge from the start (roads out, the way to the boss).
    /// </summary>
    private static bool Shown(MapMarker m) => m != null && m.Visible && !QuestMarks.Covers(m);      // dungeon objectives: quest pins only

    private static string MarkerLabel(MapMarker m, MapMarkStyle st) => string.IsNullOrEmpty(m.Label) ? st.Name : m.Label;

    private void RefreshAreaInfo()
    {
        var g = Game.I; var d = g?.Hero?.D; var lv = _lv;
        if (lv == null)
        {
            _areaName.Text = "未知之地"; _areaSub.Text = ""; _areaInfo.Text = "";
            _objHead.Visible = _objText.Visible = _objBar.Visible = false;
            _regionInfo.Visible = false; _trackHead.Visible = _trackText.Visible = false;
            SizeLegend();
            return;
        }
        string areaId = lv.AreaId ?? "";
        _areaName.Text = ZoneDb.AreaName(areaId);
        if (lv.IsTown) _areaSub.Text = ZoneDb.TownSubtitle;
        else if (lv.IsRift) _areaSub.Text = $"第 {lv.RiftTier} 層";
        else if (lv.Dungeon != null) _areaSub.Text = (lv.Region != null ? lv.Region.Name + "・" : "") + (lv.Dungeon.Story ? "主線地城" : "地城");
        else _areaSub.Text = lv.Region?.Subtitle ?? "";

        var info = new StringBuilder(96);
        if (lv.IsTown) info.Append("燼火庇護之地，魔物不敢靠近。");
        else info.Append($"魔物等級 {lv.MonsterLevel}　·　世界階級：{WorldUiKit.TierName(g?.WorldTier ?? 0)}");
        if (_grid != null) info.Append($"\n已探索 {_explored:0}%");
        _areaInfo.Text = info.ToString();

        string obj = lv.ObjectiveText;
        if (string.IsNullOrEmpty(obj) && lv.Dungeon != null && lv.Dungeon.Goal != DungeonGoal.None) obj = ZoneDb.GoalText[(int)lv.Dungeon.Goal];
        bool hasObj = !string.IsNullOrEmpty(obj);
        // ObjectiveDone is already true while the boss still stands: the phase tells that step (Boss) from a finished
        // place (Cleared). The boss step names the next task in gold, without the done mark and without the met goal's bar.
        var phase = lv.Phase;
        bool bossNext = phase == Level.DungeonPhase.Boss;
        _objDone = phase == Level.DungeonPhase.Cleared;
        _objProgress = _objDone || bossNext ? 1f : Mathf.Clamp(lv.IsRift && lv.ObjectiveProgress <= 0f ? lv.RiftProgress : lv.ObjectiveProgress, 0f, 1f);
        _objHead.Visible = _objText.Visible = hasObj;
        _objBar.Visible = hasObj && !lv.IsTown && (lv.IsRift || !bossNext);
        if (hasObj)
        {
            _objText.Text = _objDone ? obj + "（已完成）" : obj;
            _objText.AddThemeColorOverride("font_color", _objDone ? Palette.Good : bossNext ? UiTheme.Gold : UiTheme.Text);
        }

        var r = lv.Region;
        _regionInfo.Visible = r != null && !lv.IsRift;
        if (r != null && !lv.IsRift)
        {
            var sb = new StringBuilder(256);
            string good = WorldUiKit.Hex(Palette.Good), dim = WorldUiKit.Hex(UiTheme.Dim), gold = WorldUiKit.Hex(UiTheme.Gold);
            sb.Append($"[color=#{gold}]{r.Name}[/color]\n");
            sb.Append(WorldUiKit.StrongholdFree(d, r) ? $"[color=#{good}]◆[/color] 據點「{r.Stronghold}」已解放\n" : $"[color=#{dim}]◇ 據點「{r.Stronghold}」尚未解放[/color]\n");
            if (r.Dungeons != null)
                foreach (var dg in r.Dungeons)
                    sb.Append(WorldUiKit.DungeonCleared(d, dg) ? $"[color=#{good}]◆[/color] {dg.Name}\n" : $"[color=#{dim}]◇ {dg.Name}[/color]\n");
            string boss = MonsterDb.Get(r.Boss)?.Name ?? "首領";
            sb.Append(WorldUiKit.BossSlain(d, r) ? $"[color=#{good}]◆[/color] {boss}已伏誅\n" : $"[color=#{WorldUiKit.Hex(Palette.Bad)}]◇ {boss}仍在等候[/color]\n");
            sb.Append($"名望 {WorldUiKit.RenownTier(r.Id)}/4　{WorldUiKit.RenownPoints(r.Id)} 點");
            _regionInfo.Text = sb.ToString();
        }

        IReadOnlyList<string> track = null;
        try { track = Quests.Tracker(); }
        catch (Exception e) { Log.Warn("map", "quest tracker unavailable: " + e.Message); }
        bool hasTrack = track != null && track.Count > 0;
        _trackHead.Visible = _trackText.Visible = hasTrack;
        if (hasTrack)
        {
            var sb = new StringBuilder(256);
            for (int i = 0; i < track.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(track[i]);
            }
            _trackText.Text = sb.ToString();
        }
        SizeLegend();
    }

    // ───────────────────────── area tab: view ─────────────────────────

    private Vector2 W2S(Vector2 world)
    {
        var d = world - _focus; var c = _areaCanvas.Size * 0.5f;
        return new Vector2(c.X + d.Dot(_right) * _scale, c.Y - d.Dot(_fwd) * _scale);
    }

    private Vector2 S2W(Vector2 screen)
    {
        var c = _areaCanvas.Size * 0.5f;
        return _focus + _right * ((screen.X - c.X) / _scale) - _fwd * ((screen.Y - c.Y) / _scale);
    }

    private void ClampFocus()
    {
        if (_grid == null) return;
        var lo = _grid.Origin; var hi = _grid.Origin + _grid.Size;
        _focus = new Vector2(Mathf.Clamp(_focus.X, lo.X, hi.X), Mathf.Clamp(_focus.Y, lo.Y, hi.Y));
    }

    private void FitArea()
    {
        _needFit = false;
        if (_grid == null) return;
        var sz = _areaCanvas.Size; var ws = _grid.Size;
        float bw = MathF.Abs(_right.X) * ws.X + MathF.Abs(_right.Y) * ws.Y, bh = MathF.Abs(_fwd.X) * ws.X + MathF.Abs(_fwd.Y) * ws.Y;
        _fitScale = MathF.Max(0.4f, MathF.Min(sz.X / MathF.Max(1f, bw), sz.Y / MathF.Max(1f, bh)) * 0.94f);
        float lo = _fitScale * 0.85f, hi = MathF.Max(MaxScale, lo);
        _scale = Mathf.Clamp(MathF.Max(_fitScale, 5.5f), lo, hi);
        if (_scale <= _fitScale * 1.05f || !TryHero(out var hero, out _)) _focus = _grid.Origin + ws * 0.5f;
        else _focus = hero;
        ClampFocus();
        RedrawArea();
    }

    private void CenterOnHero()
    {
        if (_grid == null) return;
        if (TryHero(out var hero, out _)) _focus = hero; else _focus = _grid.Origin + _grid.Size * 0.5f;
        ClampFocus();
        RedrawArea();
        WorldUiKit.Sfx("ui_click", 0.6f);
    }

    private void ZoomAt(Vector2 screen, float factor)
    {
        var before = S2W(screen);
        float lo = _fitScale * 0.85f, hi = MathF.Max(MaxScale, lo);
        _scale = Mathf.Clamp(_scale * factor, lo, hi);
        _focus += before - S2W(screen);           // the point under the cursor stays put
        ClampFocus();
        RedrawArea();
    }

    private void RedrawArea()
    {
        _areaCanvas.QueueRedraw();
        _areaOverlay.QueueRedraw();
    }

    private void OnAreaResized()
    {
        if (_open && _grid != null && !_needFit) RedrawArea();
    }

    private void OnAreaMouseExited()
    {
        _hoverQuest = null;
        if (_hoverMarker == null) return;
        _hoverMarker = null;
        _areaOverlay.QueueRedraw();
    }

    private void OnAreaInput(InputEvent e)
    {
        if (!_open || _grid == null) return;
        try
        {
            if (e is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right || mb.ButtonIndex == MouseButton.Middle)
                {
                    _dragging = mb.Pressed;
                    _areaCanvas.AcceptEvent();
                }
                else if (mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
                {
                    ZoomAt(mb.Position, mb.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1f / 1.15f);
                    _areaCanvas.AcceptEvent();
                }
            }
            else if (e is InputEventMouseMotion mm)
            {
                if (_dragging)
                {
                    _focus -= (_right * mm.Relative.X - _fwd * mm.Relative.Y) / _scale;
                    ClampFocus();
                    RedrawArea();
                }
                HoverMarker(mm.Position);
            }
        }
        catch (Exception ex) { DrawFail(ex); }
    }

    private void HoverMarker(Vector2 mouse)
    {
        if (HoverQuest(mouse)) { if (_hoverMarker != null) { _hoverMarker = null; _areaOverlay.QueueRedraw(); } return; }
        MapMarker best = null;
        float bd = 18f * 18f;
        var marks = _lv?.Markers;
        if (marks != null && !_dragging)
            for (int i = 0; i < marks.Count; i++)
            {
                var m = marks[i];
                if (!Shown(m)) continue;
                float d2 = W2S(m.Pos).DistanceSquaredTo(mouse);
                if (d2 < bd) { bd = d2; best = m; }
            }
        if (ReferenceEquals(best, _hoverMarker)) return;
        _hoverMarker = best;
        _areaOverlay.QueueRedraw();
    }

    // ───────────────────────── area tab: drawing ─────────────────────────

    private void DrawAreaBase()
    {
        try
        {
            var cv = _areaCanvas; var sz = cv.Size;
            LookArt();
            // the floor of the well: the unexplored dark the map is drawn on
            if (_pWellField != null) UiArt.Tile(cv, _pWellField, new Rect2(Vector2.Zero, sz));
            else cv.DrawRect(new Rect2(Vector2.Zero, sz), new Color(0.030f, 0.027f, 0.025f));
            if (_areaTex == null || _grid == null)
            {
                WorldUiKit.Text(cv, WorldUiKit.BodyFont, new Vector2(0, sz.Y * 0.5f), "此地尚無可供描繪的地圖。", 24, UiTheme.Dim, HorizontalAlignment.Center, sz.X);
                return;
            }
            float k = NavGrid.Cell / SS * _scale;
            cv.DrawSetTransformMatrix(new Transform2D(new Vector2(_right.X, -_fwd.X) * k, new Vector2(_right.Y, -_fwd.Y) * k, W2S(_grid.Origin)));
            cv.DrawTextureRect(_areaTex, new Rect2(0, 0, _texW, _texH), false);
            cv.DrawSetTransformMatrix(Transform2D.Identity);
        }
        catch (Exception e) { DrawFail(e); }
    }

    /// <summary>The map pane is an inset well: its forged rim is drawn last, over the map, the markers and their names.</summary>
    private void DrawAreaRim()
    {
        try
        {
            LookArt();
            var rim = _areaRim; var box = new Rect2(Vector2.Zero, rim.Size);
            if (_pWell != null) UiArt.Nine(rim, _pWell, box);
            else rim.DrawRect(box, new Color(UiTheme.Border, 0.7f), false, 1f);
        }
        catch (Exception e) { DrawFail(e); }
    }

    private void DrawAreaOverlay()
    {
        try
        {
            if (_grid == null || _lv == null) return;
            var ov = _areaOverlay; var sz = ov.Size; var font = WorldUiKit.SmallFont;
            var marks = _lv.Markers;
            if (marks == null) return;

            // Marker names are written in screen space at a fixed readable size (FsMark), whatever the zoom. A name of
            // that size does not always fit — markers crowd together when zoomed out — and then it is left out rather
            // than shrunk: the glyph, the legend and the name that appears on hovering still say what the marker is.
            bool names = _scale >= 3.2f;
            _labelQueue.Clear(); _glyphBoxes.Clear(); _labelBoxes.Clear();
            MapMarker hover = null;
            for (int i = 0; i < marks.Count; i++)
            {
                var m = marks[i];
                if (!Shown(m)) continue;
                if (ReferenceEquals(m, _hoverMarker)) { hover = m; continue; }
                var p = W2S(m.Pos);
                if (p.X < -40f || p.Y < -40f || p.X > sz.X + 40f || p.Y > sz.Y + 40f) continue;
                var st = MapGlyphs.Style(m.Kind);
                float r = 7.5f * st.Size;
                MapGlyphs.Draw(ov, st, p, r);
                if (!names) continue;
                _glyphBoxes.Add(new Rect2(p.X - r, p.Y - r, r * 2f, r * 2f));
                if (st.Major) _labelQueue.Add(new MarkLabel { M = m, St = st, P = p, R = r, I = i });
            }

            // the marker under the pointer: its name always shows, so it takes its place first
            MapMarkStyle hoverStyle = null;
            Vector2 hoverAt = default, hoverText = default;
            float hoverR = 0f;
            string hoverName = null;
            if (hover != null)
            {
                hoverStyle = MapGlyphs.Style(hover.Kind);
                hoverAt = W2S(hover.Pos);
                hoverR = 9.5f * hoverStyle.Size;
                hoverName = MarkerLabel(hover, hoverStyle);
                float lineH = WorldUiKit.LineHeight(font, FsMarkHover), w = WorldUiKit.Measure(font, hoverName, FsMarkHover).X;
                float x = MathF.Round(Mathf.Clamp(hoverAt.X - w * 0.5f, 14f, MathF.Max(14f, sz.X - w - 14f)));  // kept inside the rim of the map pane
                float top = hoverAt.Y + hoverR + 8f;                                                             // under the gold ring ...
                if (top + lineH > sz.Y - 12f) top = hoverAt.Y - hoverR - 8f - lineH;                             // ... or above it at the lower edge
                top = MathF.Round(top);
                hoverText = new Vector2(x, MathF.Round(top + WorldUiKit.Ascent(font, FsMarkHover)));
                _glyphBoxes.Add(new Rect2(hoverAt.X - hoverR - 9f, hoverAt.Y - hoverR - 9f, (hoverR + 9f) * 2f, (hoverR + 9f) * 2f));
                _labelBoxes.Add(new Rect2(x - 4f, top, w + 8f, lineH));
            }

            if (_labelQueue.Count > 0)
            {
                _labelQueue.Sort(LabelOrder);
                float lineH = WorldUiKit.LineHeight(font, FsMark), ascent = WorldUiKit.Ascent(font, FsMark);
                // the line box of a CJK face has air above and below the glyphs: the ink is what must not collide
                float airTop = MathF.Round(lineH * 0.14f), airBottom = MathF.Round(lineH * 0.08f), inkH = lineH - airTop - airBottom;
                var col = new Color(0.9f, 0.87f, 0.8f);
                for (int i = 0; i < _labelQueue.Count; i++)
                {
                    var q = _labelQueue[i];
                    string name = MarkerLabel(q.M, q.St);
                    float w = WorldUiKit.Measure(font, name, FsMark).X;
                    if (w <= 0f) continue;
                    float x = MathF.Round(q.P.X - w * 0.5f);
                    float inkTop = MathF.Round(q.P.Y + q.R + 4f);                           // under the glyph ...
                    var box = new Rect2(x - 3f, inkTop, w + 6f, inkH);
                    if (!LabelFits(box, q.St.Always))
                    {
                        inkTop = MathF.Round(q.P.Y - q.R - 4f - inkH);                      // ... or above it when that spot is taken
                        box = new Rect2(x - 3f, inkTop, w + 6f, inkH);
                        if (!LabelFits(box, q.St.Always)) continue;
                    }
                    _labelBoxes.Add(box);
                    WorldUiKit.Text(ov, font, new Vector2(x, inkTop - airTop + ascent), name, FsMark, col, HorizontalAlignment.Left, -1f, 4);
                }
            }

            if (hover != null)
            {
                // the marker under the pointer sits in a lit forged ring (a gold circle without the art)
                LookArt();
                float ringR = hoverR + 9f;
                if (_pNodeLit != null) UiArt.Sprite(ov, _pNodeLit, new Rect2(hoverAt.X - ringR, hoverAt.Y - ringR, ringR * 2f, ringR * 2f));
                else ov.DrawArc(hoverAt, hoverR + 6f, 0f, Mathf.Tau, 28, new Color(UiTheme.Gold, 0.9f), 1.6f, true);
                MapGlyphs.Draw(ov, hoverStyle, hoverAt, hoverR);
                WorldUiKit.Text(ov, font, hoverText, hoverName, FsMarkHover, UiTheme.Gold, HorizontalAlignment.Left, -1f, 5);
            }
        }
        catch (Exception e) { DrawFail(e); }
    }

    /// <summary>
    /// True when a marker name can be written into <paramref name="box"/>: clear of the names already placed and of the
    /// other glyphs. What the hero is after (<paramref name="always"/>) only yields to other names.
    /// </summary>
    private bool LabelFits(Rect2 box, bool always)
    {
        for (int i = 0; i < _labelBoxes.Count; i++) if (_labelBoxes[i].Intersects(box)) return false;
        if (always) return true;
        for (int i = 0; i < _glyphBoxes.Count; i++) if (_glyphBoxes[i].Intersects(box)) return false;
        return true;
    }

    private void DrawAreaFx()
    {
        try
        {
            if (_grid == null || _lv == null) return;
            var fx = _areaFx;
            float pulse = 0.5f + 0.5f * MathF.Sin(_t * 3.2f);
            var marks = _lv.Markers;
            if (marks != null)
                for (int i = 0; i < marks.Count; i++)
                {
                    var m = marks[i];
                    if (!Shown(m)) continue;
                    var st = MapGlyphs.Style(m.Kind);
                    if (st.Always) fx.DrawArc(W2S(m.Pos), 12f + 6f * pulse, 0f, Mathf.Tau, 24, new Color(st.Col, 0.6f * (1f - pulse * 0.7f)), 1.6f, true);
                }
            for (int i = 0; i < _goals.Count; i++)          // the way the main quest leads
            {
                var p = W2S(_goals[i].Pos);
                fx.DrawArc(p, 15f + 7f * pulse, 0f, Mathf.Tau, 28, new Color(1f, 0.83f, 0.35f, 0.85f * (1f - pulse * 0.75f)), 2.2f, true);
                fx.DrawArc(p, 13f, 0f, Mathf.Tau, 28, new Color(1f, 0.83f, 0.35f, 0.9f), 1.6f, true);
            }
            DrawQuestPins(fx, pulse);
            if (!TryHero(out var hero, out var forward)) return;
            var c = W2S(hero);
            var dir = new Vector2(forward.Dot(_right), -forward.Dot(_fwd));
            dir = dir.LengthSquared() < 1e-4f ? new Vector2(0, -1) : dir.Normalized();
            var side = new Vector2(-dir.Y, dir.X);
            fx.DrawArc(c, 14f + 8f * pulse, 0f, Mathf.Tau, 28, new Color(1f, 0.85f, 0.45f, 0.55f * (1f - pulse)), 2f, true);
            fx.DrawCircle(c, 10.5f, new Color(0.02f, 0.015f, 0.01f, 0.75f));
            Vector2 tip = c + dir * 10f, notch = c - dir * 3f;
            T3[0] = tip; T3[1] = c - dir * 7.5f + side * 7f; T3[2] = notch;
            fx.DrawColoredPolygon(T3, new Color(1f, 0.9f, 0.6f));
            T3[0] = tip; T3[1] = notch; T3[2] = c - dir * 7.5f - side * 7f;
            fx.DrawColoredPolygon(T3, new Color(0.95f, 0.68f, 0.3f));
        }
        catch (Exception e) { DrawFail(e); }
    }

    // ───────────────────────── area tab: quest pins ─────────────────────────

    private const float QuestPinR = 16.5f, QuestEdgeInset = 30f;
    /// <summary>Test hook (--suite=mapquest): hover the first quest pin shown, main quest first, as a pointer would.</summary>
    internal static bool TestHover;

    /// <summary>Which kinds of quest pin the legend lists (those shown in this area right now), main before side.</summary>
    private void BakeQuestLegend(Level lv)
    {
        _legendQuest.Clear();
        var list = QuestMarks.Of(lv);
        foreach (var m in list)
        {
            if (!m.Known) continue;
            int at = _legendQuest.FindIndex(e => e.kind == m.Kind);
            if (at < 0) _legendQuest.Add((m.Kind, m.Main));
            else if (m.Main && !_legendQuest[at].main) _legendQuest[at] = (m.Kind, true);
        }
        _legendQuest.Sort((a, b) => a.kind.CompareTo(b.kind));
        Log.Info("map", $"quest pins in {lv.AreaId}: {QuestMarks.Describe(lv)}");
    }

    private static bool SameTarget(HoverQ h, QuestMark m) => h.Ref != null ? ReferenceEquals(h.Ref, m.Ref) : h.Name == m.Name && h.Kind == m.Kind;

    /// <summary>Hovering a quest pin: true when one is under the pointer (it then takes the hover from the markers).</summary>
    private bool HoverQuest(Vector2 mouse)
    {
        HoverQ best = null;
        float bd = 24f * 24f;
        if (_lv != null && !_dragging)
            foreach (var m in QuestMarks.Of(_lv))
            {
                if (!m.Known) continue;
                var tip = W2S(m.Pos);
                if (!InView(tip, _areaFx.Size)) continue;
                float d2 = MathF.Min(QuestGlyph.Head(tip, QuestPinR).DistanceSquaredTo(mouse), tip.DistanceSquaredTo(mouse));
                if (d2 >= bd) continue;
                bd = d2;
                best = new HoverQ { Ref = m.Ref, Kind = m.Kind, Main = m.Main, Name = m.Name, Text = m.Text, Pos = m.Pos };
            }
        _hoverQuest = best;
        return best != null;
    }

    private bool InView(Vector2 tip, Vector2 sz) =>
        tip.X >= QuestEdgeInset && tip.X <= sz.X - QuestEdgeInset && QuestGlyph.Head(tip, QuestPinR).Y - QuestPinR >= QuestEdgeInset * 0.6f && tip.Y <= sz.Y - QuestEdgeInset;

    private struct MapEdge { public QuestMark M; public Vector2 At, Dir; public int Count; }
    private readonly List<MapEdge> _edgeAt = new();
    private const float QuestEdgeR = 13.5f;

    /// <summary>
    /// The quest pins, every frame so moving targets are followed: side targets first, the main quest on top. A target
    /// outside the pane sits on its edge with an arrow toward it (one per spot, the main quest's first, with a "×n"
    /// badge when several share it; the main quest's pulses). The pin under the pointer gets a ring and its objective.
    /// </summary>
    private void DrawQuestPins(Control fx, float pulse)
    {
        var list = QuestMarks.Of(_lv);
        if (list.Count == 0) { _hoverQuest = null; return; }
        var sz = fx.Size;
        var mid = sz * 0.5f;
        if (TestHover && _hoverQuest == null)
            for (int pass = 1; pass >= 0 && _hoverQuest == null; pass--)
                foreach (var m in list)
                    if (m.Known && m.Main == (pass == 1) && InView(W2S(m.Pos), sz)) { _hoverQuest = new HoverQ { Ref = m.Ref, Kind = m.Kind, Main = m.Main, Name = m.Name, Text = m.Text, Pos = m.Pos }; break; }

        // off the pane: on its edge (kept inside by the pin's own reach), main quest first, one per spot
        _edgeAt.Clear();
        float inset = MathF.Max(QuestEdgeInset, QuestGlyph.EdgeReach(QuestEdgeR) + 20f);     // clear of the forged rim of the pane
        float hx = MathF.Max(1f, mid.X - inset), hy = MathF.Max(1f, mid.Y - inset);
        for (int pass = 1; pass >= 0; pass--)
            foreach (var m in list)
            {
                if (!m.Known || m.Main != (pass == 1)) continue;
                var tip = W2S(m.Pos);
                if (InView(tip, sz)) continue;
                var dir = tip - mid;
                if (dir.LengthSquared() < 1f) continue;
                float t = MathF.Min(hx / MathF.Max(1e-3f, MathF.Abs(dir.X)), hy / MathF.Max(1e-3f, MathF.Abs(dir.Y)));
                var at = mid + dir * MathF.Min(1f, t);
                int near = -1;
                for (int j = 0; j < _edgeAt.Count; j++) if (_edgeAt[j].At.DistanceSquaredTo(at) < 46f * 46f) { near = j; break; }
                if (near >= 0) { var e = _edgeAt[near]; e.Count++; _edgeAt[near] = e; continue; }
                _edgeAt.Add(new MapEdge { M = m, At = at, Dir = dir, Count = 1 });
            }
        for (int j = _edgeAt.Count - 1; j >= 0; j--)
        {
            var e = _edgeAt[j];
            QuestGlyph.Edge(fx, e.M.Kind, QuestMarks.ColorOf(e.M), e.At, e.Dir, QuestEdgeR, 0.95f, e.M.Main ? pulse : -1f, e.Count);
        }

        bool hoverAlive = false;
        for (int pass = 0; pass < 2; pass++)
            foreach (var m in list)
            {
                if (!m.Known || m.Main != (pass == 1)) continue;
                var tip = W2S(m.Pos);
                if (!InView(tip, sz)) continue;
                var col = QuestMarks.ColorOf(m);
                if (_hoverQuest != null && SameTarget(_hoverQuest, m)) { hoverAlive = true; _hoverQuest.Pos = m.Pos; _hoverQuest.Text = m.Text; }
                fx.DrawArc(tip, 7f + 7f * pulse, 0f, Mathf.Tau, 24, new Color(col, (m.Main ? 0.75f : 0.5f) * (1f - pulse * 0.7f)), 1.8f, true);
                QuestGlyph.Pin(fx, m.Kind, col, tip, QuestPinR);
            }
        if (_hoverQuest == null) return;
        if (!hoverAlive) { _hoverQuest = null; return; }

        // the pin under the pointer: a lit ring, the target's name and the objective, on a dark plate
        var h = _hoverQuest;
        var font = WorldUiKit.SmallFont;
        var hcol = h.Main ? QuestMarks.MainCol : QuestMarks.SideCol;
        var htip = W2S(h.Pos);
        var hhead = QuestGlyph.Head(htip, QuestPinR);
        fx.DrawArc(hhead, QuestPinR + 5f, 0f, Mathf.Tau, 28, new Color(UiTheme.Gold, 0.95f), 2f, true);
        QuestGlyph.Pin(fx, h.Kind, hcol, htip, QuestPinR);
        string l1 = h.Name, l2 = string.IsNullOrEmpty(h.Text) ? QuestMarks.KindName(h.Kind) : (h.Main ? "主線　" : "支線　") + h.Text;
        float lh1 = WorldUiKit.LineHeight(font, FsMarkHover), lh2 = WorldUiKit.LineHeight(font, FsMark);
        float w = MathF.Max(WorldUiKit.Measure(font, l1, FsMarkHover).X, WorldUiKit.Measure(font, l2, FsMark).X) + 20f;
        float hgt = lh1 + lh2 + 10f;
        float x = MathF.Round(Mathf.Clamp(htip.X - w * 0.5f, 14f, MathF.Max(14f, sz.X - w - 14f)));
        float top = htip.Y + 8f;
        if (top + hgt > sz.Y - 12f) top = hhead.Y - QuestPinR - 10f - hgt;
        top = MathF.Round(top);
        var plate = new Rect2(x, top, w, hgt);
        fx.DrawRect(plate, new Color(0.035f, 0.028f, 0.022f, 0.92f));
        fx.DrawRect(plate, new Color(hcol, 0.8f), false, 1.4f);
        WorldUiKit.Text(fx, font, new Vector2(x + 10f, MathF.Round(top + 4f + WorldUiKit.Ascent(font, FsMarkHover))), l1, FsMarkHover, hcol, HorizontalAlignment.Left, -1f, 4);
        WorldUiKit.Text(fx, font, new Vector2(x + 10f, MathF.Round(top + 4f + lh1 + WorldUiKit.Ascent(font, FsMark))), l2, FsMark, UiTheme.Text, HorizontalAlignment.Left, -1f, 4);
    }

    private void DrawObjectiveBar()
    {
        try
        {
            var sz = _objBar.Size;
            LookArt();
            Color fill = _objDone ? Palette.Good : Palette.Ember;
            if (_pTrack != null)
            {
                // a forged trough with the progress laid into it (the fill keeps its colour: ember, green when done)
                float h = MathF.Max(8f, MathF.Round(sz.Y * 0.5f)), y = 3f;
                UiArt.Nine(_objBar, _pTrack, new Rect2(0f, y, sz.X, h));
                if (_objProgress > 0.001f)
                {
                    float w = MathF.Max(h, sz.X * _objProgress);
                    if (_pFill != null) UiArt.Nine(_objBar, _pFill, new Rect2(0f, y, w, h), fill.Lightened(0.15f));
                    else _objBar.DrawRect(new Rect2(3f, y + 3f, w - 6f, h - 6f), fill);
                }
                return;
            }
            float bh = MathF.Round(sz.Y * 0.58f);       // the flat bar: 14 px at 標準, the rest of the box is air under it
            _objBar.DrawRect(new Rect2(0, 0, sz.X, bh), new Color(0.05f, 0.045f, 0.04f));
            if (_objProgress > 0.001f) _objBar.DrawRect(new Rect2(1, 1, (sz.X - 2) * _objProgress, bh - 2), fill);
            _objBar.DrawRect(new Rect2(0, 0, sz.X, bh), new Color(UiTheme.Border, 0.9f), false, 1f);
        }
        catch (Exception e) { DrawFail(e); }
    }

    private void DrawLegend()
    {
        try
        {
            var cv = _legend; var font = WorldUiKit.SmallFont;
            // rows step by the measured line height (SizeLegend sizes the box with the same number); each caption is
            // centred on its glyph, and cut with an ellipsis should the column ever be too narrow for it
            float row = LegendRow, lineH = WorldUiKit.LineHeight(font, FsLegend), ascent = WorldUiKit.Ascent(font, FsLegend);
            const float tx = 36f;
            float tw = MathF.Max(40f, cv.Size.X - tx - 2f);
            float y = MathF.Round(row * 0.5f);
            float Baseline(float centre) => MathF.Round(centre - lineH * 0.5f + ascent);
            // the hero
            T3[0] = new Vector2(14, y - 8); T3[1] = new Vector2(21, y + 7); T3[2] = new Vector2(7, y + 7);
            cv.DrawCircle(new Vector2(14, y), 11f, new Color(0.02f, 0.015f, 0.01f, 0.8f));
            cv.DrawColoredPolygon(T3, new Color(1f, 0.85f, 0.5f));
            WorldUiKit.Text(cv, font, new Vector2(tx, Baseline(y)), "你的位置", FsLegend, UiTheme.Text, HorizontalAlignment.Left, tw);
            y += row;
            if (_goals.Count > 0)
            {
                cv.DrawArc(new Vector2(14, y), 9f, 0f, Mathf.Tau, 20, new Color(1f, 0.83f, 0.35f), 1.8f, true);
                cv.DrawArc(new Vector2(14, y), 5f, 0f, Mathf.Tau, 16, new Color(1f, 0.83f, 0.35f, 0.5f), 1.4f, true);
                WorldUiKit.Text(cv, font, new Vector2(tx, Baseline(y)), "主線的去向", FsLegend, UiTheme.Gold, HorizontalAlignment.Left, tw);
                y += row;
            }
            if (_legendQuest.Count > 0)
            {
                // the quest pins: one row per kind of target present, then the key to their two colours
                foreach (var (kind, main) in _legendQuest)
                {
                    QuestGlyph.Pin(cv, kind, main ? QuestMarks.MainCol : QuestMarks.SideCol, new Vector2(14, y + 9f), 7.5f);
                    WorldUiKit.Text(cv, font, new Vector2(tx, Baseline(y)), QuestMarks.KindName(kind), FsLegend, main ? UiTheme.Gold : UiTheme.Text, HorizontalAlignment.Left, tw);
                    y += row;
                }
                cv.DrawCircle(new Vector2(9, y), 5f, QuestMarks.MainCol);
                cv.DrawCircle(new Vector2(20, y), 5f, QuestMarks.SideCol);
                WorldUiKit.Text(cv, font, new Vector2(tx, Baseline(y)), "金色：主線　銀藍：支線", FsLegend, UiTheme.Dim, HorizontalAlignment.Left, tw);
                y += row;
            }
            foreach (var st in _legendStyles)
            {
                MapGlyphs.Draw(cv, st, new Vector2(14, y), 7.5f);
                WorldUiKit.Text(cv, font, new Vector2(tx, Baseline(y)), st.Name, FsLegend, UiTheme.Text, HorizontalAlignment.Left, tw);
                y += row;
            }
            // the fog: a swatch of the well's own floor in a fine line frame
            LookArt();
            var fog = new Rect2(5, y - 8, 18, 16);
            if (_pWellField != null) UiArt.Tile(cv, _pWellField, fog); else cv.DrawRect(fog, new Color(0.03f, 0.027f, 0.025f));
            if (_pLine != null) UiArt.Nine(cv, _pLine, fog, new Color(UiTheme.Dim, 0.75f));
            else cv.DrawRect(fog, new Color(UiTheme.Dim, 0.6f), false, 1f);
            WorldUiKit.Text(cv, font, new Vector2(tx, Baseline(y)), "灰霧：尚未踏足之處", FsLegend, UiTheme.Dim, HorizontalAlignment.Left, tw);
        }
        catch (Exception e) { DrawFail(e); }
    }

    // ───────────────────────── world tab: data ─────────────────────────

    private void RefreshWorld()
    {
        _nodes.Clear();
        var g = Game.I; var d = g?.Hero?.D;
        string cur = CurrentLevel()?.AreaId;
        string curSite = cur == null ? null : cur == ZoneDb.Town ? ZoneDb.Town : ZoneDb.RegionOf(cur)?.Id;
        string quest = WorldUiKit.NextObjectiveArea();
        string questSite = string.IsNullOrEmpty(quest) ? null : quest == ZoneDb.Town ? ZoneDb.Town : ZoneDb.RegionOf(quest)?.Id;
        foreach (var s in WorldMapArt.Sites)
        {
            var n = new NodeView { Site = s, Id = s.Id };
            if (s.Id == ZoneDb.Town)
            {
                n.Name = ZoneDb.TownName; n.Line2 = ZoneDb.TownSubtitle; n.Accent = new Color(1f, 0.72f, 0.3f); n.Waypoint = true;
            }
            else
            {
                var r = ZoneDb.Region(s.Id);
                if (r == null) continue;
                n.R = r; n.Name = r.Name; n.Accent = r.Accent;
                n.Locked = !WorldUiKit.CanEnter(r.Id);
                n.Line2 = $"{WorldUiKit.ActLabel(r)}　建議等級 {r.Level}";
                n.Stronghold = WorldUiKit.StrongholdFree(d, r);
                if (r.Dungeons != null)
                    for (int i = 0; i < r.Dungeons.Length && i < 3; i++)
                    {
                        n.DungeonDone[i] = WorldUiKit.DungeonCleared(d, r.Dungeons[i]);
                        if (n.DungeonDone[i]) n.Dungeons++;
                    }
                n.Boss = WorldUiKit.BossSlain(d, r);
                n.RenownTier = WorldUiKit.RenownTier(r.Id);
                n.RenownPts = WorldUiKit.RenownPoints(r.Id);
                n.Line3 = $"名望 {n.RenownTier}/4　{n.RenownPts} 點";
                n.Waypoint = d != null && d.Waypoints != null && d.Waypoints.Contains(r.Id);
            }
            n.Current = curSite == s.Id;
            n.Quest = questSite == s.Id;
            n.Travel = TravelRule(n, cur, d, out n.Entry, out n.Reason);
            _nodes.Add(n);
        }
        _hoverNode = null;
        WorldUiKit.TipHide();
    }

    /// <summary>
    /// Travel is offered to the town, to any region whose 燼塔 is lit and, when setting out from the town itself, to any
    /// region the story lets the hero enter (arriving on foot from the town side).
    /// </summary>
    private bool TravelRule(NodeView n, string currentArea, SaveData d, out string entry, out string reason)
    {
        entry = "waypoint"; reason = null;
        if (!_travel || d == null) return false;
        if (n.Id == currentArea) { reason = "你已身在此地。"; return false; }
        if (n.Id == ZoneDb.Town || n.Waypoint) return true;
        if (n.Locked)
        {
            string why = WorldUiKit.WhyLocked(n.Id);
            reason = string.IsNullOrEmpty(why) ? "通往此地的道路仍被灰暮封鎖。" : why + "。";
            return false;
        }
        if (_gate) { entry = ZoneDb.Town; return true; }
        reason = "此地的燼塔尚未點燃，無法傳送。";
        return false;
    }

    private NodeView NodeById(string id)
    {
        foreach (var n in _nodes) if (n.Id == id) return n;
        return null;
    }

    private string NodeTooltip(NodeView n)
    {
        var d = Game.I?.Hero?.D;
        string good = WorldUiKit.Hex(Palette.Good), bad = WorldUiKit.Hex(Palette.Bad), dim = WorldUiKit.Hex(UiTheme.Dim), gold = WorldUiKit.Hex(UiTheme.Gold);
        var sb = new StringBuilder(640);
        // the place name: the scaled size is written into the tag here, once (the tooltip shows the BBCode as it is)
        sb.Append($"[font_size={UiTheme.Fs(FsPlace)}][color=#{WorldUiKit.Hex(n.Locked ? UiTheme.Dim : n.Accent)}]{n.Name}[/color][/font_size]\n");
        if (n.R == null)
        {
            sb.Append($"[color=#{dim}]{ZoneDb.TownSubtitle}[/color]\n\n");
            sb.Append("守燈長老、鐵匠與行商都在大火盆旁等你。\n燼火不滅，這裡永遠是歸處。");
        }
        else
        {
            var r = n.R;
            sb.Append($"[color=#{dim}]{r.Subtitle}[/color]\n");
            sb.Append($"{WorldUiKit.ActLabel(r)}　·　建議等級 {r.Level}\n");
            if (n.Locked)
            {
                string why = WorldUiKit.WhyLocked(r.Id);
                sb.Append($"[color=#{bad}]尚未開放——{(string.IsNullOrEmpty(why) ? "主線推進之後，道路才會顯現" : why)}。[/color]\n");
            }
            sb.Append('\n');
            sb.Append(n.Stronghold ? $"[color=#{good}]◆[/color] 據點「{r.Stronghold}」已解放\n" : $"[color=#{dim}]◇[/color] 據點「{r.Stronghold}」尚未解放\n");
            sb.Append($"地城　已淨空 {n.Dungeons}/{(r.Dungeons?.Length ?? 0)}\n");
            if (r.Dungeons != null)
                foreach (var dg in r.Dungeons)
                {
                    bool done = WorldUiKit.DungeonCleared(d, dg);
                    sb.Append(done ? $"　[color=#{good}]◆[/color] {dg.Name}" : $"　[color=#{dim}]◇ {dg.Name}[/color]");
                    if (dg.Story) sb.Append($"[color=#{dim}]（主線）[/color]");
                    sb.Append('\n');
                }
            string boss = MonsterDb.Get(r.Boss)?.Name ?? "首領";
            sb.Append(n.Boss ? $"[color=#{good}]◆[/color] 首領　{boss}　已伏誅\n" : $"[color=#{bad}]◇[/color] 首領　{boss}\n");
            sb.Append($"名望　第 {n.RenownTier}/4 階　·　{n.RenownPts} 點\n");
            sb.Append(n.Waypoint ? $"[color=#{good}]◆[/color] 燼塔已點燃，可傳送" : $"[color=#{dim}]◇ 燼塔尚未點燃[/color]");
        }
        if (n.Quest) sb.Append($"\n\n[color=#{gold}]◆ 主線的下一步在這裡[/color]");
        if (n.Current) sb.Append($"\n[color=#{gold}]▼ 你目前身在此地[/color]");
        if (_travel)
        {
            if (n.Travel) sb.Append(n.Entry == ZoneDb.Town
                ? $"\n\n[color=#{gold}]點擊左鍵，自殘燈鎮徒步前往[/color]\n[color=#{dim}]（此地的燼塔尚未重燃）[/color]"
                : n.R == null ? $"\n\n[color=#{gold}]點擊左鍵，返回殘燈鎮[/color]" : $"\n\n[color=#{gold}]點擊左鍵，傳送至此地的燼塔[/color]");
            else if (!string.IsNullOrEmpty(n.Reason)) sb.Append($"\n\n[color=#{bad}]{n.Reason}[/color]");
        }
        return sb.ToString();
    }

    private void Travel(NodeView n)
    {
        var g = Game.I;
        if (g == null || n == null) return;
        string from = g.Level?.AreaId ?? "-";
        Log.Info("map", $"travel {from} -> {n.Id} entry={n.Entry}");
        WorldUiKit.Sfx("portal_open");
        string id = n.Id, entry = n.Entry;
        WorldUiKit.ClosePanel(Id);
        Log.Guard("map", () => g.EnterArea(id, entry));
    }

    // ───────────────────────── world tab: view ─────────────────────────

    private void LayoutWorld()
    {
        var sz = _worldCanvas.Size;
        _wFit = MathF.Min(sz.X / WorldMapArt.DW, sz.Y / WorldMapArt.DH);
        _wOrg = (sz - new Vector2(WorldMapArt.DW, WorldMapArt.DH) * _wFit) * 0.5f;
    }

    private Vector2 WP(Vector2 design) => _wOrg + design * _wFit;

    private static float NodeRadius(NodeView n) => n.R == null ? 35f : 30f;

    /// <summary>
    /// Lays out the label block of a node — name, the act / level line, the row of pips and the renown line — under
    /// its medallion (centre <paramref name="c"/>, radius <paramref name="r"/>, both in canvas pixels), or above it where
    /// a road arrives from the south. The text keeps its design size whatever the fit of the map (<paramref name="f"/>
    /// only places the block and sizes the pips), so every step comes from the font metrics at the current text scale.
    /// </summary>
    private NodeLabels LabelsOf(NodeView n, Vector2 c, float r, float f)
    {
        bool town = n.R == null;
        Font title = WorldUiKit.TitleFont, small = WorldUiKit.SmallFont;
        int fsName = town ? FsNodeTown : FsNode;
        if (!n.Measured)
        {
            n.NameW = WorldUiKit.Measure(title, n.Name, fsName).X;
            n.Line2W = WorldUiKit.Measure(small, n.Line2, FsNodeLine).X;
            n.Line3W = WorldUiKit.Measure(small, n.Line3, FsNodeLine).X;
            n.Measured = true;
        }
        float nameH = WorldUiKit.LineHeight(title, fsName), lineH = WorldUiKit.LineHeight(small, FsNodeLine);
        float lineAscent = WorldUiKit.Ascent(small, FsNodeLine);
        float pipHalf = 8.5f * f;                                       // a pip and its dark disc
        // With the art the name stands on a forged plate (pointed ends, a stud at each): as tall as the name needs
        // between its inlay lines, as wide as the name plus the two ends. The small lines follow under the plate.
        LookArt();
        bool plate = _pPlate != null;
        float plateH = plate ? MathF.Round(UiTheme.Fs(fsName) * 1.5f) + 2f : 0f;
        float plateHalfW = plate ? MathF.Round(n.NameW * 0.5f) + PlateEnd : 0f;
        float nameTop = plate ? MathF.Round((plateH - nameH) * 0.5f) - 1f : 0f;        // the name, centred between the inlay lines
        // (the line boxes of both faces have air to spare above and below the glyphs: the lines are stepped by a
        // little less than their boxes, the plate taking the room the name's own air used to give)
        float top2 = plate ? plateH - MathF.Round(lineH * 0.1f) : nameH - MathF.Round(nameH * 0.1f);
        float pips = top2 + (plate ? MathF.Round(lineH * 0.92f) : lineH) + pipHalf;
        float top3 = pips + pipHalf - MathF.Round(lineH * (plate ? 0.14f : 0.1f));
        float height = town ? top2 + lineH : top3 + lineH;
        // clear of the medallion: its ring, and round a region the four arcs of its renown
        float gap = plate ? (town ? 5f : 11f) : 4f, gapAbove = plate ? 10f : 9f;
        float top = n.Site.LabelAbove ? c.Y - r - gapAbove * f - height : c.Y + r + gap * f;
        if (plate && f > 0.01f)
        {
            // The plate makes the block a little taller than the bare name did. Where the largest text size would
            // push it under the rim of the chart, it closes up on its medallion instead (never over the glyph).
            float air = MathF.Round(lineH * 0.16f);
            float sheetTop = _wOrg.Y + 7f, sheetBottom = _wOrg.Y + WorldMapArt.DH * f - 7f;
            if (n.Site.LabelAbove) { if (top < sheetTop) top = MathF.Min(sheetTop, c.Y - r - 3f * f - height + air); }
            else if (top + height - air > sheetBottom) top = MathF.Max(sheetBottom - height + air, c.Y + r);
            top = MathF.Round(top);
        }
        float widest = MathF.Max(MathF.Max(MathF.Max(n.NameW, plateHalfW * 2f - 12f), n.Line2W), town ? 0f : MathF.Max(n.Line3W, 93f * f));
        return new NodeLabels
        {
            Top = top, Bottom = top + height, HalfW = widest * 0.5f + 6f,
            NameBase = MathF.Round(top + nameTop + WorldUiKit.Ascent(title, fsName)),
            Line2Base = MathF.Round(top + top2 + lineAscent),
            PipsY = MathF.Round(top + pips),
            Line3Base = MathF.Round(top + top3 + lineAscent),
            PlateH = plateH, PlateHalfW = plateHalfW,
        };
    }

    /// <summary>What a name plate adds to each side of its name: the pointed end with its stud, and a little air.</summary>
    private const float PlateEnd = 31f;

    private void OnWorldResized()
    {
        LayoutWorld();
        _worldCanvas.QueueRedraw(); _worldNodes.QueueRedraw(); _worldRim.QueueRedraw();
    }

    private void OnWorldMouseExited()
    {
        if (_hoverNode == null) return;
        _hoverNode = null;
        WorldUiKit.TipHide();
        _worldCanvas.MouseDefaultCursorShape = CursorShape.Arrow;
        _worldNodes.QueueRedraw();
    }

    private NodeView NodeAt(Vector2 mouse)
    {
        float f = _wFit;
        if (f <= 0.01f) return null;
        foreach (var n in _nodes)
        {
            var c = WP(n.Site.P);
            float r = NodeRadius(n) * f;
            if (c.DistanceSquaredTo(mouse) <= (r + 10f * f) * (r + 10f * f)) return n;
            // the label block counts too: as large as it is written (it grows with the text scale)
            var lb = LabelsOf(n, c, r, f);
            float top = n.Site.LabelAbove ? lb.Top : c.Y + r, bottom = n.Site.LabelAbove ? c.Y - r : lb.Bottom;
            if (MathF.Abs(mouse.X - c.X) < lb.HalfW && mouse.Y > top && mouse.Y < bottom) return n;
        }
        return null;
    }

    private void OnWorldInput(InputEvent e)
    {
        if (!_open) return;
        try
        {
            if (e is InputEventMouseMotion mm)
            {
                var n = NodeAt(mm.Position);
                if (ReferenceEquals(n, _hoverNode)) return;
                _hoverNode = n;
                _worldNodes.QueueRedraw();
                if (n == null)
                {
                    WorldUiKit.TipHide();
                    _worldCanvas.MouseDefaultCursorShape = CursorShape.Arrow;
                    return;
                }
                _worldCanvas.MouseDefaultCursorShape = n.Travel ? CursorShape.PointingHand : CursorShape.Arrow;
                float r = NodeRadius(n) * _wFit;
                WorldUiKit.Tip(NodeTooltip(n), _worldCanvas.GlobalPosition + WP(n.Site.P) + new Vector2(r + 22f, -r));
                WorldUiKit.Sfx("ui_hover", 0.5f);
            }
            else if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                var n = NodeAt(mb.Position);
                _worldCanvas.AcceptEvent();
                if (n == null || !_travel) return;
                if (n.Travel) Travel(n);
                else Refuse(n.Reason ?? "無法前往該處。");
            }
        }
        catch (Exception ex) { DrawFail(ex); }
    }

    // ───────────────────────── world tab: drawing ─────────────────────────

    private static readonly Color InkCol = new(0.075f, 0.058f, 0.045f, 0.78f);

    private void DrawWorldBase()
    {
        try
        {
            LayoutWorld();
            var cv = _worldCanvas; var sz = cv.Size; float f = _wFit;
            LookArt();
            // With the art the chart lies in a well of its own size (DrawWorldRim) and the window's field shows beside
            // it; without it the whole pane is the dark table the chart lies on.
            if (_pWell == null) cv.DrawRect(new Rect2(Vector2.Zero, sz), new Color(0.028f, 0.025f, 0.023f));
            if (f <= 0.01f) return;
            var rect = new Rect2(_wOrg, new Vector2(WorldMapArt.DW, WorldMapArt.DH) * f);
            var tex = WorldMapArt.Texture;
            if (tex != null) cv.DrawTextureRect(tex, rect, false);
            else cv.DrawRect(rect, new Color(0.115f, 0.10f, 0.085f));      // bare vellum for the moment the painting takes

            foreach (var d in WorldMapArt.Decor) DrawDeco(cv, d.Kind, WP(d.P), d.S * f);

            // Lettering on the map is written in screen space at fixed design sizes: the fit of the map (f) only moves it.
            // Names of the dead seas, wherever the painted coast leaves room; each is centred on its spot, so a larger
            // text scale grows it evenly to both sides.
            var title = WorldUiKit.TitleFont;
            var seaCol = new Color(0.46f, 0.45f, 0.5f, 0.5f);
            if (!WorldMapArt.IsLand(new Vector2(230, 750)) && !WorldMapArt.IsLand(new Vector2(300, 750)))
                SeaName(cv, title, new Vector2(253, 760), "灰　海", seaCol);
            // (this one stood at y 572, squeezed above the old legend box; with the box gone it has moved down into
            // the open water, clear of the label block of 鏽砂廢都 at every text scale)
            if (!WorldMapArt.IsLand(new Vector2(1490, 640)) && !WorldMapArt.IsLand(new Vector2(1550, 640)))
                SeaName(cv, title, new Vector2(1514, 652), "無燈之海", seaCol);
            if (!WorldMapArt.IsLand(new Vector2(700, 60)) && !WorldMapArt.IsLand(new Vector2(780, 60)))
                SeaName(cv, title, new Vector2(719, 70), "寂　洋", seaCol);

            // cartouche: the name of the land, its subtitle and a rule, stacked from the measured lines
            var body = WorldUiKit.BodyFont;
            const string land = "瑟蘭", landSub = "灰暮之下的殘土";
            var corner = WP(new Vector2(54, 30));
            float landH = WorldUiKit.LineHeight(title, FsLand), subH = WorldUiKit.LineHeight(body, FsLandSub), subAscent = WorldUiKit.Ascent(body, FsLandSub);
            float landBase = MathF.Round(corner.Y + WorldUiKit.Ascent(title, FsLand));
            float subBase = MathF.Round(corner.Y + landH - MathF.Round(landH * 0.14f) + subAscent);
            float ruleY = MathF.Round(subBase + (subH - subAscent) + 4f);
            float ruleW = MathF.Max(194f * f, MathF.Max(WorldUiKit.Measure(title, land, FsLand).X, WorldUiKit.Measure(body, landSub, FsLandSub).X + 4f) + 12f);
            WorldUiKit.Text(cv, title, new Vector2(corner.X, landBase), land, FsLand, new Color(0.90f, 0.76f, 0.48f, 0.95f), HorizontalAlignment.Left, -1f, 6);
            WorldUiKit.Text(cv, body, new Vector2(corner.X + 4f, subBase), landSub, FsLandSub, new Color(0.72f, 0.66f, 0.55f, 0.9f), HorizontalAlignment.Left, -1f, 4);
            if (_pHeadLine != null) UiArt.Nine(cv, _pHeadLine, new Rect2(corner.X - 2f, ruleY - 2f, ruleW + 8f, _pHeadLine.BoxSize.Y));
            else cv.DrawLine(new Vector2(corner.X + 2f, ruleY), new Vector2(corner.X + 2f + ruleW, ruleY), new Color(0.80f, 0.64f, 0.36f, 0.7f), 1.5f * f, true);

            DrawCompass(cv, WP(new Vector2(132, 690)), 40f * f, f);

            // the neat line of the chart: a fine bevelled line just inside the rim (two flat lines without the art)
            var edge = new Color(0.58f, 0.45f, 0.25f, 0.9f);
            if (_pWell != null && _pLine != null) UiArt.Nine(cv, _pLine, rect.Grow(-MathF.Round(9f * f)), new Color(edge, 0.5f));
            else
            {
                cv.DrawRect(rect, edge, false, 2f);
                cv.DrawRect(rect.Grow(-7f * f), new Color(edge, 0.35f), false, 1f);
            }
        }
        catch (Exception e) { DrawFail(e); }
    }

    /// <summary>The chart lies in an inset well of its own size: the forged rim is drawn last, over the chart and its nodes.</summary>
    private void DrawWorldRim()
    {
        try
        {
            LookArt();
            LayoutWorld();
            if (_pWell == null || _wFit <= 0.01f) return;
            var rect = new Rect2(_wOrg, new Vector2(WorldMapArt.DW, WorldMapArt.DH) * _wFit);
            UiArt.Nine(_worldRim, _pWell, new Rect2(MathF.Round(rect.Position.X), MathF.Round(rect.Position.Y), MathF.Round(rect.Size.X), MathF.Round(rect.Size.Y)));
        }
        catch (Exception e) { DrawFail(e); }
    }

    /// <summary>Writes the name of a sea centred on a spot of the map (design coordinates; the Y is the baseline).</summary>
    private void SeaName(CanvasItem cv, Font font, Vector2 design, string text, Color col)
    {
        var p = WP(design);
        float w = WorldUiKit.Measure(font, text, FsSea).X;
        WorldUiKit.Text(cv, font, new Vector2(MathF.Round(p.X - w * 0.5f), MathF.Round(p.Y)), text, FsSea, col);
    }

    private static void DrawCompass(CanvasItem cv, Vector2 c, float r, float f)
    {
        var line = new Color(0.72f, 0.60f, 0.38f, 0.75f);
        cv.DrawArc(c, r, 0f, Mathf.Tau, 40, line, 1.4f * f, true);
        cv.DrawArc(c, r * 0.72f, 0f, Mathf.Tau, 32, new Color(line, 0.4f), 1f * f, true);
        for (int i = 0; i < 8; i++)
        {
            float ang = i * Mathf.Pi * 0.25f - Mathf.Pi * 0.5f;
            var dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
            var side = new Vector2(-dir.Y, dir.X);
            float len = (i % 2 == 0 ? 1.18f : 0.62f) * r, half = (i % 2 == 0 ? 0.13f : 0.09f) * r;
            T3[0] = c + dir * len; T3[1] = c + side * half; T3[2] = c - side * half;
            cv.DrawColoredPolygon(T3, i == 0 ? new Color(0.95f, 0.72f, 0.36f, 0.95f) : new Color(0.62f, 0.52f, 0.36f, 0.8f));
        }
        cv.DrawCircle(c, r * 0.1f, new Color(0.08f, 0.06f, 0.05f));
        // 北 stands on the north needle: centred from its measured width, its baseline just above the tip
        var font = WorldUiKit.TitleFont;
        float w = WorldUiKit.Measure(font, "北", FsCompass).X;
        WorldUiKit.Text(cv, font, new Vector2(MathF.Round(c.X - w * 0.5f), MathF.Round(c.Y - r * 1.3f)), "北", FsCompass, new Color(0.9f, 0.76f, 0.5f, 0.9f), HorizontalAlignment.Left, -1f, 4);
    }

    private static void DrawDeco(CanvasItem cv, byte kind, Vector2 p, float s)
    {
        var ink = InkCol;
        float w = MathF.Max(1.2f, 1.5f * s);
        switch (kind)
        {
            case WorldMapArt.DeadTree:
                cv.DrawLine(p, p + new Vector2(0, -15f * s), ink, w, true);
                cv.DrawLine(p + new Vector2(0, -8f * s), p + new Vector2(-6f * s, -14f * s), ink, w * 0.8f, true);
                cv.DrawLine(p + new Vector2(0, -11f * s), p + new Vector2(5.5f * s, -17f * s), ink, w * 0.8f, true);
                cv.DrawLine(p + new Vector2(-3f * s, -11f * s), p + new Vector2(-4f * s, -17f * s), ink, w * 0.6f, true);
                break;
            case WorldMapArt.Grave:
                cv.DrawArc(p + new Vector2(0, -7f * s), 3.2f * s, Mathf.Pi, Mathf.Tau, 6, ink, w, true);
                cv.DrawLine(p + new Vector2(-3.2f * s, -7f * s), p + new Vector2(-3.2f * s, 0), ink, w, true);
                cv.DrawLine(p + new Vector2(3.2f * s, -7f * s), p + new Vector2(3.2f * s, 0), ink, w, true);
                cv.DrawLine(p + new Vector2(-5.5f * s, 0), p + new Vector2(5.5f * s, 0), ink, w * 0.8f, true);
                break;
            case WorldMapArt.Peak:
                T3[0] = p + new Vector2(-12f * s, 0); T3[1] = p + new Vector2(0, -18f * s); T3[2] = p + new Vector2(12f * s, 0);
                cv.DrawColoredPolygon(T3, new Color(0.62f, 0.64f, 0.66f, 0.9f));
                T3[0] = p + new Vector2(-4.2f * s, -11.7f * s); T3[1] = p + new Vector2(0, -18f * s); T3[2] = p + new Vector2(4.2f * s, -11.7f * s);
                cv.DrawColoredPolygon(T3, new Color(0.93f, 0.95f, 0.97f, 0.95f));
                cv.DrawLine(p + new Vector2(-12f * s, 0), p + new Vector2(0, -18f * s), ink, w, true);
                cv.DrawLine(p + new Vector2(0, -18f * s), p + new Vector2(12f * s, 0), ink, w, true);
                cv.DrawLine(p + new Vector2(1.5f * s, -12f * s), p + new Vector2(4.5f * s, -2f * s), new Color(ink, 0.5f), w * 0.7f, true);
                cv.DrawLine(p + new Vector2(4f * s, -9f * s), p + new Vector2(7.5f * s, -1.5f * s), new Color(ink, 0.5f), w * 0.7f, true);
                break;
            case WorldMapArt.Barrow:
                cv.DrawArc(p, 10f * s, Mathf.Pi, Mathf.Tau, 12, ink, w, true);
                cv.DrawLine(p + new Vector2(-12f * s, 0), p + new Vector2(12f * s, 0), ink, w * 0.8f, true);
                cv.DrawRect(new Rect2(p.X - 2f * s, p.Y - 5f * s, 4f * s, 5f * s), new Color(ink, 0.9f));
                break;
            case WorldMapArt.Menhir:
                cv.DrawLine(p + new Vector2(-2.2f * s, 0), p + new Vector2(-1.4f * s, -11f * s), ink, w, true);
                cv.DrawLine(p + new Vector2(-1.4f * s, -11f * s), p + new Vector2(1.8f * s, -10f * s), ink, w, true);
                cv.DrawLine(p + new Vector2(1.8f * s, -10f * s), p + new Vector2(2.4f * s, 0), ink, w, true);
                break;
            case WorldMapArt.Reeds:
                cv.DrawLine(p, p + new Vector2(-3f * s, -10f * s), ink, w * 0.8f, true);
                cv.DrawLine(p, p + new Vector2(0.5f * s, -12f * s), ink, w * 0.8f, true);
                cv.DrawLine(p, p + new Vector2(4f * s, -9f * s), ink, w * 0.8f, true);
                cv.DrawArc(p + new Vector2(-7f * s, 5f * s), 5f * s, Mathf.Pi * 1.15f, Mathf.Pi * 1.85f, 6, new Color(0.55f, 0.66f, 0.35f, 0.55f), w * 0.8f, true);
                cv.DrawArc(p + new Vector2(3f * s, 5f * s), 5f * s, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 6, new Color(0.55f, 0.66f, 0.35f, 0.55f), w * 0.8f, true);
                break;
            case WorldMapArt.Dune:
                cv.DrawArc(p + new Vector2(0, 12f * s), 18f * s, Mathf.Pi * 1.2f, Mathf.Pi * 1.8f, 10, ink, w, true);
                cv.DrawArc(p + new Vector2(9f * s, 15f * s), 15f * s, Mathf.Pi * 1.3f, Mathf.Pi * 1.75f, 8, new Color(ink, 0.5f), w * 0.8f, true);
                break;
            case WorldMapArt.Pillar:
                cv.DrawRect(new Rect2(p.X - 2.4f * s, p.Y - 13f * s, 4.8f * s, 13f * s), new Color(0.74f, 0.58f, 0.32f, 0.85f));
                cv.DrawRect(new Rect2(p.X - 2.4f * s, p.Y - 13f * s, 4.8f * s, 13f * s), ink, false, w * 0.8f);
                cv.DrawLine(p + new Vector2(-4.5f * s, 0), p + new Vector2(4.5f * s, 0), ink, w, true);
                cv.DrawLine(p + new Vector2(-3.6f * s, -13f * s), p + new Vector2(3.2f * s, -15f * s), ink, w, true);
                break;
            case WorldMapArt.Spire:
                T3[0] = p + new Vector2(-4f * s, 0); T3[1] = p + new Vector2(0, -24f * s); T3[2] = p + new Vector2(4f * s, 0);
                cv.DrawColoredPolygon(T3, new Color(0.86f, 0.85f, 0.83f, 0.92f));
                cv.DrawLine(p + new Vector2(-4f * s, 0), p + new Vector2(0, -24f * s), ink, w, true);
                cv.DrawLine(p + new Vector2(0, -24f * s), p + new Vector2(4f * s, 0), ink, w, true);
                cv.DrawLine(p + new Vector2(-6f * s, 0), p + new Vector2(6f * s, 0), ink, w, true);
                break;
            case WorldMapArt.Crag:
                T3[0] = p + new Vector2(-6f * s, 0); T3[1] = p + new Vector2(1.5f * s, -19f * s); T3[2] = p + new Vector2(7f * s, 0);
                cv.DrawColoredPolygon(T3, new Color(0.04f, 0.03f, 0.03f, 0.95f));
                cv.DrawLine(p + new Vector2(-6f * s, 0), p + new Vector2(1.5f * s, -19f * s), new Color(1f, 0.45f, 0.12f, 0.7f), w * 0.8f, true);
                cv.DrawLine(p + new Vector2(1.5f * s, -19f * s), p + new Vector2(7f * s, 0), new Color(0.5f, 0.2f, 0.08f, 0.7f), w * 0.8f, true);
                break;
            case WorldMapArt.Hut:
                cv.DrawRect(new Rect2(p.X - 4.5f * s, p.Y - 6f * s, 9f * s, 6f * s), new Color(0.3f, 0.22f, 0.14f, 0.9f));
                T3[0] = p + new Vector2(-6.5f * s, -6f * s); T3[1] = p + new Vector2(0, -12f * s); T3[2] = p + new Vector2(6.5f * s, -6f * s);
                cv.DrawColoredPolygon(T3, new Color(0.14f, 0.1f, 0.08f, 0.95f));
                cv.DrawRect(new Rect2(p.X - 1.2f * s, p.Y - 3.6f * s, 2.4f * s, 2.4f * s), new Color(1f, 0.72f, 0.3f, 0.95f));
                break;
        }
    }

    private void DrawWorldNodes()
    {
        try
        {
            LayoutWorld();
            var cv = _worldNodes; float f = _wFit;
            if (f <= 0.01f) return;
            foreach (var r in WorldMapArt.Routes)
            {
                NodeView a = NodeById(r.A.Id), b = NodeById(r.B.Id);
                bool lit = a != null && b != null && !a.Locked && !b.Locked;
                var pts = r.Pts;
                for (int i = 0; i + 1 < pts.Length; i += 2)
                {
                    var mid = (pts[i] + pts[i + 1]) * 0.5f;
                    if (mid.DistanceSquaredTo(r.A.P) < 44f * 44f || mid.DistanceSquaredTo(r.B.P) < 44f * 44f) continue;
                    Vector2 p0 = WP(pts[i]), p1 = WP(pts[i + 1]);
                    if (lit)
                    {
                        cv.DrawLine(p0, p1, new Color(0.03f, 0.02f, 0.015f, 0.75f), 5f * f, true);
                        cv.DrawLine(p0, p1, new Color(0.95f, 0.76f, 0.4f, 0.95f), 2.6f * f, true);
                    }
                    else cv.DrawLine(p0, p1, new Color(0.09f, 0.07f, 0.055f, 0.62f), 2.2f * f, true);
                }
            }
            foreach (var n in _nodes) DrawNode(cv, n, f);
        }
        catch (Exception e) { DrawFail(e); }
    }

    private void DrawNode(CanvasItem cv, NodeView n, float f)
    {
        var c = WP(n.Site.P);
        bool town = n.R == null, hover = ReferenceEquals(n, _hoverNode);
        float r = NodeRadius(n) * f;
        Color ring = n.Locked ? new Color(0.40f, 0.38f, 0.36f) : n.Accent;
        Color gold = UiTheme.Gold;

        // The medallion. With the art it is a cast ring over a dark floor: dull iron while the road is closed, ember
        // gold once it is open, bright under the pointer and wherever a journey is offered; the town is the hub with
        // a second ring. The floor keeps a breath of the region's colour, the glyph carries the rest of it.
        LookArt();
        var cast = n.Locked ? _pNodeLocked : town ? _pNodeHub : hover || (_travel && n.Travel) ? _pNodeLit : _pNodeOpen;
        float arcR = r + 6.5f * f;
        if (cast != null)
        {
            // outer radius of the cast ring (the hub's second ring stands 18 % further out and ends where the others do)
            float cr = town ? (r + 4f * f) / 1.18f : r + 3f * f;
            arcR = cr + 4.5f * f;
            cv.DrawCircle(c, town ? r + 6f * f : arcR + 3.5f * f, new Color(0.02f, 0.015f, 0.012f, 0.84f));
            cv.DrawCircle(c, cr - 3f * f, n.Locked ? new Color(0.07f, 0.068f, 0.066f) : new Color(0.075f, 0.062f, 0.052f).Lerp(n.Accent, 0.1f));
            Color mod = n.Locked ? new Color(0.86f, 0.86f, 0.88f) : hover && town ? new Color(1.2f, 1.16f, 1.05f) : Colors.White;
            UiArt.Sprite(cv, cast, new Rect2(c.X - cr, c.Y - cr, cr * 2f, cr * 2f), mod);
        }
        else
        {
            cv.DrawCircle(c, r + 7f * f, new Color(0.02f, 0.015f, 0.012f, 0.84f));
            cv.DrawCircle(c, r, n.Locked ? new Color(0.085f, 0.082f, 0.08f) : new Color(0.09f, 0.072f, 0.06f));
            cv.DrawArc(c, r, 0f, Mathf.Tau, 48, ring, 3f * f, true);
        }
        if (!town)
            for (int i = 0; i < 4; i++)          // renown: four arcs round the medallion
            {
                float a0 = -Mathf.Pi * 0.5f + i * Mathf.Pi * 0.5f + 0.13f;
                cv.DrawArc(c, arcR, a0, a0 + Mathf.Pi * 0.5f - 0.26f, 10, i < n.RenownTier ? gold : new Color(0.2f, 0.18f, 0.16f, 0.9f), 2.6f * f, true);
            }
        if (n.Locked) DrawPadlock(cv, c, r * 0.5f);
        else DrawSiteGlyph(cv, n.Id, c, r * 0.6f, n.Accent.Lerp(Colors.White, 0.25f), f);
        if (n.Waypoint && !town)
        {
            // the lit beacon: its flame set in a small forged socket on the shoulder of the medallion
            var at = c + new Vector2(r * 0.86f, -r * 0.86f);
            var flame = MapGlyphs.Style("waypoint");
            if (_pSocket != null)
            {
                float sr = 11.5f * f;
                UiArt.Sprite(cv, _pSocket, new Rect2(at.X - sr, at.Y - sr, sr * 2f, sr * 2f));
                MapGlyphs.Shape(cv, flame.Shape, at, 6.5f * f, flame.Col);
            }
            else MapGlyphs.Draw(cv, flame, at, 7f * f);
        }

        // the label block sits under the medallion, or above it where a road arrives from the south. Its lines are
        // written at fixed design sizes and centred from their measured widths (see LabelsOf).
        var lb = LabelsOf(n, c, r, f);
        float yPips = lb.PipsY;
        var title = WorldUiKit.TitleFont; var small = WorldUiKit.SmallFont;
        Color nameCol = hover ? gold.Lightened(0.15f) : n.Locked ? new Color(0.62f, 0.6f, 0.57f) : new Color(0.96f, 0.92f, 0.84f);
        if (lb.PlateH > 0f && _pPlate != null)
        {
            // the name plate: held back where the road is closed, warmed up under the pointer
            Color mod = n.Locked ? new Color(0.62f, 0.62f, 0.65f) : hover ? new Color(1.32f, 1.2f, 1f) : Colors.White;
            UiArt.Nine(cv, _pPlate, new Rect2(MathF.Round(c.X - lb.PlateHalfW), MathF.Round(lb.Top), lb.PlateHalfW * 2f, lb.PlateH), mod);
            WorldUiKit.Text(cv, title, new Vector2(MathF.Round(c.X - n.NameW * 0.5f), lb.NameBase), n.Name, town ? FsNodeTown : FsNode, nameCol, HorizontalAlignment.Left, -1f, 3);
        }
        else
        WorldUiKit.Text(cv, title, new Vector2(MathF.Round(c.X - n.NameW * 0.5f), lb.NameBase), n.Name, town ? FsNodeTown : FsNode, nameCol, HorizontalAlignment.Left, -1f, 6);
        WorldUiKit.Text(cv, small, new Vector2(MathF.Round(c.X - n.Line2W * 0.5f), lb.Line2Base), n.Line2, FsNodeLine, new Color(0.80f, 0.76f, 0.68f, n.Locked ? 0.7f : 1f), HorizontalAlignment.Left, -1f, 4);
        if (town) return;

        float step = 19f * f, pr = 6f * f;
        var p0 = new Vector2(c.X - step * 2f, yPips);
        cv.DrawCircle(p0, pr + 2.5f * f, new Color(0.02f, 0.015f, 0.012f, 0.8f));
        MapGlyphs.Shape(cv, MapGlyphs.Tower, p0, pr, n.Stronghold ? new Color(1f, 0.55f, 0.2f) : new Color(0.3f, 0.28f, 0.26f));
        for (int i = 0; i < 3; i++)
        {
            var p = new Vector2(c.X - step + i * step, yPips);
            cv.DrawCircle(p, pr + 2.5f * f, new Color(0.02f, 0.015f, 0.012f, 0.8f));
            MapGlyphs.Shape(cv, MapGlyphs.Diamond, p, pr, n.DungeonDone[i] ? n.Accent : new Color(0.3f, 0.28f, 0.26f));
            if (!n.DungeonDone[i]) MapGlyphs.Shape(cv, MapGlyphs.Diamond, p, pr * 0.55f, new Color(0.06f, 0.05f, 0.045f));
        }
        var pb = new Vector2(c.X + step * 2f, yPips);
        cv.DrawCircle(pb, pr + 2.5f * f, new Color(0.02f, 0.015f, 0.012f, 0.8f));
        MapGlyphs.Shape(cv, MapGlyphs.Skull, pb, pr, n.Boss ? new Color(0.42f, 0.4f, 0.38f) : Palette.Danger);
        if (n.Boss) cv.DrawLine(pb + new Vector2(-pr, pr), pb + new Vector2(pr, -pr), new Color(0.9f, 0.86f, 0.78f), 1.6f * f, true);

        WorldUiKit.Text(cv, small, new Vector2(MathF.Round(c.X - n.Line3W * 0.5f), lb.Line3Base), n.Line3, FsNodeLine, n.RenownTier > 0 ? new Color(gold, 0.95f) : new Color(0.72f, 0.68f, 0.6f, 0.9f), HorizontalAlignment.Left, -1f, 4);
    }

    private static void DrawPadlock(CanvasItem cv, Vector2 c, float r)
    {
        var col = new Color(0.62f, 0.6f, 0.57f);
        cv.DrawArc(c + new Vector2(0, -r * 0.15f), r * 0.5f, Mathf.Pi, Mathf.Tau, 12, col, MathF.Max(2f, r * 0.22f), true);
        cv.DrawRect(new Rect2(c.X - r * 0.75f, c.Y - r * 0.15f, r * 1.5f, r * 1.15f), col);
        cv.DrawCircle(c + new Vector2(0, r * 0.35f), r * 0.2f, new Color(0.08f, 0.075f, 0.07f));
    }

    private static void DrawSiteGlyph(CanvasItem cv, string id, Vector2 c, float r, Color col, float f)
    {
        float w = MathF.Max(1.6f, 2.2f * f);
        switch (id)
        {
            case "town":        // the great brazier
                cv.DrawArc(c + new Vector2(0, r * 0.05f), r * 0.8f, Mathf.Pi * 0.08f, Mathf.Pi * 0.92f, 12, col, w, true);
                cv.DrawLine(c + new Vector2(-r * 0.8f, r * 0.2f), c + new Vector2(r * 0.8f, r * 0.2f), col, w, true);
                cv.DrawLine(c + new Vector2(0, r * 0.85f), c + new Vector2(0, r * 1.15f), col, w, true);
                MapGlyphs.Shape(cv, MapGlyphs.Flame, c + new Vector2(0, -r * 0.45f), r * 0.62f, new Color(1f, 0.6f, 0.18f));
                break;
            case "wastes":      // a dead tree
                cv.DrawLine(c + new Vector2(0, r), c + new Vector2(0, -r * 0.9f), col, w, true);
                cv.DrawLine(c + new Vector2(0, r * 0.1f), c + new Vector2(-r * 0.7f, -r * 0.55f), col, w * 0.85f, true);
                cv.DrawLine(c + new Vector2(0, -r * 0.2f), c + new Vector2(r * 0.65f, -r * 0.85f), col, w * 0.85f, true);
                cv.DrawLine(c + new Vector2(-r * 0.38f, -r * 0.25f), c + new Vector2(-r * 0.5f, -r * 0.95f), col, w * 0.7f, true);
                cv.DrawLine(c + new Vector2(-r * 0.6f, r), c + new Vector2(r * 0.6f, r), col, w * 0.85f, true);
                break;
            case "frost":       // a pass between two peaks
                cv.DrawLine(c + new Vector2(-r, r * 0.8f), c + new Vector2(-r * 0.3f, -r * 0.8f), col, w, true);
                cv.DrawLine(c + new Vector2(-r * 0.3f, -r * 0.8f), c + new Vector2(r * 0.1f, r * 0.1f), col, w, true);
                cv.DrawLine(c + new Vector2(r * 0.1f, r * 0.1f), c + new Vector2(r * 0.45f, -r * 0.4f), col, w, true);
                cv.DrawLine(c + new Vector2(r * 0.45f, -r * 0.4f), c + new Vector2(r, r * 0.8f), col, w, true);
                cv.DrawLine(c + new Vector2(-r * 0.52f, -r * 0.3f), c + new Vector2(-r * 0.08f, -r * 0.3f), col, w * 0.7f, true);
                break;
            case "crypt":       // a barrow door
                cv.DrawArc(c + new Vector2(0, -r * 0.05f), r * 0.75f, Mathf.Pi, Mathf.Tau, 14, col, w, true);
                cv.DrawLine(c + new Vector2(-r * 0.75f, -r * 0.05f), c + new Vector2(-r * 0.75f, r * 0.9f), col, w, true);
                cv.DrawLine(c + new Vector2(r * 0.75f, -r * 0.05f), c + new Vector2(r * 0.75f, r * 0.9f), col, w, true);
                cv.DrawLine(c + new Vector2(-r, r * 0.9f), c + new Vector2(r, r * 0.9f), col, w, true);
                cv.DrawRect(new Rect2(c.X - r * 0.22f, c.Y + r * 0.1f, r * 0.44f, r * 0.8f), col);
                break;
            case "mire":        // reeds over still water
                cv.DrawLine(c + new Vector2(-r * 0.45f, r * 0.5f), c + new Vector2(-r * 0.6f, -r * 0.7f), col, w, true);
                cv.DrawLine(c + new Vector2(0, r * 0.5f), c + new Vector2(0.05f * r, -r), col, w, true);
                cv.DrawLine(c + new Vector2(r * 0.45f, r * 0.5f), c + new Vector2(r * 0.65f, -r * 0.6f), col, w, true);
                cv.DrawArc(c + new Vector2(-r * 0.45f, r * 1.25f), r * 0.6f, Mathf.Pi * 1.2f, Mathf.Pi * 1.8f, 8, col, w * 0.8f, true);
                cv.DrawArc(c + new Vector2(r * 0.45f, r * 0.45f), r * 0.6f, Mathf.Pi * 0.2f, Mathf.Pi * 0.8f, 8, col, w * 0.8f, true);
                break;
            case "sands":       // a broken column under a dead sun
                cv.DrawArc(c + new Vector2(r * 0.45f, -r * 0.5f), r * 0.32f, 0f, Mathf.Tau, 16, col, w * 0.8f, true);
                cv.DrawRect(new Rect2(c.X - r * 0.62f, c.Y - r * 0.5f, r * 0.42f, r * 1.4f), col);
                cv.DrawLine(c + new Vector2(-r, r * 0.9f), c + new Vector2(r, r * 0.9f), col, w, true);
                cv.DrawArc(c + new Vector2(r * 0.35f, r * 1.6f), r * 0.95f, Mathf.Pi * 1.25f, Mathf.Pi * 1.75f, 8, col, w * 0.8f, true);
                break;
            case "sanctum":     // a cathedral spire and its silent bell
                cv.DrawLine(c + new Vector2(-r * 0.42f, r * 0.9f), c + new Vector2(0, -r * 1.05f), col, w, true);
                cv.DrawLine(c + new Vector2(0, -r * 1.05f), c + new Vector2(r * 0.42f, r * 0.9f), col, w, true);
                cv.DrawLine(c + new Vector2(-r * 0.85f, r * 0.9f), c + new Vector2(r * 0.85f, r * 0.9f), col, w, true);
                cv.DrawArc(c + new Vector2(0, r * 0.25f), r * 0.2f, 0f, Mathf.Tau, 12, col, w * 0.8f, true);
                break;
            default:            // abyss: the dead forge, an ember in a ring of obsidian
                cv.DrawArc(c, r * 0.95f, Mathf.Pi * 0.1f, Mathf.Pi * 0.9f, 12, col, w, true);
                cv.DrawArc(c, r * 0.95f, Mathf.Pi * 1.1f, Mathf.Pi * 1.9f, 12, col, w, true);
                MapGlyphs.Shape(cv, MapGlyphs.Diamond, c, r * 0.6f, new Color(1f, 0.5f, 0.15f));
                MapGlyphs.Shape(cv, MapGlyphs.Diamond, c, r * 0.28f, new Color(1f, 0.92f, 0.8f));
                break;
        }
    }

    private void DrawWorldFx()
    {
        try
        {
            var fx = _worldFx; float f = _wFit;
            if (f <= 0.01f) return;
            float pulse = 0.5f + 0.5f * MathF.Sin(_t * 3f), bob = MathF.Sin(_t * 2.4f) * 4f * f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                var c = WP(n.Site.P);
                float r = NodeRadius(n) * f;
                if (_travel && n.Travel)
                    fx.DrawArc(c, r + (12f + 5f * pulse) * f, 0f, Mathf.Tau, 48, new Color(1f, 0.82f, 0.4f, 0.85f - 0.45f * pulse), 2.4f * f, true);
                // (with the art the node under the pointer shows it in its cast ring and its name plate: see DrawNode)
                if (ReferenceEquals(n, _hoverNode) && _pNodeLit == null)
                    fx.DrawArc(c, r + 3f * f, 0f, Mathf.Tau, 48, new Color(1f, 0.95f, 0.8f, 0.9f), 2f * f, true);
                // the two floating markers sit on the side of the medallion that the label block leaves free
                float away = n.Site.LabelAbove ? 1f : -1f;
                if (n.Current)
                {
                    var p = c + new Vector2(-r * 0.75f, away * (r + 20f * f) + bob);
                    fx.DrawCircle(p, 11f * f, new Color(0.02f, 0.015f, 0.01f, 0.8f));
                    T3[0] = p + new Vector2(0, -away * 8f * f); T3[1] = p + new Vector2(-7.5f * f, away * 6f * f); T3[2] = p + new Vector2(7.5f * f, away * 6f * f);
                    fx.DrawColoredPolygon(T3, new Color(1f, 0.9f, 0.6f));
                }
                if (n.Quest)
                {
                    var p = c + new Vector2(r * 0.75f, away * (r + 22f * f) - bob);
                    fx.DrawArc(p, (13f + 5f * pulse) * f, 0f, Mathf.Tau, 24, new Color(1f, 0.83f, 0.35f, 0.6f * (1f - pulse)), 1.6f * f, true);
                    fx.DrawCircle(p, 12f * f, new Color(0.02f, 0.015f, 0.01f, 0.85f));
                    MapGlyphs.Shape(fx, MapGlyphs.Exclaim, p, 9f * f, UiTheme.Gold);
                }
            }
        }
        catch (Exception e) { DrawFail(e); }
    }
}
