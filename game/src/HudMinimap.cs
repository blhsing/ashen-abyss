using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// The round minimap. The level grid and its fog of war (Level.Seen) are packed into a small texture — one texel per
/// 2 m cell — that a shader magnifies into soft ink-like floor shapes with a wall outline, inside the same
/// forged-iron ring as the globes. Markers, elite dots and the hero arrow are drawn on top. North is up
/// (world −Z), matching the camera.
/// </summary>
public partial class HudMinimap : Control
{
    public const float Diameter = 236f;
    private const float RangeM = 46f;             // world metres from the centre to the inner edge of the ring
    private const float RingR = 0.9f;             // must match R in the map shader
    private static readonly StringName PMap = "map_tex", PCenter = "center", PSpan = "span", PHasMap = "has_map";

    /// <summary>How one kind of marker looks. Edge = it stays on the rim of the map when it is out of range.</summary>
    private sealed class MarkStyle
    {
        public string Icon; public Color Col; public float Size; public bool Edge;
        public Texture2D Tex;
    }

    private static readonly Dictionary<string, MarkStyle> Styles = new()
    {
        ["waypoint"] = new() { Icon = "fire-bowl", Col = new Color(1f, 0.78f, 0.4f), Size = 20f, Edge = true },
        ["exit"] = new() { Icon = "exit-door", Col = new Color(0.85f, 0.9f, 1f), Size = 18f, Edge = true },
        ["entrance"] = new() { Icon = "dungeon-gate", Col = new Color(0.85f, 0.9f, 1f), Size = 19f },
        ["boss"] = new() { Icon = "crowned-skull", Col = new Color(1f, 0.35f, 0.25f), Size = 22f, Edge = true },
        ["bossdoor"] = new() { Icon = "wooden-door", Col = new Color(1f, 0.5f, 0.35f), Size = 18f, Edge = true },
        ["stronghold"] = new() { Icon = "stone-tower", Col = new Color(1f, 0.55f, 0.3f), Size = 20f, Edge = true },
        ["altar"] = new() { Icon = "fire-shrine", Col = new Color(1f, 0.7f, 0.35f), Size = 18f },
        ["chest"] = new() { Icon = "locked-chest", Col = new Color(1f, 0.86f, 0.45f), Size = 17f },
        ["shrine"] = new() { Icon = "rune-stone", Col = new Color(0.65f, 0.85f, 1f), Size = 17f },
        ["event"] = new() { Icon = "polar-star", Col = new Color(1f, 0.6f, 0.25f), Size = 19f, Edge = true },
        ["objective"] = new() { Icon = "position-marker", Col = new Color(1f, 0.86f, 0.4f), Size = 20f, Edge = true },
        ["quest"] = new() { Icon = "scroll-unfurled", Col = new Color(1f, 0.86f, 0.4f), Size = 20f, Edge = true },
        ["npc"] = new() { Icon = "conversation", Col = new Color(0.8f, 0.95f, 0.75f), Size = 17f },
        ["elder"] = new() { Icon = "lantern-flame", Col = new Color(1f, 0.86f, 0.5f), Size = 18f },
        ["smith"] = new() { Icon = "anvil", Col = new Color(0.9f, 0.85f, 0.75f), Size = 18f },
        ["merchant"] = new() { Icon = "two-coins", Col = new Color(1f, 0.86f, 0.45f), Size = 18f },
        ["occultist"] = new() { Icon = "crystal-ball", Col = new Color(0.8f, 0.6f, 1f), Size = 18f },
        ["jeweler"] = new() { Icon = "gem-chain", Col = new Color(0.6f, 0.9f, 1f), Size = 18f },
        ["villager"] = new() { Icon = "person", Col = new Color(0.75f, 0.8f, 0.7f), Size = 14f },
        ["stash"] = new() { Icon = "chest", Col = new Color(0.9f, 0.82f, 0.6f), Size = 18f },
        ["brazier"] = new() { Icon = "campfire", Col = new Color(1f, 0.6f, 0.25f), Size = 18f },
        ["tierstatue"] = new() { Icon = "laurel-crown", Col = new Color(0.9f, 0.85f, 0.75f), Size = 18f },
        ["riftstone"] = new() { Icon = "magic-portal", Col = new Color(0.75f, 0.55f, 1f), Size = 19f },
        ["portal"] = new() { Icon = "magic-portal", Col = new Color(0.55f, 0.8f, 1f), Size = 19f, Edge = true },
        ["prisoner"] = new() { Icon = "prisoner", Col = new Color(0.8f, 0.95f, 0.75f), Size = 17f },
        ["idol"] = new() { Icon = "totem-head", Col = new Color(1f, 0.5f, 0.4f), Size = 17f },
        ["elite"] = new() { Icon = "horned-skull", Col = new Color(1f, 0.6f, 0.25f), Size = 17f },
    };

