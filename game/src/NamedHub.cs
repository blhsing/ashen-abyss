using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Registry of live named encounters and the glue to the rest of the game.
/// PARKED (2026-11-15): only the registry exists. Nothing in the game creates a <see cref="NamedEncounter"/> yet, so the
/// whole named layer is inert: no area hook, no spawning, no loot, no records. See Y:\NAMED_NOTES.md for the next steps.
/// </summary>
public static class NamedHub
{
    private static readonly Dictionary<Monster, NamedEncounter> Owner = new();      // named monsters, their adds and props → encounter

    /// <summary>Called by every new BossBrain: a named encounter re-attaches its script to a fresh brain. No-op for region bosses.</summary>
    internal static void BrainCreated(Monster m, BossBrain brain)
    {
        if (m == null || Owner.Count == 0) return;
        if (Owner.TryGetValue(m, out var e) && e != null && GodotObject.IsInstanceValid(e) && ReferenceEquals(e.M, m)) e.AttachBrain(brain);
    }

    internal static void Link(Monster m, NamedEncounter e) { if (m != null && e != null) Owner[m] = e; }
    internal static void Unlink(Monster m) { if (m != null) Owner.Remove(m); }

    internal static void Forget(NamedEncounter e)
    {
        if (e == null || Owner.Count == 0) return;
        var gone = new List<Monster>();
        foreach (var kv in Owner) if (ReferenceEquals(kv.Value, e)) gone.Add(kv.Key);
        foreach (var m in gone) Owner.Remove(m);
    }

    /// <summary>The named monster of an encounter died: loot, kill record, announcements. (Not written yet.)</summary>
    internal static void Slain(NamedEncounter e, Vector2 at) { }

    /// <summary>True when burning ground lies within <paramref name="r"/> of a point. (Not written yet: needs a GroundZone accessor.)</summary>
    internal static bool FireNear(Vector2 p, float r) => false;
}
