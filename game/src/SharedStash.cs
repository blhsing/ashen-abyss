using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AshenAbyss;

/// <summary>The shared stash file (user://saves/stash.json; hardcore: stash_hc.json).</summary>
public sealed class StashFile
{
    public int Ver = 1;
    public int Tabs = SharedStash.BaseTabs;
    public List<Item> Items = new();
    public string SavedAt = "";
}

/// <summary>Shared-stash fields of a character save (Save.cs declares the rest).</summary>
public sealed partial class SaveData
{
    /// <summary>
    /// Runtime view of the stash: once <see cref="SharedStash.Attach"/> ran for this session it IS the shared stash's item
    /// list (every character of the account sees the same one); before that, the save's own stash (<see cref="OwnStash"/>).
    /// Never written with the character.
    /// </summary>
    [JsonIgnore] public List<Item> Stash = new();
    /// <summary>The per-character stash of 0.1–0.3 saves ("Stash" in the file). Moved into the shared stash on the first load, then null.</summary>
    [JsonPropertyName("Stash"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<Item> OwnStash;
    /// <summary>True once this character's own stash went into the shared stash (never migrated twice).</summary>
    public bool StashShared;
    /// <summary>A stable id of the character (who put an item into the shared stash: <see cref="Item.StashBy"/>).</summary>
    public string Cid;
}

/// <summary>
/// The account-wide stash (0.4.0): one stash for all normal characters and one for all hardcore characters, so items
/// never cross between the two. Tabs of <see cref="TabSize"/> cells (<see cref="BaseTabs"/> to start; a migration that
/// needs more adds tabs, up to <see cref="MaxTabs"/>, and never drops an item). An item's tab is <see cref="Item.Tab"/>.
///
/// Files: user://saves/stash.json and stash_hc.json for the menu slots 1–8; a test slot N (0, 90–99) uses its own
/// slotN.stash.json / slotN.stash_hc.json, emptied when a new test hero starts (unless --stash-keep), so test runs
/// never touch the player's stash and behave as the old per-save stash did. Demo heroes (slot -1) get none.
/// Writes are atomic (temp file, then replace) and the previous good file is kept as .bak, which is read when the main
/// file is damaged. The file is read fresh whenever a character is loaded and written whenever its character is saved
/// (and soon after every change), the side that gains an item first, so a crash can at worst duplicate an item, never
/// lose one. Only one game runs at a time (single-instance lock) and one character is loaded at a time, so the session's
/// list is the newest view.
/// </summary>
public static class SharedStash
{
    public const int TabSize = 80, BaseTabs = 4, MaxTabs = 12;
    private const string Cat = "stash";
    private static readonly JsonSerializerOptions Opt = new() { IncludeFields = true, WriteIndented = false, AllowTrailingCommas = true };

    /// <summary>Tabs of the attached stash (BaseTabs when none is attached).</summary>
    public static int Tabs { get; private set; } = BaseTabs;
    public static int Capacity => Tabs * TabSize;
    /// <summary>The character the stash is attached to (null: none; its D.Stash is then its own list).</summary>
    public static SaveData Owner { get; private set; }
    public static string FilePath => _path;
    public static bool Hardcore { get; private set; }
    /// <summary>The stash tab the stash screen shows (new deposits go there while it has room).</summary>
    public static int Page;

    private static string _path, _lastJson;
    private static HashSet<string> _lastUids = new();
    private static bool _badMain;

    private static string Dir => ProjectSettings.GlobalizePath("user://saves");

    /// <summary>The stash file of a slot (null for the demo and other slots that keep nothing).</summary>
    public static string PathFor(int slot, bool hardcore)
    {
        string hc = hardcore ? "_hc" : "";
        if (slot >= 1 && slot <= SaveSystem.SlotCount) return System.IO.Path.Combine(Dir, $"stash{hc}.json");
        if (slot == 0 || slot >= 90 && slot <= 99) return System.IO.Path.Combine(Dir, $"slot{slot}.stash{hc}.json");
        return null;
    }

    public static bool IsShared(SaveData d) => d != null && ReferenceEquals(Owner, d);
    /// <summary>The items of tab <paramref name="tab"/> in list order.</summary>
    public static IEnumerable<Item> TabItems(List<Item> stash, int tab) => stash.Where(i => i != null && Math.Clamp(i.Tab, 0, Int32.MaxValue) == tab);
    public static int CountIn(List<Item> stash, int tab) => stash.Count(i => i != null && i.Tab == tab);

    /// <summary>A tab with a free cell: <paramref name="prefer"/> if it has one, else the first; -1 when every tab is full.</summary>
    public static int FreeTab(List<Item> stash, int prefer = -1)
    {
        var counts = new int[Math.Max(Tabs, 1)];
        foreach (var it in stash) if (it != null && it.Tab >= 0 && it.Tab < counts.Length) counts[it.Tab]++;
        if (prefer >= 0 && prefer < counts.Length && counts[prefer] < TabSize) return prefer;
        for (int t = 0; t < counts.Length; t++) if (counts[t] < TabSize) return t;
        return -1;
    }

    /// <summary>Did <paramref name="it"/> go into the shared stash from this character? (False for items another character put there.)</summary>
    public static bool PutBy(Item it, SaveData d) => it != null && d != null && (!IsShared(d) || it.StashBy == null || it.StashBy == d.Cid);

    // ───────────────────────── attach (character loaded) ─────────────────────────

    /// <summary>
    /// Session start: reads the stash file of this slot fresh, moves a not-yet-migrated character's own stash into it,
    /// and makes <c>d.Stash</c> the shared list. <paramref name="fresh"/>: a new test hero (test slots start empty).
    /// </summary>
    public static void Attach(SaveData d, int slot, bool fresh)
    {
        Detach();
        if (d == null) return;
        d.Cid ??= Guid.NewGuid().ToString("N")[..12];
        string path = PathFor(slot, d.Hardcore);
        if (path == null) { d.Stash = d.OwnStash ?? d.Stash ?? new(); return; }      // demo: the hero's own list, never written
        bool test = slot == 0 || slot >= 90;
        var file = fresh && test && Game.I?.Arg("stash-keep") != true ? new StashFile() : Read(path);
        if (fresh && test && file.Items.Count == 0) Log.Info(Cat, $"test slot {slot}: the test stash starts empty");
        _path = path; Hardcore = d.Hardcore;
        Tabs = Math.Clamp(file.Tabs, BaseTabs, MaxTabs * 4);
        var items = file.Items ?? new();
        items.RemoveAll(i => i == null);
        foreach (var it in items) { it.Affixes ??= new(); it.Gems ??= new(); it.Implicits ??= new(); }
        int migrated = 0, reuid = 0;
        // a save not yet migrated; or items a migrated save gained while no shared stash was attached (a test or a tool
        // that edited the save directly): they join the shared stash too
        if (!d.StashShared || d.OwnStash is { Count: > 0 })
        {
            var own = d.OwnStash ?? new();
            own.RemoveAll(i => i == null);
            var uids = new HashSet<string>(items.Select(i => i.Uid));
            foreach (var it in own)
            {
                it.Affixes ??= new(); it.Gems ??= new(); it.Implicits ??= new();
                if (string.IsNullOrEmpty(it.Uid) || uids.Contains(it.Uid))
                {
                    string old = it.Uid;
                    it.Uid = Guid.NewGuid().ToString("N")[..12];
                    reuid++;
                    if (old != null && d.GearSets != null)               // this character's sets keep pointing at its item
                        foreach (var s in d.GearSets) if (s?.Uids != null) for (int k = 0; k < s.Uids.Length; k++) if (s.Uids[k] == old) s.Uids[k] = it.Uid;
                }
                uids.Add(it.Uid);
                it.StashBy = d.Cid;
                it.Tab = -1;                                             // placed below
                items.Add(it);
                migrated++;
            }
            d.OwnStash = null;
            d.StashShared = true;
        }
        Normalize(items);
        d.Stash = items;
        Owner = d;
        _lastJson = null; _lastUids = new();
        if (migrated > 0 || _badMain)
        {
            // migrated: the stash first (if the game stopped before the character is written, the items are in both, never
            // in neither). Read from the backup: the main file is made whole again at once (the backup stays as it is).
            Write(force: true);
            if (migrated > 0) Log.Info(Cat, $"migrated {migrated} item(s) of {d.Name} into the {(d.Hardcore ? "hardcore " : "")}shared stash ({reuid} re-uid); now {items.Count}/{Capacity} in {Tabs} tabs");
        }
        else { _lastJson = Json(items); _lastUids = new HashSet<string>(items.Select(i => i.Uid)); }
        Log.Info(Cat, $"attached {(d.Hardcore ? "hardcore " : "")}stash {System.IO.Path.GetFileName(path)} to {d.Name} (slot {slot}): {items.Count}/{Capacity} item(s), {Tabs} tabs");
    }

    public static void Detach() { Owner = null; _path = null; _lastJson = null; _lastUids = new(); Tabs = BaseTabs; _badMain = false; }

    /// <summary>Every item gets a tab with room; tabs are added when needed (an item is never dropped).</summary>
    private static void Normalize(List<Item> items)
    {
        var counts = new Dictionary<int, int>();
        var homeless = new List<Item>();
        foreach (var it in items)
        {
            if (it.Tab < 0 || it.Tab >= MaxTabs * 4) { homeless.Add(it); continue; }
            counts.TryGetValue(it.Tab, out int n);
            if (n >= TabSize) { homeless.Add(it); continue; }
            counts[it.Tab] = n + 1;
            if (it.Tab >= Tabs) Tabs = it.Tab + 1;
        }
        foreach (var it in homeless)
        {
            int t = 0;
            while (counts.TryGetValue(t, out int n) && n >= TabSize) t++;
            it.Tab = t;
            counts[t] = (counts.TryGetValue(t, out int c) ? c : 0) + 1;
            if (t >= Tabs) Tabs = t + 1;
        }
    }

    // ───────────────────────── files ─────────────────────────

    private static StashFile Read(string path)
    {
        _badMain = false;
        foreach (var p in new[] { path, path + ".bak" })
        {
            if (!File.Exists(p)) continue;
            try
            {
                var f = JsonSerializer.Deserialize<StashFile>(File.ReadAllText(p), Opt) ?? throw new InvalidDataException("empty stash file");
                f.Items ??= new();
                if (p != path) Log.Warn(Cat, $"{System.IO.Path.GetFileName(path)} unreadable: the backup was used ({f.Items.Count} item(s))");
                return f;
            }
            catch (Exception e)
            {
                if (p == path) { _badMain = true; Log.Warn(Cat, $"{System.IO.Path.GetFileName(p)} unreadable ({e.GetType().Name}: {e.Message}); trying the backup"); }
                else Log.Error(Cat, $"the stash backup {System.IO.Path.GetFileName(p)} is unreadable too", e);
            }
        }
        if (_badMain)
        {
            // nothing readable: both damaged files are kept aside for recovery and the stash starts empty (so play goes on
            // and new deposits are saved)
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            foreach (var p in new[] { path, path + ".bak" })
                if (File.Exists(p)) { try { File.Move(p, $"{p}.damaged_{ts}", true); } catch (Exception e) { Log.Error(Cat, $"moving {p} aside failed", e); } }
            Log.Error(Cat, $"no readable shared stash: the damaged files were kept as *.damaged_{ts}; starting an empty stash");
            _badMain = false;
        }
        return new StashFile();
    }

    private static string Json(List<Item> items) => JsonSerializer.Serialize(items, Opt);

    /// <summary>Items that are in the stash now but were not at the last write (the stash gained them).</summary>
    public static bool Gained() => Owner != null && Owner.Stash.Any(i => i != null && !_lastUids.Contains(i.Uid));

    /// <summary>Writes the stash file if it changed since the last write. Returns false only on a failed write.</summary>
    public static bool Write(bool force = false)
    {
        var d = Owner;
        if (d == null || _path == null) return true;
        try
        {
            var items = d.Stash;
            string json = Json(items);
            if (!force && json == _lastJson) return true;
            var f = new StashFile { Tabs = Tabs, Items = items, SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
            string text = JsonSerializer.Serialize(f, Opt), tmp = _path + ".tmp", bak = _path + ".bak";
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path));
            File.WriteAllText(tmp, text);
            if (File.Exists(_path) && !_badMain) File.Copy(_path, bak, true);         // a damaged main file never replaces a good backup
            _badMain = false;
            File.Move(tmp, _path, true);
            _lastJson = json; _lastUids = new HashSet<string>(items.Where(i => i != null).Select(i => i.Uid));
            Log.Info(Cat, $"{System.IO.Path.GetFileName(_path)} saved: {items.Count}/{Capacity} item(s), {Tabs} tabs, bytes={text.Length}");
            return true;
        }
        catch (Exception e) { Log.Error(Cat, $"saving the shared stash {_path} failed", e); return false; }
    }
}
