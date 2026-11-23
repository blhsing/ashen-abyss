using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// 亡者名冊, the book tab of the skill screen (generic over ClassBook; only the Soulbinder has one): three columns, one per
/// line, each with its kinds, the two upgrade picks of the chosen kind and the sacrifice; the golem column also forges the
/// iron golem (a picker of the bag's metal items, then a confirmation with the rarity, the name and the stat preview).
/// </summary>
public partial class BookPage : Control
{
    private VBoxContainer _cols0;
    private HBoxContainer _cols;
    private Label _fee, _title, _hint, _lock;
    private Control _picker, _confirm;
    private Item _pending;
    public Action Back;
    /// <summary>For the test suite and screenshots: the forge picker / confirmation open now.</summary>
    public bool PickerOpen => _picker != null && IsInstanceValid(_picker) && _picker.Visible;
    public bool ConfirmOpen => _confirm != null && IsInstanceValid(_confirm) && _confirm.Visible;

    public BookPage()
    {
        Name = "BookPage";
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
    }

    public override void _Ready()
    {
        var bg = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        bg.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTheme.Bg, 1f), UiTheme.Border, 2, 0));      // fully opaque: the tree must not show through
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var s in new[] { "margin_left", "margin_right" }) margin.AddThemeConstantOverride(s, 48);
        foreach (var s in new[] { "margin_top", "margin_bottom" }) margin.AddThemeConstantOverride(s, 28);
        AddChild(margin);
        _cols0 = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _cols0.AddThemeConstantOverride("separation", 10);
        margin.AddChild(_cols0);

        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 18);
        _title = UiTheme.Label("亡者名冊", UiTheme.FsTitle, UiTheme.Gold, true);
        head.AddChild(_title);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _fee = UiTheme.Label("", UiTheme.FsRow, UiTheme.Text);
        _fee.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(_fee);
        var back = UiTheme.Button("返回技能樹", () => Back?.Invoke(), UiTheme.FsRow);
        back.FocusMode = FocusModeEnum.None;
        head.AddChild(back);
        _cols0.AddChild(head);
        _hint = UiTheme.Label("", UiTheme.FsCaption, UiTheme.Dim);
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _cols0.AddChild(_hint);
        _lock = UiTheme.Label("", UiTheme.FsRow, Palette.Danger);
        _cols0.AddChild(_lock);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _cols0.AddChild(scroll);
        _cols = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _cols.AddThemeConstantOverride("separation", 22);
        scroll.AddChild(_cols);
    }

    private static bool InTown => Game.I?.Level?.IsTown ?? false;

    /// <summary>Rebuilds the page from the hero's book (cheap: a dozen controls per column).</summary>
    public void Rebuild()
    {
        var h = Game.I?.Hero;
        if (h == null || _cols == null) return;
        var b = h.Book;
        foreach (var c in _cols.GetChildren()) { _cols.RemoveChild(c); c.QueueFree(); }
        if (b == null) { _lock.Text = "此職業沒有亡者名冊。"; return; }
        h.EnsureBook();
        _title.Text = b.Name;
        _hint.Text = b.Hint;
        _lock.Text = h.BookUnlocked ? "" : $"等級 {b.UnlockLevel} 解鎖亡者名冊。";
        int fee = h.BookFee(InTown);
        _fee.Text = InTown ? "身在殘燈鎮：變更免費" : h.D.Book.FreeChanges > 0 ? $"免費變更剩餘 {h.D.Book.FreeChanges} 次" : $"每次變更：{fee} 金幣（持有 {h.D.Gold}）";
        foreach (var line in b.Lines) _cols.AddChild(Column(h, line));
    }

    private Control Column(Hero h, BookLineDef line)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(520f, 0f) };
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTheme.Panel, UiTheme.Border, 2, 6));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        var pad = new MarginContainer();
        foreach (var s in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" }) pad.AddThemeConstantOverride(s, 16);
        pad.AddChild(v);
        panel.AddChild(pad);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(new TextureRect { Texture = Icons.Raster(line.Icon), CustomMinimumSize = new Vector2(48f, 48f), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        head.AddChild(UiTheme.Label(line.Name, UiTheme.FsName, UiTheme.Gold, true));
        v.AddChild(head);

        bool sac = h.LineSacrificed(line.Id);
        string cur = h.BookKindId(line.Id);
        bool open = h.BookUnlocked;
        foreach (var k in line.Kinds)
        {
            bool sel = !sac && cur == k.Id;
            var btn = UiTheme.Button(k.Name, () => Pick(line.Id, k.Id, -1), UiTheme.FsBody);
            btn.Icon = Icons.Raster(k.Icon);
            btn.ExpandIcon = false; btn.AddThemeConstantOverride("icon_max_width", 40);
            btn.ToggleMode = true; btn.ButtonPressed = sel; btn.Disabled = !open; btn.FocusMode = FocusModeEnum.None;
            btn.Alignment = HorizontalAlignment.Left;
            btn.Name = "Kind_" + k.Id;
            if (sel) btn.AddThemeColorOverride("font_pressed_color", UiTheme.Gold);
            v.AddChild(btn);
            var desc = UiTheme.Label(k.Desc, UiTheme.FsCaption, sel ? UiTheme.Text : UiTheme.Dim);
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            desc.CustomMinimumSize = new Vector2(480f, 0f);
            v.AddChild(desc);
            if (sel)
            {
                int up = h.BookUp(line.Id);
                for (int i = 0; i < k.Ups.Length; i++)
                {
                    int ii = i;
                    var u = k.Ups[i];
                    var ub = UiTheme.Button($"強化：{u.Name}", () => Pick(line.Id, k.Id, h.BookUp(line.Id) == ii ? -1 : ii), UiTheme.FsRow);
                    ub.ToggleMode = true; ub.ButtonPressed = up == i; ub.Disabled = !open; ub.FocusMode = FocusModeEnum.None; ub.Alignment = HorizontalAlignment.Left;
                    ub.Name = $"Up_{k.Id}_{i}";
                    v.AddChild(ub);
                    var ud = UiTheme.Label("　" + u.Desc, UiTheme.FsCaption, up == i ? UiTheme.Text : UiTheme.Dim);
                    ud.AutowrapMode = TextServer.AutowrapMode.WordSmart; ud.CustomMinimumSize = new Vector2(480f, 0f);
                    v.AddChild(ud);
                }
                if (k.Id == "g_iron") ForgeBox(h, v);
            }
        }
        v.AddChild(new HSeparator());
        var sb = UiTheme.Button(sac ? $"已獻祭（{line.SacName}）" : line.SacName, () => Pick(line.Id, sac ? line.Kinds[0].Id : ClassBook.Sacrificed, -1), UiTheme.FsRow);
        sb.ToggleMode = true; sb.ButtonPressed = sac; sb.Disabled = !open; sb.FocusMode = FocusModeEnum.None;
        sb.Icon = Icons.Raster("skill/sb_explode"); sb.ExpandIcon = false; sb.AddThemeConstantOverride("icon_max_width", 36); sb.Alignment = HorizontalAlignment.Left;
        sb.Name = "Sacrifice_" + line.Id;
        v.AddChild(sb);
        float mul = h.SacrificeMul;
        var sd = UiTheme.Label(line.SacDesc + (mul > 1.01f ? $"（獎勵 ×{mul:0.##}）" : ""), UiTheme.FsCaption, sac ? UiTheme.Text : UiTheme.Dim);
        sd.AutowrapMode = TextServer.AutowrapMode.WordSmart; sd.CustomMinimumSize = new Vector2(480f, 0f);
        v.AddChild(sd);
        return panel;
    }

    private void ForgeBox(Hero h, VBoxContainer v)
    {
        var it = h.D.GolemForge;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        if (it != null)
        {
            var name = UiTheme.Label($"鑄自：{it.Name}", UiTheme.FsRow, Palette.Rarity(it.Rarity));
            box.AddChild(name);
            var st = GolemForge.Inherit(it, h);
            var lines = UiTheme.Label(string.Join("\n", st.Lines), UiTheme.FsCaption, UiTheme.Text);
            lines.AutowrapMode = TextServer.AutowrapMode.WordSmart; lines.CustomMinimumSize = new Vector2(480f, 0f);
            box.AddChild(lines);
        }
        else box.AddChild(UiTheme.Label("尚未鑄造：熔渣之軀，以拳頭作戰。", UiTheme.FsCaption, UiTheme.Dim));
        var fb = UiTheme.Button(it == null ? "鑄造魔像" : "重新鑄造", OpenPicker, UiTheme.FsRow);
        fb.FocusMode = FocusModeEnum.None; fb.Disabled = !h.BookUnlocked; fb.Name = "ForgeButton";
        box.AddChild(fb);
        v.AddChild(box);
    }

    private void Pick(string line, string kind, int up)
    {
        var h = Game.I?.Hero;
        if (h == null) return;
        if (!h.SetBook(line, kind, up, InTown, out var why)) { Snd.Ui("ui_error"); Game.I.Hud?.Warn(why ?? "無法變更。"); }
        else Snd.Ui("ui_click");
        Rebuild();
    }

    // ───────────────────────── forge picker and confirmation ─────────────────────────

    public void OpenPicker()
    {
        var h = Game.I?.Hero;
        if (h == null) return;
        ClosePopups();
        var cands = GolemForge.Candidates(h).OrderByDescending(x => GolemForge.Score(x, h)).ToList();
        var (box, v) = Popup("鑄造鐵魔像", 760f);
        v.AddChild(UiTheme.Label("選擇行囊中的一件金屬武器、盾牌或護甲。物品會被熔入魔像並消失。", UiTheme.FsCaption, UiTheme.Dim));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(700f, 420f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);
        v.AddChild(scroll);
        if (cands.Count == 0) list.AddChild(UiTheme.Label("行囊中沒有可鑄造的金屬裝備。", UiTheme.FsRow, UiTheme.Dim));
        foreach (var it in cands)
        {
            var item = it;
            var b = UiTheme.Button($"{it.Name}　{GolemForge.Verdict(h, it)}", () => Confirm(item), UiTheme.FsRow);
            b.Icon = Icons.ForItem(it); b.ExpandIcon = false; b.AddThemeConstantOverride("icon_max_width", 40);
            b.AddThemeColorOverride("font_color", Palette.Rarity(it.Rarity)); b.Alignment = HorizontalAlignment.Left; b.FocusMode = FocusModeEnum.None;
            b.Name = "Cand_" + it.Uid;
            list.AddChild(b);
        }
        // why the rest of the bag does not show
        var refused = h.D.Bag.Where(x => x.Kind != ItemKind.Gem && GolemForge.Refusal(h, x) != null).GroupBy(x => GolemForge.Refusal(h, x)).Select(g => $"{g.Count()} 件：{g.Key}").ToList();
        foreach (var r in refused.Take(4)) v.AddChild(UiTheme.Label(r, UiTheme.FsCaption, UiTheme.Dim));
        var cancel = UiTheme.Button("取消", ClosePopups, UiTheme.FsRow);
        cancel.FocusMode = FocusModeEnum.None;
        v.AddChild(cancel);
        _picker = box;
    }

    public void Confirm(Item it)
    {
        var h = Game.I?.Hero;
        if (h == null || it == null) return;
        if (_picker != null && IsInstanceValid(_picker)) _picker.Visible = false;
        _pending = it;
        var (box, v) = Popup("確認鑄造", 680f);
        v.AddChild(UiTheme.Label($"{Names.Rarity(it.Rarity)}　{it.Name}", UiTheme.FsName, Palette.Rarity(it.Rarity), true));
        var pv = UiTheme.Label(string.Join("\n", GolemForge.Preview(h, it)), UiTheme.FsRow, UiTheme.Text);
        pv.AutowrapMode = TextServer.AutowrapMode.WordSmart; pv.CustomMinimumSize = new Vector2(620f, 0f);
        v.AddChild(pv);
        v.AddChild(UiTheme.Label("此物品將被熔入魔像，無法取回。", UiTheme.FsRow, Palette.Danger));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        var ok = UiTheme.Button("確認鑄造", DoForge, UiTheme.FsRow); ok.Name = "ConfirmForge"; ok.FocusMode = FocusModeEnum.None;
        var no = UiTheme.Button("取消", ClosePopups, UiTheme.FsRow); no.FocusMode = FocusModeEnum.None;
        row.AddChild(ok); row.AddChild(no);
        v.AddChild(row);
        _confirm = box;
    }

    public void DoForge()
    {
        var h = Game.I?.Hero;
        var it = _pending;
        ClosePopups();
        if (h == null || it == null) return;
        if (!GolemForge.Forge(h, it, out var why)) { Snd.Ui("ui_error"); Game.I.Hud?.Warn(why); }
        else Game.I.Hud?.Toast($"鐵魔像以「{it.Name}」重新鑄成。", Palette.Gold);
        Rebuild();
    }

    public void ClosePopups()
    {
        foreach (var c in new[] { _picker, _confirm }) if (c != null && IsInstanceValid(c)) { c.GetParent()?.RemoveChild(c); c.QueueFree(); }
        _picker = null; _confirm = null; _pending = null;
    }

    private (Control box, VBoxContainer v) Popup(string title, float width)
    {
        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dim.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, 0f) };
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTheme.Panel, UiTheme.Gold.Darkened(0.35f), 2, 6));
        center.AddChild(panel);
        var pad = new MarginContainer();
        foreach (var s in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" }) pad.AddThemeConstantOverride(s, 22);
        panel.AddChild(pad);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        pad.AddChild(v);
        v.AddChild(UiTheme.Label(title, UiTheme.FsTitle, UiTheme.Gold, true));
        return (dim, v);
    }
}

/// <summary>Small UI pieces of the golem feature used by other panels.</summary>
public static class GolemUi
{
    public static readonly Color ForgeColor = new(0.95f, 0.55f, 0.22f);

    /// <summary>The 「鑄」 badge of an item the iron golem can be forged from (bottom-left corner; bottom-right is the junk mark).</summary>
    public static void ForgeBadge(Control holder)
    {
        var l = UiTheme.Label("鑄", UiTheme.FsCaption, ForgeColor);
        l.Name = "ForgeBadge";
        l.MouseFilter = Control.MouseFilterEnum.Ignore;
        l.AddThemeConstantOverride("line_spacing", 0);
        l.AddThemeConstantOverride("outline_size", 4);
        l.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        holder.AddChild(l);
        l.AnchorLeft = 0f; l.AnchorRight = 0f; l.AnchorTop = 1f; l.AnchorBottom = 1f;
        l.GrowHorizontal = Control.GrowDirection.End;
        l.GrowVertical = Control.GrowDirection.Begin;
        l.OffsetLeft = 5f; l.OffsetRight = 5f; l.OffsetTop = -1f; l.OffsetBottom = -1f;
    }
}
