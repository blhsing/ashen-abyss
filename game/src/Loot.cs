using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace AshenAbyss;

/// <summary>
/// Everything the world gives back: experience and kill bookkeeping when a monster falls, the drop tables,
/// loot lying on the ground (items, gold piles, health globes), chests, vendor stock and gambling.
///
/// Ground loot is a plain <see cref="Interactable"/> with a small procedural mesh. Its motion (the pop-out arc,
/// gold flying to the hero, the bobbing globe, the floating gold number) is driven by one static ticker hooked to
/// SceneTree.ProcessFrame, so no extra node type is needed and nothing moves while the game is paused.
/// </summary>
public static class Loot
{
    private const string Cat = "loot";

    // ───────────────────────── tuning ─────────────────────────

    private const float PopStagger = 0.07f;        // delay between the drops of one fountain
    private const float VisScale = 1.25f;          // ground meshes are a little larger than life so they read from the camera
    private const float ItemSpacing = 1.15f;       // minimum distance between two item labels
    private const float SmallSpacing = 0.8f;       // gold piles and globes
    private const float GoldMagnet = 3.2f;         // gold flies to the hero from this far
    private const float GlobeTouch = 1.7f;
    private const float GlobeHeight = 0.55f;

    private static readonly string[] DropSfx = { "drop_common", "drop_magic", "drop_rare", "drop_legendary", "drop_unique" };
    private static readonly float[] DiscSize = { 0.9f, 1.1f, 1.35f, 1.7f, 1.7f };
    private static readonly float[] DiscGlow = { 0.10f, 0.32f, 0.5f, 0.8f, 0.8f };

    /// <summary>Relative weights of the five rarities before world tier and magic find are applied.</summary>
    private sealed class Odds
    {
        public readonly float C, M, R, L, U;
        public Odds(float c, float m, float r, float l, float u) { C = c; M = m; R = r; L = l; U = u; }
    }

    private static readonly Odds OddsNormal = new(50f, 34f, 14f, 1.6f, 0.25f);
    private static readonly Odds OddsChampion = new(22f, 44f, 28f, 5f, 0.6f);
    private static readonly Odds OddsElite = new(0f, 34f, 51f, 13f, 1.6f);
    private static readonly Odds OddsMini = new(0f, 18f, 56f, 23f, 3f);
    private static readonly Odds OddsBoss = new(0f, 6f, 58f, 31f, 5f);
    private static readonly Odds OddsChest = new(32f, 44f, 21f, 2.6f, 0.35f);
    private static readonly Odds OddsGamble = new(0f, 53f, 40f, 6f, 0.8f);
    private static readonly Odds OddsVendor = new(8f, 50f, 42f, 0f, 0f);

    // world tier 0..3
    private static readonly float[] TierCommonMul = { 1f, 0.8f, 0.6f, 0.45f };
    private static readonly float[] TierRareMul = { 1f, 1.15f, 1.3f, 1.45f };
    private static readonly float[] TierLegendMul = { 1f, 1.3f, 1.8f, 2.4f };
    private static readonly float[] TierUniqueMul = { 1f, 1.6f, 3.2f, 5.5f };

    private static readonly Comparison<Item> ByRarity = (a, b) => ((int)a.Rarity).CompareTo((int)b.Rarity);
    private static readonly Comparison<(Item item, int price)> GemOrder = (a, b) => string.CompareOrdinal(a.item.GemId, b.item.GemId);
    private static readonly ItemKind[] ArmorKinds = { ItemKind.Helm, ItemKind.Chest, ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet, ItemKind.Ring };

    static Loot()
    {
        World.Event += OnWorldEvent;
    }

    private static void OnWorldEvent(string evt, string arg)
    {
        if (evt != "area") return;
        if (Stock.Count > 0 && Log.Verbose) Log.Debug(Cat, $"vendor stock reset on entering '{arg}'");
        Stock.Clear();
        _goldText = null;
    }

    private static int Tier()
    {
        var g = Game.I;
        return g == null ? 0 : Math.Clamp(g.WorldTier, 0, Balance.TierXp.Length - 1);
    }

    /// <summary>Level used for chests and other drops that have no monster: the area's monster level, else the hero's.</summary>
    private static int AreaLevel(Hero hero)
    {
        int lv = Game.I?.Level?.MonsterLevel ?? 0;
        return lv > 0 ? lv : Math.Max(1, hero.D.Level);
    }

    // ═════════════════════════ monster deaths ═════════════════════════

    /// <summary>Called by Monster.Die for hostile monsters: experience, counters, world events, then the drop roll.</summary>
    public static void OnMonsterDeath(Monster m)
    {
        if (m == null || m.Def == null || m.Faction != Faction.Monster) return;
        var game = Game.I;
        var hero = game?.Hero;
        if (hero == null) return;
        var d = hero.D;
        int wt = Tier();

        bool boss = m.Brain != null || m.Tier == MonTier.Boss;
        bool mini = !boss && m.MiniBoss;
        bool elite = !boss && !mini && m.Tier == MonTier.Elite;
        bool champ = !boss && !mini && m.Tier == MonTier.Champion;

        float xp = m.XpValue * Balance.TierXp[wt] * LevelFactor(d.Level, m.Level);
        if (xp > 0f) hero.AddXp(xp);

        d.Kills++;
        if (boss) d.Bosses++;
        else if (mini || elite || champ) d.Elites++;

        string id = m.Def.Id;
        World.Emit("kill", id);
        if (boss) World.Emit("boss", id);
        else if (mini) World.Emit("miniboss", game.Level?.AreaId ?? d.Area ?? "");
        else if (elite || champ) World.Emit("elite", id);

        if (boss || mini) Log.Info(Cat, $"{(boss ? "boss" : "mini-boss")} slain: {id} lv{m.Level} tier={wt} xp={xp:F0} noLoot={m.NoLoot}");
        else if (Log.Verbose) Log.Debug(Cat, $"kill {id} lv{m.Level} {m.Tier} xp={xp:F0} noLoot={m.NoLoot}");

        if (m.NoLoot) return;
        // a marked objective target is worth at least a champion's spoils
        RollDrops(m, hero, wt, boss, mini, elite, champ || m.Objective);
    }

    /// <summary>
    /// Full experience near the hero's level, far less for prey well below, and a catch-up bonus for stronger prey: a hero who
    /// follows the story alone reached the last region six levels under its monsters (6.8 s per normal monster) with +4 % a level.
    /// </summary>
    private static float LevelFactor(int heroLevel, int monsterLevel)
    {
        int diff = heroLevel - monsterLevel;
        if (diff <= 3) return diff >= 0 ? 1f : 1f + MathF.Min(0.8f, -diff * 0.1f);
        return MathF.Max(0.1f, 1f - (diff - 3) * 0.13f);
    }

    private static float GoldUnit(int level, Hero hero, int wt) =>
        (6f + level * 2.4f) * (1f + Mathf.Clamp(hero.S[Stat.GoldFind], 0f, 500f) / 100f) * Balance.TierGold[wt];

    /// <summary>Health globes fall more often when the hero is short of potions or badly hurt.</summary>
    private static float GlobeNeed(Hero hero)
    {
        var p = World.P;
        if (p == null || !GodotObject.IsInstanceValid(p)) return 1f;
        int cap = Math.Max(1, hero.D.PotionCap);
        float frac = p.Potions / (float)cap;
        float need = frac <= 0f ? 3f : frac <= 0.5f ? 1.8f : frac < 1f ? 1.2f : 0.8f;
        if (p.LifeFrac < 0.35f) need *= 1.5f;
        return need;
    }

