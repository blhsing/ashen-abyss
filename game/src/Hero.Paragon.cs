using System;
using System.Collections.Generic;

namespace AshenAbyss;

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════
//  燼紋盤 — per-hero state and rules: which boards are attached, which nodes are lit, which glyph sits where.
//  Board and glyph definitions are in Paragon.cs; the panel is ParagonUi.cs.
//
//  Saved as:  SaveData.ParagonBoards  attached boards (lattice position + owned node indices), [0] = start board
//             SaveData.Sockets        board id → glyph id sitting in that board's socket
//             SaveData.Glyphs         glyph id → level (present = awake)
//             SaveData.GlyphTries     banked tempering attempts, one entry per attempt = the rift tier that earned it
//  SaveData.Talents belonged to the retired single board and is emptied on load.
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>One attached board as it is saved.</summary>
public sealed class ParagonBoardSave
{
    public string Id = "";
    public int X, Y;                            // position on the lattice of boards; the start board is at (0, 0)
    public List<int> Nodes = new();             // owned node indices (y * 15 + x); the 燼種 itself is implicit
}

public sealed partial class SaveData
{
    public List<ParagonBoardSave> ParagonBoards = new();
    public List<int> GlyphTries = new();
}

/// <summary>Runtime view of one attached board: definition, save entry, owned flags and its socket's evaluation.</summary>
public sealed class ParagonBoardState
{
    public PBoard Def;
    public ParagonBoardSave Save;
    public int Ordinal;                         // 0 for the start board; rare thresholds rise with it
    public readonly bool[] Owned = new bool[PBoard.CellCount];
    public int OwnedCount;                      // lit nodes, not counting the 燼種
    public GlyphDef Glyph;                      // the glyph working in this board's socket (null if none)
    public int GlyphLevel, GlyphRadius;
    public float GlyphPoints;                   // attribute points of the glyph's attribute lit inside its radius
    public bool GlyphBonus;
    public int X => Save.X;
    public int Y => Save.Y;
}

public struct GlyphEval
{
    public GlyphDef Glyph;
    public int Level, Radius;
    public float Points, Mul;
    public bool Bonus;
}

public sealed partial class Hero
{
    private sealed class RareSlot { public PNode Node; public int Board, Threshold; public bool Active; }

    private readonly List<ParagonBoardState> _pb = new();
    private readonly StatBlock _pStats = new();                 // everything the boards grant that does not depend on a threshold
    private readonly List<string> _pHooks = new();
    private readonly List<RareSlot> _pRares = new();            // owned rare nodes; their second half is decided on every Recalc
    private readonly float[] _pAttr = new float[4];             // attribute totals the thresholds were last checked against
    private readonly bool[] _pSeen = new bool[ParagonDb.MaxBoards * PBoard.CellCount];
    private readonly int[] _pQueue = new int[ParagonDb.MaxBoards * PBoard.CellCount];
    private bool _pBooted, _pBroken, _pStateDirty = true, _pStatsDirty = true;
    private int _pSpent, _pSyncedParagon = -1;

    // ───────────────────────── stats ─────────────────────────

    /// <summary>Called by Recalc after gear, passives and spec, before buffs: board and glyph stats into S, hooks into Keystones.</summary>
    partial void ApplyParagonStats()
    {
        if (_pBroken) return;
        try
        {
            if (!_pBooted)
            {
                _pBooted = true;
                ParagonUp += OnParagonLevelUp;
                ParagonHooks.Ensure();
            }
            if (_pSyncedParagon != D.Paragon) { _pSyncedParagon = D.Paragon; SyncGlyphs(null); }
            EnsureParagon();
            if (_pBroken) return;

            S.AddAll(_pStats);
            for (int i = 0; i < _pHooks.Count; i++) Keystones.Add(_pHooks[i]);

            // rare nodes: the second half wakes only while the attribute total (gear, passives and boards included) meets the threshold
            _pAttr[0] = S[Stat.Str]; _pAttr[1] = S[Stat.Int]; _pAttr[2] = S[Stat.Dex]; _pAttr[3] = S[Stat.Wil];
            for (int i = 0; i < _pRares.Count; i++)
            {
                var r = _pRares[i];
                r.Active = ParagonAttr(r.Node.Req) >= r.Threshold;
                if (!r.Active) continue;
                var bonus = r.Node.Bonus;
                for (int k = 0; k < bonus.Length; k++) S.Add(bonus[k].s, bonus[k].v);
            }
        }
        catch (Exception e)
        {
            BreakParagon("applying stats", e);
        }
    }

    private void BreakParagon(string what, Exception e)
    {
        _pBroken = true;
        _pb.Clear(); _pStats.Clear(); _pHooks.Clear(); _pRares.Clear(); _pSpent = 0;
        Log.Error("paragon", $"燼紋盤 failed while {what}; it is disabled for this session (the save is left untouched)", e);
    }

    private void EnsureParagon()
    {
        if (_pBroken) return;
        try
        {
            if (_pStateDirty) RebuildParagonState();
            if (_pStatsDirty) RebuildParagonStats();
        }
        catch (Exception e)
        {
            BreakParagon("rebuilding state", e);
        }
    }

