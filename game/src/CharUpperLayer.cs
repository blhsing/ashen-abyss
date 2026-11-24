using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Upper-body animation layer (RENDER, for moving while attacking). The model's AnimationPlayer keeps playing a
/// locomotion clip on the whole body; this SkeletonModifier3D, the first modifier under the skeleton, overwrites the
/// local rotations of the upper body (spine, neck, head, clavicles, arms, hands, fingers) with a cast / shoot clip
/// sampled at its own time, and twists the spine so the chest faces the aim while the hips face the movement.
/// The spine_01 joint takes half the clip so the waist blends; the twist is shared over the three spine bones.
/// </summary>
public partial class CharUpperLayer : SkeletonModifier3D
{
    public Animation Clip;
    public double Time, Rate = 1.0;
    /// <summary>Spine twist toward the aim (radians, about the model's up axis); positive turns the chest left.</summary>
    public float Twist;
    private float _w, _twistNow;
    public bool Fading;
    private int[] _bones;            // upper-body bones
    private float[] _boneW;          // per-bone weight of the clip
    private int[] _spine = Array.Empty<int>();
    private static readonly Dictionary<(ulong, int), int[]> TrackCache = new();

    public bool Busy => Clip != null && !Fading;
    public float Weight => _w;

    public void Setup(Skeleton3D sk)
    {
        var bones = new List<int>(); var w = new List<float>(); var spine = new List<int>();
        for (int i = 0; i < sk.GetBoneCount(); i++)
        {
            string n = sk.GetBoneName(i).ToLowerInvariant();
            bool upper = n.StartsWith("spine") || n.StartsWith("neck") || n.StartsWith("head") || n.StartsWith("clavicle") || n.StartsWith("upperarm")
                || n.StartsWith("lowerarm") || n.StartsWith("hand") || n.StartsWith("index") || n.StartsWith("middle") || n.StartsWith("ring")
                || n.StartsWith("pinky") || n.StartsWith("thumb");
            if (!upper) continue;
            bones.Add(i); w.Add(n == "spine_01" ? 0.5f : 1f);
            if (n is "spine_01" or "spine_02" or "spine_03") spine.Add(i);
        }
        _bones = bones.ToArray(); _boneW = w.ToArray(); _spine = spine.ToArray();
    }

    private int[] Tracks(Skeleton3D sk, Animation a)
    {
        var key = (a.GetInstanceId(), sk.GetBoneCount());
        if (TrackCache.TryGetValue(key, out var t)) return t;
        t = new int[sk.GetBoneCount()];
        Array.Fill(t, -1);
        for (int i = 0; i < a.GetTrackCount(); i++)
        {
            if (a.TrackGetType(i) != Animation.TrackType.Rotation3D) continue;
            string p = a.TrackGetPath(i).ToString();
            int c = p.LastIndexOf(':');
            if (c < 0) continue;
            int b = sk.FindBone(p[(c + 1)..]);
            if (b >= 0) t[b] = i;
        }
        TrackCache[key] = t;
        return t;
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var sk = GetSkeleton();
        if (sk == null || _bones == null) return;
        float dt = (float)delta;
        // the clip runs on its own clock; past its end it fades out (the legs never stopped)
        if (Clip != null)
        {
            Time += delta * Rate;
            if (Time >= Clip.Length) { Time = Clip.Length; Fading = true; }
            _w = Fading ? Mathf.MoveToward(_w, 0f, dt / 0.15f) : Mathf.MoveToward(_w, 1f, dt / 0.08f);
            if (Fading && _w <= 0f) Clip = null;
        }
        else _w = Mathf.MoveToward(_w, 0f, dt / 0.15f);
        _twistNow = Mathf.Lerp(_twistNow, Twist, 1f - Mathf.Exp(-dt * 14f));

        if (Clip != null && _w > 0.001f)
        {
            var tr = Tracks(sk, Clip);
            for (int k = 0; k < _bones.Length; k++)
            {
                int b = _bones[k], t = tr[b];
                if (t < 0) continue;
                var q = Clip.RotationTrackInterpolate(t, Time);
                var cur = sk.GetBonePoseRotation(b);
                sk.SetBonePoseRotation(b, cur.Normalized().Slerp(q.Normalized(), _w * _boneW[k]).Normalized());
            }
        }
        // the chest turns to the aim: the twist is spread over the spine, about the model's up axis in each parent's space
        if (Mathf.Abs(_twistNow) > 0.001f && _spine.Length > 0)
        {
            float share = _twistNow / _spine.Length;
            foreach (int b in _spine)
            {
                int parent = sk.GetBoneParent(b);
                var pb = parent >= 0 ? sk.GetBoneGlobalPose(parent).Basis.Orthonormalized() : Basis.Identity;
                var axis = (pb.Inverse() * Vector3.Up).Normalized();
                var cur = sk.GetBonePoseRotation(b);
                sk.SetBonePoseRotation(b, (new Quaternion(axis, share) * cur).Normalized());
            }
        }
    }
}

public partial class CharModel
{
    private CharUpperLayer _upper;
    private bool _layered;
    private float _hipYaw;
    private Vector3 _aimDir = Vector3.Back;