    private static void RollDrops(Monster m, Hero hero, int wt, bool boss, bool mini, bool elite, bool champ)
    {
        Vector2 at = m.Pos2;
        float h0 = Mathf.Clamp(m.HeightM * 0.5f, 0.5f, 1.5f);
        int level = Math.Max(1, m.Level);
        float need = GlobeNeed(hero);

        Odds odds;
        Rarity floor = Rarity.Common;        // minimum rarity of the first item of the batch
        bool strong = false;                 // elites and above roll slightly higher item power
        int items, piles = 1, globes = 0, gems = 0, scrap = 0, dust = 0, cores = 0;
        float goldMul = 0f;

        if (boss)
        {
            odds = OddsBoss; strong = true;
            items = Rng.Range(5, 7);
            goldMul = 40f; piles = 5; globes = 2;
            gems = 1 + (Rng.Chance(0.5f) ? 1 : 0);
            scrap = Rng.Range(4, 7); dust = Rng.Range(2, 4); cores = 1 + (wt >= 2 ? 1 : 0);
            if (hero.D.Set("loot_first_" + m.Def.Id))
            {
                floor = Rarity.Legendary;
                Log.Info(Cat, $"first kill of {m.Def.Id} on this character: a legendary is guaranteed");
            }
        }
        else if (mini)
        {
            odds = OddsMini; strong = true;
            items = Rng.Range(3, 4);
            goldMul = 14f; piles = 3; globes = 1;
            if (Rng.Chance(0.35f)) gems = 1;
            scrap = Rng.Range(2, 4); dust = Rng.Range(1, 2); cores = Rng.Chance(0.2f) ? 1 : 0;
        }
        else if (elite)
        {
            odds = OddsElite; strong = true;
            items = Rng.Range(2, 3) + (wt >= 2 && Rng.Chance(0.3f) ? 1 : 0);
            goldMul = 6f; piles = 2;
            if (Rng.Chance(MathF.Min(0.9f, 0.4f * need))) globes = 1;
            if (Rng.Chance(0.1f)) gems = 1;
            if (Rng.Chance(0.6f)) { scrap = Rng.Range(1, 3); dust = Rng.Chance(0.35f) ? 1 : 0; }
        }
        else if (champ)
        {
            odds = OddsChampion;
            items = (Rng.Chance(0.6f + 0.04f * wt) ? 1 : 0) + (Rng.Chance(0.08f) ? 1 : 0);
            if (Rng.Chance(0.6f)) goldMul = 2.5f;
            if (Rng.Chance(MathF.Min(0.6f, 0.12f * need))) globes = 1;
            if (Rng.Chance(0.03f)) gems = 1;
            if (Rng.Chance(0.2f)) { scrap = Rng.Range(1, 2); dust = Rng.Chance(0.1f) ? 1 : 0; }
        }
        else
        {
            odds = OddsNormal;
            items = Rng.Chance(0.07f + 0.01f * wt) ? 1 : 0;
            if (Rng.Chance(0.22f)) goldMul = 1f;
            if (Rng.Chance(MathF.Min(0.3f, 0.03f * need))) globes = 1;
            if (Rng.Chance(0.006f)) gems = 1;
            if (Rng.Chance(0.03f)) scrap = 1;
        }
        if (level < 6) gems = 0;             // nothing to socket them into yet

        int seq = 0;
        if (goldMul > 0f) GoldPiles(GoldUnit(level, hero, wt) * goldMul, piles, at, h0, ref seq);

        // The weapon is most of a hero's damage (a level-4 weapon hits 2.4× as hard as the starter). A hero who still swings one
        // many levels old gets a new one from the next named kill, and often from an elite: a hunter who had found none in four
        // levels did 17 damage a second and could not finish the first boss.
        ItemKind? firstKind = null;
        var held = hero.D.Equip[(int)EquipSlot.Weapon];
        int heroLevel = Math.Max(level, hero.D.Level);       // monsters of an area keep the level the hero had on arrival
        if ((held == null || held.Power < heroLevel * 6) && (boss || mini || (elite && Rng.Chance(0.5f))))
        {
            firstKind = ItemKind.Weapon;
            if (floor < Rarity.Magic) floor = Rarity.Magic;
            if (items < 1) items = 1;
            Log.Info(Cat, $"weapon pity: the hero's weapon has power {held?.Power ?? 0} at level {heroLevel}, {m.Def.Id} drops one");
        }

        if (items == 1)
        {
            SpawnItem(Make(firstKind != null ? heroLevel : level, odds, hero.Class, firstKind, floor, strong, hero), at, h0, seq++ * PopStagger);
        }
        else if (items > 1)
        {
            var batch = new List<Item>(items);
            for (int i = 0; i < items; i++) batch.Add(Make(i == 0 && firstKind != null ? heroLevel : level, odds, hero.Class, i == 0 ? firstKind : null, i == 0 ? floor : Rarity.Common, strong, hero));
            batch.Sort(ByRarity);            // the best piece lands last
            foreach (var it in batch) SpawnItem(it, at, h0, seq++ * PopStagger);
        }
        for (int i = 0; i < gems; i++) SpawnItem(ItemDb.RandomGem(level), at, h0, seq++ * PopStagger);
        for (int i = 0; i < globes; i++) SpawnGlobe(at, h0, seq++ * PopStagger);
        if (scrap + dust + cores > 0) DropMaterials(scrap, dust, cores, at);
    }

    private static void GoldPiles(float total, int piles, Vector2 at, float h0, ref int seq)
    {
        int amount = Math.Max(1, (int)MathF.Round(total * Rng.Range(0.75f, 1.25f)));
        piles = Math.Clamp(piles, 1, amount);
        int left = amount;
        for (int i = 0; i < piles; i++)
        {
            int rest = piles - 1 - i;        // piles still to come after this one; each needs at least one coin
            int share = rest == 0 ? left : Math.Max(1, (int)(amount / (float)piles * Rng.Range(0.7f, 1.3f)));
            share = Math.Min(share, left - rest);
            left -= share;
            SpawnGold(share, at, h0, seq++ * PopStagger);
        }
    }

    // ═════════════════════════ rolling items ═════════════════════════

    /// <summary>
    /// The shared roller. <paramref name="quality"/>: 0 ordinary monster or plain chest, 1 champion, 2 elite, 3 and above boss —
    /// higher quality shifts the rarity odds upward. World tier and the hero's magic find are applied on top.
    /// </summary>
    public static Item Roll(int level, int quality, ClassId cls, ItemKind? kind = null)
    {
        var odds = quality <= 0 ? OddsNormal : quality == 1 ? OddsChampion : quality == 2 ? OddsElite : OddsBoss;
        var it = Make(level, odds, cls, kind, Rarity.Common, quality >= 2, null);
        if (Log.Verbose) Log.Debug(Cat, $"roll lv{level} q{quality} {cls} → {it.Rarity} {it.Name} [{it.Kind} power {it.Power}]");
        return it;
    }

    private static Item Make(int level, Odds odds, ClassId cls, ItemKind? kind, Rarity floor, bool strong, Hero hero)
    {
        level = Math.Max(1, level);
        if (kind == ItemKind.Gem) return ItemDb.RandomGem(level);
        hero ??= Game.I?.Hero;
        var rarity = RollRarity(odds, level, floor, hero);
        var it = ItemDb.Generate(PowerFor(level, strong), rarity, cls, kind);
        // an item the world hands to this hero is never beyond the hero's reach
        if (hero != null && it.IsEquip && it.ReqLevel > hero.D.Level) it.ReqLevel = Math.Max(1, hero.D.Level);
        return it;
    }

    /// <summary>About ten power per level, thirty per world tier and ten more from elites and bosses.</summary>
    private static int PowerFor(int level, bool strong) =>
        Math.Clamp(level * 10 + Tier() * 30 + (strong ? 10 : 0) + Rng.Range(-6, 5), 5, ItemDb.MaxPower);

    private static Rarity RollRarity(Odds o, int level, Rarity floor, Hero hero)
    {
        int wt = Tier();
        float mf = hero == null ? 0f : Mathf.Clamp(hero.S[Stat.MagicFind], 0f, 300f) / 100f;
        float c = o.C * TierCommonMul[wt];
        float m = o.M;
        float r = o.R * TierRareMul[wt] * (1f + mf * 0.5f);
        float l = o.L * TierLegendMul[wt] * (1f + mf);
        float u = o.U * TierUniqueMul[wt] * (1f + mf);
        if (level < 6) { l *= 0.35f; u = 0f; }       // the first hours stay modest
        else if (level < 14) u *= 0.4f;

        float total = c + m + r + l + u;
        Rarity res = Rarity.Magic;
        if (total > 0f)
        {
            float x = Rng.F() * total;
            res = x < u ? Rarity.Unique : x < u + l ? Rarity.Legendary : x < u + l + r ? Rarity.Rare : x < u + l + r + m ? Rarity.Magic : Rarity.Common;
        }
        return res < floor ? floor : res;
    }

    // ═════════════════════════ ground loot ═════════════════════════

    /// <summary>Drops an item on the ground near <paramref name="at"/>: it pops out in an arc, then waits to be picked up.</summary>
    public static Interactable DropItem(Item it, Vector2 at) => SpawnItem(it, at, 0.7f, 0f);

    /// <summary>A pile of exactly <paramref name="amount"/> gold that flies to the hero when they come close.</summary>
    public static void DropGold(int amount, Vector2 at) => SpawnGold(amount, at, 0.6f, 0f);

    /// <summary>A health globe: 15% of maximum life and one potion charge when the hero walks into it.</summary>
    public static void DropGlobe(Vector2 at) => SpawnGlobe(at, 0.7f, 0f);