    /// <summary>Rebuilds the runtime boards from the save, repairing anything a damaged or hand-edited save may contain.</summary>
    private void RebuildParagonState()
    {
        _pStateDirty = false; _pStatsDirty = true;
        D.ParagonBoards ??= new(); D.GlyphTries ??= new(); D.Glyphs ??= new(); D.Sockets ??= new();
        if (D.Talents != null && D.Talents.Count > 0)
        {
            Log.Info("paragon", $"discarding {D.Talents.Count} nodes of the retired single board");
            D.Talents.Clear();
        }

        var list = D.ParagonBoards;
        var start = ParagonDb.StartBoard(Class);
        list.RemoveAll(b => b == null || string.IsNullOrEmpty(b.Id));
        int si = list.FindIndex(b => b.Id == start.Id);
        if (si < 0)
        {
            if (list.Count > 0) Log.Warn("paragon", "saved boards do not include this class's start board: starting over with an empty 燼紋盤");
            list.Clear();
            list.Add(new ParagonBoardSave { Id = start.Id });
        }
        else if (si > 0)
        {
            var s = list[si];
            list.RemoveAt(si); list.Insert(0, s);
        }
        list[0].X = 0; list[0].Y = 0;

        _pb.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            var sv = list[i];
            var def = ParagonDb.Board(Class, sv.Id);
            string bad = def == null ? "unknown board" : _pb.Count >= ParagonDb.MaxBoards ? "too many boards" : i > 0 && def.IsStart ? "second start board" : null;
            for (int k = 0; bad == null && k < _pb.Count; k++)
            {
                if (_pb[k].Def == def) bad = "duplicate board";
                else if (_pb[k].X == sv.X && _pb[k].Y == sv.Y) bad = "two boards in one place";
            }
            if (bad != null)
            {
                Log.Warn("paragon", $"dropping saved board '{sv.Id}' at {sv.X},{sv.Y}: {bad}");
                list.RemoveAt(i--);
                continue;
            }
            sv.Nodes ??= new();
            var st = new ParagonBoardState { Def = def, Save = sv, Ordinal = _pb.Count };
            for (int k = sv.Nodes.Count - 1; k >= 0; k--)
            {
                int idx = sv.Nodes[k];
                var n = def.Node(idx);
                if (n == null || n.Type == PNodeType.Start || st.Owned[idx]) { sv.Nodes.RemoveAt(k); continue; }
                st.Owned[idx] = true;
            }
            if (def.StartNode != null) st.Owned[def.StartNode.Index] = true;
            _pb.Add(st);
        }

        // boards must hang together across the lattice, starting from the start board
        var linked = new bool[ParagonDb.MaxBoards];
        linked[0] = true;
        for (bool grew = true; grew;)
        {
            grew = false;
            for (int i = 1; i < _pb.Count; i++)
            {
                if (linked[i]) continue;
                for (int k = 0; k < _pb.Count; k++)
                    if (linked[k] && Math.Abs(_pb[k].X - _pb[i].X) + Math.Abs(_pb[k].Y - _pb[i].Y) == 1) { linked[i] = true; grew = true; break; }
            }
        }
        for (int i = _pb.Count - 1; i >= 1; i--)
        {
            if (linked[i]) continue;
            Log.Warn("paragon", $"dropping saved board '{_pb[i].Def.Id}': it touches no other board");
            list.Remove(_pb[i].Save);
            _pb.RemoveAt(i);
        }
        for (int i = 0; i < _pb.Count; i++) _pb[i].Ordinal = i;

        // every lit node must be connected to the 燼種
        ParagonFlood(-1, -1);
        int dropped = 0, spent = 0;
        for (int b = 0; b < _pb.Count; b++)
        {
            var st = _pb[b];
            for (int k = st.Save.Nodes.Count - 1; k >= 0; k--)
            {
                int idx = st.Save.Nodes[k];
                if (_pSeen[b * PBoard.CellCount + idx]) continue;
                st.Owned[idx] = false; st.Save.Nodes.RemoveAt(k); dropped++;
            }
            spent += st.Save.Nodes.Count;
        }
        if (dropped > 0) Log.Warn("paragon", $"{dropped} saved nodes were not connected to the start and were refunded");

        int earned = D.Paragon + D.BonusParagonPts;
        if (spent > earned)
        {
            Log.Warn("paragon", $"{spent} nodes lit with only {earned} points earned: refunding everything");
            foreach (var st in _pb)
            {
                st.Save.Nodes.Clear();
                Array.Clear(st.Owned, 0, st.Owned.Length);
                if (st.Def.StartNode != null) st.Owned[st.Def.StartNode.Index] = true;
            }
            D.Sockets.Clear();
        }

        // a socket holds a glyph only if the socket is lit, the glyph is awake and it sits nowhere else
        if (D.Sockets.Count > 0)
        {
            List<string> drop = null;
            HashSet<string> used = null;
            foreach (var kv in D.Sockets)
            {
                int bi = PbIndexOf(kv.Key);
                var sock = bi >= 0 ? _pb[bi].Def.Socket : null;
                bool ok = sock != null && _pb[bi].Owned[sock.Index] && kv.Value != null && GlyphDb.Get(kv.Value) != null
                          && D.Glyphs.ContainsKey(kv.Value) && (used ??= new()).Add(kv.Value);
                if (!ok) (drop ??= new()).Add(kv.Key);
            }
            if (drop != null)
                foreach (var k in drop)
                {
                    Log.Warn("paragon", $"clearing the socket entry of '{k}': it no longer holds a usable glyph");
                    D.Sockets.Remove(k);
                }
        }