    private readonly ColorRect _map;
    private readonly Control _over;
    private readonly ShaderMaterial _mat;
    private readonly Vector2[] _arrow = new Vector2[3];
    private readonly Vector2[] _rhomb = new Vector2[4];

    private Level _level;
    private NavGrid _grid;
    private Image _img;
    private ImageTexture _tex;
    private byte[] _px;
    private int _seenCount = -1;
    private float _refresh, _unseen, _questT;
    private bool _hasMap;
    private Vector2 _hero;
    private float _facing;
    private string _questArea, _questAreaName;

    public HudMinimap()
    {
        Name = "HudMinimap";
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(Diameter, Diameter);
        Size = CustomMinimumSize;

        _mat = new ShaderMaterial { Shader = HudShaders.Map };
        _map = new ColorRect { Material = _mat, Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(Diameter, Diameter) };
        AddChild(_map);
        _over = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(Diameter, Diameter), TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        _over.Draw += DrawOverlay;
        AddChild(_over);
        if (UiTheme.Art && UiArt.Has("minimap_ring"))
        {
            _mat.SetShaderParameter("art", 1f);
            _ring = UiArt.P("minimap_ring");
            var ring = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(Diameter, Diameter) };
            ring.Draw += () => UiArt.Sprite(ring, _ring, new Rect2(0f, 0f, Diameter, Diameter));
            AddChild(ring);
        }
    }

    private readonly UiArt.Piece _ring;

    /// <summary>Per-frame update. A null level or player simply shows the empty ring.</summary>
    public void Tick(Level lv, Player p, float dt)
    {
        NavGrid grid = lv?.Grid;
        if (lv == null || grid == null || p == null || grid.W <= 0 || grid.H <= 0)
        {
            if (_hasMap) { _hasMap = false; _mat.SetShaderParameter(PHasMap, 0f); _over.QueueRedraw(); }
            _level = null; _grid = null;
            return;
        }
        if (!ReferenceEquals(grid, _grid) || !ReferenceEquals(lv, _level))
        {
            _level = lv; _grid = grid;
            _px = new byte[grid.W * grid.H * 4];
            _img = Image.CreateFromData(grid.W, grid.H, false, Image.Format.Rgba8, _px);
            _tex = ImageTexture.CreateFromImage(_img);
            _mat.SetShaderParameter(PMap, _tex);
            _seenCount = -1; _refresh = 0f; _unseen = 0f; _questT = 0f;
        }
        if (!_hasMap) { _hasMap = true; _mat.SetShaderParameter(PHasMap, 1f); }

        _hero = p.Pos2;
        _facing = p.Facing;
        _refresh -= dt;
        if (_refresh <= 0f)
        {
            _refresh = 0.25f;
            Rebuild(lv, grid);
        }
        _questT -= dt;
        if (_questT <= 0f)
        {
            _questT = 1.5f;
            try
            {
                string next = Quests.NextObjectiveArea();
                if (next != _questArea) { _questArea = next; _questAreaName = string.IsNullOrEmpty(next) ? null : ZoneDb.AreaName(next); }
            }
            catch (Exception e) { UiSafe.Report("minimap quest target", e); _questArea = null; _questAreaName = null; }
        }

        Vector2 world = grid.Size;
        var center = new Vector2((_hero.X - grid.Origin.X) / world.X, (_hero.Y - grid.Origin.Y) / world.Y);
        var span = new Vector2(RangeM / world.X, RangeM / world.Y);
        _mat.SetShaderParameter(PCenter, center);
        _mat.SetShaderParameter(PSpan, span);
        _over.QueueRedraw();
    }

    /// <summary>Packs floor / road / pit / seen into the map texture; uploads only when the explored area changed.</summary>
    private void Rebuild(Level lv, NavGrid g)
    {
        int n = g.W * g.H;
        bool[] seen = lv.Seen;
        bool fog = seen != null && seen.Length == n;
        byte[] deco = lv.Data?.Deco;
        bool hasDeco = deco != null && deco.Length == n;
        byte[] cells = g.T;
        byte[] px = _px;
        int count = 0;
        for (int i = 0; i < n; i++)
        {
            int o = i * 4;
            if (fog && !seen[i]) { px[o] = 0; px[o + 1] = 0; px[o + 2] = 0; px[o + 3] = 0; continue; }
            count++;
            byte t = cells[i];
            byte road = 0;
            if (hasDeco && t == NavGrid.Floor) { byte d = deco[i]; road = d == 1 ? (byte)255 : d == 3 || d == 4 ? (byte)120 : (byte)0; }
            px[o] = t == NavGrid.Floor ? (byte)255 : (byte)0;
            px[o + 1] = road;
            px[o + 2] = t == NavGrid.Pit ? (byte)255 : (byte)0;
            px[o + 3] = 255;
        }
        if (count != _seenCount)
        {
            _seenCount = count;
            _img.SetData(g.W, g.H, false, Image.Format.Rgba8, px);
            _tex.Update(_img);
        }

        // normally the level lifts its own fog; if the ground under the hero stays unknown, lift it from here
        if (fog)
        {
            Vector2I c = g.CellOf(_hero);
            if (g.In(c.X, c.Y) && !seen[c.Y * g.W + c.X])
            {
                _unseen += 0.25f;
                if (_unseen >= 0.75f) lv.Reveal(_hero, 18f);
            }
            else _unseen = 0f;
        }
    }

    private static MarkStyle StyleOf(MapMarker m)
    {
        string kind = m.Kind ?? "";
        if (!Styles.TryGetValue(kind, out var s))
        {
            // "npc:smith", "npc_smith" and the like: try the part after the separator
            int cut = kind.IndexOfAny(new[] { ':', '_', '.' });
            if (cut <= 0 || !Styles.TryGetValue(kind.Substring(cut + 1), out s))
                if (cut <= 0 || !Styles.TryGetValue(kind.Substring(0, cut), out s)) s = null;
            Styles[kind] = s;       // remember the answer (also when there is none)
        }
        if (s != null && s.Tex == null) s.Tex = Icons.Get(s.Icon);
        return s;
    }

    private void DrawOverlay()
    {
        var c = new Vector2(Diameter * 0.5f, Diameter * 0.5f);
        if (!_hasMap || _level == null || _grid == null || !IsInstanceValid(_level)) return;
        float usable = Diameter * 0.5f * RingR;
        float k = usable / RangeM;                          // pixels per metre
        float limit = usable - 9f;
        var shade = new Color(0f, 0f, 0f, 0.6f);

        var marks = _level.Markers;
        if (marks != null)
        {
            for (int i = 0; i < marks.Count; i++)
            {
                var m = marks[i];
                if (m == null || !m.Visible) continue;
                MarkStyle st = StyleOf(m);
                // the way toward the current story step is drawn in gold and stays on the rim when far away
                bool goal = _questArea != null && (m.Kind == "exit" || m.Kind == "entrance")
                    && ((m.Ref is string rs && rs == _questArea) || m.Label == _questAreaName || m.Label == _questArea);
                Vector2 q = (m.Pos - _hero) * k;
                float len = q.Length();
                bool clamped = false;
                if (len > limit)
                {
                    if (!(goal || (st != null && st.Edge))) continue;
                    q = q / len * limit;
                    clamped = true;
                }
                Vector2 at = c + q;
                if (st == null)
                {
                    Rhomb(at, 4.5f, shade);
                    Rhomb(at, 3f, new Color(0.85f, 0.82f, 0.74f));
                    continue;
                }
                float size = clamped ? st.Size * 0.78f : st.Size;
                Color col = goal ? new Color(1f, 0.86f, 0.4f) : st.Col;
                if (clamped) col = new Color(col, 0.8f);
                _over.DrawCircle(at, size * 0.62f, goal ? new Color(0.25f, 0.17f, 0.03f, 0.85f) : shade);
                _over.DrawTextureRect(st.Tex, new Rect2(at.X - size * 0.5f, at.Y - size * 0.5f, size, size), false, col);
            }
        }

        // elites and bosses that have noticed the hero
        var mons = World.Monsters;
        for (int i = 0; i < mons.Count; i++)
        {
            var m = mons[i];
            if (m == null || m.Dead || !m.IsElite || !IsInstanceValid(m)) continue;
            if (!m.Aggro && !m.IsBoss) continue;
            Vector2 q = (m.Pos2 - _hero) * k;
            if (q.LengthSquared() > limit * limit) continue;
            Rhomb(c + q, 5.5f, shade);
            Rhomb(c + q, 3.8f, m.Tier == MonTier.Boss ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.62f, 0.2f));
        }

        // the hero: an arrow that points where they face (facing 0 looks toward +Z, which is down on the map)
        var dir = new Vector2(Mathf.Sin(_facing), Mathf.Cos(_facing));
        var side = new Vector2(-dir.Y, dir.X);
        _arrow[0] = c + dir * 10.5f; _arrow[1] = c - dir * 6.5f + side * 6.5f; _arrow[2] = c - dir * 6.5f - side * 6.5f;
        _over.DrawColoredPolygon(_arrow, new Color(0f, 0f, 0f, 0.75f));
        _arrow[0] = c + dir * 8f; _arrow[1] = c - dir * 4.5f + side * 4.6f; _arrow[2] = c - dir * 4.5f - side * 4.6f;
        _over.DrawColoredPolygon(_arrow, new Color(1f, 0.9f, 0.62f));
    }

    private void Rhomb(Vector2 p, float r, Color col)
    {
        _rhomb[0] = new Vector2(p.X, p.Y - r); _rhomb[1] = new Vector2(p.X + r, p.Y);
        _rhomb[2] = new Vector2(p.X, p.Y + r); _rhomb[3] = new Vector2(p.X - r, p.Y);
        _over.DrawColoredPolygon(_rhomb, col);
    }
}
