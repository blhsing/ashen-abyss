using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// AI pilot strategy (the "beat the game without dying" part) and its test harness.
/// - Story bosses are not entered underlevelled: below the lair's wanted level the pilot first gains levels in the
///   highest open region whose monsters are not above the hero (they scale with the hero there), then goes in.
/// - Before a story lair: a full belt of potions (town portal to refill when it is short) and a rest by the arena until
///   the potions and life are back.
/// - In town: the merchant's upgrades are bought, the smith masterworks what is worn (gold and salvage the player has).
/// - Paragon points (from level 50) are spent on the best reachable node.
/// Test switch --pilot-test=boss[:secs] (fight the boss of the --area, count deaths and telegraphed hits, then quit) or
/// --pilot-test=campaign[:secs] (play the main quest from a new character to the end, one line per boss, then quit).
/// </summary>
public partial class AiPilot
{
    // ───────────────────────── strategy ─────────────────────────

    /// <summary>Level a hero should have before walking into a region's story lair (monsters there are max(region, hero) + 1).</summary>
    internal static int WantedLevel(RegionDef r) => r == null ? 1 : r.Act == 1 ? 6 : r.Level + 2;      // act 1: level 4 was enough for a melee hero, not for a hunter (out of potions in the graveyard crowd)

    private string _restockFor;
    private double _restAt = -1;

    /// <summary>
    /// Called before each travel step: true = the pilot does something else first (gains levels, refills potions).
    /// </summary>
    internal bool BeforeTravel(Game g, string dest)
    {
        var dg = ZoneDb.Dungeon(dest);
        var lv = g.Level; var p = g.Player; var hero = g.Hero;
        var region = ZoneDb.RegionOf(dest);
        if (region == null || lv == null || p == null || hero == null || BossTest) return false;
        bool lair = dg != null && dg.Story;
        // a story lair wants two levels over its region; a new region at least the region's own level (its monsters never
        // drop below it, while above it they match the hero: so the levels that matter are the ones up to the region's)
        int want = lair ? WantedLevel(region) : region.Level;
        if (hero.D.Level < want && !Grinding)
        {
            string where = GrindRegionFor(hero);
            _grindRegion = where;
            _grindLevel = want - 2;                  // Grinding ends at _grindLevel + 2
            _grindUntil = _clock + 1200.0;
            Log.Info("pilot", $"not ready for {dest}: level {hero.D.Level} < {want}; gaining levels in {where} first");
            Log.Guard("hud", () => g.Hud?.Toast($"AI 代打：等級 {hero.D.Level} 尚不足，先到{ZoneDb.AreaName(where)}磨練", Palette.Ember));
            return true;
        }
        if (!lair) return false;
        // never walk into a lair in the middle of a fight (the entrance crowd follows, and the lair has its own)
        if (!lv.IsTown && p.InCombat > 0f) return true;
        // a full belt before the lair: the town refills it
        if (!lv.IsTown && p.Potions <= hero.D.PotionCap - 2 && _restockFor != dest)
        {
            _restockFor = dest;
            Log.Info("pilot", $"potions {p.Potions}/{hero.D.PotionCap} before {dest}: town portal to refill");
            TownPortal(g, p, "refill potions before the boss");
            return true;
        }
        return false;
    }

    /// <summary>The highest open region whose own level is not above the hero's (its monsters then match the hero).</summary>
    private static string GrindRegionFor(Hero hero)
    {
        string best = ZoneDb.Regions[0].Id; int bl = -1;
        foreach (var r in ZoneDb.Regions)
        {
            if (r.Level > hero.D.Level || r.Level < bl || !CanEnter(r.Id)) continue;
            best = r.Id; bl = r.Level;
        }
        return best;
    }

    private readonly HashSet<string> _errands = new();
    private string _errandArea;
    private double _errandAt = -10, _errandSince = -1, _paragonAt = -100;

