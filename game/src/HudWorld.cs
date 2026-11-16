using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AshenAbyss;

/// <summary>
/// Everything the HUD pins to the 3D world, projected through the camera and drawn by this one control:
/// enemy health bars and elite nameplates with their affixes, pooled floating damage numbers (white hits, yellow
/// crits, blue overpowers, small grey damage-over-time, red for damage to the hero, 「閃避」 for dodges), loot labels
/// that push each other apart and can be clicked, and the prompt for the object under the pointer.
/// </summary>
public partial class HudWorld : Control
{
    private const int MaxNums = 96, MaxLabels = 48;
    private const float NumW = 420f, LabelPad = 10f;
    /// <summary>
    /// Design sizes (px at 1080p, before the text scale) of everything this layer draws; each goes through
    /// UiTheme.Fs at draw / measure time, so a change of the text-size setting shows on the next frame.
    /// Floating numbers: normal hit, crit, overpower, overpowering crit, minion hit, damage over time, hit on the
    /// hero, damage over time on the hero, words (「閃避」). Nameplates: elite name, plain name, affix line.
    /// Labels: loot / object name, and the hint line under it.
    /// </summary>
    private const int NumNormal = 26, NumCrit = 34, NumOver = 36, NumOverCrit = 40, NumMinion = 22, NumDot = 20, NumHero = 28, NumHeroDot = 20, NumWord = 24;
    private const int PlateName = 22, PlateNameMinor = 20, PlateTitle = 20, LabelSize = 22, LabelSubSize = 20;

    private static readonly Color NormalCol = new(0.96f, 0.95f, 0.92f);
    private static readonly Color CritCol = new(1f, 0.87f, 0.2f);
    private static readonly Color OverCol = new(0.38f, 0.7f, 1f);
    private static readonly Color DotCol = new(0.66f, 0.66f, 0.66f);
    private static readonly Color MinionCol = new(0.72f, 0.9f, 0.84f);
    private static readonly Color HeroCol = new(1f, 0.26f, 0.2f);
    private static readonly Color DodgeCol = new(0.82f, 0.92f, 1f);
    private static readonly string[] SmallInts = new string[1000];

    private struct Num
    {
        public bool On, Pop;
        public Vector3 At;
        public float T, Life, Value, Dx, Rise;
        public string Text;
        public Color Col;
        public int Size;            // DESIGN size; UiTheme.Fs is applied when the number is drawn
        public byte Kind;           // 0 hit, 1 damage over time on an enemy, 2 hit on the hero, 3 damage over time on the hero, 4 word
        public Actor Target;
    }

    private struct Lab
    {
        public Interactable Obj;
        public Rect2 Rect;
        public string Text, Sub;
        public Color Col;
        public bool Loot, Hot, Fine;
    }

    private readonly Num[] _nums = new Num[MaxNums];
    private readonly Lab[] _labs = new Lab[MaxLabels];
    private readonly Dictionary<(string, int), float> _widths = new();
    private readonly Random _rand = new();
    private int _numNext, _numLive, _labCount;
    private bool _active, _hooked;
    private float _heroHit;
    private Player _p;
    private Camera3D _cam;
    private Monster _onBossBar;
    private Interactable _pointer;
    private Font _body, _ui, _bold;

    /// <summary>The object whose label the pointer is resting on (clicking walks there and uses it), or null.</summary>
    public Interactable PointerObj => _pointer;

