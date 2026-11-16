using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// Keeps its children (the weather emitter) at a fixed offset from the hero, or from the camera focus while there
/// is no hero. It only copies a position, so it runs during pauses as well.
/// </summary>
public partial class EnvFollow : Node3D
{
    public Vector3 Offset;
    private int _errors;

    public override void _Ready() => Snap();

    public override void _Process(double delta)
    {
        try { Snap(); }
        catch (Exception e)
        {
            if (_errors++ < 3) Log.Error("env", "weather follower update failed", e);
        }
    }

    private void Snap()
    {
        if (!IsInsideTree()) return;
        Node3D target = World.P;
        if (target == null || !IsInstanceValid(target) || !target.IsInsideTree()) target = Game.I?.Cam;
        if (target == null || !IsInstanceValid(target) || !target.IsInsideTree()) return;
        var g = target.GlobalPosition;
        GlobalPosition = new Vector3(g.X + Offset.X, Offset.Y, g.Z + Offset.Z);     // the ground plane: jumps do not lift the weather
    }
}