    /// <summary>
    /// Errands that come before the bot's own plan (true = this think is taken): in town the merchant and the smith; by a
    /// story boss's arena a short rest until the potions and life are back.
    /// </summary>
    internal bool Errand(Game g, Player p, Level lv, Monster target)
    {
        if (lv == null || p == null || p.Dead) return false;
        if (_errandArea != lv.AreaId) { _errandArea = lv.AreaId; _errands.Clear(); _errandSince = _clock; _restAt = -1; }
        var hero = g.Hero;
        if (_clock - _paragonAt > 15.0) { _paragonAt = _clock; SpendParagon(hero); }
        if (lv.IsTown)
        {
            if (_clock - _errandSince > 75.0) return false;      // never let errands hold the journey up for long
            if (!_errands.Contains("merchant") && Visit(p, "merchant", () => Shop(hero))) return true;
            if (!_errands.Contains("smith") && Visit(p, "smith", () => Masterwork(hero))) return true;
            return false;
        }
        // rest by a story boss that has not noticed the hero yet
        var boss = lv.BossMon;
        if (boss == null || !IsInstanceValid(boss) || boss.Dead || boss.Aggro || !lv.BossIsStory || p.InCombat > 0f) { _restAt = -1; return false; }
        if (p.Pos2.DistanceTo(boss.Pos2) > 26f) return false;
        bool short_ = p.Potions < hero.D.PotionCap || p.LifeFrac < 0.95f;
        if (!short_) { _restAt = -1; return false; }
        if (_restAt < 0) { _restAt = _clock; Log.Info("pilot", $"rests before {boss.Def?.Id}: potions {p.Potions}/{hero.D.PotionCap}, life {p.LifeFrac * 100f:0}%"); }
        if (_clock - _restAt > 100.0) return false;
        if (!p.Busy) p.Stop();
        return true;
    }

    /// <summary>Walks to a town NPC; next to them runs <paramref name="deed"/> once.</summary>
    private bool Visit(Player p, string npc, Action deed)
    {
        var it = Nearest(p.Pos2, x => x.Tag as string == npc);
        if (it == null) { _errands.Add(npc); return false; }
        if (it.Pos2.DistanceTo(p.Pos2) > it.UseRange + 1.0f)
        {
            if (_clock - _errandAt > 1.0) { _errandAt = _clock; p.SetMove(it.Pos2, false); }
            return true;
        }
        _errands.Add(npc);
        p.Stop();
        Log.Guard("pilot", deed);
        return true;
    }

    /// <summary>The merchant: buys what would be a clear upgrade, keeping some gold for the smith.</summary>
    private void Shop(Hero hero)
    {
        // junk to gold first: plain and magic pieces that are no upgrade (rares go to the smith as salvage)
        int sold = 0; long before = hero.D.Gold;
        for (int i = hero.D.Bag.Count - 1; i >= 0; i--)
        {
            var it = hero.D.Bag[i];
            if (it == null || !it.IsEquip || hero.IsProtected(it) || it.Rarity > Rarity.Magic || GearEval.IsUpgrade(hero, it)) continue;
            hero.Sell(it); sold++;
        }
        if (sold > 0) Log.Info("pilot", $"merchant: sold {sold} pieces for {hero.D.Gold - before} gold");
        // still crowded (legendaries the smith was told to keep, gems the bot never sockets): what is no upgrade goes
        // to the stash while it has room, the rest is sold — a bag that stays full would send the pilot home every minute
        if (hero.D.Bag.Count >= 20)
        {
            int stashed = 0, sold2 = 0;
            for (int i = hero.D.Bag.Count - 1; i >= 0 && hero.D.Bag.Count >= 16; i--)
            {
                var it = hero.D.Bag[i];
                if (it == null || hero.IsProtected(it) || it.IsEquip && GearEval.IsUpgrade(hero, it)) continue;
                bool keep = !it.IsEquip || it.Rarity >= Rarity.Legendary;
                if (keep && hero.D.Stash.Count < Hero.StashSize && hero.ToStash(it)) { stashed++; continue; }
                if (it.IsEquip && it.Rarity >= Rarity.Legendary) { hero.Salvage(it); sold2++; continue; }
                hero.Sell(it); sold2++;
            }
            Log.Info("pilot", $"merchant: bag {hero.D.Bag.Count}/{Hero.BagSize} after {stashed} to the stash and {sold2} sold/salvaged");
        }
        var stock = Loot.VendorStock("merchant", hero);
        if (stock == null) return;
        int bought = 0;
        for (int pass = 0; pass < 4; pass++)
        {
            (Item item, int price) pick = default; float gain = 0f;
            foreach (var e in stock)
            {
                if (e.item == null || e.price > hero.D.Gold || !GearEval.IsUpgrade(hero, e.item)) continue;
                float g = GearEval.Judge(hero, e.item).Total;
                if (g > gain) { gain = g; pick = e; }
            }
            if (pick.item == null || hero.BagFull) break;
            if (!hero.Buy(pick.item, pick.price)) break;
            stock.Remove(pick);
            var old = hero.Compared(pick.item);
            hero.Equip(pick.item);
            Equipped(pick.item, old);
            bought++;
            Log.Info("pilot", $"merchant: bought {pick.item.Name} ({pick.item.Rarity}, power {pick.item.Power}) for {pick.price}; gold {hero.D.Gold}");
        }
        if (bought == 0) Log.Info("pilot", $"merchant: nothing worth buying (gold {hero.D.Gold})");
    }

