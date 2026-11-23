using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AshenAbyss;

/// <summary>Everything persisted for one character. Plain data; serialised as JSON with public fields.</summary>
public sealed partial class SaveData
{
    /// <summary>Save format: 1 = original; 2 = adds Resume (exact place and state of the current area), Portal and Fog (LevelState.cs).</summary>
    public int Version = SaveSystem.FormatVersion;
    public string Name = "";
    public ClassId Class;
    public bool Hardcore, Dead;
    public int Level = 1;
    public long Xp;
    public int Paragon;                 // 燼紋等級 earned after the level cap
    public long ParagonXp;
    public long Gold;
    public int Scrap, Dust, Cores;      // 碎鐵 / 幽塵 / 燼核
    public int BonusSkillPts, BonusParagonPts, PotionCap = 4;
    public Dictionary<string, int> Skills = new();     // skill or passive id → rank
    public List<string> Ups = new();                   // taken skill upgrades
    public string[] Bar = new string[6];               // LMB, RMB, 1, 2, 3, 4
    public string Spec;                                // class specialisation id
    public List<int> Talents = new();                  // paragon board nodes
    public Dictionary<string, int> Glyphs = new();     // glyph id → level
    public Dictionary<string, string> Sockets = new(); // board socket → glyph id
    public Item[] Equip = new Item[SaveSystem.EquipSlots];      // by EquipSlot; 0 = MainHand (the old Weapon), 9 = OffHand
    public List<Item> Bag = new();
    // Stash (the shared stash view) and OwnStash (a 0.1–0.3 save's own stash): SharedStash.cs
    public Dictionary<string, float> Codex = new();    // aspect id → best roll found
    public Dictionary<string, int> Enchants = new();   // item uid → times enchanted
    public Dictionary<string, int> Quests = new();     // quest id → stage
    public List<string> Waypoints = new() { "town" };
    public List<string> Flags = new();                 // strongholds, dungeons, altars, bosses, tutorials…
    public Dictionary<string, int> Renown = new();     // region id → renown points
    public int Tier, MaxTier, RiftBest;
    public double PlayTime;
    public string Area = "town";
    public string SavedAt = "";
    public int Kills, Deaths, Elites, Bosses, Seed = Rng.R.Next();

    public bool Has(string flag) => Flags.Contains(flag);
    public bool Set(string flag) { if (Flags.Contains(flag)) return false; Flags.Add(flag); return true; }
}

public sealed class SlotInfo
{
    public int Slot;
    public bool Exists, Corrupt;
    public string Name, Area, SavedAt;
    public ClassId Class;
    public int Level, Paragon, Tier;
    public bool Hardcore, Dead;
    public double PlayTime;
}

/// <summary>
/// Save slots live in user://saves/slotN.json (…\AppData\Roaming\AshenAbyss\saves). Writes are atomic
/// (temp file, then replace) and the previous version is kept as slotN.bak, which is used if the main file is damaged.
/// </summary>
public static class SaveSystem
{
    public const int SlotCount = 8;
    /// <summary>1 = original; 2 = with the exact resume state (written only while LevelState.Enabled).</summary>
    public static int FormatVersion => LevelState.Enabled ? 2 : 1;
    private static readonly JsonSerializerOptions Opt = new() { IncludeFields = true, WriteIndented = false, AllowTrailingCommas = true };
    private static string Dir => ProjectSettings.GlobalizePath("user://saves");
    private static string PathOf(int slot, string ext = "json") => System.IO.Path.Combine(Dir, $"slot{slot}.{ext}");

    /// <summary>Slots whose main file was unreadable at the last load (the backup was used): the next save keeps that backup.</summary>
    private static readonly HashSet<int> BadMain = new();

