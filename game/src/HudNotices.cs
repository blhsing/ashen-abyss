using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// The HUD's transient messages, all drawn by one control: toasts (stacked under the boss bar), the red warning
/// line, the area title card, the level-up banner and the subtitle band for barks and voiced lines.
/// Timers only run while the game is being played, so a title queued during loading is not lost.
/// </summary>
public partial class HudNotices : Control
{
    private const int MaxToasts = 6;
    private const float ToastLife = 3.4f, WarnLife = 1.9f, TitleLife = 4.6f, BannerLife = 4.2f;
    /// <summary>
    /// Design sizes (px at 1080p, before the text scale): area title card and its subtitle, level-up banner and its
    /// line, toasts, the red warning, the subtitle band (spoken line and speaker). All go through UiTheme.Fs in Paint.
    /// </summary>
    private const int AreaTitleSize = 60, AreaSubSize = 26, BannerSize = 48, BannerSubSize = 24, ToastSize = 24, WarnSize = 28,
        SubtitleSize = 28, SpeakerSize = 24;

    /// <summary>Where the first toast starts (top of its line), set by the HUD so the stack begins under the boss frame.</summary>
    public float ToastTop = 176f;
    /// <summary>Distance from the bottom of the screen to the bottom of the subtitle text (clear of the action bar and its buffs).</summary>
    public float SubtitleBottom = 318f;
    private int _linePx;

    private struct Toast
    {
        public string Text, Shown;
        public float W;         // measured width of Shown at Px (0 = measure again)
        public int Px;
        public Color Col;
        public float T;
        public int Count;
    }

    private readonly List<Toast> _toasts = new(MaxToasts + 1);
    private string _warn; private float _warnT = 99f;
    private string _title, _titleSub; private float _titleT = 99f;
    private string _banner, _bannerSub; private float _bannerT = 99f; private Color _bannerCol = Colors.White;
    private string _speaker, _line; private float _lineT, _lineLife; private Vector2 _lineSize; private float _lineWidth;
    private bool _idle = true;

    public HudNotices()
    {
        Name = "HudNotices";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public void AddToast(string text, Color c)
    {
        if (string.IsNullOrEmpty(text)) return;
        // the same message twice in a row becomes "… ×2" instead of a second line
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            var t = _toasts[i];
            if (t.Text != text || t.T > 1.2f) continue;
            t.Count++; t.T = Mathf.Min(t.T, 0.2f); t.Shown = text + "　×" + t.Count; t.Col = c; t.W = 0f;
            _toasts[i] = t;
            _idle = false;
            return;
        }
        if (_toasts.Count >= MaxToasts) _toasts.RemoveAt(0);
        _toasts.Add(new Toast { Text = text, Shown = text, Col = c, T = 0f, Count = 1 });
        _idle = false;
    }

    /// <summary>Removes toasts whose text starts with <paramref name="prefix"/> (a banner is about to say the same thing).</summary>
    public void DropToasts(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return;
        for (int i = _toasts.Count - 1; i >= 0; i--)
            if (_toasts[i].Text != null && _toasts[i].Text.StartsWith(prefix, StringComparison.Ordinal)) _toasts.RemoveAt(i);
    }

    public void Warn(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        _warn = text; _warnT = 0f; _idle = false;
    }

    public void AreaTitle(string name, string subtitle)
    {
        if (string.IsNullOrEmpty(name)) return;
        _title = name; _titleSub = subtitle ?? ""; _titleT = 0f; _idle = false;
    }

    public void Banner(string title, string sub, Color c)
    {
        if (string.IsNullOrEmpty(title)) return;
        _banner = title; _bannerSub = sub ?? ""; _bannerCol = c; _bannerT = 0f; _idle = false;
    }

    public void Subtitle(string speaker, string text, float seconds)
    {
        if (string.IsNullOrEmpty(text)) { _line = null; return; }
        _speaker = string.IsNullOrEmpty(speaker) ? null : speaker;
        _line = text;
        _lineT = 0f;
        _lineLife = Mathf.Max(1.2f, seconds);
        _lineWidth = 0f;                  // measured on the next draw
        _idle = false;
    }