    private static readonly EquipSlot[] SmithOrder =
        { EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Chest, EquipSlot.Helm, EquipSlot.Pants, EquipSlot.Gloves, EquipSlot.Boots, EquipSlot.Amulet, EquipSlot.Ring1, EquipSlot.Ring2 };

    /// <summary>The smith: masterworks what is worn, weapon first, with the gold and salvage the hero has.</summary>
    private void Masterwork(Hero hero)
    {
        int ups = 0;
        for (int round = 0; round < 4; round++)
        {
            bool any = false;
            foreach (var s in SmithOrder)
            {
                var it = hero.D.Equip[(int)s];
                if (it == null || it.Rarity < Rarity.Magic || hero.WhyCantUpgrade(it) != null) continue;
                if (hero.Upgrade(it)) { ups++; any = true; }
            }
            if (!any) break;
        }
        Log.Info("pilot", $"smith: {ups} masterwork ranks (gold {hero.D.Gold}, scrap {hero.D.Scrap}, dust {hero.D.Dust}, cores {hero.D.Cores})");
    }

    /// <summary>Paragon points (level 50 on): the best reachable node each time — legendary, rare, magic, then plain.</summary>
    internal static void SpendParagon(Hero hero)
    {
        if (hero == null || hero.ParagonPoints <= 0) return;
        int spent = 0;
        for (int guard = 0; guard < 400 && hero.ParagonPoints > 0; guard++)
        {
            int bb = -1, bn = -1, bs = int.MinValue;
            var boards = hero.ParagonBoards;
            for (int b = 0; b < boards.Count; b++)
            {
                var def = boards[b].Def;
                for (int n = 0; n < PBoard.CellCount; n++)
                {
                    var node = def.Cells[n];
                    if (node == null || !hero.ParagonReachable(b, n) || hero.WhyCantTakeParagon(b, n) != null) continue;
                    int s = node.Type switch { PNodeType.Legendary => 50, PNodeType.Rare => 30, PNodeType.Magic => 12, _ => 5 } + node.Stats.Length;
                    if (s > bs) { bs = s; bb = b; bn = n; }
                }
            }
            if (bb < 0 || !hero.TakeParagon(bb, bn)) break;
            spent++;
        }
        if (spent > 0) Log.Info("pilot", $"paragon: {spent} points spent ({hero.ParagonPoints} left)");
    }

    // ───────────────────────── test harness ─────────────────────────

    private string _testMode;
    private double _testSecs = -1, _testT0 = -1, _testQuit = -1, _testLastLine;
    private bool _testStarted, _testDone;
    private int _testDeaths;
    // the fight in progress (one record per boss engagement until it dies or the hero leaves)
    private string _fightId;
    private double _fightT0;
    private int _fightDeaths, _fThreats, _fDodged, _fTaken;
    private double _fDmgTele, _fDmgOther;
    private readonly List<string> _fightLines = new();