    public static bool Save(int slot, SaveData d)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            d.SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            d.Version = FormatVersion;
            // the stash: a character attached to the shared stash writes none of its own (SharedStash.cs); the shared file
            // is written on the side that gains items first, so a crash in between can duplicate an item but never lose one
            bool shared = SharedStash.IsShared(d);
            d.OwnStash = shared ? null : d.Stash;
            bool gained = shared && SharedStash.Gained();
            if (gained && !SharedStash.Write()) { Log.Error("save", $"slot {slot} not saved: the shared stash could not be written first"); return false; }
            string json = JsonSerializer.Serialize(d, Opt);
            string tmp = PathOf(slot, "tmp"), main = PathOf(slot), bak = PathOf(slot, "bak");
            File.WriteAllText(tmp, json);
            if (File.Exists(main) && !BadMain.Remove(slot)) File.Copy(main, bak, true);      // a damaged main file never replaces a good backup
            File.Move(tmp, main, true);
            Log.Info("save", $"slot {slot} saved: {d.Name} lv{d.Level} {Names.Class(d.Class)} area={d.Area} bytes={json.Length}");
            if (shared && !gained) SharedStash.Write();
            return true;
        }
        catch (Exception e)
        {
            Log.Error("save", $"saving slot {slot} failed", e);
            return false;
        }
    }

    public static SaveData Load(int slot)
    {
        foreach (var ext in new[] { "json", "bak" })
        {
            string p = PathOf(slot, ext);
            if (!File.Exists(p)) continue;
            try
            {
                string text = File.ReadAllText(p);
                SaveData d;
                try { d = JsonSerializer.Deserialize<SaveData>(text, Opt); }
                catch (JsonException) when (WithoutResume(text) is { } bare)
                {
                    // the character is fine but its resume state is damaged: keep the character, wake it the old way
                    d = JsonSerializer.Deserialize<SaveData>(bare, Opt);
                    Log.Warn("save", $"slot {slot} .{ext}: the resume state could not be read and was dropped; the hero wakes at a portal as before");
                }
                if (d == null) throw new InvalidDataException("empty save");
                Repair(d);
                if (ext == "json") BadMain.Remove(slot);
                Log.Info("save", $"slot {slot} loaded from .{ext}: {d.Name} lv{d.Level} {Names.Class(d.Class)}");
                return d;
            }
            catch (Exception e)
            {
                // the main file damaged with a backup still to try: a warning (the backup is the save then); else an error
                if (ext == "json" && File.Exists(PathOf(slot, "bak"))) { BadMain.Add(slot); Log.Warn("save", $"slot {slot} .json unreadable ({e.GetType().Name}: {e.Message}); trying the backup .bak"); }
                else Log.Error("save", $"slot {slot} .{ext} unreadable", e);
            }
        }
        return null;
    }

    /// <summary>The save text with the exact-resume parts (Resume, Portal) removed; null when the text is not a JSON object at all.</summary>
    private static string WithoutResume(string text)
    {
        try
        {
            if (System.Text.Json.Nodes.JsonNode.Parse(text) is not System.Text.Json.Nodes.JsonObject o) return null;
            if (!o.Remove("Resume") & !o.Remove("Portal")) return null;         // nothing to drop: the damage is elsewhere
            return o.ToJsonString();
        }
        catch (Exception) { return null; }
    }

    /// <summary>Fills in anything missing so older or hand-edited saves keep loading.</summary>
    private static void Repair(SaveData d)
    {
        d.Skills ??= new(); d.Ups ??= new(); d.Talents ??= new(); d.Bag ??= new(); if (d.OwnStash != null) d.Stash = d.OwnStash; d.Stash ??= new(); d.Codex ??= new();      // the stash: SharedStash.Attach takes over
        d.Quests ??= new(); d.Waypoints ??= new() { "town" }; d.Flags ??= new(); d.Glyphs ??= new(); d.Sockets ??= new();
        d.Enchants ??= new(); d.Renown ??= new();
        d.GearSets ??= new(); d.Discards ??= new(); d.Junk ??= new();      // modular gear: gear sets and bag marks (absent before 0.4)
        d.Fog ??= new();
        if (d.Resume != null && (string.IsNullOrEmpty(d.Resume.Area) || d.Resume.Area != d.Area)) d.Resume = null;   // not where the save says the hero is: wake as before
        if (d.Resume != null) { d.Resume.Cds ??= new(); d.Resume.DeadMons ??= new(); d.Resume.Spent ??= new(); d.Resume.Loot ??= new(); d.Resume.Gold ??= new(); d.Resume.Shards ??= new(); d.Resume.Chests ??= new(); d.Resume.Hurt ??= new(); d.Resume.EventsDone ??= new(); d.Resume.Loot.RemoveAll(l => l == null || l.It == null); }
        if (d.Bar == null || d.Bar.Length != 6) d.Bar = new string[6];
        if (d.Equip == null || d.Equip.Length != EquipSlots) { var e = new Item[EquipSlots]; if (d.Equip != null) Array.Copy(d.Equip, e, Math.Min(EquipSlots, d.Equip.Length)); d.Equip = e; }
        d.Level = Math.Clamp(d.Level, 1, Balance.LevelCap);
        d.Bag.RemoveAll(i => i == null); d.Stash.RemoveAll(i => i == null);
        foreach (var it in d.Bag.Concat(d.Stash).Concat(d.Equip.Where(i => i != null))) { it.Affixes ??= new(); it.Gems ??= new(); it.Implicits ??= new(); }
        MigrateGear(d);
    }

    /// <summary>Equipment slots in a save: 9 up to 0.3 (the old Weapon slot is index 0 = MainHand), 10 since (OffHand appended).</summary>
    public const int EquipSlots = 10;

    /// <summary>
    /// 0.1–0.3 → modular gear (idempotent). Every weapon without a base gets its class's legacy base with the same damage
    /// per second (ItemDb.Migrate); armour, jewellery and gems get their base and look ids. When the worn weapon was the
    /// lamp knight's sword-and-shield, the pactbinder's grimoire or the forgebody's pair of fists, a plain companion piece
    /// of the same power goes into the empty off hand so the hero keeps what the old weapon showed.
    /// </summary>
    public static int MigrateGear(SaveData d)
    {
        int n = 0;
        var main = d.Equip[(int)EquipSlot.MainHand];
        bool mainWasOld = main != null && main.Kind == ItemKind.Weapon && ItemDb.BaseOf(main.Base) == null;
        foreach (var it in d.Bag.Concat(d.Stash).Concat(d.Equip.Where(i => i != null))) if (ItemDb.Migrate(it, d.Class)) n++;
        if (d.Resume?.Loot != null) foreach (var l in d.Resume.Loot) if (ItemDb.Migrate(l.It, d.Class)) n++;
        if (d.Portal?.State?.Loot != null) foreach (var l in d.Portal.State.Loot) if (l?.It != null && ItemDb.Migrate(l.It, d.Class)) n++;
        // a main-hand item that cannot be there any more (an off-hand piece) moves to the off hand
        if (main != null && main.Kind == ItemKind.OffHand && d.Equip[(int)EquipSlot.OffHand] == null) { d.Equip[(int)EquipSlot.OffHand] = main; d.Equip[(int)EquipSlot.MainHand] = null; }
        if (mainWasOld && d.Equip[(int)EquipSlot.OffHand] == null && main.WeaponClass < 0)
        {
            var comp = ItemDb.LegacyCompanion(main, d.Class);
            if (comp != null) { d.Equip[(int)EquipSlot.OffHand] = comp; n++; }
        }
        if (n > 0) Log.Info("save", $"gear migration: {n} item(s) brought to the modular-gear format for {d.Name} ({Names.Class(d.Class)}); main hand {main?.Base}, off hand {d.Equip[(int)EquipSlot.OffHand]?.Base}");
        return n;
        if (string.IsNullOrEmpty(d.Area)) d.Area = "town";
    }

    /// <summary>A save read from JSON text exactly as a slot is loaded (repair and gear migration included); null when unreadable. Touches no file.</summary>
    public static SaveData FromJson(string json)
    {
        var d = JsonSerializer.Deserialize<SaveData>(json, Opt);
        if (d != null) Repair(d);
        return d;
    }

    /// <summary>JSON text of a save as a slot would hold it (no file written).</summary>
    public static string ToJson(SaveData d) { d.OwnStash = SharedStash.IsShared(d) ? null : d.Stash; return JsonSerializer.Serialize(d, Opt); }

    public static List<SlotInfo> List()
    {
        var l = new List<SlotInfo>();
        for (int i = 1; i <= SlotCount; i++)
        {
            var info = new SlotInfo { Slot = i };
            if (File.Exists(PathOf(i)) || File.Exists(PathOf(i, "bak")))
            {
                var d = LoadQuiet(i);
                if (d == null) { info.Exists = true; info.Corrupt = true; }
                else
                {
                    info.Exists = true; info.Name = d.Name; info.Class = d.Class; info.Level = d.Level; info.Paragon = d.Paragon; info.Tier = d.Tier;
                    info.Area = d.Area; info.SavedAt = d.SavedAt; info.Hardcore = d.Hardcore; info.Dead = d.Dead; info.PlayTime = d.PlayTime;
                }
            }
            l.Add(info);
        }
        return l;
    }

    private static SaveData LoadQuiet(int slot)
    {
        foreach (var ext in new[] { "json", "bak" })
        {
            try
            {
                string p = PathOf(slot, ext);
                if (!File.Exists(p)) continue;
                var d = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(p), Opt);
                if (d != null) { Repair(d); return d; }
            }
            catch { }
        }
        return null;
    }

    public static void Delete(int slot)
    {
        try
        {
            foreach (var ext in new[] { "json", "bak", "tmp" })
                if (File.Exists(PathOf(slot, ext))) File.Delete(PathOf(slot, ext));
            Log.Info("save", $"slot {slot} deleted");
        }
        catch (Exception e) { Log.Error("save", $"deleting slot {slot} failed", e); }
    }
}

