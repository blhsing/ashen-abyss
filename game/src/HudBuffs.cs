using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// The row of status icons above the action bar: the hero's active buffs (gold frame) followed by the afflictions
/// on them (red frame), each with its remaining seconds and stack count. Hovering an icon names it.
/// Drawn in one pass; the control resizes itself to the icons so it never blocks clicks elsewhere.
/// </summary>
public partial class HudBuffs : Control
{
    public const float IconSize = 44f, Gap = 8f;
    /// <summary>Design size of the stack count and the seconds under an icon (counters: caption size).</summary>
    private const int CountSize = UiTheme.FsCaption;
    /// <summary>Height of the row at the current text scale: the icon plus the line of seconds under it.</summary>
    public static float RowHeight => IconSize + MathF.Ceiling(UiTheme.UiBold.GetHeight(UiTheme.Fs(CountSize))) - 2f;
    private const int Max = 18;

    private struct Entry
    {
        public Texture2D Tex;
        public Color Tint;
        public float Left;
        public int Stacks;
        public bool Bad, Aura;
        public string Name;
    }

    private static readonly (StatusId id, string name, string icon, Color tint)[] Afflictions =
    {
        (StatusId.Stun, "暈眩", "knockout", new Color(1f, 0.9f, 0.5f)),
        (StatusId.Freeze, "凍結", "ice-cube", new Color(0.6f, 0.88f, 1f)),
        (StatusId.Root, "定身", "root-tip", new Color(0.7f, 0.85f, 0.45f)),
        (StatusId.Fear, "恐懼", "terror", new Color(0.8f, 0.6f, 1f)),
        (StatusId.Burn, "燃燒", "flame", new Color(1f, 0.55f, 0.2f)),
        (StatusId.Poison, "中毒", "poison-bottle", new Color(0.6f, 0.95f, 0.3f)),
        (StatusId.Bleed, "流血", "bleeding-wound", new Color(0.95f, 0.3f, 0.3f)),
        (StatusId.Rot, "腐朽", "skull-crossed-bones", new Color(0.7f, 0.45f, 0.95f)),
        (StatusId.Chill, "冰緩", "snowflake-1", new Color(0.6f, 0.88f, 1f)),
        (StatusId.Slow, "緩速", "snail", new Color(0.8f, 0.78f, 0.7f)),
        (StatusId.Vulnerable, "易傷", "broken-shield", new Color(0.95f, 0.6f, 0.95f)),
        (StatusId.Weaken, "虛弱", "broken-bone", new Color(0.85f, 0.8f, 0.7f)),
    };

    private static readonly string[] Seconds = new string[100];
    private static readonly string[] Minutes = new string[61];       // "1分" … "60分" for what lasts longer than 99 seconds
    private static readonly string[] StackText = new string[21];

    private readonly Entry[] _e = new Entry[Max];
    private readonly Dictionary<string, Texture2D> _icons = new();
    private readonly Texture2D[] _afflictionIcons = new Texture2D[Afflictions.Length];
    private int _n, _hover = -1, _shownHover = -1;
    private float _redraw;
    private string _hoverName;