    /// <summary>Crafting materials go straight to the hero's stores, with a toast.</summary>
    public static void DropMaterials(int scrap, int dust, int cores, Vector2 at)
    {
        scrap = Math.Max(0, scrap); dust = Math.Max(0, dust); cores = Math.Max(0, cores);
        if (scrap + dust + cores <= 0) return;
        var game = Game.I;
        var hero = game?.Hero;
        if (hero == null) return;
        hero.D.Scrap += scrap; hero.D.Dust += dust; hero.D.Cores += cores;
        hero.Notify();

        var sb = new StringBuilder("獲得材料");
        if (scrap > 0) sb.Append("　碎鐵 ×").Append(scrap);
        if (dust > 0) sb.Append("　幽塵 ×").Append(dust);
        if (cores > 0) sb.Append("　燼核 ×").Append(cores);
        Color c = cores > 0 ? Palette.Ember : dust > 0 ? new Color(0.72f, 0.74f, 0.95f) : Palette.Text;
        game.Hud?.Toast(sb.ToString(), c);
        try
        {
            Vector3 pos = World.To3(at, 0.15f);
            Fx.Ring(pos, 0.8f, c, 0.45f);
            Snd.At("item_pickup", pos, 0.45f);
        }
        catch (Exception e) { Log.Error(Cat, "material pickup effects failed", e); }
        if (Log.Verbose) Log.Debug(Cat, $"materials +{scrap} scrap +{dust} dust +{cores} cores → {hero.D.Scrap}/{hero.D.Dust}/{hero.D.Cores}");
    }

    private static bool Always(Item it) =>
        it.Rarity >= Rarity.Rare || it.Kind == ItemKind.Gem || (Game.I?.Cfg?.ShowAllLoot ?? false);

    private static Node3D Root()
    {
        var r = World.Root;
        return r != null && GodotObject.IsInstanceValid(r) && r.IsInsideTree() ? r : null;
    }

    private static Interactable SpawnItem(Item it, Vector2 from, float fromHeight, float delay)
    {
        if (it == null) return null;
        if (it.Rarity >= Rarity.Legendary) Log.Info(Cat, $"{it.Rarity} drop: {it.Name} [{it.Kind} power {it.Power}] at ({from.X:F1}, {from.Y:F1})");
        else if (Log.Verbose) Log.Debug(Cat, $"drop {it.Rarity} {it.Name} [{it.Kind} power {it.Power}]");

        var root = Root();
        if (root == null)
        {
            // no level to drop into (never during normal play): hand the item over rather than lose it
            var hero = Game.I?.Hero;
            bool kept = hero != null && hero.AddItem(it);
            Log.Warn(Cat, $"no world root for ground loot; {it.Name} {(kept ? "went straight to the bag" : "was lost")}");
            return null;
        }

        var node = new Interactable
        {
            Kind = "loot", Item = it, LootRarity = it.Rarity,
            Label = it.Name, LabelColor = Palette.Rarity(it.Rarity),
            AlwaysLabel = Always(it), ShowLabel = false, CanUse = false,
            LabelHeight = 0.7f, UseRange = 2.4f,
        };
        node.OnUse = p => Pickup(node, it, p);

        bool jewel = it.Kind == ItemKind.Ring || it.Kind == ItemKind.Amulet;
        float scale = jewel ? 1.55f : it.Kind == ItemKind.Gem ? 1.2f : VisScale;
        var vis = new MeshInstance3D { Mesh = MeshFor(MeshKey(it)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = Vector3.One * scale };
        if (it.Kind == ItemKind.Gem) { var gm = GemMaterial(it); if (gm != null) vis.MaterialOverride = gm; }
        node.AddChild(vis);
        root.AddChild(node);

        Vector2 spot = FindSpot(from, ItemSpacing);
        Spots.Add(new Spot { Pos = spot, Node = node });
        float yaw = Rng.Range(0f, Mathf.Tau);
        if (EnsureTicker())
        {
            Vector3 start = World.To3(from, fromHeight);
            node.GlobalPosition = start;
            if (delay > 0f) node.Visible = false;
            Anims.Add(new Anim
            {
                Mode = ModePop, Node = node, Vis = vis, From = start, To = World.To3(spot),
                Dur = Rng.Range(0.46f, 0.58f), Delay = delay, Height = Rng.Range(1.2f, 1.7f),
                Spin = Rng.Range(-9f, 9f), Tumble = Rng.Range(4f, 8f), Yaw = yaw, Scale = scale,
                Done = () => LandItem(node, it),
            });
        }
        else
        {
            node.GlobalPosition = World.To3(spot);
            vis.Rotation = new Vector3(0f, yaw, 0f);
            LandItem(node, it);
        }
        return node;
    }

    private static void LandItem(Interactable node, Item it)
    {
        if (!GodotObject.IsInstanceValid(node)) return;
        node.CanUse = true;
        node.ShowLabel = true;
        node.AlwaysLabel = Always(it);
        try
        {
            int r = Math.Clamp((int)it.Rarity, 0, 4);
            Vector3 pos = node.GlobalPosition;
            if (EnsureArt())
            {
                AddDisc(node, DiscMats[r], DiscSize[r]);
                if (it.Kind is ItemKind.Ring or ItemKind.Amulet or ItemKind.Gem) AddGlint(node, 0.24f);
            }
            if (it.Rarity >= Rarity.Rare)
            {
                var beam = Fx.LootBeam(it.Rarity);
                if (beam != null && GodotObject.IsInstanceValid(beam))
                {
                    var parent = beam.GetParent();
                    if (parent == null) node.AddChild(beam);
                    else if (parent != node) { beam.Reparent(node, false); beam.Position = Vector3.Zero; }
                }
            }
            if (it.Rarity >= Rarity.Legendary) Fx.Ring(pos + Vector3.Up * 0.1f, 1.8f, Palette.Rarity(it.Rarity), 0.7f);
            Snd.At(DropSfx[r], pos, it.Rarity >= Rarity.Rare ? 1f : 0.55f);
        }
        catch (Exception e) { Log.Error(Cat, "loot landing effects failed", e); }
    }

    private static void Pickup(Interactable node, Item it, Player p)
    {
        var hero = p?.Hero ?? Game.I?.Hero;
        if (hero == null || !GodotObject.IsInstanceValid(node)) return;
        if (!hero.AddItem(it))
        {
            Game.I?.Hud?.Warn("背包已滿");
            Snd.Ui("ui_error", 0.8f);
            if (Log.Verbose) Log.Debug(Cat, $"bag full, {it.Name} stays on the ground");
            return;
        }
        Snd.At("item_pickup", node.GlobalPosition);
        if (it.Rarity >= Rarity.Legendary)
        {
            Log.Info(Cat, $"picked up {it.Rarity}: {it.Name} [{it.Kind} power {it.Power}] bag={hero.D.Bag.Count}/{Hero.BagSize}");
            Game.I?.Hud?.Toast($"拾獲{Names.Rarity(it.Rarity)}物品　{it.Name}", Palette.Rarity(it.Rarity));
        }
        else if (Log.Verbose) Log.Debug(Cat, $"picked up {it.Rarity} {it.Name}");
        node.Consume();
    }

    private static void SpawnGold(int amount, Vector2 from, float fromHeight, float delay)
    {
        if (amount <= 0) return;
        var hero = Game.I?.Hero;
        var root = Root();
        if (root == null)
        {
            if (hero != null) hero.D.Gold += amount;
            Log.Warn(Cat, $"no world root for a gold pile; {amount} gold credited directly");
            return;
        }

        float unit = 6f + Math.Max(1, hero?.D.Level ?? 1) * 2.4f;
        int size = amount < unit * 2.5f ? 0 : amount < unit * 9f ? 1 : 2;
        var node = new Interactable
        {
            Kind = "gold", Label = amount + " 金幣", LabelColor = Palette.Gold,
            ShowLabel = false, CanUse = false, AutoRadius = GoldMagnet, LabelHeight = 0.5f, Tag = amount,
        };
        var vis = new MeshInstance3D { Mesh = MeshFor(KeyGold + size), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = Vector3.One * VisScale };
        node.AddChild(vis);
        root.AddChild(node);

        MeshInstance3D disc = null;
        node.OnUse = p =>
        {
            node.CanUse = false;
            var h = p?.Hero ?? Game.I?.Hero;
            if (h != null) h.D.Gold += amount;
            if (Log.Verbose) Log.Debug(Cat, $"gold +{amount} → {h?.D.Gold}");
            if (disc != null && GodotObject.IsInstanceValid(disc)) disc.Visible = false;
            Vector3 start = node.GlobalPosition;
            if (!EnsureTicker())
            {
                Snd.At("gold_pickup", start, 0.7f);
                node.QueueFree();
                return;
            }
            Anims.Add(new Anim
            {
                Mode = ModeFly, Node = node, Vis = vis, From = start, To = start, Dur = 0.26f, Height = 0.45f,
                Scale = VisScale, Yaw = vis.Rotation.Y,
                Done = () =>
                {
                    Vector3 end = node.GlobalPosition;
                    Snd.At("gold_pickup", end, 0.7f);
                    FloatGold(amount, end + Vector3.Up * 0.9f);
                    node.QueueFree();
                },
            });
        };

        Vector2 spot = FindSpot(from, SmallSpacing);
        Spots.Add(new Spot { Pos = spot, Node = node });
        float yaw = Rng.Range(0f, Mathf.Tau);
        Action land = () =>
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            node.CanUse = true;
            try
            {
                if (EnsureArt()) disc = AddDisc(node, _goldDisc, 0.95f);
                Snd.At("coins", node.GlobalPosition, 0.45f);
            }
            catch (Exception e) { Log.Error(Cat, "gold landing effects failed", e); }
        };
        if (EnsureTicker())
        {
            Vector3 start = World.To3(from, fromHeight);
            node.GlobalPosition = start;
            if (delay > 0f) node.Visible = false;
            Anims.Add(new Anim
            {
                Mode = ModePop, Node = node, Vis = vis, From = start, To = World.To3(spot),
                Dur = Rng.Range(0.4f, 0.52f), Delay = delay, Height = Rng.Range(0.9f, 1.4f),
                Spin = Rng.Range(-6f, 6f), Tumble = 0f, Yaw = yaw, Scale = VisScale, Done = land,
            });
        }
        else
        {
            node.GlobalPosition = World.To3(spot);
            vis.Rotation = new Vector3(0f, yaw, 0f);
            land();
        }
        if (Log.Verbose) Log.Debug(Cat, $"gold pile {amount}");
    }

