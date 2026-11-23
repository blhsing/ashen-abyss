using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AshenAbyss;

/// <summary>Remembers which items the player has already looked at, so freshly acquired ones can carry a marker.</summary>
public static class ItemMarks
{
    private static Hero _hero;
    private static bool _hooked;
    private static readonly HashSet<string> Seen = new();

    /// <summary>
    /// Starts following the game from the first world event on (the "area" event of every load comes before any
    /// loot can be picked up), so items found before the inventory was ever opened still count as new.
    /// </summary>
    public static void EnsureHooked()
    {
        if (_hooked) return;
        _hooked = true;
        World.Event += (evt, arg) => Track(Game.I?.Hero);
    }

    /// <summary>Cheap; call often. When the hero changes (new game, load) everything already owned counts as seen.</summary>
    public static void Track(Hero h)
    {
        if (ReferenceEquals(h, _hero)) return;
        _hero = h;
        Seen.Clear();
        if (h == null) return;
        foreach (var it in h.D.Bag) Seen.Add(it.Uid);
        foreach (var it in h.D.Stash) Seen.Add(it.Uid);
        foreach (var it in h.D.Equip) if (it != null) Seen.Add(it.Uid);
    }

    public static bool IsNew(Item it) => it != null && !Seen.Contains(it.Uid);
    public static void See(Item it) { if (it != null) Seen.Add(it.Uid); }
}

/// <summary>One hoverable cell registered with a <see cref="SlotBoard"/>: an item, or any control with an explanation.</summary>
public sealed class SlotView
{
    public Control Node;                 // rectangle that reacts to the mouse
    public Control Clip;                 // scroll area that may hide the cell (null when always visible)
    public Item Item;
    public bool Compare = true;          // show the equipped counterpart beside the tooltip
    public bool Glow = true;             // outline the cell while hovered
    public Func<Item, string> Extra;     // extra BBCode lines under the item tooltip (price, hints)
    public string Text;                  // tooltip of a cell that is not an item
    public Control NewDot;               // "new item" marker, hidden on first hover
}

/// <summary>
/// The hover logic shared by the item panels. Cells are found by rectangle every frame, so a cell that is rebuilt
/// under a resting pointer still answers, and the tooltip is shown once per change with the equipped item beside it.
/// (The theme's slot widget shows the same text on its own mouse-enter; this adds the comparison card and the
/// markers, and covers the cells that are not items.)
/// </summary>
public sealed class SlotBoard
{
    private readonly List<SlotView> _views = new();
    private SlotView _cur;
    private Panel _glow;
    public Action<SlotView> OnHover;
    public Item Hovered => _cur?.Item;
    /// <summary>The item under the pointer in any open panel (the last board that saw it), or null.</summary>
    public static Item HoveredItem;

    public SlotView Add(SlotView v) { _views.Add(v); return v; }

    /// <summary>Forget every cell. Call BEFORE freeing the nodes of a rebuild.</summary>
    public void Clear()
    {
        DetachGlow();
        _views.Clear();
        if (_cur != null) { _cur = null; HoveredItem = null; Game.I?.Ui?.HideTooltip(); }
    }

    public void Dispose()
    {
        Clear();
        if (_glow != null && GodotObject.IsInstanceValid(_glow)) _glow.QueueFree();
        _glow = null;
    }

    public void Poll(Control panel)
    {
        SlotView hit = null;
        if (_views.Count > 0 && panel.IsVisibleInTree())
        {
            var vp = panel.GetViewport();
            var over = vp?.GuiGetHoveredControl();
            // a dialog or another panel above us owns the mouse: no tooltips from underneath
            if (vp != null && (over == null || over == panel || panel.IsAncestorOf(over)))
            {
                Vector2 m = Ui.PointerPos(panel);
                for (int i = 0; i < _views.Count; i++)
                {
                    var v = _views[i];
                    if (!GodotObject.IsInstanceValid(v.Node)) continue;
                    if (v.Clip != null && GodotObject.IsInstanceValid(v.Clip) && !v.Clip.GetGlobalRect().HasPoint(m)) continue;
                    if (v.Node.GetGlobalRect().HasPoint(m)) { hit = v; break; }
                }
            }
        }
        if (ReferenceEquals(hit, _cur)) return;
        _cur = hit;
        HoveredItem = hit?.Item;          // SYSTEMS: G opens the 武器指南 on the item under the pointer, whichever panel shows it
        DetachGlow();
        var ui = Game.I?.Ui;
        if (hit == null) { ui?.HideTooltip(); return; }
        if (hit.Glow) AttachGlow(hit.Node);
        OnHover?.Invoke(hit);
        string bb = hit.Item != null ? ItemUi.Tip(hit) : hit.Text;
        if (string.IsNullOrEmpty(bb)) { ui?.HideTooltip(); return; }
        string cmp = hit.Item != null && hit.Compare ? ItemUi.CompareTip(hit.Item) : null;
        // beside the cell, never on top of it: the manager puts the card to the right of the rectangle, or to its
        // left when the right has no room, and the comparison card on the far side of that
        ui?.ShowTooltipAt(hit.Node.GetGlobalRect(), bb, cmp, null);
    }

    private void AttachGlow(Control host)
    {
        if (_glow == null || !GodotObject.IsInstanceValid(_glow))
        {
            _glow = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
            _glow.AddThemeStyleboxOverride("panel", ItemUi.OutlineBox(new Color(1f, 0.93f, 0.76f, 0.9f), 2));
        }
        host.AddChild(_glow);
        _glow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    private void DetachGlow()
    {
        if (_glow == null || !GodotObject.IsInstanceValid(_glow)) { _glow = null; return; }
        _glow.GetParent()?.RemoveChild(_glow);
    }
}

/// <summary>Widgets shared by the inventory and the vendor panels: item cells with markers, currency rows, tabs, cost lines.</summary>
public static class ItemUi
{
    public static readonly Color ScrapColor = new(0.72f, 0.76f, 0.8f);
    public static readonly Color DustColor = new(0.7f, 0.58f, 0.96f);
    public static readonly Color CoreColor = new(1f, 0.55f, 0.2f);
    public static readonly Color JunkColor = new(0.92f, 0.4f, 0.34f);
    public static readonly Color PickColor = new(0.45f, 0.9f, 0.4f);

    private static StyleBox _emptyBox;
    private static readonly Dictionary<(Color, int), StyleBox> Outlines = new();
    private static readonly Dictionary<Color, StyleBox> Dots = new();
    private static readonly Dictionary<int, StyleBox> OptionBoxes = new();

    // ───────────────────────── raster art ─────────────────────────

    private static bool _artLooked;
    private static UiArt.Piece _socket, _mount, _bullet, _headLine, _rule;

    /// <summary>The pieces the item panels draw by themselves, looked up once. They stay null without the art: every user falls back to its flat drawing.</summary>
    private static void LookArt()
    {
        if (_artLooked) return;
        _artLooked = true;
        if (!UiTheme.Art) return;
        _socket = UiArt.P("socket"); _mount = UiArt.P("gem_slot"); _bullet = UiArt.P("bullet");
        _headLine = UiArt.P("header_line"); _rule = UiArt.P("rule");
    }

    /// <summary>True when the forged socket pieces are there: <see cref="DrawSocket"/> may be used.</summary>
    public static bool SocketArt { get { LookArt(); return _socket != null && _mount != null; } }

    /// <summary>
    /// One forged socket filling the square <paramref name="box"/>: the iron ring — or, with <paramref name="mount"/>,
    /// the diamond mount — holding a stone of colour <paramref name="stone"/> when <paramref name="filled"/>.
    /// For draw callbacks (it allocates nothing); draws nothing without the art, see <see cref="SocketArt"/>.
    /// </summary>
    public static void DrawSocket(CanvasItem ci, Rect2 box, bool filled, Color stone, bool mount = false)
    {
        var piece = mount ? _mount : _socket;
        if (piece == null) return;
        UiArt.Sprite(ci, piece, box);
        if (!filled) return;
        Vector2 c = box.Position + box.Size * 0.5f;
        float r = box.Size.X * (mount ? 0.25f : 0.31f);
        ci.DrawCircle(c, r, stone.Darkened(0.5f), true, -1f, true);
        ci.DrawCircle(c, r * 0.8f, stone, true, -1f, true);
        if (r >= 3.5f) ci.DrawCircle(c - new Vector2(r, r) * 0.28f, r * 0.3f, new Color(stone.Lightened(0.6f), 0.85f), true, -1f, true);
    }

    /// <summary>A stone of colour <paramref name="c"/> set in a small iron socket (the mark of a currency), <paramref name="size"/> design px.</summary>
    private static Control Stone(Color c, float size)
    {
        var p = new Control
        {
            CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };
        p.Draw += () => DrawSocket(p, new Rect2(Vector2.Zero, p.Size), true, c);
        return p;
    }

    /// <summary>
    /// The theme's slot marks the sockets of an item with flat squares; here they become forged sockets, each one
    /// holding a stone of its gem's colour once it is filled. The squares are recognised by their shape (a row of
    /// one colour rectangle per socket) and hidden only when found, so a slot that draws its sockets differently
    /// is left exactly as the theme made it.
    /// </summary>
    private static void SocketPips(Control holder, Control inner, Item it, int size)
    {
        if (it == null || it.Sockets <= 0 || inner == null || !SocketArt) return;
        int n = it.Sockets;
        HBoxContainer flat = null;
        foreach (var ch in inner.GetChildren())
        {
            if (ch is not HBoxContainer hb || hb.GetChildCount() != n) continue;
            bool squares = true;
            foreach (var sq in hb.GetChildren()) if (sq is not ColorRect) { squares = false; break; }
            if (squares) { flat = hb; break; }
        }
        if (flat == null) return;
        flat.Visible = false;
        var set = new bool[n];
        var stones = new Color[n];
        for (int i = 0; i < n; i++)
        {
            GemDef g = i < it.Gems.Count ? ItemDb.Gem(it.Gems[i]) : null;
            set[i] = g != null;
            if (g != null) stones[i] = g.Color;
        }
        float d = MathF.Max(10f, MathF.Round(size / 5f)), step = d + 1f;
        var pips = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        pips.Draw += () =>
        {
            for (int i = 0; i < n; i++) DrawSocket(pips, new Rect2(i * step, 0f, d, d), set[i], stones[i]);
        };
        holder.AddChild(pips);
        Corner(pips, false, true, n * step - 1f, d, 6f);      // inside the cell's rarity line
    }

    /// <summary>
    /// Underlines a section heading with the forged header line (a stud and a tapering rule) across the heading's
    /// column. The line lies in the gap under the label, so nothing changes size.
    /// </summary>
    public static T Underlined<T>(T label) where T : Control
    {
        LookArt();
        if (_headLine == null) return label;
        float h = _headLine.BoxSize.Y;
        label.Draw += () => UiArt.Nine(label, _headLine, new Rect2(0f, label.Size.Y - 2f, label.Size.X, h));
        return label;
    }

