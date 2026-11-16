using Godot;

namespace AshenAbyss;

/// <summary>
/// The title backdrop's pose on top of the boss's idle: his chin lowered (a nod about the model's left-right axis, shared
/// between neck and head) so he glares up at the lens from under his brow. A skeleton modifier, so it runs after the
/// animation every frame and the idle's breathing still moves everything else.
/// </summary>
public partial class LairStare : SkeletonModifier3D
{
    public int Head = -1, Neck = -1;
    /// <summary>Degrees of nod on the neck and on the head (+ = chin down, toward the lens).</summary>
    public float NeckDeg, HeadDeg;

    public override void _ProcessModificationWithDelta(double delta)
    {
        var sk = GetSkeleton();
        if (sk == null) return;
        Nod(sk, Neck, NeckDeg);
        Nod(sk, Head, HeadDeg);
    }

    private static void Nod(Skeleton3D sk, int bone, float deg)
    {
        if (bone < 0 || Mathf.IsZeroApprox(deg)) return;
        int parent = sk.GetBoneParent(bone);
        var pg = parent >= 0 ? sk.GetBoneGlobalPose(parent).Basis.Orthonormalized() : Basis.Identity;
        var g = sk.GetBoneGlobalPose(bone).Basis.Orthonormalized();
        // the model faces +Z: a turn about +X carries the top of the head toward the lens
        var turned = new Basis(Vector3.Right, Mathf.DegToRad(deg)) * g;
        sk.SetBonePoseRotation(bone, (pg.Inverse() * turned).GetRotationQuaternion());
    }
}
