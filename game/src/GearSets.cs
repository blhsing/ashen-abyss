using Godot;
using System;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// Gear-set switching in play (裝備組): F1–F6 load sets 1–6, E swaps between the last two (keys rebindable in the
/// settings, Settings.SetKeys). Out of combat a switch is immediate; in combat it is allowed too, with no animation
/// lock, and any switch starts a 1.5 s cooldown shared by every key (no accidental double swap). In town the stash counts.
/// A toast names the set (and lists ⚠ what is missing); a small HUD line shows the active set.
/// </summary>
public static class GearSets
{
    public const double CooldownSec = 1.5;
    public static readonly string[] DefaultKeys = { "F1", "F2", "F3", "F4", "F5", "F6", "E" };
    /// <summary>Keys the game already uses in play; a set key may not take one of them.</summary>
    public static readonly string[] Reserved = { "W", "A", "S", "D", "Q", "T", "I", "C", "K", "P", "M", "J", "Tab", "Space", "Escape", "Shift", "Alt", "Ctrl", "F8", "F11", "F12", "Key1", "Key2", "Key3", "Key4" };
    private static ulong _readyAt;
    private static Label _hud;

    /// <summary>The configured key of set <paramref name="i"/> (0–5) or of the quick swap (6), as the Key enum.</summary>
    public static Key KeyOf(int i)
    {
        var cfg = Game.I?.Cfg?.SetKeys;
        string name = cfg != null && i < cfg.Length && !string.IsNullOrEmpty(cfg[i]) ? cfg[i] : DefaultKeys[i];
        return Enum.TryParse(name, true, out Key k) ? k : Key.None;
    }

    /// <summary>The key's caption for the interface: F1, E, 1 …</summary>
    public static string KeyName(int i)
    {
        var k = KeyOf(i);
        string s = k.ToString();
        return s.StartsWith("Key") && s.Length == 4 ? s[3..] : s;
    }

    public static bool InTown { get { var lv = Game.I?.Level; return lv != null && GodotObject.IsInstanceValid(lv) && lv.IsTown; } }

    /// <summary>From Ui.HandleKey: a set key in play. Returns true when it was one (handled, whatever the outcome).</summary>
    public static bool HandleKey(Key key)
    {
        if (key == Key.None) return false;
        for (int i = 0; i < 7; i++)
        {
            if (KeyOf(i) != key) continue;
            if (i == 6) QuickSwap(); else Switch(i, true);
            return true;
        }
        return false;
    }

    /// <summary>E: back to the set used before the current one.</summary>
    public static bool QuickSwap()
    {
        var h = Game.I?.Hero;
        if (h == null) return false;
        int prev = h.D.SetPrev;
        if (prev < 0 || h.SetAt(prev).Empty)
        {
            // nothing used before: the first other saved set
            prev = Enumerable.Range(0, Hero.SetCount).FirstOrDefault(j => j != h.D.SetActive && !h.SetAt(j).Empty, -1);
            if (prev < 0) { Game.I?.Hud?.Warn("尚未儲存可切換的裝備組（在物品欄的裝備組儲存目前裝備）"); return false; }
        }
        return Switch(prev, true);
    }

    /// <summary>Loads set <paramref name="i"/> now. <paramref name="fromKey"/>: a hotkey (always under the shared cooldown).</summary>
    public static bool Switch(int i, bool fromKey)
    {
        var g = Game.I; var h = g?.Hero; var p = g?.Player;
        if (h == null) return false;
        if (p != null && GodotObject.IsInstanceValid(p) && p.Dead) return false;
        ulong now = Time.GetTicksMsec();
        bool combat = p != null && GodotObject.IsInstanceValid(p) && p.InCombat > 0f;
        if ((fromKey || combat) && now < _readyAt)
        {
            g.Hud?.Warn($"裝備組冷卻中（{(_readyAt - now) / 1000.0:0.0} 秒）");
            return false;
        }
        var r = h.LoadSet(i, InTown);
        if (!r.Ok) { g.Hud?.Warn(r.Why); Snd.Ui("ui_error"); return false; }
        _readyAt = now + (ulong)(CooldownSec * 1000);
        string miss = r.Missing.Count > 0 ? $"　⚠ 缺少：{string.Join("、", r.Missing.Take(4))}{(r.Missing.Count > 4 ? "…" : "")}" : "";
        string stash = r.FromStash > 0 ? $"　（從儲藏箱取出 {r.FromStash} 件）" : "";
        g.Hud?.Toast($"已切換：{h.SetName(i)}{stash}{miss}", r.Missing.Count > 0 ? Palette.Ember : Palette.Gold);
        Snd.Ui("equip_weapon");
        RefreshHud();
        Log.Info("sets", $"switch to set {i + 1} ({(fromKey ? "key" : "click")}, {(combat ? "in combat" : "calm")}, {(InTown ? "town" : "field")}); missing {r.Missing.Count}");
        return true;
    }

