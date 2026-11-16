using Godot;

namespace AshenAbyss;

/// <summary>
/// Base class of every panel the <see cref="Ui"/> manager can show (inventory, skills, map, vendors, menus…).
/// A panel builds its node tree in code, is registered once and is then added to and removed from the tree as it
/// opens and closes. The manager supplies the dimmed backdrop, pause handling, sounds, Esc and the hotkeys.
/// </summary>
public partial class UiPanel : Control
{
    /// <summary>The id the panel is opened by, e.g. "inventory".</summary>
    public virtual string Id => "";
    /// <summary>True (default) when the game should stand still while the panel is open.</summary>
    public virtual bool Pauses => true;
    /// <summary>True when the panel covers the whole screen; the HUD hides behind it and the backdrop is opaque.</summary>
    public virtual bool Fullscreen => false;
    /// <summary>Called every time the panel is shown; <paramref name="arg"/> is whatever the opener passed.</summary>
    public virtual void OnOpen(object arg) { }
    /// <summary>Called just before the panel leaves the screen.</summary>
    public virtual void OnClose() { }
    /// <summary>Rebuild the visible contents. The manager also calls this after the hero's stats or inventory change.</summary>
    public virtual void Refresh() { }
    /// <summary>
    /// The text-size setting changed (UiTheme.TextScale). The manager has already rescaled every font-size override,
    /// BBCode [font_size] tag and UiTheme.MinSize box in this panel's tree and asked custom-drawn controls to redraw;
    /// override this to redo any layout the panel computes by hand from text metrics (cached string widths, manual
    /// positions, cached BBCode). Called for every registered panel, open or not; <see cref="Refresh"/> follows for
    /// the open ones.
    /// </summary>
    public virtual void OnTextScale() { }
}
