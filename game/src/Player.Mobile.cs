using Godot;
using System;


namespace AshenAbyss;

/// <summary>
/// [MOBILE 0.4.1] Moving while attacking (施法時可移動). The user: "is it currently possible for the hunter to shoot at a
/// long-distance target while moving away from the target at the same time? … this feature is important for all ranged
/// classes because they are mostly weak at melee and need to keep a distance from enemies while attacking."
///
/// A skill flagged <see cref="SkillDef.Mobile"/> (SkillDb.MobileIds: projectiles, chains, ground spells, pools, curses,
/// traps, commands) no longer roots the hero: during its cast W/A/S/D or a held left click on the ground keep moving at
/// <see cref="MobileSpeedMul"/> of the normal speed (65 %), the hero keeps facing the aim and the shot leaves toward the
/// aim (SkillExec.Fire aims from where the hero is at the contact frame). Shift still roots; a click on an enemy out of
/// reach still runs up to it, then shoots. The setting off (Settings.MoveWhileCasting, or --mobile-cast=0) restores the
/// old stop-and-shoot behaviour exactly.
///
/// Animation: standing, the full-body attack clip plays as before. Moving, the upper-body layer RENDER is adding to
/// CharModel (PlayUpperAttack / SetLocomotion / EndUpper, Y:\tools\gear\CONTRACT.md) is used through <see cref="UpperBody"/>,
/// which finds it by reflection so this file compiles before and after it lands. Without it the legs keep walking
/// (a backward step plays the walk or jog in reverse) while the body faces the aim; the attack clip shows only when standing.
/// </summary>
public partial class Player
{
    /// <summary>Movement speed during a Mobile cast, as a share of the normal speed (--mobile-speed=0.65 overrides).</summary>
    public const float MobileSpeedDefault = 0.65f;
    /// <summary>Damage of a Mobile shot that leaves while the hero walks (balance trim: ranged boss fights became faster than melee; --mobile-dmg overrides). Standing shots: full damage.</summary>
    public const float MobileDmgDefault = 0.9f;

    /// <summary>Test harness: a held movement-key direction (x: A/D, y: W/S), as if those keys were down. Zero = real keys only.</summary>
    public static Vector2 TestMove;

    /// <summary>Test harness statistics: Mobile casts begun, of them begun or continued while moving, metres walked during them.</summary>
    public int MobileCasts, MobileCastsMoving;
    public float MobileMetres;
    /// <summary>Test harness: where the hero stood and where the shot was aimed at the contact frame of the last cast.</summary>
    public Vector2 LastFireFrom, LastFireAim;
    /// <summary>Test harness: whether the last shot left on the move (and so took the moving-shot damage).</summary>
    public bool LastFireMoving;

    private static float _speedArg = -1f, _dmgArg = -1f;
    private static bool? _castArg;
    private static bool _argsRead;

    private static void ReadArgs()
    {
        if (_argsRead || Game.I == null) return;
        _argsRead = true;
        string v = Game.I.ArgValue("mobile-cast");
        if (v != null) _castArg = v != "0" && !v.Equals("off", StringComparison.OrdinalIgnoreCase) && !v.Equals("false", StringComparison.OrdinalIgnoreCase);
        string dm = Game.I.ArgValue("mobile-dmg");
        if (dm != null && float.TryParse(dm, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fd)) _dmgArg = Mathf.Clamp(fd, 0.1f, 1.5f);
        string s = Game.I.ArgValue("mobile-speed");
        if (s != null && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) _speedArg = Mathf.Clamp(f, 0.1f, 1f);
        if (v != null || s != null || dm != null) Log.Info("mobile", $"switches: mobile-cast={_castArg?.ToString() ?? "setting"} mobile-speed={(_speedArg > 0 ? _speedArg : MobileSpeedDefault):0.00} mobile-dmg={(_dmgArg > 0 ? _dmgArg : MobileDmgDefault):0.00}");
    }

    /// <summary>Test harness: forces the setting on or off (null = the player's setting / the switch).</summary>
    public static bool? MobileForce;

    /// <summary>施法時可移動: the setting (on by default), unless --mobile-cast or a test overrides it.</summary>
    public static bool MobileCastOn
    {
        get
        {
            if (MobileForce != null) return MobileForce.Value;
            ReadArgs();
            if (_castArg != null) return _castArg.Value;
            return Game.I?.Cfg?.MoveWhileCasting ?? true;
        }
    }

    public static float MobileSpeedMul { get { ReadArgs(); return _speedArg > 0f ? _speedArg : MobileSpeedDefault; } }
    public static float MobileDmgMul { get { ReadArgs(); return _dmgArg > 0f ? _dmgArg : MobileDmgDefault; } }

    private bool _castWalking;          // this cast began on the move, or the hero has walked during it (MobileStep)
    private float _castWalkM;

