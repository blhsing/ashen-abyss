using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Anything the hero can click or walk into: loot on the ground, chests, shrines, altars, portals, NPCs, doors.
/// Behaviour is supplied as callbacks so one node type covers them all.
/// </summary>
public partial class Interactable : Node3D
{
    public static readonly List<Interactable> All = new();
    public string Kind = "", Label = "", Sub = "";
    public Color LabelColor = Palette.Text;
    public float UseRange = 2.2f;
    public float AutoRadius;                  // > 0: triggers by itself when the hero walks this close (gold, health globes)
    public bool CanUse = true, ShowLabel = true, AlwaysLabel;
    public Action<Player> OnUse;
    public Item Item;                         // loot payload, if any
    public Rarity LootRarity;
    public float LabelHeight = 1.0f;
    public object Tag;
    /// <summary>Puts the object in its used-up state without its effects (rewards, sounds, events): a loaded save restoring an opened chest, a toppled idol…</summary>
    public Action Spend;

    public Vector2 Pos2 => new(GlobalPosition.X, GlobalPosition.Z);

    public override void _EnterTree() => All.Add(this);
    public override void _ExitTree() => All.Remove(this);

    public void Use(Player p)
    {
        if (!CanUse) return;
        Log.Guard("interact", () => OnUse?.Invoke(p));
    }

    public override void _Process(double delta)
    {
        if (AutoRadius <= 0 || !CanUse) return;
        var p = World.P;
        if (p != null && !p.Dead && p.Pos2.DistanceTo(Pos2) <= AutoRadius) Use(p);
    }

    public static Interactable Pick(Vector2 aim, float r)
    {
        Interactable best = null; float bd = r;
        foreach (var i in All)
        {
            if (!i.CanUse || i.AutoRadius > 0) continue;
            float d = i.Pos2.DistanceTo(aim);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    public void Consume() { CanUse = false; QueueFree(); }

    /// <summary>Save restore: this object was already used. Without a <see cref="Spend"/> it just stops being usable.</summary>
    public void RestoreSpent()
    {
        if (Spend != null) { Log.Guard("interact", Spend); return; }
        CanUse = false; ShowLabel = false;
    }
}