    /// <summary>Clears everything at once (returning to the title screen).</summary>
    public void Clear()
    {
        _toasts.Clear();
        _warnT = _titleT = _bannerT = 99f;
        _line = null;
        if (!_idle) { _idle = true; QueueRedraw(); }
    }

    public void Tick(float dt, bool playing)
    {
        if (_idle) return;
        if (playing)
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                t.T += dt;
                if (t.T >= ToastLife) _toasts.RemoveAt(i); else _toasts[i] = t;
            }
            if (_warnT < WarnLife) _warnT += dt;
            if (_titleT < TitleLife) _titleT += dt;
            if (_bannerT < BannerLife) _bannerT += dt;
            if (_line != null) { _lineT += dt; if (_lineT >= _lineLife) _line = null; }
        }
        bool busy = _toasts.Count > 0 || _warnT < WarnLife || _titleT < TitleLife || _bannerT < BannerLife || _line != null;
        if (!busy) _idle = true;
        QueueRedraw();
    }

    private static float Fade(float t, float life, float fadeIn, float fadeOut)
    {
        if (t < fadeIn) return t / fadeIn;
        if (t > life - fadeOut) return Mathf.Max(0f, (life - t) / fadeOut);
        return 1f;
    }

    /// <summary>The text scale changed: measurements are taken again on the next draw.</summary>
    public void Remeasure()
    {
        _lineWidth = 0f;
        _linePx = 0;
        QueueRedraw();
    }

    /// <summary>One centred, outlined line. <paramref name="size"/> is the final pixel size (already through UiTheme.Fs).</summary>
    private void Line(Font f, float baseline, string s, int size, Color col, int outline, float dx = 0f)
    {
        var pos = new Vector2(dx, baseline);
        DrawStringOutline(f, pos, s, HorizontalAlignment.Center, Size.X, size, outline, new Color(0f, 0f, 0f, 0.85f * col.A));
        DrawString(f, pos, s, HorizontalAlignment.Center, Size.X, size, col);
    }

    /// <summary>A hairline that fades out toward both ends, centred on x.</summary>
    private void Flourish(float cx, float y, float half, Color col)
    {
        const int steps = 8;
        for (int i = 0; i < steps; i++)
        {
            float a = 1f - i / (float)steps;
            float seg = half / steps;
            var c = new Color(col, col.A * a * a);
            DrawRect(new Rect2(cx + i * seg, y, seg + 1f, 1.5f), c);
            DrawRect(new Rect2(cx - (i + 1) * seg, y, seg + 1f, 1.5f), c);
        }
    }

    /// <summary>
    /// A soft band of soot behind a big line so it reads over fire, snow and spell effects alike: darkest in the
    /// middle, gone at both ends. Drawn from a handful of rectangles; nothing is allocated.
    /// </summary>
    private static GradientTexture2D _veilTex;

    private void Veil(float cx, float top, float height, float half, float alpha)
    {
        // one soft elliptical glow of soot, stretched over the lines: a radial gradient texture built once
        if (_veilTex == null)
        {
            var g = new Gradient();
            g.SetOffset(0, 0f); g.SetColor(0, new Color(0.012f, 0.01f, 0.012f, 1f));
            g.SetOffset(1, 1f); g.SetColor(1, new Color(0.012f, 0.01f, 0.012f, 0f));
            g.AddPoint(0.5f, new Color(0.012f, 0.01f, 0.012f, 0.78f));
            g.AddPoint(0.8f, new Color(0.012f, 0.01f, 0.012f, 0.22f));
            _veilTex = new GradientTexture2D
            {
                Gradient = g, Width = 160, Height = 80, Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            };
        }
        float pad = height * 0.55f;
        DrawTextureRect(_veilTex, new Rect2(cx - half, top - pad, half * 2f, height + pad * 2f), false, new Color(1f, 1f, 1f, alpha));
    }

    private static UiArt.Piece _ruleArt, _veilArt;
    private static bool _artLooked;
    private static void LookArt()
    {
        if (_artLooked) return;
        _artLooked = true;
        if (!UiTheme.Art) return;
        _ruleArt = UiArt.P("title_rule"); _veilArt = UiArt.P("tracker");
    }
    private static UiArt.Piece RuleArt { get { LookArt(); return _ruleArt; } }
    private static UiArt.Piece VeilArt { get { LookArt(); return _veilArt; } }

    public override void _Draw()
    {
        if (_idle) return;
        try { Paint(); }
        catch (Exception e) { UiSafe.Report("notices", e); }
    }

    private void Paint()
    {
        Vector2 s = Size;
        float cx = s.X * 0.5f;
        Font title = UiTheme.Title, body = UiTheme.Body;
        float titleBottom = 0f;           // toasts step below the title card while it is on screen

        // ── area title card ──
        if (_titleT < TitleLife && _title != null)
        {
            int big = UiTheme.Fs(AreaTitleSize), sub = UiTheme.Fs(AreaSubSize);
            float a = Fade(_titleT, TitleLife, 0.7f, 1.1f);
            float y = s.Y * 0.25f;
            float drift = (1f - Mathf.Min(1f, _titleT / 0.9f)) * 10f;
            float rule = y + MathF.Round(title.GetDescent(big) * 0.7f) + 4f;
            float vTop = y - title.GetAscent(big) - 4f;
            titleBottom = rule + 8f + (_titleSub.Length > 0 ? body.GetHeight(sub) : 0f) + 10f;
            Veil(cx, vTop, titleBottom - vTop, 620f, 0.7f * a);
            Line(title, y - drift, _title, big, new Color(UiTheme.Gold, a), 8);
            if (RuleArt != null) UiArt.SpriteAt(this, RuleArt, new Vector2(cx, rule + 3f), new Color(1f, 1f, 1f, a * Mathf.Min(1f, _titleT / 0.8f)), 1f);
            else Flourish(cx, rule, 260f * Mathf.Min(1f, _titleT / 0.8f), new Color(UiTheme.Gold, 0.85f * a));
            if (_titleSub.Length > 0) Line(body, rule + 8f + body.GetAscent(sub), _titleSub, sub, new Color(UiTheme.Text, 0.92f * a), 5);
        }

        // ── level-up banner ──
        if (_bannerT < BannerLife && _banner != null)
        {
            int big = UiTheme.Fs(BannerSize), sub = UiTheme.Fs(BannerSubSize);
            float a = Fade(_bannerT, BannerLife, 0.35f, 0.9f);
            float y = s.Y * 0.38f;
            float glow = Mathf.Max(0f, 1f - _bannerT / 0.6f);
            float bTop = y - title.GetAscent(big) - 6f;
            float under = y + MathF.Round(title.GetDescent(big) * 0.7f) + 2f;
            float bBottom = under + 8f + (_bannerSub.Length > 0 ? MathF.Round(body.GetHeight(sub) * 0.95f) : 0f);
            // a ribbon of soot between the two hairlines: the words stay readable inside the level-up light
            Veil(cx, bTop, bBottom - bTop, 560f, 0.95f * a);
            Flourish(cx, bTop, 340f, new Color(_bannerCol, 0.85f * a));
            Line(title, y, _banner, big, new Color(_bannerCol.Lerp(Colors.White, glow * 0.6f), a), 8);
            if (_bannerSub.Length > 0) Line(body, under + body.GetAscent(sub), _bannerSub, sub, new Color(UiTheme.Text, 0.97f * a), 5);
            Flourish(cx, bBottom, 340f, new Color(_bannerCol, 0.85f * a));
        }

        // ── toasts ──
        if (_toasts.Count > 0)
        {
            int px = UiTheme.Fs(ToastSize);
            float lineH = body.GetHeight(px);
            var veil = VeilArt;
            float step = veil != null ? MathF.Round(lineH + 6f) : MathF.Round(lineH * 0.92f);
            float y = Mathf.Max(ToastTop, titleBottom) + body.GetAscent(px);
            for (int i = 0; i < _toasts.Count; i++)
            {
                var t = _toasts[i];
                float a = Fade(t.T, ToastLife, 0.14f, 0.6f);
                float slide = (1f - Mathf.Min(1f, t.T / 0.16f)) * 8f;
                if (veil != null)
                {
                    // a translucent plaque behind the line: readable over fire and snow alike (width measured once per text)
                    if (t.W <= 0f || t.Px != px)
                    {
                        t.W = body.GetStringSize(t.Shown, HorizontalAlignment.Left, -1f, px).X; t.Px = px;
                        _toasts[i] = t;
                    }
                    UiArt.Nine(this, veil, new Rect2(MathF.Round(cx - t.W * 0.5f - 26f), MathF.Round(y + slide - body.GetAscent(px)), MathF.Round(t.W + 52f), MathF.Round(lineH + 2f)), new Color(1f, 1f, 1f, a));
                }
                Line(body, y + slide, t.Shown, px, new Color(t.Col, a), 5);
                y += step;
            }
        }

        // ── warning ──
        if (_warnT < WarnLife && _warn != null)
        {
            float a = Fade(_warnT, WarnLife, 0.06f, 0.5f);
            float shake = _warnT < 0.25f ? Mathf.Sin(_warnT * 70f) * 5f * (1f - _warnT / 0.25f) : 0f;
            Line(title, s.Y * 0.665f, _warn, UiTheme.Fs(WarnSize), new Color(1f, 0.46f, 0.38f, a), 6, shake);
        }

        // ── subtitles ──
        if (_line != null)
        {
            var cfg = Game.I?.Cfg;
            if (cfg == null || cfg.Subtitles)
            {
                int px = UiTheme.Fs(SubtitleSize), who = UiTheme.Fs(SpeakerSize);
                float maxW = Mathf.Min(UiTheme.Px(1040f), s.X - 120f);
                if (_lineWidth != maxW || _linePx != px)
                {
                    _lineWidth = maxW;
                    _linePx = px;
                    _lineSize = body.GetMultilineStringSize(_line, HorizontalAlignment.Center, maxW, px);
                }
                float a = Fade(_lineT, _lineLife, 0.2f, 0.45f);
                float h = _lineSize.Y;
                // the band stands on a fixed line above the action bar and grows upward with the text
                float top = s.Y - SubtitleBottom - h;
                float w = Mathf.Max(Mathf.Min(maxW, _lineSize.X) + 44f, 260f);
                float head = _speaker != null ? MathF.Round(title.GetHeight(who) * 0.9f) : 0f;      // room for the speaker's name above the line
                var band = new Rect2(cx - w * 0.5f, top - 10f - head, w, h + 20f + head);
                if (VeilArt != null) UiArt.Nine(this, VeilArt, band, new Color(1f, 1f, 1f, a));
                else
                {
                    DrawRect(band, new Color(0.02f, 0.02f, 0.025f, 0.66f * a));
                    DrawRect(new Rect2(band.Position, w, 1f), new Color(UiTheme.Gold, 0.35f * a));
                    DrawRect(new Rect2(band.Position.X, band.End.Y - 1f, w, 1f), new Color(UiTheme.Gold, 0.35f * a));
                }
                if (_speaker != null) Line(title, band.Position.Y + 6f + title.GetAscent(who), _speaker, who, new Color(UiTheme.Gold, a), 4);
                float ascent = body.GetAscent(px);
                var pos = new Vector2(cx - maxW * 0.5f, top + ascent);
                DrawMultilineString(body, pos, _line, HorizontalAlignment.Center, maxW, px, -1, new Color(UiTheme.Text, a));
            }
        }
    }
}