    /// <summary>
    /// Rebinds set key <paramref name="i"/> (0–5 the sets, 6 the quick swap). Refused (with the reason) for a key the game
    /// already uses in play, or one another set key holds. Saved with the settings.
    /// </summary>
    public static string Rebind(int i, Key key)
    {
        var cfg = Game.I?.Cfg;
        if (cfg == null) return "設定尚未載入";
        string name = key.ToString();
        if (key == Key.None || Reserved.Contains(name)) return $"「{name}」已用於遊戲操作";
        if (cfg.SetKeys == null || cfg.SetKeys.Length != 7) cfg.SetKeys = (string[])DefaultKeys.Clone();
        for (int j = 0; j < 7; j++) if (j != i && string.Equals(cfg.SetKeys[j], name, StringComparison.OrdinalIgnoreCase)) return $"「{name}」已用於{(j < 6 ? $"裝備組 {j + 1}" : "快速切換")}";
        cfg.SetKeys[i] = name;
        cfg.Save();
        RefreshHud();
        Log.Info("cfg", $"gear-set key {i} → {name}");
        return null;
    }

    public static void ResetKeys()
    {
        var cfg = Game.I?.Cfg;
        if (cfg == null) return;
        cfg.SetKeys = (string[])DefaultKeys.Clone();
        cfg.Save();
        RefreshHud();
    }

    /// <summary>
    /// Test harness (UI script "sets"): one set per hand style the class has (雙手 / 雙持 / 單手＋副手), each with freshly
    /// rolled hands of the hero's power; the other sets' weapons wait in the bag. Set 1 ends up worn.
    /// </summary>
    public static void PrepareDemo(Hero h)
    {
        if (h == null) return;
        var rng = new Random(h.D.Seed ^ 0x5E75);
        int power = Math.Clamp(h.D.Level * 10, 10, ItemDb.MaxPower);
        var styles = ItemDb.HandStyles(h.Class);
        for (int i = 0; i < styles.Count && i < Hero.SetCount; i++)
        {
            var (mh, oh) = ItemDb.RollHands(h.Class, power, styles[i], () => Rarity.Rare, rng);
            foreach (var w in new[] { mh, oh }) if (w != null) w.ReqLevel = 1;
            foreach (var slot in new[] { EquipSlot.MainHand, EquipSlot.OffHand })
            {
                var cur = h.D.Equip[(int)slot];
                if (cur != null && !h.BagFull) { h.D.Bag.Add(cur); h.D.Equip[(int)slot] = null; }
            }
            h.D.Equip[(int)EquipSlot.MainHand] = mh;
            h.D.Equip[(int)EquipSlot.OffHand] = oh;
            h.Recalc();
            h.SaveSet(i);
            h.RenameSet(i, styles[i] switch { "2h" => "雙手", "dual" => "雙持", "off" => "單手＋副手", _ => "單手" });
        }
        h.LoadSet(0, true);
        RefreshHud();
        Log.Info("sets", $"test sets prepared: {string.Join(", ", styles)}");
    }

    /// <summary>The small HUD line with the active set (bottom-left), refreshed after every change.</summary>
    public static void RefreshHud()
    {
        var hud = Game.I?.Hud; var h = Game.I?.Hero;
        if (hud == null || !GodotObject.IsInstanceValid(hud)) return;
        if (_hud == null || !GodotObject.IsInstanceValid(_hud))
        {
            _hud = UiTheme.Outlined(UiTheme.Label("", UiTheme.FsCaption, UiTheme.Dim), 5);
            _hud.Name = "GearSetLine";
            _hud.MouseFilter = Control.MouseFilterEnum.Ignore;
            hud.AddChild(_hud);
            _hud.AnchorLeft = 0f; _hud.AnchorRight = 0f; _hud.AnchorTop = 1f; _hud.AnchorBottom = 1f;
            _hud.GrowVertical = Control.GrowDirection.Begin;
            _hud.OffsetLeft = 22f; _hud.OffsetRight = 22f; _hud.OffsetTop = -18f; _hud.OffsetBottom = -18f;
        }
        int a = h?.D.SetActive ?? -1;
        bool any = h != null && Enumerable.Range(0, Hero.SetCount).Any(j => !h.SetAt(j).Empty);
        _hud.Visible = any;
        if (!any) return;
        _hud.Text = a >= 0 ? $"裝備組：{h.SetName(a)}（{KeyName(a)}）　{KeyName(6)} 快速切換" : $"裝備組：未使用　{KeyName(0)}–{KeyName(5)} 切換";
    }
}

/// <summary>Catches the next key press for a rebinding (settings: 裝備組 keys). Esc cancels.</summary>
public partial class KeyCapture : Control
{
    public Action<Key> OnKey;
    public Action OnCancel;

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
        GetViewport().SetInputAsHandled();
        Key key = k.Keycode != Key.None ? k.Keycode : k.PhysicalKeycode;
        var done = key == Key.Escape ? null : OnKey;
        var cancel = key == Key.Escape ? OnCancel : null;
        QueueFree();
        if (done != null) Log.Guard("ui", () => done(key));
        else cancel?.Invoke();
    }
}