    internal bool BossTest => _testMode == "boss";
    internal bool InTest => _testMode != null;

    private void ReadTestSwitch()
    {
        string v = Game.I?.ArgValue("pilot-test");
        if (string.IsNullOrEmpty(v)) return;
        var parts = v.Split(':');
        _testMode = parts[0].Trim().ToLowerInvariant();
        if (parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double s)) _testSecs = s;
        if (_testMode != "boss" && _testMode != "campaign") { Log.Warn("pilot", $"--pilot-test: unknown mode '{v}'"); _testMode = null; return; }
        if (_testSecs <= 0) _testSecs = _testMode == "boss" ? 900 : 14400;
        if (_pilotAt < 0) _pilotAt = 2;
        Log.Info("pilot", $"--pilot-test={_testMode}: limit {_testSecs:0} s of game time");
    }

    /// <summary>A death in a test: counted. In the boss test the pilot keeps trying (no grinding, no hand-back) until the limit.</summary>
    internal bool TestDeath()
    {
        if (!InTest) return false;
        _testDeaths++;
        if (_fightId != null) _fightDeaths++;
        Log.Info("pilot", $"TEST death {_testDeaths} at t={Game.I?.Time - _testT0:0}s in {Game.I?.Level?.AreaId} (fight={_fightId ?? "-"}) life bar {Game.I?.Hero?.MaxLife:0}; last hits: {DangerMap.DeathReport(10)}");
        return BossTest;
    }

    private void TestOnEvent(string evt, string arg)
    {
        if (!InTest) return;
        if (evt == "boss") EndFight(arg, true);
        else if (evt == "levelup") { }
    }

    private void TestTick(Game g)
    {
        if (!InTest || _testDone) return;
        if (_bot != null && !_testStarted) { _testStarted = true; _testT0 = g.Time; DangerMap.Reset(); DangerMap.LogHits = true; }
        if (!_testStarted) return;
        double t = g.Time - _testT0;
        // the fight record
        if (_bot != null && g.Player != null && IsInstanceValid(g.Player) && !g.Player.Dead)
        {
            if (_fightId == null && DangerMap.BossFight())
            {
                var b = FightingBoss(g.Player.Pos2);
                if (b != null) BeginFight(b.Def?.Id ?? "?");
            }
        }
        if (g.Time - _testLastLine > 60.0)
        {
            _testLastLine = g.Time;
            var h = g.Hero; var p = g.Player;
            Log.Info("pilot", $"TEST progress t={t:0}s area={g.Level?.AreaId} level={h?.D.Level} life={(p != null && IsInstanceValid(p) ? p.LifeFrac * 100f : 0):0}% potions={p?.Potions}/{h?.D.PotionCap} deaths={_testDeaths} " +
                $"quest={SafeQuestArea()} {DangerMap.Stats()} {_bot?.ReflexStats() ?? ""}");
        }
        bool over = _bot == null || t > _testSecs || (BossTest && _testBossKilled);
        if (!over) return;
        if (_testQuit < 0)
        {
            _testQuit = g.Time + 3.0;
            if (_fightId != null) EndFight(null, false);
            var h = g.Hero;
            string why = _bot == null ? "pilot stopped" : t > _testSecs ? "time limit" : "boss killed";
            Log.Info("pilot", $"TEST RESULT mode={_testMode} class={Names.ClassKey(h.Class)} end={why} t={t:0}s level={h.D.Level} deaths={_testDeaths} area={g.Level?.AreaId} " +
                $"campaignDone={SafeCampaignDone()} bossesKilled={_testBosses} {DangerMap.Stats()} {_bot?.ReflexStats() ?? _lastReflex}");
            foreach (var l in _fightLines) Log.Info("pilot", "TEST FIGHT " + l);
        }
        if (g.Time >= _testQuit) { _testDone = true; Log.Info("pilot", "TEST over: quitting"); g.Quit(); }
    }

    private bool _testBossKilled;
    private int _testBosses;
    private string _lastReflex = "";

    private static bool SafeCampaignDone()
    {
        try { return Quests.CampaignDone; } catch (Exception) { return false; }
    }

    private static Monster FightingBoss(Vector2 me)
    {
        Monster best = null; float bd = float.MaxValue;
        foreach (var m in World.Monsters)
        {
            if (m == null || m.Dead || m.Brain == null || !m.Aggro || !IsInstanceValid(m)) continue;
            float d = m.Pos2.DistanceSquaredTo(me);
            if (d < bd) { bd = d; best = m; }
        }
        return best;
    }

    private void BeginFight(string id)
    {
        _fightId = id; _fightT0 = Game.I.Time; _fightDeaths = 0;
        _fThreats = DangerMap.Threats; _fDodged = DangerMap.Dodged; _fTaken = DangerMap.Taken;
        _fDmgTele = DangerMap.TakenDmg; _fDmgOther = DangerMap.OtherDmg;
        var h = Game.I.Hero; var p = Game.I.Player;
        Log.Info("pilot", $"TEST fight begins: {id} in {Game.I.Level?.AreaId} hero lv{h?.D.Level} life {p?.MaxLife:0} potions {p?.Potions}/{h?.D.PotionCap}");
    }

    private void EndFight(string killedId, bool killed)
    {
        if (killed) { _testBosses++; if (BossTest) _testBossKilled = true; }
        if (_fightId == null && !killed) return;
        string id = _fightId ?? killedId;
        var g = Game.I;
        string line = $"boss={id} area={g?.Level?.AreaId} killed={killed} secs={(g?.Time ?? 0) - _fightT0:0} deaths={_fightDeaths} heroLevel={g?.Hero?.D.Level} " +
            $"threats={DangerMap.Threats - _fThreats} dodged={DangerMap.Dodged - _fDodged} taken={DangerMap.Taken - _fTaken} " +
            $"dmgTelegraphed={DangerMap.TakenDmg - _fDmgTele:0} dmgOther={DangerMap.OtherDmg - _fDmgOther:0} life={g?.Hero?.MaxLife:0}";
        _fightLines.Add(line);
        Log.Info("pilot", "TEST fight ends: " + line);
        _fightId = null;
    }

    /// <summary>Area change in a test: a fight left unfinished is closed.</summary>
    private void TestOnArea()
    {
        if (InTest && _fightId != null) EndFight(null, false);
    }
}

