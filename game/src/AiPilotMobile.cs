using Godot;

namespace AshenAbyss;

/// <summary>
/// [MOBILE 0.4.1] The AI pilot shoots on the move (施法時可移動, Player.Mobile.cs). A Mobile cast no longer stops the walk,
/// so a ranged hero:
/// - kites while it fires: when the kite (PilotPosition) sends it back to its distance, it keeps casting Mobile skills
///   instead of waiting the kite out, and it starts backing away sooner (as soon as a monster closes inside its ideal
///   range less 2 m, not only when one reaches 3 m);
/// - fires while it dodge-walks out of a mark, when the slower walk (65 %) still clears the mark in time.
/// The kite spot still comes from RingSpot: safe from the danger map and away from sleeping packs' notice range.
/// With the setting off everything behaves as before.
/// </summary>
public partial class TestBot
{
    private bool _mobileOnly;
    private double _shootDodgeAt = -10;
    /// <summary>Statistics for the pilot harness: Mobile casts made while kiting / while dodge-walking.</summary>
    internal int KiteShots, DodgeShots;

    /// <summary>The pilot's hero can use moving casts: the setting is on and its basic attack is a ranged Mobile skill.</summary>
    private static bool PilotMobile(Hero hero)
    {
        if (!Player.MobileCastOn || !PilotRanged(hero)) return false;
        var basic = hero.Eff(hero.D.Bar[0]) ?? hero.Eff(hero.D.Bar[1]);
        return basic != null && basic.Mobile;
    }

    /// <summary>While the kite walk runs: the Mobile skills on the bar, in the usual priority order.</summary>
    private void PilotShootOnTheMove(Player p, Hero hero, Monster t, Vector2 me, float dist, bool los)
    {
        if (p.Busy || t == null || !IsInstanceValid(t) || t.Dead || !PilotMobile(hero)) return;
        _mobileOnly = true;
        try
        {
            for (int k = 0; k < SlotOrder.Length; k++)
                if (TryUse(p, hero, SlotOrder[k], t, me, dist, los)) { KiteShots++; _doing = "kite + shoot"; return; }
        }
        finally { _mobileOnly = false; }
    }

    /// <summary>
    /// While the reflex walks the hero out of a mark: one Mobile skill at the fight's target (or the nearest enemy in
    /// sight that has noticed the hero), only when the walk at the casting speed still gets clear in time.
    /// </summary>
    private void PilotShootWhileDodging(Player p, float real)
    {
        if (!_dgActive || p.Busy || p.Stunned || p.Rooted || _chanSlot >= 0) return;
        if (_clock - _shootDodgeAt < 0.12) return;
        var hero = p.Hero;
        if (hero == null || !PilotMobile(hero)) return;
        _shootDodgeAt = _clock;
        Vector2 me = p.Pos2;
        float slow = Mathf.Max(1.5f, HeroSpeed(p) * Player.MobileSpeedMul);
        if (Risk(p, me, _dgTo, slow, false, Horizon, out _) > 0.01f) return;        // the walk must still clear the mark at 65 %
        var grid = World.Grid;
        Monster t = PickedTarget();
        if (t == null || t.Dead || !t.Aggro)
        {
            t = null; float bd = 18f * 18f;
            var list = World.Monsters;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null || m.Dead || m.Untargetable || !m.Aggro || !IsInstanceValid(m)) continue;
                float d2 = m.Pos2.DistanceSquaredTo(me);
                if (d2 < bd && (grid == null || grid.LineShot(me, m.Pos2))) { bd = d2; t = m; }
            }
        }
        if (t == null || !IsInstanceValid(t)) return;
        float dist = me.DistanceTo(t.Pos2);
        bool los = grid == null || grid.LineShot(me, t.Pos2);
        _mobileOnly = true;
        try
        {
            for (int k = 0; k < SlotOrder.Length; k++)
                if (TryUse(p, hero, SlotOrder[k], t, me, dist, los)) { DodgeShots++; return; }
        }
        finally { _mobileOnly = false; }
    }
}
