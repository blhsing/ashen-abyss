using Godot;
using System;

namespace AshenAbyss;

/// <summary>The 亡者名冊 tab of the skill screen (BookUi.cs): a button in the left column opens the book page over the tree.</summary>
public partial class SkillTreeUi
{
    private Button _bookBtn;
    private BookPage _book;
    public BookPage Book => _book;

    private void BuildBookTab()
    {
        _bookBtn = UiTheme.Button("亡者名冊", () => ShowBook(true), UiTheme.FsRow);
        _bookBtn.FocusMode = FocusModeEnum.None;
        _bookBtn.Visible = false;
        AddChild(_bookBtn);
        _book = new BookPage { Back = () => ShowBook(false) };
        AddChild(_book);
    }

    /// <summary>Shows the book page (or the tree again).</summary>
    public void ShowBook(bool on)
    {
        if (_book == null) return;
        if (on && Game.I?.Hero?.Book == null) on = false;
        if (!on) _book.ClosePopups();
        _book.Visible = on;
        if (on) { _book.Rebuild(); Log.Info("ui", "skill screen: 亡者名冊 tab"); }
    }

    /// <summary>Places the book button above reset / close; returns the height it takes from the column.</summary>
    private float LayoutBookTab(ClassId cls, float x, float w, float btnY, float btnH, float k)
    {
        if (_bookBtn == null) return 0f;
        bool has = ClassBook.Of(cls) != null;
        _bookBtn.Visible = has;
        if (!has) return 0f;
        _bookBtn.Position = new Vector2(x, btnY - btnH - 8f * k);
        _bookBtn.Size = new Vector2(w, btnH);
        return btnH + 8f * k;
    }
}
