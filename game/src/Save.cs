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
    public Item[] Equip = new Item[9];
    public List<Item> Bag = new();
    public List<Item> Stash = new();
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

    public static bool Save(int slot, SaveData d)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            d.SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            d.Version = FormatVersion;
            string json = JsonSerializer.Serialize(d, Opt);
            string tmp = PathOf(slot, "tmp"), main = PathOf(slot), bak = PathOf(slot, "bak");
            File.WriteAllText(tmp, json);
            if (File.Exists(main)) File.Copy(main, bak, true);
            File.Move(tmp, main, true);
            Log.Info("save", $"slot {slot} saved: {d.Name} lv{d.Level} {Names.Class(d.Class)} area={d.Area} bytes={json.Length}");
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
                var d = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(p), Opt);
                if (d == null) throw new InvalidDataException("empty save");
                Repair(d);
                Log.Info("save", $"slot {slot} loaded from .{ext}: {d.Name} lv{d.Level} {Names.Class(d.Class)}");
                return d;
            }
            catch (Exception e)
            {
                Log.Error("save", $"slot {slot} .{ext} unreadable", e);
            }
        }
        return null;
    }

    /// <summary>Fills in anything missing so older or hand-edited saves keep loading.</summary>
    private static void Repair(SaveData d)
    {
        d.Skills ??= new(); d.Ups ??= new(); d.Talents ??= new(); d.Bag ??= new(); d.Stash ??= new(); d.Codex ??= new();
        d.Quests ??= new(); d.Waypoints ??= new() { "town" }; d.Flags ??= new(); d.Glyphs ??= new(); d.Sockets ??= new();
        d.Enchants ??= new(); d.Renown ??= new();
        d.Fog ??= new();
        if (d.Resume != null && (string.IsNullOrEmpty(d.Resume.Area) || d.Resume.Area != d.Area)) d.Resume = null;   // not where the save says the hero is: wake as before
        if (d.Resume != null) { d.Resume.Cds ??= new(); d.Resume.DeadMons ??= new(); d.Resume.Spent ??= new(); d.Resume.Loot ??= new(); d.Resume.Gold ??= new(); d.Resume.Shards ??= new(); d.Resume.Chests ??= new(); d.Resume.Loot.RemoveAll(l => l == null || l.It == null); }
        if (d.Bar == null || d.Bar.Length != 6) d.Bar = new string[6];
        if (d.Equip == null || d.Equip.Length != 9) { var e = new Item[9]; if (d.Equip != null) Array.Copy(d.Equip, e, Math.Min(9, d.Equip.Length)); d.Equip = e; }
        d.Level = Math.Clamp(d.Level, 1, Balance.LevelCap);
        d.Bag.RemoveAll(i => i == null); d.Stash.RemoveAll(i => i == null);
        foreach (var it in d.Bag.Concat(d.Stash).Concat(d.Equip.Where(i => i != null))) { it.Affixes ??= new(); it.Gems ??= new(); }
        if (string.IsNullOrEmpty(d.Area)) d.Area = "town";
    }

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
