using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// Top-centre enemy frame. For a boss: name, a wide life bar (with the chunk just lost, a barrier sheen and quarter
/// marks) and the stagger meter beneath it. With no boss around it becomes a slim frame for the enemy under the
/// pointer: name, affixes and life.
/// </summary>
public partial class HudBossBar : Control
{
    public const float W = 760f, H = 104f;
    /// <summary>Design sizes (px at 1080p): a boss's name, the name of the enemy under the pointer, the rank / title line and the stagger tag.</summary>
    private const int BossNameSize = 30, TargetNameSize = 24, SubSize = UiTheme.FsCaption;

    /// <summary>
    /// How far down the frame can reach at the current text scale (name, rank line, life bar, stagger meter and its
    /// tag). The control keeps its nominal size; the HUD uses this to start the toasts below the frame.
    /// </summary>
    public static float FullHeight
    {
        get
        {
            Font title = UiTheme.Title, small = UiTheme.Ui;
            int name = UiTheme.Fs(BossNameSize), sub = UiTheme.Fs(SubSize);
            return MathF.Round(title.GetHeight(name) * 0.92f) + MathF.Round(small.GetHeight(sub) * 0.9f) + 4f + 20f + 9f + 6f + 1f + title.GetHeight(sub);
        }
    }

    private Monster _m;
    private bool _boss;
    private float _alpha, _life = 1f, _ghost = 1f, _hold, _barrier, _stagger, _clock;
    private string _name = "", _sub = "";
    private bool _staggered;
    private readonly Color _frameCol = UiTheme.Gold.Darkened(0.35f);

    /// <summary>The boss currently on the bar (so the world layer does not draw a second bar over its head).</summary>
    public Monster Shown => _boss && _alpha > 0.05f ? _m : null;

    public HudBossBar()
    {
        Name = "HudBossBar";
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(W, H);
        Size = new Vector2(W, H);
        Visible = false;
    }

    /// <summary><paramref name="boss"/> wins over <paramref name="hover"/>; pass null for both to fade the bar out.</summary>
    public void Tick(Monster boss, Monster hover, float dt)
    {
        _clock += dt;
        Monster want = boss ?? hover;
        if (want != null && (!IsInstanceValid(want) || want.Dead)) want = null;
        if (want != null)
        {
            if (!ReferenceEquals(want, _m))
            {
                _m = want;
                _boss = boss != null;
                _name = want.DisplayName ?? "";
                _sub = Describe(want, _boss);
                _life = _ghost = want.MaxLife > 0f ? Mathf.Clamp(want.Life / want.MaxLife, 0f, 1f) : 0f;
                _hold = 0f;
            }
            else _boss = boss != null;
            float f = want.MaxLife > 0f ? Mathf.Clamp(want.Life / want.MaxLife, 0f, 1f) : 0f;
            if (f < _life - 0.002f) _hold = 0.4f;
            _life = f;
            if (_ghost < f) _ghost = f;
            else if (_ghost > f) { if (_hold > 0f) _hold -= dt; else _ghost = Mathf.Max(f, _ghost - dt * 0.4f); }
            _barrier = want.MaxLife > 0f ? Mathf.Clamp(want.Barrier / want.MaxLife, 0f, 1f) : 0f;
            _stagger = Mathf.Clamp(want.Stagger / 100f, 0f, 1f);
            _staggered = want.Staggered;
            _alpha = Mathf.Min(1f, _alpha + dt * 6f);
        }
        else
        {
            if (_m != null && (!IsInstanceValid(_m) || _m.Dead)) { _life = 0f; }
            _alpha = Mathf.Max(0f, _alpha - dt * (_boss ? 1.2f : 5f));
            if (_ghost > _life) _ghost = Mathf.Max(_life, _ghost - dt * 0.6f);
            if (_alpha <= 0f) _m = null;
        }
        bool show = _alpha > 0.01f;
        if (show != Visible) Visible = show;
        if (show) QueueRedraw();
    }

