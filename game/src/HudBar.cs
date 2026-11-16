using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// The bottom-centre cluster of the HUD: life globe on the left, resource globe on the right (with combo points or
/// sigils above it), and between them a forged plate carrying the potion, the six action slots (1-4, then the two
/// mouse buttons), the evade indicator and the experience bar with the level.
/// </summary>
public partial class HudBar : Control
{
    public const float W = 960f, H = 208f;
    private const float SkillSize = 72f, PotionSize = 56f, EvadeSize = 52f, Step = 78f, Group = 14f;
    /// <summary>Design size of the level line under the experience bar (a caption).</summary>
    private const int LevelSize = UiTheme.FsCaption;
    private static readonly string[] KeyNames = { "左鍵", "右鍵", "1", "2", "3", "4" };      // same order as SaveData.Bar
    // left to right on the plate: potion, keys 1-4, the two mouse buttons, evade
    private static readonly float[] SlotX = { 567f, 645f, 247f, 325f, 403f, 481f };
    private const float PotionX = 177f, EvadeX = 731f;

    // computed by Layout() from the font metrics at the current text scale, bottom-up: level line, experience bar,
    // plate (cells + the strip of key caps under them)
    private Rect2 _plate, _xpRect;
    private float _levelTop;
    private readonly Control _guard, _xpHit;

    /// <summary>Top edge of the forged plate inside this control (the buff row sits just above it).</summary>
    public float PlateTop => _plate.Position.Y;

    private readonly HudGlobe _life, _res;
    private readonly HudActionSlot[] _slots = new HudActionSlot[6];
    private readonly HudActionSlot _potion, _evade;
    private readonly StyleBoxFlat _plateBox;
    private readonly UiArt.Piece _plateArt, _xpArt;
    private Hero _hero;
    private float _xp = -1f;
    private int _level = -1, _paragon = -1;
    private string _levelText = "";
    private string _lifeRead = "", _resRead = "";
    private int _lifeKey = int.MinValue, _resKey = int.MinValue;

    public HudBar()
    {
        Name = "HudBar";
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(W, H);
        Size = new Vector2(W, H);

        _plateBox = new StyleBoxFlat
        {
            BgColor = new Color(0.062f, 0.057f, 0.058f, 0.94f), BorderColor = UiTheme.Border, AntiAliasing = true,
            ShadowColor = new Color(0f, 0f, 0f, 0.55f), ShadowSize = 10, ShadowOffset = new Vector2(0f, 3f),
        };
        _plateBox.SetBorderWidthAll(1);
        _plateBox.BorderWidthTop = 2;
        _plateBox.SetCornerRadiusAll(5);
        if (UiTheme.Art)
        {
            _plateArt = UiArt.P("hud_plate");
            _xpArt = UiArt.P("xp_frame");
        }

        // a recessed strip under the slots that swallows clicks, so a stray click on the plate never moves the hero
        _guard = new Control { MouseFilter = MouseFilterEnum.Stop };
        AddChild(_guard);

        _potion = new HudActionSlot(HudActionSlot.SlotKind.Potion, 0, PotionSize, "Q");
        AddChild(_potion);
        for (int i = 0; i < 6; i++)
        {
            _slots[i] = new HudActionSlot(HudActionSlot.SlotKind.Skill, i, SkillSize, KeyNames[i]);
            AddChild(_slots[i]);
        }
        _evade = new HudActionSlot(HudActionSlot.SlotKind.Evade, 0, EvadeSize, "空白");
        AddChild(_evade);

        _xpHit = new Control { MouseFilter = MouseFilterEnum.Stop };
        AddChild(_xpHit);
        UiTheme.Tip(_xpHit, XpTip, true);

        _life = new HudGlobe { Position = new Vector2(0f, H - HudGlobe.Diameter - HudGlobe.PipArea) };
        _life.Setup(new Color(0.74f, 0.07f, 0.06f), new Color(0.78f, 0.9f, 1f), 0f);
        AddChild(_life);
        _res = new HudGlobe { Position = new Vector2(W - HudGlobe.Diameter, H - HudGlobe.Diameter - HudGlobe.PipArea) };
        _res.Setup(new Color(0.2f, 0.45f, 1f), new Color(0.78f, 0.9f, 1f), 1.7f);
        AddChild(_res);
        Layout();
    }