    public HudBuffs()
    {
        Name = "HudBuffs";
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        Size = new Vector2(0f, RowHeight);
        Visible = false;
        for (int i = 0; i < Seconds.Length; i++) Seconds[i] = i.ToString();
        for (int i = 0; i < Minutes.Length; i++) Minutes[i] = i + "分";
        for (int i = 0; i < StackText.Length; i++) StackText[i] = "×" + i;
        MouseExited += () => { _hover = -1; SyncTip(); };
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseMotion mm) return;
        int i = (int)(mm.Position.X / (IconSize + Gap));
        _hover = i >= 0 && i < _n && mm.Position.X - i * (IconSize + Gap) <= IconSize ? i : -1;
        SyncTip();
    }

    private void SyncTip()
    {
        var ui = Game.I?.Ui;
        if (ui == null) return;
        if (_hover < 0 || _hover >= _n)
        {
            if (_shownHover >= 0) { _shownHover = -1; _hoverName = null; ui.HideTooltipOf(this); }
            return;
        }
        ref Entry en = ref _e[_hover];
        if (_shownHover == _hover && en.Name == _hoverName) return;
        _shownHover = _hover; _hoverName = en.Name;
        string col = UiTheme.Hex(en.Bad ? Palette.Bad : UiTheme.Gold);
        string bb = $"[color=#{col}]{en.Name}[/color]";
        if (en.Stacks > 1) bb += $"  ×{en.Stacks}";
        var at = GlobalPosition + new Vector2(_hover * (IconSize + Gap), -6f);
        ui.ShowTooltipAbove(new Rect2(at, IconSize, IconSize), bb, this);
    }

    /// <summary>Collects what is active on the hero and lays the row out, centred on its anchor.</summary>
    public void Tick(Player p, float dt)
    {
        int n = 0;
        var buffs = p.Hero.Buffs;
        for (int i = 0; i < buffs.Count && n < Max; i++)
        {
            var b = buffs[i];
            var d = b.Def;
            if (d == null) continue;
            string name = d.Name ?? "";
            // one skill often grants several stat buffs under the same name: show a single icon for them
            bool merged = false;
            for (int j = 0; j < n; j++)
                if (!_e[j].Bad && _e[j].Name == name && Mathf.Abs(_e[j].Left - b.Left) < 0.15f) { merged = true; break; }
            if (merged) continue;
            ref Entry en = ref _e[n++];
            en.Tex = IconFor(d); en.Tint = BuffTint(d); en.Left = b.Left; en.Stacks = b.Stacks; en.Bad = false; en.Aura = d.Aura; en.Name = name;
        }
        if (p.StealthLeft > 0f && n < Max && !p.Hero.BuffFlag(static b => b.Stealth))
        {
            ref Entry en = ref _e[n++];
            en.Tex = Icons.Get("invisible"); en.Tint = new Color(0.7f, 0.8f, 0.95f); en.Left = p.StealthLeft; en.Stacks = 1; en.Bad = false; en.Aura = false; en.Name = "潛行";
        }
        for (int i = 0; i < Afflictions.Length && n < Max; i++)
        {
            float left = p.StTime[(int)Afflictions[i].id];
            if (left <= 0f) continue;
            ref Entry en = ref _e[n++];
            en.Tex = _afflictionIcons[i] ??= Icons.Get(Afflictions[i].icon);
            en.Tint = Afflictions[i].tint; en.Left = left; en.Stacks = 1; en.Bad = true; en.Aura = false; en.Name = Afflictions[i].name;
        }

        if (n != _n)
        {
            _n = n;
            float w = n > 0 ? n * (IconSize + Gap) - Gap : 0f;
            // anchored at the bottom centre by the HUD: keep the row centred as it grows and shrinks
            OffsetLeft = -w * 0.5f;
            OffsetRight = w * 0.5f;
            Visible = n > 0;
            if (_hover >= n) { _hover = -1; SyncTip(); }
            _redraw = 0f;
        }
        if (n == 0) return;
        _redraw -= dt;
        if (_redraw <= 0f)
        {
            _redraw = 0.1f;
            QueueRedraw();
            if (_hover >= 0) SyncTip();
        }
    }

    private Texture2D IconFor(BuffDef d)
    {
        string id = d.Id ?? "";
        if (_icons.TryGetValue(id, out var t)) return t;
        // skill buffs are named "<skill id>_<suffix>": borrow the skill's own glyph
        SkillDef skill = SkillDb.Get(id);
        int cut = id.LastIndexOf('_');
        while (skill == null && cut > 0)
        {
            skill = SkillDb.Get(id.Substring(0, cut));
            cut = cut > 1 ? id.LastIndexOf('_', cut - 1) : -1;
        }
        t = skill != null ? Icons.ForSkill(skill) : Icons.Get(GenericIcon(d));
        _icons[id] = t;
        return t;
    }

    private static string GenericIcon(BuffDef d)
    {
        if (d.Stealth) return "invisible";
        if (d.Immune) return "eye-shield";
        if (d.Deathless) return "crowned-heart";
        if (d.Unstoppable) return "strong";
        if (d.FreeCast || d.FreePips) return "magic-swirl";
        if (d.LifeSteal > 0f) return "bleeding-heart";
        if (d.RegenPct > 0f) return "healing";
        if (d.Burn > 0f) return "small-fire";
        return d.Stat switch
        {
            Stat.DmgAll or Stat.DmgPhysical or Stat.DmgBasic or Stat.DmgCore or Stat.DmgUlt => "crossed-swords",
            Stat.DmgFire => "flame", Stat.DmgFrost => "snowflake-2", Stat.DmgLightning => "lightning-frequency",
            Stat.DmgPoison => "poison-bottle", Stat.DmgShadow => "cursed-star",
            Stat.AttackSpeed => "lightning-frequency", Stat.MoveSpeed => "wingfoot",
            Stat.DmgReduction or Stat.ResAll => "shield", Stat.Armor => "armor-vest", Stat.BarrierPct => "bolt-shield",
            Stat.CritChance => "on-target", Stat.CritDmg => "bullseye", Stat.Dodge => "dodging",
            Stat.LifeRegen or Stat.Healing or Stat.LifeOnHit or Stat.LifeOnKill => "healing", Stat.MaxLife or Stat.LifePct => "hearts",
            Stat.Cdr => "hourglass", Stat.ResourceGen or Stat.MaxResource or Stat.ResourceCost => "embrassed-energy",
            _ => "upgrade"
        };
    }

    private static Color BuffTint(BuffDef d)
    {
        if (d.Stealth) return new Color(0.7f, 0.8f, 0.95f);
        if (d.Burn > 0f || d.Stat == Stat.DmgFire) return Palette.Element(Element.Fire);
        return d.Stat switch
        {
            Stat.DmgFrost => Palette.Element(Element.Frost), Stat.DmgLightning => Palette.Element(Element.Lightning),
            Stat.DmgPoison => Palette.Element(Element.Poison), Stat.DmgShadow => Palette.Element(Element.Shadow),
            Stat.DmgReduction or Stat.Armor or Stat.ResAll or Stat.BarrierPct => new Color(0.75f, 0.85f, 1f),
            Stat.MoveSpeed or Stat.AttackSpeed or Stat.Dodge => new Color(0.75f, 1f, 0.8f),
            Stat.LifeRegen or Stat.Healing or Stat.MaxLife or Stat.LifePct => new Color(1f, 0.6f, 0.55f),
            _ => new Color(1f, 0.88f, 0.62f)
        };
    }

    private readonly UiArt.Piece _frameArt = UiTheme.Art ? UiArt.P("buff_frame") : null;

    public override void _Draw()
    {
        if (_n <= 0) return;
        var font = UiTheme.UiBold;
        int px = UiTheme.Fs(CountSize);
        float ascent = font.GetAscent(px), descent = font.GetDescent(px);
        var well = new Color(0.03f, 0.028f, 0.032f, 0.88f);
        for (int i = 0; i < _n; i++)
        {
            ref Entry en = ref _e[i];
            float x = i * (IconSize + Gap);
            var r = new Rect2(x, 0f, IconSize, IconSize);
            bool hot = i == _hover;
            DrawRect(r, well);
            if (en.Tex != null) DrawTextureRect(en.Tex, r.Grow(-4f), false, Icons.Tint(en.Tex, en.Tint));
            Color frame = en.Bad ? new Color(0.85f, 0.25f, 0.2f, hot ? 1f : 0.85f) : new Color(UiTheme.Gold, hot ? 1f : 0.6f);
            if (_frameArt != null)
                UiArt.Sprite(this, _frameArt, r.Grow(2f), en.Bad ? new Color(1f, 0.5f, 0.45f) : hot ? Colors.White : new Color(0.88f, 0.86f, 0.84f));
            else DrawRect(r, frame, false, hot ? 2f : 1f);

            if (en.Stacks > 1)
            {
                string st = en.Stacks < StackText.Length ? StackText[en.Stacks] : "×" + en.Stacks;
                // bottom-right corner of the icon; the box reaches far to the left so a wide count is never trimmed
                var sp = new Vector2(x - 60f, IconSize - 1f - descent * 0.45f);
                DrawStringOutline(font, sp, st, HorizontalAlignment.Right, IconSize + 58f, px, 5, new Color(0f, 0f, 0f, 0.9f));
                DrawString(font, sp, st, HorizontalAlignment.Right, IconSize + 58f, px, Colors.White);
            }
            if (!en.Aura && en.Left > 0f && en.Left < 3600f)       // an hour or more is as good as permanent: no clock under it
            {
                int sec = Mathf.CeilToInt(en.Left);
                string s = sec < Seconds.Length ? Seconds[sec] : Minutes[Math.Min(Minutes.Length - 1, sec / 60)];
                Color c = en.Left < 3f ? new Color(1f, 0.55f, 0.45f) : UiTheme.Text;
                // the seconds stand on their own line under the icon (measured, not a constant tuned for one size)
                var tp = new Vector2(x - 20f, IconSize - 3f + ascent);
                DrawStringOutline(font, tp, s, HorizontalAlignment.Center, IconSize + 40f, px, 5, new Color(0f, 0f, 0f, 0.9f));
                DrawString(font, tp, s, HorizontalAlignment.Center, IconSize + 40f, px, c);
            }
        }
    }
}
