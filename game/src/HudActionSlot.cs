using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// One square of the action bar. As a skill slot it shows the bound skill's glyph, a cooldown sweep with the
/// seconds left, the charge count, the key, and — when the skill cannot be cast — a dimmed glyph with the reason.
/// The same control also serves as the potion slot (count, Q) and the evade indicator (cooldown, Space).
/// Hovering shows a tooltip; clicking a skill slot opens the skills panel.
/// </summary>
public partial class HudActionSlot : Control
{
    public enum SlotKind { Skill, Potion, Evade }

    private static readonly Dictionary<int, ImageTexture> SweepTextures = new();
    private static StyleBox _boxNormal, _boxHover, _boxUlt, _boxEmpty, _boxLack;
    private static readonly string[] Tenths = new string[100];      // "0.0" … "9.9"
    private static readonly string[] Whole = new string[200];       // "0" … "199"

    private readonly SlotKind _kind;
    private readonly int _index;
    private readonly float _size;
    private readonly string _keyName;
    private readonly Panel _bg;
    private readonly TextureRect _icon;
    private readonly TextureProgressBar _sweep;
    private readonly ColorRect _flash;
    private readonly Label _key, _cdText, _count;

    private SkillDef _def;
    private string _skillId, _iconName;
    private Color _tint = Colors.White;
    private bool _hover, _dim, _wasDown, _bound, _lack;
    private float _flashA, _sweepVal = -1f, _sweepAlpha = -1f;
    private int _cdShown = int.MinValue, _countShown = int.MinValue;
    private string _lackWhy;
    private StyleBox _style;

    public HudActionSlot() : this(SlotKind.Skill, 0, 64f, "") { }

    public HudActionSlot(SlotKind kind, int index, float size, string keyName)
    {
        _kind = kind; _index = index; _size = size; _keyName = keyName ?? "";
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(size, size);
        Size = new Vector2(size, size);
        EnsureStyles();

        _bg = new Panel { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(size, size) };
        AddChild(_bg);
        SetStyle(_boxEmpty);

        float pad = Mathf.Round(size * 0.11f);
        // the forged cell has a rim of about a tenth of its size: sweep and flash stay on the floor inside it
        float inset = UiTheme.Art ? MathF.Round(size * 0.09f) : 2f;
        _icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Position = new Vector2(pad, pad), Size = new Vector2(size - pad * 2f, size - pad * 2f), Visible = false,
        };
        AddChild(_icon);

        _sweep = new TextureProgressBar
        {
            MinValue = 0.0, MaxValue = 1.0, Step = 0.0, Value = 0.0,
            FillMode = (int)TextureProgressBar.FillModeEnum.CounterClockwise,
            TextureProgress = SweepTexture((int)(size - inset * 2f)), MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(inset, inset), Visible = false,
        };
        AddChild(_sweep);

        _flash = new ColorRect { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(inset, inset), Size = new Vector2(size - inset * 2f, size - inset * 2f), Visible = false };
        AddChild(_flash);