    private static string Describe(Monster m, bool boss)
    {
        if (boss && m.Tier == MonTier.Boss) return "";
        string rank = m.MiniBoss ? "頭目" : m.Tier switch { MonTier.Elite => "菁英", MonTier.Champion => "精銳", _ => "" };
        string title = m.Title ?? "";
        if (rank.Length == 0) return title;
        return title.Length == 0 ? rank : rank + "　" + title;
    }

    public override void _Draw()
    {
        if (_alpha <= 0.01f) return;
        float a = _alpha;
        var title = UiTheme.Title;
        var small = UiTheme.Ui;
        float barW = _boss ? W : 440f;
        float x0 = (W - barW) * 0.5f;
        float barH = _boss ? 20f : 12f;
        int nameSize = UiTheme.Fs(_boss ? BossNameSize : TargetNameSize);
        int subSize = UiTheme.Fs(SubSize);
        float y = 0f;

        // every line is placed from the font's own metrics; the text boxes are wider than the bar (a long name at the
        // largest text size must not be trimmed)
        const float textW = W + 600f, textX = -300f;
        Color nameCol = _boss ? UiTheme.Gold : _m != null && IsInstanceValid(_m) ? TierColor(_m) : UiTheme.Text;
        var np = new Vector2(textX, y + title.GetAscent(nameSize));
        DrawStringOutline(title, np, _name, HorizontalAlignment.Center, textW, nameSize, 6, new Color(0f, 0f, 0f, 0.85f * a));
        DrawString(title, np, _name, HorizontalAlignment.Center, textW, nameSize, new Color(nameCol, a));
        y += MathF.Round(title.GetHeight(nameSize) * 0.92f);
        if (_sub.Length > 0)
        {
            var sp = new Vector2(textX, y + small.GetAscent(subSize) - 4f);
            DrawStringOutline(small, sp, _sub, HorizontalAlignment.Center, textW, subSize, 5, new Color(0f, 0f, 0f, 0.85f * a));
            DrawString(small, sp, _sub, HorizontalAlignment.Center, textW, subSize, new Color(0.9f, 0.7f, 0.45f, a));
            y += MathF.Round(small.GetHeight(subSize) * 0.9f);
        }
        y += 4f;

        var bar = new Rect2(x0, y, barW, barH);
        var frameArt = _boss ? BossArt : TargetArt;
        if (frameArt == null) Framed(bar.Grow(3f), a);
        var inner = bar;
        DrawRect(inner, new Color(0.085f, 0.03f, 0.03f, 0.95f * a));
        if (_ghost > _life) DrawRect(new Rect2(inner.Position.X + inner.Size.X * _life, inner.Position.Y, inner.Size.X * (_ghost - _life), inner.Size.Y), new Color(0.95f, 0.85f, 0.75f, 0.75f * a));
        if (_life > 0f)
        {
            float w = inner.Size.X * _life;
            DrawRect(new Rect2(inner.Position, w, inner.Size.Y), new Color(0.62f, 0.07f, 0.06f, a));
            DrawRect(new Rect2(inner.Position, w, inner.Size.Y * 0.45f), new Color(0.86f, 0.16f, 0.12f, a));
            DrawRect(new Rect2(inner.Position, w, 2f), new Color(1f, 0.55f, 0.45f, 0.7f * a));
        }
        if (_barrier > 0f)
        {
            float w = inner.Size.X * Mathf.Min(1f, _barrier);
            DrawRect(new Rect2(inner.Position, w, inner.Size.Y), new Color(0.75f, 0.9f, 1f, 0.28f * a));
            DrawRect(new Rect2(inner.Position.X, inner.End.Y - 3f, w, 3f), new Color(0.85f, 0.95f, 1f, 0.95f * a));
        }
        if (_boss)
            for (int i = 1; i < 4; i++)
                DrawRect(new Rect2(inner.Position.X + inner.Size.X * i / 4f - 1f, inner.Position.Y, 2f, inner.Size.Y), new Color(0f, 0f, 0f, 0.55f * a));
        // the forged frame lies over the ends of the bar: spear tips with ember stones for a boss, a light frame otherwise
        if (frameArt != null)
            UiArt.Nine(this, frameArt, _boss ? new Rect2(bar.Position.X - 34f, bar.Position.Y - 10f, bar.Size.X + 68f, bar.Size.Y + 20f)
                : new Rect2(bar.Position.X - 9f, bar.Position.Y - 5f, bar.Size.X + 18f, bar.Size.Y + 10f), new Color(1f, 1f, 1f, a));
        y += barH + (frameArt != null && _boss ? 14f : 9f);

        if (!_boss) return;
        // stagger: fills as the boss is controlled or battered; full → the boss reels and takes more damage
        var sb = new Rect2(W * 0.5f - 210f, y, 420f, 6f);
        if (StaggerArt == null) DrawRect(sb.Grow(1.5f), new Color(0f, 0f, 0f, 0.7f * a));
        DrawRect(sb, new Color(0.1f, 0.09f, 0.08f, 0.95f * a));
        if (_staggered)
        {
            // reeling: the whole meter burns bright and breathes, with a soft halo, so the opening cannot be missed
            float pulse = 0.5f + 0.5f * Mathf.Sin(_clock * 10f);
            DrawRect(sb.Grow(3f), new Color(1f, 0.75f, 0.3f, (0.18f + 0.22f * pulse) * a));
            DrawRect(sb, new Color(UiTheme.Gold, a));
            DrawRect(sb, new Color(1f, 0.97f, 0.85f, (0.35f + 0.65f * pulse) * a));
            int tagSize = UiTheme.Fs(SubSize);
            var tp = new Vector2(0f, sb.End.Y + 1f + title.GetAscent(tagSize));
            DrawStringOutline(title, tp, "踉蹌", HorizontalAlignment.Center, W, tagSize, 5, new Color(0f, 0f, 0f, 0.85f * a));
            DrawString(title, tp, "踉蹌", HorizontalAlignment.Center, W, tagSize, new Color(1f, 0.92f, 0.65f, a));
        }
        else if (_stagger > 0f)
        {
            DrawRect(new Rect2(sb.Position, sb.Size.X * _stagger, sb.Size.Y), new Color(UiTheme.Gold, 0.95f * a));
            DrawRect(new Rect2(sb.Position, sb.Size.X * _stagger, 2f), new Color(1f, 0.95f, 0.8f, 0.8f * a));
        }
        if (StaggerArt != null) UiArt.Nine(this, StaggerArt, new Rect2(sb.Position.X - 6f, sb.Position.Y - 3f, sb.Size.X + 12f, sb.Size.Y + 6f), new Color(1f, 1f, 1f, a));
    }