    /// <summary>A vertical divider between two columns: the forged rule stood on end (the theme's flat line without the art).</summary>
    public static Control VRule()
    {
        LookArt();
        if (_rule == null) return new VSeparator();
        float h = _rule.BoxSize.Y;
        var host = new Control { CustomMinimumSize = new Vector2(10f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        // a quarter turn to the left: the piece's lit upper edge ends up on the left, as the light falls on everything else
        var line = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Rotation = -Mathf.Pi * 0.5f };
        line.Draw += () => UiArt.Nine(line, _rule, new Rect2(0f, 0f, line.Size.X, h), new Color(1f, 1f, 1f, 0.8f));
        host.AddChild(line);
        host.Resized += () =>
        {
            line.Size = new Vector2(host.Size.Y, h);
            line.Position = new Vector2(MathF.Round((host.Size.X - h) * 0.5f), host.Size.Y);
            line.QueueRedraw();
        };
        return host;
    }

    /// <summary>
    /// An inset well (recessed rim over the darkest field) around a grid of cells or a list. With
    /// <paramref name="fill"/> it is as wide as its column and a grid sits in its middle; otherwise it hugs its content.
    /// </summary>
    public static PanelContainer Well(Control content, bool fill = true, bool centre = true)
    {
        var w = UiTheme.Well();
        w.MouseFilter = Control.MouseFilterEnum.Pass;
        w.SizeFlagsHorizontal = fill ? Control.SizeFlags.Fill : Control.SizeFlags.ShrinkBegin;
        if (content != null)
        {
            if (fill && centre && content is GridContainer) content.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            w.AddChild(content);
        }
        return w;
    }

    /// <summary>Room the well takes around its content on each axis (both sides together), in design px.</summary>
    public static float WellPad
    {
        get
        {
            var sb = UiTheme.WellBox;
            return sb == null ? 0f : sb.GetMargin(Side.Left) + sb.GetMargin(Side.Right);
        }
    }

    /// <summary>
    /// Dresses a button as one of several options to pick from (an affix, a recipe, a codex entry): a quiet row
    /// plate that lights up on hover, and the ember plate for the chosen one. Actions keep the theme's button.
    /// Without the row pieces only the lettering changes, as before.
    /// </summary>
    public static void Option(Button b, bool active, float padX = 18f)
    {
        Tone(b, active);
        if (!UiTheme.Art || !UiArt.Has("row") || !UiArt.Has("row_hover") || !UiArt.Has("row_on")) return;
        b.AddThemeStyleboxOverride("normal", OptionBox(active ? 2 : 0, padX));
        b.AddThemeStyleboxOverride("hover", OptionBox(active ? 2 : 1, padX));
        b.AddThemeStyleboxOverride("pressed", OptionBox(2, padX));
        b.AddThemeStyleboxOverride("hover_pressed", OptionBox(2, padX));
        b.AddThemeStyleboxOverride("disabled", OptionBox(3, padX));
        if (!active) return;
        var c = UiTheme.Gold.Lightened(0.3f);
        b.AddThemeColorOverride("font_color", c);
        b.AddThemeColorOverride("font_focus_color", c);
        b.AddThemeColorOverride("font_hover_color", UiTheme.Gold.Lightened(0.35f));
    }

    /// <summary>
    /// Row plates for options: 0 idle, 1 hover, 2 chosen, 3 disabled. With the theme button's content margins
    /// (18 × 8) an option is exactly as large as a button; a smaller <paramref name="padX"/> makes a compact chip.
    /// </summary>
    private static StyleBox OptionBox(int state, float padX)
    {
        int key = state + 8 * (int)MathF.Round(padX);
        if (OptionBoxes.TryGetValue(key, out var sb)) return sb;
        sb = state switch
        {
            1 => UiTheme.ArtBox("row_hover", padX, 8f),
            2 => UiTheme.ArtBox("row_on", padX, 8f),
            3 => UiTheme.ArtBox("items_row_off|" + key, () => new UiNine("row", null, 0f, padX, 8f) { FrameMod = new Color(0.66f, 0.66f, 0.68f, 0.6f) }),
            _ => UiTheme.ArtBox("row", padX, 8f),
        };
        return OptionBoxes[key] = sb;
    }

    // ───────────────────────── text ─────────────────────────

    public static string Col(Color c, string s) => $"[color=#{c.ToHtml(false)}]{s}[/color]";
    public static string Dim(string s) => Col(Palette.TextDim, s);
    public static string Pct(float v) => v.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    /// <summary>"花費　金幣 1,200（持有 3,400）　碎鐵 4（持有 2）" with unaffordable parts in red.</summary>
    public static string Costs(SaveData d, long gold, int scrap = 0, int dust = 0, int cores = 0)
    {
        var sb = new StringBuilder("花費　");
        int n = 0;
        n += Part(sb, "金幣", gold, d.Gold, Palette.Gold);
        // gold and materials together: the materials go on a line of their own under the gold, so no entry is ever
        // broken between its amount and what the hero holds (the panes are narrow at the largest text size)
        int kinds = n + (scrap > 0 ? 1 : 0) + (dust > 0 ? 1 : 0) + (cores > 0 ? 1 : 0);
        if (n > 0 && kinds >= 2) sb.Append('\n').Append("　　　");
        n += Part(sb, Crafting.ScrapName, scrap, d.Scrap, ScrapColor);
        n += Part(sb, Crafting.DustName, dust, d.Dust, DustColor);
        n += Part(sb, Crafting.CoreName, cores, d.Cores, CoreColor);
        if (n == 0) sb.Append(Dim("無"));
        return sb.ToString();
    }

    private static int Part(StringBuilder sb, string name, long need, long have, Color c)
    {
        if (need <= 0) return 0;
        sb.Append(Col(have >= need ? c : Palette.Bad, $"{name} {Crafting.Num(need)}"));
        sb.Append(Dim($"（持有 {Crafting.Num(have)}）　"));
        return 1;
    }

    public static string Tip(SlotView v)
    {
        var it = v.Item;
        if (it == null) return v.Text ?? "";
        var h = Game.I?.Hero;
        bool cmp = v.Compare && h != null && !h.IsEquipped(it);
        Item eq = cmp ? h.Compared(it) : null, eq2 = cmp ? h.ComparedExtra(it) : null;
        string bb = ItemText.Tooltip(it, eq, h?.D.Level ?? 99, eq != null || eq2 != null, eq2, cmp ? h : null);
        string extra = v.Extra?.Invoke(it);
        return string.IsNullOrEmpty(extra) ? bb : bb + "\n" + extra;
    }

    /// <summary>Tooltip of the equipped item this one would replace, for the side-by-side comparison (null when there is none).</summary>
    public static string CompareTip(Item it)
    {
        var h = Game.I?.Hero;
        if (h == null || it == null || h.IsEquipped(it)) return null;
        var eq = h.Compared(it);
        var eq2 = h.ComparedExtra(it);
        if (eq == null || ReferenceEquals(eq, it)) eq = null;
        if (eq == null && eq2 == null) return null;
        // a two-hand weapon against main hand + off hand: both worn pieces are shown
        string s = eq != null ? Dim("— 目前裝備 —") + "\n" + ItemText.Tooltip(eq, null, h.D.Level, false) : "";
        if (eq2 != null) s += (s.Length > 0 ? "\n\n" : "") + Dim("— 目前副手 —") + "\n" + ItemText.Tooltip(eq2, null, h.D.Level, false);
        return s;
    }

    public static string EquipSound(Item it) =>
        it.Kind == ItemKind.Weapon ? "equip_weapon" : it.Kind is ItemKind.Amulet or ItemKind.Ring ? "equip_jewel" : "equip_armor";

    /// <summary>Worn slots in reading order (hands first), for lists of worn gear.</summary>
    public static readonly EquipSlot[] WornOrder =
    {
        EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Gloves, EquipSlot.Pants, EquipSlot.Boots,
        EquipSlot.Amulet, EquipSlot.Ring1, EquipSlot.Ring2,
    };

    public static ItemKind KindOf(EquipSlot s) => s switch
    {
        EquipSlot.MainHand => ItemKind.Weapon, EquipSlot.OffHand => ItemKind.OffHand, EquipSlot.Helm => ItemKind.Helm, EquipSlot.Chest => ItemKind.Chest, EquipSlot.Gloves => ItemKind.Gloves,
        EquipSlot.Pants => ItemKind.Pants, EquipSlot.Boots => ItemKind.Boots, EquipSlot.Amulet => ItemKind.Amulet, _ => ItemKind.Ring
    };

    /// <summary>Caption of a worn slot: 主手 / 副手 for the hands, the item kind otherwise.</summary>
    public static string SlotName(EquipSlot s) => s == EquipSlot.MainHand ? "主手" : s == EquipSlot.OffHand ? "副手" : Names.Kind(KindOf(s));

    // ───────────────────────── style ─────────────────────────

    /// <summary>
    /// The frame of a marked cell. With the raster art it is a glowing rim around the forged cell, tinted with
    /// <paramref name="c"/> (the theme's gold keeps the rim's own colour): the bracketed selection rim from 3 px up
    /// (the chosen cell), the plain rim below (a candidate, the hovered cell). The rim lies outside the cell's
    /// rarity line, so both read. Without the art: a flat outline of that thickness.
    /// </summary>
    public static StyleBox OutlineBox(Color c, int px)
    {
        if (Outlines.TryGetValue((c, px), out var sb)) return sb;
        UiArt.Piece rim = UiTheme.Art ? UiArt.P(px >= 3 ? "slot_selected" : "slot_hover") : null;
        if (rim != null) sb = new UiNine { Over1 = rim, Over1Mod = c == UiTheme.Gold ? Colors.White : c };
        else sb = UiTheme.Box(new Color(0, 0, 0, 0), c, px, 4);
        return Outlines[(c, px)] = sb;
    }

    /// <summary>The theme's cell box for the plain cells built here when the slot widget itself cannot be; flat as the last resort.</summary>
    public static StyleBox CellBox(int rarity, Color flatBg, Color flatBorder, int px)
    {
        try
        {
            var sb = UiTheme.SlotBox(rarity, false, false);
            if (sb != null) return sb;
        }
        catch (Exception e) { Log.Error("ui", "theme slot box failed", e); }
        return UiTheme.Box(flatBg, flatBorder, px, 4);
    }

    private static StyleBox Dot(Color c)
    {
        if (!Dots.TryGetValue(c, out var sb)) Dots[c] = sb = UiTheme.Box(c, c.Darkened(0.45f), 1, 6);
        return sb;
    }

    private static T Fill<T>(T c) where T : Control
    {
        c.MouseFilter = Control.MouseFilterEnum.Ignore;
        c.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return c;
    }

    private static void Corner(Control c, bool right, bool bottom, float w, float h, float inset)
    {
        float ax = right ? 1f : 0f, ay = bottom ? 1f : 0f;
        c.AnchorLeft = ax; c.AnchorRight = ax; c.AnchorTop = ay; c.AnchorBottom = ay;
        c.OffsetLeft = right ? -(w + inset) : inset; c.OffsetRight = right ? -inset : w + inset;
        c.OffsetTop = bottom ? -(h + inset) : inset; c.OffsetBottom = bottom ? -inset : h + inset;
    }

    // ───────────────────────── cells ─────────────────────────

    /// <summary>
    /// An item cell: the theme's slot widget inside a holder that carries our markers, registered for hover.
    /// Clicks always report the item the cell was built for. The widget's own tooltip is switched off (its text
    /// function answers null): the board shows the card, so the comparison and the extra lines cannot be replaced
    /// by a second, plainer tooltip when the pointer re-enters the cell.
    /// </summary>
    public static SlotView Slot(SlotBoard board, Item it, int size, Action<Item> onLeft, Action<Item> onRight,
        Func<Item, string> extra = null, bool compare = true, Control clip = null)
    {
        var holder = new Control { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore };
        var view = new SlotView { Node = holder, Item = it, Extra = extra, Compare = compare, Clip = clip };
        void Left() => Log.Guard("ui", () => onLeft?.Invoke(it));
        void Right() => Log.Guard("ui", () => onRight?.Invoke(it));
        Control inner = null;
        try { inner = UiTheme.ItemSlot(it, size, _ => Left(), _ => Right(), _ => null); }
        catch (Exception e) { Log.Error("ui", $"theme item slot failed for {it?.Name}", e); }
        inner ??= FallbackSlot(it, Left, Right);
        holder.AddChild(inner);
        inner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        try { SocketPips(holder, inner, it, size); }
        catch (Exception e) { Log.Error("ui", $"socket marks failed for {it?.Name}", e); }
        try { if (Unusable(it)) { NoUseMark(holder, it); view.Glow = false; } }
        catch (Exception e) { Log.Error("ui", $"class mark failed for {it?.Name}", e); }
        board?.Add(view);
        return view;
    }

    public static readonly Color NoUseColor = new(0.86f, 0.22f, 0.16f);

    /// <summary>A weapon or off-hand the hero's class cannot use at all (any base outside its loadout, another class's unique).</summary>
    public static bool Unusable(Item it)
    {
        var h = Game.I?.Hero;
        return h != null && it != null && it.Kind is ItemKind.Weapon or ItemKind.OffHand && !ItemDb.UsableBy(it, h.Class);
    }

    /// <summary>
    /// The class-incompatible mark on a cell: a red-tinted rim and a ⊘ in the top-right corner (top-left when the
    /// masterwork badge holds the top-right). It sits on the frame, over the icon image, which stays as it is.
    /// </summary>
    public static void NoUseMark(Control holder, Item it)
    {
        holder.AddChild(Fill(new ColorRect { Color = new Color(0.45f, 0.04f, 0.02f, 0.22f) }));
        Outline(holder, NoUseColor, 2);
        var l = UiTheme.Outlined(UiTheme.Label("⊘", UiTheme.FsCaption, NoUseColor), 4);
        l.MouseFilter = Control.MouseFilterEnum.Ignore;
        l.AddThemeConstantOverride("line_spacing", 0);
        holder.AddChild(l);
        bool right = it.Upgrade <= 0;
        l.AnchorLeft = right ? 1f : 0f; l.AnchorRight = l.AnchorLeft; l.AnchorTop = 0f; l.AnchorBottom = 0f;
        l.GrowHorizontal = right ? Control.GrowDirection.Begin : Control.GrowDirection.End;
        l.OffsetLeft = right ? -4f : 4f; l.OffsetRight = l.OffsetLeft; l.OffsetTop = -2f; l.OffsetBottom = -2f;
    }

    /// <summary>Plain cell used only if the theme's slot widget cannot be built: rarity frame, tinted icon, clicks.</summary>
    private static Control FallbackSlot(Item it, Action left, Action right)
    {
        var rc = Palette.Rarity(it.Rarity);
        var p = new Panel { MouseFilter = Control.MouseFilterEnum.Stop };
        p.AddThemeStyleboxOverride("panel", CellBox((int)it.Rarity, new Color(0.07f, 0.065f, 0.06f, 0.95f), rc.Darkened(0.25f), 2));
        var tex = Icons.ForItem(it);
        var icon = new TextureRect
        {
            Texture = tex, Modulate = Icons.Tint(tex, rc), MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        p.AddChild(icon);
        icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 6);
        p.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true } mb) return;
            if (mb.ButtonIndex == MouseButton.Left) left();
            else if (mb.ButtonIndex == MouseButton.Right) right();
        };
        return p;
    }

    /// <summary>An empty cell (the theme's recessed slot), optionally with a faint icon of what belongs there.</summary>
    public static Control Empty(int size, Texture2D ghost = null)
    {
        Control p = null;
        try { p = UiTheme.ItemSlot(null, size, null, null, null); }
        catch (Exception e) { Log.Error("ui", "theme empty slot failed", e); }
        if (p == null)
        {
            _emptyBox ??= CellBox(-1, new Color(0.05f, 0.048f, 0.045f, 0.82f), new Color(0.24f, 0.22f, 0.2f, 0.9f), 1);
            var panel = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
            panel.AddThemeStyleboxOverride("panel", _emptyBox);
            p = panel;
        }
        p.CustomMinimumSize = new Vector2(size, size);
        if (ghost != null)
        {
            var t = new TextureRect
            {
                Texture = ghost, Modulate = new Color(1, 1, 1, 0.12f), MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            p.AddChild(t);
            t.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, Math.Max(4, size / 7));
        }
        return p;
    }

    /// <summary>An empty cell that waits for a gem: the theme's recessed slot with the forged gem mount in its middle (at its own size).</summary>
    public static Control EmptySocket(int size)
    {
        var p = Empty(size);
        LookArt();
        if (_mount == null) return p;
        var m = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        m.Draw += () => UiArt.SpriteCentered(m, _mount, m.Size * 0.5f);
        p.AddChild(m);
        m.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return p;
    }

    public static void Outline(Control holder, Color c, int px = 2)
    {
        var p = new Panel();
        p.AddThemeStyleboxOverride("panel", OutlineBox(c, px));
        holder.AddChild(Fill(p));
    }

    public static void Dimmed(Control holder, float alpha = 0.62f) =>
        holder.AddChild(Fill(new ColorRect { Color = new Color(0, 0, 0, alpha) }));

    /// <summary>
    /// The "廢" mark of a junk item. The four corners of a cell are shared out: masterwork badge top-right and socket
    /// pips bottom-left (both drawn by the theme's slot), the new-item dot top-left, this mark bottom-right. The
    /// label is pinned to its corner and takes whatever room its caption-size glyph needs, at any text scale.
    /// </summary>
    public static void JunkMark(Control holder) => MarkBadge(holder, "收", RecycleColor, true);

    /// <summary>Bag marks: 收 (回收 at the smith), 棄 (丟棄), and 鎖 (locked; never marked, so it has the corner to itself).</summary>
    public static void BagBadges(Hero h, Item it, Control holder)
    {
        var m = h.MarkOf(it);
        if (m == BagMark.Recycle) MarkBadge(holder, "收", RecycleColor, true);
        else if (m == BagMark.Discard) MarkBadge(holder, "棄", JunkColor, true);
        if (h.InSet(it)) MarkBadge(holder, "組", SetColor, false);           // part of a gear set: protected like a locked item
        else if (it.Locked) MarkBadge(holder, "鎖", LockColor, false);
    }

    public static readonly Color SetColor = new(0.55f, 0.8f, 0.95f);

    public static readonly Color RecycleColor = new(0.95f, 0.66f, 0.3f), LockColor = new(0.86f, 0.8f, 0.62f);

    /// <summary>One glyph in the bottom-right corner of a cell, over a dark veil when <paramref name="veiled"/> (marked to go).</summary>
    public static void MarkBadge(Control holder, string glyph, Color color, bool veiled)
    {
        if (veiled)
        {
            var veil = Fill(new ColorRect { Color = new Color(0.08f, 0.02f, 0.02f, 0.5f) });
            holder.AddChild(veil);
            // the veil darkens the cell's floor and glyph; the forged rim around them stays as it is
            if (UiTheme.Art) veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 4);
        }
        var l = UiTheme.Label(glyph, UiTheme.FsCaption, color);
        l.MouseFilter = Control.MouseFilterEnum.Ignore;
        l.AddThemeConstantOverride("line_spacing", 0);
        holder.AddChild(l);
        // anchored to the bottom-right corner with no size of its own: the label grows up and to the left to the
        // size of its text (the line box already carries 3–4 px of air under the glyph, so the bottom inset is small)
        l.AnchorLeft = 1f; l.AnchorRight = 1f; l.AnchorTop = 1f; l.AnchorBottom = 1f;
        l.GrowHorizontal = Control.GrowDirection.Begin;
        l.GrowVertical = Control.GrowDirection.Begin;
        l.OffsetLeft = -5f; l.OffsetRight = -5f; l.OffsetTop = -1f; l.OffsetBottom = -1f;
    }

    /// <summary>The ember dot of a freshly acquired item, in the top-left corner (the top-right one belongs to the masterwork badge).</summary>
    public static Control NewDot(Control holder)
    {
        LookArt();
        if (_bullet != null)
        {
            // an ember stone in its gold setting, riveted over the cell's corner
            var stud = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
            stud.Draw += () => UiArt.Sprite(stud, _bullet, new Rect2(Vector2.Zero, stud.Size));
            holder.AddChild(stud);
            Corner(stud, false, false, 18, 18, -1);
            return stud;
        }
        var p = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", Dot(Palette.Ember));
        holder.AddChild(p);
        Corner(p, false, false, 10, 10, 4);
        return p;
    }

    /// <summary>
    /// A cell with one line of text under it (prices) at caption size. <paramref name="width"/> is the DESIGN width
    /// kept for the caption: it grows with the text scale, so the column can become wider than the cell. A caption
    /// that still does not fit ends in an ellipsis.
    /// </summary>
    public static Control Captioned(Control cell, string text, Color c, int width)
    {
        var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        v.AddThemeConstantOverride("separation", 1);
        cell.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        v.AddChild(cell);
        var l = UiTheme.Trimmed(UiTheme.Label(text, UiTheme.FsCaption, c), false);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        UiTheme.MinSize(l, width, 0);
        l.MouseFilter = Control.MouseFilterEnum.Ignore;
        v.AddChild(l);
        return v;
    }

    // ───────────────────────── layout pieces ─────────────────────────

    public static GridContainer Grid(int columns, int sep = 4)
    {
        var g = new GridContainer { Columns = columns, MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        g.AddThemeConstantOverride("h_separation", sep);
        g.AddThemeConstantOverride("v_separation", sep);
        return g;
    }

    /// <summary>The forty bag cells. <paramref name="decorate"/> runs for every item cell (selection frames, markers).</summary>
    public static GridContainer BagGrid(SlotBoard board, Hero h, int cell, Action<Item> onLeft, Action<Item> onRight,
        Func<Item, string> extra = null, Action<Item, SlotView> decorate = null, bool compare = true)
    {
        var g = Grid(10);
        var bag = h.D.Bag;
        for (int i = 0; i < Hero.BagSize; i++)
        {
            if (i >= bag.Count) { g.AddChild(Empty(cell)); continue; }
            var it = bag[i];
            var v = Slot(board, it, cell, onLeft, onRight, extra, compare);
            BagBadges(h, it, v.Node);
            decorate?.Invoke(it, v);
            g.AddChild(v.Node);
        }
        return g;
    }

    public static ScrollContainer Scroll(Control content, Vector2 minSize)
    {
        var sc = new ScrollContainer { CustomMinimumSize = minSize, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        sc.AddChild(content);
        return sc;
    }

    /// <summary>
    /// A theme button (it clicks and guards its callback by itself) that never takes keyboard focus.
    /// <paramref name="size"/> is a DESIGN font size: UiTheme.Button sends it through UiTheme.Fs — pass a plain number.
    /// </summary>
    public static Button Btn(string text, Action onPress, int size = UiTheme.FsRow)
    {
        var b = UiTheme.Button(text, onPress, size);
        b.FocusMode = Control.FocusModeEnum.None;
        return b;
    }

    /// <summary>Colours a button as the chosen (gold) or an idle (dim) option of a group.</summary>
    public static void Tone(Button b, bool active)
    {
        var c = active ? UiTheme.Gold : UiTheme.Dim;
        b.AddThemeColorOverride("font_color", c);
        b.AddThemeColorOverride("font_focus_color", c);
        b.AddThemeColorOverride("font_hover_color", active ? UiTheme.Gold : UiTheme.Text);
        b.AddThemeColorOverride("font_pressed_color", UiTheme.Gold);
    }

    /// <summary>
    /// A row of option buttons, the current one in gold. <paramref name="size"/> is a DESIGN font size (it reaches
    /// UiTheme.Fs through <see cref="Btn"/>) and <paramref name="minWidth"/> a design width: both grow with the text scale.
    /// </summary>
    public static HBoxContainer Tabs(string[] names, int current, Action<int> pick, int size = UiTheme.FsRow, int minWidth = 120)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var b = Btn(names[i], () => pick(idx), size);
            UiTheme.MinSize(b, minWidth, 0);
            UiTheme.TabTone(b, i == current);
            box.AddChild(b);
        }
        return box;
    }

    /// <summary>Gold and the three crafting materials, each with a short explanation on hover.</summary>
    public static HBoxContainer Money(SlotBoard board, SaveData d)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", 20);
        box.AddChild(Purse(board, "金幣", d.Gold, Palette.Gold, "金幣\n" + Dim("買賣、精鑄、淬火、附魔與銘刻都少不了它。")));
        box.AddChild(Purse(board, Crafting.ScrapName, d.Scrap, ScrapColor, Crafting.ScrapName + "\n" + Dim("拆解任何裝備都能得到。精鑄與淬火的基本材料。")));
        box.AddChild(Purse(board, Crafting.DustName, d.Dust, DustColor, Crafting.DustName + "\n" + Dim("拆解稀有以上的裝備所得。用於附魔、開鑿插槽、淬火與高階精鑄。")));
        box.AddChild(Purse(board, Crafting.CoreName, d.Cores, CoreColor, Crafting.CoreName + "\n" + Dim("傳奇與獨特裝備拆解後留下的餘火。用於銘刻威能與最後幾級精鑄。")));
        return box;
    }

    private static Control Purse(SlotBoard board, string name, long value, Color c, string tip)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 6);
        if (SocketArt) h.AddChild(Stone(c, 18f));
        else
        {
            var dot = new Panel { CustomMinimumSize = new Vector2(12, 12), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
            dot.AddThemeStyleboxOverride("panel", Dot(c));
            h.AddChild(dot);
        }
        // caption-size name, row-size amount: bottom-aligned so the two sizes share a baseline
        var label = UiTheme.Label(name, UiTheme.FsCaption, UiTheme.Dim);
        label.VerticalAlignment = VerticalAlignment.Bottom;
        h.AddChild(label);
        var amount = UiTheme.Label(Crafting.Num(value), UiTheme.FsRow, c);
        amount.VerticalAlignment = VerticalAlignment.Bottom;
        h.AddChild(amount);
        board?.Add(new SlotView { Node = h, Text = tip, Glow = false, Compare = false });
        return h;
    }

    public static void Clear(Node n)
    {
        if (n == null) return;
        foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); }
    }
}