    /// <summary>True while the upper-body layer drives the arms (a cast or shot playing over the legs).</summary>
    public bool UpperBusy => _upper != null && IsInstanceValid(_upper) && _upper.Busy;
    /// <summary>True between <see cref="PlayUpperAttack"/> and <see cref="EndUpper"/>.</summary>
    public bool Layered => _layered;

    private CharUpperLayer EnsureUpper()
    {
        if (Skeleton == null) return null;
        if (_upper != null && IsInstanceValid(_upper)) return _upper;
        _upper = new CharUpperLayer { Name = "UpperLayer" };
        _upper.Setup(Skeleton);
        Skeleton.AddChild(_upper);
        Skeleton.MoveChild(_upper, 0);          // before the grip IK: the left hand is placed on the layered arms
        return _upper;
    }

    /// <summary>
    /// Plays an attack / cast clip on the upper body only, timed so it strikes <paramref name="contactIn"/> seconds from
    /// now (as <see cref="PlayAttack"/>), with the chest turned toward <paramref name="aimDir"/> (world XZ). The legs keep
    /// what <see cref="SetLocomotion"/> asks for. Call <see cref="EndUpper"/> when the action is over.
    /// </summary>
    public void PlayUpperAttack(string clip, float speed, float contactIn, Vector3 aimDir)
    {
        if (Anim == null || _clips != null) { PlayAttack(clip, speed, contactIn, 0.06f); return; }      // a beast: whole body as before
        if (!Anim.HasAnimation(clip)) { if (MissingAnims.Add(clip)) Log.Error("model", $"animation '{clip}' requested by '{ModelId}' does not exist"); return; }
        var up = EnsureUpper();
        if (up == null) return;
        var a = Anim.GetAnimation(clip);
        float st = Strike.TryGetValue(clip, out float s) ? s : (float)a.Length * 0.45f;
        double rate = Math.Max(0.05, speed), start = 0;
        if (contactIn > 0.01f)
        {
            double atContact = contactIn * rate;
            if (st > atContact + 0.04) start = Math.Min(st - atContact, a.Length - 0.05);
            else if (st < atContact - 0.04) rate = Math.Max(st / contactIn, rate * 0.35);
        }
        up.Clip = a; up.Time = start; up.Rate = rate; up.Fading = false;
        if (!_layered) _hipYaw = GlobalRotation.Y;
        _layered = true;
        _aimDir = aimDir.LengthSquared() > 1e-4f ? aimDir : _aimDir;
        SetProcess(true);
    }

    /// <summary>
    /// While layered: the legs walk / jog toward <paramref name="moveDir"/> (world XZ) at <paramref name="speed"/> m/s (an idle
    /// below 0.2). The hips face the movement while the chest stays within 90° of the aim; when the aim lies behind
    /// the movement, the hips face the aim and the legs back-pedal (the locomotion clip played backwards: the library
    /// has no back-pedal or strafe clip). Rotates this model under its actor; <see cref="EndUpper"/> undoes it.
    /// </summary>
    public void SetLocomotion(Vector3 moveDir, float speed, Vector3? aimDir = null)
    {
        if (!_layered || Anim == null) return;
        if (aimDir is Vector3 ad && ad.LengthSquared() > 1e-4f) _aimDir = ad;
        float aimYaw = Mathf.Atan2(_aimDir.X, _aimDir.Z);
        float hip = aimYaw;
        bool back = false;
        if (speed > 0.2f && moveDir.LengthSquared() > 1e-4f)
        {
            float moveYaw = Mathf.Atan2(moveDir.X, moveDir.Z);
            float diff = Mathf.Wrap(aimYaw - moveYaw, -Mathf.Pi, Mathf.Pi);
            back = Mathf.Abs(diff) > Mathf.DegToRad(100f);
            hip = back ? moveYaw + Mathf.Pi : moveYaw;
            Locomote(speed, 0.12f, "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop");
            if (back) Anim.SpeedScale = -Mathf.Abs(Anim.SpeedScale);
        }
        else Play(StanceIdle, 1f, 0.15f);
        // the hips turn smoothly; the chest makes up the rest toward the aim, at most 90°
        _hipYaw = Mathf.LerpAngle(_hipYaw, hip, 0.35f);
        float parentYaw = GetParent() is Node3D p3 ? p3.GlobalRotation.Y : 0f;
        Rotation = new Vector3(Rotation.X, Mathf.Wrap(_hipYaw - parentYaw, -Mathf.Pi, Mathf.Pi), Rotation.Z);
        if (_upper != null) _upper.Twist = Mathf.Clamp(Mathf.Wrap(aimYaw - _hipYaw, -Mathf.Pi, Mathf.Pi), -Mathf.Pi / 2f, Mathf.Pi / 2f);
    }

    /// <summary>Ends the layered mode: the upper clip fades out, the model faces its actor again, full-body clips resume.</summary>
    public void EndUpper()
    {
        if (!_layered) return;
        _layered = false;
        if (_upper != null && IsInstanceValid(_upper)) { _upper.Fading = true; _upper.Twist = 0f; }
        Rotation = new Vector3(Rotation.X, 0f, Rotation.Z);
        if (Anim != null && Anim.SpeedScale < 0f) Anim.SpeedScale = -Anim.SpeedScale;
    }
}
