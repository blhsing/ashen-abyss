using Godot;
using System;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// The minion frame by the life globe (tools/golems/PLAN.md §4): the golem's portrait, life and re-form timer, a forge mark
/// with the inherited affixes in its tooltip (iron golem), the Pactbinder's fiend, and the warrior and mage counts.
/// Hidden when the hero has no persistent minions. Added to the HUD by MinionSystem.
/// </summary>
public partial class HudMinions : Control
{
    private const float FrameW = 300f, FrameH = 112f, Portrait = 76f;
    private TextureRect _portrait, _forge;
    private Label _name, _state, _counts;
    private Control _bar;
    private float _life = 1f, _slow;
    private bool _dead;
    private string _sig;

    public HudMinions()
    {
        Name = "HudMinions";
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = 0.5f; AnchorRight = 0.5f; AnchorTop = 1f; AnchorBottom = 1f;
        OffsetLeft = -HudBar.W * 0.5f - FrameW - 6f; OffsetRight = -HudBar.W * 0.5f - 6f;
        OffsetTop = -FrameH - 18f; OffsetBottom = -18f;
        Visible = false;
    }

    public override void _Ready()
    {
        var back = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        try { back.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.05f, 0.045f, 0.042f, 0.82f), new Color(UiTheme.Border, 0.7f), 2, 6)); } catch (Exception e) { Log.Error("hud", "minion frame box", e); }
        back.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(back);
        _portrait = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(10f, (FrameH - Portrait) * 0.5f), Size = new Vector2(Portrait, Portrait) };
        AddChild(_portrait);
        _forge = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Stop,
            Position = new Vector2(10f + Portrait - 30f, (FrameH - Portrait) * 0.5f + Portrait - 30f), Size = new Vector2(34f, 34f), Visible = false };
        AddChild(_forge);
        _name = UiTheme.Outlined(UiTheme.Label("", UiTheme.FsRow, UiTheme.Gold), 4);
        _name.Position = new Vector2(96f, 6f); _name.Size = new Vector2(FrameW - 104f, 28f); _name.ClipText = true;
        AddChild(_name);
        _bar = new Control { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(96f, 40f), Size = new Vector2(FrameW - 108f, 16f) };
        _bar.Draw += DrawBar;
        AddChild(_bar);
        _state = UiTheme.Outlined(UiTheme.Small("", UiTheme.FsCaption, UiTheme.Text), 4);
        _state.Position = new Vector2(96f, 58f); _state.Size = new Vector2(FrameW - 104f, 24f);
        AddChild(_state);
        _counts = UiTheme.Outlined(UiTheme.Small("", UiTheme.FsCaption, UiTheme.Text), 4);
        _counts.Position = new Vector2(96f, 82f); _counts.Size = new Vector2(FrameW - 104f, 24f);
        AddChild(_counts);
    }

    private void DrawBar()
    {
        var r = new Rect2(Vector2.Zero, _bar.Size);
        _bar.DrawRect(r, new Color(0.02f, 0.02f, 0.02f, 0.9f));
        var fill = new Rect2(r.Position + Vector2.One, new Vector2((r.Size.X - 2f) * Mathf.Clamp(_life, 0f, 1f), r.Size.Y - 2f));
        _bar.DrawRect(fill, _dead ? new Color(0.35f, 0.33f, 0.3f) : new Color(0.62f, 0.12f, 0.1f));
        _bar.DrawRect(r, new Color(UiTheme.Border, 0.9f), false, 1f);
    }

    public override void _Process(double delta)
    {
        var g = Game.I; var h = g?.Hero; var p = World.P;
        var sys = MinionSystem.I;
        if (g == null || h == null || p == null || !GodotObject.IsInstanceValid(p) || sys == null || g.State != Game.GameState.Play || (g.Hud?.Lean ?? false))
        {
            if (Visible) Visible = false;
            return;
        }
        _slow -= (float)delta;
        if (_slow > 0f) return;
        _slow = 0.15f;
        try { Update(h, p, sys); }
        catch (Exception e) { UiSafe.Report("hud minions", e); }
    }

    private void Update(Hero h, Player p, MinionSystem sys)
    {
        string kind = MinionSystem.WantedGolem(h);
        var fiend = World.Minions.FirstOrDefault(m => !m.Dead && m.Def.Id.StartsWith("pact") && m.LifeTime <= 0f);
        int w = MinionSystem.Count("sb_raise"), mg = MinionSystem.Count(MinionSystem.MageTag);
        bool book = MinionSystem.BookRaises(h);
        bool show = kind != null || fiend != null || (book && (w > 0 || mg > 0 || h.BaseRank("sb_raise") > 0));
        if (Visible != show) Visible = show;
        if (!show) return;

        string sig;
        if (kind != null)
        {
            var art = MinionArt.OfKind(kind);
            var k = ClassBook.Kind(kind);
            _dead = !sys.GolemAlive;
            _life = sys.GolemAlive ? sys.Golem.Life / Mathf.Max(1f, sys.Golem.MaxLife) : 1f - Mathf.Clamp(sys.ReformLeft / Mathf.Max(1f, sys.ReformTotal), 0f, 1f);
            _name.Text = art?.Name ?? k?.Name ?? "魔像";
            _state.Text = sys.GolemAlive ? $"生命 {Mathf.Round(sys.Golem.Life)} / {Mathf.Round(sys.Golem.MaxLife)}" : $"重新塑成：{Mathf.Ceil(sys.ReformLeft)} 秒";
            sig = kind + (h.D.GolemForge?.Uid ?? "");
            if (sig != _sig)
            {
                _portrait.Texture = Icons.Raster(k?.Icon ?? "skill/sb_colossus") ?? Icons.Raster("skill/sb_colossus");
                var it = kind == "g_iron" ? h.D.GolemForge : null;
                _forge.Visible = it != null;
                if (it != null)
                {
                    _forge.Texture = Icons.ForItem(it);
                    var st = GolemForge.Inherit(it, h);
                    _forge.TooltipText = $"鑄自：{it.Name}\n" + string.Join("\n", st.Lines);
                }
            }
        }
        else if (fiend != null)
        {
            _dead = false;
            _life = fiend.Life / Mathf.Max(1f, fiend.MaxLife);
            _name.Text = fiend.Def.Name;
            _state.Text = $"生命 {Mathf.Round(fiend.Life)} / {Mathf.Round(fiend.MaxLife)}";
            sig = "fiend" + fiend.Def.Id;
            if (sig != _sig) { _portrait.Texture = Icons.Raster("skill/pb_bind"); _forge.Visible = false; }
        }
        else
        {
            _dead = false; _life = 0f;
            _name.Text = "亡者之軍";
            _state.Text = h.LineSacrificed(ClassBook.Golem) ? "魔像已獻祭" : "";
            sig = "army";
            if (sig != _sig) { _portrait.Texture = Icons.Raster("skill/sb_raise"); _forge.Visible = false; }
        }
        _sig = sig;
        _counts.Text = book
            ? $"{(h.LineSacrificed(ClassBook.Warriors) ? "戰士 獻祭" : $"戰士 {w}/{MinionSystem.WarriorCap(h)}")}　{(h.LineSacrificed(ClassBook.Mages) ? "術士 獻祭" : $"術士 {mg}/{MinionSystem.MageCap(h)}")}"
            : (w > 0 ? $"骸骨僕從 {w}" : "");
        _bar.QueueRedraw();
    }
}