/// <summary>
/// Panel "inventory": character sheet on the left, paper doll (worn gear around a live portrait of the hero) and bag on the right.
/// Left click equips or swaps, right click on worn gear unequips, Shift+left click marks junk, right click on a gem
/// then a click on gear with a free socket sets the gem. New items carry an ember dot until hovered.
/// </summary>
public partial class InventoryUi : UiPanel
{
    public override string Id => "inventory";

    // Design sizes at text scale 1.0. SheetW is a minimum width that grows with the text scale (UiTheme.MinSize);
    // RightW is the width of the bag's well and of the column it stands in, the cells keep their size, and the paper
    // doll (at least DollW × DollH) spreads over the width of its panel. The frame is at least FrameW × FrameH and
    // grows with its content: about 1196 × 1003 at 標準 and 大 (the doll takes the height the smaller text leaves,
    // see DollGrow) and 1316 × 1000 at the largest text size, which leaves the crest its 34 px above the frame on a
    // 1080-line screen.
    // The niche holds the live portrait (HeroPortrait3D): NicheW × NicheH between the slot captions, which are
    // DollTagW wide (two glyphs are 52 px at the largest text size).
    private const int FrameW = 1196, FrameH = 900, SheetW = 400, RightW = 696, DollW = 676, DollH = 338, DollCell = 68, BagCell = 64, DollTagW = 60;
    private const float DollEdge = 10f, DollPad = 10f, NicheW = 372f, NicheTop = 4f, NicheH = 252f, NicheRim = 3f;
    // The frame is as tall as the screen allows at the largest text size. At the smaller ones the text rows leave
    // height over, and the doll takes it: the niche (and the hero in it) grows by this much at 標準, half of it at 大,
    // nothing at 特大; the side cells spread over the taller doll and the weapon cell stays under the niche.
    private const float DollGrowMax = 48f;
    private static float DollGrow => MathF.Round(DollGrowMax * Mathf.Clamp((1.3f - UiTheme.TextScale) / 0.3f, 0f, 1f));
    private const float SmithRange = 9f;
    private const string IdleHint = "左鍵：裝備　右鍵：卸下或選取寶石　Shift＋左鍵／R：標記回收　X：標記丟棄　L：鎖定　F：篩選";
    private static readonly string[] SheetTabs = { "總覽", "攻勢", "守禦", "巧用", "裝備組" };

    // paper-doll cells: column (-1 = along the left edge, 1 = along the right edge, 0 = the middle: the two hands side by
    // side under the niche, main hand left of centre, off hand right of it) and top, in the doll's own coordinates
    private const float HandGap = 16f;
    private static readonly Color LockedRim = new(0.42f, 0.2f, 0.16f);
    private static readonly (EquipSlot slot, int col, float y)[] DollLayout =
    {
        (EquipSlot.Helm, -1, 8), (EquipSlot.Chest, -1, 90), (EquipSlot.Gloves, -1, 172), (EquipSlot.Pants, -1, 254),
        (EquipSlot.Amulet, 1, 8), (EquipSlot.Ring1, 1, 90), (EquipSlot.Ring2, 1, 172), (EquipSlot.Boots, 1, 254),
        (EquipSlot.MainHand, 0, 262), (EquipSlot.OffHand, 0, 262),
    };

    private readonly SlotBoard _board = new();
    private bool _built, _open, _dirty;
    private Hero _hero;
    private PanelContainer _frame;
    private Label _name, _class, _who, _msg, _bagCount;
    private HBoxContainer _tabBox, _moneyBox;
    private VBoxContainer _rows;
    private ScrollContainer _sheetScroll;
    private Control _doll, _cells, _nicheRim, _bagBox;
    private HeroPortrait3D _portrait;           // the hero, alive, in the niche between the two columns of cells
    private Button _junkBtn, _discardBtn, _filterBtn;
    private PanelContainer _dollFrame, _filterFrame;
    private VBoxContainer _filterBody;
    private ScrollContainer _filterScroll;
    private bool _filterOn;
    private readonly BagFilter _filter = new();
    private int _tab;
    private Item _gemPick;
    private float _msgT, _dt;
    private int _faults, _settle;
    private UiArt.Piece _niche, _nicheField;     // the recess the figure stands in (null without the art)