    private static UiArt.Piece _bossArt, _targetArt, _staggerArt;
    private static bool _artLooked;
    private static void LookArt()
    {
        if (_artLooked) return;
        _artLooked = true;
        if (!UiTheme.Art) return;
        _bossArt = UiArt.P("boss_frame"); _targetArt = UiArt.P("target_frame"); _staggerArt = UiArt.P("boss_stagger");
    }
    private static UiArt.Piece BossArt { get { LookArt(); return _bossArt; } }
    private static UiArt.Piece TargetArt { get { LookArt(); return _targetArt; } }
    private static UiArt.Piece StaggerArt { get { LookArt(); return _staggerArt; } }

    /// <summary>The dark plate and thin gold edge around the life bar, faded with the rest of the frame.</summary>
    private void Framed(Rect2 r, float a)
    {
        DrawRect(r.Grow(2f), new Color(0f, 0f, 0f, 0.35f * a));
        DrawRect(r, new Color(0.03f, 0.028f, 0.032f, 0.9f * a));
        DrawRect(r, new Color(_frameCol, a), false, 1f);
    }

    public static Color TierColor(Monster m)
    {
        if (m.MiniBoss) return Palette.Ember;
        return m.Tier switch
        {
            MonTier.Boss => UiTheme.Gold,
            MonTier.Elite => Palette.Rarity(Rarity.Rare),
            MonTier.Champion => new Color(0.55f, 0.72f, 1f),
            MonTier.Minion => new Color(0.5f, 0.95f, 0.8f),
            _ => UiTheme.Text
        };
    }
}