public partial class AiPilot
{
    /// <summary>
    /// The order the pilot takes the open story regions in when it may choose (act 2 opens three at once): the
    /// frost pass last — its packs chill and freeze, and a hero meets them better with more skill ranks (the defensive
    /// skills' crowd-control immunity), more potions and better gear.
    /// </summary>
    internal static int RegionRank(string region) => region switch
    {
        "wastes" => 0, "crypt" => 1, "mire" => 2, "frost" => 3, "sands" => 4, "sanctum" => 5, "abyss" => 6, _ => 9,
    };

    /// <summary>
    /// Skill points: when a defensive skill on the bar may take a specialisation that makes the hero immune to crowd
    /// control while it lasts (戰吼「無可阻擋」 and the like), the pilot takes that one — freezes and stuns are what kill
    /// a bot in a pack. Runs after Game.AutoBuild, with the points it left.
    /// </summary>
    internal static void PreferUnstoppable(Hero hero)
    {
        foreach (var id in hero.D.Bar)
        {
            var s = id != null ? SkillDb.Get(id) : null;
            if (s == null || s.Cat != SkillCat.Defensive || s.Enh == null || hero.BaseRank(id) <= 0) continue;
            foreach (var u in new[] { s.A, s.B })
            {
                if (u == null || hero.HasUp(u.Id)) continue;
                var probe = s.Clone();
                try { u.Mod?.Invoke(probe); } catch (Exception) { continue; }
                if (!probe.Buffs.Any(b => b.Unstoppable || b.Immune) || s.Buffs.Any(b => b.Unstoppable || b.Immune)) continue;
                if (!hero.HasUp(s.Enh.Id) && !hero.TakeUp(id, s.Enh.Id)) continue;
                if (hero.TakeUp(id, u.Id)) Log.Info("pilot", $"skills: {id} takes '{u.Name}' (immune to crowd control while it lasts)");
            }
        }
    }
}