    // ───────────────────────── life cycle ─────────────────────────

    public InventoryUi()
    {
        Name = "InventoryUi";
        ItemMarks.EnsureHooked();
    }

    public override void _Ready()
    {
        base._Ready();
        Log.Guard("ui", EnsureBuilt);
    }

    // the manager takes a closed panel out of the tree: nothing may stay subscribed or parked in a freed cell
    public override void _ExitTree()
    {
        base._ExitTree();
        _open = false;
        Hook(null);
        _portrait?.Unload();
        _board.Dispose();
    }

    public override void OnOpen(object arg)
    {
        EnsureBuilt();
        _open = true;
        _gemPick = null;
        _msgT = 0;
        _faults = 0;
        Hook(Game.I?.Hero);
        ItemMarks.Track(_hero);
        _hero?.PruneJunk();
        // the vendor panels bring their own bag view and need the room: they win
        if (Game.I?.Ui != null && Game.I.Ui.IsOpen("vendor"))
            Callable.From(() => Game.I?.Ui?.Close("inventory")).CallDeferred();
        Log.Guard("ui", Rebuild);
    }

    public override void OnClose()
    {
        _open = false;
        _gemPick = null;
        _board.Clear();
        Hook(null);
        _portrait?.Unload();        // nothing is rendered and no model is kept while the panel is away
    }

    public override void Refresh() => _dirty = true;

    private void Hook(Hero h)
    {
        if (ReferenceEquals(h, _hero)) return;
        if (_hero != null) _hero.Changed -= OnHeroChanged;
        _hero = h;
        if (_hero != null) _hero.Changed += OnHeroChanged;
    }

