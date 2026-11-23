using Godot;

namespace AshenAbyss;

/// <summary>Player-minion hooks of the monster state machine (MinionSystem.cs, Golems.cs). Hostile monsters never use them.</summary>
public partial class Monster
{
    /// <summary>Behaviour of a book minion or golem (null for plain summons).</summary>
    public MinionCtl Ctl;
    /// <summary>The actor the state machine is after right now (null when idle).</summary>
    public Actor CurrentTarget => _target;
    /// <summary>Points the minion at a new target at once (the hero's "attack my target").</summary>
    public void SetTarget(Actor a) { _target = a; _path.Clear(); }
    public bool IsGolem => Ctl is GolemCtl;
    /// <summary>Hit a player minion takes, before its life: book kinds and golems scale it with the hero (MinionSystem.Incoming).</summary>
    private float MinionIncoming(Hit h) => Faction == Faction.Player ? MinionSystem.Incoming(this, h) : h.Amount;
}