public partial class AiPilot
{
    /// <summary>
    /// Two deaths without progress: grinding where it just died (a stronghold's crowd, a frost pass) only sends it back
    /// into the same fight. It levels up in the highest other open region whose level is not above the hero's instead,
    /// three levels or ten minutes, then comes back stronger (skill ranks, passives and gear grow faster than the
    /// monsters, which scale with the hero).
    /// </summary>
    private void ChooseRegroup(Game g)
    {
        var hero = g?.Hero;
        if (hero == null || BossTest) return;
        string here = _grindRegion;
        string best = null; int bl = -1;
        foreach (var r in ZoneDb.Regions)
        {
            if (r.Id == here || r.Level > hero.D.Level || r.Level < bl || !CanEnter(r.Id)) continue;
            best = r.Id; bl = r.Level;
        }
        if (best == null) return;
        _grindRegion = best;
        _grindUntil = _clock + 600.0;
        _grindLevel = hero.D.Level + 1;              // Grinding ends at _grindLevel + 2: three levels up
        Log.Info("pilot", $"two deaths in {here}: regroups in {best} until level {_grindLevel + 2} (or 10 minutes)");
    }
}

public partial class AiPilot
{
    /// <summary>
    /// Strongholds that measured as walls below a hero level (kept small, per region). 凍骨關 (瓦雷克): his banner musters
    /// frostbound every 15 s and their blows chill to a freeze; heroes arriving at level 17–19 died there 4–11 times,
    /// heroes at 22+ cleared it with 0–1 deaths.
    /// </summary>
    private static readonly Dictionary<string, int> StrongholdLevel = new() { ["frost"] = 22 };

    /// <summary>False (and a grind elsewhere is set up) while the hero is below the stronghold's measured level.</summary>
    internal bool StrongholdReady(Level lv)
    {
        var hero = Game.I?.Hero;
        if (hero == null || lv?.Region == null || BossTest) return true;
        int want = Math.Max(lv.Region.Level + 2, StrongholdLevel.GetValueOrDefault(lv.Region.Id));      // a level-1 hunter charging the outpost met 39 monsters
        if (hero.D.Level >= want) return true;
        if (Grinding) return false;
        string best = null; int bl = -1;
        foreach (var r in ZoneDb.Regions)
        {
            if (r.Id == lv.Region.Id || r.Level > hero.D.Level || r.Level < bl || !CanEnter(r.Id)) continue;
            best = r.Id; bl = r.Level;
        }
        _grindRegion = best ?? lv.Region.Id;
        _grindLevel = want - 2;
        _grindUntil = _clock + 1500.0;
        Log.Info("pilot", $"stronghold of {lv.Region.Id} wants level {want} (hero {hero.D.Level}): levels up in {_grindRegion} first");
        return false;
    }
}

public partial class AiPilot
{
    private readonly Dictionary<string, int> _deathsByRegion = new();

    /// <summary>
    /// Deaths per region, never reset by progress (levels come while regrouping, so the "without progress" count never
    /// reaches the hand-back there). Eight deaths in one region: the pilot has no answer to that place — it hands back
    /// with that reason instead of trading lives forever.
    /// </summary>
    private bool WallReached()
    {
        var lv = Game.I?.Level;
        string region = lv != null && IsInstanceValid(lv) ? (lv.Region ?? ZoneDb.RegionOf(lv.AreaId))?.Id : null;
        if (region == null) return false;
        int n = _deathsByRegion.GetValueOrDefault(region) + 1;
        _deathsByRegion[region] = n;
        if (n < 8) return false;
        Stop("wall: " + region, $"在「{ZoneDb.AreaName(region)}」接連倒下八次，AI 代打找不到對策，請親自挑戰。");
        return true;
    }
}