    private void OnHeroChanged() => _dirty = true;

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!_built) return;
        _dt = (float)delta;
        try { Tick(); }
        catch (Exception e)
        {
            // never let a fault escape the frame, and never let it repeat sixty times a second in the log
            _dirty = false;
            if (++_faults <= 5) Log.Error("ui", "inventory panel frame failed", e);
        }
    }

    private void Tick()
    {
        var h = Game.I?.Hero;
        ItemMarks.Track(h);
        if (!_open) return;
        if (!ReferenceEquals(h, _hero)) { Hook(h); _dirty = true; }
        if (_msgT > 0f)
        {
            _msgT -= _dt;
            if (_msgT <= 0f) ShowIdleHint();
        }
        if (_dirty) Rebuild();
        if (_settle > 0) { _settle--; return; }     // rebuilt cells get their place at the end of the frame: hover resumes on the next
        _board.Poll(this);
    }

    // ───────────────────────── static layout ─────────────────────────

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        _board.OnHover = OnHover;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _frame = UiTheme.Frame(true);
        _frame.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_frame);
        _frame.AnchorLeft = 1f; _frame.AnchorRight = 1f; _frame.AnchorTop = 0.5f; _frame.AnchorBottom = 0.5f;
        _frame.OffsetLeft = -(FrameW + 28); _frame.OffsetRight = -28;
        _frame.OffsetTop = -FrameH * 0.5f; _frame.OffsetBottom = FrameH * 0.5f;
        _frame.GrowHorizontal = GrowDirection.Begin;
        _frame.GrowVertical = GrowDirection.Both;

        // the heavy frame brings its own padding (UiTheme.WinPadX × WinPadY)
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        _frame.AddChild(root);
        if (UiTheme.Art) { _niche = UiArt.P("well"); _nicheField = UiArt.Field("well"); }

        // header
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 14);
        head.AddChild(UiTheme.Label("行囊", UiTheme.FsTitle, UiTheme.Gold, true));
        _who = UiTheme.Trimmed(UiTheme.Label("", UiTheme.FsRow, UiTheme.Dim), false);
        _who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _who.HorizontalAlignment = HorizontalAlignment.Right;
        _who.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(_who);
        var close = ItemUi.Btn("關閉", () => Game.I?.Ui?.Close("inventory"), UiTheme.FsRow);
        close.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(close);
        root.AddChild(head);
        root.AddChild(UiTheme.Rule());

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 16);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(body);

        // character sheet: the column widens with the text so a stat name and its value keep sharing one line;
        // the stat list scrolls in a well under its tabs, inside whatever height the right-hand column gives the body
        var sheet = new VBoxContainer();
        sheet.AddThemeConstantOverride("separation", 6);
        _name = UiTheme.Trimmed(UiTheme.Label("", 30, UiTheme.Text, true));
        sheet.AddChild(_name);
        _class = UiTheme.Wrapped(UiTheme.Label("", UiTheme.FsRow, UiTheme.Dim));      // level, class, paragon, spec: two lines when it must
        sheet.AddChild(_class);
        _setStrip = new HBoxContainer();             // 裝備組 strip (SYSTEMS): six sets, click to load, right-click for save / rename / clear
        _setStrip.AddThemeConstantOverride("separation", 4);
        sheet.AddChild(_setStrip);
        _tabBox = new HBoxContainer();
        sheet.AddChild(_tabBox);
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 2);
        _sheetScroll = UiTheme.MinSize(ItemUi.Scroll(_rows, Vector2.Zero), SheetW, 300);
        var sheetWell = ItemUi.Well(_sheetScroll);
        sheetWell.SizeFlagsVertical = SizeFlags.ExpandFill;
        sheet.AddChild(sheetWell);
        body.AddChild(sheet);

        // paper doll, purse, bag. The column is as wide as the bag's well at every text size (the purse row and
        // the two-line hint fit in it at the largest one; should a purse ever need more, the column and both boxes
        // widen with it). The doll stands in a sub-panel as wide as the column — its side cells keep to the panel's
        // edges, the hero's live portrait in its niche and the weapon to the middle — and the bag lies in a well of the
        // same width. Bottom to top inside the doll: the niche's field, the portrait, the niche's rim, the cells.
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(RightW, 0) };
        right.AddThemeConstantOverride("separation", 8);
        var dollFrame = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        dollFrame.AddThemeStyleboxOverride("panel", UiTheme.Art
            ? UiTheme.ArtBox("panel", DollPad, DollPad, "soot", 3f)
            : new StyleBoxEmpty { ContentMarginLeft = DollPad, ContentMarginRight = DollPad, ContentMarginTop = DollPad, ContentMarginBottom = DollPad });
        _doll = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _doll.Draw += DrawNiche;
        dollFrame.AddChild(_doll);
        _portrait = new HeroPortrait3D();
        _doll.AddChild(_portrait);
        LayoutDoll();
        _nicheRim = new Control { Name = "NicheRim", MouseFilter = MouseFilterEnum.Ignore };
        _nicheRim.Draw += DrawNicheRim;
        _doll.AddChild(_nicheRim);
        _nicheRim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _cells = new Control { Name = "Cells", MouseFilter = MouseFilterEnum.Ignore };
        _doll.AddChild(_cells);
        _cells.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        right.AddChild(dollFrame);
        _dollFrame = dollFrame;
        // the bag filter (行囊篩選) takes the paper doll's place while it is open (F or the 篩選 button)
        _filterFrame = new PanelContainer { Visible = false };
        _filterFrame.AddThemeStyleboxOverride("panel", UiTheme.Art
            ? UiTheme.ArtBox("panel", DollPad, DollPad, "soot", 3f)
            : new StyleBoxEmpty { ContentMarginLeft = DollPad, ContentMarginRight = DollPad, ContentMarginTop = DollPad, ContentMarginBottom = DollPad });
        _filterBody = new VBoxContainer();
        _filterBody.AddThemeConstantOverride("separation", 6);
        // it scrolls inside exactly the doll's height, so the panel never grows (larger text sizes scroll instead)
        _filterScroll = ItemUi.Scroll(_filterBody, Vector2.Zero);
        _filterFrame.AddChild(_filterScroll);
        right.AddChild(_filterFrame);
        _moneyBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        right.AddChild(_moneyBox);
        _bagBox = new MarginContainer();
        right.AddChild(_bagBox);

        var tools = new HBoxContainer();
        tools.AddThemeConstantOverride("separation", 10);
        tools.AddChild(ItemUi.Btn("整理", SortBag, UiTheme.FsRow));
        _filterBtn = ItemUi.Btn("篩選", ToggleFilter, UiTheme.FsRow);
        tools.AddChild(_filterBtn);
        _junkBtn = ItemUi.Btn("回收", RecycleMarked, UiTheme.FsRow);
        tools.AddChild(_junkBtn);
        _discardBtn = ItemUi.Btn("丟棄", DiscardMarked, UiTheme.FsRow);
        tools.AddChild(_discardBtn);
        _bagCount = UiTheme.Trimmed(UiTheme.Label("", UiTheme.FsCaption, UiTheme.Dim), false);
        _bagCount.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _bagCount.HorizontalAlignment = HorizontalAlignment.Right;
        _bagCount.VerticalAlignment = VerticalAlignment.Center;
        tools.AddChild(_bagCount);
        right.AddChild(tools);

        // hint and message line: room for two lines is always kept, so a long message wraps (and ends in an ellipsis
        // only past the second line) and the frame does not change height when the text does
        _msg = UiTheme.Wrapped(UiTheme.Label(IdleHint, UiTheme.FsCaption, UiTheme.Dim));
        _msg.MaxLinesVisible = 2;
        _msg.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        UiTheme.MinSize(_msg, 0, 62);
        right.AddChild(_msg);
        body.AddChild(right);
    }

    // ───────────────────────── rebuild ─────────────────────────

    private void Rebuild()
    {
        _dirty = false;
        if (!_built) return;
        _settle = 1;
        _board.Clear();
        var h = _hero;
        if (h == null) return;
        if (_gemPick != null && !h.InBag(_gemPick)) _gemPick = null;
        var d = h.D;
        _who.Text = $"{(string.IsNullOrEmpty(d.Name) ? "持燼者" : d.Name)}　等級 {d.Level} {Names.Class(h.Class)}";
        BuildSheet(h);
        BuildSetStrip(h);
        GearSets.RefreshHud();
        BuildDoll(h);
        _portrait?.SetHero(h);      // model, weapon in hand and scale follow the hero; a no-op when nothing visible changed
        ItemUi.Clear(_moneyBox);
        _moneyBox.AddChild(ItemUi.Money(_board, d));
        ItemUi.Clear(_bagBox);
        _bagBox.AddChild(ItemUi.Well(ItemUi.BagGrid(_board, h, BagCell, OnBagLeft, OnBagRight, BagHint, DecorateBag)));
        _bagCount.Text = $"行囊 {d.Bag.Count} / {Hero.BagSize}";
        int junk = h.RecycleItems().Count, toss = h.DiscardItems().Count;
        _junkBtn.Text = junk > 0 ? $"回收（{junk}）" : "回收";
        ItemUi.Tone(_junkBtn, junk > 0 && AtSmith());
        _discardBtn.Text = toss > 0 ? $"丟棄（{toss}）" : "丟棄";
        ItemUi.Tone(_discardBtn, toss > 0);
        ItemUi.Tone(_filterBtn, _filterOn || _filter.Active);
        _dollFrame.Visible = !_filterOn;
        _filterFrame.Visible = _filterOn;
        if (_filterOn) BuildFilter(h);
        if (_msgT <= 0f) ShowIdleHint();
    }

    private void ShowIdleHint()
    {
        _msgT = 0f;
        if (_msg == null) return;
        if (_gemPick != null)
        {
            _msg.Text = $"鑲嵌「{_gemPick.Name}」：點擊一件有空插槽的裝備（右鍵取消）";
            _msg.AddThemeColorOverride("font_color", ItemUi.PickColor);
        }
        else
        {
            _msg.Text = IdleHint;
            _msg.AddThemeColorOverride("font_color", UiTheme.Dim);
        }
    }

    private void Say(string text, Color? c = null)
    {
        if (_msg == null) return;
        _msg.Text = text;
        _msg.AddThemeColorOverride("font_color", c ?? UiTheme.Text);
        _msgT = 4.5f;
    }

    private void Fail(string why)
    {
        Say(why, Palette.Bad);
        Snd.Ui("ui_error");
    }

    private void OnHover(SlotView v)
    {
        if (v.Item == null || v.NewDot == null) return;
        ItemMarks.See(v.Item);
        if (GodotObject.IsInstanceValid(v.NewDot)) v.NewDot.Visible = false;
        v.NewDot = null;
    }

    // ───────────────────────── paper doll ─────────────────────────

    /// <summary>Height of the doll and place of the portrait for the text size in force (see <see cref="DollGrow"/>).</summary>
    private void LayoutDoll()
    {
        if (_doll == null || _portrait == null) return;
        float grow = DollGrow;
        _doll.CustomMinimumSize = new Vector2(DollW, DollH + grow);
        Pin(_portrait, 0.5f, -NicheW * 0.5f + NicheRim, NicheTop + NicheRim, NicheW - 2f * NicheRim, NicheH + grow - 2f * NicheRim);
        _doll.QueueRedraw();
        _nicheRim?.QueueRedraw();
    }

    public override void OnTextScale()
    {
        base.OnTextScale();
        if (!_built) return;
        LayoutDoll();
        _dirty = true;
    }

    /// <summary>Tooltip head of an empty worn slot: its name and, for the hands, what the class can hold there.</summary>
    private static string HandHint(Hero h, EquipSlot slot)
    {
        string Names_(IEnumerable<string> ids) => string.Join("、", ids.Select(id => ItemDb.BaseOf(id)?.Name ?? id));
        var g = ItemDb.Loadout(h.Class);
        if (slot == EquipSlot.MainHand)
        {
            string s = "主手\n";
            if (g.TwoHand.Length > 0) s += ItemUi.Dim("雙手：" + Names_(g.TwoHand)) + "\n";
            if (g.MainHand.Length > 0) s += ItemUi.Dim("單手：" + Names_(g.MainHand)) + "\n";
            return s;
        }
        if (slot == EquipSlot.OffHand)
        {
            var offs = g.OffHand.Concat(g.Dual).ToArray();
            if (offs.Length == 0) return "副手\n" + ItemUi.Dim(Names.Class(h.Class) + "不使用副手。") + "\n";
            string s = "副手\n";
            if (g.OffHand.Length > 0) s += ItemUi.Dim("副手：" + Names_(g.OffHand)) + "\n";
            if (g.CanDual) s += ItemUi.Dim("雙持：" + Names_(g.Dual)) + "\n";
            return s;
        }
        return ItemUi.SlotName(slot) + "\n";
    }

    private void BuildDoll(Hero h)
    {
        ItemUi.Clear(_cells);
        float grow = DollGrow;
        foreach (var (slot, col, y0) in DollLayout)
        {
            // the side columns spread over the height of the doll; the weapon keeps its place under the niche
            float y = col == 0 ? y0 + grow : y0 + MathF.Round(grow * (y0 - 8f) / 246f);
            var kind = ItemUi.KindOf(slot);
            var it = h.D.Equip[(int)slot];
            Control node;
            if (it != null)
            {
                var v = ItemUi.Slot(_board, it, DollCell, OnWornLeft, OnWornRight, WornHint, false);
                node = v.Node;
                if (_gemPick != null)
                {
                    if (it.Gems.Count < it.Sockets) ItemUi.Outline(node, ItemUi.PickColor);
                    else ItemUi.Dimmed(node);
                }
            }
            else if (slot == EquipSlot.OffHand && h.OffHandBlocked)
            {
                // a two-hand weapon holds both hands: the off hand shows that weapon's ghost, greyed and rimmed as locked
                node = ItemUi.Empty(DollCell, Icons.ForItem(h.MainHand));
                ItemUi.Dimmed(node, 0.3f);
                ItemUi.Outline(node, LockedRim);
                _board.Add(new SlotView { Node = node, Compare = false, Glow = false,
                    Text = "副手（已鎖定）\n" + ItemUi.Dim($"雙手武器「{h.MainHand.Name}」同時佔用兩手。\n換上單手武器後才能使用副手。") });
            }
            else
            {
                node = ItemUi.Empty(DollCell, Icons.ForKind(kind, slot == EquipSlot.MainHand ? (int)h.Class : -1));
                _board.Add(new SlotView { Node = node, Text = HandHint(h, slot) + ItemUi.Dim("尚未裝備"), Compare = false, Glow = false });
            }
            // pinned to the doll's left edge, right edge or middle, so the layout follows the width of the panel
            _cells.AddChild(node);
            float ax = col < 0 ? 0f : col > 0 ? 1f : 0.5f;
            float x = col < 0 ? DollEdge : col > 0 ? -(DollEdge + DollCell)
                : slot == EquipSlot.MainHand ? -DollCell - HandGap * 0.5f : HandGap * 0.5f;
            Pin(node, ax, x, y, DollCell, DollCell);

            // slot name beside the cell, toward the figure: a box as tall as the cell with the text centred in
            // it, so the caption sits level with the cell at every text size (two glyphs are 52 px wide at the largest);
            // the two hands carry theirs on their outer sides
            var tag = UiTheme.Label(ItemUi.SlotName(slot), UiTheme.FsCaption, UiTheme.Dim);
            tag.MouseFilter = MouseFilterEnum.Ignore;
            tag.ClipText = true;
            _cells.AddChild(tag);
            bool rightSide = col > 0 || slot == EquipSlot.MainHand;
            tag.HorizontalAlignment = rightSide ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            tag.VerticalAlignment = VerticalAlignment.Center;
            Pin(tag, ax, rightSide ? x - 8 - DollTagW : x + DollCell + 8, y, DollTagW, DollCell);
        }
        _doll.QueueRedraw();
        _nicheRim.QueueRedraw();
    }

    /// <summary>Places a child of the doll: <paramref name="ax"/> 0 measures x from the doll's left edge, 1 from its right edge, 0.5 from its middle.</summary>
    private static void Pin(Control c, float ax, float x, float y, float w, float h)
    {
        c.AnchorLeft = ax; c.AnchorRight = ax; c.AnchorTop = 0f; c.AnchorBottom = 0f;
        c.OffsetLeft = x; c.OffsetRight = x + w; c.OffsetTop = y; c.OffsetBottom = y + h;
    }

    /// <summary>The recess the hero stands in, between the two columns of cells (design px, in the doll's coordinates).</summary>
    private Rect2 NicheRect => new(MathF.Round(_doll.Size.X * 0.5f) - NicheW * 0.5f, NicheTop, NicheW, NicheH + DollGrow);

    /// <summary>Under the portrait: the darkest field of the interface (what shows while no model is on the stage).</summary>
    private void DrawNiche()
    {
        var recess = NicheRect;
        if (_nicheField != null) UiArt.Tile(_doll, _nicheField, recess.Grow(-2f));
        else _doll.DrawRect(recess, new Color(0.03f, 0.03f, 0.035f, 0.94f));
    }

    /// <summary>Over the portrait: the sunken rim of the recess, so the picture sits inside the frame art.</summary>
    private void DrawNicheRim()
    {
        var recess = NicheRect;
        if (_niche != null) UiArt.Nine(_nicheRim, _niche, recess);
        else _nicheRim.DrawRect(recess, new Color(0.5f, 0.34f, 0.2f, 0.55f), false, 1.5f);
    }

    private string WornHint(Item it) => _gemPick != null ? SocketHint(it) : ItemUi.Dim("右鍵：卸下");

    /// <summary>While a gem is picked: what a click on this piece of gear would do.</summary>
    private string SocketHint(Item target)
    {
        string why = _hero?.WhyCantSocket(_gemPick, target);
        return why == null ? ItemUi.Col(ItemUi.PickColor, "左鍵：嵌入所選的寶石") : ItemUi.Col(Palette.Bad, why);
    }

    private void OnWornLeft(Item it)
    {
        if (_hero == null) return;
        if (_gemPick != null) { TrySocket(it); return; }
        Say("右鍵點擊可卸下這件裝備。");
    }

    private void OnWornRight(Item it)
    {
        var h = _hero;
        if (h == null) return;
        if (_gemPick != null) { CancelGemPick(); return; }
        int idx = Array.IndexOf(h.D.Equip, it);
        if (idx < 0) return;
        string why = h.WhyCantUnequip((EquipSlot)idx);
        if (why != null) { Fail(why); return; }
        if (!h.Unequip((EquipSlot)idx)) { Fail("無法卸下"); return; }
        ItemMarks.See(it);
        Snd.Ui(ItemUi.EquipSound(it));
        Log.Info("inv", $"unequipped {it.Name} from {(EquipSlot)idx}");
    }

    // ───────────────────────── bag ─────────────────────────

    // ───────────────────────── gear sets (裝備組) ─────────────────────────

    private HBoxContainer _setStrip;
    private PopupMenu _setMenu;
    private int _setMenuFor = -1;

    private static string Stance(Hero h) =>
        h.OffHandBlocked ? "雙手" : h.DualWielding ? "雙持" : h.OffHand != null ? "單手＋副手" : h.Weapon != null ? "單手" : "徒手";

    /// <summary>Tooltip of a set: its key, its items (⚠ for the missing ones) and what it stores.</summary>
    private static string SetTip(Hero h, int i)
    {
        var s = h.SetAt(i);
        var sb = new StringBuilder();
        sb.Append($"{h.SetName(i)}　{ItemUi.Dim($"按鍵 {GearSets.KeyName(i)}")}{(h.D.SetActive == i ? "　" + ItemUi.Col(UiTheme.Gold, "使用中") : "")}\n");
        if (s.Empty) return sb + ItemUi.Dim("尚未儲存。右鍵：儲存目前裝備為此組");
        var missing = new HashSet<string>(h.MissingOf(i, GearSets.InTown));
        foreach (var slot in ItemUi.WornOrder)
        {
            string uid = s.Uids[(int)slot];
            if (uid == null) continue;
            string name = s.ItemNames[(int)slot] ?? "?";
            bool gone = missing.Contains(name) && h.MissingOf(i, true).Contains(name);
            bool away = missing.Contains(name) && !gone;
            sb.Append(ItemUi.Dim(ItemUi.SlotName(slot) + "：") + (gone ? ItemUi.Col(Palette.Bad, "⚠ " + name + "（找不到）") : away ? ItemUi.Col(Palette.Ember, "⚠ " + name + "（在儲藏箱）") : name) + "\n");
        }
        sb.Append(ItemUi.Dim("也保存技能列與專精。左鍵：載入　右鍵：儲存／改名／清除"));
        return sb.ToString();
    }

    private void BuildSetStrip(Hero h)
    {
        ItemUi.Clear(_setStrip);
        var cap = UiTheme.Label("裝備組", UiTheme.FsCaption, UiTheme.Dim);
        cap.VerticalAlignment = VerticalAlignment.Center;
        _setStrip.AddChild(cap);
        for (int i = 0; i < Hero.SetCount; i++)
        {
            int k = i;
            var s = h.SetAt(i);
            Item main = null;
            if (!s.Empty)
            {
                string uid = s.Uids[(int)EquipSlot.MainHand] ?? s.Uids[(int)EquipSlot.OffHand];
                main = uid == null ? null : h.D.Equip.Concat(h.D.Bag).Concat(h.D.Stash).FirstOrDefault(x => x != null && x.Uid == uid);
            }
            bool active = h.D.SetActive == i, warn = !s.Empty && h.MissingOf(i, GearSets.InTown).Count > 0;
            var cell = new Control { CustomMinimumSize = new Vector2(40, 40), MouseFilter = MouseFilterEnum.Stop };
            var frame = new Panel { MouseFilter = MouseFilterEnum.Ignore };
            frame.AddThemeStyleboxOverride("panel", ItemUi.CellBox(main != null ? (int)main.Rarity : -1, new Color(0.07f, 0.065f, 0.06f, 0.95f),
                active ? UiTheme.Gold : new Color(0.35f, 0.32f, 0.28f), active ? 2 : 1));
            frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            cell.AddChild(frame);
            if (main != null)
            {
                var icon = new TextureRect { Texture = Icons.ForItem(main), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
                icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 4);
                cell.AddChild(icon);
            }
            var num = UiTheme.Outlined(UiTheme.Label(warn ? $"{i + 1}⚠" : (i + 1).ToString(), UiTheme.FsCaption, warn ? Palette.Ember : active ? UiTheme.Gold : UiTheme.Text), 4);
            num.MouseFilter = MouseFilterEnum.Ignore;
            num.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, 1);
            num.GrowHorizontal = GrowDirection.Begin; num.GrowVertical = GrowDirection.Begin;
            cell.AddChild(num);
            cell.GuiInput += e => Log.Guard("ui", () =>
            {
                if (e is not InputEventMouseButton mb || !mb.Pressed) return;
                if (mb.ButtonIndex == MouseButton.Left) LoadSetClick(k);
                else if (mb.ButtonIndex == MouseButton.Right) OpenSetMenu(k);
            });
            _setStrip.AddChild(cell);
            _board.Add(new SlotView { Node = cell, Text = SetTip(h, i), Glow = true, Compare = false });
        }
    }

    private void LoadSetClick(int i)
    {
        var h = _hero;
        if (h == null) return;
        if (h.SetAt(i).Empty) { Say($"「{h.SetName(i)}」尚未儲存裝備。右鍵：儲存目前裝備為此組。"); return; }
        if (GearSets.Switch(i, false)) { Say($"已切換：{h.SetName(i)}", Palette.Good); _dirty = true; }
        else Fail("無法切換裝備組（行囊空間或冷卻，詳見畫面上方訊息）");
    }

    private void OpenSetMenu(int i)
    {
        var h = _hero;
        if (h == null) return;
        if (_setMenu == null || !IsInstanceValid(_setMenu))
        {
            _setMenu = new PopupMenu();
            _setMenu.AddThemeFontSizeOverride("font_size", UiTheme.Fs(UiTheme.FsRow));
            _setMenu.IdPressed += id => Log.Guard("ui", () => SetMenuPick((int)id));
            AddChild(_setMenu);
        }
        _setMenu.Clear();
        _setMenu.AddItem("儲存目前裝備為此組", 0);
        _setMenu.AddItem("載入", 1);
        _setMenu.AddItem("重新命名", 2);
        _setMenu.AddItem("清除", 3);
        _setMenu.SetItemDisabled(1, h.SetAt(i).Empty);
        _setMenu.SetItemDisabled(3, h.SetAt(i).Empty);
        _setMenuFor = i;
        _setMenu.Position = (Vector2I)GetViewport().GetMousePosition();
        _setMenu.Popup();
    }

    private void SetMenuPick(int id)
    {
        var h = _hero; int i = _setMenuFor;
        if (h == null || i < 0) return;
        switch (id)
        {
            case 0: SaveSetClick(i); break;
            case 1: LoadSetClick(i); break;
            case 2: RenameSetClick(i); break;
            case 3:
                Game.I?.Ui?.Confirm($"清除「{h.SetName(i)}」？\n裝備本身不會被丟棄，只是不再屬於這個裝備組（也不再受保護）。", () => Log.Guard("ui", () => { h.ClearSet(i); GearSets.RefreshHud(); Say($"已清除「{h.SetName(i)}」。"); }), "清除", "取消");
                break;
        }
    }

    private void SaveSetClick(int i)
    {
        var h = _hero;
        if (h == null) return;
        void Go() { h.SaveSet(i); GearSets.RefreshHud(); Snd.Ui("ui_click"); Say($"已將目前裝備存為「{h.SetName(i)}」（按 {GearSets.KeyName(i)} 載入）。", Palette.Good); }
        if (!h.SetAt(i).Empty && h.D.SetActive != i && Game.I?.Ui != null)
            Game.I.Ui.Confirm($"以目前裝備覆蓋「{h.SetName(i)}」？", () => Log.Guard("ui", Go), "覆蓋", "取消");
        else Go();
    }

    private void RenameSetClick(int i)
    {
        var h = _hero;
        if (h == null || Game.I?.Ui == null) return;
        Game.I.Ui.Prompt("裝備組名稱（最多 12 字）", h.SetName(i), name => Log.Guard("ui", () => { h.RenameSet(i, name); GearSets.RefreshHud(); _dirty = true; }));
    }

    /// <summary>The 裝備組 tab of the sheet: every set with its stance and its numbers as if worn, against what is worn now.</summary>
    private void SetsTab(Hero h)
    {
        Head("裝備組");
        _rows.AddChild(UiTheme.Wrapped(UiTheme.Label($"{GearSets.KeyName(0)}–{GearSets.KeyName(5)} 載入，{GearSets.KeyName(6)} 在最近兩組間快速切換（戰鬥中冷卻 {GearSets.CooldownSec:0.#} 秒）。裝備組保存裝備、技能列與專精；技能等級不隨之改變。在殘燈鎮時會自動從儲藏箱取出。",
            UiTheme.FsCaption, UiTheme.Dim), SheetW - 24));
        var now = GearEval.Baseline(h);
        float o0 = GearEval.Offense(now), l0 = now.MaxLife, a0 = now.Armor;
        float r0 = ResAvg(now);
        for (int i = 0; i < Hero.SetCount; i++)
        {
            int k = i;
            var s = h.SetAt(i);
            bool active = h.D.SetActive == i;
            var missing = s.Empty ? new List<string>() : h.MissingOf(i, GearSets.InTown);
            var head = new HBoxContainer();
            head.AddThemeConstantOverride("separation", 8);
            var name = UiTheme.Trimmed(UiTheme.Label($"{i + 1}. {h.SetName(i)}{(missing.Count > 0 ? " ⚠" : "")}", UiTheme.FsRow, active ? UiTheme.Gold : missing.Count > 0 ? Palette.Ember : UiTheme.Text, active), false);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(name);
            head.AddChild(UiTheme.Label($"〔{GearSets.KeyName(i)}〕", UiTheme.FsCaption, UiTheme.Dim));
            _rows.AddChild(head);
            _board.Add(new SlotView { Node = head, Clip = _sheetScroll, Text = SetTip(h, i), Glow = false, Compare = false });
            if (!s.Empty)
            {
                Hero p = null;
                try { p = h.PreviewSet(i); } catch (Exception e) { Log.Error("ui", $"set {i + 1} preview failed", e); }
                if (p != null)
                {
                    float o = GearEval.Offense(p), r = ResAvg(p);
                    string D(float v, float b, string f) => active || MathF.Abs(v - b) < 0.5f ? "" : $"（{(v > b ? "+" : "−")}{MathF.Abs(v - b).ToString(f)}）";
                    var line = $"{Stance(p)}・{p.MainHand?.Gb?.Name ?? p.Weapon?.Name ?? "徒手"}{(p.OffHand != null ? "＋" + (p.OffHand.Gb?.Name ?? "") : "")}\n"
                        + $"每秒傷害 {o:0}{D(o, o0, "0")}　生命 {p.MaxLife:0}{D(p.MaxLife, l0, "0")}\n護甲 {p.Armor:0}{D(p.Armor, a0, "0")}　抗性 {r:0}%{D(r, r0, "0")}";
                    _rows.AddChild(UiTheme.Wrapped(UiTheme.Label(line, UiTheme.FsCaption, UiTheme.Text), SheetW - 24));
                }
            }
            else _rows.AddChild(UiTheme.Label("（空）", UiTheme.FsCaption, UiTheme.Dim));
            var acts = new HFlowContainer();
            acts.AddThemeConstantOverride("h_separation", 6);
            if (!s.Empty && !active) acts.AddChild(ItemUi.Btn("載入", () => LoadSetClick(k), UiTheme.FsCaption));
            acts.AddChild(ItemUi.Btn("儲存目前裝備", () => SaveSetClick(k), UiTheme.FsCaption));
            acts.AddChild(ItemUi.Btn("改名", () => RenameSetClick(k), UiTheme.FsCaption));
            _rows.AddChild(acts);
        }
    }

    private static float ResAvg(Hero h) =>
        (h.Res(Element.Fire) + h.Res(Element.Frost) + h.Res(Element.Lightning) + h.Res(Element.Poison) + h.Res(Element.Shadow)) / 5f;

    // ───────────────────────── bag filter (行囊篩選) and bulk marks ─────────────────────────

    private void ToggleFilter()
    {
        _filterOn = !_filterOn;
        // the filter takes exactly the doll's height, so nothing below it moves
        if (_filterOn && _dollFrame != null)
        {
            float hh = _dollFrame.Size.Y > 0f ? _dollFrame.Size.Y : DollH + DollGrow + 2f * DollPad;
            _filterFrame.CustomMinimumSize = new Vector2(0, hh);
            _filterScroll.CustomMinimumSize = new Vector2(0, MathF.Max(120f, hh - 2f * DollPad));
        }
        Snd.Ui("ui_click");
        _dirty = true;
        Log.Info("inv", $"bag filter panel {(_filterOn ? "opened" : "closed")}");
    }

    private bool Shown(Item it) => !_filter.Active || _filter.Matches(_hero, it, ItemMarks.IsNew);

    /// <summary>A drop-down at caption size that never takes keyboard focus (the panel's hotkeys stay live).</summary>
    private OptionButton Choice(string[] items, int selected, Action<int> onPick)
    {
        var ob = new OptionButton { FocusMode = FocusModeEnum.None };
        foreach (var s in items) ob.AddItem(s);
        ob.Selected = Math.Clamp(selected, 0, items.Length - 1);
        ob.AddThemeFontSizeOverride("font_size", UiTheme.Fs(UiTheme.FsCaption));
        ob.GetPopup().AddThemeFontSizeOverride("font_size", UiTheme.Fs(UiTheme.FsRow));
        ob.ItemSelected += i => Log.Guard("ui", () => { onPick((int)i); _filter.Preset = null; _dirty = true; });
        return ob;
    }

    private static HFlowContainer Flow()
    {
        var f = new HFlowContainer();
        f.AddThemeConstantOverride("h_separation", 6);
        f.AddThemeConstantOverride("v_separation", 4);
        return f;
    }

    private static Label Caption(string s)
    {
        var l = UiTheme.Label(s, UiTheme.FsCaption, UiTheme.Dim);
        l.VerticalAlignment = VerticalAlignment.Center;
        return l;
    }

    // every stat an affix (or a base's own stat) can carry, by name
    private static readonly Stat[] AffixStats = ItemDb.Pools.Select(p => p.S).Concat(ItemDb.GearBases.SelectMany(b => b.Implicit.Select(x => x.s))).Distinct().OrderBy(s => (int)s).ToArray();

    private void BuildFilter(Hero h)
    {
        ItemUi.Clear(_filterBody);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        var title = UiTheme.Label("行囊篩選", UiTheme.FsRow, UiTheme.Gold, true);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        head.AddChild(ItemUi.Btn("返回裝備", ToggleFilter, UiTheme.FsCaption));
        _filterBody.AddChild(head);

        // rarity: any ticked one matches
        var r = Flow();
        r.AddChild(Caption("稀有度"));
        for (int i = 0; i < 5; i++)
        {
            int k = i;
            var b = ItemUi.Btn(Names.Rarity((Rarity)i), () => { _filter.Rarities[k] = !_filter.Rarities[k]; _filter.Preset = null; _dirty = true; }, UiTheme.FsCaption);
            b.AddThemeColorOverride("font_color", _filter.Rarities[i] ? Palette.Rarity((Rarity)i) : UiTheme.Dim);
            r.AddChild(b);
        }
        _filterBody.AddChild(r);

        var row = Flow();
        row.AddChild(Choice(new[] { "種類：全部" }.Concat(BagFilter.Kinds.Select(x => x.name)).ToArray(), _filter.Kind + 1, i => _filter.Kind = i - 1));
        row.AddChild(Choice(BagFilter.UsableNames, _filter.Usable, i => _filter.Usable = i));
        row.AddChild(Choice(BagFilter.VerdictNames, _filter.Verdict, i => _filter.Verdict = i));
        row.AddChild(Choice(Enumerable.Range(0, BagFilter.BelowCount).Select(BagFilter.BelowName).ToArray(), _filter.Below, i => _filter.Below = i));
        int aSel = _filter.Affix == Stat.Count ? 0 : Array.IndexOf(AffixStats, _filter.Affix) + 1;
        row.AddChild(Choice(new[] { "詞綴：不限" }.Concat(AffixStats.Select(StatInfo.Name)).ToArray(), aSel, i => _filter.Affix = i == 0 ? Stat.Count : AffixStats[i - 1]));
        var nb = ItemUi.Btn("僅新物品", () => { _filter.NewOnly = !_filter.NewOnly; _filter.Preset = null; _dirty = true; }, UiTheme.FsCaption);
        ItemUi.Tone(nb, _filter.NewOnly);
        row.AddChild(nb);
        _filterBody.AddChild(row);

        var pre = Flow();
        pre.AddChild(Caption("快速篩選"));
        foreach (var (id, name) in BagFilter.Presets)
        {
            if (id == "forgeable" && h.Class != ClassId.Soulbinder) continue;      // [golem] 可鑄造魔像: the Soulbinder's iron golem only
            string pid = id;
            var b = ItemUi.Btn(name, () => { _filter.ApplyPreset(pid); _dirty = true; Log.Info("inv", $"bag filter preset {pid}"); }, UiTheme.FsCaption);
            ItemUi.Tone(b, _filter.Preset == id);
            pre.AddChild(b);
        }
        pre.AddChild(ItemUi.Btn("清除篩選", () => { _filter.Clear(); _dirty = true; }, UiTheme.FsCaption));
        _filterBody.AddChild(pre);
        _filterBody.AddChild(UiTheme.Rule());

        var shown = _filter.Active ? _filter.Pick(h, ItemMarks.IsNew) : new List<Item>();
        int locked = shown.Count(i => h.IsProtected(i));
        var act = Flow();
        act.AddChild(Caption(_filter.Active ? $"符合 {shown.Count} 件{(locked > 0 ? $"（{locked} 件鎖定或在裝備組中）" : "")}" : "尚未設定篩選條件"));
        act.AddChild(ItemUi.Btn("全部標記回收", () => MarkShown(BagMark.Recycle), UiTheme.FsCaption));
        act.AddChild(ItemUi.Btn("全部標記丟棄", () => MarkShown(BagMark.Discard), UiTheme.FsCaption));
        act.AddChild(ItemUi.Btn("清除標記", ClearShownMarks, UiTheme.FsCaption));
        _filterBody.AddChild(act);
        var keys = UiTheme.Wrapped(UiTheme.Label("R 回收　X 丟棄　L 鎖定　Shift＋R／X 全部標記　C 清除　F 返回", UiTheme.FsCaption, UiTheme.Dim));
        _filterBody.AddChild(keys);
    }

    /// <summary>Marks every item the filter shows (locked items and, for presets, legendary / unique pieces with a power are left alone).</summary>
    private void MarkShown(BagMark m)
    {
        var h = _hero;
        if (h == null) return;
        if (!_filter.Active) { Fail("請先設定篩選條件，再一次標記所有符合的物品。"); return; }
        var shown = _filter.Pick(h, ItemMarks.IsNew);
        int locked = shown.Count(i => h.IsProtected(i));
        int special = 0;
        if (_filter.Preset != null) { special = shown.Count(i => !h.IsProtected(i) && (i.Aspect != null || i.Unique != null)); shown.RemoveAll(i => i.Aspect != null || i.Unique != null); }
        int gems = m == BagMark.Recycle ? shown.Count(i => !i.IsEquip) : 0;
        int n = h.MarkAll(shown, m);
        var skipped = new List<string>();
        if (locked > 0) skipped.Add($"{locked} 件鎖定或在裝備組中");
        if (special > 0) skipped.Add($"{special} 件含特殊效果");
        if (gems > 0) skipped.Add($"{gems} 顆寶石無法回收");
        Snd.Ui("ui_click");
        Say($"已將 {n} 件物品標記為{(m == BagMark.Recycle ? "回收" : "丟棄")}。{(skipped.Count > 0 ? "（略過：" + string.Join("、", skipped) + "）" : "")}");
        Log.Info("inv", $"bulk mark {m}: {n} marked, skipped locked={locked} special={special} gems={gems} (filter preset={_filter.Preset ?? "-"})");
    }

    private void ClearShownMarks()
    {
        var h = _hero;
        if (h == null) return;
        int n = h.ClearMarks(_filter.Active ? _filter.Pick(h, ItemMarks.IsNew) : null);
        Say(n > 0 ? $"已清除 {n} 件物品的標記。" : "沒有需要清除的標記。");
    }

    private void ToggleOne(Item it, BagMark m)
    {
        var h = _hero;
        if (h == null || it == null) return;
        string why = h.WhyCantMark(it, m);
        if (why != null && h.MarkOf(it) != m) { Fail(why); return; }
        var now = h.ToggleMark(it, m);
        Snd.Ui("ui_click");
        Say(now == BagMark.None ? $"已取消「{it.Name}」的標記。" : $"已將「{it.Name}」標記為{(now == BagMark.Recycle ? "回收" : "丟棄")}。");
    }

    private void ToggleLockOf(Item it)
    {
        var h = _hero;
        if (h == null || it == null || !(h.InBag(it) || h.IsEquipped(it))) return;
        bool now = h.ToggleLock(it);
        Snd.Ui("ui_click");
        Say(now ? $"已鎖定「{it.Name}」：批次標記、回收與丟棄都不會動到它。" : $"已解除「{it.Name}」的鎖定。");
        _dirty = true;
    }

    /// <summary>At the smith: every item marked 回收 is salvaged, after a confirmation that shows what it yields.</summary>
    private void RecycleMarked()
    {
        var h = _hero;
        if (h == null) return;
        var list = h.RecycleItems().FindAll(i => h.WhyCantSalvage(i) == null);
        if (list.Count == 0) { Fail("沒有標記回收的物品。以 Shift＋左鍵或 R 標記，或在篩選中一次標記。"); return; }
        if (!AtSmith()) { Fail($"已標記 {list.Count} 件回收：回收需要在鐵匠布蘭身邊進行。"); return; }
        var pre = Crafting.PreviewSalvage(list, h.D);
        string text = $"回收 {list.Count} 件標記的物品？\n可得：{Crafting.Mats(pre.Scrap, pre.Dust, pre.Cores)}"
            + (pre.Gems > 0 ? $"，取回寶石 {pre.Gems} 顆" : "")
            + (pre.Precious > 0 ? $"\n其中 {pre.Precious} 件傳奇或獨特裝備，回收之後無法復原。" : "");
        void Go()
        {
            var r = h.SalvageMany(list);
            if (r.Count == 0) { Fail("沒有回收任何物品"); return; }
            Snd.Ui("salvage");
            string powers = r.Powers.Count > 0 ? $"　威能典籍：{string.Join("、", r.Powers)}" : "";
            Say($"回收了 {r.Count} 件物品：{Crafting.Mats(r.Scrap, r.Dust, r.Cores)}{powers}", Palette.Good);
            Log.Info("inv", $"recycled {r.Count} marked items: scrap {r.Scrap} dust {r.Dust} cores {r.Cores}");
        }
        if (Game.I?.Ui != null) Game.I.Ui.Confirm(text, () => Log.Guard("ui", Go), "回收", "取消");
        else Go();
    }

    /// <summary>Anywhere: every item marked 丟棄 is thrown away, after a confirmation that counts them by rarity.</summary>
    private void DiscardMarked()
    {
        var h = _hero;
        if (h == null) return;
        var list = h.DiscardItems().FindAll(i => h.WhyCantDiscard(i) == null);
        if (list.Count == 0) { Fail("沒有標記丟棄的物品。以 X 標記，或在篩選中一次標記。"); return; }
        var by = new int[5];
        foreach (var i in list) by[Math.Clamp((int)i.Rarity, 0, 4)]++;
        string counts = string.Join("、", Enumerable.Range(0, 5).Where(i => by[i] > 0).Select(i => $"{Names.Rarity((Rarity)i)} {by[i]}"));
        void Go()
        {
            var res = h.DiscardMarked();
            int n = res.Sum();
            Snd.Ui("ui_click");
            Say($"丟棄了 {n} 件物品。", Palette.Good);
            Log.Info("inv", $"discarded {n} marked items ({string.Join("/", res)})");
        }
        string warn = by[3] + by[4] > 0 ? "\n其中有傳奇或獨特裝備！" : "";
        if (Game.I?.Ui != null) Game.I.Ui.Confirm($"丟棄 {list.Count} 件標記的物品？\n{counts}{warn}\n丟棄之後無法找回。（到鐵匠那裡回收可以換得材料。）", () => Log.Guard("ui", Go), "丟棄", "取消");
        else Go();
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (!_open || e is not InputEventKey k || !k.Pressed || k.Echo) return;
        var ui = Game.I?.Ui;
        if (ui == null || ui.ModalOpen || !ui.IsOpen("inventory")) return;
        Key key = k.Keycode != Key.None ? k.Keycode : k.PhysicalKeycode;
        var it = _board.Hovered;
        bool done = true;
        try
        {
            switch (key)
            {
                case Key.F: ToggleFilter(); break;
                case Key.R when k.ShiftPressed: MarkShown(BagMark.Recycle); break;
                case Key.X when k.ShiftPressed: MarkShown(BagMark.Discard); break;
                case Key.R when it != null && _hero.InBag(it): ToggleOne(it, BagMark.Recycle); break;
                case Key.X when it != null && _hero.InBag(it): ToggleOne(it, BagMark.Discard); break;
                case Key.L when it != null: ToggleLockOf(it); break;
                case Key.C: ClearShownMarks(); break;
                default: done = false; break;
            }
        }
        catch (Exception ex) { Log.Error("ui", $"inventory hotkey {key} failed", ex); }
        if (done) GetViewport().SetInputAsHandled();
    }

    private void DecorateBag(Item it, SlotView v)
    {
        if (_filterOn && _filter.Active && !Shown(it)) ItemUi.Dimmed(v.Node, 0.66f);      // the filter dims what it does not show
        else if (_filterOn && _filter.Forgeable) GolemUi.ForgeBadge(v.Node);            // [golem] 可鑄造魔像: the forge badge on what the filter shows
        if (_gemPick != null)
        {
            if (ReferenceEquals(it, _gemPick)) ItemUi.Outline(v.Node, ItemUi.PickColor, 3);
            else if (it.IsEquip && it.Gems.Count < it.Sockets) ItemUi.Outline(v.Node, ItemUi.PickColor);
            else ItemUi.Dimmed(v.Node, 0.5f);
        }
        if (ItemMarks.IsNew(it)) v.NewDot = ItemUi.NewDot(v.Node);
    }

    private string BagHint(Item it)
    {
        var h = _hero;
        if (h == null) return null;
        if (_gemPick != null)
            return ReferenceEquals(it, _gemPick) ? ItemUi.Dim("再次點擊：取消鑲嵌") : it.IsEquip ? SocketHint(it) : ItemUi.Dim("點擊：改選這顆寶石");
        string junk = it.Locked ? "L：解除鎖定" : (h.MarkOf(it) == BagMark.Recycle ? "Shift＋左鍵／R：取消回收" : "Shift＋左鍵／R：標記回收") + "　X：" + (h.MarkOf(it) == BagMark.Discard ? "取消丟棄" : "標記丟棄") + "　L：鎖定";
        if (it.Kind == ItemKind.Gem) return ItemUi.Dim(junk + "\nCtrl＋左鍵：丟在地上");
        string why = h.WhyCantEquip(it);
        return (why != null ? ItemUi.Col(Palette.Bad, why) + "\n" : "") + ItemUi.Dim("左鍵：裝備　" + junk
            + (it.Kind == ItemKind.Weapon && !it.TwoHand && ItemDb.Allows(h.Class, it.Base, EquipSlot.OffHand) ? "\nAlt＋左鍵：裝備在副手" : "") + "\nCtrl＋左鍵：丟在地上");
    }

    private void OnBagLeft(Item it)
    {
        var h = _hero;
        if (h == null || !h.InBag(it)) return;
        ItemMarks.See(it);
        if (_gemPick != null) { TrySocket(it); return; }
        if (Input.IsKeyPressed(Key.Ctrl)) { DropOnGround(h, it); return; }
        if (Input.IsKeyPressed(Key.Shift)) { ToggleOne(it, BagMark.Recycle); return; }     // Shift＋左鍵: 回收 mark (R does the same, X the 丟棄 mark)
        if (it.Kind == ItemKind.Gem) { BeginGemPick(it); return; }
        EquipFromBag(h, it);
    }

    private void OnBagRight(Item it)
    {
        var h = _hero;
        if (h == null || !h.InBag(it)) return;
        ItemMarks.See(it);
        if (_gemPick != null) { CancelGemPick(); return; }
        if (it.Kind == ItemKind.Gem) { BeginGemPick(it); return; }
        EquipFromBag(h, it);
    }

    private void EquipFromBag(Hero h, Item it)
    {
        // Alt＋click puts a one-hand weapon in the off hand (dual-wield classes); otherwise the item finds its own slot
        EquipSlot? slot = it.Kind == ItemKind.Weapon && !it.TwoHand && Input.IsKeyPressed(Key.Alt) && ItemDb.Allows(h.Class, it.Base, EquipSlot.OffHand)
            ? EquipSlot.OffHand : null;
        string why = h.WhyCantEquip(it, slot);
        if (why != null)
        {
            Fail(why);
            if (!ItemDb.UsableBy(it, h.Class)) Game.I?.Hud?.Warn(why);       // 「燼刃衛無法裝備魔杖」 as a toast too, never a silent no
            return;
        }
        Item main0 = h.MainHand, off0 = h.OffHand;
        if (!h.Equip(it, slot)) { Fail("無法裝備"); return; }
        h.ClearJunk(it);
        Snd.Ui(ItemUi.EquipSound(it));
        // the other hand's piece that had to make room went to the bag: say so
        if (off0 != null && off0 != it && h.OffHand == null && h.InBag(off0) && it.TwoHand) Say($"雙手武器佔用兩手：副手的「{off0.Name}」已收入行囊。");
        else if (main0 != null && main0.TwoHand && h.MainHand == null && h.InBag(main0)) Say($"「{main0.Name}」需要雙手，已收入行囊。");
        Log.Info("inv", $"equipped {it.Name} ({it.Rarity}, power {it.Power}, {it.Base}) in {slot?.ToString() ?? "auto"}; main={h.MainHand?.Base} off={h.OffHand?.Base}");
    }

    /// <summary>Ctrl＋左鍵: the item leaves the bag and lies on the ground at the hero's feet, where it can be picked up again.</summary>
    private void DropOnGround(Hero h, Item it)
    {
        string why = h.WhyCantDiscard(it);
        if (why != null) { Fail(why); return; }
        var p = Game.I?.Player;
        if (p == null || !GodotObject.IsInstanceValid(p)) { Fail("現在無法丟棄物品"); return; }
        if (!h.Discard(it)) { Fail("無法丟棄"); return; }
        Log.Guard("loot", () => Loot.DropItem(it, p.Pos2));
        Snd.Ui("ui_click");
        Say($"已將「{it.Name}」丟在地上。");
    }

    private void SortBag() => _hero?.SortBag();

    // ───────────────────────── gems ─────────────────────────

    private void BeginGemPick(Item gem)
    {
        _gemPick = gem;
        _msgT = 0f;
        _dirty = true;
        Snd.Ui("ui_click");
    }

    private void CancelGemPick()
    {
        _gemPick = null;
        _msgT = 0f;
        _dirty = true;
    }

    private void TrySocket(Item target)
    {
        var h = _hero; var gem = _gemPick;
        if (h == null || gem == null) return;
        if (ReferenceEquals(target, gem)) { CancelGemPick(); return; }
        if (target.Kind == ItemKind.Gem) { BeginGemPick(target); return; }
        string why = h.WhyCantSocket(gem, target);
        if (why != null) { Fail(why); return; }
        if (!h.Socket(gem, target)) { Fail("無法鑲嵌"); return; }
        _gemPick = null;
        Snd.Ui("gem_socket");
        Say($"「{gem.Name}」已嵌入「{target.Name}」。", Palette.Good);
        Log.Info("craft", $"socketed {gem.GemId} into {target.Name} ({target.Gems.Count}/{target.Sockets})");
    }

    // ───────────────────────── junk ─────────────────────────

    /// <summary>True while the smith's panel is open or the hero stands next to the smith in town.</summary>
    public static bool AtSmith()
    {
        if (VendorUi.ActiveNpc == "smith") return true;
        var g = Game.I;
        var lv = g?.Level; var p = g?.Player;
        if (lv == null || p == null || !GodotObject.IsInstanceValid(lv) || !GodotObject.IsInstanceValid(p)) return false;
        if (!lv.IsTown || lv.Data == null) return false;
        foreach (var poi in lv.Data.All("npc"))
            if (poi != null && poi.Tag == "smith" && poi.Pos.DistanceTo(p.Pos2) <= SmithRange) return true;
        return false;
    }

    // ───────────────────────── character sheet ─────────────────────────

    private void BuildSheet(Hero h)
    {
        var d = h.D;
        _name.Text = string.IsNullOrEmpty(d.Name) ? Names.Class(h.Class) : d.Name;
        string line = $"等級 {d.Level}　{Names.Class(h.Class)}";
        if (d.Paragon > 0) line += $"　燼紋 {d.Paragon}";
        if (h.Spec != null) line += $"　{h.Spec.Name}";
        _class.Text = line;
        ItemUi.Clear(_tabBox);
        _tabBox.AddChild(ItemUi.Tabs(SheetTabs, _tab, PickTab, UiTheme.FsRow, 66));
        ItemUi.Clear(_rows);
        switch (_tab)
        {
            case 1: Offence(h); break;
            case 2: Defence(h); break;
            case 3: Utility(h); break;
            case 4: SetsTab(h); break;
            default: Overview(h); break;
        }
    }

    private void PickTab(int i)
    {
        if (i == _tab) return;
        _tab = i;
        _dirty = true;
        if (_sheetScroll != null) _sheetScroll.ScrollVertical = 0;
    }

    private static string N(float v) => Crafting.Num((long)MathF.Round(v));

    /// <summary>Sustained basic-attack output: weapon × speed × main attribute × damage bonuses, with crits averaged in.</summary>
    public static float EstimateDps(Hero h)
    {
        float w = (h.WeaponMin + h.WeaponMax) * 0.5f;
        float add = h.S[Stat.DmgAll];
        float crit = Mathf.Clamp(h.S[Stat.CritChance] / 100f, 0f, 1f);
        float normal = 1f + add / 100f;
        float critical = 1.5f * (1f + (add + h.S[Stat.CritDmg]) / 100f);
        return w * h.SwingsPerSec * h.MainMul * ((1f - crit) * normal + crit * critical);
    }

    private void Head(string text)
    {
        var l = UiTheme.Label(text, UiTheme.FsBody, UiTheme.Gold, true);
        UiTheme.MinSize(l, 0, 44);                       // a 35 px line with some air above it; grows with the text
        l.VerticalAlignment = VerticalAlignment.Bottom;
        _rows.AddChild(ItemUi.Underlined(l));
    }

    /// <summary>
    /// One line of the sheet: name on the left, value on the right, both at row size. The value always shows in
    /// full; a name that cannot fit beside it would end in an ellipsis (the column is sized so that none does, and
    /// the tooltip of a row starts with its whole name).
    /// </summary>
    private void Row(string name, string value, string tip = null, Color? valueColor = null)
    {
        var row = UiTheme.MinSize(new HBoxContainer(), 0, 34);
        var l = UiTheme.Trimmed(UiTheme.Label(name, UiTheme.FsRow, UiTheme.Dim), false);
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        l.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(l);
        var v = UiTheme.Label(value, UiTheme.FsRow, valueColor ?? UiTheme.Text);
        v.HorizontalAlignment = HorizontalAlignment.Right;
        v.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(v);
        row.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0), MouseFilter = MouseFilterEnum.Ignore });   // keeps numbers clear of the scrollbar
        _rows.AddChild(row);
        if (tip != null) _board.Add(new SlotView { Node = row, Clip = _sheetScroll, Text = name + "\n" + ItemUi.Dim(tip), Glow = false, Compare = false });
    }

    /// <summary>A row for one stat straight from the stat block, gold when it sits at its cap.</summary>
    private void StatRow(Hero h, Stat s, float cap = 0f, string name = null)
    {
        float v = h.S[s];
        bool pct = StatInfo.IsPct(s);
        bool capped = cap > 0f && v >= cap - 0.01f;
        string text = pct ? ItemUi.Pct(v) : N(v);
        if (capped) text += "（上限）";
        string tip = StatTip(s);
        if (cap > 0f) tip = (tip == null ? "" : tip + "\n") + $"上限 {cap:0}{(pct ? "%" : "")}。";
        Row(name ?? StatInfo.Name(s), text, tip, capped ? UiTheme.Gold : (Color?)null);
    }

    private void Overview(Hero h)
    {
        var d = h.D;
        Head("概況");
        Row("等級", d.Paragon > 0 ? $"{d.Level}　燼紋 {d.Paragon}" : d.Level.ToString());
        if (d.Level < Balance.LevelCap) Row("經驗", $"{Crafting.Num(d.Xp)} / {Crafting.Num(Balance.XpToNext(d.Level))}");
        else if (d.Paragon < Balance.ParagonCap) Row("燼紋經驗", $"{Crafting.Num(d.ParagonXp)} / {Crafting.Num(Balance.ParagonXp(d.Paragon))}");
        else Row("燼紋經驗", "已達頂點");
        Row("生命上限", N(h.MaxLife), "生命歸零時倒下。由等級、裝備上的生命與生命加成決定。");
        Row("護甲", N(h.Armor), $"面對同等級的敵人，護甲使你受到的傷害降低 {ItemUi.Pct(Balance.ArmorDr(h.Armor, d.Level) * 100f)}。\n敵人等級越高，同樣的護甲效果越低。");
        Row("每秒傷害（估算）", N(EstimateDps(h)), "以武器傷害、攻擊速度、主屬性、傷害加成與爆擊推算的持續輸出。\n不含技能倍率與條件加成，僅供比較裝備。");
        Row("武器傷害", $"{h.WeaponMin:0}–{h.WeaponMax:0}", h.DualWielding ? "雙持：兩手輪流出擊，各以自己武器的傷害範圍為基礎；這裡是兩手的平均。" : "每一次技能命中，都以這個範圍內的一個數值為基礎。");
        Row("每秒攻擊", h.SwingsPerSec.ToString("0.00", CultureInfo.InvariantCulture), h.DualWielding ? $"兩把武器速度的平均，雙持再快 {(ItemDb.DualSpeed - 1f) * 100f:0}%，乘上攻擊速度加成。" : "武器的基礎攻擊頻率，再乘上攻擊速度加成。");
        Row($"{Names.Resource(h.Class)}上限", N(h.MaxResource));
        Row("藥水容量", d.PotionCap.ToString());

        Head("屬性");
        var main = Names.MainStat(h.Class);
        Attr(h, Stat.Str, main, "每點力量提供 1 點護甲。");
        Attr(h, Stat.Int, main, "每點智力提供 0.04% 全元素抗性。");
        Attr(h, Stat.Dex, main, "每點敏捷提供 0.015% 閃避機率與 0.01% 爆擊機率。");
        Attr(h, Stat.Wil, main, "每點意志提供 0.08% 受到治療效果與 0.05% 資源產生。");

        Head("專精");
        if (h.Spec != null) Row(h.Spec.Name, "", h.Spec.Desc);
        else Row(d.Level >= SkillDb.SpecLevel ? "尚未選擇專精" : $"等級 {SkillDb.SpecLevel} 開放", "");

        // 可用武器: the class's loadout (the same table the tooltips, filters and loot use)
        Head("可用武器");
        foreach (var line in ItemDb.LoadoutLines(h.Class))
        {
            _rows.AddChild(UiTheme.Wrapped(UiTheme.Label(line, UiTheme.FsRow, UiTheme.Text), SheetW - 24));
        }
        string held = h.OffHandBlocked ? "雙手持握" : h.DualWielding ? "雙持" : h.OffHand != null ? "單手＋副手" : h.Weapon != null ? "單手" : "徒手";
        Row("目前持握", held, "雙手武器同時佔用兩手；可雙持的職業可在副手再拿一把單手武器。");
        // SYSTEMS: the weapon guide for this class (also G on any weapon or off-hand)
        _rows.AddChild(ItemUi.Btn("武器指南（G）", () => Game.I?.Ui?.Open("guide", h.Class), UiTheme.FsRow));
    }

    private void Attr(Hero h, Stat s, Stat main, string tip)
    {
        bool isMain = s == main;
        string name = StatInfo.Name(s) + (isMain ? "（主屬性）" : "");
        if (isMain) tip += $"\n主屬性：每點使你造成的傷害提高 0.25%（目前 +{ItemUi.Pct((h.MainMul - 1f) * 100f)}）。";
        Row(name, N(h.S[s]), tip, isMain ? UiTheme.Gold : (Color?)null);
    }

    private void Offence(Hero h)
    {
        Head("攻擊");
        Row("武器傷害", $"{h.WeaponMin:0}–{h.WeaponMax:0}");
        Row("每秒攻擊", h.SwingsPerSec.ToString("0.00", CultureInfo.InvariantCulture));
        StatRow(h, Stat.AttackSpeed, 100f);
        StatRow(h, Stat.CritChance, 75f);
        Row("爆擊傷害", $"×1.5　+{ItemUi.Pct(h.S[Stat.CritDmg])}", "爆擊造成 1.5 倍傷害；此處的加成在爆擊時併入傷害加成一起計算。");
        StatRow(h, Stat.LuckyHit);
        Row("主屬性加成", "+" + ItemUi.Pct((h.MainMul - 1f) * 100f), $"來自{StatInfo.Name(Names.MainStat(h.Class))}：每點 0.25%。與下列加成相乘。");

        Head("傷害加成");
        StatRow(h, Stat.DmgAll);
        StatRow(h, Stat.DmgPhysical); StatRow(h, Stat.DmgFire); StatRow(h, Stat.DmgFrost);
        StatRow(h, Stat.DmgLightning); StatRow(h, Stat.DmgPoison); StatRow(h, Stat.DmgShadow);
        StatRow(h, Stat.DmgDot); StatRow(h, Stat.DmgElite); StatRow(h, Stat.DmgCC);
        StatRow(h, Stat.DmgVuln); StatRow(h, Stat.DmgOverpower);

        Head("技能");
        StatRow(h, Stat.DmgBasic); StatRow(h, Stat.DmgCore); StatRow(h, Stat.DmgUlt); StatRow(h, Stat.MinionDmg);
        StatRow(h, Stat.SkillRanks);
    }

    private void Defence(Hero h)
    {
        Head("生存");
        Row("生命上限", N(h.MaxLife));
        StatRow(h, Stat.MaxLife, 0f, "額外生命");
        StatRow(h, Stat.LifePct, 0f, "生命上限加成");
        Row("護甲", N(h.Armor), $"面對同等級的敵人，受到的傷害降低 {ItemUi.Pct(Balance.ArmorDr(h.Armor, h.D.Level) * 100f)}（最高 80%）。");
        StatRow(h, Stat.DmgReduction, 75f);
        StatRow(h, Stat.Dodge, 50f);
        StatRow(h, Stat.Block, 50f);
        StatRow(h, Stat.BarrierPct);

        Head("元素抗性");
        Res(h, Element.Fire); Res(h, Element.Frost); Res(h, Element.Lightning); Res(h, Element.Poison); Res(h, Element.Shadow);
        StatRow(h, Stat.ResAll);

        Head("恢復");
        StatRow(h, Stat.LifeRegen); StatRow(h, Stat.LifeOnHit); StatRow(h, Stat.LifeOnKill);
        StatRow(h, Stat.Healing); StatRow(h, Stat.Thorns);
    }

    private void Res(Hero h, Element e)
    {
        const float cap = 70f;
        float v = h.Res(e);
        bool capped = v >= cap - 0.01f;
        var own = StatInfo.ResOf(e);
        string tip = $"全元素抗性 {ItemUi.Pct(h.S[Stat.ResAll])} ＋ {StatInfo.Name(own)} {ItemUi.Pct(h.S[own])}。\n抗性使該元素的傷害按比例降低，上限 {cap:0}%。";
        Row(StatInfo.Name(own), $"{ItemUi.Pct(v)} / {cap:0}%", tip, capped ? UiTheme.Gold : Palette.Element(e));
    }

    private void Utility(Hero h)
    {
        var d = h.D;
        Head("機動與資源");
        StatRow(h, Stat.MoveSpeed, 100f);
        Row($"{Names.Resource(h.Class)}上限", N(h.MaxResource));
        StatRow(h, Stat.MaxResource, 0f, $"額外{Names.Resource(h.Class)}上限");
        StatRow(h, Stat.ResourceGen);
        StatRow(h, Stat.ResourceCost, 50f);
        StatRow(h, Stat.Cdr, 60f);

        Head("拾獲");
        StatRow(h, Stat.GoldFind); StatRow(h, Stat.MagicFind); StatRow(h, Stat.XpGain);

        Head("旅途");
        var t = TimeSpan.FromSeconds(Math.Max(0, d.PlayTime));
        Row("旅行時間", $"{(int)t.TotalHours} 小時 {t.Minutes} 分");
        Row("擊殺", Crafting.Num(d.Kills));
        Row("菁英", Crafting.Num(d.Elites));
        Row("首領", Crafting.Num(d.Bosses));
        Row("倒下", Crafting.Num(d.Deaths));
        Row("威能典籍", $"{d.Codex.Count} / {ItemDb.Aspects.Count}", "拆解傳奇裝備所收錄的威能，可請祕術師銘刻到其他裝備上。");
    }

    private static string StatTip(Stat s) => s switch
    {
        Stat.MaxLife => "裝備、寶石與被動技能直接提供的生命，計入生命上限加成之前。",
        Stat.LifePct => "按比例提高生命上限。",
        Stat.ResAll => "同時計入五種元素抗性。智力也會提供少量。",
        Stat.DmgReduction => "所有受到的傷害按比例降低，與護甲、抗性相乘。",
        Stat.Dodge => "完全避開一次攻擊的機率。敏捷也會提供少量。",
        Stat.Block => "以盾格擋的機率：被格擋的攻擊傷害減半。主要來自盾牌。",
        Stat.LifeRegen => "每秒自然恢復的生命。",
        Stat.LifeOnHit => "每次命中敵人時恢復的生命。",
        Stat.LifeOnKill => "每擊殺一名敵人時恢復的生命。",
        Stat.Healing => "藥水、技能與擊中恢復等所有治療的效果提高。",
        Stat.Thorns => "近戰攻擊者每次打中你時受到的反擊傷害。",
        Stat.BarrierPct => "你獲得的屏障更厚。",
        Stat.DmgAll => "所有傷害的加成。與下列各項相加後一併計算。",
        Stat.DmgDot => "燃燒、中毒、流血與腐蝕等持續傷害的加成。",
        Stat.DmgElite => "對菁英與首領的傷害加成。",
        Stat.DmgCC => "對暈眩、凍結、緩速、定身等受控敵人的傷害加成。",
        Stat.DmgVuln => "對易傷敵人的額外加成（易傷本身已使其多受 20% 傷害）。",
        Stat.DmgOverpower => "壓制一擊的額外加成。壓制偶爾觸發，造成 1.25 倍傷害。",
        Stat.DmgBasic => "基本技能的傷害加成。",
        Stat.DmgCore => "核心技能的傷害加成。",
        Stat.DmgUlt => "終極技能的傷害加成。",
        Stat.MinionDmg => "你的僕從與召喚物造成的傷害加成。",
        Stat.AttackSpeed => "提高攻擊與施放的速度。",
        Stat.CritChance => "每次命中成為爆擊的機率。基礎 5%，敏捷也會提供少量。",
        Stat.LuckyHit => "提高各種「幸運一擊」效果的觸發機率。",
        Stat.MaxResource => "裝備、寶石與被動技能直接提供的資源上限，加在基礎的 100 點之上。",
        Stat.ResourceGen => "資源的產生與自然恢復加快。意志也會提供少量。",
        Stat.ResourceCost => "技能消耗的資源減少。",
        Stat.Cdr => "技能的冷卻時間縮短。",
        Stat.MoveSpeed => "移動得更快。",
        Stat.GoldFind => "敵人與寶箱掉落更多金幣。",
        Stat.MagicFind => "提高掉落稀有以上裝備的機會。",
        Stat.XpGain => "獲得的經驗值提高。",
        Stat.SkillRanks => "所有已學會的技能額外提高的等級。",
        _ => null
    };
}
