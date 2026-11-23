using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// 亡者名冊 on the character sheet: which kind each line uses (or that it was sacrificed), the upgrade pick of each kind,
/// what a change costs, the sacrifice bonuses (added to the stats by Recalc) and the migration of saves from before the book.
/// </summary>
public sealed partial class Hero
{
    public ClassBookDef Book => ClassBook.Of(Class);
    public bool BookUnlocked => Book != null && D.Level >= Book.UnlockLevel;

    /// <summary>
    /// Gives the hero its book the first time it is asked for. A save from before the book existed (a Soulbinder past level 1
    /// with skills already spent) gets the default book and one free change (the brief's "free respec") and is logged.
    /// </summary>
    public BookData EnsureBook()
    {
        var b = Book;
        if (b == null) return null;
        if (D.Book == null)
        {
            D.Book = new BookData();
            bool old = true;          // new heroes get an empty book in Hero.NewCharacter: no book at all means a save from before it
            for (int i = 0; i < b.Lines.Length; i++)
            {
                D.Book.Pick[b.Lines[i].Id] = i < b.Defaults.Length ? b.Defaults[i] : b.Lines[i].Kinds[0].Id;
                D.Book.Up[b.Lines[i].Id] = -1;
            }
            if (old) { D.Book.Migrated = true; D.Book.FreeChanges = 1; Log.Info("book", $"{D.Name} ({Names.Class(Class)} lv{D.Level}): save from before 亡者名冊 — default book (掠襲者 / 影術士 / 骨魔像) and one free change"); }
        }
        D.Book.Pick ??= new(); D.Book.Up ??= new();
        foreach (var l in b.Lines)
        {
            if (!D.Book.Pick.TryGetValue(l.Id, out var k) || (k != ClassBook.Sacrificed && l.Kinds.All(x => x.Id != k))) D.Book.Pick[l.Id] = l.Kinds[0].Id;
            if (!D.Book.Up.ContainsKey(l.Id)) D.Book.Up[l.Id] = -1;
        }
        return D.Book;
    }

    /// <summary>The kind chosen in a line, null when sacrificed (or the class has no such book line).</summary>
    public BookKindDef BookKind(string line)
    {
        if (Book == null) return null;
        var bd = EnsureBook();
        return bd.Pick.TryGetValue(line, out var k) && k != ClassBook.Sacrificed ? ClassBook.Kind(k) : null;
    }

    public bool LineSacrificed(string line) => Book != null && EnsureBook().Pick.TryGetValue(line, out var k) && k == ClassBook.Sacrificed;
    public string BookKindId(string line) => BookKind(line)?.Id;
    /// <summary>The upgrade pick (0 or 1) taken in a line, or -1.</summary>
    public int BookUp(string line) => Book == null ? -1 : EnsureBook().Up.GetValueOrDefault(line, -1);
    public bool HasBookUp(string kindId, int up) { var k = ClassBook.Kind(kindId); return k != null && BookKindId(k.Line) == kindId && BookUp(k.Line) == up; }
    public int SacrificedCount => Book == null ? 0 : Book.Lines.Count(l => LineSacrificed(l.Id));

    /// <summary>Gold one change costs where the hero stands now: nothing in town, nothing while a free change is left.</summary>
    public int BookFee(bool inTown) => inTown || (D.Book?.FreeChanges ?? 0) > 0 ? 0 : ClassBook.Fee(D.Level);

    /// <summary>
    /// Changes one line: <paramref name="kindId"/> a kind of that line or <see cref="ClassBook.Sacrificed"/>, <paramref name="up"/>
    /// the upgrade pick (−1 none). Fails (with the reason in <paramref name="why"/>) below the unlock level or without the fee.
    /// </summary>
    public bool SetBook(string line, string kindId, int up, bool inTown, out string why)
    {
        why = null;
        var b = Book;
        if (b == null) { why = "此職業沒有亡者名冊。"; return false; }
        if (!BookUnlocked) { why = $"等級 {b.UnlockLevel} 解鎖。"; return false; }
        var l = ClassBook.Line(b, line);
        if (l == null) { why = "沒有這條亡者線。"; return false; }
        if (kindId != ClassBook.Sacrificed && l.Kinds.All(k => k.Id != kindId)) { why = "沒有這個種類。"; return false; }
        if (kindId == ClassBook.Sacrificed) up = -1;
        up = Math.Clamp(up, -1, 1);
        var bd = EnsureBook();
        if (bd.Pick.GetValueOrDefault(line) == kindId && bd.Up.GetValueOrDefault(line, -1) == up) return true;    // nothing changes, nothing is paid
        int fee = BookFee(inTown);
        if (fee > 0 && D.Gold < fee) { why = $"需要 {fee} 金幣（在殘燈鎮變更免費）。"; return false; }
        if (fee > 0) D.Gold -= fee;
        else if (!inTown && bd.FreeChanges > 0) bd.FreeChanges--;
        string was = bd.Pick.GetValueOrDefault(line);
        bd.Pick[line] = kindId; bd.Up[line] = up;
        Log.Info("book", $"{line}: {was} → {kindId} up={up} fee={fee} town={inTown} gold={D.Gold}");
        Recalc();
        BookChanged?.Invoke(line);
        return true;
    }

    /// <summary>Raised after a line of the book changed (MinionSystem respawns that line's minions).</summary>
    public static event Action<string> BookChanged;

    /// <summary>Multiplier of every sacrifice bonus: 獻祭 specialisation ×1.5, the 獻祭聖約 legendary power on top.</summary>
    public float SacrificeMul => (D.Spec == "sb_offering" ? 1.5f : 1f) * (1f + Asp("sb_sacrament") / 100f);

    /// <summary>Sacrifice bonuses (called by Recalc after the paragon board).</summary>
    private void ApplyBookStats()
    {
        var b = Book;
        if (b == null || D.Book == null || D.Level < b.UnlockLevel) return;
        float mul = SacrificeMul;
        foreach (var l in b.Lines)
        {
            if (!LineSacrificed(l.Id)) continue;
            foreach (var (s, v) in l.SacStats) S.Add(s, v * mul);
            if (l.SacArmorPct > 0f)
            {
                float gear = 0f;
                foreach (var it in D.Equip) if (it != null) gear += it.Armor * it.BaseMul;
                S.Add(Stat.Armor, gear * l.SacArmorPct / 100f * mul);
            }
        }
        // 三線盡獻: the whole book given up — the lone soul's build path (bot fights: without this a minionless
        // Soulbinder died 5-8 times where every golem build died 0)
        if (SacrificedCount == b.Lines.Length) { S.Add(Stat.DmgAll, AllSacrificedDmg); S.Add(Stat.DmgReduction, AllSacrificedDr); }
    }

    public const float AllSacrificedDmg = 0f, AllSacrificedDr = 10f;

    /// <summary>Book and golem effects on one skill (called by Eff): the command's cooldown, the warriors' cap.</summary>
    private void BookEff(string id, SkillDef e)
    {
        if (Class != ClassId.Soulbinder) return;
        if (id == "sb_golem_cmd")
        {
            e.Cooldown = MathF.Max(4f, e.Cooldown * (1f - MathF.Min(50f, S[Stat.CmdCdr]) / 100f));
            if (HasUnique("u_crucible")) e.Cooldown = MathF.Max(4f, e.Cooldown - 3f);
        }
        if (id == "sb_raise" && HasBookUp("w_raider", 1)) e.SummonMax += 1;
    }
}