    /// <summary>The damage multiplier of the shot leaving now: a Mobile shot fired on the move deals MobileDmgMul (90 %).</summary>
    private float MobileFireMul()
    {
        LastFireMoving = _castDef != null && _castDef.Mobile && MobileCastOn && (_castWalking || _castWalkM > 0.15f);
        return LastFireMoving ? MobileDmgMul : 1f;
    }

    /// <summary>True while a Mobile skill is being cast with the setting on: the hero may walk (Shift still roots).</summary>
    public bool MobileCasting => _act == Act.Cast && _castDef != null && _castDef.Mobile && MobileCastOn;

    /// <summary>The skill being cast right now (null when none), for the harness and the pilot.</summary>
    public SkillDef CastingDef => _act == Act.Cast || _act == Act.Channel ? _castDef : null;

    private bool _castUpper, _castLegs, _noUpper, _movedFrame;
    private string _castAnim;
    private float _castAnimSpeed = 1f;
    private float _aimHoldT;
    private Vector2 _holdAim;
    private Actor _holdTarget;

    /// <summary>The movement keys held right now (W/A/S/D, or the harness's TestMove).</summary>
    private Vector2 KeyWish()
    {
        Vector2 wish = TestMove;
        if (InputEnabled)
        {
            if (Input.IsKeyPressed(Key.W)) wish.Y -= 1; if (Input.IsKeyPressed(Key.S)) wish.Y += 1;
            if (Input.IsKeyPressed(Key.A)) wish.X -= 1; if (Input.IsKeyPressed(Key.D)) wish.X += 1;
        }
        return wish;
    }

    /// <summary>Whether a Mobile skill (with the setting on) should skip the run-up because the player steers with W/A/S/D.</summary>
    private bool SteeringMobile(SkillDef d) => d != null && d.Mobile && MobileCastOn && KeyWish() != Vector2.Zero;

    private Vector2 CastAimNow()
    {
        var t = _act == Act.Cast ? _castTarget : _holdTarget;
        if (t != null && IsInstanceValid(t) && !t.Dead) return t.Pos2;
        return _act == Act.Cast ? _castPoint : _holdAim;
    }

    /// <summary>
    /// BeginCast's attack clip. Standing (or a skill that roots, or the setting off): the full-body clip, exactly as before.
    /// A Mobile skill begun on the move: the upper-body layer when CharModel has it, else the legs keep walking.
    /// </summary>
    private void PlayCastAnim(SkillDef d, string anim, float speed, float contactIn)
    {
        var m = ActiveModel;
        _castAnim = anim; _castAnimSpeed = speed; _castLegs = false; _noUpper = false; _castWalking = false; _castWalkM = 0f;
        bool mobile = d.Mobile && MobileCastOn;
        if (mobile) MobileCasts++;
        bool moving = mobile && !ShiftHeld && !(Rooted && !Unstoppable) && (KeyWish() != Vector2.Zero || _hasMove);
        if (moving) _castWalking = true;
        if (moving)
        {
            MobileCastsMoving++;
            Vector2 aim = CastAimNow() - Pos2;
            if (UpperBody.Has(m) && UpperBody.Play(m, anim, speed, contactIn, aim)) { _castUpper = true; return; }
            _noUpper = true;            // this model cannot layer (a beast form) or the layer is off
            _castLegs = true;           // fallback: no attack clip, the walk goes on (MobileStep)
            return;
        }
        if (_castUpper) { UpperBody.End(m); _castUpper = false; }
        m.PlayAttack(anim, speed, contactIn, 0.06f);        // the swing is lined up with the contact frame
    }

    /// <summary>True while the movement code should face the aim and animate as a Mobile cast (the cast, and 0.3 s after it while still walking).</summary>
    private bool MobileMoveNow => MobileCasting || _aimHoldT > 0f && _act == Act.Free;

    /// <summary>One frame of walking during a Mobile cast (or the aim hold after it): face the aim, legs and upper body.</summary>
    private void MobileStep(Vector2 vel, Vector2 moved, float dt)
    {
        if (_act == Act.Cast) { MobileMetres += moved.Length(); _castWalkM += moved.Length(); _aimHoldT = 0.3f; _holdAim = _castPoint; _holdTarget = _castTarget; }
        Vector2 aim = CastAimNow() - Pos2;
        if (aim.LengthSquared() > 0.01f) FaceDir(aim, 1f - Mathf.Exp(-22f * dt));
        var m = ActiveModel;
        float speed = moved.Length() / Mathf.Max(1e-4f, dt);
        _movedFrame = true;
        if (UpperBody.Has(m) && !_noUpper)
        {
            if (!_castUpper && _act == Act.Cast && _castAnim != null)
            {
                // began standing, now walking: hand the swing over to the upper body, still lined up with the contact frame
                float left = _fired ? 0.02f : Mathf.Max(0.02f, _contactT - _actT);
                _castUpper = UpperBody.Play(m, _castAnim, _castAnimSpeed, left, aim);
                if (!_castUpper) _noUpper = true;
            }
            if (_castUpper) { UpperBody.Locomotion(m, vel.Normalized(), speed, aim); return; }
        }
        // no upper-body layer: a full-body clip begun standing plays on to its contact frame; then the legs walk
        if (_act == Act.Cast && !_castLegs && !_fired) return;
        _castLegs = true;
        MobileLegs(vel, speed);
    }