/// <summary>User settings in user://settings.json.</summary>
public sealed class Settings
{
    public float Master = 0.9f, Music = 0.7f, Sfx = 0.9f, Voice = 1f, Ambience = 0.8f;
    public bool Fullscreen = true, VSync = true, DamageNumbers = true, ScreenShake = true, Subtitles = true, ShowAllLoot = false;
    public int Quality = 2;             // 0 低, 1 中, 2 高, 3 極致
    public float RenderScale = 0f;      // 0 = automatic from resolution
    public int LastSlot = 0;
    public float Brightness = 1f;       // 亮度 0.8 … 1.3: exposure of the 3D picture on top of the level grading (applied by Gfx.TuneEnvironment); 1 = the dark look the game is graded for
    public int UiTextScale = 0;         // 介面文字大小：0 標準 (×1.0), 1 大 (×1.15), 2 特大 (×1.3) — applied by UiTheme.ApplyTextScale
    public bool SystemCursor = false;   // 滑鼠游標：false 遊戲 (the forged cursors, Cursors.cs), true 系統 (the operating system's own pointer)
    public int GearWeight = 0;          // 裝備比較：0 平衡 (50/50), 1 偏重攻擊 (75/25), 2 偏重防禦 (25/75) — GearEval
    public string[] SetKeys = { "F1", "F2", "F3", "F4", "F5", "F6", "E" };   // 裝備組 1–6 and 快速切換 (GearSets; Godot Key names)
    public int ScreenshotMode = 0;      // 截圖方式 (PrtSc / F12): 0 剪貼簿（預設）, 1 存成檔案, 2 剪貼簿與檔案 — Game.TakeScreenshot (FIXES)

    private static readonly JsonSerializerOptions Opt = new() { IncludeFields = true, WriteIndented = true };
    private static string FilePath => ProjectSettings.GlobalizePath("user://settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Opt);
                if (s != null) { Log.Info("cfg", $"settings loaded quality={s.Quality} scale={s.RenderScale} fullscreen={s.Fullscreen}"); return s; }
            }
        }
        catch (Exception e) { Log.Error("cfg", "settings unreadable, using defaults", e); }
        return new Settings();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opt)); }
        catch (Exception e) { Log.Error("cfg", "saving settings failed", e); }
    }
}