        D.GlyphTries.RemoveAll(t => t <= 0);
        TrimGlyphTries();
        Log.Info("paragon", $"state ready for {Names.Class(Class)}: boards={_pb.Count} lit={Math.Min(spent, earned)} earned={earned} glyphs={D.Glyphs.Count} tries={D.GlyphTries.Count}");
    }

    private void RebuildParagonStats()
    {
        _pStatsDirty = false;
        _pStats.Clear(); _pHooks.Clear(); _pRares.Clear(); _pSpent = 0;
        for (int b = 0; b < _pb.Count; b++)
        {
            var st = _pb[b];
            st.OwnedCount = 0;
            var nodes = st.Def.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (!st.Owned[n.Index]) continue;
                if (n.Type != PNodeType.Start) { st.OwnedCount++; _pSpent++; }
                var ss = n.Stats;
                for (int k = 0; k < ss.Length; k++) _pStats.Add(ss[k].s, ss[k].v);
                if (n.Hook != null && !_pHooks.Contains(n.Hook)) _pHooks.Add(n.Hook);
                if (n.Type == PNodeType.Rare && n.Bonus.Length > 0)
                    _pRares.Add(new RareSlot { Node = n, Board = b, Threshold = ParagonDb.RareThreshold(Class, n.Req, st.Ordinal) });
            }

            st.Glyph = null; st.GlyphLevel = 0; st.GlyphRadius = 0; st.GlyphPoints = 0; st.GlyphBonus = false;
            var sock = st.Def.Socket;
            if (sock == null || !st.Owned[sock.Index] || !D.Sockets.TryGetValue(st.Def.Id, out string gid) || gid == null) continue;
            var g = GlyphDb.Get(gid);
            if (g == null || !D.Glyphs.TryGetValue(gid, out int lvl)) continue;
            lvl = Math.Clamp(lvl, 1, GlyphDb.MaxLevel);
            st.Glyph = g; st.GlyphLevel = lvl; st.GlyphRadius = GlyphDb.Radius(lvl);
            st.GlyphPoints = GlyphPointsIn(st, g.Attr, st.GlyphRadius);
            st.GlyphBonus = st.GlyphPoints >= g.Threshold;
            float mul = GlyphDb.LevelMul(lvl) * st.GlyphPoints / 5f;
            for (int k = 0; k < g.Scale.Length; k++) _pStats.Add(g.Scale[k].s, g.Scale[k].per5 * mul);
            if (st.GlyphBonus) for (int k = 0; k < g.Bonus.Length; k++) _pStats.Add(g.Bonus[k].s, g.Bonus[k].v);
        }
    }

    /// <summary>Points of one attribute granted by the lit nodes inside a radius around the board's socket.</summary>
    private static float GlyphPointsIn(ParagonBoardState st, Stat attr, int radius)
    {
        var sock = st.Def.Socket;
        if (sock == null) return 0f;
        float pts = 0f;
        var nodes = st.Def.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == sock || !st.Owned[n.Index] || !GlyphDb.InRadius(n.X - sock.X, n.Y - sock.Y, radius)) continue;
            var ss = n.Stats;
            for (int k = 0; k < ss.Length; k++) if (ss[k].s == attr) pts += ss[k].v;
        }
        return pts;
    }

    // ───────────────────────── lattice helpers ─────────────────────────

    private int PbIndexAt(int x, int y)
    {
        for (int i = 0; i < _pb.Count; i++) if (_pb[i].X == x && _pb[i].Y == y) return i;
        return -1;
    }

    private int PbIndexOf(string boardId)
    {
        for (int i = 0; i < _pb.Count; i++) if (_pb[i].Def.Id == boardId) return i;
        return -1;
    }

    private bool PbValid(int board, int node) => board >= 0 && board < _pb.Count && _pb[board].Def.Node(node) != null;

    /// <summary>
    /// The node next to (board, idx) in direction d. Inside a board that is the neighbouring cell; a gate also
    /// reaches across its edge to the facing gate of the board attached there.
    /// </summary>
    private bool ParagonNeighbor(int board, int idx, int d, out int nb, out int ni)
    {
        nb = board; ni = -1;
        var st = _pb[board];
        var (dx, dy) = ParagonDb.Dirs[d];
        int x = idx % PBoard.Size + dx, y = idx / PBoard.Size + dy;
        if (x >= 0 && y >= 0 && x < PBoard.Size && y < PBoard.Size)
        {
            ni = y * PBoard.Size + x;
            return st.Def.Cells[ni] != null;
        }
        var n = st.Def.Cells[idx];
        if (n == null || n.Type != PNodeType.Gate || n.GateDir != d) return false;
        nb = PbIndexAt(st.X + dx, st.Y + dy);
        if (nb < 0) return false;
        var g = _pb[nb].Def.Gates[ParagonDb.Opposite(d)];
        if (g == null) return false;
        ni = g.Index;
        return true;
    }

    /// <summary>
    /// Floods from the 燼種 across lit nodes, optionally pretending one of them is dark. Marks _pSeen
    /// (board * cells + index) and returns how many nodes besides the 燼種 were reached.
    /// </summary>
    private int ParagonFlood(int skipBoard, int skipNode)
    {
        Array.Clear(_pSeen, 0, _pSeen.Length);
        if (_pb.Count == 0 || _pb[0].Def.StartNode == null) return 0;
        const int cells = PBoard.CellCount;
        int head = 0, tail = 0, reached = 0;
        int s = _pb[0].Def.StartNode.Index;
        _pSeen[s] = true; _pQueue[tail++] = s;
        while (head < tail)
        {
            int key = _pQueue[head++];
            int b = key / cells, idx = key % cells;
            for (int d = 0; d < 4; d++)
            {
                if (!ParagonNeighbor(b, idx, d, out int nb, out int ni)) continue;
                if (!_pb[nb].Owned[ni] || (nb == skipBoard && ni == skipNode)) continue;
                int nk = nb * cells + ni;
                if (_pSeen[nk]) continue;
                _pSeen[nk] = true; _pQueue[tail++] = nk; reached++;
            }
        }
        return reached;
    }

    private bool TouchesLit(int board, int node)
    {
        for (int d = 0; d < 4; d++)
            if (ParagonNeighbor(board, node, d, out int nb, out int ni) && _pb[nb].Owned[ni]) return true;
        return false;
    }

    // ───────────────────────── queries ─────────────────────────

    /// <summary>The 燼紋盤 wakes at the level cap.</summary>
    public bool ParagonUnlocked => D.Level >= Balance.LevelCap;
    public int ParagonUnlockLevel => Balance.LevelCap;
    /// <summary>True when the board data could not be used this session (see the log); nothing is granted or spendable then.</summary>
    public bool ParagonBroken => _pBroken;
    public int ParagonEarned => D.Paragon + D.BonusParagonPts;
    public int ParagonSpent { get { EnsureParagon(); return _pSpent; } }
    /// <summary>Unspent 燼紋點 (0 while the system is still locked).</summary>
    public int ParagonPoints => ParagonUnlocked && !_pBroken ? Math.Max(0, ParagonEarned - ParagonSpent) : 0;

    /// <summary>Attached boards; [0] is the start board. Each carries its lattice position and owned flags.</summary>
    public IReadOnlyList<ParagonBoardState> ParagonBoards { get { EnsureParagon(); return _pb; } }
    public int ParagonBoardIndexAt(int x, int y) { EnsureParagon(); return PbIndexAt(x, y); }

    public bool ParagonOwned(int board, int node) { EnsureParagon(); return PbValid(board, node) && _pb[board].Owned[node]; }

    /// <summary>Dark, but next to a lit node: one point away.</summary>
    public bool ParagonReachable(int board, int node)
    {
        EnsureParagon();
        return PbValid(board, node) && !_pb[board].Owned[node] && TouchesLit(board, node);
    }

    /// <summary>The attribute total the rare thresholds were last checked against (Str / Int / Dex / Wil).</summary>
    public float ParagonAttr(Stat s) => s switch { Stat.Str => _pAttr[0], Stat.Int => _pAttr[1], Stat.Dex => _pAttr[2], Stat.Wil => _pAttr[3], _ => 0f };

    public int ParagonRareThreshold(int board, PNode n)
    {
        EnsureParagon();
        int ordinal = board >= 0 && board < _pb.Count ? _pb[board].Ordinal : _pb.Count;
        return ParagonDb.RareThreshold(Class, n.Req, ordinal);
    }

    /// <summary>Whether the second half of a lit rare node is currently awake.</summary>
    public bool ParagonRareActive(int board, int node)
    {
        EnsureParagon();
        for (int i = 0; i < _pRares.Count; i++) if (_pRares[i].Board == board && _pRares[i].Node.Index == node) return _pRares[i].Active;
        return false;
    }

    // ───────────────────────── allocation ─────────────────────────

    /// <summary>Null when the node can be lit right now, otherwise the reason (zh-TW, for the UI).</summary>
    public string WhyCantTakeParagon(int board, int node)
    {
        if (!ParagonUnlocked) return $"持燼者達到等級 {Balance.LevelCap} 後，燼紋盤才會甦醒";
        EnsureParagon();
        if (!PbValid(board, node)) return "這裡沒有燼紋";
        if (_pb[board].Owned[node]) return "已經點亮";
        if (!TouchesLit(board, node)) return "尚未與已點亮的燼紋相連";
        if (ParagonEarned - _pSpent <= 0) return "燼紋點不足";
        return null;
    }

    public bool TakeParagon(int board, int node)
    {
        string why = WhyCantTakeParagon(board, node);
        if (why != null) { Log.Debug("paragon", $"take refused b{board} n{node}: {why}"); return false; }
        var st = _pb[board];
        var n = st.Def.Node(node);
        st.Owned[node] = true; st.Save.Nodes.Add(node);
        _pStatsDirty = true;
        string msg = $"lit {st.Def.Id} ({n.X},{n.Y}) {n.Type} '{n.Name}' points left {ParagonEarned - _pSpent - 1}";
        if (n.Type is PNodeType.Legendary or PNodeType.Gate or PNodeType.Socket or PNodeType.Rare) Log.Info("paragon", msg); else Log.Debug("paragon", msg);
        Recalc();
        return true;
    }

    /// <summary>Null when the node can be refunded, otherwise the reason. Refunds are free but must keep everything connected.</summary>
    public string WhyCantRefundParagon(int board, int node)
    {
        EnsureParagon();
        if (!PbValid(board, node) || !_pb[board].Owned[node]) return "尚未點亮";
        if (_pb[board].Def.Node(node).Type == PNodeType.Start) return "燼種無法熄滅";
        if (ParagonFlood(board, node) != _pSpent - 1) return "熄滅這裡會使其他燼紋失去連結";
        return null;
    }

    public bool RefundParagon(int board, int node)
    {
        string why = WhyCantRefundParagon(board, node);
        if (why != null) { Log.Debug("paragon", $"refund refused b{board} n{node}: {why}"); return false; }
        var st = _pb[board];
        var n = st.Def.Node(node);
        st.Owned[node] = false; st.Save.Nodes.Remove(node);
        if (n.Type == PNodeType.Socket && D.Sockets.Remove(st.Def.Id)) Log.Info("paragon", $"glyph removed from {st.Def.Id}: its socket went dark");
        _pStatsDirty = true;
        string msg = $"refunded {st.Def.Id} ({n.X},{n.Y}) {n.Type} '{n.Name}'";
        if (n.Type is PNodeType.Legendary or PNodeType.Gate or PNodeType.Socket or PNodeType.Rare) Log.Info("paragon", msg); else Log.Debug("paragon", msg);
        Recalc();
        return true;
    }

    /// <summary>
    /// For code that edits the saved 燼紋 data directly (test harness, cheats, migrations) instead of going through
    /// the methods here: drops the cached state so the next Recalc reads the save again. Calls Recalc itself.
    /// </summary>
    public void InvalidateParagon()
    {
        if (_pBroken) return;
        _pStateDirty = true;
        _pSyncedParagon = -1;
        Recalc();
    }

    /// <summary>Refunds every node and takes every glyph out; attached boards stay where they are.</summary>
    public void RefundAllParagon()
    {
        EnsureParagon();
        if (_pBroken) return;
        foreach (var st in _pb) st.Save.Nodes.Clear();
        D.Sockets.Clear();
        _pStateDirty = true;
        Log.Info("paragon", "all nodes refunded");
        Recalc();
    }

    /// <summary>Back to a bare start board: every node refunded, every attached board removed. Glyph levels are kept.</summary>
    public void ResetParagon()
    {
        EnsureParagon();
        if (_pBroken) return;
        for (int i = D.ParagonBoards.Count - 1; i >= 1; i--) D.ParagonBoards.RemoveAt(i);
        if (D.ParagonBoards.Count > 0) D.ParagonBoards[0].Nodes?.Clear();
        D.Sockets.Clear();
        _pStateDirty = true;
        Log.Info("paragon", "燼紋盤 reset to the bare start board");
        Recalc();
    }

    /// <summary>
    /// Spends unspent points by always lighting the most valuable reachable node (legendary, rare, socket, magic,
    /// gate, normal). Meant for the test harness and quick trials; returns the number of points spent.
    /// </summary>
    public int AutoAllocateParagon(int maxPoints = int.MaxValue)
    {
        if (!ParagonUnlocked) return 0;
        EnsureParagon();
        if (_pBroken) return 0;
        int budget = Math.Min(maxPoints, ParagonEarned - _pSpent), done = 0;
        while (done < budget)
        {
            int bb = -1, bn = -1, best = -1;
            for (int b = 0; b < _pb.Count; b++)
            {
                var st = _pb[b];
                var nodes = st.Def.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    if (st.Owned[n.Index] || !TouchesLit(b, n.Index)) continue;
                    int score = n.Type switch { PNodeType.Legendary => 6, PNodeType.Rare => 5, PNodeType.Socket => 4, PNodeType.Magic => 3, PNodeType.Gate => 2, _ => 1 };
                    if (score > best) { best = score; bb = b; bn = n.Index; }
                }
            }
            if (bb < 0) break;
            _pb[bb].Owned[bn] = true; _pb[bb].Save.Nodes.Add(bn);
            done++;
        }
        if (done > 0)
        {
            _pStatsDirty = true;
            Log.Info("paragon", $"auto-allocated {done} points");
            Recalc();
        }
        return done;
    }

    // ───────────────────────── boards ─────────────────────────

    /// <summary>A lit gate with nothing beyond it, while another board may still be attached.</summary>
    public bool ParagonGateOpen(int board, int dir)
    {
        if (!ParagonUnlocked) return false;
        EnsureParagon();
        if (board < 0 || board >= _pb.Count || dir < 0 || dir > 3 || _pb.Count >= ParagonDb.MaxBoards) return false;
        var st = _pb[board];
        var gate = st.Def.Gates[dir];
        if (gate == null || !st.Owned[gate.Index]) return false;
        return PbIndexAt(st.X + ParagonDb.Dirs[dir].dx, st.Y + ParagonDb.Dirs[dir].dy) < 0;
    }

    /// <summary>The boards of this class that are not on the lattice yet.</summary>
    public List<PBoard> ParagonBoardChoices()
    {
        EnsureParagon();
        var res = new List<PBoard>();
        if (_pBroken) return res;
        foreach (var def in ParagonDb.Boards(Class))
            if (!def.IsStart && PbIndexOf(def.Id) < 0) res.Add(def);
        return res;
    }

    public string WhyCantAttachParagonBoard(int board, int dir, string boardId)
    {
        if (!ParagonUnlocked) return $"持燼者達到等級 {Balance.LevelCap} 後，燼紋盤才會甦醒";
        EnsureParagon();
        if (board < 0 || board >= _pb.Count || dir < 0 || dir > 3) return "這裡沒有燼門";
        var st = _pb[board];
        var gate = st.Def.Gates[dir];
        if (gate == null || !st.Owned[gate.Index]) return "須先點亮這一側的燼門";
        if (_pb.Count >= ParagonDb.MaxBoards) return $"盤面合計最多 {ParagonDb.MaxBoards} 面燼紋盤";
        if (PbIndexAt(st.X + ParagonDb.Dirs[dir].dx, st.Y + ParagonDb.Dirs[dir].dy) >= 0) return "這一側已經接著燼紋盤";
        var def = ParagonDb.Board(Class, boardId);
        if (def == null || def.IsStart) return "這面燼紋盤無法接續";
        if (PbIndexOf(def.Id) >= 0) return "這面燼紋盤已在盤面上";
        return null;
    }

    /// <summary>Attaches a board beyond the lit gate on side <paramref name="dir"/> of an attached board.</summary>
    public bool AttachParagonBoard(int board, int dir, string boardId)
    {
        string why = WhyCantAttachParagonBoard(board, dir, boardId);
        if (why != null) { Log.Warn("paragon", $"attach '{boardId}' refused at b{board} side {dir}: {why}"); return false; }
        var st = _pb[board];
        var sv = new ParagonBoardSave { Id = boardId, X = st.X + ParagonDb.Dirs[dir].dx, Y = st.Y + ParagonDb.Dirs[dir].dy };
        D.ParagonBoards.Add(sv);
        _pStateDirty = true;
        Log.Info("paragon", $"board '{boardId}' attached at {sv.X},{sv.Y} beyond the {ParagonDb.DirNames[dir]} gate of '{st.Def.Id}' (now {D.ParagonBoards.Count} boards)");
        Recalc();
        return true;
    }

    /// <summary>A board can be taken off again when nothing on it is lit and the remaining boards still hang together.</summary>
    public string WhyCantDetachParagonBoard(int board)
    {
        EnsureParagon();
        if (board < 0 || board >= _pb.Count) return "這裡沒有燼紋盤";
        if (board == 0) return "起始盤無法卸除";
        if (_pb[board].OwnedCount > 0) return "須先熄滅這面盤上所有的燼紋";
        var linked = new bool[ParagonDb.MaxBoards];
        linked[0] = true;
        for (bool grew = true; grew;)
        {
            grew = false;
            for (int i = 1; i < _pb.Count; i++)
            {
                if (linked[i] || i == board) continue;
                for (int k = 0; k < _pb.Count; k++)
                    if (linked[k] && k != board && Math.Abs(_pb[k].X - _pb[i].X) + Math.Abs(_pb[k].Y - _pb[i].Y) == 1) { linked[i] = true; grew = true; break; }
            }
        }
        for (int i = 1; i < _pb.Count; i++) if (i != board && !linked[i]) return "另一面燼紋盤只靠它與起始盤相連";
        return null;
    }

    public bool DetachParagonBoard(int board)
    {
        string why = WhyCantDetachParagonBoard(board);
        if (why != null) { Log.Debug("paragon", $"detach refused b{board}: {why}"); return false; }
        var st = _pb[board];
        D.ParagonBoards.Remove(st.Save);
        D.Sockets.Remove(st.Def.Id);
        _pStateDirty = true;
        Log.Info("paragon", $"board '{st.Def.Id}' detached (now {D.ParagonBoards.Count} boards)");
        Recalc();
        return true;
    }

    // ───────────────────────── glyphs ─────────────────────────

    /// <summary>Every glyph, in the order this class wakes them.</summary>
    public IReadOnlyList<GlyphDef> ParagonGlyphs => GlyphDb.Order(Class);
    public bool GlyphUnlocked(string glyphId) => glyphId != null && D.Glyphs != null && D.Glyphs.ContainsKey(glyphId);
    /// <summary>0 while the glyph still sleeps.</summary>
    public int GlyphLevel(string glyphId) => glyphId != null && D.Glyphs != null && D.Glyphs.TryGetValue(glyphId, out int l) ? Math.Clamp(l, 1, GlyphDb.MaxLevel) : 0;
    /// <summary>燼紋等級 at which the glyph wakes for this class.</summary>
    public int GlyphUnlockLevel(string glyphId) => GlyphDb.UnlockLevel(Class, glyphId);

    /// <summary>Id of the glyph in an attached board's socket, or null.</summary>
    public string SocketedGlyph(int board)
    {
        EnsureParagon();
        if (board < 0 || board >= _pb.Count || D.Sockets == null) return null;
        return D.Sockets.TryGetValue(_pb[board].Def.Id, out string g) ? g : null;
    }

    /// <summary>Index of the attached board whose socket holds the glyph, −1 when it is not socketed.</summary>
    public int GlyphBoard(string glyphId)
    {
        EnsureParagon();
        if (glyphId == null || D.Sockets == null) return -1;
        foreach (var kv in D.Sockets) if (kv.Value == glyphId) return PbIndexOf(kv.Key);
        return -1;
    }

    /// <summary>What a glyph does (or would do) in a board's socket with the nodes lit right now.</summary>
    public GlyphEval EvalGlyph(int board, string glyphId)
    {
        var ev = default(GlyphEval);
        EnsureParagon();
        var g = GlyphDb.Get(glyphId);
        if (g == null || board < 0 || board >= _pb.Count || _pb[board].Def.Socket == null) return ev;
        ev.Glyph = g;
        ev.Level = Math.Max(1, GlyphLevel(glyphId));
        ev.Radius = GlyphDb.Radius(ev.Level);
        ev.Mul = GlyphDb.LevelMul(ev.Level);
        ev.Points = GlyphPointsIn(_pb[board], g.Attr, ev.Radius);
        ev.Bonus = ev.Points >= g.Threshold;
        return ev;
    }

    public string WhyCantSocketGlyph(int board, string glyphId)
    {
        if (!ParagonUnlocked) return $"持燼者達到等級 {Balance.LevelCap} 後，燼紋盤才會甦醒";
        EnsureParagon();
        if (board < 0 || board >= _pb.Count || _pb[board].Def.Socket == null) return "這面燼紋盤沒有燼符槽";
        if (!_pb[board].Owned[_pb[board].Def.Socket.Index]) return "須先點亮燼符槽";
        if (GlyphDb.Get(glyphId) == null || !GlyphUnlocked(glyphId)) return "這枚燼符尚未甦醒";
        if (SocketedGlyph(board) == glyphId) return "這枚燼符已嵌在此處";
        return null;
    }

    /// <summary>Puts a glyph into a board's socket. A glyph sits in one socket at a time, so it moves if it was elsewhere.</summary>
    public bool SocketGlyph(int board, string glyphId)
    {
        string why = WhyCantSocketGlyph(board, glyphId);
        if (why != null) { Log.Debug("paragon", $"socket '{glyphId}' refused b{board}: {why}"); return false; }
        string source = null;
        foreach (var kv in D.Sockets) if (kv.Value == glyphId) { source = kv.Key; break; }
        if (source != null) D.Sockets.Remove(source);
        string id = _pb[board].Def.Id;
        D.Sockets.TryGetValue(id, out string old);
        D.Sockets[id] = glyphId;
        _pStatsDirty = true;
        Log.Info("paragon", $"glyph {glyphId} lv{GlyphLevel(glyphId)} socketed in '{id}'{(old != null ? " replacing " + old : "")}{(source != null ? " (moved from '" + source + "')" : "")}");
        Recalc();
        return true;
    }

    public bool UnsocketGlyph(int board)
    {
        EnsureParagon();
        if (board < 0 || board >= _pb.Count) return false;
        string id = _pb[board].Def.Id;
        if (!D.Sockets.TryGetValue(id, out string old)) return false;
        D.Sockets.Remove(id);
        _pStatsDirty = true;
        Log.Info("paragon", $"glyph {old} taken out of '{id}'");
        Recalc();
        return true;
    }

    /// <summary>Wakes every glyph the current 燼紋等級 entitles the class to; newly woken ids are appended to <paramref name="fresh"/>.</summary>
    private void SyncGlyphs(List<string> fresh)
    {
        D.Glyphs ??= new();
        if (D.Level < Balance.LevelCap) return;
        var order = GlyphDb.Order(Class);
        for (int i = 0; i < order.Count; i++)
        {
            if (D.Paragon < GlyphDb.UnlockLevel(Class, order[i].Id)) break;
            if (D.Glyphs.ContainsKey(order[i].Id)) continue;
            D.Glyphs[order[i].Id] = 1;
            fresh?.Add(order[i].Id);
            Log.Info("paragon", $"glyph {order[i].Id} woke at 燼紋等級 {D.Paragon}");
        }
    }

    private void OnParagonLevelUp(int level)
    {
        try
        {
            var fresh = new List<string>();
            _pSyncedParagon = D.Paragon;
            SyncGlyphs(fresh);
            Log.Info("paragon", $"燼紋等級 {level}: unspent points {ParagonPoints}");
            // the level-up itself is announced by Game; this module only adds what it owns
            foreach (var id in fresh) ParagonHooks.Toast($"{GlyphDb.Get(id)?.Name} 甦醒了");
            Notify();
        }
        catch (Exception e)
        {
            Log.Error("paragon", "handling a 燼紋 level-up failed", e);
        }
    }

    // ───────────────────────── glyph tempering (rift clears) ─────────────────────────

    /// <summary>Banked tempering attempts.</summary>
    public int GlyphTries => D.GlyphTries?.Count ?? 0;

    /// <summary>The deepest rift tier among the banked attempts (0 when there are none).</summary>
    public int GlyphTryTier
    {
        get
        {
            int best = 0;
            var t = D.GlyphTries;
            if (t != null) for (int i = 0; i < t.Count; i++) if (t[i] > best) best = t[i];
            return best;
        }
    }

    /// <summary>
    /// Which banked attempt a glyph of this level would use: the one with the best chance, and among equals the
    /// shallowest, so deep-rift attempts are kept for the glyphs that need them. −1 when nothing is banked.
    /// </summary>
    private int PickGlyphTry(int level)
    {
        var t = D.GlyphTries;
        if (t == null || t.Count == 0) return -1;
        int best = 0;
        for (int i = 1; i < t.Count; i++) if (t[i] > t[best]) best = i;
        float top = GlyphDb.UpgradeChance(t[best], level);
        int pick = best;
        for (int i = 0; i < t.Count; i++)
            if (t[i] < t[pick] && GlyphDb.UpgradeChance(t[i], level) >= top - 0.0001f) pick = i;
        return pick;
    }

    /// <summary>Rift tier of the attempt the next tempering of this glyph would consume (0 if none).</summary>
    public int GlyphTryTierFor(string glyphId)
    {
        int pick = PickGlyphTry(Math.Max(1, GlyphLevel(glyphId)));
        return pick < 0 ? 0 : D.GlyphTries[pick];
    }

    /// <summary>Success chance (0-1) of the next tempering of this glyph; 0 when it cannot be tempered.</summary>
    public float GlyphUpgradeChance(string glyphId)
    {
        if (WhyCantUpgradeGlyph(glyphId) != null) return 0f;
        int level = GlyphLevel(glyphId);
        int pick = PickGlyphTry(level);
        return pick < 0 ? 0f : GlyphDb.UpgradeChance(D.GlyphTries[pick], level);
    }

    public string WhyCantUpgradeGlyph(string glyphId)
    {
        if (GlyphDb.Get(glyphId) == null || !GlyphUnlocked(glyphId)) return "這枚燼符尚未甦醒";
        if (GlyphLevel(glyphId) >= GlyphDb.MaxLevel) return "這枚燼符已達最高等級";
        if (GlyphTries <= 0) return "沒有淬鍊機會：通關深淵裂隙即可獲得";
        return null;
    }

    /// <summary>Spends one banked attempt on a glyph.</summary>
    /// <returns>1 the glyph gained a level, 0 the attempt failed, −1 nothing was spent.</returns>
    public int UpgradeGlyph(string glyphId)
    {
        string why = WhyCantUpgradeGlyph(glyphId);
        if (why != null) { Log.Debug("paragon", $"tempering '{glyphId}' refused: {why}"); return -1; }
        int level = GlyphLevel(glyphId);
        int pick = PickGlyphTry(level);
        if (pick < 0) return -1;
        int tier = D.GlyphTries[pick];
        D.GlyphTries.RemoveAt(pick);
        float chance = GlyphDb.UpgradeChance(tier, level);
        bool ok = Rng.Chance(chance);
        Log.Info("paragon", $"tempering {glyphId} lv{level} with a tier {tier} attempt ({chance * 100f:0}%): {(ok ? "success" : "failed")}; attempts left {D.GlyphTries.Count}");
        if (ok)
        {
            D.Glyphs[glyphId] = level + 1;
            _pStatsDirty = true;
            Recalc();
        }
        else Notify();
        return ok ? 1 : 0;
    }

    /// <summary>Banks tempering attempts earned at a rift tier. Only the deepest GlyphDb.MaxTries are kept.</summary>
    public void AddGlyphTries(int tier, int count)
    {
        D.GlyphTries ??= new();
        tier = Math.Max(1, tier);
        for (int i = 0; i < count; i++) D.GlyphTries.Add(tier);
        TrimGlyphTries();
        Notify();
    }

    private void TrimGlyphTries()
    {
        var t = D.GlyphTries;
        while (t.Count > GlyphDb.MaxTries)
        {
            int low = 0;
            for (int i = 1; i < t.Count; i++) if (t[i] < t[low]) low = i;
            t.RemoveAt(low);
        }
    }

    /// <summary>A 深淵裂隙 was cleared at this tier: three tempering attempts are banked for the glyph list.</summary>
    public void OnRiftCleared(int tier)
    {
        tier = Math.Max(1, tier);
        AddGlyphTries(tier, GlyphDb.TriesPerClear);
        Log.Info("paragon", $"rift tier {tier} cleared: +{GlyphDb.TriesPerClear} tempering attempts (banked {GlyphTries})");
        if (ParagonUnlocked) ParagonHooks.Toast($"獲得 {GlyphDb.TriesPerClear} 次燼符淬鍊機會（裂隙第 {tier} 層）");
    }
}