    private static void SpawnGlobe(Vector2 from, float fromHeight, float delay)
    {
        var root = Root();
        if (root == null) { Log.Warn(Cat, "no world root for a health globe"); return; }

        var node = new Interactable { Kind = "globe", Label = "生命之燼", LabelColor = Palette.Bad, ShowLabel = false, CanUse = false, AutoRadius = GlobeTouch, LabelHeight = 1.1f };
        var holder = new Node3D { Position = new Vector3(0f, GlobeHeight, 0f) };
        if (EnsureArt())
        {
            var off = GeometryInstance3D.ShadowCastingSetting.Off;
            holder.AddChild(new MeshInstance3D { Mesh = _globeMesh, MaterialOverride = _globeMat, CastShadow = off });
            holder.AddChild(new MeshInstance3D { Mesh = _quad, MaterialOverride = _haloMat, CastShadow = off, Scale = Vector3.One * 2.6f });
        }
        else holder.AddChild(new MeshInstance3D { Mesh = Fallback(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        holder.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.22f, 0.16f), LightEnergy = 1.1f, OmniRange = 3.2f, OmniAttenuation = 1.6f, ShadowEnabled = false });
        node.AddChild(holder);
        root.AddChild(node);

        node.OnUse = p =>
        {
            if (p == null || p.Hero == null) return;
            int cap = p.Hero.D.PotionCap;
            if (p.Life >= p.MaxLife - 0.5f && p.Potions >= cap)
            {
                // nothing to restore: the globe sleeps until the hero needs it (re-armed by the ticker)
                if (EnsureTicker()) { node.CanUse = false; Dormant.Add(node); }
                return;
            }
            p.Heal(p.MaxLife * 0.15f);
            if (p.Potions < cap) p.Potions++;
            Fx.Heal(p);
            Snd.At("potion", node.GlobalPosition, 0.6f);
            if (Log.Verbose) Log.Debug(Cat, $"health globe: life {p.Life:F0}/{p.MaxLife:F0} potions {p.Potions}/{cap}");
            node.Consume();
        };

        Vector2 spot = FindSpot(from, SmallSpacing);
        Spots.Add(new Spot { Pos = spot, Node = node });
        if (EnsureTicker())
        {
            Vector3 start = World.To3(from, fromHeight);
            node.GlobalPosition = start;
            if (delay > 0f) node.Visible = false;
            Anims.Add(new Anim
            {
                Mode = ModePop, Node = node, Vis = holder, From = start, To = World.To3(spot),
                Dur = Rng.Range(0.45f, 0.55f), Delay = delay, Height = Rng.Range(1.1f, 1.5f), Scale = 1f,
                Done = () =>
                {
                    if (!GodotObject.IsInstanceValid(node)) return;
                    node.CanUse = true;
                    Anims.Add(new Anim { Mode = ModeBob, Node = node, Vis = holder, Height = GlobeHeight, T = Rng.Range(0f, 2f) });
                },
            });
        }
        else
        {
            node.GlobalPosition = World.To3(spot);
            node.CanUse = true;
        }
        if (Log.Verbose) Log.Debug(Cat, "health globe dropped");
    }

    // ───────────────────────── scatter ─────────────────────────

    private struct Spot { public Vector2 Pos; public Node3D Node; }
    private static readonly List<Spot> Spots = new();

    /// <summary>A free, reachable place near <paramref name="origin"/>, kept apart from other loot so labels do not stack.</summary>
    private static Vector2 FindSpot(Vector2 origin, float spacing)
    {
        var grid = World.Grid;
        if (grid != null && !grid.WalkAt(origin)) origin = grid.NearestWalk(origin);
        for (int i = Spots.Count - 1; i >= 0; i--)
        {
            var n = Spots[i].Node;
            if (!GodotObject.IsInstanceValid(n) || n.IsQueuedForDeletion()) { Spots[i] = Spots[Spots.Count - 1]; Spots.RemoveAt(Spots.Count - 1); }
        }

        float baseAng = Rng.Range(0f, Mathf.Tau);
        Vector2 best = origin;
        float bestGap = -1f;
        for (int k = 0; k < 28; k++)
        {
            float ang = baseAng + k * 2.39996f;                    // golden angle: even coverage without clumping
            float r = MathF.Min(0.85f + 0.17f * k, 3.6f);
            var c = origin + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r;
            if (grid != null)
            {
                if (!grid.WalkAt(c) || !grid.LineWalk(origin, c)) continue;
                c = grid.ResolveCircle(c, 0.35f, origin);          // out of pillars, trees and wall edges
            }
            float gap = float.MaxValue;
            for (int i = 0; i < Spots.Count; i++) gap = MathF.Min(gap, Spots[i].Pos.DistanceSquaredTo(c));
            if (gap >= spacing * spacing) return c;
            if (gap > bestGap) { bestGap = gap; best = c; }
        }
        return best;
    }

    // ═════════════════════════ chests ═════════════════════════

    /// <summary>Spills a chest's contents. Quality: 0 common, 1 fine (a rare guaranteed), 2 mini-boss, 3 region boss.</summary>
    public static void OpenChest(Vector2 at, int quality)
    {
        var hero = Game.I?.Hero;
        if (hero == null) return;
        quality = Math.Clamp(quality, 0, 3);
        int wt = Tier();
        int level = AreaLevel(hero);

        Odds odds;
        Rarity floor = Rarity.Common;
        int items, piles, gems = 0, globes = 0, scrap = 0, dust = 0, cores = 0;
        float goldMul;
        switch (quality)
        {
            case 0:
                odds = OddsChest; items = Rng.Range(1, 2); goldMul = 2.5f; piles = 1;
                if (Rng.Chance(0.04f)) gems = 1;
                if (Rng.Chance(0.15f)) scrap = 1;
                break;
            case 1:
                odds = OddsChampion; items = Rng.Range(2, 3); floor = Rarity.Rare; goldMul = 5f; piles = 2;
                if (Rng.Chance(0.12f)) gems = 1;
                if (Rng.Chance(0.4f)) scrap = Rng.Range(1, 2);
                break;
            case 2:
                odds = OddsMini; items = Rng.Range(3, 4); goldMul = 12f; piles = 3; globes = 1;
                if (Rng.Chance(0.35f)) gems = 1;
                scrap = Rng.Range(2, 3); dust = 1;
                break;
            default:
                odds = OddsBoss; items = Rng.Range(5, 6); floor = Rarity.Rare; goldMul = 30f; piles = 4; globes = 1;
                gems = 1; scrap = Rng.Range(3, 5); dust = Rng.Range(2, 3); cores = 1;
                break;
        }
        if (level < 6) gems = 0;
        Log.Debug(Cat, $"chest q{quality} opened at ({at.X:F1}, {at.Y:F1}) lv{level}: {items} items, gold x{goldMul}");

        try
        {
            Vector3 pos = World.To3(at, 0.5f);
            Snd.At("chest_open", pos);
            Fx.Burst(pos, Element.Fire, 1.1f + 0.3f * quality, 0.3f + 0.15f * quality);
            Fx.Ring(World.To3(at, 0.1f), 1.3f + 0.3f * quality, Palette.Gold, 0.5f);
        }
        catch (Exception e) { Log.Error(Cat, "chest effects failed", e); }

        const float h0 = 0.6f;
        int seq = 0;
        GoldPiles(GoldUnit(level, hero, wt) * goldMul, piles, at, h0, ref seq);
        bool strong = quality >= 2;
        var batch = new List<Item>(items);
        for (int i = 0; i < items; i++) batch.Add(Make(level, odds, hero.Class, null, i == 0 ? floor : Rarity.Common, strong, hero));
        batch.Sort(ByRarity);
        foreach (var it in batch) SpawnItem(it, at, h0, seq++ * PopStagger);
        for (int i = 0; i < gems; i++) SpawnItem(ItemDb.RandomGem(level), at, h0, seq++ * PopStagger);
        for (int i = 0; i < globes; i++) SpawnGlobe(at, h0, seq++ * PopStagger);
        if (scrap + dust + cores > 0) DropMaterials(scrap, dust, cores, at);
    }

