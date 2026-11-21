using Godot;

namespace AshenAbyss;

/// <summary>
/// Puts the left hand on the second grip of a two-hand weapon held in the right hand. Runs after the animation (a
/// SkeletonModifier3D child of the hero's skeleton): the weapon's frame is read from the animated right hand, the left
/// wrist is placed where the left fist closes round the grip point (charlib's left grip frame), and the arm is solved
/// as two bones keeping the elbow on the side the clip bent it. Out of reach, the arm stretches as far as it can.
/// </summary>
public partial class GearGripIK : SkeletonModifier3D
{
    public int Upper = -1, Lower = -1, HandL = -1, HandR = -1;
    /// <summary>Weapon frame in hand_r's space (contract weapon axes: +Z up the weapon).</summary>
    public Transform3D WeaponInHand = Transform3D.Identity;
    /// <summary>The second grip point in weapon space.</summary>
    public Vector3 Grip2;
    /// <summary>Left grip frame: weapon space → hand_l space when the left fist holds the weapon at its origin.</summary>
    public Transform3D GripL = Transform3D.Identity;

    public override void _ProcessModificationWithDelta(double delta)
    {
        var sk = GetSkeleton();
        if (sk == null || Upper < 0 || Lower < 0 || HandL < 0 || HandR < 0) return;
        float k = Influence;
        if (k <= 0.001f) return;
        var weapon = sk.GetBoneGlobalPose(HandR) * WeaponInHand;
        var want = weapon * new Transform3D(Basis.Identity, Grip2) * GripL.AffineInverse();     // hand_l in skeleton space

        var U = sk.GetBoneGlobalPose(Upper);
        var L = sk.GetBoneGlobalPose(Lower);
        var H = sk.GetBoneGlobalPose(HandL);
        Vector3 s = U.Origin, e = L.Origin, w = H.Origin, t = want.Origin;
        float a = s.DistanceTo(e), b = e.DistanceTo(w);
        if (a < 1e-4f || b < 1e-4f) return;
        Vector3 st = t - s;
        float d = st.Length();
        if (d < 1e-4f) return;
        Vector3 dir = st / d;
        d = Mathf.Clamp(d, Mathf.Abs(a - b) + 1e-3f, (a + b) * 0.999f);
        t = s + dir * d;

        // the bend stays on the side the animation put the elbow (falls back to down and out)
        Vector3 pole = (e - s) - dir * (e - s).Dot(dir);
        if (pole.LengthSquared() < 1e-6f) pole = new Vector3(0.3f, -1f, -0.2f) - dir * new Vector3(0.3f, -1f, -0.2f).Dot(dir);
        pole = pole.Normalized();
        float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
        Vector3 eNew = s + dir * (a * cosA) + pole * (a * Mathf.Sqrt(1f - cosA * cosA));

        var q1 = Arc((e - s).Normalized(), (eNew - s).Normalized());
        var Ub = new Basis(q1) * U.Basis;
        Vector3 w1 = s + q1 * (w - s);
        var q2 = Arc((w1 - eNew).Normalized(), (t - eNew).Normalized());
        var Lb = new Basis(q2) * new Basis(q1) * L.Basis;
        var Hb = want.Basis;

        int parent = sk.GetBoneParent(Upper);
        var P = parent >= 0 ? sk.GetBoneGlobalPose(parent).Basis : Basis.Identity;
        Set(sk, Upper, (P.Inverse() * Ub).GetRotationQuaternion(), k);
        Set(sk, Lower, (Ub.Inverse() * Lb).GetRotationQuaternion(), k);
        Set(sk, HandL, (Lb.Inverse() * Hb).GetRotationQuaternion(), k);
    }

    private static void Set(Skeleton3D sk, int bone, Quaternion q, float k)
    {
        var cur = sk.GetBonePoseRotation(bone);
        sk.SetBonePoseRotation(bone, k >= 0.999f ? q.Normalized() : cur.Slerp(q.Normalized(), k));
    }

    private static Quaternion Arc(Vector3 from, Vector3 to)
    {
        float c = from.Dot(to);
        if (c > 0.99999f) return Quaternion.Identity;
        if (c < -0.99999f)
        {
            var axis = from.Cross(Vector3.Right);
            if (axis.LengthSquared() < 1e-6f) axis = from.Cross(Vector3.Up);
            return new Quaternion(axis.Normalized(), Mathf.Pi);
        }
        return new Quaternion(from, to);
    }
}