/// <summary>
/// Glue between the plain Hero and the running game: listens for rift clears on World.Event (once, for whichever
/// hero is in play) and shows 燼紋 toasts on the HUD.
/// </summary>
public static class ParagonHooks
{
    private static bool _hooked;

    public static void Ensure()
    {
        if (_hooked) return;
        _hooked = true;
        World.Event += OnWorldEvent;
        Log.Info("paragon", "listening for rift clears");
    }

    private static void OnWorldEvent(string evt, string arg)
    {
        if (evt != "rift") return;
        var game = Game.I;
        var hero = game?.Hero;
        if (hero == null) return;
        int tier = ParseTier(arg);
        if (tier <= 0) tier = game.Level != null && game.Level.RiftTier > 0 ? game.Level.RiftTier : Math.Max(1, hero.D.RiftBest);
        hero.OnRiftCleared(tier);
    }

    /// <summary>Reads the tier from the event argument ("12", or anything ending in digits such as "rift:12").</summary>
    private static int ParseTier(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return 0;
        int end = arg.Length;
        while (end > 0 && !char.IsDigit(arg[end - 1])) end--;
        int start = end;
        while (start > 0 && char.IsDigit(arg[start - 1])) start--;
        return end > start && int.TryParse(arg.AsSpan(start, end - start), out int t) ? t : 0;
    }

    public static void Toast(string text)
    {
        try { Game.I?.Hud?.Toast(text, Palette.Ember); }
        catch (Exception e) { Log.Warn("paragon", "toast failed: " + e.Message); }
    }
}