    // ═════════════════════════ vendors ═════════════════════════

    private static readonly Dictionary<string, List<(Item item, int price)>> Stock = new();
    private static Hero _stockHero;

    /// <summary>
    /// What an NPC sells right now. The merchant's shelf is rolled once per town visit (it resets on the "area" world
    /// event); the jeweler always has one of every gem the hero's level allows. The returned list is the live cached
    /// instance — entries the hero already owns are pruned on every call, so re-query after a purchase.
    /// </summary>
    public static List<(Item item, int price)> VendorStock(string npcId, Hero h)
    {
        if (h == null || string.IsNullOrEmpty(npcId)) return new List<(Item item, int price)>();
        if (!ReferenceEquals(_stockHero, h)) { Stock.Clear(); _stockHero = h; }

        switch (npcId)
        {
            case "merchant":
            {
                if (!Stock.TryGetValue(npcId, out var list))
                {
                    Stock[npcId] = list = BuildMerchant(h);
                    Log.Debug(Cat, $"merchant stock rolled: {list.Count} pieces around lv{h.D.Level}");
                }
                for (int i = list.Count - 1; i >= 0; i--) if (Owned(h, list[i].item)) list.RemoveAt(i);
                return list;
            }
            case "jeweler":
            {
                if (!Stock.TryGetValue(npcId, out var list)) Stock[npcId] = list = new List<(Item item, int price)>(18);
                for (int i = list.Count - 1; i >= 0; i--) if (Owned(h, list[i].item)) list.RemoveAt(i);
                int maxTier = h.D.Level >= 32 ? 3 : h.D.Level >= 15 ? 2 : 1;
                bool added = false;
                foreach (var g in ItemDb.Gems)
                {
                    if (g.Tier > maxTier) continue;
                    bool have = false;
                    foreach (var e in list) if (e.item.GemId == g.Id) { have = true; break; }
                    if (have) continue;
                    var gem = ItemDb.MakeGem(g.Id);
                    list.Add((gem, VendorPrice(gem)));
                    added = true;
                }
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var def = ItemDb.Gem(list[i].item.GemId);
                    if (def == null || def.Tier > maxTier) list.RemoveAt(i);
                }
                if (added) list.Sort(GemOrder);
                return list;
            }
            default:
                // the smith and the occultist offer services, not wares
                return new List<(Item item, int price)>();
        }
    }

    /// <summary>Forces every vendor to restock on the next visit (also happens on each area change).</summary>
    public static void ResetVendors() => Stock.Clear();

    private static int VendorPrice(Item it) => Math.Max(1, it.Value * 4);

    private static bool Owned(Hero h, Item it) =>
        it == null || h.D.Bag.Contains(it) || h.D.Stash.Contains(it) || Array.IndexOf(h.D.Equip, it) >= 0;

    private static List<(Item item, int price)> BuildMerchant(Hero h)
    {
        var list = new List<(Item item, int price)>(10);
        int lvl = Math.Max(1, h.D.Level);
        // one weapon for the class, one of every other slot, two wildcards
        var kinds = new List<ItemKind>(10) { ItemKind.Weapon };
        var pool = (ItemKind[])ArmorKinds.Clone();
        for (int i = pool.Length - 1; i > 0; i--) { int j = Rng.R.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
        kinds.AddRange(pool);
        kinds.Add(Rng.Chance(0.4f) ? ItemKind.Weapon : Rng.Pick(ArmorKinds));
        kinds.Add(Rng.Pick(ArmorKinds));
        foreach (var k in kinds)
        {
            var it = Make(Math.Max(1, lvl + Rng.Range(-2, 1)), OddsVendor, h.Class, k, Rarity.Common, false, h);
            list.Add((it, VendorPrice(it)));
        }
        return list;
    }

    /// <summary>What a gamble on this kind of item costs the hero right now.</summary>
    public static int GamblePrice(ItemKind kind, Hero h)
    {
        int lvl = Math.Max(1, h?.D.Level ?? 1);
        float k = kind switch
        {
            ItemKind.Amulet => 1.9f, ItemKind.Weapon => 1.7f, ItemKind.Ring => 1.6f, ItemKind.Chest => 1.25f,
            ItemKind.Helm or ItemKind.Pants => 1.05f, ItemKind.Gem => 0.8f, _ => 0.9f
        };
        float raw = (70f + lvl * 34f + lvl * lvl * 0.9f) * k * (1f + 0.25f * Tier());
        return Math.Max(5, (int)MathF.Round(raw / 5f) * 5);
    }

    /// <summary>
    /// Rolls an unidentified piece of the chosen kind (mostly magic or rare, a small chance of better) and quotes its
    /// price. Nothing is charged here: the caller pays with <c>Hero.Buy(item, price)</c>.
    /// </summary>
    public static (Item item, int price) Gamble(ItemKind kind, Hero h)
    {
        int price = GamblePrice(kind, h);
        if (h == null) return (null, price);
        int lvl = Math.Max(1, h.D.Level);
        Item it = kind == ItemKind.Gem ? ItemDb.RandomGem(lvl) : Make(lvl, OddsGamble, h.Class, kind, Rarity.Magic, false, h);
        if (it.Rarity >= Rarity.Legendary) Log.Info(Cat, $"gamble on {kind} for {price} gold produced {it.Rarity}: {it.Name} [power {it.Power}]");
        else Log.Debug(Cat, $"gamble on {kind} for {price} gold → {it.Rarity} {it.Name} [power {it.Power}]");
        return (it, price);
    }

    // ═════════════════════════ motion ═════════════════════════

    private const int ModePop = 0, ModeFly = 1, ModeText = 2, ModeBob = 3;

    private sealed class Anim
    {
        public int Mode;
        public Node3D Node;          // what moves
        public Node3D Vis;           // child that tumbles, shrinks or bobs
        public Label3D Text;         // floating numbers only
        public Vector3 From, To;
        public float T, Dur = 1f, Delay, Height, Spin, Tumble, Yaw, Scale = 1f;
        public Color Tint;
        public Action Done;
        public bool Ended;
    }

    private static readonly List<Anim> Anims = new();
    private static readonly List<Interactable> Dormant = new();     // globes waiting until the hero needs them
    private static readonly Action TickAction = TickBody;
    private static Window _window;
    private static bool _hooked, _lastShowAll;
    private static float _slow;
    private static Anim _goldText;
    private static int _goldSum;

    private static bool EnsureTicker()
    {
        if (_hooked) return true;
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null) return false;
        _window = tree.Root;
        tree.ProcessFrame += OnFrame;
        _hooked = true;
        Log.Debug(Cat, "loot ticker attached to the scene tree");
        return true;
    }

    private static void OnFrame() => Log.Guard(Cat, TickAction);

    private static void TickBody()
    {
        var g = Game.I;
        if (g == null || g.Paused) return;
        if (Anims.Count == 0 && Dormant.Count == 0 && Interactable.All.Count == 0) return;
        float dt = MathF.Min(0.1f, (float)_window.GetProcessDeltaTime());

        _slow -= dt;
        if (_slow <= 0f) { _slow = 0.4f; SlowTick(g); }

        for (int i = Anims.Count - 1; i >= 0; i--)
        {
            if (i >= Anims.Count) continue;
            var a = Anims[i];
            bool done;
            try { done = Step(a, dt); }
            catch (Exception e)
            {
                Log.Error(Cat, "loot animation failed", e);
                Abort(a);
                done = true;
            }
            if (!done) continue;
            a.Ended = true;
            int last = Anims.Count - 1;
            if (i <= last && ReferenceEquals(Anims[i], a)) { Anims[i] = Anims[last]; Anims.RemoveAt(last); }
            else Anims.Remove(a);
        }
    }

    /// <summary>Things that only need a look a few times a second.</summary>
    private static void SlowTick(Game g)
    {
        bool showAll = g.Cfg != null && g.Cfg.ShowAllLoot;
        if (showAll != _lastShowAll)
        {
            _lastShowAll = showAll;
            foreach (var i in Interactable.All)
                if (i.Kind == "loot" && i.Item != null) i.AlwaysLabel = Always(i.Item);
            Log.Debug(Cat, $"loot labels: show all = {showAll}");
        }

        if (Dormant.Count == 0) return;
        var p = World.P;
        bool wants = p != null && GodotObject.IsInstanceValid(p) && !p.Dead && p.Hero != null
            && (p.Life < p.MaxLife - 0.5f || p.Potions < p.Hero.D.PotionCap);
        for (int i = Dormant.Count - 1; i >= 0; i--)
        {
            var n = Dormant[i];
            if (!GodotObject.IsInstanceValid(n) || n.IsQueuedForDeletion()) { Dormant.RemoveAt(i); continue; }
            if (wants) { n.CanUse = true; Dormant.RemoveAt(i); }
        }
    }

    /// <summary>Advances one animation; true when it is finished and can be dropped.</summary>
    private static bool Step(Anim a, float dt)
    {
        var n = a.Node;
        if (!GodotObject.IsInstanceValid(n) || !n.IsInsideTree()) return true;
        if (a.Delay > 0f)
        {
            a.Delay -= dt;
            if (a.Delay > 0f) return false;
            n.Visible = true;
        }
        a.T += dt;

        switch (a.Mode)
        {
            case ModePop:
            {
                float t = Mathf.Clamp(a.T / a.Dur, 0f, 1f);
                float e = 1f - (1f - t) * (1f - t);
                const float main = 0.8f;                 // the arc, then one small bounce where it lands
                float y;
                if (t < main) { float u = t / main; y = Mathf.Lerp(a.From.Y, a.To.Y, u) + a.Height * 4f * u * (1f - u); }
                else { float u = (t - main) / (1f - main); y = a.To.Y + a.Height * 0.1f * 4f * u * (1f - u); }
                n.GlobalPosition = new Vector3(Mathf.Lerp(a.From.X, a.To.X, e), y, Mathf.Lerp(a.From.Z, a.To.Z, e));
                if (a.Vis != null)
                {
                    float rest = 1f - MathF.Min(1f, t / main);
                    a.Vis.Rotation = new Vector3(a.Tumble * rest, a.Yaw + a.Spin * rest, 0f);
                    float s = a.Scale * Mathf.Lerp(0.5f, 1f, MathF.Min(1f, t * 2.5f));
                    a.Vis.Scale = new Vector3(s, s, s);
                }
                if (t < 1f) return false;
                a.Done?.Invoke();
                return true;
            }
            case ModeFly:
            {
                float t = Mathf.Clamp(a.T / a.Dur, 0f, 1f);
                var p = World.P;
                Vector3 target = p != null && GodotObject.IsInstanceValid(p) && p.IsInsideTree() ? p.Chest : a.To;
                float e = t * t;
                Vector3 pos = a.From.Lerp(target, e);
                pos.Y += MathF.Sin(t * MathF.PI) * a.Height;
                n.GlobalPosition = pos;
                if (a.Vis != null)
                {
                    float s = a.Scale * (1f - 0.55f * e);
                    a.Vis.Scale = new Vector3(s, s, s);
                    a.Vis.Rotation = new Vector3(0f, a.Yaw + a.T * 14f, 0f);
                }
                if (t < 1f) return false;
                a.Done?.Invoke();
                return true;
            }
            case ModeText:
            {
                float t = Mathf.Clamp(a.T / a.Dur, 0f, 1f);
                float e = 1f - (1f - t) * (1f - t);
                n.GlobalPosition = new Vector3(a.From.X, a.From.Y + a.Height * e, a.From.Z);
                if (t > 0.55f && a.Text != null)
                {
                    float alpha = 1f - (t - 0.55f) / 0.45f;
                    a.Text.Modulate = new Color(a.Tint.R, a.Tint.G, a.Tint.B, alpha);
                    a.Text.OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.95f * alpha);
                }
                if (t < 1f) return false;
                n.QueueFree();
                return true;
            }
            default:
            {
                // globes hover and breathe until someone takes them
                if (a.Vis != null && GodotObject.IsInstanceValid(a.Vis))
                {
                    a.Vis.Position = new Vector3(0f, a.Height + MathF.Sin(a.T * 2.6f) * 0.07f, 0f);
                    float s = 1f + MathF.Sin(a.T * 5.2f) * 0.04f;
                    a.Vis.Scale = new Vector3(s, s, s);
                }
                return false;
            }
        }
    }

    /// <summary>An animation threw: leave its node in a usable state instead of stranded in mid-air.</summary>
    private static void Abort(Anim a)
    {
        try
        {
            var n = a.Node;
            if (!GodotObject.IsInstanceValid(n)) return;
            if (a.Mode == ModePop)
            {
                if (!n.IsInsideTree()) return;
                n.Visible = true;
                n.GlobalPosition = a.To;
                a.Done?.Invoke();
            }
            else if (a.Mode == ModeFly || a.Mode == ModeText) n.QueueFree();
        }
        catch (Exception e) { Log.Error(Cat, "could not settle a failed loot animation", e); }
    }

    /// <summary>Gold picked up in quick succession adds up in one rising number instead of a stack of them.</summary>
    private static void FloatGold(int amount, Vector3 at)
    {
        var a = _goldText;
        if (a != null && !a.Ended && a.T < 0.5f && a.Text != null && GodotObject.IsInstanceValid(a.Text))
        {
            _goldSum += amount;
            a.Text.Text = "+" + _goldSum;
            a.T = MathF.Min(a.T, 0.15f);
            return;
        }
        _goldSum = amount;
        _goldText = FloatText("+" + amount, at, Palette.Gold);
    }

    private static Anim FloatText(string text, Vector3 at, Color color)
    {
        var root = Root();
        if (root == null || !EnsureTicker()) return null;
        var lb = new Label3D
        {
            // sized like the HUD's damage numbers: 56 px glyphs × 0.0039 m = 0.22 m in the world, about 26 px on screen at the
            // default zoom (it used to be twice that and covered the loot labels)
            Text = text, FontSize = 56, OutlineSize = 14, PixelSize = 0.0039f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            Modulate = color, OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.95f),
            RenderPriority = 6, OutlineRenderPriority = 5,
        };
        var font = UiFont();
        if (font != null) lb.Font = font;
        root.AddChild(lb);
        lb.GlobalPosition = at;
        var a = new Anim { Mode = ModeText, Node = lb, Text = lb, From = at, Dur = 1.1f, Height = 1.3f, Tint = color };
        Anims.Add(a);
        return a;
    }

    private static Font _font;
    private static bool _fontTried;

    private static Font UiFont()
    {
        if (_fontTried) return _font;
        _fontTried = true;
        try { _font = UiTheme.Ui; }
        catch (Exception e) { Log.Warn(Cat, "UI font unavailable for floating numbers: " + e.Message); }
        return _font;
    }

    // ═════════════════════════ visuals ═════════════════════════

    private const int KeyWeapon = 100, KeyGold = 200;

    private static bool _artReady, _artFailed;
    private static StandardMaterial3D _steel, _iron, _leather, _cloth, _wood, _gold, _bone, _paper, _ember;
    private static StandardMaterial3D _globeMat, _haloMat, _glintMat, _goldDisc;
    private static readonly StandardMaterial3D[] DiscMats = new StandardMaterial3D[5];
    private static readonly Dictionary<int, Mesh> Meshes = new();
    private static readonly Dictionary<string, StandardMaterial3D> GemMats = new();
    private static Texture2D _dot;
    private static PlaneMesh _discMesh;
    private static QuadMesh _quad;
    private static SphereMesh _globeMesh;
    private static BoxMesh _fallback;

    private static Mesh Fallback() => _fallback ??= new BoxMesh { Size = new Vector3(0.3f, 0.2f, 0.3f) };

    /// <summary>Builds the shared materials and helper meshes once. False when that failed (plain markers are used instead).</summary>
    private static bool EnsureArt()
    {
        if (_artReady) return true;
        if (_artFailed) return false;
        try
        {
            _steel = Solid(new Color(0.62f, 0.64f, 0.68f), 0.85f, 0.35f);
            _iron = Solid(new Color(0.2f, 0.21f, 0.24f), 0.7f, 0.5f);
            _leather = Solid(new Color(0.36f, 0.24f, 0.15f), 0f, 0.85f);
            _cloth = Solid(new Color(0.46f, 0.41f, 0.35f), 0f, 0.95f);
            _wood = Solid(new Color(0.3f, 0.2f, 0.12f), 0f, 0.8f);
            _bone = Solid(new Color(0.8f, 0.76f, 0.66f), 0f, 0.7f);
            _paper = Solid(new Color(0.76f, 0.7f, 0.58f), 0f, 0.9f);
            _gold = Solid(new Color(1f, 0.78f, 0.3f), 0.9f, 0.3f, new Color(0.9f, 0.58f, 0.14f), 0.45f);
            _ember = Solid(new Color(1f, 0.45f, 0.12f), 0.1f, 0.25f, new Color(1f, 0.42f, 0.1f), 2.2f);
            _globeMat = Solid(new Color(0.75f, 0.06f, 0.05f), 0.1f, 0.2f, new Color(1f, 0.12f, 0.08f), 2.6f);

            var grad = new Gradient
            {
                Offsets = new[] { 0f, 0.35f, 1f },
                Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0f) },
            };
            _dot = new GradientTexture2D
            {
                Gradient = grad, Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            };
            for (int r = 0; r < DiscMats.Length; r++) DiscMats[r] = Glow(Palette.Rarity((Rarity)r) * DiscGlow[r], false);
            _goldDisc = Glow(Palette.Gold * 0.3f, false);
            _haloMat = Glow(new Color(1f, 0.16f, 0.1f) * 0.7f, true);
            _glintMat = Glow(new Color(1f, 0.93f, 0.75f) * 0.9f, true);

            _discMesh = new PlaneMesh { Size = Vector2.One };
            _quad = new QuadMesh { Size = new Vector2(0.4f, 0.4f) };
            _globeMesh = new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 14, Rings = 7 };
            _artReady = true;
        }
        catch (Exception e)
        {
            _artFailed = true;
            Log.Error(Cat, "building loot visuals failed; plain markers will be used", e);
        }
        return _artReady;
    }

    private static StandardMaterial3D Solid(Color albedo, float metallic, float roughness, Color? emission = null, float energy = 0f)
    {
        var m = new StandardMaterial3D { AlbedoColor = albedo, Metallic = metallic, Roughness = roughness };
        if (emission.HasValue)
        {
            m.EmissionEnabled = true;
            m.Emission = emission.Value;
            m.EmissionEnergyMultiplier = energy;
        }
        return m;
    }

    /// <summary>Additive soft-dot material: a pool of light on the ground, or a camera-facing glint.</summary>
    private static StandardMaterial3D Glow(Color c, bool billboard)
    {
        var m = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoTexture = _dot,
            AlbedoColor = new Color(c.R, c.G, c.B, 1f),
            DisableReceiveShadows = true,
        };
        if (billboard)
        {
            m.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            m.BillboardKeepScale = true;
        }
        return m;
    }

    private static MeshInstance3D AddDisc(Node3D parent, Material mat, float size)
    {
        var disc = new MeshInstance3D
        {
            Mesh = _discMesh, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0f, 0.04f, 0f), Scale = new Vector3(size, 1f, size),
        };
        parent.AddChild(disc);
        return disc;
    }

    private static void AddGlint(Node3D parent, float height)
    {
        parent.AddChild(new MeshInstance3D
        {
            Mesh = _quad, MaterialOverride = _glintMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0f, height, 0f),
        });
    }

    private static StandardMaterial3D GemMaterial(Item it)
    {
        if (!EnsureArt()) return null;
        var def = ItemDb.Gem(it.GemId);
        string key = def?.Icon ?? "gem";
        if (GemMats.TryGetValue(key, out var m)) return m;
        Color c = def?.Color ?? new Color(0.9f, 0.9f, 0.95f);
        GemMats[key] = m = Solid(c, 0.2f, 0.15f, c, 1.6f);
        return m;
    }

    private static int MeshKey(Item it) =>
        it.Kind == ItemKind.Weapon ? KeyWeapon + Math.Clamp(it.WeaponClass, 0, Names.ClassCount - 1) : (int)it.Kind;

    private static Mesh MeshFor(int key)
    {
        if (Meshes.TryGetValue(key, out var mesh)) return mesh;
        if (!EnsureArt()) return Fallback();
        try { mesh = BuildMesh(key); }
        catch (Exception e)
        {
            Log.Error(Cat, $"building loot mesh {key} failed; using a plain marker", e);
            mesh = Fallback();
        }
        Meshes[key] = mesh;
        return mesh;
    }

    // primitives are kept low-poly: these props are a hand-span wide on screen
    private static BoxMesh Box(float x, float y, float z) => new() { Size = new Vector3(x, y, z) };
    private static CylinderMesh Cyl(float r, float h, int seg = 8) => new() { TopRadius = r, BottomRadius = r, Height = h, RadialSegments = seg, Rings = 1 };
    private static SphereMesh Ball(float r, int seg = 8, int rings = 4) => new() { Radius = r, Height = r * 2f, RadialSegments = seg, Rings = rings };
    private static SphereMesh Dome(float r, float h, int seg = 10, int rings = 4) => new() { Radius = r, Height = h, RadialSegments = seg, Rings = rings, IsHemisphere = true };
    private static SphereMesh Octa(float r, float h) => new() { Radius = r, Height = h, RadialSegments = 4, Rings = 1 };
    private static TorusMesh Hoop(float inner, float outer) => new() { InnerRadius = inner, OuterRadius = outer, Rings = 16, RingSegments = 6 };
    private static CapsuleMesh Cap(float r, float h) => new() { Radius = r, Height = h, RadialSegments = 10, Rings = 3 };
    private static PrismMesh Prism(float x, float y, float z) => new() { Size = new Vector3(x, y, z) };

    /// <summary>Merges primitive meshes into one ArrayMesh with one surface per material (one draw call per material).</summary>
    private sealed class Kit
    {
        private readonly SurfaceTool _st = new();
        private ArrayMesh _mesh;
        private Material _mat;
        private bool _open;

        public Kit Mat(Material m)
        {
            Flush();
            _mat = m;
            _st.Clear();
            _st.Begin(Mesh.PrimitiveType.Triangles);
            _open = true;
            return this;
        }

        /// <summary>Adds a primitive at a position with a rotation in degrees (yaw applied last).</summary>
        public Kit Add(Mesh prim, float x, float y, float z, float rx = 0f, float ry = 0f, float rz = 0f)
        {
            var basis = Basis.FromEuler(new Vector3(Mathf.DegToRad(rx), Mathf.DegToRad(ry), Mathf.DegToRad(rz)));
            _st.AppendFrom(prim, 0, new Transform3D(basis, new Vector3(x, y, z)));
            return this;
        }

        private void Flush()
        {
            if (!_open) return;
            if (_mat != null) _st.SetMaterial(_mat);
            _mesh = _st.Commit(_mesh);
            _open = false;
        }

        public ArrayMesh Done()
        {
            Flush();
            return _mesh ?? new ArrayMesh();
        }
    }

    /// <summary>
    /// Small original props that read as the item's type from the camera: +Z is the long axis, everything rests on y = 0.
    /// </summary>
    private static Mesh BuildMesh(int key)
    {
        var k = new Kit();
        switch (key)
        {
            case KeyWeapon + 0:     // 巨劍: long blade, crossguard, wrapped grip
                k.Mat(_steel).Add(Box(0.11f, 0.028f, 0.92f), 0f, 0.04f, 0.18f).Add(Prism(0.11f, 0.16f, 0.028f), 0f, 0.04f, 0.72f, 90f);
                k.Mat(_iron).Add(Box(0.36f, 0.05f, 0.06f), 0f, 0.045f, -0.3f).Add(Ball(0.05f), 0f, 0.05f, -0.61f);
                k.Mat(_leather).Add(Cyl(0.027f, 0.27f), 0f, 0.045f, -0.45f, 90f);
                break;
            case KeyWeapon + 1:     // 法杖: shaft, a ring at the head holding an ember crystal
                k.Mat(_wood).Add(Cyl(0.026f, 1.2f), 0f, 0.04f, 0f, 90f);
                k.Mat(_steel).Add(Hoop(0.06f, 0.1f), 0f, 0.04f, 0.66f).Add(Cyl(0.034f, 0.08f), 0f, 0.04f, -0.58f, 90f);
                k.Mat(_ember).Add(Octa(0.05f, 0.13f), 0f, 0.075f, 0.66f);
                break;
            case KeyWeapon + 2:     // 手弩: stock, prod and a loaded bolt
                k.Mat(_wood).Add(Box(0.07f, 0.06f, 0.62f), 0f, 0.04f, 0f);
                k.Mat(_iron).Add(Box(0.62f, 0.03f, 0.05f), 0f, 0.055f, 0.24f).Add(Box(0.05f, 0.05f, 0.12f), 0f, 0.06f, -0.14f);
                k.Mat(_steel).Add(Cyl(0.013f, 0.44f, 6), 0f, 0.085f, 0.1f, 90f).Add(Prism(0.05f, 0.08f, 0.02f), 0f, 0.085f, 0.36f, 90f);
                break;
            case KeyWeapon + 3:     // 鐮杖: long shaft and a hooked blade
                k.Mat(_wood).Add(Cyl(0.026f, 1.15f), 0f, 0.04f, 0f, 90f);
                k.Mat(_steel).Add(Box(0.46f, 0.022f, 0.11f), 0.21f, 0.045f, 0.5f, 0f, 12f).Add(Prism(0.11f, 0.2f, 0.022f), 0.5f, 0.045f, 0.41f, 90f, 116f);
                k.Mat(_bone).Add(Ball(0.05f), 0f, 0.05f, 0.56f);
                break;
            case KeyWeapon + 4:     // 圖騰戰棍: bound haft and a spiked head
                k.Mat(_wood).Add(Cyl(0.034f, 0.85f), 0f, 0.05f, -0.12f, 90f).Add(Cyl(0.11f, 0.3f), 0f, 0.11f, 0.42f, 90f);
                k.Mat(_leather).Add(Hoop(0.034f, 0.062f), 0f, 0.05f, -0.34f, 90f).Add(Hoop(0.034f, 0.062f), 0f, 0.05f, -0.22f, 90f);
                k.Mat(_bone).Add(Prism(0.08f, 0.14f, 0.05f), 0.15f, 0.11f, 0.42f, 0f, 0f, -90f).Add(Prism(0.08f, 0.14f, 0.05f), -0.15f, 0.11f, 0.42f, 0f, 0f, 90f)
                    .Add(Prism(0.08f, 0.14f, 0.05f), 0f, 0.25f, 0.42f);
                break;
            case KeyWeapon + 5:     // 拳甲: a pair of plated fists with ember studs
                k.Mat(_iron);
                for (int s = -1; s <= 1; s += 2) k.Add(Box(0.15f, 0.11f, 0.2f), s * 0.13f, 0.06f, 0.05f + s * 0.03f).Add(Cyl(0.075f, 0.14f), s * 0.13f, 0.075f, -0.13f + s * 0.03f, 90f);
                k.Mat(_steel);
                for (int s = -1; s <= 1; s += 2) k.Add(Box(0.16f, 0.045f, 0.065f), s * 0.13f, 0.105f, 0.12f + s * 0.03f);
                k.Mat(_ember);
                for (int s = -1; s <= 1; s += 2) k.Add(Ball(0.024f, 6, 3), s * 0.13f, 0.135f, 0.12f + s * 0.03f);
                break;
            case KeyWeapon + 6:     // 劍盾: round shield with a gilded boss, short sword beside it
                k.Mat(_steel).Add(Cyl(0.3f, 0.045f, 14), -0.14f, 0.03f, 0f).Add(Box(0.06f, 0.022f, 0.62f), 0.36f, 0.03f, 0.1f, 0f, 20f);
                k.Mat(_iron).Add(Hoop(0.265f, 0.315f), -0.14f, 0.04f, 0f).Add(Box(0.2f, 0.04f, 0.045f), 0.254f, 0.035f, -0.191f, 0f, 20f)
                    .Add(Cyl(0.02f, 0.17f, 6), 0.22f, 0.035f, -0.285f, 90f, 20f);
                k.Mat(_gold).Add(Dome(0.085f, 0.06f, 10, 3), -0.14f, 0.05f, 0f);
                break;
            case KeyWeapon + 7:     // 咒典: clasped book with a sigil ring, a wand lying beside it
                k.Mat(_leather).Add(Box(0.34f, 0.018f, 0.44f), -0.06f, 0.009f, 0f).Add(Box(0.34f, 0.018f, 0.44f), -0.06f, 0.103f, 0f).Add(Box(0.022f, 0.112f, 0.44f), -0.22f, 0.056f, 0f);
                k.Mat(_paper).Add(Box(0.3f, 0.076f, 0.41f), -0.05f, 0.056f, 0f);
                k.Mat(_gold).Add(Box(0.03f, 0.114f, 0.07f), 0.112f, 0.057f, 0f).Add(Hoop(0.05f, 0.075f), -0.06f, 0.113f, 0f);
                k.Mat(_wood).Add(Cyl(0.015f, 0.42f, 6), 0.24f, 0.02f, 0f, 90f, 15f);
                k.Mat(_ember).Add(Ball(0.03f, 6, 3), 0.294f, 0.03f, 0.203f);
                break;

            case (int)ItemKind.Helm:    // domed helm with a crest ridge and nasal guard
                k.Mat(_steel).Add(Dome(0.2f, 0.22f, 12, 5), 0f, 0.03f, 0f);
                k.Mat(_iron).Add(Cyl(0.215f, 0.045f, 12), 0f, 0.0225f, 0f).Add(Box(0.035f, 0.07f, 0.34f), 0f, 0.215f, 0f).Add(Box(0.05f, 0.13f, 0.03f), 0f, 0.09f, 0.2f);
                break;
            case (int)ItemKind.Chest:   // armour rolled in ash-grey cloth and strapped, a pauldron showing
                k.Mat(_cloth).Add(Cap(0.17f, 0.56f), 0f, 0.17f, 0f, 0f, 0f, 90f);
                k.Mat(_leather).Add(Hoop(0.165f, 0.2f), -0.12f, 0.17f, 0f, 0f, 0f, 90f).Add(Hoop(0.165f, 0.2f), 0.12f, 0.17f, 0f, 0f, 0f, 90f);
                k.Mat(_steel).Add(Dome(0.12f, 0.08f, 10, 3), 0f, 0.31f, 0f);
                break;
            case (int)ItemKind.Gloves:  // a pair of gloves with iron cuffs
                k.Mat(_leather);
                for (int s = -1; s <= 1; s += 2)
                    k.Add(Box(0.12f, 0.07f, 0.18f), s * 0.11f, 0.04f, 0.05f + s * 0.03f).Add(Box(0.045f, 0.05f, 0.09f), s * 0.188f, 0.035f, 0.02f + s * 0.03f);
                k.Mat(_iron);
                for (int s = -1; s <= 1; s += 2) k.Add(Cyl(0.068f, 0.11f), s * 0.11f, 0.068f, -0.09f + s * 0.03f, 90f);
                break;
            case (int)ItemKind.Pants:   // folded leg armour under a buckled belt
                k.Mat(_cloth).Add(Box(0.36f, 0.06f, 0.42f), 0f, 0.03f, 0f);
                k.Mat(_leather).Add(Box(0.33f, 0.055f, 0.2f), 0f, 0.088f, -0.09f);
                k.Mat(_iron).Add(Box(0.37f, 0.03f, 0.05f), 0f, 0.075f, 0.12f);
                k.Mat(_gold).Add(Box(0.06f, 0.036f, 0.062f), 0f, 0.077f, 0.12f);
                break;
            case (int)ItemKind.Boots:   // a pair of boots with capped toes
                k.Mat(_leather);
                for (int s = -1; s <= 1; s += 2) k.Add(Cyl(0.065f, 0.26f), s * 0.1f, 0.19f, -0.05f).Add(Box(0.125f, 0.085f, 0.25f), s * 0.1f, 0.043f, 0.03f);
                k.Mat(_iron);
                for (int s = -1; s <= 1; s += 2) k.Add(Box(0.13f, 0.05f, 0.07f), s * 0.1f, 0.04f, 0.16f).Add(Hoop(0.06f, 0.08f), s * 0.1f, 0.31f, -0.05f);
                break;
            case (int)ItemKind.Amulet:  // a loop of chain and an ember crystal pendant
                k.Mat(_gold).Add(Hoop(0.125f, 0.165f), 0f, 0.02f, -0.03f).Add(Cyl(0.055f, 0.024f), 0f, 0.022f, 0.13f);
                k.Mat(_ember).Add(Octa(0.045f, 0.085f), 0f, 0.06f, 0.13f);
                break;
            case (int)ItemKind.Ring:    // a band with a set stone
                k.Mat(_gold).Add(Hoop(0.07f, 0.115f), 0f, 0.023f, 0f).Add(Cyl(0.042f, 0.03f, 6), 0f, 0.035f, 0.093f);
                k.Mat(_ember).Add(Octa(0.04f, 0.075f), 0f, 0.075f, 0.093f);
                break;
            case (int)ItemKind.Gem:     // a cut stone balanced on its point (material set per gem)
                k.Mat(_ember).Add(Octa(0.12f, 0.3f), 0f, 0.18f, 0f, 0f, 0f, 16f);
                break;

            case KeyGold + 0:
            case KeyGold + 1:
            case KeyGold + 2:
            {
                int size = key - KeyGold;
                var rnd = new Random(7919 + size * 31);          // fixed seed: the same three piles every run
                int coins = size == 0 ? 5 : size == 1 ? 10 : 16;
                float spread = size == 0 ? 0.13f : size == 1 ? 0.2f : 0.28f;
                float mound = size == 0 ? 0f : size == 1 ? 0.075f : 0.12f;
                k.Mat(_gold);
                if (size > 0) k.Add(Dome(size == 1 ? 0.13f : 0.2f, mound, 10, 3), 0f, 0f, 0f);
                for (int i = 0; i < coins; i++)
                {
                    float ang = (float)rnd.NextDouble() * MathF.Tau, r = spread * MathF.Sqrt((float)rnd.NextDouble());
                    float y = 0.012f + mound * MathF.Max(0f, 1f - r / spread) + (float)rnd.NextDouble() * 0.02f;
                    float tiltX = ((float)rnd.NextDouble() * 2f - 1f) * 26f, tiltZ = ((float)rnd.NextDouble() * 2f - 1f) * 26f;
                    k.Add(Cyl(0.055f, 0.016f), MathF.Cos(ang) * r, y, MathF.Sin(ang) * r, tiltX, (float)rnd.NextDouble() * 360f, tiltZ);
                }
                break;
            }
            default:
                return Fallback();
        }
        return k.Done();
    }
}
