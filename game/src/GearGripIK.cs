using Godot;

namespace AshenAbyss;

/// <summary>
/// Puts the left hand on the second grip of a two-hand weapon held in the right hand. Runs after the animation (a
/// SkeletonModifier3D child of the hero's skeleton): the weapon's frame is read from the animated right hand, the left
/// wrist is placed where the left fist closes round a point of the shaft (charlib's left grip frame), and the arm is
/// solved as two bones keeping the elbow on the side the clip bent it.
///
/// The grip point is the weapon's grip2 when that lies outside the body and within the arm's reach; otherwise the
/// nearest point of the shaft between grip2 and the right fist that is (a one-hand clip often holds the shaft so that
/// its butt end runs through the torso). When no point of the shaft qualifies, the left arm is left to the animation,
/// blended out smoothly — never pulled into the chest.
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
    /// <summary>Torso bones (pelvis, neck) for the body test; -1 = no test.</summary>
    public int Pelvis = -1, Neck = -1;
    /// <summary>Radius of the torso capsule plus the room a fist needs (metres).</summary>
    public float BodyRadius = 0.24f;
    /// <summary>0 = off, 1 = grip2 only (the first version), 2 = the safe grip point (default). Test switch --gear-ik=N.</summary>
    public static int Mode = 2;

    // ── diagnostics (the test suite reads them; skeleton space) ──
    public Vector3 LastGrip2, LastTarget, LastWrist, LastElbow;
    public float LastWeight, LastT;
    public bool LastGrip2Inside, LastGrip2OutOfReach;

    private float _w = -1f;

    public override void _ProcessModificationWithDelta(double delta)
    {
        var sk = GetSkeleton();
        if (sk == null || Upper < 0 || Lower < 0 || HandL < 0 || HandR < 0 || Mode == 0) return;
        float k = Influence;
        if (k <= 0.001f) return;
        var weapon = sk.GetBoneGlobalPose(HandR) * WeaponInHand;
        var U = sk.GetBoneGlobalPose(Upper);
        var L = sk.GetBoneGlobalPose(Lower);
        var H = sk.GetBoneGlobalPose(HandL);
        Vector3 s = U.Origin;
        float a = s.DistanceTo(L.Origin), b = L.Origin.DistanceTo(H.Origin);
        if (a < 1e-4f || b < 1e-4f) return;
        float reach = (a + b) * 0.97f;

        // where on the shaft the left fist goes
        Transform3D AtT(float t) => weapon * new Transform3D(Basis.Identity, Grip2 * t) * GripL.AffineInverse();
        float tUse = 1f;
        float want = 1f;
        LastGrip2 = weapon * Grip2;
        LastGrip2Inside = Inside(sk, LastGrip2);
        LastGrip2OutOfReach = s.DistanceTo(AtT(1f).Origin) > reach;
        if (Mode == 2)
        {
            // from grip2 (t = 1) toward the right fist (t = 0.25: a fist's width away): the first point outside the body and in reach
            tUse = -1f;
            for (float t = 1f; t >= 0.249f; t -= 0.05f)
            {
                var p = weapon * (Grip2 * t);
                if (Inside(sk, p)) continue;
                if (s.DistanceTo(AtT(t).Origin) > reach) continue;
                tUse = t; break;
            }
            if (tUse < 0f) { want = 0f; tUse = 1f; }
        }
        // ease in and out (no pop when the clip swings the shaft through the body)
        float dt = (float)delta;
        _w = _w < 0f ? want : Mathf.MoveToward(_w, want, dt * 8f);
        LastWeight = _w; LastT = tUse;
        float w = k * _w;
        var target = AtT(tUse);
        LastTarget = target.Origin;
        if (w <= 0.001f) { LastWrist = H.Origin; LastElbow = L.Origin; return; }

        Vector3 e = L.Origin, wr = H.Origin, t0 = target.Origin;
        Vector3 st = t0 - s;
        float d = st.Length();
        if (d < 1e-4f) return;
        Vector3 dir = st / d;
        d = Mathf.Clamp(d, Mathf.Abs(a - b) + 1e-3f, (a + b) * 0.999f);
        Vector3 tt = s + dir * d;

        // the bend stays on the side the animation put the elbow (falls back to down and out)
        Vector3 pole = (e - s) - dir * (e - s).Dot(dir);
        if (pole.LengthSquared() < 1e-6f) pole = new Vector3(0.3f, -1f, -0.2f) - dir * new Vector3(0.3f, -1f, -0.2f).Dot(dir);
        pole = pole.Normalized();
        float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
        Vector3 eNew = s + dir * (a * cosA) + pole * (a * Mathf.Sqrt(1f - cosA * cosA));

        var q1 = Arc((e - s).Normalized(), (eNew - s).Normalized());
        var Ub = new Basis(q1) * U.Basis;
        Vector3 w1 = s + q1 * (wr - s);
        var q2 = Arc((w1 - eNew).Normalized(), (tt - eNew).Normalized());
        var Lb = new Basis(q2) * new Basis(q1) * L.Basis;
        var Hb = target.Basis;
        LastWrist = tt; LastElbow = eNew;

        int parent = sk.GetBoneParent(Upper);
        var P = parent >= 0 ? sk.GetBoneGlobalPose(parent).Basis : Basis.Identity;
        Set(sk, Upper, (P.Inverse() * Ub).GetRotationQuaternion(), w);
        Set(sk, Lower, (Ub.Inverse() * Lb).GetRotationQuaternion(), w);
        Set(sk, HandL, (Lb.Inverse() * Hb).GetRotationQuaternion(), w);
    }

    /// <summary>True when a point (skeleton space) lies within the torso capsule (pelvis → neck).</summary>
    private bool Inside(Skeleton3D sk, Vector3 p)
    {
        if (Pelvis < 0 || Neck < 0) return false;
        Vector3 a = sk.GetBoneGlobalPose(Pelvis).Origin, b = sk.GetBoneGlobalPose(Neck).Origin;
        Vector3 ab = b - a;
        float t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
        return p.DistanceTo(a + ab * t) < BodyRadius;
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