    /// <summary>
    /// Places the plate, the cells, the experience bar and the level line from the real font metrics, so the larger
    /// captions have the room they need at every text scale. Bottom-up: the level line stands on the bottom edge,
    /// the experience bar above it, then the plate holding the cells and the strip of key caps under them.
    /// Called when the bar is built and again by the HUD whenever the text scale changes.
    /// </summary>
    public void Layout()
    {
        Font font = UiTheme.Ui;
        float levelH = MathF.Ceiling(font.GetHeight(UiTheme.Fs(LevelSize)));
        float strip = HudActionSlot.KeyStripHeight;
        _levelTop = H - levelH + 2f;
        _xpRect = new Rect2(186f, _levelTop - 10f, W - 372f, 8f);
        float plateH = 6f + SkillSize + strip + 3f;
        _plate = new Rect2(140f, _xpRect.Position.Y - 6f - plateH, W - 280f, plateH);
        float rowY = _plate.Position.Y + 6f;

        _guard.Position = _plate.Position + new Vector2(34f, 0f);
        _guard.Size = _plate.Size - new Vector2(68f, 0f);
        _xpHit.Position = _xpRect.Position - new Vector2(0f, 4f);
        _xpHit.Size = new Vector2(_xpRect.Size.X, H - _xpRect.Position.Y + 4f);

        // smaller cells are centred on the row; every key cap starts on the same line under the big cells
        float capTop = rowY + SkillSize + 1f;
        Place(_potion, PotionX, rowY + (SkillSize - PotionSize) * 0.5f, capTop);
        for (int i = 0; i < _slots.Length; i++) Place(_slots[i], SlotX[i], rowY, capTop);
        Place(_evade, EvadeX, rowY + (SkillSize - EvadeSize) * 0.5f, capTop);
        QueueRedraw();
    }

    private static void Place(HudActionSlot slot, float x, float y, float capTop)
    {
        slot.Position = new Vector2(x, MathF.Round(y));
        slot.Relayout(capTop - MathF.Round(y));
    }

    private string XpTip()
    {
        var h = Game.I?.Hero;
        if (h == null) return null;
        string dim = UiTheme.Hex(UiTheme.Dim), gold = UiTheme.Hex(UiTheme.Gold);
        if (h.D.Level < Balance.LevelCap)
            return $"[color=#{gold}]等級 {h.D.Level}[/color]\n經驗值 {h.D.Xp:N0} / {Balance.XpToNext(h.D.Level):N0}\n[color=#{dim}]每升一級獲得 1 點技能點。[/color]";
        if (h.D.Paragon < Balance.ParagonCap)
            return $"[color=#{gold}]等級 {h.D.Level}　燼紋 {h.D.Paragon}[/color]\n燼紋經驗 {h.D.ParagonXp:N0} / {Balance.ParagonXp(h.D.Paragon):N0}\n[color=#{dim}]每提升一級燼紋，便能在燼紋盤上多點亮一處。[/color]";
        return $"[color=#{gold}]等級 {h.D.Level}　燼紋 {h.D.Paragon}[/color]\n[color=#{dim}]你體內的燼火已無可再添。[/color]";
    }

