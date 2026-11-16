using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// A style box drawn from the raster interface art (see <see cref="UiArt"/>): an optional seamless field tile, a
/// nine-slice frame (or a fixed sprite stretched over the box), up to two tinted overlay layers (rarity glow, hover
/// rim) and ornaments anchored to the middle of the top / bottom edge (the window crest). Everything is given in
/// design pixels; the frame's shadow and the ornaments may reach outside the control's rectangle.
/// Instances are shared: build them once (UiTheme caches them) and never per frame.
/// </summary>
public partial class UiNine : StyleBox
{
    public UiArt.Piece Frame, Field, Over1, Over2, Top, Bottom;
    /// <summary>How far the field stays inside the box (design px): hides its corners under the frame.</summary>
    public float FieldInset;
    public Color FrameMod = Colors.White, FieldMod = Colors.White, Over1Mod = Colors.White, Over2Mod = Colors.White;
    /// <summary>A flat colour laid under everything (alpha 0 = none): e.g. the dim plate of HUD plaques.</summary>
    public Color Back = new(0f, 0f, 0f, 0f);
    /// <summary>How far the flat colour stays inside the box (design px).</summary>
    public float BackInset;
    /// <summary>Lay the flat colour over the frame's floor instead of under it (a tint veil inside a slot).</summary>
    public bool BackOver;
    /// <summary>The top ornament is left out on boxes narrower than this (design px).</summary>
    public float TopMinWidth = 300f;
    /// <summary>Draw the frame as a thin strip centred on the box (separators): the piece keeps its own height.</summary>
    public bool Line;
    /// <summary>Extra room the frame takes outside the box on every side (design px, may be negative).</summary>
    public float Grow;
    /// <summary>
    /// A lighter frame (and top ornament) for boxes narrower or lower than <see cref="SmallBelow"/>: a confirm dialog
    /// should not carry the corner castings of a full window.
    /// </summary>
    public UiArt.Piece Small, SmallTop;
    public Vector2 SmallBelow;

    public UiNine() { }

    public UiNine(string frame, string field = null, float fieldInset = 0f, float padX = 0f, float padY = 0f)
    {
        Frame = frame != null ? UiArt.P(frame) : null;
        Field = field != null ? UiArt.Field(field) : null;
        FieldInset = fieldInset;
        ContentMarginLeft = padX; ContentMarginRight = padX;
        ContentMarginTop = padY; ContentMarginBottom = padY;
    }

    public UiNine Margins(float left, float top, float right, float bottom)
    {
        ContentMarginLeft = left; ContentMarginTop = top; ContentMarginRight = right; ContentMarginBottom = bottom;
        return this;
    }

    public override void _Draw(Rid ci, Rect2 rect)
    {
        try
        {
            if (Grow != 0f) rect = rect.Grow(Grow);
            if (Back.A > 0f && !BackOver) RenderingServer.CanvasItemAddRect(ci, BackInset != 0f ? rect.Grow(-BackInset) : rect, Back);
            if (Field != null)
            {
                float i = FieldInset;
                UiArt.Tile(ci, Field, new Rect2(rect.Position.X + i, rect.Position.Y + i, rect.Size.X - 2f * i, rect.Size.Y - 2f * i), FieldMod);
            }
            var frame = Frame;
            var top = Top;
            if (Small != null && (rect.Size.X < SmallBelow.X || rect.Size.Y < SmallBelow.Y)) { frame = Small; top = SmallTop; }
            if (frame != null)
            {
                if (Line)
                {
                    float h = frame.BoxSize.Y;
                    UiArt.Nine(ci, frame, new Rect2(rect.Position.X, rect.Position.Y + MathF.Round((rect.Size.Y - h) * 0.5f), rect.Size.X, h), FrameMod);
                }
                else if (frame.Kind == UiArt.Kind.Sprite) UiArt.Sprite(ci, frame, rect, FrameMod);
                else UiArt.Nine(ci, frame, rect, FrameMod);
            }
            if (Back.A > 0f && BackOver) RenderingServer.CanvasItemAddRect(ci, BackInset != 0f ? rect.Grow(-BackInset) : rect, Back);
            if (Over1 != null) Layer(ci, Over1, rect, Over1Mod);
            if (Over2 != null) Layer(ci, Over2, rect, Over2Mod);
            if (top != null && rect.Size.X >= (ReferenceEquals(top, Top) ? TopMinWidth : top.W * 0.5f + 80f))
                UiArt.SpriteAt(ci, top, new Vector2(rect.Position.X + MathF.Round(rect.Size.X * 0.5f), rect.Position.Y), Colors.White);
            if (Bottom != null && rect.Size.X >= TopMinWidth)
                UiArt.SpriteAt(ci, Bottom, new Vector2(rect.Position.X + MathF.Round(rect.Size.X * 0.5f), rect.End.Y), Colors.White);
        }
        catch (Exception e) { Log.Error("uiart", "drawing a frame failed", e); }
    }

    private static void Layer(Rid ci, UiArt.Piece p, Rect2 rect, Color mod)
    {
        if (p.Kind == UiArt.Kind.Sprite) UiArt.Sprite(ci, p, rect, mod);
        else UiArt.Nine(ci, p, rect, mod);
    }
}