    public HudWorld()
    {
        Name = "HudWorld";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _EnterTree()
    {
        if (_hooked) return;
        World.Damage += OnDamage;
        _hooked = true;
    }

    public override void _ExitTree()
    {
        if (!_hooked) return;
        World.Damage -= OnDamage;
        _hooked = false;
    }

    /// <summary>The largest hit the hero took since the last call, as a fraction of their life (for the screen edge flash).</summary>
    public float TakeHeroHit()
    {
        float v = _heroHit;
        _heroHit = 0f;
        return v;
    }

    /// <summary>Called instead of Tick while the HUD is hidden: nothing is drawn and nothing can be clicked.</summary>
    public void Idle()
    {
        if (!_active && _labCount == 0 && _pointer == null) return;
        _active = false;
        _labCount = 0;
        _pointer = null;
        _p = null; _cam = null; _onBossBar = null;
        for (int i = 0; i < MaxNums; i++) { _nums[i].On = false; _nums[i].Target = null; _nums[i].Text = null; }
        _numLive = 0;
        QueueRedraw();
    }

    public void Tick(Player p, float dt, bool interactive, bool showAllLoot, Monster onBossBar)
    {
        _active = true;
        _p = p;
        _onBossBar = onBossBar;
        _cam = GetViewport()?.GetCamera3D();
        _body ??= UiTheme.Body; _ui ??= UiTheme.Ui; _bold ??= UiTheme.UiBold;

        if (_numLive > 0)
        {
            int live = 0;
            for (int i = 0; i < MaxNums; i++)
            {
                ref Num n = ref _nums[i];
                if (!n.On) continue;
                n.T += dt;
                if (n.T >= n.Life) { n.On = false; n.Target = null; n.Text = null; }
                else live++;
            }
            _numLive = live;
        }

        if (_cam != null && IsInstanceValid(_cam)) BuildLabels(p, _cam, interactive, showAllLoot);
        else { _labCount = 0; _pointer = null; }
        QueueRedraw();
    }

    // ───────────────────────── damage numbers ─────────────────────────

    private void OnDamage(Actor a, Hit h, float amount)
    {
        try
        {
            if (!_active || a == null) return;
            bool toHero = a is Player;
            if (toHero && amount > 0f) _heroHit = Mathf.Max(_heroHit, amount / Mathf.Max(1f, a.MaxLife));
            var cfg = Game.I?.Cfg;
            if (cfg != null && !cfg.DamageNumbers) return;
            if (amount < 0f)
            {
                if (toHero) Spawn(a, "閃避", DodgeCol, NumWord, 4, 0f, 0.9f, false);
                return;
            }
            if (amount < 0.5f || ReferenceEquals(h.Source, a)) return;
            if (toHero)
            {
                if (h.Dot)
                {
                    if (!Merge(a, amount, 3, "-")) Spawn(a, "-" + Fmt(amount), HeroCol, NumHeroDot, 3, amount, 0.85f, false);
                }
                else Spawn(a, "-" + Fmt(amount), HeroCol, NumHero, 2, amount, 1.05f, amount > a.MaxLife * 0.12f);
                return;
            }
            if (a.Faction != Faction.Monster) return;                 // the hero's own minions take their knocks quietly
            if (h.Dot)
            {
                if (!Merge(a, amount, 1, "")) Spawn(a, Fmt(amount), DotCol, NumDot, 1, amount, 0.8f, false);
            }
            else if (h.Overpower) Spawn(a, Fmt(amount) + "!", OverCol, h.Crit ? NumOverCrit : NumOver, 0, amount, 1.25f, true);
            else if (h.Crit) Spawn(a, Fmt(amount), CritCol, NumCrit, 0, amount, 1.15f, true);
            else Spawn(a, Fmt(amount), h.Minion ? MinionCol : NormalCol, h.Minion ? NumMinion : NumNormal, 0, amount, 0.95f, false);
        }
        catch (Exception e) { UiSafe.Report("damage number", e); }
    }

    /// <summary>Folds a tick of damage over time into the number already floating over the same target.</summary>
    private bool Merge(Actor a, float amount, byte kind, string prefix)
    {
        for (int i = 0; i < MaxNums; i++)
        {
            ref Num n = ref _nums[i];
            if (!n.On || n.Kind != kind || n.T > 0.45f || !ReferenceEquals(n.Target, a)) continue;
            n.Value += amount;
            n.Text = prefix + Fmt(n.Value);
            return true;
        }
        return false;
    }

    private void Spawn(Actor a, string text, Color col, int size, byte kind, float value, float life, bool pop)
    {
        int slot = -1;
        for (int i = 0; i < MaxNums; i++)
        {
            int j = (_numNext + i) % MaxNums;
            if (!_nums[j].On) { slot = j; break; }
        }
        if (slot < 0) slot = _numNext;                                // pool full: the oldest number gives way
        else _numLive++;
        _numNext = (slot + 1) % MaxNums;
        Vector3 at = a.GlobalPosition;
        at.Y += a.HeightM * 0.85f + 0.3f;
        ref Num n = ref _nums[slot];
        n.On = true; n.Pop = pop; n.At = at; n.T = 0f; n.Life = life; n.Value = value; n.Text = text; n.Col = col;
        n.Size = size; n.Kind = kind; n.Target = a;
        n.Dx = ((float)_rand.NextDouble() * 2f - 1f) * (kind == 1 || kind == 3 ? 14f : 30f);
        n.Rise = 56f + (float)_rand.NextDouble() * 24f;
    }

    private static string Fmt(float v)
    {
        if (v < 999.5f)
        {
            int i = Math.Max(0, (int)MathF.Round(v));
            return SmallInts[i] ??= i.ToString(CultureInfo.InvariantCulture);
        }
        if (v < 10000f) return ((int)MathF.Round(v)).ToString(CultureInfo.InvariantCulture);
        if (v < 1e6f) return (v / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + "K";
        return (v / 1e6f).ToString("0.00", CultureInfo.InvariantCulture) + "M";
    }

    // ───────────────────────── labels ─────────────────────────

    /// <summary>Measured width of a label line at the final pixel size <paramref name="px"/> (cached per string, size and face).</summary>
    private float Width(string s, int px, bool small = false)
    {
        var key = (s, small ? -px : px);
        if (_widths.TryGetValue(key, out float w)) return w;
        if (_widths.Count > 400) _widths.Clear();
        w = MathF.Ceiling((small ? _ui : _body).GetStringSize(s, HorizontalAlignment.Left, -1f, px).X);
        _widths[key] = w;
        return w;
    }

    // label metrics at the current text scale, refreshed every frame the labels are built
    private int _labPx, _subPx;
    private float _labLine, _subLine;

    private void LabelMetrics()
    {
        int lab = UiTheme.Fs(LabelSize), sub = UiTheme.Fs(LabelSubSize);
        if (lab == _labPx && sub == _subPx) return;
        _labPx = lab; _subPx = sub;
        // the line box of the CJK faces carries a lot of air: a label line is a little tighter than the font's own height
        _labLine = MathF.Round(_body.GetHeight(lab) * 0.88f);
        _subLine = MathF.Round(_ui.GetHeight(sub) * 0.86f);
    }

    private void BuildLabels(Player p, Camera3D cam, bool interactive, bool showAll)
    {
        int count = 0;
        LabelMetrics();
        Vector2 screen = Size;
        Vector2 hp = p.Pos2;
        Interactable hoverObj = p.HoverObj;
        Interactable sticky = _pointer;
        var all = Interactable.All;
        for (int i = 0; i < all.Count && count < MaxLabels; i++)
        {
            var o = all[i];
            if (o == null || !o.CanUse || !o.ShowLabel) continue;
            string label = o.Label;
            if (string.IsNullOrEmpty(label)) continue;
            if (o.AutoRadius > 0f && !o.AlwaysLabel) continue;        // picked up by walking over it: no label needed
            bool loot = o.Item != null;
            bool hot = ReferenceEquals(o, hoverObj) || ReferenceEquals(o, sticky);
            Rarity rar = loot ? o.Item.Rarity : o.LootRarity;
            Vector3 wp = o.GlobalPosition;
            float dx = wp.X - hp.X, dz = wp.Z - hp.Y;
            float d2 = dx * dx + dz * dz;
            bool near = !loot && d2 < 4.6f * 4.6f;                    // standing next to it: name and what it does
            bool show;
            if (hot || o.AlwaysLabel) show = d2 < 42f * 42f;
            else if (loot) show = showAll ? d2 < 46f * 46f : d2 < 11f * 11f || (rar >= Rarity.Rare && d2 < 30f * 30f);
            else show = near;
            if (!show) continue;
            wp.Y += o.LabelHeight;
            if (cam.IsPositionBehind(wp)) continue;
            Vector2 sp = cam.UnprojectPosition(wp);
            if (sp.X < -80f || sp.X > screen.X + 80f || sp.Y < -30f || sp.Y > screen.Y + 40f) continue;

            string sub = null;
            if (!loot && (hot || near))
            {
                if (!string.IsNullOrEmpty(o.Sub)) sub = o.Sub;        // also carries the progress of a channelled use
                else if (hot) sub = "左鍵　互動";
            }
            float w = Width(label, _labPx);
            if (sub != null) w = Mathf.Max(w, Width(sub, _subPx, true));
            float h = sub != null ? _labLine + _subLine + 2f : _labLine;
            Color col = o.LabelColor;
            if (loot && col == Palette.Text) col = Palette.Rarity(rar);

            ref Lab l = ref _labs[count++];
            l.Obj = o; l.Text = label; l.Sub = sub; l.Col = col; l.Loot = loot; l.Hot = hot; l.Fine = loot && rar >= Rarity.Rare;
            l.Rect = new Rect2(MathF.Round(sp.X - w * 0.5f - LabelPad), MathF.Round(sp.Y - h), w + LabelPad * 2f, h);
        }
        _labCount = count;

        // push overlapping labels upward; earlier (older) labels keep their place so the stack does not jitter
        for (int i = 1; i < count; i++)
        {
            Rect2 a = _labs[i].Rect;
            for (int pass = 0; pass < 10; pass++)
            {
                bool moved = false;
                for (int j = 0; j < i; j++)
                {
                    Rect2 b = _labs[j].Rect;
                    if (a.Position.X < b.End.X + 3f && a.End.X + 3f > b.Position.X && a.Position.Y < b.End.Y + 2f && a.End.Y + 2f > b.Position.Y)
                    {
                        a = new Rect2(a.Position.X, b.Position.Y - a.Size.Y - 3f, a.Size);
                        moved = true;
                    }
                }
                if (!moved) break;
            }
            _labs[i].Rect = a;
        }

        Interactable pointer = null;
        if (interactive)
        {
            Vector2 mouse = Ui.PointerIn(this);
            for (int i = count - 1; i >= 0; i--)
                if (_labs[i].Rect.HasPoint(mouse)) { pointer = _labs[i].Obj; _labs[i].Hot = true; break; }
        }
        _pointer = pointer;
        for (int i = count; i < MaxLabels && _labs[i].Obj != null; i++) { _labs[i].Obj = null; _labs[i].Text = null; _labs[i].Sub = null; }
    }

    // ───────────────────────── drawing ─────────────────────────

    public override void _Draw()
    {
        if (!_active) return;
        var cam = _cam;
        var p = _p;
        if (cam == null || p == null || !IsInstanceValid(cam) || !IsInstanceValid(p) || _body == null) return;
        try
        {
            Vector2 screen = Size;
            DrawEnemyBars(cam, p, screen);
            DrawLabels();
            if (_numLive > 0) DrawNumbers(cam, screen);
        }
        catch (Exception e) { UiSafe.Report("world overlay", e); }
    }

    private readonly Rect2[] _plates = new Rect2[24];       // nameplates drawn this frame (to keep them from overlapping)
    private int _plateCount;

    private void DrawEnemyBars(Camera3D cam, Player p, Vector2 screen)
    {
        _plateCount = 0;
        Vector2 hp = p.Pos2;
        Actor hover = p.Hover;
        var mons = World.Monsters;
        for (int i = 0; i < mons.Count; i++)
        {
            var m = mons[i];
            if (m == null || m.Dead || ReferenceEquals(m, _onBossBar)) continue;
            bool hot = ReferenceEquals(m, hover);
            bool elite = m.IsElite;
            if (!hot && !(m.Life < m.MaxLife) && m.Barrier <= 0f && !(elite && m.Aggro)) continue;
            if (!IsInstanceValid(m)) continue;
            Vector3 wp = m.GlobalPosition;
            float dx = wp.X - hp.X, dz = wp.Z - hp.Y;
            if (dx * dx + dz * dz > 38f * 38f) continue;
            wp.Y += m.HeightM + 0.35f;
            if (cam.IsPositionBehind(wp)) continue;
            Vector2 sp = cam.UnprojectPosition(wp);
            if (sp.X < -70f || sp.X > screen.X + 70f || sp.Y < -10f || sp.Y > screen.Y + 40f) continue;
            DrawBar(m, sp, elite, hot, true);
        }
        var mins = World.Minions;
        for (int i = 0; i < mins.Count; i++)
        {
            var m = mins[i];
            if (m == null || m.Dead || !(m.Life < m.MaxLife) || !IsInstanceValid(m)) continue;
            Vector3 wp = m.GlobalPosition;
            wp.Y += m.HeightM + 0.3f;
            if (cam.IsPositionBehind(wp)) continue;
            Vector2 sp = cam.UnprojectPosition(wp);
            if (sp.X < -70f || sp.X > screen.X + 70f || sp.Y < -10f || sp.Y > screen.Y + 40f) continue;
            DrawBar(m, sp, false, false, false);
        }
    }

    private static UiArt.Piece _barArt, _labelHotArt, _labelLineArt;
    private static bool _artLooked;
    private static void LookArt()
    {
        if (_artLooked) return;
        _artLooked = true;
        if (!UiTheme.Art) return;
        _barArt = UiArt.P("plate_bar"); _labelHotArt = UiArt.P("plaque"); _labelLineArt = UiArt.P("boxline");
    }
    private static UiArt.Piece BarArt { get { LookArt(); return _barArt; } }
    private static UiArt.Piece LabelHotArt { get { LookArt(); return _labelHotArt; } }
    private static UiArt.Piece LabelLineArt { get { LookArt(); return _labelLineArt; } }

    private void DrawBar(Monster m, Vector2 sp, bool elite, bool hot, bool hostile)
    {
        float w = !hostile ? 40f : m.Tier == MonTier.Boss ? 120f : m.MiniBoss ? 108f : m.Tier == MonTier.Elite ? 84f : m.Tier == MonTier.Champion ? 66f : 48f;
        float h = elite ? 7f : hostile ? 5f : 4f;
        float frac = m.MaxLife > 0f ? Mathf.Clamp(m.Life / m.MaxLife, 0f, 1f) : 0f;
        var r = new Rect2(MathF.Round(sp.X - w * 0.5f), MathF.Round(sp.Y - h), w, h);
        DrawRect(r.Grow(1.5f), new Color(0f, 0f, 0f, 0.72f));
        DrawRect(r, hostile ? new Color(0.13f, 0.03f, 0.03f, 0.92f) : new Color(0.03f, 0.1f, 0.08f, 0.92f));
        if (frac > 0f)
        {
            float fw = Mathf.Max(1f, w * frac);
            DrawRect(new Rect2(r.Position, fw, h), hostile ? new Color(0.8f, 0.12f, 0.09f) : new Color(0.22f, 0.78f, 0.58f));
            DrawRect(new Rect2(r.Position, fw, 2f), hostile ? new Color(1f, 0.42f, 0.34f, 0.8f) : new Color(0.6f, 1f, 0.85f, 0.8f));
        }
        if (m.Barrier > 0f && m.MaxLife > 0f)
            DrawRect(new Rect2(r.Position.X, r.Position.Y - 3.5f, w * Mathf.Min(1f, m.Barrier / m.MaxLife), 2f), new Color(0.82f, 0.93f, 1f, 0.95f));
        if (!hostile) return;
        var barArt = BarArt;
        if (barArt != null && (elite || hot || m.Has(StatusId.Vulnerable)))
        {
            // a small forged frame for the bars that matter: gold for the marked, violet while vulnerable
            Color tint = m.Has(StatusId.Vulnerable) ? new Color(0.9f, 0.62f, 1f) : elite ? new Color(1f, 0.9f, 0.7f) : Colors.White;
            UiArt.Nine(this, barArt, new Rect2(r.Position.X - 4f, r.Position.Y - 2.5f, r.Size.X + 8f, r.Size.Y + 5f), tint);
        }
        else if (m.Has(StatusId.Vulnerable)) DrawRect(r.Grow(1.5f), new Color(0.88f, 0.55f, 1f, 0.95f), false, 1f);
        else if (elite) DrawRect(r.Grow(1.5f), new Color(UiTheme.Gold, 0.8f), false, 1f);
        if (!elite && !hot) return;

        // nameplate: affix line directly over the bar, the name above it; stepped by the real line height.
        // A pack of champions standing shoulder to shoulder would write their names over one another: a plate that
        // would land on one already drawn this frame is left out (its bar stays), unless the pointer is on that enemy.
        string plateName = m.DisplayName;
        if (!string.IsNullOrEmpty(plateName))
        {
            float pw = Width(plateName, UiTheme.Fs(elite ? PlateName : PlateNameMinor)) + 12f;
            float ph = MathF.Round(_body.GetHeight(UiTheme.Fs(PlateName)) * 1.7f);
            var plate = new Rect2(sp.X - pw * 0.5f, r.Position.Y - 8f - ph, pw, ph);
            if (!hot)
                for (int i = 0; i < _plateCount; i++)
                    if (_plates[i].Intersects(plate)) return;
            if (_plateCount < _plates.Length) _plates[_plateCount++] = plate;
        }
        float baseline = r.Position.Y - 8f;
        string title = m.Title;
        if (!string.IsNullOrEmpty(title))
        {
            int titlePx = UiTheme.Fs(PlateTitle);
            Say(_ui, sp.X, baseline, title, titlePx, new Color(0.95f, 0.72f, 0.45f), 5);
            baseline -= MathF.Round(_ui.GetHeight(titlePx) * 0.8f);
        }
        string name = m.DisplayName;
        if (!string.IsNullOrEmpty(name)) Say(_body, sp.X, baseline, name, UiTheme.Fs(elite ? PlateName : PlateNameMinor), HudBossBar.TierColor(m), 5);
    }

    /// <summary>One line of outlined text centred on x with its baseline at y. <paramref name="size"/> is the final pixel size.</summary>
    private void Say(Font f, float x, float baseline, string s, int size, Color col, int outline)
    {
        var pos = new Vector2(x - 400f, baseline);
        DrawStringOutline(f, pos, s, HorizontalAlignment.Center, 800f, size, outline, new Color(0f, 0f, 0f, 0.85f * col.A));
        DrawString(f, pos, s, HorizontalAlignment.Center, 800f, size, col);
    }

    private void DrawLabels()
    {
        for (int i = 0; i < _labCount; i++)
        {
            ref Lab l = ref _labs[i];
            Rect2 r = l.Rect;
            var hotArt = LabelHotArt;
            if (l.Hot && hotArt != null) UiArt.Nine(this, hotArt, r.Grow(2f), Colors.White);
            else
            {
                DrawRect(r, l.Hot ? new Color(0.17f, 0.13f, 0.07f, 0.94f) : new Color(0.02f, 0.02f, 0.025f, l.Loot ? 0.74f : 0.62f));
                var lineArt = LabelLineArt;
                if (l.Hot) DrawRect(r, new Color(UiTheme.Gold, 0.95f), false, 1f);
                else if (l.Fine && lineArt != null) UiArt.Nine(this, lineArt, r, new Color(l.Col, 0.8f));
                else if (l.Fine) DrawRect(r, new Color(l.Col, 0.55f), false, 1f);
            }
            Color col = l.Hot ? l.Col.Lightened(0.2f) : l.Col;
            // each line is centred in its band of the plate: baseline = band top + (band - line box) / 2 + ascent
            float y1 = r.Position.Y + (_labLine - _body.GetHeight(_labPx)) * 0.5f + _body.GetAscent(_labPx);
            DrawString(_body, new Vector2(r.Position.X - 20f, MathF.Round(y1)), l.Text, HorizontalAlignment.Center, r.Size.X + 40f, _labPx, col);
            if (l.Sub != null)
            {
                float y2 = r.Position.Y + _labLine + (_subLine - _ui.GetHeight(_subPx)) * 0.5f + _ui.GetAscent(_subPx);
                DrawString(_ui, new Vector2(r.Position.X - 20f, MathF.Round(y2)), l.Sub, HorizontalAlignment.Center, r.Size.X + 40f, _subPx, new Color(UiTheme.Dim, 0.95f));
            }
        }
    }

    private void DrawNumbers(Camera3D cam, Vector2 screen)
    {
        for (int i = 0; i < MaxNums; i++)
        {
            ref Num n = ref _nums[i];
            if (!n.On || n.Text == null) continue;
            if (cam.IsPositionBehind(n.At)) continue;
            Vector2 sp = cam.UnprojectPosition(n.At);
            float k = Mathf.Clamp(n.T / n.Life, 0f, 1f);
            float ease = 1f - (1f - k) * (1f - k);
            sp.X += n.Dx * ease;
            sp.Y -= n.Rise * ease;
            if (sp.X < -60f || sp.X > screen.X + 60f || sp.Y < -20f || sp.Y > screen.Y + 30f) continue;
            float alpha = k < 0.62f ? 1f : Mathf.Max(0f, 1f - (k - 0.62f) / 0.38f);
            float scale = n.Pop && n.T < 0.14f ? Mathf.Lerp(1.7f, 1f, n.T / 0.14f) : 1f;
            var col = new Color(n.Col, alpha);
            var edge = new Color(0f, 0f, 0f, 0.85f * alpha);
            int px = UiTheme.Fs(n.Size);
            if (scale != 1f)
            {
                DrawSetTransform(sp, 0f, new Vector2(scale, scale));
                var pos = new Vector2(-NumW * 0.5f, 0f);
                DrawStringOutline(_bold, pos, n.Text, HorizontalAlignment.Center, NumW, px, 6, edge);
                DrawString(_bold, pos, n.Text, HorizontalAlignment.Center, NumW, px, col);
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            }
            else
            {
                var pos = new Vector2(sp.X - NumW * 0.5f, sp.Y);
                DrawStringOutline(_bold, pos, n.Text, HorizontalAlignment.Center, NumW, px, 6, edge);
                DrawString(_bold, pos, n.Text, HorizontalAlignment.Center, NumW, px, col);
            }
        }
    }
}
