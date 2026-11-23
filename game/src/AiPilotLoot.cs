using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// AI pilot loot: after a fight the pilot walks to what dropped and picks it up the way a player does — Player.GoUse on
/// the item on the ground (the same Interactable.OnUse → Loot.Pickup path as a click), walking over gold and health
/// globes. What it picks up: equipment that GearEval rates as an upgrade, anything rare or better, usable magic pieces,
/// gems; gold always; health globes when hurt or short of potions (crafting materials go to the stores by themselves).
/// Not while enemies are on the hero or a mark is about to land; during a boss fight only an item within 4 m of a safe
/// spot. With a full bag it leaves equipment on the ground (the bag trip, a town portal and back, empties it; dungeons and rifts come back as left).
/// </summary>
public partial class TestBot
{
    private ulong _lootId;
    private Item _lootItem;
    private double _lootSince, _lootIssued = -10, _lootScan = -10;
    private readonly Dictionary<ulong, double> _lootSkip = new();

    private static bool LootWanted(Hero hero, Item it)
    {
        if (it == null) return false;
        if (it.Kind == ItemKind.Gem) return true;
        if (!it.IsEquip) return true;
        if (it.Rarity >= Rarity.Rare) return true;
        if (!hero.UsableByClass(it)) return false;
        if (it.Rarity >= Rarity.Magic) return true;
        return GearEval.IsUpgrade(hero, it);
    }

    /// <summary>True when the pilot spent this think on loot.</summary>
    private bool PilotLoot(Player p, Hero hero, Vector2 me, Monster target, Level level)
    {
        if (p.Busy || p.Dead) return false;
        // the item it went for: picked up (gone from the ground and in the bag) → logged once
        if (_lootItem != null)
        {
            bool inBag = hero.D.Bag.Contains(_lootItem);
            if (inBag)
            {
                Log.Info("pilot", $"picks up {_lootItem.Name} ({_lootItem.Rarity})");
                _lootItem = null; _lootId = 0;
            }
        }
        bool threat = target != null && target.Aggro && target.Pos2.DistanceTo(me) < 10f || CloseMonsters(me, 8f) > 0;
        if (threat) return false;
        if (Risk(p, me, me, 0f, false, 1.2f, out _) > 0.01f) return false;
        bool boss = DangerMap.BossFight();
        float reach = boss ? 4f : 20f;

        Interactable best = null; float bd = float.MaxValue;
        var all = Interactable.All;
        double now = _clock;
        for (int i = 0; i < all.Count; i++)
        {
            var it = all[i];
            if (it == null || !IsInstanceValid(it) || !it.CanUse || !it.IsInsideTree()) continue;
            float d = it.Pos2.DistanceTo(me);
            if (d > reach) continue;
            ulong id = it.GetInstanceId();
            if (_lootSkip.TryGetValue(id, out double until) && until > now) continue;
            bool want;
            if (it.Item != null) want = !hero.BagFull && LootWanted(hero, it.Item);
            else if (it.Kind == "gold") want = true;
            else if (it.Kind == "globe") want = p.LifeFrac < 0.95f || p.Potions < hero.D.PotionCap;
            else continue;
            if (!want) continue;
            if (boss && Risk(p, me, it.Pos2, HeroSpeed(p), false, 1.5f, out _) > 0.01f) continue;
            float score = d - (it.Item != null ? (int)it.Item.Rarity * 1.5f : 0f);
            if (score < bd) { bd = score; best = it; }
        }
        if (best == null) { _lootId = 0; return false; }

        ulong bid = best.GetInstanceId();
        if (bid != _lootId) { _lootId = bid; _lootSince = now; _lootIssued = -10; _lootItem = best.Item; }
        if (now - _lootSince > 8.0)
        {
            _lootSkip[bid] = now + 60.0;          // could not get to it (a wall, a ledge): not again for a minute
            _lootId = 0; _lootItem = null;
            return false;
        }
        if (now - _lootIssued > 0.6)
        {
            _lootIssued = now;
            if (best.AutoRadius > 0f) p.SetMove(best.Pos2, false); else p.GoUse(best);
            _moving = true;
        }
        _doing = "loot " + (best.Item?.Name ?? best.Kind);
        _stuckSince = _clock;
        return true;
    }
}