        // The key cap is a caption (20 design px, never smaller). At that size it no longer fits in a corner of the
        // glyph, so it sits in its own strip under the cell, centred on it; HudBar reserves the strip (Relayout).
        _key = UiTheme.Outlined(UiTheme.Small(_keyName, KeySize, new Color(0.8f, 0.76f, 0.68f), true), 5);
        _key.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_key);

        _cdText = UiTheme.Outlined(UiTheme.Small("", size >= 60f ? 26 : 22, Colors.White, true), 6);
        _cdText.HorizontalAlignment = HorizontalAlignment.Center;
        _cdText.VerticalAlignment = VerticalAlignment.Center;
        _cdText.ClipText = true;
        _cdText.Visible = false;
        AddChild(_cdText);

        _count = UiTheme.Outlined(UiTheme.Small("", CountSize, UiTheme.Gold, true), 5);
        _count.HorizontalAlignment = HorizontalAlignment.Right;
        _count.VerticalAlignment = VerticalAlignment.Bottom;
        _count.ClipText = true;
        _count.Visible = false;
        AddChild(_count);

        if (kind == SlotKind.Potion) Bind(Icons.Get("heart-bottle"), new Color(0.95f, 0.3f, 0.25f));
        else if (kind == SlotKind.Evade) Bind(Icons.Get("dodging"), UiTheme.Text);

        MouseEntered += OnEnter;
        MouseExited += OnExit;
        GuiInput += OnGui;
        Relayout(size + 1f);
    }

    /// <summary>Design sizes (px at 1080p) of the cell's captions; all go through UiTheme.Fs inside UiTheme.Small.</summary>
    public const int KeySize = UiTheme.FsCaption, CountSize = UiTheme.FsCaption;

    /// <summary>Height of the key-cap strip under a row of cells at the current text scale.</summary>
    public static float KeyStripHeight => MathF.Ceiling(UiTheme.UiBold.GetHeight(UiTheme.Fs(KeySize))) - 4f;

    /// <summary>
    /// Places the labels from the font's real metrics. <paramref name="keyTop"/> is where the key-cap strip starts,
    /// in this cell's own coordinates (the bar aligns the caps of cells of different sizes on one line).
    /// Called by the bar when it is built and whenever the text scale changes.
    /// </summary>
    public void Relayout(float keyTop)
    {
        float size = _size;
        Font bold = UiTheme.UiBold;
        int keyPx = UiTheme.Fs(KeySize);
        float keyW = MathF.Ceiling(bold.GetStringSize(_keyName, HorizontalAlignment.Left, -1f, keyPx).X) + 2f;
        float keyH = MathF.Ceiling(bold.GetHeight(keyPx));
        _key.Size = new Vector2(keyW, keyH);
        _key.Position = new Vector2(MathF.Round((size - keyW) * 0.5f), MathF.Round(keyTop - 3f));

        _cdText.Position = Vector2.Zero;
        _cdText.Size = new Vector2(size, size);

        // the count hugs the bottom-right corner; the line box of the font has air under the digits, so it may hang
        // a few pixels below the cell without the digits leaving it
        float countH = MathF.Ceiling(bold.GetHeight(UiTheme.Fs(CountSize)));
        _count.Size = new Vector2(size - 5f, countH);
        _count.Position = new Vector2(0f, size - countH + MathF.Round(countH * 0.14f));

    }

    private static void EnsureStyles()
    {
        if (_boxNormal != null) return;
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
        StyleBoxFlat make(Color bg, Color border, int px)
        {
            var s = new StyleBoxFlat { BgColor = bg, BorderColor = border, AntiAliasing = true };
            s.SetBorderWidthAll(px);
            s.SetCornerRadiusAll(4);
            return s;
        }
        var well = new Color(0.03f, 0.028f, 0.032f, 0.94f);
        _boxNormal = make(well, UiTheme.Border, 2);
        _boxHover = make(new Color(0.07f, 0.06f, 0.055f, 0.96f), UiTheme.Gold, 2);
        _boxUlt = make(well, UiTheme.Gold.Darkened(0.2f), 2);
        _boxEmpty = make(new Color(0.03f, 0.028f, 0.032f, 0.8f), UiTheme.Border.Darkened(0.35f), 2);
        _boxLack = make(new Color(0.05f, 0.022f, 0.022f, 0.94f), new Color(0.42f, 0.13f, 0.1f), 2);
    }

    private static ImageTexture SweepTexture(int px)
    {
        px = Math.Max(8, px);
        if (SweepTextures.TryGetValue(px, out var t)) return t;
        var data = new byte[px * px * 4];
        for (int i = 3; i < data.Length; i += 4) data[i] = 190;       // black at 75 % opacity
        t = ImageTexture.CreateFromImage(Image.CreateFromData(px, px, false, Image.Format.Rgba8, data));
        SweepTextures[px] = t;
        return t;
    }

    private void SetStyle(StyleBox s)
    {
        if (ReferenceEquals(s, _style)) return;
        _style = s;
        _bg.AddThemeStyleboxOverride("panel", s);
    }

    private void Bind(Texture2D tex, Color tint)
    {
        _icon.Texture = tex;
        _tint = Icons.Tint(tex, tint);          // raster art keeps its colours (potion, evade and skills alike)
        _icon.Modulate = _tint;
        _icon.Visible = true;
        _bound = true;
        _dim = false;
    }

    private void Unbind()
    {
        _bound = false;
        _def = null; _skillId = null; _iconName = null; _lack = false; _lackWhy = null;
        _icon.Visible = false;
        SetSweep(0f, false);
        SetCooldownText(-1f);
        SetCount(int.MinValue);
    }

    // ───────────────────────── per-frame ─────────────────────────

    public void Tick(Player p, float dt)
    {
        switch (_kind)
        {
            case SlotKind.Skill: TickSkill(p); break;
            case SlotKind.Potion: TickPotion(p); break;
            default: TickEvade(p); break;
        }
        if (_flashA > 0f)
        {
            _flashA = Mathf.Max(0f, _flashA - dt * 3.2f);
            _flash.Color = new Color(1f, 0.9f, 0.72f, _flashA * 0.55f);
            _flash.Visible = _flashA > 0f;
        }
        if (_kind != SlotKind.Skill || _bound)
            SetStyle(_hover ? _boxHover : _lack && _kind == SlotKind.Skill ? _boxLack : _def != null && _def.Cat == SkillCat.Ultimate ? _boxUlt : _boxNormal);
        else SetStyle(_hover ? _boxHover : _boxEmpty);
    }

    private void TickSkill(Player p)
    {
        var hero = p.Hero;
        string[] bar = hero.D.Bar;
        string id = bar != null && _index < bar.Length ? bar[_index] : null;
        SkillDef def = id != null && hero.Rank(id) > 0 ? hero.Eff(id) : null;
        if (def == null)
        {
            if (_bound) { Unbind(); if (_hover) ShowTip(); }
            return;
        }
        bool changed = def.Id != _skillId || def.Icon != _iconName;
        _def = def;
        if (changed)
        {
            _skillId = def.Id; _iconName = def.Icon;
            Color el = def.Element == Element.Physical ? UiTheme.Text : Palette.Element(def.Element).Lerp(UiTheme.Text, 0.2f);
            Bind(Icons.ForSkill(def), el);
            _wasDown = false;
            if (_hover) ShowTip();
        }

        var cd = p.CdOf(def);
        bool hasCd = def.Cooldown > 0f;
        bool down = hasCd && cd.Charges <= 0;
        float frac = hasCd && cd.Charges < cd.MaxCharges && cd.Max > 0.01f ? Mathf.Clamp(cd.Left / cd.Max, 0f, 1f) : 0f;
        string why = p.Dead ? "" : p.WhyCant(def);
        SetSweep(frac, down);
        SetCooldownText(down ? cd.Left : -1f);
        SetCount(cd.MaxCharges > 1 ? cd.Charges : int.MinValue);
        // out of resource (or sigils): the glyph dims and the cell takes a dull red frame; the words come with the
        // warning when the player tries to cast, and in the tooltip - never as text over the glyph
        _lack = why != null && !down && why.Length > 0;
        _lackWhy = _lack ? why : null;
        SetDim(why != null);
        if (_wasDown && !down) _flashA = 1f;
        _wasDown = down;
    }

    private void TickPotion(Player p)
    {
        int n = p.Potions;
        SetCount(n);
        SetDim(n <= 0);
    }

    private void TickEvade(Player p)
    {
        bool down = p.EvadeCd > 0f;
        float frac = down && p.EvadeMax > 0.01f ? Mathf.Clamp(p.EvadeCd / p.EvadeMax, 0f, 1f) : 0f;
        SetSweep(frac, down);
        SetCooldownText(down && p.EvadeCd > 0.95f ? p.EvadeCd : -1f);
        SetDim(down);
        if (_wasDown && !down) _flashA = 1f;
        _wasDown = down;
    }

    private void SetSweep(float frac, bool blocked)
    {
        float alpha = blocked ? 1f : 0.45f;
        if (Mathf.Abs(frac - _sweepVal) > 0.002f || (frac <= 0f && _sweepVal > 0f))
        {
            _sweepVal = frac;
            _sweep.Value = frac;
            _sweep.Visible = frac > 0f;
        }
        if (alpha != _sweepAlpha)
        {
            _sweepAlpha = alpha;
            _sweep.Modulate = new Color(1f, 1f, 1f, alpha);
        }
    }

    private void SetCooldownText(float seconds)
    {
        int code = seconds < 0f ? -1 : seconds >= 9.95f ? 1000 + Mathf.CeilToInt(seconds) : Mathf.Clamp(Mathf.CeilToInt(seconds * 10f), 0, 99);
        if (code == _cdShown) return;
        _cdShown = code;
        if (code < 0) { _cdText.Visible = false; return; }
        _cdText.Text = code >= 1000 ? (code - 1000 < Whole.Length ? Whole[code - 1000] : (code - 1000).ToString()) : Tenths[code];
        _cdText.Visible = true;
    }

    private void SetCount(int n)
    {
        if (n == _countShown) return;
        _countShown = n;
        if (n == int.MinValue) { _count.Visible = false; return; }
        _count.Text = n >= 0 && n < Whole.Length ? Whole[n] : n.ToString();
        _count.AddThemeColorOverride("font_color", n > 0 ? UiTheme.Gold : new Color(0.75f, 0.3f, 0.25f));
        _count.Visible = true;
    }

    private void SetDim(bool dim)
    {
        if (dim == _dim) return;
        _dim = dim;
        _icon.Modulate = dim ? new Color(_tint.R * 0.42f, _tint.G * 0.42f, _tint.B * 0.42f, 0.9f) : _tint;
    }

    // ───────────────────────── pointer ─────────────────────────

    private void OnEnter()
    {
        _hover = true;
        ShowTip();
    }

    private void OnExit()
    {
        _hover = false;
        Game.I?.Ui?.HideTooltipOf(this);
    }

    private void OnGui(InputEvent e)
    {
        if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left) return;
        AcceptEvent();
        if (_kind != SlotKind.Skill) return;
        Snd.Ui("ui_click");
        var ui = Game.I?.Ui;
        if (ui != null && !ui.IsOpen("skills")) ui.Open("skills");
    }

    private void ShowTip()
    {
        var g = Game.I;
        var ui = g?.Ui;
        if (ui == null) return;
        string bb;
        try { bb = TipText(g); }
        catch (Exception e) { UiSafe.Report("action slot tooltip", e); return; }
        if (string.IsNullOrEmpty(bb)) { ui.HideTooltipOf(this); return; }
        ui.ShowTooltipAbove(GetGlobalRect().Grow(2f), bb, this);
    }

    /// <summary>
    /// The tooltip's BBCode. Its [font_size] tags carry final sizes (UiTheme.Fs written into the tag here, and inside
    /// SkillText for the skill card), so the string must not be passed through UiTheme.Bb afterwards.
    /// </summary>
    private string TipText(Game g)
    {
        string dim = UiTheme.Hex(UiTheme.Dim), gold = UiTheme.Hex(UiTheme.Gold);
        int name = UiTheme.Fs(UiTheme.FsName);
        var p = g.Player;
        switch (_kind)
        {
            case SlotKind.Potion:
            {
                string have = p != null && IsInstanceValid(p) ? $"\n[color=#{dim}]持有 {p.Potions} / {p.Hero.D.PotionCap}[/color]" : "";
                return $"[font_size={name}][color=#{gold}]餘燼藥劑[/color][/font_size]\n飲下後立即恢復 35% 生命上限。\n每 30 秒自行補滿一瓶，升級時全數補滿。{have}\n[color=#{dim}]按鍵：{_keyName}[/color]";
            }
            case SlotKind.Evade:
            {
                string cd = p != null && IsInstanceValid(p) ? $"{p.EvadeMax:0.#}" : "5";
                return $"[font_size={name}][color=#{gold}]閃避[/color][/font_size]\n朝移動方向或游標方向翻滾，途中不受傷害，並掙脫一切束縛。\n[color=#{dim}]冷卻時間 {cd} 秒　按鍵：{_keyName}[/color]";
            }
        }
        var hero = g.Hero;
        if (_def == null || hero == null)
            return $"[font_size={name}][color=#{gold}]空的技能欄[/color][/font_size]\n[color=#{dim}]點擊開啟技能頁（K），將學會的技能指派到此欄。　按鍵：{_keyName}[/color]";
        string body;
        try { body = SkillText.Tooltip(_def, hero); }
        catch (Exception e)
        {
            UiSafe.Report("skill tooltip text", e);
            string desc = (_def.Desc ?? "").Replace("{0}", _def.DmgPct(hero.Rank(_def.Id)).ToString());
            body = $"[font_size={name}][color=#{gold}]{_def.Name}[/color][/font_size]\n{desc}";
        }
        string lack = _lackWhy != null ? $"\n[color=#{UiTheme.Hex(Palette.Bad)}]目前無法施放：{_lackWhy}[/color]" : "";
        return body + lack + $"\n[color=#{dim}]按鍵：{_keyName}　點擊開啟技能頁[/color]";
    }
}