    public void Tick(Player p, float dt)
    {
        var hero = p.Hero;
        if (!ReferenceEquals(hero, _hero))
        {
            _hero = hero;
            _res.Setup(Names.ResourceColor(hero.Class), new Color(0.78f, 0.9f, 1f), 1.7f);
            _life.Setup(new Color(0.74f, 0.07f, 0.06f), new Color(0.78f, 0.9f, 1f), 0f);
            _xp = -1f; _level = -1; _lifeKey = _resKey = int.MinValue;
        }

        float max = p.MaxLife;
        float lifeFrac = max > 0f ? Mathf.Clamp(p.Life / max, 0f, 1f) : 0f;
        _life.SetValues(lifeFrac, max > 0f ? Mathf.Clamp(p.Barrier / max, 0f, 1f) : 0f, dt);
        _life.SetPulse(!p.Dead && lifeFrac < 0.35f ? Mathf.Clamp((0.35f - lifeFrac) / 0.3f, 0.15f, 1f) : 0f);
        _res.SetValues(p.ResourceFrac, 0f, dt);

        var cls = hero.Class;
        if (hero.MaxPips > 0)
            _res.SetPips(p.Pips, hero.MaxPips, cls == ClassId.Hunter ? new Color(0.3f, 0.92f, 0.76f) : new Color(1f, 0.42f, 0.3f), cls == ClassId.Hunter);
        else _res.SetPips(0, 0, Colors.White, false);

        if (_life.Hovered)
        {
            int life = Mathf.CeilToInt(p.Life), cap = Mathf.RoundToInt(max), shield = Mathf.RoundToInt(p.Barrier);
            int key = HashCode.Combine(life, cap, shield);
            if (key != _lifeKey)
            {
                _lifeKey = key;
                _lifeRead = shield > 0 ? $"{life} / {cap}\n屏障 {shield}" : $"{life} / {cap}";
                _life.SetReadout(_lifeRead);
            }
        }
        if (_res.Hovered)
        {
            int cur = Mathf.FloorToInt(p.Resource), cap = Mathf.RoundToInt(hero.MaxResource);
            int key = HashCode.Combine(cur, cap, p.Pips, hero.MaxPips);
            if (key != _resKey)
            {
                _resKey = key;
                _resRead = $"{cur} / {cap}\n{Names.Resource(cls)}";
                if (hero.MaxPips > 0) _resRead += $"\n{(cls == ClassId.Hunter ? "連擊點" : "契印")} {p.Pips} / {hero.MaxPips}";
                _res.SetReadout(_resRead);
            }
        }

        for (int i = 0; i < _slots.Length; i++) _slots[i].Tick(p, dt);
        _potion.Tick(p, dt);
        _evade.Tick(p, dt);

        float xp = Mathf.Clamp(hero.XpFraction, 0f, 1f);
        if (Mathf.Abs(xp - _xp) > 0.0015f || hero.D.Level != _level || hero.D.Paragon != _paragon)
        {
            _xp = xp;
            if (hero.D.Level != _level || hero.D.Paragon != _paragon)
            {
                _level = hero.D.Level; _paragon = hero.D.Paragon;
                _levelText = _level >= Balance.LevelCap ? $"等級 {_level}　·　燼紋 {_paragon}" : $"等級 {_level}";
            }
            QueueRedraw();
        }
    }

    public override void _Draw()
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
        }
        if (_xp > 0f)
        {
            float w = XpRect.Size.X * _xp;
            DrawRect(new Rect2(XpRect.Position, w, XpRect.Size.Y), UiTheme.Gold.Darkened(0.28f));
            DrawRect(new Rect2(XpRect.Position, w, 2f), new Color(1f, 0.88f, 0.6f, 0.75f));
            DrawRect(new Rect2(XpRect.Position.X + w - 2f, XpRect.Position.Y, 2f, XpRect.Size.Y), new Color(1f, 0.95f, 0.8f, 0.9f));
        }
        for (int i = 1; i < 10; i++)
            DrawRect(new Rect2(XpRect.Position.X + XpRect.Size.X * i / 10f, XpRect.Position.Y, 1f, XpRect.Size.Y), new Color(0f, 0f, 0f, 0.55f));
        if (_xpArt != null) UiArt.Nine(this, _xpArt, new Rect2(XpRect.Position.X - 14f, XpRect.Position.Y - 3f, XpRect.Size.X + 28f, XpRect.Size.Y + 6f));

        if (_levelText.Length > 0)
        {
            var font = UiTheme.Ui;
            int px = UiTheme.Fs(LevelSize);
            var pos = new Vector2(XpRect.Position.X, _levelTop + font.GetAscent(px));
            DrawStringOutline(font, pos, _levelText, HorizontalAlignment.Center, XpRect.Size.X, px, 5, new Color(0f, 0f, 0f, 0.85f));
            DrawString(font, pos, _levelText, HorizontalAlignment.Center, XpRect.Size.X, px, new Color(UiTheme.Gold, 0.95f));
        }
    }
}