    /// <summary>The hero stands still during a Mobile cast: the upper body plays on over standing legs.</summary>
    private void MobileStand()
    {
        var m = ActiveModel;
        if (_castUpper) { UpperBody.Locomotion(m, Vector2.Zero, 0f, CastAimNow() - Pos2); return; }
        if (_castLegs) { m.Play(IdleAnim, 1f, 0.15f); _castLegs = false; }
    }

    /// <summary>Fallback legs (no upper-body layer): the walk or jog that fits the speed, in reverse when stepping backwards.</summary>
    private void MobileLegs(Vector2 vel, float speed)
    {
        var m = ActiveModel;
        if (vel.LengthSquared() < 1e-4f) return;
        float back = vel.Normalized().Dot(Forward);
        if (back > -0.35f) { m.Locomote(Mathf.Max(speed, 0.5f), 0.12f, "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop"); return; }
        float scale = Mathf.Max(0.05f, m.Scale.X);
        string clip = speed > 2.2f ? "Jog_Fwd_Loop" : "Walk_Loop";
        float stride = CharModel.Stride.TryGetValue(clip, out float st) ? st : 1f;
        float rate = Mathf.Clamp(speed / (stride * scale), CharModel.MinMoveRate, CharModel.MaxMoveRate);
        m.Play(clip, -rate, 0.12f);          // the same stride played backwards: a step back, feet on the ground
    }

    /// <summary>End of the frame: the upper-body layer is released once the cast and the aim hold are over.</summary>
    private void MobileTickEnd(float dt)
    {
        if (_aimHoldT > 0f && _act != Act.Cast) _aimHoldT -= dt;
        if (_act != Act.Free && _act != Act.Cast) _aimHoldT = 0f;      // a dash, an evade or a channel takes over
        // standing after the cast (no aim hold): back to the full body at once, the idle clip is the normal one
        if (_castUpper && _act != Act.Cast && (_aimHoldT <= 0f || !_movedFrame)) { UpperBody.End(ActiveModel); _castUpper = false; _aimHoldT = 0f; }
        _movedFrame = false;
        if (_act != Act.Cast && _aimHoldT <= 0f) _castLegs = false;
    }
}

/// <summary>
/// [MOBILE 0.4.1] Thin wrapper over CharModel's upper-body layer (RENDER, CharUpperLayer.cs; Y:\tools\gear\CONTRACT.md
/// "RENDER API 2026-11-24"): <c>PlayUpperAttack</c>, <c>SetLocomotion</c> (every frame while layered), <c>EndUpper</c>.
/// Directions are Pos2-space ground vectors, handed over as world XZ. A model that cannot layer (a beast form: the call
/// falls back to its full-body clip and <c>Layered</c> stays false) counts as having no layer, so Player uses its own
/// fallback legs. --upper-off forces the fallback everywhere (for comparisons).
/// </summary>
public static class UpperBody
{
    private static bool? _off;
    private static bool Off => _off ??= ReadOff();
    private static bool ReadOff()
    {
        bool off = Game.I != null && Game.I.Arg("upper-off");
        Log.Info("mobile", off ? "upper-body layer: off (--upper-off), fallback legs" : "upper-body layer: CharModel.PlayUpperAttack / SetLocomotion / EndUpper");
        return off;
    }

    private static Vector3 W(Vector2 v) => new(v.X, 0f, v.Y);

    /// <summary>True when walking casts may use the layer on this model.</summary>
    public static bool Has(CharModel m) => !Off && m != null && GodotObject.IsInstanceValid(m);

    /// <summary>Starts the clip on the upper body; false when the model could not layer it (it then played full-body).</summary>
    public static bool Play(CharModel m, string clip, float speed, float contactIn, Vector2 aimDir)
    {
        if (!Has(m) || clip == null) return false;
        m.PlayUpperAttack(clip, speed, contactIn, W(aimDir));
        return m.Layered;
    }

    public static void Locomotion(CharModel m, Vector2 moveDir, float speed, Vector2 aimDir)
    {
        if (m == null || !GodotObject.IsInstanceValid(m) || !m.Layered) return;
        m.SetLocomotion(W(moveDir), speed, aimDir.LengthSquared() > 1e-4f ? W(aimDir) : null);
    }

    public static void End(CharModel m) { if (m != null && GodotObject.IsInstanceValid(m)) m.EndUpper(); }
}
